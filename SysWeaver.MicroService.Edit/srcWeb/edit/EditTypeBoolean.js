class EditTypeBoolean {

    CreateDefault(member, options) {
        return this.GetDefault(member, options)[0];
    }

    IsOfType(obj) {
        const t = typeof (obj);
        if (t === "boolean")
            return true;
        if (t !== "number") {
            if (t !== "string")
                return false;
            if (obj == "true")
                return true;
            if (obj == "false")
                return true;
            return false;
        }
        if (obj == 0)
            return true;
        if (obj == 1)
            return true;
        return false;
    }

    Condition(obj) {
        const t = typeof (obj);
        if (t === "string")
            return obj == "true";
        if (t == "boolean")
            return !!obj;
        return obj == 1;
    }

    async Validate(obj, member, options, name) {
        const t = typeof (obj);
        if (t === "string") {
            if ((obj == "true") | (obj == "false"))
                return null;
            return [name, member.DisplayName + ': The value "' + obj + '" is not a valid boolean value, expecting: "true", "false", 1 or 0'];
        }
        if (t == "boolean")
            return null;
        return [name, member.DisplayName + ': The value "' + obj + '" of type "' + t + '" is not a valid boolean value, expecting: "true", "false", 1 or 0'];
    }

    ToString(obj, member, options) {
        return "" + (obj ? "true" : "false");
    }

    GetDefault(member, options) {
        let typeTitle = null;
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def)) {
            def = Edit.IsTrue(def);
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", def);
        }
        return [def, typeTitle];
    }



    async AddEditor(obj, editor, editContext, member, title, options) {
        const mn = member.Name;
        const cell = editContext.Element;

        const mmc = this.GetDefault(member, options);
        const def = mmc[0];
        let typeTitle = mmc[1];
        const ot = typeof obj[mn];
        if ((ot === "undefined") || (ot === "object"))
            obj[mn] = def ? def : false;

        let slider = null;
        slider = Edit.CreateSliderInput(
            () => {
                const v = !!obj[mn];
                if (v)
                    cell.classList.add("EditTrue");

                else
                    cell.classList.remove("EditTrue");
                return v ? 1 : 0;
            },
            async v =>  {
                const old = obj[mn];
                const newValue = v != 0;
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
            1,
            options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0)
        );
        cell.classList.add("EditBoolean");
        cell.appendChild(slider);
        return {
            Focus: slider,
            Title: typeTitle,
            DefValue: def,
            DefValueText: def,
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            UpdateValue: isFirst => {
                slider.UpdateValue();
                if (obj[mn])
                    cell.classList.add("EditTrue");
                else
                    cell.classList.remove("EditTrue");
                if (!isFirst)
                    slider.focus();
            },
        };
    }

}
