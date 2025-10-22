class EditTypeString {


    constructor(forceReadOnly) {
        this.ForceReadOnly = !!forceReadOnly;
    }

    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    IsOfType(obj) {
        const t = typeof (obj);
        return (t !== "undefined") && (t !== "object");
    }

    Condition(obj, member, options) {
        obj = "" + obj;
        const maxLength = Edit.GetIntParam(member.Max, 0);
        if (maxLength > 0)
            if (obj.length > maxLength)
                obj = obj.substr(0, maxLength);

        const isPassword = (member.Flags & TypeMemberFlags.Password) !== 0;
        const isMultiLine = ((member.Flags & TypeMemberFlags.Multiline) !== 0) && (!isPassword);
        if (!isMultiLine)
            obj = obj.replace(/(\r\n|\n|\r)/gm, " ");
        return obj;
    }

    async Validate(obj, member, options, name) {
        if (obj == null) {
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
            return [name, member.DisplayName + ": Value may not be null"];
        }
        obj = "" + obj;
        const ol = obj.length;
        const maxLength = Edit.GetIntParam(member.Max, 0);
        if (maxLength && (maxLength > 0) && (ol > maxLength))
            return [name, member.DisplayName + ": Text to long! Maximum allowed length is " + ValueFormat.countSuffix(maxLength, " char", " chars") + ", the current text is " + ValueFormat.countSuffix(ol, " char!", " chars!")];
        const minLength = Edit.GetIntParam(member.Min, 0);
        if (minLength && (minLength > 0) && (ol < minLength))
            return [name, member.DisplayName + ": Text to short! Minimum allowed length is " + ValueFormat.countSuffix(minLength, " char", " chars") + ", the current text is " + ValueFormat.countSuffix(ol, " char!", " chars!")];
        return null;
    }


    ToString(obj, member, options) {
        return "" + obj;
    }

    GetDefault(member, options) {
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            return def === "\t" ? "" : def;
        if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
            return null;
        return "";
    }

    async AddEditor(obj, editor, editContext, member, title, options) {
        let typeTitle = null;
        const mn = member.Name;
        const cell = editContext.Element;
        const readOnly = options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0) || this.ForceReadOnly;

        let minLength = parseInt(member.Min);
        if (minLength && (minLength <= 0))
            minLength = null;
        if (minLength)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Minimum length", minLength + " characters");

        let maxLength = parseInt(member.Max);
        if (maxLength && (maxLength <= 0))
            maxLength = null;
        if (maxLength)
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Maximum length", maxLength + " characters");

        const def = this.GetDefault(member, options);
        const defText = def == null ? null : ('"' + def + '"');
        typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", defText);

        if (typeof obj[mn] === "undefined")
            obj[mn] = def;

        let validateKeyOrPattern = null;
        const isPassword = (member.Flags & TypeMemberFlags.Password) !== 0;
        const isMultiLine = ((member.Flags & TypeMemberFlags.Multiline) !== 0) && (!isPassword);
        const text = Edit.CreateTextInput(
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
                    "Set value: \"" + newValue + "\"",
                    mn,
                    text,
                );
                return true;
            },
            v => v,
            v => v,
            member.Summary,
            null,
            validateKeyOrPattern,
            maxLength,
            def,
            readOnly,
            isMultiLine ? 3 : 0
        );
        if (isPassword)
            text.type = "password";
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
