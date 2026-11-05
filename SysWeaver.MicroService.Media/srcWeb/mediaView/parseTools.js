/**
 * @file parseTools.js - generic "URL parameters / JSON / object -> typed params object" mapper.
 *
 * Loaded with a classic `<script defer>` (no module) by the effect sub-folder pages
 * (`backgroundEffects/`, `imageEffects/`, `collageEffects/`, `textEffects/` `index.html`, as `../parseTools.js`, used by
 * effectPage.js) and by `TextDemo.html`. Needs no SysWeaver core.
 *
 * Globals defined: the class `ParseTools` (static members only), also published as `Sw.Media.ParseTools`.
 * It also holds the shared security rule of url-like media parameters (UrlProblem, InApp, IsAppContent, TextFromUrl).
 * Dependencies: none (only browser APIs: URLSearchParams, fetch, window.postMessage).
 *
 * Conventions:
 * - Values are converted according to the type of the EXISTING member of the destination object, so the
 *   destination must be pre-populated with typed defaults (e.g. a `new MediaPlayerParamsEffect()`).
 * - Parameter names are case-sensitive and must match the member names (PascalCase).
 * - Colors can be passed without `#` (which is awkward in URLs): a leading `$` is used instead.
 * - Pages chain several targets through nested `onNotFound` callbacks (media props -> effect params ->
 *   effect specific shader variables decoded with `DecodeObject`).
 * - Every assignment is logged to the console.
 */

/**
 * Static helper class that copies URL parameters, JSON side files or plain objects onto typed default objects.
 * Never instantiated; all members are static.
 */
class ParseTools {

    /**
     * Maximum length of a text from the url that is rendered inside the application (TextFromUrl).
     * @type {number}
     */
    static MaxAppText = 80;

    /**
     * True when this page is shown inside the SysWeaver application (the app shell is the top window, same origin),
     * at any depth (the shell's content frame or a frame inside it).
     * @returns {boolean}
     */
    static InApp() {
        try {
            return window.top !== window && !!window.top.SysWeaverApp;
        }
        catch {
            return false;
        }
    }

    /**
     * True when this page is the content frame of the app shell itself (shown right under the application's header).
     * @returns {boolean}
     */
    static IsAppContent() {
        try {
            return window.parent !== window && window.parent === window.top && !!window.top.SysWeaverApp;
        }
        catch {
            return false;
        }
    }

    /**
     * The security rule of url-like media parameters that come from the page url (the same rule as the `link` parameter
     * of MediaView.html, see media.js MediaLinkProblem): a http(s) url of this site is allowed; any other scheme
     * ("javascript:", "data:", "blob:", ...) is refused; a url of another site is refused inside the application (InApp),
     * where it would look like content of this site, and allowed in a window of its own or when framed by another host.
     * Check every such parameter (effect, image, font) before fetching it or its side json.
     * @param {string} url - The url (relative urls resolve against base); empty = nothing to check.
     * @param {string} [base] - Base url (default the page location).
     * @returns {{Kind: string, Url: (string|null), Host: string}|null} null when allowed, else Kind "Scheme" (not a web
     * address) or "Site" (another site, Url = the absolute url, Host = its host name).
     */
    static UrlProblem(url, base) {
        if (url === null || typeof url === "undefined" || url === "")
            return null;
        let u = null;
        try {
            u = new URL(String(url), base || window.location.href);
        }
        catch {
            return { Kind: "Scheme", Url: null, Host: "" };
        }
        if (u.protocol !== "http:" && u.protocol !== "https:")
            return { Kind: "Scheme", Url: null, Host: "" };
        if (u.origin === window.location.origin || !ParseTools.InApp())
            return null;
        return { Kind: "Site", Url: u.href, Host: u.host };
    }

    /**
     * Limit a text that comes from the page url when it is rendered inside the application (InApp): at most MaxAppText
     * characters (an ellipsis marks the cut), so a crafted link can't paint a long message under the application header.
     * Outside the application the text is returned unchanged.
     * @param {string} text - The text.
     * @param {number} [max] - Maximum length (default MaxAppText).
     * @returns {string} The (possibly shortened) text.
     */
    static TextFromUrl(text, max) {
        const s = text === null || typeof text === "undefined" ? "" : String(text);
        const m = max > 1 ? max : ParseTools.MaxAppText;
        if (s.length <= m || !ParseTools.InApp())
            return s;
        return s.substring(0, m - 1) + "…";
    }


    /**
     * Decodes a string value into an object when it has a recognized format.
     * - `$RGB`, `$RGBA`, `$RRGGBB`, `$RRGGBBAA` (hex, `$` instead of `#`) become a color object
     *   `{Red, Green, Blue, Alpha}` with 0..1 float components (Alpha defaults to 1.0).
     *   A `$` string of any other length falls through to the checks below.
     * - Strings starting with `{` or `[` are parsed with `JSON.parse` (throws on invalid JSON).
     * - Anything else is returned unchanged.
     * @param {string} val - The string to decode.
     * @returns {({Red:number, Green:number, Blue:number, Alpha:number}|object|Array|string)} The decoded value.
     */
    static DecodeObject(val) {
        if (val.startsWith('$')) {
            switch (val.length) {
                case 4:
                    return {
                        Red: parseInt(val.substring(1, 2), 16) / 15.0,
                        Green: parseInt(val.substring(2, 3), 16) / 15.0,
                        Blue: parseInt(val.substring(3, 4), 16) / 15.0,
                        Alpha: 1.0,
                    };
                case 5:
                    return {
                        Red: parseInt(val.substring(1, 2), 16) / 15.0,
                        Green: parseInt(val.substring(2, 3), 16) / 15.0,
                        Blue: parseInt(val.substring(3, 4), 16) / 15.0,
                        Alpha: parseInt(val.substring(4, 5), 16) / 15.0,
                    };
                case 7:
                    return {
                        Red: parseInt(val.substring(1, 3), 16) / 255.0,
                        Green: parseInt(val.substring(3, 5), 16) / 255.0,
                        Blue: parseInt(val.substring(5, 7), 16) / 255.0,
                        Alpha: 1.0,
                    };
                case 9:
                    return {
                        Red: parseInt(val.substring(1, 3), 16) / 255.0,
                        Green: parseInt(val.substring(3, 5), 16) / 255.0,
                        Blue: parseInt(val.substring(5, 7), 16) / 255.0,
                        Alpha: parseInt(val.substring(7, 9), 16) / 255.0,
                    };
            }
        }
        if (val.startsWith('{') || val.startsWith('['))
            return JSON.parse(val);
        return val;
    }


    /**
     * Installs a two-way message relay between the parent window and a child iframe, so an outer host can
     * control a player nested two frames deep (e.g. a page that renders through an iframe of `MediaPreview.html`).
     * Replaces `window.onmessage`: messages from the iframe's window are re-posted to `window.parent`,
     * messages from the parent (or the top window) are re-posted into the iframe; both use the original `e.origin` as target
     * origin. Security: only messages of this origin are relayed (a foreign window can't control the player); messages from
     * this window itself are ignored, and nothing is forwarded while the iframe has no content window.
     * @param {HTMLIFrameElement} iframe - The child iframe to relay messages to/from.
     * @returns {void}
     */
    static MessageProxy(iframe) {
        const p = window.parent;
        window.onmessage = e => {
            if (e.source === window || e.origin !== window.location.origin)
                return;
            const w = iframe.contentWindow;
            if (!w)
                return;
            if (e.source !== w && e.source !== p && e.source !== window.top)
                return;
             (e.source === w ? p : w).postMessage(e.data, e.origin);
        }
    }


    /**
     * Assigns a string value to `obj[key]`, converted according to the type of the existing member:
     * - `val === null`: sets `null` (even if the member does not exist).
     * - member missing (`undefined`): calls `onNotFound(key, val)`, or logs a console warning when no callback is given.
     * - `object` member (including a member that is currently `null`): `ParseTools.DecodeObject(val)`.
     * - `string` member: the value, with a leading `$` replaced by `#` (colors in URLs).
     * - `boolean` member: true for `"true"` or `"1"`, otherwise false.
     * - `number` member: `parseFloat(val)`; the assignment is skipped when the result is NaN.
     * - any other member type (e.g. function): nothing happens.
     * Every assignment is logged with `console.log(logPrefix + key + ' = ' + value)`.
     * Exceptions (e.g. invalid JSON) are caught and logged as a warning with the error's `message`.
     * @param {object} obj - The destination object with typed default members.
     * @param {string} key - Member name (case-sensitive).
     * @param {?string} val - The value as a string, or null.
     * @param {function(string, string):void} [onNotFound] - Called with (key, val) when `obj` has no such member.
     * @param {string} [logPrefix=""] - Prefix for the console logging.
     * @returns {void}
     */
    static SetToObject(obj, key, val, onNotFound, logPrefix) {
        logPrefix = logPrefix ?? "";
        try {
            const kt = typeof obj[key];
            if (val === null) {
                obj[key] = null;
                console.log(logPrefix + key + ' = null');
                return;
            }
            if (kt === "undefined") {
                if (onNotFound)
                    onNotFound(key, val);
                else
                    console.warn('"' + key + '" is not found!');
                return;
            }
            if (kt === "object") {
                const v = ParseTools.DecodeObject(val);
                obj[key] = v;
                console.log(logPrefix + key + ' = ' + JSON.stringify(v));
                return;
            }                
            if (kt === "string") {
                if (val.startsWith('$'))
                    val = "#" + val.substring(1);
                obj[key] = val;
                console.log(logPrefix + key + ' = "' + val + '"');
                return;
            }
            if (kt === "boolean") {
                const v = val === "true" || val === "1"
                obj[key] = v;
                console.log(logPrefix + key + ' = ' + (v ? "true" : "false"));
                return;
            }
            if (kt === "number") {
                const v = parseFloat(val);
                if (!isNaN(v)) {
                    obj[key] = v;
                    console.log(logPrefix + key + ' = ' + v);
                }
                return;
            }
        }
        catch (e) {
            console.warn(logPrefix + key + ": " + e.message);
        }
    }

    /**
     * Copy search params to an existing object.
     * Runs `SetToObject` for every entry; the literal string `"null"` is passed as `null`.
     * @param {URLSearchParams} searchParams Search parameters
     * @param {object} dest The target object, any parameter that have a matching member in this object will be populated
     * @param {function(string, string)} onNotFound Optional function to if a key is not found in the destination
     * @param {string} logPrefix Optional prefix to apply to any logging
     */
    static ParamsToObject(searchParams, dest, onNotFound, logPrefix) {
        for (let k of searchParams) {
            const key = k[0];
            let val = k[1];
            if (val === "null")
                val = null;
            ParseTools.SetToObject(dest, key, val, onNotFound, logPrefix);
        }
    }

    /**
     * Copy object members to an existing object
     * Runs `SetToObject` for every non-function member (including inherited enumerable ones, `for...in`),
     * converting each value to a string first (`"" + val`, so objects become "[object Object]" and null becomes "null").
     * @param {object} srcObject The source object, any member that exist in the destination will be copied
     * @param {object} dest The target object, any source member that have a matching member in this object will be populated
     * @param {function(string, string)} onNotFound Optional function to if a key is not found in the destination
     * @param {string} logPrefix Optional prefix to apply to any logging
     */
    static ObjectToObject(srcObject, dest, onNotFound, logPrefix) {
        for (let key in srcObject) {
            const val = srcObject[key];
            if (typeof val !== "function")
                ParseTools.SetToObject(dest, key, "" + val, onNotFound, logPrefix);
        }
    }

    /**
     * Take an object and create url parameters with the same name
     * Skips `undefined` and function members, JSON-encodes object members, writes falsy numbers (0, NaN) as `0`
     * and false booleans as `false`; `null` is written as the string "null" (read back as null by `ParamsToObject`).
     * @param {object} srcObject The object to create an url parameters from
     * @returns {string} The url parameters
     */
    static ObjectToParams(srcObject) {
        const pm = new URLSearchParams();
        for (let key in srcObject) {
            let val = srcObject[key];
            const vt = typeof val;
            if (val !== null) {
                if (vt === "undefined")
                    continue;
                if (vt === "function")
                    continue;
                if (vt === "object") {
                    pm.set(key, JSON.stringify(val));
                    continue;
                }
                if (!val) {
                    if (vt === "number")
                        val = 0;
                    if (vt === "boolean")
                        val = false;
                }
            }
            pm.set(key, val);
        }
        return pm.toString();
    }


    /**
     * Load some properties from a file
     * Fetches `srcUrl` (GET, mode "cors", default cache); on HTTP 200 parses the body as JSON and applies it with
     * `ObjectToObject`. Network errors, parse errors and non-200 statuses (e.g. a missing side file) are silently ignored.
     * @param {string} srcUrl The url of a json file with properties
     * @param {object} dest The target object, any source member that have a matching member in this object will be populated
     * @param {function(string, string)} onNotFound Optional function to if a key is not found in the destination
     * @param {string} logPrefix Optional prefix to apply to any logging
     * @returns {Promise<void>} Resolves when the file was applied (or ignored).
     */
    static async FileToObject(srcUrl, dest, onNotFound, logPrefix) {

        let effectVars = null;
        try {
            const res = await fetch(new Request(srcUrl, {
                method: "GET",
                mode: "cors",
                cache: "default",
            }));
            if (res.status === 200) {
                effectVars = await res.json();
            }
        }
        catch {
        }
        if (effectVars)
            ParseTools.ObjectToObject(effectVars, dest, onNotFound, logPrefix);
    }




}

/** Publish the new API name (ParseTools stays the public legacy global). */
(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    (Sw.Media = Sw.Media || {}).ParseTools = ParseTools;
})();
