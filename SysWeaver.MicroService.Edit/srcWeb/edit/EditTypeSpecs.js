/**
 * @file Small string editor specs (`EditTypes.Email`, `EditTypes.Url`, `EditTypes.Phone` in C#, member spec `Email` / `Url` /
 * `Phone`): the string editor with the matching on screen keyboard (inputmode), no spell check / auto capitalization, and a
 * format check (validation and a red box while the text is not valid). Empty texts are valid (use EditMin for required values).
 * Before, these specs fell back to the plain string editor.
 *
 * Loaded by Edit.js. The handlers derive from `Sw.Edit.Handlers.String` when they are first used (the files load in parallel).
 * Classes: `Sw.Edit.Handlers.Email`, `Url`, `Phone` (created on first use); the factories are registered with RegisterSpec.
 */
(function () {
    "use strict";

    const E = Sw.Edit;

    /**
     * Spec descriptions: the input mode, autocomplete token and the check.
     * @type {Object<string, {InputMode: string, Test: function(string):boolean, Error: function(string):string, Placeholder: function():string}>}
     */
    const Specs = {
        Email: {
            InputMode: "email",
            Test: s => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s),
            Error: v => _T("\"{0}\" is not a valid e-mail address", v, "Validation error of an e-mail address value.{0} is replaced with the invalid value."),
            Placeholder: () => _TF("name@example.com", "Placeholder of the text box of an e-mail address value"),
        },
        Url: {
            InputMode: "url",
            //  Absolute urls must parse; relative paths ("mcp", "auth/Login.html?to={0}") are allowed, white space is not
            Test: s => {
                if (/\s/.test(s))
                    return false;
                if (!/^[a-z][a-z0-9+.-]*:/i.test(s))
                    return true;
                try {
                    return !!new URL(s);
                }
                catch {
                    return false;
                }
            },
            Error: v => _T("\"{0}\" is not a valid url", v, "Validation error of an url value.{0} is replaced with the invalid value."),
            Placeholder: () => _TF("https://example.com/path", "Placeholder of the text box of an url value"),
        },
        Phone: {
            InputMode: "tel",
            Test: s => /^\+?[0-9][0-9 ()\-.]{3,}$/.test(s),
            Error: v => _T("\"{0}\" is not a valid phone number (digits, spaces, - and a leading +)", v, "Validation error of a phone number value.{0} is replaced with the invalid value."),
            Placeholder: () => _TF("+1 555 0100", "Placeholder of the text box of a phone number value"),
        },
    };

    /** @type {Object<string, function>} Spec name -> handler class (created on first use). */
    const Classes = {};

    /**
     * The handler class of a spec (a subclass of the string handler).
     * @param {string} name - The spec.
     * @returns {function} The class.
     */
    function specClass(name) {
        if (Classes[name])
            return Classes[name];
        const spec = Specs[name];
        class EditTypeSpecString extends E.Handlers.String {
            /**
             * Validate: the string rules, then the format of a non empty text.
             * @param {*} obj - The value.
             * @param {object} member - The member.
             * @param {object} options - Options.
             * @param {string} path - Path.
             * @returns {Promise<?Array<string>>} Null or [path, message].
             */
            async Validate(obj, member, options, path) {
                const r = await super.Validate(obj, member, options, path);
                if (r || obj === null || obj === undefined || obj === "")
                    return r;
                return spec.Test(("" + obj).trim()) ? null : [path, (member.DisplayName || member.Name) + ": " + spec.Error(obj)];
            }

            /**
             * The string editor with the spec's keyboard and a live format check.
             * @param {object} obj - The object.
             * @param {Sw.Edit} editor - The editor.
             * @param {object} editContext - Layout context.
             * @param {object} member - The member.
             * @param {string} title - Tooltip.
             * @param {object} options - Options.
             * @returns {Promise<object>} The item.
             */
            async AddEditor(obj, editor, editContext, member, title, options) {
                const item = await super.AddEditor(obj, editor, editContext, member, title, options);
                const i = item && item.Focus;
                if (i && i.tagName === "INPUT") {
                    i.inputMode = spec.InputMode;
                    i.autocapitalize = "off";
                    i.setAttribute("autocorrect", "off");
                    i.spellcheck = false;
                    i.classList.add("sw-edit-" + name.toLowerCase());
                    if (!i.readOnly && !i.placeholder)
                        i.placeholder = spec.Placeholder();
                    const check = () => {
                        const s = i.value.trim();
                        const bad = !!s && !spec.Test(s);
                        i.classList.toggle("sw-invalid", bad);
                        i.setAttribute("aria-invalid", bad ? "true" : "false");
                        i.title = bad ? spec.Error(s) : "";
                    };
                    i.addEventListener("input", check);
                    check();
                    const upd = item.UpdateValue;
                    item.UpdateValue = isFirst => {
                        upd(isFirst);
                        check();
                    };
                }
                return item;
            }
        }
        Classes[name] = EditTypeSpecString;
        E.Handlers[name] = EditTypeSpecString;
        return EditTypeSpecString;
    }

    for (const name of Object.keys(Specs))
        E.RegisterSpec(name, typeName => typeName === "System.String" ? new (specClass(name))() : null);
})();
