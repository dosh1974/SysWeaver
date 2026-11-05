/**
 * @file explore/icons.js - page script of explore/icons.html, the icon gallery (Debug / Icons).
 *
 * Entry: `<body onload="iconsMain()">`. Url parameter `path`: the folder to list (default: the server's `icons/`).
 * Server: `Api/debug/explore/GetIcons` (GET, or POST `{Param: path}`) -> IconInfo[] `{Url, Name, Size, Location}`.
 * Each tile shows the icon on a checkerboard with its file name and the icon class names that draw it (the `.IconX` rules of the
 * loaded style sheets, ex: sw/ui.css = ui/icons.css, read from the CSSOM), sorted by name in a compact grid. Icon classes with an
 * inline (data:) picture and the built in inline svg icons of `Sw.Icon(name)` (Sw.Icons.List()) get tiles too (not with `path`).
 * A click (or Enter / Space) copies the relative svg url (`icons/ok.svg`, the default, like the original gallery) or, with "Copy:
 * Class name", the class name (`IconOk`; the first one when several classes share the file; a file without a class copies its url). The copy mode is remembered in localStorage
 * `SysWeaver.IconGalleryCopy` ("class" / "url"). A filter box narrows the tiles by file or class name (typing anywhere starts
 * filtering), and a "Size" slider sets the icon size (32..256 px, default 64, remembered in localStorage `SysWeaver.IconGallerySize`;
 * css variable --icons-size on body).
 * Uses Sw (sw/core.js + sw/ui.js). Styled by explore/icons.css.
 */

(() => {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /** Smallest, largest and default icon size in px (the "Size" slider; grows with --sw-scale on large screens). */
    const IconMin = 32, IconMax = 256, IconDefault = 64;

    /** Local storage key of the copy mode ("class" or "url"). */
    const CopyKey = "SysWeaver.IconGalleryCopy";

    /**
     * Join the non empty lines.
     * @param {...string} s The lines.
     * @returns {string} The text.
     */
    const lines = (...s) => s.filter(x => x).join("\n");

    /**
     * Collect the icon classes of the loaded style sheets: rules with a single `.IconX` selector (comma lists are split) whose
     * mask-image / background-image is an url.
     * @returns {Map<string, string[]>} Picture url (absolute, or the data: url) -> class names (sorted).
     */
    function iconClasses() {
        const map = new Map();
        /**
         * Add the rules of a rule list.
         * @param {CSSRuleList} rules The rules.
         * @param {string} base The style sheet url (relative urls are resolved against it).
         */
        const scan = (rules, base) => {
            for (const r of rules) {
                if (r.cssRules && !r.selectorText) {
                    scan(r.cssRules, base);
                    continue;
                }
                if (!r.selectorText || !r.style)
                    continue;
                const names = r.selectorText.split(",").map(x => x.trim()).filter(x => /^\.Icon[A-Za-z0-9_-]+$/.test(x)).map(x => x.substring(1));
                if (!names.length)
                    continue;
                const v = r.style.getPropertyValue("mask-image") || r.style.getPropertyValue("-webkit-mask-image") || r.style.getPropertyValue("background-image");
                const m = /url\(\s*(["']?)(.*?)\1\s*\)/.exec(v || "");
                if (!m)
                    continue;
                let url = m[2];
                if (!/^data:/i.test(url)) {
                    try {
                        url = new URL(url, base).href;
                    }
                    catch {
                        continue;
                    }
                }
                const list = map.get(url) || [];
                names.forEach(n => {
                    if (!list.includes(n))
                        list.push(n);
                });
                map.set(url, list);
            }
        };
        for (const s of Array.from(document.styleSheets)) {
            try {
                scan(s.cssRules, s.href || document.baseURI);
            }
            catch {
                //  A cross origin sheet: no access
            }
        }
        map.forEach(v => v.sort());
        return map;
    }

    /**
     * Page entry of explore/icons.html.
     * @returns {Promise<void>} Resolves when all icons are loaded.
     */
    window.iconsMain = async function () {
        const remove = Sw.Page.Loading();
        try {
            const pending = new Sw.PendingImages();
            const path = Sw.Url.Get("path");
            const api = Sw.Net.Api("debug/explore/GetIcons");
            const icons = path ? await Sw.Net.Send(api, { Param: path }) : await Sw.Net.Get(api);
            const classes = iconClasses();
            /** @type {string} What a click copies: "url" (the svg url, the default, like the original gallery) or "class" (the class name). */
            let mode = Sw.Storage.Local.Get(CopyKey, "url") === "class" ? "class" : "url";
            //  The tiles are copy buttons, grouped (a list of buttons was announced as plain list items)
            const list = el("div.icons-list", { attrs: { role: "group", "aria-label": _TF("Icons", "Accessible name of the group of icon tiles in the icon gallery") } });
            /**
             * Create a tile (a copy button).
             * @param {Element} pic The picture element.
             * @param {string} name The file / icon name.
             * @param {string[]} cls The class names (or the Sw.Icon name).
             * @param {string|null} rel The relative url of the file (null: no file).
             * @param {string} info The first tool tip lines.
             * @returns {HTMLElement} The tile.
             */
            const tile = (pic, name, cls, rel, info) => {
                const t = el("div.icons-item", { attrs: { role: "button" } }, pic, el("div.icons-name", { text: name }),
                    cls.length ? el("div.icons-class", { text: cls.join(" ") }) : null);
                Sw.KeyClick(t);
                /** @returns {string} What a click copies in the current mode. */
                const value = () => (mode === "url" && rel) || cls[0] || rel || name;
                /** Update the tool tip and the accessible name for the copy mode. */
                t.SwMode = () => {
                    const v = value();
                    t.title = lines(info, cls.length > 1 ? _T("Classes: {0}", cls.join(", "), "A tool tip line in the icon gallery listing the icon class names that show the same icon, {0} is the list") : "")
                        + "\n\n" + _T('Click to copy "{0}".', v, "A tool tip line of an icon tile in the icon gallery, {0} is the text that a click copies (a class name or an url)");
                    t.setAttribute("aria-label", _T("Copy {0}", v, "Accessible name of an icon tile in the icon gallery (a click copies a class name or an url), {0} is the text that is copied"));
                };
                t.SwMode();
                /* SwWeb:allow ux-lst-4 (an icon tile of the gallery is a copy button: its accessible name is "Copy <name>") */
                t.addEventListener("click", ev => {
                    if (ev.shiftKey || ev.altKey || ev.ctrlKey || ev.metaKey)
                        return;
                    Sw.Clipboard.Copy(value());
                });
                t.SwName = (name + " " + cls.join(" ") + " " + (rel || "")).toLowerCase();
                return t;
            };
            //  Sorted by name (the server lists them in folder order)
            icons.sort((a, b) => String(a.Name).localeCompare(String(b.Name), undefined, { numeric: true, sensitivity: "base" }));
            const tiles = icons.map(icon => {
                const rel = icon.Url;
                const abs = new URL(rel, Sw.Url.Root).href;
                const img = Sw.CreateImg();
                img.draggable = false;
                img.alt = "";
                pending.Start(img, abs);
                return tile(img, icon.Name, classes.get(abs) || [], rel,
                    lines(rel, _T("Size: {0} bytes.", icon.Size, "A tool tip describing the size of a file in bytes.{0} is replaced with the numeric size"), icon.Location));
            });
            if (!path) {
                //  Icon classes with an inline picture (a data: svg in the style sheet, no file)
                classes.forEach((cls, url) => {
                    if (/^data:/i.test(url))
                        tiles.push(tile(el("div.icons-pic", null, Sw.IconClass(cls[0])), cls[0], cls, null,
                            _TF("An icon class with an inline picture (no file).", "A tool tip line in the icon gallery for an icon class whose picture is defined in the style sheet")));
                });
                //  The built in inline svg icons: Sw.Icon("name")
                if (Sw.Icons && Sw.Icons.List)
                    Sw.Icons.List().forEach(n => tiles.push(tile(el("div.icons-pic", null, Sw.Icon(n)), 'Sw.Icon("' + n + '")', [n], null,
                        _TF("A built in inline svg icon of the Sw ui library.", "A tool tip line in the icon gallery for an inline svg icon (drawn with Sw.Icon(name))"))));
            }
            list.append(...tiles);
            const count = el("span.icons-count");
            /** Show the tiles matching the filter. */
            const filter = v => {
                const f = (v || "").trim().toLowerCase();
                let n = 0;
                tiles.forEach(t => {
                    const show = !f || t.SwName.includes(f);
                    t.hidden = !show;
                    n += show ? 1 : 0;
                });
                count.textContent = _T("{0} of {1}", n, tiles.length, "Number of shown icons of all icons in the icon gallery, {0} is the shown count, {1} the total");
                none.E.hidden = n > 0;
            };
            //  Nothing matches the filter: an empty state under the bar (the bar stays)
            const none = Sw.StateView({
                Icon: "search",
                Title: _TF("No icons match", "Title of the empty state of the icon gallery when no icon matches the filter text"),
                Text: _TF("Try another part of the file or class name.", "Text of the empty state of the icon gallery when no icon matches the filter text"),
            });
            const box = new Sw.TextBox({
                Placeholder: _TF("Filter icons..", "Placeholder of the filter box of the icon gallery"),
                Title: _TF("Show only the icons whose file or class name contains this text", "Tool tip of the filter box of the icon gallery"),
                Icon: "search",
                Clear: true,
                OnInput: filter,
                OnChange: filter,
            });
            filter("");
            //  What a click copies (remembered per browser)
            const copySel = new Sw.Select({
                Small: true,
                Title: _TF("What a click on an icon copies", "Tool tip of the select in the icon gallery that chooses what a click on an icon copies"),
                Options: [
                    { Value: "url", Label: _TF("Svg url", "Option of the copy select in the icon gallery: a click copies the relative url of the svg file") },
                    { Value: "class", Label: _TF("Class name", "Option of the copy select in the icon gallery: a click copies the icon class name (ex: IconOk)") },
                ],
                Value: mode,
                OnChange: v => {
                    mode = v === "url" ? "url" : "class";
                    Sw.Storage.Local.Set(CopyKey, mode);
                    tiles.forEach(t => t.SwMode());
                },
            });
            const copyBox = el("div.icons-size", null,
                el("span", { text: _TF("Copy", "Label of the select in the icon gallery that chooses what a click on an icon copies (class name or svg url)") }), copySel.E);
            //  Icon size (px, before the large screen scaling --sw-scale): remembered per browser
            const sizeKey = "SysWeaver.IconGallerySize";
            /** Apply an icon size. */
            const setSize = v => document.body.style.setProperty("--icons-size", Math.round(v) + "px");
            let size = parseInt(Sw.Storage.Local.Get(sizeKey, ""), 10);
            if (!(size >= IconMin && size <= IconMax))
                size = IconDefault;
            setSize(size);
            const slider = new Sw.Slider({
                Min: IconMin, Max: IconMax, Step: 8, Value: size, Default: IconDefault,
                Label: _TF("Icon size", "Accessible name of the slider that sets the size of the icons in the icon gallery"),
                OnInput: setSize,
                OnChange: v => {
                    setSize(v);
                    Sw.Storage.Local.Set(sizeKey, Math.round(v));
                },
            });
            const sizeBox = el("div.icons-size", null,
                el("span", { text: _TF("Size", "Label of the slider that sets the size of the icons in the icon gallery") }), slider.E);
            //  Design v2 page: header (title, what it is) + the tool bar + the grid, left aligned (ui/page.css)
            const header = Sw.PageHeader({
                //  (the original page title, lower case: S5-B-1; in the app shell the header shows the menu name)
                Title: _TF("icons", "Title of the icon gallery page (the original page title)"),
                Description: path
                    ? _T("Icons in {0}. Select an icon to copy its class name or url.", path, "Description of the icon gallery page for a folder given in the link, {0} is the folder")
                    : _TF("The icon files of this service and the built in icons. Select an icon to copy its class name or url.", "Description of the icon gallery page"),
            });
            document.body.append(el("main.sw-page-dash.icons-page", null, header.E, el("div.icons-bar", null, box.E, copyBox, sizeBox, count), list, none.E));
            //  Typing anywhere filters (not while a control has the focus)
            document.addEventListener("keydown", ev => {
                const a = document.activeElement;
                if (ev.key.length === 1 && !ev.ctrlKey && !ev.metaKey && !ev.altKey && ev.key !== " " && a !== box.Input && !/INPUT|TEXTAREA|BUTTON|SELECT/.test(a.tagName) && !a.closest("[role=listbox], [role=dialog]"))
                    box.Input.focus();
            });
            await pending.WaitAll();
        }
        catch (e) {
            //  Not allowed for this role (403): the shared "not allowed" state instead of a toast on an empty page
            if (Sw.Access && Sw.Access.IsDenied(e))
                Sw.Page.ShowNotAllowed({ Error: e });
            else
                Sw.Fail(e && e.message ? e.message : String(e), true);
        }
        finally {
            remove();
        }
    };
})();