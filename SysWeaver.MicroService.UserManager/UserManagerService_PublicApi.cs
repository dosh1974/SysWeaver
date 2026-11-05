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
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    public sealed partial class UserManagerService
    {
        public const String AuditGroup = "UserManager";

        #region Public API

        /// <summary>
        /// Get information about a potential new user from a token, (will decrease the short code use counter if the token is a short code)
        /// </summary>
        /// <param name="token">The token as sent in the mail</param>
        /// <param name="context"></param>
        /// <returns>New user data or null if it's an invalid token</returns>
        [WebApi]
        public async Task<NewUserData> GetNewUserData(String token, HttpServerRequest context)
            => (await GetAction<NewUserData>(token, DbAction.CreateUser, context).ConfigureAwait(false))?.Item2;

        /// <summary>
        /// Check if a short code is valid (will decrease the short code use counter)
        /// </summary>
        /// <param name="code">The short code to test</param>
        /// <param name="context"></param>
        /// <returns>1 if ok, 0 if wrong, -1 if non-existent</returns>
        [WebApi]
        public int ValidateShortCode(String code, HttpServerRequest context)
        {
            var s = context.Session;
            if (!s.TryGet<ShortData>(ShortCodeName, out var shortData))
                return -1;
            return TrySessionCode(s, shortData, CleanUpShortCode(code));
        }


        /// <summary>
        /// Check a password reset code (sent by <see cref="ForgotPassword"/>) in any browser: the code belongs to the account, not to the browser that asked.
        /// A right code becomes the short code of this session, so the reset page (ResetPassword.html?&lt;code&gt;) works as for a code sent to this session.
        /// Wrong tries count per account (<see cref="UserManagerParams.CodeTriesPerUser"/>), from all browsers together.
        /// The answer tells nothing about who has an account: an unknown user id answers like an account with a wrong code (0, and -2 after as many tries).
        /// </summary>
        /// <param name="request">The user id (null or empty: the one this session asked a reset for) and the code</param>
        /// <param name="context"></param>
        /// <returns>1 if ok, 0 if wrong (or expired), -2 if too many wrong codes were tried for the user id (wait and try again)</returns>
        /// <exception cref="HttpResponseException">400: no user id</exception>
        [WebApi]
        public async Task<int> ValidateResetCode(ResetCodeRequest request, HttpServerRequest context)
        {
            var s = context.Session;
            var identifier = request?.UserId?.Trim();
            if (String.IsNullOrEmpty(identifier))
                s.TryGet<String>(ResetIdName, out identifier);
            if (String.IsNullOrEmpty(identifier))
                throw new HttpResponseException(400, "Enter a user id: your email address, phone number or user name");
            var code = CleanUpShortCode(request?.Code);
            long id;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                id = await FindUser(c, identifier).ConfigureAwait(false);
            if (id == 0)
                return TryUnknownCode(identifier);
            String token = null;
            DateTime expiration = default;
            var right = (code != null) && FindResetCode(id, code, out token, out expiration);
            var r = TryUserCode(id, right);
            if (r > 0)
                s.Set(ShortCodeName, new ShortData(code, token, expiration, DbAction.ResetPassword, id));
            return r;
        }


        /// <summary>
        /// The methods that can be used for sign up (sign up is still only available if the SignUp service is loaded)
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public String[] SignUpMethods() => InternalSignUpMethods;


        /// <summary>
        /// Get information about the user manager configuration
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public UserManagerProps UserManagerProps() => ManProps;


        #region User

        /// <summary>
        /// Check if the currently logged in user is managed by the user manager
        /// </summary>
        /// <param name="context"></param>
        /// <returns>True if the currently logged in user is managed by the user manager</returns>
        [WebApi]
        [WebApiAuth]
        public bool IsManagedUser(HttpServerRequest context)
            => context.Session.Auth.AuthContext is DbUser;




        /// <summary>
        /// Send an message to all the currently logged in users communication methods that enabled the user to delete account
        /// </summary>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<String[]> SendDeleteUserRequest(HttpServerRequest context)
        {
            var user = context.Session.Auth.AuthContext as DbUser;
            if (user == null)
                throw new NoUserLoggedInException();
            var id = user.Id;
            List<String> targets;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                targets = await GetUserComTargets(c, id).ConfigureAwait(false);
            if (targets.Count <= 0)
                throw new UserHaveNoComs();
            var now = DateTime.UtcNow;
            var tokens = await AddAction(id, DbAction.DeleteUser, UserSubject(id), now + DeleteUserTimeout, context, now + DeleteUserShortTimeout).ConfigureAwait(false);
            //  The same form as the other links ("?<token>"); the page still reads the older "?token=<token>" links
            String link = String.Concat(LinkRoot(context), "auth/DeleteAccount.html?",
                HttpUtility.UrlEncode(tokens.Item1));

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeleteUser, lang, targets, (ma, coms) =>
            {
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Phone]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);

            return sentTo;
        }

        /// <summary>
        /// Delete's a user, using the action token received in a message
        /// </summary>
        /// <param name="token">The delete user request token, sent in the message</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>The user name of the deleted user or null if it fails</returns>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<String> DeleteUser(String token, HttpServerRequest context)
        {
            var dataD = await GetAction<long>(token, DbAction.DeleteUser, context).ConfigureAwait(false);
            if (dataD == null)
                throw Refused(token);
            token = dataD.Item1;
            var id = dataD.Item2;
            if (id == 0)
                throw new TokenExpiredException();
            String name;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false);
                if (user == null)
                    throw new UserNoLongerExistException(id);
                await ThrowIfDisabled(c, id).ConfigureAwait(false);
                var userGuid = MakeGuid(id);
                name = user.UserName;
                await c.DeleteAllAsync<DbUser>(x => x.Id == id).ConfigureAwait(false);
                await c.DeleteAllAsync<DbEmailAddress>(x => x.UserId == id).ConfigureAwait(false);
                await c.DeleteAllAsync<DbPhoneNumber>(x => x.UserId == id).ConfigureAwait(false);
                await c.DeleteAllAsync<DbAuthPassKey>(x => x.UserId == id).ConfigureAwait(false);
                await c.DeleteAllAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                await c.DeleteAllAsync<DbToken>(x => x.UserId == id).ConfigureAwait(false);
                await c.DeleteAllAsync<DbUserData>(x => x.UserGuid == userGuid).ConfigureAwait(false);
                await c.DeleteAllAsync<DbUserDataString>(x => x.UserGuid == userGuid).ConfigureAwait(false);
                await DeleteAction(c, token, DbAction.DeleteUser, context).ConfigureAwait(false);
                //  Every other link of the user stops working
                await RevokeOnEvent(c, id, AccountEvents.AccountDeleted).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            //  Audit (the code came from a message to the user, the session may belong to someone else or to no one)
            var caller = context.Session?.Auth?.Username;
            var auditText = (caller == null) || String.Equals(caller, name, StringComparison.Ordinal)
                ? $"User \"{name}\" (#{id}) deleted their account (confirmed with the code sent to them)"
                : $"User \"{name}\" (#{id}) was deleted with the code sent to them, from the session of \"{caller}\"";
            Manager.AddMessage(auditText, MessageLevels.Info);
            context.Server.PushMessageUser(MakeGuid(id), new PushMessage("reload"));
            context.Server.CloseSession(context.Session);
            await RaiseOnUserDeleted(id, name).ConfigureAwait(false);
            return name;
        }

        /// <summary>
        /// Add a new user (and optionally password), using the action token received in an email
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>Response</returns>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<UserLoginResponse> AddUser(AddUserRequest r, HttpServerRequest context)
        {
            var auth = await CreateUser(r, context, true).ConfigureAwait(false);
            return new UserLoginResponse
            {
                Username = auth.Username,
                Tokens = auth.Tokens?.ToArray(),
            };
        }

        #endregion//User

        #region Password

        /// <summary>
        /// Set a new password for the currently logged in user
        /// </summary>
        /// <param name="request">Parameters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True if password was changed</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> SetPassword(SetPasswordRequest request, HttpServerRequest context)
        {
            var user = ValidatePasswordRequestData(request, context);
            if (user == null)
                return false;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var id = user.Id;
            var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
            if (!ValidatePasswordRequestPassword(request, authData))
                return false;
            authData.Pwd = request.NewHash;
            authData.MustResetPassword = false;
            await c.UpdateAsync(authData, x => new { x.Pwd, x.MustResetPassword }).ConfigureAwait(false);
            //  A password change revokes every outstanding link of the user whose meaning depends on the old state (see ActionTokenGroups)
            await RevokeOnEvent(c, id, AccountEvents.PasswordChanged).ConfigureAwait(false);
            tr.Commit();
            Manager.AddMessage(MessageLevels.Info, $"User \"{user.UserName}\" (#{id}) changed their password");
            return true;
        }


        /// <summary>
        /// Send the reset instructions (link and short code) to the user, see <see cref="Send"/>).
        /// Anyone can call this, so the answer tells nothing about who has an account: it is the same for an unknown, a disabled and an
        /// existing account (and one with no e-mail address or phone number), in what it returns, in its time (the account is looked up
        /// and the message sent in the background) and in the session (no short code is pending in it: the code belongs to the account and
        /// is checked with <see cref="ValidateResetCode"/>, in any browser; the session only remembers the user id for it).
        /// An account gets reset messages with a growing pause (<see cref="ResetPasswordBackoff"/>): asking sooner sends nothing, the codes sent
        /// before keep working.
        /// Why nothing was sent is only logged.
        /// </summary>
        /// <param name="identifier">User identification (user name, e-mail address or phone number)</param>
        /// <param name="context"></param>
        /// <returns>The identifier (trimmed), never the e-mail addresses and phone numbers of the account: show it as "if an account uses ..."</returns>
        /// <exception cref="HttpResponseException">400: no identifier</exception>
        [WebApi]
        public Task<String[]> ForgotPassword(String identifier, HttpServerRequest context)
        {
            identifier = identifier?.Trim();
            if (String.IsNullOrEmpty(identifier))
                throw new HttpResponseException(400, "Enter a user id: your email address, phone number or user name");
            var session = context.Session;
            //  The code step of this browser checks the codes of this user id (ValidateResetCode); a code pending in the session
            //  (ex: an open sign up code step) is replaced, the same for every user id
            session.Set(ResetIdName, identifier);
            session.TryRemove(ShortCodeName);
            //  One address can't keep the queue full (so that nobody else gets a message) or run up the text message costs
            if (!TryClaimResetPasswordIp(context.GetIpAddress() ?? "", DateTime.UtcNow))
            {
                Manager.AddMessage(MessageLevels.Debug, $"Too many password reset requests from the same address, nothing sent for the user id {MaskUserId(identifier)}");
                return Task.FromResult<String[]>([identifier]);
            }
            //  A flood of requests can't take all the database connections: the work waits for a free slot, too much work is dropped
            if (Interlocked.Increment(ref ResetPasswordQueued) > ResetPasswordMaxQueued)
            {
                Interlocked.Decrement(ref ResetPasswordQueued);
                Manager.AddMessage(MessageLevels.Warning, $"Too many password reset requests at once, nothing sent for the user id {MaskUserId(identifier)}");
                return Task.FromResult<String[]>([identifier]);
            }
            var vars = GetMessageParams(context);
            var prefix = LinkRoot(context);
            var language = session.Language;
            var stop = ResetPasswordStop.Token;
            _ = Task.Run(async () =>
            {
                //  The service is stopping: the waiting work ends quietly
                try
                {
                    await ResetPasswordSlots.WaitAsync(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Decrement(ref ResetPasswordQueued);
                    return;
                }
                try
                {
                    if (!stop.IsCancellationRequested)
                        await SendResetPassword(identifier, false, true, null, prefix, vars, language).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is UserDoNotExistException or UserDisabledException or UserHaveNoComs)
                {
                    Manager.AddMessage(MessageLevels.Debug, $"No password reset instructions sent for the user id {MaskUserId(identifier)}: {ex.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Manager.AddMessage(MessageLevels.Warning, $"Failed to send the password reset instructions for the user id {MaskUserId(identifier)}", ex);
                }
                finally
                {
                    ResetPasswordSlots.Release();
                    Interlocked.Decrement(ref ResetPasswordQueued);
                }
            });
            return Task.FromResult<String[]>([identifier]);
        }

        /// <summary>
        /// The pause after the first reset message (<see cref="ForgotPassword"/>) to an account; it doubles with each message sent
        /// within <see cref="ResetPasswordBackoffMax"/> of the one before (60 s, 2 min, 4 min .. 1 h), so nobody can flood an account
        /// </summary>
        readonly TimeSpan ResetPasswordBackoff = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The longest pause between two reset messages to an account (and how long it is remembered)
        /// </summary>
        readonly TimeSpan ResetPasswordBackoffMax = TimeSpan.FromHours(1);

        /// <summary>
        /// When the last reset message was sent and the pause before the next one, per user id (lock it)
        /// </summary>
        readonly Dictionary<long, (DateTime Last, TimeSpan Pause)> ResetPasswordSent = new Dictionary<long, (DateTime, TimeSpan)>();

        /// <summary>
        /// At most this many password reset lookups / sends run at once (the others wait)
        /// </summary>
        readonly SemaphoreSlim ResetPasswordSlots = new SemaphoreSlim(4);

        /// <summary>
        /// The number of password resets running or waiting
        /// </summary>
        int ResetPasswordQueued;

        /// <summary>
        /// More password resets running or waiting than this are dropped (logged)
        /// </summary>
        const int ResetPasswordMaxQueued = 200;

        /// <summary>
        /// Stops the password reset work that is waiting when the service is disposed
        /// </summary>
        readonly CancellationTokenSource ResetPasswordStop = new CancellationTokenSource();

        /// <summary>
        /// At most this many password reset requests per <see cref="ResetPasswordIpWindow"/> from one ip address (more are answered the same, nothing is done)
        /// </summary>
        const int ResetPasswordMaxPerIp = 20;

        /// <summary>
        /// The time window of <see cref="ResetPasswordMaxPerIp"/>
        /// </summary>
        readonly TimeSpan ResetPasswordIpWindow = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The start of the current window and the number of requests in it, per ip address (lock it)
        /// </summary>
        readonly Dictionary<String, (DateTime Start, int Count)> ResetPasswordIps = new Dictionary<String, (DateTime, int)>(StringComparer.Ordinal);

        /// <summary>
        /// Count a password reset request from an ip address (atomic)
        /// </summary>
        /// <param name="ip">The ip address</param>
        /// <param name="now">The time (UTC)</param>
        /// <returns>True if the request may be handled, false if the address made too many requests</returns>
        bool TryClaimResetPasswordIp(String ip, DateTime now)
        {
            lock (ResetPasswordIps)
            {
                //  A window in the future (the clock went back) is over too
                if (!ResetPasswordIps.TryGetValue(ip, out var w) || ((now - w.Start) >= ResetPasswordIpWindow) || (now < w.Start))
                    w = (now, 0);
                if (w.Count >= ResetPasswordMaxPerIp)
                    return false;
                ResetPasswordIps[ip] = (w.Start, w.Count + 1);
                return true;
            }
        }

        /// <summary>
        /// Claim the right to send a reset message to an account now (atomic)
        /// </summary>
        /// <param name="id">The user id</param>
        /// <param name="now">The time (UTC)</param>
        /// <param name="previous">The state before the claim (give it to <see cref="ReleaseResetPassword"/> if nothing could be sent), null if none</param>
        /// <returns>True if a message may be sent (the pause is over), else false</returns>
        bool TryClaimResetPassword(long id, DateTime now, out (DateTime Last, TimeSpan Pause)? previous)
        {
            lock (ResetPasswordSent)
            {
                previous = null;
                var pause = ResetPasswordBackoff;
                if (ResetPasswordSent.TryGetValue(id, out var last))
                {
                    previous = last;
                    var since = now - last.Last;
                    //  A last time in the future (the clock went back) is no pause
                    if ((since >= TimeSpan.Zero) && (since < last.Pause))
                        return false;
                    if ((since >= TimeSpan.Zero) && (since < ResetPasswordBackoffMax))
                        pause = TimeSpan.FromTicks(Math.Min(last.Pause.Ticks * 2, ResetPasswordBackoffMax.Ticks));
                }
                ResetPasswordSent[id] = (now, pause);
                return true;
            }
        }

        /// <summary>
        /// Undo a claim (<see cref="TryClaimResetPassword"/>) when nothing could be sent, so that asking again works (unless a newer claim was made)
        /// </summary>
        /// <param name="id">The user id</param>
        /// <param name="now">The time of the claim</param>
        /// <param name="previous">The state before the claim</param>
        void ReleaseResetPassword(long id, DateTime now, (DateTime Last, TimeSpan Pause)? previous)
        {
            lock (ResetPasswordSent)
            {
                if (!(ResetPasswordSent.TryGetValue(id, out var cur) && (cur.Last == now)))
                    return;
                if (previous == null)
                    ResetPasswordSent.Remove(id);
                else
                    ResetPasswordSent[id] = previous.Value;
            }
        }

        /// <summary>
        /// Forget the reset message pauses and ip address windows that are over (called by Prune)
        /// </summary>
        void PruneResetPasswordSent()
        {
            var now = DateTime.UtcNow;
            var max = ResetPasswordBackoffMax;
            lock (ResetPasswordSent)
            {
                List<long> old = null;
                foreach (var x in ResetPasswordSent)
                    if (((now - x.Value.Last) >= max) || (now < x.Value.Last))
                        (old ??= new List<long>()).Add(x.Key);
                if (old != null)
                    foreach (var x in old)
                        ResetPasswordSent.Remove(x);
            }
            var window = ResetPasswordIpWindow;
            lock (ResetPasswordIps)
            {
                List<String> old = null;
                foreach (var x in ResetPasswordIps)
                    if (((now - x.Value.Start) >= window) || (now < x.Value.Start))
                        (old ??= new List<String>()).Add(x.Key);
                if (old != null)
                    foreach (var x in old)
                        ResetPasswordIps.Remove(x);
            }
        }

        /// <summary>
        /// A user id for the log, without the whole e-mail address or phone number (ex: "+46…44", short ids: "…")
        /// </summary>
        static String MaskUserId(String identifier)
            => identifier.Length < 10 ? "\"…\"" : String.Concat("\"", identifier[..3], "…", identifier[^2..], "\"");

        /// <summary>
        /// Send the reset instructions an administrator required (<see cref="StartPasswordChange"/>), now (no pause, the errors are thrown).
        /// </summary>
        /// <param name="identifier">User identification</param>
        /// <param name="context">The request</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        Task<String[]> InternalStartPasswordChange(String identifier, HttpServerRequest context)
            => SendResetPassword(identifier?.Trim(), true, false, context?.Session, LinkRoot(context), GetMessageParams(context), context?.Session?.Language);

        /// <summary>
        /// Send the reset instructions (link and short code) to the user (see <see cref="Send"/>).
        /// Uses no request (it may be over when this runs in the background).
        /// </summary>
        /// <param name="identifier">User identification (trimmed)</param>
        /// <param name="forced">True when an administrator required the new password (<see cref="StartPasswordChange"/>): the message says so
        /// (<see cref="UserManagerComOps.ForcedResetPassword"/>) instead of "you asked for a reset";
        /// false for <see cref="ForgotPassword"/> and <see cref="UndoEmailChange"/></param>
        /// <param name="pause">True for <see cref="ForgotPassword"/>: an account gets a message only when its pause is over (see <see cref="TryClaimResetPassword"/>)</param>
        /// <param name="session">The session that also gets the short code (the user's own sign in session), null for none; the code always
        /// belongs to the account too (<see cref="ValidateResetCode"/>)</param>
        /// <param name="prefix">The url prefix of the links</param>
        /// <param name="baseVars">The message variables of the request (<see cref="GetMessageParams(HttpServerRequest, string[])"/>), not changed</param>
        /// <param name="language">The language to use if the user has none</param>
        /// <returns>An array of communication targets that instructions was sent to (empty when the pause of the account is not over)</returns>
        async Task<String[]> SendResetPassword(String identifier, bool forced, bool pause, HttpSession session, String prefix, Dictionary<String, String> baseVars,
            String language)
        {
            List<String> targets;
            long id;
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                id = await FindUser(c, identifier).ConfigureAwait(false);
                if (id == 0)
                    throw new UserDoNotExistException(identifier);
                user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false);
                if (user == null)
                    throw new UserDoNotExistException(identifier);
                //  A disabled account gets no reset message (it can't sign in in any way until it is enabled again); ForgotPassword
                //  answers the same as for an unknown user id (the administrator's StartPasswordChange is told)
                await ThrowIfDisabled(c, id).ConfigureAwait(false);
                targets = await GetUserComTargets(c, id).ConfigureAwait(false);
            }
            if (targets.Count <= 0)
                throw new UserHaveNoComs();
            var now = DateTime.UtcNow;
            //  The pause is not over: nothing is sent (the codes sent before keep working)
            (DateTime Last, TimeSpan Pause)? previous = null;
            if (pause && !TryClaimResetPassword(id, now, out previous))
            {
                Manager.AddMessage(MessageLevels.Debug, $"No password reset instructions sent for the user id {MaskUserId(identifier)}: the pause of the account is not over");
                return [];
            }
            try
            {
                var salt = AuthTools.GetRandomSalt();
                var tokens = await AddAction(new InternalNewPasswordData
                {
                    NickName = user.NickName,
                    UserName = user.UserName,
                    UserId = id,
                    Salt = salt,
                }, DbAction.ResetPassword, UserSubject(id), now + ResetPasswordTimeout).ConfigureAwait(false);
                var shortToken = tokens.Item1.Replace(' ', '+');
                var shortExpiration = now + ResetPasswordShortTimeout;
                AddResetCode(id, tokens.Item2, shortToken, shortExpiration);
                session?.Set(ShortCodeName, new ShortData(tokens.Item2, shortToken, shortExpiration, DbAction.ResetPassword, id));

                var userName = user.NickName ?? user.UserName;
                var lang = await GetLang(user.Language ?? language).ConfigureAwait(false);
                String link = String.Concat(prefix, "auth/ResetPassword.html?", HttpUtility.UrlEncode(tokens.Item1));
                var sentTo = await Send(forced ? UserManagerComOps.ForcedResetPassword : UserManagerComOps.ResetPassword, lang, targets, (ma, coms) =>
                    new Dictionary<String, String>(baseVars, StringComparer.Ordinal)
                    {
                        ["[UserName]"] = userName,
                        ["[Phone]"] = ma,
                        ["[ShortCode]"] = FormatShortCode(tokens.Item2),
                        ["[Link]"] = link,
                    }).ConfigureAwait(false);
                if (pause && (sentTo.Length <= 0))
                    ReleaseResetPassword(id, now, previous);
                return sentTo;
            }
            catch
            {
                //  Nothing was sent: asking again sends at once
                if (pause)
                    ReleaseResetPassword(id, now, previous);
                throw;
            }
        }

        /// <summary>
        /// Set a new password for a user, using the action token received in an communication
        /// </summary>
        /// <param name="login">Parameters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True if password was changed</returns>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<UserLoginResponse> ResetPassword(AddUserRequest login, HttpServerRequest context)
        {
            var s = context?.Session;
            if (s == null)
                throw new NoSessionException();
            var token = login.Token;
            //  The type of the matched action, it must be deleted
            var actionType = DbAction.ResetPassword;
            var dataD = await GetAction<InternalNewPasswordData>(token, actionType, context).ConfigureAwait(false);
            if (dataD == null)
            {
                actionType = DbAction.AddPassword;
                dataD = await GetAction<InternalNewPasswordData>(token, actionType, context).ConfigureAwait(false);
                if (dataD == null)
                    throw Refused(token);
            }
            token = dataD.Item1;
            var npData = dataD.Item2;
            if (npData == null)
                throw new TokenExpiredException();
            var id = npData.UserId;
            var salt = npData.Salt;
            if (String.IsNullOrEmpty(salt) || (salt.Length < 16))
                throw new TokenExpiredException();
            Authorization auth;
            //  What happened, for the audit log line
            String how;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                //  A disabled account: its links don't work until it is enabled again (nothing is changed)
                await ThrowIfDisabled(c, id).ConfigureAwait(false);
                var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                var isNew = authData == null;
                //  An administrator required a new password: the current one may not be chosen again (the client sends the hash of the
                //  new password made with the current salt, NewPasswordData.CurrentSalt; older clients send none and are not checked)
                if (!isNew && authData.MustResetPassword && IsUsablePassword(authData) && IsSameStoredHash(authData.Pwd, login.CurrentHash))
                    return new UserLoginResponse { Error = UserErrors.SamePassword };
                how = isNew ? "added a password" : (authData.MustResetPassword ? "chose a new password after a forced reset" : "reset their password with the code sent to them");
                var now = DateTime.UtcNow;
                if (isNew)
                    authData = new DbAuthPassword
                    {
                        Created = now,
                        UserId = id,
                    };
                authData.Salt = salt;
                authData.Pwd = login.NewHash;
                authData.MustResetPassword = false;
                authData.LastUsed = now;
                if (isNew)
                    await c.InsertAsync(authData).ConfigureAwait(false);
                else
                    await c.UpdateAsync(authData, x => new { x.Salt, x.Pwd, x.MustResetPassword, x.LastUsed }).ConfigureAwait(false);
                auth = await InternalAuthUser(c, id).ConfigureAwait(false);
                if (auth == null)
                    throw await NoAuthException(c, id).ConfigureAwait(false);
                await DeleteAction(c, token, actionType, context).ConfigureAwait(false);
                //  The password changed: the other (older) links of the user stop working (see ActionTokenGroups)
                await RevokeOnEvent(c, id, AccountEvents.PasswordChanged).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            Manager.AddMessage(MessageLevels.Info, $"User \"{auth.Username}\" (#{id}) {how}");
            var ret = new UserLoginResponse
            {
                Username = auth.Username,
                Tokens = auth.Tokens?.ToArray(),
            };
            if (s.Auth != null)
                s.Auth.RequestLogout("Password change");
            s.SetAuth(auth);
            await context.Server.RunOnLogin(s, context).ConfigureAwait(false);
            return ret;
        }

        /// <summary>
        /// Get information about a potential new user from a token
        /// </summary>
        /// <param name="token">The token as sent in the email</param>
        /// <param name="context"></param>
        /// <returns>New user data</returns>
        /// <exception cref="TokenExpiredException">The token can't be used (used, replaced, expired or unknown): 410 (a short code: 400) with the reason</exception>
        [WebApi]
        public async Task<NewPasswordData> GetNewPasswordData(String token, HttpServerRequest context)
        {
            var dataD = await GetAction<InternalNewPasswordData>(token, DbAction.ResetPassword, context).ConfigureAwait(false);
            if (dataD == null)
            {
                dataD = await GetAction<InternalNewPasswordData>(token, DbAction.AddPassword, context).ConfigureAwait(false);
                if (dataD == null)
                    throw Refused(token);
            }
            token = dataD.Item1;
            var t = dataD.Item2;
            var res = new NewPasswordData
            {
                NickName = t.NickName,
                Salt = t.Salt,
                UserName = t.UserName,
            };
            //  An administrator required the new password: say so, and let the client prove that the new password differs (CurrentSalt)
            if (t.UserId > 0)
            {
                var uid = t.UserId;
                using var c = await Db.GetAsync().ConfigureAwait(false);
                var pwd = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == uid).ConfigureAwait(false);
                if ((pwd != null) && pwd.MustResetPassword && IsUsablePassword(pwd))
                {
                    res.Forced = true;
                    res.CurrentSalt = pwd.Salt;
                }
            }
            return res;
        }

        /// <summary>
        /// True if a hash sent by a client (base64 text) equals a stored password hash (constant time).
        /// </summary>
        /// <param name="stored">The stored hash</param>
        /// <param name="sent">The sent hash, null or empty never matches</param>
        /// <returns>True if they are the same</returns>
        static bool IsSameStoredHash(String stored, String sent)
        {
            if (String.IsNullOrEmpty(stored) || String.IsNullOrEmpty(sent))
                return false;
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(stored), System.Text.Encoding.UTF8.GetBytes(sent));
        }

        /// <summary>
        /// Send a message to all the currently logged in users communication methods that enabled the user to add a password
        /// </summary>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<String[]> SendAddPasswordRequest(HttpServerRequest context)
        {
            var user = context.Session.Auth.AuthContext as DbUser;
            if (user == null)
                throw new NoUserLoggedInException();
            var id = user.Id;
            List<String> targets;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                targets = await GetUserComTargets(c, id).ConfigureAwait(false);
                if (targets.Count <= 0)
                    throw new UserHaveNoComs();
                //  A row without a hash (made by GetSaltAsync when anyone asked for the salt) is no password
                if (IsUsablePassword(await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false)))
                    throw new UserAlreadyHavePassword();
            }
            var salt = AuthTools.GetRandomSalt();
            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewPasswordData
            {
                NickName = user.NickName,
                UserName = user.UserName,
                UserId = id,
                Salt = salt,
            }, DbAction.AddPassword, UserSubject(id), now + AddPasswordTimeout, context, now + AddPasswordShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.AddPassword, lang, targets, (ma, coms) =>
            {
                String link = String.Concat(LinkRoot(context), "auth/AddPassword.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Phone]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);

            return sentTo;
        }

        /// <summary>
        /// Send a message to all the currently logged in users communication methods that enabled the user to delete a password
        /// </summary>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<String[]> SendDeletePasswordRequest(HttpServerRequest context)
        {
            var user = context.Session.Auth.AuthContext as DbUser;
            if (user == null)
                throw new NoUserLoggedInException();
            var id = user.Id;
            List<String> targets;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                targets = await GetUserComTargets(c, id).ConfigureAwait(false);
                if (targets.Count <= 0)
                    throw new UserHaveNoComs();
                if (!IsUsablePassword(await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false)))
                    throw new UserDontHavePassword();
            }
            var salt = AuthTools.GetRandomSalt();
            var now = DateTime.UtcNow;
            var tokens = await AddAction(id, DbAction.DeletePassword, UserSubject(id), now + DelPasswordTimeout, context, now + DelPasswordShortTimeout).ConfigureAwait(false);

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeletePassword, lang, targets, (ma, coms) =>
            {
                String link = String.Concat(LinkRoot(context), "auth/DeletePassword.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Phone]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);

            return sentTo;
        }

        /// <summary>
        /// Delete's a password from a users account, using the action token received in an email
        /// </summary>
        /// <param name="token">The action token that was sent in the email</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True if successful</returns>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> DeletePassword(String token, HttpServerRequest context)
        {
            var dataD = await GetAction<long>(token, DbAction.DeletePassword, context).ConfigureAwait(false);
            if (dataD == null)
                throw Refused(token);
            token = dataD.Item1;
            var id = dataD.Item2;
            if (id == 0)
                throw new TokenExpiredException();
            String name;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                await ThrowIfDisabled(c, id).ConfigureAwait(false);
                name = (await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false))?.UserName ?? "?";
                await c.DeleteAllAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                await DeleteAction(c, token, DbAction.DeletePassword, context).ConfigureAwait(false);
                await RevokeOnEvent(c, id, AccountEvents.PasswordRemoved).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            Manager.AddMessage(MessageLevels.Info, $"User \"{name}\" (#{id}) removed their password");
            context.Session.InvalidateCache();
            context.Server.PushMessageUser(MakeGuid(id), new PushMessage("reload"));
            return true;
        }

        /// <summary>
        /// Get the password policy for creating, changing or setting a password the user manager
        /// </summary>
        /// <returns>The password policy</returns>
        [WebApi]
        public PasswordPolicy GetCreatePasswordPolicy() => PasswordPolicy;


        #endregion//Password

        #region Email


        /// <summary>
        /// Send a message to the specified email address with a link, that if followed will associate the email address to the logged in account
        /// </summary>
        /// <param name="newEmail">The email address to add</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public Task<String[]> SendAddEmailRequest(SetEmailRequest newEmail, HttpServerRequest context)
            => InternalAddChangeEmailRequest(newEmail, false, context);

        /// <summary>
        /// Send a message to the specified email address with a link, that if followed will change the associate email address on the logged in account to the supplied email
        /// </summary>
        /// <param name="newEmail">The email address to add</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public Task<String[]> SendChangeEmailRequest(SetEmailRequest newEmail, HttpServerRequest context)
            => InternalAddChangeEmailRequest(newEmail, true, context);


        public async Task<String[]> SendAddChangeEmailRequest(long userId, IUserCoreData user, String newEmail, String language, String origin)
        {
            var am = AuthMan;
            List<String> targets;
            String existingEmail;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                //var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
                targets = await GetUserComTargets(c, userId).ConfigureAwait(false);
                existingEmail = targets.FirstOrDefault(x => am.ValidateEmailAddress(x, false));
                if (IsSameComTarget(existingEmail, newEmail))
                    throw new EmailAlreadyAssignedException(newEmail);
                if (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == newEmail).ConfigureAwait(false) != null)
                    throw new EmailInUseException(newEmail);
            }

            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewEmailData
            {
                UserId = userId,
                Email = newEmail,
            }, DbAction.AddEmail, UserSubject(userId), now + AddEmailTimeout).ConfigureAwait(false);
            bool isChanged = existingEmail != null;
            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? language).ConfigureAwait(false);
            var sentTo = await Send(isChanged ? UserManagerComOps.ChangeEmailNoCode : UserManagerComOps.AddEmailNoCode, lang, [newEmail], (ma, coms) =>
            {
                String link = String.Concat(HttpServer?.ExternalRootUri ?? "", isChanged ? "auth/ChangeEmail.html?" : "auth/AddEmail.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(null,
                    "[UserName]", userName,
                    "[Email]", ma,
                    "[Link]", link,
                    "[Origin]", GetOriginParam(origin)
                );
            }).ConfigureAwait(false);
            return sentTo;

        }

        /// <summary>
        /// True if the existing communication target (email / phone) is the same as the new one (culture invariant, case insensitive), false if there is no existing target
        /// </summary>
        static bool IsSameComTarget(String existing, String newTarget)
            => (existing != null) && existing.FastToLower().FastEquals(newTarget?.FastToLower());

        /// <summary>
        /// The [Origin] message parameter, empty for a null origin
        /// </summary>
        static String GetOriginParam(String origin)
            => origin?.Replace("[Color]", HashColors.AppColors.Acc1) ?? "";


        async Task<String[]> InternalAddChangeEmailRequest(SetEmailRequest request, bool isChanged, HttpServerRequest context)
        {
            var user = ValidatePasswordRequestData(request, context);
            if (user == null)
                throw new AuthenticationFailedException();
            var newEmail = request.Email?.Trim();
            var am = AuthMan;
            am.ValidateEmailAddress(newEmail);
            var userId = user.Id;
            List<String> targets;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
                if (!ValidatePasswordRequestPassword(request, authData))
                    throw new AuthenticationFailedException();
                targets = await GetUserComTargets(c, userId).ConfigureAwait(false);
                var existingEmail = targets.FirstOrDefault(x => am.ValidateEmailAddress(x, false));
                if (isChanged)
                {
                    if (IsSameComTarget(existingEmail, newEmail))
                        throw new EmailAlreadyAssignedException(newEmail);
                }
                else
                {
                    if (existingEmail != null)
                        throw new UserAlreadyHaveEmail();
                }
                if (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == newEmail).ConfigureAwait(false) != null)
                    throw new EmailInUseException(newEmail);
            }

            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewEmailData
            {
                UserId = userId,
                Email = newEmail,
            }, DbAction.AddEmail, UserSubject(userId), now + AddEmailTimeout, context, now + AddEmailShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(isChanged ? UserManagerComOps.ChangeEmail : UserManagerComOps.AddEmail, lang, [newEmail], (ma, coms) =>
            {
                String link = String.Concat(LinkRoot(context), isChanged ? "auth/ChangeEmail.html?" : "auth/AddEmail.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Email]", ma,
                    "[Link]", link
                );
            }).ConfigureAwait(false);
            return sentTo;
        }

        /// <summary>
        /// Delete an email address
        /// </summary>
        /// <param name="request">Validation</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that recovery instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<String[]> DeleteEmail(AutheticatedRequest request, HttpServerRequest context)
        {
            var user = ValidatePasswordRequestData(request, context);
            if (user == null)
                throw new AuthenticationFailedException();
            var userId = user.Id;
            List<String> targets;
            String emailTarget;
            var am = AuthMan;
            if (am == null)
                throw new Exception("Email not supported!");
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
                if (!ValidatePasswordRequestPassword(request, authData))
                    throw new AuthenticationFailedException();
                targets = await GetUserComTargets(c, userId).ConfigureAwait(false);
                emailTarget = targets.FirstOrDefault(x => am.ValidateEmailAddress(x, false));
                if (emailTarget == null)
                    throw new UserDontHaveEmail();
//                if (targets.Count <= 1)
//                    throw new CantDeleteLastUserCom();
            }
            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewEmailData
            {
                UserId = userId,
                Email = emailTarget,
            }, DbAction.AddEmail, UserSubject(userId, RestoreVariant), now + AddEmailTimeout, context, now + AddEmailShortTimeout).ConfigureAwait(false);

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeletedEmail, lang, [emailTarget], (ma, coms) =>
            {
                String link = String.Concat(LinkRoot(context), "auth/AddEmail.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Email]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                await c.DeleteAllAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                //  The links that depend on the removed address stop working, except the restore link just sent to it
                await RevokeOnEvent(c, userId, AccountEvents.EmailRemoved, tokens.Item1).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            await RaiseOnEmailRemoved(userId, emailTarget).ConfigureAwait(false);
            context.Session.InvalidateCache();
            context.Server.PushMessageUser(MakeGuid(userId), new PushMessage("reload"));
            return sentTo;
        }

        /// <summary>
        /// Add/change an email address based on a token
        /// </summary>
        /// <param name="token">The action token that was sent in the email</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True if successful</returns>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> AddEmail(String token, HttpServerRequest context)
        {
            var dataD = await GetAction<InternalNewEmailData>(token, DbAction.AddEmail, context).ConfigureAwait(false);
            if (dataD == null)
                throw Refused(token);
            token = dataD.Item1;
            var data = dataD.Item2;
            var userId = data.UserId;
            var newEmail = data.Email;
            String existingEmail = null;
            //  The name and language for the notice to the old address (the name before the change: it may be the old address)
            String noticeName, noticeLanguage;
            //  The account before the change, for the undo link in the notice
            String oldUserName, oldNickName, oldPhone;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    throw new UserNoLongerExistException(userId);
                await ThrowIfDisabled(c, userId).ConfigureAwait(false);
                noticeName = user.NickName ?? user.UserName;
                noticeLanguage = user.Language;
                oldUserName = user.UserName;
                oldNickName = user.NickName;
                oldPhone = (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false))?.Phone;
                if (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == newEmail).ConfigureAwait(false) != null)
                    throw new EmailInUseException(newEmail);
                var existing = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
                //  A restore link (sent to a removed address) never replaces a newer address
                if ((existing != null) && SubjectTokens.HasTag(token, DbAction.AddEmail, UserSubject(userId, RestoreVariant)))
                {
                    await DeleteAction(c, token, DbAction.AddEmail, context).ConfigureAwait(false);
                    await tr.CommitAsync().ConfigureAwait(false);
                    MarkDead(token, CauseNewerContact);
                    throw new TokenExpiredException(TokenRefusedReason(CauseNewerContact, false, DbAction.AddEmail));
                }
                if (existing != null)
                {
                    existingEmail = existing.Email;
                    if (user.UserName.FastEquals(existingEmail))
                    {
                        user.UserName = newEmail;
                        if (user.NickName.FastEquals(existingEmail))
                        {
                            user.NickName = newEmail;
                            await c.UpdateAsync(user, x => new { x.UserName, x.NickName }).ConfigureAwait(false);
                        }
                        else
                        {
                            await c.UpdateAsync(user, x => new { x.UserName }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        if (user.NickName.FastEquals(existingEmail))
                        {
                            user.NickName = newEmail;
                            await c.UpdateAsync(user, x => new { x.NickName }).ConfigureAwait(false);
                        }
                    }
                    await c.DeleteAsync(existing).ConfigureAwait(false);
                }
                await c.InsertAsync(new DbEmailAddress
                {
                    Email = newEmail,
                    UserId = userId,
                }).ConfigureAwait(false);
                await DeleteAction(c, token, DbAction.AddEmail, context).ConfigureAwait(false);
                //  The links that depend on the old address stop working: reset / delete links sent to it, other e-mail links, the restore link
                await RevokeOnEvent(c, userId, AccountEvents.EmailChanged).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            if (existingEmail == null)
                await RaiseOnEmailAdded(userId, newEmail).ConfigureAwait(false);
            else
            {
                //  Tell the old address (the owner learns about a change made by someone who has the password): the new address
                //  is not named; the undo link gives the account back (UndoEmailChange), the reset link is the way when it no longer works.
                //  The links use the site's own root (LinkRoot): the caller may be the one who took the account (Host header).
                //  Sent before the event, so that a failing event handler can't skip it
                try
                {
                    var lang = await GetLang(noticeLanguage ?? context?.Session?.Language).ConfigureAwait(false);
                    var root = LinkRoot(context);
                    var undo = await AddUndoEmail(new InternalUndoEmailData
                    {
                        UserId = userId,
                        OldEmail = existingEmail,
                        NewEmail = newEmail,
                        OldUserName = oldUserName,
                        OldNickName = oldNickName,
                        OldPhone = oldPhone,
                        Changed = DateTime.UtcNow,
                    }).ConfigureAwait(false);
                    await Send(UserManagerComOps.ChangedEmail, lang, [existingEmail], (ma, coms) => GetMessageParams(context,
                        "[UserName]", noticeName,
                        "[Email]", ma,
                        "[Root]", root,
                        "[Link]", String.Concat(root, "auth/ForgotPassword.html"),
                        "[UndoLink]", String.Concat(root, "auth/UndoEmail.html?", HttpUtility.UrlEncode(undo))
                    )).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    //  The change is done: a failed notice doesn't fail it
                    Manager.AddMessage(MessageLevels.Warning, $"Failed to send the changed email notice of user #{userId} to the old address", ex);
                }
                await RaiseOnEmailChanged(userId, newEmail, existingEmail).ConfigureAwait(false);
            }
            context.Session.InvalidateCache();
            context.Server.PushMessageUser(MakeGuid(userId), new PushMessage("reload"));
            return true;
        }


        #endregion//Email

        #region Phone


        /// <summary>
        /// Send a message to the specified phone number with a link, that if followed will associate the phone number to the logged in account
        /// </summary>
        /// <param name="newPhone">The phone number to add</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public Task<String[]> SendAddPhoneRequest(SetPhoneRequest newPhone, HttpServerRequest context)
            => InternalAddChangePhoneRequest(newPhone, false, context);

        /// <summary>
        /// Send a message to the specified phone number with a link, that if followed will change the associate phone number on the logged in account to the supplied phone
        /// </summary>
        /// <param name="newPhone">The phone number to add</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        public Task<String[]> SendChangePhoneRequest(SetPhoneRequest newPhone, HttpServerRequest context)
            => InternalAddChangePhoneRequest(newPhone, true, context);


        async Task<String[]> InternalAddChangePhoneRequest(SetPhoneRequest request, bool isChanged, HttpServerRequest context)
        {
            var user = ValidatePasswordRequestData(request, context);
            if (user == null)
                throw new AuthenticationFailedException();
            var newPhone = request.Phone?.Trim();
            var am = AuthMan;
            am.ValidatePhoneNumber(ref newPhone);
            var id = user.Id;
            List<String> targets;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                if (!ValidatePasswordRequestPassword(request, authData))
                    throw new AuthenticationFailedException();
                targets = await GetUserComTargets(c, id).ConfigureAwait(false);
                var existingPhone = targets.FirstOrDefault(x => am.ValidatePhoneNumber(ref x, false));
                if (isChanged)
                {
                    if (IsSameComTarget(existingPhone, newPhone))
                        throw new PhoneAlreadyAssignedException(newPhone);
                }
                else
                {
                    if (existingPhone != null)
                        throw new UserAlreadyHavePhone();
                }
                if (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == newPhone).ConfigureAwait(false) != null)
                    throw new PhoneInUseException(newPhone);
            }

            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewPhoneData
            {
                UserId = id,
                Phone = newPhone,
            }, DbAction.AddPhone, UserSubject(id), now + AddPhoneTimeout, context, now + AddPhoneShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(isChanged ? UserManagerComOps.ChangePhone : UserManagerComOps.AddPhone, lang, [newPhone], (ma, coms) =>
            {
                String link = String.Concat(LinkRoot(context), isChanged ? "auth/ChangePhone.html?" : "auth/AddPhone.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Phone]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);
            return sentTo;
        }

        /// <summary>
        /// Delete an phone number
        /// </summary>
        /// <param name="request">Validation</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>An array of communication targets that recovery instructions was sent to</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<String[]> DeletePhone(AutheticatedRequest request, HttpServerRequest context)
        {
            var user = ValidatePasswordRequestData(request, context);
            if (user == null)
                throw new AuthenticationFailedException();
            var userId = user.Id;
            List<String> targets;
            String phoneTarget;
            var am = AuthMan;
            if (am == null)
                throw new Exception("Phone not supported!");
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
                if (!ValidatePasswordRequestPassword(request, authData))
                    throw new AuthenticationFailedException();
                targets = await GetUserComTargets(c, userId).ConfigureAwait(false);
                phoneTarget = targets.FirstOrDefault(x => am.ValidatePhoneNumber(ref x, false));
                if (phoneTarget == null)
                    throw new UserDontHavePhone();
                //                if (targets.Count <= 1)
                //                    throw new CantDeleteLastUserCom();
            }
            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewPhoneData
            {
                UserId = userId,
                Phone = phoneTarget,
            }, DbAction.AddPhone, UserSubject(userId, RestoreVariant), now + AddPhoneTimeout, context, now + AddPhoneShortTimeout).ConfigureAwait(false);

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeletedPhone, lang, [phoneTarget], (ma, coms) =>
            {
                String link = String.Concat(LinkRoot(context), "auth/AddPhone.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Phone]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                await c.DeleteAllAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
                //  The links that depend on the removed number stop working, except the restore link just sent to it
                await RevokeOnEvent(c, userId, AccountEvents.PhoneRemoved, tokens.Item1).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            await RaiseOnPhoneRemoved(userId, phoneTarget).ConfigureAwait(false);
            context.Session.InvalidateCache();
            context.Server.PushMessageUser(MakeGuid(userId), new PushMessage("reload"));
            return sentTo;
        }

        /// <summary>
        /// Add/change an phone number based on a token
        /// </summary>
        /// <param name="token">The action token that was sent in the phone</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>True if successful</returns>
        [WebApi]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> AddPhone(String token, HttpServerRequest context)
        {
            var dataD = await GetAction<InternalNewPhoneData>(token, DbAction.AddPhone, context).ConfigureAwait(false);
            if (dataD == null)
                throw Refused(token);
            token = dataD.Item1;
            var data = dataD.Item2;
            var userId = data.UserId;
            var newPhone = data.Phone;
            String existingPhone = null;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    throw new UserNoLongerExistException(userId);
                await ThrowIfDisabled(c, userId).ConfigureAwait(false);
                if (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == newPhone).ConfigureAwait(false) != null)
                    throw new PhoneInUseException(newPhone);
                var existing = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
                //  A restore link (sent to a removed number) never replaces a newer number
                if ((existing != null) && SubjectTokens.HasTag(token, DbAction.AddPhone, UserSubject(userId, RestoreVariant)))
                {
                    await DeleteAction(c, token, DbAction.AddPhone, context).ConfigureAwait(false);
                    await tr.CommitAsync().ConfigureAwait(false);
                    MarkDead(token, CauseNewerContact);
                    throw new TokenExpiredException(TokenRefusedReason(CauseNewerContact, false, DbAction.AddPhone));
                }
                if (existing != null)
                {
                    existingPhone = existing.Phone;
                    if (user.UserName.FastEquals(existingPhone))
                    {
                        user.UserName = newPhone;
                        if (user.NickName.FastEquals(existingPhone))
                        {
                            user.NickName = newPhone;
                            await c.UpdateAsync(user, x => new { x.UserName, x.NickName }).ConfigureAwait(false);
                        }else
                        {
                            await c.UpdateAsync(user, x => new { x.UserName }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        if (user.NickName.FastEquals(existingPhone))
                        {
                            user.NickName = newPhone;
                            await c.UpdateAsync(user, x => new { x.NickName }).ConfigureAwait(false);
                        }
                    }
                    await c.DeleteAsync(existing).ConfigureAwait(false);
                }
                await c.InsertAsync(new DbPhoneNumber
                {
                    Phone = newPhone,
                    UserId = userId,
                }).ConfigureAwait(false);
                await DeleteAction(c, token, DbAction.AddPhone, context).ConfigureAwait(false);
                //  The links that depend on the old number stop working: reset / delete links sent to it, other phone links, the restore link
                await RevokeOnEvent(c, userId, AccountEvents.PhoneChanged).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            if (existingPhone == null)
                await RaiseOnPhoneAdded(userId, newPhone).ConfigureAwait(false);
            else
                await RaiseOnPhoneChanged(userId, newPhone, existingPhone).ConfigureAwait(false);
            context.Session.InvalidateCache();
            context.Server.PushMessageUser(MakeGuid(userId), new PushMessage("reload"));
            return true;
        }


        #endregion//Phone


        #region NickName

        public readonly bool CanSetNickName;

        /// <summary>
        /// Change the nick name of the currently logged in user.
        /// If successful a page reload request is sent to all sessions for that user (maybe don't have time to get the result or act on it)
        /// </summary>
        /// <param name="newName">The new nick name</param>
        /// <param name="context"></param>
        /// <returns>True if the user now has this nick name (also when it already was the name: setting it again is no failure), false if the user no longer exists</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiOptional(nameof(CanSetNickName))]
        public async Task<bool> SetNickName(String newName, HttpServerRequest context)
        {
            var user = context.Session.Auth.AuthContext as DbUser;
            if (user == null)
                throw new Exception("Only user managed by the UserManager may change nick name!");
            var t = await SetNickName(user.Id, newName).ConfigureAwait(false);
            //  Item3 is null only when the user no longer exists (else the new name, or the current one when it already was the name)
            return t.Item3 != null;
        }



        #endregion// NickName

        #endregion//Public API


    }
}
