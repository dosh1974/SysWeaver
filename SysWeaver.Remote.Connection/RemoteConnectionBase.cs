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

namespace SysWeaver.Remote
{

    /// <summary>
    /// Base class for remote connection implmentations
    /// </summary>
    public abstract class RemoteConnectionBase : IRemoteApi, IPerfMonitored, IHaveStats
    {
        #region IRemoteApi

        /// <summary>
        /// Invoked before any request (after request payload serilization)
        /// </summary>
        public event RemoteApiCallBegin OnCallBegin;

        /// <summary>
        /// Invoked after any request (before response payload deserilization)
        /// Will always get called if OnRequestBegin is called
        /// </summary>
        public event RemoteApiCallEnd OnCallEnd;

        /// <summary>
        /// Cancels all pending request on the remote connection 
        /// </summary>
        public void Cancel() => Client.CancelPendingRequests();

        #endregion//IRemoteApi

        public override string ToString() => Tos;

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
        /// The base url, all endpoints defined in an API is prefixed with this value, ex: "http://locahost:1234/api/"
        /// </summary>
        public readonly String UrlBase;

        /// <summary>
        /// The serializer to use for for decoding responses
        /// </summary>
        public readonly ISerializerType Ser;

        /// <summary>
        /// The serializer to use for encoding (POST/PUT)
        /// </summary>
        public readonly ISerializerType PostSer;

        /// <summary>
        /// The internal HttpClient that is used
        /// </summary>
        public readonly HttpClient Client;

        /// <summary>
        /// The internal HttpClientHandler that is used
        /// </summary>
        public readonly DelegatingHandler ClientHandler;

        /// <summary>
        /// The timeout to use for a request, less or equal to zero to uses the timeout attribute on the interface type or if not present 60 000 ms is used.
        /// </summary>
        public readonly int TimeoutInMilliSeconds;

        /// <summary>
        /// If true, url's in exceptions is stripped to not disclose sensitive information
        /// </summary>
        public readonly bool CleanUrl = true;

        /// <summary>
        /// If true, the traffic is routed through the Tor network.
        /// </summary>
        public readonly bool UsingTor;

        /// <summary>
        /// The compression type to use when sending content, the server MUST support the compression method.
        /// </summary>
        public readonly ICompType Compression;

        /// <summary>
        /// The compression level
        /// </summary>
        public readonly CompEncoderLevels CompLevel;

        /// <summary>
        /// The performance monitor instance
        /// </summary>
        public PerfMonitor PerfMon { get; private set; }

        /// <summary>
        /// Return some stats
        /// </summary>
        /// <returns>The stats</returns>
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
                    AllowAutoRedirect = p.AllowAutoRedirect,
                },
            };
            ClientHandler = handler;
            UrlBase = p.BaseUrl;
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
                                auth = ", auth: bearer";
                            }
                            else
                            {
                                var byteArray = Encoding.ASCII.GetBytes(String.Join(":", user, password));
                                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                                auth = ", auth: basic";
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
            }else
            {
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b);
                auth = ", auth: bearer";
            }
            Tos = String.Concat(interfaceType.Name, '@', UrlBase, UsingTor ? " [using Tor]" : "", auth);
        }

        #region Called by the generated class

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
                    if (ex is EndPointException)
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
                    if (ex is EndPointException)
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
                    if (ex is EndPointException)
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
                    if (ex is EndPointException)
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
                    if (ex is EndPointException)
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

        protected async Task VoidDelete(String url, ApiMeta meta, EndPointOptions opt)
        {
            using (PerfMon.Track(meta.Name))
            {
                var apiUrl = UrlBase + url;
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                        await WaitResponse(req, opt, HttpEndPointTypes.Get, null, ReadOnlyMemory<Byte>.Empty).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex is EndPointException)
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
                    if (ex is EndPointException)
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
                    if (ex is EndPointException)
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

        public String GetCleanUrl(String s)
        {
            if (!CleanUrl)
                return s;
            if (String.IsNullOrEmpty(s))
                return s;
            var i = s.IndexOf("://");
            if (i >= 0)
            {
                i += 3;
                var e = s.IndexOf('/', i);
                if (e < 0)
                    return "";
                i = e + 1;
            } else
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
                if (code != HttpStatusCode.OK)
                {
                    var icode = (int)code;
                    var sex = new EndPointException(req.Method + " \"" + GetCleanUrl(req.RequestUri.ToString()) + "\", responded with " + icode + " [" + res.StatusCode + "]", icode);
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
                if ((ce != null) && (ex is not EndPointException))
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
                    var sex = new EndPointException(req.Method + " \"" + GetCleanUrl(req.RequestUri.ToString()) + "\", responded with " + icode + " [" + res.StatusCode + "]", icode);
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
                if ((ce != null) && (ex is not EndPointException))
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
