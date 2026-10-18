/**
 * @file EditTypeDateTime.js - type handler for System.DateTime, System.DateOnly and System.TimeOnly (and their Nullable variants) in the object editor.
 *
 * Loaded by Edit.Init() (Edit.js) via includeJs(current, "EditTypeDateTime.js").
 * Defines the global class EditTypeDateTime. Instances are created in Edit.GetTypeMap():
 * `new EditTypeDateTime(false)` (DateTime), `(false, true)` (DateOnly), `(false, false, true)` (TimeOnly); the
 * Nullable variants pass a 4th argument (true) that the constructor ignores.
 *
 * Value representations: values are either Date objects or strings (ISO strings for DateTime, "yyyy-mm-dd" for
 * DateOnly, "hh:mm:ss" for TimeOnly). Conversions keep the representation of the original value (a Date stays a
 * Date, a string becomes an ISO string via toISOString()). With TypeMemberFlags.DateUnspecified a date edit
 * produces "yyyy-mm-ddThh:mm:ss" without timezone conversion.
 *
 * Dependencies: Edit (IsValid, CreateGenericInput, CreateChangeEvent), ValueFormat.AddNonNullLine (common),
 * TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract; GetMinMax plays the role of GetMinMaxDefault.
 */

/**
 * Type handler for date/time values using native <input type=date> and/or <input type=time step=1>.
 */
class EditTypeDateTime {


    /** @type {Date} Smallest supported date (Unix epoch, 1970-01-01T00:00:00Z). */
    static MinDate = new Date(0);
    /** @type {Date} Largest date representable by JavaScript (8.64e15 ms after the epoch). */
    static MaxDate = new Date(8640000000000000);

    /**
     * Converts a value to a Date.
     * @param {*} obj - Date (returned as is), "\t" or "" (returns onDefault), or anything Date.parse accepts.
     * @param {*} onInvalid - Returned when Date.parse fails (or yields 0, i.e. the epoch itself).
     * @param {*} onDefault - Returned for "\t" (server marker for default) and "".
     * @returns {Date|*} The parsed Date or one of the fallbacks.
     */
    static ToDateTime(obj, onInvalid, onDefault) {
        if (obj instanceof Date)
            return obj;
        if ((obj === "\t") || (obj === ""))
            return onDefault;
        const t = Date.parse("" + obj);
        if (!t)
            obj = onInvalid;
        else
            obj = new Date(t);
        return obj;
    }


    /**
     * @param {boolean} [forceReadOnly=false] - Always create read-only editors.
     * @param {boolean} [onlyDate=false] - Date only (System.DateOnly): only a date input is shown.
     * @param {boolean} [onlyTime=false] - Time only (System.TimeOnly): only a time input is shown.
     */
    constructor(forceReadOnly, onlyDate, onlyTime) {
        /** @type {boolean} Forces read-only editors. */
        this.ForceReadOnly = !!forceReadOnly;
        /** @type {boolean} Handles date-only values. */
        this.OnlyDate = !!onlyDate;
        /** @type {boolean} Handles time-only values. */
        this.OnlyTime = !!onlyTime;
    }

    /**
     * Formats a time as "hh:mm:ss" (zero padded).
     * @param {number} h - Hours.
     * @param {number} m - Minutes.
     * @param {number} s - Seconds.
     * @returns {string} The time string.
     */
    static GetTimeString(h, m, s) {
        return ("" + h).padStart(2, '0') + ":" + ("" + m).padStart(2, '0') + ":" + ("" + s).padStart(2, '0');
    }
    /**
     * Formats a date as "yyyy-mm-dd" (zero padded).
     * @param {number} y - Year.
     * @param {number} m - Month (1-12).
     * @param {number} d - Day of month.
     * @returns {string} The date string.
     */
    static GetDateString(y, m, d) {
        return ("" + y).padStart(4, '0') + "-" + ("" + m).padStart(2, '0') + "-" + ("" + d).padStart(2, '0');
    }


    /**
     * Returns the local time part of a value as shown by <input type=time>.
     * @param {Date|string} v - Value (strings are parsed with Date.parse unless OnlyTime).
     * @param {boolean} unspecifiedDate - DateUnspecified flag (unused here).
     * @returns {?string} null for OnlyDate, v unchanged for OnlyTime, else local "hh:mm:ss".
     */
    GetLocalTimeString(v, unspecifiedDate) {
        if (this.OnlyDate)
            return null;
        if (this.OnlyTime)
            return v;
        if (!(v instanceof Date))
            v = new Date(Date.parse(v));
        return EditTypeDateTime.GetTimeString(v.getHours(), v.getMinutes(), v.getSeconds());
    }

    /**
     * Returns the local date part of a value as shown by <input type=date>.
     * @param {Date|string} v - Value.
     * @param {boolean} unspecifiedDate - DateUnspecified flag (unused here).
     * @returns {?string} null for OnlyTime, v unchanged for an OnlyDate string, else local "yyyy-mm-dd".
     */
    GetLocalDateString(v, unspecifiedDate) {
        if (this.OnlyTime)
            return null;
        const isDate = v instanceof Date;
        if (this.OnlyDate && (!isDate))
            return v;
        if (!isDate)
            v = new Date(Date.parse(v));
        return EditTypeDateTime.GetDateString(v.getFullYear(), v.getMonth() + 1, v.getDate());
    }

    /**
     * Combines the date of the original value with a new local time from a time input.
     * @param {Date|string} org - Current value.
     * @param {string} v - New time "hh[:mm[:ss]]".
     * @param {boolean} unspecifiedDate - DateUnspecified flag (unused here).
     * @returns {Date|string|null} null for OnlyDate, v for OnlyTime, a Date when org was a Date, else an ISO string.
     */
    SetLocalTimeString(org, v, unspecifiedDate) {
        if (this.OnlyDate)
            return null;
        if (this.OnlyTime)
            return v;
        const isDate = org instanceof Date;
        if (!isDate)
            org = new Date(Date.parse(org));
        const x = v.split(':');
        const sl = x.length;
        let h = 0;
        let m = 0;
        let s = 0;
        if (sl > 0)
            h = parseInt(x[0]);
        if (sl > 1)
            m = parseInt(x[1]);
        if (sl > 2)
            s = parseInt(x[2]);
        const n = new Date(org.getFullYear(), org.getMonth(), org.getDate(), h, m, s);
        if (isDate)
            return n;
        return n.toISOString();
    }


    /**
     * Combines a new local date from a date input with the time of the original value.
     * @param {Date|string} org - Current value.
     * @param {string} v - New date "yyyy-mm-dd".
     * @param {boolean} unspecifiedDate - When true returns "yyyy-mm-ddThh:mm:ss" (no timezone conversion).
     * @returns {Date|string|null} null for OnlyTime, v for an OnlyDate string value, the unspecified string,
     *   a Date when org was a Date, else an ISO string.
     */
    SetLocalDateString(org, v, unspecifiedDate) {
        if (this.OnlyTime)
            return null;
        const isDate = org instanceof Date;
        if (this.OnlyDate && (!isDate))
            return v;
        if (!isDate)
            org = new Date(Date.parse(org));
        const x = v.split('-');
        const sl = x.length;
        let y = 0;
        let m = 0;
        let d = 0;
        if (sl > 0)
            y = parseInt(x[0]);
        if (sl > 1)
            m = parseInt(x[1]);
        if (sl > 2)
            d = parseInt(x[2]);
        if (unspecifiedDate)
            return EditTypeDateTime.GetDateString(y, m, d) + "T" + EditTypeDateTime.GetTimeString(org.getHours(), org.getMinutes(), org.getSeconds());

        const n = new Date(y, m - 1, d, org.getHours(), org.getMinutes(), org.getSeconds());
        if (isDate)
            return n;
        return n.toISOString();
    }

    /**
     * Checks a "yyyy-mm-dd" string loosely (3 parts, numeric year, month 1-12, day 1-31). Not used in this file.
     * @param {Date|string} v - Value; a Date is always valid.
     * @returns {boolean} Whether the value looks like a valid date.
     */
    static IsValidDate(v) {
        if (v instanceof Date)
            return true;
        const x = v.split('-');
        const sl = x.length;
        if (sl !== 3)
            return false;
        const y = parseInt(x[0]);
        if ((!y) && (y !== 0))
            return false;
        const m = parseInt(x[1]);
        if (!m)
            return false;
        if ((m < 1) || (m > 12))
            return false;
        const d = parseInt(x[2]);
        if (!d)
            return false;
        if ((d < 1) || (d > 31))
            return false;
        return true;
    }


    /**
     * Checks a "hh[:mm[:ss]]" string loosely (hour 0-23, minutes and seconds 0-60). Not used in this file.
     * @param {Date|string} v - Value; a Date is always valid.
     * @returns {boolean} Whether the value looks like a valid time.
     */
    static IsValidTime(v) {
        if (v instanceof Date)
            return true;
        const x = v.split(':');
        const sl = x.length;
        if ((sl < 1) || (sl > 3))
            return false;
        const h = parseInt(x[0]);
        if ((!h) && (h !== 0))
            return false;
        if ((h < 0) || (h > 23))
            return false;
        if (sl > 1) {
            const m = parseInt(x[1]);
            if ((!m) && (m !== 0))
                return false;
            if ((m < 0) || (m > 60))
                return false;
        }
        if (sl > 2) {
            const m = parseInt(x[2]);
            if ((!m) && (m !== 0))
                return false;
            if ((m < 0) || (m > 60))
                return false;
        }
        return true;
    }


    /**
     * Creates the default value for a new member.
     * @param {object} member - Member description.
     * @param {EditOptions} options - Editor options.
     * @returns {Date|string} GetMinMax(...)[2].
     */
    CreateDefault(member, options) {
        return this.GetMinMax(member, options)[2];
    }

    /**
     * Checks whether a pasted / dropped value is a Date or a string Date.parse accepts.
     * @param {*} obj - Candidate value.
     * @returns {boolean} true when usable as a date/time.
     */
    IsOfType(obj) {
        if (obj instanceof Date)
            return true;
        if (typeof obj !== "string")
            return false;
        return !!Date.parse(obj);
    }

    /**
     * Converts a pasted / dropped value and clamps it to the member's min/max.
     * Date objects are clamped and returned as Dates; OnlyTime values are returned unchanged; strings are parsed
     * (MinDate when invalid), clamped and returned as local "yyyy-mm-dd" for OnlyDate, otherwise as an ISO string.
     * @param {Date|string} obj - Value to convert.
     * @param {object} member - Member description (Min, Max, Default, Flags).
     * @param {EditOptions} options - Editor options.
     * @returns {Date|string} The conditioned value.
     */
    Condition(obj, member, options) {
        const mmd = this.GetMinMax(member, options);
        const min = mmd[0];
        const max = mmd[1];
        if (obj instanceof Date) {
            if (this.OnlyTime)
                return obj;
            if (obj < min)
                obj = min;
            if (obj > max)
                obj = max;
            return obj;
        }
        if (this.OnlyTime)
            return obj;
        const t = Date.parse("" + obj);
        if (!t)
            obj = EditTypeDateTime.MinDate;
        else
            obj = new Date(t);
        if (obj < min)
            obj = min;
        if (obj > max)
            obj = max;
        const unspecifiedDate = (member.Flags & TypeMemberFlags.DateUnspecified) !== 0;
        if (this.OnlyDate)
            return this.GetLocalDateString(obj, unspecifiedDate);
        if (this.OnlyTime)
            return this.GetLocalTimeString(obj, unspecifiedDate);
        return obj.toISOString();
    }

    /**
     * Returns the local date of d at a whole hour.
     * @param {Date} d - Source date.
     * @param {number} [hour=12] - Hour to use (12 when missing).
     * @returns {Date} New Date at hour:00:00 local time.
     */
    static GetDate(d, hour) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), (!hour) && (hour !== 0) ? 12 : hour, 0, 0);
    }

    /**
     * Returns the start (00:00:00 local) of the day of d.
     * @param {Date} d - Source date.
     * @returns {Date} Start of the day.
     */
    static GetMinDate(d) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0);
    }

    /**
     * Returns the end (23:59:59.999 local) of the day of d.
     * @param {Date} d - Source date.
     * @returns {Date} End of the day.
     */
    static GetMaxDate(d) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999);
    }


    /**
     * Merges member.Min / Max / Default with the supported range and builds the tooltip text.
     * Min is expanded to the start of its day, Max to the end of its day, both clamped to MinDate..MaxDate.
     * The default is member.Default (ToDateTime, "\t" = now) or now rounded down to the current hour, clamped to
     * [min, max]; when member.Default is a string the returned default is converted to an ISO string.
     * Tooltip lines are added when the parsed member value survived clamping unchanged (identity compare, so
     * effectively only for unclamped values) or always when options.Dev is set.
     * @param {object} member - Member description (Min, Max, Default, Flags).
     * @param {EditOptions} options - Editor options (Dev).
     * @returns {Array} [min, max, def, typeTitle, haveMin, haveMax, parsedDef, defText].
     */
    GetMinMax(member, options) {
        const typeMin = EditTypeDateTime.MinDate;
        const typeMax = EditTypeDateTime.MaxDate;
        let defNow = new Date();
        defNow = EditTypeDateTime.GetDate(defNow, defNow.getHours());
        let typeTitle = null;
        //  Min
        let min = member.Min;
        if (Edit.IsValid(min))
            min = EditTypeDateTime.GetMinDate(EditTypeDateTime.ToDateTime(min, typeMin, defNow));
        const parsedMin = min;
        if (Edit.IsValid(min))
            min = min > typeMin ? min : typeMin;
        else
            min = typeMin;
        if (min > typeMax)
            min = typeMax;
        const haveMin = Edit.IsValid(parsedMin) && (parsedMin === min);
        const unspecifiedDate = (member.Flags & TypeMemberFlags.DateUnspecified) !== 0;
        if (haveMin || options.Dev)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Minimum value", this.GetLocalDateString(min, unspecifiedDate));
        //  Max
        let max = member.Max;
        if (Edit.IsValid(max))
            max = EditTypeDateTime.GetMaxDate(EditTypeDateTime.ToDateTime(max, typeMax, defNow));
        const parsedMax = max;
        if (Edit.IsValid(max))
            max = max < typeMax ? max : typeMax;
        else
            max = typeMax;
        if (max < typeMin)
            max = typeMin;
        if (max < min)
            max = min;
        const haveMax = Edit.IsValid(parsedMax) && (parsedMax === max);
        if (haveMax || options.Dev)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Maximum value", this.GetLocalDateString(max, unspecifiedDate));
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            def = EditTypeDateTime.ToDateTime(def, defNow, defNow);
        const parsedDef = def;
        if (!def)
            def = defNow;
        if (def < min)
            def = min;
        if (def > max)
            def = max;
        const defText = this.ToString(def, member, options);
        if ((Edit.IsValid(parsedDef) && (parsedDef === def)) || options.Dev)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", defText);

        if ((typeof member.Default === "string") && (def instanceof Date))
            def = def.toISOString();
        return [min, max, def, typeTitle, haveMin, haveMax, parsedDef, defText];
    }


    /**
     * Validates a value: must be a Date or a string (OnlyTime strings are always accepted), strings must parse
     * with Date.parse, and the value must be within [min, max] (values below min are accepted with AcceptNull).
     * For OnlyDate out-of-range values the message shows the limit as a local date (`GetLocalDateString`).
     * @param {Date|string} obj - Value to validate.
     * @param {object} member - Member description (Flags, DisplayName, Min, Max).
     * @param {EditOptions} options - Editor options.
     * @param {string} name - Member path/name returned with the error.
     * @returns {Promise<?Array<string>>} null when valid, otherwise [name, message].
     */
    async Validate(obj, member, options, name) {
        const isDate = obj instanceof Date;
        if (!isDate) {
            if (typeof (obj) !== "string")
                return [name, member.DisplayName + ": Value must be a string"];
            if (this.OnlyTime)
                return null;
            const t = Date.parse(obj);
            if (!t)
                return [name, member.DisplayName + ": Value must be a valid date / time string"];
            obj = new Date(t);
        }
        const mmd = this.GetMinMax(member, options);
        const min = mmd[0];
        const max = mmd[1];
        const unspecifiedDate = (member.Flags & TypeMemberFlags.DateUnspecified) !== 0;
        if (obj < min) {
            if ((member.Flags & TypeMemberFlags.AcceptNull) === 0)
                return this.OnlyDate
                    ?
                    [name, member.DisplayName + ": The date may not be earlier than " + this.GetLocalDateString(min, unspecifiedDate)]
                    :
                    [name, member.DisplayName + ": The time may not be earlier than " + min.toISOString()];
        }
        if (obj > max)
            return this.OnlyDate
                ?
                [name, member.DisplayName + ": The date may not be later than " + this.GetLocalDateString(max, unspecifiedDate)]
                :
                [name, member.DisplayName + ": The time may not be later than " + max.toISOString()];
        return null;
    }


    /**
     * Formats a value for display: local "yyyy-mm-dd hh:mm:ss", or only the date / time part for OnlyDate / OnlyTime.
     * @param {Date|string} obj - Value.
     * @param {object} member - Member description (Flags).
     * @param {EditOptions} [options] - Editor options (unused).
     * @returns {string} Display text.
     */
    ToString(obj, member, options) {
        const haveBoth = !(this.OnlyDate || this.OnlyTime);
        const unspecifiedDate = (member.Flags & TypeMemberFlags.DateUnspecified) !== 0;
        const defDate = this.GetLocalDateString(obj, unspecifiedDate);
        const defTime = this.GetLocalTimeString(obj, unspecifiedDate);
        const defText = haveBoth ? (defDate + " " + defTime) : (this.OnlyDate ? defDate : defTime);
        return defText;
    }


    /**
     * Builds the date/time editor in editContext.Element: an <input type=date> (unless OnlyTime, with min/max when
     * the member specifies them) and/or an <input type=time step=1> (unless OnlyDate), created with
     * Edit.CreateGenericInput. The date input gets HalfWidth when both are shown; the time input always gets
     * HalfWidth. obj[member.Name] is set to the default when undefined.
     * Read only when options.ReadOnly, the member has TypeMemberFlags.ReadOnly, or ForceReadOnly.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - Owning editor (Invoke, Element, Key, KeyName).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Min, Max, Default, Flags).
     * @param {string} title - Member title (unused).
     * @param {EditOptions} options - Editor options (ReadOnly, Dev).
     * @returns {Promise<object>} Item {Focus (date or time input), Title, DefValue, DefValueText, SetValue(v),
     *   UpdateValue(isFirst)}; SetValue writes obj[mn] and dispatches EditChange on editor.Element.
     */
    async AddEditor(obj, editor, editContext, member, title, options) {
        const th = this;
        const mn = member.Name;
        const cell = editContext.Element;
        const readOnly = options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) !== 0) || th.ForceReadOnly;
        const unspecifiedDate = (member.Flags & TypeMemberFlags.DateUnspecified) !== 0;
        const mmd = this.GetMinMax(member, options);
        const min = mmd[0];
        const max = mmd[1];
        let def = mmd[2];

        let typeTitle = mmd[3];
        const haveMin = mmd[4];
        const haveMax = mmd[5];

        const defText = mmd[7];

        const haveBoth = !(th.OnlyDate || th.OnlyTime);

        if (typeof obj[mn] === "undefined")
            obj[mn] = def;

        


        let dateInp = null;
        let timeInp = null;


        /**
         * Applies a new value from either input through editor.Invoke (undoable, title shows the formatted and raw value).
         * @param {Date|string} v - New value (already converted by SetLocalDateString / SetLocalTimeString).
         * @returns {Promise<boolean>} false when unchanged (===), true after invoking the change.
         */
        async function setValue(v) {
            const old = obj[mn];
            if (old === v)
                return false;
            const newValue = v;
            await editor.Invoke(
                edit => {
                    edit.SetValue(newValue);
                },
                edit => {
                    edit.SetValue(old);
                },
                "Set value: \"" + th.ToString(newValue, member, options) + "\" [" + newValue + "]",
                mn
            );
            return true;
        }

        if (!th.OnlyTime) {
            dateInp = Edit.CreateGenericInput("date",
                () => obj[mn],
                setValue,
                v => th.SetLocalDateString(obj[mn], v, unspecifiedDate),
                v => th.GetLocalDateString(v, unspecifiedDate),
                null,
                readOnly
            );
            if (haveMin)
                dateInp.min = this.GetLocalDateString(min, unspecifiedDate);
            if (haveMax)
                dateInp.max = this.GetLocalDateString(max, unspecifiedDate);
            if (haveBoth)
                dateInp.classList.add("HalfWidth");
            cell.appendChild(dateInp);
        }
        if (!th.OnlyDate) {
            timeInp = Edit.CreateGenericInput("time",
                () => obj[mn],
                setValue,
                v => th.SetLocalTimeString(obj[mn], v, unspecifiedDate),
                v => th.GetLocalTimeString(v, unspecifiedDate),
                null,
                readOnly
            );
            timeInp.setAttribute("step", "1");
            if (timeInp)
                timeInp.classList.add("HalfWidth");
            cell.appendChild(timeInp);
        }
        const foc = dateInp ?? timeInp;
        return {
            Focus: foc,
            Title: typeTitle,
            DefValue: def,
            DefValueText: defText,
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            UpdateValue: isFirst => {

                if (dateInp)
                    dateInp.UpdateValue();
                if (timeInp)
                    timeInp.UpdateValue();
                if (!isFirst)
                    foc.focus();
            },
        };
    }

}
