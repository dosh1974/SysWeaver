/**
 * @file Plug-in handler for string members with the `Language` edit type (`[EditType(EditTypes.Language)]`), an ISO 639-1
 * language picker (`Sw.Edit.Handlers.Language`, legacy globals `EditTypeLanguage`, `EditTypeLanguageFactory`).
 *
 * Loaded on demand by Edit.js when a member uses the spec (or included by a page). Loads `../iso_data/languages.js`
 * (IsoLanguage) and EditLanguage.css. The value shows the flag and the language name ("None" when empty, an unknown icon and
 * "Invalid!" for unknown codes); click / Enter / Space opens a searchable, paged selection ranked by name / code match and
 * recent use (recent picks are stored in localStorage "SysWeaver.PopUpRecent.Language").
 * Without the IsoDataService (Api/HaveService probe, as ui/cells.js) nothing under iso_data/ is requested (no 404): the value is
 * edited as a plain two letter code (text box, a-z only) and validated by its form. Read only: the flag and name as plain text.
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;
    const Url = document.currentScript ? document.currentScript.src : E.ScriptUrl;
    /** @type {string} Folder of the language flag icons. */
    const IsoBase = new URL("../iso_data/", Url).href;

    /**
     * Find a language.
     * @param {string} code - ISO code or name.
     * @returns {?object} The IsoLanguage.
     */
    function iso(code) {
        const L = lang();
        return L && code ? L.Get(code) : null;
    }

    /**
     * The IsoLanguage class (iso_data/languages.js), null when not loaded (no IsoDataService).
     * @returns {?object} The class.
     */
    function lang() {
        return typeof IsoLanguage !== "undefined" ? IsoLanguage : (window.IsoLanguage || null);
    }

    /**
     * Is the IsoDataService running (it serves iso_data/)? One Api/HaveService probe per page; a failing probe counts as present.
     * @returns {Promise<boolean>} True when iso_data/ can be loaded.
     */
    async function haveIsoData() {
        if (lang())
            return true;
        try {
            return !!(await Sw.Net.Send(Sw.Url.Root + "Api/HaveService", "SysWeaver.MicroService.IsoDataService", { NoNavigate: true }));
        }
        catch {
            return true;
        }
    }

    /** A plain ISO 639-1 code ("en"): the check without the language list. */
    const CodeRe = /^[a-z]{2}$/;

    /**
     * Language handler (strings only).
     */
    class EditTypeLanguage extends E.Handler {
        /**
         * @param {string} typename - The member type (must be System.String).
         */
        constructor(typename) {
            super();
            if (typename && typename !== "System.String")
                throw new Error("Invalid type \"" + typename + "\"! - Language is only supported for strings!");
        }

        /**
         * Accept texts.
         * @param {*} obj - Candidate.
         * @returns {boolean} True for non objects.
         */
        IsOfType(obj) {
            const t = typeof obj;
            return t !== "undefined" && t !== "object";
        }

        /**
         * Convert a value to an ISO 639-1 code (unknown -> "en").
         * @param {*} obj - The value.
         * @returns {string} The code.
         */
        Condition(obj) {
            if (!lang()) {
                //  No language list: keep a code as it is (lower case), never replace it by "en"
                const s = ("" + obj).trim().toLowerCase();
                return CodeRe.test(s) ? s : "en";
            }
            const l = iso("" + obj);
            return l ? l.Iso639_1 : "en";
        }

        /**
         * Validate a two letter ISO 639-1 code (null only with AcceptNull).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            if (obj === null || obj === undefined)
                return E.Handler.NullCheck(obj, member, name);
            const l = iso("" + obj);
            if (!lang() ? !CodeRe.test("" + obj) : (!l || l.Iso639_1 !== "" + obj))
                return [name, (member.DisplayName || member.Name) + ": " + _TF("Value must be a valid two letter ISO 639-1 language code", "Validation error of a language code")];
            return null;
        }

        /**
         * The default: metadata ("\t" = "en"), else null with AcceptNull, else "en".
         * @param {object} member - The member.
         * @returns {?string} The default.
         */
        GetDefault(member) {
            if (Has(member.Default))
                return member.Default === "\t" ? "en" : member.Default;
            return (member.Flags & F.AcceptNull) !== 0 ? null : "en";
        }

        /**
         * Show the language selection.
         * @returns {Promise<?object>} The picked IsoLanguage (null when dismissed).
         */
        static Pick() {
            if (!EditTypeLanguage.Selection) {
                EditTypeLanguage.Selection = new Sw.SearchSelection({
                    Id: "Language",
                    GetId: d => d.Iso639_1,
                    MaxRecent: 10,
                    Render: (d, select, isRecent) => {
                        const b = Sw.el("button.sw-edit-langsel", {
                            type: "button", title: _T("Name: {0}, ISO 639-1 language code: {1}", d.Name, d.Iso639_1, "Tool tip of a language in a language selection.{0} is replaced with the language name.{1} is replaced with the two letter code."),
                            attrs: { role: "option" },
                        }, Sw.el("img", { src: IsoBase + "language/" + d.Iso639_1 + ".svg", alt: "", loading: "lazy" }), Sw.el("span", { text: d.Name }), Sw.el("span.sw-edit-langcode", { text: d.Iso639_1 }));
                        if (isRecent)
                            b.classList.add("Recent");
                        b.addEventListener("click", () => select(d));
                        return b;
                    },
                    Find: async (text, recent, maxCount, offset) => {
                        text = (text || "").trim().toLowerCase();
                        const res = [];
                        for (const d of (typeof IsoLanguage !== "undefined" ? IsoLanguage : window.IsoLanguage).Languages) {
                            let r = 0;
                            if (text) {
                                const ni = d.Name.toLowerCase().indexOf(text);
                                const ci = d.Iso639_1.toLowerCase().indexOf(text);
                                if (ni < 0 && ci < 0)
                                    continue;
                                r = (ci === 0 ? 0 : (ni < 0 ? 1000 : ni)) + (ci < 0 ? 4000 : ci * 4);
                            }
                            r = r * 100 + (recent.get(d.Iso639_1) || 99);
                            res.push({ D: d, R: r });
                        }
                        res.sort((a, b) => a.R - b.R);
                        return res.slice(offset, offset + maxCount).map(x => x.D);
                    },
                });
            }
            return EditTypeLanguage.Selection.Show({ Title: _TF("Select language", "Title of a dialog where a language is selected"), PageSize: 25 });
        }

        /**
         * Build the language button.
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
            const def = this.GetDefault(member);
            if (obj[mn] === undefined)
                obj[mn] = def;
            const defTitle = E.Util.AddLine(null, _TF("Default value", "Header of a tool tip row with the default value of a property"), def === null ? "null" : ('"' + def + '"'));
            if (!lang()) {
                //  No language list (no IsoDataService): a plain code, no flags (nothing under iso_data/ is requested)
                const set = async (v, ev, token) => {
                    const old = obj[mn];
                    if (old === v)
                        return;
                    await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", v, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
                };
                const tb = E.CreateTextInput(() => obj[mn], set, s => (s = ("" + s).trim().toLowerCase()) ? s : null, v => v ?? "",
                    _TF("Two letter ISO 639-1 code, ex: en", "Placeholder of a language code text box (shown when the language list is not available)"), null, "abcdefghijklmnopqrstuvwxyz", 2, def, readOnly);
                tb.classList.add("sw-edit-langcode-input");
                tb.setAttribute("aria-label", member.DisplayName || mn);
                if (readOnly && !obj[mn])
                    tb.placeholder = "null";
                editContext.Element.append(tb);
                return E.Handler.Item(obj, editor, member, { Focus: tb, Title: defTitle, DefValue: def, DefValueText: def === null ? "null" : def, Update: () => tb.UpdateValue() });
            }
            const img = Sw.el("img", { alt: "" });
            const text = Sw.el("span.sw-edit-langtext");
            //  Read only: the flag and the name as plain text (focusable, not a button)
            const b = readOnly
                ? Sw.el("span.sw-edit-language.sw-edit-language-ro.EditReadOnly", { tabIndex: 0, attrs: { role: "textbox", "aria-readonly": "true" } }, img, text)
                : Sw.el("button.sw-edit-language.SysWeaver-EditLanguage", { type: "button" }, img, text);
            if (readOnly)
                b.setAttribute("aria-label", member.DisplayName || mn);
            const update = () => {
                const v = obj[mn];
                let tip;
                if (!v) {
                    img.removeAttribute("src");
                    img.hidden = true;
                    text.textContent = _TF("None", "Shown when no language is selected");
                    tip = _TF("No language selected (null).", "Tool tip when no language is selected");
                }
                else {
                    const l = iso(v);
                    img.hidden = false;
                    if (!l) {
                        img.src = IsoBase + "icons/unknown.svg";
                        text.textContent = _TF("Invalid!", "Shown when a language code is unknown");
                        tip = _T("\"{0}\" is not a valid two letter ISO 639-1 language code!", v, "Tool tip of an invalid language code.{0} is replaced with the code.");
                    }
                    else {
                        img.src = IsoBase + "language/" + l.Iso639_1 + ".svg";
                        text.textContent = l.Name;
                        tip = _T("ISO 639-1 language code: {0}", l.Iso639_1, "Tool tip of a language.{0} is replaced with the two letter code.");
                    }
                }
                if (!readOnly)
                    tip += "\n\n" + _TF("Select another language.", "Tool tip of a button that opens a language selection");
                b.title = tip;
            };
            update();
            if (!readOnly) {
                b.addEventListener("click", async () => {
                    const l = await EditTypeLanguage.Pick();
                    if (!l)
                        return;
                    const old = obj[mn];
                    if (old === l.Iso639_1)
                        return;
                    await editor.SetMemberValue(member, l.Iso639_1, old, _T("Set value: \"{0}\"", l.Iso639_1, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."));
                });
            }
            editContext.Element.append(b);
            return E.Handler.Item(obj, editor, member, {
                Focus: b, Title: defTitle,
                DefValue: def, DefValueText: def === null ? "null" : def, Update: update,
            });
        }
    }

    /**
     * The plug-in factory (old hook name).
     * @param {string} typename - The member type.
     * @returns {EditTypeLanguage} The handler.
     */
    function EditTypeLanguageFactory(typename) {
        return new EditTypeLanguage(typename);
    }

    E.Handlers.Language = EditTypeLanguage;
    //  The language list only when the IsoDataService serves it (no 404 / console error without it; the handler then edits codes)
    E.PluginReady.Language = Promise.all([
        haveIsoData().then(have => have && !lang() ? Sw.Net.IncludeJs(Url, "../iso_data/languages.js", true) : null),
        Sw.Net.IncludeCss(Url, "EditLanguage.css", true),
    ]);
    E.RegisterSpec("Language", EditTypeLanguageFactory);
    if (!window.EditTypeLanguage)
        window.EditTypeLanguage = EditTypeLanguage;
    if (!window.EditTypeLanguageFactory)
        window.EditTypeLanguageFactory = EditTypeLanguageFactory;
})();
