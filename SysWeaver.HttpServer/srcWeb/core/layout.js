/**
 * @file core/layout.js - responsive layout state, classes and events.
 *
 * Purpose: `Sw.Layout` (same behavior and names as the old SysWeaverInit layout code):
 *  - Mobile: url "mobile" (true/false/1/0), else localStorage "SysWeaver.Mobile" (json), else the value of a
 *    same-origin SysWeaver parent window, else Sw.Platform.IsMobileDevice. Classes "SwMobile" or "SwDesktop".
 *  - Portrait (width <= height) and Narrow (width <= 600 px) follow the top SysWeaver window: a same-origin child
 *    frame takes them from its parent (new or old library); "Self" values are this window's own.
 *    Classes on html and body: SwPortrait/SwLandscape, SwNarrow/SwWide, SwSelfPortrait/SwSelfLandscape,
 *    SwSelfNarrow/SwSelfWide.
 *  - Events on change: CustomEvents on body (detail = the boolean): "SwOrientation" + "SwPortrait"/"SwLandscape",
 *    "SwLayout" + "SwNarrow"/"SwWide", "SwSelfOrientation" + "SwSelfPortrait"/"SwSelfLandscape",
 *    "SwSelfLayout" + "SwSelfNarrow"/"SwSelfWide"; plus Sw.Events "layout.changed" (Sw.Layout).
 *  - The orientation stays while the on screen keyboard is open (Keyboard, core/keyboard.js).
 *  - Page classes (on html at once, on body when it exists): "Transparent" (url transparent=true, or a child
 *    frame unless transparent=false), "FillScreen" (url fillscreen) and "SwNested" (framed by a page of this site that is not the
 *    app shell: one page padding level, see nestedInPage).
 *
 * Load order: after core/session.js (core/session_start.js calls Init). Dependencies: Sw.Url, Sw.Storage, Sw.Platform, Sw.Events; Sw.Theme.Density when present.
 * Globals defined: none (Sw.Layout).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** Narrow breakpoint in css px (width <= this is narrow). */
    const narrowWidth = 600;

    /** @type {object|null} The parent window's layout source (Sw.Layout of a new parent, or the old parent window). */
    let parentSrc = null;

    /** @type {boolean} True if this is a child frame of a same-origin SysWeaver window. */
    let isChild = false;

    /** @type {boolean} True once Init ran. */
    let inited = false;

    /** @type {number} Last handled inner width. */
    let lastW = -1;

    /** @type {number} Last handled inner height. */
    let lastH = -1;

    /**
     * Parse a mobile override ("true"/"false"/"1"/"0"/json), null if not usable.
     * @param {string|null} v - Value.
     * @returns {boolean|null}
     */
    function parseBool(v) {
        if (v === null || typeof v === "undefined")
            return null;
        v = ("" + v).trim().toLowerCase();
        if (v === "true" || v === "1" || v === "")
            return true;
        if (v === "false" || v === "0")
            return false;
        return null;
    }

    /** Find the parent's layout source (same origin SysWeaver window, new or old library). */
    function findParent() {
        try {
            const p = window.parent;
            if (p === window)
                return;
            const sl = p.Sw && p.Sw.Layout;
            if (sl && (typeof sl.Mobile === "boolean")) {
                parentSrc = sl;
                isChild = true;
                return;
            }
            if (typeof p.Mobile === "boolean") {
                parentSrc = p;
                isChild = true;
            }
        }
        catch {
        }
    }

    /**
     * Set one of two exclusive classes on html and body.
     * @param {boolean} on - Which one.
     * @param {string} a - Class when on.
     * @param {string} b - Class when off.
     */
    function setPair(on, a, b) {
        const add = on ? a : b;
        const rem = on ? b : a;
        const h = document.documentElement.classList;
        if (!h.contains(add) || h.contains(rem)) {
            h.add(add);
            h.remove(rem);
        }
        const body = document.body;
        if (body) {
            const l = body.classList;
            if (!l.contains(add) || l.contains(rem)) {
                l.add(add);
                l.remove(rem);
            }
        }
    }

    /**
     * Dispatch a pair of layout events on body.
     * @param {string} general - The general event name (e.g. "SwLayout").
     * @param {boolean} v - The value (detail).
     * @param {string} a - Specific name when true.
     * @param {string} b - Specific name when false.
     */
    function fire(general, v, a, b) {
        const body = document.body;
        if (!body)
            return;
        const o = { detail: v };
        body.dispatchEvent(new CustomEvent(general, o));
        body.dispatchEvent(new CustomEvent(v ? a : b, o));
    }

    /**
     * Recompute the layout (when the size changed or force is set), update classes and fire events.
     * @param {boolean} [force] - Recompute even if the window size is unchanged.
     */
    function update(force) {
        const ww = window.innerWidth;
        const wh = window.innerHeight;
        if (!force && (ww === lastW) && (wh === lastH))
            return;
        lastW = ww;
        lastH = wh;
        const L = Sw.Layout;
        const swn = ww <= narrowWidth;
        let wp = null;
        let wn = swn;
        if (isChild && parentSrc) {
            try {
                if (typeof parentSrc.Portrait === "boolean")
                    wp = parentSrc.Portrait;
                if (typeof parentSrc.Narrow === "boolean")
                    wn = parentSrc.Narrow;
            }
            catch {
            }
        }
        //  The on screen keyboard makes the window lower: the orientation stays
        const swp = (L.Keyboard && (typeof L.SelfPortrait === "boolean")) ? L.SelfPortrait : ww <= wh;
        if (wp === null)
            wp = swp;
        const nwp = L.Portrait !== wp;
        const nwn = L.Narrow !== wn;
        const snwp = L.SelfPortrait !== swp;
        const snwn = L.SelfNarrow !== swn;
        L.Width = ww;
        L.Height = wh;
        L.Portrait = wp;
        L.Narrow = wn;
        L.SelfPortrait = swp;
        L.SelfNarrow = swn;
        setPair(wp, "SwPortrait", "SwLandscape");
        setPair(wn, "SwNarrow", "SwWide");
        setPair(swp, "SwSelfPortrait", "SwSelfLandscape");
        setPair(swn, "SwSelfNarrow", "SwSelfWide");
        if (!document.body) {
            //  Events need the body, re-evaluate when it exists
            L.Portrait = L.Narrow = L.SelfPortrait = L.SelfNarrow = null;
            lastW = lastH = -1;
            return;
        }
        if (nwp)
            fire("SwOrientation", wp, "SwPortrait", "SwLandscape");
        if (nwn)
            fire("SwLayout", wn, "SwNarrow", "SwWide");
        if (snwp)
            fire("SwSelfOrientation", swp, "SwSelfPortrait", "SwSelfLandscape");
        if (snwn)
            fire("SwSelfLayout", swn, "SwSelfNarrow", "SwSelfWide");
        if (nwp || nwn || snwp || snwn)
            Sw.Events.Emit("layout.changed", L);
    }

    /**
     * Test if this page is framed by another page of this site that is not the app shell (a dashboard of frames, a chart wall, an
     * overview with a preview frame; frames created or pointed from code too): the host page already has the page padding, so this
     * page drops its own (class SwNested, ui/tokens.css: --sw-page-px 0), one padding level. An iframe with data-sw-page-padding keeps it.
     * @returns {boolean}
     */
    function nestedInPage() {
        if (!isChild)
            return false;
        try {
            const fe = window.frameElement;
            if (fe && fe.hasAttribute("data-sw-page-padding"))
                return false;
            const p = window.parent;
            return !(p.document.body && p.document.body.classList.contains("sw-shell")) && !p.SysWeaverApp;
        }
        catch {
            return false;
        }
    }

    /** Apply the device and page classes (html now, body when available). */
    function applyPageClasses() {
        const L = Sw.Layout;
        const add = [L.Mobile ? "SwMobile" : "SwDesktop"];
        const tr = Sw.Url.Get("transparent");
        if (tr === "true" || (isChild && (tr !== "false")))
            add.push("Transparent");
        if (Sw.Url.Get("fillscreen") !== null)
            add.push("FillScreen");
        if (nestedInPage())
            add.push("SwNested");
        L.IsTransparent = add.indexOf("Transparent") >= 0;
        const h = document.documentElement.classList;
        add.forEach(c => h.add(c));
        if (document.body)
            add.forEach(c => document.body.classList.add(c));
    }

    /**
     * @namespace Sw.Layout - responsive layout state.
     */
    Sw.Layout = {

        /** @type {number} Narrow breakpoint (css px). */
        NarrowWidth: narrowWidth,

        /** @type {boolean|null} Mobile mode (set by Init). */
        Mobile: null,

        /** @type {boolean|null} Portrait (of the top SysWeaver window). */
        Portrait: null,

        /** @type {boolean|null} Narrow (of the top SysWeaver window). */
        Narrow: null,

        /** @type {boolean|null} This window's own portrait state. */
        SelfPortrait: null,

        /** @type {boolean|null} This window's own narrow state. */
        SelfNarrow: null,

        /** @type {number} Last handled window inner width. */
        Width: 0,

        /** @type {number} Last handled window inner height. */
        Height: 0,

        /** @type {boolean} True if the Transparent class is applied. */
        IsTransparent: false,

        /** @type {boolean} True if this is a child frame of a same-origin SysWeaver window. */
        get IsChild() {
            return isChild;
        },

        /**
         * Initialize (once): mobile detection, page classes, size tracking (window resize, passive).
         */
        Init() {
            if (inited)
                return;
            inited = true;
            findParent();
            let mob = parseBool(Sw.Url.Get("mobile"));
            if (mob === null)
                mob = parseBool(Sw.Storage.Get("SysWeaver.Mobile", null));
            if (mob === null)
                mob = (isChild && parentSrc) ? !!parentSrc.Mobile : Sw.Platform.IsMobileDevice;
            Sw.Layout.Mobile = mob;
            applyPageClasses();
            //  The automatic density depends on the mobile mode (core/theme.js)
            if (Sw.Theme && Sw.Theme.Density)
                Sw.Theme.Density.Apply();
            update(true);
            window.addEventListener("resize", () => {
                //  The keyboard first (core/keyboard.js): the orientation stays while it is open
                if (Sw.Layout.KeyboardCheck)
                    Sw.Layout.KeyboardCheck();
                update(false);
            }, { passive: true });
            /** Body dependent work once the DOM is parsed and again at load (as before). */
            const onReady = () => {
                applyPageClasses();
                update(true);
            };
            if (document.readyState === "loading")
                document.addEventListener("DOMContentLoaded", onReady, { once: true });
            else
                onReady();
            window.addEventListener("load", onReady, { once: true });
        },

        /**
         * Re-evaluate the layout now (e.g. after the parent's layout changed).
         * @param {boolean} [force=true] - Recompute even if the size is unchanged.
         */
        Update(force) {
            update(force !== false);
        },

        /**
         * Call fn when any layout value changed.
         * @param {function(object):void} fn - Handler (receives Sw.Layout).
         * @returns {function():void} Removes the handler.
         */
        OnChange: fn => Sw.Events.On("layout.changed", fn),
    };
})();
