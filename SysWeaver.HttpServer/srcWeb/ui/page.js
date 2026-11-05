/**
 * @file ui/page.js - SysWeaver UI library (Sw), part 7 of 7: page level parts of Design v2 (PORTING.md B10 / PORTING.md B11).
 *
 * Styles: ui/page.css. Needs ui/base.js (Sw.el, Sw.Icon, Sw.Icons). Bundled last in sw/ui.js.
 *  - Sw.PageHeader: the page header (h1 title, one line description, status, actions), left aligned. Only defined here when no other
 *    part of the library defines it (ui/layout.js owns the final version; this one has the same options and members).
 *  - Sw.StateView: the empty / loading / error state of a page, card or list (icon, title, one sentence, actions, technical details).
 *  - Sw.Meter: a 6 px value bar (KPI tiles) that turns warning / danger at thresholds.
 *  - The shared page anatomy (ui/README.md "Page anatomy"): Sw.PageLayout (main.sw-page-<type> + Sw.PageHeader), Sw.MenuTitle (the
 *    page's menu item name in the shell: h1 = menu = breadcrumb), Sw.Card, Sw.Facts (facts strip), Sw.Kpi (KPI tile with an optional
 *    meter), Sw.ListCard (row list with status badges and an empty state), Sw.Grid (12 columns), Sw.SectionTitle.
 *  - Shared page parts (from ServerManager): Sw.UploadButton / Sw.UploadTarget / Sw.ReportUpload (upload + drop to a server
 *    repo), Sw.FillTable (a live table filling a fill page).
 *  - List / detail: Sw.SelectList (a keyboard listbox, single / multi select, empty state), Sw.ListDetail (list + detail side by side,
 *    stacked with Back on narrow containers).
 *  - Forms and wizards: Sw.Form (fields, Required, rules, messages under the fields, focus, submit gating), Sw.Steps (a wizard: step
 *    list, heading focus, Back / Next / Finish, per step validation).
 *  - Extra line icons for dashboards (cpu, memory, drive, folder), registered when missing.
 */

(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const el = Sw.el;

    //  Line icons used by the dashboards (24 grid, stroked with currentColor; registered only when the icon set lacks them)
    const ExtraIcons = {
        cpu: '<rect x="6" y="6" width="12" height="12" rx="1.5"/><rect x="9.5" y="9.5" width="5" height="5"/><path d="M9 2.5v3M15 2.5v3M9 18.5v3M15 18.5v3M2.5 9h3M2.5 15h3M18.5 9h3M18.5 15h3"/>',
        memory: '<rect x="3" y="7" width="18" height="10" rx="1.5"/><path d="M7.5 7v10M12 7v10M16.5 7v10M6 17v3M18 17v3"/>',
        drive: '<rect x="2.5" y="13" width="19" height="7" rx="2"/><path d="M5.5 13 8 5h8l2.5 8"/><path d="M6.5 16.5h.01M10 16.5h.01" stroke-width="2.5"/>',
        folder: '<path d="M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5Z"/>',
        grid: '<rect x="3.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="3.5" y="13.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="13.5" width="7" height="7" rx="1.5"/>',
        inbox: '<path d="M3 13h5l1.5 3h5L16 13h5"/><path d="M5.5 5h13L21 13v6a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-6Z"/>',
    };
    if (Sw.Icons && Sw.Icons.Has)
        for (const k in ExtraIcons)
            if (!Sw.Icons.Has(k))
                Sw.Icons.Add(k, ExtraIcons[k]);

    /**
     * Make an icon element: an inline svg icon name, a SysWeaver icon class or url (Sw.Icon handles both), or an element as is.
     * @param {string|Element} [icon] The icon.
     * @returns {?Element} The element, null without an icon.
     */
    function iconOf(icon) {
        if (!icon)
            return null;
        if (icon instanceof Element)
            return icon;
        if (typeof icon === "string" && (icon.indexOf("/") >= 0 || icon.endsWith(".svg")) && Sw.IconClass)
            return Sw.IconClass(icon, {});
        return Sw.Icon ? Sw.Icon(icon) : null;
    }

    /**
     * Normalize a list of actions (elements, Sw components with .E, or falsy entries that are skipped) to elements.
     * @param {Array<*>|*} list The actions.
     * @returns {Element[]} The elements.
     */
    function elementsOf(list) {
        const r = [];
        for (const a of Array.isArray(list) ? list : [list])
            if (a)
                r.push(a instanceof Element ? a : a.E);
        return r.filter(x => x);
    }

    // ------------------------------------------------------------------ the options of each part (DevMode checks, one registry)

    /** A text option. */
    const S = "string";
    /** Content: nodes, components, text or a list of them. */
    const Any = "any";
    const CardOpts = {
        Title: S, Sub: S, Count: "number|boolean", Tools: Any, Actions: Any, Body: Any, Flush: "boolean", Span: "number|string", Tag: S,
        Level: "number", Class: S, Label: S, OnClick: "function", Href: S,
    };
    /**
     * The option schemas of the page parts (option checks by mechanism, not by list: Sw.Card({ Titel }), Sw.PageLayout({ Titel }),
     * a Sw.Form field { Requred: true } were silent; a typo in Required silently dropped the validation). Registered by name with
     * Sw.Work.RegisterOptions (the core's Sw.DevOptions.Register), checked in DevMode when the part is made.
     */
    const PageSchema = {
        PageHeader: { Title: S, Description: S, Status: Any, Actions: Any, Compact: "boolean", Class: S, Level: "number", MenuTitle: "boolean|string", Overflow: Any },
        StateView: {
            Kind: { Type: S, Check: v => /^(empty|loading|error)$/.test(v) ? "" : '"' + v + '" is no kind (empty, loading, error).' },
            Icon: Any, Title: S, Text: S, Actions: Any, Details: S, Small: "boolean", Lines: "number",
        },
        Meter: { Label: S, Value: "number", Max: "number", Warn: "number", Danger: "number", Good: { Type: S, Check: v => /^(low|high)$/.test(v) ? "" : '"' + v + '" is no Good (low, high).' }, Absolute: "boolean", Levels: Any },
        Card: CardOpts,
        Facts: { Items: "array", Card: "boolean", Label: S, Class: S },
        "Facts item": { Key: Any, Label: S, Value: Any, Title: S, Href: S, OnClick: "function", Copy: "boolean|string", Mono: "boolean" },
        Kpi: { Label: S, Icon: Any, Value: Any, Unit: S, Sub: S, Meter: "boolean|object", Span: "number", Href: S, OnClick: "function", Title: S, Class: S },
        ListCard: Object.assign({}, CardOpts, { Items: "array", Empty: Any }),
        "ListCard item": { Key: Any, Text: S, Icon: Any, Status: Any, Meta: Any, Title: S, Href: S, OnClick: "function", Sub: S, Class: S },
        Grid: { Content: Any, Class: S, Label: S, Tag: S },
        PageLayout: {
            Type: { Type: S, Check: v => /^(dash|form|read|fill)$/.test(v) ? "" : '"' + v + '" is no page type (dash, form, read, fill).' },
            Title: S, Description: S, Status: Any, Actions: Any, Compact: "boolean", MenuTitle: "boolean|string", Main: Any, Parent: Any, Class: S, Header: "boolean",
        },
        UploadButton: { Text: S, Title: S, Repo: S, OnUploaded: "function", Guard: Any, Small: "boolean", Icon: S, Class: S },
        SelectList: { Items: "array", Value: Any, Multi: "boolean", Label: S, Render: "function", Empty: Any, OnSelect: "function", OnOpen: "function", Class: S },
        ListDetail: { List: Any, Detail: Any, Label: S, ListWidth: S, Breakpoint: "number", Back: "boolean", BackText: S, OnBack: "function", OnLayout: "function", Class: S },
        Form: {
            Fields: "array", Submit: Any, OnSubmit: "function", Gate: { Type: S, Check: v => v === "required" ? "" : '"' + v + '" is no gate (only "required").' },
            Title: S, Dirty: "boolean|string", Draft: S, Resend: "boolean", Name: S,
        },
        "Form field": {
            Name: "string|number", Control: "element|component", Label: S, Required: "boolean|string", Validate: "function", When: "function", NoDraft: "boolean",
            //  (set by the form itself)
            ErrorE: Any, Error: Any, DraftH: Any,
            $Check: fd => [fd.Control ? "" : "a field without Control is skipped (pass the control: a Sw component or an input element)."],
        },
        Steps: { Steps: "array", Label: S, FinishText: S, OnFinish: "function", OnCancel: "function", NextText: S, BackText: S, Class: S, Start: "number" },
        "Steps step": { Title: S, Sub: S, Content: Any, Form: Any, Validate: "function", OnShow: "function" },
    };
    if (Sw.Work && typeof Sw.Work.RegisterOptions === "function")
        for (const k of Object.keys(PageSchema))
            Sw.Work.RegisterOptions("Sw." + k, PageSchema[k]);
    if (Sw.DevOptions && typeof Sw.DevOptions.NoOptions === "function")
        Sw.DevOptions.NoOptions("Sw.FillTable", "o are Sw.Table.Create options (checked there)");
    //  Schemas of ui apis whose files check nothing yet (registered here, the last ui part, so the registry knows them; their files can
    //  call Sw.Work.CheckOptions(name, o) with no schema)
    if (Sw.Work && typeof Sw.Work.RegisterOptions === "function") {
        Sw.Work.RegisterOptions("Sw.FullScreen.OnTouch", { AnyPointer: "boolean" });
        Sw.Work.RegisterOptions("Sw.FullScreen.OnCorner", { Corner: { Type: S, Check: v => /^(top|bottom)-(left|right)$/.test(v) ? "" : '"' + v + '" is no corner (top-right, top-left, bottom-right, bottom-left).' }, Size: "number", Area: Any });
        Sw.Work.RegisterOptions("Sw.PropertyEditor", { Defs: "array", Values: "object", OnInput: "function", OnChange: "function", Realtime: "boolean", ResetButtons: "boolean", Hints: "boolean", Label: S });
        Sw.Work.RegisterOptions("Sw.SearchSelection", { Id: S, GetId: "function", MaxRecent: "number", Render: "function", Find: "function" });
    }

    /**
     * Check the options of a page part (DevMode; nothing in production).
     * @param {string} name The part ("Card", "Form field").
     * @param {object} o The options.
     * @returns {object} o.
     */
    function checkOpts(name, o) {
        if (o && typeof o === "object" && Sw.Work && typeof Sw.Work.CheckOptions === "function")
            Sw.Work.CheckOptions("Sw." + name, o, PageSchema[name]);
        return o;
    }

    /**
     * Check every item of a list option.
     * @param {string} name The item kind ("Facts item"). @param {*} list The items.
     */
    function checkEach(name, list) {
        if (Array.isArray(list) && typeof Sw.DevMode === "function" && Sw.DevMode())
            for (const x of list)
                if (x && typeof x === "object" && !(x instanceof Element) && !(x.E instanceof Element))
                    checkOpts(name, x);
    }

    /**
     * The page header (Design v2, PORTING.md B10): header.sw-ph > div.sw-ph-text (h1.sw-ph-title, p.sw-ph-desc), div.sw-ph-status, div.sw-ph-actions.
     * Callable with or without new. Options: Title (text), Description (text; empty = no line), Status (an element, ex: Sw.Badge),
     * Actions (array of elements / Sw.Button), Compact (fill pages: smaller title, one line description), Class, Level (heading level, default 1).
     * Members: E (the header), TitleE, DescE, StatusE, ActionsE; SetTitle(text), SetDescription(text), SetStatus(element|null),
     * AddAction(elementOrComponent).
     * @param {object} [o] Options.
     * @returns {object} The header object.
     */
    function PageHeader(o) {
        o = o || {};
        checkOpts("PageHeader", o);
        const titleE = el("h" + (o.Level || 1) + ".sw-ph-title", { text: o.Title || "" });
        const descE = el("p.sw-ph-desc", { text: o.Description || "" });
        descE.hidden = !o.Description;
        const statusE = el("div.sw-ph-status");
        const actionsE = el("div.sw-ph-actions", null, ...elementsOf(o.Actions || []));
        const e = el("header.sw-ph", { class: (o.Compact ? "is-compact " : "") + (o.Class || "") }, el("div.sw-ph-text", null, titleE, descE), statusE, actionsE);
        const h = {
            E: e,
            TitleE: titleE,
            DescE: descE,
            StatusE: statusE,
            ActionsE: actionsE,
            /** @param {string} t The title. */
            SetTitle(t) {
                titleE.textContent = t || "";
            },
            /** @param {string} t The description (empty hides the line). */
            SetDescription(t) {
                descE.textContent = t || "";
                descE.hidden = !t;
            },
            /** @param {?Element} s The status element (null removes it). */
            SetStatus(s) {
                statusE.replaceChildren(...(s ? [s] : []));
                statusE.hidden = !s;
            },
            /** @param {Element|{E: Element}} a An action to append. */
            AddAction(a) {
                actionsE.append(...elementsOf(a));
                actionsE.hidden = false;
            },
        };
        h.SetStatus(o.Status || null);
        actionsE.hidden = !actionsE.children.length;
        return h;
    }

    /**
     * A state view (Design v2, PORTING.md B13 / PORTING.md B11): div.sw-state.is-<kind> with an icon in a sunken circle, a title, one sentence,
     * actions and an optional "Details" disclosure (technical text, selectable). Loading states show skeleton lines instead of an icon
     * when Lines is given. Callable with or without new.
     * Options: Kind ("empty" (default) | "loading" | "error"), Icon (Sw.Icon name / element; defaults per kind), Title, Text,
     * Actions (elements / Sw.Button), Details (technical text), Small (compact row for cards and lists), Lines (loading: n skeleton lines).
     * Members: E; Set(options) replaces the content.
     * @param {object} [o] Options.
     * @returns {{E: HTMLElement, Set: function(object):void}} The view.
     */
    function StateView(o) {
        const e = el("div.sw-state");
        /** @param {object} x Options (see StateView). */
        const set = x => {
            x = x || {};
            checkOpts("StateView", x);
            const kind = x.Kind || "empty";
            e.className = "sw-state is-" + kind + (x.Small ? " is-small" : "");
            e.setAttribute("role", kind === "error" ? "alert" : "status");
            if (kind === "loading")
                e.setAttribute("aria-busy", "true");
            else
                e.removeAttribute("aria-busy");
            const parts = [];
            if (kind === "loading" && x.Lines > 0) {
                const sk = el("div.sw-state-skel", { attrs: { "aria-hidden": "true" } });
                for (let i = 0; i < x.Lines; ++i)
                    sk.append(el("span.sw-skel", { style: { width: (90 - (i * 17) % 40) + "%", margin: "6px 0" } }));
                sk.style.width = "min(100%, 28rem)";
                parts.push(sk);
            }
            else {
                const ic = iconOf(x.Icon || (kind === "error" ? "warning" : (kind === "loading" ? "refresh" : "inbox")));
                if (ic)
                    parts.push(el("div.sw-state-icon", { attrs: { "aria-hidden": "true" } }, ic));
            }
            const txt = el("div.sw-state-body");
            if (x.Title)
                txt.append(el("p.sw-state-title", { text: x.Title }));
            if (x.Text)
                txt.append(el("p.sw-state-text", { text: x.Text }));
            if (x.Small)
                parts.push(txt);
            else
                parts.push(...txt.childNodes);
            const acts = elementsOf(x.Actions || []);
            if (acts.length)
                parts.push(el("div.sw-state-actions", null, ...acts));
            if (x.Details)
                parts.push(el("details", null, el("summary", { text: _TF("Details", "Disclosure that shows the technical text of an error") }), el("pre", { text: String(x.Details) })));
            e.replaceChildren(...parts);
        };
        set(o);
        return { E: e, Set: set };
    }

    /**
     * A meter (Design v2, PORTING.md B11: proportions of one total): div.sw-meter > i, role meter with min / max / now.
     * Callable with or without new. Options: Label (accessible name), Value (0..Max), Max (default 100), and the levels:
     *  - no Warn and no Danger: a plain bar, never coloured (a progress, a share); give the thresholds to get levels;
     *  - Good: "low" (default: a use, more is worse; at or above Warn = warn, at or above Danger = danger: cpu Warn 0.8, Danger 0.92)
     *    or "high" (more is better; at or below Warn = warn, at or below Danger = danger: "Shipped on time" Max 100, Warn 95, Danger 90);
     *  - Warn / Danger: a fraction of Max (0 <= x <= 1: 0.8 = 80 %) or an absolute value on the meter scale (x > 1: Max 100, Warn 70,
     *    Danger 90; an absolute threshold keeps its value when Max changes); Absolute: true reads every threshold as a value on the
     *    scale (Max 5, Warn 1 = 1 of 5); either one may be left out;
     *  - Levels: false: never coloured (the thresholds are ignored).
     * Members: E, Set(value, max) (also updates is-warn / is-danger and aria-valuenow), Level ("ok" | "warn" | "danger"; "ok" without levels).
     * @param {object} [o] Options.
     * @returns {{E: HTMLElement, Set: function(number, number=):string}} The meter.
     */
    function Meter(o) {
        o = o || {};
        checkOpts("Meter", o);
        const bar = el("i");
        const e = el("div.sw-meter", { attrs: { role: "meter", "aria-valuemin": "0", "aria-label": o.Label || null } }, bar);
        const m = {
            E: e,
            Level: "ok",
            /**
             * @param {number} v The value.
             * @param {number} [max] The maximum (default: the last one, initially o.Max or 100).
             * @returns {string} The level ("ok" | "warn" | "danger").
             */
            Set(v, max) {
                if (max > 0)
                    o.Max = max;
                const mx = o.Max > 0 ? o.Max : 100;
                const f = Math.max(0, Math.min(1, (+v || 0) / mx));
                bar.style.width = (f * 100).toFixed(2) + "%";
                e.setAttribute("aria-valuemax", String(mx));
                e.setAttribute("aria-valuenow", String(+v || 0));
                //  a threshold above 1 is an absolute value on the meter scale (Max 100, Danger 90), else a fraction of Max (0.92)
                //  no thresholds = no levels (a plain bar); Good "high" = low values are bad; Absolute = every threshold is a value
                const r = (+v || 0) / mx;
                const frac = t => {
                    if (t === undefined || t === null || t === "")
                        return null;
                    t = +t;
                    return !isFinite(t) || t < 0 ? null : (o.Absolute || t > 1 ? t / mx : t);
                };
                const wf = frac(o.Warn);
                const df = frac(o.Danger);
                let lv = "ok";
                if (o.Levels !== false) {
                    if (o.Good === "high")
                        lv = df !== null && r <= df ? "danger" : (wf !== null && r <= wf ? "warn" : "ok");
                    else
                        lv = df !== null && r >= df ? "danger" : (wf !== null && r >= wf ? "warn" : "ok");
                }
                e.classList.toggle("is-warn", lv === "warn");
                e.classList.toggle("is-danger", lv === "danger");
                m.Level = lv;
                return lv;
            },
        };
        m.Set(o.Value || 0, o.Max);
        return m;
    }

    // ------------------------------------------------------------------ Design v2 page anatomy (shared by every app)

    /**
     * The name of this page's main menu item when it is shown in the app shell (the item whose url is exactly this page, also the url
     * the frame was opened with), else null. Sw.PageHeader uses it as the title (MenuTitle option, default on), so the h1, the
     * breadcrumb of the top bar and the menu always say the same ("Process information", not the server title "Process info table").
     * @returns {?string} The menu item name.
     */
    function MenuTitle() {
        try {
            if (window.top === window || window.parent !== window.top)
                return null;
            const app = window.top.SysWeaverApp;
            if (!app || typeof app.FindEntry !== "function")
                return null;
            const en = app.FindEntry(window.location.href, window.frameElement);
            const n = en && en.Item ? String(en.Item.Name || "").trim() : "";
            return n || null;
        }
        catch {
            return null;
        }
    }

    /**
     * Turn a value into nodes: a text, a number, an element, a component (.E), an array of those; null / false are skipped.
     * @param {*} v The value.
     * @returns {Node[]} The nodes.
     */
    function nodesOf(v) {
        if (v === null || v === undefined || v === false)
            return [];
        if (Array.isArray(v))
            return v.flatMap(nodesOf);
        if (v instanceof Node)
            return [v];
        if (v.E instanceof Node)
            return [v.E];
        return [document.createTextNode(String(v))];
    }

    /**
     * Make a status badge: a text, {Text, Kind ("ok" | "error" | "warning" | "accent" | "info"), Dot, Title}, or an element.
     * @param {*} s The status.
     * @returns {?Element} The badge.
     */
    function badgeOf(s) {
        if (!s)
            return null;
        if (s instanceof Element)
            return s;
        if (s.E instanceof Element)
            return s.E;
        const o = typeof s === "string" ? { Text: s } : s;
        const b = Sw.Badge ? Sw.Badge(o.Text || "", o.Kind, o.Title) : el("span.sw-badge", { text: o.Text || "" });
        if (o.Dot)
            b.classList.add("sw-dot");
        return b;
    }

    /**
     * Make an element open a page or run an action on click (Href: a page url of this site, opened in place like a menu item;
     * OnClick: a function, one run at a time, errors become a toast).
     * @param {Element} e The element (a button).
     * @param {{Href: ?string, OnClick: ?function(Event):*}} o The options.
     */
    function bindOpen(e, o) {
        //  Sw.OnClick: pure clicks only (a modifier click focuses, INP-2), busy while the work runs (FBK-2 / 3), errors as a toast
        Sw.OnClick(e, (ev, ctx) => {
            if (o.Href) {
                Sw.Page.Navigate(o.Href, "_self");
                return undefined;
            }
            return o.OnClick ? o.OnClick(ev, ctx) : undefined;
        }, { NoKey: /^(BUTTON|A)$/.test(e.tagName), Leave: false });
    }

    /** @type {number} Counter for the ids of card titles. */
    let cardN = 0;

    /**
     * A card (Design v2, PORTING.md B12 / PORTING.md B11): section.sw-card > div.sw-card-head (h2.sw-card-title, span.sw-card-sub, an optional count
     * badge, div.sw-card-tools) + div.sw-card-body. Flat (surface + hairline, radius 8, no shadow). Callable with or without new.
     * Options: Title, Sub (one line next to the title), Count (a number badge after the title), Tools (alias Actions; buttons, button options {Text, Icon, Kind, OnClick} / Sw.Segmented / elements,
     * right aligned), Body (nodes / components / text), Flush (no body padding: lists, charts, tables edge to edge), Span (3 | 4 | 6 | 8
     * | 12: the .sw-span-n class in a .sw-grid), Tag (default "section"), Level (title heading level, default 2), Class, Label (accessible
     * name when there is no title), OnClick(event, { Signal }) / Href (a clickable card: its title becomes the button that opens it, a pure
     * click on the card's free area clicks it, a modifier click only focuses it (INP-2), work makes it busy (Sw.OnClick)). No title and no
     * tools: no head row.
     * Members: E, HeadE, TitleE, SubE, CountE, ToolsE, BodyE; SetTitle(t), SetSub(t), SetCount(n|null), SetTools(items), SetBody(content).
     * @param {object} [o] Options.
     * @returns {object} The card.
     */
    function Card(o) {
        o = o || {};
        checkOpts("Card", o);
        const id = "sw-card-" + (++cardN);
        const titleE = el("h" + Math.max(2, Math.min(6, o.Level || 2)) + ".sw-card-title", { id });
        const subE = el("span.sw-card-sub");
        const countE = el("span.sw-badge.sw-card-count");
        const toolsE = el("div.sw-card-tools");
        const headE = el("div.sw-card-head", null, titleE, subE, countE, toolsE);
        const bodyE = el("div.sw-card-body", { class: o.Flush ? "sw-flush" : null });
        const e = el((o.Tag || "section") + ".sw-card", { class: [o.Span ? "sw-span-" + o.Span : "", o.Class || ""].join(" ").trim() || null }, headE, bodyE);
        //  A clickable card (OnClick / Href): the title is a button (one Tab stop named by the card's title); a pure click on the card's
        //  free area clicks it, a modifier click only focuses it (INP-2), a text selection by drag is no click
        const openB = o.OnClick || o.Href ? el("button.sw-card-open", { type: "button" }) : null;
        if (openB) {
            e.classList.add("sw-card-click");
            bindOpen(openB, { Href: o.Href, OnClick: o.OnClick });
            e.addEventListener("click", ev => {
                const t = ev.target instanceof Element ? ev.target : null;
                if (!t || openB.contains(t) || t.closest("a[href], button, input, select, textarea, label, summary, [role=button], [role=link], [role=switch], [role=checkbox], [role=radio], [role=tab], [contenteditable]"))
                    return;
                if (Sw.ModifierClick(ev, openB))
                    return;
                const sel = window.getSelection && window.getSelection();
                if (sel && !sel.isCollapsed && e.contains(sel.anchorNode))
                    return;
                openB.click();
            });
        }
        const c = {
            E: e, HeadE: headE, TitleE: titleE, SubE: subE, CountE: countE, ToolsE: toolsE, BodyE: bodyE,
            /** Show / hide the head row (shown with a title or tools) and name the card by its title. */
            Sync() {
                headE.hidden = !titleE.textContent && !toolsE.childNodes.length;
                if (titleE.textContent)
                    e.setAttribute("aria-labelledby", id);
                else {
                    e.removeAttribute("aria-labelledby");
                    if (o.Label)
                        e.setAttribute("aria-label", o.Label);
                }
            },
            /** @param {string} t The title. */
            SetTitle(t) {
                if (openB) {
                    openB.textContent = t || o.Label || "";
                    titleE.replaceChildren(openB);
                }
                else
                    titleE.textContent = t || "";
                c.Sync();
            },
            /** @param {string} t The sub line (empty hides it). */
            SetSub(t) {
                subE.textContent = t || "";
            },
            /** @param {?number|string} n The count (null / undefined hides the badge). */
            SetCount(n) {
                countE.hidden = n === null || n === undefined || n === "";
                countE.textContent = countE.hidden ? "" : (typeof n === "number" && Sw.Format && Sw.Format.Number ? Sw.Format.Number(n) : String(n));
            },
            /** @param {Array<*>|*} items The tools (nodes, components, or button options {Text, Icon, Kind, OnClick, ...} = a Sw.Button). */
            SetTools(items) {
                const btn = a => a && typeof a === "object" && !Array.isArray(a) && !(a instanceof Node) && !(a.E instanceof Node) && (a.Text || a.Icon) && Sw.Button ? new Sw.Button(a) : a;
                toolsE.replaceChildren(...nodesOf((Array.isArray(items) ? items : [items]).map(btn)));
                c.Sync();
            },
            /** @param {*} content The body content (replaces it). */
            SetBody(content) {
                bodyE.replaceChildren(...nodesOf(content));
            },
        };
        c.SetTitle(o.Title);
        c.SetSub(o.Sub);
        c.SetCount(o.Count);
        //  Actions (the name Sw.PageHeader, Sw.StateView and the dialogs use) = Tools: buttons in the card head, never dropped
        c.SetTools(o.Tools !== undefined && o.Tools !== null ? o.Tools : o.Actions);
        c.SetBody(o.Body);
        return c;
    }

    /**
     * A facts strip (Design v2, PORTING.md B11: replaces rows of fact pills): dl.sw-facts (in a card by default), one cell per fact (div.sw-fact
     * > dt + dd), cells divided by hairlines, 2 per row on phones. A fact with Href / OnClick has a button value (opens a page, copies ...).
     * Callable with or without new. Options: Items [{Key, Label, Value, Title (tool tip), Href, OnClick, Copy (a click copies the shown value), Mono (code font)}], Card (default
     * true: the strip is a card; inside another card it drops its frame anyway, ui/page.css), Label (accessible name), Class.
     * Members: E; Add(item) -> the fact {E, ValueE, Set(value), SetTitle(text)}; Get(key) -> the fact; Set(key, value).
     * @param {object} [o] Options.
     * @returns {object} The strip.
     */
    function Facts(o) {
        o = o || {};
        checkOpts("Facts", o);
        checkEach("Facts item", o.Items);
        const e = el("dl.sw-facts", { class: [o.Card === false ? "" : "sw-card", o.Class || ""].join(" ").trim() || null, attrs: { "aria-label": o.Label || null } });
        const map = new Map();
        const f = {
            E: e,
            /**
             * @param {object} it The fact ({Key, Label, Value, Title, Href, OnClick, Mono}).
             * @returns {{E: HTMLElement, ValueE: HTMLElement, Set: function(*):void, SetTitle: function(string):void}} The fact.
             */
            Add(it) {
                if (it.Copy && !it.Href && !it.OnClick)
                    it = Object.assign({}, it, { OnClick: () => Sw.Clipboard.Copy(v.textContent) });
                const clickable = !!(it.Href || it.OnClick);
                const v = clickable ? el("button.sw-fact-value", { type: "button" }) : el("span.sw-fact-text");
                if (it.Mono)
                    v.classList.add("sw-mono");
                if (clickable)
                    bindOpen(v, it);
                const cell = el("div.sw-fact", null, el("dt", { text: it.Label || "" }), el("dd", null, v));
                e.append(cell);
                const x = {
                    E: cell, ValueE: v,
                    /** @param {*} val The value (text, number or nodes). */
                    Set(val) {
                        if (typeof val === "string" || typeof val === "number") {
                            const s = String(val);
                            if (v.textContent !== s)
                                v.textContent = s;
                        }
                        else
                            v.replaceChildren(...nodesOf(val));
                    },
                    /** @param {string} t The tool tip. */
                    SetTitle(t) {
                        if (t)
                            v.title = t;
                        else
                            v.removeAttribute("title");
                    },
                };
                x.Set(it.Value ?? "");
                x.SetTitle(it.Title);
                if (it.Title && Sw.Hint && Sw.Hint.Attach)
                    Sw.Hint.Attach(v);
                map.set(it.Key || it.Label, x);
                return x;
            },
            /** @param {string} key The key (or label). @returns {?object} The fact. */
            Get(key) {
                return map.get(key) || null;
            },
            /** @param {string} key The key (or label). @param {*} value The value. */
            Set(key, value) {
                const x = map.get(key);
                if (x)
                    x.Set(value);
            },
        };
        for (const it of o.Items || [])
            if (it)
                f.Add(it);
        return f;
    }

    /**
     * A KPI tile (Design v2, PORTING.md B11): a card with an icon + label, the value (heading font, 32 px) with a small unit, an optional
     * meter (proportions of one total: cpu, memory, a drive; warning / danger at thresholds) and a context line ("28.36 GB of 63.35 GB").
     * A button when it opens a page (Href / OnClick). No donuts for a single value. Callable with or without new.
     * Options: Label, Icon (Sw.Icon name, icon url or element), Value, Unit, Sub, Meter (true = a plain bar, or the Sw.Meter options {Max, Warn, Danger, Good, Absolute, Levels}; the meter value is
     * the numeric Value unless Set gets MeterValue), Span (grid span, default 3; 0 = none), Href, OnClick, Title (tool tip), Class.
     * Members: E, ValueE, UnitE, SubE, Meter (Sw.Meter or null); Set({Value, Unit, Sub, MeterValue, Max}) (only the given fields change,
     * the DOM only when a text changed); Level() ("ok" | "warn" | "danger").
     * @param {object} [o] Options.
     * @returns {object} The tile.
     */
    function Kpi(o) {
        o = o || {};
        checkOpts("Kpi", o);
        const valueE = el("span.sw-kpi-num");
        const unitE = el("small");
        const subE = el("div.sw-kpi-sub");
        const meter = o.Meter ? Meter(Object.assign({ Label: o.Label }, o.Meter === true ? {} : o.Meter)) : null;
        const parts = [
            el("div.sw-kpi-label", null, iconOf(o.Icon), el("span", { text: o.Label || "" })),
            el("div.sw-kpi-value", null, valueE, unitE),
            meter ? meter.E : null,
            subE,
        ];
        const cls = ["sw-card", "sw-kpi", o.Span === 0 ? "" : "sw-span-" + (o.Span || 3), o.Class || ""].join(" ").replace(/\s+/g, " ").trim();
        const clickable = !!(o.Href || o.OnClick);
        const e = clickable
            ? el("button", { type: "button", class: cls, title: o.Title || null }, ...parts)
            : el("div", { class: cls, title: o.Title || null }, ...parts);
        if (clickable)
            bindOpen(e, o);
        if (o.Title && Sw.Hint && Sw.Hint.Attach)
            Sw.Hint.Attach(e);
        const setText = (x, v) => {
            const s = v === null || v === undefined ? "" : String(v);
            if (x.textContent !== s)
                x.textContent = s;
        };
        const k = {
            E: e, ValueE: valueE, UnitE: unitE, SubE: subE, Meter: meter,
            /**
             * Update the tile (only the given fields).
             * @param {{Value: *, Unit: string, Sub: *, MeterValue: number, Max: number}} x The new values.
             */
            Set(x) {
                if ("Value" in x)
                    setText(valueE, x.Value);
                if ("Unit" in x)
                    setText(unitE, x.Unit);
                if ("Sub" in x) {
                    if (x.Sub instanceof Node || Array.isArray(x.Sub))
                        subE.replaceChildren(...nodesOf(x.Sub));
                    else
                        setText(subE, x.Sub);
                }
                if (meter && ("MeterValue" in x || "Value" in x || "Max" in x)) {
                    const mv = "MeterValue" in x ? x.MeterValue : parseFloat(x.Value);
                    if (isFinite(mv))
                        meter.Set(mv, x.Max);
                }
            },
            /** @returns {string} The meter level ("ok" without a meter). */
            Level() {
                return meter ? meter.Level : "ok";
            },
        };
        k.Set({ Value: o.Value ?? "", Unit: o.Unit ?? "", Sub: o.Sub ?? "" });
        return k;
    }

    /**
     * A list card (Design v2, PORTING.md B11: "Running services", "Problems", "Recent pages"): a card whose body is a row list ul.sw-rows; each
     * row: an optional status badge (Status {Text, Kind, Dot}), an icon, the text and a right aligned meta text; a row with Href / OnClick
     * is a button (li > button.sw-rows-link). An empty list shows a compact empty state (Empty {Title, Text, Icon, Actions}).
     * Callable with or without new. Options: the Card options (Title, Sub, Tools, Span, Class, ...), Items, Empty, Count (true: the number
     * of items as the count badge).
     * Members: the card members plus ListE and SetItems(items) (rebuilds the rows; keeps the focus on the row with the same Key / text).
     * Item: {Key, Text, Icon, Status, Meta, Title, Href, OnClick}.
     * @param {object} [o] Options.
     * @returns {object} The list card.
     */
    function ListCard(o) {
        o = o || {};
        checkOpts("ListCard", o);
        checkEach("ListCard item", o.Items);
        const c = Card(Object.assign({}, o, { Flush: true, Body: null, Count: null, Items: undefined, Empty: undefined }));
        const ul = el("ul.sw-rows");
        c.ListE = ul;
        c.SetItems = items => {
            items = (items || []).filter(x => x);
            if (o.Count)
                c.SetCount(items.length);
            if (!items.length) {
                const em = o.Empty || {};
                c.BodyE.replaceChildren(StateView({ Small: true, Icon: em.Icon || "info", Title: em.Title || _TF("Nothing to show", "Title of the empty state of a list card"), Text: em.Text || "", Actions: em.Actions || [] }).E);
                return;
            }
            const a = document.activeElement;
            const focusKey = a && ul.contains(a) && a.dataset ? a.dataset.key : null;
            const rows = items.map(it => {
                const key = String(it.Key ?? it.Text ?? "");
                const inner = [
                    badgeOf(it.Status),
                    it.Icon ? iconOf(it.Icon) : null,
                    el("span.sw-rows-text", { text: it.Text || "" }),
                    it.Meta !== undefined && it.Meta !== null && it.Meta !== "" ? el("span.sw-rows-meta", { text: String(it.Meta) }) : null,
                ].filter(x => x);
                if (it.Href || it.OnClick) {
                    const b = el("button.sw-rows-link", { type: "button", title: it.Title || null }, ...inner);
                    b.dataset.key = key;
                    bindOpen(b, it);
                    return el("li", null, b);
                }
                const li = el("li", { title: it.Title || null }, ...inner);
                li.dataset.key = key;
                return li;
            });
            ul.replaceChildren(...rows);
            if (ul.parentNode !== c.BodyE)
                c.BodyE.replaceChildren(ul);
            if (focusKey !== null && focusKey !== undefined) {
                const f = [...ul.querySelectorAll("button[data-key]")].find(x => x.dataset.key === focusKey);
                if (f)
                    f.focus({ preventScroll: true });
            }
        };
        c.SetItems(o.Items || []);
        return c;
    }

    /**
     * A 12 column grid (Design v2, ui/README.md Page anatomy): div.sw-grid (16 px gap); children use .sw-span-3/4/6/8/12 (Sw.Kpi / Sw.Card: the Span option).
     * Callable with or without new. Options: Content (elements / components), Class, Label (accessible name; the grid is then a region), Tag.
     * Members: E, Add(...items).
     * @param {object} [o] Options.
     * @returns {{E: HTMLElement, Add: function(...*):object}} The grid.
     */
    function Grid(o) {
        o = o || {};
        checkOpts("Grid", o);
        const e = el((o.Tag || "div") + ".sw-grid", { class: o.Class || null, attrs: { "aria-label": o.Label || null, role: o.Label && !o.Tag ? "region" : null } });
        const g = {
            E: e,
            /** @param {...*} items Elements / components. @returns {object} This. */
            Add(...items) {
                e.append(...nodesOf(items));
                return g;
            },
        };
        g.Add(o.Content || []);
        return g;
    }

    /**
     * A section title between grids (Design v2, PORTING.md B11): div.sw-section-title > h2 + span (one line).
     * @param {string} title The title.
     * @param {string} [sub] The sub line.
     * @returns {HTMLElement} The element.
     */
    function SectionTitle(title, sub) {
        return el("div.sw-section-title", null, el("h2", { text: title || "" }), sub ? el("span", { text: sub }) : null);
    }

    /**
     * Build the Design v2 page skeleton (Design v2, PORTING.md B10 "One page anatomy"): main.sw-page-<Type> with the page header first, in the
     * body (or Parent). The header title is the page's menu item name inside the shell (Sw.MenuTitle; MenuTitle: false keeps Title), else
     * Title, else the document title. A page that already has its own main keeps it (Main: the element or a selector; it gets the class).
     * Callable with or without new. Options: Type ("dash" (default) | "form" | "read" | "fill"), Title, Description, Status, Actions,
     * Compact (default: true for fill pages), MenuTitle (default true), Main, Parent, Class, Header (false: no header).
     * Members: E (the main), Header (the Sw.PageHeader, or null), Add(...items) (appends after the header).
     * @param {object} [o] Options.
     * @returns {{E: HTMLElement, Header: ?object, Add: function(...*):object}} The page.
     */
    function PageLayout(o) {
        o = o || {};
        checkOpts("PageLayout", o);
        const type = o.Type || "dash";
        let main = typeof o.Main === "string" ? document.querySelector(o.Main) : o.Main;
        if (!main) {
            main = el("main");
            (o.Parent || document.body).append(main);
        }
        main.classList.add("sw-page-" + type);
        if (o.Class)
            main.classList.add(...o.Class.split(" ").filter(x => x));
        let header = null;
        if (o.Header !== false) {
            header = Sw.PageHeader({
                Title: o.Title || document.title, Description: o.Description, Status: o.Status, Actions: o.Actions,
                Compact: o.Compact ?? type === "fill", MenuTitle: o.MenuTitle,
            });
            main.prepend(header.E);
        }
        const p = {
            E: main, Header: header,
            /** @param {...*} items Elements / components. @returns {object} This. */
            Add(...items) {
                main.append(...nodesOf(items));
                return p;
            },
        };
        return p;
    }

    // ------------------------------------------------------------------ shared page parts promoted from ServerManager

    /** Upload status codes of common/fileUploader.js (0 = uploaded, 1 = already there). */
    const UploadOk = 0;
    const UploadAlready = 1;

    /**
     * Show the outcome of an upload as one toast (was SmShared.ReportUpload): an error, the first failing file, the first
     * already uploaded file, or the first uploaded file (then onUploaded is called).
     * @param {{Error: (string|Error|null), Status: Array<number>}} res Result of the upload library (common/fileUploader.js).
     * @param {File[]|FileList} files The files (same order as res.Status).
     * @param {function():void} [onUploaded] Called when at least one file was uploaded.
     */
    function ReportUpload(res, files, onUploaded) {
        if (!res)
            return;
        const err = res.Error;
        if (err) {
            Sw.Fail(err.message || String(err));
            return;
        }
        const ss = res.Status || [];
        const failed = ss.findIndex(s => s !== UploadOk && s !== UploadAlready);
        if (failed >= 0) {
            const code = ss[failed];
            const text = typeof window.fileUploaderStatusText === "function" ? window.fileUploaderStatusText(code) : String(code);
            Sw.Fail(_T("{0}, when uploading \"{1}\"", text, files[failed].name, "Text displayed when uploading of a file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the file"));
            return;
        }
        const already = ss.indexOf(UploadAlready);
        if (already >= 0) {
            Sw.Info(_T("File \"{0}\" was already uploaded", files[already].name, "Text displayed when a file have already been uploaded to a server.{0} is replaced with the name of the file"));
            return;
        }
        const ok = ss.indexOf(UploadOk);
        if (ok >= 0) {
            Sw.Info(_T("Uploaded \"{0}\"", files[ok].name, "Text displayed when a file was successfully uploaded to a server.{0} is replaced with the name of the file"));
            if (onUploaded)
                onUploaded();
        }
    }

    /**
     * Make an element an upload target for a server repo (was SmShared.UploadTarget): click / Enter / Space opens the file
     * picker, files can be dropped on it, several files at once. Needs the page to include common/fileUploader.js (the FileUploader
     * module; its public fileUploaderSetup toggles the classes Dragging, Selecting and Uploading); without it the element is disabled
     * with a tool tip.
     * @param {HTMLElement} e The target (a button element, so it is keyboard and touch operable).
     * @param {string} repo The repo name (ex: "Keys").
     * @param {function():void} [onUploaded] Called after a successful upload.
     * @param {{Url: string, Auth: *}} [guard] Disable the target for users without the role (Sw.Access.Guard; Url: a server method with the
     * same auth, ex: the page's table). Without it the target is disabled after the first refused request of the page.
     * @returns {boolean} False when the upload library is missing.
     */
    function UploadTarget(e, repo, onUploaded, guard) {
        if (typeof window.fileUploaderSetup !== "function") {
            //  (disabled with its reason, INP-12: the reason is the tool tip and what an attempt says; setting "disabled" first gave
            //  "Not available right now")
            const why = _TF("File uploads are not available on this service", "Tool tip of an upload button when the file upload service is missing");
            if (Sw.Disabled && typeof Sw.Disabled.Set === "function")
                Sw.Disabled.Set(e, true, why);
            else {
                e.disabled = true;
                e.title = why;
            }
            return false;
        }
        window.fileUploaderSetup(e, repo, null, (ev, res, files) => ReportUpload(res, files, onUploaded), null, true);
        if (Sw.Access && Sw.Access.Guard)
            Sw.Access.Guard(e, guard || {});
        return true;
    }

    /**
     * An upload button that is also a drop target (was SmShared.UploadButton): a secondary Sw.Button with the upload icon
     * and a word; files dragged over it give a dashed accent frame (button.sw-dropbtn.Dragging, ui/page.css). Callable with or without new.
     * Options: Text (visible text, "Upload key files"), Title (tool tip), Repo (the server repo), OnUploaded, Guard (see Sw.UploadTarget),
     * Small (card heads), Icon (default "upload"), Class.
     * @param {object} o Options.
     * @returns {object} The button (Sw.Button).
     */
    function UploadButton(o) {
        o = o || {};
        checkOpts("UploadButton", o);
        const b = new Sw.Button({ Text: o.Text, Icon: o.Icon || "upload", Title: o.Title, Small: !!o.Small, Class: ("sw-dropbtn " + (o.Class || "")).replace(/\s+/g, " ").trim() });
        UploadTarget(b.E, o.Repo, o.OnUploaded, o.Guard);
        return b;
    }

    /**
     * Put a live data table (explore/table.js Sw.Table in page mode: the table frame takes the free height, the page size follows it) into
     * a fill page (Sw.PageLayout Type "fill") (was SmShared.FillTable). The table's own compact header is dropped (the page
     * has one) and its refresh line ("Live, updates every 5 s") moves under the page description. Needs explore/table.js on the page.
     * @param {object} page The page (Sw.PageLayout).
     * @param {string} url The table api url (ex: "../ServerManager/KeysTable").
     * @param {object} [o] More Sw.Table.Create options (ex: { Class }); Class is added to the host div.
     * @returns {Promise<object>} The table (UpdateNow() ...).
     */
    async function FillTable(page, url, o) {
        o = o || {};
        const host = el("div.sw-fill.sw-fill-table", { class: o.Class || null });
        page.Add(host);
        const opts = Object.assign({}, o, { Url: url, Element: host, Page: true });
        delete opts.Class;
        //  The table is named like its page (the empty state "There is nothing in Services right now", exports, the grid's accessible
        //  name), not like its api ("Services table"); the saved table state is keyed by the url, so it is not affected
        const title = page.Header && page.Header.TitleE ? page.Header.TitleE.textContent.trim() : "";
        if (!opts.Name && title)
            opts.Name = title;
        const t = await Sw.Table.Create(opts);
        if (t.PH) {
            t.PH.remove();
            if (t.PHDesc && page.Header)
                page.Header.E.querySelector(".sw-ph-text").append(t.PHDesc);
        }
        return t;
    }

    // ------------------------------------------------------------------ list / detail: Sw.SelectList, Sw.ListDetail

    /** Counter for the ids of list rows and step headings. */
    let pageUid = 0;

    /**
     * A selectable list (ui/README.md "List / detail"): ul[role=listbox] with the focus on the list and the active row in
     * aria-activedescendant, so a re-render (live data) never loses the focus.
     *  - Single select (default): the selection follows the arrows (a master / detail list); click selects.
     *  - Multi (Multi: true): arrows move the active row, Space toggles it, Shift+arrows / Shift+click select a range,
     *    Ctrl/Cmd+click toggles, Ctrl/Cmd+A selects all; rows show a check box.
     *  - Keys: Up / Down, Home / End, Page Up / Page Down, Enter (OnOpen), letters jump to the next row starting with them.
     *  - No items: a compact Sw.StateView (Empty: {Icon, Title, Text, Actions}, or a function returning it).
     * Callable with or without new. Options: Items, Value (key, or keys when Multi), Multi, Label (accessible name), Render(item, li)
     * (the row content: nodes / an element / text; default: Icon, Text, Sub, Meta, Status), Empty, OnSelect(value, items, how) (value =
     * the key or null, keys when Multi; how = "click" | "key" | "api": a list / detail page opens the detail on a phone only for
     * "click"), OnOpen(item) (Enter, double click, a click on the selected row), Class.
     * Item: {Key (unique, required; a string or a number: Get(), Active and OnSelect give it back as given), Text, Sub, Meta, Icon, Status ({Text, Kind, Dot} or a string), Title, Class, Disabled}.
     * Members: E, ListE, Items, SetItems(items) (keeps the selection and the active row by key; selected keys that are gone are dropped
     * and OnSelect is called), Get(), Set(value, silent), Item(key), Row(key), Active (the active key), Focus(), Move(delta).
     * @param {object} [o] Options.
     * @returns {object} The list.
     */
    function SelectList(o) {
        o = o || {};
        checkOpts("SelectList", o);
        const id = "sw-sl" + (++pageUid);
        const multi = !!o.Multi;
        const ul = el("ul.sw-selectlist-list", { tabIndex: 0, attrs: { role: "listbox", "aria-label": o.Label || null, "aria-multiselectable": multi ? "true" : null } });
        const empty = el("div.sw-selectlist-empty");
        empty.hidden = true;
        const e = el("div.sw-selectlist", { class: (multi ? "sw-selectlist-multi " : "") + (o.Class || "") }, ul, empty);
        /** @type {Set<string>} */
        let sel = new Set();
        let anchor = null;
        let typed = "", typedAt = 0;
        /** The active row (rows and sets work on the key text, the data-key). */
        let act = null;
        /**
         * The key as given in the item (a number stays a number).
         * @param {string} k Key text.
         * @returns {*} The item's Key.
         */
        const orig = k => { const it = k === null ? null : l.Item(k); return it ? it.Key : k; };
        const l = {
            E: e, ListE: ul, Items: [],
            /** @returns {*} The active key (as given in the item). */
            get Active() { return orig(act); },
            /** @param {string} key Key. @returns {?object} The item. */
            Item: key => l.Items.find(x => String(x.Key) === String(key)) || null,
            /** @param {string} key Key. @returns {?HTMLElement} The row. */
            Row: key => ul.querySelector("li[data-key=\"" + CSS.escape(String(key)) + "\"]"),
            /** @returns {*} The selected key (keys when Multi), as given in the items (a number key stays a number). */
            Get: () => multi ? [...sel].map(orig) : (sel.size ? orig([...sel][0]) : null),
            /**
             * Select (and make active) a key, or keys when Multi.
             * @param {string|string[]|null} v The value.
             * @param {boolean} [silent] Don't call OnSelect.
             */
            Set(v, silent) {
                const keys = (Array.isArray(v) ? v : (v === null || v === undefined ? [] : [v])).map(String).filter(k => l.Item(k));
                sel = new Set(multi ? keys : keys.slice(0, 1));
                if (keys.length)
                    setActive(keys[keys.length - 1], true);
                paint();
                if (!silent)
                    fire();
            },
            /** Focus the list. */
            Focus: () => ul.focus({ preventScroll: true }),
            /**
             * Move the active row (single select: the selection) by delta rows.
             * @param {number} delta Rows (+1 next, -1 previous).
             */
            Move(delta) {
                const keys = enabledKeys();
                if (!keys.length)
                    return;
                let i = keys.indexOf(act);
                i = i < 0 ? (delta > 0 ? 0 : keys.length - 1) : Math.max(0, Math.min(keys.length - 1, i + delta));
                go(keys[i], false, false, "key");
            },
            /**
             * Replace the items (rows rebuilt; selection and active row kept by key).
             * @param {object[]} items Items.
             */
            SetItems(items) {
                l.Items = (items || []).filter(x => x && x.Key !== undefined && x.Key !== null);
                const had = sel.size;
                sel = new Set([...sel].filter(k => l.Item(k)));
                if (act !== null && !l.Item(act))
                    act = null;
                render();
                if (sel.size !== had)
                    fire();
            },
        };
        /** @returns {string[]} The keys of the rows that can be selected. */
        const enabledKeys = () => l.Items.filter(x => !x.Disabled).map(x => String(x.Key));
        const fire = how => o.OnSelect && o.OnSelect(l.Get(), l.Items.filter(x => sel.has(String(x.Key))), how || "api");
        /**
         * Set the active row (aria-activedescendant) and scroll it into view.
         * @param {?string} key Key.
         * @param {boolean} [noScroll] Don't scroll.
         */
        const setActive = (key, noScroll) => {
            act = key;
            for (const li of ul.children)
                li.classList.toggle("sw-active", li.dataset.key === key);
            const r = key !== null ? l.Row(key) : null;
            if (r)
                ul.setAttribute("aria-activedescendant", r.id);
            else
                ul.removeAttribute("aria-activedescendant");
            if (r && !noScroll)
                r.scrollIntoView({ block: "nearest" });
        };
        /** Show the selection on the rows. */
        const paint = () => {
            for (const li of ul.children) {
                const on = sel.has(li.dataset.key);
                li.setAttribute("aria-selected", on ? "true" : "false");
                li.classList.toggle("sw-selected", on);
            }
        };
        /**
         * Go to a row (keyboard / mouse).
         * @param {string} key Key.
         * @param {boolean} toggle Ctrl / Space in a multi list: toggle the row.
         * @param {boolean} range Shift in a multi list: select from the anchor.
         */
        const go = (key, toggle, range, how) => {
            const before = [...sel].join("\n");
            if (!multi)
                sel = new Set([key]);
            else if (range && anchor !== null) {
                const keys = enabledKeys();
                const a = keys.indexOf(anchor), b = keys.indexOf(key);
                if (a >= 0 && b >= 0)
                    keys.slice(Math.min(a, b), Math.max(a, b) + 1).forEach(k => sel.add(k));
            }
            else if (toggle) {
                if (sel.has(key))
                    sel.delete(key);
                else
                    sel.add(key);
                anchor = key;
            }
            else
                anchor = key;
            setActive(key);
            paint();
            if ([...sel].join("\n") !== before)
                fire(how);
        };
        /** Build the rows (or the empty state). */
        const render = () => {
            const rows = l.Items.map((it, i) => {
                const key = String(it.Key);
                const li = el("li.sw-selectlist-row", { id: id + "-" + i, class: it.Class || null, title: it.Title || null, attrs: { role: "option", "aria-disabled": it.Disabled ? "true" : null } });
                li.dataset.key = key;
                if (multi)
                    li.append(el("span.sw-selectlist-check", { attrs: { "aria-hidden": "true" } }, Sw.Icon ? Sw.Icon("check") : null));
                let content = o.Render ? o.Render(it, li) : [
                    it.Icon ? iconOf(it.Icon) : null,
                    el("span.sw-selectlist-text", null, el("span.sw-selectlist-title", { text: it.Text ?? key }), it.Sub ? el("span.sw-selectlist-sub", { text: it.Sub }) : null),
                    it.Meta !== undefined && it.Meta !== null && it.Meta !== "" ? el("span.sw-selectlist-meta", { text: String(it.Meta) }) : null,
                    it.Status ? badgeOf(it.Status) : null,
                ];
                if (typeof content === "string")
                    content = [document.createTextNode(content)];
                li.append(...nodesOf(Array.isArray(content) ? content : [content]));
                return li;
            });
            ul.replaceChildren(...rows);
            const none = !rows.length;
            ul.hidden = none;
            empty.hidden = !none;
            if (none) {
                const em = (typeof o.Empty === "function" ? o.Empty() : o.Empty) || {};
                empty.replaceChildren(StateView({ Small: true, Icon: em.Icon || "list", Title: em.Title || _TF("Nothing to show", "Title of the empty state of a list"), Text: em.Text || "", Actions: em.Actions || [] }).E);
            }
            paint();
            setActive(act, true);
        };
        ul.addEventListener("click", ev => {
            const li = ev.target.closest && ev.target.closest("li.sw-selectlist-row");
            if (!li || !ul.contains(li) || li.getAttribute("aria-disabled") === "true")
                return;
            //  UX guide INP-2: a modifier click never selects or opens; it makes the row the active one and shows the focus. Only a
            //  multi select list gives Ctrl / Cmd (toggle) and Shift (range) a meaning; Alt never selects.
            if ((ev.shiftKey || ev.ctrlKey || ev.altKey || ev.metaKey) && (!multi || ev.altKey)) {
                setActive(li.dataset.key, true);
                if (Sw.Focus && Sw.Focus.Show)
                    Sw.Focus.Show(ul);
                else
                    ul.focus();
                return;
            }
            //  a click on the selected row of a single select list opens it (ex: back on a phone: show the detail again)
            const again = !multi && sel.has(li.dataset.key) && sel.size === 1;
            go(li.dataset.key, multi && (ev.ctrlKey || ev.metaKey || ev.target.closest(".sw-selectlist-check")), multi && ev.shiftKey, "click");
            if (again && o.OnOpen)
                o.OnOpen(l.Item(li.dataset.key));
        });
        ul.addEventListener("dblclick", ev => {
            const li = ev.target.closest && ev.target.closest("li.sw-selectlist-row");
            if (li && o.OnOpen)
                o.OnOpen(l.Item(li.dataset.key));
        });
        ul.addEventListener("keydown", ev => {
            const keys = enabledKeys();
            if (!keys.length || ev.altKey)
                return;
            const mod = ev.ctrlKey || ev.metaKey;
            let i = keys.indexOf(act);
            const page = Math.max(1, Math.floor(ul.clientHeight / ((ul.firstElementChild && ul.firstElementChild.offsetHeight) || 40)) - 1);
            let to = null;
            switch (ev.key) {
                case "ArrowDown": to = i < 0 ? 0 : Math.min(keys.length - 1, i + 1); break;
                case "ArrowUp": to = i < 0 ? keys.length - 1 : Math.max(0, i - 1); break;
                case "Home": to = 0; break;
                case "End": to = keys.length - 1; break;
                case "PageDown": to = Math.min(keys.length - 1, Math.max(0, i) + page); break;
                case "PageUp": to = Math.max(0, i - page); break;
                case " ":
                    if (!multi || act === null)
                        return;
                    go(act, true, false, "key");
                    ev.preventDefault();
                    return;
                case "Enter":
                    if (act === null || !o.OnOpen)
                        return;
                    o.OnOpen(l.Item(act));
                    ev.preventDefault();
                    return;
                case "a":
                case "A":
                    if (multi && mod) {
                        sel = new Set(keys);
                        paint();
                        fire();
                        ev.preventDefault();
                    }
                    return;
                default:
                    //  type ahead: the next row whose text starts with the typed letters
                    if (ev.key.length === 1 && !mod && /\S/.test(ev.key)) {
                        const now = performance.now();
                        typed = (now - typedAt < 700 ? typed : "") + ev.key.toLowerCase();
                        typedAt = now;
                        const textOf = k => { const r = l.Row(k); return r ? r.textContent.trim().toLowerCase() : ""; };
                        const start = typed.length > 1 ? Math.max(0, i) : i + 1;
                        for (let n = 0; n < keys.length; ++n) {
                            const k = keys[(start + n) % keys.length];
                            if (textOf(k).startsWith(typed)) {
                                go(k, false, false, "key");
                                ev.preventDefault();
                                break;
                            }
                        }
                    }
                    return;
            }
            ev.preventDefault();
            if (multi)
                go(keys[to], false, ev.shiftKey, "key");
            else
                go(keys[to], false, false, "key");
        });
        //  The first focus by keyboard shows the selected row as active
        ul.addEventListener("focus", () => {
            if (act === null && sel.size)
                setActive([...sel][0]);
        });
        if (o.Value !== undefined && o.Value !== null)
            sel = new Set((Array.isArray(o.Value) ? o.Value : [o.Value]).map(String));
        l.SetItems(o.Items || []);
        if (sel.size)
            setActive([...sel][0], true);
        return l;
    }

    /**
     * A list / detail layout (ui/README.md "List / detail"): the list and the detail side by side on wide containers (the list in a
     * fixed column), stacked on narrow ones (below Breakpoint px of its own width, not the window): the list first, ShowDetail(true)
     * replaces it with the detail and a Back bar ("Back to the list"; Escape too); Back returns to the list and focuses it.
     * Callable with or without new. Options: List, Detail (elements or components), Label (accessible name), ListWidth (css, default
     * clamp(17rem, 34%, 26rem)), Breakpoint (default 640), Back (false: the app shows its own back button and calls Back()),
     * BackText, OnBack(), OnLayout(narrow), Class.
     * Members: E, ListE, DetailE (the panes), Narrow, Showing ("list" | "detail"), ShowDetail(on, focus), Back(), Layout().
     * @param {object} [o] Options.
     * @returns {object} The layout.
     */
    function ListDetail(o) {
        o = o || {};
        checkOpts("ListDetail", o);
        const listE = el("div.sw-ld-list", null, ...nodesOf([o.List]));
        const backText = o.BackText || _TF("Back to the list", "Button above the detail of a list / detail page on phones that shows the list again");
        const backB = o.Back === false ? null : new Sw.Button({ Text: _TF("Back", "Short text of the button that returns from a detail to its list on phones"), Icon: "left", Kind: "ghost", Title: backText, Label: backText, OnClick: () => d.Back() });
        const bar = backB ? el("div.sw-ld-backbar", null, backB.E) : null;
        const detailE = el("div.sw-ld-detail", { tabIndex: -1 }, bar, ...nodesOf([o.Detail]));
        const e = el("div.sw-ld", { class: o.Class || null, attrs: { role: o.Label ? "region" : null, "aria-label": o.Label || null } }, listE, detailE);
        if (o.ListWidth)
            e.style.setProperty("--sw-ld-list-w", o.ListWidth);
        const bp = o.Breakpoint || 640;
        const d = {
            E: e, ListE: listE, DetailE: detailE, Narrow: false, Showing: "list",
            /**
             * Show the detail (narrow layouts replace the list; wide ones show both anyway).
             * @param {boolean} on Show the detail (false: the list).
             * @param {boolean} [focus] Move the focus (to the detail pane, or back to the list).
             */
            ShowDetail(on, focus) {
                d.Showing = on ? "detail" : "list";
                e.dataset.show = d.Showing;
                if (focus && d.Narrow)
                    (on ? detailE : (listE.querySelector("[role=listbox], [tabindex='0']") || listE)).focus({ preventScroll: true });
            },
            /** Back to the list (narrow layouts; the Back bar, Escape). */
            Back() {
                d.ShowDetail(false, true);
                o.OnBack && o.OnBack();
            },
            /** Measure the width and switch between the wide and the narrow layout. */
            Layout() {
                const w = e.getBoundingClientRect().width;
                if (!w)
                    return;
                const n = w < bp;
                if (n === d.Narrow && e.classList.contains("sw-ld-ready"))
                    return;
                d.Narrow = n;
                e.classList.toggle("sw-ld-narrow", n);
                e.classList.add("sw-ld-ready");
                o.OnLayout && o.OnLayout(n);
            },
        };
        e.dataset.show = "list";
        detailE.addEventListener("keydown", ev => {
            if (ev.key === "Escape" && d.Narrow && d.Showing === "detail" && !ev.defaultPrevented && !document.querySelector(".sw-dialog, .sw-popup")) {
                ev.preventDefault();
                d.Back();
            }
        });
        if (window.ResizeObserver)
            new ResizeObserver(() => d.Layout()).observe(e);
        else
            window.addEventListener("resize", () => d.Layout());
        requestAnimationFrame(() => d.Layout());
        return d;
    }

    // ------------------------------------------------------------------ forms and wizards: Sw.Form, Sw.Steps

    /**
     * The focusable / labelled part of a control (an element, or a component with Input / Button / E).
     * @param {*} c The control.
     * @returns {?HTMLElement} The element.
     */
    function focusOf(c) {
        if (!c)
            return null;
        if (c instanceof Element)
            return c.matches("input, select, textarea, button, [tabindex]") ? c : (c.querySelector("input, select, textarea, button, [tabindex]") || c);
        return c.Input || c.Button || c.Btn || (c.E ? focusOf(c.E) : null);
    }

    /**
     * The value of a control: Get() of a component, value / checked of an element.
     * @param {*} c The control.
     * @returns {*} The value.
     */
    function valueOf(c) {
        if (!c)
            return null;
        if (typeof c.Get === "function")
            return c.Get();
        if (c instanceof HTMLInputElement && (c.type === "checkbox" || c.type === "radio"))
            return c.checked;
        return "value" in c ? c.value : null;
    }

    /**
     * True for an empty value (Required): null, undefined, NaN, blank text, an empty array, false.
     * @param {*} v The value.
     * @returns {boolean} Empty.
     */
    function isEmpty(v) {
        return v === null || v === undefined || v === false || (typeof v === "number" && isNaN(v)) || (typeof v === "string" && !v.trim()) || (Array.isArray(v) && !v.length);
    }

    /**
     * A form level validation helper on Sw controls (ui/README.md "Forms: Sw.Form"): one list of fields, Required and custom rules,
     * the messages under the fields (the control's own SetError when it has one, else a shared error line with aria-invalid /
     * aria-describedby), the focus on the first bad field, and a submit that only runs when everything is valid.
     *  - A field: {Name, Control, Label, Required (true or the message), Validate(value, values) -> message or null, When(values) ->
     *    false skips the field (ex: a mode that hides it)}. The control is a component (TextBox, NumberBox, Select, Segmented, Toggle,
     *    ...: Get(), and SetError / Validate / Commit when it has them) or an input element. The Label defaults to the control's label.
     *  - A control's own rule runs too: Validate() (when the control has it, ex: a future Core API), else Commit() of a TextBox with
     *    Validate (false = invalid; its message is already shown).
     *  - After the first Validate() (or a failed submit) a field is checked again on change / input, so a fixed field clears at once.
     * Callable with or without new. Options: Fields, Submit (the submit Sw.Button or button element: a click submits), OnSubmit(values)
     * (may return a promise: the button is busy until it settles; a false result keeps the form "not submitted"), Gate ("required":
     * the submit button is disabled while a Required field is empty; default: always enabled, a click shows the errors), Title (what the
     * form edits, for the leave question "Your changes to {Title} are lost."), Dirty (opt in, default off as the original, P3-A-4: true or a
     * text (the leave sentence) makes the page dirty while a field differs from its value before the user's first edit, cleared when
     * OnSubmit succeeds (UX guide SAF-10; inside a dialog the dialog asks instead)), Draft (a context: every multi line text field is kept as a draft, SAF-11; a
     * field's NoDraft opts out; cleared on a successful submit), Resend (true: a save that failed because the connection was lost is sent
     * again when it is back, for saves that are safe to repeat; default: "Not saved: the connection was lost. Your entries are kept." with
     * Try again at the form, FBK-9). A Sw.Button Submit without OnClick submits on click and is busy until OnSubmit settles (repeats
     * absorbed). A required field gets the marker and aria-required. An error thrown by OnSubmit is shown in the form's status line
     * (never uncaught).
     * Members: Fields, Validate(focus = true) -> {Ok, Values, Errors: {name: message}}, Values(), SetError(name, message) (ex: a server
     * error for one field), Clear() (all messages), Submit() (validate, then OnSubmit; resolves false when not saved), Add(field),
     * Field(name), IsDirty(), MarkClean().
     * @param {object} [o] Options.
     * @returns {object} The form.
     */
    function Form(o) {
        o = o || {};
        checkOpts("Form", o);
        let checked = false;
        const formId = "sw-form:" + (o.Name || ++pageUid);
        /** @type {Map<object, string>} The base value of each field (before the user's first edit, or after a save). */
        const base = new Map();
        /** @type {Set<object>} Fields the user edited (their base stays until a save). */
        const edited = new Set();
        let dirty = false;
        let saving = false;
        let status = null;
        let offOnline = null;
        const f = {
            Fields: [],
            /** @returns {boolean} True when a field differs from its value before the user's edits (or the last save). */
            IsDirty: () => dirty,
            /** The current values are saved: the form is clean (the new base values; also done after a successful OnSubmit). */
            MarkClean() {
                f.Fields.forEach(fd => base.set(fd, snap(valueOf(fd.Control))));
                edited.clear();
                track();
            },
            /**
             * A button outside the form sent data (Sw.Button, ui/controls.js): when its writes carried every edited value of the form,
             * the form was saved by it (A3-10: a Save in the page header that sends the form's values) and is clean.
             * @param {string} text The url and payload of the writes.
             * @returns {boolean} True when it saved the form.
             */
            SentBy(text) {
                const has = Sw.Edits && Sw.Edits.Carries;
                const eds = f.Fields.filter(fd => edited.has(fd) && base.get(fd) !== snap(valueOf(fd.Control)));
                if (!has || !eds.length || !eds.every(fd => has(text, valueOf(fd.Control))))
                    return false;
                f.MarkClean();
                return true;
            },
            /** @returns {boolean} True when the form's fields left the page (it is dropped from Sw.Edits.Forms). */
            Gone: () => f.Fields.length > 0 && f.Fields.every(fd => {
                const fe = focusOf(fd.Control);
                return !fe || !fe.isConnected;
            }),
            /** @param {string} name Name. @returns {?object} The field. */
            Field: name => f.Fields.find(x => x.Name === name) || null,
            /**
             * Add a field.
             * @param {object} fd The field.
             * @returns {object} The form.
             */
            Add(fd) {
                checkOpts("Form field", fd);
                if (!fd || !fd.Control)
                    return f;
                f.Fields.push(fd);
                //  (known to the buttons of the page: a Save outside the form that sends its values cleans it)
                if (Sw.Edits && Sw.Edits.Forms)
                    Sw.Edits.Forms.add(f);
                const c = fd.Control;
                const fe = focusOf(c);
                //  The form tracks the dirty state of its controls (they don't mark the page themselves)
                c.SwForm = f;
                if (fe)
                    fe.SwForm = f;
                base.set(fd, snap(valueOf(c)));
                //  A required field says so (UX guide FRM-2), also when only the form marks it required: aria-required and the marker
                if (fd.Required && fe && !fe.hasAttribute("aria-required"))
                    fe.setAttribute("aria-required", "true");
                if (fd.Required)
                    addRequiredMark(c, fe);
                //  LAY-9: a field the form checks has its message line before the first message (typing never moves what follows)
                if (fd.Required || fd.Validate) {
                    if (c && c.ErrorE instanceof Element)
                        Sw.Reserve.Line(c.ErrorE);
                    else if (fe && !(c && typeof c.SetError === "function" && !(c instanceof Element))) {
                        show(fd, null);
                        if (fd.ErrorE)
                            Sw.Reserve.Line(fd.ErrorE);
                    }
                }
                //  Drafts (SAF-11): the form's Draft context keeps every multi line text field (not secrets) as a draft
                if (o.Draft && !fd.NoDraft && fe && fe.tagName === "TEXTAREA" && !c.DraftH && Sw.Draft && Sw.Draft.Attach) {
                    const h = Sw.Draft.Attach(fe, { Context: o.Draft + "." + (fd.Name || f.Fields.length), Notice: c.Field || (c.E instanceof Element ? c.E : fe) });
                    if (h)
                        fd.DraftH = h;
                }
                //  An edit is a value the user changed: an event of this field (not one sent by code: a draft restore, a script) after
                //  which its value differs from its base; a plain click or key that changed nothing is no edit (A2-15)
                const recheck = ev => {
                    if (checked || fd.Error)
                        check(fd, f.Values());
                    gate();
                    if (ev && ev.isTrusted === false && !(ev.type === "sw-change")) {
                        //  (a value put in by code is the new base)
                        if (!edited.has(fd))
                            base.set(fd, snap(valueOf(c)));
                    }
                    else if (base.get(fd) !== snap(valueOf(c))) {
                        edited.add(fd);
                        //  (when the user edited it last: a page's own SetDirty of the same edit gives a developer warning)
                        Form.DirtyAt = Date.now();
                    }
                    track();
                };
                //  The base is the value before the user's first edit: values loaded by code after the form was made are no edits
                const rebase = () => {
                    if (!edited.has(fd))
                        base.set(fd, snap(valueOf(c)));
                };
                if (fe) {
                    fe.addEventListener("focusin", rebase);
                    fe.addEventListener("pointerdown", rebase, true);
                    fe.addEventListener("input", recheck);
                    fe.addEventListener("change", recheck);
                    //  components without native events (Select, Segmented, Toggle): a click / key in them, or their sw-change
                    fe.addEventListener("click", ev => setTimeout(() => recheck(ev), 0));
                    fe.addEventListener("keyup", ev => setTimeout(() => recheck(ev), 0));
                }
                if (c.E instanceof Element && c.E !== fe)
                    c.E.addEventListener("sw-change", ev => setTimeout(() => recheck(ev), 0));
                gate();
                return f;
            },
            /** @returns {object} The values by field name (skipped fields included). */
            Values() {
                const v = {};
                f.Fields.forEach(fd => v[fd.Name || f.Fields.indexOf(fd)] = valueOf(fd.Control));
                return v;
            },
            /**
             * Show (or clear) the message of a field.
             * @param {string} name The field name.
             * @param {?string} msg The message, null clears it.
             */
            SetError(name, msg) {
                const fd = f.Field(name);
                if (fd)
                    show(fd, msg || null);
            },
            /** Clear all messages. */
            Clear() {
                f.Fields.forEach(fd => show(fd, null));
                checked = false;
            },
            /**
             * Check every field and show the messages.
             * @param {boolean} [focus] Focus the first bad field (default true) and scroll it into view.
             * @returns {{Ok: boolean, Values: object, Errors: object}} The result.
             */
            Validate(focus) {
                checked = true;
                const values = f.Values();
                const errors = {};
                let first = null;
                for (const fd of f.Fields) {
                    const msg = check(fd, values);
                    if (msg) {
                        errors[fd.Name || f.Fields.indexOf(fd)] = msg;
                        first = first || fd;
                    }
                }
                if (first && focus !== false) {
                    const fe = focusOf(first.Control);
                    if (fe) {
                        fe.focus({ preventScroll: true });
                        fe.scrollIntoView({ block: "nearest" });
                    }
                }
                return { Ok: !first, Values: values, Errors: errors };
            },
            /**
             * Validate, then run OnSubmit with the values.
             * @returns {Promise<boolean>} True when it was submitted.
             */
            async Submit() {
                const r = f.Validate(true);
                if (!r.Ok || !o.OnSubmit)
                    return r.Ok;
                setStatus(null);
                //  While the save runs the page is not marked (a save that navigates when done is not asked about); a failure marks it again
                saving = true;
                track();
                //  The one result path (Sw.Work.Outcome): a throw, a rejection, a failed request OnSubmit started without returning it, or
                //  a false result is "not saved"; shown once, in the form's status line (FRM-3, FBK-5)
                const W = Sw.Work;
                const said = W && W.Watch ? W.Watch() : null;
                let out;
                if (W && W.Scope) {
                    const sc = W.Scope(() => o.OnSubmit(r.Values));
                    let res;
                    if (sc.Threw)
                        res = { Failed: true, Error: sc.Error };
                    else {
                        try {
                            res = { Value: await (W.Promise(sc) || sc.Result) };
                        }
                        catch (x) {
                            res = { Failed: true, Error: x };
                        }
                    }
                    out = W.Outcome(sc, res);
                }
                else {
                    try {
                        const v = await o.OnSubmit(r.Values);
                        out = { Ok: v !== false, NotDone: v === false };
                    }
                    catch (x) {
                        out = { Ok: false, Error: x, Aborted: !!(x && x.name === "AbortError") };
                    }
                }
                const saidIt = said ? said.Stop() : false;
                saving = false;
                if (!out.Ok) {
                    track();
                    if (out.Aborted)
                        return false;
                    const x = out.Error;
                    //  A lost connection (FBK-9): the values and the dirty state stay, a clear line at the form says so, and the save is
                    //  sent again when the connection is back (Resend: true, for saves that are safe to repeat) or offered as Try again
                    if (x && Sw.Net && Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(x)) {
                        console.warn(x);
                        offline(r.Values);
                        return false;
                    }
                    if (x) {
                        console.error(x);
                        if (!saidIt) {
                            //  One composer (Admin N13, G31): "Not saved." + the server's reason (the error object: its status and
                            //  X-Sw-Reason) + one piece of advice, never a second one appended here
                            const m = x && x.message ? x.message : String(x);
                            const what = _TF("Not saved.", "Start of the status line at a form whose save failed; the reason follows");
                            setStatus(Sw.Net && typeof Sw.Net.Message === "function" ? String(Sw.Net.Message(what, typeof x === "object" ? x : m))
                                : (Sw.Net && Sw.Net.ErrorText ? Sw.Net.ErrorText(m).Text : m), "error");
                        }
                        return false;
                    }
                    //  false without a word: say it at the form (never silent), and tell the developer
                    if (!saidIt) {
                        Sw.DevWarn && Sw.DevWarn("Sw.Form: OnSubmit returned false and showed nothing; say why (SetError(name, message) on the field, a toast) or throw an Error with the reason (UX guide FBK-5). The form now says \"Not saved.\"", "form-false-silent:" + formId);
                        setStatus(_TF("Not saved.", "Status line at a form whose save was refused without a reason"), "error");
                    }
                    return false;
                }
                //  Saved: the edited controls around the form that are not its fields are clean too (SAF-10)
                const sbE = o.Submit ? (o.Submit instanceof Element ? o.Submit : o.Submit.E) : null;
                if (sbE && Sw.Edits && Sw.Edits.Saved)
                    Sw.Edits.Saved(sbE);
                f.MarkClean();
                f.Fields.forEach(fd => {
                    if (fd.DraftH)
                        fd.DraftH.Clear();
                    if (fd.Control && typeof fd.Control.ClearDraft === "function")
                        fd.Control.ClearDraft();
                });
                //  (and any field drafted by hand inside the form: cleared only now, after the save succeeded; FD R4 F6)
                if (Sw.Draft && typeof Sw.Draft.Sent === "function" && f.E instanceof Element)
                    Promise.resolve(Sw.Draft.Sent(f.E)).catch(() => { });
                return true;
            },
        };
        /**
         * A copy of a value to compare later (arrays / objects by content).
         * @param {*} v The value.
         * @returns {string} The copy.
         */
        function snap(v) {
            try {
                return JSON.stringify(v === undefined ? null : v);
            }
            catch {
                return String(v);
            }
        }
        /**
         * True when a field's text is kept as a draft (the form's Draft, the TextBox Draft option, or Sw.Draft.Attach by hand).
         * @param {object} fd The field.
         * @returns {boolean} True when drafted.
         */
        function drafted(fd) {
            const c = fd.Control;
            const fe = focusOf(c);
            const act = h => !!h && h.Active !== false;
            return act(fd.DraftH) || act(c && c.DraftH) || act(fe && fe.SwDraft);
        }
        /** Compare the fields with their base values and mark the page (Sw.Page.SetDirty; not inside a dialog: the dialog asks, POP-6). */
        function track() {
            //  A field the user did not edit holds what code put in it (a load after the form was made): that is its base (A2-15)
            for (const fd of f.Fields)
                if (!edited.has(fd))
                    base.set(fd, snap(valueOf(fd.Control)));
            dirty = f.Fields.some(fd => edited.has(fd) && base.get(fd) !== snap(valueOf(fd.Control)));
            //  Opt in (Dirty: true or the leave text): the original pages never asked "Leave site?" for a form (P3-A-4)
            if (!(o.Dirty === true || typeof o.Dirty === "string") || !Sw.Page || !Sw.Page.SetDirty)
                return;
            //  A drafted field keeps its text (Sw.Draft: restored on the next visit): leaving never says it is lost (FD N8 / F15); only
            //  the other edited fields count for the leave question
            const lost = f.Fields.some(fd => edited.has(fd) && base.get(fd) !== snap(valueOf(fd.Control)) && !drafted(fd));
            if (saving) {
                Sw.Page.ClearDirty(formId);
                return;
            }
            const sb0 = o.Submit ? (o.Submit instanceof Element ? o.Submit : o.Submit.E) : null;
            const anchor = sb0 || (f.Fields[0] && focusOf(f.Fields[0].Control));
            if (anchor && anchor.closest && anchor.closest("[aria-modal='true'], .sw-dialog"))
                return;
            //  The form's name: its Title, else the title of the card / section it is in (A2-8: never just "the form" when it has one)
            let title = o.Title;
            if (!title && anchor && anchor.closest) {
                const host = anchor.closest(".sw-card, .sw-group, section, fieldset, form");
                const te = host && host.querySelector(".sw-card-title, .sw-group-title, h1, h2, h3, legend");
                title = (host && host.getAttribute("aria-label")) || (te && te.textContent.trim()) || "";
            }
            Sw.Page.SetDirty(formId, lost ? (typeof o.Dirty === "string" ? o.Dirty : (title
                ? _T("Your changes to {0} are lost.", title, "Text of the question 'Leave without saving?'. {0} names what was edited, ex: a card title 'Mail settings'")
                : _TF("Your changes in the form are lost.", "Text of the question 'Leave without saving?' for a form without a name"))) : null);
        }
        /**
         * Show (or clear) the form's status line (a failed save), before the submit button's row.
         * @param {?string} text The text.
         * @param {string} [kind] "error" (default) or "info".
         * @param {?object} [action] A button {Text, OnClick}.
         */
        function setStatus(text, kind, action) {
            if (!text) {
                if (status)
                    status.hidden = true;
                return;
            }
            const sbE = o.Submit ? (o.Submit instanceof Element ? o.Submit : o.Submit.E) : null;
            if (!status) {
                status = el("div.sw-form-status", { attrs: { role: "alert" } });
                //  LAY-9: once shown, the line keeps its place (a new save clears it without moving the buttons)
                Sw.Reserve.Line(status, { Lines: 0 });
                const row = sbE ? (sbE.closest(".sw-row, .sw-form-foot, .sw-card-foot, footer") || sbE) : null;
                if (row && row.parentNode)
                    row.parentNode.insertBefore(status, row);
                else if (f.Fields.length) {
                    const last = f.Fields[f.Fields.length - 1].Control;
                    const le = last && last.E instanceof Element ? last.E : focusOf(last);
                    if (le)
                        le.after(status);
                }
            }
            status.className = "sw-form-status sw-reserve sw-" + (kind || "error");
            status.replaceChildren(el("span.sw-form-status-text", { text }));
            if (action && Sw.Button)
                status.append(new Sw.Button({ Text: action.Text, Small: true, Saves: false, OnClick: action.OnClick }).E);
            status.hidden = false;
        }
        /**
         * The save failed because the server could not be reached: keep everything, say so, resend or offer Try again when it is back.
         * @param {object} values The values that were sent.
         */
        function offline(values) {
            const again = {
                Text: _TF("Try again", "Button next to 'Not saved: the connection was lost' at a form: sends the form again"),
                OnClick: () => f.Submit(),
            };
            const resend = o.Resend === true || o.Resend === "online";
            setStatus(resend
                ? _TF("Not saved: the connection was lost. It is sent again when the connection is back.", "Status line at a form whose save failed because the server could not be reached (it is sent again automatically)")
                : _TF("Not saved: the connection was lost. Your entries are kept.", "Status line at a form whose save failed because the server could not be reached"), "error", again);
            if (offOnline)
                offOnline();
            offOnline = null;
            if (!Sw.Events || !Sw.Events.On)
                return;
            offOnline = Sw.Events.On("net.online", () => {
                if (offOnline)
                    offOnline();
                offOnline = null;
                if (!status || status.hidden || !status.isConnected)
                    return;
                if (resend && snap(f.Values()) === snap(values)) {
                    f.Submit();
                    return;
                }
                setStatus(_TF("Not saved yet: the connection is back. Use Try again to save.", "Status line at a form whose save failed while offline, after the connection came back"), "info", again);
                if (Sw.Page && Sw.Page.Announce)
                    Sw.Page.Announce(_TF("The connection is back: the form was not saved yet.", "Announced when the connection came back and a form still has to be saved"));
            });
        }
        /**
         * The label of a field (for the default Required message).
         * @param {object} fd The field.
         * @returns {string} Label.
         */
        const labelOf = fd => {
            if (fd.Label)
                return fd.Label;
            const c = fd.Control;
            const t = (c && c.O && (c.O.Label || c.O.AriaLabel)) || (c && c.LabelE && c.LabelE.textContent);
            if (t)
                return t;
            const fe = focusOf(c);
            return (fe && (fe.getAttribute("aria-label") || (fe.labels && fe.labels[0] && fe.labels[0].textContent))) || fd.Name || "";
        };
        /**
         * Check one field and show its message.
         * @param {object} fd The field.
         * @param {object} values All values.
         * @returns {?string} The message (a control that showed its own message gives "!").
         */
        const check = (fd, values) => {
            if (fd.When && !fd.When(values)) {
                show(fd, null);
                return null;
            }
            const c = fd.Control;
            const v = valueOf(c);
            let msg = null;
            if (fd.Required && isEmpty(v))
                msg = typeof fd.Required === "string" ? fd.Required : _T("{0} is required.", labelOf(fd), "Message under an empty required form field.{0} is replaced with the name of the field");
            if (!msg && fd.Validate)
                msg = fd.Validate(v, values) || null;
            if (!msg && c && typeof c.Validate === "function" && !(c instanceof Element)) {
                const r = c.Validate();
                if (typeof r === "string" && r)
                    msg = r;
                else if (r === false)
                    return fd.Error = "!";
            }
            else if (!msg && c && typeof c.Commit === "function" && c.O && c.O.Validate) {
                if (c.Commit() === false)
                    return fd.Error = c.Error || "!";
            }
            show(fd, msg);
            return msg;
        };
        /**
         * Show a message on a field: the control's SetError, else a shared error line after the control's field.
         * @param {object} fd The field.
         * @param {?string} msg The message.
         */
        const show = (fd, msg) => {
            fd.Error = msg;
            const c = fd.Control;
            if (c && typeof c.SetError === "function" && !(c instanceof Element)) {
                c.SetError(msg);
                return;
            }
            const fe = focusOf(c);
            if (!fe)
                return;
            if (!fd.ErrorE) {
                fd.ErrorE = el("div.sw-text-error.sw-form-error", { id: "sw-fe" + (++pageUid), attrs: { "aria-live": "polite" } });
                const host = (c && c.E instanceof Element ? c.E : fe);
                const field = host.closest(".sw-field");
                //  inside the field (under the control and its hint, the field gap), else right after the control
                if (field)
                    field.append(fd.ErrorE);
                else
                    host.after(fd.ErrorE);
            }
            //  LAY-9: a shown message keeps its line (cleared while typing, nothing below moves)
            if (msg)
                Sw.Reserve.Line(fd.ErrorE);
            fd.ErrorE.textContent = msg || "";
            fd.ErrorE.hidden = !msg;
            const desc = (fe.getAttribute("aria-describedby") || "").split(" ").filter(x => x && x !== fd.ErrorE.id);
            if (msg) {
                fe.setAttribute("aria-invalid", "true");
                desc.push(fd.ErrorE.id);
            }
            else
                fe.removeAttribute("aria-invalid");
            if (desc.length)
                fe.setAttribute("aria-describedby", desc.join(" "));
            else
                fe.removeAttribute("aria-describedby");
            const fieldE = fe.closest(".sw-field");
            if (fieldE)
                fieldE.classList.toggle("sw-invalid", !!msg);
        };
        const sb = o.Submit ? (o.Submit instanceof Element ? o.Submit : o.Submit.E) : null;
        /**
         * Gate "required" (opt in): the submit button is disabled (aria-disabled, UX guide INP-12) while a required field is empty; its
         * reason names the empty fields, so an attempt says what is missing (FRM-7: the default keeps the button enabled).
         */
        const gate = () => {
            if (o.Gate !== "required" || !sb)
                return;
            const values = f.Values();
            const empty = f.Fields.filter(fd => fd.Required && !(fd.When && !fd.When(values)) && isEmpty(valueOf(fd.Control)));
            const missing = empty.length > 0;
            const why = missing ? _T("Fill in {0} first", empty.map(labelOf).filter(x => x).join(", "), "Reason (tool tip) of a disabled submit button of a form while required fields are empty, {0} is replaced with the labels of the empty fields") : undefined;
            if (o.Submit.SetDisabled)
                o.Submit.SetDisabled(missing, why);
            else if (Sw.Disabled)
                Sw.Disabled.Set(sb, missing, why);
            else
                sb.disabled = missing;
        };
        f.Gate = gate;
        (o.Fields || []).forEach(fd => f.Add(fd));
        if (sb && o.Submit.O && !o.Submit.O.OnClick && typeof o.Submit.Click === "function") {
            //  A Sw.Button: its OnClick submits, so it is busy and absorbs repeats (FBK-2: one save per click); its own "save" rule is off
            //  (the form marks its fields clean only when OnSubmit succeeded)
            o.Submit.O.OnClick = () => f.Submit();
            o.Submit.O.Saves = false;
            if (sb.type === "submit")
                sb.addEventListener("click", ev => ev.preventDefault());
        }
        else if (sb && !(o.Submit.O && o.Submit.O.OnClick)) {
            let running = false;
            sb.addEventListener("click", ev => {
                ev.preventDefault();
                if (running)
                    return;
                running = true;
                sb.setAttribute("aria-busy", "true");
                f.Submit().finally(() => {
                    running = false;
                    sb.removeAttribute("aria-busy");
                });
            });
        }
        return f;
    }

    /**
     * Show the required marker after a control's label when it has none (the form marks the field required, FRM-2).
     * @param {*} c The control.
     * @param {?Element} fe Its focusable element.
     */
    function addRequiredMark(c, fe) {
        let lab = c && c.LabelE instanceof Element ? c.LabelE : null;
        if (!lab && fe && fe.labels && fe.labels[0])
            lab = fe.labels[0];
        if (!lab) {
            const fl = fe && fe.closest && fe.closest(".sw-field");
            lab = fl ? fl.querySelector(".sw-field-label") : null;
        }
        if (!lab || lab.querySelector(".sw-field-req"))
            return;
        lab.append(el("span.sw-field-req", { text: "*", title: _TF("Required", "Tool tip of the asterisk after the label of a form field that must be filled in"), attrs: { "aria-hidden": "true" } }));
    }

    /**
     * A step by step flow (a wizard; ui/README.md "Wizards: Sw.Steps"): the step list (numbered, done steps with a check, the
     * current one with aria-current="step"; on narrow containers "Step 2 of 4" and a progress line), the step title as a heading,
     * the step content and a footer with Cancel (left), Back and Next / the finish button (right, the one primary button).
     *  - Next validates the step first: its Form (Sw.Form: the messages show, the first bad field gets the focus) and / or its
     *    Validate() (false or a message = stay; a promise keeps the button busy).
     *  - Moving focuses the new step's heading (screen readers hear "Step 2 of 4: Plan"); Enter in a single line field = Next.
     *  - Done steps can be clicked in the step list to go back; later steps open only after the steps before them are valid.
     * Callable with or without new. Options: Steps [{Title, Sub (one line), Content (element / component, or a function (steps) that
     * builds it once), Form, Validate(steps) -> true / false / message / promise, OnShow(steps)}], Label (accessible name), FinishText
     * (default "Finish"), OnFinish(steps) (may return a promise; true / undefined = done), OnCancel(steps) (no Cancel button without it),
     * NextText, BackText, Class, Start (index).
     * Members: E, Index, Count, Go(index) (only to done steps or the next one), Next(), Back(), SetBusy(bool), Body (the content host),
     * Step(i) (the step object, with its built E).
     * @param {object} [o] Options.
     * @returns {object} The steps.
     */
    function Steps(o) {
        o = o || {};
        checkOpts("Steps", o);
        checkEach("Steps step", o.Steps);
        const steps = (o.Steps || []).filter(x => x);
        const n = steps.length;
        const id = "sw-steps" + (++pageUid);
        const list = el("ol.sw-steps-list", { attrs: { "aria-label": _TF("Steps", "Accessible name of the list of steps of a wizard") } });
        const progress = el("div.sw-steps-progress", { attrs: { "aria-hidden": "true" } }, el("span"));
        const count = el("div.sw-steps-count");
        const title = el("h2.sw-steps-title", { id: id + "-t", tabIndex: -1 });
        const sub = el("p.sw-steps-sub");
        const body = el("div.sw-steps-body");
        //  a step level message (Validate returned a text): inline above the footer, not a toast (one error surface)
        const err = el("div.sw-text-error.sw-steps-error", { attrs: { role: "alert" } });
        err.hidden = true;
        const cancel = o.OnCancel ? new Sw.Button({ Text: _TF("Cancel", "Button of a wizard that leaves it without saving"), Kind: "ghost", OnClick: () => o.OnCancel(s) }) : null;
        const back = new Sw.Button({ Text: o.BackText || _TF("Back", "Button of a wizard that shows the step before"), Icon: "left", OnClick: () => s.Back() });
        const next = new Sw.Button({ Text: "", Kind: "primary", OnClick: () => s.Next() });
        const foot = el("div.sw-steps-foot", null, cancel ? cancel.E : null, el("span.sw-steps-grow"), back.E, next.E);
        const head = el("div.sw-steps-head", null, list, count, progress);
        const e = el("section.sw-steps", { class: o.Class || null, attrs: { "aria-labelledby": o.Label ? null : id + "-t", "aria-label": o.Label || null } }, head, el("div.sw-steps-main", null, title, sub, body, err), foot);
        let reached = 0;
        let busy = false;
        const s = {
            E: e, Body: body, Index: -1, Count: n,
            /** @param {number} i Index. @returns {object} The step. */
            Step: i => steps[i],
            /**
             * Show a step (only a step that was reached before, or the next one).
             * @param {number} i Index.
             * @param {boolean} [focus] Focus the heading (default true).
             */
            Go(i, focus) {
                if (i < 0 || i >= n || i > reached)
                    return;
                const st = steps[i];
                if (!st.E) {
                    let c = typeof st.Content === "function" ? st.Content(s) : st.Content;
                    st.E = el("div.sw-steps-pane", null, ...nodesOf([c]));
                }
                s.Index = i;
                body.replaceChildren(st.E);
                err.hidden = true;
                err.textContent = "";
                title.textContent = st.Title || "";
                sub.textContent = st.Sub || "";
                sub.hidden = !st.Sub;
                count.textContent = _T("Step {0} of {1}", i + 1, n, "Position in a wizard (phones, and the accessible name of the step heading).{0} is the step number.{1} is the number of steps");
                title.setAttribute("aria-describedby", count.id = id + "-c");
                progress.firstElementChild.style.width = ((i + 1) / n * 100) + "%";
                paintList();
                back.E.hidden = i === 0;
                const last = i === n - 1;
                next.SetText(last ? (o.FinishText || _TF("Finish", "Last button of a wizard")) : (o.NextText || _TF("Next", "Button of a wizard that goes to the next step")));
                if (focus !== false)
                    title.focus({ preventScroll: false });
                st.OnShow && st.OnShow(s);
            },
            /** Validate the step, then show the next one (or finish). @returns {Promise<boolean>} Moved / finished. */
            async Next() {
                if (busy)
                    return false;
                const st = steps[s.Index];
                if (!(await valid(st)))
                    return false;
                if (s.Index < n - 1) {
                    reached = Math.max(reached, s.Index + 1);
                    s.Go(s.Index + 1);
                    return true;
                }
                if (!o.OnFinish)
                    return true;
                s.SetBusy(true);
                try {
                    const r = await o.OnFinish(s);
                    return r !== false;
                }
                finally {
                    s.SetBusy(false);
                }
            },
            /** Show the step before. */
            Back() {
                if (!busy && s.Index > 0)
                    s.Go(s.Index - 1);
            },
            /**
             * Busy (a running Validate / OnFinish): the buttons are disabled, the primary one shows its spinner.
             * @param {boolean} on Busy.
             */
            SetBusy(on) {
                busy = !!on;
                next.SetWorking && next.SetWorking(busy);
                const why = busy ? _TF("Wait until the step is done", "Reason (tool tip) of the Back / Cancel buttons of a wizard while its step is checked or finished") : undefined;
                back.SetDisabled(busy, why);
                if (cancel)
                    cancel.SetDisabled(busy, why);
            },
        };
        /**
         * Validate a step (its Form, then its Validate).
         * @param {object} st The step.
         * @returns {Promise<boolean>} Valid.
         */
        const valid = async st => {
            if (st.Form && !st.Form.Validate(true).Ok)
                return false;
            if (!st.Validate)
                return true;
            s.SetBusy(true);
            try {
                const r = await st.Validate(s);
                if (typeof r === "string" && r) {
                    err.textContent = r;
                    err.hidden = false;
                    return false;
                }
                return r !== false;
            }
            finally {
                s.SetBusy(false);
            }
        };
        /** The step list: done (a check, a button back), current (aria-current), later (muted; reachable ones are buttons). */
        const paintList = () => {
            list.replaceChildren(...steps.map((st, i) => {
                const state = i < s.Index ? "done" : (i === s.Index ? "current" : "todo");
                const mark = el("span.sw-steps-mark", { attrs: { "aria-hidden": "true" } }, state === "done" && Sw.Icon ? Sw.Icon("check") : String(i + 1));
                const label = el("span.sw-steps-label", { text: st.Title || "" });
                const status = el("span.sw-sr-only", { text: state === "done" ? " " + _TF("(done)", "Added to a finished step in the step list of a wizard, for screen readers") : "" });
                const canGo = i !== s.Index && i <= reached;
                const inner = canGo ? el("button.sw-steps-link", { type: "button" }, mark, label, status) : el("span.sw-steps-link", null, mark, label, status);
                if (canGo)
                    inner.addEventListener("click", () => {
                        if (!busy)
                            s.Go(i);
                    });
                return el("li.sw-steps-item", { class: "sw-steps-" + state, attrs: { "aria-current": state === "current" ? "step" : null } }, inner);
            }));
        };
        //  Enter in a single line field moves on (not in text areas, buttons, selects or menus)
        body.addEventListener("keydown", ev => {
            if (ev.key === "Enter" && !ev.shiftKey && !ev.ctrlKey && !ev.altKey && !ev.metaKey && ev.target instanceof HTMLInputElement && !/^(checkbox|radio|button|submit)$/.test(ev.target.type) && !ev.defaultPrevented) {
                ev.preventDefault();
                //  commit the field first (TextBox commits on Enter too)
                setTimeout(() => s.Next(), 0);
            }
        });
        reached = Math.max(0, Math.min(n - 1, o.Start || 0));
        if (n)
            s.Go(reached, false);
        return s;
    }

    const add = { StateView, Meter, MenuTitle, Card, Facts, Kpi, ListCard, Grid, SectionTitle, PageLayout, ReportUpload, UploadTarget, UploadButton, FillTable, SelectList, ListDetail, Form, Steps };
    //  ui/layout.js owns Sw.PageHeader (Design v2 WP-A); this one is only the same API until it is there
    if (typeof Sw.PageHeader !== "function")
        add.PageHeader = PageHeader;
    //  A component is not text (FD R4 F16: `area.append(Sw.Card({...}))` showed "[object Object]"): the objects these factories return
    //  get a toString that gives "" and a developer warning to append x.E (Sw.el and the Sw containers take components)
    for (const [name, f] of Object.entries(add)) {
        if (typeof f !== "function" || name === "ReportUpload" || name === "UploadTarget" || name === "FillTable")
            continue;
        //  (a Proxy: the factory's own properties, ex: Sw.Form.DirtyAt, stay the same object's)
        add[name] = new Proxy(f, {
            apply(target, self, args) {
                const r = Reflect.apply(target, self, args);
                if (r && typeof r === "object" && r.E instanceof Element && !Object.prototype.hasOwnProperty.call(r, "toString"))
                    Object.defineProperty(r, "toString", {
                        value: () => {
                            Sw.DevWarn("A Sw." + name + " was turned into text (native append / innerHTML / a template string): it shows nothing. Append its element (x.E), or pass the component to Sw.el / a Sw container (they take components).", "comp-text:" + name);
                            return "";
                        },
                        configurable: true, writable: true,
                    });
                return r;
            },
        });
    }
    Object.assign(Sw, add);
})();
