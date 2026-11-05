/**
 * @file ui/gallery.js - the component gallery of the SysWeaver UI library (Sw): every component in every state.
 *
 * Used for manual and automated testing (Playwright) and by the review ("critic") rounds. Elements that tests use have a data-g attribute.
 * The demo texts are intentionally not translated (SwWeb:no-tr-file: a developer page, the body has translate="no").
 * Loaded last by ui/gallery.html (after the core and ui files or bundles).
 */
(() => {
    "use strict";

    const { el } = Sw;

    /** Demo country names (for long, searchable lists). */
    const Countries = ["Argentina", "Australia", "Austria", "Belgium", "Brazil", "Canada", "Chile", "China", "Colombia", "Croatia", "Czechia", "Denmark",
        "Egypt", "Estonia", "Finland", "France", "Germany", "Greece", "Hungary", "Iceland", "India", "Indonesia", "Ireland", "Israel", "Italy", "Japan",
        "Kenya", "Latvia", "Lithuania", "Luxembourg", "Mexico", "Morocco", "Netherlands", "New Zealand", "Nigeria", "Norway", "Peru", "Poland",
        "Portugal", "Romania", "Singapore", "Slovakia", "Slovenia", "South Africa", "Spain", "Sweden", "Switzerland", "Thailand", "Turkey",
        "Ukraine", "United Kingdom", "United States", "Uruguay", "Vietnam"];

    /** The ui/README.md of the library (the README sections are linked from every gallery card). */
    const ReadMe = "https://github.com/dosh1974/SysWeaver/blob/main/SysWeaver.HttpServer/srcWeb/ui/README.md";

    /** The developer quick start guide (a first page in 5 minutes). */
    const QuickStart = "https://github.com/dosh1974/SysWeaver/blob/main/_docs/WebQuickStart.md";

    /**
     * Per section: the API names, the README section (anchor) and a minimal copyable snippet.
     * @type {Object<string, {Api: string[], Doc: string, Code: string}>}
     */
    const Docs = {
        buttons: {
            Api: ["Sw.Button", "Sw.ButtonRow"], Doc: "#button",
            Code: 'const save = new Sw.Button({ Text: "Save", Icon: "save", Kind: "primary",\n' +
                '    OnClick: async () => { await Sw.Net.Send("Api/my/Save", data); Sw.Info("Saved"); } });\n' +
                'const cancel = new Sw.Button({ Text: "Cancel", OnClick: () => history.back() });\n' +
                "document.body.append(Sw.ButtonRow(cancel, save));\n" +
                "// OnClick returning a promise shows a busy state, a rejection shows a fail toast",
        },
        uxcontrols: {
            Api: ["Sw.Disabled", "Sw.ModifierClick", "Sw.MenuButton", "Sw.CopyButton", "Sw.Clipboard.CopyOnClick"], Doc: "#disabled-busy-and-copy-ux-guide",
            Code: '// Disabled with a reason: stays focusable, a click / Enter shows and announces why (INP-12)\n' +
                'const del = new Sw.Button({ Text: "Delete", Kind: "danger", Disabled: "Select a row first" });\n' +
                'del.SetDisabled(false);                       // enabled (SetDisabled(true, "reason") again)\n' +
                '// A menu button: click, right click, long press, Menu key, Shift+F10 (INP-3)\n' +
                'new Sw.Button({ Text: "More", Icon: "menu", Menu: () => [{ Text: "Rename", OnClick: rename }] });\n' +
                '// Copy: confirms in place, no toast (INP-31)\n' +
                'Sw.CopyButton({ Value: key, What: "API key", Generic: true, Label: "Copy the API key of MailService" });',
        },
        inputs: {
            Api: ["Sw.TextBox", "Sw.NumberBox"], Doc: "#textbox",
            Code: 'const name = new Sw.TextBox({ Label: "Name", Clear: true, OnChange: v => console.log(v) });\n' +
                'const age = new Sw.NumberBox({ Label: "Age", Min: 0, Max: 150, Step: 1, Value: 42 });\n' +
                "form.append(name.E, age.E);\n" +
                "name.Get();          // the value\n" +
                "age.Set(43, true);   // true = silent (no callbacks)",
        },
        toggles: {
            Api: ["Sw.Toggle", "Sw.Chip", "Sw.Badge"], Doc: "#toggle-switch",
            Code: 'const on = new Sw.Toggle({ Label: "Enabled", Value: true, OnChange: v => console.log(v) });\n' +
                'const chip = new Sw.Chip({ Text: "Filter", Icon: "filter", Pressable: true, OnChange: p => console.log(p) });\n' +
                'const badge = Sw.Badge("Error", "error");   // an element (ok, error, warning, accent, accent2)\n' +
                "box.append(on.E, chip.E, badge);",
        },
        sliders: {
            Api: ["Sw.Slider"], Doc: "#slider",
            Code: "const s = new Sw.Slider({ Min: 0, Max: 1, Value: 0.5, Default: 0.5,\n" +
                "    OnInput: v => preview(v),   // while dragging\n" +
                "    OnChange: v => save(v) });  // when released\n" +
                "box.append(s.E);",
        },
        selects: {
            Api: ["Sw.Select", "Sw.ColorPicker"], Doc: "#select",
            Code: 'const sel = new Sw.Select({ Options: [{ Value: 1, Label: "One" }, { Value: 2, Label: "Two", Icon: "star" }],\n' +
                "    Value: 1, OnChange: v => console.log(v) });   // long lists get a filter box\n" +
                'const color = new Sw.ColorPicker({ Value: "#4fc9e0", Hex: true, OnChange: c => console.log(c) });\n' +
                "box.append(sel.E, color.E);",
        },
        progress: {
            Api: ["Sw.ProgressBar", "Sw.Spinner"], Doc: "#progressbar-spinner",
            Code: 'const bar = new Sw.ProgressBar({ Label: "Upload" });\n' +
                'bar.SetValue(3, 10, "{1} of {2} files ({0})");\n' +
                "box.append(bar.E, new Sw.Spinner({ Size: 32 }).E);",
        },
        menus: {
            Api: ["Sw.Menu", "Sw.Popup", "Sw.Hint"], Doc: "#menu",
            Code: "const items = [\n" +
                '    { Text: "Open", Icon: "open", OnClick: () => open() },\n' +
                '    { Text: "Show grid", Checked: true, Keep: true },\n' +
                '    "-",\n' +
                '    { Text: "Export", Items: [{ Text: "CSV" }, { Text: "JSON" }] },\n' +
                "];\n" +
                "new Sw.Button({ Text: \"Menu\", Menu: items }); // a menu button: click, right click, long press\n" +
                "Sw.Menu.Show(button.E, items);              // or null = centered\n" +
                "Sw.Menu.Context(element, () => items);      // long press, right click, menu key",
        },
        dialogs: {
            Api: ["Sw.Dialog", "Sw.SearchSelection"], Doc: "#dialog",
            Code: 'if (await Sw.Dialog.Confirm("Delete the file?", "This can\'t be undone.", "Delete", { Danger: true }))\n' +
                "    await remove();\n" +
                'const name = await Sw.Dialog.Prompt("Rename", "report.md", "File name");   // null = cancelled\n' +
                'const v = await Sw.Dialog.Show({ Title: "Choose", Content: "Any text or node",\n' +
                '    Buttons: [{ Text: "Later", Value: 1 }, { Text: "Now", Kind: "primary", Value: 2 }] });',
        },
        overlays: {
            Api: ["Sw.Toast", "Sw.Info", "Sw.Fail", "Sw.Loader", "Sw.ServerBlock", "Sw.FullScreen"], Doc: "#toasts",
            Code: 'Sw.Info("Saved 3 files");\n' +
                'Sw.Fail("Failed to save");\n' +
                'Sw.Toast("Disk almost full", { Kind: "warning" });\n' +
                'const done = Sw.Loader.Show({ Target: box, Text: "Busy" });   // omit Target: the whole page\n' +
                "try { await work(); } finally { done(); }",
        },
        layout: {
            Api: ["Sw.Tabs", "Sw.Split", "Sw.Pager", "Sw.Columns"], Doc: "#tabs",
            Code: 'const tabs = new Sw.Tabs({ Label: "Views" });\n' +
                'tabs.Add({ Title: "Overview", Icon: "home", Content: overview });\n' +
                'tabs.Add({ Title: "Details", Content: details });\n' +
                'const split = new Sw.Split({ A: list, B: tabs.E, Size: 0.3, Key: "MyPage.Split" });\n' +
                "const cols = new Sw.Columns({ MinWidth: 320 });   // one column on phones\n" +
                "cols.Add(cardA, cardB, cardC);",
        },
        props: {
            Api: ["Sw.PropertyEditor"], Doc: "#propertyeditor",
            Code: "const ed = new Sw.PropertyEditor({\n" +
                "    Defs: [\n" +
                '        { Key: "Speed", Label: "Speed", Type: "float", Min: 0.05, Max: 8, Log: true, Default: 1 },\n' +
                '        { Key: "Loop", Label: "Loop", Type: "bool", Default: true },\n' +
                "    ],\n" +
                "    Values: { Speed: 2 },\n" +
                "    OnChange: (key, value) => console.log(key, value),\n" +
                "});\n" +
                "box.append(ed.E);",
        },
        images: {
            Api: ["Sw.CreateImg", "Sw.FadeSrc", "Sw.SetImageSource"], Doc: "#helpers-basejs",
            Code: "const img = Sw.CreateImg({ FadeIn: 400, ShowError: true });   // hidden until loaded, then faded in\n" +
                'img.src = "../icons/user.svg";\n' +
                "box.append(img);",
        },
        code: {
            Api: ["Sw.CodeEditor"], Doc: "#codeeditor-codejs",
            Code: 'await Sw.Net.IncludeJs(null, "../ui/code.js");   // not in sw/ui.js, loaded on demand\n' +
                'const ed = new Sw.CodeEditor({ Value: src, Language: "glsl", OnChange: e => save(e.Get()) });\n' +
                "box.append(ed.E);",
        },
        cells: {
            Api: ["Sw.Cells", "Sw.Markdown"], Doc: "#value-cells-cellsjs",
            Code: "// Render a typed value like a table cell (TableDataColumn.Format)\n" +
                'Sw.Cells.Update(td, "System.Int64", 123456789, false, "ByteSize");\n' +
                'Sw.Cells.Update(td2, "System.String", "https://example.com", false, "Url");\n' +
                'Sw.Markdown.Set(div, "Some **bold** text");   // sanitized',
        },
        brand: {
            Api: ["--sw-brand-icon", "--sw-brand-logo", ".sw-brand-mark", ".sw-weave", "--ThemeFontHead"], Doc: "#brand",
            Code: '<div class="sw-brand-logo" role="img" aria-label="App logo" style="width: 160px"></div>    <!-- the app logo.svg, 4:3 -->\n' +
                '<div class="sw-brand-mark" style="height: 64px"></div>                                  <!-- the app icon.svg, square -->\n' +
                '<body class="sw-weave"><main class="sw-weave-panel">...</main></body>               <!-- weave behind a solid panel -->\n' +
                "/* The server generates icon.svg / logo.svg per app; an app may serve its own, or point these at other files in app/app.css (write the urls as ../app/x.svg), see _docs/WebBranding.md: */\n" +
                ':root { --sw-brand-icon: url("../app/acme-mark.svg"); --sw-brand-logo: url("../app/acme-logo.svg"); --ThemeAcc1: #c2185b; }',
        },
        icons: {
            Api: ["Sw.Icon", "Sw.IconClass"], Doc: "#icon-classes-iconscss",
            Code: 'box.append(Sw.Icon("save"));                                  // inline svg (stroked, text color)\n' +
                'box.append(Sw.IconClass("IconReload", { Size: 32, Title: "Reload" }));   // a SysWeaver icon class\n' +
                'new Sw.Button({ Icon: "IconUser", Title: "User" });             // buttons take both',
        },
    };

    /**
     * The meta row of a section: API names, a link to the README section and a "Code" toggle with a copyable snippet.
     * @param {string} id Section id.
     * @returns {?HTMLElement[]} The meta row and the (hidden) code box, or null.
     */
    function sectionMeta(id) {
        const d = Docs[id];
        if (!d)
            return null;
        const pre = el("pre.g-code", { text: d.Code, hidden: true, dataset: { g: "code-" + id } });
        const copy = new Sw.Button({ Text: "Copy", Icon: "copy", Small: true, Kind: "ghost", Title: "Copy the snippet", OnClick: () => Sw.Clipboard.Copy(d.Code) });
        const box = el("div.g-codebox", { hidden: true }, pre, el("div.g-row.g-code-tools", null, copy.E));
        const toggle = new Sw.Button({
            Text: "Code", Small: true, Pressable: true, Title: "Show a minimal code snippet",
            OnClick: b => {
                box.hidden = !b.Pressed;
                pre.hidden = !b.Pressed;
            },
        });
        tag(toggle, "code-btn-" + id);
        const api = el("span.g-api", null, ...d.Api.map(a => el("code", { text: a })));
        const doc = el("a.g-doc", { text: "Docs", href: ReadMe + d.Doc, attrs: { target: "_blank", rel: "noopener", title: "Open the README section of " + d.Api[0] } });
        return [el("div.g-meta", null, api, el("span.g-meta-tools", null, doc, toggle.E)), box];
    }

    /**
     * Create a gallery section (a Sw.Group) with its API names, docs link and code snippet (see Docs).
     * @param {string} id Section id (data-g).
     * @param {string} title Title.
     * @param {...Node} content Content.
     * @returns {HTMLElement} The section.
     */
    function section(id, title, ...content) {
        const meta = sectionMeta(id);
        const g = new Sw.Group({ Title: title, Class: "g-section", Content: meta ? [...meta, ...content] : content });
        g.E.dataset.g = id;
        return g.E;
    }

    /**
     * A display name (first letter in upper case) of a theme name.
     * @param {string} n The theme name.
     * @returns {string} The display name.
     */
    const themeLabel = n => n ? n.charAt(0).toUpperCase() + n.slice(1) : n;

    /**
     * The intro below the header: what the library is, how to load it, links to the docs.
     * @returns {HTMLElement} The intro.
     */
    function intro() {
        const link = (text, href) => el("a", { text, href, attrs: { target: "_blank", rel: "noopener" } });
        return el("section.g-intro", { dataset: { g: "intro" } },
            el("p", null, "The ", el("b", { text: "Sw UI library" }),
                ": dependency free components for SysWeaver pages that work with touch, mouse and keyboard, on desktops and phones, in every theme. " +
                "Load ", el("code", { text: "sw/core.js" }), " + ", el("code", { text: "sw/ui.js" }), " (and ", el("code", { text: "common/theme.css" }), " + ",
                el("code", { text: "sw/ui.css" }), "), create a component with one options object and append its ", el("code", { text: ".E" }), " element."),
            el("p.sw-muted", null, "Every card names its API, shows a minimal snippet (Code, with Copy) and links to its README section (Docs). Start here: ",
                link("UI library README", ReadMe), " · ", link("Your first page in 5 minutes", QuickStart)));
    }

    /**
     * A labelled row of items.
     * @param {string} label Label.
     * @param {...(Node|{E: Node})} items Items.
     * @returns {HTMLElement} The row.
     */
    function row(label, ...items) {
        return el("div.g-row", null, label ? el("span.g-label", { text: label }) : null, ...items.map(x => x && x.E ? x.E : x));
    }

    /**
     * Tag an element (or component) for tests.
     * @param {HTMLElement|{E: HTMLElement}} x The element or component.
     * @param {string} id The data-g value.
     * @returns {*} x.
     */
    function tag(x, id) {
        (x.E || x).dataset.g = id;
        return x;
    }

    /** Short delay helper. @param {number} ms Milliseconds. @returns {Promise<void>} */
    const wait = ms => new Promise(r => setTimeout(r, ms));
    /** A wait that an AbortSignal stops (rejects with an AbortError), as a fetch with { signal } does. */
    const waitAbortable = (ms, signal) => new Promise((r, j) => {
        const t = setTimeout(r, ms);
        signal && signal.addEventListener("abort", () => {
            clearTimeout(t);
            j(new DOMException("Aborted", "AbortError"));
        });
    });

    // ------------------------------------------------------------------ header

    /**
     * The sticky header: title, theme select, layout toggles, keyboard help.
     * @returns {HTMLElement} The header.
     */
    function header() {
        Sw.Theme.Register("Club", { Scheme: "dark", Colors: { Background: "#101418", Foreground: "#e8eef4", Accent1: "#ff3fa4", Accent2: "#3fd6ff" } });
        const themes = Sw.Theme.List().map(n => ({ Value: n, Label: themeLabel(n) }));
        const sel = tag(new Sw.Select({ Options: themes, Value: Sw.Theme.Get(), Title: "Theme", Small: true, OnChange: v => Sw.Theme.Set(v) }), "theme");
        Sw.Theme.OnChange(() => sel.Set(Sw.Theme.Get(), true));
        const mob = tag(new Sw.Toggle({ Label: "Mobile", Value: document.documentElement.classList.contains("SwMobile"), OnChange: v => {
            const u = new URL(location.href);
            u.searchParams.set("mobile", v ? "true" : "false");
            location.href = u.href;
        } }), "mobile");
        const help = tag(new Sw.Button({ Icon: "help", Title: "Keyboard shortcuts (?)", Kind: "ghost", OnClick: showKeys }), "keys");
        const top = tag(new Sw.Button({ Icon: "up", Title: "Back to the top", Kind: "ghost", OnClick: () => window.scrollTo({ top: 0, behavior: "smooth" }) }), "top");
        return el("header.g-head", null, el("h1", { text: "SysWeaver UI gallery" }), sel.E, mob.E, help.E, top.E);
    }

    /** Show the registered keyboard shortcuts. */
    function showKeys() {
        const list = el("table.g-table", null, ...Sw.Keys.List().map(k => el("tr", null, el("td", null, el("kbd", { text: k.Combo })), el("td", { text: k.Title }))));
        Sw.Dialog.Show({ Title: "Keyboard shortcuts", Content: list });
    }

    // ------------------------------------------------------------------ sections

    /**
     * The app marks (the server generated icon.svg and logo.svg of this app), the 4 theme colours, the fonts and the weave;
     * a deployment may override the marks in app/app.css (see the Code snippet and _docs/WebBranding.md).
     * @returns {HTMLElement} Brand.
     */
    function brand() {
        const swatch = (name, v) => el("div.g-swatch", null, el("span.g-swatch-color", { style: { background: "var(" + v + ")" } }),
            el("span", { text: name }), el("code.sw-muted", { text: v }));
        const fonts = el("div.g-fonts", null,
            el("div.sw-head", { text: "Composed by configuration", style: { fontSize: "1.6em" } }),
            el("div", { text: "Barlow Regular: build services from small, annotated micro services." }),
            el("div", { text: "Barlow Medium: one JSON manifest decides what runs.", style: { fontWeight: "500" } }));
        const weave = el("div.sw-weave.g-weave", null, el("div.sw-weave-panel.g-weave-panel", null,
            el("div.sw-brand-logo", { attrs: { role: "img", "aria-label": "App logo" }, style: { width: "120px" } })));
        return section("brand", "Brand",
            el("div.sw-muted", { text: "Every app has its own icon (square) and logo (4:3), generated by the server from the app name and seed, in the app colors (the Application theme)." }),
            row("Marks", tag(el("div.sw-brand-mark", { attrs: { role: "img", "aria-label": "App icon" }, style: { height: "56px" } }), "brand-mark"),
                tag(el("div.sw-brand-logo", { attrs: { role: "img", "aria-label": "App logo" }, style: { width: "160px" } }), "brand-logo"),
                el("img", { src: "../icon.svg", alt: "App icon at 32 px", title: "The app icon at 32 px (the browser tab icon)", style: { width: "32px", height: "32px" } })),
            row("Colors", swatch("Background", "--ThemeBackground"), swatch("Main", "--ThemeMain"), swatch("Accent 1", "--ThemeAcc1"), swatch("Accent 2", "--ThemeAcc2")),
            //  The chart series slots in legend order (series 1 takes slot 1 ...; see chart/README.md), with the current theme's (and app/app.css) values
            tag(row("Chart slots", ...[1, 2, 3, 4, 5, 6, 7].map(i => swatch(String(i), "--sw-chart-" + i))), "chart-slots"),
            row("Fonts", fonts),
            row("Weave", weave));
    }

    /** @returns {HTMLElement} Buttons. */
    function buttons() {
        let count = 0;
        const counter = el("span.sw-badge", { text: "0" });
        return section("buttons", "Buttons",
            row("Kinds", tag(new Sw.Button({ Text: "Default" }), "btn-default"), tag(new Sw.Button({ Text: "Primary", Kind: "primary" }), "btn-primary"),
                new Sw.Button({ Text: "Accent", Kind: "accent" }), new Sw.Button({ Text: "Ghost", Kind: "ghost" }), new Sw.Button({ Text: "Danger", Kind: "danger" })),
            row("Icons", new Sw.Button({ Text: "Save", Icon: "save" }), new Sw.Button({ Text: "Reload", Icon: "IconReload", Kind: "primary" }),
                new Sw.Button({ Text: "Next", Icon: "right", IconRight: true }), tag(new Sw.Button({ Icon: "settings", Title: "Settings (icon only: long press or focus shows this hint)" }), "btn-icon"),
                new Sw.Button({ Icon: "IconUser", Title: "User (icon class)", Kind: "ghost" }), new Sw.Button({ Icon: "../icons/table_country.svg", Title: "Url mask icon", Kind: "ghost" }),
                new Sw.Button({ Icon: "../icons/ext/pdf.svg*", Title: "Full color url icon", Kind: "ghost" })),
            row("Small", new Sw.Button({ Text: "Small", Small: true }), new Sw.Button({ Icon: "plus", Small: true, Title: "Add" }), new Sw.Button({ Text: "Small primary", Small: true, Kind: "primary" })),
            row("States", tag(new Sw.Button({ Text: "Toggle", Pressable: true, Icon: "star" }), "btn-toggle"), new Sw.Button({ Text: "Pressed", Pressable: true, Pressed: true }),
                new Sw.Button({ Text: "Disabled", Disabled: "Demo: always disabled" }), new Sw.Button({ Text: "Disabled primary", Kind: "primary", Disabled: "Demo: always disabled" }),
                new Sw.Button({ Text: "Working", Working: true, Icon: "save" })),
            row("Actions", tag(new Sw.Button({ Text: "Async 2 s", Icon: "send", Kind: "primary", OnClick: () => wait(2000).then(() => Sw.Info("Async click done")) }), "btn-async"),
                tag(new Sw.Button({ Text: "Throws", Icon: "panic", OnClick: async () => { await wait(300); throw new Error("Demo failure from an async click"); } }), "btn-throw"),
                tag(new Sw.Button({ Icon: "plus", Title: "Hold to repeat", Repeat: true, OnClick: () => counter.textContent = String(++count) }), "btn-repeat"), counter),
            new Sw.Button({ Text: "Block (full width)", Block: true, Icon: "download" }),
            Sw.ButtonRow(new Sw.Button({ Text: "Row 1" }), new Sw.Button({ Text: "Row 2" }), new Sw.Button({ Text: "Row 3", Kind: "primary" })));
    }

    /**
     * The UX guide rules the controls implement: disabled with a reason, busy buttons, modifier clicks, menu buttons, repeat by click,
     * copy with an in place confirmation, the touch target of small controls.
     * @returns {HTMLElement} The section.
     */
    function uxControls() {
        const del = tag(new Sw.Button({ Text: "Delete row", Icon: "trash", Kind: "danger", Disabled: "Select a row first" }), "ux-disabled");
        const sel = tag(new Sw.Toggle({ Label: "A row is selected", OnChange: v => del.SetDisabled(!v, v ? undefined : "Select a row first") }), "ux-select-row");
        const tb = tag(new Sw.TextBox({ Label: "Disabled box (reason on use)", Value: "Read me, copy me", Disabled: "Only the owner can change this", Width: "100%" }), "ux-text-disabled");
        const seg = tag(new Sw.Segmented({ AriaLabel: "Range", Items: [{ Value: 1, Text: "1 h" }, { Value: 24, Text: "24 h" }, { Value: 0, Text: "All", Disabled: "Needs the archive service" }], Value: 1 }), "ux-seg");
        const busyText = tag(new Sw.Button({ Text: "Save (text only, 2 s)", OnClick: () => wait(2000) }), "ux-busy-text");
        const busyIcon = tag(new Sw.Button({ Text: "Export", Icon: "download", Kind: "primary", OnClick: () => wait(1500) }), "ux-busy-icon");
        const busyToast = tag(new Sw.Button({ Text: "Send (toast result)", Icon: "send", OnClick: () => wait(800).then(() => Sw.Info("Message sent to 3 people")) }), "ux-busy-toast");
        const menuB = tag(new Sw.Button({ Text: "More", Icon: "menu", Title: "More actions for this row (right click or Shift+F10 too)", Menu: () => [{ Text: "Rename", Icon: "edit" }, { Text: "Duplicate", Icon: "copy" }, "-", { Text: "Delete", Icon: "trash", Danger: true }] }), "ux-menu");
        let n = 0;
        const count = el("span.sw-badge", { text: "0", attrs: { "aria-live": "polite" } });
        const rep = tag(new Sw.Button({ Icon: "plus", Title: "Add one (hold to repeat; a screen reader click adds one)", Repeat: true, OnClick: () => count.textContent = String(++n) }), "ux-repeat");
        const copyKey = tag(Sw.CopyButton({ Value: "sk-demo-4711-secret", What: "API key", Generic: true, Label: "Copy the API key of MailService" }), "ux-copy");
        const copyId = tag(Sw.CopyButton({ Value: "7f3a9c2e-61d4-4b0e-9a55-1c2d3e4f5a6b" }), "ux-copy-id");
        const value = tag(el("code.g-copy-value", { text: "7f3a9c2e-61d4-4b0e-9a55-1c2d3e4f5a6b" }), "ux-copy-value");
        value.title = Sw.Clipboard.CopyOnClick(value, null);
        const mods = tag(el("div.g-box.sw-selectable", { text: "Ctrl / Shift / Alt + click on any control on this page only moves the focus there (the ring shows), it never runs the action." }), "ux-mod-note");
        return section("uxcontrols", "Disabled, busy and copy (UX guide)",
            row("Disabled", sel, del, seg),
            tb,
            row("Busy", busyText, busyIcon, busyToast),
            row("Menu, repeat", menuB, rep, count),
            row("Copy", copyKey, copyId, value),
            row("Small (44 px hit area on touch)", new Sw.Button({ Icon: "plus", Small: true, Title: "Add" }), new Sw.Button({ Icon: "minus", Small: true, Title: "Remove" }), new Sw.Chip({ Text: "Chip" })),
            mods);
    }

    /**
     * The parity test of the controls: every value control with Disabled (a reason), Required and its busy / dirty behaviour, and a
     * "Check" button that audits them (aria-disabled + reason, aria-required + marker + message, SetDisabled round trip) and lists the
     * result. Also: an OnClick that forgets to return its request (still busy), a clickable card, a form whose save fails offline.
     * @returns {HTMLElement} The section.
     */
    function uxParity() {
        const why = "Locked by the demo";
        const opts = [{ Value: 1, Label: "One" }, { Value: 2, Label: "Two" }];
        const disabled = [
            ["Button", new Sw.Button({ Text: "Button", Disabled: why })],
            ["Segmented", new Sw.Segmented({ AriaLabel: "Segmented", Items: [{ Value: 1, Text: "A" }, { Value: 2, Text: "B" }], Value: 1, Disabled: why })],
            ["Toggle", new Sw.Toggle({ Label: "Toggle", Disabled: why })],
            ["Slider", new Sw.Slider({ Title: "Slider", Value: 0.4, Disabled: why })],
            ["NumberBox", new Sw.NumberBox({ Label: "NumberBox", Value: 3, Disabled: why })],
            ["TextBox", new Sw.TextBox({ Label: "TextBox", Value: "text", Disabled: why })],
            ["Select", new Sw.Select({ Label: "Select", Options: opts, Value: 1, Disabled: why })],
            ["ColorPicker", new Sw.ColorPicker({ Title: "ColorPicker", Hex: true, Value: "#3366ff", Disabled: why })],
            ["Chip", new Sw.Chip({ Text: "Chip", Disabled: why })],
        ];
        const required = [
            ["TextBox", new Sw.TextBox({ Label: "TextBox", Required: true })],
            ["NumberBox", new Sw.NumberBox({ Label: "NumberBox", Required: true })],
            ["Select", new Sw.Select({ Label: "Select", Options: opts, Required: true })],
            ["Toggle", new Sw.Toggle({ Label: "Toggle (must be on)", Required: true })],
        ];
        const out = tag(el("div.g-box.sw-selectable", { attrs: { role: "status" } }), "ux-parity-out");
        /** @param {object} c A control. @returns {Element} Its focusable part. */
        const focusE = c => c.Input || (c.Button instanceof Element ? c.Button : null) || (c.Buttons ? c.Buttons[0] : null) || c.E;
        /** Audit the controls and list the result. */
        const check = () => {
            const rows = [];
            for (const [n, c] of disabled) {
                const f = focusE(c);
                const aria = f.getAttribute("aria-disabled") === "true";
                const reason = f.SwDisabledReason === why;
                c.SetDisabled(false);
                const off = f.getAttribute("aria-disabled") !== "true";
                c.SetDisabled(true, why);
                rows.push([n, "Disabled", aria && reason && off ? "ok" : "FAIL (aria-disabled " + aria + ", reason " + reason + ", enables " + off + ")"]);
            }
            for (const [n, c] of required) {
                const f = focusE(c);
                const aria = f.getAttribute("aria-required") === "true";
                const mark = !!c.E.querySelector(".sw-field-req");
                const msg = c.Validate();
                rows.push([n, "Required", aria && mark && msg ? "ok" : "FAIL (aria-required " + aria + ", marker " + mark + ", message " + !!msg + ")"]);
            }
            out.replaceChildren(el("table.g-parity", null, el("tbody", null, ...rows.map(r => el("tr", null, ...r.map(x => el("td", { text: x })))))));
            const bad = rows.filter(r => r[2] !== "ok").length;
            Sw.Page.Announce(bad ? bad + " checks failed" : "All " + rows.length + " checks passed");
        };
        //  Busy without a returned promise: the request still makes the button busy (a developer warning on localhost says to return it)
        const forgot = tag(new Sw.Button({
            Text: "Forgot return", Icon: "send", Saves: false,
            OnClick: () => {
                /* SwWeb:allow ux-fbk-2 (the demo of the guard rail: a forgotten return still makes the button busy) */
                Sw.Net.Get("gallery.html?ux-parity=" + Date.now(), { Text: true });
            },
        }), "ux-forgot-return");
        //  A clickable card: a pure click opens, a modifier click only focuses its title button
        let opened = 0;
        const openCount = el("span.sw-badge", { text: "0" });
        const card = Sw.Card({ Title: "Clickable card", Sub: "Ctrl+click only focuses", Body: "Click anywhere on this card.", OnClick: () => wait(600).then(() => openCount.textContent = String(++opened)) });
        tag(card.E, "ux-card-click");
        //  A form whose first save fails as if the connection was lost: the text stays, a line says so, Try again
        const name = new Sw.TextBox({ Label: "Display name", Value: "Ada" });
        const save = new Sw.Button({ Text: "Save (fails once, offline)", Kind: "primary" });
        let fail = true;
        Sw.Form({
            Title: "the demo form", Fields: [{ Name: "Name", Control: name, Required: true }], Submit: save,
            OnSubmit: async () => {
                await wait(400);
                if (fail) {
                    fail = false;
                    const x = new TypeError("Failed to fetch");
                    x.Offline = true;
                    throw x;
                }
            },
        });
        tag(save, "ux-form-save");
        tag(name, "ux-form-name");
        return section("uxparity", "Controls parity: disabled, required, busy, dirty (UX guide)",
            row("Disabled (a reason)", ...disabled.map(x => x[1])),
            row("Required", ...required.map(x => x[1])),
            row("Audit", tag(new Sw.Button({ Text: "Check", Icon: "check", Saves: false, OnClick: check }), "ux-parity-check")),
            out,
            row("Busy", forgot, el("span.sw-muted", { text: "Clicks while busy are absorbed." })),
            row("Card", card.E, openCount),
            el("p.sw-muted", { text: "Type in Display name and leave the page: it asks first. The first Save fails as if offline: the text stays, Try again saves." }),
            name.E, Sw.ButtonRow(save));
    }

    /** @returns {HTMLElement} Inputs. */
    function inputs() {
        const echo = el("span.sw-muted", { text: "-" });
        return section("inputs", "Text and numbers",
            tag(new Sw.TextBox({ Label: "Name", Placeholder: "Your name", Clear: true, OnInput: v => echo.textContent = v || "-", Width: "100%" }), "text-name"),
            row("Echo", echo),
            tag(new Sw.TextBox({ Placeholder: "Search", Type: "search", Icon: "search", Clear: true, Width: "100%" }), "text-search"),
            tag(new Sw.TextBox({ Label: "Password", Type: "password", Reveal: true, Value: "secret", Width: "100%" }), "text-pw"),
            tag(new Sw.TextBox({ Label: "Email (validated on change)", Type: "email", Width: "100%", Validate: v => !v || /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v) ? null : "That is not an email address" }), "text-email"),
            new Sw.TextBox({ Label: "Multiline", Multiline: true, Rows: 3, Value: "Line one\nLine two", Width: "100%" }),
            new Sw.TextBox({ Label: "Disabled", Value: "Can't touch this", Disabled: "Demo: always disabled", Width: "100%" }),
            row("Numbers", tag(new Sw.NumberBox({ Value: 42, Step: 1, Min: 0, Max: 100, Label: "Integer 0..100" }), "num-int"), new Sw.NumberBox({ Value: 0.25, Step: 0.01, Label: "Fraction" }),
                new Sw.NumberBox({ Value: 7, Disabled: "Demo: always disabled", Label: "Disabled" })));
    }

    /** @returns {HTMLElement} Switches, chips and badges. */
    function toggles() {
        const tags = row("Removable");
        ["Alpha", "Beta", "Gamma"].forEach(n => tags.append(new Sw.Chip({ Text: n, Removable: true, Pressable: false, OnRemove: c => Sw.Info("Removed " + n) }).E));
        return section("toggles", "Switches, chips and badges",
            row("Switch", tag(new Sw.Toggle({ Label: "Enabled", Value: true }), "toggle-on"), tag(new Sw.Toggle({ Label: "Disabled feature" }), "toggle-off"),
                new Sw.Toggle({ Label: "Locked", Value: true, Disabled: "Demo: always disabled" })),
            row("Chips", tag(new Sw.Chip({ Text: "Filter", Icon: "filter" }), "chip"), new Sw.Chip({ Text: "Pressed", Pressed: true }), new Sw.Chip({ Text: "Tables", Icon: "IconTable" })),
            tags,
            row("Badges", Sw.Badge("Neutral"), Sw.Badge("Ok", "ok"), Sw.Badge("Error", "error"), Sw.Badge("Warning", "warning"), Sw.Badge("Accent", "accent"), Sw.Badge("Accent 2", "accent2")));
    }

    /** @returns {HTMLElement} Sliders. */
    function sliders() {
        const val = el("span.sw-badge.sw-mono", { text: "0.50" });
        const vbox = el("div", { style: { height: "160px", display: "flex", gap: "24px" } },
            new Sw.Slider({ Vertical: true, Value: 0.3, Title: "Vertical" }).E,
            new Sw.Slider({ Vertical: true, Fader: true, Value: 0.7, Accent2: true, Title: "Vertical fader" }).E);
        return section("sliders", "Sliders",
            row("Plain", tag(new Sw.Slider({ Value: 0.5, Default: 0.5, Title: "Plain slider (double tap resets)", OnInput: v => val.textContent = v.toFixed(2) }), "slider"), val),
            row("Log", new Sw.Slider({ Min: 0.05, Max: 8, Log: true, Value: 1, Center: 1, Default: 1, Title: "Speed" })),
            row("Steps", new Sw.Slider({ Min: 0, Max: 10, Step: 1, Value: 3, Accent2: true, Title: "Integer steps" })),
            row("Fader", new Sw.Slider({ Fader: true, Value: 0.5, Center: 0.5, Title: "Crossfader" })),
            row("Disabled", new Sw.Slider({ Value: 0.4, Disabled: "Demo: always disabled", Title: "Disabled" })),
            vbox);
    }

    /** @returns {HTMLElement} Selects and color pickers. */
    function selects() {
        const short = [{ Value: 1, Label: "One" }, { Value: 2, Label: "Two", Icon: "star" }, { Value: 3, Label: "Three (disabled)", Disabled: true }, { Value: 4, Label: "Four", Hint: "Ctrl+4" }];
        const long = Countries.map(c => ({ Value: c, Label: c }));
        return section("selects", "Selects and colors",
            row("Short", tag(new Sw.Select({ Options: short, Value: 2, Title: "Short list" }), "select-short")),
            row("Searchable", tag(new Sw.Select({ Options: long, Value: "Sweden", Title: "Country", Width: "220px" }), "select-long")),
            row("Placeholder", new Sw.Select({ Options: short, Placeholder: "Pick one...", Title: "Nothing selected" })),
            row("Disabled", new Sw.Select({ Options: short, Value: 1, Disabled: "Demo: always disabled" })),
            row("Colors", tag(new Sw.ColorPicker({ Value: [0.9, 0.42, 0.66], Title: "Pink" }), "color"), new Sw.ColorPicker({ Value: "#4fc9e0", Hex: true, Title: "Hex value" }),
                new Sw.ColorPicker({ Value: [2, 1, 0.4], Title: "HDR (intensity slider)" }), new Sw.ColorPicker({ Value: [0.2, 0.2, 0.2], Disabled: "Demo: always disabled" })));
    }

    /** @returns {HTMLElement} Progress bars and spinners. */
    function progress() {
        const live = new Sw.ProgressBar({ Value: 0, Label: "Demo progress" });
        let p = 0;
        setInterval(() => {
            p = (p + 3) % 103;
            live.SetValue(p, 100, "{1} of {2} files ({0})");
        }, 400);
        return section("progress", "Progress and spinners",
            tag(live, "progress-live"),
            new Sw.ProgressBar({ Value: 0 }), new Sw.ProgressBar({ Value: 35 }), new Sw.ProgressBar({ Value: 70, Text: "Uploading {0}" }), new Sw.ProgressBar({ Value: 100 }),
            new Sw.ProgressBar({ Indeterminate: true }), new Sw.ProgressBar({ Value: 60, Small: true }),
            row("Spinners", new Sw.Spinner().E, new Sw.Spinner({ Size: 32 }).E, new Sw.Spinner({ Size: 48, Title: "Loading" }).E));
    }

    /**
     * Icon classes whose svg files come from an optional service (not from the icon module): shown only when the service runs
     * (one Api/HaveService call), so apps without it request no missing files (no 404s). Service type name: mask url pattern.
     */
    const OptionalIconFiles = { "SysWeaver.MicroService.PassKeyService": /\/icons\/passkey_[^/]*$/i };

    /** @returns {HTMLElement} Icons (inline svg set and every icon class of the style sheets). */
    function icons() {
        const inline = el("div.g-icons");
        Sw.Icons.List().forEach(n => inline.append(el("div.g-icon", { title: n }, Sw.Icon(n), el("span", { text: n }))));
        /** @type {Map<string, string|null>} Icon class name to the optional service its file needs (null = always available). */
        const classes = new Map();
        for (const s of document.styleSheets) {
            let rules = null;
            try {
                rules = s.cssRules;
            }
            catch {
                continue;
            }
            for (const r of rules || []) {
                const m = /^\.(Icon[A-Z][A-Za-z0-9]*)$/.exec(r.selectorText || "");
                if (!m || m[1].startsWith("IconColor"))
                    continue;
                const u = /url\(\s*["']?([^"')]+)/.exec((r.style && (r.style.getPropertyValue("mask-image") || r.style.getPropertyValue("-webkit-mask-image"))) || "");
                let svc = null;
                if (u) {
                    const path = new URL(u[1], s.href || location.href).pathname;
                    svc = Object.keys(OptionalIconFiles).find(k => OptionalIconFiles[k].test(path)) || null;
                }
                //  The last rule wins (a page may override a class), like the cascade
                classes.set(m[1], svc);
            }
        }
        const cls = el("div.g-icons", { dataset: { g: "icon-classes" } });
        const count = el("div.sw-muted");
        const shown = [];
        /** Show the available icon classes in name order (again when an optional service was found). */
        const fill = () => {
            cls.replaceChildren(...shown.sort().map(n => el("div.g-icon", { title: n }, Sw.IconClass(n), el("span", { text: n }))));
            count.textContent = "Icon classes (Sw.IconClass, " + shown.length + ")";
        };
        classes.forEach((svc, n) => {
            if (!svc)
                shown.push(n);
        });
        fill();
        new Set(Array.from(classes.values()).filter(x => x)).forEach(async svc => {
            let have = false;
            try {
                have = !!(await Sw.Net.Send(Sw.Url.Root + "Api/HaveService", svc, { NoNavigate: true }));
            }
            catch {
            }
            if (!have)
                return;
            classes.forEach((s, n) => {
                if (s === svc)
                    shown.push(n);
            });
            fill();
        });
        return section("icons", "Icons",
            el("div.sw-muted", { text: "Inline svg icons (Sw.Icon, " + Sw.Icons.List().length + ")" }), inline,
            count, cls,
            row("Colors", Sw.IconClass("IconHome", { Size: 32, Color: "IconColorThemeMain" }), Sw.IconClass("IconHome", { Size: 32, Color: "IconColorThemeAcc1" }),
                Sw.IconClass("IconHome", { Size: 32, Color: "IconColorThemeAcc2" }), Sw.IconClass("IconLogo", { Size: 32, Title: "Logo (full color)" })));
    }

    /**
     * The demo menu items (icons, hints, check / radio, disabled, danger, headings, separators, submenus).
     * @returns {Array} The items.
     */
    function demoMenu() {
        let theme = Sw.Theme.Get();
        return [
            { Title: "File" },
            { Text: "New", Icon: "newFile", Hint: "Ctrl+N", OnClick: () => Sw.Info("New") },
            { Text: "Open...", Icon: "open", Hint: "Ctrl+O", OnClick: () => Sw.Info("Open") },
            { Text: "Recent", Icon: "history", Items: [{ Text: "report.md", OnClick: () => Sw.Info("report.md") }, { Text: "notes.txt" }, "-", { Text: "Clear recent", Danger: true }] },
            { Text: "Export", Icon: "download", Items: async () => { await wait(300); return [{ Text: "As CSV", Icon: "IconFileCsv" }, { Text: "As JSON", Icon: "IconFileJson" }, { Text: "More formats", Items: [{ Text: "Excel", Icon: "IconFileXlsx" }, { Text: "Pdf", Icon: "IconFilePdf" }] }]; } },
            "-",
            { Title: "View" },
            { Text: "Show grid", Checked: true, Keep: true },
            { Text: "Show rulers", Checked: false, Keep: true },
            { Text: "Theme", Icon: "theme", Items: Sw.Theme.List().map(n => ({ Text: themeLabel(n), Checked: n === theme, Radio: true, OnClick: () => Sw.Theme.Set(n) })) },
            { Text: "Disabled item", Icon: "lock", Disabled: true },
            "-",
            { Text: "Delete everything", Icon: "trash", Danger: true, OnClick: () => Sw.Fail("Nothing was deleted (demo)") },
        ];
    }

    /** @returns {HTMLElement} Popups and menus. */
    function menus() {
        const ctx = el("div.g-box", { text: "Long press (touch), right click (mouse) or the context menu key / Shift+F10 (keyboard) here", tabIndex: 0, dataset: { g: "context-area" } });
        Sw.Menu.Context(ctx, ev => [{ Title: "Context" }, { Text: "Copy", Icon: "copy", OnClick: () => Sw.Clipboard.Copy("Context menu text") }, { Text: "Paste", Icon: "replace", Disabled: true }, "-", { Text: "Properties", Icon: "settings" }]);
        const menuBtn = tag(new Sw.Button({ Text: "Menu", Icon: "menu", Menu: () => demoMenu() }), "menu-btn");
        const dropBtn = tag(new Sw.Button({ Text: "Drill down", Icon: "layers", Menu: () => demoMenu(), MenuOptions: { DrillDown: true } }), "menu-drill");
        const popBtn = tag(new Sw.Button({ Text: "Popup", Icon: "info", OnClick: b => {
            const tb = new Sw.TextBox({ Placeholder: "Inside a popup" });
            const p = Sw.Popup.Show(b.E, el("div", { style: { padding: "12px", display: "flex", flexDirection: "column", gap: "8px", width: "260px" } },
                el("b", { text: "A popup" }), el("div.sw-muted", { text: "Tap outside or press Escape to close." }), tb.E,
                new Sw.Button({ Text: "Close", Kind: "primary", OnClick: () => p.Close() }).E), { Focus: true });
        } }), "popup-btn");
        const hint = tag(new Sw.Button({ Text: "Hint", Icon: "help", OnClick: b => Sw.Hint.Show(b.E, "This is a hint bubble (Sw.Hint)") }), "hint-btn");
        return section("menus", "Popups and menus",
            row("Menus", menuBtn, dropBtn, new Sw.Button({ Text: "Above", Icon: "up", Menu: () => demoMenu(), MenuOptions: { Side: "above" } }),
                new Sw.Button({ Text: "Right aligned", Icon: "right", Menu: () => demoMenu(), MenuOptions: { Align: "end" } })),
            row("Popups", popBtn, hint), ctx);
    }

    /** @returns {HTMLElement} Dialogs. */
    function dialogs() {
        const out = el("span.sw-badge", { text: "-", dataset: { g: "dialog-result" } });
        /** Show a dialog result. */
        const show = v => out.textContent = String(v);
        const recents = new Sw.SearchSelection({
            Id: "GalleryCountries",
            GetId: d => d,
            Find: async (filter, recent, count, offset) => {
                await wait(150);
                const f = filter.toLowerCase();
                const all = Countries.filter(c => c.toLowerCase().includes(f)).sort((a, b) => (recent.get(a) || 999) - (recent.get(b) || 999));
                return all.slice(offset, offset + count);
            },
        });
        return section("dialogs", "Dialogs",
            row("Basic", tag(new Sw.Button({ Text: "Show", OnClick: async () => show(await Sw.Dialog.Show({ Title: "A dialog", Icon: "info", Content: "Any content: text, elements, or a builder function.\nThe buttons resolve the promise.", Buttons: [{ Text: "Later", Value: "later" }, { Text: "Now", Kind: "primary", Value: "now" }] })) }), "dlg-show"),
                tag(new Sw.Button({ Text: "Confirm", OnClick: async () => show(await Sw.Dialog.Confirm("Delete the file?", "report.md will be removed. This can't be undone.", "Delete", { Danger: true })) }), "dlg-confirm"),
                tag(new Sw.Button({ Text: "Prompt", OnClick: async () => show(await Sw.Dialog.Prompt("Rename", "report.md", "File name", { Text: "Enter a new name", Validate: v => v.trim() ? null : "A name is required" })) }), "dlg-prompt"),
                tag(new Sw.Button({ Text: "Alert", OnClick: async () => show(await Sw.Dialog.Alert("Done", "Everything went fine.")) }), "dlg-alert")),
            row("Large", new Sw.Button({ Text: "Wide", OnClick: () => Sw.Dialog.Show({ Title: "Wide dialog", Wide: true, Content: el("div", null, ...Array.from({ length: 40 }, (_, i) => el("p", { text: "Paragraph " + (i + 1) + " of a long scrolling dialog body." }))) }) }),
                new Sw.Button({ Text: "Full height", OnClick: () => Sw.Dialog.Show({ Title: "Full height", Full: true, Content: "Uses the whole height." }) }),
                new Sw.Button({ Text: "No close", OnClick: () => Sw.Dialog.Show({ Title: "Accept the terms to continue", Close: false, NoEscape: true, Buttons: [{ Text: "Accept", Kind: "primary", Value: 1 }] }) })),
            row("Select", tag(new Sw.Button({ Text: "Selection", Icon: "search", OnClick: async () => show(await Sw.Dialog.Select({ Title: "Pick a country", GetAll: true, Items: async s => Countries.filter(c => c.toLowerCase().includes(s.toLowerCase())).map(c => ({ Text: c, Value: c, Icon: "globe" })) })) }), "dlg-select"),
                tag(new Sw.Button({ Text: "With recents", Icon: "history", OnClick: async () => show(await recents.Show({ Title: "Countries (paged, remembers recents)", PageSize: 10 })) }), "dlg-recent"),
                tag(new Sw.Button({ Text: "Working 2 s", Icon: "clock", Title: "Work({ Signal }): reading the signal makes it cancellable, Cancel aborts it", OnClick: () => { /* SwWeb:allow ux-fbk-4 (Work reads the AbortSignal: Cancel is offered) */ Sw.Dialog.Working({ Title: "Checking the links", Text: "Checking 12 links", Work: ({ Signal }) => waitAbortable(2000, Signal) }); } }), "dlg-working")),
            //  Close attempts (UX-POP-6): dirty asks "Discard changes?", busy shows the rejection cue, the 10 s Close, Cancel
            row("Close attempts", tag(new Sw.Button({
                Text: "Dirty form", Title: "Type in the field, then try to close it: outside click, Escape, the close button, Cancel", OnClick: async () => show(await Sw.Dialog.Show({
                    Title: "Rename report", Id: "gallery.dirty", Content: el("div.sw-dialog-prompt", null, new Sw.TextBox({ Label: "File name", Value: "report.md" }).E),
                    Buttons: [{ Text: "Cancel", Value: null }, { Text: "Rename", Kind: "primary", Value: "renamed" }],
                })),
            }), "dlg-dirty"),
                tag(new Sw.Button({
                    Text: "Save with Cancel", Title: "An async Save (6 s) that reads the AbortSignal it is given: while it runs only Cancel acts, and Cancel really stops it", OnClick: async () => show(await Sw.Dialog.Show({
                        Title: "Edit customer", Content: el("div.sw-dialog-prompt", null, new Sw.TextBox({ Label: "Name", Value: "Initech" }).E, new Sw.Toggle({ Label: "VIP customer" }).E),
                        Buttons: [{ Text: "Cancel", Value: null }, { Text: "Save", Kind: "primary", Value: "saved", OnClick: (close, value, { Signal }) => waitAbortable(6000, Signal) }],
                    })),
                }), "dlg-savecancel"),
                tag(new Sw.Button({
                    Text: "Save that can't be stopped", Title: "An async Save (5 s) that doesn't read the AbortSignal (like a request already sent): no Cancel, \"Stop waiting (Save may still finish)\" instead; the dialog never says \"done\" for it", OnClick: async () => show(await Sw.Dialog.Show({
                        Title: "Edit customer", Id: "gallery.stopwait", Content: el("div.sw-dialog-prompt", null, new Sw.TextBox({ Label: "Name", Value: "Initech" }).E),
                        Buttons: [{ Text: "Cancel", Value: null }, { Text: "Save", Kind: "primary", Value: "saved", OnClick: () => wait(5000) }],
                    })),
                }), "dlg-stopwait"),
                tag(new Sw.Button({
                    Text: "Busy with Cancel", Title: "A 20 s upload with progress: closing is refused, Cancel stops it", OnClick: () => {
                        const h = Sw.Dialog.Open({ Title: "Upload report.xlsx", Id: "gallery.busy", Content: "The file is sent to the server.", Buttons: [{ Text: "Cancel", Value: null }, { Text: "Done", Kind: "primary", Value: 1 }] });
                        let n = 0, timer = 0;
                        const p = new Promise(r => timer = setInterval(() => { n++; h.SetProgress(n / 20); if (n >= 20) { clearInterval(timer); r(); } }, 1000));
                        h.SetBusy(p, { Cancel: () => clearInterval(timer), Text: "Uploading report.xlsx", Progress: 0, Background: true });
                        (async () => show(await h.Result))();
                    },
                }), "dlg-busy"),
                tag(new Sw.Button({ Text: "Working 15 s", Icon: "clock", Title: "Work that can't be stopped and has no activity list entry: after 10 s \"Close (the work still finishes)\" is added left of the other buttons", OnClick: () => { const close = Sw.Dialog.Working({ Title: "Rebuilding the index", Text: "This normally takes a few seconds.", Background: false }); /* SwWeb:allow ux-fbk-4 ux-fbk-2 (demo of the 10 s Close: work that can't be stopped; opening it is not work) */ wait(15000).then(close); } }), "dlg-working15"),
                tag(new Sw.Button({ Text: "Working + Cancel", Icon: "stop", OnClick: () => { let stop; const p = new Promise(r => { const t = setTimeout(r, 8000); stop = () => { clearTimeout(t); r(); }; }); Sw.Dialog.Working({ Title: "Copying files", Text: "Copying 120 files", Promise: p, Cancel: () => stop() }); } }), "dlg-workingcancel")),
            row("Movable", tag(new Sw.Button({ Text: "Movable dialog", Icon: "drag", Title: "Drag the title bar or an edge, or use the dialog menu (Move, Size, Maximize, Reset position); remembered", OnClick: () => Sw.Dialog.Show({ Title: "Move and resize me", Id: "gallery.movable", Content: "Drag the title bar, an edge or a corner; double click the title bar to maximize. The dialog menu (⋮) does it with the keyboard or with step buttons. The position is remembered for this dialog.", Buttons: [{ Text: "Close", Kind: "primary", Value: 1 }] }) }), "dlg-movable")),
            row("Result", out));
    }

    /** @returns {HTMLElement} Toasts, loaders, server block, full screen. */
    function overlays() {
        const box = el("div.g-box", { text: "A local loader covers only this box.", dataset: { g: "loader-box" } });
        const fsState = el("span.sw-badge", { text: "off" });
        Sw.FullScreen.OnChange(on => fsState.textContent = on ? "on" : "off");
        /** Simulate a server block of a kind for 3.5 s (core events). */
        const block = kind => async () => {
            Sw.Events.Emit("server.block", { Type: kind, Time: new Date() });
            await wait(3500);
            Sw.Events.Emit("server.unblock", { Type: "server.continue" });
        };
        return section("overlays", "Toasts and overlays",
            row("Toasts", tag(new Sw.Button({ Text: "Info", OnClick: () => Sw.Info("Saved 3 files (click to copy)") }), "toast-info"),
                tag(new Sw.Button({ Text: "Fail", Kind: "danger", OnClick: () => Sw.Fail("Failed to save: the server said no") }), "toast-fail"),
                new Sw.Button({ Text: "Success", OnClick: () => Sw.Toast("All good", { Kind: "ok" }) }), new Sw.Button({ Text: "Warning", OnClick: () => Sw.Toast("Disk almost full", { Kind: "warning" }) }),
                new Sw.Button({ Text: "Long", OnClick: () => Sw.Info("A long message that wraps over several lines. ".repeat(6)) }),
                new Sw.Button({ Text: "Copy text", Icon: "copy", OnClick: () => Sw.Clipboard.Copy("Copied from the gallery") })),
            row("Loaders", tag(new Sw.Button({ Text: "Page loader 2 s", OnClick: async () => { const r = Sw.Loader.Show({ Text: "Loading the gallery..." }); await wait(2000); r(); } }), "loader-page"),
                new Sw.Button({ Text: "Opaque 1.5 s", OnClick: async () => { const r = Sw.Loader.Show({ Opaque: true, Text: "Opaque loader" }); await wait(1500); r(); } }),
                tag(new Sw.Button({ Text: "Local 2 s", OnClick: async () => { const r = Sw.Loader.Show({ Target: box, Text: "Busy" }); await wait(2000); r(); } }), "loader-local"),
                new Sw.Button({ Text: "Sw.Page.Loading", OnClick: async () => { const r = Sw.Page.Loading({ Text: "Page protocol loading" }); await wait(1500); r(true); } })),
            box,
            row("Server", tag(new Sw.Button({ Text: "Paused", Icon: "pause", OnClick: block("server.pause") }), "block-pause"),
                new Sw.Button({ Text: "Restarting", Icon: "refresh", OnClick: block("server.restart") }),
                new Sw.Button({ Text: "Shut down", Icon: "power", OnClick: block("server.shutdown") }),
                new Sw.Button({ Text: "Lost", Icon: "warning", OnClick: block("server.error") })),
            row("Full screen", tag(new Sw.Button({ Text: "Toggle", Icon: "fullscreen", OnClick: () => Sw.FullScreen.Toggle() }), "fullscreen"), fsState));
    }

    /** @returns {HTMLElement} Tabs, split, pager, columns. */
    function layouts() {
        let n = 3;
        const tabs = new Sw.Tabs({ Closable: true, OnAdd: () => tabs.Add({ Title: "Tab " + (++n), Content: el("div", { style: { padding: "12px" }, text: "Content of tab " + n }) }), Label: "Demo tabs" });
        tabs.Add({ Title: "Overview", Icon: "home", Closable: false, Content: el("div", { style: { padding: "12px" }, text: "Arrow keys move between the tabs, Delete closes, middle click closes." }) });
        const t2 = tabs.Add({ Title: "Edited", Content: el("div", { style: { padding: "12px" }, text: "This tab has unsaved changes (the dot)." }) });
        t2.SetDirty(true);
        const t3 = tabs.Add({ Title: "Errors", Icon: "warning", Content: el("div", { style: { padding: "12px" }, text: "A badge shows a count." }) });
        t3.SetBadge("3", "error");
        tabs.E.style.height = "160px";
        tabs.E.style.border = "1px solid var(--sw-line)";
        tabs.E.dataset.g = "tabs";
        const split = new Sw.Split({ A: el("div", { style: { padding: "8px" }, text: "Pane A (drag the bar, arrow keys, double click resets)" }), B: el("div", { style: { padding: "8px" }, text: "Pane B" }), Size: 0.4, Key: "SwGallerySplit" });
        split.E.style.height = "120px";
        split.E.style.border = "1px solid var(--sw-line)";
        split.E.dataset.g = "split";
        const vsplit = new Sw.Split({ A: el("div", { style: { padding: "8px" }, text: "Top" }), B: el("div", { style: { padding: "8px" }, text: "Bottom" }), Vertical: true });
        vsplit.E.style.height = "160px";
        vsplit.E.style.border = "1px solid var(--sw-line)";
        const prev = new Sw.Button({ Icon: "left", Title: "Previous page", Small: true });
        const next = new Sw.Button({ Icon: "right", Title: "Next page", Small: true });
        const pager = new Sw.Pager({ OnChange: p => { prev.SetDisabled(!p.CanPrev(), "This is the first page"); next.SetDisabled(!p.CanNext(), "This is the last page"); } });
        prev.O.OnClick = () => pager.Prev();
        next.O.OnClick = () => pager.Next();
        for (let i = 1; i <= 14; ++i)
            pager.Content.append(el("div.g-pager-item", { text: "Item " + i, tabIndex: 0 }));
        pager.E.dataset.g = "pager";
        const cols = new Sw.Columns({ MinWidth: 140 });
        for (let i = 1; i <= 5; ++i)
            cols.Add(el("div.g-pager-item", { style: { width: "auto" }, text: "Column " + i }));
        return section("layout", "Tabs, split, pager, columns", tabs.E, split.E, vsplit.E, row(null, prev, next), pager.E, cols.E);
    }

    /** @returns {HTMLElement} The property editor. */
    function props() {
        const out = el("div.sw-muted.sw-mono", { text: "-" });
        const ed = new Sw.PropertyEditor({
            Defs: [
                { Key: "Speed", Label: "Speed", Type: "float", Min: 0.05, Max: 8, Log: true, Default: 1, Unit: "×", Group: "Clip", Desc: "Playback speed" },
                { Key: "Count", Label: "Count", Type: "int", Min: 0, Max: 20, Default: 5, Group: "Clip" },
                { Key: "Loop", Label: "Loop", Type: "bool", Default: true, Group: "Clip" },
                { Key: "Mode", Label: "Mode", Type: "enum", Default: 0, Options: [{ Value: 0, Label: "Fast" }, { Value: 1, Label: "Nice" }], Group: "Look" },
                { Key: "Tint", Label: "Tint", Type: "color", Default: [1, 0.5, 0.2], Group: "Look" },
                { Key: "Offset", Label: "Offset", Type: "vec2", Default: [0, 0], Min: -1, Max: 1, Group: "Look" },
                { Key: "Title", Label: "Title", Type: "string", Default: "Hello", Group: "Look" },
                { Key: "Range", Label: "Range", Type: "float", Default: 100, NoSlider: true, Group: "Look" },
            ],
            Values: { Speed: 2 },
            ResetButtons: true,
            OnChange: (k, v) => out.textContent = k + " = " + JSON.stringify(v),
        });
        ed.E.dataset.g = "props";
        return section("props", "Property editor", ed.E, out, row(null, new Sw.Button({ Text: "Disable Look", Small: true, Pressable: true, OnClick: b => ed.SetEnabled(d => d.Group === "Look", !b.Pressed) })));
    }

    /** @returns {HTMLElement} Value cells (Sw.Cells, every format). */
    function cells() {
        const now = new Date();
        const ago = new Date(now.getTime() - 3723000);
        const rowsDef = [
            ["Default", "System.String", "Hello world", null],
            ["Default (template)", "System.String", "Sweden", "Default;Country: {0};Title for {0}"],
            ["Number int", "System.Int32", 1234567, null],
            ["Number double", "System.Double", 3.14159, null],
            ["Number negative", "System.Int32", -42, null],
            ["Number zero", "System.Int32", 0, null],
            ["Number (2 dec, unit)", "System.Double", 12.5, "Number;2;{0} kg"],
            ["Url _blank", "System.String", "https://example.com", "Url"],
            ["Url *_self", "System.String", "gallery.html", "Url;{0};*{0}"],
            ["Url ^ / - targets", "System.String", "Top window", "Url;{0};^gallery.html"],
            ["Img", "System.String", "../icons/user.svg", "Img;{0};+../icons/user.svg;User icon;32;32"],
            ["Tags", "System.String", "admin:Administrator,ops:Operations,dev¤test:Comma in tag", "Tags;{1};{2};{0};true"],
            ["PerRowFormat", "System.String", "12345678", null, "System.Int64|ByteSize"],
            ["ByteSize", "System.Int64", 123456789, "ByteSize"],
            ["ByteSpeed", "System.Int64", 2500000, "ByteSpeed"],
            ["Toggle (fails: no api)", "System.String", "true,7", "Toggle;Enabled;Disabled;Click to disable {1};Click to enable {1};gallery/ToggleDemo?{2}"],
            ["Actions", "System.String", "7", "Actions;Open|Open in this tab|&gallery.html|IconNewTab;New tab|Open in a new tab|@gallery.html|external;Call|A GET request (fails)|gallery/ActionDemo|send;Off|No link||"],
            ["Duration", "System.TimeSpan", "01:02:03.5", "Duration"],
            ["TimeSpan type", "System.TimeSpan", "3.04:05:06", null],
            ["Json", "System.String", JSON.stringify({ Name: "Gallery", Count: 3, Ok: true, Items: [1, 2, 3], Nothing: null }), "Json;24;;{0}"],
            ["Text", "System.String", "A long text\nwith several lines\nthat opens in a popup when clicked.", "Text;20;;{0};False"],
            ["MD", "System.String", "# Markdown\n\nSome **bold** and *italic* text, a [link](https://example.com), `code`, and a list:\n\n- One\n- Two\n\n<script>alert(1)</script>\n[bad](javascript:alert(1))", "MD;16;;{0}"],
            ["Uptime", "System.DateTime", ago.toISOString(), "Uptime"],
            ["WebColor", "System.String", "#4fc9e0", "WebColor"],
            ["WebColor dark", "System.String", "rgba(20, 30, 60, 0.5)", "WebColor"],
            ["DateTime", "System.DateTime", now.toISOString(), null],
            ["DateTime utc", "System.DateTime", now.toISOString(), "utc"],
            ["DateTime live (empty)", "System.DateTime", "", null],
            ["DateTime not set", "System.DateTime", "0001-01-01T00:00:00", null],
            ["Boolean true", "System.Boolean", true, null],
            ["Boolean false", "System.Boolean", false, null],
            ["Type", "System.Type", "System.String", null],
        ];
        const body = el("tbody");
        for (const r of rowsDef) {
            const td = el("td");
            Sw.Cells.Update(td, r[1], r[2], false, r[3], r[4], () => Sw.Info("onRefresh called"));
            body.append(el("tr", null, el("th", { text: r[0] }), el("td.sw-muted.sw-mono", { text: r[3] || r[1], style: { fontSize: "11px", maxWidth: "180px", textOverflow: "ellipsis", whiteSpace: "nowrap" }, title: r[3] || r[1] }), td));
        }
        const flashCell = el("td");
        let fv = 1;
        Sw.Cells.Update(flashCell, "System.Int32", fv, "sw-flash");
        setInterval(() => Sw.Cells.Update(flashCell, "System.Int32", ++fv, "sw-flash"), 2500);
        body.append(el("tr", null, el("th", { text: "Flash on change" }), el("td.sw-muted", { text: "sw-flash" }), flashCell));
        const table = el("table.g-table", { dataset: { g: "cells" } }, el("thead", null, el("tr", null, el("th", { text: "Case" }), el("th", { text: "Format" }), el("th", { text: "Cell" }))), body);
        return section("cells", "Value cells", el("div.g-scroll", null, table));
    }

    /** @returns {HTMLElement} The code editor. */
    function code() {
        if (!Sw.CodeEditor)
            return section("code", "Code editor", el("div.sw-muted", { text: "ui/code.js is not loaded" }));
        const ed = new Sw.CodeEditor({
            Language: "glsl",
            Value: "// A GLSL sample (Ctrl+F find, Ctrl+G go to line, Ctrl+Space complete)\nvoid mainImage(out vec4 fragColor, in vec2 fragCoord)\n{\n    vec2 uv = fragCoord / iResolution.xy;\n    vec3 col = 0.5 + 0.5 * cos(iTime + uv.xyx + vec3(0, 2, 4));\n    fragColor = vec4(col, 1.0);\n}\n",
        });
        ed.SetMarkers([{ Line: 5, Kind: "warning", Text: "A demo warning" }]);
        ed.E.style.height = "220px";
        ed.E.style.border = "1px solid var(--sw-line)";
        ed.E.dataset.g = "code";
        return section("code", "Code editor", ed.E);
    }

    /** @returns {HTMLElement} Smooth image loading. */
    function images() {
        const box = el("div.g-imgs");
        const names = ["user", "users", "home", "settings", "chart", "table", "server", "world", "brain", "image"];
        /** (Re)load the demo images. */
        const load = () => {
            box.replaceChildren();
            names.forEach((n, i) => {
                const img = Sw.CreateImg({ FadeIn: 400, Channel: "gallery," + 80 });
                img.alt = n;
                img.src = "../icons/" + n + ".svg?" + Date.now();
                box.append(img);
            });
            const bad = Sw.CreateImg({ ShowError: true });
            bad.alt = "Broken image";
            bad.src = "data:image/png;base64,AAAA";
            box.append(el("figure.g-fail", null, bad, el("figcaption.sw-muted", { text: "(demo of a failed load)" })));
        };
        load();
        return section("images", "Smooth image loading", el("div.sw-muted", { text: "Images fade in when loaded, a fade channel staggers them." }), box,
            row(null, new Sw.Button({ Text: "Reload images", Icon: "refresh", OnClick: load })));
    }

    // ------------------------------------------------------------------ main

    Sw.Page.Main(async () => {
        document.body.append(header(), intro());
        const cols = new Sw.Columns({ MinWidth: 360 });
        cols.Add(brand(), buttons(), uxControls(), uxParity(), inputs(), toggles(), sliders(), selects(), progress(), menus(), dialogs(), overlays(), layouts(), props(), images(), code());
        document.body.append(cols.E, el("div", { style: { marginTop: "16px" } }, cells()), el("div", { style: { marginTop: "16px" } }, icons()));
        Sw.Keys.Add("?", showKeys, { Title: "Show the keyboard shortcuts" });
        Sw.Keys.Add("Mod+K", () => Sw.Menu.Show(null, demoMenu()), { Title: "Open the demo menu" });
        Sw.Keys.Add("Alt+T", () => Sw.Theme.Set(Sw.Theme.Get() === "light" ? "dark" : "light"), { Title: "Toggle light / dark" });
        Sw.NoContextMenu();
        window.SwGalleryReady = true;
    }, { Loading: { Text: "Loading the gallery", Opaque: true } });
})();
