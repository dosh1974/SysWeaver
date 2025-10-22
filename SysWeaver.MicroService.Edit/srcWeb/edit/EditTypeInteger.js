class EditTypeInteger {

    constructor(signed, byteSize) {
        this.Signed = signed;
        this.ByteSize = byteSize;
        //  Type min and max
        let typeMax = Math.pow(2, byteSize * 8);
        let typeMin = 0;
        if (signed) {
            typeMax /= 2;
            typeMin = -typeMax;
            --typeMax;
        }
        this.TypeMin = typeMin;
        this.TypeMax = typeMax;
    }

    IsOfType(obj) {
        const t = typeof (obj);
        if (t !== "number") {
            if (t !== "string")
                return false;
            try {
                let res = parseInt(obj);
                if (isNaN(res)) {
                    res = parseFloat(obj);
                    if (isNaN(res))
                        return false;
                    res = Math.round(res) | 0;
                }
                obj = res;
            }
            catch (e)
            {
                return false;
            }
        }
        if (obj < this.TypeMin)
            return false;
        if (obj > this.TypeMax)
            return false;
        return true;
    }

    Condition(obj, member, options) {
        if (typeof (obj) == "string") {
            let res = parseInt(obj);
            obj = isNaN(res) ? parseFloat(obj) : res;
        }
        obj = Math.round(obj) | 0;
        const mmd = this.GetMinMaxDefault(member, options);
        const min = mmd[0];
        if (obj < min)
            obj = min;
        const max = mmd[1];
        if (obj > max)
            obj = max;
        return obj;
    }


    async Validate(obj, member, options, name) {
        const mmd = this.GetMinMaxDefault(member, options);
        const min = mmd[0];
        if (obj < min)
            return [name, member.DisplayName + ": Value to small! Smallest allowed value is " + min + ", current value is " + obj];
        const max = mmd[1];
        if (obj > max)
            return [name, member.DisplayName + ": Value to large! Largest allowed value is " + max + ", current value is " + obj];
        return null;
    }


    GetDefault(mmd) {
        const def = mmd[2];
        if (def)
            return def;
        const min = mmd[0];
        const max = mmd[1];
        let value = 0;
        if (value < min)
            value = min;
        if (value > max)
            value = max;
        return value;

    }

    CreateDefault(member, options) {
        return this.GetDefault(this.GetMinMaxDefault(member, options));
    }

    ToString(obj, member, options) {
        return "" + obj;
    }

    GetMinMaxDefault(member, options) {
        const typeMin = this.TypeMin;
        const typeMax = this.TypeMax;
        let typeTitle = null;
        //  Min
        let min = member.Min;
        if (Edit.IsValid(min))
            min = Math.trunc(parseFloat("" + min));
        const parsedMin = min;
        if (Edit.IsValid(min))
            min = min > typeMin ? min : typeMin;
        else
            min = typeMin;
        if (min > typeMax)
            min = typeMax;
        const haveMin = Edit.IsValid(parsedMin) && (parsedMin === min);
        if (haveMin || options.Dev)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Minimum value", min);
        //  Max
        let max = member.Max;
        if (Edit.IsValid(max))
            max = Math.trunc(parseFloat("" + max));
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
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Maximum value", max);
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            def = Math.trunc(parseFloat("" + def));
        const parsedDef = def;
        if (!def)
            def = 0;
        if (def < min)
            def = min;
        if (def > max)
            def = max;
        if ((Edit.IsValid(parsedDef) && (parsedDef === def)) || options.Dev)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", def);
        return [min, max, def, typeTitle, haveMin, haveMax, parsedDef];
    }


    async AddEditor(obj, editor, editContext, member, title, options) {

        let signed = this.Signed;
        const mn = member.Name;
        const cell = editContext.Element;


        const mmd = this.GetMinMaxDefault(member, options);
        const min = mmd[0];
        const max = mmd[1];
        const def = mmd[2];
        let typeTitle = mmd[3];
        const haveMin = mmd[4];
        const haveMax = mmd[5];

        const ot = typeof obj[mn];
        if ((ot === "undefined") || (ot === "object"))
            obj[mn] = this.GetDefault(mmd);
        let text = null;
        let slider = null;

        //  String length
        const fromText = v => {
            let val = Math.floor(parseFloat(v));
            if (!val)
                val = 0;
            if (val < min)
                val = min;
            if (val > max)
                val = max;
            return val;
        };

        signed &= (min < 0);
        const lenMin = ("" + min).length;
        const lenMax = ("" + max).length;
        const maxLen = lenMin > lenMax ? lenMin : lenMax;

        text = Edit.CreateTextInput(

            () => obj[mn],
            async v => {
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
                    "Set value: " + newValue,
                    mn,
                    null,
                    text
                );
                return true;
            },
            fromText,
            v => "" + v,
            member.Summary,
            v => v,
            (key, pos, val) => {
                if ((pos == 0) && signed && (key == '-'))
                    return true;
                return key >= '0' && key <= '9';
            },
            maxLen,
            def,
            options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0)
        );
        //text.style.textAlign = "right";
        text.style.maxWidth = (maxLen + 1) + "em";
        cell.appendChild(text);

        //  Use slider?
        if (haveMin && haveMax && ((member.Flags & TypeMemberFlags.Slider) != 0)) {
            let step = 0;
            let smin = 0;
            let smax = 0;
            const pt = member.EditParams;
            if (pt) {
                try {

                    step = parseInt(pt);
                    if ((!step) || (step <= 0)) {
                        console.warn('Invalid edit parameter "' + pt + '", expected an integer!');
                        step = 0;
                    } else {
                        smin = Math.floor(min / step);
                        smax = Math.floor(max / step);
                    }
                }
                catch (e)
                {
                    console.warn('Invalid edit parameter "' + pt + '", expected an integer, error: ' + e);
                }
            }
            if (step <= 0) {
                step = 1;
                for (; ;) {
                    smin = Math.floor(min / step);
                    smax = Math.floor(max / step);
                    const steps = smax - smin;
                    if (steps <= 100)
                        break;
                    step *= 10;
                }
            }
            const scount = smax - smin;
            slider = Edit.CreateSliderInput(
                () => {
                    let pos = Math.ceil(obj[mn] / step) - smin;
                    if (pos < 0)
                        pos = 0;
                    if (pos > scount)
                        pos = scount;
                    return pos;
                },
                async v => {
                    let val = (parseInt(v) + smin) * step;
                    if (val < min)
                        val = min;
                    if (val > max)
                        val = max;
                    const old = obj[mn];
                    const newValue = val;
                    if (old == newValue)
                        return false;
                    await editor.Invoke(
                        edit => {
                            edit.SetValue(newValue);
                        },
                        edit => {
                            edit.SetValue(old);
                        },
                        "Set value: " + newValue,
                        mn,
                        slider,
                        slider,
                    );
                    return true;
                },
                scount,
                options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0)
            );
            cell.classList.add("EditSlider");
            cell.appendChild(slider);
        }
        return {
            Focus: text,
            Title: typeTitle,
            DefValue: mmd[6],
            DefValueText: mmd[6],
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            UpdateValue: isFirst => {
                text.UpdateValue();
                if (slider) {
                    slider.UpdateValue();
                    if (document.activeElement == slider)
                        return;
                }
                if (!isFirst)
                    text.focus();
            },
        };
    }

}
