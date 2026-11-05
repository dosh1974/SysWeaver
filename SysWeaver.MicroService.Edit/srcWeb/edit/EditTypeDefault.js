/**
 * @file Fallback type handler for members no other handler supports (`Sw.Edit.Handlers.Default`, legacy global
 * `EditTypeDefault`).
 *
 * Loaded by Edit.js. Shows the value as read only text (`"" + value`, objects as JSON); validation only checks null against
 * AcceptNull.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;

    /**
     * Read only fallback handler.
     */
    class EditTypeDefault extends E.Handler {
        /** Create the handler (always read only). */
        constructor() {
            super();
            /** @type {boolean} Always read only. */
            this.ForceReadOnly = true;
        }

        /**
         * Accept an object with a matching $type (non primitive members only).
         * @param {*} obj - Candidate.
         * @param {object} member - The member.
         * @returns {boolean} True when acceptable.
         */
        IsOfType(obj, member) {
            return !E.IsPrimitive(member) && obj !== null && typeof obj === "object" && obj.$type === member.TypeName;
        }

        /**
         * Validate null against AcceptNull.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            return E.Handler.NullCheck(obj, member, name);
        }

        /**
         * The default: metadata, else null with AcceptNull, else {}.
         * @param {?object} member - The member.
         * @returns {*} The default.
         */
        GetDefault(member) {
            if (member) {
                if (Has(member.Default))
                    return member.Default;
                if ((member.Flags & F.AcceptNull) !== 0)
                    return null;
            }
            return {};
        }

        /**
         * Text of a value.
         * @param {*} v - The value.
         * @returns {string} The text.
         */
        static Text(v) {
            if (v === null || v === undefined)
                return "";
            return typeof v === "object" ? JSON.stringify(v) : ("" + v);
        }

        /**
         * Build the read only text.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @returns {Promise<object>} The item (no SetValue changes from the UI).
         */
        async AddEditor(obj, editor, editContext, member) {
            const mn = member.Name;
            const def = Has(member.Default) ? member.Default : undefined;
            if (obj[mn] === undefined)
                obj[mn] = def ?? null;
            const text = E.CreateTextInput(() => obj[mn], null, null, EditTypeDefault.Text, member.Summary, null, null, 0, def, true);
            text.setAttribute("aria-label", member.DisplayName || mn);
            editContext.Element.append(text);
            return E.Handler.Item(obj, editor, member, {
                Focus: text, Title: def !== undefined ? E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), def) : null,
                DefValue: undefined, DefValueText: def,
                Update: () => text.UpdateValue(),
            });
        }
    }

    E.Handlers.Default = EditTypeDefault;
    if (!window.EditTypeDefault)
        window.EditTypeDefault = EditTypeDefault;
})();
