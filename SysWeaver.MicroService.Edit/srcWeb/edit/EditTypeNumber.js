/**
 * @file EditTypeNumber.js - type handler for floating point types (Single, Double, Decimal and their Nullable variants) of the object editor.
 *
 * Loaded by Edit.Init() (Edit.js) via includeJs(current, "EditTypeNumber.js").
 * Defines the global class EditTypeNumber. Instances are created in Edit.GetTypeMap(), e.g.
 * `new EditTypeNumber(-3.40282347e+38, 3.40282347e+38, 11)` for System.Single; the Nullable variants pass a 4th
 * argument (true) that the constructor ignores.
 *
 * Dependencies: Edit (IsValid, CreateTextInput, CreateSliderInput, CreateChangeEvent), ValueFormat.AddNonNullLine
 * (common), TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract, but GetDefault takes the array returned by
 * GetMinMaxDefault. Metadata values (Min/Max/Default) may use "," as decimal separator (the first "," is replaced by ".").
 */

/**
 * Type handler for floating point values: a numeric text input (digits, one ".", leading "-" when min < 0) plus an
 * optional range slider (Slider flag with both Min and Max; cell class EditSlider).
 */
class EditTypeNumber {

    /**
     * @param {number} typeMin - Smallest value of the .NET type.
     * @param {number} typeMax - Largest value of the .NET type.
     * @param {number} maxLen - Maximum number of characters in the text input (reduced by one for unsigned ranges).
     */
    constructor(typeMin, typeMax, maxLen) {
        /** @type {number} Type minimum. */
        this.TypeMin = typeMin;
        /** @type {number} Type maximum. */
        this.TypeMax = typeMax;
        /** @type {number} Max text length. */
        this.MaxLen = maxLen;
    }

    /**
     * Checks whether a pasted / dropped value is a number (or a string parseFloat accepts) within TypeMin..TypeMax.
     * @param {*} obj - Candidate value.
     * @returns {boolean} true when numeric and within the type range.
     */
    IsOfType(obj) {
        const t = typeof (obj);
        if (t !== "number") {
            if (t !== "string")
                return false;
            try {
                obj = parseFloat(obj);
                if (isNaN(obj))
                    return false;
            }
            catch (e) {
                return false;
            }
        }
        if (obj < this.TypeMin)
            return false;
        if (obj > this.TypeMax)
            return false;
        return true;
    }

    /**
     * Converts a pasted / dropped value (parseFloat for strings) and clamps it to the member's min/max.
     * @param {number|string} obj - Value to convert.
     * @param {object} member - Member description (Min, Max, Default).
     * @param {EditOptions} options - Editor options.
     * @returns {number} The conditioned number.
     */
    Condition(obj, member, options) {
        if (typeof (obj) == "string")
            obj = parseFloat(obj);
        const mmd = this.GetMinMaxDefault(member, options);
        const min = mmd[0];
        if (obj < min)
            obj = min;
        const max = mmd[1];
        if (obj > max)
            obj = max;
        return obj;
    }

    /**
     * Validates that a value is within the effective min/max of the member (no type or null check).
     * @param {number} obj - Value to validate.
     * @param {object} member - Member description (Min, Max, Default, DisplayName).
     * @param {EditOptions} options - Editor options.
     * @param {string} name - Member path/name returned with the error.
     * @returns {Promise<?Array<string>>} null when valid, otherwise [name, message].
     */
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

    /**
     * Picks the default value from a GetMinMaxDefault result.
     * @param {Array} mmd - Result of GetMinMaxDefault: [min, max, def, ...].
     * @returns {number} mmd[2] when truthy, otherwise 0 clamped to [min, max].
     */
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

    /**
     * Creates the default value for a new member.
     * @param {object} member - Member description.
     * @param {EditOptions} options - Editor options.
     * @returns {number} GetDefault(GetMinMaxDefault(member, options)).
     */
    CreateDefault(member, options) {
        return this.GetDefault(this.GetMinMaxDefault(member, options));
    }

    /**
     * Formats a value for display.
     * @param {*} obj - Value.
     * @param {object} member - Member description (unused).
     * @param {EditOptions} options - Editor options (unused).
     * @returns {string} "" + obj.
     */
    ToString(obj, member, options) {
        return "" + obj;
    }


    /**
     * Merges member.Min / Max / Default (parseFloat after replacing the first "," with ".") with the type range and
     * builds the tooltip text. Min/Max are clamped to the type range (max raised to at least min); the default is
     * clamped to [min, max] (falsy becomes 0). Tooltip lines are added when the parsed member value survived
     * clamping unchanged, or always when options.Dev is set.
     * @param {object} member - Member description (Min, Max, Default).
     * @param {EditOptions} options - Editor options (Dev).
     * @returns {Array} [min, max, def, typeTitle, haveMin, haveMax, parsedDef].
     */
    GetMinMaxDefault(member, options) {
        let typeTitle = null;
        const typeMin = this.TypeMin;
        const typeMax = this.TypeMax;
        //  Min
        let min = member.Min;
        if (Edit.IsValid(min))
            min = parseFloat(("" + min).replace(',', '.'));
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
            max = parseFloat(("" + max).replace(',', '.'));
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
            def = parseFloat(("" + def).replace(',', '.'));
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


    /**
     * Builds the number editor in editContext.Element.
     * - obj[member.Name] is set to the default when undefined or an object (incl. null).
     * - A text input (Edit.CreateTextInput) accepting digits, one "." and a leading "-" when min < 0; maxlength and
     *   CSS max-width come from MaxLen (minus one for non-negative ranges).
     * - With TypeMemberFlags.Slider and both haveMin and haveMax, a range slider is added (cell class EditSlider).
     *   Step: parseFloat(member.EditParams) when > 0 (warning logged otherwise), else a power of ten (up or down)
     *   giving 10..100 steps. A number of decimals is then derived so that step, min and max are whole at that
     *   scale (max 5); the text input then formats with toFixed(decimals) and slider values are rounded to it.
     * Text changes go through editor.Invoke without tracking; slider changes use the slider as tracking object.
     * Read only when options.ReadOnly or the member has TypeMemberFlags.ReadOnly.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - Owning editor (Invoke, Element, Key, KeyName).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Min, Max, Default, Flags, EditParams, Summary).
     * @param {string} title - Member title (unused).
     * @param {EditOptions} options - Editor options (ReadOnly, Dev).
     * @returns {Promise<object>} Item {Focus, Title, DefValue, DefValueText, SetValue(v), UpdateValue(isFirst)};
     *   DefValue/DefValueText are the parsed member default; UpdateValue does not steal focus from an active slider.
     */
    async AddEditor(obj, editor, editContext, member, title, options) {
        const mn = member.Name;
        const cell = editContext.Element;

        const mmd = this.GetMinMaxDefault(member, options);
        const min = mmd[0];
        const max = mmd[1];
        const def = mmd[2];
        let typeTitle = mmd[3];
        const haveMin = mmd[4];
        const haveMax = mmd[5];
        let maxLen = this.MaxLen;
        const ot = typeof obj[mn];
        if ((ot === "undefined") || (ot === "object"))
            obj[mn] = this.GetDefault(mmd);
        let text = null;
        let slider = null;

        /**
         * Parses the text input value: parseFloat(v), 0 when falsy/NaN, clamped to [min, max].
         * @param {string} v - Text input value.
         * @returns {number} Parsed number.
         */
        const fromText = v => {
            let val = parseFloat(v);
            if (!val)
                val = 0;
            if (val < min)
                val = min;
            if (val > max)
                val = max;
            return val;
        };

        let decimals = -1;

        //  String length
        const signed = min < 0;
        if (!signed)
            --maxLen;
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
            v => decimals < 0 ? ("" + v) : v.toFixed(decimals),
            member.Summary,
            v => v,
            (key, pos, val) => {
                if ((pos == 0) && signed && (key == '-'))
                    return true;
                if (key == '.')
                    return val.indexOf('.') < 0;
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

                    step = parseFloat(pt);
                    if ((!step) || (step <= 0)) {
                        console.warn('Invalid edit parameter "' + pt + '", expected an integer!');
                        step = 0;
                    } else {
                        smin = Math.floor(min / step);
                        smax = Math.floor(max / step);
                    }
                }
                catch (e) {
                    console.warn('Invalid edit parameter "' + pt + '", expected an integer, error: ' + e);
                }
            }
            if (step <= 0) {
                step = 1;
                for (; ;) {
                    smin = Math.floor(min / step);
                    smax = Math.floor(max / step);
                    const steps = smax - smin;
                    if ((steps <= 100) && (steps >= 10))
                        break;
                    if (steps > 10)
                        step *= 10;

                    else
                        step /= 10;
                }
            }
            decimals = 0;
            for (let scale = 1; scale < 100000; scale *= 10, ++ decimals) {
                let test = scale * step;
                if (Math.round(test) != test)
                    continue;
                test = scale * min;
                if (Math.round(test) != test)
                    continue;
                test = scale * max;
                if (Math.round(test) != test)
                    continue;
                break;
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
                    val = parseFloat(val.toFixed(decimals));
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
                        slider
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
