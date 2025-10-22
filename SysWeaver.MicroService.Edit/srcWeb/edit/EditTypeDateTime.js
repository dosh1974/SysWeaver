class EditTypeDateTime {


    static MinDate = new Date(0);
    static MaxDate = new Date(8640000000000000);

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


    constructor(forceReadOnly, onlyDate, onlyTime) {
        this.ForceReadOnly = !!forceReadOnly;
        this.OnlyDate = !!onlyDate;
        this.OnlyTime = !!onlyTime;
    }

    static GetTimeString(h, m, s) {
        return ("" + h).padStart(2, '0') + ":" + ("" + m).padStart(2, '0') + ":" + ("" + s).padStart(2, '0');
    }
    static GetDateString(y, m, d) {
        return ("" + y).padStart(4, '0') + "-" + ("" + m).padStart(2, '0') + "-" + ("" + d).padStart(2, '0');
    }


    GetLocalTimeString(v, unspecifiedDate) {
        if (this.OnlyDate)
            return null;
        if (this.OnlyTime)
            return v;
        if (!(v instanceof Date))
            v = new Date(Date.parse(v));
        return EditTypeDateTime.GetTimeString(v.getHours(), v.getMinutes(), v.getSeconds());
    }

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


    CreateDefault(member, options) {
        return this.GetMinMax(member, options)[2];
    }

    IsOfType(obj) {
        if (obj instanceof Date)
            return true;
        if (typeof obj !== "string")
            return false;
        return !!Date.parse(obj);
    }

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

    static GetDate(d, hour) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), (!hour) && (hour !== 0) ? 12 : hour, 0, 0);
    }

    static GetMinDate(d) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0);
    }

    static GetMaxDate(d) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999);
    }


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


    ToString(obj, member, options) {
        const haveBoth = !(this.OnlyDate || this.OnlyTime);
        const unspecifiedDate = (member.Flags & TypeMemberFlags.DateUnspecified) !== 0;
        const defDate = this.GetLocalDateString(obj, unspecifiedDate);
        const defTime = this.GetLocalTimeString(obj, unspecifiedDate);
        const defText = haveBoth ? (defDate + " " + defTime) : (this.OnlyDate ? defDate : defTime);
        return defText;
    }


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
