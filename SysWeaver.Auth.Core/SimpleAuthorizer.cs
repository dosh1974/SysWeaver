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
    /// A simple authorizer
    /// </summary>
    [WebMenuPath(null, "Debug/SimpleAuth", "Authorizer", "Simple authorizer", "IconUser")]
    [WebMenuEmbedded(null, "Debug/SimpleAuth/GenPwd", "Generate password hash", "auth/PwdGen.html", "Generate password hashes to be used for the simple authorizer", "IconLock", 0, "debug,ops")]
    [WebMenuEmbedded(null, "Debug/SimpleAuth/KeyMan", "API-key management", "auth/KeyMan.html", "Manage API-keys", "IconKey", 0, "", false, nameof(CanManageKeys))]
    public sealed class SimpleAuthorizer : AuthorizerBase, IDisposable, IRunTimeWebApiAuth
    {
        public override string Name => "Simple";

        public override string GuidPrefix => "SI";

        Task<bool> CanManageKeys(Authorization auth, WebMenuItem item)
        {
            if (auth == null)
                return TaskExt.FalseTask;
            var p = Params;
            if (p.ApiKeyAuth == null)
                return TaskExt.FalseTask;
            var r = p.ApiKeyManagementAuth;
            if (r == null)
                return TaskExt.FalseTask;
            if (auth.IsValid(r))
                return TaskExt.TrueTask;
            return TaskExt.FalseTask;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="msg">Optional message host to be used for logging</param>
        /// <param name="p">Parameters</param>
        public SimpleAuthorizer(IMessageHost msg = null, SimpleAuthorizerParams p = null)
        {
            p = p ?? new SimpleAuthorizerParams();
            Msg = msg;
            Params = p;
            InternalPasswordPolicy = p.PasswordPolicy ?? new PasswordPolicy();

            if (p.ApiKeyAuth != null)
            {
                var aa = p.ApiKeyManagementAuth;
                MethodAuths = new Dictionary<String, String>(StringComparer.Ordinal)
                {
                    { nameof(AppInfo), aa },
                    { nameof(ApiKeysSupported), aa },
                    { nameof(GetApiKeys), aa },
                    { nameof(AddApiKey), aa },
                    { nameof(RemoveApiKey), aa },
                }.Freeze();
            }else
            {
                MethodAuths = ReadOnlyData.EmptyDictionary<String, String>();
            }

            UpdateUsers(true);
            if (!String.IsNullOrEmpty(p.Location))
            {
                UserFile = new ManagedFile(p, OnFileUpdate);
                OnFileUpdate(UserFile.TryGetNow());
            }
        }
        
        readonly IMessageHost Msg;

        public override PasswordPolicy PasswordPolicy => InternalPasswordPolicy;

        readonly PasswordPolicy InternalPasswordPolicy;


        ManagedFile UserFile;
        readonly ExceptionTracker Fails = new ExceptionTracker();
        readonly SimpleAuthorizerParams Params;

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
            var f = new MemoryStream(d.Data).ReadAllLines(false).ToArray();
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

        const String ApiKeyKey = "BasicAuthApiKeys";

        static IEnumerable<String> Append(IEnumerable<String> x, String append)
        {
            if (x != null)
            {
                foreach (var a in x)
                    yield return a + append;
            }
        }


        void UpdateUsers(bool first = false)
        {
            try
            {
                var p = Params;
                var tokenDelim = TokenDelim;
                var a = new Dictionary<String, Tuple<Byte[], Authorization, String, bool>>(StringComparer.Ordinal);
                var guidMap = new Dictionary<String, AuthorizationInfo>(StringComparer.Ordinal);
                var aba = p.AllowBasicAuth;
                var apiKeyAuth = p.ApiKeyAuth;
                Dictionary<String, Task<Authorization>> bearerAuths = new(StringComparer.Ordinal);
                AddUsers(a, guidMap, p.Users, aba);
                AddUsers(a, guidMap, FileLines, aba);
                if (apiKeyAuth != null)
                {
                    var apiKeys = KeyValueStore.AllApp.TryGet<String[]>(ApiKeyKey);
                    if (apiKeys != null)
                    {
                        apiKeyAuth = ":" + apiKeyAuth;
                        foreach (var kv in apiKeys)
                        {
                            var auth = AddUser(a, guidMap, kv + apiKeyAuth, true, true);
                            bearerAuths[kv.Substring(kv.IndexOf(':') + 1)] = Task.FromResult(auth);
                        }
                    }
                }
                Dictionary<String, Tuple<Byte[], Authorization, String, bool>> changed = new (StringComparer.Ordinal);
                foreach (var x in Auths)
                    changed.Add(x.Key, x.Value);
                foreach (var x in a)
                {
                    if (!changed.TryGetValue(x.Key, out var y))
                        continue;
                    if (x.Value.Item3 == y.Item3)
                    {
                        if (SetsAreEqual(x.Value.Item2.Tokens, y.Item2.Tokens))
                        {
                            changed.Remove(x.Key);
                            continue;
                        }
                    }
                }

                

                Interlocked.Exchange(ref Auths, a.Freeze());
                Interlocked.Exchange(ref BearerAuths, bearerAuths.Freeze());
                Interlocked.Exchange(ref AuthGuids, guidMap.Freeze());
              

                foreach (var x in changed)
                    x.Value.Item2.RequestLogout("Password or tokens have changed!");
            }
            catch (Exception ex)
            {
                if (first)
                    throw;
                Fails.OnException(ex);
            }
        }

        void AddUsers(Dictionary<String, Tuple<Byte[], Authorization, String, bool>> a, Dictionary<String, AuthorizationInfo> guidMap, IEnumerable<String> users, bool allowBasicAuth, bool ignorePolicy = false)
        {
            if (users == null)
                return;
            foreach (var x in users)
                AddUser(a, guidMap, x, allowBasicAuth, ignorePolicy);
        }

        Authorization AddUser(Dictionary<String, Tuple<Byte[], Authorization, String, bool>> a, Dictionary<String, AuthorizationInfo> guidMap, String u, bool allowBasicAuth, bool ignorePolicy = false)
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
            String hash = null;
            if (pwd.Length == 44)
            {
                Byte[] d = new byte[32];
                if (Convert.TryFromBase64String(pwd, d.AsSpan(), out var w))
                {
                    if (w == 32)
                        hash = pwd;
                }
            }
            if (hash == null)
            {
                if (!ignorePolicy)
                {
                    var pol = PasswordPolicy;
                    var s = pol.Check(pwd);
                    if (s != PasswordStatus.Ok)
                    {
                        Msg?.AddMessage("Error: " + s + ", password for user " + user.ToQuoted() + " doesn't meet the password policy: " + pol, MessageLevels.Warning);
                        return null;
                    }
                }
                hash = AuthTools.ComputeSimplePasswordHash(ul, pwd);
            }
            var guid = String.Concat(GuidPrefix, ':', HashTools.GetHashString(user));
            var auth = new Authorization(this, user, Authorization.GetRequiredTokenSet(tokens), guid, user, null, null, domain);
            allowBasicAuth |= auth.Tokens.Contains("service");
            guidMap[guid] = new AuthorizationInfo(auth);
            a[ul] = new Tuple<byte[], Authorization, String, bool>(Convert.FromBase64String(hash), auth, hash, allowBasicAuth);
            return auth;
        }

        static readonly Char[] TokenDelim = "|:".ToCharArray();

        public override string ToString() => String.Join(String.Join(", ", Auths.Keys.Select(x => x.ToQuoted())), "Users: [", ']');

        IReadOnlyDictionary<String, Tuple<Byte[], Authorization, String, bool>> Auths = new Dictionary<String, Tuple<Byte[], Authorization, String, bool>>(StringComparer.Ordinal).Freeze();
        IReadOnlyDictionary<String, AuthorizationInfo> AuthGuids = new Dictionary<String, AuthorizationInfo>(StringComparer.Ordinal).Freeze();

        IReadOnlyDictionary<String, Task<Authorization>> BearerAuths = new Dictionary<String, Task<Authorization>>(StringComparer.Ordinal).Freeze();


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
        /// Get information about all users handled by the simple authorizer
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/SimpleAuth/{0}", "Users", null, "IconUsers")]
        public TableData UsersTable(TableDataRequest r) => TableDataTools.Get(r, 5000, Auths.Values.Select(x => new Info(x.Item2)));


        /// <summary>
        /// Get the salt to use for a user password
        /// </summary>
        /// <param name="user">The name of the user</param>
        /// <returns></returns>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        public Task<String> GetAuthSalt(String user)
        {
            return Task.FromResult(AuthTools.ComputeSimpleSalt(user));
        }


        public override long ChangeCounter => 0;


        /// <summary>
        /// Get information about a user (from it's guid)
        /// </summary>
        /// <param name="userGuid"></param>
        /// <returns></returns>
        public override Task<AuthorizationInfo> FindUserFromGuid(String userGuid)
        {
            if (!AuthGuids.TryGetValue(userGuid, out var data))
                return Task.FromResult<AuthorizationInfo>(null);
            return Task.FromResult(new AuthorizationInfo(data));
        }

        /// <summary>
        /// Get information about a user
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <returns></returns>
        public override Task<AuthorizationInfo> FindUser(String userName)
        {
            if (!Auths.TryGetValue(userName.FastToLower(), out var data))
                return Task.FromResult<AuthorizationInfo>(null);
            return Task.FromResult(new AuthorizationInfo(data.Item2));
        }

        public override Task<Authorization> BasicAuth(string userName, byte[] hash)
        {
            if (!Auths.TryGetValue(userName.FastToLower(), out var data))
                return NoAuth;
            //  Basic auth allowed
            if (!data.Item4)
                return NoAuth;
            if (!SpanExt.ContentEqual(hash, data.Item1))
                return NoAuth;
            return Task.FromResult(data.Item2);
        }

        public override Task<Authorization> BearerAuth(string token)
            => BearerAuths.TryGetValue(token, out var data) ? data : NoAuth;

        public override Task<Authorization> SecureAuth(string userName, byte[] hash, String oneTimePad)
        {
            if (!Auths.TryGetValue(userName.FastToLower(), out var data))
                return NoAuth;
            var dhash = AuthTools.ComputeHash(data.Item3, oneTimePad);
            if (!SpanExt.ContentEqual(hash, dhash))
                return NoAuth;
            return Task.FromResult(data.Item2);
        }


        public override Task<Authorization> TokenAuth(string oneTimeToken)
            => BearerAuths.TryGetValue(oneTimeToken, out var data) ? data : NoAuth;

        static readonly Task<String> TaskTupleStringStringNull = Task.FromResult<String>(null);

        public override Task<String> GetSaltAsync(string userName)
        {
            userName = userName.FastToLower();
            if (!Auths.ContainsKey(userName))
                return TaskTupleStringStringNull;
            return Task.FromResult(AuthTools.ComputeSimpleSalt(userName));
        }

        public Task<String> GetUserNameFromEmail(String email)
        {
            email = email.FastToLower();
            if (Auths.TryGetValue(email, out var a))
                return Task.FromResult(a.Item2.Username);
            return TaskExt.NullStringTask;
        }


        /// <summary>
        /// Get the password policy for the Simple Authorizer.
        /// </summary>
        /// <returns>The password policy</returns>
        [WebApi("debug/simpleAuth/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public PasswordPolicy GetPasswordPolicy()
            => PasswordPolicy;




        #region Api Keys


        readonly Object ApiKeyLock = new object();

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


        const String ApiKeyPath = "simpleAuth/{0}";
        const String ApiKeyAuditGroup = "api-key";
        const String ApiKeyAuth = Roles.Disabled;


        /// <summary>
        /// Get app information
        /// </summary>
        /// <returns>[EnvInfo.AppName, EnvInfo.AppDisplayName]</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ApiKeyAuth)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public String[] AppInfo()
            => [EnvInfo.AppName, EnvInfo.AppDisplayName];

        /// <summary>
        /// Check if API key management is enabled
        /// </summary>
        /// <returns>True if GetApiKeys, RemoveApiKey, AddApiKey is available</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ApiKeyAuth)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public bool ApiKeysSupported()
            => Params.ApiKeyAuth != null;


        /// <summary>
        /// Get a list of ALL api keys.
        /// </summary>
        /// <returns>An array of strings with the user/password pairs excoded as "user:key"</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ApiKeyAuth)]
        public String[] GetApiKeys()
            => KeyValueStore.AllApp.TryGet<String[]>(ApiKeyKey);

        /// <summary>
        /// Remove an api key
        /// </summary>
        /// <param name="keyName">The name of the key (user name)</param>
        /// <returns>True if the key was removed, false if it doesn't exit</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ApiKeyAuth)]
        [WebApiAudit(ApiKeyAuditGroup)]
        public bool RemoveApiKey(String keyName)
        {
            lock (ApiKeyLock)
            {
                var t = KeyValueStore.AllApp.TryGet<String[]>(ApiKeyKey);
                if (t == null)
                    return false;
                var tl = t.Length;
                var isFull = keyName.Contains(':');
                int del = -1;
                if (isFull)
                {
                    for (int i = 0; i < tl; ++i)
                    {
                        if (t[i].FastEquals(keyName))
                        {
                            del = i;
                            break;
                        }
                    }
                }else
                {
                    var kl = keyName.Length;
                    for (int i = 0; i < tl; ++i)
                    {
                        var tt = t[i];
                        if (tt.FastStartsWith(keyName))
                        {
                            if (tt[kl] == ':')
                            {
                                del = i;
                                break;
                            }
                        }
                    }

                }
                if (del < 0)
                    return false;
                t = t.RemoveAt(del);
                KeyValueStore.AllApp.Set(ApiKeyKey, t);
                UpdateUsers();
                return true;
            }
        }


        static Object AuditOutputFilter_AddApiKey(long id, Object obj)
        {
            var data = obj as String;
            if (data == null)
                return obj;
            var x = data.Split(':');
            x[1] = x[1].Substring(0, 6) + "*****";
            return String.Join(':', x);
        }

        /// <summary>
        /// Add/create a new API key
        /// </summary>
        /// <param name="keyName">The name of the new key, please use only alpha numericals</param>
        /// <returns>Return a string with new user/password pair as "user:password"</returns>
        /// <exception cref="Exception"></exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ApiKeyAuth)]
        [WebApiAudit(ApiKeyAuditGroup)]
        [WebApiAuditFilterReturn(nameof(AuditOutputFilter_AddApiKey))]
        public String AddApiKey(String keyName)
        {
            if (keyName == null)
                throw new Exception("Invalid key name (may not be null)");
            keyName = keyName.Trim();
            if (keyName.Length <= 0)
                throw new Exception("Invalid key name (may not be all white spaces or empty)");
            if (keyName.Contains(':'))
                throw new Exception("Invalid key name (may not contain a ':')");
            var kl = keyName.Length;
            if (kl > AuhorizationLimits.MaxUserNameLength)
                throw new Exception("Key name to long, use at most " + AuhorizationLimits.MaxUserNameLength + " chars!");
            lock (ApiKeyLock)
            {
                var t = KeyValueStore.AllApp.TryGet<String[]>(ApiKeyKey);
                if (t != null)
                {
                    var tl = t.Length;
                    for (int i = 0; i < tl; ++i)
                    {
                        var tt = t[i];
                        if (tt.FastStartsWith(keyName))
                        {
                            if (tt[kl] == ':')
                                return null;
                        }
                    }
                }
                var kv = String.Join(':', keyName, GenKey());
                t = t.Push(kv);
                KeyValueStore.AllApp.Set(ApiKeyKey, t);
                UpdateUsers();
                return kv;
            }
        }


        public IReadOnlyDictionary<String, String> MethodAuths { get; init; }

        #endregion//Api Keys


    }


}
