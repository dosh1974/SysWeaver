using System;
using System.Linq;

namespace SysWeaver.Auth
{
    public sealed class SimpleAuthorizerParams : ManagedFileParams
    {

        public override string ToString() =>
            String.Concat(
                nameof(Users), ": [", String.Join(", ", (Users ?? []).Select(x => x.Split(':')[0].ToQuoted())), "]");

        /// <summary>
        /// One user per string with the following syntax: "username:password".
        /// Auth tokens can be specified by appending a colon (:) and a comma separated list of tokens, ex:
        /// "username:password:token1, token2, token3"
        /// Password could be a simple hash (computed by running the as a command line using the hash command "hash user password").
        /// Simple hashes are unique per user per application (entry assembly name).
        /// </summary>
        public string[] Users;

        /// <summary>
        /// The requirements for passwords.
        /// </summary>
        public PasswordPolicy PasswordPolicy;

        /// <summary>
        /// If true (and the server accepts basic auth, then users are allowed to login using basic auth)
        /// </summary>
        public bool AllowBasicAuth;

        /// <summary>
        /// If non-null, API keys can be created and deleted, users logged in using these keys will have the auth specified here, they will be allowed to use basic auth (http server still need to allow for it)
        /// </summary>
        public String ApiKeyAuth = "Service";

        /// <summary>
        /// The auth required to manage ApiKeys
        /// </summary>
        public String ApiKeyManagementAuth = Roles.AdminOps;

    }


}
