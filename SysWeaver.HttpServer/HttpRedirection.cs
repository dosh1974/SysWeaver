using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// A url prefix redirection rule, used by the <see cref="RedirectHttpServerModule"/> (ex: http to https upgrades).
    /// </summary>
    public sealed class HttpRedirection
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(From.ToQuoted(), " => ", To.ToQuoted(), " using ", Code);

        /// <summary>
        /// A http to https upgrade redirection ("http://*:80/" to "https://*:443/" using 302), a new instance is returned on every call.
        /// </summary>
        public static HttpRedirection HttpToHttps => new HttpRedirection
        {
            From = "http://*:80/",
            To = "https://*:443/",
        };

        /// <summary>
        /// If the url starts with this value a redirection will happen.
        /// * is replaced with the host that is performing the request.
        /// Example: "http://*:80/"
        /// </summary>
        public String From;

        /// <summary>
        /// The matching part is replaced with this.
        /// * is replaced with the host that is performing the request.
        /// Example:"https://*:443/"
        /// </summary>
        public String To;

        /// <summary>
        /// Redirect status code, can be 301, 302, 307 or 308.
        /// </summary>
        public int Code = 302;

    }
}
