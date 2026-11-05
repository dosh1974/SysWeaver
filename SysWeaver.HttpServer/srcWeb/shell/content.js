/**
 * @file shell/content.js - SysWeaver app shell (Sw.Shell), part 2 of 3: the content frame host and the shell history.
 *
 * Sw.Shell.ContentHost shows one page at a time in an iframe inside a host element (the shell's <app-content>):
 *  - Navigate(url): a new iframe is created hidden (class "Hidden", no generic load fade); the previous one is hidden and removed 25 ms later.
 *  - Reveal protocol: the new frame is revealed when its page posts {Type: "LoaderRemoved"} (Sw.Page.Loaded / the old PageLoaded) - only
 *    messages from the CURRENT frame's window count - or 1 s after its load event. On reveal the frame document gets html.Embedded, the
 *    OnReveal(url, title, win) callback runs (the shell selects the menu item and sets the title).
 *  - In-frame navigation (links, location changes, server redirects inside the frame): the frame hides instantly (no transition) on the
 *    document's pagehide and the next document is revealed like a new page (no unthemed flash).
 *  - A slow page shows a thin progress bar on the host (class sw-shell-loading) after 300 ms, plus a centered spinner with "Loading..."
 *    (div.sw-shell-pageloading, a polite status) while the new page is not revealed yet (it fades in, so fast pages never show it).
 *  - History (option History): every navigation pushes {state: url, hash: "#" + Base64(url)} (the deep link format), except the first one,
 *    which replaces the current entry (so going back to a deep linked first page works); RestoreState(url) re-embeds without a new entry.
 *  - Navigating to an url clears that page's stored page state (Sw.History.ClearOther), unless restoring from history (NoHistory) or
 *    KeepState (the shell's return after a sign in).
 *  - Lost connection (UX guide FBK-9, FBK-6): a page opened while Sw.Net.Online is false, a frame whose first document is the browser's
 *    network error page, a page still loading when the connection is lost, or one not shown after 15 s while the server can't be
 *    reached shows the offline state ("Could not open <page>", Try now) instead of an endless loader, and opens by itself on
 *    "net.online". A page not shown after 15 s while online gets a developer warning (Sw.DevWarn).
 *  - Security: Navigate refuses non http(s) urls ("javascript:" in a frame src would run in the shell origin); the shell checks the
 *    origin of untrusted urls (deep links, messages) before calling it. A frame that showed a page of this site and then navigates
 *    itself to another origin (a link or script in the page) is never revealed (OnForeign; the shell goes back to the last page and
 *    tells the user). "LoaderRemoved" for a page of this site only counts from this origin.
 *
 * Classic script (IIFE, "use strict"), needs sw/core.js and sw/ui.js. Documentation: shell/README.md.
 */
(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const Shell = Sw.Shell = Sw.Shell || {};
    //  Load once: this file is part of sw/shell.js, app/application.js and sw/compat.js, a page may load more than one of them
    if (Shell.ContentHost)
        return;

    /**
     * Test if a message source is a window inside the content frame: the frame's window itself or a frame nested in it (any depth).
     * @param {?Window} src The message source (MessageEvent.source).
     * @param {?Window} frameWin The content frame's window.
     * @returns {boolean} True when src is frameWin or one of its descendants.
     */
    Shell.InFrame = (src, frameWin) => {
        if (!src || !frameWin)
            return false;
        try {
            for (let w = src, i = 0; w && i < 16; ++i) {
                if (w === frameWin)
                    return true;
                if (w === w.parent)
                    return false;
                w = w.parent;
            }
        }
        catch {
        }
        return false;
    };

    /** Time (ms) after the frame's load event before it is revealed anyway. */
    const RevealFallbackMs = 1000;

    /** Time (ms) before the loading bar shows. */
    const LoadingBarMs = 300;

    /**
     * Encode a content url as the shell hash (standard base64 of the utf-8 bytes, as before).
     * @param {string} url The absolute url.
     * @returns {string} The hash text (without "#").
     */
    function EncodeHash(url) {
        return Sw.Util.Base64EncodeString(url);
    }

    /**
     * Decode the shell hash.
     * @param {string} hash The hash (with or without "#").
     * @returns {string|null} The url, or null if the hash is empty or invalid.
     */
    function DecodeHash(hash) {
        if (!hash)
            return null;
        if (hash.charAt(0) === "#")
            hash = hash.substring(1);
        if (!hash)
            return null;
        try {
            return Sw.Util.Base64DecodeString(decodeURIComponent(hash)) || null;
        }
        catch {
            return null;
        }
    }

    /**
     * The HTTP error of the document in a content frame: the navigation's response status (Navigation Timing
     * responseStatus) when it is 400 or more, else 404 for the server's plain text "Not Found" answer (browsers without
     * responseStatus). 0 for a normal page, an unreadable (other site) frame or an empty document.
     * @param {Window|null} win The frame window.
     * @returns {number} The status, 0 if none.
     */
    function PageErrorStatus(win) {
        if (!win)
            return 0;
        try {
            const nav = win.performance && win.performance.getEntriesByType ? win.performance.getEntriesByType("navigation")[0] : null;
            const s = nav && typeof nav.responseStatus === "number" ? nav.responseStatus : 0;
            if (s >= 400)
                return s;
            if (s > 0)
                return 0;
            const d = win.document;
            if (d && d.contentType && d.contentType !== "text/html" && d.body && /^\s*Not Found\b/i.test(d.body.textContent || ""))
                return 404;
        }
        catch {
        }
        return 0;
    }

    /**
     * Hosts the content iframe of the shell (see the file header).
     */
    class ContentHost {
        /**
         * @param {object} o Options: { Host (the element that gets the iframes), History (push history entries), OnStart(url) (navigation
         * started), OnReveal(url, title, win, frame) (a page was revealed; frame.SwUrl is the requested url), Title(url, win, frame) (optional: the title for a revealed page),
         * OnForeign(lastUrl, frame) (optional: a page of this site navigated the frame to another site, which is never revealed; lastUrl is
         * the last same-origin document of the frame; without it the frame is blanked) }.
         */
        constructor(o) {
            const t = this;
            t.O = o || {};
            t.Host = t.O.Host;
            /** @type {HTMLIFrameElement|null} The current frame. */
            t.Frame = null;
            /** @type {boolean} True after the first navigation. */
            t.DoneFirst = false;
            /** @type {HTMLElement|null} The loading indicator (spinner and "Loading..." text; shown while the host has the class sw-shell-loading). */
            t.LoadingEl = null;
            if (t.Host) {
                const text = _TF("Loading...", "Text shown in the middle of the application while a page is loading");
                t.LoadingEl = Sw.el("div.sw-shell-pageloading", { attrs: { role: "status", "aria-live": "polite" } },
                    Sw.Spinner ? new Sw.Spinner({ Size: "2.4em" }).E : null,
                    Sw.el("span.sw-shell-pageloadingtext", { text }));
                t.Host.append(t.LoadingEl);
            }
            t.OnMessage = ev => t.HandleMessage(ev);
            window.addEventListener("message", t.OnMessage);
            /** @type {string|null} The url that could not be opened because the server can't be reached (opened when it is back). */
            t.OfflineUrl = null;
            //  A lost connection (UX guide FBK-9): a page that is still loading shows the offline state instead of an endless loader,
            //  and opens by itself when the server answers again
            t.OnNetOffline = () => {
                const f = t.Frame;
                if (f && f.SwReveal && !t.OfflineUrl)
                    t.ShowOffline(f.SwUrl || t.GetUrl(), f);
            };
            t.OnNetOnline = () => {
                const u = t.OfflineUrl;
                if (u) {
                    t.OfflineUrl = null;
                    t.Navigate(u, { NoHistory: true });
                }
            };
            if (Sw.Events) {
                Sw.Events.On("net.offline", t.OnNetOffline);
                Sw.Events.On("net.online", t.OnNetOnline);
            }
        }

        /**
         * Stop listening (the host is no longer used).
         */
        Dispose() {
            window.removeEventListener("message", this.OnMessage);
            if (Sw.Events && Sw.Events.Off) {
                Sw.Events.Off("net.offline", this.OnNetOffline);
                Sw.Events.Off("net.online", this.OnNetOnline);
            }
        }

        /**
         * Show the offline state instead of a page that can't be loaded because the server can't be reached (UX guide FBK-9, FBK-6): "Could
         * not open <page>", the page opens by itself when the connection is back ("Try now" checks at once). The frame stays hidden.
         * @param {string|null} url The url of the page.
         * @param {HTMLIFrameElement} [f] The frame that was loading it.
         */
        ShowOffline(url, f) {
            const t = this;
            if (!url)
                return;
            if (f) {
                f.SwReveal = null;
                clearTimeout(f.SwTimer);
                clearTimeout(f.SwWatch);
                f.classList.add("Hidden");
                f.setAttribute("aria-hidden", "true");
            }
            t.OfflineUrl = url;
            t.SetLoading(false);
            let name = null;
            try {
                name = t.O.Title ? t.O.Title(url, null, f || t.Frame) : null;
            }
            catch {
            }
            const title = name
                ? _T("Could not open {0}", name, "Title of the state shown instead of a page while the server can't be reached.{0} is replaced with the name of the page (its menu item)")
                : _TF("Could not open the page", "Title of the state shown instead of a page while the server can't be reached (the page has no name)");
            const text = _TF("The service can't be reached. The page opens by itself when the connection is back; nothing you typed elsewhere is lost.", "Text of the state shown instead of a page while the service can't be reached");
            const actions = [];
            if (Sw.Button)
                actions.push(new Sw.Button({
                    Text: _TF("Try now", "Button of the offline state of a page: checks the connection now and opens the page when the server answers"),
                    Title: _TF("Check the connection to the service now and open the page", "Tool tip of the Try now button of the offline state of a page"),
                    Icon: "refresh", Kind: "primary",
                    OnClick: async () => {
                        const ok = Sw.Net && Sw.Net.Probe ? await Sw.Net.Probe() : true;
                        if (ok && t.OfflineUrl)
                            t.OnNetOnline();
                    },
                }));
            t.ClearPageError();
            const box = Sw.el("div.sw-shell-pageerror.sw-shell-pageoffline.sw-root");
            box.dataset.status = "offline";
            const st = Sw.EmptyState ? Sw.EmptyState({ Icon: "warning", Title: title, Text: text, Actions: actions.map(b => b.E), Error: false })
                : { E: Sw.el("div", { text: title + ". " + text }) };
            box.append(st.E);
            if (t.Host)
                t.Host.append(box);
            t.ErrorEl = box;
            //  The header and the breadcrumb name the page ("Monitoring > Active sessions"); "Could not open …" is the state's own heading
            if (t.O.OnReveal)
                t.O.OnReveal(url, name || title, null, f || t.Frame);
        }

        /**
         * Show the error state of a page that could not be shown (Design v2: Sw.EmptyState with Back and Home) instead of
         * the frame (it stays hidden). Options used: O.AppName() (the app's name in the text), O.OnHome() (the Home action; default: the
         * site root).
         * @param {number} status The HTTP status (404, 403, 500, ...).
         * @param {string|null} url The url of the page.
         * @returns {string} The title for the top bar.
         */
        ShowPageError(status, url) {
            const t = this;
            const app = (t.O.AppName && t.O.AppName()) || document.title || "";
            const notFound = status === 404 || status === 410;
            const denied = status === 401 || status === 403;
            const title = notFound ? _TF("Page not found", "Title of the state shown in the application when a page does not exist (HTTP 404)")
                : denied ? _TF("No access to this page", "Title of the state shown in the application when the server refused a page (HTTP 401 / 403)")
                : _TF("The page could not be shown", "Title of the state shown in the application when the server failed to send a page (HTTP 5xx)");
            const text = notFound
                ? (app ? _T("This page is not available in {0}. The link may be old, or the page belongs to another app.", app, "Text of the state shown when a page does not exist, {0} is the name of the application")
                    : _TF("This page is not available here. The link may be old, or the page belongs to another app.", "Text of the state shown when a page does not exist (no application name)"))
                : denied ? _TF("The service did not allow this page for your account. Ask an administrator if you need it.", "Text of the state shown when the service refused a page")
                : _T("The service answered with an error ({0}). Try again in a moment.", String(status), "Text of the state shown when the service failed to send a page, {0} is the HTTP status code");
            const actions = [];
            if (Sw.Button) {
                if (window.history.length > 1)
                    actions.push(new Sw.Button({ Text: _TF("Back", "Button of the page error state that goes back to the previous page"), Icon: "left", OnClick: () => window.history.back() }));
                actions.push(new Sw.Button({
                    Text: _TF("Go to Home", "Button of the page error state that opens the start page"), Icon: "home", Kind: "primary",
                    OnClick: () => t.O.OnHome ? t.O.OnHome() : (window.location.href = Sw.Url.Root),
                }));
                if (!notFound && !denied && url)
                    actions.push(new Sw.Button({ Text: _TF("Try again", "Button of the page error state that loads the page again"), Icon: "refresh", OnClick: () => t.Navigate(url, { NoHistory: true }) }));
            }
            t.ClearPageError();
            const box = Sw.el("div.sw-shell-pageerror.sw-root");
            box.dataset.status = String(status);
            const st = Sw.EmptyState ? Sw.EmptyState({ Icon: notFound ? "search" : "warning", Title: title, Text: text, Actions: actions.map(b => b.E), Error: !notFound && !denied })
                : { E: Sw.el("div", { text: title + ". " + text }) };
            box.append(st.E);
            if (t.Host)
                t.Host.append(box);
            t.ErrorEl = box;
            const first = box.querySelector(".sw-button.sw-primary");
            if (first && document.activeElement === document.body)
                first.focus({ preventScroll: true });
            return title;
        }

        /**
         * Remove the page error state (a navigation, a working page).
         */
        ClearPageError() {
            if (this.ErrorEl) {
                this.ErrorEl.remove();
                this.ErrorEl = null;
            }
        }

        /**
         * Handle a window message: "LoaderRemoved" from the current frame reveals it.
         * @param {MessageEvent} ev The event.
         */
        HandleMessage(ev) {
            const d = ev.data;
            if (!d || d.Type !== "LoaderRemoved")
                return;
            const f = this.Frame;
            //  Pages of this site must send it from this origin (an external Embedded menu page may send it from its own origin); from
            //  the page or a frame nested in it (as the original shell)
            if (f && f.SwReveal && Sw.Shell.InFrame(ev.source, f.contentWindow) && (!f.SwSameOrigin || ev.origin === window.location.origin))
                f.SwReveal();
        }

        /**
         * The url shown now: the live location of the frame when readable (same origin), else its src.
         * @returns {string|null} The url, or null without a frame.
         */
        GetUrl() {
            const f = this.Frame;
            if (!f)
                return null;
            try {
                const h = f.contentWindow.location.href;
                if (h && h !== "about:blank")
                    return h;
            }
            catch {
            }
            return f.src || null;
        }

        /**
         * Show a page. Only http(s) urls are shown: "javascript:", "data:", "blob:" etc. are refused (a warning is logged, nothing
         * changes), as a frame src with a script url runs in the shell's own origin. Callers with untrusted urls (deep links, messages)
         * must also check the origin (see the shell's SafeContentUrl and Sw.Url.SafeSameOrigin).
         * @param {string} url The url (relative urls resolve against the document).
         * @param {object} [o] Options: { NoHistory (don't add a history entry, don't clear the page state: restoring), Replace (replace the
         * current history entry instead of adding one), KeepState (keep the page's stored state, ex: a page shown again after a sign in) }.
         * @returns {boolean} False if the url was refused (or there is no host).
         */
        Navigate(url, o) {
            const t = this;
            o = o || {};
            const host = t.Host;
            if (!host)
                return false;
            if (!Sw.Url.IsHttp(url, document.baseURI)) {
                console.warn("Sw.Shell: refused to show a non http(s) url in the content frame");
                return false;
            }
            url = new URL(url, document.baseURI).href;
            if (!o.NoHistory && !o.KeepState)
                Sw.History.ClearOther(url);
            if (t.O.OnStart)
                t.O.OnStart(url);
            t.ClearPageError();
            t.OfflineUrl = null;
            //  Hide and remove the previous frame(s)
            for (const c of Array.from(host.children)) {
                if (c.tagName === "IFRAME") {
                    c.SwReveal = null;
                    clearTimeout(c.SwTimer);
                    clearTimeout(c.SwWatch);
                    c.classList.add("Hidden");
                    c.setAttribute("aria-hidden", "true");
                    c.tabIndex = -1;
                    setTimeout(() => c.remove(), 25);
                }
            }
            //  No generic load fade: the shell controls the visibility with the "Hidden" class (an inline opacity fade would override it)
            const f = document.createElement("iframe");
            f.className = "Hidden";
            f.setAttribute("frameborder", "0");
            f.setAttribute("allowtransparency", "true");
            f.setAttribute("allow", "fullscreen; clipboard-read; clipboard-write");
            f.setAttribute("title", _TF("Content", "Accessible name of the frame that shows the current page in the application"));
            f.SwUrl = url;
            t.Frame = f;
            t.SetLoading(true);
            /** Reveal the frame (once per document; re-armed when the frame navigates by itself). */
            const reveal = () => {
                if (t.Frame !== f || !f.SwReveal)
                    return;
                f.SwReveal = null;
                clearTimeout(f.SwTimer);
                clearTimeout(f.SwWatch);
                f.SwTimer = 0;
                let newUrl = null, title = null, win = null;
                try {
                    win = f.contentWindow;
                    newUrl = win.location.href;
                    try {
                        win.document.documentElement.classList.add("Embedded");
                    }
                    catch {
                    }
                }
                catch {
                    newUrl = f.src;
                    win = null;
                }
                //  a page of this site that does not exist (or failed) shows the shell's error state, not the raw server text
                const status = PageErrorStatus(win);
                if (status) {
                    t.SetLoading(false);
                    const ttl = t.ShowPageError(status, newUrl);
                    if (t.O.OnReveal)
                        t.O.OnReveal(newUrl, ttl, win, f);
                    return;
                }
                t.ClearPageError();
                if (t.O.Title)
                    title = t.O.Title(newUrl, win, f);
                if (!title) {
                    try {
                        title = win ? (win.document.title || win.location.pathname) : null;
                    }
                    catch {
                    }
                }
                f.classList.remove("Hidden");
                f.removeAttribute("aria-hidden");
                t.SetLoading(false);
                if (t.O.OnReveal)
                    t.O.OnReveal(newUrl, title, win, f);
            };
            f.SwReveal = reveal;
            //  Security (defense in depth): a frame showing a page of this site must not navigate itself to another site (a link or a
            //  script in the page); the url of the last same-origin document is kept to go back to it
            f.SwSameOrigin = !!Sw.Url.SafeSameOrigin(url, document.baseURI);
            f.SwLastUrl = null;
            /**
             * The frame could not load a page of this site (no readable document, or nothing after a while): when the server can't be
             * reached the offline state shows (and the page opens by itself later), else the page error state with Try again.
             */
            const failed = async () => {
                if (t.Frame !== f || !f.SwReveal)
                    return;
                const ok = Sw.Net && Sw.Net.Check ? await Sw.Net.Check() : true;
                if (t.Frame !== f || !f.SwReveal)
                    return;
                if (!ok)
                    t.ShowOffline(f.SwUrl || url, f);
                else {
                    f.SwReveal = null;
                    clearTimeout(f.SwTimer);
                    clearTimeout(f.SwWatch);
                    t.SetLoading(false);
                    const ttl = t.ShowPageError(503, f.SwUrl || url);
                    if (t.O.OnReveal)
                        t.O.OnReveal(f.SwUrl || url, ttl, null, f);
                }
            };
            //  A page that is not shown after a while: check the connection (an offline navigation never fires load in some browsers);
            //  a slow page (slow network) keeps loading, and a developer warning names it
            clearTimeout(f.SwWatch);
            f.SwWatch = setTimeout(async () => {
                if (t.Frame !== f || !f.SwReveal)
                    return;
                const ok = Sw.Net && Sw.Net.Check ? await Sw.Net.Check() : true;
                if (t.Frame !== f || !f.SwReveal)
                    return;
                if (!ok)
                    t.ShowOffline(f.SwUrl || url, f);
                else if (Sw.DevWarn)
                    Sw.DevWarn("Sw.Shell: the page " + (f.SwUrl || url) + " is still not shown after 15 s: call Sw.Page.Loaded() (or the loader's done()) when it is ready, also after errors", "shell.unrevealed." + (f.SwUrl || url));
            }, 15000);
            f.addEventListener("load", () => {
                if (t.Frame !== f)
                    return;
                let readable = false;
                try {
                    readable = !!f.contentWindow.location.href && f.contentWindow.location.href !== "about:blank";
                }
                catch {
                }
                if (readable)
                    f.SwLastUrl = f.contentWindow.location.href;
                else if (f.SwSameOrigin && !f.SwLastUrl && f.SwReveal) {
                    //  The first document of a page of this site is not readable: the browser's network error page (the server can't
                    //  be reached), never shown
                    failed();
                    return;
                }
                else if (f.SwSameOrigin && f.SwLastUrl) {
                    //  A page of this site navigated the frame to another origin: never reveal it (the first document of a frame
                    //  is not checked: an unreadable first load is usually a network error page)
                    f.SwReveal = null;
                    clearTimeout(f.SwTimer);
                    console.warn("Sw.Shell: a page navigated the content frame to another site");
                    if (t.O.OnForeign)
                        t.O.OnForeign(f.SwLastUrl, f);
                    else
                        f.src = "about:blank";
                    return;
                }
                try {
                    f.contentWindow.addEventListener("pagehide", () => {
                        if (!f.isConnected || t.Frame !== f)
                            return;
                        //  Hide instantly (no opacity transition), else the next document shows during the fade out
                        const s = f.style;
                        s.transition = "none";
                        f.classList.add("Hidden");
                        void f.offsetWidth;
                        s.transition = "";
                        //  A page that navigates before it was revealed is a redirect: keep the requested url (menu selection)
                        if (!f.SwReveal)
                            f.SwUrl = null;
                        f.SwReveal = reveal;
                        t.SetLoading(true);
                        if (t.O.OnStart)
                            t.O.OnStart(null);
                    });
                }
                catch {
                    //  Cross-origin document: can't be observed
                }
                clearTimeout(f.SwTimer);
                f.SwTimer = setTimeout(() => {
                    if (f.SwReveal)
                        f.SwReveal();
                }, RevealFallbackMs);
            });
            if (t.O.History) {
                if (!o.NoHistory) {
                    if (t.DoneFirst && !o.Replace)
                        t.PushState(url);
                    else if (!t.DoneFirst && Shell.DecodeHash(window.location.hash))
                        t.ReplaceState(url);
                    else if (o.Replace)
                        t.ReplaceState(url);
                }
            }
            t.DoneFirst = true;
            window.SysWeaverDoneFirst = true;
            //  Known to be offline: no request, the offline state at once (the page opens when the server answers again)
            if (Sw.Net && Sw.Net.Online === false) {
                host.append(f);
                t.ShowOffline(url, f);
                return true;
            }
            f.src = Sw.Url.AppendTheme(url);
            host.append(f);
            return true;
        }

        /**
         * Show or hide the loading bar and the loading indicator (after a short delay).
         * @param {boolean} on Loading.
         */
        SetLoading(on) {
            const t = this;
            clearTimeout(t.LoadingTimer);
            if (on)
                t.LoadingTimer = setTimeout(() => t.Host.classList.add("sw-shell-loading"), LoadingBarMs);
            else
                t.Host.classList.remove("sw-shell-loading");
        }

        /**
         * Add a history entry for an url (state = url, hash = base64 url).
         * @param {string} url The absolute url.
         */
        PushState(url) {
            const n = new URL(window.location.href);
            n.hash = EncodeHash(url);
            window.history.pushState(url, "", n);
        }

        /**
         * Replace the current history entry with an url.
         * @param {string} url The absolute url.
         */
        ReplaceState(url) {
            const n = new URL(window.location.href);
            n.hash = EncodeHash(url);
            window.history.replaceState(url, "", n);
        }

        /**
         * Focus the content frame (keyboard users continue in the page).
         */
        Focus() {
            const f = this.Frame;
            if (!f)
                return;
            try {
                f.focus({ preventScroll: true });
                f.contentWindow.focus();
                //  Never the page's body (FOC-1, F6 from a toast): the element the user was in, else the top open dialog's first field,
                //  else the first focusable element of the page's main content, else the main content itself
                const d = f.contentDocument;
                const ae = d && d.activeElement;
                if (d && (!ae || ae === d.body || ae === d.documentElement || ae.closest("[inert], .sw-toast.sw-hide"))) {
                    const usable = e => !!e && e.isConnected && !e.closest("[inert], [hidden], .sw-toasts") && e.getClientRects().length > 0;
                    const sel = "button:not([disabled]), [href], input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";
                    const last = f.SwLastFocus && f.SwLastFocus.deref ? f.SwLastFocus.deref() : null;
                    const modals = Array.from(d.querySelectorAll("[aria-modal='true']")).filter(m => !m.hidden && m.getClientRects().length > 0);
                    const top = modals[modals.length - 1] || null;
                    let to = last && usable(last) && (!top || top.contains(last)) ? last : null;
                    const box = top || d.querySelector("main, [role=main]") || d.body;
                    if (!to)
                        to = Array.from(box.querySelectorAll(sel)).find(usable) || null;
                    if (!to && box !== d.body) {
                        if (!box.hasAttribute("tabindex"))
                            box.setAttribute("tabindex", "-1");
                        to = box;
                    }
                    if (to)
                        to.focus({ preventScroll: true });
                }
            }
            catch {
            }
        }

        /**
         * Remember the last focused element of the framed page (Focus returns there, never to its body). Called for each page shown.
         * @param {Window} w The page's window.
         */
        TrackFocus(w) {
            const f = this.Frame;
            try {
                if (!f || !w || w.SwShellFocusTrack)
                    return;
                w.SwShellFocusTrack = true;
                w.document.addEventListener("focusin", ev => {
                    const e = ev.target;
                    if (e && e.nodeType === 1 && e !== w.document.body && !(e.closest && e.closest(".sw-toasts, .sw-toast")) && typeof WeakRef === "function")
                        f.SwLastFocus = new WeakRef(e);
                }, true);
            }
            catch {
                //  Another origin
            }
        }
    }

    Object.assign(Shell, {
        ContentHost,
        EncodeHash,
        DecodeHash,
    });
})();
