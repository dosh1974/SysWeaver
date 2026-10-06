using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters common to all <see cref="HttpServerBase"/> implementations (sessions, auth, templates, languages, rate limits etc).
    /// </summary>
    public class HttpServerBaseParams
    {

        /// <inheritdoc/>
        public override string ToString() => String.Concat("Session keep alive for: ", SessionExtendLifetime, " minutes, session cookie lifetime: ", SessionCookieLifetime, " minutes");

        /// <summary>
        /// Enable performance monitoring (not read by <see cref="HttpServerBase"/>, implementations may use it).
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// The session cookie name, EnvInfo variables may be used (ex: "[AppName]"), an empty string uses the default.
        /// Non-ASCII chars are escaped. Setting this to null disables sessions, which is not supported (requests will fail).
        /// </summary>
        public String SessionCookieName = "SysWeaver.Session.[AppName]";

        /// <summary>
        /// The device id cookie name, EnvInfo variables may be used, an empty string uses the default.
        /// The device id is a random value that is stored in a cookie (max one year) and reported in the session data.
        /// </summary>
        public String DeviceIdCookieName = "SysWeaver.DeviceId";

        /// <summary>
        /// Lifetime of the session cookie in minutes (capped to one year), at least 10 * <see cref="SessionExtendLifetime"/> + 30 minutes.
        /// The server side session expires earlier if it's not used (see <see cref="SessionExtendLifetime"/>).
        /// </summary>
        public int SessionCookieLifetime = 365 * 24 * 60;

        /// <summary>
        /// Number of minutes to keep the session alive after some form of interaction (at least 1).
        /// Sessions with 3 or fewer requests that have no strongly authenticated user expire after 30 seconds of inactivity.
        /// </summary>
        public int SessionExtendLifetime = 15;

        /// <summary>
        /// If non-null, the page (relative to the site root) to redirect to when a protected resource is requested without any auth.
        /// {0} is replaced with the url encoded original url (the query string of the redirect is removed if the original resource isn't a ".html" page).
        /// If null, a 401 response is sent instead.
        /// </summary>
        public String AuthRedirect = "auth/Login.html?to={0}";

        /// <summary>
        /// Set to false to disable any form of auth using the Authorization header (Basic, Bearer) and the "x-api-key" / "x-goog-api-key" headers.
        /// A user authenticated this way is stored in the session (the session cookie can be used for subsequent requests).
        /// </summary>
        public bool AllowAuthorizationAuth = true;
        
        /// <summary>
        /// The url to redirect to after logouts (currently not used by <see cref="HttpServerBase"/>).
        /// </summary>
        public String LogoutRedirect = "LoggedOut.html";


        /// <summary>
        /// Optional "Key=Value" strings that are available as variables ("${Key}") in text templates.
        /// </summary>
        public String[] Variables;

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
        /// An optional array of patterns for files that should be used as text templates (in addition to the built-in ones, ex: "index.html").
        /// These files must contain text stored as UTF-8.
        /// Patterns can use wildcards '*' (matches zero or more) or '?' (matches one).
        /// If the pattern starts with '$' the rest of the pattern is a regular expression.
        /// If the pattern starts with '#' the match should be case insensitive.
        /// If the pattern starts with '$#' the rest of the pattern is a regular expression matched case insensitive.
        /// </summary>
        public String[] Templates;

        /// <summary>
        /// The external root uri (ex: "https://www.mydomain.com/"), used when building absolute links for use outside of a request (ex: in emails).
        /// If null, the local prefix (set by the implementation) or the prefix of the first request that is handled is used (see <see cref="HttpServerBase.ExternalRootUri"/>).
        /// </summary>
        public String ExternalRootUri;

        /// <summary>
        /// If true and a translator exists, enable automatic translations of web assets and API responses.
        /// </summary>
        public bool AutoTranslate;

        /// <summary>
        /// Array of languages that are allowed (can be specified in a session).
        /// Auto translation is only valid to one of these (cross sectioned with the supported languages in the translator).
        /// </summary>
        public String[] AllowedLanguages;

        /// <summary>
        /// Allow the session and device id cookies to be sent cross origin ("SameSite=None;Secure", needed when the site is shown in an iframe on another site).
        /// If false the cookies only use "HttpOnly" (no Secure or SameSite attribute).
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



