/**
 * @file core/kernel.js - the kernel of the SysWeaver client library (namespace `Sw`): the parts that every bundle needs, also the small
 * messaging bundle sw/session.js.
 *
 * Purpose:
 *  - Creates the `window.Sw` namespace, `Sw.Version`, the diagnostic log `Sw.Log` (Sw.Debug) and the developer warnings `Sw.DevMode` /
 *    `Sw.DevWarn` (page author warnings, only in developer mode).
 *  - `Sw.Emitter` (a minimal synchronous named event emitter) and the shared core event bus `Sw.Events`.
 *  - `Sw.Util.Delay`, `Sw.Util.WaitEvent` and the shared frame loop `Sw.Util.Animator` (the rest of Sw.Util is in core/base.js).
 *  - `Sw.Platform`: device / platform detection (computed once) and geolocation.
 *
 * Load order: first file of every bundle (sw/session.js, sw/core.js, common/common.js, sw/net.js); core/base.js extends it.
 * Works concatenated or loaded on its own. Dependencies: browser APIs only.
 * Globals defined: `window.Sw` (only). No visible UI.
 */
(function () {
    "use strict";

    /** @namespace Sw - root namespace of the SysWeaver client library (core + ui). */
    const Sw = window.Sw = window.Sw || {};

    /** @type {string} Version of the Sw core library. */
    Sw.Version = "2.0.0";

    /**
     * @type {boolean} Diagnostic console logging (Sw.Log), off by default. On when localStorage "SysWeaver.Debug" is "1" or
     * "true" (set it in the browser console and reload), or set Sw.Debug = true at run time.
     */
    Sw.Debug = typeof Sw.Debug === "boolean" ? Sw.Debug : (() => {
        try {
            const v = window.localStorage.getItem("SysWeaver.Debug");
            return v === "1" || v === "true";
        }
        catch {
            return false;
        }
    })();

    /**
     * Log diagnostic information (session master election, server time, theme, reloads) to the console, only when Sw.Debug
     * is true. Warnings and errors use console.warn / console.error directly.
     * @param {...*} args Values to log.
     */
    Sw.Log = (...args) => {
        if (Sw.Debug)
            console.log(...args);
    };

    /**
     * True when developer warnings are shown (Sw.DevWarn): the one switch for every framework warning to page authors (core, ui,
     * dialogs, shell). Order: localStorage "SysWeaver.DevWarn" = "0" (off, ex: a quiet demo on localhost) or "1" (on, also on a
     * production host name); else Sw.Debug; else on for a page served from localhost (the developer's own machine), off elsewhere
     * (production: users never see them). A page can replace it (Sw.DevMode = () => false, or a boolean).
     * @returns {boolean} True to warn.
     */
    Sw.DevMode = () => {
        try {
            const v = window.localStorage.getItem("SysWeaver.DevWarn");
            if (v === "0" || v === "false")
                return false;
            if (v === "1" || v === "true")
                return true;
        }
        catch {
            //  no storage: the defaults
        }
        if (Sw.Debug)
            return true;
        const h = location.hostname;
        return h === "localhost" || h === "127.0.0.1" || h === "::1" || h === "[::1]";
    };

    /** @type {Set<string>} The developer warnings shown already in this page. */
    const devWarned = new Set();

    /**
     * Warn the page author about a misuse of the framework (console.warn "[Sw UX] ...", once per key and page; only in Sw.DevMode(), so
     * users never see it). Ex: a draft that is never cleared, an activity listener of a framed page, a page that never says it loaded.
     * @param {string} msg The message (English, for developers: not translated).
     * @param {string} [key] De-duplication key (default the message).
     */
    Sw.DevWarn = Sw.DevWarn || ((msg, key) => {
        key = key || msg;
        if (devWarned.has(key) || !(typeof Sw.DevMode === "function" ? Sw.DevMode() : !!Sw.DevMode))
            return;
        devWarned.add(key);
        console.warn("[Sw UX] " + msg);
    });

    /**
     * Minimal named event emitter (synchronous dispatch, handlers called in registration order).
     * Handler exceptions are caught and logged so one failing handler does not stop the others.
     * Usage: const off = emitter.On("name", (a, b) => ...); emitter.Emit("name", 1, 2); off();
     */
    class Emitter {

        /** Creates an empty emitter. */
        constructor() {
            /** @type {Map<string, Function[]>} Event name -> handlers (private, do not touch). */
            this.Map = new Map();
        }

        /**
         * Add a handler for an event name.
         * @param {string} name - Event name (case sensitive).
         * @param {Function} fn - Handler, called with the arguments passed to Emit.
         * @returns {function():void} A function that removes the handler again (same as Off(name, fn)).
         */
        On(name, fn) {
            let list = this.Map.get(name);
            if (!list) {
                list = [];
                this.Map.set(name, list);
            }
            list.push(fn);
            return () => this.Off(name, fn);
        }

        /**
         * Add a handler that is removed after its first invocation.
         * @param {string} name - Event name.
         * @param {Function} fn - Handler.
         * @returns {function():void} A function that removes the handler (if not yet called).
         */
        Once(name, fn) {
            const t = this;
            /** Wrapper removing itself before calling fn. @param {...*} args - Emit arguments. @returns {*} */
            function once(...args) {
                t.Off(name, once);
                return fn.apply(this, args);
            }
            return t.On(name, once);
        }

        /**
         * Remove a handler added with On.
         * @param {string} name - Event name.
         * @param {Function} fn - The handler.
         * @returns {boolean} true if the handler was found and removed.
         */
        Off(name, fn) {
            const list = this.Map.get(name);
            if (!list)
                return false;
            const i = list.lastIndexOf(fn);
            if (i < 0)
                return false;
            list.splice(i, 1);
            if (list.length <= 0)
                this.Map.delete(name);
            return true;
        }

        /**
         * Test if an event has any handlers.
         * @param {string} name - Event name.
         * @returns {boolean} true if at least one handler is registered.
         */
        Has(name) {
            const list = this.Map.get(name);
            return !!list && (list.length > 0);
        }

        /**
         * Call all handlers of an event synchronously (a copy of the handler list is used, so handlers may
         * add or remove handlers). Returned promises are not awaited (use EmitAsync for that).
         * @param {string} name - Event name.
         * @param {...*} args - Arguments for the handlers.
         * @returns {Array<*>} The values returned by the handlers (in order).
         */
        Emit(name, ...args) {
            const list = this.Map.get(name);
            if (!list)
                return [];
            const fns = list.slice();
            const res = [];
            const l = fns.length;
            for (let i = 0; i < l; ++i) {
                try {
                    res.push(fns[i](...args));
                }
                catch (e) {
                    console.error("Sw.Events: handler of \"" + name + "\" failed: " + (e && e.message ? e.message : e));
                }
            }
            return res;
        }

        /**
         * Call all handlers of an event in order, awaiting each (exceptions are logged, not thrown).
         * @param {string} name - Event name.
         * @param {...*} args - Arguments for the handlers.
         * @returns {Promise<Array<*>>} The (awaited) values returned by the handlers.
         */
        async EmitAsync(name, ...args) {
            const list = this.Map.get(name);
            if (!list)
                return [];
            const fns = list.slice();
            const res = [];
            const l = fns.length;
            for (let i = 0; i < l; ++i) {
                try {
                    res.push(await fns[i](...args));
                }
                catch (e) {
                    console.error("Sw.Events: handler of \"" + name + "\" failed: " + (e && e.message ? e.message : e));
                }
            }
            return res;
        }
    }

    /** @type {typeof Emitter} The emitter class (create private emitters with new Sw.Emitter()). */
    Sw.Emitter = Emitter;

    /**
     * @type {Emitter} The global core event bus. Event names used by the core:
     *  - "server.block" {Type, Title, Text, Icon}: a top window should block input (server paused / restarting / lost).
     *  - "server.unblock" {Type}: the server is available again.
     *  - "server.message" (msg): every server push message that reached this window (also emitted by type name
     *    for the forced types, e.g. "user.login", "filereload").
     *  - "net.error" {Url, Status, Error}: a request made with Sw.Net failed.
     *  - "auth.redirect" {Url}: a request was redirected (typically to the login page); the page navigates there.
     *  - "theme.changed" {Theme, IsDark}: a theme was applied.
     *  - "layout.changed" (Sw.Layout): any layout value changed.
     *  - "page.loading" / "page.loaded", "page.error" {Error}, "page.interacted" (Event).
     */
    Sw.Events = Sw.Events || new Emitter();

    // ------------------------------------------------------------------ Async helpers

    /**
     * Shared requestAnimationFrame loop: every added function is called once per frame with the time (ms)
     * since its own first frame. A function returning true is removed. The loop stops when no functions
     * remain. Exceptions in functions are swallowed.
     */
    const Animator = {

        /** @type {Map<function(number):boolean, number>} Function -> start time stamp (-1 until its first frame). */
        Functions: new Map(),

        /** @type {boolean} True while the frame loop is running. */
        Running: false,

        /**
         * Add a function that should be executed every frame.
         * @param {function(number):boolean} func - Called with the elapsed time in ms; return true to stop.
         */
        Add(func) {
            const fns = Animator.Functions;
            fns.set(func, -1);
            if (Animator.Running)
                return;
            Animator.Running = true;
            /** The frame loop. @param {number} timeStamp - requestAnimationFrame time stamp. */
            const fn = timeStamp => {
                if (fns.size <= 0) {
                    Animator.Running = false;
                    return;
                }
                fns.forEach((value, key) => {
                    if (value < 0) {
                        value = timeStamp;
                        fns.set(key, value);
                    }
                    try {
                        if (key(timeStamp - value))
                            fns.delete(key);
                    }
                    catch {
                    }
                });
                if (fns.size <= 0) {
                    Animator.Running = false;
                    return;
                }
                window.requestAnimationFrame(fn);
            };
            window.requestAnimationFrame(fn);
        },

        /**
         * Stop a function from being executed every frame (not needed if it returned true).
         * @param {function(number):boolean} func - A function previously added.
         */
        Remove(func) {
            if (func)
                Animator.Functions.delete(func);
        },
    };

    Sw.Util = Object.assign(Sw.Util || {}, {

        /** Shared requestAnimationFrame loop (Add(fn), Remove(fn)). */
        Animator: Animator,

        /**
         * Delay execution.
         * @param {number} ms - Milliseconds to wait.
         * @returns {Promise<void>}
         */
        Delay: ms => new Promise(resolve => setTimeout(resolve, ms)),

        /**
         * Wait for the first of one or more events on a target.
         * @param {EventTarget} target - Element, window, document or any EventTarget.
         * @param {string|string[]} names - Event name(s).
         * @param {number} [timeout] - Optional time out in ms (> 0); resolves null on time out.
         * @param {function(EventTarget):void} [beforeFn] - Called after the listeners are attached (e.g. to trigger the event).
         * @returns {Promise<Event|null>} The event, or null on time out.
         */
        WaitEvent(target, names, timeout, beforeFn) {
            const list = Array.isArray(names) ? names : [names];
            return new Promise(resolve => {
                let th = null;
                /** Finish: detach and resolve. @param {Event|null} ev - The event or null. */
                const end = ev => {
                    if (th)
                        clearTimeout(th);
                    th = null;
                    list.forEach(n => target.removeEventListener(n, end));
                    resolve(ev);
                };
                list.forEach(n => target.addEventListener(n, end));
                if (beforeFn)
                    beforeFn(target);
                if (timeout && (timeout > 0))
                    th = setTimeout(() => end(null), timeout);
            });
        },
    });

    // ------------------------------------------------------------------ Platform

    /** @type {boolean|null} Cached result of the mobile device test. */
    let isMobileCached = null;

    /**
     * Test if this is likely a mobile device (navigator.userAgentData.mobile, else the detectmobilebrowsers
     * user agent expressions). Computed once.
     * @returns {boolean}
     */
    function isMobileDevice() {
        if (isMobileCached !== null)
            return isMobileCached;
        const p = navigator.userAgentData;
        if (p && (typeof p.mobile === "boolean"))
            return (isMobileCached = p.mobile);
        const a = navigator.userAgent || navigator.vendor || window.opera || "";
        isMobileCached = /(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows ce|xda|xiino/i.test(a) || /1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-/i.test(a.substr(0, 4));
        return isMobileCached;
    }

    /**
     * Get a lower-case platform name ("windows", "macos", "ios", "android", "linux", or what the browser reports;
     * "unknown" if nothing is known). Same algorithm as the old getPlatform.
     * @returns {string}
     */
    function platformName() {
        const p = navigator.userAgentData;
        if (p && p.platform)
            return p.platform.toLowerCase();
        const u = (navigator.userAgent || "").toLowerCase();
        const np = navigator.platform;
        if (typeof np === "string") {
            if (/android/i.test(u))
                return "android";
            return np.toLowerCase();
        }
        if (!u)
            return "unknown";
        if (/(macintosh|macintel|macppc|mac68k|macos)/i.test(u))
            return "macos";
        if (/(iphone|ipad|ipod)/i.test(u))
            return "ios";
        if (/(win32|win64|windows|wince)/i.test(u))
            return "windows";
        if (/android/i.test(u))
            return "android";
        if (/linux/i.test(u))
            return "linux";
        return "unknown";
    }

    const ua = navigator.userAgent || "";

    /**
     * @namespace Sw.Platform - device / platform detection (values computed once at load).
     */
    Sw.Platform = {
        /** @type {boolean} Likely a mobile device (user agent data / user agent). */
        IsMobileDevice: isMobileDevice(),
        /** @type {boolean} Touch input is available. */
        IsTouch: (navigator.maxTouchPoints > 0) || ("ontouchstart" in window),
        /** @type {boolean} Apple macOS. */
        IsMac: /mac/i.test(navigator.platform || "") || /macintosh/i.test(ua),
        /** @type {boolean} Apple iOS / iPadOS (iPads report as Mac with touch). */
        IsIos: /(iphone|ipad|ipod)/i.test(ua) || (/macintosh/i.test(ua) && (navigator.maxTouchPoints > 1)),
        /** @type {boolean} Android. */
        IsAndroid: /android/i.test(ua),
        /** @type {boolean} Hosted in a WebView2 control (chrome.webview available). */
        IsWebView2: !!(window.chrome && window.chrome.webview),
        /** @type {boolean} Hosted in CefSharp. */
        IsCef: typeof window.CefSharp !== "undefined",
        /** @type {string} Lower-case platform name (see platformName). */
        Name: platformName(),

        /**
         * Mobile layout parameters (same as the old getMobileParams):
         * [0] is mobile device, [1] prefer a single column (portrait and (mobile or width <= 400/800 px)),
         * [2] is portrait, [3] scale (mobile only: width/max above max, width/min below min).
         * @returns {Array<boolean|number>}
         */
        MobileParams() {
            const isMob = Sw.Platform.IsMobileDevice;
            const width = window.innerWidth;
            const height = window.innerHeight;
            const isPortrait = width < height;
            const zoomIn = isMob && isPortrait;
            const minScale = zoomIn ? 200 : 400;
            const maxScale = zoomIn ? 400 : 800;
            const single = isPortrait && ((width <= maxScale) || isMob);
            let scale = 1;
            if (isMob) {
                if (width > maxScale)
                    scale = width / maxScale;
                else if (width < minScale)
                    scale = width / minScale;
            }
            return [isMob, single, isPortrait, scale];
        },

        /**
         * Get the geo location of the device.
         * @param {boolean} [lowPrecision] - Low precision is acceptable (usually faster).
         * @param {number} [timeout] - Max time in ms (default Infinity).
         * @param {number} [maximumAge] - Max age of a cached position in ms (default 0).
         * @returns {Promise<GeolocationPosition>} Throws an Error on failure.
         */
        GetPosition(lowPrecision, timeout, maximumAge) {
            return new Promise((resolve, reject) => {
                try {
                    navigator.geolocation.getCurrentPosition(resolve, e => reject(new Error(e.message)), {
                        timeout: (timeout && timeout > 0) ? timeout : Infinity,
                        maximumAge: (maximumAge && maximumAge > 0) ? maximumAge : 0,
                        enableHighAccuracy: !lowPrecision,
                    });
                }
                catch (e) {
                    reject(e);
                }
            });
        },
    };
})();
