/**
 * @file core/include.js - dynamic includes and cache re-validation (the part of Sw.Net that the small bundles need).
 *
 * Purpose:
 *  - `Sw.Net.IncludeJs(baseUrl, file, noThrow, opts)` / `Sw.Net.IncludeCss`: load a script / stylesheet once (de-duplicated by
 *    absolute url); resolves {element, error, errorEv, loaded}. Site urls only (offline rule): another site is refused
 *    unless opts.AllowOnline is true (a deliberate, documented online feature).
 *  - `Sw.Net.FlushCache(url)`: re-validate an url in the browser cache (the session's "filereload" message).
 * core/net.js adds the rest of Sw.Net (requests, connection state, caching). sw/session.js has this file without net.js: the theme
 * script uses IncludeCss ("name@css" themes, the "css" url flag) and the session FlushCache.
 *
 * Load order: after core/url.js. Dependencies: Sw.Url, Sw.DevWarn; Sw.DevOptions when present.
 * Globals defined: none (Sw.Net.IncludeJs, IncludeCss, FlushCache).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** @type {Map<string, Promise<object>>} Absolute url -> include result promise. */
    const includes = new Map();

    /**
     * Resolve an include file url (same rules as the old includeJs/includeCss).
     * @param {string|null} base - Base file url (the file is resolved relative to its folder); null = common/.
     * @param {string} file - Relative or absolute (http/https) url.
     * @returns {string} Absolute url.
     */
    function includeUrl(base, file) {
        if (Sw.Url.IsAbsolute(file))
            return file;
        if (!base)
            return new URL(file, Sw.Url.Common).href;
        return new URL(base + "/../" + file, window.location.href).href;
    }

    /**
     * Is an url local (offline rule): this site (the page origin), or a data url (no request)?
     * @param {string} url - Absolute url.
     * @returns {boolean}
     */
    function isLocalUrl(url) {
        try {
            const u = new URL(url, window.location.href);
            if (u.protocol === "data:")
                return true;
            return u.origin === window.location.origin;
        }
        catch {
            return false;
        }
    }

    /**
     * Include a script or stylesheet once.
     * @param {string|null} base - Base (see includeUrl).
     * @param {string} file - File.
     * @param {boolean} noThrow - Resolve (instead of reject) on failure.
     * @param {boolean} isCss - Stylesheet instead of script.
     * @param {{AllowOnline:boolean}} [opts] - AllowOnline: also load from another site (a deliberate online feature).
     * @returns {Promise<{element:HTMLElement|null, error:string|null, errorEv:*, loaded:boolean}>}
     */
    function include(base, file, noThrow, isCss, opts) {
        let url;
        try {
            url = includeUrl(base, file);
            //  Offline rule: SysWeaver pages work without internet; a file of another site is only loaded when the caller opts in
            if (!(opts && opts.AllowOnline === true) && !isLocalUrl(url))
                throw new Error(_T('Refused to load "{0}": only files of this site are loaded (pass AllowOnline for a deliberate online feature).', url, "Developer error when a page tries to load a script or stylesheet from another web site (the offline rule). {0} is replaced by the url"));
        }
        catch (e) {
            Sw.DevWarn("Sw.Net.Include" + (isCss ? "Css" : "Js") + ": " + e.message, "include." + url);
            const r = { element: null, error: e.message, errorEv: e, loaded: false };
            return noThrow ? Promise.resolve(r) : Promise.reject(r);
        }
        let p = includes.get(url);
        if (!p) {
            //  Already in the page as a static tag (ex: explore/table.js includes ../app/application.js, which table.html also loads):
            //  don't evaluate it a second time (re-running a script with top level classes throws "already declared").
            const existing = Array.from(document.querySelectorAll(isCss ? 'link[rel="stylesheet"][href]' : "script[src]")).find(e => (isCss ? e.href : e.src) === url);
            if (existing) {
                p = new Promise(resolve => {
                    const done = () => resolve({ element: existing, error: null, errorEv: null, loaded: true });
                    //  Static (deferred / async) scripts have all run once the window load event fired
                    if (isCss || document.readyState === "complete")
                        done();
                    else
                        window.addEventListener("load", done, { once: true });
                });
                includes.set(url, p);
            }
        }
        if (!p) {
            p = new Promise(resolve => {
                const r = { element: null, error: null, errorEv: null, loaded: false };
                try {
                    let s;
                    if (isCss) {
                        s = document.createElement("link");
                        s.rel = "stylesheet";
                    } else {
                        s = document.createElement("script");
                        s.async = true;
                    }
                    s.addEventListener("load", () => {
                        r.element = s;
                        r.loaded = true;
                        resolve(r);
                    });
                    s.addEventListener("error", ev => {
                        s.remove();
                        r.error = isCss
                            ? _T('Failed to load "{0}"', url, "Error text when a css file could not be loaded dynamically. {0} is the url of the file, ex: ../ui/theme.css")
                            : _T('Failed to load "{0}"', url, "Error text when a js file could not be loaded dynamically. {0} is the url of the file, ex: ../ui/chart.js");
                        r.errorEv = ev;
                        includes.delete(url);
                        resolve(r);
                    });
                    if (isCss)
                        s.href = url;
                    else
                        s.src = url;
                    document.head.appendChild(s);
                }
                catch (e) {
                    r.error = e.message;
                    r.errorEv = e;
                    includes.delete(url);
                    resolve(r);
                }
            });
            includes.set(url, p);
        }
        return p.then(r => {
            if (r.error && !noThrow)
                throw r;
            return r;
        });
    }

    Sw.Net = Object.assign(Sw.Net || {}, {

        /**
         * Re-validate an url in the browser cache (fetch with cache "reload", body read so the cache entry is
         * replaced). Never throws.
         * @param {string} url - Url.
         * @returns {Promise<void>}
         */
        async FlushCache(url) {
            try {
                const res = await fetch(url, { cache: "reload" });
                await res.arrayBuffer();
            }
            catch {
            }
        },

        /**
         * Include a script once (de-duplicated by absolute url, concurrent callers share the load).
         * Site urls only (offline rule): an url of another site (from file or baseUrl) is refused (the error result, a console
         * warning, nothing is requested) unless opts.AllowOnline is true, for a deliberate online feature that is documented as such.
         * @param {string|null} baseUrl - A file url; file is resolved relative to its folder (null = the common/ folder).
         * @param {string} file - Relative path, or an absolute url of this site (another site needs opts.AllowOnline).
         * @param {boolean} [noThrow] - Resolve with the error result instead of rejecting.
         * @param {{AllowOnline:boolean}} [opts] - AllowOnline: true also loads a file of another site (internet).
         * @returns {Promise<{element:HTMLScriptElement|null, error:string|null, errorEv:*, loaded:boolean}>}
         */
        IncludeJs: (baseUrl, file, noThrow, opts) => include(baseUrl, file, noThrow, false, Sw.DevOptions ? Sw.DevOptions("Sw.Net.IncludeJs", opts) : opts),

        /**
         * Include a stylesheet once (see IncludeJs; site urls only unless opts.AllowOnline).
         * @param {string|null} baseUrl - Base file url (null = the common/ folder).
         * @param {string} file - Relative path, or an absolute url of this site (another site needs opts.AllowOnline).
         * @param {boolean} [noThrow] - Resolve with the error result instead of rejecting.
         * @param {{AllowOnline:boolean}} [opts] - AllowOnline: true also loads a file of another site (internet).
         * @returns {Promise<{element:HTMLLinkElement|null, error:string|null, errorEv:*, loaded:boolean}>}
         */
        IncludeCss: (baseUrl, file, noThrow, opts) => include(baseUrl, file, noThrow, true, Sw.DevOptions ? Sw.DevOptions("Sw.Net.IncludeCss", opts) : opts),
    });

    if (Sw.DevOptions && Sw.DevOptions.Register) {
        Sw.DevOptions.Register("Sw.Net.IncludeJs", { AllowOnline: "boolean" });
        Sw.DevOptions.Register("Sw.Net.IncludeCss", { AllowOnline: "boolean" });
    }
})();
