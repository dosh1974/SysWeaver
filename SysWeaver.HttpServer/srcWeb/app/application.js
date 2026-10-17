


/**
 * @file The SysWeaver web application shell and the reusable menu / pop-up library.
 *
 * Loading: a classic (non-module) script. The root `index.html` loads common/common.js, app/application.js and
 * app/app.js with `<script defer>` and starts the shell with `<body onload="appMain()">`. Many pages in other
 * projects (explore table/api, chart, edit, chat, media view, server manager, log file, passkeys, KeyMan,
 * user storage, ...) load `../app/application.js` (statically or via `includeJs`) only to use the pop-up
 * functions; they must also link app/application.css. Nothing executes at load time.
 *
 * Globals defined: classes `WebMenu`, `WebMenuItem`, `MainMenuStyle`, `MainMenu`, `SearchablePoupSelection`,
 * `AppParams`, `App`; functions `CreateTabBlock`, `CreatePopUpBlocker`, `CreatePopUpAtElement`, `PopUpElement`,
 * `PopUpElementMenu`, `PopUpMenu`, `PopUp`, `PopUpWorking`, `PopUpSelection`, `PopUpEdit`, `Confirm`, `appMain`;
 * window globals `SysWeaverAppTitle`, `SysWeaverApp`, `SysWeaverDoneFirst` and the implicit global `contentUrl`.
 *
 * Dependencies (common.js): `ColorIcon`, `Button`, `ValueFormat`, `FullScreen`, `InterOp`, `AddLoading`,
 * `BlockTab`/`UnblockTab`, `badClick`, `isPureClick`, `keyboardClick`, `waitFor`, `delay`, `sendRequest`,
 * `getRequest`, `createIFrame`, `appendTheme`, `GetAbsolutePath`, `ClearOtherHistoryState`,
 * `Base64EncodeString`/`Base64DecodeString`, `Open`, `Fail`, `_T`/`_TF`, `window.Mobile`/`window.Narrow` and the
 * `SwLayout` body event. `PopUpEdit` additionally needs `Edit`, `EditOptions`, `TypeMemberFlags` (edit/ scripts).
 *
 * Conventions: pop-up elements use custom tag names `SysWeaver-*` appended to `document.body`; the shell DOM
 * uses `app-*` tags; menu state is kept as `<body>` classes (`SwPinned`, `SwCollapsed`); embedded pages run in an
 * iframe inside `<app-content>` and talk to the shell with `postMessage` (`IframeNavigating`, `LoaderRemoved`).
 * Storage keys: `SysWeaver.Pinned`, `SysWeaver.MenuCollapsed.<menu>.<id>`, `SysWeaver.PopUpRecent.<id>`.
 */

/**
 * A menu tree as returned by the server (`Api/application/GetMenu`, C# `WebMenu` from WebMenuService) or built
 * on the client for pop-up menus. Rendered by `MainMenu`.
 */
class WebMenu {

    /** Root URI reported by the server. @type {string} */
    RootUri = "";
    /** Top level items. @type {WebMenuItem[]} */
    Items = [];
    /** Menu name; used in the collapsed-state storage key (MainMenu.ExpKey). @type {string} */
    Name = "";

    /**
     * Converts a plain (JSON) object into a `WebMenu`, converting `Items` recursively to `WebMenuItem`
     * instances (in place). Note: `name` is only applied when `Items` is non-empty.
     * @param {object} obj - Plain object with `RootUri` / `Items`.
     * @param {string} [name] - Menu name to set.
     * @returns {WebMenu} New WebMenu instance.
     */
    static From(obj, name) {
        const m = Object.assign(new WebMenu(), obj);
        const c = m.Items;
        if (!c)
            return m;
        const cl = c.length;
        if (cl <= 0)
            return m;
        for (let i = 0; i < cl; ++i)
            c[i] = WebMenuItem.From(c[i]);
        if (name)
            m.Name = name;
        return m;
    }

}


/**
 * A menu item (C# `WebMenuItem`). `Type` is the numeric C# `WebMenuItemTypes` value: 0 Path (folder),
 * 1 Table, 2 Embedded, 3 Link, 4 LinkExternal, 5 Js, 6 Chart. For client-built menus `Data` may instead be an
 * async function `(item, style, isUserClick) => bool` (truthy result hides the menu). `MainMenu.Add` also
 * stores the click handler on the item as `OnClick` (expando).
 */
class WebMenuItem {
    // Id of the item
    Id = "";
    // Name of the item
    Name = ""
    // The type of the item
    Type = "";
    // Title (tool tip)
    Title = "";
    // Class name for an icon
    IconClass = "";
    // Data (type dependent, typically an url).
    Data = "";
    // Optional child items
    Children = null;
    // 1 = Disabled, 2 = Checked
    Flags = 0;

    /**
     * Converts a plain (JSON) object into a `WebMenuItem`, converting `Children` recursively (in place).
     * @param {object} obj - Plain item object.
     * @returns {WebMenuItem} New WebMenuItem instance.
     */
    static From(obj) {
        const m = Object.assign(new WebMenuItem(), obj);
        const c = m.Children;
        if (!c)
            return m;
        const cl = c.length;
        if (cl <= 0)
            return m;
        for (let i = 0; i < cl; ++i)
            c[i] = WebMenuItem.From(c[i]);  
        return m;
    }

}


/**
 * Visual settings and host callbacks for a `MainMenu`. One instance per menu (main menu, each pop-up menu).
 */
class MainMenuStyle {

    /** Item icon size in pixels (scaled by --ThemeIconSize via ColorIcon). @type {number} */
    IconWidth = 32;
    IconHeight = 32;

    /** Expander icon size in pixels and the indentation per depth level in pixels. @type {number} */
    ExpandIconWidth = 20;
    ExpandIconHeight = 20;
    ExpandTab = 16;

    /** Colour classes for item icons (normal / selected) and for sub-item icons. @type {string} */
    IconColorClass = "IconColorThemeBackground";
    IconSelColorClass = "IconColorThemeAcc1";
    SubIconColorClass = "IconColorThemeBackground";
    SubIconSelColorClass = "IconColorThemeAcc1";

    /** "expanded,collapsed" icon class pairs for nested and top level expanders (see MainMenu.SetExpIcon). @type {string} */
    IconExpander = "IconCollapse,IconExpand";
    IconExpanderMain = "IconCollapseMain,IconExpandMain";
    /** CSS class added to the element the menu is rendered into. @type {string} */
    MenuStyle = "DefaultMenu";
    /** Element that hosts the embedded content iframe (`<app-content>`). @type {?HTMLElement} */
    Content = null;
    /** Called with the selected `WebMenuItem` (or null) whenever the selection changes. @type {?function(?WebMenuItem):void} */
    SelectFn = null;
    /** Called with no args when navigation starts and with (url, title) when new content is revealed. @type {?function(string=, string=):void} */
    ExternalContentFn = null;
    /** Called to close / collapse the menu after an action. @type {?function():void} */
    HideFn = null;


}

/**
 * Renders a `WebMenu` as nested `<table>`s (one row per item: indent, expander icon, name, item icon) and
 * handles item actions, including the central iframe content navigation (`EmbeddContent`) and the
 * history/hash handling. Used for the main menu (`App.Init`) and for every pop-up menu (`PopUpMenu`).
 * Create with `new MainMenu(menu, style, toElement)`; the table is in `.Element`.
 */
class MainMenu {



    /**
     * Sets a ColorIcon image to the first class of a comma separated class list.
     * @param {ColorIcon} icon - Icon to change.
     * @param {?string} iconClasses - "class[,class]" list; nothing happens when falsy.
     * @returns {void}
     */
    static SetIcon(icon, iconClasses) {
        if (!iconClasses)
            return;
        const x = iconClasses.split(',');
        icon.ChangeImage(x[0].trim());
    }

    /**
     * Sets an expander icon from an "expandedClass,collapsedClass" pair (single class: always that one).
     * @param {ColorIcon} icon - Expander icon.
     * @param {boolean} isExpanded - True selects the first class, false the second.
     * @param {?string} iconClasses - Class pair; nothing happens when falsy.
     * @returns {void}
     */
    static SetExpIcon(icon, isExpanded, iconClasses) {
        if (!iconClasses)
            return;
        const x = iconClasses.split(',');
        if (x.length < 2) {
            icon.ChangeImage(x[0].trim());
            return;
        }
        icon.ChangeImage(x[isExpanded ? 0 : 1].trim());
    }


    /**
     * Builds the menu table. Does nothing beyond setting `Data`/`Style` when the menu has no items (then
     * `Element` stays undefined). Sets `ExpKey` = "SysWeaver.MenuCollapsed.<menu.Name or NoName>.", adds every
     * top level item with `Add`, adds `MenuNoExpand` to the table when no item has children, and if
     * `toElement` is given adds `style.MenuStyle` to it and appends the table.
     * @param {WebMenu} menu - Menu to render.
     * @param {MainMenuStyle} [style] - Style and callbacks; a default MainMenuStyle when null/undefined.
     * @param {HTMLElement} [toElement] - Optional parent element.
     */
    constructor(menu, style, toElement) {
        style = style ?? new MainMenuStyle();
        this.Data = menu;
        this.Style = style;
        const c = menu.Items;
        if (!c)
            return;
        const cl = c.length;
        if (cl <= 0)
            return;

        let name = menu.Name;
        if ((!name) || (name.length <= 0))
            name = "NoName";
        this.ExpKey = "SysWeaver.MenuCollapsed." + name + ".";

        const e = document.createElement("table");
        e.setAttribute("cellspacing", "0");
        e.setAttribute("cellpadding", "0");
        let haveExpand = false;
        for (let i = 0; i < cl; ++i)
            haveExpand |= this.Add(e, c[i], 0, style);
        if (!haveExpand)
            e.classList.add("MenuNoExpand");
        this.Element = e;
        if (toElement) {
            toElement.classList.add(style.MenuStyle);
            toElement.appendChild(e);
        }
    }

    

    /**
     * Reveals the newest content iframe early: if the last child of `style.Content` is an iframe with a pending
     * `DoRemove`, it is called. Invoked by the shell when an embedded page posts `{Type:"LoaderRemoved"}`.
     * @param {MainMenuStyle} style - Style whose `Content` hosts the iframe.
     * @returns {void}
     */
    LoaderRemoved(style) {
        const content = style.Content;
        if (!content)
            return;
        const prevIFrame = content.lastElementChild;
        if (prevIFrame && (prevIFrame.tagName === "IFRAME")) {
            if (prevIFrame.DoRemove) {
                prevIFrame.DoRemove();
                //console.log("Loader done!");
            }
        }
    }


    /**
     * Returns the URL currently shown in a content host: the live `contentWindow.location.href` of its last
     * child iframe when accessible (same origin), else the iframe's `src`.
     * @param {?HTMLElement} content - The content host element.
     * @returns {?string} URL, or null if there is no host or the last child is not an iframe.
     */
    static GetEmbeddedUrl(content) {
        if (!content)
            return null;
        const le = content.lastElementChild;
        if (!le)
            return null;
        if (le.tagName !== "IFRAME")
            return null;
        try {
            const cv = le.contentWindow;
            return cv.location.href;
        }
        catch
        {
        }
        return le.src;
    }

    /**
     * Central content navigation: loads `url` into a new iframe inside `style.Content`.
     * 1. Makes the URL absolute (`GetAbsolutePath`); unless `noNewState`, clears stored history state of other
     *    pages (`ClearOtherHistoryState`).
     * 2. Calls `style.ExternalContentFn()` with no arguments (navigation started).
     * 3. If the first child of the host is an iframe it gets class `Hidden` and is removed after 25 ms,
     *    otherwise the host is cleared.
     * 4. Creates a hidden iframe (`createIFrame`, frameborder 0, allowtransparency) with an expando
     *    `DoRemove` that reveals it (see below); `onload` schedules `DoRemove` after 1 s (`RemoveTimer`).
     * 5. Unless `noNewState`, pushes a history entry (`SetUrl`) - but not for the very first embed;
     *    then sets `window.SysWeaverDoneFirst = true`, sets `src = appendTheme(url)` and appends the iframe.
     * @param {string} url - URL to embed (relative URLs resolve against the current page).
     * @param {MainMenuStyle} style - Style providing `Content` and `ExternalContentFn`.
     * @param {boolean} [noNewState] - True to not touch history (used for popstate / initial navigation).
     * @returns {void}
     */
    EmbeddContent(url, style, noNewState)
    {
        const content = style.Content;
        if (!content)
            return;
        url = GetAbsolutePath(url);
        if (!noNewState)
            ClearOtherHistoryState(url);

        const fn = style.ExternalContentFn;
        if (fn)
            fn();

        const prevIFrame = content.firstElementChild;
        if (prevIFrame && (prevIFrame.tagName === "IFRAME")){

            prevIFrame.classList.add("Hidden");
            setTimeout(() => prevIFrame.remove(), 25);
        } else {
            content.innerText = "";
        }
        const e = createIFrame();
        e.classList.add("Hidden");
        e.setAttribute("frameborder", "0");
        e.setAttribute("allowtransparency", "true");
        /**
         * Reveals the iframe (once): adds `Embedded` to the frame's `<html>` (same origin only), looks up the
         * frame's `location.href` in `RowMap` to select the matching menu row (title = item Name) or else
         * deselects (title = frame document.title, fallback location.pathname), clears the timer, removes
         * `Hidden` and calls `ExternalContentFn(newUrl, title)`. Called from the 1 s load timer or early by
         * `LoaderRemoved`; sets itself to null afterwards.
         * @returns {void}
         */
        e.DoRemove = () => {
            const cwin = e.contentWindow;
            let newUrl = null;
            let title;
            if (cwin) {
                const loc = cwin.location;
                newUrl = loc.href;
                console.log("Iframe loaded: " + newUrl);

                try {
                    const doc = cwin.document.documentElement;
                    doc.classList.add("Embedded");
                }
                catch
                {
                }


                const hrow = this.RowMap.get(newUrl);
                title = loc.pathname;
                if (hrow) {
                    title = hrow["MenuItem"].Name;
                    this.Select(hrow);
                }
                else {
                    try {
                        title = cwin.document.title;
                    }
                    catch
                    {
                    }
                    this.Select(null);
                }
            }
            if (e.RemoveTimer)
                clearTimeout(e.RemoveTimer);
            e.RemoveTimer = null;
            e.DoRemove = null;
            e.classList.remove("Hidden");
            if (fn && newUrl)
                fn(newUrl, title);
        };
        e.onload = async ev => {
            e.RemoveTimer = setTimeout(() => {
                if (e.DoRemove)
                    e.DoRemove();
                //console.log("Timeout done!");
            }, 1000);
        };
        if (!noNewState)
            if (window.SysWeaverDoneFirst)
                this.SetUrl(url);
        /** Global flag: true after the first embed; while falsy no history entry is pushed (reset by App popstate). @type {boolean} */
        window.SysWeaverDoneFirst = true;
        e.src = appendTheme(url);
        content.appendChild(e);
    }

    /** The rendered menu. @type {?WebMenu} */
    Data = null;
    /** The style / callbacks in use. @type {?MainMenuStyle} */
    Style = null;
    /** Currently selected row (`tr.MenuSel`). @type {?HTMLTableRowElement} */
    Selected = null;
    /** Original tool tip of the selected row (the title is cleared while selected). @type {?string} */
    SelTitle = null;
    /** Absolute item URL -> row, used to highlight the row matching the loaded frame URL. @type {Map<string, HTMLTableRowElement>} */
    RowMap = new Map();

    /**
     * Changes the highlighted row: restores the previous row (removes `MenuSel`, restores its title, re-enables
     * its icon with the normal colour), then highlights `row` (adds `MenuSel`, clears its title, disables its
     * icon with the selected colour) and calls `style.SelectFn(row.MenuItem or null)`.
     * @param {?HTMLTableRowElement} row - Row to select, or null to deselect.
     * @returns {void}
     */
    Select(row)
    {
        const s = this.Selected;
        const style = this.Style;
        if (s) {
            s.classList.remove("MenuSel");
            s.title = this.SelTitle;
            const i = s["MenuIcon"];
            if (i) {
                i.SetEnabled(true);
                i.ChangeColor(style.IconColorClass);
            }

        }
        this.Selected = row;
        let data = null;
        if (row) {
            row.classList.add("MenuSel");
            this.SelTitle = row.title;
            row.title = "";
            const i = row["MenuIcon"];
            if (i) {
                i.SetEnabled(false);
                i.ChangeColor(style.IconSelColorClass);
            }
            data = row["MenuItem"];
        }
        
        const f = style.SelectFn;
        if (f)
            f(data);
    }

    /**
     * Pushes a history entry with `state = key` and the URL hash set to `Base64EncodeString(key)` (deep link).
     * @param {string} key - Absolute content URL.
     * @returns {void}
     */
    SetUrl(key) {
        const n = new URL(window.location);
        n.hash = Base64EncodeString(key);
//        window.location.replace(n);
        window.history.pushState(key, "", n);
    }

    /**
     * Same as `SetUrl` but replaces the current history entry (`history.replaceState`).
     * @param {string} key - Absolute content URL.
     * @returns {void}
     */
    ReplaceUrl(key) {
        const n = new URL(window.location);
        n.hash = Base64EncodeString(key);
        //        window.location.replace(n);
        window.history.replaceState(key, "", n);
    }

    /**
     * Appends one item row (`tr.MenuData`, expandos `MenuItem` = item and `MenuIcon` = ColorIcon) to a table,
     * recursively followed by a `tr.MenuNested` row with a nested table for its children.
     * - Cells: indentation (width depth*ExpandTab+6 px; also shifts the table's background tree-line position),
     *   expander, name, icon. `Id === "-"` makes a separator (`MenuSeparator`, optional label span, no action).
     * - Action by `Data`/`Type`: function Data -> called `(item, style, isUserClick)`, truthy hides the menu;
     *   1 Table -> embeds `explore/table.html?q=../<Data>`; 2 Embedded -> embeds `<Data>`; 3 Link ->
     *   `window.location.href = Data`; 4 LinkExternal -> `window.open(Data)`; 5 Js -> calls `window[Data]` if it
     *   is a function, else `eval(Data)` (truthy hides the menu); 6 Chart -> embeds `chart/chart.html?q=../<Data>`.
     *   Embedding actions ignore user clicks on the already selected row, select the row and call `HideFn`.
     * - Embedded URLs are made absolute against the current page directory and stored in `RowMap`.
     * - Items with children get `SubMenu`; without own action the row toggles expand/collapse (`expandFn`),
     *   persisted as "1" in `localStorage[ExpKey + (Id || Name)]` when collapsed (`SubMenuHide` / `MenuHide`).
     *   Expandable items without icon use `style.IconExpanderMain` as icon.
     * - Flags bit 1 -> `MenuDisabled` and no handler; bit 2 -> `MenuChecked`. Rows with a handler get `MenuClick`.
     * - The handler is also stored on the item as `data.OnClick` (called without event for programmatic use).
     * @param {HTMLTableElement} toElement - Table to append to.
     * @param {WebMenuItem} data - Item to render.
     * @param {number} depth - Nesting depth (0 = top level).
     * @param {MainMenuStyle} style - Style and callbacks.
     * @returns {boolean} True if the item has children (expandable).
     */
    Add(toElement, data, depth, style)
    {
        let expId = data.Id;
        if ((!expId) || (expId.length <= 0))
            expId = data.Name;
        const expKey = this.ExpKey + expId;

        const rowData = data;
        const hrow = toElement.insertRow();
        let title = data.Title ?? "";
        hrow.classList.add("MenuData");
        hrow["MenuItem"] = rowData;
        hrow.title = title;
        const expTitle = title ? (title + "\n\n") : "";
        const spacing = hrow.insertCell();
        if (depth > 0) {
            const spaceWidth = (depth * style.ExpandTab + 6) + "px";
            spacing.style.width = spaceWidth;
            spacing.style.minWidth = spaceWidth;
            const lines = "calc(var(--ThemeIconSize) * 10px + " + ((depth - 1) * style.ExpandTab + 8) + "px)";
            toElement.style.backgroundPositionX = lines;
            toElement.firstElementChild.style.backgroundPositionX = lines;
        }

        let flags = data.Flags ?? 0;
        const exp = hrow.insertCell();
        const name = hrow.insertCell();
        let type = data.Type;
        let typeData = data.Data;
        let iconClass = data.IconClass;
        let icon;
        if (data.Id === "-") {
            name.colSpan = 2;
            flags = 0;
            hrow.classList.add("MenuSeparator");
            if (data.Name) {
                const s = document.createElement("span");
                s.innerText = data.Name;
                name.appendChild(s);
            }
            type = 0;
            typeData = null;
            iconClass = null;
        } else {
            name.innerText = data.Name;
            icon = hrow.insertCell();
        }
        const c = data.Children;
        const cl = c ? c.length : 0;
        const canExpand = cl > 0;

        if (canExpand) {
            hrow.classList.add("SubMenu");
        }

        const disabled = (flags & 1) !== 0;
        const checked = (flags & 2) !== 0;

        let onclick = null;
        let url = null;
        if (typeof typeData === "function") {
            onclick = async ev => {
                if (badClick(ev))
                    return;
                try {
                    if (await typeData(data, style, !!ev)) {
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    }
                }
                catch (ex) {
                    console.warn("Menu: Can't run function, error: " + ex);
                }
            }

        } else {
            switch (type) {
                case 1:
                    url = "explore/table.html?q=../" + typeData;
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        if (ev && (hrow == this.Selected))
                            return;
                        this.EmbeddContent(url, style);
                        this.Select(hrow);
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    };
                    break;
                case 2:
                    url = typeData;
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        if (ev && (hrow == this.Selected))
                            return;
                        this.EmbeddContent(url, style);
                        this.Select(hrow);
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    }
                    break;
                case 3:
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                        window.location.href = typeData;
                    }
                    break;
                case 4:
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                        window.open(typeData);
                    }
                    break;
                case 5:
                    onclick = async ev => {
                        if (badClick(ev))
                            return;
                        try {
                            const fn = window[typeData];
                            if (typeof fn === "function") {
                                if (await fn(data, style, !!ev)) {
                                    const fn = style.HideFn;
                                    if (fn)
                                        fn();
                                }
                            } else {
                                if (eval(typeData)) {
                                    const fn = style.HideFn;
                                    if (fn)
                                        fn();
                                }
                            }
                        }
                        catch (ex) {
                            console.warn("Menu: Can't evalute js '" + typeData + "', error: " + ex);
                        }
                    }
                    break;
                case 6:
                    url = "chart/chart.html?q=../" + typeData;
                    onclick = ev => {
                        if (badClick(ev))
                            return;
                        if (ev && (hrow == this.Selected))
                            return;
                        this.EmbeddContent(url, style);
                        this.Select(hrow);
                        const fn = style.HideFn;
                        if (fn)
                            fn();
                    };
                    break;
            }
        }
        if (url) {
            if (url.indexOf("://") < 0) {
                const uri = window.location;
                let p = uri.pathname;
                if (!p.endsWith("/"))
                    p = p.substring(0, p.lastIndexOf('/') + 1);
                url = uri.origin + p + url;
            }
            this.RowMap.set(url, hrow);
        }

        const irow = canExpand ? toElement.insertRow() : null;

        const expIconClass = style.IconExpander;
        //  Force icon for expandable
        const useExpIcon = (!iconClass) && (!onclick) && (canExpand);
        if (useExpIcon)
            iconClass = style.IconExpanderMain;

        /**
         * Toggles the child row (`MenuHide` on irow, `SubMenuHide` on hrow), persists the state in localStorage,
         * updates the row tool tip and both the item and expander icons. Null when the item has no children.
         * @param {?Event} ev - Click event (propagation is stopped), or undefined for programmatic calls.
         * @returns {void}
         */
        const expandFn = canExpand ? function (ev)
        {
            if (badClick(ev))
                return;
            if (ev)
                ev.stopPropagation();
            irow.classList.toggle("MenuHide");
            const isExpanded = !irow.classList.contains("MenuHide");
            if (isExpanded) {
                hrow.classList.remove("SubMenuHide");
                localStorage.removeItem(expKey);
            } else {
                hrow.classList.add("SubMenuHide");
                localStorage.setItem(expKey, "1");
            }
            const eic = icon["MenuIcon"];
            hrow.title = expTitle + (isExpanded ?
                _TF("Click to collapse", "The tool tip description of a button on a menu row that when pressed will hide the sub menu (child) items")
                :
                _TF("Click to expand", "The tool tip description of a button on a menu row that when pressed will show the sub menu (child) items")
            );
            if (eic)
                MainMenu.SetExpIcon(eic, isExpanded, iconClass);
            const eic2 = exp["MenuIcon"];
            if (eic2)
                MainMenu.SetExpIcon(eic2, isExpanded, expIconClass);
        } : null;

        if (!onclick)
            onclick = expandFn;
   
        if (disabled) {
            hrow.classList.add("MenuDisabled");
            onclick = null;
        }
        if (checked)
            hrow.classList.add("MenuChecked");

        data.OnClick = onclick;
        hrow.onclick = onclick;
        if (onclick)
            hrow.classList.add("MenuClick");

        let iconH = null;
        //const iconWidth = "calc(var(--ThemeIconSize)*" + style.IconWidth + "px)";
        if (iconClass) {
            iconH = new ColorIcon(null, canExpand ? style.SubIconColorClass : style.IconColorClass, style.IconWidth, style.IconHeight, null, onclick);
            MainMenu.SetIcon(iconH, iconClass)
            icon.appendChild(iconH.Element);
            //icon.style.width = iconWidth;
            //icon.style.minWidth = iconWidth;
            icon["MenuIcon"] = iconH;
            hrow["MenuIcon"] = iconH;
        } 

        //exp.style.width = (style.IconWidth / 2) + "px";

        if (!canExpand)
            return false;

 

        const isExpanded = localStorage.getItem(expKey) !== "1";
        if (!isExpanded) {
            hrow.classList.add("SubMenuHide");
            irow.classList.add("MenuHide");
        }
        hrow.title = expTitle + (isExpanded ?
            _TF("Click to collapse", "The tool tip description of a button on a menu row that when pressed will hide the sub menu (child) items")
            :
            _TF("Click to expand", "The tool tip description of a button on a menu row that when pressed will show the sub menu (child) items")
        );
        MainMenu.SetExpIcon(iconH, isExpanded, iconClass)
        const iconE = new ColorIcon("", style.SubIconColorClass, style.ExpandIconWidth, style.ExpandIconHeight, null, expandFn);
        MainMenu.SetExpIcon(iconE, isExpanded, expIconClass);
        const expIconWidth = style.ExpandIconWidth + "px";
        //exp.style.width = expIconWidth;
        //exp.style.minWidth = expIconWidth;
        exp["MenuIcon"] = iconE;
        exp.appendChild(iconE.Element);
//        irow.insertCell();
        const items = irow.insertCell();
        items.setAttribute("colspan", "4");
        irow.classList.add("MenuNested");

        const nt = document.createElement("table");
        nt.setAttribute("cellspacing", "0");
        nt.setAttribute("cellpadding", "0");
        items.appendChild(nt);
        const nextDepth = depth + 1;
        for (let i = 0; i < cl; ++i)
            this.Add(nt, c[i], nextDepth, style);
        return true;
    }


}


/**
 * Creates a `<SysWeaver-TabBlock>` element with tabIndex 0, used as a focus sentinel at the end of a pop-up
 * (appended by `PopUpElement`).
 * @returns {HTMLElement} The new (unattached) element.
 */
function CreateTabBlock() {
    const e = document.createElement("SysWeaver-TabBlock");
    e.tabIndex = "0";
    return e;
}

/**
 * Creates a "blocker" element that captures all clicks, dims the background etc.
 * Returns [blocker element, async close function]
 * The `<SysWeaver-Blocker>` (not yet attached; the caller appends it) traps keyboard focus (`BlockTab`), gets
 * tabIndex 0, stops propagation of key events, and closes on a primary mouse-down plus mouse-up both on the
 * backdrop itself or on Escape - unless `dontAllowClose`.
 * @param {boolean} dontAllowClose If true, the blocker can't be removed be pressing 'Esc' (or clicking the backdrop).
 * @param {function():Promise} onClose Optional async function that get's called when closed.
 * @returns {array} [0] = The HTMLElement of the blocker, [1] = an async function that when called removes the blocker.
 */
function CreatePopUpBlocker(dontAllowClose, onClose) {
    const block = document.createElement("SysWeaver-Blocker");

    BlockTab(block);


    /**
     * Closes the blocker: awaits `onClose`, calls `UnblockTab`, removes the element and finally calls the
     * optional expando `Close.EndFn` (set by callers that wait for the pop-up to close).
     * @returns {Promise<void>}
     */
    async function Close() {
        if (onClose)
            await onClose();
        UnblockTab(block);
        block.remove();
        const endFn = Close.EndFn;
        if (endFn)
            endFn();
    }

    block.tabIndex = "0";

    let gotClick = false;
    block.onmousedown = ev => {
        gotClick = false;
        if (badClick(ev, true))
            return;
        gotClick = ev.target === block;
    };

    block.onmouseup = ev => {
        if (!gotClick)
            return;
        gotClick = false;
        if (ev.target !== block)
            return;
        if (badClick(ev))
            return;
        if (!dontAllowClose)
            Close();
    };


    block.onkeydown = ev => {
        ev.stopPropagation();
        if (isPureClick(ev)) {
            if (ev.key === "Escape") {
                if (!dontAllowClose) {
                    ev.preventDefault();
                    Close();
                }
            }
        }
    };

    return [block, Close];
}


/**
 * Creates a `<SysWeaver-PopupMenu>` inside a new blocker (`CreatePopUpBlocker`), positioned with fixed
 * coordinates computed once from `element.getBoundingClientRect()`. The blocker is not attached to the DOM.
 * Position: left = rect.x + widthScale * rect.width + offsetX, top = rect.y + heightScale * rect.height + offsetY.
 * Non-number scale/offset arguments default to 0.
 * @param {HTMLElement} element The element to position the pop-up relative to
 * @param {number} widthScale Optional scaling of the horizontal position: dx = widthScale * element.width + offsetX
 * @param {number} heightScale Optional scaling of the vertical position: dy = heightScale * element.height + offsetY.
 * @param {number} offsetX Optional offset of the horizontal position: dx = widthScale * element.width + offsetX
 * @param {number} offsetY Optional offset of the vertical position: dy = heightScale * element.height + offsetY.
 * @param {bool} dontAllowClose If true, the pop-up can't be closed besides calling the returned close function.
 * @param {function():Promise} onClose Optional async function that get's called when closed.
 * @returns {array} [0] = The HTMLElement to add pop-up content to, [1] = an async function that when called removes the blocker. [2] = The HTMLElement of the blocker.
 */
function CreatePopUpAtElement(element, widthScale, heightScale, offsetX, offsetY, dontAllowClose, onClose) {
    if (typeof widthScale !== "number")
        widthScale = 0;
    if (typeof heightScale !== "number")
        heightScale = 0;
    if (typeof offsetX !== "number")
        offsetX = 0;
    if (typeof offsetY !== "number")
        offsetY = 0;

    const popupMenuElement = document.createElement("SysWeaver-PopupMenu");
    /**
     * Sets the pop-up's left/top style (px) from the current bounding rect of `element` (see above).
     * @returns {void}
     */
    function setPos() {

        const rect = element.getBoundingClientRect();
        const dx = rect.width * widthScale + offsetX + rect.x;
        const dy = rect.height * heightScale + offsetY + rect.y;
        popupMenuElement.style.left = dx + "px";
        popupMenuElement.style.top = dy + "px";
    }

    setPos();
/*    
    //new ResizeObserver(setPos).observe(document.body);
    */

    /*
    const s = window.getComputedStyle(element);
    const disp = s.getPropertyValue("position");
    if (disp == "absolute") {
        const dx = parseInt(s.getPropertyValue("width").split('p')[0]) * -0.25;
        const dy = parseInt(s.getPropertyValue("height").split('p')[0]) + 8;
        popupMenuElement.style.left = dx + "px";
        popupMenuElement.style.top = dy + "px";
    }
    */
    const bec = CreatePopUpBlocker(dontAllowClose, onClose);
    const block = bec[0];
    const close = bec[1];
    block.appendChild(popupMenuElement);
    //popupMenuElement.appendChild(block);
    return [popupMenuElement, close, block];
}

/**
 * Create a popup menu relative to some element
 * Opens a closable `PopUpElementMenu` (not blocking) whose `<SysWeaver-PopupMenuBack>` gets class `DefaultMenu`
 * and renders the `WebMenu` returned by `build` with a new `MainMenu`. The menu style's `Content` is the page's
 * `<app-content>` (looked up by tag name), so Embedded/Table/Chart items load into the shell's content area;
 * `HideFn` closes the pop-up. Its `ExternalContentFn` hides `<app-ContentTitle>` (class `Hidden`) when there is no
 * title, otherwise sets its text/tool tip and `document.title = window.SysWeaverAppTitle + " - " + title`, and
 * always assigns the undeclared global `contentUrl`. Note: the returned promise is not awaited / returned.
 * @param {HTMLElement} element The element used to position the pop-up.
 * @param {function(function():Promise<void>, HTMLElement):WebMenu} build Function (called synchronously, result not awaited) that receives the close function and the menu back element and returns the WebMenu to show.
 * @param {boolean} rightAlign If true the pop-up is right aligned (else it left aligned).
 * @param {boolean} bottomAlign If true the pop-up is bottom aligned (else it top aligned).
 * @returns {void}
 */
function PopUpMenu(element, build, rightAlign, bottomAlign) {

    PopUpElementMenu(element, (popupMenuBackElement, close) =>
    {
        popupMenuBackElement.classList.add("DefaultMenu");

        const menuStyle = new MainMenuStyle();
        const appContent = document.getElementsByTagName("app-content")[0];
        const appHeaderMiddle = document.getElementsByTagName("app-ContentTitle")[0];
        menuStyle.HideFn = close;
        menuStyle.Content = appContent;
        /**
         * Updates the shell title (`<app-ContentTitle>` and document.title) for content opened from this pop-up
         * menu and stores `url` in the implicit global `contentUrl`.
         * @param {string} [url] - New content URL (undefined when navigation starts).
         * @param {string} [title] - New content title; falsy hides the title element.
         * @returns {void}
         */
        menuStyle.ExternalContentFn = (url, title) => {
            if (!title) {
                appHeaderMiddle.classList.add("Hidden");
            } else {
                appHeaderMiddle.classList.remove("Hidden");
                appHeaderMiddle.innerText = title;
                appHeaderMiddle.title = title;
                document.title = window.SysWeaverAppTitle + " - " + title;
            }
            contentUrl = url;
        };
        const menu = build(close, popupMenuBackElement);
        const popupMenu = new MainMenu(menu, menuStyle, popupMenuBackElement);
    }, false, false, null, rightAlign, bottomAlign);

}


/**
 * Create a visual pop-up that blocks input behind, positioned and tracked using some element as anchor.
 * The position can be adjusted, like: dx = widthScale * element.width + offsetX, dy = heightScale * element.height + offsetY.
 * @param {HTMLElement} element The element used to position the pop-up.
 * @param {function(HTMLElement, function():Promise<void>):Promise<void>} build Async function that should build the visual DOM on the supplied element. First argument is the HTMLElement to put content, second is an async function that closes the PopUp and continues execution.
 * @param {boolean} blockUntilClosed If true, the function will block (on the promise) until it's closed.
 * @param {boolean} dontAllowClose If true, no close button will be available and it can't be closed by clicking outside (the closePopup function must be called).
 * @param {function():Promise<void>} onClose An optional function to call when the pop-up is closed.
 * @param {number} widthScale Part of the horizontal offset of the anchor point, as in a fraction of the element width (offsetX += widthScale * element.width).
 * @param {number} heightScale Part of the vertical offset of the anchor point, as in a fraction of the element height (offsetY += heightScale * element.height).
 * @param {number} offsetX The horizontal offset of the anchor point in pixels.
 * @param {number} offsetY The vertical offset of the anchor point in pixels.
 * Creates the pop-up with `CreatePopUpAtElement` (sets the expando `page.Close`), toggles class `Scroll` on the
 * blocker via a ResizeObserver when the content overflows, appends the blocker to `document.body` and focuses
 * it, awaits `build`, then appends a `CreateTabBlock()` sentinel. When blocking, waits until `close.EndFn` runs.
 * The position is computed once (not tracked afterwards).
 * @returns {Promise<void>}
 */
async function PopUpElement(element, build, blockUntilClosed, dontAllowClose, onClose, widthScale, heightScale, offsetX, offsetY) {

    const bec = CreatePopUpAtElement(element, widthScale, heightScale, offsetX, offsetY, dontAllowClose, onClose);
    const page = bec[0];
    const close = bec[1];
    const block = bec[2];
    page.Close = close;
    new ResizeObserver(function () {
        if (page.scrollHeight > page.clientHeight)
            block.classList.add("Scroll");
        else
            block.classList.remove("Scroll");
    }).observe(page)

    document.body.appendChild(block);
    block.focus();

    await build(page, close);

    page.appendChild(CreateTabBlock());
    if (blockUntilClosed) {
        await waitFor(async endUsing => {
            close.EndFn = endUsing;
        });
    }
}

/**
 * Create a visual pop-up that blocks input behind, positioned and tracked using some element as anchor.
 * Useful for context menues etc.
 * @param {HTMLElement} element The element used to position the pop-up.
 * @param {function(HTMLElement, function():Promise<void>):Promise<void>} build Async function that should build the visual DOM on the supplied element. First argument is the HTMLElement to put content, second is an async function that closes the PopUp and continues execution.
 * @param {boolean} blockUntilClosed If true, the function will block (on the promise) until it's closed.
 * @param {boolean} dontAllowClose If true, no close button will be available and it can't be closed by clicking outside (the closePopup function must be called).
 * @param {function():Promise<void>} onClose An optional function to call when the pop-up is closed.
 * @param {boolean} rightAlign If true the pop-up is right aligned (else it left aligned).
 * @param {boolean} bottomAlign If true the pop-up is bottom aligned (else it top aligned).
 * Uses `PopUpElement` with anchor scale x = -0.25 (right aligned) or -0.75 (left aligned), y = 0 / offset -8
 * (bottom aligned) or y = 1 / offset +8 (below the element), and builds into a `<SysWeaver-PopupMenuBack>`
 * (class `Right` when right aligned, expando `Close`). Note: `build` receives (backElement, close) here.
 * @returns {Promise<void>}
 */
async function PopUpElementMenu(element, build, blockUntilClosed, dontAllowClose, onClose, rightAlign, bottomAlign) {

    const scaleX = rightAlign ? -0.25 : -0.75;
    const scaleY = bottomAlign ? 0 : 1;
    const offsetY = bottomAlign ? -8 : 8;
    await PopUpElement(element, async (toE, close) => {
        const popupMenuBackElement = document.createElement("SysWeaver-PopupMenuBack");
        toE.appendChild(popupMenuBackElement);
        if (rightAlign)
            popupMenuBackElement.classList.add("Right");
        popupMenuBackElement.Close = close;
        await build(popupMenuBackElement, close);
    }, blockUntilClosed, dontAllowClose, onClose, scaleX, scaleY, 0, offsetY);
}


/**
 * Create a visual pop-up that blocks input behind.
 * @param {function(HTMLElement, function():Promise<void>, HTMLElement):Promise<void>} build Async function that should build the visual DOM on the supplied element. First argument is the HTMLElement to put content, second is an async function that closes the PopUp and continues execution. Third argument is the container for the close button, other button may be added here (can be null).
 * @param {boolean} blockUntilClosed If true, the function will block (on the promise) until it's closed.
 * @param {boolean} dontAllowClose If true, no close button will be available and it can't be closed by clicking outside (the closePopup function must be called).
 * @param {function():Promise<void>} onClose An optional function to call when the pop-up is closed.
 * @param {boolean} haveCloseButton If true, a close button will be visible (unless dontAllowClose is true).
 * @returns {function():Promise<void>} The async function to use for closing the pop-up (only useful when blockUntilClosed is false).
 * DOM: a `CreatePopUpBlocker` blocker containing a focusable `<SysWeaver-PopUpBackground>` (clicks do not
 * propagate) with expandos `Close`, `Block`, `MakeFullWidth()` (adds `FullWidth` to the blocker) and
 * `MakeMaxHeight()` (adds `MaxHeight`). A ResizeObserver toggles `Scroll` on the blocker on overflow.
 * `haveCloseButton` defaults to `!dontAllowClose`; when set, a `<SysWeaver-BlockerCloseButton>` is passed to
 * `build` and afterwards appended to the page with an `IconClosePopUp` ColorIcon that calls Close.
 * When blocking, the promise resolves (with undefined) once Close has run (`Close.EndFn`).
 */
async function PopUp(build, blockUntilClosed, dontAllowClose, onClose, haveCloseButton) {

    if (typeof haveCloseButton === "undefined")
        haveCloseButton = !dontAllowClose;

    const bec = CreatePopUpBlocker(dontAllowClose, onClose);
    const block = bec[0];
    const Close = bec[1];


    const page = document.createElement("SysWeaver-PopUpBackground");
    page.onclick = ev => ev.stopPropagation();
    page.Close = Close;
    page.Block = block;
    /**
     * Makes the pop-up use the full width (adds class `FullWidth` to the blocker).
     * @returns {void}
     */
    page.MakeFullWidth = () => block.classList.add("FullWidth");
    /**
     * Makes the pop-up use the maximum height (adds class `MaxHeight` to the blocker).
     * @returns {void}
     */
    page.MakeMaxHeight = () => block.classList.add("MaxHeight");
    block.appendChild(page);
    page.tabIndex = "0";
    /*
    if (haveCloseButton) {
        const blockClose = document.createElement("SysWeaver-BlockerClose");
        blockClose.onclick = ev => ev.stopPropagation();
        block.appendChild(blockClose);
        const cb = document.createElement("SysWeaver-BlockerCloseButton");
        blockClose.appendChild(cb);
        const closeButton = new ColorIcon("IconClosePopUp", "IconColorThemeAcc1", 32, 32, "Click to close pop-up", Close);
        cb.appendChild(closeButton.Element);
    }*/

    new ResizeObserver(function () {
        if (page.scrollHeight > page.clientHeight)
            block.classList.add("Scroll");
        else
            block.classList.remove("Scroll");
    }).observe(page)

    document.body.appendChild(block);
    const cb = haveCloseButton ? document.createElement("SysWeaver-BlockerCloseButton") : null;
    await build(page, Close, cb);

    //page.appendChild(CreateTabBlock());

    if (haveCloseButton) {
        page.appendChild(cb);
        const closeButton = new ColorIcon("IconClosePopUp", "IconColorThemeAcc1", 32, 32,
            _TF("Click to close pop-up", "The tool tip description on a button that when pressed will close a pop-up dialog"),
            Close);
        cb.appendChild(closeButton.Element);
    }

    page.focus();

    if (blockUntilClosed) {
        await waitFor(async endUsing => {
            Close.EndFn = endUsing;
        });
    } else {
        return Close;
    }
}

/**
 * Pop-up a "working" "in progress" pop-up
 * @param {string} title Optional title of the pop-up
 * @param {string} text Optional text of the pop-up
 * @param {string} iconClass Optional icon class name or uri
 * @param {number} iconWidth Optional width in pixels of the icon (defaults to 64)
 * @param {number} iconHeight Optional height in pixels of the icon (defaults to 64)
 * @returns {function()} The function to execute to close the pop-up
 * (Actually a Promise resolving to that async close function.) The pop-up cannot be dismissed by the user and
 * has no close button. Content: optional `<SysWeaver-PopUpWorkingTitle>`, a `<SysWeaver-PopUpWorkingIcon>` with
 * a 256x256 `IconWorking`, optional `<SysWeaver-PopUpWorkingText>` and, if `iconClass` is given, a second
 * `<SysWeaver-PopUpWorkingIcon>` with that icon.
 */
async function PopUpWorking(title, text, iconClass, iconWidth, iconHeight) {
    return PopUp(el => {
        if (title) {
            const t = document.createElement("SysWeaver-PopUpWorkingTitle");
            t.innerText = title;
            el.appendChild(t);
        }
        {
            const i = document.createElement("SysWeaver-PopUpWorkingIcon");
            el.appendChild(i);
            const ci = new ColorIcon("IconWorking", "IconColorThemeMain", 256, 256);
            i.appendChild(ci.Element);
        }
        if (text) {
            const t = document.createElement("SysWeaver-PopUpWorkingText");
            t.innerText = text;
            el.appendChild(t);
        }
        if (iconClass) {
            const i = document.createElement("SysWeaver-PopUpWorkingIcon");
            el.appendChild(i);
            if (!iconWidth)
                iconWidth = 64;
            if (!iconHeight)
                iconHeight = 64;
            const ci = new ColorIcon(iconClass, "IconColorThemeMain", iconWidth, iconHeight);
            i.appendChild(ci.Element);
        }
    }, false, true);
}


/**
 * Pop up a selection of custom items that are searchable
 * DOM inside a `PopUp`: `<SysWeaver-PopUpText>` (text, tool tip = title), `<SysWeaver-PopUpFilter>` holding the
 * input (placeholder = title) and an `IconClearInput` clear button, and `<SysWeaver-SelectList>` holding the
 * elements returned by `getItems`. Enter (without modifiers) clicks the first list element. Results of a search
 * are dropped if the input changed meanwhile; identical consecutive searches are skipped.
 * Note: the first parameter is named `text` in code (documented as `header` below).
 * @returns {Promise<void>} Resolves when built, or when closed if `blockUntilClosed`.
 * @param {string} header Header text
 * @param {string} title Header title (tool tip)
 * @param {function(string, function):HTMLElement[]} getItems Get a list of items as HTML elements given the search text, the function closes the dialog (use when select)
 * @param {string} startText Optional initial search text
 * @param {boolean} getAll If true getItems will be called even if the search text is empty.
 * @param {boolean} blockUntilClosed Block the thread until the pop-up is closed.
 * @param {boolean} dontAllowClose If true, the pop-up can't be closed and closeFn must be called to close it.
 * @param {function} onClose Optional callback that is called when the pop-up is closed.
 */
async function PopUpSelection(text, title, getItems, startText, getAll, blockUntilClosed, dontAllowClose, onClose) {

    await PopUp(async (page, close) => {


        const textE = document.createElement("SysWeaver-PopUpText");
        page.appendChild(textE);
        textE.innerText = text;
        textE.title = title;


        const inputRow = document.createElement("SysWeaver-PopUpFilter");
        page.appendChild(inputRow);

        const inputE = document.createElement("input");
        inputRow.appendChild(inputE);
        inputE.tabIndex = "0";
        inputE.placeholder = title;
        if (startText)
            inputE.value = startText;

        const items = document.createElement("SysWeaver-SelectList");
        page.appendChild(items);

        let prevSearch = null;
        /**
         * Runs `getItems` for the current input value (skipped when empty unless `getAll`, or when equal to the
         * previous search) and replaces the list content, unless the input changed while waiting.
         * @returns {Promise<void>}
         */
        async function update() {
            const search = inputE.value;
            if (!getAll) {
                if (!search)
                    return;
                if (search.length <= 0)
                    return;
            }
            if (search == prevSearch)
                return;
            prevSearch = search;
            const r = await getItems(search, close);
            if (inputE.value != search)
                return;
            const rl = r.length;
            items.innerText = "";
            for (let i = 0; i < rl; ++i)
                items.appendChild(r[i]);
        }

        inputE.focus();
        await update();

        /**
         * Input change handler: enables the clear button only when there is text, then runs `update`.
         * @returns {Promise<void>}
         */
        const filterChangeFn = async () =>
        {
            clearButton.SetEnabled(!!inputE.value);
            await update();
        };

        inputE.oninput = filterChangeFn;

        inputE.onchange = filterChangeFn;

        inputE.onkeydown = async ev => {
            if (ev.key !== "Enter")
                return;
            if (!isPureClick(ev))
                return;
            ev.stopPropagation();
            const fe = items.firstElementChild;
            if (fe) {
                fe.click();
            }
        }

        const clearButton = new ColorIcon("IconClearInput", "IconColorThemeAcc1", 35, 35,
            _TF("Click to clear filter", "The tool tip description on a button that when pressed will clear the search filter"),
            async ev => {
                inputE.value = "";
                await filterChangeFn();
            });
        clearButton.SetEnabled(!!inputE.value);
        inputRow.appendChild(clearButton.Element);


    }, blockUntilClosed, dontAllowClose, onClose);
}

/**
 * Pop-up a property editor and wait until it's closed or accepted
 * @param {string|object} type The dotnet name of the type to edit or a type specifier object
 * @param {any} obj An optional object of that type to edit (inital value)
 * @param {string} buttonText Text on the accept button
 * @param {string} buttonIcon Optional, class name or icon url 
 * @param {string} buttonToolTip Tool tip for the button
 * @param {string} title Optional title
 * @param {string} titleToolTip Optional tool tip on the title
 * @param {string} primName If the type is primitve, use this as the display name
 * @returns {Promise<?object>} The edited object (`Edit.GetObject()`) if the accept button was pressed, else null.
 * Shows a blocking `PopUp` with an optional `<SysWeaver-ConfTitle>`, an `Edit` property editor (from the edit/
 * scripts, `EditOptions` Title/Border off) and a `<SysWeaver-ConfButtons>` row with the accept `Button`.
 * A string `type` is first resolved with `sendRequest("../edit/GetTypeInfo", type)` (Fail + empty pop-up on
 * error); for primitive types (`TypeMemberFlags.IsPrimitive`) the display name is primName ?? title ?? "Value".
 * NOTE: this function is declared twice in this file with identical bodies; this first declaration is
 * overridden by the second one below.
 */
async function PopUpEdit(type, obj, buttonText, buttonIcon, buttonToolTip, title, titleToolTip, primName) {
    let ret = null;
    await PopUp(async (e, closeFn) => {
        if (title) {
            const t = document.createElement("SysWeaver-ConfTitle");
            t.innerText = title;
            if (titleToolTip)
                t.title = titleToolTip;
            e.appendChild(t);
        }
        if (typeof type === "string") {
            try {
                type = await sendRequest("../edit/GetTypeInfo", type);
                if (!type) {
                    Fail(_TF("Invalid typename!", "Error message displayed when a type with the given name wasn't found"));
                    return;
                }
            }
            catch (e) {
                Fail(_TF("Failed to get type info", "Error message displayed when there was a failure to get type information for the given type") + ". " + e.message);
                return;
            }
            if ((type.Flags & TypeMemberFlags.IsPrimitive) !== 0) {
                const n = primName ?? title ?? _TF("Value", "Short header of a property");
                type.DisplayName = n;
                type.Members[0].DisplayName = n;
            }
        }
        const opt = new EditOptions();
        opt.Title = false;
        opt.Border = false;
        const pe = new Edit(type, undefined, opt);
        await pe.SetObject(obj)
        e.appendChild(pe.Element);
        pe.Element.style.marginTop = "3em";

        const bs = document.createElement("SysWeaver-ConfButtons");
        const apply = new Button(null, buttonText ?? _TF("OK", "Text on a button that when pressed will accept"), buttonToolTip, buttonIcon, true, () => {
            ret = pe.GetObject();
            closeFn();
        });
        bs.appendChild(apply.Element);
        e.appendChild(bs);
    }, true);
    return ret;
}
/**
 * Pop-up a property editor and wait until it's closed or accepted
 * @param {string|object} type The dotnet name of the type to edit or a type specifier object
 * @param {any} obj An optional object of that type to edit (inital value)
 * @param {string} buttonText Text on the accept button
 * @param {string} buttonIcon Optional, class name or icon url 
 * @param {string} buttonToolTip Tool tip for the button
 * @param {string} title Optional title
 * @param {string} titleToolTip Optional tool tip on the title
 * @param {string} primName If the type is primitve, use this as the display name
 * @returns {Promise<?object>} The edited object if the accept button was pressed, else null.
 * NOTE: second (effective) declaration of PopUpEdit; identical to the one above, see its comment for details.
 */
async function PopUpEdit(type, obj, buttonText, buttonIcon, buttonToolTip, title, titleToolTip, primName) {
    let ret = null;
    await PopUp(async (e, closeFn) => {
        if (title) {
            const t = document.createElement("SysWeaver-ConfTitle");
            t.innerText = title;
            if (titleToolTip)
                t.title = titleToolTip;
            e.appendChild(t);
        }
        if (typeof type === "string") {
            try {
                type = await sendRequest("../edit/GetTypeInfo", type);
                if (!type) {
                    Fail(_TF("Invalid typename!", "Error message displayed when a type with the given name wasn't found"));
                    return;
                }
            }
            catch (e) {
                Fail(_TF("Failed to get type info", "Error message displayed when there was a failure to get type information for the given type") + ". " + e.message);
                return;
            }
            if ((type.Flags & TypeMemberFlags.IsPrimitive) !== 0) {
                const n = primName ?? title ?? _TF("Value", "Short header of a property");
                type.DisplayName = n;
                type.Members[0].DisplayName = n;
            }
        }
        const opt = new EditOptions();
        opt.Title = false;
        opt.Border = false;
        const pe = new Edit(type, undefined, opt);
        await pe.SetObject(obj)
        e.appendChild(pe.Element);
        pe.Element.style.marginTop = "3em";

        const bs = document.createElement("SysWeaver-ConfButtons");
        const apply = new Button(null, buttonText ?? _TF("OK", "Text on a button that when pressed will accept"), buttonToolTip, buttonIcon, true, () => {
            ret = pe.GetObject();
            closeFn();
        });
        bs.appendChild(apply.Element);
        e.appendChild(bs);
    }, true);
    return ret;
}

/**
 * Popup a confirm dialog that let the user acccept or reject something
 * @param {string} title Title of the confirmation dialog
 * @param {string} text Text of the confirmation dialog
 * @param {string} okButtonText Text on the OK button
 * @param {string} cancelButtonText Text on the CANCEL button
 * @param {string} okButtonImageClass Optional image to use on the OK button (class name or image url)
 * @param {string} cancelButtonImageClass Optional image to use on the CANCEL button (class name or image url)
 * @param {string} okButtonTitle Optional tool tip text on the OK button
 * @param {string} cancelButtonTitle Optional tool tip text on the CANCEL button
 * @param {string} okStyle Optional css class to add to the OK button
 * @param {string} cancelStyle Optional css class to add to the CANCEL button
 * @returns {boolean} True if the user pressed the ok button
 * (A Promise<boolean>; resolves when the blocking pop-up closes - Cancel, Escape or backdrop click give false.)
 * DOM: optional `<SysWeaver-ConfTitle>` / `<SysWeaver-ConfText>` (clicking copies the text to the clipboard via
 * `ValueFormat.copyOnClick`) and a `<SysWeaver-ConfButtons>` row with OK (default icon `IconOk`, text "Ok") and
 * Cancel (default icon `IconCancel`, text "Cancel") `Button`s.
 */
async function Confirm(title, text, okButtonText, cancelButtonText, okButtonImageClass, cancelButtonImageClass, okButtonTitle, cancelButtonTitle, okStyle, cancelStyle)
{
    let ok = false;
    await PopUp(async (to, close) => {
        if (title) {
            const te = document.createElement("SysWeaver-ConfTitle");
            te.innerText = title;
            te.title = title + "\n\n" + _TF("Click to copy text to clipboard", "A tool tip description on a confirmation dialog title that when clicked will copy the title text to the clipboard");
            ValueFormat.copyOnClick(te, title);
            to.appendChild(te);
        }
        if (text) {
            const te = document.createElement("SysWeaver-ConfText");
            te.innerText = text;
            te.title = text + "\n\n" + _TF("Click to copy text to clipboard", "A tool tip description on a confirmation dialog text area that when clicked will copy the text to the clipboard");
            ValueFormat.copyOnClick(te, text);
            to.appendChild(te);
        }
        const b = document.createElement("SysWeaver-ConfButtons");
        to.appendChild(b);
        if (!okButtonText)
            okButtonText = _TF("Ok", "The text of a button on a confirmation dialog that when pressed will confirm that the user wants to perform the action");
        const okButton = new Button(okStyle ?? null, okButtonText, okButtonTitle ?? "", okButtonImageClass ?? "IconOk", true, () => {
            ok = true;
            close();
        });
        b.appendChild(okButton.Element);
        if (!cancelButtonText)
            cancelButtonText = _TF("Cancel", "The text of a button on a confirmation dialog that when pressed will close the dialog without performing the action");
        const cancelButton = new Button(cancelStyle ?? null, cancelButtonText, cancelButtonTitle ?? "", cancelButtonImageClass ?? "IconCancel", true, close);
        b.appendChild(cancelButton.Element);
    }, true);
    return ok;
}

/**
 * A reusable, paged, searchable selection pop-up that remembers the last filter and a most-recently-used list
 * of ids in `localStorage["SysWeaver.PopUpRecent.<id>"]` (JSON `{Filter: string, Recent: any[]}`).
 * Create once with the callbacks, then call `Show(...)` each time the selection is needed.
 * Used e.g. by edit/EditTypeLanguage.js.
 */
class SearchablePoupSelection {
    /** localStorage key "SysWeaver.PopUpRecent.<id>". @type {string} */
    Key;
    /** Creates the list element for a data item: `(data, onSelect(data), isRecent) => HTMLElement`. @type {function(any, function(any):Promise<void>, boolean):HTMLElement} */
    CreateElementFn;
    /** Finds items: `async (filter, recentMap, count, offset) => any[]` (recentMap: recent id -> 1-based MRU position). @type {function(string, Map<any,number>, number, number):Promise<any[]>} */
    FindFn;
    /** Maximum number of recent ids kept. @type {number} */
    MaxRecent;
    /** Returns the id to remember for a data item. @type {function(any):any} */
    GetRecentIdFn;
    /**
     * @param {string} id - Unique id of this selection, used in the storage key.
     * @param {function(any):any} getRecentIdFn - Maps a data item to its id (stored in the MRU list).
     * @param {function(any, function(any):Promise<void>, boolean):HTMLElement} createElementFn - Builds a list element; must call the given select function when chosen.
     * @param {function(string, Map<any,number>, number, number):Promise<any[]>} findFn - Search callback (filter, recentMap, maxCount, offset).
     * @param {number} [maxRecent=10] - Size of the MRU list (falsy -> 10).
     */
    constructor(id, getRecentIdFn, createElementFn, findFn, maxRecent) {
        if (!maxRecent)
            maxRecent = 10;
        this.Key = "SysWeaver.PopUpRecent." + id;
        this.CreateElementFn = createElementFn;
        this.FindFn = findFn;
        this.MaxRecent = maxRecent;
        this.GetRecentIdFn = getRecentIdFn;
    }


    /**
     * Shows the selection in a `PopUp`: `<SysWeaver-PopUpText>` title, `<SysWeaver-PopUpFilter>` (input initialised
     * with the stored filter + `IconClearInput` clear button) and `<SysWeaver-PopSelData>` with the results. Results
     * are fetched `maxView + 1` at a time; if more exist, a `<SysWeaver-PopSelEnd>` with an `IconMoreData` button
     * loads the next page (appended), else it says "No more data!" ("Nothing found!" when empty). Enter selects
     * the first result. Selecting calls `onSelect` (errors ignored), moves the item id to the front of the MRU
     * list, saves the state and closes the pop-up. The filter is saved when a new first page is fetched.
     * @param {string} titleText - Title text.
     * @param {function(any):Promise<void>} [onSelect] - Called with the selected data item.
     * @param {number} [maxView=20] - Page size.
     * @param {string} [titleToolTip] - Title tool tip (defaults to titleText).
     * @param {string} [filterPlaceholder] - Input placeholder (default "Enter some text to filter").
     * @param {boolean} [blockUntilClose] - Wait until the pop-up is closed.
     * @param {boolean} [dontAllowClose] - Prevent user dismissal.
     * @param {function():Promise<void>} [onClose] - Called when closed.
     * @returns {Promise<void>}
     */
    async Show(titleText, onSelect, maxView, titleToolTip, filterPlaceholder, blockUntilClose, dontAllowClose, onClose) {
        if (!maxView)
            maxView = 20;
        let state = {
            Filter: "",
            Recent: [],
        };
        let prevState = localStorage.getItem(this.Key);
        if (prevState) {

            try {
                prevState = JSON.parse(prevState);
                Object.assign(state, prevState);
                if (typeof state.Filter !== "string")
                    state.Filter = "";
                const r = state.Recent;
                const mx = this.MaxRecent;
                if (r.length > mx)
                    r.splice(mx, r.length - mx);
            }
            catch (e) {
                console.warn("Failed to parse previous state, error: " + e.message);
                state = {
                    Filter: "",
                    Recent: [],
                };
            }
        }
        const recentMap = new Map();
        const recent = state.Recent;
        const rl = recent.length;
        for (let i = 0; i < rl; ++i)
            recentMap.set(recent[i], 1 + i);

        const createElementFn = this.CreateElementFn;


        await PopUp(async (e, closeFn) => {

            const th = this;
            const titleEl = document.createElement("SysWeaver-PopUpText");
            titleEl.innerText = titleText;
            titleEl.title = titleToolTip ?? titleText;
            const inputRow = document.createElement("SysWeaver-PopUpFilter");
            const inputEl = document.createElement("input");
            inputRow.appendChild(inputEl);
            inputEl.type = "text";
            inputEl.placeholder = filterPlaceholder ?? _TF("Enter some text to filter", "The placeholder text of an input field that is used for filtering some items");
            inputEl.value = state.Filter;

            const dataEl = document.createElement("SysWeaver-PopSelData");
            const getRecentFn = th.GetRecentIdFn;
            const findFn = th.FindFn;
            /**
             * Selects a data item: awaits `onSelect(val)` (exceptions swallowed), updates and persists the MRU
             * list (exceptions swallowed) and closes the pop-up.
             * @param {any} val - Selected data item.
             * @returns {Promise<void>}
             */
            const onSelectFn = async val => {
                if (onSelect) {
                    try {
                        await onSelect(val);
                    }
                    catch
                    {
                    }
                }
                try {
                    const recentId = getRecentFn(val);
                    const ri = recent.indexOf(recentId);
                    if (ri >= 0)
                        recent.splice(ri, 1);
                    recent.splice(0, 0, recentId);
                    const mx = th.MaxRecent;
                    if (recent.length > mx)
                        recent.splice(mx, recent.length - mx);
                    localStorage.setItem(th.Key, JSON.stringify(state));
                }
                catch
                {
                }
                closeFn();
            };
            let offset = 0;
            let lastSearch = null;
            let lastOffset = -1;
            let topSearch = null;
            /**
             * Fetches and renders results for the current filter and `offset` (skipped if neither changed).
             * Offset 0 replaces the list (and persists the filter once a previous fetch happened); a larger offset
             * replaces the trailing end element with the next page. Tracks the first result in `topSearch`.
             * @returns {Promise<void>}
             */
            const updateList = async () => {

                const filter = inputEl.value;
                if ((lastSearch === filter) && (lastOffset === offset))
                    return;
                if ((lastOffset >= 0) && (offset <= 0)) {
                    state.Filter = filter;
                    try {
                        localStorage.setItem(th.Key, JSON.stringify(state));
                    }
                    catch
                    {
                    }
                }
                lastSearch = filter;
                lastOffset = offset;

                const found = await findFn(filter, recentMap, maxView + 1, offset);
                if (offset <= 0)
                    topSearch = null;
                if (found && (found.length > 0)) {
                    if (offset <= 0)
                        dataEl.innerText = "";
                    else
                        dataEl.lastElementChild.remove();
                    let l = found.length;
                    const gotMore = l > maxView;
                    if (gotMore)
                        l = maxView;
                    for (let i = 0; i < l; ++i) {
                        const data = found[i];
                        if (!topSearch)
                            topSearch = data;
                        const ie = createElementFn(data, onSelectFn, recentMap.has(getRecentFn(data)));
                        dataEl.appendChild(ie);
                    }

                    const end = document.createElement("SysWeaver-PopSelEnd");
                    dataEl.appendChild(end);
                    if (gotMore) {
                        const moreIcon = new ColorIcon("IconMoreData", "IconColorThemeMain", 64, 64,
                            _TF("Click to get more results", "The tool tip description on a button that when clicked will view more results"),
                            async ev => {
                                moreIcon.StartWorking();
                                offset += maxView;
                                await updateList();
                            });
                        end.appendChild(moreIcon.Element);
                    } else {
                        end.innerText = _TF("No more data!", "A text that is displayed at the end of a list of items when no more items is available");
                    }
                } else {
                    if (offset <= 0) {
                        dataEl.innerText = "";
                        const end = document.createElement("SysWeaver-PopSelEnd");
                        end.innerText = _TF("Nothing found!", "A text that is displayed when the result of filtering some data yielded no results");
                        dataEl.appendChild(end);
                    } else {
                        dataEl.lastElementChild.innerText = _TF("No more data!", "A text that is displayed at the end of a list of items when no more items is available");
                    }
                }
            };

            await updateList();

            /**
             * Filter input handler: updates the clear button state, resets paging and refreshes the list.
             * @returns {Promise<void>}
             */
            const filterChangedFn = async () => {
                clearButton.SetEnabled(!!inputEl.value);
                offset = 0;
                await updateList();
            };
            inputEl.onkeydown = async ev => {
                if (ev.key !== "Enter")
                    return;
                if (!isPureClick(ev))
                    return;
                ev.preventDefault();
                ev.stopPropagation();
                if (!topSearch)
                    return;
                await onSelectFn(topSearch);
            };

            const clearButton = new ColorIcon("IconClearInput", "IconColorThemeAcc1", 35, 35,
                _TF("Click to clear filter", "The tool tip description on a button that when pressed will clear the search filter"),
                async ev => {
                    inputEl.value = "";
                    await filterChangedFn();
                });
            clearButton.SetEnabled(!!inputEl.value);
            inputRow.appendChild(clearButton.Element);

            inputEl.onchange = filterChangedFn;
            inputEl.oninput = filterChangedFn;

            e.appendChild(titleEl);
            e.appendChild(inputRow);
            e.appendChild(dataEl);
            inputEl.focus();
        }, blockUntilClose, dontAllowClose, onClose);

    }


}


/**
 * Options for `App.Create` / `App.Init`. `appMain` always uses the defaults; pass an instance to
 * `App.Create(params)` directly to customise the shell.
 */
class AppParams {

    /** Element to build the shell in (default `document.body`). @type {?HTMLElement} */
    PageElement = null;
    /** Create the main menu (`<app-menu>`) and its header button. @type {boolean} */
    MainMenu = true;
    /** Comma separated server menu names for the main menu (null -> "Default"; "" -> none). @type {?string} */
    MainMenus = null;
    /** Initial pinned state on desktop when nothing is stored in localStorage["SysWeaver.Pinned"]. @type {boolean} */
    MainMenuPinned = false;

    /** Create the `<app-content>` host. @type {boolean} */
    Content = true;
    /** Show the back button: true/false, or null = only on mobile (`window.Mobile`). @type {?boolean} */
    Navigation = null;
    /** Show the user button (pop-up with the server "User" menu; skipped fetch when SettingMenus === ""). @type {boolean} */
    UserButton = true;
    /** Show the settings button. @type {boolean} */
    SettingsButton = true;
    /** Comma separated server menus for the settings pop-up (null -> "Theme", or "User,Theme" without a user button). @type {?string} */
    SettingMenus = null;
    /** Add the "Enter/Exit full screen" item to the settings pop-up. @type {boolean} */
    SettingsFs = true;
    /** Add the "Open in new tab" item to the settings pop-up (only if Content is also true). @type {boolean} */
    SettingsNewTab = true;
    /** Optional callback to modify the settings WebMenu before it is shown. @type {?function(WebMenu, function():Promise<void>):void} */
    OnSettingsMenuOpenFn = null; // (webMenu, closeFn)
    /** Optional async callback to modify the main menu definition before rendering. @type {?function(WebMenu, MainMenuStyle, function():void):Promise<void>} */
    OnMainMenuDefFn = null;  // async (menuDef, mainMenuStyle, hideFn)
}


/**
 * The application shell. Created once by `appMain` via `App.Create()`; builds the header (`<app-header>` with
 * menu, back, pin, title, user and settings buttons), the content host `<app-content>` and the main menu
 * `<app-menu>`, wires history/message navigation and returns the initial-page function in `InitPage`.
 * The instance is published as `window.SysWeaverApp`.
 */
class App {

    /** Element the shell is built in. @type {HTMLElement} */
    Page;
    /** `<app-header>` element. @type {HTMLElement} */
    AppHeader;
    /** `<app-menu>` element (main menu host). @type {HTMLElement} */
    AppMenu;
    /** `<app-content>` element (iframe host). @type {HTMLElement} */
    Content;
    /** `<app-MenuButton>` element (main menu toggle), only when params.MainMenu. @type {HTMLElement} */
    AppMenuButton;
    /** `<app-BackButton>` element (history.back()), only when navigation is enabled. @type {HTMLElement} */
    AppBackButton;
    /** `<app-PinButton>` element (pins the main menu). @type {HTMLElement} */
    PinButton;
    /** `<app-ContentTitle>` element. @type {HTMLElement} */
    Title;
    /** Function returned by Init that opens the initial content (URL hash or first menu item). @type {function():void} */
    InitPage;

    /**
     * Creates an App, runs `Init(params)` and stores its result in `InitPage`.
     * @param {AppParams} [params] - Options (defaults when falsy).
     * @returns {Promise<App>} The initialised app.
     */
    static async Create(params) {
        const app = new App();
        app.InitPage = await app.Init(params);
        return app;
    }

    /**
     * Builds the shell DOM and wiring. Order: `<app-header>`; optional `<app-BackButton>` (`IconNavBack`, Navigation
     * or window.Mobile); `<app-PinButton>` (`IconLock`, pins and stores localStorage["SysWeaver.Pinned"]=true);
     * `<app-ContentTitle>` (click copies its title); `<app-UserButton>` (requests `Api/application/GetMenu("User")`
     * and `Api/auth/GetUser`; icon `IconCurrentUserFill` + class `WithUser` when logged in, else icons/user.svg;
     * single-root menus are unwrapped; listens for InterOp `UserImageChanged` to refresh the background image
     * `../auth/UserImages/Current/small?<n>`); `<app-SettingsButton>` (`IconSettings`; pop-up "AppSettings" menu with
     * full screen toggle, server menu items, "Open in new tab" and `OnSettingsMenuOpenFn`); `<app-content>`; and
     * `<app-menu>` with the main menu (name "Main", from `Api/application/GetMenu(MainMenus ?? "Default")`).
     * Main menu state: body classes `SwPinned` / `SwCollapsed` (see inner functions), `app-capture` overlay,
     * auto-show when the mouse is within 8 px of the menu strip's left edge. Registers window `popstate`
     * (string state -> re-embed without new history entry; null -> reset SysWeaverDoneFirst and click the first
     * item) and `message` (`IframeNavigating` -> embed Url; `LoaderRemoved` -> reveal frame) listeners.
     * Server requests run sequentially. The pin button does nothing when MainMenu is false (`setPinned` is null).
     * @param {AppParams} [params] - Options (defaults when falsy).
     * @returns {Promise<function():void>} Function opening the initial page: embeds the Base64 URL from the
     *   location hash if present, else the first main menu item's `OnClick`; a no-op without a main menu/items.
     */
    async Init(params) {
        if (!params)
            params = new AppParams();
        const docTitle = document.title;
        /** Original document title of the shell, used to build "<app title> - <content title>". @type {string} */
        window.SysWeaverAppTitle = docTitle;
        const t = this;
        /** The App instance (global shell reference). @type {App} */
        window.SysWeaverApp = t;
        const page = params.PageElement || document.body;
        t.Page = page;
        const collapsedStyle = "SwCollapsed";
        const pinnedStyle = "SwPinned";
        const headerIconColor = "IconColorThemeAcc1";
        const menuStyle = new MainMenuStyle();
        menuStyle.IconColorClass = "IconColorThemeAcc1";
        menuStyle.IconSelColorClass = "IconColorThemeAcc1";
        menuStyle.SubIconColorClass = "IconColorThemeMain";
        menuStyle.SubIconSelColorClass = "IconColorThemeMain";


        //  Header
        const buttonSize = 44;
        const appHeader = document.createElement("app-header");
        t.AppHeader = appHeader;
        page.appendChild(appHeader);

        //  Back button
        const n = params.Navigation;
        if (n === null ? window.Mobile : n) {
            const appBackButton = document.createElement("app-BackButton")
            t.AppBackButton = appBackButton;
            appHeader.appendChild(appBackButton);

            const backIcon = new ColorIcon("IconNavBack", headerIconColor, buttonSize, buttonSize,
                _TF("Click to navigate back", "The tool tip description on a button that when clicked will navigate to the previous page in the history"),
                ev => {
                    history.back();
                });
            backIcon.Element.classList.add("IconButton");
            appBackButton.appendChild(backIcon.Element);

            if (!params.MainMenu)
                appBackButton.style.left = "8px";
        }

        //  Pin
        {
            const pinButton = document.createElement("app-PinButton")
            t.PinButton = pinButton;
            appHeader.appendChild(pinButton);

            const backIcon = new ColorIcon("IconLock", headerIconColor, buttonSize, buttonSize,
                _TF("Click to lock the menu in the extapnded state", "The tool tip description on a button that when clicked will always show the menu"),
                ev => {
                    if (!setPinned)
                        return;
                    setPinned(true);
                    localStorage.setItem("SysWeaver.Pinned", true);
                });
            backIcon.Element.classList.add("IconButton");
            pinButton.appendChild(backIcon.Element);
        }


        //  Main title
        const appHeaderMiddle = document.createElement("app-ContentTitle")
        t.Title = appHeaderMiddle;
        /**
         * Copies the current content title to the clipboard (with an info toast) on a primary click.
         * @param {MouseEvent} ev - Click event.
         * @returns {void}
         */
        appHeaderMiddle.onclick = ev => {
            if (badClick(ev))
                return;
            ValueFormat.copyToClipboardInfo(appHeaderMiddle.title);
        };

        appHeader.appendChild(appHeaderMiddle);

        //  User button
        if (params.UserButton) {
            const appSettingsButton = document.createElement("app-UserButton")
            appHeader.appendChild(appSettingsButton);

            const settingMenus = "User";
            let themeItems = params.SettingMenus === "" ? [] : WebMenu.From(await sendRequest("Api/application/GetMenu", settingMenus)).Items;
            const user = await getRequest("Api/auth/GetUser");
            const haveUser = user && user.Succeeded;

            let til = themeItems.length;
            if (til === 1) {
                themeItems = themeItems[0].Children;
                til = themeItems.length;
            }



            const expandEmbeddedIcon = new ColorIcon(haveUser ? "IconCurrentUserFill" : "icons/user.svg", headerIconColor, buttonSize, buttonSize,
                (haveUser
                    ?
                    (_T("Logged in as {0},", user.NickName, "A tool tip description descriping what user is logged into the page.{0} is replaced with the name of the user.") + "\n\n")
                    :
                    "")
                +
                _TF("Click to show user options", "The tool tip description on a button that when clicked will show some user options"),
                ev => {
                    PopUpMenu(appSettingsButton, close => {
                        const menu = new WebMenu();
                        for (let i = 0; i < til; ++i)
                            menu.Items.push(themeItems[i]);
                        return menu;
                    }, true);

                }, null, null, true);
            const el = expandEmbeddedIcon.Element;
            if (haveUser) {
                let count = 0;
                el.classList.add("WithUser");
                InterOp.AddListener(async ev => {
                    const msg = InterOp.GetMessage(ev);
                    const type = msg.Type;
                    if (type !== "UserImageChanged")
                        return;
                    el.style.backgroundImage = 'url("../auth/UserImages/Current/small?' + count + '")';
                    ++count;
                });

            }
            appSettingsButton.appendChild(el);
        }

        //  Settings
        if (params.SettingsButton) {
            const appSettingsButton = document.createElement("app-SettingsButton")
            appHeader.appendChild(appSettingsButton);

            const settingMenus = params.SettingMenus ?? (params.UserButton ? "Theme" : "User,Theme");
            const themeItems = params.SettingMenus === "" ? [] : WebMenu.From(await sendRequest("Api/application/GetMenu", settingMenus)).Items;
            const til = themeItems.length;

            const expandEmbeddedIcon = new ColorIcon("IconSettings", headerIconColor, buttonSize, buttonSize,
                _TF("Click to show settings", "The tool tip description on a button that when clicked will show some settings"),
                ev => {
                    PopUpMenu(appSettingsButton, close => {

                        const menu = new WebMenu();
                        menu.Name = "AppSettings";
                        if (params.SettingsFs) {
                            if (FullScreen.IsFull()) {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Exit full screen", "The text of a menu option that when clicked will exit from full screen mode"),
                                    Flags: 0,
                                    IconClass: "IconFullScreenExit",
                                    Title: _TF("Exit full screen mode", "The tool tip description of a menu option that when clicked will exit from full screen mode"),
                                    Data: async () => {

                                        await FullScreen.Exit();
                                        close();
                                    },
                                }));
                            } else {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Enter full screen", "The text of a menu option that when clicked will enter full screen mode"),
                                    Flags: 0,
                                    IconClass: "IconFullScreenEnter",
                                    Title: _TF("Enter full screen mode", "The tool tip description of a menu option that when clicked will enter full screen mode"),
                                    Data: async () => {

                                        await FullScreen.Enter();
                                        close();
                                    },
                                }));
                            }
                        }

                        for (let i = 0; i < til; ++i)
                            menu.Items.push(themeItems[i]);

                        if (params.SettingsNewTab && params.Content) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Open in new tab", "The text of a menu option that when clicked will open the current content in a new browser tab"),
                                Flags: 0,
                                IconClass: "IconNewTab",
                                Title: _TF("Open the current content in a new tab", "The tool tip description of a menu option that when clicked will open the current content in a new browser tab"),
                                Data: async () => {
                                    const contentUrl = MainMenu.GetEmbeddedUrl(t.Content);
                                    if (contentUrl)
                                        Open(contentUrl);
                                    close();
                                },
                            }));
                        }
                        if (params.OnSettingsMenuOpenFn)
                            params.OnSettingsMenuOpenFn(menu, close);
                        return menu;
                    }, true);

                }, null, null, true);

            appSettingsButton.appendChild(expandEmbeddedIcon.Element);


        }



        //  Content
        if (params.Content) {

            const appContent = document.createElement("app-content");
            this.Content = appContent;
            page.appendChild(appContent);
        }

        let setPinned = null;
        const bcl = document.body.classList;

        //  Main menu
        if (params.MainMenu) {
            const appMenu = document.createElement("app-menu");
            t.AppMenu = appMenu;
            page.appendChild(appMenu);



            let pinned = null;
            /**
             * Applies the pinned state as body classes: on narrow layouts always collapsed (`SwCollapsed`, no
             * `SwPinned`) while remembering `p`; otherwise `SwPinned` (true) or `SwCollapsed` (false). A truthy `p`
             * is always re-applied. Does not write localStorage (callers do). Re-run on the body `SwLayout` event.
             * @param {boolean} p - True to pin the menu.
             * @returns {void}
             */
            setPinned  = p => {
                if (window.Narrow) {
                    bcl.add(collapsedStyle);
                    bcl.remove(pinnedStyle);
                    pinned = p;
                    return;
                } else {
                    if (p)
                        pinned = !p;
                }
                if (p === pinned)
                    return;
                pinned = p;
                if (pinned) {
                    bcl.add(pinnedStyle);
                    bcl.remove(collapsedStyle);
                } else {
                    bcl.remove(pinnedStyle);
                    bcl.add(collapsedStyle);
                }
            }

            document.body.addEventListener("SwLayout", () => setPinned(pinned));


            if (window.Mobile) {
                setPinned(false);
            } else {
                const np = localStorage.getItem("SysWeaver.Pinned");
                setPinned(np === null ? params.MainMenuPinned : JSON.parse(np));
            }

            const appMenuButton = document.createElement("app-MenuButton")
            t.AppMenuButton = appMenuButton;
            appHeader.insertBefore(appMenuButton, appHeader.firstElementChild);

            const appMenuCapture = document.createElement("app-capture");

            let mouseOverFn = null;
            /**
             * Removes the menu's auto-show handler for 250 ms (prevents re-opening right after hiding).
             * Removes and re-adds the same "mousemove" listener that the handler is registered with.
             * @returns {void}
             */
            const tempDisableAutoShow = function () {
                appMenu.removeEventListener("mousemove", mouseOverFn);
                setTimeout(() => {
                    appMenu.addEventListener("mousemove", mouseOverFn);
                }, 250);
            };

            /**
             * Collapses a temporarily shown menu: no-op if already collapsed or if pinned on a wide layout.
             * Removes the `app-capture` overlay and the body capture listeners, adds `SwCollapsed` and briefly
             * disables auto-show. Also used as `HideFn` of the main menu style.
             * @returns {void}
             */
            const menuHide = function () {
                if (bcl.contains(collapsedStyle))
                    return;
                if (pinned)
                    if (!window.Narrow)
                        return;
                appMenuCapture.remove();
                document.body.removeEventListener("mouseleave", menuCaptureLeave);
                document.body.removeEventListener("mouseover", menuCapture);
                bcl.add(collapsedStyle);
                tempDisableAutoShow();
            }

            /**
             * Capture-phase body mouseover handler while the menu is shown: hides the menu when the pointer is over
             * anything that is not inside `<app-menu>` or `<app-header>`.
             * @param {MouseEvent} ev - Mouse event.
             * @returns {void}
             */
            const menuCapture = function (ev) {
                let target = ev.target;
                while (target != document.body) {
                    if (target == appMenu)
                        return;
                    if (target == appHeader)
                        return;
                    target = target.parentElement;
                }
                menuHide();
            }

            /**
             * Capture-phase body mouseleave handler: hides the menu when the pointer leaves the body.
             * @param {MouseEvent} ev - Mouse event.
             * @returns {void}
             */
            const menuCaptureLeave = function (ev) {
                if (ev.target === document.body)
                    menuHide();
            }

            /**
             * Temporarily shows the collapsed menu: appends the `<app-capture>` overlay, registers the capture
             * listeners (menuCapture / menuCaptureLeave) and removes `SwCollapsed`. No-op when not collapsed.
             * @returns {void}
             */
            const menuShow = function () {
                if (!bcl.contains(collapsedStyle))
                    return;
                document.body.appendChild(appMenuCapture);
                document.body.addEventListener("mouseover", menuCapture, true);
                document.body.addEventListener("mouseleave", menuCaptureLeave, true);
                bcl.remove(collapsedStyle);
            }


            /**
             * Main menu button handler: when pinned, unpins (stores localStorage["SysWeaver.Pinned"]=false);
             * otherwise shows or hides the temporary menu.
             * @returns {void}
             */
            const menuToggle = function () {
                if (bcl.contains(pinnedStyle)) {
                    setPinned(false);
                    localStorage.setItem("SysWeaver.Pinned", false);
                    return;
                }
                if (bcl.contains(collapsedStyle))
                    menuShow();
                else
                    menuHide();
            }

            const noOpenWidth = 8;

            /**
             * Auto-show handler (registered for "mousemove" on `<app-menu>`): shows the collapsed menu when the
             * pointer's clientX is below 8 px.
             * @param {MouseEvent} ev - Mouse event.
             * @returns {void}
             */
            mouseOverFn = ev => {
                if (!bcl.contains(collapsedStyle))
                    return;

                //const maxX = ev.target.getBoundingClientRect().width - noOpenWidth;
                if (ev.clientX < noOpenWidth)
                    menuShow();
            };


            const menuIcon = new ColorIcon("IconMainMenu", headerIconColor, buttonSize, buttonSize,
                _TF("Click to show the main menu", "The tool tip description of a button that when clicked will show the main application menu"),
                menuToggle
                );

            appMenu.addEventListener("mousemove", mouseOverFn);
            menuIcon.Element.classList.add("IconButton");
            appMenuButton.appendChild(menuIcon.Element);

            const menuDef = params.MainMenus === "" ? [] : WebMenu.From(await sendRequest("Api/application/GetMenu", params.MainMenus ?? "Default"));
            menuStyle.Content = t.Content;
            menuStyle.HideFn = menuHide;

            /**
             * Selection callback of the main menu (intentionally empty).
             * @param {?WebMenuItem} data - Selected item.
             * @returns {void}
             */
            menuStyle.SelectFn = data => {

            };
            /**
             * Updates `<app-ContentTitle>` (hidden via class `Hidden` without title) and
             * `document.title = "<original title> - <title>"` when main-menu content is revealed.
             * @param {string} [url] - New content URL (unused).
             * @param {string} [title] - Content title.
             * @returns {void}
             */
            menuStyle.ExternalContentFn = (url, title) => {
                if (!title) {
                    appHeaderMiddle.classList.add("Hidden");
                } else {
                    appHeaderMiddle.classList.remove("Hidden");
                    appHeaderMiddle.innerText = title;
                    appHeaderMiddle.title = title;
                    document.title = docTitle + " - " + title;
                }
            };

            if (params.OnMainMenuDefFn)
                await params.OnMainMenuDefFn(menuDef, menuStyle, menuHide);
            menuDef.Name = "Main";
            const mainMenu = new MainMenu(menuDef, menuStyle, appMenu);







            window.addEventListener("popstate", ev => {
                const s = ev.state;
                if (s) {
                    if (typeof s === "string") {
                        console.log("Navigated to '" + s + '"');
                        mainMenu.EmbeddContent(s, menuStyle, true);
                    }
                }
                else {
                    console.log("Navigated to front page");
                    // Back to the initial (state-less) entry: re-open the first item without pushing history.
                    window.SysWeaverDoneFirst = false;
                    menuDef.Items[0].OnClick();
                }
            });
            window.addEventListener("message", ev => {
                const d = ev.data;
                if (d.Type === "IframeNavigating") {
                    mainMenu.EmbeddContent(d.Url, menuStyle);
                }
                if (d.Type === "LoaderRemoved") {
                    mainMenu.LoaderRemoved(menuStyle);
                }
            });

            const clh = window.location.hash;
            if (clh) {
                const hash = clh.substring(1);
                if (hash.length > 0) {
                    const key = Base64DecodeString(hash);
                    return () => mainMenu.EmbeddContent(key, menuStyle);
                } else {
                    if (menuDef.Items.length > 0) {
                        const c = menuDef.Items[0].OnClick;
                        return c;
                    }
                }
            } else {
                if (menuDef.Items.length > 0) {
                    const c = menuDef.Items[0].OnClick;
                    return c;
                }
            }

        }




        return () => { };
    }



}



/**
 * Shell entry point (`<body onload="appMain()">` in the root index.html). Shows the full-page loading overlay,
 * creates the shell with default `AppParams` (`App.Create()`), awaits the optional application hook
 * `window.AppMain()` (typically defined in app/app.js), removes the overlay, waits 1 ms and opens the initial
 * page (`app.InitPage()`).
 * @param {*} [settingMenus] - Unused.
 * @returns {Promise<void>}
 */
async function appMain(settingMenus) {

    const page = document.body;
    const removeLoader = AddLoading(page);
    const app = await App.Create();
    const f = window["AppMain"];
    if (typeof f === "function")
        await f();
    removeLoader();
    await delay(1);
    app.InitPage();


}