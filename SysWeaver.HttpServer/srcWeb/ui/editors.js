/**
 * @file ui/editors.js - SysWeaver UI library (Sw), part 5 of 7: the property editor.
 *
 * Sw.PropertyEditor renders setting definitions (the shape of the C# SettingDef in EffectWeaver.Common) as a property grid of
 * sliders, number boxes, switches, selects, text boxes and color pickers.
 * Needs ui/base.js, ui/controls.js (and ui/overlay.js for selects / color pickers / toasts). Documentation: ui/README.md.
 */

(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    const { el } = Sw;

    /**
     * Renders a list of setting definitions as a property grid.
     * Definitions: { Key, Label, Type: "float" | "int" | "bool" | "enum" | "vec2" | "vec3" | "vec4" | "color" | "color4" | "string",
     *      Default, Min, Max, Step, Log, Unit, Desc (tooltip of the label), Options: [{ Value, Label }] (enum), Names: [component names] (vectors),
     *      Descs: [component descriptions] (vectors: each component gets its name and description above its slider), Group (a heading before
     *      the first definition of a group), NoSlider (number boxes only, for unbounded values),
     *      Hint (the hint line under the label; default for numbers: the range and unit, ex: "0 to 1 ms"; "" = none), Help (a help text under
     *      the value), ReadOnly (the value is shown as text: no input, nothing can change it; SetValues still updates it) }.
     * Options: Defs, Values ({ key: value }, missing values show the defaults), OnInput(key, value) (continuous), OnChange(key, value) (committed),
     *          Realtime (call OnInput while dragging, default true), ResetButtons (a reset to default button on the number rows, dimmed at the default),
     *          Hints (default true: the hint lines).
     * Layout (Design v2, PORTING.md B14): label column (wraps, never clipped) with the hint line under the name, the value column with help / error texts
     * under the control. Modified values (not the default) get an accent colored label and a "modified" dot after it: a click on the dot (or a
     * long press / right click on the label) resets the value.
     * On narrow screens the label is shown above its value (css).
     * Methods: SetValues(values) (updates the widgets without events), Get(key), SetEnabled(keyOrFn, enabled), SetError(key, text) (a validation
     * message under the value, null clears it; the value gets aria-invalid).
     */
    class PropertyEditor {
        /**
         * @param {object} [o] Options (see the class description).
         */
        constructor(o) {
            const t = this;
            o = o || {};
            t.O = o;
            t.E = el("div.sw-props");
            t.Widgets = new Map();
            t.Values = Object.assign({}, o.Values || {});
            let group = null;
            for (const d of o.Defs || []) {
                if (d.Group && d.Group !== group) {
                    group = d.Group;
                    t.E.append(el("div.sw-prop-group", { text: group, attrs: { role: "heading", "aria-level": "3" } }));
                }
                t.AddRow(d);
            }
        }

        /**
         * The current value of a definition (the default when not set).
         * @param {object} d The definition.
         * @returns {*} The value.
         */
        Value(d) {
            const v = this.Values[d.Key];
            return v === undefined || v === null ? d.Default : v;
        }

        /**
         * Store a value and call the callbacks.
         * @param {object} d The definition.
         * @param {*} v The value.
         * @param {boolean} final True when committed (OnChange), else OnInput (if Realtime).
         */
        Emit(d, v, final) {
            const t = this;
            t.Values[d.Key] = v;
            t.MarkModified(d);
            if (final)
                t.O.OnChange && t.O.OnChange(d.Key, v);
            else if (t.O.Realtime !== false)
                t.O.OnInput && t.O.OnInput(d.Key, v);
        }

        /**
         * Update the modified marker of a row.
         * @param {object} d The definition.
         */
        MarkModified(d) {
            const w = this.Widgets.get(d.Key);
            if (!w)
                return;
            const same = JSON.stringify(this.Value(d)) === JSON.stringify(d.Default);
            w.Label.classList.toggle("sw-modified", !same);
            if (w.Dot)
                w.Dot.hidden = same || !!d.ReadOnly;
            if (w.ResetBtn)
                w.ResetBtn.classList.toggle("sw-at-default", same);
        }

        /**
         * The default hint line of a definition: numbers "min to max unit" (or the unit alone), else "".
         * @param {object} d The definition.
         * @returns {string} The hint.
         */
        static Hint(d) {
            if (d.Hint !== undefined && d.Hint !== null)
                return String(d.Hint);
            const num = !d.Type || d.Type === "float" || d.Type === "int";
            const unit = d.Unit ? " " + d.Unit : "";
            if (num && d.Min !== undefined && d.Max !== undefined && !d.NoSlider)
                return _T("{0} to {1}", d.Min, d.Max, "Range in the hint line of a member, ex: 1 to 500.{0} is the minimum.{1} the maximum") + unit;
            return unit.trim();
        }

        /**
         * Show (or clear) a validation message under a value.
         * @param {string} key The key.
         * @param {?string} text The message (null / "" clears it).
         */
        SetError(key, text) {
            const w = this.Widgets.get(key);
            if (!w)
                return;
            //  LAY-9: a shown message keeps its line (cleared while typing, the rows below don't move)
            if (text && Sw.Reserve)
                Sw.Reserve.Line(w.Error);
            w.Error.textContent = text || "";
            w.Error.hidden = !text;
            if (text)
                w.Value.setAttribute("aria-invalid", "true");
            else
                w.Value.removeAttribute("aria-invalid");
        }

        /**
         * The display text of a read only value.
         * @param {object} d The definition.
         * @param {*} x The value.
         * @returns {string} The text.
         */
        static Text(d, x) {
            if (x === null || x === undefined)
                return "null";
            if (d.Type === "bool")
                return x ? _TF("Yes", "Read only boolean value shown as text: true") : _TF("No", "Read only boolean value shown as text: false");
            if (d.Type === "enum") {
                const o = (d.Options || []).find(p => p.Value === x);
                return o ? (o.Label ?? String(x)) : String(x);
            }
            if (Array.isArray(x))
                return x.map(v => typeof v === "number" ? +v.toFixed(4) : v).join(", ");
            if (typeof x === "number")
                return String(+x.toFixed(6)) + (d.Unit ? " " + d.Unit : "");
            return typeof x === "object" ? JSON.stringify(x) : String(x);
        }

        /**
         * Add a row for a definition.
         * @param {object} d The definition.
         */
        AddRow(d) {
            const t = this;
            const name = d.Label || d.Key;
            const label = el("div.sw-prop-label", { title: d.Desc || name }, el("span.sw-prop-name", { text: name }));
            const value = el("div.sw-prop-value", { attrs: { role: "group", "aria-label": name } });
            const w = { Def: d, Label: label, Value: value, Set: null };
            const v = t.Value(d);
            //  The "modified" dot after the name: a click resets to the default (as the long press on the label)
            const reset = () => {
                const def = d.Default;
                w.Set(def);
                t.Emit(d, def, true);
                if (Sw.Toast)
                    Sw.Toast(_T("{0} reset", name, "Message shown when a setting was reset to its default value, {0} is the name of the setting"), { NoCopy: true, Duration: 2000 });
            };
            if (!d.ReadOnly) {
                w.Dot = el("button.sw-prop-dot", {
                    type: "button", hidden: true,
                    title: _T("Modified: click to reset to the default ({0})", String(d.Default) + (d.Unit || ""), "Tool tip of the dot after a modified setting; a click resets it.{0} is the default value"),
                    attrs: { "aria-label": _T("Reset {0} to the default", name, "Accessible name of the dot after a modified setting.{0} is the name of the setting") },
                });
                w.Dot.addEventListener("click", ev => {
                    ev.stopPropagation();
                    reset();
                });
                label.append(w.Dot);
            }
            //  Read only rows show only an explicit Hint: the default range / unit hint says nothing the value text doesn't ("1234.5 h")
            const hint = t.O.Hints === false || (d.ReadOnly && (d.Hint === undefined || d.Hint === null)) ? "" : PropertyEditor.Hint(d);
            if (hint) {
                //  Every " · " part never breaks inside ("0.05 to 8 ×"); the line wraps between parts
                const he = el("small.sw-prop-hint", { title: hint });
                hint.split(" · ").forEach((p, i) => {
                    if (i)
                        he.append(" · ");
                    he.append(el("span.sw-prop-hintpart", { text: p }));
                });
                label.append(he);
            }
            w.Error = el("div.sw-prop-error", { hidden: true, attrs: { role: "alert" } });
            if (d.ReadOnly) {
                //  Read only: plain text, no input (Design v2: nothing read only can become writable)
                const ro = el("span.sw-prop-ro", { tabIndex: 0, attrs: { role: "textbox", "aria-readonly": "true", "aria-label": name } });
                value.append(ro);
                //  null and empty texts show a quiet token ("null", "empty"; the same as Sw.Edit's), never a blank
                w.Set = x => {
                    const isNull = x === null || x === undefined;
                    const s = isNull ? "" : PropertyEditor.Text(d, x);
                    ro.textContent = s;
                    ro.classList.toggle("sw-prop-null", isNull);
                    ro.classList.toggle("sw-prop-empty", !isNull && s === "");
                    if (isNull || s === "")
                        ro.dataset.token = isNull ? "null" : _TF("empty", "Shown (muted) instead of a read only value that is an empty text");
                    else
                        delete ro.dataset.token;
                };
                w.Set(v);
                label.classList.add("sw-prop-label-ro");
                t.Widgets.set(d.Key, w);
                t.E.append(label, value);
                if (d.Help)
                    t.E.append(el("div.sw-prop-help", { text: d.Help }));
                t.E.append(w.Error);
                t.MarkModified(d);
                return;
            }
            const num = (comp, val, def, compName) => {
                const step = d.Step ?? (d.Type === "int" ? 1 : 0.01);
                const aria = compName ? name + " " + compName : name;
                if (d.NoSlider) {
                    const only = new Sw.NumberBox({ Step: step, Value: val, AriaLabel: aria, OnChange: x => comp(x, true) });
                    return { Slider: { E: null }, Box: only, Set: x => only.Set(x, true) };
                }
                const box = new Sw.NumberBox({ Step: step, Value: val, AriaLabel: aria, Min: d.Type === "int" ? d.Min : undefined, OnChange: x => slider.Set(x, false, true) });
                const slider = new Sw.Slider({
                    Min: d.Min ?? 0, Max: d.Max ?? 1, Step: d.Type === "int" ? Math.max(1, d.Step || 1) : step, Log: !!d.Log, Value: val, Default: def,
                    Center: d.Log ? def : undefined, Label: aria,
                    OnInput: x => {
                        box.Set(x, true);
                        comp(x, false);
                    },
                    OnChange: x => {
                        box.Set(x, true);
                        comp(x, true);
                    },
                });
                return { Slider: slider, Box: box, Set: x => { slider.Set(x, true, true); box.Set(x, true); } };
            };
            switch (d.Type) {
                case "bool": {
                    const tg = new Sw.Toggle({ Value: !!v, Title: name, OnChange: x => t.Emit(d, x, true) });
                    value.append(tg.E);
                    w.Set = x => tg.Set(!!x, true);
                    break;
                }
                case "enum": {
                    const sel = new Sw.Select({ Options: d.Options || [], Value: v, Title: name, OnChange: x => t.Emit(d, x, true) });
                    value.append(sel.E);
                    w.Set = x => sel.Set(x, true);
                    break;
                }
                case "string": {
                    const tb = new Sw.TextBox({ Value: v ?? "", Title: name, OnChange: x => t.Emit(d, x, true) });
                    tb.E.style.flex = "1";
                    value.append(tb.E);
                    w.Set = x => tb.Set(x ?? "", true);
                    break;
                }
                case "color":
                case "color4": {
                    const cp = new Sw.ColorPicker({ Value: v, Title: name, OnInput: x => t.Emit(d, x, false), OnChange: x => t.Emit(d, x, true) });
                    value.append(cp.E);
                    w.Set = x => cp.Set(x, true);
                    break;
                }
                case "vec2":
                case "vec3":
                case "vec4": {
                    const n = +d.Type[3];
                    const names = d.Names || ["x", "y", "z", "w"];
                    const box = el("div.sw-prop-vec");
                    const parts = [];
                    for (let i = 0; i < n; ++i) {
                        const def = Array.isArray(d.Default) ? d.Default[i] : 0;
                        const p = num((x, final) => {
                            const cur = (t.Value(d) || []).slice();
                            while (cur.length < n)
                                cur.push(0);
                            cur[i] = x;
                            t.Emit(d, cur, final);
                        }, Array.isArray(v) ? v[i] : 0, def, names[i] || "");
                        parts.push(p);
                        if (d.Descs)
                            box.append(el("div.sw-prop-vec-named", null,
                                el("div.sw-prop-vec-name", { text: names[i] || "" }),
                                d.Descs[i] ? el("div.sw-prop-vec-desc", { text: d.Descs[i] }) : null,
                                el("div.sw-prop-vec-row", null, p.Slider.E, p.Box.E)));
                        else
                            box.append(el("div.sw-prop-vec-row", null, el("span.sw-prop-comp", { text: names[i] || "" }), p.Slider.E, p.Box.E));
                    }
                    value.append(box);
                    w.Set = x => parts.forEach((p, i) => p.Set(Array.isArray(x) ? x[i] : 0));
                    break;
                }
                default: {
                    const p = num((x, final) => t.Emit(d, x, final), v, d.Default);
                    value.append(...[p.Slider.E, p.Box.E].filter(x => x));
                    //  the unit right after its value box, also with reset buttons (it was dropped there and only the hint named it)
                    if (d.Unit)
                        value.append(el("span.sw-muted.sw-prop-unit", { text: d.Unit }));
                    w.Set = x => p.Set(x);
                    if (t.O.ResetButtons) {
                        w.ResetBtn = new Sw.Button({
                            Icon: "reset", Small: true, Kind: "ghost", Class: "sw-prop-reset",
                            Title: _T("Set the default ({0})", String(d.Default) + (d.Unit || ""), "Tool tip of a button that resets a setting to its default value, {0} is the default value"),
                            OnClick: () => {
                                w.Set(d.Default);
                                t.Emit(d, d.Default, true);
                            },
                        }).E;
                        value.append(w.ResetBtn);
                    }
                    break;
                }
            }
            //  Long press / right click on the label resets to the default
            Sw.LongPress(label, reset);
            t.Widgets.set(d.Key, w);
            t.E.append(label, value);
            //  Help and validation texts under the control (grid column 2)
            if (d.Help)
                t.E.append(el("div.sw-prop-help", { text: d.Help }));
            t.E.append(w.Error);
            t.MarkModified(d);
        }

        /**
         * Get a value.
         * @param {string} key The key.
         * @returns {*} The value (the default when not set), undefined for unknown keys.
         */
        Get(key) {
            const w = this.Widgets.get(key);
            return w ? this.Value(w.Def) : undefined;
        }

        /**
         * Enable / disable rows: match = a key, or a function(def) returning true for the rows to change.
         * Disabled rows are dimmed and don't take input (their values still update with SetValues).
         * @param {string|function(object):boolean} match A key or a filter.
         * @param {boolean} enabled Enabled.
         */
        SetEnabled(match, enabled) {
            for (const w of this.Widgets.values()) {
                if (typeof match === "function" ? !match(w.Def) : w.Def.Key !== match)
                    continue;
                w.Value.classList.toggle("sw-disabled", !enabled);
                w.Label.classList.toggle("sw-disabled", !enabled);
                if (enabled)
                    w.Value.removeAttribute("inert");
                else
                    w.Value.setAttribute("inert", "");
            }
        }

        /**
         * Update the widgets (no events), keys that are missing get their defaults.
         * @param {object} values The values.
         */
        SetValues(values) {
            const t = this;
            t.Values = Object.assign({}, values || {});
            for (const w of t.Widgets.values()) {
                w.Set(t.Value(w.Def));
                t.MarkModified(w.Def);
            }
        }
    }

    Sw.PropertyEditor = PropertyEditor;
})();
