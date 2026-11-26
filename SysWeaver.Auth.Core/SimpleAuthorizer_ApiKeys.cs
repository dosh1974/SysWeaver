using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.MicroService;

namespace SysWeaver.Auth
{
    /// <summary>
    /// API keys: stored encrypted in <see cref="KeyValueStore.AllApp"/> (see <see cref="DataProtection"/>, the secret is stored outside of the key list).
    /// Every key has its own tokens (at least one, each "service" or "service_&lt;scope&gt;", see <see cref="NormalizeApiKeyTokens"/>)
    /// and an optional list of allowed client addresses / networks.
    /// An API key only gets access to what requires one of its tokens (<see cref="Authorization.IsApiKey"/>).
    /// </summary>
    public sealed partial class SimpleAuthorizer
    {
        /// <summary>
        /// The key value store key of the encrypted API keys, one line per key: "name:random:encryptedKey:tokens:addresses"
        /// (random and encryptedKey are base64, tokens is a comma separated list, addresses is an optional comma separated list of addresses / networks)
        /// </summary>
        const String ApiKeyStoreKey = "SimpleAuthorizer.ApiKeys";

        /// <summary>
        /// The kind of protected value (see <see cref="DataProtection"/>)
        /// </summary>
        const String ApiKeyKind = "apikey";

        const String ApiKeyPath = "simpleAuth/{0}";
        const String ApiKeyAuditGroup = "api-key";

        /// <summary>
        /// The auth of the management methods in the code (disabled), the effective auth is set at run time (<see cref="MethodAuths"/>)
        /// </summary>
        const String ManagementAuthDisabled = Roles.Disabled;

        /// <summary>
        /// The token every API key token is, or starts with (followed by "_" and a scope)
        /// </summary>
        public const String ApiKeyToken = "service";

        /// <summary>
        /// The maximum length of an API key token
        /// </summary>
        public const int MaxApiKeyTokenLength = 64;

        /// <summary>
        /// The tokens of a new API key when none are given (<see cref="SimpleAuthorizerParams.ApiKeyAuth"/>, normalized), null if API keys are disabled
        /// </summary>
        readonly String ApiKeyDefaultTokens;

        /// <summary>
        /// Serializes the changes of the stored API keys and users (and <see cref="UpdateUsers"/>)
        /// </summary>
        readonly Object ApiKeyLock = new object();

        /// <summary>
        /// A stored API key
        /// </summary>
        sealed class ApiKeyRecord
        {
            public String Name;
            public String Key;
            public String Tokens;
            public String Addresses;
        }

        /// <summary>
        /// Runtime auth overrides for the API key and user management methods (<see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/>,
        /// <see cref="Roles.Admin"/> for the users), the methods of a disabled feature stay disabled.
        /// </summary>
        public IReadOnlyDictionary<String, String> MethodAuths { get; init; }

        IReadOnlyDictionary<String, String> BuildMethodAuths()
        {
            var d = new Dictionary<String, String>(StringComparer.Ordinal);
            if (ApiKeyDefaultTokens != null)
            {
                var aa = ManagementAuth;
                d[nameof(AppInfo)] = aa;
                d[nameof(ApiKeysSupported)] = aa;
                d[nameof(GetApiKeys)] = aa;
                d[nameof(AddApiKey)] = aa;
                d[nameof(RemoveApiKey)] = aa;
                d[nameof(SetApiKeyAddresses)] = aa;
                d[nameof(SetApiKeyTokens)] = aa;
            }
            if (Params.AllowStoredUsers)
            {
                const String ua = UserManagementAuth;
                d[nameof(StoredUsersSupported)] = ua;
                d[nameof(GetStoredUsers)] = ua;
                d[nameof(AddStoredUser)] = ua;
                d[nameof(RemoveStoredUser)] = ua;
                d[nameof(SetStoredUserTokens)] = ua;
            }
            return d.Freeze();
        }

        /// <summary>
        /// The tokens of new API keys from <see cref="SimpleAuthorizerParams.ApiKeyAuth"/>: null disables API keys, so does an invalid value (with an error message)
        /// </summary>
        String GetApiKeyDefaultTokens(String tokens)
        {
            if (tokens == null)
                return null;
            try
            {
                return NormalizeApiKeyTokens(tokens);
            }
            catch (ArgumentException ex)
            {
                Msg?.AddMessage(MessageLevels.Error, $"{LogPrefix}API keys are disabled, {nameof(SimpleAuthorizerParams.ApiKeyAuth)} is invalid: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// True if a (lower case) token is a valid API key token: "service" or "service_" followed by letters, digits, '_', '-' or '.'
        /// </summary>
        /// <param name="token">The lower case token</param>
        /// <returns>True if valid</returns>
        public static bool IsApiKeyToken(String token)
        {
            if (String.IsNullOrEmpty(token) || (token.Length > MaxApiKeyTokenLength))
                return false;
            if (!token.StartsWith(ApiKeyToken, StringComparison.Ordinal))
                return false;
            var l = token.Length;
            var s = ApiKeyToken.Length;
            if (l == s)
                return true;
            if ((token[s] != '_') || (l == s + 1))
                return false;
            for (int i = s + 1; i < l; ++i)
            {
                var c = token[i];
                if (!(Char.IsAsciiLetterLower(c) || Char.IsAsciiDigit(c) || (c == '_') || (c == '-') || (c == '.')))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Validate and normalize the tokens of an API key: comma separated, white spaces trimmed, lower cased, duplicates removed, sorted.
        /// </summary>
        /// <param name="tokens">The tokens, ex: "Service, service_translate"</param>
        /// <returns>The normalized list, ex: "service,service_translate"</returns>
        /// <exception cref="ArgumentException">No token, or a token that isn't "service" or "service_&lt;scope&gt;" (scope: letters, digits, '_', '-', '.')</exception>
        public static String NormalizeApiKeyTokens(String tokens)
        {
            var t = (tokens ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.FastToLower()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (t.Length == 0)
                throw new ArgumentException("An API key must have at least one token (\"" + ApiKeyToken + "\" or \"" + ApiKeyToken + "_<scope>\")", nameof(tokens));
            foreach (var x in t)
                if (!IsApiKeyToken(x))
                    throw new ArgumentException("Invalid API key token " + x.ToQuoted() + ", only \"" + ApiKeyToken + "\" or \"" + ApiKeyToken + "_<scope>\" (letters, digits, '_', '-' and '.') is allowed, at most " + MaxApiKeyTokenLength + " chars", nameof(tokens));
            return String.Join(',', t);
        }

        /// <summary>
        /// Read the API keys (decrypted), records with an invalid key or tokens are skipped (with an error message).
        /// Must be called with <see cref="ApiKeyLock"/> held, or from <see cref="UpdateUsers"/> (which holds it).
        /// </summary>
        /// <returns>The keys (never null)</returns>
        List<ApiKeyRecord> ReadApiKeyRecords()
        {
            var dp = DataProtection.Default;
            var r = new List<ApiKeyRecord>();
            var lines = KeyValueStore.AllApp.TryGet<String[]>(ApiKeyStoreKey);
            if (lines == null)
                return r;
            foreach (var l in lines)
            {
                var p = l.Split(':', 5);
                if (p.Length < 4)
                    continue;
                String key;
                String tokens;
                try
                {
                    key = dp.Decrypt(ApiKeyKind, Convert.FromBase64String(p[1]), Convert.FromBase64String(p[2]));
                    tokens = NormalizeApiKeyTokens(p[3]);
                }
                catch (Exception ex)
                {
                    Msg?.AddMessage(LogPrefix + "Failed to read the API key " + p[0].ToQuoted() + " (the data protection secret may have changed, or the stored tokens are invalid), the key is ignored", ex, MessageLevels.Error);
                    continue;
                }
                r.Add(new ApiKeyRecord
                {
                    Name = p[0],
                    Key = key,
                    Tokens = tokens,
                    Addresses = p.Length > 4 ? NullIfEmpty(p[4]) : null,
                });
            }
            return r;
        }

        static String NullIfEmpty(String s) => String.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Write the API keys (encrypted, a new random value per key and write)
        /// </summary>
        /// <param name="keys">The keys</param>
        static void WriteApiKeyRecords(IEnumerable<ApiKeyRecord> keys)
        {
            var dp = DataProtection.Default;
            var lines = new List<String>();
            foreach (var k in keys)
            {
                var rnd = DataProtection.NewRandom();
                lines.Add(String.Join(':', k.Name, Convert.ToBase64String(rnd), Convert.ToBase64String(dp.Encrypt(ApiKeyKind, rnd, k.Key)), k.Tokens, k.Addresses ?? ""));
            }
            if (lines.Count == 0)
                KeyValueStore.AllApp.Delete(ApiKeyStoreKey);
            else
                KeyValueStore.AllApp.Set(ApiKeyStoreKey, lines.ToArray());
        }

        /// <summary>
        /// A random 40 char alpha numerical key (from a secure RNG)
        /// </summary>
        static String GenKey()
        {
            var cg = AlphaNumericCodeGenerator.Get(10);
            using var rng = SecureRng.Get();
            var mask = cg.InputMask;
            var a = cg.Encode((long)rng.GetUInt64() & mask, false, false);
            var b = cg.Encode((long)rng.GetUInt64() & mask, false, false);
            var c = cg.Encode((long)rng.GetUInt64() & mask, false, false);
            var d = cg.Encode((long)rng.GetUInt64() & mask, false, false);
            a = a.Interleave(b);
            c = c.Interleave(d);
            return a.Interleave(c);
        }

        /// <summary>
        /// Validate the name of a new API key or stored user (it's the user name)
        /// </summary>
        /// <param name="name">The name</param>
        /// <param name="paramName">The parameter name for the exception</param>
        /// <returns>The trimmed name</returns>
        /// <exception cref="ArgumentException">The name is empty, contains a ':' or '|', starts with a '#' or "//" or is too long</exception>
        static String ValidateName(String name, String paramName)
        {
            name = name?.Trim() ?? "";
            if (name.Length <= 0)
                throw new ArgumentException("Invalid name (may not be all white spaces or empty)", paramName);
            if ((name.IndexOf(':') >= 0) || (name.IndexOf('|') >= 0))
                throw new ArgumentException("Invalid name (may not contain a ':' or '|')", paramName);
            //  Would be parsed as a comment line (the key / user would never work)
            if ((name[0] == '#') || name.StartsWith("//", StringComparison.Ordinal))
                throw new ArgumentException("Invalid name (may not start with a '#' or \"//\")", paramName);
            if (name.Length > AuhorizationLimits.MaxUserNameLength)
                throw new ArgumentException("Name too long, use at most " + AuhorizationLimits.MaxUserNameLength + " chars!", paramName);
            return name;
        }

        /// <summary>
        /// The quoted user name of a web api caller for log lines, "an unknown caller" when unknown (ex: a call from code).
        /// </summary>
        static String CallerName(IAuthorizedRequest context)
        {
            var n = context?.CallerAuth?.Username;
            return String.IsNullOrEmpty(n) ? "an unknown caller" : n.ToQuoted();
        }

        #region API

        /// <summary>
        /// Get app information (for the API key management page).
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <returns>[EnvInfo.AppName, EnvInfo.AppDisplayName]</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public String[] AppInfo()
            => [EnvInfo.AppName, EnvInfo.AppDisplayName];

        /// <summary>
        /// Check if API key management is enabled.
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <returns>The tokens of a new key when none are given (<see cref="SimpleAuthorizerParams.ApiKeyAuth"/>, normalized), null if API keys are disabled</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public String ApiKeysSupported()
            => ApiKeyDefaultTokens;

        /// <summary>
        /// Get ALL API keys (decrypted, in clear text: they are stored encrypted, see <see cref="DataProtection"/>), with their tokens and allowed addresses.
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <returns>The keys (empty if there are none)</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        public ApiKeyInfo[] GetApiKeys()
        {
            lock (ApiKeyLock)
                return ReadApiKeyRecords().Select(x => new ApiKeyInfo
                {
                    Name = x.Name,
                    Key = String.Join(':', x.Name, x.Key),
                    Tokens = x.Tokens,
                    Addresses = x.Addresses ?? "",
                }).ToArray();
        }

        static Object AuditOutputFilter_AddApiKey(long id, Object obj)
        {
            var data = obj as String;
            if (data == null)
                return obj;
            var i = data.IndexOf(':');
            if (i < 0)
                return obj;
            var k = data.Substring(i + 1);
            return String.Concat(data.AsSpan(0, i + 1), k.Length > 6 ? k.Substring(0, 6) : "", "*****");
        }

        /// <summary>
        /// Add/create a new API key with a random 40 char alpha numerical key (from a secure RNG).
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <param name="request">The name of the new key (used as user name, please use only letters, digits, '_' and '-') and its tokens
        /// (comma separated, each "service" or "service_&lt;scope&gt;", empty or null for <see cref="SimpleAuthorizerParams.ApiKeyAuth"/>)</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>The new key as "name:key"</returns>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentException">The name or the tokens are invalid</exception>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: a key or a user with that name already exists (names are case insensitive)</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(ApiKeyAuditGroup)]
        [WebApiAuditFilterReturn(nameof(AuditOutputFilter_AddApiKey))]
        public String AddApiKey(AddApiKeyRequest request, IAuthorizedRequest context = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var name = ValidateName(request.Name, nameof(request.Name));
            var tokens = String.IsNullOrWhiteSpace(request.Tokens) ? ApiKeyDefaultTokens : NormalizeApiKeyTokens(request.Tokens);
            if (tokens == null)
                throw SysWeaver.Net.HttpResponseException.Refused("API keys are disabled");
            lock (ApiKeyLock)
            {
                var r = ReadApiKeyRecords();
                //  May not collide with an existing key or user (would replace it, same name => same guid)
                if (r.Any(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) || Auths.ContainsKey(name.FastToLower()))
                    throw SysWeaver.Net.HttpResponseException.Refused("Can't create the API key " + name.ToQuoted() + ": a key or user with that name already exists");
                var key = GenKey();
                r.Add(new ApiKeyRecord
                {
                    Name = name,
                    Key = key,
                    Tokens = tokens,
                });
                WriteApiKeyRecords(r);
                //  Always logged (the WebApiAudit entry, with the user, is only stored when an audit service is running), never the key itself
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}API key {name.ToQuoted()} with the tokens {tokens.ToQuoted()} was created by {CallerName(context)}");
                UpdateUsers();
                return String.Join(':', name, key);
            }
        }

        /// <summary>
        /// Remove an API key.
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <param name="keyName">The name of the key</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>True when the key was removed (never false: a key that doesn't exist is a 409 with the reason)</returns>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: there is no API key with that name (it may already be removed)</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(ApiKeyAuditGroup)]
        public bool RemoveApiKey(String keyName, IAuthorizedRequest context = null)
        {
            var name = keyName?.Trim() ?? "";
            lock (ApiKeyLock)
            {
                var r = ReadApiKeyRecords();
                var i = r.FindIndex(x => x.Name.FastEquals(name));
                if (i < 0)
                    throw SysWeaver.Net.HttpResponseException.Gone("Can't remove the API key " + name.ToQuoted() + ": it doesn't exist (it may already be removed), refresh the list");
                r.RemoveAt(i);
                WriteApiKeyRecords(r);
                //  Always logged (the WebApiAudit entry, with the user, is only stored when an audit service is running)
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}API key {name.ToQuoted()} was removed by {CallerName(context)}");
                UpdateUsers();
                return true;
            }
        }

        /// <summary>
        /// Restrict an API key to some client addresses / networks.
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <param name="request">The key name and the addresses (comma separated, ex: "10.0.0.5, 192.168.1.0/24"), empty or null allows any address</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>True when changed</returns>
        /// <exception cref="ArgumentException">An address is invalid</exception>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: the key doesn't exist (it may be removed)</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(ApiKeyAuditGroup)]
        public bool SetApiKeyAddresses(ApiKeyAddressesRequest request, IAuthorizedRequest context = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var name = request.Name?.Trim() ?? "";
            //  Validates (and normalizes) the list
            var nets = Authorization.ParseNetworks(request.Addresses);
            var addresses = nets == null ? null : String.Join(", ", nets.Select(x => x.ToString()));
            lock (ApiKeyLock)
            {
                var r = ReadApiKeyRecords();
                var k = r.FirstOrDefault(x => x.Name.FastEquals(name));
                if (k == null)
                    throw SysWeaver.Net.HttpResponseException.Gone("Can't change the API key " + name.ToQuoted() + ": it doesn't exist (it may be removed), refresh the list");
                k.Addresses = addresses;
                WriteApiKeyRecords(r);
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}API key {name.ToQuoted()} was restricted to {(addresses ?? "any address").ToQuoted()} by {CallerName(context)}");
                UpdateUsers();
                return true;
            }
        }

        /// <summary>
        /// Change the tokens of an API key (sessions signed in with the key are asked to sign out).
        /// Requires <see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/> (disabled if API keys are disabled).
        /// </summary>
        /// <param name="request">The key name and the tokens (comma separated, at least one, each "service" or "service_&lt;scope&gt;")</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>True when changed</returns>
        /// <exception cref="ArgumentException">The tokens are invalid</exception>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: the key doesn't exist (it may be removed)</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(ApiKeyAuditGroup)]
        public bool SetApiKeyTokens(ApiKeyTokensRequest request, IAuthorizedRequest context = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var name = request.Name?.Trim() ?? "";
            var tokens = NormalizeApiKeyTokens(request.Tokens);
            lock (ApiKeyLock)
            {
                var r = ReadApiKeyRecords();
                var k = r.FirstOrDefault(x => x.Name.FastEquals(name));
                if (k == null)
                    throw SysWeaver.Net.HttpResponseException.Gone("Can't change the API key " + name.ToQuoted() + ": it doesn't exist (it may be removed), refresh the list");
                k.Tokens = tokens;
                WriteApiKeyRecords(r);
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}The tokens of the API key {name.ToQuoted()} were set to {tokens.ToQuoted()} by {CallerName(context)}");
                UpdateUsers();
                return true;
            }
        }

        #endregion//API
    }

    /// <summary>
    /// An API key (see <see cref="SimpleAuthorizer.GetApiKeys"/>)
    /// </summary>
    public sealed class ApiKeyInfo
    {
        /// <summary>
        /// The name of the key (the user name)
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The key as "name:key" (the credentials, as used for basic auth)
        /// </summary>
        public String Key { get; set; }

        /// <summary>
        /// The tokens of the key (comma separated, each "service" or "service_&lt;scope&gt;")
        /// </summary>
        public String Tokens { get; set; }

        /// <summary>
        /// The allowed client addresses / networks (comma separated), empty = any address
        /// </summary>
        public String Addresses { get; set; }
    }

    /// <summary>
    /// The parameters of <see cref="SimpleAuthorizer.AddApiKey"/>
    /// </summary>
    public sealed class AddApiKeyRequest
    {
        /// <summary>
        /// The name of the new key (the user name: letters, digits, '_' and '-')
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The tokens (comma separated, each "service" or "service_&lt;scope&gt;"), empty or null for <see cref="SimpleAuthorizerParams.ApiKeyAuth"/>
        /// </summary>
        public String Tokens { get; set; }
    }

    /// <summary>
    /// The parameters of <see cref="SimpleAuthorizer.SetApiKeyAddresses"/>
    /// </summary>
    public sealed class ApiKeyAddressesRequest
    {
        /// <summary>
        /// The name of the API key
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The allowed addresses / networks (comma separated, ex: "10.0.0.5, 192.168.1.0/24"), empty or null allows any address
        /// </summary>
        public String Addresses { get; set; }
    }

    /// <summary>
    /// The parameters of <see cref="SimpleAuthorizer.SetApiKeyTokens"/>
    /// </summary>
    public sealed class ApiKeyTokensRequest
    {
        /// <summary>
        /// The name of the API key
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The tokens (comma separated, at least one, each "service" or "service_&lt;scope&gt;")
        /// </summary>
        public String Tokens { get; set; }
    }
}
