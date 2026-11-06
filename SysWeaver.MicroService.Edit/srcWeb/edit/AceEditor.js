/**
 * @file edit/AceEditor.js - `Sw.AceEditor`: a small, page independent wrapper of the vendored Ace editor (edit/external/ace.js) with the
 * SysWeaver theme (edit/ace.css, class "ace-sysweaver"). The text page (edit/text.html) uses its theme helper; other pages (ex: the JSON
 * editors of explore/api.html) create editors with it, Ace and the theme are loaded on demand:
 *
 *     await Sw.Net.IncludeJs(null, "../edit/AceEditor.js");
 *     const ed = await Sw.AceEditor.Create({ Mode: "json", MinLines: 4, MaxLines: 30, OnChange: text => ... });
 *     host.append(ed.E); ed.Set("{}");
 *
 * Interface (the same as Sw.CodeEditor of ui/code.js, so a page can switch): `E` (the host element), `Input` (the text input, for an
 * accessible name), `Get()`, `Set(text)` (no undo history: a new text), `Focus()`, `SetMarkers([{Line, Kind, Text}])` (gutter
 * annotations, Kind "error" / "warning" / "info"), plus `Editor` (the Ace editor), `SetReadOnly(on)`, `Resize()`.
 * Height: with MaxLines the editor grows with its text from MinLines to MaxLines lines (then scrolls); without it the host's css height.
 * With Resizable a grip in the bottom right corner changes the most lines shown (remembered with SizeKey).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /** @type {string} The url of this script (Ace and the theme are resolved relative to it). */
    const Self = (document.currentScript && document.currentScript.src) || new URL("../edit/AceEditor.js", document.baseURI).href;

    /** @type {?Promise<boolean>} The load of Ace and the theme (once). */
    let loading = null;

    /** @type {boolean} The popups of Ace (appended to body) are tagged with the theme class. */
    let popupsTagged = false;

    class AceEditor {
        /**
         * Load Ace (edit/external/ace.js, ext-language_tools.js) and the theme (edit/ace.css) once; nothing is loaded when the page has them.
         * @returns {Promise<boolean>} True when Ace is available.
         */
        static Load() {
            if (!loading)
                loading = (async () => {
                    try {
                        await Sw.Net.IncludeCss(Self, "ace.css");
                        if (!window.ace)
                            await Sw.Net.IncludeJs(Self, "external/ace.js");
                        if (window.ace && ace.config) {
                            //  Modes, workers and extensions are loaded by Ace from the folder of ace.js
                            if (!ace.config.get("basePath"))
                                ace.config.set("basePath", new URL("external/", Self).href);
                            if (!ace.require("ace/ext/language_tools"))
                                await Sw.Net.IncludeJs(Self, "external/ext-language_tools.js", true);
                        }
                    }
                    catch (e) {
                        console.warn("The Ace editor (edit/external/ace.js) could not be loaded: " + e);
                    }
                    return !!(window.ace && ace.edit);
                })();
            return loading;
        }

        /**
         * Apply the SysWeaver theme to an Ace editor: the "ace-sysweaver" theme (follows the light / dark scheme of the Sw theme) and the
         * same class on the Ace popups appended to body (autocomplete, tooltips).
         * @param {object} editor - The Ace editor.
         * @param {HTMLElement} [host] - The editor element (default: editor.container).
         */
        static Theme(editor, host) {
            //  The Sw based theme replaces Ace's default (textmate) theme, whose compound token rules would win otherwise.
            const set = () => editor.setTheme({ cssClass: "ace-sysweaver", isDark: !!(Sw.Theme && Sw.Theme.IsDark), cssText: "" });
            set();
            if (Sw.Theme && Sw.Theme.OnChange)
                Sw.Theme.OnChange(set);
            (host || editor.container).classList.add("ace-sysweaver");
            if (popupsTagged || !document.body)
                return;
            popupsTagged = true;
            const tag = () => {
                for (const c of document.body.children)
                    if (c.tagName === "DIV" && (c.classList.contains("ace_editor") || c.classList.contains("ace_tooltip")) && !c.classList.contains("ace-sysweaver"))
                        c.classList.add("ace-sysweaver");
            };
            new MutationObserver(tag).observe(document.body, { childList: true });
        }

        /**
         * Load Ace (when needed) and create an editor.
         * @param {object} [o] - Options, see the constructor.
         * @returns {Promise<?AceEditor>} The editor, null when Ace could not be loaded.
         */
        static async Create(o) {
            return await AceEditor.Load() ? new AceEditor(o) : null;
        }

        /**
         * Create an editor (Ace must be loaded: use Create).
         * @param {object} [o] - Options.
         * @param {string} [o.Mode] - Ace mode ("json", "sh", "javascript" ...; default "text").
         * @param {boolean} [o.ReadOnly] - Read only (the text can be selected and copied).
         * @param {number} [o.TabSize] - Indent size (default 2).
         * @param {boolean} [o.Wrap] - Soft wrap long lines.
         * @param {number} [o.MinLines] - Grow with the text from this many lines (with MaxLines).
         * @param {number} [o.MaxLines] - ... up to this many lines, then scroll.
         * @param {boolean} [o.Resizable] - (with MaxLines) A grip in the bottom right corner sets how many lines are shown at most: drag
         *   (or arrow keys, Shift = 10 lines), double click / End = all the text, Enter = MaxLines again.
         * @param {string} [o.SizeKey] - (Resizable) Local storage key that remembers the size ("all" or a line count).
         * @param {string} [o.Placeholder] - Text shown while the editor is empty.
         * @param {string} [o.Label] - Accessible name of the text input.
         * @param {string} [o.Class] - Extra class(es) of the host element.
         * @param {function(string): void} [o.OnChange] - Called after the user changed the text (not after Set).
         */
        constructor(o) {
            o = o || {};
            const t = this;
            /** @type {object} The options. */
            t.O = o;
            /** @type {HTMLElement} The host element. */
            t.E = el("div.sw-ace", { class: o.Class || null });
            const opts = {
                mode: "ace/mode/" + (o.Mode || "text"),
                readOnly: !!o.ReadOnly,
                tabSize: o.TabSize || 2,
                useSoftTabs: true,
                wrap: !!o.Wrap,
                showPrintMargin: false,
                highlightActiveLine: !o.ReadOnly,
                highlightGutterLine: !o.ReadOnly,
                showFoldWidgets: true,
                useSvgGutterIcons: true,
                //  The page validates its own texts (ex: the API inspector against the argument type)
                useWorker: false,
                fontSize: "var(--sw-mono-size)",
                placeholder: o.Placeholder || undefined,
            };
            if (o.MaxLines) {
                opts.minLines = o.MinLines || 1;
                opts.maxLines = o.MaxLines;
            }
            /** @type {object} The Ace editor. */
            const ed = t.Editor = ace.edit(t.E, opts);
            AceEditor.Theme(ed, t.E);
            ed.renderer.setScrollMargin(4, 4);
            /** @type {HTMLElement} The text input of the editor. */
            t.Input = ed.textInput.getElement();
            if (o.Label)
                t.Input.setAttribute("aria-label", o.Label);
            let setting = false;
            ed.session.on("change", () => {
                if (!setting && o.OnChange)
                    o.OnChange(ed.getValue());
            });
            t.SetValue = v => {
                setting = true;
                try {
                    ed.session.setValue(v ?? "");
                }
                finally {
                    setting = false;
                }
            };
            new ResizeObserver(() => ed.resize()).observe(t.E);
            if (o.Resizable && o.MaxLines)
                t.AddGrip();
        }

        /**
         * The size grip (option Resizable): sets the most lines shown (Ace's maxLines; a shorter text keeps its own height).
         */
        AddGrip() {
            const t = this;
            const o = t.O;
            const ed = t.Editor;
            const def = o.MaxLines;
            const min = o.MinLines || 1;
            let lines = def;
            if (o.SizeKey) {
                try {
                    const v = localStorage.getItem(o.SizeKey);
                    const n = parseInt(v, 10);
                    lines = v === "all" ? Infinity : n >= min ? n : def;
                }
                catch {
                }
            }
            const grip = el("div.sw-ace-grip", {
                tabIndex: 0,
                title: _TF("Drag to show more or fewer lines. Double click: all the text, again: the normal size.", "Tool tip of the size grip in the bottom right corner of a code / json box"),
                attrs: { role: "separator", "aria-orientation": "horizontal", "aria-label": _TF("Height of the text box", "Accessible name of the size grip of a code / json box") },
            });
            /**
             * Show at most n lines.
             * @param {number} n - Lines (Infinity: all).
             * @param {boolean} [dragging] - While dragging: exactly n lines, so the box follows the pointer.
             * @param {boolean} [keep] - Remember it (SizeKey).
             */
            const set = (n, dragging, keep) => {
                lines = n === Infinity ? n : Math.max(min, Math.round(n));
                ed.setOptions({ minLines: dragging && lines !== Infinity ? lines : min, maxLines: lines });
                grip.setAttribute("aria-valuetext", lines === Infinity ? _TF("All lines", "Value of the size grip of a code / json box: the whole text is shown") : _T("{0} lines", lines, "Value of the size grip of a code / json box.{0} is the number of lines shown at most"));
                if (!keep || !o.SizeKey)
                    return;
                try {
                    if (lines === def)
                        localStorage.removeItem(o.SizeKey);
                    else
                        localStorage.setItem(o.SizeKey, lines === Infinity ? "all" : String(lines));
                }
                catch {
                }
            };
            /** @returns {number} The lines shown now. */
            const shown = () => Math.max(min, Math.min(ed.session.getScreenLength(), lines));
            let start = 0;
            Sw.Drag(grip, {
                Threshold: 0,
                OnStart: e => {
                    e.preventDefault();
                    start = shown();
                },
                OnMove: (e, s) => set(start + s.DY / (ed.renderer.lineHeight || 16), true),
                OnEnd: () => set(lines, false, true),
            });
            grip.addEventListener("dblclick", () => set(lines === Infinity ? def : Infinity, false, true));
            grip.addEventListener("keydown", e => {
                const step = e.shiftKey ? 10 : 1;
                switch (e.key) {
                    case "ArrowUp": set(shown() - step, false, true); break;
                    case "ArrowDown": set(shown() + step, false, true); break;
                    case "Home": set(min, false, true); break;
                    case "End": set(Infinity, false, true); break;
                    case "Enter": set(def, false, true); break;
                    default: return;
                }
                e.preventDefault();
            });
            t.E.append(grip);
            set(lines);
        }

        /** @returns {string} The text. */
        Get() {
            return this.Editor.getValue();
        }

        /**
         * Replace the text (cursor at the start, a new undo history; no OnChange).
         * @param {string} text - The text.
         */
        Set(text) {
            this.SetValue(text);
            this.Editor.clearSelection();
            this.Editor.gotoLine(1, 0, false);
            this.Editor.renderer.scrollToRow(0);
        }

        /** Focus the editor. */
        Focus() {
            this.Editor.focus();
        }

        /**
         * Read only on / off.
         * @param {boolean} on - Read only.
         */
        SetReadOnly(on) {
            this.Editor.setReadOnly(!!on);
        }

        /** Re-layout (after the host was shown or resized by code). */
        Resize() {
            this.Editor.resize(true);
        }

        /**
         * Mark lines in the gutter (replaces the previous marks).
         * @param {Array<{Line: number, Kind: ?string, Text: ?string}>} marks - 1 based line numbers.
         */
        SetMarkers(marks) {
            this.Editor.session.setAnnotations((marks || []).map(m => ({ row: Math.max(0, (m.Line || 1) - 1), column: 0, text: m.Text || "", type: m.Kind || "error" })));
        }
    }

    Sw.AceEditor = AceEditor;
})();
