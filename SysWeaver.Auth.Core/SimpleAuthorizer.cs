using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.Auth
{


    /// <summary>
    /// A simple authorizer with a static set of users defined in the parameters and/or a (monitored) file, and optional API keys.
    /// </summary>
    /// <remarks>
    /// Users are defined as "user:password:tokens:domain" strings, see <see cref="SimpleAuthorizerParams.Users"/>.
    /// The password is a SCRAM verifier ("$s1$...", see <see cref="ScramVerifier"/>, the "Generate password hash" debug page can compute it)
    /// or clear text (validated against the password policy). Password hashes (44 char base64) are refused.
    /// API keys are only accepted as header auth (bearer / basic), never as a one time token (<see cref="AuthorizerBase.TokenAuth(string)"/>).
    /// API keys are stored encrypted in <see cref="KeyValueStore.AllApp"/> (see <see cref="DataProtection"/>), optionally restricted to some client addresses, and are accepted as bearer tokens (and basic auth).
    /// Header auth (basic / bearer) is only allowed for API keys, unless <see cref="SimpleAuthorizerParams.AllowBasicAuth"/> is true.
    /// The user guids are "SI:" followed by a hash of the user name.
    /// Thread safe, user changes are applied atomically and users whose password or tokens changed are asked to log out.
    /// </remarks>
    [WebMenuEmbedded(null, "Admin/GenPwd", "Generate password hash", "auth/index.html", "Generate password hashes to be used for the simple authorizer", "IconLock", 3, Roles.Ops)]
    [WebMenuEmbedded(null, "Admin/KeyMan", "API-key management", "auth/KeyMan.html", "Manage API-keys", "IconKey", 2, "", false, nameof(CanManageKeys))]
    [WebMenuEmbedded(null, "Admin/UserMan", "Stored users", "auth/UserMan.html", "Add and remove users and change their tokens", "IconUsers", 2, "", false, nameof(CanManageUsers))]
    public sealed partial class SimpleAuthorizer : AuthorizerBase, IDisposable, IRunTimeWebApiAuth
    {
        /// <inheritdoc/>
        public override string Name => "Simple";

        /// <inheritdoc/>
        public override string GuidPrefix => "SI";

        Task<bool> CanManageKeys(Authorization auth, WebMenuItem item)
        {
            if (auth == null)
                return TaskExt.FalseTask;
            if (ApiKeyDefaultTokens == null)
                return TaskExt.FalseTask;
            if (auth.IsValid(ManagementTokens))
                return TaskExt.TrueTask;
            return TaskExt.FalseTask;
        }

        /// <summary>
        /// Create a simple authorizer, users are loaded immediately (and reloaded if the user file changes).
        /// </summary>
        /// <param name="msg">Optional message host to be used for logging</param>
        /// <param name="p">Parameters, null uses the defaults (no users)</param>
        /// <exception cref="Exception">Loading the users failed and <see cref="ManagedFileParams.MustExist"/> is true.</exception>
        public SimpleAuthorizer(IMessageHost msg = null, SimpleAuthorizerParams p = null)
        {
            p = p ?? new SimpleAuthorizerParams();
            Msg = msg;
            Params = p;
            InternalPasswordPolicy = p.PasswordPolicy ?? new PasswordPolicy();
            AppHashString = String.IsNullOrEmpty(p.AppHashString) ? EnvInfo.AppAssemblyName : p.AppHashString;
            InternalScramDefaults = new ScramInfo
            {
                Iterations = ScramTools.ClampIterations(p.ScramIterations),
                Binding = AppHashString,
            };
            MustExist = p.MustExist;
            //  null must never mean "no auth required" for the key management methods, use the default instead
            ManagementAuth = p.ApiKeyManagementAuth ?? Roles.AdminOps;
            ManagementTokens = Authorization.GetRequiredTokens(ManagementAuth);
            ApiKeyDefaultTokens = GetApiKeyDefaultTokens(p.ApiKeyAuth);
            MethodAuths = BuildMethodAuths();
            if (!String.IsNullOrEmpty(p.Location))
            {
                UserFile = new ManagedFile(p, OnFileUpdate);
                OnFileUpdate(UserFile.TryGetNow());
            }else
                UpdateUsers();
        }

        readonly bool MustExist;

        /// <summary>
        /// The effective auth required to manage API keys (<see cref="SimpleAuthorizerParams.ApiKeyManagementAuth"/>, null is mapped to <see cref="Roles.AdminOps"/>).
        /// </summary>
        readonly String ManagementAuth;

        /// <summary>
        /// The pre-processed tokens of <see cref="ManagementAuth"/> (no parsing per check)
        /// </summary>
        readonly IReadOnlyList<String> ManagementTokens;

        readonly IMessageHost Msg;

        /// <inheritdoc/>
        public override PasswordPolicy PasswordPolicy => InternalPasswordPolicy;

        readonly PasswordPolicy InternalPasswordPolicy;


        ManagedFile UserFile;
        readonly ExceptionTracker Fails = new ExceptionTracker();
        readonly SimpleAuthorizerParams Params;

        /// <summary>
        /// Stop monitoring the user file.
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref UserFile, null)?.Dispose();
        }

        void OnFileUpdate(ManagedFileData d)
        {
            if (d == null)
            {
                FileLines = null;
                UpdateUsers();
                return;
            }
            var ex = d.Ex;
            if (ex != null)
            {
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to read {d.Location.ToQuoted()}", ex);
                FileLines = null;
                UpdateUsers();
                return;
            }
            var f = StringTools.GetLines(d.Data.Span);
            FileLines = f;
            UpdateUsers();
        }

        String[] FileLines;


        bool SetsAreEqual(IReadOnlySet<String> a, IReadOnlySet<String> b)
        {
            if (a == null)
                return b == null;
            if (b == null)
                return false;
            return a.SetEquals(b) && b.SetEquals(a);
        }


        static IEnumerable<String> Append(IEnumerable<String> x, String append)
        {
            if (x != null)
            {
                foreach (var a in x)
                    yield return a + append;
            }
        }

        bool First = true;

        void UpdateUsers()
        {
            var first = Interlocked.Exchange(ref First, false);
            var msg = Msg;
            try
            {
                msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Updating users");
                using var _ = msg?.Tab();
                Dictionary<String, UserEntry> changed = new (StringComparer.Ordinal);
                //  Serialize updates, so that a concurrent update (file change vs api key change) can't publish a stale snapshot
                lock (ApiKeyLock)
                {
                    //  Incremented before the new authorizations are created, so that they capture the new value (else their Cc never matches the ChangeCounter)
                    Interlocked.Increment(ref InternalChangeCounter);
                    var p = Params;
                    var tokenDelim = TokenDelim;
                    var a = new Dictionary<String, UserEntry>(StringComparer.Ordinal);
                    var guidMap = new Dictionary<String, AuthorizationInfo>(StringComparer.Ordinal);
                    var aba = p.AllowBasicAuth;
                    Dictionary<String, Task<Authorization>> bearerAuths = new(StringComparer.Ordinal);
                    if ((p.Users?.Length ?? 0) > 0)
                    {
                        msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Adding users from service config");
                        using (msg?.Tab())
                            AddUsers(a, guidMap, p.Users, aba);
                    }
                    if ((FileLines?.Length ?? 0) > 0)
                    {
                        msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Adding users from file");
                        using (msg?.Tab())
                            AddUsers(a, guidMap, FileLines, aba);
                    }
                    if (p.AllowStoredUsers)
                    {
                        var stored = ReadStoredUserRecords();
                        if (stored.Count > 0)
                        {
                            msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Adding stored users");
                            using var __ = msg?.Tab();
                            //  Stored users may not replace configured users (same name => same user and guid)
                            var configured = new HashSet<String>(a.Keys, StringComparer.Ordinal);
                            foreach (var rec in stored)
                            {
                                if (configured.Contains(rec.Name.FastToLower()))
                                {
                                    msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Ignoring the stored user {rec.Name.ToQuoted()}, a user with that name already exists");
                                    continue;
                                }
                                AddUser(a, guidMap, String.Join(':', rec.Name, rec.Verifier, rec.Tokens ?? ""), aba, true);
                            }
                        }
                    }
                    if (ApiKeyDefaultTokens != null)
                    {
                        var apiKeys = ReadApiKeyRecords();
                        if (apiKeys.Count > 0)
                        {
                            msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Adding users from stored api keys");
                            using var __ = msg?.Tab();
                            //  Api keys may not replace other users (same name => same user and guid)
                            var configured = new HashSet<String>(a.Keys, StringComparer.Ordinal);
                            foreach (var rec in apiKeys)
                            {
                                var keyName = rec.Name.Trim();
                                if (configured.Contains(keyName.FastToLower()))
                                {
                                    msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Ignoring api key {keyName.ToQuoted()}, a user with that name already exists");
                                    continue;
                                }
                                IReadOnlyList<System.Net.IPNetwork> nets;
                                try
                                {
                                    nets = Authorization.ParseNetworks(rec.Addresses);
                                }
                                catch (ArgumentException ex)
                                {
                                    //  Fail closed: an invalid list allows no address
                                    msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}The allowed addresses of the api key {keyName.ToQuoted()} are invalid, the key can't be used: {ex.Message}");
                                    nets = [];
                                }
                                var auth = AddUser(a, guidMap, String.Join(':', rec.Name, rec.Key, rec.Tokens), true, true, true, nets);
                                if (auth == null)
                                    continue;
                                bearerAuths[rec.Key] = Task.FromResult(auth);
                            }
                        }
                    }
                    foreach (var x in Auths)
                        changed.Add(x.Key, x.Value);
                    foreach (var x in a)
                    {
                        if (!changed.TryGetValue(x.Key, out var y))
                            continue;
                        if (x.Value.PwdKey == y.PwdKey)
                        {
                            if (SetsAreEqual(x.Value.Auth.Tokens, y.Auth.Tokens))
                            {
                                changed.Remove(x.Key);
                                continue;
                            }
                        }
                    }
                    Interlocked.Exchange(ref Auths, a.Freeze());
                    Interlocked.Exchange(ref BearerAuths, bearerAuths.Freeze());
                    Interlocked.Exchange(ref AuthGuids, guidMap.Freeze());
                }
                foreach (var x in changed)
                {
                    x.Value.Auth.RequestLogout("Password or tokens have changed!");
                    //  The weak (basic auth / API key) authorization is used by token and bearer logins
                    x.Value.BasicAuth?.RequestLogout("Password or tokens have changed!");
                }
            }
            catch (Exception ex)
            {
                Fails.OnException(ex);
                if (first)
                {
                    if (MustExist)
                        throw;
                    msg?.AddMessage(LogPrefix + "Failed to update users", ex, MessageLevels.Warning);
                }else
                {
                    //  Must be visible, the previous users are kept (so other changes of the update aren't applied)
                    msg?.AddMessage(LogPrefix + "Failed to update users, keeping the previous users", ex, MessageLevels.Error);
                }
                return;
            }
            msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Users updated");
        }

        /// <summary>
        /// True if a password value is a password hash (44 chars of base64 for 32 bytes, ex: <see cref="AuthTools.ComputeSimplePasswordHash(string, string, string)"/>), not a password
        /// </summary>
        static bool IsPasswordHash(String pwd)
        {
            if (pwd.Length != 44)
                return false;
            Span<Byte> d = stackalloc byte[32];
            return Convert.TryFromBase64String(pwd, d, out var w) && (w == 32);
        }

        void AddUsers(Dictionary<String, UserEntry> a, Dictionary<String, AuthorizationInfo> guidMap, IEnumerable<String> users, bool allowBasicAuth, bool ignorePolicy = false)
        {
            if (users == null)
                return;
            foreach (var x in users)
                AddUser(a, guidMap, x, allowBasicAuth, ignorePolicy);
        }

        Authorization AddUser(Dictionary<String, UserEntry> a, Dictionary<String, AuthorizationInfo> guidMap, String u, bool allowBasicAuth, bool ignorePolicy = false, bool isApiKey = false, IReadOnlyList<System.Net.IPNetwork> networks = null)
        {
            var ud = u?.Trim();
            if (String.IsNullOrEmpty(ud))
                return null;
            if (ud.StartsWith("//"))
                return null;
            if (ud[0] == '#')
                return null;
            var i = ud.IndexOf(':');
            if (i <= 0)
                return null;
            var user = ud.Substring(0, i).TrimEnd();
            var pwd = ud.Substring(i + 1).TrimStart();
            String tokens = "";
            String domain = "";
            i = pwd.IndexOfAny(TokenDelim);
            if (i > 0)
            {
                tokens = pwd.Substring(i + 1).TrimStart();
                pwd = pwd.Substring(0, i).TrimEnd();
                i = tokens.IndexOfAny(TokenDelim);
                if (i >= 0)
                {
                    domain = tokens.Substring(i + 1).TrimStart();
                    tokens = tokens.Substring(0, i).TrimEnd();
                }
            }
            var ul = user.FastToLower();
            //  The stored value: a SCRAM verifier ("$s1$...") or a clear text password
            String hash = null;
            var verifier = ScramVerifier.TryParse(pwd);
            if ((verifier == null) && pwd.StartsWith(ScramVerifier.Prefix, StringComparison.Ordinal))
            {
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Ignoring user {user.ToQuoted()}, the password verifier is invalid");
                return null;
            }
            if (verifier == null)
            {
                //  A 44 char base64 value (32 bytes) is a password hash, not a password: refused, else a copied hash would be a working password
                if (!isApiKey && IsPasswordHash(pwd))
                {
                    Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Ignoring user {user.ToQuoted()}, password hashes are not supported (use a \"{ScramVerifier.Prefix}...\" verifier)");
                    return null;
                }
                if (!ignorePolicy)
                {
                    var pol = PasswordPolicy;
                    var s = pol.Check(pwd);
                    if (s != PasswordStatus.Ok)
                    {
                        Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}{s}, password for user {user.ToQuoted()} doesn't meet the password policy: {pol}");
                        return null;
                    }
                }
                hash = AuthTools.ComputeSimplePasswordHash(ul, pwd, AppHashString);
                //  API keys are long random strings, a low iteration count is enough (and keeps updates fast)
                verifier = GetDerivedVerifier(hash, isApiKey ? ScramTools.MinIterations : Params.ScramIterations);
            }
            var guid = String.Concat(GuidPrefix, ':', HashTools.GetHashString(user));
            Authorization auth;
            Authorization basicAuth;
            AuthorizationInfo info;
            try
            {
                auth = new Authorization(this, user, Authorization.GetRequiredTokenSet(tokens), false, guid, user, null, null, domain) { AllowedNetworks = networks };
                //  Header auth (basic / bearer) is only for API keys, unless SimpleAuthorizerParams.AllowBasicAuth is set.
                //  Basic auth compares the password hash (no PBKDF2 per request), so it needs a clear text password (or an API key)
                basicAuth = allowBasicAuth && (hash != null) ? new Authorization(this, user, Authorization.GetRequiredTokenSet(tokens), true, guid, user, null, null, domain) { AllowedNetworks = networks, IsApiKey = isApiKey } : null;
                info = new AuthorizationInfo(auth);
            }
            catch (ArgumentException ex)
            {
                //  An invalid line (ex: a too long user name or domain) is skipped, the other users are still loaded
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Ignoring user {user.ToQuoted()}, the user line is invalid: {ex.Message}");
                return null;
            }
            guidMap[guid] = info;
            a[ul] = new UserEntry
            {
                Auth = auth,
                BasicAuth = basicAuth,
                Verifier = verifier,
                PasswordHash = basicAuth == null ? null : Convert.FromBase64String(hash),
                PwdKey = verifier.ToString(),
                IsApiKey = isApiKey,
            };
            Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Added user \"{user}\"");
            return basicAuth ?? auth;
        }

        /// <summary>
        /// A user (or API key)
        /// </summary>
        sealed class UserEntry
        {
            /// <summary>
            /// The (strong) authorization of the user
            /// </summary>
            public Authorization Auth;
            /// <summary>
            /// The weak authorization (basic auth, API keys), null if basic auth isn't allowed for the user
            /// </summary>
            public Authorization BasicAuth;
            /// <summary>
            /// The SCRAM verifier (never null)
            /// </summary>
            public ScramVerifier Verifier;
            /// <summary>
            /// The password hash H (password equivalent, see <see cref="AuthTools.ComputeSimplePasswordHash(string, string, string)"/>) for basic auth, null if basic auth isn't allowed for the user
            /// </summary>
            public Byte[] PasswordHash;
            /// <summary>
            /// The stored password value (used to detect password changes)
            /// </summary>
            public String PwdKey;
            /// <summary>
            /// True for an API key user: every sign in method gives the weak authorization (<see cref="BasicAuth"/>), so a key never gets a strong session (ex: can't add passkeys)
            /// </summary>
            public bool IsApiKey;

            /// <summary>
            /// The authorization to give after a successful sign in (password / proof): the weak one for API keys
            /// </summary>
            public Authorization SignInAuth => IsApiKey ? BasicAuth : Auth;
        }

        /// <summary>
        /// Verifiers of clear text passwords and API keys, PBKDF2 is only computed once per hash, binding and iteration count.
        /// The salt is random (per process): it is public (sign in parameters), a salt derived from the hash would let anyone test password guesses without the PBKDF2 cost.
        /// The one time pad of a sign in is per process too, so a new salt after a restart doesn't matter.
        /// </summary>
        readonly System.Collections.Concurrent.ConcurrentDictionary<String, ScramVerifier> DerivedVerifiers = new (StringComparer.Ordinal);

        ScramVerifier GetDerivedVerifier(String passwordHash, int iterations)
        {
            iterations = ScramTools.ClampIterations(iterations);
            var binding = AppHashString;
            var key = String.Join('|', passwordHash, iterations, binding);
            if (DerivedVerifiers.TryGetValue(key, out var v))
                return v;
            v = ScramTools.CreateVerifier(passwordHash, binding, iterations);
            return DerivedVerifiers.GetOrAdd(key, v);
        }

        /// <summary>
        /// The application binding string (<see cref="SimpleAuthorizerParams.AppHashString"/>, defaults to the entry assembly name)
        /// </summary>
        public readonly String AppHashString;

        const String LogPrefix = "[Auth] ";

        static readonly Char[] TokenDelim = "|:".ToCharArray();

        /// <inheritdoc/>
        public override string ToString() => String.Join(String.Join(", ", Auths.Keys.Select(x => x.ToQuoted())), "Users: [", ']');

        IReadOnlyDictionary<String, UserEntry> Auths = new Dictionary<String, UserEntry>(StringComparer.Ordinal).Freeze();
        IReadOnlyDictionary<String, AuthorizationInfo> AuthGuids = new Dictionary<String, AuthorizationInfo>(StringComparer.Ordinal).Freeze();

        IReadOnlyDictionary<String, Task<Authorization>> BearerAuths = new Dictionary<String, Task<Authorization>>(StringComparer.Ordinal).Freeze();


        [TableDataCard("+user@auth/cards")]
        sealed class Info
        {
            /// <summary>
            /// Icon for the user
            /// </summary>
            [TableDataUserIcon]
            public readonly String Icon;

            /// <summary>
            /// The name of the user
            /// </summary>
            public readonly String Name;

            /// <summary>
            /// The auth token that this user have
            /// </summary>
            [TableDataTags]
            public readonly String Auth;

            /// <summary>
            ///  The domain for this user
            /// </summary>
            public readonly String Domain;

            public Info(Authorization a)
            {
                Icon = a.Guid.ToHex();
                Name = a.Username;
                var t = a.Tokens;
                Auth = t == null || (t.Count == 0) ? null : String.Join(',', t);
                Domain = a.Domain;
            }
        }

        /// <summary>
        /// The built-in users: the users of the server configuration (handled by the simple authorizer), they can't be changed here
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <returns>The users</returns>
        /// <remarks>Shown as Administration > "Built-in users", so it never clashes with the user accounts of a user manager (Administration > Users).</remarks>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Admin/{0}", "Built-in users", "The users of the service configuration, they can't be changed here", "IconUsers", 1)]
        public TableData UsersTable(TableDataRequest r) => TableDataTools.Get(r, 5000, Auths.Values.Select(x => new Info(x.Auth)));


        /// <summary>
        /// Get the salt to use for a user password hash, see <see cref="AuthTools.ComputeSimpleSalt(string)"/>.
        /// Returns a salt for any user name (known or not), used by the "Generate password hash" page.
        /// </summary>
        /// <param name="user">The name of the user</param>
        /// <returns>The salt</returns>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        public Task<String> GetAuthSalt(String user)
        {
            return Task.FromResult(AuthTools.ComputeSimpleSalt(user, AppHashString));
        }

        /// <summary>
        /// Get the parameters used to create a password verifier ("$s1$..." format) on the "Generate password hash" page.
        /// The verifier is: "$s1$" + Iterations + "$" + ToBase64(salt) + "$" + ToBase64(SHA256(HMAC-SHA256(PBKDF2-SHA256(UTF8(hash + "|" + Binding), salt, Iterations), "Client Key")))
        /// where hash is the simple hash (password + "|" + GetAuthSalt(user)) and salt is 16 random bytes.
        /// </summary>
        /// <returns>The parameters (the salt is null, use a random one)</returns>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public ScramInfo GetVerifierParams() => new ScramInfo
        {
            Iterations = InternalScramDefaults.Iterations,
            Binding = AppHashString,
        };

        /// <inheritdoc/>
        public override ScramInfo ScramDefaults => InternalScramDefaults;

        readonly ScramInfo InternalScramDefaults;


        /// <summary>
        /// Incremented every time the users (including API keys) are updated, so cached authorizations are re-validated.
        /// Changed users are also signalled by <see cref="Authorization.RequestLogout(string)"/> on the old authorizations.
        /// </summary>
        public override long ChangeCounter => Interlocked.Read(ref InternalChangeCounter);

        long InternalChangeCounter;


        /// <inheritdoc/>
        public override Task<AuthorizationInfo> FindUserFromGuid(String userGuid)
        {
            if (!AuthGuids.TryGetValue(userGuid, out var data))
                return NullFindUserFromGuid;
            return Task.FromResult(new AuthorizationInfo(data));
        }

        static readonly Task<AuthorizationInfo> NullFindUserFromGuid = Task.FromResult<AuthorizationInfo>(null);

        /// <inheritdoc/>
        public override Task<AuthorizationInfo> FindUser(String userName)
        {
            if (!Auths.TryGetValue(userName.FastToLower(), out var data))
                return NullTaskAuthorizationInfo;
            return Task.FromResult(new AuthorizationInfo(data.Auth));
        }

        static readonly Task<AuthorizationInfo> NullTaskAuthorizationInfo = Task.FromResult<AuthorizationInfo>(null);

        /// <summary>
        /// Authorize a user using basic auth, only allowed for API keys, or for all users if <see cref="SimpleAuthorizerParams.AllowBasicAuth"/> is true.
        /// The hash is compared in constant time against the password hash (no PBKDF2, so failed attempts are as cheap as successful ones).
        /// Users defined with a verifier have no password hash and can't use basic auth.
        /// </summary>
        /// <param name="userName">The user name (case insensitive)</param>
        /// <param name="hash">The password hash: SHA256(UTF8(password|salt))</param>
        /// <returns>A weak authorization, or null</returns>
        public override Task<Authorization> BasicAuth(string userName, byte[] hash)
        {
            if (!Auths.TryGetValue(userName.FastToLower(), out var data))
                return NoAuth;
            //  Basic auth allowed
            var ba = data.BasicAuth;
            if (ba == null)
                return NoAuth;
            if (hash == null)
                return NoAuth;
            var lh = data.PasswordHash;
            if ((lh == null) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(hash, lh))
                return NoAuth;
            return Task.FromResult(ba);
        }

        /// <summary>
        /// Authorize using an API key (the key part of a "name:key" API key) as a bearer token.
        /// </summary>
        /// <param name="token">The API key (null is not found, i.e. a "Bearer" header without a token)</param>
        /// <returns>A weak authorization for the API key user, or null</returns>
        public override Task<Authorization> BearerAuth(string token)
            => (token != null) && BearerAuths.TryGetValue(token, out var data) ? data : NoAuth;

        /// <inheritdoc/>
        public override Task<ScramInfo> GetScramInfoAsync(string userName)
        {
            if (!Auths.TryGetValue((userName ?? "").FastToLower(), out var data))
                return NoScram;
            return Task.FromResult(data.Verifier.GetInfo(AppHashString));
        }

        /// <inheritdoc/>
        public override Task<Authorization> ScramAuth(ScramProof proof)
        {
            if (proof == null)
                return NoAuth;
            if (!Auths.TryGetValue((proof.UserName ?? "").FastToLower(), out var data))
                return NoAuth;
            if (!ScramTools.Verify(data.Verifier, proof))
                return NoAuth;
            return Task.FromResult(data.SignInAuth);
        }


        static readonly Task<String> TaskTupleStringStringNull = Task.FromResult<String>(null);

        /// <summary>
        /// Get the salt of a known user, see <see cref="AuthTools.ComputeSimpleSalt(string, string)"/> (using <see cref="AppHashString"/>).
        /// </summary>
        /// <param name="userName">The user name (case insensitive), null is treated as empty</param>
        /// <returns>The salt, or null if the user is unknown</returns>
        public override Task<String> GetSaltAsync(string userName)
        {
            userName = (userName ?? "").FastToLower();
            if (!Auths.ContainsKey(userName))
                return TaskTupleStringStringNull;
            return Task.FromResult(AuthTools.ComputeSimpleSalt(userName, AppHashString));
        }

        /// <summary>
        /// Get the user name from an email, the simple authorizer uses the user name as email so this is a case insensitive user name lookup.
        /// </summary>
        /// <param name="email">The email (user name)</param>
        /// <returns>The user name with the original casing, or null if unknown</returns>
        public Task<String> GetUserNameFromEmail(String email)
        {
            email = email.FastToLower();
            if (Auths.TryGetValue(email, out var a))
                return Task.FromResult(a.Auth.Username);
            return TaskExt.NullStringTask;
        }


        /// <summary>
        /// Get the password policy for the Simple Authorizer.
        /// </summary>
        /// <returns>The password policy</returns>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public PasswordPolicy GetPasswordPolicy()
            => PasswordPolicy;






    }


}
