/**
 * @file Type handler for System.String (and, read only, System.Object) members (`Sw.Edit.Handlers.String`, legacy global
 * `EditTypeString`).
 *
 * Loaded by Edit.js. A single line input, a resizable textarea (Multiline flag) or a password input (Password flag).
 * A member Default of "\t" is the server's marker for an empty string.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;
    const addLine = E.Util.AddLine;

    /**
     * Text handler.
     */
    class EditTypeString extends E.Handler {
        /**
         * @param {boolean} [forceReadOnly] - Always read only (System.Object).
         */
        constructor(forceReadOnly) {
            super();
            /** @type {boolean} Always read only. */
            this.ForceReadOnly = !!forceReadOnly;
        }

        /**
         * Accept anything that is not an object (null is rejected here, the engine accepts null with AcceptNull).
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable as text.
         */
        IsOfType(obj) {
            const t = typeof obj;
            return t !== "undefined" && t !== "object";
        }

        /**
         * Convert a pasted / dropped value: text, truncated to Max, line breaks become spaces unless multi line.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @returns {string} The text.
         */
        Condition(obj, member) {
            obj = "" + obj;
            const max = E.GetIntParam(member.Max, 0);
            if (max > 0 && obj.length > max)
                obj = obj.substring(0, max);
            if (!EditTypeString.IsMultiline(member))
                obj = obj.replace(/(\r\n|\n|\r)/gm, " ");
            return obj;
        }

        /**
         * Test for a multi line (non password) member.
         * @param {object} member - The member.
         * @returns {boolean} True for a textarea.
         */
        static IsMultiline(member) {
            return (member.Flags & F.Multiline) !== 0 && (member.Flags & F.Password) === 0;
        }

        /**
         * Validate: null only with AcceptNull; length within Min..Max.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            if (obj === null || obj === undefined)
                return E.Handler.NullCheck(obj, member, name);
            const l = ("" + obj).length;
            const dn = member.DisplayName || member.Name;
            const max = E.GetIntParam(member.Max, 0);
            if (max > 0 && l > max)
                return [name, dn + ": " + _T("Text too long: at most {0} characters are allowed, the text has {1}.", max, l, "Validation error of a text value that is too long.{0} is replaced with the maximum number of characters.{1} is replaced with the current number of characters.")];
            const min = E.GetIntParam(member.Min, 0);
            if (min > 0 && l < min)
                return [name, dn + ": " + _T("Text too short: at least {0} characters are needed, the text has {1}.", min, l, "Validation error of a text value that is too short.{0} is replaced with the minimum number of characters.{1} is replaced with the current number of characters.")];
            return null;
        }

        /**
         * The default: metadata ("\t" = empty), else null with AcceptNull, else "".
         * @param {object} member - The member.
         * @returns {?string} The default.
         */
        GetDefault(member) {
            const def = member.Default;
            if (Has(def))
                return def === "\t" ? "" : def;
            if ((member.Flags & F.AcceptNull) !== 0)
                return null;
            return "";
        }

        /**
         * Build the editor.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item.
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const mn = member.Name;
            const readOnly = this.IsReadOnly(member, options);
            const min = E.GetIntParam(member.Min, 0);
            const max = E.GetIntParam(member.Max, 0);
            let tip = null;
            if (min > 0)
                tip = addLine(tip, _TF("Minimum length", "Header of a tool tip row with the minimum number of characters of a text"), min);
            if (max > 0)
                tip = addLine(tip, _TF("Maximum length", "Header of a tool tip row with the maximum number of characters of a text"), max);
            const def = this.GetDefault(member);
            const defText = def === null ? "null" : ('"' + def + '"');
            tip = addLine(tip, _TF("Default value", "Header of a tool tip row with the default value of a property"), defText);
            if (obj[mn] === undefined)
                obj[mn] = def;
            const isPassword = (member.Flags & F.Password) !== 0;
            const multi = EditTypeString.IsMultiline(member);
            const text = E.CreateTextInput(
                () => obj[mn] ?? "",
                async (v, ev, token) => {
                    const old = obj[mn];
                    if (old === v || (old === null && v === ""))
                        return;
                    await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", isPassword ? "***" : v, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
                },
                v => v, v => (v === null || v === undefined) ? "" : ("" + v),
                member.Summary, null, null, max > 0 ? max : null, def, readOnly, multi ? 3 : 0);
            if (isPassword) {
                text.type = "password";
                text.autocomplete = "new-password";
            }
            if (obj[mn] === null && (member.Flags & F.AcceptNull) !== 0)
                text.placeholder = "null";
            text.setAttribute("aria-label", member.DisplayName || mn);
            editContext.Element.append(text);
            return E.Handler.Item(obj, editor, member, {
                Focus: text, Title: tip, DefValue: def, DefValueText: defText,
                Update: () => text.UpdateValue(),
            });
        }
    }

    E.Handlers.String = EditTypeString;
    if (!window.EditTypeString)
        window.EditTypeString = EditTypeString;
})();
