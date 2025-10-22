using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.Auth
{


    /// <summary>
    /// The auhorization for a user
    /// </summary>
    public sealed class Authorization : AuthorizationInfo, IDisposable
    {
        public override string ToString() => String.Concat(Username, " with: ", String.Join(", ", Tokens));


        public static IReadOnlyList<String> RoleDebug = GetRequiredTokens(Roles.Debug);
        public static IReadOnlyList<String> RoleAdmin = GetRequiredTokens(Roles.Admin);
        public static IReadOnlyList<String> RoleDev = GetRequiredTokens(Roles.Dev);
        public static IReadOnlyList<String> RoleOps = GetRequiredTokens(Roles.Ops);
        public static IReadOnlyList<String> RoleService = GetRequiredTokens(Roles.Service);
        public static IReadOnlyList<String> RoleDisabled = GetRequiredTokens(Roles.Disabled);
        public static IReadOnlyList<String> RoleAdminOps = GetRequiredTokens(Roles.AdminOps);
        public static IReadOnlyList<String> RoleOpsDev = GetRequiredTokens(Roles.OpsDev);


        /// <summary>
        /// The auhorizer of this user
        /// </summary>
        public readonly AuthorizerBase Auth;

        /// <summary>
        /// Some optional data that is only meaningful to the authorizer of this user 
        /// </summary>
        public readonly Object AuthContext;

        /// <summary>
        /// The change counter of the auhtorizer when this auhorization was created, if this doesn't equal to Auth.ChangeCounter a new auth will have to be performed
        /// </summary>
        public readonly long Cc;

        /// <summary>
        /// Transform a comma separated token list to a pre processed and faster representation
        /// </summary>
        /// <param name="requiredTokens">A list of comma separated tokens</param>
        /// <returns>A readonly list of curated tokens</returns>
        public static IReadOnlyList<String> GetRequiredTokens(String requiredTokens)
        {
            if (requiredTokens == null)
                return null;
            requiredTokens = requiredTokens.Trim();
            if (requiredTokens.Length <= 0)
                return AuthTools.Empty;
            if (requiredTokens == "-")
                return AuthTools.NoAuth;
            var t = requiredTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tl = t.Length;
            HashSet<String> tokens = new HashSet<string>(tl, StringComparer.Ordinal);
            for (int i = 0; i < tl; ++ i)
            {
                var tt = t[i].FastToLower();
                tokens.Add(tt);
            }
            return tokens.ToArray();
        }

        /// <summary>
        /// Transform a comma separated token list to a pre processed and faster representation
        /// </summary>
        /// <param name="requiredTokens">A list of comma separated tokens</param>
        /// <returns>A readonly list of curated tokens</returns>
        public static IReadOnlySet<String> GetRequiredTokenSet(String requiredTokens)
        {
            if (requiredTokens == null)
                return null;
            requiredTokens = requiredTokens.Trim();
            if (requiredTokens.Length <= 0)
                return AuthTools.EmptyTokens;
            if (requiredTokens == "-")
                return AuthTools.NoAuthSet;
            var t = requiredTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tl = t.Length;
            HashSet<String> tokens = new HashSet<string>(tl, StringComparer.Ordinal);
            for (int i = 0; i < tl; ++i)
            {
                var tt = t[i].FastToLower();
                tokens.Add(tt);
            }
            return tokens;
        }

        /// <summary>
        /// Validate that this user have ANY of the supplied tokens
        /// </summary>
        /// <param name="requiredTokens">A list of comma separated tokens that must exist in this users Token set</param>
        /// <returns>True if all tokens are present</returns>
        public bool IsValid(String requiredTokens = null)
        {
            if (String.IsNullOrEmpty(requiredTokens))
                return true;
            requiredTokens = requiredTokens.Trim();
            if (requiredTokens == "-")
                return false;
            var ts = Tokens;
            var t = requiredTokens.Split(',');
            var tl = t.Length;
            bool allEmpty = true;
            for (int i = 0; i < tl; ++ i)
            {
                var tt = t[i].Trim().FastToLower();
                if (tt == String.Empty)
                    continue;
                allEmpty = false;
                if (ts.Contains(tt))
                    return true;
            }
            return allEmpty;
        }

        /// <summary>
        /// Validate that this user have ANY of the supplied tokens
        /// </summary>
        /// <param name="requiredTokens">A list of tokens that must exist in this users Token set</param>
        /// <returns>True if all tokens are present</returns>
        public bool IsValid(IReadOnlyList<String> requiredTokens)
        {
            if (requiredTokens == null)
                return true;
            if (requiredTokens == AuthTools.NoAuth)
                return false;
            var tl = requiredTokens.Count;
            if (tl <= 0)
                return true;
            var ts = Tokens;
            if (ts == null)
                return true;
            for (int i = 0; i < tl; ++ i)
            {
                if (ts.Contains(requiredTokens[i])) 
                    return true;
            }
            return false;
        }


        public Authorization(AuthorizerBase auth, string username, IReadOnlySet<string> tokens, String guid, string email = null, string nickName = null, object authContext = null, String domain = null, string language = null)
            : base(username, tokens, language, domain, email, guid, nickName)
        {
            Auth = auth;
            Cc = auth.ChangeCounter;
            AuthContext = authContext;
        }
        
        /// <summary>
        /// Use this to "lock" some operation for a specific user
        /// </summary>
        public readonly SemaphoreSlim UserLock = new SemaphoreSlim(1);

        /// <summary>
        /// Request a logout of this auth
        /// </summary>
        /// <param name="reason">A reason for the logout</param>
        public void RequestLogout(String reason) => OnRequestLogout?.Invoke(reason);

        /// <summary>
        /// Raised when request logout is called
        /// </summary>
        public event Action<String> OnRequestLogout;



        public void Dispose()
        {
            Interlocked.Exchange(ref InternalUserData, null)?.Dispose();
        }



        #region User data


        volatile UserData InternalUserData;

        ConcurrentDictionary<String, Object> Values
        {
            get
            {
                var u = InternalUserData;
                if (u != null)
                    return u.Values;
                lock (this)
                {
                    u = InternalUserData;
                    if (u != null)
                        return u.Values;
                    u = Auth.GetUserData(Guid);
                    InternalUserData = u;
                }
                return u.Values;
            }
        }

        /// <summary>
        /// Get or create user data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="create">The function to call if the data wasn't found (will only be executed once in a concurrent environment)</param>
        /// <returns>The found or created value</returns>
        public T GetOrCreate<T>(String key, Func<T> create)
        {
            var v = Values;
            if (v.TryGetValue(key, out var val))
                return (T)val;
            lock (v)
            {
                if (v.TryGetValue(key, out val))
                    return (T)val;
                var vv = create();
                v[key] = vv;
                return vv;
            }
        }

        /// <summary>
        /// Try to add some user data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The value to add</param>
        /// <returns>True if the value was added to the user data</returns>
        public bool TryAdd<T>(String key, T val) => Values.TryAdd(key, val);

        /// <summary>
        /// Try to get some user data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The data (if present)</param>
        /// <returns>True if the data exists, else false</returns>
        public bool TryGet<T>(String key, out T val)
        {
            if (Values.TryGetValue(key, out var v))
            {
                val = (T)v;
                return true;
            }
            val = default;
            return false;
        }

        /// <summary>
        /// Try to remove some user data
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The removed data (if present)</param>
        /// <returns>True if the data was removed, else false</returns>
        public bool TryRemove<T>(String key, out T val)
        {
            var vals = Values;
            if (vals.TryRemove(key, out var v))
            {
                val = (T)v;
                return true;
            }
            val = default;
            return false;
        }

        /// <summary>
        /// Try to remove some user data
        /// </summary>
        /// <param name="key">The unique key for this data</param>
        /// <returns>True if the data was removed, else false</returns>
        public bool TryRemove(String key)
        {
            var vals = Values;
            if (vals.TryRemove(key, out var v))
                return true;
            return false;
        }

        /// <summary>
        /// Set some user data (add or replace)
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="key">The unique key for this data</param>
        /// <param name="val">The value to set (or add)</param>
        public void Set<T>(String key, T val)
        {
            Values[key] = val;
        }


        #endregion//User data


        /// <summary>
        /// Get the url of the logged in user image
        /// </summary>
        /// <param name="imageName">"small", "large" or a specific size, can use "" to get the base path to all images</param>
        /// <param name="rootUrl">Depends on where the link will be used, this value should "point" to the web root</param>
        /// <returns>An url to the specified image</returns>
        public String GetUserImage(String imageName = "small", String rootUrl = "../")
            => String.Concat(rootUrl, "auth/UserImages/", Guid.ToHex(), '/', imageName);

    }

}
