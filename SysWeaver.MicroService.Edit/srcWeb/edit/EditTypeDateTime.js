/**
 * @file Type handler for DateTime, DateOnly and TimeOnly members (and their nullable variants) (`Sw.Edit.Handlers.DateTime`,
 * legacy global `EditTypeDateTime`).
 *
 * Loaded by Edit.js. Native date and time inputs (time with seconds), side by side on wide editors and stacked on narrow
 * ones. The inputs show local time; DateTime values are stored as ISO strings (or Date objects when the value was a Date),
 * with the DateUnspecified flag as `yyyy-mm-ddThh:mm:ss` without time zone conversion; DateOnly as `yyyy-mm-dd`, TimeOnly
 * as `hh:mm:ss`. Nullable types (and AcceptNull members) accept an empty input as null.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;
    const addLine = E.Util.AddLine;
    const pad = (v, n) => ("" + v).padStart(n || 2, "0");

    /**
     * Date / time handler.
     */
    class EditTypeDateTime extends E.Handler {
        /**
         * @param {boolean} [forceReadOnly] - Always read only.
         * @param {boolean} [onlyDate] - DateOnly.
         * @param {boolean} [onlyTime] - TimeOnly.
         * @param {boolean} [nullable] - Nullable variant.
         */
        constructor(forceReadOnly, onlyDate, onlyTime, nullable) {
            super();
            /** @type {boolean} Always read only. */
            this.ForceReadOnly = !!forceReadOnly;
            /** @type {boolean} DateOnly. */
            this.OnlyDate = !!onlyDate;
            /** @type {boolean} TimeOnly. */
            this.OnlyTime = !!onlyTime;
            /** @type {boolean} Nullable. */
            this.Nullable = !!nullable;
        }

        /**
         * Time text.
         * @param {number} h - Hours.
         * @param {number} m - Minutes.
         * @param {number} s - Seconds.
         * @returns {string} "hh:mm:ss".
         */
        static GetTimeString(h, m, s) {
            return pad(h) + ":" + pad(m) + ":" + pad(s);
        }

        /**
         * Date text.
         * @param {number} y - Year.
         * @param {number} m - Month (1-12).
         * @param {number} d - Day.
         * @returns {string} "yyyy-mm-dd".
         */
        static GetDateString(y, m, d) {
            return pad(y, 4) + "-" + pad(m) + "-" + pad(d);
        }

        /**
         * Convert a value to a Date (date only / time only texts are read as local times).
         * @param {*} v - The value.
         * @returns {?Date} The date (null if not set / invalid).
         */
        ToDate(v) {
            if (v === null || v === undefined || v === "" || v === "\t")
                return null;
            if (v instanceof Date)
                return isNaN(v.getTime()) ? null : v;
            const s = "" + v;
            if (this.OnlyTime || /^\d{1,2}:\d{2}/.test(s)) {
                const p = s.split(":").map(x => parseInt(x) || 0);
                return new Date(1970, 0, 1, p[0] || 0, p[1] || 0, p[2] || 0);
            }
            if (/^\d{4}-\d{2}-\d{2}$/.test(s)) {
                const p = s.split("-").map(x => parseInt(x));
                return new Date(p[0], p[1] - 1, p[2]);
            }
            const t = Date.parse(s);
            return isNaN(t) ? null : new Date(t);
        }

        /**
         * Test if null is a valid value.
         * @param {object} member - The member.
         * @returns {boolean} True for nullable types / AcceptNull.
         */
        AcceptsNull(member) {
            return this.Nullable || (member.Flags & F.AcceptNull) !== 0;
        }

        /**
         * Local date text of a value (input value of the date input).
         * @param {*} v - The value.
         * @param {boolean} [unspecified] - DateUnspecified (texts are used as is).
         * @returns {?string} "yyyy-mm-dd", "" for null, or null for TimeOnly.
         */
        GetLocalDateString(v, unspecified) {
            if (this.OnlyTime)
                return null;
            if (typeof v === "string" && (this.OnlyDate || unspecified) && /^\d{4}-\d{2}-\d{2}/.test(v))
                return v.substring(0, 10);
            const d = this.ToDate(v);
            return d ? EditTypeDateTime.GetDateString(d.getFullYear(), d.getMonth() + 1, d.getDate()) : "";
        }

        /**
         * Local time text of a value (input value of the time input).
         * @param {*} v - The value.
         * @param {boolean} [unspecified] - DateUnspecified.
         * @returns {?string} "hh:mm:ss", "" for null, or null for DateOnly.
         */
        GetLocalTimeString(v, unspecified) {
            if (this.OnlyDate)
                return null;
            if (typeof v === "string" && unspecified && /T\d{2}:\d{2}/.test(v))
                return v.substring(11, 19);
            const d = this.ToDate(v);
            return d ? EditTypeDateTime.GetTimeString(d.getHours(), d.getMinutes(), d.getSeconds()) : "";
        }

        /**
         * Store a value from a Date in the format of the original value.
         * @param {Date} d - The new date (local).
         * @param {*} org - The original value (a Date keeps Date objects).
         * @param {boolean} unspecified - DateUnspecified.
         * @returns {*} The value to store.
         */
        FromDate(d, org, unspecified) {
            if (this.OnlyTime)
                return EditTypeDateTime.GetTimeString(d.getHours(), d.getMinutes(), d.getSeconds());
            if (this.OnlyDate)
                return EditTypeDateTime.GetDateString(d.getFullYear(), d.getMonth() + 1, d.getDate());
            if (org instanceof Date)
                return d;
            if (unspecified)
                return EditTypeDateTime.GetDateString(d.getFullYear(), d.getMonth() + 1, d.getDate()) + "T" + EditTypeDateTime.GetTimeString(d.getHours(), d.getMinutes(), d.getSeconds());
            return d.toISOString();
        }

        /**
         * Apply a date input text to a value (keeps the time part).
         * @param {*} org - Current value.
         * @param {string} v - "yyyy-mm-dd" ("" = null when allowed).
         * @param {boolean} unspecified - DateUnspecified.
         * @returns {*} The new value (undefined = invalid / no change).
         */
        SetLocalDateString(org, v, unspecified) {
            if (!v)
                return null;
            const p = v.split("-").map(x => parseInt(x));
            if (p.length !== 3 || p.some(x => isNaN(x)))
                return undefined;
            const o = this.ToDate(org) || this.ToDate(this.Now());
            return this.FromDate(new Date(p[0], p[1] - 1, p[2], o.getHours(), o.getMinutes(), o.getSeconds()), org, unspecified);
        }

        /**
         * Apply a time input text to a value (keeps the date part).
         * @param {*} org - Current value.
         * @param {string} v - "hh:mm[:ss]" ("" = null when allowed).
         * @param {boolean} unspecified - DateUnspecified.
         * @returns {*} The new value.
         */
        SetLocalTimeString(org, v, unspecified) {
            if (!v)
                return null;
            const p = v.split(":").map(x => parseInt(x) || 0);
            const o = this.ToDate(org) || this.ToDate(this.Now());
            return this.FromDate(new Date(o.getFullYear(), o.getMonth(), o.getDate(), p[0] || 0, p[1] || 0, p[2] || 0), org, unspecified);
        }

        /**
         * Now rounded to the hour.
         * @returns {Date} The date.
         */
        Now() {
            const n = new Date();
            return new Date(n.getFullYear(), n.getMonth(), n.getDate(), n.getHours(), 0, 0);
        }

        /**
         * Range and default of a member: Min = start of its day, Max = end of its day; default = metadata, else now rounded to
         * the hour (clamped).
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {{Min: Date, Max: Date, Def: *, DefText: string, Title: ?string, HaveMin: boolean, HaveMax: boolean}} The info.
         */
        Range(member, options) {
            const tmin = new Date(-62135596800000), tmax = new Date(253402300799000);
            const unspecified = (member.Flags & F.DateUnspecified) !== 0;
            let title = null;
            let min = Has(member.Min) ? this.ToDate(member.Min) : null;
            let max = Has(member.Max) ? this.ToDate(member.Max) : null;
            const haveMin = !!min, haveMax = !!max;
            min = min ? new Date(min.getFullYear(), min.getMonth(), min.getDate()) : tmin;
            max = max ? new Date(max.getFullYear(), max.getMonth(), max.getDate(), 23, 59, 59, 999) : tmax;
            if (max < min)
                max = min;
            const dev = options && options.Dev;
            if (!this.OnlyTime) {
                if (haveMin || dev)
                    title = addLine(title, _TF("Minimum value", "Header of a tool tip row with the smallest allowed value"), this.GetLocalDateString(min));
                if (haveMax || dev)
                    title = addLine(title, _TF("Maximum value", "Header of a tool tip row with the largest allowed value"), this.GetLocalDateString(max));
            }
            const pdef = Has(member.Default) && member.Default !== "\t" ? this.ToDate(member.Default) : null;
            let d = pdef || this.Now();
            if (!this.OnlyTime) {
                if (d < min)
                    d = min;
                if (d > max)
                    d = max;
            }
            const def = this.FromDate(d, typeof member.Default === "string" ? "" : d, unspecified);
            const defText = this.ToString(def, member);
            if (pdef || dev)
                title = addLine(title, _TF("Default value", "Header of a tool tip row with the default value of a property"), defText);
            return { Min: min, Max: max, Def: def, DefText: defText, Title: title, HaveMin: haveMin, HaveMax: haveMax, HaveDef: !!pdef };
        }

        /**
         * The default value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {*} The default.
         */
        GetDefault(member, options) {
            return this.Range(member, options || {}).Def;
        }

        /**
         * Old name of the range info: [min, max, def, title, haveMin, haveMax, parsedDef, defText].
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {Array} The info.
         */
        GetMinMax(member, options) {
            const r = this.Range(member, options || {});
            return [r.Min, r.Max, r.Def, r.Title, r.HaveMin, r.HaveMax, r.HaveDef ? r.Def : undefined, r.DefText];
        }

        /**
         * Accept Dates and parseable texts.
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj) {
            if (obj instanceof Date)
                return !isNaN(obj.getTime());
            return typeof obj === "string" && !!this.ToDate(obj);
        }

        /**
         * Convert an accepted value (clamped to the range; unparsable -> the minimum).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {*} The value.
         */
        Condition(obj, member, options) {
            const r = this.Range(member, options || {});
            let d = this.ToDate(obj) || r.Min;
            if (!this.OnlyTime) {
                if (d < r.Min)
                    d = r.Min;
                if (d > r.Max)
                    d = r.Max;
            }
            return this.FromDate(d, obj, (member.Flags & F.DateUnspecified) !== 0);
        }

        /**
         * Validate: a date / time text inside the range (an earlier date is allowed with AcceptNull).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            const dn = member.DisplayName || member.Name;
            if (obj === null || obj === undefined)
                return this.AcceptsNull(member) ? null : E.Handler.NullCheck(obj, member, name);
            if (!(obj instanceof Date) && typeof obj !== "string")
                return [name, dn + ": " + _TF("Value must be a string", "Validation error of a date / time value that isn't a text")];
            const d = this.ToDate(obj);
            if (!d)
                return [name, dn + ": " + _TF("Value must be a valid date / time string", "Validation error of a date / time value that can't be parsed")];
            if (this.OnlyTime)
                return null;
            const r = this.Range(member, options);
            if (d < r.Min && (member.Flags & F.AcceptNull) === 0)
                return [name, dn + ": " + _T("The date may not be earlier than {0}", this.GetLocalDateString(r.Min), "Validation error of a date that is too early.{0} is replaced with the first allowed date.")];
            if (d > r.Max)
                return [name, dn + ": " + _T("The date may not be later than {0}", this.GetLocalDateString(r.Max), "Validation error of a date that is too late.{0} is replaced with the last allowed date.")];
            return null;
        }

        /**
         * Display text (local).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @returns {string} "yyyy-mm-dd hh:mm:ss", the date or the time.
         */
        ToString(obj, member) {
            if (obj === null || obj === undefined)
                return "null";
            const u = member && (member.Flags & F.DateUnspecified) !== 0;
            if (this.OnlyDate)
                return this.GetLocalDateString(obj, u);
            if (this.OnlyTime)
                return this.GetLocalTimeString(obj, u);
            return this.GetLocalDateString(obj, u) + " " + this.GetLocalTimeString(obj, u);
        }

        /**
         * Build the date and / or time inputs.
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
            const mn = member.Name;
            const cell = editContext.Element;
            const readOnly = this.IsReadOnly(member, options);
            const unspecified = (member.Flags & F.DateUnspecified) !== 0;
            const r = this.Range(member, options);
            const nullOk = this.AcceptsNull(member);
            if (obj[mn] === undefined || (obj[mn] === null && !nullOk))
                obj[mn] = r.Def;
            const commit = async v => {
                if (v === undefined || (v === null && !nullOk))
                    return;
                const old = obj[mn];
                if (old === v || (old instanceof Date && v instanceof Date && old.getTime() === v.getTime()))
                    return;
                await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", t.ToString(v, member), "Command text stored in a log when a property value was changed.{0} is replaced with the new value."));
            };
            const wrap = Sw.el("div.sw-edit-datetime");
            let dateInp = null, timeInp = null;
            if (!this.OnlyTime) {
                dateInp = E.CreateGenericInput("date", () => obj[mn], commit, v => t.SetLocalDateString(obj[mn], v, unspecified), v => t.GetLocalDateString(v, unspecified), null, readOnly);
                if (r.HaveMin)
                    dateInp.min = this.GetLocalDateString(r.Min);
                if (r.HaveMax)
                    dateInp.max = this.GetLocalDateString(r.Max);
                dateInp.setAttribute("aria-label", (member.DisplayName || mn) + " (" + _TF("date", "Accessible name suffix of a date input") + ")");
                wrap.append(dateInp);
            }
            if (!this.OnlyDate) {
                timeInp = E.CreateGenericInput("time", () => obj[mn], commit, v => t.SetLocalTimeString(obj[mn], v, unspecified), v => t.GetLocalTimeString(v, unspecified), null, readOnly);
                timeInp.step = 1;
                timeInp.setAttribute("aria-label", (member.DisplayName || mn) + " (" + _TF("time", "Accessible name suffix of a time input") + ")");
                wrap.append(timeInp);
            }
            if (dateInp && timeInp)
                wrap.classList.add("sw-edit-datetime-both");
            if (dateInp)
                dateInp.classList.add("HalfWidth");
            if (timeInp)
                timeInp.classList.add("HalfWidth");
            cell.append(wrap);
            return E.Handler.Item(obj, editor, member, {
                Focus: dateInp || timeInp, Title: r.Title, DefValue: r.Def, DefValueText: r.DefText,
                Update: () => {
                    if (dateInp)
                        dateInp.UpdateValue();
                    if (timeInp)
                        timeInp.UpdateValue();
                },
            });
        }
    }

    E.Handlers.DateTime = EditTypeDateTime;
    if (!window.EditTypeDateTime)
        window.EditTypeDateTime = EditTypeDateTime;
})();
