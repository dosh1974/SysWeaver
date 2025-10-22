class EditTypeDefault {

    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    IsOfType(obj, member, options) {
        if (!Edit.IsPrimitive(member)) {
            if (typeof (obj) !== "object")
                return false;
            const tn = obj["$type"];
            if (member.TypeName === tn)
                return true;
        }
        return false;
    }

    Condition(obj) {
//        if (obj)
//            delete obj["$type"];
        return obj;
    }

    async Validate(obj, member, options, name) {
        if (obj == null) {
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
            return [name, member.DisplayName + ": Value is null and null is not allowed!" ];
        }
        return null;
    }

    ToString(obj, member, options) {
        return "" + obj;
    }

    GetDefault(member, options) {
        //  Default
        if (member) {
            let def = member.Default;
            if (Edit.IsValid(def))
                return def;
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
        }
        return {};
    }


    async AddEditor(obj, editor, editContext, member, title, options) {
        let typeTitle = null;
        const mn = member.Name;
        const cell = editContext.Element;

        let def = member.Default;
        if (Edit.IsValid(def)) {
            typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", def);
        }
        else
            def = null;

        if (typeof obj[mn] === "undefined")
            obj[mn] = def ? def : null;


        const text = Edit.CreateTextInput(
            () => obj[mn],
            null,
            null,
            v => "" + v,
            member.Summary,
            null,
            null,
            0,
            def,
            true
        );
        cell.appendChild(text);
        member.Flags |= 16;
        return {
            Title: typeTitle,
            DefValue: def,
            DefValueText: def,
            UpdateValue: () => {
                text.UpdateValue();
            },
        };
    }
}




