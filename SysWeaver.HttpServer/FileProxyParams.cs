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
        /// The remote folder to serve
        /// </summary>
        public String SourceRoot;

        /// <summary>
        /// If true, proxy requests through tor (tor must be enabled)
        /// </summary>
        public bool UseTor;

        /// <summary>
        /// If true, any bad server certificates are accepted.. NOT RECOMMENDED!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// Auth required to access the proxied urls
        /// </summary>
        public String Auth;
    }

}
