/**
 * @file ui/dialogtest.js - the exhaustive self-test of dialogs (ui/dialogtest.html): the footer layout engine, input while busy and the
 * framework's own dialogs.
 *
 * The texts are test names and measurements for developers, not translated (SwWeb:no-tr-file: a developer page).
 *
 * Every frame loads this page with ?fixture&usetheme=<theme> at 320x180 (1280x720 at 400 % zoom), 390x844 (phone sheet), 844x390 (phone
 * in landscape) and 1920x1080, in the 5 built in themes, and runs every generated case; the host page lists every problem. There are
 * no "notes": anything below is a FAILURE.
 *  - Generated dialogs: footers of 1 to 4 buttons (a single action without a way out, Cancel + Save, an extra left button, two extra
 *    buttons), short and long labels (translations), bodies with one field, three fields (TextBox, NumberBox, textarea) or text only, and a title of two lines (at 400 % zoom the
 *    rejection line takes the title's place instead of pushing the focused field under the footer: D1).
 *  - States: idle, busy (Stop waiting in Cancel's slot / the 10 s Close), the rejection line, typing / arrows / wheel while busy, the work
 *    writing the server's value into the fields, a real Cancel (stopping, the line follows), a lookup that ends with the dialog open and
 *    a field message, typing in validated fields (field messages that come, change and go: LAY-9), a label swap (Cancel → Close, a
 *    longer label), a progress dialog, and the framework's own dialogs (Confirm,
 *    Prompt, Alert, Working, "Discard changes?").
 *  - Also fails on: a body of one field that scrolls once the line is shown (above 500 px high; D24).
 *  - Fails on: a title bar control (title, ⋮, ×) or a footer button that was on screen and moved (> 1.5 px), a label, input or text of
 *    the body that moved in the body's content while typing (LAY-9: field messages have their space before the first key), "Stop waiting" not in
 *    Cancel's slot, two of the footer / title bar parts overlapping, a control or the focused field covered, a footer / title bar control
 *    off screen, a footer that scrolls sideways or a footer button outside the box (S6-D1: at 400 % the buttons wrap, never scroll),
 *    the focused field not fully visible, a field that stays editable while busy (input lost), a NumberBox that changes while
 *    locked, a "newer changes" line or "Discard changes?" after the work wrote a value itself, a line that still says "use Cancel" while
 *    Cancel is stopping, a button label broken inside a word or cut without its full text as the tool tip (Sw.Dialog.CheckLabels), a
 *    longer label set by code without the developer warning that asks for Labels, a developer warning ("[Sw UX]") from the framework,
 *    and anything Sw.Overlay.Check() reports. Positions are taken after the box's opening animation ended.
 *  - A GATE: Passed is true only with 0 problems; a problem that is accepted needs a written waiver (Waivers below, with its reason, and
 *    the same list in ui/README "Self-test"). The page title starts with PASSED / FAILED.
 * The result is also in window.SwDialogSelfTest ({ Done, Passed, Frames, Checks, Problems: [text], Waived, Waivers, Notes: [] }) for test
 * runners and the smoke: fail when Passed is false.
 * ?only=<theme|WxH> runs some frames, ?quick fewer generated dialogs, ?bundle loads the bundles. The gate runs with visible (classic) scroll
 * bars (a headless browser without --hide-scrollbars), as Windows desktop users see them.
 */
(function () {
    "use strict";

    const Themes = ["light", "dark", "hacker", "application", "pro"];
    const Sizes = [[320, 180], [390, 844], [844, 390], [1920, 1080]];
    const quick = /[?&]quick\b/i.test(location.search);
    const wait = ms => new Promise(r => setTimeout(r, ms));
    const mk = (tag, cls, text) => {
        const e = document.createElement(tag);
        if (cls)
            e.className = cls;
        if (text !== undefined)
            e.textContent = text;
        return e;
    };

    // ------------------------------------------------------------------ fixture (inside a frame)

    const visible = e => !!e && e.isConnected && e.getClientRects().length > 0 && getComputedStyle(e).visibility !== "hidden" && !e.closest(".sw-leaving") && !e.closest("[inert]");
    const nameOf = e => ((e.getAttribute("aria-label") || e.textContent || e.title || e.tagName) + "").trim().replace(/\s+/g, " ").slice(0, 40);
    const topBox = () => Array.from(document.querySelectorAll(".sw-dialog:not(.sw-leaving)")).pop() || null;
    /** The controls that must never move: the title bar and the footer buttons. */
    const fixed = box => Array.from(box.querySelectorAll(":scope > .sw-dialog-head .sw-dialog-title, :scope > .sw-dialog-head .sw-button, :scope > .sw-dialog-foot .sw-button")).filter(visible);
    const fields = box => Array.from(box.querySelectorAll(".sw-dialog-body input:not([type=hidden]), .sw-dialog-body textarea")).filter(visible);
    /**
     * Wait until the box's own animations (the opening slide / scale; not endless ones such as spinners) have ended: positions are
     * measured on the settled box, also when a busy machine runs the animations late.
     * @param {HTMLElement} box The dialog.
     */
    const settled = async box => {
        await new Promise(r => requestAnimationFrame(r));
        let an = [];
        try {
            an = box.getAnimations({ subtree: true }).filter(a => a.playState === "running" && !(a.effect && a.effect.getTiming && a.effect.getTiming().iterations === Infinity));
        }
        catch {
        }
        await Promise.race([Promise.all(an.map(a => a.finished.catch(() => { }))), wait(2000)]);
        await new Promise(r => requestAnimationFrame(r));
    };
    /** The idle positions of the controls that must never move (after the box settled). */
    const snap = async box => {
        await settled(box);
        return new Map(fixed(box).map(e => [e, e.getBoundingClientRect()]));
    };
    const btnByText = (box, rx) => Array.from(box.querySelectorAll(".sw-dialog-foot .sw-button")).find(b => visible(b) && rx.test(b.textContent.trim()));
    const key = (t, k, o) => t.dispatchEvent(new KeyboardEvent("keydown", Object.assign({ key: k, bubbles: true, cancelable: true }, o || {})));
    const escape = () => key(document.activeElement && document.activeElement !== document.body ? document.activeElement : document, "Escape");
    const inter = (a, b) => Math.max(0, Math.min(a.right, b.right) - Math.max(a.left, b.left)) * Math.max(0, Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top));
    const rxq = s => new RegExp("^" + s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$");

    /** Developer warnings of the framework during the run (every one is a failure). */
    const warns = [];
    const ow = console.warn;
    console.warn = function (...a) {
        const s = a.map(x => String(x)).join(" ");
        //  (Sw.Focus.Keep can't tell where the focus is in a frame without the window's focus: not counted there)
        if (/^\[Sw UX\]/.test(s) && !(/Sw\.Focus\.Keep/.test(s) && !document.hasFocus()))
            warns.push(s.slice(0, 220));
        return ow.apply(this, a);
    };

    /**
     * Check one state of the open dialog against its idle measurements.
     * @param {string} label The case / state. @param {Map} idle The idle positions (or null). @param {Array<string>} out The problems.
     * @param {object} [o] { Replaced: [cancelRect, stopButton] (Stop waiting must sit in Cancel's slot), Box (the dialog) }.
     */
    function check(label, idle, out, o) {
        o = o || {};
        const box = o.Box || topBox();
        if (!box || !box.isConnected) {
            out.push(label + ": no dialog");
            return;
        }
        const W = window.innerWidth, H = window.innerHeight;
        if (idle)
            for (const [e, r0] of idle) {
                if (!visible(e))
                    continue;
                const r = e.getBoundingClientRect();
                const d = Math.hypot(r.left - r0.left, r.top - r0.top);
                if (d > 1.5)
                    out.push(label + ": moved \"" + nameOf(e) + "\" " + Math.round(d) + " px");
            }
        if (o.Replaced) {
            const [cr, sb] = o.Replaced;
            if (!sb || !visible(sb))
                out.push(label + ": no Stop waiting button");
            else {
                const r = sb.getBoundingClientRect();
                const inSlot = Math.abs(r.right - cr.right) <= 1.5 && Math.abs(r.bottom - cr.bottom) <= 1.5;
                if (!inSlot && !sb.closest(".sw-dialog-foot-extra"))
                    out.push(label + ": Stop waiting not in Cancel's slot (" + Math.round(r.right - cr.right) + ", " + Math.round(r.bottom - cr.bottom) + " px)");
            }
        }
        const body = box.querySelector(".sw-dialog-body");
        const br = body ? body.getBoundingClientRect() : null;
        //  overlaps between the parts that must stay apart: footer buttons, the status, the line, the title bar controls
        const parts = fixed(box).concat(Array.from(box.querySelectorAll(".sw-dialog-foot .sw-dialog-progress:not([hidden]), .sw-dialog-reject:not([hidden])")).filter(visible));
        for (let i = 0; i < parts.length; ++i)
            for (let j = i + 1; j < parts.length; ++j) {
                if (parts[i].contains(parts[j]) || parts[j].contains(parts[i]))
                    continue;
                const a = parts[i].getBoundingClientRect(), b = parts[j].getBoundingClientRect();
                if (inter(a, b) > 4)
                    out.push(label + ": overlap \"" + nameOf(parts[i]) + "\" / \"" + nameOf(parts[j]) + "\"");
            }
        //  the footer never scrolls sideways and every footer button lies inside the box (S6-D1, WCAG 1.4.10 reflow: at 400 % zoom the
        //  three-button footer scrolled sideways and its first button read "ction"; this check tested only the right and bottom edges)
        const foot = box.querySelector(":scope > .sw-dialog-foot");
        if (foot && visible(foot)) {
            if (foot.scrollWidth > foot.clientWidth + 1)
                out.push(label + ": the footer scrolls sideways (" + foot.scrollWidth + " > " + foot.clientWidth + " px)");
            const bx = box.getBoundingClientRect();
            for (const b of fixed(box).filter(b => foot.contains(b))) {
                const r = b.getBoundingClientRect();
                if (r.left < bx.left - 1 || r.right > bx.right + 1 || r.left < -1 || r.right > W + 1)
                    out.push(label + ": footer button outside the box \"" + nameOf(b) + "\" (" + Math.round(r.left) + ".." + Math.round(r.right) + " of " + Math.round(bx.left) + ".." + Math.round(bx.right) + ")");
            }
        }
        for (const c of fixed(box).concat(fields(box))) {
            const r0 = c.getBoundingClientRect();
            const inBody = body && body.contains(c);
            const r = {
                left: Math.max(r0.left, 0, inBody ? br.left : 0), right: Math.min(r0.right, W, inBody ? br.right : W),
                top: Math.max(r0.top, 0, inBody ? br.top : 0), bottom: Math.min(r0.bottom, H, inBody ? br.bottom : H),
            };
            if (!inBody && (r0.bottom > H + 1 || r0.right > W + 1 || r0.top < -1 || r0.left < -1))
                out.push(label + ": off screen \"" + nameOf(c) + "\" (" + [r0.left, r0.top, r0.right, r0.bottom].map(Math.round).join(",") + ")");
            if (r.right - r.left < 2 || r.bottom - r.top < 2)
                continue;
            const x = (r.left + r.right) / 2, y = (r.top + r.bottom) / 2;
            const h = document.elementFromPoint(x, y);
            if (h && h !== c && !c.contains(h) && !(h.contains(c) && h.closest(".sw-dialog") === box && !h.matches(".sw-dialog-foot, .sw-dialog-status, .sw-dialog-reject, .sw-toast")) && !(h.tagName === "LABEL" && h.control === c))
                out.push(label + ": covered \"" + nameOf(c) + "\" by " + (h.className || h.tagName).toString().slice(0, 40));
        }
        //  the focused field: fully visible (in the body, in the window, nothing over its corners)
        const ae = document.activeElement;
        if (ae && body && body.contains(ae) && ae.matches("input, textarea")) {
            const r = ae.getBoundingClientRect();
            if (r.height > 0) {
                const top = Math.max(br.top, 0), bot = Math.min(br.bottom, H);
                //  (a field taller than the body, a text area at 400 %: it must fill the body)
                const tall = r.height > bot - top + 1;
                if (tall ? Math.min(r.bottom, bot) - Math.max(r.top, top) < bot - top - 2 : (r.top < top - 1 || r.bottom > bot + 1))
                    out.push(label + ": focused field not fully visible (" + Math.round(Math.max(0, Math.min(r.bottom, bot) - Math.max(r.top, top))) + " of " + Math.round(r.height) + " px)");
                else
                    //  (the corners of its visible part: a field taller than the body fills it, its rest is scrolled out of view)
                    for (const [px, py] of [[r.left + 3, Math.max(r.top, top) + 3], [r.right - 3, Math.max(r.top, top) + 3], [r.left + 3, Math.min(r.bottom, bot) - 3], [r.right - 3, Math.min(r.bottom, bot) - 3]]) {
                        const h = document.elementFromPoint(px, py);
                        if (h && h !== ae && !ae.contains(h) && !h.contains(ae)) {
                            out.push(label + ": focused field covered by " + (h.className || h.tagName).toString().slice(0, 40));
                            break;
                        }
                    }
            }
        }
        //  labels: never broken inside a word, never cut without … and the full text as the tool tip (TXT-17)
        if (Sw.Dialog.CheckLabels)
            for (const p of Sw.Dialog.CheckLabels(box))
                out.push(label + ": " + p);
        else
            out.push(label + ": Sw.Dialog.CheckLabels is missing");
        if (Sw.Overlay && Sw.Overlay.Check)
            for (const p of Sw.Overlay.Check())
                out.push(label + ": overlay " + p);
        for (const w of warns.splice(0))
            out.push(label + ": developer warning " + w);
    }

    // ------------------------------------------------------------------ generated dialogs

    const Labels = {
        short: { test: "Test connection", help: "Preview", cancel: "Cancel", save: "Save", rebuild: "Rebuild" },
        long: {
            test: "Verbindung testen und Zertifikat prüfen", help: "Vorschau der Änderungen anzeigen", cancel: "Abbrechen und schließen",
            save: "Speichern und den Dienst neu starten", rebuild: "Den Suchindex vollständig neu aufbauen",
        },
        //  the busy three-button dialog of S6-D1 (Designer series 6: at 400 % its footer scrolled sideways, the first button read "ction")
        s6d1: { test: "Test connection", help: "Preview", cancel: "Cancel", save: "Save and restart", rebuild: "Rebuild" },
    };

    /**
     * The buttons of a generated footer.
     * @param {number} n 1..4. @param {object} L The labels. @param {function} work The primary's OnClick.
     * @returns {Array<object>} The definitions.
     */
    function footer(n, L, work) {
        if (n === 1)
            return [{ Text: L.rebuild, Kind: "primary", Value: "done", OnClick: work }];
        const b = [{ Text: L.cancel, Value: null }, { Text: L.save, Kind: "primary", Value: "saved", OnClick: work }];
        if (n >= 3)
            b.unshift({ Text: L.test, Close: false, OnClick: () => wait(50).then(() => false) });
        if (n >= 4)
            b.unshift({ Text: L.help, Close: false, OnClick: () => wait(50).then(() => false) });
        return b;
    }

    /**
     * The body of a generated dialog.
     * @param {string} kind "one", "three" or "text".
     * @returns {{E: HTMLElement, Name: object|null, Port: object|null, Last: HTMLElement|null}} The body.
     */
    function bodyOf(kind) {
        const e = mk("div", "sw-dialog-prompt");
        e.append(mk("div", "sw-dialog-text", "The server restarts after the save; open sessions end."));
        if (kind === "text")
            return { E: e, Name: null, Port: null, Last: null };
        const name = new Sw.TextBox({ Label: "Name", Value: "Initech" });
        e.append(name.E);
        if (kind === "one")
            return { E: e, Name: name, Port: null, Last: name.Input };
        const port = new Sw.NumberBox({ Label: "Port", Value: 80 });
        /* SwWeb:allow ux-saf-11 (a test fixture: nothing typed here is kept) */
        const notes = new Sw.TextBox({ Label: "Notes", Multiline: true, Rows: 3, Value: "Main office" });
        e.append(port.E, notes.E);
        return { E: e, Name: name, Port: port, Last: notes.Input };
    }

    /**
     * A save that can't be stopped: idle → busy (Stop waiting / the 10 s Close) → line → typing, arrows, wheel → the work writes the
     * server's value → closes (never "newer changes").
     */
    async function saveCase(id, n, L, kind, out, title) {
        let release;
        const work = () => new Promise(r => release = r);
        const B = bodyOf(kind);
        const h = Sw.Dialog.Open({ Title: title || "Edit server settings", Id: "dtsave." + id, Content: B.E, Buttons: footer(n, L, work) });
        await wait(450);
        if (B.Last)
            B.Last.focus();
        await wait(100);
        const box = h.Box;
        const idle = await snap(box);
        check(id + "/idle", null, out, { Box: box });
        const cancel = n > 1 ? btnByText(box, rxq(L.cancel)) : null;
        const cr = cancel ? cancel.getBoundingClientRect() : null;
        btnByText(box, rxq(n === 1 ? L.rebuild : L.save)).click();
        await wait(350);
        const sw = () => Array.from(box.querySelectorAll(".sw-dialog-foot .sw-button")).find(b => visible(b) && b.SwLabel && /^Stop waiting/.test(b.SwLabel));
        check(id + "/busy", idle, out, { Box: box, Replaced: cr ? [cr, sw()] : null });
        escape();
        await wait(450);
        check(id + "/line", idle, out, { Box: box, Replaced: cr ? [cr, sw()] : null });
        //  a body of one field never scrolls once the line came (D24; the reserved room is taken by the line), above 500 px high
        const bodyE = box.querySelector(":scope > .sw-dialog-body");
        if (kind === "one" && window.innerHeight > 500 && bodyE && bodyE.scrollHeight > bodyE.clientHeight + 1)
            out.push(id + "/line: the body of one field scrolls (" + bodyE.scrollHeight + " > " + bodyE.clientHeight + " px) once the line is shown");
        if (B.Name) {
            if (!B.Name.Input.readOnly)
                out.push(id + "/busy: the field stays editable (input typed now is lost)");
            key(document.activeElement || B.Name.Input, "a");
        }
        if (B.Port) {
            const p = B.Port.Input;
            const v0 = p.value;
            key(p, "ArrowUp");
            p.dispatchEvent(new WheelEvent("wheel", { deltaY: -100, bubbles: true, cancelable: true }));
            await wait(50);
            if (p.value !== v0)
                out.push(id + "/busy: NumberBox changed while locked (" + v0 + " → " + p.value + ")");
        }
        await wait(250);
        check(id + "/typing", idle, out, { Box: box });
        //  the 10 s way out (shortened)
        await wait(Sw.Dialog.WayOutMs + 350);
        check(id + "/waylong", idle, out, { Box: box });
        //  the work writes the server's value into the fields, then ends: closes, no "newer changes"
        if (B.Name)
            B.Name.Set("C-10042");
        if (B.Port)
            B.Port.Set(8080);
        release(n === 1 ? "done" : "saved");
        const v = await Promise.race([h.Result, wait(1200).then(() => "open")]);
        if (v === "open") {
            const line = box.querySelector(".sw-dialog-reject:not([hidden])");
            out.push(id + "/end: the dialog stayed open after the work wrote the server's value" + (line ? " (\"" + line.textContent.slice(0, 60) + "\")" : ""));
            h.Close(null);
        }
        if (Array.from(document.querySelectorAll(".sw-dialog:not(.sw-leaving) .sw-dialog-title")).some(t => /Discard/.test(t.textContent)))
            out.push(id + "/end: \"Discard changes?\" after a save");
    }

    /** A save that reads its signal and takes 0.8 s to stop: Cancel stays, the line follows the state (never "use Cancel" while stopping). */
    async function cancelCase(id, n, L, kind, out) {
        const work = (c, v, { Signal }) => new Promise((res, rej) => {
            const t = setTimeout(() => res("saved"), 8000);
            Signal.addEventListener("abort", () => {
                clearTimeout(t);
                setTimeout(() => rej(new DOMException("Stopped", "AbortError")), 800);
            }, { once: true });
        });
        const B = bodyOf(kind);
        const h = Sw.Dialog.Open({ Title: "Search the archive", Id: "dtcancel." + id, Content: B.E, Buttons: footer(n, L, work) });
        await wait(450);
        if (B.Last)
            B.Last.focus();
        await wait(100);
        const box = h.Box;
        const idle = await snap(box);
        btnByText(box, rxq(L.save)).click();
        await wait(350);
        check(id + "/busy", idle, out, { Box: box });
        escape();
        await wait(450);
        check(id + "/line", idle, out, { Box: box });
        const c = btnByText(box, rxq(L.cancel));
        if (!c) {
            out.push(id + "/busy: no Cancel for cancellable work");
            h.Close(null);
            return;
        }
        c.click();
        await wait(300);
        const line = box.querySelector(".sw-dialog-reject:not([hidden])");
        if (line && /use .*Cancel/i.test(line.textContent))
            out.push(id + "/stopping: the line still says \"" + line.textContent.slice(0, 60) + "\"");
        check(id + "/stopping", idle, out, { Box: box });
        const v = await Promise.race([h.Result, wait(2500).then(() => "timeout")]);
        if (v === "timeout") {
            out.push(id + ": Cancel did not close the dialog");
            h.Close(null);
        }
    }

    /** A lookup that ends with the dialog open and a field message (Users "Force a password reset"): the footer stays. */
    async function lookupCase(id, out) {
        const tb = new Sw.TextBox({ Label: "User", Value: "someone@example.com" });
        const h = Sw.Dialog.Open({
            Title: "Force a password reset", Id: "dtlookup." + id, Content: mk("div", "sw-dialog-prompt"),
            Buttons: [{ Text: "Cancel", Value: null }, {
                Text: "Continue", Kind: "primary", Value: "ok", OnClick: () => wait(600).then(() => {
                    if (tb.SetError)
                        tb.SetError("someone@example.com has no password (signs in with a passkey only), there is nothing to reset.");
                    return false;
                }),
            }],
        });
        h.Body.querySelector(".sw-dialog-prompt").append(tb.E);
        await wait(450);
        tb.Input.focus();
        await wait(100);
        await settled(h.Box);
        const foots = new Map(Array.from(h.Box.querySelectorAll(".sw-dialog-foot .sw-button")).filter(visible).map(e => [e, e.getBoundingClientRect()]));
        btnByText(h.Box, /^Continue$/).click();
        await wait(1100);
        check(id + "/after", foots, out, { Box: h.Box });
        h.Close(null);
    }

    /**
     * Typing in validated fields (UX guide LAY-9: nothing moves while the user types): field messages come and go (Required, Validate
     * with a long message, a NumberBox, a message set by code and cleared by a valid value). The title bar and the footer stay put
     * (check), and every label, input and text in the body keeps its place in the body's content (its scroll offset taken out: a low
     * window scrolls the body to the focused field, which is no layout shift).
     */
    async function typingCase(id, out) {
        const name = new Sw.TextBox({ Label: "Name", Required: "Enter a name" });
        const mailMsg = "Enter an e-mail address with an @ and a domain, ex: anna@example.com";
        const mail = new Sw.TextBox({
            Label: "E-mail",
            Validate: v => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(String(v || "").trim()) ? null : mailMsg,
            //  (the longest message Validate shows: its space, two lines in a narrow dialog, is there before the first key)
            Messages: [mailMsg],
        });
        const port = new Sw.NumberBox({ Label: "Port", Required: true, Min: 1, Max: 65535 });
        const h = Sw.Dialog.Open({
            Title: "Add a contact", Id: "dttype." + id, Content: mk("div", "sw-dialog-prompt"),
            Buttons: [{ Text: "Cancel", Value: null }, { Text: "Save", Kind: "primary", Value: "ok" }],
        });
        const body = h.Body.querySelector(".sw-dialog-prompt");
        body.append(name.E, mail.E, port.E, mk("div", "sw-dialog-text", "The contact is added to the address book."));
        await wait(450);
        const scroller = h.Box.querySelector(".sw-dialog-body");
        const watched = () => Array.from(h.Box.querySelectorAll(".sw-dialog-body input, .sw-dialog-body label, .sw-dialog-body .sw-dialog-text")).filter(visible);
        /** The place of an element in the body's content (the body's scroll taken out). */
        const at = e => {
            const r = e.getBoundingClientRect(), b = scroller.getBoundingClientRect();
            return { X: r.left - b.left + scroller.scrollLeft, Y: r.top - b.top + scroller.scrollTop };
        };
        let idle = await snap(h.Box);
        let idleBody = new Map(watched().map(e => [e, at(e)]));
        const setValue = async (ctl, v, commit) => {
            ctl.Input.focus();
            ctl.Input.value = v;
            ctl.Input.dispatchEvent(new Event("input", { bubbles: true }));
            if (commit)
                ctl.Input.dispatchEvent(new Event("change", { bubbles: true }));
            await wait(120);
        };
        const step = async (label, f) => {
            await f();
            await wait(80);
            check(id + "/" + label, idle, out, { Box: h.Box });
            for (const [e, p0] of idleBody) {
                if (!visible(e))
                    continue;
                const p = at(e);
                const d = Math.hypot(p.X - p0.X, p.Y - p0.Y);
                if (d > 1.5)
                    out.push(id + "/" + label + ": typing moved \"" + nameOf(e) + "\" " + Math.round(d) + " px (LAY-9)");
            }
        };
        await step("mail-bad", () => setValue(mail, "anna", true));
        await step("mail-typing", () => setValue(mail, "anna@exa", false));
        await step("mail-good", () => setValue(mail, "anna@example.com", true));
        await step("name-empty", () => setValue(name, "", true));
        await step("name-good", () => setValue(name, "Anna", true));
        await step("port-empty", async () => {
            port.Validate();
            await wait(10);
        });
        await step("port-good", () => setValue(port, "8080", false));
        //  A longer message set by code (a server's answer, not typing) may take more lines once: then the framework warns the
        //  developer (pass Messages); the line keeps that height, so typing a valid value (the message goes) moves nothing
        const slot = mail.ErrorE.parentElement;
        const before = slot.getBoundingClientRect().height;
        mail.SetError("anna@example.com is already in the address book (added by someone else a moment ago); choose another address");
        await wait(200);
        const grew = slot.getBoundingClientRect().height > before + 1;
        const told = warns.findIndex(w => /needs \d+ px more than the space its field reserved/.test(w));
        if (grew && told < 0)
            out.push(id + "/code-message: the message grew its line without the developer warning (LAY-9)");
        else if (!grew && told >= 0)
            out.push(id + "/code-message: a developer warning for a message that fits");
        if (told >= 0)
            warns.splice(told, 1);
        idle = await snap(h.Box);
        idleBody = new Map(watched().map(e => [e, at(e)]));
        await step("code-cleared", () => setValue(mail, "anna2@example.com", true));
        await step("code-again", () => setValue(mail, "anna", true));
        h.Close(null);
    }

    /** Label swaps (Admin N29): Cancel → Close (shorter, with and without Labels), Continue → a longer label (with Labels). */
    async function swapCase(id, withLabels, out) {
        const defs = [{ Text: "Cancel", Value: null }, { Text: "Continue", Kind: "primary", Value: true }];
        if (withLabels) {
            defs[0].Labels = ["Close"];
            defs[1].Labels = ["Force password reset"];
        }
        const h = Sw.Dialog.Open({ Title: "Withdraw the invitation?", Id: "dtswap." + id, Role: "alertdialog", Content: "c4r4w1@example.com is no longer listed.", Buttons: defs });
        await wait(500);
        const idle = await snap(h.Box);
        defs[0].Button.SetText("Close");
        await wait(150);
        check(id + "/close", idle, out, { Box: h.Box });
        //  a longer label keeps its slot only when the dialog knows it (Labels); without, the framework asks for Labels (a developer warning)
        if (withLabels) {
            defs[1].Button.SetText("Force password reset");
            await wait(150);
            check(id + "/longer", idle, out, { Box: h.Box });
        }
        else {
            //  a longer label set by code without Labels (an author mistake): the slot stays, the label is one line cut with … and
            //  its full text as the tool tip (never broken inside a word), and the developer warning asks for Labels
            defs[1].Button.SetText("Send now and notify the customer by e-mail");
            await wait(300);
            const asked = warns.findIndex(w => /got a longer label/.test(w));
            if (asked < 0)
                out.push(id + "/longer: no developer warning that asks for Labels");
            else
                warns.splice(asked, 1);
            check(id + "/longer", idle, out, { Box: h.Box });
        }
        h.Close(null);
    }

    /** SetBusy with progress, text and Cancel (an upload). */
    async function progressCase(out) {
        const h = Sw.Dialog.Open({ Title: "Upload report.xlsx", Id: "dt.progress", Content: "The file is sent to the server.", Buttons: [{ Text: "Cancel", Value: null }, { Text: "Done", Kind: "primary", Value: 1 }] });
        await wait(500);
        const idle = await snap(h.Box);
        let stop;
        const pr = new Promise(r => stop = r);
        h.SetBusy(pr, { Cancel: () => stop(), Text: "Uploading report.xlsx", Progress: 0.3, Background: true });
        await wait(2300);
        check("progress/busy", idle, out, { Box: h.Box });
        h.SetProgress(0.8, "Checking report.xlsx");
        await wait(300);
        check("progress/phase", idle, out, { Box: h.Box });
        h.Attempt("outside");
        await wait(450);
        check("progress/line", idle, out, { Box: h.Box });
        stop();
        await wait(200);
        h.Close(null);
    }

    /** The framework's own dialogs: Confirm, Prompt, Alert, Working, "Discard changes?". */
    async function frameworkCases(out) {
        //  Confirm (danger)
        let p = Sw.Dialog.Confirm("Delete the file?", "report.md will be removed. This can't be undone.", "Delete", { Danger: true });
        await wait(500);
        check("confirm/idle", null, out);
        let b = btnByText(topBox(), /^Cancel$/);
        b && b.click();
        await p;
        //  Prompt: the line after an outside click while dirty
        p = Sw.Dialog.Prompt("Rename report", "report.md", "File name");
        await wait(500);
        const box = topBox();
        const inp = box.querySelector(".sw-dialog-body input");
        inp && inp.focus();
        const idle = await snap(box);
        box.SwDialog.SetDirty(true);
        box.SwDialog.Attempt("outside");
        await wait(450);
        check("prompt/line", idle, out, { Box: box });
        box.SwDialog.Close(null);
        await p;
        //  Alert
        p = Sw.Dialog.Alert("Saved", "The settings are saved.");
        await wait(500);
        check("alert/idle", null, out);
        topBox().SwDialog.Close(null);
        await p;
        //  Working with its 10 s Close
        const close = Sw.Dialog.Working({ Title: "Rebuilding the index", Text: "This normally takes a few seconds.", Background: false });
        await wait(500);
        const wBox = topBox();
        const wIdle = await snap(wBox);
        await wait(Sw.Dialog.WayOutMs + 400);
        check("working/close", wIdle, out, { Box: wBox });
        close();
        await wait(300);
        //  "Discard changes?" over a dirty dialog
        const tb = new Sw.TextBox({ Label: "File name", Value: "report.md" });
        const h = Sw.Dialog.Open({ Title: "Rename report", Id: "dt.discard", Content: mk("div", "sw-dialog-prompt"), Buttons: [{ Text: "Cancel", Value: null }, { Text: "Rename", Kind: "primary", Value: 1 }] });
        h.Body.querySelector(".sw-dialog-prompt").append(tb.E);
        await wait(450);
        tb.Input.focus();
        h.SetDirty(true);
        const dIdle = await snap(h.Box);
        h.Attempt("escape");
        await wait(500);
        check("discard/question", null, out);
        const under = new Map(Array.from(dIdle).filter(([e]) => !e.closest(".sw-dialog-head")));
        check("discard/under", under, out, { Box: h.Box });
        b = btnByText(topBox(), /^Keep editing$/);
        b && b.click();
        await wait(300);
        h.Close(null);
    }

    /**
     * Run every case in this frame.
     * @returns {Promise<{Problems: string[], Checks: number}>} The result.
     */
    async function runFixture() {
        const out = [];
        let n = 0;
        Sw.Dialog.WayOutMs = 700;
        const run = async (name, f) => {
            try {
                await f();
            }
            catch (x) {
                out.push(name + ": threw " + (x && x.message ? x.message : x));
            }
            ++n;
            if (Sw.Popup && Sw.Popup.CloseAll)
                Sw.Popup.CloseAll();
            for (const d of Array.from(document.querySelectorAll(".sw-dialog:not(.sw-leaving)")))
                if (d.SwDialog)
                    d.SwDialog.Close(null);
            await wait(250);
        };
        const counts = quick ? [2, 3] : [1, 2, 3, 4];
        const bodies = quick ? ["one"] : ["one", "three", "text"];
        for (const lk of ["short", "long"])
            for (const c of counts)
                for (const b of bodies)
                    await run("save." + c + "." + lk + "." + b, () => saveCase("save." + c + "." + lk + "." + b, c, Labels[lk], b, out));
        //  a title of two lines at 400 % zoom with three buttons: the line must not push the focused field under the footer (D1)
        for (const c of quick ? [3] : [2, 3, 4])
            await run("save." + c + ".longtitle.one", () => saveCase("save." + c + ".longtitle.one", c, Labels.short, "one", out, "Edit managed service settings"));
        //  S6-D1: three buttons with "Save and restart", a title of two lines, one field (the footer wraps at 400 % zoom, never sideways)
        await run("save.3.s6d1.one", () => saveCase("save.3.s6d1.one", 3, Labels.s6d1, "one", out, "Edit managed service settings"));
        for (const lk of ["short", "long"])
            for (const c of [2, 3, 4])
                await run("cancel." + c + "." + lk, () => cancelCase("cancel." + c + "." + lk, c, Labels[lk], "one", out));
        await run("lookup", () => lookupCase("lookup", out));
        await run("typing", () => typingCase("typing", out));
        await run("swap.labels", () => swapCase("swap.labels", true, out));
        await run("swap.plain", () => swapCase("swap.plain", false, out));
        await run("progress", () => progressCase(out));
        await run("framework", () => frameworkCases(out));
        Sw.Dialog.WayOutMs = 10000;
        return { Problems: out, Checks: n };
    }

    // ------------------------------------------------------------------ host

    async function runHost() {
        document.body.classList.add("dt-host");
        const h1 = mk("h1", null, "Dialog self-test");
        const sum = mk("p", null, "Running " + Themes.length * Sizes.length + " frames…");
        const grid = mk("div", "dt-frames");
        document.body.append(h1, sum, grid);
        const only = new URLSearchParams(location.search).get("only");
        const extra = (/[?&]bundle\b/i.test(location.search) ? "&bundle" : "") + (quick ? "&quick" : "");
        const jobs = [];
        //  every frame stays inside the window while it runs: the browser throttles frames out of view (no animation frames, no resize
        //  observers), which would test a different engine; the problem lists go below the grid
        const runs = [];
        for (const [w, h] of Sizes)
            for (const t of Themes)
                if (!only || only === t || only === w + "x" + h)
                    runs.push([w, h, t]);
        const cols = Math.max(1, Math.min(runs.length, Themes.length));
        const rows = Math.max(1, Math.ceil(runs.length / cols));
        const cw = (window.innerWidth - 32 - 12 * (cols - 1)) / cols, ch = (window.innerHeight - 150 - 34 * rows) / rows;
        const lists = mk("div");
        document.body.append(lists);
        for (const [w, h, t] of runs) {
            const s = Math.min(1, cw / w, ch / h);
            const cell = mk("div", "dt-cell");
            const cap = mk("div", null, w + "x" + h + " " + t);
            const clip = mk("div", "dt-clip");
            clip.style.width = Math.floor(w * s) + "px";
            clip.style.height = Math.floor(h * s) + "px";
            const fr = document.createElement("iframe");
            fr.width = String(w);
            fr.height = String(h);
            fr.style.width = w + "px";
            fr.style.height = h + "px";
            fr.style.transform = "scale(" + s + ")";
            fr.title = "Dialog self-test " + w + "x" + h + " " + t;
            fr.src = "dialogtest.html?fixture&usetheme=" + t + extra;
            const list = mk("ul", "dt-list");
            clip.append(fr);
            cell.append(cap, clip);
            grid.append(cell);
            jobs.push(new Promise(res => {
                fr.addEventListener("load", async () => {
                    const fw = fr.contentWindow;
                    for (let i = 0; i < 100 && !(fw.DialogFixture && fw.Sw && fw.Sw.Dialog); ++i)
                        await wait(100);
                    let r;
                    try {
                        r = await fw.DialogFixture.Run();
                    }
                    catch (x) {
                        r = { Problems: ["frame: " + (x && x.message ? x.message : x)], Checks: 0 };
                    }
                    const label = w + "x" + h + " " + t;
                    cap.className = r.Problems.length ? "dt-bad" : "dt-ok";
                    cap.textContent = label + ": " + (r.Problems.length ? r.Problems.length + " problem(s)" : "ok") + " (" + r.Checks + " cases)";
                    if (r.Problems.length) {
                        lists.append(mk("h2", null, label));
                        for (const p of r.Problems)
                            list.append(mk("li", null, p));
                        lists.append(list);
                    }
                    res({ Label: label, Problems: r.Problems.map(p => label + " " + p), Checks: r.Checks });
                }, { once: true });
            }));
        }
        const rs = await Promise.all(jobs);
        const all = rs.flatMap(r => r.Problems);
        //  A GATE: every problem fails it, unless it matches a written waiver (Waivers: the reason is listed in ui/README "Self-test")
        const waived = [];
        const problems = all.filter(p => {
            const w = Waivers.find(x => x.Match.test(p));
            if (w)
                waived.push(p + " (waiver: " + w.Why + ")");
            return !w;
        });
        const checks = rs.reduce((s, r) => s + r.Checks, 0);
        const passed = problems.length === 0;
        sum.textContent = (passed ? "PASSED: " : "FAILED: ") + rs.length + " frames, " + checks + " cases: " + (problems.length ? problems.length + " problem(s)" : "no problems") +
            (waived.length ? ", " + waived.length + " waived" : "");
        sum.className = passed ? "dt-ok" : "dt-bad";
        document.title = (passed ? "PASSED" : "FAILED") + " - SysWeaver dialog self-test";
        window.SwDialogSelfTest = { Done: true, Passed: passed, Frames: rs.length, Checks: checks, Problems: problems, Waived: waived, Waivers: Waivers.map(w => String(w.Match) + ": " + w.Why), Notes: [] };
    }

    /**
     * Accepted problems, each with its written reason (also in ui/README "Self-test"). Empty: nothing is waived, every problem fails.
     * Add one only with the owner's agreement: { Match: /regex over "WxH theme case/state: problem"/, Why: "the reason" }.
     * @type {Array<{Match: RegExp, Why: string}>}
     */
    const Waivers = [];

    window.SwDialogSelfTest = { Done: false, Passed: false, Frames: 0, Checks: 0, Problems: [], Waived: [], Waivers: [], Notes: [] };
    if (/[?&]fixture\b/i.test(location.search)) {
        document.body.classList.add("dt-fixture-page");
        document.body.append(mk("p", null, "Dialog self-test fixture"));
        window.DialogFixture = { Run: runFixture };
    }
    else if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", runHost);
    else
        runHost();
})();
