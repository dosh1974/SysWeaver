using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Whether the user of a session may call an API, returned by <see cref="ApiHttpServerModule.CanCall(String[], HttpServerRequest)"/> ("Api/auth/CanCall").
    /// A small, purpose built answer for user interfaces (disable buttons the user may not use, name the role that is needed),
    /// without the details of "Api/debug/GetApiInfo" (signature, types, assembly).
    /// </summary>
    public sealed class ApiCallPermission
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(Url, Found ? (Allowed ? " allowed" : " refused") : " not found", Auth == null ? "" : (" (" + Auth + ")"));

        /// <summary>
        /// The url as asked for (relative to the site root, ex: "Api/logFile/DeleteLogFile", "ServerManager/KillProcess").
        /// </summary>
        public String Url;

        /// <summary>
        /// True if an API exists at the url (false for an unknown url, or a url that isn't an API, ex: a page or a file).
        /// </summary>
        public bool Found;

        /// <summary>
        /// True if the user of the session may call the API (false if not found).
        /// </summary>
        public bool Allowed;

        /// <summary>
        /// The auth required by the API, the same text as ApiInfo.Auth: null = anyone (no sign in required), "" = any signed in user,
        /// else a comma separated list of tokens (roles) where any one is enough, ex: "Admin, Debug". Null if not found.
        /// </summary>
        public String Auth;
    }
}
