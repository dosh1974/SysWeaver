/**
 * @file EditTypeDefault.js - fallback type handler of the object editor for member types that no other handler supports.
 *
 * Loaded by Edit.Init() (Edit.js) via includeJs(current, "EditTypeDefault.js").
 * Defines the global class EditTypeDefault. A single shared instance is created lazily by Edit.GetTypeHandler()
 * (stored in Edit.DefaultTypeHandler) when a type name is not in the type map and the member is neither a
 * collection, an object nor an enum.
 *
 * Dependencies: Edit (IsPrimitive, IsValid, CreateTextInput), ValueFormat.AddNonNullLine (common),
 * TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract (CreateDefault, IsOfType, Condition, Validate,
 * ToString, GetDefault, AddEditor); the editor is always read only.
 */

/**
 * Stateless fallback handler: shows the value as read-only text ("" + value) and marks the member read only.
 */
class EditTypeDefault {

    /**
     * Creates the default value for a new member.
     * @param {object} member - Member description (TypeMemberDesc).
     * @param {EditOptions} options - Editor options.
     * @returns {*} Same as GetDefault.
     */
    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    /**
     * Checks whether a pasted / dropped value fits: only for non-primitive members, when obj is an object whose
     * "$type" property equals member.TypeName.
     * @param {*} obj - Candidate value.
     * @param {object} member - Member (or type) description with Flags and TypeName.
     * @param {EditOptions} [options] - Editor options (unused).
     * @returns {boolean} true when the value's $type matches.
     */
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

    /**
     * Conditions a pasted / dropped value; returns it unchanged (stripping "$type" is commented out).
     * @param {*} obj - Value.
     * @returns {*} obj as is.
     */
    Condition(obj) {
//        if (obj)
//            delete obj["$type"];
        return obj;
    }

    /**
     * Validates a value: only null/undefined is checked (allowed when the member has TypeMemberFlags.AcceptNull).
     * @param {*} obj - Value to validate.
     * @param {object} member - Member description (Flags, DisplayName).
     * @param {EditOptions} options - Editor options (unused).
     * @param {string} name - Member path/name returned with the error.
     * @returns {Promise<?Array<string>>} null when valid, otherwise [name, message].
     */
    async Validate(obj, member, options, name) {
        if (obj == null) {
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
            return [name, member.DisplayName + ": Value is null and null is not allowed!" ];
        }
        return null;
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
     * Computes the default value.
     * @param {?object} member - Member description; may be null.
     * @param {EditOptions} options - Editor options (unused).
     * @returns {*} member.Default when Edit.IsValid, else null when the member has AcceptNull, else {} (also when member is null).
     */
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


    /**
     * Builds a read-only text input showing "" + obj[member.Name] in editContext.Element.
     * When obj[member.Name] is undefined it is set to member.Default (if valid and truthy) or null.
     * Side effect: permanently sets bit 16 (TypeMemberFlags.ReadOnly) on the shared member description.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - Owning editor (unused).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Default, Summary used as placeholder, Flags).
     * @param {string} title - Member title (unused).
     * @param {EditOptions} options - Editor options (unused).
     * @returns {Promise<object>} Item {Title, DefValue, DefValueText, UpdateValue()}; no Focus or SetValue.
     */
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




