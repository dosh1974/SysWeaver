using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Db;
using SysWeaver.Net;
using SimpleStack.Orm;
using SysWeaver.Data;
using SysWeaver.MicroService.Db;
using SimpleStack.Orm.Expressions.Statements.Typed;
using SysWeaver.IsoData;

namespace SysWeaver.MicroService
{
    public sealed partial class UserManagerService
    {


        #region Public privileged API


        /// <summary>
        /// The maximum length of the domain of an invited user.
        /// </summary>
        public const int MaxInviteDomainLength = 256;

        /// <summary>
        /// Validate the domain of an invitation (see <see cref="InviteUserRequest.Domain"/>).
        /// </summary>
        /// <param name="requested">The requested domain, null or white space for the inviter's own domain</param>
        /// <param name="inviterDomain">The domain of the inviter, may be null</param>
        /// <param name="inviterTokens">The (lower case) security tokens of the inviter, may be null</param>
        /// <returns>The domain to store (trimmed): the inviter's domain when none was requested, the requested one when the inviter may use it</returns>
        /// <exception cref="HttpResponseException">400: the domain is too long or has control characters; 403: the domain is not the inviter's own and the inviter is neither Debug nor Admin</exception>
        public static String ValidateInviteDomain(String requested, String inviterDomain, IReadOnlySet<String> inviterTokens)
        {
            var d = requested?.Trim();
            if (String.IsNullOrEmpty(d))
                return String.IsNullOrEmpty(inviterDomain) ? null : inviterDomain;
            if (d.Length > MaxInviteDomainLength)
                throw new HttpResponseException(400, "The domain is too long (at most " + MaxInviteDomainLength + " characters)");
            foreach (var c in d)
                if (Char.IsControl(c))
                    throw new HttpResponseException(400, "The domain may not contain control characters");
            if (String.Equals(d, inviterDomain?.Trim(), StringComparison.OrdinalIgnoreCase))
                return inviterDomain.Trim();
            if ((inviterTokens != null) && (inviterTokens.Contains("debug") || inviterTokens.Contains("admin")))
                return d;
            throw new HttpResponseException(403, "You can only invite users to your own domain" + (String.IsNullOrEmpty(inviterDomain) ? "" : " (" + inviterDomain + ")") + ", leave the domain empty or ask an administrator");
        }

        /// <summary>
        /// Invite a user to join a site.
        /// The domain of the new account is checked (<see cref="ValidateInviteDomain"/>): empty means the inviter's domain, another domain needs Debug or Admin.
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>Response</returns>
        /// <exception cref="HttpResponseException">400 / 403: the domain is not valid or not allowed for the inviter</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.InviteUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> InviteUser(InviteUserRequest r, HttpServerRequest context)
        {
            //  Get validated tokens
            String[] authTokens = null;
            if (r.Tokens != null)
            {
                var a = context?.Session?.Auth?.Tokens;
                if (a != null)
                {
                    var isDebug = a.Contains("debug");
                    var isAdmin = a.Contains("admin");
                    var temp = new HashSet<String>(StringComparer.Ordinal);
                    foreach (var t in r.Tokens)
                    {
                        var tt = t?.FastTrimToLower();
                        if (tt == null)
                            continue;
                        if (a.Contains(tt))
                        {
                            temp.Add(tt);
                            continue;
                        }
                        if (isDebug)
                        {
                            temp.Add(tt);
                            continue;
                        }
                        if (isAdmin)
                        {
                            if (!tt.FastEquals("debug"))
                                temp.Add(tt);
                        }
                    }
                    if (temp.Count > 0)
                        authTokens = temp.ToArray();
                }
            }
            //  Validate target
            var target = r.Email?.Trim();
            var com = GetComs(ref target);
            if (com == null)
                throw InvalidTargetError(target);
            //  The domain: empty = the inviter's own, another one only for Debug / Admin (an inviter can't create accounts in a domain it doesn't manage)
            var inviter = context?.Session?.Auth;
            var domain = ValidateInviteDomain(r.Domain, inviter?.Domain, inviter?.Tokens);

            var name = r.Name?.Trim();
            var nameOrTarget = String.IsNullOrEmpty(name) ? target : name;
            var expires = DateTime.UtcNow + InviteUserTimeout;
            String salt = AuthTools.GetRandomSalt();
            //  Every com but Email sends to a phone number (Sms and WhatsApp): the new account gets it as its phone, not as its e-mail
            var isEmail = com.Name == "Email";
            var tokens = await GetNewUserToken("invite", name, isEmail ? target : null, isEmail ? null : target, salt, authTokens, domain, r.Language, expires, null, context?.Session?.Auth?.Username).ConfigureAwait(false);
            String link = String.Concat(LinkRoot(context), "auth/ChoosePassword.html?user=",
                HttpUtility.UrlEncode(nameOrTarget),
                "&salt=",
                HttpUtility.UrlEncode(salt),
                "&token=", HttpUtility.UrlEncode(tokens.Item1));
            var d = GetMessageParams(context,
                    "[UserName]", nameOrTarget,
                    "[Email]", target,
                    "[Link]", link
                );
            var user = context.Session.Auth.AuthContext as DbUser;
            var lang = await GetLang(r.Language ?? user?.Language ?? context?.Session?.Language).ConfigureAwait(false);
            await com.Send(UserManagerComOps.Invite, lang, target, d, TextSystemString).ConfigureAwait(false);
            //  Audit: who invited whom, with which tokens (never the link or the code)
            var by = context?.Session?.Auth?.Username ?? "?";
            var tokenText = (authTokens == null) || (authTokens.Length == 0) ? "no tokens" : String.Concat("the tokens ", String.Join(", ", authTokens));
            Manager.AddMessage(MessageLevels.Info, $"User \"{by}\" invited \"{target}\" with {tokenText}, the invitation expires {expires:yyyy-MM-dd HH:mm} UTC");
            return true;
        }

        /// <summary>
        /// Force a user to reset their password on the next login.
        /// The password stops working at once: a sign in with it creates no session, instead the user gets a link and a code to choose a new
        /// password (see <see cref="LoginService.Login"/>, <see cref="AuthInfo.PasswordChangeRequired"/>), and the user's current sessions are signed out.
        /// Passkeys still work (they are a separate credential).
        /// </summary>
        /// <param name="userIdentifier">User identifier, typically email</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True if a password was found and reset, false if no password was found</returns>
        /// <exception cref="UserDoNotExistException">There is no such user (use <see cref="ForcePasswordResetUser"/> for a result without exceptions)</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.PasswordReset)]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> ForcePasswordReset(String userIdentifier, HttpServerRequest context)
        {
            var r = await InternalForcePasswordReset(userIdentifier, 0, context).ConfigureAwait(false);
            if (!r.Found)
                throw new UserDoNotExistException(userIdentifier);
            return r.HadPassword;
        }

        /// <summary>
        /// Force a user to reset their password on the next login, like <see cref="ForcePasswordReset"/>, but an unknown user is a result, not an error
        /// (used by the Users page).
        /// </summary>
        /// <param name="userIdentifier">User identifier: user name, e-mail address or phone number</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.PasswordReset)]
        [WebApiAudit(AuditGroup)]
        public Task<ForcePasswordResetResult> ForcePasswordResetUser(String userIdentifier, HttpServerRequest context)
            => InternalForcePasswordReset(userIdentifier?.Trim(), 0, context);

        /// <summary>
        /// Look up the user of a forced password reset without changing anything, so a page can check what was typed before it asks for
        /// the confirmation (<see cref="ForcePasswordResetUser"/> does the reset).
        /// </summary>
        /// <param name="userIdentifier">User identifier: user name, e-mail address or phone number</param>
        /// <param name="context">Context, handled internally (the language of the texts)</param>
        /// <returns>Found, UserName and HadPassword (true if the user has a password that a reset would affect); SignedOut is always 0.
        /// The facts for the confirmation: Reachable, PassKeys, MustResetPassword, Disabled, LockedOut, Consequences (what the reset does, translated sentences)
        /// and Refused (why the reset would be refused: it would leave the user without any way to sign in; null if it can be done).</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.PasswordReset)]
        public async Task<ForcePasswordResetResult> FindPasswordResetUser(String userIdentifier, HttpServerRequest context)
        {
            var res = new ForcePasswordResetResult();
            userIdentifier = userIdentifier?.Trim();
            if (String.IsNullOrEmpty(userIdentifier))
                return res;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var userId = await FindUser(c, userIdentifier).ConfigureAwait(false);
            var a = await GetUserAccess(c, userId).ConfigureAwait(false);
            if (a == null)
                return res;
            res.Found = true;
            res.UserName = a.User.UserName;
            res.HadPassword = a.HasPassword;
            await FillAccessFacts(res, a, context).ConfigureAwait(false);
            return res;
        }

        /// <summary>
        /// Force a password reset from the row action of the users table of the Users page (<see cref="UserAdminTable"/>), like <see cref="ForcePasswordReset"/>.
        /// </summary>
        /// <param name="token">The row token of the user (from the "Actions" column of the users table, it can't be guessed, so a crafted link can't use it)</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        /// <exception cref="ArgumentException">The token is invalid</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.PasswordReset)]
        [WebApiAudit(AuditGroup)]
        public Task<ForcePasswordResetResult> RowForcePasswordReset(String token, HttpServerRequest context)
        {
            var id = UserRowTokenId(token);
            if (id <= 0)
                throw new HttpResponseException(403, "This row is no longer valid: refresh the list and try again");
            return InternalForcePasswordReset(null, id, context);
        }

        async Task<ForcePasswordResetResult> InternalForcePasswordReset(String userIdentifier, long userId, HttpServerRequest context)
        {
            var res = new ForcePasswordResetResult();
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                if (userId <= 0)
                    userId = String.IsNullOrEmpty(userIdentifier) ? 0 : await FindUser(c, userIdentifier).ConfigureAwait(false);
                user = userId <= 0 ? null : await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    return res;
                res.Found = true;
                res.UserName = user.UserName;
                var pwd = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
                //  The placeholder of GetSaltAsync (empty hash) is no password
                if (!IsUsablePassword(pwd))
                    return res;
                //  Never leave a user without any way to sign in: no passkey and nowhere to send the link to choose a new password = refused (409) with the reason
                var a = await GetUserAccess(c, userId).ConfigureAwait(false);
                if ((a != null) && a.ResetLocksOut)
                    throw HttpResponseException.Refused(ResetLocksOutReason(user.UserName));
                res.HadPassword = true;
                pwd.MustResetPassword = true;
                await c.UpdateAsync(pwd, x => new { x.MustResetPassword }).ConfigureAwait(false);
                //  The links sent before stop working: reset, contact change, restore, delete account ... (the user gets a new reset link at the next sign in)
                await RevokeOnEvent(c, userId, AccountEvents.PasswordForcedReset).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            //  The sessions that were signed in (with the old password, or any other way) are signed out, except the caller's own
            var server = context?.Server;
            if (server != null)
            {
                List<HttpSession> sessions = new List<HttpSession>();
                server.EnumUserSessions(MakeGuid(userId), s => sessions.Add(s));
                var own = context.Session;
                foreach (var s in sessions)
                {
                    if (s == own)
                        continue;
                    if (server.CloseSession(s, "An administrator asked you to choose a new password"))
                        ++res.SignedOut;
                }
            }
            var by = context?.Session?.Auth?.Username ?? "?";
            Manager.AddMessage(MessageLevels.Info, $"User \"{user.UserName}\" must choose a new password (forced by \"{by}\"), {res.SignedOut} session(s) signed out");
            return res;
        }


        /// <summary>
        /// Key of the row tokens (users and invitations tables), random per process so tokens never survive a restart.
        /// </summary>
        static readonly byte[] RowTokenKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

        /// <summary>
        /// The first 128 bits (base64url) of a HMAC-SHA256 of a text with <see cref="RowTokenKey"/>.
        /// </summary>
        static String RowMac(String text)
        {
            var h = System.Security.Cryptography.HMACSHA256.HashData(RowTokenKey, System.Text.Encoding.UTF8.GetBytes(text ?? ""));
            return Convert.ToBase64String(h, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// The row token of a user ("id.mac"), sent in the "Actions" column of the users table of the Users page only (<see cref="DbUserAdminRow.Actions"/>).
        /// </summary>
        /// <param name="userId">The user id</param>
        /// <returns>The token</returns>
        internal static String UserRowToken(long userId)
            => String.Concat(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), ".", RowMac("user|" + userId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        /// <summary>
        /// The user id of a row token (<see cref="UserRowToken"/>), 0 if it's invalid.
        /// </summary>
        static long UserRowTokenId(String token)
        {
            if (String.IsNullOrEmpty(token) || (token.Length > 64))
                return 0;
            var i = token.IndexOf('.');
            if (i <= 0)
                return 0;
            if (!long.TryParse(token.AsSpan(0, i), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) || (id <= 0))
                return 0;
            var a = System.Text.Encoding.ASCII.GetBytes(token);
            var b = System.Text.Encoding.ASCII.GetBytes(UserRowToken(id));
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b) ? id : 0;
        }


        const int ClientCache = 4;
        const int ServerCache = 3;
        const int TableRefresh = 5000;

        /// <summary>
        /// All users as a user table
        /// </summary>
        /// <param name="r">Table request</param>
        /// <returns>Table user</returns>
        /// <remarks>The plain table, as before (no row actions). The Users page (auth/Users.html) uses <see cref="UserAdminTable"/>.</remarks>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ViewUser)]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(ServerCache)]
        public async Task<TableData> UserDataTable(TableDataRequest r)
        {
            var ret = await Db.GetAsTableData<DbUser>(r, TableRefresh).ConfigureAwait(false);
            return ret;
        }

        /// <summary>
        /// The user accounts of this site with their row actions (Force password reset, Undo forced reset, Disable, Enable; only for users that may
        /// manage users, and only the actions that fit each user's state).
        /// </summary>
        /// <param name="r">Table request</param>
        /// <param name="context">Context, handled internally (the row actions are only sent to users that may use them)</param>
        /// <returns>The users</returns>
        /// <remarks>Shown on the Users page (auth/Users.html, user menu Management > Users). Not cached on the server: the
        /// re-read right after a row action, or after a delete, shows the new state at once (with a request cache it showed the old row and its
        /// old actions until the next refresh). The search box searches the user name, domain, language and nick name (DbData plain text search).</remarks>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ViewUser)]
        public async Task<TableData> UserAdminTable(TableDataRequest r, HttpServerRequest context)
        {
            var ret = await Db.GetAsTableData<DbUserAdminRow>(r, TableRefresh).ConfigureAwait(false);
            if (!DbDataType<DbUserAdminRow>.MemberNameToColumn.TryGetValue(nameof(DbUserAdminRow.Actions), out var ai))
                return ret;
            //  the row actions (and their tokens) only for users that may manage users
            if (!(context?.Session?.Auth?.IsValid(UserManagerRoles.ManageUser) ?? false))
            {
                foreach (var row in ret.Rows ?? [])
                {
                    var v = row?.Values;
                    if ((v != null) && (ai < v.Length))
                        v[ai] = "";
                }
                return ret;
            }
            //  and only on users that have a password (a reset does nothing to a passkey only user), one look up per shown row
            if (DbDataType<DbUserAdminRow>.MemberNameToColumn.TryGetValue(nameof(DbUserAdminRow.Id), out var ii))
            {
                using var c = await Db.GetAsync().ConfigureAwait(false);
                foreach (var row in ret.Rows ?? [])
                {
                    var v = row?.Values;
                    if ((v == null) || (ai >= v.Length) || (ii >= v.Length) || (v[ii] == null))
                        continue;
                    long uid;
                    try
                    {
                        uid = Convert.ToInt64(v[ii], System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        continue;
                    }
                    //  The actions that fit the user's state (DbUserAdminRow.Actions): Force password reset only when it can be done (a password, no reset pending,
                    //  a way back in), Undo forced reset when one is pending, Disable / Enable by state
                    var a = await GetUserAccess(c, uid).ConfigureAwait(false);
                    v[ai] = a == null ? "" : UserActions(uid, a.User.UserName, a);
                }
            }
            return ret;
        }

        /// <summary>
        /// The pending invitations of this site: account creation links that were sent and have not been used or expired yet.
        /// Each row has a "Withdraw invitation" action (<see cref="CancelInvitation"/>). Self sign ups in progress are listed too, with "Self sign-up" as "Invited by".
        /// </summary>
        /// <param name="r">Table request</param>
        /// <returns>The pending invitations, the ones that expire last first</returns>
        /// <remarks>Shown on the Users page (auth/Users.html, "Pending invitations").</remarks>
        [WebApi]
        [WebApiAuth(UserManagerRoles.InviteUser)]
        public async Task<TableData> InvitationTable(TableDataRequest r)
        {
            var list = await GetPendingInvitations().ConfigureAwait(false);
            return TableDataTools.Get(r, TableRefresh, list.Select(x => x.Item2));
        }

        /// <summary>
        /// Cancel a pending invitation (or self sign up): the link (and code) in the message stops working.
        /// </summary>
        /// <param name="token">The cancel token of the invitation (from the "Actions" column of <see cref="InvitationTable"/>, it can't be guessed,
        /// so a crafted link can't cancel an invitation)</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True when the invitation was withdrawn (it never returns false: an invitation that is not found is a 409 with the reason, so the client can show why nothing was done)</returns>
        /// <exception cref="HttpResponseException">400: the token is missing; 409: the invitation was not found (it was already used, has expired or was withdrawn)</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.InviteUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> CancelInvitation(String token, HttpServerRequest context)
        {
            if (String.IsNullOrEmpty(token) || (token.Length > 64))
                throw new HttpResponseException(400, "An invitation token is required");
            var a = System.Text.Encoding.ASCII.GetBytes(token);
            foreach (var x in await GetPendingInvitations().ConfigureAwait(false))
            {
                var b = System.Text.Encoding.ASCII.GetBytes(x.Item2.CancelToken);
                if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b))
                    continue;
                using (var c = await Db.GetAsync().ConfigureAwait(false))
                    await DeleteAction(c, x.Item1, DbAction.CreateUser, null).ConfigureAwait(false);
                var by = context?.Session?.Auth?.Username ?? "?";
                Manager.AddMessage(MessageLevels.Info, $"The invitation of \"{x.Item2.Target}\" was cancelled by \"{by}\"");
                return true;
            }
            throw HttpResponseException.Refused("The invitation could not be withdrawn: it was already used, has expired or was withdrawn");
        }

        /// <summary>
        /// The "Invited by" value of a pending self sign up (<see cref="InvitationTable"/>)
        /// </summary>
        public const String SelfSignUpLabel = "Self sign-up";

        /// <summary>
        /// The pending (not expired) account creation actions (invitations and self sign ups): the action token and the row
        /// </summary>
        async Task<List<Tuple<String, PendingInvitation>>> GetPendingInvitations()
        {
            var now = DateTime.UtcNow;
            List<DbAction> actions;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                actions = (await c.SelectAsync<DbAction>(x => x.Type == DbAction.CreateUser && x.Expiration > now).ConfigureAwait(false)).ToList();
            var res = new List<Tuple<String, PendingInvitation>>(actions.Count);
            foreach (var x in actions.OrderByDescending(x => x.Expiration))
            {
                NewUserData d;
                try
                {
                    d = Data.FromData<NewUserData>(x.Data);
                }
                catch
                {
                    continue;
                }
                if (d == null)
                    continue;
                var target = d.Email ?? d.Phone;
                //  A self sign up (an account creation link the visitor asked for) is listed and can be withdrawn like an invitation, but labelled
                //  "Self sign-up" in "Invited by" (S7a N57: it showed an empty inviter). Known by its token: created for the subject "signup:<target>"
                //  (an invitation's is "invite:<target>", see GetNewUserToken), so an old invitation without a recorded inviter stays empty
                var isSignUp = (target != null) && SysWeaver.Security.SubjectTokens.HasTag(x.Token, DbAction.CreateUser, TargetSubject("signup", target));
                var un = d.UserName;
                res.Add(Tuple.Create(x.Token, new PendingInvitation
                {
                    Target = target,
                    UserName = String.Equals(un, target, StringComparison.OrdinalIgnoreCase) ? null : un,
                    Tokens = d.Tokens == null ? null : String.Join(", ", d.Tokens),
                    InvitedBy = isSignUp ? SelfSignUpLabel : d.InvitedBy,
                    Expires = x.Expiration,
                    CancelToken = RowMac("invite|" + x.Token),
                }));
            }
            return res;
        }


        #endregion//Public privileged API

    }

}
