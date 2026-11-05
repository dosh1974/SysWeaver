/**
 * @file ui/cells.js - SysWeaver UI library (Sw), part 6 of 7: value cell renderers (the DOM part of the old ValueFormat.update*).
 *
 * Sw.Cells.Update(el, type, value, flash, format, nextValue, onRefresh) renders a typed value into an element (a table cell, a property view...)
 * using the server defined TableDataColumn.Format "<FormatName>;arg1;arg2;..." (contract 1.5): Default, Number, Url, Tags, PerRowFormat,
 * ByteSize, ByteSpeed, Toggle, Actions, Img, Duration, Json, Text, MD, Uptime, WebColor; without a format the C# type picks the renderer
 * (numbers, DateTime, TimeSpan, Boolean, Type), else Default. The semantics (placeholders, defaults, copy on click, url target prefixes,
 * Actions url prefixes) are the same as the old renderers; the texts come from Sw.Format (core/format.js).
 * Renderers have the old signature (el, value, formats, nextValue, flash, type, onRefresh) and are exposed as Sw.Cells.<Name>.
 * Markdown is rendered with common/marked.js, loaded on demand (Sw.Markdown).
 * Needs core (Sw.Format, Sw.Net, Sw.Url, Sw.Page), ui/base.js, ui/controls.js, ui/overlay.js. Documentation: ui/README.md.
 */

(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const { el, Icon } = Sw;

    /** The core formatter. */
    const F = () => Sw.Format;

    /**
     * Format a template with arguments ({0}, {1}, ... with the {_0} / {^0} / {$0} case variants), undefined for an empty template.
     * @param {string} fmt The template.
     * @param {...*} args The arguments.
     * @returns {string|undefined} The text.
     */
    const sf = (fmt, ...args) => fmt === undefined || fmt === null || fmt === "" ? (fmt === "" ? "" : undefined) : F().StringFormatArgs(fmt, args);

    /**
     * Format a template with an argument array.
     * @param {string} fmt The template.
     * @param {Array} args The arguments.
     * @returns {string} The text.
     */
    const sfa = (fmt, args) => fmt ? F().StringFormatArgs(fmt, args) : (fmt === "" ? "" : fmt);

    /**
     * The css classes the renderers set on the cell (replace them, ex: with the old names "Right", "Monospaced" ..., for old style sheets).
     * @type {{Right: string, Left: string, Center: string, Mono: string, Null: string, False: string, Text: string, Actions: string, Capped: string, Link: string, Kind: string}}
     */
    const Classes = {
        Right: "sw-right",
        Left: "sw-left",
        Center: "sw-center",
        Mono: "sw-mono",
        Null: "sw-null",
        False: "sw-false",
        Text: "sw-cell-text",
        Actions: "sw-cell-actions",
        Capped: "sw-capped",
        Link: "sw-cell-link",
        Kind: "sw-fmt-",
    };

    // ------------------------------------------------------------------ cell state

    /**
     * Get (create) the renderer state of a cell.
     * @param {HTMLElement} e The cell.
     * @returns {{Format: string|null, Click: function|null, Bound: boolean, Live: function|null, Next: number, Attached: boolean, Born: number}} The state.
     */
    function state(e) {
        return e.SwCell || (e.SwCell = { Format: null, Click: null, Bound: false, Live: null, Next: 0, Attached: false, Born: 0 });
    }

    /**
     * Set the click action of a cell (replaces the previous one), optionally keyboard clickable.
     * @param {HTMLElement} e The cell.
     * @param {function(Event):void|null} fn The action (null removes it).
     * @param {boolean} [keyboard] Also Enter / Space (tabindex 0).
     */
    function setClick(e, fn, keyboard) {
        const s = state(e);
        s.Click = fn;
        if (fn && !s.Bound) {
            s.Bound = true;
            e.addEventListener("click", ev => {
                const f = state(e).Click;
                if (f)
                    f(ev);
            });
        }
        if (fn && keyboard)
            Sw.KeyClick(e);
        else if (!fn)
            Sw.RemoveKeyClick(e);
    }

    /**
     * Remove a copy on click handler set by Sw.Clipboard.CopyOnClick.
     * @param {HTMLElement} e The element.
     */
    function removeCopy(e) {
        Sw.Clipboard.RemoveCopy(e);
    }

    /**
     * Give a value a copy action (Sw.Clipboard.CopyOnClick: a plain click copies, as the original copyOnClick; also Ctrl+C on the
     * focused value and its menu, UX guide INP-31); returns the hint text for the tooltip.
     * @param {HTMLElement} e The element.
     * @param {*} v The value (null = the current text).
     * @returns {string} The hint.
     */
    function copyClick(e, v) {
        return Sw.Clipboard.CopyOnClick(e, v);
    }

    /**
     * Track which renderer owns a cell (the old updateFormat). When the renderer changes, the cell is reset: live updates stop, inline colors,
     * the renderer classes, the title, click / copy handlers and the keyboard click are removed.
     * @param {HTMLElement} e The cell.
     * @param {string} name The renderer name.
     * @returns {boolean} True if the renderer is new (first use or changed), so it must set up the cell.
     */
    function Reset(e, name) {
        const s = state(e);
        if (s.Format === name)
            return false;
        const first = s.Format === null;
        s.Format = name;
        if (first)
            return true;
        s.Live = null;
        const st = e.style;
        st.color = "";
        st.backgroundColor = "";
        e.textContent = "";
        e.removeAttribute("title");
        const c = Classes;
        e.classList.remove(c.Right, c.Left, c.Center, c.Null, c.False, c.Mono, c.Text, c.Actions, c.Capped, c.Link, "sw-cell-dt", "sw-cell-dt-utc", "sw-cell-dt-local");
        setClick(e, null);
        removeCopy(e);
        return true;
    }

    /** @type {Map<HTMLElement, string>} The elements whose flash class is (re)started at the end of the current task (doFlash). */
    const flashes = new Map();
    /** @type {boolean} A flushFlashes microtask is queued. */
    let flashQueued = false;

    /**
     * Restart a flash animation class on an element (removed again after 1050 ms). Batched (S7a-A2-2): the classes of every element
     * changed in the same task are (re)started together at its end, so a live refresh of many cells never reads the layout per cell
     * (before: `void e.offsetWidth` after each text change forced one layout of the whole table per changed cell, 50-350 ms of main
     * thread on every refresh of the Performance table), and the animation starts with the next frame (its first painted frame shows
     * the full tint).
     * @param {HTMLElement} e The element.
     * @param {string} flash The class.
     */
    function doFlash(e, flash) {
        //  (a running flash is restarted: the class goes now and comes back after one style pass, flushFlashes)
        if (e.classList.contains(flash)) {
            e.classList.remove(flash);
            e.SwFlashRestart = true;
        }
        flashes.set(e, flash);
        clearTimeout(e.SwFlash);
        if (!flashQueued) {
            flashQueued = true;
            queueMicrotask(flushFlashes);
        }
    }

    /** Start the flash classes queued by doFlash: one style pass for the restarted ones (no layout), then every class at once. */
    function flushFlashes() {
        flashQueued = false;
        const all = Array.from(flashes);
        flashes.clear();
        //  (one style recalculation sees the removed classes, so adding them again restarts the animation; never a layout)
        const re = all.find(([e]) => e.SwFlashRestart && e.isConnected);
        if (re)
            void getComputedStyle(re[0]).animationName;
        for (const [e, flash] of all) {
            e.SwFlashRestart = false;
            e.classList.add(flash);
            e.SwFlash = setTimeout(() => e.classList.remove(flash), 1050);
        }
    }

    /**
     * Set the (trimmed) text and the title of an element if changed (the old updateText).
     * @param {HTMLElement} e The element.
     * @param {*} text The text (null / undefined = "").
     * @param {string} [title] The tooltip (only set when truthy).
     * @param {string|false} [flash] A class to flash when the text changed.
     * @returns {boolean} True if the text changed (or the element had child elements).
     */
    function SetText(e, text, title, flash) {
        text = text === undefined || text === null ? "" : String(text).trim();
        if (title) {
            title = String(title);
            if (e.title !== title)
                e.title = title;
        }
        if (text === e.textContent && !e.firstElementChild)
            return false;
        e.textContent = text;
        if (flash)
            doFlash(e, flash);
        return true;
    }

    /**
     * Replace the children of an element and set the title (the old updateHtml, with nodes instead of html text).
     * Compares a signature to avoid rebuilding unchanged content.
     * @param {HTMLElement} e The element.
     * @param {string} sig A signature of the content (unchanged = no rebuild).
     * @param {function():Node[]} build Builds the new children.
     * @param {string} [title] The tooltip.
     * @param {string|false} [flash] A class to flash when changed.
     * @returns {boolean} True if rebuilt.
     */
    function SetNodes(e, sig, build, title, flash) {
        if (title !== undefined && title !== null) {
            if (title)
                e.title = title;
            else
                e.removeAttribute("title");
        }
        const s = state(e);
        if (s.Sig === sig && e.firstChild)
            return false;
        s.Sig = sig;
        //  A rebuilt cell keeps the keyboard place (FOC-1): the focused button inside it gets its successor (same name / position)
        const keep = e.contains(document.activeElement) && document.activeElement !== e && Sw.Focus && Sw.Focus.Keep ? Sw.Focus.Keep(e) : null;
        e.replaceChildren(...build());
        if (keep) {
            keep.Restore();
            keep.Stop();
        }
        if (flash)
            doFlash(e, flash);
        return true;
    }

    // ------------------------------------------------------------------ live updates (Uptime, live DateTime)

    /** @type {Set<HTMLElement>} Cells with live updates. */
    const live = new Set();
    /** @type {number} The shared timer. */
    let liveTimer = 0;

    /** Shared ticker of the live cells: each cell decides when it's due next; cells that were attached and got removed stop. */
    function tickLive() {
        const now = performance.now();
        for (const e of live) {
            const s = e.SwCell;
            if (!s || !s.Live) {
                live.delete(e);
                continue;
            }
            const c = e.isConnected;
            if (!c && (s.Attached || now - s.Born > 60000)) {
                s.Live = null;
                live.delete(e);
                continue;
            }
            s.Attached = s.Attached || c;
            if (now >= s.Next)
                s.Next = now + s.Live();
        }
        if (!live.size) {
            clearInterval(liveTimer);
            liveTimer = 0;
        }
    }

    /**
     * Start live updates of a cell.
     * @param {HTMLElement} e The cell.
     * @param {function():number} fn Updates the cell, returns the ms until the next update.
     */
    function addLive(e, fn) {
        const s = state(e);
        s.Live = fn;
        s.Born = performance.now();
        s.Attached = e.isConnected;
        s.Next = s.Born + fn();
        live.add(e);
        if (!liveTimer)
            liveTimer = setInterval(tickLive, 100);
    }

    // ------------------------------------------------------------------ links

    /**
     * Split a target prefix from an url: '+' or none = _blank, '*' = _self, '^' = _top, '-' = _parent (the old getUrlTarget).
     * @param {string} url Url with an optional prefix.
     * @returns {string[]} [url, target] (["", ""] for empty urls).
     */
    function UrlTarget(url) {
        if (!url)
            return ["", ""];
        switch (url.charAt(0)) {
            case "+": return [url.substring(1), "_blank"];
            case "*": return [url.substring(1), "_self"];
            case "^": return [url.substring(1), "_top"];
            case "-": return [url.substring(1), "_parent"];
        }
        return [url, "_blank"];
    }

    /**
     * Resolve an url against the page.
     * @param {string} u The url.
     * @returns {string} The absolute url ("" for empty).
     */
    function absUrl(u) {
        if (!u)
            return "";
        try {
            return Sw.Url && Sw.Url.Absolute ? Sw.Url.Absolute(u) : new URL(u, document.baseURI).href;
        }
        catch {
            return u;
        }
    }

    /**
     * Open an url as a link click (the old onLink). A click with a modifier key only moves the focus (UX guide INP-2, Sw.BadClick); it
     * used to copy the url (copy it from a copy button or a menu instead).
     * @param {Event} ev The click event.
     * @param {string} url The url.
     * @param {string} [target] The window target (default "_self").
     * @param {boolean} [useEdit] Open with the registered editor.
     * @param {boolean} [useView] Open with the registered viewer (takes precedence).
     */
    function OnLink(ev, url, target, useEdit, useView) {
        if (Sw.BadClick(ev))
            return;
        const P = Sw.Page || {};
        if (useView && P.View)
            P.View(url, target);
        else if (useEdit && P.Edit)
            P.Edit(url, target);
        else if (P.Navigate)
            P.Navigate(url, target || "_self");
        else
            window.open(url, target || "_self");
    }

    /**
     * Create a link element (an anchor with a real href: middle click / open in new tab work; a plain click navigates with Sw.Page.Navigate;
     * a click with a modifier key keeps the browser's link gestures, ex: Ctrl+click opens a new tab, UX guide INP-2). The old createLink /
     * fixLinks building block.
     * @param {string} url The url.
     * @param {string} [text] The text, or the image src when isImage.
     * @param {string} [target] The window target (default "_self").
     * @param {string} [title] The tooltip.
     * @param {boolean} [isImage] Show a (smoothly loaded) image instead of text.
     * @param {object} [o] Options: { UseEdit, UseView, Class }.
     * @returns {HTMLAnchorElement} The link (class sw-link).
     */
    function CreateLink(url, text, target, title, isImage, o) {
        o = o || {};
        if (Sw.Work && typeof Sw.Work.CheckOptions === "function")
            Sw.Work.CheckOptions("Sw.Cells.CreateLink", o);
        //  Target keywords are case-insensitive ("_TOP" is "_top"): compare the normalized target only
        target = Sw.Page && Sw.Page.Target ? Sw.Page.Target(target, "_self") : (target || "_self");
        const own = target === "_self";
        const a = el("a.sw-link", { href: url, title: title || null, class: o.Class || null, attrs: { target: own ? null : target, rel: own ? null : "noopener" } });
        a.addEventListener("click", ev => {
            //  A real link: Ctrl / Shift / Meta + click and the middle button are the browser's (new tab, new window)
            if (!Sw.IsPureEvent(ev))
                return;
            ev.preventDefault();
            OnLink(ev, url, target, o.UseEdit, o.UseView);
        });
        if (text) {
            if (isImage) {
                a.classList.add("sw-link-image");
                const i = Sw.CreateImg({ ShowError: true });
                i.src = text;
                i.alt = title || "";
                a.append(i);
            }
            else
                a.textContent = text;
        }
        return a;
    }

    // ------------------------------------------------------------------ popups (json, text, markdown)

    /**
     * A copy button for a popup's tool area.
     * @param {string} copy The text to copy.
     * @param {string} title The tooltip.
     * @returns {HTMLButtonElement} The button.
     */
    function copyButton(copy, title) {
        return new Sw.Button({ Icon: "copy", Kind: "ghost", Title: title, OnClick: () => Sw.Clipboard.Copy(copy, { Generic: true }) }).E;
    }

    /**
     * Show json text in a dialog (pretty printed and highlighted).
     * @param {string} text The json (shown as is when it can't be parsed).
     * @param {string} [copy] A text the copy button copies (no button when empty).
     * @param {string} [title] The dialog title.
     * @returns {Promise<void>} Resolves when closed.
     */
    async function ShowJson(text, copy, title) {
        let fmt = text;
        try {
            fmt = JSON.stringify(JSON.parse(text), null, 4);
        }
        catch {
        }
        const pre = el("pre.sw-code-json.sw-selectable", { html: F().JsonToHtml(fmt) });
        await Sw.Dialog.Show({
            Title: title || "", Wide: true, Class: "sw-text-dialog",
            Content: (body, close, tools) => {
                if (copy)
                    tools.append(copyButton(copy, _TF("Copy the original json text to the clipboard", "This is a tool tip on an icon that will copy some text to the clipboard when clicked")));
                return pre;
            },
        });
    }

    /**
     * Show a long text in a dialog.
     * @param {string} text The text.
     * @param {boolean} [mono] Monospace font.
     * @param {string} [copy] A text the copy button copies (no button when empty).
     * @param {string} [title] The dialog title.
     * @returns {Promise<void>} Resolves when closed.
     */
    async function ShowText(text, mono, copy, title) {
        const pre = el("div.sw-longtext.sw-selectable", { text });
        if (mono)
            pre.classList.add("sw-mono");
        await Sw.Dialog.Show({
            Title: title || "", Wide: true, Class: "sw-text-dialog",
            Content: (body, close, tools) => {
                if (copy)
                    tools.append(copyButton(copy, _TF("Copy the full text to the clipboard", "This is a tool tip on an icon that will copy some text to the clipboard when clicked")));
                return pre;
            },
        });
    }

    /**
     * Show markdown in a dialog.
     * @param {string} text The markdown.
     * @param {string} [copy] A text the copy button copies (no button when empty).
     * @param {string} [title] The dialog title.
     * @returns {Promise<void>} Resolves when closed.
     */
    async function ShowMarkdown(text, copy, title) {
        await Sw.Dialog.Show({
            Title: title || "", Wide: true, Class: "sw-text-dialog",
            Content: async (body, close, tools) => {
                if (copy)
                    tools.append(copyButton(copy, _TF("Copy the original MD text to the clipboard", "This is a tool tip on an icon that will copy some text to the clipboard when clicked")));
                const md = el("div.sw-md.sw-selectable");
                md.innerHTML = await Markdown.ToHtml(text);
                return md;
            },
        });
    }

    // ------------------------------------------------------------------ markdown

    /** Raw html tags allowed in markdown (formatting only). */
    const mdAllowed = new Set(["hr", "br", "img", "figure", "figcaption", "b", "strong", "i", "em", "mark", "small", "del", "ins", "sub", "sup", "blockquote", "q", "abbr", "address", "cite", "bdo", "u", "s", "kbd", "code",
        //  Folded sections (ex: a privacy notice in a welcome message): each tag on its own line between blank lines, no attributes (so
        //  they start closed, "open" is removed)
        "details", "summary"]);

    /**
     * Test if a link target is safe (http(s), mailto, tel, relative, fragment), blocks script urls.
     * @param {string} href The url.
     * @returns {boolean} True if safe.
     */
    function safeHref(href) {
        const h = String(href || "").trim().toLowerCase().replace(/[\s\u0000-\u001f]/g, "");
        return !/^(javascript|vbscript|data):/.test(h) || /^data:image\//.test(h);
    }

    /** File extensions of same origin static images that markdown loads automatically. */
    const mdImageExt = /\.(png|jpe?g|gif|webp|avif|bmp|ico|svg)$/;

    /**
     * Test if a markdown image may load automatically (without a click). Allowed: `data:image/(png|jpeg|gif|webp|avif|bmp)` and same
     * origin static image files: http(s), no query string, no `Api` path segment, an image file extension, no (double) encoded path tricks.
     * Everything else (other origins: tracking pixels; same origin api / query urls: credentialed GET requests) is rendered as a
     * "Show image" placeholder that loads the image only when clicked.
     * @param {string} src The image url (absolute or relative to the document).
     * @returns {boolean} True if the image may load automatically.
     */
    function mdImageAutoLoad(src) {
        const s = String(src || "").trim();
        if (/^data:/i.test(s))
            return /^data:image\/(png|jpe?g|gif|webp|avif|bmp)[;,]/i.test(s.replace(/[\s\u0000-\u001f]/g, ""));
        let u;
        try {
            u = new URL(s, document.baseURI);
        }
        catch {
            return false;
        }
        if ((u.protocol !== "http:" && u.protocol !== "https:") || u.origin !== window.location.origin || u.search || u.username || u.password)
            return false;
        let path;
        try {
            path = decodeURIComponent(u.pathname).toLowerCase();
        }
        catch {
            return false;
        }
        if (path.includes("%") || path.includes("\\"))
            return false;
        return !path.split("/").includes("api") && mdImageExt.test(path);
    }

    /**
     * Create the placeholder of an image that must not load automatically: a button like span ("Show image") that loads the image on
     * click / Enter / Space (see the delegated handler below). The url is shown in the tool tip.
     * @param {string} src The image url.
     * @param {string} alt The alternative text.
     * @returns {HTMLElement} The placeholder.
     */
    function mdImagePlaceholder(src, alt) {
        const p = document.createElement("span");
        p.className = "sw-md-img-off";
        p.setAttribute("role", "button");
        p.setAttribute("tabindex", "0");
        p.setAttribute("data-sw-src", src);
        if (alt)
            p.setAttribute("data-sw-alt", alt);
        p.title = _T('Load the image from "{0}"', src, "Tool tip of a placeholder for an image in a message that is not loaded automatically (it could track the reader), {0} is the image url");
        p.textContent = alt
            ? _T('Show image "{0}"', alt, "Text of a placeholder for an image in a message that is not loaded automatically, {0} is the image description")
            : _T("Show image", "Text of a placeholder for an image in a message that is not loaded automatically");
        return p;
    }

    /**
     * Replace an image placeholder with the image (no referrer).
     * @param {HTMLElement} p The placeholder (.sw-md-img-off).
     */
    function mdImageShow(p) {
        const i = document.createElement("img");
        i.referrerPolicy = "no-referrer";
        i.alt = p.getAttribute("data-sw-alt") || "";
        i.src = p.getAttribute("data-sw-src") || "";
        p.replaceWith(i);
    }

    //  Delegated handler of the image placeholders (markdown html is set with innerHTML, so the placeholders have no own listeners).
    //  Capture phase: the click does not reach a surrounding link or row handler.
    document.addEventListener("click", ev => {
        const p = ev.target && ev.target.closest ? ev.target.closest(".sw-md-img-off") : null;
        if (!p)
            return;
        ev.preventDefault();
        ev.stopPropagation();
        mdImageShow(p);
    }, true);
    document.addEventListener("keydown", ev => {
        if (ev.key !== "Enter" && ev.key !== " ")
            return;
        const p = ev.target && ev.target.closest ? ev.target.closest(".sw-md-img-off") : null;
        if (!p)
            return;
        ev.preventDefault();
        ev.stopPropagation();
        mdImageShow(p);
    }, true);

    /**
     * Elements the markdown output may contain (what marked generates plus mdAllowed). Others are unwrapped (children kept),
     * except mdDropped which are removed with their content.
     */
    const mdTags = new Set([...mdAllowed, "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "a", "pre", "table", "thead", "tbody", "tfoot", "tr", "th", "td", "input", "span", "div"]);

    /** Elements removed with their content by the markdown sanitizer. */
    const mdDropped = new Set(["script", "style", "iframe", "frame", "frameset", "object", "embed", "applet", "template", "noscript", "svg", "math", "link", "meta", "base", "form", "textarea", "select", "button", "title", "head"]);

    /**
     * Allowed attributes per element of the markdown output ("*" = any element). Values are validated by mdAttrOk.
     * Everything else (style, class, id, srcset, on*, data-*, ...) is removed.
     */
    const mdAttrs = {
        "*": ["title"],
        a: ["href", "target", "rel", "class"],
        img: ["src", "alt"],
        th: ["align"],
        td: ["align"],
        ol: ["start"],
        code: ["class"],
        input: ["type", "checked", "disabled"],
        bdo: ["dir"],
    };

    /**
     * Validate an allowed attribute value of the markdown output.
     * @param {string} tag The lower case tag name.
     * @param {string} name The lower case attribute name.
     * @param {string} value The value.
     * @returns {boolean} True to keep the attribute.
     */
    function mdAttrOk(tag, name, value) {
        switch (name) {
            case "href":
            case "src":
                return safeHref(value);
            case "target":
                return value === "_blank";
            case "rel":
                return value === "noopener";
            case "class":
                // Only the classes the renderers set (link images, code block languages).
                return tag === "a" ? value === "Image sw-link-image" : /^language-[\w+#.-]{1,32}$/.test(value);
            case "align":
                return /^(left|right|center)$/.test(value);
            case "start":
                return /^\d{1,9}$/.test(value);
            case "type":
                return value === "checkbox";
            case "dir":
                return /^(ltr|rtl)$/.test(value);
        }
        return true;
    }

    /**
     * Sanitize the html produced from markdown with an allow list of elements and attributes (DOM based, in an inert template:
     * nothing loads or runs while parsing). Unknown elements are unwrapped, dangerous ones dropped, comments removed,
     * check boxes forced disabled, images that may not load automatically (mdImageAutoLoad) replaced by a "Show image" placeholder.
     * @param {string} html The html.
     * @returns {string} The sanitized html.
     */
    function sanitizeMdHtml(html) {
        const tpl = document.createElement("template");
        tpl.innerHTML = html;
        const walk = parent => {
            for (let n = parent.firstChild; n; ) {
                const next = n.nextSibling;
                if (n.nodeType === 1) {
                    const tag = n.localName;
                    if (mdDropped.has(tag) || n.namespaceURI !== "http://www.w3.org/1999/xhtml" || (tag === "input" && n.getAttribute("type") !== "checkbox")) {
                        n.remove();
                    }
                    else if (!mdTags.has(tag)) {
                        walk(n);
                        n.replaceWith(...n.childNodes);
                    }
                    else {
                        const allowed = mdAttrs[tag];
                        for (const a of Array.from(n.attributes)) {
                            const name = a.name.toLowerCase();
                            if (!((name === "title" || (allowed && allowed.includes(name))) && mdAttrOk(tag, name, a.value)))
                                n.removeAttribute(a.name);
                        }
                        if (tag === "input")
                            n.setAttribute("disabled", "");
                        if (tag === "img") {
                            const src = n.getAttribute("src");
                            if (!src)
                                n.remove();
                            else if (!mdImageAutoLoad(src))
                                n.replaceWith(mdImagePlaceholder(src, n.getAttribute("alt") || ""));
                            else
                                n.setAttribute("referrerpolicy", "no-referrer");
                            n = next;
                            continue;
                        }
                        walk(n);
                    }
                }
                else if (n.nodeType !== 3)
                    n.remove();
                n = next;
            }
        };
        walk(tpl.content);
        return tpl.innerHTML;
    }

    /** @type {boolean} True once the custom renderers are registered with marked. */
    let mdReady = false;

    /** Markdown to html (common/marked.js, loaded on demand) with a small allow list of raw html tags, safe links and an element / attribute allow list sanitizer on the output. */
    const Markdown = {
        /**
         * Load marked.js (once).
         * @returns {Promise<object>} The global marked.
         */
        async Load() {
            if (!window.marked)
                await Sw.Net.IncludeJs(null, "marked.js");
            const m = window.marked;
            if (m && !mdReady) {
                mdReady = true;
                let inLink = false;
                const H = F().HtmlAttributeSafe;
                const renderer = {
                    link(l) {
                        let text;
                        inLink = true;
                        try {
                            text = this.parser.parseInline(l.tokens);
                        }
                        finally {
                            inLink = false;
                        }
                        let href = l.href;
                        if (!safeHref(href))
                            return text;
                        if (Sw.Url && Sw.Url.IsAbsolute && !Sw.Url.IsAbsolute(href) && Sw.Url.AppendTheme)
                            href = Sw.Url.AppendTheme(href);
                        const title = l.title || _T('Open "{0}" in a new tab', href, "Tool tip that informs the user that an URL can be opened by clicking on the element");
                        return '<a target="_blank" rel="noopener" title="' + H(title) + '" href="' + H(href) + '"' + (text.startsWith("<img") ? ' class="Image sw-link-image"' : "") + ">" + text + "</a>";
                    },
                    image(l) {
                        if (inLink)
                            return false;
                        const href = l.href;
                        if (!safeHref(href))
                            return H(l.text || "");
                        let title = l.title || _T('Open "{0}" in a new tab', href, "Tool tip that informs the user that an image can be opened by clicking on the element");
                        if (l.text)
                            title = l.text + "\n\n" + title;
                        return '<a class="Image sw-link-image" target="_blank" rel="noopener" title="' + H(title) + '" href="' + H(href) + '"><img src="' + H(href) + '" alt="' + H(l.text || "") + '"></a>';
                    },
                    html(l) {
                        const m2 = /^<\/?([a-z][a-z0-9]*)\b[^>]*>\s*$/i.exec(l.raw.trim());
                        if (m2 && mdAllowed.has(m2[1].toLowerCase()) && !/\son\w+\s*=/i.test(l.raw) && !/javascript:/i.test(l.raw))
                            return false;
                        return "";
                    },
                };
                m.use({ renderer });
            }
            return m;
        },
        /**
         * Convert markdown to html.
         * @param {string} text The markdown.
         * @returns {Promise<string>} The html.
         */
        async ToHtml(text) {
            const m = await Markdown.Load();
            if (!m)
                return F().HtmlSafe(text || "");
            return sanitizeMdHtml(await m.parse(text || ""));
        },
        /**
         * Sanitize html with the allow list of the markdown output (elements, attributes, safe links; scripts, styles, frames,
         * forms, event handlers removed; images that may not load automatically replaced by a "Show image" placeholder).
         * Used for html from the server that is shown inline (ex: chat messages in the Html format).
         * @param {string} html The html.
         * @returns {string} The sanitized html.
         */
        Sanitize(html) {
            return sanitizeMdHtml(String(html || ""));
        },
        /**
         * Render markdown into an element (the old ValueFormat.SetMarkDown).
         * @param {HTMLElement} e The element.
         * @param {string} text The markdown.
         * @returns {Promise<void>}
         */
        async Set(e, text) {
            e.innerHTML = await Markdown.ToHtml(text);
        },
        /**
         * Test if an image url may load automatically in user content (the markdown image rule: data:image png/jpeg/gif/webp/avif/bmp
         * or a same origin static image file without query string outside Api/). Use it for other user supplied image urls too.
         * @param {string} src The image url.
         * @returns {boolean} True if it may load without a click.
         */
        CanAutoLoadImage: mdImageAutoLoad,
        /**
         * Create a "Show image" placeholder that loads the image on click / Enter / Space (for images that may not load automatically).
         * @param {string} src The image url.
         * @param {string} [alt] The alternative text.
         * @returns {HTMLElement} The placeholder element (class sw-md-img-off).
         */
        ImagePlaceholder: (src, alt) => mdImagePlaceholder(src, alt || ""),
    };

    // ------------------------------------------------------------------ renderers

    /**
     * Cap a text at a length or the first line break.
     * @param {string} value The text.
     * @param {string} capArg The cap argument (min 4).
     * @returns {{Text: string, Capped: boolean}} The capped text.
     */
    function cap(value, capArg) {
        let c = parseInt(capArg);
        if (!(c >= 4))
            c = 4;
        const n = value.indexOf("\n"), r = value.indexOf("\r");
        if (n >= 0 && n < c)
            c = n;
        if (r >= 0 && r < c)
            c = r;
        return c < value.length ? { Text: value.substring(0, c), Capped: true } : { Text: value, Capped: false };
    }

    /**
     * Renderer "Default" (class Text): text = formats[1] (default "{0}"; null = ""), title = formats[2] (default "Raw: {2}" when the text differs),
     * formats[3] = copy on click (default on; a string is a template for the copied value). Args: {0} text, {1} nextValue, {2} value.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     */
    function Default(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "Default"))
            e.classList.add(Classes.Text);
        const text = value === null || value === undefined ? "" : sf(formats[1] ?? "{0}", value, nextValue);
        let title = sf(formats[2] ?? (text != value ? "Raw: {2}" : ""), text, nextValue, value);
        const copy = formats[3];
        if (copy || copy === undefined) {
            let cv = value;
            if (typeof copy === "string")
                cv = sf(copy, text, nextValue, value);
            title = F().JoinNonEmpty("\n\n", title, copyClick(e, cv));
        }
        SetText(e, text, title, flash);
    }

    /**
     * Renderer for C# type names (System.Type): a link to ../edit/type.html?q=<value> in the same window (unless formats are given), via Url.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     */
    function Type(e, value, formats, nextValue, flash, type, onRefresh) {
        if (!formats.length)
            formats = ["Url", "{0}", "*../edit/type.html?q={2}", _TF('Show information about the type "{0}".', "Tool tip to indicate that extra information about a C# type can be viewed by clicking on the element")];
        Url(e, value, formats, nextValue, flash, type, onRefresh);
    }

    /** @type {Promise|null} The load of iso_data/currencies.js (once). */
    let currencyLoad = null;
    /** @type {boolean} True when the load of iso_data/currencies.js has finished (loaded or failed). */
    let currencyDone = false;

    /**
     * The currency record (iso_data/currencies.js, the global IsoCurrency class of the IsoData service) of an ISO 4217 code.
     * @param {string} code The code.
     * @returns {object|null|undefined} The record, null if unknown (or the IsoData service is missing), undefined while not loaded.
     */
    function currencyInfo(code) {
        // eslint-disable-next-line no-undef
        if (typeof IsoCurrency !== "undefined" && IsoCurrency.Get)
            // eslint-disable-next-line no-undef
            return IsoCurrency.Get(code) || null;
        return currencyDone ? null : undefined;
    }

    /**
     * Load iso_data/currencies.js once (a missing IsoDataService: the code is shown, see NumberCell), then call fn.
     * @param {function():void} fn Called when the load has finished.
     */
    function loadCurrencies(fn) {
        if (!currencyLoad)
            currencyLoad = isoData()
                .then(have => have ? Sw.Net.IncludeJs(null, Sw.Url.Root + "iso_data/currencies.js", true) : null)
                .catch(() => null)
                .then(() => { currencyDone = true; });
        currencyLoad.then(fn);
    }

    /**
     * Renderer "Number" (classes Right, Mono): formats[1] = decimals (negative / missing: that many decimals only for decimal types, default -2),
     * formats[2] = text ({0} formatted, {1} nextValue, {2} raw; exactly "{0} s" = a number of seconds, shown as a duration, spanText),
     * formats[3] = title (default "Raw: {2}"), formats[4] = copy on click (default on). Class False for negative values, Null for zero.
     * A tiny value rounds to the format's decimals as the original ("Number;4": 0.0000, not 0.000041; P2-A / P3-A-8).
     * formats[5] = currency (C# TableDataCurrency: an ISO 4217 code or a template of it, ex: "{1}" = the next column): the value is an amount,
     * shown with the currency symbol from iso_data/currencies.js (loaded on demand, currencyInfo) and the usual separators for every currency
     * ("$1 234.50", "1 234.50 kr"), so a column reads the same on every row; negative decimals = the decimals of the currency. Unknown currency
     * (or no IsoData service): "1 234.50 XYZ".
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     */
    function NumberCell(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "Number"))
            e.classList.add(Classes.Right, Classes.Mono);
        let decimals = parseInt(formats[1]);
        if (!decimals)
            decimals = -2;
        //  An amount in a currency: its symbol and (for negative decimals) its decimals
        const code = formats[5] && value !== null && value !== undefined ? sf(formats[5], value, nextValue, value) : "";
        let cur = null;
        if (code) {
            cur = currencyInfo(code);
            const st = state(e);
            st.Currency = code + "|" + value;
            if (cur === undefined) {
                //  Not loaded yet: render with the code now, again when iso_data/currencies.js is loaded (if the cell still shows this value)
                const sig = st.Currency;
                loadCurrencies(() => {
                    if (st.Currency === sig && e.isConnected)
                        NumberCell(e, value, formats, nextValue, false, type, onRefresh);
                });
            }
            if (cur && decimals < 0)
                decimals = cur.DecimalNumbers | 0;
        }
        if (decimals < 0)
            decimals = F().DecimalTypes[type] ? -decimals : 0;
        const vs = value === null || value === undefined ? "" : F().Number(+value, decimals);
        let text = sf(formats[2] ?? "{0}", vs, nextValue, value);
        if (code && vs !== "")
            text = cur ? (cur.SymbolPrefix || "") + text + (cur.SymbolSuffix || "") : text + " " + code;
        let title = sf(formats[3] ?? "Raw: {2}", vs, nextValue, value, text);
        if (code)
            title = F().JoinNonEmpty("\n", cur && cur.Name ? cur.Name + " (" + cur.Iso4217 + ")" : code, title);
        const copy = formats[4];
        if (copy || copy === undefined)
            title = F().JoinNonEmpty("\n\n", title, copyClick(e, value));
        SetText(e, text, title, flash);
        e.classList.toggle(Classes.False, value < 0);
        e.classList.toggle(Classes.Null, !(value < 0) && !(value > 0));
    }

    /**
     * Renderer "WebColor": the color text (formats[1], default "{0}") on the color as background, class False for dark colors (luma < 128),
     * tooltip formats[2] + the RGBA components + a copy hint; click copies the color text. Empty values clear the cell.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {boolean|undefined} True when a color was rendered.
     */
    function WebColor(e, value, formats, nextValue, flash, type, onRefresh) {
        if (!value) {
            Reset(e, "");
            SetText(e, "", "", false);
            return;
        }
        Reset(e, "WebColor");
        const text = sf(formats[1] ?? "{0}", value, nextValue);
        e.style.backgroundColor = text;
        let title = sf(formats[2] ?? "", value, nextValue, text) || "";
        const c = Sw.Color.ParseCss(text) || [0, 0, 0, 1];
        const [r, g, b, a] = c;
        e.classList.toggle(Classes.False, Sw.Color.Luma(r, g, b) < 128);
        if (title)
            title += "\n\n";
        title += _TF("Red", "Prefix for a tool tip value, displaying the red part of a RGB color") + ": " + Math.round(r) + "\n";
        title += _TF("Green", "Prefix for a tool tip value, displaying the green part of a RGB color") + ": " + Math.round(g) + "\n";
        title += _TF("Blue", "Prefix for a tool tip value, displaying the blue part of a RGB color") + ": " + Math.round(b) + "\n";
        title += _TF("Alpha", "Prefix for a tool tip value, displaying the alpha part of a RGBA color") + ": " + Math.round(a * 255) + " (" + F().Number(a, -3) + ")\n";
        title += "\n" + copyClick(e, text);
        SetText(e, text, title, flash);
        return true;
    }

    /**
     * Renderer "Url": text = formats[1] (default "{0}"), url = formats[2] (default "{2}" = the text; a target prefix + _blank, * _self, ^ _top, - _parent),
     * title = formats[3] (default 'Click to open "{3}".'). Args: {0} value, {1} nextValue, {2} text, {3} absolute url. Plain text without an url.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     */
    function Url(e, value, formats, nextValue, flash, type, onRefresh) {
        Reset(e, "Url");
        if (value === undefined || value === null) {
            SetText(e, "", "", flash);
            return;
        }
        const text = sf(formats[1] ?? "{0}", value, nextValue);
        const ut = UrlTarget(sf(formats[2] ?? "{2}", value, nextValue, text));
        const url = absUrl(ut[0]);
        const target = ut[1];
        let title = sf(formats[3] ?? (url ? _TF('Open "{3}".', "Tool tip that indicates that an url can be viewed by clicking on an element") : ""), value, nextValue, text, url);
        const ext = External(url);
        if (ext && ext.Offline) {
            //  An internet link without internet: plain text, the address in the tool tip
            e.classList.remove(Classes.Link);
            SetNodes(e, "o" + url + "\n" + text, () => [el("span.sw-link-off", { text })], ext.Title, flash);
            return;
        }
        if (url) {
            e.classList.add(Classes.Link);
            if (ext)
                title = ext.Search ? ext.Title : F().JoinNonEmpty("\n", title, ext.Title);
            SetNodes(e, "u" + url + "\n" + target + "\n" + text + (ext ? "\ne" : ""), () => [ext ? ExternalLink(url, text, ext) : CreateLink(url, text, target)], title, flash);
            return;
        }
        e.classList.remove(Classes.Link);
        SetText(e, text, title, flash);
    }

    // ------------------------------------------------------------------ external links

    /**
     * Is an url a link to another site (http / https, another origin than this page), and how is it shown: the host, whether it is a
     * web search (the cell text is sent to the search engine, so it is named as such), whether the browser is offline (navigator.onLine
     * false, or Sw.Cells.Internet set to false by the app after a failed connectivity hint: then the link is shown as plain text with
     * the address in the tool tip), and the tool tip text ("Opens <host> in a new tab" / "Search the web: opens <host> ..." / "Needs
     * the internet: <url>").
     * @param {string} url The absolute url.
     * @returns {?{Host: string, Search: boolean, Offline: boolean, Title: string}} Null for links of this site (and non http urls).
     */
    function External(url) {
        if (!url)
            return null;
        let u;
        try {
            u = new URL(url, document.baseURI);
        }
        catch {
            return null;
        }
        if ((u.protocol !== "http:" && u.protocol !== "https:") || u.origin === window.location.origin)
            return null;
        const host = u.host;
        const offline = navigator.onLine === false || Sw.Cells.Internet === false;
        const q = /(^|\.)(google|bing|duckduckgo|yahoo)\.[a-z.]+$/i.test(u.hostname) && /\/search$/i.test(u.pathname) ? (u.searchParams.get("q") || "") : null;
        let title;
        if (offline)
            title = _T("Needs the internet (not connected): {0}", u.href, "Tool tip of a link to another web site shown as plain text while there is no internet connection, {0} is the address");
        else if (q !== null)
            title = _T("Search the web: opens {0} in a new tab and sends this search to it:", host, "Tool tip of a web search link in a table cell (the searched text follows on the next line), {0} is the search site (ex: www.google.com)") +
                "\n" + (q.length > 200 ? q.substring(0, 199) + "…" : q);
        else
            title = _T("Opens {0} in a new tab (another web site)", host, "Tool tip of a link to another web site, {0} is the site (host name)");
        return { Host: host, Search: q !== null, Offline: offline, Title: title };
    }

    /**
     * A link to another site (External): a new tab without opener or referrer, the external icon after the text (a web search: the
     * search icon before the text, so it shows even when a long text is cut), and the External tool tip.
     * @param {string} url The url.
     * @param {?string} [text] The text (default: the url; null: the caller appends the content, ex: an image).
     * @param {{Host: string, Search: boolean, Title: string}} [ext] The External result (default: External(url)).
     * @returns {HTMLAnchorElement} The link (class sw-link sw-link-ext).
     */
    function ExternalLink(url, text, ext) {
        //  the optional arguments have defaults (it threw without ext): the text is the url, ext is worked out from it
        if (text === undefined)
            text = String(url ?? "");
        ext = ext || External(url) || { Host: "", Search: false, Title: _TF("Opens in a new tab", "Tool tip of a link that opens in a new browser tab") };
        const a = CreateLink(url, text, "_blank", ext.Title, false, { Class: "sw-link-ext" });
        a.rel = "noopener noreferrer";
        const icon = ext.Search ? Icon("search", "sw-link-ext-icon sw-link-search-icon") : Icon("external", "sw-link-ext-icon");
        //  The icon never wraps away from the text (a narrow cell showed "Excel" / "↗" on two lines): it shares a no-wrap span with the
        //  last word (the first one before the leading search icon); of a long word only the last / first 6 characters stay with it
        const s = typeof text === "string" && a.childElementCount === 0 ? text : "";
        if (!s) {
            if (ext.Search)
                a.prepend(icon);
            else
                a.append(icon);
            return a;
        }
        let tail;
        if (ext.Search) {
            const m = /^\S{1,12}(?=\s|$)/.exec(s);
            tail = m ? m[0] : s.slice(0, 6);
        }
        else {
            const m = /(^|\s)(\S{1,12})$/.exec(s);
            tail = m ? m[2] : s.slice(-6);
        }
        const head = ext.Search ? s.slice(tail.length) : s.slice(0, s.length - tail.length);
        const keep = el("span.sw-link-keep", null, ext.Search ? icon : null, tail, ext.Search ? null : icon);
        a.replaceChildren(...(ext.Search ? [keep, head] : [head, keep]).filter(x => x !== ""));
        return a;
    }

    /** @type {boolean|null} Can iso_data/ (IsoDataService) be loaded: always assumed, like the original (see isoData). */
    const isoKnown = true;

    /**
     * Images under iso_data/ (country / language / currency flags) are served by the optional IsoDataService. Like the original, they
     * are requested directly, without a probe (no Api/HaveService request): without the service a flag fails to load and its cell
     * shows the value as text (Img), never a broken image.
     * @returns {Promise<boolean>} True (the flags are assumed to exist).
     */
    function isoData() {
        return Promise.resolve(true);
    }

    /**
     * Renderer "Img": formats[1] = src template (a leading '-' aligns left, '+' right, '*' none), formats[2] = click url template (target prefix allowed),
     * formats[3] = title, formats[4] / [5] = max width / height (px, scaled by --ThemeIconSize), formats[6] = prefix for relative values.
     * A picture that fails to load shows the cell value as text (ex: "SE", "GET") instead of a broken image; iso_data/ flags are not requested
     * a flag that fails to load (no IsoDataService) shows the value as text.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     */
    function Img(e, value, formats, nextValue, flash, type, onRefresh) {
        Reset(e, "Img");
        if (value === undefined || value === null) {
            SetText(e, "", "", flash);
            return;
        }
        const pre = formats[6];
        if (pre && !(Sw.Url && Sw.Url.IsAbsolute ? Sw.Url.IsAbsolute(value) : /^[a-z]+:/i.test(value)))
            value = pre + value;
        let src = sf(formats[1] ?? "{0}", value, nextValue);
        const ut = UrlTarget(formats[2] ? sf(formats[2], value, nextValue, src) : null);
        const url = absUrl(ut[0]);
        const target = ut[1];
        let title = sf(formats[3] ?? (url ? _TF('Open "{3}".', "Tool tip that indicates that an image can be viewed by clicking on an element") : ""), value, nextValue, src, url);
        //  A short word value (ex: "POST" / "GET" drawn as an icon) names the picture: alt text and tool tip say the value, not the image
        //  url that a generic server title ("Click to open "{3}".") would show
        const raw = String(value).trim();
        const name = raw && raw.length <= 40 && !/[\/\\:?#]/.test(raw) && (formats[3] == null || (title && ((url && title.includes(url)) || title.includes(src))))
            ? raw : "";
        if (name)
            title = url
                ? _T("{0} (opens the picture)", name, "Tool tip of a picture in a table cell that shows a value (ex: POST) and opens the picture when clicked, {0} is the value")
                : name;
        let align = null;
        const f = src.charAt(0);
        if (f === "-" || f === "+" || f === "*") {
            align = f === "-" ? Classes.Left : f === "+" ? Classes.Right : null;
            src = src.substring(1);
        }
        e.classList.toggle(Classes.Left, align === Classes.Left);
        e.classList.toggle(Classes.Right, align === Classes.Right);
        const maxW = parseInt(formats[4]), maxH = parseInt(formats[5]);
        const iso = /(^|\/)iso_data\//i.test(src);
        if (iso && isoKnown === false) {
            //  No flag service: the value as plain text (its links, ex: country information, need the service too)
            SetNodes(e, "t" + raw, () => [el("span.sw-cell-img-text", { text: raw })], name ? "" : title, flash);
            return;
        }
        //  A link to another site (ex: country information) is marked (External); offline the picture has no link, the address is in the tool tip
        const ext = External(url);
        const linkUrl = ext && ext.Offline ? "" : url;
        if (ext)
            title = F().JoinNonEmpty("\n", title, ext.Title);
        SetNodes(e, "i" + src + "\n" + url + "\n" + maxW + "x" + maxH + (ext ? (ext.Offline ? "\no" : "\ne") : ""), () => {
            const i = Sw.CreateImg({ ShowError: !raw });
            //  Not loadable: show the value as text instead of a broken image
            if (raw)
                i.addEventListener("error", () => {
                    if (i.parentNode)
                        i.replaceWith(el("span.sw-cell-img-text", { text: raw }));
                }, { once: true });
            i.className = "sw-cell-img";
            if (maxW > 0)
                i.style.maxWidth = "calc(var(--ThemeIconSize, 1) * " + maxW + "px)";
            if (maxH > 0)
                i.style.maxHeight = "calc(var(--ThemeIconSize, 1) * " + maxH + "px)";
            //  The text alternative names what the image (link) does, not the mouse action or a raw url ("Click to open "http://.../post.svg"."):
            //  a server title as an action, else "Open <file name>"
            let file = url ? (src.split(/[?#]/)[0].split("/").pop() || "").replace(/\.[a-z0-9]+$/i, "") : "";
            try {
                file = decodeURIComponent(file);
            }
            catch {
            }
            //  A server title naming the row's value late (ex: a country flag: "Show information about the country with the ISO 3166 Alpha 2
            //  country code: SE") starts with the row's own value, so the links of a column don't all sound the same: "SE: Show information .."
            let action = formats[3] != null ? Sw.ActionName(title) : "";
            const at = action && raw && raw.length <= 40 ? action.indexOf(raw) : -1;
            if (at > 20)
                action = _T("{0}: {1}", raw, action, "Accessible name of an image link in a table cell, {0} is the cell value (ex: a country code), {1} is what the link does");
            i.alt = name || (formats[3] != null ? action : (file ? _T("Open {0}", file, "Accessible name of an image link in a table cell that opens the image, {0} is the image file name") : ""));
            let a = null;
            if (iso && isoKnown !== true)
                //  First flag of the page: wait for the probe; without the service the value is shown as plain text (no request)
                isoData().then(ok => {
                    if (ok)
                        i.src = src;
                    else {
                        const o = a || i;
                        if (o.parentNode)
                            o.replaceWith(el("span.sw-cell-img-text", { text: raw }));
                    }
                });
            else
                i.src = src;
            if (!linkUrl)
                return [i];
            if (ext) {
                a = ExternalLink(url, null, ext);
                a.classList.add("sw-link-image");
                a.prepend(i);
                return [a];
            }
            a = CreateLink(url, null, target, null, false, { Class: "sw-link-image" });
            a.append(i);
            return [a];
        }, title, flash);
    }

    /**
     * Renderer "DateTimeLive" / format "Uptime" (classes Mono, Right): the time elapsed since the value (formats[2] template, default "{0}"),
     * kept up to date (shared timer, stops when the cell is removed). "0001-01-01T00:00:00" shows "-"; a falsy value shows "Now".
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @param {boolean} [noCopy] No copy on click. @param {boolean|string} [blankIfNeg] Text for future times. @param {boolean} [invert] Time until the value.
     * @returns {HTMLElement} The cell.
     */
    function DateTimeLive(e, value, formats, nextValue, flash, type, onRefresh, noCopy, blankIfNeg, invert) {
        const fmt = (formats ? formats[2] : null) ?? "{0}";
        if (Reset(e, "DateTimeLive" + fmt))
            e.classList.add(Classes.Mono, Classes.Right);
        state(e).Live = null;
        if (value === "0001-01-01T00:00:00") {
            SetText(e, "-", _TF("Not set", "Tool tip indicating that the value is not set"), flash);
            return e;
        }
        if (!value) {
            SetText(e, _TF("Now", "Text that indicate that an event happened right now"), _TF("Right now", "Tool tip indicating that that an event happened right now"), flash);
            return e;
        }
        const dd = F().ParseDate ? F().ParseDate(value) : (value instanceof Date ? value : new Date(value));
        let title = F().TimeStamp(dd)[0];
        if (!noCopy)
            title += "\n\n" + copyClick(e, value);
        const scale = invert ? -1 : 1;
        /** The current elapsed time text, as the original (Sw.Format.DurationMs: "41 seconds", "16 minutes", "11 days"). */
        const text = () => sf(fmt, F().DurationMs((Date.now() - dd.getTime()) * scale, blankIfNeg));
        SetText(e, text(), title, flash);
        addLive(e, () => {
            const t = text();
            if (e.textContent !== t)
                e.textContent = t;
            const el2 = (Date.now() - dd.getTime()) * scale;
            const minute = 60000;
            //  (as the original: every 100 ms, after 10 minutes at the next minute)
            return el2 > 10 * minute ? Math.ceil(el2 / minute) * minute - el2 + 1000 : 100;
        });
        return e;
    }

    /**
     * Renderer "DateTime" (classes sw-fmt-dt, Mono, Right): the time stamp (formats[0] = "utc" / "local" selects the conversion).
     * "0001-01-01T00:00:00" shows "-"; a falsy value shows the current time, updated every second.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @param {boolean} [onlyDate] Only the date. @param {boolean} [noCopy] No copy on click.
     * @returns {HTMLElement} The cell.
     */
    function DateTime(e, value, formats, nextValue, flash, type, onRefresh, onlyDate, noCopy) {
        const fmt = formats ? formats[0] : null;
        const conv = fmt === "utc" || fmt === "local" ? fmt : null;
        if (Reset(e, "DateTime" + (fmt ?? ""))) {
            e.classList.add(Classes.Mono, Classes.Right, "sw-cell-dt");
            if (conv)
                e.classList.add("sw-cell-dt-" + conv);
        }
        state(e).Live = null;
        if (value === "0001-01-01T00:00:00") {
            SetText(e, "-", _TF("Not set", "Tool tip indicating that the value is not set (is undefined or null)"), flash);
            return e;
        }
        if (!value) {
            const upd = () => {
                const v = F().TimeStamp(new Date(), onlyDate);
                SetText(e, v[1], v[0] + (noCopy ? "" : "\n\n" + copyClick(e, null)), false);
                return 1000 - (Date.now() % 1000) + 5;
            };
            addLive(e, upd);
            return e;
        }
        const v = F().TimeStamp(value, onlyDate, null, null, conv);
        let title = v[0];
        if (!noCopy)
            title += "\n\n" + copyClick(e, value);
        SetText(e, v[1], title, flash);
        return e;
    }

    /**
     * Duration text of table cells: the original ValueFormat.formatTimeSpan (Sw.Format.TimeSpan), one unit: "2 672.1 ms", "559.0 seconds",
     * "78 minutes", "66 hours", "11 days", "7 years"; zero = the zero text (default "0").
     * @param {string|number} value TimeSpan text or seconds.
     * @param {string} [zero] Text of a zero duration.
     * @returns {string} The text.
     */
    function spanText(value, zero) {
        return F().TimeSpan(value, zero);
    }

    /**
     * Renderer "Duration" / TimeSpan (classes Mono, Right): formats[1] text ({0} formatted (spanText), {1} nextValue, {2} raw),
     * formats[2] title (default "Raw: {2}"), formats[3] copy (missing = copy the raw value, "" = no copy, else a template, {3} = text),
     * formats[4] zero text.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement} The cell.
     */
    function TimeSpan(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "TimeSpan"))
            e.classList.add(Classes.Mono, Classes.Right);
        formats = formats || [];
        const c = spanText(value, formats[4]);
        const text = sf(formats[1] ?? "{0}", c, nextValue, value);
        let title = sf(formats[2] ?? "Raw: {2}", c, nextValue, value, text);
        //  The server's "forever" (∞, ex: the 50 year request cache of static files) says so in words above the raw value (S7a-D10: the
        //  tool tip only said "1581120000 s")
        if (c === "∞" || c === "- ∞")
            title = F().JoinNonEmpty("\n", _TF("Forever (never expires)", "Tool tip of a duration table cell whose value is the server's 'forever' (shown as ∞), above the raw value"), title);
        const copy = formats[3];
        if (copy || copy === undefined) {
            let cv = value;
            if (typeof copy === "string")
                cv = sf(copy, c, nextValue, value, text);
            title = F().JoinNonEmpty("\n\n", title, copyClick(e, cv));
        }
        SetText(e, text, title, flash);
        return e;
    }

    /**
     * The title of a cell's text popup (every dialog has one, so a screenshot or a screen reader tells what it shows): the column, then
     * what names the row (its name column, else up to 3 short facts), ex: "Exception · ExchangeRate · Fails.https://... · 06:10:48".
     * @param {HTMLElement} e The cell.
     * @returns {string} The title ("Details" when the cell has no column label and no row).
     */
    function popupTitle(e) {
        const col = (e.dataset && e.dataset.label || "").trim();
        const name = RowName(e);
        const cut = s => s.length > 60 ? s.slice(0, 59) + "…" : s;
        //  bare numbers (a count, an id column) tell nothing in a title: the text and time facts
        const parts = name ? [name.Value] : rowFacts(e, [col], 6).map(f => f.Value).filter(v => !/^[-+]?[\d\s.,%]+$/.test(v)).slice(0, 3);
        const s = [col].concat(parts.map(cut)).filter(x => x).join(" · ");
        return s || _TF("Details", "Title of the dialog that shows the full text of a table cell when the cell has no column name");
    }

    /**
     * Set up a capped text cell with a click (and Enter) popup.
     * @param {HTMLElement} e The cell.
     * @param {string} capText The capped text.
     * @param {boolean} capped True if capped.
     * @param {string} title The tooltip.
     * @param {string|false} flash Flash class.
     * @param {function():void} open Opens the popup.
     */
    function cappedPopup(e, capText, capped, title, flash, open) {
        e.classList.toggle(Classes.Capped, capped);
        SetText(e, capText, title, flash);
        setClick(e, ev => {
            if (Sw.HasSelectionIn(e) || Sw.BadClick(ev))
                return;
            open();
        }, true);
    }

    /**
     * Clear a capped text cell.
     * @param {HTMLElement} e The cell.
     */
    function clearCapped(e) {
        e.removeAttribute("title");
        e.classList.remove(Classes.Capped);
        e.textContent = "";
        setClick(e, null);
    }

    /**
     * Renderer "Json" (class Mono): the text capped at formats[1] chars (min 4) or the first line break (class Capped), title formats[2]
     * (default "{1}" = pretty printed json; {0} raw, {2} capped). Click / Enter opens the highlighted json, formats[3] = copy template (a copy button).
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement|undefined} The cell.
     */
    function Json(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "Json"))
            e.classList.add(Classes.Mono);
        if (!value) {
            clearCapped(e);
            return;
        }
        value = String(value);
        const c = cap(value, formats[1]);
        let pretty = value;
        try {
            pretty = JSON.stringify(JSON.parse(value), null, 4);
        }
        catch {
        }
        const title = sf(formats[2] ?? "{1}", value, pretty, c.Text);
        const copy = formats[3] ? sf(formats[3], value, pretty, c.Text) : null;
        cappedPopup(e, c.Text, c.Capped, title, flash, () => ShowJson(value, copy, popupTitle(e)));
        return e;
    }

    /**
     * Renderer "MD": like Json but the popup shows the rendered markdown (title formats[2], default "{0}" = the full text; {1} capped).
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement|undefined} The cell.
     */
    function MarkDown(e, value, formats, nextValue, flash, type, onRefresh) {
        Reset(e, "MarkDown");
        if (!value) {
            clearCapped(e);
            return;
        }
        value = String(value);
        const c = cap(value, formats[1]);
        const title = sf(formats[2] ?? "{0}", value, c.Text);
        const copy = formats[3] ? sf(formats[3], value, c.Text) : null;
        cappedPopup(e, c.Text, c.Capped, title, flash, () => ShowMarkdown(value, copy, popupTitle(e)));
        return e;
    }

    /**
     * Renderer "Text": like MD but the popup shows the plain text (monospace when formats[4] is "True").
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement|undefined} The cell.
     */
    function LongText(e, value, formats, nextValue, flash, type, onRefresh) {
        const mono = String(formats[4] ?? "").toLowerCase() === "true";
        if (Reset(e, "LongText"))
            e.classList.toggle(Classes.Mono, mono);
        if (!value) {
            clearCapped(e);
            return;
        }
        value = String(value);
        const c = cap(value, formats[1]);
        const title = sf(formats[2] ?? "{0}", value, c.Text);
        const copy = formats[3] ? sf(formats[3], value, c.Text) : null;
        cappedPopup(e, c.Text, c.Capped, title, flash, () => ShowText(value, mono, copy, popupTitle(e)));
        return e;
    }

    /**
     * Renderer "Boolean" (read only; the Toggle format is the editable one), as the original: "☑ True" (class Text) or "☐ False" (class
     * False; also a null value); the raw value is in the tool tip, copy on click.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement} The cell.
     */
    function BooleanCell(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "Boolean"))
            e.classList.add("sw-cell-bool");
        const on = !!value;
        const text = on
            ? _TF('☑ True', 'A boolean true indicator, displayed when something is true')
            : _TF('☐ False', 'A boolean false indicator, displayed when something is false');
        const title = F().JoinNonEmpty("\n\n", value === null || value === undefined ? "" : String(value), copyClick(e, value));
        SetText(e, text, title, flash);
        e.classList.toggle(Classes.Text, on);
        e.classList.toggle(Classes.False, !on);
        return e;
    }

    /**
     * Renderer of the C# type SysWeaver.MessageLevels (the Level column of log tables; the server sends the enum number): the number, as
     * the original (Default renderer), with the level name ("Debug", "Info", "Warning", "Error"; "All" for 0) in the tool tip; copy on
     * click copies the number. Warnings / errors get the classes sw-level-warning / sw-level-error.
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement} The cell.
     */
    function MessageLevelCell(e, value, formats, nextValue, flash, type, onRefresh) {
        Reset(e, "MessageLevel");
        const names = [
            _TF("All", "Name of the log message level 0 (a filter threshold that accepts all messages)"),
            _TF("Debug", "Name of the log message level Debug (detailed diagnostic messages)"),
            _TF("Info", "Name of the log message level Info (normal operation)"),
            _TF("Warning", "Name of the log message level Warning"),
            _TF("Error", "Name of the log message level Error"),
        ];
        const n = typeof value === "number" ? value : parseInt(value);
        const name = (Number.isInteger(n) && n >= 0 && n < names.length) ? names[n] : (value === null || value === undefined ? "" : String(value));
        const text = value === null || value === undefined ? "" : String(value);
        const title = F().JoinNonEmpty("\n\n", name !== text ? name : "", copyClick(e, value));
        SetText(e, text, title, flash);
        e.classList.toggle("sw-level-warning", n === 3);
        e.classList.toggle("sw-level-error", n === 4);
        return e;
    }

    /**
     * Renderer "ByteSize" / "ByteSpeed" (classes Right, Mono): text formats[2] ({0} formatted, {1} nextValue, {2} raw), title formats[3]
     * (default "Raw: {2}"), copy on click.
     * @param {boolean} speed True for ByteSpeed.
     * @returns {function} The renderer.
     */
    function bytesRenderer(speed) {
        return function (e, value, formats, nextValue, flash, type, onRefresh) {
            if (Reset(e, speed ? "ByteSpeed" : "ByteSize"))
                e.classList.add(Classes.Right, Classes.Mono);
            formats = formats || [];
            //  A null value is unknown (ex: the memory of a process that can't be read), not "0 bytes"
            if (value === null || value === undefined) {
                removeCopy(e);
                SetText(e, "", _TF("Unknown", "Tool tip of an empty size cell in a table: the value is not known (ex: the memory use of a process that cannot be read)"), flash);
                return e;
            }
            const vs = speed ? F().ByteSpeed(+value || 0) : F().Bytes(+value || 0);
            const text = sf(formats[2] ?? "{0}", vs, nextValue, value);
            let title = sf(formats[3] ?? "Raw: {2}", vs, nextValue, value, text);
            title = F().JoinNonEmpty("\n\n", title, copyClick(e, value));
            SetText(e, text, title, flash);
            return e;
        };
    }

    /**
     * The special tag of an auth requirement (C# TableDataAuth, the api inspector, the explore pages): null = open to anyone (the accent
     * "Open" tag, so open end points stand out), "" = any signed in user (the "Any logged in user" tag); null for a token list.
     * @param {string|null|undefined} auth The requirement: null = open, "" = any signed in user, else comma separated tokens.
     * @returns {HTMLElement|null} The tag (span.sw-tag.sw-tag-open / .sw-tag-signedin with its tool tip), null for tokens.
     */
    function AuthTag(auth) {
        if (auth === null || auth === undefined)
            return el("span.sw-tag.sw-tag-open", {
                text: _TF("Open", "Text of the tag in an auth column of an api or web resource that anyone can use, without signing in"),
                title: _TF("Open: anyone can use this, no sign in required.", "Tool tip of the Open tag in an auth column of an api or web resource that anyone can use"),
            });
        if (String(auth).trim().length === 0)
            return el("span.sw-tag.sw-tag-signedin", {
                text: _TF("Any logged in user", "Text of the tag in an auth column: the api or web resource needs a signed in user, but no special role"),
                title: _TF("Any signed in user, no special role required.", "Tool tip of the Any logged in user tag in an auth column"),
            });
        return null;
    }

    /**
     * Renderer "Tags": a comma separated list ('¤' inside a tag stands for ','; an empty string shows the tag "🔑": a signed in user without
     * a special token, as the original); each tag "key:value" becomes a span
     * with text formats[1] (default "{1}" = key) and title formats[2] (default "{2}" = value); args {0} tag, {1} key, {2} value, {3} text, {4} title.
     * formats[3] = per tag copy template; formats[4] "true" = clicking the cell (outside a tag) copies the raw value.
     * formats[5] "Auth" = an auth requirement (C# TableDataAuth): null shows the accent "Open" tag, "" the "Any logged in user" tag (AuthTag).
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement|undefined} The cell.
     */
    function Tags(e, value, formats, nextValue, flash, type, onRefresh) {
        Reset(e, "Tags");
        const auth = String(formats[5] ?? "").toLowerCase() === "auth";
        if (auth && (value === null || value === undefined || String(value).trim().length === 0)) {
            const open = value === null || value === undefined;
            SetNodes(e, "a" + (open ? "o" : "s"), () => [AuthTag(open ? null : "")], "", flash);
            removeCopy(e);
            return e;
        }
        if (value === null || value === undefined) {
            e.replaceChildren();
            state(e).Sig = null;
            return;
        }
        value = String(value);
        const ft = formats[1] ?? "{1}", fti = formats[2] ?? "{2}", fc = formats[3];
        const all = String(formats[4] ?? "").toLowerCase() === "true";
        //  An empty list (a signed in user without a special token): the tag "🔑", as the original
        const tags = value.length === 0 ? ["🔑"] : value.split(",");
        //  (the key alone says nothing: its tool tip says what it means)
        const keyTitle = value.length === 0 ? _TF("Any signed in user", "Tool tip of the key tag in an auth column: the api needs a signed in user, but no special role") : "";
        SetNodes(e, "t" + value + "\n" + formats.join(";"), () => tags.map(tag0 => {
            const tag = tag0.trim().split("¤").join(",");
            const [k, v] = F().KeyValueSplit(tag, ":");
            const text = sf(ft, tag, k, v);
            let title = sf(fti, tag, k, v, text);
            const s = el("span.sw-tag", { text });
            if (fc) {
                const cp = sf(fc, tag, k, v, text, title);
                title = F().JoinNonEmpty("\n\n", title, copyClick(s, cp));
                s.addEventListener("click", ev => ev.stopPropagation());
            }
            title = F().JoinNonEmpty("\n\n", keyTitle, title);
            if (title)
                s.title = title;
            return s;
        }), null, flash);
        if (all)
            e.title = copyClick(e, value);
        else
            removeCopy(e);
        return e;
    }

    /**
     * Renderer "PerRowFormat": nextValue is "TypeName|Format|NextValue" (the type's first comma part is used), rendered with Update;
     * without nextValue the value is rendered as a string (Default).
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement|undefined} The cell.
     */
    function PerRowFormat(e, value, formats, nextValue, flash, type, onRefresh) {
        if (!nextValue) {
            Default(e, value, formats, nextValue, flash, "System.String");
            return;
        }
        const s = String(nextValue).split("|");
        Update(e, s[0].split(",")[0], value, flash, s[1], s[2], onRefresh);
        return e;
    }

    /**
     * A reason as one sentence for people: capitalised, ending with a period ("process #65596 has already ended" → "Process #65596
     * has already ended.").
     * @param {*} s The text.
     * @returns {string} The sentence ("" for none).
     */
    function sentence(s) {
        s = String(s ?? "").trim();
        if (!s)
            return "";
        s = s.charAt(0).toLocaleUpperCase() + s.slice(1);
        return /[.!?…。！？]$/.test(s) ? s : s + ".";
    }

    /**
     * The server's explicit outcome kind of a refused request (X-Sw-Outcome: Sw.Net.OutcomeKind, else the error's Outcome): "AlreadyDone"
     * (the goal is met already), "Gone" (the item is no longer there), "Refused", "Denied", "Invalid", or "" (a failure / no kind).
     * Never guessed from the status code: a 404 / 409 / 410 without the server's kind is "not done".
     * @param {*} err The error.
     * @returns {string} The kind.
     */
    function outcomeKind(err) {
        if (!err || typeof err !== "object")
            return "";
        let k = "";
        try {
            k = Sw.Net && typeof Sw.Net.OutcomeKind === "function" ? Sw.Net.OutcomeKind(err) : err.Outcome;
        }
        catch {
            k = err.Outcome;
        }
        k = String(k || "").trim();
        if (/^already(done)?$/i.test(k))
            return "AlreadyDone";
        const known = ["Gone", "Refused", "Denied", "Invalid"].find(x => x.toLowerCase() === k.toLowerCase());
        return known || "";
    }

    /**
     * Join "What: Why. Do." the one way (the core's composer Sw.Net.Compose: sentence boundaries, never two pieces of advice).
     * @param {string} what What happened ("Kill: not done."). @param {string} why The reason. @param {string} [todo] The advice.
     * @returns {string} The text.
     */
    function compose(what, why, todo) {
        if (Sw.Net && typeof Sw.Net.Compose === "function") {
            try {
                return String(Sw.Net.Compose(what, why, todo || ""));
            }
            catch {
                //  (the local join)
            }
        }
        return [what, sentence(why), sentence(todo)].filter(Boolean).join(" ");
    }

    /**
     * Run a cell action request (Toggle / Actions): GET the url (reload), a failure is shown as a fail toast; then onRefresh.
     * @param {HTMLElement} b The clicked element (busy while running, Sw.Work.Busy).
     * @param {string} url The api url.
     * @param {function} onRefresh Refresh callback.
     * @returns {Promise<void>}
     */
    async function runAction(b, url, onRefresh) {
        //  The one busy state (Sw.Work, the same as Sw.Button; FBK-2 / 3 / 4): label and colour kept, aria-busy, cursor progress, a
        //  spinner after 250 ms in the icon's place, "Working…" announced, after 2 s the elapsed time in the tool tip and, when the shell
        //  has the activity list, an item there (running, then done / failed; leaving the page asks first)
        const name = (b.getAttribute("aria-label") || b.textContent || "").trim();
        //  The one name of the action (Ops N3): the confirmation's title ("Kill msedge (63564)"), else the action and the row's name
        const rowName = b.dataset.swRowName || "";
        const item = ItemName(b);
        const work = b.SwActionTitle || (rowName ? _T("{0} ({1})", name, rowName, "Title of a table row action in the activity list. {0} is the action, ex: 'Kill', {1} the row's name, ex: 'msedge'")
            : (item ? _T("{0} ({1})", name, item, "Title of a table row action in the activity list when the row has no name. {0} is the action, ex: 'Kill', {1} the row's key values, ex: 'Name: msedge, Id: 63564'") : name));
        b.SwActionTitle = null;
        //  (a row action button that is hidden in its row, ex: kept at width 0 after an earlier "done": its busy state and the focus
        //  have no place, the focus falls elsewhere (Ops R4-7, R3-12); the table should show it again)
        if (b.isConnected && b.closest("[data-sw-item], tr") && !b.getClientRects().length && !b.closest("[role=menu]"))
            Sw.DevWarn('The row action "' + name + '" ran from a button that is hidden in its row (no box): its busy state can\'t be seen and the focus has no place after it. Show the action again after a refresh (UX guide FBK-2, FOC-1).', "cell-action-hidden:" + name);
        const start = Date.now();
        const sc = Sw.Work.Scope(() => Sw.Net.Get(url, { Reload: true }));
        const p = sc.Threw ? Promise.reject(sc.Error) : Sw.Work.Promise(sc);
        const A = Sw.Activity;
        const useList = !!(A && A.Start && A.Supported !== false);
        let job = null;
        const later = useList ? setTimeout(() => {
            job = A.Start({ Key: "sw.cell:" + url, Title: work });
        }, 2000) : 0;
        //  The focus stays in the table through the refresh and a row that is marked done, also for "not done" (FOC-1, A3-9: after a
        //  confirmation the dialog gives the focus back later, so the place is the button itself, not where the focus is now)
        const host = (b.SwActHost && b.SwActHost.isConnected ? b.SwActHost : null) || b.closest("table, [role=grid], .sw-table, .sw-cards");
        const rowEl = b.closest("[data-sw-item]");
        const rowKey = b.SwActKey || (rowEl ? rowEl.getAttribute("data-sw-item") : "");
        b.SwActHost = null;
        b.SwActKey = null;
        const keep = Sw.Focus && Sw.Focus.Keep ? Sw.Focus.Keep(host || b.parentElement || b, null, { From: b }) : null;
        const said = Sw.Work.Watch();
        //  class sw-busy too: explore/table.js holds the row order while a row action runs
        b.classList.add("sw-busy");
        const res = await Sw.Work.Busy(b, p, { Name: name, Leave: !useList, Kind: "server", Work: work });
        b.classList.remove("sw-busy");
        clearTimeout(later);
        //  The server's outcome (FBK-5, TXT-30: "done" only when the server said so): a failed request, or a false answer, is not done
        const out = Sw.Work.Outcome(sc, res);
        let why = "";
        if (out.Error) {
            //  Logged as the original cell action did (console.log, not a warning: the row shows the refusal; S5-A-14)
            console.log('Request "' + url + '" failed!, exception: ' + out.Error);
            const m = out.Error && out.Error.message ? out.Error.message : String(out.Error);
            //  (the error object: its status and the server's reason, A3-2 / N10)
            why = Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(out.Error)
                ? _TF("The connection to the service was lost.", "Reason after a table row action failed because the service could not be reached")
                : (Sw.Net.ErrorText ? (Sw.Net.ErrorText(typeof out.Error === "object" ? out.Error : m).Text || m) : m);
        }
        else if (out.NotDone)
            why = _TF("The service did not do it (it may have been done already, or it is not possible now).", "Reason after a table row action that the service answered with 'no' (false)");
        //  The result in one place (FBK-2): the table shows it in the row when the row is on the screen (it sets InPlace), else a
        //  toast / the activity result
        //  The row went away while it was asked about or ran (a refresh, another admin; Admin N16): the result can't be shown in it
        let gone = false;
        if (!b.isConnected) {
            const again = host && host.isConnected && rowKey ? Array.from(host.querySelectorAll("[data-sw-item]")).find(x => x.getAttribute("data-sw-item") === rowKey) : null;
            gone = !again;
        }
        const status = out.Error && typeof out.Error === "object" ? out.Error.Status : undefined;
        //  The reason as one sentence for people (FBK-1 "rejected and why", TXT-25): capitalised, ending with a period, the advice once
        const reason = sentence(why);
        //  The outcome comes from the server's explicit kind (X-Sw-Outcome: Sw.Net.OutcomeKind), never from the status code (a
        //  refused Kill / Withdraw must never read "not needed"): AlreadyDone = the goal is met already ("not needed"), Gone = the item
        //  is no longer there; both make the row final (its actions go, like a done final action). Anything else is "not done".
        const kind = out.Ok || out.Aborted ? "" : outcomeKind(out.Error);
        const final = kind === "AlreadyDone" || kind === "Gone";
        const neutral = final;
        //  The short text the row shows (Admin N26 / Ops R4-5: visible, never only a tool tip; wraps on a phone): "Kill: not done. <reason>"
        const verb = (n, ctx) => kind === "AlreadyDone"
            ? _T("{0}: not needed.", n, "Start of the result of a table row action that the server says was done already (its goal is met). {0} is the action" + ctx + "; the reason follows")
            : (kind === "Gone"
                ? _T("{0}: no longer there.", n, "Start of the result of a table row action whose item the server says is gone. {0} is the action" + ctx + "; the reason follows")
                : _T("{0}: not done.", n, "Start of the result of a table row action that failed or was refused. {0} is the action" + ctx + "; the reason follows"));
        const note = out.Ok || out.Aborted ? "" : compose(verb(name, ", ex: 'Kill'"), reason);
        const detail = {
            Ok: out.Ok, Aborted: out.Aborted, Value: out.Value, Error: out.Ok ? null : (why || null), Text: name, Name: rowName, Work: work, Url: url,
            Ms: Date.now() - start, InPlace: false, Gone: gone, Status: status, Reason: reason, Note: note, Neutral: neutral,
            Outcome: kind, Final: final,
        };
        b.dispatchEvent(new CustomEvent("sw-cell-result", { bubbles: true, detail }));
        const inPlace = !!detail.InPlace;
        //  (one verdict: the row note, the tool tip, the activity item and the toast say the same)
        const failText = compose(verb(work, " and its row, ex: 'Kill msedge (63564)'"), reason);
        if (job) {
            if (final) {
                //  the goal is met / the item is gone: not a failure, the same words as the row
                job.Quiet = inPlace;
                if (job.Done)
                    job.Done({ Quiet: inPlace, Text: failText });
            }
            else if (out.Ok || out.Aborted) {
                job.Quiet = inPlace;
                if (out.Aborted && job.Cancelled)
                    job.Cancelled();
                else if (job.Done)
                    job.Done({ Quiet: inPlace });
            }
            else {
                job.Quiet = inPlace;
                if (job.Fail)
                    job.Fail(reason || failText);
            }
        }
        else if (!out.Ok && !out.Aborted && !inPlace && !said.Stop()) {
            //  A lost connection while the shell says it is offline: its calm line says it (FBK-9)
            if (out.Error && Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(out.Error) && Sw.Net.Online === false) {
                //  (said by the shell)
            }
            //  The server says the goal is met already / the item is gone: information, not an error (Admin N16), in its own words
            else if (final && Sw.Toast)
                Sw.Toast(failText, { Kind: "info" });
            else
                Sw.Fail(failText);
        }
        //  Done, but the row is gone (no place for the result): one short confirmation (Admin N16: the real outcome, never an error)
        else if (out.Ok && gone && !inPlace && !said.Stop() && Sw.Toast)
            Sw.Toast(_T("{0}: done", work, "Result of a table row action whose row was no longer shown. {0} names the action and its row, ex: 'Withdraw invitation (anna)'"), { Kind: "ok" });
        said.Stop();
        onRefresh && onRefresh(detail);
        //  The focus back in the row (or its neighbour) after the result and the refresh that may rebuild it (A3-9: also for "not
        //  done", and after a confirmation whose dialog gave the focus back late); only while it is lost (on the body)
        if (keep)
            for (const ms of [200, 800, 2000])
                setTimeout(() => {
                    keep.Restore();
                    if (ms === 2000)
                        keep.Stop();
                }, ms);
        return detail;
    }

    /**
     * Renderer "Toggle" (class Actions): value "bool[,arg2,...]" ("true" / "1" = checked); template args [bool, nextValue, ...rest].
     * Shows a check box (span.sw-cell-check, the original's ☑ / ☐) in a pill (filled when checked) and formats[1] (default "True") or formats[2] (default "False"),
     * tooltip formats[3] / formats[4]; clicking sends a GET of formats[5] (formatted with the args) and then calls onRefresh(). The request
     * is kept in data-sw-toggle-api: an Actions cell of the same row leaves out an action with the same request (one control per setting).
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement} The cell.
     */
    function Toggle(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "Toggle"))
            e.classList.add(Classes.Actions);
        const args = String(value).split(",");
        const v0 = args[0].toLowerCase();
        const on = v0 === "true" || v0 === "1";
        args[0] = on;
        args.splice(1, 0, nextValue);
        const text = on ? sfa(formats[1] ?? "True", args) : sfa(formats[2] ?? "False", args);
        const title = on
            ? sfa(formats[3] ?? _TF("Uncheck", "Tool tip to indicate that something can be unchecked (unselected) by clicking on the element)"), args)
            : sfa(formats[4] ?? _TF("Check", "Tool tip to indicate that something can be checked (selected) by clicking on the element)"), args);
        const api = sfa(formats[5] ?? "", args);
        //  The row's Actions cell leaves out an action with the same request (ex: Performance systems "Toggle" next to this switch)
        if (api)
            e.dataset.swToggleApi = api;
        else
            delete e.dataset.swToggleApi;
        SetNodes(e, "g" + on + "\n" + text + "\n" + title + "\n" + api, () => {
            //  The original's look (owner report 4 #8): a check box in a pill, filled when checked, so the state reads at a glance even
            //  when both texts are the same ("Enabled")
            const b = el("button.sw-cell-toggle", { type: "button", title, attrs: { role: "checkbox", "aria-checked": on ? "true" : "false" } },
                el("span.sw-cell-check", { attrs: { "aria-hidden": "true" } }), el("span", { text }));
            let busy = false;
            b.addEventListener("click", async ev => {
                if (Sw.BadClick(ev) || busy || !api)
                    return;
                busy = true;
                await runAction(b, api, onRefresh);
                busy = false;
            });
            return [b];
        }, "", flash);
        return e;
    }

    /**
     * Words that make a cell action destructive (asked before it runs, see IsDestructiveAction): kill, delete, remove, stop, uninstall,
     * reboot, restart, shut down, terminate, erase, purge, wipe, drop, destroy, reset (ex: the counters of a performance system).
     * @type {RegExp}
     */
    const DestructiveWords = /\b(kill|delete|remove|stop|uninstall|reboot|restart|shut\s*down|shutdown|terminate|erase|purge|wipe|drop|destroy|reset)\b/i;

    /**
     * True if a cell action (Actions renderer) changes or removes something that can't be brought back, so it must be confirmed: its text,
     * its title (tool tip) or the method name of its link (the last path segment, "KillProcess" -> "kill process") has a destructive word
     * (DestructiveWords), or the server title warns ("Warning", "can not be undone"). Only used when the server says nothing about a
     * confirmation (an action without the optional "confirm" part, see Actions / C# TableDataActionsExAttribute), then the words decide.
     * Exposed as Sw.Cells.IsDestructiveAction.
     * @param {{Text: string, Title: string, Link: string}} d The action.
     * @returns {boolean} True to ask first.
     */
    function IsDestructiveAction(d) {
        const title = String(d.Title || "");
        if (/warning|can\s*not\s+be\s+undone|cannot\s+be\s+undone/i.test(title))
            return true;
        const path = String(d.Link || "").replace(/^[&@]/, "").split(/[?#]/)[0];
        const method = (path.split("/").pop() || "").replace(/\.[a-z0-9]+$/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2");
        return DestructiveWords.test(String(d.Text || "")) || DestructiveWords.test(Sw.ActionName(title)) || DestructiveWords.test(method);
    }

    /**
     * The item a cell belongs to, for confirmations ("Name: msedge, Id: 40544"): the data-sw-item attribute of the closest element that has
     * one (explore/table.js sets it on each row from the key columns), else the first cell of the row with a short text that is not only a
     * number (with its data-label), else "".
     * @param {HTMLElement} e The cell.
     * @returns {string} The item description.
     */
    function ItemName(e) {
        const host = e.closest && e.closest("[data-sw-item]");
        if (host && host.dataset.swItem)
            return host.dataset.swItem;
        const tr = e.closest && e.closest("tr");
        if (!tr)
            return "";
        for (const c of tr.cells) {
            if (c === e || c.classList.contains("sw-table-rn"))
                continue;
            const s = c.textContent.trim();
            if (s && s.length <= 80 && !/^[\d\s.,:%+-]*$/.test(s))
                return (c.dataset.label ? c.dataset.label + ": " : "") + s;
        }
        return "";
    }

    /** @type {RegExp} Column labels that name a row for people (explore/table.js picks its card title column the same way). */
    const nameLabels = /^(display ?name|name|full ?name|title|user ?name|user|login|account|e-?mail|subject|host ?name|host|machine ?name|computer ?name|file ?name|label|caption)$/i;

    /**
     * The name of the row a cell belongs to, for people (ex: the user of a session, not its token): the data-sw-name attribute
     * (+ data-sw-name-label) of the closest element that has one (a table may set it from its title column), else the text of the
     * first other cell of the row whose data-label names an identity (Name, User, E-mail, Host, File name ...), else null.
     * Exposed as Sw.Cells.RowName.
     * @param {HTMLElement} e The cell.
     * @returns {{Label: string, Value: string}|null} The name and the label of its column, null = none.
     */
    function RowName(e) {
        const host = e.closest && e.closest("[data-sw-name]");
        if (host && host.dataset.swName)
            return { Label: host.dataset.swNameLabel || "", Value: host.dataset.swName };
        const tr = e.closest && e.closest("tr");
        if (!tr)
            return null;
        for (const c of tr.cells) {
            const l = (c.dataset.label || "").trim();
            if (c === e || c.classList.contains("sw-table-rn") || !l || !nameLabels.test(l))
                continue;
            const s = c.textContent.trim();
            if (s && s.length <= 80)
                return { Label: l, Value: s };
        }
        return null;
    }

    /**
     * Up to max short facts of the row a cell belongs to (visible cells with a data-label and a one line text; not the action cells
     * and not the row number), so a confirmation tells similar rows apart (ex: the address, agent and start of a session).
     * @param {HTMLElement} e The cell.
     * @param {string[]} skip Labels that are shown already.
     * @param {number} [max] The most facts (default 4).
     * @param {string[]} [skipValues] Values that are shown already (the name).
     * @returns {Array<{Label: string, Value: string}>} The facts.
     */
    function rowFacts(e, skip, max, skipValues) {
        skipValues = skipValues || [];
        const tr = e.closest && e.closest("tr");
        const r = [];
        if (!tr)
            return r;
        for (const c of tr.cells) {
            if (r.length >= (max || 4))
                break;
            const l = (c.dataset.label || "").trim();
            if (c === e || !l || c.classList.contains("sw-table-rn") || c.querySelector("button") || skip.indexOf(l) >= 0 || !c.getClientRects().length)
                continue;
            const s = c.textContent.replace(/\s+/g, " ").trim();
            //  not a repeat of the name (Auth "ops" next to User "ops") and not a bare yes / no flag, which tells no rows apart
            if (s && s !== "-" && s.length <= 90 && skipValues.indexOf(s) < 0 && !/^(yes|no|true|false|on|off|✓|✗)$/i.test(s))
                r.push({ Label: l, Value: s });
        }
        return r;
    }

    /**
     * Ask before a destructive cell action: a dialog that names the action and the item, with "Cancel" focused (Enter on it and Escape
     * cancel; no Enter shortcut for the action) and the action as a danger button. The question is the server's confirmation text
     * (d.Confirm, ex: "Kill the process?") if any, else made from the action's title or text.
     * @param {HTMLElement} cell The cell.
     * @param {{Text: string, Title: string, Confirm: (string|boolean|undefined)}} d The action.
     * @param {HTMLElement} [btn] The action's button (gets SwActionTitle: the confirmation's title, the action's one name).
     * @returns {Promise<boolean>} True to run it.
     */
    async function confirmAction(cell, d, btn) {
        //  The item names the row by its key values, which are raw: a server time ("2026-10-20T03:01:10Z") is shown in the viewer's
        //  time like the cells (the time rule, core/README "Times")
        const p2 = n => String(n).padStart(2, "0");
        let item = ItemName(cell).replace(/\b\d{4}-\d\d-\d\dT\d\d:\d\d(:\d\d(\.\d+)?)?(Z|[+-]\d\d:?\d\d)\b/g, s => {
            const t = new Date(s);
            return isNaN(t) ? s : t.getFullYear() + "-" + p2(t.getMonth() + 1) + "-" + p2(t.getDate()) + " " + p2(t.getHours()) + ":" + p2(t.getMinutes()) + ":" + p2(t.getSeconds());
        });
        const q = typeof d.Confirm === "string" && d.Confirm ? d.Confirm : "";
        const verb = d.Text || Sw.ActionName(d.Title);
        //  A server confirmation of two parts, "question\nexplanation" (C# TableDataActionsExAttribute): the question is the title as
        //  given, the explanation the text, and the server's explanation replaces the default "This cannot be undone." (it says
        //  itself what can or can not be undone); the row is named by its name and its short facts (formatted like the cells)
        const nl = q.indexOf("\n");
        if (nl > 0 && Sw.Dialog) {
            const name2 = RowName(cell);
            return ConfirmDanger({
                Title: q.substring(0, nl).trim(),
                Text: q.substring(nl + 1).trim() || undefined,
                Facts: rowFacts(cell, name2 ? [name2.Label] : [], 4, name2 ? [name2.Value] : []),
                Warning: "",
                OkText: d.Text,
                OkIcon: d.Image || null,
                Row: btn,
            });
        }
        //  The row is named for people: by its identity column (the user of a session), not by an opaque key (its token). The key
        //  stays in the item line, and a few short facts of the row (address, agent, start) tell similar rows apart.
        const name = RowName(cell);
        const named = !!(name && name.Value && item.indexOf(name.Value) < 0);
        if (named)
            item = (name.Label ? name.Label + ": " : "") + name.Value + (item ? ", " + item : "");
        //  with a known row the title names the action and its target ("Kill devenv (33936)?", "End session for ops?",
        //  Sw.Dialog.DangerTitle); a short server question ("Kill the process?") then says nothing more and is left out, a longer
        //  server text is kept
        if (item && verb && Sw.Dialog && Sw.Dialog.DangerTitle) {
            const title = named ? Sw.Dialog.DangerTitle(verb, name.Value, { Name: true }) : Sw.Dialog.DangerTitle(verb, item);
            //  The one name of the action (Ops N3): the job, the toast and the leave question reuse the title ("Kill msedge (63564)")
            if (btn)
                btn.SwActionTitle = String(title).replace(/\s*\?\s*$/, "");
            return ConfirmDanger({
                Title: title,
                Item: item,
                Facts: named ? rowFacts(cell, [name.Label], 4, [name.Value]) : undefined,
                Text: q && !(q.length <= 60 && /\?\s*$/.test(q)) ? q : undefined,
                OkText: d.Text,
                OkIcon: d.Image || null,
                Row: btn,
            });
        }
        return ConfirmDanger({
            Title: q || (Sw.ActionName(d.Title) || d.Text || _TF("Run this action", "Fallback title of the confirmation dialog of a table cell action without a text")) + "?",
            Item: item,
            OkText: d.Text,
            OkIcon: d.Image || null,
            Row: btn,
        });
    }

    /** The options of ConfirmDanger / Sw.Danger.Run (Sw.DevOptions: unknown, wrong type, colliding). */
    const DangerSchema = {
        Title: "string", Item: "string|node", ItemLabel: "string", Text: "string|node", Warning: "string|node", Confirm: "string",
        Facts: "array", OkText: "string", OkIcon: "string", OkTitle: "string", CancelTitle: "string", Action: "string", Target: "string",
        Prepare: "function", Button: "element|component", Row: "element",
    };
    const DangerRunSchema = Object.assign({}, DangerSchema, { Consequence: "string|node", Run: "function", Done: "boolean|string", Focus: "element", Kind: "string" });
    //  (the one registry of option schemas: Sw.Work.RegisterOptions)
    if (Sw.Work && typeof Sw.Work.RegisterOptions === "function") {
        Sw.Work.RegisterOptions("Sw.Cells.ConfirmDanger", DangerSchema);
        Sw.Work.RegisterOptions("Sw.Danger.Confirm", DangerSchema);
        Sw.Work.RegisterOptions("Sw.Danger.Run", DangerRunSchema);
        Sw.Work.RegisterOptions("Sw.Cells.CreateLink", { UseEdit: "boolean", UseView: "boolean", Class: "string" });
    }
    delete DangerRunSchema.Row;

    /**
     * The one confirmation of destructive actions (table cell actions like Kill, the text editor's Delete / Reload, ...; exposed as
     * Sw.Cells.ConfirmDanger), so they all look and read the same: a warning icon and a question as the title ("Kill the process?",
     * "Delete the file?"), the item ("Item: Name: msedge, Id: 40544" / "File: LogFile.txt"), an optional text, the warning ("This cannot
     * be undone." unless replaced), and the buttons "Cancel" (focused: Enter on it and Escape cancel) and the action as a danger button
     * (the verb: "Kill", "Delete"). An alertdialog; the item and texts are its accessible description.
     * @param {object} o Options: { Title (the question), Item (the item text), ItemLabel (default "Item:"), Text (extra text), Warning
     * (replaces "This cannot be undone."; "" = none), Confirm (a text to type, ex: the account name: an input "Type <x> to confirm" gets the focus and the
     * danger button stays disabled until the text matches; Enter confirms then), Facts ([{Label, Value}]: short facts of the row under the item, ex: Address, User agent), OkText (default "OK"), OkIcon, OkTitle, CancelTitle, Action + Target (
     * with no Title the title is Sw.Dialog.DangerTitle(Action, Target ?? Item), ex: "Kill devenv (33936)?", and Action is the default
     * OkText), Prepare(signal) (the details the confirmation needs from the server first, ex: a one time delete token with facts:
     * returns an object whose keys Title, Item, ItemLabel, Text, Warning, Facts, Confirm, OkText override the options; it is fetched
     * outside the click's work, with the busy state on Button at once, or, when Button is hidden (in a "More actions" menu) or not
     * given, a small working dialog with Cancel after 300 ms; when it fails, throws or returns nothing the reason is shown and no
     * confirmation opens: a destructive action never falls back to a weaker question), Button (the Sw.Button / element that started
     * it), Row (a table row action's button: when the table no longer lists its row while the question is open, data-sw-row-gone /
     * the "sw-row-gone" event, the question says so, the action is off, Cancel becomes Close and the result is false) }.
     * @returns {Promise<boolean|object>} True if confirmed (with Prepare: the prepared object, truthy), false otherwise.
     */
    async function ConfirmDanger(o) {
        if (o && !o.SwPrepared && Sw.Work && typeof Sw.Work.CheckOptions === "function")
            Sw.Work.CheckOptions("Sw.Cells.ConfirmDanger", o, DangerSchema);
        if (o && typeof o.Prepare === "function") {
            const prep = await prepareDanger(o);
            if (!prep)
                return false;
            const ov = {};
            for (const k of ["Title", "Item", "ItemLabel", "Text", "Warning", "Facts", "Confirm", "OkText"])
                if (prep[k] !== undefined)
                    ov[k] = prep[k];
            const ok = await ConfirmDanger(Object.assign({}, o, ov, { Prepare: undefined }));
            return ok ? prep : false;
        }
        const D = Sw.Dialog;
        if (!o.Title && o.Action && D && D.DangerTitle)
            o = Object.assign({}, o, { Title: D.DangerTitle(o.Action, o.Target ?? o.Item), OkText: o.OkText || o.Action });
        if (!D || !D.Show)
            return window.confirm(o.Title || "?");
        const warning = o.Warning ?? _TF("This cannot be undone.", "Warning in the confirmation dialog of a destructive action (ex: Kill a process, Delete a file)");
        //  The item line only when the title doesn't name it already ("Delete the account anna?" + "Item: anna" says it twice; "Kill
        //  msedge (63564)?" + "Item: Name: msedge, Id: 63564" too: every value of the item is in the title)
        const itemVals = o.Item ? String(o.Item).split(/,\s*/).map(s => s.replace(/^[^:]{1,40}:\s*/, "").trim()).filter(x => x) : [];
        const titleS = String(o.Title || "");
        const showItem = !!o.Item && !(titleS.indexOf(String(o.Item)) >= 0 || (itemVals.length && itemVals.every(x => titleS.indexOf(x) >= 0)));
        const content = el("div.sw-cell-confirm", null,
            showItem ? el("p.sw-cell-confirm-item", null,
                el("span", { text: (o.ItemLabel || _TF("Item:", "Label before the name of the row (item) in the confirmation dialog of a table cell action (ex: Item: Name: msedge, Id: 40544)")) + " " }),
                el("strong", { text: o.Item })) : null,
            o.Facts && o.Facts.length ? el("dl.sw-cell-confirm-facts", null,
                o.Facts.map(f => [el("dt", { text: f.Label }), el("dd", { text: f.Value, title: f.Value })])) : null,
            o.Text ? el("p", { text: o.Text }) : null,
            warning ? el("p.sw-cell-confirm-warn", { text: warning }) : null);
        //  Type to confirm (Confirm: the text, ex: the account name): the danger button stays disabled until it is typed exactly
        const must = o.Confirm !== undefined && o.Confirm !== null && o.Confirm !== "" ? String(o.Confirm) : null;
        const typed = must !== null && Sw.TextBox ? new Sw.TextBox({
            Label: _T("Type {0} to confirm", must, "Label of the input in a confirmation dialog of a destructive action (ex: delete an account): the action runs only when the text is typed.{0} is the text to type (ex: the account name)"),
            AutoComplete: "off", SpellCheck: false, Dirty: false,
        }) : null;
        if (typed) {
            typed.Input.setAttribute("autocapitalize", "none");
            //  A safety check, not user data: Cancel after typing closes (no "Discard changes?", POP-6)
            typed.Input.setAttribute("data-sw-nodirty", "");
            typed.E.setAttribute("data-sw-nodirty", "");
            typed.Input.setAttribute("data-sw-nodraft", "");
            content.append(typed.E);
        }
        //  The reason of the disabled danger button (INP-12): what enables it
        const typeWhy = typed ? _T("Type {0} to confirm", must, "Reason (tool tip) of the disabled danger button of a type-to-confirm dialog, {0} is the text to type (ex: the account name)") : undefined;
        //  A row action's confirmation follows its row (Admin N16 / G22): when the table no longer lists the row while the question is
        //  open (explore/table.js marks the button data-sw-row-gone and sends "sw-row-gone"), the question says so, the action is off
        //  and Cancel becomes Close: nothing is sent for a row that is gone
        const rowE = o.Row && o.Row.E instanceof Element ? o.Row.E : (o.Row instanceof Element ? o.Row : null);
        let rowGone = false;
        const goneShow = rowE ? (close, box) => {
            const gone = ev => {
                if (rowGone || !box.isConnected)
                    return;
                rowGone = true;
                const who = (ev && ev.detail && ev.detail.Name) || rowE.dataset.swRowName || "";
                //  The table's own cause when it gives one ("OpsVictim is no longer listed: it may have ended…"), never a
                //  guessed "someone else"; then that nothing was sent
                const cause = ev && ev.detail && typeof ev.detail.Reason === "string" ? ev.detail.Reason.trim() : "";
                const msg = cause
                    ? compose(sentence(cause), "", _TF("Nothing was sent.", "End of the line in the confirmation of a table row action when the row went away while the question was open"))
                    : (who
                        ? _T("{0} is no longer listed (it may have ended or been removed). Nothing was sent.", who, "Line in the confirmation of a table row action when the row went away while the question was open. {0} names the row, ex: 'The invitation to anna@example.com'")
                        : _TF("This row is no longer listed (it may have ended or been removed). Nothing was sent.", "Line in the confirmation of a table row action when the row went away while the question was open"));
                content.prepend(el("p.sw-cell-confirm-gone", { attrs: { role: "status" } }, Icon("warning"), el("span", { text: msg })));
                //  (nothing will be sent: the consequence "This cannot be undone." no longer applies)
                const warnE = content.querySelector(".sw-cell-confirm-warn");
                if (warnE)
                    warnE.hidden = true;
                const ok = box.querySelector(".sw-cell-confirm-ok");
                const offWhy = _TF("No longer listed", "Reason (tool tip) of the disabled action of a confirmation whose table row went away");
                if (ok && ok.SwButton)
                    ok.SwButton.SetDisabled(true, offWhy);
                else if (ok)
                    Sw.Disabled.Set(ok, true, offWhy);
                const cancel = box.querySelector(".sw-cell-confirm-cancel");
                if (cancel && cancel.SwButton)
                    cancel.SwButton.SetText(_TF("Close", "The button that closes a confirmation whose table row went away (nothing to confirm any more)"));
                if (cancel && (document.activeElement === ok || (typed && document.activeElement === typed.Input)))
                    cancel.focus();
            };
            if (rowE.dataset.swRowGone === "true")
                gone();
            rowE.addEventListener("sw-row-gone", gone);
        } : null;
        const v = await D.Show({
            Title: o.Title,
            Icon: "warning",
            Content: content,
            Role: "alertdialog",
            Class: "sw-cell-confirm-dialog",
            Buttons: [
                { Text: _TF("Cancel", "The text of a button on a confirmation dialog that when pressed will close the dialog without performing the action"), Title: o.CancelTitle, Value: false, Class: "sw-cell-confirm-cancel" },
                { Text: o.OkText || _TF("OK", "The text of a button on a confirmation dialog that when pressed will confirm that the user wants to perform the action"), Icon: o.OkIcon || undefined, Title: o.OkTitle, Kind: "danger", Value: true, Disabled: typed ? typeWhy : false, Class: "sw-cell-confirm-ok" },
            ],
            //  with a text to type the input gets the focus (Cancel otherwise: a destructive action is never the default)
            Focus: typed ? typed.Input : ".sw-cell-confirm-cancel",
            OnShow: typed || goneShow ? (close, box) => {
                if (goneShow)
                    goneShow(close, box);
                if (!typed)
                    return;
                const ok = box.querySelector(".sw-cell-confirm-ok");
                const match = () => typed.Input.value.trim() === must && !rowGone;
                const sb = ok && ok.SwButton;
                typed.Input.addEventListener("input", () => {
                    if (sb)
                        sb.SetDisabled(!match(), typeWhy);
                    else if (ok)
                        Sw.Disabled.Set(ok, !match(), typeWhy);
                });
                typed.Input.addEventListener("keydown", ev => {
                    if (ev.key !== "Enter")
                        return;
                    ev.preventDefault();
                    if (match())
                        close(true);
                    //  Not typed yet: say what is missing (an attempt never does nothing silently)
                    else if (ok)
                        Sw.Disabled.Reject(ok, ev);
                });
                //  The word is a check, not an edit: Escape closes the dialog (Admin N19: it stepped the word back, up to 3 Escapes); the
                //  text box's own Escape revert sees nothing to revert
                typed.Input.addEventListener("keydown", ev => {
                    if (ev.key === "Escape")
                        typed.Committed = typed.Input.value;
                }, { capture: true });
            } : undefined,
        });
        return v === true && !rowGone && !(rowE && rowE.dataset.swRowGone === "true");
    }

    /**
     * The Prepare step of ConfirmDanger / Sw.Danger.Run: fetch what the confirmation needs (a token, facts) with visible feedback at once
     * (the button busy, or a working dialog when the button is hidden), never in the click's own work (the click only opened a question:
     * it announces nothing), and stop with the reason when it fails (SAF-2 / SAF-4: never a weaker confirmation instead).
     * @param {object} o The ConfirmDanger options (Prepare, Button, Action, Title).
     * @returns {Promise<object|null>} The prepared object, null when it failed or was cancelled (the reason is shown).
     */
    async function prepareDanger(o) {
        //  leave the caller's click scope first: the request is not the click's work (no "returned no promise", no "Delete: done")
        await Promise.resolve();
        const be = o.Button ? (o.Button.E instanceof Element ? o.Button.E : o.Button) : null;
        const visible = be instanceof Element && be.isConnected && be.getClientRects().length > 0 && !be.closest("[hidden], [inert]");
        const ac = typeof AbortController === "function" ? new AbortController() : null;
        const name = o.Action || o.OkText || "";
        /** @type {string} What the wait is for: the question without its question mark ("Delete the log file"), else the action. */
        const subject = (o.Title ? String(o.Title).trim().replace(/\s*\?$/, "") : "") || name;
        let p;
        try {
            p = Promise.resolve(o.Prepare(ac ? ac.signal : undefined));
        }
        catch (x) {
            p = Promise.reject(x);
        }
        let closer = null;
        let t = 0;
        if (visible && Sw.Work && Sw.Work.Busy)
            //  the busy state on the button at once (FBK-2), no result announcement (the confirmation is the next step)
            Sw.Work.Busy(be, p, { Name: name, Done: false, Leave: false });
        else if (Sw.Dialog && Sw.Dialog.Working)
            //  the button is in a menu (gone) or unknown: a small working dialog, so the wait is seen and the focus has a place (FOC-1)
            t = setTimeout(() => {
                t = 0;
                /* SwWeb:allow ux-fbk-4 (Cancel aborts the Prepare request: the signal is given to it) */
                //  (the wait is not the question: "Delete the log file: getting the details", without the question's "?" and without the
                //  dialog menu; the question with its menu comes next; R4-9, S5-A-15)
                closer = Sw.Dialog.Working({
                    Title: subject ? _T("{0}: getting the details", subject, "Title of the small working dialog while a destructive action (ex: Delete) fetches what its confirmation shows. {0}: the action or what it does, ex: Delete the log file") : _TF("Getting the details", "Title of the small working dialog while a destructive action fetches what its confirmation shows"),
                    Text: _TF("Asking the service what this changes…", "Text of the working dialog while a destructive action fetches its confirmation details (ex: a delete token, the file size)"),
                    Promise: p, Background: false, NoMove: true,
                    Cancel: () => ac && ac.abort(),
                });
            }, 300);
        let v = null, err = null;
        try {
            v = await p;
        }
        catch (x) {
            err = x;
        }
        clearTimeout(t);
        if (typeof closer === "function")
            closer();
        if (err && (err.name === "AbortError" || (ac && ac.signal.aborted)))
            return null;
        if (err && Sw.Access && Sw.Access.IsDenied && Sw.Access.IsDenied(err)) {
            if (Sw.Access.ActionDenied)
                Sw.Access.ActionDenied(err, o.Button || null);
            return null;
        }
        if (err || !v || typeof v !== "object") {
            //  One sentence through the core's composer (the server's reason or the status meaning; one piece of advice, never two)
            const what = name
                ? _T("{0} can't be prepared right now, nothing was changed.", name, "Start of the error when a destructive action (ex: Delete) could not fetch its confirmation details (a token); no weaker confirmation is shown; the reason follows. {0}: the action, ex: Delete")
                : _TF("This can't be prepared right now, nothing was changed.", "Start of the error when a destructive action could not fetch its confirmation details; the reason follows");
            if (err && Sw.Fail && Sw.Net && typeof Sw.Net.Message === "function") {
                //  (one composer, one piece of advice: the status' advice, or "Try again" for a reason without one; Ops R4-8)
                Sw.Fail(String(Sw.Net.Message(what, err, { Do: _TF("Try again in a moment.", "Advice after a destructive action could not fetch its confirmation details (a token)") })));
                return null;
            }
            const why = err ? (err.message || String(err)) : _TF("The service sent no details", "Reason when a destructive action could not be prepared because the service answered nothing");
            if (Sw.Fail)
                Sw.Fail(name
                    ? _T("{0} can't be prepared right now: {1}. Nothing was changed; try again.", name, String(why).replace(/[.\s]+$/, ""), "Error when a destructive action (ex: Delete) could not fetch its confirmation details (a token); no weaker confirmation is shown. {0}: the action, ex: Delete; {1}: the reason")
                    : _T("This can't be prepared right now: {0}. Nothing was changed; try again.", String(why).replace(/[.\s]+$/, ""), "Error when a destructive action could not fetch its confirmation details. {0}: the reason"));
            return null;
        }
        return v;
    }

    /**
     * Run a destructive action the one safe way (UX guide SAF-1 / 2 / 3, FBK-2, FOC-1): the confirmation (ConfirmDanger: the title
     * "Delete the email "Test"?", Cancel focused, the consequence, type to confirm with Confirm), then the work with the busy state on
     * the button that started it, one result (announced, or the error), and the focus kept on the page (the button, else Focus). An
     * action can't skip its confirmation this way. Exposed as Sw.Danger.Run.
     * @param {object} o Options: { Action (the verb, the danger button: "Delete"), Target (what: 'the email "Test"'), Item, Consequence
     * (what happens, ex: "The mail is removed for every user."; replaces "This cannot be undone." when Warning is not given), Warning,
     * Confirm (a text to type), Facts, Run(signal) (the work; may return a promise), Button (the Sw.Button / element that started it:
     * busy while it runs), Done (the result text, default "<Action>: done"; false: none), Focus (the element to focus after it, ex: the
     * next row), Prepare(signal) (see ConfirmDanger: details / a token fetched first, with feedback; Run gets the result as its
     * second argument; a failed Prepare shows the reason and runs nothing), Title, ItemLabel, Text, OkText (passed to the confirmation) }.
     * @returns {Promise<boolean>} True when it ran without an error.
     */
    async function DangerRun(o) {
        o = o || {};
        if (Sw.Work && typeof Sw.Work.CheckOptions === "function")
            Sw.Work.CheckOptions("Sw.Danger.Run", o, DangerRunSchema);
        if (typeof o.Run !== "function")
            throw new Error("Sw.Danger.Run: Run (the work) is required");
        if (!o.Action)
            Sw.DevWarn("Sw.Danger.Run without Action: the danger button says \"OK\"; pass the verb (\"Delete\").", "danger-noaction");
        const ok = await ConfirmDanger({
            Action: o.Action, Target: o.Target, Item: o.Item, Text: o.Text !== undefined ? o.Text : (o.Warning !== undefined ? o.Consequence : undefined),
            Warning: o.Warning !== undefined ? o.Warning : o.Consequence, Confirm: o.Confirm, Facts: o.Facts,
            Title: o.Title, ItemLabel: o.ItemLabel, OkText: o.OkText, OkIcon: o.OkIcon, OkTitle: o.OkTitle, CancelTitle: o.CancelTitle,
            Prepare: o.Prepare, Button: o.Button,
        });
        if (!ok)
            return false;
        const prepared = typeof o.Prepare === "function" ? ok : undefined;
        const be = o.Button ? (o.Button.E instanceof Element ? o.Button.E : o.Button) : null;
        //  The work in a click scope: the one result path (Sw.Work.Outcome: a throw, a rejection, a failed request it started or a false
        //  result is not done; an abort is never "done"), shown once (Sw.Work.Report), the focus kept on the page (FOC-1)
        const keep = Sw.Focus && Sw.Focus.Keep && be instanceof Element ? Sw.Focus.Keep(be.closest("table, [role=grid], [role=list], ul, ol, .sw-card, section, main") || be.parentElement || be) : null;
        const said = Sw.Work.Watch();
        const sc = Sw.Work.Scope(signal => o.Run(signal, prepared));
        const p = sc.Threw ? Promise.reject(sc.Error) : (Sw.Work.Promise(sc) || Promise.resolve(sc.Result));
        const off = () => sc.Controller && sc.Controller.abort();
        window.addEventListener("pagehide", off);
        let res;
        //  a button that is not on screen (it sat in a "More actions" menu) can't show the busy state: a working dialog does (FBK-2)
        const shown = be instanceof Element && be.isConnected && be.getClientRects().length > 0 && !be.closest("[hidden], [inert]");
        if (!shown && Sw.Dialog && Sw.Dialog.Working) {
            let wclose = null;
            const wt = setTimeout(() => {
                /* SwWeb:allow ux-fbk-4 (the destructive work was sent: it can't be cancelled; the 10 s Close names that) */
                wclose = Sw.Dialog.Working({ Title: o.Title || (o.Action ? _T("{0}: working", o.Action, "Title of the working dialog while a confirmed destructive action runs. {0}: the action, ex: Delete") : _TF("Working", "Title of the working dialog while a confirmed destructive action runs")), Promise: p, Background: false });
            }, 300);
            const end = () => {
                clearTimeout(wt);
                if (typeof wclose === "function")
                    wclose();
            };
            p.then(end, end);
        }
        if (be instanceof Element)
            res = await Sw.Work.Busy(be, p, { Name: o.Action, Kind: o.Kind, Work: o.Target ? (o.Action + " " + o.Target) : undefined });
        else {
            try {
                res = { Value: await p };
            }
            catch (x) {
                res = { Failed: true, Error: x };
            }
        }
        window.removeEventListener("pagehide", off);
        const out = Sw.Work.Outcome(sc, res);
        const f = o.Focus && o.Focus.isConnected ? o.Focus : null;
        if (f && (!document.activeElement || document.activeElement === document.body || !document.activeElement.isConnected))
            f.focus();
        else if (keep)
            keep.Restore();
        if (keep)
            keep.Stop();
        Sw.Work.Report(out, {
            Name: o.Action || "", Said: said.Stop(), Spun: true, Done: o.Done === false ? false
                : (typeof o.Done === "string" ? o.Done : _T("{0}: done", o.Action || "", "Announced after a confirmed destructive action finished. {0} is the action, ex: 'Delete'")),
        });
        return out.Ok;
    }

    /**
     * True if a cell action can never apply to its row, so its button is disabled. Only a fallback for servers that send no per row
     * visibility (the optional "visible" part of an action, then this is never used): killing process id 0 (the Idle / swapper pseudo
     * process on every OS) or the Windows "System" process (id 4 with the name System; on other systems id 4 may be a normal process, so
     * the name must match too). Only the old KillProcess?pid request is checked.
     * @param {{Link: string, Visible: (boolean|undefined)}} d The action.
     * @param {HTMLElement} e The cell (its row's item text, ItemName).
     * @returns {boolean} True to disable the action.
     */
    function actionNotApplicable(d, e) {
        if (d.Visible !== undefined)
            return false;
        const m = /(?:^|\/)KillProcess\?(?:pid=)?(-?\d+)$/i.exec(String(d.Link || ""));
        if (!m)
            return false;
        const pid = parseInt(m[1]);
        if (pid <= 0)
            return true;
        return pid === 4 && /(^|,\s*|:\s*)System(\s*,|$)/.test(ItemName(e));
    }

    /**
     * True if another cell of the cell's row is a Toggle switch that sends this request (data-sw-toggle-api).
     * @param {HTMLElement} e The cell.
     * @param {string} link The action's request.
     * @returns {boolean} True if a Toggle of the row has the same request.
     */
    function toggleOfRow(e, link) {
        const tr = e.parentElement;
        if (!tr)
            return false;
        for (const c of tr.children)
            if (c !== e && c.dataset && c.dataset.swToggleApi === link)
                return true;
        return false;
    }

    /**
     * Renderer "Actions" (class Actions): one button per formats entry 1..n, each "text|title|link|image[|confirm|visible|done[|final]]" formatted with
     * the args (value parts, with nextValue inserted at index 1); the image is an icon. A link starting with '&' opens in the same tab, '@' in
     * a new tab, anything else is a GET request; then onRefresh() is called. Actions without a link are disabled. An action with the same
     * request as a Toggle switch of the row is left out (the switch shows the state and toggles it).
     * The optional parts come from the C# TableDataActionsExAttribute (confirm, visible) and TableDataActionsFullAttribute (also done):
     * - confirm: the server's confirmation question (ex: "Kill the process?"), "-" = never ask; empty / missing = the server says nothing,
     *   then destructive looking actions (IsDestructiveAction: Kill, Delete, Remove, Stop ...) ask first. confirmAction names the row's item.
     * - visible: empty / missing = always shown, else the action is left out of a row when the formatted text is "", "0", "false" or "no"
     *   (ex: "{2}" with the value "1234,0").
     * - done: the text of a successful action (its row note and announcement in a data table) instead of "<Action>: done"; empty / missing =
     *   the generic text. The server's own outcome text (X-Sw-Outcome) still wins.
     * - final (TableDataActionsFullAttribute with TableDataActionFlags.Final): "final" = the action ends or removes the row's item (Delete,
     *   Kill, Withdraw ...), "" = it does not (the server declared it). The button gets data-sw-final="true" / "false"; without the part (an
     *   older format, TableDataActions / Ex) it has no data-sw-final and a data table treats the action as not final (the original re-read the
     *   row after every action). It is never guessed from the words of the text or tool tip (G47, Admin N53).
     * @param {HTMLElement} e The cell. @param {*} value The value. @param {string[]} formats The format arguments. @param {*} nextValue The next value.
     * @param {string|false} flash Flash class. @param {string} type The C# type. @param {function} onRefresh Refresh callback.
     * @returns {HTMLElement|undefined} The cell.
     */
    function Actions(e, value, formats, nextValue, flash, type, onRefresh) {
        if (Reset(e, "Actions"))
            e.classList.add(Classes.Actions);
        if (value === null || value === undefined) {
            e.replaceChildren();
            state(e).Sig = null;
            return;
        }
        const args = String(value).split(",");
        args.splice(1, 0, nextValue);
        const defs = [];
        for (let i = 1; i < formats.length; ++i) {
            const p = formats[i].split("|");
            const d = { Text: sfa(p[0] ?? "", args), Title: sfa(p[1] ?? "", args), Link: sfa(p[2] ?? "", args), Image: sfa(p[3] ?? "", args) };
            //  The server's confirmation: undefined = not specified (the words decide), false = never ask, else the question
            if (p[4])
                d.Confirm = p[4] === "-" ? false : sfa(p[4], args);
            //  The server's per row visibility (ex: Kill only on processes that can be killed)
            if (p[5]) {
                d.Visible = !/^\s*(0|false|no)?\s*$/i.test(sfa(p[5], args));
                if (!d.Visible)
                    continue;
            }
            //  The done text of the action (C# TableDataActionsFullAttribute: ex: "{2} must choose a new password at the next sign in"): the
            //  result of a successful action in the table (its row note and announcement) instead of "<Action>: done" (Admin N8, G15)
            if (p[6])
                d.Done = sfa(p[6], args);
            //  Final or not, as the server declared it (C# TableDataActionFlags.Final, the 8th part: "final" / ""); undefined = not declared
            //  (an older format). Never guessed from the words (G47, Admin N53)
            if (p.length > 7)
                d.Final = p[7].trim().toLowerCase() === "final";
            //  The same request as a Toggle switch of the row (it shows the state and toggles it): one control for one setting
            if (d.Link && toggleOfRow(e, d.Link))
                continue;
            defs.push(d);
        }
        SetNodes(e, "a" + JSON.stringify(defs), () => defs.map(d => {
            const na = !!d.Link && actionNotApplicable(d, e);
            const title = na
                ? _TF("Not possible for this item (a system process that cannot be stopped)", "Tool tip of a disabled table cell action (ex: Kill on the Idle or System process)")
                : d.Title;
            const b = el("button.sw-cell-action", { type: "button", title: title || null, attrs: { "aria-label": d.Text || d.Title || null, "data-sw-done": d.Done || null, "data-sw-final": d.Final === undefined ? null : String(d.Final) } },
                d.Image ? Icon(d.Image) : null, d.Text ? el("span", { text: d.Text }) : null);
            //  Not possible: aria-disabled (focusable, a click or Enter says why; UX guide INP-12)
            if (!d.Link || na)
                Sw.Disabled.Set(b, true, na ? title : (d.Title || _TF("Not available for this item", "Tool tip of a disabled table cell action that has no request for this row")));
            if (d.Link && !na) {
                let busy = false;
                b.addEventListener("click", async ev => {
                    if (Sw.BadClick(ev) || busy)
                        return;
                    busy = true;
                    //  Where the row is (its table and key), before a confirmation during which a refresh may replace it (Admin N16)
                    b.SwActHost = b.closest("table, [role=grid], .sw-table, .sw-cards");
                    const rk = b.closest("[data-sw-item]");
                    b.SwActKey = rk ? rk.getAttribute("data-sw-item") : "";
                    try {
                        const ask = d.Confirm === undefined ? IsDestructiveAction(d) : !!d.Confirm;
                        if (ask && !await confirmAction(e, d, b))
                            return;
                        const u = d.Link;
                        const c = u.charAt(0);
                        if (c === "&" || c === "@") {
                            const P = Sw.Page || {};
                            const tgt = c === "&" ? "_self" : "_blank";
                            if (P.Navigate)
                                P.Navigate(u.substring(1), tgt);
                            else
                                window.open(u.substring(1), tgt);
                            onRefresh && onRefresh();
                        }
                        else
                            await runAction(b, u, onRefresh);
                    }
                    finally {
                        busy = false;
                    }
                });
            }
            return b;
        }), "", flash);
        return e;
    }

    // ------------------------------------------------------------------ dispatch

    /** @type {Map<string, function>} C# type name -> renderer (when there is no format). */
    const TypeFormatters = new Map([
        ["System.SByte", NumberCell],
        ["System.Int16", NumberCell],
        ["System.Int32", NumberCell],
        ["System.Int64", NumberCell],
        ["System.Byte", NumberCell],
        ["System.UInt16", NumberCell],
        ["System.UInt32", NumberCell],
        ["System.UInt64", NumberCell],
        ["System.Single", NumberCell],
        ["System.Double", NumberCell],
        ["System.Decimal", NumberCell],
        ["System.DateTime", DateTime],
        ["System.TimeSpan", TimeSpan],
        ["System.Boolean", BooleanCell],
        ["System.Type", Type],
        ["SysWeaver.MessageLevels", MessageLevelCell],
    ]);

    /** @type {Map<string, function>} Format name (first ';' part of the format) -> renderer; overrides TypeFormatters. */
    const FormatFormatters = new Map([
        ["Default", Default],
        ["Number", NumberCell],
        ["Url", Url],
        ["Img", Img],
        ["Tags", Tags],
        ["PerRowFormat", PerRowFormat],
        ["ByteSize", bytesRenderer(false)],
        ["ByteSpeed", bytesRenderer(true)],
        ["Toggle", Toggle],
        ["Actions", Actions],
        ["Duration", TimeSpan],
        ["Json", Json],
        ["Text", LongText],
        ["MD", MarkDown],
        ["Uptime", DateTimeLive],
        ["WebColor", WebColor],
    ]);

    /**
     * Render a value into a cell (the old ValueFormat.update): the format "Name;arg1;arg2..." picks the renderer (FormatFormatters), else the
     * C# type (TypeFormatters), else Default. The cell keeps exactly one kind class (Classes.Kind + the format name, ex: sw-fmt-Number).
     * @param {HTMLElement} e The cell.
     * @param {string} type The C# type name of the value.
     * @param {*} value The value.
     * @param {string|false} [flash] A class that flashes when the content changes.
     * @param {string} [format] The format ("Kind;arg1;arg2...").
     * @param {*} [nextValue] The next column's value ({1} in the templates).
     * @param {function():void} [onRefresh] Called after a Toggle / Actions request (refresh the data).
     * @returns {HTMLElement} The cell.
     */
    function Update(e, type, value, flash, format, nextValue, onRefresh) {
        const formats = format ? String(format).split(";") : [];
        let fn = TypeFormatters.get(type) || Default;
        let kind = null;
        if (formats.length > 0) {
            kind = Classes.Kind + formats[0];
            const fs = FormatFormatters.get(formats[0]);
            if (fs)
                fn = fs;
        }
        const pre = Classes.Kind;
        const rm = [];
        e.classList.forEach(c => {
            if (c.startsWith(pre) && c !== kind)
                rm.push(c);
        });
        rm.forEach(c => e.classList.remove(c));
        if (kind)
            e.classList.add(kind);
        fn(e, value, formats, nextValue, flash, type, onRefresh);
        return e;
    }

    Sw.Cells = {
        Classes,
        Update,
        Reset,
        SetText,
        SetNodes,
        UrlTarget,
        CreateLink,
        External,
        ExternalLink,
        /** @type {?boolean} false: the app knows there is no internet (links to other sites are shown as plain text, see External); null = unknown. */
        Internet: null,
        OnLink,
        ShowJson,
        ShowText,
        ShowMarkdown,
        TypeFormatters,
        FormatFormatters,
        Default,
        Type,
        Number: NumberCell,
        WebColor,
        Url,
        Img,
        DateTimeLive,
        Uptime: DateTimeLive,
        DateTime,
        TimeSpan,
        Duration: TimeSpan,
        Json,
        MarkDown,
        LongText,
        Boolean: BooleanCell,
        ByteSize: FormatFormatters.get("ByteSize"),
        ByteSpeed: FormatFormatters.get("ByteSpeed"),
        Tags,
        /** The special tag of an auth requirement: null = the accent "Open" tag, "" = "Any logged in user", null for tokens (AuthTag). */
        AuthTag,
        PerRowFormat,
        Toggle,
        Actions,
        IsDestructiveAction,
        ConfirmDanger,
        ItemName,
        RowName,
    };
    Sw.Markdown = Markdown;
    //  Destructive actions the one safe way: Sw.Danger.Run (confirm, busy, one result), Sw.Danger.Confirm (= Sw.Cells.ConfirmDanger)
    Sw.Danger = Object.assign(Sw.Danger || {}, { Run: DangerRun, Confirm: ConfirmDanger });
})();
