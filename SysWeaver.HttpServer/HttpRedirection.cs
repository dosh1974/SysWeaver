using System;

namespace SysWeaver.Net
{
    public sealed class HttpRedirection
    {
        public override string ToString() => String.Concat(From.ToQuoted(), " => ", To.ToQuoted(), " using ", Code);

        /// <summary>
        /// A http to https upgrade redirection
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
        /// Redirect code, can be 301, 302, 307, 308
        /// </summary>
        public int Code = 302;

    }
}
