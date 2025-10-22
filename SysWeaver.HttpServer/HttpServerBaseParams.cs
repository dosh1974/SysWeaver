using System;

namespace SysWeaver.Net
{
    public class HttpServerBaseParams
    {

        public override string ToString() => String.Concat("Session keep alive for: ", SessionExtendLifetime, " minutes, session max lifetime: ", SessionMaxLifetime, " minutes");

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// The session cookie name, EnvInfo variables may be used
        /// </summary>
        public String SessionCookieName = "SysWeaver.Session.[AppName]";

        /// <summary>
        /// The device id name, EnvInfo variables may be used
        /// </summary>
        public String DeviceIdCookieName = "SysWeaver.DeviceId";

        /// <summary>
        /// Maximum lifetime of the session in minutes
        /// </summary>
        public int SessionMaxLifetime = 24 * 60;

        /// <summary>
        /// Number of minutes to keep the session alive after some form of interaction
        /// </summary>
        public int SessionExtendLifetime = 15;

        /// <summary>   
        /// If non-null, the site to redirect to for failed auth.
        /// {0} is the original page
        /// </summary>
        public String AuthRedirect = "auth/Login.html?to={0}";

        /// <summary>   
        /// Set to false to disable any form of auth using the Authorization header (normally auth is only allowed for API-keys).
        /// </summary>
        public bool AllowAuthorizationAuth = true;
        
        /// <summary>
        /// The url to redirect to after logouts
        /// </summary>
        public String LogoutRedirect = "LoggedOut.html";


        /// <summary>
        /// Optional variable that can be used in text templates
        /// </summary>
        public String[] Variables;

        /// <summary>
        /// True to be case sensitive
        /// </summary>
        public bool CaseSensitive = true;

        /// <summary>
        /// Remove any added firewall rules upon exit
        /// </summary>
        public bool RemoveFirewallOnExit = true;

        /// <summary>
        /// Number of minutes to wait before retrying to get a certificate if it failed during start up.
        /// </summary>
        public int FirstCertRetryMinutes = 5;

        /// <summary>
        /// Number of minutes to wait before retrying to get a certificate if it fails during an update.
        /// </summary>
        public int CertRetryMinutes = 60;


        /// <summary>
        /// An optional array of patterns for files that should be used as text templates.
        /// These files must contain text stored as UTF-8.
        /// Patterns can use wildcards '*' (matches one or more) or '?' (matches one).
        /// If the pattern starts with '$' the rest of the pattern is a regular expression.
        /// If the pattern starts with '#' the match should be case insensitive.
        /// If the pattern starts with '$#' the rest of the pattern is a regular expression matched case insensitive.
        /// </summary>
        public String[] Templates;

        /// <summary>
        /// The external URI
        /// </summary>
        public String ExternalRootUri;

        /// <summary>
        /// If true and a translator exist, enable automatic translations
        /// </summary>
        public bool AutoTranslate;

        /// <summary>
        /// Array of languages that are allowed (can be specified in a session).
        /// Auto translation is only valid to one of these (cross sectioned with the supported languages in the translator).
        /// </summary>
        public String[] AllowedLanguages;

        /// <summary>
        /// Allow the Session and Device Cookie to be saved cross origin (needed when iframing in an page).
        /// </summary>
        public bool CorsCookies;

        /// <summary>
        /// Optional, request limiter for the whole server, this applies to ALL calls
        /// </summary>
        public HttpRateLimiterParams ServerLimits;

        /// <summary>
        /// Optional, request limiter for each individual session
        /// </summary>
        public HttpRateLimiterParams SessionLimits;

    }

}



