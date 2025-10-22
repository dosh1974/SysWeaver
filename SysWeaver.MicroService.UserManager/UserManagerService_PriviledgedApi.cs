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
        /// Invite a user to join a site
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context">Context, handled internally</param>
        /// <returns>Response</returns>
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
                        var tt = t?.Trim()?.FastToLower();
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
                throw new Exception("Don't know how to send messages to " + target.ToQuoted());
            //  TODO: Validate domain
            var domain = r.Domain;

            var name = r.Name?.Trim();
            var nameOrTarget = String.IsNullOrEmpty(name) ? target : name;
            var expires = DateTime.UtcNow + InviteUserTimeout;
            String salt = AuthTools.GetRandomSalt();
            var tokens = await GetNewUserToken(name, target, null, salt, authTokens, domain, r.Language, expires).ConfigureAwait(false);
            String link = String.Concat(context?.Prefix ?? "", "auth/ChoosePassword.html?user=",
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
            return true;
        }

        /// <summary>
        /// Force a user to reset their password on the next login
        /// </summary>
        /// <param name="userIdentifier">User identifier, typically email</param>
        /// <returns>True if a password was found and reset, false if no password was found</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.PasswordReset)]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> ForcePasswordReset(String userIdentifier)
        {
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var userId = await FindUser(c, userIdentifier).ConfigureAwait(false);
            if (userId == 0)
                throw new UserDoNotExistException(userIdentifier);
            var pwd = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == userId).ConfigureAwait(false);
            if (pwd == null)
                return false;
            pwd.MustResetPassword = true;
            await c.UpdateAsync(pwd, x => new { x.MustResetPassword }).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return true;
        }


        const int ClientCache = 4;
        const int ServerCache = 3;
        const int TableRefresh = 5000;

        /// <summary>
        /// All users as a user table
        /// </summary>
        /// <param name="r">Table request</param>
        /// <returns>Table user</returns>
        [WebApi]
        [WebApiAuth(UserManagerRoles.ViewUser)]
        [WebMenuTable("User", "User/Management/UserDataTable", "View users", null, "IconUsers", 0, UserManagerRoles.ViewUser)]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(ServerCache)]
        public async Task<TableData> UserDataTable(TableDataRequest r)
        {
            var ret = await Db.GetAsTableData<DbUser>(r, TableRefresh).ConfigureAwait(false);
            return ret;
        }


        #endregion//Public privileged API

    }

}
