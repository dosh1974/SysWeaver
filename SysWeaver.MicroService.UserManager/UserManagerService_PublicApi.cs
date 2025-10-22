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
            if (DateTime.UtcNow >= shortData.Expiration)
            {
                s.TryRemove(ShortCodeName);
                return -1;
            }
            if (!shortData.ShortCode.FastEquals(CleanUpShortCode(code)))
            {
                --shortData.TriesLeft;
                if (shortData.TriesLeft < 0)
                    s.TryRemove(ShortCodeName);
                return 0;
            }
            return 1;
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
            var tokens = await AddAction(id, DbAction.DeleteUser, now + DeleteUserTimeout, context, now + DeleteUserShortTimeout).ConfigureAwait(false);
            String link = String.Concat(context?.Prefix ?? "", "auth/DeleteAccount.html?token=",
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
        /// Delete's a user, using the action token recieved in a message
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
                throw new TokenExpiredException();
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
                await tr.CommitAsync().ConfigureAwait(false);
            }
            context.Server.PushMessageUser(MakeGuid(id), new PushMessage("reload"));
            context.Server.CloseSession(context.Session);
            try
            {
                await RaiseOnUserDeleted(id, name).ConfigureAwait(false);
            }
            catch
            {
            }
            return name;
        }

        /// <summary>
        /// Add a new user (and optionally password), using the action token recieved in an email
        /// </summary>
        /// <param name="r">Paramaters</param>
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
            tr.Commit();
            return true;
        }


        /// <summary>
        /// Send an message to all the users communication methods that enables a user to reset the password
        /// </summary>
        /// <param name="identifier">User identification</param>
        /// <param name="context"></param>
        /// <returns>An array of communication targets that instructions was sent to</returns>
        /// <exception cref="UserDoNotExistException"></exception>
        /// <exception cref="UserHaveNoComs"></exception>
        [WebApi]
        public async Task<String[]> ForgotPassword(String identifier, HttpServerRequest context)
        {
            identifier = identifier?.Trim();
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
                identifier = user.UserName;
                targets = await GetUserComTargets(c, id).ConfigureAwait(false);
            }
            if (targets.Count <= 0)
                throw new UserHaveNoComs();
            var salt = AuthTools.GetRandomSalt();
            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewPasswordData
            {
                NickName = user.NickName,
                UserName = user.UserName,
                UserId = id,
                Salt = salt,
            }, DbAction.ResetPassword, now + ResetPasswordTimeout, context, now + ResetPasswordShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.ResetPassword, lang, targets, (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", "auth/ResetPassword.html?", HttpUtility.UrlEncode(tokens.Item1));
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
        /// Set a new password for a user, using the action token recieved in an communication
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
            var dataD = await GetAction<InternalNewPasswordData>(token, DbAction.ResetPassword, context).ConfigureAwait(false);
            if (dataD == null)
            {
                dataD = await GetAction<InternalNewPasswordData>(token, DbAction.AddPassword, context).ConfigureAwait(false);
                if (dataD == null)
                    throw new TokenExpiredException();
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
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var authData = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                var isNew = authData == null;
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
                    throw new UserNoLongerExistException(id);
                await DeleteAction(c, token, DbAction.ResetPassword, context).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
            var ret = new UserLoginResponse
            {
                Username = auth.Username,
                Tokens = auth.Tokens?.ToArray(),
            };
            if (s.Auth != null)
                s.Auth.RequestLogout("Password change");
            s.SetAuth(auth);
            await context.Server.RunOnLogin(s).ConfigureAwait(false);
            return ret;
        }

        /// <summary>
        /// Get information about a potential new user from a token
        /// </summary>
        /// <param name="token">The token as sent in the email</param>
        /// <param name="context"></param>
        /// <returns>New user data or null if it's an invalid token</returns>
        [WebApi]
        public async Task<NewPasswordData> GetNewPasswordData(String token, HttpServerRequest context)
        {
            var dataD = await GetAction<InternalNewPasswordData>(token, DbAction.ResetPassword, context).ConfigureAwait(false);
            if (dataD == null)
            {
                dataD = await GetAction<InternalNewPasswordData>(token, DbAction.AddPassword, context).ConfigureAwait(false);
                if (dataD == null)
                    throw new TokenExpiredException();
            }
            token = dataD.Item1;
            var t = dataD.Item2;
            return new NewPasswordData
            {
                NickName = t.NickName,
                Salt = t.Salt,
                UserName = t.UserName,
            };
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
                if ((await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false)) != null)
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
            }, DbAction.AddPassword, now + AddPasswordTimeout, context, now + AddPasswordShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.AddPassword, lang, targets, (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", "auth/AddPassword.html?", HttpUtility.UrlEncode(tokens.Item1));
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
                if ((await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false)) == null)
                    throw new UserDontHavePassword();
            }
            var salt = AuthTools.GetRandomSalt();
            var now = DateTime.UtcNow;
            var tokens = await AddAction(id, DbAction.DeletePassword, now + DelPasswordTimeout, context, now + DelPasswordShortTimeout).ConfigureAwait(false);

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeletePassword, lang, targets, (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", "auth/DeletePassword.html?", HttpUtility.UrlEncode(tokens.Item1));
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
        /// Delete's a password from a users account, using the action token recieved in an email
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
                throw new TokenExpiredException();
            token = dataD.Item1;
            var id = dataD.Item2;
            if (id == 0)
                throw new TokenExpiredException();
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);

                await c.DeleteAllAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                await DeleteAction(c, token, DbAction.DeletePassword, context).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
            }
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
                if (existingEmail != null)
                    if (existingEmail.ToLower().FastEquals(newEmail.ToLower()))
                        throw new EmailAlreadyAssignedException(newEmail);
                if (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == newEmail).ConfigureAwait(false) != null)
                    throw new EmailInUseException(newEmail);
            }

            var now = DateTime.UtcNow;
            var tokens = await AddAction(new InternalNewEmailData
            {
                UserId = userId,
                Email = newEmail,
            }, DbAction.AddEmail, now + AddEmailTimeout).ConfigureAwait(false);
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
                    "[Origin]", origin.Replace("[Color]", HashColors.AppColors.Acc1)
                );
            }).ConfigureAwait(false);
            return sentTo;

        }


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
                    if (existingEmail.ToLower().FastEquals(newEmail.ToLower()))
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
            }, DbAction.AddEmail, now + AddEmailTimeout, context, now + AddEmailShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(isChanged ? UserManagerComOps.ChangeEmail : UserManagerComOps.AddEmail, lang, [newEmail], (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", isChanged ? "auth/ChangeEmail.html?" : "auth/AddEmail.html?", HttpUtility.UrlEncode(tokens.Item1));
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
            }, DbAction.AddEmail, now + AddEmailTimeout, context, now + AddEmailShortTimeout).ConfigureAwait(false);

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeletedEmail, lang, [emailTarget], (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", "auth/AddEmail.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Email]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                await c.DeleteAllAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
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
                throw new TokenExpiredException();
            token = dataD.Item1;
            var data = dataD.Item2;
            var userId = data.UserId;
            var newEmail = data.Email;
            String existingEmail = null;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
                if (user == null)
                    throw new UserNoLongerExistException(userId);
                if (await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == newEmail).ConfigureAwait(false) != null)
                    throw new EmailInUseException(newEmail);
                var existing = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
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
                await tr.CommitAsync().ConfigureAwait(false);
            }
            if (existingEmail == null)
                await RaiseOnEmailAdded(userId, newEmail).ConfigureAwait(false);
            else
                await RaiseOnEmailChanged(userId, newEmail, existingEmail).ConfigureAwait(false);
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
                    if (existingPhone.ToLower().FastEquals(newPhone.ToLower()))
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
            }, DbAction.AddPhone, now + AddPhoneTimeout, context, now + AddPhoneShortTimeout).ConfigureAwait(false);


            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(isChanged ? UserManagerComOps.ChangePhone : UserManagerComOps.AddPhone, lang, [newPhone], (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", isChanged ? "auth/ChangePhone.html?" : "auth/AddPhone.html?", HttpUtility.UrlEncode(tokens.Item1));
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
            }, DbAction.AddPhone, now + AddPhoneTimeout, context, now + AddPhoneShortTimeout).ConfigureAwait(false);

            var userName = user.NickName ?? user.UserName;
            var lang = await GetLang(user.Language ?? context?.Session?.Language).ConfigureAwait(false);
            var sentTo = await Send(UserManagerComOps.DeletedPhone, lang, [phoneTarget], (ma, coms) =>
            {
                String link = String.Concat(context?.Prefix ?? "", "auth/AddPhone.html?", HttpUtility.UrlEncode(tokens.Item1));
                return GetMessageParams(context,
                    "[UserName]", userName,
                    "[Phone]", ma,
                    "[ShortCode]", FormatShortCode(tokens.Item2),
                    "[Link]", link
                );
            }).ConfigureAwait(false);
            using (var c = await Db.GetAsync().ConfigureAwait(false))
                await c.DeleteAllAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
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
                throw new TokenExpiredException();
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
                if (await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == newPhone).ConfigureAwait(false) != null)
                    throw new PhoneInUseException(newPhone);
                var existing = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.UserId == userId).ConfigureAwait(false);
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
        /// <returns>True if name was changed</returns>
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
            return t.Item1;
        }



        #endregion// NickName

        #endregion//Public API


    }
}
