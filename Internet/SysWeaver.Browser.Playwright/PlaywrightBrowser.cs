using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.WebBrowser
{

    public sealed class PlaywrightBrowserParams
    {
        public const String BrowserChromium = "Chromium";
        public const String BrowserFirefox = "Firefox";
        public const String BrowserWebkit = "Webkit";

        /// <summary>
        /// The browser engine to use, one of "Chromium", "Firefox" or "Webkit".
        /// </summary>
        public String Browser = BrowserChromium;

        /// <summary>
        /// Run without any window.
        /// A process without an interactive desktop (ex: a service) always runs headless, since a browser window can't render there.
        /// </summary>
        public bool Headless = true;

        /// <summary>
        /// The browser executable, variables can be used (see PathTemplate).
        /// If null and no Channel is specified, an installed Chrome or Edge is used for Chromium (the browser downloaded by Playwright if none is found).
        /// Firefox and Webkit always use the browser downloaded by Playwright (those are per user, a service running as another user won't find them).
        /// </summary>
        public String ExecutablePath;

        /// <summary>
        /// Optional Playwright browser channel, ex: "chrome", "msedge" or "chromium", ignored if ExecutablePath is specified.
        /// </summary>
        public String Channel;

        /// <summary>
        /// If null, every window is isolated, nothing is stored and any number of processes can run at the same time.
        /// If not null, this folder (variables can be used, see PathTemplate) is the browser profile: cookies etc are kept and shared by all windows.
        /// A profile can only be used by one browser process, and the user running the process must have full access to it.
        /// </summary>
        public String UserDataFolder;

        /// <summary>
        /// Ignore any certificate errors, this is dangerous!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// If true, the gpu isn't used (Chromium only).
        /// </summary>
        public bool DisableGpu;

        /// <summary>
        /// Render WebGL in software (slower, but needs no gpu; Chromium only).
        /// If null, it's only used when there is no interactive desktop (ex: a service).
        /// </summary>
        public bool? SoftwareWebGl;

        /// <summary>
        /// Try to not appear as an automated browser, some sites refuse to show anything otherwise (Chromium only).
        /// </summary>
        public bool HideAutomation = true;

        /// <summary>
        /// The browser language (a service doesn't have the language of the user).
        /// </summary>
        public String Locale = "en-US";

        /// <summary>
        /// Additional command line arguments to the browser.
        /// </summary>
        public String[] ExtraArgs;

        /// <summary>
        /// Maximum number of milliseconds to wait for the browser to start.
        /// </summary>
        public int LaunchTimeoutMs = 60000;

        /// <summary>
        /// Maximum number of milliseconds to wait for a page to load, a page that isn't fully loaded by then can still be used.
        /// </summary>
        public int LoadTimeoutMs = 30000;

        /// <summary>
        /// Maximum number of milliseconds to wait for the network to become idle after a page has loaded.
        /// </summary>
        public int IdleTimeoutMs = 5000;

        /// <summary>
        /// Maximum number of milliseconds to wait for a screenshot.
        /// </summary>
        public int CaptureTimeoutMs = 30000;

        /// <summary>
        /// Start a new browser after this many windows have been opened in one browser, 0 = never.
        /// A long running browser (in particular one rendering WebGL in software) slowly builds up state until it can't open pages anymore.
        /// New windows use the new browser at once, the old one is closed when its open windows are closed (or after RecycleWaitMs).
        /// Not used with a UserDataFolder (a profile can only be used by one browser process at a time).
        /// </summary>
        public int RecycleAfterWindows = 500;

        /// <summary>
        /// Maximum number of milliseconds to wait for the windows that are open in a recycled browser to be closed, before closing it anyway.
        /// </summary>
        public int RecycleWaitMs = 120000;

        /// <summary>
        /// Closing a window that takes longer than this number of milliseconds is logged as a warning.
        /// </summary>
        public int SlowCloseMs = 5000;
    }

    /// <summary>
    /// A browser service using Playwright.
    /// The browser is started on first use, and started again if it has died.
    /// </summary>
    public sealed class PlaywrightBrowser : IBrowserService, IDisposable, IHaveStats
    {
        public PlaywrightBrowser(IMessageHost messageHost = null, PlaywrightBrowserParams p = null)
        {
            Params = p ?? new();
            M = messageHost;
        }

        const String Prefix = "[Playwright] ";

        readonly PlaywrightBrowserParams Params;
        readonly IMessageHost M;
        readonly AsyncLock CreateLock = new AsyncLock();

        Session Current;
        volatile bool Disposed;

        /// <summary>
        /// The last browser being closed, a new browser using a profile must wait for the old one to release it
        /// </summary>
        Task LastStop = Task.CompletedTask;

        /// <summary>
        /// The browsers that are running, including recycled ones that still have open windows
        /// </summary>
        readonly ConcurrentDictionary<int, Session> Sessions = new ConcurrentDictionary<int, Session>();

        static int SessionCounter;

        long StatStarts;
        long StatOpened;
        long StatOpen;
        long StatRecoveries;
        long StatRecycles;
        long StatCloseFails;
        long StatSlowCloses;


        internal sealed class Session
        {
            public readonly int Id = Interlocked.Increment(ref SessionCounter);

            public IPlaywright Driver;
            /// <summary>
            /// Null if using a persistent profile
            /// </summary>
            public IBrowser Browser;
            /// <summary>
            /// Not null if using a persistent profile
            /// </summary>
            public IBrowserContext Shared;

            public volatile bool Dead;

            /// <summary>
            /// The number of windows opened in this browser
            /// </summary>
            public int Opened;

            /// <summary>
            /// The number of windows that are open (or being opened) in this browser
            /// </summary>
            public int Open;

            /// <summary>
            /// 1 when no new windows should be opened in this browser, it's closed when the open windows are closed
            /// </summary>
            public int Retiring;

            /// <summary>
            /// 1 when the browser is closed (or being closed)
            /// </summary>
            public int Stopped;

            public bool Alive => (!Dead) && (Volatile.Read(ref Retiring) == 0) && ((Shared != null) || (Browser?.IsConnected ?? false));
        }

        /// <summary>
        /// Gets told when a window (or a context it replaced) is closed
        /// </summary>
        internal sealed class WindowOwner
        {
            public WindowOwner(PlaywrightBrowser browser, Session session)
            {
                Browser = browser;
                S = session;
            }
            readonly PlaywrightBrowser Browser;
            readonly Session S;

            /// <param name="ex">The exception if closing failed</param>
            /// <param name="time">The time it took to close</param>
            /// <param name="windowDone">True when the window itself is closed, false when it's a context that the window replaced</param>
            public void Closed(Exception ex, TimeSpan time, bool windowDone)
                => Browser.OnClosed(S, ex, time, windowDone);
        }

        void OnClosed(Session s, Exception ex, TimeSpan time, bool windowDone)
        {
            //  Failing to close a window of a browser that is gone is expected
            if ((ex != null) && (!s.Dead) && (Volatile.Read(ref s.Stopped) == 0))
            {
                Interlocked.Increment(ref StatCloseFails);
                M?.AddMessage(String.Concat(Prefix, "Failed to close a window in browser #", s.Id.ToString(CultureInfo.InvariantCulture)), ex, MessageLevels.Warning);
            }
            var ms = time.TotalMilliseconds;
            var slow = Params.SlowCloseMs;
            if ((slow > 0) && (ms > slow))
            {
                Interlocked.Increment(ref StatSlowCloses);
                M?.AddMessage(String.Concat(Prefix, "Closing a window in browser #", s.Id.ToString(CultureInfo.InvariantCulture), " took ", ((long)ms).ToString(CultureInfo.InvariantCulture), " ms"), MessageLevels.Warning);
            }
            if (windowDone)
            {
                Interlocked.Decrement(ref StatOpen);
                Release(s);
            }
        }

        /// <summary>
        /// A window of the browser is closed (or failed to open)
        /// </summary>
        void Release(Session s)
        {
            if ((Interlocked.Decrement(ref s.Open) == 0) && (Volatile.Read(ref s.Retiring) != 0))
                StopLater(s, "recycled");
        }

        /// <summary>
        /// Use a new browser for new windows, close this one when its windows are closed
        /// </summary>
        void Retire(Session s)
        {
            if (Interlocked.Exchange(ref s.Retiring, 1) != 0)
                return;
            Interlocked.CompareExchange(ref Current, null, s);
            Interlocked.Increment(ref StatRecycles);
            var open = Volatile.Read(ref s.Open);
            M?.AddMessage(String.Concat(Prefix, "Browser #", s.Id.ToString(CultureInfo.InvariantCulture), " has opened ", Volatile.Read(ref s.Opened).ToString(CultureInfo.InvariantCulture), " windows, new windows use a new browser (", open.ToString(CultureInfo.InvariantCulture), " windows still open)"), MessageLevels.Debug);
            if (open == 0)
            {
                StopLater(s, "recycled");
                return;
            }
            var wait = Math.Max(1000, Params.RecycleWaitMs);
            _ = Task.Delay(wait).ContinueWith(_ =>
            {
                if (Volatile.Read(ref s.Stopped) == 0)
                    StopLater(s, String.Concat("recycled, ", Volatile.Read(ref s.Open).ToString(CultureInfo.InvariantCulture), " windows were still open after ", wait.ToString(CultureInfo.InvariantCulture), " ms"));
            }, TaskScheduler.Default);
        }

        public IEnumerable<Stats> GetStats()
        {
            const String sys = "Playwright";
            yield return new Stats(sys, "Browser starts", Interlocked.Read(ref StatStarts), "The number of times a browser has been started");
            yield return new Stats(sys, "Windows opened", Interlocked.Read(ref StatOpened), "The number of windows that have been opened");
            yield return new Stats(sys, "Windows open", Interlocked.Read(ref StatOpen), "The number of windows that are open right now");
            yield return new Stats(sys, "Browser recoveries", Interlocked.Read(ref StatRecoveries), "The number of times the browser was restarted because it couldn't open a window (ex: \"Target crashed\")");
            yield return new Stats(sys, "Browser recycles", Interlocked.Read(ref StatRecycles), "The number of times a new browser was started after opening RecycleAfterWindows windows");
            yield return new Stats(sys, "Failed window closes", Interlocked.Read(ref StatCloseFails), "The number of times closing a window failed while the browser was running");
            yield return new Stats(sys, "Slow window closes", Interlocked.Read(ref StatSlowCloses), "The number of times closing a window took longer than SlowCloseMs");
        }


        /// <summary>
        /// Find an installed browser to use as Chromium
        /// </summary>
        /// <returns>The executable, null to use the one downloaded by Playwright</returns>
        static String FindChromium()
        {
            List<String> c = new List<string>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
                {
                    var r = Environment.GetFolderPath(root);
                    if (!String.IsNullOrEmpty(r))
                        c.Add(Path.Combine(r, "Google", "Chrome", "Application", "chrome.exe"));
                }
                foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
                {
                    var r = Environment.GetFolderPath(root);
                    if (!String.IsNullOrEmpty(r))
                        c.Add(Path.Combine(r, "Microsoft", "Edge", "Application", "msedge.exe"));
                }
            }
            else
            {
                c.Add("/usr/bin/google-chrome");
                c.Add("/usr/bin/chromium");
                c.Add("/usr/bin/chromium-browser");
                c.Add("/usr/bin/microsoft-edge");
            }
            return c.FirstOrDefault(File.Exists);
        }

        IEnumerable<String> GetChromiumArgs(bool interactive)
        {
            var p = Params;
            yield return "--autoplay-policy=no-user-gesture-required";
            if (p.HideAutomation)
                yield return "--disable-blink-features=AutomationControlled";
            if (p.DisableGpu)
            {
                yield return "--disable-gpu";
                yield break;
            }
            if (p.SoftwareWebGl ?? !interactive)
            {
                yield return "--use-gl=angle";
                yield return "--use-angle=swiftshader";
            }
            //  WebGL must work also without a usable gpu driver
            yield return "--enable-unsafe-swiftshader";
            yield return "--ignore-gpu-blocklist";
        }

        bool IsChromium => (Params.Browser ?? PlaywrightBrowserParams.BrowserChromium).FastEquals(PlaywrightBrowserParams.BrowserChromium);

        async Task<Session> Start()
        {
            var p = Params;
            var name = p.Browser ?? PlaywrightBrowserParams.BrowserChromium;
            bool isChromium = IsChromium;
            bool isFirefox = name.FastEquals(PlaywrightBrowserParams.BrowserFirefox);
            bool isWebkit = name.FastEquals(PlaywrightBrowserParams.BrowserWebkit);
            if (!(isChromium || isFirefox || isWebkit))
                throw new Exception("Invalid browser name \"" + name + "\", valid are: \"" + PlaywrightBrowserParams.BrowserChromium + "\", \"" + PlaywrightBrowserParams.BrowserFirefox + "\", \"" + PlaywrightBrowserParams.BrowserWebkit + "\"");

            //  False for a service, or anything else that isn't on a visible window station
            bool interactive = Environment.UserInteractive;
            bool headless = p.Headless;
            if (!(headless || interactive))
            {
                M?.AddMessage(Prefix + "There is no interactive desktop, a browser window can't render there so running headless", MessageLevels.Warning);
                headless = true;
            }

            var exe = p.ExecutablePath;
            if (!String.IsNullOrEmpty(exe))
            {
                exe = PathTemplate.Resolve(exe);
                if (!File.Exists(exe))
                    throw new Exception("The browser executable \"" + exe + "\" doesn't exist!");
            }
            else
            {
                exe = (isChromium && String.IsNullOrEmpty(p.Channel)) ? FindChromium() : null;
            }

            var args = new List<String>();
            if (isChromium)
                args.AddRange(GetChromiumArgs(interactive));
            if (p.ExtraArgs != null)
                args.AddRange(p.ExtraArgs);
            var ignored = (isChromium && p.HideAutomation) ? new[] { "--enable-automation" } : null;
            var channel = (String.IsNullOrEmpty(exe) && !String.IsNullOrEmpty(p.Channel)) ? p.Channel : null;
            var from = exe ?? channel ?? "downloaded by Playwright";

            M?.AddMessage(String.Concat(Prefix, "Starting ", name, " (", from, "), headless: ", headless.ToString(), ", interactive desktop: ", interactive.ToString(), ", user: ", Environment.UserName), MessageLevels.Debug);

            var driver = await Playwright.CreateAsync().ConfigureAwait(false);
            try
            {
                var type = isFirefox ? driver.Firefox : (isWebkit ? driver.Webkit : driver.Chromium);
                var s = new Session
                {
                    Driver = driver,
                };
                var folder = p.UserDataFolder;
                if (String.IsNullOrEmpty(folder))
                {
                    var b = await type.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        ExecutablePath = exe,
                        Channel = channel,
                        Headless = headless,
                        Args = args,
                        IgnoreDefaultArgs = ignored,
                        Timeout = p.LaunchTimeoutMs,
                    }).ConfigureAwait(false);
                    b.Disconnected += (_, _) => OnDied(s);
                    s.Browser = b;
                }
                else
                {
                    folder = PathTemplate.Resolve(folder);
                    Directory.CreateDirectory(folder);
                    var c = await type.LaunchPersistentContextAsync(folder, new BrowserTypeLaunchPersistentContextOptions
                    {
                        ExecutablePath = exe,
                        Channel = channel,
                        Headless = headless,
                        Args = args,
                        IgnoreDefaultArgs = ignored,
                        Timeout = p.LaunchTimeoutMs,
                        IgnoreHTTPSErrors = p.IgnoreCertErrors,
                        Locale = p.Locale,
                        ViewportSize = DefaultSize,
                    }).ConfigureAwait(false);
                    c.Close += (_, _) => OnDied(s);
                    await InitContext(c).ConfigureAwait(false);
                    s.Shared = c;
                }
                Sessions[s.Id] = s;
                Interlocked.Increment(ref StatStarts);
                M?.AddMessage(String.Concat(Prefix, "Browser #", s.Id.ToString(CultureInfo.InvariantCulture), " started"), MessageLevels.Debug);
                return s;
            }
            catch (Exception ex)
            {
                driver.Dispose();
                M?.AddMessage(String.Concat(Prefix, "Failed to start ", name, " (", from, ")"), ex);
                throw;
            }
        }

        //  Not Playwright's own built-in default (1280x720): that exact size is a well known tell of an unconfigured automation framework,
        //  so a window that's actually shown (not just captured as a picture) uses a common real desktop size instead
        static ViewportSize DefaultSize => new ViewportSize
        {
            Width = 1920,
            Height = 1080,
        };

        const String HideAutomationScript =
            """
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            window.chrome = window.chrome || { runtime: {} };
            """;

        async Task InitContext(IBrowserContext c)
        {
            if (Params.HideAutomation && IsChromium)
                await c.AddInitScriptAsync(HideAutomationScript).ConfigureAwait(false);
        }

        void OnDied(Session s)
        {
            s.Dead = true;
            if (Disposed)
                return;
            if (Interlocked.CompareExchange(ref Current, null, s) == s)
            {
                M?.AddMessage(String.Concat(Prefix, "Browser #", s.Id.ToString(CultureInfo.InvariantCulture), " died, it will be started again when needed"), MessageLevels.Warning);
            }
            //  Nothing is done if we're the ones closing it
            StopLater(s, "died");
        }

        /// <summary>
        /// Close the browser and the driver in the background (once)
        /// </summary>
        Task StopLater(Session s, String reason)
        {
            if (Interlocked.Exchange(ref s.Stopped, 1) != 0)
                return Task.CompletedTask;
            var t = Stop(s, reason);
            Volatile.Write(ref LastStop, t);
            return t;
        }

        async Task Stop(Session s, String reason)
        {
            s.Dead = true;
            M?.AddMessage(String.Concat(Prefix, "Closing browser #", s.Id.ToString(CultureInfo.InvariantCulture), ": ", reason), MessageLevels.Debug);
            try
            {
                var t = s.Shared != null ? s.Shared.CloseAsync() : s.Browser?.CloseAsync();
                if (t != null)
                    await t.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch
            {
                //  Already gone, or hanging: disposing the driver below ends it
            }
            try
            {
                s.Driver?.Dispose();
            }
            catch
            {
            }
            Sessions.TryRemove(s.Id, out _);
        }

        async Task<Session> GetSession()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            var s = Volatile.Read(ref Current);
            if ((s != null) && s.Alive)
                return s;
            using var _ = await CreateLock.Lock().ConfigureAwait(false);
            ObjectDisposedException.ThrowIf(Disposed, this);
            s = Volatile.Read(ref Current);
            if ((s != null) && s.Alive)
                return s;
            if ((s != null) && (Interlocked.CompareExchange(ref Current, null, s) == s))
                StopLater(s, "not usable anymore");
            //  A profile can only be used by one browser process, wait for the previous browser to release it
            try
            {
                await Volatile.Read(ref LastStop).WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            }
            catch
            {
            }
            s = await Start().ConfigureAwait(false);
            Volatile.Write(ref Current, s);
            return s;
        }

        /// <summary>
        /// A context per window: isolated, and a page can't affect any other window (the scale can only be set when creating a context)
        /// </summary>
        async Task<(IBrowserContext, IPage)> CreateWindow(Session s, double scale)
        {
            var p = Params;
            var c = await s.Browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = p.IgnoreCertErrors,
                Locale = p.Locale,
                ViewportSize = DefaultSize,
                DeviceScaleFactor = (float)scale,
            }).ConfigureAwait(false);
            try
            {
                await InitContext(c).ConfigureAwait(false);
                return (c, await c.NewPageAsync().ConfigureAwait(false));
            }
            catch
            {
                await c.CloseAsync().ConfigureAwait(false);
                throw;
            }
        }

        async Task<IBrowserWindow> InternalOpen(Session s)
        {
            var p = Params;
            //  Counted as open while being opened, so that a recycled browser isn't closed under it
            Interlocked.Increment(ref s.Open);
            PlaywrightBrowserWindow w;
            try
            {
                var owner = new WindowOwner(this, s);
                var shared = s.Shared;
                if (shared != null)
                {
                    var sp = await shared.NewPageAsync().ConfigureAwait(false);
                    w = new PlaywrightBrowserWindow(sp, null, null, p, M, owner);
                }
                else
                {
                    var (c, page) = await CreateWindow(s, 1).ConfigureAwait(false);
                    w = new PlaywrightBrowserWindow(page, c, x => CreateWindow(s, x), p, M, owner);
                }
            }
            catch
            {
                Release(s);
                throw;
            }
            Interlocked.Increment(ref StatOpen);
            Interlocked.Increment(ref StatOpened);
            var max = p.RecycleAfterWindows;
            if ((max > 0) && (s.Shared == null) && (Interlocked.Increment(ref s.Opened) >= max))
                Retire(s);
            return w;
        }

        public async Task<IBrowserWindow> OpenWindow()
        {
            var s = await GetSession().ConfigureAwait(false);
            try
            {
                return await InternalOpen(s).ConfigureAwait(false);
            }
            catch (PlaywrightException ex) when (!Disposed)
            {
                //  Either the browser died just now, or it's still running but can't open a window (ex: "Target crashed"),
                //  that won't get better by itself so close it and try once more with a new browser
                if (Interlocked.CompareExchange(ref Current, null, s) == s)
                {
                    Interlocked.Increment(ref StatRecoveries);
                    M?.AddMessage(String.Concat(Prefix, "Browser #", s.Id.ToString(CultureInfo.InvariantCulture), " failed to open a window, starting a new browser"), ex, MessageLevels.Warning);
                }
                s.Dead = true;
                StopLater(s, "failed to open a window");
                s = await GetSession().ConfigureAwait(false);
                return await InternalOpen(s).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            Disposed = true;
            Interlocked.Exchange(ref Current, null);
            var stops = Sessions.Values.Select(x => StopLater(x, "disposed")).ToArray();
            try
            {
                Task.WhenAll(stops).Wait(5000);
            }
            catch
            {
            }
        }
    }

    sealed class PlaywrightBrowserWindow : IBrowserWindow, IBrowserWindowInput
    {
        public async Task<(int Width, int Height)> ViewportSize()
        {
            var v = Page.ViewportSize;
            if (v != null)
                return (v.Width, v.Height);
            var r = await Page.EvaluateAsync<int[]>("() => [window.innerWidth, window.innerHeight]").ConfigureAwait(false);
            return (r[0], r[1]);
        }

        /// <summary>The mouse moving to a point, no button pressed (the person's own cursor, relayed as they move it over the remote view)</summary>
        public Task Move(double x, double y) => Page.Mouse.MoveAsync((float)x, (float)y);

        /// <summary>A click at a point (the person's click from a remote view, relayed as is)</summary>
        public Task Click(double x, double y) => Page.Mouse.ClickAsync((float)x, (float)y);

        public Task Type(String text) => Page.Keyboard.TypeAsync(text ?? "", new KeyboardTypeOptions { Delay = 40 });

        public Task Press(String key) => Page.Keyboard.PressAsync(key);

        public PlaywrightBrowserWindow(IPage page, IBrowserContext owned, Func<double, Task<(IBrowserContext, IPage)>> recreate, PlaywrightBrowserParams p, IMessageHost m, PlaywrightBrowser.WindowOwner owner)
        {
            Page = page;
            Owned = owned;
            Recreate = recreate;
            Params = p;
            M = m;
            Owner = owner;
        }

        readonly PlaywrightBrowser.WindowOwner Owner;

        const String Prefix = "[Playwright] ";

        /// <summary>
        /// The name of the function that the js objects use to call the host
        /// </summary>
        const String BridgeFunction = "__swHostCall";

        IPage Page;
        IBrowserContext Owned;
        readonly Func<double, Task<(IBrowserContext, IPage)>> Recreate;
        readonly PlaywrightBrowserParams Params;
        readonly IMessageHost M;

        int WasDisposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref WasDisposed, 1) != 0)
                return;
            _ = Close(Owned, Page, true);
        }

        /// <summary>
        /// Close a context (or the page if using a shared context) and tell the owner how it went
        /// </summary>
        /// <param name="owned">The context to close, null if the page is in a shared context</param>
        /// <param name="page">The page to close if there is no owned context</param>
        /// <param name="windowDone">True when this window is done, false when closing a context that was replaced</param>
        async Task Close(IBrowserContext owned, IPage page, bool windowDone)
        {
            var start = Stopwatch.GetTimestamp();
            Exception error = null;
            try
            {
                if (owned != null)
                    await owned.CloseAsync().ConfigureAwait(false);
                else
                    await page.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            try
            {
                Owner?.Closed(error, Stopwatch.GetElapsedTime(start), windowDone);
            }
            catch
            {
            }
        }


        public string Url { get; private set; }

        public int Width { get; private set; } = 1280;

        public int Height { get; private set; } = 720;

        double Scale = 1.0;
        bool HaveLoaded;


        #region Js objects

        sealed class JsObject
        {
            public JsObject(Object obj)
            {
                Obj = obj;
                //  Only what the type itself declares, nothing from Object etc.
                Methods = obj.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(x => !(x.IsSpecialName || x.IsGenericMethod || (x.Name == nameof(IDisposable.Dispose))))
                    .GroupBy(x => x.Name, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            }
            public readonly Object Obj;
            public readonly Dictionary<String, MethodInfo> Methods;
        }

        readonly AsyncLock FnLock = new AsyncLock();

        readonly ConcurrentDictionary<String, JsObject> Objs = new ConcurrentDictionary<String, JsObject>(StringComparer.Ordinal);

        bool HaveBridge;

        /// <summary>
        /// The scripts defining the js objects
        /// </summary>
        readonly List<String> ObjectScripts = new List<string>();

        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        /// <summary>
        /// Convert a js argument to a parameter of a method
        /// </summary>
        static Object ConvertArg(JsonElement a, Type t)
        {
            var u = Nullable.GetUnderlyingType(t);
            if ((a.ValueKind == JsonValueKind.Null) || (a.ValueKind == JsonValueKind.Undefined))
                return (t.IsValueType && (u == null)) ? Activator.CreateInstance(t) : null;
            var n = u ?? t;
            //  All numbers are doubles in js, ex: a computed width with decimals
            if ((a.ValueKind == JsonValueKind.Number) && n.IsPrimitive && (n != typeof(double)) && (n != typeof(float)))
                return Convert.ChangeType(Math.Round(a.GetDouble()), n, CultureInfo.InvariantCulture);
            return a.Deserialize(t, JsonOptions);
        }

        /// <summary>
        /// A call from the page: ["objectName", "methodName", [args..]] => { "v": result } or { "e": "error" }.
        /// Must not block the Playwright message loop, so that a method can use this window (resize, capture etc).
        /// </summary>
        async Task<String> OnCall(BindingSource source, String call)
        {
            try
            {
                //  An embedded frame (ex: an ad) should not be able to control the host
                if ((source?.Frame != null) && (source.Frame != Page.MainFrame))
                    throw new Exception("Only allowed from the main frame");
                using var d = JsonDocument.Parse(call);
                var r = d.RootElement;
                var name = r[0].GetString();
                var method = r[1].GetString();
                if (!Objs.TryGetValue(name, out var o))
                    throw new Exception("No object named \"" + name + "\"");
                if (!o.Methods.TryGetValue(method, out var fn))
                    throw new Exception("No method named \"" + method + "\" in object \"" + name + "\"");
                var pars = fn.GetParameters();
                var jsArgs = r[2];
                var jl = jsArgs.GetArrayLength();
                var args = new Object[pars.Length];
                for (int i = 0; i < pars.Length; ++i)
                {
                    var p = pars[i];
                    if (i < jl)
                        args[i] = ConvertArg(jsArgs[i], p.ParameterType);
                    else
                        args[i] = p.HasDefaultValue ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null);
                }
                var res = await Task.Run(async () =>
                {
                    var v = fn.Invoke(o.Obj, args);
                    if (v is Task t)
                    {
                        await t.ConfigureAwait(false);
                        var rt = fn.ReturnType;
                        v = rt.IsGenericType ? rt.GetProperty(nameof(Task<int>.Result)).GetValue(t) : null;
                    }
                    return v;
                }).ConfigureAwait(false);
                return JsonSerializer.Serialize(new Dictionary<String, Object> { { "v", res } }, JsonOptions);
            }
            catch (Exception ex)
            {
                var e = (ex as TargetInvocationException)?.InnerException ?? ex;
                M?.AddMessage(String.Concat(Prefix, "A call from the page failed: ", call), e, MessageLevels.Debug);
                return JsonSerializer.Serialize(new Dictionary<String, Object> { { "e", e.Message } }, JsonOptions);
            }
        }

        public async Task<bool> AddJsObject(string name, object obj)
        {
            using var _ = await FnLock.Lock().ConfigureAwait(false);
            var o = new JsObject(obj);
            if (!Objs.TryAdd(name, o))
                return false;
            var page = Page;
            if (!HaveBridge)
            {
                await page.ExposeBindingAsync<String, Task<String>>(BridgeFunction, OnCall).ConfigureAwait(false);
                HaveBridge = true;
            }
            //  Same location as the host objects of WebView2 (window.chrome doesn't exist in other browsers)
            var jsName = JsonSerializer.Serialize(name);
            var b = new StringBuilder();
            b.AppendLine("(() => {");
            b.AppendLine("  if (window !== window.top) return;");
            b.AppendLine("  const c = window.chrome = window.chrome || {};");
            b.AppendLine("  const w = c.webview = c.webview || {};");
            b.AppendLine("  const h = w.hostObjects = w.hostObjects || {};");
            b.AppendLine("  const call = async (m, a) => {");
            b.Append("    const r = JSON.parse(await window.").Append(BridgeFunction).Append("(JSON.stringify([").Append(jsName).AppendLine(", m, a])));");
            b.AppendLine("    if ('e' in r) throw new Error(r.e);");
            b.AppendLine("    return r.v;");
            b.AppendLine("  };");
            b.Append("  h[").Append(jsName).AppendLine("] = {");
            foreach (var x in o.Methods.Keys)
            {
                var n = JsonSerializer.Serialize(x);
                b.Append("    ").Append(n).Append(": (...a) => call(").Append(n).AppendLine(", a),");
            }
            b.AppendLine("  };");
            b.AppendLine("})();");
            var cmd = b.ToString();
            ObjectScripts.Add(cmd);
            await page.AddInitScriptAsync(cmd).ConfigureAwait(false);
            await page.EvaluateAsync(cmd).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RemoveJsObject(string name)
        {
            using var _ = await FnLock.Lock().ConfigureAwait(false);
            if (!Objs.TryRemove(name, out var obj))
                return false;
            await Page.EvaluateAsync(String.Concat("delete window.chrome?.webview?.hostObjects?.[", JsonSerializer.Serialize(name), "];")).ConfigureAwait(false);
            return true;
        }

        #endregion//Js objects


        static Object FromJson(JsonElement? e)
        {
            if (e == null)
                return null;
            var v = e.Value;
            switch (v.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    return v.GetDouble();
                case JsonValueKind.String:
                    return v.GetString();
            }
            return v.GetRawText();
        }

        public async Task<BrowserJsResponse> RunJs(string js)
        {
            try
            {
                var res = await Page.EvaluateAsync(js).ConfigureAwait(false);
                return new BrowserJsResponse(true, FromJson(res), null);
            }
            catch (PlaywrightException ex)
            {
                return new BrowserJsResponse(false, null, ex.Message);
            }
        }


        public async Task<byte[]> CaptureJpeg(int quality = 70, bool optimizeForSpeed = false)
            => await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Jpeg,
                Quality = Math.Clamp(quality, 0, 100),
                Timeout = Params.CaptureTimeoutMs,
            }).ConfigureAwait(false);

        public async Task<byte[]> CapturePng(bool optimizeForSpeed = false)
            => await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Png,
                OmitBackground = true,
                Timeout = Params.CaptureTimeoutMs,
            }).ConfigureAwait(false);


        public async Task LoadUrl(string url, bool throwOnError = true)
        {
            Url = url;
            HaveLoaded = true;
            try
            {
                await Page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = Params.LoadTimeoutMs,
                }).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                //  Something is taking forever, what we got so far can still be used
                M?.AddMessage(String.Concat(Prefix, "Timed out loading ", url, ": ", ex.Message), MessageLevels.Warning);
            }
            catch (PlaywrightException) when (!throwOnError)
            {
            }
        }

        /// <summary>
        /// Change the scale by replacing the context and page, only possible before a page is loaded
        /// </summary>
        async Task SetScale(double scale)
        {
            if (scale == Scale)
                return;
            if (HaveLoaded || (Recreate == null))
            {
                M?.AddMessage(String.Concat(Prefix, "The scale can only be changed before loading a page, and not when using a UserDataFolder (scale stays at ", Scale.ToString(CultureInfo.InvariantCulture), ")"), MessageLevels.Debug);
                return;
            }
            using var scaleLock = await FnLock.Lock().ConfigureAwait(false);
            var (c, page) = await Recreate(scale).ConfigureAwait(false);
            try
            {
                if (HaveBridge)
                    await page.ExposeBindingAsync<String, Task<String>>(BridgeFunction, OnCall).ConfigureAwait(false);
                foreach (var x in ObjectScripts)
                    await page.AddInitScriptAsync(x).ConfigureAwait(false);
            }
            catch
            {
                _ = Close(c, page, false);
                throw;
            }
            var oldContext = Owned;
            var oldPage = Page;
            Owned = c;
            Page = page;
            Scale = scale;
            _ = Close(oldContext, oldPage, false);
        }

        public async Task<bool> Resize(int width, int height, double scale = 1)
        {
            await SetScale(scale <= 0 ? 1 : Math.Clamp(scale, 0.1, 10.0)).ConfigureAwait(false);
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            await Page.SetViewportSizeAsync(width, height).ConfigureAwait(false);
            Width = width;
            Height = height;
            return true;
        }


        public async Task<bool> WaitLoaded(bool throwOnError = true)
        {
            try
            {
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = Params.IdleTimeoutMs,
                }).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                //  Some pages are never idle (ex: streaming), not an error
            }
            catch (PlaywrightException)
            {
                if (throwOnError)
                    throw;
                return false;
            }
            return true;
        }
    }


}
