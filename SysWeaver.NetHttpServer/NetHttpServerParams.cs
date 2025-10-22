using System;

namespace SysWeaver.Net
{


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
        /// Optional translator instance name 
        /// </summary>
        public String TranslatorInstance;

        /// <summary>
        /// Optional audit instance name 
        /// </summary>
        public String AuditInstance;


    }
}
