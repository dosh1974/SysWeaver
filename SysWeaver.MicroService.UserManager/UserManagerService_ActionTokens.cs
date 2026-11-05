using System;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.MicroService.Db;
using SysWeaver.Net;
using SysWeaver.Security;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{
    public sealed partial class UserManagerService
    {
        #region Check an action token (peek, without using it)

        /// <summary>
        /// What a link (action token) is and whether it still works, WITHOUT using it: call it when a link page loads, so a dead link shows
        /// "This link no longer works" with the real cause (used, a newer link was sent, the password / e-mail / phone changed, expired) before the user acts,
        /// and a live link shows the facts (account, new address) in its confirmation.
        /// <para>A short code (32 characters or less) is not looked up (State "Unknown"). Facts are only returned for a live link of the asked kind.</para>
        /// </summary>
        /// <param name="r">The token and the kind of link the page expects</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>The state, the kind, the reason (when it doesn't work) and the facts (when it works)</returns>
        [WebApi]
        public async Task<ActionTokenState> CheckActionToken(ActionTokenRequest r, HttpServerRequest context)
        {
            var res = await InternalCheckActionToken(r?.Token, r?.Kind).ConfigureAwait(false);
            var reason = res.Reason;
            if ((reason != null) && (context != null))
                res.Reason = await context.Translator.TranslateSafe(reason, context.Session?.Language ?? context.Language, "en", "Why a link that was sent in an e-mail or text message can't be used, shown to the user", TranslationEffort.Medium, TranslationCacheRetention.Long).ConfigureAwait(false);
            return res;
        }

        /// <summary>
        /// The kind name of an action type (see <see cref="ActionTokenKinds"/>).
        /// </summary>
        /// <param name="type">The action type</param>
        /// <param name="restore">True for a restore link (subject variant <see cref="RestoreVariant"/>)</param>
        /// <returns>The kind</returns>
        public static String ActionTokenKind(String type, bool restore = false)
        {
            switch (type)
            {
                case DbAction.ResetPassword: return ActionTokenKinds.ResetPassword;
                case DbAction.AddPassword: return ActionTokenKinds.AddPassword;
                case DbAction.DeletePassword: return ActionTokenKinds.DeletePassword;
                case DbAction.DeleteUser: return ActionTokenKinds.DeleteAccount;
                case DbAction.AddEmail: return restore ? ActionTokenKinds.RestoreEmail : ActionTokenKinds.AddEmail;
                case DbAction.AddPhone: return restore ? ActionTokenKinds.RestorePhone : ActionTokenKinds.AddPhone;
                case ActionTokenGetResetChallenge: return ActionTokenKinds.AddPassKey;
                case DbAction.CreateUser: return ActionTokenKinds.NewUser;
                case DbAction.UndoEmail: return ActionTokenKinds.UndoEmail;
            }
            return type;
        }

        /// <summary>
        /// True if a link of the actual kind can be used by a page that expects the asked kind (the same api accepts it).
        /// </summary>
        /// <param name="asked">The kind the page expects, null or empty for any</param>
        /// <param name="actual">The kind of the link</param>
        /// <returns>True if it matches</returns>
        public static bool ActionTokenKindMatches(String asked, String actual)
        {
            if (String.IsNullOrEmpty(asked) || String.Equals(asked, actual, StringComparison.Ordinal))
                return true;
            switch (asked)
            {
                case ActionTokenKinds.ResetPassword:
                    return actual == ActionTokenKinds.AddPassword;
                case ActionTokenKinds.AddEmail:
                    return actual == ActionTokenKinds.RestoreEmail;
                case ActionTokenKinds.AddPhone:
                    return actual == ActionTokenKinds.RestorePhone;
                case ActionTokenKinds.AddPassKey:
                    return (actual == ActionTokenKinds.ResetPassword) || (actual == ActionTokenKinds.AddPassword);
            }
            return false;
        }

        /// <summary>
        /// The state of a dead token from its cause ("Used", "Replaced", "Expired", an event ...).
        /// </summary>
        static String StateOfCause(String cause)
        {
            switch (cause)
            {
                case null: return ActionTokenStates.Unknown;
                case CauseUsed: return ActionTokenStates.Used;
                case CauseReplaced: return ActionTokenStates.Replaced;
                case CauseExpired: return ActionTokenStates.Expired;
            }
            return ActionTokenStates.Revoked;
        }

        async Task<ActionTokenState> InternalCheckActionToken(String token, String askedKind)
        {
            token = token?.Trim().Replace(' ', '+');
            var res = new ActionTokenState
            {
                State = ActionTokenStates.Unknown,
            };
            if (String.IsNullOrEmpty(token) || (token.Length <= 32))
            {
                res.Reason = TokenRefusedReason(null, !String.IsNullOrEmpty(token));
                return res;
            }
            DbAction d;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                d = await c.FirstOrDefaultAsync<DbAction>(x => x.Token == token).ConfigureAwait(false);
            if (d == null)
            {
                var cause = DeadCause(token);
                res.State = StateOfCause(cause);
                res.Reason = TokenRefusedReason(cause);
                return res;
            }
            //  The facts in the data
            long uid = 0;
            String target = null;
            String account = null;
            try
            {
                switch (d.Type)
                {
                    case DbAction.ResetPassword:
                    case DbAction.AddPassword:
                    case ActionTokenGetResetChallenge:
                        uid = Data.FromData<InternalNewPasswordData>(d.Data)?.UserId ?? 0;
                        break;
                    case DbAction.DeletePassword:
                    case DbAction.DeleteUser:
                        uid = Data.FromData<long>(d.Data);
                        break;
                    case DbAction.AddEmail:
                        {
                            var e = Data.FromData<InternalNewEmailData>(d.Data);
                            uid = e?.UserId ?? 0;
                            target = e?.Email;
                        }
                        break;
                    case DbAction.AddPhone:
                        {
                            var p = Data.FromData<InternalNewPhoneData>(d.Data);
                            uid = p?.UserId ?? 0;
                            target = p?.Phone;
                        }
                        break;
                    case DbAction.UndoEmail:
                        {
                            var u = Data.FromData<InternalUndoEmailData>(d.Data);
                            uid = u?.UserId ?? 0;
                            target = u?.OldEmail;
                        }
                        break;
                    case DbAction.CreateUser:
                        {
                            var n = Data.FromData<NewUserData>(d.Data);
                            target = n?.Email ?? n?.Phone;
                            account = String.IsNullOrEmpty(n?.UserName) ? target : n.UserName;
                        }
                        break;
                }
            }
            catch
            {
            }
            var restore = (uid > 0) && ((d.Type == DbAction.AddEmail) || (d.Type == DbAction.AddPhone)) && SubjectTokens.HasTag(token, d.Type, UserSubject(uid, RestoreVariant));
            res.Kind = ActionTokenKind(d.Type, restore);
            if (!ActionTokenKindMatches(askedKind, res.Kind))
            {
                res.State = ActionTokenStates.WrongKind;
                res.Reason = "This link is for something else: open it from the message it came in.";
                return res;
            }
            if (DateTime.UtcNow > d.Expiration)
            {
                MarkDead(token, CauseExpired);
                res.State = ActionTokenStates.Expired;
                res.Reason = TokenRefusedReason(CauseExpired);
                return res;
            }
            if (uid > 0)
            {
                using var c = await Db.GetAsync().ConfigureAwait(false);
                var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == uid).ConfigureAwait(false);
                if (user == null)
                {
                    res.State = ActionTokenStates.Revoked;
                    res.Reason = TokenRefusedReason(AccountEvents.AccountDeleted);
                    return res;
                }
                account = user.UserName;
                //  A disabled account: its links don't work until it is enabled again (the page says so on load, not after the user acted)
                if (await IsDisabled(c, uid).ConfigureAwait(false))
                {
                    res.State = ActionTokenStates.Disabled;
                    res.Reason = "An administrator disabled the account: its links don't work until it is enabled again.";
                    return res;
                }
                //  A restore link never replaces a newer address (AddEmail / AddPhone refuse it too)
                if (restore)
                {
                    var newer = d.Type == DbAction.AddEmail
                        ? (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == uid).ConfigureAwait(false)) != null
                        : (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == uid).ConfigureAwait(false)) != null;
                    if (newer)
                    {
                        res.State = ActionTokenStates.Revoked;
                        res.Reason = TokenRefusedReason(CauseNewerContact, false, d.Type);
                        return res;
                    }
                }
            }
            res.State = ActionTokenStates.Live;
            res.Account = account;
            res.Target = target;
            res.Expires = d.Expiration;
            return res;
        }

        #endregion//Check an action token
    }
}
