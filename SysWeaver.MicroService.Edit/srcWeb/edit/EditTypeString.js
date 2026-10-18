/**
 * @file EditTypeString.js - type handler for System.String (and, read only, System.Object) members of the object editor.
 *
 * Loaded by Edit.Init() (Edit.js) via includeJs(current, "EditTypeString.js").
 * Defines the global class EditTypeString. Instances are created in Edit.GetTypeMap():
 * `new EditTypeString()` for "System.String" and `new EditTypeString(true)` (forced read only) for "System.Object".
 *
 * Dependencies: Edit (GetIntParam, IsValid, CreateTextInput, CreateChangeEvent), ValueFormat (AddNonNullLine,
 * countSuffix) from common, TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract (CreateDefault, IsOfType, Condition, Validate,
 * ToString, GetDefault, AddEditor). A member.Default of "\t" is the server's marker for an empty string.
 */

/**
 * Type handler for text values: a single-line text input, a 3-line textarea (Multiline flag, not for passwords)
 * or a password input (Password flag).
 */
class EditTypeString {


    /**
     * @param {boolean} [forceReadOnly=false] - When truthy the editor is always read only (used for System.Object).
     */
    constructor(forceReadOnly) {
        /** @type {boolean} Forces read-only editors regardless of options / member flags. */
        this.ForceReadOnly = !!forceReadOnly;
    }

    /**
     * Creates the default value for a new member.
     * @param {object} member - Member description (TypeMemberDesc).
     * @param {EditOptions} options - Editor options.
     * @returns {?string} Same as GetDefault.
     */
    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    /**
     * Checks whether a pasted / dropped value can be used as text.
     * @param {*} obj - Candidate value.
     * @returns {boolean} true for anything that is neither undefined nor an object (null is an object, so rejected).
     */
    IsOfType(obj) {
        const t = typeof (obj);
        return (t !== "undefined") && (t !== "object");
    }

    /**
     * Converts a pasted / dropped value to a string that fits the member: truncates to member.Max characters
     * (when > 0) and replaces line breaks with spaces unless the member is Multiline (and not Password).
     * @param {*} obj - Value to convert ("" + obj).
     * @param {object} member - Member description (Max, Flags).
     * @param {EditOptions} [options] - Editor options (unused).
     * @returns {string} The conditioned text.
     */
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

    /**
     * Validates a value: null is only allowed with TypeMemberFlags.AcceptNull; the text length ("" + obj) must be
     * within member.Min..member.Max (each only checked when > 0).
     * @param {*} obj - Value to validate.
     * @param {object} member - Member description (Flags, Min, Max, DisplayName).
     * @param {EditOptions} options - Editor options (unused).
     * @param {string} name - Member path/name returned with the error.
     * @returns {Promise<?Array<string>>} null when valid, otherwise [name, message].
     */
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
     * @param {object} member - Member description (Default, Flags).
     * @param {EditOptions} options - Editor options (unused).
     * @returns {?string} member.Default when Edit.IsValid ("\t" maps to ""), else null with AcceptNull, else "".
     */
    GetDefault(member, options) {
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            return def === "\t" ? "" : def;
        if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
            return null;
        return "";
    }

    /**
     * Builds the text editor in editContext.Element using Edit.CreateTextInput.
     * Read only when options.ReadOnly, the member has TypeMemberFlags.ReadOnly, or ForceReadOnly.
     * The tooltip (typeTitle) lists minimum/maximum length (member.Min/Max, when > 0) and the quoted default.
     * maxlength comes from member.Max; Multiline members (non-password) get a 3-line textarea; Password members get
     * type=password. obj[member.Name] is set to the default when undefined.
     * User changes go through editor.Invoke ("Set value: \"...\""), passing the text element as the undo tracking
     * object so consecutive typing is coalesced into one undo step.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - Owning editor (Invoke, Element, Key, KeyName).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Min, Max, Default, Flags, Summary used as placeholder).
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
