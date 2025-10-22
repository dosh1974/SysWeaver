using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{


    /// <summary>
    /// Adds a headless web browser, that can be used for various automations etc.
    /// </summary>
    public class WebViewBrowser : IBrowserService, IDisposable, IHaveStats
    {

        public WebViewBrowser(IMessageHost messageHost = null, WebViewBrowserParams p = null)
        {
            p = p ?? new WebViewBrowserParams();
            M = messageHost;
            MaxFreeWindowCount = Math.Max(0, p.MaxFreeWindows);
            Params = p;

        }
        readonly IMessageHost M;
        readonly WebViewBrowserParams Params;

        public void Dispose()
        {
            IsDisposing = true;
            if (Interlocked.Read(ref AllocatedWindowCount) > 0)
            {
                var f = FreeWindows;
                while (f.TryPop(out var w))
                    w.Dispose();
            }
            else
            {
                Interlocked.Exchange(ref Env, null)?.Dispose();
            }
        }

        readonly AsyncLock CreateLock = new AsyncLock();

        WebViewProxy.Proxy Env;


        readonly ConcurrentStack<WebViewProxy.Proxy.Win> FreeWindows = new ConcurrentStack<WebViewProxy.Proxy.Win>();
        readonly long MaxFreeWindowCount;
        long AllocatedWindowCount;
        long FreeWindowCount;
        bool IsDisposing;

        void DoDispose(WebViewProxy.Proxy.Win win)
        {
            if (IsDisposing)
            {
                win.Kill();
                if (Interlocked.Decrement(ref AllocatedWindowCount) == 0)
                    Interlocked.Exchange(ref Env, null)?.Dispose();
                return;
            }
            if (Interlocked.Read(ref FreeWindowCount) >= MaxFreeWindowCount)
            {
                win.Kill();
                Interlocked.Decrement(ref AllocatedWindowCount);
                return;
            }
            /*            if (Interlocked.Read(ref win.ReUseCount) > 16)
                        {
                            win.Browser.Dispose();
                            Interlocked.Decrement(ref AllocatedWindowCount);
                            return;
                        }
            */
            Interlocked.Increment(ref FreeWindowCount);
            FreeWindows.Push(win);
        }


        public async Task<IBrowserWindow> OpenWindow()
        {
            var e = Env;
            if (e == null)
            {
                using var _ = await CreateLock.Lock().ConfigureAwait(false);
                e = Env;
                if (e == null)
                {
                    e = await WebViewProxy.Create(Params).ConfigureAwait(false);
                    Env = e;
                }
            }

            var f = FreeWindows;
            if (f.TryPop(out var b))
            {
                Interlocked.Increment(ref b.ReUseCount);
                Interlocked.Decrement(ref FreeWindowCount);
                return b;
            }
            b = await e.CreateWindow(Params, DoDispose).ConfigureAwait(false);
            Interlocked.Increment(ref AllocatedWindowCount);
            return b;
        }

        const String Name = "WebViewBrowserService";

        public IEnumerable<Stats> GetStats()
        {
            yield return new Stats(Name, "Allocated", Interlocked.Read(ref AllocatedWindowCount), "The maximum number of allocated browser windows");
            yield return new Stats(Name, "Idle", Interlocked.Read(ref FreeWindowCount), "The number of free to use browser windows");
        }
    }





}
