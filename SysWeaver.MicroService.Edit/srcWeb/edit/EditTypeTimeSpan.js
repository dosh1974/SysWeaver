/**
 * @file Type handlers for `System.TimeSpan` (durations) and `System.Guid` (`Sw.Edit.Handlers.TimeSpan` / `Guid`, legacy globals
 * `EditTypeTimeSpan` / `EditTypeGuid`); both also as `System.Nullable`1[[...]]`. Before these, both types were shown read only.
 *
 * Loaded by Edit.js. JSON form (SwJson): a TimeSpan is a string in the .NET "c" format `[-][d.]hh:mm:ss[.fffffff]` ("00:01:30",
 * "1.02:03:04.5"), a Guid a lower case "D" string ("0f8fad5b-d9cb-469f-a165-70867728950e").
 *
 * TimeSpan editor: a text box that takes the "c" format or short units ("90s", "5m", "1h 30m", "2d", "1.5h", "250ms"; a plain
 * number is seconds) and stores the "c" format; the tool tip / the hint after the box reads the duration ("1 h 30 min").
 * Min / Max (EditRange / EditMin / EditMax with "c" strings) are checked. Guid editor: a monospace text box (validated) and a
 * "New" button (crypto.randomUUID); the member menu has "New id" too.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;

    /** Milliseconds per unit (short unit texts). */
    const Units = { ms: 1, s: 1000, sec: 1000, m: 60000, min: 60000, h: 3600000, hr: 3600000, d: 86400000, day: 86400000, days: 86400000, w: 604800000 };

    /**
     * TimeSpan handler.
     */
    class EditTypeTimeSpan extends E.Handler {
        /**
         * @param {boolean} [nullable] - System.Nullable`1[[System.TimeSpan]].
         */
        constructor(nullable) {
            super();
            /** @type {boolean} Nullable type (an empty text is null). */
            this.Nullable = !!nullable;
            /** @type {boolean} The type has no editable members (edited as one value, also as a collection item). */
            this.IgnoreMembers = true;
        }

        /**
         * Parse a duration text to milliseconds: the "c" format, unit texts ("1h 30m", "90s", "250ms") or a number of seconds.
         * @param {*} v - The text (or a number of milliseconds).
         * @returns {number} Milliseconds (NaN when invalid).
         */
        static Parse(v) {
            if (typeof v === "number")
                return v;
            const s = ("" + (v ?? "")).trim().toLowerCase();
            if (!s)
                return NaN;
            //  [-][d.]hh:mm[:ss[.fffffff]]
            const c = /^(-)?(?:(\d+)\.)?(\d+):(\d+)(?::(\d+)(?:\.(\d{1,7}))?)?$/.exec(s);
            if (c) {
                const ms = ((+(c[2] || 0) * 24 + +c[3]) * 60 + +c[4]) * 60000 + (+(c[5] || 0)) * 1000 + (c[6] ? parseFloat("0." + c[6]) * 1000 : 0);
                return c[1] ? -ms : ms;
            }
            if (/^-?\d+(\.\d+)?$/.test(s))
                return parseFloat(s) * 1000;
            const neg = s.startsWith("-");
            const rx = /(\d+(?:\.\d+)?)\s*([a-z]+)/g;
            let m, total = 0, any = false, rest = s.replace(/^-/, "");
            while ((m = rx.exec(rest))) {
                const u = Units[m[2]];
                if (!u)
                    return NaN;
                total += parseFloat(m[1]) * u;
                any = true;
            }
            if (!any || rest.replace(rx, "").trim())
                return NaN;
            return neg ? -total : total;
        }

        /**
         * Format milliseconds in the "c" format (trailing fraction zeros removed, like SwJson).
         * @param {number} ms - Milliseconds.
         * @returns {string} The text.
         */
        static Format(ms) {
            const neg = ms < 0;
            let t = Math.round(Math.abs(ms) * 10000);     // ticks
            const ticks = t % 10000000;
            t = Math.floor(t / 10000000);
            const sec = t % 60;
            t = Math.floor(t / 60);
            const min = t % 60;
            t = Math.floor(t / 60);
            const h = t % 24;
            const d = Math.floor(t / 24);
            const p2 = x => String(x).padStart(2, "0");
            let s = (neg ? "-" : "") + (d ? d + "." : "") + p2(h) + ":" + p2(min) + ":" + p2(sec);
            if (ticks)
                s += "." + String(ticks).padStart(7, "0").replace(/0+$/, "");
            return s;
        }

        /**
         * A readable form ("1 d 2 h 3 min 4.5 s", "0 s").
         * @param {number} ms - Milliseconds.
         * @returns {string} The text.
         */
        static Human(ms) {
            if (isNaN(ms))
                return "";
            const neg = ms < 0;
            let r = Math.abs(ms);
            const parts = [];
            const d = Math.floor(r / 86400000); r -= d * 86400000;
            const h = Math.floor(r / 3600000); r -= h * 3600000;
            const m = Math.floor(r / 60000); r -= m * 60000;
            const s = Math.round(r) / 1000;
            if (d)
                parts.push(d + " d");    //  SwWeb:allow ux-txt-20 (the duration box's own input syntax: Parse reads these units back)
            if (h)
                parts.push(h + " h");    //  SwWeb:allow ux-txt-20 (the duration box's own input syntax: Parse reads these units back)
            if (m)
                parts.push(m + " min");    //  SwWeb:allow ux-txt-20 (the duration box's own input syntax: Parse reads these units back)
            if (s || !parts.length)
                parts.push(s + " s");    //  SwWeb:allow ux-txt-20 (the duration box's own input syntax: Parse reads these units back)
            return (neg ? "-" : "") + parts.join(" ");
        }

        /**
         * Accept duration texts and numbers (milliseconds).
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj) {
            return (typeof obj === "string" || typeof obj === "number") && !isNaN(EditTypeTimeSpan.Parse(obj));
        }

        /**
         * Convert to the "c" format.
         * @param {*} obj - The value.
         * @returns {string} The text.
         */
        Condition(obj) {
            return EditTypeTimeSpan.Format(EditTypeTimeSpan.Parse(obj));
        }

        /**
         * Range of a member (milliseconds, NaN = none).
         * @param {object} member - The member.
         * @returns {{Min: number, Max: number}} The range.
         */
        static Range(member) {
            return { Min: Has(member.Min) ? EditTypeTimeSpan.Parse(member.Min) : NaN, Max: Has(member.Max) ? EditTypeTimeSpan.Parse(member.Max) : NaN };
        }

        /**
         * Validate (format and range; null only for nullable / AcceptNull).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            const dn = member.DisplayName || member.Name;
            if (obj === null || obj === undefined)
                return this.Nullable ? null : E.Handler.NullCheck(obj, member, name);
            const ms = EditTypeTimeSpan.Parse(obj);
            if (isNaN(ms))
                return [name, dn + ": " + _T("\"{0}\" is not a valid duration (ex: 00:01:30, 90s, 1h 30m)", obj, "Validation error of a duration value.{0} is replaced with the invalid value.")];
            const r = EditTypeTimeSpan.Range(member);
            if (!isNaN(r.Min) && ms < r.Min)
                return [name, dn + ": " + _T("The duration must be at least {0}", EditTypeTimeSpan.Human(r.Min), "Validation error of a too short duration.{0} is replaced with the minimum.")];
            if (!isNaN(r.Max) && ms > r.Max)
                return [name, dn + ": " + _T("The duration must be at most {0}", EditTypeTimeSpan.Human(r.Max), "Validation error of a too long duration.{0} is replaced with the maximum.")];
            return null;
        }

        /**
         * The default ("00:00:00", null for nullable / AcceptNull without a default).
         * @param {object} member - The member.
         * @returns {?string} The default.
         */
        GetDefault(member) {
            const d = member.Default;
            if (Has(d) && d !== "\t" && !isNaN(EditTypeTimeSpan.Parse(d)))
                return EditTypeTimeSpan.Format(EditTypeTimeSpan.Parse(d));
            if (this.Nullable || (member.Flags & F.AcceptNull) !== 0)
                return d === "\t" ? "00:00:00" : null;
            return "00:00:00";
        }

        /**
         * Display text (readable).
         * @param {*} obj - The value.
         * @returns {string} The text.
         */
        ToString(obj) {
            return obj === null || obj === undefined ? "null" : EditTypeTimeSpan.Human(EditTypeTimeSpan.Parse(obj));
        }

        /**
         * Build the duration text box and the readable hint.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item.
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const mn = member.Name;
            const readOnly = this.IsReadOnly(member, options);
            const nullOk = this.Nullable || (member.Flags & F.AcceptNull) !== 0;
            const def = this.GetDefault(member);
            if (obj[mn] === undefined || (obj[mn] === null && !nullOk))
                obj[mn] = def;
            const r = EditTypeTimeSpan.Range(member);
            let tip = E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), def === null ? "null" : def);
            if (!isNaN(r.Min))
                tip = E.Util.AddLine(tip, _TF("Minimum value", "Header of a tool tip row with the smallest allowed value"), EditTypeTimeSpan.Human(r.Min));
            if (!isNaN(r.Max))
                tip = E.Util.AddLine(tip, _TF("Maximum value", "Header of a tool tip row with the largest allowed value"), EditTypeTimeSpan.Human(r.Max));
            const hint = Sw.el("span.sw-edit-hint", { attrs: { "aria-live": "polite" } });
            /** @type {?{Value: string, Text: string}} A typed value that was out of range and set to the limit (shown until the value changes). */
            let clampNote = null;
            /**
             * The message of a typed duration outside Min / Max: the limit is used instead (typing commits at once), and the hint says so.
             * @param {number} ms - The typed duration.
             * @returns {?string} The message, null when inside the range.
             */
            const rangeNote = ms => {
                if (!isNaN(r.Min) && ms < r.Min)
                    return _T("Set to the minimum {0} ({1} is too short)", EditTypeTimeSpan.Human(r.Min), EditTypeTimeSpan.Human(ms), "Message after a duration box when the typed duration was below the minimum and the minimum was used.{0} is the minimum.{1} the typed duration");
                if (!isNaN(r.Max) && ms > r.Max)
                    return _T("Set to the maximum {0} ({1} is too long)", EditTypeTimeSpan.Human(r.Max), EditTypeTimeSpan.Human(ms), "Message after a duration box when the typed duration was above the maximum and the maximum was used.{0} is the maximum.{1} the typed duration");
                return null;
            };
            const text = E.CreateTextInput(() => obj[mn], async (v, ev, token) => {
                const old = obj[mn];
                if (old === v)
                    return;
                await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", v, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
            }, s => {
                s = ("" + s).trim();
                if (!(clampNote && clampNote.Value === s))
                    clampNote = null;
                if (!s)
                    return nullOk ? null : obj[mn];
                let ms = EditTypeTimeSpan.Parse(s);
                if (isNaN(ms))
                    return obj[mn];
                //  Out of range: the limit is used, and the hint says so (never clamped silently)
                const note = rangeNote(ms);
                if (!isNaN(r.Min))
                    ms = Math.max(ms, r.Min);
                if (!isNaN(r.Max))
                    ms = Math.min(ms, r.Max);
                const f = EditTypeTimeSpan.Format(ms);
                //  The clamped text itself (shown after Enter, parsed again when leaving the box) keeps the note
                if (note)
                    clampNote = { Value: f, Text: note };
                else if (!(clampNote && clampNote.Value === f))
                    clampNote = null;
                return f;
            }, v => v === null || v === undefined ? "" : String(v), _TF("Duration, ex: 00:01:30, 90s, 1h 30m, 2d", "Placeholder of the text box of a duration value"), null, null, 40, def, readOnly);
            text.classList.add("sw-edit-duration");
            text.setAttribute("aria-label", member.DisplayName || mn);
            const sync = () => {
                const v = obj[mn];
                const ms = v === null || v === undefined ? NaN : EditTypeTimeSpan.Parse(v);
                const note = clampNote && clampNote.Value === v ? clampNote.Text : null;
                hint.classList.toggle("sw-edit-hintwarn", !!note);
                hint.textContent = note || (isNaN(ms) ? "" : "= " + EditTypeTimeSpan.Human(ms));
                if (v === null && nullOk)
                    text.placeholder = "null";
            };
            text.addEventListener("input", () => {
                //  The text was just parsed (and committed) by the input binding: clampNote is the note of this text
                const s = text.value.trim();
                const ms = EditTypeTimeSpan.Parse(s);
                const bad = !!s && isNaN(ms);
                const out = !bad && clampNote ? clampNote.Text : null;
                text.classList.toggle("sw-invalid", bad);
                text.setAttribute("aria-invalid", bad ? "true" : "false");
                hint.classList.toggle("sw-edit-hintwarn", !!out);
                hint.textContent = bad ? _TF("not a duration", "Hint after the text box of a duration value when the text is not a duration") : out ? out : s ? "= " + EditTypeTimeSpan.Human(ms) : "";
            });
            text.addEventListener("blur", () => {
                text.classList.remove("sw-invalid");
                sync();
            });
            sync();
            editContext.Element.append(text, hint);
            return E.Handler.Item(obj, editor, member, {
                Focus: text, Title: tip, DefValue: def, DefValueText: def === null ? "null" : def,
                Update: () => {
                    text.UpdateValue();
                    sync();
                },
            });
        }
    }

    /** Guid text: 32 hex digits with dashes (8-4-4-4-12), braces / no dashes accepted on input. */
    const GuidRx = /^\{?([0-9a-f]{8})-?([0-9a-f]{4})-?([0-9a-f]{4})-?([0-9a-f]{4})-?([0-9a-f]{12})\}?$/i;

    /**
     * Guid handler.
     */
    class EditTypeGuid extends E.Handler {
        /**
         * @param {boolean} [nullable] - System.Nullable`1[[System.Guid]].
         */
        constructor(nullable) {
            super();
            /** @type {boolean} Nullable type. */
            this.Nullable = !!nullable;
            /** @type {boolean} Edited as one value. */
            this.IgnoreMembers = true;
        }

        /**
         * Normalize a guid text ("D" format, lower case) or null when invalid.
         * @param {*} v - The text.
         * @returns {?string} The guid.
         */
        static Norm(v) {
            const m = GuidRx.exec(("" + (v ?? "")).trim());
            return m ? m.slice(1).join("-").toLowerCase() : null;
        }

        /**
         * A new random guid.
         * @returns {string} The guid.
         */
        static New() {
            if (window.crypto && crypto.randomUUID)
                return crypto.randomUUID();
            const b = new Uint8Array(16);
            crypto.getRandomValues(b);
            b[6] = (b[6] & 15) | 64;
            b[8] = (b[8] & 63) | 128;
            const h = Array.from(b, x => x.toString(16).padStart(2, "0")).join("");
            return h.substring(0, 8) + "-" + h.substring(8, 12) + "-" + h.substring(12, 16) + "-" + h.substring(16, 20) + "-" + h.substring(20);
        }

        /**
         * Accept guid texts.
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj) {
            return typeof obj === "string" && !!EditTypeGuid.Norm(obj);
        }

        /**
         * Normalize.
         * @param {*} obj - The value.
         * @returns {string} The guid.
         */
        Condition(obj) {
            return EditTypeGuid.Norm(obj) || obj;
        }

        /**
         * Validate the format.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            if (obj === null || obj === undefined)
                return this.Nullable ? null : E.Handler.NullCheck(obj, member, name);
            return EditTypeGuid.Norm(obj) ? null : [name, (member.DisplayName || member.Name) + ": " + _T("\"{0}\" is not a valid id (guid)", obj, "Validation error of a guid value.{0} is replaced with the invalid value.")];
        }

        /**
         * The default (the empty guid; null for nullable / AcceptNull).
         * @param {object} member - The member.
         * @returns {?string} The default.
         */
        GetDefault(member) {
            const d = member.Default;
            if (Has(d) && EditTypeGuid.Norm(d))
                return EditTypeGuid.Norm(d);
            if (this.Nullable || (member.Flags & F.AcceptNull) !== 0)
                return null;
            return "00000000-0000-0000-0000-000000000000";
        }

        /**
         * Build the text box and the "New" button.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item.
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const mn = member.Name;
            const readOnly = this.IsReadOnly(member, options);
            const nullOk = this.Nullable || (member.Flags & F.AcceptNull) !== 0;
            const def = this.GetDefault(member);
            if (obj[mn] === undefined || (obj[mn] === null && !nullOk))
                obj[mn] = def;
            const set = (v, token) => {
                const old = obj[mn];
                if (old === v)
                    return Promise.resolve();
                return editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", v, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
            };
            const text = E.CreateTextInput(() => obj[mn], (v, ev, token) => set(v, token), s => {
                s = ("" + s).trim();
                if (!s)
                    return nullOk ? null : obj[mn];
                return EditTypeGuid.Norm(s) || obj[mn];
            }, v => v ?? "", "00000000-0000-0000-0000-000000000000", null, null, 38, def, readOnly);
            text.classList.add("sw-edit-guid");
            text.setAttribute("aria-label", member.DisplayName || mn);
            text.addEventListener("input", () => {
                const bad = !!text.value.trim() && !EditTypeGuid.Norm(text.value);
                text.classList.toggle("sw-invalid", bad);
                text.setAttribute("aria-invalid", bad ? "true" : "false");
            });
            text.addEventListener("blur", () => text.classList.remove("sw-invalid"));
            editContext.Element.append(text);
            const newId = () => set(EditTypeGuid.New());
            if (!readOnly)
                editContext.Element.append(new Sw.Button({
                    Small: true, Text: _TF("New", "Text of a button that creates a new random id (guid)"), Icon: "refresh",
                    Title: _TF("Create a new random id", "Tool tip of a button that creates a new random id (guid)"),
                    Label: _T("Create a new random id for {0}", member.DisplayName || mn, "Accessible name of the button that creates a new random id (guid).{0} is replaced with the name of the value"),
                    OnClick: newId,
                }).E);
            const item = E.Handler.Item(obj, editor, member, {
                Focus: text, Title: E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), def ?? "null"),
                DefValue: def, DefValueText: def ?? "null", Update: () => text.UpdateValue(),
            });
            if (!readOnly)
                item.AddMenuItems = menu => menu.Items.push({
                    Text: _TF("New id", "Text of a menu item that sets a new random id (guid)"), Icon: "refresh",
                    Title: _TF("Create a new random id", "Tool tip of a button that creates a new random id (guid)"), OnClick: newId,
                });
            return item;
        }
    }

    E.Handlers.TimeSpan = EditTypeTimeSpan;
    E.Handlers.Guid = EditTypeGuid;
    if (!window.EditTypeTimeSpan)
        window.EditTypeTimeSpan = EditTypeTimeSpan;
    if (!window.EditTypeGuid)
        window.EditTypeGuid = EditTypeGuid;
})();
