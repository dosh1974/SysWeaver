class EditTypeCollection {


    static async Init() {
        const current = document.currentScript.src;
        await includeJs(current, "EditCollectionArray.js");
        await includeJs(current, "EditCollectionMap.js");
    }

    CreateDefault(member, options) {
        return this.GetDefault(member, options)[0];
    }

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

    Condition(obj) {
        return obj;
    }

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
            return !!err;
        });
        elementType.DisplayName = temp;
        if (!err)
            return null;
        return [err[0], member.DisplayName + err[1]];
    }


    ToString(obj, member, options) {
        return "" + obj;
    }

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
        const container = this.GetContainer(member);
        const def = this.GetDefault(member, options);
        if (typeof obj[mn] === "undefined")
            obj[mn] = this.GetDefault(member, options);

        let vals = obj[mn];
        if (!vals)
            vals = container.createVal();

        const isReadOnly = options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0);
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

        const newOpt = options.Clone();
        newOpt.Title = !Edit.IsPrimitive(elementType);

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



        async function CreateNewDefault() {
            if ((elementType.Flags & TypeMemberFlags.AcceptNull) != 0) {
                if (!th.GetDefault(member, options))
                    return null;
            }
            return await Edit.CreateDefault(elementType, options);
        }

        function focus(key) {
            keyMap.get("" + key).Element.focus();
        }

        function remove(key) {
            keyMap.get("" + key).ArrayEntry.remove();
        }

        function colChanged() {
            editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, obj[mn]));
        }

        async function addItemEditor(values, key, relativeTo, relation) {
            const arrayEntry = document.createElement("SysWeaver-EditEntry");
            const arrayEntryHeader = document.createElement("SysWeaver-EditEntryHeader");
            const arrayEntryValue = document.createElement("SysWeaver-EditEntryValue");
            arrayEntry.appendChild(arrayEntryHeader);
            arrayEntry.appendChild(arrayEntryValue);
            const keyName = container.getKeyName(key);
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


            const itemEdit = new Edit(elementType, editor.Style, newOpt, editor, member.ElementInst, evListener);
            await itemEdit.SetObject(values[key]);
            keyMap.set("" + key, itemEdit);
            itemEdit.Key = key;
            itemEdit.KeyName = keyName;
            itemEdit.ArrayEntry = arrayEntry;
            arrayEntry.Edit = itemEdit;
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
                            }else
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
                        close();
                    },
                    Flags: isFixed ? 1 : 0,
                }));

                if (isIndexed) {
                    const valCount = container.countVal(values);
                    const isMax = valCount >= max;
                    const isTop = currentKey <= 0;
                    const isBottom = (currentKey + 1) >= valCount;

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
        return {
            Title: typeTitle,
            DefValue: def,
            DefValueText: def == null ? null : _TF("empty", "Text used to indicate that a list of items is empty"),
            DataRow: dataRow,
            Focus: expandIcon.Element,
            UpdateValue: async () => {
                Edit.SetCollectionCount(countText, obj[mn], container);
//                if (isExpanded) {
//                    await toggleExpand();
//                    await toggleExpand();
//                }
            },
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
            GetValue: () => obj[mn],
            SetValue: async newVal => {
                obj[mn] = newVal;
                if (isExpanded) {
                    await toggleExpand();
                    await toggleExpand();
                }
                colChanged();
                //editor.Element.dispatchEvent(Edit.CreateChangeEvent(member, newVal, null, editor.Key, editor.KeyName));

            },
            AddMenuItems: (menu, close) => {

                if (isReadOnly)
                    return;
                const currentValue = obj[mn];
                const isNull = currentValue === null;
                const valLen = isNull ? 0 : container.countVal(currentValue) > 0;
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

                const canAdd = member.Max <= 0 || (valLen < member.Max);
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

EditTypeCollection.Init();