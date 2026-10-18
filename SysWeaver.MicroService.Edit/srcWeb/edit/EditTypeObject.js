/**
 * @file Type handler for nested (complex) objects in the object editor.
 *
 * Loaded on demand by `Edit.Init()` (Edit.js) via `includeJs`. Defines the global class `EditTypeObject`.
 * A single shared instance is created lazily by `Edit.GetObjectTypeHandler()` and returned by `Edit.GetTypeHandler`
 * for members with `TypeMemberFlags.IsObject` (and no more specific handler).
 * Implements the duck-typed type-handler contract documented on class `Edit` (Edit.js).
 *
 * Dependencies (globals from other files): `Edit`, `EditOptions` (via `options.Clone()`), `TypeInfoCache`
 * (type.js), `TypeMemberFlags`, `ValueFormat`, `ColorIcon`, `isPureClick` (common.js).
 *
 * UI: the member row shows a 36 px expand icon and a `SysWeaver-EditInfo` summary; expanding builds a child `Edit`
 * (inside `SysWeaver-EditEntry` / `SysWeaver-EditEntryHeader` / `SysWeaver-EditEntryValue`) in the data row
 * created with `editContext.CreateData()`. Classes used: `EditObject`, `DataRow`, `EditorExpanded`.
 * Tooltip / undo texts in this file are not localized.
 */

/**
 * Type handler for nested objects. Stateless apart from per-editor closures created in `AddEditor`.
 */
class EditTypeObject {

    /**
     * Value written into a new parent object for this member; same as `GetDefault`.
     * @param {object} member - The `TypeMemberDesc`.
     * @param {EditOptions} options - The editor options.
     * @returns {?object} The default object (see `GetDefault`).
     */
    CreateDefault(member, options) {
        return this.GetDefault(member, options);
    }

    /**
     * Whether a pasted / dropped value is acceptable: `member` must not be primitive, `obj` must be an object and
     * `obj.$type` must equal `member.TypeName` exactly (no comma/assembly stripping here).
     * @param {*} obj - Candidate value.
     * @param {object} member - The `TypeMemberDesc` (or `TypeDesc`).
     * @param {EditOptions} [options] - Unused.
     * @returns {boolean} True if acceptable.
     */
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

    /**
     * Conditions an accepted pasted / dropped value before it is stored. Returns it unchanged (the `$type`
     * property is kept; removing it is commented out).
     * @param {*} obj - The accepted value.
     * @returns {*} `obj`.
     */
    Condition(obj) {
//        if (obj)
//            delete obj["$type"];
        return obj;
    }

    /**
     * Validates an object value. Null/undefined is OK when the member has `AcceptNull`, else an error
     * "<DisplayName>: Value is null and null is not allowed!" (not localized). Otherwise loads the member type with
     * `Edit.GetType(member.TypeName, member.ElementInst)` and runs `Edit.ValidateType` on it.
     * @param {?object} obj - The value to validate.
     * @param {object} member - The `TypeMemberDesc`.
     * @param {EditOptions} options - The editor options.
     * @param {string} name - Path of the value (used as error path prefix).
     * @returns {Promise<?Array<string>>} Null when valid, else `[path, message]` with path `name + "." + innerPath`
     *   and message `DisplayName + "/" + innerMessage`.
     */
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


    /**
     * Display text of an object value.
     * @param {?object} obj - The value.
     * @param {object} [member] - Unused.
     * @param {EditOptions} [options] - Unused.
     * @returns {?string} `JSON.stringify(obj)`, or null for a falsy value.
     */
    ToString(obj, member, options) {
        return obj ? JSON.stringify(obj) : null;
    }

    /**
     * Default described by the metadata: `{}` for a `"\t"` default (marker for "new instance"),
     * `JSON.parse(member.Default)` when a default is defined, `null` when the member has `AcceptNull`, else `{}`
     * (also `{}` when `member` is falsy).
     * @param {?object} member - The `TypeMemberDesc`.
     * @param {EditOptions} [options] - Unused.
     * @returns {?object} The default value (a new object each call).
     */
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

    /**
     * JSON deep clone of a default value. Not used within this folder (`AddEditor` uses `Edit.CloneType`).
     * @param {?object} def - Value to clone.
     * @returns {?object} The clone, or null for a falsy `def`.
     */
    static CloneDef(def) {
        if (!def)
            return null;
        return JSON.parse(JSON.stringify(def));
        
    }

    /**
     * Builds the editor of an object member (handler contract `AddEditor`).
     * Resolves the member type (`Edit.GetType(member.TypeName, member.ElementInst)`) and clones the options for the
     * child editor (`Title = false`, `ReadOnly` = member/options read only, `Name = member.Name`).
     * If the metadata default is an empty object it is replaced by the server default instance
     * (`TypeInfoCache.GetInstance`, errors ignored) or, failing that, by the object a temporary child `Edit` produces
     * from `SetObject({})`; in the latter case a "Default value: <json>" line is added to the tooltip.
     * Writes `Edit.CloneType(def)` into `obj[member.Name]` when that is undefined.
     * DOM: adds class `EditObject` to the editor cell, creates a data row (`editContext.CreateData()`, class
     * `DataRow`), a 36 px expand `ColorIcon` (inactive with "Can't expand null object" when read only and null) and a
     * `SysWeaver-EditInfo` summary filled by `Edit.SetToString`. The child editor is created lazily on expand.
     * @param {object} obj - The parent object; the value is `obj[member.Name]`.
     * @param {Edit} editor - The parent editor.
     * @param {{PrevRow:HTMLTableRowElement, Element:HTMLTableCellElement, CreateData:function():HTMLTableCellElement}} editContext
     *   - Layout context (see `EditDesktop.AddMember`).
     * @param {object} member - The `TypeMemberDesc`.
     * @param {string} title - Member tooltip (unused here).
     * @param {EditOptions} options - The editor options.
     * @returns {Promise<object>} The item: `{ Title, DefValue, DefValueText, DataRow, SetValue, UpdateValue,
     *   AddMenuItems, FindItem }` (no `Focus`).
     */
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

        /**
         * Expands or collapses the nested object editor (inner function of `AddEditor`; click handler of the expand
         * icon, also called without event from `UpdateValue` / `FindItem`). Ignores clicks with modifier keys.
         * Expanding: clears the data cell, creates `SysWeaver-EditEntry` (header + value) holding a child
         * `Edit(objectType, editor.Style, newOpt, editor, member)` set to `obj[mn]`; if the value was null/falsy, records
         * an undoable "Create object" step (via `editor.Invoke`) that stores the child's newly created default object.
         * Keeps the summary in sync on child `EditChange` events, switches the icon to collapse and adds class
         * `EditorExpanded` to the data row and the previous (value) row.
         * Collapsing: drops the child editor, clears the cell, switches the icon and removes `EditorExpanded`.
         * @param {MouseEvent} [ev] - The click event (optional).
         * @returns {Promise<void>}
         */
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
        //  The item returned to the engine (see the type-handler contract on class Edit).
        return {
            /** @type {?string} Constraint text prepended to the member tooltip ("Default value: ..." or null). */
            Title: typeTitle,
            /** @type {?object} Value used by the member menu's "Set default". */
            DefValue: def,
            /** @type {?string} JSON text of `DefValue` (null when there is no default). */
            DefValueText: def == null ? null : JSON.stringify(def),
            /** @type {HTMLTableRowElement} The data row holding the nested editor (toggled `Hide` by `Hide:` expressions). */
            DataRow: dataRow,
            /**
             * Stores a new value into `obj[mn]` and dispatches `EditChange` (member, value, editor.Key/KeyName) on the
             * parent editor element. Does not refresh the UI (the engine calls `UpdateValue` afterwards).
             * @param {?object} newVal - New value.
             * @returns {void}
             */
            SetValue: newVal => {
                obj[mn] = newVal;
                editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));
            },
            /**
             * Refreshes the UI from `obj[mn]` (called after undo/redo). Skipped while the expand step itself is
             * running. Refreshes the summary, collapses if expanded and then expands whenever the new value is
             * non-null (so it also expands a previously collapsed editor).
             * @returns {Promise<void>}
             */
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
            /**
             * Member-menu hook; adds nothing (placeholder).
             * @param {WebMenu} menu - The menu being built.
             * @param {function():void} close - Closes the menu.
             * @returns {void}
             */
            AddMenuItems: (menu, close) => {

                if (isReadOnly)
                    return;
            },
            /**
             * Resolves an undo path below this member: expands if needed and delegates `name` (the remaining path,
             * starting with this member's segment) to the child editor's `FindItem`.
             * @param {string} name - Remaining dotted path.
             * @returns {Promise<object|Edit|null>} The target item / editor.
             */
            FindItem: async name => {
                if (!isExpanded)
                    await toggleExpand();
                return await itemEdit.FindItem(name);
            }
        };
    }

}
