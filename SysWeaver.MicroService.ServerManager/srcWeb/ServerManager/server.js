/**
 * @file server.js - the ServerManager "Server" dashboard (server.html, entry `serverMain`) and the metric detail page
 * (server_metrics.html, entry `serverMetricsMain`).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js and smShared.js; the pages call their
 * entry from `<body onload>`.
 *
 * Design v2 page anatomy (Design v2, PORTING.md B11 "Server"), built from the shared ui components (ui/page.js):
 *  - Sw.PageLayout: page header "Server" (the menu name), a one line description, actions "Deleted files" and "Reboot..." (danger outline,
 *    confirmed),
 *  - Sw.Facts: the facts strip (Host, Operating system, Processors, Memory, Processes, Server time) instead of the old pill row,
 *  - Sw.Kpi: KPI tiles with a meter for the running services, CPU, Memory and every drive (instead of the donut charts, fed by the same
 *    chart apis at the same rate, so the page sends the same requests as the original page).
 *
 * Globals defined: `serverMain`, `serverMetricsMain`, `smReboot`, class `SmServerClock`.
 *
 * Server API (relative to ServerManager/), the same requests as the original page: GetServerInfo (once), GetServerStats (every 5 s), the
 * chart apis GetServicesChart, GetCpuChart, GetMemChart (every 2 s) and GetDriveChart?<i> (every 10 s; the rate is the RefreshRate of the
 * reply, as the chart page polls it, see smPollChart), RebootComputer (with the RebootToken of GetServerInfo). The metric page shows the chart
 * apis Get<X>Chart, Get<X>HistoryShortChart and Get<X>HistoryChart.
 * Charts are iframes of ../chart/chart.html (one way, url parameters only) under a transparent click button (SmShared.ChartTile).
 *
 * Url parameters of server_metrics.html: q1 (current value chart api), q2 (short history api), q3 (long history api),
 * title (document title; untrusted url text, so set by Sw.Page.SetLinkTitle: plain, limited text, no caption). Each api is relative to ServerManager/ and may carry its own "?<arg>".
 */

/**
 * The live server clock: the server's local wall clock estimated from a server time stamp and half the round trip.
 */
class SmServerClock {
    /**
     * @param {HTMLElement} textEl - Element receiving "HH:mm:ss".
     * @param {HTMLElement} titleEl - Element receiving the tool tip (date and time zone).
     * @param {{TzName: string, TzDayName: string}} info - Time zone names.
     */
    constructor(textEl, titleEl, info) {
        /** @type {HTMLElement} */
        this.TextEl = textEl;
        /** @type {HTMLElement} */
        this.TitleEl = titleEl;
        /** @type {string} Time zone text for the tool tip. */
        this.Zone = info.TzDayName + (info.TzDayName !== info.TzName ? " (" + info.TzName + ")" : "");
        /** @type {number} Server wall clock (as an UTC ms value) at local time AtTime. */
        this.ServerStart = 0;
        /** @type {number} Local time (ms) of the last sync. */
        this.AtTime = 0;
    }

    /**
     * Sync with a server time stamp.
     * @param {string} time - The server local time (ISO text without zone, from the server).
     * @param {number} sentAt - Local time (ms) when the request was sent.
     */
    Sync(time, sentAt) {
        const now = Date.now();
        const halfPing = (now - sentAt) * 0.5;
        this.AtTime = now;
        this.ServerStart = Sw.Format.UtcToLocal(new Date(new Date(time).getTime() + halfPing)).getTime();
    }

    /**
     * The current server wall clock as [date, time] texts ("yyyy-MM-dd", "HH:mm:ss").
     * @returns {string[]} The parts.
     */
    Now() {
        //  SwWeb:allow ux-txt-20 (the server's wall clock kept as a UTC ms value: the ISO parts are the server's own date and time, shown with its zone)
        const c = new Date(this.ServerStart + (Date.now() - this.AtTime)).toISOString().split("T");
        return [c[0], c[1].split(".")[0]];
    }

    /**
     * Redraw (only touches the DOM when a text changed).
     */
    Draw() {
        if (!this.TextEl)
            return;
        const n = this.Now();
        if (this.TextEl.textContent !== n[1])
            this.TextEl.textContent = n[1];
        //  The shared time rule (core/README "Times"): the server's wall clock is labelled with its zone; when the viewer
        //  is in another zone the tool tip also gives the viewer's time (the sidebar line says "your time")
        const F = Sw.Format;
        const zone = F && typeof F.ServerZone === "function" ? F.ServerZone() : undefined;
        const local = new Date();
        const differs = typeof zone === "number" && zone !== -local.getTimezoneOffset();
        const lines = [
            _T("Date: {0}", n[0], "Tool tip line of the server clock.{0} is replaced with the date"),
            _T("Time zone: {0}", this.Zone + (typeof zone === "number" && F.OffsetText ? ", " + F.OffsetText(zone) : ""), "Tool tip line of the server clock.{0} is replaced with the time zone name"),
            _TF("This is the local server time and date.", "Tool tip line of the server clock"),
        ];
        if (differs)
            lines.push(_T("Your time: {0} ({1})", local.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }), F.ZoneText ? F.ZoneText(local) : "", "Tool tip line of the server clock when the viewer is in another time zone.{0} is replaced with the viewer's time, {1} with the viewer's zone (ex: WEST, UTC+1)"));
        lines.push("", _TF("Copy this to the clipboard", "Tool tip line of the server clock"));
        const t = SmShared.Lines(...lines);
        if (this.TitleEl && this.TitleEl.title !== t)
            this.TitleEl.title = t;
    }

    /**
     * Start redrawing every 100 ms (as long as the page lives).
     */
    Start() {
        this.Draw();
        setInterval(() => this.Draw(), 100);
    }
}

/**
 * Format a percentage for a KPI tile (one decimal, "-" when unknown).
 * @param {number} v - The value (0..100).
 * @returns {string} The text.
 */
function smPercent(v) {
    return Number.isFinite(v) ? v.toFixed(1) : "-";
}

/**
 * Show the meter level of a KPI tile also as text (SmShared.KpiSub).
 * @param {object} kpi - The tile (Sw.Kpi).
 * @param {string} text - The context line.
 */
function smKpiSub(kpi, text) {
    SmShared.KpiSub(kpi, text);
}

/**
 * The used / free values (in GB) and the title of a two value chart (GetDriveChart, GetMemChart: dataset 0 = [used, free]).
 * @param {?object} c - The chart config.
 * @returns {?{Title: string, Used: number, Free: number}} The values, null when unusable.
 */
function smUsedFree(c) {
    const d = c && c.data && c.data.datasets && c.data.datasets[0] && c.data.datasets[0].data;
    if (!Array.isArray(d) || d.length < 2)
        return null;
    return { Title: String(c.Title || ""), Used: +d[0] || 0, Free: +d[1] || 0 };
}

/**
 * Poll a chart api the way the chart page (chart/chart.html) polls it, so the page sends the same requests as the original chart tiles:
 * GET ../ServerManager/<api>, then again RefreshRate ms (from the reply, at least 500 ms, 15 s until a reply names one) after the
 * reply; a failed request waits 3 s more. Runs as long as the page lives.
 * @param {string} api - Chart api relative to ServerManager/ (may carry "?<arg>").
 * @param {function(object):void} onData - Called with every reply (the chart config).
 * @returns {Promise<void>} Resolves after the first request (answered or failed).
 */
async function smPollChart(api, onData) {
    let rate = 15000;
    const once = async () => {
        try {
            const d = await Sw.Net.Get(api, { NoNavigate: true });
            if (d) {
                onData(d);
                if (d.RefreshRate > 0)
                    rate = d.RefreshRate;
                if (rate < 500)
                    rate = 500;
            }
        }
        catch {
            //  A live poll: the tile keeps its last value and the next poll retries (a lost connection is the shell's connection line)
            await Sw.Util.Delay(3000);
        }
        setTimeout(once, rate);
    };
    await once();
}

/**
 * Entry point of server.html: the server dashboard (page header, facts, KPI tiles, charts, services).
 * @returns {Promise<void>}
 */
function serverMain() {
    return Sw.Page.Main(async () => {
        const sentAt = Date.now();
        /** @type {{Machine:string, Os:string, OsBase:string, ProcessorCount:number, Memory:number, Time:string, TzName:string, TzDayName:string, ProcessCount:number, DriveCount:number}} */
        const info = await Sw.Net.Get("GetServerInfo");
        if (!info)
            return;     //  Redirected (ex: to the login page) or nothing to show
        const clock = new SmServerClock(null, null, info);
        clock.Sync(info.Time, sentAt);
        //  The history charts get UTC time labels: show them in the server's time zone, like the "server time" clock
        SmShared.UseServerTimeZone(info);

        const cpuTitle = _TF("CPU use", "Title of a page showing the cpu use of a server");
        const memTitle = _TF("Memory use", "Title of a page showing the memory use of a server");
        const discTitle = _TF("Disc use", "Title of a page showing the disc use of a server");
        const showDetails = _TF("Show details.", "Tool tip line of a clickable statistic");
        const procUrl = "../explore/table.html?q=../ServerManager/ProcessInfoTable";

        //  Page header: the actions (the harmless "Deleted files" first, "Reboot..." as a danger outline, confirmed by a dialog)
        let procCount = String(info.ProcessCount);
        const page = Sw.PageLayout({
            Main: ".sm-server",
            Type: "dash",
            Title: _TF("Server", "Title of the server dashboard page"),
            Description: _T("Live resource use of {0}, updated every 2 seconds.", info.Machine, "Description of the server dashboard page, {0} is the server (machine) name"),
            Actions: [
                new Sw.Button({
                    Text: _TF("Deleted files", "Text of a button that opens the folder with backups of deleted and replaced files"),
                    Icon: "folder",
                    Title: SmShared.Lines(
                        _TF("Browse the backup folder of the server manager.", "Tool tip line of a button that opens the folder with backups of deleted and replaced files"),
                        _TF("It keeps copies of deleted and replaced service files and keys.", "Tool tip line of a button that opens the folder with backups of deleted and replaced files"),
                        _TF("Nothing is deleted by this button.", "Tool tip line of a button that opens the folder with backups of deleted and replaced files")),
                    OnClick: () => Sw.Page.Navigate("../ServerManager/Bak/explore", "_self"),
                }),
                new Sw.Button({
                    Text: _TF("Reboot...", "Text of a button that reboots the server (asks for a confirmation first)"),
                    Icon: "power",
                    Kind: "danger",
                    Title: _TF("Reboot the server (asks for a confirmation first)", "Tool tip of a button that reboots the server"),
                    OnClick: () => smReboot(info.Machine, procCount, info, sentAt),
                }),
            ],
        });

        //  Facts strip (static facts; the processes and the clock are live)
        const facts = Sw.Facts({ Label: _TF("Server", "Accessible name of the server facts strip") });
        facts.Add({ Label: _TF("Host", "Label of the server name in the facts strip of the server page"), Value: info.Machine, Title: _TF("Name of the server.", "Tool tip of the server name") });
        facts.Add({ Label: _TF("Operating system", "Label of the server operating system in the facts strip of the server page"), Value: info.Os, Title: _TF("Operative system.", "Tool tip of the server operating system") });
        facts.Add({
            Label: _TF("Processors", "Label of the number of logical cpu cores in the facts strip of the server page"),
            Value: info.ProcessorCount === 1 ? _TF("1 logical CPU", "Number of logical cpu cores of a server (exactly one)") : _T("{0} logical CPUs", info.ProcessorCount, "Number of logical cpu cores of a server, {0} is the number"),
            Title: SmShared.Lines(_TF("Number of logical CPU cores.", "Tool tip of the server cpu count"), "", showDetails),
            Href: SmShared.MetricsUrl("GetCpuChart", cpuTitle),
        });
        facts.Add({
            Label: _TF("Memory", "Label of the amount of physical memory in the facts strip of the server page"), Value: Sw.Format.Bytes(info.Memory, 2),
            Title: SmShared.Lines(_TF("Amount of physical RAM memory.", "Tool tip of the server memory size"), "", showDetails),
            Href: SmShared.MetricsUrl("GetMemChart", memTitle),
        });
        const procFact = facts.Add({
            Label: _TF("Processes", "Label of the number of running processes in the facts strip of the server page"), Value: procCount,
            Title: SmShared.Lines(_TF("Number of processes that are running.", "Tool tip of the server process count"), "", showDetails),
            Href: procUrl,
        });
        const clockFact = facts.Add({
            //  "Server time (UTC+2)": the server's wall clock with its zone (the shared time rule)
            Label: Sw.Format.ServerZoneText && Sw.Format.ServerZoneText()
                ? _T("Server time ({0})", Sw.Format.ServerZoneText(), "Label of the live clock (local time of the server) in the facts strip of the server page.{0} is replaced with the server's zone, ex: UTC+2")
                : _TF("Server time", "Label of the live clock (local time of the server) in the facts strip of the server page"),
            Value: "",
            OnClick: () => {
                const n = clock.Now();
                return Sw.Clipboard.Copy(n[0] + " " + n[1]);
            },
        });
        clock.TextEl = clockFact.ValueE;
        clock.TitleEl = clockFact.ValueE;
        clock.Start();

        //  KPI tiles (the original's donut charts, same data): running services, CPU, memory, one per drive. A click opens the services
        //  page (services) or the metric page (the others).
        const kpis = Sw.Grid({ Label: _TF("Resource use", "Accessible name of the KPI tiles of the server page") });
        const svc = Sw.Kpi({
            Label: _TF("Running services", "Label of the running managed services tile of the server page"), Icon: "server", Meter: { Max: 100, Levels: false },
            Href: "services.html", Title: SmShared.Lines(_TF("The managed services that are running.", "Tool tip of the running services tile of the server page"), "", showDetails),
        });
        const cpu = Sw.Kpi({ Label: _TF("CPU", "Label of the cpu use tile of the server page"), Icon: "cpu", Unit: "%", Meter: { Max: 100, Warn: 0.8, Danger: 0.92 }, Href: SmShared.MetricsUrl("GetCpuChart", cpuTitle), Title: SmShared.Lines(cpuTitle, "", showDetails) });
        const mem = Sw.Kpi({ Label: _TF("Memory", "Label of the memory use tile of the server page"), Icon: "memory", Unit: "%", Meter: { Max: 100, Warn: 0.8, Danger: 0.92 }, Href: SmShared.MetricsUrl("GetMemChart", memTitle), Title: SmShared.Lines(memTitle, "", showDetails) });
        kpis.Add(svc, cpu, mem);
        const drives = [];
        for (let i = 0; i < info.DriveCount; ++i) {
            const k = Sw.Kpi({ Label: _TF("Drive", "Label of a drive use tile of the server page until the drive name is known"), Icon: "drive", Unit: "%", Meter: { Max: 100, Warn: 0.8, Danger: 0.92 }, Href: SmShared.MetricsUrl("GetDriveChart?" + i, discTitle), Title: SmShared.Lines(discTitle, "", showDetails) });
            drives.push(k);
            kpis.Add(k);
        }
        page.Add(facts, kpis);

        //  Live values: the same requests as the original page (its chart tiles): GetServicesChart, GetCpuChart and GetMemChart (every 2 s,
        //  the RefreshRate of the reply), GetDriveChart?<i> (every 10 s), GetServerStats (every 5 s)
        const setUse = (k, pct, sub) => {
            k.Set({ Value: smPercent(pct), MeterValue: pct });
            smKpiSub(k, sub);
        };
        const values = c => {
            const d = c && c.data && c.data.datasets && c.data.datasets[0] && c.data.datasets[0].data;
            return Array.isArray(d) ? d.map(x => +x || 0) : null;
        };
        const gb = 1024 * 1024 * 1024;
        const polls = [
            smPollChart("GetServicesChart", c => {
                const v = values(c);
                if (!v || v.length < 2)
                    return;
                const tot = v[0] + v[1];
                svc.Set({ Value: Sw.Format.Number(v[0], 0) + " / " + Sw.Format.Number(tot, 0), MeterValue: tot > 0 ? v[0] / tot * 100 : 0 });
                svc.Set({ Sub: tot > 0 ? _T("{0} stopped", Sw.Format.Number(v[1], 0), "Context line of the running services tile of the server page, {0} is the number of stopped services") : _TF("No managed services", "Context line of the running services tile of the server page when there are no managed services") });
            }),
            smPollChart("GetCpuChart", c => {
                const v = values(c);
                if (v && v.length)
                    setUse(cpu, v[0], _TF("Of all processors", "Context line of the cpu tile of the server page"));
            }),
            smPollChart("GetMemChart", c => {
                const d = smUsedFree(c);
                if (!d)
                    return;
                const tot = d.Used + d.Free;
                setUse(mem, tot > 0 ? d.Used / tot * 100 : NaN, _T("{0} of {1}", Sw.Format.Bytes(d.Used * gb, 2), Sw.Format.Bytes(tot * gb, 2), "Context line of the memory tile of the server page, {0} is the used and {1} the total memory"));
            }),
        ];
        drives.forEach((k, i) => polls.push(smPollChart("GetDriveChart?" + i, c => {
            const d = smUsedFree(c);
            if (!d)
                return;
            const label = k.E.querySelector(".sw-kpi-label > span");
            const name = d.Title ? _T("Drive {0}", d.Title.trim(), "Label of a drive use tile of the server page, {0} is the drive name and label") : "";
            if (name && label.textContent !== name)
                label.textContent = name;
            const tot = d.Used + d.Free;
            setUse(k, tot > 0 ? d.Used / tot * 100 : NaN, _T("{0} of {1} used", Sw.Format.Bytes(d.Used * gb, 0), Sw.Format.Bytes(tot * gb, 0), "Context line of a drive tile of the server page, {0} is the used and {1} the total size"));
        })));
        //  The process count and the clock sync: GetServerStats every 5 s (after the reply), the first 5 s after the page is shown
        const pollStats = async () => {
            try {
                const at = Date.now();
                const s = await Sw.Net.Get("GetServerStats", { NoNavigate: true });
                if (s) {
                    clock.Sync(s.Time, at);
                    procCount = String(s.ProcessCount);
                    procFact.Set(procCount);
                }
            }
            catch {
                //  A live poll: the facts keep their last value and the next poll retries
            }
            setTimeout(pollStats, 5000);
        };
        await Promise.all(polls);
        setTimeout(pollStats, 5000);
    }, { Loading: true });
}

/**
 * The Reboot command: confirm (the shared danger confirmation, Sw.Cells.ConfirmDanger: the question names the server, "Cancel" is
 * focused, the danger button says "Reboot <server>"), then ask the server to reboot.
 * @param {string} [host] - The server (machine) name shown in the dialog (GetServerInfo Machine).
 * @param {string} [processes] - The number of running processes (shown in the dialog), optional.
 * @param {?{RebootToken: string}} [info] - The GetServerInfo reply of the page (its reboot token is used while it is fresh), optional.
 * @param {number} [infoAt] - Local time (ms) when that reply was asked for.
 * @returns {Promise<void>}
 */
async function smReboot(host, processes, info, infoAt) {
    host = typeof host === "string" && host ? host : _TF("this server", "Fallback name of the server in the reboot confirmation when its name is unknown");
    const ok = await Sw.Cells.ConfirmDanger({
        Title: _T("Reboot {0}?", host, "Title of the dialog asking to reboot the server, {0} is the server (machine) name"),
        Item: host,
        ItemLabel: _TF("Server:", "Label before the server name in the reboot confirmation dialog"),
        Text: (processes ? _T("All {0} running programs and services on this server stop, and data that is not saved is lost.", processes, "Text of the reboot confirmation dialog, {0} is the number of running processes") : _TF("All running programs and services on this server stop, and data that is not saved is lost.", "Text of the reboot confirmation dialog when the number of processes is unknown")) + " " +
            _TF("The server and this page are unavailable until it has started again (usually a few minutes).", "Text of the reboot confirmation dialog"),
        Warning: "",
        OkText: _T("Reboot {0}", host, "Text of the danger button that confirms a server reboot, {0} is the server (machine) name"),
        OkIcon: "../icons/power.svg",
        CancelTitle: _TF("Close this dialog, the server keeps running", "Tool tip of the Cancel button of the reboot confirmation dialog"),
    });
    if (!ok)
        return;
    //  The server requires a reboot token (only sent by GetServerInfo, valid for at least 15 minutes), so a plain link can't reboot the
    //  server. The token of the page's own GetServerInfo is used while it is younger than 10 minutes (one request, RebootComputer, as the
    //  original page); an older page asks for a new one first.
    if (!info || !info.RebootToken || !(Date.now() - (infoAt || 0) < 10 * 60 * 1000))
        info = await Sw.Net.Get("GetServerInfo", { Reload: true, NoNavigate: true });
    const token = info && info.RebootToken;
    if (token && await Sw.Net.Get("RebootComputer?" + encodeURIComponent(JSON.stringify(token)), { Reload: true }))
        Sw.Info(_TF("Rebooting the server", "Message shown when the server reboot was started"));
    else
        Sw.Fail(_TF("Failed to reboot the server", "Message shown when the server reboot failed"));
}

/**
 * Entry point of server_metrics.html: the current value chart and two history charts of one metric, in the Design v2 anatomy
 * (page header with the title from the link, three cards). Clicking a chart opens it full page in ../chart/chart.html (with its menu).
 * @returns {Promise<void>}
 */
function serverMetricsMain() {
    return Sw.Page.Main(async () => {
        const p = Sw.Url.Params();
        const q1 = p.get("q1");
        const q2 = p.get("q2");
        const q3 = p.get("q3");
        const title = p.get("title");
        //  Untrusted url text: plain, at most 80 characters, shown by the shell as plain text (Sw.Page.SetLinkTitle)
        Sw.Page.SetLinkTitle(title);
        const page = Sw.PageLayout({
            Main: ".sm-metrics",
            Type: "dash",
            Title: Sw.Page.TitleFromLink || _TF("Metrics", "Title of the server metrics page without a title in the link"),
            MenuTitle: false,
            Description: _TF("The current value, the last minutes and the long term history. Select a chart to open it with its menu (type, colours, export).", "Description of the server metrics page"),
        });
        //  As the original page: every chart named in the link is shown, also when one or two are missing; only a link without any is an error
        if (!q1 && !q2 && !q3) {
            page.Add(Sw.StateView({
                Kind: "error",
                Title: _TF("Missing chart parameters", "Title of the error on the metrics page when the url lacks parameters"),
                Text: _TF("Open this page from the Server page (the url needs q1, q2 and q3).", "Text of the error on the metrics page when the url lacks parameters"),
            }));
            return;
        }
        const tip = _TF("Open this chart", "Tool tip of a chart that opens it full page");
        const card = (t, api, extra, full, cls) => Sw.Card({
            Label: t, Span: 12, Flush: true, Class: "sm-metric " + cls,
            Body: SmShared.ChartTile({ Api: api, Extra: extra, Title: tip, OnClick: () => Sw.Page.Navigate(SmShared.ChartUrl(api, full), "_self") }),
        });
        page.Add(Sw.Grid({
            Content: [
                q1 ? card(_TF("Now", "Title of the current value chart on the server metrics page"), q1, "", "&center=true", "sm-metric-now") : null,
                q2 ? card(_TF("Recent", "Title of the short history chart on the server metrics page"), q2, "&type=line&aspect=false&legend=false", "&aspect=false&noLabels=true", "sm-metric-hist") : null,
                q3 ? card(_TF("History", "Title of the long history chart on the server metrics page"), q3, "&type=line&aspect=false&legend=false", "&aspect=false&noLabels=true", "sm-metric-hist") : null,
            ].filter(x => x),
        }));
    }, { Loading: true });
}
