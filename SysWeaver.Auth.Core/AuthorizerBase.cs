using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Base class for a source of users that can validate credentials (used by <see cref="AuthManager"/>).
    /// Implementations override the auth methods they support, the defaults reject everything.
    /// </summary>
    /// <remarks>
    /// Password hashes used by the framework are: hash = SHA256(UTF8(password + "|" + salt)) where the salt is provided by <see cref="GetSaltAsync(string)"/>,
    /// see <see cref="AuthTools.ComputeHash(string, string)"/>.
    /// Implementations must be thread safe.
    /// </remarks>
    public abstract class AuthorizerBase
    {
        /// <summary>
        /// A unique name of this authorizer
        /// </summary>
        public abstract String Name { get; }

        /// <summary>
        /// The unique guid prefix, all user guids from this authorizer starts with this prefix followed by a ':'.
        /// </summary>
        public abstract String GuidPrefix { get;  }

        /// <summary>
        /// Return this for failed auths
        /// </summary>
        protected static readonly Task<Authorization> NoAuth = TaskExt<Authorization>.NullTask;

        /// <summary>
        /// Get information about a user (from it's guid)
        /// </summary>
        /// <param name="userGuid">The user guid (including the prefix)</param>
        /// <returns>The user information, or null if unknown</returns>
        public abstract Task<AuthorizationInfo> FindUserFromGuid(String userGuid);

        /// <summary>
        /// Get information about a user
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <returns>The user information, or null if unknown</returns>
        public abstract Task<AuthorizationInfo> FindUser(String userName);

        /// <summary>
        /// Authorize a user from the Http header using the basic auth schema (plain text password or hash have been sent, can use replay attacks etc)
        /// </summary>
        /// <param name="userName">The plain text username</param>
        /// <param name="hash">The password hash: SHA256(UTF8.GetBytes(password + "|" + salt)) where salt is the salt returned by <see cref="GetSaltAsync(string)"/></param>
        /// <returns>null if password is wrong or user is unknown (or basic auth is not allowed for the user)</returns>
        public virtual Task<Authorization> BasicAuth(String userName, Byte[] hash) => NoAuth;


        /// <summary>
        /// Authorize a user from the Http header using the bearer token schema (can use replay attacks etc)
        /// </summary>
        /// <param name="token">The Bearer token found in the header</param>
        /// <returns>null if the Bearer token is wrong or unknown</returns>
        public virtual Task<Authorization> BearerAuth(String token) => NoAuth;


        /// <summary>
        /// Authorize a user using the more secure OneTimePadded hashed data, no replay attacks possible
        /// </summary>
        /// <param name="userName">The plain text username</param>
        /// <param name="hash">The one time hash: SHA256(UTF8.GetBytes(ToBase64(SHA256(UTF8.GetBytes(password + "|" + salt))) + "|" + oneTimePad))</param>
        /// <param name="oneTimePad">The one time pad used</param>
        /// <returns>null if password is wrong or user is unknown</returns>
        public virtual Task<Authorization> SecureAuth(String userName, Byte[] hash, String oneTimePad) => NoAuth;


        /// <summary>
        /// Authorize a user using a one time use token
        /// </summary>
        /// <param name="oneTimeToken">A token, typically coming from some other web-site</param>
        /// <returns>null if the onTimeToken is wrong or unknown</returns>
        public virtual Task<Authorization> TokenAuth(String oneTimeToken) => NoAuth;


        /// <summary>
        /// Check if the user was logged in using an external session id (used for remote / single sign-out)
        /// </summary>
        /// <param name="auth">The auth of the current session (must match)</param>
        /// <param name="externalSessionId">The session representing the user that a remote service want to logout</param>
        /// <returns>True if the currently logged in user is logged in using the external session id</returns>
        public virtual bool TryLogoutTokenAuth(Authorization auth, String externalSessionId) => false;

        /// <summary>
        /// Get salt for a specific user
        /// </summary>
        /// <param name="userName">The plain text username</param>
        /// <returns>The salt, or null if the user is unknown to this authorizer</returns>
        public virtual Task<String> GetSaltAsync(String userName)
             => TaskExt.NullStringTask;

        /// <summary>
        /// If any authorization information changes (db updates, files reloaded etc), increase this counter (invalidates cached auth's)
        /// </summary>
        public abstract long ChangeCounter { get; }

        /// <summary>
        /// The required password policy (default is <see cref="SysWeaver.Auth.PasswordPolicy.Default"/>)
        /// </summary>
        public virtual PasswordPolicy PasswordPolicy { get => PasswordPolicy.Default; }






        readonly ConcurrentDictionary<String, UserData> UserData = new ConcurrentDictionary<string, UserData>(StringComparer.Ordinal);


        /// <summary>
        /// Get (and add a reference to) the shared per-user data of a user, used by <see cref="Authorization"/> so that all sessions of a user share data.
        /// The reference is released by disposing the returned object.
        /// </summary>
        /// <param name="userGuid">The user guid</param>
        /// <returns>The user data</returns>
        internal UserData GetUserData(String userGuid)
        {
            var u = UserData;
            if (u.TryGetValue(userGuid, out var userData))
            {
                //  Never resurrect a count of 0 outside the lock (it's being removed by Dispose), use the locked path instead
                for (; ; )
                {
                    var c = Interlocked.Read(ref userData.RefCount);
                    if (c <= 0)
                        break;
                    if (Interlocked.CompareExchange(ref userData.RefCount, c + 1, c) == c)
                        return userData;
                }
            }
            lock (u)
            {
                if (u.TryGetValue(userGuid, out userData))
                {
                    Interlocked.Increment(ref userData.RefCount);
                    return userData;
                }
                userData = new UserData(userGuid, u);
                u.TryAdd(userGuid, userData);
                return userData;
            }
        }



        /// <summary>
        /// A completed task with the value true, the default result of <see cref="SetLanguage(string, string)"/>.
        /// </summary>
        protected static readonly Task<bool> NoLang = Task.FromResult(true);


        /// <summary>
        /// Override to store the language when the user changes it
        /// </summary>
        /// <param name="userGuid">Guid of the user</param>
        /// <param name="languageCode">The new language code, ex: "en-US"</param>
        /// <returns>True if successful (the default implementation does nothing and returns true)</returns>
        public virtual Task<bool> SetLanguage(String userGuid, String languageCode) => NoLang;



    }


    /// <summary>
    /// Reference counted per-user data shared by all <see cref="Authorization"/> instances of the same user (within one authorizer).
    /// Removed from the authorizer when the last reference is disposed.
    /// </summary>
    sealed class UserData : IDisposable
    {
        public UserData(String userGuid, ConcurrentDictionary<String, UserData> data)
        {
            UserGuid = userGuid;
            Data = data;
        }
        readonly String UserGuid;
        readonly ConcurrentDictionary<String, UserData> Data;

        /// <summary>
        /// Number of references (<see cref="Authorization"/> instances) to this data.
        /// </summary>
        public long RefCount = 1;


        /// <summary>
        /// The user data values.
        /// </summary>
        public readonly ConcurrentDictionary<String, Object> Values = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);


        /// <summary>
        /// Release a reference, the data is removed from the authorizer when the last reference is released.
        /// </summary>
        public void Dispose()
        {
            var value = Interlocked.Decrement(ref RefCount);
            if (value != 0)
                return;
            var u = Data;
            lock (u)
            {
                if (Interlocked.Read(ref RefCount) != 0)
                    return;
                u.TryRemove(UserGuid, out var userData);
            }
        }

    }

}
