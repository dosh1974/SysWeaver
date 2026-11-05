/**
 * @file shell/compat_application.js - the legacy compatibility layer of the old app/application.js (last part of the GENERATED
 * app/application.js bundle = shell/menu.js + shell/content.js + shell/shell.js + this file; also the last part of the GENERATED sw/compat.js
 * bundle for migrated new format pages: sw/core.js + sw/ui.js + sw/compat.js).
 *
 * Old format pages load common/common.js (sw/core.js + sw/ui.js + ui/compat_common.js) and then app/application.js; this file defines
 * every global of the old application.js with the same names, signatures, return values and member names, implemented on the Sw ui
 * library and the Sw shell:
 *  - WebMenu, WebMenuItem (+ From), MainMenuStyle: the same data classes.
 *  - MainMenu(menu, style, toElement): renders the menu as a Sw.Shell.MenuTree (keyboard navigable tree) into toElement; EmbeddContent /
 *    LoaderRemoved / GetEmbeddedUrl / SetUrl / ReplaceUrl / Select / RowMap keep working (embedding uses Sw.Shell.ContentHost, or the
 *    shell's content frame when style.Content is the shell's <app-content>).
 *  - PopUpMenu(element, build(close, backElement) -> WebMenu, rightAlign, bottomAlign): a Sw.Menu (submenus, keyboard, touch drill down).
 *  - PopUp, PopUpWorking, PopUpSelection, SearchablePoupSelection, Confirm, PopUpEdit: Sw.Dialog based (focus trap, Escape, responsive).
 *  - CreateTabBlock, CreatePopUpBlocker, CreatePopUpAtElement, PopUpElement, PopUpElementMenu: the old element based pop-ups
 *    (<SysWeaver-Blocker>, <SysWeaver-PopupMenu>, <SysWeaver-PopupMenuBack>, styled by shell/compat_application.css) for code that builds
 *    into them (edit/Edit.js), kept on screen with Sw.Place.
 *  - AppParams, App, appMain: the shell (Sw.Shell.Params, Sw.Shell.App, Sw.Shell.Main).
 * Window state: SysWeaverApp, SysWeaverAppTitle, SysWeaverDoneFirst (set by the shell), contentUrl (set by PopUpMenu navigation).
 * New format pages must not use these names (SwWeb check --strict); use Sw.Menu, Sw.Dialog, Sw.Shell instead.
 */
(() => {
    "use strict";

    const Sw = window.Sw;
    if (!Sw || !Sw.Dialog || !Sw.Shell || !Sw.Shell.MenuTree) {
        console.error("compat_application.js: sw/core.js, sw/ui.js (common/common.js) and the shell must be loaded first");
        return;
    }
    const Shell = Sw.Shell;

    /**
     * Define a global (a window property, like the old top level declarations).
     * @param {string} name - The global name.
     * @param {*} value - The value.
     */
    function def(name, value) {
        window[name] = value;
    }

    // ------------------------------------------------------------------ menu model

    /**
     * A menu tree (C# WebMenu from Api/application/GetMenu, or built on the client).
     */
    class WebMenu {
        /** Root uri reported by the server. @type {string} */
        RootUri = "";
        /** Top level items. @type {WebMenuItem[]} */
        Items = [];
        /** Menu name (collapsed state key). @type {string} */
        Name = "";

        /**
         * Convert a plain object to a WebMenu (Items converted in place; the name is only set when there are items, as before).
         * @param {object} obj - Plain object.
         * @param {string} [name] - Menu name.
         * @returns {WebMenu} The menu.
         */
        static From(obj, name) {
            const m = Object.assign(new WebMenu(), obj);
            const c = m.Items;
            if (!c || c.length <= 0)
                return m;
            for (let i = 0; i < c.length; ++i)
                c[i] = WebMenuItem.From(c[i]);
            if (name)
                m.Name = name;
            return m;
        }
    }

    /**
     * A menu item (C# WebMenuItem). Type: 0 Path, 1 Table, 2 Embedded, 3 Link, 4 LinkExternal, 5 Js, 6 Chart; Data may be an async
     * function (item, style, isUserClick) => bool (truthy hides the menu). Flags: 1 disabled, 2 checked. OnClick is set by MainMenu.
     */
    class WebMenuItem {
        Id = "";
        Name = "";
        Type = "";
        Title = "";
        IconClass = "";
        Data = "";
        Children = null;
        Flags = 0;

        /**
         * Convert a plain object to a WebMenuItem (Children converted in place).
         * @param {object} obj - Plain object.
         * @returns {WebMenuItem} The item.
         */
        static From(obj) {
            const m = Object.assign(new WebMenuItem(), obj);
            const c = m.Children;
            if (!c || c.length <= 0)
                return m;
            for (let i = 0; i < c.length; ++i)
                c[i] = WebMenuItem.From(c[i]);
            return m;
        }
    }

    /**
     * Visual settings and host callbacks of a MainMenu (the sizes and colour classes are kept for old code; the Sw tree styles the menu).
     */
    class MainMenuStyle {
        IconWidth = 32;
        IconHeight = 32;
        ExpandIconWidth = 20;
        ExpandIconHeight = 20;
        ExpandTab = 16;
        IconColorClass = "IconColorThemeBackground";
        IconSelColorClass = "IconColorThemeAcc1";
        SubIconColorClass = "IconColorThemeBackground";
        SubIconSelColorClass = "IconColorThemeAcc1";
        IconExpander = "IconCollapse,IconExpand";
        IconExpanderMain = "IconCollapseMain,IconExpandMain";
        /** Class added to the element the menu is rendered into. @type {string} */
        MenuStyle = "DefaultMenu";
        /** Element that hosts the embedded content frame. @type {HTMLElement|null} */
        Content = null;
        /** Called with the selected item (or null). @type {function|null} */
        SelectFn = null;
        /** Called with no args when navigation starts and with (url, title) when new content is revealed. @type {function|null} */
        ExternalContentFn = null;
        /** Called to close / collapse the menu after an action. @type {function|null} */
        HideFn = null;
    }

    /**
     * Navigate an embedding item when there is no content host: the shell of this window, else the page itself ("_self", the shell of
     * the top window takes over through IframeNavigating).
     * @param {string} url - The url.
     */
    function navigateAnywhere(url) {
        const app = Shell.Current;
        if (app && app.Host) {
            app.Navigate(url);
            return;
        }
        Sw.Page.Navigate(url, "_self");
    }

    /**
     * Renders a WebMenu as a tree (Sw.Shell.MenuTree) and handles item actions and content embedding (the old MainMenu).
     */
    class MainMenu {
        /** The rendered menu. @type {WebMenu|null} */
        Data = null;
        /** The style. @type {MainMenuStyle|null} */
        Style = null;
        /** The selected row (tree item element, expando MenuItem). @type {HTMLElement|null} */
        Selected = null;
        /** Original tool tip of the selected row. @type {string|null} */
        SelTitle = null;
        /** Absolute item url -> row element. @type {Map<string, HTMLElement>} */
        RowMap = new Map();

        /**
         * Set an icon (ColorIcon) to the first class of a comma separated list.
         * @param {object} icon - A ColorIcon (ChangeImage).
         * @param {string} iconClasses - "class[,class]".
         */
        static SetIcon(icon, iconClasses) {
            if (!iconClasses)
                return;
            icon.ChangeImage(iconClasses.split(",")[0].trim());
        }

        /**
         * Set an expander icon from an "expanded,collapsed" class pair.
         * @param {object} icon - A ColorIcon (ChangeImage).
         * @param {boolean} isExpanded - Expanded.
         * @param {string} iconClasses - The pair (a single class is always used).
         */
        static SetExpIcon(icon, isExpanded, iconClasses) {
            if (!iconClasses)
                return;
            const x = iconClasses.split(",");
            icon.ChangeImage((x.length < 2 ? x[0] : x[isExpanded ? 0 : 1]).trim());
        }

        /**
         * The url shown in a content host: the live frame location (same origin) or its src.
         * @param {HTMLElement|null} content - The content host.
         * @returns {string|null} The url.
         */
        static GetEmbeddedUrl(content) {
            if (!content)
                return null;
            if (content.SwHost)
                return content.SwHost.GetUrl();
            const app = Shell.Current;
            if (app && app.Content === content && app.Host)
                return app.Host.GetUrl();
            const le = content.lastElementChild;
            if (!le || le.tagName !== "IFRAME")
                return null;
            try {
                return le.contentWindow.location.href;
            }
            catch {
            }
            return le.src;
        }

        /**
         * Build the menu (nothing is rendered for a menu without items: Element stays undefined, as before).
         * @param {WebMenu} menu - The menu.
         * @param {MainMenuStyle} [style] - Style and callbacks.
         * @param {HTMLElement} [toElement] - Parent element (gets style.MenuStyle as class).
         */
        constructor(menu, style, toElement) {
            const t = this;
            style = style ?? new MainMenuStyle();
            t.Data = menu;
            t.Style = style;
            const c = menu && menu.Items;
            if (!c || c.length <= 0)
                return;
            const name = (menu.Name && menu.Name.length) ? menu.Name : "NoName";
            t.ExpKey = Shell.CollapsedPrefix + name + ".";
            const tree = new Shell.MenuTree({
                Menu: menu,
                Name: name,
                Class: "sw-shell-popuptree",
                Context: {
                    Navigate: url => t.EmbeddContent(url, style),
                    Hide: () => {
                        if (style.HideFn)
                            style.HideFn();
                    },
                    Style: style,
                },
            });
            t.Tree = tree;
            for (const en of tree.Entries) {
                if (en.Separator)
                    continue;
                en.E.MenuItem = en.Item;
                en.E.MenuIcon = null;
                en.Item.OnClick = en.Disabled ? null : (ev => tree.Activate(en, ev));
                if (en.Url && !t.RowMap.has(en.Url))
                    t.RowMap.set(en.Url, en.E);
            }
            t.Element = tree.E;
            if (toElement) {
                if (style.MenuStyle)
                    toElement.classList.add(style.MenuStyle);
                toElement.append(tree.E);
            }
        }

        /**
         * Select a row (null deselects); calls style.SelectFn.
         * @param {HTMLElement|null} row - The row (tree item element).
         */
        Select(row) {
            const t = this;
            if (!t.Tree)
                return;
            t.Tree.Select(row && row.SwEntry ? row.SwEntry : null);
            t.Selected = row || null;
        }

        /**
         * Push a history entry for a content url (hash = base64 url).
         * @param {string} key - The absolute url.
         */
        SetUrl(key) {
            const n = new URL(window.location.href);
            n.hash = Shell.EncodeHash(key);
            window.history.pushState(key, "", n);
        }

        /**
         * Replace the current history entry with a content url.
         * @param {string} key - The absolute url.
         */
        ReplaceUrl(key) {
            const n = new URL(window.location.href);
            n.hash = Shell.EncodeHash(key);
            window.history.replaceState(key, "", n);
        }

        /**
         * Embed a page in style.Content (the shell's content frame, or a Sw.Shell.ContentHost on that element). Without a content element
         * the page is opened through the shell / IframeNavigating.
         * @param {string} url - The url.
         * @param {MainMenuStyle} [style] - The style (default this menu's).
         * @param {boolean} [noNewState] - Don't add a history entry.
         */
        EmbeddContent(url, style, noNewState) {
            const t = this;
            style = style || t.Style;
            const content = style && style.Content;
            const abs = new URL(url, Sw.Url.Root).href;
            if (!content) {
                navigateAnywhere(abs);
                return;
            }
            const app = Shell.Current;
            if (app && app.Content === content) {
                app.Navigate(abs, { NoHistory: !!noNewState });
                return;
            }
            let host = content.SwHost;
            if (!host) {
                host = content.SwHost = new Shell.ContentHost({
                    Host: content,
                    History: true,
                    OnStart: () => {
                        const f = style.ExternalContentFn;
                        if (f)
                            f();
                    },
                    OnReveal: (u, title) => {
                        const en = t.Tree ? t.Tree.SelectUrl(u) : null;
                        t.Selected = en ? en.E : null;
                        const f = style.ExternalContentFn;
                        if (f && u)
                            f(u, title);
                    },
                    Title: u => {
                        const en = t.Tree ? t.Tree.FindUrl(u) : null;
                        return en ? en.Item.Name : null;
                    },
                });
            }
            host.Navigate(abs, { NoHistory: !!noNewState });
        }

        /**
         * Reveal the newest content frame early (normally done automatically on "LoaderRemoved").
         * @param {MainMenuStyle} [style] - The style.
         */
        LoaderRemoved(style) {
            style = style || this.Style;
            const content = style && style.Content;
            const host = content ? (content.SwHost || (Shell.Current && Shell.Current.Content === content ? Shell.Current.Host : null)) : null;
            const f = host && host.Frame;
            if (f && f.SwReveal)
                f.SwReveal();
        }

        /**
         * Append one item (and its children) to a container (old API; renders a small tree of that item).
         * @param {HTMLElement} toElement - The container.
         * @param {WebMenuItem} data - The item.
         * @param {number} depth - Unused (kept).
         * @param {MainMenuStyle} [style] - The style.
         * @returns {boolean} True if the item has children.
         */
        Add(toElement, data, depth, style) {
            const m = new MainMenu({ Items: [data], Name: this.Data ? this.Data.Name : "" }, style || this.Style, null);
            if (m.Element)
                toElement.append(m.Element);
            return !!(data.Children && data.Children.length);
        }
    }

    // ------------------------------------------------------------------ element based pop-ups (old DOM, kept for code that builds into them)

    /**
     * Create a focus sentinel element (<SysWeaver-TabBlock>, tab index 0).
     * @returns {HTMLElement} The element (not attached).
     */
    function CreateTabBlock() {
        const e = document.createElement("SysWeaver-TabBlock");
        e.tabIndex = 0;
        e.classList.add("sw-tabblock");
        return e;
    }

    /**
     * Create a blocker (<SysWeaver-Blocker>, not attached; the caller appends it): dims the page, traps the focus, closes on a click on the
     * backdrop (pointer down and up on it) or Escape unless dontAllowClose.
     * @param {boolean} dontAllowClose - Only the returned close function closes it.
     * @param {function():Promise} [onClose] - Awaited before it is removed.
     * @returns {Array} [blocker element, async close function].
     */
    function CreatePopUpBlocker(dontAllowClose, onClose) {
        const block = document.createElement("SysWeaver-Blocker");
        block.className = "sw-root";
        block.tabIndex = 0;
        let closed = false;
        let release = null;
        /**
         * Close the blocker (await onClose, release the focus, remove it, then call Close.EndFn).
         * @returns {Promise<void>}
         */
        async function Close() {
            if (closed)
                return;
            closed = true;
            if (onClose)
                await onClose();
            if (release)
                release();
            block.remove();
            const endFn = Close.EndFn;
            if (endFn)
                endFn();
        }
        //  Trap the focus once attached
        queueMicrotask(() => {
            if (!closed)
                release = Sw.Focus.Trap(block);
        });
        let down = false;
        block.addEventListener("pointerdown", ev => {
            down = ev.target === block && ev.button === 0 && Sw.IsPureEvent(ev);
        });
        block.addEventListener("pointerup", ev => {
            const d = down;
            down = false;
            if (d && ev.target === block && !dontAllowClose)
                Close();
        });
        block.addEventListener("keydown", ev => {
            ev.stopPropagation();
            if (ev.key === "Escape" && Sw.IsPureEvent(ev) && !dontAllowClose) {
                ev.preventDefault();
                Close();
            }
        });
        return [block, Close];
    }

    /**
     * Create a <SysWeaver-PopupMenu> in a new blocker at a point relative to an element (computed once:
     * left = rect.x + widthScale * rect.width + offsetX, top = rect.y + heightScale * rect.height + offsetY).
     * @param {HTMLElement} element - The anchor.
     * @param {number} [widthScale] - Horizontal fraction of the anchor width.
     * @param {number} [heightScale] - Vertical fraction of the anchor height.
     * @param {number} [offsetX] - Horizontal offset (px).
     * @param {number} [offsetY] - Vertical offset (px).
     * @param {boolean} [dontAllowClose] - Only code can close it.
     * @param {function():Promise} [onClose] - Called when closed.
     * @returns {Array} [content element, async close function, blocker element].
     */
    function CreatePopUpAtElement(element, widthScale, heightScale, offsetX, offsetY, dontAllowClose, onClose) {
        const n = v => typeof v === "number" ? v : 0;
        const pop = document.createElement("SysWeaver-PopupMenu");
        const r = element.getBoundingClientRect();
        pop.style.left = (r.width * n(widthScale) + n(offsetX) + r.x) + "px";
        pop.style.top = (r.height * n(heightScale) + n(offsetY) + r.y) + "px";
        const bc = CreatePopUpBlocker(dontAllowClose, onClose);
        bc[0].append(pop);
        return [pop, bc[1], bc[0]];
    }

    /**
     * Keep the content of an anchored pop-up on screen (shifts its fixed children left / up when they overflow).
     * @param {HTMLElement} pop - The <SysWeaver-PopupMenu>.
     */
    function keepOnScreen(pop) {
        const vw = window.innerWidth, vh = window.innerHeight, m = 8;
        for (const c of pop.children) {
            const r = c.getBoundingClientRect();
            if (!r.width && !r.height)
                continue;
            const cs = getComputedStyle(c);
            if (cs.position !== "fixed")
                continue;
            let dx = 0, dy = 0;
            if (r.right > vw - m)
                dx = (vw - m) - r.right;
            if (r.left + dx < m)
                dx = m - r.left;
            if (r.bottom > vh - m)
                dy = (vh - m) - r.bottom;
            if (r.top + dy < m)
                dy = m - r.top;
            if (dx || dy) {
                c.style.left = (r.left + dx) + "px";
                c.style.top = (r.top + dy) + "px";
            }
        }
    }

    /**
     * Show an anchored pop-up that blocks the page (the position is relative to the element: dx = widthScale * width + offsetX ...).
     * @param {HTMLElement} element - The anchor.
     * @param {function(HTMLElement, function():Promise):Promise} build - Builds into the content element; the second argument closes.
     * @param {boolean} [blockUntilClosed] - Resolve when closed.
     * @param {boolean} [dontAllowClose] - Only the close function closes it.
     * @param {function():Promise} [onClose] - Called when closed.
     * @param {number} [widthScale] - See CreatePopUpAtElement.
     * @param {number} [heightScale] - See CreatePopUpAtElement.
     * @param {number} [offsetX] - See CreatePopUpAtElement.
     * @param {number} [offsetY] - See CreatePopUpAtElement.
     * @returns {Promise<void>}
     */
    async function PopUpElement(element, build, blockUntilClosed, dontAllowClose, onClose, widthScale, heightScale, offsetX, offsetY) {
        const bc = CreatePopUpAtElement(element, widthScale, heightScale, offsetX, offsetY, dontAllowClose, onClose);
        const page = bc[0], close = bc[1], block = bc[2];
        page.Close = close;
        const ro = new ResizeObserver(() => {
            if (!block.isConnected) {
                ro.disconnect();
                return;
            }
            block.classList.toggle("Scroll", page.scrollHeight > page.clientHeight);
        });
        ro.observe(page);
        document.body.append(block);
        Sw.Focus.Set(block);
        await build(page, close);
        page.append(CreateTabBlock());
        requestAnimationFrame(() => keepOnScreen(page));
        if (blockUntilClosed)
            await new Promise(r => close.EndFn = r);
    }

    /**
     * Show an anchored pop-up menu panel (<SysWeaver-PopupMenuBack>, class Right when right aligned) below (or above) an element.
     * The panel is placed on screen next to the anchor once built (unless the builder positioned it, ex: with TrackElement).
     * @param {HTMLElement} element - The anchor.
     * @param {function(HTMLElement, function():Promise):Promise} build - Builds into the panel (panel, close).
     * @param {boolean} [blockUntilClosed] - Resolve when closed.
     * @param {boolean} [dontAllowClose] - Only the close function closes it.
     * @param {function():Promise} [onClose] - Called when closed.
     * @param {boolean} [rightAlign] - Align the right edges.
     * @param {boolean} [bottomAlign] - Open above the element.
     * @returns {Promise<void>}
     */
    async function PopUpElementMenu(element, build, blockUntilClosed, dontAllowClose, onClose, rightAlign, bottomAlign) {
        await PopUpElement(element, async (toE, close) => {
            const back = document.createElement("SysWeaver-PopupMenuBack");
            back.className = "sw-root";
            toE.append(back);
            if (rightAlign)
                back.classList.add("Right");
            back.Close = close;
            back.style.visibility = "hidden";
            await build(back, close);
            back.style.visibility = "";
            if (!back.style.left && !back.style.top) {
                const place = () => Sw.Place(back, element, { Side: bottomAlign ? "above" : "below", Align: rightAlign ? "end" : "start", Gap: 6 });
                place();
                const ro = new ResizeObserver(() => {
                    if (!back.isConnected) {
                        ro.disconnect();
                        return;
                    }
                    place();
                });
                ro.observe(back);
            }
            const f = Sw.Focus.Tabbables(back)[0];
            if (f)
                f.focus({ preventScroll: true });
        }, blockUntilClosed, dontAllowClose, onClose, 0, bottomAlign ? 0 : 1, 0, bottomAlign ? -8 : 8);
    }

    // ------------------------------------------------------------------ menus and dialogs (Sw)

    /**
     * Show a pop-up menu next to an element: build(close, backElement) returns the WebMenu, shown as a Sw.Menu (submenus, check items,
     * keyboard, touch drill down). Embedding items load into the shell (or navigate the page through the shell). backElement is a detached
     * <SysWeaver-PopupMenuBack> inside a detached <SysWeaver-PopupMenu> (old callers add classes to it).
     * @param {HTMLElement} element - The anchor.
     * @param {function(function():Promise, HTMLElement):WebMenu} build - Returns the menu.
     * @param {boolean} [rightAlign] - Align the right edges.
     * @param {boolean} [bottomAlign] - Open above the element.
     */
    function PopUpMenu(element, build, rightAlign, bottomAlign) {
        const pop = document.createElement("SysWeaver-PopupMenu");
        const back = document.createElement("SysWeaver-PopupMenuBack");
        pop.append(back);
        let handle = null;
        let closed = false;
        /** Close the menu. @returns {Promise<void>} */
        const close = async () => {
            closed = true;
            if (handle)
                handle.Close();
        };
        back.Close = close;
        const style = new MainMenuStyle();
        style.HideFn = close;
        let menu = null;
        try {
            menu = build(close, back);
        }
        catch (x) {
            console.error("PopUpMenu: build failed: " + (x && x.stack ? x.stack : x));
            return;
        }
        if (closed || !menu || !menu.Items || !menu.Items.length)
            return;
        handle = Shell.ShowMenu(element, menu.Items, {
            Navigate: url => {
                window.contentUrl = url;
                navigateAnywhere(url);
            },
            Style: style,
        }, {
            Align: rightAlign ? "end" : "start",
            Side: bottomAlign ? "above" : "below",
        });
    }

    /**
     * Show a modal dialog (Sw.Dialog) built by a callback.
     * @param {function(HTMLElement, function():Promise, HTMLElement):Promise} build - Builds into the content element (<SysWeaver-PopUpBackground>
     * with the expandos Close, Block, MakeFullWidth(), MakeMaxHeight()); the second argument closes, the third is the header tool area.
     * @param {boolean} [blockUntilClosed] - Resolve when closed (else resolve to the close function once built).
     * @param {boolean} [dontAllowClose] - No close button, Escape and the backdrop don't close it.
     * @param {function():Promise} [onClose] - Called when closed.
     * @param {boolean} [haveCloseButton] - Show a close button (default !dontAllowClose).
     * @returns {Promise<function():Promise|undefined>} The close function (not blocking), else undefined once closed.
     */
    async function PopUp(build, blockUntilClosed, dontAllowClose, onClose, haveCloseButton) {
        if (typeof haveCloseButton === "undefined")
            haveCloseButton = !dontAllowClose;
        let calledOnClose = false;
        const runOnClose = async () => {
            if (calledOnClose)
                return;
            calledOnClose = true;
            if (onClose)
                await onClose();
        };
        let handle = null;
        /** Close the dialog (awaits onClose first, as before). @returns {Promise<void>} */
        const Close = async () => {
            await runOnClose();
            if (handle)
                handle.Close(null);
        };
        const page = document.createElement("SysWeaver-PopUpBackground");
        page.Close = Close;
        /** The dialog box of the page (available while building). @returns {HTMLElement|null} */
        const box = () => page.closest(".sw-dialog") || (handle && handle.Box);
        page.MakeFullWidth = () => {
            const b = box();
            if (b)
                b.classList.add("sw-wide", "FullWidth");
        };
        page.MakeMaxHeight = () => {
            const b = box();
            if (b)
                b.classList.add("sw-full", "MaxHeight");
        };
        let built = null;
        handle = Sw.Dialog.Open({
            Class: "sw-compat-popup",
            //  the old PopUp has no title: the page title names it (Sw.Dialog warns about a dialog without a name)
            AriaLabel: document.title || null,
            Close: !!haveCloseButton,
            NoEscape: !!dontAllowClose,
            Modal: !!dontAllowClose,
            Content: (body, close, tools) => {
                page.Block = handle ? handle.Box : null;
                body.append(page);
                built = (async () => {
                    await build(page, Close, tools);
                })();
                return built.then(() => null);
            },
            OnClose: () => {
                runOnClose();
            },
        });
        page.Block = handle.Box;
        try {
            await built;
        }
        catch {
            //  Reported by Sw.Dialog
        }
        if (blockUntilClosed) {
            await handle.Result;
            return undefined;
        }
        return Close;
    }

    /**
     * Show a "working" dialog that the user can't close.
     * @param {string} [title] - Title.
     * @param {string} [text] - Text.
     * @param {string} [iconClass] - An extra icon (class or url).
     * @returns {Promise<function():Promise<void>>} Resolves to the close function.
     */
    async function PopUpWorking(title, text, iconClass) {
        /* SwWeb:allow ux-fbk-4 (the old PopUpWorking API: the caller owns the work and closes it; new code uses Sw.Dialog.Working with Cancel / Background or Sw.Activity.Run) */
        const close = Sw.Dialog.Working({ Title: title || null, Text: text || null, Icon: iconClass || null });
        return async () => close();
    }

    /**
     * A searchable selection: getItems(search, close) returns elements for the filter text (they call close themselves when chosen).
     * @param {string} text - Header text.
     * @param {string} title - Placeholder / tool tip.
     * @param {function(string, function):(HTMLElement[]|Promise<HTMLElement[]>)} getItems - Items for a filter.
     * @param {string} [startText] - Initial filter.
     * @param {boolean} [getAll] - Also list items for an empty filter.
     * @param {boolean} [blockUntilClosed] - Resolve when closed.
     * @param {boolean} [dontAllowClose] - Unused (the selection can always be dismissed).
     * @param {function():Promise} [onClose] - Called when closed.
     * @returns {Promise<void>}
     */
    async function PopUpSelection(text, title, getItems, startText, getAll, blockUntilClosed, dontAllowClose, onClose) {
        const r = Sw.Dialog.Select({
            Title: text,
            Placeholder: title,
            StartText: startText || "",
            GetAll: !!getAll,
            Items: (search, close) => getItems(search, async () => close(null)),
        });
        const done = r.then(async () => {
            if (onClose)
                await onClose();
        });
        if (blockUntilClosed)
            await done;
    }

    /**
     * A paged, searchable selection that remembers the filter and the recent selections (Sw.SearchSelection; storage key
     * "SysWeaver.PopUpRecent.<id>").
     */
    class SearchablePoupSelection {
        /**
         * @param {string} id - Unique id.
         * @param {function(*):*} getRecentIdFn - Item -> id.
         * @param {function(*, function(*):Promise, boolean):HTMLElement} createElementFn - Builds an item element (calls select(item)).
         * @param {function(string, Map, number, number):Promise<Array>} findFn - (filter, recentMap, count, offset) -> items.
         * @param {number} [maxRecent=10] - Size of the recent list.
         */
        constructor(id, getRecentIdFn, createElementFn, findFn, maxRecent) {
            if (!maxRecent)
                maxRecent = 10;
            this.Key = "SysWeaver.PopUpRecent." + id;
            this.CreateElementFn = createElementFn;
            this.FindFn = findFn;
            this.MaxRecent = maxRecent;
            this.GetRecentIdFn = getRecentIdFn;
            this.Sel = new Sw.SearchSelection({ Id: id, GetId: getRecentIdFn, Render: createElementFn, Find: findFn, MaxRecent: maxRecent });
        }

        /**
         * Show the selection.
         * @param {string} titleText - Title.
         * @param {function(*):Promise} [onSelect] - Called with the selected item.
         * @param {number} [maxView=20] - Page size.
         * @param {string} [titleToolTip] - Title tool tip.
         * @param {string} [filterPlaceholder] - Filter placeholder.
         * @param {boolean} [blockUntilClose] - Resolve when closed.
         * @param {boolean} [dontAllowClose] - Unused.
         * @param {function():Promise} [onClose] - Called when closed.
         * @returns {Promise<void>}
         */
        async Show(titleText, onSelect, maxView, titleToolTip, filterPlaceholder, blockUntilClose, dontAllowClose, onClose) {
            const s = this.Sel;
            s.O.Render = this.CreateElementFn;
            s.O.Find = this.FindFn;
            s.O.GetId = this.GetRecentIdFn;
            s.MaxRecent = this.MaxRecent;
            const r = s.Show({ Title: titleText, OnSelect: onSelect, PageSize: maxView || 20, Placeholder: filterPlaceholder });
            const done = r.then(async () => {
                if (onClose)
                    await onClose();
            });
            if (blockUntilClose)
                await done;
        }
    }

    /**
     * Ask the user to confirm something.
     * @param {string} title - Title.
     * @param {string} [text] - Text.
     * @param {string} [okButtonText] - OK text.
     * @param {string} [cancelButtonText] - Cancel text.
     * @param {string} [okButtonImageClass] - OK icon (default IconOk).
     * @param {string} [cancelButtonImageClass] - Cancel icon (default IconCancel).
     * @param {string} [okButtonTitle] - OK tool tip.
     * @param {string} [cancelButtonTitle] - Cancel tool tip.
     * @param {string} [okStyle] - Old style class of the OK button (a "Danger" / "Red" style makes it a danger button).
     * @param {string} [cancelStyle] - Unused.
     * @returns {Promise<boolean>} True if confirmed.
     */
    function Confirm(title, text, okButtonText, cancelButtonText, okButtonImageClass, cancelButtonImageClass, okButtonTitle, cancelButtonTitle, okStyle, cancelStyle) {
        const st = typeof okStyle === "string" ? okStyle : (okStyle && okStyle.ButtonStyle) || "";
        //  As before a click on the title copies it too (the text copies itself): the dialog is in the document once Confirm returns
        queueMicrotask(() => {
            const boxes = document.querySelectorAll(".sw-dialog[role=alertdialog]");
            const te = boxes.length ? boxes[boxes.length - 1].querySelector(".sw-dialog-title") : null;
            if (te && title && Sw.Clipboard && Sw.Clipboard.CopyOnClick)
                Sw.Clipboard.CopyOnClick(te, String(title), { AddTitle: true });
        });
        return Sw.Dialog.Confirm(title, text, okButtonText || null, {
            CancelText: cancelButtonText || null,
            OkIcon: okButtonImageClass ?? "IconOk",
            CancelIcon: cancelButtonImageClass ?? "IconCancel",
            OkTitle: okButtonTitle || null,
            CancelTitle: cancelButtonTitle || null,
            Danger: /danger|red|delete|remove/i.test(st),
        });
    }

    /**
     * Edit an object in a dialog (needs the edit/ scripts: Edit, EditOptions, TypeMemberFlags).
     * @param {string|object} type - The .NET type name (resolved with edit/GetTypeInfo) or a type info.
     * @param {*} [obj] - The initial value.
     * @param {string} [buttonText] - Accept button text (default "OK").
     * @param {string} [buttonIcon] - Accept button icon.
     * @param {string} [buttonToolTip] - Accept button tool tip.
     * @param {string} [title] - Title.
     * @param {string} [titleToolTip] - Title tool tip.
     * @param {string} [primName] - Display name of a primitive type.
     * @returns {Promise<object|null>} The edited object, or null when dismissed.
     */
    async function PopUpEdit(type, obj, buttonText, buttonIcon, buttonToolTip, title, titleToolTip, primName) {
        let pe = null;
        let ret = null;
        const v = await Sw.Dialog.Show({
            Title: title || "",
            Wide: true,
            Class: "sw-compat-popupedit",
            Content: async (body, close) => {
                if (typeof type === "string") {
                    try {
                        type = await Sw.Net.Send(Sw.Url.Root + "edit/GetTypeInfo", type);
                    }
                    catch (e) {
                        Sw.Fail(_TF("Failed to get type info", "Error message displayed when there was a failure to get type information for the given type") + ". " + e.message);
                        close(null);
                        return null;
                    }
                    if (!type) {
                        Sw.Fail(_TF("Invalid typename!", "Error message displayed when a type with the given name wasn't found"));
                        close(null);
                        return null;
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
                pe = new Edit(type, undefined, opt);
                await pe.SetObject(obj);
                return pe.Element;
            },
            Buttons: [{
                Text: buttonText ?? _TF("OK", "Text on a button that when pressed will accept"),
                Icon: buttonIcon || null,
                Title: buttonToolTip || null,
                Kind: "primary",
                Value: true,
                OnClick: () => {
                    if (pe)
                        ret = pe.GetObject();
                },
            }],
            OnShow: (close, box) => {
                const te = box.querySelector(".sw-dialog-title");
                if (te && titleToolTip)
                    te.title = titleToolTip;
            },
        });
        return v === true ? ret : null;
    }

    // ------------------------------------------------------------------ shell

    /** Shell options (the old AppParams). */
    class AppParams extends Shell.Params {
    }

    /** The application shell (the old App: Create(params), Init(params), InitPage, Page, AppHeader, AppMenu, Content ...). */
    class App extends Shell.App {
    }

    def("WebMenu", WebMenu);
    def("WebMenuItem", WebMenuItem);
    def("MainMenuStyle", MainMenuStyle);
    def("MainMenu", MainMenu);
    def("CreateTabBlock", CreateTabBlock);
    def("CreatePopUpBlocker", CreatePopUpBlocker);
    def("CreatePopUpAtElement", CreatePopUpAtElement);
    def("PopUpElement", PopUpElement);
    def("PopUpElementMenu", PopUpElementMenu);
    def("PopUpMenu", PopUpMenu);
    def("PopUp", PopUp);
    def("PopUpWorking", PopUpWorking);
    def("PopUpSelection", PopUpSelection);
    def("SearchablePoupSelection", SearchablePoupSelection);
    def("Confirm", Confirm);
    def("PopUpEdit", PopUpEdit);
    def("AppParams", AppParams);
    def("App", App);
    if (typeof window.appMain !== "function")
        def("appMain", Shell.Main);
})();
