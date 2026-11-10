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
        /// Non-ASCII chars are escaped. Setting this to null disables the session cookie, every request then gets a new session (as for a client that doesn't store cookies).
        /// </summary>
        public String SessionCookieName = "SysWeaver.Session.[AppName]";

        /// <summary>
        /// The device id cookie name, EnvInfo variables may be used, an empty string uses the default.
        /// The device id is a random value that is stored in a cookie (max one year) and reported in the session data.
        /// </summary>
        public String DeviceIdCookieName = "SysWeaver.DeviceId";

        /// <summary>
        /// Lifetime of the session cookie in minutes (capped to one year), at least 10 * <see cref="SessionExtendLifetime"/> + 30 minutes.
        /// The server side session expires earlier if it's not used (see <see cref="SessionExtendLifetime"/>), a cookie of an expired session is useless (a new session is created).
        /// The cookie is only set when a session starts (or its token is rotated, ex: at a sign in), so this is also the longest time a continuously used session can live.
        /// Default 7 days (it used to be one year).
        /// </summary>
        public int SessionCookieLifetime = 7 * 24 * 60;

        /// <summary>
        /// The SameSite attribute of the session and device id cookies: "Lax" (default), "Strict" or empty (no attribute, the browser default).
        /// "Lax": the cookies are sent for requests from this site and for top level navigations from other sites (ex: a link in an email opens the app signed in),
        /// but not for requests that other sites make (images, forms, fetch, frames on another site): a basic cross site request forgery protection.
        /// "Strict": the cookies are not even sent when following a link from another site (the user appears signed out on the first page).
        /// Ignored when <see cref="CorsCookies"/> is true (that uses "SameSite=None;Secure").
        /// The "Secure" attribute is always added for https requests.
        /// </summary>
        public String CookieSameSite = "Lax";

        /// <summary>
        /// Who may show the pages of this server in a frame (clickjacking protection): the value of the Content-Security-Policy "frame-ancestors" directive that is sent with every response.
        /// "'self'" (default): only pages of this site (same scheme, host and port) may frame it (also sends "X-Frame-Options: SAMEORIGIN" for old browsers).
        /// "'none'": never in a frame (also sends "X-Frame-Options: DENY").
        /// A space separated list of sources allows other sites, ex: "'self' https://portal.mydomain.com https://*.mydomain.com" (no X-Frame-Options is sent then).
        /// Null, empty or "*" sends no header (anyone may frame the site, the old behaviour).
        /// </summary>
        public String FrameAncestors = "'self'";

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
        [EditType(EditTypes.Url)]
        public String ExternalRootUri;

        /// <summary>
        /// The proxies that may tell the address of the client (the "Forwarded" or "X-Forwarded-For" header): when the connection comes from
        /// one of them, <see cref="HttpServerRequest.GetIpAddress"/> is the forwarded client address (and <see cref="HttpServerRequest.IsForwarded"/>
        /// is true). Every entry is an address ("10.0.0.5", "fd00::1"), a network ("10.0.0.0/8", "fd00::/8"), "loopback" (127.0.0.0/8, ::1)
        /// or "private" (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16, fc00::/7, fe80::/10).
        /// Null (the default) = "loopback" and "private" (a proxy on the same machine or network, ex: a SysWeaver reverse proxy).
        /// An empty array = never (the address of the connection is always used).
        /// A client that connects directly from a trusted network can set the header itself: only trust networks where every machine is a proxy
        /// or is trusted not to lie about its address.
        /// </summary>
        public String[] TrustedProxies;

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
        /// If false the cookies use "HttpOnly" and the SameSite attribute of <see cref="CookieSameSite"/> ("Secure" is added for https requests).
        /// When the site is shown in a frame on another site, also allow that site in <see cref="FrameAncestors"/>.
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

        /// <summary>
        /// Responses with a known length (in memory or a seekable stream) that are smaller than this number of bytes are sent uncompressed (no Content-Encoding header),
        /// since compressing a few bytes costs more than it saves (the compressed output is often larger than the input).
        /// Pre-compressed data that the client doesn't accept is still decompressed.
        /// Zero (or a negative value) disables this, compressible responses are then always compressed.
        /// </summary>
        public int MinCompressSize = 128;

        /// <summary>
        /// If true, the web client of a DEBUG build also sends api arguments with the release ("optimized") encodings: GET with the json,
        /// "_u" (base64url UTF-8) or "_d" (base64url deflated) query when small enough, else POST (see core/net.js Sw.Net.Send).
        /// Release builds always do this (the client's debug-only code is stripped); by default debug builds POST every argument (easier to read in the browser tools),
        /// so encoding problems that only happen in release builds are not seen while debugging. Set this to true to test the release encodings in a debug build.
        /// Published to the client as the template variable "Client.OptimizedSend" ("true" / "false") in the templated "app/manifest.json" ("sysweaver_optimized_send").
        /// </summary>
        public bool OptimizedSend;

    }

}



