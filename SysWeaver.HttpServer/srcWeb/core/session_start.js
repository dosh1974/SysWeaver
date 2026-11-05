/**
 * @file core/session_start.js - starts the messaging core of a page (the messaging half of the old SysWeaverInit): the last file of the
 * small bundle sw/session.js, and part of sw/core.js / common/common.js (same file, it runs once per page).
 *
 * Purpose: runs once when evaluated:
 *  1. Url flags: "colorscheme" (adds meta color-scheme; a frame started with it follows the InterOp message "color.scheme" {Value}),
 *     "ignoreuserchanges=true" (Sw.Session.IgnoreUserChanges). ("mobile", "transparent", "fillscreen" are read by Sw.Layout, "top" by
 *     Sw.Session.)
 *  2. The requested theme (applies nothing, see below): "usetheme" / "settheme" url flags and the InterOp messages "Theme.Changed",
 *     "theme.use", "theme.set".
 *  3. The automatic classes: Sw.Layout.Init (html / body classes SwMobile / SwDesktop, SwPortrait / SwLandscape, SwNarrow / SwWide,
 *     SwSelf*, Transparent, FillScreen, SwNested and the body events).
 *  4. The session: Sw.Session.InitReactions and Sw.Session.Init (master election, GetMessages long poll, server time, default
 *     reactions); not with data-sw-no-session on html (a page that opts out of the session: no serverTime, no GetMessages).
 *  5. The fallback server block overlay (see below).
 *  6. Starts what an opt-in script queued in Sw.AfterSession because it ran first (sw/theme.js before sw/session.js: any order works).
 *
 * The requested theme (Sw.Session.RequestedTheme): sw/session.js applies no theme (no data-theme, no theme css), but it keeps what the
 * SysWeaver handshake asks for, so a page with its own theming can follow it, and core/theme.js (sw/theme.js) applies it:
 *  - RequestedTheme: the theme forced for this window ("usetheme" url flag, the InterOp message "theme.use" {Value} in a frame started
 *    with usetheme, or a page's assignment; the legacy window.UseTheme), else null. Not persisted. Any window's setTheme / resetTheme
 *    ("Theme.Changed" from another window) drops it in every window (as the original: the user's choice wins).
 *  - StoredTheme: the chosen theme (localStorage "SysWeaver.Theme": the "settheme" url flag, the message "theme.set" {Value} in a frame
 *    started with settheme, setTheme), else null.
 *  - Sw.Events "theme.request" {Theme, Stored, Reason} when one of them changed by a message (Reason "use", "set", "changed") or by
 *    RequestTheme (Reason "page"). A page with its own theme uses Theme || Stored || its default, at start and on every event.
 *
 * The fallback server block overlay: a top window blocks input while the server is paused / restarting / shut down / lost (Sw.Events
 * "server.block" / "server.unblock" of Sw.Session), as every page of the original did. The ui library (sw/ui.js) draws the designed
 * dialog; a page without it gets a minimal self-styled one (inline styles, system colors, no css file; movable by its title bar,
 * resizable, × / Escape only hide it until the server is back, no reload button: the session reloads by itself). Any other "server.block"
 * handler (or setting info.Handled = true in one) claims the event, so a page that draws its own overlay just listens. Opt out:
 * data-sw-no-block on html, or Sw.Session.FallbackBlock = false.
 * A page that loads sw/ui.js without sw/ui.css (a page with its own UI) can't show the designed dialog, so the core draws its overlay there
 * too (Sw.Session.HasUiCss). When such a page styles the original overlay itself (a css rule for the element SysWeaver-ServerBlock, ex:
 * position: fixed), the overlay is drawn as the original's elements, without inline styles, so the page's look applies: SysWeaver-ServerBlock
 * with SysWeaver-ServerImage (empty: the original's 128 px theme icon isn't drawn), SysWeaver-ServerTitle, SysWeaver-ServerText,
 * SysWeaver-ServerWaiting and SysWeaver-ServerTime (the time since the block). It can't be closed, as the original.
 *
 * Load order: after core/session.js and core/layout.js. Dependencies: Sw.Url, Sw.Storage, Sw.InterOp, Sw.Events, Sw.Session, Sw.Layout.
 * Globals defined: none (Sw.Session.RequestedTheme, StoredTheme, RequestTheme, ThemeStorageKey, FallbackBlock, HasUiCss). Side effect: runs when
 * evaluated.
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const S = Sw.Session;
    const on = Sw.InterOp.On;
    const id = Sw.InterOp.Id;
    const isTop = S.IsTop;

    // ------------------------------------------------------------------ url flags

    const colorScheme = Sw.Url.Get("colorscheme");
    if (colorScheme) {
        const meta = document.createElement("meta");
        meta.name = "color-scheme";
        meta.content = colorScheme;
        document.head.appendChild(meta);
        if (!isTop)
            on("color.scheme", msg => {
                meta.content = msg.Value;
            });
    }
    if (Sw.Url.Get("ignoreuserchanges") === "true")
        S.IgnoreUserChanges();

    // ------------------------------------------------------------------ the requested theme

    /** localStorage key of the chosen theme (also Sw.Theme.StorageKey). */
    const themeKey = "SysWeaver.Theme";

    const setTheme = Sw.Url.Get("settheme");
    const useTheme = Sw.Url.Get("usetheme");

    /** @type {string|null} The theme forced for this window (see the file header). */
    let requested = useTheme ? "" + useTheme : null;

    if (setTheme)
        Sw.Storage.Set(themeKey, setTheme);

    /**
     * Change the requested theme and tell the page (Sw.Events "theme.request").
     * @param {string|null} theme The forced theme (null: none).
     * @param {string} reason "use", "set", "changed" or "page".
     */
    function request(theme, reason) {
        requested = theme ? "" + theme : null;
        Sw.Events.Emit("theme.request", { Theme: requested, Stored: Sw.Storage.Get(themeKey, null) || null, Reason: reason });
    }

    //  Another window chose a theme (setTheme / resetTheme / its OS scheme): every window drops its forced theme, as the original
    on("Theme.Changed", msg => {
        if (msg.From !== id)
            request(null, "changed");
    });
    //  A host page tells its frame (only a frame started with the matching url flag listens, as the original)
    if (!isTop) {
        if (setTheme)
            on("theme.set", msg => {
                Sw.Storage.Set(themeKey, msg.Value);
                request(requested, "set");
            });
        if (useTheme)
            on("theme.use", msg => request(msg.Value, "use"));
    }

    Object.defineProperties(S, {
        /** @type {string|null} The theme forced for this window (see the file header); an assignment changes it without an event. */
        RequestedTheme: {
            get: () => requested,
            set: v => {
                requested = v ? "" + v : null;
            },
            enumerable: true,
            configurable: true,
        },
        /** @type {string|null} The chosen theme (localStorage "SysWeaver.Theme"), null if none. */
        StoredTheme: {
            get: () => Sw.Storage.Get(themeKey, null) || null,
            enumerable: true,
            configurable: true,
        },
    });

    /** @type {string} localStorage key of the chosen theme ("SysWeaver.Theme"). */
    S.ThemeStorageKey = themeKey;

    /**
     * Force a theme for this window (as the "usetheme" url flag; null: none) and tell the page (Sw.Events "theme.request", Reason
     * "page"); with sw/theme.js the theme is applied.
     * @param {string|null} theme The theme name (optionally "name@css").
     */
    S.RequestTheme = theme => request(theme, "page");

    // ------------------------------------------------------------------ the automatic classes and the session

    Sw.Layout.Init();

    if (!document.documentElement.hasAttribute("data-sw-no-session")) {
        S.InitReactions();
        S.Init();
    }

    // ------------------------------------------------------------------ the fallback server block overlay

    /** @type {boolean} False: no fallback overlay (see the file header). */
    S.FallbackBlock = true;

    /** @type {{E: HTMLElement, Box: HTMLElement, Close: function():void, Timer: number, Off: function():void}|null} The shown fallback overlay. */
    let shown = null;

    /** @type {string|null} The block type the user closed (it stays closed until "server.unblock" or a block of another type). */
    let closedType = null;

    /** The input events the overlay blocks outside its box (the original blocked input on the document). */
    const blockedEvents = ["keydown", "keyup", "keypress", "wheel", "contextmenu"];

    /**
     * Input outside the box never reaches the page (no shortcut runs against a paused or lost server); input inside the box works.
     * @param {Event} ev The event.
     */
    function blockInput(ev) {
        if (shown && shown.Box.contains(ev.target))
            return;
        ev.preventDefault();
        ev.stopPropagation();
        //  Escape closes it also when the focus left the box (ex: after a press on the backdrop, S7a-A1-5)
        if (shown && ev.type === "keydown" && ev.key === "Escape")
            shown.Close();
    }

    /** Remove the fallback overlay. */
    function hide() {
        const s = shown;
        if (!s)
            return;
        shown = null;
        clearInterval(s.Timer);
        s.Off();
        s.E.remove();
    }

    /**
     * Show the fallback overlay: a small dialog (title bar with ×, text, the time since it was blocked) on a dimmed backdrop that takes
     * the page's input. The title bar moves it (mouse, pen, touch), the corner resizes it, × or Escape closes it (only hidden: the page
     * still reloads when the server is back as a new instance). No reload button: the session reloads by itself.
     * @param {{Type: string, Title: string, Text: string, Time: Date}} info The "server.block" info.
     */
    function show(info) {
        hide();
        const e = document.createElement("div");
        e.className = "sw-serverblock";
        e.setAttribute("data-sw-fallback", "");
        e.style.cssText = "position:fixed;inset:0;z-index:2147483647;background:rgba(0,0,0,0.55);font:16px system-ui,sans-serif;";
        const box = document.createElement("div");
        box.setAttribute("role", "alertdialog");
        box.setAttribute("aria-modal", "true");
        box.tabIndex = -1;
        box.style.cssText = "position:fixed;left:0;top:0;box-sizing:border-box;width:min(calc(100vw - 16px),26em);min-width:min(calc(100vw - 16px),14em);"
            + "max-width:calc(100vw - 8px);max-height:calc(100vh - 8px);min-height:8em;overflow:auto;resize:both;display:flex;flex-direction:column;"
            + "border:1px solid GrayText;border-radius:8px;background:Canvas;color:CanvasText;box-shadow:0 8px 32px rgba(0,0,0,0.35);outline:none;";
        const head = document.createElement("div");
        head.style.cssText = "display:flex;align-items:flex-start;gap:0.5em;padding:0.75em 0.5em 0.25em 1.25em;cursor:move;touch-action:none;user-select:none;";
        const title = document.createElement("div");
        title.style.cssText = "flex:1;font-weight:600;font-size:1.2em;padding-top:0.15em;";
        title.textContent = info.Title || "";
        title.id = "sw-serverblock-title";
        const close = document.createElement("button");
        close.type = "button";
        close.textContent = "×";
        close.title = _TF("Close", "Tool tip of the button that closes a dialog");
        close.setAttribute("aria-label", close.title);
        close.style.cssText = "flex:none;width:2em;height:2em;border:0;border-radius:6px;background:transparent;color:inherit;font:1.4em/1 system-ui,sans-serif;cursor:pointer;";
        head.append(title, close);
        const body = document.createElement("div");
        body.style.cssText = "padding:0.25em 1.25em 1.25em;text-align:center;";
        const text = document.createElement("div");
        text.textContent = info.Text || "";
        text.id = "sw-serverblock-text";
        const time = document.createElement("div");
        time.style.cssText = "margin-top:0.75em;font-weight:600;font-variant-numeric:tabular-nums;";
        const note = document.createElement("div");
        note.style.cssText = "margin-top:0.75em;font-size:0.85em;opacity:0.75;";
        note.textContent = _TF("This closes by itself when the service is back; the page reloads if the service was restarted. Close it to read or copy the page meanwhile.",
            "Note on the server unavailable dialog: it goes away by itself when the server answers again (the page reloads when it is a restarted server); the user may close it to see the page behind it");
        body.append(text, time, note);
        box.append(head, body);
        e.append(box);
        box.setAttribute("aria-labelledby", title.id);
        box.setAttribute("aria-describedby", text.id);
        const start = info.Time instanceof Date ? info.Time.getTime() : Date.now();
        const tick = () => {
            const s = Math.max(0, Math.floor((Date.now() - start) / 1000));
            time.textContent = Math.floor(s / 60) + ":" + ("0" + (s % 60)).slice(-2);
        };
        tick();
        /** Closed by the user: hidden until the server is back or another kind of block comes. */
        const userClose = () => {
            closedType = info.Type || "";
            hide();
        };
        close.addEventListener("click", userClose);
        //  keys typed in the box stay in it (no page shortcut); Escape closes
        ["keydown", "keyup", "keypress"].forEach(n => box.addEventListener(n, ev => {
            ev.stopPropagation();
            if (n === "keydown" && ev.key === "Escape") {
                ev.preventDefault();
                userClose();
            }
        }));
        //  A press on the backdrop keeps the focus in the box (so Escape and Tab stay with the dialog)
        e.addEventListener("pointerdown", ev => {
            if (ev.target !== e)
                return;
            ev.preventDefault();
            try {
                box.focus({ preventScroll: true });
            }
            catch {
            }
        });
        //  Keep the box's title bar inside the window: its height, and sideways at least 160 px of it, so it moves freely both ways (the
        //  same rule as Sw.Dialog's MinOnScreen, ui/overlay.js, AS7-8); a window resize brings the whole box back
        const vw = () => document.documentElement.clientWidth || window.innerWidth;
        const vh = () => document.documentElement.clientHeight || window.innerHeight;
        const place = (l, t, free) => {
            const w = Math.min(box.offsetWidth, vw());
            l = free ? Math.max(Math.min(160, w) - w, Math.min(l, vw() - Math.min(160, w))) : Math.max(0, Math.min(l, vw() - w));
            t = Math.max(0, Math.min(t, vh() - Math.min(head.offsetHeight || 40, vh())));
            box.style.left = Math.round(l) + "px";
            box.style.top = Math.round(t) + "px";
        };
        //  Title bar drag: the offset of the press inside the box is kept (one pointer; never jumps)
        head.addEventListener("pointerdown", ev => {
            if (ev.button !== 0 || ev.target === close)
                return;
            ev.preventDefault();
            const r = box.getBoundingClientRect();
            const dx = ev.clientX - r.left, dy = ev.clientY - r.top, pid = ev.pointerId;
            try {
                head.setPointerCapture(pid);
            }
            catch {
            }
            const mouse = ev.pointerType === "mouse";
            //  The drag ends as Sw.Dialog's (AS7-8, OWN3-Down-2): on the release, a cancel, a lost capture, or a mouse move with no button
            //  held (the release happened where the page didn't see it): never a box that keeps following the pointer
            const end = () => {
                head.removeEventListener("pointermove", move);
                head.removeEventListener("pointerup", up);
                head.removeEventListener("pointercancel", up);
                head.removeEventListener("lostpointercapture", up);
            };
            const move = m => {
                if (m.pointerId !== pid)
                    return;
                if (mouse && m.buttons === 0) {
                    end();
                    return;
                }
                place(m.clientX - dx, m.clientY - dy, true);
            };
            const up = u => {
                if (u.pointerId === pid)
                    end();
            };
            head.addEventListener("pointermove", move);
            head.addEventListener("pointerup", up);
            head.addEventListener("pointercancel", up);
            head.addEventListener("lostpointercapture", up);
        });
        const onResize = () => place(box.offsetLeft, box.offsetTop);
        window.addEventListener("resize", onResize);
        blockedEvents.forEach(n => window.addEventListener(n, blockInput, { capture: true, passive: false }));
        (document.body || document.documentElement).appendChild(e);
        place((vw() - box.offsetWidth) / 2, Math.max(8, (vh() - box.offsetHeight) / 2));
        try {
            box.focus({ preventScroll: true });
        }
        catch {
        }
        shown = {
            E: e,
            Box: box,
            Close: userClose,
            Timer: setInterval(tick, 1000),
            Off: () => {
                blockedEvents.forEach(n => window.removeEventListener(n, blockInput, { capture: true }));
                window.removeEventListener("resize", onResize);
            },
        };
    }

    /**
     * True when sw/ui.css is loaded (its tokens are on the root element), so the ui library can draw its designed server block dialog.
     * @returns {boolean} True if the page has the Sw component styles.
     */
    S.HasUiCss = () => !!getComputedStyle(document.documentElement).getPropertyValue("--sw-elev-1").trim();

    /**
     * True when the page's own css styles the original overlay element (SysWeaver-ServerBlock positioned fixed / absolute).
     * @returns {boolean} True if the original's elements should be drawn.
     */
    function pageStylesOriginal() {
        const p = document.createElement("SysWeaver-ServerBlock");
        (document.body || document.documentElement).appendChild(p);
        const pos = getComputedStyle(p).position;
        p.remove();
        return (pos === "fixed") || (pos === "absolute");
    }

    /**
     * Show the overlay as the original's elements (styled by the page's own css, no inline styles): it blocks the page's input and can't be
     * closed; the time since the block is updated every second (Sw.Cells.DateTimeLive when the ui library is loaded, as the original's
     * ValueFormat.updateDateTimeLive).
     * @param {{Type: string, Title: string, Text: string, Time: Date}} info The "server.block" info.
     */
    function showOriginal(info) {
        hide();
        const mk = (n, t) => {
            const x = document.createElement("SysWeaver-Server" + n);
            if (t !== undefined)
                x.textContent = t;
            return x;
        };
        const e = mk("Block");
        e.setAttribute("data-sw-fallback", "");
        //  (not focused, as the original: a focus ring would draw around the whole page; the input is blocked anyway)
        e.setAttribute("role", "alertdialog");
        e.setAttribute("aria-modal", "true");
        const title = mk("Title", info.Title || "");
        title.id = "sw-serverblock-title";
        const time = mk("Time");
        e.append(mk("Image"), title, mk("Text", info.Text || ""), mk("Waiting"), time);
        e.setAttribute("aria-labelledby", title.id);
        const at = info.Time instanceof Date ? info.Time : new Date();
        let timer = 0;
        if (Sw.Cells && typeof Sw.Cells.DateTimeLive === "function")
            Sw.Cells.DateTimeLive(time, at);
        else {
            const tick = () => {
                const s = Math.max(0, Math.floor((Date.now() - at.getTime()) / 1000));
                time.textContent = Math.floor(s / 60) + ":" + ("0" + (s % 60)).slice(-2);
            };
            tick();
            timer = setInterval(tick, 1000);
        }
        blockedEvents.forEach(n => window.addEventListener(n, blockInput, { capture: true, passive: false }));
        (document.body || document.documentElement).appendChild(e);
        shown = {
            E: e,
            Box: e,
            Close: () => {
            },
            Timer: timer,
            Off: () => blockedEvents.forEach(n => window.removeEventListener(n, blockInput, { capture: true })),
        };
    }

    /** @type {object|null} The last "server.block" info (null after "server.unblock"). */
    let last = null;

    /**
     * The core's "server.block" handler: draws the fallback overlay when no other handler claims the event.
     * @param {object} info The block info.
     */
    function onBlock(info) {
        const mine = last = info || {};
        //  After the other handlers of the same emit: one of them may claim it; an unblock meanwhile cancels it
        queueMicrotask(() => {
            //  The ui library's dialog (marked SwUiBlock) only counts when sw/ui.css is there to style it
            const styled = S.HasUiCss();
            const others = (Sw.Events.Map.get("server.block") || []).some(f => (f !== onBlock) && !(f.SwUiBlock && !styled));
            if (!S.FallbackBlock || others || mine.Handled || document.documentElement.hasAttribute("data-sw-no-block"))
                return;
            //  closed by the user: only another kind of block shows it again
            if ((closedType !== null) && (closedType === (mine.Type || "")))
                return;
            closedType = null;
            if (last === mine) {
                if (!styled && pageStylesOriginal())
                    showOriginal(mine);
                else
                    show(mine);
            }
        });
    }
    Sw.Events.On("server.block", onBlock);
    Sw.Events.On("server.unblock", () => {
        last = null;
        closedType = null;
        hide();
    });

    // ------------------------------------------------------------------ scripts that ran before the messaging core

    //  An opt-in script that ran before sw/session.js (sw/theme.js as a classic script before a deferred sw/session.js, async scripts)
    //  queued its start in Sw.AfterSession: it starts now, so the order of the scripts doesn't matter (AS6-3)
    const after = Sw.AfterSession;
    Sw.AfterSession = null;
    if (Array.isArray(after))
        after.forEach(fn => {
            try {
                fn();
            }
            catch (e) {
                console.error(e);
            }
        });
})();
