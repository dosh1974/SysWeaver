/**
 * @file Type handler for System.Boolean (and Nullable<Boolean>) members (`Sw.Edit.Handlers.Boolean`, legacy global
 * `EditTypeBoolean`).
 *
 * Loaded by Edit.js. A Sw.Toggle switch (click / tap, Space / Enter); the value cell gets class EditTrue while true. Read only: "Yes" / "No" as plain text (no switch).
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const Has = E.Util.Has;

    /**
     * Boolean handler.
     */
    class EditTypeBoolean extends E.Handler {
        /**
         * @param {boolean} [nullable] - Nullable<Boolean> (edited like a boolean; null shows as false).
         */
        constructor(nullable) {
            super();
            /** @type {boolean} Nullable variant. */
            this.Nullable = !!nullable;
        }

        /**
         * Accept booleans, "true" / "false" and 0 / 1.
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj) {
            const t = typeof obj;
            if (t === "boolean")
                return true;
            if (t === "string")
                return obj === "true" || obj === "false" || obj === "True" || obj === "False";
            if (t === "number")
                return obj === 0 || obj === 1;
            return false;
        }

        /**
         * Convert an accepted value to a boolean.
         * @param {*} obj - The value.
         * @returns {boolean} The boolean.
         */
        Condition(obj) {
            if (typeof obj === "string")
                return obj.toLowerCase() === "true";
            return obj === true || obj === 1;
        }

        /**
         * Validate: booleans (null only with AcceptNull / nullable).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            if (obj === null)
                return this.Nullable ? null : E.Handler.NullCheck(obj, member, name);
            if (typeof obj === "boolean" || this.IsOfType(obj))
                return null;
            return [name, (member.DisplayName || member.Name) + ": " + _T("The value \"{0}\" is not a valid boolean value, expecting: true or false", obj, "Validation error of a boolean value.{0} is replaced with the invalid value.")];
        }

        /**
         * Display text.
         * @param {*} obj - The value.
         * @returns {string} "true" / "false".
         */
        ToString(obj) {
            return obj ? "true" : "false";
        }

        /**
         * The default (metadata "1" / "true" / "True", else false).
         * @param {object} member - The member.
         * @returns {boolean} The default.
         */
        GetDefault(member) {
            return Has(member.Default) ? E.IsTrue(member.Default) : false;
        }

        /**
         * Build the toggle.
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
            const cell = editContext.Element;
            const def = this.GetDefault(member);
            const hasDef = Has(member.Default);
            if (typeof obj[mn] !== "boolean")
                obj[mn] = obj[mn] === undefined || obj[mn] === null || typeof obj[mn] === "object" ? def : this.Condition(obj[mn]);
            if (this.IsReadOnly(member, options)) {
                //  Read only (Design v2, PORTING.md B14): "Yes" / "No" as plain text, not a switch that looks live
                const ro = E.ReadOnlyText(() => obj[mn], v => v ? _TF("Yes", "Read only display of a true boolean value") : _TF("No", "Read only display of a false boolean value"), false);
                ro.classList.add("sw-edit-robool");
                ro.setAttribute("aria-label", member.DisplayName || mn);
                cell.classList.add("EditBoolean");
                const rosync = () => {
                    ro.UpdateValue();
                    cell.classList.toggle("EditTrue", !!obj[mn]);
                };
                rosync();
                cell.append(ro);
                return E.Handler.Item(obj, editor, member, {
                    Focus: ro,
                    Title: hasDef ? E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), def) : null,
                    DefValue: def, DefValueText: "" + def,
                    Update: rosync,
                });
            }
            const tog = new Sw.Toggle({
                Value: !!obj[mn], Title: member.DisplayName || mn, Disabled: this.IsReadOnly(member, options),
                OnChange: async v => {
                    const old = obj[mn];
                    if (old === v)
                        return;
                    await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", v, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."));
                },
            });
            tog.E.removeAttribute("title");
            cell.classList.add("EditBoolean");
            const sync = () => {
                tog.Set(!!obj[mn], true);
                cell.classList.toggle("EditTrue", !!obj[mn]);
            };
            sync();
            cell.append(tog.E);
            return E.Handler.Item(obj, editor, member, {
                Focus: tog.E,
                Title: hasDef ? E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), def) : null,
                DefValue: def, DefValueText: "" + def,
                Update: sync,
            });
        }
    }

    E.Handlers.Boolean = EditTypeBoolean;
    if (!window.EditTypeBoolean)
        window.EditTypeBoolean = EditTypeBoolean;
})();
