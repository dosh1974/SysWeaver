using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using SysWeaver.Compression;
using SysWeaver.Remote.Connection;

namespace SysWeaver.Remote
{


    /// <summary>
    /// Parameters for a specific instance of a remote connection
    /// </summary>
    public class RemoteConnection : CredentialParams
    {

#if DEBUG        
        public override string ToString()
        {
            var b = BearerToken;
            if (!String.IsNullOrEmpty(b))
                return String.Concat("Bearer ", b, '@', BaseUrl);
            if (GetUserPassword(out var user, out var _))
                return String.Concat(user, "Basic ", '@', BaseUrl);
            return BaseUrl;
        }
#endif//DEBUG        


        /// <summary>
        /// The base url, all endpoints defined in an API is prefixed with this value, ex: "http://locahost:1234/api/"
        /// </summary>
        public String BaseUrl;

        /// <summary>
        /// If this is non-empty, the bearer token is sent in the Authorization header (typically this is the API key).
        /// The bearer token can be read from a credentials file where the user name part is bearer, ex:
        /// "CredFile": "Test.txt" and "Test.txt" content is "bearer:the token".
        /// </summary>
        public String BearerToken;

        /// <summary>
        /// The timeout to use for a request, less or equal to zero to uses the timeout attribute on the interface type or if not present 60 000 ms is used.
        /// </summary>
        public int TimeoutInMilliSeconds;

        /// <summary>
        /// Alllow the server to respond with compressed data using these formats
        /// </summary>
        public DecompressionMethods AcceptedCompressionMethods = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;


        /// <summary>
        /// The compression method to use when sending content, the server MUST support the compression method.
        /// null = Uncomporessed.
        /// br = Brotli
        /// deflate = Deflate.
        /// gzip = GZip
        /// </summary>
        public String Compression;

        /// <summary>
        /// The compression level to use when sending content
        /// </summary>
        public CompEncoderLevels CompLevel = CompEncoderLevels.Best;


        /// <summary>
        /// The serializer to use for encoding (POST/PUT) and for decoding responses, encoding can be different if PostSerializer is used, if null or empty the serializer attribute of the interface is used (or else json)
        /// </summary>
        public String Serializer;

        /// <summary>
        /// The serializer to use for encoding (POST/PUT), by default the Serializer is used
        /// </summary>
        public String PostSerializer;

        /// <summary>
        /// Maximum number of concurrent API calls on this connection
        /// </summary>
        public int MaxConcurrency = 32;

        /// <summary>
        /// If true, url's in exceptions is stripped to not disclose sensitive information
        /// </summary>
        public bool CleanUrl = true;

        /// <summary>
        /// Number of seconds to cache remote calls on this connection, typically it's better to specify this per API end point, use 0 to disable caching.
        /// </summary>
        public int CacheDuration;

        /// <summary>
        /// Maximum number of cached calls per api on this connection, typically it's better to specify this per API end point, use 0 to disable caching.
        /// </summary>
        public int MaxCachedItems;

        /// <summary>
        /// An optional proxy to use.
        /// </summary>
        public WebProxy Proxy;

        /// <summary>
        /// If true and no Proxy is specified, route all API calls through the TOR network.
        /// The SysWeaver.Tor assembly must be available.
        /// </summary>
        public bool UseTor;

        /// <summary>
        /// If true, any bad server certificates are accepted.. NOT RECOMMENDED!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// An optional cert validator, return true to accept the request
        /// </summary>
        public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> CertValidator;

        /// <summary>
        /// If true, redirected requests are followed
        /// </summary>
        public bool AllowAutoRedirect;

        /// <summary>
        /// Optional user agent string to use.
        /// </summary>
        public String UserAgent;

        /// <summary>
        /// The auth method to use
        /// </summary>
        public RemoteAuthMethod AuthMethod;

        /// <summary>
        /// The suffix to add to the BaseUrl to get to the service root url (if required)
        /// </summary>
        public String SysWeaverBaseSuffix;

        /// <summary>
        /// Create an instance of a remote connection with the specified parameters (any parameters changes after creation will not affect the created connection)
        /// </summary>
        /// <typeparam name="T">The interface to create an instance of, must inherit IDisposable (and/or optionally IRemoteApi), use RemoteXXXX attributes on interface methods to control the actions</typeparam>
        /// <returns>An instance of T</returns>
        public T Create<T>() where T : class, IDisposable => InterfaceTypeCache<T>.Create(this, typeof(T));


        /// <summary>
        /// Create an instance of a remote connection with the specified parameters (any parameters changes after creation will not affect the created connection)
        /// </summary>
        /// <param name="t">The type of the interface to create an instance of, must inherit IDisposable (and/or optionally IRemoteApi), use RemoteXXXX attributes on interface methods to control the actions</param>
        /// <returns>An instance of the type, must cast to use</returns>
        public IDisposable Create(Type t)
        {
            var c = Cache;
            if (c.TryGetValue(t, out var fn))
                return fn(this);
            lock (c)
            {
                if (c.TryGetValue(t, out fn))
                    return fn(this);
                var cacheType = typeof(InterfaceTypeCache<>).MakeGenericType(t);
                var prop = cacheType.GetField(nameof(InterfaceTypeCache<IDisposable>.Create), BindingFlags.Static | BindingFlags.Public);
                var pval = prop.GetValue(null) as Delegate;
                var p = Expression.Parameter(typeof(RemoteConnection));
                var ce = Expression.Invoke(Expression.Constant(pval), p, Expression.Constant(t));
                var exp = Expression.Convert(ce, typeof(IDisposable));
                var lexp = Expression.Lambda<Func<RemoteConnection, IDisposable>>(exp, p);
                fn = lexp.Compile();
                c[t] = fn;
            }
            return fn(this);
        }



        static readonly Dictionary<Type, Func<RemoteConnection, IDisposable>> Cache = new ();


    }
}


