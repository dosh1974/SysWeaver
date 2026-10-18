/**
 * @file Type handler for collections (arrays and dictionaries) in the object editor.
 *
 * Loaded on demand by `Edit.Init()` (Edit.js) via `includeJs`. Defines the global class `EditTypeCollection`; on
 * evaluation runs `EditTypeCollection.Init()` (last statement, not awaited), which loads the container strategies
 * EditCollectionArray.js and EditCollectionMap.js relative to this script.
 * The shared instance is `Edit.CollectionTypeHandler`, created by `Edit.GetTypeHandler` for members with
 * `TypeMemberFlags.Collection`. Implements the duck-typed type-handler contract documented on class `Edit`.
 *
 * Dependencies (globals from other files): `Edit`, `EditOptions`, `EditCollectionArray`, `EditCollectionMap`,
 * `TypeMemberFlags`, `ValueFormat`, `ColorIcon`, `WebMenuItem`, `isPureClick`, `includeJs`, `_T` / `_TF` (common.js).
 *
 * Each element is edited by its own child `Edit` (with `Key` / `KeyName` / `ArrayEntry` set) inside a
 * `SysWeaver-EditEntry` (`SysWeaver-EditEntryHeader` + `SysWeaver-EditEntryValue`) in the data row created with
 * `editContext.CreateData()`. Element changes are written back into the collection with `container.setVal`.
 * All structural changes (add, clear, remove, insert, move) are undoable via `Edit.Invoke`.
 */

/**
 * Type handler for collections. Stateless apart from the per-editor closures created in `AddEditor`.
 */
class EditTypeCollection {


    /**
     * Loads the two container strategy scripts in parallel with `includeJs(document.currentScript.src, ...)`.
     * Called once while this script is being evaluated (so `document.currentScript` is this script).
     * @returns {Promise<void>}
     */
    static async Init() {
        const current = document.currentScript.src;
        await Promise.all([
            includeJs(current, "EditCollectionArray.js"),
            includeJs(current, "EditCollectionMap.js")
            ]
        );
    }

    /**
     * Value written into a new parent object for this member. Returns element 0 of `GetDefault(...)`, i.e.
     * `undefined` for an empty array or `Map`, and throws a TypeError when `GetDefault` returns null (`AcceptNull`).
     * @param {object} member - The `TypeMemberDesc`.
     * @param {EditOptions} options - The editor options.
     * @returns {*} `GetDefault(member, options)[0]`.
     */
    CreateDefault(member, options) {
        return this.GetDefault(member, options)[0];
    }

    /**
     * Whether a pasted / dropped value is acceptable: must be an array (so `Map` dictionaries never match) and, when
     * `member` is given, its count must not exceed `member.Max` (default 1000000000). The `Min` check has no effect
     * (both branches return true).
     * @param {*} obj - Candidate value.
     * @param {object} [member] - The `TypeMemberDesc`.
     * @returns {boolean} True if acceptable.
     */
    IsOfType(obj, member) {
        if (typeof (obj) !== "object")
            return false;
        if (!Array.isArray(obj))
            return false;
        if (member) {
            let max = member.Max;
            if (Edit.IsValid(max)) {
                max = parseInt(max);
            } else {
                max = 1000000000;
            }
            const c = this.GetContainer(member);
            const ol = c.countVal(obj);
            if (ol > max)
                return false;

            //  Min
            let min = member.Min;
            if (Edit.IsValid(min)) {
                min = parseInt(min);
            } else {
                min = 0;
            }
            if ((min >= max) && (ol != max))
                return true;
        }
        return true;
    }

    /**
     * Conditions an accepted pasted / dropped value; returns it unchanged.
     * @param {*} obj - The accepted value.
     * @returns {*} `obj`.
     */
    Condition(obj) {
        return obj;
    }

    /**
     * Validates a collection value. Null: OK with `AcceptNull`, else "Value may not be null!". Then checks the count
     * against `Max` ("To many items! ...") and `Min` ("To few items! ...") using `Edit.GetIntParam` and
     * `ValueFormat.countSuffix`. Then validates the elements with `Edit.ValidateType(elementType, value, options,
     * name + keyName)` via `container.íterate`, temporarily setting the element type's `DisplayName` to the key name.
     * Iteration stops at the first failing element, whose error is reported.
     * @param {?(Array|Map)} obj - The value to validate.
     * @param {object} member - The `TypeMemberDesc`.
     * @param {EditOptions} options - The editor options.
     * @param {string} name - Path of the value (error path prefix).
     * @returns {Promise<?Array<string>>} Null when valid, else `[path, message]`; element errors have the message
     *   prefixed with `member.DisplayName` (no separator).
     */
    async Validate(obj, member, options, name) {
        if (obj == null) {
            if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
                return null;
            return [name, member.DisplayName + ": " +
                _TF("Value may not be null!", "Error message displayed when trying to set a value to null that may not be null")
            ];
        }
        const container = this.GetContainer(member);
        const ol = container.countVal(obj);
        const maxLength = Edit.GetIntParam(member.Max, 1000000000);
        if (maxLength && (maxLength > 0) && (ol > maxLength))
            return [name, member.DisplayName + ": " +
                _T("To many items! Maximum allowed items are {0}, currently there are {1}", maxLength, ValueFormat.countSuffix(ol,
                    _TF(" item!", "Suffix to a numerical text for a singlular value, ex: 'one item!'"),
                    _TF(" items!", "Suffix to a numerical text for a multiple value, ex: 'three items!' or '42 items'")),
                    "Error message displayed when trying to add more items that allowed to a collection.{0} is replaced with the maximum number of items allowed.{1] is replaced by a text with the current number of items, ex: 'one item!' or '32 items!' etc")];
        const minLength = Edit.GetIntParam(member.Min, 0);
        if (minLength && (minLength > 0) && (ol < minLength))
            return [name, member.DisplayName + ": " +
                _T("To few items! Minimum allowed items are {0}, currently there are {1}", minLength, ValueFormat.countSuffix(ol,
                    _TF(" item!", "Suffix to a numerical text for a singlular value, ex: 'one item!'"),
                    _TF(" items!", "Suffix to a numerical text for a multiple value, ex: 'three items!' or '42 items'")),
                    "Error message displayed when trying to add more items that allowed to a collection.{0} is replaced with the maximum number of items allowed.{1] is replaced by a text with the current number of items, ex: 'one item!' or '32 items!' etc")];
        const elementType = await Edit.GetType(member.ElementTypeName, member.ElementInst);
        const temp = elementType.DisplayName;
        let err = null;
        await container.íterate(obj, async (key, value) => {

            const keyName = container.getKeyName(key);
            const ied = name + keyName;
            elementType.DisplayName = keyName;
            err = await Edit.ValidateType(elementType, value, options, ied);
            return !err;
        });
        elementType.DisplayName = temp;
        if (!err)
            return null;
        return [err[0], member.DisplayName + err[1]];
    }


    /**
     * Display text of a collection value.
     * @param {*} obj - The value.
     * @param {object} [member] - Unused.
     * @param {EditOptions} [options] - Unused.
     * @returns {string} `"" + obj`.
     */
    ToString(obj, member, options) {
        return "" + obj;
    }

    /**
     * Default described by the metadata: a new empty container for a `"\t"` default; otherwise `null` when the member
     * has `AcceptNull`, else a new empty container (`GetContainer(member).createVal()`).
     * @param {object} member - The `TypeMemberDesc`.
     * @param {EditOptions} [options] - Unused.
     * @returns {?(Array|Map)} The default collection.
     */
    GetDefault(member, options) {
        let def = member.Default;
        if (Edit.IsValid(def)) {
            if (def === "\t")
                return this.GetContainer(member).createVal();
        }
        if ((member.Flags & TypeMemberFlags.AcceptNull) !== 0)
            return null;
        return this.GetContainer(member).createVal();
    }

    /**
     * Picks the container strategy for a collection member.
     * @param {object} member - The `TypeMemberDesc`.
     * @returns {?(EditCollectionArray|EditCollectionMap)} `EditCollectionMap.Inst` when `member.Key` and
     *   `member.Element` are set, `null` when only `member.Key` is set (sets are not implemented), else
     *   `EditCollectionArray.Inst`.
     */
    GetContainer(member) {
        if (member.Key) {
            if (member.Element)
                return EditCollectionMap.Inst;

            else
                return null; // TODO: Set
        } else {
            return EditCollectionArray.Inst;
        }
    }

    /**
     * Builds the editor of a collection member (handler contract `AddEditor`).
     * Reads `Min` / `Max` (tooltip lines "Minimum/Maximum allowed count"; defaults 0 / 1000000000), writes
     * `GetDefault(...)` into `obj[member.Name]` when undefined and computes `isReadOnly` (`editContext.ReadOnly`, never
     * set by the layouts, options or member flag), `isFixed` (`Min >= Max` or read only) and `isIndexed`
     * (`TypeMemberFlags.Indexed`: enables insert/move operations).
     * DOM: adds class `EditContainer` to the editor cell, a data row (`editContext.CreateData()`, class `DataRow`),
     * a 36 px expand `ColorIcon` and a `SysWeaver-EditInfo` count (`Edit.SetCollectionCount`).
     * Resolves the element type (`Edit.GetType(member.ElementTypeName, member.ElementInst)`) and its handler; elements
     * are primitive when the type is primitive or the handler has `IgnoreMembers`. Item editors are created lazily on
     * expand (immediately when `options.ExpandAllCollections`). `keyMap` maps `"" + key` to the item `Edit`.
     * @param {object} obj - The parent object; the value is `obj[member.Name]`.
     * @param {Edit} editor - The parent editor.
     * @param {{PrevRow:HTMLTableRowElement, Element:HTMLTableCellElement, CreateData:function():HTMLTableCellElement}} editContext
     *   - Layout context (see `EditDesktop.AddMember`).
     * @param {object} member - The `TypeMemberDesc` (with `ElementTypeName`, `ElementInst`, `Min`, `Max`, `Key`, `Element`).
     * @param {string} title - Member tooltip (unused here).
     * @param {EditOptions} options - The editor options.
     * @returns {Promise<object>} The item: `{ Title, DefValue, DefValueText, DataRow, Focus, UpdateValue, FindItem,
     *   GetValue, SetValue, AddMenuItems }`.
     */
    async AddEditor(obj, editor, editContext, member, title, options) {
        let typeTitle = null;
        const mn = member.Name;
        const cell = editContext.Element;
        //  Min
        let min = member.Min;
        if (Edit.IsValid(min)) {
            min = parseInt(min ?? "0");
            if (!isNaN(min))
                if (min > 0)
                    typeTitle = ValueFormat.AddNonNullLine(typeTitle, _TF("Minimum allowed count", 'This is the header of a tool tip row that displays the minimum number of allowed items in a collection'), min);
        } else {
            min = 0;
        }
        //  Max
        let max = member.Max;
        if (Edit.IsValid(max)) {
            max = parseInt(max);
            if (!isNaN(max))
                typeTitle = ValueFormat.AddNonNullLine(typeTitle, _TF("Maximum allowed count", 'This is the header of a tool tip row that displays the maximum number of allowed items in a collection'), max);
        } else {
            max = 1000000000;
        }
        // The server sends Max = null for "no limit", which parses to NaN: treat it as no limit too.
        if (isNaN(max))
            max = 1000000000;
        const container = this.GetContainer(member);
        const def = this.GetDefault(member, options);
        if (typeof obj[mn] === "undefined")
            obj[mn] = this.GetDefault(member, options);

        let vals = obj[mn];
        if (!vals)
            vals = container.createVal();

        const isReadOnly = editContext.ReadOnly || options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0);
        const isFixed = (min >= max) || isReadOnly;
        const isIndexed = (member.Flags & TypeMemberFlags.Indexed) != 0;

        cell.classList.add("EditContainer");
        const data = editContext.CreateData();
        const dataRow = data.parentElement;
        dataRow.classList.add("DataRow");
        const prevRow = editContext.PrevRow;

        let isExpanded = false;
        let expandIcon = null;
        const countText = document.createElement("SysWeaver-EditInfo");
        Edit.SetCollectionCount(countText, obj[mn], container);


        const keyMap = new Map();


        /**
         * Expands or collapses the item list (click handler of the expand icon; also called without event by the
         * undo/redo steps, `FindItem`, `SetValue` and `ExpandAllCollections`). Ignores clicks with modifier keys and
         * stops propagation. Re-reads `vals = obj[mn]`.
         * Expanding: clears the data cell, calls `addItemEditor` for every key, switches the icon to collapse and adds
         * `EditorExpanded` to the data row and the previous (value) row when the collection is non-empty.
         * Collapsing: clears `keyMap` and the data cell, switches the icon and removes `EditorExpanded`.
         * @param {MouseEvent} [ev] - The click event (optional).
         * @param {function(*, Edit):void} [onEditor] - Optional callback receiving each created item editor.
         * @returns {Promise<void>}
         */
        async function toggleExpand(ev, onEditor) {
            if (!isPureClick(ev))
                return;
            if (ev)
                ev.stopPropagation();
            vals = obj[mn];
            isExpanded ^= true;
            if (isExpanded) {
                data.innerHTML = "";
                await container.onKeys(vals, async key => {
                    const edit = await addItemEditor(vals, key);
                    if (onEditor)
                        onEditor(key, edit);
                });
                expandIcon.ChangeImage("SysWeaverEditIconCollapse");
                expandIcon.SetTitle(
                    _TF("Click to collapse collection", "Tool tip description of a button that when clicked will collapse (hide) the items in a collection")
                );
                if (container.countVal(vals) > 0) {
                    dataRow.classList.add("EditorExpanded");
                    prevRow.classList.add("EditorExpanded");
                }
            } else {
                keyMap.clear();
                data.innerHTML = "";
                expandIcon.ChangeImage("SysWeaverEditIconExpand");
                expandIcon.SetTitle(
                    _TF("Click to expand collection", "Tool tip description of a button that when clicked will expand (show) the items in a collection")
                );
                dataRow.classList.remove("EditorExpanded");
                prevRow.classList.remove("EditorExpanded");
            }
        }
        expandIcon = new ColorIcon(
            isExpanded ? "SysWeaverEditIconCollapse" : "SysWeaverEditIconExpand",
            "IconColorThemeMain", 36, 36,
            isExpanded
                ?
                _TF("Click to collapse collection", "Tool tip description of a button that when clicked will collapse (hide) the items in a collection")
                :
                _TF("Click to expand collection", "Tool tip description of a button that when clicked will expand (show) the items in a collection")
            ,
            toggleExpand
        );
        cell.appendChild(expandIcon.Element);
        cell.appendChild(countText);
        const elementType = await Edit.GetType(member.ElementTypeName, member.ElementInst);
        const th = Edit.GetTypeHandler(member.ElementTypeName, member.ElementInst);
        const isPrimitive = Edit.IsPrimitive(elementType) || th.IgnoreMembers;

        //  Note: this options clone is not used; addItemEditor creates its own `newOpt` (a fresh EditOptions).
        const newOpt = options.Clone();
        newOpt.Title = !Edit.IsPrimitive(elementType);
        newOpt.ReadOnly |= isReadOnly;

        /**
         * Renumbers the item editors after keys changed (removal, insertion, move): walks the `SysWeaver-EditEntry`
         * children of the data cell in key order and, where an entry's editor has a different key, updates `keyMap`,
         * the editor's `Key`, `ArrayEntry`, `KeyName` and title (`Edit.ChangeTitle`). Stale `keyMap` entries for
         * keys no longer present are not removed.
         * @param {Array|Map} values - The collection value.
         * @returns {Promise<void>}
         */
        async function reOrder(values) {
            //  Need to change keys
            const cs = data.childNodes;
            let i = 0;
            await container.onKeys(values, key => {
                const ae = cs[i];
                const cedit = ae.Edit;
                ++i;
                if (key === cedit.Key)
                    return;
                keyMap.set("" + key, cedit);
                cedit.Key = key;
                cedit.ArrayEntry = ae;
                const kn = container.getKeyName(key);
                cedit.KeyName = kn;
                cedit.ChangeTitle(kn);
            });
        }



        /**
         * Creates the value for a new element: `null` when the element type has `AcceptNull` and the element handler's
         * `GetDefault(member, options)` (called with the collection member) is falsy, else `Edit.CreateDefault(elementType, options)`.
         * @returns {Promise<*>} The new element value.
         */
        async function CreateNewDefault() {
            if ((elementType.Flags & TypeMemberFlags.AcceptNull) != 0) {
                if (!th.GetDefault(member, options))
                    return null;
            }
            return await Edit.CreateDefault(elementType, options);
        }

        /**
         * Focuses the root element of the item editor for `key` (throws if the key is not in `keyMap`).
         * @param {*} key - Item key.
         * @returns {void}
         */
        function focus(key) {
            keyMap.get("" + key).Element.focus();
        }

        /**
         * Removes the item's `SysWeaver-EditEntry` from the DOM (the `keyMap` entry is kept).
         * @param {*} key - Item key.
         * @returns {void}
         */
        function remove(key) {
            keyMap.get("" + key).ArrayEntry.remove();
        }

        /**
         * Dispatches an `EditChange` for the whole collection (`member`, `obj[mn]`, no key) on the parent editor element.
         * @returns {void}
         */
        function colChanged() {
            editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, obj[mn]));
        }

        /**
         * Creates the editor for one element: a `SysWeaver-EditEntry` (header + value) holding a child
         * `Edit(elementType, editor.Style, newOpt, editor, member.ElementInst, evListener)` set to `values[key]`.
         * `newOpt` is a fresh `EditOptions` (defaults, so not inheriting ReadOnly etc.) with `KeyName = "[key]"`,
         * `Name = mn` and an `AddToHeader` hook adding remove / insert-after icons.
         * For primitive elements it copies `Min/Max/Default/Flags/Summary/Remark` of the element type onto its first
         * member and sets that member's `DisplayName` to the key name.
         * The `evListener` writes the child's top-level value back with `container.setVal(obj[mn], key, value)`
         * (for objects only when the event has no member, i.e. whole-object replacements).
         * Registers the editor in `keyMap`, sets `Key`, `KeyName`, `ArrayEntry` and `AddMenuItems`, then inserts the
         * entry with `relativeTo.insertAdjacentElement(relation, entry)` or appends it to the data cell.
         * Note: the child is filled with `values[key]`, which only works for arrays (for a `Map` it is undefined, so
         * a default is created).
         * @param {Array|Map} values - The collection value.
         * @param {*} key - Element key / index.
         * @param {Element} [relativeTo] - Existing entry to insert relative to.
         * @param {InsertPosition} [relation] - `"beforebegin"` / `"afterend"` etc.
         * @returns {Promise<Edit>} The item editor.
         */
        async function addItemEditor(values, key, relativeTo, relation) {
            const arrayEntry = document.createElement("SysWeaver-EditEntry");
            const arrayEntryHeader = document.createElement("SysWeaver-EditEntryHeader");
            const arrayEntryValue = document.createElement("SysWeaver-EditEntryValue");
            arrayEntry.appendChild(arrayEntryHeader);
            arrayEntry.appendChild(arrayEntryValue);
            const keyName = container.getKeyName(key);
            const newOpt = new EditOptions();
            newOpt.KeyName = keyName;
            newOpt.Name = mn;
            if (isPrimitive) {
                const pm = elementType.Members[0];
                pm.Min = elementType.Min;
                pm.Max = elementType.Max;
                pm.Default = elementType.Default;
                pm.Flags = elementType.Flags;
                pm.Summary = elementType.Summary;
                pm.Remark = elementType.Remark;
                //pm.Name = newOpt.KeyName;
                pm.DisplayName = newOpt.KeyName;
            }
            /**
             * `EditChange` listener of the item editor (registered by the `Edit` constructor): writes the item's new
             * top-level value back into the collection with `container.setVal(obj[mn], evKey, value)` when the event
             * is not a nested one (`detail.Next` null); for object elements additionally only when `detail.Member` is
             * null (whole-object replacement). `evKey` is the key at creation time (not updated by `reOrder`).
             * @type {function(CustomEvent):void}
             */
            let evListener;
            const evKey = key;
            if (isPrimitive) {
                evListener = ev => {
                    const det = ev.detail;
                    if (!det.Next)
                        container.setVal(obj[mn], evKey, det.Value);
                };
            } else {
                evListener = ev => {
                    const det = ev.detail;
                    if (!det.Next)
                        if (!det.Member)
                            container.setVal(obj[mn], evKey, det.Value);
                };
            }


            /**
             * Undoable removal of this item (click handler of the remove icon and the "Remove item" menu entry).
             * Do: `container.removeVal(values, key)`, removes the DOM entry and renumbers (`reOrder`) when needed, or
             * expands when collapsed; then `colChanged()` and count update.
             * Undo: re-inserts the old value with `container.insertAt` (arrays only) and a new item editor at the
             * original DOM position (or expands), renumbers, focuses it, `colChanged()` and count update.
             * Registered on the parent editor (`editor.Invoke`, path name `mn`).
             * @returns {Promise<void>}
             */
            async function Remove() {
                const currentKey = itemEdit.Key;
                const currentKeyName = itemEdit.KeyName;
                const oldVal = container.getVal(vals, currentKey);
                const parent = arrayEntry.parentNode;
                const rel = Array.prototype.indexOf.call(parent.children, arrayEntry);
                let doReorder;
                await editor.Invoke(async () => {
                    doReorder = container.removeVal(values, currentKey);
                    if (isExpanded) {
                        remove(currentKey);
                        if (doReorder)
                            await reOrder(values);
                    } else
                        await toggleExpand();
                    colChanged();
                    Edit.SetCollectionCount(countText, vals, container);
                }, async () => {
                    const newKey = container.insertAt(values, oldVal, currentKey);
                    if (isExpanded) {
                        await addItemEditor(values, newKey, parent.children[rel], "beforebegin");
                        if (doReorder)
                            await reOrder(values);
                    }
                    else
                        await toggleExpand();
                    focus(newKey);
                    colChanged();
                    Edit.SetCollectionCount(countText, vals, container);
                },
                    _T("Remove item: {0}", currentKeyName, "Command text stored in a log when an item in a collection is removed.{0} is replaced with the name of the key of item.")
                    , mn);
            }

            /**
             * Undoable insertion of a new element (`CreateNewDefault()`) after this item (click handler of the plus
             * icon and the "Insert after" menu entry; indexed collections). Do: `container.insertAfter`, adds the item
             * editor after this entry (or expands), `reOrder`, focuses the new item, `colChanged()`, count update.
             * Undo: `container.removeVal`, removes the entry, renumbers, focuses this item.
             * Registered through the item editor (`itemEdit.Invoke`), so the undo path includes the item key.
             * @returns {Promise<void>}
             */
            async function InsertAfter() {
                const currentKey = itemEdit.Key;
                const currentKeyName = itemEdit.KeyName;
                const defNew = await CreateNewDefault();
                let newKey;
                await itemEdit.Invoke(
                    async edit => {
                        newKey = container.insertAfter(values, defNew, currentKey);
                        if (isExpanded)
                            await addItemEditor(values, newKey, keyMap.get("" + currentKey).ArrayEntry, "afterend");
                        else
                            await toggleExpand();
                        await reOrder(values);
                        focus(newKey);
                        colChanged();
                        Edit.SetCollectionCount(countText, vals, container);
                    },
                    async edit => {

                        const doReorder = container.removeVal(values, newKey);
                        if (isExpanded)
                            remove(newKey);
                        else
                            await toggleExpand();
                        if (doReorder)
                            await reOrder(values);
                        focus(currentKey);
                        colChanged();
                        Edit.SetCollectionCount(countText, vals, container);
                    },
                    _TF("Insert item after", "Command text stored in a log when an item is inserted after the selected item"),
                    mn);
            }

            /**
             * Title-row hook of the item editor (called by `Edit.SetTypeInfo`; only when the item editor has a title
             * row, i.e. `Title` option true which is the `EditOptions` default). Unless fixed / read only, adds a remove
             * icon (`ColorIcon("../icons/clear.svg")`, class `EditFloatRight`, runs `Remove`) and, for indexed
             * collections, an insert-after icon (`ColorIcon("../icons/plus.svg")`, runs `InsertAfter`).
             * @param {HTMLElement} toElement - The title cell.
             * @returns {void}
             */
            newOpt.AddToHeader = toElement => {
                if (!(isFixed || isReadOnly)) {
                    let ri;

                    ri = new ColorIcon("../icons/clear.svg", "IconColorThemeAcc2", 24, 24,
                        _TF("Remove the item from the collection", "Tool tip description for a menu item that when clicked will remove an item from a collection"),
                        Remove);
                    ri.Element.classList.add("EditFloatRight");
                    toElement.appendChild(ri.Element);
                    if (isIndexed) {
                        ri = new ColorIcon("../icons/plus.svg", "IconColorThemeAcc2", 24, 24,
                            _TF("Insert a new item after this item", "Tool tip description on a menu item that when clicked will insert a new item after the selected item"),
                            InsertAfter);
                        toElement.appendChild(ri.Element);
                       

                    } else {

                    }


                    

                }
            };


            const itemEdit = new Edit(elementType, editor.Style, newOpt, editor, member.ElementInst, evListener);
            await itemEdit.SetObject(values[key]);
            keyMap.set("" + key, itemEdit);
            itemEdit.Key = key;
            itemEdit.KeyName = keyName;
            itemEdit.ArrayEntry = arrayEntry;
            arrayEntry.Edit = itemEdit;

            /**
             * Adds the item operations to the item editor's object menu (and member menu of primitive item editors):
             * "Remove item" (`Id: "Remove"`, disabled when fixed) and, for indexed collections, "Insert before",
             * "Insert after" (disabled when fixed or at `Max`), "Move to top", "Move up", "Move down", "Move to bottom"
             * (`Id`s `InsertBefore`, `InsertAfter`, `MoveTop`, `MoveUp`, `MoveDown`, `MoveBottom`; disabled when fixed or
             * already at that end). Icons use the `SysWeaverEditIcon*` classes. "Insert before" is undoable through
             * `itemEdit.Invoke` (`container.insertAt`, mirror of `InsertAfter`).
             * @param {WebMenuItem[]} menuItems - The menu item array to append to.
             * @param {function():void} close - Closes the menu.
             * @returns {void}
             */
            itemEdit.AddMenuItems = (menuItems, close) => {
                const currentKey = itemEdit.Key;
                const currentKeyName = itemEdit.KeyName;
                menuItems.push(WebMenuItem.From({
                    Id: "Remove",
                    Name: _TF("Remove item", "Text of a menu item that when clicked will remove an item from a collection"),
                    IconClass: "SysWeaverEditIconClearCollection",
                    Title: isFixed ? (isReadOnly
                        ?
                        _TF("Can't remove, collection is read only", "Tool tip description for a menu item that when clicked will remove an item from a collection when the collection is read only")
                        :
                        _TF("Can't remove, collection is fixed size", "Tool tip description for a menu item that when clicked will remove an item from a collection when the collection is of a fixed length")
                    ) :
                        _TF("Remove the item from the collection", "Tool tip description for a menu item that when clicked will remove an item from a collection")
                    ,
                    Data: async () => {
                        await Remove();
                        close();
                    },
                    Flags: isFixed ? 1 : 0,
                }));

                if (isIndexed) {
                    const valCount = container.countVal(values);
                    const isMax = valCount >= max;
                    const isTop = currentKey <= 0;
                    const isBottom = (currentKey + 1) >= valCount;

                    /**
                     * Undoable move of an element (indexed collections): `container.move(values, from, to)`, moves the
                     * DOM entry accordingly (or expands when collapsed), `reOrder`, `colChanged()`, count update; undo
                     * moves it back. Registered on the parent editor (`editor.Invoke`, path name `mn`).
                     * @param {number} from - Current index.
                     * @param {number} to - Target index.
                     * @returns {Promise<void>}
                     */
                    async function Move(from, to) {
                    // TODO Undos!
                        await editor.Invoke(async () => {
                            container.move(values, from, to);
                            if (isExpanded) {
                                const src = data.children[from];
                                src.remove();
                                if (to >= data.children.length)
                                    data.appendChild(src);
                                else
                                    data.insertBefore(src, data.children[to]);
                                await reOrder(values);
                            } else {
                                await toggleExpand();
                            }
                            colChanged();
                            Edit.SetCollectionCount(countText, values, container);
                        },
                        async () => {
                            container.move(values, to, from);
                            if(isExpanded) {
                                const src = data.children[to];
                                src.remove();
                                if (from >= data.children.length)
                                    data.appendChild(src);

                                else
                                    data.insertBefore(src, data.children[from]);
                                await reOrder(values);
                            } else {
                                await toggleExpand();
                            }
                            colChanged();
                            Edit.SetCollectionCount(countText, values, container);
                            },
                            _T("Move item from {0} to {1}", from, to, "Command text stored in a log when an item in a collection is moved from one position to another.{0} is the name the position it was moved from.{1} is the name of the position it was moved to.")
                            , mn);
                    }

                    menuItems.push(WebMenuItem.From({
                        Id: "InsertBefore",
                        Name:
                            _TF("Insert before", "Text of a menu item that when clicked will insert a new item before the selected item")
                        ,
                        IconClass: "SysWeaverEditIconAddItemBefore",
                        Title: isReadOnly ?
                                _TF("Can't insert item, collection is read only", "Tool tip description on a menu item that when pressed would insert an item into a collection, displayed when the collection is read only")
                            : (isMax ?
                                _TF("Can't insert item, the collections is maxed out", "Tool tip description on a menu item that when pressed would insert an item into a collection, displayed when the collection is full (have reached the maximum number of allowed items)")
                                :
                                _TF("Insert a new item before this item", "Tool tip description on a menu item that when clicked will insert a new item before the selected item")
                            ),
                        Data: async () => {

                            const defNew = await CreateNewDefault();
                            let newKey;
                            await itemEdit.Invoke(
                                async edit => {
                                    newKey = container.insertAt(values, defNew, currentKey);
                                    if (isExpanded)
                                        await addItemEditor(values, newKey, keyMap.get("" + currentKey).ArrayEntry, "beforebegin");
                                    else
                                        await toggleExpand();
                                    await reOrder(values);
                                    focus(newKey);
                                    colChanged();
                                    Edit.SetCollectionCount(countText, vals, container);
                                },
                                async edit => {

                                    const doReorder = container.removeVal(values, newKey);
                                    if (isExpanded)
                                        remove(newKey);
                                    else
                                        await toggleExpand();
                                    if (doReorder)
                                        await reOrder(values);
                                    focus(currentKey);
                                    colChanged();
                                    Edit.SetCollectionCount(countText, vals, container);
                                },
                                _TF("Insert item before", "Command text stored in a log when an item is inserted before the selected item"),
                                mn);
                            close();
                        },
                        Flags: (isFixed || isMax) ? 1 : 0,
                    }));
                    menuItems.push(WebMenuItem.From({
                        Id: "InsertAfter",
                        Name:
                            _TF("Insert after", "Text of a menu item that when clicked will insert a new item after the selected item")
                        ,
                        IconClass: "SysWeaverEditIconAddItemAfter",
                        Title: isReadOnly ?
                            _TF("Can't insert item, collection is read only", "Tool tip description on a menu item that when pressed would insert an item into a collection, displayed when the collection is read only")
                            : (isMax ?
                                _TF("Can't insert item, the collections is maxed out", "Tool tip description on a menu item that when pressed would insert an item into a collection, displayed when the collection is full (have reached the maximum number of allowed items)")
                                :
                                _TF("Insert a new item after this item", "Tool tip description on a menu item that when clicked will insert a new item after the selected item")
                            ),
                        Data: async () => {
                            await InsertAfter();
                            close();
                        },
                        Flags: (isFixed || isMax) ? 1 : 0,
                    }));



                    menuItems.push(WebMenuItem.From({
                        Id: "MoveTop",
                        Name:
                            _TF("Move to top", "Text for a menu item that when clicked will move the selected item to the top of a list")
                        ,
                        IconClass: "SysWeaverEditIconMoveItemTop",
                        Title: isReadOnly ?
                            _TF("Can't move item, collection is read only", "Tool tip description of a menu item that when clicked will move an item, displayed when the collection is read only")
                            : (isTop ?
                                _TF("Can't move item, already at the top", "Tool tip description of a menu item that when clicked will move an item to the top, displayed when the item is already at the top")
                                :
                                _TF("Move this item to the top", "Tool tip description on a menu item that when clicked will move the selected item to the top of a list")
                            ),
                        Data: async () => {
                            await Move(currentKey, 0);
                            close();
                        },
                        Flags: (isFixed || isTop) ? 1 : 0,
                    }));


                    menuItems.push(WebMenuItem.From({
                        Id: "MoveUp",
                        Name:
                            _TF("Move up", "Text for a menu item that when clicked will move the selected item up one position in the list")
                        ,
                        IconClass: "SysWeaverEditIconMoveItemUp",
                        Title: isReadOnly ?
                            _TF("Can't move item, collection is read only", "Tool tip description of a menu item that when clicked will move an item, displayed when the collection is read only")
                            : (isTop ?
                                _TF("Can't move item, already at the top", "Tool tip description of a menu item that when clicked will move an item to the top, displayed when the item is already at the top")
                                :
                                _TF("Move this item one step up", "Tool tip description of a menu item that when clicked will move the selected item up one position in the list")
                            ),
                        Data: async () => {
                            await Move(currentKey, currentKey - 1);
                            close();
                        },
                        Flags: (isFixed || isTop) ? 1 : 0,
                    }));

                    menuItems.push(WebMenuItem.From({
                        Id: "MoveDown",
                        Name:
                            _TF("Move down", "Text for a menu item that when clicked will move the selected item down one position in the list")
                        ,
                        IconClass: "SysWeaverEditIconMoveItemDown",
                        Title: isReadOnly ?
                            _TF("Can't move item, collection is read only", "Tool tip description of a menu item that when clicked will move an item, displayed when the collection is read only")
                            : (isBottom ?
                                _TF("Can't move item, already at the bottom", "Tool tip description of a menu item that when clicked will move an item to the bottom, displayed when the item is already at the bottom")
                                :
                                _TF("Move this item one step down", "Tool tip description of a menu item that when clicked will move the selected item down one position in the list")
                            ),
                        Data: async () => {
                            await Move(currentKey, currentKey + 1);
                            close();
                        },
                        Flags: (isFixed || isBottom) ? 1 : 0,
                    }));

                    menuItems.push(WebMenuItem.From({
                        Id: "MoveBottom",
                        Name:
                            _TF("Move to bottom", "Text for a menu item that when clicked will move the selected item to the bottom of a list")
                        ,
                        IconClass: "SysWeaverEditIconMoveItemBottom",
                        Title: isReadOnly ?
                            _TF("Can't move item, collection is read only", "Tool tip description of a menu item that when clicked will move an item, displayed when the collection is read only")
                            : (isBottom ?
                                _TF("Can't move item, already at the bottom", "Tool tip description of a menu item that when clicked will move an item to the bottom, displayed when the item is already at the bottom")
                                :
                                _TF("Move this item to the bottom", "Tool tip description of a menu item that when clicked will move the selected item to the bottom of a list")
                            ),
                        Data: async () => {
                            await Move(currentKey, valCount - 1);
                            close();
                        },
                        Flags: (isFixed || isBottom) ? 1 : 0,
                    }));

                }

            };
            arrayEntryValue.appendChild(itemEdit.Element);
            arrayEntry.Edit = itemEdit;
            if (relativeTo)
                relativeTo.insertAdjacentElement(relation, arrayEntry);

            else
                data.appendChild(arrayEntry);
            return itemEdit;
        }

        if (options.ExpandAllCollections && (!isExpanded))
            await toggleExpand();
        //  The item returned to the engine (see the type-handler contract on class Edit).
        return {
            /** @type {?string} Constraint text ("Minimum/Maximum allowed count") prepended to the member tooltip. */
            Title: typeTitle,
            /** @type {?(Array|Map)} Default collection used by "Set default" (shared instance, not cloned). */
            DefValue: def,
            /** @type {?string} Localized "empty", or null when the default is null. */
            DefValueText: def == null ? null : _TF("empty", "Text used to indicate that a list of items is empty"),
            /** @type {HTMLTableRowElement} The data row holding the item editors. */
            DataRow: dataRow,
            /** @type {HTMLElement} Element focused when validation fails here (the expand icon). */
            Focus: expandIcon.Element,
            /**
             * Refreshes the UI from `obj[mn]` after undo/redo: only updates the count text (re-rendering expanded
             * items is commented out).
             * @returns {Promise<void>}
             */
            UpdateValue: async () => {
                Edit.SetCollectionCount(countText, obj[mn], container);
//                if (isExpanded) {
//                    await toggleExpand();
//                    await toggleExpand();
//                }
            },
            /**
             * Resolves an undo path into an item: takes the text between the first `[` and the last character of the
             * first path segment (e.g. `"Items[3]"` -> `"3"`), expands if needed and delegates the full `name` to that
             * item editor's `FindItem`.
             * @param {string} name - Remaining dotted path, first segment `"<member>[<key>]"`.
             * @returns {Promise<object|Edit|null>} The target, or null when the key has no item editor.
             */
            FindItem: async name => {
                const ps = name.split('.');
                let findKey = ps[0];
                const fki = findKey.indexOf('[');
                findKey = findKey.substr(fki + 1, findKey.length - 2 - fki);
                if (!isExpanded)
                    await toggleExpand();
                const ie = keyMap.get(findKey);
                if (typeof ie === "undefined")
                    return null;
//                if (ps.length <= 1)
//                    return ie;
                return await ie.FindItem(name);
            },
            /**
             * Current collection value.
             * @returns {?(Array|Map)} `obj[mn]`.
             */
            GetValue: () => obj[mn],
            /**
             * Replaces the collection value: stores into `obj[mn]`, re-renders the items when expanded (collapse +
             * expand) and dispatches `EditChange` via `colChanged()`.
             * @param {?(Array|Map)} newVal - New collection.
             * @returns {Promise<void>}
             */
            SetValue: async newVal => {
                obj[mn] = newVal;
                if (isExpanded) {
                    await toggleExpand();
                    await toggleExpand();
                }
                colChanged();
                //editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));

            },
            /**
             * Adds collection operations to the member menu (nothing when read only):
             * "Make empty" (when null) / "Clear collection" (`Id: "Clear"`, replaces the value with a new empty
             * container via `SetValue`; undo restores the old value and expands) and "Add item" (`Id: "Add"`: creates
             * `CreateNewDefault()`, adds it with `container.addVal` (creating the container when null), adds an item
             * editor / renumbers when expanded, expands afterwards; undo removes it or restores null). Both are undoable
             * via `editor.Invoke` with path name `mn`.
             * "Add item" is enabled while the item count is below `max` (the parsed `member.Max`, default
             * 1000000000 when it is missing, null or not a number) or when `max <= 0`.
             * @param {WebMenu} menu - The menu being built (items are pushed to `menu.Items`).
             * @param {function():void} close - Closes the menu.
             * @returns {void}
             */
            AddMenuItems: (menu, close) => {

                if (isReadOnly)
                    return;
                const currentValue = obj[mn];
                const isNull = currentValue === null;
                const valLen = isNull ? 0 : container.countVal(currentValue);
                const canClear = isNull || (valLen > 0);
                menu.Items.push(WebMenuItem.From({
                    Id: "Clear",
                    Name: isNull ?
                        _TF("Make empty", "Text on a menu item that when clicked will make a collection empty istead of non-existent (null)")
                        :
                        _TF("Clear collection", "Text on a menu item that when clicked will clear a collection (remove all items)")
                    ,
                    IconClass: "SysWeaverEditIconClearCollection",
                    Title: canClear ? (isNull ?
                        _TF("Make an empty collection instead of null", "Tool tip description of a menu item that when clicked will make a collection empty istead of non-existent")
                        :
                        _TF("Remove all items from the collection", "Tool tip description of a menu item that when clicked will clear a collection (remove all items)")
                    ) :
                        _TF("Collection is already empty", "Tool tip description of a menu item that when clicked will clear a collection, displayed when the collection is already empty")
                    ,
                    Data: async () => {
                        const oldVals = obj[mn];
                        const newVals = container.createVal();
                        await editor.Invoke(
                            async edit => {
                                await edit.SetValue(newVals);
                            },
                            async edit => {
                                await edit.SetValue(oldVals);
                                if (!isExpanded)
                                    await toggleExpand();
                            },
                            isNull ?
                                _TF("Make empty collection", "Command text stored in a log when a collection is made empty instead of non-existent (null)")
                                :
                                _TF("Clear collection", "Command text stored in a log when a collection is cleared (all items removed)")
                            ,
                            mn);
                        close();
                    },
                    Flags: canClear ? 0 : 1,
                }));

                const canAdd = max <= 0 || (valLen < max);
                menu.Items.push(WebMenuItem.From({
                    Id: "Add",
                    Name:
                        _TF("Add item", "Text on a menu item that when clicked will add a new item to a collection")
                    ,
                    IconClass: "SysWeaverEditIconAddItem",
                    Title: canAdd ?
                        _TF("Add a new item to the collection", "Tool tip description of a menu item that when clicked will add a new item to a collection")
                        :
                        _TF("Maximum number of items already added", "Tool tip description of a menu item that when clicked will add a new item to a collection, displayed when the maximum allowed number of items is already in the collection")
                    ,
                    Data: async () => {

                        let newKey = null;
                        let wasNull = false;
                        await editor.Invoke(
                            async edit => {
                                const defNew = await CreateNewDefault();
                                let newVals = edit.GetValue();
                                wasNull = (newVals == null) || (typeof (newVals) === "undefined");
                                if (wasNull)
                                    newVals = container.createVal();
                                newKey = container.addVal(newVals, defNew);
                                if (wasNull) {
                                    await edit.SetValue(newVals);
                                } else {
                                    if (isExpanded) {
                                        await addItemEditor(newVals, newKey);
                                        await reOrder(newVals);
                                    }
                                    colChanged();
                                    Edit.SetCollectionCount(countText, newVals, container);

                                }
                                if (!isExpanded)
                                    await toggleExpand();
                            },
                            async edit => {
                                let vals = edit.GetValue();
                                if (wasNull) {
                                    vals = null;
                                    await edit.SetValue(vals);
                                } else {
                                    container.removeVal(vals, newKey);
                                    if (isExpanded) {
                                        remove(newKey);
                                        await reOrder(vals);
                                    }
                                    colChanged();
                                    Edit.SetCollectionCount(countText, vals, container);
                                }
                                if (!isExpanded)
                                    await toggleExpand();
                            }, 
                            _TF("Add item to collection", "Command text stored in a log when an new item is added to a collection")
                            ,
                            mn
                        );
                        close();
                    },
                    Flags: canAdd ? 0 : 1,
                }));


            },
        };
    }
}

//  Start loading the container strategies (EditCollectionArray.js, EditCollectionMap.js); not awaited.
EditTypeCollection.Init();