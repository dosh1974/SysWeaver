/**
 * @file ui/overlay.js - SysWeaver UI library (Sw), part 3 of 7: everything that floats above the page.
 *
 *  - Sw.Place / Sw.Track: position a fixed element next to an anchor (the old TrackElement)
 *  - Sw.Popup: anchored popups (tap outside / Escape closes), one shared overlay stack with the dialogs
 *  - Sw.Menu: popup menus with icons, hints, checked / radio items, disabled items, headings, separators and submenus
 *    (cascading on wide screens, drill down on narrow / touch screens), full keyboard support; Sw.Menu.Context (long press / right click)
 *  - Sw.Dialog: modal dialogs (Show / Open / Confirm / Prompt / Alert / Select / Working) with the inert background, dirty / busy state and
 *    close attempts (UX-POP-6), movable / resizable geometry (POP-7); Sw.SearchSelection (paged searchable selection with recents)
 *  - Sw.Toast, Sw.Fail, Sw.Info, Sw.Message: toasts (click copies the text to the clipboard)
 *  - Sw.Hint: tooltips for touch (long press) and keyboard (focus) users
 *  - Sw.Loader: the loading overlay (the visual part of Sw.Page.Loading, the old AddLoading)
 *  - Sw.ServerBlock: the dialog shown while the server is paused / restarting / shut down / unreachable (movable, closable; no reload button)
 *  - Sw.FullScreen: full screen helpers (the old FullScreen and Ew.Fullscreen)
 *  - Sw.Overlay: the "covers nothing" geometry check of fixed overlays (toasts, activity lines): developer warnings and Check()
 * Needs ui/base.js and ui/controls.js. Documentation: ui/README.md.
 */

(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const { el, clamp, Icon, Focus, Spinner } = Sw;

    // ------------------------------------------------------------------ placement

    /**
     * Get the rectangle of an anchor.
     * @param {Element|{X: number, Y: number}|null} anchor An element, a point, or null (the viewport center).
     * @returns {{left: number, right: number, top: number, bottom: number, width: number, height: number}} The rectangle (viewport coordinates).
     */
    function anchorRect(anchor) {
        const vw = window.innerWidth, vh = window.innerHeight;
        if (anchor && anchor.getBoundingClientRect)
            return anchor.getBoundingClientRect();
        if (anchor && anchor.X !== undefined)
            return { left: anchor.X, right: anchor.X, top: anchor.Y, bottom: anchor.Y, width: 0, height: 0 };
        return { left: vw / 2, right: vw / 2, top: vh / 2, bottom: vh / 2, width: 0, height: 0 };
    }

    /**
     * Position a fixed element next to an anchor, flipping to the other side when there is no room, and keeping it on screen.
     * @param {HTMLElement} box The element (position: fixed).
     * @param {Element|{X: number, Y: number}|null} anchor An element, a point, or null (centered).
     * @param {object} [o] Options: { Side ("below" | "above" | "left" | "right", default below), Align ("start" | "end" | "center", default start),
     * Gap (px between anchor and box, default 4), Margin (px kept to the viewport edges, default 8) }.
     */
    function Place(box, anchor, o) {
        o = o || {};
        const vw = window.innerWidth, vh = window.innerHeight;
        const m = o.Margin ?? 8;
        const g = o.Gap ?? 4;
        const r = anchorRect(anchor);
        const bw = box.offsetWidth, bh = box.offsetHeight;
        const side = anchor ? (o.Side || "below") : "center";
        let x, y;
        if (side === "center") {
            x = (vw - bw) / 2;
            y = (vh - bh) / 2;
        }
        else if (side === "below" || side === "above") {
            const align = o.Align || "start";
            x = align === "end" ? r.right - bw : align === "center" ? r.left + r.width / 2 - bw / 2 : r.left;
            y = side === "below" ? r.bottom + g : r.top - bh - g;
            if (side === "below" && y + bh > vh - m && r.top - bh - g > m)
                y = r.top - bh - g;
            if (side === "above" && y < m && r.bottom + g + bh < vh - m)
                y = r.bottom + g;
        }
        else {
            x = side === "right" ? r.right + g : r.left - bw - g;
            const align = o.Align || "start";
            y = align === "end" ? r.bottom - bh : align === "center" ? r.top + r.height / 2 - bh / 2 : r.top;
            if (side === "right" && x + bw > vw - m && r.left - bw - g > m)
                x = r.left - bw - g;
            if (side === "left" && x < m && r.right + g + bw < vw - m)
                x = r.right + g;
        }
        box.style.left = Math.round(clamp(x, m, Math.max(m, vw - bw - m))) + "px";
        box.style.top = Math.round(clamp(y, m, Math.max(m, vh - bh - m))) + "px";
    }

    /**
     * Keep a fixed element next to an anchor element while the page scrolls or resizes (the old TrackElement).
     * Stops by itself when the tracked element is removed from the document.
     * @param {Element|MouseEvent} anchor The anchor element, or a mouse event (its target is tracked, the click point is kept relative to it).
     * @param {HTMLElement} box The fixed element to move.
     * @param {object} [o] Options of Sw.Place.
     * @returns {function():void} Stops tracking.
     */
    function Track(anchor, box, o) {
        let target = anchor;
        let rel = null;
        if (anchor instanceof MouseEvent) {
            target = anchor.target;
            const r = target.getBoundingClientRect();
            if (anchor.isTrusted && r.width > 0 && r.height > 0)
                rel = [(anchor.clientX - r.left) / r.width, (anchor.clientY - r.top) / r.height];
        }
        /** Place the box next to the tracked anchor (stops when either is detached). */
        const place = () => {
            if (!box.isConnected || !target.isConnected) {
                stop();
                return;
            }
            if (rel) {
                const r = target.getBoundingClientRect();
                Place(box, { X: r.left + rel[0] * r.width, Y: r.top + rel[1] * r.height }, o);
            }
            else
                Place(box, target, o);
        };
        let raf = 0;
        /** Coalesce placement updates to one per animation frame. */
        const later = () => {
            if (!raf)
                raf = requestAnimationFrame(() => {
                    raf = 0;
                    place();
                });
        };
        const ro = new ResizeObserver(later);
        ro.observe(box);
        ro.observe(target);
        window.addEventListener("resize", later);
        window.addEventListener("scroll", later, true);
        /** Stop tracking: remove the observers and listeners. */
        const stop = () => {
            ro.disconnect();
            cancelAnimationFrame(raf);
            window.removeEventListener("resize", later);
            window.removeEventListener("scroll", later, true);
        };
        place();
        return stop;
    }

    // ------------------------------------------------------------------ motion (UX-MOT-1: showing and hiding are transitions)

    /**
     * The longest time of a computed CSS time list ("0.24s, 100ms").
     * @param {string} v The computed value.
     * @returns {number} Milliseconds.
     */
    function cssMs(v) {
        let m = 0;
        for (const s of String(v || "0s").split(",")) {
            const t = s.trim();
            const n = parseFloat(t) || 0;
            m = Math.max(m, t.endsWith("ms") ? n : n * 1000);
        }
        return m;
    }

    /**
     * Shared exit transitions of every overlay (dialogs, sheets, scrims, popups, menus, toasts, hints, the loader, the server block): one
     * code path, so nothing vanishes in one frame (UX-MOT-1) and nothing waits for a transition that a theme turned off.
     */
    const Motion = {
        /**
         * Let an element leave with its theme transition, then remove it (or call o.Done): adds the class "sw-leaving" (or o.Class),
         * makes it inert and hidden from assistive technology at once (it can't be clicked or focused while it fades), waits for the
         * animation / transition the class starts (the durations come from the theme tokens --sw-dur-* and their scale --sw-motion; a 0 duration and reduced motion
         * end at once), with a fallback timer.
         * @param {Element} e The element.
         * @param {object} [o] Options: { Class (default "sw-leaving"), Keep (don't remove it), Done() (called after) }.
         * @returns {Promise<void>} Resolves when it is gone.
         */
        Out(e, o) {
            o = o || {};
            checkOpts("Sw.Motion.Out", o);
            const finish = () => {
                if (!o.Keep && e && e.parentNode)
                    e.remove();
                if (o.Done)
                    try {
                        o.Done();
                    }
                    catch (x) {
                        console.error(x);
                    }
            };
            if (!e || !e.isConnected || e.SwLeaving) {
                if (!(e && e.SwLeaving))
                    finish();
                return e && e.SwLeaving ? e.SwLeaving : Promise.resolve();
            }
            e.classList.add(o.Class || "sw-leaving");
            e.inert = true;
            e.setAttribute("aria-hidden", "true");
            let ms = 0;
            try {
                const cs = getComputedStyle(e);
                ms = Math.max(cssMs(cs.animationDuration) + cssMs(cs.animationDelay), cssMs(cs.transitionDuration));
                if (cs.animationName === "none" && cssMs(cs.transitionDuration) <= 0)
                    ms = 0;
            }
            catch {
            }
            //  (reduced motion: the exit still fades, without moving: the tokens keep the durations and set the distances to 0)
            if (ms <= 2) {
                finish();
                return Promise.resolve();
            }
            e.SwLeaving = new Promise(res => {
                let fin = false;
                const end = ev => {
                    if ((ev && ev.target !== e) || fin)
                        return;
                    fin = true;
                    clearTimeout(t);
                    e.removeEventListener("animationend", end);
                    e.removeEventListener("transitionend", end);
                    finish();
                    res();
                };
                e.addEventListener("animationend", end);
                e.addEventListener("transitionend", end);
                const t = setTimeout(end, ms + 80);
            });
            return e.SwLeaving;
        },
        /**
         * Test if the user asked for reduced motion (no movement then: the tokens set the slide distances to 0 and the zoom to 1; fades
         * keep their durations).
         * @returns {boolean} True when reduced.
         */
        Reduced() {
            try {
                return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            }
            catch {
                return false;
            }
        },
    };

    // ------------------------------------------------------------------ overlay stack (popups and dialogs)

    /** @type {Array<{Close: function, NoEscape: boolean, Box: HTMLElement}>} Open overlays, the last one is on top. */
    const stack = [];

    /**
     * @type {boolean} True while a closing popup gives the focus back to its opener (Hint.Attach shows no hint for that focus:
     * the opener's tool tip, ex: "Click to show settings", must not pop up after a menu item was picked).
     */
    let restoringFocus = false;

    /**
     * Escape closes the innermost thing (UX-POP-3): a visible tool tip / hint bubble takes the first Escape (window capture phase, before
     * fields, edits and pop-ups, TIP-5); then fields can consume it; then the top most overlay gets it (bubble phase): a popup closes, a
     * dialog treats it as a close attempt (Attempt("escape"): a dirty or busy dialog doesn't close silently, POP-6).
     */
    window.addEventListener("keydown", e => {
        if (e.key !== "Escape" || e.defaultPrevented || !hintShown)
            return;
        Hint.Hide();
        e.preventDefault();
        e.stopImmediatePropagation();
    }, true);
    document.addEventListener("keydown", escapeTop);

    /**
     * Give an Escape keydown to the top most overlay (a popup closes, a dialog treats it as a close attempt).
     * @param {KeyboardEvent} e The event.
     * @returns {boolean} True if an overlay took it (the default is prevented).
     */
    function escapeTop(e) {
        if (e.key !== "Escape" || e.defaultPrevented || !stack.length)
            return false;
        const top = stack[stack.length - 1];
        e.preventDefault();
        if (top.Attempt)
            top.Attempt("escape");
        else if (!top.NoEscape)
            top.Close();
        return true;
    }

    /**
     * Add an overlay entry to the stack (the modal background, the toasts and the depth listeners follow).
     * @param {object} p The entry.
     */
    function pushStack(p) {
        stack.push(p);
        refreshModal();
    }

    /**
     * Remove an overlay entry from the stack.
     * @param {object} p The entry.
     */
    function unstack(p) {
        const i = stack.indexOf(p);
        if (i >= 0) {
            stack.splice(i, 1);
            refreshModal();
        }
        //  A level belongs to its owner (UX-POP-5): the menus, pickers and questions of a pop-up close with it (none stays orphaned)
        for (const q of stack.slice().reverse())
            if (q.Owner === p && !q.Closed)
                try {
                    q.Close();
                }
                catch (x) {
                    console.error(x);
                }
    }

    /**
     * The open overlay entry that holds an element (the owner of a menu or popup anchored in it), else null.
     * @param {Element|object|null} anchor The anchor (an element; points and null have no owner).
     * @returns {object|null} The entry.
     */
    function ownerOf(anchor) {
        const e = anchor && anchor.nodeType === 1 ? anchor : null;
        if (!e)
            return null;
        for (let i = stack.length - 1; i >= 0; --i) {
            const p = stack[i];
            if (!p.Closed && p.Box && p.Box.contains(e))
                return p;
        }
        return null;
    }

    /**
     * An Owner option as an overlay entry: an element (the pop-up that holds it), a dialog handle or a popup / menu object, null = none.
     * @param {*} x The option.
     * @returns {object|null} The entry.
     */
    function resolveOwner(x) {
        if (!x)
            return null;
        if (x.nodeType === 1)
            return ownerOf(x);
        if (x.SwEntry)
            return x.SwEntry;
        return stack.includes(x) ? x : null;
    }

    /**
     * The stacking order of an owner's box (its menus and popups go just above it: a menu of a dialog, or of the session warning
     * that sits above every pop-up, is never under its owner).
     * @param {object|null} owner The owner entry.
     * @returns {number} The z-index (0 when unknown).
     */
    function ownerZ(owner) {
        if (!owner || !owner.Box)
            return 0;
        try {
            return parseInt(getComputedStyle(owner.Box).zIndex, 10) || 0;
        }
        catch {
            return 0;
        }
    }

    // ------------------------------------------------------------------ modal background (UX-POP-1)

    /**
     * Elements a modal never makes inert: the toasts, tool tips / hints, the server block, the page's live region (Sw.Page.Announce)
     * and anything marked data-sw-above-modal (ex: a session time-out warning that is not an Sw.Dialog).
     */
    const AboveModal = ".sw-toasts, .sw-hint, .sw-serverblock, .sw-page-live, [data-sw-above-modal]";

    /** @type {Set<Element>} The elements this file made inert (only these are restored; elements made inert by others stay so). */
    const inerted = new Set();

    /** @type {MutationObserver|null} Watches the body while a modal is open (elements added behind it become inert too). */
    let bodyObserver = null;

    /**
     * The stack index of the entry that makes everything behind it inert: the top most dialog shown with Above (the session time-out
     * warning), else the top most dialog; -1 when no dialog is open. Entries above it (its menus, nested confirmations) stay usable.
     * @returns {number} The index.
     */
    function modalIndex() {
        for (let i = stack.length - 1; i >= 0; --i)
            if (stack[i].Above)
                return i;
        for (let i = stack.length - 1; i >= 0; --i)
            if (stack[i].IsDialog)
                return i;
        return -1;
    }

    /**
     * The top most open dialog entry (toasts and the live region move into it, so they are announced and reachable, POP-1).
     * @returns {object|null} The entry.
     */
    function topDialogEntry() {
        for (let i = stack.length - 1; i >= 0; --i)
            if (stack[i].IsDialog)
                return stack[i];
        return null;
    }

    /**
     * Bring the background in line with the overlay stack (called on every stack change):
     * - while a dialog is open everything else in this document is inert (the shell chrome too: the shell is this document or does it
     *   for its content frame; a framed page that frames a dialog page makes its own content inert except the frame);
     * - one scrim: only the top most dimming layer is dimmed (a confirmation over a dialog moves the dim, never a double dim; none when
     *   the app shell or a nested frame already dims the app);
     * - the toasts and the Sw.Page.Announce live region sit inside the top dialog (screen readers hear them, toast buttons are in the
     *   dialog's Tab cycle; a toast never takes the focus);
     * - the Back / pop-up depth listeners (Sw.Popup.OnDepth) are told.
     */
    function refreshModal() {
        const b = document.body;
        if (!b)
            return;
        const idx = modalIndex();
        const keep = [];
        if (idx >= 0)
            for (let i = idx; i < stack.length; ++i) {
                keep.push(stack[i].Box);
                if (stack[i].Layer)
                    keep.push(stack[i].Layer);
            }
        if (idx < 0 && childModals.size) {
            for (const x of childModals.values())
                keep.push(x.Frame);
            if (childScrim)
                keep.push(childScrim);
        }
        const want = new Set();
        if (keep.length) {
            //  Keep the path from the body to every kept element, make every other child along it inert
            const path = new Set();
            for (const k of keep)
                for (let e = k; e && e !== b && e !== document.documentElement; e = e.parentElement)
                    path.add(e);
            const parents = new Set([b]);
            for (const p of path)
                if (p.parentElement && p.parentElement !== document.documentElement)
                    parents.add(p.parentElement);
            for (const par of parents)
                for (const c of par.children) {
                    if (path.has(c) || /^(SCRIPT|STYLE|LINK|TEMPLATE|META)$/.test(c.tagName) || c.matches(AboveModal) || c.SwLeaving)
                        continue;
                    want.add(c);
                }
        }
        for (const e of Array.from(inerted))
            if (!want.has(e) || !e.isConnected) {
                e.inert = false;
                inerted.delete(e);
            }
        for (const e of want)
            if (!inerted.has(e) && !e.inert) {
                e.inert = true;
                inerted.add(e);
            }
        if (keep.length && !bodyObserver && window.MutationObserver) {
            bodyObserver = new MutationObserver(() => refreshModal());
            bodyObserver.observe(b, { childList: true });
        }
        else if (!keep.length && bodyObserver) {
            bodyObserver.disconnect();
            bodyObserver = null;
        }
        //  One scrim
        const appDim = b.classList.contains("sw-shell-modal") || !!childScrim;
        let dimmed = false;
        for (let i = stack.length - 1; i >= 0; --i) {
            const p = stack[i];
            if (!p.Layer || !p.DimLayer)
                continue;
            const on = !dimmed && !appDim;
            p.Layer.classList.toggle("sw-dim", on);
            if (on || !appDim)
                dimmed = true;
        }
        //  Toasts and the live region inside the top dialog
        const td = topDialogEntry();
        const host = td && !td.Closed ? td.Box : b;
        if (toastHost && toastHost.isConnected && toastHost.parentNode !== host)
            host.append(toastHost);
        const live = document.querySelector(".sw-page-live");
        if (live && live.parentNode !== host)
            host.append(live);
        notifyDepth();
        //  a sheet or dialog footer that opened or closed moves the toasts clear of it (FBK-8)
        if (toastHost && toastHost.isConnected)
            requestAnimationFrame(placeToasts);
    }

    /**
     * Closing or reloading the tab with a dirty dialog, or a busy one whose work stops with the page, asks first (UX-POP-6, SAF-10): the
     * dialogs report to Sw.Page.SetDirty (the shell then asks on the menu, links and Back too); this prompt is only the fallback for
     * pages without core/page.js.
     */
    /* SwWeb:allow ux-saf-10 (fallback for pages without Sw.Page.SetDirty; the dialogs report to Sw.Page.SetDirty otherwise) */
    window.addEventListener("beforeunload", e => {
        if (Sw.Page && typeof Sw.Page.SetDirty === "function")
            return;
        if (stack.some(p => p.Unload && p.Unload())) {
            e.preventDefault();
            e.returnValue = "";
        }
    });

    // ------------------------------------------------------------------ pop-up depth and Back (UX-MOB-3)

    /** @type {Set<function(number):void>} Listeners of the pop-up depth (Sw.Popup.OnDepth). */
    const depthFns = new Set();

    /** @type {Map<Window, number>} Top window: the pop-up depth of each framed page (message "SwPopupDepth"). */
    const frameDepth = new Map();

    /** @type {number} The depth last told to the listeners. */
    let lastDepth = 0;

    /** @type {number} The own depth last posted to the top window (framed pages). */
    let lastPosted = 0;

    /**
     * The number of open pop-up levels: this window's overlays plus (top window) those of its framed pages.
     * @returns {number} The depth.
     */
    function totalDepth() {
        let n = stack.length;
        for (const [w, c] of frameDepth) {
            if (w.closed) {
                frameDepth.delete(w);
                continue;
            }
            n += c;
        }
        return n;
    }

    /** Tell the depth listeners (and, in a frame, the top window) about a changed pop-up depth. */
    function notifyDepth() {
        if (inFrame && Sw.Frame) {
            const dlgs = stack.filter(p => p.IsDialog && !p.Closed).length;
            const key = stack.length * 1000 + dlgs;
            if (key !== lastPosted) {
                lastPosted = key;
                Sw.Frame.PostTop("SwPopupDepth", { Count: stack.length, Dialogs: dlgs });
            }
        }
        const n = totalDepth();
        if (n === lastDepth)
            return;
        lastDepth = n;
        for (const f of Array.from(depthFns))
            try {
                f(n);
            }
            catch (x) {
                console.error(x);
            }
    }

    /**
     * Time and target of the last user input anywhere (a trusted pointer press, key, text input, paste or drop): the dirty tracking of
     * dialogs counts a change as the user's only when it follows input on that control (Dialog.Open; values set by code are no edits).
     */
    let lastUserInput = 0;
    /** @type {EventTarget|null} The target of the last user input. */
    let lastInputTarget = null;
    ["pointerdown", "keydown", "beforeinput", "paste", "drop"].forEach(n => window.addEventListener(n, e => {
        if (!e.isTrusted)
            return;
        lastUserInput = Date.now();
        lastInputTarget = e.target;
    }, true));

    /**
     * True when developer warnings are on (Sw.DevMode of the core: Sw.Debug or localhost, overridable; else Sw.Debug).
     * @returns {boolean} True to warn.
     */
    function devMode() {
        try {
            return typeof Sw.DevMode === "function" ? !!Sw.DevMode() : !!Sw.Debug;
        }
        catch {
            return false;
        }
    }

    /**
     * Warn the page author (the one developer warning channel, Sw.DevWarn: "[Sw UX] ...", once per key, never in production).
     * @param {string} msg The message (English, for developers).
     * @param {string} [key] The de-duplication key.
     */
    function devWarn(msg, key) {
        if (typeof Sw.DevWarn === "function")
            Sw.DevWarn(msg, key);
        else if (Sw.Debug)
            console.warn("[Sw UX] " + msg);
    }

    /**
     * After an outside press closed (or was refused by) a pop-up: swallow that pointer's mousedown, pointerup, mouseup, click, dblclick
     * and contextmenu, so the element behind is not activated (UX-POP-2: one outside click = close, a second click = act) and the focus
     * the close gave back stays (the mousedown's default action would move it to the body, FOC-1).
     * @param {PointerEvent} e The pointerdown on the scrim / layer.
     */
    function swallowOutside(e) {
        const types = ["mousedown", "pointerup", "mouseup", "click", "dblclick", "contextmenu", "auxclick"];
        let done = false;
        const stop = () => {
            if (done)
                return;
            done = true;
            types.forEach(t => window.removeEventListener(t, kill, true));
        };
        const kill = ev => {
            if (ev.type === "pointerup" && ev.pointerId !== e.pointerId)
                return;
            ev.preventDefault();
            ev.stopImmediatePropagation();
            if (ev.type === "pointerup")
                setTimeout(stop, e.pointerType === "touch" ? 400 : 0);
            else if (ev.type === "click" && e.pointerType === "touch")
                stop();
        };
        types.forEach(t => window.addEventListener(t, kill, true));
        setTimeout(stop, 1500);
    }

    /**
     * Wire the outside press of a scrim / layer (UX-POP-2): a mouse or pen press acts on pointerdown; on touch the act happens on the
     * release of a tap (at most 10 px of movement), so a scroll gesture outside does nothing. The rest of that pointer's events are
     * swallowed.
     * @param {HTMLElement} layer The layer.
     * @param {function():void} act Called for the outside press (closes, or gives the rejection cue).
     */
    function outsidePress(layer, act) {
        layer.addEventListener("pointerdown", e => {
            e.preventDefault();
            if (e.pointerType !== "touch") {
                swallowOutside(e);
                act();
                return;
            }
            const sx = e.clientX, sy = e.clientY;
            const up = ev => {
                if (ev.pointerId !== e.pointerId)
                    return;
                window.removeEventListener("pointerup", up, true);
                window.removeEventListener("pointercancel", cancel, true);
                if (Math.hypot(ev.clientX - sx, ev.clientY - sy) <= 10)
                    act();
            };
            const cancel = ev => {
                if (ev.pointerId !== e.pointerId)
                    return;
                window.removeEventListener("pointerup", up, true);
                window.removeEventListener("pointercancel", cancel, true);
            };
            window.addEventListener("pointerup", up, true);
            window.addEventListener("pointercancel", cancel, true);
            //  after the own listeners: the swallower stops the other listeners of that pointerup
            swallowOutside(e);
        });
    }

    // ------------------------------------------------------------------ popup

    /**
     * Popups: Sw.Popup.Show(anchor, content, opts) shows content next to an anchor element (or a point {X, Y}, or null = centered),
     * closed by tapping outside, Escape, or calling the returned object's Close().
     */
    const Popup = {
        /**
         * Show a popup.
         * @param {Element|{X: number, Y: number}|null} anchor The anchor.
         * @param {Node|Node[]} content The content.
         * @param {object} [opts] Options: { Side, Align, Gap (see Sw.Place), Dim (dim the page), Class, MinWidth (px), Modal (tapping outside doesn't close),
         * NoEscape, NoLayer (no outside tap layer; used by submenus), NoTrap (don't trap the focus), Focus (true: focus the first focusable element),
         * Role (aria role of the box, default "dialog"), Label (aria label), OnClose(), Host (the parent element, default the body), BeforeRestore() (called before the focus goes back to the opener),
         * Owner (the pop-up this one belongs to: an element in it, a dialog handle or a popup; default the open pop-up / dialog the anchor is in, null for none):
         * an owned popup sits just above its owner, its outside layer covers the owner (a press on the owner closes the popup and does nothing else) and it closes
         * with its owner }.
         * @returns {{Box: HTMLElement, Layer: HTMLElement|null, Close: function():void, Place: function():void, Closed: boolean}} The popup.
         */
        Show(anchor, content, opts) {
            const o = opts || {};
            if (!o._Menu)
                checkOpts("Sw.Popup.Show", o);
            const layer = o.NoLayer ? null : el("div.sw-layer" + (o.Dim ? ".sw-dim" : ""));
            const box = el("div.sw-popup.sw-root", {
                class: o.Class || null, tabIndex: -1,
                attrs: { role: o.Role === undefined ? "dialog" : o.Role, "aria-label": o.Label || null },
            });
            if (Array.isArray(content))
                box.append(...content.filter(x => x));
            else if (content)
                box.append(content);
            if (o.MinWidth)
                box.style.minWidth = Math.min(o.MinWidth, window.innerWidth - 16) + "px";
            const prevFocus = document.activeElement;
            //  The owner: the open pop-up / dialog the anchor is in (or o.Owner): this popup is a level of it, sits just above it (its
            //  outside layer covers the owner too, so a press on the owner closes the popup and does nothing else, POP-2) and closes
            //  with it (POP-5)
            const owner = o.Owner !== undefined ? resolveOwner(o.Owner) : ownerOf(anchor);
            const p = { Layer: layer, Box: box, Closed: false, NoEscape: !!o.NoEscape, Owner: owner || null };
            const z = ownerZ(owner);
            if (z) {
                if (layer)
                    layer.style.zIndex = String(z);
                box.style.zIndex = String(z);
            }
            let release = null;
            const onResize = () => p.Place();
            p.Close = () => {
                if (p.Closed)
                    return;
                p.Closed = true;
                unstack(p);
                window.removeEventListener("resize", onResize);
                const hadFocus = box.contains(document.activeElement);
                if (release)
                    release();
                //  exit transitions (UX-MOT-1): inert at once, removed after the theme duration
                p.Leaving = Promise.all([layer ? Motion.Out(layer) : null, Motion.Out(box)]);
                o.BeforeRestore && o.BeforeRestore();
                if (hadFocus && prevFocus && prevFocus.isConnected) {
                    restoringFocus = true;
                    try {
                        //  back to the opener; one that can't take the focus (a component root) gives it to its first control (FOC-1)
                        if (!(typeof Focus.Restore === "function" && Focus.Restore(prevFocus)))
                            Focus.Set(prevFocus);
                    }
                    finally {
                        restoringFocus = false;
                    }
                }
                Hint.Hide();
                o.OnClose && o.OnClose();
            };
            //  a press outside it that arrives from the app shell (the page is framed, see frameCatcher)
            if (layer)
                p.OutsideAct = () => {
                    if (!o.Modal)
                        p.Close();
                };
            if (layer) {
                outsidePress(layer, () => {
                    if (!o.Modal)
                        p.Close();
                });
                //  A wheel over the layer would scroll the page behind the anchored popup: close instead
                layer.addEventListener("wheel", () => {
                    if (!o.Modal)
                        p.Close();
                }, { passive: true });
            }
            if (layer)
                (o.Host || document.body).append(layer);
            (o.Host || document.body).append(box);
            p.DimLayer = !!o.Dim;
            pushStack(p);
            p.Place = () => Place(box, anchor, o);
            p.Place();
            window.addEventListener("resize", onResize);
            if (!o.NoTrap)
                release = Focus.Trap(box);
            if (o.Focus) {
                const f = Focus.Tabbables(box)[0];
                Focus.Set(f || box);
            }
            return p;
        },
        /** Close all popups and dialogs (from code: no close attempt, dirty or busy dialogs close too). */
        CloseAll: () => stack.slice().reverse().forEach(p => p.Close()),
        /**
         * Test if any popup or dialog is open.
         * @returns {boolean} True if open.
         */
        IsOpen: () => stack.length > 0,
        /**
         * The number of open pop-up levels (menus, submenus, popups, dialogs): this window's, plus in the top window those of the
         * framed pages (they report it with the message "SwPopupDepth").
         * @returns {number} The depth (0 = none).
         */
        Depth: () => totalDepth(),
        /**
         * Call a function whenever the pop-up depth changes (the browser / phone Back closes the top pop-up first, UX-MOB-3: the app
         * shell keeps one history entry while the depth is above 0).
         * @param {function(number):void} fn Called with the new depth.
         * @returns {function():void} Removes the listener.
         */
        OnDepth(fn) {
            depthFns.add(fn);
            return () => depthFns.delete(fn);
        },
        /**
         * Close the top pop-up level as a close attempt (UX-MOB-3, POP-6): a menu or popup closes; a dialog gets
         * Attempt(kind): clean dialogs close, dirty ones ask "Discard changes?", busy ones stay open with the rejection cue. In the top
         * window with no own pop-up, the attempt goes to the framed page that shows one ("SwPopupBack").
         * @param {string} [kind] The close attempt: "back" (default; the browser / phone Back), "swipe", "escape", "outside".
         * @returns {boolean} True when there was a pop-up (the Back was used up, even when the dialog stays open).
         */
        CloseTop(kind) {
            const top = stack[stack.length - 1];
            if (top) {
                if (top.Attempt)
                    top.Attempt(kind || "back");
                else
                    top.Close();
                return true;
            }
            for (const [w, n] of frameDepth) {
                if (n <= 0)
                    continue;
                try {
                    w.postMessage({ Type: "SwPopupBack", Kind: kind || "back" }, window.location.origin);
                    return true;
                }
                catch {
                    frameDepth.delete(w);
                }
            }
            return false;
        },
    };

    if (Sw.Frame) {
        //  Top window: the pop-up depth of the framed pages; framed pages: a Back from the top window closes the own top pop-up
        if (!inFrameWindow())
            Sw.Frame.On("SwPopupDepth", (d, ev) => {
                if (!ev.source)
                    return;
                const n = +d.Count || 0;
                if (n > 0)
                    frameDepth.set(ev.source, n);
                else
                    frameDepth.delete(ev.source);
                //  a menu / pop-up open in the framed page (no dialog: a dialog dims the shell itself) is one modal layer with it
                frameCatcher(ev.source, n > 0 && !(+d.Dialogs > 0));
                notifyDepth();
            });
        else {
            Sw.Frame.On("SwPopupBack", (d, ev) => {
                if (ev.source === window.top || ev.source === window.parent)
                    Popup.CloseTop(d.Kind || "back");
            });
            //  a press on the app shell while a pop-up of this page is open: the same as a press outside it here (it closes; the press
            //  does nothing else in the shell, POP-2)
            Sw.Frame.On("SwPopupOutside", (d, ev) => {
                if (ev.source !== window.top && ev.source !== window.parent)
                    return;
                for (let i = stack.length - 1; i >= 0; --i) {
                    const p = stack[i];
                    if (p.Closed)
                        continue;
                    if (p.Outside) {
                        p.Outside();
                        return;
                    }
                    if (p.OutsideAct) {
                        p.OutsideAct();
                        return;
                    }
                }
            });
        }
    }

    /** @type {Map<Window, {Frame: HTMLIFrameElement, Host: HTMLElement, Place: function():void}>} Top window: catchers of framed pages' pop-ups. */
    const frameCatches = new Map();

    /**
     * Top window: while a framed page shows a menu or pop-up, the rest of the app (the shell header, the sidebar) is covered by a
     * transparent layer around that frame (UX-POP-2 across frames: one modal layer). A press on it closes the framed page's pop-up and
     * is swallowed (one click = close, a second click = act); on touch only a tap does it (a scroll gesture does nothing).
     * @param {Window} win The framed page's window.
     * @param {boolean} on Cover / uncover.
     */
    function frameCatcher(win, on) {
        let c = frameCatches.get(win);
        if (!on) {
            if (c) {
                frameCatches.delete(win);
                window.removeEventListener("resize", c.Place);
                window.removeEventListener("scroll", c.Place, true);
                /* SwWeb:allow ux-mot-1 (a transparent press catcher, nothing visible leaves) */
                c.Host.remove();
            }
            return;
        }
        if (c) {
            c.Place();
            return;
        }
        //  the top level frame that holds the page (a page framed in a framed page: the outer frame)
        let frame = null;
        for (const f of document.querySelectorAll("iframe")) {
            try {
                for (let w = win; w && !frame; w = w.parent === w ? null : w.parent) {
                    if (f.contentWindow === w)
                        frame = f;
                    if (w === window || w.parent === window)
                        break;
                }
            }
            catch {
            }
            if (frame)
                break;
        }
        if (!frame)
            return;
        const host = el("div.sw-frame-catch-host", { attrs: { "aria-hidden": "true" } });
        const parts = [0, 1, 2, 3].map(() => el("div.sw-frame-catch"));
        host.append(...parts);
        const act = () => {
            try {
                win.postMessage({ Type: "SwPopupOutside" }, window.location.origin);
            }
            catch {
            }
        };
        parts.forEach(p => {
            outsidePress(p, act);
            p.addEventListener("wheel", act, { passive: true });
        });
        const place = () => {
            if (!frame.isConnected) {
                frameCatcher(win, false);
                return;
            }
            const r = frame.getBoundingClientRect();
            const W = window.innerWidth, H = window.innerHeight;
            const q = [[0, 0, W, r.top], [0, r.bottom, W, H - r.bottom], [0, r.top, r.left, r.height], [r.right, r.top, W - r.right, r.height]];
            parts.forEach((p, i) => {
                p.style.left = q[i][0] + "px";
                p.style.top = q[i][1] + "px";
                p.style.width = Math.max(0, q[i][2]) + "px";
                p.style.height = Math.max(0, q[i][3]) + "px";
            });
        };
        c = { Frame: frame, Host: host, Place: place };
        frameCatches.set(win, c);
        document.body.append(host);
        place();
        window.addEventListener("resize", place);
        window.addEventListener("scroll", place, true);
    }

    /**
     * True in a framed page.
     * @returns {boolean} Framed.
     */
    function inFrameWindow() {
        try {
            return window.top !== window;
        }
        catch {
            return true;
        }
    }

    // ------------------------------------------------------------------ menu

    /**
     * Test if menus should drill down (submenus replace the list) instead of cascading (submenus open to the side).
     * @returns {boolean} True on narrow or touch only screens.
     */
    function useDrillDown() {
        return window.innerWidth < 640 || (window.matchMedia && window.matchMedia("(hover: none)").matches);
    }

    /**
     * Popup menus.
     * Items: { Text, Icon (Sw.Icons name, icon class or url), Hint (right aligned text, ex: a shortcut), Title (tooltip),
     *          Desc (optional short description, a muted second line under the text, ex: what an export format is for),
     *          Checked (true / false: a check box item, role menuitemcheckbox; undefined: a plain item), Radio (with Checked: a radio
     *          item, role menuitemradio; the check mark is in the icon column, or at the end of an item with its own Icon), Disabled, Danger,
     *          Keep (don't close on click), Class, OnClick(item, event), Items (submenu: an array, or a function returning an array or a promise of one) }
     *        | a control row (stays open while it is changed): { Text, Title, Icon, Disabled, Slider: { Min, Max, Step, Value, Default, Log,
     *          Format(value) -> the value text, OnInput(value), OnChange(value) } } or { Text, ..., Input: { Value, Placeholder, Type, Width,
     *          OnInput(value), OnChange(value) (Enter, which also closes the menu, or leaving the box) } } (createControlItem)
     *        | "-" (separator) | { Title } (heading, an item without Text).
     */
    const Menu = {
        /**
         * Show a popup menu.
         * Keyboard: Up / Down / Home / End move, typing a letter jumps to the next item starting with it, Enter / Space activate,
         * Right opens a submenu, Left / Backspace go back from a submenu, Escape closes the current level, Tab closes the menu.
         * Mouse: hovering a submenu item opens it. Touch / narrow screens: submenus drill down (with a back row).
         * @param {Element|{X: number, Y: number}|null} anchor The anchor (element, point or null = centered).
         * @param {Array<object|string>} items The items.
         * A menu is its own pop-up level (UX-POP-2, POP-5): its outside layer covers everything, its owner too (a menu of a dialog: a
         * press on the dialog closes the menu and does nothing else), it belongs to the pop-up its anchor is in (Owner) and closes with
         * it, and an anchor has one menu at a time (showing it again closes the open one).
         * @param {object} [opts] Popup options (Side, Align, MinWidth, Class, OnClose, Owner ...) plus { NoFocus (don't focus the first item),
         * Focus (index of the item to focus first), DrillDown (true / false, default automatic), Label (aria label) }.
         * @returns {{Close: function():void, Place: function():void, Box: HTMLElement}} The menu popup.
         */
        Show(anchor, items, opts) {
            const o = opts || {};
            checkOpts("Sw.Menu.Show", o);
            //  One menu per anchor: showing it again (a second press on ⋮, a synthetic click) closes the open one (the anchor toggles)
            const anchorE = anchor && anchor.nodeType === 1 ? anchor : null;
            if (anchorE && anchorE.SwMenu && !anchorE.SwMenu.Closed) {
                const open = anchorE.SwMenu;
                open.Close();
                return open;
            }
            const drill = o.DrillDown ?? useDrillDown();
            const root = { Closed: false, Levels: [], Outs: [] };
            if (anchorE) {
                anchorE.SwMenu = root;
                if (anchorE.hasAttribute("aria-haspopup"))
                    anchorE.setAttribute("aria-expanded", "true");
            }
            //  All levels live in one host (display: contents); inside a dialog's focus trap the menu traps the focus in its host, so
            //  its items (and cascading submenus) can take the keyboard focus
            root.Host = el("div.sw-menu-host", { style: { display: "contents" } });
            document.body.append(root.Host);
            let trapped = false;
            if (!Focus.Allowed(root.Host)) {
                Focus.Trap(root.Host);
                trapped = true;
            }
            root.Release = () => {
                if (trapped) {
                    trapped = false;
                    Focus.Release(root.Host, true);
                }
            };
            /** Close the whole menu. */
            const closeAll = () => {
                if (root.Closed)
                    return;
                root.Closed = true;
                for (let i = root.Levels.length - 1; i >= 0; --i)
                    root.Levels[i].Popup.Close();
                root.Release();
                if (anchorE) {
                    if (anchorE.SwMenu === root)
                        anchorE.SwMenu = null;
                    if (anchorE.hasAttribute("aria-haspopup"))
                        anchorE.setAttribute("aria-expanded", "false");
                }
                //  the host goes after the exit transitions of every level (also those that closed first: a level leaves the list
                //  when it closes, its transition is in root.Outs; UX-MOT-1)
                Promise.all(root.Outs).then(() => root.Host.remove());
            };
            root.Close = closeAll;
            const first = openLevel(root, anchor, items, Object.assign({}, o, { OnClose: () => { closeAll(); o.OnClose && o.OnClose(); } }), 0, drill);
            root.SwEntry = first.Popup;
            root.Box = first.Popup.Box;
            root.Place = () => first.Popup.Place();
            return root;
        },

        /**
         * Show a context menu on long press (touch), right click (mouse) and the context menu key / Shift+F10 (keyboard).
         * @param {HTMLElement} target The element.
         * @param {Array<object|string>|function(Event):(Array|Promise<Array>)} items The items, or a function creating them for the event.
         * @param {object} [opts] Menu options.
         * @returns {function():void} Removes the handlers.
         */
        Context(target, items, opts) {
            checkOpts("Sw.Menu.Context", opts);
            return Sw.LongPress(target, async ev => {
                const list = typeof items === "function" ? await items(ev) : items;
                if (!list || !list.length)
                    return;
                const at = ev.clientX !== undefined && (ev.clientX || ev.clientY) ? { X: ev.clientX, Y: ev.clientY } : (ev.target || target);
                Menu.Show(at, list, opts);
            });
        },

        /**
         * The appearance submenus of a page with its own menu (no shell settings menu: an app page, a console, a kiosk): Theme
         * (Automatic, the built in themes, the themes registered with a Title), Density and Text size, as radio items that apply at once
         * (Sw.Theme.Set / Reset, Sw.Theme.Density.Set, Sw.Theme.TextSize.Set: stored per browser, every window follows).
         * @param {{Theme: boolean, Density: boolean, TextSize: boolean, Hacker: boolean}} [o] Leave out a part with false; Hacker: true
         *   lists the Hacker theme (the server shows it to Debug users only).
         * @returns {Array<object>} Menu items (submenus) for Sw.Menu.Show.
         */
        Appearance(o) {
            o = o || {};
            const T = Sw.Theme;
            const items = [];
            if (!T)
                return items;
            if (o.Theme !== false) {
                const auto = !Sw.Storage.Get(Sw.Session.ThemeStorageKey, null) && !T.Forced;
                const cur = (T.Get() || "").split("@")[0];
                const th = (name, text, icon) => ({ Text: text, Icon: icon, Radio: true, Checked: !auto && cur === name, OnClick: () => T.Set(name) });
                const list = [
                    { Text: _TF("Automatic", "Theme menu item: follow the light / dark setting of the browser or OS"), Icon: "theme", Radio: true, Checked: auto, OnClick: () => T.Reset() },
                    th("light", _TF("Light", "Theme menu item: the light theme"), "sun"),
                    th("dark", _TF("Dark", "Theme menu item: the dark theme"), "moon"),
                    th("application", _TF("Application", "Theme menu item: the colours of this application"), null),
                    th("pro", _TF("Pro", "Theme menu item: the compact, mid dark theme for desktop work"), null),
                ];
                if (o.Hacker)
                    list.push(th("hacker", _TF("Hacker", "Theme menu item: the retro terminal theme"), null));
                const custom = T.Custom ? T.Custom() : [];
                if (custom.length)
                    list.push("-", ...custom.map(c => th(c.Name, c.Title, null)));
                items.push({ Text: _TF("Theme", "Menu: the submenu that picks the colour theme"), Icon: "theme", Items: list });
            }
            if (o.Density !== false && T.Density) {
                const D = T.Density;
                const cur = D.IsAuto ? "" : D.Get();
                const names = {
                    compact: _TF("Compact", "Density menu item: small controls and rows"),
                    comfortable: _TF("Comfortable", "Density menu item: the normal size of controls and rows"),
                    touch: _TF("Touch", "Density menu item: large controls for touch screens"),
                };
                items.push({
                    Text: _TF("Density", "Menu: the submenu that picks how dense (compact or roomy) the user interface is"), Icon: "layers",
                    Items: [{ Text: _TF("Automatic", "Density menu item: by device and theme"), Radio: true, Checked: cur === "", OnClick: () => D.Set(null) }]
                        .concat(D.Names.map(n => ({ Text: names[n] || n, Radio: true, Checked: cur === n, OnClick: () => D.Set(n) }))),
                });
            }
            if (o.TextSize !== false && T.TextSize) {
                const S = T.TextSize;
                const cur = S.Get();
                items.push({
                    Text: _TF("Text size", "Menu: the submenu that picks the text size of the user interface"), Icon: "search",
                    Items: S.Sizes.map(n => ({ Text: _T("{0} %", n, "Menu: a choice of the submenu that picks the text size of the user interface. {0} is the size in percent of normal, ex: 125"), Radio: true, Checked: cur === n, OnClick: () => S.Set(n) })),
                });
            }
            return items;
        },
    };

    /**
     * Open one menu level (the root, or a cascading submenu).
     * @param {object} root The menu root state.
     * @param {Element|{X: number, Y: number}|null} anchor The anchor.
     * @param {Array} items The items.
     * @param {object} o Options.
     * @param {number} depth 0 for the root.
     * @param {boolean} drill Drill down mode.
     * @returns {object} The level { Popup, List, Items, Depth, Child }.
     */
    function openLevel(root, anchor, items, o, depth, drill) {
        const list = el("div.sw-menu", { attrs: { role: "menu", "aria-label": o.Label || null } });
        //  (Class: the menu's own class, kept for the sub menu levels so they look like the menu, ex: a theme scope)
        const level = { Root: root, List: list, Depth: depth, Child: null, Drill: drill, Stack: [], RootItems: items, Class: o.Class || null };
        renderLevel(level, items, null);
        level.Popup = Popup.Show(anchor, list, Object.assign({}, o, {
            //  (the options were checked as menu options)
            _Menu: true,
            Role: null,
            NoTrap: true,
            NoLayer: depth > 0,
            Side: depth > 0 ? "right" : o.Side,
            Gap: depth > 0 ? -2 : o.Gap,
            Class: "sw-menu-popup" + (o.Class ? " " + o.Class : ""),
            Focus: false,
            Host: root.Host,
            BeforeRestore: depth === 0 ? () => root.Release() : undefined,
            OnClose: () => {
                if (level.Child)
                    level.Child.Popup.Close();
                if (level.Popup && level.Popup.Leaving)
                    root.Outs.push(level.Popup.Leaving);
                const i = root.Levels.indexOf(level);
                if (i >= 0)
                    root.Levels.splice(i, 1);
                if (depth === 0)
                    o.OnClose && o.OnClose();
                else if (level.ParentItem) {
                    level.ParentItem.setAttribute("aria-expanded", "false");
                    if (level.Parent)
                        level.Parent.Child = null;
                }
            },
        }));
        root.Levels.push(level);
        bindLevelKeys(level, o);
        if (!(o.NoFocus)) {
            const all = enabledItems(list);
            const n = typeof o.Focus === "number" && o.Focus >= 0 ? itemsByIndex(list)[o.Focus] : null;
            const f = (n && !n.disabled) ? n : all[0];
            f && f.focus({ preventScroll: true });
        }
        return level;
    }

    /**
     * The enabled item buttons of a menu list.
     * @param {HTMLElement} list The list.
     * @returns {HTMLButtonElement[]} The items.
     */
    function enabledItems(list) {
        return Array.from(list.querySelectorAll(":scope > .sw-menu-item:not(:disabled)"));
    }

    /**
     * All item buttons of a menu list (including disabled ones), in the order of the item array (separators and headings excluded).
     * @param {HTMLElement} list The list.
     * @returns {HTMLButtonElement[]} The items.
     */
    function itemsByIndex(list) {
        return Array.from(list.querySelectorAll(":scope > .sw-menu-item:not(.sw-menu-back)"));
    }

    /**
     * Fill a menu list with items (replacing the content).
     * @param {object} level The level.
     * @param {Array} items The items.
     * @param {string|null} backTitle When drilled down: the title of the back row.
     */
    function renderLevel(level, items, backTitle) {
        const list = level.List;
        list.replaceChildren();
        if (backTitle !== null) {
            const back = el("button.sw-menu-item.sw-menu-back", { type: "button", attrs: { role: "menuitem" } },
                Icon("left"), el("span.sw-menu-text", { text: backTitle }));
            back.addEventListener("click", () => drillBack(level));
            list.append(back);
        }
        for (const it of items || []) {
            if (!it)
                continue;
            if (it === "-") {
                list.append(el("div.sw-menu-sep", { attrs: { role: "separator" } }));
                continue;
            }
            if (it.Title !== undefined && it.Text === undefined) {
                list.append(el("div.sw-menu-title", { text: it.Title, attrs: { role: "presentation" } }));
                continue;
            }
            list.append(it.Slider || it.Input ? createControlItem(level, it) : createItem(level, it));
        }
    }

    /**
     * Create one menu item button.
     * @param {object} level The level.
     * @param {object} it The item definition.
     * @returns {HTMLButtonElement} The item.
     */
    function createItem(level, it) {
        const isCheck = it.Checked !== undefined && it.Checked !== null;
        const role = isCheck ? (it.Radio ? "menuitemradio" : "menuitemcheckbox") : "menuitem";
        const b = el("button.sw-menu-item", {
            type: "button", disabled: !!it.Disabled, title: it.Title || null,
            class: it.Class || null,
            attrs: { role, "aria-checked": isCheck ? (it.Checked ? "true" : "false") : null, "aria-haspopup": it.Items ? "menu" : null, "aria-expanded": it.Items ? "false" : null },
        });
        if (it.Checked)
            b.classList.add("sw-checked");
        if (it.Danger)
            b.classList.add("sw-danger");
        //  Leading column: the icon, a check mark, or an empty slot (keeps the texts aligned)
        //  a server icon with a line twin (Sw.Icons.LineOf) is drawn in the one outline style of menus
        if (it.Icon)
            b.append(Icon((Sw.Icons.LineOf && Sw.Icons.LineOf(it.Icon)) || it.Icon));
        else if (isCheck)
            b.append(Icon(it.Radio ? "target" : "check", it.Checked ? "" : "sw-hidden-icon"));
        else
            b.append(el("span.sw-menu-noicon"));
        const text = el("span.sw-menu-text", { text: it.Text ?? "" });
        if (it.Desc) {
            //  Text and a muted description line (type ahead still uses span.sw-menu-text)
            const desc = el("span.sw-menu-desc", { text: it.Desc });
            b.append(el("span.sw-menu-texts", null, text, desc));
            b.classList.add("sw-menu-hasdesc");
        }
        else
            b.append(text);
        if (it.Hint)
            b.append(el("span.sw-menu-hint", { text: it.Hint }));
        //  An item with its own icon shows the check mark at the end (the texts of the menu stay aligned after the icon column)
        if (it.Icon && isCheck)
            b.append(Icon("check", "sw-menu-check" + (it.Checked ? "" : " sw-hidden-icon")));
        if (it.Items)
            b.append(Icon("right", "sw-menu-sub"));
        b.SwItem = it;
        b.addEventListener("click", ev => activateItem(level, b, ev));
        if (it.Items) {
            let timer = 0;
            b.addEventListener("pointerenter", ev => {
                if (ev.pointerType !== "mouse" || level.Drill)
                    return;
                clearTimeout(timer);
                timer = setTimeout(() => openSub(level, b, false), 160);
            });
            b.addEventListener("pointerleave", () => clearTimeout(timer));
        }
        else {
            b.addEventListener("pointerenter", ev => {
                if (ev.pointerType !== "mouse" || !level.Child)
                    return;
                const c = level.Child;
                setTimeout(() => {
                    if (level.Child === c && !c.Popup.Box.matches(":hover"))
                        c.Popup.Close();
                }, 220);
            });
        }
        return b;
    }

    /**
     * Create a control row of a menu (an item with Slider or Input): the label (Text, the leading icon column like the other items) and the
     * control; changing it keeps the menu open. The row is a menu item for the arrow keys (Up / Down move to the next item); focusing it
     * focuses the control, which keeps Left / Right, Home / End, typing and Backspace.
     *  - Slider: { Min, Max, Step, Value, Default, Log, Format(value) -> text (the value shown after the slider), OnInput(value) (while
     *    dragging), OnChange(value) (committed) } - an Sw.Slider.
     *  - Input: { Value, Placeholder, Type (the input type, default "text"), Width (css width of the box), OnInput(value) (every key),
     *    OnChange(value) (Enter or leaving the box) } - Enter also closes the menu.
     * @param {object} level The level.
     * @param {object} it The item definition ({ Text, Title, Icon, Disabled, Slider | Input }).
     * @returns {HTMLElement} The row (div.sw-menu-item.sw-menu-control).
     */
    function createControlItem(level, it) {
        const row = el("div.sw-menu-item.sw-menu-control", {
            title: it.Title || null, class: it.Class || null, tabIndex: -1,
            attrs: { role: "presentation", "aria-disabled": it.Disabled ? "true" : null },
        });
        row.SwItem = it;
        row.append(it.Icon ? Icon((Sw.Icons.LineOf && Sw.Icons.LineOf(it.Icon)) || it.Icon) : el("span.sw-menu-noicon"));
        const label = el("span.sw-menu-text", { text: it.Text ?? "" });
        row.append(label);
        let control;
        if (it.Slider) {
            const s = it.Slider;
            const fmt = typeof s.Format === "function" ? s.Format : null;
            const value = fmt ? el("span.sw-menu-hint.sw-menu-value", { text: fmt(s.Value ?? s.Min ?? 0) }) : null;
            const slider = new Sw.Slider({
                Min: s.Min ?? 0, Max: s.Max ?? 100, Step: s.Step ?? 1, Value: s.Value ?? s.Min ?? 0, Default: s.Default, Log: s.Log,
                Label: it.Text, Disabled: !!it.Disabled,
                OnInput: v => {
                    if (value)
                        value.textContent = fmt(v);
                    s.OnInput && s.OnInput(v);
                },
                OnChange: v => {
                    if (value)
                        value.textContent = fmt(v);
                    s.OnChange && s.OnChange(v);
                },
            });
            slider.E.classList.add("sw-menu-slider");
            control = slider.E;
            row.append(control);
            if (value)
                row.append(value);
        }
        else {
            const p = it.Input;
            const input = el("input.sw-input.sw-menu-input", {
                type: p.Type || "text", value: p.Value ?? "", placeholder: p.Placeholder || null, disabled: !!it.Disabled,
                attrs: { "aria-label": it.Text || p.Placeholder || null }, style: p.Width ? { width: p.Width } : null,
            });
            let last = input.value;
            const commit = () => {
                if (input.value === last)
                    return;
                last = input.value;
                p.OnChange && p.OnChange(input.value);
            };
            input.addEventListener("input", () => p.OnInput && p.OnInput(input.value));
            input.addEventListener("change", commit);
            input.addEventListener("keydown", e => {
                if (e.key !== "Enter")
                    return;
                e.preventDefault();
                commit();
                level.Root.Close();
            });
            control = input;
            row.append(control);
        }
        //  The row takes the focus for the arrow keys of the menu, the control gets it
        row.addEventListener("focus", () => control.focus({ preventScroll: true }));
        //  Up / Down (and Escape, Tab) are the menu's, before the control sees them (a slider also steps on Up / Down)
        row.addEventListener("keydown", e => {
            if (["ArrowUp", "ArrowDown", "Escape", "Tab"].indexOf(e.key) < 0 || e.target === row)
                return;
            e.stopPropagation();
            e.preventDefault();
            level.List.dispatchEvent(new KeyboardEvent("keydown", { key: e.key, shiftKey: e.shiftKey, bubbles: true, cancelable: true }));
        }, true);
        //  The pointer on a control row closes an open sub menu of the level (like any item)
        row.addEventListener("pointerenter", ev => {
            if (ev.pointerType !== "mouse" || !level.Child)
                return;
            const c = level.Child;
            setTimeout(() => {
                if (level.Child === c && !c.Popup.Box.matches(":hover"))
                    c.Popup.Close();
            }, 220);
        });
        return row;
    }

    /**
     * Activate an item (click, Enter, Space).
     * @param {object} level The level.
     * @param {HTMLButtonElement} b The item.
     * @param {Event} ev The event.
     */
    function activateItem(level, b, ev) {
        const it = b.SwItem;
        if (!it || b.disabled)
            return;
        if (it.Items) {
            openSub(level, b, true);
            return;
        }
        if (!it.Keep)
            level.Root.Close();
        else if (it.Checked !== undefined && it.Checked !== null && !it.Radio) {
            //  A kept check box item flips in place
            it.Checked = !it.Checked;
            b.setAttribute("aria-checked", it.Checked ? "true" : "false");
            b.classList.toggle("sw-checked", it.Checked);
            //  The check mark: the leading icon of an item without an icon, else the extra check after the item's icon
            const ci = it.Icon ? b.querySelector(".sw-menu-check") : b.querySelector(".sw-icon:not(.sw-menu-sub)");
            if (ci)
                ci.classList.toggle("sw-hidden-icon", !it.Checked);
        }
        it.OnClick && it.OnClick(it, ev);
    }

    /**
     * Resolve the submenu items of an item.
     * @param {object} it The item.
     * @returns {Promise<Array>} The items.
     */
    async function subItems(it) {
        const s = it.Items;
        return typeof s === "function" ? (await s(it)) || [] : s;
    }

    /**
     * Open the submenu of an item (cascading popup, or drill down).
     * @param {object} level The level of the item.
     * @param {HTMLButtonElement} b The item.
     * @param {boolean} focus Focus the first submenu item.
     */
    async function openSub(level, b, focus) {
        const it = b.SwItem;
        if (level.Drill) {
            const items = await subItems(it);
            if (level.Root.Closed)
                return;
            level.Stack.push({ Items: level.Current || null, Title: level.Title, Focus: itemsByIndex(level.List).indexOf(b) });
            level.Current = items;
            level.Title = it.Text;
            renderLevel(level, items, it.Text);
            level.Popup.Place();
            const f = enabledItems(level.List)[level.List.querySelector(".sw-menu-back") ? 1 : 0] || enabledItems(level.List)[0];
            f && f.focus({ preventScroll: true });
            return;
        }
        if (level.Child) {
            if (level.Child.ParentItem === b) {
                if (focus) {
                    const f = enabledItems(level.Child.List)[0];
                    f && f.focus({ preventScroll: true });
                }
                return;
            }
            level.Child.Popup.Close();
        }
        const items = await subItems(it);
        if (level.Root.Closed || !b.isConnected)
            return;
        const child = openLevel(level.Root, b, items, { NoFocus: !focus, Align: "start", Class: level.Class }, level.Depth + 1, false);
        child.ParentItem = b;
        child.Parent = level;
        level.Child = child;
        b.setAttribute("aria-expanded", "true");
    }

    /**
     * Go back one drill down step.
     * @param {object} level The level.
     */
    function drillBack(level) {
        const prev = level.Stack.pop();
        if (!prev)
            return;
        level.Current = prev.Items;
        level.Title = prev.Title;
        renderLevel(level, prev.Items || level.RootItems, level.Stack.length ? prev.Title : null);
        level.Popup.Place();
        const n = itemsByIndex(level.List)[prev.Focus] || enabledItems(level.List)[0];
        n && n.focus({ preventScroll: true });
    }

    /**
     * Keyboard handling of a menu level.
     * @param {object} level The level.
     * @param {object} o The menu options.
     */
    function bindLevelKeys(level, o) {
        const list = level.List;
        list.addEventListener("keydown", e => {
            const all = enabledItems(list);
            //  The focus may be in a control row (a slider, an input): the row is the item; the control keeps its own keys
            const act = document.activeElement;
            const cur = act && act.closest ? (act.closest(".sw-menu-item") || act) : act;
            const i = all.indexOf(cur);
            if (cur !== act && cur && cur.classList && cur.classList.contains("sw-menu-control") && ["ArrowUp", "ArrowDown", "Escape", "Tab"].indexOf(e.key) < 0)
                return;
            const go = n => {
                const t = all[(n + all.length) % all.length];
                t && t.focus({ preventScroll: true });
            };
            switch (e.key) {
                case "ArrowDown": go(i + 1); break;
                case "ArrowUp": go(i < 0 ? all.length - 1 : i - 1); break;
                case "Home": go(0); break;
                case "End": go(all.length - 1); break;
                case "ArrowRight":
                    if (cur && cur.SwItem && cur.SwItem.Items)
                        openSub(level, cur, true);
                    else
                        return;
                    break;
                case "ArrowLeft":
                case "Backspace":
                    if (level.Drill && level.Stack.length)
                        drillBack(level);
                    else if (level.Depth > 0) {
                        const pi = level.ParentItem;
                        level.Popup.Close();
                        pi && pi.focus({ preventScroll: true });
                    }
                    else
                        return;
                    break;
                case "Escape":
                    if (level.Drill && level.Stack.length) {
                        drillBack(level);
                        break;
                    }
                    if (level.Depth > 0) {
                        const pi = level.ParentItem;
                        level.Popup.Close();
                        pi && pi.focus({ preventScroll: true });
                        break;
                    }
                    return;
                case "Tab":
                    level.Root.Close();
                    return;
                default: {
                    //  Type ahead: the next item starting with the typed letter
                    if (e.key.length !== 1 || e.ctrlKey || e.metaKey || e.altKey || e.key === " ")
                        return;
                    const k = e.key.toLowerCase();
                    const n = all.length;
                    for (let j = 1; j <= n; ++j) {
                        const c = all[(i + j) % n];
                        const tx = c.querySelector(".sw-menu-text");
                        if (tx && tx.textContent.trim().toLowerCase().startsWith(k)) {
                            c.focus({ preventScroll: true });
                            break;
                        }
                    }
                    break;
                }
            }
            e.preventDefault();
            e.stopPropagation();
        });
    }

    // ------------------------------------------------------------------ dialogs

    /** @type {number} Open dialogs of this window (a framed page tells the app shell, which dims its chrome). */
    let openDialogs = 0;

    /** @type {boolean} This page is in a frame (the top window may be the app shell). */
    const inFrame = (() => {
        try {
            return window.top !== window;
        }
        catch {
            return true;
        }
    })();

    /**
     * Tell the top window (the app shell) how many dialogs are open here: {Type: "SwDialogModal", Count} (same origin only).
     * @param {number} delta +1 opened, -1 closed, 0 none (re-send).
     */
    function frameModal(delta) {
        openDialogs = Math.max(0, openDialogs + delta);
        if (inFrame && Sw.Frame) {
            Sw.Frame.PostTop("SwDialogModal", { Count: openDialogs });
            //  A frame in a frame (ex: an app Home that frames a library page): the page in between dims itself too (one scrim for
            //  the whole app) and passes Escape / a tap on its scrim down (see the "SwFrameModal" listener)
            if (parentIsFrame())
                Sw.Frame.PostParent("SwFrameModal", { Count: openDialogs });
        }
    }

    /**
     * True when this window's parent is itself a frame of a same-origin page (a nested frame).
     * @returns {boolean} Nested.
     */
    function parentIsFrame() {
        try {
            return window.parent !== window && window.parent !== window.top;
        }
        catch {
            return false;
        }
    }

    /** @type {Map<Window, {Count: number, Frame: HTMLIFrameElement}>} Child frames of this page that show a dialog (nested frames). */
    const childModals = new Map();

    /** @type {HTMLElement|null} This page's scrim while a child frame shows a dialog. */
    let childScrim = null;

    /**
     * A child frame of this page (a page that is itself framed by the app shell) opened or closed dialogs: dim this page with the same
     * scrim as a dialog (the shell dims its chrome), raise the frame above it, keep the focus in it; a tap on the scrim and Escape here
     * go to the child's top dialog. The count is passed up when this page is nested too.
     * @param {{Count: number}} d The message.
     * @param {MessageEvent} ev The event (source: the child window).
     */
    function onChildModal(d, ev) {
        const frame = Array.from(document.querySelectorAll("iframe")).find(f => {
            try {
                return f.contentWindow === ev.source;
            }
            catch {
                return false;
            }
        });
        if (!frame)
            return;
        const n = +d.Count || 0;
        if (n > 0)
            childModals.set(ev.source, { Count: n, Frame: frame });
        else
            childModals.delete(ev.source);
        if (parentIsFrame())
            Sw.Frame.PostParent("SwFrameModal", { Count: openDialogs + Array.from(childModals.values()).reduce((s, x) => s + x.Count, 0) });
        document.querySelectorAll("iframe.sw-frame-modal").forEach(f => {
            if (!Array.from(childModals.values()).some(x => x.Frame === f))
                f.classList.remove("sw-frame-modal", "sw-frame-static");
        });
        if (!childModals.size) {
            if (childScrim)
                childScrim.remove();
            childScrim = null;
            refreshModal();
            return;
        }
        frame.classList.add("sw-frame-modal");
        //  z-index needs a positioned frame: a static one becomes relative (it stays in place)
        frame.classList.toggle("sw-frame-static", getComputedStyle(frame).position === "static" || frame.classList.contains("sw-frame-static"));
        if (!childScrim) {
            childScrim = el("div.sw-layer.sw-dim.sw-frame-scrim", { attrs: { "aria-hidden": "true" } });
            childScrim.addEventListener("pointerdown", e => {
                e.preventDefault();
                postChildren({ Type: "SwDialogOutside" });
            });
            document.body.append(childScrim);
        }
        //  the rest of this page is inert while the frame shows a dialog (UX-POP-1)
        refreshModal();
        if (document.activeElement !== frame)
            try {
                frame.focus();
            }
            catch {
            }
    }

    /**
     * Pass a message to the child frames that show a dialog.
     * @param {object} m The message ({Type: "SwDialogOutside", Key}).
     */
    function postChildren(m) {
        for (const w of childModals.keys())
            try {
                w.postMessage(m, window.location.origin);
            }
            catch {
            }
    }

    if (inFrame && Sw.Frame) {
        Sw.Frame.On("SwFrameModal", onChildModal);
        document.addEventListener("keydown", e => {
            if (e.key === "Escape" && !e.defaultPrevented && childModals.size && !stack.length) {
                e.preventDefault();
                postChildren({ Type: "SwDialogOutside", Key: "Escape" });
            }
        });
    }

    if (inFrame) {
        //  A page that goes away (navigation, reload) with an open dialog must not leave the shell dimmed
        window.addEventListener("pagehide", () => {
            if (openDialogs > 0) {
                openDialogs = 0;
                frameModal(0);
            }
            if (lastPosted > 0 && Sw.Frame) {
                lastPosted = 0;
                Sw.Frame.PostTop("SwPopupDepth", { Count: 0 });
            }
        });
        //  A tap on the shell's scrim: the same as a tap on this page's scrim (closes the top dialog unless it is modal)
        if (Sw.Frame)
            Sw.Frame.On("SwDialogOutside", (d, ev) => {
                //  from the app shell, or from the framed page this page is nested in (it passes its scrim tap / Escape down)
                if (ev.source !== window.top && ev.source !== window.parent)
                    return;
                const top = stack[stack.length - 1];
                if (!top)
                    return;
                //  Escape pressed while the focus was in the shell: the same as Escape here
                if (d.Key === "Escape") {
                    if (top.Attempt)
                        top.Attempt("escape");
                    else if (!top.NoEscape)
                        top.Close();
                }
                else if (top.Outside)
                    top.Outside();
            });
    }

    /**
     * Test if a dialog button definition is a way out (a footer Cancel / Close: a close attempt, UX-POP-6) rather than an action that
     * closes the dialog with its value (Save, Delete, OK). Explicit: Dismiss (true / "cancel" / "close"); else a button without
     * OnClick, not primary / danger, whose Value is null, undefined or false.
     * @param {object} b The definition.
     * @returns {boolean} True for a way out.
     */
    function isDismiss(b) {
        if (b.Dismiss !== undefined)
            return !!b.Dismiss;
        if (b.Kind === "primary" || b.Kind === "danger" || b.Kind === "danger-solid" || b.Close === false || b.OnClick)
            return false;
        return b.Value === null || b.Value === undefined || b.Value === false;
    }

    /**
     * An AbortError (the end of work the user stopped: Sw.Button treats it as aborted, never as "done" or as a failure).
     * @returns {Error} The error.
     */
    function abortError() {
        let x;
        try {
            x = new DOMException("Stopped by the user", "AbortError");
        }
        catch {
            x = new Error("Stopped by the user");
            x.name = "AbortError";
        }
        //  the dialog announced it already ("Save cancelled" / "Stopped waiting for Save…"): the button says nothing more
        try {
            x.SwSaid = true;
        }
        catch {
        }
        return x;
    }

    /**
     * @param {*} x An error.
     * @returns {boolean} True for an AbortError.
     */
    function isAbort(x) {
        return !!x && x.name === "AbortError";
    }

    // ------------------------------------------------------------------ requests of a footer button's work (honest Cancel)

    /** @type {object|null} The footer job whose OnClick runs synchronously now (its requests are its own). */
    let curJob = null;
    /** @type {WeakMap<AbortSignal, object>} The signal given to a footer job's work → the job (requests sent later with it are its own). */
    const jobSignals = new WeakMap();

    /**
     * Record the requests of footer button work (Sw.Net.Send / Get, fetch): sent in the click itself, or later with the signal the
     * work was given. A write (Sw.Net.Send, a fetch that is not GET / HEAD) may be done on the server already when it is aborted, so the
     * work can no longer be cancelled, only no longer waited for ("Stop waiting", POP-6). The one write test is Sw.Net.IsWrite: not a
     * write is a Sw.Net.Read / Get, a Send with { ReadOnly: true } (a read api) or { AbortSafe: true } (the server undoes / stops the
     * work when the request is aborted, ex: a job that honours the connection), a GET / HEAD fetch. The inner fetch of a Sw.Net call
     * (Request.SwNet) is not counted again.
     */
    function trackJobRequests() {
        if (trackJobRequests.Done)
            return;
        trackJobRequests.Done = true;
        const wrap = (host, name, optsAt, sigName) => {
            const f = host && host[name];
            if (typeof f !== "function" || f.SwDlgTracked)
                return;
            const w = function (...args) {
                const o = args[optsAt];
                const a0 = args[0];
                let sig = o && typeof o === "object" ? o[sigName] : null;
                if (!sig && name === "fetch" && a0 && typeof a0 === "object" && a0.signal)
                    sig = a0.signal;
                const job = (sig && jobSignals.get(sig)) || curJob;
                const p = f.apply(this, args);
                //  the inner fetch of a Sw.Net call (its Request is marked SwNet) was recorded with the outer call already
                const inner = name === "fetch" && a0 && typeof a0 === "object" && a0.SwNet === true;
                const detached = (o && typeof o === "object" && (o.Detached || o.Background)) || (a0 && typeof a0 === "object" && a0.SwDetached === true);
                if (job && !inner && !detached) {
                    //  the one write test of the framework (Sw.Net.IsWrite: ReadOnly, Sw.Net.Read, AbortSafe, the old Read: true, GET / HEAD)
                    const opts = o && typeof o === "object" ? o : {};
                    const kind = name === "fetch" ? String(opts.method || (a0 && typeof a0 === "object" && a0.method) || "GET").toUpperCase() : name;
                    const write = Sw.Net && typeof Sw.Net.IsWrite === "function" ? !!Sw.Net.IsWrite(opts, kind)
                        : (name === "Get" || /^(?:GET|HEAD|OPTIONS)$/.test(kind) ? false : !(opts.ReadOnly === true || opts.Read === true || opts.AbortSafe === true));
                    job.Sent(write);
                }
                return p;
            };
            w.SwDlgTracked = true;
            //  keep the marks of a wrapper below (ui/base.js tracks requests too)
            if (f.SwTracked)
                w.SwTracked = true;
            host[name] = w;
        };
        if (Sw.Net) {
            wrap(Sw.Net, "Send", 2, "Signal");
            wrap(Sw.Net, "Get", 1, "Signal");
        }
        wrap(window, "fetch", 1, "signal");
    }

    /**
     * Create the button row of a dialog.
     * @param {Array<object>} buttons Button definitions.
     * @param {{Close: function(*):void, Attempt: function(string, object):void, Track: function(Promise):Promise}} ctl The dialog:
     * Close (closes with a value), Attempt (a close attempt of a way out), Track (a button's async work makes the dialog busy).
     * @returns {HTMLElement} The footer.
     */
    function dialogButtons(buttons, ctl) {
        const foot = el("div.sw-dialog-foot");
        for (const b of buttons) {
            if (!b)
                continue;
            //  The caller's text as given: the original showed "Yes, remove it!" as written (the wording rule is for new texts, README)
            const text = b.Text;
            const dismiss = isDismiss(b);
            /** After the button's own work: the close attempt (a way out) or the close with the value. */
            const after = (r, job) => {
                if (r === false)
                    return;
                if (dismiss)
                    ctl.Attempt("footer", b);
                else if (b.Close !== false)
                    ctl.CloseAfter(b.Value, job || null);
            };
            const btn = new Sw.Button({
                Text: text, Kind: b.Kind, Icon: b.Icon, Title: b.Title, Label: b.Label, Class: b.Class, Disabled: b.Disabled,
                //  the dialog reports its running work to the page itself, in one sentence with its unsaved input (SAF-10)
                Leave: false,
                OnClick: (self, ev, bctx) => {
                    //  Busy means locked (UX-POP-6): while work runs only the declared ways out act; any other button gives the cue
                    if (ctl.Locked(b)) {
                        ctl.Reject("busy-button");
                        return undefined;
                    }
                    if (!b.OnClick) {
                        //  a close attempt is not work: no busy state / "done" announcement on the button (FBK-2)
                        after(undefined);
                        return undefined;
                    }
                    //  The work gets its own AbortSignal (linked to the Sw.Button's click signal: Abort() and leaving the page fire it).
                    //  Every request sent with it, or in the click itself, is recorded on the job (trackJobRequests): a write may be done
                    //  on the server already, so after one the work is no longer cancellable, only no longer waited for (POP-6 / FBK-5).
                    trackJobRequests();
                    const own = typeof AbortController === "function" ? new AbortController() : null;
                    const outer = bctx && bctx.Signal ? bctx.Signal : null;
                    if (own && outer) {
                        if (outer.aborted)
                            own.abort();
                        else
                            outer.addEventListener("abort", () => own.abort(), { once: true });
                    }
                    const sig = own ? own.signal : (outer || undefined);
                    let usesSignal = false;
                    const ctx = {
                        get Signal() {
                            usesSignal = true;
                            return sig;
                        },
                        Dialog: ctl.Handle,
                    };
                    const job = {
                        Def: b, Text: text, Stopped: false, Writes: 0, Reads: 0, Snap: ctl.Snapshot(), T0: performance.now(),
                        /** A request of the work went out (write: it may be done on the server whatever happens next). */
                        Sent: write => {
                            if (write) {
                                ++job.Writes;
                                if (b.Cancellable === true && job.Writes === 1)
                                    devWarn("Sw.Dialog button \"" + text + "\": Cancellable: true, but the work sent a write (Sw.Net.Send / POST): the server may have it already, so Cancel is \"Stop waiting\" now. Send a read with Sw.Net.Read(url, arg, { Signal }) (or { ReadOnly: true }), or mark a request whose abort the server honours with { AbortSafe: true } (UX POP-6).", "dlg-cancelwrite:" + text);
                                ctl.Changed();
                            }
                            else
                                ++job.Reads;
                        },
                    };
                    if (sig)
                        jobSignals.set(sig, job);
                    //  a close from the work itself keeps edits made after the click (never "done" while the newer edit is gone)
                    const workClose = v => ctl.CloseAfter(v, job);
                    let r, reqs = [], inner = null;
                    curJob = job;
                    try {
                        if (Sw.Work && typeof Sw.Work.Scope === "function") {
                            const sc = Sw.Work.Scope(() => b.OnClick(workClose, b.Value, ctx));
                            if (sc.Threw)
                                throw sc.Error;
                            r = sc.Result;
                            reqs = sc.Requests || [];
                            inner = sc.Controller;
                            if (!(r && typeof r.then === "function") && reqs.length) {
                                devWarn("Sw.Dialog button \"" + text + "\": OnClick sent a request but returned no promise. Return it (OnClick: () => Sw.Net.Send(...)) so the dialog waits for it and shows a failure (UX FBK-2).", "dlg-noreturn:" + text);
                                r = Promise.allSettled(reqs).then(rs => {
                                    const bad = rs.find(x => x.status === "rejected");
                                    if (bad)
                                        throw bad.reason;
                                    return undefined;
                                });
                            }
                        }
                        else
                            r = b.OnClick(workClose, b.Value, ctx);
                    }
                    finally {
                        curJob = null;
                    }
                    if (!(r && typeof r.then === "function")) {
                        after(r, job);
                        return undefined;
                    }
                    const abort = () => {
                        if (own)
                            own.abort();
                        if (inner)
                            try {
                                inner.abort();
                            }
                            catch {
                            }
                        if (btn && typeof btn.Abort === "function")
                            btn.Abort();
                    };
                    //  Cancellable (the footer Cancel stops it): no write went out, and the work reads the signal (or the button says
                    //  Cancellable: true: a read, a job the server stops). A write is never aborted, whatever Cancellable says, unless the
                    //  request itself is marked AbortSafe. Otherwise the way out is "Stop waiting": the work may still finish.
                    const cancellable = () => job.Writes === 0 && (b.Cancellable === true || (b.Cancellable !== false && !!sig && usesSignal));
                    let stopRej = null;
                    const stopped = new Promise((_, rej) => stopRej = rej);
                    stopped.catch(() => { });
                    Object.assign(job, {
                        Cancellable: cancellable, Abort: abort, P: r,
                        /** Stop waiting: the button's click ends now, quietly (never "done"); the work goes on (a sent write is never aborted). */
                        Detach: () => {
                            job.Stopped = true;
                            if (cancellable())
                                abort();
                            stopRej(abortError());
                        },
                    });
                    //  Work nobody waits for any more: a later failure is a toast (FBK-5), a later success is said (the user was told it
                    //  "may still finish") and closes the dialog unless it holds newer edits (ctl.Late). Work that closed the dialog
                    //  itself and then failed: the failure is a toast (the dialog that would show it is gone).
                    r.then(v => {
                        if (job.Stopped)
                            ctl.Late(job, v, after);
                    }, x => {
                        if (isAbort(x) || (sig && sig.aborted && !job.Stopped))
                            return;
                        if (job.Stopped || ctl.IsClosed()) {
                            Sw.Fail(Sw.Net && typeof Sw.Net.FailText === "function" ? Sw.Net.FailText(text, x) : _T("{0} failed: {1}", text, x && x.message ? x.message : String(x), "Error toast when the work of a dialog button fails after the user stopped waiting for it, or after the dialog closed. {0}: the button label, ex: Save; {1}: the error"));
                            try {
                                x.SwSaid = true;
                            }
                            catch {
                            }
                        }
                    });
                    //  the button stays busy while its work runs (that is work); a stopped or cancelled run ends as aborted: no "done"
                    return Promise.race([ctl.Track(r, job), stopped])
                        .then(v => {
                            if (job.Stopped)
                                throw abortError();
                            return after(v, job);
                        }, x => {
                            if (job.Stopped || isAbort(x) || (sig && sig.aborted))
                                throw abortError();
                            throw x;
                        });
                },
            });
            btn.E.SwDialogButton = { Def: b, Text: text, Dismiss: dismiss };
            b.Button = btn;
            foot.append(btn.E);
        }
        return foot;
    }

    /**
     * Test if the activity list (FBK-4, the app shell's Sw.Activity) exists: only then a busy dialog offers "Continue in background".
     * @returns {boolean} True when available.
     */
    function hasActivity() {
        const A = Sw.Activity;
        return !!(A && A.Supported !== false && (typeof A.Start === "function" || typeof A.Add === "function"));
    }

    /**
     * Announce a text to screen readers from inside the open dialog (Sw.Page.Announce: its live region sits in the top dialog while one
     * is open, see refreshModal; a dialog-local polite region when the core page module is missing).
     * @param {string} text The text.
     * @param {HTMLElement} [box] The dialog (for the fallback region).
     */
    function announce(text, box) {
        if (!text)
            return;
        if (Sw.Page && Sw.Page.Announce) {
            Sw.Page.Announce(text);
            return;
        }
        if (!box)
            return;
        let r = box.querySelector(":scope > .sw-dialog-live");
        if (!r) {
            r = el("div.sw-sr-only.sw-dialog-live", { attrs: { "aria-live": "polite", "aria-atomic": "true" } });
            box.append(r);
        }
        r.textContent = "";
        setTimeout(() => r.textContent = text, 100);
    }

    /** Media queries of the phone layout (glossary: at most 760 px wide or at most 500 px high) and of sheets (narrow, not low). */
    //  (the phone layout is "not the desktop query" of the css (min-width: 761px and min-height: 501px): at a zoom the window can be
    //  760.5 px wide, where neither "max-width: 760px" nor "min-width: 761px" matches; a movable dialog in that gap was dragged by the
    //  script while the css ignored its position). With a mouse (pointer: fine) the desktop layout starts at 480 px: a page in the
    //  shell's content frame of a mid size window (ex: 636 px next to the pinned menu) gets a movable dialog, not a phone sheet (S7a-A1-4);
    //  the same queries are in ui/overlays.css.
    const deskMq = window.matchMedia ? window.matchMedia("(min-width: 761px) and (min-height: 501px), (min-width: 480px) and (min-height: 501px) and (pointer: fine)") : null;
    const phoneMq = deskMq ? { get matches() { return !deskMq.matches; } } : null;
    const sheetMq = window.matchMedia ? window.matchMedia("(max-width: 760px) and (min-height: 501px) and (pointer: coarse), (max-width: 760px) and (min-height: 501px) and (pointer: none), (max-width: 479.98px) and (min-height: 501px)") : null;

    /**
     * Test for the phone layout (dialogs are not movable there).
     * @returns {boolean} True in the phone layout.
     */
    function isPhoneLayout() {
        return !!(phoneMq && phoneMq.matches);
    }

    /**
     * Test for the sheet layout (narrow and not low: dialogs are full width sheets at the bottom).
     * @returns {boolean} True for sheets.
     */
    function isSheetLayout() {
        return !!(sheetMq && sheetMq.matches);
    }

    // ------------------------------------------------------------------ movable / resizable dialogs (UX-POP-7)

    /** @type {number} A dialog the user moves may leave a side of the window up to this much of its title bar (css px, OWN4-Dlg-1). */
    const MinOnScreen = 160;

    /**
     * localStorage key prefix of the remembered dialog geometry: "SysWeaver.Dialog.<site>.<id>" = json { L, T, W, H, Moved, Sized, Max }
     * in CSS px; the site is the path of the site root (two apps behind one proxy origin keep their own positions). The old key without
     * the site ("SysWeaver.Dialog.<id>") is still read.
     */
    const GeoKey = "SysWeaver.Dialog.";

    /**
     * The site part of the geometry keys.
     * @returns {string} The site path without dots (ex: "%2Fapp%2F").
     */
    function geoSite() {
        let p = "/";
        try {
            p = new URL(Sw.Url && Sw.Url.Root ? Sw.Url.Root : location.href).pathname || "/";
        }
        catch {
        }
        return encodeURIComponent(p).replace(/\./g, "%2E");
    }

    /**
     * Load the remembered geometry of a dialog kind.
     * @param {string} id The dialog id.
     * @returns {object|null} The geometry.
     */
    function loadGeo(id) {
        try {
            const v = JSON.parse(localStorage.getItem(GeoKey + geoSite() + "." + id) || localStorage.getItem(GeoKey + id) || "null");
            return v && typeof v === "object" ? v : null;
        }
        catch {
            return null;
        }
    }

    /**
     * Store (or with null forget) the geometry of a dialog kind.
     * @param {string} id The dialog id.
     * @param {object|null} g The geometry.
     */
    function saveGeo(id, g) {
        try {
            if (g)
                localStorage.setItem(GeoKey + geoSite() + "." + id, JSON.stringify(g));
            else
                localStorage.removeItem(GeoKey + geoSite() + "." + id);
            localStorage.removeItem(GeoKey + id);
        }
        catch {
        }
    }

    /** Elements of a dialog head that are not a drag handle. */
    const NoDrag = "button, a, input, select, textarea, [role=button], [contenteditable], .sw-dialog-tools";

    /**
     * Make a dialog movable and resizable (UX-POP-7): title bar drag (dead zone 4 px with mouse / pen, 10 px on touch), 8 px edge and
     * corner handles, double click on the title bar maximizes / restores, the dialog menu (Move, Size, Maximize / Restore, Reset position)
     * with keyboard modes and step buttons, the geometry remembered per dialog id and restored clamped, kept on screen on resize.
     * Phone layout: not movable; sheets get a handle (drag up: taller, drag down: a close attempt "swipe") and Size in the dialog menu.
     * @param {HTMLElement} box The dialog.
     * @param {HTMLElement} head The title bar.
     * @param {HTMLElement} body The body.
     * @param {string|null} id The dialog id (null: not remembered).
     * @param {function(string):void} attempt The close attempt of the dialog (for the sheet swipe).
     * @returns {{Menu: function(HTMLElement):void, Restore: function():void, Stop: function():void, InMode: function():boolean}} The controller.
     */
    function geometry(box, head, body, id, attempt) {
        const g = { L: 0, T: 0, W: 0, H: 0, Placed: false, Moved: false, Sized: false, Max: false, SheetH: 0 };
        const mode = { Kind: null, Start: null, Bar: null, Opener: null };
        const vw = () => document.documentElement.clientWidth || window.innerWidth;
        const vh = () => document.documentElement.clientHeight || window.innerHeight;
        const foot = () => box.querySelector(":scope > .sw-dialog-foot");
        const minW = () => Math.min(vw() - 16, 280);
        const minH = () => Math.min(vh() - 16, head.offsetHeight + (foot() ? foot().offsetHeight : 0) + 96);
        /**
         * Keep the geometry inside the viewport: the size within min / max; while the user moves it (drag, Move mode) the title bar
         * stays on screen: its whole height, and sideways a part of it as wide as the dialog or MinOnScreen px (the dialog moves freely
         * both ways, like the vertical move; a whole box clamp left a wide dialog in a narrow window almost no room sideways, OWN4-Dlg-1);
         * with whole (a restore, a window resize) the whole dialog (a position stored on a bigger screen never puts the footer off
         * screen, POP-7 / POP-8).
         * @param {boolean} [whole] Clamp the whole box, not only the title bar.
         */
        const clampG = whole => {
            if (g.Sized) {
                //  (never below the size the first resize started from, FloorW / FloorH: a short dialog doesn't grow when resized)
                g.W = clamp(g.W, Math.min(minW(), g.FloorW || Infinity), vw() - 16);
                g.H = clamp(g.H, Math.min(minH(), g.FloorH || Infinity), vh() - 16);
            }
            const w = g.Sized ? g.W : box.offsetWidth;
            const h = whole ? Math.min(g.Sized ? g.H : box.offsetHeight, vh()) : (head.offsetHeight || 48);
            if (whole)
                g.L = clamp(g.L, 0, Math.max(0, vw() - w));
            else {
                const keep = Math.min(w, MinOnScreen);
                g.L = clamp(g.L, keep - w, Math.max(keep - w, vw() - keep));
            }
            g.T = clamp(g.T, 0, Math.max(0, vh() - h));
        };
        /** Apply the geometry (css variables; the phone layout css ignores them). */
        const apply = () => {
            //  a moved / maximized dialog is placed by its geometry (a kept bottom edge of the centered one no longer applies)
            if ((g.Placed || g.Max) && !box.style.height)
                ["top", "bottom", "margin-bottom"].forEach(p => box.style.removeProperty(p));
            box.classList.toggle("sw-max", g.Max);
            box.classList.toggle("sw-placed", g.Placed && !g.Max);
            box.classList.toggle("sw-sized", g.Sized && !g.Max);
            //  (on the device pixel grid, not whole css pixels: at a zoom of 125 % a whole css pixel is 1.25 screen pixels, and a dialog
            //  that follows the pointer in whole css pixels judders against it)
            const dpr = window.devicePixelRatio || 1;
            box.style.setProperty("--sw-dlg-l", Math.round(g.L * dpr) / dpr + "px");
            box.style.setProperty("--sw-dlg-t", Math.round(g.T * dpr) / dpr + "px");
            if (g.Sized) {
                box.style.setProperty("--sw-dlg-w", Math.round(g.W) + "px");
                box.style.setProperty("--sw-dlg-h", Math.round(g.H) + "px");
            }
            box.classList.toggle("sw-sheet-sized", g.SheetH > 0);
            if (g.SheetH > 0)
                box.style.setProperty("--sw-sheet-h", Math.round(g.SheetH) + "px");
        };
        /** No two way scrolling: a remembered width smaller than the content needs at this zoom grows. */
        const fitContent = () => {
            if (!g.Sized)
                return;
            const over = body.scrollWidth - body.clientWidth;
            if (over > 1) {
                g.W += over + 2;
                clampG();
                apply();
            }
        };
        /** Remember what the user changed (per dialog id). */
        const persist = () => {
            if (!id)
                return;
            if (!g.Moved && !g.Sized && !g.Max) {
                saveGeo(id, null);
                return;
            }
            saveGeo(id, { L: Math.round(g.L), T: Math.round(g.T), W: Math.round(g.W), H: Math.round(g.H), Moved: g.Moved, Sized: g.Sized, Max: g.Max });
        };
        /** Switch from the centered css layout to an explicit position (the current one). */
        const place = () => {
            if (g.Placed)
                return;
            const r = box.getBoundingClientRect();
            g.L = r.left;
            g.T = r.top;
            g.Placed = true;
        };
        let posTimer = 0;
        /** Announce the new position / size (debounced). */
        const announcePos = () => {
            clearTimeout(posTimer);
            posTimer = setTimeout(() => {
                const r = box.getBoundingClientRect();
                announce(mode.Kind === "size" || (!mode.Kind && g.Sized)
                    ? _T("Dialog size {0} by {1} pixels", Math.round(r.width), Math.round(r.height), "Screen reader announcement after the user resized a dialog. {0}: width in CSS pixels, ex: 640; {1}: height, ex: 480")
                    : _T("Dialog at {0}, {1} pixels from the top left corner", Math.round(r.left), Math.round(r.top), "Screen reader announcement after the user moved a dialog. {0}: distance from the left edge in CSS pixels, ex: 120; {1}: from the top, ex: 80"), box);
            }, 400);
        };
        /**
         * Toggle the maximized state.
         * @param {boolean} [on] The new state (default: toggle).
         */
        const toggleMax = on => {
            on = on === undefined ? !g.Max : !!on;
            if (on === g.Max)
                return;
            g.Max = on;
            apply();
            persist();
            announce(on ? _TF("Dialog maximized", "Screen reader announcement after a dialog was maximized") : _TF("Dialog restored", "Screen reader announcement after a maximized dialog got its previous size back"), box);
        };
        /** Forget the position and size (the dialog is centered again). */
        const reset = () => {
            Object.assign(g, { L: 0, T: 0, W: 0, H: 0, Placed: false, Moved: false, Sized: false, Max: false, SheetH: 0 });
            ["--sw-dlg-l", "--sw-dlg-t", "--sw-dlg-w", "--sw-dlg-h", "--sw-sheet-h"].forEach(p => box.style.removeProperty(p));
            apply();
            persist();
            announce(_TF("Dialog position reset", "Screen reader announcement after a dialog was centered again with its default size"), box);
        };

        //  Title bar drag. The box follows the pointer at the spot where it was grabbed (its rendered position when the drag starts:
        //  never a stored or held one), one drag at a time, and it always ends: the pointer is captured at the press (the release comes
        //  back also from outside the frame or the window), and a mouse that moves without its button ends a drag whose release was
        //  missed (a drag left running would let the dialog follow the bare mouse, and a second press would run a second drag against
        //  it: the dialog snapping between two places, OWN3-Down-2).
        /** Ends the running title bar drag, null when none. @type {function():void|null} */
        let endDrag = null;
        head.addEventListener("pointerdown", e => {
            if (e.button !== 0 || isPhoneLayout() || g.Max || mode.Kind || (e.target.closest && e.target.closest(NoDrag)))
                return;
            if (endDrag)
                endDrag();
            const sx = e.clientX, sy = e.clientY, pid = e.pointerId, dead = e.pointerType === "touch" ? 10 : 4;
            const mouse = e.pointerType === "mouse";
            let grab = null, dragging = false;
            try {
                head.setPointerCapture(pid);
            }
            catch {
            }
            const end = () => {
                if (endDrag !== end)
                    return;
                endDrag = null;
                window.removeEventListener("pointermove", move, true);
                window.removeEventListener("pointerup", up, true);
                window.removeEventListener("pointercancel", up, true);
                head.removeEventListener("lostpointercapture", up);
                try {
                    if (head.hasPointerCapture(pid))
                        head.releasePointerCapture(pid);
                }
                catch {
                }
                if (dragging) {
                    box.classList.remove("sw-dragging");
                    g.Moved = true;
                    persist();
                }
            };
            const move = ev => {
                if (ev.pointerId !== pid)
                    return;
                if (mouse && ev.buttons === 0) {
                    end();
                    return;
                }
                if (!dragging) {
                    if (Math.hypot(ev.clientX - sx, ev.clientY - sy) <= dead)
                        return;
                    dragging = true;
                    //  grab what is on screen (an inline top / bottom of a centered or held box gives way to the drag's position)
                    const rr = box.getBoundingClientRect();
                    ["top", "bottom", "margin-bottom"].forEach(p => box.style.removeProperty(p));
                    g.Placed = true;
                    grab = { X: sx - rr.left, Y: sy - rr.top };
                    box.classList.add("sw-dragging");
                }
                g.L = ev.clientX - grab.X;
                g.T = ev.clientY - grab.Y;
                clampG();
                apply();
            };
            const up = ev => {
                if (ev.pointerId === pid)
                    end();
            };
            endDrag = end;
            window.addEventListener("pointermove", move, true);
            window.addEventListener("pointerup", up, true);
            window.addEventListener("pointercancel", up, true);
            head.addEventListener("lostpointercapture", up);
        });
        //  A press on the title bar (a drag handle) keeps the focus where it is (the caret stays in the field, FOC-1)
        head.addEventListener("mousedown", e => {
            if (e.button === 0 && !(e.target.closest && e.target.closest(NoDrag)) && box.contains(document.activeElement) && document.activeElement !== box)
                e.preventDefault();
        });
        head.addEventListener("dblclick", e => {
            if (isPhoneLayout() || mode.Kind || (e.target.closest && e.target.closest(NoDrag)))
                return;
            toggleMax();
        });

        //  Edge and corner handles (8 px; the keyboard and single pointer path is the dialog menu's Size)
        for (const d of ["n", "s", "e", "w", "ne", "nw", "se", "sw"]) {
            const h = el("div.sw-dialog-rz.sw-rz-" + d, { attrs: { "aria-hidden": "true" } });
            h.addEventListener("pointerdown", e => {
                if (e.button !== 0 || isPhoneLayout() || g.Max || mode.Kind)
                    return;
                e.preventDefault();
                e.stopPropagation();
                const r = box.getBoundingClientRect();
                const sx = e.clientX, sy = e.clientY;
                if (!g.Sized) {
                    /** @type {number} The dialog's own size when it was first resized (px): the resize minimum is never above it. */
                    g.FloorW = r.width;
                    g.FloorH = r.height;
                }
                try {
                    h.setPointerCapture(e.pointerId);
                }
                catch {
                }
                box.classList.add("sw-dragging");
                const move = ev => {
                    if (ev.pointerId !== e.pointerId)
                        return;
                    const dx = ev.clientX - sx, dy = ev.clientY - sy;
                    let W = r.width, H = r.height;
                    if (d.includes("e"))
                        W = r.width + dx;
                    if (d.includes("w"))
                        W = r.width - dx;
                    if (d.includes("s"))
                        H = r.height + dy;
                    if (d.includes("n"))
                        H = r.height - dy;
                    //  (the minimum never above the dialog's own size: a short dialog grew 42 px on the first 5 px move)
                    W = clamp(W, Math.min(minW(), g.FloorW || Infinity), vw() - 16);
                    H = clamp(H, Math.min(minH(), g.FloorH || Infinity), vh() - 16);
                    g.L = d.includes("w") ? r.right - W : r.left;
                    g.T = d.includes("n") ? r.bottom - H : r.top;
                    g.W = W;
                    g.H = H;
                    g.Placed = g.Sized = true;
                    clampG();
                    //  (a resize never pushes the dialog further off a side than it was: only a move may do that. A dialog moved partly
                    //  off screen keeps its place, the edge under the pointer follows it; before, the whole width was put back on screen,
                    //  a jump of 352 px on the first resize move, S7a-D7)
                    const lo = Math.min(0, r.left);
                    g.L = clamp(g.L, lo, Math.max(lo, vw() - g.W, r.right > vw() + 0.5 ? r.left : lo));
                    apply();
                };
                const up = ev => {
                    if (ev.pointerId !== e.pointerId)
                        return;
                    h.removeEventListener("pointermove", move);
                    h.removeEventListener("pointerup", up);
                    h.removeEventListener("pointercancel", up);
                    h.removeEventListener("lostpointercapture", up);
                    box.classList.remove("sw-dragging");
                    g.Moved = true;
                    persist();
                    fitContent();
                };
                h.addEventListener("pointermove", move);
                h.addEventListener("pointerup", up);
                h.addEventListener("pointercancel", up);
                //  (a lost capture ends it too: no resize left running, OWN3-Down-2)
                h.addEventListener("lostpointercapture", up);
            });
            box.append(h);
        }

        //  Sheets (phone portrait): a handle; drag up makes the sheet taller, drag down is a close attempt (swipe)
        const handle = el("div.sw-sheet-handle", { attrs: { "aria-hidden": "true" } });
        box.prepend(handle);
        const sheetDrag = e => {
            if (!isSheetLayout() || e.button !== 0 || mode.Kind || (e.target.closest && e.target.closest(NoDrag)))
                return;
            const sy = e.clientY, h0 = box.getBoundingClientRect().height;
            let active = false, dy = 0, lastY = sy, lastT = e.timeStamp, vel = 0;
            const move = ev => {
                if (ev.pointerId !== e.pointerId)
                    return;
                dy = ev.clientY - sy;
                if (!active) {
                    if (Math.abs(dy) <= 10)
                        return;
                    active = true;
                    box.classList.add("sw-sheet-drag");
                }
                if (ev.timeStamp > lastT)
                    vel = (ev.clientY - lastY) / (ev.timeStamp - lastT);
                lastY = ev.clientY;
                lastT = ev.timeStamp;
                if (dy > 0)
                    box.style.setProperty("--sw-sheet-dy", dy + "px");
                else {
                    box.style.setProperty("--sw-sheet-dy", "0px");
                    g.SheetH = Math.min(h0 - dy, vh() - 24);
                    apply();
                }
            };
            const up = ev => {
                if (ev.pointerId !== e.pointerId)
                    return;
                window.removeEventListener("pointermove", move, true);
                window.removeEventListener("pointerup", up, true);
                window.removeEventListener("pointercancel", up, true);
                box.classList.remove("sw-sheet-drag");
                box.style.removeProperty("--sw-sheet-dy");
                if (active && dy > 0 && (dy > Math.min(160, h0 * 0.3) || (dy > 40 && vel > 0.6)))
                    attempt("swipe");
            };
            window.addEventListener("pointermove", move, true);
            window.addEventListener("pointerup", up, true);
            window.addEventListener("pointercancel", up, true);
        };
        handle.addEventListener("pointerdown", sheetDrag);
        head.addEventListener("pointerdown", sheetDrag);

        /**
         * One step of the Move / Size mode (step buttons and arrow keys).
         * @param {number} dx Horizontal px.
         * @param {number} dy Vertical px.
         */
        const stepBy = (dx, dy) => {
            if (mode.Kind === "move") {
                g.L += dx;
                g.T += dy;
                g.Moved = true;
            }
            else if (isSheetLayout()) {
                g.SheetH = clamp((g.SheetH || box.getBoundingClientRect().height) - dy, 160, vh() - 24);
            }
            else {
                g.W += dx;
                g.H += dy;
                g.Sized = true;
            }
            clampG();
            apply();
            announcePos();
        };
        /**
         * End the Move / Size mode.
         * @param {boolean} revert Go back to the geometry before the mode (Escape, Revert).
         */
        const endMode = revert => {
            if (!mode.Kind)
                return;
            if (revert) {
                Object.assign(g, mode.Start);
                apply();
            }
            else
                persist();
            mode.Bar && mode.Bar.remove();
            mode.Bar = null;
            mode.Kind = null;
            box.classList.remove("sw-mode");
            announce(revert ? _TF("Dialog position and size reverted", "Screen reader announcement after the user left the move / resize mode of a dialog with Escape or Revert")
                : _TF("Dialog position and size kept", "Screen reader announcement after the user finished moving / resizing a dialog with Enter or Done"), box);
            if (mode.Opener && mode.Opener.isConnected)
                Focus.Set(mode.Opener);
        };
        /**
         * Start the Move ("move") or Size ("size") mode: arrows move / resize by 10 px (Shift: 50 px), Enter keeps, Escape reverts;
         * on-screen step buttons (50 px), Done and Revert do the same without dragging (WCAG 2.5.7).
         * @param {string} kind "move" | "size".
         * @param {HTMLElement} opener The element to focus after the mode.
         */
        const startMode = (kind, opener) => {
            endMode(true);
            const sheet = isSheetLayout();
            if (g.Max)
                g.Max = false;
            if (!sheet) {
                place();
                if (kind === "size" && !g.Sized) {
                    const r = box.getBoundingClientRect();
                    g.W = r.width;
                    g.H = r.height;
                    g.Sized = true;
                }
                apply();
            }
            mode.Kind = kind;
            mode.Start = Object.assign({}, g);
            mode.Opener = opener;
            const B = (icon, title, dx, dy) => new Sw.Button({ Icon: icon, Title: title, Kind: "ghost", Class: "sw-dialog-step", OnClick: () => stepBy(dx, dy) }).E;
            const steps = kind === "move" ? [
                B("up", _TF("Move up 50 px", "Tool tip of a step button that moves a dialog up by 50 pixels"), 0, -50),
                B("down", _TF("Move down 50 px", "Tool tip of a step button that moves a dialog down by 50 pixels"), 0, 50),
                B("left", _TF("Move left 50 px", "Tool tip of a step button that moves a dialog to the left by 50 pixels"), -50, 0),
                B("right", _TF("Move right 50 px", "Tool tip of a step button that moves a dialog to the right by 50 pixels"), 50, 0),
            ] : sheet ? [
                B("up", _TF("Make taller 50 px", "Tool tip of a step button that makes a dialog 50 pixels taller"), 0, -50),
                B("down", _TF("Make shorter 50 px", "Tool tip of a step button that makes a dialog 50 pixels shorter"), 0, 50),
            ] : [
                B("left", _TF("Make narrower 50 px", "Tool tip of a step button that makes a dialog 50 pixels narrower"), -50, 0),
                B("right", _TF("Make wider 50 px", "Tool tip of a step button that makes a dialog 50 pixels wider"), 50, 0),
                B("up", _TF("Make shorter 50 px", "Tool tip of a step button that makes a dialog 50 pixels shorter"), 0, -50),
                B("down", _TF("Make taller 50 px", "Tool tip of a step button that makes a dialog 50 pixels taller"), 0, 50),
            ];
            const revert = new Sw.Button({ Text: _TF("Revert", "Button that ends moving / resizing a dialog and puts it back where it was (Escape)"), Small: true, OnClick: () => endMode(true) });
            const done = new Sw.Button({ Text: _TF("Done", "Button that ends moving / resizing a dialog and keeps the new position and size (Enter)"), Kind: "primary", Small: true, OnClick: () => endMode(false) });
            mode.Bar = el("div.sw-dialog-modebar", {
                attrs: { role: "toolbar", "aria-label": kind === "move" ? _TF("Move the dialog", "Accessible name of the tool bar with step buttons that move a dialog") : _TF("Resize the dialog", "Accessible name of the tool bar with step buttons that resize a dialog") },
            }, el("span.sw-dialog-modelabel", { text: kind === "move" ? _TF("Move", "Label of the tool bar shown while a dialog is moved with step buttons or arrow keys") : _TF("Size", "Label of the tool bar shown while a dialog is resized with step buttons or arrow keys") }),
            el("span.sw-dialog-steps", null, ...steps), revert.E, done.E);
            box.append(mode.Bar);
            box.classList.add("sw-mode");
            Focus.Set(steps[0]);
            announce(kind === "move"
                ? _TF("Move mode: arrow keys move the dialog, Shift for larger steps, Enter keeps, Escape reverts", "Screen reader announcement when the move mode of a dialog starts (dialog menu, Move)")
                : _TF("Size mode: arrow keys resize the dialog, Shift for larger steps, Enter keeps, Escape reverts", "Screen reader announcement when the resize mode of a dialog starts (dialog menu, Size)"), box);
        };
        //  Keys of the Move / Size mode (capture: before fields, buttons and the close attempt of Escape)
        box.addEventListener("keydown", e => {
            if (!mode.Kind || e.ctrlKey || e.altKey || e.metaKey)
                return;
            const n = e.shiftKey ? 50 : 10;
            const d = { ArrowUp: [0, -n], ArrowDown: [0, n], ArrowLeft: [-n, 0], ArrowRight: [n, 0] }[e.key];
            if (d)
                stepBy(d[0], d[1]);
            else if (e.key === "Enter")
                endMode(false);
            else if (e.key === "Escape")
                endMode(true);
            else
                return;
            e.preventDefault();
            e.stopPropagation();
        }, true);

        const onResize = () => {
            if (g.Placed || g.Sized) {
                //  (during a drag only the title bar must stay on screen: a whole box clamp would fight the pointer)
                clampG(!box.classList.contains("sw-dragging"));
                apply();
            }
        };
        window.addEventListener("resize", onResize);

        return {
            /**
             * Show the dialog menu.
             * @param {HTMLElement} anchor The menu button.
             */
            Menu(anchor) {
                const items = [];
                if (!isPhoneLayout()) {
                    items.push({ Text: _TF("Move", "Dialog menu item: move the dialog with arrow keys or step buttons"), Icon: "drag", Title: _TF("Move the dialog with the arrow keys or step buttons; dragging the title bar works too", "Tool tip of the Move item of the dialog menu"), OnClick: () => startMode("move", anchor) });
                    items.push({ Text: _TF("Size", "Dialog menu item: resize the dialog with arrow keys or step buttons"), Icon: "columns", Title: _TF("Resize the dialog with the arrow keys or step buttons; dragging an edge works too", "Tool tip of the Size item of the dialog menu"), OnClick: () => startMode("size", anchor) });
                    items.push(g.Max
                        ? { Text: _TF("Restore", "Dialog menu item: give a maximized dialog its previous size back"), Icon: "fullscreenExit", Title: _TF("Back to the previous size (double click on the title bar)", "Tool tip of the Restore item of the dialog menu"), OnClick: () => toggleMax(false) }
                        : { Text: _TF("Maximize", "Dialog menu item: make the dialog fill the window"), Icon: "fullscreen", Title: _TF("Fill the window (double click on the title bar)", "Tool tip of the Maximize item of the dialog menu"), OnClick: () => toggleMax(true) });
                }
                else if (isSheetLayout())
                    items.push({ Text: _TF("Size", "Dialog menu item: resize the dialog with arrow keys or step buttons"), Icon: "columns", Title: _TF("Make the sheet taller or shorter with step buttons; dragging the handle works too", "Tool tip of the Size item of the dialog menu on a phone"), OnClick: () => startMode("size", anchor) });
                items.push({ Text: _TF("Reset position", "Dialog menu item: center the dialog again with its default size"), Icon: "reset", Title: _TF("Center the dialog with its default size and forget the stored position", "Tool tip of the Reset position item of the dialog menu"), OnClick: reset });
                Menu.Show(anchor, items, { Align: "end", Label: _TF("Dialog options", "Accessible name of the menu of a dialog (move, size, maximize, reset position)") });
            },
            /** Restore the remembered geometry (clamped; nothing in the phone layout, where the css ignores it anyway). */
            Restore() {
                const s = id ? loadGeo(id) : null;
                if (!s)
                    return;
                if (s.Sized && s.W > 0 && s.H > 0) {
                    g.W = +s.W;
                    g.H = +s.H;
                    g.Sized = true;
                }
                if (s.Moved) {
                    g.L = +s.L || 0;
                    g.T = +s.T || 0;
                    g.Moved = g.Placed = true;
                }
                else if (g.Sized) {
                    g.L = (vw() - g.W) / 2;
                    g.T = (vh() - g.H) / 2;
                    g.Placed = true;
                }
                g.Max = !!s.Max;
                clampG(true);
                apply();
                fitContent();
            },
            /** Remove the window listeners. */
            Stop() {
                clearTimeout(posTimer);
                window.removeEventListener("resize", onResize);
            },
            /**
             * Test if the Move / Size mode is on.
             * @returns {boolean} True in a mode.
             */
            InMode: () => !!mode.Kind,
        };
    }

    // ------------------------------------------------------------------ dialog helpers (ids, time so far, focus on touch, debug checks)

    /**
     * A stable id of a dialog kind when the caller gave none (UX-POP-7: every dialog kind remembers its position): the page path, the
     * role and class, the title with its numbers taken out and the button labels, hashed. Only a moved / resized dialog stores anything.
     * @param {object} o The dialog options.
     * @returns {string} The id ("auto.<hash>").
     */
    function defaultDialogId(o) {
        let path = "";
        try {
            path = location.pathname;
        }
        catch {
        }
        const src = [path, o.Role || "", o.Class || "", o.Wide ? "w" : "", String(o.Title || o.AriaLabel || "").replace(/\d+/g, "#"),
            (o.Buttons || []).filter(b => b).map(b => typeof b.Text === "string" ? b.Text : "").join("|")].join("\u0001");
        let h = 2166136261;
        for (let i = 0; i < src.length; ++i) {
            h ^= src.charCodeAt(i);
            h = Math.imul(h, 16777619);
        }
        return "auto." + (h >>> 0).toString(36);
    }

    /**
     * The time so far of busy work in one format ("9 s", "10 s", "1 min 5 s"), the same as the activity list.
     * @param {number} ms Milliseconds.
     * @returns {string} The text.
     */
    function elapsedText(ms) {
        const s = Math.max(0, Math.floor(ms / 1000));
        if (s < 60)
            return _T("{0} s", s, "Time so far of busy work, ex: 12 s. {0} is the whole number of seconds, ex: 12");
        return _T("{0} min {1} s", Math.floor(s / 60), s % 60, "Time so far of busy work, ex: 1 min 5 s. {0} is the whole number of minutes, ex: 1. {1} is the whole number of seconds, ex: 5");
    }

    /**
     * aria-disabled with a reason (Sw.Disabled from ui/base.js when present, else the attribute).
     * @param {HTMLElement} e The element.
     * @param {boolean} on Disabled.
     * @param {string} [reason] The reason.
     */
    function setDisabled(e, on, reason) {
        if (Sw.Disabled && typeof Sw.Disabled.Set === "function") {
            Sw.Disabled.Set(e, on, reason);
            return;
        }
        e.toggleAttribute("aria-disabled", on);
        if (on)
            e.setAttribute("aria-disabled", "true");
    }

    /**
     * @param {HTMLElement} e The element.
     * @returns {boolean} True when it is disabled (aria-disabled or disabled).
     */
    function isDisabled(e) {
        if (Sw.Disabled && typeof Sw.Disabled.Is === "function")
            return !!Sw.Disabled.Is(e);
        return e.disabled === true || e.getAttribute("aria-disabled") === "true";
    }

    /** @type {string} The pointer type of the last press ("touch", "mouse", "pen"). */
    let lastPointerType = "";
    window.addEventListener("pointerdown", e => lastPointerType = e.pointerType || "", true);

    /**
     * True when the user works with touch: the last press was a touch, or (no press yet) the device has no hover and a coarse pointer.
     * @returns {boolean} Touch first.
     */
    function touchFirst() {
        if (lastPointerType)
            return lastPointerType === "touch";
        try {
            return window.matchMedia("(hover: none) and (pointer: coarse)").matches;
        }
        catch {
            return false;
        }
    }

    /**
     * True for an element that opens the on-screen keyboard when focused.
     * @param {Element} e The element.
     * @returns {boolean} A text entry.
     */
    function isTextEntry(e) {
        if (!e || !e.tagName)
            return false;
        if (e.isContentEditable || e.tagName === "TEXTAREA")
            return true;
        return e.tagName === "INPUT" && !/^(checkbox|radio|range|button|submit|reset|image|file|color|hidden)$/.test(e.type || "");
    }

    /**
     * The label problems of the visible buttons in a root (Sw.Dialog.CheckLabels): a word broken over two lines, a label cut without
     * … and its full text as the tool tip.
     * @param {ParentNode} root Where to look.
     * @returns {string[]} The problems.
     */
    function labelProblems(root) {
        const out = [];
        for (const b of root.querySelectorAll(".sw-button")) {
            if (b.offsetParent === null || b.closest("[inert], [aria-hidden='true'], .sw-leaving"))
                continue;
            const t = b.querySelector(".sw-button-text");
            const name = t ? t.textContent.trim().replace(/\s+/g, " ") : "";
            if (!name)
                continue;
            //  every word on one line: its rectangles all share a line (Range rects per word)
            let broken = false;
            const walk = document.createTreeWalker(t, NodeFilter.SHOW_TEXT);
            for (let n = walk.nextNode(); n && !broken; n = walk.nextNode()) {
                const rx = /\S+/g;
                let m;
                while (!broken && (m = rx.exec(n.data))) {
                    const r = document.createRange();
                    r.setStart(n, m.index);
                    r.setEnd(n, m.index + m[0].length);
                    const tops = Array.from(r.getClientRects()).filter(q => q.width > 0.5 && q.height > 0.5).map(q => q.top);
                    if (tops.length > 1 && Math.max(...tops) - Math.min(...tops) > 2)
                        broken = true;
                }
            }
            if (broken)
                out.push("the label \"" + name.slice(0, 40) + "\" breaks inside a word");
            //  cut: only with … and the full text as its tool tip or name
            const cs = getComputedStyle(t);
            if (t.scrollWidth > t.clientWidth + 1 && !(cs.textOverflow === "ellipsis" && (b.title || b.getAttribute("aria-label"))))
                out.push("the label \"" + name.slice(0, 40) + "\" is cut" + (cs.textOverflow === "ellipsis" ? " without its full text as the tool tip" : " off"));
        }
        return out;
    }

    /**
     * Developer mode (Sw.DevMode): warn when an existing footer button of an open dialog moves, or a title bar control while the dialog is
     * busy or shows its line (LAY-1, POP-6: buttons never jump, ex: a status
     * text that wraps the footer while the user reaches for Cancel). Positions on screen; moving / resizing / maximizing the dialog
     * (by the user) and window resizes don't count.
     * @param {HTMLElement} box The dialog.
     * @param {function():HTMLElement|null} footOf Returns the footer.
     * @param {string} name The dialog name for the message.
     * @param {function():boolean} [heldOf] True while the dialog keeps its geometry (busy, its line shows): title bar moves count then.
     * @returns {function():void} Stops watching.
     */
    function debugWatch(box, footOf, name, heldOf) {
        if (typeof ResizeObserver !== "function" || typeof MutationObserver !== "function")
            return () => { };
        let last = new Map(), raf = 0, resized = 0, geoSig = "", ready = false;
        const sigOf = () => ["sw-max", "sw-placed", "sw-sized", "sw-sheet-sized"].map(c => box.classList.contains(c) ? 1 : 0).join("") +
            ["--sw-dlg-l", "--sw-dlg-t", "--sw-dlg-w", "--sw-dlg-h", "--sw-sheet-h"].map(p => box.style.getPropertyValue(p)).join(",");
        const pos = () => {
            const f = footOf();
            const m = new Map();
            if (!f)
                return m;
            const head = box.querySelectorAll(":scope > .sw-dialog-head .sw-dialog-title, :scope > .sw-dialog-head .sw-button");
            for (const b of Array.from(f.querySelectorAll(":scope > .sw-button")).concat(Array.from(head)))
                if (b.offsetParent !== null) {
                    const r = b.getBoundingClientRect();
                    m.set(b, [Math.round(r.left), Math.round(r.top)]);
                }
            return m;
        };
        const check = () => {
            raf = 0;
            const now = pos();
            const sig = sigOf();
            //  (an animation of the box, ex: a sheet sliding in, a pulse, moves its buttons on purpose)
            let animating = false;
            try {
                animating = typeof box.getAnimations === "function" && box.getAnimations().some(a => a.playState === "running" && a.effect && a.effect.target === box);
            }
            catch {
            }
            const userGeo = sig !== geoSig || animating;
            geoSig = sig;
            //  not during the opening transition (a sheet slides in) and not after a user / window geometry change
            if (!userGeo && ready && !box.matches(".sw-dragging, .sw-mode, .sw-sheet-drag, .sw-leaving") && Date.now() - resized > 400)
                for (const [b, p] of now) {
                    const q = last.get(b);
                    if (q && (Math.abs(q[0] - p[0]) > 2 || Math.abs(q[1] - p[1]) > 2) && (!b.closest(".sw-dialog-head") || (heldOf && heldOf())))
                        devWarn("Sw.Dialog \"" + name + "\": " + (b.closest(".sw-dialog-head") ? "the title bar control \"" : "the footer button \"") + (b.textContent.trim() || b.getAttribute("aria-label") || "") + "\" moved " +
                            Math.round(Math.hypot(q[0] - p[0], q[1] - p[1])) + " px (UX LAY-1: buttons never move; reserve the space, add buttons left of the existing ones).", "dlg-move:" + name);
                }
            last = now;
            //  labels never break inside a word, a cut label has its full text as the tool tip (TXT-17)
            if (ready && !box.matches(".sw-leaving"))
                for (const p of labelProblems(box))
                    devWarn("Sw.Dialog \"" + name + "\": " + p + " (UX TXT-17: a label wraps only between words; give it room (Labels: [...]) or a shorter text).", "dlg-label:" + name + ":" + p);
        };
        const later = () => {
            if (!raf)
                //  the PAINTED state: a task after the next frame (the dialog's own fixes, kept slots and the kept bottom edge, run in
                //  ResizeObservers after the frame's animation callbacks, before its paint; a check inside a callback would see the
                //  layout before them)
                raf = requestAnimationFrame(() => setTimeout(check, 0));
        };
        const onResize = () => resized = Date.now();
        const ro = new ResizeObserver(later);
        ro.observe(box);
        const mo = new MutationObserver(() => {
            const f = footOf();
            if (f) {
                ro.observe(f);
                for (const b of f.children)
                    ro.observe(b);
            }
            later();
        });
        mo.observe(box, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ["hidden", "class"] });
        window.addEventListener("resize", onResize);
        last = pos();
        geoSig = sigOf();
        //  the positions after the opening transition are the reference (not those taken while a sheet slid in; a busy machine runs
        //  the opening animation late: its end is waited for)
        const settleNow = () => {
            last = pos();
            geoSig = sigOf();
            ready = true;
        };
        const settle = setTimeout(() => {
            let an = [];
            try {
                an = box.getAnimations({ subtree: true }).filter(a => a.playState === "running" && !(a.effect && a.effect.getTiming && a.effect.getTiming().iterations === Infinity));
            }
            catch {
            }
            if (an.length)
                Promise.all(an.map(a => a.finished.catch(() => { }))).then(() => requestAnimationFrame(settleNow));
            else
                settleNow();
        }, 520);
        const f0 = footOf();
        if (f0) {
            ro.observe(f0);
            for (const b of f0.children)
                ro.observe(b);
        }
        return () => {
            ro.disconnect();
            mo.disconnect();
            cancelAnimationFrame(raf);
            clearTimeout(settle);
            window.removeEventListener("resize", onResize);
        };
    }

    /**
     * Developer mode (Sw.DevMode): warn when something the framework lays over a dialog (its rejection line, a toast in it) covers one of
     * that dialog's own controls or its title (POP-6: the line never covers the title bar, ⋮, × or a button; FBK-8: toasts never cover
     * actions). Checked after the layout settled.
     * @param {HTMLElement} box The dialog.
     * @param {string} name The dialog name for the message.
     */
    function coverCheck(box, name) {
        if (!devMode() || !box || !box.isConnected)
            return;
        //  after the reveal transitions (the status row opens over --sw-dur-2)
        setTimeout(() => {
            if (!box.isConnected || box.classList.contains("sw-leaving"))
                return;
            const layers = Array.from(box.querySelectorAll(".sw-dialog-reject:not([hidden]), .sw-toast:not(.sw-hide)"));
            if (!layers.length)
                return;
            const inLayer = c => layers.some(v => v.contains(c));
            const ctrls = Array.from(box.querySelectorAll(".sw-dialog-title, button, input, select, textarea, a[href], [role=button], [role=switch]"))
                .filter(c => c.offsetParent !== null && !inLayer(c) && !c.closest(".sw-toasts, .sw-dialog-slot-off") && getComputedStyle(c).visibility !== "hidden");
            for (const v of layers) {
                //  the visible part of the line: its row (sw-dialog-status-in, overflow hidden) clips it while the row opens (the theme's
                //  --sw-dur-2: 400 ms in Hacker, also with reduced motion, owner 2026-10-28); the whole line box "covered" the field below
                //  the half open row (dialogtest at 320x180, S8)
                const a0 = v.getBoundingClientRect();
                const clip = v.closest(".sw-dialog-status-in");
                const cr = clip ? clip.getBoundingClientRect() : a0;
                const a = { top: Math.max(a0.top, cr.top), bottom: Math.min(a0.bottom, cr.bottom), left: Math.max(a0.left, cr.left), right: Math.min(a0.right, cr.right) };
                a.width = a.right - a.left;
                a.height = a.bottom - a.top;
                if (!(a.width > 0) || !(a.height > 0))
                    continue;
                for (const c of ctrls) {
                    //  the visible part of the control (a field scrolled out of the body's view is not covered by anything)
                    const r0 = c.getBoundingClientRect();
                    const sb = c.closest(".sw-dialog-body");
                    const br = sb ? sb.getBoundingClientRect() : r0;
                    const r = { top: Math.max(r0.top, br.top), bottom: Math.min(r0.bottom, br.bottom), left: Math.max(r0.left, br.left), right: Math.min(r0.right, br.right) };
                    if (r.bottom - r.top > 1 && r.right - r.left > 1 && r.top < a.bottom - 1 && r.bottom > a.top + 1 && r.left < a.right - 1 && r.right > a.left + 1) {
                        const what = v.classList.contains("sw-toast") ? "a toast" : "the rejection line";
                        const nm = (c.getAttribute("aria-label") || c.textContent || c.title || c.tagName).trim().slice(0, 40);
                        devWarn("Sw.Dialog \"" + name + "\": " + what + " covers its control \"" + nm + "\" (UX POP-6 / FBK-8: an overlay never covers the title bar, its buttons, the fields or the focus).", "dlg-cover:" + name + ":" + what);
                        return;
                    }
                }
            }
        }, 350);
    }

    /** @type {Set<function():void>} Listeners of Sw.Dialog.OnChange (a dialog opened or closed). */
    const dialogFns = new Set();

    /** Tell the Sw.Dialog.OnChange listeners. */
    function notifyDialogs() {
        for (const f of Array.from(dialogFns))
            try {
                f();
            }
            catch (x) {
                console.error(x);
            }
    }

    // ------------------------------------------------------------------ option schemas (the one registry: Sw.DevOptions.Register)

    /** The placement options of Sw.Place / Sw.Track (and of every pop-up placed with them). */
    const PlaceSchema = { Side: { Type: "string", Values: ["below", "above", "left", "right"] }, Align: { Type: "string", Values: ["start", "end", "center"] }, Gap: "number", Margin: "number" };
    /** The options of Sw.Popup.Show. */
    const PopupSchema = Object.assign({}, PlaceSchema, {
        Dim: "boolean", Class: "string", MinWidth: "number", Modal: "boolean", NoEscape: "boolean", NoLayer: "boolean", NoTrap: "boolean",
        Focus: "boolean|number|element|string", Role: "string", Label: "string", OnClose: "function", Host: "element", BeforeRestore: "function", Owner: "any",
    });
    /** The options of Sw.Menu.Show / Sw.Menu.Context. */
    const MenuSchema = Object.assign({}, PopupSchema, { NoFocus: "boolean", DrillDown: "boolean" });
    /** The options of Sw.Dialog.Open / Sw.Dialog.Show. */
    const DialogSchema = {
        Title: "string|element", Icon: "any", Content: "any", Buttons: "array", Id: "string", NoMove: "boolean", Wide: "any", Full: "boolean",
        Close: "boolean", Modal: "boolean", NoEscape: "boolean", EnterValue: "any", Focus: "element|string|boolean", AutoDirty: "boolean", BusyButtons: "any",
        Above: "boolean", Class: "string", AriaLabel: "string", Role: "string", Describe: "boolean|string|element", OnShow: "function", OnClose: "function",
        Reserve: "boolean", TouchFocus: "boolean", Owner: "any", EditWhileBusy: "boolean",
    };
    /** A footer button of a dialog (Button is set by the dialog: its Sw.Button). */
    const DialogButtonSchema = {
        Text: "string", Kind: "string", Icon: "any", Title: { Type: "string" }, Label: "string", Labels: "array", Value: "any", Close: "boolean",
        Dismiss: "boolean|string", OnClick: "function", Class: "string", Disabled: "any", WhileBusy: "boolean", Cancellable: "boolean", Button: "any",
    };
    /** The options of Sw.Dialog.Confirm (the 4th argument; the options form adds Title, Text and OkText). */
    const ConfirmSchema = {
        CancelText: "string", Danger: "boolean", Safe: "boolean", OkIcon: "string", CancelIcon: "string", OkTitle: "string", CancelTitle: "string",
        Icon: "string", Target: "string", Id: "string", Owner: "any",
    };
    /** The options of the framework's own dialogs and the other overlays. */
    const OverlaySchemas = {
        "Sw.Place": PlaceSchema,
        "Sw.Track": PlaceSchema,
        "Sw.Popup.Show": PopupSchema,
        "Sw.Menu.Show": MenuSchema,
        "Sw.Menu.Context": MenuSchema,
        "Sw.Dialog.Open": DialogSchema,
        "Sw.Dialog.Show": DialogSchema,
        "Sw.Dialog button": DialogButtonSchema,
        "Sw.Dialog.Confirm": ConfirmSchema,
        //  (the options form: Confirm({ Title, Text, OkText, ...the options }); FD S5b N2: a wrong name there was checked against the
        //  options only, so the warning left out the three names the author needed)
        "Sw.Dialog.Confirm({...})": Object.assign({ Title: "string", Text: "string", OkText: "string" }, ConfirmSchema),
        "Sw.Dialog.Form": {
            Text: "string", OkText: "string", CancelText: "string", Id: "string", Wide: "any", Validate: "function", OnOk: "function", OnChange: "function", OnReady: "function", After: "element",
        },
        "Sw.Dialog.Prompt": {
            Text: "string", Label: "string", Multiline: "boolean", Rows: "number", Type: "string", Validate: "function", OkText: "string", CancelText: "string",
            Mono: "boolean", Id: "string",
        },
        "Sw.Dialog.Select": { Title: "string", Tip: "string", Placeholder: "string", StartText: "string", GetAll: "boolean", Items: "function", Wide: "boolean", Id: "string" },
        "Sw.Dialog.Working": {
            Title: { Type: "string" }, Text: "string", Icon: "string", Cancel: "function", Background: "boolean|function", Promise: "promise|object", Progress: { Type: "number", Min: 0, Max: 1 },
            CloseText: "string", Stop: "function", Id: "string", NoMove: "boolean",
        },
        "Sw.Dialog.DangerTitle": { Name: "boolean" },
        "Sw.Toast": {
            Kind: { Type: "string", Values: ["", "error", "ok", "info", "warning", "danger", "success"] }, Duration: "number|boolean", NoCopy: "boolean", Title: "string",
            Action: { Type: "object", Check: v => v && typeof v.Text === "string" && typeof v.OnClick === "function" ? "" : "Action is { Text, OnClick(event) }." },
            Details: "any",
        },
        "Sw.Loader.Show": { Target: "element", Element: "element", Text: "string", Opaque: "boolean", Icon: "string", Delay: "number" },
        "Sw.Motion.Out": { Class: "string", Keep: "boolean", Done: "function" },
        "Sw.Overlay.Register": { Name: "string", Owns: "any", Settle: "number" },
        "Sw.Overlay.Covers": { Name: "string", Owns: "any" },
    };
    try {
        if (Sw.DevOptions && typeof Sw.DevOptions.Register === "function")
            for (const k of Object.keys(OverlaySchemas))
                Sw.DevOptions.Register(k, OverlaySchemas[k]);
    }
    catch {
        //  (an older core: the local check below)
    }

    /**
     * Developer check of an overlay api's options (unknown names: a typo, an option of another api; wrong types), through the one
     * shared check of the core (Sw.DevOptions with the registered schema), else the local fallback (unknown names).
     * @param {string} where The api ("Sw.Toast"; a dialog button: "Sw.Dialog button \"Save\"").
     * @param {object} o The options.
     * @param {string} [schemaName] The registered schema (default where).
     */
    function checkOpts(where, o, schemaName) {
        if (!o || typeof o !== "object" || !devMode())
            return;
        const schema = OverlaySchemas[schemaName || where];
        if (!schema)
            return;
        if (typeof Sw.DevOptions === "function") {
            //  (null means "not given" in the options of the overlays)
            const oo = {};
            for (const k of Object.keys(o))
                if (o[k] !== null)
                    oo[k] = o[k];
            Sw.DevOptions(where, oo, schema);
            return;
        }
        for (const k of Object.keys(o))
            if (!(k in schema) && k[0] !== "_")
                devWarn(where + ": unknown option \"" + k + "\" (ignored; known: " + Object.keys(schema).join(", ") + ").", "dlg-key:" + where + ":" + k);
    }

    // ------------------------------------------------------------------ dialogs

    /**
     * Sw.Dialog.Confirm (see there). Cancel has the focus and Enter never confirms unless the action is marked Safe (UX-SAF-3: the safe
     * choice has the focus; a plain Confirm is often destructive in practice).
     * @param {string} title Title.
     * @param {string} [text] Text.
     * @param {string} [okText] The verb of the action.
     * @param {object} [o] Options.
     * @returns {Promise<boolean>} True if confirmed.
     */
    async function confirmImpl(title, text, okText, o) {
        o = o || {};
        if (!title && o.Target)
            title = Dialog.DangerTitle(okText || "", o.Target);
        if (!okText || /^(ok|okay|yes|no)$/i.test(String(okText).trim()))
            devWarn("Sw.Dialog.Confirm \"" + title + "\": name the action on the button (\"Delete\", \"Save\"), never OK / Yes / No (UX-POP-9).", "dlg-ok:" + title);
        const safe = o.Safe === true && !o.Danger;
        //  The text is selectable (copy with the selection); a plain click never copies (UX-INP-31, LST-4)
        const content = text ? el("div.sw-dialog-text", { text }) : null;
        const v = await Dialog.Show({
            Title: title, Icon: o.Icon ?? (o.Danger ? "warning" : undefined), Content: content, Role: "alertdialog", Id: o.Id || "sw.confirm", Owner: o.Owner,
            Buttons: [
                { Text: o.CancelText || _TF("Cancel", "The text of a button on a confirmation dialog that when pressed will close the dialog without performing the action"), Icon: o.CancelIcon, Title: o.CancelTitle, Value: false, Class: "sw-dialog-cancel" },
                { Text: okText || _TF("OK", "The text of a button on a confirmation dialog that when pressed will confirm that the user wants to perform the action"), Icon: o.OkIcon, Title: o.OkTitle, Kind: o.Danger ? "danger" : "primary", Value: true, Class: "sw-dialog-ok" },
            ],
            //  The safe choice is the default: Cancel has the focus (Enter on it cancels) and Enter elsewhere does nothing; only a
            //  Safe action focuses OK and confirms on Enter
            Focus: safe ? ".sw-dialog-ok" : ".sw-dialog-cancel",
            EnterValue: safe ? true : undefined,
            //  a confirmation holds no user input: never "Discard changes?"
            AutoDirty: false,
        });
        return v === true;
    }

    /**
     * Sw.Dialog.Prompt (see there).
     * @param {string} title Title.
     * @param {string} [value] Initial value.
     * @param {string} [placeholder] Placeholder.
     * @param {object} [o] Options.
     * @returns {Promise<string|null>} The text, or null if cancelled.
     */
    async function promptImpl(title, value, placeholder, o) {
        o = o || {};
        const tb = new Sw.TextBox({ Value: value || "", Placeholder: placeholder || "", Label: o.Label, Multiline: o.Multiline, Type: o.Type, Validate: o.Validate, Mono: o.Mono, Rows: o.Rows });
        const content = el("div.sw-dialog-prompt", null, o.Text ? el("div.sw-dialog-text", { text: o.Text }) : null, tb.E);
        const v = await Dialog.Show({
            Title: title, Id: o.Id || "sw.prompt",
            Content: content,
            Buttons: [
                { Text: o.CancelText || _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: null },
                {
                    Text: o.OkText || _TF("OK", "The text of a button on a dialog that when pressed will accept the entered value"), Kind: "primary", Value: "ok",
                    OnClick: () => tb.Commit(),
                },
            ],
            EnterValue: () => tb.Commit() ? "ok" : undefined,
            Focus: tb.Input,
            //  select the text only when the field got the focus (on touch it doesn't: no keyboard pops up unasked)
            OnShow: () => {
                if (document.activeElement === tb.Input)
                    tb.Input.select();
            },
        });
        return v === "ok" ? tb.Get().trim() : null;
    }

    /**
     * Sw.Dialog.Form (see there).
     * @param {string} title Title.
     * @param {object[]} defs The fields.
     * @param {object} [values] Initial values.
     * @param {object} [o] Options.
     * @returns {Promise<object|null>} The values, or null if cancelled.
     */
    async function formImpl(title, defs, values, o) {
        o = o || {};
        const cur = {};
        for (const d of defs || [])
            cur[d.Key] = values && values[d.Key] !== undefined ? values[d.Key] : d.Default;
        //  A form starts unmodified: the initial values are the fields' defaults (no "modified" marks; a reset goes back to them)
        const pe = new Sw.PropertyEditor({
            Defs: (defs || []).map(d => Object.assign({}, d, { Default: cur[d.Key] })), Values: cur, Hints: o.Hints,
            OnInput: (k, v) => cur[k] = v,
            OnChange: (k, v) => {
                cur[k] = v;
                pe.SetError(k, null);
                if (o.OnChange)
                    o.OnChange(k, v, cur, pe);
            },
        });
        let box = null;
        const content = el("div.sw-dialog-form", null, o.Text ? el("div.sw-dialog-text", { text: o.Text }) : null, pe.E, o.After || null);
        //  The OK button can be disabled by the form (ex: nothing to write): ctl.SetOk(enabled, reason)
        let okOff = null;
        const applyOk = () => {
            const b = box && box.querySelector(".sw-dialog-foot .sw-button:last-child");
            if (!b)
                return;
            //  Reachable while disabled (UX INP-12): a click or Enter says why (the reason is also the tool tip)
            Sw.Disabled.Set(b, okOff !== null, okOff !== null ? { Reason: okOff || _TF("Not available yet", "Reason of a disabled OK button of a form when the form gives none") } : undefined);
        };
        const ctl = {
            SetOk: (enabled, reason) => {
                okOff = enabled ? null : (reason || "");
                applyOk();
            },
            //  Set fields as if typed (ex: a one click fix of a hint): the values, OnChange per field
            Set: values => {
                //  (SetValues shows defaults for the keys it isn't given: give it every value)
                pe.SetValues(Object.assign({}, cur, values));
                for (const [k, v] of Object.entries(values || {})) {
                    cur[k] = v;
                    pe.SetError(k, null);
                    if (o.OnChange)
                        o.OnChange(k, v, cur, pe);
                }
            },
        };
        if (o.OnReady)
            o.OnReady(pe, cur, ctl);
        //  Errors: { key: message } under the fields, a text as a toast; true when there are none
        const show = err => {
            if (!err || (typeof err === "object" && !Object.keys(err).length))
                return true;
            if (typeof err === "string") {
                Sw.Toast(err, { Kind: "error" });
                return false;
            }
            for (const [k, m] of Object.entries(err))
                pe.SetError(k, m);
            return false;
        };
        const check = () => {
            //  a text field with the focus commits on blur: commit it first
            const a = document.activeElement;
            if (a && content.contains(a) && typeof a.blur === "function")
                a.blur();
            for (const d of defs || [])
                pe.SetError(d.Key, null);
            return show(o.Validate ? o.Validate(cur) : null);
        };
        const ok = () => {
            if (okOff !== null)
                return false;
            if (!check())
                return false;
            if (!o.OnOk)
                return undefined;
            const r = o.OnOk(cur);
            const after = x => x === false ? false : show(x && typeof x === "object" ? x : null) ? undefined : false;
            return r && typeof r.then === "function" ? r.then(after) : after(r);
        };
        const v = await Dialog.Show({
            Title: title, Id: o.Id || "sw.form", Wide: o.Wide,
            Content: content,
            Buttons: [
                { Text: o.CancelText || _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: null },
                { Text: o.OkText || _TF("OK", "The text of a button on a dialog that when pressed will accept the entered value"), Kind: "primary", Value: "ok", OnClick: ok },
            ],
            //  Enter = OK (with OnOk: a click on OK, so the work runs as from the button)
            EnterValue: () => {
                if (!o.OnOk)
                    return check() ? "ok" : undefined;
                const b = box && box.querySelector(".sw-dialog-foot .sw-button:last-child");
                //  (a disabled OK says why: Sw.Disabled)
                if (b)
                    b.click();
                return undefined;
            },
            OnShow: (c, b) => {
                box = b;
                applyOk();
            },
        });
        return v === "ok" ? cur : null;
    }

    /**
     * Sw.Dialog.Alert (see there).
     * @param {string} title Title.
     * @param {string} [text] Text.
     * @param {string} [okText] The button text (default "Close").
     * @returns {Promise<void>} Resolves when closed.
     */
    async function alertImpl(title, text, okText) {
        await Dialog.Show({
            Title: title, Id: "sw.alert",
            Content: text ? el("div.sw-dialog-text", { text }) : null,
            Buttons: [{ Text: okText || _TF("Close", "The text of the button that closes a message dialog"), Kind: "primary", Value: true }],
            EnterValue: true,
            Role: "alertdialog",
        });
    }

    /**
     * Modal dialogs (promise based).
     */
    const Dialog = {
        /**
         * Open a dialog and return a handle at once (use Show to await the result).
         * @param {object} o Options: { Title, Icon, Content (a node, a text, or a function(body, close, tools) returning a node / promise; tools is the
         * header area for extra buttons), Buttons: [{ Text, Kind, Icon, Title, Value, Close (default true), Dismiss (a way out: a close attempt;
         * default for buttons without OnClick whose Value is null / false), OnClick(close, value) (return false to keep it open; a promise makes the dialog busy) }],
         * Id (a stable id of the dialog kind: its position and size are remembered), NoMove (not movable / resizable),
         * Wide, Full (use the full height), Close (show a close button, default true), Modal (true: an outside click never closes, it gives
         * the rejection cue; default: a clean dialog closes on an outside click), NoEscape (Escape doesn't close, it gives the rejection cue;
         * with Close: false the dialog can only be closed by its buttons or from code), EnterValue (Enter outside a textarea closes with this
         * value, or a function returning it), Focus (an element or a selector to focus), AutoDirty (default true, false for an "alertdialog": fields
         * the user changed away from their opening value make the dialog dirty; values set by code are the opening value), BusyButtons (default
         * true: a button's async OnClick makes the dialog busy), Above (opens above every other pop-up: the session time-out warning, UX-POP-5),
         * Class, AriaLabel (the accessible name of a dialog without a visible Title; a dialog with neither gives a developer warning), Role (default "dialog";
         * "alertdialog" for confirmations: no dirty tracking unless AutoDirty: true), Describe (the accessible description, aria-describedby: an element
         * or a selector inside the body, false for none; default: the whole body of an "alertdialog", else the first ".sw-dialog-text" of the
         * body), Owner (the dialog / pop-up this one belongs to: it closes with it, ex: a question about the dialog's state), OnShow(close, box, body),
         * OnClose(value) }. Button keys: Text, Kind, Icon, Title, Label (accessible name), Value, Close, Dismiss, OnClick(close, value, { Signal, Dialog }),
         * Class, Disabled (true, or the reason as a text: disabled with that reason), WhileBusy (acts while the dialog is busy), Cancellable (true: the
         * work can really be stopped although it sent a request, ex: a read or a server job that honours the abort). Unknown keys give a developer warning.
         * @returns {{Box: HTMLElement, Body: HTMLElement, Tools: HTMLElement, Close: function(*):void, Result: Promise<*>,
         * SetDirty: function(boolean):void, IsDirty: function():boolean, SetBusy: function((Promise|boolean), object=):void, IsBusy: function():boolean,
         * SetProgress: function((number|null), string=):void, Attempt: function(string):Promise<boolean>}} The handle;
         * Result resolves to the value passed to Close (null when dismissed). Close(value) from code always closes (no close attempt).
         */
        Open(o) {
            o = o || {};
            //  Every dialog has a title (Dialog rules): it names the dialog for screen readers and a screenshot of it says what it shows
            if (!o.Title && !o.AriaLabel)
                devWarn("Sw.Dialog: a dialog without a Title (every dialog needs one; it names the dialog for screen readers and screenshots).", "dlg-notitle:" + (new Error().stack || "").split("\n").slice(2, 3).join(""));
            checkOpts("Sw.Dialog.Open", o);
            for (const b of o.Buttons || [])
                if (b)
                    checkOpts("Sw.Dialog button \"" + (b.Text || "") + "\"", b, "Sw.Dialog button");
            //  Every dialog kind remembers its position (UX-POP-7): without an Id a stable one is derived from the page and the dialog
            const dlgId = o.Id || defaultDialogId(o);
            if (o.Id && stack.some(p => p.IsDialog && !p.Closed && p.DlgId === o.Id))
                devWarn("Sw.Dialog: a second dialog with the Id \"" + o.Id + "\" opened on top of the first one (UX-POP-5); focus the open one, or give each dialog kind its own Id.", "dlg-id2:" + o.Id);
            //  A confirmation (alertdialog) holds no user data: never "Discard changes?" for a typed safety check (POP-6); a dialog that
            //  is an alertdialog with real input passes AutoDirty: true
            const autoDirty = o.AutoDirty !== undefined ? o.AutoDirty !== false : o.Role !== "alertdialog";
            let resolve;
            const result = new Promise(r => resolve = r);
            //  A dialog result is waiting for the user, not work (Sw.Button shows no busy state for it, FBK-2)
            result.SwDialog = true;
            const layer = el("div.sw-layer.sw-dim.sw-dialog-layer");
            const titleId = Sw.Uid("sw-dlg");
            const box = el("div.sw-dialog.sw-root", {
                class: o.Class || null, tabIndex: -1,
                attrs: { role: o.Role || "dialog", "aria-modal": "true", "aria-labelledby": o.Title ? titleId : null, "aria-label": !o.Title && o.AriaLabel ? o.AriaLabel : null },
            });
            if (o.Wide)
                box.classList.add("sw-wide");
            if (o.Full)
                box.classList.add("sw-full");
            if (o.Above) {
                layer.classList.add("sw-above");
                box.classList.add("sw-above");
            }
            //  Owner (explicit): a question of a dialog (its "Discard changes?") closes with that dialog, it never stays alone on the page
            const entry = { Box: box, Layer: layer, DimLayer: true, IsDialog: true, Above: !!o.Above, NoEscape: !!o.NoEscape, Closed: false, DlgId: o.Id || null, Opened: Date.now(),
                Owner: o.Owner ? resolveOwner(o.Owner) : null };
            //  The Sw.Button whose OnClick opened this dialog (its click scope runs now): cancelling the dialog's work aborts that
            //  button's work too (Sw.Button.Abort: its AbortSignal and the requests its click started)
            const opener = Sw.Button && Sw.Button.Current && typeof Sw.Button.Current.Abort === "function" ? Sw.Button.Current : null;
            //  The element the focus comes from (the opener): it gets the focus back when the dialog closes and the focus would be lost
            //  (ex: a late result closed it under its own question, FOC-1)
            const openerFocus = document.activeElement && document.activeElement !== document.body ? document.activeElement : null;
            //  Dialog state (UX-POP-6): dirty (unsaved input), busy (work running: explicit SetBusy, or a button's async OnClick)
            const st = { Dirty: false, Manual: false, Other: false, Ex: null, Jobs: [], Busy: false, Start: 0, Asking: false, Tick: 0, Ten: 0, Mile: 0, Rej: { Kind: "", T: 0 }, Ways: "", Hidden: [], OnClose: [], Discarded: false };
            let release = null, geo = null, debugStop = null;
            /** @type {Set<string>} Why the dialog keeps its geometry ("busy": work runs; "status": the status line shows; "stack": a busy footer stacks). */
            const holds = new Set();
            /**
             * Keep the dialog's box where it is while a slot opens or the footer changes (the status line, the busy footer, Stop waiting
             * in Cancel's place): its height is fixed, so the title bar, ⋮, × and the footer buttons stay where the pointer is and the
             * body gives the room (it scrolls; the focused field is kept in view). One rule for centered dialogs, sheets and moved ones
             * (LAY-1, POP-6 "never shifting it"); dialogs the user sized or maximized, or full ones, have a fixed height already.
             * @param {string} why The reason.
             * @param {boolean} on Keep / let go.
             */
            const hold = (why, on) => {
                if (on) {
                    if (!holds.size && !st.Held && !box.style.height && !box.matches(".sw-sized, .sw-max, .sw-full, .sw-sheet-sized, .sw-dragging, .sw-mode")) {
                        const r = box.getBoundingClientRect();
                        if (r.height > 0) {
                            st.Held = true;
                            box.style.height = Math.round(r.height * 100) / 100 + "px";
                            /** @type {number} The body's height when the box was held (useReserve: what the status row / footer took since). */
                            st.BodyH0 = body.clientHeight;
                            //  while held, any size change inside (the status row opening, a taller Stop waiting, an extra row) is fitted
                            //  before it is painted (ResizeObserver runs after layout, before paint): no frame with a moved button
                            if (typeof ResizeObserver === "function") {
                                st.HoldRo = new ResizeObserver(() => {
                                    fitBox();
                                    useReserve();
                                    //  the focused field stays whole in view in every frame (the status row opens with a transition)
                                    scrollFocus();
                                });
                                [head, statusE, body].forEach(x => st.HoldRo.observe(x));
                                if (foot)
                                    st.HoldRo.observe(foot);
                            }
                        }
                    }
                    holds.add(why);
                    //  and in every frame while held: content inside the body may move the field too (a control's locked look, a
                    //  message) without any size change of the parts
                    if (!st.FocusLoop) {
                        const loop = () => {
                            if (!holds.size || entry.Closed) {
                                st.FocusLoop = 0;
                                return;
                            }
                            try {
                                scrollFocus();
                            }
                            catch (x) {
                                console.error(x);
                            }
                            st.FocusLoop = requestAnimationFrame(loop);
                        };
                        st.FocusLoop = requestAnimationFrame(loop);
                    }
                    return;
                }
                holds.delete(why);
                if (!holds.size && st.Held)
                    unhold();
            };
            /** Let the dialog follow its content again. */
            const unhold = () => {
                if (st.HoldRo) {
                    st.HoldRo.disconnect();
                    st.HoldRo = null;
                }
                st.Held = false;
                box.classList.remove("sw-dialog-squeezed");
                body.style.removeProperty("--sw-dlg-used");
                if (box.classList.contains("sw-dialog-line-head"))
                    lineInHead(false);
                box.style.removeProperty("height");
                //  a box that grew at its top keeps its bottom edge (the footer stays where it was all along; the top comes back down)
                if (!st.BottomKept)
                    ["top", "bottom", "margin-bottom"].forEach(p => box.style.removeProperty(p));
                else if (box.classList.contains("sw-placed"))
                    box.style.removeProperty("top");
            };
            //  a window resize lets the dialog fit again (its height follows the content and the viewport)
            const onHoldResize = () => {
                if (!st.Held && st.BottomKept) {
                    st.BottomKept = false;
                    ["top", "bottom", "margin-bottom"].forEach(p => box.style.removeProperty(p));
                }
                if (st.Held) {
                    st.BottomKept = false;
                    unhold();
                    const keep = Array.from(holds);
                    holds.clear();
                    requestAnimationFrame(() => {
                        keep.forEach(w => hold(w, true));
                        //  the busy footer is measured again from the idle one at the new size
                        refreezeFoot();
                        keepFocusInView();
                    });
                }
            };
            window.addEventListener("resize", onHoldResize);
            const close = v => {
                if (entry.Closed)
                    return;
                entry.Closed = true;
                clearBusyTimers();
                clearTimeout(st.UnholdT);
                window.removeEventListener("resize", onHoldResize);
                geo && geo.Stop();
                dirtyObserver && dirtyObserver.disconnect();
                for (const r of st.Ros || [])
                    r.disconnect();
                //  unstack first: the toasts and the live region move out of the box, the background is usable again (focus can return)
                unstack(entry);
                if (release)
                    release();
                //  never the body: when the focus was in the dialog (or its question) and nothing took it back, the opener does
                setTimeout(() => {
                    const a = document.activeElement;
                    if ((!a || a === document.body || !a.isConnected || (a.closest && a.closest(".sw-leaving"))) && openerFocus && openerFocus.isConnected && !stack.some(p => p.IsDialog && !p.Closed))
                        try {
                            if (!(typeof Focus.Restore === "function" && Focus.Restore(openerFocus)))
                                Focus.Set(openerFocus);
                        }
                        catch {
                        }
                }, 0);
                dirtyPage(false);
                busyPage(false);
                debugStop && debugStop();
                //  exit transition (UX-MOT-1): the scrim fades, the dialog fades / the sheet slides out; inert at once
                Motion.Out(layer);
                Motion.Out(box);
                frameModal(-1);
                notifyDialogs();
                //  Everything the dialog owns is disposed with it (drafts, listeners: handle.OnClose / Own); Discarded says why
                const info = { Value: v === undefined ? null : v, Discarded: st.Discarded };
                for (const f of st.OnClose.splice(0))
                    try {
                        f(info);
                    }
                    catch (x) {
                        console.error(x);
                    }
                o.OnClose && o.OnClose(v === undefined ? null : v);
                resolve(v === undefined ? null : v);
            };
            entry.Close = () => close(null);
            const tools = el("div.sw-dialog-tools");
            const head = el("div.sw-dialog-head", null,
                o.Icon ? Icon(o.Icon, "sw-dialog-icon") : null,
                el("h2.sw-dialog-title", { id: titleId, text: o.Title || "" }),
                tools);
            const movable = !o.NoMove;
            let menuBtn = null;
            if (movable) {
                menuBtn = new Sw.Button({
                    Icon: "moreV", Kind: "ghost", Class: "sw-dialog-menu", Label: _TF("Dialog options", "Accessible name of the button that opens the menu of a dialog (move, size, maximize, reset position)"),
                    Title: _TF("Move, resize or maximize this dialog", "Tool tip of the button that opens the menu of a dialog"),
                    OnClick: () => {
                        geo && geo.Menu(menuBtn.E);
                    },
                });
                menuBtn.E.setAttribute("aria-haspopup", "menu");
                head.append(menuBtn.E);
            }
            const closeTitle = _TF("Close", "Tool tip of the button that closes a dialog");
            const closeBgTitle = _TF("Close; the work goes on", "Tool tip of the close button of a dialog whose work goes on in the background when it is closed");
            let closeBtn = null;
            if (o.Close !== false) {
                closeBtn = new Sw.Button({ Icon: "close", Title: closeTitle, Kind: "ghost", Class: "sw-dialog-close", OnClick: () => {
                    //  a close attempt, not work: nothing to announce when the dialog stays (the rejection cue speaks)
                    attempt("x");
                } });
                head.append(closeBtn.E);
            }
            if (!o.Title && !o.Icon)
                head.classList.add("sw-no-title");
            const body = el("div.sw-dialog-body");
            //  The status slot (POP-6: "a line after the title"): a row between the title bar and the body, 0 high while empty, revealed
            //  in place for the rejection line; it never covers the title, ⋮, ×, a field, the focus or a button at any size or zoom (the
            //  footer doesn't move while the line shows: see hold); selectable, part of the description
            const rejectE = el("div.sw-dialog-reject.sw-selectable", { id: Sw.Uid("sw-dlg-rej"), hidden: true });
            const statusE = el("div.sw-dialog-status", null, el("div.sw-dialog-status-in", null, rejectE));
            box.append(head, statusE, body);
            let foot = null;
            const ctl = {
                Close: close,
                IsClosed: () => entry.Closed,
                Attempt: (kind, def) => attempt(kind, def),
                Reject: kind => reject(kind),
                /**
                 * Busy means locked: while the dialog is busy a footer button acts only when it is a declared way out (a Cancel that can
                 * stop the work, a plain Close: both are close attempts) or opted in with WhileBusy: true.
                 * @param {object} b The button definition.
                 * @returns {boolean} True when the button must not act now.
                 */
                Locked: b => st.Busy && !b.WhileBusy && !isDismiss(b),
                /**
                 * A button's async OnClick: the dialog is busy until it settles.
                 * @param {Promise} p The work.
                 * @param {object} info { Def, Text (the button label), Abort() (aborts the AbortSignal given to OnClick), Cancellable() }.
                 * @returns {Promise} The work.
                 */
                Track: (p, info) => {
                    if (o.BusyButtons === false)
                        return p;
                    const job = Object.assign({ P: p }, info || {});
                    st.Jobs.push(job);
                    syncBusy();
                    //  Sw.Button sets up its abort controller when its OnClick returned (still this task): look at the ways out again
                    queueMicrotask(syncBusy);
                    const done = () => {
                        const i = st.Jobs.indexOf(job);
                        if (i >= 0) {
                            st.Jobs.splice(i, 1);
                            syncBusy();
                        }
                    };
                    p.then(done, done);
                    return p;
                },
                /** @returns {Map<Element, *>} The values of the fields and controls now (what a footer button's work reads). */
                Snapshot: () => snapshot(),
                /** Close with a value after a button's work, unless the user changed the dialog since the work started (kept, dirty). */
                CloseAfter: (v, job) => closeAfter(v, job),
                /** The work of a button the user stopped waiting for ended with v (FBK-5: said, never silent). */
                Late: (job, v, after) => late(job, v, after),
                /** A job's requests changed (a write went out): the ways out may change (Cancel → Stop waiting). */
                Changed: () => {
                    st.Ways = "";
                    syncBusy();
                },
                Handle: null,
            };
            if (o.Buttons && o.Buttons.length) {
                foot = dialogButtons(o.Buttons, ctl);
                box.append(foot);
            }
            else if (o.Reserve) {
                //  Working: reserved room for the buttons a busy dialog adds (Cancel, the 10 s Close)
                foot = el("div.sw-dialog-foot.sw-reserve");
                box.append(foot);
            }
            box.addEventListener("keydown", e => {
                if (e.key === "Enter" && o.EnterValue !== undefined && e.target.tagName !== "TEXTAREA" && e.target.tagName !== "BUTTON" && Sw.IsPureEvent(e) && !e.defaultPrevented) {
                    e.preventDefault();
                    if (st.Busy) {
                        reject("enter");
                        return;
                    }
                    const v = typeof o.EnterValue === "function" ? o.EnterValue() : o.EnterValue;
                    //  A function returning undefined keeps the dialog open (ex: a failed validation)
                    if (v !== undefined)
                        close(v);
                }
                //  F6 (UX-FBK-8): between the dialog and its toasts
                else if (e.key === "F6" && !e.ctrlKey && !e.altKey && !e.metaKey && toastHost && box.contains(toastHost)) {
                    const ts = Array.from(toastHost.querySelectorAll(".sw-toast:not(.sw-hide)"));
                    if (!ts.length)
                        return;
                    e.preventDefault();
                    if (toastHost.contains(document.activeElement)) {
                        //  back to where the user was (never the body behind the modal, FOC-1)
                        const back = st.F6Back && st.F6Back.isConnected && box.contains(st.F6Back) && !toastHost.contains(st.F6Back) ? st.F6Back
                            : (Focus.Tabbables(box).find(x => !toastHost.contains(x)) || box);
                        if (!Focus.Set(back) || !box.contains(document.activeElement) || toastHost.contains(document.activeElement))
                            box.focus({ preventScroll: true });
                    }
                    else {
                        st.F6Back = document.activeElement;
                        Focus.Set(ts[ts.length - 1]);
                    }
                }
            });
            //  Escape in a field of the dialog is a close attempt first (UX-POP-6, POP-3): a dirty dialog asks "Discard changes?" before
            //  anything is thrown away (a field's own Escape revert would drop the edit silently, then ask about nothing). An open list
            //  of the field (aria-expanded) and fields marked data-sw-ownescape keep their Escape.
            box.addEventListener("keydown", e => {
                if (e.key !== "Escape" || e.defaultPrevented || !Sw.IsPureEvent(e) || (geo && geo.InMode()) || hintShown)
                    return;
                const t = e.target;
                if (!t || !t.closest || !tracked(t) || t.closest("[data-sw-ownescape]"))
                    return;
                if (t.getAttribute("aria-expanded") === "true" || (t.closest("[aria-expanded='true']") && t.closest("[aria-expanded='true']") !== box))
                    return;
                if (stack[stack.length - 1] !== entry)
                    return;
                e.preventDefault();
                e.stopPropagation();
                attempt("escape");
            }, true);
            const explicitModal = o.Modal === true;
            outsidePress(layer, () => attempt("outside"));
            //  No scrolling of the page behind (UX-POP-10)
            layer.addEventListener("wheel", e => e.preventDefault(), { passive: false });
            layer.addEventListener("touchmove", e => e.preventDefault(), { passive: false });
            document.body.append(layer, box);
            //  after its entrance the box has no animation of its own (the end of a rejection pulse must not replay the entrance)
            box.addEventListener("animationend", e => {
                if (e.target === box && !box.classList.contains("sw-reject-pulse"))
                    box.classList.add("sw-shown");
            });
            setTimeout(() => box.classList.add("sw-shown"), 900);
            //  A tap on the app shell's scrim (the page is framed) acts like a tap on this scrim
            entry.Outside = () => attempt("outside");
            entry.Attempt = kind => attempt(kind);
            entry.Unload = () => st.Dirty || (st.Busy && !(st.Ex && st.Ex.O.StopsWithPage === false));
            frameModal(1);
            pushStack(entry);
            notifyDialogs();
            release = Focus.Trap(box);
            if (movable)
                geo = geometry(box, head, body, dlgId, k => attempt(k));
            else
                box.classList.add("sw-nomove");

            // ---------------------------------------------------------- dirty (glossary: a value differs from the opening value)
            //  Dirty = user input only: a field's base is taken right before the user's first input on it (a press, a key, text input,
            //  paste, drop), so a value a page sets from code before that (an async fill after the open) is the opening value; a change
            //  without user input on that control (an untrusted input event, a Set() from code, a state change) moves the base too.
            /** @type {Map<Element, *>} The base of each field the user touched (its value right before the first input). */
            const initial = new Map();
            /** @type {Map<Element, *>} The value of each field when the dialog opened (Recheck: a restored draft counts as an edit). */
            const openVal = new Map();
            /** @type {Map<Element, *>} Fields / state elements whose base code moved: the base before (Recheck takes it back). */
            const codeOld = new Map();
            const codeOldState = new Map();
            const FieldSel = "input, textarea, select, [contenteditable='true'], [contenteditable='']";
            const valueOf = f => f.isContentEditable ? f.textContent
                : (f.type === "checkbox" || f.type === "radio") ? f.checked
                    : (f.tagName === "SELECT" && f.multiple) ? Array.from(f.selectedOptions).map(x => x.value).join("\n") : f.value;
            const tracked = f => f && f.matches && f.matches(FieldSel) && body.contains(f) && !f.closest("[data-sw-nodirty]") &&
                !/^(search|hidden|button|submit|reset|image|file)$/.test(f.type || "") && (!f.readOnly || lockedRO.has(f));
            /** @type {Set<Element>} The fields this dialog made read only while its work runs (they still count as the user's data). */
            const lockedRO = new Set();
            const snap = f => {
                if (!tracked(f) || initial.has(f))
                    return;
                initial.set(f, valueOf(f));
                //  a radio group changes together
                if (f.type === "radio" && f.name)
                    body.querySelectorAll("input[type=radio]").forEach(x => {
                        if (x.name === f.name && !initial.has(x))
                            initial.set(x, x.checked);
                    });
            };
            //  Sw controls that are not native fields are compared by value too (glossary "Dirty": switching back is clean): a control
            //  that exposes itself on its element (E.SwControl with Get()) by its value, else the state of switch / radio / slider
            //  elements (aria-checked / aria-valuenow) and the label of a select
            const StateSel = "[role=switch], [role=radio], [role=checkbox], [role=slider], [role=spinbutton]";
            const NoState = "[data-sw-nodirty], [role=tablist], [role=tab], [role=tree], [role=listbox], details, .sw-dialog-reject, .sw-dialog-foot";
            /** @type {Map<Element, string>} The opening state of each stateful element / control. */
            const initialState = new Map();
            const ctlValue = c => {
                try {
                    const v = c.Get();
                    return typeof v === "object" ? JSON.stringify(v) : String(v);
                }
                catch {
                    return "";
                }
            };
            const stateOf = e => e.SwControl && typeof e.SwControl.Get === "function" ? "v:" + ctlValue(e.SwControl)
                : e.matches(StateSel) ? "s:" + (e.getAttribute("aria-checked") ?? "") + "|" + (e.getAttribute("aria-valuenow") ?? "")
                    : "t:" + e.textContent;
            const stateful = e => e && e.nodeType === 1 && body.contains(e) && !e.closest(NoState) && (!!e.SwControl || e.matches(StateSel) || e.matches(".sw-select-label"));
            /**
             * Take the bases of the state elements (switches, radios, sliders, select labels: a later change from code moves them, see the
             * observer); with all, also of the Sw controls (E.SwControl, compared by value; else their base is taken on the first input).
             * @param {boolean} [all] Include the Sw controls.
             * @param {Map<Element, string>} [into] The map (default the bases).
             */
            const snapStates = (all, into) => {
                const m = into || initialState;
                body.querySelectorAll(StateSel + ", .sw-select-label").forEach(e => {
                    if (stateful(e) && !m.has(e))
                        m.set(e, stateOf(e));
                });
                if (all)
                    body.querySelectorAll("*").forEach(e => {
                        if (e.SwControl && stateful(e) && !m.has(e))
                            m.set(e, stateOf(e));
                    });
            };
            /** @type {Map<Element, string>} The state of each state element / control when the dialog opened (Recheck). */
            const openState = new Map();
            /**
             * The control an element belongs to (the closest element with E.SwControl inside the body), else the element.
             * @param {Element} e The element.
             * @returns {Element} The control root.
             */
            const ctlRoot = e => {
                for (let x = e; x && x !== body && x.nodeType === 1; x = x.parentElement)
                    if (x.SwControl)
                        return x;
                return e;
            };
            /**
             * True when the user gave input to this control within ms: the last trusted press / key / text input / paste / drop was on it,
             * on its label, or in a pop-up level above this dialog (a select's list, a menu, a picker). A change without that is code.
             * @param {Element} e The field, state element or control.
             * @param {number} ms The window.
             * @returns {boolean} User input.
             */
            const userOn = (e, ms) => {
                const t = lastInputTarget;
                if (!t || !e || t.nodeType !== 1 || Date.now() - lastUserInput > ms)
                    return false;
                const r = ctlRoot(e);
                if (r.contains(t) || e.contains(t))
                    return true;
                const lab = t.closest ? t.closest("label") : null;
                if (lab && lab.control && (r.contains(lab.control) || lab.control === e))
                    return true;
                const i = stack.indexOf(entry);
                for (let k = i + 1; i >= 0 && k < stack.length; ++k)
                    if (stack[k].Box && stack[k].Box.contains(t))
                        return true;
                return false;
            };
            /**
             * True when an element on the event's path is marked data-sw-nodirty (a filter, a type-to-confirm input: not user data).
             * @param {Event} e The event.
             * @returns {boolean} Not tracked.
             */
            const noDirtyPath = e => {
                const p = typeof e.composedPath === "function" ? e.composedPath() : [];
                for (const n of p) {
                    if (n === body)
                        break;
                    if (n && n.nodeType === 1 && n.hasAttribute("data-sw-nodirty"))
                        return true;
                }
                return false;
            };
            /**
             * A value moved by code: the new base (the old one is kept for Recheck).
             * @param {Map} m The base map. @param {Map} old The kept old bases. @param {Element} k The element. @param {*} v The new base.
             */
            const moveBase = (m, old, k, v) => {
                if (!m.has(k))
                    return;
                if (!old.has(k))
                    old.set(k, m.get(k));
                m.set(k, v);
            };
            /**
             * @type {Map<Element, boolean>} Sw controls that reported their own state with sw-change (detail.Changed: the value differs
             * from the control's base, its Value or last Set() from code): their verdict wins over the field / state comparison.
             */
            const reported = new Map();
            /** @type {WeakSet<Element>} Controls whose base is older than the dialog's opening values (after Rebase): compared here. */
            const rebased = new WeakSet();
            const byControl = e => {
                for (const k of reported.keys())
                    if (k.contains(e))
                        return true;
                return false;
            };
            const recompute = () => {
                if (st.Manual || !autoDirty || entry.Closed)
                    return;
                let d = st.Other;
                for (const [k, v] of reported)
                    if (!d && v && k.isConnected) {
                        d = true;
                        break;
                    }
                for (const [f, v] of initial)
                    if (!d && f.isConnected && valueOf(f) !== v && !byControl(f)) {
                        d = true;
                        break;
                    }
                if (!d)
                    for (const [e, v] of initialState)
                        if (e.isConnected && stateOf(e) !== v && !byControl(e)) {
                            d = true;
                            break;
                        }
                applyDirty(d);
            };
            const fieldOf = t => t && t.closest ? t.closest(FieldSel) : null;
            //  The base of a field / control is taken right before the user's first input on it (a focus alone is no input)
            ["pointerdown", "keydown", "beforeinput", "paste", "drop"].forEach(n => body.addEventListener(n, e => {
                if (!e.isTrusted)
                    return;
                snap(fieldOf(e.target));
                const r = e.target && e.target.nodeType === 1 ? ctlRoot(e.target) : null;
                if (r && r.SwControl && stateful(r) && !initialState.has(r))
                    initialState.set(r, stateOf(r));
            }, true));
            /**
             * @type {Map<Element, number>} Who changed it (user vs code): the field / control root of every change the user made
             * (a trusted input / change event, a Sw control's sw-change with detail.User) → performance.now() of that change.
             */
            const userEdits = new Map();
            const userEdit = t => {
                if (!t || t.nodeType !== 1)
                    return;
                const now = performance.now();
                userEdits.set(ctlRoot(t), now);
                const f = fieldOf(t);
                if (f)
                    userEdits.set(f, now);
            };
            /**
             * True when the user changed this element (or the field / control it is in or holds) after a time: a change made by code
             * (a work that writes the server's value into a field, a normalised text) never counts (FRM-5: newer edits are the user's).
             * Uses Sw.Edits.UserSince of ui/base.js when it exists, else the dialog's own record of trusted changes.
             * @param {Element} k The field / state element / control. @param {number} t0 The time (performance.now()).
             * @returns {boolean} Changed by the user since t0.
             */
            const userSince = (k, t0) => {
                const E = Sw.Edits;
                if (E && typeof E.UserSince === "function")
                    try {
                        if (E.UserSince(ctlRoot(k), t0) || E.UserSince(k, t0))
                            return true;
                    }
                    catch {
                    }
                for (const [e, t] of userEdits)
                    if (t >= t0 && (e === k || e.contains(k) || k.contains(e)))
                        return true;
                return false;
            };
            ["input", "change"].forEach(n => body.addEventListener(n, e => {
                if (e.isTrusted)
                    userEdit(e.target);
                const f = fieldOf(e.target);
                if (!tracked(f) || noDirtyPath(e))
                    return;
                //  A value set by code (an event a page dispatched, no user input on that field just before; ex: filling the dialog
                //  after a load) is the new opening value, not an edit. A control's own revert inside a key press is the user's.
                if (!e.isTrusted && !userOn(f, 300))
                    moveBase(initial, codeOld, f, valueOf(f));
                recompute();
            }));
            //  A bubbling "sw-change" from a control's element: a Sw control says whether its value differs from its base
            //  (detail.Changed, ui/controls.js); else compared by value when the control exposes it (E.SwControl) or by its state
            //  elements; an unknown component without any of these keeps the dialog dirty until saved. Not user data (never tracked):
            //  data-sw-nodirty on the control, on its input or anywhere on the event path, a control with Dirty: false.
            body.addEventListener("sw-change", e => {
                const t = e.target;
                if (!t || !t.closest || t.closest("[data-sw-nodirty]") || noDirtyPath(e))
                    return;
                const dt = e.detail;
                const sc = dt && dt.Control;
                //  who changed it: the control says so (detail.User), else input on it just before
                if (dt && dt.User === true)
                    userEdit(t);
                else if (!(dt && dt.User === false) && userOn(t, 1500))
                    userEdit(t);
                if ((dt && dt.NoData) || (sc && ((sc.O && sc.O.Dirty === false) || (sc.Input && sc.Input.closest && sc.Input.closest("[data-sw-nodirty]")) || (t.querySelector && t.querySelector("[data-sw-nodirty]"))))) {
                    if (reported.delete(t))
                        recompute();
                    return;
                }
                //  a change without user input on the control (a page set it, a restored draft: Recheck counts that) moves its base
                if ((dt && dt.User === false) || (!(dt && dt.User === true) && !userOn(t, 1500))) {
                    reported.delete(t);
                    if (initialState.has(t))
                        moveBase(initialState, codeOldState, t, stateOf(t));
                    recompute();
                    return;
                }
                if (sc && typeof dt.Changed === "boolean" && !rebased.has(t)) {
                    reported.set(t, dt.Changed);
                    recompute();
                    return;
                }
                const c = t.SwControl ? t : (Array.from(initialState.keys()).find(k => k.contains(t)) || Array.from(initial.keys()).find(k => t.contains(k)));
                if (!c)
                    st.Other = true;
                recompute();
            });
            let dirtyObserver = null;
            if (autoDirty && window.MutationObserver) {
                dirtyObserver = new MutationObserver(recs => {
                    if (st.Manual || entry.Closed)
                        return;
                    let seen = false;
                    for (const r of recs) {
                        const t0 = r.target.nodeType === 1 ? r.target : r.target.parentElement;
                        const t = t0 && t0.closest ? (t0.matches(StateSel) ? t0 : t0.closest(".sw-select-label")) : null;
                        if (!t || !stateful(t))
                            continue;
                        //  the user's change only after input on that control (or its list); anything else is code (a fill after a load)
                        const user = userOn(t, 1500);
                        if (!initialState.has(t)) {
                            //  first change of an element added after the open: its old state from the record
                            if (r.type === "attributes")
                                initialState.set(t, user ? "s:" + (r.attributeName === "aria-checked" ? (r.oldValue ?? "") + "|" + (t.getAttribute("aria-valuenow") ?? "") : (t.getAttribute("aria-checked") ?? "") + "|" + (r.oldValue ?? "")) : stateOf(t));
                            else
                                initialState.set(t, stateOf(t));
                        }
                        else if (!user)
                            //  a change from code (a page filling the dialog) is the new opening state, not unsaved input
                            moveBase(initialState, codeOldState, t, stateOf(t));
                        seen = true;
                    }
                    if (seen)
                        recompute();
                });
                dirtyObserver.observe(body, { subtree: true, attributes: true, attributeOldValue: true, attributeFilter: ["aria-checked", "aria-valuenow"], childList: true, characterData: true });
            }
            /**
             * Set the dirty state (a change ends the rejection line, POP-6).
             * @param {boolean} v Dirty.
             */
            const applyDirty = v => {
                v = !!v;
                if (st.Dirty === v)
                    return;
                st.Dirty = v;
                box.classList.toggle("sw-dirty", v);
                dirtyPage(v);
                hideReject();
            };
            /** @type {{Dirty: boolean, Work: boolean}} What this dialog reported to the page (Sw.Page.SetDirty "dlg:<id>"). */
            const pageRep = { Dirty: false, Work: false };
            /**
             * Report the dialog to the page with one sentence for its whole state (SAF-10; never one reason per part): leaving the page
             * or closing the tab asks while it has unsaved input ("Leave without saving?") or work that stops with the page ("Leave this
             * page?"); the page is clean again once the dialog is (closed, saved, undone, the work ended).
             */
            const pageReport = () => {
                const P = Sw.Page;
                if (!P || typeof P.SetDirty !== "function")
                    return;
                const key = "dlg:" + titleId;
                //  Unsaved input asks only when the page said so (SetDirty on the handle): the original never asked before the page was
                //  left, so the automatic field tracking keeps its "Discard changes?" inside the dialog and adds no "Leave site?" (P3-A-4)
                const dirty = pageRep.Dirty && st.Manual && !entry.Closed, work = pageRep.Work && !entry.Closed;
                if (!dirty && !work) {
                    if (typeof P.ClearDirty === "function")
                        P.ClearDirty(key);
                    else
                        P.SetDirty(key, null);
                    return;
                }
                const what = (st.Ex && st.Ex.O.Title) || (st.Jobs[0] && st.Jobs[0].Text) || o.Title || "";
                let text;
                if (dirty && work)
                    text = o.Title
                        ? _T("Your changes in the dialog \"{0}\" are not saved, and {1} may not finish.", o.Title, what, "Reason to ask before the page is left while a dialog has unsaved input and its work runs. {0}: the dialog title, ex: Edit customer; {1}: what runs, ex: Save")
                        : _TF("Your changes in the open dialog are not saved, and its work may not finish.", "Reason to ask before the page is left while a dialog without a title has unsaved input and work that runs");
                else if (dirty)
                    text = o.Title
                        ? _T("Your changes in the dialog \"{0}\" are not saved.", o.Title, "Reason to ask before the page is left while a dialog has unsaved input. {0}: the dialog title, ex: Edit customer")
                        : _TF("Your changes in the open dialog are not saved.", "Reason to ask before the page is left while a dialog without a title has unsaved input");
                else
                    text = what ? _T("{0} stops.", what, "Reason to ask before the page is left while a dialog's work runs. {0}: what runs, ex: Uploading report.xlsx")
                        : _TF("The running work stops.", "Reason to ask before the page is left while a dialog's work runs");
                //  unsaved input decides the question ("Leave without saving?"); work alone asks "Leave this page?"
                P.SetDirty(key, text, dirty ? undefined : { Work: true });
            };
            /**
             * The dirty part of the page report.
             * @param {boolean} v Dirty.
             */
            const dirtyPage = v => {
                pageRep.Dirty = !!v;
                pageReport();
            };
            /**
             * The work part of the page report (work that stops with the page).
             * @param {boolean} on Running.
             */
            const busyPage = on => {
                on = !!on;
                if (on === pageRep.Work)
                    return;
                pageRep.Work = on;
                pageReport();
            };

            // ---------------------------------------------------------- busy (FBK-2, FBK-4, POP-6)
            //  Fixed slots (LAY-1): the status (what it waits for, the progress, the time so far) is the first item of the one row
            //  footer and shrinks with an ellipsis; buttons a busy dialog adds (Cancel, Continue in background, the 10 s Close) are
            //  inserted left of the existing ones, so the buttons that were there never move.
            const progE = el("div.sw-dialog-progress", { id: Sw.Uid("sw-dlg-prog"), hidden: true });
            const busyBtns = [];
            let stopBtn = null;
            const elapsed = () => elapsedText(Date.now() - st.Start);
            const ensureFoot = () => {
                if (!foot) {
                    foot = el("div.sw-dialog-foot");
                    box.append(foot);
                }
                return foot;
            };
            const clearBusyTimers = () => {
                clearInterval(st.Tick);
                clearTimeout(st.Ten);
                clearInterval(st.Mile);
                st.Tick = st.Ten = st.Mile = 0;
            };

            // ---------------------------------------------------------- input while busy (FRM-5: never lose input; POP-6)
            //  While work runs the fields are read only (not inert: the focus, the caret, selection and copy stay; FRM-8 look), so
            //  nothing typed during a save can be lost. EditWhileBusy: true keeps them editable: then edits made after the work started
            //  keep the dialog open and dirty when the work ends (closeAfter). Both: a close never drops an edit the work didn't send.
            const LockSel = "button, input, select, textarea, label, [contenteditable='true'], [contenteditable=''], [role=switch], [role=checkbox], [role=radio], [role=slider], [role=spinbutton], [role=option], [role=combobox], [role=listbox], [role=menuitemcheckbox], [role=menuitemradio]";
            const locking = () => st.Locked && !entry.Closed;
            /** A blocked edit: the cue once a second, the line says why (the focus stays). */
            const blocked = e => {
                e.preventDefault();
                e.stopImmediatePropagation();
                const now = Date.now();
                if (now - (st.EditRej || 0) > 1000) {
                    st.EditRej = now;
                    reject("busy-edit");
                }
            };
            const isTextField = t => t && t.matches && (t.matches("textarea, [contenteditable='true'], [contenteditable='']") ||
                (t.matches("input") && !/^(checkbox|radio|range|button|submit|reset|image|file|color|hidden)$/.test(t.type || "")));
            ["beforeinput", "paste", "drop", "cut"].forEach(n => body.addEventListener(n, e => {
                if (locking() && e.target && e.target.closest && e.target.closest(LockSel))
                    blocked(e);
            }, true));
            //  pointer presses on controls (switches, check boxes, selects, buttons in the body); a press on a text field keeps working
            //  (focus, caret, selection)
            ["pointerdown", "mousedown", "click", "change", "dblclick"].forEach(n => body.addEventListener(n, e => {
                if (!locking() || !e.target || !e.target.closest)
                    return;
                const c = e.target.closest(LockSel);
                if (!c || c.closest("a[href], summary, [role=tab], [data-sw-whilebusy]") || isTextField(c) || (c.tagName === "LABEL" && isTextField(c.control)))
                    return;
                if (n === "change") {
                    e.stopImmediatePropagation();
                    return;
                }
                blocked(e);
            }, true));
            /** @type {Array<object>} The Sw controls this dialog locked while busy (unlocked after). */
            const lockedCtls = [];
            //  a control refused a change while locked (ui/controls.js "sw-locked"): the dialog's line says why (no second cue)
            body.addEventListener("sw-locked", e => {
                if (!locking())
                    return;
                e.preventDefault();
                const now = Date.now();
                if (now - (st.EditRej || 0) > 1000) {
                    st.EditRej = now;
                    reject("busy-edit");
                }
            });
            //  fields that change their value with arrows / the wheel themselves (number boxes, spin buttons, sliders): blocked too
            const StepSel = ".sw-number, [role=spinbutton], input[type=number], [role=slider], input[type=range]";
            body.addEventListener("wheel", e => {
                if (locking() && e.target && e.target.closest && e.target.closest(StepSel) && !e.target.closest("[data-sw-whilebusy]"))
                    blocked(e);
            }, { capture: true, passive: false });
            //  keys that change a control (text fields are read only already: their keys only move the caret)
            body.addEventListener("keydown", e => {
                if (!locking() || !e.target || !e.target.closest || e.ctrlKey || e.metaKey || e.altKey)
                    return;
                if (e.target.closest(StepSel) && !e.target.closest("[data-sw-whilebusy]") && /^(ArrowUp|ArrowDown|PageUp|PageDown|Up|Down)$/.test(e.key)) {
                    blocked(e);
                    return;
                }
                const c = e.target.closest(LockSel);
                if (!c || isTextField(c) || c.closest("a[href], summary, [role=tab], [data-sw-whilebusy]"))
                    return;
                if (/^( |Enter|Spacebar|ArrowUp|ArrowDown|ArrowLeft|ArrowRight|Home|End|PageUp|PageDown|Delete|Backspace)$/.test(e.key) || e.key.length === 1)
                    blocked(e);
            }, true);
            /**
             * Lock / unlock the fields for busy work.
             * @param {boolean} on Lock.
             */
            const lockFields = on => {
                on = !!on && o.EditWhileBusy !== true;
                if (on === !!st.Locked)
                    return;
                st.Locked = on;
                box.classList.toggle("sw-dialog-locked", on);
                if (on) {
                    body.querySelectorAll("input, textarea").forEach(f => {
                        if (!f.readOnly && !f.disabled && isTextField(f) && !f.closest("[data-sw-whilebusy]")) {
                            f.readOnly = true;
                            lockedRO.add(f);
                        }
                    });
                    //  Lock by capability, not by tag: every Sw control in the body locks itself (Locked(true): no value change by key,
                    //  wheel, pointer or paste; its arrows, steppers and pop-ups included), so read only is really read only (FRM-8)
                    const seen = new Set();
                    body.querySelectorAll("*").forEach(e => {
                        const c = e.SwControl;
                        if (!c || seen.has(c) || e.closest("[data-sw-whilebusy]"))
                            return;
                        seen.add(c);
                        if (typeof c.Locked !== "function") {
                            devWarn("Sw.Dialog \"" + (o.Title || dlgId) + "\": a control in the body (" + (c.constructor && c.constructor.name || "SwControl") + ") has no Locked(bool): it can't be locked while the dialog's work runs (UX FRM-5 / FRM-8).", "dlg-nolock:" + (c.constructor && c.constructor.name));
                            return;
                        }
                        const was = typeof c.IsLocked === "function" ? c.IsLocked() : c.IsLocked;
                        if (was === true)
                            return;
                        try {
                            c.Locked(true);
                            lockedCtls.push(c);
                        }
                        catch (x) {
                            console.error(x);
                        }
                    });
                }
                else {
                    for (const f of lockedRO)
                        f.readOnly = false;
                    lockedRO.clear();
                    for (const c of lockedCtls.splice(0))
                        try {
                            c.Locked(false);
                        }
                        catch (x) {
                            console.error(x);
                        }
                }
            };
            /** @returns {Map<Element, *>} The values of the fields and controls now. */
            const snapshot = () => {
                const m = new Map();
                body.querySelectorAll(FieldSel).forEach(f => {
                    if (tracked(f))
                        m.set(f, valueOf(f));
                });
                body.querySelectorAll(StateSel + ", .sw-select-label").forEach(e => {
                    if (stateful(e))
                        m.set(e, stateOf(e));
                });
                body.querySelectorAll("*").forEach(e => {
                    if (e.SwControl && stateful(e))
                        m.set(e, stateOf(e));
                });
                return m;
            };
            /** @returns {*} The value of a snapshot key now. @param {Element} k The key. */
            const nowOf = k => k.matches(FieldSel) ? valueOf(k) : stateOf(k);
            /**
             * @param {Map<Element, *>} snap A snapshot.
             * @param {number} [t0] The start of the work (performance.now()): only the user's changes since then count; a value the
             * work wrote itself (the server's value, a normalised text) is no newer edit.
             * @returns {boolean} True when a field or control the user changed differs from it now (an edit the work didn't see).
             */
            const newerThan = (snap, t0) => {
                for (const [k, v] of snap)
                    if (k.isConnected && nowOf(k) !== v && (t0 === undefined || userSince(k, t0)))
                        return true;
                return false;
            };
            /**
             * The work ended but the dialog holds edits made after it started: keep it open, dirty against what the work sent, and say so.
             * @param {object} job The job.
             * @param {string} msg The line.
             */
            const keepNewer = (job, msg) => {
                if (!st.Manual) {
                    for (const k of reported.keys())
                        rebased.add(k);
                    reported.clear();
                    initial.clear();
                    initialState.clear();
                    for (const [k, v0] of job.Snap) {
                        //  a value the work wrote itself is the saved one (only the user's newer edits stay dirty)
                        const v = k.isConnected && nowOf(k) !== v0 && job.T0 !== undefined && !userSince(k, job.T0) ? nowOf(k) : v0;
                        if (k.matches(FieldSel))
                            initial.set(k, v);
                        else
                            initialState.set(k, v);
                    }
                    st.Other = false;
                    recompute();
                }
                else
                    applyDirty(true);
                notice(msg);
            };
            /**
             * Close with a value after a button's work, unless the dialog holds edits the work didn't send (then it stays, dirty).
             * @param {*} v The value.
             * @param {object|null} job The job (null: close).
             */
            const closeAfter = (v, job) => {
                if (entry.Closed)
                    return;
                if (job && job.Snap && newerThan(job.Snap, job.T0)) {
                    keepNewer(job, _T("{0} is done; your newer changes are not saved yet", job.Text, "Line of a dialog after the work of a button ended while the user had changed the dialog since it started: the dialog stays open. {0}: the button label, ex: Save"));
                    return;
                }
                close(v);
            };
            /**
             * The work the user stopped waiting for ended with v: said once (a toast), and the dialog follows it.
             * @param {object} job The job.
             * @param {*} v The result.
             * @param {function(*, object):void} after The button's after-work step (closes with its value).
             */
            const late = (job, v, after) => {
                if (v === false)
                    return;
                const what = job.Text;
                if (entry.Closed) {
                    Sw.Info(_T("{0} finished after all", what, "Toast when the work of a dialog button that the user stopped waiting for has finished after the dialog closed. {0}: the button label, ex: Save"));
                    return;
                }
                if (job.Snap && newerThan(job.Snap, job.T0)) {
                    keepNewer(job, _T("{0} finished after all; your newer changes are not saved yet", what, "Line of a dialog when the work the user stopped waiting for finished while the dialog holds newer changes. {0}: the button label, ex: Save"));
                    return;
                }
                const asking = st.Asking;
                after(v, null);
                Sw.Info(asking
                    ? _T("{0} finished after all: your changes were saved and the dialog is closed", what, "Toast when the work of a dialog button that the user stopped waiting for finished while the dialog asked Discard changes?: both close. {0}: the button label, ex: Save")
                    : _T("{0} finished after all", what, "Toast when the work of a dialog button that the user stopped waiting for has finished. {0}: the button label, ex: Save"));
            };
            /**
             * True when all the running work can really be stopped (honest Cancel, POP-6): SetBusy with Cancel / Abort (the caller says
             * so), and every footer button's work is cancellable (it reads the signal and sent no request, or Cancellable: true). Work
             * that sent a request (it may be done on the server already) or ignores the signal can't be cancelled: see canStopWaiting.
             * @returns {boolean} Cancellable.
             */
            const busyCancellable = () => (!!st.Ex || st.Jobs.length > 0) && (!st.Ex || !!(st.Ex.O.Cancel || st.Ex.O.Abort)) &&
                st.Jobs.every(j => j.Cancellable && j.Cancellable());
            /**
             * True when the work can't be cancelled but the user may stop waiting for it ("Stop waiting (Save may still finish)"):
             * a footer button's work, or SetBusy in the click of a Sw.Button (its requests are sent).
             * @returns {boolean} Stop waiting is the way out.
             */
            const canStopWaiting = () => !busyCancellable() && (st.Jobs.length > 0 || !!(st.Ex && st.Ex.O.Opener && st.Ex.O.Opener.CanAbort && st.Ex.O.Opener.CanAbort()));
            /** @returns {string} What runs (for the labels and announcements), ex: "Save". */
            const busyWhat = () => (st.Jobs[0] && st.Jobs[0].Text) || (st.Ex && st.Ex.O.Title) || o.Title || "";
            /** @returns {string} The label of the way out that stops waiting (it names the consequence, POP-6). */
            const stopWaitLabel = () => {
                const w = busyWhat();
                return w ? _T("Stop waiting ({0} may still finish)", w, "Button of a busy dialog whose work can't be cancelled (a request was sent): closes it without waiting; the work may still be done. {0}: what runs, ex: Save")
                    : _TF("Stop waiting (the work may still finish)", "Button of a busy dialog whose work can't be cancelled: closes it without waiting; the work may still be done");
            };
            /** @returns {string} The short label of that way out (when the full one has no room in Cancel's slot). */
            const shortStopLabel = () => _TF("Stop waiting", "Short label of the Stop waiting button of a busy dialog when the full label (Stop waiting (Save may still finish)) has no room; the status next to it says that the work may still finish");
            /** @returns {string} The tool tip of that way out. */
            const stopWaitTitle = () => _TF("Close this dialog without waiting; the work can't be undone from here and may still be done by the service", "Tool tip of the Stop waiting button of a busy dialog");
            /** @returns {HTMLElement[]} Every footer button in visual order (the extra row above the button row first). */
            const footBtns = () => foot ? Array.from(foot.querySelectorAll(":scope > .sw-dialog-foot-extra > .sw-button, :scope > .sw-button")) : [];
            /** @returns {boolean} True for a footer button that is not offered now (its slot kept, or replaced by Stop waiting). */
            const isOff = b => b.hidden || b.classList.contains("sw-dialog-slot-off") || b.classList.contains("sw-dialog-slot-gone");
            /** The footer Cancel buttons (ways out that stop the work), as opposed to a plain Close (Dismiss "close"). */
            const footCancels = () => footBtns().filter(b => b.SwDialogButton && b.SwDialogButton.Dismiss && b.SwDialogButton.Def.Dismiss !== "close");
            /** The visible label of every busy way out (for the rejection line and the 10 s announcement), in footer order. */
            const busyWays = () => {
                const w = [];
                for (const b of footBtns()) {
                    if (isOff(b))
                        continue;
                    if (b.SwLabel)
                        w.push(b.SwLabel);
                    else if (b.SwDialogButton && b.SwDialogButton.Dismiss)
                        w.push(b.SwDialogButton.Text);
                }
                if (st.XWay && st.Busy)
                    w.push(xWayName());
                return w;
            };
            /** @returns {string} The name of the × when it is the way out of a busy dialog in a low window: "× (Stop waiting (Save may still finish))". */
            const xWayName = () => _T("× ({0})", st.XWay ? st.XWay.Label : "", "Names the close button (×) of a busy dialog in a low window, where it is the way out. {0}: what it does, ex: Stop waiting (Save may still finish)");
            /** The element a busy dialog focuses: the first way out (Cancel, Stop waiting, Continue in background, the 10 s Close), else the box. */
            const wayOut = () => footBtns().find(b => !isOff(b) && (b.SwLabel || (b.SwDialogButton && b.SwDialogButton.Dismiss))) || box;

            // ---------------------------------------------------------- the footer layout engine (LAY-1, TXT-17, POP-6)
            //  One engine for every busy state and any number of footer buttons, computed from the idle footer. When work starts the box
            //  keeps its geometry (hold) and every footer button is pinned where it was (absolute, at its idle offset from the footer's
            //  bottom right edge, its idle width): nothing the busy state adds can move it. The footer keeps the idle buttons' room and
            //  grows only upwards (the body gives the room; the room was reserved when the dialog opened, see reserveRoom). "Stop
            //  waiting" takes Cancel's slot (growing left only into free room of its row; the short label "Stop waiting" when the full one
            //  is taller than Cancel there), an added button (Cancel of SetBusy, Continue in background, the 10 s Close) goes left of the
            //  bottom row when it fits, else into a row of its own above the buttons; the status shares the bottom row when it has about
            //  6 em there, else takes a row of its own above (a low window: the time only). Nothing that was on screen moves, nothing
            //  covers the focus or the buttons, Save is never pushed off screen; the end of the work restores the idle footer.
            /** @type {Map<HTMLElement, string>} Footer buttons pinned while busy → their inline style before. */
            const frozen = new Map();
            let extraRow = null, roomE = null;
            /**
             * The idle footer measured when the work started (coordinates in the footer's padding box: L / R from its left edge, B from
             * its bottom edge): { W, PL, PR, PB, Gap, RoomH, Btns: [{ E, L, R, B, W, H }] }.
             */
            let footRef = null;
            /** @returns {object} The bottom row of the idle footer { B, H, L (its leftmost button's left edge) }. */
            const bottomRow = () => {
                const R = footRef;
                const bs = R.Btns.filter(x => x.E.isConnected);
                if (!bs.length)
                    return { B: R.PB, H: R.RoomH, L: R.W - R.PR };
                const b0 = Math.min(...bs.map(x => x.B));
                const row = bs.filter(x => x.B - b0 < 3);
                return { B: b0, H: Math.max(...row.map(x => x.H)), L: Math.min(...row.map(x => x.L)) };
            };
            const freezeFoot = on => {
                if (!foot)
                    return;
                if (on) {
                    if (footRef)
                        return;
                    const fr = foot.getBoundingClientRect();
                    const sl = foot.scrollLeft;
                    if (!fr.width)
                        return;
                    const cs = getComputedStyle(foot);
                    const n = p => parseFloat(cs[p]) || 0;
                    const bl = n("borderLeftWidth"), bt = n("borderTopWidth");
                    //  The padding box (where absolute children are placed): without the scroll bars. A footer that scrolls sideways in a
                    //  low window has a horizontal bar under it with classic (visible) scroll bars; measured from the border box, the pinned
                    //  buttons moved up by the bar's height (10 px at 320 x 180)
                    const W = foot.clientWidth;
                    const H = foot.clientHeight;
                    const padBottom = fr.top + bt + H;
                    const btns = [];
                    for (const b of footBtns()) {
                        if (b.classList.contains("sw-dialog-busybtn"))
                            continue;
                        const r = b.getBoundingClientRect();
                        if (!r.width)
                            continue;
                        //  (content coordinates: a footer that scrolls sideways in a low window keeps its scroll position)
                        btns.push({ E: b, L: r.left - fr.left - bl + foot.scrollLeft, R: r.right - fr.left - bl + foot.scrollLeft, B: padBottom - r.bottom, W: r.width, H: r.height });
                    }
                    footRef = { W, PL: n("paddingLeft"), PR: n("paddingRight"), PB: n("paddingBottom"), Gap: n("columnGap") || 8, RoomH: Math.max(0, H - n("paddingTop") - n("paddingBottom")), Btns: btns };
                    roomE = el("div.sw-dialog-foot-room", { attrs: { "aria-hidden": "true" } });
                    roomE.style.height = footRef.RoomH + "px";
                    foot.classList.add("sw-foot-frozen");
                    for (const x of btns) {
                        frozen.set(x.E, x.E.style.cssText);
                        Object.assign(x.E.style, { position: "absolute", right: (W - x.R) + "px", bottom: x.B + "px", width: x.W + "px", minHeight: x.H + "px", flex: "none", margin: "0" });
                    }
                    foot.append(roomE);
                    //  (the footer never scrolls sideways (footWrap); a scroll position it had is kept)
                    foot.scrollLeft = sl;
                    return;
                }
                for (const [b, css] of frozen)
                    b.style.cssText = css;
                frozen.clear();
                footRef = null;
                foot.classList.remove("sw-foot-frozen", "sw-foot-stack", "sw-foot-tight");
                if (roomE) {
                    /* SwWeb:allow ux-mot-1 (a spacer of the busy footer, not an overlay) */
                    roomE.remove();
                    roomE = null;
                }
                if (extraRow) {
                    /* SwWeb:allow ux-mot-1 (a footer row of the busy state, not an overlay: it leaves with the state change) */
                    extraRow.remove();
                    extraRow = null;
                }
                progE.classList.remove("sw-dialog-progress-inline", "sw-dialog-progress-off");
                progE.style.cssText = "";
            };
            /** @returns {HTMLElement[]} The buttons the busy state added (Stop waiting, Cancel, Continue in background, the 10 s Close). */
            const busyItems = () => busyBtns.concat(stopBtn ? [stopBtn] : []).filter(b => b && b.isConnected);
            /** Put a busy button into the row of its own above the footer buttons (right aligned, in the flow: the footer grows up). */
            const toExtraRow = b => {
                if (!extraRow) {
                    extraRow = el("div.sw-dialog-foot-extra");
                    foot.append(extraRow);
                }
                b.classList.remove("sw-dialog-busyabs");
                ["position", "right", "bottom", "min-width", "max-width"].forEach(p => b.style.removeProperty(p));
                extraRow.append(b);
            };
            /**
             * Set the label of a busy button: the full one, the short "Stop waiting", or (no room for a word of it) the icon only; the
             * full text stays its accessible name and tool tip. A label never wraps (one line, never broken inside a word: TXT-17).
             * @param {HTMLElement} b The button. @param {boolean|string} short true / "short": the short label; "icon": the icon only.
             */
            const busyLabel = (b, short) => {
                const full = b.SwLabel;
                const icon = short === "icon";
                const text = icon ? "" : short ? shortStopLabel() : full;
                const sb = b.SwSb;
                if (sb && typeof sb.SetContent === "function")
                    sb.SetContent(text, icon ? "logout" : null);
                else if (sb && typeof sb.SetText === "function")
                    sb.SetText(text);
                else {
                    const t = b.querySelector(".sw-button-text");
                    if (t)
                        t.textContent = text;
                }
                b.classList.toggle("sw-dialog-busy-icon", icon);
                if (short) {
                    b.setAttribute("aria-label", full);
                    b.title = full;
                    st.StopShort = true;
                }
                else {
                    b.removeAttribute("aria-label");
                    b.title = b.SwTitle || "";
                }
            };
            /** Place the busy buttons (see the engine above). */
            const placeItems = () => {
                const items = busyItems();
                if (!foot || !footRef) {
                    //  no idle footer to keep (a dialog without buttons): the busy buttons flow in the footer
                    for (const b of items) {
                        b.classList.remove("sw-dialog-busyabs");
                        b.style.position = "";
                        if (b.parentNode !== foot && foot)
                            foot.append(b);
                    }
                    return;
                }
                const R = footRef;
                const em = parseFloat(getComputedStyle(foot).fontSize) || 14;
                const row = bottomRow();
                let left = row.L;
                for (const b of items) {
                    if (b.parentNode !== foot)
                        foot.append(b);
                    b.classList.add("sw-dialog-busyabs");
                    Object.assign(b.style, { position: "absolute", minWidth: "", maxWidth: "", width: "" });
                    const at = b.SwAt ? R.Btns.find(x => x.E === b.SwAt) : null;
                    if (at) {
                        //  Stop waiting in Cancel's slot: its right and bottom edges, at least its width, growing left into free room
                        //  only. Its label is MEASURED on one line (it never wraps, never breaks inside a word, TXT-17), in this order:
                        //  the full label beside the status, the short "Stop waiting" beside the status, the short label with the status
                        //  giving up its room (it moves to its own row / the time only), the icon only (the full text is its name and
                        //  tool tip). The slot was sized for the short label when the dialog opened (slotLabels), so the icon is rare.
                        const before = R.Btns.filter(x => x !== at && Math.abs(x.B - at.B) < 3 && x.R <= at.L + 1);
                        const lb = before.length ? Math.max(...before.map(x => x.R)) + R.Gap : R.PL;
                        const statusHere = !isSheetLayout() && !before.length && Math.abs(at.B - row.B) < 3;
                        const all = Math.max(at.W, at.R - lb);
                        const withStatus = Math.max(at.W, at.R - lb - (statusHere ? em * (window.innerHeight > 500 ? 7 : 3) + R.Gap : 0));
                        Object.assign(b.style, { right: (R.W - at.R) + "px", bottom: at.B + "px", minWidth: at.W + "px", maxWidth: "none" });
                        //  the width the label needs on one line
                        const need = mode => {
                            busyLabel(b, mode);
                            return b.getBoundingClientRect().width;
                        };
                        const ways = [[false, withStatus], ["short", withStatus], ["short", all], ["icon", all]];
                        let pick = ways[ways.length - 1];
                        for (const w of ways)
                            if (need(w[0]) <= w[1] + 0.5) {
                                pick = w;
                                break;
                            }
                        busyLabel(b, pick[0]);
                        b.style.maxWidth = Math.floor(pick[1]) + "px";
                        const r = b.getBoundingClientRect();
                        //  taller than Cancel with a button above it: a row of its own (never over another button)
                        const over = R.Btns.some(x => x !== at && x.B > at.B + 1 && x.L < at.R && x.R > at.R - r.width && at.B + r.height > x.B - 1);
                        if (over)
                            extraRowLabel(b);
                        else if (Math.abs(at.B - row.B) < 3)
                            left = Math.min(left, at.R - r.width);
                        continue;
                    }
                    //  an added button: left of the bottom row when its label fits there on one line (the full one, for Stop waiting
                    //  the short one next), else a row of its own above the buttons
                    const room = left - R.Gap - R.PL;
                    Object.assign(b.style, { right: (R.W - (left - R.Gap)) + "px", bottom: row.B + "px", maxWidth: "none" });
                    const modes = b.SwLabel === stopWaitLabel() ? [false, "short"] : [false];
                    let mode = null, r = null;
                    for (const m of modes) {
                        busyLabel(b, m);
                        r = b.getBoundingClientRect();
                        if (room >= em * 3 && r.width <= room + 0.5 && r.height <= row.H * 1.35) {
                            mode = m;
                            break;
                        }
                    }
                    if (mode === null) {
                        extraRowLabel(b);
                        continue;
                    }
                    b.style.maxWidth = Math.max(0, Math.floor(room)) + "px";
                    b.style.bottom = (row.B + Math.max(0, (row.H - r.height) / 2)) + "px";
                    left -= R.Gap + r.width;
                }
                st.FootLeft = left;
            };
            /**
             * A busy button in the row of its own above the buttons: the full label when it fits the row on one line, else the short
             * one (Stop waiting), else the icon only (its full text is the name and tool tip). Never wraps.
             * @param {HTMLElement} b The button.
             */
            const extraRowLabel = b => {
                toExtraRow(b);
                const avail = extraRow.clientWidth || (footRef ? footRef.W - footRef.PL - footRef.PR : 0);
                const modes = b.SwLabel === stopWaitLabel() ? [false, "short", "icon"] : [false];
                for (const m of modes) {
                    busyLabel(b, m);
                    if (!avail || b.getBoundingClientRect().width <= avail + 0.5)
                        return;
                }
            };
            /**
             * The status of a busy footer: in the bottom row when it has about 6 em there (and the whole time); else a row of its own
             * above the buttons (on sheets always), in a low window (400 % zoom, a phone in landscape) the time only in the row when
             * there is room for it. Buttons never shrink or move for it.
             */
            const placeStatus = () => {
                if (!foot || !footRef || progE.hidden || progE.parentNode !== foot)
                    return;
                const R = footRef;
                foot.classList.remove("sw-foot-stack", "sw-foot-tight");
                progE.classList.remove("sw-dialog-progress-inline", "sw-dialog-progress-off");
                progE.style.cssText = "";
                if (isSheetLayout()) {
                    foot.classList.add("sw-foot-stack");
                    return;
                }
                const em = parseFloat(getComputedStyle(progE).fontSize) || 14;
                const row = bottomRow();
                const left = Math.min(st.FootLeft === undefined ? row.L : st.FootLeft, row.L);
                const free = left - R.Gap - R.PL;
                const low = window.innerHeight <= 500;
                const inline = () => {
                    progE.classList.add("sw-dialog-progress-inline");
                    Object.assign(progE.style, { left: R.PL + "px", width: Math.floor(free) + "px", bottom: row.B + "px", height: row.H + "px" });
                };
                if (free >= em * 6) {
                    inline();
                    const pr = progE.getBoundingClientRect();
                    const te = progE.querySelector(".sw-dialog-progress-text");
                    const tm = progE.querySelector(".sw-dialog-progress-time");
                    const timeCut = !!(tm && !tm.hidden && tm.getBoundingClientRect().right > pr.right + 1);
                    const textCut = !!(te && te.textContent && te.scrollWidth > te.clientWidth + 1 && te.clientWidth < em * 6);
                    if (!timeCut && !textCut)
                        return;
                }
                if (low && free >= em * 3) {
                    inline();
                    foot.classList.add("sw-foot-tight");
                    return;
                }
                progE.classList.remove("sw-dialog-progress-inline", "sw-dialog-progress-off");
                progE.style.cssText = "";
                //  a low window without room in the button row: no status row (the body keeps the room for the focused field); the
                //  running button spins, the status stays the dialog's description and tool tip
                if (low) {
                    progE.classList.add("sw-dialog-progress-off");
                    return;
                }
                foot.classList.add("sw-foot-stack");
            };
            /** The pinned buttons may reach above the idle room (a taller Stop waiting): the room grows up to hold them (nothing overlaps). */
            const growRoom = () => {
                if (!foot || !footRef || !roomE)
                    return;
                const fr = foot.getBoundingClientRect();
                const bb = parseFloat(getComputedStyle(foot).borderBottomWidth) || 0;
                const base = fr.bottom - bb;
                let top = footRef.PB + footRef.RoomH;
                for (const b of Array.from(frozen.keys()).concat(Array.from(foot.querySelectorAll(":scope > .sw-dialog-busyabs, :scope > .sw-dialog-progress-inline"))))
                    if (b.isConnected && !isOff(b)) {
                        const r = b.getBoundingClientRect();
                        if (r.height)
                            top = Math.max(top, base - r.top);
                    }
                const h = Math.ceil(top - footRef.PB);
                if (Math.abs(h - (parseFloat(roomE.style.height) || 0)) > 0.5)
                    roomE.style.height = h + "px";
            };
            /** Lay the busy footer out again (the ways out changed). */
            const layoutFoot = () => {
                if (extraRow && !extraRow.children.length) {
                    /* SwWeb:allow ux-mot-1 (an empty footer row of the busy state, not an overlay) */
                    extraRow.remove();
                    extraRow = null;
                }
                placeItems();
                placeStatus();
                growRoom();
            };
            /** The status text changed (every second): its place again, unless it has a row of its own already (it never jumps back). */
            const statusTick = () => {
                if (!foot || !footRef || foot.classList.contains("sw-foot-stack"))
                    return;
                const was = foot.className;
                placeStatus();
                growRoom();
                if (foot.className !== was)
                    keepFocusInView();
            };
            /** A window resize while busy: measure the idle footer again (the user changed the window; the buttons may move then). */
            const refreezeFoot = () => {
                if (!footRef)
                    return;
                const items = busyItems();
                const hidden = progE.hidden;
                items.forEach(b => {
                    foot.append(b);
                    b.hidden = true;
                });
                progE.hidden = true;
                freezeFoot(false);
                freezeFoot(true);
                items.forEach(b => b.hidden = false);
                progE.hidden = hidden;
                layoutFoot();
            };
            /**
             * The last resort of the held box when the body can't give more room (it is down to its padding): first the body's padding
             * gives some (sw-dialog-squeezed: the title bar and the buttons stay), then the box grows at its top (a centered dialog keeps
             * its bottom edge, a sheet is anchored at the bottom, a moved one moves its top up), so the footer buttons still stay. Never
             * past the window. The room reserved at the opening (reserveRoom) normally makes this unnecessary.
             */
            const fitBox = () => {
                if (!st.Held || entry.Closed || !box.isConnected)
                    return;
                const last = foot && foot.isConnected ? foot : body;
                const cs = getComputedStyle(box);
                const overNow = () => {
                    const br = box.getBoundingClientRect();
                    return last.getBoundingClientRect().bottom - (br.bottom - (parseFloat(cs.borderBottomWidth) || 0) - (parseFloat(cs.paddingBottom) || 0));
                };
                if (overNow() <= 0.5)
                    return;
                if (!box.classList.contains("sw-dialog-squeezed")) {
                    box.classList.add("sw-dialog-squeezed");
                    if (overNow() <= 0.5)
                        return;
                }
                const br = box.getBoundingClientRect();
                const maxH = parseFloat(cs.maxHeight) || window.innerHeight;
                const h = Math.min(br.height + overNow(), maxH, window.innerHeight);
                if (h <= br.height + 0.5)
                    return;
                if (box.classList.contains("sw-placed"))
                    box.style.top = Math.max(0, br.top - (h - br.height)) + "px";
                else if (!isSheetLayout()) {
                    //  keep the bottom edge (the footer stays), but never past the top of the window
                    st.BottomKept = true;
                    box.style.top = "auto";
                    box.style.marginBottom = "0";
                    box.style.bottom = Math.max(0, Math.min(Math.round(window.innerHeight - br.bottom), Math.floor(window.innerHeight - h))) + "px";
                }
                box.style.height = h + "px";
            };
            /**
             * The room reserved when the dialog opened (reserveRoom, the empty block at the end of the body) is taken by what the held
             * box added since (the status row, the rejection line, busy footer rows): the block shrinks by the height the body lost, so
             * the body's own content keeps its room and a short body never scrolls (D24). Nothing above the block moves.
             */
            const useReserve = () => {
                if (!st.Held || entry.Closed)
                    return;
                const res = parseFloat(body.style.getPropertyValue("--sw-dlg-reserve")) || 0;
                const used = res > 0 ? Math.max(0, Math.min(res, Math.round((st.BodyH0 || 0) - body.clientHeight))) : 0;
                if (used !== (parseFloat(body.style.getPropertyValue("--sw-dlg-used")) || 0))
                    body.style.setProperty("--sw-dlg-used", used + "px");
            };
            /**
             * A held box whose body can't show the focused field whole any more although the footer fits (the rejection line at 400 %
             * zoom with a title of two lines and a scrolling footer, D1): the body's padding gives its room first (sw-dialog-squeezed,
             * as fitBox does when the footer overflows), so the field fits above the footer. The title bar and the footer buttons don't
             * move; unhold removes it.
             */
            const tighten = () => {
                const fe = document.activeElement;
                if (entry.Closed || !st.Held || box.classList.contains("sw-dialog-squeezed") || !fe || !body.contains(fe) || fe === body)
                    return;
                if (fe.getBoundingClientRect().height > body.clientHeight - 2)
                    box.classList.add("sw-dialog-squeezed");
            };
            /** Scroll the body so the focused field is whole in view (with its label when both fit); only the body scrolls. */
            const scrollFocus = () => {
                const fe = document.activeElement;
                if (entry.Closed || !fe || !body.contains(fe) || fe === body)
                    return;
                //  the field with its label (the control's block) when both fit, else the field (an input may reach out of its block)
                const f = fe.getBoundingClientRect();
                const blk = fe.closest(".sw-text, .sw-field, .sw-check, label");
                let t = { top: f.top, bottom: f.bottom };
                if (blk && blk !== fe && body.contains(blk)) {
                    const k = blk.getBoundingClientRect();
                    const u = { top: Math.min(k.top, f.top), bottom: Math.max(k.bottom, f.bottom) };
                    if (u.bottom - u.top <= body.clientHeight - 4)
                        t = u;
                }
                const b = body.getBoundingClientRect();
                const top = b.top + body.clientTop, bot = top + body.clientHeight;
                if (t.top < top)
                    body.scrollTop -= Math.ceil(top - t.top);
                else if (t.bottom > bot)
                    body.scrollTop += Math.min(Math.ceil(t.bottom - bot), Math.floor(t.top - top));
            };
            /**
             * After a change of the held layout: fit the box (fitBox), then keep the focused field whole in view of the body (with its
             * label when both fit). While held, every size change of the parts does the same (ResizeObserver: before it is painted).
             */
            const keepFocusInView = () => {
                const go = () => {
                    if (entry.Closed)
                        return;
                    fitBox();
                    useReserve();
                    tighten();
                    scrollFocus();
                };
                go();
                requestAnimationFrame(go);
                //  again after the status row opened (its transition gives the body its final height)
                let ms = 0;
                try {
                    ms = cssMs(getComputedStyle(statusE).transitionDuration);
                }
                catch {
                }
                setTimeout(go, ms + 80);
            };
            //  Any focus that lands in the body (the user's, the dialog's default focus, a focus trap that gives the focus back when the
            //  frame / window gets it again: those use preventScroll) brings the field whole into view, idle or busy (FOC, LAY-1)
            box.addEventListener("focusin", ev => {
                if (!entry.Closed && ev.target !== body && body.contains(ev.target)) {
                    scrollFocus();
                    requestAnimationFrame(scrollFocus);
                }
            });
            /**
             * Add a busy button (Cancel, Stop waiting, Continue in background, the 10 s Close) to the footer; layoutFoot places it.
             * @param {string} text The label.
             * @param {string} title The tool tip.
             * @param {function():void} fn The action.
             * @param {string} [kind] The button kind.
             * @param {HTMLElement} [at] The footer Cancel this button replaces in place (Stop waiting).
             * @returns {HTMLElement} The button.
             */
            const busyButton = (text, title, fn, kind, at) => {
                const sb = new Sw.Button({ Text: text, Title: title, Kind: kind, Class: "sw-dialog-busybtn", OnClick: () => {
                    fn();
                } });
                const b = sb.E;
                b.SwLabel = text;
                b.SwTitle = title;
                b.SwSb = sb;
                b.SwAt = at && at.isConnected ? at : null;
                //  out of the flow at once (placeItems places it): nothing moves before it is placed
                b.classList.add("sw-dialog-busyabs");
                b.style.position = "absolute";
                ensureFoot().append(b);
                if (b.SwAt) {
                    at.classList.add("sw-dialog-slot-gone");
                    at.inert = true;
                    at.setAttribute("aria-hidden", "true");
                }
                return b;
            };
            /** @type {Array<{E: HTMLElement, Was: boolean}>} Footer buttons this dialog locked while busy (restored after). */
            const locked = [];
            /** Busy means locked: the footer buttons that are not a way out are aria-disabled with the reason (restored after). */
            const lockButtons = on => {
                if (!on) {
                    for (const x of locked.splice(0))
                        if (x.E.isConnected && !x.Was)
                            setDisabled(x.E, false);
                    return;
                }
                if (!foot)
                    return;
                const running = new Set(st.Jobs.map(j => j.Def));
                for (const b of footBtns()) {
                    const d = b.SwDialogButton;
                    if (!d || d.Dismiss || d.Def.WhileBusy || running.has(d.Def) || locked.some(x => x.E === b))
                        continue;
                    locked.push({ E: b, Was: isDisabled(b) });
                    setDisabled(b, true, _TF("Still working: this is available again when the work is done", "Reason shown on a dialog button that can't be used while the dialog's work runs"));
                }
            };
            /** Bring the busy ui in line with the state (explicit SetBusy and / or the buttons' async work). */
            const syncBusy = () => {
                if (entry.Closed)
                    return;
                const busy = !!st.Ex || st.Jobs.length > 0;
                const O = st.Ex ? st.Ex.O : {};
                const was = st.Busy;
                if (busy && !st.Busy) {
                    st.Busy = true;
                    st.Start = Date.now();
                    //  the idle footer is the reference: the box and every button keep their place; the fields are read only
                    hold("busy", true);
                    freezeFoot(true);
                    lockFields(true);
                }
                else if (!busy && st.Busy) {
                    st.Busy = false;
                    st.XWay = null;
                    st.Stopping = false;
                    st.StopShort = false;
                    clearBusyTimers();
                    if (stopBtn) {
                        /* SwWeb:allow ux-mot-1 (a footer button of the busy state, not an overlay) */
                        stopBtn.remove();
                        stopBtn = null;
                    }
                }
                box.toggleAttribute("aria-busy", st.Busy);
                if (st.Busy)
                    box.setAttribute("aria-busy", "true");
                busyPage(st.Busy && !(st.Ex && st.Ex.O.StopsWithPage === false));
                box.classList.toggle("sw-dialog-busy", st.Busy);
                const canCancel = st.Busy && busyCancellable();
                const offerBg = st.Busy && !!O.Background && hasActivity();
                //  the author offered a way out (a footer Cancel) but the work can't be cancelled: "Stop waiting" takes its place at once
                const stopWait = st.Busy && !offerBg && canStopWaiting() && footCancels().length > 0;
                const sig = [st.Busy, canCancel, offerBg, stopWait, !!stopBtn, !!st.Ex].join(",");
                if (sig !== st.Ways) {
                    st.Ways = sig;
                    /* SwWeb:allow ux-mot-1 (footer buttons of the busy state, not an overlay: they leave with the state change) */
                    busyBtns.splice(0).forEach(b => b.remove());
                    st.StopShort = false;
                    //  a footer Cancel that can't stop the work is not offered: Stop waiting takes its slot, or the slot stays empty
                    const cancels = footCancels();
                    for (const b of cancels) {
                        const off = st.Busy && !canCancel;
                        b.classList.toggle("sw-dialog-slot-off", off);
                        b.classList.remove("sw-dialog-slot-gone");
                        b.inert = off;
                        b.toggleAttribute("aria-hidden", off);
                    }
                    if (st.Busy) {
                        if (canCancel && !cancels.length)
                            busyBtns.push(busyButton(_TF("Cancel", "Button of a busy dialog that stops the running work"), O.CancelTitle || _TF("Stop the work and close", "Tool tip of the Cancel button of a busy dialog"), () => attempt("cancel")));
                        if (stopWait && !stopBtn)
                            busyBtns.push(busyButton(stopWaitLabel(), stopWaitTitle(), () => attempt("wait"), undefined, cancels[0]));
                        if (offerBg)
                            busyBtns.push(busyButton(_TF("Continue in background", "Button of a busy dialog: close it while the work goes on (the activity list shows it)"),
                                _TF("Close this dialog; the work goes on and you are told when it is done", "Tool tip of the Continue in background button of a busy dialog"), () => attempt("background")));
                    }
                    //  the busy footer laid out before anything measures it (the line, the focus)
                    if (st.Busy)
                        layoutFoot();
                    lockButtons(st.Busy);
                    if (closeBtn) {
                        closeBtn.SetTitle(st.XWay && st.Busy ? st.XWay.Label : offerBg ? closeBgTitle : closeTitle);
                        closeBtn.SetLabel(st.XWay && st.Busy ? st.XWay.Label : "");
                    }
                    if (was !== st.Busy)
                        hideReject();
                    else
                        refreshReject();
                    //  the focus never stays on a button that no longer acts (busy means locked; only the ways out act)
                    const ae = document.activeElement;
                    if (st.Busy && ae && foot && foot.contains(ae) && (locked.some(x => x.E === ae) || ae.inert || ae.closest(".sw-dialog-slot-off, .sw-dialog-slot-gone")))
                        Focus.Set(wayOut());
                    if (st.Busy) {
                        layoutFoot();
                        keepFocusInView();
                    }
                }
                if (!st.Busy) {
                    progE.hidden = true;
                    if (was) {
                        //  the idle footer again (the same boxes as before the work), the fields editable, the box free
                        freezeFoot(false);
                        lockFields(false);
                        hold("busy", false);
                    }
                    describe();
                    return;
                }
                //  Progress (FBK-4): what it waits for, determinate when known, the time so far; never in a live region
                const showProg = st.Ex ? !!(O.Text || O.Progress !== undefined || o.Reserve || st.Stopping) : true;
                progE.hidden = !showProg;
                if (showProg) {
                    const f = ensureFoot();
                    if (progE.parentNode !== f || f.firstChild !== progE)
                        f.prepend(progE);
                    renderProgress();
                    if (!st.Tick)
                        st.Tick = setInterval(renderProgress, 1000);
                    //  milestones: at most one announcement every 30 s
                    if (!st.Mile && O.Announce !== false)
                        st.Mile = setInterval(() => announce(_T("Still working, {0} so far", elapsed(), "Screen reader announcement every 30 seconds while a dialog is busy. {0}: the time so far, ex: 1 min 30 s"), box), 30000);
                }
                //  A busy dialog with no way out: at 10 s it adds the Close that names the consequence (left of the buttons already
                //  there, or in a row of its own above them)
                if (!st.Ten && !stopBtn && !st.XWay && !canCancel && !offerBg && !stopWait && O.NoClose !== true)
                    st.Ten = setTimeout(() => {
                        st.Ten = 0;
                        const O2 = st.Ex ? st.Ex.O : {};
                        if (!st.Busy || entry.Closed || stopBtn || busyCancellable() || (O2.Background && hasActivity()))
                            return;
                        const stops = typeof O2.Stop === "function";
                        //  a button's sent work: the user stops waiting, the work may still be done (never "Cancel" for it)
                        const wait = !stops && !O2.CloseText && canStopWaiting();
                        const label = O2.CloseText || (stops
                            ? _TF("Close (the work stops)", "Button added to a busy dialog after 10 s when its work can be stopped: closes it and stops the work (the label names the consequence)")
                            : wait ? stopWaitLabel()
                                : _TF("Close (the work still finishes)", "Button added to a busy dialog after 10 s when its work can't be stopped: closes it without waiting (the label names the consequence)"));
                        stopBtn = busyButton(label, stops
                            ? _TF("Close this dialog and stop the work", "Tool tip of the Close button added to a busy dialog after 10 s that stops the work")
                            : wait ? stopWaitTitle()
                                : _TF("Close this dialog without waiting; the work still finishes, its result is not shown here", "Tool tip of the Close button added to a busy dialog after 10 s whose work goes on"), () => attempt(stops ? "stop" : "wait"));
                        syncBusy();
                        //  a low window (400 % zoom, a phone in landscape) without room for it in the button row: no row of its own
                        //  (it would cover the focused field); the title bar's × becomes this way out (its name and tool tip say so)
                        if (stopBtn && closeBtn && window.innerHeight <= 500 && stopBtn.closest(".sw-dialog-foot-extra")) {
                            /* SwWeb:allow ux-mot-1 (a footer button of the busy state, not an overlay) */
                            stopBtn.remove();
                            stopBtn = null;
                            st.XWay = { Kind: stops ? "stop" : "wait", Label: label };
                            syncBusy();
                        }
                        keepFocusInView();
                        announce(_T("Still working: use {0}", st.XWay ? xWayName() : label, "Announcement when a busy dialog adds a way out after 10 s. {0}: the label of the added button, ex: Close (the upload stops)"), box);
                    }, Dialog.WayOutMs > 0 ? Dialog.WayOutMs : 10000);
                describe();
            };
            /** Render the progress text (the elapsed time ticks here, not in a live region; the rejection line is not touched). */
            const renderProgress = () => {
                if (!st.Busy)
                    return;
                const O = st.Ex ? st.Ex.O : {};
                const parts = [];
                if (!st.Stopping && O.Progress !== undefined && O.Progress !== null && isFinite(+O.Progress)) {
                    const pct = Math.round(clamp(+O.Progress, 0, 1) * 100);
                    let bar = progE.querySelector(".sw-dialog-bar");
                    if (!bar) {
                        bar = el("div.sw-dialog-bar", { attrs: { role: "progressbar", "aria-valuemin": "0", "aria-valuemax": "100" } }, el("div.sw-dialog-bar-fill"));
                        progE.prepend(bar);
                    }
                    bar.setAttribute("aria-valuenow", String(pct));
                    bar.setAttribute("aria-label", O.Text || _TF("Progress", "Accessible name of the progress bar of a busy dialog"));
                    bar.firstChild.style.transform = "scaleX(" + (pct / 100) + ")";
                    parts.push(pct + " %");
                }
                else {
                    const bar = progE.querySelector(".sw-dialog-bar");
                    bar && bar.remove();
                    //  the running button already spins (Sw.Button busy): no second spinner for a button's work
                    if (!o.Reserve && st.Ex && !progE.querySelector(".sw-spinner"))
                        progE.prepend(new Spinner({ Size: "1.25em", Title: _TF("Working", "Accessible name of a busy indicator") }).E);
                }
                const what = busyWhat();
                if (st.Stopping)
                    //  running → stopping: the stop was asked for, the work has not ended yet (one cancellation model)
                    parts.push(cancelWords("stopping", what));
                else if (O.Text && O.ShowText !== false)
                    parts.push(O.Text);
                else if (!st.Ex && st.Jobs.length && st.Jobs[0].Text)
                    parts.push(footBtns().some(x => x.SwLabel && !isOff(x) && x.textContent.trim() !== x.SwLabel)
                        ? _T("Working: {0}; it may still finish if you stop waiting", st.Jobs[0].Text, "Status line of a dialog while the work of one of its buttons runs and its Stop waiting button has only its short label. {0}: the button label, ex: Save")
                        : _T("Working: {0}", st.Jobs[0].Text, "Status line of a dialog while the work of one of its buttons runs. {0}: the button label, ex: Save"));
                let t = progE.querySelector(".sw-dialog-progress-text");
                if (!t) {
                    t = el("span.sw-dialog-progress-text.sw-selectable");
                    progE.append(t);
                }
                //  the time so far has its own slot: the text ellipsizes, the time never does (TXT-17)
                let tm = progE.querySelector(".sw-dialog-progress-time");
                if (!tm) {
                    tm = el("span.sw-dialog-progress-time");
                    progE.append(tm);
                }
                const time = Date.now() - st.Start >= 2000 ? elapsed() : "";
                t.textContent = parts.join(" · ");
                tm.textContent = time ? (parts.length ? "· " : "") + time : "";
                tm.hidden = !time;
                progE.title = [t.textContent, time].filter(x => x).join(" · ");
                statusTick();
            };
            /**
             * The words of the one cancellation model (Sw.Cancel.Texts of the core, the same as the activity list and the buttons),
             * named after what runs: "Save: Stopping…", "Save: Cancelled", "Save: Finished before it could stop".
             * @param {string} kind "stopping", "cancelled" or "done". @param {string} what What runs (may be empty).
             * @returns {string} The text.
             */
            const cancelWords = (kind, what) => {
                const T = Sw.Cancel && Sw.Cancel.Texts;
                const w = T ? (kind === "stopping" ? T.Stopping() : kind === "cancelled" ? T.Cancelled() : T.FinishedAnyway())
                    : (kind === "stopping" ? _TF("Stopping…", "State of running work after the user chose Cancel, until the work confirms that it stopped")
                        : kind === "cancelled" ? _TF("Cancelled", "State of work that stopped after the user chose Cancel")
                            : _TF("Finished before it could stop", "State of work that finished although the user chose Cancel (it could not stop in time)"));
                return what ? _T("{0}: {1}", what, w, "A state of the work of a dialog after Cancel, named after what runs. {0}: what runs, ex: Save; {1}: the state, ex: Stopping…") : w;
            };
            /**
             * Stop the running work, honestly, with one cancellation model (POP-6, FBK-5): running → stopping → stopped / done / failed.
             * "cancel" (only work that sent no write and honours the abort: a write that went out meanwhile turns it into "wait"):
             * the stop is asked for, the status says "Stopping Save…" until the work has ended (at most 2 s): aborted = stopped
             * ("Save cancelled"), it finished anyway = done (the dialog closes with its value, "Save finished before it could be
             * cancelled"), it failed = failed (the dialog stays, the failure is shown), still running after 2 s = the dialog stops
             * waiting ("may still finish"). "stop" (the 10 s Close of work with a Stop function) and "wait" (Stop waiting: the work
             * goes on, a sent write is never aborted; its later result is still said) end the waiting at once.
             * @param {string} how "cancel", "stop" or "wait".
             * @returns {Promise<string>} "stopped", "done", "failed", "waiting" (stopped waiting) or "closed".
             */
            const stopWork = async how => {
                const O = st.Ex ? st.Ex.O : {};
                const what = busyWhat();
                if (how === "cancel" && !busyCancellable() && !O.Cancel)
                    how = "wait";
                const cancel = how === "cancel";
                const fn = cancel ? (O.Cancel || O.Stop) : how === "stop" ? O.Stop : null;
                const said = text => {
                    if (what)
                        announce(text, box);
                };
                if (!cancel) {
                    try {
                        if (typeof fn === "function")
                            await fn();
                    }
                    catch (x) {
                        console.error(x);
                    }
                    for (const j of st.Jobs.splice(0))
                        try {
                            if (j.Detach)
                                j.Detach();
                        }
                        catch (x) {
                            console.error(x);
                        }
                    st.Ex = null;
                    syncBusy();
                    said(typeof fn === "function"
                        ? cancelWords("cancelled", what)
                        : _T("Stopped waiting for {0}; it may still finish", what, "Announcement after the user stopped waiting for work of a dialog that can't be cancelled (a request was sent). {0}: what runs, ex: Save"));
                    return typeof fn === "function" ? "stopped" : "waiting";
                }
                //  running → stopping
                st.Stopping = true;
                for (const b of footCancels().concat(busyBtns))
                    if (b.isConnected)
                        setDisabled(b, true, _TF("Stopping: the dialog closes when the work has ended", "Reason shown on the Cancel button of a dialog while its work is being stopped"));
                renderProgress();
                syncBusy();
                //  the line follows the state (it never says "use Cancel" while Cancel is stopping the work)
                refreshReject();
                const ps = st.Jobs.map(j => j.P).filter(p => p && typeof p.then === "function");
                if (st.Ex && st.Ex.P)
                    ps.push(st.Ex.P);
                try {
                    if (typeof O.Abort === "function")
                        O.Abort();
                    //  the Sw.Button whose click started the work / opened the dialog: its action is cancelled too
                    for (const b of new Set([O.Opener, opener]))
                        if (b && typeof b.CanAbort === "function" && b.CanAbort())
                            b.Abort();
                    for (const j of st.Jobs)
                        if (j.Abort)
                            j.Abort();
                    if (typeof fn === "function")
                        await fn();
                }
                catch (x) {
                    console.error(x);
                }
                let outcome = "stopped";
                if (ps.length) {
                    const rs = await Promise.race([Promise.allSettled(ps), new Promise(r => setTimeout(() => r(null), 2000))]);
                    if (!rs)
                        outcome = "waiting";
                    else if (rs.some(x => x.status === "rejected" && !isAbort(x.reason)))
                        outcome = "failed";
                    else if (rs.some(x => x.status === "fulfilled") && !rs.some(x => x.status === "rejected"))
                        outcome = "done";
                }
                for (const b of footCancels().concat(busyBtns))
                    if (b.isConnected)
                        setDisabled(b, false);
                st.Stopping = false;
                if (entry.Closed) {
                    //  the work finished and closed the dialog with its value (done)
                    if (outcome === "done")
                        said(cancelWords("done", what));
                    return "closed";
                }
                if (outcome === "waiting")
                    for (const j of st.Jobs.splice(0))
                        try {
                            if (j.Detach)
                                j.Detach();
                        }
                        catch (x) {
                            console.error(x);
                        }
                if (outcome !== "failed") {
                    st.Jobs.length = 0;
                    st.Ex = null;
                }
                syncBusy();
                if (outcome === "stopped")
                    said(cancelWords("cancelled", what));
                else if (outcome === "waiting")
                    said(_T("Stopped waiting for {0}; it may still finish", what, "Announcement after the user stopped waiting for work of a dialog that can't be cancelled (a request was sent). {0}: what runs, ex: Save"));
                else if (outcome === "done")
                    said(cancelWords("done", what));
                return outcome;
            };

            // ---------------------------------------------------------- close attempts (UX-POP-6 table)
            /** The footer buttons with their definitions (shown ones only). */
            const footDefs = () => footBtns().filter(b => b.SwDialogButton && !isOff(b)).map(b => b.SwDialogButton);
            /** The text of the rejection line for the current state, naming only the visible buttons by their labels. */
            const rejectText = kind => {
                if (st.Stopping && st.Busy) {
                    const w = busyWhat();
                    return w ? _T("Stopping {0}: the dialog closes when it has stopped", w, "Line of a dialog after Cancel while its work is being stopped (a close attempt now). {0}: what runs, ex: Save")
                        : _TF("Stopping: the dialog closes when the work has stopped", "Line of a dialog after Cancel while its work is being stopped");
                }
                if (kind === "busy-edit" && st.Busy) {
                    const w = busyWhat();
                    return w ? _T("{0} is running: you can change the fields again when it is done", w, "Line of a busy dialog when the user tries to change a field while its work runs (the fields are read only then, so nothing typed is lost). {0}: what runs, ex: Save")
                        : _TF("Still working: you can change the fields again when it is done", "Line of a busy dialog when the user tries to change a field while its work runs");
                }
                if (st.Busy) {
                    const w = busyWays();
                    return w.length >= 2 ? _T("Still working: use {0} or {1}", w[0], w[1], "The rejection line of a dialog after a close attempt. {0}, {1}: the visible labels of the buttons to use, ex: Save, Cancel")
                        : w.length ? _T("Still working: use {0}", w[0], "The rejection line of a busy dialog after a close attempt. {0}: the label of the button to use, ex: Cancel")
                            : _TF("Still working: please wait until it is done", "The rejection line of a busy dialog that has no way out yet (a close attempt was refused)");
                }
                const defs = footDefs();
                if (st.Dirty) {
                    const save = defs.find(x => !x.Dismiss && (x.Def.Kind === "primary" || x.Def.Kind === "danger" || x.Def.Kind === "danger-solid")) || defs.find(x => !x.Dismiss);
                    const cancel = defs.find(x => x.Dismiss);
                    if (save && cancel)
                        return _T("Unsaved changes: use {0} or {1}", save.Text, cancel.Text, "The rejection line of a dialog after a close attempt. {0}, {1}: the visible labels of the buttons to use, ex: Save, Cancel");
                    const one = (save || cancel) ? (save || cancel).Text : closeTitle;
                    return _T("Unsaved changes: use {0}", one, "The rejection line of a dialog with unsaved changes after a close attempt. {0}: the label of the button to use, ex: Save");
                }
                const labels = defs.map(x => x.Text).filter(x => x);
                return labels.length >= 2 ? _T("Use {0} or {1}", labels[0], labels[1], "The rejection line of a dialog that only closes with its buttons, after a close attempt (an outside click, Escape). {0}, {1}: the button labels, ex: Cancel, Save")
                    : labels.length ? _T("Use {0}", labels[0], "The rejection line of a dialog that only closes with its button, after a close attempt. {0}: the button label, ex: OK") : "";
            };
            /**
             * A low window (400 % zoom) where the line's own row would leave the body less room than the focused field (D1: a title of
             * two lines, three buttons): the line takes the place of the title text (sw-dialog-line-head: the title row keeps its size,
             * ⋮ / × and the footer don't move, the body keeps its room, so the field stays whole in view) until it goes. The title is
             * still the dialog's accessible name; the line is in its description and announced, as in its own row.
             * @param {boolean} on Show the line there (when needed), or back to its own row.
             */
            const lineInHead = on => {
                const te = head.querySelector(".sw-dialog-title");
                if (!on || rejectE.hidden || !te) {
                    if (box.classList.contains("sw-dialog-line-head")) {
                        box.classList.remove("sw-dialog-line-head");
                        ["--sw-lh-x", "--sw-lh-y", "--sw-lh-w", "--sw-lh-h"].forEach(p => box.style.removeProperty(p));
                    }
                    return;
                }
                const fe = document.activeElement;
                if (window.innerHeight > 500 || !fe || !body.contains(fe) || fe === body || box.classList.contains("sw-dialog-line-head"))
                    return;
                const rcs = getComputedStyle(rejectE);
                const row = rejectE.offsetHeight + (parseFloat(rcs.marginTop) || 0) + (parseFloat(rcs.marginBottom) || 0);
                const tr = te.getBoundingClientRect();
                if (body.clientHeight - row >= fe.getBoundingClientRect().height + 2 || tr.height < rejectE.offsetHeight)
                    return;
                const br = box.getBoundingClientRect();
                box.style.setProperty("--sw-lh-x", Math.round(tr.left - br.left) + "px");
                box.style.setProperty("--sw-lh-y", Math.round(tr.top - br.top) + "px");
                box.style.setProperty("--sw-lh-w", Math.round(tr.width) + "px");
                box.style.setProperty("--sw-lh-h", Math.round(tr.height) + "px");
                box.classList.add("sw-dialog-line-head");
            };
            /** Hide the rejection line (the dirty / busy state changed: it stays until then, or until the next close attempt). */
            const hideReject = () => {
                if (rejectE.hidden)
                    return;
                rejectE.hidden = true;
                lineInHead(false);
                describe();
                //  the row collapses with its transition; the footer is held until then (nothing moves)
                clearTimeout(st.UnholdT);
                let ms = 0;
                try {
                    ms = cssMs(getComputedStyle(statusE).transitionDuration);
                }
                catch {
                }
                st.UnholdT = setTimeout(() => {
                    if (rejectE.hidden)
                        hold("status", false);
                }, ms + 60);
            };
            /**
             * Show the rejection line in the status slot (its own row after the title bar): nothing is covered, the footer stays where
             * it is (hold), the focused field stays visible in the body.
             */
            const placeReject = () => {
                clearTimeout(st.UnholdT);
                hold("status", true);
                lineInHead(true);
                keepFocusInView();
                coverCheck(box, o.Title || o.AriaLabel || dlgId);
            };
            /** The ways out changed while the line is shown (ex: the 10 s Close arrived): the line names the current buttons. */
            const refreshReject = () => {
                if (rejectE.hidden || st.LineKind === "notice")
                    return;
                const msg = rejectText(st.LineKind === "busy-edit" && !st.Busy ? "" : st.LineKind);
                if (!msg) {
                    hideReject();
                    return;
                }
                if (rejectE.textContent !== msg) {
                    rejectE.textContent = msg;
                    rejectE.title = msg;
                    placeReject();
                }
            };
            /**
             * The rejection cue (FBK-1): a pulse within 100 ms (none with zero motion), the line naming the buttons to use, announced on
             * every attempt (the same input within 1 s once); the focus stays. The line stays until the next close attempt or a change
             * of the dirty / busy state (progress ticks don't touch it).
             * @param {string} kind The close attempt.
             * @returns {boolean} false (not closed).
             */
            const reject = kind => {
                const msg = rejectText(kind);
                box.classList.remove("sw-reject-pulse");
                void box.offsetWidth;
                box.classList.add("sw-reject-pulse");
                clearTimeout(st.PulseT);
                st.PulseT = setTimeout(() => box.classList.remove("sw-reject-pulse"), 1000);
                if (msg) {
                    st.LineKind = kind;
                    //  hold the footer first, then reveal the row (also with zero motion)
                    clearTimeout(st.UnholdT);
                    hold("status", true);
                    rejectE.textContent = msg;
                    rejectE.title = msg;
                    rejectE.hidden = false;
                    placeReject();
                    describe();
                    const now = Date.now();
                    if (!(st.Rej.Kind === kind && now - st.Rej.T < 1000)) {
                        st.Rej = { Kind: kind, T: now };
                        announce(msg, box);
                    }
                }
                return false;
            };
            /**
             * A line in the status slot that is not a rejection (no pulse): what happened to the work (ex: "Save is done; your newer changes
             * are not saved yet"), announced; it stays until the next close attempt or state change.
             * @param {string} msg The text.
             */
            const notice = msg => {
                if (entry.Closed || !msg)
                    return;
                st.LineKind = "notice";
                clearTimeout(st.UnholdT);
                hold("status", true);
                rejectE.textContent = msg;
                rejectE.title = msg;
                rejectE.hidden = false;
                placeReject();
                describe();
                announce(msg, box);
            };
            /**
             * Ask "Discard changes?" ("Discard" / "Keep editing", Keep editing focused; any close attempt on it keeps editing).
             * @returns {Promise<boolean>} True to discard.
             */
            const askDiscard = async () => {
                st.Asking = true;
                try {
                    return await Dialog.Confirm(_TF("Discard changes?", "Title of the confirmation when a dialog with unsaved changes is closed"),
                        _TF("Your changes in this dialog are not saved.", "Text of the confirmation when a dialog with unsaved changes is closed"),
                        _TF("Discard", "Button of the confirmation that closes a dialog and throws away its unsaved changes"),
                        //  owned by this dialog: when its work ends and closes it while the question is open, the question goes too
                        { Danger: true, CancelText: _TF("Keep editing", "Button of the confirmation that keeps a dialog with unsaved changes open"), Id: "sw.discard", Owner: handle });
                }
                finally {
                    st.Asking = false;
                }
            };
            /**
             * Close a dirty dialog after "Discard changes?", a clean one at once.
             * @param {*} v The value.
             * @returns {Promise<boolean>} True if closed.
             */
            const closeAsDirty = async v => {
                if (st.Dirty) {
                    if (!(await askDiscard()))
                        return false;
                    //  the unsaved input was thrown away on purpose: its drafts go too (handle.OnClose: Discarded)
                    st.Discarded = true;
                }
                close(v === undefined ? null : v);
                return true;
            };
            /** "Continue in background": the work goes on (the activity list shows it), the dialog closes, the focus returns to the opener. */
            const background = () => {
                const O = st.Ex ? st.Ex.O : {};
                try {
                    if (typeof O.Background === "function")
                        O.Background({ Promise: st.Ex.P, Cancel: O.Cancel || null, Title: O.Title || o.Title || "" });
                    else if (hasActivity()) {
                        //  the shell's activity list (core/activity.js): Track a promise, else Start a job. Key: SetBusy's Key, else the
                        //  explicit dialog Id, else this dialog (two dialogs of one kind are two jobs; Track finds a running job by Key)
                        const A = Sw.Activity;
                        const cancel = O.Cancel || O.Abort || undefined;
                        const jo = { Key: O.Key || (o.Id && o.Id !== "sw.working" ? o.Id : null) || titleId, Title: O.Title || o.Title || "", Text: O.Text, Cancel: cancel };
                        if (st.Ex && st.Ex.P && typeof A.Track === "function")
                            A.Track(st.Ex.P, jo);
                        else if (typeof A.Start === "function")
                            A.Start(jo);
                    }
                }
                catch (x) {
                    console.error(x);
                }
                announce(_TF("Continuing in the background", "Announcement after a busy dialog was closed with Continue in background"), box);
                st.Ex = null;
                st.Jobs.length = 0;
                close(null);
                return true;
            };
            /**
             * A close attempt (glossary): what it does depends on the state, as in the UX-POP-6 table.
             * @param {string} kind "x" (close button), "footer" (a Cancel / Close button, def), "escape", "outside", "back", "swipe",
             * "cancel" (the busy Cancel), "stop" (the 10 s Close of work with a Stop function), "wait" (Stop waiting: work that can't be
             * cancelled goes on), "background", "enter", "busy-button" (a locked button).
             * @param {object} [def] The footer button definition.
             * @returns {Promise<boolean>} True if the dialog closed.
             */
            const attempt = async (kind, def) => {
                if (entry.Closed || st.Asking)
                    return false;
                //  while the work is being stopped every attempt gets the cue (the dialog closes when it has ended)
                if (st.Stopping)
                    return reject(kind);
                const busy = st.Busy;
                const O = st.Ex ? st.Ex.O : {};
                const canBg = busy && !!O.Background && hasActivity();
                const canCancel = busy && busyCancellable();
                switch (kind) {
                    case "x":
                        if (!busy)
                            return closeAsDirty(null);
                        //  (the × is the way out of a low window: see the 10 s way out)
                        if (st.XWay)
                            return attempt(st.XWay.Kind);
                        if (!st.Dirty && canBg)
                            return background();
                        return reject(kind);
                    case "footer": {
                        const v = def ? def.Value : null;
                        if (!busy)
                            return closeAsDirty(v);
                        //  a plain Close acts as the close button; Cancel (or a Close naming a consequence) stops the work first
                        if ((def && def.Dismiss === "close") || !canCancel)
                            return attempt("x");
                        const out = await stopWork("cancel");
                        if (entry.Closed)
                            return true;
                        if (out === "failed")
                            return false;
                        return closeAsDirty(v);
                    }
                    case "cancel":
                    case "stop":
                    case "wait":
                        //  Cancel stops the work; Stop waiting leaves it running (it may still finish); then clean / dirty as usual
                        const out2 = await stopWork(kind === "cancel" && !canCancel ? "wait" : kind);
                        if (entry.Closed)
                            return true;
                        if (out2 === "failed")
                            return false;
                        return closeAsDirty(null);
                    case "background":
                        if (!canBg)
                            return reject(kind);
                        if (st.Dirty && !O.InputSent && !(await askDiscard()))
                            return false;
                        return background();
                    case "outside":
                        if (busy || st.Dirty || explicitModal || (o.NoEscape && o.Close === false))
                            return reject(kind);
                        close(null);
                        return true;
                    case "escape":
                    case "back":
                    case "swipe":
                        if (busy || (o.NoEscape && kind === "escape") || (o.NoEscape && o.Close === false))
                            return reject(kind);
                        return closeAsDirty(null);
                    default:
                        return reject(kind);
                }
            };

            const handle = {
                Box: box, Body: body, Tools: tools, Close: close, Result: result, Id: dlgId,
                /**
                 * Mark the dialog dirty (unsaved input) or clean (saved, or back to the opening values); after the first call the automatic
                 * field tracking is off. Usually not needed: fields and Sw controls are compared with their opening values.
                 * @param {boolean} v Dirty.
                 */
                SetDirty(v) {
                    st.Manual = true;
                    applyDirty(v);
                },
                /** @returns {boolean} True while dirty. */
                IsDirty: () => st.Dirty,
                /**
                 * Count the values set from code since the dialog opened as the user's (a restored draft: text the user typed before):
                 * compare the fields and controls with the values they had when the dialog opened. Not needed after filling a dialog
                 * from code (a change without user input is the opening value by itself).
                 */
                Recheck() {
                    for (const [k, v] of codeOld)
                        initial.set(k, v);
                    for (const [k, v] of codeOldState)
                        initialState.set(k, v);
                    codeOld.clear();
                    codeOldState.clear();
                    for (const [f, v] of openVal)
                        if (f.isConnected && tracked(f) && !initial.has(f))
                            initial.set(f, v);
                    for (const [e, v] of openState)
                        if (e.isConnected && !initialState.has(e))
                            initialState.set(e, v);
                    recompute();
                },
                /**
                 * Take the current values as the opening values (after a save that keeps the dialog open): the dialog is clean.
                 */
                Rebase() {
                    //  the controls' own base is older than the new opening values: compare those here from now on
                    for (const k of reported.keys())
                        rebased.add(k);
                    reported.clear();
                    initial.clear();
                    initialState.clear();
                    codeOld.clear();
                    codeOldState.clear();
                    openVal.clear();
                    openState.clear();
                    body.querySelectorAll(FieldSel).forEach(snap);
                    snapStates(true);
                    st.Other = false;
                    recompute();
                },
                /**
                 * Call a function when the dialog closes (once): fn({ Value, Discarded }) — Discarded is true when the user threw
                 * unsaved input away ("Discard changes?" → Discard). Drafts (Sw.Draft) in a dialog use it: Discard clears the draft,
                 * any close disposes it.
                 * @param {function({Value: *, Discarded: boolean}):void} fn The function.
                 * @returns {function():void} Removes it.
                 */
                OnClose(fn) {
                    if (entry.Closed) {
                        try {
                            fn({ Value: null, Discarded: st.Discarded });
                        }
                        catch (x) {
                            console.error(x);
                        }
                        return () => { };
                    }
                    st.OnClose.push(fn);
                    return () => {
                        const i = st.OnClose.indexOf(fn);
                        if (i >= 0)
                            st.OnClose.splice(i, 1);
                    };
                },
                /**
                 * Dispose something with the dialog: a function, or an object with Dispose() / Release() / disconnect().
                 * @param {function|object} x The thing.
                 * @returns {function|object} x.
                 */
                Own(x) {
                    handle.OnClose(() => {
                        if (typeof x === "function")
                            x();
                        else if (x && typeof x.Dispose === "function")
                            x.Dispose();
                        else if (x && typeof x.Release === "function")
                            x.Release();
                        else if (x && typeof x.disconnect === "function")
                            x.disconnect();
                    });
                    return x;
                },
                /**
                 * Mark the dialog busy (work running) or not.
                 * @param {Promise|function|boolean} x A promise (busy until it settles), a function({Signal, Progress(v, text)}) that
                 * starts the work and returns its promise (reading Signal makes the work cancellable: Cancel aborts it), true (busy until
                 * SetBusy(false)) or false.
                 * @param {object} [bo] Options: { Cancel() (the work can be stopped: Cancel buttons stop it), Abort (an AbortController,
                 * or a function: Cancel aborts it), Background (true or a function({Promise, Cancel, Title}): "Continue in background"
                 * is offered when the activity list exists, Sw.Activity), InputSent (the unsaved input went with the job: "Continue in
                 * background" doesn't ask), Text (what it waits for, shown and announced at the start), Progress (0..1, determinate),
                 * CloseText (the label of the 10 s Close that names the consequence, ex: "Close (the upload stops)"), Stop() (called by
                 * that Close), StopsWithPage (default true: closing the tab asks), Announce (default true), Key, Title (of the background
                 * job), NoClose (no 10 s Close) }.
                 */
                SetBusy(x, bo) {
                    bo = Object.assign({}, bo || {});
                    st.SetBusyUsed = true;
                    if (!x) {
                        st.Ex = null;
                        syncBusy();
                        return;
                    }
                    if (bo.Abort && typeof bo.Abort.abort === "function") {
                        const c = bo.Abort;
                        bo.Abort = () => c.abort();
                    }
                    if (typeof x === "function") {
                        const ac = typeof AbortController === "function" ? new AbortController() : null;
                        let used = false;
                        const fx = x;
                        x = fx({
                            get Signal() {
                                used = true;
                                return ac ? ac.signal : undefined;
                            },
                            Progress: (v, t) => handle.SetProgress(v, t),
                        });
                        if (used && ac && !bo.Abort)
                            bo.Abort = () => ac.abort();
                        if (!(x && typeof x.then === "function"))
                            x = Promise.resolve(x);
                    }
                    //  Set while a Sw.Button's OnClick runs: the work was started in that click (its requests carry the button's
                    //  AbortSignal), so Cancel can stop it through Sw.Button.Abort
                    if (!bo.Cancel && !bo.Abort && Sw.Button && Sw.Button.Current && typeof Sw.Button.Current.Abort === "function")
                        bo.Opener = Sw.Button.Current;
                    const rec = { O: bo, P: x && typeof x.then === "function" ? x : null };
                    st.Ex = rec;
                    syncBusy();
                    //  the button's abort controller exists once its OnClick returned (still this task): look at the ways out again
                    if (bo.Opener)
                        queueMicrotask(syncBusy);
                    if (bo.Text && bo.Announce !== false)
                        announce(bo.Text, box);
                    if (rec.P)
                        rec.P.then(() => {
                            if (st.Ex === rec) {
                                st.Ex = null;
                                syncBusy();
                            }
                        }, () => {
                            if (st.Ex === rec) {
                                st.Ex = null;
                                syncBusy();
                            }
                        });
                },
                /** @returns {boolean} True while busy. */
                IsBusy: () => st.Busy,
                /**
                 * Update the progress of a busy dialog: a new text is a phase change (announced), the value is not. Only the status slot
                 * changes (the buttons and the rejection line stay).
                 * @param {number|null} value 0..1, or null (unknown).
                 * @param {string} [text] What it does now.
                 */
                SetProgress(value, text) {
                    if (!st.Ex)
                        return;
                    const O = st.Ex.O;
                    const v = value === null || value === undefined || !isFinite(+value) ? undefined : +value;
                    if (value !== null && value !== undefined && !(+value >= 0 && +value <= 1))
                        devWarn("Sw.Dialog.SetProgress: the value must be 0..1 or null (unknown), got " + value + ".", "dlg-progress");
                    O.Progress = v;
                    if (text !== undefined && text !== O.Text) {
                        O.Text = text;
                        if (O.Announce !== false)
                            announce(text, box);
                    }
                    if (progE.hidden)
                        syncBusy();
                    else
                        renderProgress();
                },
                /**
                 * A close attempt from code (as the user would do it).
                 * @param {string} [kind] The kind (default "x").
                 * @returns {Promise<boolean>} True if closed.
                 */
                Attempt: kind => attempt(kind || "x"),
            };
            ctl.Handle = handle;
            entry.Handle = handle;
            box.SwDialog = handle;
            Object.defineProperty(handle, "SwEntry", { value: entry, enumerable: false });
            //  aria-describedby: screen readers read the message (ex: the item and "This can not be undone.") with the title, plus the
            //  rejection line and the busy progress while shown
            let descId = null;
            const describe = () => {
                if (descId === null && o.Describe !== false) {
                    let d = o.Describe;
                    if (typeof d === "string")
                        d = body.querySelector(d);
                    if (!d && o.Describe === undefined)
                        d = o.Role === "alertdialog" ? body : body.querySelector(".sw-dialog-text");
                    if (d instanceof Element && d.textContent.trim()) {
                        if (!d.id)
                            d.id = Sw.Uid("sw-dlg-desc");
                        descId = d.id;
                    }
                }
                const ids = [descId, rejectE.hidden ? null : rejectE.id, progE.hidden || !progE.isConnected ? null : progE.id].filter(x => x);
                if (ids.length)
                    box.setAttribute("aria-describedby", ids.join(" "));
                else
                    box.removeAttribute("aria-describedby");
            };
            // ---------------------------------------------------------- reserved room and stable geometry (LAY-1, POP-6)
            //  (a dialog with a Cancel and an action may run work set later with SetBusy, ex: an upload; a confirmation does not)
            /** True when the dialog may run work (a button's OnClick, the Working dialog, SetBusy was used). */
            const hasWork = () => !!(o.Reserve || (o.Buttons || []).some(b => b && b.OnClick) || st.SetBusyUsed ||
                (o.Role !== "alertdialog" && (o.Buttons || []).some(b => b && isDismiss(b)) && (o.Buttons || []).some(b => b && !isDismiss(b))));
            /**
             * Reserve the room the busy and close attempt states need, when the dialog opens (UX guide POP-6: "the reserved space fits
             * two lines"): the rejection line (as many lines as the longest line this dialog can show takes: its real texts with its button
             * labels, Designer S5b N2; one in a low window), the status row of a busy footer and the row of the 10 s
             * Close when they can't share the button row, less what the body can give while its largest field (or two lines of text)
             * stays whole. The rest is an empty block at the end of the body (--sw-dlg-reserve), so later nothing grows: the title,
             * ⋮, × and the footer buttons stay where the user's pointer is, on sheets too. Never past the window (the box grows at most
             * to its max height).
             */
            const reserveRoom = () => {
                if (entry.Closed || st.Held || !box.isConnected || box.matches(".sw-full, .sw-max, .sw-sized, .sw-sheet-sized, .sw-leaving"))
                    return;
                const cur = parseFloat(body.style.getPropertyValue("--sw-dlg-reserve")) || 0;
                const set = r => {
                    r = Math.max(0, Math.round(r));
                    if (Math.abs(r - cur) <= 1)
                        return;
                    body.classList.toggle("sw-dlg-reserved", r > 0);
                    body.style.setProperty("--sw-dlg-reserve", r + "px");
                };
                const fields = Array.from(body.querySelectorAll(FieldSel)).filter(f => f.getClientRects().length > 0);
                const work = hasWork();
                if (!((autoDirty && fields.length) || work || explicitModal || o.NoEscape || o.Close === false)) {
                    set(0);
                    return;
                }
                const low = window.innerHeight <= 500;
                const rcs = getComputedStyle(rejectE);
                const lh = parseFloat(rcs.lineHeight) || (parseFloat(rcs.fontSize) || 13) * 1.35;
                //  The lines this dialog can show (Designer S5b N2: a made-up sample, the stop label of the dialog title and twice the
                //  longest label, gave a third line nobody saw, ~20 px of empty band from the opening): the real texts of the line with
                //  this dialog's labels (its idle lines; when it may run work, the busy lines for each button that can run it, its label is
                //  what runs, and each way out), each measured in a laid out but invisible copy of the line (the theme's font, letter
                //  spacing and wrapping: exact), the tallest one counts; one line in a low window (it ellipsizes there)
                const samples = ["M"];
                if (!low) {
                    const labs = footBtns().map(b => b.textContent.trim()).filter(x => x);
                    samples.push(_TF("Still working: please wait until it is done", "The rejection line of a busy dialog that has no way out yet (a close attempt was refused)"),
                        _TF("Still working: you can change the fields again when it is done", "Line of a busy dialog when the user tries to change a field while its work runs"));
                    //  (the idle lines: unsaved changes, a dialog that closes only with its buttons; every pair of labels)
                    for (const a of labs) {
                        samples.push(_T("Unsaved changes: use {0}", a, "The rejection line of a dialog with unsaved changes after a close attempt. {0}: the label of the button to use, ex: Save"),
                            _T("Use {0}", a, "The rejection line of a dialog that only closes with its button, after a close attempt. {0}: the button label, ex: OK"));
                        for (const c of labs)
                            if (a !== c)
                                samples.push(_T("Unsaved changes: use {0} or {1}", a, c, "The rejection line of a dialog after a close attempt. {0}, {1}: the visible labels of the buttons to use, ex: Save, Cancel"),
                                    _T("Use {0} or {1}", a, c, "The rejection line of a dialog that only closes with its buttons, after a close attempt (an outside click, Escape). {0}, {1}: the button labels, ex: Cancel, Save"));
                    }
                    if (work) {
                        const runs = footBtns().filter(b => b.SwDialogButton && !b.SwDialogButton.Dismiss).map(b => b.textContent.trim()).filter(x => x);
                        if (o.Reserve || !runs.length)
                            runs.push(busyWhat() || o.Title || "");
                        const outs = footBtns().filter(b => b.SwDialogButton && b.SwDialogButton.Dismiss).map(b => b.textContent.trim()).filter(x => x);
                        for (const w of runs) {
                            const stop = w ? _T("Stop waiting ({0} may still finish)", w, "Button of a busy dialog whose work can't be cancelled (a request was sent): closes it without waiting; the work may still be done. {0}: what runs, ex: Save")
                                : _TF("Stop waiting (the work may still finish)", "Button of a busy dialog whose work can't be cancelled: closes it without waiting; the work may still be done");
                            samples.push(_T("Still working: use {0}", stop, "The rejection line of a busy dialog after a close attempt. {0}: the label of the button to use, ex: Cancel"));
                            for (const c of outs)
                                samples.push(_T("Still working: use {0} or {1}", stop, c, "The rejection line of a dialog after a close attempt. {0}, {1}: the visible labels of the buttons to use, ex: Save, Cancel"), _T("Still working: use {0} or {1}", c, stop, "The rejection line of a dialog after a close attempt. {0}, {1}: the visible labels of the buttons to use, ex: Save, Cancel"));
                            if (w)
                                samples.push(_T("{0} is running: you can change the fields again when it is done", w, "Line of a busy dialog when the user tries to change a field while its work runs (the fields are read only then, so nothing typed is lost). {0}: what runs, ex: Save"),
                                    _T("Stopping {0}: the dialog closes when it has stopped", w, "Line of a dialog after Cancel while its work is being stopped (a close attempt now). {0}: what runs, ex: Save"));
                        }
                    }
                }
                //  (measured laid out but invisible: the status row may be closed, 0 high with its content clipped)
                const m = el("div.sw-dialog-reject", { attrs: { "aria-hidden": "true" } });
                Object.assign(m.style, { position: "absolute", visibility: "hidden", left: "0", right: "0", top: "0" });
                rejectE.parentNode.append(m);
                let line = 0;
                for (const s of samples) {
                    m.textContent = s;
                    line = Math.max(line, m.offsetHeight);
                }
                /* SwWeb:allow ux-mot-1 (a measuring copy, never shown) */
                m.remove();
                //  (+ 4 px: sub-pixel rounding of the rows never takes the last pixels from the body, so the footer never moves by 1 to 3 px)
                let need = line + (parseFloat(rcs.marginTop) || 0) + (parseFloat(rcs.marginBottom) || 0) + 4;
                if (work) {
                    const em = parseFloat(getComputedStyle(foot || box).fontSize) || 14;
                    const sheet = isSheetLayout();
                    let free = 0;
                    if (foot) {
                        const fr = foot.getBoundingClientRect();
                        const fcs = getComputedStyle(foot);
                        let lm = fr.right - (parseFloat(fcs.paddingRight) || 0), bot = -Infinity;
                        const bs = footBtns().filter(b => !isOff(b)).map(b => b.getBoundingClientRect()).filter(r => r.width);
                        bs.forEach(r => bot = Math.max(bot, r.bottom));
                        bs.forEach(r => {
                            if (bot - r.bottom < 3)
                                lm = Math.min(lm, r.left);
                        });
                        free = lm - fr.left - (parseFloat(fcs.paddingLeft) || 0);
                    }
                    const ctrlH = foot ? Math.max(...footBtns().map(b => b.getBoundingClientRect().height), em * 2.5) : em * 2.5;
                    //  the width of the longest label the busy footer may add (Stop waiting names the work: translations make it long)
                    let addW = em * 16;
                    try {
                        const cv = reserveRoom.Cv || (reserveRoom.Cv = document.createElement("canvas").getContext("2d"));
                        cv.font = getComputedStyle(foot || box).font;
                        //  (it names the running button's label, unknown yet: the longest one, a little too much rather than too little)
                        const lab = footBtns().map(b => b.textContent.trim()).reduce((a, x) => x.length > a.length ? x : a, "");
                        addW = cv.measureText(stopWaitLabel()).width + cv.measureText(lab).width + em * 2.5;
                    }
                    catch {
                    }
                    //  the status: a row of its own on sheets and where the button row has no room for it next to Stop waiting
                    if (sheet || free < Math.min(addW, em * 16) + em * 7)
                        need += Math.ceil(em * (low ? 1.3 : 1.5)) + 8;
                    //  the 10 s Close (no footer Cancel to take Stop waiting): a row of its own where the button row has no room
                    if (!footCancels().length && (sheet || free < addW))
                        need += ctrlH + 8;
                }
                //  what the body can give and still show its largest field whole (or two lines of text); the squeezed padding gives some
                const bcs = getComputedStyle(body);
                const pt = parseFloat(bcs.paddingTop) || 0, pb = parseFloat(bcs.paddingBottom) || 0;
                let keep = 0;
                for (const f of fields) {
                    const blk = f.closest(".sw-text, .sw-field, .sw-check, label") || f;
                    keep = Math.max(keep, (body.contains(blk) ? blk : f).getBoundingClientRect().height);
                }
                if (!fields.length)
                    keep = Math.min(Math.max(0, body.scrollHeight - pt - pb - cur), lh * 2);
                //  A body of one field (or none) gives only its empty space: a short body never gets a scroll bar when the line comes
                //  (D24: a one-field busy dialog scrolled its field once "Still working" showed); a longer body gives what it can
                //  while its largest field stays whole (it scrolls, as it would for its own content)
                const give = fields.length <= 1
                    ? Math.max(0, body.clientHeight - body.scrollHeight - 2)
                    : (body.clientHeight - cur) - keep - 8;
                let r = need - Math.max(0, give);
                //  (a short body has nothing to give if the line comes out taller than measured: a little more, never a scroll bar)
                if (fields.length <= 1)
                    r += 4;
                //  never past the window: at most up to the box's max height
                const maxH = parseFloat(getComputedStyle(box).maxHeight) || window.innerHeight;
                const bh = box.getBoundingClientRect().height - cur;
                set(Math.min(r, Math.max(0, Math.min(maxH, window.innerHeight) - bh)));
            };
            /**
             * Content that grows or shrinks while the dialog is shown (a field message, a lookup result) never moves the footer: a
             * centered dialog keeps its bottom edge and grows or shrinks at its top (sheets are anchored at the bottom already). A window
             * resize centers it again.
             */
            let lastBox = null;
            const keepBottom = () => {
                if (entry.Closed)
                    return;
                //  a relabeled footer button first gets its kept slot back (the box then has its real size: a label that grew for one
                //  layout never moves the dialog)
                keepSlots();
                const r = box.getBoundingClientRect();
                const prev = lastBox;
                lastBox = r;
                if (!prev || st.Held || !box.classList.contains("sw-shown") || isSheetLayout() || Math.abs(r.height - prev.height) < 0.5 ||
                    box.matches(".sw-placed, .sw-max, .sw-sized, .sw-full, .sw-dragging, .sw-mode, .sw-leaving"))
                    return;
                st.BottomKept = true;
                box.style.top = "auto";
                box.style.marginBottom = "0";
                box.style.bottom = Math.max(0, Math.min(Math.round(window.innerHeight - prev.bottom), Math.floor(window.innerHeight - r.height))) + "px";
                lastBox = box.getBoundingClientRect();
            };
            /**
             * Footer slots keep their width (LAY-1): a button that alternates labels (Labels: ["Cancel", "Close"]) gets the room of the
             * widest one when the dialog opens; a label that changes later never makes its button narrower or wider (it keeps its box,
             * a longer text wraps), so the buttons next to it never move (Admin N29: Cancel → Close, Continue ↔ Force). A window resize
             * lets the buttons follow their labels again.
             */
            const slotW = new Map();
            const slotCss = new Map();
            const slotsIdle = () => !footRef && foot && !box.matches(".sw-leaving");
            const keepSlots = () => {
                if (entry.Closed || !slotsIdle())
                    return;
                //  (layout widths: the opening animation scales the box, its rectangles are scaled then)
                const k = box.offsetWidth ? box.getBoundingClientRect().width / box.offsetWidth : 1;
                const btns = Array.from(foot.querySelectorAll(":scope > .sw-button:not(.sw-dialog-busybtn)"));
                //  only a LABEL change (of any footer button: on sheets the buttons share the row) keeps the slots; the same labels with
                //  other widths (the web font arrived, the declared labels were measured, the opening animation) are the new slots
                const relabeled = btns.some(b => {
                    const q = slotW.get(b);
                    return (q && q.T !== b.textContent) || slotCss.has(b);
                });
                for (const b of btns) {
                    const w = b.getBoundingClientRect().width / (k > 0.2 ? k : 1);
                    if (!w)
                        continue;
                    const q = slotW.get(b);
                    const p = q ? q.W : undefined;
                    if (p === undefined || Math.abs(w - p) < 0.5 || !relabeled) {
                        slotW.set(b, { W: w, T: b.textContent });
                        continue;
                    }
                    if (!slotCss.has(b))
                        slotCss.set(b, b.style.cssText);
                    if (w > p + 0.5) {
                        //  a longer label keeps the slot on one line with an ellipsis (never broken inside a word); its full text is the tool tip
                        b.classList.add("sw-dialog-slot-ellip");
                        if (!b.title)
                            b.title = b.textContent.trim();
                        devWarn("Sw.Dialog \"" + (o.Title || dlgId) + "\": the footer button \"" + b.textContent.trim() + "\" got a longer label; it keeps its slot (one line, cut with …). Declare every label it shows (Labels: [...]) so its slot fits the widest from the start (UX LAY-1, TXT-17).", "dlg-labels:" + (o.Title || dlgId));
                    }
                    b.style.flex = "0 0 " + p + "px";
                    b.style.width = p + "px";
                    b.style.minWidth = p + "px";
                    b.style.maxWidth = p + "px";
                }
            };
            //  (declared label widths: every label a footer button shows is measured on one line when the dialog opens; a footer Cancel of
            //  a dialog that runs work also fits the short "Stop waiting" that may take its slot, so that label never wraps or breaks)
            const slotLabels = () => {
                if (!foot)
                    return;
                const cancels = new Set(hasWork() ? footCancels() : []);
                for (const b of foot.querySelectorAll(":scope > .sw-button")) {
                    const d = b.SwDialogButton && b.SwDialogButton.Def;
                    const sb = d && d.Button;
                    const labels = (d && Array.isArray(d.Labels) ? d.Labels : []).concat(cancels.has(b) ? [shortStopLabel()] : []);
                    if (!d || !labels.length || !sb || typeof sb.SetText !== "function")
                        continue;
                    const ws = b.style.whiteSpace;
                    b.style.whiteSpace = "nowrap";
                    let w = b.getBoundingClientRect().width;
                    for (const t of labels) {
                        sb.SetText(t);
                        w = Math.max(w, b.getBoundingClientRect().width);
                    }
                    sb.SetText(b.SwDialogButton.Text);
                    b.style.whiteSpace = ws;
                    const fcs = getComputedStyle(foot);
                    const avail = foot.clientWidth - (parseFloat(fcs.paddingLeft) || 0) - (parseFloat(fcs.paddingRight) || 0);
                    b.style.minWidth = Math.ceil(avail > 0 ? Math.min(w, avail) : w) + "px";
                }
            };
            const onSlotResize = () => {
                for (const [b, css] of slotCss) {
                    b.style.cssText = css;
                    b.classList.remove("sw-dialog-slot-ellip");
                }
                slotCss.clear();
                slotW.clear();
                lastBox = null;
                if (!st.Held && st.BottomKept) {
                    st.BottomKept = false;
                    ["top", "bottom", "margin-bottom"].forEach(p => box.style.removeProperty(p));
                }
                requestAnimationFrame(() => {
                    footWrap();
                    reserveRoom();
                });
            };
            window.addEventListener("resize", onSlotResize);
            st.OnClose.push(() => window.removeEventListener("resize", onSlotResize));
            /**
             * A low window (400 % zoom, a phone in landscape) whose footer buttons need more than one row (S6-D1, WCAG 1.4.10 reflow: the
             * footer never scrolls sideways, every button is on screen): the buttons wrap in compact rows (sw-foot-wrap: the small control
             * height, one line per label, the primary action last, at the bottom right), and when they would need more than two rows they
             * share two rows (sw-foot-grid2: two per row, a label too long for its half cut with … and its full text as the tool tip), so
             * the body keeps its room for the focused field.
             */
            const footWrap = () => {
                if (!foot || footRef)
                    return;
                foot.classList.remove("sw-foot-wrap", "sw-foot-grid2");
                if (window.innerHeight > 500)
                    return;
                const rows = () => {
                    const bots = footBtns().filter(b => !isOff(b)).map(b => b.getBoundingClientRect()).filter(r => r.width).map(r => Math.round(r.bottom));
                    return bots.filter((y, i) => bots.findIndex(z => Math.abs(z - y) <= 4) === i).length;
                };
                if (rows() <= 1)
                    return;
                foot.classList.add("sw-foot-wrap");
                if (rows() > 2)
                    foot.classList.add("sw-foot-grid2");
                //  (one line per label there: a label cut with … has its full text as the tool tip)
                for (const b of footBtns()) {
                    const t = b.querySelector(".sw-button-text");
                    if (t && t.scrollWidth > t.clientWidth + 1 && !b.title)
                        b.title = t.textContent.trim();
                }
            };
            const watchGeometry = () => {
                if (typeof ResizeObserver !== "function")
                    return;
                slotLabels();
                st.Ros = st.Ros || [];
                const bro = new ResizeObserver(() => {
                    if (!st.Held)
                        reserveRoom();
                });
                bro.observe(body);
                const kro = new ResizeObserver(keepBottom);
                kro.observe(box);
                const sro = new ResizeObserver(keepSlots);
                if (foot)
                    for (const b of foot.querySelectorAll(":scope > .sw-button"))
                        sro.observe(b);
                st.Ros.push(bro, kro, sro);
            };
            const finish = () => {
                if (entry.Closed)
                    return;
                //  Touch: no text field gets the focus on open (it would pop the on-screen keyboard up unasked, FOC-2); the dialog
                //  (its title is read) or a non-text control does. TouchFocus: true keeps a field focus (a search picker).
                const touch = touchFirst() && o.TouchFocus !== true;
                let f = null;
                if (o.Focus)
                    f = typeof o.Focus === "string" ? box.querySelector(o.Focus) : o.Focus;
                if (f && touch && isTextEntry(f))
                    f = null;
                if (!f && !touch)
                    f = body.querySelector("input:not([type=hidden]), textarea, select");
                //  Safe default focus: a destructive action is never the default, Cancel (the first other button) is
                if (!f && box.querySelector(".sw-dialog-foot .sw-button.sw-danger, .sw-dialog-foot .sw-button.sw-danger-solid"))
                    f = box.querySelector(".sw-dialog-foot .sw-button:not(.sw-danger):not(.sw-danger-solid)");
                if (!f && !touch)
                    f = box.querySelector(".sw-dialog-foot .sw-button.sw-primary") ||
                        box.querySelector(".sw-dialog-foot .sw-button:last-child") || Focus.Tabbables(body)[0];
                //  Busy means locked: a dialog that opens busy focuses its way out (Cancel), never a button that doesn't act now
                if (st.Busy)
                    f = wayOut();
                describe();
                //  the opening state: the state elements' bases (code changes move them later); a field's base is taken on the user's
                //  first input on it; the values at the open are kept for Recheck (a restored draft)
                snapStates();
                snapStates(true, openState);
                body.querySelectorAll(FieldSel).forEach(f => {
                    if (tracked(f))
                        openVal.set(f, valueOf(f));
                });
                geo && geo.Restore();
                //  the room of the busy and close attempt states, reserved now (nothing grows later), and the stable footer / bottom edge
                //  (the slots fit their declared labels first: the footer row is measured with them)
                slotLabels();
                footWrap();
                reserveRoom();
                watchGeometry();
                //  the trap keeps the default focus, so a dialog opened while its frame has no focus (Focus.Set does
                //  nothing then) still focuses Cancel, not Close, when the focus arrives
                if (Focus.Initial)
                    Focus.Initial(box, f || null);
                Focus.Set(f || box);
                if (devMode())
                    debugStop = debugWatch(box, () => foot, o.Title || o.AriaLabel || dlgId, () => !!st.Held);
                o.OnShow && o.OnShow(close, box, body);
            };
            const c = o.Content;
            if (typeof c === "function") {
                const r = c(body, close, tools);
                if (r && typeof r.then === "function")
                    r.then(n => {
                        if (n instanceof Node)
                            body.append(n);
                        finish();
                    }, x => {
                        console.error(x);
                        Sw.Fail(x && x.message ? x.message : String(x));
                        close(null);
                    });
                else {
                    if (r instanceof Node)
                        body.append(r);
                    finish();
                }
            }
            else {
                if (typeof c === "string")
                    body.append(el("div.sw-dialog-text", { text: c }));
                else if (c)
                    body.append(c);
                finish();
            }
            return handle;
        },

        /**
         * @type {number} After how many ms a busy dialog without a way out adds the Close that names the consequence (default 10000, the
         * guide's 10 s rule; the dialog self-test page lowers it).
         */
        WayOutMs: 10000,

        /**
         * The label check of buttons (TXT-17: a label wraps only between words, never inside one; a label cut with … has its full text as
         * the tool tip or name). Developer mode runs it on every dialog footer / title bar change ("[Sw UX] … breaks inside a word");
         * the dialog self-test fails on it.
         * @param {ParentNode} [root=document] Where to look (every visible .sw-button in it).
         * @returns {string[]} The problems, ex: "the label \"Stop waiting\" breaks inside a word".
         */
        CheckLabels: root => labelProblems(root || document),

        /**
         * Show a dialog and wait for it to close.
         * @param {object} o Options (see Open).
         * @returns {Promise<*>} The value of the button that closed it (null when dismissed).
         */
        Show(o) {
            return Dialog.Open(o).Result;
        },

        /**
         * The open dialog on top, as a handle-like object ({ Box, Attempt(kind) }), or null.
         * @returns {{Box: HTMLElement, Attempt: function(string):Promise<boolean>}|null} The top dialog.
         */
        Top() {
            const e = topDialogEntry();
            return e ? { Box: e.Box, Attempt: k => e.Attempt(k || "x"), Handle: e.Handle || null } : null;
        },

        /**
         * The open dialog that holds a node (a field, a control's element), as its Open handle (OnClose, Own, Recheck, SetDirty ...), or
         * null when the node is not in an open Sw.Dialog. Components use it to bind to their dialog (ex: Sw.Draft clears its draft
         * when the user discards the dialog and disposes itself when it closes).
         * @param {Node} node The node.
         * @returns {object|null} The handle.
         */
        Of(node) {
            const b = node && node.nodeType ? (node.nodeType === 1 ? node : node.parentElement) : null;
            const box = b && b.closest ? b.closest(".sw-dialog") : null;
            return box && box.SwDialog && !box.classList.contains("sw-leaving") ? box.SwDialog : null;
        },

        /**
         * Test if a dialog that opened at or after a time is still open (Sw.Button: a click whose promise waits for a dialog is waiting
         * for the user, not working, FBK-2).
         * @param {number} t The time (Date.now()).
         * @returns {boolean} True when such a dialog is open.
         */
        OpenSince: t => stack.some(p => p.IsDialog && !p.Closed && p.Opened >= t),

        /**
         * Call a function whenever a dialog opens or closes.
         * @param {function():void} fn The function.
         * @returns {function():void} Removes it.
         */
        OnChange(fn) {
            dialogFns.add(fn);
            return () => dialogFns.delete(fn);
        },

        /**
         * The element live regions belong in (UX-POP-1): the open top dialog, else the body. Sw.Page.Announce's region is moved there
         * automatically while a dialog is open.
         * @returns {HTMLElement} The host.
         */
        LiveHost() {
            const e = topDialogEntry();
            return e ? e.Box : document.body;
        },

        /**
         * Ask the user to confirm something.
         * @param {string} title Title.
         * @param {string} [text] Text (selectable).
         * @param {string} [okText] Text of the OK button (default "OK").
         * @param {object} [o] Options: { CancelText, Danger (the OK button is a danger button and the title gets the warning icon, like
         * Sw.Cells.ConfirmDanger), Safe (the action is harmless and undoable: OK gets the focus and Enter confirms; without it Cancel has
         * the focus and Enter never confirms, UX-SAF-3), OkIcon, CancelIcon, OkTitle, CancelTitle, Icon,
         * Target (the item the action changes; with no title the title is made by DangerTitle(okText, Target), ex:
         * "Kill devenv (33936)?"), Id (the dialog id, default "sw.confirm"), Owner (a dialog handle / element: the confirmation closes with
         * that dialog) }.
         * The options form works too (like Show): Confirm({ Title, Text, OkText, ...the options above }) (FD F9: an options object as the
         * title showed "[object Object]").
         * @returns {Promise<boolean>} True if confirmed.
         */
        Confirm(title, text, okText, o) {
            if (title && typeof title === "object" && !(title instanceof Element) && text === undefined && okText === undefined && o === undefined) {
                //  (checked as the options form: an unknown name lists Title, Text and OkText too, FD S5b N2)
                checkOpts("Sw.Dialog.Confirm", title, "Sw.Dialog.Confirm({...})");
                const { Title, Text, OkText, ...rest } = title;
                title = Title;
                text = Text;
                okText = OkText;
                o = Object.keys(rest).length ? rest : undefined;
            }
            else
                checkOpts("Sw.Dialog.Confirm", o);
            const p = confirmImpl(title, text, okText, o);
            p.SwDialog = true;
            return p;
        },

        /**
         * Show a message with a button that closes it.
         * @param {string} title Title.
         * @param {string} [text] Text.
         * @param {string} [okText] Text of the button (default "Close").
         * @returns {Promise<void>} Resolves when closed.
         */
        Alert(title, text, okText) {
            const p = alertImpl(title, text, okText);
            p.SwDialog = true;
            return p;
        },

        /**
         * Ask the user for a text.
         * @param {string} title Title.
         * @param {string} [value] Initial value.
         * @param {string} [placeholder] Placeholder.
         * @param {object} [o] Options: { Text (a description above the input), Label, Multiline, Type (input type), Validate(value) -> error | null,
         * OkText, CancelText, Mono, Id }.
         * @returns {Promise<string|null>} The (trimmed) text, or null if cancelled.
         */
        Prompt(title, value, placeholder, o) {
            checkOpts("Sw.Dialog.Prompt", o);
            const p = promptImpl(title, value, placeholder, o);
            p.SwDialog = true;
            return p;
        },

        /**
         * Ask the user for several values: a form of Sw.PropertyEditor definitions (Type "string" for texts, NoSlider for unbounded
         * numbers, Help for a line under a field).
         * @param {string} title Title.
         * @param {object[]} defs The fields (PropertyEditor definitions).
         * @param {object} [values] The initial values ({ key: value }, missing ones show their Default).
         * @param {object} [o] Options: { Text (a description above the fields), OkText, CancelText, Id, Wide,
         * Validate(values) -> null | { key: message } (shown under the fields) | "message" (a toast), checked on OK and Enter,
         * OnOk(values) -> the work of OK (may return a promise: the dialog is busy; it stays open when it returns false or { key: message },
         * a thrown error is shown), OnChange(key, value, values, editor) (ex: to enable fields), OnReady(editor, values, form) (the form is built: ex: disable fields; form.SetOk(enabled, reason) enables / disables OK, ex: nothing to write; form.Set(values) sets fields as if typed, ex: a one click fix), After (an element under the fields, ex: a preview of the result) }.
         * @returns {Promise<object|null>} All values (the defaults filled in), or null if cancelled.
         */
        Form(title, defs, values, o) {
            checkOpts("Sw.Dialog.Form", o);
            const p = formImpl(title, defs, values, o);
            p.SwDialog = true;
            return p;
        },

        /**
         * The short name of an item for a dialog title: an item text of "Label: value" parts (Sw.Cells.ItemName, ex:
         * "Name: devenv, Id: 33936") becomes "devenv (33936)" (the first text value, then the first number); any other text is returned
         * trimmed (at most 60 characters).
         * @param {string} item The item text.
         * @returns {string} The short name ("" for none).
         */
        ShortTarget(item) {
            const s = String(item ?? "").trim();
            if (!s)
                return "";
            const parts = s.split(/,\s+/).map(p => {
                const m = /^[^:]{1,40}:\s+(.+)$/.exec(p);
                return m ? m[1].trim() : null;
            });
            let r = s;
            if (parts.length && parts.every(p => p)) {
                const num = parts.find(p => /^[\d\s.,:-]+$/.test(p));
                const name = parts.find(p => p !== num);
                r = name && num ? name + " (" + num + ")" : (name || num);
            }
            return r.length > 100 ? r.substring(0, 99) + "…" : r;
        },

        /**
         * The title of a destructive confirmation, naming the action and its target (ui/README "Dialog rules"):
         * "Kill devenv (33936)?", "Delete LogFile.txt?". The action is the danger button's verb; the target is an item text
         * (see ShortTarget). Without a target: "Kill?"; without an action: "Continue?".
         * With o.Name the target is the name of the row the action is about (ex: the user of a session), not the object of the verb:
         * an action of several words then reads "End session for ops?" (one word: "Kill devenv?").
         * @param {string} action The verb ("Kill", "End session").
         * @param {string} [target] The item ("Name: devenv, Id: 33936", "LogFile.txt").
         * @param {{Name: boolean}} [o] Options: Name (the target names the row, see above).
         * @returns {string} The title.
         */
        DangerTitle(action, target, o) {
            checkOpts("Sw.Dialog.DangerTitle", o);
            const a = String(action || "").replace(/[!?.…]+$/, "").trim() || _TF("Continue", "Fallback verb of a confirmation dialog title when the action has no name");
            const t = Dialog.ShortTarget(target);
            if (t && o && o.Name && /\s/.test(a))
                return _T("{0} for {1}?", a, t, "Title of the confirmation dialog of a destructive action about a named row.{0} is replaced with the action (ex: End session), {1} with the name of the row (ex: the user ops)");
            return t
                ? _T("{0} {1}?", a, t, "Title of the confirmation dialog of a destructive action.{0} is replaced with the action (ex: Kill), {1} with the item it changes (ex: devenv (33936))")
                : a + "?";
        },

        /**
         * A searchable selection (the old PopUpSelection): a filter box and a list of items produced by a callback for the current filter.
         * Items are elements (they handle their own clicks, call close(value) to finish) or data { Text, Hint, Icon, Value, Title }.
         * Enter picks the first item.
         * @param {object} o Options: { Title, Tip (tooltip of the title), Placeholder, StartText, GetAll (also call Items with an empty filter),
         * Items(filter, close) -> Array | Promise<Array>, Wide }.
         * @returns {Promise<*>} The picked value (null when dismissed).
         */
        Select(o) {
            o = o || {};
            checkOpts("Sw.Dialog.Select", o);
            const filter = new Sw.TextBox({
                Value: o.StartText || "", Type: "search", Clear: true, Icon: "search",
                Placeholder: o.Placeholder || o.Title || _TF("Type to filter", "Placeholder of a search box that filters a list of options"),
            });
            const list = el("div.sw-select-results", { attrs: { role: "listbox" } });
            let prev = null;
            let closeFn = null;
            const update = async () => {
                const search = filter.Get();
                if (!o.GetAll && !search) {
                    list.replaceChildren();
                    prev = search;
                    return;
                }
                if (search === prev)
                    return;
                prev = search;
                let r;
                try {
                    r = (await o.Items(search, closeFn)) || [];
                }
                catch (x) {
                    Sw.Fail(x && x.message ? x.message : String(x));
                    r = [];
                }
                if (filter.Get() !== search)
                    return;
                list.replaceChildren();
                for (const it of r) {
                    if (it instanceof Node) {
                        list.append(it);
                        continue;
                    }
                    const row = el("button.sw-select-result", { type: "button", title: it.Title || null, attrs: { role: "option" } },
                        it.Icon ? Icon(it.Icon) : null, el("span.sw-select-result-text", { text: it.Text ?? String(it.Value) }),
                        it.Hint ? el("span.sw-select-result-hint", { text: it.Hint }) : null);
                    row.addEventListener("click", ev => {
                        if (Sw.BadClick(ev))
                            return;
                        closeFn(it.Value);
                    });
                    list.append(row);
                }
                if (!r.length && search)
                    list.append(el("div.sw-list-empty", { text: _TF("Nothing found!", "A text that is displayed when the result of filtering some data yielded no results") }));
            };
            const debounced = Sw.Debounce(update, 120);
            filter.Input.addEventListener("input", debounced);
            filter.Input.addEventListener("keydown", e => {
                if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                    e.preventDefault();
                    const f = list.firstElementChild;
                    f && f.click();
                }
                else if (e.key === "ArrowDown") {
                    e.preventDefault();
                    const f = Focus.Tabbables(list)[0];
                    f && f.focus();
                }
            });
            list.addEventListener("keydown", e => {
                if (e.key !== "ArrowDown" && e.key !== "ArrowUp")
                    return;
                e.preventDefault();
                const all = Focus.Tabbables(list);
                const i = all.indexOf(document.activeElement);
                if (e.key === "ArrowUp" && i <= 0) {
                    filter.Input.focus();
                    return;
                }
                const n = all[clamp(i + (e.key === "ArrowDown" ? 1 : -1), 0, all.length - 1)];
                n && n.focus();
            });
            const h = Dialog.Open({
                Title: o.Title, Wide: o.Wide, Class: "sw-select-dialog", Id: o.Id || "sw.select",
                Content: el("div.sw-select-box", null, o.Tip ? el("div.sw-muted", { text: o.Tip }) : null, filter.E, list),
                Focus: filter.Input,
            });
            closeFn = h.Close;
            update();
            return h.Result;
        },

        /**
         * Show a busy "working" dialog (the old PopUpWorking): a spinner, the text and the time so far; a busy dialog (UX-POP-6), so a
         * close attempt gives the rejection cue. Ways out: Cancel (with o.Cancel), "Continue in background" (with o.Background, only
         * when the activity list Sw.Activity exists); with neither, a Close that names the consequence is added after 10 s.
         * @param {object|string} [o] Options { Title, Text, Icon (an extra icon below the text), Cancel() (the work can be stopped; the
         * dialog closes after it), Background (true, or a function({Promise, Cancel, Title}) that hands the job to the activity list),
         * Promise (the work: the dialog closes by itself when it settles), Progress (0..1), CloseText (the 10 s Close, ex:
         * "Close (the upload stops)"), Stop() (called by that Close), Id, NoMove (no dialog menu: a short wait, ex: before a question) }, or (old signature) the title.
         * @param {string} [text] Old signature: the text.
         * @param {string} [icon] Old signature: the icon.
         * @returns {function():void} Closes the dialog; it also has Handle (the Sw.Dialog.Open handle), Result, SetText(text) (a phase
         * change, announced) and SetProgress(value, text).
         */
        Working(o, text, icon) {
            if (typeof o !== "object" || o === null)
                o = { Title: o, Text: text, Icon: icon };
            else
                checkOpts("Sw.Dialog.Working", o);
            const textE = el("div.sw-working-text.sw-selectable", { text: o.Text || "" });
            textE.hidden = !o.Text;
            const content = el("div.sw-working-box", null,
                new Sw.Spinner({ Size: "4em", Title: o.Title || _TF("Working", "Accessible name of a busy indicator") }).E,
                textE,
                o.Icon ? Icon(o.Icon, "sw-working-icon") : null);
            const h = Dialog.Open({ Title: o.Title, Content: content, NoEscape: true, Modal: true, Class: "sw-working-dialog", Id: o.Id || "sw.working", Reserve: true, Describe: textE, NoMove: !!o.NoMove });
            //  Work({ Signal, Progress }): the dialog starts the work; reading Signal makes it cancellable (Cancel aborts it)
            let abort = null, early = null, fn = null;
            if (typeof o.Work === "function") {
                const ac = typeof AbortController === "function" ? new AbortController() : null;
                let used = false;
                let p;
                try {
                    p = o.Work({
                        get Signal() {
                            used = true;
                            return ac ? ac.signal : undefined;
                        },
                        Progress: (v, t) => fn ? fn.SetProgress(v, t) : (early = [v, t]),
                    });
                }
                catch (x) {
                    p = Promise.reject(x);
                }
                o.Promise = p && typeof p.then === "function" ? p : Promise.resolve(p);
                if (used && ac)
                    abort = () => ac.abort();
            }
            const hasP = !!(o.Promise && typeof o.Promise.then === "function");
            h.SetBusy(o.Promise || true, {
                Cancel: o.Cancel, Abort: abort || undefined,
                //  a running promise can go on in the background by default (offered only where the activity list exists)
                Background: o.Background ?? (hasP ? true : undefined),
                Text: o.Text, ShowText: false, Progress: o.Progress, CloseText: o.CloseText, Stop: o.Stop, Title: o.Title, Key: o.Key,
            });
            fn = () => h.Close(null);
            fn.Handle = h;
            fn.Result = h.Result;
            fn.SetText = t => {
                textE.textContent = t || "";
                textE.hidden = !t;
                h.SetProgress(o.Progress === undefined ? null : o.Progress, t || "");
            };
            fn.SetProgress = (v, t) => {
                o.Progress = v;
                if (t !== undefined) {
                    textE.textContent = t || "";
                    textE.hidden = !t;
                }
                h.SetProgress(v, t);
            };
            if (early)
                fn.SetProgress(early[0], early[1]);
            if (o.Promise && typeof o.Promise.then === "function")
                o.Promise.then(fn, fn);
            return fn;
        },
    };

    // ------------------------------------------------------------------ searchable selection with recents

    /**
     * A reusable, paged, searchable selection dialog that remembers the last filter and the recently selected items
     * (the old SearchablePoupSelection; same local storage key "SysWeaver.PopUpRecent.<Id>", json { Filter, Recent }).
     */
    class SearchSelection {
        /**
         * @param {object} o Options: { Id (unique, part of the storage key), GetId(data) (the id stored in the recent list),
         * Render(data, select, isRecent) -> element (must call select(data) when chosen; default: a button with data.Text / data.Name),
         * Find(filter, recentMap, count, offset) -> Promise<data[]> (recentMap: id -> 1 based recency), MaxRecent (default 10) }.
         * Old positional arguments (id, getRecentIdFn, createElementFn, findFn, maxRecent) are accepted too.
         */
        constructor(o) {
            if (typeof o !== "object" || o === null)
                o = { Id: arguments[0], GetId: arguments[1], Render: arguments[2], Find: arguments[3], MaxRecent: arguments[4] };
            this.O = o;
            this.Key = "SysWeaver.PopUpRecent." + o.Id;
            this.MaxRecent = o.MaxRecent || 10;
        }

        /**
         * Load the stored state.
         * @returns {{Filter: string, Recent: Array}} The state.
         */
        Load() {
            let s = null;
            try {
                s = JSON.parse(localStorage.getItem(this.Key) || "null");
            }
            catch {
            }
            const st = { Filter: "", Recent: [] };
            if (s && typeof s === "object") {
                if (typeof s.Filter === "string")
                    st.Filter = s.Filter;
                if (Array.isArray(s.Recent))
                    st.Recent = s.Recent.slice(0, this.MaxRecent);
            }
            return st;
        }

        /**
         * Store the state.
         * @param {{Filter: string, Recent: Array}} st The state.
         */
        Save(st) {
            try {
                localStorage.setItem(this.Key, JSON.stringify(st));
            }
            catch {
            }
        }

        /**
         * Show the selection.
         * @param {object|string} [so] Options { Title, Tip, Placeholder, PageSize (default 20), OnSelect(data) }, or (old signature) the title
         * followed by onSelect, maxView, titleToolTip, filterPlaceholder.
         * @returns {Promise<*>} The selected data (null when dismissed).
         */
        Show(so) {
            if (typeof so !== "object" || so === null)
                so = { Title: arguments[0], OnSelect: arguments[1], PageSize: arguments[2], Tip: arguments[3], Placeholder: arguments[4] };
            const t = this;
            const o = t.O;
            const pageSize = so.PageSize || 20;
            const state = t.Load();
            const recentMap = new Map();
            state.Recent.forEach((id, i) => recentMap.set(id, i + 1));
            const filter = new Sw.TextBox({
                Value: state.Filter, Type: "search", Clear: true, Icon: "search",
                Placeholder: so.Placeholder || _TF("Enter some text to filter", "The placeholder text of an input field that is used for filtering some items"),
            });
            const data = el("div.sw-select-results", { attrs: { role: "listbox" } });
            let closeFn = null;
            let offset = 0, lastFilter = null, lastOffset = -1, top = null, more = null;
            const select = async val => {
                if (so.OnSelect) {
                    try {
                        await so.OnSelect(val);
                    }
                    catch (x) {
                        console.error(x);
                    }
                }
                try {
                    const id = o.GetId(val);
                    const r = state.Recent;
                    const ri = r.indexOf(id);
                    if (ri >= 0)
                        r.splice(ri, 1);
                    r.unshift(id);
                    if (r.length > t.MaxRecent)
                        r.length = t.MaxRecent;
                    t.Save(state);
                }
                catch {
                }
                closeFn(val);
            };
            const render = (d, isRecent) => {
                if (o.Render)
                    return o.Render(d, select, isRecent);
                const b = el("button.sw-select-result", { type: "button", attrs: { role: "option" } },
                    isRecent ? Icon("history", "sw-select-recent") : null,
                    el("span.sw-select-result-text", { text: d.Text ?? d.Name ?? String(d) }));
                b.addEventListener("click", () => select(d));
                return b;
            };
            const update = async () => {
                const f = filter.Get();
                if (lastFilter === f && lastOffset === offset)
                    return;
                if (lastOffset >= 0 && offset <= 0) {
                    state.Filter = f;
                    t.Save(state);
                }
                lastFilter = f;
                lastOffset = offset;
                let found = [];
                try {
                    found = (await o.Find(f, recentMap, pageSize + 1, offset)) || [];
                }
                catch (x) {
                    Sw.Fail(x && x.message ? x.message : String(x));
                }
                if (filter.Get() !== f)
                    return;
                if (offset <= 0) {
                    data.replaceChildren();
                    top = null;
                }
                if (more) {
                    more.remove();
                    more = null;
                }
                const gotMore = found.length > pageSize;
                const n = Math.min(found.length, pageSize);
                for (let i = 0; i < n; ++i) {
                    const d = found[i];
                    if (top === null)
                        top = d;
                    data.append(render(d, recentMap.has(o.GetId(d))));
                }
                if (gotMore) {
                    const b = new Sw.Button({
                        Text: _TF("Show more", "A button at the end of a list that loads more results"), Icon: "down", Kind: "ghost", Block: true,
                        Title: _TF("Get more results", "The tool tip description on a button that when clicked will view more results"),
                        OnClick: async () => {
                            offset += pageSize;
                            await update();
                        },
                    });
                    more = el("div.sw-select-end", null, b.E);
                }
                else
                    more = el("div.sw-select-end.sw-muted", {
                        text: (offset <= 0 && n === 0)
                            ? _TF("Nothing found!", "A text that is displayed when the result of filtering some data yielded no results")
                            : _TF("No more data!", "A text that is displayed at the end of a list of items when no more items is available"),
                    });
                data.append(more);
            };
            const debounced = Sw.Debounce(() => {
                offset = 0;
                update();
            }, 150);
            filter.Input.addEventListener("input", debounced);
            filter.Input.addEventListener("keydown", e => {
                if (e.key === "Enter" && Sw.IsPureEvent(e)) {
                    e.preventDefault();
                    if (top !== null)
                        select(top);
                }
                else if (e.key === "ArrowDown") {
                    e.preventDefault();
                    const f = Focus.Tabbables(data)[0];
                    f && f.focus();
                }
            });
            data.addEventListener("keydown", e => {
                if (e.key !== "ArrowDown" && e.key !== "ArrowUp")
                    return;
                e.preventDefault();
                const all = Focus.Tabbables(data);
                const i = all.indexOf(document.activeElement);
                if (e.key === "ArrowUp" && i <= 0) {
                    filter.Input.focus();
                    return;
                }
                const n = all[clamp(i + (e.key === "ArrowDown" ? 1 : -1), 0, all.length - 1)];
                n && n.focus();
            });
            const h = Dialog.Open({
                Title: so.Title, Class: "sw-select-dialog", Id: "sw.search." + o.Id,
                Content: el("div.sw-select-box", null, so.Tip ? el("div.sw-muted", { text: so.Tip }) : null, filter.E, data),
                Focus: filter.Input,
            });
            closeFn = h.Close;
            update();
            return h.Result;
        }
    }

    // ------------------------------------------------------------------ toasts

    /** @type {HTMLElement|null} The toast container. */
    let toastHost = null;

    /** @type {Element|null} Where the focus was when F6 moved it to the toasts (Toast.Focus): it goes back there when the last one closes. */
    let toastBack = null;

    /** Maximum number of toasts shown at once (the oldest is removed). */
    const MaxToasts = 3;

    /** Bars at the bottom of a page that toasts keep clear of (table pagers / footers, tool bars, navigation, or a data-sw-toastavoid element). */
    const ToastAvoid = "footer, [role=toolbar], [role=navigation], nav, .sw-pager, .sw-edit-formfoot, .sw-dialog-foot, .sw-sheet, [data-sw-toastavoid]";

    /** Surfaces that declare themselves (sheets, the activity sheet, data-sw-toastavoid): toasts sit above them however tall they are. */
    const ToastAvoidAlways = ".sw-sheet, [data-sw-toastavoid]";

    /** Actions that a toast must never cover (ex: the Save button of a form footer); any control of a fixed / sticky bar counts too. */
    const ToastAvoidControl = ".sw-button.sw-primary, .sw-button.sw-danger, .sw-button.sw-danger-solid, button[type=submit], input[type=submit]";

    /** Any control (counts when it is in a fixed / sticky bar at the bottom). */
    const ToastAnyControl = "button, [role=button], a[href], input, select, textarea, .sw-button";

    /**
     * The fixed or sticky bar at the bottom of the window that an element is in (any page's own sticky action bar counts,
     * not only the known classes).
     * @param {Element} e The element.
     * @param {number} h The height of the element's window.
     * @returns {Element|null} The bar.
     */
    function bottomBar(e, h) {
        const doc = e && e.ownerDocument;
        const win = (doc && doc.defaultView) || window;
        for (let i = 0; e && e !== doc.body && i < 8; ++i, e = e.parentElement) {
            const p = win.getComputedStyle(e).position;
            if (p !== "fixed" && p !== "sticky")
                continue;
            const r = e.getBoundingClientRect();
            return (r.height > 0 && r.height < h * 0.4 && r.bottom >= h - 40) ? e : null;
        }
        return null;
    }

    /**
     * The avoid zones of the toasts in this window's coordinates: every control a user can act on (Sw.Overlay's list; topmost in its own
     * document, so not behind a dialog's scrim there), every registered fixed overlay (Sw.Overlay.Register) and every bar at the
     * bottom the toasts keep clear of (a sheet or [data-sw-toastavoid] anywhere), in this document and in same-origin framed pages (3
     * levels). Only what lies in the toasts' column counts.
     * @param {HTMLElement} host The toast container.
     * @param {number} x0 The left edge of the column. @param {number} x1 Its right edge.
     * @returns {Array<{L: number, T: number, R: number, B: number}>} The zones.
     */
    function toastZones(host, x0, x1) {
        const out = [];
        const add = (r, ox, oy) => {
            const z = { L: r.left + ox, T: r.top + oy, R: r.right + ox, B: r.bottom + oy };
            if (z.R - z.L >= 2 && z.B - z.T >= 2 && z.R > x0 + 1 && z.L < x1 - 1)
                out.push(z);
        };
        const visit = (doc, ox, oy, vw, vh, depth) => {
            let all = [];
            try {
                all = doc.querySelectorAll(ActSel + ", " + ToastAvoid);
            }
            catch {
                return;
            }
            for (const c of all) {
                if (host.contains(c) || c.closest("[inert], [aria-hidden='true'], .sw-leaving, .sw-frame-catch, .sw-toasts"))
                    continue;
                const r = c.getBoundingClientRect();
                if (r.width < 2 || r.height < 2 || r.right + ox <= x0 || r.left + ox >= x1 || r.bottom <= 0 || r.top >= vh || r.right <= 0 || r.left >= vw)
                    continue;
                //  a bar counts when it touches the bottom edge (a sheet / data-sw-toastavoid surface anywhere); a control always
                const control = c.matches(ActSel);
                if (control && (isRegion(c, r, vw, vh) || inDataRow(c)))
                    continue;
                if (!control && !c.matches(ToastAvoidAlways) && r.bottom < vh - 40)
                    continue;
                //  topmost in its own document (a control under that document's dialog scrim is not reachable now)
                if (control) {
                    let hit = null;
                    try {
                        hit = doc.elementFromPoint(Math.min(Math.max((r.left + r.right) / 2, 0), vw - 1), Math.min(Math.max((r.top + r.bottom) / 2, 0), vh - 1));
                    }
                    catch {
                    }
                    if (hit && !(hit === c || c.contains(hit) || hit.contains(c) || host.contains(hit)))
                        continue;
                    //  the fixed / sticky bar at the bottom it is in (a table's pager bar): the whole bar, as the probes see it
                    const bar = r.bottom > vh * 0.5 ? (c.closest(ToastAvoidAlways) || c.closest(ToastAvoid) || bottomBar(c, vh)) : null;
                    const br = bar ? bar.getBoundingClientRect() : null;
                    if (br && br.height > 0 && br.height < vh * 0.4 && br.bottom >= vh - 40)
                        add(br, ox, oy);
                }
                add(r, ox, oy);
            }
            if (depth >= 3)
                return;
            for (const f of doc.querySelectorAll("iframe")) {
                let fd = null;
                try {
                    fd = f.contentDocument;
                }
                catch {
                    fd = null;
                }
                const r = f.getBoundingClientRect();
                if (fd && fd.documentElement && r.width >= 2 && r.height >= 2)
                    visit(fd, ox + r.left + f.clientLeft, oy + r.top + f.clientTop, f.clientWidth, f.clientHeight, depth + 1);
            }
        };
        visit(document, 0, 0, window.innerWidth, window.innerHeight, 0);
        //  an open pop-up (a menu, the shell's activity list) is never covered by a toast
        for (const p of document.querySelectorAll(".sw-popup:not(.sw-leaving)"))
            if (!p.contains(host))
                add(p.getBoundingClientRect(), 0, 0);
        //  the registered fixed overlays (an activity line, an offline line, a floating bar)
        for (const [e] of coverRegs)
            if (e !== host && e.isConnected && !host.contains(e))
                for (const p of [e].concat(Array.from(e.children)))
                    add(p.getBoundingClientRect(), 0, 0);
        return out;
    }

    /**
     * The place of the toast stack: the lowest lift (at least the lift the bars need) where the stack covers no avoid zone
     * (toastZones), else the top of the window when that is free, else the place that covers the fewest.
     * @param {HTMLElement} host The toast container.
     * @param {number} lift The lift the bars at the bottom need. @param {boolean} top At the top already (a tall surface below).
     * @param {number} left The column's left edge (no toast shown yet). @param {number} right Its right edge.
     * @param {number} w The window width. @param {number} h The window height.
     * @returns {{Lift: number, Top: boolean}} The place.
     */
    function toastSpot(host, lift, top, left, right, w, h) {
        const shown = Array.from(host.children).filter(t => t.classList.contains("sw-toast") && !t.classList.contains("sw-hide"));
        const hr = host.getBoundingClientRect();
        const cur = parseFloat(host.style.getPropertyValue("--sw-toast-lift")) || 0;
        const atTop = host.classList.contains("sw-toasts-top");
        let stackH = 0;
        if (shown.length) {
            const rs = shown.map(t => t.getBoundingClientRect());
            stackH = Math.max(...rs.map(r => r.bottom)) - Math.min(...rs.map(r => r.top));
        }
        //  (a toast about to be shown: about one line)
        stackH = Math.max(stackH, 56);
        //  the stack's bottom edge at a lift (measured from where it is now: right in any containing block)
        const margin = w <= 760 ? 12 : 16;
        const bottomAt = L => (shown.length && !atTop ? hr.bottom + cur : h - margin) - L;
        const x0 = hr.width ? hr.left : left, x1 = hr.width ? hr.right : right;
        let zones;
        try {
            zones = toastZones(host, x0, x1);
        }
        catch {
            zones = [];
        }
        const covers = (t0, t1) => zones.reduce((n, z) => n + (z.T < t1 - 1 && z.B > t0 + 1 ? 1 : 0), 0);
        const topSpan = () => {
            const t0 = shown.length && atTop ? hr.top : 12;
            return [t0, t0 + stackH];
        };
        const base = { Lift: top ? 0 : lift, Top: top, N: top ? covers(...topSpan()) : covers(bottomAt(lift) - stackH, bottomAt(lift)) };
        if (!zones.length || base.N === 0)
            return base;
        let best = base;
        if (!top) {
            //  candidates: above each zone in the column with the usual margin (lowest first; the same place the bar probes give)
            const cands = Array.from(new Set(zones.map(z => Math.ceil(bottomAt(0) - z.T + margin + 4)).filter(L => L > lift))).sort((a, b) => a - b);
            for (const L of cands) {
                const b = bottomAt(L);
                if (b - stackH < 8)
                    break;
                const n = covers(b - stackH, b);
                if (n < best.N)
                    best = { Lift: L, Top: false, N: n };
                if (n === 0)
                    return best;
            }
        }
        const tn = covers(...topSpan());
        if (tn < best.N)
            best = { Lift: 0, Top: true, N: tn };
        return best;
    }

    /**
     * Place the toasts so they never cover the controls a user needs right then (the placement rule in ui/README.md):
     * sets --sw-toast-lift on the container (or, without room above a tall surface, puts the toasts at the top: sw-toasts-top).
     * 1. Above a bar at the bottom of the window: a table pager / footer, a tool bar, a form footer, a dialog footer, or any fixed /
     *    sticky bar there (probed along the bottom edge), and above a sheet or a [data-sw-toastavoid] surface however tall it is (the
     *    activity sheet on phones).
     * 2. Above any control (Save, a pager button, a link) in the toasts' own column near the bottom (probed in that column).
     * The probes look into same-origin framed pages too (the app shell's toasts keep clear of a framed table's pager). Only the
     * topmost element at each probed point counts.
     */
    function placeToasts() {
        const host = toastHost;
        if (!host || !host.isConnected)
            return;
        const w = window.innerWidth, h = window.innerHeight;
        let lift = 0, top = false;
        /**
         * The topmost element at a point that is not a toast, looking into same-origin frames.
         * @param {number} x The x of the point in this window.
         * @param {number} y The y of the point in this window.
         * @returns {{E: Element, OY: number, H: number}|null} The element, the vertical offset of its document and its window height.
         */
        const at = (x, y) => {
            let doc = document, ox = 0, oy = 0, hh = h;
            for (let d = 0; d < 4; ++d) {
                let e = null;
                try {
                    e = doc.elementsFromPoint(x - ox, y - oy).find(n => !host.contains(n)) || null;
                }
                catch {
                    return null;
                }
                if (!e)
                    return null;
                if (e.tagName === "IFRAME") {
                    let fd = null;
                    try {
                        fd = e.contentDocument;
                    }
                    catch {
                        fd = null;
                    }
                    if (fd && fd.documentElement) {
                        const r = e.getBoundingClientRect();
                        ox += r.left + e.clientLeft;
                        oy += r.top + e.clientTop;
                        hh = e.clientHeight;
                        doc = fd;
                        continue;
                    }
                }
                return { E: e, OY: oy, H: hh };
            }
            return null;
        };
        for (const fx of [0.5, 0.3, 0.7, 0.12, 0.88]) {
            for (const dy of [3, 14, 30]) {
                const p = at(w * fx, h - dy);
                if (!p || !p.E.closest)
                    continue;
                const always = p.E.closest(ToastAvoidAlways);
                const bar = always || p.E.closest(ToastAvoid) || bottomBar(p.E, p.H);
                if (!bar)
                    continue;
                const r = bar.getBoundingClientRect();
                if (r.height > 0 && (always || r.height < p.H * 0.4) && r.bottom >= p.H - 40)
                    lift = Math.max(lift, Math.ceil(h - (r.top + p.OY)));
            }
        }
        //  no room above a tall surface: the toasts go to the top of the window (they never cover it)
        if (lift > h - 140) {
            top = true;
            lift = 0;
        }
        //  The toasts' column (bottom right, full width on phones): no primary / danger action (or a control of a bottom bar) under a toast
        const phone = w <= 760;
        const right = phone ? w - 12 : w - 16;
        const left = phone ? 12 : Math.max(0, right - Math.min(416, w - 32));
        if (!top)
            for (let i = 0; i <= 5; ++i) {
                const x = left + (right - left) * i / 5;
                for (const dy of [6, 20, 36, 52, 68, 84]) {
                    const y = h - lift - dy;
                    if (y < h * 0.5)
                        break;
                    const p = at(x, y);
                    const e = p && p.E;
                    let c = e && e.closest ? e.closest(ToastAvoidControl) : null;
                    if (!c && e && e.closest) {
                        const any = e.closest(ToastAnyControl);
                        c = any && bottomBar(any, p.H) ? any : null;
                    }
                    if (!c)
                        continue;
                    const r = c.getBoundingClientRect();
                    if (r.height > 0 && r.top + p.OY > h * 0.5)
                        lift = Math.max(lift, Math.ceil(h - (r.top + p.OY)) + 4);
                }
            }
        //  3. The toasts are PLACED with every avoid zone (the check of Sw.Overlay drives the place, it does not only warn): the
        //  stack goes to the lowest place where it covers no control, no registered overlay and no bar, also in framed pages and
        //  while a pop-up is open (geometry, not what is on top now: a framed pager under the shell's open list counts)
        const placed = toastSpot(host, lift, top, left, right, w, h);
        toastCovers = placed.N > 0;
        lift = placed.Lift;
        top = placed.Top;
        host.classList.toggle("sw-toasts-top", top);
        //  (only a change: the cover check places the toasts too, and a style write would start that check again)
        if (host.style.getPropertyValue("--sw-toast-lift") !== lift + "px")
            host.style.setProperty("--sw-toast-lift", lift + "px");
        //  developer check: a toast in a dialog never covers that dialog's controls (only when a free place existed: a page's own notice
        //  shown over a dialog that fills the window, ex: a new mail over the composer, has none, and the place covering the fewest was taken)
        const td = topDialogEntry();
        if (td && td.Box.contains(host) && !(placed.N > 0)) {
            const tt = td.Box.querySelector(".sw-dialog-title");
            coverCheck(td.Box, td.Box.getAttribute("aria-label") || (tt ? tt.textContent : ""));
        }
    }

    /** @type {boolean} The last placement found no free place (it covers the fewest): the Sw.Overlay check of the toasts stays quiet. */
    let toastCovers = false;

    /** @type {number} The timer that places the toasts again while any is shown (a sheet or a pager can appear under them). */
    let toastWatch = 0;

    /** Place the toasts again every second while any is shown (cheap: a few hit tests). */
    function keepPlacingToasts() {
        if (toastWatch)
            return;
        toastWatch = setInterval(() => {
            if (!toastHost || !toastHost.isConnected || !toastHost.querySelector(".sw-toast:not(.sw-hide)")) {
                clearInterval(toastWatch);
                toastWatch = 0;
                return;
            }
            placeToasts();
        }, 1000);
    }

    /** True once the resize listener of placeToasts is added. */
    let toastResizeBound = false;

    /**
     * The time a toast stays (UX-FBK-8): 5 s + 60 ms per character, at least 10 s with an action (Undo); errors stay until closed.
     * A longer Duration given by the caller wins; true = 24 hours.
     * @param {string} text The text (with its details).
     * @param {string} kind The kind.
     * @param {object} o The toast options.
     * @returns {number} Milliseconds (Infinity: until closed).
     */
    function toastTime(text, kind, o) {
        if (kind === "error")
            return Infinity;
        let ms = 5000 + 60 * text.length;
        if (o.Action && o.Action.Text)
            ms = Math.max(ms, 10000);
        if (o.Duration === true)
            return 24 * 60 * 60 * 1000;
        if (o.Duration > ms)
            ms = o.Duration;
        return ms;
    }

    /** While the tab is hidden toast timers wait (a result toast is seen when the user comes back, FBK-4). */
    document.addEventListener("visibilitychange", () => {
        if (!toastHost)
            return;
        for (const t of toastHost.children)
            if (t.SwToast)
                t.SwToast.Run();
    });

    /**
     * Show a toast (a short message at the bottom, lifted above a bottom bar such as a table pager, see placeToasts). Identical messages
     * are merged (a counter, the timer restarts).
     * Clicking (or Enter on) a toast copies its text to the clipboard and removes it (unless NoCopy, then it's only removed).
     * Timing (UX-FBK-8): 5 s + 60 ms per character, at least 10 s with an Action, errors until closed; hovering or focusing it pauses
     * the timer, a hidden tab waits. Closed by its close button, a click, or on touch a sideways swipe. While a dialog is open the toasts
     * sit inside it (announced, in its Tab cycle; F6 moves between the dialog and its toasts). A toast never takes the focus.
     * @param {string} text The message.
     * Design v2: bottom right (full width on phones), an inverse surface with the status icon, a close button, at most 3 at once.
     * @param {object} [o] Options: { Kind ("error" | "ok" | "info" | "warning"), Duration (ms: the least time it stays; true = 24 hours), NoCopy, Title,
     * Action ({Text, OnClick(event)}: a button in the toast, ex: "Undo"; clicking it closes the toast), Details }.
     * @returns {{E: HTMLElement, Close: function():void}} The toast.
     */
    function Toast(text, o) {
        o = o || {};
        checkOpts("Sw.Toast", o);
        text = text === null || text === undefined ? "" : String(text);
        let fresh = false;
        if (!toastHost || !toastHost.isConnected) {
            toastHost = el("div.sw-toasts.sw-root", { attrs: { role: "region", "aria-label": _TF("Notifications", "Accessible name of the area where short messages (toasts) are shown") } });
            const td = topDialogEntry();
            (td ? td.Box : document.body).append(toastHost);
            fresh = true;
            //  toasts never cover actions (FBK-8): the generic geometry check watches them in developer mode
            //  (no free place: the stack took the place that covers the fewest, P3-A-10; not the caller's mistake, no developer warning)
            Overlay.Register(toastHost, { Name: "Toasts", Settle: 600, Owns: c => toastCovers || inDataRow(c) });
            //  (the check places the toasts first: placeToasts, F10)
            const reg = coverRegs.get(toastHost);
            if (reg)
                reg.Place = placeToasts;
        }
        if (!toastResizeBound) {
            toastResizeBound = true;
            window.addEventListener("resize", () => {
                clearTimeout(placeToasts.Timer);
                placeToasts.Timer = setTimeout(placeToasts, 150);
            });
        }
        placeToasts();
        //  "danger" / "success" are aliases of "error" / "ok" (one name per tone, ui/README "Naming rules")
        const kind = o.Kind === "danger" ? "error" : o.Kind === "success" ? "ok" : (o.Kind || "");
        const dur = toastTime(text + (o.Details ? String(o.Details) : ""), kind, o);
        //  Merge identical messages
        for (const ex of toastHost.children) {
            if (ex.SwToast && ex.SwToast.Text === text && ex.SwToast.Kind === kind && !ex.classList.contains("sw-hide")) {
                const s = ex.SwToast;
                s.Count++;
                s.CountE.textContent = "×" + s.Count;
                s.CountE.hidden = false;
                s.Restart(dur);
                return { E: ex, Close: s.Close };
            }
        }
        const countE = el("span.sw-toast-count");
        countE.hidden = true;
        const textE = el("span.sw-toast-text.sw-selectable", { text });
        const t = el("div.sw-toast", {
            tabIndex: 0,
            title: o.Title || (o.NoCopy ? _TF("Remove", "A tool tip for a message shown to the user")
                : _TF("Copy to the clipboard and remove", "A tool tip for a message shown to the user")),
            attrs: { role: kind === "error" ? "alert" : "status" },
        }, Icon(kind === "error" ? "warning" : kind === "ok" ? "check" : kind === "warning" ? "warning" : "info", "sw-toast-icon"), textE, countE);
        if (kind)
            t.classList.add("sw-" + kind);
        let timer = 0, closed = false, hovered = false, focused = false, started = 0, remaining = dur;
        //  Optional action (ex: "Undo") and the close button; clicks on them don't copy the text
        const stop = ev => ev.stopPropagation();
        //  keys on them: only the toast's own Enter / Space (copy and close) are kept from the toast; F6, Tab and Escape go on (F6 from a
        //  toast's button back to the dialog or page, never to the body: FOC-1, FBK-8)
        const stopKey = ev => {
            if (ev.key === "Enter" || ev.key === " ")
                ev.stopPropagation();
        };
        //  the technical details of a message for people (Sw.Fail of a failed request), folded; copied with the text
        if (o.Details) {
            const d = el("details.sw-toast-details", null,
                el("summary", { text: _TF("Details", "Summary of the folded technical details in an error message (toast)") }),
                el("pre", { text: String(o.Details) }));
            d.addEventListener("click", stop);
            d.addEventListener("keydown", stopKey);
            d.addEventListener("toggle", () => pause());
            textE.append(d);
        }
        if (o.Action && o.Action.Text) {
            //  the announcement names the action ("Deleted 3 rows. Undo available.")
            textE.append(el("span.sw-sr-only", { text: " " + _T("{0} available.", o.Action.Text, "Read by screen readers after a message (toast) that has an action button. {0}: the action, ex: Undo") }));
            const ab = new Sw.Button({
                Text: o.Action.Text, Kind: "ghost", Small: true, Class: "sw-toast-action",
                OnClick: (b, ev) => {
                    close();
                    return o.Action.OnClick ? o.Action.OnClick(ev) : undefined;
                },
            });
            ab.E.addEventListener("click", stop);
            ab.E.addEventListener("keydown", stopKey);
            t.append(ab.E);
        }
        const cb = new Sw.Button({
            Icon: "close", Kind: "ghost", Small: true, Class: "sw-toast-close",
            Title: _TF("Close", "Tool tip of the button that closes a short message (toast)"),
            OnClick: () => close(),
        });
        cb.E.addEventListener("click", stop);
        cb.E.addEventListener("keydown", stopKey);
        t.append(cb.E);
        /** Close the toast (fade out, then remove; the focus goes back into the page or dialog, never to the body). */
        const close = () => {
            if (closed)
                return;
            closed = true;
            clearTimeout(timer);
            const had = t.contains(document.activeElement);
            t.classList.add("sw-hide");
            if (had) {
                //  the next toast, else back into the dialog, else where the user was before moving to the toasts (F6), else the page:
                //  never the body (FOC-1)
                const td = topDialogEntry();
                const next = Array.from(toastHost.querySelectorAll(".sw-toast:not(.sw-hide)"))[0];
                const back = toastBack && toastBack.isConnected && !toastBack.inert && !toastBack.closest("[inert]") ? toastBack : null;
                const to = next || (td ? (Focus.Tabbables(td.Box).find(x => !toastHost.contains(x)) || td.Box)
                    : back || Focus.Tabbables(document.querySelector("main") || document.body).find(x => !toastHost.contains(x)) || null);
                Focus.Set(to);
            }
            //  the exit transition of the theme (UX-MOT-1), then removed
            Motion.Out(t, { Class: "sw-hide" });
        };
        /** Pause the timer (hover, focus, hidden tab, open details). */
        const pause = () => {
            if (!timer)
                return;
            clearTimeout(timer);
            timer = 0;
            remaining = Math.max(1500, remaining - (Date.now() - started));
        };
        /** Run the timer when nothing holds it. */
        const run = () => {
            if (closed)
                return;
            if (hovered || focused || document.hidden || remaining === Infinity) {
                pause();
                return;
            }
            if (timer)
                return;
            started = Date.now();
            timer = setTimeout(close, remaining);
        };
        const s = {
            Text: text, Kind: kind, Count: 1, CountE: countE, Close: close, Dur: dur, Run: run,
            Restart: d => {
                s.Dur = Math.max(d || 0, s.Dur);
                clearTimeout(timer);
                timer = 0;
                remaining = s.Dur;
                run();
            },
        };
        t.SwToast = s;
        let swiped = false;
        t.addEventListener("click", ev => {
            if (swiped) {
                swiped = false;
                return;
            }
            if (Sw.HasSelectionIn(t) || Sw.BadClick(ev))
                return;
            close();
            if (!o.NoCopy)
                Sw.Clipboard.Copy(o.Details ? text + "\n\n" + o.Details : text, { Toast: false });
        });
        t.addEventListener("keydown", ev => {
            if ((ev.key === "Enter" || ev.key === " ") && ev.target === t) {
                ev.preventDefault();
                t.click();
            }
            else if (ev.key === "Escape" && t.contains(ev.target)) {
                //  Escape on a focused toast (or its button) closes it (the dialog under it stays)
                ev.preventDefault();
                ev.stopPropagation();
                close();
            }
        });
        t.addEventListener("pointerenter", () => {
            hovered = true;
            run();
        });
        t.addEventListener("pointerleave", () => {
            hovered = false;
            run();
        });
        t.addEventListener("focusin", () => {
            focused = true;
            run();
        });
        t.addEventListener("focusout", ev => {
            if (t.contains(ev.relatedTarget))
                return;
            focused = false;
            run();
        });
        //  Touch: a sideways swipe closes (the close button is the single tap alternative, INP-20)
        t.addEventListener("pointerdown", e => {
            if (e.pointerType !== "touch" || (e.target.closest && e.target.closest("button, summary, details")))
                return;
            const sx = e.clientX, sy = e.clientY;
            let dx = 0, active = false;
            const move = ev => {
                if (ev.pointerId !== e.pointerId)
                    return;
                dx = ev.clientX - sx;
                if (!active && Math.abs(dx) > 10 && Math.abs(dx) > Math.abs(ev.clientY - sy)) {
                    active = true;
                    t.classList.add("sw-toast-swipe");
                }
                if (active)
                    t.style.setProperty("--sw-toast-dx", dx + "px");
            };
            const up = ev => {
                if (ev.pointerId !== e.pointerId)
                    return;
                t.removeEventListener("pointermove", move);
                t.removeEventListener("pointerup", up);
                t.removeEventListener("pointercancel", up);
                t.classList.remove("sw-toast-swipe");
                t.style.removeProperty("--sw-toast-dx");
                if (active) {
                    swiped = true;
                    setTimeout(() => swiped = false, 400);
                    if (Math.abs(dx) > 80)
                        close();
                }
            };
            t.addEventListener("pointermove", move);
            t.addEventListener("pointerup", up);
            t.addEventListener("pointercancel", up);
        });
        //  A just created region is announced more reliably when the toast arrives a moment later
        const add = () => {
            toastHost.append(t);
            const live = Array.from(toastHost.children).filter(x => !x.classList.contains("sw-hide"));
            if (live.length > MaxToasts)
                live[0].SwToast && live[0].SwToast.Close();
            s.Restart(dur);
            placeToasts();
            keepPlacingToasts();
        };
        if (fresh)
            setTimeout(add, 50);
        else
            add();
        return { E: t, Close: close };
    }

    /**
     * The toast region (for the app shell's F6 cycle, UX-FBK-8): the container while it shows a toast, else null.
     * @returns {HTMLElement|null} The region.
     */
    Toast.Region = () => toastHost && toastHost.isConnected && toastHost.querySelector(".sw-toast:not(.sw-hide)") ? toastHost : null;

    /**
     * Move the focus to the newest toast (F6, UX-FBK-8).
     * @returns {boolean} True if a toast got the focus.
     */
    Toast.Focus = () => {
        const r = Toast.Region();
        const ts = r ? Array.from(r.querySelectorAll(".sw-toast:not(.sw-hide)")) : [];
        const ae = document.activeElement;
        if (ts.length && ae && ae !== document.body && !r.contains(ae))
            toastBack = ae;
        return ts.length ? Focus.Set(ts[ts.length - 1]) : false;
    };

    /**
     * Show an error toast (the old Fail).
     * a failed request (an Error, a Sw.Net.Error, or a text with an HTTP status such as "Could not save: 404 - Not Found") is
     * shown as a message for people (Sw.Net.ErrorText: what failed, why, what to do), with the raw text folded under "Details" (copied
     * with it, and logged).
     * @param {string|Error} text The message or error (also logged with console.log, as the original Message).
     * @param {number|boolean} [duration] Ignored: an error toast stays until it is closed (UX-FBK-8; kept for the old signature).
     * @param {boolean} [noCopyOnClick] Clicking only removes the toast (no copy).
     * @returns {{E: HTMLElement, Close: function():void}} The toast.
     */
    function Fail(text, duration, noCopyOnClick) {
        console.log(text);
        //  Offline: the shell's one calm line says it (UX-FBK-9), no error toast per failed call
        const N = Sw.Net;
        if (N && N.Online === false && typeof N.IsNetworkError === "function" && N.IsNetworkError(text))
            return { E: el("div.sw-toast"), Close: () => { } };
        const h = Sw.Net && Sw.Net.ErrorText ? Sw.Net.ErrorText(text) : { Text: String(text ?? ""), Details: "" };
        return Toast(h.Text, { Kind: "error", Duration: duration ?? (h.Details ? 10000 : undefined), NoCopy: noCopyOnClick, Details: h.Details || undefined });
    }

    /**
     * Show an information toast (the old Info).
     * @param {string} text The message (also logged).
     * @param {number|boolean} [duration] The least time in ms (default 5 s + 60 ms per character, true = 24 hours).
     * @param {boolean} [noCopyOnClick] Clicking only removes the toast (no copy).
     * @returns {{E: HTMLElement, Close: function():void}} The toast.
     */
    function Info(text, duration, noCopyOnClick) {
        console.log(text);
        return Toast(text, { Kind: "info", Duration: duration, NoCopy: noCopyOnClick });
    }

    /**
     * Show a toast with an old message class (the old Message): "MsgError" = error, "MsgInfo" = info, else the class is the kind.
     * @param {string} text The message.
     * @param {number|boolean} [duration] Duration.
     * @param {string} [className] "MsgError" | "MsgInfo" | a kind.
     * @param {boolean} [noCopyOnClick] No copy on click.
     * @returns {{E: HTMLElement, Close: function():void}} The toast.
     */
    function Message(text, duration, className, noCopyOnClick) {
        //  (logged as the original Message: console.log for every kind)
        console.log(text);
        const kind = className === "MsgError" ? "error" : className === "MsgInfo" ? "info" : (className || "");
        return Toast(text, { Kind: kind, Duration: duration, NoCopy: noCopyOnClick });
    }

    // ------------------------------------------------------------------ hints (tooltips for touch and keyboard)

    /** @type {{Close: function}|null} The visible hint. */
    let hintShown = null;

    /**
     * Tooltips for users without a mouse: a long press (touch / pen) or keyboard focus shows the element's title in a bubble.
     */
    const Hint = {
        /**
         * Show a hint bubble next to an element.
         * @param {HTMLElement} e The element.
         * @param {string} [text] The text (default the element's title / aria-label).
         * @param {number} [ms] Auto hide after ms (default 2500, 0 = until Hide).
         */
        Show(e, text, ms) {
            Hint.Hide();
            text = text || e.getAttribute("title") || e.getAttribute("aria-label");
            if (!text)
                return;
            const b = el("div.sw-hint.sw-root", { text, attrs: { role: "tooltip" } });
            document.body.append(b);
            Place(b, e, { Side: "below", Align: "center", Gap: 6 });
            let timer = 0;
            const h = {
                Close: () => {
                    clearTimeout(timer);
                    Motion.Out(b);
                    if (hintShown === h)
                        hintShown = null;
                    document.removeEventListener("pointerdown", h.Close, true);
                },
            };
            hintShown = h;
            if (ms !== 0)
                timer = setTimeout(h.Close, ms || 2500);
            setTimeout(() => document.addEventListener("pointerdown", h.Close, true), 0);
        },
        /** Hide the visible hint. */
        Hide() {
            hintShown && hintShown.Close();
        },
        /**
         * Test if a hint bubble is visible (it takes the first Escape, UX-TIP-5 / POP-3).
         * @returns {boolean} True if shown.
         */
        IsShown: () => !!hintShown,
        /**
         * Attach hint behavior to an element (long press with touch / pen, keyboard focus).
         * @param {HTMLElement} e The element (the hint text is its title at show time, or text).
         * @param {string} [text] Fixed text.
         * @returns {function():void} Removes the handlers.
         */
        Attach(e, text) {
            let timer = 0, sx = 0, sy = 0, fired = false;
            const down = ev => {
                if (ev.pointerType === "mouse")
                    return;
                fired = false;
                sx = ev.clientX;
                sy = ev.clientY;
                clearTimeout(timer);
                timer = setTimeout(() => {
                    fired = true;
                    Hint.Show(e, text);
                }, 500);
            };
            const move = ev => {
                if (timer && Math.hypot(ev.clientX - sx, ev.clientY - sy) > 10)
                    clearTimeout(timer);
            };
            const up = () => clearTimeout(timer);
            const click = ev => {
                if (fired) {
                    fired = false;
                    ev.preventDefault();
                    ev.stopImmediatePropagation();
                }
            };
            let ftimer = 0;
            const focus = () => {
                clearTimeout(ftimer);
                //  No hint when a closing menu / popup gives the focus back to this element
                if (restoringFocus)
                    return;
                if (e.matches(":focus-visible"))
                    ftimer = setTimeout(() => {
                        if (document.activeElement === e)
                            Hint.Show(e, text, 0);
                    }, 700);
            };
            const blur = () => {
                clearTimeout(ftimer);
                Hint.Hide();
            };
            e.addEventListener("pointerdown", down);
            e.addEventListener("pointermove", move);
            e.addEventListener("pointerup", up);
            e.addEventListener("pointercancel", up);
            e.addEventListener("click", click, true);
            e.addEventListener("focus", focus);
            e.addEventListener("blur", blur);
            e.addEventListener("keydown", blur);
            //  Touch long press would show the browser's context menu / text selection callout
            e.addEventListener("contextmenu", ev => {
                if (fired)
                    ev.preventDefault();
            });
            return () => {
                e.removeEventListener("pointerdown", down);
                e.removeEventListener("pointermove", move);
                e.removeEventListener("pointerup", up);
                e.removeEventListener("pointercancel", up);
                e.removeEventListener("click", click, true);
                e.removeEventListener("focus", focus);
                e.removeEventListener("blur", blur);
                e.removeEventListener("keydown", blur);
            };
        },
    };

    // ------------------------------------------------------------------ loader overlay

    /**
     * The loading overlay (the visual part of Sw.Page.Loading, the old AddLoading visual): blocks pointer and keyboard input and the focus.
     */
    const Loader = {
        /**
         * Show a loading overlay.
         * @param {object} [o] Options: { Target (an element to cover, default the page), Text, Opaque (hide the content completely),
         * Icon (an icon / icon class instead of the spinner, "" = none), Delay (ms before it becomes visible; default 0 when Opaque, else 150) }.
         * @returns {function():void} Removes the overlay (safe to call more than once).
         */
        Show(o) {
            o = o || {};
            checkOpts("Sw.Loader.Show", o);
            const target = o.Target && o.Target !== document.body ? o.Target : null;
            const ov = el("div.sw-loader.sw-root", {
                tabIndex: -1,
                attrs: { role: "status", "aria-live": "polite", "aria-busy": "true", "aria-label": o.Text || _TF("Loading", "Accessible name of the loading indicator") },
            });
            if (o.Opaque)
                ov.classList.add("sw-opaque");
            if (target) {
                ov.classList.add("sw-local");
                if (getComputedStyle(target).position === "static")
                    target.classList.add("sw-loader-host");
            }
            const delay = o.Delay ?? (o.Opaque ? 0 : 150);
            ov.style.setProperty("--sw-loader-delay", delay + "ms");
            if (o.Icon !== "")
                ov.append(o.Icon ? Icon(o.Icon, "sw-loader-icon") : new Spinner({ Class: "sw-loader-icon" }).E);
            if (o.Text)
                ov.append(el("div.sw-loader-text", { text: o.Text }));
            const stop = ev => {
                ev.preventDefault();
                ev.stopPropagation();
            };
            ["pointerdown", "pointerup", "click", "dblclick", "contextmenu", "keydown", "keyup", "wheel"].forEach(n => ov.addEventListener(n, stop, n === "wheel" ? { passive: false } : undefined));
            (target || document.body).append(ov);
            //  Trap and focus only in the own document, and only take the focus when this document has it: the loader of an
            //  embedded page must not steal the focus from the parent page
            const release = target ? null : Focus.Trap(ov);
            if (!target)
                Focus.Set(ov);
            let done = false;
            return () => {
                if (done)
                    return;
                done = true;
                if (release)
                    release();
                Motion.Out(ov, {
                    Class: "sw-hide",
                    Done: () => {
                        if (target && !target.querySelector(":scope > .sw-loader"))
                            target.classList.remove("sw-loader-host");
                    },
                });
            };
        },
    };

    // ------------------------------------------------------------------ server block overlay

    /**
     * The texts and icons of the server states (looked up at show time so the translations are current).
     * @param {string} kind The message type ("server.pause", "server.restart", "server.shutdown", "server.error").
     * @returns {{Title: string, Text: string, Icon: string}} The texts.
     */
    function serverKind(kind) {
        switch (kind) {
            case "server.pause":
            case "pause":
                return {
                    Title: _TF("Service is paused.", "Displayed as the title on a connected web page if the web service is paused"),
                    Text: _TF("Waiting for service to resume.", "Displayed as the details pon a connected web page if the web service is paused"),
                    Icon: "pause",
                };
            case "server.restart":
            case "restart":
                return {
                    Title: _TF("Service is restarting.", "Displayed as the title on a connected web page if the web service is restarting"),
                    Text: _TF("Waiting for new service instance to start.", "Displayed as the details pon a connected web page if the web service is restarting"),
                    Icon: "refresh",
                };
            case "server.shutdown":
            case "shutdown":
                return {
                    Title: _TF("Service has been shut down.", "Displayed as the title on a connected web page if the web service has been shutdown"),
                    Text: _TF("Waiting for new service instance to start.", "Displayed as the details pon a connected web page if the web service has been shutdown"),
                    Icon: "power",
                };
        }
        return {
            Title: _TF("Service communication lost.", "Displayed as the title on a connected web page if the web service is not reachable anymore"),
            Text: _TF("Waiting for network or service to become available.", "Displayed as the details pon a connected web page if the web service is not reachable anymore"),
            Icon: "warning",
        };
    }

    /**
     * @type {{H: object, Timer: number, Start: number, Kind: string, Closed: boolean, ByCode: boolean, TextE: HTMLElement, TimeE: HTMLElement}|null}
     * The server block while the server is unavailable (Closed: the user closed its dialog, the server is still unavailable).
     */
    let block = null;

    /**
     * Keys typed in the server block never reach the page's own listeners on document / window: a page's keyboard shortcut (save,
     * delete ...) must not run against a paused or lost server, as in the original, which blocked input on the document (S6 F2).
     * Escape closes the dialog here (the overlay stack's handling) and stops too: one key, one action, the page's own Escape (close its
     * panel, clear a field) does not run as well (S7a-A1-5). Default actions of other keys are kept.
     * @param {KeyboardEvent} ev The event (bubbling out of the dialog).
     */
    function stopKeys(ev) {
        if (ev.type === "keydown")
            escapeTop(ev);
        ev.stopPropagation();
    }

    /**
     * The server block: a dialog shown while the server is unavailable (pause, restart, shutdown, connection lost), above every other
     * pop-up; the page behind it takes no input. Shown automatically on the core's "server.block" event (top windows), removed on
     * "server.unblock". It has no reload button: the core keeps polling and reloads the page by itself when the server is back as a
     * new instance (a restart may bring a new front end); the same instance after a network drop: no reload, the dialog just goes away.
     * Like every dialog it can be moved, resized and closed (×, Escape): closing only hides it (the user can read or copy the page);
     * the reload still happens. A block of another kind (ex: a restart after a pause) shows it again.
     */
    const ServerBlock = {
        /**
         * Show (or update) the server block.
         * @param {string|{Type: string, Title: string, Text: string, Icon: string, Time: Date}} kind A server message type ("server.pause",
         * "server.restart", "server.shutdown", "server.error"), or custom texts.
         */
        Show(kind) {
            const isObj = typeof kind === "object" && kind !== null;
            const k = isObj ? (kind.Type || kind.Kind || "") : String(kind || "");
            const info = serverKind(k);
            //  Texts from the core (translated there), the icon from the type (inline svg icons look the same in every theme)
            if (isObj) {
                if (kind.Title)
                    info.Title = kind.Title;
                if (kind.Text)
                    info.Text = kind.Text;
                if (kind.Icon && !k)
                    info.Icon = kind.Icon;
            }
            //  (the core's server.block already logged the title, once per window as the original)
            if (!isObj || !kind.Time)
                console.warn(info.Title);
            const start = isObj && kind.Time ? new Date(kind.Time).getTime() : Date.now();
            if (block && block.Kind !== k) {
                block.Start = start;
                block.Kind = k;
                //  closed by the user: another kind of block is news, show it again
                if (block.Closed) {
                    clearInterval(block.Timer);
                    block = null;
                }
            }
            //  closed by the user: it stays closed until the server is back (Hide) or another kind of block comes
            if (block && block.Closed)
                return;
            if (!block) {
                const textE = el("div.sw-serverblock-text", { text: info.Text });
                const timeE = el("div.sw-serverblock-time", { attrs: { "aria-hidden": "true" } });
                const content = el("div.sw-serverblock-card", null,
                    textE,
                    el("div.sw-serverblock-wait", null, new Spinner({ Class: "sw-serverblock-spinner" }).E, timeE),
                    el("div.sw-serverblock-note", {
                        text: _TF("This closes by itself when the service is back; the page reloads if the service was restarted. Close it to read or copy the page meanwhile.",
                            "Note on the server unavailable dialog: it goes away by itself when the server answers again (the page reloads when it is a restarted server); the user may close it to see the page behind it"),
                    }));
                const b = { H: null, Timer: 0, Start: start, Kind: k, Closed: false, ByCode: false, TextE: textE, TimeE: timeE };
                block = b;
                b.H = Dialog.Open({
                    Title: info.Title, Icon: info.Icon, Content: content, Id: "sw.serverblock", Role: "alertdialog", AutoDirty: false,
                    //  (a clean dialog: a press outside closes it like ×, no reserved rejection line)
                    Above: true, Class: "sw-serverblock", Describe: textE,
                    OnClose: () => {
                        clearInterval(b.Timer);
                        if (block === b && !b.ByCode)
                            b.Closed = true;
                    },
                });
                ["keydown", "keyup", "keypress"].forEach(n => b.H.Box.addEventListener(n, stopKeys));
            }
            else {
                const box = block.H.Box;
                const t = box.querySelector(".sw-dialog-title");
                if (t)
                    t.textContent = info.Title;
                const ic = box.querySelector(".sw-dialog-head > .sw-dialog-icon");
                if (ic)
                    ic.replaceWith(Icon(info.Icon, "sw-dialog-icon"));
                block.TextE.textContent = info.Text;
            }
            const b = block;
            //  the time since the server became unavailable as a steady clock (m:ss, h:mm:ss)
            const tick = () => {
                const s = Math.max(0, Math.floor((Date.now() - b.Start) / 1000));
                const h = Math.floor(s / 3600), m = Math.floor(s / 60) % 60, pad = v => ("0" + v).slice(-2);
                b.TimeE.textContent = (h ? h + ":" + pad(m) : m) + ":" + pad(s % 60);
            };
            clearInterval(b.Timer);
            tick();
            b.Timer = setInterval(tick, 1000);
        },
        /** Remove the server block (the server is back). */
        Hide() {
            if (!block)
                return;
            const b = block;
            block = null;
            clearInterval(b.Timer);
            if (!b.Closed) {
                b.ByCode = true;
                b.H.Close(null);
            }
        },
        /**
         * Test if the server block is shown (false while the user closed it).
         * @returns {boolean} True if shown.
         */
        IsShown: () => !!block && !block.Closed,
    };

    // ------------------------------------------------------------------ full screen

    /** Full screen helpers (the old FullScreen class and Ew.Fullscreen). */
    const FullScreen = {
        /**
         * Test if the page is in full screen, including an installed app running standalone / full screen (the old FullScreen.IsFull).
         * @returns {boolean} True if full screen.
         */
        IsFull() {
            if (document.fullscreenElement || document.webkitFullscreenElement)
                return true;
            try {
                if (window.matchMedia("(display-mode: fullscreen)").matches || window.matchMedia("(display-mode: standalone)").matches)
                    return true;
            }
            catch {
                return screen.width - window.outerWidth <= 16 && screen.height - window.outerHeight <= 16;
            }
            return false;
        },
        /**
         * Test if an element is shown full screen with the Fullscreen api.
         * @returns {boolean} True if document.fullscreenElement is set.
         */
        Is: () => !!(document.fullscreenElement || document.webkitFullscreenElement),
        /**
         * Enter full screen.
         * @param {Element} [element] The element (default the whole page).
         * @returns {Promise<boolean>} True on success.
         */
        async Enter(element) {
            const e = element || document.documentElement;
            if (FullScreen.Is())
                return true;
            const fn = e.requestFullscreen || e.webkitRequestFullscreen;
            if (!fn)
                return false;
            try {
                await fn.call(e);
                return true;
            }
            catch {
                return false;
            }
        },
        /**
         * Exit full screen.
         * @returns {Promise<void>}
         */
        async Exit() {
            try {
                if (document.fullscreenElement)
                    await document.exitFullscreen();
                else if (document.webkitFullscreenElement)
                    document.webkitExitFullscreen();
            }
            catch {
            }
        },
        /**
         * Toggle full screen.
         * @param {Element} [element] The element (default the whole page).
         * @returns {Promise<void>}
         */
        async Toggle(element) {
            if (FullScreen.Is())
                await FullScreen.Exit();
            else
                await FullScreen.Enter(element);
        },
        /**
         * Call a function when full screen is entered or left.
         * @param {function(boolean):void} fn Called with the new state.
         * @returns {function():void} Removes the handler.
         */
        OnChange(fn) {
            const h = () => fn(FullScreen.Is());
            document.addEventListener("fullscreenchange", h);
            document.addEventListener("webkitfullscreenchange", h);
            return () => {
                document.removeEventListener("fullscreenchange", h);
                document.removeEventListener("webkitfullscreenchange", h);
            };
        },
        /**
         * Enter full screen on the first touch (or any pointer with AnyPointer), double click toggles.
         * @param {Element} [element] The element (default the whole page).
         * @param {object} [o] Options: { AnyPointer }.
         */
        OnTouch(element, o) {
            o = o || {};
            document.addEventListener("pointerup", ev => {
                if (ev.pointerType === "touch" || o.AnyPointer)
                    FullScreen.Enter(element);
            });
            document.addEventListener("dblclick", () => FullScreen.Toggle(element));
        },
        /**
         * Toggle full screen with a tap / click in a corner of the page or an element (nothing visible is added).
         * @param {Element} [element] The element to show full screen (default the whole page).
         * @param {object} [o] Options: { Corner ("top-right" (default) | "top-left" | "bottom-right" | "bottom-left"), Size (fraction, default 0.2),
         * Area (an element: the corner of this element, only taps on it count), OnToggle(event) (called instead of toggling) }.
         */
        OnCorner(element, o) {
            o = o || {};
            const size = o.Size ?? 0.2;
            const corner = o.Corner || "top-right";
            const area = o.Area || null;
            const inside = ev => {
                const r = area ? area.getBoundingClientRect() : { left: 0, top: 0, width: window.innerWidth, height: window.innerHeight };
                if (r.width <= 0 || r.height <= 0)
                    return false;
                const x = (ev.clientX - r.left) / r.width, y = (ev.clientY - r.top) / r.height;
                if (x < 0 || x > 1 || y < 0 || y > 1)
                    return false;
                return (corner.endsWith("right") ? x >= 1 - size : x <= size) && (corner.startsWith("top") ? y <= size : y >= 1 - size);
            };
            const host = area || document;
            let down = null;
            host.addEventListener("pointerdown", ev => down = ev.button === 0 && inside(ev) ? { X: ev.clientX, Y: ev.clientY } : null);
            host.addEventListener("pointerup", ev => {
                if (down && inside(ev) && Math.hypot(ev.clientX - down.X, ev.clientY - down.Y) < 20) {
                    if (o.OnToggle)
                        o.OnToggle(ev);
                    else
                        FullScreen.Toggle(element);
                }
                down = null;
            });
        },
    };

    // ------------------------------------------------------------------ covers nothing: the geometry check of fixed overlays

    /** The elements a check looks at: everything a user can act on. */
    const ActSel = "button, a[href], input:not([type=hidden]), select, textarea, summary, [tabindex]:not([tabindex='-1']), [role=button], [role=link], [role=switch], [role=checkbox], [role=radio], [role=tab], [role=menuitem], [role=slider], [role=combobox], [contenteditable='true'], [contenteditable='']";
    /** @type {Map<Element, {Name: string, Owns: *, Settle: number, T: number, Mo: MutationObserver|null, Place: (function():void|undefined)}>} The registered overlays. */
    const coverRegs = new Map();
    let coverBound = false;

    /**
     * True for a large focusable REGION (a grid, a chart, a scroll area with tabindex: a fifth of the window or 40 % of its height), not a
     * control: an overlay over a part of it blocks nothing (its own controls are checked one by one). Fields, buttons and links always
     * count.
     * @param {Element} c The element. @param {DOMRect} r Its rectangle. @param {number} vw @param {number} vh Its window size.
     * @returns {boolean} A region.
     */
    function isRegion(c, r, vw, vh) {
        return !c.matches("input, textarea, select, button, a[href], [contenteditable], [role=button], [role=link], [role=switch], [role=checkbox], [role=radio], [role=slider], [role=combobox], [role=tab], [role=menuitem]")
            && (r.width * r.height > vw * vh * 0.2 || r.height > vh * 0.4);
    }

    /**
     * True for a control inside a data row of a table / grid (not its header row): rows are content that scrolls and the row's actions
     * are also in its menu and reached by keyboard, so a short-lived overlay (a toast) may lie over a row; the table's tool bar, header
     * and pager always count.
     * @param {Element} c The element.
     * @returns {boolean} In a data row.
     */
    function inDataRow(c) {
        const row = c.closest("tr, [role=row]");
        return !!row && !row.closest("thead, tfoot") && !row.querySelector("th, [role=columnheader]");
    }

    /**
     * True when an overlay owns an element (it may lie over it).
     * @param {*} owns An element, a selector, an array of those, or a function(element) → boolean.
     * @param {Element} c The element.
     * @returns {boolean} Owned.
     */
    function ownsEl(owns, c) {
        if (!owns)
            return false;
        if (Array.isArray(owns))
            return owns.some(x => ownsEl(x, c));
        if (typeof owns === "function")
            return !!owns(c);
        if (typeof owns === "string")
            return !!c.closest(owns);
        return owns.nodeType === 1 && (owns === c || owns.contains(c));
    }

    /**
     * The interactive elements a registered overlay covers now (the overlay is the topmost element at the middle of the overlap).
     * @param {Element} e The overlay. @param {object} rec Its registration.
     * @returns {string[]} The problems.
     */
    function coverProblems(e, rec) {
        if (!e.isConnected)
            return [];
        const cs = getComputedStyle(e);
        if (cs.display === "none" || cs.visibility === "hidden")
            return [];
        const out = [];
        //  the overlay's own visible parts (a toast host is an empty column; its toasts are the parts)
        const parts = [e].concat(Array.from(e.children)).map(x => x.getBoundingClientRect()).filter(r => r.width >= 1 && r.height >= 1);
        if (!parts.length)
            return out;
        for (const c of document.querySelectorAll(ActSel)) {
            if (e.contains(c) || c.contains(e) || ownsEl(rec.Owns, c) || c.closest("[inert], [aria-hidden='true'], .sw-leaving, .sw-frame-catch"))
                continue;
            const cr = c.getBoundingClientRect();
            if (cr.width < 2 || cr.height < 2 || isRegion(c, cr, window.innerWidth, window.innerHeight))
                continue;
            //  (the same rule as the toast placement, toastZones: a control under another layer at its middle, ex: a dialog's scrim
            //  that is leaving, can't be used now; F10: the placement skipped such a link, this check flagged it)
            const mid = document.elementFromPoint(Math.min(Math.max((cr.left + cr.right) / 2, 0), window.innerWidth - 1), Math.min(Math.max((cr.top + cr.bottom) / 2, 0), window.innerHeight - 1));
            if (mid && !(mid === c || c.contains(mid) || mid.contains(c) || e.contains(mid)))
                continue;
            for (const r of parts) {
                const x0 = Math.max(r.left, cr.left), x1 = Math.min(r.right, cr.right), y0 = Math.max(r.top, cr.top), y1 = Math.min(r.bottom, cr.bottom);
                if (x1 - x0 < 2 || y1 - y0 < 2)
                    continue;
                const hit = document.elementFromPoint((x0 + x1) / 2, (y0 + y1) / 2);
                if (hit && e.contains(hit)) {
                    out.push("\"" + rec.Name + "\" covers \"" + ((c.getAttribute("aria-label") || c.textContent || c.title || c.tagName) + "").trim().replace(/\s+/g, " ").slice(0, 40) + "\"");
                    break;
                }
            }
        }
        return out;
    }

    /**
     * Check one registered overlay now (developer mode) and warn about what it covers.
     * @param {Element} e The overlay.
     */
    function coverRun(e) {
        const rec = coverRegs.get(e);
        if (!rec)
            return;
        if (!e.isConnected) {
            Overlay.Unregister(e);
            return;
        }
        //  (an overlay that places itself, the toasts: placed first, so the check sees where it goes now and not a place of a moment
        //  ago, ex: before a dialog closed; F10)
        if (rec.Place)
            try {
                rec.Place();
            }
            catch {
            }
        for (const p of coverProblems(e, rec))
            devWarn("Sw.Overlay: " + p + " (UX LAY-1 / FBK-8: a fixed overlay covers nothing a user can act on; give it room, move it, or register what it owns).", "cover:" + p);
    }

    /**
     * Check a registered overlay after its layout settled (throttled).
     * @param {Element} e The overlay.
     */
    function coverLater(e) {
        const rec = coverRegs.get(e);
        if (!rec || !devMode())
            return;
        clearTimeout(rec.T);
        rec.T = setTimeout(() => coverRun(e), rec.Settle);
    }

    /**
     * Fixed overlays that must cover nothing (UX LAY-1, FBK-8, POP-6): toasts, activity lines, offline lines, floating bars, a page's
     * own fixed helpers. Pop-ups, menus and dialogs are modal layers and don't register.
     */
    const Overlay = {
        /**
         * Register a fixed overlay: in developer mode (Sw.DevMode) it is checked whenever it changes, the window is resized or
         * scrolled, and a "[Sw UX] Sw.Overlay: "<Name>" covers "<control>"" warning names every control it lies over that it doesn't
         * own. Unregistered automatically when it leaves the document.
         * @param {Element} e The overlay.
         * @param {object} [o] { Name (for the message), Owns (what it may lie over: an element, a selector, an array, a function),
         * Settle (ms after a change before checking, default 400) }.
         * @returns {function():void} Unregisters it.
         */
        Register(e, o) {
            o = o || {};
            checkOpts("Sw.Overlay.Register", o);
            if (!e || e.nodeType !== 1)
                return () => { };
            Overlay.Unregister(e);
            const rec = { Name: o.Name || e.className || e.tagName, Owns: o.Owns || null, Settle: o.Settle > 0 ? o.Settle : 400, T: 0, Mo: null };
            coverRegs.set(e, rec);
            if (devMode()) {
                if (typeof MutationObserver === "function") {
                    rec.Mo = new MutationObserver(() => coverLater(e));
                    rec.Mo.observe(e, { childList: true, subtree: true, attributes: true, attributeFilter: ["class", "style", "hidden"] });
                }
                if (!coverBound) {
                    coverBound = true;
                    const all = () => coverRegs.forEach((_, k) => coverLater(k));
                    window.addEventListener("resize", all);
                    window.addEventListener("scroll", all, { capture: true, passive: true });
                }
                coverLater(e);
            }
            return () => Overlay.Unregister(e);
        },
        /**
         * Stop checking an overlay.
         * @param {Element} e The overlay.
         */
        Unregister(e) {
            const rec = coverRegs.get(e);
            if (!rec)
                return;
            clearTimeout(rec.T);
            rec.Mo && rec.Mo.disconnect();
            coverRegs.delete(e);
        },
        /**
         * Check every registered overlay now (also outside developer mode: for self-tests and smoke runs).
         * @returns {string[]} The problems, ex: "\"Toasts\" covers \"Next\"".
         */
        Check() {
            const out = [];
            for (const [e, rec] of Array.from(coverRegs)) {
                if (!e.isConnected) {
                    Overlay.Unregister(e);
                    continue;
                }
                if (rec.Place)
                    try {
                        rec.Place();
                    }
                    catch {
                    }
                out.push(...coverProblems(e, rec));
            }
            return out;
        },
        /**
         * The same check for any element (not registered): what it covers now.
         * @param {Element} e The element. @param {object} [o] { Name, Owns }.
         * @returns {string[]} The problems.
         */
        Covers(e, o) {
            o = o || {};
            checkOpts("Sw.Overlay.Covers", o);
            return e && e.nodeType === 1 ? coverProblems(e, { Name: o.Name || e.className || e.tagName, Owns: o.Owns || null }) : [];
        },
    };

    // ------------------------------------------------------------------ core integration

    /**
     * Hook the UI into the core: the server block overlay (top windows), network error toasts, the loading overlay visual.
     */
    function integrate() {
        const E = Sw.Events;
        if (E && E.On) {
            //  A page without sw/ui.css (its own UI) can't style the dialog: the core draws its overlay there (core/session_start.js)
            const onBlock = info => {
                if (!Sw.Session || (typeof Sw.Session.HasUiCss !== "function") || Sw.Session.HasUiCss())
                    ServerBlock.Show(info && (info.Type || info.Kind) ? info : (info || "server.error"));
            };
            onBlock.SwUiBlock = true;
            E.On("server.block", onBlock);
            //  A hint placed before a theme change would stay in the old colours (and is stale): hide it
            E.On("theme.changed", () => Hint.Hide());
            E.On("server.unblock", () => ServerBlock.Hide());
        }
        const P = Sw.Page;
        if (P && P.OnLoading)
            P.OnLoading(o => Loader.Show({ Target: o.Element || o.Target, Text: o.Text, Opaque: o.Opaque, Icon: o.Icon, Delay: o.Delay }));
        //  Sw.Page.Announce puts its live region into the open top dialog (UX-POP-1)
        if (P && !P.AnnounceHost)
            P.AnnounceHost = () => {
                const e = topDialogEntry();
                return e && !e.Closed ? e.Box : null;
            };
    }
    integrate();

    Object.assign(Sw, {
        /**
         * Sw.Place(box, anchor, o): see Place (the options are checked in developer mode).
         * @param {HTMLElement} box The element. @param {Element|{X: number, Y: number}|null} anchor The anchor. @param {object} [o] Options.
         * @returns {*} See Place.
         */
        Place(box, anchor, o) {
            checkOpts("Sw.Place", o);
            return Place(box, anchor, o);
        },
        /**
         * Sw.Track(anchor, box, o): see Track (the options are checked in developer mode).
         * @param {Element|MouseEvent} anchor The anchor. @param {HTMLElement} box The element. @param {object} [o] Options of Sw.Place.
         * @returns {function():void} Stops tracking.
         */
        Track(anchor, box, o) {
            checkOpts("Sw.Track", o);
            return Track(anchor, box, o);
        },
        Popup,
        Menu,
        Dialog,
        SearchSelection,
        Toast,
        Fail,
        Info,
        Message,
        Hint,
        Loader,
        ServerBlock,
        FullScreen,
        Fullscreen: FullScreen,
        Motion,
        Overlay,
    });
})();
