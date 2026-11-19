using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters for a <see cref="HttpProxy"/>, a module that serves remote http(s) folders under a local web folder.
    /// The optional credentials (user / password) are sent to the remote server as basic auth, or as a bearer token if the user is "bearer".
    /// </summary>
    public sealed class HttpProxyParams : CredentialParams
    {
        /// <summary>
        /// The remote server to get data from, ex: "https://example.com/".
        /// </summary>
        public String SourceServer;

        /// <summary>
        /// If true, proxy requests through tor (tor must be enabled).
        /// </summary>
        public bool UseTor;

        /// <summary>
        /// If true, any bad server certificates are accepted. NOT RECOMMENDED!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// How often to prune disc files
        /// </summary>
        public int PruneMinutes = 15;

        /// <summary>
        /// List of end points to proxy 
        /// </summary>
        public HttpProxyEndPoint[] EndPoints;

        /// <summary>
        /// Comma separated tokens required to access the proxied urls, null for no auth.
        /// </summary>
        public String Auth;

        /// <summary>
        /// If true, the client's Cookie header (including the server's session and device id cookies) is forwarded to the remote server.
        /// Default is false (no client cookies are sent to the remote server).
        /// </summary>
        public bool ForwardCookies;

        /// <summary>
        /// If true, the client's Authorization header is forwarded to the remote server.
        /// Default is false (only the credentials of these parameters, if any, are sent to the remote server).
        /// </summary>
        public bool ForwardAuthorization;

        /// <summary>
        /// If true, allow serving of stale data from memory or disc in the case where the source server isn't reachable.
        /// </summary>
        public bool ServeStale = true;

        /// <summary>
        /// When stale data may be served (see <see cref="ServeStale"/>) and the cached data needs to be validated, this is the maximum number of milliseconds to wait for the source.
        /// If the source hasn't responded in time the stale data is served, and the request to the source continues in the background (updating the cache when done).
        /// This keeps responses quick when the source goes down (or is slow), before it has been detected as offline.
        /// </summary>
        public int StaleWaitMs = 2000;

        /// <summary>
        /// How frequent to check the source for a change (a conditional request using the ETag / Last-Modified of the cached data, a 304 means unchanged).
        /// Within this time cached data is served without contacting the source.
        /// Use zero to validate on every request, or a negative value to use the max-age of the source's Cache-Control header (zero if missing).
        /// </summary>
        public int CheckEtagSeconds = 15;

        /// <summary>
        /// The maximum number of seconds to wait for a response from the source server (including reading the body).
        /// If the source doesn't respond in time (or can't be reached) it's considered offline, see <see cref="OnlineCheckSeconds"/>.
        /// </summary>
        public int RequestTimeoutSeconds = 20;

        /// <summary>
        /// While a source server is considered offline no requests are sent to it (stale data is served if allowed, else a 503 is returned immediately).
        /// This is how often (in seconds) to check if an offline source server is online again.
        /// </summary>
        public int OnlineCheckSeconds = 5;

        /// <summary>
        /// The maximum size (in bytes) of a single response that is cached in memory (larger responses may still be cached on disc).
        /// </summary>
        public int MaxMemItemSize = 16 << 20;

        /// <summary>
        /// The duration (in seconds) for which to cache responses in memory.
        /// Any time a 304 is returned, the in-memory caching is extend to this value (keeping used data in memory).
        /// Set to zero or less to disable caching. The default is 5 minutes.
        /// </summary>
        public int MemCacheDurationSeconds = 5 * 60;

        /// <summary>
        /// The duration (in seconds) for which to cache responses on disc.
        /// Any time a 304 is returned, the disc caching is extend to this value (keeping used data in memory).
        /// Set to zero or less to disable caching. The default is 0 seconds (no disc caching).
        /// </summary>
        public int DiscCacheDurationSeconds;

    }


    public sealed class HttpProxyEndPoint
    {
        /// <summary>
        /// The web folder to serve the remote folder at, ex: "remote/".
        /// If it contains "://" it's an absolute prefix and the proxy only responds to requests using that exact listener prefix.
        /// </summary>
        public String WebRoot;

        /// <summary>
        /// The remote folder to read data from, ex: "files/".
        /// The local url (after the web root) is appended as is, so this should normally end with a '/'.
        /// This is appended to the <see cref="HttpProxyParams.SourceServer"/> to form the full source url (if it contains "://" it's an absolute url and used as is).
        /// </summary>
        public String SourceRoot;

        /// <summary>
        /// Override the default Auth. Use "-" to disable auth for this endpoint, or null to use the <see cref="HttpProxyParams.Auth"/> of the parent <see cref="HttpProxyParams"/>.
        /// Comma separated tokens required to access the proxied urls.
        /// </summary>
        public String Auth;

        /// <summary>
        /// Override the default ForwardCookies setting. Use null to use the <see cref="HttpProxyParams.ForwardCookies"/> of the parent <see cref="HttpProxyParams"/>.
        /// If true, the client's Cookie header (including the server's session and device id cookies) is forwarded to the remote server.
        /// Default is false (no client cookies are sent to the remote server).
        /// </summary>
        public bool? ForwardCookies;

        /// <summary>
        /// Override the default ForwardAuthorization setting. Use null to use the <see cref="HttpProxyParams.ForwardAuthorization"/> of the parent <see cref="HttpProxyParams"/>.
        /// If true, the client's Authorization header is forwarded to the remote server.
        /// Default is false (only the credentials of these parameters, if any, are sent to the remote server).
        /// </summary>
        public bool? ForwardAuthorization;

        /// <summary>
        /// Override the default ServeStale settings. Use null to use the <see cref="HttpProxyParams.ServeStale"/> of the parent <see cref="HttpProxyParams"/>.
        /// </summary>
        public bool? ServeStale;

        /// <summary>
        /// Override the default CheckEtagSeconds settings. Use null to use the <see cref="HttpProxyParams.CheckEtagSeconds"/> of the parent <see cref="HttpProxyParams"/>.
        /// </summary>
        public int? CheckEtagSeconds;


        /// <summary>
        /// Override the default MemCacheDurationSeconds settings. Use null to use the <see cref="HttpProxyParams.MemCacheDurationSeconds"/> of the parent <see cref="HttpProxyParams"/>.
        /// </summary>
        public int? MemCacheDurationSeconds;

        /// <summary>
        /// Override the default DiscCacheDurationSeconds settings. Use null to use the <see cref="HttpProxyParams.DiscCacheDurationSeconds"/> of the parent <see cref="HttpProxyParams"/>.
        /// </summary>
        public int? DiscCacheDurationSeconds;

    }



}
