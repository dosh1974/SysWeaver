/**
 * @file core/init.js - core start up (the UI-less part of the old SysWeaverInit).
 *
 * Purpose: `Sw.Init()` runs once per window (called automatically at the end of this file), the full core extras of the old
 * SysWeaverInit (the messaging half is core/session_start.js, the theme half core/theme.js; both run when evaluated):
 *  1. Page: clears the page state on a browser reload, interaction tracking, scroll position saving.
 *  2. App script: `app/app.js` in every page that does not load it itself, when the shell found it has content (see
 *     loadAppScript; Sw.Page.AppScript).
 * Visual parts of the old init (smooth image loading, server block overlay, loader) are added by the ui library.
 *
 * Load order: last core file. Dependencies: all core modules.
 * Globals defined: none (Sw.Init). Side effect: runs Sw.Init() when evaluated.
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** @type {boolean} True once Init ran. */
    let inited = false;

    /**
     * Test if the current navigation is a browser reload.
     * @returns {boolean}
     */
    function didReload() {
        try {
            const nav = performance.getEntriesByType("navigation");
            if (nav && nav.length > 0)
                return nav.some(n => n.type === "reload");
            return !!(performance.navigation && performance.navigation.type === 1);
        }
        catch {
            return false;
        }
    }

    /**
     * Initialize the core for this window (once; see the file header).
     */
    Sw.Init = function () {
        if (inited)
            return;
        inited = true;
        if (didReload())
            Sw.History.Clear();
        Sw.Page.TrackInteraction();
        Sw.Page.TrackScroll();
        loadAppScript();
    };

    /** localStorage key: per site root, true if the app script app/app.js has content (written by the shell). */
    const appScriptKey = "SysWeaver.AppScript";

    /**
     * Does app/app.js have content, known without a request: the resource timing entry of a same origin window that loaded it
     * (this page or an ancestor, ex: the shell), else what the shell remembered for this site.
     * @param {string} url - Absolute url of app/app.js.
     * @param {boolean} self - Only look at this window (the shell).
     * @returns {boolean|null} null if not known.
     */
    function appScriptHas(url, self) {
        let w = window;
        try {
            for (;;) {
                const e = w.performance.getEntriesByName(url, "resource")[0];
                if (e)
                    return (e.decodedBodySize || e.encodedBodySize || e.transferSize) > 0;
                if (self || !w.parent || w.parent === w)
                    break;
                w = w.parent;
            }
        }
        catch {
            //  A cross origin ancestor
        }
        const m = Sw.Storage.GetJson(appScriptKey, null);
        const v = m && typeof m === "object" ? m[Sw.Url.Root] : undefined;
        return typeof v === "boolean" ? v : null;
    }

    /**
     * The app script: the app shell loads `app/app.js` with its own script tag (as the original) and remembers per site whether it
     * has content. Every other page of the site (framed content pages, ex: explore/table.html, and pages opened on their own) loads
     * it here too, after the page's deferred scripts (so Sw.Cells, Sw.Edit ... exist), but only when it is known to have content
     * (the server answers an empty script when the app has none, and the original requested it only in the shell): then what an
     * app registers there (custom cell formats, edit plug-ins, themes, branding code) reaches every page. Sw.Page.Main waits for
     * it (up to 5 s); body onload handlers run after it anyway (a script added before the load event delays it). Opt out per page:
     * data-sw-no-app on html. Shell only work in app/app.js belongs in window.AppMain (only the shell calls it) or behind
     * `Sw.Page.IsTop`. Sw.Page.AppScript is the promise (resolves when the script ran or failed, false when not loaded).
     */
    function loadAppScript() {
        let done;
        Sw.Page.AppScript = new Promise(r => done = r);
        const start = () => {
            try {
                const root = Sw.Url.Root;
                const url = root ? new URL("app/app.js", root).href : "";
                const has = Array.from(document.scripts).some(s => /(^|\/)app\/app\.js([?#]|$)/.test(s.getAttribute("src") || ""));
                if (has && root) {
                    //  The shell: remember for the other pages whether it has content (no request)
                    const note = () => {
                        const v = appScriptHas(url, true);
                        if (v === null)
                            return;
                        let m = Sw.Storage.GetJson(appScriptKey, null);
                        if (!m || typeof m !== "object")
                            m = {};
                        if (m[root] !== v) {
                            m[root] = v;
                            Sw.Storage.SetJson(appScriptKey, m);
                        }
                    };
                    if (document.readyState === "complete")
                        note();
                    else
                        window.addEventListener("load", note, { once: true });
                }
                if (has || !root || document.documentElement.hasAttribute("data-sw-no-app") || !appScriptHas(url, false)) {
                    done(false);
                    return;
                }
                const s = document.createElement("script");
                s.src = root + "app/app.js";
                s.async = false;
                s.onload = () => done(true);
                s.onerror = () => done(false);
                document.head.append(s);
            }
            catch {
                done(false);
            }
        };
        if (document.readyState === "loading")
            document.addEventListener("DOMContentLoaded", start, { once: true });
        else
            start();
    }

    Sw.Init();
})();
