/**
 * @file EditTypeLanguage.js - special-type editor plugin for language members (EditTypes.Language = "Language" in
 * SysWeaver.Common/Edit/EditTypes.cs) of the object editor; the value is an ISO 639-1 code string.
 *
 * Loading: not included by Edit.Init(); a page must include this script itself (no in-repo includer was found).
 * When loaded it calls EditTypeLanguage.Init(), which includes ../iso_data/languages.js (IsoLanguage) and
 * EditLanguage.css relative to this script.
 * Plug-in mechanism: Edit.GetTypeHandler() looks up window["EditType" + spec + "Factory"] for members whose
 * member.Type is "Language[:...]" and calls it with (typeName, specParts); the result is cached in the type map.
 *
 * Globals defined: function EditTypeLanguageFactory, class EditTypeLanguage.
 * Dependencies: SearchablePoupSelection, badClick, keyboardClick, createImg (app/application.js, common),
 * IsoLanguage (iso_data/languages.js), Edit (IsValid, CreateChangeEvent), ValueFormat.AddNonNullLine,
 * TypeMemberFlags (type.js), includeJs/includeCss.
 */

/**
 * Factory used by Edit.GetTypeHandler() for member.Type "Language". Creates a new handler per call
 * (Edit caches it per "typeName|spec").
 * @param {string} typename - .NET type name of the member; must be "System.String".
 * @returns {EditTypeLanguage} New handler (throws for other types).
 */
function EditTypeLanguageFactory(typename) {
    return new EditTypeLanguage(typename);
}



/**
 * Type handler for language codes. Renders a clickable SysWeaver-EditLanguage element (flag image + language name)
 * that opens a searchable language popup.
 */
class EditTypeLanguage {


    /**
     * Loads the dependencies of this plugin: ../iso_data/languages.js and EditLanguage.css (relative to this script).
     * Called once at the end of this file.
     * @returns {Promise<void>} Resolves when both are loaded.
     */
    static async Init() {
        const current = document.currentScript.src;
        await Promise.all([
            includeJs(current, "../iso_data/languages.js"),
            includeCss(current, "EditLanguage.css")
        ]);
    }

    /**
     * Shared language picker popup (SearchablePoupSelection with id "Language", recent ids = Iso639_1, up to 10 recent).
     * Each row is a SysWeaver-EditLangSel element (class Recent for recently used languages) with a flag image from
     * ../iso_data/language/<iso>.svg and a SysWeaver-EditLangText name. The find function filters
     * IsoLanguage.Languages by the lower-cased search text, ranks matches (see rank) plus recent-use rank,
     * stores the result in data.Rank, sorts, and applies offset / maxCount paging.
     * @type {SearchablePoupSelection}
     */
    static LanguageSel = new SearchablePoupSelection(
        "Language",
        data => data.Iso639_1,
        (data, onSelectFn, isRecent) => {
            const ls = document.createElement("SysWeaver-EditLangSel");
            ls.tabIndex = "0";
            ls.onclick = async ev => {
                if (badClick(ev))
                    return;
                await onSelectFn(data);
            };
            if (isRecent)
                ls.classList.add("Recent");
            ls.title = "Name: " + data.Name + ".\nISO 639-1 language code: " + data.Iso639_1 + ".";
            keyboardClick(ls);
            const img = createImg("img");
            img.src = "../iso_data/language/" + data.Iso639_1 + ".svg";
            ls.appendChild(img);

            const text = document.createElement("SysWeaver-EditLangText");
            ls.appendChild(text);
            text.innerText = data.Name;
            return ls;
        },
        (text, recent, maxCount, offset) => {
            text = text.toLowerCase();
            const res = [];
            const src = IsoLanguage.Languages;
            const sl = src.length;
            /**
             * Ranks a language against the search text (lower is better). Note: the variable names are swapped -
             * isoIndex holds the position in the name and nameIndex the position in the ISO code.
             * @param {{Name:string, Iso639_1:string}} val - Language entry.
             * @returns {number} 0 without search text; 1000000 when neither name nor code matches; otherwise
             *   namePos (4 if no name match) + codePos * 4 (codePos 1000 if no code match).
             */
            const rank = val => {
                if (!text)
                    return 0;
                let isoIndex = val.Name.toLowerCase().indexOf(text);
                let nameIndex = val.Iso639_1.toLowerCase().indexOf(text);
                if (isoIndex < 0) {
                    isoIndex = 4;
                    if (nameIndex < 0)
                        return 1000000;
                }
                if (nameIndex < 0)
                    nameIndex = 1000;
                return isoIndex + nameIndex * 4;
            };
            for (let i = 0; i < sl; ++i) {
                const data = src[i];
                let r = rank(data);
                if (r >= 1000000)
                    continue;
                r += 1000000;
                let rc = recent.get(data.Iso639_1);
                if (!rc)
                    rc = 9999999;
                r += rc;
                data.Rank = r;
                res.push(data);
            }
            res.sort((a, b) => a.Rank - b.Rank);
            if (offset > 0)
                res.splice(0, offset);
            const delLength = res.length - maxCount;
            if (delLength > 0)
                res.splice(maxCount, delLength);
            return res;
        }, 10);

        


    /**
     * @param {string} typename - .NET type name; throws a string error unless it is "System.String".
     */
    constructor(typename) {
        if (typename != "System.String")
            throw "Invalid type \"" + typename + "\"! - Language is only supported for strings!";
    }

    /**
     * Creates the default value for a new member.
     * @param {object} member - Member description.
     * @param {EditOptions} options - Editor options.
     * @returns {?string} Same as GetDefault.
     */
    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    /**
     * Checks whether a pasted / dropped value could be a language code.
     * @param {*} obj - Candidate value.
     * @returns {boolean} true for anything that is neither undefined nor an object.
     */
    IsOfType(obj) {
        const t = typeof (obj);
        return (t !== "undefined") && (t !== "object");
    }

    /**
     * Converts a value to an ISO 639-1 code via IsoLanguage.Get, falling back to "en" when unknown.
     * @param {*} obj - Value ("" + obj is looked up).
     * @param {object} member - Member description (unused).
     * @param {EditOptions} options - Editor options (unused).
     * @returns {string} The ISO 639-1 code.
     */
    Condition(obj, member, options) {
        obj = "" + obj;
        obj = IsoLanguage.Get(obj);
        obj = obj ? obj.Iso639_1 : "en";
        return obj;
    }

    /**
     * Validates a value: null only with TypeMemberFlags.AcceptNull; otherwise IsoLanguage.Get must find it and its
     * Iso639_1 must equal the value exactly.
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
            return [name, member.DisplayName + ": Value may not be null"];
        }
        obj = "" + obj;
        const iso = IsoLanguage.Get(obj);
        if (!iso)
            return [name, member.DisplayName + ": Value must be a valid two letter ISO 639-1 language code"];
        if (iso.Iso639_1 != obj)
            return [name, member.DisplayName + ": Value must be a valid two letter ISO 639-1 language code"];
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
     * @returns {?string} member.Default when Edit.IsValid ("\t" maps to "en"), else null with AcceptNull, else "en".
     */
    GetDefault(member, options) {
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            return def === "\t" ? "en" : def;
        if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
            return null;
        return "en";
    }

    /**
     * Builds the language editor: a SysWeaver-EditLanguage element with a flag img and a SysWeaver-EditLangText.
     * Unless read only (options.ReadOnly, TypeMemberFlags.ReadOnly or this.ForceReadOnly, which is never set and
     * thus undefined), it gets tabIndex 0, the class Enabled and a click handler that opens
     * EditTypeLanguage.LanguageSel ("Select language", 25 visible rows); a selection goes through editor.Invoke
     * with the text element as tracking object. obj[member.Name] is set to the default when undefined.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - Owning editor (Invoke, Element, Key, KeyName).
     * @param {{Element:HTMLElement}} editContext - Layout context; Element is the value cell.
     * @param {object} member - Member description (Name, Default, Flags).
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

        const def = this.GetDefault(member, options);
        const defText = def == null ? null : ('"' + def + '"');
        typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", defText);

        if (typeof obj[mn] === "undefined")
            obj[mn] = def;

        const e = document.createElement("SysWeaver-EditLanguage");
        if (!readOnly) {
            e.tabIndex = "0";
            e.classList.add("Enabled");
            e.onclick = async ev => {
                await EditTypeLanguage.LanguageSel.Show("Select language", async newLang => {
                    const v = newLang.Iso639_1;
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
                }, 25);
            };
            keyboardClick(e);
        }

        const icon = createImg("img");
        e.appendChild(icon);

        const text = document.createElement("SysWeaver-EditLangText");
        e.appendChild(text);

        /**
         * Refreshes the flag icon, the text and the tooltips from obj[mn]: "None" (no icon) for a falsy value,
         * "Invalid!" with ../iso_data/icons/unknown.svg for an unknown code, otherwise the flag
         * ../iso_data/language/<iso>.svg and the language name. Adds a "Click to select" hint when editable.
         * @returns {void}
         */
        const updateValue = () => {
            const val = obj[mn];
            let title = "";
            if (!val) {
                icon.src = "";
                text.innerText = "None";
                title = "No language selected (null).";
            } else {
                const iso = IsoLanguage.Get(val);
                if (!iso) {
                    icon.src = "../iso_data/icons/unknown.svg";
                    text.innerText = "Invalid!";
                    title = '"' + val + '" is not a valid two letter ISO 639-1 language code!';
                } else {
                    icon.src = "../iso_data/language/" + iso.Iso639_1 + ".svg";
                    text.innerText = iso.Name;
                    title = "ISO 639-1 language code: " + iso.Iso639_1;
                }
            }
            if (!readOnly)
                title += "\n\nClick to select another language.";
            icon.title = title;
            text.title = title;
        };
        updateValue();
        cell.appendChild(e);



        return {
            Focus: e,
            Title: typeTitle,
            DefValue: def,
            DefValueText: defText,
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            UpdateValue: isFirst => {
                updateValue();
                if (!isFirst)
                    e.focus();
            },
        };
    }

}

// Load languages.js and EditLanguage.css as soon as this script is evaluated (document.currentScript is only valid now).
EditTypeLanguage.Init();

