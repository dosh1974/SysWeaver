/**
 * @file EditTypeBoolean.js - type handler for System.Boolean (and Nullable<Boolean>) members of the object editor.
 *
 * Loaded by Edit.Init() (Edit.js) through includeJs(current, "EditTypeBoolean.js") when Edit.js is included by a
 * page (e.g. explore/api.html, ServerManager/services.html, edit test page).
 * Defines the global class EditTypeBoolean. Instances are created once per type map in Edit.GetTypeMap()
 * (`new EditTypeBoolean()` for "System.Boolean" and `new EditTypeBoolean(true)` for the Nullable variant; the class
 * has no constructor, so the argument is ignored and both behave identically).
 *
 * Dependencies: Edit (IsValid, IsTrue, CreateSliderInput, CreateChangeEvent), ValueFormat.AddNonNullLine (common),
 * TypeMemberFlags (type.js).
 * Conventions: implements the duck-typed type handler contract shared by all EditType*.js files
 * (CreateDefault, IsOfType, Condition, Validate, ToString, GetDefault, AddEditor); see README.md "Type handlers".
 * Styling: the value cell gets the classes EditBoolean (always) and EditTrue (while the value is true), styled
 * by EditDesktop.css / EditMobile.css as a toggle switch.
 */

/**
 * Stateless type handler for boolean members. Shared by all editors via the Edit type map; renders a
 * two-step range slider (0 = false, 1 = true) that is styled as a toggle switch.
 */
class EditTypeBoolean {

    /**
     * Creates the default value for a new boolean member.
     * @param {object} member - Member description (TypeMemberDesc) from TypeService.
     * @param {EditOptions} options - Editor options.
     * @returns {boolean|*} The parsed member.Default (via GetDefault), or the raw member.Default when it is not valid (usually undefined/null).
     */
    CreateDefault(member, options) {
        return this.GetDefault(member, options)[0];
    }

    /**
     * Checks whether a pasted / dropped value can be used as a boolean.
     * @param {*} obj - Candidate value.
     * @returns {boolean} true for any boolean, the strings "true"/"false" and the numbers 0/1 (loose ==, so "0"/"1" strings are not accepted since strings are checked first).
     */
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

    /**
     * Coerces a value accepted by IsOfType into a real boolean.
     * @param {*} obj - Value to convert.
     * @returns {boolean} String: obj == "true"; boolean: !!obj; anything else: obj == 1.
     */
    Condition(obj) {
        const t = typeof (obj);
        if (t === "string")
            return obj == "true";
        if (t == "boolean")
            return !!obj;
        return obj == 1;
    }

    /**
     * Validates a member value. Accepts booleans and the strings "true"/"false"; note that numbers (including 0/1)
     * are reported as invalid even though the message mentions them and IsOfType accepts them.
     * @param {*} obj - Value to validate.
     * @param {object} member - Member description (DisplayName is used in the message).
     * @param {EditOptions} options - Editor options (unused).
     * @param {string} name - Member path/name returned with the error.
     * @returns {Promise<?Array<string>>} null when valid, otherwise [name, message].
     */
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

    /**
     * Formats a value for display.
     * @param {*} obj - Value (truthiness decides).
     * @param {object} member - Member description (unused).
     * @param {EditOptions} options - Editor options (unused).
     * @returns {string} "true" or "false".
     */
    ToString(obj, member, options) {
        return "" + (obj ? "true" : "false");
    }

    /**
     * Computes the default value and the tooltip text describing it.
     * @param {object} member - Member description; member.Default is parsed with Edit.IsTrue when Edit.IsValid.
     * @param {EditOptions} options - Editor options (unused).
     * @returns {Array} [def, typeTitle]: def is a boolean (or the raw invalid member.Default), typeTitle is a
     *   "Default value: ..." line (ValueFormat.AddNonNullLine) or null.
     */
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



    /**
     * Builds the toggle editor for a boolean member inside editContext.Element.
     * If obj[member.Name] is undefined or an object (incl. null) it is set to the default (or false).
     * Creates a range input via Edit.CreateSliderInput (steps 0..1, disabled when options.ReadOnly or the member
     * has TypeMemberFlags.ReadOnly), adds the cell class EditBoolean and toggles EditTrue to reflect the value.
     * User changes go through editor.Invoke (undoable "Set value: <bool>", the slider is the tracking object).
     * @param {object} obj - The object being edited (value is obj[member.Name]).
     * @param {Edit} editor - Owning editor (used for Invoke, Element, Key, KeyName).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Default, Flags, DisplayName).
     * @param {string} title - Member title text (unused here).
     * @param {EditOptions} options - Editor options (ReadOnly).
     * @returns {Promise<object>} Editor item {Focus, Title, DefValue, DefValueText, SetValue(v), UpdateValue(isFirst)}.
     *   SetValue writes obj[mn] and dispatches an EditChange event on editor.Element; UpdateValue refreshes the
     *   slider and the EditTrue class and focuses the slider unless isFirst.
     */
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
