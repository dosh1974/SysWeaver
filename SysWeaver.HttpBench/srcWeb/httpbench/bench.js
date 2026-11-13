/**
 * @file httpbench/bench.js - the HTTP bench page (bench.html, entry `benchMain`; menu item Debug / HTTP bench of HttpBenchService).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js and ../explore/table.js; started from `<body onload>`.
 *
 * A Design v2 dashboard page (Sw.PageLayout "dash"):
 *  - the page header with the actions Import / Export (the form as a json parameter file with "DataType": "HttpBench",
 *    the format the HttpBench console tool reads), GO (primary: start a test) and STOP (danger, confirmed, only while a test runs),
 *  - the test form: target (server url, accept any certificate), load (duration slider 10 s to 120 min, workers), compression
 *    (api values as POST bodies or GET query strings, their compression, responses: Accept-Encoding) and the end points, grouped in modules (a switch per module and a
 *    chip per end point),
 *  - the test: while it runs a progress bar, live KPI tiles and live charts (GetStatus every 500 ms, the charts refresh themselves);
 *    when it is done the report: facts, KPI tiles and tabs with the charts (chart/chart.html frames, with their menus: type, colours,
 *    export) and the tables (Sw.Table: sorting, filters, search, export) from the totals down to single requests,
 *  - the kept tests (a table, a row opens its report: bench.html?run=<id>).
 *
 * Server api (Api/httpbench/, HttpBenchService): GetOptions, Start, Stop, GetStatus, the tables (Param = the test id) RunsTable,
 * EndPointTable, ModuleTable, SecondTable, SecondEndPointTable, StatusTable, ErrorTable, WorkerTable, RequestTable, HistogramTable
 * and the charts (GET ?<id>) ThroughputChart, LatencyChart, BandwidthChart, EndPointLatencyChart, EndPointRpsChart, EndPointSizeChart,
 * HistogramChart, ModuleChart, StatusChart.
 *
 * Url parameters: run (the id of a test to show, default the running or the latest test).
 *
 * Globals defined: `benchMain`.
 */
"use strict";

/** The api folder of the bench service (relative to this page and to chart/chart.html). */
const HbApi = "../Api/httpbench/";

/**
 * A duration in seconds as text: "45 s", "2 min 30 s", "1 h 5 min".
 * @param {number} s - Seconds.
 * @returns {string} The text.
 */
function hbDuration(s) {
    s = Math.round(s);
    if (s < 60)
        return _T("{0} s", s, "A duration in seconds, ex: 45 s");
    if (s < 3600) {
        const m = Math.floor(s / 60);
        const r = s % 60;
        return r ? _T("{0} min {1} s", m, r, "A duration in minutes and seconds, ex: 2 min 30 s") : _T("{0} min", m, "A duration in whole minutes, ex: 5 min");
    }
    const h = Math.floor(s / 3600);
    const m = Math.round((s % 3600) / 60);
    return m ? _T("{0} h {1} min", h, m, "A duration in hours and minutes, ex: 1 h 5 min") : _T("{0} h", h, "A duration in whole hours, ex: 2 h");
}

/**
 * A time in ms with a precision that fits its size: "0.152", "3.41", "27.5", "312".
 * @param {number} ms - Milliseconds.
 * @returns {string} The number text (no unit).
 */
function hbMs(ms) {
    const d = ms >= 100 ? 0 : ms >= 10 ? 1 : ms >= 1 ? 2 : 3;
    return Sw.Format.Number(ms || 0, d);
}

/**
 * A number with a fixed number of decimals and thousand separators.
 * @param {number} v - The value.
 * @param {number} [d=0] - Decimals.
 * @returns {string} The text.
 */
function hbNum(v, d) {
    return Sw.Format.Number(v || 0, d || 0);
}

/** The localStorage key of the recently tested servers (a json array of urls, the latest first). */
const HbRecentKey = "SysWeaver.HttpBench.Servers";

/** The most recently tested servers that are kept. */
const HbRecentMax = 20;

/** The localStorage key of the form settings (a BenchStartRequest: the page starts up as it was left). */
const HbSettingsKey = "SysWeaver.HttpBench.Settings";

/** The localStorage key of the selected tab of the report. */
const HbTabKey = "SysWeaver.HttpBench.Tab";

/**
 * The form settings kept in this browser.
 * @returns {?object} The settings (BenchStartRequest), null if none.
 */
function hbLoadSettings() {
    const v = Sw.Storage.Local.GetJson(HbSettingsKey, null);
    return v && typeof v === "object" && !Array.isArray(v) ? v : null;
}

/**
 * The server address of a url: the origin and the path to the folder, no query string or fragment (the root the end points are
 * relative to, as the server makes it).
 * @param {string} text - An url.
 * @returns {?string} The address, null for an invalid url.
 */
function hbServerAddress(text) {
    try {
        const u = new URL((text || "").trim());
        if ((u.protocol !== "http:") && (u.protocol !== "https:"))
            return null;
        const path = u.pathname.endsWith("/") ? u.pathname : u.pathname + "/";
        return u.origin + path;
    }
    catch {
        return null;
    }
}

/**
 * The recently tested servers, the latest first.
 * @returns {string[]} The addresses.
 */
function hbRecentServers() {
    const v = Sw.Storage.Local.GetJson(HbRecentKey, []);
    return Array.isArray(v) ? v.filter(x => typeof x === "string" && x).slice(0, HbRecentMax) : [];
}

/**
 * Remember a tested server (first in the list, at most HbRecentMax).
 * @param {string} url - The url of the server.
 */
function hbRememberServer(url) {
    const a = hbServerAddress(url);
    if (!a)
        return;
    const list = [a, ...hbRecentServers().filter(x => x !== a)].slice(0, HbRecentMax);
    Sw.Storage.Local.SetJson(HbRecentKey, list);
}

/**
 * A url of the chart page for a chart api of the bench service.
 * @param {string} api - The chart api (ex: "ThroughputChart").
 * @param {number} id - The test id.
 * @param {string} [extra] - More url parameters ("&legend=false").
 * @returns {string} The url.
 */
function hbChartUrl(api, id, extra) {
    return Sw.Url.AppendTheme("../chart/chart.html?q=" + encodeURIComponent(HbApi + api + "?" + id) + "&timeoffset=local" + (extra || ""));
}

/**
 * A card with a live chart (an iframe of the chart page, with its menu: type, colours, export) and a link that opens it in a new tab.
 * @param {object} o - Options.
 * @param {string} o.Title - Card title (the chart's own title is hidden).
 * @param {string} [o.Sub] - Card sub title.
 * @param {string} o.Api - The chart api.
 * @param {number} o.Id - The test id.
 * @param {number} [o.Span=12] - Grid span (3, 4, 6, 8, 12).
 * @param {string} [o.Height] - Css height of the chart (default from bench.css).
 * @param {string} [o.Extra] - More url parameters of the chart page.
 * @param {boolean} [o.Live] - A chart of the live view (its collapsed state is kept apart from the report chart of the same api).
 * @returns {object} The card (Sw.Card).
 */
function hbChartCard(o) {
    const url = hbChartUrl(o.Api, o.Id, "&title=false" + (o.Extra || ""));
    const f = Sw.CreateIFrame();
    f.className = "hb-chart-frame";
    f.title = o.Title;
    f.src = url;
    const box = Sw.el("div.hb-chart", null, f);
    if (o.Height)
        box.style.height = o.Height;
    //  A real link (middle click, copy link), styled as a small icon button by bench.css
    const open = Sw.el("a.hb-open", {
        href: hbChartUrl(o.Api, o.Id),
        target: "_blank",
        rel: "noopener",
        title: _TF("Open this chart in a new tab", "Tool tip of the link that opens a chart of the test report full size in a new tab"),
        attrs: { "aria-label": _T("Open the chart {0} in a new tab", o.Title, "Accessible name of the link that opens a chart in a new tab, {0} is the chart title") },
    }, Sw.Icon("external"));
    return Sw.Card({ Title: o.Title, Sub: o.Sub, Span: o.Span || 12, Flush: true, Tools: [open], Body: box, Class: "hb-chart-card", Collapsible: true, StateKey: "chart." + o.Api + (o.Live ? ".live" : "") });
}

/**
 * Entry point of bench.html.
 * @returns {Promise<void>}
 */
function benchMain() {
    return Sw.Page.Main(async () => {
        const opts = await Sw.Net.Send(HbApi + "GetOptions", null);
        //  The form as it was left in this browser, else the settings of the latest test of the server, else the defaults
        const last = hbLoadSettings() || opts.LatestSettings;
        /** @type {?function():void} Saves the form (set when every control exists). */
        let saveSettings = null;
        const save = () => saveSettings && saveSettings();
        const running = _TF("A test is running: stop it or wait for it to end", "Reason shown on the controls of the test form while a test runs");

        //  ===== Target =====
        const rootUrl = new URL("../", window.location.href).href;
        const target = new Sw.TextBox({
            Label: _TF("Server url", "Label of the text box with the root url of the server to performance test"),
            Hint: _TF("The root of the SysWeaver server to test. Testing this server is fine, but the workers then take cpu time from it.", "Hint under the server url box of the http bench page"),
            Value: (last && last.Target) || rootUrl,
            Required: _TF("Enter the url of the server to test", "Message under the empty server url box of the http bench page"),
            Validate: v => {
                try {
                    const u = new URL((v || "").trim());
                    if ((u.protocol === "http:") || (u.protocol === "https:"))
                        return null;
                }
                catch {
                }
                return _TF("Enter an http or https url, ex: https://myserver/", "Message under the server url box when the text is not an http url");
            },
            Messages: [_TF("Enter an http or https url, ex: https://myserver/", "Message under the server url box when the text is not an http url")],
            InputMode: "url",
            SpellCheck: false,
            Mono: true,
            Clear: true,
            OnInput: save,
            OnChange: save,
        });
        const thisServer = new Sw.Button({
            Text: _TF("This server", "Button that puts the url of the server that shows the page in the server url box"),
            Title: _T("Test this server: {0}", rootUrl, "Tool tip of the button that sets the target to the server of the page, {0} is its url"),
            Small: true,
            Saves: false,
            OnClick: () => {
                target.Set(rootUrl);
                save();
            },
        });
        //  The recently tested servers (localStorage, the latest first): a menu that puts one in the url box
        const recentItems = () => {
            const list = hbRecentServers();
            if (list.length <= 0)
                return [{ Text: _TF("No servers tested yet", "Menu item shown when no server has been tested from this browser"), Disabled: true }];
            return [
                { Title: _TF("Recently tested", "Heading of the menu with the recently tested servers") },
                ...list.map(x => ({ Text: x, Icon: "history", Checked: hbServerAddress(target.Get()) === x, OnClick: () => { target.Set(x); save(); } })),
                "-",
                {
                    Text: _TF("Clear the list", "Menu item that forgets the recently tested servers"),
                    Icon: "trash",
                    OnClick: () => Sw.Storage.Local.Remove(HbRecentKey),
                },
            ];
        };
        const recent = new Sw.Button({
            Text: _TF("Recent servers", "Button that opens a menu with the recently tested servers"),
            Icon: "history",
            Title: _T("Pick one of the last {0} tested servers (kept in this browser)", HbRecentMax, "Tool tip of the recent servers button"),
            Small: true,
            Saves: false,
            Menu: recentItems,
        });
        const anyCert = new Sw.Toggle({
            Label: _TF("Accept any certificate", "Switch on the http bench page that makes the workers accept self signed and expired https certificates"),
            Title: _TF("Accept self signed, LAN and expired certificates of test servers", "Tool tip of the accept any certificate switch"),
            Value: last ? !!last.IgnoreCertificateErrors : true,
            OnChange: save,
        });

        //  ===== Load =====
        const durText = Sw.el("span.hb-value", { attrs: { "aria-live": "polite" } });
        const duration = new Sw.Slider({
            Min: opts.MinDurationSeconds,
            Max: opts.MaxDurationSeconds,
            Log: true,
            Value: Math.min(opts.MaxDurationSeconds, Math.max(opts.MinDurationSeconds, (last && last.DurationSeconds) || 60)),
            Default: 60,
            AriaLabel: _TF("Duration of the test", "Accessible name of the duration slider of the http bench page"),
            OnInput: v => durText.textContent = hbDuration(v),
            OnChange: v => {
                durText.textContent = hbDuration(v);
                save();
            },
        });
        durText.textContent = hbDuration(duration.Get());
        const durField = new Sw.Field({
            Label: _TF("Duration", "Label of the slider that sets how long a performance test runs"),
            Hint: _T("{0} to {1}", hbDuration(opts.MinDurationSeconds), hbDuration(opts.MaxDurationSeconds), "Hint under the duration slider: the shortest and longest test"),
            Control: duration,
            Class: "hb-duration",
        });
        durField.E.append(Sw.el("div.hb-value-row", null, durText));
        const workers = new Sw.NumberBox({
            Label: _TF("Workers", "Label of the box with the number of concurrent clients of a performance test"),
            Hint: _T("Clients that each send one request at a time, with their own connection and session (1 to {0})", opts.MaxConcurrency, "Hint under the workers box of the http bench page"),
            Min: 1,
            Max: opts.MaxConcurrency,
            Step: 1,
            Value: Math.min(opts.MaxConcurrency, Math.max(1, (last && last.Concurrency) || 16)),
            Required: true,
            OnChange: save,
        });

        //  ===== Compression =====
        const items = list => list.map(x => ({ Value: x.Value, Text: x.Text, Title: x.Title }));
        const pick = (list, v, def) => list.some(x => x.Value === v) ? v : def;
        //  How the apis that take a value get it: as the POST body, or in the GET query string (HttpServer README "Calling APIs")
        const valueMethod = new Sw.Segmented({
            Items: [
                //  SwWeb:no-tr (http method names)
                { Value: "POST", Text: "POST", Title: _TF("The value is the json body of a POST request", "Tool tip of the POST choice of how api values are sent") },
                { Value: "GET", Text: "GET", Title: _TF("The value is the query string of a GET request: url encoded json, or a compressed payload (_b, _g, _d, _z + base64url)", "Tool tip of the GET choice of how api values are sent") }, // SwWeb:no-tr (http method name)
            ],
            Value: last && last.SendAsGet ? "GET" : "POST",
            AriaLabel: _TF("How api values are sent", "Accessible name of the choice between POST and GET requests for apis that take a value"),
            OnChange: save,
        });
        const methodField = new Sw.Field({
            Label: _TF("Api values as", "Label of the choice between POST and GET requests for the apis that take a value"),
            Hint: _TF("The apis that take a value (the POST end points) get it as the request body, or in the query string", "Hint under the POST / GET choice"),
            Control: valueMethod,
        });
        const reqComp = new Sw.Segmented({
            Items: items(opts.RequestCompressions),
            Value: pick(opts.RequestCompressions, last ? last.RequestCompression : "", ""),
            AriaLabel: _TF("Compression of the api values", "Accessible name of the choice of the request value compression"),
            OnChange: save,
        });
        const respComp = new Sw.Segmented({
            Items: items(opts.ResponseCompressions),
            Value: pick(opts.ResponseCompressions, last ? last.ResponseCompression : "*", "*"),
            AriaLabel: _TF("Accepted response compression", "Accessible name of the choice of the Accept-Encoding header"),
            OnChange: save,
        });
        const reqField = new Sw.Field({
            Label: _TF("Api values", "Label of the choice of how the api values the workers send are compressed"),
            Hint: _TF("POST: the Content-Encoding of the json body. GET: the compressed query string payload (None: url encoded json)", "Hint under the request value compression choice"),
            Control: reqComp,
        });
        const respField = new Sw.Field({
            Label: _TF("Responses", "Label of the choice of which response compressions the workers accept"),
            Hint: _TF("The Accept-Encoding header: the server compresses responses with one of them (All: it picks, like for a browser)", "Hint under the response compression choice"),
            Control: respComp,
        });

        //  ===== End points =====
        const chosen = last ? new Set(last.EndPoints || []) : null;
        let epTotal = 0;
        const mods = opts.Modules.map(m => {
            epTotal += m.EndPoints.length;
            const count = Sw.el("span.hb-module-count");
            let toggle = null;
            const chips = m.EndPoints.map(e => {
                const c = new Sw.Chip({
                    Text: e.Name,
                    Title: e.Method + " " + e.Path + "\n" + e.Desc,
                    Pressable: true,
                    Pressed: chosen ? chosen.has(e.Id) : !!m.Default,
                    OnChange: () => sync(),
                });
                c.Id = e.Id;
                return c;
            });
            toggle = new Sw.Toggle({
                Label: m.Name,
                Title: m.Desc,
                Value: chips.some(c => c.Get()),
                OnChange: on => {
                    for (const c of chips)
                        c.Set(on, true);
                    sync();
                },
            });
            const head = Sw.el("div.hb-module-head", null, Sw.Icon(m.Icon), toggle.E, count);
            const block = Sw.el("section.hb-module", { attrs: { "aria-label": m.Name } },
                head,
                Sw.el("p.hb-module-desc", { text: m.Desc }),
                Sw.el("div.hb-chips", { attrs: { role: "group", "aria-label": _T("End points of {0}", m.Name, "Accessible name of the group of end point chips of a module") } }, chips));
            return { Module: m, Chips: chips, Toggle: toggle, Count: count, E: block };
        });
        const selectedIds = () => mods.flatMap(x => x.Chips.filter(c => c.Get()).map(c => c.Id));
        const setAll = on => {
            for (const x of mods) {
                for (const c of x.Chips)
                    c.Set(on, true);
            }
            sync();
        };
        const allBtn = new Sw.Button({
            Text: _TF("All", "Button that selects every end point of the http bench page"),
            Title: _TF("Select every end point", "Tool tip of the select all end points button"),
            Small: true,
            Saves: false,
            OnClick: () => setAll(true),
        });
        const noneBtn = new Sw.Button({
            Text: _TF("None", "Button that clears the selection of end points of the http bench page"),
            Title: _TF("Clear the selection of end points", "Tool tip of the select no end points button"),
            Small: true,
            Saves: false,
            OnClick: () => setAll(false),
        });
        const epCard = Sw.Card({
            Title: _TF("End points", "Title of the card where the user selects which end points a performance test calls"),
            Collapsible: true,
            Span: 12,
            Tools: [allBtn, noneBtn],
            Body: [
                Sw.el("p.hb-note", { text: _TF("Every worker calls the selected end points in this order, over and over (each one starts at a different end point). Switch modules on and off, or pick single end points.", "Explanation above the end point selection of the http bench page") }),
                Sw.el("div.hb-modules", null, mods.map(x => x.E)),
            ],
        });

        /**
         * Update the counts and the module switches after a change of the selection.
         */
        function sync() {
            let n = 0;
            for (const x of mods) {
                const k = x.Chips.filter(c => c.Get()).length;
                n += k;
                x.Count.textContent = _T("{0} of {1}", k, x.Chips.length, "Count of selected end points of a module, ex: 4 of 5");
                x.Toggle.Set(k > 0, true);
            }
            epCard.SetSub(_T("{0} of {1} selected", n, epTotal, "Sub title of the end point card: the number of selected end points"));
            save();
        }

        /**
         * The settings of the form (the shape of the Start request and of an exported parameter file).
         * @returns {object} The settings (BenchStartRequest).
         */
        const formSettings = () => ({
            Target: (target.Get() || "").trim(),
            DurationSeconds: Math.round(duration.Get()),
            Concurrency: Math.round(workers.Get() || 0) || 16,
            EndPoints: selectedIds(),
            SendAsGet: valueMethod.Get() === "GET",
            RequestCompression: reqComp.Get(),
            ResponseCompression: respComp.Get(),
            IgnoreCertificateErrors: anyCert.Get(),
        });

        //  Every change of the form is kept in this browser (the shape of the Start request), so the page starts up as it was left
        saveSettings = () => Sw.Storage.Local.SetJson(HbSettingsKey, formSettings());
        sync();

        //  ===== Parameter files (export / import) =====
        /** The value of the "DataType" member that identifies a parameter file (BenchSettingsFile). */
        const dataType = opts.SettingsDataType || "HttpBench";

        /**
         * Put settings (an imported parameter file) in the form; members that are missing keep their value, except the end points
         * (missing = the modules that are on by default, as the HttpBench console tool does).
         * @param {object} v - The settings (BenchStartRequest).
         * @returns {number} The number of unknown end points that were skipped.
         */
        function applySettings(v) {
            const num = x => typeof x === "number" && Number.isFinite(x);
            if (typeof v.Target === "string" && v.Target.trim())
                target.Set(v.Target.trim(), true);
            if (typeof v.IgnoreCertificateErrors === "boolean")
                anyCert.Set(v.IgnoreCertificateErrors, true);
            if (num(v.DurationSeconds)) {
                duration.Set(Math.min(opts.MaxDurationSeconds, Math.max(opts.MinDurationSeconds, Math.round(v.DurationSeconds))), true);
                durText.textContent = hbDuration(duration.Get());
            }
            if (num(v.Concurrency))
                workers.Set(Math.min(opts.MaxConcurrency, Math.max(1, Math.round(v.Concurrency))), true);
            if (typeof v.SendAsGet === "boolean")
                valueMethod.Set(v.SendAsGet ? "GET" : "POST", true);
            if (typeof v.RequestCompression === "string" || v.RequestCompression === null)
                reqComp.Set(pick(opts.RequestCompressions, v.RequestCompression || "", reqComp.Get()), true);
            if (typeof v.ResponseCompression === "string" || v.ResponseCompression === null)
                respComp.Set(pick(opts.ResponseCompressions, v.ResponseCompression || "", respComp.Get()), true);
            let unknown = 0;
            const ids = Array.isArray(v.EndPoints) ? v.EndPoints.filter(x => typeof x === "string") : null;
            if (ids) {
                const known = new Set(mods.flatMap(x => x.Chips.map(c => c.Id)));
                unknown = ids.filter(x => !known.has(x)).length;
            }
            const set = ids ? new Set(ids) : null;
            for (const x of mods) {
                for (const c of x.Chips)
                    c.Set(set ? set.has(c.Id) : !!x.Module.Default, true);
            }
            sync();
            return unknown;
        }

        const exportBtn = new Sw.Button({
            Text: _TF("Export", "Button that saves the settings of the test form as a json file"),
            Icon: "download",
            Saves: false,
            Title: _TF("Save the settings of the form as a parameter file (json), for the HttpBench console tool or to import later", "Tool tip of the export button of the http bench page"),
            OnClick: () => {
                const s = formSettings();
                const a = hbServerAddress(s.Target);
                const host = a ? new URL(a).host : "server";
                Sw.Net.DownloadText("HttpBench_" + host.replace(/[^A-Za-z0-9.-]+/g, "_") + ".json", JSON.stringify(Object.assign({ DataType: dataType }, s), null, 2));
            },
        });
        //  A hidden file input: the import button opens it, the file is read here (nothing is sent to the server)
        const importInput = Sw.el("input", { attrs: { type: "file", accept: ".json,application/json", hidden: "" } });
        importInput.addEventListener("change", async () => {
            const f = importInput.files && importInput.files[0];
            importInput.value = "";
            if (!f)
                return;
            try {
                let v = null;
                try {
                    v = JSON.parse(await f.text());
                }
                catch {
                }
                if (!v || typeof v !== "object" || Array.isArray(v) || v.DataType !== dataType)
                    throw new Error(_T("{0} is not an HTTP bench parameter file (no \"DataType\": \"{1}\")", f.name, dataType, "Error when an imported file is not a parameter file of the http bench page, {0} is the file name"));
                const unknown = applySettings(v);
                Sw.Toast(unknown > 0
                    ? _T("Imported {0}, {1} unknown end points were skipped", f.name, unknown, "Message after a parameter file was imported that names end points this server doesn't know")
                    : _T("Imported {0}", f.name, "Message after a parameter file was imported into the http bench form"), { Kind: unknown > 0 ? "warning" : "info" });
            }
            catch (e) {
                Sw.Fail(e);
            }
        });
        const importBtn = new Sw.Button({
            Text: _TF("Import", "Button that loads the settings of the test form from a json file"),
            Icon: "upload",
            Saves: false,
            Title: _TF("Load the settings of the form from a parameter file (json) that was exported before", "Tool tip of the import button of the http bench page"),
            OnClick: () => importInput.click(),
        });

        //  ===== Actions =====
        let current = 0;
        const goBtn = new Sw.Button({
            Text: _TF("GO", "Button that starts a performance test"),
            Icon: "play",
            Kind: "primary",
            Saves: false,
            Title: _TF("Start the test: the workers call the selected end points until the time is up", "Tool tip of the button that starts a performance test"),
            OnClick: async () => {
                if (target.Validate()) {
                    target.Focus();
                    throw new Error(target.Validate());
                }
                const eps = selectedIds();
                if (eps.length <= 0)
                    throw new Error(_TF("Select at least one end point", "Reason why a performance test can't start"));
                if (workers.Validate()) {
                    workers.Focus();
                    throw new Error(workers.Validate());
                }
                const id = await Sw.Net.Send(HbApi + "Start", Object.assign(formSettings(), { Concurrency: Math.round(workers.Get()), EndPoints: eps }));
                hbRememberServer(target.Get());
                //  The test is shown and polled with detached requests (not awaited): the button is only busy while the test starts
                showRun(id).catch(e => Sw.Fail(e));
            },
        });
        const stopBtn = new Sw.Button({
            Text: _TF("STOP", "Button that stops the running performance test"),
            Icon: "stop",
            Kind: "danger",
            Hidden: true,
            Saves: false,
            Title: _TF("Stop the test now (the report shows what was measured so far)", "Tool tip of the button that stops a performance test"),
            OnClick: async () => {
                const ok = await Sw.Dialog.Confirm(
                    _T("Stop test {0}?", current, "Title of the confirmation before a performance test is stopped, {0} is the test number"),
                    _TF("The workers stop at once; requests in flight are not counted. The report shows what was measured so far.", "Text of the confirmation before a performance test is stopped"),
                    _TF("Stop test", "Confirm button that stops a performance test"),
                    { Danger: true });
                if (!ok)
                    return;
                await Sw.Net.Send(HbApi + "Stop", null);
            },
        });

        const formControls = [target, thisServer, recent, anyCert, duration, workers, valueMethod, reqComp, respComp, allBtn, noneBtn, importBtn, ...mods.flatMap(x => [x.Toggle, ...x.Chips])];
        /**
         * Lock or unlock the test form (while a test runs).
         * @param {boolean} on - True while a test runs.
         */
        function setRunning(on) {
            for (const c of formControls)
                c.SetDisabled(on, on ? running : undefined);
            goBtn.SetDisabled(on, on ? running : undefined);
            stopBtn.E.hidden = !on;
            page.Header.FitActions && page.Header.FitActions();
        }

        //  ===== Page =====
        const page = Sw.PageLayout({
            Main: ".hb-page",
            Type: "dash",
            Title: _TF("HTTP bench", "Title of the page that performance tests a web server"),
            Description: _TF("Performance test a SysWeaver server: workers call its public end points for a while, then the report shows the results from the totals down to single requests.", "Description of the http bench page"),
            Actions: [importBtn, exportBtn, stopBtn, goBtn],
        });
        document.body.append(importInput);
        const runBox = Sw.el("div.hb-run");
        const runsBox = Sw.el("div.hb-runs");
        page.Add(
            Sw.Grid({
                Content: [
                    Sw.Card({
                        Title: _TF("Target", "Title of the card with the server to test"),
                        Collapsible: true,
                        Span: 6,
                        Body: [target, Sw.el("div.sw-row.sw-start", null, thisServer.E, recent.E), anyCert],
                    }),
                    Sw.Card({
                        Title: _TF("Load", "Title of the card with the duration and the number of workers of a test"),
                        Collapsible: true,
                        Span: 6,
                        Body: [durField, workers],
                    }),
                    Sw.Card({
                        Title: _TF("Requests", "Title of the card with how the requests of a test are sent: method and compression"),
                        Collapsible: true,
                        Span: 12,
                        Body: [Sw.el("div.hb-comp", null, methodField.E, reqField.E, respField.E)],
                    }),
                    epCard,
                ],
            }),
            runBox,
            Sw.Grid({
                Content: [Sw.Card({
                    Title: _TF("Tests", "Title of the card with the list of kept performance tests"),
                    Collapsible: true,
                    Sub: _TF("The latest tests (kept in memory until the service restarts). Select a test to show its report.", "Sub title of the list of performance tests"),
                    Span: 12,
                    Flush: true,
                    Body: runsBox,
                })],
            }));
        let runsTable = null;

        //  ===== A test =====
        let showToken = 0;

        /**
         * Show a test: live while it runs, the report when it is done.
         * @param {number} id - The test id.
         * @returns {Promise<void>}
         */
        async function showRun(id) {
            const token = ++showToken;
            current = id;
            //  Detached: a view of the test, not the work of the GO button that started it
            const s = await Sw.Net.Send(HbApi + "GetStatus", id, { Detached: true });
            if (token !== showToken)
                return;
            if (runsTable)
                runsTable.UpdateNow();
            if (s.Done) {
                setRunning(false);
                buildReport(s);
                return;
            }
            setRunning(true);
            const live = buildLive(s);
            //  Poll the status until the test is done, then show the report
            for (; ;) {
                await new Promise(r => setTimeout(r, 500));
                if (token !== showToken)
                    return;
                let st = null;
                try {
                    st = await Sw.Net.Send(HbApi + "GetStatus", id, { Detached: true });
                }
                catch (e) {
                    if (token !== showToken)
                        return;
                    if (e && (e.Status === 404 || e.Status === 410)) {
                        setRunning(false);
                        runBox.replaceChildren(Sw.StateView({ Kind: "error", Title: _TF("The test is gone", "Title shown when a running test can't be found any more"), Text: e.message }).E);
                        return;
                    }
                    continue;
                }
                if (token !== showToken)
                    return;
                if (st.Done) {
                    setRunning(false);
                    if (runsTable)
                        runsTable.UpdateNow();
                    buildReport(st);
                    return;
                }
                live(st);
            }
        }

        /**
         * Build the live view of a running test.
         * @param {object} s - The status (BenchStatus).
         * @returns {function(object):void} Updates the view with a new status.
         */
        function buildLive(s) {
            const progress = new Sw.ProgressBar({ AriaLabel: _TF("Progress of the test", "Accessible name of the progress bar of a running performance test") });
            const kRps = Sw.Kpi({ Label: _TF("Requests/s", "KPI tile: requests completed in the last second"), Icon: "speed", Span: 3 });
            const kAvg = Sw.Kpi({ Label: _TF("Response time", "KPI tile: the average response time of the last second"), Icon: "clock", Unit: "ms", Span: 3 });
            const kReq = Sw.Kpi({ Label: _TF("Requests", "KPI tile: requests completed so far"), Icon: "send", Span: 3 });
            const kErr = Sw.Kpi({ Label: _TF("Errors", "KPI tile: requests that failed so far"), Icon: "alert", Span: 3 });
            const card = Sw.Card({
                Title: _T("Test {0} is running", s.Id, "Title of the card of a running performance test, {0} is the test number"),
                Sub: s.Target,
                Span: 12,
                Body: [progress],
            });
            runBox.replaceChildren(
                Sw.SectionTitle(_T("Test {0}", s.Id, "Section title of a performance test, {0} is the test number")),
                Sw.Grid({ Content: [card, kRps, kAvg, kReq, kErr] }).E,
                Sw.Grid({
                    Content: [
                        hbChartCard({ Title: _TF("Requests per second", "Title of the live throughput chart of a running test"), Api: "ThroughputChart", Id: s.Id, Span: 6, Live: true }),
                        hbChartCard({ Title: _TF("Response time", "Title of the live response time chart of a running test"), Api: "LatencyChart", Id: s.Id, Span: 6, Live: true }),
                    ],
                }).E);
            const update = st => {
                const left = Math.max(0, st.DurationSeconds - st.ElapsedSeconds);
                const text = st.State === 1
                    ? _TF("Finishing: the last requests are completing", "Progress text of a performance test whose time is up")
                    : _T("{0} of {1}, {2} left", hbDuration(st.ElapsedSeconds), hbDuration(st.DurationSeconds), hbDuration(left), "Progress text of a running performance test, ex: 1 min 2 s of 5 min, 3 min 58 s left");
                progress.SetValue(Math.min(st.ElapsedSeconds, st.DurationSeconds), st.DurationSeconds, text);
                kRps.Set({ Value: hbNum(st.CurrentRps), Sub: _T("Average {0}, peak {1}", hbNum(st.Rps, 1), hbNum(st.PeakRps), "Sub line of the requests/s tile of a running test") });
                kAvg.Set({ Value: hbMs(st.CurrentAvgMs), Sub: _T("Average {0} ms, P95 {1} ms", hbMs(st.AvgMs), hbMs(st.P95Ms), "Sub line of the response time tile of a running test") });
                kReq.Set({ Value: hbNum(st.Requests), Sub: _T("{0} received", Sw.Format.Bytes(st.WireRecv), "Sub line of the requests tile: bytes received so far") });
                kErr.Set({ Value: hbNum(st.Errors), Sub: _T("{0} %", hbNum(st.ErrorPercent, 2), "Sub line of the errors tile: the share of failed requests") });
            };
            update(s);
            return update;
        }

        /**
         * Build the report of a finished test.
         * @param {object} s - The status (BenchStatus).
         */
        function buildReport(s) {
            const id = s.Id;
            const states = [
                [_TF("Running", "State of a performance test"), "info"],
                [_TF("Finishing", "State of a performance test whose time is up"), "info"],
                [_TF("Completed", "State of a performance test that ran for its whole duration"), "ok"],
                [_TF("Stopped", "State of a performance test that a user stopped"), "warning"],
                [_TF("Failed", "State of a performance test that could not run"), "danger"],
            ];
            const st = states[s.State] || states[2];
            const startedAt = Sw.Format.TimeStamp(s.Started);
            const facts = Sw.Facts({
                Card: false,
                Items: [
                    { Label: _TF("Target", "Fact of a test report: the tested server"), Value: s.Target, Mono: true, Copy: true },
                    { Label: _TF("Started", "Fact of a test report: when it started"), Value: startedAt[1], Title: startedAt[0] },
                    { Label: _TF("Duration", "Fact of a test report: how long it ran"), Value: hbDuration(s.ElapsedSeconds), Title: _T("Planned: {0}", hbDuration(s.DurationSeconds), "Tool tip of the duration fact: the planned duration") },
                    { Label: _TF("Workers", "Fact of a test report: the number of concurrent clients"), Value: hbNum(s.Concurrency) },
                    { Label: _TF("End points", "Fact of a test report: the number of end points called"), Value: hbNum(s.EndPointCount) },
                    { Label: _TF("Api values", "Fact of a test report: how api values were sent (POST or GET) and their compression"), Value: (s.ValueMethod || "POST") + ", " + (s.RequestCompression || _TF("no compression", "No compression of the api values")) },
                    { Label: _TF("Accept-Encoding", "Fact of a test report: the accepted response compressions"), Value: s.ResponseCompression || _TF("None", "No compression") },
                    { Label: _TF("Connections", "Fact of a test report: the connections the workers opened"), Value: hbNum(s.Connections) },
                ],
            });
            const head = Sw.Card({
                Title: _T("Test {0}", id, "Title of the report card of a performance test, {0} is the test number"),
                Collapsible: true,
                StateKey: "report",
                Sub: s.Message || null,
                Span: 12,
                Tools: [Sw.Badge({ Text: st[0], Kind: st[1] })],
                Body: [facts],
            });
            const secs = Math.max(0.001, Math.min(s.ElapsedSeconds, s.DurationSeconds));
            const kpis = [
                Sw.Kpi({ Label: _TF("Requests", "KPI tile of a test report: completed requests"), Icon: "send", Value: hbNum(s.Requests), Sub: _T("{0} end points × {1} workers", s.EndPointCount, s.Concurrency, "Sub line of the requests tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("Requests/s", "KPI tile of a test report: the average throughput"), Icon: "speed", Value: hbNum(s.Rps, 1), Sub: _T("Peak {0} in one second", hbNum(s.PeakRps), "Sub line of the requests/s tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("Average response", "KPI tile of a test report: the average response time"), Icon: "clock", Value: hbMs(s.AvgMs), Unit: "ms", Sub: _T("Median {0} ms, min {1} ms", hbMs(s.P50Ms), hbMs(s.MinMs), "Sub line of the average response tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("P95 / P99", "KPI tile of a test report: the 95th and 99th percentile of the response times"), Icon: "gauge", Value: hbMs(s.P95Ms) + " / " + hbMs(s.P99Ms), Unit: "ms", Sub: _T("Max {0} ms", hbMs(s.MaxMs), "Sub line of the percentile tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("Errors", "KPI tile of a test report: failed requests"), Icon: "alert", Value: hbNum(s.Errors), Sub: _T("{0} % of the requests", hbNum(s.ErrorPercent, 2), "Sub line of the errors tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("Received", "KPI tile of a test report: bytes received on the wire"), Icon: "download", Value: Sw.Format.Bytes(s.WireRecv), Sub: _T("{0}/s, bodies {1}", Sw.Format.Bytes(s.WireRecv / secs), Sw.Format.Bytes(s.BodyRecv), "Sub line of the received tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("Sent", "KPI tile of a test report: bytes sent on the wire"), Icon: "upload", Value: Sw.Format.Bytes(s.WireSent), Sub: _T("{0}/s, bodies {1}", Sw.Format.Bytes(s.WireSent / secs), Sw.Format.Bytes(s.BodySent), "Sub line of the sent tile of a test report"), Span: 3 }),
                Sw.Kpi({ Label: _TF("Time to first byte", "KPI tile of a test report: the average time until the response headers"), Icon: "bolt", Value: hbMs(s.AvgTtfbMs), Unit: "ms", Sub: _TF("Average, until the response headers", "Sub line of the time to first byte tile of a test report"), Span: 3 }),
            ];

            //  The tab that was selected last (read before the tabs are added: adding the first one may select it)
            const keptTab = Sw.Storage.Local.Get(HbTabKey, null);
            //  Tabs: the charts, then the tables from the totals down to single requests (built when first shown)
            const tabs = new Sw.Tabs({ Label: _TF("Report", "Accessible name of the tabs of a test report"), Fill: false, OnSelect: tab => {
                Sw.Storage.Local.Set(HbTabKey, tab.Id);
                open(tab);
            } });
            const tables = [];
            const addTable = (key, title, icon, api, name, empty) => {
                const box = Sw.el("div.hb-table");
                const tab = tabs.Add({ Id: key, Title: title, Icon: icon, Content: box });
                tables.push(tab);
                tab.Data = { Box: box, Api: api, Name: name, Empty: empty, Built: false };
                return tab;
            };
            const n = s.EndPointCount;
            const tall = Math.max(16, n * 1.6 + 6) + "rem";
            const chartTab = tabs.Add({
                Id: "charts",
                Title: _TF("Charts", "Tab of a test report with the charts"),
                Icon: "chartLine",
                Content: Sw.Grid({
                    Content: [
                        hbChartCard({ Title: _TF("Requests per second", "Chart card of a test report"), Sub: _TF("Completed requests and errors per second", "Sub title of the throughput chart card"), Api: "ThroughputChart", Id: id, Span: 12 }),
                        hbChartCard({ Title: _TF("Response time", "Chart card of a test report"), Sub: _TF("Average, median, P95 and P99 per second", "Sub title of the response time chart card"), Api: "LatencyChart", Id: id, Span: 12 }),
                        hbChartCard({ Title: _TF("Network traffic", "Chart card of a test report"), Sub: _TF("MB per second on the wire", "Sub title of the network chart card"), Api: "BandwidthChart", Id: id, Span: 6 }),
                        hbChartCard({ Title: _TF("Response time distribution", "Chart card of a test report"), Sub: _TF("Requests per response time (log buckets)", "Sub title of the histogram chart card"), Api: "HistogramChart", Id: id, Span: 6 }),
                        hbChartCard({ Title: _TF("Response time per end point", "Chart card of a test report"), Sub: _TF("Average and P95", "Sub title of the end point response time chart card"), Api: "EndPointLatencyChart", Id: id, Span: 12, Height: tall }),
                        hbChartCard({ Title: _TF("Requests per second per end point", "Chart card of a test report"), Api: "EndPointRpsChart", Id: id, Span: 6, Height: tall, Extra: "&legend=false" }),
                        hbChartCard({ Title: _TF("Response size per end point", "Chart card of a test report"), Sub: _TF("Average KB per request", "Sub title of the response size chart card"), Api: "EndPointSizeChart", Id: id, Span: 6, Height: tall }),
                        hbChartCard({ Title: _TF("Requests per module", "Chart card of a test report"), Api: "ModuleChart", Id: id, Span: 6 }),
                        hbChartCard({ Title: _TF("Status codes", "Chart card of a test report"), Api: "StatusChart", Id: id, Span: 6 }),
                    ],
                }).E,
            });
            addTable("endpoints", _TF("End points", "Tab of a test report with a table row per end point"), "list", "EndPointTable", _T("End points of test {0}", id, "Export name of a table of a test report"));
            addTable("modules", _TF("Modules", "Tab of a test report with a table row per module"), "layers", "ModuleTable", _T("Modules of test {0}", id, "Export name of a table of a test report"));
            addTable("seconds", _TF("Per second", "Tab of a test report with a table row per second"), "clock", "SecondTable", _T("Seconds of test {0}", id, "Export name of a table of a test report"));
            addTable("secondeps", _TF("Per second and end point", "Tab of a test report with a table row per second and end point"), "grid", "SecondEndPointTable", _T("Seconds and end points of test {0}", id, "Export name of a table of a test report"));
            addTable("status", _TF("Status codes", "Tab of a test report with a table of the http status codes"), "check", "StatusTable", _T("Status codes of test {0}", id, "Export name of a table of a test report"));
            addTable("errors", _TF("Errors", "Tab of a test report with a table of the errors"), "alert", "ErrorTable", _T("Errors of test {0}", id, "Export name of a table of a test report"),
                [_TF("No errors", "Empty state title of the errors table of a test report"), _TF("Every request got a response with a status below 400.", "Empty state text of the errors table of a test report")]);
            addTable("workers", _TF("Workers", "Tab of a test report with a table row per worker"), "users", "WorkerTable", _T("Workers of test {0}", id, "Export name of a table of a test report"));
            addTable("requests", _TF("Requests", "Tab of a test report with a table row per request"), "rows", "RequestTable", _T("Requests of test {0}", id, "Export name of a table of a test report"));
            addTable("histogram", _TF("Response times", "Tab of a test report with the response time histogram table"), "chart", "HistogramTable", _T("Response times of test {0}", id, "Export name of a table of a test report"));

            /**
             * Build the table of a tab the first time it is shown.
             * @param {object} tab - The tab.
             */
            function open(tab) {
                const d = tab && tab.Data;
                if (!d || d.Built)
                    return;
                d.Built = true;
                const o = { Url: HbApi + d.Api, Element: d.Box, Param: String(id), Name: d.Name };
                if (d.Empty) {
                    o.EmptyTitle = d.Empty[0];
                    o.EmptyText = d.Empty[1];
                }
                //  After the tab panel is shown and laid out: the automatic page size is measured from the free height of the frame
                requestAnimationFrame(() => requestAnimationFrame(() => Sw.Table.Create(o).catch(e => Sw.Fail(e))));
            }

            runBox.replaceChildren(
                Sw.SectionTitle(_T("Test {0}", id, "Section title of a performance test, {0} is the test number")),
                Sw.Grid({ Content: [head, ...kpis] }).E,
                Sw.el("div.hb-tabs", null, tabs.E));
            //  The tab that was selected last (any report), else the charts
            tabs.Select(keptTab && tabs.Get(keptTab) ? keptTab : chartTab.Id);
        }

        //  ===== Start =====
        runsTable = await Sw.Table.Create({
            Url: HbApi + "RunsTable",
            Element: runsBox,
            Name: _TF("Tests", "Export name of the table of performance tests"),
            EmptyTitle: _TF("No tests yet", "Empty state title of the list of performance tests"),
            EmptyText: _TF("Choose the target and the end points, then press GO.", "Empty state text of the list of performance tests"),
        });
        const fromUrl = parseInt(Sw.Url.Get("run") || "0", 10) || 0;
        const show = opts.Running || fromUrl || opts.Latest;
        if (show) {
            try {
                await showRun(show);
            }
            catch (e) {
                runBox.replaceChildren(Sw.StateView({ Kind: "error", Small: true, Title: _T("Test {0} can't be shown", show, "Title of the error when a test report can't be shown"), Text: e.message }).E);
            }
        }
        else {
            runBox.replaceChildren(Sw.StateView({
                Kind: "empty",
                Icon: "speed",
                Title: _TF("No test has run yet", "Empty state title of the http bench page"),
                Text: _TF("Choose the target, the load and the end points above, then press GO. The live progress and the report show up here.", "Empty state text of the http bench page"),
            }).E);
        }
    }, { Loading: true });
}
