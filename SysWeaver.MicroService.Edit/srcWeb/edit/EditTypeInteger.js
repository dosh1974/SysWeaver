/**
 * @file EditTypeInteger.js - type handler for the integer types (SByte..UInt64 and their Nullable variants) of the object editor.
 *
 * Loaded by Edit.Init() (Edit.js) via includeJs(current, "EditTypeInteger.js").
 * Defines the global class EditTypeInteger. Instances are created in Edit.GetTypeMap(), one per .NET integer type,
 * e.g. `new EditTypeInteger(true, 4)` for System.Int32 and
 * `new EditTypeInteger(true, 8, false, Number.MIN_SAFE_INTEGER, Number.MAX_SAFE_INTEGER)` for System.Int64.
 *
 * Dependencies: Edit (IsValid, CreateTextInput, CreateSliderInput, CreateChangeEvent), ValueFormat.AddNonNullLine
 * (common), TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract, but note that GetDefault takes the array returned
 * by GetMinMaxDefault instead of (member, options).
 */

/**
 * Type handler for integer values: a digit-only text input plus an optional range slider (Slider flag with both
 * Min and Max given; the cell then gets the class EditSlider).
 */
class EditTypeInteger {

    /**
     * @param {boolean} signed - Whether the type is signed.
     * @param {number} byteSize - Size of the type in bytes (1, 2, 4 or 8); used to compute the type range.
     * @param {boolean} [nullable] - Whether this is a Nullable<T> type (stored only, not used).
     * @param {number} [literalMin] - Explicit type minimum, used together with literalMax.
     * @param {number} [literalMax] - Explicit type maximum; when truthy, literalMin/literalMax replace the computed range
     *   (used for Int64/UInt64 to stay within JavaScript safe integers).
     */
    constructor(signed, byteSize, nullable, literalMin, literalMax) {
        /** @type {boolean} Whether negative values are allowed by the type. */
        this.Signed = signed;
        /** @type {number} Type size in bytes. */
        this.ByteSize = byteSize;

        /** @type {boolean|undefined} Nullable flag (informational only). */
        this.Nullable = nullable;
        if (literalMax) {
            this.TypeMin = literalMin;
            this.TypeMax = literalMax;
        } else {
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
    }

    /**
     * Checks whether a pasted / dropped value is an integer within the type range (TypeMin..TypeMax).
     * Strings are parsed with parseInt, falling back to parseFloat rounded (and truncated to 32 bits with | 0).
     * @param {*} obj - Candidate value (number or string).
     * @returns {boolean} true when numeric and within the type range.
     */
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

    /**
     * Converts a pasted / dropped value into an integer clamped to the member's min/max (GetMinMaxDefault).
     * Strings are parsed with parseInt (parseFloat fallback); the value is rounded and truncated to a 32-bit int
     * (`| 0`), so values outside the int32 range wrap before clamping.
     * @param {number|string} obj - Value to convert.
     * @param {object} member - Member description (Min, Max, Default).
     * @param {EditOptions} options - Editor options.
     * @returns {number} The conditioned integer.
     */
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
     * Merges member.Min / Max / Default with the type range and builds the tooltip text.
     * Min and Max are parsed with parseInt and clamped to the type range (max is also raised to at least min);
     * Default is parsed with parseFloat, truncated, and clamped to [min, max] (falsy defaults become 0).
     * Tooltip lines ("Minimum value", "Maximum value", "Default value") are added when the parsed member value
     * survived clamping unchanged, or always when options.Dev is set.
     * @param {object} member - Member description (Min, Max, Default).
     * @param {EditOptions} options - Editor options (Dev).
     * @returns {Array} [min, max, def, typeTitle, haveMin, haveMax, parsedDef]: haveMin/haveMax tell whether the
     *   member specified an in-range limit, parsedDef is the parsed (unclamped) member default or the raw invalid value.
     */
    GetMinMaxDefault(member, options) {
        const typeMin = this.TypeMin;
        const typeMax = this.TypeMax;
        let typeTitle = null;
        //  Min
        let min = member.Min;
        if (Edit.IsValid(min))
            min = Math.trunc(parseInt("" + min));
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
            max = Math.trunc(parseInt("" + max));
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


    /**
     * Builds the integer editor in editContext.Element.
     * - obj[member.Name] is set to the default when undefined or an object (incl. null).
     * - A text input (Edit.CreateTextInput) that only accepts digits, plus a leading "-" when the type is signed and
     *   min < 0; maxlength and CSS max-width come from the longest of the min/max digit counts.
     * - With TypeMemberFlags.Slider and both haveMin and haveMax, a range slider is added (cell class EditSlider).
     *   Its step is parseInt(member.EditParams) when valid (warning logged otherwise), else the smallest power of
     *   ten giving at most 100 steps.
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
        /**
         * Parses the text input value: floor(parseFloat(v)), 0 when falsy/NaN, clamped to [min, max].
         * @param {string} v - Text input value.
         * @returns {number} Parsed integer.
         */
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
