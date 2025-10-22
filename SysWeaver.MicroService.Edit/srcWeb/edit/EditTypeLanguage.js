

function EditTypeLanguageFactory(typename) {
    return new EditTypeLanguage(typename);
}



class EditTypeLanguage {


    static async Init() {
        const current = document.currentScript.src;
        await Promise.all([
            includeJs(current, "../iso_data/languages.js"),
            includeCss(current, "EditLanguage.css")
        ]);
    }

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
            const img = document.createElement("img");
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

        


    constructor(typename) {
        if (typename != "System.String")
            throw "Invalid type \"" + typename + "\"! - Language is only supported for strings!";
    }

    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    IsOfType(obj) {
        const t = typeof (obj);
        return (t !== "undefined") && (t !== "object");
    }

    Condition(obj, member, options) {
        obj = "" + obj;
        obj = IsoLanguage.Get(obj);
        obj = obj ? obj.Iso639_1 : "en";
        return obj;
    }

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


    ToString(obj, member, options) {
        return "" + obj;
    }

    GetDefault(member, options) {
        //  Default
        let def = member.Default;
        if (Edit.IsValid(def))
            return def === "\t" ? "en" : def;
        if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
            return null;
        return "en";
    }

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

        const icon = document.createElement("img");
        e.appendChild(icon);

        const text = document.createElement("SysWeaver-EditLangText");
        e.appendChild(text);

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

EditTypeLanguage.Init();

