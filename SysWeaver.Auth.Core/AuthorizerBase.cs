using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Interface for a class that can validate a user
    /// </summary>
    public abstract class AuthorizerBase
    {
        /// <summary>
        /// A unique name of this authorizer
        /// </summary>
        public abstract String Name { get; }

        /// <summary>
        /// The unqiue guid prefix
        /// </summary>
        public abstract String GuidPrefix { get;  }

        /// <summary>
        /// Return this for failed auths
        /// </summary>
        protected static readonly Task<Authorization> NoAuth = Task.FromResult((Authorization)null);

        /// <summary>
        /// Get information about a user (from it's guid)
        /// </summary>
        /// <param name="userGuid"></param>
        /// <returns></returns>
        public abstract Task<AuthorizationInfo> FindUserFromGuid(String userGuid);

        /// <summary>
        /// Get information about a user
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <returns></returns>
        public abstract Task<AuthorizationInfo> FindUser(String userName);

        /// <summary>
        /// Authorize a user from the Http header using the basic auth schema (plain text password or hash have been sent, can use replay attacks etc)
        /// </summary>
        /// <param name="userName">The plain text username</param>
        /// <param name="hash">The password hash: SHA256(UTF8.GetBytes("userName.FastLower()|password|suffix|salt")) where suffix is the Authorize.HashSuffix</param>
        /// <returns>null if password is wrong or user is unknown</returns>
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
        /// <param name="hash">The password hash: SHA256(UTF8.GetBytes(ToBase64(SHA256(UTF8.GetBytes("userName.FastLower()|password|suffix|salt"))) | oneTimePad) where suffix is the Authorize.HashSuffix</param>
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
        /// Check if the user was logged in using an exeternal session id
        /// </summary>
        /// <param name="auth">The auth of the current session (must match)</param>
        /// <param name="externalSessionId">The session representing the user that a remove service want to logout</param>
        /// <returns>True if the currently logged in user is logged in using the external session id</returns>
        public virtual bool TryLogoutTokenAuth(Authorization auth, String externalSessionId) => false;

        /// <summary>
        /// Get salt for a specific user
        /// </summary>
        /// <param name="userName">The plain text username</param>
        /// <returns>The salt and user guid, or null if the user is unknown Tuple.Create(salt, userGuid)</returns>
        public abstract Task<String> GetSaltAsync(String userName);


        /// <summary>
        /// If any authorization information changes (db updates, files reloaded etc), increase this counter (invalidates cached auth's)
        /// </summary>
        public abstract long ChangeCounter { get; }

        /// <summary>
        /// The required password policy 
        /// </summary>
        public abstract PasswordPolicy PasswordPolicy { get; }






        readonly ConcurrentDictionary<String, UserData> UserData = new ConcurrentDictionary<string, UserData>(StringComparer.Ordinal);


        internal UserData GetUserData(String userGuid)
        {
            var u = UserData;
            if (u.TryGetValue(userGuid, out var userData))
            {
                Interlocked.Increment(ref userData.RefCount);
                return userData;
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



        protected static readonly Task<bool> NoLang = Task.FromResult(true);


        /// <summary>
        /// Override to store language upon user change
        /// </summary>
        /// <param name="userGuid">Guid of the user</param>
        /// <param name="languageCode"></param>
        /// <returns></returns>
        public virtual Task<bool> SetLanguage(String userGuid, String languageCode) => NoLang;



    }


    sealed class UserData : IDisposable
    {
        public UserData(String userGuid, ConcurrentDictionary<String, UserData> data)
        {
            UserGuid = userGuid;
            Data = data;
        }
        readonly String UserGuid;
        readonly ConcurrentDictionary<String, UserData> Data;

        public long RefCount = 1;


        public readonly ConcurrentDictionary<String, Object> Values = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);


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
