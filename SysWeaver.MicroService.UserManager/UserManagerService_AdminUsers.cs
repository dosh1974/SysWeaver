using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using SimpleStack.Orm;
using SysWeaver.Auth;
using SysWeaver.MicroService.Db;
using SysWeaver.Net;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The request of the admin user APIs (<see cref="UserManagerService.UnlockUser"/>, <see cref="UserManagerService.DisableUser"/>,
    /// <see cref="UserManagerService.EnableUser"/>, <see cref="UserManagerService.AdminDeleteUser"/>).
    /// </summary>
    public sealed class AdminUserRequest
    {
        /// <summary>
        /// The user: user name, e-mail address or phone number.
        /// </summary>
        public String User;

        /// <summary>
        /// Delete only: the user name of the account, typed by the administrator to confirm (case insensitive). A delete without it is refused,
        /// so a stray call (a script, a replayed request, a click) can't delete an account.
        /// </summary>
        public String Confirm;
    }


    /// <summary>
    /// The result of the admin user APIs.
    /// </summary>
    public sealed class AdminUserResult
    {
        /// <summary>
        /// The user name of the account.
        /// </summary>
        public String UserName;

        /// <summary>
        /// What was done, a sentence for the administrator (translated), ex: "c4r5d@example.com was deleted".
        /// </summary>
        public String Text;

        /// <summary>
        /// The number of sessions of the user that were signed out.
        /// </summary>
        public int SignedOut;
    }


    public sealed partial class UserManagerService
    {
        /// <summary>
        /// The prefix of the reserved token (role) that marks a disabled account (the token table's key is the token, so it is made unique with the user id):
        /// a disabled account can't sign in in any way (password, passkey, link), see <see cref="DisableUser"/>.
        /// </summary>
        public const String DisabledTokenPrefix = "sw-disabled-";

        static String DisabledToken(long userId) => DisabledTokenPrefix + userId.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// True if an administrator disabled the account (<see cref="DisableUser"/>).
        /// </summary>
        async Task<bool> IsDisabled(OrmConnection c, long userId)
        {
            if (userId <= 0)
                return false;
            var dt = DisabledToken(userId);
            return await c.FirstOrDefaultAsync<DbToken>(x => x.Token == dt).ConfigureAwait(false) != null;
        }

        /// <summary>
        /// Refuse an account action (a reset message, a link) of a disabled account: <see cref="UserDisabledException"/> (423, "This account is disabled ...").
        /// A disabled account can't sign in in any way, so it gets no password reset message and its links don't work until it is enabled again.
        /// </summary>
        async Task ThrowIfDisabled(OrmConnection c, long userId)
        {
            if (await IsDisabled(c, userId).ConfigureAwait(false))
                throw new UserDisabledException();
        }

        /// <summary>
        /// The exception for a user that has no authorization any more (<see cref="InternalAuthUser"/> returned null): disabled
        /// (<see cref="UserDisabledException"/>), else gone (<see cref="UserNoLongerExistException"/>).
        /// </summary>
        async Task<Exception> NoAuthException(OrmConnection c, long userId)
            => await IsDisabled(c, userId).ConfigureAwait(false) ? new UserDisabledException() : new UserNoLongerExistException(userId);

        /// <summary>
        /// How a user can sign in now and after a forced password reset (the facts behind <see cref="ForcePasswordResetResult"/>).
        /// </summary>
        sealed class UserAccess
        {
            public DbUser User;
            public bool HasPassword;
            public bool MustResetPassword;
            /// <summary>
            /// The e-mail addresses and phone numbers a message (the reset link) can be sent to (a sender for it is configured).
            /// </summary>
            public int Reachable;
            public int PassKeys;
            public bool Disabled;

            /// <summary>
            /// True if the user can't sign in at all: disabled, or no passkey and no usable password (none, or one that must be reset and nowhere to send the link).
            /// </summary>
            public bool LockedOut => Disabled || ((PassKeys <= 0) && (!HasPassword || (MustResetPassword && (Reachable <= 0))));

            /// <summary>
            /// True if a forced reset would leave the user without any way to sign in.
            /// </summary>
            public bool ResetLocksOut => HasPassword && (PassKeys <= 0) && (Reachable <= 0);
        }

        async Task<UserAccess> GetUserAccess(OrmConnection c, long userId)
        {
            var user = userId <= 0 ? null : await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
            if (user == null)
                return null;
            var pwd = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
            var a = new UserAccess
            {
                User = user,
                HasPassword = IsUsablePassword(pwd),
                PassKeys = (int)await c.CountAsync<DbAuthPassKey>(x => x.UserId == userId).ConfigureAwait(false),
            };
            a.MustResetPassword = a.HasPassword && pwd.MustResetPassword;
            var coms = Coms;
            foreach (var t in await GetUserComTargets(c, userId).ConfigureAwait(false))
            {
                foreach (var com in coms)
                {
                    var target = t;
                    if (com.CleanAndValidate(ref target))
                    {
                        ++a.Reachable;
                        break;
                    }
                }
            }
            a.Disabled = await IsDisabled(c, userId).ConfigureAwait(false);
            return a;
        }

        /// <summary>
        /// The reason a forced reset is refused for a user that would be locked out by it (english, translated by the server when thrown).
        /// </summary>
        static String ResetLocksOutReason(String userName)
            => String.Concat(userName, " has no e-mail address, phone number or passkey: after a forced password reset they could not sign in again. Ask them to add an e-mail address, a phone number or a passkey first, or disable the account");

        static Task<String> Tr(HttpServerRequest context, String text)
            => context?.Translator.TranslateSafe(text, context.Session?.Language ?? context.Language, "en", "A fact shown to an administrator before or after an action on a user account", TranslationEffort.Medium, TranslationCacheRetention.Long) ?? Task.FromResult(text);

        /// <summary>
        /// Fill the sign in facts and the consequences of a forced reset into a result (translated to the session language).
        /// </summary>
        async Task FillAccessFacts(ForcePasswordResetResult res, UserAccess a, HttpServerRequest context)
        {
            res.Reachable = a.Reachable;
            res.PassKeys = a.PassKeys;
            res.MustResetPassword = a.MustResetPassword;
            res.Disabled = a.Disabled;
            res.LockedOut = a.LockedOut;
            var name = a.User.UserName;
            List<String> c = new List<string>();
            if (a.Disabled)
                c.Add(await Tr(context, name + " is disabled and can't sign in.").ConfigureAwait(false));
            if (a.LockedOut && !a.Disabled)
                c.Add(await Tr(context, name + " can't sign in now: there is no passkey, and no e-mail address or phone number to send a password link to.").ConfigureAwait(false));
            if (a.ResetLocksOut)
            {
                res.Refused = await Tr(context, ResetLocksOutReason(name)).ConfigureAwait(false);
            }
            else if (a.HasPassword)
            {
                //  (the same sentence as the row action's confirmation (DbUserAdminRow) and the Users page, plus the link: Admin N49)
                c.Add(await Tr(context, a.MustResetPassword
                    ? "A new password is already required: nothing changes."
                    : "They must choose a new password at the next sign in: the current password stops working and their open sessions end. They get a link to choose it by e-mail or text message.").ConfigureAwait(false));
                if (a.PassKeys > 0)
                    c.Add(await Tr(context, a.PassKeys == 1 ? "Their passkey keeps working." : "Their " + a.PassKeys.ToString(CultureInfo.InvariantCulture) + " passkeys keep working.").ConfigureAwait(false));
            }
            res.Consequences = c.ToArray();
        }


        /// <summary>
        /// Find a user of the admin APIs by user name, e-mail address or phone number.
        /// </summary>
        async Task<UserAccess> AdminFind(OrmConnection c, String user)
        {
            user = user?.Trim();
            if (String.IsNullOrEmpty(user) || (user.Length > 256))
                throw new HttpResponseException(400, "Enter the user name, e-mail address or phone number of the user");
            var id = await FindUser(c, user).ConfigureAwait(false);
            return await GetUserAccess(c, id).ConfigureAwait(false)
                ?? throw new HttpResponseException(404, String.Concat("There is no user ", user.ToQuoted(), ": check the user name, e-mail address or phone number"));
        }

        int SignOutUser(HttpServerRequest context, long userId, String reason)
        {
            var server = context?.Server;
            if (server == null)
                return 0;
            List<HttpSession> sessions = new List<HttpSession>();
            server.EnumUserSessions(MakeGuid(userId), s => sessions.Add(s));
            int n = 0;
            foreach (var s in sessions)
                if (server.CloseSession(s, reason))
                    ++n;
            return n;
        }

        static void RefuseOwnAccount(HttpServerRequest context, DbUser user, String what)
        {
            var own = (context?.Session?.Auth?.AuthContext as DbUser)?.Id ?? 0;
            if ((own != 0) && (own == user.Id))
                throw HttpResponseException.Refused(String.Concat("This is your own account: you can't ", what, " it here"));
        }

        /// <summary>
        /// Undo a forced password reset (unlock): the user's current password works again (ex: a user that was forced to reset but has no e-mail address
        /// or phone number to get the link). Logged (who, whom).
        /// </summary>
        /// <param name="r">The user</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        /// <exception cref="HttpResponseException">400 no user given, 404 no such user, 409 AlreadyDone: no forced reset is pending (nothing to undo), 409 the user has no password</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> UnlockUser(AdminUserRequest r, HttpServerRequest context)
        {
            String name;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var a = await AdminFind(c, r?.User).ConfigureAwait(false);
                name = a.User.UserName;
                if (!a.HasPassword)
                    throw HttpResponseException.Refused(String.Concat(name, " has no password, there is no password reset to undo"));
                if (!a.MustResetPassword)
                    throw HttpResponseException.AlreadyDone(String.Concat(name, " is not required to choose a new password, there is nothing to undo"));
                var id = a.User.Id;
                var pwd = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                pwd.MustResetPassword = false;
                await c.UpdateAsync(pwd, x => new { x.MustResetPassword }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            var by = context?.Session?.Auth?.Username ?? "?";
            Manager.AddMessage(MessageLevels.Info, $"The forced password reset of \"{name}\" was undone by \"{by}\": the current password works again");
            return new AdminUserResult
            {
                UserName = name,
                Text = await Tr(context, name + " can sign in with the current password again.").ConfigureAwait(false),
            };
        }

        /// <summary>
        /// Disable an account: it can't sign in in any way (password, passkey, links) until it is enabled again (<see cref="EnableUser"/>), and its sessions end.
        /// Nothing is deleted. Your own account can't be disabled. Logged (who, whom).
        /// </summary>
        /// <param name="r">The user</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        /// <exception cref="HttpResponseException">400 no user given, 404 no such user, 409 AlreadyDone: already disabled, 409 your own account</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> DisableUser(AdminUserRequest r, HttpServerRequest context)
        {
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var a = await AdminFind(c, r?.User).ConfigureAwait(false);
                user = a.User;
                RefuseOwnAccount(context, user, "disable");
                if (a.Disabled)
                    throw HttpResponseException.AlreadyDone(String.Concat(user.UserName, " is already disabled"));
                await c.InsertAsync<DbToken>(new DbToken { UserId = user.Id, Token = DisabledToken(user.Id) }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            var n = SignOutUser(context, user.Id, "Your account was disabled by an administrator");
            var by = context?.Session?.Auth?.Username ?? "?";
            Manager.AddMessage(MessageLevels.Warning, $"The account \"{user.UserName}\" (#{user.Id}) was disabled by \"{by}\", {n} session(s) signed out");
            return new AdminUserResult
            {
                UserName = user.UserName,
                SignedOut = n,
                Text = await Tr(context, user.UserName + " is disabled and can't sign in until the account is enabled again.").ConfigureAwait(false),
            };
        }

        /// <summary>
        /// Enable an account that was disabled (<see cref="DisableUser"/>). Logged (who, whom).
        /// </summary>
        /// <param name="r">The user</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        /// <exception cref="HttpResponseException">400 no user given, 404 no such user, 409 AlreadyDone: not disabled</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> EnableUser(AdminUserRequest r, HttpServerRequest context)
        {
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var a = await AdminFind(c, r?.User).ConfigureAwait(false);
                user = a.User;
                if (!a.Disabled)
                    throw HttpResponseException.AlreadyDone(String.Concat(user.UserName, " is not disabled"));
                var dt = DisabledToken(user.Id);
                await c.DeleteAllAsync<DbToken>(x => x.Token == dt).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            var by = context?.Session?.Auth?.Username ?? "?";
            Manager.AddMessage(MessageLevels.Info, $"The account \"{user.UserName}\" (#{user.Id}) was enabled by \"{by}\"");
            return new AdminUserResult
            {
                UserName = user.UserName,
                Text = await Tr(context, user.UserName + " can sign in again.").ConfigureAwait(false),
            };
        }

        /// <summary>
        /// Delete an account as an administrator (ex: a user that is locked out, or left): the account, its sign in methods, addresses, roles and data are removed,
        /// its links stop working and its sessions end. Confirm must be the account's user name (typed by the administrator).
        /// Your own account can't be deleted here (use Delete account). Logged (who, whom).
        /// </summary>
        /// <param name="r">The user and the confirmation</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        /// <exception cref="HttpResponseException">400 no user given / the confirmation is not the user name, 404 no such user, 409 your own account</exception>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> AdminDeleteUser(AdminUserRequest r, HttpServerRequest context)
        {
            DbUser user;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var a = await AdminFind(c, r?.User).ConfigureAwait(false);
                user = a.User;
                RefuseOwnAccount(context, user, "delete");
                if (!String.Equals(r?.Confirm?.Trim(), user.UserName, StringComparison.OrdinalIgnoreCase))
                    throw HttpResponseException.Refused(String.Concat("Type the user name ", user.UserName.ToQuoted(), " to confirm the delete"), 400);
                await DeleteUserRows(c, user.Id).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            var n = SignOutUser(context, user.Id, "Your account was deleted by an administrator");
            var by = context?.Session?.Auth?.Username ?? "?";
            Manager.AddMessage(MessageLevels.Warning, $"User \"{user.UserName}\" (#{user.Id}) was deleted by the administrator \"{by}\", {n} session(s) signed out");
            await RaiseOnUserDeleted(user.Id, user.UserName).ConfigureAwait(false);
            return new AdminUserResult
            {
                UserName = user.UserName,
                SignedOut = n,
                Text = await Tr(context, user.UserName + " was deleted.").ConfigureAwait(false),
            };
        }

        /// <summary>
        /// Remove every row of a user (in the caller's transaction) and revoke all its links.
        /// </summary>
        async Task DeleteUserRows(OrmConnection c, long id)
        {
            var userGuid = MakeGuid(id);
            await c.DeleteAllAsync<DbUser>(x => x.Id == id).ConfigureAwait(false);
            await c.DeleteAllAsync<DbEmailAddress>(x => x.UserId == id).ConfigureAwait(false);
            await c.DeleteAllAsync<DbPhoneNumber>(x => x.UserId == id).ConfigureAwait(false);
            await c.DeleteAllAsync<DbAuthPassKey>(x => x.UserId == id).ConfigureAwait(false);
            await c.DeleteAllAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
            await c.DeleteAllAsync<DbToken>(x => x.UserId == id).ConfigureAwait(false);
            await c.DeleteAllAsync<DbUserData>(x => x.UserGuid == userGuid).ConfigureAwait(false);
            await c.DeleteAllAsync<DbUserDataString>(x => x.UserGuid == userGuid).ConfigureAwait(false);
            //  Every link of the user stops working
            await RevokeOnEvent(c, id, AccountEvents.AccountDeleted).ConfigureAwait(false);
        }


        /// <summary>
        /// Where a code or link for the signed in user would be sent (read only, nothing is sent): the user's own e-mail addresses and phone numbers that a message
        /// can be sent to. For the text above a "Send the code" button of an account page (ex: Delete account), so a page never sends a message on load.
        /// </summary>
        /// <param name="context">Context, handled internally</param>
        /// <returns>The addresses / numbers (empty if none)</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<String[]> GetMyComTargets(HttpServerRequest context)
        {
            var user = context.Session.Auth.AuthContext as DbUser ?? throw new NoUserLoggedInException();
            List<String> res = new List<string>();
            using var c = await Db.GetAsync().ConfigureAwait(false);
            foreach (var t in await GetUserComTargets(c, user.Id).ConfigureAwait(false))
            {
                foreach (var com in Coms)
                {
                    var target = t;
                    if (com.CleanAndValidate(ref target))
                    {
                        res.Add(target);
                        break;
                    }
                }
            }
            return res.ToArray();
        }

        /// <summary>
        /// Row action of the users table: undo a forced password reset (<see cref="UnlockUser"/>).
        /// </summary>
        /// <param name="token">The row token of the user (from the "Actions" column of the users table)</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> RowUnlockUser(String token, HttpServerRequest context)
            => await UnlockUser(new AdminUserRequest { User = await RowUserName(token).ConfigureAwait(false) }, context).ConfigureAwait(false);

        /// <summary>
        /// Row action of the users table: disable the account (<see cref="DisableUser"/>).
        /// </summary>
        /// <param name="token">The row token of the user (from the "Actions" column of the users table)</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> RowDisableUser(String token, HttpServerRequest context)
            => await DisableUser(new AdminUserRequest { User = await RowUserName(token).ConfigureAwait(false) }, context).ConfigureAwait(false);

        /// <summary>
        /// Row action of the users table: enable the account (<see cref="EnableUser"/>).
        /// </summary>
        /// <param name="token">The row token of the user (from the "Actions" column of the users table)</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>What was done</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ManageUser)]
        [WebApiAudit(AuditGroup)]
        public async Task<AdminUserResult> RowEnableUser(String token, HttpServerRequest context)
            => await EnableUser(new AdminUserRequest { User = await RowUserName(token).ConfigureAwait(false) }, context).ConfigureAwait(false);

        async Task<String> RowUserName(String token)
        {
            var id = UserRowTokenId(token);
            if (id <= 0)
                throw new HttpResponseException(403, "This row is no longer valid: refresh the list and try again");
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var u = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false);
            return u?.UserName ?? throw HttpResponseException.Gone("The user no longer exists: refresh the list", 404);
        }

        /// <summary>
        /// The row actions value of a user in the users table: "rowToken,userName,reset,unlock,disable,enable" (the last four "1" or "0", see <see cref="DbUserAdminRow.Actions"/>).
        /// </summary>
        static String UserActions(long id, String userName, UserAccess a)
            => String.Join(',', UserRowToken(id), (userName ?? "").Replace(',', ' '),
                (a != null) && a.HasPassword && !a.MustResetPassword && !a.ResetLocksOut && !a.Disabled ? "1" : "0",
                (a != null) && a.MustResetPassword ? "1" : "0",
                (a != null) && !a.Disabled ? "1" : "0",
                (a != null) && a.Disabled ? "1" : "0");
    }
}
