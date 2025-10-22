using System;

namespace SysWeaver.Net
{
    public sealed class FileProxyParams : CredentialParams
    {
        /// <summary>
        /// The web folder to serve the remove folder at
        /// </summary>
        public String WebRoot;

        /// <summary>
        /// The remove folder to serve
        /// </summary>
        public String SourceRoot;

        /// <summary>
        /// The preferred on the fly compression schemes
        /// </summary>
        public String Compression = "br:Fast, deflate:Fast, gzip:Fast";

        /// <summary>
        /// Number of seconds to cache the file on a client
        /// </summary>
        public int ClientCacheDuration = 5;

        /// <summary>
        /// Number of seconds to cache any intermediate results (i.e small files that are compressed on the fly)
        /// </summary>
        public int ServerCacheDuration = 4;
    
        /// <summary>
        /// If true, proxy requests through tor (tor must be enabled)
        /// </summary>
        public bool UseTor;

        /// <summary>
        /// If true, any bad server certificates are accepted.. NOT RECOMMENDED!
        /// </summary>
        public bool IgnoreCertErrors;
    }

}
