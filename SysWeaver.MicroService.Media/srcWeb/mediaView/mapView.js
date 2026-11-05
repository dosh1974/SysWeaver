/**
 * @file mapView.js - page logic of MapView.html: a Google Maps (Embed API) or OpenStreetMap view with an optional pin,
 * link / title overlay and DPI scaling. Rendered by the Thumbnail.Web service (GetGoogleMap, headless capture) and embedded
 * by MapDemo.html and other pages.
 *
 * Url parameters (C# contract, ThumbnailWebService.GetGoogleMap: key, center, zoom, language, maptype, dpi, pin, pinX, pinY,
 * pinHeight; key / pin / pinX / pinY arrive unescaped):
 * - `maptype`: roadmap (default) or any Google type; `dark` = roadmap inverted; open | openstreet | openstreetmap = OSM;
 *   opendark | darkstreet | openstreetdark | openstreetmapdark = dark OSM.
 * - `key`: Google key; when missing `Api/GetGoogleMapsKey` (Dev role) is tried, else OpenStreetMap is used (warning).
 * - `center`: required "lat,lng"; other characters are stripped, a trailing S / W negates.
 * - `zoom` (default 16), `language` | `lang` (default en), `dpi` (percent, default 100).
 * - `interactive=true`: the map can be used (otherwise pointer events are off).
 * - `link`: true | view (Google Maps search), dir (directions), any http(s) url (a link over the map; other schemes such
 *   as "javascript:" are ignored); `title`: tool tip.
 * - `pin`: true (= red-marker), star, green-pennant, blue-medieval, game-flag, green-static-flag, red|green|blue-flag,
 *   red|green|blue-needle, red|green|blue-marker (map/*.svg), an image url or a `data:image/` url; `pinX`, `pinY` (anchor,
 *   css length or %), `pinHeight` (px, default 96 or the preset's).
 * Security inside the app shell (InApp; the media rule of parseTools.js UrlProblem): an own `pin` image must be of this
 * site (or a `data:image/` url of at most MaxPinBytes), an image of another site is not loaded, every pin is shown at most
 * MaxPinPx high and wide, and an own pin picture is marked "The pin is a picture from the link"; a `link` of this site must be
 * a page (IsPageUrl: never a raw server method such as Api/... or ServerManager/KillProcess, which may perform an action on
 * GET) and is shown in the shell's content frame (NavigateInApp), a link to another site (also the Google links) opens in a
 * new tab (noopener) and is marked with a visible "Opens <host> in a new tab ↗" badge; a custom `title` is text only (control
 * and bidi characters removed), limited to MaxAppText characters (a title: plain text, no caption).
 * Outside the app shell (headless capture, a window of its own, another host) own pins and links may be any http(s) url
 * (a link opens in a new tab with noopener), as before.
 * Screenshot host (ScreenShotHost: isSupported / doIt): with a host the page uses no fades and calls
 * `doIt(0, 0, null)` 500 ms after the map frame loaded (same 3 argument call as before).
 * Online, the same requests as the original page: the map frame (OpenStreetMap, or Google with a key) is created at once;
 * an OpenStreetMap type asks the server nothing, a Google type without a `key` url parameter asks `Api/GetGoogleMapsKey`
 * (once per map view). Map tiles are an external data service (the one online part of the application); without internet
 * the frame shows the browser's own error page, as before.
 *
 * A lean page on purpose (it is embedded many times and captured headless): it loads only the translation fallbacks
 * (../core/i18n.js), not the whole SysWeaver core. Errors are shown as red text.
 * Globals defined: `MapView` (static class, also `Sw.Media.MapView`). Runs `MapView.Main()` when loaded.
 */

/**
 * The map view page. Never instantiated.
 */
class MapView {

    /** maptype values that select OpenStreetMap: 1 = normal, 2 = dark. */
    static OpenStreetMap = new Map([
        ["open", 1], ["openstreet", 1], ["openstreetmap", 1],
        ["opendark", 2], ["darkstreet", 2], ["openstreetdark", 2], ["openstreetmapdark", 2],
    ]);

    /**
     * Pin presets: [image, anchorX, anchorY, height]. green-static-flag pointed to a missing GreenStaticFlag.svg (a broken
     * image); it now uses the green flag.
     */
    static Pins = new Map([
        ["true", ["map/RedMarker.svg", "50%", "95.4545454545%", 80]],
        ["star", ["map/Star.svg", "50%", "100%", 80]],
        ["green-pennant", ["map/GreenPennant.svg", "5%", "95%", 80]],
        ["blue-medieval", ["map/BlueMedieval.svg", "15.709768274%", "100%", 80]],
        ["game-flag", ["map/GameFlag.svg", "28%", "98%", 80]],
        ["green-static-flag", ["map/GreenFlag.svg", "31.7708333333%", "74.3055555556%", 96]],
        ["red-flag", ["map/RedFlag.svg", "31.7708333333%", "74.3055555556%", 96]],
        ["green-flag", ["map/GreenFlag.svg", "31.7708333333%", "74.3055555556%", 96]],
        ["blue-flag", ["map/BlueFlag.svg", "31.7708333333%", "74.3055555556%", 96]],
        ["red-needle", ["map/RedNeedle.svg", "50%", "98.275862069%", 80]],
        ["green-needle", ["map/GreenNeedle.svg", "50%", "98.275862069%", 80]],
        ["blue-needle", ["map/BlueNeedle.svg", "50%", "98.275862069%", 80]],
        ["red-marker", ["map/RedMarker.svg", "50%", "95.4545454545%", 80]],
        ["green-marker", ["map/GreenMarker.svg", "50%", "95.4545454545%", 80]],
        ["blue-marker", ["map/BlueMarker.svg", "50%", "95.4545454545%", 80]],
    ]);

    /**
     * Maximum length of a custom `title` from the url inside the app shell (the same limit as ParseTools.MaxAppText).
     * @type {number}
     */
    static MaxAppText = 80;

    /**
     * Inside the app shell: the largest own pin picture (MaxPinBytes bytes of a `data:image/` url, estimated from its text) and
     * the largest shown pin size (MaxPinPx css pixels high and wide, before the dpi scaling). Larger data pins are ignored.
     * @type {number}
     */
    static MaxPinBytes = 64 * 1024;

    /** @type {number} See MaxPinBytes. */
    static MaxPinPx = 128;

    /**
     * Control and invisible formatting characters removed from a text of the url inside the app shell (C0 / C1 controls, bidi
     * marks, embeddings, overrides and isolates, zero width characters): they could reorder or hide parts of the text.
     * @type {RegExp}
     */
    static AppTextStrip = /[\u0000-\u001f\u007f-\u009f\u200b-\u200f\u202a-\u202e\u2060-\u2069\ufeff]/g;

    /**
     * True when this page is shown inside the SysWeaver application (the app shell is the same-origin top window), at any
     * depth (same rule as ParseTools.InApp).
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
     * The absolute url when url is a http(s) url of this site (same rule as Sw.Url.SafeSameOrigin), else null.
     * @param {string} url The url (relative to this page).
     * @returns {string|null} The absolute url or null.
     */
    static SameSite(url) {
        try {
            const u = new URL(url, window.location.href);
            return (u.protocol === "http:" || u.protocol === "https:") && u.origin === window.location.origin ? u.href : null;
        }
        catch {
            return null;
        }
    }

    /**
     * The host name of an url ("" when it can't be parsed).
     * @param {string} url The url.
     * @returns {string} The host.
     */
    static Host(url) {
        try {
            return new URL(url, window.location.href).host;
        }
        catch {
            return "";
        }
    }

    /**
     * Limit a text from the url inside the app shell (MaxAppText characters, an ellipsis marks the cut); unchanged outside.
     * @param {string} text The text.
     * @returns {string} The text to use.
     */
    static TextFromUrl(text) {
        let s = String(text ?? "");
        if (!MapView.InApp())
            return s;
        s = s.replace(MapView.AppTextStrip, " ").replace(/\s+/g, " ").trim();
        return s.length <= MapView.MaxAppText ? s : s.substring(0, MapView.MaxAppText - 1) + "…";
    }

    /**
     * The decoded size in bytes of a `data:` url (base64 or url encoded text), estimated from its text.
     * @param {string} url The data url.
     * @returns {number} Bytes.
     */
    static DataBytes(url) {
        const c = url.indexOf(",");
        const head = c >= 0 ? url.substring(0, c) : url;
        const data = c >= 0 ? url.substring(c + 1) : "";
        if (/;base64$/i.test(head))
            return Math.floor(data.replace(/[^A-Za-z0-9+/]/g, "").length * 3 / 4);
        try {
            return new TextEncoder().encode(decodeURIComponent(data)).length;
        }
        catch {
            return data.length;
        }
    }

    /**
     * True when an url of this site is a page (or file) that may be shown in the app, not a raw server method (a GET of a
     * method may perform an action, ex: ServerManager/KillProcess): a path ending in "/", a file name with an extension, or an
     * "explore" folder listing; under the api root ("Api/") only ".html" / ".htm". Uses Sw.Url.IsPageUrl when loaded, else the
     * same rule: the extension is tested on the raw (undecoded) path, and a path with an encoded delimiter (%3F, %23, %26, %2F,
     * %5C, %2E, %3B), a double encoding (%25) or an encoded control character is never a page (the server decodes before routing).
     * @param {string} url An absolute url of this site (SameSite).
     * @returns {boolean}
     */
    static IsPageUrl(url) {
        try {
            const sw = window.Sw && window.Sw.Url;
            if (sw && typeof sw.IsPageUrl === "function")
                return sw.IsPageUrl(url);
            const u = new URL(url, window.location.href);
            const raw = u.pathname;
            if (/%(3f|23|26|2f|5c|2e|3b|25|[01][0-9a-f]|7f)/i.test(raw))
                return false;
            const path = decodeURIComponent(raw).replace(/\\/g, "/");
            //  This page is one folder below the site root
            const api = new URL("../Api/", window.location.href).pathname.toLowerCase();
            if (path.toLowerCase().startsWith(api))
                return /\.html?$/i.test(raw);
            const last = raw.substring(raw.lastIndexOf("/") + 1);
            return !last || last.indexOf(".") > 0 || last.toLowerCase() === "explore";
        }
        catch {
            return false;
        }
    }

    /**
     * Show a same-site url in the app shell's content frame: through the content page's Sw.Page.Navigate("_self") when this
     * map is embedded in it (ex: MapDemo.html); when this page is the content frame itself the shell is told first
     * ("IframeNavigating", it adds a history entry) and the frame navigates itself.
     * @param {string} url An absolute url of this site (SameSite).
     */
    static NavigateInApp(url) {
        let w = window;
        try {
            while (w.parent !== w.top)
                w = w.parent;
            if (w !== window && w.Sw && w.Sw.Page && typeof w.Sw.Page.Navigate === "function") {
                w.Sw.Page.Navigate(url, "_self");
                return;
            }
            if (w === window)
                window.top.postMessage({ Type: "IframeNavigating", Url: url }, window.location.origin);
        }
        catch {
        }
        setTimeout(() => {
            w.location.href = url;
        }, 100);
    }

    /**
     * Test if an url (relative to this page) is a http(s) url (the page has no Sw.Url; same rule as Sw.Url.IsHttp).
     * @param {string} url The url.
     * @returns {boolean} True for http: and https: urls.
     */
    static IsHttp(url) {
        try {
            const p = new URL(url, window.location.href).protocol;
            return p === "http:" || p === "https:";
        }
        catch {
            return false;
        }
    }

    /**
     * Validate a css length from the url (pinX / pinY) before it goes into a css custom property: a plain number with an
     * optional unit (px, %, em, rem, vw, vh, vmin, vmax), else the fallback (no other css text from the url).
     * @param {string} v The value.
     * @param {string} fallback The value used when v is not a plain length.
     * @returns {string} A safe length.
     */
    static CssLength(v, fallback) {
        const s = String(v ?? "").trim();
        return /^\d+(\.\d+)?(px|%|em|rem|vw|vh|vmin|vmax)?$/.test(s) ? s : fallback;
    }

    /**
     * Wait.
     * @param {number} ms Milliseconds.
     * @returns {Promise<void>}
     */
    static Delay(ms) {
        return new Promise(r => setTimeout(r, ms));
    }

    /**
     * Parse a coordinate: keeps digits, "." and "-"; a trailing S / W (when neg is that letter) negates.
     * @param {string} x The text.
     * @param {string} neg The letter that negates ("s" or "w").
     * @returns {number} The value.
     * @throws {Error} For an invalid coordinate.
     */
    static Coordinate(x, neg) {
        let y = "";
        for (const c of x)
            if ((c >= "0" && c <= "9") || c === "." || c === "-")
                y += c;
        const val = parseFloat(y);
        if (!y || isNaN(val))
            throw new Error(_T("Invalid coordinate: {0}", x, "Error of the map view, {0} is the bad coordinate text"));
        const t = x.trim();
        return t.charAt(t.length - 1).toLowerCase() === neg ? -val : val;
    }

    /**
     * Find the Google Maps key (url, else the api).
     * @param {URLSearchParams} p Url parameters.
     * @returns {Promise<string|null>} The key or null.
     */
    static async GetKey(p) {
        let key = p.get("key");
        if (!key) {
            try {
                const res = await fetch(new Request("Api/GetGoogleMapsKey", { method: "GET", mode: "cors", cache: "default", redirect: "manual" }));
                if (res.status === 200)
                    key = await res.json();
            }
            catch {
            }
        }
        return key ? ("" + key).trim() : null;
    }

    /**
     * Find the screenshot host (CefSharp binding, or the WebView2 host object).
     * @returns {Promise<object|null>} The host or null.
     */
    static async GetScreenShotHost() {
        if (window.CefSharp)
            await CefSharp.BindObjectAsync();
        try {
            return window.ScreenShotHost ?? window.chrome?.webview?.hostObjects?.ScreenShotHost ?? null;
        }
        catch {
            return null;
        }
    }

    /**
     * Page main.
     * @returns {Promise<void>}
     */
    static async Main() {
        const p = new URL(window.location.href).searchParams;
        const b = document.body;
        try {
            let maptype = (p.get("maptype") ?? "roadmap").toLowerCase();
            let isOsm = MapView.OpenStreetMap.has(maptype);
            let key = null;
            //  Online like the original: an OpenStreetMap type asks nothing, a Google type asks the key (url, else the api)
            if (!isOsm) {
                key = await MapView.GetKey(p);
                if (!key) {
                    console.warn('Must supply a google map API key using the "key" parameter, switching to open street map!');
                    maptype = maptype === "dark" ? "opendark" : "open";
                    isOsm = true;
                }
            }
            if (isOsm) {
                if (MapView.OpenStreetMap.get(maptype) === 2)
                    b.classList.add("OpenDark");
            } else if (maptype === "dark") {
                b.classList.add("Dark");
                maptype = "roadmap";
            }

            const centerText = p.get("center");
            if (!centerText)
                throw new Error(_TF('Must supply a coordinate the "center" parameter', "Error of the map view without the center url parameter"));
            const parts = centerText.split(",");
            if (parts.length !== 2)
                throw new Error(_TF("Must use comma separated coordinates", "Error of the map view when the center url parameter is not lat,lng"));
            const center = MapView.Coordinate(parts[0], "s") + "," + MapView.Coordinate(parts[1], "w");

            const dpi = parseFloat(p.get("dpi") ?? "100") || 100;
            const zoom = parseInt(p.get("zoom") ?? "16") || 16;
            //  Url encoded once here (it goes into the Google links and the embed url)
            const lang = encodeURIComponent(p.get("language") ?? p.get("lang") ?? "en");
            const interactive = p.get("interactive") === "true";

            //  Link / title (a custom title comes from the url: plain text, limited inside the app shell)
            const inApp = MapView.InApp();
            let link = p.get("link");
            let title = p.get("title") ? MapView.TextFromUrl(p.get("title")) : null;
            if (link) {
                const view = _TF("View using Google Maps", "Title of an image of a map that when selected will open Google Maps in a new tab");
                const linkMap = new Map([
                    ["", [null, null]],
                    ["false", [null, null]],
                    ["null", [null, null]],
                    ["true", ["https://www.google.com/maps/search/" + center + "?hl=" + lang, view]],
                    ["view", ["https://www.google.com/maps/search/" + center + "?hl=" + lang, view]],
                    ["dir", ["https://www.google.com/maps/dir/ /" + center + "?hl=" + lang, _TF("Get directions using Google Maps", "Title of an image of a map that when selected will open Google Maps in a new tab")]],
                ]);
                if (linkMap.has(link)) {
                    b.classList.add("Link");
                    const d = linkMap.get(link);
                    link = d[0];
                    title = title || d[1];
                } else
                    title = title || _T('Open "{0}"', MapView.TextFromUrl(link), "Title of an image of a map that when selected will open a page. {0} will be replaced with the address of the page");
                //  Security: only http(s) links (a crafted "javascript:" link must not run when the map is clicked)
                if (link && !MapView.IsHttp(link)) {
                    console.warn("MapView: ignored a link parameter that is not a http(s) url");
                    link = null;
                }
                //  Security: inside the app shell a link of this site must be a page, never a raw server method (a GET may perform
                //  an action, ex: Api/... or ServerManager/KillProcess)
                if (link && inApp && MapView.SameSite(link) && !MapView.IsPageUrl(link)) {
                    console.warn("MapView: ignored a link parameter that is not a page of this site");
                    link = null;
                }
            }
            if (!interactive)
                b.classList.add("NoInteraction");

            //  Pin
            let pin = p.get("pin") || null;
            let pinX = p.get("pinX") || "50%";
            let pinY = p.get("pinY") || "100%";
            let pinHeight = p.get("pinHeight");
            //  True for an own pin picture (not a preset)
            let ownPin = false;
            if (pin) {
                const d = MapView.Pins.get(pin);
                if (d) {
                    pin = d[0];
                    pinX = p.get("pinX") || d[1];
                    pinY = p.get("pinY") || d[2];
                    pinHeight = pinHeight || d[3];
                } else if (pin === "false" || pin === "null")
                    pin = null;
                else if (/^data:image\//i.test(pin)) {
                    //  An image in the url itself (no request); inside the app shell limited in size
                    if (inApp && MapView.DataBytes(pin) > MapView.MaxPinBytes) {
                        console.warn("MapView: ignored a pin picture larger than " + MapView.MaxPinBytes + " bytes inside the application");
                        pin = null;
                    }
                } else if (!MapView.IsHttp(pin)) {
                    //  Security: an own pin image must be a web address
                    console.warn("MapView: ignored a pin parameter that is not a http(s) url");
                    pin = null;
                } else if (inApp && !MapView.SameSite(pin)) {
                    //  Security: inside the app shell no image of another site (a tracking pixel, a foreign picture in the app)
                    console.warn("MapView: ignored a pin image of another site inside the application");
                    pin = null;
                }
                pinHeight = pinHeight || 96;
                ownPin = !!pin && !d;
            }
            //  Inside the app shell a pin is at most MaxPinPx high (and wide, see below): a picture from the link can't cover the map
            if (pin && inApp)
                pinHeight = Math.min(parseFloat(pinHeight) || 96, MapView.MaxPinPx);
            const ds = document.documentElement.style;
            ds.setProperty("--dpi", "" + (dpi / 100.0));
            ds.setProperty("--idpi", "" + (100.0 / dpi));
            //  Only plain lengths from the url go into the css (no css text such as url(...))
            ds.setProperty("--pinX", "-" + MapView.CssLength(pinX, "50%"));
            ds.setProperty("--pinY", "-" + MapView.CssLength(pinY, "100%"));
            ds.setProperty("--pinHeight", (parseFloat(pinHeight) || 96) + "px");

            //  Screenshot host
            const shost = await MapView.GetScreenShotHost();
            if (shost) {
                await shost.isSupported();
                b.classList.add("NoFade");
            }

            let url;
            if (isOsm) {
                const scale = zoom < 16 ? (1 << (16 - zoom)) : (1.0 / (1 << (zoom - 16)));
                const w = 0.012 * scale * window.innerWidth / 1920.0;
                const h = 0.0035 * scale * window.innerHeight / 1080.0;
                const cs = center.split(",");
                const x = parseFloat(cs[1]);
                const y = parseFloat(cs[0]);
                url = "https://www.openstreetmap.org/export/embed.html?bbox=" + (x - w) + "," + (y - h) + "," + (x + w) + "," + (y + h);
            } else
                url = "https://www.google.com/maps/embed/v1/view?key=" + encodeURIComponent(key) + "&center=" + center + "&zoom=" + zoom + "&language=" + lang + "&maptype=" + encodeURIComponent(maptype);

            const f = document.createElement("iframe");
            f.setAttribute("frameborder", "0");
            f.setAttribute("referrerpolicy", "no-referrer-when-downgrade");
            f.setAttribute("allowfullscreen", "");
            f.title = title || _TF("Map", "Accessible name of the map frame");
            if (!interactive)
                f.tabIndex = -1;
            b.appendChild(f);
            f.onload = async () => {
                await MapView.Delay(100);
                b.classList.add("Show");
                if (shost) {
                    await MapView.Delay(500);
                    await shost.doIt(0, 0, null);
                }
            };
            f.onerror = () => {
                b.textContent = _TF("Error loading map!", "Error of the map view");
                b.classList.add("Show");
            };
            if (pin) {
                const pinE = document.createElement("img");
                pinE.alt = "";
                if (inApp) {
                    pinE.style.maxWidth = MapView.MaxPinPx + "px";
                    pinE.style.objectFit = "contain";
                }
                pinE.src = pin;
                b.appendChild(pinE);
            }
            if (link) {
                const a = document.createElement("a");
                a.href = link;
                const same = MapView.SameSite(link);
                //  The title is plain, limited text (no "Text from the link" caption on titles: owner OWN3-Shell-2)
                let label = title;
                if (inApp && same) {
                    //  This site: shown in the app (the content frame), not in a new tab; modified clicks keep the browser behavior
                    a.addEventListener("click", ev => {
                        if (ev.button !== 0 || ev.ctrlKey || ev.metaKey || ev.shiftKey || ev.altKey)
                            return;
                        ev.preventDefault();
                        MapView.NavigateInApp(same);
                    });
                } else {
                    a.target = "_blank";
                    a.rel = "noopener noreferrer";
                    if (inApp) {
                        //  Another site: say so on the map
                        const ext = _T("Opens {0} in a new tab", MapView.Host(link), "Badge on a map view inside the application whose link opens another web site. {0} is the host name of that site");
                        const e = document.createElement("Map-External");
                        e.textContent = ext + " ↗";
                        e.setAttribute("aria-hidden", "true");
                        b.appendChild(e);
                        label = (label ? label + " - " : "") + ext;
                    }
                }
                if (label) {
                    a.title = label;
                    a.setAttribute("aria-label", label);
                }
                b.appendChild(a);
            } else if (title && !interactive) {
                const t = document.createElement("Map-Title");
                t.title = title;
                b.appendChild(t);
            }
            if (ownPin && inApp) {
                //  An own pin picture (it may show text too) is marked as a picture from the link (the title is a title: plain,
                //  limited text, no caption, owner OWN3-Shell-2)
                const c = document.createElement("Map-Caption");
                c.setAttribute("aria-hidden", "true");
                {
                    const l = document.createElement("strong");
                    l.textContent = _TF("The pin is a picture from the link", "Caption on a map view inside the application when its pin is a picture given in the page address (a link), not one of the application");
                    c.append(l);
                }
                b.appendChild(c);
            }
            f.src = url;
        }
        catch (e) {
            console.log("Error: " + e.message);
            b.textContent = e.message;
            b.classList.add("Show");
        }
    }
}

(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    (Sw.Media = Sw.Media || {}).MapView = MapView;
    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", () => MapView.Main(), { once: true });
    else
        MapView.Main();
})();
