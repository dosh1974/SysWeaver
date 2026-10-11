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
        /// Password could be a simple hash (a base64 encoded SHA256, see <see cref="AuthTools.ComputeSimplePasswordHash(string, string)"/>, can be generated using the "Generate password hash" debug page).
        /// Simple hashes are unique per user per application (entry assembly name).
        /// Any 44 char password that is valid base64 of 32 bytes is used as a hash (so such a clear text password can't be used).
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
        /// If true (and the server accepts basic auth), then users are allowed to login using basic auth.
        /// Users with the "service" token are always allowed to use basic auth.
        /// </summary>
        public bool AllowBasicAuth;

        /// <summary>
        /// If non-null, API keys can be created and deleted, users logged in using these keys will have the tokens specified here (comma separated).
        /// API keys can be used as bearer tokens, and they are allowed to use basic auth (http server still need to allow for it).
        /// </summary>
        public String ApiKeyAuth = "Service";

        /// <summary>
        /// The auth (comma separated tokens) required to manage API keys.
        /// null is treated as the default (<see cref="Roles.AdminOps"/>), use <see cref="Roles.Disabled"/> ("-") to disable API key management.
        /// </summary>
        public String ApiKeyManagementAuth = Roles.AdminOps;

    }


}
