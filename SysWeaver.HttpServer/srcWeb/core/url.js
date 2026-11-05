/**
 * @file core/url.js - URL helpers and the site root.
 *
 * Purpose: `Sw.Url` with
 *  - `Root`: the absolute site root (with the server prefix, ending in "/"), derived from the location of the
 *    core script. The core is always served one folder below the root (`sw/core.js`, `sw/session.js`, `sw/net.js`, `core/url.js`, or
 *    the legacy bundle `common/common.js`), so root = script folder + "../". All core server urls (GetMessages,
 *    serverTime, Api/...) are built from it, so the site also works under a url prefix.
 *  - `Common`: root + "common/" (base for includes with no explicit base, like the old library).
 *  - Cached url parameters with lower-cased keys (`Params`, `Get`, `Has`, `GetNumber`, `GetInt`).
 *  - `IsAbsolute`, `Absolute`, `IsSameOrigin`, `Join`, `AddParam`, `AppendTheme`, `FileName` (the decoded file name of an url, for showing or saving).
 *  - `SiteUrl` / `SitePath`: the one rule for site urls in url parameters (`q` of explore/table.html and explore/api.html):
 *    "./", "../", "/" or a scheme resolve like a link, anything else ("Api/x") is relative to the site root.
 *  - Guards for untrusted urls (url parameters, deep links, messages): `SafeSameOrigin` (a http(s) url of this site,
 *    else null), `IsHttp`, `IsScript` ("javascript:" / "vbscript:"), `IsPageUrl` (a page or file, not a raw server
 *    method such as Api/... or ServerManager/KillProcess, which must never be loaded as a page from untrusted input).
 *
 * Load order: after core/kernel.js (must be evaluated while its script element is executing, so that
 * document.currentScript is available; a fallback searches the script elements and finally uses the origin).
 * Dependencies: Sw.Theme or Sw.Session (at call time only, for AppendTheme).
 * Globals defined: none (Sw.Url).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /**
     * Find the url of the core script (bundle, single file or legacy common.js).
     * @returns {string|null} Absolute script url, or null if not found.
     */
    function findScript() {
        const cs = document.currentScript;
        if (cs && cs.src)
            return cs.src;
        const rx = /\/(sw\/(core|session|theme|net)|core\/[a-z0-9_]+|common\/common)\.js(\?.*)?$/i;
        const s = document.scripts;
        for (let i = s.length - 1; i >= 0; --i) {
            const src = s[i].src;
            if (src && rx.test(src))
                return src;
        }
        return null;
    }

    const script = findScript();

    /** @type {string} Absolute site root ending with "/". */
    const root = script ? new URL("../", script).href : new URL("/", window.location.href).href;

    /** @type {URLSearchParams|null} Cached parameters of the current url (lower-cased keys). */
    let params = null;

    /** @type {string|null} The search string the cache was built from. */
    let paramsSearch = null;

    /**
     * @type {RegExp} An encoded delimiter in a raw url path (? # & / \ . ; and % (double encoding), control characters):
     * the server decodes the path before routing, so such a path is never treated as a page (see IsPageUrl).
     */
    const encodedDelimiter = /%(3f|23|26|2f|5c|2e|3b|25|[01][0-9a-f]|7f)/i;

    /**
     * Test if an url is absolute (starts with http:// or https://, same as the old IsAbsolutePath).
     * @param {string} url - The url.
     * @returns {boolean}
     */
    function isAbsolute(url) {
        if (typeof url !== "string")
            return false;
        return url.startsWith("http://") || url.startsWith("https://");
    }

    /**
     * Parse an url (relative to base or the current location).
     * @param {string|URL} url - Url.
     * @param {string} [base] - Base url (default the current location).
     * @returns {URL|null} The parsed url, null for an empty / non string / invalid url.
     */
    function parse(url, base) {
        if (url instanceof URL)
            url = url.href;
        if (typeof url !== "string" || url.length <= 0)
            return null;
        try {
            return new URL(url, base ? base : window.location.href);
        }
        catch {
            return null;
        }
    }

    /**
     * @namespace Sw.Url - url helpers.
     */
    Sw.Url = {

        /** @type {string|null} Absolute url of the core script (null if unknown). */
        Script: script,

        /** @type {string} Absolute site root (including the server prefix), ends with "/". */
        Root: root,

        /** @type {string} Absolute url of the common/ folder (root + "common/"). */
        Common: root + "common/",

        /**
         * Get the parameters of the current url with lower-cased keys (cached; the cache is rebuilt if the
         * search part of the location changed, e.g. by history.replaceState).
         * @returns {URLSearchParams}
         */
        Params() {
            const s = window.location.search;
            if (params && (paramsSearch === s))
                return params;
            const o = new URLSearchParams(s);
            const p = new URLSearchParams();
            for (const [k, v] of o)
                p.append(k.toLowerCase(), v);
            params = p;
            paramsSearch = s;
            return p;
        },

        /**
         * Parse a query string into parameters with lower-cased keys (not cached).
         * @param {string} search - Query string (with or without "?") or a full url.
         * @returns {URLSearchParams}
         */
        ParamsOf(search) {
            search = "" + (search ?? "");
            const q = search.indexOf('?');
            if (q >= 0)
                search = search.substring(q + 1);
            const h = search.indexOf('#');
            if (h >= 0)
                search = search.substring(0, h);
            const p = new URLSearchParams();
            for (const [k, v] of new URLSearchParams(search))
                p.append(k.toLowerCase(), v);
            return p;
        },

        /**
         * Get a parameter of the current url (case insensitive name).
         * @param {string} name - Parameter name.
         * @returns {string|null} The (first) value or null.
         */
        Get: name => Sw.Url.Params().get(("" + name).toLowerCase()),

        /**
         * Test if the current url has a parameter (case insensitive name, a value is not required).
         * @param {string} name - Parameter name.
         * @returns {boolean}
         */
        Has: name => Sw.Url.Params().has(("" + name).toLowerCase()),

        /**
         * Get a decimal number parameter of the current url.
         * @param {string} name - Parameter name (case insensitive).
         * @param {number|null} [def=null] - Value if missing or not a number.
         * @returns {number|null}
         */
        GetNumber(name, def) {
            const v = Sw.Url.Get(name);
            if (typeof v !== "string")
                return def ?? null;
            const n = Number.parseFloat(v);
            return Number.isNaN(n) ? (def ?? null) : n;
        },

        /**
         * Get an integer parameter of the current url.
         * @param {string} name - Parameter name (case insensitive).
         * @param {number|null} [def=null] - Value if missing or not a number.
         * @returns {number|null}
         */
        GetInt(name, def) {
            const v = Sw.Url.Get(name);
            if (typeof v !== "string")
                return def ?? null;
            const n = Number.parseInt(v);
            return Number.isNaN(n) ? (def ?? null) : n;
        },

        /** Test if an url starts with http:// or https:// (see isAbsolute). */
        IsAbsolute: isAbsolute,

        /**
         * Make an url absolute.
         * @param {string} url - Url (absolute urls are returned as is).
         * @param {string} [base] - Base url (default the current location).
         * @returns {string|null} Absolute url, null for a falsy url.
         */
        Absolute(url, base) {
            if (!url)
                return null;
            if (isAbsolute(url))
                return url;
            return new URL(url, base ? base : window.location.href).href;
        },

        /**
         * Resolve a site url given as an url parameter (the `q` of explore/table.html, explore/api.html ...): one rule for all pages.
         * A value that starts with "./", "../", "/", "\" or a scheme ("https:") is resolved like a link on the page (against base);
         * any other value ("Api/debug/ApiTable") is relative to the site root (Sw.Url.Root), the form in which the server lists its
         * api urls. So "../Api/x" (the menu form, relative to a page one folder below the root) and "Api/x" give the same url.
         * Not a security check: pass the result to SafeSameOrigin before fetching untrusted input.
         * @param {string} url - Url parameter value.
         * @param {string} [base] - Base url of link style values (default the current location).
         * @returns {string|null} The absolute url, null for an empty or invalid value.
         */
        SiteUrl(url, base) {
            if (typeof url !== "string" || !(url = url.trim()))
                return null;
            const u = /^(?:\.\.?(?:[\/\\]|$)|[\/\\]|[a-z][a-z0-9+.\-]*:)/i.test(url) ? parse(url, base) : parse(url, root);
            return u ? u.href : null;
        },

        /**
         * The path of an url of this site relative to the site root, without query and hash, ex: "Api/debug/ApiTable" (the key the
         * server uses for its apis, Api/debug/GetApiInfo). The url is resolved with SiteUrl first.
         * @param {string} url - Url (an url parameter value, see SiteUrl).
         * @param {string} [base] - Base url of link style values (default the current location).
         * @returns {string|null} The decoded path, null when the url is not a http(s) url of this site below the site root.
         */
        SitePath(url, base) {
            const u = parse(Sw.Url.SafeSameOrigin(Sw.Url.SiteUrl(url, base)));
            const r = parse(root);
            if (!u || !r || u.origin !== r.origin || !u.pathname.startsWith(r.pathname))
                return null;
            try {
                return decodeURIComponent(u.pathname.substring(r.pathname.length));
            }
            catch {
                return null;
            }
        },

        /**
         * Test if an url (absolute or relative to the current location) has the same origin as this page.
         * @param {string} url - Url.
         * @returns {boolean}
         */
        IsSameOrigin(url) {
            try {
                return new URL(url, window.location.href).origin === window.location.origin;
            }
            catch {
                return false;
            }
        },

        /**
         * Test if an url (absolute or relative to base) uses the http: or https: scheme. Unlike IsAbsolute the url is resolved
         * first, so a relative url is http(s) when the page is, and "javascript:", "data:", "blob:", "vbscript:" etc. are not.
         * @param {string} url - Url.
         * @param {string} [base] - Base url (default the current location).
         * @returns {boolean}
         */
        IsHttp(url, base) {
            const u = parse(url, base);
            return !!u && (u.protocol === "http:" || u.protocol === "https:");
        },

        /**
         * Validate an url from an untrusted source (an url parameter, a deep link, a message): it must be a http(s) url of this
         * site (relative, or absolute with the same origin as this page). Rejects other sites, protocol-relative urls of other
         * hosts ("//host/x", "\\host\x") and every other scheme ("javascript:", "data:", "blob:", "vbscript:", ...).
         * Use it before fetching, framing or navigating to such an url.
         * @param {string} url - Url (relative urls resolve against base).
         * @param {string} [base] - Base url (default the current location).
         * @returns {string|null} The absolute url when allowed, else null (also for an empty or non string url).
         */
        SafeSameOrigin(url, base) {
            const u = parse(url, base);
            if (!u || (u.protocol !== "http:" && u.protocol !== "https:") || u.origin !== window.location.origin)
                return null;
            return u.href;
        },

        /**
         * Test if an url of this site is a page (or file) that may be shown as a page, not a raw server method: a GET of a method
         * may perform an action (ex: ServerManager/KillProcess?pid=..), so a deep link, a page message or a link parameter must
         * never load it into the app. A page is a path ending in "/", a file name with an extension (".html", ".md", ".png" ..),
         * or an "explore" folder listing; under the api root (Root + "Api/", any case) only ".html" / ".htm" files and "explore"
         * folder listings (Api/explore, Api/debug/explore).
         * Method paths have no extension (Api/debug/GetApiInfo, ServerManager/KillProcess). Not a check of the origin (see
         * SafeSameOrigin), and not a server side protection (the server must not perform actions on GET).
         * The extension is tested on the raw (undecoded) path as the browser sends it, and a path holding an encoded delimiter
         * (%3F, %23, %26, %2F, %5C, %2E, %3B, any case), a double encoding (%25) or an encoded control character is never a
         * page: the server decodes the path before routing, so "KillProcess%3F.html" would run KillProcess with ".html" as query.
         * @param {string} url - Url (relative urls resolve against base).
         * @param {string} [base] - Base url (default the current location).
         * @returns {boolean} True for a page, false for a method path or an invalid url.
         */
        IsPageUrl(url, base) {
            const u = parse(url, base);
            if (!u || (u.protocol !== "http:" && u.protocol !== "https:"))
                return false;
            const raw = u.pathname;
            if (encodedDelimiter.test(raw))
                return false;
            let path;
            try {
                path = decodeURIComponent(raw).replace(/\\/g, "/");
            }
            catch {
                return false;
            }
            //  The api root is matched on the decoded path (the server's view), the extension on the raw one
            const rootPath = parse(root).pathname;
            const last = raw.substring(raw.lastIndexOf("/") + 1);
            //  (the "explore" listing of an api folder, ex: Api/explore, Api/debug/explore, is a page too: the explore module answers
            //  "<any folder>/explore" and no api method is named "explore"; exact case, as the module matches it; OWN4-Explore-1)
            if (path.toLowerCase().startsWith((rootPath + "api/").toLowerCase()))
                return /\.html?$/i.test(raw) || last === "explore";
            if (!last)
                return true;
            return last.indexOf(".") > 0 || last.toLowerCase() === "explore";
        },

        /**
         * Test if an url runs script when navigated to ("javascript:" or "vbscript:", after the browser's own url clean up, so
         * leading spaces or tabs inside the scheme count too). Such urls must never be opened from untrusted input.
         * Any other value but an URL is converted with String first (window.open and location do that too, so an Array or
         * a String object holding "javascript:..." is a script url).
         * @param {string} url - Url.
         * @returns {boolean}
         */
        IsScript(url) {
            const u = parse(url === null || typeof url === "undefined" || url instanceof URL ? url : String(url));
            return !!u && (u.protocol === "javascript:" || u.protocol === "vbscript:");
        },

        /**
         * The file name of an url, for showing or saving (not for building urls): the last path segment without the query and
         * hash, uri decoded ("My%20file.png" -> "My file.png"; a malformed escape is kept as it is).
         * @param {string} url - Url (absolute or relative).
         * @returns {string} The name ("" when the url ends with "/").
         */
        FileName(url) {
            let s = String(url || "");
            const end = s.search(/[?#]/);
            if (end >= 0)
                s = s.substring(0, end);
            s = s.substring(s.lastIndexOf("/") + 1);
            try {
                return decodeURIComponent(s);
            }
            catch {
                return s;
            }
        },

        /**
         * Join url parts with exactly one "/" between them.
         * @param {...string} parts - Parts (empty / null parts are skipped).
         * @returns {string}
         */
        Join(...parts) {
            let r = "";
            parts.forEach(p => {
                if (p === null || typeof p === "undefined")
                    return;
                p = "" + p;
                if (p.length <= 0)
                    return;
                if (r.length <= 0) {
                    r = p;
                    return;
                }
                const a = r.endsWith("/");
                const b = p.startsWith("/");
                if (a && b)
                    r += p.substring(1);
                else if (a || b)
                    r += p;
                else
                    r += "/" + p;
            });
            return r;
        },

        /**
         * Append a query parameter to an url ("?" or "&" as needed, the value is uri encoded).
         * @param {string} url - Url.
         * @param {string} name - Parameter name.
         * @param {*} [value] - Value (omitted: only the name is appended).
         * @returns {string}
         */
        AddParam(url, name, value) {
            const h = url.indexOf('#');
            const hash = h >= 0 ? url.substring(h) : "";
            if (h >= 0)
                url = url.substring(0, h);
            const p = (typeof value === "undefined" || value === null) ? name : (name + "=" + encodeURIComponent("" + value));
            return url + (url.indexOf('?') < 0 ? "?" : "&") + p + hash;
        },

        /**
         * Append the forced theme (Sw.Theme.Forced, from the "usetheme" url parameter) as "useTheme=<name>"
         * so a page opened from this one uses the same forced theme.
         * The parameter goes before any "#hash" part (same as AddParam): "a.html#h" -> "a.html?useTheme=dark#h".
         * A "usetheme" parameter already in the url (any case) is replaced, not repeated (Get returns the first value, so
         * a second one would lose): "a.html?useTheme=light" -> "a.html?useTheme=dark".
         * @param {string} url - The url to extend.
         * An url of another site is not changed (the theme is this site's; upstream appendTheme, merge 4).
         * @returns {string} The url, unchanged if no theme is forced or it is another site's.
         */
        AppendTheme(url) {
            const t = Sw.Theme ? Sw.Theme.Forced : (Sw.Session ? Sw.Session.RequestedTheme : null);
            if (!t)
                return url;
            url = "" + url;
            if (!Sw.Url.IsSameOrigin(url))
                return url;
            const h = url.indexOf('#');
            const hash = h >= 0 ? url.substring(h) : "";
            let base = h >= 0 ? url.substring(0, h) : url;
            const q = base.indexOf('?');
            if (q >= 0) {
                const kept = base.substring(q + 1).split('&').filter(p => p && !/^usetheme(=|$)/i.test(p));
                base = base.substring(0, q) + (kept.length ? "?" + kept.join("&") : "");
            }
            return Sw.Url.AddParam(base + hash, "useTheme", t);
        },
    };
})();
