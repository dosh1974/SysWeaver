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
    /// Parameters for a remote connection, and the factory (<see cref="Create{T}"/>) that creates a runtime generated implementation of a remote API interface using them.
    /// Typically deserialized from the "Params" of a service manifest entry whose type is a remote API interface.
    /// </summary>
    /// <remarks>
    /// Credentials (<see cref="CredentialParams.User"/>, <see cref="CredentialParams.Password"/> or <see cref="CredentialParams.CredFile"/>) are used according to <see cref="AuthMethod"/>.
    /// Creating a connection updates some fields of this instance with resolved values (base url, serializers, timeout).
    /// </remarks>
    public class RemoteConnection : CredentialParams
    {

#if DEBUG        
        /// <summary>
        /// DEBUG builds only: returns the base url prefixed with the auth info. NOTE: includes the bearer token in clear text.
        /// </summary>
        public override string ToString()
        {
            var b = BearerToken;
            if (!String.IsNullOrEmpty(b))
                return String.Concat("Bearer ", b, '@', BaseUrl);
            if (GetUserPassword(out var user, out var _, false))
                return String.Concat(user, "Basic ", '@', BaseUrl);
            return BaseUrl;
        }
#endif//DEBUG        


        /// <summary>
        /// The base url, all endpoints defined in an API are prefixed with this value, ex: "http://localhost:1234/api/".
        /// Path templates (see <see cref="PathTemplate"/>) are resolved, and if the result is an existing file, the first non-comment line of that file is used as the url.
        /// Required.
        /// </summary>
        public String BaseUrl;

        /// <summary>
        /// If this is non-empty, the bearer token is sent in the Authorization header (typically this is the API key), and any user credentials are ignored.
        /// The bearer token can also be read from a credentials file where the user name part is bearer, ex:
        /// "CredFile": "Test.txt" and "Test.txt" content is "bearer:the token" (only with <see cref="RemoteAuthMethod.HttpAuth"/>).
        /// </summary>
        public String BearerToken;

        /// <summary>
        /// The default timeout in milliseconds for a request, less or equal to zero to use the <see cref="RemoteTimeoutAttribute"/> on the interface type, or if not present 60 000 ms.
        /// This is also the upper limit for any per end point timeout.
        /// </summary>
        public int TimeoutInMilliSeconds;

        /// <summary>
        /// Allow the server to respond with compressed data using these formats (automatically decompressed).
        /// </summary>
        public DecompressionMethods AcceptedCompressionMethods = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;


        /// <summary>
        /// The compression method to use when sending content, the server MUST support the compression method.
        /// null = Uncompressed.
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
        /// The serializer (file extension name, ex: "json") to use for encoding (POST/PUT) and for decoding responses, encoding can be different if PostSerializer is used, if null or empty the <see cref="RemoteSerializerAttribute"/> of the interface is used (or else "json").
        /// Use <see cref="RemoteParam.FormUrlSerializer"/> for x-www-form-urlencoded payloads (write only).
        /// </summary>
        public String Serializer;

        /// <summary>
        /// The serializer to use for encoding (POST/PUT), if null or empty the <see cref="RemoteSerializerAttribute"/> of the interface is used, or else <see cref="Serializer"/>.
        /// </summary>
        public String PostSerializer;

        /// <summary>
        /// Maximum number of concurrent connections to the server (used as <see cref="HttpClientHandler.MaxConnectionsPerServer"/>, minimum 2).
        /// Calls above this limit are queued by the handler, not rejected.
        /// </summary>
        public int MaxConcurrency = 32;

        /// <summary>
        /// If true, urls in exceptions are stripped of scheme, host and query string to not disclose sensitive information.
        /// </summary>
        public bool CleanUrl = true;

        /// <summary>
        /// Number of seconds to cache GET/DELETE responses on this connection (used by end points without a <see cref="RemoteCacheAttribute"/>), typically it's better to specify this per API end point, use 0 to disable time based caching.
        /// </summary>
        public int CacheDuration;

        /// <summary>
        /// Maximum number of cached responses per end point on this connection (used by end points without a <see cref="RemoteCacheAttribute"/>), typically it's better to specify this per API end point.
        /// Caching is disabled only if both this and <see cref="CacheDuration"/> are 0.
        /// </summary>
        public int MaxCachedItems;

        /// <summary>
        /// An optional proxy to use, can't be combined with <see cref="UseTor"/>.
        /// </summary>
        public WebProxy Proxy;

        /// <summary>
        /// If true, route all API calls through the TOR network (an exception is thrown on creation if a <see cref="Proxy"/> is also specified).
        /// The SysWeaver.Tor assembly must be available.
        /// </summary>
        public bool UseTor;

        /// <summary>
        /// If true, any bad server certificates are accepted. NOT RECOMMENDED! Ignored if <see cref="CertValidator"/> is set.
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// An optional server certificate validator, return true to accept the certificate. Takes precedence over <see cref="IgnoreCertErrors"/>.
        /// </summary>
        public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> CertValidator;

        /// <summary>
        /// If true, redirect responses are followed automatically.
        /// </summary>
        public bool AllowAutoRedirect;

        /// <summary>
        /// Optional user agent string to use (must be a valid product token, ex: "MyApp/1.0"), if null or empty the SysWeaver default user agent is used.
        /// </summary>
        public String UserAgent;

        /// <summary>
        /// The auth method to use for the credentials (ignored if <see cref="BearerToken"/> is set).
        /// </summary>
        public RemoteAuthMethod AuthMethod;

        /// <summary>
        /// The suffix to add to the BaseUrl to get to the service root url (if required), only used for <see cref="RemoteAuthMethod.SysWeaverLogin"/>.
        /// </summary>
        public String SysWeaverBaseSuffix;

        /// <summary>
        /// Create an instance of a remote connection with the specified parameters (any parameters changes after creation will not affect the created connection).
        /// The implementing type is generated once per interface (on first use) and cached.
        /// </summary>
        /// <typeparam name="T">The public interface to create an instance of, must inherit IDisposable (and/or optionally IRemoteApi), use RemoteXXXX attributes on interface methods to control the actions.
        /// All methods must return <see cref="System.Threading.Tasks.Task"/> or <see cref="System.Threading.Tasks.Task{TResult}"/>.</typeparam>
        /// <returns>An instance of T, the caller owns it and should dispose it.</returns>
        /// <exception cref="Exception">The interface is invalid, or the connection could not be set up (see <see cref="RemoteConnectionBase"/>).</exception>
        public T Create<T>() where T : class, IDisposable => InterfaceTypeCache<T>.Create(this, typeof(T));


        /// <summary>
        /// Create an instance of a remote connection with the specified parameters (any parameters changes after creation will not affect the created connection).
        /// Non-generic version of <see cref="Create{T}"/>, the compiled factory delegate is cached per type (thread safe).
        /// </summary>
        /// <param name="t">The type of the interface to create an instance of, must inherit IDisposable (and/or optionally IRemoteApi), use RemoteXXXX attributes on interface methods to control the actions</param>
        /// <returns>An instance of the type, must cast to use. The caller owns it and should dispose it.</returns>
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


