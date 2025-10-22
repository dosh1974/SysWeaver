class EditTypeObject {

    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

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

    Condition(obj) {
//        if (obj)
//            delete obj["$type"];
        return obj;
    }

    async Validate(obj, member, options, name) {
        if (obj == null) {
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
            return [name, member.DisplayName + ": Value is null and null is not allowed!"];
        }
        const objectType = await Edit.GetType(member.TypeName, member.ElementInst);
        const err = await Edit.ValidateType(objectType, obj, options, null);
        if (!err)
            return null;
        return [name + "." + err[0], member.DisplayName + "/" + err[1]];
    }


    ToString(obj, member, options) {
        return obj ? JSON.stringify(obj) : null;
    }

    GetDefault(member, options) {
        //  Default
        if (member) {
            let def = member.Default;
            if (def === "\t")
                return {};
            if (Edit.IsValid(def))
                return JSON.parse(def);
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
        }
        return {};
    }

    static CloneDef(def) {
        if (!def)
            return null;
        return JSON.parse(JSON.stringify(def));
        
    }

    async AddEditor(obj, editor, editContext, member, title, options) {
        let typeTitle = null;
        const mn = member.Name;
        const cell = editContext.Element;

        const isReadOnly = options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0);
        const objectType = await Edit.GetType(member.TypeName, member.ElementInst);
        const newOpt = options.Clone();
        newOpt.Title = false;
        newOpt.ReadOnly = isReadOnly;
        newOpt.Name = mn;

        let def = this.GetDefault(member, options);
        let doInit = false;
        if (def && (Object.keys(def).length === 0)) { 
            doInit = true;
            try {
                const nd = await TypeInfoCache.GetInstance(member.TypeName);
                if (nd) {
                    def = nd;
                    doInit = false;
                }
            }
            catch
            {
            }
        }
        if (doInit) {
            if (typeof (def) !== "undefined") {
                if (def) {
                    const itemEdit = new Edit(objectType, editor.Style, newOpt, editor, member);
                    await itemEdit.SetObject({});
                    def = itemEdit.GetObject();
                }
                typeTitle = ValueFormat.AddNonNullLine(typeTitle, "Default value", def ? JSON.stringify(def) : "null");
            }
            else
                def = null;
        }

        if (typeof obj[mn] === "undefined")
            obj[mn] = Edit.CloneType(def);


        cell.classList.add("EditObject");
        const data = editContext.CreateData();
        const dataRow = data.parentElement;
        dataRow.classList.add("DataRow");
        const prevRow = editContext.PrevRow;
        let isExpanded = false;
        let expandIcon = null;
        const countText = document.createElement("SysWeaver-EditInfo");
        Edit.SetToString(countText, obj[mn]);

 
        let itemEdit = null;

        let isInternal = false;

        async function toggleExpand(ev) {
            if (!isPureClick(ev))
                return;
            isExpanded ^= true;
            if (isExpanded) {
                data.innerHTML = "";

                const arrayEntry = document.createElement("SysWeaver-EditEntry");
                const arrayEntryHeader = document.createElement("SysWeaver-EditEntryHeader");
                const arrayEntryValue = document.createElement("SysWeaver-EditEntryValue");
                arrayEntry.appendChild(arrayEntryHeader);
                arrayEntry.appendChild(arrayEntryValue);
                isInternal = true;
                const needNew = !obj[mn];
                itemEdit = new Edit(objectType, editor.Style, newOpt, editor, member);
                await itemEdit.SetObject(obj[mn]);
                if (needNew) {
                    const newObj = itemEdit.GetObject();
                    await editor.Invoke(
                        edit => {
                            edit.SetValue(newObj);
                        },
                        edit => {
                            edit.SetValue(null);
                        },
                        "Create object",
                        mn);
                    Edit.SetToString(countText, newObj);
                }
                itemEdit.Element.addEventListener("EditChange", ev => {
                    Edit.SetToString(countText, obj[mn]);
                });
                isInternal = false;

                arrayEntry.Edit = itemEdit;
                arrayEntryValue.appendChild(itemEdit.Element);
                arrayEntry.Edit = itemEdit;
                data.appendChild(arrayEntry);
                expandIcon.ChangeImage("SysWeaverEditIconCollapse");
                expandIcon.SetTitle("Click to collapse object");
                dataRow.classList.add("EditorExpanded");
                prevRow.classList.add("EditorExpanded");
            } else {
                itemEdit = null;
                data.innerHTML = "";
                expandIcon.ChangeImage("SysWeaverEditIconExpand");
                expandIcon.SetTitle("Click to expand object");
                dataRow.classList.remove("EditorExpanded");
                prevRow.classList.remove("EditorExpanded");
            }
        }
        const isNull = obj[mn] == null;
        const canExpand = !(isReadOnly && isNull);

        expandIcon = new ColorIcon(
            isExpanded ? "SysWeaverEditIconCollapse" : "SysWeaverEditIconExpand",
            "IconColorThemeMain", 36, 36,
            canExpand ? (isExpanded ? "Click to collapse object" : (isNull ? "Click to create a new object and expand it" : "Click to expand object")) : "Can't expand null object", 
            canExpand ? toggleExpand : null
        );
        cell.appendChild(expandIcon.Element);
        cell.appendChild(countText);
        return {
            Title: typeTitle,
            DefValue: def,
            DefValueText: def == null ? null : JSON.stringify(def),
            DataRow: dataRow,
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            UpdateValue: async () => {
                if (isInternal)
                    return;
                const newVal = obj[mn];
                Edit.SetToString(countText, newVal);
                if (isExpanded)
                    await toggleExpand();
                if (newVal != null)
                    await toggleExpand();
            },
            AddMenuItems: (menu, close) => {

                if (isReadOnly)
                    return;
            },
            FindItem: async name => {
                if (!isExpanded)
                    await toggleExpand();
                return await itemEdit.FindItem(name);
            }
        };
    }

}
