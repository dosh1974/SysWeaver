/**
 * @file Type handler for enum members, including [Flags] enums (`Sw.Edit.Handlers.Enum`, legacy global `EditTypeEnum`).
 *
 * Loaded by Edit.js. Enum values come packed in `member.Default` as `default>value<name<summary<remarks|...`.
 * Normal enums use a Sw.Select (searchable when long), flags enums a row of toggle chips (bits are merged; multi bit values
 * show their decomposition in the tooltip). A member `Type` spec `Allow.<Member>` disables values whose bit is not set in the
 * sibling member `<Member>` (re-evaluated when the list opens / the chips get the focus). Read only: the value name as plain text (flags: the set names).
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;
    const addLine = E.Util.AddLine;

    /**
     * Enum handler (shared; value maps are cached per enum type name).
     */
    class EditTypeEnum extends E.Handler {
        /**
         * @param {boolean} [forceReadOnly] - Always read only.
         */
        constructor(forceReadOnly) {
            super();
            /** @type {boolean} Always read only. */
            this.ForceReadOnly = !!forceReadOnly;
            /** @type {Map<string, object>} Enum type name -> value info. */
            this.TypeMap = new Map();
        }

        /**
         * Number of set bits.
         * @param {number} num - The value.
         * @returns {number} The count.
         */
        static NumSetBits(num) {
            let c = 0;
            for (let v = num >>> 0; v; v >>>= 1)
                c += v & 1;
            return c;
        }

        /**
         * Test for a flags enum.
         * @param {object} member - The member.
         * @returns {boolean} True for [Flags] enums.
         */
        static IsFlags(member) {
            return (member.Flags & F.IsFlags) !== 0 && (member.Flags & F.IsEnum) !== 0;
        }

        /**
         * Decode the enum metadata (cached).
         * @param {object} member - The member.
         * @returns {{Lookup: Map, Values: Array, Single: Array, Default: number}} Lookup (value / text / name / display name ->
         * [value, displayName, name]), Values ([value, displayName, title] in declaration order), Single (single bit values).
         */
        Info(member) {
            const key = member.TypeName + "|" + member.Default;
            let t = this.TypeMap.get(key);
            if (t)
                return t;
            const isFlags = EditTypeEnum.IsFlags(member);
            const packed = "" + (member.Default ?? "");
            const gt = packed.indexOf(">");
            const def = parseInt(gt >= 0 ? packed.substring(0, gt) : packed) || 0;
            const vals = gt >= 0 ? packed.substring(gt + 1).split("|").filter(x => x) : [];
            const lookup = new Map();
            const values = [];
            const single = [];
            const decamel = s => Sw.Format.RemoveCamelCase ? Sw.Format.RemoveCamelCase(s) : s;
            for (const vs of vals) {
                const kv = vs.split("<");
                const v = parseInt(kv[0]);
                const name = kv[1] || ("" + v);
                const dn = decamel(name);
                let title = v + ": " + name;
                title = addLine(title, _TF("Summary", "The prefix of a row in a tool tip that ends with the C# code summary of an enum value"), kv[2] || null);
                title = addLine(title, _TF("Remarks", "The prefix of a row in a tool tip that ends with the C# code remarks of an enum value"), kv[3] || null);
                const e = [v, dn, name, title];
                for (const k of [v, "" + v, name, dn, name.toLowerCase()])
                    if (!lookup.has(k))
                        lookup.set(k, e);
                values.push(e);
                if (isFlags && EditTypeEnum.NumSetBits(v) === 1)
                    single.push(e);
            }
            t = { Lookup: lookup, Values: values, Single: single, Default: def, IsFlags: isFlags };
            this.TypeMap.set(key, t);
            return t;
        }

        /**
         * The old value map accessor (`___edit___`, `___editro___`, `___single___` keys plus lookups).
         * @param {object} member - The member.
         * @returns {Map} The map.
         */
        GetValueMap(member) {
            const i = this.Info(member);
            const m = new Map(i.Lookup);
            m.set("___single___", i.Single.map(x => [x[0], x[1]]));
            m.set("___edit___", i.Values.map(x => [x[0], x[1], x[3]]));
            m.set("___editro___", (i.IsFlags ? i.Single : i.Values).map(x => [x[0], x[1], x[3]]));
            return m;
        }

        /**
         * Accept numbers and texts (names are resolved by Condition).
         * @param {*} obj - Candidate.
         * @returns {boolean} True for numbers and strings.
         */
        IsOfType(obj) {
            return (typeof obj === "number" && !isNaN(obj)) || typeof obj === "string";
        }

        /**
         * Decode a value: a number (each bit must be known for flags), a name, a de camel cased name or "A + B".
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @returns {?number} The value, or null if unknown.
         */
        Decode(obj, member) {
            const i = this.Info(member);
            if (typeof obj === "number") {
                if (!i.IsFlags)
                    return i.Lookup.has(obj) ? obj : null;
                let rest = obj | 0;
                for (const s of i.Single)
                    if ((rest & s[0]) === s[0])
                        rest &= ~s[0];
                return rest === 0 ? (obj | 0) : null;
            }
            let x = 0;
            for (let p of ("" + obj).split("+")) {
                p = p.trim();
                const e = i.Lookup.get(p) || i.Lookup.get(p.toLowerCase()) || (/^-?\d+$/.test(p) ? i.Lookup.get(parseInt(p)) : null);
                if (!e) {
                    if (/^-?\d+$/.test(p) && i.IsFlags) {
                        x |= parseInt(p);
                        continue;
                    }
                    return null;
                }
                x |= e[0];
            }
            return x;
        }

        /**
         * Convert an accepted value (unknown values become the default).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @returns {number} The value.
         */
        Condition(obj, member) {
            const v = this.Decode(obj, member);
            return v === null ? this.GetDefault(member) : v;
        }

        /**
         * Validate: a known value (null is not allowed).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            const dn = member.DisplayName || member.Name;
            if (obj === null || obj === undefined)
                return E.Handler.NullCheck(obj, member, name);
            if (this.Decode(obj, member) === null)
                return [name, dn + ": " + _T("The value \"{0}\" is not a valid enum value", obj, "Validation error of an enum value.{0} is replaced with the invalid value.")];
            return null;
        }

        /**
         * Display text: the name, flags as "A + B" ("0" when none).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @returns {string} The text.
         */
        ToString(obj, member) {
            const i = this.Info(member);
            if (!i.IsFlags) {
                const e = i.Lookup.get(obj);
                return e ? e[1] : ("" + obj);
            }
            const v = this.Decode(obj, member) ?? 0;
            const parts = i.Single.filter(s => (v & s[0]) === s[0]).map(s => s[1]);
            return parts.length ? parts.join(" + ") : "0";
        }

        /**
         * The default value (from the packed metadata).
         * @param {object} member - The member.
         * @returns {number} The default.
         */
        GetDefault(member) {
            return this.Info(member).Default;
        }

        /**
         * Build the editor (Sw.Select, or flag chips).
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
            const i = this.Info(member);
            const readOnly = this.IsReadOnly(member, options);
            const def = i.Default;
            const defText = this.ToString(def, member);
            if (obj[mn] === undefined || obj[mn] === null)
                obj[mn] = def;
            else if (typeof obj[mn] === "string")
                obj[mn] = this.Condition(obj[mn], member);
            let openFn = null;
            const spec = member.Type;
            if (spec && spec.startsWith("Allow.")) {
                const other = spec.substring(6);
                if (obj[other] === undefined)
                    console.warn(other + " is undefined!");
                else
                    openFn = opts => {
                        const allowed = obj[other] | 0;
                        for (const o of opts)
                            o.disabled = (allowed & (1 << o.KeyValue[0])) === 0;
                    };
            }
            const values = i.Values.map(x => [x[0], x[1], i.IsFlags && EditTypeEnum.NumSetBits(x[0]) > 1 ? (t.ToString(x[0], member) + "\n" + x[3]) : x[3]]);
            const sel = E.CreateSelectionInput(() => obj[mn], async v => {
                v = typeof v === "number" ? v : parseInt("" + v);
                const old = obj[mn];
                if (old === v)
                    return;
                await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", t.ToString(v, member), "Command text stored in a log when a property value was changed.{0} is replaced with the new value."));
            }, values, def, readOnly, i.IsFlags, openFn);
            sel.setAttribute("aria-label", member.DisplayName || mn);
            editContext.Element.append(sel);
            return E.Handler.Item(obj, editor, member, {
                Focus: sel, Title: addLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), defText),
                DefValue: def, DefValueText: defText,
                Update: () => sel.UpdateValue(),
            });
        }
    }

    E.Handlers.Enum = EditTypeEnum;
    if (!window.EditTypeEnum)
        window.EditTypeEnum = EditTypeEnum;
})();
