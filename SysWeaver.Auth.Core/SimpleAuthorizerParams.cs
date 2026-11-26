using System;
using System.Linq;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Parameters for <see cref="SimpleAuthorizer"/>.
    /// The inherited <see cref="ManagedFileParams"/> can specify an optional (monitored) user file with one user per line (same syntax as <see cref="Users"/>, lines starting with "#" or "//" are ignored).
    /// </summary>
    public sealed class SimpleAuthorizerParams : ManagedFileParams
    {

        /// <inheritdoc/>
        public override string ToString() =>
            String.Concat(
                nameof(Users), ": [", String.Join(", ", (Users ?? []).Select(x => x.Split(':')[0].ToQuoted())), "]");


        /// <summary>
        /// Create parameters with the defaults (the user file is optional).
        /// </summary>
        public SimpleAuthorizerParams()
        {
            MustExist = false;
        }

        /// <summary>
        /// One user per string with the following syntax: "username:password".
        /// Auth tokens can be specified by appending a colon (:) or bar (|) and a comma separated list of tokens, ex:
        /// "username:password:token1, token2, token3"
        /// An application specific domain can be appended after another colon or bar, ex: "username:password:tokens:domain".
        /// The password may therefore not contain ':' or '|'.
        /// Password should be a SCRAM verifier: "$s1$iterations$salt$storedKey" (see <see cref="ScramVerifier"/>, generated using the "Generate password hash" page).
        /// A verifier can't be used to sign in, it must be brute forced (PBKDF2 per guess) to find the password.
        /// Password can also be clear text (hashed and turned into a verifier when loaded).
        /// Verifiers are unique per user per application (<see cref="AppHashString"/>, default is the entry assembly name).
        /// A 44 char password that is valid base64 of 32 bytes is a password hash, not a password: such users are skipped with a warning.
        /// Clear text passwords must fulfill the <see cref="PasswordPolicy"/>, users with invalid passwords are skipped.
        /// Users with a too long name (over <see cref="AuhorizationLimits.MaxUserNameLength"/> chars) or domain (over <see cref="AuhorizationLimits.MaxDomainName"/> chars) are skipped with a warning message, the other users are still loaded.
        /// User names are case insensitive (the last definition of a name wins), white spaces around the parts are trimmed, empty lines and lines starting with "#" or "//" are ignored.
        /// The user guid is "SI:" followed by a hash of the user name (as written, case sensitive).
        /// </summary>
        public string[] Users;

        /// <summary>
        /// The requirements for clear text passwords, null uses the default policy.
        /// </summary>
        public PasswordPolicy PasswordPolicy;

        /// <summary>
        /// If true (and the server accepts the Authorization header), then the users of the file / configuration are also allowed to login using basic auth.
        /// By default (false) only API keys can use header auth (basic / bearer), they are meant for services (the users of the file / configuration sign in with the login page).
        /// Basic auth compares the password hash (no PBKDF2 per request), so it only works for users defined with a clear text password.
        /// </summary>
        public bool AllowBasicAuth;

        /// <summary>
        /// If non-null, API keys can be created and deleted, and this is the tokens of a new key when none are given (comma separated).
        /// Every key has its own tokens: at least one, each "service" or "service_&lt;scope&gt;" (see <see cref="SimpleAuthorizer.NormalizeApiKeyTokens"/>),
        /// an invalid value here disables API keys (with an error message).
        /// API keys can be used as bearer tokens, and they are allowed to use basic auth (http server still need to allow for it).
        /// An API key only gets access to what requires one of its tokens, never to what any logged in user may use (an empty requirement, see <see cref="Authorization.IsApiKey"/>).
        /// Each service token is a token of its own: "service_&lt;scope&gt;" does not fulfill a "service" requirement and "service" does not fulfill a "service_&lt;scope&gt;" requirement.
        /// A requirement with a service token is also fulfilled by "debug" and "dev" (see <see cref="Authorization.ServiceImpliedTokens"/>).
        /// </summary>
        public String ApiKeyAuth = "Service";

        /// <summary>
        /// If true (the default) users can be added and removed at run time and their tokens changed (the "Stored users" page, by users with the "admin" or "debug" token),
        /// they are stored encrypted in <see cref="KeyValueStore.AllApp"/> (see <see cref="SimpleAuthorizer.AddStoredUser"/>).
        /// </summary>
        public bool AllowStoredUsers = true;

        /// <summary>
        /// The auth (comma separated tokens) required to manage API keys.
        /// null is treated as the default (<see cref="Roles.AdminOps"/>), use <see cref="Roles.Disabled"/> ("-") to disable API key management.
        /// </summary>
        public String ApiKeyManagementAuth = Roles.AdminOps;

        #region Password hashing

        /// <summary>
        /// The string that binds stored password values to this application.
        /// It's part of the simple salt (<see cref="AuthTools.ComputeSimpleSalt(string, string)"/>) and of the SCRAM verifier input.
        /// null (the default) uses the entry assembly name, set it to the same value in all applications that should share a users file.
        /// Changing it invalidates all stored verifiers.
        /// </summary>
        public String AppHashString;

        /// <summary>
        /// The PBKDF2 iteration count of verifiers created by the server (clear text passwords), 0 or less uses <see cref="ScramTools.DefaultIterations"/>.
        /// Verifiers in the "$s1$..." format carry their own iteration count.
        /// </summary>
        public int ScramIterations;

        #endregion//Password hashing

        #region Passkeys

        /// <summary>
        /// If true the users can add passkeys and sign in with them (requires the passkey service).
        /// The passkeys are stored in <see cref="KeyValueStore.AllApp"/>, the users of the file / configuration can't be changed.
        /// API key users (weak authorizations) can never add passkeys.
        /// </summary>
        public bool AllowPasskeys = true;

        #endregion//Passkeys

    }


}
