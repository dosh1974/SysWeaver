using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.MicroService;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Stored users: users added at run time (web API / the "Users" page), stored encrypted in <see cref="KeyValueStore.AllApp"/> like the API keys.
    /// A stored user has a SCRAM verifier ("$s1$...", made by the "Generate password hash" page or the "hash" verb of the service host) and tokens.
    /// Managed by users with the <see cref="Roles.Admin"/> tokens ("admin" or "debug"). A caller may only give the tokens it has itself, and may only change
    /// or remove a user whose tokens it all has (no role escalation, see <see cref="AuthTools.TokensNotHeld(Authorization, IEnumerable{string})"/>; "debug" has every token).
    /// The users of the configuration and the user file can't be changed here, and a stored user can't replace them.
    /// </summary>
    public sealed partial class SimpleAuthorizer
    {
        /// <summary>
        /// The key value store key of the stored users, one line per user: "name:random:encryptedVerifier:tokens" (random and encryptedVerifier are base64)
        /// </summary>
        const String StoredUsersStoreKey = "SimpleAuthorizer.Users";

        /// <summary>
        /// The kind of protected value (see <see cref="DataProtection"/>)
        /// </summary>
        const String StoredUserKind = "user";

        const String StoredUserAuditGroup = "user";

        /// <summary>
        /// The auth required to manage the stored users: "admin" or "debug"
        /// </summary>
        const String UserManagementAuth = Roles.Admin;

        /// <summary>
        /// A stored user
        /// </summary>
        sealed class StoredUserRecord
        {
            public String Name;
            public String Verifier;
            public String Tokens;
        }

        Task<bool> CanManageUsers(Authorization auth, WebMenuItem item)
            => (auth != null) && Params.AllowStoredUsers && auth.IsValid(UserManagementAuth) ? TaskExt.TrueTask : TaskExt.FalseTask;

        /// <summary>
        /// Validate and normalize the tokens of a stored user: comma separated, white spaces trimmed, lower cased, duplicates removed, sorted (may be empty).
        /// </summary>
        /// <param name="tokens">The tokens</param>
        /// <returns>The normalized list ("" for none)</returns>
        /// <exception cref="ArgumentException">A token has a ':', '|' or white space, or is too long</exception>
        public static String NormalizeUserTokens(String tokens)
        {
            var t = (tokens ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.FastToLower()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            foreach (var x in t)
            {
                if ((x.Length > MaxApiKeyTokenLength) || x.Any(c => (c == ':') || (c == '|') || Char.IsWhiteSpace(c)))
                    throw new ArgumentException("Invalid token " + x.ToQuoted() + " (no ':', '|' or spaces, at most " + MaxApiKeyTokenLength + " chars)", nameof(tokens));
            }
            return String.Join(',', t);
        }

        /// <summary>
        /// Read the stored users (decrypted), records that can't be read are skipped (with an error message).
        /// Must be called with <see cref="ApiKeyLock"/> held, or from <see cref="UpdateUsers"/> (which holds it).
        /// </summary>
        List<StoredUserRecord> ReadStoredUserRecords()
        {
            var dp = DataProtection.Default;
            var r = new List<StoredUserRecord>();
            var lines = KeyValueStore.AllApp.TryGet<String[]>(StoredUsersStoreKey);
            if (lines == null)
                return r;
            foreach (var l in lines)
            {
                var p = l.Split(':', 4);
                if (p.Length < 4)
                    continue;
                String verifier;
                String tokens;
                try
                {
                    verifier = dp.Decrypt(StoredUserKind, Convert.FromBase64String(p[1]), Convert.FromBase64String(p[2]));
                    if (ScramVerifier.TryParse(verifier) == null)
                        throw new Exception("The stored password verifier is invalid");
                    tokens = NormalizeUserTokens(p[3]);
                }
                catch (Exception ex)
                {
                    Msg?.AddMessage(LogPrefix + "Failed to read the stored user " + p[0].ToQuoted() + " (the data protection secret may have changed), the user is ignored", ex, MessageLevels.Error);
                    continue;
                }
                r.Add(new StoredUserRecord
                {
                    Name = p[0],
                    Verifier = verifier,
                    Tokens = tokens,
                });
            }
            return r;
        }

        /// <summary>
        /// Write the stored users (the verifiers encrypted, a new random value per user and write)
        /// </summary>
        static void WriteStoredUserRecords(IEnumerable<StoredUserRecord> users)
        {
            var dp = DataProtection.Default;
            var lines = new List<String>();
            foreach (var u in users)
            {
                var rnd = DataProtection.NewRandom();
                lines.Add(String.Join(':', u.Name, Convert.ToBase64String(rnd), Convert.ToBase64String(dp.Encrypt(StoredUserKind, rnd, u.Verifier)), u.Tokens ?? ""));
            }
            if (lines.Count == 0)
                KeyValueStore.AllApp.Delete(StoredUsersStoreKey);
            else
                KeyValueStore.AllApp.Set(StoredUsersStoreKey, lines.ToArray());
        }

        /// <summary>
        /// The tokens (of a normalized list) that the caller doesn't have: it may not give them, nor change or remove a user that has them
        /// (see <see cref="AuthTools.TokensNotHeld(Authorization, IEnumerable{string})"/>, a "debug" caller has them all)
        /// </summary>
        static String[] NotHeld(IAuthorizedRequest context, String normalizedTokens)
            => AuthTools.TokensNotHeld(context?.CallerAuth, (normalizedTokens ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Refuse (403) when the caller doesn't have some of the tokens
        /// </summary>
        static void RefuseNotHeld(String[] notHeld, String what)
        {
            if (notHeld.Length <= 0)
                return;
            throw SysWeaver.Net.HttpResponseException.Refused(String.Concat(what, ": you don't have the ", notHeld.Length == 1 ? "token " : "tokens ",
                String.Join(", ", notHeld.Select(x => x.ToQuoted())), " yourself (only a user that has a token may give it, or change a user that has it)"), 403);
        }

        static Object AuditInputFilter_AddStoredUser(long id, Object obj)
        {
            //  The verifier isn't stored in the audit log, only the user name and the tokens
            if (obj is not AddStoredUserRequest r)
                return obj;
            var a = r.Auth ?? "";
            var i = a.IndexOf(':');
            return new AddStoredUserRequest { Auth = (i < 0 ? a : a.Substring(0, i).Trim()) + " : *****", Tokens = r.Tokens };
        }

        #region API

        /// <summary>
        /// Check if the stored users can be managed (<see cref="SimpleAuthorizerParams.AllowStoredUsers"/>).
        /// Requires the "admin" or "debug" token.
        /// </summary>
        /// <returns>True if supported</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public bool StoredUsersSupported() => Params.AllowStoredUsers;

        /// <summary>
        /// Get the stored users (not the users of the configuration and the user file).
        /// Requires the "admin" or "debug" token.
        /// </summary>
        /// <param name="context">The caller (handled internally by the web api)</param>
        /// <returns>The users (empty if there are none)</returns>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        public StoredUserInfo[] GetStoredUsers(IAuthorizedRequest context = null)
        {
            lock (ApiKeyLock)
                return ReadStoredUserRecords().Select(x => new StoredUserInfo
                {
                    Name = x.Name,
                    Tokens = x.Tokens,
                    CanChange = NotHeld(context, x.Tokens).Length == 0,
                }).ToArray();
        }

        /// <summary>
        /// Add a stored user.
        /// Requires the "admin" or "debug" token, only the tokens the caller has itself may be given.
        /// </summary>
        /// <param name="request">The user line ("username : $s1$...", as made by the "Generate password hash" page or the "hash" verb of the service host) and the tokens (comma separated)</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>True when added</returns>
        /// <exception cref="ArgumentException">The line, the name, the verifier or the tokens are invalid</exception>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: a user or API key with that name exists; 403: a token the caller doesn't have itself</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(StoredUserAuditGroup)]
        [WebApiAuditFilterParams(nameof(AuditInputFilter_AddStoredUser))]
        public bool AddStoredUser(AddStoredUserRequest request, IAuthorizedRequest context = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var line = request.Auth?.Trim() ?? "";
            var i = line.IndexOf(':');
            if (i <= 0)
                throw new ArgumentException("Invalid user line, expected \"username : " + ScramVerifier.Prefix + "...\" (from the Generate password hash page)", nameof(request.Auth));
            var name = ValidateName(line.Substring(0, i), nameof(request.Auth));
            var verifier = line.Substring(i + 1).Trim();
            var v = ScramVerifier.TryParse(verifier);
            if (v == null)
                throw new ArgumentException("Invalid password verifier, expected \"" + ScramVerifier.Prefix + "...\" (from the Generate password hash page)", nameof(request.Auth));
            var tokens = NormalizeUserTokens(request.Tokens);
            RefuseNotHeld(NotHeld(context, tokens), "Can't add the user " + name.ToQuoted());
            lock (ApiKeyLock)
            {
                var r = ReadStoredUserRecords();
                if (r.Any(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) || Auths.ContainsKey(name.FastToLower()))
                    throw SysWeaver.Net.HttpResponseException.Refused("Can't add the user " + name.ToQuoted() + ": a user or API key with that name already exists");
                r.Add(new StoredUserRecord
                {
                    Name = name,
                    Verifier = v.ToString(),
                    Tokens = tokens,
                });
                WriteStoredUserRecords(r);
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}The user {name.ToQuoted()} with the tokens {tokens.ToQuoted()} was added by {CallerName(context)}");
                UpdateUsers();
                return true;
            }
        }

        /// <summary>
        /// Remove a stored user (the sessions of the user are asked to sign out).
        /// Requires the "admin" or "debug" token, only a user whose tokens the caller all has may be removed.
        /// </summary>
        /// <param name="userName">The name of the user</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>True when removed</returns>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: there is no stored user with that name; 403: the user has a token the caller doesn't have</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(StoredUserAuditGroup)]
        public bool RemoveStoredUser(String userName, IAuthorizedRequest context = null)
        {
            var name = userName?.Trim() ?? "";
            lock (ApiKeyLock)
            {
                var r = ReadStoredUserRecords();
                var i = r.FindIndex(x => x.Name.FastEquals(name));
                if (i < 0)
                    throw SysWeaver.Net.HttpResponseException.Gone("Can't remove the user " + name.ToQuoted() + ": it isn't a stored user (it may already be removed), refresh the list");
                RefuseNotHeld(NotHeld(context, r[i].Tokens), "Can't remove the user " + name.ToQuoted());
                r.RemoveAt(i);
                WriteStoredUserRecords(r);
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}The user {name.ToQuoted()} was removed by {CallerName(context)}");
                UpdateUsers();
                return true;
            }
        }

        /// <summary>
        /// Change the tokens of a stored user (the sessions of the user are asked to sign out).
        /// Requires the "admin" or "debug" token, only a user whose tokens the caller all has may be changed, and only to tokens the caller has.
        /// </summary>
        /// <param name="request">The user name and the tokens (comma separated)</param>
        /// <param name="context">The caller (handled internally by the web api), named in the log line, may be null</param>
        /// <returns>True when changed</returns>
        /// <exception cref="ArgumentException">The tokens are invalid</exception>
        /// <exception cref="SysWeaver.Net.HttpResponseException">409 with the reason: there is no stored user with that name; 403: a token (old or new) the caller doesn't have</exception>
        [WebApi(ApiKeyPath)]
        [WebApiAuth(ManagementAuthDisabled)]
        [WebApiAudit(StoredUserAuditGroup)]
        public bool SetStoredUserTokens(StoredUserTokensRequest request, IAuthorizedRequest context = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var name = request.Name?.Trim() ?? "";
            var tokens = NormalizeUserTokens(request.Tokens);
            RefuseNotHeld(NotHeld(context, tokens), "Can't give the tokens to " + name.ToQuoted());
            lock (ApiKeyLock)
            {
                var r = ReadStoredUserRecords();
                var u = r.FirstOrDefault(x => x.Name.FastEquals(name));
                if (u == null)
                    throw SysWeaver.Net.HttpResponseException.Gone("Can't change the user " + name.ToQuoted() + ": it isn't a stored user (it may be removed), refresh the list");
                RefuseNotHeld(NotHeld(context, u.Tokens), "Can't change the tokens of " + name.ToQuoted());
                u.Tokens = tokens;
                WriteStoredUserRecords(r);
                Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}The tokens of the user {name.ToQuoted()} were set to {tokens.ToQuoted()} by {CallerName(context)}");
                UpdateUsers();
                return true;
            }
        }

        #endregion//API
    }

    /// <summary>
    /// A stored user (see <see cref="SimpleAuthorizer.GetStoredUsers"/>)
    /// </summary>
    public sealed class StoredUserInfo
    {
        /// <summary>
        /// The user name
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The tokens (comma separated, lower case)
        /// </summary>
        public String Tokens { get; set; }

        /// <summary>
        /// True if the caller may change or remove the user (false: the user has a token the caller doesn't have)
        /// </summary>
        public bool CanChange { get; set; }
    }

    /// <summary>
    /// The parameters of <see cref="SimpleAuthorizer.AddStoredUser"/>
    /// </summary>
    public sealed class AddStoredUserRequest
    {
        /// <summary>
        /// The user line: "username : $s1$..." (as made by the "Generate password hash" page or the "hash" verb of the service host)
        /// </summary>
        public String Auth { get; set; }

        /// <summary>
        /// The tokens (comma separated, may be empty)
        /// </summary>
        public String Tokens { get; set; }
    }

    /// <summary>
    /// The parameters of <see cref="SimpleAuthorizer.SetStoredUserTokens"/>
    /// </summary>
    public sealed class StoredUserTokensRequest
    {
        /// <summary>
        /// The user name
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The tokens (comma separated, may be empty)
        /// </summary>
        public String Tokens { get; set; }
    }
}
