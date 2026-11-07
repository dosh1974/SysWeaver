/**
 * @file explore/explore.js - the generated end point listing page (`<any folder>/explore`).
 *
 * Not a static page: ExploreHttpServerModule serves the template srcData/index.html (placeholders `$(NAME)`, it can't change) which loads
 * common/common.js (the legacy bundle = Sw core + ui + compat), this file, common/theme.css and explore/explore.css, and calls the global
 * `Build(data)` from `<body onload>` with the inlined ExploreData. The code only uses Sw (part of common.js) and defines one global: `Build`.
 *
 * ExploreData: `{FolderSuffix, FolderIcon, VirtualFolderIcon, ApiIcon, IconBase, ExtIconBase, MimeIconBase, BaseUrl, LocalUrl, Items}`;
 * ExploreItem: `{Name, Method, Type (0 Unknown, 1 Folder, 2 File, 3 Api, 4 FileUpload,
 * 5 Builtin: an API like end point of the web server itself, outside the API module: login, logout, basic_auth, serverTime ...),ClientCacheDuration, RequestCacheDuration, PerSession,
 * CompPreference, PreCompressed, Auth, Location, Size, LastModified, Mime}`.
 * Storage: `localStorage["ExploreSettings"] = {Order: number[] (column id + 1, negative = descending), Cols: number[] (column ids)}`.
 * Input: sort with a click / tap / Enter on a column title (buttons), links are real links (Enter, middle click, a click with a modifier key
 * copies the url). Narrow screens (max-width 600px) and touch screens up to 1000px (phones in landscape) show cards like the data tables: the column titles are replaced by a card tool
 * bar ("Sort: <column>" menu button; the "Filter by name" box is shown on every screen), and empty values ("" or "-") are left out of the cards.
 * Styled by explore/explore.css.
 */

(() => {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /** Settings key (unchanged). */
    const SettingsKey = "ExploreSettings";

    /** Default column order (column ids). */
    const DefaultCols = [0, 1, 2, 5, 3, 6, 7, 4, 8, 9];

    /**
     * Thousands separated by spaces ("1 234 567").
     * @param {number|string} v The value.
     * @returns {string} The text.
     */
    function intText(v) {
        return String(v).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
    }

    /**
     * Lower case extension of a name ("" without one, and for folders / uploads when a type is given).
     * @param {string} n The name.
     * @param {number} [t] The item type.
     * @returns {string} The extension.
     */
    function fileExt(n, t) {
        if (t && (t < 2 || t > 3))
            return "";
        const i = n.lastIndexOf(".");
        return i < 0 ? "" : n.substring(i + 1).toLowerCase();
    }

    /**
     * Compare two values for sorting, empty values first.
     * @param {*} a Value a.
     * @param {*} b Value b.
     * @param {boolean} [emptyFirst] Falsy values sort before everything.
     * @returns {number} The order.
     */
    function cmp(a, b, emptyFirst) {
        if (emptyFirst) {
            if (!a)
                return b ? -1 : 0;
            if (!b)
                return 1;
        }
        return a < b ? -1 : a > b ? 1 : 0;
    }

    /**
     * Load the settings (missing / invalid parts get the defaults).
     * @returns {{Order: number[], Cols: number[]}} The settings.
     */
    function getSettings() {
        const s = Sw.Storage.Local.GetJson(SettingsKey, null) || {};
        if (!Array.isArray(s.Order))
            s.Order = [1, 2];
        if (!Array.isArray(s.Cols) || s.Cols.length !== 10)
            s.Cols = DefaultCols.slice();
        return s;
    }

    /**
     * Fill a size cell: strings as is, null "-" (unknown), numbers with a KB / MB / GB tooltip (the original units) and a copy action (a click copies, as the original; Sw.Clipboard.CopyOnClick).
     * @param {HTMLElement} c The cell.
     * @param {number|string|null} size The size.
     */
    function setSize(c, size) {
        if (typeof size === "string") {
            c.textContent = size;
            c.classList.add("Dimmed");
            return;
        }
        if (size === null || size === undefined) {
            c.textContent = "-";
            c.classList.add("Dimmed");
            c.title = _TF("Size is not known, this is probably a dynamically generated resource", "A tool tip description of a text field containing the size of a web resources with an unknown size");
            return;
        }
        c.textContent = intText(size);
        let t = Sw.Clipboard.CopyOnClick(c, size, { Keyboard: true });
        const kb = (size + 512) >> 10;
        if (kb > 0) {
            t += "\n\n" + _T("{0} KB", intText(kb), "Tool tip of the size column of a web resource listing: the size in kilobytes. {0} is the formatted whole number, ex: 12");
            const mb = Math.round(size / 1048576);
            if (mb > 0)
                t += "\n" + _T("{0} MB", intText(mb), "Tool tip of the size column of a web resource listing: the size in megabytes. {0} is the formatted whole number, ex: 12");
            const gb = Math.round(size / 1073741824);
            if (gb > 0)
                t += "\n" + _T("{0} GB", intText(gb), "Tool tip of the size column of a web resource listing: the size in gigabytes. {0} is the formatted whole number, ex: 12");
        }
        c.title = t;
    }

    /**
     * Fill a cache duration cell.
     * @param {HTMLElement} c The cell.
     * @param {number} d Seconds.
     */
    function setCache(c, d) {
        if (d <= 0) {
            c.classList.add("Dimmed");
            c.textContent = "-";
            c.title = _TF("Not cached", "A tool tip descriptions for a text describing that a web resource will never be cached");
        }
        else if (d > 1500000000) {
            //  (as the original)
            c.textContent = "∞";
            c.title = _TF("Infinite (until the service is restarted)", "A tool tip description of a text that let the user know that a web resource will be cached until the service is restarted");
        }
        else {
            //  (the elapsed time text of the TimeSpan cells: "30 seconds", "60 minutes", "24 hours", "7 days")
            c.textContent = Sw.Format.TimeSpan(d);
            c.title = _T("Cached for {0} seconds", d, "A tool tip descriptions for a text describing how long a web resource will be cached in seconds.{0} will be replaced with the number of seconds");
        }
    }

    /**
     * The display title of a column (the type icon column has no title: its tool tip name, capitalized).
     * @param {object} c The column.
     * @returns {string} The title.
     */
    function colTitle(c) {
        return c.Title || (c.Name.charAt(0).toUpperCase() + c.Name.substring(1));
    }

    /**
     * A small pill.
     * @param {string} text The text.
     * @param {string} [title] The tooltip.
     * @returns {HTMLSpanElement} The pill.
     */
    function pill(text, title) {
        return el("span.ExplorePill", { text, title: title || null });
    }

    /**
     * The columns: id, header class, sort compare, tooltip name / title / description, cell renderer (cell, item, row info).
     * @type {Array<object>}
     */
    const Columns = [
        {
            Cls: "ExploreIcon",
            Name: _TF("type", "A table column name of a column with web resource types, used in tool tips"),
            Title: null,
            Desc: _TF("The type of the web resource.", "A table column description of a column with web resource types, used in tool tips"),
            Cmp: (a, b) => a.Type - b.Type,
            Render: (c, it, r) => {
                if (r.Img)
                    c.append(Sw.Cells.CreateLink(r.Link, r.Img, r.Target, (it.Mime ?? it.Location) + (r.Edit ? r.EditTip : r.OpenTip), true, { UseEdit: r.Edit }));
            },
        },
        {
            Cls: "ExploreName",
            Name: _TF("name", "A table column name of a column with web resource names, used in tool tips"),
            Title: _TF("Name", "A table column title of a column with web resource names, used as column title text"),
            Desc: _TF("Name of the web resource.", "A table column name of a column with web resource names, used in tool tips"),
            Cmp: (a, b) => cmp(a.Name.toLowerCase(), b.Name.toLowerCase()),
            Render: (c, it, r) => c.append(Sw.Cells.CreateLink(r.Link, it.Name, r.Target, it.Location + r.OpenTip, false, { UseView: r.Edit })),
        },
        {
            Cls: "ExploreSize",
            Name: _TF("size", "A table column name of a column with web resource byte sizes, used in tool tips"),
            Title: _TF("Size", "A table column title of a column with web resource byte sizes, used as column title text"),
            Desc: _TF("Size of the web resource if applicable", "A table column name of a column with web resource byte sizes, used in tool tips"),
            Cmp: (a, b) => (a.Size || 0) - (b.Size || 0),
            Render: (c, it, r) => {
                setSize(c, r.Size);
                if (r.SizeTip)
                    c.title = r.SizeTip;
            },
        },
        {
            Cls: "ExploreExt",
            Name: _TF("web resource extension", "A table column name of a column with web resource file extensions, used in tool tips"),
            Title: _TF("Ext", "A table column title of a column with web resource file extensions, used as column title text"),
            Desc: _TF("Web resource extension if applicable", "A table column name of a column with web resource file extensions, used in tool tips"),
            Cmp: (a, b) => cmp(fileExt(a.Name, a.Type), fileExt(b.Name, b.Type)),
            Render: (c, it, r) => {
                if (r.IsRes) {
                    c.textContent = r.Ext;
                    if (it.Mime)
                        c.title = it.Mime;
                }
            },
        },
        {
            Cls: "ExploreAuth",
            Name: _TF("required authentication", "A table column name of a column with required web resource authentication tokens, used in tool tips"),
            Title: _TF("Auth", "A table column title of a column with required web resource authentication tokens, used as column title text"),
            Desc: _TF("Required authentication and/or any required authentication tokens.", "A table column name of a column with required web resource authentication tokens, used in tool tips"),
            Cmp: (a, b) => cmp(a.Auth, b.Auth, true),
            Render: (c, it, r) => {
                if (!r.IsRes)
                    return;
                //  The tags of the data tables' auth columns (Sw.Cells.AuthTag, C# TableDataAuth): the accent "Open" tag, the "Any logged
                //  in user" tag or one tag per token
                const wd = el("div");
                const a = it.Auth;
                const tokens = (a ?? "").split(",").map(x => x.trim()).filter(x => x);
                if (tokens.length) {
                    tokens.forEach(x => wd.append(el("span.sw-tag", { text: x })));
                    c.title = _TF("Client must have authorization to access this resource and have one of the following authorization tokens:", "A tool tip description for a list of authorization tokens that are required to user a web resource, a list of token will follow") + "\n" + tokens.join("\n");
                }
                else {
                    wd.append(Sw.Cells.AuthTag(a === undefined ? null : a));
                    c.title = a === null || a === undefined
                        ? _TF("Anyone can access this resource.", "A tool tip description explaining that a web resource can be accessed at all times")
                        : _TF("Client must have authorization to access this resource.", "A tool tip description explaining that a web resource requires an authorized user to be accessed");
                }
                c.append(wd);
            },
        },
        {
            Cls: "ExploreTime",
            Name: _TF("last modified time stamp", "A table column name of a column with the last modified web resource time stamps, used in tool tips"),
            Title: _TF("Time", "A table column title of a column with the last modified web resource time stamps, used as column title text"),
            Desc: _TF("Last modified time stamp.", "A table column name of a column with the last modified web resource time stamps, used in tool tips"),
            Cmp: (a, b) => cmp(new Date(a.LastModified), new Date(b.LastModified)),
            Render: (c, it) => {
                Sw.Cells.DateTime(c, it.LastModified, null);
                Sw.KeyClick(c);
            },
        },
        {
            Cls: "ExploreClientCache",
            Name: _TF("client cache duration", "A table column name of a column with web resource client caching, used in tool tips"),
            Title: _TF("Client", "A table column title of a column with web resource client caching, used as column title text"),
            Desc: _TF("The duration is seconds that web clients are instructed to keep the data.", "A table column name of a column with web resource client caching, used in tool tips"),
            Cmp: (a, b) => cmp(a.ClientCacheDuration, b.ClientCacheDuration),
            Render: (c, it) => setCache(c, it.ClientCacheDuration),
        },
        {
            Cls: "ExploreRequestCache",
            Name: _TF("server cache duration", "A table column name of a column with web resource server caching, used in tool tips"),
            Title: _TF("Server", "A table column title of a column with web resource server caching, used as column title text"),
            Desc: _TF("The duration in seconds that the data is cached by the service.", "A table column name of a column with web resource server caching, used in tool tips"),
            Cmp: (a, b) => cmp(a.RequestCacheDuration, b.RequestCacheDuration),
            Render: (c, it) => setCache(c, it.RequestCacheDuration),
        },
        {
            Cls: "ExploreCompPreference",
            Name: _TF("compression preference", "A table column name of a column with on-the-fly web resource compression preference, used in tool tips"),
            Title: _TF("Compression", "A table column title of a column with on-the-fly web resource compression preference, used as column title text"),
            Desc: _TF("The on-the-fly compression to apply, if any.", "A table column name of a column with on-the-fly web resource compression preference, used in tool tips"),
            Cmp: (a, b) => cmp(a.CompPreference, b.CompPreference, true),
            Render: (c, it) => {
                if (!it.CompPreference)
                    return;
                const wd = el("div");
                it.CompPreference.split(",").map(x => x.trim()).filter(x => x).forEach(m => {
                    const kv = m.split(":");
                    wd.append(pill(kv[0], _T("{1} quality using {0}-compression.", kv[0], kv[1] ?? "", "A tool tip description describing the comporession method and quality used by a web resource.{0} is the name of the compession method, ex: Brotli, Zip, Deflate.{1} is the compression quality, ex: Fast, Normal, Best")));
                });
                c.append(wd);
            },
        },
        {
            Cls: "ExplorePreCompressed",
            Name: _TF("stored compression method", "A table column name of a column with the compression method used to store web resources, used in tool tips"),
            Title: _TF("Stored", "A table column title of a column with the compression method used to store web resources, used as column title text"),
            Desc: _TF("The storage compression method used (if stored compressed).", "A table column name of a column with the compression method used to store web resources, used in tool tips"),
            Cmp: (a, b) => cmp(a.PreCompressed, b.PreCompressed, true),
            Render: (c, it) => {
                c.textContent = it.PreCompressed ?? "";
            },
        },
    ];

    /**
     * The explore listing.
     */
    class Explore {
        /**
         * @param {object} data The ExploreData.
         */
        constructor(data) {
            /** @type {object} The ExploreData. */
            this.Data = data;
            /** @type {{Order: number[], Cols: number[]}} The settings. */
            this.Set = getSettings();
            const q = location.search;
            /** @type {string} The query string kept on every link. */
            this.Query = q && q.length > 1 ? q : "";
            /** @type {boolean} Links open in a new tab only when embedded (the app shell). */
            this.Embedded = window.parent !== window;
            /** @type {HTMLElement} The table wrapper. */
            this.Wrap = el("div.ExploreWrap");
        }

        /**
         * Build the page (Design v2, ui/page.css): main.sw-page-dash.ExplorePage > a compact page header (title "Explore", the folder path
         * as a breadcrumb under it, the item count as status), the card tool bar and the table.
         */
        Build() {
            const n = (this.Data.Items || []).length;
            //  (the document title stays the server's, 'Exploring "<folder>"', like the original listing; the page header only shows "Explore")
            const docTitle = document.title;
            const header = Sw.PageHeader({
                Title: _TF("Explore", "Title of the web resource listing page (the files, folders and APIs of a web folder)"),
                Compact: true,
                Status: Sw.Badge(n === 1
                    ? _TF("1 item", "Number of items in the explored web folder (exactly one), shown next to the page title")
                    : _T("{0} items", n, "Number of items in the explored web folder, shown next to the page title. {0} is the number")),
            });
            //  The folder path replaces the description line (it is the page's context, and its links navigate)
            header.DescE.replaceWith(this.BuildNavigator());
            document.body.replaceChildren(el("main.sw-page-dash.ExplorePage", null, header.E, this.BuildCardBar(), this.Wrap));
            if (docTitle && !(typeof Sw.MenuTitle === "function" && Sw.MenuTitle()))
                document.title = docTitle;
            //  Alt+click on a link copies its address (as the original listing did on a modifier click; Ctrl / Shift + click keep the
            //  browser's new tab / window, and the browser's own link menu has "Copy link address")
            if (Sw.Clipboard.AltClickLinks)
                Sw.Clipboard.AltClickLinks(this.Wrap);
            this.BuildTable();
        }

        /**
         * The card tool bar (shown on narrow screens only, explore.css, where the column titles are hidden): a "Sort" button (menu of
         * the columns and the direction, like the data table cards) and a "Filter by name" box.
         * @returns {HTMLElement} The bar.
         */
        BuildCardBar() {
            const t = this;
            /** @type {string} The name filter (lower case, "" = all). */
            t.FilterText = "";
            /** @type {object} The Sort button. */
            t.SortB = new Sw.Button({ Icon: "IconAscending", Text: "", Kind: "ghost", Class: "ExploreCardBtn", OnClick: b => t.SortMenu(b.E) });
            t.SortB.E.setAttribute("aria-haspopup", "menu");
            const f = v => t.Filter(v);
            /** @type {object} The name filter box. */
            t.FilterBox = new Sw.TextBox({
                Placeholder: _TF("Filter by name..", "Placeholder of the box on the web resource listing (phones) that shows only the items whose name contains the text"),
                Title: _TF("Show only the items whose name contains this text", "Tool tip of the name filter box on the web resource listing"),
                Icon: "filter", Clear: true, OnInput: f, OnChange: f,
            });
            t.FilterBox.E.classList.add("ExploreFilter");
            t.UpdateCardBar();
            return el("div.ExploreCardBar", { attrs: { role: "toolbar", "aria-label": _TF("Sort and filter", "Accessible name of the sort and filter bar of the web resource listing on phones") } }, t.SortB.E, t.FilterBox.E);
        }

        /** Show the current sort column and direction on the Sort button ("Sort: Name"). */
        UpdateCardBar() {
            if (!this.SortB)
                return;
            const o = this.Set.Order[0] || 0;
            const col = Columns[Math.abs(o) - 1];
            const name = col && colTitle(col);
            this.SortB.SetContent(name
                ? _T("Sort: {0}", name, "Text of the sort button of a list on phones, {0} is the title of the column the list is sorted by")
                : _TF("Sort", "Text of the sort button of a list on phones (not sorted)"), o < 0 ? "IconDescending" : "IconAscending");
        }

        /**
         * The sort menu of the card tool bar: the columns (the current one checked) and the direction.
         * @param {HTMLElement} at The button.
         */
        SortMenu(at) {
            const t = this;
            const o = t.Set.Order[0] || 0;
            const curId = Math.abs(o) - 1;
            const cur = Columns[curId] ? curId : -1;
            const head = _TF("Sort by", "Heading of the sort menu of a data table on phones (a list of the columns follows)");
            const items = [{ Title: head }];
            t.Set.Cols.filter(id => Columns[id]).forEach(id => {
                const c = Columns[id];
                items.push({
                    Text: colTitle(c), Title: c.Desc, Radio: true, Checked: id === cur,
                    Hint: id === cur ? (o < 0 ? "↓" : "↑") : null,
                    OnClick: () => t.Sort(id),
                });
            });
            items.push("-",
                {
                    Text: _TF("Ascending (A to Z, 1 to 9)", "Menu item of the sort menu of a data table on phones that sorts the current column in ascending order"),
                    Icon: "IconAscending", Radio: true, Checked: cur >= 0 && o > 0, Disabled: cur < 0,
                    OnClick: () => t.Sort(cur, false),
                },
                {
                    Text: _TF("Descending (Z to A, 9 to 1)", "Menu item of the sort menu of a data table on phones that sorts the current column in descending order"),
                    Icon: "IconDescending", Radio: true, Checked: cur >= 0 && o < 0, Disabled: cur < 0,
                    OnClick: () => t.Sort(cur, true),
                });
            Sw.Menu.Show(at, items, { Label: head });
        }

        /**
         * Show only the rows whose name contains a text.
         * @param {string} v The text ("" = all).
         */
        Filter(v) {
            this.FilterText = (v || "").trim().toLowerCase();
            this.ApplyFilter();
        }

        /** Hide the rows not matching the name filter; nothing left (or an empty folder) shows an empty state under the table. */
        ApplyFilter() {
            const f = this.FilterText;
            const body = this.Wrap.querySelector("tbody");
            let n = 0;
            if (body)
                for (const tr of body.rows) {
                    tr.hidden = !!f && !tr.SwName.includes(f);
                    n += tr.hidden ? 0 : 1;
                }
            if (!this.Empty) {
                /** @type {object} The empty state (Sw.StateView), after the table. */
                this.Empty = Sw.StateView({});
                this.Wrap.after(this.Empty.E);
            }
            this.Empty.Set(f
                ? { Icon: "search", Title: _TF("No items match", "Title of the empty state of the web resource listing when no item name contains the filter text"), Text: _TF("Try another part of the name.", "Text of the empty state of the web resource listing when no item name contains the filter text") }
                : { Title: _TF("This folder is empty", "Title of the empty state of the web resource listing of a folder without items") });
            this.Empty.E.hidden = n > 0;
        }

        /**
         * The path bar: one link per folder of LocalUrl ("ROOT" first); parents are explored, the last opens the folder itself.
         * @returns {HTMLElement} The navigator.
         */
        BuildNavigator() {
            const d = this.Data;
            const nav = el("nav.ExploreNavigator", { attrs: { "aria-label": _TF("Folder path", "Accessible name of the bar with the folders of the explored path") } });
            const parts = d.LocalUrl.split("/");
            const l = parts.length;
            const explore = d.FolderSuffix.substring(1);
            const base = d.BaseUrl.split("/");
            for (let i = 0; i < l; ++i) {
                const name = i === 0 ? _TF("ROOT", "Text shown as the web root in a navigation bar") : parts[i - 1];
                const depth = l - i - 1;
                const folder = base.slice(0, base.length - depth - 1).join("/") + "/";
                const a = i + 1 >= l
                    ? Sw.Cells.CreateLink("./" + this.Query, name, "_blank", _T('Open "{0}" in a new tab', folder, "Tool tip description of a text field that when clicked will open a web resource in a new browser tab.{0} is replaced with the url to the web resource"))
                    : Sw.Cells.CreateLink("../".repeat(depth) + explore + this.Query, name, "_self", _T('Explore the folder "{0}"', folder, "Tool tip description of a text field that when clicked will explore a web folder.{0} is replaced with the url to the web folder"));
                a.classList.add("Path");
                nav.append(a);
            }
            return nav;
        }

        /**
         * Sort click: the column becomes the primary sort key; clicking the primary column again flips its direction.
         * @param {number} id Column id.
         * @param {boolean} [setDesc] The direction to use (the card sort menu), default: toggle as described.
         */
        Sort(id, setDesc) {
            const key = id + 1;
            const o = this.Set.Order;
            const cur = o[0] === key || o[0] === -key;
            //  (like the original: the primary column flips; another column keeps the direction it has in the order, else ascending)
            const desc = typeof setDesc === "boolean" ? setDesc : cur ? o[0] > 0 : o.indexOf(-key) >= 0;
            this.Set.Order = [desc ? -key : key].concat(o.filter(x => x !== key && x !== -key));
            Sw.Storage.Local.SetJson(SettingsKey, this.Set);
            this.BuildTable(id);
        }

        /**
         * Sort the items by the settings.
         */
        SortItems() {
            const order = this.Set.Order;
            this.Data.Items.sort((a, b) => {
                for (const ord of order) {
                    const col = Columns[Math.abs(ord) - 1];
                    if (!col)
                        continue;
                    const d = col.Cmp(a, b);
                    if (d)
                        return ord < 0 ? -d : d;
                }
                return 0;
            });
        }

        /**
         * A header cell with a sort button.
         * @param {number} id Column id.
         * @returns {HTMLTableCellElement} The cell.
         */
        Header(id) {
            const col = Columns[id];
            const key = id + 1;
            const order = this.Set.Order;
            let prio = 0, desc = false;
            order.some((o, i) => {
                if (Math.abs(o) !== key)
                    return false;
                prio = i + 1;
                desc = o < 0;
                return true;
            });
            const current = prio === 1;
            const nextDesc = current ? !desc : false;
            let tip = nextDesc
                ? (current
                    ? _T("Sort by {0} in descending order", col.Name, "A tool tip description to a button on a table column that when clicked will sort the table in descending based on that column.{0} is replaced by the title of the column")
                    : _T("Sort by {0} in descending order as priority #1", col.Name, "A tool tip description to a button on a table column that when clicked will sort the table in descending based on that column.{0} is replaced by the title of the column"))
                : (current || prio <= 0
                    ? _T("Sort by {0} in ascending order", col.Name, "A tool tip description to a button on a table column that when clicked will sort the table in ascending based on that column.{0} is replaced by the title of the column")
                    : _T("Sort by {0} in ascending order as priority #1", col.Name, "A tool tip description to a button on a table column that when clicked will sort the table in ascending based on that column.{0} is replaced by the title of the column"));
            if (prio > 0)
                tip += "\n" + _T("Currently at priority #{0}.", prio, "A tool tip description to a table column header, that indicates the sorting priority of that column.{0} is replaced with the priority number, 1, 2, 3 and so on");
            const icon = Sw.IconClass(desc ? "IconDescending" : "IconAscending", { Class: current ? null : "Dimmed" });
            const b = el("button.ExploreSort", { type: "button", title: col.Desc + "\n\n" + tip, attrs: { "aria-label": Sw.ActionName(tip) } }, icon, col.Title ? el("span", { text: col.Title }) : null);
            b.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                this.Sort(id);
            });
            return el("th", { class: col.Cls + "H", attrs: { scope: "col", "aria-sort": current ? (desc ? "descending" : "ascending") : null } }, b);
        }

        /**
         * Information about an item used by the cell renderers (link, target, icon, size ...).
         * @param {object} it The item.
         * @returns {object} The row info.
         */
        RowInfo(it) {
            const d = this.Data;
            const q = this.Query;
            const abs = d.BaseUrl + it.Name;
            const r = {
                Link: it.Name + q,
                Img: null,
                Size: "",
                Ext: "",
                IsRes: false,
                Edit: false,
                NewTab: true,
                OpenTip: "\n\n" + _T('Open "{0}"', abs, "A tool tip description on an item that when clicked will open a url.{0} is replaced with the url"),
                EditTip: "\n\n" + _T('View "{0}"', abs, "A tool tip description on an item that when clicked will view the document at an url.{0} is replaced with the url"),
            };
            switch (it.Type) {
                case 0:
                    r.Ext = fileExt(it.Name);
                    r.Img = d.ApiIcon;
                    r.Size = "[???]"; // unknown kind (dynamic handler), as the original
                    r.IsRes = true;
                    r.NewTab = false;
                    break;
                case 1:
                    r.Img = (it.Location || "").startsWith("[Folder]") ? d.FolderIcon : d.VirtualFolderIcon;
                    r.OpenTip = "\n\n" + _T('Explore folder "{0}"', abs, "A tool tip description on an item that when clicked will explore the content of a web folder.{0} is replaced with the url of the web folder");
                    r.Link = it.Name + d.FolderSuffix + q;
                    r.Size = "<DIR>"; // (as the original)
                    r.NewTab = false;
                    break;
                case 2:
                    r.Ext = fileExt(it.Name);
                    r.Img = d.ExtIconBase + (r.Ext === "" ? "bin" : r.Ext) + ".svg";
                    r.Size = it.Size;
                    r.IsRes = true;
                    r.Edit = true;
                    break;
                case 3:
                    r.Ext = fileExt(it.Name);
                    r.Img = d.ApiIcon;
                    r.Link = d.IconBase + "../explore/api.html?q=" + d.LocalUrl + it.Name;
                    r.OpenTip = "\n\n" + _T('Explore the API "{0}"', abs, "A tool tip description on an item that when clicked will explore the API of that url.{0} is replaced with the url of the API");
                    r.Size = "[API]"; // (as the original)
                    r.IsRes = true;
                    r.NewTab = false;
                    break;
                case 4:
                    r.Img = d.ApiIcon;
                    r.Size = "[UP]"; // (as the original)
                    r.IsRes = true;
                    break;
                case 5:
                    //  Built-in end point of the web server (login, logout, basic_auth, serverTime ...): API like, outside the API module
                    r.Ext = fileExt(it.Name);
                    r.Img = d.ApiIcon;
                    r.Size = _TF("Built-in", "Text in the size column of a web resource listing for an end point that the web server handles itself, like an API (ex: login, logout, serverTime; no size)");
                    r.SizeTip = _TF("Handled by the web server itself, like an API call but outside the API module (ex: sign in, sign out, server time). It has no size.", "Tool tip of the size column of a web resource listing for an end point that the web server handles itself (ex: login, logout, serverTime)");
                    r.IsRes = true;
                    r.NewTab = false;
                    break;
            }
            r.Target = r.NewTab && this.Embedded ? "_blank" : "_self";
            return r;
        }

        /**
         * (Re)build the table.
         * @param {number} [focusCol] Column id whose sort button gets the focus back (after a sort).
         */
        BuildTable(focusCol) {
            this.SortItems();
            const cols = this.Set.Cols.filter(id => Columns[id]);
            const head = el("tr.ThemeAccBg.ThemeBaseFg", null, ...cols.map(id => this.Header(id)));
            const body = el("tbody");
            this.Data.Items.forEach(it => {
                const r = this.RowInfo(it);
                const tr = body.insertRow();
                tr.SwName = it.Name.toLowerCase();
                cols.forEach(id => {
                    const c = tr.insertCell();
                    const col = Columns[id];
                    c.className = col.Cls;
                    //  The label of the value in the phone card layout (explore.css)
                    if (col.Title)
                        c.dataset.label = col.Title;
                    col.Render(c, it, r, this.Data);
                    //  Empty values ("" or "-") are left out of the phone cards (explore.css .ExploreBlank)
                    if (id > 1 && !c.firstElementChild && /^-?$/.test(c.textContent.trim()))
                        c.classList.add("ExploreBlank");
                });
            });
            //  SwWeb:allow ux-lst-3 (the folder listing of the srcData template: file links with its own sort buttons, name filter and phone cards; not yet ported to Sw.Table({ Rows }))
            const table = el("table.Explore", null, el("thead", null, head), body);
            this.Wrap.replaceChildren(table);
            this.ApplyFilter();
            this.UpdateCardBar();
            if (focusCol !== undefined) {
                const i = cols.indexOf(focusCol);
                const b = i >= 0 ? head.cells[i].querySelector("button") : null;
                if (b)
                    b.focus();
            }
        }
    }

    /**
     * Entry point (called by the srcData template's inline `exploreMain()` with the inlined ExploreData). Permanent global.
     * @param {object} data The ExploreData of the current folder.
     */
    window.Build = function (data) {
        const done = Sw.Page.Loading();
        try {
            new Explore(data).Build();
        }
        catch (e) {
            console.error(e);
            Sw.Fail(e && e.message ? e.message : String(e), true);
        }
        finally {
            done();
        }
    };
})();