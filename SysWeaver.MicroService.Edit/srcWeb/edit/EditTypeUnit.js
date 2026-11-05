/**
 * @file Type handler for numbers with a unit (`Sw.Edit.Handlers.Unit`, legacy global `EditTypeUnit`), selected by the member spec
 * `Unit:<Unit>` (C#: `[EditUnit(EditUnits.Seconds)]` in SysWeaver.Common/Edit, `EditTypes.Unit`).
 *
 * Loaded by Edit.js. Works on every number member (Int32, Int64, Double, ... and their nullable forms): the stored value stays a plain
 * number in the member's unit. The editor is a text box with the unit after it ("s", "MB", "%"), a readable hint ("= 2 h 30 min",
 * "= 2 MB", "= 25 %") and, for members with a range and the Slider flag, a slider. Typed text may carry its own unit, it is converted:
 * | Kind | Units (member spec) | Typed text examples |
 * |---|---|---|
 * | Duration | Milliseconds, Seconds, Minutes, Hours, Days | "90", "90s", "1h 30m", "1.5 h", "2d", "250ms" |
 * | Byte size (1024 based) | Bytes, KiloBytes, MegaBytes, GigaBytes | "2097152", "2 MB", "512k", "1.5 GiB" |
 * | Percent | Percent (0 to 100), Ratio (0 to 1, shown as a percent hint) | "50", "50%"; Ratio: "0.25", "25%" |
 * Min / Max (in the member's unit) are applied: a typed value outside them is set to the limit and the hint says so (never silently).
 * Unknown units fall back to the number editor of the type.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;

    /**
     * The units: kind, size of one unit in the base unit of the kind (ms, bytes, ratio), the suffix shown after the box.
     * @type {Object<string, {Kind: string, Size: number, Suffix: string}>}
     */
    const Units = {
        Milliseconds: { Kind: "Duration", Size: 1, Suffix: "ms" },
        Seconds: { Kind: "Duration", Size: 1000, Suffix: "s" },
        Minutes: { Kind: "Duration", Size: 60000, Suffix: "min" },
        Hours: { Kind: "Duration", Size: 3600000, Suffix: "h" },
        Days: { Kind: "Duration", Size: 86400000, Suffix: "d" },
        Bytes: { Kind: "Bytes", Size: 1, Suffix: "B" },
        KiloBytes: { Kind: "Bytes", Size: 1024, Suffix: "KB" },
        MegaBytes: { Kind: "Bytes", Size: 1048576, Suffix: "MB" },
        GigaBytes: { Kind: "Bytes", Size: 1073741824, Suffix: "GB" },
        Percent: { Kind: "Ratio", Size: 0.01, Suffix: "%" },
        Ratio: { Kind: "Ratio", Size: 1, Suffix: "" },
    };

    /** Typed unit words (lower case) -> size in the base unit of their kind. */
    const Words = {
        Duration: {
            ms: 1, msec: 1, millisecond: 1, milliseconds: 1,
            s: 1000, sec: 1000, secs: 1000, second: 1000, seconds: 1000,
            m: 60000, min: 60000, mins: 60000, minute: 60000, minutes: 60000,
            h: 3600000, hr: 3600000, hrs: 3600000, hour: 3600000, hours: 3600000,
            d: 86400000, day: 86400000, days: 86400000,
            w: 604800000, week: 604800000, weeks: 604800000,
        },
        Bytes: {
            b: 1, byte: 1, bytes: 1,
            k: 1024, kb: 1024, kib: 1024,
            m: 1048576, mb: 1048576, mib: 1048576,
            g: 1073741824, gb: 1073741824, gib: 1073741824,
            t: 1099511627776, tb: 1099511627776, tib: 1099511627776,
        },
        Ratio: {
            "%": 0.01,
        },
    };

    /**
     * Number with unit handler (one instance per member type and unit).
     */
    class EditTypeUnit extends E.Handler {
        /**
         * @param {string} typeName - The member type.
         * @param {object} unit - The unit (Units entry).
         * @param {string} unitName - The unit name (spec part).
         * @param {object} base - The number handler of the type (range, default, validation).
         */
        constructor(typeName, unit, unitName, base) {
            super();
            /** @type {string} The member type. */
            this.TypeName = typeName;
            /** @type {{Kind: string, Size: number, Suffix: string}} The unit. */
            this.Unit = unit;
            /** @type {string} The unit name ("Seconds"). */
            this.UnitName = unitName;
            /** @type {object} The number handler of the member type. */
            this.Base = base;
        }

        /**
         * Parse a typed text into a number in a unit: a plain number is in that unit, "1h 30m" / "2 MB" / "25%" are converted.
         * @param {string} text - The text.
         * @param {{Kind: string, Size: number}} unit - The unit of the result.
         * @returns {number} The value (NaN when not readable).
         */
        static ParseIn(text, unit) {
            let s = ("" + text).trim().toLowerCase().replace(/,/g, ".");
            if (!s)
                return NaN;
            if (/^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/.test(s))
                return parseFloat(s);
            const words = Words[unit.Kind] || {};
            const neg = s.startsWith("-");
            if (neg)
                s = s.substring(1).trim();
            const re = /(\d+\.?\d*|\.\d+)\s*([a-z%µ]+)/g;
            let total = 0, any = false, m, rest = s;
            while ((m = re.exec(s)) !== null) {
                const w = words[m[2]];
                if (w === undefined)
                    return NaN;
                total += parseFloat(m[1]) * w;
                any = true;
                rest = rest.replace(m[0], "");
            }
            if (!any || rest.trim())
                return NaN;
            return (neg ? -total : total) / unit.Size;
        }

        /**
         * The readable form of a value: durations as "1 h 30 min", sizes in the best 1024 unit ("2 MB"), ratios as a percent.
         * @param {number} v - The value in the member's unit.
         * @returns {string} The text ("" for no value).
         */
        Human(v) {
            if (v === null || v === undefined || isNaN(v))
                return "";
            const u = this.Unit;
            const b = v * u.Size;
            if (u.Kind === "Duration") {
                const TS = E.Handlers.TimeSpan;
                return TS ? TS.Human(b) : b + " ms";
            }
            if (u.Kind === "Bytes") {
                const units = ["B", "KB", "MB", "GB", "TB"];
                let i = 0, x = Math.abs(b);
                while (x >= 1024 && i < units.length - 1) {
                    x /= 1024;
                    ++i;
                }
                return (b < 0 ? "-" : "") + (Math.round(x * 100) / 100) + " " + units[i];
            }
            return (Math.round(b * 10000) / 100) + " %";
        }

        /**
         * The default (the number handler's).
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {*} The default.
         */
        GetDefault(member, options) {
            return this.Base.GetDefault(member, options);
        }

        /**
         * Accept numbers and texts with a unit.
         * @param {*} obj - Candidate.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj, member, options) {
            if (typeof obj === "number")
                return this.Base.IsOfType(obj, member, options);
            return typeof obj === "string" && !isNaN(EditTypeUnit.ParseIn(obj, this.Unit));
        }

        /**
         * Convert an accepted value (a pasted "2h") to the stored number (range applied by the number handler).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {number} The value.
         */
        Condition(obj, member, options) {
            const v = typeof obj === "number" ? obj : EditTypeUnit.ParseIn(obj, this.Unit);
            return this.Base.Condition(v, member, options);
        }

        /**
         * Validate (the number handler's checks).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            return await this.Base.Validate(obj, member, options, name);
        }

        /**
         * Display text: the number with its unit.
         * @param {*} obj - The value.
         * @returns {string} The text.
         */
        ToString(obj) {
            if (obj === null || obj === undefined)
                return "null";
            return obj + (this.Unit.Suffix ? " " + this.Unit.Suffix : "");
        }

        /**
         * Build the text box, the unit, the hint and (Slider flag with a range) a slider.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item.
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const t = this;
            const B = t.Base;
            const mn = member.Name;
            const cell = editContext.Element;
            const r = B.Range(member, options);
            const readOnly = this.IsReadOnly(member, options);
            const nullOk = B.AcceptsNull(member);
            if (obj[mn] === undefined || (obj[mn] === null && !nullOk) || (obj[mn] !== null && typeof obj[mn] === "object"))
                obj[mn] = r.Def;
            const u = t.Unit;
            const hint = Sw.el("span.sw-edit-hint", { attrs: { "aria-live": "polite" } });
            /** @type {?{Value: number, Text: string}} A typed value outside Min / Max that was set to the limit. */
            let clampNote = null;
            const fmt = v => v === null || v === undefined || (typeof v === "number" && isNaN(v)) ? "" : "" + v;
            const parse = s => {
                s = ("" + s).trim();
                if (!s)
                    return nullOk ? null : obj[mn];
                let v = EditTypeUnit.ParseIn(s, u);
                if (isNaN(v))
                    return obj[mn];
                if (B.IsInteger)
                    v = Math.round(v);
                const c = Math.min(Math.max(v, r.Min), r.Max);
                if (c !== v)
                    clampNote = {
                        Value: c,
                        Text: c > v
                            ? _T("Set to the minimum {0} ({1} is too small)", t.Human(c), t.Human(v), "Message after a box of a number with a unit when the typed value was below the minimum and the minimum was used.{0} is the minimum.{1} the typed value")
                            : _T("Set to the maximum {0} ({1} is too large)", t.Human(c), t.Human(v), "Message after a box of a number with a unit when the typed value was above the maximum and the maximum was used.{0} is the maximum.{1} the typed value"),
                    };
                else if (!(clampNote && clampNote.Value === c))
                    clampNote = null;
                return c;
            };
            const commit = async (v, token) => {
                const old = obj[mn];
                if (old === v) {
                    sync();
                    return;
                }
                await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", t.ToString(v), "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
            };
            const ph = u.Kind === "Duration" ? _TF("ex: 90, 90s, 1h 30m, 2d", "Placeholder of the text box of a duration number (the member has a time unit)")
                : u.Kind === "Bytes" ? _TF("ex: 2048, 2 MB, 512k", "Placeholder of the text box of a byte size number")
                    : _TF("ex: 25, 25%", "Placeholder of the text box of a percent / ratio number");
            const text = E.CreateTextInput(() => obj[mn], (v, ev, token) => commit(v, token), parse, fmt, ph, null, null, 40, r.Def, readOnly);
            text.inputMode = "text";
            text.classList.add("sw-edit-number", "sw-edit-unitbox");
            text.style.setProperty("--sw-edit-chars", Math.min(Math.max(("" + r.Max).length, 6) + 2, 18));
            text.setAttribute("aria-label", (member.DisplayName || mn) + (u.Suffix ? " (" + u.Suffix + ")" : ""));
            E.FitNumberBox(text);
            const suffix = u.Suffix ? Sw.el("span.sw-edit-unit", { text: u.Suffix, attrs: { "aria-hidden": "true" } }) : null;
            const sync = () => {
                const v = obj[mn];
                const note = clampNote && clampNote.Value === v ? clampNote.Text : null;
                hint.classList.toggle("sw-edit-hintwarn", !!note);
                const h = t.Human(v);
                //  The readable form only when it says more than the number and its unit ("= 2 h" for 7200 s, nothing for 30 s)
                hint.textContent = note || (h && h.replace(/\s/g, "") !== (fmt(v) + u.Suffix).replace(/\s/g, "") ? "= " + h : "");
            };
            text.addEventListener("input", () => {
                const s = text.value.trim();
                const bad = !!s && isNaN(EditTypeUnit.ParseIn(s, u));
                text.classList.toggle("sw-invalid", bad);
                text.setAttribute("aria-invalid", bad ? "true" : "false");
                if (bad) {
                    hint.classList.remove("sw-edit-hintwarn");
                    hint.textContent = _TF("not a number", "Hint after the text box of a number with a unit when the text can't be read");
                }
                else
                    sync();
            });
            text.addEventListener("blur", () => {
                text.classList.remove("sw-invalid");
                sync();
            });
            cell.append(text);
            if (suffix)
                cell.append(suffix);
            let slider = null;
            if (!readOnly && r.HaveMin && r.HaveMax && (member.Flags & F.Slider) !== 0) {
                let token = null;
                const step = B.SliderStep(member, r.Min, r.Max);
                slider = new Sw.Slider({
                    Min: r.Min, Max: r.Max, Step: step, Value: obj[mn] ?? r.Def, Default: r.Def, Disabled: readOnly,
                    Title: member.DisplayName || mn,
                    OnStart: () => token = {},
                    OnInput: v => commit(v, token || (token = {})),
                    OnChange: v => {
                        const tk = token;
                        token = null;
                        return commit(v, tk);
                    },
                });
                cell.classList.add("EditSlider", "sw-edit-hasslider");
                cell.append(slider.E);
            }
            cell.append(hint);
            sync();
            let tip = r.Title;
            tip = E.Util.AddLine(tip, _TF("Unit", "Header of a tool tip row with the unit of a number value, ex: Unit: seconds"), t.UnitName);
            return E.Handler.Item(obj, editor, member, {
                Focus: text, Title: tip, DefValue: r.ParsedDef, DefValueText: r.ParsedDef === undefined ? undefined : t.ToString(r.ParsedDef),
                Update: () => {
                    text.UpdateValue();
                    if (slider && obj[mn] !== null)
                        slider.Set(obj[mn], true, true);
                    sync();
                },
            });
        }
    }

    /**
     * The plug-in factory (old hook name): a handler for number members with a known unit, null otherwise (the number editor is used).
     * @param {string} typeName - The member type.
     * @param {string[]} parts - The spec parts ("Unit", unit name).
     * @returns {?EditTypeUnit} The handler.
     */
    function EditTypeUnitFactory(typeName, parts) {
        const name = parts && parts[1];
        const unit = name ? Units[name] : null;
        if (!unit)
            return null;
        const base = E.GetTypeHandler(typeName);
        if (!base || !(base instanceof E.NumberHandler))
            return null;
        return new EditTypeUnit(typeName, unit, name, base);
    }

    EditTypeUnit.Units = Units;
    E.Handlers.Unit = EditTypeUnit;
    E.RegisterSpec("Unit", EditTypeUnitFactory);
    if (!window.EditTypeUnit)
        window.EditTypeUnit = EditTypeUnit;
    if (!window.EditTypeUnitFactory)
        window.EditTypeUnitFactory = EditTypeUnitFactory;
})();
