using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Security;
using SysWeaver.Translation;

namespace SysWeaver.Net
{

    public class NetHttpServer : HttpServerBase, IDisposable
    {

        static Exception ValidatePrefix(String p)
        {
            try
            {
                var l = new HttpListener();
                l.Prefixes.Add(p);
                l.Start();
                l.Stop();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public NetHttpServer(IMessageHost msg, ITranslator translator, IApiAuditService audit, IReadOnlyDictionary<String, ICertificateProvider> certProviders, AuthManager auth, IFirewallHandler firewallHandler, NetHttpServerParams p = null)
            : base(msg, translator, audit, auth, firewallHandler, p, typeof(HttpListenerException))
        {
            if (!HttpListener.IsSupported)
                throw new Exception(Prefix + "Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
            p = p ?? new NetHttpServerParams();
            PerfMon.Enabled = p.PerMon;
            var listenOn = p.ListenOn;
            if ((listenOn == null) || (listenOn.Length <= 0) || (listenOn[0] == null))
                listenOn = [HttpServerPrefix.DefaultExternalHttps];
            HttpListener l = new HttpListener();
            var duration = TimeSpan.FromMinutes(5);
            var timeOut = l.TimeoutManager;
            timeOut.DrainEntityBody = duration;
            timeOut.IdleConnection = duration;
            l.IgnoreWriteExceptions = true;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
#pragma warning disable CA1416
                timeOut.EntityBody = duration;
                timeOut.HeaderWait = duration;
                timeOut.MinSendBytesPerSecond = 1;
                timeOut.RequestQueue = duration;
#pragma warning restore
            }
            var prefixes = new List<HttpServerPrefix>();
            var seen = new HashSet<string>();
            //var certs = new Dictionary<int, Tuple<ICertificateProvider, HttpServerPrefix>>();
            var certs = new List<Tuple<int, ICertificateProvider, HttpServerPrefix>>();

            //ServicePointManager.CheckCertificateRevocationList = true;
            /*            ServicePointManager.UseNagleAlgorithm = true;
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.DefaultConnectionLimit = 100000;
              */
            bool haveFirewall = false;
            String localPrefix = null;
            foreach (var prefix in listenOn)
            {
                if (prefix == null)
                    continue;
                var pre = prefix.Prefix?.Trim();
                if (String.IsNullOrEmpty(pre))
                    continue;
                var certName = prefix.Certificate?.Trim();
                ICertificateProvider cp = null;
                if (!String.IsNullOrEmpty(certName))
                {
                    if (certProviders == null)
                        throw new Exception(Prefix + "No certificate providers supplied!");
                    if (certName == "*")
                    {
                        var ff = certProviders.FirstOrDefault();
                        cp = ff.Value;
                        if (cp == null)
                            throw new Exception(Prefix + "No certificate provider found, disable certificates or add a certificate provider!");
                        certName = ff.Key;
                    }
                    else
                    {
                        certProviders.TryGetValue(certName, out cp);
                        if (cp == null)
                            throw new Exception(Prefix + "The certificate provider named " + certName.ToQuoted() + " isn't found!");
                    }
                }
                pre = HttpServerPrefix.FixPrefix(pre);
                if (String.IsNullOrEmpty(pre))
                    continue;
                if (!seen.Add(pre))
                    continue;
                if ((cp == null) && pre.FastStartsWith("https://"))
                {
                    if (p.IgnoreBadPrefixes)
                    {
                        msg?.AddMessage(Prefix + "Can't listen on prefix \"" + pre + "\" without a certificate", MessageLevels.Warning);
                        continue;
                    }
                    else
                    {
                        throw new Exception(Prefix + "Can't listen on prefix \"" + pre + "\" without a certificate");
                    }
                }
                var ex = ValidatePrefix(pre);
                if (ex != null)
                {
                    if (p.IgnoreBadPrefixes)
                    {
                        msg?.AddMessage(Prefix + "Can't listen on prefix \"" + pre + "\", exception: " + ex.Message, MessageLevels.Warning);
                        continue;
                    }
                    else
                    {
                        throw new Exception(Prefix + "Can't listen on prefix \"" + pre + "\", exception: " + ex.Message, ex);
                    }
                }
                if (certName != null)
                    msg?.AddMessage(Prefix + "Listening on prefix \"" + pre + "\", using certificate " + certName.ToQuoted() + " [" + cp + "]", MessageLevels.Info);
                else
                    msg?.AddMessage(Prefix + "Listening on prefix \"" + pre + "\"", MessageLevels.Info);
                l.Prefixes.Add(pre);
                var pc = prefix.Clone();
                pc.Prefix = pre;
                prefixes.Add(pc);
                haveFirewall |= prefix.AddToFirewall;
                var uri = new Uri(pre.Replace("*", "localhost"));
                localPrefix = localPrefix ?? uri.ToString();
                if (ExternalRootUri == null)
                    ExternalRootUri = localPrefix;
                if (cp != null)
                {
                    var port = uri.Port;
                    certs.Add(Tuple.Create(port, cp, prefix));

                    /*                    if (certs.TryGetValue(port, out var e))
                                        {
                                            if (e.Item1 != cp)
                                                msg?.AddMessage(Prefix + "Prefix " + prefix.Prefix.ToQuoted() + " is assigned a different certificate provider than the previously seen prefix " + e.Item2.Prefix.ToQuoted() + ".\nThey are using the same port, this won't work on all platform, using the provider for the latter.", MessageLevels.Warning);
                                        }
                                        else
                                        {
                                            certs[port] = Tuple.Create(cp, prefix);
                                        }*/
                }
#pragma warning disable SYSLIB0014
                var sp = ServicePointManager.FindServicePoint(uri);
                sp.ConnectionLimit = 100000;
                sp.UseNagleAlgorithm = true;
                sp.Expect100Continue = true;
#pragma warning restore
            }
            LocalUri = localPrefix;


            //  Get and bind certificates
            if (certs.Count > 0)
            {
                msg?.AddMessage(Prefix + "Binding certificates to ports");
                using var t = msg?.Tab();
                {
                    foreach (var x in certs)
                    {
                        var port = x.Item1;
                        var provider = x.Item2;
                        var pre = x.Item3.Prefix;
                        provider.OnChanged += newCert => OnCertChanged(provider, pre);
                        var cert = provider.GetCert().RunAsync();
                        if (cert == null || (!CertificateBinder.BindHttps(pre, cert, msg, Prefix).RunAsync()))
                        {
                            var retry = DateTime.UtcNow.AddMinutes(FirstCertRetryMinutes);
                            msg?.AddMessage(Prefix + "Will try to get new certificate at " + retry.ToString("o"));
                            Scheduler.AddTask(retry, () => SwitchCert(provider, pre));
                        }else
                        {
                            CurrentCertThumbPrint = cert.Thumbprint;
                        }
                    }
                }
            }
            SetPrefixes(prefixes, haveFirewall);
            Listener = l;
            SecondsToWait = Math.Max(1, p.SecondsToWait);
        }

        protected override HttpServerRequest ReplaceUrl(HttpServerRequest s, string newUrl, String newMethod = null)
        {
            var o = s as NetHttpServerRequest;
            var host = GetHost(out var prefix, out var queryStart, ref newUrl);
            if (prefix == null)
                return null;
            var h = new NetHttpServerRequest(o.Context, newUrl, prefix, this, host, queryStart, newMethod);
            h.Init(s.Session);
            return h;
        }


        public readonly int SecondsToWait;





        readonly HttpListener Listener;
        SemaphoreSlim IsListening;

        public bool IsStopped
        {
            get
            {
                lock (Listener)
                    return IsListening == null;
            }
        }

        protected override Task<bool> OnNewCert(ICertificateProvider certProvider, String pre)
        {
            lock (Listener)
            {
                var cert = certProvider.GetCert().RunAsync();
                if (cert == null)
                    return TaskExt.FalseTask;
                var res = CertificateBinder.BindHttps(pre, cert, Msg, Prefix).RunAsync();
                CurrentCertThumbPrint = cert.Thumbprint;
                if (res && (!IsPaused))
                {
                    Stop();
                    Start();
                }
                return TaskExt.TrueTask;
            }
        }

        public bool Start()
        {
            lock (Listener)
            {
                if (IsListening != null)
                    return false;
                Msg?.AddMessage(Prefix + "Starting", MessageLevels.Debug);
                IsListening = new SemaphoreSlim(0, 1);
                Listener.Start();
                IsPaused = false;
                Task.Run(() => Dispatcher().ConfigureAwait(false)).ConfigureAwait(false);
            }
            return true;
        }

        public bool Stop()
        {
            lock (Listener)
            {
                var l = IsListening;
                if (l == null)
                    return false;
                Msg?.AddMessage(Prefix + "Stopping", MessageLevels.Debug);
                //  No new reuests accepeted
                IsPaused = true;
                //  Telling ongoing requests that they should stop ASAP
                InvokeCancel();
                //  Given pending request a change to complete (around 3 seconds at most)
                for (int i = 0; i < 30; ++i)
                {
                    if (Interlocked.Read(ref ReqCounter) == 0)
                        break;
                    //  Sleep a bit if there are any ongoing requests
                    Thread.Sleep(100);
                }
                //  Stop listening
                Listener.Stop();
                l.Wait();
                IsListening = null;
            }
            Msg?.AddMessage(Prefix + "Stopped", MessageLevels.Debug);
            return true;
        }


        public override void Pause()
        {
            lock (Listener)
            {
                if (IsPaused)
                    return;
                if (IsListening == null)
                    return;
                Msg?.AddMessage(Prefix + "Pausing", MessageLevels.Debug);
                RunBeforePause();
                IsPaused = true;
                InvokeCancel();
                if (Interlocked.Read(ref ReqCounter) == 0)
                    Stop();
            }
        }

        public override void Continue()
        {
            lock (Listener)
            {
                if (!IsPaused)
                    return;
                Start();
                Msg?.AddMessage(Prefix + "Resuming", MessageLevels.Debug);
                IsPaused = false;
                RunAfterContinue();
            }
        }


        long ReqCounter;


        readonly ExceptionTracker DispatchErrors = new ExceptionTracker();

        public override IEnumerable<Stats> GetStats()
        {
            foreach (var x in base.GetStats())
                yield return x;
            foreach (var x in DispatchErrors.GetStats(nameof(NetHttpServer), "DispatchEx."))
                yield return x;
        }

        /// <summary>
        /// Listens for incoming request, tries to dispatch the request to a separate thread as quickly as possible.
        /// This is a long running task, maybe it should be moved to a separate thread.
        /// </summary>
        /// <returns></returns>
        async Task Dispatcher()
        {
            Msg?.AddMessage(Prefix + "Started", MessageLevels.Debug);
            var l = Listener;
            while (l.IsListening)
            {
                if (IsPaused)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                    continue;
                }
                try
                {
                    var c = await l.GetContextAsync().ConfigureAwait(false);
                    Interlocked.Increment(ref ReqCounter);
                    TaskExt.StartNewAsyncChain(() => HandleRequest(c).ConfigureAwait(false));
                    continue;
                }
                catch (Exception ex)
                {
                    DispatchErrors.OnException(ex);
                }
                Thread.Sleep(0);
            }
            //  Wait for all pending requests handlers to complete
            var now = DateTime.UtcNow;
            var waitDuration = SecondsToWait;
            long pending = 0;
            for (; ; )
            {
                pending = Interlocked.Read(ref ReqCounter);
                if (pending == 0)
                    break;
                if ((DateTime.UtcNow - now).TotalSeconds >= waitDuration)
                {
                    Msg?.AddMessage(Prefix + pending + " request failed to complete within " + waitDuration + " seconds", MessageLevels.Warning);
                    break;
                }
                await Task.Delay(10).ConfigureAwait(false);
            }
            IsListening?.Release();
        }

        async ValueTask WriteResponseString(HttpListenerResponse res, String text, String mime = HttpServerTools.TextMime)
        {
            var e = Encoding.UTF8;
            var data = e.GetBytes(text);
            var rheaders = res.Headers;
            rheaders[HttpResponseHeader.ContentType] = mime;
            await res.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        }


        async ValueTask HandlePaused(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.StatusCode = 503;
            var lang = await GetAcceptLanguage(req.Headers["Accept-Language"]).ConfigureAwait(false);
            var text = await Translator.TranslateSafe("The service is temporarily paused", lang).ConfigureAwait(false);
            await WriteResponseString(res, text).ConfigureAwait(false);
        }

        async ValueTask Handle404(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.StatusCode = 404;
            var lang = await GetAcceptLanguage(req.Headers["Accept-Language"]).ConfigureAwait(false);
            var text = await Get404Text(lang).ConfigureAwait(false);
            await WriteResponseString(res, text).ConfigureAwait(false);
        }


        /// <summary>
        /// Handles a single incoming request
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        async ValueTask HandleRequest(HttpListenerContext c)
        {
            using (PerfMon.Track(nameof(HandleRequest)))
            {
                var res = c.Response;
                String url = "";
                try
                {
                    var req = c.Request;
                    if (IsPaused)
                    {
                        await HandlePaused(req, res).ConfigureAwait(false);
                        return;
                    }
                    url = req.Url.AbsoluteUri;
                    var host = GetHost(out var prefix, out var queryStart, ref url);
                    if (prefix == null)
                    {
                        await Handle404(req, res).ConfigureAwait(false);
                        return;
                    }
                    using var data = new NetHttpServerRequest(c, url, prefix, this, host, queryStart);
                    data.SetResHeader("Server", "");
                    await Handle(data).ConfigureAwait(false);
                }
                catch (HttpListenerException ex)
                {
                    ListenerExceptions.OnException(ex);
                }
                catch (Exception ex)
                {
                    RequestExceptions.OnException(ex);
#if DEBUG
                    Msg?.AddMessage(Prefix + "Request failed!", ex, MessageLevels.Debug);
#endif//DEBUG
                    try
                    {
                        res.StatusCode = 500;
                    }
                    catch
                    {
                    }
                }
                finally
                {
                    try
                    {
                        res.OutputStream.Close();
                        //res.Headers["Server"] = "";
                        res.Close();
                    }
                    catch (Exception ex)
                    {
                        RequestExceptions.OnException(ex);
#if DEBUG
                        Msg?.AddMessage(Prefix + "Closing the response for \"" + url + "\" failed!", ex, MessageLevels.Debug);
#endif//DEBUG
                    }
                    if (Interlocked.Decrement(ref ReqCounter) == 0)
                    {
                        if (IsPaused)
                        {
                            lock (Listener)
                            {
                                if (IsPaused)
                                {
                                    if (IsListening != null)
                                        Stop();
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void OnDispose()
        {
            Stop();
            var msg = Msg;
            try
            {
                Listener.Close();
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to close listener", ex, MessageLevels.Warning);
            }
        }






    }



}
