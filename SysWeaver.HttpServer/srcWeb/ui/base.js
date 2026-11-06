/**
 * @file ui/base.js - SysWeaver UI library (Sw), part 1 of 7: DOM and input helpers.
 *
 * The SysWeaver UI library is a dependency free, touch first component library (vanilla js, classic scripts, no modules).
 * It is a port of the EffectWeaver "Ew" library, rebranded and extended for SysWeaver. It extends the global Sw namespace that
 * the UI-less core (core/*.js, bundle sw/core.js) creates. Load order: core, then ui/base.js, ui/controls.js, ui/overlay.js,
 * ui/layout.js, ui/editors.js, ui/cells.js (ui/code.js optional). All files work concatenated (bundle sw/ui.js) and one by one.
 * Documentation: ui/README.md. Styles: ui/tokens.css, ui/components.css, ui/overlays.css, ui/cells.css, ui/code.css, ui/icons.css.
 *
 * Contents of this file:
 *  - Sw.el (element builder), Sw.clamp, Sw.snap, Sw.FormatNumber
 *  - Sw.Icon / Sw.Icons (inline svg icon set) and Sw.IconClass / Sw.SetIconClass (SysWeaver mask icon classes, IconX, or urls)
 *  - Sw.LongPress, Sw.NoContextMenu, Sw.IsPureEvent, Sw.BadClick, Sw.ModifierClick, Sw.IsPureContextMenu, Sw.KeyClick, Sw.MenuButton
 *  - Sw.Disabled (aria-disabled controls that stay reachable and say why), Sw.DevWarn (developer warnings for misuse)
 *  - Sw.Work (the busy state of work started by a click, request tracking), Sw.OnClick (a clickable element the right way)
 *  - Sw.Focus (tabbable queries, focus traps: the old BlockTab / UnblockTab)
 *  - Sw.Keys (keyboard shortcut registry), Sw.Drag (pointer drag helper)
 *  - Sw.Clipboard (copy with toast, copy on click)
 *  - Sw.Color (color math), Sw.Debounce / Sw.Throttle (fallbacks when the core has none)
 *  - Smooth image / iframe loading: Sw.FadeSrc, Sw.UnfadeSrc, Sw.CreateImg, Sw.CreateIFrame, Sw.SetImageSource, Sw.PendingImages
 *  - Sw.Reserve (reserved space: nothing moves while the user types, UX guide LAY-9) and its developer check of layout shifts
 *
 * Conventions (all components):
 *  - Components are classes constructed with one options object (PascalCase keys, all optional).
 *  - The root element is .E, append it where you want it.
 *  - Values: Get(), Set(value, silent) (silent = no callbacks). Callbacks: OnInput(value) while dragging / typing,
 *    OnChange(value) when committed (released, Enter, blur).
 */

(() => {
    "use strict";

    /** @type {object} The global namespace (created by the core, created here if the core is missing). */
    const Sw = window.Sw = window.Sw || {};

    // ------------------------------------------------------------------ element builder

    /**
     * Create an element.
     * @param {string} tag Tag name, may include classes: "div.a.b".
     * @param {object|Node|string} [props] Optional: { class, text, html, title, attrs: {}, style: {}, dataset: {}, on: { event: fn }, ...any element property }.
     * A Node, a string, an array or a component ({E}) is treated as the first child.
     * @param {...(Node|string|number|{E: Node}|Array|null|undefined|false)} children Children: nodes and texts (numbers
     * become text); arrays are flattened (any depth), so el("div", null, [a, b], c) is el("div", null, a, b, c); a component (an object
     * with an E node, ex: a Sw.Button) adds its E. null, undefined and false are skipped. Any other object is left out with a console
     * error (it used to be added as the text "[object Object]").
     * @returns {HTMLElement} The new element.
     */
    function el(tag, props, ...children) {
        const parts = tag.split(".");
        const e = document.createElement(parts[0] || "div");
        if (parts.length > 1)
            e.className = parts.slice(1).join(" ");
        if (props && (props instanceof Node || typeof props === "string" || Array.isArray(props) || (props.E instanceof Node))) {
            children.unshift(props);
            props = null;
        }
        if (props) {
            for (const [k, v] of Object.entries(props)) {
                if (v === undefined || v === null)
                    continue;
                switch (k) {
                    case "class": e.className = (e.className ? e.className + " " : "") + v; break;
                    case "text": e.textContent = v; break;
                    case "html": e.innerHTML = v; break;
                    case "attrs": for (const [a, av] of Object.entries(v)) if (av !== null && av !== undefined && av !== false) e.setAttribute(a, av === true ? "" : av); break;
                    case "style": Object.assign(e.style, v); break;
                    case "dataset": Object.assign(e.dataset, v); break;
                    case "on": for (const [ev, fn] of Object.entries(v)) if (fn) e.addEventListener(ev, fn); break;
                    default: e[k] = v; break;
                }
            }
        }
        appendChildren(e, children);
        return e;
    }

    /**
     * Append the children of Sw.el: flattens arrays, adds a component's E, skips null / undefined / false.
     * @param {HTMLElement} e The parent.
     * @param {Array} list The children.
     */
    function appendChildren(e, list) {
        for (const c of list) {
            if (c === null || c === undefined || c === false)
                continue;
            if (Array.isArray(c))
                appendChildren(e, c);
            else if (c instanceof Node || typeof c !== "object")
                e.append(c);
            else if (c.E instanceof Node)
                e.append(c.E);
            else
                console.error("Sw.el(\"" + e.tagName.toLowerCase() + "\"): a child must be a Node, a text, an array or a component with an E node, not", c);
        }
    }

    /**
     * Clamp a value to a range.
     * @param {number} v Value.
     * @param {number} a Minimum.
     * @param {number} b Maximum.
     * @returns {number} The clamped value.
     */
    const clamp = (v, a, b) => Math.min(b, Math.max(a, v));

    /**
     * Round to a step (relative to min), avoids float noise.
     * @param {number} v Value.
     * @param {number} step Step (0 or less = no snapping).
     * @param {number} [min] The value the steps are relative to (default 0).
     * @returns {number} The snapped value.
     */
    function snap(v, step, min) {
        if (!(step > 0))
            return v;
        const n = Math.round((v - (min || 0)) / step);
        const r = (min || 0) + n * step;
        const d = Math.max(0, Math.min(10, -Math.floor(Math.log10(step)) + 1));
        return parseFloat(r.toFixed(d));
    }

    /**
     * Format a number with the precision of a step.
     * @param {number} v Value.
     * @param {number} step Step (integers show no decimals, 0 shows 3 decimals).
     * @returns {string} The text ("" for non finite values).
     */
    function formatNumber(v, step) {
        if (typeof v !== "number" || !isFinite(v))
            return "";
        if (step >= 1 && Number.isInteger(step))
            return String(Math.round(v));
        const d = step > 0 ? Math.max(0, Math.min(6, Math.ceil(-Math.log10(step) - 1e-9))) : 3;
        return v.toFixed(d);
    }

    // ------------------------------------------------------------------ icons

    /** @type {Object<string, string>} Inline svg icons (24x24, stroked): name -> svg inner markup. Sw.Icons.Add registers more. */
    const IconPaths = {
        menu: '<path d="M4 6h16M4 12h16M4 18h16"/>',
        more: '<circle cx="5" cy="12" r="1.5"/><circle cx="12" cy="12" r="1.5"/><circle cx="19" cy="12" r="1.5"/>',
        moreV: '<circle cx="12" cy="5" r="1.5"/><circle cx="12" cy="12" r="1.5"/><circle cx="12" cy="19" r="1.5"/>',
        plus: '<path d="M12 5v14M5 12h14"/>',
        minus: '<path d="M5 12h14"/>',
        close: '<path d="M6 6l12 12M18 6L6 18"/>',
        check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
        left: '<path d="M15 5l-7 7 7 7"/>',
        right: '<path d="M9 5l7 7-7 7"/>',
        up: '<path d="M5 15l7-7 7 7"/>',
        down: '<path d="M5 9l7 7 7-7"/>',
        eye: '<path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
        eyeOff: '<path d="M3 3l18 18M10.6 5.1A10.4 10.4 0 0 1 12 5c6.4 0 10 7 10 7a17 17 0 0 1-3.2 4.1M6.6 6.6C3.8 8.4 2 12 2 12s3.6 7 10 7c1.9 0 3.5-.6 4.9-1.4M9.9 9.9a3 3 0 0 0 4.2 4.2"/>',
        save: '<path d="M5 3h11l3 3v15H5z"/><path d="M8 3v6h8V3M8 21v-7h8v7"/>',
        open: '<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>',
        trash: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>',
        copy: '<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V5a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3"/>',
        edit: '<path d="M4 20h4L19 9l-4-4L4 16z"/><path d="M13.5 6.5l4 4"/>',
        reset: '<path d="M4 12a8 8 0 1 0 2.4-5.7"/><path d="M4 4v4h4"/>',
        history: '<path d="M3 12a9 9 0 1 0 2.6-6.4"/><path d="M3 4v4h4M12 7v5l3 2"/>',
        fullscreen: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>',
        fullscreenExit: '<path d="M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5"/>',
        window: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 9h18"/>',
        external: '<path d="M14 4h6v6M20 4l-9 9"/><path d="M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/>',
        panic: '<path d="M12 3l9.5 17h-19z"/><path d="M12 10v4M12 17.5v.5"/>',
        warning: '<path d="M12 3l9.5 17h-19z"/><path d="M12 10v4M12 17.5v.5"/>',
        play: '<path d="M7 4l13 8-13 8z"/>',
        pause: '<path d="M7 4v16M17 4v16"/>',
        layers: '<path d="M12 3l9 5-9 5-9-5z"/><path d="M3 13l9 5 9-5"/>',
        sliders: '<path d="M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0"/><circle cx="16" cy="6" r="2"/><circle cx="10" cy="12" r="2"/><circle cx="18" cy="18" r="2"/>',
        star: '<path d="M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z"/>',
        search: '<circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/>',
        theme: '<circle cx="12" cy="12" r="9"/><path d="M12 3a9 9 0 0 0 0 18z" fill="currentColor"/>',
        bolt: '<path d="M13 2L4 14h7l-1 8 9-12h-7z"/>',
        speed: '<path d="M4 18a9 9 0 1 1 16 0"/><path d="M12 14l4-5"/>',
        mouse: '<rect x="6" y="3" width="12" height="18" rx="6"/><path d="M12 7v4"/>',
        drag: '<path d="M9 6h0M15 6h0M9 12h0M15 12h0M9 18h0M15 18h0" stroke-width="3"/>',
        info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5v.5"/>',
        help: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6v.6M12 17v.5"/>',
        cue: '<path d="M6 21V4M6 4h11l-3 4 3 4H6"/>',
        toCue: '<path d="M4 4v16M20 5l-10 7 10 7z"/>',
        download: '<path d="M12 4v11M7 10l5 5 5-5"/><path d="M5 20h14"/>',
        upload: '<path d="M12 16V5M7 10l5-5 5 5"/><path d="M5 20h14"/>',
        strobe: '<path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9L7 7M17 17l2.1 2.1M4.9 19.1L7 17M17 7l2.1-2.1"/><circle cx="12" cy="12" r="4"/>',
        code: '<path d="M8 7l-5 5 5 5M16 7l5 5-5 5M14 4l-4 16"/>',
        wand: '<path d="M4 20L15 9M13 7l2-2 4 4-2 2z"/><path d="M6 3v3M4.5 4.5h3M19 14v3M17.5 15.5h3"/>',
        file: '<path d="M6 3h8l5 5v13H6z"/><path d="M14 3v5h5"/>',
        newFile: '<path d="M6 3h8l5 5v13H6z"/><path d="M14 3v5h5M12.5 11v7M9 14.5h7"/>',
        log: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3M12 15h5"/>',
        book: '<path d="M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z"/><path d="M4 19V5M8 7h7"/>',
        puzzle: '<path d="M4 8h4a2 2 0 1 1 4 0h4v4a2 2 0 1 1 0 4v4H4v-4a2 2 0 1 0 0-4z"/>',
        dock: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 9h18M12 9v11"/>',
        camera: '<path d="M4 8h3l2-3h6l2 3h3v11H4z"/><circle cx="12" cy="13" r="3.5"/>',
        replace: '<path d="M4 7h11l-3-3M20 17H9l3 3"/>',
        undo: '<path d="M9 14L4 9l5-5"/><path d="M4 9h11a5 5 0 0 1 0 10h-3"/>',
        redo: '<path d="M15 14l5-5-5-5"/><path d="M20 9H9a5 5 0 0 0 0 10h3"/>',
        target: '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M2 12h3M19 12h3"/>',
        sparkle: '<path d="M10 3l1.8 5.2L17 10l-5.2 1.8L10 17l-1.8-5.2L3 10l5.2-1.8z"/><path d="M18 14l.9 2.1L21 17l-2.1.9L18 20l-.9-2.1L15 17l2.1-.9z"/>',
        send: '<path d="M4 12L20 4l-5 16-3-7z"/><path d="M12 13l8-9"/>',
        stop: '<rect x="6" y="6" width="12" height="12" rx="1.5"/>',
        key: '<circle cx="8" cy="15" r="4"/><path d="M11 12l9-9M16 7l3 3M14 9l2 2"/>',
        user: '<circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/>',
        users: '<circle cx="9" cy="8" r="3.5"/><path d="M2 20a7 7 0 0 1 14 0"/><path d="M16 4.5a3.5 3.5 0 0 1 0 7M18 13.5a7 7 0 0 1 4 6.5"/>',
        settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
        home: '<path d="M3 11l9-8 9 8"/><path d="M5 10v10h5v-6h4v6h5V10"/>',
        lock: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
        unlock: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 7.5-2"/>',
        login: '<path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4M10 17l5-5-5-5M15 12H3"/>',
        logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>',
        power: '<path d="M12 3v9"/><path d="M6.3 6.3a8 8 0 1 0 11.4 0"/>',
        link: '<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>',
        attach: '<path d="M20.5 11.5l-8.6 8.6a5.5 5.5 0 0 1-7.8-7.8l8.6-8.6a3.7 3.7 0 0 1 5.2 5.2l-8.6 8.6a1.8 1.8 0 0 1-2.6-2.6l8-8"/>',
        mic: '<rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/>',
        share: '<circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4"/>',
        filter: '<path d="M3 5h18l-7 8v6l-4 2v-8z"/>',
        clearInput: '<circle cx="12" cy="12" r="9"/><path d="M9 9l6 6M15 9l-6 6"/>',
        table: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18M3 15h18M9 10v10"/>',
        chart: '<path d="M4 20V11M10 20V4M16 20v-7M2 20h20"/>',
        globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/>',
        fade: '<path d="M3 5v14h18L3 5z"/><path d="M3 12h9"/>',
        moon: '<path d="M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z"/>',
        sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
        clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
        calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/>',
        mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/>',
        image: '<rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="9" cy="10" r="2"/><path d="M21 16l-5-5-9 9"/>',
        db: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>',
        bell: '<path d="M6 16v-5a6 6 0 0 1 12 0v5l2 2H4z"/><path d="M10 21h4"/>',
        pin: '<path d="M9 4h6l-1 6 3 3H7l3-3z"/><path d="M12 13v8"/>',
        server: '<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',
        refresh: '<path d="M20 12a8 8 0 1 1-2.4-5.7"/><path d="M20 4v4h-4"/>',
        //  Design v2 line set additions (shell chrome, tables, dashboards, editors; 24 grid, drawn with the 1.75 stroke of svg.sw-icon)
        grid: '<rect x="3.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="3.5" y="13.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="13.5" width="7" height="7" rx="1.5"/>',
        cpu: '<rect x="6" y="6" width="12" height="12" rx="1.5"/><rect x="9.5" y="9.5" width="5" height="5"/><path d="M9 2.5v3M15 2.5v3M9 18.5v3M15 18.5v3M2.5 9h3M2.5 15h3M18.5 9h3M18.5 15h3"/>',
        memory: '<rect x="3" y="7" width="18" height="10" rx="1.5"/><path d="M7.5 7v10M12 7v10M16.5 7v10M6 17v3M18 17v3"/>',
        drive: '<rect x="2.5" y="13" width="19" height="7" rx="2"/><path d="M5.5 13 8 5h8l2.5 8"/><path d="M6.5 16.5h.01M10 16.5h.01" stroke-width="2.5"/>',
        list: '<path d="M9 6h12M9 12h12M9 18h12"/><path d="M4 6h.01M4 12h.01M4 18h.01" stroke-width="2.5"/>',
        alert: '<path d="M10.3 4 2.6 17.5A2 2 0 0 0 4.3 20.5h15.4a2 2 0 0 0 1.7-3L13.7 4a2 2 0 0 0-3.4 0Z"/><path d="M12 9.5v4"/><path d="M12 17h.01" stroke-width="2.5"/>',
        gauge: '<path d="M3.5 18a8.5 8.5 0 1 1 17 0"/><path d="m12 18 4-6"/>',
        chartLine: '<path d="M3 3v18h18"/><path d="m7 15 4-4 3 3 5-6"/>',
        shield: '<path d="M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6Z"/>',
        columns: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16M15 4v16"/>',
        rows: '<path d="M3 5h18M3 10h18M3 15h18M3 20h18"/>',
        grip: '<path d="M9 6h.01M15 6h.01M9 12h.01M15 12h.01M9 18h.01M15 18h.01" stroke-width="3"/>',
        kebab: '<path d="M12 5h.01M12 12h.01M12 19h.01" stroke-width="3"/>',
        first: '<path d="m11 6-6 6 6 6M18 6l-6 6 6 6"/>',
        last: '<path d="m13 6 6 6-6 6M6 6l6 6-6 6"/>',
        sort: '<path d="M12 5v14M6 13l6 6 6-6"/>',
        sidebar: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16"/>',
        collapseAll: '<path d="m7 20 5-5 5 5M7 4l5 5 5-5"/>',
        expandAll: '<path d="m7 15 5 5 5-5M7 9l5-5 5 5"/>',
        kill: '<circle cx="12" cy="12" r="9"/><path d="m9 9 6 6M15 9l-6 6"/>',
        braces: '<path d="M8 3H7a2 2 0 0 0-2 2v4a2 2 0 0 1-2 2 2 2 0 0 1 2 2v4a2 2 0 0 0 2 2h1M16 3h1a2 2 0 0 1 2 2v4a2 2 0 0 0 2 2 2 2 0 0 0-2 2v4a2 2 0 0 1-2 2h-1"/>',
        folder: '<path d="M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5Z"/>',
        format: '<path d="M4 6h16M4 10h10M4 14h16M4 18h10"/>',
        density: '<path d="M4 5h16M4 9.5h16M4 14h16M4 18.5h16"/><path d="M2 3v18M22 3v18" stroke-opacity=".45"/>',
        circle: '<circle cx="12" cy="12" r="9"/>',
        //  Line twins of the IconModule menu icons (LineIcons): the menu and the rail draw every framework page in this one outline style
        chat: '<path d="M4 5h16v11H9l-5 4z"/><path d="M8 9.5h8M8 12.5h5"/>',
        gradient: '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 15 15 3M3 21 21 3M9 21 21 9"/>',
        banknote: '<rect x="2" y="6" width="20" height="12" rx="2"/><circle cx="12" cy="12" r="2.5"/><path d="M6 9.5v5M18 9.5v5"/>',
        banknotes: '<rect x="2" y="9" width="16" height="10" rx="2"/><circle cx="10" cy="14" r="2.2"/><path d="M5.5 5.5H20a2 2 0 0 1 2 2V15"/>',
        compass: '<circle cx="12" cy="12" r="9"/><path d="m15.5 8.5-2 5-5 2 2-5z"/>',
        folderCheck: '<path d="M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5Z"/><path d="m9 13.5 2 2 4-4"/>',
        folderUser: '<path d="M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5Z"/><circle cx="12" cy="12" r="1.8"/><path d="M9 17.5a3 3 0 0 1 6 0"/>',
        folderArrow: '<path d="M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5Z"/><path d="m10 16 4.5-4.5M11 11.5h3.5V15"/>',
        palette: '<path d="M12 3a9 9 0 1 0 0 18c1.1 0 1.5-.8 1.5-1.5 0-.9-.7-1.2-.7-2s.6-1.5 1.5-1.5H17a4 4 0 0 0 4-4c0-5-4-9-9-9z"/><path d="M7.5 11h.01M10 7h.01M15 7.5h.01" stroke-width="2.5"/>',
        userAdd: '<circle cx="10" cy="8" r="4"/><path d="M3 21a7 7 0 0 1 11.5-5.4M19 14v6M16 17h6"/>',
        userKey: '<circle cx="9" cy="8" r="4"/><path d="M2 21a7 7 0 0 1 11-5.7"/><circle cx="17" cy="17" r="2.2"/><path d="M18.6 15.4 21.5 12.5M20.3 13.7l1 1"/>',
        fileKey: '<path d="M6 3h8l5 5v13H6z"/><path d="M14 3v5h5"/><circle cx="10" cy="16" r="2"/><path d="m11.5 14.5 3.5-3.5M14 12l1.5 1.5"/>',
        fileText: '<path d="M6 3h8l5 5v13H6z"/><path d="M14 3v5h5M9 12.5h6M9 15.5h6M9 18.5h3.5"/>',
        fileCode: '<path d="M6 3h8l5 5v13H6z"/><path d="M14 3v5h5M10.5 12.5 8.5 15l2 2.5M14.5 12.5l2 2.5-2 2.5"/>',
        template: '<path d="M6 3h8l5 5v13H6z"/><path d="M14 3v5h5M11 12h-.5a1 1 0 0 0-1 1v1.2l-.8.8.8.8V18a1 1 0 0 0 1 1h.5M14 12h.5a1 1 0 0 1 1 1v1.2l.8.8-.8.8V18a1 1 0 0 1-1 1H14"/>',
        refund: '<path d="M4 12a8 8 0 1 0 2.4-5.7"/><path d="M4 4v4h4M14 9.5h-2.5a1.5 1.5 0 0 0 0 3h1a1.5 1.5 0 0 1 0 3H10M12 8v1.5M12 15.5V17"/>',
        dbClock: '<ellipse cx="11" cy="5" rx="7" ry="2.5"/><path d="M4 5v12c0 1.4 3.1 2.5 7 2.5M18 5v4.5M4 11c0 1.4 3.1 2.5 7 2.5"/><circle cx="17.5" cy="16.5" r="4"/><path d="M17.5 14.5v2l1.5 1"/>',
        monitor: '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>',
        screen: '<rect x="2" y="5" width="20" height="13" rx="1.5"/><path d="M7 21h10"/>',
        archive: '<rect x="3" y="4" width="18" height="5" rx="1"/><path d="M5 9v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V9M10 13h4"/>',
        flag: '<path d="M5 21V4"/><path d="M5 4.5c3-1.5 5 1.5 8 0s4.5-.8 6 0v8c-1.5-.8-3-1.5-6 0s-5-1.5-8 0"/>',
        coins: '<ellipse cx="9" cy="6.5" rx="6" ry="2.5"/><path d="M3 6.5v4c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5v-4M9 17c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5v-4c0-1.3-2.1-2.3-5-2.5M9 13v4"/>',
        language: '<path d="M3 5h9M7.5 3v2M5 5c.5 3 2.8 5.8 5.8 7.2M10 5c-.8 3.4-3 6.2-6 7.7"/><path d="m13 21 4-9 4 9M14.4 18h5.2"/>',
        translateFrom: '<path d="M3 5h9M7.5 3v2M5 5c.5 3 2.8 5.8 5.8 7.2M10 5c-.8 3.4-3 6.2-6 7.7"/><path d="M13 17h8M18 14l3 3-3 3"/>',
        translateTo: '<path d="M12 5h9M16.5 3v2M14 5c.5 3 2.8 5.8 5.8 7.2M19 5c-.8 3.4-3 6.2-6 7.7"/><path d="M3 17h8M8 14l3 3-3 3"/>',
        phone: '<path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a1 1 0 0 1-1 1A16 16 0 0 1 4 5a1 1 0 0 1 1-1z"/>',
        binary: '<rect x="4" y="4" width="5" height="7" rx="2.5"/><rect x="15" y="13" width="5" height="7" rx="2.5"/><path d="M16.5 4v7M15 5.5l1.5-1.5M6.5 13v7M5 14.5 6.5 13"/>',
        session: '<rect x="3" y="4" width="18" height="13" rx="2"/><circle cx="12" cy="9.5" r="2.3"/><path d="M8.5 17a3.5 3.5 0 0 1 7 0M9 21h6"/>',
        tables: '<rect x="7" y="8" width="14" height="12" rx="2"/><path d="M7 13h14M12 13v7M4 16.5V6a2 2 0 0 1 2-2h11"/>',
        network: '<rect x="9" y="3" width="6" height="5" rx="1"/><rect x="3" y="16" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M6 16v-4h12v4"/>',
        text: '<path d="M5 7V4h14v3M12 4v16M9 20h6"/>',
        //  The app logo (IconLogo: the app's full colour icon.svg) in menus: a mask of a filled logo is a solid block, so menus draw this
        logo: '<path d="M12 3 19.8 7.5v9L12 21l-7.8-4.5v-9z"/><path d="M12 8.2 15.3 10v4L12 15.8 8.7 14v-4z"/>',
    };

    /**
     * The line twins of the IconModule menu icons, by svg file name (lower case): the shell menu, the rail and the menus of the top
     * bar draw these instead of the server's mixed filled / outline art, so every framework page reads in one outline style (Design
     * v2). Each file gets its own glyph, so pages that had distinct icons still do. An icon that is not listed (an app's own) keeps its
     * server art, drawn as a monochrome mask (WebBranding / PORTING.md "Icons" say how to draw one that fits).
     */
    const LineIcons = Object.freeze({
        "home.svg": "home", "warning.svg": "alert", "table_log.svg": "list", "log_file.svg": "fileText", "table_stats.svg": "chart",
        "chart.svg": "chartLine", "table_cache.svg": "db", "cache_session.svg": "dbClock", "table_performance.svg": "speed",
        "table_perf_system.svg": "gauge", "table_session.svg": "session", "user.svg": "user", "users.svg": "users",
        "services_loaded.svg": "puzzle", "table_services.svg": "cpu", "clock.svg": "clock", "settings.svg": "settings",
        "protect.svg": "shield", "key.svg": "key", "key_file.svg": "fileKey", "lock.svg": "lock", "book.svg": "book",
        "compile.svg": "code", "table.svg": "table", "table_tables.svg": "tables", "table_refs.svg": "link", "table_api.svg": "braces",
        "table_serializer.svg": "binary", "table_compression.svg": "archive", "table_template.svg": "template",
        "table_country.svg": "flag", "table_currency.svg": "coins", "table_language.svg": "language", "phone.svg": "phone",
        "table_translate_from.svg": "translateFrom", "table_translate_to.svg": "translateTo", "explore.svg": "compass",
        "menu_folder.svg": "folder", "folder_managed.svg": "folderCheck", "folder_shared.svg": "folderUser",
        "folder_remote.svg": "folderArrow", "organize.svg": "open", "ssd.svg": "drive", "computer.svg": "monitor",
        "display.svg": "screen", "server.svg": "server", "network.svg": "network", "world.svg": "globe", "file_types.svg": "fileCode",
        "chat.svg": "chat", "email.svg": "mail", "image.svg": "image", "color_palette.svg": "palette", "gradient.svg": "gradient",
        "collage.svg": "grid", "upload.svg": "upload", "user_add.svg": "userAdd", "user_password.svg": "userKey",
        "user_logout.svg": "logout", "sync.svg": "refresh", "reload.svg": "refresh", "power.svg": "power", "brick.svg": "layers",
        "brain.svg": "sparkle", "text.svg": "text", "cash_bill.svg": "banknote", "cash_bills.svg": "banknotes", "refund.svg": "refund",
        "theme_light.svg": "sun", "theme_dark.svg": "moon", "theme_auto.svg": "theme", "theme_hacker.svg": "log", "theme.svg": "theme",
    });

    /** @type {Object<string, string>} Line twins added by apps (Sw.Icons.AddLine). */
    const lineExtra = {};

    /** @type {Map<string, string>} Icon class or url -> its svg file name (lower case, "" = none). */
    const iconFiles = new Map();

    /**
     * The svg file an icon class or url shows (an icon class is looked up in the loaded css, ex: IconHome -> "home.svg").
     * @param {string} image The icon class or url (a trailing '*' is ignored).
     * @returns {string} The file name (lower case), "" when unknown.
     */
    function iconFile(image) {
        let f = iconFiles.get(image);
        if (f !== undefined)
            return f;
        let s = String(image || "").replace(/\*$/, "");
        f = "";
        if (/[\/.]/.test(s))
            f = s.split(/[?#]/)[0];
        else if (/^Icon[A-Z0-9]/.test(s) && document.body) {
            const probe = document.createElement("span");
            probe.className = s;
            probe.style.cssText = "position:absolute;width:0;height:0;overflow:hidden";
            document.body.append(probe);
            const cs = getComputedStyle(probe);
            const m = /url\(["']?([^"')]*)/.exec(cs.maskImage || cs.webkitMaskImage || cs.backgroundImage || "");
            probe.remove();
            f = m ? m[1] : "";
            //  the css may not be applied yet: ask again later
            if (!f)
                return "";
        }
        f = f.substring(f.lastIndexOf("/") + 1).toLowerCase();
        iconFiles.set(image, f);
        return f;
    }

    /**
     * The line glyph (Sw.Icons name) that stands for a server icon (an IconModule class or url), see LineIcons.
     * @param {string} image The icon class or url.
     * @returns {string|null} The glyph name, null when the icon has no line twin.
     */
    function LineOf(image) {
        if (!image)
            return null;
        if (Object.prototype.hasOwnProperty.call(IconPaths, image))
            return image;
        //  The full colour app logo class: its mask is a filled block whatever the app's icon.svg looks like (AddLine("IconLogo", x))
        if (String(image).replace(/\*$/, "") === "IconLogo")
            return lineExtra["iconlogo"] || "logo";
        const f = iconFile(image);
        const n = lineExtra[f] || LineIcons[f];
        return n || null;
    }

    /**
     * A menu icon in the one outline style: the line twin of a server icon (LineOf) as an inline svg, else the server icon as a
     * monochrome mask (Sw.IconClass). Used by the shell menu, the rail and the top bar menus.
     * @param {string} image The icon class or url (a trailing '*' is dropped: menus are monochrome).
     * @param {string} [cls] Extra class(es).
     * @returns {Element} The icon.
     */
    function LineIcon(image, cls) {
        const n = LineOf(image);
        if (n)
            return Icon(n, cls);
        if (!isIconClass(image))
            return Icon(image, cls);
        return IconClass(String(image).replace(/\*$/, ""), { Class: cls });
    }

    /** The svg namespace. */
    const SvgNs = "http://www.w3.org/2000/svg";

    /**
     * Test if a name refers to a SysWeaver icon class ("IconReload") or an icon url ("../icons/x.svg", "icons/x.svg*").
     * @param {string} name Name to test.
     * @returns {boolean} True for class names starting with "Icon" + an upper case letter, and for urls (containing '.' or '/').
     */
    function isIconClass(name) {
        if (typeof name !== "string" || !name)
            return false;
        if (name.indexOf(".") >= 0 || name.indexOf("/") >= 0)
            return true;
        return /^Icon[A-Z0-9]/.test(name);
    }

    /**
     * Create an icon element: an inline svg for the built in names (Sw.Icons), else a mask icon (Sw.IconClass) for SysWeaver icon
     * class names (IconReload) and urls (../icons/x.svg; a trailing '*' shows the image in full color).
     * @param {string} name Icon name, icon class or url.
     * @param {string} [cls] Optional extra class(es).
     * An unknown name shows a neutral dashed square (class sw-icon-unknown, data-icon = the name) and logs one console
     * warning per name; it used to show the "info" icon, which reads as a real (wrong) meaning.
     * @returns {Element} An svg element (class sw-icon) or a span (class sw-icon sw-icon-mask).
     */
    function Icon(name, cls) {
        if (!IconPaths[name] && isIconClass(name))
            return IconClass(name, { Class: cls });
        const svg = document.createElementNS(SvgNs, "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        const known = Object.prototype.hasOwnProperty.call(IconPaths, name);
        svg.setAttribute("class", "sw-icon" + (known ? "" : " sw-icon-unknown") + (cls ? " " + cls : ""));
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("focusable", "false");
        if (!known) {
            svg.dataset.icon = String(name ?? "");
            if (name && !warnedIcons.has(name)) {
                warnedIcons.add(name);
                //  (an author mistake: the one developer warning channel, never shown in production)
                DevWarn("Sw.Icon: unknown icon name \"" + name + "\" (see Sw.Icons.List(); an icon class \"IconX\" or an url \"../icons/x.svg\" also works).", "icon-unknown:" + name);
            }
        }
        svg.innerHTML = known ? IconPaths[name] : UnknownIconPath;
        return svg;
    }

    /** The neutral glyph of an unknown icon name: a dashed rounded square. */
    const UnknownIconPath = '<rect x="5" y="5" width="14" height="14" rx="3" stroke-dasharray="3 3"/>';

    /** @type {Set<string>} Unknown icon names already warned about (one console warning per name). */
    const warnedIcons = new Set();

    /** Registry of the inline svg icons. */
    const Icons = {
        /**
         * Register an inline svg icon.
         * @param {string} name Icon name.
         * @param {string} svgInner The inner svg markup (24x24 view box, stroked with currentColor).
         */
        Add(name, svgInner) {
            IconPaths[name] = svgInner;
        },
        /**
         * Test if an inline svg icon exists.
         * @param {string} name Icon name.
         * @returns {boolean} True if registered.
         */
        Has: name => !!IconPaths[name],
        /**
         * List the names of the inline svg icons.
         * @returns {string[]} The names, sorted.
         */
        List: () => Object.keys(IconPaths).sort(),
        /**
         * The line twin of a server icon (an IconModule class or url): its Sw.Icons name, null when there is none (see LineIcon).
         * @param {string} image The icon class or url.
         * @returns {string|null} The name.
         */
        LineOf: image => LineOf(image),
        /**
         * Give a server icon file a line twin (apps that ship their own menu icons): LineIcons(file, name).
         * @param {string} file The svg file name (ex: "orders.svg").
         * @param {string} name The Sw.Icons name (register a new one with Add).
         */
        AddLine(file, name) {
            lineExtra[String(file).toLowerCase()] = name;
        },
    };

    /** @type {Object<string, string>} Old SysWeaver color classes (ColorIcon) mapped to css colors. */
    const IconColors = {
        IconColorThemeBackground: "var(--sw-bg)",
        IconColorThemeMain: "var(--sw-fg)",
        IconColorThemeAcc1: "var(--sw-accent1)",
        IconColorThemeAcc2: "var(--sw-accent2)",
    };

    /**
     * Set (or replace) the image of a mask icon element.
     * The image is a SysWeaver icon class (a css class setting mask-image, see ui/icons.css), or an url used as mask-image,
     * or an url ending with '*' used as a full color background image. A falsy image hides the element (visibility hidden).
     * @param {HTMLElement} e The icon element (created by Sw.IconClass).
     * @param {string} image The icon class or url.
     * @returns {HTMLElement} The element.
     */
    function SetIconClass(e, image) {
        const st = e.style;
        const prev = e.SwIconClass;
        if (prev && /^Icon[A-Z0-9]/.test(prev) && prev.indexOf(".") < 0 && prev.indexOf("/") < 0)
            e.classList.remove(prev);
        e.SwIconClass = image || null;
        st.maskImage = "";
        st.backgroundImage = "";
        e.classList.remove("sw-icon-color");
        if (!image) {
            st.visibility = "hidden";
            return e;
        }
        st.visibility = "";
        if (image.indexOf(".") >= 0 || image.indexOf("/") >= 0) {
            if (image.charAt(image.length - 1) === "*") {
                st.backgroundImage = "url(\"" + image.substring(0, image.length - 1).replace(/"/g, "%22") + "\")";
                e.classList.add("sw-icon-color");
            }
            else
                st.maskImage = "url(\"" + image.replace(/"/g, "%22") + "\")";
        }
        else
            e.classList.add(image);
        return e;
    }

    /**
     * Create a mask icon from a SysWeaver icon class (ex: "IconReload", see ui/icons.css) or an url (a trailing '*' = full color image).
     * The icon is drawn with the current text color (or Color), 1.35em square unless Size is given.
     * @param {string} image Icon class or url.
     * @param {object} [o] Options: { Size (px number or css length), Color (css color or an old color class like "IconColorThemeAcc1"),
     * Title (tooltip, also makes the icon accessible: role img + aria-label), Class (extra classes) }.
     * @returns {HTMLSpanElement} The icon element (class sw-icon sw-icon-mask).
     */
    function IconClass(image, o) {
        optCheck("Sw.IconClass", o);
        o = o || {};
        const e = document.createElement("span");
        e.className = "sw-icon sw-icon-mask" + (o.Class ? " " + o.Class : "");
        if (o.Title) {
            e.title = o.Title;
            e.setAttribute("role", "img");
            e.setAttribute("aria-label", o.Title);
        }
        else
            e.setAttribute("aria-hidden", "true");
        if (o.Size) {
            const s = typeof o.Size === "number" ? o.Size + "px" : o.Size;
            e.style.width = s;
            e.style.height = s;
        }
        if (o.Color)
            e.style.color = IconColors[o.Color] || o.Color;
        return SetIconClass(e, image);
    }

    // ------------------------------------------------------------------ events and input

    /**
     * Test if an event has no modifier keys (shift, alt, ctrl, meta) held (the old isPureClick).
     * @param {Event} [ev] The event (missing = pure).
     * @returns {boolean} True if no modifier is held.
     */
    function IsPureEvent(ev) {
        return !ev || !(ev.shiftKey || ev.altKey || ev.ctrlKey || ev.metaKey);
    }

    /**
     * Common click guard (the old badClick): unless keepProp, calls preventDefault and stopPropagation on the event,
     * then reports whether a modifier key is held (i.e. the click should be ignored by "plain click" handlers).
     * @param {Event} [ev] The event (missing = not bad).
     * @param {boolean} [keepProp] Don't prevent the default action or stop the propagation.
     * @returns {boolean} True if a modifier key is held.
     */
    function BadClick(ev, keepProp) {
        if (!ev)
            return false;
        if (!keepProp) {
            ev.preventDefault();
            ev.stopPropagation();
        }
        const bad = !IsPureEvent(ev);
        //  A modifier click moves the focus (UX guide INP-2); the caller then ignores the click
        if (bad && ev.type === "click")
            ModifierClick(ev);
        return bad;
    }

    /** Interactive elements a modifier click focuses (tabindex -1 only counts for the item roles below). */
    const InteractiveSel = "a[href],area[href],button,input,select,textarea,summary,iframe,[contenteditable=''],[contenteditable='true']," +
        "[tabindex]:not([tabindex='-1']),[role=button],[role=link],[role=switch],[role=checkbox],[role=radio],[role=tab],[role=menuitem],[role=slider]";

    /** The list / table items a modifier click focuses when it hit no interactive element (cell, row, card, list item, tree node). */
    const ItemSel = "[role=gridcell],[role=cell],[role=rowheader],[role=columnheader],[role=row],[role=option],[role=treeitem],[role=listitem],[data-sw-item],td,th,li,.sw-card";

    /**
     * The element a modifier click focuses: the interactive element under the pointer, else the list / table item it belongs to, else
     * the element the handler is bound to when it is interactive; null for static text (the browser's behaviour: no focus).
     * @param {Event} ev The click event.
     * @returns {HTMLElement|null} The element.
     */
    function modifierTarget(ev) {
        let t = ev.target;
        if (t && t.nodeType !== 1)
            t = t.parentElement;
        if (!t || !t.closest)
            return null;
        const bound = ev.currentTarget instanceof Element ? ev.currentTarget : null;
        const hit = t.closest(InteractiveSel);
        if (hit && !hit.matches(":disabled"))
            return hit;
        const item = t.closest(ItemSel);
        //  An item outside the element whose handler runs (a delegated handler on a container) is still the clicked item
        if (item && (!bound || bound.contains(item) || item.contains(bound)))
            return item;
        if (bound && bound.matches(InteractiveSel + ",[tabindex]") && !bound.matches(":disabled"))
            return bound;
        return null;
    }

    /**
     * The one handling of a modifier click (UX guide INP-2): a left click with Shift, Ctrl, Alt or Meta held moves the keyboard focus to
     * the clicked interactive element (else to its list / table item) and shows it (Sw.Focus.Show), and never runs the pure click's
     * action. Sw.BadClick calls it for click events; call it yourself in handlers that test Sw.IsPureEvent:
     * `if (Sw.ModifierClick(ev)) return;`. A click with a modifier sent by the keyboard (Ctrl+Enter, detail 0) is a key combination, not
     * a modifier click: no focus change, but the result is still true (the pure action must not run).
     * @param {Event} ev The click event.
     * @param {HTMLElement} [target] The element to focus (default: found from the event).
     * @returns {boolean} True if the event was a modifier click (the caller must not act), false for a pure click.
     */
    function ModifierClick(ev, target) {
        if (!ev || IsPureEvent(ev))
            return false;
        if (ev.type === "click" && ev.detail === 0 && !ev.pointerType)
            return true;
        const e = target || modifierTarget(ev);
        if (e && Focus.Allowed(e))
            Focus.Show(e);
        return true;
    }

    /** @type {HTMLElement|null} The element that shows the fallback focus ring (class sw-focus-shown). */
    let focusShownE = null;

    /** Remove the fallback focus ring (next pointer down, or the element lost the focus). */
    function clearFocusShown() {
        const e = focusShownE;
        focusShownE = null;
        document.removeEventListener("pointerdown", clearFocusShown, true);
        if (e) {
            e.classList.remove("sw-focus-shown");
            e.removeEventListener("blur", clearFocusShown);
        }
    }

    /**
     * Make a non button element keyboard clickable (the old keyboardClick): tabindex 0 (if not set), class sw-keyclick,
     * Enter (on key down) and Space (on key up) without modifiers click it. Calling it twice is harmless.
     * @param {HTMLElement} e The element.
     * @param {function(Event):void} [onClick] Optional click handler to add.
     * @returns {HTMLElement} The element (for chaining).
     */
    function KeyClick(e, onClick) {
        if (onClick)
            e.addEventListener("click", onClick);
        if (e.SwKeyClick)
            return e;
        if (!e.hasAttribute("tabindex"))
            e.tabIndex = 0;
        e.classList.add("sw-keyclick");
        /** Enter (key down) clicks the element; Space only prevents scrolling here. */
        const down = ev => {
            if (ev.target !== e || !IsPureEvent(ev))
                return;
            if (ev.key === "Enter") {
                ev.preventDefault();
                e.click();
            }
            else if (ev.key === " ")
                ev.preventDefault();
        };
        /** Space (key up) clicks the element. */
        const up = ev => {
            if (ev.target !== e || !IsPureEvent(ev) || ev.key !== " ")
                return;
            ev.preventDefault();
            e.click();
        };
        e.addEventListener("keydown", down);
        e.addEventListener("keyup", up);
        e.SwKeyClick = () => {
            e.removeEventListener("keydown", down);
            e.removeEventListener("keyup", up);
            e.classList.remove("sw-keyclick");
            e.removeAttribute("tabindex");
            e.SwKeyClick = null;
        };
        return e;
    }

    /**
     * Undo Sw.KeyClick (the old removeKeyboardClick).
     * @param {HTMLElement} e The element.
     * @returns {HTMLElement} The element.
     */
    function RemoveKeyClick(e) {
        if (e.SwKeyClick)
            e.SwKeyClick();
        return e;
    }

    /**
     * Make an accessible name from a tool tip: drops a leading "Click to " / "Click here to " / "Click " (tool tips describe the mouse
     * action, a name says what the control does), a trailing period and everything after the first blank line, and starts with a capital
     * ("Click to sort by Name." -> "Sort by Name"). Text in other languages is returned unchanged (apart from the period and extra lines).
     * @param {?string} title The tool tip text.
     * @returns {string} The name ("" for no text).
     */
    function ActionName(title) {
        let s = String(title || "").split(/\n\s*\n/)[0].trim();
        s = s.replace(/^click\s+(here\s+)?(to\s+)?/i, "").replace(/\.$/, "").trim();
        return s ? s.charAt(0).toUpperCase() + s.substring(1) : "";
    }

    /**
     * Is a contextmenu event a pure right click (UX guide INP-3)? A right click with Shift, Ctrl, Alt or Meta held is left to the browser
     * (its menu: Inspect, copy, spell check); on Apple platforms Ctrl + click is the right click, so Ctrl alone counts as pure there.
     * A context menu from the keyboard (Menu key, Shift+F10) or a touch long press is pure.
     * @param {MouseEvent} ev The contextmenu event.
     * @returns {boolean} True if the element's own menu may open.
     */
    function IsPureContextMenu(ev) {
        if (!ev || IsPureEvent(ev))
            return true;
        //  Shift+F10 sends a contextmenu event with shiftKey on some browsers: a keyboard menu (no pointer position)
        if (ev.shiftKey && !ev.ctrlKey && !ev.altKey && !ev.metaKey && !ev.clientX && !ev.clientY)
            return true;
        return isMac && ev.ctrlKey && !ev.shiftKey && !ev.altKey && !ev.metaKey;
    }

    /**
     * Long press (touch or pen hold), a pure right click (mouse), or the context menu key / Shift+F10 (keyboard) on an element (UX guide
     * INP-3, INP-5). The click that follows a long press is suppressed. Nested elements with a long press: the innermost one handles the event (ex: a ⋮ menu button in a bar that has a menu of its own). A right click with a modifier key is left alone, so the browser's
     * menu appears (Sw.IsPureContextMenu). A mouse hold does nothing (a mouse acts on release, INP-7). An aria-disabled element shows
     * its reason instead (Sw.Disabled.Reject).
     * @param {HTMLElement} element The element.
     * @param {function(Event):void} fn Called with the triggering event (pointer, contextmenu or keyboard event).
     * @param {number|object} [ms] Hold duration in ms (default 550), or options { Ms, Skip(ev) (return true to leave this event to the
     * browser, ex: a text selection inside the element), Mouse (true: a mouse hold fires too, the old behaviour) }.
     * @returns {function():void} Call to remove the handlers.
     */
    function LongPress(element, fn, ms) {
        const o = ms && typeof ms === "object" ? ms : { Ms: ms };
        ms = o.Ms || 550;
        const skip = ev => !!(o.Skip && o.Skip(ev));
        /** Run the action, or show the reason of a disabled element. */
        const run = ev => {
            if (Disabled.Is(element)) {
                Disabled.Reject(element, ev);
                return;
            }
            fn(ev);
        };
        let timer = 0, sx = 0, sy = 0, fired = false;
        /** Cancel the pending long press timer. */
        const cancel = () => {
            clearTimeout(timer);
            timer = 0;
        };
        /** Pointer down: start the long press timer (primary button, touch and pen only unless Mouse). */
        const down = e => {
            if (e.button !== 0 || (e.pointerType === "mouse" && !o.Mouse) || !IsPureEvent(e) || skip(e) || e.SwLongPress)
                return;
            //  Nested long press elements (ex: a menu button in a bar with its own menu): the innermost one handles it
            e.SwLongPress = true;
            fired = false;
            sx = e.clientX;
            sy = e.clientY;
            cancel();
            timer = setTimeout(() => {
                timer = 0;
                fired = true;
                if (navigator.vibrate)
                    try {
                        navigator.vibrate(15);
                    }
                    catch {
                    }
                run(e);
            }, ms);
        };
        /** Pointer move: a move of more than 10 px cancels the long press. */
        const move = e => {
            if (timer && Math.hypot(e.clientX - sx, e.clientY - sy) > 10)
                cancel();
        };
        /** Click after a fired long press: swallowed. */
        const click = e => {
            if (fired) {
                fired = false;
                e.stopPropagation();
                e.preventDefault();
            }
        };
        /** Context menu (a pure right click with a mouse): runs the action instead of the browser menu; with a modifier the browser's menu shows. */
        const ctx = e => {
            cancel();
            if (!IsPureContextMenu(e) || (!fired && skip(e)) || e.SwLongPress)
                return;
            e.SwLongPress = true;
            e.preventDefault();
            if (e.pointerType !== "touch" && !fired)
                run(e);
        };
        /** Keyboard: the context menu key or Shift+F10 runs the action. */
        const key = e => {
            if (e.key === "ContextMenu" || (e.key === "F10" && e.shiftKey && !e.ctrlKey && !e.altKey && !e.metaKey)) {
                if (skip(e) || e.SwLongPress)
                    return;
                e.SwLongPress = true;
                e.preventDefault();
                run(e);
            }
        };
        element.addEventListener("pointerdown", down);
        element.addEventListener("pointermove", move);
        element.addEventListener("pointerup", cancel);
        element.addEventListener("pointercancel", cancel);
        element.addEventListener("pointerleave", cancel);
        element.addEventListener("click", click, true);
        element.addEventListener("contextmenu", ctx);
        element.addEventListener("keydown", key);
        return () => {
            cancel();
            element.removeEventListener("pointerdown", down);
            element.removeEventListener("pointermove", move);
            element.removeEventListener("pointerup", cancel);
            element.removeEventListener("pointercancel", cancel);
            element.removeEventListener("pointerleave", cancel);
            element.removeEventListener("click", click, true);
            element.removeEventListener("contextmenu", ctx);
            element.removeEventListener("keydown", key);
        };
    }

    /**
     * Disable the browser context menu of a touch long press (it would cover the page's own long press), outside text inputs, editable
     * content and selected text. A mouse right click and the keyboard keep the browser's menu (UX guide INP-4: elements without a menu
     * of their own never block it).
     * @returns {function():void} Call to enable the context menu again.
     */
    function NoContextMenu() {
        let touchAt = 0;
        /** Remember the last touch / pen press (older browsers send a contextmenu without a pointer type). */
        const down = e => {
            if (e.pointerType === "touch" || e.pointerType === "pen")
                touchAt = Date.now();
        };
        /** Context menu handler: prevents the browser menu of a touch long press outside text inputs. */
        const fn = e => {
            const t = e.target;
            const tag = t && t.tagName;
            const touch = e.pointerType ? (e.pointerType === "touch" || e.pointerType === "pen") : Date.now() - touchAt < 1500;
            if (!touch || tag === "INPUT" || tag === "TEXTAREA" || (t && t.isContentEditable) || HasSelectionIn(t && t.nodeType === 1 ? t : document.body))
                return;
            //  A menu button opens its menu instead (menuButtonOf, the global context menu handler below)
            if (menuButtonOf(t))
                return;
            e.preventDefault();
        };
        document.addEventListener("pointerdown", down, true);
        document.addEventListener("contextmenu", fn);
        return () => {
            document.removeEventListener("pointerdown", down, true);
            document.removeEventListener("contextmenu", fn);
        };
    }

    /**
     * Make an element a menu button (UX guide INP-3): a pure click, a pure right click, a long press (touch), the context menu key and
     * Shift+F10 open the menu (Sw.Menu.Show anchored to the element); aria-haspopup="menu" and aria-expanded (true while it is open).
     * A modifier click only focuses (INP-2), a right click with a modifier shows the browser's menu. Sw.Button({ Menu }) uses it.
     * @param {HTMLElement} e The element (a button, or any element made keyboard clickable).
     * @param {Array|function(Event):(Array|Promise<Array>)} items The menu items, or a function creating them (see Sw.Menu).
     * @param {object} [o] Options: { Menu (options for Sw.Menu.Show: Align, Side, Label ...), NoClick (don't bind the click: the caller
     * opens it with the returned Open, ex: Sw.Button), AtPointer (a right click opens the menu at the pointer instead of the element) }.
     * @returns {{Open: function(Event=):Promise<void>, Close: function():void, Remove: function():void}} Open / close it, or remove the handlers.
     */
    function MenuButton(e, items, o) {
        optCheck("Sw.MenuButton", o);
        o = o || {};
        e.setAttribute("aria-haspopup", "menu");
        e.setAttribute("aria-expanded", "false");
        let menu = null;
        /** Open the menu (once; a second call while it is open does nothing). */
        const open = async ev => {
            if (menu || !Sw.Menu)
                return;
            const list = typeof items === "function" ? await items(ev) : items;
            if (!list || !list.length || menu)
                return;
            const mo = o.Menu || {};
            const at = o.AtPointer && ev && ev.type === "contextmenu" && (ev.clientX || ev.clientY) ? { X: ev.clientX, Y: ev.clientY } : e;
            e.setAttribute("aria-expanded", "true");
            menu = Sw.Menu.Show(at, list, Object.assign({}, mo, {
                OnClose: () => {
                    menu = null;
                    e.setAttribute("aria-expanded", "false");
                    if (mo.OnClose)
                        mo.OnClose();
                },
            }));
        };
        /** Pure click: open (a modifier click focuses, Sw.BadClick). */
        const click = ev => {
            if (BadClick(ev) || Disabled.Is(e))
                return;
            open(ev);
        };
        if (!o.NoClick)
            /* SwWeb:allow ux-inp-2 (click starts with BadClick) */
            e.addEventListener("click", click);
        const offLong = LongPress(e, ev => open(ev));
        return {
            Open: open,
            Close: () => menu && menu.Close(),
            Remove: () => {
                offLong();
                e.removeEventListener("click", click);
                e.removeAttribute("aria-haspopup");
                e.removeAttribute("aria-expanded");
            },
        };
    }

    /**
     * The menu button an event target is in: a button (or role="button" / a link) with aria-haspopup="menu" (or "true").
     * @param {?EventTarget} t The event target.
     * @returns {?HTMLElement} The button, or null.
     */
    function menuButtonOf(t) {
        const b = t instanceof Element ? t.closest('[aria-haspopup="menu"], [aria-haspopup="true"]') : null;
        return b && (b.tagName === "BUTTON" || b.tagName === "A" || b.getAttribute("role") === "button") && !b.disabled ? b : null;
    }

    //  Every menu button answers the right mouse button (UX guide INP-3), also one that opens its menu from a plain click handler
    //  (Sw.Menu.Show in OnClick, the shell's buttons, page markup) instead of Sw.MenuButton / Sw.Button({ Menu }): a pure right click (and
    //  the context menu key / Shift+F10, a touch long press where the browser sends a contextmenu) on a button with aria-haspopup="menu"
    //  clicks it, at the pointer, instead of showing the browser's menu. A handler of its own (Sw.MenuButton, Sw.LongPress) already
    //  prevented the default, and a right click with a modifier keeps the browser's menu (Sw.IsPureContextMenu).
    if (typeof document !== "undefined")
        document.addEventListener("contextmenu", ev => {
            if (ev.defaultPrevented || !IsPureContextMenu(ev))
                return;
            const b = menuButtonOf(ev.target);
            if (!b)
                return;
            ev.preventDefault();
            b.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true, view: window, detail: 1, clientX: ev.clientX, clientY: ev.clientY, screenX: ev.screenX, screenY: ev.screenY }));
        });

    // ------------------------------------------------------------------ disabled controls (UX guide INP-12)

    /**
     * The native disabled property of an element's class (the Sw controls shadow it on their element, see Disabled.Shadow).
     * @param {HTMLElement} e The element.
     * @returns {PropertyDescriptor|null} The descriptor (null for elements without one, ex: a div).
     */
    function nativeDisabled(e) {
        for (let p = Object.getPrototypeOf(e); p; p = Object.getPrototypeOf(p)) {
            const d = Object.getOwnPropertyDescriptor(p, "disabled");
            if (d)
                return d.set ? d : null;
        }
        return null;
    }

    /**
     * Disabled controls that stay reachable: aria-disabled="true" (not the disabled attribute), so the control keeps its place in the Tab
     * order, its tool tip can be read, and an activation attempt says why at once (FBK-1): the reason in a hint bubble anchored to the
     * control (Sw.Hint, also on touch, 3.5 s or until the next pointer down) and announced (Sw.Page.Announce; the same input on the same
     * element within 1 s is announced once). Every Sw control uses it in SetDisabled; use it for your own elements too.
     */
    const Disabled = {
        /**
         * Disable or enable an element.
         * @param {HTMLElement} e The element.
         * @param {boolean} d Disabled.
         * @param {string|{Reason: string, Native: boolean}} [o] The reason ("Select a row first", "Your role doesn't allow this"; also the
         * tool tip while disabled; empty: the element's title is the reason), or options { Reason, Native (use the native disabled
         * attribute instead: only for controls that must not take the focus, ex: inside a collapsed or inert area) }.
         */
        Set(e, d, o) {
            optCheck("Sw.Disabled.Set", o);
            if (!e)
                return;
            o = typeof o === "string" ? { Reason: o } : (o || {});
            d = !!d;
            if (o.Native) {
                setNative(e, d);
                return;
            }
            //  An element disabled the old way (attribute) becomes focusable again
            if (e.hasAttribute("disabled"))
                setNative(e, false);
            if (d)
                e.setAttribute("aria-disabled", "true");
            else
                e.removeAttribute("aria-disabled");
            e.classList.toggle("sw-disabled", d);
            if (o.Reason !== undefined || !d)
                Disabled.SetReason(e, d ? o.Reason : null);
            Disabled.Guard(e);
            //  The author's mistakes that break INP-12 silently (developer warnings only)
            if (d && !e.SwDisabledReason)
                DevWarn('Disabled without a reason: "' + (accessibleName(e) || e.tagName) + '". Pass why and what enables it, ex: SetDisabled(true, "Select a row first") (UX guide INP-12); an attempt now says "Not available right now".', "dis-noreason:" + (accessibleName(e) || e.tagName));
            if (d && !e.matches(InteractiveSel + ",[role]"))
                DevWarn("Sw.Disabled.Set on a non interactive element (" + e.tagName.toLowerCase() + (e.className ? "." + String(e.className).split(" ")[0] : "") + "): aria-disabled means nothing there; disable the control itself.", "dis-noninteractive:" + e.tagName);
        },
        /**
         * Change the reason of a disabled element (shown as its tool tip while disabled; the normal tool tip comes back when enabled).
         * @param {HTMLElement} e The element.
         * @param {string|null} reason The reason.
         */
        SetReason(e, reason) {
            const prev = e.SwDisabledReason || null;
            reason = reason || null;
            if (prev === reason)
                return;
            if (reason) {
                if (!prev)
                    e.SwTitleBefore = e.getAttribute("title");
                e.title = reason;
            }
            else if (prev) {
                if (e.getAttribute("title") === prev) {
                    if (e.SwTitleBefore)
                        e.title = e.SwTitleBefore;
                    else
                        e.removeAttribute("title");
                }
                e.SwTitleBefore = null;
            }
            e.SwDisabledReason = reason;
        },
        /**
         * Test if an element is disabled (aria-disabled="true" or the disabled attribute).
         * @param {Element} e The element.
         * @returns {boolean} True if disabled.
         */
        Is(e) {
            return !!e && e.nodeType === 1 && (e.getAttribute("aria-disabled") === "true" || e.matches(":disabled"));
        },
        /**
         * The guard of an aria-disabled element (installed once by Set): a capturing click handler that stops the activation (the click
         * of the mouse, a tap, Enter / Space, assistive technology) and shows the reason; a modifier click focuses (INP-2).
         * @param {HTMLElement} e The element.
         */
        Guard(e) {
            if (e.SwDisabledGuard)
                return;
            e.SwDisabledGuard = ev => {
                if (e.getAttribute("aria-disabled") !== "true")
                    return;
                ev.preventDefault();
                ev.stopImmediatePropagation();
                if (ModifierClick(ev, e))
                    return;
                Disabled.Reject(e, ev);
            };
            e.addEventListener("click", e.SwDisabledGuard, true);
            //  Native fields change by keyboard without a click (a select by arrows, a text input by typing, paste and drop): the same guard
            if (/^(SELECT|INPUT|TEXTAREA)$/.test(e.tagName)) {
                const keys = /^(Arrow(Up|Down|Left|Right)|Home|End|Page(Up|Down)| |Backspace|Delete)$/;
                const text = e.tagName === "TEXTAREA" || (e.tagName === "INPUT" && !/^(checkbox|radio|range|color|file|button|submit|reset)$/i.test(e.type));
                const guardKey = ev => {
                    if (e.getAttribute("aria-disabled") !== "true" || ev.ctrlKey || ev.metaKey || ev.altKey || ev.key === "Tab" || ev.key === "Escape")
                        return;
                    if (keys.test(ev.key) || (text && ev.key.length === 1) || (e.tagName === "SELECT" && ev.key.length === 1) || ev.key === "Enter" && e.tagName === "SELECT") {
                        ev.preventDefault();
                        ev.stopImmediatePropagation();
                        Disabled.Reject(e, ev);
                    }
                };
                const guardEdit = ev => {
                    if (e.getAttribute("aria-disabled") !== "true")
                        return;
                    ev.preventDefault();
                    ev.stopImmediatePropagation();
                    Disabled.Reject(e, ev);
                };
                e.addEventListener("keydown", guardKey, true);
                e.addEventListener("paste", guardEdit, true);
                e.addEventListener("drop", guardEdit, true);
                //  A select changed by the mouse wheel or a picker the guard missed: put the value back
                if (e.tagName === "SELECT") {
                    let last = e.value;
                    e.addEventListener("focus", () => last = e.value, true);
                    e.addEventListener("change", ev => {
                        if (e.getAttribute("aria-disabled") === "true") {
                            e.value = last;
                            ev.stopImmediatePropagation();
                        }
                        else
                            last = e.value;
                    }, true);
                    e.addEventListener("mousedown", ev => {
                        if (e.getAttribute("aria-disabled") === "true") {
                            ev.preventDefault();
                            e.focus();
                            Disabled.Reject(e, ev);
                        }
                    }, true);
                }
            }
        },
        /**
         * Say why a disabled control can't be used (an activation attempt, FBK-1): the reason in a hint bubble next to it and announced.
         * @param {HTMLElement} e The element.
         * @param {Event} [ev] The attempt (the same input type on the same element within 1 s is announced once).
         */
        Reject(e, ev) {
            //  The reason only: the tool tip of an enabled control says what it does, not why it is disabled (an action's description
            //  shown as the "reason" reads like a contradiction: "Send invitation is not available: Send an invitation ...")
            const reason = e.SwDisabledReason || e.getAttribute("data-sw-disabled-reason")
                || _TF("Not available right now", "Reason shown when a disabled button or control is clicked and it has no reason of its own");
            if (Sw.Hint && Sw.Hint.Show)
                Sw.Hint.Show(e, reason, 3500);
            const now = Date.now();
            //  The same input (a pointer click, a key, a long press ...) on the same element within 1 s is announced once (FBK-1)
            const kind = ev ? ev.type + (ev.type === "click" ? (ev.detail === 0 ? ":key" : ":pointer") : ev.key ? ":" + ev.key : "") : "";
            if (e.SwRejectAt && now - e.SwRejectAt < 1000 && e.SwRejectKind === kind)
                return;
            e.SwRejectAt = now;
            e.SwRejectKind = kind;
            const name = accessibleName(e);
            if (Sw.Page && Sw.Page.Announce)
                Sw.Page.Announce(name
                    ? _T("{0} is not available: {1}", name, reason, "Announced to screen readers when a disabled control is activated. {0} is the name of the control, ex: 'Save'; {1} is the reason, ex: 'Select a row first'")
                    : reason);
        },
    };

    /**
     * A short accessible name of an element (aria-label, aria-labelledby, its label, else its text), for announcements.
     * @param {Element} e The element.
     * @returns {string} The name ("" when it has none), at most 60 characters.
     */
    function accessibleName(e) {
        let s = e.getAttribute("aria-label") || "";
        if (!s && e.getAttribute("aria-labelledby"))
            s = e.getAttribute("aria-labelledby").split(/\s+/).map(id => {
                const l = document.getElementById(id);
                return l ? l.textContent : "";
            }).join(" ");
        if (!s && e.labels && e.labels.length)
            s = e.labels[0].textContent;
        if (!s && !/^(INPUT|TEXTAREA|SELECT)$/.test(e.tagName))
            s = e.textContent;
        return String(s || "").replace(/\*/g, "").trim().replace(/\s+/g, " ").substring(0, 60);
    }

    /**
     * Set the real disabled property / attribute (bypassing the aria-disabled shadow property of the Sw controls).
     * @param {HTMLElement} e The element.
     * @param {boolean} d Disabled.
     */
    function setNative(e, d) {
        const nd = nativeDisabled(e);
        if (nd)
            nd.set.call(e, d);
        else if (d)
            e.setAttribute("disabled", "");
        else if (d)
            e.setAttribute("disabled", "");
        else
            e.removeAttribute("disabled");
    }

    /**
     * Make an element's disabled property report and set the aria-disabled state (backward compatibility: code that reads or writes
     * button.E.disabled keeps working with the Sw controls). Setting it calls set(value).
     * @param {HTMLElement} e The element.
     * @param {function(boolean):void} set Sets the state (the component's SetDisabled).
     */
    function shadowDisabled(e, set) {
        const nd = nativeDisabled(e);
        Object.defineProperty(e, "disabled", {
            configurable: true,
            get: () => e.getAttribute("aria-disabled") === "true" || (nd && nd.get ? !!nd.get.call(e) : false),
            set: v => set(!!v),
        });
    }
    Disabled.Shadow = shadowDisabled;

    // ------------------------------------------------------------------ developer warnings

    /** @type {Set<string>} The developer warnings shown already (each once per page; only used without the core). */
    const devWarned = new Set();

    /**
     * Warn the page author about a misuse of the framework: the one channel is the core's Sw.DevWarn (gated by Sw.DevMode(), which a
     * page or the developer can switch off: core/README "Developer warnings"); this copy is only used when the core is not loaded. Used
     * by the controls for patterns that break the UX guide silently (an async OnClick that returned no promise, a disabled control
     * without a reason, a copy button without a value ...). Read at call time, so an override of Sw.DevMode / Sw.DevWarn applies.
     * @param {string} msg The message (English, for developers: not translated).
     * @param {string} [key] De-duplication key (default the message).
     */
    function DevWarn(msg, key) {
        if (typeof Sw.DevWarn === "function" && Sw.DevWarn !== DevWarn)
            return Sw.DevWarn(msg, key);
        key = key || msg;
        if (devWarned.has(key) || typeof Sw.DevMode !== "function" || !Sw.DevMode())
            return;
        devWarned.add(key);
        console.warn("[Sw UX] " + msg);
    }

    /**
     * The type names of an options schema entry ("string", "number", "boolean", "function", "object", "array", "element" (a DOM
     * element), "component" (an object with E), "node" (an element, a component or a text), "any"); "string|boolean" lists several.
     * @param {*} spec The entry: a type text, an array of them, or { Type, Collides }.
     * @returns {Array<string>|null} The types, null for any.
     */
    function optTypes(spec) {
        const t = spec && typeof spec === "object" && !Array.isArray(spec) ? spec.Type : spec;
        if (!t || t === "any")
            return null;
        return (Array.isArray(t) ? t : String(t).split("|")).map(x => x.trim()).filter(Boolean);
    }

    /**
     * @param {*} v A value. @param {string} t A type name (optTypes).
     * @returns {boolean} True when the value has the type.
     */
    function optIs(v, t) {
        switch (t) {
            case "array": return Array.isArray(v);
            case "object": return typeof v === "object" && !Array.isArray(v);
            case "element": return typeof Element === "function" && v instanceof Element;
            case "component": return !!v && typeof v === "object" && typeof Element === "function" && v.E instanceof Element;
            case "node": return typeof v === "string" || (typeof Node === "function" && v instanceof Node) || (!!v && typeof Element === "function" && v.E instanceof Element);
            case "number": return typeof v === "number" && isFinite(v);
            default: return typeof v === t;
        }
    }

    /**
     * The known option nearest to a misspelled one (case only, or at most 2 letters apart).
     * @param {string} k The given name. @param {Array<string>} known The known names.
     * @returns {string} The nearest, or "".
     */
    function optNear(k, known) {
        const lk = k.toLowerCase();
        const same = known.find(x => x.toLowerCase() === lk);
        if (same)
            return same;
        const dist = (a, b) => {
            const d = Array.from({ length: b.length + 1 }, (_, i) => i);
            for (let i = 1; i <= a.length; ++i) {
                let prev = d[0];
                d[0] = i;
                for (let j = 1; j <= b.length; ++j) {
                    const cur = d[j];
                    d[j] = Math.min(d[j] + 1, d[j - 1] + 1, prev + (a[i - 1] === b[j - 1] ? 0 : 1));
                    prev = cur;
                }
            }
            return d[b.length];
        };
        let best = "", bd = 3;
        for (const x of known) {
            const n = dist(lk, x.toLowerCase());
            if (n < bd) {
                bd = n;
                best = x;
            }
        }
        return best;
    }

    /** @type {Map<string, object>} The ui option schemas by api name (Work.RegisterOptions). */
    const uiSchemas = new Map();

    /**
     * A ui options schema in the core's shape (Sw.DevOptions): types as arrays ("component" / "node" are objects there), no ui rules.
     * @param {object} schema The ui schema.
     * @returns {object} The core schema.
     */
    function coreSchema(schema) {
        const cs = {};
        for (const k of Object.keys(schema)) {
            if (k[0] === "$")
                continue;
            const t = optTypes(schema[k]);
            cs[k] = !t ? "any" : Array.from(new Set(t.flatMap(x => x === "component" ? ["object"] : (x === "node" ? ["string", "element", "object"] : [x]))));
        }
        return cs;
    }

    /**
     * Register the options schema of a ui api by its name (the one registry, Sw.DevOptions.Register of the core, so its walker knows the
     * api is checked: option checks by mechanism, not by list): Work.CheckOptions(where, o) then finds it without the schema.
     * @param {string} where The api ("Sw.Card", "Sw.Form field").
     * @param {object} schema The ui schema (DevOptions).
     * @returns {object} The schema.
     */
    function RegisterOptions(where, schema) {
        uiSchemas.set(String(where), schema);
        try {
            if (typeof Sw.DevOptions === "function" && Sw.DevOptions !== DevOptions && typeof Sw.DevOptions.Register === "function")
                Sw.DevOptions.Register(String(where), coreSchema(schema));
        }
        catch {
            //  (an older core: the ui map only)
        }
        return schema;
    }

    /**
     * Check an options object against the schema registered for the api (DevMode; a non-object is the api's short form: not checked).
     * @param {string} where The api ("Sw.OnClick"). @param {*} o The options.
     */
    function optCheck(where, o) {
        if (o && typeof o === "object" && !Array.isArray(o))
            DevOptions(where, o);
    }

    //  The options of this file's apis (the one registry: Work.RegisterOptions; checked by optCheck where the api starts)
    {
        const S = "string", F = "function", B = "boolean", N = "number", A = "any";
        const copy = { Toast: B, Generic: B, Secret: B, Show: B, What: S, Button: A };
        const fade = { ShowError: B, FadeIn: N, FadeDelay: N, Channel: S, Parent: A, UseEvent: S };
        const schemas = {
            "Sw.Clipboard.Copy": copy,
            "Sw.Clipboard.Message": copy,
            "Sw.Clipboard.CopyOnClick": { Keyboard: B, AddTitle: B, What: S, Generic: B, Secret: B, Show: B },
            "Sw.FadeSrc": fade,
            "Sw.CreateImg": fade,
            "Sw.CreateIFrame": fade,
            "Sw.Drag": { OnStart: F, OnMove: F, OnEnd: F, OnTap: F, Threshold: N, Button: N, Touch: B },
            "Sw.IconClass": { Size: "number|string", Color: S, Title: S, Class: S },
            "Sw.Keys.Add": { Title: S, InInputs: B, Global: B, Replace: B, Scope: A },
            "Sw.MenuButton": { Menu: "object", NoClick: B, AtPointer: B },
            "Sw.OnClick": { Name: S, Leave: B, NoKey: B, Done: "boolean|string" },
            "Sw.SetImageSource": { Fallback: S, FlushCache: B, OnLoaded: F, ReloadOn: S },
            "Sw.Focus.Keep": { From: A },
            "Sw.Work.Error": { Name: S, Button: A, Said: B, Done: A },
            "Sw.Work.Report": { Name: S, Said: B, Done: "boolean|string", Spun: B, Button: A },
            "Sw.Work.Busy": { Name: S, Leave: B, Kind: S, Work: S, Done: A, Modals: A, SetSpin: F, SetTitle: F, Announce: A },
            "Sw.Work.Current": { Then: B },
            "Sw.Disabled.Set": { Reason: S, Native: B },
            "Sw.Reserve.Line": { Lines: N },
            "Sw.Reserve.Stack": { Sizer: A, SizerClass: S, Class: S },
        };
        for (const k of Object.keys(schemas))
            RegisterOptions(k, schemas[k]);
        try {
            if (Sw.DevOptions && typeof Sw.DevOptions.NoOptions === "function")
                Sw.DevOptions.NoOptions("Sw.Work.CheckOptions", "the option checker itself: o is the options it checks");
        }
        catch {
            //  (an older core)
        }
    }

    /**
     * The developer check of an options object (DevMode only; core/README "Developer warnings"): an UNKNOWN option (with the nearest
     * known name: "Lable" → "Label"), an option of the WRONG TYPE (Read: true where a function is expected, Progress 40 where 0..1 is
     * expected: the schema's Check), and COLLIDING options (two that can't work together: Label + AriaLabel hides the visible label).
     * Each warning once per page. The one shared validation: the core's Sw.DevOptions (unknown names and types, with this schema turned
     * into its shape) plus the ui rules here (a value Check, two options that collide, $Check over several options); without the core
     * this file checks everything itself. Exposed as Sw.Work.CheckOptions (the controls, cells and dialogs use it).
     * @param {string} where The API ("Sw.TextBox").
     * @param {object} o The options.
     * @param {object} schema { Name: "string" | "number|boolean" | ["string", "function"] | { Type, Check(value, o) -> message or "",
     * Collides: ["Other", "why"] }, ..., $Check(o) -> message(s) or "" (rules over several options), $Open: true (unknown names are fine) }.
     * @returns {object} o.
     */
    function DevOptions(where, o, schema) {
        if (!schema && where)
            schema = uiSchemas.get(String(where)) || null;
        if (!o || typeof o !== "object" || !schema || !(typeof Sw.DevMode === "function" && Sw.DevMode()))
            return o;
        const core = typeof Sw.DevOptions === "function" && Sw.DevOptions !== DevOptions ? Sw.DevOptions : null;
        if (core) {
            //  the core's check in its shape: types as arrays ("component" / "node" are objects there), no ui rules
            const cs = coreSchema(schema);
            if (schema.$Open)
                for (const k of Object.keys(o))
                    if (!(k in cs))
                        cs[k] = "any";
            //  (null means "not given" in the ui options)
            const oo = {};
            for (const k of Object.keys(o))
                if (o[k] !== null && !k.startsWith("Sw"))
                    oo[k] = o[k];
            try {
                core(where, oo, cs);
            }
            catch {
                //  (a check never breaks the api it checks)
            }
            uiRules(where, o, schema);
            return o;
        }
        const known = Object.keys(schema).filter(k => k[0] !== "$");
        for (const k of Object.keys(o)) {
            const v = o[k];
            if (k[0] === "$" || k.startsWith("Sw"))
                continue;
            const spec = schema[k];
            if (spec === undefined) {
                if (schema.$Open || v === undefined)
                    continue;
                const near = optNear(k, known);
                DevWarn(where + ': unknown option "' + k + '"' + (near ? ' (did you mean "' + near + '"?)' : "") + "; it is ignored. Options: " + known.join(", ") + ".", "opt-unknown:" + where + ":" + k);
                continue;
            }
            if (v === undefined || v === null)
                continue;
            const types = optTypes(spec);
            if (types && !types.some(t => optIs(v, t))) {
                const got = Array.isArray(v) ? "an array" : (typeof v === "object" ? "an object" : typeof v + " " + JSON.stringify(v).slice(0, 40));
                DevWarn(where + ': option "' + k + '" must be ' + types.join(" or ") + ", got " + got + "; it does not work as meant.", "opt-type:" + where + ":" + k);
                continue;
            }
        }
        uiRules(where, o, schema);
        return o;
    }

    /**
     * The ui rules of an options schema (DevOptions): a value Check, Collides: ["Other", "why"] (both given), $Check(o).
     * @param {string} where The API. @param {object} o The options. @param {object} schema The schema.
     */
    function uiRules(where, o, schema) {
        try {
            for (const k of Object.keys(o)) {
                const v = o[k];
                const spec = schema[k];
                if (v === undefined || v === null || !spec || typeof spec !== "object" || Array.isArray(spec))
                    continue;
                const types = optTypes(spec);
                if (types && !types.some(t => optIs(v, t)))
                    continue;
                if (typeof spec.Check === "function") {
                    const m = spec.Check(v, o);
                    if (m)
                        DevWarn(where + ': option "' + k + '": ' + m, "opt-check:" + where + ":" + k);
                }
                if (Array.isArray(spec.Collides) && o[spec.Collides[0]] !== undefined && o[spec.Collides[0]] !== null && o[spec.Collides[0]] !== false)
                    DevWarn(where + ': options "' + k + '" and "' + spec.Collides[0] + '" collide: ' + spec.Collides[1], "opt-collide:" + where + ":" + k);
            }
            if (typeof schema.$Check === "function") {
                const ms = [].concat(schema.$Check(o) || []).filter(Boolean);
                for (const m of ms)
                    DevWarn(where + ": " + m, "opt-rule:" + where + ":" + m.slice(0, 60));
            }
        }
        catch {
            //  (a check never breaks the api it checks)
        }
    }

    // ------------------------------------------------------------------ work started by a click (UX guide FBK-2, FBK-3, FBK-4)

    /** The trusted event types that are the user's own action (a click, a key, a pick, a drop): work inside them acts for the user. */
    const UserEventTypes = /^(click|dblclick|auxclick|keydown|keyup|keypress|pointerup|pointerdown|mouseup|mousedown|touchend|submit|change|input|paste|drop|contextmenu)$/;

    /** @type {Array<{Requests: Array<Promise>, Signal: AbortSignal}>} The open click scopes (innermost last). */
    const workScopes = [];
    /** @type {Map<Element, Promise<boolean>>} The elements busy with work now (Work.Busy) and the outcome to come (Work.Current). */
    const busyWorks = new Map();
    /** @type {Set<Array<object>>} The open write recorders (Work.Record). */
    const recorders = new Set();
    /** @type {Array<string>} The tracked request calls running now (synchronously): an inner call is part of the outer one. */
    const reqDepth = [];

    /**
     * The one write test for a recorded request (every tracker asks the core's Sw.Net.IsWrite, which knows ReadOnly,
     * AbortSafe, Sw.Net.Read and the old Read: true): Sw.Net.Send / Get / Read by their options, a fetch by its method.
     * @param {string} name "Send" | "Get" | "Read" | "fetch".
     * @param {Array<*>} args The call's arguments.
     * @returns {boolean} True for a write.
     */
    function isWriteCall(name, args) {
        let method = "GET";
        if (name === "fetch") {
            const a0 = args[0], i = args[1];
            method = String((i && i.method) || (a0 && typeof a0 === "object" && a0.method) || "GET").toUpperCase();
        }
        if (Sw.Net && typeof Sw.Net.IsWrite === "function") {
            try {
                if (name === "Send")
                    return !!Sw.Net.IsWrite(args[2] && typeof args[2] === "object" ? args[2] : {}, "Send");
                if (name === "Get" || name === "Read")
                    return !!Sw.Net.IsWrite(args[1] && typeof args[1] === "object" ? args[1] : {}, "Get");
                return !!Sw.Net.IsWrite({}, method);
            }
            catch {
                //  (the local test)
            }
        }
        if (name === "Send") {
            const o = args[2];
            return !(o && typeof o === "object" && (o.ReadOnly === true || o.Read === true));
        }
        return name === "fetch" ? method !== "GET" && method !== "HEAD" : false;
    }

    /** Install the request trackers once: Sw.Net.Send / Get / fetch started inside a click scope join its work. */
    function trackRequests() {
        if (trackRequests.Done)
            return;
        trackRequests.Done = true;
        /**
         * Wrap a request function: inside a scope the promise is collected and the scope's AbortSignal is used when the call has none.
         * @param {object} host The object.
         * @param {string} name The function name.
         * @param {number} optsAt The index of the options argument (Signal goes there).
         * @param {string} sigName The name of the signal option.
         */
        const wrap = (host, name, optsAt, sigName) => {
            const f = host && host[name];
            if (typeof f !== "function" || f.SwTracked)
                return;
            const w = function (...args) {
                //  A request made by another tracked request (Sw.Net.Get calls fetch): the outer call is the one request, and its Detached
                //  travels with it (A3-3: Get(url, { Detached: true }) reached fetch inside the click scope without the flag)
                if (reqDepth.length)
                    return f.apply(this, args);
                const sc = workScopes[workScopes.length - 1];
                const o0 = args[optsAt];
                //  Detached / Background: a request that is not the click's work (telemetry, a live view, a long poll): not collected
                const a0 = args[0];
                const detached = !!(o0 && typeof o0 === "object" && !(typeof Request === "function" && o0 instanceof Request) && (o0.Detached || o0.Background))
                    || !!(name === "fetch" && a0 && typeof a0 === "object" && a0.SwDetached === true);
                if (sc && sc.Signal && !detached) {
                    const o = args[optsAt];
                    if (o === undefined || o === null)
                        args[optsAt] = { [sigName]: sc.Signal };
                    else if (typeof o === "object" && !o[sigName] && !(o instanceof Request))
                        args[optsAt] = Object.assign({}, o, { [sigName]: sc.Signal });
                }
                let p;
                reqDepth.push(name);
                try {
                    p = f.apply(this, args);
                }
                finally {
                    reqDepth.pop();
                }
                if (sc && !detached && p && typeof p.then === "function") {
                    sc.Requests.push(p);
                    //  A write (Sw.Net.IsWrite: a Send not marked read, a fetch that is not GET / HEAD): an abort can't say it was not
                    //  done on the server
                    if (isWriteCall(name, args))
                        sc.Writes++;
                }
                //  Writes seen by the open recorders (Work.Record: a button / Enter whose work sent the edited values is a save; a
                //  Detached write is recorded as such: the controls warn when it carried the edited values, the button's own save)
                if (recorders.size && isWriteCall(name, args)) {
                    let m = name === "Send" ? "POST" : "GET";
                    if (name === "fetch") {
                        const a0 = args[0], i = args[1];
                        m = String((i && i.method) || (a0 && typeof a0 === "object" && a0.method) || "GET").toUpperCase();
                    }
                    if (m) {
                        const a0 = args[0];
                        const body = name === "Send" ? args[1] : (args[1] && args[1].body);
                        const w2 = { Name: name, Method: m, Url: typeof a0 === "string" ? a0 : (a0 && a0.url) || "", Body: body, Detached: detached };
                        recorders.forEach(r => r.push(w2));
                    }
                }
                return p;
            };
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
     * Elements of open modal dialogs (aria-modal), to tell work from waiting for the user (a confirmation opened by the click).
     * @returns {Set<Element>} The dialogs.
     */
    function openModals() {
        return new Set(Array.from(document.querySelectorAll("[aria-modal='true']")).filter(d => d.isConnected && !d.hidden && !d.closest("[hidden]")));
    }

    /**
     * The one busy look of an element without an icon (FBK-2 / FBK-3: "busy is not disabled: it keeps its label"): the spinner goes in
     * front of the label, clear of the rounded edge; when there is room before the label (a wide or full width button) the label stays
     * where it was, else the label moves by the missing room taken from the end padding (spinner + label read as one group), so the
     * label stays at full strength, the width stays and nothing is covered. Too little room: a smaller spinner.
     * @param {HTMLElement} e The element (class sw-working-over while busy).
     * @param {?HTMLElement} spin The spinner to place, null = undo.
     */
    function SpinBeside(e, spin) {
        //  (placed again, ex: a button's content set while busy: measure the element's own padding)
        if (e.SwSpinPad) {
            e.style.paddingInlineStart = e.SwSpinPad[0];
            e.style.paddingInlineEnd = e.SwSpinPad[1];
            e.SwSpinPad = null;
        }
        if (!spin) {
            e.classList.remove("sw-working-over");
            return;
        }
        const cs = getComputedStyle(e);
        const rtl = cs.direction === "rtl";
        const ps = parseFloat(cs.paddingInlineStart || (rtl ? cs.paddingRight : cs.paddingLeft)) || 0;
        const pe = parseFloat(cs.paddingInlineEnd || (rtl ? cs.paddingLeft : cs.paddingRight)) || 0;
        const bl = parseFloat(cs.borderLeftWidth) || 0, br = parseFloat(cs.borderRightWidth) || 0;
        const fs = parseFloat(cs.fontSize) || 14;
        const gap = Math.max(3, Math.round(fs * 0.3));
        //  (clear of the rounded corner: D8 "pressed against the edge")
        const edge = Math.max(4, Math.round(fs * 0.3));
        //  The free room before and after the label (padding, and the centring slack of a wide / full width button)
        let before = ps, after = pe;
        try {
            const er = e.getBoundingClientRect();
            const rg = document.createRange();
            rg.selectNodeContents(e);
            const tr = rg.getBoundingClientRect();
            if (tr.width && er.width) {
                before = rtl ? er.right - br - tr.right : tr.left - er.left - bl;
                after = rtl ? tr.left - er.left - bl : er.right - br - tr.right;
            }
        }
        catch {
            //  (the paddings)
        }
        let size = Math.round(fs * 0.85);
        if (!e.SwSpinPad)
            e.SwSpinPad = [e.style.paddingInlineStart, e.style.paddingInlineEnd];
        spin.classList.add("sw-over", "sw-beside");
        e.classList.add("sw-working-over");
        e.append(spin);
        if (before >= edge + size + gap) {
            //  Room in front of the label (a wide button): the spinner sits right before it, the label stays where it was
            spin.style.width = size + "px";
            spin.style.height = size + "px";
            spin.style.insetInlineStart = Math.round(before - size - gap) + "px";
            return;
        }
        //  Else the label moves by the missing room, taken from the end side (the width stays): spinner + label read as one group
        const maxShift = Math.max(0, after - edge);
        size = Math.max(8, Math.min(size, before + maxShift - edge - gap));
        const shift = Math.max(0, Math.min(maxShift, edge + size + gap - before));
        spin.style.width = size + "px";
        spin.style.height = size + "px";
        spin.style.insetInlineStart = Math.max(2, Math.round(before + shift - size - gap)) + "px";
        if (shift > 0) {
            e.style.paddingInlineStart = (ps + shift) + "px";
            e.style.paddingInlineEnd = Math.max(0, pe - shift) + "px";
        }
    }

    /**
     * Format an elapsed time the one way ("3 s", "1 min 5 s").
     * @param {number} ms Milliseconds.
     * @returns {string} The text.
     */
    function elapsedText(ms) {
        const s = Math.max(0, Math.round(ms / 1000));
        if (s < 60)
            return _T("{0} s", s, "Elapsed time in seconds of running work, ex: '3 s'");
        return _T("{0} min {1} s", Math.floor(s / 60), s % 60, "Elapsed time of running work, ex: '1 min 5 s'");
    }

    /**
     * The work of a click (UX guide FBK-2, FBK-3, FBK-4, SAF-10): one implementation for Sw.Button, Sw.OnClick, card / row actions.
     *  - Work.Scope(fn): runs fn synchronously in a click scope and returns { Result, Requests, Controller }: every Sw.Net.Send / Get,
     *    Sw.Activity.Send and fetch started inside it is collected (so an OnClick that forgot to return its promise still makes the
     *    control busy) and gets the scope's AbortSignal when it has none (Cancel, Abort(), leaving the page abort it).
     *  - Work.Promise(scope): the promise to wait for: the returned one, else all collected requests (settled), else null.
     *  - Work.Busy(el, promise, o): the busy state of any element: aria-busy, cursor progress, repeats absorbed by the caller, the width
     *    kept, a spinner after 250 ms in place of its icon (or over its label), "Working…" announced, after 2 s the elapsed time in its
     *    tool tip; while a dialog opened by the click waits for the user it is not busy (no spinner, no announcement). A busy state of
     *    more than 2 s marks the page: leaving it asks "Leave this page? <name> may not finish." (option Leave: false opts out).
     *    Returns a promise that settles with { Failed, Error, Spun } (never rejects).
     */
    /**
     * Test if a value is a Sw.Activity job (core/activity.js Job).
     * @param {*} x The value.
     * @returns {boolean} True for a job.
     */
    function isJob(x) {
        return !!x && typeof x === "object" && typeof x.State === "string" && typeof x.Done === "function" && typeof x.Fail === "function" && typeof x.Cancelled === "function" && "Running" in x;
    }

    /**
     * The end of a Sw.Activity job as a promise: resolves with its result when done, rejects with its error when it failed (marked
     * SwSaid when a visual activity list shows it: the shell's toast and list say it, the control adds nothing) and with an AbortError
     * when it was cancelled (never "done").
     * @param {object} j The job.
     * @returns {Promise<*>} The end.
     */
    function jobEnd(j) {
        //  (the core's own end of a job: resolves with the value, rejects marked SwSaid: the activity list / line showed it)
        if (j.Finished && typeof j.Finished.then === "function")
            return Promise.resolve(j.Finished);
        return new Promise((resolve, reject) => {
            let off = null;
            const check = () => {
                if (j.Running)
                    return false;
                if (off)
                    off();
                const shown = !!(Sw.Activity && Sw.Activity.Supported);
                if (j.State === "failed") {
                    const x = new Error(j.Error || _TF("The work failed.", "Error text of a job in the activity list that failed without a message"));
                    x.SwSaid = shown;
                    reject(x);
                }
                else if (j.State === "cancelled") {
                    const x = new Error(j.Text || "cancelled");
                    x.name = "AbortError";
                    x.SwSaid = true;
                    reject(x);
                }
                else
                    resolve(j.Result || j);
                return true;
            };
            if (check())
                return;
            if (Sw.Activity && typeof Sw.Activity.On === "function")
                off = Sw.Activity.On(e => {
                    if (!e || !e.Job || e.Job === j)
                        check();
                });
            //  (a list without events: look again now and then)
            const iv = setInterval(() => {
                if (check())
                    clearInterval(iv);
            }, 1000);
            if (off) {
                const off0 = off;
                off = () => {
                    off0();
                    clearInterval(iv);
                };
            }
        });
    }

    /**
     * The one failure sentence "<name>: not done. <why>" (no "not done.: ", F16): the shell's composer Sw.Net.FailText(name, x) when it
     * exists, else the readable reason of the error object (Sw.Net.ErrorText reads its Status and the server's text).
     * @param {string} name What failed ("" = no name).
     * @param {*} x The error (an Error, a Sw.Net.Error or a text).
     * @returns {string} The sentence.
     */
    function failText(name, x) {
        if (Sw.Net && typeof Sw.Net.FailText === "function") {
            try {
                const s = Sw.Net.FailText(name, x);
                if (s)
                    return String(s);
            }
            catch {
                //  (fall back to the local sentence)
            }
        }
        let why = x && typeof x === "object" ? String(x.message ?? x) : String(x ?? "");
        if (Sw.Net && Sw.Net.ErrorText) {
            try {
                const et = Sw.Net.ErrorText(x && typeof x === "object" ? x : why);
                if (et && et.Text)
                    why = String(et.Text);
            }
            catch {
                //  (the raw text)
            }
        }
        why = why.trim();
        if (!name)
            return why || _TF("Not done.", "Error after an action ended without doing its work and without saying why");
        //  (a reason that names the action already, ex: "Save: the server ...", is the whole sentence)
        if (why && why.indexOf(name) === 0)
            return why;
        return why
            ? _T("{0}: not done. {1}", name, why, "Error after an action failed. {0} is the action's name, ex: 'Save', {1} the reason from the server or the code")
            : _T("{0}: not done.", name, "Error after an action ended without doing its work and without saying why. {0} is the action's name, ex: 'Save'");
    }

    const Work = {
        Scope(fn) {
            trackRequests();
            //  The click's signal is a Sw.Cancel.Controller where the core has it: Sw.Net never aborts a WRITE that has left with it
            //  (POP-6: "a sent write is never aborted"; a Cancel stops waiting for it instead)
            let ac = null;
            try {
                ac = Sw.Cancel && typeof Sw.Cancel.Controller === "function" ? Sw.Cancel.Controller() : null;
            }
            catch {
                ac = null;
            }
            if (!ac && typeof AbortController === "function")
                ac = new AbortController();
            //  User: the work runs inside the user's own trusted event (a click, a key, a pick): values its synchronous part writes into
            //  controls are the user's edits (a "Same as billing" fill), Work.InUser()
            const ev = typeof window !== "undefined" ? window.event : null;
            const sc = { Requests: [], Signal: ac ? ac.signal : null, Writes: 0, User: !!(ev && ev.isTrusted && UserEventTypes.test(ev.type || "")) };
            //  Done: true when the work succeeded, false when it failed / was stopped (Work.Current; settled by Outcome, or at once
            //  when the work was synchronous)
            let settle = null;
            sc.Done = new Promise(r => settle = r);
            sc.Settle = ok => {
                if (settle) {
                    settle(!!ok);
                    settle = null;
                }
            };
            workScopes.push(sc);
            let result, error = null, threw = false;
            try {
                result = fn(ac ? ac.signal : undefined);
            }
            catch (x) {
                threw = true;
                error = x;
            }
            finally {
                workScopes.splice(workScopes.indexOf(sc), 1);
            }
            return { Result: result, Threw: threw, Error: error, Requests: sc.Requests, Controller: ac, Writes: sc.Writes, Done: sc.Done, Settle: sc.Settle, User: sc.User };
        },
        /**
         * True while the synchronous part of work started by the user's own event runs (a Sw.Button OnClick, Sw.OnClick, Enter, a
         * row action, a control's OnChange after the user's pick): what it writes into a control (Set) is the user's edit, so a "Same as
         * billing" fill makes the page dirty (SAF-10). Code that loads data in such a handler passes Set(v, { User: false }).
         * @returns {boolean} The state.
         */
        InUser() {
            const sc = workScopes[workScopes.length - 1];
            return !!(sc && sc.User);
        },
        /**
         * The work running now for an element (FD R4 F6: a draft cleared before its save failed lost the text): inside the synchronous
         * part of a click's work (Sw.Button OnClick, Sw.OnClick, Enter, an auto save, a row action, Sw.Danger.Run) that work; else the
         * busy work of a button / field in the element's form, card, group, section or dialog (or the element itself). A Promise<boolean>
         * that resolves true when it succeeded, false when it failed or was stopped; null when no work runs. Sw.Draft's Clear waits for
         * it, so a draft is only removed after the save succeeded.
         * Never a wait for itself (`await Sw.Work.Current(b.E)` in b's own OnClick was busy for ever): asking for the work of
         * the very element that runs it (the button, in its own click or while it is busy) answers null and, in DevMode, warns. Inside
         * your own OnClick await your own request instead. { Then: true }: the caller only chains on it, never awaits it in the work
         * (Sw.Draft): no self rule.
         * @param {Element|{E: Element}} [el] The element (a field, a form).
         * @param {{Then: boolean}} [o] Options.
         * @returns {?Promise<boolean>} The outcome to come, or null.
         */
        Current(el, o) {
            el = el && el.E instanceof Element ? el.E : el;
            if (o && o.Then === true)
                return workScopes.length ? workScopes[workScopes.length - 1].Done : Work.Running(el);
            /** @param {Element} x An element. @returns {boolean} A button-like element (its work is its own click's). */
            const isActor = x => x instanceof Element && (x.tagName === "BUTTON" || x.tagName === "A" || /^(button|menuitem|link|switch|option|tab)$/.test(x.getAttribute("role") || ""));
            const self = () => {
                DevWarn("Sw.Work.Current was asked for the work of the element that runs it (its own button): awaiting that inside its own work would wait for itself for ever, so it answers null. Await your own request instead; Current is for other code (a draft, a field) (UX guide FBK-4).", "work-current-self");
                return null;
            };
            if (workScopes.length) {
                //  (the user's event of this very work: its target is the asked element, or inside it)
                const ev = typeof window !== "undefined" ? window.event : null;
                if (isActor(el) && ev && ev.target instanceof Node && el.contains(ev.target))
                    return self();
                return workScopes[workScopes.length - 1].Done;
            }
            if (!(el instanceof Element))
                return null;
            for (const [e] of busyWorks)
                if (e.isConnected && isActor(e) && (e === el || e.contains(el)))
                    return self();
            return Work.Running(el);
        },
        /**
         * The work running now in an element's form / card / group / section / dialog (or the element itself), for a check (is work
         * running? Locked's message, Sw.Lock): like Current but never inside a click's synchronous part and without the self rule. Don't
         * await it in that work itself.
         * @param {Element|{E: Element}} el The element.
         * @returns {?Promise<boolean>} The outcome to come, or null.
         */
        Running(el) {
            el = el && el.E instanceof Element ? el.E : el;
            if (!(el instanceof Element))
                return null;
            const box = el.closest("form, .sw-card, .sw-group, section, .sw-dialog, [role=dialog], [role=alertdialog]") || el;
            for (const [e, d] of busyWorks)
                if (e.isConnected && (e === el || e.contains(el) || el.contains(e) || box.contains(e)))
                    return d;
            return null;
        },
        Promise(sc) {
            if (sc.SwPromise)
                return sc.SwPromise;
            const r = sc.Result;
            //  (synchronous work: done now, unless it threw; the caller's Outcome settles that one)
            if (!sc.Threw && !(r && typeof r.then === "function") && !isJob(r) && !sc.Requests.length && sc.Settle)
                sc.Settle(r !== false);
            //  A Sw.Activity job (Start; StartServer resolves one): the work runs until the job ends, so the control stays busy until
            //  then and its end is the outcome (FD N5: a returned Start job made nothing busy, a stand-alone page showed no failure)
            if (isJob(r) && typeof r.then !== "function")
                return (sc.SwPromise = jobEnd(r));
            if (r && typeof r.then === "function") {
                //  (an async handler that started a request before its first await without awaiting it: busy until that ends too, docs #12)
                const reqs = sc.Requests.slice();
                const own = Promise.resolve(r).then(v => isJob(v) && typeof v.then !== "function" ? jobEnd(v) : v);
                //  (handled by the caller later: not an unhandled rejection meanwhile)
                own.catch(() => { });
                return (sc.SwPromise = reqs.length ? Promise.allSettled(reqs).then(() => own) : own);
            }
            if (sc.Requests.length) {
                //  (the requests it started without returning them: Outcome reads their results one by one)
                sc.Implicit = true;
                return Promise.allSettled(sc.Requests);
            }
            return null;
        },
        /**
         * The outcome of a scope's work, the one rule for every async handler (Sw.Button OnClick, Sw.OnClick, an auto saving control's
         * OnChange, row actions, Sw.Danger.Run, Sw.Form): a throw, a rejection, a rejected request of the scope or a false result is a
         * failure; work whose signal was aborted (Cancel, Abort(), the page was left) is Aborted, never "done".
         * @param {object} sc The scope (Work.Scope), or null.
         * @param {{Failed: boolean, Error: *, Value: *}} res The settled work (Work.Busy's result, or { Failed, Error, Value }).
         * @returns {{Ok: boolean, Aborted: boolean, Error: *, NotDone: boolean, Wrote: boolean, Value: *}} The outcome.
         */
        Outcome(sc, res) {
            res = res || {};
            let error = res.Failed ? (res.Error ?? new Error("failed")) : null;
            let notDone = false;
            const v = res.Value;
            if (!error && sc && sc.Implicit && Array.isArray(v)) {
                for (const x of v) {
                    if (x && x.status === "rejected") {
                        error = error || (x.reason ?? new Error("failed"));
                    }
                    else if (x && x.value === false)
                        notDone = true;
                }
            }
            else if (!error && v === false)
                notDone = true;
            const sigAborted = !!(sc && sc.Controller && sc.Controller.signal && sc.Controller.signal.aborted);
            //  The one cancellation model (Sw.Cancel, core/base.js): "stopped" only when the work confirmed it (an AbortError); work that
            //  finished although Cancel was asked is done ("Finished before it could stop": AfterCancel), never "cancelled"
            const aborted = !!(error && error.name === "AbortError") || (sigAborted && !error && notDone);
            //  (the work's Done for Work.Current: a draft waiting for it is cleared only on success)
            if (sc && typeof sc.Settle === "function")
                sc.Settle(!error && !notDone && !aborted);
            return {
                Ok: !error && !notDone && !aborted, Aborted: aborted, Error: aborted ? null : error, NotDone: !aborted && !error && notDone,
                AfterCancel: sigAborted && !aborted,
                Wrote: !!(sc && sc.Writes > 0), Value: sc && sc.Implicit ? undefined : v,
                //  Announced already by whoever stopped / failed it (a dialog's Cancel / Stop waiting, the activity list): Report adds nothing
                Said: !!(error && typeof error === "object" && error.SwSaid === true),
            };
        },
        /**
         * Watch whether running work shows something itself (FBK-2 / FRM-3: a result in one place): a toast, a changed alert / form
         * status line / field message (not the screen reader only region, not a live list or clock, not a dialog: a question), or a
         * field marked invalid. Stop() tells.
         * @returns {{Stop: function():boolean}} Stop watching; true if the work said something.
         */
        Watch() {
            let seen = false;
            const said = n => {
                const e = n && (n.nodeType === 1 ? n : n.parentElement);
                if (!e || !e.closest)
                    return false;
                if (e.closest(".sw-page-live, .sw-sr-only"))
                    return false;
                //  (only what a user sees as a message: a live clock or a refreshed list with aria-live is no result)
                //  (a dialog is no result: a confirmation opened by the work is a question)
                return !!(e.closest(".sw-toast, .sw-toasts, [role=alert], .sw-form-status, .sw-text-error, .sw-field-error")
                    || (e.querySelector && e.querySelector(".sw-toast, [role=alert]")));
            };
            const mo = typeof MutationObserver === "function" ? new MutationObserver(list => {
                if (seen)
                    return;
                for (const m of list) {
                    if (m.type === "attributes") {
                        if (m.attributeName === "aria-invalid" && m.target.getAttribute("aria-invalid") === "true")
                            seen = true;
                        else if (m.attributeName === "hidden" && !m.target.hidden && said(m.target))
                            seen = true;
                    }
                    else if (said(m.target) || Array.from(m.addedNodes).some(said))
                        seen = true;
                    if (seen)
                        break;
                }
            }) : null;
            if (mo && document.body)
                mo.observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ["aria-invalid", "hidden"] });
            return {
                Stop: () => {
                    if (mo) {
                        //  (changes queued but not delivered yet count too)
                        const rest = mo.takeRecords();
                        if (!seen && rest.length)
                            seen = rest.some(m => m.type === "attributes" ? (m.attributeName === "aria-invalid" && m.target.getAttribute("aria-invalid") === "true") : (said(m.target) || Array.from(m.addedNodes).some(said)));
                        mo.disconnect();
                    }
                    return seen;
                },
            };
        },
        /**
         * Show a failure the one way: a refused request (403, Sw.Access) names the role needed; a lost connection says "<name>: not
         * done, the connection to the server was lost" (nothing while the shell says it is offline: FBK-9); else the error text as a fail
         * toast. Never a raw "Failed to fetch".
         * @param {*} x The error.
         * @param {object} [o] { Name (what failed, ex: the button's name), Button (Sw.Button: Sw.Access disables it with the reason) }.
         */
        Error(x, o) {
            optCheck("Sw.Work.Error", o);
            o = o || {};
            if (Sw.Access && Sw.Access.IsDenied && Sw.Access.IsDenied(x)) {
                console.warn(x && x.message ? x.message : x);
                if (Sw.Access.ActionDenied)
                    Sw.Access.ActionDenied(x, o.Button);
                return;
            }
            if (Sw.Net && Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(x)) {
                console.warn(x);
                if (Sw.Fail && Sw.Net.Online !== false)
                    Sw.Fail(o.Name
                        ? _T("{0}: not done, the connection to the service was lost. Try again when it is back.", o.Name, "Error after a button's action failed because the service could not be reached. {0} is the button's name, ex: 'Save'")
                        : _TF("Not done: the connection to the service was lost. Try again when it is back.", "Error after an action failed because the service could not be reached"));
                return;
            }
            console.error(x);
            //  One sentence "<name>: not done. <why>" built from the error object (its status and the server's reason), so Sw.Fail
            //  doesn't read it as a new message with a prefix ("not done.: …", F16)
            if (Sw.Fail)
                Sw.Fail(failText(o.Name || "", x));
        },
        /** The one failure sentence: FailText(name, error) -> "<name>: not done. <why>" (Sw.Net.FailText when the core has it). */
        FailText: failText,
        /** The options check of the ui (DevMode): the core's Sw.DevOptions (unknown names, types) plus value Check, Collides, $Check. */
        CheckOptions: DevOptions,
        /** Register a ui api's options schema by name (Sw.DevOptions.Register of the core too): RegisterOptions(where, schema). */
        RegisterOptions,
        /**
         * Show the outcome of work in exactly one place (FBK-2, FBK-5): nothing when the work showed something itself (o.Said) or when
         * the caller shows it in place; else a failure toast (Work.Error), "<name>: not done." for a false result that said nothing (and a
         * developer warning), "<name> stopped: it may already have been done on the server." for an aborted write ("<name> stopped." is
         * only announced for an aborted read), else "<name>: done" announced when the work showed a spinner (or o.Done is a text).
         * @param {object} out The outcome (Work.Outcome).
         * @param {object} [o] { Name, Said (the work said something: Work.Watch), Done (false: no announcement; a text), Spun, Button }.
         * @returns {string} What was shown: "said", "error", "notdone", "stopped", "done" or "".
         */
        Report(out, o) {
            optCheck("Sw.Work.Report", o);
            o = o || {};
            const name = o.Name || "";
            //  One result per outcome (FD N2: the snippet pasted into two branches showed the failure twice)
            if (out && typeof out === "object") {
                if (out.SwReported) {
                    DevWarn('Sw.Work.Report was called twice for one outcome' + (name ? ' ("' + name + '")' : "") + ": the result is shown once; remove the second call (UX guide FBK-5).", "report-twice:" + name);
                    return "said";
                }
                try {
                    Object.defineProperty(out, "SwReported", { value: true, configurable: true });
                }
                catch {
                    //  (a frozen outcome: reported every time)
                }
            }
            //  Announced already (a dialog's "Save cancelled" / "Stopped waiting for Save…", the activity list's failure): nothing more
            if (out.Said)
                return "said";
            if (out.Aborted) {
                if (out.Wrote) {
                    if (Sw.Toast)
                        Sw.Toast(name
                            ? _T("{0} stopped: it may already have been done by the service.", name, "Message after a running action that sends data was cancelled. {0} is the action's name, ex: 'Transfer'")
                            : _TF("Stopped: it may already have been done by the service.", "Message after a running action that sends data was cancelled"), { Kind: "warning" });
                }
                else if (Sw.Page && Sw.Page.Announce)
                    Sw.Page.Announce(name ? _T("{0} stopped.", name, "Announced after a running action was cancelled. {0} is the action's name, ex: 'Save'") : _TF("Stopped.", "Announced after a running action was cancelled"));
                return "stopped";
            }
            if (out.Error) {
                if (o.Said && !(Sw.Net && Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(out.Error)))
                    return "said";
                Work.Error(out.Error, { Name: name, Button: o.Button });
                return "error";
            }
            if (out.NotDone) {
                if (o.Said)
                    return "said";
                DevWarn('"' + (name || "An action") + '" ended with false ("not done") and showed nothing: say why (a toast, a status line, a field message) or throw an Error with the reason (UX guide FBK-5); the user now sees "' + (name || "Not done") + ': not done."', "notdone-silent:" + name);
                if (Sw.Fail)
                    Sw.Fail(name ? _T("{0}: not done.", name, "Error after an action ended without doing its work and without saying why. {0} is the action's name, ex: 'Save'") : _TF("Not done.", "Error after an action ended without doing its work and without saying why"));
                return "notdone";
            }
            //  Cancel was asked but the work finished: say so (the user expects it stopped; Sw.Cancel's words)
            if (out.AfterCancel && !o.Said && Sw.Page && Sw.Page.Announce) {
                const fin = Sw.Cancel && Sw.Cancel.Texts && Sw.Cancel.Texts.FinishedAnyway ? Sw.Cancel.Texts.FinishedAnyway() : _TF("Finished before it could stop", "State of work that finished although the user chose Cancel (it could not stop in time)");
                Sw.Page.Announce(name ? _T("{0}: {1}", name, fin, "Announced when work finished although the user chose Cancel. {0} is the action's name, ex: 'Save', {1} the state 'Finished before it could stop'") : fin);
                return "done";
            }
            if (!o.Said && o.Done !== false && (o.Spun || typeof o.Done === "string") && Sw.Page && Sw.Page.Announce) {
                Sw.Page.Announce(typeof o.Done === "string" ? o.Done : _T("{0}: done", name, "Announced to screen readers when a button's action that showed a spinner has finished. {0} is the button's name, ex: 'Save'"));
                return "done";
            }
            return o.Said ? "said" : "";
        },
        async Busy(e, p, o) {
            optCheck("Sw.Work.Busy", o);
            o = o || {};
            //  (Work.Current: the busy work of this element and its outcome, o.Done = the scope's Done when the caller has it)
            const done = o.Done || Promise.resolve(p).then(v => v !== false, () => false);
            busyWorks.set(e, done);
            Promise.resolve(p).catch(() => { }).finally(() => {
                if (busyWorks.get(e) === done)
                    busyWorks.delete(e);
            });
            const name = o.Name || accessibleName(e) || "";
            const before = o.Modals || openModals();
            const start = Date.now();
            const titleBefore = e.getAttribute("title");
            let spun = false, spin = null, iconHidden = null, leaveKey = null, timer = 0;
            const waiting = () => {
                for (const d of openModals())
                    if (!before.has(d) && !d.contains(e))
                        return true;
                return false;
            };
            const setSpin = on => {
                if (o.SetSpin) {
                    o.SetSpin(on);
                    return;
                }
                if (on && !spin) {
                    const w = e.getBoundingClientRect().width;
                    e.style.minWidth = w + "px";
                    spin = Sw.Spinner ? new Sw.Spinner({ Class: "sw-busy-spin" }).E : el("span.sw-busy-spin", { attrs: { "aria-hidden": "true" } });
                    const ic = e.querySelector(":scope > .sw-icon, :scope > svg, :scope > img");
                    if (ic) {
                        //  In the icon's place, at the icon's size (the width stays exactly: FBK-3)
                        const r = ic.getBoundingClientRect();
                        if (r.width && r.height) {
                            spin.style.width = r.width + "px";
                            spin.style.height = r.height + "px";
                        }
                        iconHidden = ic;
                        ic.hidden = true;
                        ic.after(spin);
                    }
                    else
                        SpinBeside(e, spin);
                    e.classList.add("sw-working");
                    //  (a rounding difference never grows it: the width is kept)
                    if (e.getBoundingClientRect().width > w + 0.5)
                        e.style.maxWidth = w + "px";
                }
                else if (!on && spin) {
                    spin.remove();
                    spin = null;
                    if (iconHidden)
                        iconHidden.hidden = false;
                    iconHidden = null;
                    SpinBeside(e, null);
                    e.classList.remove("sw-working", "sw-working-over");
                    e.style.minWidth = "";
                    e.style.maxWidth = "";
                }
            };
            const tick = () => {
                const ms = Date.now() - start;
                const wait = waiting();
                if (wait) {
                    //  Waiting for the user (a confirmation): not busy (FBK-2: busy = work running)
                    e.removeAttribute("aria-busy");
                    setSpin(false);
                }
                else {
                    e.setAttribute("aria-busy", "true");
                    if (ms >= 250 && !spin) {
                        setSpin(true);
                        if (!spun && o.Announce !== false && Sw.Page && Sw.Page.Announce)
                            Sw.Page.Announce(_TF("Working…", "Announced to screen readers when a button's action still runs after a quarter of a second (the button shows a spinner)"));
                        spun = true;
                    }
                    if (ms >= 2000) {
                        //  What it waits for and the time so far, read on demand (FBK-4: never in a live region)
                        const t = name ? _T("{0}: working ({1})", name, elapsedText(ms), "Tool tip of a busy button after 2 s. {0} is the button's name, {1} the elapsed time, ex: 'Kill: working (3 s)'") : elapsedText(ms);
                        if (o.SetTitle)
                            o.SetTitle(t);
                        else
                            e.title = t;
                        if (!leaveKey && o.Leave !== false && Sw.Page && Sw.Page.SetDirty) {
                            leaveKey = "sw-work:" + (++workN);
                            //  The true consequence by the kind of work (Kind "server": the server still does it, leaving only
                            //  loses the result; core/page.js WorkText), else "<name> may not finish."
                            if (o.Kind && Sw.Page.WorkText)
                                Sw.Page.SetDirty(leaveKey, true, { Work: true, Name: o.Work || name, Kind: o.Kind });
                            else
                                Sw.Page.SetDirty(leaveKey, name
                                ? _T("{0} may not finish.", name, "Text of the question 'Leave this page?' while a button's action still runs. {0} is the button's name, ex: 'Save'")
                                : _TF("Work that is still running may not finish.", "Text of the question 'Leave this page?' while an action still runs"), { Work: true });
                        }
                    }
                }
                timer = setTimeout(tick, ms < 250 ? 250 - ms : 250);
            };
            e.setAttribute("aria-busy", "true");
            timer = setTimeout(tick, 0);
            let failed = false, error = null, value;
            try {
                value = await p;
            }
            catch (x) {
                failed = true;
                error = x;
            }
            finally {
                clearTimeout(timer);
                e.removeAttribute("aria-busy");
                setSpin(false);
                if (Date.now() - start >= 2000) {
                    if (o.SetTitle)
                        o.SetTitle(null);
                    else if (titleBefore === null)
                        e.removeAttribute("title");
                    else
                        e.title = titleBefore;
                }
                if (leaveKey && Sw.Page && Sw.Page.ClearDirty)
                    Sw.Page.ClearDirty(leaveKey);
            }
            return { Failed: failed, Error: error, Spun: spun, Value: value };
        },
        OpenModals: openModals,
        Elapsed: elapsedText,
        SpinBeside,
        /**
         * Record the writes (Sw.Net.Send, a fetch that is not GET / HEAD) started anywhere until Stop(), also after an await: the
         * controls use it to see that a button's or Enter's work sent the edited values (a save, SAF-10).
         * @returns {{Stop: function():Array<{Name: string, Method: string, Url: string, Body: *}>}} Stop recording; the writes.
         */
        Record() {
            trackRequests();
            const list = [];
            recorders.add(list);
            return {
                Stop: () => {
                    recorders.delete(list);
                    return list;
                },
            };
        },
    };
    let workN = 0;

    /**
     * Make any element clickable the right way (UX guide INP-1, INP-2, FBK-2): Enter / Space click it (Sw.KeyClick, a Tab stop), a pure
     * click runs fn, a modifier click only moves the focus there (Sw.ModifierClick), a disabled element (Sw.Disabled) says why, and work
     * started by fn makes the element busy (a returned promise, or the requests it started: Sw.Work) with repeats absorbed. Errors
     * become a fail toast. Use it for cards, tiles and hand made rows instead of addEventListener("click").
     * @param {HTMLElement} e The element.
     * @param {function(Event, {Signal: AbortSignal}):*} fn The action.
     * @param {object} [o] Options: { Name (for the announcements / busy tool tip), Leave (false: no leave question for long work),
     * NoKey (don't make it keyboard clickable: it is a button or link already) }.
     * @returns {function():void} Removes the handlers.
     */
    function OnClick(e, fn, o) {
        optCheck("Sw.OnClick", o);
        o = o || {};
        let busy = false;
        if (!o.NoKey && !/^(BUTTON|A|INPUT|SUMMARY)$/.test(e.tagName))
            KeyClick(e);
        const h = async ev => {
            if (ModifierClick(ev))
                return;
            //  A click on an interactive child (a link, a button inside a card) is that child's
            const inner = ev.target && ev.target.closest ? ev.target.closest(InteractiveSel) : null;
            if (inner && inner !== e && e.contains(inner))
                return;
            if (Disabled.Is(e)) {
                Disabled.Reject(e, ev);
                return;
            }
            if (busy)
                return;
            const modals = openModals();
            const name = o.Name || accessibleName(e) || "";
            const sc = Work.Scope(signal => fn(ev, { Signal: signal }));
            if (sc.Threw) {
                Work.Report(Work.Outcome(sc, { Failed: true, Error: sc.Error }), { Name: name });
                return;
            }
            const p = Work.Promise(sc);
            if (!p)
                return;
            busy = true;
            const off = () => sc.Controller && sc.Controller.abort();
            window.addEventListener("pagehide", off);
            const watch = Work.Watch();
            //  Focus kept through a rebuild the work causes (FOC-1)
            const keep = Focus.Keep(e.parentElement || e);
            const r = await Work.Busy(e, p, { Name: o.Name, Leave: o.Leave, Modals: modals });
            window.removeEventListener("pagehide", off);
            busy = false;
            //  The one result path (FBK-5): a failure or a false result is shown once, never "done"
            Work.Report(Work.Outcome(sc, r), { Name: name, Said: watch.Stop(), Spun: r.Spun, Done: o.Done });
            keep.Restore();
            keep.Stop();
        };
        e.addEventListener("click", h);
        return () => {
            e.removeEventListener("click", h);
            if (!o.NoKey)
                RemoveKeyClick(e);
        };
    }

    // ------------------------------------------------------------------ focus

    /** Css selector of elements that may take focus (filtered further by Focus.IsTabbable). */
    const FocusSelector = "a[href],area[href],button,input,select,textarea,iframe,summary,[tabindex],[contenteditable=''],[contenteditable='true']";

    /** @type {Array<{E: HTMLElement, Prev: Element|null}>} The focus trap stack (last = active). */
    const traps = [];
    /** @type {Element|null} The last element that had the focus inside the active trap. */
    let trapLast = null;

    /**
     * The window got the focus (ex: the app shell focused this frame after a dialog was opened from code, while the frame had no
     * focus): when the focus is not in the active trap yet, move it there (the preferred element, Focus.Initial, else the first
     * tabbable one), so the keyboard works (Tab, Enter, Escape) however the dialog was opened.
     */
    function onTrapWindowFocus() {
        const t = traps[traps.length - 1];
        if (!t || !t.E.isConnected)
            return;
        setTimeout(() => {
            const ae = document.activeElement;
            if (ae && ae !== document.body && t.E.contains(ae))
                return;
            const ini = t.Initial && t.Initial.isConnected && t.E.contains(t.Initial) && Focus.IsTabbable(t.Initial) ? t.Initial : null;
            const f = ini || Focus.Tabbables(t.E)[0] || t.E;
            if (f === t.E && !f.hasAttribute("tabindex"))
                f.tabIndex = -1;
            f.focus({ preventScroll: true });
        }, 0);
    }

    /**
     * Capturing focusin handler enforcing the active focus trap: focus that lands outside the trap (or on a sw-tabblock sentinel)
     * is moved back to the last focused element inside it, or to the first tabbable element / the trap itself.
     * @param {FocusEvent} ev The focus event.
     */
    function onTrapFocus(ev) {
        const t = traps[traps.length - 1];
        if (!t)
            return;
        if (!t.E.isConnected) {
            Focus.Release(t.E);
            return;
        }
        const target = ev.target;
        if (target && target.classList && target.classList.contains("sw-tabblock")) {
            //  A sentinel at an edge: wrap around
            const all = Focus.Tabbables(t.E);
            const n = target.dataset.edge === "start" ? all[all.length - 1] : all[0];
            (n || t.E).focus({ preventScroll: true });
            return;
        }
        if (t.E.contains(target)) {
            trapLast = target;
            return;
        }
        //  Focus escaped (Tab past the end, a click on the page behind ...). with no focus inside the trap yet (a dialog
        //  opened while its frame had no focus), the trap's preferred element (Focus.Initial, ex: Cancel of a danger dialog) wins
        //  over the first tabbable element (the dialog's Close button)
        const ini = t.Initial && t.Initial.isConnected && t.E.contains(t.Initial) && Focus.IsTabbable(t.Initial) ? t.Initial : null;
        const back = (trapLast && t.E.contains(trapLast) && trapLast.isConnected) ? trapLast : (ini || Focus.Tabbables(t.E)[0] || t.E);
        if (!back.hasAttribute("tabindex") && back === t.E)
            back.tabIndex = -1;
        back.focus({ preventScroll: true });
    }

    /**
     * Capturing Tab handler of the active focus trap: Tab on the last tabbable element wraps to the first, Shift+Tab on the first
     * wraps to the last (focus leaving the document to the browser ui would not fire a focus event the trap could correct).
     * @param {KeyboardEvent} ev The key event.
     */
    function onTrapKey(ev) {
        //  A key while the focus is still outside the active modal dialog (S7b-FD-N1: the server block opened while a page field had the
        //  focus and it stayed there; Escape neither closed the block nor stayed out of the page): the focus goes into the dialog and
        //  the key with it, so the page behind never gets it. Only modal dialogs (aria-modal): a popup list may keep the focus in its field
        const top = traps[traps.length - 1];
        const tgt = ev.target;
        if (top && top.E.isConnected && ev.isTrusted && top.E.getAttribute("aria-modal") === "true" && tgt instanceof Node && !top.E.contains(tgt) &&
            !(tgt instanceof Element && tgt.closest("[aria-modal='true']"))) {
            ev.preventDefault();
            ev.stopPropagation();
            const ini = top.Initial && top.Initial.isConnected && top.E.contains(top.Initial) && Focus.IsTabbable(top.Initial) ? top.Initial : null;
            const f = (trapLast && top.E.contains(trapLast) && trapLast.isConnected) ? trapLast : (ini || Focus.Tabbables(top.E)[0] || top.E);
            if (f === top.E && !f.hasAttribute("tabindex"))
                f.tabIndex = -1;
            f.focus({ preventScroll: true });
            //  (Escape acts on the dialog now: a close attempt, always safe. Enter is not passed on: it was meant for the page's field and
            //  must never submit a dialog the user did not look at (EnterValue); Tab only moved the focus in; typing is dropped)
            if (ev.key === "Escape" && document.activeElement === f)
                f.dispatchEvent(new KeyboardEvent("keydown", { key: ev.key, code: ev.code, shiftKey: ev.shiftKey, bubbles: true, cancelable: true }));
            return;
        }
        if (ev.key !== "Tab" || ev.ctrlKey || ev.altKey || ev.metaKey)
            return;
        const t = traps[traps.length - 1];
        if (!t || !t.E.isConnected)
            return;
        const all = Focus.Tabbables(t.E);
        if (!all.length) {
            ev.preventDefault();
            return;
        }
        const a = document.activeElement;
        const i = all.indexOf(a);
        if (ev.shiftKey ? (i <= 0) : (i < 0 || i === all.length - 1)) {
            ev.preventDefault();
            (ev.shiftKey ? all[all.length - 1] : all[0]).focus();
        }
    }

    /** @type {?Element} The element that had the focus last (outside any trap set later): a trap gives the focus back to it. */
    let lastFocused = null;
    if (typeof document !== "undefined")
        document.addEventListener("focusin", ev => {
            if (ev.target instanceof Element && ev.target !== document.body && !(traps.length && traps[traps.length - 1].E.contains(ev.target)))
                lastFocused = ev.target;
        }, true);

    /** @type {Set<Element>} The containers a Focus.Keep watches now (the focus rescue leaves them to it). */
    const focusKeeps = new Set();

    /**
     * The focus never stays on the page body after a result (FOC-1, FOC-2: "Invitation sent" replaced the form, "Account
     * deleted", a sent form: the focus fell to BODY): when the focused element is removed or hidden and nothing took the focus 300 ms
     * later, the focus goes to the nearest part that is still there: its result (a status / alert line, a form status, a state view, an
     * element marked data-sw-result), else its heading, else its first control. Not inside dialogs, menus, the shell's navigation or a
     * container a Focus.Keep watches (they place it themselves); a DevWarn names where it went, so the page can do it on purpose.
     */
    function focusRescue() {
        if (focusRescue.Done || typeof document === "undefined")
            return;
        focusRescue.Done = true;
        let lastE = null, chain = [], timer = 0;
        //  The element the USER acted on while it had the focus (a trusted key or press): only its loss is rescued; a focus set by
        //  code (a page load, the shell's navigation, autofocus) that goes away is none of the user's business (no move, no warning)
        let userE = null;
        ["keydown", "pointerdown", "click"].forEach(n => document.addEventListener(n, ev => {
            const a = document.activeElement;
            if (ev.isTrusted && a && a !== document.body && a !== document.documentElement)
                userE = a;
        }, true));
        document.addEventListener("focusin", ev => {
            const t = ev.target;
            if (!(t instanceof Element) || t === document.body)
                return;
            lastE = t;
            chain = [];
            for (let p = t.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement)
                chain.push(p);
        }, true);
        const gone = x => !x || !x.isConnected || !x.getClientRects().length || !!x.closest("[hidden], [inert]");
        const lostNow = () => {
            const a = document.activeElement;
            return !a || a === document.body || a === document.documentElement;
        };
        const shown = x => !!(x && x.isConnected && x.getClientRects().length && !x.closest("[hidden], [inert]"));
        const rescue = () => {
            timer = 0;
            const was = lastE;
            if (!was || was !== userE || !gone(was) || !lostNow() || !Focus.CanTake() || !document.hasFocus())
                return;
            //  (dialogs, menus and the shell's own parts place the focus themselves; a kept container too)
            if (openModals().size || [was].concat(chain).some(p => p instanceof Element && p.matches(".sw-dialog, [aria-modal='true'], [role=dialog], [role=alertdialog], [role=menu], [role=listbox], [role=menubar], nav, .sw-popup, .sw-toasts, .sw-shell-nav, .sw-table, [role=grid], table")))
                return;
            for (const k of focusKeeps)
                if (k.isConnected && (k.contains(was) || chain.includes(k)))
                    return;
            const host = chain.find(p => p.isConnected && shown(p)) || document.querySelector("main, [role=main]") || document.body;
            //  the result first, then the heading, then the first control (never another dialog / menu)
            const pick = root => {
                const res = Array.from(root.querySelectorAll("[data-sw-result], .sw-form-status, [role=alert], [role=status], .sw-state")).find(x => shown(x) && (x.textContent || "").trim() && !x.closest(".sw-page-live, .sw-sr-only, .sw-toasts"));
                if (res)
                    return res.matches(".sw-state") ? (res.querySelector("h1, h2, h3, h4, .sw-state-title") || res) : res;
                const h = Array.from(root.querySelectorAll("h1, h2, h3, [role=heading]")).find(shown);
                if (h)
                    return h;
                return Focus.Tabbables(root)[0] || null;
            };
            let target = null;
            for (let r = host; r && !target; r = r.parentElement)
                target = pick(r);
            if (!target)
                return;
            if (!target.matches(FocusSelector))
                target.tabIndex = -1;
            try {
                target.focus({ preventScroll: true });
            }
            catch {
                target.focus();
            }
            if (document.activeElement === target)
                DevWarn('The focused element was removed or hidden and the focus fell to the page body; it was moved to "' + ((target.getAttribute("aria-label") || target.textContent || target.tagName).trim().slice(0, 60)) + '". Put the focus on the result yourself (Sw.Focus.Show(resultElement)) or keep the button (UX guide FOC-1, FOC-2).', "focus-rescue:" + (was.id || was.className || was.tagName));
        };
        const later = () => {
            if (!timer)
                timer = setTimeout(rescue, 300);
        };
        document.addEventListener("focusout", ev => {
            if (!ev.relatedTarget)
                later();
        }, true);
        if (typeof MutationObserver === "function")
            new MutationObserver(() => {
                if (lastE && !lastE.isConnected && lostNow())
                    later();
            }).observe(document.documentElement, { childList: true, subtree: true });
    }

    /** Focus helpers: tabbable queries and a stack of focus traps (modal popups, dialogs, overlays). */
    const Focus = {
        /**
         * Test if this document may move the focus programmatically without stealing it from another document: always in the top
         * window, in a frame only while the frame's document has the focus. Calling focus() inside an unfocused (embedded, maybe hidden)
         * frame moves the keyboard focus of the PARENT page into the frame (an old browser behaviour), so overlays shown by embedded pages (loader,
         * dialogs, server block) only take the focus when the frame already has it; their trap still catches the focus when the user
         * moves into the frame later.
         * @returns {boolean} True if focus() may be called.
         */
        CanTake() {
            let top = true;
            try {
                top = window.top === window;
            }
            catch {
                top = false;
            }
            return top || document.hasFocus();
        },
        /**
         * Focus an element if this document may take the focus (see Focus.CanTake).
         * @param {HTMLElement|null} e The element (null is ignored).
         * @param {boolean} [scroll] Scroll the element into view (default false: preventScroll).
         * @returns {boolean} True if the element was focused.
         */
        Set(e, scroll) {
            if (!e || !e.focus || !Focus.CanTake())
                return false;
            e.focus({ preventScroll: !scroll });
            return true;
        },
        /**
         * Focus an element and show the focus ring, also after a pointer click (UX guide INP-2: a modifier click shows where the focus
         * went): focus({ focusVisible: true }); where the browser ignores that option (or the element had the focus already, ex: a
         * button focused by its own mouse down) the same --sw-focus outline is drawn by the class sw-focus-shown, removed on the next
         * pointer down or when the element loses the focus. An element that can't take the focus gets tabindex -1 first.
         * @param {HTMLElement|null} e The element.
         * @param {boolean} [scroll] Scroll it into view (default false).
         * @returns {boolean} True if the element has the focus.
         */
        Show(e, scroll) {
            if (!e || !e.focus || !Focus.CanTake())
                return false;
            if (!e.matches(FocusSelector))
                e.tabIndex = -1;
            try {
                e.focus({ preventScroll: !scroll, focusVisible: true });
            }
            catch {
                e.focus();
            }
            if (document.activeElement !== e)
                return false;
            let shown = false;
            try {
                shown = e.matches(":focus-visible");
            }
            catch {
            }
            if (!shown) {
                clearFocusShown();
                focusShownE = e;
                e.classList.add("sw-focus-shown");
                e.addEventListener("blur", clearFocusShown);
                setTimeout(() => {
                    if (focusShownE === e)
                        document.addEventListener("pointerdown", clearFocusShown, true);
                }, 0);
            }
            return true;
        },
        /**
         * Give the focus to a target the safe way (FOC-1: never to the body): the target when it can take the focus and is shown, else its
         * first tabbable element (a component root, a card, a row), else nothing.
         * @param {?Element} target The element.
         * @returns {boolean} True if the focus is in the target now.
         */
        Restore(target) {
            if (!target || !target.isConnected || !Focus.CanTake())
                return false;
            const shown = x => !!(x.getClientRects().length) && !x.closest("[inert], [hidden]");
            let f = null;
            if (shown(target) && target.matches(FocusSelector) && !target.matches(":disabled") && target.tabIndex >= -1 && !(target.tagName === "INPUT" && target.type === "hidden"))
                f = target;
            else
                f = Focus.Tabbables(target)[0] || null;
            if (!f)
                return false;
            try {
                f.focus({ preventScroll: true });
            }
            catch {
                f.focus();
            }
            return document.activeElement === f || target.contains(document.activeElement);
        },
        /**
         * Keep the keyboard place inside a container through a rebuild, a hidden or removed element, or async work (FOC-1; row actions,
         * refreshed lists, rebuilt cells and tabs): remembers the focused element in it (and follows the focus while it is kept); Restore()
         * acts only when the focus was lost (on the body, or on an element that is gone or hidden): the same element when it is back,
         * else the same id, else in the same row / item (data-sw-item / row position) the element with the same name, its done note
         * (.sw-table-done) or its first tabbable, else the same name anywhere in the container, else the same position, else the
         * container itself (tabindex -1). In DevMode a warning when nothing could take it.
         * Keep(container, work): runs / waits for work (a function or a promise), restores after it and resolves with its result.
         * @param {Element|{E: Element}} container The container.
         * @param {function|Promise} [work] The work.
         * @param {{From: Element}} [o] From: the element the place is (when the focus is not in the container yet, ex: a dialog that
         * started from that row button is still closing and gives the focus back later).
         * @returns {{Restore: function():boolean, Stop: function():void}|Promise<*>} The handle (no work), else the work's promise.
         */
        Keep(container, work, o) {
            optCheck("Sw.Focus.Keep", o);
            const c = container && container.E instanceof Element ? container.E : container;
            if (work !== undefined && work !== null) {
                const h = Focus.Keep(c, null, o);
                let p;
                try {
                    p = Promise.resolve(typeof work === "function" ? work() : work);
                }
                catch (x) {
                    p = Promise.reject(x);
                }
                const end = () => setTimeout(() => {
                    h.Restore();
                    h.Stop();
                }, 0);
                p.then(end, end);
                return p;
            }
            if (!(c instanceof Element))
                return { Restore: () => false, Stop: () => { } };
            let last = null, info = null;
            const describe = a => {
                const row = a.closest("tr, [role=row], [data-sw-item], li");
                const inRow = row && c.contains(row) ? row : null;
                const cell = a.closest("td, th, [role=gridcell]");
                return {
                    Id: a.id || "", Key: a.getAttribute("data-sw-focus-key") || "",
                    Label: (a.getAttribute("aria-label") || a.textContent || "").trim().slice(0, 80), Tag: a.tagName,
                    Row: inRow, RowKey: inRow ? (inRow.getAttribute("data-sw-item") || inRow.SwItem || "") : "",
                    RowIndex: inRow && inRow.parentElement ? Array.prototype.indexOf.call(inRow.parentElement.children, inRow) : -1,
                    RowParent: inRow ? inRow.parentElement : null, Cell: cell ? cell.cellIndex ?? -1 : -1,
                    Index: Focus.Tabbables(c).indexOf(a),
                };
            };
            const note = a => {
                if (a && a !== c && a.nodeType === 1 && c.contains(a)) {
                    last = a;
                    info = describe(a);
                }
            };
            //  From is the place (a row's action button: its result belongs there), before where the focus is now (Ops R4-7: the
            //  focus sat on another row's result when a failed action of this row started, and stayed there)
            const from = o && o.From && o.From.E instanceof Element ? o.From.E : (o && o.From);
            if (from instanceof Element)
                note(from);
            if (!last)
                note(document.activeElement);
            //  (with From: the focus moves only by the user's own keys / presses from now on; a focus put on another row's result by
            //  code, a closing dialog's fallback or a rebuild, is not the user's choice)
            let userMoved = false;
            const onUser = ev => {
                if (ev.isTrusted)
                    userMoved = true;
            };
            const onIn = ev => {
                if (!from || userMoved)
                    note(ev.target);
            };
            c.addEventListener("focusin", onIn);
            focusKeeps.add(c);
            if (from) {
                document.addEventListener("keydown", onUser, true);
                document.addEventListener("pointerdown", onUser, true);
            }
            const shown = x => !!(x && x.isConnected && x.getClientRects().length && !x.closest("[inert], [hidden]"));
            const lost = () => {
                const a = document.activeElement;
                if (!a || a === document.body || a === document.documentElement || !a.isConnected || (a === last && !shown(a)))
                    return true;
                //  another row's result note (Ops R4-7: "Kill: done" of row B after row A's Kill failed) is no place for this result
                return !!(from && !userMoved && info && info.Row && a !== last && c.contains(a) && !info.Row.contains(a) && a.closest(".sw-table-done, .sw-table-failnote, .sw-cell-note, tr, [role=row], [data-sw-item]"));
            };
            const named = (root, i) => {
                if (!root || !i.Label)
                    return null;
                return Focus.Tabbables(root).find(x => x.tagName === i.Tag && (x.getAttribute("aria-label") || x.textContent || "").trim().slice(0, 80) === i.Label) || null;
            };
            const take = x => {
                if (!x || !x.isConnected)
                    return false;
                if (!x.matches(FocusSelector))
                    x.tabIndex = -1;
                return Focus.Restore(x);
            };
            //  (inside a dialog: when it closes, the dialog gives the focus back to its opener itself)
            const dlg = c.closest(".sw-dialog, [aria-modal='true']");
            return {
                Restore() {
                    if (!last || !lost())
                        return !!last;
                    if (dlg && (!dlg.isConnected || dlg.classList.contains("sw-leaving") || dlg.closest(".sw-leaving")))
                        return false;
                    const i = info;
                    if (shown(last) && Focus.Restore(last))
                        return true;
                    if (!c.isConnected) {
                        //  (the container itself was replaced, ex: a form by its result: the focus rescue places the focus on what replaced it)
                        return false;
                    }
                    if (i.Id) {
                        const x = document.getElementById(i.Id);
                        if (x && c.contains(x) && shown(x) && take(x))
                            return true;
                    }
                    if (i.Key) {
                        const x = Array.from(c.querySelectorAll("[data-sw-focus-key]")).find(y => y.getAttribute("data-sw-focus-key") === i.Key);
                        if (x && shown(x) && take(x))
                            return true;
                    }
                    //  The same row / item: by its key, else the one that now has its place
                    let row = i.Row && i.Row.isConnected && c.contains(i.Row) ? i.Row : null;
                    if (!row && i.RowKey)
                        row = Array.from(c.querySelectorAll("[data-sw-item]")).find(y => y.getAttribute("data-sw-item") === i.RowKey) || null;
                    if (!row && i.RowParent && i.RowParent.isConnected && i.RowIndex >= 0) {
                        const kids = i.RowParent.children;
                        row = kids.length ? kids[Math.min(i.RowIndex, kids.length - 1)] : null;
                    }
                    if (row && shown(row)) {
                        //  (its newest result note first: a failure note over an older done note)
                        const notes = Array.from(row.querySelectorAll(".sw-table-failnote, .sw-table-done, .sw-cell-note")).filter(shown);
                        const x = named(row, i) || notes.find(n => n.matches(".sw-table-failnote")) || notes[0] || (i.Cell >= 0 && row.cells && row.cells[i.Cell] ? Focus.Tabbables(row.cells[i.Cell])[0] : null) || Focus.Tabbables(row)[0];
                        if (x && take(x))
                            return true;
                        //  The row is still there: its place is the row itself, never another row's element with the same name (a
                        //  screen reader would read "Kill: done" of another row after this row's failure)
                        const cell = i.Cell >= 0 && row.cells && row.cells[i.Cell] ? row.cells[i.Cell] : null;
                        if ((cell && take(cell)) || take(row))
                            return true;
                    }
                    const n = named(c, i);
                    if (n && take(n))
                        return true;
                    const all = Focus.Tabbables(c);
                    if (all.length && take(all[Math.max(0, Math.min(i.Index, all.length - 1))]))
                        return true;
                    if (take(c))
                        return true;
                    DevWarn("Sw.Focus.Keep: nothing could take the focus after the change, it is on the page body now (FOC-1)", "focus-keep-none");
                    return false;
                },
                Stop() {
                    c.removeEventListener("focusin", onIn);
                    focusKeeps.delete(c);
                    document.removeEventListener("keydown", onUser, true);
                    document.removeEventListener("pointerdown", onUser, true);
                },
            };
        },
        /**
         * Test if an element is visible and can take keyboard focus with Tab (the old isTabable, plus visibility and disabled checks).
         * @param {Element} e The element.
         * @returns {boolean} True if tabbable.
         */
        IsTabbable(e) {
            //  :disabled, not .disabled: a Sw control that is aria-disabled reports disabled = true and stays tabbable (INP-12)
            if (!e || e.tabIndex < 0 || e.hidden || (e.matches && e.matches(":disabled")))
                return false;
            if (e.tagName === "INPUT" && e.type === "hidden")
                return false;
            if (e.closest && e.closest("[inert]"))
                return false;
            if (!(e.offsetWidth || e.offsetHeight || e.getClientRects().length))
                return false;
            const cs = getComputedStyle(e);
            return cs.visibility !== "hidden" && cs.display !== "none";
        },
        /**
         * List the tabbable elements inside a root (document order, tabindex > 0 is not reordered).
         * @param {ParentNode} [root] The root (default document).
         * @returns {HTMLElement[]} The tabbable elements.
         */
        Tabbables(root) {
            return Array.from((root || document).querySelectorAll(FocusSelector)).filter(x => !x.classList.contains("sw-tabblock") && Focus.IsTabbable(x));
        },
        /**
         * Move the focus to the next tabbable element after an element (wraps around).
         * @param {Element} e The element to start from.
         * @param {ParentNode} [root] Limit to this root (default document).
         * @returns {HTMLElement|null} The focused element or null.
         */
        Next(e, root) {
            const all = Focus.Tabbables(root);
            if (!all.length)
                return null;
            let i = all.indexOf(e);
            if (i < 0)
                i = all.findIndex(x => e.compareDocumentPosition(x) & Node.DOCUMENT_POSITION_FOLLOWING) - 1;
            const n = all[(i + 1 + all.length) % all.length];
            n.focus();
            return n;
        },
        /**
         * Move the focus to the previous tabbable element before an element (wraps around).
         * @param {Element} e The element to start from.
         * @param {ParentNode} [root] Limit to this root (default document).
         * @returns {HTMLElement|null} The focused element or null.
         */
        Prev(e, root) {
            const all = Focus.Tabbables(root);
            if (!all.length)
                return null;
            let i = all.indexOf(e);
            if (i < 0) {
                i = all.findIndex(x => e.compareDocumentPosition(x) & Node.DOCUMENT_POSITION_FOLLOWING);
                if (i < 0)
                    i = all.length;
            }
            const n = all[(i - 1 + all.length) % all.length];
            n.focus();
            return n;
        },
        /**
         * Make Enter (without modifiers) in an input move the focus to the next tabbable element (the old tabToNextOnEnter).
         * @param {HTMLElement} input The input element.
         * @param {function(HTMLElement|null, HTMLElement):void} [onNext] Called with the newly focused element and the input.
         * @returns {function():void} Call to remove the handler.
         */
        NextOnEnter(input, onNext) {
            const fn = ev => {
                if (ev.key !== "Enter" || !IsPureEvent(ev))
                    return;
                ev.preventDefault();
                const n = Focus.Next(input);
                if (onNext)
                    onNext(n, input);
            };
            input.addEventListener("keydown", fn);
            return () => input.removeEventListener("keydown", fn);
        },
        /**
         * Trap the keyboard focus inside an element (the old BlockTab): Tab and clicks outside can't move the focus out of it.
         * Traps stack (the last one is active). Release the trap (Focus.Release or the returned function) when the element is removed;
         * a detached trap releases itself. Releasing the active trap restores the focus that was active when it was set up.
         * The trap itself never moves the focus: it only acts on focus / Tab events of its own document (a trap in an unfocused frame
         * does not touch the parent page; it catches the focus when the focus enters the frame).
         * @param {HTMLElement} e The element.
         * @returns {function():void} Releases the trap.
         */
        Trap(e) {
            if (!traps.length) {
                document.addEventListener("focusin", onTrapFocus, true);
                document.addEventListener("keydown", onTrapKey, true);
                window.addEventListener("focus", onTrapWindowFocus);
            }
            //  (the focus to give back: the focused element, else the one focused last, ex: a dialog opened after an await or by a
            //  press on something that takes no focus)
            const act = document.activeElement;
            traps.push({ E: e, Prev: act && act !== document.body && act !== document.documentElement ? act : lastFocused });
            trapLast = null;
            return () => Focus.Release(e);
        },
        /**
         * Set the preferred focus of a trap: when the focus enters the trap from outside before anything inside it had the
         * focus (ex: a dialog opened in a frame that had no focus; the focus arrives later), it goes to this element instead of the first
         * tabbable one. Sw.Dialog sets it to its default focus (Cancel in a danger dialog).
         * @param {HTMLElement} e The trapped element.
         * @param {HTMLElement|null} target The preferred element (inside e).
         */
        Initial(e, target) {
            const t = traps.find(x => x.E === e);
            if (t)
                t.Initial = target || null;
        },
        /**
         * Release a focus trap (the old UnblockTab).
         * @param {HTMLElement} e The trapped element.
         * @param {boolean} [noRestore] Don't restore the previous focus.
         */
        Release(e, noRestore) {
            const i = traps.findIndex(x => x.E === e);
            if (i < 0)
                return;
            const t = traps[i];
            const wasTop = i === traps.length - 1;
            traps.splice(i, 1);
            trapLast = null;
            if (!traps.length) {
                document.removeEventListener("focusin", onTrapFocus, true);
                document.removeEventListener("keydown", onTrapKey, true);
                window.removeEventListener("focus", onTrapWindowFocus);
            }
            if (wasTop && !noRestore) {
                //  the focus before the trap, else (it went away meanwhile) the element focused last that is still there
                const p = t.Prev && t.Prev.isConnected && t.Prev !== document.body ? t.Prev : (lastFocused && lastFocused.isConnected && !e.contains(lastFocused) ? lastFocused : null);
                if (p)
                    Focus.Set(p);
            }
        },
        /**
         * Test if an element is inside the active focus trap (or no trap is active).
         * @param {Element} e The element.
         * @returns {boolean} True if the element may take the focus.
         */
        Allowed(e) {
            const t = traps[traps.length - 1];
            return !t || t.E.contains(e);
        },
        /**
         * Create a focus sentinel element (class sw-tabblock): put one at the start and one at the end of a trapped element so Tab wraps.
         * @param {"start"|"end"} edge Which edge the sentinel is at.
         * @returns {HTMLElement} The sentinel (tabindex 0, invisible).
         */
        Sentinel(edge) {
            return el("span.sw-tabblock", { tabIndex: 0, dataset: { edge }, attrs: { "aria-hidden": "true" } });
        },
    };

    // ------------------------------------------------------------------ keyboard shortcuts

    /** @type {Array<{Combo: string, Key: string, Ctrl: boolean, Alt: boolean, Shift: boolean, Meta: boolean, Fn: function, Title: string, InInputs: boolean}>} */
    const shortcuts = [];

    /** True on Apple platforms ("Mod" is Cmd there). */
    const isMac = /Mac|iPhone|iPad|iPod/.test(navigator.platform || navigator.userAgent || "");

    /**
     * Parse a shortcut combination ("Ctrl+Shift+K", "Mod+S" (Cmd on Apple, else Ctrl), "Escape", "?", "Alt+ArrowUp").
     * @param {string} combo The combination.
     * @returns {{Key: string, Ctrl: boolean, Alt: boolean, Shift: boolean, Meta: boolean}} The parsed combination.
     */
    function parseCombo(combo) {
        const parts = combo.split("+").map(x => x.trim()).filter(x => x);
        //  "Ctrl++" ends with an empty part: the key is "+"
        const key = combo.endsWith("++") ? "+" : parts.pop();
        const r = { Key: key.length === 1 ? key.toLowerCase() : key, Ctrl: false, Alt: false, Shift: false, Meta: false };
        for (const p of parts) {
            switch (p.toLowerCase()) {
                case "ctrl": case "control": r.Ctrl = true; break;
                case "alt": case "option": r.Alt = true; break;
                case "shift": r.Shift = true; break;
                case "meta": case "cmd": case "win": r.Meta = true; break;
                case "mod": if (isMac) r.Meta = true; else r.Ctrl = true; break;
            }
        }
        return r;
    }

    /**
     * The identity of a parsed combination (the same keys, however written: "Mod+S" = "Ctrl+S" off Apple).
     * @param {{Key: string, Ctrl: boolean, Alt: boolean, Shift: boolean, Meta: boolean}} s The parsed combination.
     * @returns {string} The signature.
     */
    function comboSig(s) {
        return (s.Ctrl ? "C" : "") + (s.Alt ? "A" : "") + (s.Shift ? "S" : "") + (s.Meta ? "M" : "") + "+" + s.Key;
    }

    /**
     * True for a combination the browser or the system keeps (the page's keydown never sees it, or the browser acts first).
     * @param {{Key: string, Ctrl: boolean, Alt: boolean, Shift: boolean, Meta: boolean}} s The parsed combination.
     * @returns {boolean} True if reserved.
     */
    function reservedCombo(s) {
        const mod = isMac ? s.Meta : s.Ctrl;
        const k = s.Key;
        if (mod && !s.Alt && /^(w|t|n|q)$/.test(k))
            return true;
        if (s.Ctrl && (k === "Tab" || k === "PageUp" || k === "PageDown"))
            return true;
        if (s.Alt && k === "F4")
            return true;
        return false;
    }

    /**
     * True while a shortcut's scope element is part of what the user sees: connected, rendered (not display none, not in a hidden tab
     * panel) and not inert.
     * @param {Element} e The scope element.
     * @returns {boolean} True when live.
     */
    function scopeLive(e) {
        return !!(e && e.isConnected && e.getClientRects().length && !e.closest("[hidden], [inert]"));
    }

    /** Global key handler of Sw.Keys (runs the newest matching shortcut; a scoped one only while its element is shown). */
    function onShortcutKey(ev) {
        if (ev.defaultPrevented || ev.isComposing)
            return;
        const t = ev.target;
        const editable = t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.tagName === "SELECT" || t.isContentEditable);
        const k = ev.key && ev.key.length === 1 ? ev.key.toLowerCase() : ev.key;
        for (let i = shortcuts.length - 1; i >= 0; --i) {
            const s = shortcuts[i];
            if (s.Key !== k || s.Ctrl !== ev.ctrlKey || s.Alt !== ev.altKey || s.Meta !== ev.metaKey)
                continue;
            //  Shift is implied by printable characters like "?" (don't require the user to write "Shift+?")
            if (s.Shift !== ev.shiftKey && !(k.length === 1 && !s.Shift && !/[a-z0-9]/.test(k)))
                continue;
            if (editable && !s.InInputs)
                continue;
            if (!Focus.Allowed(t) && !s.Global)
                continue;
            //  A scoped shortcut (Scope: its element) belongs to what is shown: a table in a hidden tab can't take "/" (Admin N15)
            if (s.Scope && !scopeLive(s.Scope)) {
                //  (FD P6: a scope element that was never shown makes a dead shortcut; say so when the key is used and nothing runs)
                if (!s.SeenLive && !shortcuts.some(x => x !== s && x.Sig === s.Sig && (!x.Scope || scopeLive(x.Scope))))
                    DevWarn('Sw.Keys.Add("' + s.Combo + '"' + (s.Title ? ", " + s.Title : "") + "): its Scope element " + (s.Scope.isConnected ? "is not shown (hidden, display none or inert)" : "is not in the document") + " and has never been shown since the shortcut was added, so the key does nothing. Pass the element that is on the screen (the table's E, the tab panel), or no Scope.", "keys-deadscope:" + s.Sig);
                continue;
            }
            s.SeenLive = true;
            ev.preventDefault();
            s.Fn(ev);
            return;
        }
    }

    /** The options of Sw.Keys.Add (DevOptions). */
    const KeysSchema = {
        Title: "string", InInputs: "boolean", Global: "boolean", Replace: "boolean",
        Scope: "element|component",
    };

    /** Keyboard shortcut registry (one global keydown listener, the newest registration wins). */
    const Keys = {
        /**
         * Register a keyboard shortcut.
         * @param {string} combo Combination, ex: "Ctrl+S", "Mod+K" (Cmd on Apple, else Ctrl), "Shift+F10", "?", "Escape".
         * @param {function(KeyboardEvent):void} fn Handler (the default action is prevented).
         * @param {object} [o] Options: { Title (description for Sw.Keys.List / ShowHelp), InInputs (also fire in text inputs),
         * Global (also fire while a modal popup traps the focus), Replace (true: this one replaces an earlier registration of the same
         * combination on purpose; no developer warning), Scope (an element or component: the shortcut runs only while it is connected
         * and shown, ex: a table in a tab panel; several scoped registrations of one combination are normal, the newest shown one runs) }.
         * A second unscoped registration of a combination hides the first one (the newest wins, List() shows only it) and gives a developer
         * warning unless Replace; a combination the browser keeps for itself (Mod+W / T / N, Ctrl+Tab, Alt+F4 ...) never reaches the
         * page: a developer warning names it.
         * @returns {function():void} Removes the shortcut.
         */
        Add(combo, fn, o) {
            optCheck("Sw.Keys.Add", o);
            o = o || {};
            if (!shortcuts.length)
                document.addEventListener("keydown", onShortcutKey);
            DevOptions('Sw.Keys.Add("' + combo + '")', o, KeysSchema);
            if (typeof fn !== "function")
                DevWarn('Sw.Keys.Add("' + combo + '"): the handler is not a function; the key does nothing.', "keys-nofn:" + combo);
            const scope = o.Scope && o.Scope.E instanceof Element ? o.Scope.E : (o.Scope instanceof Element ? o.Scope : null);
            const s = Object.assign(parseCombo(combo), { Combo: combo, Fn: fn, Title: o.Title || "", InInputs: !!o.InInputs, Global: !!o.Global, Scope: scope });
            if (scope && scopeLive(scope))
                s.SeenLive = true;
            //  (watched for a few seconds: a scope shown a moment later, ex: after a load, is fine)
            if (scope && !s.SeenLive)
                for (const ms of [500, 3000])
                    setTimeout(() => {
                        if (scopeLive(scope))
                            s.SeenLive = true;
                    }, ms);
            s.Sig = comboSig(s);
            //  (scoped registrations of one combination take turns by what is shown: no duplicate)
            const was = shortcuts.find(x => x.Sig === s.Sig && !(scope && x.Scope && x.Scope !== scope));
            if (was && !o.Replace)
                DevWarn('Sw.Keys.Add("' + combo + '"' + (s.Title ? ", " + s.Title : "") + '): the combination is registered already' + (was.Title ? ' ("' + was.Title + '")' : "") + "; only the newest one runs. Remove the old one (Add returns the remover) or pass { Replace: true } when that is intended.", "keys-dup:" + s.Sig);
            if (reservedCombo(s))
                DevWarn('Sw.Keys.Add("' + combo + '"): the browser keeps this combination for itself (tab, window or browser commands); the page never gets it. Choose another one.', "keys-reserved:" + s.Sig);
            shortcuts.push(s);
            return () => {
                const i = shortcuts.indexOf(s);
                if (i >= 0)
                    shortcuts.splice(i, 1);
                if (!shortcuts.length)
                    document.removeEventListener("keydown", onShortcutKey);
            };
        },
        /**
         * List the registered shortcuts that have a title.
         * @returns {Array<{Combo: string, Title: string}>} The shortcuts (newest last).
         */
        List() {
            //  (a combination registered twice: only the newest runs, so only it is listed; a scoped one only while it is shown)
            const live = shortcuts.filter(s => !s.Scope || scopeLive(s.Scope));
            return live.filter((s, i) => s.Title && !live.some((x, j) => j > i && x.Sig === s.Sig && x.InInputs === s.InInputs)).map(s => ({ Combo: Keys.Format(s.Combo), Title: s.Title }));
        },
        /**
         * Format a combination for display ("Mod" becomes "Ctrl" or "⌘").
         * @param {string} combo The combination.
         * @returns {string} Display text.
         */
        Format(combo) {
            return combo.replace(/\bMod\b/g, isMac ? "⌘" : "Ctrl");
        },
        /** True on Apple platforms. */
        IsMac: isMac,
    };

    // ------------------------------------------------------------------ pointer drag

    /**
     * Pointer drag helper (mouse, touch and pen): calls back on start (after a small movement threshold), move and end,
     * with pointer capture so the drag continues outside the element.
     * @param {HTMLElement} e The element to drag on.
     * @param {object} o Options: { OnStart(ev, state) (return false to ignore this pointer), OnMove(ev, state), OnEnd(ev, state, cancelled),
     * OnTap(ev, state) (released without moving past the threshold), Threshold (px, default 4; 0 starts at once), Button (default 0),
     * Touch (false: ignore touch, leave it to scrolling) }. state = { X0, Y0, X, Y, DX, DY, Started, Data (yours) }.
     * @returns {function():void} Removes the handlers.
     */
    function Drag(e, o) {
        optCheck("Sw.Drag", o);
        o = o || {};
        const thr = o.Threshold ?? 4;
        let st = null;
        /** Listen on the window while a pointer is down: moves before the drag starts may leave a thin handle (a splitter) at once. */
        const listen = on => {
            const f = on ? window.addEventListener : window.removeEventListener;
            f.call(window, "pointermove", move, true);
            f.call(window, "pointerup", up, true);
            f.call(window, "pointercancel", cancel, true);
            f.call(window, "blur", blur);
        };
        /** Pointer down: remember the pointer (starts at once with a zero threshold). */
        const down = ev => {
            if (st || ev.button !== (o.Button ?? 0))
                return;
            if (o.Touch === false && ev.pointerType === "touch")
                return;
            const s = { Id: ev.pointerId, X0: ev.clientX, Y0: ev.clientY, X: ev.clientX, Y: ev.clientY, DX: 0, DY: 0, Started: false, Data: null };
            st = s;
            listen(true);
            if (thr <= 0)
                start(ev);
        };
        /** Start the drag (OnStart may cancel it), capture the pointer. */
        const start = ev => {
            if (o.OnStart && o.OnStart(ev, st) === false) {
                st = null;
                listen(false);
                return;
            }
            st.Started = true;
            try {
                e.setPointerCapture(st.Id);
            }
            catch {
            }
            e.classList.add("sw-dragging");
        };
        /** Pointer move: update the state, start after the threshold, call OnMove. */
        const move = ev => {
            if (!st || ev.pointerId !== st.Id)
                return;
            //  A mouse without a pressed button: the release was missed (a lost capture, a remote desktop that drops events), end
            //  now instead of dragging with no button held
            if (ev.pointerType === "mouse" && ev.buttons === 0) {
                end(ev, !st.Started); // not started: cancelled (not a tap)
                return;
            }
            st.X = ev.clientX;
            st.Y = ev.clientY;
            st.DX = st.X - st.X0;
            st.DY = st.Y - st.Y0;
            if (!st.Started) {
                if (Math.hypot(st.DX, st.DY) < thr)
                    return;
                start(ev);
                if (!st)
                    return;
            }
            ev.preventDefault();
            o.OnMove && o.OnMove(ev, st);
        };
        /** Pointer up / cancel: end the drag (OnEnd) or report a tap (OnTap). */
        const end = (ev, cancelled) => {
            if (!st || ev.pointerId !== st.Id)
                return;
            const s = st;
            st = null;
            listen(false);
            e.classList.remove("sw-dragging");
            if (s.Started) {
                try {
                    if (e.hasPointerCapture && e.hasPointerCapture(s.Id))
                        e.releasePointerCapture(s.Id);
                }
                catch {
                }
                o.OnEnd && o.OnEnd(ev, s, cancelled);
            }
            else if (!cancelled)
                o.OnTap && o.OnTap(ev, s);
        };
        /** Pointer up handler. */
        const up = ev => end(ev, false);
        /** Pointer cancel handler. */
        const cancel = ev => end(ev, true);
        /** The capture was lost (the browser or the system took the pointer): the drag ends where it is (no release follows). */
        const lost = ev => {
            if (st && st.Started && ev.pointerId === st.Id)
                end(ev, false);
        };
        /** The window lost the focus (alt+tab, a remote desktop switch) during a drag: end it, the release would go elsewhere. */
        const blur = () => {
            if (st)
                end({ pointerId: st.Id, clientX: st.X, clientY: st.Y }, !st.Started); // not started: cancelled (not a tap)
        };
        e.addEventListener("pointerdown", down);
        e.addEventListener("lostpointercapture", lost);
        return () => {
            //  A drag in progress ends (cancelled: OnEnd with cancelled = true), so no capture, class or window listener stays behind
            if (st)
                end({ pointerId: st.Id, clientX: st.X, clientY: st.Y }, true);
            e.removeEventListener("pointerdown", down);
            e.removeEventListener("lostpointercapture", lost);
            listen(false);
        };
    }

    // ------------------------------------------------------------------ clipboard

    /**
     * Copy text with the legacy execCommand fallback (works in insecure http contexts where navigator.clipboard is missing).
     * @param {string} text The text.
     * @returns {boolean} True on success.
     */
    function legacyCopy(text) {
        const ta = el("textarea", { value: text, attrs: { readonly: "", "aria-hidden": "true" }, style: { position: "fixed", left: "-9999px", top: "0", opacity: "0" } });
        const prev = document.activeElement;
        //  Inside the open modal dialog (the one with the focus, else the top one): outside it the dialog keeps the focus and the
        //  selection, and nothing was copied (ex: the seed button of the logo designer's theme preview over plain http)
        const modals = Array.from(document.querySelectorAll("[aria-modal='true']")).filter(m => m.isConnected && m.getClientRects().length > 0);
        const host = (prev && prev.closest && prev.closest("[aria-modal='true']")) || modals[modals.length - 1] || document.body;
        host.append(ta);
        try {
            ta.focus({ preventScroll: true });
        }
        catch {
        }
        ta.select();
        let ok = false;
        try {
            ok = document.execCommand("copy");
        }
        catch {
        }
        ta.remove();
        if (prev && prev !== document.body)
            Focus.Set(prev);
        return ok;
    }

    /**
     * The copy button whose click runs now (a Sw.Button with a copy icon, or the Copy option): Sw.Clipboard.Copy called from its OnClick
     * confirms on that button (UX guide INP-31).
     * @returns {HTMLElement|null} The button element.
     */
    function currentCopyButton() {
        const b = Sw.Button && Sw.Button.Current;
        if (!b || !b.E || !b.E.isConnected)
            return null;
        return b.O && (b.O.Copy || /copy/i.test(b.IconName || "")) ? b.E : null;
    }

    /** Clipboard helpers (copy with an in place confirmation or a toast, copy actions on values). */
    const Clipboard = {
        /**
         * Copy text to the clipboard (falls back to execCommand when the async clipboard api is missing or denied, ex: plain http).
         * Confirmed in place when it comes from a copy button (UX guide INP-31): the button's icon becomes a check mark for 1.5 s, its tool
         * tip says "Copied" and the copy is announced (Sw.Clipboard.Confirm), no toast. The button is the Button option, or found by itself
         * when Copy runs (synchronously) in the OnClick of a Sw.Button with a copy icon. Without a button an info toast names the value.
         * A failure is always a fail toast.
         * @param {*} value The value (converted to a string).
         * @param {object|boolean} [o] Options: { Toast (false: no toast; true: a toast also after an in place confirmation), Generic (the
         * message doesn't quote the text: long values), Secret (never quote it; a value that looks secret, Clipboard.LooksSecret, is never
         * quoted either unless Show: true), What (what was copied, for the message: "API key" -> "API key copied"),
         * Button (the copy button element or component to confirm on; null: none) }. A boolean is the Toast option.
         * @returns {Promise<boolean>} True on success.
         */
        async Copy(value, o) {
            optCheck("Sw.Clipboard.Copy", o);
            if (typeof o === "boolean")
                o = { Toast: o };
            o = o || {};
            //  Found before the first await: Sw.Button.Current is set only while OnClick runs
            const btn = o.Button !== undefined ? (o.Button && o.Button.E ? o.Button.E : o.Button) : currentCopyButton();
            const text = value === null || value === undefined ? "" : String(value);
            let ok = false, err = null;
            try {
                if (navigator.clipboard && window.isSecureContext !== false) {
                    await navigator.clipboard.writeText(text);
                    ok = true;
                }
            }
            catch (e) {
                err = e;
            }
            if (!ok)
                ok = legacyCopy(text);
            const msg = Clipboard.Message(text, o);
            if (ok && btn)
                Clipboard.Confirm(btn, msg);
            if (o.Toast !== false && Sw.Toast && (!ok || !btn || o.Toast === true)) {
                if (ok)
                    Sw.Info(msg, null, true);
                else
                    Sw.Fail(_T("Failed to copy the text to the clipboard. {0}", err ? err.message : "", "Message displayed when the web page failed to copy some text to the clipboard. {0} is replaced with the java script exception text"), null, true);
            }
            return ok;
        },
        /**
         * Read text from the clipboard.
         * @returns {Promise<string|null>} The text, or null if unavailable / denied.
         */
        async Read() {
            try {
                return await navigator.clipboard.readText();
            }
            catch {
                return null;
            }
        },
        /**
         * The message of a copy ('Copied "abc" to the clipboard'; "API key copied" with What; a generic one with Generic).
         * @param {string} text The copied text.
         * @param {{Generic: boolean, What: string}} [o] Options (see Copy).
         * @returns {string} The message.
         */
        Message(text, o) {
            optCheck("Sw.Clipboard.Message", o);
            o = o || {};
            if (o.What)
                return _T("{0} copied", o.What, "Message after a copy to the clipboard (button tool tip, announcement, toast). {0} names what was copied, ex: 'API key', 'The url of MailService'");
            //  Never the value when it is (or looks like) a secret (UX guide SEC-1): a screen reader would speak it, a toast would show it
            if (o.Generic || o.Secret || (!o.Show && Clipboard.LooksSecret(text)))
                return _TF("Copied text to the clipboard", "Message displayed when some text is copied to the clipboard.");
            const shown = text.length > 80 ? text.substring(0, 77) + "..." : text;
            return _T('Copied "{0}" to the clipboard', shown, "Message displayed when some text is copied to the clipboard.");
        },
        /**
         * True for a text that looks like a secret (an api key "name:key", a token, a long random string, a password field's value): its
         * copy message never quotes it (SEC-1). Pass Show: true to Copy to quote it anyway, Secret: true to never quote.
         * @param {string} text The text.
         * @returns {boolean} True if it looks secret.
         */
        LooksSecret(text) {
            const s = String(text || "").trim();
            if (!s || /\s/.test(s))
                return false;
            const ae = document.activeElement;
            if (ae && ae.tagName === "INPUT" && ae.type === "password")
                return true;
            //  "name:key" api keys, bearer / jwt tokens, long base32 / base64 / hex runs (mixed letters and digits, 20+ characters)
            if (/^[\w.-]{1,64}:[A-Za-z0-9+/=_-]{16,}$/.test(s) || /^eyJ[\w-]+\.[\w-]+\./.test(s))
                return true;
            return s.length >= 20 && /^[A-Za-z0-9+/=_-]+$/.test(s) && /\d/.test(s) && /[a-z]/i.test(s) && !/^\d+$/.test(s) && !/^[0-9a-f]{8}-/.test(s);
        },
        /**
         * Confirm a copy in place on its button (UX guide INP-31): a check mark instead of the icon for 1.5 s (the width stays), the tool
         * tip "Copied", class sw-copied, and the message announced (Sw.Page.Announce). Sw.Clipboard.Copy calls it for copy buttons.
         * @param {HTMLElement|{E: HTMLElement}} target The button (element or Sw.Button).
         * @param {string} [msg] The announcement (default "Copied").
         */
        Confirm(target, msg) {
            const e = target && target.E ? target.E : target;
            if (!e)
                return;
            const b = e.SwButton || null;
            const copied = _TF("Copied", "Tool tip of a copy button for 1.5 s after it copied its value to the clipboard");
            if (!e.SwCopiedTimer) {
                const st = { Title: b ? b.Title : e.getAttribute("title"), Icon: b ? b.IconName : null, Width: e.style.minWidth };
                e.SwCopiedState = st;
                e.style.minWidth = e.getBoundingClientRect().width + "px";
                if (b) {
                    if (b.IconName)
                        b.SetIcon("check");
                    b.SetTitle(copied);
                }
                else
                    e.title = copied;
                e.classList.add("sw-copied");
            }
            clearTimeout(e.SwCopiedTimer);
            e.SwCopiedTimer = setTimeout(() => {
                const st = e.SwCopiedState || {};
                e.SwCopiedTimer = 0;
                e.SwCopiedState = null;
                e.classList.remove("sw-copied");
                if (b) {
                    if (st.Icon)
                        b.SetIcon(st.Icon);
                    b.SetTitle(st.Title);
                }
                else if (st.Title)
                    e.title = st.Title;
                else
                    e.removeAttribute("title");
                e.style.minWidth = st.Width || "";
            }, 1500);
            if (Sw.Page && Sw.Page.Announce)
                Sw.Page.Announce(msg || copied);
        },
        /**
         * Give a value a copy action, as the old ValueFormat.copyOnClick (UX guide INP-31):
         *  - on a value (any element but a button): a plain left click or tap copies the value (a toast names it), as the original; a
         *    click with a modifier, a click while text is selected in it and a click on a link / control inside it don't. Also copied with Ctrl+C while the element has the focus and no text is selected (a toast names it), and from its menu (a pure
         *    right click, a long press on touch, the context menu key, Shift+F10): "Copy value", "Select text". With text selected the
         *    browser's menu and copy work as usual; a right click with a modifier shows the browser's menu (INP-3).
         *  - on a button (a button element or role button): a click copies and confirms on the button (Sw.Clipboard.Confirm).
         * Adds class sw-copyable. Replaces a previous copy action of the element (Sw.Clipboard.RemoveCopy removes it).
         * @param {HTMLElement} e The element.
         * @param {*} [value] The value to copy, null / undefined = the element's current text.
         * @param {object} [o] Options: { Keyboard (the value is a Tab stop, tabindex 0, and Enter / Space copy it, as the original; else tabindex -1: a click focuses it), AddTitle
         * (append the hint to the element's title), What (what is copied, for the messages), Generic (don't quote the value: secrets) }.
         * @returns {string} The (translated) hint text for the tool tip.
         */
        CopyOnClick(e, value, o) {
            optCheck("Sw.Clipboard.CopyOnClick", o);
            o = o || {};
            const v = value === null || value === undefined ? null : String(value);
            const get = () => v === null ? e.textContent : v;
            const mo = { What: o.What, Generic: o.Generic };
            Clipboard.RemoveCopy(e);
            e.classList.add("sw-copyable");
            const isButton = e.tagName === "BUTTON" || e.getAttribute("role") === "button";
            const shown = v !== null ? (o.Generic ? null : (v.length > 200 ? v.substring(0, 197) + "..." : v)) : null;
            if (isButton) {
                e.SwCopy = ev => {
                    if (BadClick(ev) || Disabled.Is(e))
                        return;
                    Clipboard.Copy(get(), Object.assign({ Button: e }, mo));
                };
                e.addEventListener("click", e.SwCopy);
                return shown !== null
                    ? _T('Copy "{0}" to the clipboard', shown, "Tool tip of a copy button. {0} is the value it copies")
                    : _TF("Copy text to the clipboard", "Tool tip to indicate that some text can be copied to the clipboard by clicking on an element");
            }
            if (!e.hasAttribute("tabindex")) {
                e.tabIndex = o.Keyboard ? 0 : -1;
                e.SwCopyTab = true;
            }
            //  Keyboard: Enter / Space on the focused value copy it, as the original copyOnClick(…, true) (its keyboardClick clicked the
            //  value on the key up; S5-A-1). Space doesn't scroll the page.
            if (o.Keyboard) {
                e.SwCopyKeys = ev => {
                    if (ev.target !== e || (ev.key !== "Enter" && ev.key !== " ") || ev.shiftKey || ev.altKey || ev.ctrlKey || ev.metaKey)
                        return;
                    ev.preventDefault();
                    if (ev.type === "keyup" && !Disabled.Is(e))
                        Clipboard.Copy(get(), Object.assign({ Button: null }, mo));
                };
                e.addEventListener("keydown", e.SwCopyKeys);
                e.addEventListener("keyup", e.SwCopyKeys);
            }
            //  A plain click copies (the original copyOnClick)
            e.SwCopy = ev => {
                //  A click on a link / control inside the value is theirs, untouched: checked before BadClick (which cancels the default
                //  action and stops the event), so an inner link navigates, a Ctrl / Shift / middle click keeps its new tab / window and
                //  the parents still see the click
                const t = ev.target;
                const inner = t && t !== e && t.closest ? t.closest("a[href], button, input, select, textarea, [role='button'], [role='link']") : null;
                if (inner && inner !== e && e.contains(inner))
                    return;
                if (BadClick(ev) || ev.button > 0 || Disabled.Is(e) || HasSelectionIn(e))
                    return;
                Clipboard.Copy(get(), Object.assign({ Button: null }, mo));
            };
            e.addEventListener("click", e.SwCopy);
            //  Ctrl+C on the focused value (no text selected): the value itself, without labels or formatting
            e.SwCopyKey = ev => {
                if (HasSelectionIn(e))
                    return;
                const text = get();
                ev.preventDefault();
                try {
                    if (ev.clipboardData) {
                        ev.clipboardData.setData("text/plain", text);
                        if (Sw.Info)
                            Sw.Info(Clipboard.Message(text, mo), null, true);
                        return;
                    }
                }
                catch {
                }
                Clipboard.Copy(text, Object.assign({ Button: null }, mo));
            };
            e.addEventListener("copy", e.SwCopyKey);
            /** The value's menu (falls back to a plain copy when the menus are not loaded). */
            const menu = ev => {
                if (!Sw.Menu) {
                    Clipboard.Copy(get(), Object.assign({ Button: null }, mo));
                    return;
                }
                const at = ev && ev.type === "contextmenu" && (ev.clientX || ev.clientY) ? { X: ev.clientX, Y: ev.clientY } : e;
                Sw.Menu.Show(at, [
                    { Text: _TF("Copy value", "Menu item that copies a value (an id, a time stamp, a text) to the clipboard"), Icon: "copy", OnClick: () => Clipboard.Copy(get(), Object.assign({ Button: null }, mo)) },
                    {
                        Text: _TF("Select text", "Menu item that selects the text of a value, ex: to read it aloud or copy a part of it"), OnClick: () => {
                            try {
                                const r = document.createRange();
                                r.selectNodeContents(e);
                                const sel = window.getSelection();
                                sel.removeAllRanges();
                                sel.addRange(r);
                            }
                            catch {
                            }
                        },
                    },
                ]);
            };
            e.SwCopyMenu = LongPress(e, menu, { Skip: () => HasSelectionIn(e) });
            const title = shown !== null
                ? _T('Click to copy "{0}" to the clipboard', shown, 'Tool tip to indicate that some known text can be copied to the clipboard by clicking on an element')
                : _T('Click to copy text to the clipboard', 'Tool tip to indicate that some text can be copied to the clipboard by clicking on an element');
            if (o.AddTitle)
                e.title = e.title ? (e.title + "\n\n" + title) : title;
            return title;
        },
        /**
         * Remove the copy action set by CopyOnClick (handlers, class, the tabindex it added).
         * @param {HTMLElement} e The element.
         */
        RemoveCopy(e) {
            if (e.SwCopy) {
                e.removeEventListener("click", e.SwCopy);
                e.SwCopy = null;
            }
            if (e.SwCopyKey) {
                e.removeEventListener("copy", e.SwCopyKey);
                e.SwCopyKey = null;
            }
            if (e.SwCopyKeys) {
                e.removeEventListener("keydown", e.SwCopyKeys);
                e.removeEventListener("keyup", e.SwCopyKeys);
                e.SwCopyKeys = null;
            }
            if (e.SwCopyMenu) {
                e.SwCopyMenu();
                e.SwCopyMenu = null;
            }
            if (e.SwCopyTab) {
                e.removeAttribute("tabindex");
                e.SwCopyTab = false;
            }
            e.oncopy = null;
            e.classList.remove("sw-copyable");
        },
        /**
         * The address a link copies (Sw.Clipboard.LinkItem, AltClickLinks): the absolute url of an `<a href>`, null for none, a script
         * or an in page anchor.
         * @param {?Element} a The link (or an element inside it).
         * @returns {?string} The url.
         */
        LinkUrl(a) {
            a = a && a.closest ? a.closest("a[href]") : null;
            if (!a)
                return null;
            const href = a.getAttribute("href") || "";
            if (!href || href.startsWith("#") || /^\s*javascript:/i.test(href))
                return null;
            return a.href || href;
        },
        /**
         * A "Copy link" menu item for a link (data table cell menus): copies its address with a toast that names it. Null when the
         * element is no copyable link (LinkUrl).
         * @param {?Element} a The link (or an element inside it).
         * @param {string} [name] The link's name when a menu lists several links ("Copy link: Details"); default: no name.
         * @returns {?object} The menu item ({ Text, Icon, OnClick }).
         */
        LinkItem(a, name) {
            const url = Clipboard.LinkUrl(a);
            if (!url)
                return null;
            return {
                Text: name
                    ? _T("Copy link: {0}", name, "Menu item that copies the address of one of several links (ex: in a data table cell) to the clipboard, {0} names the link")
                    : _TF("Copy link", "Menu item that copies the address of a link (ex: in a data table cell) to the clipboard"),
                Title: url,
                Icon: "link",
                OnClick: () => Clipboard.Copy(url, { Button: null }),
            };
        },
        /**
         * A left click with a modifier (Shift, Ctrl, Alt or Meta) on a link inside `root` copies its address (a toast names it), as the
         * original tables and listing (onLink: a click that is not a pure click copies); a plain click opens it. Used by the data tables
         * and the explore listing.
         * @param {HTMLElement} root The container of the links.
         * @returns {function(): void} Removes the handler.
         */
        AltClickLinks(root) {
            const on = ev => {
                if (!(ev.altKey || ev.ctrlKey || ev.metaKey || ev.shiftKey) || ev.button !== 0 || !ev.target || !ev.target.closest)
                    return;
                const a = ev.target.closest("a[href]");
                const url = a && root.contains(a) ? Clipboard.LinkUrl(a) : null;
                if (!url)
                    return;
                ev.preventDefault();
                ev.stopPropagation();
                Clipboard.Copy(url, { Button: null });
            };
            root.addEventListener("click", on, true);
            return () => root.removeEventListener("click", on, true);
        },
    };

    /**
     * Test if the user has selected text inside an element (so a click is a selection, not an action).
     * @param {Element} e The element.
     * @returns {boolean} True if a non empty selection intersects the element.
     */
    function HasSelectionIn(e) {
        try {
            const sel = window.getSelection();
            if (!sel || sel.isCollapsed || !sel.rangeCount)
                return false;
            for (let i = 0; i < sel.rangeCount; ++i)
                if (sel.getRangeAt(i).intersectsNode(e))
                    return true;
        }
        catch {
        }
        return false;
    }

    // ------------------------------------------------------------------ color math

    /**
     * HSV to RGB.
     * @param {number} h Hue 0..1.
     * @param {number} s Saturation 0..1.
     * @param {number} v Value 0..1.
     * @returns {number[]} [r, g, b], 0..1.
     */
    function hsvToRgb(h, s, v) {
        /** One HSV channel function (n = 5, 3, 1 for r, g, b). */
        const f = n => {
            const k = (n + h * 6) % 6;
            return v - v * s * Math.max(0, Math.min(k, 4 - k, 1));
        };
        return [f(5), f(3), f(1)];
    }

    /**
     * RGB to HSV.
     * @param {number} r Red 0..1.
     * @param {number} g Green 0..1.
     * @param {number} b Blue 0..1.
     * @returns {number[]} [h, s, v], 0..1.
     */
    function rgbToHsv(r, g, b) {
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        const d = max - min;
        let h = 0;
        if (d > 0) {
            if (max === r)
                h = ((g - b) / d) % 6;
            else if (max === g)
                h = (b - r) / d + 2;
            else
                h = (r - g) / d + 4;
            h /= 6;
            if (h < 0)
                h += 1;
        }
        return [h, max > 0 ? d / max : 0, max];
    }

    /**
     * RGB (0..1 floats) to a #rrggbb hex string (values are clamped).
     * @param {number[]} rgb [r, g, b, ...].
     * @returns {string} "#rrggbb".
     */
    const toHex = rgb => "#" + rgb.slice(0, 3).map(x => Math.round(clamp(x, 0, 1) * 255).toString(16).padStart(2, "0")).join("");

    /**
     * Parse a #rrggbb or #rgb hex string.
     * @param {string} s The text.
     * @returns {number[]|null} [r, g, b] 0..1 floats, or null if invalid.
     */
    function fromHex(s) {
        let t = (s || "").trim().replace(/^#/, "");
        if (/^[0-9a-f]{3}$/i.test(t))
            t = t.split("").map(c => c + c).join("");
        if (!/^[0-9a-f]{6}$/i.test(t))
            return null;
        const n = parseInt(t, 16);
        return [(n >> 16 & 255) / 255, (n >> 8 & 255) / 255, (n & 255) / 255];
    }

    /** @type {HTMLElement|null} Hidden element used to resolve css colors. */
    let colorProbe = null;

    /**
     * Resolve any css color (keyword, hex, rgb(), hsl(), var(...)) to [r, g, b, a] (0..255, alpha 0..1) using the computed style
     * (the old GetRgbColor, which also parsed the alpha as an integer: a fractional alpha became 0).
     * @param {string} color A css color.
     * @returns {number[]|null} [r, g, b, a] or null if it can't be resolved.
     */
    function parseCss(color) {
        if (!colorProbe) {
            colorProbe = el("div", { attrs: { "aria-hidden": "true" }, style: { display: "none" } });
        }
        colorProbe.style.color = "";
        colorProbe.style.color = color;
        if (!colorProbe.style.color)
            return null;
        document.body.append(colorProbe);
        const c = getComputedStyle(colorProbe).color;
        colorProbe.remove();
        const m = /rgba?\(([^)]+)\)/.exec(c);
        if (!m)
            return null;
        const p = m[1].split(/[\s,/]+/).filter(x => x).map(x => parseFloat(x));
        return [p[0] || 0, p[1] || 0, p[2] || 0, p.length > 3 ? p[3] : 1];
    }

    /** Color math helpers. */
    const Color = {
        HsvToRgb: hsvToRgb,
        RgbToHsv: rgbToHsv,
        ToHex: toHex,
        FromHex: fromHex,
        ParseCss: parseCss,
        /**
         * Perceived brightness (luma) of 0..255 rgb values.
         * @param {number} r Red.
         * @param {number} g Green.
         * @param {number} b Blue.
         * @returns {number} 0..255.
         */
        Luma: (r, g, b) => 0.2989 * r + 0.587 * g + 0.114 * b,
    };

    // ------------------------------------------------------------------ timing fallbacks

    /** Core helpers (Sw.Util) when present. */
    const U = Sw.Util || {};

    /**
     * Debounce: returns a function that calls fn(...args) ms after the last call (uses Sw.Util.Debounce when the core has it).
     * @param {function} fn The function.
     * @param {number} ms Delay in ms.
     * @returns {function} The debounced function.
     */
    const Debounce = U.Debounce || ((fn, ms) => {
        let timer = 0;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), ms);
        };
    });

    /**
     * Throttle: calls fn at most once per ms, the last call is always delivered (uses Sw.Util.Throttle when the core has it).
     * @param {function} fn The function.
     * @param {number} ms Interval in ms.
     * @returns {function} The throttled function.
     */
    const Throttle = U.Throttle || ((fn, ms) => {
        let last = 0, timer = 0, pending = null;
        return (...args) => {
            pending = args;
            const now = performance.now();
            const run = () => {
                last = performance.now();
                timer = 0;
                const a = pending;
                pending = null;
                fn(...a);
            };
            if (now - last >= ms) {
                clearTimeout(timer);
                run();
            }
            else if (!timer)
                timer = setTimeout(run, ms - (now - last));
        };
    });

    // ------------------------------------------------------------------ smooth src loading (images / iframes)

    /** @type {Map<string, {Name: string, Count: number, StartTime: number, Delay: number, First: HTMLElement, Index: number, Delayed: HTMLElement[], Items: number}>} Fade channels. */
    const fadeChannels = new Map();

    /**
     * Read a numeric data attribute.
     * @param {Element} e The element.
     * @param {string} name Attribute name.
     * @param {number} def Default value.
     * @returns {number} The value (default when missing, negative or malformed).
     */
    function numAttr(e, name, def) {
        const v = parseFloat(e.getAttribute(name));
        return v >= 0 ? v : def;
    }

    /**
     * Detach an element from its fade channel (the channel is removed when it has no items left).
     * @param {HTMLElement} e The element.
     */
    function leaveChannel(e) {
        const ch = e.SwFade && e.SwFade.Channel;
        if (!ch)
            return;
        e.SwFade.Channel = null;
        if (--ch.Items <= 0)
            fadeChannels.delete(ch.Name);
    }

    /**
     * The fade duration of Sw.FadeSrc from the theme: --sw-dur-2 (ms; "160ms" or "0.16s"), 150 when the token is missing (a page
     * without the ui css).
     * @returns {number} Milliseconds.
     */
    function themeFadeMs() {
        try {
            const v = getComputedStyle(document.documentElement).getPropertyValue("--sw-dur-2").trim();
            const m = /^(-?[\d.]+)(ms|s)$/.exec(v);
            if (m)
                return Math.max(0, parseFloat(m[1]) * (m[2] === "s" ? 1000 : 1));
        }
        catch {
        }
        return 150;
    }

    /**
     * Reveal a faded element (after its delay / channel offset), animating the opacity with a smootherstep curve.
     * @param {HTMLElement} e The element (set up by Sw.FadeSrc).
     */
    function fadeReveal(e) {
        const f = e.SwFade;
        if (!f)
            return;
        f.Stop();
        const s = f.Style;
        const count = ++f.Count;
        let d = f.Delay;
        const ch = f.Channel;
        //  The fade duration: the option / data-sw-fadein, else the theme's --sw-dur-2 (UX guide LAY-3, MOT-2; 0 / reduced motion: at once)
        const fade = f.FadeIn >= 0 ? f.FadeIn : themeFadeMs();
        if (fade > 0 && ch) {
            if (e === ch.First) {
                ch.StartTime = performance.now();
                ch.Count = count;
                const delayed = ch.Delayed;
                ch.Delayed = [];
                delayed.forEach(x => fadeReveal(x));
            }
            else {
                if (count !== ch.Count) {
                    //  Wait until the first element of the channel has started
                    f.Count = count - 1;
                    ch.Delayed.push(e);
                    return;
                }
                d += ch.Delay * f.Index - (performance.now() - ch.StartTime);
            }
            leaveChannel(e);
        }
        if (!(fade > 1)) {
            s.opacity = "";
            s.visibility = "";
            return;
        }
        d = Math.max(0, d);
        let start = -1;
        s.opacity = "0";
        s.visibility = "";
        /** Smootherstep easing 0..1. */
        const smooth = t => t * t * t * (t * (t * 6 - 15) + 10);
        /** Animation frame: fade the opacity in after the delay. */
        const anim = t => {
            if (start < 0)
                start = t + d;
            const p = (t - start) / fade;
            if (p >= 1) {
                s.opacity = "";
                f.Anim = 0;
                return;
            }
            if (p > 0)
                s.opacity = String(smooth(p));
            f.Anim = requestAnimationFrame(anim);
        };
        f.Anim = requestAnimationFrame(anim);
    }

    /**
     * Smooth (glitch free) loading of an img or iframe (the old fixElementWithSrc): the element is hidden while its src loads and then
     * faded in. Changing src later hides it again until the new source has loaded. Calling it again on the same element does nothing.
     * Options default to the element's data attributes: data-sw-fadein (fade ms, default the theme's --sw-dur-2, read when the element
     * is revealed: the theme's motion scale --sw-motion, reduced motion shows at once), data-sw-fadedelay (ms, default 0),
     * data-sw-fadechannel ("name,delayMs": elements of a channel are revealed in sequence), data-sw-fadeparent (animate the n:th parent instead),
     * data-sw-useevent (iframes: wait for the framed page's PageLoaded message instead of the load event).
     * Note: the inline opacity / visibility of the animated element are overridden, style a parent instead.
     * @param {HTMLImageElement|HTMLIFrameElement} e The element.
     * @param {object} [o] Options: { ShowError (images: show the broken image on errors), FadeIn, FadeDelay, Channel, Parent, UseEvent }.
     * @returns {HTMLElement} The element.
     */
    function FadeSrc(e, o) {
        optCheck("Sw.FadeSrc", o);
        if (!e || e.SwFade)
            return e;
        o = o || {};
        const isImg = e.tagName === "IMG";
        const isFrame = e.tagName === "IFRAME";
        if (isImg)
            e.setAttribute("draggable", "false");
        const useEvent = isFrame && (o.UseEvent ?? e.hasAttribute("data-sw-useevent"));
        let target = e;
        for (let i = o.Parent ?? numAttr(e, "data-sw-fadeparent", 0); i > 0 && target.parentElement; --i)
            target = target.parentElement;
        const f = {
            //  -1: the theme's --sw-dur-2 at reveal time (themeFadeMs)
            FadeIn: o.FadeIn ?? numAttr(e, "data-sw-fadein", -1),
            Delay: o.FadeDelay ?? numAttr(e, "data-sw-fadedelay", 0),
            ShowError: !!o.ShowError,
            Style: target.style,
            Count: 0,
            Channel: null,
            Index: 0,
            Anim: 0,
            Obs: null,
        };
        const chSpec = o.Channel ?? e.getAttribute("data-sw-fadechannel");
        if (chSpec) {
            const cp = String(chSpec).split(",");
            const name = cp[0].trim();
            const cd = cp.length > 1 ? parseFloat(cp[1]) : 100;
            if (cd > 0) {
                let ch = fadeChannels.get(name);
                if (!ch) {
                    ch = { Name: name, Count: 0, StartTime: 0, Delay: cd, First: e, Index: 0, Delayed: [], Items: 0 };
                    fadeChannels.set(name, ch);
                }
                f.Index = ch.Index++;
                ch.Items++;
                f.Channel = ch;
            }
        }
        e.SwFade = f;
        /** Load event: reveal the element. */
        const onLoad = () => fadeReveal(e);
        /** Error event: stop listening; show the broken image when ShowError. */
        const onError = () => {
            f.Stop();
            leaveChannel(e);
            if (f.ShowError) {
                f.Style.opacity = "";
                f.Style.visibility = "";
            }
        };
        /** Message from the framed page: PageLoaded (from this iframe only) reveals it. */
        const onMsg = ev => {
            const d = ev.data;
            if (d && d.Type === "PageLoaded" && ev.source === e.contentWindow)
                fadeReveal(e);
        };
        /** Start listening for load (or the PageLoaded message) and error. */
        const listen = () => {
            if (useEvent)
                window.addEventListener("message", onMsg);
            else
                e.addEventListener("load", onLoad);
            e.addEventListener("error", onError);
        };
        f.Stop = () => {
            if (f.Anim)
                cancelAnimationFrame(f.Anim);
            f.Anim = 0;
            window.removeEventListener("message", onMsg);
            e.removeEventListener("load", onLoad);
            e.removeEventListener("error", onError);
        };
        /** Hide the animated element. */
        const hide = () => {
            f.Style.visibility = "hidden";
            f.Style.opacity = "0";
        };
        try {
            f.Obs = new MutationObserver(() => {
                f.Stop();
                hide();
                listen();
            });
            f.Obs.observe(e, { attributes: true, attributeFilter: ["src"] });
        }
        catch {
        }
        if (e.getAttribute("src")) {
            let done = false;
            if (isImg && e.complete)
                done = e.naturalWidth > 0 || f.ShowError;
            else if (isFrame && !useEvent) {
                try {
                    done = e.contentWindow.document.readyState === "complete" && e.contentWindow.location.href !== "about:blank";
                }
                catch {
                    //  Cross origin frame: can't tell, wait for the load event
                }
            }
            if (done) {
                if (f.Delay > 0 || f.Channel) {
                    hide();
                    fadeReveal(e);
                }
                else {
                    f.Style.visibility = "";
                    f.Style.opacity = "";
                    leaveChannel(e);
                }
                return e;
            }
            listen();
        }
        hide();
        return e;
    }

    /**
     * Undo Sw.FadeSrc (the old cleanElementWithSrc): removes the listeners and the src observer, leaves the fade channel.
     * @param {HTMLElement} e The element.
     * @param {boolean} [show] Show the element (default: it stays hidden, as the old function did).
     * @returns {HTMLElement} The element.
     */
    function UnfadeSrc(e, show) {
        const f = e && e.SwFade;
        if (!f)
            return e;
        f.Stop();
        if (f.Obs)
            f.Obs.disconnect();
        leaveChannel(e);
        if (show) {
            f.Style.visibility = "";
            f.Style.opacity = "";
        }
        else {
            f.Style.visibility = "hidden";
            f.Style.opacity = "0";
        }
        e.SwFade = null;
        return e;
    }

    /**
     * Create an img that is hidden while loading and then faded in (the old createImg).
     * @param {object|boolean} [o] Options of Sw.FadeSrc, or (old signature) showError.
     * @param {number} [fadeIn] Old signature: the fade in duration.
     * @returns {HTMLImageElement} The image.
     */
    function CreateImg(o, fadeIn) {
        if (typeof o !== "object" || o === null)
            o = { ShowError: !!o, FadeIn: fadeIn };
        else
            optCheck("Sw.CreateImg", o);
        return FadeSrc(document.createElement("img"), o);
    }

    /**
     * Create an iframe that is hidden while loading and then faded in (the old createIFrame).
     * @param {object|number} [o] Options of Sw.FadeSrc, or (old signature) the fade in duration.
     * @returns {HTMLIFrameElement} The iframe.
     */
    function CreateIFrame(o) {
        if (typeof o !== "object" || o === null)
            o = { FadeIn: o };
        else
            optCheck("Sw.CreateIFrame", o);
        return FadeSrc(document.createElement("iframe"), o);
    }

    /**
     * Test if an element opts in to smooth src loading by its attributes (data-sw-fade, data-sw-fadein or data-sw-useevent).
     * @param {Node} e The node.
     * @returns {boolean} True if it opts in.
     */
    function wantsFade(e) {
        return !!(e && e.hasAttribute && (e.hasAttribute("data-sw-fade") || e.hasAttribute("data-sw-fadein") || e.hasAttribute("data-sw-useevent")));
    }

    /**
     * Apply fn to a node and to all its img / iframe descendants that opt in to smooth loading.
     * @param {Node} root The node.
     * @param {function(HTMLElement):void} fn Sw.FadeSrc or Sw.UnfadeSrc.
     */
    function eachFade(root, fn) {
        if (wantsFade(root))
            fn(root);
        if (root.querySelectorAll)
            root.querySelectorAll("img[data-sw-fade],img[data-sw-fadein],iframe[data-sw-fade],iframe[data-sw-fadein],iframe[data-sw-useevent]").forEach(x => fn(x));
    }

    /** Set up the attribute based smooth loading for the document (existing and added elements), runs once. */
    function autoFade() {
        if (autoFade.Done)
            return;
        autoFade.Done = true;
        eachFade(document, FadeSrc);
        new MutationObserver(changes => {
            for (const c of changes) {
                c.addedNodes.forEach(n => eachFade(n, FadeSrc));
                c.removedNodes.forEach(n => {
                    if (!n.isConnected)
                        eachFade(n, x => UnfadeSrc(x));
                });
            }
        }).observe(document, { subtree: true, childList: true });
    }
    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", autoFade, { once: true });
    else
        autoFade();

    /**
     * Set or reload an image source while it's hidden (opacity 0), with an optional fallback, cache flush and server message reload
     * (the old SetImageSource).
     * @param {HTMLImageElement} img The image.
     * @param {string} [src] The new url (default: the current src).
     * @param {object} [o] Options: { Fallback (url used if loading fails), FlushCache (download a fresh copy first),
     * OnLoaded(ok) (true if src loaded, false on error / fallback), ReloadOn (a server message type that reloads the image, ex: "UserImageChanged") }.
     * @returns {Promise<boolean>} Resolves when loaded (true) or failed (false).
     */
    async function SetImageSource(img, src, o) {
        optCheck("Sw.SetImageSource", o);
        o = o || {};
        const s = src ?? img.src;
        if (o.FlushCache)
            await flushUrl(s);
        const ims = img.style;
        let oldOp = ims.opacity;
        if (!(parseFloat(oldOp) > 0))
            oldOp = "";
        ims.opacity = "0";
        const ok = await new Promise(resolve => {
            let fails = 0;
            const done = r => {
                img.removeEventListener("load", load);
                img.removeEventListener("error", err);
                ims.opacity = oldOp;
                resolve(r);
            };
            const load = () => done(fails === 0);
            const err = () => {
                ++fails;
                if (fails === 1 && o.Fallback) {
                    img.src = o.Fallback;
                    return;
                }
                done(false);
            };
            img.addEventListener("load", load);
            img.addEventListener("error", err);
            img.src = s;
            if (img.complete && img.naturalWidth > 0)
                done(true);
        });
        if (o.OnLoaded)
            o.OnLoaded(ok);
        if (o.ReloadOn && !img.SwReloadOn) {
            img.SwReloadOn = o.ReloadOn;
            const handler = async () => {
                if (!img.isConnected) {
                    off();
                    return;
                }
                await SetImageSource(img, s, { Fallback: o.Fallback, FlushCache: true, OnLoaded: o.OnLoaded });
            };
            const off = subscribe(o.ReloadOn, handler);
        }
        return ok;
    }

    /**
     * Flush the browser cache for an url (uses the core's flush helper when present, else a reload fetch).
     * @param {string} url The url.
     * @returns {Promise<void>}
     */
    async function flushUrl(url) {
        const N = Sw.Net;
        try {
            if (N && N.FlushCache)
                await N.FlushCache(url);
            else
                await fetch(url, { cache: "reload", credentials: "same-origin" });
        }
        catch {
        }
    }

    /**
     * Subscribe to a server / InterOp message type using whatever the core offers.
     * @param {string} type The message type.
     * @param {function(object):void} fn Handler.
     * @returns {function():void} Unsubscribes.
     */
    function subscribe(type, fn) {
        const S = Sw.Session, I = Sw.InterOp;
        if (S && S.Subscribe) {
            const r = S.Subscribe(type, fn);
            return typeof r === "function" ? r : () => S.Unsubscribe && S.Unsubscribe(type, fn);
        }
        if (I && I.On) {
            const r = I.On(type, fn);
            return typeof r === "function" ? r : () => I.Off && I.Off(type, fn);
        }
        return () => { };
    }

    /** Keeps track of pending image loads and some basic stats, can wait for all images to load (or fail). */
    class PendingImages {
        /** Creates an empty tracker. */
        constructor() {
            /** @type {number} Number of images started. */
            this.Count = 0;
            /** @type {number} Number of images currently loading. */
            this.InProgress = 0;
            /** @type {number} Number of failed images. */
            this.Fails = 0;
            /** @type {Array<function():void>} Pending WaitAll resolvers. */
            this.Waiters = [];
        }

        /**
         * Track an image (call before setting its src, or pass the src here). Uses load / error listeners (handlers set by others are kept).
         * @param {HTMLImageElement} img The image.
         * @param {string} [src] Optional src to set.
         */
        Start(img, src) {
            const t = this;
            const fin = failed => {
                img.removeEventListener("load", ok);
                img.removeEventListener("error", bad);
                if (failed)
                    ++t.Fails;
                if (--t.InProgress <= 0) {
                    const w = t.Waiters;
                    t.Waiters = [];
                    w.forEach(x => x());
                }
            };
            const ok = () => fin(false);
            const bad = () => fin(true);
            img.addEventListener("load", ok);
            img.addEventListener("error", bad);
            ++t.InProgress;
            ++t.Count;
            if (src)
                img.src = src;
        }

        /**
         * Wait for all images in progress to complete (load or fail).
         * @returns {Promise<void>} Resolved when nothing is loading.
         */
        WaitAll() {
            if (this.InProgress <= 0)
                return Promise.resolve();
            return new Promise(r => this.Waiters.push(r));
        }
    }

    // ------------------------------------------------------------------ reserved space: nothing moves while the user types (LAY-9)

    /** @type {ResizeObserver|null} Keeps reserved lines at the tallest height they had (Reserve.Line). */
    let reserveRo = null;

    /** @type {Set<HTMLElement>} The reserved lines that keep their tallest height (reset when the window size changes). */
    const reserveKept = new Set();

    /**
     * Keep an element at the tallest height it had while it is in the page: a shorter or cleared text never pulls up what follows.
     * The height starts over when the window size changes (lines wrap differently; that is no typing).
     * @param {HTMLElement} e The element.
     */
    function reserveKeep(e) {
        if (e.SwReserveKept || typeof ResizeObserver !== "function")
            return;
        e.SwReserveKept = true;
        if (!reserveRo) {
            reserveRo = new ResizeObserver(entries => {
                for (const en of entries) {
                    const t = en.target;
                    if (!t.isConnected) {
                        reserveRo.unobserve(t);
                        reserveKept.delete(t);
                        t.SwReserveKept = false;
                        continue;
                    }
                    //  the size in the element's own box-sizing, so min-height means the same box
                    const border = getComputedStyle(t).boxSizing === "border-box";
                    const sz = border ? en.borderBoxSize : en.contentBoxSize;
                    const h = sz && sz[0] ? sz[0].blockSize : en.contentRect.height;
                    if (h > (t.SwReserveMax || 0) + 0.5) {
                        t.SwReserveMax = h;
                        t.style.minHeight = h + "px";
                    }
                }
            });
            let w = window.innerWidth, h = window.innerHeight;
            window.addEventListener("resize", Debounce(() => {
                if (window.innerWidth === w && window.innerHeight === h)
                    return;
                w = window.innerWidth;
                h = window.innerHeight;
                for (const t of reserveKept) {
                    t.SwReserveMax = 0;
                    t.style.minHeight = t.SwReserveBase || "";
                }
            }, 200));
        }
        reserveKept.add(e);
        reserveRo.observe(e);
    }

    /**
     * Sw.Reserve: whatever appears, changes or disappears while the user types (validation messages, hints, password rules, the
     * status next to a button, conditional help) never moves anything (UX guide LAY-9): its space is there before the first key, and
     * only its opacity changes. The framework's own lines use it (field messages of Sw.TextBox / NumberBox / Select with Required or
     * Validate, Sw.Form's messages); a page uses it for its own lines.
     *  - Line(e, o): e keeps its box when it is hidden (the `hidden` attribute or Show(e, false): invisible, opacity 0, never
     *    display: none), is at least o.Lines lines high (default 1, its own line height) and keeps the tallest height it had while it is
     *    in the page (a shorter or a cleared message doesn't pull up what follows). Returns e.
     *  - Show(e, on): show (fade in) or hide (fade out) a reserved line. Returns e.
     *  - Stack(o): one grid cell for things shown one at a time or of a changing size: the cell is as tall as its tallest child.
     *    o.Sizer: texts or nodes, the longest content the cell will hold (invisible copies, aria-hidden, class o.SizerClass so they
     *    look like the real line) size it from the first paint; o.Class: classes of the cell. Append the real lines to it.
     *  - SetSizer(stack, sizer, sizerClass): replace the sizer copies (ex: when the texts are known later).
     * Developer check (Sw.DevMode): a layout shift inside a form or dialog within a second of typing is a developer warning that names
     * the field and what moved.
     */
    const Reserve = {
        Line(e, o) {
            optCheck("Sw.Reserve.Line", o);
            if (!(e instanceof Element))
                return e;
            o = o || {};
            e.classList.add("sw-reserve");
            if (typeof o.Lines === "number" && o.Lines >= 0) {
                e.style.setProperty("--sw-reserve-lines", String(o.Lines));
                //  (inline, so a page's own min-height of the line doesn't undo it; the kept height starts from it again after a resize)
                e.SwReserveBase = o.Lines > 0 ? "calc(" + o.Lines + " * 1lh)" : "";
                if (!(e.SwReserveMax > 0))
                    e.style.minHeight = e.SwReserveBase;
            }
            reserveKeep(e);
            return e;
        },
        Show(e, on) {
            if (!(e instanceof Element))
                return e;
            Reserve.Line(e);
            e.classList.toggle("sw-reserve-off", !on);
            return e;
        },
        Stack(o) {
            optCheck("Sw.Reserve.Stack", o);
            o = o || {};
            const s = el("div.sw-reserve-stack");
            if (o.Class)
                s.classList.add(...String(o.Class).split(/\s+/).filter(x => x));
            Reserve.SetSizer(s, o.Sizer, o.SizerClass);
            return s;
        },
        SetSizer(stack, sizer, sizerClass) {
            if (!(stack instanceof Element))
                return;
            stack.querySelectorAll(":scope > .sw-reserve-sizer").forEach(x => x.remove());
            const list = sizer === undefined || sizer === null ? [] : (Array.isArray(sizer) ? sizer : [sizer]);
            for (const x of list) {
                if (x === null || x === undefined || x === "")
                    continue;
                const z = x instanceof Node ? x : el("div", { text: String(x) });
                if (!(z instanceof Element))
                    continue;
                z.classList.add("sw-reserve-sizer");
                if (sizerClass)
                    z.classList.add(...String(sizerClass).split(/\s+/).filter(c => c));
                z.setAttribute("aria-hidden", "true");
                z.removeAttribute("id");
                z.querySelectorAll("[id]").forEach(c => c.removeAttribute("id"));
                stack.prepend(z);
            }
        },
    };

    /**
     * Developer check of LAY-9 (Sw.DevMode only): a layout shift inside a form or a dialog within a second after the user typed in
     * one of its fields is a developer warning ("[Sw UX] Layout shift while typing ..."), once per field and moved element. Search and
     * filter boxes are not checked (their results are meant to change), nor shifts after an action that followed the typing (a click,
     * Enter, Escape, a submit: a dialog that closes on Send is its result) or inside a closing dialog. Runs once per document.
     */
    function watchTypingShifts() {
        if (watchTypingShifts.Done || typeof PerformanceObserver !== "function")
            return;
        watchTypingShifts.Done = true;
        let last = null;
        /** @type {number} The time of the last action (click, Enter, Escape, submit), on the event / performance time line. */
        let actT = 0;
        const acted = ev => {
            if (ev.isTrusted && (ev.type !== "keydown" || ev.key === "Enter" || ev.key === "Escape"))
                actT = ev.timeStamp;
        };
        document.addEventListener("click", acted, true);
        document.addEventListener("keydown", acted, true);
        document.addEventListener("submit", acted, true);
        document.addEventListener("input", ev => {
            if (ev.isTrusted && ev.target instanceof Element)
                last = { T: ev.timeStamp, E: ev.target };
        }, true);
        const nameOf = e => String(e.getAttribute("aria-label") || (e.labels && e.labels[0] && e.labels[0].textContent) || e.getAttribute("name") || e.getAttribute("placeholder") || e.tagName.toLowerCase()).trim().replace(/\s+/g, " ").slice(0, 40);
        const describe = n => {
            const e = n.nodeType === 1 ? n : n.parentElement;
            if (!e)
                return "?";
            const cls = String(e.className && e.className.baseVal !== undefined ? e.className.baseVal : e.className || "").split(/\s+/).filter(x => x)[0];
            return e.tagName.toLowerCase() + (cls ? "." + cls : "") + (e.textContent && e.textContent.trim() ? " \"" + e.textContent.trim().replace(/\s+/g, " ").slice(0, 30) + "\"" : "");
        };
        try {
            new PerformanceObserver(list => {
                if (!last || !(typeof Sw.DevMode === "function" ? Sw.DevMode() : !!Sw.DevMode))
                    return;
                for (const en of list.getEntries()) {
                    if (en.startTime < last.T - 50 || en.startTime - last.T > 1000)
                        continue;
                    //  (after an action that followed the typing: its result, see acted)
                    if (actT > last.T && en.startTime >= actT - 16)
                        continue;
                    const inp = last.E;
                    if (!inp.isConnected || inp.matches("[type=search], [role=searchbox], [role=combobox], [data-sw-filter], .sw-shell-filterinput"))
                        continue;
                    const host = inp.closest("form, .sw-dialog, .sw-form, [data-sw-form]");
                    //  (a dialog that is closing: its fields move with it)
                    if (!host || host.closest(".sw-leaving, [inert]"))
                        continue;
                    const moved = (en.sources || []).map(s => s.node).filter(n => n && host.contains(n));
                    if (!moved.length)
                        continue;
                    const what = moved.slice(0, 3).map(describe).join(", ");
                    Sw.DevWarn("Layout shift while typing in \"" + nameOf(inp) + "\": " + what + " moved (UX guide LAY-9: reserve the space of messages, hints and rules before the first key and change only their opacity; Sw.Reserve, ui/README \"No layout shift on input\").", "typing-shift:" + nameOf(inp) + ":" + describe(moved[0]));
                }
            }).observe({ type: "layout-shift", buffered: false });
        }
        catch {
            //  no layout shift entries in this browser: no check
        }
    }

    // ------------------------------------------------------------------ exports

    Object.assign(Sw, {
        el,
        clamp,
        snap,
        FormatNumber: formatNumber,
        Icon,
        Icons,
        IconClass,
        LineIcon,
        SetIconClass,
        IsIconClass: isIconClass,
        IconColors,
        IsPureEvent,
        BadClick,
        ModifierClick,
        IsPureContextMenu,
        MenuButton,
        Disabled,
        Work,
        OnClick,
        KeyClick,
        RemoveKeyClick,
        ActionName,
        LongPress,
        NoContextMenu,
        HasSelectionIn,
        Focus,
        Keys,
        Drag,
        Clipboard,
        Color,
        FadeSrc,
        UnfadeSrc,
        CreateImg,
        CreateIFrame,
        SetImageSource,
        PendingImages,
        Reserve,
    });
    //  One developer warning channel: the core's Sw.DevWarn / Sw.DevMode (core/base.js); these only fill in without the core
    if (typeof Sw.DevWarn !== "function")
        Sw.DevWarn = DevWarn;
    if (typeof Sw.DevMode !== "function")
        Sw.DevMode = () => !!Sw.Debug;
    //  The shared option check (unknown / wrong type / colliding options): the core's when it has one
    if (typeof Sw.DevOptions !== "function")
        Sw.DevOptions = DevOptions;
    if (!Sw.Debounce)
        Sw.Debounce = Debounce;
    if (!Sw.Throttle)
        Sw.Throttle = Throttle;
    //  The focus never stays on the body after a result (FOC-1 / FOC-2)
    focusRescue();
    //  LAY-9: a layout shift while typing in a form or dialog is a developer warning
    watchTypingShifts();
})();
