using System;

namespace SysWeaver.Net
{


    /// <summary>
    /// Parameters for <see cref="NetHttpServer"/>.
    /// </summary>
    public class NetHttpServerParams : HttpServerBaseParams
    {
        /// <summary>
        /// The prefixes to listen on
        /// </summary>
        public HttpServerPrefix[] ListenOn = [
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
        /// Optional instance name of the translator (ITranslator) to use, null for the default instance
        /// </summary>
        public String TranslatorInstance;

        /// <summary>
        /// Optional instance name of the API audit service (IApiAuditService) to use, null for the default instance
        /// </summary>
        public String AuditInstance;


    }
}
