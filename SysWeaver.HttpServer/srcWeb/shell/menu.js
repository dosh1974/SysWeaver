/**
 * @file shell/menu.js - SysWeaver app shell (Sw.Shell), part 1 of 3: the server menu model, item actions, the menu tree and pop-up menus.
 *
 *  - Sw.Shell.MenuTypes: the C# WebMenuItemTypes values (0 Path, 1 Table, 2 Embedded, 3 Link, 4 LinkExternal, 5 Js, 6 Chart).
 *  - Sw.Shell.GetMenu(names): Api/application/GetMenu for comma separated menu names ("Default", "User", "Theme", "User,Theme").
 *  - Sw.Shell.ItemUrl(item): the page url of an embedding item (Table -> explore/table.html?q=../<Data>, Chart -> chart/chart.html?q=../<Data>,
 *    Embedded -> Data), resolved against the site root (absolute).
 *  - Sw.Shell.RunItem(item, ctx, ev): runs the action of an item (function Data, Js: window[Data] or eval, Link, LinkExternal, embedding).
 *  - Sw.Shell.MenuTree: renders a menu as an ARIA tree (the main menu, and the old MainMenu of pop-ups): nesting with persisted collapse
 *    state ("SysWeaver.MenuCollapsed.<menu>.<Id|Name>" = "1"), separators (Id "-"), flags (1 disabled, 2 checked), icons (icon classes or
 *    urls), selection, url -> item map, full keyboard support (arrows, Home / End, Enter / Space, type ahead, * expands all siblings).
 *  - Sw.Shell.ToMenuItems(items, ctx, opts): converts server menu items to Sw.Menu items (pop-up menus: user, settings, the old PopUpMenu).
 *    Picking a theme item closes the menu; a theme item sharing its icon with an earlier theme item gets a distinct one (ThemeIcons).
 *  - MenuTree extras: FindBest(url) (the best matching item of a shown page), Filter(text) (the shell's menu filter box), SetRail(bool) (icon only rail: rows get their name as tool tip and
 *    long press hint), DefaultCollapsed(item) option (folders collapsed until the user expands them, ex: "Debug"), SetAllExpanded(bool)
 *    (the shell's "Collapse all" / "Expand all" menu tools), the Sections option (Design v2: top level folders render as section labels,
 *    their children at the first indent; in the rail an open section is a hairline and a collapsed one its chevron, in the same row box:
 *    the rail keeps the expand state, so no row moves between the rail and the full menu), Ancestors(entry) (the
 *    folders above an entry: the shell's breadcrumb).
 *
 * Classic script (IIFE, "use strict"), needs sw/core.js and sw/ui.js. Nothing runs at load time except the namespace setup.
 * Documentation: shell/README.md.
 */
(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const Shell = Sw.Shell = Sw.Shell || {};
    //  Load once: this file is part of sw/shell.js, app/application.js and sw/compat.js, a page may load more than one of them
    if (Shell.MenuTree)
        return;

    /** The C# WebMenuItemTypes values. */
    const MenuTypes = Object.freeze({
        Path: 0,
        Table: 1,
        Embedded: 2,
        Link: 3,
        LinkExternal: 4,
        Js: 5,
        Chart: 6,
    });

    /** Menu item flag bits (C# WebMenuItemFlags). */
    const MenuFlags = Object.freeze({
        Disabled: 1,
        Checked: 2,
    });

    /**
     * Request server menus.
     * @param {string} names Comma separated menu names ("Default", "User", "Theme" ...).
     * @returns {Promise<{RootUri: string, Items: Array<object>, Name: string}>} The menu (Items is never null).
     */
    async function GetMenu(names) {
        const m = (await Sw.Net.Send(Sw.Url.Root + "Api/application/GetMenu", names)) || {};
        if (!Array.isArray(m.Items))
            m.Items = [];
        return m;
    }

    /**
     * Test if an item is a separator (Id "-").
     * @param {object} item The item.
     * @returns {boolean} True for separators.
     */
    const isSeparator = item => !!item && item.Id === "-";

    /**
     * Get the children of an item.
     * @param {object} item The item.
     * @returns {Array<object>} The children (empty array if none).
     */
    const childrenOf = item => (item && Array.isArray(item.Children)) ? item.Children : [];

    /**
     * Get the page url of an embedding item (types Table, Embedded, Chart), absolute (resolved against the site root, as the server defines
     * menu urls relative to the site root).
     * @param {object} item The item.
     * @returns {string|null} The absolute url, or null if the item doesn't embed a page.
     */
    function ItemUrl(item) {
        if (!item || typeof item.Data !== "string" || isSeparator(item))
            return null;
        let u = null;
        switch (item.Type) {
            case MenuTypes.Table: u = "explore/table.html?q=../" + item.Data; break;
            case MenuTypes.Embedded: u = item.Data; break;
            case MenuTypes.Chart: u = "chart/chart.html?q=../" + item.Data; break;
            default: return null;
        }
        try {
            return new URL(u, Sw.Url.Root).href;
        }
        catch {
            return null;
        }
    }

    /**
     * Normalize an url for comparing a loaded page with a menu item: absolute, no hash, no forced theme parameter ("usetheme").
     * @param {string} url The url.
     * @returns {string} The normalized url ("" if invalid).
     */
    function NormalizeUrl(url) {
        if (!url)
            return "";
        try {
            const u = new URL(url, Sw.Url.Root);
            u.hash = "";
            const drop = [];
            u.searchParams.forEach((v, k) => {
                if (k.toLowerCase() === "usetheme")
                    drop.push(k);
            });
            drop.forEach(k => u.searchParams.delete(k));
            //  Always re-serialize the query, so both sides of a comparison use the same encoding
            u.search = u.searchParams.toString();
            return u.href;
        }
        catch {
            return "";
        }
    }

    /**
     * Test if an item has an action of its own (anything but a plain folder).
     * @param {object} item The item.
     * @returns {boolean} True if clicking the item does something besides expanding.
     */
    function HasAction(item) {
        if (!item || isSeparator(item))
            return false;
        if (typeof item.Data === "function")
            return true;
        switch (item.Type) {
            case MenuTypes.Table:
            case MenuTypes.Embedded:
            case MenuTypes.Chart:
            case MenuTypes.Link:
            case MenuTypes.LinkExternal:
            case MenuTypes.Js:
                return true;
        }
        return false;
    }

    /**
     * Run the action of a menu item.
     * Function Data: awaited as fn(item, ctx.Style, isUserClick), a truthy result hides the menu.
     * Js (5): window[Data](item, ctx.Style, isUserClick) when Data names a global function, else Data is evaluated as a statement in the
     * global scope (the server's theme items: "if(setTheme('dark'));false"); a truthy result hides the menu.
     * Table / Embedded / Chart: ctx.Navigate(url, item, ev) then hide. Link (3): this window navigates to Data. LinkExternal (4): new tab.
     * @param {object} item The item.
     * @param {object} ctx Context: { Navigate(url, item, ev) (embeds a page), Hide() (closes / collapses the menu), Style (passed to Js
     * functions as their second argument, the old MainMenuStyle) }.
     * @param {Event} [ev] The triggering event (undefined for programmatic calls).
     * @returns {Promise<boolean>} True if the menu should be (and was) hidden.
     */
    async function RunItem(item, ctx, ev) {
        ctx = ctx || {};
        const hide = () => {
            if (ctx.Hide)
                ctx.Hide();
            return true;
        };
        const data = item.Data;
        if (typeof data === "function") {
            try {
                if (await data(item, ctx.Style, !!ev))
                    return hide();
            }
            catch (x) {
                Sw.DevWarn("Menu: can't run the function of a menu item, error: " + x, "menu.fn." + x);
            }
            return false;
        }
        switch (item.Type) {
            case MenuTypes.Table:
            case MenuTypes.Embedded:
            case MenuTypes.Chart: {
                const url = ItemUrl(item);
                if (!url)
                    return false;
                if (ctx.Navigate)
                    ctx.Navigate(url, item, ev);
                else
                    Sw.Page.Navigate(url, "_self");
                return hide();
            }
            case MenuTypes.Link:
                hide();
                window.location.href = new URL(data, Sw.Url.Root).href;
                return true;
            case MenuTypes.LinkExternal:
                hide();
                window.open(new URL(data, Sw.Url.Root).href);
                return true;
            case MenuTypes.Js:
                try {
                    const fn = window[data];
                    let r;
                    if (typeof fn === "function")
                        r = await fn(item, ctx.Style, !!ev);
                    else
                        r = (0, eval)(data);
                    if (r)
                        return hide();
                }
                catch (x) {
                    Sw.DevWarn("Menu: can't evaluate the js '" + data + "' of a menu item, error: " + x, "menu.js." + data);
                }
                return false;
        }
        return false;
    }

    /**
     * Find the theme a Js theme item selects (the server's "if(setTheme('dark'));false" / "if(resetTheme());false").
     * @param {object} item The item.
     * @returns {string|null} The theme name, "" for the automatic theme (resetTheme), or null if the item is not a theme item.
     */
    function ThemeOfItem(item) {
        if (!item || item.Type !== MenuTypes.Js || typeof item.Data !== "string")
            return null;
        const m = /setTheme\(\s*['"]([^'"]+)['"]\s*\)/.exec(item.Data);
        if (m)
            return m[1].toLowerCase();
        if (/resetTheme\(\s*\)/.test(item.Data))
            return "";
        return null;
    }

    /**
     * Distinct icons for theme items whose server icon is already used by an earlier theme item of the same menu (older servers gave "Dark"
     * and "Pro" the same moon icon; current servers send IconThemePro, the same display icon). Keys: theme names (lower case, "" = automatic), values: icon urls relative to the site root.
     */
    const ThemeIcons = Object.freeze({
        pro: "icons/display.svg",
        dark: "icons/theme_dark.svg",
        light: "icons/theme_light.svg",
        hacker: "icons/theme_hacker.svg",
        application: "icons/color_palette.svg",
        "": "icons/theme_auto.svg",
    });

    /**
     * The icon of a server menu item as the shell shows it (Design v2, PORTING.md B15): monochrome. A full colour icon url ("...svg*") becomes the
     * mask of the same image, drawn in the menu's icon colour (--sw-text-3; the accent on the current item); the colourful variants are
     * only shown in content (ex: Home tiles, tables). Icon classes and plain urls are masks already.
     * @param {?string} icon The item's IconClass.
     * @returns {?string} The icon for Sw.IconClass / Sw.Menu, null for none.
     */
    function MonoIcon(icon) {
        if (!icon)
            return null;
        const s = String(icon);
        return s.charAt(s.length - 1) === "*" ? (s.substring(0, s.length - 1) || null) : s;
    }

    /**
     * The theme the user stored ("" = automatic).
     * @returns {string} The stored theme name (lower case) or "".
     */
    function storedTheme() {
        const t = Sw.Storage.Get(Sw.Theme.StorageKey || "SysWeaver.Theme", "");
        return (t || "").toLowerCase();
    }

    // ------------------------------------------------------------------ pop-up menu items (Sw.Menu)

    /**
     * Update the radio check marks of the theme items in an open Sw.Menu list after a theme change (the menu stays open).
     * @param {HTMLElement} list The menu list (div.sw-menu).
     */
    function refreshThemeChecks(list) {
        if (!list)
            return;
        const cur = storedTheme();
        list.querySelectorAll(".sw-menu-item").forEach(b => {
            const it = b.SwItem;
            if (!it || it.SwTheme === undefined)
                return;
            const on = it.SwTheme === cur;
            it.Checked = on;
            b.setAttribute("aria-checked", on ? "true" : "false");
            b.classList.toggle("sw-checked", on);
            const c = b.querySelector(".sw-menu-check");
            if (c)
                c.classList.toggle("sw-hidden-icon", !on);
        });
    }

    /**
     * Convert server menu items (WebMenuItem) to Sw.Menu items.
     * Folders become submenus, or (opts.Sections, top level only) a heading followed by their children (one click theme switching).
     * Function / Js items stay open unless they return a truthy value (as before), theme items show the stored theme as a checked radio item.
     * @param {Array<object>} items The items.
     * @param {object} ctx Action context (see RunItem); ctx.Hide is set to close the menu when missing.
     * @param {object} [opts] Options: { Sections (top level folders become sections), Menu (filled with the Sw.Menu handle by ShowMenu) }.
     * @returns {Array<object|string>} Sw.Menu items.
     */
    function ToMenuItems(items, ctx, opts) {
        opts = opts || {};
        const out = [];
        /** @type {Set<string>} Icons used by the theme items so far (a duplicate gets its ThemeIcons icon). */
        const themeIcons = new Set();
        const conv = (list, depth) => {
            const res = [];
            for (const it of list || []) {
                if (!it)
                    continue;
                if (isSeparator(it)) {
                    res.push(it.Name ? { Title: it.Name } : "-");
                    continue;
                }
                const kids = childrenOf(it);
                const flags = it.Flags || 0;
                const disabled = (flags & MenuFlags.Disabled) !== 0;
                const theme = ThemeOfItem(it);
                const m = {
                    //  (a theme item's server name "(Automatic)" reads as the other choices: "Automatic", Designer D14)
                    Text: theme !== null ? String(it.Name || "").replace(/^\((.+)\)$/, "$1") : (it.Name || ""),
                    Title: it.Title || null,
                    Icon: MonoIcon(it.IconClass),
                    Disabled: disabled,
                    SwSource: it,
                };
                if (theme !== null) {
                    const alt = ThemeIcons[theme] ? new URL(ThemeIcons[theme], Sw.Url.Root).href : null;
                    if (m.Icon && themeIcons.has(m.Icon) && alt && !themeIcons.has(alt))
                        m.Icon = alt;
                    if (m.Icon)
                        themeIcons.add(m.Icon);
                    m.SwTheme = theme;
                    m.Radio = true;
                    m.Checked = theme === storedTheme();
                }
                //  (one of a group of choices, ex: the settings menu's Density: a radio item, checked or not, menuitemradio)
                else if (it.Radio === true) {
                    m.Radio = true;
                    m.Checked = (flags & MenuFlags.Checked) !== 0;
                }
                else if ((flags & MenuFlags.Checked) !== 0)
                    m.Checked = true;
                if (kids.length > 0) {
                    if (opts.Sections && depth === 0) {
                        if (res.length || out.length)
                            res.push("-");
                        res.push({ Title: it.Name || "" });
                        if (HasAction(it))
                            res.push(actionItem(it, m));
                        res.push(...conv(kids, depth + 1));
                        continue;
                    }
                    const sub = conv(kids, depth + 1);
                    if (HasAction(it))
                        sub.unshift(actionItem(it, Object.assign({}, m)), "-");
                    m.Items = sub;
                    res.push(m);
                    continue;
                }
                res.push(actionItem(it, m));
            }
            return res;
        };
        /**
         * Attach the action of a server item to a Sw.Menu item.
         * @param {object} it The server item.
         * @param {object} m The Sw.Menu item.
         * @returns {object} m.
         */
        const actionItem = (it, m) => {
            //  Function / Js items decide themselves if the menu closes (truthy result), so the menu stays open by default.
            //  Theme items close the menu: an open menu covers the page, swallows the next tap and doesn't fit a new theme's font.
            const decides = typeof it.Data === "function" || it.Type === MenuTypes.Js;
            m.Keep = decides && m.SwTheme === undefined;
            m.OnClick = async (mi, ev) => {
                const list = ev && ev.target && ev.target.closest ? ev.target.closest(".sw-menu") : null;
                await RunItem(it, ctx, ev);
                if (m.SwTheme !== undefined)
                    refreshThemeChecks(list);
            };
            if (!HasAction(it))
                m.Disabled = true;
            return m;
        };
        out.push(...conv(items, 0));
        return out;
    }

    /**
     * Show server menu items as a pop-up menu (Sw.Menu).
     * @param {Element|{X:number, Y:number}} anchor The anchor.
     * @param {Array<object>} items Server menu items.
     * @param {object} ctx Action context (see RunItem); Hide defaults to closing this menu.
     * @param {object} [opts] Sw.Menu options plus { Sections }.
     * @returns {object} The Sw.Menu handle.
     */
    function ShowMenu(anchor, items, ctx, opts) {
        opts = opts || {};
        ctx = Object.assign({}, ctx);
        let handle = null;
        const userHide = ctx.Hide;
        ctx.Hide = () => {
            if (handle)
                handle.Close();
            if (userHide)
                userHide();
        };
        //  (Sections is this layer's option, used by ToMenuItems: not passed on, so Sw.Menu.Show has no unknown option to warn about; D23)
        const mo = Object.assign({}, opts);
        delete mo.Sections;
        handle = Sw.Menu.Show(anchor, ToMenuItems(items, ctx, opts), mo);
        return handle;
    }

    // ------------------------------------------------------------------ distinct item icons

    /**
     * Distinct icons of framework pages whose servers send a shared icon (ex: "Problems" and "Statistics" both IconTableStats, the three
     * folder sync tables IconSync). Keys: the item's Data (the request or page, lower case), values: icon classes of ui/icons.css. Used only
     * for an item that shares its glyph with another page of the same menu, and only when the distinct glyph is not used by another page
     * yet (see DistinctIcons); a server that names its own distinct icon is never changed.
     */
    const MenuIcons = Object.freeze({
        "api/debug/problemstable": "IconProblems",
        "logfile/logfile.html": "IconLogFile",
        "api/debug/sessioncacheentries": "IconSessionCache",
        "foldersync/managedfolderstable": "IconFolderManaged",
        "foldersync/sharedfolderstable": "IconFolderShared",
        "foldersync/remotefolderstable": "IconFolderRemote",
        "api/debug/explore/datatablereferencestable": "IconTableRefs",
        "api/fakeemail/messagetable": "IconTableEmails",
        "api/messagetable": "IconTableMessages",
        "openai/getimagemodeltable": "IconAiImageModels",
        "openai/getllmmodeltable": "IconAiTextModels",
        "api/debug/reverseproxyendpointstable": "IconEndPoints",
        "reverseproxy/getclientdetails": "IconTableClients",
        "api/debug/servicestable": "IconLoadedServices",
        "servermanager/keys.html": "IconKeyFiles",
        "mediaview/backgroundeffectslib.html": "IconBackgroundEffects",
        "mediaview/imageeffectslib.html": "IconImageEffects",
        "mediaview/collageeffectslib.html": "IconCollageEffects",
    });

    /** @type {Map<string, string>} Icon (class or url) -> the svg it shows (file name, lower case). */
    const glyphCache = new Map();

    /**
     * The glyph an icon shows: the svg file name of an url, or of the mask image of an icon class (two classes may show one svg).
     * @param {string} icon The icon (MonoIcon result).
     * @returns {string} The glyph key.
     */
    function glyphOf(icon) {
        let g = glyphCache.get(icon);
        if (g !== undefined)
            return g;
        g = icon.toLowerCase();
        if (!/[\/.]/.test(icon) && document.body) {
            const probe = document.createElement("span");
            probe.className = icon;
            probe.style.cssText = "position:absolute;width:0;height:0;overflow:hidden";
            document.body.append(probe);
            const cs = getComputedStyle(probe);
            const m = /url\(["']?([^"')]*)/.exec(cs.maskImage || cs.webkitMaskImage || cs.backgroundImage || "");
            probe.remove();
            if (m)
                g = m[1].toLowerCase();
        }
        g = g.substring(g.lastIndexOf("/") + 1);
        glyphCache.set(icon, g);
        return g;
    }

    /**
     * Give pages that share one glyph with another page of the same menu a distinct icon (MenuIcons), so the rail (icons only) never
     * shows two pages with the same icon. A page without a known distinct icon keeps its own. A folder keeps its icon unless an item
     * inside it shows the same glyph, then it gets the folder glyph (IconMenuFolder); the same page listed twice (ex: as a
     * favourite) gets the same icon both times.
     * @param {Array<object>} items The menu items.
     * @returns {Map<object, string>} Item -> the icon to show instead of its IconClass.
     */
    function DistinctIcons(items) {
        const leaves = [];
        /** @type {Array<object>} Folders with an icon (their glyph counts as used, so a page that shows it gets its distinct icon). */
        const folderItems = [];
        const walk = l => {
            for (const it of l || []) {
                if (!it || isSeparator(it))
                    continue;
                const kids = childrenOf(it);
                if (kids.length) {
                    if (MonoIcon(it.IconClass))
                        folderItems.push(it);
                    walk(kids);
                }
                else if (MonoIcon(it.IconClass))
                    leaves.push(it);
            }
        };
        walk(items);
        const page = it => String(it.Data || it.Name || "");
        /** @type {Map<string, Set<string>>} Glyph -> the pages showing it. */
        const used = new Map();
        /** @type {Map<string, string>} Page -> its glyph (the first item of the page decides). */
        const pages = new Map();
        for (const it of leaves) {
            const p = page(it);
            if (pages.has(p))
                continue;
            const g = glyphOf(MonoIcon(it.IconClass));
            pages.set(p, g);
            if (!used.has(g))
                used.set(g, new Set());
            used.get(g).add(p);
        }
        folderItems.forEach((it, i) => {
            const g = glyphOf(MonoIcon(it.IconClass));
            if (!used.has(g))
                used.set(g, new Set());
            used.get(g).add("\u0000folder" + i);
        });
        /** @type {Map<string, string>} Page -> the distinct icon. */
        const alts = new Map();
        //  The groups are taken before any change: every page of a shared glyph with a distinct icon gets it (the three folder tables all change)
        const shared = new Set([...used].filter(([, s]) => s.size > 1).map(([g]) => g));
        for (const [p, g] of pages) {
            if (!shared.has(g))
                continue;
            const alt = MenuIcons[p.toLowerCase().replace(/^(\.\.\/|\/)+/, "")];
            if (!alt)
                continue;
            const ag = glyphOf(alt);
            if (used.has(ag) && used.get(ag).size)
                continue;
            alts.set(p, alt);
            used.get(g).delete(p);
            used.set(ag, new Set([p]));
        }
        const res = new Map();
        for (const it of leaves) {
            const a = alts.get(page(it));
            if (a)
                res.set(it, a);
        }
        //  a folder that shows the same glyph as an item inside it (8209: Translation / Target languages, AI models /
        //  OpenAI LLM models) gives way: it shows the neutral folder glyph (IconMenuFolder), the page keeps its meaningful icon.
        //  Inner folders first, so their new icon counts for the outer folder.
        const inside = (l, set) => {
            for (const c of l || []) {
                if (!c || isSeparator(c))
                    continue;
                const ci = res.get(c) || MonoIcon(c.IconClass);
                if (ci)
                    set.add(glyphOf(ci));
                inside(childrenOf(c), set);
            }
            return set;
        };
        const folders = l => {
            for (const it of l || []) {
                if (!it || isSeparator(it))
                    continue;
                const kids = childrenOf(it);
                if (!kids.length)
                    continue;
                folders(kids);
                const fi = MonoIcon(it.IconClass);
                if (fi && inside(kids, new Set()).has(glyphOf(fi)))
                    res.set(it, "IconMenuFolder");
            }
        };
        folders(items);
        return res;
    }

    // ------------------------------------------------------------------ menu tree

    /** The localStorage key prefix of the collapsed folders. */
    const CollapsedPrefix = "SysWeaver.MenuCollapsed.";

    /**
     * A menu rendered as an ARIA tree (role tree / treeitem / group) with one tab stop (roving tab index).
     * Keyboard: Up / Down move, Right expands (or moves to the first child), Left collapses (or moves to the parent), Home / End,
     * Enter / Space activate (a folder without an action toggles), "*" expands all siblings, letters jump to the next item starting with them.
     * Mouse / touch: a click (tap) on a row activates it, on the chevron of a folder with an action toggles it. Modifier clicks are ignored.
     */
    class MenuTree {
        /**
         * @param {object} o Options: { Menu ({Items, Name}), Name (collapse state name, default Menu.Name or "NoName"), Context (the action
         * context, see RunItem: Navigate, Hide, Style), Label (aria label), Class, OnActivate(entry, ev, hidden) (after an action ran),
         * DefaultCollapsed(item) (true: the folder starts collapsed until the user expands it; the choice is stored), OnTopUp() (ArrowUp on the
         * first visible item, ex: back to a filter box), Sections (top level folders are section labels: class sw-shell-section, no icon,
         * the chevron at the right, their children at the first indent; in the rail a hairline whose children always show) }.
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.Menu = o.Menu || { Items: [] };
            const name = o.Name || t.Menu.Name || "NoName";
            t.ExpKey = CollapsedPrefix + name + ".";
            /** @type {Array<object>} Entries in document order: { Item, E (treeitem), Row, Group, Depth, Parent, Url, Expander }. */
            t.Entries = [];
            /** @type {Map<string, object>} Normalized absolute url -> entry. */
            t.UrlMap = new Map();
            /** @type {object|null} The selected entry. */
            t.Selected = null;
            /** @type {string} The active filter text (lower case, "" = no filter, see Filter). */
            t.FilterText = "";
            /** @type {boolean} Rendered as an icon only rail (see SetRail). */
            t.Rail = false;
            t.E = Sw.el("div.sw-shell-tree", { attrs: { role: "tree", "aria-label": o.Label || null } });
            if (o.Class)
                t.E.classList.add(...o.Class.split(" "));
            let haveFolders = false;
            for (const it of t.Menu.Items || [])
                if (childrenOf(it).length)
                    haveFolders = true;
            t.E.classList.toggle("sw-shell-tree-flat", !haveFolders);
            /** @type {Map<object, string>} Item -> a distinct icon (DistinctIcons). */
            t.IconOf = DistinctIcons(t.Menu.Items || []);
            t.AddItems(t.E, t.Menu.Items || [], 1, null);
            const first = t.Entries.find(x => !x.Separator);
            if (first)
                first.E.tabIndex = 0;
            t.E.addEventListener("keydown", ev => t.OnKey(ev));
            t.E.addEventListener("focusin", ev => {
                const en = t.EntryOf(ev.target);
                if (en)
                    t.SetTabStop(en);
            });
        }

        /**
         * Render items into a container.
         * @param {HTMLElement} to The container (the tree or a group).
         * @param {Array<object>} items The items.
         * @param {number} level The aria level (1 = top).
         * @param {object|null} parent The parent entry.
         */
        AddItems(to, items, level, parent) {
            for (const it of items)
                if (it)
                    to.append(this.AddItem(it, level, parent));
        }

        /**
         * Render one item (and its children).
         * @param {object} it The item.
         * @param {number} level The aria level.
         * @param {object|null} parent The parent entry.
         * @returns {HTMLElement} The element.
         */
        AddItem(it, level, parent) {
            const t = this;
            const el = Sw.el;
            //  The indent: one step per level; with Sections the top level folders are labels, so their children start at the first indent
            const vdepth = Math.max(0, level - 1 - (t.O.Sections && level > 1 ? 1 : 0));
            if (isSeparator(it)) {
                const sep = el("div.sw-shell-sep", { attrs: { role: "separator" }, style: { cssText: "--sw-shell-depth:" + vdepth } },
                    it.Name ? el("span", { text: it.Name }) : null);
                t.Entries.push({ Item: it, E: sep, Separator: true, Depth: level - 1, Parent: parent });
                return sep;
            }
            const kids = childrenOf(it);
            const isFolder = kids.length > 0;
            const flags = it.Flags || 0;
            const disabled = (flags & MenuFlags.Disabled) !== 0;
            const checked = (flags & MenuFlags.Checked) !== 0;
            const hasAction = HasAction(it);
            const url = ItemUrl(it);
            //  The accessible name is the item name (the text is hidden in the rail, and a folder's content includes its children)
            const e = el("div.sw-shell-item", {
                tabIndex: -1,
                attrs: {
                    role: "treeitem", "aria-level": String(level), "aria-label": it.Name || null,
                    "aria-disabled": disabled ? "true" : null, "aria-checked": checked ? "true" : null,
                },
            });
            const row = el("div.sw-shell-row", { style: { cssText: "--sw-shell-depth:" + vdepth } });
            //  Section: a top level folder rendered as a section label (Sections option)
            const section = !!t.O.Sections && isFolder && level === 1;
            const en = { Item: it, E: e, Row: row, Depth: level - 1, Parent: parent, Url: url, Folder: isFolder, Disabled: disabled, HasAction: hasAction, Section: section };
            t.Entries.push(en);
            e.SwEntry = en;
            t.UpdateRowTitle(en);
            //  Touch: a long press shows the tool tip (the name in the rail, else the description)
            if (Sw.Hint)
                Sw.Hint.Attach(row);
            //  Expander (folders), icon, text, check mark
            let expander = null;
            if (isFolder) {
                expander = el("span.sw-shell-expander", { attrs: { "aria-hidden": "true" } }, Sw.Icon("right"));
                row.append(expander);
            }
            else
                row.append(el("span.sw-shell-noexpander"));
            let icon = MonoIcon(t.IconOf && t.IconOf.has(it) ? t.IconOf.get(it) : it.IconClass);
            //  A folder without an icon or an action shows plus / minus as its icon (as before; not a section label, its chevron says it)
            en.ExpIcon = !icon && isFolder && !hasAction && !section;
            //  Other items without an icon show none (as before): an empty box keeps the text aligned
            //  (shell chrome uses the line set: plus / minus of Sw.Icons, Design v2, PORTING.md B15)
            const iconEl = icon ? (Sw.LineIcon ? Sw.LineIcon(icon, "sw-shell-icon") : Sw.IconClass(icon, { Class: "sw-shell-icon" }))
                : en.ExpIcon ? Sw.Icon("plus", "sw-shell-icon")
                : el("span.sw-shell-icon.sw-shell-noicon", { attrs: { "aria-hidden": "true" } });
            row.append(iconEl);
            en.Icon = iconEl;
            //  The name as word spans (separated by spaces, so it reads the same): the phone rail caption wraps only between words and
            //  shortens a word that is too long with an ellipsis (shell.css)
            const text = el("span.sw-shell-text");
            String(it.Name || "").split(/(\s+)/).forEach(w => {
                if (!w)
                    return;
                text.append(/^\s+$/.test(w) ? document.createTextNode(w) : el("span.sw-shell-word", { text: w }));
            });
            row.append(text);
            if (checked)
                row.append(Sw.Icon("check", "sw-shell-check"));
            if (url) {
                const k = NormalizeUrl(url);
                if (k && !t.UrlMap.has(k))
                    t.UrlMap.set(k, en);
            }
            if (disabled)
                e.classList.add("sw-disabled");
            if (hasAction)
                e.classList.add("sw-shell-action");
            e.append(row);
            row.addEventListener("click", ev => {
                if (Sw.BadClick(ev))
                    return;
                if (expander && hasAction && expander.contains(ev.target))
                    t.Toggle(en);
                else
                    t.Activate(en, ev);
            });
            if (isFolder) {
                const g = el("div.sw-shell-group", { attrs: { role: "group" }, style: { cssText: "--sw-shell-depth:" + (level - 1) } });
                en.Group = g;
                e.append(g);
                e.classList.add("sw-shell-folder");
                if (section)
                    e.classList.add("sw-shell-section");
                t.AddItems(g, kids, level + 1, en);
                //  Stored: "1" collapsed, "0" expanded (only stored for DefaultCollapsed folders), none: the default
                const stored = Sw.Storage.Get(t.ExpKey + t.KeyOf(it), null);
                en.DefaultCollapsed = !!(t.O.DefaultCollapsed && t.O.DefaultCollapsed(it, level));
                const expanded = stored === null ? !en.DefaultCollapsed : stored !== "1";
                t.SetExpanded(en, expanded, true);
            }
            return e;
        }

        /**
         * The storage key part of an item (Id, else Name).
         * @param {object} it The item.
         * @returns {string} The key.
         */
        KeyOf(it) {
            return (it.Id && it.Id.length) ? it.Id : (it.Name || "");
        }

        /**
         * Expand or collapse a folder (persisted unless silent).
         * @param {object} en The entry.
         * @param {boolean} expanded Expanded.
         * @param {boolean} [silent] Don't store the state.
         */
        SetExpanded(en, expanded, silent) {
            if (!en.Folder)
                return;
            en.Expanded = expanded;
            en.E.setAttribute("aria-expanded", expanded ? "true" : "false");
            en.E.classList.toggle("sw-collapsed", !expanded);
            if (en.ExpIcon) {
                const ic = Sw.Icon(expanded ? "minus" : "plus", "sw-shell-icon");
                en.Icon.replaceWith(ic);
                en.Icon = ic;
            }
            this.UpdateRowTitle(en);
            if (!silent) {
                const k = this.ExpKey + this.KeyOf(en.Item);
                if (expanded) {
                    if (en.DefaultCollapsed)
                        Sw.Storage.Set(k, "0");
                    else
                        Sw.Storage.Remove(k);
                }
                else
                    Sw.Storage.Set(k, "1");
            }
        }

        /**
         * Set the tool tip of an entry's row: the name (rail only, where the text is hidden), the item description (Title) and for folders
         * the expand / collapse hint.
         * @param {object} en The entry.
         */
        UpdateRowTitle(en) {
            const it = en.Item;
            const parts = [];
            if (this.Rail && it.Name)
                parts.push(it.Name);
            if (it.Title && it.Title !== it.Name)
                parts.push(it.Title);
            if (en.Folder)
                parts.push(en.Expanded
                    ? _TF("Collapse", "The tool tip description of a button on a menu row that when pressed will hide the sub menu (child) items")
                    : _TF("Expand", "The tool tip description of a button on a menu row that when pressed will show the sub menu (child) items"));
            if (parts.length)
                en.Row.title = parts.join("\n\n");
            else
                en.Row.removeAttribute("title");
        }

        /**
         * Show a count badge at the end of an entry's row (Design v2 3.8, ex: "Problems 3"): right aligned, the accessible name and
         * the tool tip say what it counts; in the rail a dot on the icon. 0 / null removes it.
         * @param {object} en The entry (ex: FindUrl(url)).
         * @param {?number} count The count (shown as "99+" above 99).
         * @param {string} [text] What it counts, for the accessible name and the tool tip (ex: "3 parts failing in the last 24 hours").
         * @param {string} [kind] The tone: "danger" (default), "warning" or "neutral".
         */
        SetBadge(en, count, text, kind) {
            if (!en || !en.Row)
                return;
            let b = en.Badge;
            if (!(count > 0)) {
                if (b)
                    b.remove();
                if (en.Item.Name)
                    en.E.setAttribute("aria-label", en.Item.Name);
                en.Badge = null;
                en.Row.classList.remove("sw-shell-hasbadge");
                return;
            }
            if (!b) {
                b = Sw.el("span.sw-shell-badge");
                en.Badge = b;
                const check = en.Row.querySelector(".sw-shell-check");
                en.Row.insertBefore(b, check || null);
            }
            b.className = "sw-shell-badge sw-shell-badge-" + (kind || "danger");
            //  the number for the eye, the sentence in the item's accessible name: "Problems, 3 parts failing in the last 24 hours"
            b.textContent = count > 99 ? "99+" : String(count);
            b.setAttribute("aria-hidden", "true");
            en.E.setAttribute("aria-label", (en.Item.Name || "") + ", " + (text || String(count)));
            b.title = text || "";
            en.Row.classList.add("sw-shell-hasbadge");
        }

        /**
         * Render as (or back from) an icon only rail: the rows get the item name as tool tip (hover, long press).
         * @param {boolean} on Rail mode.
         */
        SetRail(on) {
            const t = this;
            on = !!on;
            if (t.Rail === on)
                return;
            t.Rail = on;
            for (const en of t.Entries)
                if (!en.Separator)
                    t.UpdateRowTitle(en);
            //  An open section label is only a hairline in the rail (not an item): the tab stop moves to the first shown item
            const cur = t.EntryOf(t.E.querySelector(".sw-shell-item[tabindex='0']"));
            if (on && cur && cur.Section && cur.Expanded) {
                const vis = t.Visible();
                if (vis.length)
                    t.SetTabStop(t.Selected && vis.includes(t.Selected) ? t.Selected : vis[0]);
            }
        }

        /**
         * The folders above an entry, top first (the shell's breadcrumb).
         * @param {object|null} en The entry.
         * @returns {Array<object>} The ancestor entries (empty for a top level entry or null).
         */
        Ancestors(en) {
            const res = [];
            for (let p = en ? en.Parent : null; p; p = p.Parent)
                res.unshift(p);
            return res;
        }

        /**
         * Filter the tree: only items whose name contains the text (case insensitive), their parents and the children of matching folders
         * stay visible; folders show their children while filtering (the stored expand state is not changed); separators are hidden.
         * @param {string} text The filter text ("" shows everything again).
         * @returns {{Count: number, First: object|null, FirstLeaf: object|null}} The number of visible items with an action (-1 without a
         * filter), the first of them, and the first visible page (an item with an action that is not a folder; one whose own name matches
         * is preferred over a child of a matching folder).
         */
        Filter(text) {
            const t = this;
            const q = (text || "").trim().toLowerCase();
            t.FilterText = q;
            t.E.classList.toggle("sw-shell-filtering", !!q);
            const matched = new Set();
            const show = new Set();
            if (q) {
                for (const en of t.Entries) {
                    if (en.Separator || !(en.Item.Name || "").toLowerCase().includes(q))
                        continue;
                    matched.add(en);
                    for (let p = en; p; p = p.Parent)
                        show.add(p);
                }
                //  The children of a matching folder are shown too
                for (const en of t.Entries) {
                    if (en.Separator)
                        continue;
                    for (let p = en.Parent; p; p = p.Parent)
                        if (matched.has(p)) {
                            show.add(en);
                            break;
                        }
                }
            }
            let count = 0, first = null, leaf = null, leafMatch = null;
            for (const en of t.Entries) {
                const hidden = !!q && (en.Separator || !show.has(en));
                en.Hidden = hidden;
                en.E.classList.toggle("sw-shell-hidden", hidden);
                if (en.Separator)
                    continue;
                if (en.Folder)
                    en.E.setAttribute("aria-expanded", (q ? !hidden : en.Expanded) ? "true" : "false");
                if (!hidden && en.HasAction && !en.Disabled) {
                    ++count;
                    if (!first)
                        first = en;
                    if (!en.Folder) {
                        if (!leaf)
                            leaf = en;
                        if (!leafMatch && matched.has(en))
                            leafMatch = en;
                    }
                }
            }
            const vis = t.Visible();
            if (vis.length && !vis.includes(t.EntryOf(t.E.querySelector(".sw-shell-item[tabindex='0']"))))
                t.SetTabStop(leafMatch || leaf || first || vis[0]);
            return { Count: q ? count : -1, First: first, FirstLeaf: leafMatch || leaf };
        }

        /**
         * Toggle a folder. While filtering a folder is only expanded, never collapsed (all folders show their matches then, a collapse
         * would hide the results).
         * @param {object} en The entry.
         */
        Toggle(en) {
            if (this.FilterText && en.Expanded)
                return;
            this.SetExpanded(en, !en.Expanded);
        }

        /**
         * Expand or collapse every folder (the shell's "Expand all" / "Collapse all" menu tools; persisted like a user toggle). While
         * filtering the matches stay shown (the stored state changes, it shows when the filter is cleared). The keyboard tab stop (and
         * the focus) moves to the nearest shown parent when its item gets hidden.
         * @param {boolean} expanded Expand (true) or collapse (false).
         * @returns {number} The number of folders that changed.
         */
        SetAllExpanded(expanded) {
            const t = this;
            expanded = !!expanded;
            let n = 0;
            for (const en of t.Entries)
                if (en.Folder && en.Expanded !== expanded) {
                    t.SetExpanded(en, expanded);
                    ++n;
                }
            if (t.FilterText)
                t.Filter(t.FilterText);
            //  The tab stop (and a focused item) inside a now collapsed folder moves to the folder shown at the top of that branch
            const cur = t.EntryOf(t.E.querySelector(".sw-shell-item[tabindex='0']"));
            if (cur) {
                const vis = t.Visible();
                let to = cur;
                while (to && !vis.includes(to))
                    to = to.Parent;
                if (to && to !== cur) {
                    const had = cur.E === document.activeElement;
                    t.SetTabStop(to);
                    if (had)
                        to.E.focus({ preventScroll: true });
                }
            }
            return n;
        }

        /**
         * Activate an entry (click, Enter, Space or code): runs its action, or toggles a folder without one.
         * Embedding items don't reload on a user activation of the already selected item, they only hide a temporary menu
         * (Context.Hide: the shell's phone drawer / the menu shown over the rail).
         * @param {object} en The entry.
         * @param {Event} [ev] The event (undefined for programmatic calls).
         * @returns {Promise<boolean>} True if the menu was hidden by the action.
         */
        async Activate(en, ev) {
            const t = this;
            if (!en || en.Separator || en.Disabled)
                return false;
            if (!en.HasAction) {
                if (en.Folder) {
                    t.Toggle(en);
                    //  In the rail an open section is only a hairline (not an item): the tab stop moves to its first item
                    if (t.Rail && en.Section && en.Expanded) {
                        const first = t.Visible().find(x => x.Parent === en);
                        if (first) {
                            const had = en.E === document.activeElement;
                            t.SetTabStop(first);
                            if (had)
                                first.E.focus({ preventScroll: true });
                        }
                    }
                }
                return false;
            }
            const ctx = t.O.Context || {};
            if (en.Url && ev && en === t.Selected) {
                if (typeof ctx.Hide !== "function")
                    return false;
                ctx.Hide();
                return true;
            }
            const nctx = Object.assign({}, ctx, {
                Navigate: (url, item, e2) => {
                    t.Select(en);
                    if (ctx.Navigate)
                        ctx.Navigate(url, item, e2);
                    else
                        Sw.Page.Navigate(url, "_self");
                },
            });
            const hidden = await RunItem(en.Item, nctx, ev);
            if (t.O.OnActivate)
                t.O.OnActivate(en, ev, hidden);
            return hidden;
        }

        /**
         * Select an entry (highlight; null deselects).
         * @param {object|null} en The entry.
         */
        Select(en) {
            const t = this;
            const s = t.Selected;
            if (s === en)
                return;
            if (s) {
                s.E.classList.remove("sw-sel");
                s.E.removeAttribute("aria-selected");
                s.E.removeAttribute("aria-current");
            }
            t.Selected = en || null;
            if (en) {
                en.E.classList.add("sw-sel");
                en.E.setAttribute("aria-selected", "true");
                en.E.setAttribute("aria-current", "page");
                //  Make sure the selected item is visible: expand its parents (not persisted)
                for (let p = en.Parent; p; p = p.Parent)
                    if (!p.Expanded)
                        t.SetExpanded(p, true, true);
                t.SetTabStop(en);
            }
            const st = t.O.Context && t.O.Context.Style;
            if (st && typeof st.SelectFn === "function")
                st.SelectFn(en ? en.Item : null);
        }

        /**
         * Find the entry of a page url.
         * @param {string} url The url.
         * @returns {object|null} The entry or null.
         */
        FindUrl(url) {
            return this.UrlMap.get(NormalizeUrl(url)) || null;
        }

        /**
         * Find the entry that matches a page url best: the exact url, else an item of the same page (origin and path) whose query
         * parameters are all present in the url with the same values (the one with the most parameters wins), else none.
         * Ex: table.html?q=X&f=1 selects the item table.html?q=X; api.html?q=... selects nothing unless an item opens api.html.
         * @param {string} url The url.
         * @returns {object|null} The entry or null.
         */
        FindBest(url) {
            const exact = this.FindUrl(url);
            if (exact)
                return exact;
            let u;
            try {
                u = new URL(NormalizeUrl(url));
            }
            catch {
                return null;
            }
            let best = null, bestScore = -1;
            for (const [k, en] of this.UrlMap) {
                let m;
                try {
                    m = new URL(k);
                }
                catch {
                    continue;
                }
                if (m.origin !== u.origin || m.pathname !== u.pathname)
                    continue;
                let score = 0, ok = true;
                m.searchParams.forEach((v, n) => {
                    if (u.searchParams.get(n) === v)
                        ++score;
                    else
                        ok = false;
                });
                if (ok && score > bestScore) {
                    best = en;
                    bestScore = score;
                }
            }
            return best;
        }

        /**
         * Select the entry of a page url (deselects if there is none).
         * @param {string} url The url.
         * @returns {object|null} The selected entry.
         */
        SelectUrl(url) {
            const en = this.FindUrl(url);
            this.Select(en);
            return en;
        }

        /**
         * Find the entry of an element.
         * @param {Node} node A node inside an item.
         * @returns {object|null} The entry.
         */
        EntryOf(node) {
            const e = node && node.closest ? node.closest(".sw-shell-item") : null;
            return e && this.E.contains(e) ? e.SwEntry : null;
        }

        /**
         * Make an entry the single tab stop of the tree.
         * @param {object} en The entry.
         */
        SetTabStop(en) {
            for (const x of this.Entries)
                if (!x.Separator && x !== en && x.E.tabIndex === 0)
                    x.E.tabIndex = -1;
            en.E.tabIndex = 0;
        }

        /**
         * The visible (not inside a collapsed folder, not filtered out) item entries in document order. In the rail an open section is
         * a hairline (not an item); a collapsed section is its chevron (an item that expands it). The rail keeps the expand state of the
         * full menu, so every row stays at the same height in both (no row moves when the menu collapses to the rail or expands).
         * @returns {Array<object>} The entries.
         */
        Visible() {
            const filtering = !!this.FilterText;
            const rail = this.Rail;
            return this.Entries.filter(en => {
                if (en.Separator || (rail && en.Section && en.Expanded))
                    return false;
                if (filtering)
                    return !en.Hidden;
                for (let p = en.Parent; p; p = p.Parent)
                    if (!p.Expanded)
                        return false;
                return true;
            });
        }

        /**
         * Focus the tree: the selected entry, else the current tab stop.
         */
        Focus() {
            const t = this;
            let en = t.Selected;
            if (en && !t.Visible().includes(en))
                en = null;
            const target = en ? en.E : (t.E.querySelector(".sw-shell-item[tabindex='0']") || (t.Visible()[0] || {}).E);
            if (target) {
                target.focus({ preventScroll: true });
                target.scrollIntoView({ block: "nearest" });
            }
        }

        /**
         * Keyboard handling (see the class description).
         * @param {KeyboardEvent} ev The event.
         */
        OnKey(ev) {
            const t = this;
            const cur = t.EntryOf(document.activeElement);
            if (!cur || ev.altKey || ev.ctrlKey || ev.metaKey)
                return;
            const vis = t.Visible();
            const i = vis.indexOf(cur);
            const go = en => {
                if (!en)
                    return;
                en.E.focus({ preventScroll: true });
                en.E.scrollIntoView({ block: "nearest" });
            };
            switch (ev.key) {
                case "ArrowDown": go(vis[Math.min(vis.length - 1, i + 1)]); break;
                case "ArrowUp":
                    if (i <= 0 && t.O.OnTopUp)
                        t.O.OnTopUp();
                    else
                        go(vis[Math.max(0, i - 1)]);
                    break;
                case "Home": go(vis[0]); break;
                case "End": go(vis[vis.length - 1]); break;
                case "ArrowRight":
                    //  While filtering every shown folder is open: move to its first child
                    if (cur.Folder) {
                        if (!cur.Expanded && !t.FilterText) {
                            t.SetExpanded(cur, true);
                            //  the rail: an open section is a hairline, the focus goes to its first item
                            if (t.Rail && cur.Section)
                                go(t.Visible().find(x => x.Parent === cur));
                        }
                        else
                            go(t.Visible()[i + 1]);
                    }
                    break;
                case "ArrowLeft":
                    //  While filtering folders are not collapsed (it would hide the matches): move to the parent
                    if (cur.Folder && cur.Expanded && !t.FilterText)
                        t.SetExpanded(cur, false);
                    else if (cur.Parent && t.Rail && cur.Parent.Section && cur.Parent.Expanded && !t.FilterText) {
                        //  the rail: the open section is a hairline; Left folds it back to its chevron (an item) and focuses that
                        t.SetExpanded(cur.Parent, false);
                        t.SetTabStop(cur.Parent);
                        go(cur.Parent);
                    }
                    else if (cur.Parent)
                        go(cur.Parent);
                    break;
                case "Enter":
                case " ":
                    if (ev.repeat)
                        break;
                    t.Activate(cur, ev);
                    break;
                case "*":
                    for (const en of t.Entries)
                        if (en.Folder && en.Parent === cur.Parent && !en.Expanded)
                            t.SetExpanded(en, true);
                    break;
                default: {
                    //  "?" is left to the shell keyboard help
                    if (ev.key.length !== 1 || ev.key === "?" || ev.shiftKey && ev.key === " ")
                        return;
                    const k = ev.key.toLowerCase();
                    const n = vis.length;
                    for (let j = 1; j <= n; ++j) {
                        const c = vis[(i + j) % n];
                        if ((c.Item.Name || "").trim().toLowerCase().startsWith(k)) {
                            go(c);
                            break;
                        }
                    }
                    break;
                }
            }
            ev.preventDefault();
            ev.stopPropagation();
        }
    }

    Object.assign(Shell, {
        MenuTypes,
        MenuFlags,
        GetMenu,
        ItemUrl,
        NormalizeUrl,
        HasAction,
        RunItem,
        ThemeOfItem,
        ThemeIcons,
        MenuIcons,
        DistinctIcons,
        ToMenuItems,
        ShowMenu,
        MenuTree,
        CollapsedPrefix,
    });
})();
