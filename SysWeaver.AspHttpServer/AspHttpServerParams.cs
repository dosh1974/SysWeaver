namespace SysWeaver.Net
{
    public class AspHttpServerParams : HttpServerBaseParams
    {
        /// <summary>
        /// The prefixes to listen on
        /// </summary>
        public HttpServerPrefix[] ListenOn =
        [
            HttpServerPrefix.DefaultExternalHttps
        ];

        /// <summary>
        /// If any of the specified prefixes fails, the server will still work
        /// </summary>
        public bool IgnoreBadPrefixes = true;

        /// <summary>
        /// When shutting down, wait for any pending request for these many seconds
        /// </summary>
        public int SecondsToWait = 30;

        /// <summary>
        /// Set to true to enable http3, requires Windows 11 (or libmsquic on Linux).
        /// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3?view=aspnetcore-8.0
        /// </summary>
        public bool EnableHttp3;
    }



}
