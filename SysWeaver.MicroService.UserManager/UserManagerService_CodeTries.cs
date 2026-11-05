using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed partial class UserManagerService
    {
        #region Wrong short code tries per account

        /// <summary>
        /// The wrong short code tries of one account (or one unknown user id), from all browsers together: every limit must allow a try.
        /// A right code is never counted. In memory only.
        /// </summary>
        sealed class CodeTryGuard
        {
            public CodeTryGuard(RateLimiterParams[] limits)
            {
                Limiters = limits.Select(x => new RateLimiter(x)).ToArray();
            }

            /// <summary>
            /// Check a try and count it when it is wrong (atomic: parallel tries never get past the limit)
            /// </summary>
            /// <param name="right">True if the code is right</param>
            /// <returns>1 right, 0 wrong (counted), -2 too many wrong tries (nothing is checked or counted, also not a right code)</returns>
            public int Try(bool right)
            {
                var l = Limiters;
                lock (l)
                {
                    foreach (var x in l)
                        if (x.IsAtLimit(out _))
                            return -2;
                    if (right)
                        return 1;
                    foreach (var x in l)
                        x.Add();
                    return 0;
                }
            }

            /// <summary>
            /// True if no wrong try is remembered (the guard can be dropped)
            /// </summary>
            public bool IsIdle
            {
                get
                {
                    var l = Limiters;
                    lock (l)
                        return l.All(x => x.IsIdle);
                }
            }

            readonly RateLimiter[] Limiters;
        }

        /// <summary>
        /// The configured wrong code tries per account (<see cref="UserManagerParams.CodeTriesPerUser"/>), empty for no limit on the session codes
        /// </summary>
        readonly RateLimiterParams[] CodeTryLimits;

        /// <summary>
        /// The limits of the account reset codes (<see cref="ValidateResetCode"/>): <see cref="CodeTryLimits"/>, or 10 per 15 minutes when none
        /// are configured (an account code can be tried from any browser, it always needs a limit per account)
        /// </summary>
        readonly RateLimiterParams[] ResetCodeTryLimits;

        static RateLimiterParams[] GetCodeTryLimits(RateLimiterParams[] p)
        {
            var l = (p ?? []).Where(x => x != null).ToArray();
            foreach (var x in l)
                x.Validate();
            return l;
        }

        /// <summary>
        /// The guards per user id
        /// </summary>
        readonly ConcurrentDictionary<long, CodeTryGuard> CodeTryUsers = new ConcurrentDictionary<long, CodeTryGuard>();

        /// <summary>
        /// The guards per user id text that has no account (so an unknown id answers like a known one, see <see cref="ValidateResetCode"/>)
        /// </summary>
        readonly ConcurrentDictionary<String, CodeTryGuard> CodeTryUnknown = new ConcurrentDictionary<String, CodeTryGuard>(StringComparer.Ordinal);

        /// <summary>
        /// Check a short code try against the limits of an account
        /// </summary>
        /// <param name="userId">The user</param>
        /// <param name="right">True if the code is right</param>
        /// <returns>1 right, 0 wrong (counted), -2 too many wrong tries</returns>
        int TryUserCode(long userId, bool right)
            => CodeTryUsers.GetOrAdd(userId, _ => new CodeTryGuard(ResetCodeTryLimits)).Try(right);

        /// <summary>
        /// Check a short code try for a user id that has no account (always wrong)
        /// </summary>
        /// <param name="identifier">The user id as entered</param>
        /// <returns>0 wrong (counted), -2 too many wrong tries</returns>
        int TryUnknownCode(String identifier)
        {
            var key = identifier.Trim().ToLowerInvariant();
            if (CodeTryUnknown.TryGetValue(key, out var g))
                return g.Try(false);
            //  A flood of made up ids can't fill the memory (they are forgotten when idle, see PruneCodes)
            if (CodeTryUnknown.Count >= MaxUnknownCodeGuards)
                return 0;
            return CodeTryUnknown.GetOrAdd(key, _ => new CodeTryGuard(ResetCodeTryLimits)).Try(false);
        }

        /// <summary>
        /// At most this many unknown user ids are remembered (<see cref="CodeTryUnknown"/>)
        /// </summary>
        const int MaxUnknownCodeGuards = 100000;

        /// <summary>
        /// Check a try of the short code in a session (<see cref="ValidateShortCode"/>, <see cref="GetAction"/>): each code allows 10 tries,
        /// and the code of a known account also counts against its <see cref="CodeTryLimits"/>.
        /// A try is taken first (atomic: parallel requests can't get more wrong tries), and given back when the code is right
        /// (the pages after the code step use it again, a right code never uses up the tries).
        /// </summary>
        /// <param name="s">The session</param>
        /// <param name="d">The short code of the session</param>
        /// <param name="code">The cleaned up code (<see cref="CleanUpShortCode"/>)</param>
        /// <returns>1 right, 0 wrong, -1 no longer works (removed from the session), -2 too many wrong tries for the account</returns>
        int TrySessionCode(HttpSession s, ShortData d, String code)
        {
            if (DateTime.UtcNow >= d.Expiration)
            {
                s.TryRemove(ShortCodeName);
                return -1;
            }
            if (Interlocked.Decrement(ref d.TriesLeft) < 0)
            {
                s.TryRemove(ShortCodeName);
                return -1;
            }
            bool right = d.ShortCode.FastEquals(code);
            if ((d.UserId != 0) && (CodeTryLimits.Length > 0) && (TryUserCode(d.UserId, right) < 0))
            {
                Interlocked.Increment(ref d.TriesLeft);
                return -2;
            }
            if (!right)
                return 0;
            Interlocked.Increment(ref d.TriesLeft);
            return 1;
        }

        #endregion//Wrong short code tries per account

        #region Reset codes per account

        /// <summary>
        /// A password reset code that was sent to an account (any browser can use it, see <see cref="ValidateResetCode"/>)
        /// </summary>
        sealed class AccountCode
        {
            public String ShortCode;
            public DateTime Expiration;
        }

        /// <summary>
        /// The live reset codes and the live reset token of an account
        /// </summary>
        sealed class AccountCodes
        {
            public readonly List<AccountCode> Codes = new List<AccountCode>();
            public String Token;
        }

        /// <summary>
        /// The reset codes sent per user id (lock it). Every code sent within its <see cref="ResetPasswordShortTimeout"/> works (a late message
        /// still works, and asking again can't make a code that was sent useless), they all use the newest token (the only live one).
        /// In memory only: a restart forgets the codes (the links still work).
        /// </summary>
        readonly Dictionary<long, AccountCodes> ResetCodes = new Dictionary<long, AccountCodes>();

        /// <summary>
        /// Remember a reset code sent to an account
        /// </summary>
        /// <param name="userId">The user</param>
        /// <param name="shortCode">The code</param>
        /// <param name="token">The token (the newest, older reset tokens of the account are revoked)</param>
        /// <param name="expiration">When the code expires (UTC)</param>
        void AddResetCode(long userId, String shortCode, String token, DateTime expiration)
        {
            var now = DateTime.UtcNow;
            lock (ResetCodes)
            {
                if (!ResetCodes.TryGetValue(userId, out var a))
                    ResetCodes[userId] = a = new AccountCodes();
                a.Codes.RemoveAll(x => (now >= x.Expiration) || x.ShortCode.FastEquals(shortCode));
                a.Codes.Add(new AccountCode { ShortCode = shortCode, Expiration = expiration });
                a.Token = token;
            }
        }

        /// <summary>
        /// Forget the reset codes of an account (the reset token is revoked or used)
        /// </summary>
        /// <param name="userId">The user</param>
        void RemoveResetCodes(long userId)
        {
            lock (ResetCodes)
                ResetCodes.Remove(userId);
        }

        /// <summary>
        /// Find a live reset code of an account
        /// </summary>
        /// <param name="userId">The user</param>
        /// <param name="shortCode">The cleaned up code</param>
        /// <param name="token">The reset token if found</param>
        /// <param name="expiration">When the code expires if found</param>
        /// <returns>True if the code is a live reset code of the account</returns>
        bool FindResetCode(long userId, String shortCode, out String token, out DateTime expiration)
        {
            token = null;
            expiration = default;
            var now = DateTime.UtcNow;
            lock (ResetCodes)
            {
                if (!ResetCodes.TryGetValue(userId, out var a))
                    return false;
                foreach (var x in a.Codes)
                {
                    if ((now < x.Expiration) && x.ShortCode.FastEquals(shortCode))
                    {
                        token = a.Token;
                        expiration = x.Expiration;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Forget the expired reset codes and the idle guards (called by Prune)
        /// </summary>
        void PruneCodes()
        {
            var now = DateTime.UtcNow;
            lock (ResetCodes)
            {
                List<long> old = null;
                foreach (var x in ResetCodes)
                {
                    x.Value.Codes.RemoveAll(c => now >= c.Expiration);
                    if (x.Value.Codes.Count <= 0)
                        (old ??= new List<long>()).Add(x.Key);
                }
                if (old != null)
                    foreach (var x in old)
                        ResetCodes.Remove(x);
            }
            foreach (var x in CodeTryUsers)
                if (x.Value.IsIdle)
                    CodeTryUsers.TryRemove(x);
            foreach (var x in CodeTryUnknown)
                if (x.Value.IsIdle)
                    CodeTryUnknown.TryRemove(x);
        }

        /// <summary>
        /// The user id of a user subject (<see cref="UserSubject"/>), 0 for none
        /// </summary>
        /// <param name="subject">The subject, may be null</param>
        static long SubjectUserId(String subject)
        {
            if ((subject == null) || !subject.StartsWith("user:", StringComparison.Ordinal))
                return 0;
            var bar = subject.IndexOf('|');
            var t = bar < 0 ? subject.AsSpan(5) : subject.AsSpan(5, bar - 5);
            return long.TryParse(t, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0;
        }

        /// <summary>
        /// The session key of the user id that the session asked a password reset for (<see cref="ForgotPassword"/>, never returned)
        /// </summary>
        const String ResetIdName = "UserManager.ResetId";

        #endregion//Reset codes per account
    }
}
