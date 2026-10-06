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
    /// Validates users across any number of <see cref="AuthorizerBase"/> instances (asked in the order they were added), and caches HTTP Authorization header results.
    /// </summary>
    /// <remarks>
    /// Typically created by the AuthManagerService and used by the HTTP server for every request with an Authorization header, and for login requests.
    /// Cached header results are re-validated when the owning authorizer's <see cref="AuthorizerBase.ChangeCounter"/> changes.
    /// Thread safe.
    /// </remarks>
    public sealed class AuthManager : IDisposable
    {

        /// <inheritdoc/>
        public override string ToString() => "Realm: " + Realm.ToQuoted();

        /// <summary>
        /// Create an auth manager.
        /// </summary>
        /// <param name="p">Parameters, null uses the defaults.</param>
        /// <param name="authorizers">The initial authorizers, in priority order.</param>
        /// <exception cref="Exception">Two authorizers have the same <see cref="AuthorizerBase.GuidPrefix"/>.</exception>
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
        /// Get information about a user (from it's guid).
        /// The authorizer is selected using the guid prefix (the text before the first ':').
        /// </summary>
        /// <param name="userGuid">The user guid, ex: "SI:abc..."</param>
        /// <returns>The user information or null if the guid is unknown</returns>
        public Task<AuthorizationInfo> FindUserFromGuid(String userGuid)
        {
            var t = userGuid.IndexOf(':');
            if (t < 0)
                return TaskExt<AuthorizationInfo>.NullTask;
            var pre = userGuid.Substring(0, t);
            if (!GuidPrefixMap.TryGetValue(pre, out var auth))
                return TaskExt<AuthorizationInfo>.NullTask;
            return auth.FindUserFromGuid(userGuid);
        }


        /// <summary>
        /// Get information about a user, asking all authorizers in order.
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <returns>The user information from the first authorizer that knows the user, or null</returns>
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
        /// Get authorization from the Authorization HTTP header.
        /// Supports the "Basic" (user:password, base64 encoded) and "Bearer" / "*key" (token) schemes.
        /// Results (including failures) are cached using the full header value as key.
        /// </summary>
        /// <param name="authHeaderString">The Authorization header of the http request</param>
        /// <returns>Item1 is the authorization for the given user, null means unknown user or invalid password / token.
        /// Item2 is true if the basic scheme was used (so the caller can request basic credentials again).
        /// The tuple itself is null for unsupported schemes.</returns>
        /// <exception cref="FormatException">The basic credentials are not valid base64.</exception>
        public async Task<Tuple<Authorization, bool>> Http(String authHeaderString)
        {
            var cache = HttpCache;
            var s = authHeaderString.Trim();
            var key = s.SplitFirst(' ', out var p, false, true);
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
                var userPwd = Encoding.UTF8.GetString(Convert.FromBase64String(p));
                var sp = userPwd.IndexOf(':');
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
            if (key.FastEquals("Bearer") || key.FastEquals("*key"))
            {
                //  Handling Bearer auth
                aa = await BearerAuth(p).ConfigureAwait(false);
                auth = Tuple.Create(aa, false);
            }
            //  Get auth
            cache[s] = auth;
            return auth;
        }



        /// <summary>
        /// Get the salt for a user (from the first authorizer that knows the user).
        /// For unknown users a deterministic fake salt is returned, and a random delay is always added, to make user enumeration harder.
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
        /// Get the salt for a user and the authorizer that knows the user.
        /// For unknown users a deterministic fake salt is returned, and a random delay is always added, to make user enumeration harder.
        /// </summary>
        /// <param name="username">The user name to auth</param>
        /// <returns>The authorizer (null if no authorizer knows the user) and the salt string required to compute the password hash</returns>
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
        /// Get authorization from a username and a one time password hash (the secure login method, prevents replay attacks), asking all authorizers in order.
        /// </summary>
        /// <param name="username">The user name to auth</param>
        /// <param name="hash">The base64 encoded one time hash, should be: 
        /// serverHash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', password, userSalt))));
        /// hash = Convert.ToBase64(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('|', serverHash, oneTimePad))));
        /// </param>
        /// <param name="oneTimePad">The one time pad used</param>
        /// <returns>The authorization information for the given user, null means unknown user or invalid password</returns>
        /// <exception cref="FormatException"><paramref name="hash"/> isn't valid base64.</exception>
        public Task<Authorization> UserHash(String username, String hash, String oneTimePad) => GetAuth(username, Convert.FromBase64String(hash), oneTimePad);

 
        /// <summary>
        /// The realm, used in the "WWW-Authenticate" response header for basic auth.
        /// Defaults to the entry assembly name if not set in the parameters.
        /// </summary>
        public readonly String Realm = "SysWeaver";


        /// <summary>
        /// Stop the periodic cache pruning.
        /// </summary>
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

        /// <summary>
        /// Get authorization from a one time token (typically issued by some other site), asking all authorizers in order.
        /// </summary>
        /// <param name="oneTimeToken">The token</param>
        /// <returns>The authorization, or null if the token is null, empty or not accepted by any authorizer</returns>
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


        /// <summary>
        /// Add an authorizer (last in priority order).
        /// </summary>
        /// <param name="auth">The authorizer to add</param>
        /// <returns>True if added, false if it was already added</returns>
        /// <exception cref="Exception">Another authorizer have the same <see cref="AuthorizerBase.GuidPrefix"/>.</exception>
        public bool AddAuth(AuthorizerBase auth)
        {
            if (!Auths.TryAdd(auth, Interlocked.Increment(ref AuthIndex)))
                return false;
            if (!GuidPrefixMap.TryAdd(auth.GuidPrefix, auth))
                throw new Exception("Authorizers must have unique guid prefixes");
            UpdateCommonPolicy();
            return true;
        }

        /// <summary>
        /// Remove an authorizer.
        /// Note that already cached Authorization header results are not invalidated.
        /// </summary>
        /// <param name="auth">The authorizer to remove</param>
        /// <returns>True if removed, false if it wasn't added</returns>
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
        /// All authorizers, in priority order
        /// </summary>
        public IEnumerable<AuthorizerBase> Authorizers => OrderdAuths;

        long AuthIndex;

        AuthorizerBase[] OrderdAuths;

        readonly ConcurrentDictionary<AuthorizerBase, long> Auths = new ConcurrentDictionary<AuthorizerBase, long>();

        readonly ConcurrentDictionary<String, AuthorizerBase> GuidPrefixMap = new ConcurrentDictionary<String, AuthorizerBase>(StringComparer.Ordinal);

        readonly ConcurrentDictionary<String, Authorization> Cache = new (StringComparer.Ordinal);

        readonly ConcurrentDictionary<String, Tuple<Authorization, bool>> HttpCache = new (StringComparer.Ordinal);


        volatile PasswordPolicy InternalCommonPasswordPolicy = new PasswordPolicy();

        /// <summary>
        /// The least restrictive combination of the password policies of all authorizers (see <see cref="PasswordPolicyExt.Min(IEnumerable{PasswordPolicy})"/>).
        /// </summary>
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
