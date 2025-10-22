using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Security;


using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Primitives;
using SysWeaver.Translation;

namespace SysWeaver.Net
{


    //https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserver?view=aspnetcore-8.0

    public class AspHttpServer : HttpServerBase, IDisposable
    {
        sealed class Log : ILoggerProvider, ILogger
        {
            public Log(IMessageHost msg)
            {
                Msg = msg;
            }

            readonly IMessageHost Msg;

            public ILogger CreateLogger(string categoryName) => this;

            public void Dispose()
            {
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            static readonly MessageLevels[] Levels =
            [
                MessageLevels.Debug, // LogLevel.Trace
                MessageLevels.Debug, // LogLevel.Debug
                MessageLevels.Info, // LogLevel.Information
                MessageLevels.Warning, // LogLevel.Warning
                MessageLevels.Error, // LogLevel.Error
                MessageLevels.Error, // LogLevel.Critical
                MessageLevels.All, // LogLevel.None
            ];

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var msg = Msg;
                if (msg == null)
                    return;
                var t = "[Kestrel] " + formatter(state, exception);
                var l = Levels[(int)logLevel];
                if (exception != null)
                    msg.AddMessage(t, exception, l);
                else
                    msg.AddMessage(t, l);
            }
        }


        static Exception ValidatePrefix(out int port, out string scheme, out bool isLocalHost, out string ip, String p)
        {
            var th = "sys_weaver_temp_hostname";
            var t = p.Replace("*", th);
            var uri = new Uri(t);
            scheme = uri.Scheme;
            port = uri.Port;
            isLocalHost = false;
            ip = null;
            if (scheme != "http")
                if (scheme != "https")
                    return new Exception("Invalid uri scheme \"" + scheme + "\"");
            var ap = uri.AbsolutePath;
            if (ap != "/")
                return new Exception("Kestrel doesn't support a path in a prefix! Found \"" + ap + "\"");
            var h = uri.Host;
            if (h == th)
                return null;
            isLocalHost = uri.IsLoopback;
            if (isLocalHost)
                return null;
            IPAddress address;
            if (!IPAddress.TryParse(h, out address))
                return new Exception("Can't use a hostname! Found \"" + h + "\"");
            ip = h;
            switch (address.AddressFamily)
            {
                case System.Net.Sockets.AddressFamily.InterNetwork:
                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    return null;
            }
            return new Exception("Can't use a hostname! Found \"" + h + "\"");
        }


        public AspHttpServer(IMessageHost msg, ITranslator translator, IApiAuditService audit, IReadOnlyDictionary<String, ICertificateProvider> certProviders, AuthManager auth, IFirewallHandler firewallHandler, AspHttpServerParams p = null)
            : base(msg, translator, audit, auth, firewallHandler, p, null)
        {
            p = p ?? new AspHttpServerParams();
            PerfMon.Enabled = p.PerMon;
            var listenOn = p.ListenOn;
            if ((listenOn == null) || (listenOn.Length <= 0) || (listenOn[0] == null))
                listenOn = [HttpServerPrefix.DefaultExternalHttps];
            var cs = p.CaseSensitive;
            var services = new ServiceCollection();
            ServiceList = services;
            var duration = TimeSpan.FromMinutes(5);
            services.AddLogging(b =>
            {
                b.AddProvider(new Log(msg));
#if DEBUG
                b.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Information);
#else//DEBUG
                b.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Warning);
#endif//DEBUG
            });
            services.Configure<KestrelServerOptions>(options =>
            {
                options.ApplicationServices = Prov;
                options.AllowSynchronousIO = true;
                options.ConfigureEndpointDefaults(def =>
                {
                    def.KestrelServerOptions.Limits.KeepAliveTimeout = duration;
                    def.KestrelServerOptions.Limits.RequestHeadersTimeout = duration;
                });
                options.ConfigureHttpsDefaults(def =>
                {
                    //def.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;

                });

                var prefixes = new List<HttpServerPrefix>();
                bool haveFirewall = false;
                var seen = new HashSet<string>();
                var certs = new Dictionary<int, Tuple<ICertificateProvider, HttpServerPrefix>>();
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
                            cp = certProviders.FirstOrDefault().Value;
                            if (cp == null)
                                throw new Exception(Prefix + "No certificate provider found, disable certificates or add a certificate provider!");
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
                    if (!cs)
                        pre = pre.FastToLower();
                    if (!seen.Add(pre))
                        continue;
                    var ex = ValidatePrefix(out var port, out var scheme, out var isLocal, out var ip, pre);
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
                    X509Certificate2 cert = null;
                    if (scheme == "https")
                    {
                        if (cp == null)
                        {
                            if (p.IgnoreBadPrefixes)
                            {
                                msg?.AddMessage(Prefix + "Can't listen on prefix \"" + pre + "\" without a certificate provider!", MessageLevels.Warning);
                                continue;
                            }
                            throw new Exception(Prefix + "Can't listen on prefix \"" + pre + "\" without a certificate provider!");
                        }
                        try
                        {
                            cert = cp.GetCert().RunAsync();
                        }
                        catch (Exception ex2)
                        {
                            if (p.IgnoreBadPrefixes)
                            {
                                msg?.AddMessage(Prefix + "Can't listen on prefix \"" + pre + "\" failed to read certificate: " + ex2.Message, ex2, MessageLevels.Warning);
                                continue;
                            }
                            throw new Exception(Prefix + "Can't listen on prefix \"" + pre + "\" failed to read certificate: " + ex2.Message, ex2);
                        }
                    }
                    msg?.AddMessage(Prefix + "Listening on prefix \"" + pre + "\"", MessageLevels.Info);
                    Action<ListenOptions> onOptions = opt =>
                    {
                        opt.Protocols = HttpProtocols.Http1;
                        if (cert != null)
                        {
                            opt.Protocols = HttpProtocols.Http1AndHttp2;
                            if (p.EnableHttp3)
                            {
                                opt.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                                using (msg?.Tab())
                                    msg?.AddMessage(Prefix + "Enabled http3");
                            }
                            opt.UseHttps(cert);
                        }
                    };
                    if (isLocal)
                    {
                        localPrefix = localPrefix ?? String.Concat(scheme, "://localhost:", port, '/');
                        options.ListenLocalhost(port, onOptions);
                    }
                    else
                    {
                        if (ip != null)
                            options.Listen(IPAddress.Parse(ip), port, onOptions);
                        else
                        {
                            localPrefix = localPrefix ?? String.Concat(scheme, "://localhost:", port, '/');
                            options.ListenAnyIP(port, onOptions);
                        }
                    }
                    if (ExternalRootUri == null)
                        ExternalRootUri = localPrefix;
                    var pc = prefix.Clone();
                    pc.Prefix = pre;
                    prefixes.Add(pc);
                    haveFirewall |= prefix.AddToFirewall;
                    if (cert != null)
                    {
                        void onChange()
                        {
                            cp.OnChanged -= onChange;
                            OnCertChanged(cp, pre);
                        };
                        BeforeCertChanged.Push(() => cp.OnChanged -= onChange);
                        cp.OnChanged += onChange;
                    }
                }
                LocalUri = localPrefix;
                options.AddServerHeader = false;
                SetPrefixes(prefixes, haveFirewall);
            });
            services.AddSingleton<IHostEnvironment> (new MyEnv());
            var asm = typeof(Http3Limits).Assembly;
            var mt = Type.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.KestrelMetrics, " + asm.FullName);
            services.AddSingleton(mt);
            var it = Type.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.IHttpsConfigurationService, " + asm.FullName);
            var ot = Type.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.HttpsConfigurationService, " + asm.FullName);
            services.AddSingleton(it, ot);
            services.AddSingleton<IMeterFactory, MyMeter>();
            services.AddSingleton<IServer, KestrelServer>();
            services.AddSingleton<IConnectionListenerFactory, SocketTransportFactory>();
            services.AddTransient<IHttpContextFactory, DefaultHttpContextFactory>();
            services.AddSingleton(this);
            services.AddTransient<MyHostingApplication>();



            SecondsToWait = Math.Max(1, p.SecondsToWait);
            FirewallHandler = firewallHandler;
        }

        protected override HttpServerRequest ReplaceUrl(HttpServerRequest s, string newUrl, String newMethod = null)
        {
            var o = s as AspHttpServerRequest;
            var uri = new Uri(newUrl);
            var host = GetHost(out var prefix, out var url, uri);
            if (prefix == null)
                return null;
            var h = new AspHttpServerRequest(o.Context, url, prefix, this, uri, host, newMethod);
            h.Init(s.Session);
            return h;
        }

        sealed class MyMeter : IMeterFactory
        {
            public Meter Create(MeterOptions options)
            {
                return new Meter(options);
            }

            public void Dispose()
            {
            }
        }

        sealed class MyEnv : IHostEnvironment
        {
            public string ApplicationName
            {
                get => EnvInfo.AppName;
                set => throw new NotImplementedException();
            }

            public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string ContentRootPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string EnvironmentName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

        readonly Stack<Action> BeforeCertChanged = new Stack<Action>();



        sealed class MyHostingApplication : IHttpApplication<HttpContext>
        {
            readonly IHttpContextFactory Factory;
            readonly AspHttpServer Server;

            public MyHostingApplication(AspHttpServer server, IHttpContextFactory httpContextFactory)
            {
                Server = server;
                Factory = httpContextFactory;
            }

            
            public HttpContext CreateContext(IFeatureCollection contextFeatures) => Factory.Create(contextFeatures);

            public void DisposeContext(HttpContext context, Exception exception) => Factory.Dispose(context);

            public Task ProcessRequestAsync(HttpContext context) => Server.HandleRequest(context);
        }

        readonly IFirewallHandler FirewallHandler;
        public readonly int SecondsToWait;


        volatile bool IsRunning;

        readonly Object Lock = new object();

        public static readonly Task<String> NullStringTask = Task.FromResult<String>(null);

        public bool Start()
        {
            lock (Lock)
            {
                if (IsRunning)
                    return false;
                Msg?.AddMessage(Prefix + "Starting", MessageLevels.Debug);
                IsRunning = true;
                TryStart().RunAsync();
                IsPaused = false;
            }
            return true;
        }

        IServer Server;
        MyHostingApplication App;
        readonly ServiceCollection ServiceList;
        IServiceProvider Prov;

        protected override Task<bool> OnNewCert(ICertificateProvider cert, String pre)
        {
            lock (Lock)
            {
                var c = BeforeCertChanged;
                while (c.TryPop(out var cc))
                    cc();
                bool isPaused = IsPaused;
                Stop();
                if (!isPaused)
                    Start();
                return Task.FromResult(true);
            }
        }

        async Task TryStart()
        {
            try
            {
                var services = ServiceList;
                var serviceProvider = services.BuildServiceProvider();
                Prov = serviceProvider;
                App = serviceProvider.GetRequiredService<MyHostingApplication>();
                var server = Prov.GetRequiredService<IServer>() as KestrelServer;
                Server = server;
                await server.StartAsync(App, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(Prefix + "Failed to start Kestrel server", ex, MessageLevels.Warning);
            }
        }

        async Task TryStop()
        {
            try
            {
                var server = Interlocked.Exchange(ref Server, null) as KestrelServer;
                await server.StopAsync(default).ConfigureAwait(false);
                App = null;
                (Interlocked.Exchange(ref Prov, null) as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(Prefix + "Failed to stop Kestrel server", ex, MessageLevels.Warning);
            }
        }


        public bool Stop()
        {
            lock (Lock)
            {
                if (!IsRunning)
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
                TryStop().RunAsync();
                IsRunning = false;
            }
            Msg?.AddMessage(Prefix + "Stopped", MessageLevels.Debug);
            return true;
        }


        public override void Pause()
        {
            lock (Lock)
            {
                if (IsPaused)
                    return;
                if (!IsRunning)
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
            lock (Lock)
            {
                if (!IsPaused)
                    return;
                Start();
                Msg?.AddMessage(Prefix + "Resuming", MessageLevels.Debug);
                IsPaused = false;
                RunAfterContinue();
            }
        }

        static readonly Uri DummyUri = new Uri("http://localhost");


        public static readonly IReadOnlyList<IHttpServerEndPoint> NoEndPoints = new List<IHttpServerEndPoint>();

        Task WriteResponseString(HttpResponse res, String text, String mime = HttpServerTools.TextMime)
        {
            res.Headers["Content-Type"] = mime;
            return res.WriteAsync(text);
        }

        long ReqCounter;

        /// <summary>
        /// Handles a single incoming request
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        async Task HandleRequest(HttpContext c)
        {
            using (PerfMon.Track(nameof(HandleRequest)))
            {
                var res = c.Response;
                String url = "";
                Interlocked.Increment(ref ReqCounter);
                try
                {
                    if (IsPaused)
                    {
                        res.StatusCode = 503;
                        await WriteResponseString(res, "The service is temporarily paused").ConfigureAwait(false);
                        return;
                    }
                    var uri = new Uri(c.Request.GetDisplayUrl());
                    var host = GetHost(out var prefix, out url, uri);
                    if (prefix == null)
                    {
                        res.StatusCode = 404;
                        await WriteResponseString(res, "It's a 404!").ConfigureAwait(false);
                        return;
                    }
                    using var data = new AspHttpServerRequest(c, url, prefix, this, uri, host);
                    data.SetResHeader("Server", "");
                    await Handle(data, url).ConfigureAwait(false);
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
                        res.Body.Close();
                        //res.Headers["Server"] = StringValues.Empty;
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
                            lock (Lock)
                            {
                                if (IsPaused)
                                {
                                    if (IsRunning)
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
        }


    }

}


