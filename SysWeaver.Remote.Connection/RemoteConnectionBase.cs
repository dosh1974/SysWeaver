using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using SysWeaver.Serialization;
using SysWeaver.Remote.Connection;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SysWeaver.Compression;
using SysWeaver.Net;
using System.IO;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Base class for the runtime generated (IL emitted) classes that implement remote API interfaces, see <see cref="RemoteConnection.Create{T}"/>.
    /// Owns the <see cref="HttpClient"/> used for all calls and contains the GET/POST/PUT/DELETE helpers that the generated methods call.
    /// </summary>
    /// <remarks>
    /// Each instance owns its own <see cref="HttpClient"/> and handler; dispose the instance when it's no longer needed.
    /// All request methods are thread safe and may be called concurrently.
    /// Failures are tracked per HTTP method and exposed through <see cref="GetStats"/>, timings through <see cref="PerfMon"/>.
    /// </remarks>
    public abstract class RemoteConnectionBase : IRemoteApi, IPerfMonitored, IHaveStats
    {
        #region IRemoteApi

        /// <summary>
        /// Invoked before any request is sent (after request payload serialization and compression).
        /// </summary>
        public event RemoteApiCallBegin OnCallBegin;

        /// <summary>
        /// Invoked after any request completes (before response payload deserialization).
        /// </summary>
        public event RemoteApiCallEnd OnCallEnd;

        /// <summary>
        /// Cancels all pending requests on this connection (calls <see cref="HttpClient.CancelPendingRequests"/>).
        /// </summary>
        public void Cancel() => Client.CancelPendingRequests();

        #endregion//IRemoteApi

        /// <summary>
        /// Returns a description of the connection: interface name, base url, Tor usage and the auth method (never any secrets).
        /// </summary>
        /// <returns>A text such as <c>"IMyApi@http://host/api/, auth: Bearer"</c>.</returns>
        public override string ToString() => Tos;

        /// <summary>
        /// Cancels any pending requests and disposes the owned <see cref="HttpClient"/> and handler.
        /// The instance can't be used after disposal.
        /// </summary>
        public void Dispose()
        {
            var c = Client;
            c.CancelPendingRequests();
            c.Dispose();
            var h = ClientHandler;
            h.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The resolved base url (always ending with a single '/'), all endpoints defined in an API are prefixed with this value, ex: "http://localhost:1234/api/".
        /// </summary>
        public readonly String UrlBase;

        /// <summary>
        /// The default serializer used for decoding responses (can be overridden per end point using <see cref="RemoteSerializerAttribute"/>).
        /// </summary>
        public readonly ISerializerType Ser;

        /// <summary>
        /// The default serializer used for encoding request payloads (POST/PUT).
        /// </summary>
        public readonly ISerializerType PostSer;

        /// <summary>
        /// The internal HttpClient that is used, owned by this instance (default headers contain the user agent and any auth).
        /// </summary>
        public readonly HttpClient Client;

        /// <summary>
        /// The message handler used by <see cref="Client"/>, a <see cref="HttpClientTimeoutHandler"/> wrapping a <see cref="HttpClientHandler"/>.
        /// </summary>
        public readonly DelegatingHandler ClientHandler;

        /// <summary>
        /// The resolved default timeout in milliseconds (from <see cref="RemoteConnection.TimeoutInMilliSeconds"/>, else the interface <see cref="RemoteTimeoutAttribute"/>, else 60 000 ms).
        /// </summary>
        /// <remarks>
        /// This is also used as <see cref="HttpClient.Timeout"/>, so a per end point timeout can only shorten, never extend, the request time.
        /// </remarks>
        public readonly int TimeoutInMilliSeconds;

        /// <summary>
        /// If true, urls in exceptions are stripped of scheme, host and query to not disclose sensitive information, see <see cref="GetCleanUrl"/>.
        /// </summary>
        public readonly bool CleanUrl = true;

        /// <summary>
        /// If true, the traffic is routed through the Tor network.
        /// </summary>
        public readonly bool UsingTor;

        /// <summary>
        /// The compression type to use when sending content (null for no compression), the server MUST support the compression method.
        /// </summary>
        public readonly ICompType Compression;

        /// <summary>
        /// The compression level used when compressing request payloads (only used if <see cref="Compression"/> is non-null).
        /// </summary>
        public readonly CompEncoderLevels CompLevel;

        /// <summary>
        /// The performance monitor that tracks the time of every end point call (and cache lookups), named "Interface@BaseUrl".
        /// </summary>
        public PerfMonitor PerfMon { get; private set; }

        /// <summary>
        /// Returns failure statistics (exception counts) per HTTP method, using the system name "Remote " + <see cref="UrlBase"/>.
        /// </summary>
        /// <returns>The stats, lazily enumerated.</returns>
        public IEnumerable<Stats> GetStats()
        {
            var sys = "Remote " + UrlBase;
            foreach (var x in GetFails.GetStats(sys, "Fail.GET."))
                yield return x;
            foreach (var x in PutFails.GetStats(sys, "Fail.PUT."))
                yield return x;
            foreach (var x in PostFails.GetStats(sys, "Fail.POST."))
                yield return x;
            foreach (var x in DeleteFails.GetStats(sys, "Fail.DELETE."))
                yield return x;
        }

        readonly ExceptionTracker GetFails = new ExceptionTracker();
        readonly ExceptionTracker PutFails = new ExceptionTracker();
        readonly ExceptionTracker PostFails = new ExceptionTracker();
        readonly ExceptionTracker DeleteFails = new ExceptionTracker();


        /// <summary>
        /// Creates the connection, resolving defaults and building the <see cref="HttpClient"/>.
        /// Called by the constructor of the generated class.
        /// </summary>
        /// <param name="p">The connection parameters. NOTE: <see cref="RemoteConnection.Serializer"/>, <see cref="RemoteConnection.PostSerializer"/>,
        /// <see cref="RemoteConnection.TimeoutInMilliSeconds"/> and <see cref="RemoteConnection.BaseUrl"/> are updated in place with the resolved values.</param>
        /// <param name="interfaceType">The remote API interface type being implemented (used for naming).</param>
        /// <remarks>
        /// Serializers: <see cref="RemoteConnection.Serializer"/>, else the interface <see cref="RemoteSerializerAttribute"/>, else "json"; the post serializer defaults to the serializer.
        /// The base url is resolved using <see cref="PathTemplate.Resolve(string, System.Collections.Generic.IReadOnlyDictionary{string, string}, bool, bool)"/>; if it then points to an existing file, the first non-comment line is used as the url.
        /// Auth: a non-empty <see cref="RemoteConnection.BearerToken"/> is sent as a Bearer token. Otherwise, for <see cref="RemoteAuthMethod.HttpAuth"/>, credentials with the user name
        /// "bearer" are sent as a Bearer token, a user name starting with '*' sends the password in a custom header named by the rest of the user name (lower cased),
        /// and anything else uses Basic auth (UTF-8 encoded). For <see cref="RemoteAuthMethod.SysWeaverLogin"/> a login is performed synchronously (blocking) during construction.
        /// </remarks>
        /// <exception cref="Exception">Unknown compression, both a proxy and Tor specified, Tor unavailable, an empty base url file or a failed SysWeaver login.</exception>
        /// <exception cref="ArgumentException"><see cref="RemoteConnection.BaseUrl"/> is null.</exception>
        protected RemoteConnectionBase(RemoteConnection p, Type interfaceType)
        {
            PerfMon = new PerfMonitor(interfaceType.Name + "@" + p.BaseUrl);
            var type = GetType();
            var ser = p.Serializer;
            var postSer = p.PostSerializer;
            CleanUrl = p.CleanUrl;
            if (String.IsNullOrEmpty(ser) || String.IsNullOrEmpty(postSer))
            {
                var attr = GetAttribute<RemoteSerializerAttribute>(type);
                if (String.IsNullOrEmpty(ser))
                    ser = attr?.Ser;
                if (String.IsNullOrEmpty(ser))
                    ser = "json";
                if (String.IsNullOrEmpty(postSer))
                    postSer = attr?.PostSer;
                if (String.IsNullOrEmpty(postSer))
                    postSer = ser;
            }
            var comp = p.Compression;
            if (!String.IsNullOrEmpty(comp))
            {
                Compression = CompManager.GetFromHttp(comp);
                if (Compression == null)
                    throw new Exception("Unknown compression format \"" + comp + "\"");
                CompLevel = p.CompLevel;
            }

            p.Serializer = ser;
            p.PostSerializer = postSer;
            if (String.Equals(ser, RemoteParam.FormUrlSerializer, StringComparison.InvariantCultureIgnoreCase) || String.Equals(postSer, RemoteParam.FormUrlSerializer, StringComparison.InvariantCultureIgnoreCase))
                FormUrlSerializer.Register();
            if (String.Equals(ser, RemoteParam.FormUrlIgnoreDefaultsSerializer, StringComparison.InvariantCultureIgnoreCase) || String.Equals(postSer, RemoteParam.FormUrlIgnoreDefaultsSerializer, StringComparison.InvariantCultureIgnoreCase))
                IgnoreDefaultsFormUrlSerializer.Register();

            int timeOut = p.TimeoutInMilliSeconds;
            if (timeOut <= 0)
            {
                var attr = GetAttribute<RemoteTimeoutAttribute>(type);
                timeOut = attr?.TimeOutInMilliSeconds ?? 0;
                if (timeOut <= 0)
                    timeOut = 60000;
            }
            p.TimeoutInMilliSeconds = timeOut;
            TimeoutInMilliSeconds = timeOut;
            Ser = SerManager.Get(ser);
            PostSer = SerManager.Get(postSer);
            //  Proxy/Tor
            var proxy = p.Proxy;
            if (p.UseTor)
            {
                if (proxy != null)
                    throw new Exception("Can't have a Proxy and use Tor at the same time!");
                if (!TorService.IsAvailable)
                    throw new Exception("The SysWeaver.Tor assembly is not available!");
                proxy = TorService.Proxy;
                UsingTor = proxy != null;
            }
            //  Cert validation
            Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> certValid = null;
            if (p.IgnoreCertErrors)
                certValid = (requestMessage, certificate, chain, sslErrors) => true;
            var cv = p.CertValidator;
            if (cv != null)
                certValid = cv;
            var timeOutS = TimeSpan.FromMilliseconds(timeOut);
            var handler = new HttpClientTimeoutHandler
            {
                DefaultTimeout = timeOutS,
                InnerHandler = new HttpClientHandler
                {
                    AutomaticDecompression = p.AcceptedCompressionMethods,
                    MaxConnectionsPerServer = Math.Max(2, p.MaxConcurrency),
                    Proxy = proxy,
                    ServerCertificateCustomValidationCallback = certValid,
                    ClientCertificateOptions = certValid == null ? ClientCertificateOption.Automatic : ClientCertificateOption.Manual,
                    AllowAutoRedirect = p.AllowAutoRedirect,
                }
            };
            ClientHandler = handler;
            var baseUrl = p.BaseUrl;
            if (baseUrl == null)
                throw new ArgumentException("Must specify a base url!", nameof(p.BaseUrl));
            baseUrl = PathTemplate.Resolve(baseUrl);
            if (PathExt.IsValidPathToFile(baseUrl, true))
            {
                var fn = baseUrl;
                baseUrl = FileExt.ReadNonCommentString(baseUrl);
                if (baseUrl == null)
                    throw new Exception("Base url file " + fn.ToFilename() + " must contain at least one line of text!");
            }
            p.BaseUrl = baseUrl;
            UrlBase = baseUrl.TrimEnd('/') + '/';
            var c = new HttpClient(handler)
            {
                DefaultRequestVersion = HttpVersion.Version10,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Timeout = timeOutS,
            };
            Client = c;
            var ua = p.UserAgent;
            if (String.IsNullOrEmpty(ua))
                c.DefaultRequestHeaders.UserAgent.Add(WebTools.UserAgent);
            else
                c.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse(ua));
            String auth = "";
            var b = p.BearerToken;
            if (String.IsNullOrEmpty(b))
            {
                if (p.GetUserPassword(out var user, out var password, false))
                {
                    switch (p.AuthMethod)
                    {
                        case RemoteAuthMethod.HttpAuth:

                            var lu = user.FastToLower();
                            if (lu.FastEquals("baerer") || lu.FastEquals("bearer"))
                            {
                                b = password;
                                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b);
                                auth = ", auth: Bearer";
                            }
                            else
                            {
                                if (lu[0] == '*')
                                {
                                    lu = lu.Substring(1);
                                    b = password;
                                    c.DefaultRequestHeaders.Add(lu, b);
                                    auth = ", auth: " + lu;
                                }
                                else {
                                    var byteArray = Encoding.UTF8.GetBytes(String.Join(":", user, password));
                                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                                    auth = ", auth: Basic";
                                }
                            }
                            break;
                        case RemoteAuthMethod.SysWeaverLogin:
                            var ui = c.SysWeaverLogin(UrlBase + (p.SysWeaverBaseSuffix ?? ""), user, password).RunAsync();
                            if (ui == null)
                                throw new Exception("Failed to perform a SysWeaver login!");
                            if (!ui.Succeeded)
                                throw new Exception("SysWeaver login credentials was invalid!");
                            break;

                    }
                }
            }
            else
            {
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b);
                auth = ", auth: bearer";
            }
            Tos = String.Concat(interfaceType.Name, '@', UrlBase, UsingTor ? " [using Tor]" : "", auth);
        }

        #region Called by the generated class

        #region Return value

        #region AsyncTask

        /// <summary>
        /// Performs a GET request returning a value (used by generated <c>Task&lt;T&gt;</c> methods). Consults and updates the end point cache, keyed by <paramref name="url"/>.
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking and the optional response cache).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> Get<T>(String url, ApiMeta<T> meta, EndPointOptions opt)
        {
            var cache = meta.Cache;
            if (cache != null)
            {
                using (PerfMon.Track("Cache " + meta.Name))
                {
                    if (cache.TryGet(url, out var val))
                        return val;
                }
            }
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    T val;
                    using (var req = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                        val = await ReadResponse<T>(req, opt, HttpEndPointTypes.Get, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                    cache?.AddOrUpdate(url, val);
                    return val;
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        GetFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("GET \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    GetFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Performs a DELETE request returning a value (used by generated <c>Task&lt;T&gt;</c> methods). Consults and updates the end point cache, keyed by <paramref name="url"/>.
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking and the optional response cache).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> Delete<T>(String url, ApiMeta<T> meta, EndPointOptions opt)
        {
            var cache = meta.Cache;
            if (cache != null)
            {
                using (PerfMon.Track("Cache " + meta.Name))
                {
                    if (cache.TryGet(url, out var val))
                        return val;
                }
            }
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    T val;
                    using (var req = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                        val = await ReadResponse<T>(req, opt, HttpEndPointTypes.Delete, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                    cache?.AddOrUpdate(url, val);
                    return val;
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        DeleteFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("DELETE \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    DeleteFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Performs a POST request with a serialized payload returning a value (used by generated <c>Task&lt;T&gt;</c> methods). Never cached.
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> Post<T, D>(String url, ApiMeta<T> meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using var content = CreateContent(out var ser, out var payload, data, opt);
                    using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                    req.Content = content;
                    return await ReadResponse<T>(req, opt, HttpEndPointTypes.Post, ser, payload, content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PostFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("POST \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PostFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Performs a PUT request with a serialized payload returning a value (used by generated <c>Task&lt;T&gt;</c> methods). Never cached.
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> Put<T, D>(String url, ApiMeta<T> meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using var content = CreateContent(out var ser, out var payload, data, opt);
                    using var req = new HttpRequestMessage(HttpMethod.Put, apiUrl);
                    req.Content = content;
                    return await ReadResponse<T>(req, opt, HttpEndPointTypes.Put, ser, payload, content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PutFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("PUT \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PutFails.OnException(ex);
                    throw ex;
                }
            }
        }

        #endregion//AsyncTask


        #region AsyncValueTask

        /// <summary>
        /// GET helper selected for <c>ValueTask&lt;T&gt;</c> interface methods, identical to <see cref="Get{T}"/> (note: it returns a <see cref="Task{TResult}"/>, not a <see cref="ValueTask{TResult}"/>).
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking and the optional response cache).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> ValueGet<T>(String url, ApiMeta<T> meta, EndPointOptions opt)
        {
            var cache = meta.Cache;
            if (cache != null)
            {
                using (PerfMon.Track("Cache " + meta.Name))
                {
                    if (cache.TryGet(url, out var val))
                        return val;
                }
            }
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    T val;
                    using (var req = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                        val = await ReadResponse<T>(req, opt, HttpEndPointTypes.Get, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                    cache?.AddOrUpdate(url, val);
                    return val;
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        GetFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("GET \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    GetFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// DELETE helper selected for <c>ValueTask&lt;T&gt;</c> interface methods, identical to <see cref="Delete{T}"/> (note: it returns a <see cref="Task{TResult}"/>, not a <see cref="ValueTask{TResult}"/>).
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking and the optional response cache).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> ValueDelete<T>(String url, ApiMeta<T> meta, EndPointOptions opt)
        {
            var cache = meta.Cache;
            if (cache != null)
            {
                using (PerfMon.Track("Cache " + meta.Name))
                {
                    if (cache.TryGet(url, out var val))
                        return val;
                }
            }
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    T val;
                    using (var req = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                        val = await ReadResponse<T>(req, opt, HttpEndPointTypes.Delete, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                    cache?.AddOrUpdate(url, val);
                    return val;
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        DeleteFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("DELETE \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    DeleteFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// POST helper selected for <c>ValueTask&lt;T&gt;</c> interface methods, identical to <see cref="Post{T, D}"/> (note: it returns a <see cref="Task{TResult}"/>, not a <see cref="ValueTask{TResult}"/>).
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> ValuePost<T, D>(String url, ApiMeta<T> meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using var content = CreateContent(out var ser, out var payload, data, opt);
                    using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                    req.Content = content;
                    return await ReadResponse<T>(req, opt, HttpEndPointTypes.Post, ser, payload, content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PostFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("POST \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PostFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// PUT helper selected for <c>ValueTask&lt;T&gt;</c> interface methods, identical to <see cref="Put{T, D}"/> (note: it returns a <see cref="Task{TResult}"/>, not a <see cref="ValueTask{TResult}"/>).
        /// </summary>
        /// <typeparam name="T">The response type, deserialized using the end point or connection serializer.</typeparam>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task<T> ValuePut<T, D>(String url, ApiMeta<T> meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using var content = CreateContent(out var ser, out var payload, data, opt);
                    using var req = new HttpRequestMessage(HttpMethod.Put, apiUrl);
                    req.Content = content;
                    return await ReadResponse<T>(req, opt, HttpEndPointTypes.Put, ser, payload, content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PutFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("PUT \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PutFails.OnException(ex);
                    throw ex;
                }
            }
        }

        #endregion//AsyncValueTask

        #endregion//Return value



        #region Void

        #region AsyncTask

        /// <summary>
        /// Performs a GET request without a return value (used by generated <see cref="Task"/> methods). The response body is ignored and never cached.
        /// </summary>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task VoidGet(String url, ApiMeta meta, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                        await WaitResponse(req, opt, HttpEndPointTypes.Get, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        GetFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("GET \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    GetFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Performs a DELETE request without a return value (used by generated <see cref="Task"/> methods). The response body is ignored.
        /// </summary>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task VoidDelete(String url, ApiMeta meta, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                        await WaitResponse(req, opt, HttpEndPointTypes.Delete, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        DeleteFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("DELETE \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    DeleteFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Performs a POST request with a serialized payload without a return value (used by generated <see cref="Task"/> methods).
        /// </summary>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task VoidPost<D>(String url, ApiMeta meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var content = CreateContent(out var ser, out var payload, data, opt))
                    using (var req = new HttpRequestMessage(HttpMethod.Post, apiUrl))
                    {
                        req.Content = content;
                        await WaitResponse(req, opt, HttpEndPointTypes.Post, ser, payload, content).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PostFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("POST \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PostFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Performs a PUT request with a serialized payload without a return value (used by generated <see cref="Task"/> methods).
        /// </summary>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task VoidPut<D>(String url, ApiMeta meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var content = CreateContent(out var ser, out var payload, data, opt))
                    using (var req = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                    {
                        req.Content = content;
                        await WaitResponse(req, opt, HttpEndPointTypes.Put, ser, payload, content).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PutFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("PUT \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PutFails.OnException(ex);
                    throw ex;
                }
            }
        }

        #endregion//AsyncTask



        #region AsyncValueTask

        /// <summary>
        /// GET helper selected for <see cref="ValueTask"/> interface methods, identical to <see cref="VoidGet"/> (note: it returns a <see cref="Task"/>, not a <see cref="ValueTask"/>).
        /// </summary>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task ValueVoidGet(String url, ApiMeta meta, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                        await WaitResponse(req, opt, HttpEndPointTypes.Get, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        GetFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("GET \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    GetFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// DELETE helper selected for <see cref="ValueTask"/> interface methods, identical to <see cref="VoidDelete"/> (note: it returns a <see cref="Task"/>, not a <see cref="ValueTask"/>).
        /// </summary>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task ValueVoidDelete(String url, ApiMeta meta, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                        await WaitResponse(req, opt, HttpEndPointTypes.Delete, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        DeleteFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("DELETE \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    DeleteFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// POST helper selected for <see cref="ValueTask"/> interface methods, identical to <see cref="VoidPost{D}"/> (note: it returns a <see cref="Task"/>, not a <see cref="ValueTask"/>).
        /// </summary>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task ValueVoidPost<D>(String url, ApiMeta meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var content = CreateContent(out var ser, out var payload, data, opt))
                    using (var req = new HttpRequestMessage(HttpMethod.Post, apiUrl))
                    {
                        req.Content = content;
                        await WaitResponse(req, opt, HttpEndPointTypes.Post, ser, payload, content).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PostFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("POST \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PostFails.OnException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// PUT helper selected for <see cref="ValueTask"/> interface methods, identical to <see cref="VoidPut{D}"/> (note: it returns a <see cref="Task"/>, not a <see cref="ValueTask"/>).
        /// </summary>
        /// <typeparam name="D">The request payload type.</typeparam>
        /// <param name="url">The url relative to <see cref="UrlBase"/> (including any query string).</param>
        /// <param name="meta">End point meta data (name used for perf tracking).</param>
        /// <param name="data">The payload, serialized using the end point or connection post serializer (and optionally compressed).</param>
        /// <param name="opt">Optional end point overrides (serializers, timeout), may be null.</param>
        /// <exception cref="HttpResponseException">The server responded with a status code other than 200.</exception>
        /// <exception cref="Exception">Any other failure, wrapped with the HTTP method and (cleaned) url.</exception>
        protected async Task ValueVoidPut<D>(String url, ApiMeta meta, D data, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var content = CreateContent(out var ser, out var payload, data, opt))
                    using (var req = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                    {
                        req.Content = content;
                        await WaitResponse(req, opt, HttpEndPointTypes.Put, ser, payload, content).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is HttpResponseException)
                    {
                        PutFails.OnException(ex);
                        throw;
                    }
                    ex = new Exception("PUT \"" + GetCleanUrl(apiUrl) + "\", failed: " + ex.Message, ex);
                    PutFails.OnException(ex);
                    throw ex;
                }
            }
        }

        #endregion//AsyncValueTask


        #endregion//Void


        #endregion//Called by the generated class

        static T GetAttribute<T>(Type t) where T : Attribute
        {
            var at = typeof(T);
            foreach (var i in t.GetInterfaces())
            {
                var attr = i.GetCustomAttributes(at, true).FirstOrDefault() as T;
                if (attr != null)
                    return attr;
            }
            return null;
        }

        readonly String Tos;

        long ReqId;

        /// <summary>
        /// Removes the scheme, host and query string from a url if <see cref="CleanUrl"/> is true, ex: "https://host/api/Get?key=x" becomes "api/Get".
        /// </summary>
        /// <param name="s">The url to clean.</param>
        /// <returns>The cleaned url (empty if the url only contains a host), or <paramref name="s"/> unchanged if <see cref="CleanUrl"/> is false.</returns>
        public String GetCleanUrl(String s)
        {
            if (!CleanUrl)
                return s;
            if (String.IsNullOrEmpty(s))
                return s;
            var i = s.FastIndexOf("://");
            if (i >= 0)
            {
                i += 3;
                var e = s.IndexOf('/', i);
                if (e < 0)
                    return "";
                i = e + 1;
            }
            else
                i = 0;
            var q = s.IndexOf('?', i);
            if (q < 0)
                q = s.Length;
            return s.Substring(i, q - i);
        }


        async Task<T> ReadResponse<T>(HttpRequestMessage req, EndPointOptions opt, HttpEndPointTypes type, ISerializerType payloadSer, ReadOnlyMemory<Byte> payload, HttpContent content = null)
        {
            long rid = Interlocked.Increment(ref ReqId);
            var timeout = opt?.TimeOutInMilliSeconds ?? 0;
            var cb = OnCallBegin;
            if (cb != null)
                cb?.Invoke(rid, req.RequestUri.ToString(), type, timeout, payloadSer?.Name, payload, (int)(content?.Headers?.ContentLength ?? 0));
            var ce = OnCallEnd;
            try
            {
                if (timeout > 0)
                    req.SetTimeout(TimeSpan.FromMilliseconds(timeout));
                var res = await Client.SendAsync(req).ConfigureAwait(false);
                var c = res.Content;
                Memory<Byte> data = c == null ? null : await c.ReadAsByteArrayAsync().ConfigureAwait(false);
                var code = res.StatusCode;
                var icode = (int)code;
                if (code != HttpStatusCode.OK)
                {
                    Exception fromText = null;
                    try
                    {
                        var ct = res.Content.Headers.ContentType;
                        if (ct != null)
                        {
                            if (ct.MediaType.FastStartsWith("text/"))
                            {
                                Encoding e = Encoding.UTF8;
                                try
                                {
                                    e = Encoding.GetEncoding(ct.CharSet);
                                }
                                catch
                                {
                                }
                                var text = e.GetString(data.Span);
                                if (text.Length > 0)
                                    fromText = new HttpResponseException(icode, text);
                            }
                        }
                    }
                    catch
                    {
                    }
                    if (fromText != null)
                    {
                        if (ce != null)
                            ce?.Invoke(rid, fromText, icode, null, ref data);
                        throw fromText;
                    }
                    var sex = new HttpResponseException(icode, String.Concat(req.Method, " \"", GetCleanUrl(req.RequestUri.ToString()), "\", responded with [", code, ']'));
                    if (ce != null)
                        ce?.Invoke(rid, sex, icode, null, ref data);
                    throw sex;
                }
                var ser = opt?.Ser ?? Ser;
                if (ce != null)
                    ce?.Invoke(rid, null, 200, ser?.Name, ref data);
                return ser.Create<T>(data.Span);
            }
            catch (Exception ex)
            {
                if ((ce != null) && (ex is not HttpResponseException))
                {
                    Memory<Byte> b = null;
                    ce?.Invoke(rid, ex, 0, null, ref b);
                }
                throw;
            }
        }

        async Task WaitResponse(HttpRequestMessage req, EndPointOptions opt, HttpEndPointTypes type, ISerializerType payloadSer, ReadOnlyMemory<Byte> payload, HttpContent content = null)
        {
            long rid = Interlocked.Increment(ref ReqId);
            var timeout = opt?.TimeOutInMilliSeconds ?? 0;
            var cb = OnCallBegin;
            if (cb != null)
                cb?.Invoke(rid, req.RequestUri.ToString(), type, timeout, payloadSer?.Name, payload, (int)(content?.Headers?.ContentLength ?? 0));
            var ce = OnCallEnd;
            try
            {
                if (timeout > 0)
                    req.SetTimeout(TimeSpan.FromMilliseconds(timeout));
                var res = await Client.SendAsync(req).ConfigureAwait(false);
                var code = res.StatusCode;
                if (code != HttpStatusCode.OK)
                {
                    var c = res.Content;
                    Memory<Byte> data = c == null ? null : await c.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var icode = (int)code;
                    Exception fromText = null;
                    try
                    {
                        var ct = res.Content.Headers.ContentType;
                        if (ct != null)
                        {
                            if (ct.MediaType.FastStartsWith("text/"))
                            {
                                Encoding e = Encoding.UTF8;
                                try
                                {
                                    e = Encoding.GetEncoding(ct.CharSet);
                                }
                                catch
                                {
                                }
                                var text = e.GetString(data.Span);
                                if (text.Length > 0)
                                    fromText = new HttpResponseException(icode, text);
                            }
                        }
                    }
                    catch
                    {
                    }
                    if (fromText != null)
                    {
                        if (ce != null)
                            ce?.Invoke(rid, fromText, icode, null, ref data);
                        throw fromText;
                    }
                    var sex = new HttpResponseException(icode, String.Concat(req.Method, " \"", GetCleanUrl(req.RequestUri.ToString()), "\", responded with [", code, ']'));
                    if (ce != null)
                        ce?.Invoke(rid, sex, icode, null, ref data);
                    throw sex;
                }
                if (ce != null)
                {
                    Memory<Byte> data = null;
                    ce?.Invoke(rid, null, 200, null, ref data);
                }
            }
            catch (Exception ex)
            {
                if ((ce != null) && (ex is not HttpResponseException))
                {
                    Memory<Byte> data = null;
                    ce?.Invoke(rid, ex, 0, null, ref data);
                }
                throw;
            }
        }

        HttpContent CreateContent<T>(out ISerializerType ser, out ReadOnlyMemory<Byte> bc, T data, EndPointOptions opt)
        {
            ser = opt?.PostSer ?? PostSer;
            bc = ser.Serialize(data);
            var comp = Compression;
            if (comp != null)
                bc = comp.GetCompressed(bc.Span, CompLevel);
            var content = new ReadOnlyMemoryContent(bc);
            var h = content.Headers;
            var ct = ser.MimeHeader;
            if (comp != null)
            {
                h.Remove("Content-Encoding"); // TODO: Really needed?
                h.Add("Content-Encoding", comp.HttpCode);
            }
            h.Remove("Content-Type"); // TODO: Really needed?
            h.Add("Content-Type", ct);
            return content;
        }


    }

}