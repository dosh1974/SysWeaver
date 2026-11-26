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

        /// <summary>
        /// Get the name of the instance (if created by the manager).
        /// Can't be called in the constructor.
        /// </summary>
        public String InstanceName
        {
            get
            {
                var i = InternalServiceInfo;
                if (i != null)
                    return InternalInstanceName;
                i = Manager.GetInfo(this);
                InternalServiceInfo = i;
                var t = i.Name;
                InternalInstanceName = t;
                return t;
            }
        }
        String InternalInstanceName;
        ServiceInfo InternalServiceInfo;

        /// <summary>
        /// Get service instance information (if created by the manager).
        /// Can't be called in the constructor.
        /// </summary>
        public ServiceInfo ServiceInfo
        {
            get
            {
                var i = InternalServiceInfo;
                if (i != null)
                    return i;
                i = Manager.GetInfo(this);
                InternalServiceInfo = i;
                InternalInstanceName = i.Name;
                return i;
            }
        }

        #region IAuthorizer

        public override string Name => "UserManager";


        public override string GuidPrefix
        {
            get
            {
                var i = InstanceName;
                return String.IsNullOrEmpty(i) ? "UM" : ("UM-" + i);
            }
        }

        public override long ChangeCounter => Interlocked.Read(ref InternalChangeCounter);


        #pragma warning disable CS0649

        long InternalChangeCounter; //  No external changes support for now.

        #pragma warning restore CS0649

        async Task<long> FindUser(OrmConnection c, String identifier)
        {
            var user = await FindDbUserByName(c, identifier).ConfigureAwait(false);
            if (user != null)
                return user.Id;
            if (AuthMan.ValidateEmailAddress(identifier, false))
            {
                var mail = await FindDbEmail(c, identifier).ConfigureAwait(false);
                if (mail != null)
                    return mail.UserId;
            }
            var pn = identifier;
            if (AuthMan.ValidatePhoneNumber(ref pn, false))
            {
                var phone = await FindDbPhone(c, pn).ConfigureAwait(false);
                if (phone != null)
                    return phone.UserId;
            }
            return 0;
        }


        async Task<Authorization> InternalAuthUser(OrmConnection c, long userId)
        {
            var user = await GetDbUser(c, userId).ConfigureAwait(false);
            if (user == null)
                return null;
            var mail = await GetDbEmail(c, userId).ConfigureAwait(false);
            HashSet<String> tokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (var x in await c.SelectAsync<DbToken>(x => x.UserId == userId).ConfigureAwait(false))
            {
                var v = x.Token?.Trim();
                if (!String.IsNullOrEmpty(v))
                    tokens.Add(v.FastToLower());
            }
            //  A disabled account (DisableUser) has no authorization: no sign in in any way, its links don't work
            if (tokens.Contains(DisabledToken(userId)))
                return null;
            return new Authorization(this, user.UserName, tokens, false, MakeGuid(userId), mail?.Email, user.NickName, user, user.Domain, user.Language);
        }


        readonly bool DisableLogin;


        /// <summary>
        /// Get the id of a user from its guid
        /// </summary>
        /// <param name="userGuid">The guid of the user (see <see cref="MakeGuid(long)"/>)</param>
        /// <returns>The id of the user, 0 if the guid isn't a guid of this service</returns>
        public long GetUserId(String userGuid)
        {
            var prefix = GuidPrefix;
            var pl = prefix.Length;
            //  "prefix:id", the ':' first (cheap, and the length check of the prefix test)
            if ((userGuid.Length <= pl) || (userGuid[pl] != ':') || !userGuid.AsSpan(0, pl).SequenceEqual(prefix))
                return 0;
            var id = HashTools.ParseCompactInt64(userGuid.AsSpan(pl + 1));
            return id;
        }


        /// <summary>
        /// Get information about a user (from it's guid)
        /// </summary>
        /// <param name="userGuid"></param>
        /// <returns></returns>
        public override async Task<AuthorizationInfo> FindUserFromGuid(String userGuid)
        {
            if (PiiReady.Task.IsFaulted)
                return null;
            var id = GetUserId(userGuid);
            if (id == 0)
                return null;
            Authorization a;
            using (var c = await DbGetAsync().ConfigureAwait(false))
                a = await InternalAuthUser(c, id).ConfigureAwait(false);
            if (a == null)
                return null;
            return new AuthorizationInfo(a);
        }

        /// <summary>
        /// Get information about a user
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <returns></returns>
        public override async Task<AuthorizationInfo> FindUser(String userName)
        {
            if (LoginUnavailable)
                return null;
            Authorization a;
            using (var c = await DbGetAsync().ConfigureAwait(false))
            {
                var id = await FindUser(c, userName).ConfigureAwait(false);
                if (id == 0)
                    return null;
                a = await InternalAuthUser(c, id).ConfigureAwait(false);
            }
            if (a == null)
                return null;
            return new AuthorizationInfo(a);
        }


        /// <summary>
        /// True if a stored password can be used to sign in at all: it exists and has a SCRAM verifier (the placeholder that <see cref="GetSaltAsync"/> stores
        /// for a user without a password has neither, it must never match).
        /// </summary>
        /// <param name="auth">The stored password, may be null</param>
        /// <returns>True if it has a verifier</returns>
        static bool IsUsablePassword(DbAuthPassword auth)
            => (auth != null) && !String.IsNullOrEmpty(auth.StoredKey);

        /// <summary>
        /// Start the password change of a user that signed in with a password that must be changed (<see cref="ForcePasswordReset"/>):
        /// the same as <see cref="ForgotPassword"/>, the reset link and a short code (valid in the session of the request) are sent to the
        /// e-mail addresses and phone numbers of the user. The message says that an administrator required the new password
        /// (<see cref="UserManagerComOps.ForcedResetPassword"/>).
        /// </summary>
        /// <param name="userName">The user</param>
        /// <param name="context">The sign in request</param>
        /// <returns>Where the instructions were sent</returns>
        Task<String[]> StartPasswordChange(String userName, HttpServerRequest context)
            => InternalStartPasswordChange(userName, context);

        /// <summary>
        /// Sign in a user with a password check (SCRAM)
        /// </summary>
        /// <param name="userName">The user name (email, phone)</param>
        /// <param name="check">Validates the password proof against the stored password (never null)</param>
        /// <returns>The auth or null</returns>
        async Task<Authorization> PasswordAuth(string userName, Func<DbAuthPassword, bool> check)
        {
            if (LoginUnavailable)
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var id = await FindUser(c, userName).ConfigureAwait(false);
            if (id == 0)
                return null;
            var auth = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
            //  no password (the placeholder of GetSaltAsync has no verifier, before anyone could sign in as a passkey only user
            //  with the hash of an empty string) never signs in
            if (!IsUsablePassword(auth))
                return null;
            if (!check(auth))
                return null;
            var a = await InternalAuthUser(c, id).ConfigureAwait(false);
            if (a == null)
            {
                //  the password is right but an administrator disabled the account (DisableUser): no session, the sign in page says so
                //  (only to someone who knows the password; anyone else gets the usual "wrong user ID or password")
                if (await IsDisabled(c, id).ConfigureAwait(false))
                    throw new AccountDisabledException((await GetDbUser(c, id).ConfigureAwait(false))?.UserName ?? userName);
                return null;
            }
            //  the password is right but must be changed first (ForcePasswordReset): no session, the login service sends
            //  the user the reset instructions (StartPasswordChange) and tells the client
            if (auth.MustResetPassword)
            {
                var signInName = a.Username;
                throw new PasswordChangeRequiredException(signInName, context => StartPasswordChange(signInName, context));
            }
            auth.LastUsed = DateTime.UtcNow;
            await c.UpdateAsync(auth, x => new { x.LastUsed }).ConfigureAwait(false);
            tr.Commit();
            return a;
        }

        /// <summary>
        /// Get the salt to use for password hashing for a given user
        /// </summary>
        /// <param name="userName">The user name (email)</param>
        /// <returns>A salt to use or null if the user doesn't exit</returns>
        public override async Task<String> GetSaltAsync(string userName)
        {
            if (LoginUnavailable)
                return null;
            using var c = await DbGetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var id = String.IsNullOrEmpty(userName) ? 0 : await FindUser(c, userName).ConfigureAwait(false);
            if (id == 0)
                return null;
            var userGuid = MakeGuid(id);
            var auth = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
            if (auth != null)
                return auth.Salt;
            var now = DateTime.UtcNow;
            String salt;
            for (; ; )
            {
                salt = AuthTools.GetRandomSalt();
                if (await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.Salt == salt).ConfigureAwait(false) == null)
                    break;
            }
            await c.InsertAsync(new DbAuthPassword
            {
                Created = now,
                LastUsed = now,
                MustResetPassword = true,
                UserId = id,
                Salt = salt,
            }).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return salt;
        }


        #endregion//IAuthorizer

    }
}
