/**
 * @file core/page.js - the page protocol (loading, navigation, embedding) without any visuals.
 *
 * Purpose: `Sw.Page`
 *  - IsTop (top window, a frame whose top is cross-origin, or a frame started with the url flag "top=true": its same-origin
 *    host page does not load the SysWeaver core, so the frame runs its own session manager), IsEmbedded (window !== top).
 *  - Loading(opts) -> remover(noEvents): the old AddLoading contract without the visual. Hooks registered with
 *    OnLoading(fn) (the ui library adds the overlay) are called with the options and may return a function that
 *    removes their visual. With the url flag "noloader" no hook is called. The remover (runs once) removes the
 *    visuals and, unless noEvents, calls Loaded().
 *  - Loaded(): SignalLoaded() (posts "PageLoaded" to the parent / opener and "LoaderRemoved" to the top window,
 *    the app shell reveals the content frame on it), posts the InterOp message "WindowLoaded" and restores the
 *    scroll position saved in the page state (ScrollX/ScrollY) until the document height is stable for 1.5 s or
 *    the user interacted.
 *  - Main(fn, opts): run fn once the DOM is ready and the app script ran (app/app.js, core/init.js; optionally inside Loading), errors are logged and emitted as
 *    Sw.Events "page.error"; a refused request (Sw.Access.IsDenied: 403) shows the shared "not allowed" state
 *    (ShowNotAllowed, core/access.js) instead of leaving the page blank (opts.NotAllowed false: not).
 *  - HasLoaded: true once Loaded ran.
 *  - Navigate(url, target) (the old Open): "_self" with a relative / same-origin url first posts
 *    "IframeNavigating" {Url: absolute, Id} to the top window (the shell may take over). The app shell answers
 *    "IframeNavigationHandled" {Id, Url, Handled}: Handled true = the shell replaces the frame (no own navigation), false =
 *    the shell refused the url (the page navigates itself at once). Without an answer the page navigates itself after
 *    100 ms (1 s inside the app shell, which always answers); other targets open window.open(AppendTheme(url),
 *    target). The default target is "_self" (a new tab only with "_blank"; the old default was "_blank", the old Open keeps it). Other sites never replace this,
 *    the parent or the top window (every target but "_blank", named targets too, opens a new tab with noopener; an
 *    external Embedded menu item of the shell is shown in the shell). The url is String()-converted first; script and
 *    data urls are refused for every target ("_blank" too), a same-origin blob only for "_blank", and other non http(s)
 *    schemes but mailto/tel/sms/callto always. Target keywords are matched case-insensitively ("_TOP" = "_top").
 *  - EnsureHeading() (called by Loaded): a page without an h1 gets a visually hidden h1 with the page name (the shell's
 *    menu name when embedded, else the document title), removed when the page adds its own h1 and added again when
 *    the page removes it (opt out: Sw.Page.AutoHeading = false or data-sw-no-heading on html / body).
 *  - LinkText(text, max), LinkCaption(), SetLinkTitle(text) / TitleFromLink: untrusted url text (a title parameter) as
 *    clean, limited plain text; a title set by SetLinkTitle is shown by the app shell and the fallback heading as plain
 *    text, cut to 80 characters, without a caption (LinkCaption is only for text media from the url, not for titles).
 *  - NameFromUrl(url) ("../Api/debug/ServicesTable" -> "Services") and SetName(name) (document title unless a link title is set):
 *    name a page whose data never arrived (refused, not found, failed) instead of its generic file title ("Table").
 *  - ShowNotAllowed / ShowNotFound (core/access.js): the shared "not allowed" and "not found" states.
 *  - AppName(): the application display name for pages that are not server templates (the shell title, else the name the
 *    shell remembered, RememberAppName; no request).
 *  - Announce(text, opts): read a text to screen readers (visually hidden polite live region, assertive with opts.Assertive,
 *    cleared after about 7 s), ex: next to a toast. The region sits inside the top open modal dialog (AnnounceHost), never in the
 *    inert background behind it.
 *  - SetDirty(key, text, opts) / ClearDirty(key) / IsDirty() / DirtyTexts() / ConfirmLeave(texts): unsaved edits (and work that
 *    stops with the page) per key; while dirty, closing or reloading the tab shows the browser's "Leave site?" prompt
 *    (beforeunload) and the app shell asks "Leave without saving?" before an in-app page change (menu, links, Navigate, Back).
 *    The state is posted to the top window as "SwPageDirty" {Dirty} (the shell marks the tab title). Work entries say what really
 *    happens by their kind (WorkText(name, kind): "server" = the server still does it, only the result is not seen; "export" /
 *    "download" = no file; "upload"; "save"; "local" = it stops); the open question follows its entries (ConfirmLeave watches
 *    them: it updates its text and closes by itself, as Stay, when the work ended and nothing else is lost).
 *  - Replace(url): location.replace (a redirect without a history entry and without IframeNavigating).
 *  - Reload(flushCache, clearHash), Edit / View / EditOrOpen (open the registered editor / viewer page for an
 *    url via Api/GetExtensionEditor / Api/GetExtensionViewer, resolved against the site root).
 *  - DidInteract + "page.interacted" / body "UserInteracted" (first mousedown / keydown / touchstart),
 *    scroll position saving while scrolling (debounced), on pagehide and on beforeunload (installed by Sw.Init through
 *    TrackInteraction / TrackScroll), so Back in the app shell (which removes the frame without beforeunload) restores it.
 *
 * Load order: after core/session_start.js. Dependencies: Sw.Url, Sw.Frame, Sw.InterOp, Sw.History, Sw.Net, Sw.Events, Sw.Session (IsTop, ReloadNow).
 * Globals defined: none (Sw.Page).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** @type {Array<function(object):(function():void|void)>} Loading hooks (visuals). */
    const loadingHooks = [];

    /** @type {boolean} True once the user interacted with the page. */
    let didInteract = false;

    /** @type {boolean} True once interaction tracking is installed. */
    let trackingInteraction = false;

    /** @type {boolean} True once scroll saving is installed. */
    let trackingScroll = false;

    /** @type {boolean} True while restoreScroll moves the page (its scroll events are not saved). */
    let restoringScroll = false;

    /**
     * @returns {boolean} true if this is the top window, the top is cross-origin, or the url flag "top=true" is set (a frame of a
     * same-origin host page without the SysWeaver core acts as a top window): the rule of the session manager (Sw.Session.IsTop).
     */
    function computeIsTop() {
        return Sw.Session.IsTop;
    }

    /**
     * Restore the scroll position saved in the page state; retries every frame until the document height has
     * been stable for 1.5 s or the user interacted.
     */
    function restoreScroll() {
        const scroll = {
            left: Sw.History.Get("ScrollX", -1),
            top: Sw.History.Get("ScrollY", -1),
            behavior: "instant",
        };
        if (!((scroll.left > 0) || (scroll.top > 0)))
            return;
        const de = document.documentElement;
        let lastChanged = performance.now();
        let lastHeight = de.scrollHeight;
        //  The scroll events of the restore itself must not save a partial position (TrackScroll)
        restoringScroll = true;
        /** One restore attempt. */
        const tryScroll = () => {
            if (didInteract) {
                restoringScroll = false;
                return;
            }
            window.scrollTo(scroll);
            requestAnimationFrame(() => {
                if (didInteract) {
                    restoringScroll = false;
                    return;
                }
                const nh = de.scrollHeight;
                const pn = performance.now();
                if (nh !== lastHeight) {
                    lastChanged = pn;
                    lastHeight = nh;
                }
                if ((pn - lastChanged) > 1500) {
                    restoringScroll = false;
                    return;
                }
                requestAnimationFrame(tryScroll);
            });
        };
        tryScroll();
    }

    /**
     * Call the api of the extension editor / viewer lookup.
     * @param {string} name - "GetExtensionEditor" or "GetExtensionViewer".
     * @param {*} arg - Argument.
     * @returns {Promise<string|null>} Page url (root relative or absolute) or null.
     */
    function lookup(name, arg) {
        return Sw.Net.Send(Sw.Url.Root + "Api/" + name, arg);
    }

    /**
     * Open an editor / viewer page url returned by the server (absolute: new window, root relative: this window).
     * @param {string} u - Url.
     */
    function openTool(u) {
        if (Sw.Url.IsAbsolute(u))
            Sw.Page.Navigate(u, "_blank");
        else
            Sw.Page.Navigate(Sw.Url.Root + u, "_self");
    }

    /** @type {{polite: HTMLElement|null, assertive: HTMLElement|null}} The visually hidden live regions of Announce. */
    const liveRegions = { polite: null, assertive: null };

    /** @type {{polite: number, assertive: number}} Timers of the pending Announce text (or of clearing the announced text). */
    const liveTimers = { polite: 0, assertive: 0 };

    /**
     * The element that holds the Announce live regions: Sw.Page.AnnounceHost() when set (the ui library may name the top dialog), else
     * the last open modal ([aria-modal="true"], shown, not inside an inert subtree), else the body. A live region in the background
     * of a modal dialog is inert (not read), so it moves into the dialog while one is open.
     * @returns {HTMLElement|null} The host.
     */
    function liveHost() {
        const h = Sw.Page.AnnounceHost;
        if (typeof h === "function") {
            try {
                const e = h();
                if (e && e.isConnected)
                    return e;
            }
            catch {
                //  Fall back to the search below
            }
        }
        const ms = document.querySelectorAll('[aria-modal="true"]');
        for (let i = ms.length - 1; i >= 0; --i) {
            const m = ms[i];
            if (!m.hidden && !m.closest("[inert]") && m.getClientRects().length > 0)
                return m;
        }
        return document.body;
    }

    /** @type {Map<string, {Text: string, Work: boolean}>} The dirty keys of this window (SetDirty). */
    const dirtyKeys = new Map();

    /** @type {function(BeforeUnloadEvent):void|null} The beforeunload guard while dirty. */
    let unloadGuard = null;

    /** Install / remove the beforeunload guard, emit "page.dirty" and tell the top window (the shell marks its title). */
    function dirtyChanged() {
        const on = dirtyKeys.size > 0;
        if (on && !unloadGuard) {
            unloadGuard = ev => {
                if (!dirtyKeys.size)
                    return;
                ev.preventDefault();
                //  Older browsers need a return value to show the prompt (its text is never shown)
                ev.returnValue = "";
            };
            /* SwWeb:allow ux-saf-10 (this is the SetDirty guard itself) */
            window.addEventListener("beforeunload", unloadGuard);
        }
        else if (!on && unloadGuard) {
            window.removeEventListener("beforeunload", unloadGuard);
            unloadGuard = null;
        }
        Sw.Events.Emit("page.dirty", { Dirty: on, Texts: Sw.Page.DirtyTexts(false) });
        try {
            if (window.top !== window)
                Sw.Frame.PostTop("SwPageDirty", { Dirty: on });
        }
        catch {
            //  No frame protocol (cross-origin top): the shell asks the frame when it needs to
        }
    }

    /** @type {number} Milliseconds an announced text stays in the live region before it is cleared. */
    const liveClearMs = 7000;

    /** @type {HTMLElement|null} The visually hidden fallback h1 inserted by EnsureHeading. */
    let autoHeading = null;

    /** @type {Promise<string>|null} The memoized application name (Sw.Page.AppName). */
    let appNameP = null;

    /** @type {MutationObserver|null} Watches the document title for the fallback heading. */
    let titleObserver = null;

    /** @type {MutationObserver|null} Watches the body for a page h1 (added: drop the fallback, removed: re-check). */
    let bodyObserver = null;

    /** @type {string} Selector of a level 1 heading. */
    const h1Selector = "h1, [role=heading][aria-level='1']";

    /**
     * The human name of this page for the fallback heading: the shell's menu name when shown in the app shell (the name the
     * shell header shows), else the document title (first letter upper cased, ex: "icons" -> "Icons").
     * @returns {string} The name ("" for none).
     */
    function pageName() {
        const app = shellApp();
        if (app && typeof app.FindEntry === "function") {
            try {
                const en = app.FindEntry(window.location.href, window.frameElement);
                const n = en && en.Item ? ("" + (en.Item.Name || "")).trim() : "";
                if (n)
                    return n;
            }
            catch {
                //  Fall back to the document title
            }
        }
        const t = (document.title || "").trim();
        //  A title from the url (SetLinkTitle) is plain, limited text: used as it is (no caption, owner OWN3-Shell-2)
        if (t && t === Sw.Page.TitleFromLink)
            return t;
        return t ? t.charAt(0).toUpperCase() + t.substring(1) : "";
    }

    /**
     * Control and invisible formatting characters removed from text of the url (C0 / C1 controls, the bidi marks, embeddings,
     * overrides and isolates, zero width characters): they could reorder or hide parts of the shown text.
     * @type {RegExp}
     */
    const linkTextStrip = /[\u0000-\u001f\u007f-\u009f\u200b-\u200f\u202a-\u202e\u2060-\u2069\ufeff]/g;

    /** Remove the fallback heading (the body stays watched, so it comes back when the page removes its own h1 again). */
    function dropAutoHeading() {
        if (autoHeading) {
            autoHeading.remove();
            autoHeading = null;
        }
    }

    /**
     * The HTML window target keywords (browsers match them ASCII case-insensitively, so "_TOP" is "_top").
     * @type {RegExp}
     */
    const targetKeyword = /^_(self|blank|top|parent)$/i;

    /**
     * The app shell this page is shown in (a same-origin top window with SysWeaverApp, this page in its content frame).
     * @returns {object|null} The shell (Sw.Shell.App instance) or null.
     */
    function shellApp() {
        try {
            if (window.top === window || window.parent !== window.top)
                return null;
            const app = window.top.SysWeaverApp;
            return app && typeof app.SafeContentUrl === "function" ? app : null;
        }
        catch {
            return null;
        }
    }

    /** @type {number} Counter of the IframeNavigating ids of this window. */
    let navId = 0;

    /**
     * Navigate this window to an url of this site through the frame protocol: post "IframeNavigating" {Url, Id} to the
     * parent window; the app shell answers "IframeNavigationHandled" {Id, Url, Handled}. Handled: the shell replaces the content
     * frame, nothing else to do; refused (Handled false): navigate at once; no answer: navigate after 100 ms (1 s inside the
     * app shell, which always answers, so a slow shell does not cause a double load).
     * @param {string} abs - Absolute url of this site.
     */
    function navigateSelf(abs) {
        const id = interopIdText() + "." + (++navId);
        let done = false;
        let timer = 0;
        /** Navigate this window (once). */
        const go = () => {
            if (done)
                return;
            done = true;
            off();
            clearTimeout(timer);
            window.open(Sw.Url.AppendTheme(abs), "_self");
        };
        const off = Sw.Frame.On("IframeNavigationHandled", (d, ev) => {
            if (done || ev.source !== window.parent || d.Id !== id)
                return;
            if (d.Handled === false) {
                go();
                return;
            }
            done = true;
            off();
            clearTimeout(timer);
        });
        //  (to the parent, not the top window, as upstream's Open: the page that hosts this one handles it, ex: the app shell for its
        //  content frame; a frame nested deeper, ex: a pop-up frame in an embedded page, gets no answer and navigates itself)
        if (window.parent !== window)
            Sw.Frame.PostParent("IframeNavigating", { Url: abs, Id: id });
        timer = setTimeout(go, shellApp() ? 1000 : 100);
    }

    /**
     * This window's InterOp id as text (an IframeNavigating id prefix).
     * @returns {string}
     */
    function interopIdText() {
        return Sw.InterOp ? String(Sw.InterOp.Id) : String(Date.now());
    }

    /**
     * Schemes handed to an external program (they never load content into a window); allowed for every target.
     * @type {RegExp}
     */
    const externalSchemes = /^(mailto|tel|sms|callto):$/;

    /**
     * The origin of a "blob:" url (of the url inside it), null when it has none.
     * @param {URL} u - A parsed "blob:" url.
     * @returns {string|null}
     */
    function blobOrigin(u) {
        try {
            const o = new URL(u.pathname).origin;
            return o && o !== "null" ? o : null;
        }
        catch {
            return null;
        }
    }

    /**
     * Tell the user that Navigate refused an url: console warning, warning toast (when the ui library is loaded) and a
     * screen reader announcement (Announce).
     */
    function refusedLink() {
        console.warn("Sw.Page.Navigate: refused an url that can't be opened here");
        const text = _TF("This link can't be opened here. Links must point to a page of this site.",
            "Message shown when a page in the application tried to open a link (address) that is not on this site, which is not allowed");
        if (typeof Sw.Toast === "function")
            Sw.Toast(text, { Kind: "warning", Duration: 8000 });
        Sw.Page.Announce(text);
    }

    /**
     * @namespace Sw.Page - page protocol.
     */
    Sw.Page = {

        /** @type {boolean} True for the top window, or a frame whose top window is cross-origin. */
        IsTop: computeIsTop(),

        /** @type {boolean} True if this window is inside a frame. */
        IsEmbedded: window.top !== window.self,

        /** @type {boolean} True once Loaded ran (the page signalled that it is ready). */
        HasLoaded: false,

        /** @type {boolean} True once the user interacted with the page (stops the scroll restore). */
        get DidInteract() {
            return didInteract;
        },

        /**
         * Register a loading hook (used by the ui library to show the loading overlay).
         * @param {function({Element:HTMLElement|null, Text:string, Opaque:boolean, Icon:string}):(function():void|void)} fn -
         * Called by Loading; may return a function that removes its visual.
         * @returns {function():void} Removes the hook.
         */
        OnLoading(fn) {
            loadingHooks.push(fn);
            return () => {
                const i = loadingHooks.indexOf(fn);
                if (i >= 0)
                    loadingHooks.splice(i, 1);
            };
        },

        /**
         * Start a loading phase (see the file header).
         * @param {{Element:HTMLElement, Text:string, Opaque:boolean, Icon:string}} [opts] - Options for the visual:
         * Element (default body), Text, Opaque, Icon (icon class, "" = none).
         * @returns {function(boolean):void} Remover: call when loaded (true = do not signal / restore scroll).
         */
        Loading(opts) {
            opts = opts || {};
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Page.Loading", opts);
            const removers = [];
            if (!Sw.Url.Has("noloader")) {
                loadingHooks.slice().forEach(h => {
                    try {
                        const r = h(opts);
                        if (typeof r === "function")
                            removers.push(r);
                    }
                    catch (e) {
                        console.error("Sw.Page: loading hook failed: " + e.message);
                    }
                });
            }
            Sw.Events.Emit("page.loading", opts);
            let done = false;
            return noEvents => {
                if (done)
                    return;
                done = true;
                removers.forEach(r => {
                    try {
                        r();
                    }
                    catch (e) {
                        console.error("Sw.Page: loading remover failed: " + e.message);
                    }
                });
                if (!noEvents)
                    Sw.Page.Loaded();
            };
        },

        /**
         * Signal to the hosting windows that the page is ready: "PageLoaded" to the parent (or opener) and
         * "LoaderRemoved" to the top window (exactly the old PageLoaded).
         */
        SignalLoaded() {
            //  Same-origin hosts only (Sw.Frame default): a foreign window framing this page learns nothing
            Sw.Frame.PostParent("PageLoaded");
            Sw.Frame.PostTop("LoaderRemoved");
        },

        /**
         * Announce a text to screen readers through a visually hidden polite live region (created on first use). Use it
         * next to a toast for messages that matter (a refused link, a failed page change): a newly inserted toast is not
         * reliably read. Repeated calls within 100 ms announce only the last text; the same text twice is announced again.
         * The text is removed from the region after about 7 s, so no stale message stays at the end of the page.
         * @param {string} text - Text to announce.
         */
        Announce(text, opts) {
            if (opts && Sw.DevOptions)
                Sw.DevOptions("Sw.Page.Announce", opts);
            if (!text || !document.body)
                return;
            const kind = opts && opts.Assertive ? "assertive" : "polite";
            const host = liveHost() || document.body;
            let r = liveRegions[kind];
            if (!r) {
                r = document.createElement("div");
                r.className = "sw-sr-only sw-page-live";
                r.setAttribute("aria-live", kind);
                r.setAttribute("aria-atomic", "true");
                if (kind === "assertive")
                    r.setAttribute("role", "alert");
                //  Inline too: core pages may run without the ui css (.sw-sr-only)
                r.style.cssText = "position:absolute;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;border:0";
                liveRegions[kind] = r;
            }
            //  Inside the top open modal (the background is inert there), else at the end of the body
            if (r.parentNode !== host || !r.isConnected)
                host.append(r);
            r.textContent = "";
            clearTimeout(liveTimers[kind]);
            //  Set the text after a tick, so a live region that was just created or moved (or the same text again) is announced,
            //  then clear it (a screen reader user browsing the page later must not find a stale message)
            liveTimers[kind] = setTimeout(() => {
                //  The dialog may have closed during the tick: move to the current host first
                const h = liveHost() || document.body;
                if (r.parentNode !== h)
                    h.append(r);
                r.textContent = String(text);
                liveTimers[kind] = setTimeout(() => r.textContent = "", liveClearMs);
            }, 100);
        },

        /**
         * @type {(function():(HTMLElement|null))|null} Optional: returns the element that holds the Announce live regions (ex: the
         * ui library's top dialog). Without it (or when it returns null) the last open [aria-modal="true"] element is used, else the body.
         */
        AnnounceHost: null,

        /**
         * Mark (or clear) unsaved edits of this page (UX guide SAF-10). While any key is dirty, closing or reloading the tab shows the
         * browser's "Leave site?" prompt and the app shell asks "Leave without saving?" ("Leave" / "Stay") before an in-app page change
         * (menu, links, Sw.Page.Navigate, Back / Forward). Clear the key after a save (a dirty form that saved is clean again).
         * @param {string} key - What is dirty (ex: "editor", "form:user"; one page may have several).
         * @param {string|boolean|null} [text] - The sentence that says what is lost, ex: "Your changes to appsettings.json are lost." (translated by the caller); true
         * or left out for a generic sentence; null / false / "" clears the key (or call ClearDirty).
         * @param {{Work: boolean, Draft: boolean, Name: string, Kind: string}} [opts] - Work: work that runs on the page (ex: an export),
         * text ex: "The export stops."; the question is then "Leave this page?". With Name (what runs, ex: "Kill devenv (33936)") and
         * Kind ("server", "export", "download", "upload", "save", "local") and text true, the sentence is WorkText(Name, Kind): it says
         * what really happens (a server-side action is not stopped by leaving). Draft: the text is kept as a draft (Sw.Draft), nothing
         * is lost: the question is then "Leave this page?" with a normal Leave.
         */
        SetDirty(key, text, opts) {
            if (opts && Sw.DevOptions)
                Sw.DevOptions("Sw.Page.SetDirty", opts);
            key = String(key || "page");
            //  SetDirty("form") (no text) marks it dirty with the generic sentence; only an explicit null / false / "" clears
            if (text === undefined)
                text = true;
            if (!text) {
                Sw.Page.ClearDirty(key);
                return;
            }
            if (text === true && opts && opts.Work && opts.Name)
                text = Sw.Page.WorkText(opts.Name, opts.Kind);
            const t = text === true ? "" : String(text);
            const w = !!(opts && opts.Work);
            const d = !!(opts && opts.Draft);
            const old = dirtyKeys.get(key);
            if (old && old.Text === t && old.Work === w && old.Draft === d)
                return;
            dirtyKeys.set(key, { Text: t, Work: w, Draft: d });
            dirtyChanged();
        },

        /**
         * Clear a dirty key (SetDirty), or every key of this window when called without one.
         * @param {string} [key] - The key.
         */
        ClearDirty(key) {
            const had = dirtyKeys.size;
            if (key === undefined || key === null)
                dirtyKeys.clear();
            else
                dirtyKeys.delete(String(key));
            if (dirtyKeys.size !== had)
                dirtyChanged();
        },

        /**
         * Test for unsaved edits (SetDirty) in this window or (deep, default) in a same-origin frame inside it.
         * @param {boolean} [deep=true] - Include nested frames.
         * @returns {boolean} True when dirty.
         */
        IsDirty(deep) {
            return Sw.Page.DirtyTexts(deep).length > 0;
        },

        /**
         * The dirty entries of this window and (deep, default) of its same-origin nested frames that run the Sw core.
         * @param {boolean} [deep=true] - Include nested frames.
         * @returns {Array<{Text: string, Work: boolean}>} The entries (Text "" = the generic sentence).
         */
        DirtyTexts(deep) {
            const list = Array.from(dirtyKeys.values()).map(x => ({ Text: x.Text, Work: x.Work, Draft: !!x.Draft }));
            if (deep === false)
                return list;
            for (let i = 0; i < window.frames.length; ++i) {
                try {
                    const p = window.frames[i].Sw && window.frames[i].Sw.Page;
                    if (p && typeof p.DirtyTexts === "function")
                        list.push(...p.DirtyTexts(true));
                }
                catch {
                    //  Another origin: not known
                }
            }
            return list;
        },

        /**
         * The sentence of the leave question for work that runs on the page, by what really happens when the page is left (SAF-10:
         * name the work and its true consequence).
         * @param {string} name - What runs, ex: "Kill devenv (33936)", "Exporting report.xlsx".
         * @param {string} [kind] - "server" (the server does it: leaving only loses the result), "export" / "download" (the file is not
         * saved), "upload" (the file is not uploaded), "save" (the save may not finish), "local" (the work in the page stops: only when
         * the caller says so); none / unknown: "<name> may not finish." (never "stops": it may already be on the server).
         * @returns {string} The sentence (translated).
         */
        WorkText(name, kind) {
            name = String(name || "").trim() || _TF("The work", "Name of running work in the leave question when the page gave it no name");
            switch (String(kind || "").toLowerCase()) {
                case "server":
                    return _T("You won't see the result of {0} here; the service may still finish it.", name, "Text of the question 'Leave this page?' while an action that the service does is still running (leaving does not stop it).{0} is replaced with the action, ex: Kill devenv (33936)");
                case "export":
                case "download":
                    return _T("{0} stops; the file is not saved.", name, "Text of the question 'Leave this page?' while an export or download runs.{0} is replaced with the work, ex: Exporting report.xlsx");
                case "upload":
                    return _T("{0} stops; the file is not uploaded.", name, "Text of the question 'Leave this page?' while an upload runs.{0} is replaced with the work, ex: Uploading report.xlsx");
                case "save":
                    return _T("{0} may not finish; your changes may not be saved.", name, "Text of the question 'Leave this page?' while a save runs.{0} is replaced with the work, ex: Saving the settings");
                case "local":
                    return _T("{0} stops.", name, "Text of the question 'Leave this page?' for work that runs in the page and stops when it is left.{0} is replaced with the work, ex: Exporting report.xlsx");
                default:
                    //  Not known (no Kind: the work may already be on the server): never claim that it stops (SAF-10, TXT-25)
                    return _T("{0} may not finish.", name, "Text of the question 'Leave this page?' while work runs whose kind is not known (it may already be on the server).{0} is replaced with the work, ex: Kill devenv (33936)");
            }
        },

        /**
         * Ask "Leave without saving?" (or "Leave this page?" when only work stops) with the given dirty entries: "Leave" (a danger
         * button) and "Stay" (focused; every other way to close the question means Stay). Uses Sw.Dialog when the ui library is
         * loaded, else the browser's confirm. While it is open it follows its entries (opts.Source, by default DirtyTexts() when no
         * texts are given): the text is updated, and when the work ended and nothing else is lost the question closes by itself as Stay
         * (the page shows the result; leave again with one click).
         * @param {Array<{Text: string, Work: boolean}>} [texts] - The entries (default: DirtyTexts()).
         * @param {{Source: function():Array<{Text: string, Work: boolean}>}} [opts] - Source: returns the current entries.
         * @returns {Promise<boolean>} True to leave (also when nothing is dirty).
         */
        async ConfirmLeave(texts, opts) {
            if (opts && Sw.DevOptions)
                Sw.DevOptions("Sw.Page.ConfirmLeave", opts);
            const source = (opts && typeof opts.Source === "function") ? opts.Source : (texts ? null : () => Sw.Page.DirtyTexts());
            texts = texts || Sw.Page.DirtyTexts();
            if (!texts.length)
                return true;
            const generic = _TF("Your unsaved changes on this page are lost.", "Text of the question when the user leaves a page with unsaved changes (the page did not say which changes)");
            const work = _TF("Work that runs on this page may not finish.", "Text of the question when the user leaves a page while work started on it is still running (the page did not say which work; it may already be on the server)");
            /**
             * What the question says for entries.
             * @param {Array<{Text: string, Work: boolean, Draft: boolean}>} list The entries.
             * @returns {{Lost: number, Work: number, Lines: string[]}} The counts and the sentences.
             */
            const describe = list => {
                //  Edits that are lost (not drafts, not work); drafted text is kept, so it is only mentioned when nothing is lost (a
                //  page's own "… are lost." sentence and "… kept as a draft" would contradict each other)
                const lostL = list.filter(x => !x.Work && !x.Draft);
                const shownL = lostL.length ? list.filter(x => !x.Draft) : list;
                const ls = [];
                for (const x of shownL) {
                    const s = x.Text || (x.Work ? work : generic);
                    if (s && ls.indexOf(s) < 0)
                        ls.push(s);
                }
                return { Lost: lostL.length, Work: shownL.filter(x => x.Work).length, Lines: ls };
            };
            const first = describe(texts);
            const lines = first.Lines;
            const title = first.Lost > 0
                ? _TF("Leave without saving?", "Title of the question when the user leaves a page (menu, link, Back) that has unsaved changes")
                : _TF("Leave this page?", "Title of the question when the user leaves a page while work started on it is still running (it stops when the page is left)");
            const leave = _TF("Leave", "Button of the question 'Leave without saving?': leave the page, the unsaved changes are lost");
            const stay = _TF("Stay", "Button of the question 'Leave without saving?': stay on the page (keep the changes)");
            //  Leave is a danger button only when something is lost or stops (a drafted text is kept)
            const danger = first.Lost > 0 || first.Work > 0;
            if (Sw.Dialog && typeof Sw.Dialog.Open === "function" && Sw.el) {
                //  One dialog for both cases, so its text can follow the work (an export that finished while the question is open)
                const textE = Sw.el("div.sw-dialog-text", { text: lines.join("\n") });
                const dlg = Sw.Dialog.Open({
                    Title: title, Icon: danger ? "warning" : undefined, Role: "alertdialog", Id: "sw.leave",
                    Content: textE,
                    Buttons: [
                        { Text: stay, Value: false, Class: "sw-dialog-cancel" },
                        { Text: leave, Kind: danger ? "danger" : "primary", Value: true, Class: "sw-dialog-ok" },
                    ],
                    Focus: ".sw-dialog-cancel",
                    AutoDirty: false,
                });
                let watch = 0;
                if (source && first.Work > 0) {
                    let shown = lines.join("\n");
                    watch = setInterval(() => {
                        let now;
                        try {
                            now = describe(source() || []);
                        }
                        catch {
                            return;
                        }
                        if (now.Work === 0 && now.Lost === 0) {
                            //  The work ended and nothing else is lost: the reason to ask is gone; stay and let the page show the result
                            clearInterval(watch);
                            watch = 0;
                            dlg.Close(false);
                            Sw.Page.Announce(_TF("The work has finished. Nothing stops when you leave now.", "Announced when the question 'Leave this page?' closes by itself because the running work ended"));
                            return;
                        }
                        const s = now.Lines.join("\n");
                        if (s !== shown) {
                            shown = s;
                            textE.textContent = s;
                        }
                    }, 400);
                }
                try {
                    return (await dlg.Result) === true;
                }
                finally {
                    if (watch)
                        clearInterval(watch);
                }
            }
            if (Sw.Dialog && typeof Sw.Dialog.Confirm === "function")
                return await Sw.Dialog.Confirm(title, lines.join("\n"), leave, { Danger: danger, CancelText: stay });
            return window.confirm(title + "\n\n" + lines.join("\n"));
        },

        /**
         * The page finished loading: SignalLoaded, InterOp "WindowLoaded", scroll restore, Sw.Events "page.loaded".
         */
        Loaded() {
            Sw.Page.HasLoaded = true;
            Sw.Page.SignalLoaded();
            Sw.InterOp.Post("WindowLoaded");
            restoreScroll();
            Sw.Page.EnsureHeading();
            Sw.Events.Emit("page.loaded");
        },

        /**
         * Make untrusted text from the url (a title or name parameter) safe to show as plain text: String()-converted, control and
         * bidi / invisible formatting characters removed, white space collapsed, cut to max characters (with "…").
         * @param {*} text - The text (null / undefined give "").
         * @param {number} [max=80] - Maximum length in characters.
         * @returns {string} The cleaned text.
         */
        LinkText(text, max) {
            if (text === null || typeof text === "undefined")
                return "";
            max = max > 1 ? Math.floor(max) : 80;
            let s = String(text).replace(linkTextStrip, " ").replace(/\s+/g, " ").trim();
            const chars = [...s];
            if (chars.length > max)
                s = chars.slice(0, max - 1).join("").trimEnd() + "\u2026";
            return s;
        },

        /**
         * The caption that marks text from the url ("Text from the link"), so it can't pass as a message of the application.
         * Used for text media shown as content (the media viewer), never for titles (a title from the url is plain, limited text).
         * @returns {string} The translated caption.
         */
        LinkCaption() {
            return _TF("Text from the link", "Caption on the media viewer inside the application when it shows a text media: the text comes from the page address (a link), not from the application");
        },

        /**
         * @type {string|null} The document title when it was set from the url by SetLinkTitle (null otherwise). The app shell
         * shows such a title as plain text cut to 80 characters (LinkText), like any other title.
         */
        TitleFromLink: null,

        /**
         * Set the document title from untrusted url text (ex: the `n` parameter of a table page): cleaned and limited by
         * LinkText(text, 80) (plain text, never html) and remembered in TitleFromLink, so the app shell keeps it limited too.
         * A title confirmed later (ex: the server knows the api / type of the url): set TitleFromLink = null, then
         * document.title (a different title alone does that too).
         * @param {*} text - The text from the url.
         * @returns {string} The title that was set ("" sets nothing).
         */
        SetLinkTitle(text) {
            const s = Sw.Page.LinkText(text, 80);
            if (!s)
                return "";
            Sw.Page.TitleFromLink = s;
            document.title = s;
            return s;
        },

        /**
         * A name for people from a server method / table api url, for a page that never got its data (refused, not found, failed)
         * and would otherwise keep its generic file title ("Table"): the last path segment without a "Get" prefix and a "Table"
         * suffix, camel case split. "../Api/debug/ServicesTable" -> "Services", "Api/debug/ProcessInfoTable" -> "Process info",
         * "OpenAI/LLM/ModelsTable?x=1" -> "Models".
         * @param {string} url - The url (relative or absolute; the query and hash are ignored).
         * @returns {string} The name ("" for none).
         */
        NameFromUrl(url) {
            let s = String(url || "").split(/[?#]/)[0].replace(/\/+$/, "");
            s = s.substring(s.lastIndexOf("/") + 1);
            try {
                s = decodeURIComponent(s);
            }
            catch {
            }
            s = Sw.Page.LinkText(s, 80).replace(/\.[a-z0-9]+$/i, "");
            const t = s.replace(/^Get(?=[A-Z])/, "").replace(/(?<=.)Table$/, "");
            if (!t)
                return "";
            const words = (Sw.Format && Sw.Format.RemoveCamelCase) ? Sw.Format.RemoveCamelCase(t) : t;
            return words.charAt(0).toUpperCase() + words.substring(1);
        },

        /**
         * Name the page (document title, also used by the app shell when the page has no menu item, and by the fallback heading)
         * when its data never arrived: a title from the link (SetLinkTitle) is kept. Used by ShowNotAllowed / ShowNotFound with a
         * Name, and by pages for their error state.
         * @param {string} name - The page name ("Services", the menu item or table name).
         * @returns {boolean} True when the title was set.
         */
        SetName(name) {
            const s = Sw.Page.LinkText(name, 120);
            if (!s || Sw.Page.TitleFromLink)
                return false;
            document.title = s;
            return true;
        },

        /**
         * The application display name (EnvInfo.AppDisplayName) for pages that are not server templates (ex: app/Welcome.html),
         * known without a request (the original pages asked nothing): the title of the app shell window (its templated
         * index.html, SysWeaverAppTitle) when shown in a same origin shell, else the name the shell remembered for this site
         * (localStorage "SysWeaver.AppName", see RememberAppName), else "". White space is collapsed.
         * @returns {Promise<string>} The name ("" when unknown).
         */
        AppName() {
            if (!appNameP) {
                const clean = v => String(v || "").replace(/\s+/g, " ").trim();
                let name = "";
                try {
                    name = clean(window.top.SysWeaverAppTitle);
                }
                catch {
                    //  Cross-origin top window
                }
                if (!name) {
                    const m = Sw.Storage.GetJson("SysWeaver.AppName", null);
                    name = clean(m && typeof m === "object" ? m[Sw.Url.Root] : "");
                }
                appNameP = Promise.resolve(name);
            }
            return appNameP;
        },

        /**
         * Remember the application display name for the pages of this site opened on their own (see AppName); the shell calls it
         * with its templated title.
         * @param {string} name - The name.
         */
        RememberAppName(name) {
            const v = String(name || "").replace(/\s+/g, " ").trim();
            if (!v)
                return;
            const m = Object.assign({}, Sw.Storage.GetJson("SysWeaver.AppName", null));
            if (m[Sw.Url.Root] === v)
                return;
            m[Sw.Url.Root] = v;
            Sw.Storage.SetJson("SysWeaver.AppName", m);
        },

        /**
         * @type {boolean} Set to false (before Loaded) to opt out of the fallback page heading of EnsureHeading; the
         * attribute data-sw-no-heading on the html or body element does the same.
         */
        AutoHeading: true,

        /**
         * Make sure the page has a heading naming it (screen reader heading navigation): when the document has no h1 (or
         * role heading of level 1), insert a visually hidden h1 with the page name as the first element of the main
         * landmark (else of the body), and keep it in sync with the title. The page name is the menu name the app shell
         * shows when embedded in it, else the document title (also a title from the url, SetLinkTitle: plain, limited text).
         * Removed again as soon as the page adds its own h1, and added again when the page removes its h1 later (the body
         * stays watched once this ran).
         * Called by Loaded; does nothing when opted out (AutoHeading = false / data-sw-no-heading) or without a name.
         */
        EnsureHeading() {
            const d = document;
            if (Sw.Page.AutoHeading === false || !d.body || d.documentElement.hasAttribute("data-sw-no-heading") || d.body.hasAttribute("data-sw-no-heading")) {
                dropAutoHeading();
                return;
            }
            //  Watch the body: drop the fallback as soon as the page adds its own h1 (or sets role=heading level 1 on an
            //  element), re-check when an h1 is removed (or an element's heading role changes) while no fallback is shown
            if (!bodyObserver) {
                bodyObserver = new MutationObserver(recs => {
                    if (autoHeading) {
                        const found = recs.some(r => {
                            if (r.type === "attributes")
                                return r.target !== autoHeading && r.target.matches && r.target.matches(h1Selector);
                            return [...r.addedNodes].some(n => n.nodeType === 1 && n !== autoHeading && (n.matches(h1Selector) || !!n.querySelector(h1Selector)));
                        });
                        if (found)
                            dropAutoHeading();
                        return;
                    }
                    const lost = recs.some(r => {
                        if (r.type === "attributes")
                            return true;
                        return [...r.removedNodes].some(n => n.nodeType === 1 && (n.matches(h1Selector) || !!n.querySelector(h1Selector)));
                    });
                    if (lost)
                        Sw.Page.EnsureHeading();
                });
                bodyObserver.observe(d.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["role", "aria-level"] });
            }
            const own = [...d.querySelectorAll(h1Selector)].some(e => e !== autoHeading);
            if (own) {
                dropAutoHeading();
                return;
            }
            const text = pageName();
            if (!text)
                return;
            if (!autoHeading || !autoHeading.isConnected) {
                autoHeading = d.createElement("h1");
                autoHeading.className = "sw-sr-only sw-page-heading";
                //  Inline too: core pages may run without the ui css (.sw-sr-only)
                autoHeading.style.cssText = "position:absolute;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;border:0";
                const main = d.querySelector("main, [role=main]");
                (main || d.body).prepend(autoHeading);
            }
            autoHeading.textContent = text;
            //  Follow later title changes (pages often set the title from loaded data)
            if (!titleObserver && d.head) {
                titleObserver = new MutationObserver(() => {
                    if (autoHeading)
                        Sw.Page.EnsureHeading();
                });
                titleObserver.observe(d.head, { childList: true, subtree: true, characterData: true });
            }
        },

        /**
         * Run the page's main function once the DOM is ready.
         * @param {function():*} fn - Main function (may be async).
         * A refused request (Sw.Access.IsDenied, ex: 403) thrown by fn shows the shared "not allowed" state (Sw.Page.ShowNotAllowed).
         * @param {{Loading:(boolean|string|object), NotAllowed:boolean}} [opts] - Loading: show a loading phase while fn runs (true, a
         * text, or Loading options); NotAllowed: false = don't show the "not allowed" state for a refused request.
         * @returns {Promise<*>} The result of fn (undefined on error).
         */
        async Main(fn, opts) {
            opts = opts || {};
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Page.Main", opts);
            if (document.readyState === "loading")
                await new Promise(r => document.addEventListener("DOMContentLoaded", r, { once: true }));
            //  the app script (app/app.js: custom formats, plug-ins) runs first, so the page can use what it registers (core/init.js)
            if (Sw.Page.AppScript)
                await Promise.race([Sw.Page.AppScript, new Promise(r => setTimeout(r, 5000))]);
            let remove = null;
            const lo = opts.Loading;
            if (lo)
                remove = Sw.Page.Loading(typeof lo === "object" ? lo : (typeof lo === "string" ? { Text: lo } : {}));
            try {
                return await fn();
            }
            catch (e) {
                //  A refused request (403) is no failure of the page: show the shared "not allowed" state instead of a blank page
                if (Sw.Access && Sw.Access.IsDenied(e) && Sw.Page.ShowNotAllowed && opts.NotAllowed !== false) {
                    console.warn("Page main: not allowed: " + (e && e.message ? e.message : e));
                    Sw.Page.ShowNotAllowed({ Error: e });
                }
                else
                    console.error("Page main failed: " + (e && e.stack ? e.stack : e));
                Sw.Events.Emit("page.error", { Error: e });
                return undefined;
            }
            finally {
                if (remove)
                    remove();
            }
        },

        /**
         * Open an url (the old Open; the forced theme is appended to urls of this site). The url often comes from an url
         * parameter or table data, so it is untrusted:
         *  - Urls of this site (http(s), Sw.Url.SafeSameOrigin) open in the target. For "_self" the url is announced to the
         *    app shell first ("IframeNavigating", same origin only), so it adds a history entry. Inside the app shell a raw
         *    server method of this site (not Sw.Url.IsPageUrl, ex: Api/... or ServerManager/KillProcess) never replaces the
         *    content frame or the shell: with any target but "_blank" it opens in a new tab (noopener).
         *  - Other sites never replace this window, its parent or the top window (the app shell): with any target but
         *    "_blank" ("_self", "_top", "_parent" or a named window, which may be this window, a parent or the shell) they
         *    open in a new tab (noopener) instead. Exception: "_self" inside the app shell with the exact url of an
         *    external Embedded main menu item is shown in the shell (as the menu does). "_blank" opens with noopener.
         *  - The url is converted to a string first (String(url): an Array or a String object becomes the text the browser
         *    would open; null / undefined is refused), then parsed; only the parsed, normalized href is opened.
         *  - Script and data urls ("javascript:", "vbscript:", "data:") are always refused, for every target including
         *    "_blank". "mailto:", "tel:", "sms:" and "callto:" are allowed for every target, a "blob:" url of this origin
         *    only for "_blank" (with noopener); every other scheme is refused.
         *  - Target keywords are matched case-insensitively like browsers do ("_TOP" is "_top", see Target).
         *    A refusal logs a warning, shows a toast (with the ui library) and is announced to screen readers.
         * @param {string} url - Url (any other value is converted with String).
         * @param {string} [target="_self"] - Window target ("_self" = this window / the shell frame, "_blank" = a new tab, "_top", ...).
         */
        Navigate(url, target) {
            //  Default: this window ("go there"); a new tab only when asked ("_blank"). The old Open (compat) keeps its "_blank" default
            target = Sw.Page.Target(target, "_self");
            const U = Sw.Url;
            //  Type confusion: an Array / String object passes no string check but window.open stringifies it
            url = url === null || typeof url === "undefined" ? "" : String(url);
            if (!url || U.IsScript(url)) {
                refusedLink();
                return;
            }
            const abs = U.SafeSameOrigin(url);
            //  A raw server method (Sw.Url.IsPageUrl false, ex: Api/... or ServerManager/KillProcess) is never shown as a page
            //  of the app shell: inside the shell it opens in a new tab (noopener) instead of the content frame or the shell
            if (abs && target !== "_blank" && !U.IsPageUrl(abs) && shellApp()) {
                window.open(abs, "_blank", "noopener");
                return;
            }
            if (abs) {
                if (target === "_self") {
                    navigateSelf(abs);
                    return;
                }
                window.open(U.AppendTheme(abs), target);
                return;
            }
            //  Not this site
            //  Every target but a new tab may replace a window of the app (a named target can be this window or the shell)
            const replaces = target !== "_blank";
            let u = null;
            try {
                u = new URL(url, window.location.href);
            }
            catch {
                refusedLink();
                return;
            }
            if (u.protocol === "http:" || u.protocol === "https:") {
                const ext = u.href;
                if (replaces) {
                    //  An external page of the shell's main menu may be shown in the shell (the shell checks it again)
                    const app = target === "_self" ? shellApp() : null;
                    if (app && app.SafeContentUrl(ext) === ext) {
                        Sw.Frame.PostTop("IframeNavigating", { Url: ext });
                        return;
                    }
                    window.open(ext, "_blank", "noopener");
                    return;
                }
                window.open(ext, "_blank", "noopener");
                return;
            }
            //  Never "javascript:", "vbscript:" or "data:" (whatever IsScript missed), a blob only of this origin in a new
            //  tab, other schemes only when they are handed to an external program
            const p = u.protocol;
            const isBlob = p === "blob:" && !replaces && blobOrigin(u) === window.location.origin;
            if (!isBlob && !externalSchemes.test(p)) {
                refusedLink();
                return;
            }
            //  A blob tab gets no opener either (it can't reach this window)
            if (isBlob)
                window.open(u.href, "_blank", "noopener");
            else
                window.open(u.href, target);
        },

        /**
         * Normalize a window target: the keywords "_self", "_blank", "_top" and "_parent" are returned in lower case
         * (browsers match them ASCII case-insensitively, so compare targets only after this), other names unchanged.
         * @param {string} [target] - Window target.
         * @param {string} [fallback="_self"] - Returned for an empty target.
         * @returns {string} The normalized target.
         */
        Target(target, fallback) {
            if (!target)
                return fallback || "_self";
            const t = String(target);
            return targetKeyword.test(t) ? t.toLowerCase() : t;
        },

        /**
         * Replace this page with another url without adding a history entry (a redirect, ex: logFile/logfile.html
         * forwarding to edit/text.html). Unlike Navigate(url, "_self") no "IframeNavigating" is posted: inside the
         * app shell a page that navigates before it was revealed is treated as a redirect (the shell keeps the
         * requested url and its history entry), and Back does not return to the redirecting page.
         * The forced theme is appended like in Navigate.
         * @param {string} url - Url (relative to this page).
         */
        Replace(url) {
            window.location.replace(Sw.Url.AppendTheme(url));
        },

        /**
         * Run by Reload before it reloads: a function that returns a promise, or null (the default). The app shell sets it while its touch
         * pop-up Back guard entry exists (P3-A-11).
         * @type {(function(): Promise<void>)|null}
         */
        BeforeReload: null,

        /** @type {number} The longest Reload waits for BeforeReload (ms); then it reloads with a developer warning. */
        BeforeReloadMs: 1000,

        /**
         * Reload the page. A history entry of this page's own (the app shell's pop-up Back guard on touch screens, P3-A-11) is removed
         * first (Sw.Page.BeforeReload: a function that returns a promise, or null), so a reload from a menu item (Sign out) leaves no
         * extra entry behind.
         * @param {boolean} [flushCache] - Request a forced reload (only honoured by some browsers).
         * @param {boolean} [clearHash] - Remove the hash (and the history state) first.
         */
        Reload(flushCache, clearHash) {
            const before = typeof Sw.Page.BeforeReload === "function" ? Sw.Page.BeforeReload : null;
            if (before) {
                let p = null;
                try {
                    p = before();
                }
                catch {
                    p = null;
                }
                if (p && typeof p.then === "function") {
                    Sw.Page.BeforeReload = null;
                    //  At most BeforeReloadMs: a hook whose promise never settles must not block every reload (FD F8; S5-A-17)
                    let done = false;
                    const go = late => {
                        if (done)
                            return;
                        done = true;
                        if (late && Sw.DevWarn)
                            Sw.DevWarn("Sw.Page.BeforeReload did not settle within " + Sw.Page.BeforeReloadMs + " ms: the page reloads anyway (return a promise that settles).", "page-beforereload-late");
                        Sw.Page.Reload(flushCache, clearHash);
                    };
                    setTimeout(() => go(true), Sw.Page.BeforeReloadMs);
                    p.then(() => go(false), () => go(false));
                    return;
                }
            }
            Sw.Session.ReloadNow(flushCache, clearHash);
        },

        /**
         * Open the registered editor for an url (falls back to Navigate(read, target)).
         * @param {string} read - Url to read.
         * @param {string} [target] - Target for non editable urls.
         * @param {string} [save] - Url to save to.
         * @param {string} [remove] - Url to delete with.
         * @returns {Promise<void>}
         */
        async Edit(read, target, save, remove) {
            const u = await lookup("GetExtensionEditor", {
                Read: Sw.Url.Absolute(read),
                Save: Sw.Url.Absolute(save),
                Delete: Sw.Url.Absolute(remove),
            });
            if (!u)
                Sw.Page.Navigate(read, target || "_blank");
            else
                openTool(u);
        },

        /**
         * Open the registered viewer for an url (falls back to Navigate(read, target)).
         * @param {string} read - Url to read.
         * @param {string} [target] - Target when there is no viewer.
         * @returns {Promise<void>}
         */
        async View(read, target) {
            const u = await lookup("GetExtensionViewer", Sw.Url.Absolute(read));
            if (!u)
                Sw.Page.Navigate(read, target || "_blank");
            else
                openTool(u);
        },

        /**
         * Open the editor of an url if it has one that differs from its viewer, else open the url itself.
         * @param {string} read - Url to read.
         * @param {string} [target] - Target for non editable urls.
         * @param {string} [save] - Url to save to.
         * @param {string} [remove] - Url to delete with.
         * @returns {Promise<void>}
         */
        async EditOrOpen(read, target, save, remove) {
            const rr = Sw.Url.Absolute(read);
            const u = await lookup("GetExtensionEditor", {
                Read: rr,
                Save: Sw.Url.Absolute(save),
                Delete: Sw.Url.Absolute(remove),
            });
            if (!u || (await lookup("GetExtensionViewer", rr) === u))
                Sw.Page.Navigate(read, target || "_blank");
            else
                openTool(u);
        },

        /** Restore the saved scroll position now (normally done by Loaded). */
        RestoreScroll: restoreScroll,

        /**
         * Detect the first user interaction (mousedown / keydown / touchstart on document): sets DidInteract,
         * emits "page.interacted" and dispatches "UserInteracted" on body (installed once by Sw.Init).
         */
        TrackInteraction() {
            if (trackingInteraction)
                return;
            trackingInteraction = true;
            const d = document;
            const names = ["mousedown", "keydown", "touchstart"];
            /** First interaction handler. @param {Event} ev - Event. */
            const onInteraction = ev => {
                didInteract = true;
                names.forEach(n => d.removeEventListener(n, onInteraction, true));
                Sw.Events.Emit("page.interacted", ev);
                if (d.body)
                    d.body.dispatchEvent(new Event("UserInteracted"));
            };
            names.forEach(n => d.addEventListener(n, onInteraction, { capture: true, passive: true }));
        },

        /**
         * Save the scroll position (ScrollX / ScrollY page state) when the page is left (installed once by Sw.Init).
         */
        TrackScroll() {
            if (trackingScroll)
                return;
            trackingScroll = true;
            /** Store the scroll position in the page state (unchanged positions are not written again). */
            const save = () => {
                Sw.History.Update(state => {
                    if (state.ScrollX === window.scrollX && state.ScrollY === window.scrollY)
                        return false;
                    state.ScrollX = window.scrollX;
                    state.ScrollY = window.scrollY;
                    return true;
                });
            };
            /* SwWeb:allow ux-saf-10 (scroll position saving, no prompt) */
            window.addEventListener("beforeunload", save);
            //  The app shell removes the frame of the previous page without a beforeunload: save on pagehide and while scrolling
            //  too (debounced), so Back restores the position (UX guide SAF-12)
            window.addEventListener("pagehide", save);
            let timer = 0;
            window.addEventListener("scroll", () => {
                clearTimeout(timer);
                if (!restoringScroll)
                    timer = setTimeout(save, 300);
            }, { passive: true });
        },
    };

    //  The option schemas (Sw.DevOptions registry: checked in developer mode, listed by the walker of core/contracttest.html)
    if (Sw.DevOptions && Sw.DevOptions.Register) {
        Sw.DevOptions.Register("Sw.Page.Loading", { Element: "element", Target: "element", Text: "string", Opaque: "boolean", Icon: "string", Delay: { Type: "number", Min: 0 } });
        Sw.DevOptions.Register("Sw.Page.Announce", { Assertive: "boolean" });
        Sw.DevOptions.Register("Sw.Page.SetDirty", { Work: "boolean", Draft: "boolean", Name: "string", Kind: { Type: "string", Values: ["", "server", "export", "download", "upload", "save", "local"] } });
        Sw.DevOptions.Register("Sw.Page.ConfirmLeave", { Source: "function" });
        Sw.DevOptions.Register("Sw.Page.Main", { Loading: "boolean|string|object", NotAllowed: "boolean" });
    }
})();
