using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{

    public static class WebViewProxy
    {

        static long Count;
        static readonly AsyncLock Lock = new AsyncLock();

        static volatile Proxy P;


        static long Index;

        internal sealed class Proxy : DedicatedThreadTaskProxy
        {

            readonly WebViewBrowserParams Params;

            public Proxy(WebViewBrowserParams p) : base("WebView2 #" + Interlocked.Increment(ref Index))
            {
                Params = p;
            }

            public async Task Init()
            {
                var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2");
                CoreWebView2EnvironmentOptions env = new CoreWebView2EnvironmentOptions(null, "en");
                var p = Params;
                if (p.DisableGPU)
                    env.AdditionalBrowserArguments = "--disable-gpu";
                var e = await CoreWebView2Environment.CreateAsync(runtimePath, null, env).ConfigureAwait(false);
                E = e;
            }


            internal sealed class Win : IWebViewBrowserWindow
            {
                public Win(WebViewBrowserParams wp, CoreWebView2Controller c, Proxy p, Action<Win> onDispose)
                {
                    c.SetBoundsAndZoomFactor(new System.Drawing.Rectangle(0, 0, Width, Height), 1);
                    C = c;
                    P = p;
                    var core = c.CoreWebView2;
                    core.NavigationCompleted += Core_NavigationCompleted;
                    core.WebResourceRequested += Core_WebResourceRequested;
                    core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                    Core = core;
                    OnDispose = onDispose;
                }

                public CoreWebView2Controller Controller => C;
                public CoreWebView2 Core { get; init; }

                public async Task RunOnUiThread(Func<Task> task)
                {
                    if (P.NotUiThread)
                    {
                        await P.Run(task).ConfigureAwait(false);
                        return;
                    }
                    await task().ConfigureAwait(false);
                }

                public async Task<R> RunOnUiThread<R>(Func<Task<R>> task)
                {
                    if (P.NotUiThread)
                        return await P.Run(task).ConfigureAwait(false);
                    return await task().ConfigureAwait(false);
                }

                readonly Action<Win> OnDispose;

                public String Url { get; set;  }
                public int Width { get; set; } = 1920;
                public int Height { get; set; } = 1080;

                readonly ConcurrentDictionary<String, bool> JsObjects = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

                public long ReUseCount;

                public Task<bool> AddJsObject(String name, Object obj)
                {
                    if (P.NotUiThread)
                        return P.Run(() => AddJsObject(name, obj));
                    if (!JsObjects.TryAdd(name, true))
                        return Task.FromResult(false);
                    var core = C.CoreWebView2;
                    core.AddHostObjectToScript(name, obj);
                    return Task.FromResult(true);
                }

                public Task<bool> RemoveJsObject(String name)
                {
                    if (P.NotUiThread)
                        return P.Run(() => RemoveJsObject(name));
                    if (!JsObjects.TryRemove(name, out var _))
                        return Task.FromResult(false);
                    var core = C.CoreWebView2;
                    core.RemoveHostObjectFromScript(name);
                    return Task.FromResult(true);
                }

                public Task<bool> Resize(int width, int height)
                {
                    if (P.NotUiThread)
                        return P.Run(() => Resize(width, height));
                    C.SetBoundsAndZoomFactor(new System.Drawing.Rectangle(0, 0, width, height), 1);
                    Width = width;
                    Height = height;
                    return Task.FromResult(true);
                }


                readonly ManualResetEventSlim Completed = new ManualResetEventSlim(false);

                public Task LoadUrl(String url, bool throwOnError = true)
                {
                    if (P.NotUiThread)
                        return P.Run(() => LoadUrl(url, throwOnError));
                    var core = C.CoreWebView2;
                    Completed.Reset();
                    Interlocked.Exchange(ref LastLoadTime, DateTime.UtcNow.Ticks);
                    core.Navigate(url);
                    return Task.CompletedTask;
                }

                long LastLoadTime;
                void Core_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
                {
                    Interlocked.Exchange(ref LastLoadTime, DateTime.UtcNow.Ticks);
                }

                void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    Completed.Set();
                }


                const long MaxDelay = TimeSpan.TicksPerSecond * 5;
                const long MinSinceLastResource = TimeSpan.TicksPerMillisecond * 500;

                public async Task<bool> WaitLoaded(bool throwOnError = true)
                {
                    try
                    {
                        await Completed.WaitHandle.WaitOneAsync(15000).ConfigureAwait(false);
                        var exp = DateTime.UtcNow.Ticks + MaxDelay;
                        for (; ;)
                        {
                            var now = DateTime.UtcNow.Ticks;
                            if (now > exp)
                                break;
                            if ((now - Interlocked.Read(ref LastLoadTime)) > MinSinceLastResource)
                                break;
                            await Task.Delay(1).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                    }
                    return true;
                }

                public async Task<BrowserJsResponse> RunJs(String js)
                {
                    if (P.NotUiThread)
                        return await P.Run(() => RunJs(js)).ConfigureAwait(false);
                    var core = C.CoreWebView2;
                    var res = await core.ExecuteScriptAsync(js);
                    return new BrowserJsResponse(true, res, res);
                }

                public async Task<Byte[]> CapturePng()
                {
                    if (P.NotUiThread)
                        return await P.Run(CapturePng).ConfigureAwait(false);
                    var core = C.CoreWebView2;
                    var opt = @"{""format"":""png"",""optimizeForSpeed"":false,""quality"":100}";
                    var data = await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", opt);
                    var l = data.Length;
                    return Convert.FromBase64String(data.Substring(9, l - 11));
                }

                readonly Proxy P;
                readonly CoreWebView2Controller C;

                public void Dispose()
                {
                    if (P.NotUiThread)
                    {
                        P.Run(Dispose);
                        return;
                    }
                    var core = C.CoreWebView2;
                    var o = JsObjects;
                    foreach (var name in o.Keys)
                        core.RemoveHostObjectFromScript(name);
                    o.Clear();
                    core.Navigate("about:blank");
                    OnDispose(this);
                }

                internal void Kill()
                {
                    C.Close();
                }

            }


            Thread UiThread;

            public bool NotUiThread => Thread.CurrentThread != UiThread;

            public async Task<Win> InternalCreate(WebViewBrowserParams p, Action<Win> onDispose)
            {
                UiThread = Thread.CurrentThread;
                var e = E;
                var browserController = await e.CreateCoreWebView2ControllerAsync(-3);
                Debug.Assert(Thread.CurrentThread == UiThread);
                if (p.IgnoreCertErrors)
                    browserController.CoreWebView2.ServerCertificateErrorDetected += AcceptAllCerts;
                return new Win(p, browserController, this, onDispose);
            }

            static void AcceptAllCerts(object sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e)
            {
                var certificate = e.ServerCertificate;
                e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
            }

            public Task<Win> CreateWindow(WebViewBrowserParams p, Action<Win> onDispose)
                => Run(() => InternalCreate(p, onDispose));


            CoreWebView2Environment E;

            public override void Dispose()
            {
                if (DoExit())
                {
                    Task.Factory.StartNew(base.Dispose);
                    //  TODO: Wait? How?
                }
            }
        }

        static bool DoExit()
        {
            using var _ = Lock.LockSync();
            if (Interlocked.Decrement(ref Count) != 0)
                return false;
            P = null;
            return true;
        }

        internal static async Task<Proxy> Create(WebViewBrowserParams wp)
        {
            using var _ = await Lock.Lock().ConfigureAwait(false);
            //  Increment refcounter and start a new
            if (Interlocked.Increment(ref Count) == 1)
            {
                var p = new Proxy(wp);
                P = p;
                await p.Run(p.Init).ConfigureAwait(false);
            }
            return P;
        }






    }





}
