using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.Security
{
    /// <summary>
    /// Tokens that belong to a subject (ex: a user) and a purpose (ex: "reset password"), so that only ONE token per subject and purpose is live:
    /// issuing a new one revokes the older ones, and an event (ex: a password change) can revoke every token of a subject.
    /// <para>Use it for every link or code that is sent to a person or handed to a client (reset links, invitations, confirmation tokens),
    /// so that an older, unused link can't be used after a newer one was sent or after the account changed.</para>
    /// <para>Two flavours:
    /// <list type="bullet">
    /// <item><see cref="Create"/> / <see cref="Tag"/>: for tokens kept in a database: the token starts with a tag (16 hex characters) made from the purpose and the subject,
    /// so the older tokens of a subject and purpose can be found with a "starts with" query (ex: <c>DELETE ... WHERE Type = purpose AND Token LIKE 'TAG%'</c>).</item>
    /// <item><see cref="OneTimeTokens{T}"/>: an in-memory store that does all of it (one live per subject and purpose, one use, expiry).</item>
    /// </list></para>
    /// </summary>
    public static class SubjectTokens
    {
        /// <summary>
        /// The number of characters of the tag at the start of a token from <see cref="Create"/> (upper case hex).
        /// </summary>
        public const int TagLength = 16;

        /// <summary>
        /// The tag of a subject and purpose: the first 64 bits of a SHA256 of the purpose and the subject, as 16 upper case hex characters
        /// (no characters that are special in a SQL LIKE pattern, and the same in any letter case comparison).
        /// </summary>
        /// <param name="purpose">The purpose, ex: "RstPwd" (case sensitive)</param>
        /// <param name="subject">The subject, ex: "user:42" (case sensitive, normalize it first, ex: lower case an email address)</param>
        /// <returns>The tag</returns>
        /// <exception cref="ArgumentException">The purpose or the subject is null or empty</exception>
        public static String Tag(String purpose, String subject)
        {
            if (String.IsNullOrEmpty(purpose))
                throw new ArgumentException("A purpose is required", nameof(purpose));
            if (String.IsNullOrEmpty(subject))
                throw new ArgumentException("A subject is required", nameof(subject));
            var h = SHA256.HashData(Encoding.UTF8.GetBytes(String.Concat("SwTok1|", purpose, "|", subject)));
            return Convert.ToHexString(h, 0, TagLength / 2);
        }

        /// <summary>
        /// Create a new random token for a subject and purpose: the <see cref="Tag"/> followed by random base64 characters ([A-Za-z0-9+/]).
        /// </summary>
        /// <param name="purpose">The purpose, ex: "RstPwd"</param>
        /// <param name="subject">The subject, ex: "user:42"</param>
        /// <param name="length">The total length of the token (at least 40, 32+ random characters = 192+ bits), the default 48 fits a 48 character column</param>
        /// <returns>The token</returns>
        /// <exception cref="ArgumentException">The purpose or the subject is null or empty, or the length is below 40</exception>
        public static String Create(String purpose, String subject, int length = 48)
        {
            if (length < 40)
                throw new ArgumentException("A token must be at least 40 characters long", nameof(length));
            var tag = Tag(purpose, subject);
            var randomChars = length - TagLength;
            var bytes = RandomNumberGenerator.GetBytes((randomChars * 3 + 3) / 4);
            var r = Convert.ToBase64String(bytes).TrimEnd('=');
            return String.Concat(tag, r.AsSpan(0, randomChars));
        }

        /// <summary>
        /// True if a token was created for this subject and purpose (with <see cref="Create"/>).
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="purpose">The purpose</param>
        /// <param name="subject">The subject</param>
        /// <returns>True if the token starts with the tag of the subject and purpose</returns>
        public static bool HasTag(String token, String purpose, String subject)
        {
            if (String.IsNullOrEmpty(token) || (token.Length < TagLength) || String.IsNullOrEmpty(purpose) || String.IsNullOrEmpty(subject))
                return false;
            return token.StartsWith(Tag(purpose, subject), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A random token without a subject (22 characters, url safe: [A-Za-z0-9_-], 128 bits).
        /// </summary>
        /// <returns>The token</returns>
        public static String Random()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }


    /// <summary>
    /// An in-memory store of one time tokens with ONE live token per subject and purpose (see <see cref="SubjectTokens"/>):
    /// <see cref="Issue"/> revokes the older token of the same subject and purpose, <see cref="TryUse"/> consumes a token (once),
    /// <see cref="Revoke"/> / <see cref="RevokeSubject"/> revoke tokens when something changed (ex: the password), and tokens expire.
    /// <para>Ex: a confirmation token for a dangerous action, bound to the session: <c>var t = Tokens.Issue("delete-log", session.Token, info, out var expires);</c>
    /// then <c>if (!Tokens.TryUse(r.Token, "delete-log", session.Token, out var info)) throw new HttpResponseException(409, "...");</c></para>
    /// </summary>
    /// <typeparam name="T">The data kept with a token</typeparam>
    /// <remarks>Thread safe. Tokens never survive a restart of the process.</remarks>
    public sealed class OneTimeTokens<T>
    {
        /// <summary>
        /// Create a token store.
        /// </summary>
        /// <param name="lifetime">How long a token is valid (at least 1 second)</param>
        public OneTimeTokens(TimeSpan lifetime)
        {
            Lifetime = lifetime < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : lifetime;
        }

        /// <summary>
        /// Create a token store whose purposes are declared in groups: <see cref="RevokeOn"/> revokes, for an event (ex: "the password changed"),
        /// every live token of a subject whose purpose belongs to a group that the event revokes.
        /// </summary>
        /// <param name="lifetime">How long a token is valid (at least 1 second)</param>
        /// <param name="groups">The declared purposes and groups (see <see cref="TokenGroups"/>); <see cref="Issue"/> refuses an undeclared purpose</param>
        /// <exception cref="ArgumentNullException">The groups are null</exception>
        public OneTimeTokens(TimeSpan lifetime, TokenGroups groups) : this(lifetime)
        {
            Groups = groups ?? throw new ArgumentNullException(nameof(groups));
        }

        /// <summary>
        /// The declared purposes and groups, null if the store has none (then only <see cref="Revoke"/> / <see cref="RevokeSubject"/> revoke).
        /// </summary>
        public readonly TokenGroups Groups;

        /// <summary>
        /// How long a token is valid after it was issued.
        /// </summary>
        public readonly TimeSpan Lifetime;

        sealed class Entry
        {
            public String Purpose;
            public String Subject;
            public T Data;
            public DateTime Expires;
        }

        readonly Dictionary<String, Entry> Tokens = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly Dictionary<String, String> Live = new Dictionary<string, string>(StringComparer.Ordinal);

        static String LiveKey(String purpose, String subject) => String.Concat(purpose, "\n", subject);

        void PruneLocked(DateTime now)
        {
            List<String> old = null;
            foreach (var x in Tokens)
                if (x.Value.Expires <= now)
                    (old ??= new List<string>()).Add(x.Key);
            if (old != null)
                foreach (var x in old)
                    RemoveLocked(x);
        }

        void RemoveLocked(String token)
        {
            if (!Tokens.Remove(token, out var e))
                return;
            var k = LiveKey(e.Purpose, e.Subject);
            if (Live.TryGetValue(k, out var t) && String.Equals(t, token, StringComparison.Ordinal))
                Live.Remove(k);
        }

        /// <summary>
        /// Issue a new token for a subject and purpose, the older live token of the same subject and purpose is revoked.
        /// </summary>
        /// <param name="purpose">The purpose, ex: "delete-log"</param>
        /// <param name="subject">The subject, ex: a session token or "user:42"</param>
        /// <param name="data">The data kept with the token (returned by <see cref="TryUse"/>)</param>
        /// <param name="expires">When the token expires (UTC)</param>
        /// <returns>The token (url safe, 128 random bits)</returns>
        /// <exception cref="ArgumentException">The purpose or the subject is null or empty</exception>
        public String Issue(String purpose, String subject, T data, out DateTime expires)
        {
            if (String.IsNullOrEmpty(purpose))
                throw new ArgumentException("A purpose is required", nameof(purpose));
            if (String.IsNullOrEmpty(subject))
                throw new ArgumentException("A subject is required", nameof(subject));
            if ((Groups != null) && !Groups.IsDeclared(purpose))
                throw new ArgumentException("The purpose " + purpose + " is not declared in the token groups of this store", nameof(purpose));
            var now = DateTime.UtcNow;
            expires = now + Lifetime;
            var token = SubjectTokens.Random();
            var k = LiveKey(purpose, subject);
            lock (Tokens)
            {
                PruneLocked(now);
                if (Live.TryGetValue(k, out var older))
                    RemoveLocked(older);
                Tokens[token] = new Entry
                {
                    Purpose = purpose,
                    Subject = subject,
                    Data = data,
                    Expires = expires,
                };
                Live[k] = token;
            }
            return token;
        }

        /// <summary>
        /// Use (consume) a token: it is removed, whether it matches or not, so it can be tried once.
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="purpose">The purpose it must have</param>
        /// <param name="subject">The subject it must belong to, null for any subject</param>
        /// <param name="data">The data kept with the token</param>
        /// <returns>True if the token was live, for this purpose (and subject)</returns>
        public bool TryUse(String token, String purpose, String subject, out T data)
        {
            data = default;
            if (String.IsNullOrEmpty(token))
                return false;
            var now = DateTime.UtcNow;
            Entry e;
            lock (Tokens)
            {
                if (!Tokens.TryGetValue(token, out e))
                    return false;
                RemoveLocked(token);
            }
            if ((e.Expires <= now) || !String.Equals(e.Purpose, purpose, StringComparison.Ordinal))
                return false;
            if ((subject != null) && !String.Equals(e.Subject, subject, StringComparison.Ordinal))
                return false;
            data = e.Data;
            return true;
        }

        /// <summary>
        /// Revoke the live token of a subject and purpose.
        /// </summary>
        /// <param name="purpose">The purpose</param>
        /// <param name="subject">The subject</param>
        /// <returns>True if a token was revoked</returns>
        public bool Revoke(String purpose, String subject)
        {
            lock (Tokens)
            {
                if (!Live.TryGetValue(LiveKey(purpose, subject), out var t))
                    return false;
                RemoveLocked(t);
                return true;
            }
        }

        /// <summary>
        /// Revoke every token of a subject (all purposes), ex: when the user's password changed or the session ended.
        /// </summary>
        /// <param name="subject">The subject</param>
        /// <returns>The number of tokens revoked</returns>
        public int RevokeSubject(String subject)
        {
            lock (Tokens)
            {
                List<String> l = null;
                foreach (var x in Tokens)
                    if (String.Equals(x.Value.Subject, subject, StringComparison.Ordinal))
                        (l ??= new List<string>()).Add(x.Key);
                if (l == null)
                    return 0;
                foreach (var x in l)
                    RemoveLocked(x);
                return l.Count;
            }
        }

        /// <summary>
        /// Something happened to a subject (ex: its password changed): revoke every live token of the subject whose purpose is in a group that the event revokes
        /// (see <see cref="TokenGroups"/>).
        /// </summary>
        /// <param name="evt">The event, ex: "PasswordChanged" (an event that no group declares revokes nothing)</param>
        /// <param name="subject">The subject</param>
        /// <param name="keep">A token to keep (ex: the one just issued because of the event), null for none</param>
        /// <returns>The number of tokens revoked</returns>
        /// <exception cref="InvalidOperationException">The store was created without groups</exception>
        public int RevokeOn(String evt, String subject, String keep = null)
        {
            var g = Groups ?? throw new InvalidOperationException("This token store has no token groups, create it with OneTimeTokens(lifetime, groups)");
            var purposes = g.RevokedBy(evt);
            if (purposes.Count <= 0)
                return 0;
            lock (Tokens)
            {
                List<String> l = null;
                foreach (var x in Tokens)
                {
                    var e = x.Value;
                    if (String.Equals(e.Subject, subject, StringComparison.Ordinal) && purposes.Contains(e.Purpose) && !String.Equals(x.Key, keep, StringComparison.Ordinal))
                        (l ??= new List<string>()).Add(x.Key);
                }
                if (l == null)
                    return 0;
                foreach (var x in l)
                    RemoveLocked(x);
                return l.Count;
            }
        }

        /// <summary>
        /// The number of live (not used, not revoked; maybe expired but not pruned yet) tokens.
        /// </summary>
        public int Count
        {
            get
            {
                lock (Tokens)
                {
                    PruneLocked(DateTime.UtcNow);
                    return Tokens.Count;
                }
            }
        }
    }
}
