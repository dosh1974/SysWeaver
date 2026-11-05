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
            var user = await c.FirstOrDefaultAsync<DbUser>(x => x.UserName == identifier).ConfigureAwait(false);
            if (user != null)
                return user.Id;
            if (AuthMan.ValidateEmailAddress(identifier, false))
            {
                var mail = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.Email == identifier).ConfigureAwait(false);
                if (mail != null)
                    return mail.UserId;
            }
            var pn = identifier;
            if (AuthMan.ValidatePhoneNumber(ref pn, false))
            {
                var phone = await c.FirstOrDefaultAsync<DbPhoneNumber>(x => x.Phone == pn).ConfigureAwait(false);
                if (phone != null)
                    return phone.UserId;
            }
            return 0;
        }


        async Task<Authorization> InternalAuthUser(OrmConnection c, long userId)
        {
            var user = await c.FirstOrDefaultAsync<DbUser>(x => x.Id == userId).ConfigureAwait(false);
            if (user == null)
                return null;
            var mail = await c.FirstOrDefaultAsync<DbEmailAddress>(x => x.UserId == userId).ConfigureAwait(false);
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
            var id = GetUserId(userGuid);
            if (id == 0)
                return null;
            Authorization a;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
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
            if (DisableLogin)
                return null;
            Authorization a;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
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
        /// The base64 chars of a hash that are encoded on the stack (a SHA-256 hash is 44 chars)
        /// </summary>
        const int MaxStackPasswordHashChars = 128;

        /// <summary>
        /// Constant time compare of a stored password hash (base64) and a hash (the stored hash is password equivalent)
        /// </summary>
        static bool IsSamePasswordHash(String storedPwd, byte[] hash)
        {
            if (storedPwd == null)
                return false;
            //  The base64 text of the hash on the stack (the same chars as AuthTools.HashToString), a hash that doesn't fit uses the string
            ReadOnlySpan<Byte> hs = hash;
            scoped ReadOnlySpan<Char> h;
            Span<Char> b = stackalloc Char[MaxStackPasswordHashChars];
            if (Convert.TryToBase64Chars(hs, b, out var written))
                h = b.Slice(0, written);
            else
                h = AuthTools.HashToString(hs);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(storedPwd.AsSpan()),
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(h));
        }

        /// <summary>
        /// True if a stored password can be used to sign in at all: it exists and has a hash (the placeholder that <see cref="GetSaltAsync"/> stores
        /// for a user without a password has an empty hash, it must never match).
        /// </summary>
        /// <param name="auth">The stored password, may be null</param>
        /// <returns>True if it has a hash</returns>
        static bool IsUsablePassword(DbAuthPassword auth)
            => (auth != null) && !String.IsNullOrEmpty(auth.Pwd);

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
        /// Authorize a user
        /// </summary>
        /// <param name="userName">The user name (email)</param>
        /// <param name="hash">The password hash: hash(userName + salt)</param>
        /// <returns>The auth or null</returns>
        public override async Task<Authorization> BasicAuth(string userName, byte[] hash)
        {
            if (DisableLogin)
                return null;
            if (!Params.AllowBasicAuth)
                return null;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var id = await FindUser(c, userName).ConfigureAwait(false);
                if (id == 0)
                    return null;
                var auth = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
                //  no password (the placeholder of GetSaltAsync has an empty hash) or a password that must be changed first never signs in
                if (!IsUsablePassword(auth))
                    return null;
                if (!IsSamePasswordHash(auth.Pwd, hash))
                    return null;
                if (auth.MustResetPassword)
                    return null;
                var a = await InternalAuthUser(c, id).ConfigureAwait(false);
                if (a == null)
                    return null;
                auth.LastUsed = DateTime.UtcNow;
                await c.UpdateAsync(auth, x => new { x.LastUsed }).ConfigureAwait(false);
                tr.Commit();
                return a;
            }
        }

        /// <summary>
        /// Authorize a user
        /// </summary>
        /// <param name="userName">The user name (email)</param>
        /// <param name="hash">The password hash: SHA256(UTF8.GetBytes(ToBase64(SHA256(UTF8.GetBytes("userName.FastLower()|password|suffix|salt"))) | oneTimePad) where suffix is the Authorize.HashSuffix</param>
        /// <param name="oneTimePad">The one time pad used</param>
        /// <returns>The auth or null</returns>
        public override async Task<Authorization> SecureAuth(string userName, byte[] hash, String oneTimePad)
        {
            if (DisableLogin)
                return null;
            using var c = await Db.GetAsync().ConfigureAwait(false);
            using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
            var id = await FindUser(c, userName).ConfigureAwait(false);
            if (id == 0)
                return null;
            var auth = await c.FirstOrDefaultAsync<DbAuthPassword>(x => x.UserId == id).ConfigureAwait(false);
            //  no password (the placeholder of GetSaltAsync has an empty hash, before anyone could sign in as a passkey only user
            //  with the hash of an empty string) never signs in
            if (!IsUsablePassword(auth))
                return null;
            var hh = AuthTools.ComputeHash(auth.Pwd, oneTimePad);
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(hh, hash))
                return null;
            var a = await InternalAuthUser(c, id).ConfigureAwait(false);
            if (a == null)
            {
                //  the password is right but an administrator disabled the account (DisableUser): no session, the sign in page says so
                //  (only to someone who knows the password; anyone else gets the usual "wrong user ID or password")
                if (await IsDisabled(c, id).ConfigureAwait(false))
                    throw new AccountDisabledException((await c.FirstOrDefaultAsync<DbUser>(x => x.Id == id).ConfigureAwait(false))?.UserName ?? userName);
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
            if (DisableLogin)
                return null;
            using var c = await Db.GetAsync().ConfigureAwait(false);
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
                Pwd = "",
                UserId = id,
                Salt = salt,
            }).ConfigureAwait(false);
            await tr.CommitAsync().ConfigureAwait(false);
            return salt;
        }


        #endregion//IAuthorizer

    }
}
