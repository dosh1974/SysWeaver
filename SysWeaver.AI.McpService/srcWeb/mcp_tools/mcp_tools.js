/**
 * @file mcp_tools.js - the "Admin / MCP debug" page (mcp_tools/index.html, entry `mcpToolsMain`; menu item registered by McpService,
 * auth Roles.Admin). Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js and ../explore/table.js; started from `<body onload>`.
 *
 * Behavior:
 * - While no debug server runs, the "Expose" card lists the services that can be exposed (Api/GetDebugMcpServices): per service its
 *   API's ([WebApi] methods) and AI tools, each with a check box. A service and each of its two groups have a check box that selects or
 *   clears all of it (mixed when only some are selected). A search box filters the services and methods (service, tool, method,
 *   description). "Defaults" selects the monitoring API's (the services the server marks as Default, all their API's, no AI tools),
 *   "All" and "None" select or clear everything that is shown (the search applies). The selection is kept in the local storage
 *   (key "SysWeaver.McpDebug.Selection") and the summary line says how many tools from how many services are selected.
 * - "Start debug" calls Api/StartDebugMcp with the selection (only the services with something selected): a temporary MCP server is started,
 *   its guid, url and API key are kept in the session storage (key "McpDebug") so a reload of the page keeps showing it.
 * - The connection card shows the full url of the server, the API key and the authorization header (both redacted: only the last 4
 *   characters are shown), each with a copy button that copies the real value, "Copy config" (an MCP client configuration with the url
 *   and the key) and the exposed services. The status line (Api/CheckDebugMcp: calls, errors, last error, time to expiration)
 *   is refreshed every 10 s, when the server is gone (expired or removed elsewhere) the page goes back to the stopped state.
 * - "Stop debug" calls Api/RemoveTempServer.
 * - Two live tables: the MCP call log (Api/CallLogTable) and the MCP tools (Api/ToolTable), with Param = the guid of the debug server
 *   while it runs (null = the tables of the main MCP end point).
 *
 * Globals defined: `mcpToolsMain`.
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /**
     * Api urls.
     * @param {string} name Method.
     * @returns {string} Url under Api/.
     */
    const api = name => Sw.Url.Root + "Api/" + name;
    const post = { Post: true };

    /** @type {string} The session storage key of the running debug server ({Guid, ApiKey, Url}). */
    const StoreKey = "McpDebug";

    /** @type {string} The local storage key of the selection ({ "<service key>": { W: [method], A: [method] } }). */
    const SelectionKey = "SysWeaver.McpDebug.Selection";

    /** @type {number} Status refresh interval in ms. */
    const RefreshMs = 10000;

    /**
     * The kinds of methods of a service: the property of the service info, the key of the selection, the group title.
     * @type {Array<{Prop: string, Key: string, Title: function(): string}>}
     */
    const Kinds = [
        { Prop: "WebApis", Key: "W", Title: () => _TF("API's", "Title of the group of web API methods of a service on the MCP debug page") },
        { Prop: "AiTools", Key: "A", Title: () => _TF("AI tools", "Title of the group of AI tool methods of a service on the MCP debug page") },
    ];

    /**
     * Entry point of mcp_tools/index.html.
     * @returns {Promise<void>}
     */
    function mcpToolsMain() {
        return Sw.Page.Main(async () => {
            const remove = Sw.Page.Loading();
            try {
                await build();
            }
            finally {
                remove();
            }
        });
    }

    /**
     * The key of a service in the selection.
     * @param {{TypeName: string, InstanceName: ?string}} s The service.
     * @returns {string} The key.
     */
    const serviceKey = s => s.TypeName + "|" + (s.InstanceName || "");

    /**
     * Format a counted text.
     * @param {string} text The text, {0} is replaced by the number.
     * @param {number} n The number.
     * @returns {string} The text.
     */
    const count = (text, n) => text.replace("{0}", n);

    /**
     * The text of the required auth of a method (DebugMcpMethod.Auth).
     * @param {string} auth The auth.
     * @returns {string} The text, "" for none.
     */
    function authText(auth) {
        if (!auth)
            return _TF("open", "Auth of a method that anyone may use, shown on the MCP debug page");
        if (auth === "*")
            return _TF("any user", "Auth of a method that any signed in user may use, shown on the MCP debug page");
        return auth;
    }

    /**
     * Build the page.
     * @returns {Promise<void>}
     */
    async function build() {
        /** @type {?{Guid: string, ApiKey: string, Url: string}} The running debug server. */
        let state = Sw.Storage.Session.GetJson(StoreKey, null);
        if (state && !(state.Guid && state.ApiKey))
            state = null;

        const page = Sw.PageLayout({
            Type: "dash",
            Title: _TF("MCP debug", "Title of the page where an admin starts a temporary MCP server so that an AI agent can analyze the running service"),
            Description: _TF("Start a temporary MCP server with selected API's and AI tools of this service, so that an AI agent can look for bugs and performance problems, or use the service.", "Description of the MCP debug page"),
        });

        const start = new Sw.Button({
            Text: _TF("Start debug", "Button that starts a temporary MCP server for debugging"),
            Title: _TF("Start a temporary MCP server with the selected API's and AI tools", "Tool tip of the button that starts a temporary MCP server for debugging"),
            Icon: "play",
            Kind: "primary",
            OnClick: onStart,
        });
        const stop = new Sw.Button({
            Text: _TF("Stop debug", "Button that stops the temporary MCP server used for debugging"),
            Title: _TF("Stop the temporary MCP server, the API key stops working at once", "Tool tip of the button that stops the temporary MCP server used for debugging"),
            Icon: "stop",
            Kind: "danger",
            OnClick: onStop,
        });
        page.Header.SetActions([start, stop]);

        // ------------------------------------------------------------------ selection

        /** @type {Array<object>} The services that can be exposed (DebugMcpServiceInfo). */
        let services = [];
        /** @type {?string} Why the services could not be loaded. */
        let loadError = null;
        try {
            services = (await Sw.Net.Send(api("GetDebugMcpServices"), null, post)) || [];
        }
        catch (e) {
            console.error(e);
            loadError = Sw.ErrorMessage(e);
        }

        /** @type {Map<string, {W: Set<string>, A: Set<string>}>} The selected methods per service key. */
        const sel = new Map();
        for (const s of services)
            sel.set(serviceKey(s), { W: new Set(), A: new Set() });

        /** Select the defaults: all API's of the default services, no AI tools. */
        function selectDefaults() {
            for (const s of services) {
                const v = sel.get(serviceKey(s));
                v.W = new Set(s.Default ? (s.WebApis || []).map(m => m.Method) : []);
                v.A = new Set();
            }
        }

        /** Load the stored selection (only methods that still exist), or the defaults. */
        function loadSelection() {
            let stored = null;
            try {
                stored = JSON.parse(localStorage.getItem(SelectionKey) || "null");
            }
            catch {
                stored = null;
            }
            if (!stored || typeof stored !== "object") {
                selectDefaults();
                return;
            }
            for (const s of services) {
                const v = sel.get(serviceKey(s));
                const st = stored[serviceKey(s)] || {};
                for (const k of Kinds) {
                    const have = new Set((s[k.Prop] || []).map(m => m.Method));
                    v[k.Key] = new Set((Array.isArray(st[k.Key]) ? st[k.Key] : []).filter(m => have.has(m)));
                }
            }
        }

        /** Store the selection (a per viewer convenience, a failure is ignored). */
        function saveSelection() {
            const o = {};
            for (const [key, v] of sel)
                if (v.W.size || v.A.size)
                    o[key] = { W: [...v.W], A: [...v.A] };
            try {
                localStorage.setItem(SelectionKey, JSON.stringify(o));
            }
            catch {
                //  Private window, blocked storage etc: the selection just isn't remembered
            }
        }

        loadSelection();

        const search = new Sw.TextBox({
            Type: "search",
            AriaLabel: _TF("Filter the services and methods", "Accessible name of the search box of the MCP debug page"),
            Placeholder: _TF("Filter services, tools and descriptions", "Placeholder of the search box of the MCP debug page"),
            Icon: "search",
            Clear: true,
            OnInput: () => applyFilter(),
        });
        const summaryE = el("div.mcpd-summary", { attrs: { role: "status", "aria-live": "polite" } });
        const listE = el("div.mcpd-services");
        const selectCard = Sw.Card({
            Title: _TF("Expose", "Title of the card where the admin selects the API's and AI tools that the temporary MCP server exposes"),
            Sub: _TF("Only methods you may use are listed, the server replaces their auth with its API key.", "Sub title of the card where the admin selects what the temporary MCP server exposes"),
            Tools: [
                { Text: _TF("Defaults", "Button that selects the default (monitoring) API's on the MCP debug page"), Icon: "undo", OnClick: () => { selectDefaults(); changed(); } },
                { Text: _TF("All", "Button that selects all shown API's and AI tools on the MCP debug page"), Icon: "check", OnClick: () => setShown(true) },
                { Text: _TF("None", "Button that clears the selection of all shown API's and AI tools on the MCP debug page"), Icon: "close", OnClick: () => setShown(false) },
            ],
            Body: el("div.mcpd-select", null, el("div.mcpd-select-bar", null, search.E, summaryE), listE),
        });
        page.Add(selectCard);

        /**
         * The rendered parts of a service: its block, check box, count and per kind the group and method rows.
         * @type {Array<{S: object, E: HTMLElement, Box: HTMLInputElement, CountE: HTMLElement, Groups: Array<{Kind: object, E: HTMLElement, Box: HTMLInputElement, Rows: Array<{M: object, E: HTMLElement, Box: HTMLInputElement}>}>}>}
         */
        const views = [];

        /**
         * Set a check box from the number of selected items.
         * @param {HTMLInputElement} box The box.
         * @param {number} n Selected.
         * @param {number} total All.
         */
        function setBox(box, n, total) {
            box.checked = total > 0 && n === total;
            box.indeterminate = n > 0 && n < total;
        }

        /** Update every check box, count and the summary from the selection, and store it. */
        function changed() {
            let tools = 0;
            let used = 0;
            for (const v of views) {
                const s = sel.get(serviceKey(v.S));
                let n = 0;
                let total = 0;
                for (const g of v.Groups) {
                    const set = s[g.Kind.Key];
                    for (const r of g.Rows)
                        r.Box.checked = set.has(r.M.Method);
                    setBox(g.Box, set.size, g.Rows.length);
                    n += set.size;
                    total += g.Rows.length;
                }
                setBox(v.Box, n, total);
                v.CountE.textContent = n + " / " + total;
                v.E.classList.toggle("mcpd-on", n > 0);
                tools += n;
                if (n > 0)
                    ++used;
            }
            summaryE.textContent = tools <= 0
                ? _TF("Nothing selected", "Summary of the MCP debug page when no API or AI tool is selected")
                : _TF("{0} tools from {1} services selected", "Summary of the MCP debug page, {0} is the number of selected tools, {1} the number of services").replace("{0}", tools).replace("{1}", used);
            summaryE.classList.toggle("mcpd-none", tools <= 0);
            if (!state)
                start.SetDisabled(tools <= 0, _TF("Select at least one API or AI tool first", "Reason of the disabled start button of the MCP debug page when nothing is selected"));
            saveSelection();
        }

        /**
         * Select or clear the methods that are shown (the filter applies).
         * @param {boolean} on Select.
         */
        function setShown(on) {
            for (const v of views) {
                if (v.E.hidden)
                    continue;
                const s = sel.get(serviceKey(v.S));
                for (const g of v.Groups)
                    for (const r of g.Rows)
                        if (!r.E.hidden)
                            on ? s[g.Kind.Key].add(r.M.Method) : s[g.Kind.Key].delete(r.M.Method);
            }
            changed();
        }

        /** Show the services and methods that match the search (a service name match shows all of its methods). */
        function applyFilter() {
            const q = (search.Get() || "").trim().toLowerCase();
            let any = false;
            for (const v of views) {
                const svcHit = !q || (v.S.Name + " " + (v.S.InstanceName || "") + " " + v.S.TypeName).toLowerCase().includes(q);
                let shown = 0;
                for (const g of v.Groups) {
                    let gShown = 0;
                    for (const r of g.Rows) {
                        const m = r.M;
                        const hit = svcHit || (m.Tool + " " + m.Method + " " + (m.Description || "")).toLowerCase().includes(q);
                        r.E.hidden = !hit;
                        if (hit)
                            ++gShown;
                    }
                    g.E.hidden = gShown <= 0;
                    shown += gShown;
                }
                v.E.hidden = shown <= 0;
                //  A search opens the services with matches (a method match is otherwise invisible in a closed service)
                if (q && shown > 0 && !svcHit)
                    v.E.open = true;
                if (shown > 0)
                    any = true;
            }
            noMatchE.hidden = any || !views.length;
        }

        const noMatchE = el("p.mcpd-empty", { text: _TF("No service or method matches the filter.", "Message of the MCP debug page when the search matches nothing"), hidden: true });

        /**
         * Render a method row.
         * @param {object} v The service view.
         * @param {object} g The group.
         * @param {object} m The method (DebugMcpMethod).
         * @returns {object} The row.
         */
        function methodRow(v, g, m) {
            const box = el("input.mcpd-check", {
                type: "checkbox",
                attrs: { "aria-label": m.Tool },
                on: {
                    change: () => {
                        const set = sel.get(serviceKey(v.S))[g.Kind.Key];
                        box.checked ? set.add(m.Method) : set.delete(m.Method);
                        changed();
                    },
                },
            });
            const auth = authText(m.Auth);
            const e = el("div.mcpd-method", null,
                el("label.mcpd-method-head", null, box, el("span.mcpd-tool", { text: m.Tool })),
                el("span.mcpd-auth", { text: auth, title: _TF("The auth the method normally requires, on the debug server it only requires the API key of the server", "Tool tip of the auth of a method on the MCP debug page") }),
                m.Description ? el("span.mcpd-desc.sw-selectable", { text: m.Description }) : null);
            return { M: m, E: e, Box: box };
        }

        /**
         * Render a service.
         * @param {object} s The service (DebugMcpServiceInfo).
         * @returns {object} The view.
         */
        function serviceView(s) {
            const v = { S: s, Groups: [] };
            v.Box = el("input.mcpd-check", {
                type: "checkbox",
                attrs: { "aria-label": s.Name },
                on: {
                    //  The box is in the summary: a click must not open / close the service
                    click: ev => ev.stopPropagation(),
                    change: () => {
                        const x = sel.get(serviceKey(s));
                        const on = v.Box.checked;
                        for (const g of v.Groups)
                            x[g.Kind.Key] = new Set(on ? g.Rows.map(r => r.M.Method) : []);
                        changed();
                    },
                },
            });
            v.CountE = el("span.mcpd-count");
            const head = el("summary.mcpd-svc-head", null,
                v.Box,
                el("span.mcpd-svc-name", null,
                    el("span.mcpd-svc-title", { text: s.Name + (s.InstanceName ? " [" + s.InstanceName + "]" : "") }),
                    el("span.mcpd-svc-type.sw-selectable", { text: s.TypeName })),
                s.Default ? Sw.Badge(_TF("Monitoring", "Badge of a service that the MCP debug server exposes by default (a monitoring service)"), "info") : null,
                v.CountE);
            const body = el("div.mcpd-svc-body");
            for (const k of Kinds) {
                const methods = s[k.Prop] || [];
                if (!methods.length)
                    continue;
                const g = { Kind: k, Rows: [] };
                g.Box = el("input.mcpd-check", {
                    type: "checkbox",
                    attrs: { "aria-label": k.Title() + " - " + s.Name },
                    on: {
                        change: () => {
                            sel.get(serviceKey(s))[k.Key] = new Set(g.Box.checked ? g.Rows.map(r => r.M.Method) : []);
                            changed();
                        },
                    },
                });
                for (const m of methods)
                    g.Rows.push(methodRow(v, g, m));
                g.E = el("div.mcpd-group", null,
                    el("label.mcpd-group-head", null, g.Box, el("span", { text: k.Title() }), el("span.mcpd-group-count", { text: "(" + methods.length + ")" })),
                    el("div.mcpd-methods", null, ...g.Rows.map(r => r.E)));
                v.Groups.push(g);
                body.append(g.E);
            }
            v.E = el("details.mcpd-svc", null, head, body);
            return v;
        }

        if (loadError) {
            listE.append(Sw.Badge(_TF("Failed to get the services: {0}", "Error of the MCP debug page when the services could not be loaded, {0} is the reason").replace("{0}", loadError), "error"));
        }
        else if (!services.length) {
            listE.append(el("p.mcpd-empty", { text: _TF("There is nothing that you may expose: no service has an API or AI tool that you may use.", "Message of the MCP debug page when there are no services to expose") }));
        }
        else {
            for (const s of services) {
                const v = serviceView(s);
                views.push(v);
                listE.append(v.E);
            }
            listE.append(noMatchE);
        }

        /**
         * The request of StartDebugMcp: the services with something selected.
         * @returns {{Services: Array<object>}} The request.
         */
        function request() {
            const list = [];
            for (const s of services) {
                const v = sel.get(serviceKey(s));
                if (v.W.size || v.A.size)
                    list.push({ TypeName: s.TypeName, InstanceName: s.InstanceName || null, WebApis: [...v.W], AiTools: [...v.A] });
            }
            return { Services: list };
        }

        // ------------------------------------------------------------------ connection

        /**
         * The absolute url of the running server (the server returns it absolute, a stored relative one is resolved against this page).
         * @returns {string} The url, "" when none runs.
         */
        function fullUrl() {
            if (!state || !state.Url)
                return "";
            try {
                return new URL(state.Url, location.href).href;
            }
            catch {
                return state.Url;
            }
        }

        /**
         * A secret with only its last 4 characters shown.
         * @param {string} s The secret.
         * @returns {string} The redacted text.
         */
        const redact = s => s ? "•".repeat(12) + s.slice(-4) : "";

        /**
         * A row of the connection card: a label, a value (mono) and a copy button that copies the real value.
         * @param {string} label The label.
         * @param {string} what What is copied (the copy button's tool tip and message).
         * @param {function(): string} get The value to copy.
         * @param {boolean} secret True if the shown value is redacted (the copy button never shows the value).
         * @returns {{E: HTMLElement, ValueE: HTMLElement}} The row.
         */
        function connRow(label, what, get, secret) {
            const valueE = el(secret ? "code.mcpd-value.mcpd-redacted" : "code.mcpd-value.sw-selectable");
            const copy = Sw.CopyButton({ Get: get, What: what, Generic: !!secret });
            return {
                E: el("div.mcpd-row", null, el("span.mcpd-label", { text: label }), valueE, copy.E),
                ValueE: valueE,
            };
        }

        const urlRow = connRow(
            _TF("MCP url", "Label of the full url of the temporary MCP server"),
            _TF("MCP url", "What the copy button next to the url of the temporary MCP server copies"),
            fullUrl, false);
        const keyRow = connRow(
            _TF("API key", "Label of the (redacted) API key of the temporary MCP server"),
            _TF("API key", "What the copy button next to the API key of the temporary MCP server copies"),
            () => state ? state.ApiKey : "", true);
        const headerRow = connRow(
            _TF("Header", "Label of the (redacted) http authorization header to use with the temporary MCP server"),
            _TF("authorization header", "What the copy button next to the authorization header of the temporary MCP server copies"),
            () => state ? "Authorization: Bearer " + state.ApiKey : "", true);
        const credHint = el("p.mcpd-hint", { text: _TF("Clients authenticate with the API key, as \"Authorization: Bearer <key>\" (or \"x-api-key: <key>\"). It only gives access to the tools of this server.", "Hint under the credentials of the temporary MCP server") });

        /** Show the url and the (redacted) credentials of the running server. */
        function showConnection() {
            urlRow.ValueE.textContent = fullUrl();
            keyRow.ValueE.textContent = state ? redact(state.ApiKey) : "";
            headerRow.ValueE.textContent = state ? "Authorization: Bearer " + redact(state.ApiKey) : "";
        }

        const statusE = el("div.mcpd-status", { attrs: { role: "status", "aria-live": "polite" } });
        const exposedE = el("ul.mcpd-exposed.sw-selectable");
        const copyConfig = new Sw.Button({
            Text: _TF("Copy config", "Button that copies an MCP client configuration (url and API key) of the temporary MCP server"),
            Title: _TF("Copy an MCP client configuration with the url and the API key", "Tool tip of the button that copies an MCP client configuration"),
            Icon: "copy",
            OnClick: () => Sw.Clipboard.Copy(configText(), { Secret: true, What: _TF("MCP client configuration", "What the copy config button of the MCP debug page copies") }),
        });
        const conn = Sw.Card({
            Title: _TF("Connection", "Title of the card with the url and API key of the temporary MCP server"),
            Actions: [copyConfig],
            Body: el("div.mcpd-conn", null, el("div.mcpd-rows", null, urlRow.E, keyRow.E, headerRow.E), credHint, statusE, exposedE),
        });
        page.Add(conn);

        const callsE = el("div.mcpd-table");
        const toolsE = el("div.mcpd-table");
        const callsCard = Sw.Card({ Title: _TF("Calls", "Title of the card with the latest tool calls of the MCP server"), Body: callsE, Flush: true });
        const toolsCard = Sw.Card({ Title: _TF("Tools", "Title of the card with the tools served by the MCP server"), Body: toolsE, Flush: true });
        page.Add(callsCard, toolsCard);

        /** @type {Array<object>} The live tables (Sw.Table). */
        let tables = [];
        /** @type {?string} The Param of the shown tables. */
        let tableParam;
        /** @type {number} The number of calls when the tables were last updated. */
        let lastCalls = -1;

        /**
         * An MCP client configuration (Claude Code, Claude Desktop etc) of the running server.
         * @returns {string} The json.
         */
        function configText() {
            if (!state)
                return "";
            const cfg = { mcpServers: {} };
            cfg.mcpServers["sysweaver-debug"] = { type: "http", url: fullUrl(), headers: { Authorization: "Bearer " + state.ApiKey } };
            return JSON.stringify(cfg, null, 2);
        }

        /**
         * (Re)create the tables when the param changed.
         * @returns {Promise<void>}
         */
        async function showTables() {
            const p = state ? state.Guid : null;
            if ((tables.length > 0) && (tableParam === p))
                return;
            tableParam = p;
            for (const t of tables)
                t.Destroy();
            tables = [];
            callsE.replaceChildren();
            toolsE.replaceChildren();
            callsCard.SetSub(p ? _TF("Debug server", "Sub title of a table card when it shows the data of the temporary MCP server") : _TF("Main end point", "Sub title of a table card when it shows the data of the main MCP end point"));
            toolsCard.SetSub(callsCard.SubE.textContent);
            tables.push(await Sw.Table.Create({ Url: "../Api/CallLogTable", Element: callsE, Param: p, Name: _TF("MCP calls", "Name of the table with the latest MCP tool calls") }));
            tables.push(await Sw.Table.Create({ Url: "../Api/ToolTable", Element: toolsE, Param: p, Name: _TF("MCP tools", "Name of the table with the MCP tools") }));
        }

        /**
         * Show the state (buttons, selection, connection card, tables).
         * @returns {Promise<void>}
         */
        async function update() {
            const running = !!state;
            stop.SetDisabled(!running, _TF("No debug server is running", "Reason of the disabled stop button of the MCP debug page"));
            selectCard.E.hidden = running;
            conn.E.hidden = !running;
            showConnection();
            if (running)
                start.SetDisabled(true, _TF("A debug server is already running, stop it first", "Reason of the disabled start button of the MCP debug page"));
            else {
                statusE.replaceChildren();
                exposedE.replaceChildren();
                changed();
            }
            lastCalls = -1;
            await showTables();
        }

        /**
         * Store (or clear) the running server.
         * @param {?object} s The server ({Guid, ApiKey, Url}), null for none.
         */
        function setState(s) {
            state = s;
            if (s)
                Sw.Storage.Session.SetJson(StoreKey, s);
            else
                Sw.Storage.Session.Remove(StoreKey);
        }

        /**
         * Refresh the status line, goes back to the stopped state when the server is gone.
         * @returns {Promise<void>}
         */
        async function check() {
            if (!state)
                return;
            const guid = state.Guid;
            let s;
            try {
                s = await Sw.Net.Send(api("CheckDebugMcp"), guid, post);
            }
            catch (e) {
                statusE.replaceChildren(Sw.Badge(Sw.ErrorMessage(e), "error"));
                return;
            }
            if (!state || (state.Guid !== guid))
                return;
            if (!s || !s.Running) {
                setState(null);
                Sw.Toast && Sw.Toast(_TF("The debug server was stopped (it expired or was removed).", "Message on the MCP debug page when the temporary MCP server is gone"));
                await update();
                return;
            }
            const mins = Math.max(0, Math.round((s.SecondsToExpiration || 0) / 60));
            const parts = [
                Sw.Badge(_TF("Running", "Badge on the MCP debug page while the temporary MCP server runs"), "success"),
                el("span", { text: count(_TF("{0} tools", "The number of tools of the temporary MCP server, {0} is the number"), s.Tools) }),
                el("span", { text: count(_TF("{0} calls", "The number of tool calls of the temporary MCP server, {0} is the number"), s.Calls) }),
                el("span", { text: count(_TF("{0} errors", "The number of failed tool calls of the temporary MCP server, {0} is the number"), s.Errors) }),
                el("span", { text: count(_TF("Expires in {0} min", "When the temporary MCP server is removed unless it's used, {0} is the number of minutes"), mins) }),
            ];
            if (s.LastError)
                parts.push(el("span.mcpd-error.sw-selectable", { text: _TF("Last error: {0}", "The last error of the temporary MCP server, {0} is the error").replace("{0}", s.LastError) }));
            if (s.Warnings && s.Warnings.length)
                parts.push(el("span.mcpd-warning.sw-selectable", { text: s.Warnings.join(", ") }));
            statusE.replaceChildren(...parts);
            exposedE.replaceChildren(...(s.Services || []).map(x => el("li", { text: x })));
            if (s.Calls !== lastCalls) {
                if (lastCalls >= 0)
                    for (const t of tables)
                        t.UpdateNow();
                lastCalls = s.Calls;
            }
        }

        async function onStart() {
            const req = request();
            if (!req.Services.length)
                return;
            try {
                const r = await Sw.Net.Send(api("StartDebugMcp"), req, post);
                setState({ Guid: r.Guid, ApiKey: r.ApiKey, Url: r.Url });
                await update();
                await check();
            }
            catch (e) {
                console.error(e);
                Sw.Fail(_TF("Failed to start the debug server.", "Error message when the temporary MCP server could not be started") + "\n" + Sw.ErrorMessage(e));
            }
        }

        async function onStop() {
            if (!state)
                return;
            try {
                await Sw.Net.Send(api("RemoveTempServer"), state.Guid, post);
            }
            catch (e) {
                console.error(e);
                Sw.Fail(_TF("Failed to stop the debug server.", "Error message when the temporary MCP server could not be stopped") + "\n" + Sw.ErrorMessage(e));
                return;
            }
            setState(null);
            await update();
        }

        //  A stored server may be gone (expired or stopped from another page)
        if (state) {
            try {
                const s = await Sw.Net.Send(api("CheckDebugMcp"), state.Guid, post);
                if (!s || !s.Running)
                    setState(null);
            }
            catch (e) {
                console.warn("mcp_tools: CheckDebugMcp failed: " + Sw.ErrorMessage(e));
            }
        }
        await update();
        await check();
        setInterval(() => { check(); }, RefreshMs);
    }

    window.mcpToolsMain = mcpToolsMain;
})();
