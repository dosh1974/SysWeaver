class EditTypeEnum {

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

    constructor(forceReadOnly) {
        this.ForceReadOnly = !!forceReadOnly;
        this.TypeMap = new Map();
    }

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

    CreateDefault(member, options) {

        return this.GetDefault(member, options);
    }

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


    Condition(obj, member, options) {
        obj = this.Decode(obj, member, options);
        if (typeof (obj) !== "number")
            obj = this.GetDefault(member, options);
        return obj[0];
    }

    async Validate(obj, member, options, name) {
        if (obj == null)
            return [name, member.DisplayName + ": Value is null and null is not allowed!"];
        const val = this.Decode(obj, member, options);
        const t = typeof val;
        if (t !== "number")
            return [name, member.DisplayName + ': The value "' + obj + '" of type "' + t + '" is not a valid enum value, expecting a number or enum text'];
        return null;
    }

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

    GetDefault(member, options) {
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            return parseInt(def.split(';')[0]);
        return 0;
    }

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
