/**
 * @file explore/api.js - page script of explore/api.html, the API inspector: describes one API (method, url, auth, cache, compression,
 * C# signature) and calls it with an argument edited in a form (the object editor of edit/Edit.js, `Sw.Edit`).
 *
 * Entry: `<body onload="apiMain()">`. Url parameter `q` = API url, resolved with `Sw.Url.SitePath` like table.html's `q`: relative to
 * the site root (ex: `Api/debug/explore/GetIcons`) or link style relative to this page (`../Api/debug/explore/GetIcons`), both the same API.
 * Server: `Api/debug/GetApiInfo` (POST the url) -> ApiInfo `{Uri, Auth, Mime, Desc, ClientCacheDuration, RequestCacheDuration,
 * CompPreference, Arg, Return, ArgName, ArgSummary}`, then the API itself.
 * Uses Sw (sw/core.js + sw/ui.js: Sw.PageHeader, Sw.EmptyState, Sw.Tabs, Sw.Badge, Sw.Button) and the public api of edit/Edit.js
 * (`Sw.Edit`, `Sw.Edit.Options`, `Sw.Edit.FormFooter`, `Sw.Edit.CheckJson` / `JsonLines`, `Sw.TypeDoc.AddTypeName` / `AddTextElement` from
 * edit/type.js, loaded by Edit.js).
 * Layout (Design v2, PORTING.md B10 / PORTING.md B14, explore/api.css): a page header (the method name, the description, actions: Copy url, All
 * APIs), a meta line (url, roles, cache, compression, the "How to call" link to explore/apihelp.html), the C# signature, then two cards side
 * by side (one column below 1100 px): Input (Parameters / JSON / Curl tabs with the change count, the form footer Reset ... Send; an API
 * without an argument: Parameters (the "No parameters" state) / Curl) and Result (Result / JSON tabs with the status and the time, an empty
 * state before the first call, a null state, the error state, the "Called at ... by ..." footer).
 * The JSON tabs (the editable argument, the read only result) are Ace editors (Sw.AceEditor, edit/AceEditor.js, loaded on demand; the
 * Sw.CodeEditor of ui/code.js, else a text area, when Ace is not available) with a Copy button.
 * The Curl tab: one curl command (POSIX shell, one line, no pipes) that makes the call with the current argument; options: GET / POST,
 * the result format (the Accept header, hidden when the service has one), deflate of a GET argument (deflated by the page, the "?_d"
 * binary argument; only when the service reads deflate) and "Compressed result" (--compressed). The serializers and compressions come
 * from `Api/debug/GetApiIoInfo`; recomputed on every change.
 * Storage: `localStorage["SysWeaver.Api." + q]` = the last sent argument (without `$type`), `localStorage["SysWeaver.Tabs.Api.Args"]` /
 * `["SysWeaver.Tabs.Api.NoArgs"]` / `["SysWeaver.Tabs.Api.Results"]` = the selected tab index, `["SysWeaver.Api.Curl"]` = the curl options,
 * `["SysWeaver.Api.Split"]` = the width of the input card (Sw.Split fraction), `["SysWeaver.Api.ArgsLines"]` / `["SysWeaver.Api.ResultLines"]` =
 * the most lines of the argument / result json (the size grip of Sw.AceEditor; "all" or a count).
 * Keyboard: Ctrl+Enter (Cmd+Enter) sends the call from anywhere on the page.
 * Styled by explore/api.css.
 */

(() => {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /**
     * Tabs that remember the selected tab index in local storage ("SysWeaver.Tabs." + key, the old common/tab.js key).
     * @param {string} key The save key.
     * @returns {object} The Sw.Tabs (call `Restore()` after adding the tabs).
     */
    function savedTabs(key) {
        const k = "SysWeaver.Tabs." + key;
        let restoring = true;
        const tabs = new Sw.Tabs({
            Fill: false,
            Class: "api-tabs",
            OnSelect: tab => {
                if (!restoring)
                    Sw.Storage.Local.Set(k, String(tabs.Items.indexOf(tab)));
            },
        });
        /** Select the stored tab. */
        tabs.Restore = () => {
            const i = parseInt(Sw.Storage.Local.Get(k, "0"), 10) | 0;
            const tab = tabs.Items[Math.max(0, Math.min(tabs.Items.length - 1, i))];
            if (tab)
                tabs.Select(tab);
            restoring = false;
        };
        return tabs;
    }

    /** @type {?Promise<boolean>} The load of the Ace editor (edit/AceEditor.js and Ace, once). */
    let aceLoad = null;

    /**
     * Load Sw.AceEditor (edit/AceEditor.js, which loads Ace and its theme) once.
     * @returns {Promise<boolean>} True when Ace editors can be created.
     */
    function loadAce() {
        if (!aceLoad)
            aceLoad = (async () => {
                try {
                    if (!Sw.AceEditor)
                        await Sw.Net.IncludeJs(null, "../edit/AceEditor.js");
                    return !!Sw.AceEditor && await Sw.AceEditor.Load();
                }
                catch (e) {
                    console.warn("The Ace editor (edit/AceEditor.js) could not be loaded: " + e);
                    return false;
                }
            })();
        return aceLoad;
    }

    /**
     * A small "Copy" button (copies a text to the clipboard).
     * @param {function(): string} get - The text to copy.
     * @param {string} title - The tool tip.
     * @returns {object} The Sw.Button.
     */
    function copyButton(get, title) {
        return new Sw.Button({
            Text: _TF("Copy", "Text of a button of the API inspector that copies a text (json, a curl command) to the clipboard"),
            Icon: "copy", Kind: "ghost", Small: true, Title: title,
            OnClick: () => Sw.Clipboard.Copy(get(), { Generic: true }),
        });
    }

    /**
     * The read only json of a result: a tool row with Copy above an Ace editor (json mode, folding, its own scroll bars; it never gets
     * wider than its card), else a highlighted block.
     * @param {*} obj The value.
     * @returns {Promise<HTMLElement>} The element.
     */
    async function jsonView(obj) {
        const text = JSON.stringify(obj, null, 2) ?? "null";
        const box = el("div.api-jsonview");
        box.append(el("div.api-codebar", null, copyButton(() => text,
            _TF("Copy the result as json text to the clipboard", "Tool tip of the Copy button of the result json of the API inspector")).E));
        if (await loadAce()) {
            const ed = new Sw.AceEditor({
                Mode: "json", ReadOnly: true, MinLines: 3, MaxLines: 40, Class: "api-ace", Resizable: true, SizeKey: "SysWeaver.Api.ResultLines",
                Label: _TF("Result as json", "Accessible name of the read only json text of the result of an API call"),
            });
            ed.Set(text);
            box.append(ed.E);
        }
        else {
            const pre = el("pre.sw-code-json.api-code");
            pre.innerHTML = Sw.Format.JsonToHtml(text);
            box.append(pre);
        }
        return box;
    }

    /**
     * A small badge of the meta line (method, role, cache, compression); a click copies a value.
     * @param {string} text The text.
     * @param {string} title The tooltip (the copy hint is appended).
     * @param {*} copy The value to copy.
     * @param {string} [cls] Extra class (api-method, api-cache, api-comp).
     * @returns {HTMLElement} The badge.
     */
    function tag(text, title, copy, cls) {
        const t = el("span.sw-badge.api-tag", { text, class: cls || null });
        t.title = title + "\n\n" + Sw.Clipboard.CopyOnClick(t, copy, { Keyboard: true });
        return t;
    }

    /**
     * A duration in seconds as the elapsed time text of the TimeSpan cells ("30 seconds", "60 minutes", "24 hours", "7 days"; "∞" for
     * the server's "forever").
     * @param {number} s Seconds.
     * @returns {string} The text.
     */
    function seconds(s) {
        return s > 1500000000 ? "∞" : Sw.Format.TimeSpan(s);
    }

    /**
     * Quote a text for a posix shell (single quotes).
     * @param {string} s The text.
     * @returns {string} The quoted text.
     */
    function shellQuote(s) {
        return "'" + String(s).replace(/'/g, "'\\''") + "'";
    }

    /**
     * The API page.
     */
    class ApiPage {
        /**
         * @param {string} url The API url (relative to the root).
         * @param {object} info The ApiInfo.
         */
        constructor(url, info) {
            /** @type {string} The API url. */
            this.Url = url;
            /** @type {object} The ApiInfo. */
            this.Info = info;
            /** @type {string} The absolute API url. */
            this.Full = Sw.Url.Root + info.Uri;
            /** @type {string} Storage key of the last argument. */
            this.Key = "SysWeaver.Api." + url;
            /** @type {boolean} The API takes an argument. */
            this.HaveArg = !!info.Arg;
            /** @type {boolean} The API returns raw data of a mime type. */
            this.IsRaw = !!info.Mime;
            /** @type {object|null} The argument editor. */
            this.Input = null;
            /** @type {HTMLElement} The page root (a Design v2 form page; the dashboard width, so the cards have room to be resized). */
            this.E = el("main.api-page.sw-page-dash.sw-root");
            /** @type {HTMLElement} The body of the result card. */
            this.Results = el("div.api-results", { attrs: { "aria-live": "polite" } });
            /** @type {string} The method name (the last part of the url). */
            const parts = url.split("/");
            this.Name = parts[parts.length - 1].split(".")[0] || url;
        }

        /**
         * Build the page.
         * @returns {Promise<void>}
         */
        async Build() {
            const t = this;
            const d = t.Info;
            document.body.append(t.E);
            t.SendB = new Sw.Button({
                Text: _TF("Send API call", "The text of a button that when clicked will make a request to and API"),
                Title: _TF("Call this API and display any results below.", "The tool tip text of a button that when clicked will make a request to and API") + " (" + Sw.Keys.Format("Mod+Enter") + ")",
                Icon: "send",
                Kind: "primary",
                OnClick: () => t.Send(),
            });
            t.E.append(t.PageHeader(), t.MetaLine(), t.Signature());
            //  The cards with a bar between them: drag it (or arrow keys) to give one more width, remembered; one column below 1100 px
            const split = new Sw.Split({
                A: await t.InputCard(), B: t.ResultCard(), Min: 280, Key: "SysWeaver.Api.Split",
                Label: _TF("Width of the input and result cards", "Accessible name of the bar between the input and the result card of the API inspector"),
            });
            split.E.classList.add("api-grid");
            split.Bar.title = _TF("Drag to make the input or the result wider. Double click: the same width.", "Tool tip of the bar between the input and the result card of the API inspector");
            t.E.append(split.E);
            //  (Send is always enabled, like the original page: a refused call shows the server's answer)
            Sw.Keys.Add("Mod+Enter", () => t.SendB.Click(), { Title: _TF("Send API call", "The text of a button that when clicked will make a request to and API"), InInputs: true });
        }

        /**
         * The page header: the method name, the description (a click copies it) and the page actions.
         * @returns {HTMLElement} The header.
         */
        PageHeader() {
            const t = this;
            const d = t.Info;
            const actions = [
                new Sw.Button({
                    Text: _TF("Copy url", "Text of a button on the API inspector page that copies the full url of the API"), Icon: "link",
                    Title: _T("Copy the full url of the API to the clipboard: {0}", t.Full, "Tool tip of the Copy url button of the API inspector.{0} is replaced with the url"),
                    OnClick: () => Sw.Clipboard.Copy(t.Full),
                }),
                new Sw.Button({
                    Text: _TF("All APIs", "Text of a button on the API inspector page that opens the table of all APIs"), Icon: "table", Kind: "ghost",
                    Title: _TF("Open the list of all APIs of the service", "Tool tip of a button that opens the table listing all APIs of the service"),
                    OnClick: () => Sw.Page.Navigate("table.html?q=" + encodeURIComponent("../Api/debug/ApiTable"), "_self"),
                }),
            ];
            const h = Sw.PageHeader({ Title: t.Name, Description: d.Desc || "", Actions: actions, Class: "api-ph" });
            h.TitleE.title = t.Url + (d.Return && d.Return.Summary ? "\n" + _TF("Returns", "A prefix to a tool tip description of what an API returns") + ": " + d.Return.Summary : "");
            if (d.Desc)
                h.DescE.title = Sw.Clipboard.CopyOnClick(h.DescE, d.Desc);
            t.HeaderC = h;
            return h.E;
        }

        /**
         * The meta line: url (copy on click), the roles, cache and compression badges, the "How to call" link (explore/apihelp.html:
         * GET and POST, serializers, compression).
         * @returns {HTMLElement} The line.
         */
        MetaLine() {
            const d = this.Info;
            const s = el("div.api-meta");
            const u = el("code.api-url", { text: d.Uri });
            u.title = _TF("This is the path relative to the site's base address.", "A tool tip description of the relative URL to an API end point") + "\n" + _TF("The full path is:", "Second part of the tool tip of the url of an API, followed by the full url") + "\n\"" + this.Full + "\"\n\n" + Sw.Clipboard.CopyOnClick(u, this.Full, { Keyboard: true });
            s.append(u);
            //  The tags of the data tables' auth columns (Sw.Cells.AuthTag, C# TableDataAuth): the accent "Open" tag, the "Any logged in
            //  user" tag or one tag per token
            const roles = (d.Auth ?? "").split(",").map(x => x.trim()).filter(x => x);
            if (roles.length)
                roles.forEach(x => {
                    const t = el("span.sw-tag.api-tag", { text: x });
                    t.title = _T('The user must have the "{0}" security token to use this API.', x, "A tool tip description on a security token that is required to use this API. {0} is the name of the token, ex: Admin") + "\n\n" + Sw.Clipboard.CopyOnClick(t, x, { Keyboard: true });
                    s.append(t);
                });
            else
                s.append(Sw.Cells.AuthTag(d.Auth === undefined ? null : d.Auth));
            const cd = d.ClientCacheDuration, rd = d.RequestCacheDuration, cp = d.CompPreference ?? "";
            if (cd > 0)
                s.append(tag(_T("Client cache {0}", seconds(cd), "The text on a badge that explains that the API response will instruct the web client to cache the result for the specified duration.{0} is replaced with the duration, ex: 5 s"),
                    cd === 1
                        ? _TF("Client's are instructed to cache the response of the API call for one second.", "A tool tip description on a tag explains that the API response will instruct the web client to cache the result for one second")
                        : _T("Client's are instructed to cache the response of the API call for {0} seconds.", cd, "A tool tip description on a tag explains that the API response will instruct the web client to cache the result for the specified duration.{0} is replaced with the number of seconds that the result should be cached and will never be 1"),
                    cd, "api-cache"));
            if (rd > 0)
                s.append(tag(_T("Server cache {0}", seconds(rd), "The text on a badge that explains that the API response will be cached on the server for the specified duration.{0} is replaced with the duration, ex: 5 s or ∞"),
                    rd === 1
                        ? _TF("The service will cache the response of the API call for one second.", "A tool tip description on a tag that explains that the API response will be cached on the service one second")
                        : _T("The service will cache the response of the API call for {0} seconds.", rd, "A tool tip description on a tag that explains that the API response will cached on the service for the specified duration.{0} is replaced with the number of seconds that the result should be cached and will never be 1"),
                    rd, "api-cache"));
            const comps = cp.split(",").map(m => m.trim()).filter(x => x).map(m => {
                const kv = m.split(":");
                return { Name: kv[0].trim(), Effort: (kv[1] ?? "").trim() };
            });
            if (comps.length)
                s.append(tag(comps.map(c => c.Name.toLowerCase()).join(" · "),
                    comps.map(c => _T("The service compresses the response using {0}-compression if the client accept it.", c.Name.toUpperCase(), "A tool tip description on a tag that explains the compression method used on an API.{0} is repladced with the compression method name (Brotli, Deflate, Gzip)") + " " +
                        _T("The compression quality used is: {0}.", c.Effort, "A tool tip description on a tag that explains the compression quality used on an API.{0} is repladced with the compression quality: fast, normal, best etc")).join("\n") + "\n" +
                    _TF("If the client accepts multiple compression methods, the first supported method will be used.", "A tool tip description on a tag that explains that the first client supported compression method is used"),
                    comps.map(c => c.Name + ": " + c.Effort).join(", "), "api-comp"));
            //  Every API can be called with GET or POST: the help page explains both, the serializers and the compression methods
            const help = el("a.api-howto", {
                href: "apihelp.html?q=" + encodeURIComponent(this.Url),
                title: _TF("How to call the APIs of this service: GET and POST, the argument and result formats (serializers) and compression", "Tool tip of the How to call link of the API inspector page"),
            }, Sw.Icon("info"), el("span", { text: _TF("How to call", "Text of a link on the API inspector page to the page that explains how to call APIs (GET, POST, serializers, compression)") }));
            s.append(help);
            return s;
        }

        /**
         * The C# signature: `ReturnType Method(ArgType argName);` with linked type names (wraps on narrow screens).
         * @returns {HTMLElement} The section.
         */
        Signature() {
            const d = this.Info;
            const T = Sw.TypeDoc;
            const s = el("div.api-signature", { title: _TF("C# function signature", "A tool tip description of block of code that contains a C# function signature") });
            if (d.Return) {
                if (this.IsRaw)
                    //  Plain text (no web search link: the page works offline and sends nothing to another site)
                    s.append(el("span.api-mime", {
                        text: "[" + d.Mime.split(";")[0] + "]",
                        title: _T('This API returns raw data in the "{0}" format.', d.Mime, "A tool tip description on a tag that explains that a API returns data using the specified MIME type.{0} is replaced with the MIME type"),
                    }));
                else
                    T.AddTypeName(s, d.Return.TypeName, true);
            }
            else
                T.AddTextElement(s, "void");
            T.AddTextElement(s, this.Name,
                this.Url + (d.Return && d.Return.Summary ? "\n" + _TF("Returns", "A prefix to a tool tip description of what an API returns") + ": " + d.Return.Summary : ""), "Signature");
            T.AddTextElement(s, "(", null, "Control");
            if (this.HaveArg) {
                T.AddTypeName(s, d.Arg.TypeName, true);
                T.AddTextElement(s, d.ArgName, d.ArgSummary, "Signature");
            }
            T.AddTextElement(s, ");", null, "Control");
            return s;
        }

        /**
         * The input card: the Parameters / JSON tabs (with the change count) and the form footer (Reset ... Send); an API without an
         * argument gets a "no parameters" state and the same footer, so Send is always in the same place.
         * @returns {Promise<HTMLElement>} The card.
         */
        async InputCard() {
            const t = this;
            const card = el("section.sw-card.api-card.api-input", { attrs: { "aria-label": _TF("Input", "Accessible name of the input card (the parameters of the call) of the API inspector") } });
            if (t.HaveArg) {
                card.append(await t.ArgEditor());
                //  Reset undoes every change of the form (redo brings them back); the JSON tab shows the reset value. The change count is
                //  also shown in the tab bar.
                t.ChangesBadge = el("span.sw-badge.sw-accent.api-changes", { hidden: true, attrs: { "aria-live": "polite" } });
                t.ArgTabs.Tools.append(t.ChangesBadge);
                t.Footer = new Sw.Edit.FormFooter(t.Input, {
                    Actions: [t.SendB],
                    Badge: t.ChangesBadge,
                    //  Send is not a save: it stays when the argument has nothing editable
                    ShowReadOnly: true,
                    OnReset: () => t.ArgTabs && t.ArgTabs.Active === t.JsonTab ? t.ShowJson() : null,
                });
                t.Footer.E.classList.add("api-formfoot");
                card.append(t.Footer.E);
                t.SendActions = t.Footer.Actions;
            }
            else {
                //  Parameters (the "no parameters" state) and Curl tabs
                const tabs = savedTabs("Api.NoArgs");
                t.ArgTabs = tabs;
                tabs.Add({
                    Title: _TF("Parameters", "The text of a tab header that will display an explanation of all parameters for an API"),
                    Content: Sw.EmptyState({
                        Icon: "check", Class: "api-noargs",
                        Title: _TF("No parameters", "Title of the input card of the API inspector for an API that takes no argument"),
                        Text: _TF("This API takes no input. Send the call to see what it returns.", "Text of the input card of the API inspector for an API that takes no argument"),
                    }).E,
                });
                t.AddCurlTab(tabs);
                tabs.Restore();
                card.append(tabs.E);
                t.SendActions = el("div.sw-edit-formfoot-actions", null, t.SendB.E);
                card.append(el("div.sw-edit-formfoot.api-formfoot", { attrs: { role: "group", "aria-label": _TF("Form actions", "Accessible name of the footer of a form (reset, change count, save)") } }, t.SendActions));
            }
            return card;
        }

        /**
         * The result card: Result / JSON tabs (filled by Send) with the status and the time in the tab bar, an empty state before the
         * first call and the "Called at ... by ..." line below.
         * @returns {HTMLElement} The card.
         */
        ResultCard() {
            const t = this;
            t.ResultFoot = el("div.api-result-foot", { hidden: true });
            t.ShowNoResult();
            return el("section.sw-card.api-card.api-output", { attrs: { "aria-label": _TF("Result", "Accessible name of the result card of the API inspector") } }, t.Results, t.ResultFoot);
        }

        /**
         * Show a state (empty, sending) in the result card instead of result tabs.
         * @param {HTMLElement} e The state element.
         */
        ShowResultState(e) {
            const head = el("div.api-card-head", null, el("h2.api-card-title", { text: _TF("Result", "Title of the result card of the API inspector") }));
            this.Results.replaceChildren(head, e);
        }

        /**
         * The argument editor: "Parameters" (the form, initialized with the last sent value) and "JSON" tabs.
         * @returns {Promise<HTMLElement>} The tabs element.
         */
        async ArgEditor() {
            const t = this;
            const d = t.Info;
            const E = Sw.Edit;
            let obj = await E.CreateDefault(d.Arg, null, true);
            const local = Sw.Storage.Local.Get(t.Key, null);
            if (local) {
                try {
                    if (E.IsPrimitive(d.Arg))
                        obj = JSON.parse(local);
                    else {
                        const lobj = JSON.parse(local);
                        delete lobj.$type;
                        Object.assign(obj, lobj);
                    }
                    await E.CleanUp(d.Arg, obj);
                }
                catch (e) {
                    console.warn("Failed to parse previous object, error: " + e);
                }
            }
            t.Input = new E(d.Arg);
            await t.Input.SetObject(obj);
            t.Input.Element.addEventListener("EditChange", () => t.MarkStale());
            //  The JSON tab: a writable json editor (Sw.CodeEditor, ui/code.js, loaded on demand). Switching tabs converts: form -> text
            //  when the JSON tab opens, text -> form when the Parameters tab opens (an invalid text keeps the JSON tab open and says why).
            const json = el("div.api-json");
            t.JsonError = el("p.api-json-error", { attrs: { role: "status" }, hidden: true });
            t.JsonEditor = null;
            /** @type {?string} The text last written to the json editor (unchanged text = nothing to convert back). */
            t.JsonText = null;
            const tabs = savedTabs("Api.Args");
            t.ArgTabs = tabs;
            let jsonTab = null, switching = false;
            const prevSelect = tabs.O.OnSelect;
            tabs.O.OnSelect = async (tab, prev) => {
                prevSelect(tab, prev);
                if (switching)
                    return;
                if (tab === jsonTab)
                    await t.ShowJson();
                else if (prev === jsonTab && !await t.ApplyJson()) {
                    //  Invalid text: stay on the JSON tab (the error is shown below the editor)
                    switching = true;
                    try {
                        tabs.Select(jsonTab);
                    }
                    finally {
                        switching = false;
                    }
                    if (t.JsonEditor)
                        t.JsonEditor.Focus();
                }
                else if (tab === t.CurlTab)
                    await t.Curl.Update();
            };
            //  Copy: the json text as shown (also an unfinished text)
            json.append(el("div.api-codebar", null, copyButton(() => t.JsonEditor ? t.JsonEditor.Get() : t.ArgText(),
                _TF("Copy the parameters as json text to the clipboard", "Tool tip of the Copy button of the parameters json of the API inspector")).E));
            json.append(t.JsonError);
            t.JsonHost = json;
            tabs.Add({
                Title: _TF("Parameters", "The text of a tab header that will display an explanation of all parameters for an API"),
                Tip: _TF("Edit parameters for this API", "The tool tip description of a clickable tab header that when clicked will display an explanation of all parameters for an API"),
                Content: el("div.api-params", null, t.Input.Element),
            });
            jsonTab = tabs.Add({
                Title: _TF("JSON", "The text of a tab header that will display all parameters of an API as formatted JSON text"),
                Tip: _TF("Edit the parameters as json text (converted to and from the form when switching tabs)", "The tool tip description of a clickable tab header that shows the parameters of an API as an editable JSON text"),
                Content: json,
                Select: false,
            });
            t.JsonTab = jsonTab;
            t.AddCurlTab(tabs);
            tabs.Restore();
            return tabs.E;
        }

        /**
         * Add the Curl tab (a curl command line of the call, see CurlPanel).
         * @param {object} tabs - The Sw.Tabs of the input card.
         */
        AddCurlTab(tabs) {
            const t = this;
            t.Curl = new CurlPanel(t);
            t.CurlTab = tabs.Add({
                Title: _TF("Curl", "The text of a tab header of the API inspector that shows a curl command line that makes the call"),
                Tip: _TF("A curl command line that makes this call with the current parameters", "The tool tip of the Curl tab of the API inspector"),
                Content: t.Curl.E,
                Select: false,
            });
            if (!t.HaveArg) {
                //  (the argument tabs update it in their OnSelect)
                const prev = tabs.O.OnSelect;
                tabs.O.OnSelect = async (tab, p) => {
                    prev(tab, p);
                    if (tab === t.CurlTab)
                        await t.Curl.Update();
                };
            }
        }

        /**
         * The argument as json text (2 spaces indent, without the `$type` marker of the editor).
         * @returns {string} The text.
         */
        ArgText() {
            return JSON.stringify(stripType(this.Input.GetObject()), null, 2) ?? "null";
        }

        /**
         * Open the JSON tab: create the json editor (once) and fill it with the form's value.
         * @returns {Promise<void>}
         */
        async ShowJson() {
            const t = this;
            if (!t.JsonEditor) {
                const onChange = () => {
                    t.MarkStale();
                    t.CheckJsonSoon();
                };
                //  The Ace editor of edit/text.html (Sw.AceEditor); without it the Sw code editor (ui/code.js), else a text area
                if (await loadAce()) {
                    t.JsonEditor = new Sw.AceEditor({
                        Mode: "json", TabSize: 2, MinLines: 6, MaxLines: 30, Class: "api-json-editor api-ace", Resizable: true, SizeKey: "SysWeaver.Api.ArgsLines",
                        Placeholder: _TF("The parameters as json", "Placeholder of the json editor of the parameters of an API"),
                        Label: _TF("Parameters as json", "Accessible name of the json editor of the parameters of an API"),
                        OnChange: onChange,
                    });
                }
                else {
                    try {
                        await Sw.Net.IncludeJs(null, "../ui/code.js");
                    }
                    catch (e) {
                        console.warn("The code editor (ui/code.js) could not be loaded: " + e);
                    }
                    if (Sw.CodeEditor) {
                        t.JsonEditor = new Sw.CodeEditor({
                            Language: "json", TabSize: 2, AutoComplete: false,
                            Placeholder: _TF("The parameters as json", "Placeholder of the json editor of the parameters of an API"),
                            OnChange: onChange,
                        });
                        t.JsonEditor.Input.setAttribute("aria-label", _TF("Parameters as json", "Accessible name of the json editor of the parameters of an API"));
                        t.JsonEditor.E.classList.add("api-json-editor");
                    }
                    else {
                        //  A plain text area with the same interface
                        const ta = el("textarea.sw-input.api-json-editor.api-json-plain", { spellcheck: false, attrs: { "aria-label": _TF("Parameters as json", "Accessible name of the json editor of the parameters of an API") } });
                        ta.addEventListener("input", onChange);
                        t.JsonEditor = { E: ta, Get: () => ta.value, Set: v => ta.value = v, Focus: () => ta.focus(), SetMarkers: () => { } };
                    }
                }
                //  After the Copy tool row
                t.JsonHost.firstChild.after(t.JsonEditor.E);
            }
            t.JsonText = t.ArgText();
            t.JsonEditor.Set(t.JsonText);
            if (t.JsonEditor.Resize)
                t.JsonEditor.Resize();
            else {
                //  Sized to the text (4 to 30 lines, 20 px each; the user can resize it)
                const lines = Math.min(30, Math.max(4, t.JsonText.split("\n").length + 1));
                t.JsonEditor.E.style.height = "calc(" + lines + " * var(--sw-code-lh, 20px) + 16px)";
            }
            t.ShowJsonError(null);
        }

        /**
         * Show (or hide, null) the status line below the json editor and mark the line of an error.
         * @param {?{Text: string, Line: ?number, Ok: ?boolean}} err - The error (Ok: the "Valid JSON" line).
         */
        ShowJsonError(err) {
            const t = this;
            //  The status line: the parse / type error, else "Valid JSON" (after an edit)
            t.JsonError.hidden = err === null;
            t.JsonError.classList.toggle("api-json-ok", !!err && !!err.Ok);
            t.JsonError.setAttribute("role", err && !err.Ok ? "alert" : "status");
            t.JsonError.replaceChildren();
            if (err)
                t.JsonError.append(Sw.Icon(err.Ok ? "check" : "warning"), el("span", { text: err.Text }));
            if (t.JsonEditor && t.JsonEditor.SetMarkers)
                t.JsonEditor.SetMarkers(err && !err.Ok && err.Line ? [{ Line: err.Line, Kind: "error", Text: err.Text }] : []);
        }

        /** Check the json text shortly after the last edit (live validation line). */
        CheckJsonSoon() {
            const t = this;
            clearTimeout(t.CheckTimer);
            const seq = t.CheckSeq = (t.CheckSeq || 0) + 1;
            t.CheckTimer = setTimeout(async () => {
                if (!t.JsonEditor)
                    return;
                const r = await t.ParseJson(t.JsonEditor.Get());
                if (seq !== t.CheckSeq)
                    return;
                t.ShowJsonError(r.Ok ? { Ok: true, Text: _TF("Valid JSON", "Status line below the json editor of the parameters of an API when the text is valid") } : r.Error);
            }, 300);
        }

        /**
         * Parse and check the json text against the argument type at every depth (Sw.Edit.CheckJson: member names, value types, enums,
         * dates, durations, guids, collections, nested objects); errors name the member path and point at its line.
         * @param {string} text - The text.
         * @returns {Promise<{Ok: boolean, Value: *, Error: ?{Text: string, Line: ?number}}>} The result.
         */
        async ParseJson(text) {
            const E = Sw.Edit;
            const type = this.Info.Arg;
            let v;
            try {
                v = JSON.parse(text);
            }
            catch (e) {
                const msg = e && e.message ? e.message : String(e);
                //  "... at position 12" / "(line 3 column 5)": point at the line
                let line = null;
                const lm = /line (\d+)/i.exec(msg);
                const pm = /position (\d+)/i.exec(msg);
                if (lm)
                    line = parseInt(lm[1], 10);
                else if (pm)
                    line = text.substring(0, parseInt(pm[1], 10)).split("\n").length;
                return { Ok: false, Error: { Text: _T("Invalid json: {0}", msg, "Error shown below the json editor of the parameters of an API when the text is not valid json.{0} is replaced with the parser message") + (line && !lm ? " (" + _T("line {0}", line, "Line number of an error in a json text.{0} is the line number") + ")" : ""), Line: line } };
            }
            if (!E.IsPrimitive(type) && (v === null || typeof v !== "object" || Array.isArray(v)))
                return { Ok: false, Error: { Text: _T("Expected an object of the type {0}.", type.TypeName, "Error shown below the json editor of the parameters of an API when the json is not an object.{0} is replaced with the type name"), Line: 1 } };
            const bad = await E.CheckJson(type, v, this.Input ? this.Input.Options : null);
            if (bad) {
                const line = E.JsonLines(text).get(bad.Path) || 1;
                return {
                    Ok: false, Error: {
                        Line: line,
                        Text: (bad.Path ? bad.Path + ": " : "") + bad.Text + " (" + _T("line {0}", line, "Line number of an error in a json text.{0} is the line number") + ")",
                    },
                };
            }
            if (!E.IsPrimitive(type)) {
                delete v.$type;
                return { Ok: true, Value: v };
            }
            if (!this.Input.CanConsumeCopy(v))
                return { Ok: false, Error: { Text: _T("The value is not a valid {0}.", type.TypeName, "Error shown below the json editor of the parameters of an API when the json value has the wrong type.{0} is replaced with the type name"), Line: 1 } };
            return { Ok: true, Value: v };
        }

        /**
         * Convert the json text back to the form (when it was changed); an invalid text is reported and kept. Read only members keep
         * their values (Sw.Edit ReplaceObject; a toast names them).
         * @returns {Promise<boolean>} True when the form has the text's value (or the text was not changed).
         */
        async ApplyJson() {
            const t = this;
            if (!t.JsonEditor)
                return true;
            const text = t.JsonEditor.Get();
            if (text === t.JsonText)
                return true;
            const r = await t.ParseJson(text);
            if (!r.Ok) {
                //  One error surface: the status line below the editor plus the line marker (no toast, no inline copy of the text)
                t.ShowJsonError(r.Error);
                t.JsonError.scrollIntoView({ block: "nearest" });
                if (t.JsonEditor)
                    t.JsonEditor.Focus();
                return false;
            }
            const E = Sw.Edit;
            let v = r.Value;
            if (!E.IsPrimitive(t.Info.Arg)) {
                //  Members left out of the text keep their defaults
                v = Object.assign(await E.CreateDefault(t.Info.Arg, null, false), v);
                delete v.$type;
            }
            await t.Input.ReplaceObject(v, _TF("Edit json", "Command text stored in a log when the parameters of an API were replaced by the edited json text"));
            t.JsonText = t.ArgText();
            //  The text shows what the form took (read only members kept their values)
            if (t.ArgTabs.Active === t.JsonTab && t.JsonEditor.Get() !== t.JsonText)
                t.JsonEditor.Set(t.JsonText);
            const err = await t.Input.Validate(false);
            if (err)
                Sw.Toast(err[1], { Kind: "warning" });
            return true;
        }

        /**
         * The argument to send: from the JSON tab when it is open (validated; null when invalid), else from the form.
         * @returns {Promise<{Ok: boolean, Value: *}>} The argument.
         */
        async GetArg() {
            const t = this;
            if (t.ArgTabs && t.ArgTabs.Active === t.JsonTab && t.JsonEditor) {
                if (!await t.ApplyJson())
                    return { Ok: false };
            }
            return { Ok: true, Value: t.Input.GetObject() };
        }

        /** The input changed after the last call: the result is removed (like the original page: any parameter change removes it). */
        MarkStale() {
            const t = this;
            if (!t.Results.querySelector(".api-tabs"))
                return;
            t.Results.classList.remove("api-stale");
            t.ResultFoot.hidden = true;
            t.ResultFoot.replaceChildren();
            t.ShowNoResult();
        }

        /** The result card before a call (and after a parameter change): "No result yet". */
        ShowNoResult() {
            this.ShowResultState(Sw.EmptyState({
                Icon: "send",
                Title: _TF("No result yet", "Title of the result card of the API inspector before the first call"),
                Text: _T("Send the call to see the result here ({0}).", Sw.Keys.Format("Mod+Enter"), "Text of the result card of the API inspector before the first call.{0} is the keyboard shortcut, ex: Ctrl+Enter"),
            }).E);
        }

        /**
         * Call the API and show the result.
         * @returns {Promise<void>}
         */
        async Send() {
            const t = this;
            const d = t.Info;
            let res = null, error = null;
            let started = 0;
            //  The content of the open tab: the json text (checked, converted to the form) or the form
            let obj;
            if (t.HaveArg) {
                const arg = await t.GetArg();
                if (!arg.Ok)
                    return;
                obj = arg.Value;
            }
            //  (Send runs from the Send button's OnClick, also for Mod+Enter: the button is busy until this promise settles and absorbs
            //  repeats, UX guide FBK-2; it is not disabled meanwhile)
            t.Results.classList.add("api-busy");
            t.Results.setAttribute("aria-busy", "true");
            try {
                if (t.HaveArg) {
                    const copy = Object.assign({}, obj);
                    delete copy.$type;
                    Sw.Storage.Local.Set(t.Key, JSON.stringify(E0(obj) ? copy : obj));
                    //  The change count of the footer counts from the last sent value
                    if (t.Footer)
                        t.Footer.SetBaseline();
                    started = performance.now();
                    res = await Sw.Net.Send(t.Full, obj, { Reload: true, Blob: t.IsRaw });
                }
                else {
                    started = performance.now();
                    res = await Sw.Net.Get(t.Full, { Reload: true, Blob: t.IsRaw });
                }
            }
            /* SwWeb:allow ux-fbk-5 (the error is shown below: the result card gets the error badge and its text) */
            catch (e) {
                error = e;
            }
            finally {
                t.Results.classList.remove("api-busy", "api-stale");
                t.Results.removeAttribute("aria-busy");
            }
            const ms = Math.max(0, Math.round(performance.now() - started));
            const tabs = savedTabs("Api.Results");
            let status;
            if (error) {
                //  Refused (403): say why (the role) instead of the bare status
                const denied = Sw.Access && Sw.Access.IsDenied(error);
                const why = denied ? Sw.Access.Requires(d.Auth) : "";
                const msg = (error && error.message ? error.message : String(error));
                const st = Sw.EmptyState({
                    Error: true, Class: "api-errorstate",
                    Title: denied ? _TF("Not allowed", "Title of the error state of an API call that the server refused (403)") : _TF("The call failed", "Title of the error state of a failed API call"),
                    Text: why || _TF("The service returned an error. The details are below.", "Text of the error state of a failed API call"),
                });
                const c = el("div.api-error", null, st.E, el("pre.api-code.api-errortext", { text: msg }));
                tabs.Add({ Title: _TF("Error", "The text of a tab header that will display the server error as a result of an API call"), Content: c });
                status = Sw.Badge(denied ? _TF("Refused", "Status badge of an API call that the server refused") : _TF("Error", "Status badge of a failed API call"), "error", msg);
            }
            else if (t.IsRaw) {
                const url = URL.createObjectURL(res);
                const c = el("div.api-preview", { title: _TF("Open in a new tab.", "A tool tip description on an HTML element, that when clicked will open the data on a new browser tab") });
                const frm = d.Mime.indexOf("image/") === 0 ? Sw.CreateImg() : Sw.CreateIFrame();
                frm.src = url;
                c.append(frm);
                c.addEventListener("click", ev => {
                    if (Sw.IsPureEvent(ev))
                        Sw.Page.Navigate(url, "_blank");
                });
                tabs.Add({ Title: _TF("Preview", "The text of a tab header that will display the API result of a specific MIME type (as an embedded iframe)"), Content: c });
                status = Sw.Badge(d.Mime.split(";")[0], "ok");
            }
            else if ((res === null || res === undefined) && !d.Return) {
                tabs.Add({
                    Title: _TF("Result", "The text of a tab header that will display the detailed result of an API call"),
                    Content: Sw.EmptyState({
                        Icon: "check", Class: "api-void",
                        Title: _TF("Done", "Title of the result of a successful call to an API that returns nothing"),
                        Text: _TF("The API doesn't return a result.", "The text to display when a successful call to an API that doesn't return any result was made"),
                    }).E,
                });
                status = Sw.Badge(_TF("OK", "Status badge of a successful API call"), "ok");
            }
            else {
                //  (a null result shows the empty form of its type, edit/GetDefaultInstance, and "null" in JSON, as the original: P3-A-9)
                const opt = new Sw.Edit.Options();
                opt.ReadOnly = true;
                //  The result editor never becomes the target of the page wide undo keys (they belong to the argument form)
                const last = Sw.Edit.LastEdit;
                const out = new Sw.Edit(d.Return, null, opt);
                await out.SetObject(res);
                if (last && last.Element.isConnected)
                    Sw.Edit.LastEdit = last;
                tabs.Add({ Title: _TF("Result", "The text of a tab header that will display the detailed result of an API call"), Content: el("div.api-params", null, out.Element) });
                tabs.Add({ Title: _TF("JSON", "The text of a tab header that will display the result of an API call as formatted JSON text"), Content: await jsonView(res), Select: false });
                status = Sw.Badge(_TF("OK", "Status badge of a successful API call"), "ok");
            }
            tabs.Tools.append(status, el("span.api-time", { text: Sw.Format.DurationMs(ms), title: _TF("Time from sending the call to the complete answer", "Tool tip of the time of an API call in the result card of the API inspector") }));
            tabs.Restore();
            t.Results.replaceChildren(tabs.E);
            //  "Called 20:10:53" (no user lookup: no request the original page did not make), and a note when the input changes afterwards
            const time = new Date().toLocaleTimeString();
            t.StaleNote = el("span.api-stalenote", { hidden: true, text: _TF("The parameters changed since this call.", "Note in the result card of the API inspector when the input was changed after the call") });
            t.ResultFoot.replaceChildren(el("span", {
                text: _T("Called {0}", time, "Footer of the result card of the API inspector.{0} is the time of the call"),
            }), t.StaleNote);
            t.ResultFoot.hidden = false;
            const card = t.Results.closest(".api-card");
            if (card && card.getBoundingClientRect().top > window.innerHeight * 0.6)
                card.scrollIntoView({ block: "nearest", behavior: window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
        }
    }

    /** @type {?Promise<?object>} The ApiIoInfo of the service (Api/debug/GetApiIoInfo, once; null when not available). */
    let ioInfo = null;

    /**
     * The serializers and compression methods of the service's APIs (Api/debug/GetApiIoInfo), shared with explore/apihelp.html.
     * @returns {Promise<?object>} The ApiIoInfo, null when the service doesn't have the api.
     */
    function getIoInfo() {
        if (!ioInfo)
            ioInfo = Sw.Net.Get(Sw.Net.Api("debug/GetApiIoInfo"), { NoNavigate: true }).catch(e => {
                console.warn("Api/debug/GetApiIoInfo: " + (e && e.message ? e.message : e));
                return null;
            });
        return ioInfo;
    }

    /**
     * Raw deflate of a text in the page (Sw.Util.Deflate, the "_d" binary GET argument of the service).
     * @param {string} text - The text (UTF-8).
     * @returns {Promise<?Uint8Array>} The compressed bytes, null when the browser can't compress.
     */
    async function deflate(text) {
        try {
            return await Sw.Util.Deflate(new TextEncoder().encode(text));
        }
        catch {
            return null;
        }
    }

    /**
     * The Curl tab of the API inspector: options (GET / POST, the result format when the service has more than one, the compression of
     * the argument, "Compressed result") and a read only text area with the curl command (one curl command, no pipes, for a POSIX shell:
     * bash, zsh, Git Bash, WSL) of the call with the current argument, recomputed when an option changes and when the tab is opened.
     * A Copy button copies it.
     * The argument is sent as json: POST in the body (Content-Type: application/json), GET after "?" (url encoded json). Compressed
     * (deflate, only when the service reads deflate): deflated by the page and sent as the binary GET argument "?_d" + base64url data
     * (a compressed POST body can't be given on a curl command line, so the compression is a GET option).
     */
    class CurlPanel {
        /**
         * @param {ApiPage} page - The page.
         */
        constructor(page) {
            const t = this;
            t.P = page;
            const key = "SysWeaver.Api.Curl";
            let saved = {};
            try {
                saved = JSON.parse(Sw.Storage.Local.Get(key, "{}")) || {};
            }
            catch {
                saved = {};
            }
            /** @type {{Method: string, Format: string, Comp: string, Compressed: boolean}} The options (Format: a serializer name, "" = the default). */
            t.Opt = { Method: page.HaveArg ? "POST" : "GET", Format: "", Comp: "", Compressed: false };
            for (const k of ["Format", "Comp", "Compressed"])
                if (saved[k] !== undefined)
                    t.Opt[k] = saved[k];
            const save = () => {
                const o = Object.assign({}, t.Opt);
                delete o.Method;
                Sw.Storage.Local.Set(key, JSON.stringify(o));
            };
            const changed = () => {
                save();
                t.Update();
            };
            t.MethodC = new Sw.Segmented({
                Label: _TF("Method", "Label of the GET / POST choice of the Curl tab of the API inspector"), Small: true, Value: t.Opt.Method,
                Items: [
                    //  SwWeb:no-tr (HTTP method names)
                    { Value: "GET", Text: "GET", Title: _TF("The argument as json after \"?\" in the url", "Tool tip of the GET choice of the Curl tab of the API inspector") },
                    //  SwWeb:no-tr (HTTP method names)
                    { Value: "POST", Text: "POST", Title: _TF("The argument as json in the request body", "Tool tip of the POST choice of the Curl tab of the API inspector") },
                ],
                OnChange: v => {
                    t.Opt.Method = v;
                    t.SyncComp();
                    changed();
                },
            });
            t.FormatC = new Sw.Select({
                Label: _TF("Result format", "Label of the result format (serializer, Accept header) choice of the Curl tab of the API inspector"),
                Title: _TF("The format of the result (the Accept header)", "Tool tip of the result format choice of the Curl tab of the API inspector"),
                Options: [{ Value: "", Label: "json" }], Value: "",
                OnChange: v => {
                    t.Opt.Format = v;
                    changed();
                },
            });
            t.CompC = new Sw.Select({
                Label: _TF("Compress the argument", "Label of the argument compression choice of the Curl tab of the API inspector"),
                Title: _TF("Compress the argument (deflate, sent as a binary \"?_d\" GET argument)", "Tool tip of the argument compression choice of the Curl tab of the API inspector"),
                Options: [{ Value: "", Label: _TF("None", "The no compression choice of the Curl tab of the API inspector") }], Value: "",
                OnChange: v => {
                    t.Opt.Comp = v;
                    changed();
                },
            });
            t.CompressedC = new Sw.Toggle({
                Label: _TF("Compressed result", "Label of the switch of the Curl tab of the API inspector that adds --compressed (ask for a compressed result and decompress it)"),
                Title: _TF("Ask for a compressed result and decompress it (curl --compressed)", "Tool tip of the Compressed result switch of the Curl tab of the API inspector"),
                Value: !!t.Opt.Compressed,
                OnChange: v => {
                    t.Opt.Compressed = !!v;
                    changed();
                },
            });
            t.Text = el("textarea.sw-input.api-curl-text", {
                readOnly: true, spellcheck: false, rows: 6,
                attrs: { "aria-label": _TF("Curl command line", "Accessible name of the text area with the curl command line of the Curl tab of the API inspector"), wrap: "soft" },
            });
            t.Note = el("p.api-curl-note");
            t.E = el("div.api-curl",
                null,
                el("div.api-curl-opts", null,
                    new Sw.Field({ Label: _TF("Method", "Label of the GET / POST choice of the Curl tab of the API inspector"), Control: t.MethodC }).E,
                    t.FormatC.E, t.CompC.E, t.CompressedC.E),
                el("div.api-codebar", null, copyButton(() => t.Text.value,
                    _TF("Copy the curl command line to the clipboard", "Tool tip of the Copy button of the Curl tab of the API inspector")).E),
                t.Text, t.Note);
            t.Seq = 0;
            t.CanDeflate = false;
            t.SyncComp();
        }

        /**
         * Enable the argument compression choice: an argument, GET and a service that reads deflate.
         */
        SyncComp() {
            const t = this;
            const why = !t.P.HaveArg ? _TF("This API takes no argument", "Reason why the argument compression of the Curl tab is disabled")
                : !t.CanDeflate ? _TF("The service does not read compressed arguments", "Reason why the argument compression of the Curl tab is disabled when the service has no deflate")
                    : t.Opt.Method !== "GET" ? _TF("A compressed argument is sent with GET", "Reason why the argument compression of the Curl tab is disabled for POST")
                        : null;
            //  Disabled: shows None (the chosen compression is kept for GET)
            t.CompC.Set(why ? "" : t.Opt.Comp, true);
            t.CompC.SetDisabled(!!why, why || undefined);
        }

        /**
         * Fill the format and compression choices from the service (once). The format choice is hidden when the service has one format.
         * @returns {Promise<?object>} The ApiIoInfo.
         */
        async Init() {
            const t = this;
            const io = await getIoInfo();
            if (t.Inited)
                return io;
            t.Inited = true;
            t.Io = io;
            const outs = io && io.Output ? io.Output : [{ Name: "json", Mime: "application/json", Extension: "json", IsText: true }];
            const def = (io && io.DefaultOutput) || "json";
            t.FormatC.SetOptions(outs.map(s => ({ Value: s.Name === def ? "" : s.Name, Label: s.Extension || s.Name, Hint: s.Mime })), outs.some(s => s.Name === t.Opt.Format) ? t.Opt.Format : "");
            t.Opt.Format = t.FormatC.Get() || "";
            t.FormatC.E.hidden = outs.length < 2;
            //  deflate: the service decompresses it and has the "_d" binary GET argument
            const comps = io && io.Compression ? io.Compression : ["deflate"];
            const types = io && io.GetCompression ? io.GetCompression : [{ Char: "d", HttpCode: "deflate" }];
            t.DeflateChar = (types.find(x => x.HttpCode === "deflate") || {}).Char;
            t.CanDeflate = comps.indexOf("deflate") >= 0 && !!t.DeflateChar;
            t.CompC.SetOptions([{ Value: "", Label: _TF("None", "The no compression choice of the Curl tab of the API inspector") }].concat(t.CanDeflate ? [{ Value: "deflate", Label: "deflate" }] : []),
                t.CanDeflate && t.Opt.Comp === "deflate" ? "deflate" : "");
            t.Opt.Comp = t.CompC.Get() || "";
            t.SyncComp();
            return io;
        }

        /**
         * Recompute the command line (the options, the current argument of the form).
         * @returns {Promise<void>}
         */
        async Update() {
            const t = this;
            const seq = ++t.Seq;
            await t.Init();
            const cmd = await t.Command();
            if (seq !== t.Seq)
                return;
            t.Text.value = cmd.Text;
            //  The lines as wrapped in the text area (about 64 characters per row)
            t.Text.rows = Math.min(16, Math.max(3, cmd.Text.split("\n").reduce((n, l) => n + 1 + Math.floor(l.length / 64), 0)));
            t.Note.replaceChildren();
            t.Note.hidden = !cmd.Notes.length;
            cmd.Notes.forEach(n => t.Note.append(el("span", { text: n })));
        }

        /**
         * Build the command line.
         * @returns {Promise<{Text: string, Notes: string[]}>} The command and notes shown below it.
         */
        async Command() {
            const t = this;
            const p = t.P;
            const o = t.Opt;
            const io = t.Io;
            const q = shellQuote;
            const notes = [];
            const json = p.HaveArg && p.Input ? JSON.stringify(stripType(p.Input.GetObject())) ?? "null" : null;
            const opts = [];
            let url = q(p.Full);
            //  The result format (Accept): binary formats and raw results go to a file
            const outs = io && io.Output ? io.Output : [];
            const fmt = o.Format ? outs.find(s => s.Name === o.Format) : null;
            if (fmt)
                opts.push("-H " + q("Accept: " + fmt.Mime));
            if (o.Compressed)
                opts.push("--compressed");
            if (o.Method === "POST") {
                opts.unshift("-X POST");
                if (json !== null)
                    opts.push("-H " + q("Content-Type: application/json"), "--data-binary " + q(json));
            }
            else if (json !== null) {
                //  GET: the json after "?" (curl --globoff: {} [] are not url patterns), deflated: the binary "?_d" argument
                const data = o.Comp === "deflate" && t.CanDeflate ? await deflate(json) : null;
                if (o.Comp === "deflate" && t.CanDeflate && !data)
                    notes.push(_TF("This browser cannot deflate: the argument is sent uncompressed.", "Note of the Curl tab of the API inspector when the browser cannot compress the argument"));
                if (data)
                    url = q(p.Full + "?_" + t.DeflateChar + Sw.Util.Base64Encode(data, true));
                else {
                    opts.push("--globoff");
                    url = q(p.Full + "?" + encodeURIComponent(json));
                }
            }
            if (p.IsRaw || (fmt && !fmt.IsText)) {
                const ext = p.IsRaw ? (p.Info.Mime.split(";")[0].split("/")[1] || "bin").split("+")[0] : fmt.Extension;
                opts.push("-o " + q(p.Name + "." + ext));
            }
            const d = p.Info;
            if (d.Auth !== null && d.Auth !== undefined)
                notes.push(_TF("This API needs a signed in user: add your session cookie (-b 'name=value'), see How to call.", "Note of the Curl tab of the API inspector for an API that needs a signed in user"));
            //  One curl command on one line (no pipes, no line continuations)
            const text = ["curl"].concat(opts, [url]).join(" ");
            return { Text: text, Notes: notes };
        }
    }

    /**
     * Arguments are objects (store without the type marker) unless primitive.
     * @param {*} obj The argument.
     * @returns {boolean} True for objects.
     */
    function E0(obj) {
        return obj !== null && typeof obj === "object" && !Array.isArray(obj);
    }

    /**
     * A copy of an argument without the `$type` marker of the editor (objects only; other values as is).
     * @param {*} obj The argument.
     * @returns {*} The copy.
     */
    function stripType(obj) {
        if (!E0(obj))
            return obj;
        const c = Object.assign({}, obj);
        delete c.$type;
        return c;
    }

    /**
     * Show an in-page message instead of the inspector (missing parameter, api info not available): the page header and an empty / error
     * state with buttons to the API list (and to try again).
     * @param {string} title The heading.
     * @param {string} text The explanation.
     * @param {boolean} [retry] Add a "Try again" button (reloads the page; error look).
     */
    function showMessage(title, text, retry) {
        const actions = [];
        if (retry)
            actions.push(new Sw.Button({
                Text: _TF("Try again", "Text of a button that retries loading a page after an error"),
                Icon: "refresh",
                Kind: "primary",
                OnClick: () => location.reload(),
            }));
        actions.push(new Sw.Button({
            Text: _TF("Show all APIs", "Text of a button that opens the table listing all APIs of the server"),
            Icon: "table",
            Kind: retry ? null : "primary",
            Title: _TF("Open the list of all APIs of the service", "Tool tip of a button that opens the table listing all APIs of the service"),
            OnClick: () => Sw.Page.Navigate("table.html?q=" + encodeURIComponent("../Api/debug/ApiTable"), "_self"),
        }));
        const st = Sw.EmptyState({ Icon: retry ? "warning" : "info", Error: !!retry, Title: title, Text: text, Actions: actions });
        st.E.classList.add("api-message");
        document.body.append(el("main.api-page.sw-page-form.sw-root", null,
            Sw.PageHeader({ Title: _TF("API", "The page title prefix, is followed by the url to the API"), Class: "api-ph" }).E,
            el("section.sw-card.api-card", null, st.E)));
    }

    /**
     * The document (shell top bar, browser tab) title of an API, as the original page: "API: Api/application/GetLocalizedLanguages"
     * (D30, parity: the shortened "GetLocalizedLanguages · API" was not an approved change).
     * @param {string} url The API url.
     * @returns {string} The title.
     */
    function apiTitle(url) {
        return _TF("API", "The page title prefix, is followed by the url to the API") + (url ? ": " + url : "");
    }

    /**
     * The api info is refused for this user (GetApiInfo locked down): show what is known without it instead of a blank
     * "not allowed" page: the API url and name and the shared "not allowed" state for the API details (the reason), no retry; no
     * other request (like the original page).
     * @param {string} url The API url (relative to the site root).
     * @param {*} e The refused GetApiInfo request.
     * @returns {Promise<void>}
     */
    async function showLocked(url, e) {
        Sw.Page.TitleFromLink = null;
        document.title = apiTitle(url);
        const facts = el("dl.api-locked-facts");
        const add = (k, v) => {
            if (v === null || typeof v === "undefined" || v === "")
                return;
            facts.append(el("dt", { text: k }), el("dd", { text: typeof v === "object" ? JSON.stringify(v) : String(v) }));
        };
        add(_TF("Url", "Label of the API url on the API inspector page when the details of the API are not available"), url);
        add(_TF("Name", "Label of the API name on the API inspector page when the details of the API are not available"), url.substring(url.lastIndexOf("/") + 1));
        const denied = el("div.api-locked-denied");
        const actions = [];
        const name = url.substring(url.lastIndexOf("/") + 1).split(".")[0] || url;
        document.body.append(el("main.api-page.sw-page-form.sw-root", null,
            Sw.PageHeader({ Title: name, Description: Sw.Page.LinkText(url, 200), Actions: actions, Class: "api-ph" }).E,
            el("section.sw-card.api-card.api-locked", null, el("div.sw-card-body", null, facts, denied))));
        Sw.Page.ShowNotAllowed({
            Error: e, Auth: null, Element: denied, Scope: "section",
            Name: _TF("the details of this API", "Completes the heading 'You don't have access to {0}' on the API inspector page when the server doesn't let the user read the API details (signature, call form)"),
        });
    }

    /**
     * Page entry of explore/api.html (`<body onload="apiMain()">`). Url parameter `q`: the API url, resolved with Sw.Url.SitePath
     * (the same rule as table.html: "Api/x" relative to the site root, or "../Api/x" relative to this page, or an url of this site).
     * @returns {Promise<void>}
     */
    window.apiMain = async function () {
        const remove = Sw.Page.Loading();
        try {
            const q = Sw.Url.Get("q");
            if (!q) {
                document.title = _TF("API", "The page title prefix, is followed by the url to the API");
                showMessage(_TF("No API selected", "Heading of the API inspector page when it was opened without an API"),
                    _TF("This page describes one API and lets you call it. Open it from the list of all APIs, or add the API url as the q parameter, example: api.html?q=Api/debug/GetApiInfo", "Explanation on the API inspector page when it was opened without an API (the q url parameter is missing)"));
                return;
            }
            //  `q` is text from the url: plain, limited text (Sw.Page.SetLinkTitle) until the server knows the API (then its own url)
            Sw.Page.SetLinkTitle(_TF("API", "The page title prefix, is followed by the url to the API") + ": " + q);
            //  One rule with table.html (Sw.Url.SiteUrl): "Api/x" (relative to the site root) and "../Api/x" (relative to this page) are
            //  the same API; the server knows it by its path below the site root. Urls of other sites are never sent.
            const url = Sw.Url.SitePath(q);
            if (!url) {
                showMessage(_TF("API not available", "Heading of the API inspector page when the information about the API could not be loaded"),
                    _T('The API address "{0}" (the q parameter) is not on this site. Open the API from the list of all APIs instead.', Sw.Page.LinkText(q, 200), "Error on the API inspector page when the q url parameter points to another web site or is not a valid url. {0} is replaced with the q parameter."));
                return;
            }
            let info;
            let error = "";
            try {
                [info] = await Promise.all([Sw.Net.Send(Sw.Net.Api("debug/GetApiInfo"), url), Sw.Edit.Init()]);
            }
            catch (e) {
                //  The server doesn't let this user read api infos (GetApiInfo protected): the shared "not allowed" state, no retry
                if (Sw.Access && Sw.Access.IsDenied(e)) {
                    await showLocked(url, e);
                    return;
                }
                error = e && e.message ? e.message : String(e);
            }
            if (!info) {
                //  No error but no info: the server has no API at that url (GetApiInfo returns null); name the url and the rule
                const text = error
                    ? _T('Failed to get information about the api "{0}".', url, "An error message that is shown when the server failed to get information about an API.{0} is replaced with the url of the API.") + " " + error
                    : _T('The server has no API with the url "{0}". The q parameter is the API url relative to the site root (example: Api/debug/ApiTable) or to this page (example: ../Api/debug/ApiTable).', url, "Error on the API inspector page when the server does not know the API in the q url parameter. {0} is replaced with the API url.");
                console.warn(text);
                showMessage(_TF("API not available", "Heading of the API inspector page when the information about the API could not be loaded"), text, !!error);
                return;
            }
            if (info.Uri) {
                //  The server knows the API: its own url is the title (no longer text from the link)
                Sw.Page.TitleFromLink = null;
                document.title = apiTitle(info.Uri);
            }
            await new ApiPage(url, info).Build();
            //  The document title of the original page: "API: " + the q parameter as given (the page header shows the method name)
            Sw.Page.TitleFromLink = null;
            document.title = _TF("API", "The page title prefix, is followed by the url to the API") + ": " + (Sw.Url.Get("q") || url);
        }
        catch (e) {
            Sw.Fail(e && e.message ? e.message : String(e), true);
        }
        finally {
            remove();
        }
    };
})();
