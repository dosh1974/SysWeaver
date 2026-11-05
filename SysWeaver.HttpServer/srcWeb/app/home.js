/**
 * @file Script of the Home page (app/Home.html), the first main-menu item for signed in users: the Design v2 dashboard
 * (Design v2, PORTING.md B11). Loaded with a static `<script defer>` tag after ../sw/core.js and ../sw/ui.js; started by
 * `<body onload="homeMain()">`. Home.html is a server template (application name and description, session nick name, start time,
 * version) and holds the page header: the user image (an upload target when the custom user image service exists), the application name as
 * the title and the greeting + description as the subtitle (decision 43 / 9.3). home.js adds:
 *  - the overall status badge in the header (connected / not connected, the server time; problems when known),
 *  - the KPI tiles (uptime, version, managed services and problems when the user may see them),
 *  - the server facts strip on a server with the server manager, for every user who may call its APIs (Sw.Access.CanCall) (CPUs, memory, processes, drives; each opens its page),
 *  - inside the shell: the shortcut tiles (top level menu pages), the "Recent pages" and "Most used" cards (Sw.Shell.App.GetQuickLinks and
 *    localStorage "SysWeaver.RecentPages") and the "Problems" card (the latest failures of the exception trackers, Api/debug/ProblemsTable; the log on older servers), with empty states,
 *  - the getting started hint.
 *
 * Globals defined: `homeMain`, `homeUploaded` and the page helpers home* (ex: `homeKpis`, `homeQuickLinks`, `homeProblems`).
 * Dependencies: Sw core / ui (`Sw.Page`, `Sw.Net`, `Sw.Url`, `Sw.InterOp`, `Sw.Info`, `Sw.Fail`, `Sw.SetImageSource`, `Sw.PageHeader` markup,
 * `Sw.StateView`, `Sw.Badge`); loaded on demand: common/fileUploader.js (`Sw.Upload`, SysWeaver.MicroService.FileUploader).
 * Convention: the user image only becomes an upload target when the server hosts
 * `SysWeaver.MicroService.CustomUserImageService`; a changed image is broadcast as the InterOp message
 * `UserImageChanged` (the shell refreshes its user button).
 */
"use strict";

/**
 * Report the result of a user image upload (only the first file is reported): toasts, switch the image to the large
 * variant (or reload it) and broadcast `UserImageChanged`.
 * @param {HTMLImageElement} img - The user image element.
 * @param {{Status:number[], Error:Error}} res - Result of Sw.Upload.Files.
 * @param {File[]|FileList} files - The uploaded files.
 * @returns {void}
 */
function homeUploaded(img, res, files) {
    if (res.Error) {
        Sw.Fail(res.Error.message || String(res.Error));
        return;
    }
    const name = files[0].name;
    const status = res.Status[0];
    const S = Sw.Upload.Status;
    switch (status) {
        case S.AlreadyUploaded:
            Sw.Info(_T("User image \"{0}\" was already uploaded", name, "Text displayed when the user image that was uploaded to a server is the same as the current user image.{0} is replaced with the name of the user image file"));
            Sw.InterOp.Post("UserImageChanged");
            break;
        case S.None:
            Sw.Info(_T("Uploaded user image \"{0}\"", name, "Text displayed when a user image file was successfully uploaded to a server.{0} is replaced with the name of the user image file"));
            {
                const src = img.src;
                if (src.endsWith("/small"))
                    img.src = src.substring(0, src.length - 5) + "large";
                else
                    Sw.SetImageSource(img, src, { FlushCache: true });
            }
            Sw.InterOp.Post("UserImageChanged");
            break;
        default:
            Sw.Fail(_T("{0}, when uploading user image \"{1}\"", Sw.Upload.StatusText(status), name, "Text displayed when uploading of a user image file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the user image file"));
            break;
    }
}

/**
 * The application shell that shows this page (same origin top window with Sw.Shell.App.GetQuickLinks), else null
 * (stand alone, or an old shell).
 * @returns {object|null} The shell (Sw.Shell.App of the top window).
 */
function homeShell() {
    try {
        const app = window.top !== window.self ? window.top.SysWeaverApp : null;
        return (app && typeof app.GetQuickLinks === "function") ? app : null;
    }
    catch {
        return null;
    }
}

/**
 * Test if keyboard short cut hints are worth showing (the shell's Sw.Shell.HasKeyboard: not on phones / touch only devices).
 * @returns {boolean} True on devices with a keyboard.
 */
function homeHasKeyboard() {
    try {
        const f = window.top.Sw && window.top.Sw.Shell && window.top.Sw.Shell.HasKeyboard;
        if (typeof f === "function")
            return f();
    }
    catch {
    }
    if (Sw.Layout.Mobile)
        return false;
    return !Sw.Platform.IsTouch || window.matchMedia("(any-pointer: fine)").matches;
}

/**
 * The getting started hint (inside the shell): keyboard devices also get the menu search hot key (Alt+M), phones are told where
 * the menu is.
 * @param {boolean} haveLinks True when quick links are shown above the hint.
 * @returns {string} The hint text.
 */
function homeHint(haveLinks) {
    if (Sw.Layout.Mobile)
        return haveLinks
            ? _TF("Choose a page above, or open the menu with the button at the top left.", "Hint on the home page of the application on phones and tablets, below a list of quick links")
            : _TF("Choose a page in the menu to get started.", "Hint on the home page of the application on phones and tablets");
    if (homeHasKeyboard())
        return haveLinks
            ? _TF("Choose a page above or in the menu. To find a page, type its name in the box above the menu (or press Alt+M).", "Hint on the home page of the application below a list of quick links, Alt+M is a keyboard short cut that moves the focus to the menu search box")
            : _TF("Choose a page in the menu to get started. To find a page, type its name in the box above the menu (or press Alt+M).", "Hint on the home page of the application, Alt+M is a keyboard short cut that moves the focus to the menu search box");
    return haveLinks
        ? _TF("Choose a page above or in the menu. To find a page, type its name in the box above the menu.", "Hint on the home page of the application on touch devices, below a list of quick links")
        : _TF("Choose a page in the menu to get started. To find a page, type its name in the box above the menu.", "Hint on the home page of the application on touch devices");
}

/**
 * The current server time (synced by Sw.Session), else the local time.
 * @returns {Date} Now.
 */
function homeNow() {
    return (Sw.Session && Sw.Session.TimeOffset !== undefined) ? Sw.Session.ServerTime() : new Date();
}

/**
 * A short relative time ("2 minutes ago", "yesterday") in the user's language (Intl.RelativeTimeFormat).
 * @param {number} ms - The time (ms since 1970).
 * @returns {string} The text ("" for an unknown time).
 */
function homeAgo(ms) {
    if (!(ms > 0))
        return "";
    const s = Math.round((ms - Date.now()) / 1000);
    let rtf;
    try {
        rtf = new Intl.RelativeTimeFormat(Sw.I18n && typeof Sw.I18n.Language === "function" ? Sw.I18n.Language() : undefined, { numeric: "auto" });
    }
    catch {
        rtf = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });
    }
    const a = Math.abs(s);
    if (a < 45)
        return rtf.format(0, "second");
    if (a < 60)
        return rtf.format(s, "second");
    if (a < 3600)
        return rtf.format(Math.round(s / 60), "minute");
    if (a < 86400)
        return rtf.format(Math.round(s / 3600), "hour");
    return rtf.format(Math.round(s / 86400), "day");
}

/**
 * Parse a template time stamp (UTC "yyyy-MM-dd HH:mm:ss", ex: main.Home[data-appstart] = EnvInfo.AppStart).
 * @param {string} s - The text.
 * @returns {Date|null} The time, null when unknown (not templated, ex: ?raw).
 */
function homeParseUtc(s) {
    s = String(s || "");
    if (!/^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d$/.test(s))
        return null;
    const d = new Date(s.replace(" ", "T") + "Z");
    return isNaN(d.getTime()) ? null : d;
}

/**
 * Server start time from the template attribute main.Home[data-appstart] (EnvInfo.AppStart: UTC "yyyy-MM-dd HH:mm:ss").
 * @param {HTMLElement} main The page (main.Home).
 * @returns {Date|null} The start time, null when unknown (not templated, ex: ?raw).
 */
function homeAppStart(main) {
    return homeParseUtc(main.dataset.appstart);
}

/**
 * The greeting of the subtitle by the time of the day ("Good morning, debug"); the server text of the template (the welcome sentence) stays
 * when the nick name is unknown.
 * @param {HTMLElement} main The page (main.Home, data-nick = Session.NickName).
 */
function homeGreeting(main) {
    const g = main.querySelector(".HomeGreeting");
    const nick = String(main.dataset.nick || "").trim();
    if (!g || !nick || nick.indexOf("${") >= 0)
        return;
    const h = new Date().getHours();
    g.textContent = h < 5 || h >= 18
        ? _T("Good evening, {0}.", nick, "Greeting on the home page in the evening (18:00 - 05:00).{0} is replaced with the name of the signed in user")
        : (h < 12
            ? _T("Good morning, {0}.", nick, "Greeting on the home page in the morning (05:00 - 12:00).{0} is replaced with the name of the signed in user")
            : _T("Good afternoon, {0}.", nick, "Greeting on the home page in the afternoon (12:00 - 18:00).{0} is replaced with the name of the signed in user"));
}

/**
 * Create a KPI tile (div.sw-card.sw-kpi, or a button when it opens a page): an icon and a label, the value (Saira) with an optional unit and a sub line.
 * @param {object} o - Options.
 * @param {string} o.Icon - Sw.Icon name (inline line icon).
 * @param {string} o.Label - What the value is.
 * @param {string} [o.Title] - Tool tip (details).
 * @param {string|function():string} [o.Url] - Page to open on a click (inside the shell); a function when it is known later (a reserved tile).
 * @param {boolean} [o.Loading] - A reserved tile (homePlan): the value is a skeleton until the data arrives (class HomeLoading).
 * @returns {{E: HTMLElement, Value: HTMLElement, Unit: HTMLElement, Sub: HTMLElement}} The tile and its parts (for updates).
 */
function homeKpi(o) {
    const value = Sw.el("span.HomeKpiValue");
    const unit = Sw.el("small");
    const sub = Sw.el("div.sw-kpi-sub");
    const parts = [
        Sw.el("div.sw-kpi-label", {}, Sw.Icon(o.Icon), Sw.el("span", { text: o.Label })),
        Sw.el("div.sw-kpi-value", {}, value, unit),
        sub,
    ];
    let e;
    if (o.Url) {
        e = Sw.el("button.sw-card.sw-kpi", { type: "button", title: o.Title || o.Label }, ...parts);
        e.addEventListener("click", ev => {
            if (Sw.BadClick(ev))
                return;
            Sw.Page.Navigate(typeof o.Url === "function" ? o.Url() : o.Url, "_self");
        });
    }
    else
        e = Sw.el("div.sw-card.sw-kpi", { title: o.Title || o.Label }, ...parts);
    if (o.Loading) {
        e.classList.add("HomeLoading");
        e.setAttribute("aria-busy", "true");
    }
    if (Sw.Hint)
        Sw.Hint.Attach(e);
    return {
        E: e, Value: value, Unit: unit, Sub: sub,
        /** The data arrived: the skeleton goes. */
        Loaded() {
            e.classList.remove("HomeLoading");
            e.removeAttribute("aria-busy");
        },
    };
}

/**
 * The layout of the parts that load in the background (UX guide LAY-1: reserve the space, CLS 0 with slow servers): which of the
 * managed services tile (S), the server facts strip (F, N facts), the problems tile (P) and the problems card (C) this user saw last
 * time (localStorage "SysWeaver.HomeLayout.<nick>"), else, inside the shell, what the main menu suggests (server manager pages: S and
 * F; the Problems page: P and C).
 * @param {HTMLElement} main The page (main.Home).
 * @param {object|null} shell The shell (homeShell).
 * @returns {{Key: string, S: boolean, F: boolean, N: number, P: boolean, C: boolean}} The plan.
 */
function homePlan(main, shell) {
    const key = "SysWeaver.HomeLayout." + String(main.dataset.nick || "").replace(/[^\w@.\-]/g, "_").substring(0, 64);
    const p = Sw.Storage.Local.GetJson(key, null);
    if (p && typeof p === "object")
        return { Key: key, S: !!p.S, F: !!p.F, N: p.N > 0 ? Math.min(6, p.N | 0) : 5, P: !!p.P, C: !!p.C && !!shell };
    const plan = { Key: key, S: false, F: false, N: 5, P: false, C: false };
    try {
        const urls = shell && shell.Menu && Array.isArray(shell.Menu.Entries) ? shell.Menu.Entries.map(e => String(e.Url || (e.Item && e.Item.Data) || "")) : [];
        plan.S = plan.F = urls.some(u => /(^|[\/=])ServerManager\//i.test(u));
        plan.P = plan.C = urls.some(u => /(^|[\/=])Api\/debug\/ProblemsTable([?&#]|$)/i.test(u));
    }
    catch {
    }
    return plan;
}

/**
 * A reserved server facts strip (homePlan): the labels with skeleton values, filled by homeServer in place.
 * @param {number} n The number of facts last time.
 * @returns {HTMLElement} The strip (dl.sw-facts.HomeFacts.HomeLoading).
 */
function homeFactsSkeleton(n) {
    const dl = Sw.el("dl.sw-card.sw-facts.HomeFacts.HomeLoading", { attrs: { "aria-label": _TF("Server", "Accessible name of the server facts on the home page"), "aria-busy": "true" } });
    for (let i = 0; i < n; ++i)
        dl.append(Sw.el("div.sw-fact", {}, Sw.el("dt", {}, Sw.el("span.HomeSkel")), Sw.el("dd", {}, Sw.el("span.HomeSkel.HomeSkelValue"))));
    return dl;
}

/**
 * A reserved problems card (homePlan): the title and three skeleton rows, replaced by the real card (homeProblems).
 * @returns {HTMLElement} The card.
 */
function homeProblemsSkeleton() {
    const ul = Sw.el("ul.sw-rows.HomeSkelRows", { attrs: { "aria-hidden": "true" } });
    for (let i = 0; i < 3; ++i)
        ul.append(Sw.el("li", {}, Sw.el("span.HomeSkel.HomeSkelBadge"), Sw.el("span.sw-rows-text", {}, Sw.el("span.HomeSkel"))));
    return Sw.el("section.sw-card.HomeCard.HomeProblems.HomeLoading", { attrs: { "aria-busy": "true" } },
        Sw.el("div.sw-card-head", {}, Sw.el("h2", { text: _TF("Problems", "Title of the card with the latest logged warnings and errors on the home page") })), ul);
}

/**
 * The uptime tile from the template start time (whole minutes, the two largest units: "4 h 59 min", "2 d 3 h"; updated twice a minute).
 * @param {HTMLElement} list The KPI row.
 * @param {Date} start The server start time.
 */
function homeUptime(list, start) {
    const k = homeKpi({
        Icon: "clock",
        Label: _TF("Service uptime", "Label of the tile with the time the service has been running, on the home page"),
        Title: _T("The service started {0}", start.toLocaleString([], { dateStyle: "medium", timeStyle: "short" }), "Tool tip of the service uptime on the home page.{0} is replaced with the date and time the service started"),
    });
    const sameDay = start.toDateString() === new Date().toDateString();
    k.Sub.textContent = sameDay
        ? _T("Since {0} today", start.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }), "Sub line of the server uptime tile on the home page.{0} is replaced with the time the server started (hours and minutes)")
        : _T("Since {0}", start.toLocaleString([], { dateStyle: "medium", timeStyle: "short" }), "Sub line of the server uptime tile on the home page.{0} is replaced with the date and time the server started");
    const update = () => {
        const min = Math.floor(Math.max(0, homeNow().getTime() - start.getTime()) / 60000);
        const d = Math.floor(min / 1440);
        const h = Math.floor(min / 60) % 24;
        const m = min % 60;
        k.Value.textContent = d > 0
            ? _T("{0} d {1} h", d, h, "Server uptime on the home page in days and hours.{0} is replaced with the number of days, {1} with the hours")
            : (h > 0
                ? _T("{0} h {1} min", h, m, "Server uptime on the home page in hours and minutes.{0} is replaced with the number of hours, {1} with the minutes")
                : _T("{0} min", m, "Server uptime on the home page in minutes.{0} is replaced with the number of minutes"));
    };
    update();
    setInterval(update, 30000);
    list.append(k.E);
}

/**
 * The application version tile from the template attributes main.Home[data-appversion] (EnvInfo.AppVersion: AppInfoParams.AppVersion
 * from the *.Services.json, else the version of the executable) and [data-appbuild] (EnvInfo.AppBuildTime: UTC "yyyy-MM-dd HH:mm:ss").
 * The tile shows the version without a "+<source revision>" suffix, the build date below; the tool tip has the full version and the build time.
 * Nothing is added when the page was not templated (ex: ?raw).
 * @param {HTMLElement} main The page (main.Home).
 * @param {HTMLElement} list The KPI row.
 */
function homeVersion(main, list) {
    const ds = main.dataset;
    const v = String(ds.appversion || "").trim();
    if (!v || v.indexOf("${") >= 0)
        return;
    const short = v.split("+")[0] || v;
    let tip = _T("Application version {0}", v, "Tool tip of the application version on the home page.{0} is replaced with the full version text");
    const k = homeKpi({ Icon: "layers", Label: _TF("Version", "Label of the application version tile on the home page") });
    k.Value.textContent = short;
    const d = homeParseUtc(ds.appbuild);
    if (d) {
        tip += "\n" + _T("Built {0}", d.toLocaleString([], { dateStyle: "medium", timeStyle: "short" }), "Tool tip line of the application version on the home page.{0} is replaced with the date and time the application was built");
        k.Sub.textContent = _T("Build {0}", d.toLocaleDateString([], { dateStyle: "medium" }), "Sub line of the application version tile on the home page.{0} is replaced with the date the application was built");
    }
    k.E.title = tip;
    list.append(k.E);
}

/**
 * The header status badge: connected to the server (and the server time) or not, refreshed every 10 s and on online / offline changes;
 * a warning when problems were logged in the last 24 hours (homeProblems calls SetProblems).
 * Inside the app shell the sidebar footer already says "Connected, server time ..." on every page, so there the badge only shows when it
 * has news (not connected, problems); stand alone (no shell) it always shows.
 * @param {HTMLElement} main The page (main.Home).
 * @returns {{SetProblems: function(number):void}} Updater.
 */
function homeStatus(main) {
    const host = main.querySelector(".sw-ph-status");
    const badge = Sw.el("span.sw-badge.HomeStatus", { attrs: { role: "status" } }, Sw.el("span.HomeDot", { attrs: { "aria-hidden": "true" } }), Sw.el("span.HomeStatusText"));
    let inShell = false;
    try {
        inShell = window.top !== window && !!window.top.SysWeaverApp;
    }
    catch {
    }
    if (host) {
        host.append(badge);
        host.hidden = false;
    }
    let problems = 0;
    /** Refresh the badge. */
    const update = () => {
        let online = navigator.onLine !== false;
        try {
            const s = window.top.Sw && window.top.Sw.Session;
            if (s && s.Blocked)
                online = false;
        }
        catch {
        }
        const time = homeNow().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        badge.className = "sw-badge sw-noicon HomeStatus " + (!online ? "sw-error" : (problems > 0 ? "sw-warning" : "sw-ok"));
        badge.lastChild.textContent = !online
            ? _TF("Not connected to the service", "Status line on the home page of the application when the service cannot be reached")
            : (problems > 0
                ? (problems === 1
                    ? _TF("Connected, 1 problem in 24 h", "Status badge on the home page when exactly one part of the system failed (or one warning or error was logged) in the last 24 hours")
                    : _T("Connected, {0} problems in 24 h", problems, "Status badge on the home page when warnings or errors were logged in the last 24 hours.{0} is replaced with their number"))
                : _T("Connected to the service, server time {0}", time, "Status line on the home page of the application.{0} is replaced with the current time of the server (hours and minutes)"));
        badge.title = online
            ? _T("Connected to the service, server time {0}", time, "Status line on the home page of the application.{0} is replaced with the current time of the server (hours and minutes)")
            : badge.lastChild.textContent;
        if (host)
            host.hidden = inShell && online && problems <= 0;
    };
    update();
    setInterval(update, 10000);
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return {
        SetProblems(n) {
            problems = n | 0;
            update();
        },
    };
}

/**
 * Add one fact to the server facts strip (dl.sw-facts): a label and a value; a button when it opens a page.
 * @param {HTMLElement} dl The strip.
 * @param {string} label The label.
 * @param {string} value The value.
 * @param {string} title The tool tip.
 * @param {string} [url] Page to open (inside the shell).
 */
function homeFact(dl, label, value, title, url) {
    let v;
    if (url) {
        v = Sw.el("button.sw-fact-value", { type: "button", text: value, title });
        v.addEventListener("click", ev => {
            if (!Sw.BadClick(ev))
                Sw.Page.Navigate(url, "_self");
        });
    }
    else
        v = Sw.el("span", { text: value, title });
    dl.append(Sw.el("div.sw-fact", {}, Sw.el("dt", { text: label }), Sw.el("dd", {}, v)));
}

/**
 * The server manager's metric detail page of a "current value" chart api, as the Server page opens it (ServerManager/smShared.js
 * MetricsUrl): the current value chart and its short and long history.
 * @param {string} sm The server manager folder ("../ServerManager/").
 * @param {string} api The current value chart api (ex: "GetCpuChart"; the history apis are derived by name).
 * @param {string} title The page title.
 * @returns {string} The url ("../ServerManager/server_metrics.html?q1=GetCpuChart&q2=GetCpuHistoryShortChart&q3=GetCpuHistoryChart&title=CPU%20use").
 */
function homeMetricsUrl(sm, api, title) {
    const h = api.replace("Chart", "HistoryChart");
    const s = h.replace("Chart", "ShortChart");
    return sm + "server_metrics.html?q1=" + encodeURIComponent(api) + "&q2=" + encodeURIComponent(s) + "&q3=" + encodeURIComponent(h) + "&title=" + encodeURIComponent(title);
}

/**
 * Add the server rows on a server with the server manager for every user who may call its APIs (Api/HaveService, then Sw.Access.CanCall:
 * no token names in shared pages, so Debug, Admin, Ops and custom tokens all work), then ServerManager/GetServerInfo and GetServicesChart, the same calls as the Server page): the "Managed services" KPI tile (running of total, a
 * warning badge for stopped ones; opens the services page) and the facts strip (host, CPUs, memory, processes, drives; each opens its detail page).
 * @param {HTMLElement} main The page (main.Home).
 * @param {HTMLElement} list The KPI row.
 * @param {{Tile: object|null, Facts: HTMLElement|null}} [slot] The reserved tile and facts strip (homePlan), filled in place; removed when
 * there is nothing to show.
 * @returns {Promise<{S: boolean, F: boolean, N: number}>} What was shown (for the next plan).
 */
async function homeServer(main, list, slot) {
    slot = slot || { Tile: null, Facts: null };
    const shown = { S: false, F: false, N: 0 };
    /** Remove the reserved parts that got no data. */
    const unused = () => {
        if (slot.Tile && !shown.S)
            slot.Tile.E.remove();
        if (slot.Facts && !shown.F)
            slot.Facts.remove();
        return shown;
    };
    try {
        //  every user who may call the APIs sees the rows (Debug, Admin, Ops, custom tokens): only when the shell's menu has the server
        //  manager's pages (no Api/HaveService request, the original Home asked nothing of it; stand-alone: not shown)
        if (!homeShellHas(/(^|[\/=])ServerManager\//i))
            return unused();
        if (await Sw.Access.CanCall(Sw.Url.Root + "ServerManager/GetServerInfo") === false)
            return unused();
        const info = await Sw.Net.Get(Sw.Url.Root + "ServerManager/GetServerInfo", { NoNavigate: true });
        if (!info)
            return unused();
        const sm = "../ServerManager/";
        //  Managed services: running / total from the services chart (GetServicesChart: data [running, stopped])
        try {
            const c = await Sw.Access.CanCall(Sw.Url.Root + "ServerManager/GetServicesChart") === false
                ? null
                : await Sw.Net.Get(Sw.Url.Root + "ServerManager/GetServicesChart", { NoNavigate: true });
            const d = c && c.data && c.data.datasets && c.data.datasets[0] && c.data.datasets[0].data;
            if (Array.isArray(d)) {
                const run = Math.round(+d[0] || 0);
                const stop = Math.round(+d[1] || 0);
                const k = slot.Tile || homeServicesTile();
                k.Loaded();
                k.Value.textContent = String(run);
                k.Unit.textContent = _T("of {0} running", run + stop, "Unit after the number of running managed services on the home page.{0} is replaced with the number of managed services");
                if (stop > 0)
                    k.Sub.append(Sw.Badge(_T("{0} stopped", stop, "Badge on the managed services tile of the home page.{0} is replaced with the number of stopped services"), "warning"));
                else if (run + stop === 0)
                    k.Sub.textContent = _TF("No managed services", "Sub line of the managed services tile on the home page when the server manages no services");
                else
                    k.Sub.append(Sw.Badge(_TF("All running", "Badge on the managed services tile of the home page when every service runs"), "ok"));
                if (!k.E.isConnected)
                    list.prepend(k.E);
                shown.S = true;
            }
        }
        catch (x) {
            console.info("Home: services not shown: " + (x && x.message));
        }
        const details = _TF("Show details.", "Tool tip line of a clickable value in the server status row on the home page");
        //  The reserved strip is filled in place (same box: nothing below moves)
        let dl = slot.Facts;
        if (dl) {
            dl.replaceChildren();
            dl.classList.remove("HomeLoading");
            dl.removeAttribute("aria-busy");
        }
        else
            dl = Sw.el("dl.sw-card.sw-facts.HomeFacts", { attrs: { "aria-label": _TF("Server", "Accessible name of the server facts on the home page") } });
        homeFact(dl, _TF("Server", "Label of the server (machine) name on the home page"), info.Machine || "", _TF("Name of the server.", "Tool tip of the server name on the home page") + "\n" + details, sm + "server.html");
        homeFact(dl, info.ProcessorCount === 1
            ? _TF("CPU", "Label below the number of logical cpu cores of the server on the home page (exactly one)")
            : _TF("CPUs", "Label below the number of logical cpu cores of the server on the home page"),
        String(info.ProcessorCount), _TF("Number of logical CPU cores.", "Tool tip of the server cpu count on the home page") + "\n" + details,
            homeMetricsUrl(sm, "GetCpuChart", _TF("CPU use", "Title of a page showing the cpu use of a server")));
        homeFact(dl, _TF("Memory", "Label of the amount of physical memory of the server on the home page"), Sw.Format.Bytes(info.Memory, 2),
            _TF("Amount of physical RAM memory.", "Tool tip of the server memory size on the home page") + "\n" + details,
            homeMetricsUrl(sm, "GetMemChart", _TF("Memory use", "Title of a page showing the memory use of a server")));
        homeFact(dl, _TF("Processes", "Label of the number of running processes of the server on the home page"), String(info.ProcessCount),
            _TF("Number of processes that are running.", "Tool tip of the server process count on the home page") + "\n" + details, "../explore/table.html?q=../ServerManager/ProcessInfoTable");
        if (info.DriveCount > 0)
            homeFact(dl, info.DriveCount === 1
                ? _TF("Drive", "Label of the number of drives of the server on the home page (exactly one)")
                : _TF("Drives", "Label of the number of drives of the server on the home page"),
            String(info.DriveCount), _TF("Number of drives of the server.", "Tool tip of the server drive count on the home page") + "\n" + details, "../explore/table.html?q=../ServerManager/DriveInfoTable");
        if (!dl.isConnected)
            list.after(dl);
        shown.F = true;
        shown.N = dl.children.length;
    }
    catch (x) {
        //  Only extra information (no access, no server manager): the page keeps what it has
        console.info("Home: server status not shown: " + (x && x.message));
    }
    return unused();
}

/**
 * The managed services tile (filled by homeServer; created early as a reserved tile by homeMain).
 * @param {boolean} [loading] Reserved: a skeleton value until the data arrives.
 * @returns {object} The tile (homeKpi).
 */
function homeServicesTile(loading) {
    return homeKpi({
        Icon: "grid", Url: "../ServerManager/services.html", Loading: !!loading,
        Label: _TF("Managed services", "Label of the tile with the number of managed services on the home page"),
        Title: _TF("Show the managed services", "Tool tip of the managed services tile on the home page"),
    });
}

/**
 * Read the rows of a server table (TableData: Cols + Rows[].Values) as objects by column name.
 * @param {{Cols: {Name:string}[], Rows: {Values: any[]}[]}} r The table data.
 * @returns {object[]} The rows.
 */
function homeTableRows(r) {
    const cols = ((r && r.Cols) || []).map(c => c.Name);
    return ((r && r.Rows) || []).map(x => {
        const o = {};
        cols.forEach((n, i) => {
            if (!(n in o))
                o[n] = x.Values[i];
        });
        return o;
    });
}

/**
 * The problems of a ProblemsTable answer (homeProblemRows), newest first.
 * @param {object} r The TableData.
 * @returns {{Tracker: boolean, Url: string, Rows: {Time:number, Error:boolean, Text:string, Title:string, Count:number}[]}} The problems.
 */
function homeProblemData(r) {
    const rows = homeTableRows(r).map(v => {
        const ex = String(v.LastException ?? "");
        const first = ex.split(/\r?\n/)[0].trim();
        const name = [v.System, v.Source].filter(x => x && x !== "-").join(" ");
        return {
            Time: Date.parse(v.LastTime) || 0,
            Error: true,
            Count: +v.Count || 0,
            Text: name + (first ? ": " + first : ""),
            Title: name + "\n" + _T("{0} failures since the service started", +v.Count || 0, "Tool tip line of a problem on the home page.{0} is replaced with the number of failures") + (ex ? "\n\n" + ex : ""),
        };
    }).filter(x => x.Time > 0).sort((a, b) => b.Time - a.Time);
    return { Tracker: true, Url: "../explore/table.html?q=../Api/debug/ProblemsTable", Rows: rows };
}

/**
 * Does the main menu of the shell this page is shown in have an entry whose url matches (no request)?
 * @param {RegExp} rx The url test.
 * @returns {boolean} False stand-alone or without such an entry.
 */
function homeShellHas(rx) {
    try {
        const app = window.top !== window ? window.top.SysWeaverApp : null;
        const m = app && app.Menu;
        return !!(m && m.Entries && m.Entries.some(e => rx.test(String(e.Url || (e.Item && e.Item.Data) || ""))));
    }
    catch {
        return false;
    }
}

/**
 * The problems of the exception trackers (not every failure is logged, the exception trackers count
 * every tracked failure): `Api/debug/ProblemsTable` (ServiceManager), one row per failing part (System, Source, Count, LastTime, LastException).
 * Servers without the problems table (older ServiceManager) fall back to the warnings and errors of the in-memory log (Api/debug/LogTable).
 * Users who may call neither get null (asked with Sw.Access.CanCall first, so they don't get a refused request on every visit).
 * @returns {Promise<{Tracker: boolean, Url: string, Rows: {Time:number, Error:boolean, Text:string, Title:string, Count:number}[]}|null>}
 * The problems, newest first.
 */
async function homeProblemRows() {
    const root = Sw.Url.Root;
    //  The shell already asked (its Problems badge): its answer, no second request
    let shared;
    try {
        const app = window.top !== window ? window.top.SysWeaverApp : null;
        if (app && app.ProblemsP)
            shared = await app.ProblemsP;
    }
    catch {
        shared = undefined;
    }
    if (shared === null)
        return null;
    if (shared && Array.isArray(shared.Rows))
        return homeProblemData(shared);
    if (await Sw.Access.CanCall(root + "Api/debug/ProblemsTable") !== false)
        try {
            const r = await Sw.Net.Send(root + "Api/debug/ProblemsTable", { MaxRowCount: 200, Order: ["-LastTime"] }, { NoNavigate: true });
            if (r && Array.isArray(r.Rows))
                return homeProblemData(r);
        }
        catch (x) {
            if (Sw.Access.IsDenied(x))
                return null;
            console.info("Home: no problems table, using the log: " + (x && x.message));
        }
    //  Older servers: the warnings and errors of the in-memory log
    if (await Sw.Access.CanCall(root + "Api/debug/LogTable") === false)
        return null;
    const r = await Sw.Net.Send(root + "Api/debug/LogTable",
        { MaxRowCount: 200, Order: ["-Id"], Filters: [{ ColName: "Level", Op: "GreaterEqual", Value: "Warning" }] }, { NoNavigate: true });
    if (!r || !Array.isArray(r.Rows))
        return null;
    const cols = (r.Cols || []).map(c => c.Name);
    const iTime = cols.indexOf("Time");
    const iLevel = cols.indexOf("Level");
    const iText = cols.indexOf("Text");
    const iPrefix = cols.lastIndexOf("Prefix");
    return {
        Tracker: false,
        Url: "../explore/table.html?q=../Api/debug/LogTable",
        Rows: r.Rows.map(x => x.Values).map(v => {
            const prefix = iPrefix >= 0 ? String(v[iPrefix] || "").replace(/[[\]. ]/g, "") : "";
            const text = (prefix ? prefix + ": " : "") + String(v[iText] ?? "");
            return { Time: Date.parse(v[iTime]) || 0, Error: (+v[iLevel] || 0) >= 4, Count: 1, Text: text, Title: text };
        }),
    };
}

/**
 * The problems of the last 24 hours (homeProblemRows: the exception trackers; the log on older servers): a KPI tile (the number of
 * parts that failed in the last 24 h, opens the Problems page), the "Problems" card with the latest 3 (badge, part + last exception, time)
 * and the header status. Nothing is shown without access.
 * @param {HTMLElement} list The KPI row.
 * @param {HTMLElement} cards The card grid (or null stand alone: tile only).
 * @param {{SetProblems: function(number):void}} status The header status.
 * @param {{Tile: object|null, Card: HTMLElement|null}} [slot] The reserved tile and card (homePlan): the tile is filled in place, the
 * reserved card is replaced by the caller; both are removed when nothing is shown.
 * @returns {Promise<HTMLElement|null>} The card (not yet inserted), null without access.
 */
async function homeProblems(list, cards, status, slot) {
    slot = slot || { Tile: null, Card: null };
    const none = () => {
        if (slot.Tile)
            slot.Tile.E.remove();
        if (slot.Card)
            slot.Card.remove();
        return null;
    };
    let p;
    try {
        p = await homeProblemRows();
        if (!p)
            return none();
    }
    catch (x) {
        console.info("Home: problems not shown: " + (x && x.message));
        return none();
    }
    const rows = p.Rows;
    const url = p.Url;
    const day = rows.filter(x => x.Time > Date.now() - 86400000);
    status.SetProblems(day.length);
    const k = slot.Tile || homeProblemsTile();
    k.Url = url;
    k.E.title = p.Tracker
        ? _TF("Show the problems: how often each part of the system failed, when and why", "Tool tip of the problems tile on the home page (opens the problems table)")
        : _TF("Show the log", "Tool tip of the problems tile on the home page");
    k.Loaded();
    k.Value.textContent = String(day.length) + (rows.length >= 200 && day.length >= 200 ? "+" : "");
    if (p.Tracker && day.length)
        k.Unit.textContent = day.length === 1
            ? _TF("part failing", "Unit after the number of failing parts of the system (exactly one) on the home page")
            : _TF("parts failing", "Unit after the number of failing parts of the system on the home page");
    k.Sub.textContent = day.length
        ? _T("Latest {0}", homeAgo(day[0].Time), "Sub line of the problems tile on the home page.{0} is replaced with how long ago the latest problem was logged (ex: 12 minutes ago)")
        : (p.Tracker
            ? _TF("No failures", "Sub line of the problems tile on the home page when no part of the system failed in the last 24 hours")
            : _TF("No warnings or errors", "Sub line of the problems tile on the home page when nothing was logged"));
    if (!k.E.isConnected)
        list.append(k.E);
    slot.Shown = true;
    if (!cards) {
        if (slot.Card)
            slot.Card.remove();
        return null;
    }
    const open = Sw.el("button.sw-linkbtn.sw-push", {
        type: "button",
        text: p.Tracker
            ? _TF("Open Problems", "Link in the problems card of the home page that opens the problems table")
            : _TF("Open the log", "Link in the problems card of the home page that opens the log table"),
    });
    open.addEventListener("click", ev => {
        if (!Sw.BadClick(ev))
            Sw.Page.Navigate(url, "_self");
    });
    const head = Sw.el("div.sw-card-head", {}, Sw.el("h2", { text: _TF("Problems", "Title of the card with the latest logged warnings and errors on the home page") }));
    if (day.length) {
        //  a count badge: no status glyph (sw-count), the card title says what is counted
        const cb = Sw.Badge(String(day.length), "error");
        cb.classList.add("sw-count");
        head.append(cb);
    }
    head.append(open);
    const card = Sw.el("section.sw-card.HomeCard.HomeProblems", {}, head);
    if (!rows.length)
        card.append(Sw.StateView({
            Small: true, Icon: "check",
            Title: _TF("No problems", "Title of the empty problems card on the home page"),
            Text: p.Tracker
                ? _TF("No part of the system has failed since the service started.", "Text of the empty problems card on the home page (exception trackers)")
                : _TF("No warnings or errors were logged.", "Text of the empty problems card on the home page"),
        }).E);
    else {
        const ul = Sw.el("ul.sw-rows");
        for (const x of rows.slice(0, 3)) {
            const recent = x.Time > Date.now() - 86400000;
            const badge = p.Tracker
                ? (recent
                    ? Sw.Badge(_TF("Failing", "Badge of a part of the system that failed in the last 24 hours, on the home page"), "error")
                    : Sw.Badge(_TF("Failed", "Badge of a part of the system that failed more than 24 hours ago, on the home page"), "warning"))
                : Sw.Badge(x.Error ? _TF("Error", "Level badge of a logged error on the home page") : _TF("Warning", "Level badge of a logged warning on the home page"), x.Error ? "error" : "warning");
            ul.append(Sw.el("li", { title: x.Title },
                badge,
                Sw.el("span.sw-rows-text", { text: x.Text }),
                Sw.el("span.sw-rows-meta", { text: homeAgo(x.Time) })));
        }
        card.append(ul);
    }
    return card;
}

/**
 * The problems tile (filled by homeProblems; created early as a reserved tile by homeMain). Its page is set by homeProblems (Url: the
 * problems table, or the log on older servers).
 * @param {boolean} [loading] Reserved: a skeleton value until the data arrives.
 * @returns {object} The tile (homeKpi) with a writable Url.
 */
function homeProblemsTile(loading) {
    const ref = { Url: "../explore/table.html?q=../Api/debug/ProblemsTable" };
    const k = homeKpi({
        Icon: "warning", Url: () => ref.Url, Loading: !!loading,
        Label: _TF("Problems, last 24 h", "Label of the tile with the number of logged warnings and errors of the last 24 hours on the home page"),
        Title: _TF("Show the problems: how often each part of the system failed, when and why", "Tool tip of the problems tile on the home page (opens the problems table)"),
    });
    Object.defineProperty(k, "Url", { get: () => ref.Url, set: v => ref.Url = v });
    return k;
}

/**
 * Normalize a page url like the shell's recent pages keys (shell/menu.js NormalizeUrl: no hash, no usetheme, re-serialized query).
 * @param {string} url The url.
 * @returns {string} The key.
 */
function homeUrlKey(url) {
    try {
        const u = new URL(url, Sw.Url.Root);
        u.hash = "";
        [...u.searchParams.keys()].filter(k => k.toLowerCase() === "usetheme").forEach(k => u.searchParams.delete(k));
        u.search = u.searchParams.toString();
        return u.href;
    }
    catch {
        return "";
    }
}

/**
 * Make a link row (li > button.sw-rows-link: menu icon, name, meta text); a click (Enter / Space) opens the page like its menu item.
 * @param {object} l The link (Sw.Shell.App.GetQuickLinks).
 * @param {string} meta The meta text (right).
 * @returns {HTMLElement} The row.
 */
function homeLinkRow(l, meta) {
    const b = Sw.el("button.sw-rows-link", { type: "button", title: l.Title ? l.Name + "\n\n" + l.Title : l.Name },
        l.Icon ? (Sw.LineIcon ? Sw.LineIcon(l.Icon, "QuickIcon") : Sw.IconClass(l.Icon, { Class: "QuickIcon" })) : Sw.el("span.QuickIcon.QuickNoIcon", { attrs: { "aria-hidden": "true" } }),
        Sw.el("span.sw-rows-text", { text: l.Name }),
        meta ? Sw.el("span.sw-rows-meta", { text: meta }) : null);
    b.addEventListener("click", ev => {
        if (!Sw.BadClick(ev))
            l.Open(ev);
    });
    return Sw.el("li", {}, b);
}

/**
 * The quick links inside the shell (Sw.Shell.App.GetQuickLinks, localStorage "SysWeaver.RecentPages"):
 *  - "Shortcuts": the top level main menu pages as tiles (tonal icon square, name, one line description; the full text in the tool tip),
 *  - "Recent pages": the last visited pages with how long ago (a "Clear" button forgets the history), an empty state before the first visit,
 *  - "Most used": the pages this browser opened most often (at least twice) with the visit count.
 * A click (Enter / Space) opens the page like its menu item.
 * @param {HTMLElement} main The page (main.Home).
 * @param {object} shell The shell (Sw.Shell.App of the top window).
 * @returns {{Count: number, Cards: HTMLElement[]}} The number of links shown and the cards (not yet inserted).
 */
function homeQuickLinks(main, shell) {
    let q;
    try {
        q = shell.GetQuickLinks({ MaxRecent: 6, MaxLast: 6, MaxTop: 12, MinCount: 2 });
    }
    catch (x) {
        console.warn("Home: quick links failed: " + (x && x.message));
        return { Count: 0, Cards: [] };
    }
    let count = 0;
    //  Shortcuts: tiles
    if (q.Top && q.Top.length) {
        const ul = Sw.el("ul.sw-tiles.QuickList", { attrs: { "aria-labelledby": "home-ql-Top" } });
        for (const l of q.Top) {
            const b = Sw.el("button.sw-tile.QuickLink", { type: "button", title: l.Title ? l.Name + "\n\n" + l.Title : l.Name },
                Sw.el("span.sw-tile-ico", { attrs: { "aria-hidden": "true" } }, l.Icon ? (Sw.LineIcon ? Sw.LineIcon(l.Icon, "QuickIcon") : Sw.IconClass(l.Icon, { Class: "QuickIcon" })) : Sw.Icon("file")),
                Sw.el("span.sw-tile-text", {}, Sw.el("b", { text: l.Name }), l.Title ? Sw.el("span", { text: l.Title }) : null));
            b.addEventListener("click", ev => {
                if (!Sw.BadClick(ev))
                    l.Open(ev);
            });
            ul.append(Sw.el("li", {}, b));
            ++count;
        }
        const sec = Sw.el("section.QuickLinks", {},
            Sw.el("div.sw-section-title", {}, Sw.el("h2.QuickHeading", { id: "home-ql-Top", text: _TF("Shortcuts", "Heading of a list of quick links on the home page: the main pages of the application menu") }),
                Sw.el("span", { text: _TF("The main pages of the menu", "Sub title of the shortcuts on the home page") })),
            ul);
        main.querySelector(".HomeCards").before(sec);
    }
    //  Visit times of the shell's recent pages (by normalized url)
    const times = new Map();
    try {
        const list = Sw.Storage.GetJson("SysWeaver.RecentPages", []);
        if (Array.isArray(list))
            for (const x of list)
                if (x && typeof x.U === "string")
                    times.set(x.U, { T: x.T || 0, C: x.C | 0 });
    }
    catch {
    }
    const cards = [];
    //  Recent pages
    {
        const head = Sw.el("div.sw-card-head", {}, Sw.el("h2", { id: "home-ql-Last", text: _TF("Recent pages", "Heading of a list of quick links on the home page: the pages the user opened last") }));
        const card = Sw.el("section.sw-card.HomeCard.QuickGroup.Last", { attrs: { "aria-labelledby": "home-ql-Last" } }, head);
        const last = q.Last || [];
        if (last.length) {
            const clear = Sw.el("button.sw-linkbtn.sw-push", { type: "button", text: _TF("Clear", "Button in the recent pages card of the home page that forgets the visited pages"),
                title: _TF("Forget the pages this browser visited", "Tool tip of the clear button in the recent pages card of the home page") });
            clear.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                Sw.Storage.Remove("SysWeaver.RecentPages");
                card.querySelector(".sw-rows")?.replaceWith(homeNoRecent());
                const grid = card.parentElement;
                document.querySelector(".HomeMost")?.remove();
                if (grid)
                    homePlaceCards(grid, [...grid.children]);
                clear.remove();
            });
            head.append(clear);
            const ul = Sw.el("ul.sw-rows.QuickList");
            for (const l of last) {
                const t = times.get(homeUrlKey(l.Url));
                ul.append(homeLinkRow(l, t ? homeAgo(t.T) : ""));
                ++count;
            }
            card.append(ul);
        }
        else
            card.append(homeNoRecent());
        cards.push(card);
    }
    //  Most used (pages opened at least twice)
    const recent = q.Recent || [];
    if (recent.length) {
        const ul = Sw.el("ul.sw-rows.QuickList");
        for (const l of recent) {
            const t = times.get(homeUrlKey(l.Url));
            ul.append(homeLinkRow(l, t ? _T("{0} visits", t.C, "Meta text of a most used page on the home page.{0} is replaced with the number of visits") : ""));
            ++count;
        }
        cards.push(Sw.el("section.sw-card.HomeCard.HomeMost.QuickGroup.Recent", { attrs: { "aria-labelledby": "home-ql-Recent" } },
            Sw.el("div.sw-card-head", {}, Sw.el("h2", { id: "home-ql-Recent", text: _TF("Most used", "Heading of a list of quick links on the home page: the pages the user opened most often") })),
            ul));
    }
    return { Count: count, Cards: cards };
}

/**
 * The empty state of the recent pages card.
 * @returns {HTMLElement} The state view.
 */
function homeNoRecent() {
    return Sw.StateView({
        Small: true, Icon: "history",
        Title: _TF("No pages visited yet", "Title of the empty recent pages card on the home page"),
        Text: _TF("Pages you open from the menu appear here.", "Text of the empty recent pages card on the home page"),
    }).E;
}

/**
 * Place the cards in the card grid: two per row (one per row on narrow screens), three in a row when there are three.
 * @param {HTMLElement} grid The grid (div.HomeCards).
 * @param {HTMLElement[]} cards The cards.
 */
function homePlaceCards(grid, cards) {
    const span = cards.length === 3 ? "sw-span-4" : "sw-span-6";
    for (const c of cards) {
        c.classList.remove("sw-span-4", "sw-span-6");
        c.classList.add(span);
        grid.append(c);
    }
    grid.hidden = !grid.children.length;
}

/**
 * Page entry point (body onload): shows the loader, titles the user image, sets the greeting, adds the status badge, the KPI tiles (uptime,
 * version; managed services, server facts and problems in the background when the user may see them), the quick links and the getting
 * started hint (inside the shell), asks the server whether the custom user image service exists (`Api/HaveService`) and if so loads
 * common/fileUploader.js and makes the user image an upload target (click, Enter / Space or drop an image; repo "UserImage", images only in the picker).
 * @returns {Promise<void>}
 */
async function homeMain() {
    const done = Sw.Page.Loading();
    //  The user image is the user's own picture (not a brand mark): say so
    const userImg = document.querySelector("img.UserImage");
    if (userImg) {
        userImg.title = _TF("Your user picture", "Tool tip of the picture of the signed in user on the home page");
    }
    const main = document.querySelector("main.Home");
    if (main) {
        homeGreeting(main);
        const status = homeStatus(main);
        const kpis = main.querySelector(".HomeKpis");
        kpis.setAttribute("aria-label", _TF("Server status", "Accessible name of the row of server status values (uptime, cpus, memory ...) on the home page"));
        main.querySelector(".HomeCards").setAttribute("aria-label", _TF("Pages and problems", "Accessible name of the cards with recent pages and problems on the home page"));
        const grid = main.querySelector(".HomeCards");
        const shell = homeShell();
        //  The final layout from the first paint (UX guide LAY-1, LAY-2): the parts that load in the background get their place now
        //  (the layout of the last visit, else what the menu suggests) and are filled in place, so nothing moves when slow data arrives
        const plan = homePlan(main, shell);
        const sTile = plan.S ? homeServicesTile(true) : null;
        if (sTile)
            kpis.append(sTile.E);
        const start = homeAppStart(main);
        if (start)
            homeUptime(kpis, start);
        homeVersion(main, kpis);
        const pTile = plan.P ? homeProblemsTile(true) : null;
        if (pTile)
            kpis.append(pTile.E);
        const facts = plan.F ? homeFactsSkeleton(plan.N) : null;
        if (facts)
            kpis.after(facts);
        const ql = shell ? homeQuickLinks(main, shell) : { Count: 0, Cards: [] };
        const pCard = plan.C && shell ? homeProblemsSkeleton() : null;
        homePlaceCards(grid, pCard ? [...ql.Cards, pCard] : ql.Cards);
        const hint = main.querySelector(".HomeHint");
        if (hint && Sw.Page.IsEmbedded)
            hint.textContent = homeHint(ql.Count > 0);
        //  Server facts and problems load in the background (the page shows at once); what was shown is the next visit's plan
        const server = homeServer(main, kpis, { Tile: sTile, Facts: facts });
        const pSlot = { Tile: pTile, Card: pCard, Shown: false };
        const problems = homeProblems(kpis, shell ? grid : null, status, pSlot).then(card => {
            if (card && pCard && pCard.isConnected) {
                //  The real card takes the reserved card's place (and its grid span)
                card.classList.add(...[...pCard.classList].filter(c => c.startsWith("sw-span-")));
                pCard.replaceWith(card);
            }
            else if (card)
                homePlaceCards(grid, [...grid.children, card]);
            else if (pCard)
                homePlaceCards(grid, [...grid.children]);
            return { P: pSlot.Shown, C: !!card };
        });
        Promise.all([server, problems]).then(([s, p]) => {
            try {
                Sw.Storage.Local.SetJson(plan.Key, { S: s.S, F: s.F, N: s.N || plan.N, P: p.P, C: p.C });
            }
            catch {
            }
        }, () => { });
        //  The application name of the shell when the template had none
        const h = main.querySelector("h1.AppName");
        if (h && !h.textContent.trim())
            h.textContent = await Sw.Page.AppName() || "";
    }
    try {
        if (await Sw.Net.Send(Sw.Url.Root + "Api/HaveService", "SysWeaver.MicroService.CustomUserImageService")) {
            await Sw.Net.IncludeJs(null, "fileUploader.js");
            const img = document.querySelector("img.UserImage");
            const title = _TF("Select a new user image or drag an image here", "A tool tip description of a html element that will let the user modify their user image");
            img.classList.add("CanUpload");
            img.setAttribute("aria-label", title);
            Sw.Upload.Target(img, {
                Repo: "UserImage",
                Accept: "image/*",
                Title: title,
                OnStart: (el, files) => Sw.Info(_T("Uploading user image \"{0}\"", files[0].name, "Text displayed when a user image file is about to be uploaded to a server.{0} is replaced with the name of the user image file")),
                OnResults: (el, res, files) => homeUploaded(img, res, files),
            });
            await Sw.Upload.Ensure();
        }
    }
    catch (e) {
        Sw.Fail(e.message || String(e));
    }
    finally {
        done();
    }
}
