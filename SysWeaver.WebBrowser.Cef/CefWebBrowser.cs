
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using CefSharp;
using CefSharp.OffScreen;

namespace SysWeaver.WebBrowser
{




    /// <summary>
    /// Adds a headless web browser, that can be used for various automations etc.
    /// </summary>
    public sealed class CefWebBrowser : IBrowserService, IDisposable, IHaveStats
    {
        static CefWebBrowser()
        {
            CefSharp.OnTypeInit();
        }


        public CefWebBrowser(IMessageHost messageHost, CefWebBrowserParams p = null)
        {
            p = p ?? new CefWebBrowserParams();
            M = messageHost;
            MaxFreeWindowCount = Math.Max(0, p.MaxFreeWindows);

            CefSettings settings = new CefSettings
            {
                LogFile = EnvInfo.ExecutableBase + "_cef.log",
                LogSeverity = p.SmallLog ? LogSeverity.Fatal : LogSeverity.Verbose,
                WindowlessRenderingEnabled = true,
            };
            Settings = settings;
            var cp = p.CachePath?.Trim();
            if (cp != null)
            {
                if (cp.Length <= 0)
                    cp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysWaver.CefSharpCache");
                settings.CachePath = cp;
            };

            if (p.Init)
                TaskExt.RunAsync(Init());
        }

        readonly IMessageHost M;

        public void Dispose()
        {
            IsDisposing = true;
            if (Interlocked.Read(ref AllocatedWindowCount) > 0)
            {
                var f = FreeWindows;
                while (f.TryPop(out var w))
                    w.Dispose();
            }else
            {
                CefSharp.OnDispose(M);
            }
        }

        readonly CefSettings Settings;

        bool DidInit;

        public async Task Init()
        {
            if (DidInit)
                return;
            await CefSharp.Init(M, Settings).ConfigureAwait(false);
            DidInit = true;
        }

        readonly ConcurrentStack<Win> FreeWindows = new ConcurrentStack<Win>();
        readonly long MaxFreeWindowCount;
        long AllocatedWindowCount;
        long FreeWindowCount;
        bool IsDisposing;

        void DoDispose(Win win)
        {
            if (IsDisposing)
            {
                win.Browser.Dispose();
                if (Interlocked.Decrement(ref AllocatedWindowCount) == 0)
                    CefSharp.OnDispose(M);
                return;
            }
            if (Interlocked.Read(ref FreeWindowCount) >= MaxFreeWindowCount)
            {
                win.Browser.Dispose();
                Interlocked.Decrement(ref AllocatedWindowCount);
                return;
            }
/*            if (Interlocked.Read(ref win.ReUseCount) > 16)
            {
                win.Browser.Dispose();
                Interlocked.Decrement(ref AllocatedWindowCount);
                return;
            }
*/            Interlocked.Increment(ref FreeWindowCount);
            FreeWindows.Push(win);
        }

        public async Task<IBrowserWindow> OpenWindow()
        {
            if (IsDisposing)
                return null;
            await Init().ConfigureAwait(false);
            var f = FreeWindows;
            if (f.TryPop(out var b))
            {
                Interlocked.Increment(ref b.ReUseCount);
                Interlocked.Decrement(ref FreeWindowCount);
                return b;
            }


            b = await CefSharp.Run(() => new Win(M, DoDispose)).ConfigureAwait(false);
            Interlocked.Increment(ref AllocatedWindowCount);
            return b;
        }

        internal static String Prefix => CefSharp.Prefix;

        const String Name = "CefBrowserService";

        public IEnumerable<Stats> GetStats()
        {
            yield return new Stats(Name, "Allocated", Interlocked.Read(ref AllocatedWindowCount), "The maximum number of allocated browser windows");
            yield return new Stats(Name, "Idle", Interlocked.Read(ref FreeWindowCount), "The number of free to use browser windows");
        }


        sealed class Win : IBrowserWindow
        {
            public Win(IMessageHost m, Action<Win> onDispose)
            {
                M = m;
                var b = new ChromiumWebBrowser();
                Browser = b;
                OnDispose = onDispose;
                Browser.LoadingStateChanged += LoadingStateChanged;
            }
            readonly Action<Win> OnDispose;

            readonly ConcurrentDictionary<String, bool> JsObjects = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);


            public long ReUseCount;

            public Task<bool> AddJsObject(String name, Object obj)
            {
                if (!JsObjects.TryAdd(name, true))
                    return Task.FromResult(false);
                var rep = Browser.JavascriptObjectRepository;
                rep.Register(name, obj);
                return Task.FromResult(true);
            }

            public Task<bool> RemoveJsObject(String name)
            {
                if (!JsObjects.TryRemove(name, out var _))
                    return Task.FromResult(false);
                var rep = Browser.JavascriptObjectRepository;
                rep.UnRegister(name);
                return Task.FromResult(true);
            }

            readonly IMessageHost M;


            readonly ManualResetEventSlim HaveLoaded = new ManualResetEventSlim(false);

            async Task<LoadUrlAsyncResponse> InternalLoadUrl(String url)
            {
                HaveLoaded.Reset();
                return await Browser.LoadUrlAsync(url);
            }

            void LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
            {
                if (e.IsLoading)
                {
                    if (HaveLoaded.IsSet)
                        HaveLoaded.Reset();
                }
                else
                {
                    if (!HaveLoaded.IsSet)
                        HaveLoaded.Set();
                }
            }

            public async Task LoadUrl(String url, bool throwOnError = true)
                => Err(
                    await CefSharp.RunTask(() => InternalLoadUrl(url)).ConfigureAwait(false),
                    throwOnError);

            public override string ToString() => String.Concat(Url.ToQuoted(), " @ ", Width, 'x', Height);

            public readonly ChromiumWebBrowser Browser;

            public void Dispose()
            {
                CefSharp.Run(async () =>
                {
                    var b = Browser;
                    b.JavascriptObjectRepository.UnRegisterAll();
                    JsObjects.Clear();
                    await b.LoadUrlAsync("about:blank");
                    await Task.Delay(10);
                    HaveLoaded.Reset();
                    OnDispose(this);
                }).RunAsync();
            }

            public String Url { get; private set; }
            public int Width => Browser.Size.Width;
            public int Height => Browser.Size.Height;


            public bool Err(LoadUrlAsyncResponse e, bool throwOnError = true)
            {
                if (e.Success)
                    return true;
                var err = "Page load failed with error code: " + e.ErrorCode + ", http status code: " + e.HttpStatusCode;
                M?.AddMessage(Prefix + err, throwOnError ? MessageLevels.Error : MessageLevels.Warning);
                if (throwOnError)
                    throw new Exception(err);
                return false;
            }


            public async Task<bool> WaitLoaded(bool throwOnError = true)
            {
                try
                {
                    await HaveLoaded.WaitHandle.WaitOneAsync().ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    if (throwOnError)
                        throw;
                }
                return false;
            }

            public async Task<bool> Resize(int width, int height)
            {
                await CefSharp.RunTask(() => Browser.ResizeAsync(width, height)).ConfigureAwait(false);
                return true;
            }


            public async Task<BrowserJsResponse> RunJs(String js)
            {
                var res = await CefSharp.RunTask(() => Browser.EvaluateScriptAsync(js)).ConfigureAwait(false); 
                return new BrowserJsResponse(res.Success, res.Result, res.Message);
            }


            public async Task<Byte[]> CapturePng()
            {
                return await CefSharp.RunTask(() => Browser.CaptureScreenshotAsync()).ConfigureAwait(false);
            }
            

        }




    }

}
