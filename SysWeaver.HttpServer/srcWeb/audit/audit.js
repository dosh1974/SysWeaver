/**
 * @file audit/audit.js - the audit browser (audit/audit.html, entry `auditMain`): search the audited api calls (who did what, when and
 * from where). Menu item registered by the audit services (MySqlApiAuditService, SimpleAuditService; admin, ops or debug).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js and ../explore/table.js; started from `<body onload>`.
 *
 * Server api (C# IApiAuditReportService, all take the same query, so the totals, the value lists and the table always agree):
 * Api/Audit/GetInfo, GetSummary (totals and the time line), GetFacets (the most common values of a field), GetEntry (one call),
 * AuditTable (the rows; the query is the table Param as json).
 *
 * Parts: search box (terms, "phrases", -exclude, field:term), time range, conditions (any field, all / any must match), filter chips,
 * the time line (drag to select a range), value lists (click a value to only show it, or exclude it), the table (Sw.Table: sort, columns,
 * export, live refresh) and the details of a call (click a row; every value can be used as a filter).
 *
 * Url parameters: `q` = the state as json (the address follows the state, so a page can be bookmarked and shared),
 * `id` = show the details of this call.
 * Globals defined: `auditMain`, `AuditBrowser` (the page class).
 */
"use strict";

/**
 * The audit browser page.
 */
class AuditBrowser {
    constructor() {
        /** The state: what to find. */
        this.Q = AuditBrowser.Repair(null);
        /** @type {?object} ApiAuditInfo. */
        this.Info = null;
        /** @type {?object} The Sw.Table. */
        this.Table = null;
        /** @type {Object<string, object>} The last ApiAuditFacet of the open value lists. */
        this.Facets = {};
        /** @type {Object<string, boolean>} The fields with all values shown. */
        this.ShowMore = {};
        /** @type {number} Request counter, an answer of an older request is ignored. */
        this.Gen = 0;
    }

    // ------------------------------------------------------------------ definitions

    /** @returns {Object<string, string>} The titles of the fields (members of the C# ApiAuditEntry). */
    static get Titles() {
        if (!AuditBrowser.TitleCache)
            AuditBrowser.TitleCache = {
                State: _TF("State", "Name of a value of an audited server call: if it is in progress, completed or failed"),
                Group: _TF("Group", "Name of a value of an audited server call: the group that the called function belongs to"),
                Api: _TF("API", "Name of a value of an audited server call: the name of the called function"),
                UserName: _TF("User", "Name of a value of an audited server call: the name of the user that made the call"),
                Ip: _TF("IP address", "Name of a value of an audited server call: the network address of the client"),
                UserAgent: _TF("User agent", "Name of a value of an audited server call: the text that identifies the web browser of the client"),
                DeviceId: _TF("Device", "Name of a value of an audited server call: the id of the device that the call was made from"),
                Session: _TF("Session", "Name of a value of an audited server call: the (partly hidden) id of the session"),
                ClientId: _TF("Client", "Name of a value of an audited server call: the id of the client (ip address, device and web browser)"),
                Language: _TF("Language", "Name of a value of an audited server call: the language of the client"),
                TimeZone: _TF("Time zone", "Name of a value of an audited server call: the time zone of the client"),
                Begin: _TF("Begin", "Name of a value of an audited server call: the time when the call began"),
                End: _TF("End", "Name of a value of an audited server call: the time when the call ended"),
                Duration: _TF("Duration (ms)", "Name of a value of an audited server call: how many milliseconds it took"),
                Input: _TF("Input", "Name of a value of an audited server call: the data sent to the function"),
                Output: _TF("Output", "Name of a value of an audited server call: the data returned by the function or the error message"),
                UserGuid: _TF("User id", "Name of a value of an audited server call: the unique id of the user"),
                Id: _TF("Id", "Name of a value of an audited server call: the unique id of the call"),
            };
        return AuditBrowser.TitleCache;
    }

    /**
     * The shown text of a field value.
     * @param {string} field - The field.
     * @param {string} value - The value.
     * @returns {string} The text.
     */
    static ValueText(field, value) {
        if (field === "State")
            switch (String(value)) {
                case "0":
                case "InProgress":
                    return _TF("In progress", "State of an audited server call that has not ended");
                case "1":
                case "Completed":
                    return _TF("Completed", "State of an audited server call that ended without any error");
                case "2":
                case "Failed":
                    return _TF("Failed", "State of an audited server call that ended with an error");
            }
        return value === "" || value === null || value === undefined ? _TF("(none)", "Shown in a list of values instead of an empty value") : String(value);
    }

    /** @returns {Array<object>} The operations of conditions: C# TableDataFilterOps values (Invert: the opposite), T the field types (t text, n number, s state, d date). */
    static get Ops() {
        if (!AuditBrowser.OpCache)
            AuditBrowser.OpCache = [
                { Key: "6", Op: 6, T: "t", Text: _TF("contains", "Operation of a filter condition: the text contains the given text") },
                { Key: "!6", Op: 6, Invert: true, T: "t", Text: _TF("does not contain", "Operation of a filter condition: the text does not contain the given text") },
                { Key: "0", Op: 0, T: "tnsd", Text: _TF("is", "Operation of a filter condition: the value is equal to the given value") },
                { Key: "!0", Op: 0, Invert: true, T: "tnsd", Text: _TF("is not", "Operation of a filter condition: the value is not equal to the given value") },
                { Key: "7", Op: 7, T: "t", Text: _TF("starts with", "Operation of a filter condition: the text starts with the given text") },
                { Key: "!7", Op: 7, Invert: true, T: "t", Text: _TF("does not start with", "Operation of a filter condition: the text does not start with the given text") },
                { Key: "8", Op: 8, T: "t", Text: _TF("ends with", "Operation of a filter condition: the text ends with the given text") },
                { Key: "9", Op: 9, T: "tn", Text: _TF("is any of (comma separated)", "Operation of a filter condition: the value is equal to any of the given comma separated values") },
                { Key: "10", Op: 10, T: "tn", Text: _TF("is none of (comma separated)", "Operation of a filter condition: the value is not equal to any of the given comma separated values") },
                { Key: "3", Op: 3, T: "nd", Text: _TF("is greater than", "Operation of a filter condition: the number or time is greater (later) than the given value") },
                { Key: "5", Op: 5, T: "nd", Text: _TF("is at least", "Operation of a filter condition: the number or time is greater (later) than or equal to the given value") },
                { Key: "2", Op: 2, T: "nd", Text: _TF("is less than", "Operation of a filter condition: the number or time is less (earlier) than the given value") },
                { Key: "4", Op: 4, T: "nd", Text: _TF("is at most", "Operation of a filter condition: the number or time is less (earlier) than or equal to the given value") },
            ];
        return AuditBrowser.OpCache;
    }

    /**
     * The type of a field.
     * @param {string} field - The field.
     * @returns {string} "t" text, "n" number, "s" state or "d" date.
     */
    static TypeOf(field) {
        switch (field) {
            case "Id":
            case "ClientId":
            case "Duration":
                return "n";
            case "State":
                return "s";
            case "Begin":
            case "End":
                return "d";
        }
        return "t";
    }

    /** @returns {Array<object>} The time ranges: Value (name in the state), Text, Hours (0 = no limit). */
    static get Ranges() {
        if (!AuditBrowser.RangeCache)
            AuditBrowser.RangeCache = [
                { Value: "1h", Hours: 1, Text: _TF("Last hour", "A selectable time range: the last hour") },
                { Value: "24h", Hours: 24, Text: _TF("Last 24 hours", "A selectable time range: the last 24 hours") },
                { Value: "7d", Hours: 7 * 24, Text: _TF("Last 7 days", "A selectable time range: the last 7 days") },
                { Value: "30d", Hours: 30 * 24, Text: _TF("Last 30 days", "A selectable time range: the last 30 days") },
                { Value: "all", Hours: 0, Text: _TF("All time", "A selectable time range: no limit") },
                { Value: "custom", Hours: 0, Text: _TF("Custom range…", "A selectable time range: the user enters the start and end time") },
            ];
        return AuditBrowser.RangeCache;
    }

    // ------------------------------------------------------------------ state

    /**
     * A valid state from untrusted data (the url).
     * @param {?object} s - The data.
     * @returns {{Range: string, From: ?string, To: ?string, Text: string, Values: Array, Conds: Array, Match: string}} The state.
     */
    static Repair(s) {
        s = s && typeof s === "object" ? s : {};
        const time = t => typeof t === "string" && !isNaN(Date.parse(t)) ? new Date(t).toISOString() : null;
        const str = t => typeof t === "string" ? t : "";
        const arr = t => Array.isArray(t) ? t : [];
        const q = { Range: str(s.Range), From: time(s.From), To: time(s.To), Text: str(s.Text), Values: [], Conds: [], Match: s.Match === "any" ? "any" : "all" };
        if (!AuditBrowser.Ranges.some(x => x.Value === q.Range))
            q.Range = "7d";
        if (q.Range === "custom" && !q.From && !q.To)
            q.Range = "all";
        if (q.Range !== "custom")
            q.From = q.To = null;
        for (const v of arr(s.Values)) {
            const values = arr(v && v.Values).filter(x => typeof x === "string");
            if (v && Object.hasOwn(AuditBrowser.Titles, v.Field) && values.length)
                q.Values.push({ Field: v.Field, Values: values, Exclude: !!v.Exclude });
        }
        for (const c of arr(s.Conds))
            if (c && Object.hasOwn(AuditBrowser.Titles, c.ColName) && Number.isInteger(c.Op) && typeof c.Value === "string")
                q.Conds.push({ ColName: c.ColName, Op: c.Op, Invert: !!c.Invert, Value: c.Value });
        return q;
    }

    /**
     * The query of the server apis (C# ApiAuditQuery).
     * @returns {object} The query.
     */
    Query() {
        const q = this.Q;
        const r = {};
        const range = AuditBrowser.Ranges.find(x => x.Value === q.Range);
        if (range.Hours)
            r.From = new Date(Date.now() - range.Hours * 3600000).toISOString();
        if (q.From)
            r.From = q.From;
        if (q.To)
            r.To = q.To;
        if (q.Text.trim())
            r.Text = q.Text.trim();
        if (q.Values.length)
            r.Values = q.Values;
        if (q.Conds.length)
            r[q.Match === "any" ? "Any" : "All"] = q.Conds;
        return r;
    }

    /**
     * The query including what is entered in the table itself (its search box and column filters), so that the totals agree with the rows.
     * @returns {object} The query.
     */
    FullQuery() {
        const r = this.Query();
        const req = this.Table ? this.Table.State.RequestParams : null;
        const filters = req && Array.isArray(req.Filters) ? req.Filters.filter(x => x && x.Value) : [];
        if (filters.length)
            r.All = (r.All || []).concat(filters);
        if (req && req.SearchText)
            r.Text = ((r.Text || "") + " " + req.SearchText).trim();
        return r;
    }

    /** @returns {boolean} True if anything but the time range limits the calls. */
    get IsFiltered() {
        const q = this.Q;
        return !!q.Text.trim() || q.Values.length > 0 || q.Conds.length > 0;
    }

    /**
     * The state changed: show it and find the calls.
     */
    Changed() {
        const t = this;
        const u = new URL(location.href);
        u.searchParams.delete("id");
        u.searchParams.set("q", JSON.stringify(t.Q));
        history.replaceState(history.state, "", u.href);
        t.ShowState();
        t.Refresh();
        if (t.Table) {
            t.Table.State.RequestParams.Param = JSON.stringify(t.Query());
            t.Table.Changed();
        }
    }

    /**
     * Select (or deselect) a value of a field.
     * @param {string} field - The field.
     * @param {string} value - The value.
     * @param {boolean} [exclude] - Exclude the value instead.
     */
    ToggleValue(field, value, exclude) {
        const q = this.Q;
        exclude = !!exclude;
        const cur = q.Values.find(x => x.Field === field && x.Exclude === exclude);
        const had = cur && cur.Values.includes(value);
        //  A value is never both selected and excluded
        this.RemoveValue(field, value);
        if (!had) {
            const s = q.Values.find(x => x.Field === field && x.Exclude === exclude);
            if (s)
                s.Values.push(value);
            else
                q.Values.push({ Field: field, Values: [value], Exclude: exclude });
        }
        this.Changed();
    }

    /**
     * Remove a value of a field from the state.
     * @param {string} field - The field.
     * @param {string} value - The value.
     */
    RemoveValue(field, value) {
        const q = this.Q;
        for (const s of q.Values)
            if (s.Field === field)
                s.Values = s.Values.filter(x => x !== value);
        q.Values = q.Values.filter(x => x.Values.length);
    }

    /**
     * Use a custom time range.
     * @param {?Date} from - The start, null for none.
     * @param {?Date} to - The end, null for none.
     */
    SetRange(from, to) {
        const q = this.Q;
        q.Range = "custom";
        q.From = from ? from.toISOString() : null;
        q.To = to ? to.toISOString() : null;
        this.Changed();
    }

    // ------------------------------------------------------------------ build

    /**
     * Build the page and load the data.
     * @returns {Promise<void>}
     */
    async Build() {
        const t = this;
        t.Info = await Sw.Net.Send("../Api/Audit/GetInfo", null);
        const param = Sw.Url.Get("q");
        if (param)
            try {
                t.Q = AuditBrowser.Repair(JSON.parse(param));
            }
            catch {
                console.warn("audit: invalid q parameter");
            }
        const days = t.Info.RetentionDays;
        t.Reset = new Sw.Button({
            Text: _TF("Clear all", "Text of a button that removes all filters of the audit browser"),
            Title: _TF("Remove all filters", "Tool tip of a button that removes all filters of the audit browser"),
            Icon: "close", OnClick: () => {
                const range = t.Q.Range;
                t.Q = AuditBrowser.Repair({ Range: range === "custom" ? "7d" : range });
                t.Changed();
            },
        });
        t.Page = Sw.PageLayout({
            Type: "fill",
            Title: _TF("Audit browser", "Title of the page where the log of who called what function of the server is searched"),
            Description: days > 0
                ? _T("Who did what, when and from where. Calls are kept for {0} days.", days, "Description of the audit browser page. {0} is the number of days")
                : _TF("Who did what, when and from where.", "Description of the audit browser page"),
            Actions: [
                new Sw.Button({
                    Text: _TF("Copy link", "Text of a button that copies a link to the audit browser with the current filters"),
                    Title: _TF("Copy a link to this page with the current filters", "Tool tip of a button that copies a link to the audit browser with the current filters"),
                    Icon: "copy", OnClick: () => Sw.Clipboard.Copy(location.href, { What: _TF("link", "Name of what is copied: a link to a page") }),
                }),
                t.Reset,
            ],
        });
        t.BuildBar();
        t.Chips = Sw.el("div.ab-chips", { attrs: { role: "group", "aria-label": _TF("Active filters", "Accessible name of the list of active filters in the audit browser") } });
        t.BuildChart();
        t.FacetsE = Sw.el("aside.ab-facets", { attrs: { "aria-label": _TF("Filter by value", "Accessible name of the lists of values that can be used as filters in the audit browser") } });
        t.BuildFacets(t.FacetsE);
        t.TableE = Sw.el("div.ab-table.sw-table-fill");
        t.Page.Add(t.Bar, t.Chips, t.ChartE, Sw.el("div.ab-main.sw-fill", null, t.FacetsE, t.TableE));
        t.ShowState();
        t.Refresh();
        t.Table = await Sw.Table.Create({
            Url: "../Api/Audit/AuditTable",
            Element: t.TableE,
            Name: _TF("Audit", "Title of the table with audited calls, also the name of an exported file"),
            Param: JSON.stringify(t.Query()),
            //  Always a clean table: the page has the filters (and they are in the address)
            State: JSON.stringify({ Expanded: 0 }),
            Order: ["-Id"],
            Header: false,
            EmptyTitle: _TF("No calls found", "Title of the empty table of the audit browser"),
            EmptyText: _TF("No audited calls match the filters and the time range.", "Text of the empty table of the audit browser"),
        });
        //  (capture: a click on a value shows the details instead of copying the value, the details have copy buttons)
        t.TableE.addEventListener("click", ev => t.RowClick(ev), true);
        t.TableE.addEventListener("keydown", ev => {
            if (ev.key === "Enter")
                t.RowClick(ev);
        }, true);
        //  The state stands out (only the calls that need attention)
        const mark = () => {
            const vi = t.Table.Vis.findIndex(v => v.Col.Name === "State");
            for (const tr of t.TableE.querySelectorAll("tbody tr")) {
                const td = vi >= 0 ? tr.cells[vi + 1] : null;
                if (!td || !tr.SwRow)
                    continue;
                const state = tr.SwRow.Values[t.Table.Vis[vi].Index];
                td.classList.toggle("ab-is-failed", state === "Failed");
                td.classList.toggle("ab-is-progress", state === "InProgress");
            }
        };
        new MutationObserver(mark).observe(t.TableE, { childList: true, characterData: true, subtree: true });
        mark();
        //  The filters of the table itself
        let sig = "";
        t.TableE.addEventListener("sw-table-count", () => {
            const q = t.FullQuery();
            const s = JSON.stringify([q.Text, q.All]);
            if (s !== sig) {
                sig = s;
                t.Refresh();
            }
        });
        //  The table refreshes itself, the rest is refreshed less often
        setInterval(() => {
            if (!document.hidden && !t.Selecting)
                t.Refresh(true);
        }, 20000);
        const id = Number(Sw.Url.Get("id"));
        if (id > 0)
            t.ShowDetails(id);
    }

    /**
     * Build the search bar.
     */
    BuildBar() {
        const t = this;
        const apply = Sw.Util.Debounce(() => {
            const v = t.Search.Get() || "";
            if (v !== t.Q.Text) {
                t.Q.Text = v;
                t.Changed();
            }
        }, 400);
        t.Search = new Sw.TextBox({
            Icon: "search", Clear: true, Value: t.Q.Text, SpellCheck: false,
            Placeholder: _TF("Search, ex: login user:anna -state:failed", "Placeholder of the search box of the audit browser. The words user: and -state:failed are a fixed search syntax and must not be translated"),
            AriaLabel: _TF("Search the audit", "Accessible name of the search box of the audit browser"),
            Title: [
                _TF("All words must be found, in any field.", "Tool tip line of the search box of the audit browser"),
                _TF("Use quotes to search for a phrase, and a minus sign before a word to exclude it.", "Tool tip line of the search box of the audit browser"),
                _TF("Search in one field with its name and a colon:", "Tool tip line of the search box of the audit browser, followed by a list of field names"),
                "api: group: user: ip: input: output: state: session: device: client: lang: tz: agent: id:",
            ].join("\n"),
            OnInput: apply, OnChange: apply, OnEnter: apply,
        });
        t.Range = new Sw.Select({
            Items: AuditBrowser.Ranges.map(x => ({ Value: x.Value, Text: x.Text })), Value: t.Q.Range,
            AriaLabel: _TF("Time range", "Accessible name of the select box of the time range of the audit browser"),
            OnChange: v => {
                if (v === "custom") {
                    t.EditRange();
                    return;
                }
                t.Q.Range = v;
                t.Q.From = t.Q.To = null;
                t.Changed();
            },
        });
        t.CondB = new Sw.Button({
            Text: _TF("Conditions", "Text of a button that opens a dialog with filter conditions"),
            Title: _TF("Add conditions on any field", "Tool tip of a button that opens a dialog with filter conditions"),
            Icon: "filter", OnClick: () => {
                t.EditConditions();
            },
        });
        const values = new Sw.Button({
            Text: _TF("Values", "Text of a button that opens a dialog with lists of values that can be used as filters"),
            Title: _TF("Filter by value", "Tool tip of a button that opens a dialog with lists of values that can be used as filters"),
            Icon: "list", Class: "ab-only-narrow", OnClick: () => {
                t.FacetDialog();
            },
        });
        t.Bar = Sw.el("div.ab-bar", null, Sw.el("div.ab-search", null, t.Search.E), t.Range.E, t.CondB.E, values.E);
    }

    /**
     * Build the totals and the time line.
     */
    BuildChart() {
        const t = this;
        t.ChartHead = Sw.el("div.ab-chart-head", { attrs: { "aria-live": "polite" } });
        t.Plot = Sw.el("div.ab-plot", {
            attrs: { role: "img", "aria-label": _TF("Number of calls over time", "Accessible name of the chart with the number of calls over time") },
            title: "",
        });
        t.Axis = Sw.el("div.ab-axis", { attrs: { "aria-hidden": "true" } });
        t.ChartE = Sw.el("section.sw-card.ab-chart-card", null, t.ChartHead, t.Plot, t.Axis);
        const index = ev => {
            const r = t.Plot.getBoundingClientRect();
            const n = t.Summary ? t.Summary.Steps.length : 0;
            return n ? Math.max(0, Math.min(n - 1, Math.floor((ev.clientX - r.left) / r.width * n))) : -1;
        };
        let tip = null;
        let hover = -1;
        const hide = () => {
            if (tip)
                tip.remove();
            tip = null;
            t.Plot.querySelectorAll(".is-hover").forEach(x => x.classList.remove("is-hover"));
            hover = -1;
        };
        const show = i => {
            if (i === hover || i < 0)
                return;
            hide();
            hover = i;
            const s = t.Summary.Steps[i];
            const from = new Date(s.Time);
            const to = new Date(from.getTime() + t.Summary.StepSeconds * 1000);
            const row = (cls, text, n) => Sw.el("div", null, Sw.el("i." + cls), text, Sw.el("b", { text: Sw.Format.Number(n, 0) }));
            tip = Sw.el("div.ab-tip", null, AuditBrowser.TimeText(from, true) + " – " + AuditBrowser.TimeText(to, true),
                row("ab-completed", AuditBrowser.ValueText("State", "Completed"), s.Completed),
                row("ab-failed", AuditBrowser.ValueText("State", "Failed"), s.Failed),
                s.InProgress ? row("ab-progress", AuditBrowser.ValueText("State", "InProgress"), s.InProgress) : null);
            const n = t.Summary.Steps.length;
            t.Plot.append(tip);
            const w = t.Plot.clientWidth;
            tip.style.left = Math.max(0, Math.min(w - tip.offsetWidth, (i + 0.5) / n * w - tip.offsetWidth / 2)) + "px";
            t.Plot.children[i].classList.add("is-hover");
        };
        t.HideTip = hide;
        //  Drag to select a time range (a click selects one step)
        let start = -1;
        let mark = null;
        const place = i => {
            const n = t.Summary.Steps.length;
            const a = Math.min(start, i);
            const b = Math.max(start, i) + 1;
            mark.style.left = (a / n * 100) + "%";
            mark.style.width = ((b - a) / n * 100) + "%";
        };
        const end = () => {
            start = -1;
            t.Selecting = false;
            if (mark)
                mark.remove();
            mark = null;
        };
        t.Plot.addEventListener("pointerdown", ev => {
            if (ev.button !== 0 || index(ev) < 0 || !t.Summary.StepSeconds)
                return;
            start = index(ev);
            t.Selecting = true;
            mark = Sw.el("div.ab-select");
            t.Plot.append(mark);
            place(start);
            t.Plot.setPointerCapture(ev.pointerId);
        });
        t.Plot.addEventListener("pointermove", ev => {
            const i = index(ev);
            show(i);
            if (start >= 0)
                place(i);
        });
        t.Plot.addEventListener("pointerup", ev => {
            if (start < 0)
                return;
            const i = index(ev);
            const s = t.Summary;
            const from = new Date(s.Steps[Math.min(start, i)].Time);
            const to = new Date(new Date(s.Steps[Math.max(start, i)].Time).getTime() + s.StepSeconds * 1000);
            end();
            hide();
            t.SetRange(from, to);
        });
        t.Plot.addEventListener("pointercancel", end);
        t.Plot.addEventListener("pointerleave", () => {
            if (start < 0)
                hide();
        });
    }

    /** The fields with value lists, the first are open by default. */
    static get FacetFields() {
        return ["State", "Group", "Api", "UserName", "Ip", "DeviceId", "Session", "ClientId", "UserAgent", "Language", "TimeZone"];
    }

    /**
     * Build the value lists.
     * @param {HTMLElement} e - The container.
     */
    BuildFacets(e) {
        const t = this;
        AuditBrowser.FacetFields.forEach((field, i) => {
            const list = Sw.el("ul.ab-values");
            const count = Sw.Badge({ Text: "" });
            count.classList.add("sw-count");
            count.hidden = true;
            const d = Sw.el("details.ab-facet", { dataset: { field: field } },
                Sw.el("summary", null, Sw.Icon("down"), AuditBrowser.Titles[field], count), list);
            d.open = t.FacetOpen ? !!t.FacetOpen[field] : i < 5;
            d.addEventListener("toggle", () => {
                t.FacetOpen = t.FacetOpen || Object.fromEntries(AuditBrowser.FacetFields.map((f, n) => [f, n < 5]));
                t.FacetOpen[field] = d.open;
                if (d.open && !t.Facets[field])
                    t.RefreshFacets();
            });
            e.append(d);
        });
    }

    // ------------------------------------------------------------------ show

    /**
     * A time for people (local time).
     * @param {Date} d - The time.
     * @param {boolean} [short] - No seconds.
     * @returns {string} The text.
     */
    static TimeText(d, short) {
        const s = Sw.Format.TimeStamp(d)[1];
        return short ? s.replace(/(\d{1,2}:\d\d):\d\d/, "$1") : s;
    }

    /**
     * The value of a datetime-local input (local time).
     * @param {?string} s - The time (ISO, UTC).
     * @returns {string} The value.
     */
    static InputTime(s) {
        if (!s || isNaN(Date.parse(s)))
            return "";
        return Sw.Format.IsoText(Sw.Format.UtcToLocal(new Date(s))).substring(0, 19).replace(" ", "T");
    }

    /**
     * A duration for people.
     * @param {number} ms - Number of milliseconds.
     * @returns {string} The text.
     */
    static DurationText(ms) {
        return ms < 1000 ? Sw.Format.Number(ms, ms < 10 ? 1 : 0) + " ms" : Sw.Format.Number(ms / 1000, 2) + " s";
    }

    /**
     * Show the state in the controls.
     */
    ShowState() {
        const t = this;
        const q = t.Q;
        if ((t.Search.Get() || "") !== q.Text)
            t.Search.Set(q.Text, true);
        t.Range.Set(q.Range, true);
        t.CondB.SetText(q.Conds.length
            ? _T("Conditions ({0})", q.Conds.length, "Text of a button that opens a dialog with filter conditions. {0} is the number of conditions")
            : _TF("Conditions", "Text of a button that opens a dialog with filter conditions"));
        t.Reset.SetDisabled(!t.IsFiltered && q.Range !== "custom", _TF("There are no filters", "Why the button that removes all filters of the audit browser is disabled"));
        const chips = [];
        const chip = (text, title, remove) => chips.push(new Sw.Chip({ Text: text, Title: title, Removable: true, Pressable: false, OnRemove: () => {
            remove();
            t.Changed();
        } }).E);
        if (q.Range === "custom")
            chip((q.From ? AuditBrowser.TimeText(new Date(q.From)) : "…") + " – " + (q.To ? AuditBrowser.TimeText(new Date(q.To)) : "…"),
                AuditBrowser.Ranges.find(x => x.Value === "custom").Text, () => {
                    q.Range = "7d";
                    q.From = q.To = null;
                });
        for (const s of q.Values)
            for (const v of s.Values) {
                const text = AuditBrowser.Titles[s.Field] + (s.Exclude ? " ≠ " : ": ") + AuditBrowser.ValueText(s.Field, v);
                chip(Sw.Page.LinkText(text, 60), text, () => t.RemoveValue(s.Field, v));
            }
        q.Conds.forEach((c, i) => {
            const text = t.CondText(c);
            chip((i > 0 && q.Match === "any" ? _TF("or", "Word between two filter conditions where one of them must match") + " " : "") + Sw.Page.LinkText(text, 60), text, () => q.Conds.splice(q.Conds.indexOf(c), 1));
        });
        t.Chips.replaceChildren(...chips);
        t.ShowFacets();
    }

    /**
     * The text of a condition.
     * @param {object} c - The condition.
     * @returns {string} The text.
     */
    CondText(c) {
        const op = AuditBrowser.Ops.find(x => x.Op === c.Op && !!x.Invert === !!c.Invert);
        const v = AuditBrowser.TypeOf(c.ColName) === "d" && !isNaN(Date.parse(c.Value)) ? AuditBrowser.TimeText(new Date(c.Value)) : AuditBrowser.ValueText(c.ColName, c.Value);
        return AuditBrowser.Titles[c.ColName] + " " + (op ? op.Text : "?") + " " + v;
    }

    /**
     * Load the totals, the time line and the value lists.
     * @param {boolean} [quiet] - A periodic refresh: no error messages.
     * @returns {Promise<void>}
     */
    async Refresh(quiet) {
        const t = this;
        const gen = ++t.Gen;
        try {
            const steps = Math.max(20, Math.min(120, Math.floor(t.Plot.clientWidth / 12) || 60));
            const s = await Sw.Net.Send("../Api/Audit/GetSummary", Object.assign(t.FullQuery(), { Steps: steps }));
            if (gen !== t.Gen || t.Selecting)
                return;
            t.Summary = s;
            t.ShowSummary();
            await t.RefreshFacets(gen);
        }
        catch (e) {
            if (!quiet && gen === t.Gen)
                Sw.Fail(_TF("Failed to search the audit:", "Error message of the audit browser, followed by the reason") + "\n" + (e.message || e));
        }
    }

    /**
     * Load the open value lists.
     * @param {number} [gen] - The request, default the current.
     * @returns {Promise<void>}
     */
    async RefreshFacets(gen) {
        const t = this;
        gen = gen || t.Gen;
        const open = t.FacetOpen;
        const fields = AuditBrowser.FacetFields.filter((f, i) => open ? open[f] : i < 5);
        const load = async (names, max) => {
            if (!names.length)
                return;
            const r = await Sw.Net.Send("../Api/Audit/GetFacets", Object.assign(t.FullQuery(), { Fields: names, MaxValues: max }));
            if (gen !== t.Gen)
                return;
            for (const f of r)
                t.Facets[f.Field] = f;
        };
        //  (lists that are closed are loaded again when they are opened)
        for (const f of Object.keys(t.Facets))
            if (!fields.includes(f))
                delete t.Facets[f];
        await Promise.all([load(fields.filter(f => !t.ShowMore[f]), 6), load(fields.filter(f => t.ShowMore[f]), 100)]);
        if (gen === t.Gen)
            t.ShowFacets();
    }

    /**
     * Show the totals and the time line.
     */
    ShowSummary() {
        const t = this;
        const s = t.Summary;
        const num = n => Sw.Format.Number(n, 0);
        const total = s.Completed + s.Failed + s.InProgress;
        const key = (cls, text, n) => Sw.el("span.ab-key", null, cls ? Sw.el("i." + cls) : null, Sw.el("b", { text: n }), text);
        const head = [
            key(null, _TF("calls", "Text after the number of calls found in the audit browser"), num(total)),
            key("ab-completed", AuditBrowser.ValueText("State", "Completed"), num(s.Completed)),
            key("ab-failed", AuditBrowser.ValueText("State", "Failed"), num(s.Failed)),
        ];
        if (s.InProgress)
            head.push(key("ab-progress", AuditBrowser.ValueText("State", "InProgress"), num(s.InProgress)));
        head.push(
            key(null, _TF("users", "Text after the number of users found in the audit browser"), num(s.Users)),
            key(null, _TF("clients", "Text after the number of clients (computers, phones) found in the audit browser"), num(s.Clients)));
        //  (only if the stored times are exact enough)
        if (t.Info.TimeResolution < 1000 && s.AverageDuration !== null && s.AverageDuration !== undefined) {
            head.push(key(null, _TF("average", "Text after the average duration of the calls found in the audit browser"), AuditBrowser.DurationText(s.AverageDuration)));
            head.push(key(null, _TF("slowest", "Text after the duration of the slowest call found in the audit browser"), AuditBrowser.DurationText(s.MaxDuration)));
        }
        t.ChartHead.replaceChildren(...head);
        t.HideTip();
        const steps = s.Steps || [];
        const max = steps.reduce((a, x) => Math.max(a, x.Completed + x.Failed + x.InProgress), 0);
        const part = (cls, n) => n ? Sw.el("i." + cls, { style: { height: (n / max * 100) + "%" } }) : null;
        //  (bottom up: completed, failed, in progress)
        t.Plot.replaceChildren(...steps.map(x => Sw.el("div.ab-step", null, part("ab-progress", x.InProgress), part("ab-failed", x.Failed), part("ab-completed", x.Completed))));
        t.ChartE.hidden = !steps.length;
        if (!steps.length)
            return;
        const first = new Date(steps[0].Time);
        const last = new Date(new Date(steps[steps.length - 1].Time).getTime() + s.StepSeconds * 1000);
        t.Axis.replaceChildren(Sw.el("span", { text: AuditBrowser.TimeText(first, true) }),
            Sw.el("span", { text: _T("Highest {0}", num(max), "Text below a chart of the number of calls over time. {0} is the highest number of calls") }),
            Sw.el("span", { text: AuditBrowser.TimeText(last, true) }));
    }

    /**
     * Show the value lists (in the page and in the dialog, if it is open).
     */
    ShowFacets() {
        const t = this;
        for (const d of document.querySelectorAll("details.ab-facet")) {
            const field = d.dataset.field;
            const list = d.querySelector(".ab-values");
            const badge = d.querySelector(".sw-count");
            const sel = {};
            for (const s of t.Q.Values)
                if (s.Field === field)
                    for (const v of s.Values)
                        sel[v] = s.Exclude ? "exclude" : "select";
            const n = Object.keys(sel).length;
            badge.hidden = !n;
            badge.textContent = n;
            const f = t.Facets[field];
            const values = f ? f.Values.slice() : [];
            for (const v of Object.keys(sel))
                if (!values.some(x => x.Value === v))
                    values.push({ Value: v, Count: 0, Failed: 0 });
            const rows = values.map(v => t.FacetRow(field, v, sel[v.Value]));
            if (f && (f.More || t.ShowMore[field]))
                rows.push(Sw.el("li", null, new Sw.Button({
                    Text: t.ShowMore[field]
                        ? _TF("Show fewer", "Text of a button that shows fewer values in a list")
                        : _TF("Show more", "Text of a button that shows more values in a list"),
                    Kind: "ghost", Small: true, OnClick: () => {
                        t.ShowMore[field] = !t.ShowMore[field];
                        return t.RefreshFacets();
                    },
                }).E));
            if (!rows.length && f)
                rows.push(Sw.el("li", null, Sw.el("span.sw-muted.ab-value", { text: _TF("No values", "Shown instead of a list of values when there are none") })));
            //  The keyboard place is kept when the list is updated
            const a = list.contains(document.activeElement) ? document.activeElement.dataset.key : null;
            list.replaceChildren(...rows);
            if (a) {
                const e = Array.from(list.querySelectorAll("[data-key]")).find(x => x.dataset.key === a);
                if (e)
                    e.focus({ preventScroll: true });
            }
        }
    }

    /**
     * A row of a value list.
     * @param {string} field - The field.
     * @param {object} v - The value (ApiAuditFacetValue).
     * @param {string} [sel] - "select", "exclude" or undefined.
     * @returns {HTMLElement} The row.
     */
    FacetRow(field, v, sel) {
        const t = this;
        const text = AuditBrowser.ValueText(field, v.Value);
        const title = [text, v.Failed
            ? _T("{0} calls, {1} failed", v.Count, v.Failed, "Tool tip of a value in a list. {0} is the number of calls, {1} is the number of calls that failed")
            : _T("{0} calls", v.Count, "Tool tip of a value in a list. {0} is the number of calls")].join("\n");
        const b = Sw.el("button.ab-value" + (sel === "exclude" ? ".is-excluded" : ""), {
            type: "button", title: title,
            dataset: { key: "v" + v.Value },
            attrs: { "aria-pressed": sel ? "true" : "false" },
            on: {
                click: ev => {
                    if (Sw.ModifierClick(ev))
                        return;
                    t.ToggleValue(field, v.Value, sel === "exclude");
                },
            },
        }, Sw.el("span" + (v.Value === "" ? ".ab-none" : ""), { text: text }), Sw.el("small", { text: Sw.Format.Number(v.Count, 0) }));
        const x = new Sw.Button({
            Icon: "minus", Kind: "ghost", Small: true,
            Title: _T("Exclude {0}", text, "Tool tip of a button that excludes all calls with a value. {0} is the value"),
            OnClick: () => {
                t.ToggleValue(field, v.Value, true);
            },
        });
        x.E.dataset.key = "x" + v.Value;
        return Sw.el("li", null, b, x.E);
    }

    // ------------------------------------------------------------------ dialogs

    /**
     * Edit the custom time range.
     * @returns {Promise<void>}
     */
    async EditRange() {
        const t = this;
        const q = t.Q;
        const query = t.Query();
        const from = new Sw.TextBox({ Label: _TF("From", "Label of the start time of a time range"), Type: "datetime-local", Value: AuditBrowser.InputTime(query.From) });
        const to = new Sw.TextBox({ Label: _TF("To", "Label of the end time of a time range"), Type: "datetime-local", Value: AuditBrowser.InputTime(query.To) });
        from.Input.step = to.Input.step = "1";
        const r = await Sw.Dialog.Show({
            Title: _TF("Custom time range", "Title of a dialog where the start and end time of a time range is entered"),
            Content: Sw.el("div.ab-conds", null, from.E, to.E, Sw.el("p.ab-help", { text: _TF("Leave a time empty for no limit. Times are in your time zone.", "Help text of a dialog where the start and end time of a time range is entered") })),
            Buttons: [
                { Text: _TF("Cancel", "Text of a button that closes a dialog without any changes"), Dismiss: true },
                { Text: _TF("Apply", "Text of a button that uses what is entered in a dialog"), Kind: "primary", Value: true },
            ],
        });
        const date = b => {
            const d = b.Get() ? new Date(b.Get()) : null;
            return d && !isNaN(d) ? d : null;
        };
        if (!r) {
            t.Range.Set(q.Range, true);
            return;
        }
        if (!date(from) && !date(to)) {
            q.Range = "all";
            q.From = q.To = null;
            t.Changed();
            return;
        }
        t.SetRange(date(from), date(to));
    }

    /**
     * Edit the conditions.
     * @returns {Promise<void>}
     */
    async EditConditions() {
        const t = this;
        const rows = [];
        const list = Sw.el("div.ab-conds");
        const add = c => {
            const row = { E: Sw.el("div.ab-cond") };
            const field = new Sw.Select({
                Items: Object.keys(AuditBrowser.Titles).map(x => ({ Value: x, Text: AuditBrowser.Titles[x] })), Value: c.ColName,
                AriaLabel: _TF("Field", "Accessible name of a select box with the field of a filter condition"),
                OnChange: () => build(null, null),
            });
            const rest = Sw.el("span.sw-row.sw-start");
            const valueE = Sw.el("div.ab-cond-value");
            const build = (opKey, value) => {
                const type = AuditBrowser.TypeOf(field.Get());
                const ops = AuditBrowser.Ops.filter(x => x.T.includes(type));
                row.Op = new Sw.Select({
                    Items: ops.map(x => ({ Value: x.Key, Text: x.Text })), Value: ops.some(x => x.Key === opKey) ? opKey : ops[0].Key,
                    AriaLabel: _TF("Operation", "Accessible name of a select box with the operation of a filter condition"),
                });
                const label = _TF("Value", "Accessible name of the value of a filter condition");
                if (type === "s")
                    row.Value = new Sw.Select({ Items: ["Completed", "Failed", "InProgress"].map(x => ({ Value: x, Text: AuditBrowser.ValueText("State", x) })), Value: value || "Failed", AriaLabel: label });
                else if (type === "d") {
                    row.Value = new Sw.TextBox({ Type: "datetime-local", Value: AuditBrowser.InputTime(value), AriaLabel: label });
                    row.Value.Input.step = "1";
                }
                else
                    row.Value = new Sw.TextBox({ Value: value || "", InputMode: type === "n" ? "decimal" : undefined, AriaLabel: label, SpellCheck: false });
                rest.replaceChildren(row.Op.E);
                valueE.replaceChildren(row.Value.E);
            };
            row.Get = () => {
                const op = AuditBrowser.Ops.find(x => x.Key === row.Op.Get());
                let v = String(row.Value.Get() ?? "");
                if (AuditBrowser.TypeOf(field.Get()) === "d") {
                    const d = new Date(v);
                    v = !v || isNaN(d) ? "" : d.toISOString();
                    if (!v)
                        return null;
                }
                else if (v === "" && AuditBrowser.TypeOf(field.Get()) !== "t")
                    return null;
                return { ColName: field.Get(), Op: op.Op, Invert: !!op.Invert, Value: v };
            };
            const remove = new Sw.Button({
                Icon: "close", Kind: "ghost", Title: _TF("Remove the condition", "Tool tip of a button that removes a filter condition"), OnClick: () => {
                    rows.splice(rows.indexOf(row), 1);
                    row.E.remove();
                },
            });
            build((c.Invert ? "!" : "") + c.Op, c.Value);
            row.E.append(field.E, rest, valueE, remove.E);
            rows.push(row);
            list.append(row.E);
            return row;
        };
        for (const c of t.Q.Conds)
            add(c);
        if (!t.Q.Conds.length)
            add({ ColName: "UserName", Op: 0, Invert: false, Value: "" });
        const match = new Sw.Segmented({
            Items: [
                { Value: "all", Text: _TF("All must match", "Option of filter conditions: all conditions must match") },
                { Value: "any", Text: _TF("Any may match", "Option of filter conditions: at least one condition must match") },
            ],
            Value: t.Q.Match, AriaLabel: _TF("How conditions are combined", "Accessible name of the options of how filter conditions are combined"),
        });
        const more = new Sw.Button({
            Text: _TF("Add condition", "Text of a button that adds a filter condition"), Icon: "plus", OnClick: () => {
                add({ ColName: "Api", Op: 6, Invert: false, Value: "" }).Value.Focus();
            },
        });
        const r = await Sw.Dialog.Show({
            Title: _TF("Conditions", "Title of a dialog with filter conditions"),
            Wide: true,
            Content: Sw.el("div.ab-conds", null, Sw.el("div.sw-row.sw-start", null, match.E), list, Sw.el("div.sw-row.sw-start", null, more.E)),
            Buttons: [
                { Text: _TF("Cancel", "Text of a button that closes a dialog without any changes"), Dismiss: true },
                { Text: _TF("Apply", "Text of a button that uses what is entered in a dialog"), Kind: "primary", Value: true },
            ],
        });
        if (!r)
            return;
        t.Q.Match = match.Get();
        //  (a text that is empty is only a condition if it was entered on purpose: is / is not)
        t.Q.Conds = rows.map(x => x.Get()).filter(x => x && (x.Value !== "" || x.Op === 0)).filter(x => !(x.ColName === "UserName" && x.Value === "" && rows.length === 1 && !t.Q.Conds.length));
        t.Changed();
    }

    /**
     * Show the value lists in a dialog (narrow screens).
     * @returns {Promise<void>}
     */
    async FacetDialog() {
        const t = this;
        const e = Sw.el("div.ab-facets");
        t.BuildFacets(e);
        const p = Sw.Dialog.Show({
            Title: _TF("Filter by value", "Title of a dialog with lists of values that can be used as filters"),
            Content: Sw.el("div.ab-dialog-facets", null, e),
            AutoDirty: false,
            Buttons: [{ Text: _TF("Close", "Text of a button that closes a dialog"), Dismiss: true }],
        });
        t.ShowFacets();
        t.RefreshFacets();
        await p;
    }

    /**
     * A click (or Enter) on a row of the table: show the details, if it isn't an action of the cell.
     * @param {Event} ev - The event.
     */
    RowClick(ev) {
        const t = this;
        const x = ev.target;
        if (!x || !x.closest || !Sw.IsPureEvent(ev) || ev.defaultPrevented)
            return;
        const row = x.closest("tbody tr, .sw-table-card");
        if (!row || !row.SwRow || x.closest("a, button, input, select, [role=button], [role=link], .sw-cell-link, .sw-capped"))
            return;
        if (Sw.HasSelectionIn(row))
            return;
        const i = t.Table.Cols.findIndex(c => c.Name === "Id");
        const id = Array.isArray(row.SwRow.Values) ? row.SwRow.Values[i] : row.SwRow.Id;
        ev.preventDefault();
        ev.stopPropagation();
        t.ShowDetails(id);
    }

    /**
     * Show a call.
     * @param {number} id - The id of the call.
     * @returns {Promise<void>}
     */
    async ShowDetails(id) {
        const t = this;
        let d = null;
        try {
            d = await Sw.Net.Send("../Api/Audit/GetEntry", id);
        }
        catch (e) {
            Sw.Fail(_TF("Failed to get the call:", "Error message of the audit browser, followed by the reason") + "\n" + (e.message || e));
            return;
        }
        if (!d) {
            Sw.Fail(_TF("The call does not exist, it may have been pruned.", "Error message of the audit browser when the details of a call are not found"));
            return;
        }
        let handle = null;
        const state = AuditBrowser.StateName(d.State);
        const prop = (field, value, filter, content) => {
            if (value === null || value === undefined || value === "")
                return null;
            const text = AuditBrowser.ValueText(field, value);
            const dd = Sw.el("dd", null, content || text);
            const b = filter ? new Sw.Button({
                Icon: "filter", Kind: "ghost", Small: true,
                Title: _T("Only show calls with {0}", AuditBrowser.Titles[field] + ": " + text, "Tool tip of a button that filters on a value. {0} is the name and the value, ex: User: anna"),
                OnClick: () => {
                    handle.Close();
                    const s = t.Q.Values.find(v => v.Field === field && !v.Exclude);
                    if (!s || !s.Values.includes(String(value)))
                        t.ToggleValue(field, String(value));
                },
            }).E : null;
            return Sw.el("div", null, Sw.el("dt", { text: AuditBrowser.Titles[field] }), dd, Sw.CopyButton({ Value: String(value), What: AuditBrowser.Titles[field], Small: true }).E || null, b);
        };
        const section = (title, ...props) => {
            const p = props.filter(x => x);
            return p.length ? Sw.el("section", null, Sw.el("h3", { text: title }), Sw.el("dl.ab-props", null, ...p)) : null;
        };
        const time = s => AuditBrowser.TimeText(new Date(s));
        const json = (field, value) => {
            if (value === null || value === undefined || value === "")
                return null;
            let text = value;
            try {
                text = JSON.stringify(JSON.parse(value), null, 4);
            }
            catch {
                //  (an error message or capped data)
            }
            return Sw.el("section", null,
                Sw.el("div.ab-json-head", null, Sw.el("h3", { text: AuditBrowser.Titles[field] }), Sw.CopyButton({ Value: value, What: AuditBrowser.Titles[field], Small: true }).E || null),
                Sw.el("pre.ab-json.sw-selectable", { text: text, attrs: { tabindex: "0" } }));
        };
        const kind = { Completed: "ok", Failed: "danger", InProgress: "info" }[state];
        const content = Sw.el("div.ab-details", null,
            Sw.el("div.ab-cols", null,
                section(_TF("Call", "Title of the part with information about the call itself, in the details of an audited server call"),
                    prop("State", state, true, Sw.Badge({ Text: AuditBrowser.ValueText("State", state), Kind: kind })),
                    prop("Api", d.Api, true),
                    prop("Group", d.Group, true),
                    prop("Begin", time(d.Begin)),
                    prop("End", d.End ? time(d.End) : null),
                    prop("Duration", d.Duration !== null && d.Duration !== undefined && t.Info.TimeResolution < 1000 ? Sw.Format.Number(d.Duration, 1) : null),
                    prop("Id", d.Id)),
                section(_TF("User and client", "Title of the part with information about the user and the client, in the details of an audited server call"),
                    prop("UserName", d.UserName, true),
                    prop("UserGuid", d.UserGuid, true),
                    prop("Session", d.Session, true),
                    prop("Ip", d.Ip, true),
                    prop("DeviceId", d.DeviceId, true),
                    prop("ClientId", d.ClientId, true),
                    prop("Language", d.Language, true),
                    prop("TimeZone", d.TimeZone, true),
                    prop("UserAgent", d.UserAgent, true))),
            json("Input", d.Input),
            json("Output", d.Output));
        handle = Sw.Dialog.Open({
            Title: d.Api,
            Wide: true,
            Content: content,
            AutoDirty: false,
            Buttons: [{ Text: _TF("Close", "Text of a button that closes a dialog"), Dismiss: true }],
        });
        await handle.Result;
    }

    /**
     * The name of a state (the server may send the number).
     * @param {number|string} state - The state.
     * @returns {string} The name.
     */
    static StateName(state) {
        return ["InProgress", "Completed", "Failed"][state] || String(state);
    }
}

/**
 * Entry point of audit.html.
 * @returns {Promise<void>}
 */
function auditMain() {
    return Sw.Page.Main(() => new AuditBrowser().Build(), { Loading: true });
}
