using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.Auth
{

    /// <summary>
    /// The authorization of a logged in user: the user information, the authorizer that validated the user and per-user data storage.
    /// </summary>
    /// <remarks>
    /// Authorizations are typically shared by many sessions / requests and must be treated as immutable (except for the user data).
    /// Disposing releases the reference to the shared user data.
    /// </remarks>
    public sealed class Authorization : AuthorizationInfo, IDisposable
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(Username, " with: ", String.Join(", ", Tokens));


        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.Debug"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleDebug = GetRequiredTokens(Roles.Debug);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.Admin"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleAdmin = GetRequiredTokens(Roles.Admin);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.Dev"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleDev = GetRequiredTokens(Roles.Dev);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.Ops"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleOps = GetRequiredTokens(Roles.Ops);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.Service"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleService = GetRequiredTokens(Roles.Service);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.Disabled"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleDisabled = GetRequiredTokens(Roles.Disabled);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.AdminOps"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleAdminOps = GetRequiredTokens(Roles.AdminOps);
        /// <summary>
        /// Pre-processed tokens of <see cref="Roles.OpsDev"/>, for use with <see cref="AuthExt.IsValid(Authorization, IReadOnlyList{string})"/>.
        /// </summary>
        public static readonly IReadOnlyList<String> RoleOpsDev = GetRequiredTokens(Roles.OpsDev);


        /// <summary>
        /// The authorizer of this user
        /// </summary>
        public readonly AuthorizerBase Auth;

        /// <summary>
        /// Some optional data that is only meaningful to the authorizer of this user 
        /// </summary>
        public readonly Object AuthContext;

        /// <summary>
        /// The change counter of the authorizer when this authorization was created, if this doesn't equal to <see cref="AuthorizerBase.ChangeCounter"/> of <see cref="Auth"/> a new auth will have to be performed
        /// </summary>
        public readonly long Cc;

        /// <summary>
        /// Transform a comma separated token list to a pre processed and faster representation
        /// </summary>
        /// <param name="requiredTokens">A list of comma separated tokens (case insensitive, white spaces are trimmed)</param>
        /// <returns>A readonly list of unique lower case tokens.
        /// A requirement with a service token ("service" or "service_&lt;scope&gt;") also gets the <see cref="ServiceImpliedTokens"/> ("debug" and "dev", appended if missing).
        /// Null if <paramref name="requiredTokens"/> is null (no auth required), <see cref="AuthTools.Empty"/> if it's empty (any logged in user) and <see cref="AuthTools.NoAuth"/> if it's "-" (no one).</returns>
        public static IReadOnlyList<String> GetRequiredTokens(String requiredTokens)
        {
            if (requiredTokens == null)
                return null;
            var s = requiredTokens.AsSpan().Trim();
            if (s.Length <= 0)
                return AuthTools.Empty;
            if ((s.Length == 1) && (s[0] == '-'))
                return AuthTools.NoAuth;
            return AddServiceImplied(GetUniqueTokens(requiredTokens));
        }

        /// <summary>
        /// Append the <see cref="ServiceImpliedTokens"/> (if missing) to unique lower case tokens that contains a service token
        /// </summary>
        static String[] AddServiceImplied(String[] tokens)
        {
            bool isService = false;
            foreach (var t in tokens)
                if (IsServiceToken(t))
                {
                    isService = true;
                    break;
                }
            if (!isService)
                return tokens;
            var implied = ServiceImpliedTokens;
            int missing = 0;
            foreach (var x in implied)
                if (Array.IndexOf(tokens, x) < 0)
                    ++missing;
            if (missing == 0)
                return tokens;
            var r = new String[tokens.Length + missing];
            tokens.CopyTo(r, 0);
            int n = tokens.Length;
            foreach (var x in implied)
                if (Array.IndexOf(tokens, x) < 0)
                    r[n++] = x;
            return r;
        }

        /// <summary>
        /// Above this many (possible) tokens a hash set is used to find duplicates, else a linear search
        /// </summary>
        const int MaxLinearUniqueSearch = 8;

        /// <summary>
        /// Split a comma separated token list into unique lower case tokens, white space is trimmed and empty tokens are removed.
        /// Same result as Split(',', RemoveEmptyEntries | TrimEntries), FastToLower on every token and a HashSet (first occurrence order), but only the result is allocated.
        /// </summary>
        /// <param name="requiredTokens">The comma separated tokens</param>
        /// <returns>The unique tokens, in the order they first occur</returns>
        static String[] GetUniqueTokens(String requiredTokens)
        {
            var s = requiredTokens.AsSpan();
            // The number of tokens is at most the number of separators + 1
            var t = new String[s.Count(',') + 1];
            var seen = t.Length > MaxLinearUniqueSearch ? new HashSet<String>(t.Length, StringComparer.Ordinal) : null;
            int n = 0;
            int start = 0;
            for (; ; )
            {
                var rest = s.Slice(start);
                var e = rest.IndexOf(',');
                var end = e < 0 ? rest.Length : e;
                int a = 0;
                while ((a < end) && Char.IsWhiteSpace(rest[a]))
                    ++a;
                while ((end > a) && Char.IsWhiteSpace(rest[end - 1]))
                    --end;
                if (end > a)
                {
                    var token = LowerToken(requiredTokens, start + a, end - a);
                    if ((seen == null) ? (Array.IndexOf(t, token, 0, n) < 0) : seen.Add(token))
                        t[n++] = token;
                }
                if (e < 0)
                    break;
                start += e + 1;
            }
            if (n == t.Length)
                return t;
            if (n <= 0)
                return [];
            return t.AsSpan(0, n).ToArray();
        }

        /// <summary>
        /// Lower case a part of a string, exactly as Substring(start, length).FastToLower() (same as ToLowerInvariant):
        /// the string itself if the whole string is used and no char changes
        /// </summary>
        static String LowerToken(String s, int start, int length)
        {
            if (length == s.Length)
                return s.FastToLower();
            //  Lower cased while copying (one allocation instead of a substring and a lower cased copy).
            //  The lambda is static (cached by the compiler), a static delegate field could be null when static fields that parse tokens are initialized
            return String.Create(length, (s, start), static (dst, state) =>
            {
                var src = state.s.AsSpan(state.start, dst.Length);
                //  ASCII (the common case) using SIMD, the rest (if any) using the invariant culture
                if (Ascii.ToLower(src, dst, out var done) != System.Buffers.OperationStatus.Done)
                    src[done..].ToLowerInvariant(dst[done..]);
            });
        }

        /// <summary>
        /// Transform a comma separated token list to a pre processed and faster representation
        /// </summary>
        /// <param name="requiredTokens">A list of comma separated tokens (case insensitive, white spaces are trimmed)</param>
        /// <returns>A set of lower case tokens.
        /// Null if <paramref name="requiredTokens"/> is null, <see cref="AuthTools.EmptyTokens"/> if it's empty and <see cref="AuthTools.NoAuthSet"/> if it's "-".</returns>
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
        /// Create an authorization, the current <see cref="AuthorizerBase.ChangeCounter"/> of <paramref name="auth"/> is captured.
        /// </summary>
        /// <param name="auth">The authorizer that validated the user</param>
        /// <param name="username">The user name</param>
        /// <param name="tokens">The security tokens (lower case), null means no tokens</param>
        /// <param name="weakMethod">True if a weak auth method was used (basic auth, bearer token etc)</param>
        /// <param name="guid">The user guid, must start with the authorizer's <see cref="AuthorizerBase.GuidPrefix"/></param>
        /// <param name="email">Optional email</param>
        /// <param name="nickName">Optional nick name, null uses the user name</param>
        /// <param name="authContext">Optional authorizer specific data</param>
        /// <param name="domain">Optional application specific domain</param>
        /// <param name="language">Optional preferred language</param>
        /// <exception cref="ArgumentNullException"><paramref name="auth"/>, <paramref name="username"/> or <paramref name="guid"/> is null (DEBUG builds only).</exception>
        /// <exception cref="ArgumentException">A value is too long or the guid contains non ASCII chars.</exception>
        public Authorization(AuthorizerBase auth, string username, IReadOnlySet<string> tokens, bool weakMethod, String guid, string email = null, string nickName = null, object authContext = null, String domain = null, string language = null)
            : base(username, tokens, language, domain, email, guid, nickName)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(auth);
#endif//DEBUG
            WeakMethod = weakMethod;
            Auth = auth;
            Cc = auth.ChangeCounter;
            AuthContext = authContext;
        }

        /// <summary>
        /// The lower case "service" token. Each service token ("service" or "service_&lt;scope&gt;") is a token of its own: a "service" requirement is only fulfilled by "service".
        /// A requirement with a service token is also fulfilled by the "debug" and "dev" tokens (see <see cref="GetRequiredTokens(string)"/>).
        /// </summary>
        public const String ServiceToken = "service";

        /// <summary>
        /// The start of a scoped service token ("service_&lt;scope&gt;", ex: an API key that may only call some services)
        /// </summary>
        public const String ServiceScopePrefix = "service_";

        /// <summary>
        /// The lower case tokens that are added to every requirement with a service token ("service" or "service_&lt;scope&gt;"): developers may call any service API
        /// </summary>
        public static IReadOnlyList<String> ServiceImpliedTokens => ImpliedTokens.Tokens;

        /// <summary>
        /// Holds <see cref="ServiceImpliedTokens"/> (initialized before the static fields of Authorization that parse requirements)
        /// </summary>
        static class ImpliedTokens
        {
            public static readonly String[] Tokens = ["debug", "dev"];
        }

        /// <summary>
        /// Check if a token is a service token ("service" or "service_&lt;scope&gt;"), case insensitive (same result as lower casing the token with ToLowerInvariant first)
        /// </summary>
        /// <param name="token">The token</param>
        /// <returns>True for a service token</returns>
        public static bool IsServiceToken(ReadOnlySpan<Char> token)
        {
            var l = ServiceToken.Length;
            if ((token.Length < l) || ((token.Length > l) && (token[l] != '_')))
                return false;
            //  Per char ToLowerInvariant: the same as the lower cased token (FastToLower), ex: 'ſ' isn't 's'
            for (int i = 0; i < l; ++i)
            {
                if (Char.ToLowerInvariant(token[i]) != ServiceToken[i])
                    return false;
            }
            return true;
        }


        /// <summary>
        /// If true, a weak auth method was used (Basic auth, Bearer token etc).
        /// If false, a proper login request was used
        /// </summary>
        public readonly bool WeakMethod;

        /// <summary>
        /// Optional: the client addresses (networks) this authorization may be used from, null (the default) means any address.
        /// Checked by the http server on every request that requires auth (ex: an API key restricted to some servers).
        /// </summary>
        public IReadOnlyList<System.Net.IPNetwork> AllowedNetworks { get; init; }

        /// <summary>
        /// True for an API key: it only fulfills requirements that name one of its tokens (ex: "Service"),
        /// never the "any logged in user" requirement (an empty token list), see <see cref="AuthExt.IsValid(Authorization, string)"/>.
        /// </summary>
        public bool IsApiKey { get; init; }

        /// <summary>
        /// True if this authorization may be used from a client address (see <see cref="AllowedNetworks"/>)
        /// </summary>
        /// <param name="address">The client address as text, ex: "10.0.0.5"</param>
        /// <returns>True if allowed (always true if <see cref="AllowedNetworks"/> is null)</returns>
        public bool IsAddressAllowed(String address)
        {
            var n = AllowedNetworks;
            if (n == null)
                return true;
            if (!System.Net.IPAddress.TryParse(address, out var ip))
                return false;
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();
            foreach (var x in n)
                if (x.Contains(ip))
                    return true;
            return false;
        }

        /// <summary>
        /// Parse a comma (or space / semicolon) separated list of addresses and networks, ex: "10.0.0.5, 192.168.1.0/24, 2001:db8::/32".
        /// </summary>
        /// <param name="list">The list, null or empty returns null (any address)</param>
        /// <returns>The networks, or null for any address</returns>
        /// <exception cref="ArgumentException">An entry isn't a valid address or network</exception>
        public static IReadOnlyList<System.Net.IPNetwork> ParseNetworks(String list)
        {
            if (String.IsNullOrWhiteSpace(list))
                return null;
            var r = new List<System.Net.IPNetwork>();
            foreach (var e in list.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (e.Contains('/'))
                {
                    if (!System.Net.IPNetwork.TryParse(e, out var net))
                        throw new ArgumentException("Invalid network " + e.ToQuoted(), nameof(list));
                    r.Add(net);
                    continue;
                }
                if (!System.Net.IPAddress.TryParse(e, out var ip))
                    throw new ArgumentException("Invalid address " + e.ToQuoted(), nameof(list));
                if (ip.IsIPv4MappedToIPv6)
                    ip = ip.MapToIPv4();
                r.Add(new System.Net.IPNetwork(ip, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
            }
            return r.Count == 0 ? null : r;
        }

        
        /// <summary>
        /// Use this to "lock" some operation for a specific user.
        /// Note: this is per <see cref="Authorization"/> instance, not shared between different instances of the same user.
        /// </summary>
        public readonly SemaphoreSlim UserLock = new SemaphoreSlim(1);

        /// <summary>
        /// Request a logout of this auth, raises <see cref="OnRequestLogout"/> (the sessions using this auth are expected to log out).
        /// </summary>
        /// <param name="reason">A reason for the logout</param>
        public void RequestLogout(String reason) => OnRequestLogout?.Invoke(reason);

        /// <summary>
        /// Raised when request logout is called
        /// </summary>
        public event Action<String> OnRequestLogout;



        /// <summary>
        /// Release the reference to the shared user data.
        /// Using the user data methods after this acquires a new reference (released by calling dispose again).
        /// </summary>
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
        /// Get or create user data.
        /// User data is shared between all <see cref="Authorization"/> instances of the same user (by guid) from the same authorizer.
        /// An instance acquires a reference to the shared data on the first use of any user data method, the data is dropped when all references are released (see <see cref="Dispose"/>).
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
        /// <exception cref="InvalidCastException">The data exists but isn't a <typeparamref name="T"/>.</exception>
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
        /// <remarks>The url is "[rootUrl]auth/UserImages/[hex encoded guid]/[imageName]".</remarks>
        /// <param name="rootUrl">Depends on where the link will be used, this value should "point" to the web root</param>
        /// <returns>An url to the specified image</returns>
        public String GetUserImage(String imageName = "small", String rootUrl = "../")
            => String.Concat(rootUrl, "auth/UserImages/", Guid.ToHex(), '/', imageName);

    }


    /// <summary>
    /// Extensions for checking if an <see cref="Authorization"/> fulfills a token requirement.
    /// </summary>
    public static class AuthExt
    {
        /// <summary>
        /// Validate that this user have ANY of the supplied tokens.
        /// </summary>
        /// <param name="auth">The authorization, null means not logged in</param>
        /// <param name="requiredTokens">A list of comma separated tokens (case insensitive).
        /// Null means no auth required (always true), empty means that any logged in user except an API key (<see cref="Authorization.IsApiKey"/>) is valid and "-" means that no one is valid</param>
        /// <returns>True if the requirement is fulfilled, i.e. at least one of the tokens is present (each service token is a token of its own, a requirement with a service token is also fulfilled by "debug" and "dev", see <see cref="Authorization.ServiceImpliedTokens"/>)</returns>
        public static bool IsValid(this Authorization auth, String requiredTokens = null)
        {
            if (requiredTokens == null)
                return true;
            var s = requiredTokens.AsSpan();
            var trimmed = s.Trim();
            if ((trimmed.Length == 1) && (trimmed[0] == '-'))
                return false;
            if (auth == null)
                return false;
            //  The tokens are parsed in place (no split array) and looked up with spans (no allocation)
            IReadOnlySet<String> ts = null;
            //  True if the requirement has a service token ("debug" and "dev" are then also valid, same as GetRequiredTokens)
            bool isService = false;
            int start = 0;
            for (; ; )
            {
                var rest = s.Slice(start);
                var e = rest.IndexOf(',');
                var end = e < 0 ? rest.Length : e;
                int a = 0;
                while ((a < end) && Char.IsWhiteSpace(rest[a]))
                    ++a;
                while ((end > a) && Char.IsWhiteSpace(rest[end - 1]))
                    --end;
                if (end > a)
                {
                    //  No tokens means any logged in user, so the user tokens are only needed for the first token
                    if (ts == null)
                    {
                        ts = auth.Tokens;
                        if (ts == null)
                            return false;
                    }
                    //  Lower cased on the stack and looked up with a span (the same chars as LowerToken, no allocation for the frozen sets)
                    //  A single token (the whole string) uses the string overload (an already lower case token is looked up as is)
                    var rt = s.Slice(start + a, end - a);
                    if ((end - a) == requiredTokens.Length ? ts.ContainsLower(requiredTokens) : ts.ContainsLower(rt))
                        return true;
                    if (!isService)
                        isService = Authorization.IsServiceToken(rt);
                }
                if (e < 0)
                {
                    //  No tokens: any logged in user, except API keys (they only get access to what requires one of their tokens)
                    if (ts == null)
                        return !auth.IsApiKey;
                    if (isService)
                        foreach (var x in Authorization.ServiceImpliedTokens)
                            if (ts.Contains(x))
                                return true;
                    return false;
                }
                start += e + 1;
            }
        }

        /// <summary>
        /// Validate that this user have ANY of the supplied tokens.
        /// </summary>
        /// <param name="auth">The authorization, null means not logged in</param>
        /// <param name="requiredTokens">Tokens as returned by <see cref="Authorization.GetRequiredTokens(string)"/> (must be all lower case).
        /// Null means no auth required (always true), empty means that any logged in user except an API key (<see cref="Authorization.IsApiKey"/>) is valid and <see cref="AuthTools.NoAuth"/> (reference equality) means that no one is valid</param>
        /// <returns>True if the requirement is fulfilled, i.e. at least one of the tokens is present (each service token is a token of its own, a requirement with a service token is also fulfilled by "debug" and "dev", see <see cref="Authorization.ServiceImpliedTokens"/>)</returns>
        public static bool IsValid(this Authorization auth, IReadOnlyList<String> requiredTokens)
        {
            if (requiredTokens == null)
                return true;
            if (requiredTokens == AuthTools.NoAuth)
                return false;
            if (auth == null)
                return false;
            var tl = requiredTokens.Count;
            //  No tokens: any logged in user, except API keys (they only get access to what requires one of their tokens)
            if (tl <= 0)
                return !auth.IsApiKey;
            var ts = auth.Tokens;
            if (ts == null)
                return false;
            for (int i = 0; i < tl; ++i)
            {
                var rt = requiredTokens[i];
                if (ts.Contains(rt))
                    return true;
            }
            return false;
        }


    }

}
