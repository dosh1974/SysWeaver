using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.IsoData;
using SysWeaver.MicroService;
using SysWeaver.Security;
using SysWeaver.Translation;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.Net
{

    [WebMenuEmbedded(null, "Welcome", "Welcome", "app/Welcome.html", "Show the welcome page", "IconHome", -100, null, true)]
    [WebMenuEmbedded(null, "Home", "Home", "app/Home.html", "Show the home page", "IconHome", -100, "")]
    [WebMenuPath("Theme", "Theme", "Theme", "Color themes", "IconTheme")]
    [WebMenuPath(null, "Debug/Http Server", "Http Server", "Debug data from the http server", "icons/server.svg")]
    [WebMenuPath(null, "Debug/Data", "Data", "Misc static data", "icons/book.svg", 100)]
    [WebMenuJs("Theme", "Theme/Auto", "(Automatic)", "if(resetTheme());false", "Automatically select (from browser / OS settings)", "IconThemeAuto", 0)]
    [WebMenuJs("Theme", "Theme/Light", "Light", "if(setTheme('light'));false", "Use the bright mode color scheme", "IconThemeLight", 1)]
    [WebMenuJs("Theme", "Theme/Dark", "Dark", "if(setTheme('dark'));false", "Use the dark mode color scheme", "IconThemeDark", 2)]
    [WebMenuJs("Theme", "Theme/Application", "Application", "if(setTheme('application'));false", "Use a generated theme, unique to this application", "IconLogo", 3)]
    [WebMenuJs("Theme", "Theme/Pro", "Pro", "if(setTheme('pro'));false", "Use a small font, dark color scheme suitable for desktop computers", "IconThemeDark", 4)]
    [WebMenuJs("Theme", "Theme/Hacker", "Hacker", "if(setTheme('hacker'));false", "Use the hacker color scheme", "IconThemeHacker", 10, "Debug")]
    public abstract partial class HttpServerBase : IPerfMonitored, IHaveStats, IServicePausable, IDisposable
    {

        protected const String Prefix = "[HttpServer] ";

        /// <summary>
        /// Local host prefix
        /// </summary>
        public String LocalUri { get; protected set; }


        /// <summary>
        /// External host prefix
        /// </summary>
        public String ExternalRootUri { get; protected set; }

        protected bool ExternalRootUriFromRequest;

        long CacheHit;
        long CacheTotal;

        public override string ToString() => BaseParams.ToString();

        public readonly ITranslator Translator;

        protected HttpServerBase(IMessageHost msg, ITranslator translator, IApiAuditService audit, AuthManager auth, IFirewallHandler firewallHandler, HttpServerBaseParams p, Type listenerExceptionType)
        {
            p = p ?? new HttpServerBaseParams();
            Msg = msg;
            Auth = auth;
            BaseParams = p;
            Translator = p.AutoTranslate ? translator : null;
            FirewallHandler = firewallHandler;
            ExternalRootUri = p.ExternalRootUri;
            var envVars = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in EnvInfo.TextVarsCaseInsensitive)
                envVars[String.Join(k.Key, '[', ']')] = k.Value;
            Audit = audit;
            var s = p.SessionCookieName;
            SessionCookieName = (s == null ? null : TextTemplate.SearchAndReplace(String.IsNullOrEmpty(s) ? "SysWeaver.Session.[AppName]" : s, envVars, true, true)).EncodeNonAsciiCharacters();
            SessionCookieNameEquals = SessionCookieName + "=";

            s = p.DeviceIdCookieName;
            DeviceIdCookieName = s == null ? null : TextTemplate.SearchAndReplace(String.IsNullOrEmpty(s) ? "SysWeaver.DeviceId" : s, envVars);
            DeviceIdCookieNameEquals = DeviceIdCookieName + "=";


            var mx = Math.Max(1, p.SessionExtendLifetime);
            SessionExtendLifetime = TimeSpan.TicksPerMinute * mx;
            SessionCookieLifetime = TimeSpan.TicksPerMinute * Math.Max(mx * 10 + 30, p.SessionCookieLifetime);



            AuthRedirect = p.AuthRedirect;
            AllowAuthorizationAuth = p.AllowAuthorizationAuth;
            LogoutRedirect = p.LogoutRedirect;
            ListenerExceptionType = listenerExceptionType;
            FirstCertRetryMinutes = Math.Max(1, p.FirstCertRetryMinutes);
            CertRetryMinutes = Math.Max(1, p.CertRetryMinutes);

            Stack<String> remove = p.RemoveFirewallOnExit ? new Stack<string>() : null;
            FirewallRules = remove;

            //  Variables

            Dictionary<String, String> vars = new Dictionary<string, string>(StringComparer.Ordinal);
            var v = p.Variables;
            if (v != null)
            {
                foreach (var x in v)
                {
                    if (x == null)
                        continue;
                    var t = x.Trim();
                    var i = t.IndexOf('=');
                    if (i < 0)
                    {
                        msg?.AddMessage(Prefix + "Expected a \"Key=Value\" string, found \"" + x + "\", ignoring!", MessageLevels.Warning);
                        continue;
                    }
                    var key = t.Substring(0, i).TrimEnd();
                    if (key.Length <= 0)
                    {
                        msg?.AddMessage(Prefix + "Expected a \"Key=Value\" string, found \"" + x + "\", ignoring!", MessageLevels.Warning);
                        continue;
                    }
                    var value = t.Substring(i + 1).TrimStart();
                    vars[key] = value;
                }
            }
            var cols = HashColors.AppColors;
            vars["Color.Background"] = cols.Background;
            vars["Color.Color"] = cols.Acc1;
            vars["Color.Acc1"] = cols.Acc3;
            vars["Color.Acc2"] = cols.Acc4;
            TempVars = vars.Freeze();

            var varGroups = TempVarGroups;
            foreach (var x in StaticVars.Inst.TemplateVariableGroups)
                varGroups[x.Key] = x.Value;
            //  Forced end-points
            var feps = new Dictionary<string, Func<HttpServerRequest, HttpSession, ValueTask>>(StringComparer.Ordinal);
            feps["logout"] = HandleLogout;
            feps["auth/redirect"] = HandleAuthRedirect;
            feps["auth/logout_user"] = HandleLogoutUser;
            feps["serverTime"] = HandleServerTime;



            var oeps = new Dictionary<string, Func<HttpServerRequest, HttpSession, ValueTask<IHttpRequestHandler>>>(StringComparer.Ordinal);

            oeps["basic_auth"] = HandleBasicAuth;
            oeps["login"] = HandleLogin;
            oeps["icon.svg"] = HandleFaviconSvg;

            oeps["icon_debug.svg"] = HandleFaviconDebugSvg;
            oeps["favicon.ico"] = HandleFaviconIco;
            oeps["apple-touch-icon.png"] = HandleFaviconPng180;
            oeps["icon-180.png"] = HandleFaviconPng180;
            oeps["icon-192.png"] = HandleFaviconPng192;
            oeps["icon-512.png"] = HandleFaviconPng512;

            oeps["logo.svg"] = HandleLogoSvg;
            oeps["logo_debug.svg"] = HandleLogoDebugSvg;
            oeps["logo.png"] = HandleLogoPng1024;

            oeps["app/app.css"] = HandleEmpty;
            oeps["app/app.js"] = HandleEmpty;


            ForcedEndPoints = feps.Freeze();
            OptionalEndPoints = oeps.Freeze();

            //  Templates


            AddTemplateMatch("index.html");
            AddTemplateMatch("debug.html");
            AddTemplateMatch("app/manifest.json");
            AddTemplateMatch("app/Home.html");
            AddTemplateMatch("common/theme.css");
            var temp = p.Templates;
            if (temp != null)
            {
                foreach (var t in temp)
                {
                    if (!String.IsNullOrEmpty(t))
                        AddTemplateMatch(t);
                }
            }
            var al = ValidateLanguageList(p.AllowedLanguages);
            if (al != null)
            {
                TargetLanguages = al;
                TargetLanguagesSet = ReadOnlyData.Set(al);
            }

            var l = p.ServerLimits;
            if (l != null)
            {
                l.Validate();
                ServerLimits = new HttpRateLimiter(l);
            }
            l = p.SessionLimits;
            if (l != null)
            {
                l.Validate();
                SessionLimits = l;
            }
            CookieOptions = p.CorsCookies ? "/;HttpOnly;SameSite=None;Secure" : "/;HttpOnly";
            PruneTask = new PeriodicTask(Prune, 3000);

            var ip = NetworkTools.GetAnyLanIP();
            if (ip == null)
            {
                const int timeOut = 60;
                msg?.AddMessage(Prefix + "Waiting up to " + timeOut + " seconds for a valid LAN ip");
                ip = NetworkTools.WaitForLanIp(timeOut);
                if (ip == null)
                    msg?.AddMessage(Prefix + "No LAN ip found!", MessageLevels.Warning);
            }
        }

        public static String[] ValidateLanguageList(String[] languages)
        {
            if (languages == null)
                return languages;
            var l = languages.Length;
            if (l <= 0)
                return null;
            var seen = new HashSet<String>(l, StringComparer.Ordinal);
            for (int i = 0; i < l; ++ i)
            {
                var lang = languages[i]?.Trim();
                lang = IsoLanguage.Validate(lang, true);
                if (!seen.Add(lang))
                    throw new Exception("Language \"" + lang + "\" @ " + i + " has already been added!");
                languages[i] = lang;
            }
            return languages;
        }

        readonly HttpRateLimiterParams SessionLimits;
        readonly HttpRateLimiter ServerLimits;

        readonly IApiAuditService Audit;

        readonly PeriodicTask PruneTask;

        /// <summary>
        /// Call this when Auth on a session have been set
        /// </summary>
        /// <param name="session"></param>
        public async Task RunOnLogin(HttpSession session)
        {
            session.PushMessage(MessageUserLogIn, true, false);
            session.DoNewLogin();
            var auth = session.Auth;
            var id = auth?.Guid;
            if (id == null)
                return;
            var us = UserSessions;
            if (!us.TryGetValue(id, out var u))
            {
                lock (us)
                {
                    if (!us.TryGetValue(id, out u))
                    {
                        u = new UserData(auth);
                        us.TryAdd(id, u);
                    }
                }
            }
            u.Sessions.TryAdd(session, true);
            try
            {
                OnLogin?.Invoke(session);
            }
            catch (Exception ex)
            {
                LoginErrors.OnException(ex);
            }
            try
            {
                await OnLoginAsync.RaiseEvents(session).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LoginErrors.OnException(ex);
            }
        }

        readonly ExceptionTracker SessionStartErrors = new ExceptionTracker();
        readonly ExceptionTracker LoginErrors = new ExceptionTracker();
        readonly ExceptionTracker LogoutErrors = new ExceptionTracker();

        internal bool RunOnSessionRemove(HttpSession session, String reason = null)
        {
            var sessions = Sessions;
            try
            {
                session.InvokeOnClose();
            }
            catch (Exception ex)
            {
                SessionCloseExceptions.OnException(ex);
            }
            try
            {
                var id = session.Auth?.Guid;
                if (id == null)
                    return false;
                var us = UserSessions;
                if (!us.TryGetValue(id, out var u))
                    return false;
                var s = u.Sessions;
                if (!s.TryRemove(session, out var _))
                    return false;
                session.PushMessage(new PushMessageStringValue("user.logout", reason ?? "Session expired"), true, false);
                if (s.Count > 0)
                    return true;
                lock (us)
                {
                    if (!us.TryGetValue(id, out u))
                        return true;
                    if (s.Count > 0)
                        return true;
                    us.TryRemove(id, out var _);
                }
            }
            finally
            {
                session.OnRemove();
            }
            return true;
        }

        internal void RunOnLogout(HttpSession session, String reason = null)
        {
            reason = reason ?? "User signed out";
            //if (CloseSession(session))
            if (!RunOnSessionRemove(session, reason))
                session.PushMessage(new PushMessageStringValue("user.logout", reason), true, false);
        }

        protected void RunBeforePause()
        {
            PushMessageAllSessions(MessageServerPause);
        }

        protected void RunAfterContinue()
        {
            PushMessageAllSessions(MessageServerContinue);
        }


        /// <summary>
        /// Execute some function on all active sessions for a specific user
        /// </summary>
        /// <param name="userGuid">The user guid</param>
        /// <param name="onSession">All sessions</param>
        /// <returns>True if the user is known and have some avtive or dead sessions, else False</returns>
        public bool EnumUserSessions(String userGuid, Action<HttpSession> onSession)
        {
            if (!UserSessions.TryGetValue(userGuid, out var ud))
                return false;
            foreach (var x in ud.Sessions)
                if (x.Value)
                    onSession(x.Key);
            return true;
        }


        readonly ConcurrentDictionary<String, UserData> UserSessions = new ConcurrentDictionary<string, UserData>();



        static void PrunceCache(ConcurrentDictionary<String, HttpCacheEntry> c)
        {
            const int maxWork = 100;
            List<String> remove = new List<string>(maxWork);
            var nowT = DateTime.UtcNow.Ticks;
            foreach (var x in c)
            {
                if (nowT > x.Value.Expires)
                {
                    remove.Add(x.Key);
                    if (remove.Count >= maxWork)
                        break;
                }
            }
            foreach (var x in remove)
            {
                if (!c.TryRemove(x, out var data))
                    continue;
                if (nowT > data.Expires)
                    continue;
                c.TryAdd(x, data);
            }

        }



        bool Prune()
        {
            {
                var now = DateTime.UtcNow;
                if ((now - LastFaviconCheck) > TimeSpan.FromSeconds(15))
                {
                    Interlocked.Exchange(ref FaviconCheck, 1);
                    LastFaviconCheck = now;
                }
                now = DateTime.UtcNow;
                if ((now - LastLogoCheck) > TimeSpan.FromSeconds(15))
                {
                    Interlocked.Exchange(ref LogoCheck, 1);
                    LastLogoCheck = now;
                }
            }
            using (PerfMon.Track(nameof(Prune) + ".Cache"))
                PrunceCache(Cache);
            using (PerfMon.Track(nameof(Prune) + ".Sessions"))
            {
                const int maxWork = 100000;
                var sessions = Sessions;
                List<String> remove = new List<string>(maxWork);
                var now = DateTime.UtcNow.Ticks;
                foreach (var x in sessions)
                {
                    if (x.Value.CanExpire(now))
                    {
                        remove.Add(x.Key.ToString());
                        if (remove.Count >= maxWork)
                            break;
                    }else
                    {
                        var cache = x.Value.Cache;
                        if (cache != null)
                            PrunceCache(cache);
                    }
                }
                var rs = ExpiredSessions;
                foreach (var x in remove)
                {
                    var xx = ReadOnlyMemoryKey.Create(x);
                    if (!sessions.TryRemove(xx, out var data))
                        continue;
                    if (data.CanExpire(now))
                    {
                        Interlocked.Decrement(ref CurrentSessionCount);
                        RunOnSessionRemove(data);
                        if (data.RequestCount > 3)
                        {
                            rs.Enqueue(data);
                            Interlocked.Increment(ref ExpiredCount);
                        }
                        continue;
                    }
                    sessions.TryAdd(xx, data);
                }
                now -= (TimeSpan.TicksPerMinute * 1);
                while (rs.TryPeek(out var data))
                {
                    if (!data.CanExpire(now))
                        break;
                    Interlocked.Decrement(ref ExpiredCount);
                    rs.TryDequeue(out data);
                }
            }
            return true;
        }

        public event Action<HttpSession> OnSessionStart;
        public event Action<HttpSession> OnLogin;
        public event Action<HttpServerRequest> OnLogout;

        public event Func<HttpSession, Task> OnLoginAsync;


        static void SetToRootFn(Span<Char> dest, int x)
        {
            var l = dest.Length;
            for (int i = 0; i < l; )
            {
                dest[i] = '.';
                ++i;
                dest[i] = '.';
                ++i;
                dest[i] = '/';
                ++i;
            }
        }

        static readonly SpanAction<Char, int> SetToRootAction = SetToRootFn;

        static String GetToRoot(String local)
        {
            int c = 0;
            var l = local.Length;
            for (int i = 0; i < l; ++ i)
            {
                if (local[i] == '/')
                    ++c;
            }
            if (c <= 0)
                return String.Empty;
            return String.Create(c * 3, 0, SetToRootAction);
        }


        HttpServerRequest ReplaceUrl(HttpServerRequest s, String newUrl, String newMethod = null)
        {
            var host = GetHost(out var prefix, out var queryStart, out var didIndex, ref newUrl);
            if (prefix == null)
                return null;
            return s.ReplaceUrl(newUrl, host, prefix, queryStart, this, newMethod);
        }


        /// <summary>
        /// This method may only be called inside the Handler method in the IHttpServerModule interface implementations.
        /// Internally redirect a request (the client will see the original url, it's just the response data that will be read from somewhere else).
        /// </summary>
        /// <param name="caller">Should be the this object</param>
        /// <param name="data">Should be the data that was the argument to the Handler method.</param>
        /// <param name="newUrl">The new url to read data from, only the local part is changed</param>
        /// <returns>A handler if the newUrl can be handled by the server</returns>
        public async ValueTask<IHttpRequestHandler> InternalRedirect(IHttpServerModule caller, HttpServerRequest data, String newUrl)
        {
#if DEBUG
            if (caller == null)
                throw new ArgumentNullException(nameof(caller));
#endif//DEBUG
            var n = ReplaceUrl(data, newUrl);
            using var _ = n as IDisposable;
            try
            {
                var res = await GetHandler(n, caller).ConfigureAwait(false);
                if (res != null)
                {
                    //res.Redirected = n;
                    return res;
                }
            }
            catch
            {
            }
            //(n as IDisposable)?.Dispose();
            return null;
        }



        /// <summary>
        /// Read (and optional decompress) the data from a request handler
        /// </summary>
        /// <param name="h">The handler</param>
        /// <param name="data">The request data</param>
        /// <param name="ifNoneMatch">Optionally a last modified string, if this string equals the last modified string, null is returned</param>
        /// <returns>The data and the last modified string</returns>
        async ValueTask<Tuple<ReadOnlyMemory<Byte>, String, IHttpRequestHandler, HttpServerRequest>> InternalRead(IHttpRequestHandler h, HttpServerRequest data, String ifNoneMatch = null)
        {
            var etag = h.GetEtag(out var useAsync, data);
            if (etag.FastEquals(ifNoneMatch))
                return null;
            var comp = h.Decoder;
            if (useAsync)
            {
                using var i = await h.GetAsync(data).ConfigureAwait(false);
                var s = i.Stream;
                if (s != null)
                    return new Tuple<ReadOnlyMemory<Byte>, String, IHttpRequestHandler, HttpServerRequest>(await (comp == null ? s.ReadAllMemoryAsync() : comp.GetDecompressedAsync(s)).ConfigureAwait(false), etag, h, data);
                var mem = i.Mem;
                if (i.IsMapped)
                    mem = CloneMemory(mem);
                return new Tuple<ReadOnlyMemory<Byte>, String, IHttpRequestHandler, HttpServerRequest>(comp == null ? mem : comp.GetDecompressed(mem.Span), etag, h, data);
            }else
            {
                using var i = h.Get(data);
                var s = i.Stream;
                if (s != null)
                    return new Tuple<ReadOnlyMemory<Byte>, String, IHttpRequestHandler, HttpServerRequest>(await (comp == null ? s.ReadAllMemoryAsync() : comp.GetDecompressedAsync(s)).ConfigureAwait(false), etag, h, data);
                var mem = i.Mem;
                if (i.IsMapped)
                    mem = CloneMemory(mem);
                return new Tuple<ReadOnlyMemory<Byte>, String, IHttpRequestHandler, HttpServerRequest>(comp == null ? mem : comp.GetDecompressed(mem.Span), etag, h, data);
            }

        }

        public async Task<Tuple<ReadOnlyMemory<Byte>, String, IHttpRequestHandler, HttpServerRequest>> InternalRead(HttpServerRequest data, String newUrl, HttpSession session = null, String ifNotModifiedSince = null)
        {
            var n = ReplaceUrl(data, newUrl, "GET");
            try
            {
                var res = await GetHandler(n, null).ConfigureAwait(false);
                if (res == null)
                {
                    if (session == null)
                        return null;
                    if (OptionalEndPoints.TryGetValue(n.LocalUrl, out var oep))
                        res = await oep(data, session).ConfigureAwait(false);
                    if (res == null)
                        return null;
                }
                return await InternalRead(res, n, ifNotModifiedSince).ConfigureAwait(false);
            }
            catch
            {
            }
            (n as IDisposable)?.Dispose();
            return null;

        }



        async ValueTask<bool> HandleRaw(HttpServerRequest data)
        {
            var pm = PerfMon;
            using var _ = pm.Track(nameof(HandleRaw));
            var n = nameof(HandleRaw) + ".";
            var prefixes = PrefixRawMods;
            if (prefixes != null)
            {
                var local = data.LocalUrl;
                var prefixModules = prefixes.PrefixesOf(local);
                var prefixModuleLen = prefixModules.Count;
                for (int pmi = 0; pmi < prefixModuleLen; ++pmi)
                {
                    var modules = prefixModules[pmi];
                    var moduleLen = modules.Count;
                    for (int mi = 0; mi < moduleLen; ++mi)
                    {
                        var module = modules[mi];
                        using var __ = pm.Track(n + module.Name);
                        try
                        {
                            if (await module.Handle(data).ConfigureAwait(false))
                                return true;
                        }
                        catch (Exception ex)
                        {
                            await OnException(ex, data).ConfigureAwait(false);
                            return true;
                        }
                    }
                }
            }
            var orderedMods = OrderedRawMods;
            var oml = orderedMods.Length;
            for (int mi = 0; mi < oml; ++mi)
            {
                var module = orderedMods[mi];
                using var __ = pm.Track(n + module.Name);
                try
                {
                    if (await module.Handle(data).ConfigureAwait(false))
                        return true;
                }
                catch (Exception ex)
                {
                    await OnException(ex, data).ConfigureAwait(false);
                    return true;
                }
            }
            return false;
        }


        async ValueTask<IHttpRequestHandler> GetHandler(HttpServerRequest data, IHttpServerModule ignoreThis = null)
        {
            var pm = PerfMon;
            using var _ = pm.Track(nameof(GetHandler));
            var n = nameof(GetHandler) + ".";
            var prefixes = PrefixMods;
            if (prefixes != null)
            {
                var local = data.LocalUrl;
                var prefixModules = prefixes.PrefixesOf(local);
                var prefixModuleLen = prefixModules.Count;
                for (int pmi = 0; pmi < prefixModuleLen; ++ pmi)
                {
                    var modules = prefixModules[pmi];
                    var moduleLen = modules.Count;
                    for (int mi = 0; mi < moduleLen; ++ mi) 
                    {
                        var module = modules[mi];
                        if (module == ignoreThis)
                            continue;
                        using var __ = pm.Track(n + module.Name);
                        IHttpRequestHandler t;
                        var a = module.AsyncHandler;
                        if (a != null)
                        {
                            t = await a(data).ConfigureAwait(false);
                        }
                        else
                        {
                            t = module.Handler(data);
                        }
                        if (t != null)
                            return t;
                    }
                }
            }
            var orderedMods = OrderedMods;
            var oml = orderedMods.Length;
            for (int mi = 0; mi < oml; ++ mi)
            {
                var module = orderedMods[mi];
                if (module == ignoreThis)
                    continue;
                using var __ = pm.Track(n + module.Name);
                IHttpRequestHandler t;
                var a = module.AsyncHandler;
                if (a != null)
                {
                    t = await a(data).ConfigureAwait(false);
                }else
                {
                    t = module.Handler(data);
                }
                if (t != null)
                    return t;
            }
            return null;
        }


        static readonly ValueTask<IHttpRequestHandler> LoginTrueHandler = ValueTask.FromResult<IHttpRequestHandler>(new StaticMemoryHttpRequestHandler("login", nameof(HttpServerBase), Encoding.UTF8.GetBytes("1"), "text/javascript; charset=UTF-8", null, 0, 0));
        static readonly ValueTask<IHttpRequestHandler> LoginFalseHandler = ValueTask.FromResult<IHttpRequestHandler>(new StaticMemoryHttpRequestHandler("login", nameof(HttpServerBase), Encoding.UTF8.GetBytes("0"), "text/javascript; charset=UTF-8", null, 0, 0));
        static readonly ValueTask<IHttpRequestHandler> LoginTrueAuthHandler = ValueTask.FromResult<IHttpRequestHandler>(new StaticMemoryHttpRequestHandler("login", nameof(HttpServerBase), Encoding.UTF8.GetBytes("1"), "text/javascript; charset=UTF-8", null, 0, 0, null, null, null, Array.Empty<String>() ));

        ValueTask<IHttpRequestHandler> HandleBasicAuth(HttpServerRequest data, HttpSession session)
            => AllowAuthorizationAuth ? LoginTrueHandler : LoginFalseHandler;



        ValueTask<IHttpRequestHandler> HandleLogin(HttpServerRequest data, HttpSession session)
            => LoginTrueAuthHandler;

        public bool ForceLogout(HttpServerRequest data, HttpSession session)
        {
            bool didClose = session.Auth != null;
            if (didClose)
            {
                try
                {
                    OnLogout?.Invoke(data);
                }
                catch (Exception ex)
                {
                    LogoutErrors.OnException(ex);
                }
                RunOnLogout(session);
                session.SetAuth(null);
                session.InvalidateCache();
            }
            return didClose;
        }

        static readonly HttpApiAudit AuthRedirectAudit = new HttpApiAudit("auth/redirect", "auth");

        ValueTask<IHttpRequestHandler> HandleEmpty(HttpServerRequest data, HttpSession session)
        {
            data.SetResMime(MimeTypeMap.GetMimeType(data.LocalUrl).Item1 ?? MimeTypeMap.Data);
            data.SetResStatusCode(200);
            data.SetResHeader("Cache-Control", "max-age=" + WebApiTools.CacheClientStatic);
            return HttpServerTools.AlreadyHandledValueTask;
        }

        async ValueTask HandleAuthRedirect(HttpServerRequest data, HttpSession session)
        {
            var a = session.Auth;
            var p = data.QueryParamsLowercase;
            if (!p.TryGetValue("u", out var url))
                throw new Exception("No 'u' query parameter found, expecting a redirect url");
            if (a == null)
            {
                if (!p.TryGetValue("t", out var token))
                    throw new Exception("No 't' query parameter found, expecting a one time use token");
                var audit = Audit;
                var track = audit != null;
                var trackId = track ? ApiAudit.GetId() : 0;
                var api = AuthRedirectAudit;
                try
                {
                    if (track)
                    {
                        var safeToken = token.SecureEnd();
                        audit.OnApiBegin(trackId, data, api, safeToken);
                    }
                    var user = await Auth.TokenAuth(token).ConfigureAwait(false);
                    if (user == null)
                    {
                        //  We don't have an auth manager so we must fail, no point retrying with user/password
                        data.SetResStatusCode(401);
                        const String message = "Unauthorized - The token is invalid!";
                        if (!data.IsHead)
                            data.SetResText(await Translator.TranslateSafe(message, session.Language, "en", "An error message that is displayed when an authorization token is invalid").ConfigureAwait(false));
                        audit.OnApiException(trackId, data, api, new Exception(message));
                        return;
                    }
                    session.SetAuth(user);
                    await data.Server.RunOnLogin(session).ConfigureAwait(false);
                    if (track)
                        audit.OnApiEnd(trackId, data, api, url);
                }
                catch (Exception ex)
                {
                    audit.OnApiException(trackId, data, api, ex);
                }
            }
            data.SetResHeader("Location", url);
            data.SetResStatusCode(302);
        }

        async ValueTask HandleLogoutUser(HttpServerRequest data, HttpSession session)
        {
            var auth = session.Auth;
            if (auth == null)
                return;
            var pos = data.QueryStringStart;
            if (pos <= 0)
                throw new Exception("No query paramater! Expected a '?' followed by a session id");
            var sessionId = data.Url.Substring(pos);
            if (!auth.Auth.TryLogoutTokenAuth(auth, sessionId))
            {
                if (!data.IsHead)
                    data.SetResText("false", HttpServerTools.JsonMime);
                return;
            }
            await HandleLogout(data, session).ConfigureAwait(false);
        }

        ValueTask HandleLogout(HttpServerRequest data, HttpSession session)
        {
            bool didClose = ForceLogout(data, session);
            if (didClose)
            {
                if (String.IsNullOrEmpty(AuthRedirect))
                {
                    var am = Auth;
                    if (am != null)
                        data.SetResHeader("WWW-Authenticate", "Basic realm=" + am.Realm);
                    data.SetResStatusCode(401);
                    if (!data.IsHead)
                        data.SetResText(didClose ? "true" : "false", HttpServerTools.JsonMime);
                    return ValueTask.CompletedTask;
                }
            }
            if (!data.IsHead)
                data.SetResText(didClose ? "true" : "false", HttpServerTools.JsonMime);
            return ValueTask.CompletedTask;
        }

        ValueTask HandleServerTime(HttpServerRequest data, HttpSession session)
        {
            data.SetResMime("text/plain; charset=UTF-8");
            if (!data.IsHead)
            {
                var tz = data.GetQuery();
                if (!String.IsNullOrEmpty(tz))
                {
                    var ss = tz.Split(';');
                    session.ClientTimeZone = ss[0];
                }
                var val = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Span<Byte> temp = stackalloc byte[128];
                int i = 0;
                do
                {
                    var nv = val / 10;
                    temp[i] = (Byte)(val - (nv * 10) + '0');
                    val = nv;
                    ++i;
                } while (val != 0);
                int l = i;
                int j = 0;
                while (j < i)
                {
                    --i;
                    var t2 = temp[i];
                    temp[i] = temp[j];
                    temp[j] = t2;
                    ++j;
                }
                data.SetResBody(temp.Slice(0, l));
            }
            return ValueTask.CompletedTask;
        }



        readonly IReadOnlyDictionary<String, Func<HttpServerRequest, HttpSession, ValueTask>> ForcedEndPoints;
        readonly IReadOnlyDictionary<String, Func<HttpServerRequest, HttpSession, ValueTask<IHttpRequestHandler>>> OptionalEndPoints;


        public async ValueTask<String> Get404Text(String language)
        {
            return await Translator.TranslateSafe("Not Found - The server cannot find the requested resource.", language, "en", "This is the message to display when trying to access a non-existing end point in a web server").ConfigureAwait(false);

        }

        async ValueTask Set404(HttpServerRequest data)
        {
            data.SetResStatusCode(404);
            if (!data.IsHead)
                data.SetResText(await Get404Text(data.Language).ConfigureAwait(false));
        }

        async ValueTask Set429(HttpServerRequest data)
        {
            data.SetResStatusCode(429);
            if (!data.IsHead)
                data.SetResText(await Translator.TranslateSafe("Too Many Requests - The client has sent too many requests in a given amount of time.", data.Language, "en", "This is the message to display when trying to access an end point more frequent than allowed in a web server").ConfigureAwait(false));
        }

        /// <summary>
        /// Validate an enpoint against a session.
        /// </summary>
        /// <param name="auth"></param>
        /// <param name="data"></param>
        /// <param name="session"></param>
        /// <param name="localUrl"></param>
        /// <param name="url"></param>
        /// <param name="isHead"></param>
        /// <returns>True if the request have been handled to completion, false if the request is allowed to continue</returns>
        async ValueTask<bool> HandleAuth(IReadOnlyList<String> auth, HttpServerRequest data, HttpSession session, String localUrl, String url, bool isHead)
        {
            using var _ = PerfMon.Track(nameof(HandleAuth));
            var user = session.Auth;
            if (user == null)
            {
                //  This request is protected
                var am = Auth;
                if (am == null)
                {
                    //  We don't have an auth manager so we must fail, no point retrying with user/password
                    data.SetResStatusCode(401);
                    if (!isHead)
                        data.SetResText(await Translator.TranslateSafe("Unauthorized - No auth manager found! Endpoints requiring auth is not accessible!", session.Language, "en", "This is the message to display when trying to access a protected end point in a web server that doesn't have any way to authenticate a user").ConfigureAwait(false));
                    return true;
                }
                //  Get request auth
                var h = data.GetReqHeader("Authorization");
                if (h == null)
                {
                    var ad = AuthRedirect;
                    if (ad != null)
                    {
                        ad = GetToRoot(localUrl) + ad;
                        if (!localUrl.EndsWith(".html"))
                        {
                            var li = ad.LastIndexOf('?');
                            if (li >= 0)
                                ad = ad.Substring(0, li);
                        }
                        data.SetResHeader("Location", String.Format(ad, HttpUtility.UrlEncode(url)));
                        data.SetResStatusCode(302);
                        return true;
                    }
                    if (AllowAuthorizationAuth)
                    {
                        //  No auth in request, fail and request auth to be supplied
                        data.SetResHeader("WWW-Authenticate", "Basic realm=" + am.Realm);
                        data.SetResStatusCode(401);
                    }
                    else
                    {
                        data.SetResStatusCode(401);
                        if (!isHead)
                            data.SetResText(await Translator.TranslateSafe("Unauthorized - Authorization header is not allowed, can't authorize!", session.Language, "en", "This is the message to display when trying to access a protected end point using an Authorization request header in a web server that doesn't allow using the Authorization header").ConfigureAwait(false));
                    }
                    return true;
                }
                if (AllowAuthorizationAuth)
                {
                    //  Get user
                    var httpAuth = await am.Http(h).ConfigureAwait(false);
                    user = httpAuth.Item1;
                    if (user == null)
                    {
                        if (httpAuth.Item2)
                        {
                            //  No valid user.. try again
                            data.SetResHeader("WWW-Authenticate", "Basic realm=" + am.Realm);
                            data.SetResStatusCode(401);
                            return true;
                        }
                        data.SetResStatusCode(403);
                        return true;
                    }
                    if (session != null)
                    {
                        session.SetAuth(user);
                        await data.Server.RunOnLogin(session).ConfigureAwait(false);
                    }
                }
                else
                {
                    data.SetResStatusCode(401);
                    if (!isHead)
                        data.SetResText(await Translator.TranslateSafe("Unauthorized - Authorization header is not allowed, can't authorize!", session.Language, "en", "This is the message to display when trying to access a protected end point using an Authorization request header in a web server that doesn't allow using the Authorization header").ConfigureAwait(false));
                    return true;
                }
            }
            if (!user.IsValid(auth))
            {
                //  User doesn't have the correct security tokens
                data.SetResStatusCode(403);
                return true;
            }
            return false;
        }

#if DEBUG
        readonly AsyncLock DebugLock = new AsyncLock();
#endif//DEBUG



        public async ValueTask Handle(HttpServerRequest data)
        {
            var rateLimiter = ServerLimits;
            if ((rateLimiter != null) && (await rateLimiter.IsOverTheLimit().ConfigureAwait(false)))
            {
                await Set429(data).ConfigureAwait(false);
                return;
            }
            if (HaveRawModules && await HandleRaw(data).ConfigureAwait(false))
                return;
            using var session = await GetSession(data).ConfigureAwait(false);
            session.IncRequestCounter();
            data.Init(session);
            rateLimiter = session.RateLimiter;
            if ((rateLimiter != null) && (await rateLimiter.IsOverTheLimit().ConfigureAwait(false)))
            {
                await Set429(data).ConfigureAwait(false);
                return;
            }
            if ((ExternalRootUri == null) || (!ExternalRootUriFromRequest))
            {
                ExternalRootUriFromRequest = true;
                ExternalRootUri = data.Prefix;
            }
            //  Handle forced end points (internal end points that can't be overridden)
            data.SetResStatusCode(200);
            var localUrl = data.LocalUrl;
            if (ForcedEndPoints.TryGetValue(localUrl, out var fep))
            {
                await fep(data, session).ConfigureAwait(false);
                return;
            }
            //  Get module handler
            var t = await GetHandler(data).ConfigureAwait(false);
            if (t == null)
            {
                //  Optional end points (internal end points that can be overridden)
                if (OptionalEndPoints.TryGetValue(localUrl, out var oep))
                    t = await oep(data, session).ConfigureAwait(false);
                if (t == null)
                {
                    //  No handler found!
                    await Set404(data).ConfigureAwait(false);
                    return;
                }
            }
            if (t == HttpServerTools.AlreadyHandled)
                return;
            var newData = t.Redirected;
            using var _ = newData as IDisposable;
            data = newData ?? data;
            //  Rate limiting
            rateLimiter = t.ServiceRateLimiter;
            if ((rateLimiter != null) && (await rateLimiter.IsOverTheLimit().ConfigureAwait(false)))
            {
                await Set429(data).ConfigureAwait(false);
                return;
            }
            rateLimiter = t.SessionRateLimiter(session);
            if ((rateLimiter != null) && (await rateLimiter.IsOverTheLimit().ConfigureAwait(false)))
            {
                await Set429(data).ConfigureAwait(false);
                return;
            }
#if DEBUG
            //using var ___ = await DebugLock.Lock().ConfigureAwait(false); // Enabled this line to handle one request at a time
#endif//DEBUG

            bool isHead = data.IsHead;
            if (isHead && AllowAuthorizationAuth)
                data.SetResHeader("Access-Control-Allow-Headers", "Authorization");
            var url = data.Url;
            var lang = session.Language ?? "";
            var translator = Translator;
            var haveTranslator = translator != null;
            try
            {
                //  Auth required
                var auth = t.Auth;
                if (auth != null)
                    if (await HandleAuth(auth, data, session, localUrl, url, isHead).ConfigureAwait(false))
                        return;
                using var __ = PerfMon.Track((nameof(Handle) + ".") + t.Name);
                var etag = t.GetEtag(out bool useAsync, data);
                var ee = data.Etag;
                if (ee != null)
                    etag = etag == null ? ee : String.Concat('_', etag, ee);
                var rcd = data.RequestCacheDuration ?? t.RequestCacheDuration;

                //  Get template and prevent caching for dynamic templates 
                var extPos = localUrl.LastIndexOf('.');
                var ext = extPos < 0 ? "" : localUrl.Substring(extPos + 1).FastToLower();

                //  Auto translation based on file extensions (todo: use mime instead?)
                LanguageTemplate.ExtBuilders.TryGetValue(ext, out var langTemplateBuilder);
                var isLanguageTemplate = langTemplateBuilder != null;
                bool useLanguageCache = t.IsLocalized || isLanguageTemplate;
                Temp textTemplate = null;
                TextTemplate cachedTemplate = null;
                LanguageTemplate langTemplate = null;
                bool isDynamicTemplate = false;
                bool createTemplate = false;
                bool createTranslation = false;
                var qs = data.QueryStringStart;
                if (etag != null) 
                {
                    var prevLm = etag;
                    var prevRcd = rcd;
                    if (isLanguageTemplate)
                        etag = String.Concat(etag, ' ', lang);
                    var allowVarTemplate = t.AllowTemplates;
                    if (isLanguageTemplate || allowVarTemplate)
                    {
                        textTemplate = GetTextTemplate(localUrl, allowVarTemplate, isLanguageTemplate);
                        if (textTemplate != null)
                        {
                            cachedTemplate = textTemplate.Get(out createTemplate, out createTranslation, out isDynamicTemplate, out langTemplate, etag, lang);
                            if (cachedTemplate != null) 
                            {
                                if (isDynamicTemplate)
                                {
                                    etag = null;
                                    rcd = 0;
                                }
                            }
                            if (qs > 0)
                            {
                                if (data.Url.FastSubEquals(qs, "raw"))
                                {
                                    if (session.IsValid(AuthTools.DevAuth))
                                    {
                                        textTemplate = null;
                                        cachedTemplate = null;
                                        etag = prevLm;
                                        rcd = prevRcd;
                                        useLanguageCache = false;
                                        createTemplate = false;
                                        createTranslation = false;
                                    }
                                }
                            }
                        }
                    }
                }
                //  Handle 304's
                if (etag != null)
                {
                    if (etag.FastEquals(data.IfNoneMatch))
                    {
                        data.SetResStatusCode(304);
                        return;
                    }
                }
                //  Handle cached requests
                String cacheKey = String.Empty;
                ConcurrentDictionary<String, HttpCacheEntry> cache = null;
                var now = DateTime.UtcNow;
                var nowT = now.Ticks;
                if (rcd != 0)
                {
                    cache = ((rcd < 0) || isDynamicTemplate) ? session.SaveCache : Cache;
                    if (rcd < 0)
                        rcd = -rcd;
                    var key = (await t.GetCacheKey(data).ConfigureAwait(false)) ?? url;
                    if (key.Length > 0)
                    {
                        key = String.Concat(key, '\n', data.GetType().Name);
                        Interlocked.Increment(ref CacheTotal);
                        cacheKey = 
                            useLanguageCache && haveTranslator
                            ?
                            String.Join('\n', key, data.AcceptEncoding, data.Method, lang)
                            :
                            String.Join('\n', key, data.AcceptEncoding, data.Method);
                        if (cache.TryGetValue(cacheKey, out var ce))
                        {
                            if (nowT < ce.Expires)
                            {
                                Interlocked.Exchange(ref ce.LastUsed, nowT);
                                using (PerfMon.Track(nameof(HttpCacheEntry.SendCached)))
                                    await ce.SendCached(data, isHead).ConfigureAwait(false);
                                Interlocked.Increment(ref CacheHit);
                                return;
                            }
                        }
                    }
                }
                var output = data.OutputStream;
                var ccd = data.ClientCacheDuration ?? t.ClientCacheDuration;
                var dec = t.Decoder;


                //  Handle cached templates
                bool haveTemplate = cachedTemplate != null;
                IReadOnlyDictionary<String, String> vars = null;
                IReadOnlyDictionary<String, String> langVars = null;
                if (langTemplate != null)
                {
                    if (haveTemplate)
                        vars = GetVars(true, data);
                    if (isDynamicTemplate)
                        langVars = await GetTranslationVars(lang, langTemplate, vars).ConfigureAwait(false);
                    else
                        langVars = await langTemplate.LangVars.GetOrUpdateValueAsync(lang, GetTranslationVars, langTemplate, vars).ConfigureAwait(false);
                }
                //  Set mime unless it's already set
                var mime = data.GetResMime();
                var orgMime = mime;
                if (mime == null)
                    if (ext.Length > 0)
                        mime = MimeTypeMap.GetMimeType(ext).Item1;
                //  Handle mime transformers
                if (!t.IsDynamic)
                {
                    if ((mime != null) && (etag != null))
                    {
                        var trans = Transformers;
                        var mimeFirst = mime.SplitFirst(';').Trim();
                        if (trans.TryGetValue(mimeFirst, out var chain) || trans.TryGetValue(ext, out chain))
                        {
                            if (!data.Url.FastSubEquals(qs, "raw"))
                            {
                                var transformers = chain.Transformers;
                                var transformerLen = transformers.Length;
                                HttpRequestTransformerState state = null;
                                for (int transformerIndex = 0; transformerIndex < transformerLen; ++transformerIndex)
                                {
                                    try
                                    {
                                        state = state ?? new HttpRequestTransformerState(data, etag, mime, t, useAsync, ext);
                                        if (await transformers[transformerIndex](state).ConfigureAwait(false))
                                        {
                                            mime = state.Mime;
                                            t = state.Handler;
                                            useAsync = state.UseAsync;
                                            dec = t.Decoder;
                                            state = null;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TransformExceptions.OnException(ex);
                                    }
                                }
                            }
                        }
                    }
                }
                if ((mime != null) && (!mime.FastEquals(orgMime)))
                    data.SetResMime(mime);

                using var i = useAsync ? await t.GetAsync(data).ConfigureAwait(false) : t.Get(data);
                { 
                    if (haveTemplate)
                    {
                        dec = null;
                        i.ChangeMem(ApplyTemplate(cachedTemplate, vars ?? GetVars(isDynamicTemplate || isLanguageTemplate, data), langVars));
                    }

                    //  Get data (stream or memory)
                    /*using (var i =
                        useStream
                        ?
                        new Input(
                            useAsync
                            ?
                            (await t.GetStreamAsync(data).ConfigureAwait(false))
                            :
                            (haveTemplate 
                            ? ApplyTemplate(cachedTemplate, vars ?? GetVars(isDynamicTemplate || isLanguageTemplate, data), langVars) : t.GetStream(data))
                            )
                        :
                        new Input(
                            useAsync
                            ?
                            (await t.GetDataAsync(data).ConfigureAwait(false))
                            :
                            t.GetData(data)
                            )
                        )
                    {*/
                    //  Create (and apply text template)
                    if (createTemplate)
                    {
                        if (textTemplate != null)
                        {
                            var ds = i.Stream;
                            using var dsMem = ds != null ? await ds.ReadAllUnmanagedMemoryAsync(true).ConfigureAwait(false) : null;
                            var fileData = dsMem?.Memory ?? i.Mem;
                            var preDecData = fileData;
                            IUnmanagedReadOnlyMemory<Byte> compMem = null;
                            if (dec != null)
                            {
                                compMem = dec.GetUnmanagedDecompressed(fileData.Span);
                                fileData = compMem.Memory;
                            }
                            using (compMem)
                            {
                                var text = Encoding.UTF8.GetString(fileData.Span);
                                if (isLanguageTemplate)
                                {
                                    langTemplate = langTemplateBuilder(text, haveTranslator, false); // TODO: Build 2 templates ans use different depending on allowing browser translation
                                    text = langTemplate.Text;
                                }
                                cachedTemplate = new TextTemplate(text, "${", "}");
                                if (cachedTemplate.HaveVars)
                                {
                                    isDynamicTemplate = IsDynamic(cachedTemplate);
                                    vars = vars ?? GetVars(isDynamicTemplate || isLanguageTemplate, data);
                                    langVars = langVars ?? (langTemplate == null ? null : await langTemplate.LangVars.GetOrUpdateValueAsync(lang, GetTranslationVars, langTemplate, vars).ConfigureAwait(false));
                                    dec = null;
                                    textTemplate.Set(cachedTemplate, isDynamicTemplate, etag, langTemplate, lang);
                                    i.ChangeMem(ApplyTemplate(cachedTemplate, vars, langVars));
                                }
                                else
                                {
                                    textTemplate.Set(null, false, etag, null, null);
                                    i.ChangeMem(preDecData);
                                }
                            }
                        }
                    }else
                    {
                        if (createTranslation)
                        {
                            if (cachedTemplate.HaveVars)
                            {
                                vars = vars ?? GetVars(isDynamicTemplate || isLanguageTemplate, data);
                                langVars = langVars ?? (langTemplate == null ? null : await langTemplate.LangVars.GetOrUpdateValueAsync(lang, GetTranslationVars, langTemplate, vars).ConfigureAwait(false));
                                dec = null;
                                textTemplate.SetLangLm(etag, lang);
                                i.ChangeMem(ApplyTemplate(cachedTemplate, vars, langVars));
                            }
                            else
                            {
                                textTemplate.SetLangLm(etag, lang); // Should never reach this!?
                            }
                        }
                    }
                    if (etag != null)
                        data.SetResHeader("ETag", etag);
                    //data.SetResHeader("Last-Modified", lm);
                    //data.SetResHeader("Date", null);
                    if (isDynamicTemplate)
                        if (ccd > 1)
                            ccd = 1;
                    data.SetResHeader("Cache-Control", ccd > 0 ? ("max-age=" + ccd) : "no-cache"/*"must-revalidate"*/);
 
                    //  Determine compression
                    var acc = data.AcceptEncoding;
                    var acceptedEncoders = data.AcceptedEncoders;
                    var comp = t.Compression?.GetEncoder(acc, acceptedEncoders);
                    data.CompEncoder = comp;
                    //  Handle pre-compressed data (use directly or decompress)
                    if (dec != null)
                    {
                        //  If encoded as gzip..
                        if (dec.HttpCode.FastEquals("gzip"))
                        {
                            // ..and accept deflate
                            if (acceptedEncoders.Contains("deflate"))
                            {
                                //  Use gzip => deflate (this doesn't copy any data)
                                if (GZipToDeflate(i))
                                {
                                    data.SetResHeader("Content-Encoding", "deflate");
                                    //  No compression / caching required since we use the source data directly
                                    dec = null;
                                    comp = null;
                                    rcd = 0;
                                }
                            }
                        }
                        if (dec != null)
                        {
                            //  ..and we can send it as is, do it (and skip any caching)
                            if (acceptedEncoders.Contains(dec.HttpCode))
                            {
                                data.SetResHeader("Content-Encoding", dec.HttpCode);
                                //  No compression / caching required
                                dec = null;
                                comp = null;
                                rcd = 0;
                            }
                        }
                        //  If we have to decompress the input
                        if (dec != null)
                        {
                            if ((comp != null) || (rcd > 0))
                            {
                                //  Decompress to memory if we need to cache or compress
                                await Decompress(dec, i).ConfigureAwait(false);
                            }
                            else
                            {
                                //  Decompress to output directly if we shouldn't cache or compress
                                using (PerfMon.Track(nameof(Decompress)))
                                    await Decompress(dec, i, data.OutputStream).ConfigureAwait(false);
                                return;
                            }
                        }
                    }

                    //  Parse range header
                    var range = data.GetReqHeader("Range");
                    bool haveRange = false;
                    long skip = 0;
                    long limit = -1;
                    if (range != null)
                    {
                        range = range.Trim();
                        var rangeTemp = range.Split('=');
                        if (rangeTemp.Length == 2)
                        {
                            var rangeT = rangeTemp[0].TrimEnd().FastToLower();
                            if (rangeT.FastEquals("bytes"))
                            {
                                rangeTemp = rangeTemp[1].TrimStart().Split('-');
                                if (long.TryParse(rangeTemp[0].TrimEnd(), out var rangeStart))
                                {
                                    if (rangeStart >= 0)
                                    {
                                        if (rangeTemp.Length > 1)
                                        {
                                            var rtemp = rangeTemp[1].TrimStart();
                                            if (rtemp.Length > 0)
                                            {
                                                if (long.TryParse(rtemp, out var rangeEnd))
                                                {
                                                    var rangeLen = rangeEnd - rangeStart + 1;
                                                    if (rangeLen >= 0)
                                                    {
                                                        haveRange = true;
                                                        skip = rangeStart;
                                                        limit = rangeLen;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                haveRange = true;
                                                skip = rangeStart;
                                            }
                                        }
                                        else
                                        {
                                            haveRange = true;
                                            skip = rangeStart;
                                        }
                                    }
                                }
                            }
                        }
                        if (!haveRange)
                        {
                            data.SetResHeader("Accept-Ranges", "bytes");
                            data.SetResStatusCode(416);
                            return;
                        }
                    }

                    //  Apply compression
                    if (comp != null)
                    {
                        if ((rcd > 0) && (limit < 0) && (skip == 0))
                        {
                            //  Compress to memory if we need to cache
                            var compType = await Compress(comp.Item1, rcd >= 5 ? CompEncoderLevels.Best : comp.Item2, i).ConfigureAwait(false);
                            if (compType != null)
                                data.SetResHeader("Content-Encoding", compType);
                        }
                        else
                        {
                            if (isHead)
                                return;
                            //  Compress to output directly if we shouldn't cache
                            var encoder = comp.Item1;
                            data.SetResHeader("Content-Encoding", encoder.HttpCode);
                            using (PerfMon.Track(nameof(Compress)))
                                await Compress(encoder, comp.Item2, i, data.OutputStream).ConfigureAwait(false);
                            return;
                        }
                    }

                    //  Cache
                    if (rcd > 0)
                        await SaveToCache(cache, cacheKey, now.AddSeconds(rcd), nowT, i, data, data.LocalUrl).ConfigureAwait(false);

                    //  Get length
                    var s = i.Stream;
                    long length = -1;
                    if (s != null)
                    {
                        if (s.CanSeek)
                            length = s.Length - s.Position;
                    }
                    else
                    {
                        length = i.Mem.Length;
                    }

                    //  Write range headers
                    if (haveRange)
                    {
                        if (length < 0)
                        {
                            data.SetResHeader("Accept-Ranges", "none");
                            data.SetResStatusCode(416);
                            return;
                        }
                        if (limit < 0)
                        {
                            data.SetResHeader("Content-Range", "bytes " + skip + "-" + (length - 1) + "/" + length);
                        }
                        else
                        {
                            if ((skip + limit) > length)
                            {
                                data.SetResStatusCode(416);
                                return;
                            }
                            data.SetResHeader("Content-Range", "bytes " + skip + "-" + (skip + limit - 1) + "/" + length);
                        }
                        data.SetResStatusCode(206);
                    }
                    if (isHead)
                    {
                        if (length >= 0)
                        {
                            length -= skip;
                            if (length < 0)
                                length = 0;
                            if ((limit >= 0) && (length > limit))
                                length = limit;
                            data.SetResContentLength(length);
                        }
                        return;
                    }
                    //  Write final
                    //var output = res.OutputStream;
                    if (s != null)
                    {
                        //  Stream to stream
                        if (s.CanSeek)
                        {
                            if (skip > length)
                                skip = length;
                            if (skip > 0)
                            {
                                length -= skip;
                                s.Position += skip;
                            }
                            if (limit >= 0)
                            {
                                if (limit < length)
                                    length = limit;
                                else
                                    limit = length;
                            }
                            data.SetResContentLength(length);
                        }
                        else
                        {
                            var buf = Dummy;
                            var blen = buf.Length;
                            while (skip > 0)
                            {
                                var take = skip;
                                if (take > blen)
                                    take = blen;
                                await s.ReadAsync(buf, 0, (int)take).ConfigureAwait(false);
                                skip -= take;
                            }
                        }
                        if (limit >= 0)
                        {
                            const int bufferSize = 81920;
                            byte[] buffer = ArrayPoolStream.Rent(bufferSize);
                            try
                            {
                                long read = 0;
                                while (read < limit)
                                {
                                    var take = limit - read;
                                    if (take > bufferSize)
                                        take = bufferSize;
                                    await s.ReadAsync(buffer, 0, (int)take).ConfigureAwait(false);
                                    read += take;
                                    await output.WriteAsync(buffer, 0, (int)take).ConfigureAwait(false);
                                }
                            }
                            finally
                            {
                                ArrayPoolStream.Return(buffer);
                            }
                        }
                        else
                        {
                            await s.CopyToAsync(output).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        //  Memory to stream
                        var b = i.Mem;
                        var len = b.Length;
                        if (skip > 0)
                        {
                            if (skip > len)
                                skip = len;
                            len -= (int)skip;
                            b = b.Slice((int)skip, len);
                        }
                        if (limit >= 0)
                        {
                            if (limit < len)
                            {
                                len = (int)limit;
                                b = b.Slice(0, len);
                            }
                        }
                        data.SetResContentLength(len);
                        if (!b.IsEmpty)
                            await output.WriteAsync(b).ConfigureAwait(false);
                    }
                }

            }
            catch (Exception ex)
            {
                await OnException(ex, data).ConfigureAwait(false);
            }
        }

        async ValueTask OnException(Exception ex, HttpServerRequest data)
        {
            var le = ListenerExceptionType;
            if ((le != null) && (le.IsAssignableFrom(ex.GetType())))
            {
                ListenerExceptions.OnException(ex);
            }
            else
            {
                bool isHead = data.IsHead;
                var session = data.Session;
                var translator = session == null ? null : data.Translator;
                var re = ex as HttpResponseException;
                if (re != null)
                {
                    data.SetResStatusCode(re.ResponseCode);
                    if (!isHead)
                    {
                        var text = re.Message;
                        var tr = re.Translate;
                        if ((translator != null) && (tr != null))
                            text = await translator.TranslateSafe(text, session.Language, tr, "This is an exception message thrown by a web server, the value at the end in the enclosing [ ] are the error code, keep as is", TranslationEffort.Medium, TranslationCacheRetention.Short).ConfigureAwait(false);
                        data.SetResText(text);
                    }
                }
                else
                {
                    HandlerExceptions.OnException(ex);
#if DEBUG
                    Msg?.AddMessage(Prefix + "Handler for \"" + data.Url + "\" failed!", ex, MessageLevels.Debug);
#endif//DEBUG
                    data.SetResStatusCode(500);
                    if (!isHead)
                        data.SetResText(await translator.TranslateSafe(ex.Message + " [500]", session.Language, "en", "This is an exception message thrown by a web server", TranslationEffort.Medium, TranslationCacheRetention.Short).ConfigureAwait(false));
                }
            }

        }

        #region Cert

        /// <summary>
        /// Called whenever a new certificate have been found.
        /// Implementations should do whatever necessary for the new certificate to take effect.
        /// </summary>
        /// <param name="cert">Certificate provider</param>
        /// <param name="pre">The prefix</param>
        /// <returns></returns>
        protected abstract Task<bool> OnNewCert(ICertificateProvider cert, String pre);

        readonly int CertRetryMinutes;
        protected readonly int FirstCertRetryMinutes;


        protected String CurrentCertThumbPrint;

        /// <summary>
        /// Call to initiate a certificate switch
        /// </summary>
        /// <param name="cert">Certificate provider</param>
        /// <param name="pre">The prefix</param>
        /// <returns></returns>
        protected async Task SwitchCert(ICertificateProvider cert, String pre)
        {
            var msg = Msg;
            try
            {
                var newCert = await cert.GetCert().ConfigureAwait(false);
                var newThumb = newCert.Thumbprint;
                if (CurrentCertThumbPrint.FastEquals(newThumb))
                    return;
                //CurrentCertThumbPrint = newThumb;
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to get certificate for " + pre.ToQuoted() + ", continue to use current", ex, MessageLevels.Warning);
                var retry = DateTime.UtcNow.AddMinutes(CertRetryMinutes);
                msg?.AddMessage(Prefix + "Will try to get new certificate at " + retry.ToString("o"));
                Scheduler.AddTask(retry, () => SwitchCert(cert, pre));
                return;
            }

            try
            {
                var ok = await OnNewCert(cert, pre).ConfigureAwait(false);
                if (ok)
                    msg?.AddMessage(Prefix + "Rebound certificate for " + pre.ToQuoted());
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Certificate binding failure for " + pre.ToQuoted(), ex, MessageLevels.Warning);
            }
        }

        /// <summary>
        /// Call this whenever a certificate have been changed (
        /// </summary>
        /// <param name="cert">Certificate provider</param>
        /// <param name="pre">The prefix</param>
        protected void OnCertChanged(ICertificateProvider cert, String pre)
        {
            Msg?.AddMessage(Prefix + "Certificate for " + pre.ToQuoted() + " changed, rebinding certs.");
            TaskExt.StartNewAsyncChain(() => SwitchCert(cert, pre).ConfigureAwait(false));
        }


        #endregion// Cert

        #region Firewall

        readonly IFirewallHandler FirewallHandler;
        readonly Stack<String> FirewallRules;

        protected void SetPrefixes(IEnumerable<HttpServerPrefix> prefixes, bool haveFirewall)
        {
            var pp = prefixes.ToArray();
            foreach (var prefix in pp)
            {
                var n = prefix.Prefix;
                if (n.FastStartsWith("http://"))
                {
                    n = n.Replace(":80/", "/");
                    if (n.FastEndsWith(":80"))
                        n = n.Substring(0, n.Length - 3);
                    prefix.Prefix = n;
                    continue;
                }
                if (n.FastStartsWith("https://"))
                {
                    n = n.Replace(":443/", "/");
                    if (n.FastEndsWith(":443"))
                        n = n.Substring(0, n.Length - 4);
                    prefix.Prefix = n;
                    continue;
                }
            }
            Prefixes = pp;
            var msg = Msg;
            if (haveFirewall)
            {
                var firewallHandler = FirewallHandler;
                if (firewallHandler == null)
                {
                    msg?.AddMessage(Prefix + "No firewall handler supplied! No firewall rules will be added!", MessageLevels.Warning);
                }
                else
                {
                    msg?.AddMessage(Prefix + "Adding/updating firewall rules");
                    using var t = msg?.Tab();
                    {
                        var remove = FirewallRules;
                        foreach (var prefix in pp)
                        {
                            if (!prefix.AddToFirewall)
                                continue;
                            var name = prefix.FirewallName?.Trim();
                            if (String.IsNullOrEmpty(name))
                                name = HttpServerPrefix.DefaultFirewallName;
                            var port = new Uri(prefix.Prefix.Replace("*", "localhost")).Port;
                            Dictionary<String, String> fwVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                { "port", port.ToString() },
                            };
                            name = EnvInfo.ResolveText(name, true, fwVars);
                            firewallHandler.AddOrSet(name, port, msg, Prefix);
                            remove?.Push(name);
                        }
                    }
                }
            }
        }

        #endregion//Firewall



        public IEnumerable<String> AllPrefixes => Prefixes.Select(x => x.Prefix);

        protected HttpServerPrefix[] Prefixes { get; private set; }

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor("HttpServer");

        readonly HttpServerBaseParams BaseParams;

        #region Auth

        readonly String AuthRedirect;
        public readonly bool AllowAuthorizationAuth;
        readonly String LogoutRedirect;

        protected readonly IMessageHost Msg;
        public readonly AuthManager Auth;

        #endregion Auth

        #region Modules

        int ModuleOrder;

        void OnModuleChange()
        {
            var m = Modules;
            lock (m)
            {
                var c = m.Count;
                List<IHttpServerModule> mods = new (c);
                IHttpServerModule[] allMods = new IHttpServerModule[c];
                List<Tuple<String, IHttpServerModule>> prefixes = new(c + c);
                int i = -1;
                foreach (var x in m.OrderBy(x => x.Value))
                {
                    ++i;
                    var mod = x.Key;
                    allMods[i] = mod;
                    var pres = mod.OnlyForPrefixes;
                    if (pres != null)
                    {
                        var pl = pres.Length;
                        if (pl > 0)
                        {
                            StringTree seen = null;
                            if (pl > 1)
                                Array.Sort(pres, (a, b) => (a?.Length ?? -1) - (b?.Length ?? -1));
                            if (!String.IsNullOrEmpty(pres[0]))
                            {
                                for (int pi = 0; pi < pl; ++pi)
                                {
                                    var pre = pres[pi];
                                    if (pre == null)
                                        continue;
                                    if (seen?.StartsWithAny(pre) != null)
                                        continue;
                                    seen = StringTree.Add(pre, false, seen);
                                    prefixes.Add(Tuple.Create(pre, mod));
                                }
                                continue;
                            }
                        }
                    }
                    mods.Add(mod);
                }
                OrderedMods = mods.ToArray();
                AllMods = allMods;
                PrefixMods = prefixes.Count > 0 ? FrozenStringTreeList.Build(prefixes) : null;
            }
        }

        public bool AddModule(IHttpServerModule module)
        {
            var m = Modules;
            if (!m.TryAdd(module, Interlocked.Increment(ref ModuleOrder)))
                return false;
            OnModuleChange();
            return true;
        }

        public bool RemoveModule(IHttpServerModule module)
        {
            var m = Modules;
            if (!m.TryRemove(module, out var x))
                return false;
            OnModuleChange();
            return true;
        }

        readonly ConcurrentDictionary<IHttpServerModule, int> Modules = new ConcurrentDictionary<IHttpServerModule, int>();
        /// <summary>
        /// All mods in order that doesn't respond to certain prefixes only
        /// </summary>
        IHttpServerModule[] OrderedMods;
        /// <summary>
        /// All mods in order
        /// </summary>
        IHttpServerModule[] AllMods;
        /// <summary>
        /// A tree with modules that respond to certain prefixes only
        /// </summary>
        FrozenStringTreeList<IHttpServerModule> PrefixMods;

        #endregion //Modules


        #region RawModules

        int RawModuleOrder;

        void OnRawModuleChange()
        {
            var m = RawModules;
            lock (m)
            {
                var c = m.Count;
                List<IHttpServerRawModule> RawMods = new(c);
                IHttpServerRawModule[] allRawMods = new IHttpServerRawModule[c];
                List<Tuple<String, IHttpServerRawModule>> prefixes = new(c + c);
                int i = -1;
                foreach (var x in m.OrderBy(x => x.Value))
                {
                    ++i;
                    var RawMod = x.Key;
                    allRawMods[i] = RawMod;
                    var pres = RawMod.OnlyForPrefixes;
                    if (pres != null)
                    {
                        var pl = pres.Length;
                        if (pl > 0)
                        {
                            StringTree seen = null;
                            if (pl > 1)
                                Array.Sort(pres, (a, b) => (a?.Length ?? -1) - (b?.Length ?? -1));
                            if (!String.IsNullOrEmpty(pres[0]))
                            {
                                for (int pi = 0; pi < pl; ++pi)
                                {
                                    var pre = pres[pi];
                                    if (pre == null)
                                        continue;
                                    if (seen?.StartsWithAny(pre) != null)
                                        continue;
                                    seen = StringTree.Add(pre, false, seen);
                                    prefixes.Add(Tuple.Create(pre, RawMod));
                                }
                                continue;
                            }
                        }
                    }
                    RawMods.Add(RawMod);
                }
                OrderedRawMods = RawMods.ToArray();
                AllRawMods = allRawMods;
                HaveRawModules = allRawMods.Length > 0;
                PrefixRawMods = prefixes.Count > 0 ? FrozenStringTreeList.Build(prefixes) : null;
            }
        }

        public bool AddRawModule(IHttpServerRawModule RawModule)
        {
            var m = RawModules;
            if (!m.TryAdd(RawModule, Interlocked.Increment(ref RawModuleOrder)))
                return false;
            OnRawModuleChange();
            return true;
        }

        public bool RemoveRawModule(IHttpServerRawModule RawModule)
        {
            var m = RawModules;
            if (!m.TryRemove(RawModule, out var x))
                return false;
            OnRawModuleChange();
            return true;
        }

        readonly ConcurrentDictionary<IHttpServerRawModule, int> RawModules = new ConcurrentDictionary<IHttpServerRawModule, int>();

        /// <summary>
        /// All RawMods in order that doesn't respond to certain prefixes only
        /// </summary>
        IHttpServerRawModule[] OrderedRawMods;

        /// <summary>
        /// All RawMods in order
        /// </summary>
        IHttpServerRawModule[] AllRawMods;

        /// <summary>
        /// A tree with RawModules that respond to certain prefixes only
        /// </summary>
        FrozenStringTreeList<IHttpServerRawModule> PrefixRawMods;

        bool HaveRawModules;

        #endregion //RawModules


        #region Session

        readonly String SessionCookieName;
        readonly String SessionCookieNameEquals;

        readonly String DeviceIdCookieName;
        readonly String DeviceIdCookieNameEquals;


        readonly long SessionCookieLifetime;
        readonly long SessionExtendLifetime;

        static String GetSessionGuid()
        {
            using (var rng = SecureRng.Get())
                return rng.GetGuid24();
        }


        long TotalSessionCount;
        long CurrentSessionCount;

        long ExpiredCount;
        readonly ConcurrentQueue<HttpSession> ExpiredSessions = new ConcurrentQueue<HttpSession>();

        readonly String CookieOptions;


        readonly FastMemCache<String, String> AcceptLangCache = new FastMemCache<string, string>(TimeSpan.FromHours(24), StringComparer.Ordinal);

        async ValueTask<String> GetAcceptLang(String lang)
        {
            var langSet = TargetLanguagesSet;
            if (langSet == null)
            {
                await GetSupportedLanguages().ConfigureAwait(false);
                langSet = TargetLanguagesSet;
            }
            if (langSet != null)
            {
                try
                {
                    var langs = lang.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var ll = langs.Length;
                    List<String> extra = new(ll);
                    HashSet<String> tested = new(ll, StringComparer.Ordinal);
                    for (int i = 0; i < ll; ++i)
                    {
                        var testCode = langs[i].SplitFirst(';').TrimEnd();
                        if (!tested.Add(testCode))
                            continue;
                        if (langSet.Contains(testCode))
                            return testCode;
                        var bc = testCode.SplitFirst('-');
                        if (bc.FastEquals(testCode))
                            continue;
                        extra.Add(bc);
                    }
                    ll = extra.Count;
                    for (int i = 0; i < ll; ++i)
                    {
                        var testCode = extra[i];
                        if (!tested.Add(testCode))
                            continue;
                        if (langSet.Contains(testCode))
                            return testCode;
                    }
                }
                catch
                {
                }
            }
            return "en";
        }

        protected ValueTask<String> GetAcceptLanguage(String acceptLanguageValue)
            => AcceptLangCache.GetOrUpdateValueAsync(String.IsNullOrEmpty(acceptLanguageValue) ? "en" : acceptLanguageValue, GetAcceptLang);


        unsafe ReadOnlyMemory<Char> ExtractSessionCookie(String cookieString)
        {
            if (cookieString == null)
                return null;
            var sn = SessionCookieNameEquals;
            var sp = cookieString.AsSpan();
            fixed (Char* s = sp)
            {
                var start = s;
                var end = s + sp.Length;
                start = CharPtrTools.IndexOf(sn, start, end);
                if (start == null)
                    return null;
                start += sn.Length;
                var e = CharPtrTools.IndexOf(';', start, end);
                if (e == null)
                    e = end;
                CharPtrTools.Trim(ref start, ref e);
                return cookieString.AsMemory().Slice((int)(start - s), (int)(e - start));
            }
        }

        //readonly ConcurrentDictionary<String, HttpSession> Sessions = new (StringComparer.Ordinal);

        readonly ConcurrentDictionary<IReadOnlyMemoryKey<Char>, HttpSession> Sessions = new(ReadOnlyMemoryKey.HashStringEqualityComparer);


        async ValueTask<HttpSession> GetSession(HttpServerRequest req)
        {
            using (PerfMon.Track(nameof(GetSession)))
            {
                var sn = SessionCookieName;
                if (sn == null)
                    return null;
                var now = DateTime.UtcNow.Ticks;
                var sessions = Sessions;
                var cookieString = req.GetReqHeader("Cookie");
                HttpSession session;
                var sessionTokenMemory = ExtractSessionCookie(cookieString);// req.GetReqCookie(sn, cookieString);
                String sessionToken = null;
                if (!sessionTokenMemory.IsEmpty)
                {
                    if (sessions.TryGetValue(ReadOnlyMemoryKey.Create(sessionTokenMemory), out session))
                    {
                        session.Touch(now, req);
                        return session;
                    }
                    sessionToken = sessionTokenMemory.ToString();
                }

                var ip = req.GetIpAddress();
                var dn = DeviceIdCookieName;
                String deviceId = req.GetReqCookie(dn, cookieString);
                var cookieOpt = CookieOptions;
                if (deviceId == null)
                {
                    Span<Byte> span = stackalloc Byte[16 + 8];
                    using (var rng = SecureRng.Get())
                        rng.GetBytes(span[..16]);
                    BitConverter.TryWriteBytes(span[16..], DateTime.UtcNow.Ticks);
                    deviceId = Convert.ToBase64String(span);
                    req.UpdateCookie(HttpServerTools.MakeCookie(dn, deviceId, DateTime.MaxValue, cookieOpt));
                }

                var ua = req.GetReqHeader("User-Agent") ?? "";
                var prot = req.ProtocolVersion;
                var extLife = SessionExtendLifetime;
                var rateLimiterParams = SessionLimits;
                do
                {
                    sessionToken = GetSessionGuid();
                    session = new HttpSession(rateLimiterParams, sessionToken, now, extLife, ua, ip, prot, deviceId);
                    session.LanguageTimeStamp = DateTime.UtcNow;
                    session.Language = await GetAcceptLanguage(req.GetReqHeader("Accept-Language")).ConfigureAwait(false);
                    session.OnAuthLogout += RunOnLogout;
                } while (!sessions.TryAdd(ReadOnlyMemoryKey.Create(sessionToken), session));
                var exp = new DateTime(now + SessionCookieLifetime, DateTimeKind.Utc);
                req.UpdateCookie(HttpServerTools.MakeCookie(sn, sessionToken, exp, cookieOpt));
                try
                {
                    OnSessionStart?.Invoke(session);
                }
                catch (Exception ex)
                {
                    SessionStartErrors.OnException(ex);
                }
                Interlocked.Increment(ref CurrentSessionCount);
                Interlocked.Increment(ref TotalSessionCount);
                return session;
            }
        }

        public bool CloseSession(HttpSession session)
        {
            if (session == null)
                return false;
            var sessions = Sessions;
            if (!sessions.TryRemove(ReadOnlyMemoryKey.Create(session.Token), out session))
                return false;
            RunOnSessionRemove(session);
            ExpiredSessions.Enqueue(session);
            return true;
        }

        #endregion//Session

        static IHttpServerEndPoint GetEp(HashSet<String> seen, String url, String root)
        {
            if (root != null)
            {
                if (!url.FastStartsWith(root))
                    return null;
                var rl = root.Length;
                if (url.IndexOf('/', rl) >= 0)
                    return null;
            }
            if (!seen.Add(url))
                return null;
            var extP = url.LastIndexOf('.');
            if (extP < 0)
            {
                var ext = url.Substring(extP + 1);
                var mime = MimeTypeMap.GetMimeType(ext);
                return new HttpServerEndPoint(url, "POST", 0, 0, HttpCompressionPriority.DefaultMethods, null, null, HttpServerEndpointTypes.Unknown, "Handled by " + typeof(HttpServerBase).FullName, null, HttpServerTools.StartedTime, HttpServerTools.StartedETag, HttpServerTools.JsonMime, null);
            }
            else
            {
                var ext = url.Substring(extP + 1);
                var mime = MimeTypeMap.GetMimeType(ext);
                return new HttpServerEndPoint(url, "GET", 25, 30, mime.Item2 ? HttpCompressionPriority.DefaultMethods : null, mime.Item2 ? "br" : null, url.IndexOf("_debug") < 0 ? null : DebugAuth, HttpServerEndpointTypes.File, "Generated by " + typeof(HttpServerBase).FullName, null, HttpServerTools.StartedTime, HttpServerTools.StartedETag, mime.Item1, null);
            }
        }

        /// <summary>
        /// Enumerate all enpoints
        /// </summary>
        /// <param name="root">If null all endpoints are returned (recursively)</param>
        /// <returns>End point information</returns>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null)
        {
            using (PerfMon.Track(nameof(EnumEndPoints)))
            {
                if (!String.IsNullOrEmpty(root))
                    root = HttpServerTools.FixEnumRoot(root);
                HashSet<String> seen = new(StringComparer.Ordinal);
                foreach (var x in ForcedEndPoints)
                {
                    var t = GetEp(seen, x.Key, root);
                    if (t != null)
                        yield return t;
                }
                foreach (var x in AllMods)
                {
                    foreach (var y in x.EnumEndPoints(root))
                    {
                        if (seen.Add(y.Uri))
                            yield return y;
                    }
                }
                foreach (var x in OptionalEndPoints)
                {
                    var t = GetEp(seen, x.Key, root);
                    if (t != null)
                        yield return t;
                }
            }
        }


        #region Pause

        public abstract void Pause();
        public abstract void Continue();

        protected volatile bool IsPaused;


        #endregion // Pause

        static String MakeAbs(String relativeOrAbsolute, String prefix)
        {
            if (String.IsNullOrEmpty(relativeOrAbsolute))
                return prefix;
            if (relativeOrAbsolute.IndexOf("://", StringComparison.Ordinal) > 0)
                return relativeOrAbsolute;
            return prefix + relativeOrAbsolute;
        }



        readonly SemiFrozenDictionary<String, HttpServerHostInfo> Hosts = new SemiFrozenDictionary<string, HttpServerHostInfo>(StringComparer.Ordinal);



        HttpServerHostInfo CreateHost(String hostName, String url)
        {
            var hosts = Hosts;
            lock (hosts)
            {
                if (!hosts.TryGetValue(hostName, out var host))
                {
                    var pr = Prefixes;
                    var start = hostName.IndexOf("://") + 3;
                    var end = hostName.IndexOf(':', start);
                    if (end < 0)
                        end = hostName.Length;
                    String wild = hostName.Substring(start, end - start);
                    if (pr.Length == 1)
                    {
                        var prefix = pr[0];
                        var t = prefix.Prefix.Replace("*", wild);
                        host = new HttpServerHostInfo(t, prefix);
                        hosts[hostName] = host;
                        return host;
                    }
                    foreach (var prefix in pr)
                    {
                        var t = prefix.Prefix.Replace("*", wild);
                        if (url.FastStartsWith(t))
                        {
                            host = new HttpServerHostInfo(t, prefix);
                            hosts[hostName] = host;
                            return host;
                        }
                    }
                    throw new Exception("Unknown host name!");
                }
                return host;
            }
        }


        // TODO: Optimize (remove uri?)
        /*protected HttpServerHostInfo GetHost(out String prefix, out String url, Uri uri)
        {
            var hostName = uri.Host.FastToLower();
            var rootUrl = String.Concat(uri.Scheme, "://", hostName, ':', uri.Port, uri.LocalPath);
            url = rootUrl;
            var ul = url.Length;
            if ((ul <= 0) || (url[ul - 1] == '/'))
                url += "index.html";
            if (!Hosts.TryGetValue(hostName, out var host))
                host = CreateHost(hostName);
            prefix = host.Prefixes.StartsWithAny(url);
            url += uri.Query;
            return host;
        }*/

        static readonly SearchValues<Char> HostEnd = SearchValues.Create(['/', '?' ]);
        static readonly TextInfo Ti = CultureInfo.InvariantCulture.TextInfo;

        public unsafe HttpServerHostInfo GetHost(out String prefix, out int queryStart, out bool didIndex, ref String url)
        {
            url = HttpUtility.UrlDecode(url);
            didIndex = false;
            var urlSpan = url.AsSpan();
            var urlLen = urlSpan.Length;
            fixed (Char* urlStart = urlSpan)
            {
                var urlEnd = urlStart + urlLen;
                var pos = urlStart;
                var start = CharPtrTools.IndexOf("://", pos, urlEnd) + 3;
                var end = CharPtrTools.IndexOfAny(HostEnd, start, urlEnd);
                if (end == null)
                    end = urlEnd;
                var hostName = Ti.ToLower(url.Substring(0, (int)(end - urlStart)));
                if (!Hosts.TryGetValue(hostName, out var host))
                    host = CreateHost(hostName, url);
                prefix = host.Name;
                ++end;
                var qs = CharPtrTools.IndexOf('?', end, urlEnd);
                if (qs == null)
                {
                    queryStart = -1;
                    if (urlStart[urlLen - 1] == '/')
                    {
                        didIndex = true;
                        url += "index.html";
                    }
                }
                else
                {
                    queryStart = (int)(qs - urlStart);
                    if (urlStart[queryStart - 1] == '/')
                    {
                        url = urlSpan[..queryStart].ConcatToString("index.html", urlSpan[queryStart..]);
                        didIndex = true;
                        queryStart += 10;
                    }
                }
                return host;
            }
        }


        /*
        protected HttpServerHostInfo GetHost(out String prefix, out int queryStart, ref String url)
        {
            var s = url.AsSpan();
            var start = s.IndexOf("://", StringComparison.Ordinal) + 3;
            var end = s.Slice(start).IndexOfAny(HostEnd);
            if (end < 0)
                end = url.Length;
            else
                end += start;
            
            var hostName = url.Substring(start, end - start).FastToLower();
            if (!Hosts.TryGetValue(hostName, out var host))
                host = CreateHost(hostName);
            prefix = host.Prefix ?? host.Prefixes.StartsWithAny(url);
            ++end;
            queryStart = s.Slice(end).IndexOf('?');
            if (queryStart < 0)
            {
                var ul = url.Length;
                if (url[ul - 1] == '/')
                    url += "index.html";
            }else
            {
                queryStart += end;
                if (url[queryStart - 1] == '/')
                {
                    url = String.Concat(url.Substring(0, queryStart), "index.html", url.Substring(queryStart));
                    queryStart += 10;
                }
            }
            return host;
        }
        */

        #region Error tracking




        public virtual IEnumerable<Stats> GetStats()
        {
            const String sys = "HttpServer";
            foreach (var e in ListenerExceptions.GetStats(sys, "Listener."))
                yield return e;
            foreach (var e in HandlerExceptions.GetStats(sys, "Handler."))
                yield return e;
            foreach (var e in RequestExceptions.GetStats(sys, "Request."))
                yield return e;
            foreach (var e in SessionStartErrors.GetStats(sys, "OnSessionStart."))
                yield return e;
            foreach (var e in LoginErrors.GetStats(sys, "OnLogin."))
                yield return e;
            foreach (var e in LogoutErrors.GetStats(sys, "OnLogout."))
                yield return e;
            foreach (var e in TransformExceptions.GetStats(sys, "Transform."))
                yield return e;
            var total = Interlocked.Read(ref CacheTotal);
            var hit = Interlocked.Read(ref CacheHit);
            var ratio = (double)((Decimal)hit * 100M / (Decimal)(total <= 0 ? 1 : total));
            yield return new Stats(sys, "Cache.Ratio", ratio, "Cache hit ratio, how many time a cached resource was returned", TableDataNumberAttribute.Percentage);
            yield return new Stats(sys, "Cache.Request", total, "Total number of request that could have hit the cache");
            yield return new Stats(sys, "Session.Current", Interlocked.Read(ref CurrentSessionCount), "Total number of active sessions");
            yield return new Stats(sys, "Session.Total", Interlocked.Read(ref TotalSessionCount), "Total number of sessions created during the life time of the process");
            yield return new Stats(sys, "Session.Expired", Interlocked.Read(ref ExpiredCount), "Number of expired sessions that are kept for 5 minutes extra for debug / tracking purposes");
        }


        readonly Type ListenerExceptionType;
        protected readonly ExceptionTracker ListenerExceptions = new ExceptionTracker();
        protected readonly ExceptionTracker HandlerExceptions = new ExceptionTracker();
        protected readonly ExceptionTracker RequestExceptions = new ExceptionTracker();
        protected readonly ExceptionTracker SessionCloseExceptions = new ExceptionTracker();

        #endregion//Error tracking

        static readonly Byte[] Dummy = GC.AllocateUninitializedArray<Byte>(16384);

        #region Compression


        static bool GZipToDeflate(HttpRequestData i)
        {
            var s = i.Stream;
            if (s != null)
            {
                if (!s.CanSeek)
                    return false;
                i.ChangeStream(new TransformGZipToDeflateStream(s, true));
                return true;
            }
            i.ChangeMem(TransformGZipToDeflateStream.GetDeflateData(i.Mem));
            return true;
        }

        async ValueTask<String> Compress(ICompEncoder encoder, CompEncoderLevels level, HttpRequestData i)
        {
            var s = i.Stream;
            if (s != null)
            {
                i.ChangeMem(await encoder.GetCompressedAsync(s, level, true).ConfigureAwait(false));
                return encoder.HttpCode;
            }
            var b = i.Mem;
            var compData = encoder.GetCompressed(b.Span, level, true);
            var size = compData.Length;
            if ((size > 0) && (size < b.Length))
            {
                i.ChangeMem(compData);
                return encoder.HttpCode;
            }
            return null;
        }


        ValueTask Compress(ICompEncoder encoder, CompEncoderLevels level, HttpRequestData i, Stream output)
        {
            var s = i.Stream;
            return s == null ? encoder.CompressAsync(i.Mem, output, level) : encoder.CompressAsync(s, output, level);
        }


        async ValueTask Decompress(ICompDecoder decoder, HttpRequestData i)
        {
            var s = i.Stream;
            i.ChangeMem(s == null ? decoder.GetDecompressed(i.Mem.Span) : await decoder.GetDecompressedAsync(s).ConfigureAwait(false));
        }

        ValueTask Decompress(ICompDecoder decoder, HttpRequestData i, Stream output)
        {
            var s = i.Stream;
            return s == null ? decoder.DecompressAsync(i.Mem, output) : decoder.DecompressAsync(s, output);
        }

        #endregion//Compression

        #region Cache


        /// <summary>
        /// TODO: Prune cache (remove expired and unused entriesd)
        /// </summary>
        readonly ConcurrentDictionary<String, HttpCacheEntry> Cache = new(StringComparer.Ordinal);

        static ReadOnlyMemory<Byte> CloneMemory(ReadOnlyMemory<Byte> mem)
        {
            var t = GC.AllocateUninitializedArray<Byte>(mem.Length);
            mem.Span.CopyTo(t);
            return t;
        }

        async ValueTask SaveToCache(ConcurrentDictionary<String, HttpCacheEntry> cache, String cacheKey, DateTime expires, long nowT, HttpRequestData i, HttpServerRequest req, String localUrl)
        {
            using (PerfMon.Track(nameof(SaveToCache)))
            {
                var exp = expires.Ticks;
                var s = i.Stream;
                if (s != null)
                {
                    var nd = await s.ReadAllMemoryAsync().ConfigureAwait(false);
                    i.ChangeMem(nd);
                    cache[cacheKey] = new HttpCacheEntry(nowT, exp, req, nd, localUrl);
                    return;
                }
                var mem = i.Mem;
                if (!i.IsMapped)
                {
                    cache[cacheKey] = new HttpCacheEntry(nowT, exp, req, i.Mem, localUrl);
                    return;
                }
                //  Really save?
                //var t = CloneMemory(mem);
                //i.ChangeMem(t);
                //cache[cacheKey] = new HttpCacheEntry(nowT, exp, req, t, localUrl);
            }
        }

        /// <summary>
        /// Invalidate the cache, this does not invalidate per session cache entries
        /// </summary>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Ops)]
        public void InvalidateCache()
        {
            Cache.Clear();
        }

        /// <summary>
        /// Invalidate the cache if the predicate returns true, this does not invalidate per session cache entries
        /// </summary>
        /// <param name="shouldInvalidate">A function to determine if the entry shgould be cleared, the string is the local url</param>
        public void InvalidateCache(Func<String, bool> shouldInvalidate)
        {
            var c = Cache;
            List<String> l = [];
            foreach (var x in c)
            {
                if (!shouldInvalidate(x.Value.LocalUrl))
                    continue;
                l.Add(x.Key);
            }
            foreach (var x in l)
                c.TryRemove(x, out var _);
        }

        /// <summary>
        /// Invalidate all session caches, this does not invalidate the "global" cache
        /// </summary>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Ops)]
        public void InvalidateAllSessionCaches()
        {
            foreach (var s in Sessions)
                s.Value.InvalidateCache();
        }

        /// <summary>
        /// Invalidate all session caches if the predicate returns true, this does not invalidate the "global" cache
        /// </summary>
        /// <param name="shouldInvalidate">A function to determine if the entry shgould be cleared, the string is the local url</param>
        public void InvalidateAllSessionCaches(Func<String, bool> shouldInvalidate)
        {
            foreach (var s in Sessions)
                s.Value.InvalidateCache(shouldInvalidate);
        }

        #endregion//Cache

        /// <summary>
        /// Get messages, block until new message is sent, only one request per session is supported (only one will get messages).
        /// </summary>
        /// <param name="events">The events types to fetch and the change counter</param>
        /// <param name="request"></param>
        /// <returns>List of messages and the new change counter</returns>
        [WebApi("application/{0}")]
        public async Task<MessageStreamResponse> GetMessages(MessageStreamRequest events, HttpServerRequest request)
        {
            if (StopMessages)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return null;
            }
            return await request.Session.GetMessages(events).ConfigureAwait(false);
        }



        #region Language


        /// <summary>
        /// Get a list of the supported languages
        /// </summary>
        /// <returns>The list of supported languages.
        /// null = No language support</returns>
        [WebApi("application/{0}")]
        [WebApiClientCache(1)]
        [WebApiRequestCacheStatic]
        public async Task<String[]> GetSupportedLanguages()
        {
            var t = Translator;
            var l = TargetLanguages;
            if (l == null) 
            {
                if (t != null)
                    l = (await t.GetSupportedTargetLanguages().ConfigureAwait(false))?.ToArray() ?? Array.Empty<String>();
                else
                    l = IsoLanguage.Common;
                TargetLanguages = l;
                TargetLanguagesSet = ReadOnlyData.Set(l);
            }
            if (l == null)
                return null;
            return l.Length <= 0 ? null : l;
        }

        readonly FastMemCache<String, LanguageInfo[]> LocalizedLanguagesCache = new FastMemCache<string, LanguageInfo[]>(TimeSpan.FromDays(1));

        /// <summary>
        /// Get a list of the supported languages, with localized meta information.
        /// </summary>
        /// <returns>The list of supported languages.
        /// null = No language support</returns>
        [WebApi("application/{0}")]
        [WebApiClientCache(1)]
        [WebApiRequestCacheStatic]
        public Task<LanguageInfo[]> GetLocalizedLanguages(HttpServerRequest context)
            => LocalizedLanguagesCache.GetOrUpdateAsync(context.Session.Language, async cl =>
                {
                    var langs = await GetSupportedLanguages().ConfigureAwait(false);
                    if (langs == null)
                        return null;
                    var l = langs.Length;
                    var d = new LanguageInfo[l];
                    if (l <= 0)
                        return d;
                    cl = context.Session.Language;
                    Task<LanguageInfo>[] tasks = new Task<LanguageInfo>[l];
                    for (int i = 0; i < l; ++i)
                        tasks[i] = GetLocalizedLanguage(langs[i], cl);
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    for (int i = 0; i < l; ++i)
                        d[i] = tasks[i].Result;
                    return d;
                });

        readonly FastMemCache<String, LanguageInfo> LocalizedLangCache = new FastMemCache<string, LanguageInfo>(TimeSpan.FromDays(1));

        /// <summary>
        /// Get localized language information
        /// </summary>
        /// <returns>The list of supported languages.
        /// null = No language support</returns>
        public Task<LanguageInfo> GetLocalizedLanguage(String info, String toLanguage)
            => LocalizedLangCache.GetOrUpdateAsync(String.Concat(info, '_', toLanguage), cl =>
                OneLang(Translator, info, toLanguage)
            );


        static async Task<LanguageInfo> OneLang(ITranslator tr, String iso, String currentLang)
        {
            var li = IsoLanguage.TryGetName(iso);
            if (li == null)
                return new LanguageInfo(iso, iso, iso, iso, null);
            var name = li.Name.SplitFirst(',').TrimEnd();
            var com = li.Comment;
            if (tr == null)
                return new LanguageInfo(iso, name, name, name, com);
            var context =
                String.IsNullOrEmpty(com)
                ?
                String.Concat("This is the english name of the language \"", name, "\" with the ISO code \"", iso, '"')
                :
                String.Concat("This is the english name of the language \"", name, "\" with the ISO code \"", iso, "\" and comment: ", com)
                ;
            var n = await tr.TranslateOne(new TranslateRequest
            {
                From = "en",
                To = currentLang,
                Text = name,
                Context = context,
                Effort = TranslationEffort.High,
                Retention = TranslationCacheRetention.Long
            }).ConfigureAwait(false);
            var ln = await tr.TranslateOne(new TranslateRequest
            {
                From = "en",
                To = iso,
                Text = name,
                Context = context,
                Effort = TranslationEffort.High,
                Retention = TranslationCacheRetention.Long
            }).ConfigureAwait(false);
            if (!String.IsNullOrEmpty(com))
            {
                com = await tr.TranslateOne(new TranslateRequest
                {
                    From = "en",
                    To = currentLang,
                    Text = com,
                    Context = String.Concat("This are the comments (remarks) for the language \"", name, "\" with the ISO code \"", iso, '"'),
                    Effort = TranslationEffort.High,
                    Retention = TranslationCacheRetention.Long
                }).ConfigureAwait(false);
            }
            return new LanguageInfo(iso, name, n, ln, com);
        }


        /// <summary>
        /// Change the preferred language for the currently logged in user.
        /// If successful a page reload request is sent to all sessions for that user (maybe don't have time to get the result or act on it)
        /// </summary>
        /// <param name="languageCode">The new language code.
        /// A two letter letter ISO 639-1 code with an optional hypen followed by an ISO 3166 Alpha 2 country code.
        /// Examples:
        /// "fr", "en-US", "en-GB", "es-ES", "es-MX"
        /// </param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi("application/{0}")]
        public async Task<bool> SetLanguage(String languageCode, HttpServerRequest context)
        {
            await GetSupportedLanguages().ConfigureAwait(false);
            if (!TargetLanguagesSet.Contains(languageCode))
                throw new Exception("Language " + languageCode + " is not supported!");
            var s = context.Session;
            s.Language = languageCode;
            s.LanguageTimeStamp = DateTime.UtcNow;
            s.InvalidateCache();
            var auth = s.Auth;
            if (auth == null)
            {
                s.PushMessage(MessageRefresh);
                return true;
            }
            if (await auth.Auth.SetLanguage(auth.Guid, languageCode).ConfigureAwait(false))
                PushMessageUser(s, MessageRefresh);
            else
                s.PushMessage(MessageRefresh);
            return true;
        }

        /// <summary>
        /// Get the current language of the session
        /// </summary>
        /// <returns></returns>
        [WebApiClientCache(1)]
        [WebApi("application/{0}")]
        public String GetLanguage(HttpServerRequest context)
            => context.Session.Language;


        /// <summary>
        /// null = No lang support
        /// </summary>
        String[] TargetLanguages;
        IReadOnlySet<String> TargetLanguagesSet;



        /// <summary>
        /// Check if a language is supported
        /// </summary>
        /// <param name="lang">The ISO 639-1 language code of the language with an optional two letter ISO 3166-A2 country code separated by a hyphen ('-')</param>
        /// <returns>True if language is supported</returns>
        public async Task<bool> IsSupportedLanguage(String lang)
        {
            await GetSupportedLanguages().ConfigureAwait(false);
            var s = TargetLanguagesSet;
            if (s == null)
                return false;
            return s.Contains(lang);
        }


        #endregion//Language


        bool StopMessages;

        protected virtual void OnDispose()
        {
        }

        public void Dispose()
        {
            StopMessages = true;
            Thread.Sleep(20);
            PushMessageAllSessions(MessageServerShutDown);
            OnDispose();
            PruneTask.Dispose();
            var firewallRules = FirewallRules;
            if (firewallRules != null)
            {
                var msg = Msg;
                msg?.AddMessage(Prefix + "Deleting firewall rules");
                using (msg?.Tab())
                {
                    var firewallHandler = FirewallHandler;
                    while (firewallRules.TryPop(out var rule))
                        firewallHandler.Remove(rule, msg, Prefix);
                }
            }
            Interlocked.Exchange(ref SvgFaviconBitmapRenderer, null)?.Dispose();
            DataRefsGlobal.Dispose();
            DataRefsAnyUser.Dispose();
        }


        protected void InvokeCancel() => OnCancel?.Invoke();


        /// <summary>
        /// Called when incoming request should exit as quick as possible
        /// </summary>
        public event Action OnCancel;

        #region Debug


        public const String MenuPath = "Debug/Http Server/{0}";


        /// <summary>
        /// Send a message to the current session, just use for testing.
        /// If the server is compiled using debug, "Test", "TestA", "TestB", "TestC" can be used and will be sent to clients.
        /// </summary>
        /// <param name="message">The message text to send (no data is possible)</param>
        /// <param name="request"></param>
        [WebApi("application/debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        public void SendSessionMessage(String message, HttpServerRequest request) => request.Session.PushMessage(new PushMessage(message));

        /// <summary>
        /// Send a message to the current user (all sessions where the current user is logged in), just use for testing
        /// If the server is compiled using debug, "Test", "TestA", "TestB", "TestC" can be used and will be sent to clients.
        /// </summary>
        /// <param name="message">The message text to send (no data is possible)</param>
        /// <param name="request"></param>
        [WebApi("application/debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        public void SendUserMessage(String message, HttpServerRequest request) => request.Server.PushMessageUser(request.Session, new PushMessage(message));

        /// <summary>
        /// Send a message to all session with a logged in user, just use for testing
        /// If the server is compiled using debug, "Test", "TestA", "TestB", "TestC" can be used and will be sent to clients.
        /// </summary>
        /// <param name="message">The message text to send (no data is possible)</param>
        /// <param name="request"></param>
        [WebApi("application/debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        public void SendAllUsersMessage(String message, HttpServerRequest request) => request.Server.PushMessageAllUsers(new PushMessage(message));

        /// <summary>
        /// Send a message to all sessions, just use for testing
        /// If the server is compiled using debug, "Test", "TestA", "TestB", "TestC" can be used and will be sent to clients.
        /// </summary>
        /// <param name="message">The message text to send (no data is possible)</param>
        /// <param name="request"></param>
        [WebApi("application/debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        public void SendAllSessionsMessage(String message, HttpServerRequest request) => request.Server.PushMessageAllSessions(new PushMessage(message));

        /// <summary>
        /// Get all active sessions
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuPath, null, null, "IconTableSession")]
        public TableData ActiveSessions(TableDataRequest r)
        {
            var n = DateTime.UtcNow;
            HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
            var data = TableDataTools.Get(r, 5000, Sessions.Values.Where(x => seen.Add(x.Token)).Select(x => new SessionDebugData(x, n)).Concat(ExpiredSessions.Select(x => new SessionDebugData(x, n))));
            var rows = data.Rows;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var d = row.Values[0] as String;
                    if ((d != null) && (d.Length > 6))
                        d = d.Substring(0, 6) + "****";
                    else
                        d = "****";
                    row.Values[0] = d;
                }
            }
            return data;
        }


        /// <summary>
        /// Show a table with active users and their stats
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuPath, null, null, "IconTableUsers")]
        public TableData ActiveUsers(TableDataRequest r)
        {
            var nt = DateTime.UtcNow.Ticks;
            return TableDataTools.Get(r, 5000, UserSessions.Values.Select(x => new UserDebugData(x, nt)));

        }

        /// <summary>
        /// Get cache entries
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuPath, null, null, "IconTableCache")]
        public TableData CacheEntries(TableDataRequest r)
        {
            var n = DateTime.UtcNow;
            return TableDataTools.Get(r, 5000, Cache.Select(x => new CacheData(x, n)));
        }

        /// <summary>
        /// Get cache entries for just this session
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Ops)]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, MenuPath, null, null, "IconTableCache")]
        public TableData SessionCacheEntries(TableDataRequest r, HttpServerRequest context)
        {
            var n = DateTime.UtcNow;
            return TableDataTools.Get(r, 5000, context.Session.Cache.Nullable().Select(x => new CacheData(x, n)));
        }


        /// <summary>
        /// The mime mappings (extension to mime type) used by the web server
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/data/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Mime mapping", null, "icons/file_types.svg", -2)]
        public TypedTableData<MimeTypeMap.ExtensionEntry> MimeMappingTable(TableDataRequest r)
            => TableDataTools.GetTyped(r, 30000, MimeTypeMap.AllExtensionEntries);

        /// <summary>
        /// All mime types that the web server recognizes
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/data/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Mime types", null, "icons/world.svg", -1)]
        public TypedTableData<MimeTypeMap.MimeEntry> MimeTypesTable(TableDataRequest r)
            => TableDataTools.GetTyped(r, 30000, MimeTypeMap.AllMimeEntries);


        #endregion //Debug



    }

}
