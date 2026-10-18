/**
 * @file EditTypeEnum.js - type handler for enum members (normal and [Flags] enums) of the object editor.
 *
 * Loaded by Edit.Init() (Edit.js) via includeJs(current, "EditTypeEnum.js").
 * Defines the global class EditTypeEnum. A single shared instance is created lazily by Edit.GetTypeHandler()
 * (stored in Edit.EnumTypeHandler) for members with TypeMemberFlags.IsEnum.
 *
 * Enum metadata encoding: TypeService packs the enum into member.Default as
 * DEFAULT ">" ENTRY "|" ENTRY "|" ..., where each ENTRY is VALUE "<" NAME ["<" SUMMARY ["<" REMARKS]]:
 * ">" separates the default from the value list, "|" separates entries and "<" separates the fields of an entry.
 *
 * Dependencies: Edit (IsValid, CreateSelectionInput, CreateChangeEvent), ValueFormat (removeCamelCase,
 * AddNonNullLine) from common, TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract (CreateDefault, IsOfType, Condition, Validate,
 * ToString, GetDefault, AddEditor).
 */

/**
 * Type handler for enums. Renders a select element (multi-select for IsFlags enums) and caches decoded enum
 * value maps per type name in this.TypeMap.
 */
class EditTypeEnum {

    /**
     * Counts the number of set bits in a number (via its base-2 string).
     * @param {number} num - Value to inspect.
     * @returns {number} Number of "1" digits in num.toString(2).
     */
    static NumSetBits(num) {
        const s = num.toString(2);
        const l = s.length;
        let c = 0;
        for (let i = 0; i < l; ++i) {
            if (s.charAt(i) === '1')
                ++c;
        }
        return c;
    }

    /**
     * @param {boolean} [forceReadOnly=false] - When truthy every editor created is read only (Edit.GetTypeHandler passes nothing).
     */
    constructor(forceReadOnly) {
        /** @type {boolean} Forces read-only editors. */
        this.ForceReadOnly = !!forceReadOnly;
        /** @type {Map<string, Map<*, *>>} Cache of value maps keyed by member.TypeName (see GetValueMap). */
        this.TypeMap = new Map();
    }

    /**
     * Returns (and caches per member.TypeName) the decoded value map of an enum.
     * The map is keyed by the numeric value, the numeric string, the raw name and the de-camel-cased name, each
     * mapping to [value, displayName, rawName]; flags enums also map 0 and "0" to [0, "0", "0"].
     * Special keys:
     * - "___single___": [[value, displayName]] of flag values with exactly one bit set (flags enums only, else empty).
     * - "___edit___": select options [value, displayName, tooltip] for editable selects; for flags enums,
     *   multi-bit values get their combination text (ToString) prepended to the tooltip.
     * - "___editro___": options for read-only selects (all values, or only single-bit values for flags enums).
     * @param {object} member - Member description (TypeName, Flags, Default holding the encoded enum).
     * @param {EditOptions} [options] - Editor options (passed to ToString).
     * @returns {Map<*, *>} The value map.
     */
    GetValueMap(member, options) {
        const m = this.TypeMap;
        let t = m.get(member.TypeName);
        if (t)
            return t;
        t = new Map();
        m.set(member.TypeName, t);
        const isFlags = (member.Flags & TypeMemberFlags.IsFlags) != 0;
        const singleVals = [];
        const vals = member.Default.split('>')[1].split('|');
        const vl = vals.length;
        if (isFlags) {
            const zero = [0, "0", "0"];
            t.set(0, zero);
            t.set("0", zero);
            for (let i = 0; i < vl; ++i) {
                const kv = vals[i].split('<');
                const v = parseInt(kv[0]);
                const k = kv[1];
                const kf = ValueFormat.removeCamelCase(k);
                if (EditTypeEnum.NumSetBits(v) === 1)
                    singleVals.push([v, kf]);
            }
        }
        t.set("___single___", singleVals);
        
        let haveZero = false;
        const editVals = [];
        const editValsRo = [];
        for (let i = 0; i < vl; ++i) {
            const kv = vals[i].split('<');
            const kvl = kv.length;
            const v = parseInt(kv[0]);
            const k = kv[1];
            const kf = ValueFormat.removeCamelCase(k);
            const val = [v, kf, k];
            haveZero |= (v == 0);
            let title = v + ": " + k;
            title = ValueFormat.AddNonNullLine(title, "Summary: ", kvl > 2 ? kv[2] : null);
            title = ValueFormat.AddNonNullLine(title, "Remarks: ", kvl > 3 ? kv[3] : null);
            t.set(v, val);
            t.set("" + v, val);
            t.set(k, val);
            t.set(kf, val);
            const eVal = [v, kf, title];
            if (isFlags) {
                const bs = EditTypeEnum.NumSetBits(v);
                if (bs > 1) {
                    editVals.push([v, kf, this.ToString(v, member, options) + "\n" + title]);
                } else {
                    editVals.push(eVal);
                }
                if (bs == 1)
                    editValsRo.push(eVal);
            } else {
                editValsRo.push(eVal);
                editVals.push(eVal);
            }
        }
//        if (isFlags && (!haveZero))
//            editVals.splice(0, 0, [0, "-", "No values set"]);
        t.set("___edit___", editVals);
        t.set("___editro___", editValsRo);
        return t;
    }

    /**
     * Creates the default value for a new member.
     * @param {object} member - Member description.
     * @param {EditOptions} options - Editor options.
     * @returns {number} Same as GetDefault.
     */
    CreateDefault(member, options) {

        return this.GetDefault(member, options);
    }

    /**
     * Checks whether a pasted / dropped value could be an enum value.
     * @param {*} obj - Candidate value.
     * @returns {boolean} true for numbers that parseInt accepts and for any string (names are not checked here).
     */
    IsOfType(obj) {
        const t = typeof (obj);
        if (t === "number") {
            let res = parseInt(obj);
            if (isNaN(res))
                return false;
            return true;
        }
        if (t !== "string")
            return false;
        return true;
    }

    /**
     * Decodes a number or text into the numeric enum value.
     * A number is truncated to an int and split into its individual bits, each of which must exist in the value
     * map (so a non-flags value that is not a power of two is only accepted if all its bits are defined values);
     * a string is split on "+" and each part must be a key of the value map (no trimming, so "A + B" fails).
     * @param {number|string} obj - Value to decode.
     * @param {object} member - Member description.
     * @param {EditOptions} [options] - Editor options.
     * @returns {?number} The OR of all decoded parts (0 for the number 0), or null when any part is unknown.
     */
    Decode(obj, member, options) {
        const m = this.GetValueMap(member, options);
        let p;
        if (typeof obj === "number") {
            obj |= 0;
            p = [];
            for (let mask = 1; obj != 0; mask += mask) {
                if ((obj & mask) === 0)
                    continue;
                p.push(mask);
                obj ^= mask;
            }
        } else {
            p = obj.split('+');
        }
        let x = 0;
        const pl = p.length;
        for (let i = 0; i < pl; ++i) {
            const xp = m.get(p[i]);
            if (!xp)
                return null;
            x |= xp[0];
        }
        return x;
    }


    /**
     * Converts a pasted / dropped value (number or "A+B" name string) into a numeric enum value via Decode,
     * falling back to the member default (GetDefault) when it cannot be decoded.
     * @param {number|string} obj - Value to convert.
     * @param {object} member - Member description.
     * @param {EditOptions} [options] - Editor options.
     * @returns {number} The enum value.
     */
    Condition(obj, member, options) {
        obj = this.Decode(obj, member, options);
        if (typeof (obj) !== "number")
            obj = this.GetDefault(member, options);
        return obj;
    }

    /**
     * Validates a value: null is never allowed; otherwise it must be decodable by Decode.
     * @param {*} obj - Value to validate.
     * @param {object} member - Member description (DisplayName).
     * @param {EditOptions} options - Editor options.
     * @param {string} name - Member path/name returned with the error.
     * @returns {Promise<?Array<string>>} null when valid, otherwise [name, message].
     */
    async Validate(obj, member, options, name) {
        if (obj == null)
            return [name, member.DisplayName + ": Value is null and null is not allowed!"];
        const val = this.Decode(obj, member, options);
        const t = typeof val;
        if (t !== "number")
            return [name, member.DisplayName + ': The value "' + obj + '" of type "' + t + '" is not a valid enum value, expecting a number or enum text'];
        return null;
    }

    /**
     * Formats an enum value for display.
     * Non-flags: the de-camel-cased name from the value map, or obj itself when unknown.
     * Flags: the de-camel-cased names of all single-bit values contained in obj joined with " + ", or "0".
     * @param {number} obj - Numeric enum value.
     * @param {object} member - Member description.
     * @param {EditOptions} [options] - Editor options.
     * @returns {string|*} Display text.
     */
    ToString(obj, member, options) {
        const m = this.GetValueMap(member, options);
        if ((member.Flags & TypeMemberFlags.IsFlags) == 0) {
            let res = m.get(obj);
            return res ? res[1] : obj;
        }
        const s = m.get("___single___");
        const sl = s.length;
        let n = "";
        for (let i = 0; i < sl; ++i) {
            const k = s[i];
            if ((obj & k[0]) == 0)
                continue;
            if (n.length > 0)
                n += " + ";
            n += k[1];
        }
        return n.length <= 0 ? "0" : n;
    }

    /**
     * Returns the default enum value: parseInt of the encoded member.Default (parseInt stops at the ">" separator,
     * so this is the leading default number), or 0 when member.Default is not valid.
     * @param {object} member - Member description.
     * @param {EditOptions} [options] - Editor options (unused).
     * @returns {number} Default numeric value (NaN if the encoded default is not numeric).
     */
    GetDefault(member, options) {
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            return parseInt(def.split(';')[0]);
        return 0;
    }

    /**
     * Builds the enum editor: a select (Edit.CreateSelectionInput) filled from "___edit___" (or "___editro___"
     * when read only), multi-select for IsFlags members. Read only when options.ReadOnly, the member has
     * TypeMemberFlags.ReadOnly, or ForceReadOnly. obj[member.Name] is set to the default when undefined.
     * When member.Type is "Allow.<OtherMember>", focusing the select disables each option whose bit
     * (1 << optionValue) is not set in obj[OtherMember] (a console warning is logged if that member is undefined).
     * Selections go through editor.Invoke ("Set value: \"<n>\"", no tracking object) after parseInt.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - Owning editor (Invoke, Element, Key, KeyName).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Type, Flags, Default).
     * @param {string} title - Member title (unused).
     * @param {EditOptions} options - Editor options (ReadOnly).
     * @returns {Promise<object>} Item {Focus, Title, DefValue, DefValueText, SetValue(v), UpdateValue(isFirst)};
     *   SetValue writes obj[mn] and dispatches EditChange on editor.Element.
     */
    async AddEditor(obj, editor, editContext, member, title, options) {
        let typeTitle = null;
        const mn = member.Name;
        const cell = editContext.Element;
        const readOnly = options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0) || this.ForceReadOnly;

        const def = this.GetDefault(member, options);
        const defText = this.ToString(def, member, options);
        typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", defText);

        if (typeof obj[mn] === "undefined")
            obj[mn] = def;
        let openFn = null;
        if (member.Type) {
            if (member.Type.startsWith("Allow.")) {
                const mn = member.Type.substring(6);
                if (typeof obj[mn] === "undefined") {
                    console.warn(mn + " is undefined!");
                } else {
                    openFn = opts => {
                        const allowed = obj[mn];
                        const ol = opts.length;
                        for (let i = 0; i < ol; ++i) {
                            const opt = opts[i];
                            const m = 1 << opt.KeyValue[0];
                            opt.disabled = (allowed & m) === 0;
                        }
                    };
                }
            }
        }
        const text = Edit.CreateSelectionInput(
            () => obj[mn],
            async v => {
                const newValue = parseInt("" + v);
                const old = obj[mn];
                if (old === newValue)
                    return false;
                await editor.Invoke(
                    edit => {
                        edit.SetValue(newValue);
                    },
                    edit => {
                        edit.SetValue(old);
                    },
                    "Set value: \"" + newValue + "\"",
                    mn,
                    null,
                    text
                );
                return true;
            },
            this.GetValueMap(member, options).get(readOnly ? "___editro___" : "___edit___"),
            def,
            readOnly,
            (member.Flags & TypeMemberFlags.IsFlags) != 0,
            openFn
        );
        cell.appendChild(text);
        return {
            Focus: text,
            Title: typeTitle,
            DefValue: def,
            DefValueText: defText,
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            UpdateValue: isFirst => {
                text.UpdateValue();
                if (!isFirst)
                    text.focus();
            },
        };
    }

}
