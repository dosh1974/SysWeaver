/**
 * @file core/keyboard.js - detects the on screen keyboard of the top window (the state is Sw.Layout.Keyboard, core/layout.js).
 *
 * Purpose: the keyboard is open when a text field has the focus on a touch device and the visible area got at least 120 px lower:
 * the window itself (Android, interactive-widget=resizes-content, which the app shell asks for) or only the visual viewport (iOS,
 * Android without it: the keyboard covers the bottom of the window, Sw.Layout.KeyboardInset). Then the focused field is scrolled into
 * view above the keyboard, in its own document only (never a host page: the shell ends above the keyboard, a frame in it only needs
 * to scroll itself). Not while zoomed in (pinch zoom also makes the visual viewport smaller).
 *
 * Load order: after core/layout.js (part of sw/core.js and common/common.js, not of the small sw/session.js: its child frames take
 * the top window's state in core/layout.js). Dependencies: Sw.Layout.
 * State (top window): Sw.Layout.Keyboard / KeyboardInset, the class "SwKeyboard" on html and body, the custom properties
 * --sw-kb-inset and --sw-view-h on html, the body event "SwKeyboard" (detail = open), "layout.changed" (see setKeyboard).
 * Globals defined: none (Sw.Layout.KeyboardCheck).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** The least height (css px) the visible area must lose (a text field focused, touch) to count as an on screen keyboard. */
    const keyboardMin = 120;

    /** @type {number} The layout viewport height without the keyboard (for the width fullW). */
    let fullH = 0;

    /** @type {number} The layout viewport width fullH was measured at (a rotation or a window resize starts over). */
    let fullW = 0;

    /** @type {number} Pending reveal timers. */
    let revealT1 = 0, revealT2 = 0;

    /**
     * The element with the focus, also inside same-origin frames (the shell's content frame).
     * @returns {Element|null}
     */
    function deepActive() {
        let a = document.activeElement;
        for (let i = 0; (i < 8) && a && ((a.tagName === "IFRAME") || (a.tagName === "FRAME")); ++i) {
            let d = null;
            try {
                d = a.contentDocument;
            }
            catch {
            }
            if (!d || !d.activeElement)
                break;
            a = d.activeElement;
        }
        return a;
    }

    /**
     * True if the element takes text (an on screen keyboard shows for it).
     * @param {Element|null} el
     * @returns {boolean}
     */
    function takesText(el) {
        if (!el || el.disabled)
            return false;
        if (el.isContentEditable)
            return true;
        const tag = el.tagName;
        if (tag === "TEXTAREA")
            return true;
        if (tag !== "INPUT")
            return false;
        return !/^(button|submit|reset|checkbox|radio|range|color|file|image|hidden)$/i.test(el.type || "text");
    }

    /** Scroll the focused text field into view above the keyboard (the nearest scrolling ancestor, else the page, of its own document). */
    function reveal() {
        const L = Sw.Layout;
        const el = deepActive();
        if (!L.Keyboard || !takesText(el))
            return;
        const doc = el.ownerDocument;
        const win = doc.defaultView;
        if (!win)
            return;
        const margin = 8;
        let top = margin;
        let bottom = win.innerHeight - margin;
        if (win === window) {
            const vv = window.visualViewport;
            const vt = vv ? vv.offsetTop : 0;
            top = vt + margin;
            bottom = Math.min(bottom, (vv ? vt + vv.height : document.documentElement.clientHeight - L.KeyboardInset) - margin);
        }
        const r = el.getBoundingClientRect();
        //  A field higher than the visible part: its top (a text box grows from there)
        let delta = (r.bottom > bottom) ? Math.min(r.bottom - bottom, r.top - top) : ((r.top < top) ? r.top - top : 0);
        for (let p = el.parentElement; p && (Math.abs(delta) >= 1); p = p.parentElement) {
            if ((p.scrollHeight <= p.clientHeight) || !/(auto|scroll)/.test(win.getComputedStyle(p).overflowY))
                continue;
            const before = p.scrollTop;
            p.scrollTop = before + delta;
            delta -= p.scrollTop - before;
        }
        const se = doc.scrollingElement;
        if (se && (Math.abs(delta) >= 1))
            se.scrollTop += delta;
    }

    /**
     * Set the state: Sw.Layout.Keyboard / KeyboardInset, the class SwKeyboard on html and body, the custom properties --sw-kb-inset
     * (the part of the window the keyboard covers, 0 where the window gets lower) and --sw-view-h (the height above it) on html;
     * when it opens or closes also the layout (the orientation stays), the body event "SwKeyboard" (detail = open), the same-origin
     * child frames (they take this window's state) and "layout.changed".
     * @param {boolean} on - The keyboard is open.
     * @param {number} inset - The covered part (css px).
     */
    function setKeyboard(on, inset) {
        const L = Sw.Layout;
        const changed = L.Keyboard !== on;
        if (!changed && (L.KeyboardInset === inset))
            return;
        L.Keyboard = on;
        L.KeyboardInset = inset;
        const h = document.documentElement;
        h.classList.toggle("SwKeyboard", on);
        if (document.body)
            document.body.classList.toggle("SwKeyboard", on);
        if (on) {
            h.style.setProperty("--sw-kb-inset", inset + "px");
            h.style.setProperty("--sw-view-h", Math.max(0, (h.clientHeight || window.innerHeight) - inset) + "px");
        }
        else {
            h.style.removeProperty("--sw-kb-inset");
            h.style.removeProperty("--sw-view-h");
        }
        if (!changed)
            return;
        L.Update(true);
        if (document.body)
            document.body.dispatchEvent(new CustomEvent("SwKeyboard", { detail: on }));
        for (let i = 0; i < window.frames.length; ++i) {
            try {
                const fl = window.frames[i].Sw && window.frames[i].Sw.Layout;
                if (fl && (typeof fl.Update === "function")) {
                    fl.Keyboard = on;
                    fl.Update(true);
                }
            }
            catch {
            }
        }
        if (Sw.Events)
            Sw.Events.Emit("layout.changed", L);
    }

    /** Reveal the focused field once the layout followed the keyboard (twice: browsers resize in steps). */
    function revealSoon() {
        clearTimeout(revealT1);
        clearTimeout(revealT2);
        revealT1 = setTimeout(reveal, 60);
        revealT2 = setTimeout(reveal, 400);
    }

    /** Detect the keyboard now (top window only) and set Sw.Layout.Keyboard / KeyboardInset. */
    function keyboardCheck() {
        const L = Sw.Layout;
        if (!L || L.IsChild)
            return;
        const de = document.documentElement;
        const lh = de.clientHeight || window.innerHeight;
        const lw = de.clientWidth || window.innerWidth;
        const edit = takesText(deepActive());
        if ((lw !== fullW) || !edit) {
            fullW = lw;
            fullH = lh;
        }
        else if (lh > fullH)
            fullH = lh;
        const vv = window.visualViewport;
        const inset = (vv && (Math.abs(vv.scale - 1) < 0.01)) ? Math.max(0, Math.round(lh - vv.offsetTop - vv.height)) : 0;
        let touch = !!L.Mobile;
        if (!touch) {
            try {
                touch = window.matchMedia("(pointer: coarse)").matches;
            }
            catch {
            }
        }
        const on = edit && touch && ((inset >= keyboardMin) || ((fullH - lh) >= keyboardMin));
        const was = L.Keyboard;
        const wasInset = L.KeyboardInset;
        setKeyboard(on, on ? inset : 0);
        if (on && vv && (vv.offsetTop > 0)) {
            //  iOS pans the whole window to show the field; a page that does not scroll (the shell: it ends above the keyboard
            //  itself) is put back, else its top is out of view
            const se = document.scrollingElement;
            if (se && (se.scrollHeight <= se.clientHeight + 1))
                window.scrollTo(0, 0);
        }
        if (on && (!was || (wasInset !== L.KeyboardInset)))
            revealSoon();
    }

    if (!Sw.Layout || Sw.Layout.KeyboardCheck)
        return;
    /** @type {boolean} Sw.Layout.Keyboard: the on screen keyboard is open (a same-origin child frame gets the top window's). */
    let open = false;
    try {
        open = (window.parent !== window) && !!window.parent.Sw && (window.parent.Sw.Layout.Keyboard === true);
    }
    catch {
    }
    Sw.Layout.Keyboard = open;
    /** @type {number} Sw.Layout.KeyboardInset: the part of the window (css px) the open keyboard covers (top window; 0 where the window gets lower). */
    Sw.Layout.KeyboardInset = 0;
    //  Also run by core/layout.js on a window resize, before the layout is updated (the orientation stays while it is open)
    Sw.Layout.KeyboardCheck = keyboardCheck;
    if (window.parent !== window)
        return;
    const vv = window.visualViewport;
    if (vv) {
        vv.addEventListener("resize", keyboardCheck, { passive: true });
        vv.addEventListener("scroll", keyboardCheck, { passive: true });
    }
    document.addEventListener("focusout", () => setTimeout(keyboardCheck, 0), true);
    //  The height without the keyboard (a field in a frame gets the focus without an event here)
    keyboardCheck();
})();
