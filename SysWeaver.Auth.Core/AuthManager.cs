using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Auth
{

    /// <summary>
    /// Caches and maintains auth information
    /// </summary>
    public sealed class AuthManager : IDisposable
    {

        public override string ToString() => "Realm: " + Realm.ToQuoted();

        public AuthManager(AuthManagerParams p, params AuthorizerBase[] authorizers)
        {
            var a = Auths;
            var gp = GuidPrefixMap;
            foreach (var x in authorizers)
            {
                a.TryAdd(x, Interlocked.Increment(ref AuthIndex));
                if (!gp.TryAdd(x.GuidPrefix, x))
                    throw new Exception("Authorizers must have unique guid prefixes");
            }
            p = p ?? new AuthManagerParams();
            Realm = p.Realm ?? EnvInfo.AppAssemblyName;
            UpdateCommonPolicy();
            RetryAuth = new PeriodicTask(PruneCache, Math.Max(1, p.CacheDuration) * 1000);
        }


        /// <summary>
        /// Get information about a user (from it's guid)
        /// </summary>
        /// <param name="userGuid"></param>
        /// <returns></returns>
        public Task<AuthorizationInfo> FindUserFromGuid(String userGuid)
        {
            var t = userGuid.IndexOf(':');
            if (t < 0)
                return NullTaskFindUserFromGuid;
            var pre = userGuid.Substring(0, t);
            if (!GuidPrefixMap.TryGetValue(pre, out var auth))
                return NullTaskFindUserFromGuid;
            return auth.FindUserFromGuid(userGuid);
        }

        static readonly Task<AuthorizationInfo> NullTaskFindUserFromGuid = Task.FromResult((AuthorizationInfo)null);

        /// <summary>
        /// Get information about a user
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <returns></returns>
        public async Task<AuthorizationInfo> FindUser(String userName)
        {
            foreach (var author in OrderdAuths)
            {
                var u = await author.FindUser(userName).ConfigureAwait(false);
                if (u != null)
                    return u;
            }
            return null;
        }

        static readonly Tuple<Authorization, bool> NoAuthBasic = Tuple.Create((Authorization)null, true);
        static readonly Tuple<Authorization, bool> NoAuth = Tuple.Create((Authorization)null, false);

        /// <summary>
        /// Get authorization from the auth HTTP header
        /// </summary>
        /// <param name="authHeaderString">The auth header of the http request</param>
        /// <returns>The authorization information for the given user, null means unknown user or invalid password</returns>
        public async Task<Tuple<Authorization, bool>> Http(String authHeaderString)
        {
            var cache = HttpCache;
            var s = authHeaderString.Trim();
            var sp = s.IndexOf(' ');
            var key = sp >= 0 ? s.Substring(0, sp) : null;
            var isBasic = key.FastEquals("Basic");
            Authorization aa;
            if (cache.TryGetValue(s, out var auth))
            {
                aa = auth.Item1;
                if (aa == null)
                    return isBasic ? NoAuthBasic : NoAuth;
                if (aa.Cc == aa.Auth.ChangeCounter)
                    return auth;
            }
            aa = null;
            //  Parse
            if (isBasic)
            {
            //  Handling basic auth
                var p = s.Substring(sp + 1).TrimStart();
                var userPwd = Encoding.UTF8.GetString(Convert.FromBase64String(p));
                sp = userPwd.IndexOf(':');
                if (sp > 0)
                {
                    var username = userPwd.Substring(0, sp);
                    var t = await GetAuthorizerAndSalt(username).ConfigureAwait(false);
                    var a = t.Item1;
                    var hash = AuthTools.ComputeHash(userPwd.Substring(sp + 1), t.Item2);
                    if (a != null)
                        aa = await a.BasicAuth(username, hash).ConfigureAwait(false);
                    if (aa == null)
                        aa = await BasicAuth(username, hash).ConfigureAwait(false);
                }
                auth = Tuple.Create(aa, true);
            }
            if (key == "Bearer")
            {
                //  Handling Bearer auth
                var token = s.Substring(sp + 1).TrimStart();
                aa = await BearerAuth(token).ConfigureAwait(false);
                auth = Tuple.Create(aa, false);
            }
            //  Get auth
            cache[s] = auth;
            return auth;
        }



        /// <summary>
        /// Get the salt for a user
        /// </summary>
        /// <param name="username">The user name to auth</param>
        /// <returns>The salt string required to compute the password hash</returns>
        public async Task<String> GetSalt(String username)
        {
            String salt = null;
            foreach (var author in OrderdAuths)
            {
                salt = await author.GetSaltAsync(username).ConfigureAwait(false);
                if (salt != null)
                    break;
            }
            if (salt == null)
                salt = Convert.ToBase64String(SHA256.HashData(MemoryMarshal.Cast<Char, Byte>(username.AsSpan())), 0, 18);
            await TaskExt.RandomDelay().ConfigureAwait(false);
            return salt;
        }


        /// <summary>
        /// Get the salt for a user
        /// </summary>
        /// <param name="username">The user name to auth</param>
        /// <returns>The authorizer and the salt string required to compute the password hash</returns>
        public async Task<Tuple<AuthorizerBase, String>> GetAuthorizerAndSalt(String username)
        {
            String salt = null;
            AuthorizerBase a = null;
            foreach (var author in OrderdAuths)
            {
                salt = await author.GetSaltAsync(username).ConfigureAwait(false);
                if (salt != null)
                {
                    a = author;
                    break;
                }
            }
            if (salt == null)
                salt = Convert.ToBase64String(SHA256.HashData(MemoryMarshal.Cast<Char, Byte>(username.AsSpan())), 0, 18);
            await TaskExt.RandomDelay().ConfigureAwait(false);
            return Tuple.Create(a, salt);
        }


        /// <summary>
        /// Get authorization from a username and password hash
        /// </summary>
        /// <param name="username">The user name to auth</param>
        /// <param name="hash">The base64 encoded one time hash, should be: 
        /// serverHash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', password, userSalt))));
        /// hash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', serverHash, oneTimePad))));
        /// </param>
        /// <param name="oneTimePad">The one time pad used</param>
        /// <returns>The authorization information for the given user, null means unknown user or invalid password</returns>
        public Task<Authorization> UserHash(String username, String hash, String oneTimePad) => GetAuth(username, Convert.FromBase64String(hash), oneTimePad);

 
        public readonly String Realm = "SysWeaver";


        public void Dispose()
        {
            Interlocked.Exchange(ref RetryAuth, null)?.Dispose();
        }

        async Task<Authorization> BasicAuth(String username, Byte[] hash)
        {
            foreach (var author in OrderdAuths)
            {
                var auth = await author.BasicAuth(username, hash).ConfigureAwait(false);
                if (auth != null)
                {
#if DEBUG
                    if (auth.Auth != author)
                        throw new Exception("Invalid auth!");
#endif//DEBUG
                    return auth;
                }
            }
            return null;
        }

        async Task<Authorization> BearerAuth(String token)
        {
            foreach (var author in OrderdAuths)
            {
                var auth = await author.BearerAuth(token).ConfigureAwait(false);
                if (auth != null)
                {
#if DEBUG
                    if (auth.Auth != author)
                        throw new Exception("Invalid auth!");
#endif//DEBUG
                    return auth;
                }
            }
            return null;
        }

        public async Task<Authorization> TokenAuth(String oneTimeToken)
        {
            if (String.IsNullOrEmpty(oneTimeToken))
                return null;
            foreach (var author in OrderdAuths)
            {
                var auth = await author.TokenAuth(oneTimeToken).ConfigureAwait(false);
                if (auth != null)
                {
#if DEBUG
                    if (auth.Auth != author)
                        throw new Exception("Invalid auth!");
#endif//DEBUG
                    return auth;
                }
            }
            return null;
        }


        async Task<Authorization> GetAuth(String username, Byte[] hash, String oneTimePad)
        {
            foreach (var author in OrderdAuths)
            {
                var auth = await author.SecureAuth(username, hash, oneTimePad).ConfigureAwait(false);
                if (auth != null)
                {
#if DEBUG
                    if (auth.Auth != author)
                        throw new Exception("Invalid auth!");
#endif//DEBUG
                    return auth;
                }
            }
            return null;
        }

        bool PruneCache()
        {
            var cache = Cache;
            var nulls = cache.Where(x => x.Value == null).Select(x => x.Key).ToList();
            foreach (var n in nulls)
                cache.TryRemove(n, out var _);
            return true;
        }

        PeriodicTask RetryAuth;


        public bool AddAuth(AuthorizerBase auth)
        {
            if (!Auths.TryAdd(auth, Interlocked.Increment(ref AuthIndex)))
                return false;
            if (!GuidPrefixMap.TryAdd(auth.GuidPrefix, auth))
                throw new Exception("Authorizers must have unique guid prefixes");
            UpdateCommonPolicy();
            return true;
        }

        public bool RemoveAuth(AuthorizerBase auth)
        {
            if (!Auths.TryRemove(auth, out var _))
                return false;
            GuidPrefixMap.TryRemove(auth.GuidPrefix, out var _);
            UpdateCommonPolicy();
            return true;
        }

        void UpdateCommonPolicy()
        {
            var a = Auths;
            lock (a)
            {
                OrderdAuths = a.OrderBy(x => x.Value).Select(x => x.Key).ToArray();
                Interlocked.Exchange(ref InternalCommonPasswordPolicy, PasswordPolicyExt.Min(a.Select(x => x.Key.PasswordPolicy)));
            }
        }

        /// <summary>
        /// All authorizers
        /// </summary>
        public IEnumerable<AuthorizerBase> Authorizers => OrderdAuths;

        long AuthIndex;

        AuthorizerBase[] OrderdAuths;

        readonly ConcurrentDictionary<AuthorizerBase, long> Auths = new ConcurrentDictionary<AuthorizerBase, long>();

        readonly ConcurrentDictionary<String, AuthorizerBase> GuidPrefixMap = new ConcurrentDictionary<String, AuthorizerBase>(StringComparer.Ordinal);

        readonly ConcurrentDictionary<String, Authorization> Cache = new (StringComparer.Ordinal);

        readonly ConcurrentDictionary<String, Tuple<Authorization, bool>> HttpCache = new (StringComparer.Ordinal);


        volatile PasswordPolicy InternalCommonPasswordPolicy = new PasswordPolicy();

        public PasswordPolicy CommonPasswordPolicy => InternalCommonPasswordPolicy;


        /// <summary>
        /// Get the password policy for a user (return the policy for the authorizer of the user, or the common policy if the user is unknown).
        /// </summary>
        /// <param name="username">The user name to get password policy for</param>
        /// <returns>The password policy</returns>
        public async Task<PasswordPolicy> GetPasswordPolicy(String username)
        {
            foreach (var author in OrderdAuths)
            {
                if (await author.GetSaltAsync(username).ConfigureAwait(false) != null)
                    return author.PasswordPolicy;
            }
            return CommonPasswordPolicy;
        }


    }


}
