using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters for a <see cref="FileProxy"/>, a module that serves a remote http(s) folder under a local web folder.
    /// The optional credentials (user / password) are sent to the remote server as basic auth, or as a bearer token if the user is "bearer".
    /// </summary>
    public sealed class FileProxyParams : CredentialParams
    {
        /// <summary>
        /// The web folder to serve the remote folder at, ex: "remote/".
        /// If it contains "://" it's an absolute prefix and the proxy only responds to requests using that exact listener prefix.
        /// </summary>
        public String WebRoot;

        /// <summary>
        /// The remote folder to serve, ex: "https://example.com/files/".
        /// The local url (after the web root) is appended as is, so this should normally end with a '/'.
        /// </summary>
        public String SourceRoot;

        /// <summary>
        /// If true, proxy requests through tor (tor must be enabled).
        /// </summary>
        public bool UseTor;

        /// <summary>
        /// If true, any bad server certificates are accepted. NOT RECOMMENDED!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// Comma separated tokens required to access the proxied urls, null for no auth.
        /// </summary>
        public String Auth;
    }

}
