/**
 * @file explore/themepreview.js - page script of explore/themepreview.html, the theme preview of the logo designer (explore/logo.html).
 *
 * Entry: `<body onload="themePreviewMain()">`. A demo of a small app in the current theme: a top bar, a side menu (a current item, a
 * section, a badge), a page header, KPI cards, buttons of every kind, fields (text, select, toggle, slider, segmented), tags (the auth
 * tags of the data tables, Sw.Cells.AuthTag), chips, badges, a progress bar, tabs, a data table (Sw.Table with local rows: explore/table.js,
 * an auth column with the "Auth" tags format) and a text with a link. Nothing on it
 * sends a request (the controls only change their own state).
 * The logo designer opens it in a dialog with the theme of a seed:
 * `themepreview.html?seed=123&useTheme=application@../Api/debug/explore/PreviewTheme.css%3F123` (useTheme forces the theme of this window
 * without storing it; "application@<css>" links the seed's [data-theme="application"] colours after common/theme.css, so the page looks
 * as the app would with that AppSeed). Uses Sw (sw/core.js + sw/ui.js); styled by explore/themepreview.css.
 */

(() => {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /**
     * A side menu row of the demo.
     * @param {string} icon The line icon name.
     * @param {string} text The text.
     * @param {boolean} [sel] The current item.
     * @param {string} [badge] A count at the end.
     * @returns {HTMLElement} The row.
     */
    function menuRow(icon, text, sel, badge) {
        return el("div.tp-mrow", { class: sel ? "tp-sel" : null }, Sw.Icon(icon, "tp-micon"), el("span.tp-mtext", { text }),
            badge ? el("span.tp-mbadge", { text: badge }) : null);
    }

    /**
     * A KPI card of the demo.
     * @param {string} icon The line icon name.
     * @param {string} label The label.
     * @param {string} value The value.
     * @param {string} note The note under the value.
     * @returns {HTMLElement} The card.
     */
    function kpi(icon, label, value, note) {
        return el("section.sw-card.tp-kpi", null,
            el("div.tp-kpi-label", null, Sw.Icon(icon), el("span", { text: label })),
            el("div.tp-kpi-value", { text: value }),
            el("div.tp-kpi-note", { text: note }));
    }

    /**
     * A card with a title.
     * @param {string} title The title.
     * @param {...(Node|null)} body The content.
     * @returns {HTMLElement} The card.
     */
    function card(title, ...body) {
        return el("section.sw-card.tp-card", null,
            el("div.sw-card-head", null, el("h2.tp-head", { text: title })),
            el("div.sw-card-body", null, ...body));
    }

    /**
     * A row of controls.
     * @param {...(Node|object|null)} items Elements or Sw controls (with E).
     * @returns {HTMLElement} The row.
     */
    function row(...items) {
        return el("div.tp-row", null, ...items.filter(x => x).map(x => x.E || x));
    }

    /**
     * Page entry of explore/themepreview.html.
     */
    window.themePreviewMain = async function () {
        let seed = Sw.Url.Get("seed");
        let hasSeed = seed !== null && seed !== undefined && seed !== "";
        /**
         * The url of a generated mark for the seed: the server's debug variants (icon_debug.svg, logo_debug.svg, banner_debug.svg) with
         * the seed, else the app's own files.
         * @param {string} name "icon", "logo" or "banner".
         * @returns {string} The url.
         */
        const markUrl = name => Sw.Url.Root + (hasSeed ? name + "_debug.svg?" + encodeURIComponent(seed) : name + ".svg");
        const marks = [];
        /**
         * A generated mark of the seed (an img, updated by themePreviewSeed).
         * @param {string} name "icon", "logo" or "banner".
         * @param {string} cls The class.
         * @param {string} alt The text alternative.
         * @returns {HTMLImageElement} The image.
         */
        const mark = (name, cls, alt) => {
            const i = el("img", { class: cls, src: markUrl(name), alt, draggable: false });
            i.SwMark = name;
            marks.push(i);
            return i;
        };
        //  Top bar and side menu (the shell's look, drawn with the same tokens)
        const top = el("header.tp-top", null,
            mark("icon", "tp-brand", _TF("App icon", "Accessible name of the app icon in the theme preview")),
            el("span.tp-crumb", { text: _TF("Home", "Breadcrumb text in the top bar of the theme preview") }),
            el("span.tp-crumbsep", { text: "/" }),
            el("span.tp-crumb.tp-crumbcur", { text: _TF("Dashboard", "Current breadcrumb in the top bar of the theme preview") }),
            el("span.sw-push"),
            new Sw.Button({ Icon: "search", Kind: "ghost", Title: _TF("Search", "Tool tip of a demo button in the theme preview") }).E,
            new Sw.Button({ Icon: "bell", Kind: "ghost", Title: _TF("Notifications", "Tool tip of a demo button in the theme preview") }).E,
            new Sw.Button({ Icon: "settings", Kind: "ghost", Title: _TF("Settings", "Tool tip of a demo button in the theme preview") }).E,
            el("span.tp-avatar", { text: "D", attrs: { "aria-hidden": "true" } }));
        const side = el("nav.tp-side", { attrs: { "aria-label": _TF("Demo menu", "Accessible name of the demo menu of the theme preview") } },
            el("div.tp-filter", null, Sw.Icon("search"), el("span", { text: _TF("Filter the menu", "Placeholder of the demo menu filter in the theme preview") })),
            menuRow("home", _TF("Home", "Demo menu item in the theme preview"), true),
            menuRow("server", _TF("Server", "Demo menu item in the theme preview")),
            menuRow("folder", _TF("Files", "Demo menu item in the theme preview")),
            menuRow("users", _TF("Users", "Demo menu item in the theme preview")),
            el("div.tp-msection", { text: _TF("Monitoring", "Demo menu section in the theme preview") }),
            menuRow("log", _TF("Log file", "Demo menu item in the theme preview")),
            menuRow("alert", _TF("Problems", "Demo menu item in the theme preview"), false, "3"),
            menuRow("chartLine", _TF("Performance", "Demo menu item in the theme preview")),
            menuRow("clock", _TF("Scheduled tasks", "Demo menu item in the theme preview")));

        //  The page: header, KPI cards, controls, tags, table
        const describe = () => hasSeed
            ? _T("A demo of the app with the theme of the seed {0}.", seed, "Description of the theme preview, {0} is the logo seed whose theme is shown")
            : _TF("A demo of the app in the current theme.", "Description of the theme preview without a seed");
        const header = Sw.PageHeader({
            Title: _TF("Dashboard", "Title of the demo page in the theme preview"),
            Description: describe(),
        });
        //  The generated icon, logo and banner of the seed, on the page (they don't have to look like an app page)
        const brand = el("section.sw-card.tp-marks", { attrs: { "aria-label": _TF("Generated marks", "Accessible name of the row with the generated icon, logo and banner in the theme preview") } },
            mark("icon", "tp-mark-icon", _TF("Generated app icon", "Text alternative of the generated app icon in the theme preview")),
            mark("logo", "tp-mark-logo", _TF("Generated app logo", "Text alternative of the generated app logo in the theme preview")),
            mark("banner", "tp-mark-banner", _TF("Generated app banner", "Text alternative of the generated app banner in the theme preview")));

        /**
         * Show another seed without reloading (the logo designer's Prev / Next): its application theme (the stylesheet of the seed
         * linked last, so it wins over the ones of earlier seeds, then the theme engine applies it and derives its colours), its
         * marks and the description.
         * @param {string|number} s The seed.
         */
        window.themePreviewSeed = s => {
            seed = String(s);
            hasSeed = true;
            const css = "../Api/debug/explore/PreviewTheme.css?" + seed;
            const abs = new URL(css, Sw.Url.Common).href;
            const old = Array.from(document.querySelectorAll("link[rel=stylesheet]")).find(l => l.href === abs);
            if (old)
                document.head.appendChild(old);
            Sw.Session.RequestTheme("application@" + css);
            marks.forEach(i => i.src = markUrl(i.SwMark));
            header.SetDescription(describe());
        };
        const kpis = el("div.sw-grid.tp-kpis", null,
            kpi("server", _TF("Services", "Demo KPI label in the theme preview"), "12", _TF("11 running", "Demo KPI note in the theme preview")),
            kpi("clock", _TF("Uptime", "Demo KPI label in the theme preview"), _TF("4 days", "Demo KPI value in the theme preview"), _TF("Since Monday", "Demo KPI note in the theme preview")),
            kpi("alert", _TF("Problems", "Demo KPI label in the theme preview"), "3", _TF("Last 24 h", "Demo KPI note in the theme preview")),
            kpi("memory", _TF("Memory", "Demo KPI label in the theme preview"), "63 %", _TF("40.2 of 64 Gb", "Demo KPI note in the theme preview")));

        const buttons = card(_TF("Buttons", "Card title in the theme preview"),
            row(new Sw.Button({ Text: _TF("Primary", "Demo button text in the theme preview"), Kind: "primary", Icon: "save" }),
                new Sw.Button({ Text: _TF("Default", "Demo button text in the theme preview") }),
                new Sw.Button({ Text: _TF("Accent", "Demo button text in the theme preview"), Kind: "accent" }),
                new Sw.Button({ Text: _TF("Ghost", "Demo button text in the theme preview"), Kind: "ghost", Icon: "refresh" }),
                new Sw.Button({ Text: _TF("Danger", "Demo button text in the theme preview"), Kind: "danger", Icon: "trash" })),
            row(new Sw.Button({ Text: _TF("Pressed", "Demo button text in the theme preview"), Pressable: true, Pressed: true, Icon: "star" }),
                new Sw.Button({ Text: _TF("Disabled", "Demo button text in the theme preview"), Disabled: _TF("Demo: always disabled", "Reason of a disabled demo button in the theme preview") }),
                new Sw.Button({ Text: _TF("Small", "Demo button text in the theme preview"), Small: true }),
                new Sw.Button({ Icon: "kebab", Title: _TF("More", "Tool tip of a demo button in the theme preview") })));

        const prog = new Sw.ProgressBar({ Label: _TF("Upload", "Label of the demo progress bar in the theme preview") });
        prog.SetValue(7, 10);
        const fields = card(_TF("Fields", "Card title in the theme preview"),
            row(new Sw.TextBox({ Label: _TF("Name", "Label of a demo text box in the theme preview"), Value: _TF("Home server", "Value of a demo text box in the theme preview"), Clear: true }),
                new Sw.TextBox({ Label: _TF("Search", "Label of a demo text box in the theme preview"), Placeholder: _TF("Type to search", "Placeholder of a demo text box in the theme preview") })),
            row(new Sw.Select({
                Title: _TF("Level", "Tool tip of a demo select in the theme preview"),
                Options: [{ Value: 1, Label: _TF("Information", "Demo select option in the theme preview") }, { Value: 2, Label: _TF("Warning", "Demo select option in the theme preview") }, { Value: 3, Label: _TF("Error", "Demo select option in the theme preview") }],
                Value: 2,
            }),
                new Sw.Toggle({ Label: _TF("Enabled", "Label of a demo toggle in the theme preview"), Value: true }),
                new Sw.Toggle({ Label: _TF("Notify me", "Label of a demo toggle in the theme preview") })),
            row(new Sw.Segmented({ AriaLabel: _TF("Range", "Accessible name of a demo segmented control in the theme preview"), Items: [{ Value: 1, Text: _TF("1 h", "Demo segment in the theme preview") }, { Value: 24, Text: _TF("24 h", "Demo segment in the theme preview") }, { Value: 0, Text: _TF("All", "Demo segment in the theme preview") }], Value: 24 }),
                el("div.tp-slider", null, new Sw.Slider({ Min: 0, Max: 1, Value: 0.6, Default: 0.5 }).E)),
            prog.E);

        const tags = card(_TF("Tags and badges", "Card title in the theme preview"),
            //  (role names, as the server sends them) SwWeb:no-tr (demo data)
            row(Sw.Cells.AuthTag(null), Sw.Cells.AuthTag(""), el("span.sw-tag", { text: "ADMIN" }), el("span.sw-tag", { text: "DEBUG" })),
            row(new Sw.Chip({ Text: _TF("Filter", "Demo chip text in the theme preview"), Icon: "filter", Pressable: true }),
                new Sw.Chip({ Text: _TF("Pressed", "Demo chip text in the theme preview"), Pressable: true, Pressed: true }),
                Sw.Badge(_TF("Ok", "Demo badge text in the theme preview"), "ok"), Sw.Badge(_TF("Warning", "Demo badge text in the theme preview"), "warning"),
                Sw.Badge(_TF("Error", "Demo badge text in the theme preview"), "error"), Sw.Badge(_TF("Accent", "Demo badge text in the theme preview"), "accent"),
                Sw.Badge(_TF("Accent 2", "Demo badge text in the theme preview"), "accent2")),
            el("p.tp-text", null,
                el("span", { text: _TF("Body text with a ", "Start of the demo text in the theme preview, followed by a link") }),
                el("a.sw-link", { href: "#", text: _TF("link", "Demo link text in the theme preview"), onclick: ev => ev.preventDefault() }),
                el("span", { text: _TF(" and secondary text below.", "End of the demo text in the theme preview, after a link") })),
            el("p.tp-text2", { text: _TF("Secondary text: notes, descriptions and time stamps.", "Demo secondary text in the theme preview") }));

        //  A real data table (Sw.Table, local rows): the grid, a toggle, byte sizes and the auth tags of the data tables (C# TableDataAuth)
        const tableHost = el("div.tp-tablewrap");
        const tabs = new Sw.Tabs({ Label: _TF("Views", "Accessible name of the demo tabs in the theme preview"), Fill: false });
        tabs.Add({ Title: _TF("Services", "Demo tab title in the theme preview"), Icon: "server", Content: tableHost });
        tabs.Add({ Title: _TF("Details", "Demo tab title in the theme preview"), Content: el("p.tp-text2", { text: _TF("The second tab of the demo.", "Content of the second demo tab in the theme preview") }) });
        const data = card(_TF("Table", "Card title in the theme preview"), tabs.E);
        data.classList.add("tp-wide");

        const main = el("main.sw-page-dash.tp-main", null, header.E, brand, kpis, el("div.sw-grid.tp-grid", null, buttons, fields, tags, data));
        document.body.append(el("div.tp-app", null, top, el("div.tp-body", null, side, el("div.tp-content", null, main))));
        await Sw.Table.Create({
            Element: tableHost, Name: _TF("Services", "Name of the demo table in the theme preview"),
            Cols: [
                { Name: "Service", Title: _TF("Service", "Demo column title in the theme preview") },
                { Name: "Running", Title: _TF("Running", "Demo column title in the theme preview"), Type: "System.Boolean" },
                { Name: "Memory", Title: _TF("Memory", "Demo column title in the theme preview"), Type: "System.Int64", Format: "ByteSize" },
                { Name: "Auth", Title: _TF("Auth", "Demo column title in the theme preview"), Format: "Tags;{^0};{2};{0};true;Auth" },
            ],
            //  (role names, as the server sends them) SwWeb:no-tr (demo data)
            Rows: [
                ["nginx", true, 2254857830, "Ops"],
                ["mysql", true, 6871947673, ""],
                ["backup", false, 0, null],
                ["mail", false, 134217728, "Admin, Debug"],
            ],
        });
    };
})();
