/**
 * @file Type handler for collections: arrays / lists and dictionaries (`Sw.Edit.Handlers.Collection`, legacy global
 * `EditTypeCollection`).
 *
 * Loaded by Edit.js. The value cell shows an expand button, the item count ("null", "empty", "1 item", "{n} items") and, when
 * editable, an add button. Expanded, every item is a child editor (title "[key]") in the data row below. Items of primitive
 * types are single rows; object items have a title row. Item buttons: remove and (indexed collections) insert after; the
 * item menu (title row menu, or the member menu of primitive items) has Remove item and, for indexed collections, Insert
 * before / after and Move to top / up / down / bottom. The member menu has Clear collection (Make empty for null) and Add
 * item. Every structural change is undoable and emits one EditChange for the collection.
 *
 * Strategies (EditCollectionArray.js / EditCollectionMap.js): members with a `KeyTypeName` (dictionaries) use the Map
 * strategy (json objects; adding asks for the key), everything else the Array strategy. A collection is fixed (no
 * structural changes) when read only or when Min >= Max. `Max: null` from the server means "no limit".
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;
    const addLine = E.Util.AddLine;
    /** @type {number} "No limit" maximum. */
    const NoMax = 1000000000;

    /**
     * Collection handler (shared).
     */
    class EditTypeCollection extends E.Handler {
        /**
         * The strategy of a member.
         * @param {object} member - The member.
         * @returns {object} Sw.Edit.Collections.Map for dictionaries, else Sw.Edit.Collections.Array.
         */
        GetContainer(member) {
            return member && member.KeyTypeName ? E.Collections.Map : E.Collections.Array;
        }

        /**
         * Count limits of a member (null / missing / invalid Max = no limit).
         * @param {object} member - The member.
         * @returns {{Min: number, Max: number, HaveMin: boolean, HaveMax: boolean}} The limits.
         */
        static Limits(member) {
            const min = E.GetIntParam(member.Min, 0);
            const max = E.GetIntParam(member.Max, NoMax);
            return { Min: Math.max(0, min), Max: max < 0 ? NoMax : max, HaveMin: min > 0, HaveMax: max !== NoMax && max >= 0 && Has(member.Max) };
        }

        /**
         * Accept arrays (or json objects for dictionaries) within the maximum count.
         * @param {*} obj - Candidate.
         * @param {object} member - The member.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj, member) {
            if (obj === null || typeof obj !== "object")
                return false;
            const c = this.GetContainer(member);
            if (c === E.Collections.Array ? !Array.isArray(obj) : Array.isArray(obj))
                return false;
            return !member || c.countVal(obj) <= EditTypeCollection.Limits(member).Max;
        }

        /**
         * Validate: null only with AcceptNull, the count within Min / Max, every item (path "member[key]").
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            const dn = member.DisplayName || member.Name;
            if (obj === null || obj === undefined)
                return E.Handler.NullCheck(obj, member, name);
            const c = this.GetContainer(member);
            const n = c.countVal(obj);
            const l = EditTypeCollection.Limits(member);
            if (n > l.Max)
                return [name, dn + ": " + _T("Too many items: at most {0} allowed, there are {1}.", l.Max, n, "Error message displayed when a collection has more items than allowed.{0} is replaced with the maximum number of items allowed.{1} is replaced with the current number of items.")];
            if (n < l.Min)
                return [name, dn + ": " + _T("Too few items: at least {0} needed, there are {1}.", l.Min, n, "Error message displayed when a collection has fewer items than required.{0} is replaced with the minimum number of items.{1} is replaced with the current number of items.")];
            const et = await EditTypeCollection.ItemType(member, null);
            if (!et)
                return null;
            let err = null;
            await c.iterate(obj, async (key, value) => {
                const kn = c.getKeyName(key);
                const t = await EditTypeCollection.ItemType(member, kn);
                err = await E.ValidateType(t, value, options, name + kn);
                return !err;
            });
            return err ? [err[0], dn + err[1]] : null;
        }

        /**
         * Display text.
         * @param {*} obj - The value.
         * @returns {string} The text.
         */
        ToString(obj) {
            return obj ? JSON.stringify(obj) : "null";
        }

        /**
         * The default: an empty collection ("\t" metadata or not null), else null with AcceptNull.
         * @param {object} member - The member.
         * @returns {?(Array|object)} The default.
         */
        GetDefault(member) {
            const d = member.Default;
            if (d === "\t")
                return this.GetContainer(member).createVal();
            //  A json default (client registered types, EditDefault with a json text) of the right shape
            if (typeof d === "string" && /^\s*[\[{]/.test(d)) {
                try {
                    const v = JSON.parse(d);
                    if (this.IsOfType(v, member))
                        return v;
                }
                catch {
                }
            }
            if ((member.Flags & F.AcceptNull) !== 0)
                return null;
            return this.GetContainer(member).createVal();
        }

        /**
         * The type description of an item: the element type with the element constraints (ElementInst) applied; primitive
         * element types get the constraints on their member, titled with the key.
         * @param {object} member - The collection member.
         * @param {?string} keyName - Item title ("[3]").
         * @returns {Promise<?object>} A fresh type description (or null for an unknown element type).
         */
        static async ItemType(member, keyName) {
            let t = await E.GetType(member.ElementTypeName, member.ElementInst);
            if (!t)
                return null;
            const th = E.GetTypeHandler(member.ElementTypeName, member.ElementInst);
            if (E.IsPrimitive(t) || th.IgnoreMembers) {
                if (!t.Members || !t.Members.length)
                    t.Members = [{ Name: "Value", TypeName: member.ElementTypeName, Flags: t.Flags }];
                const pm = t.Members[0];
                for (const k of ["Min", "Max", "Default", "Flags", "Summary", "Remarks", "EditParams", "Type"])
                    if (t[k] !== undefined)
                        pm[k] = t[k];
                pm.Flags |= F.IsPrimitive;
                t.Flags |= F.IsPrimitive;
                if (keyName)
                    pm.DisplayName = keyName;
            }
            return t;
        }

        /**
         * Build the collection editor.
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
            const C = this.GetContainer(member);
            const lim = EditTypeCollection.Limits(member);
            let tip = null;
            if (lim.HaveMin)
                tip = addLine(tip, _TF("Minimum allowed count", "This is the header of a tool tip row that displays the minimum number of allowed items in a collection"), lim.Min);
            if (lim.HaveMax)
                tip = addLine(tip, _TF("Maximum allowed count", "This is the header of a tool tip row that displays the maximum number of allowed items in a collection"), lim.Max);
            const def = this.GetDefault(member);
            if (obj[mn] === undefined)
                obj[mn] = this.GetDefault(member);
            const readOnly = !!editContext.ReadOnly || E.IsReadOnly(member, options);
            const fixed = readOnly || (lim.HaveMax && lim.Min >= lim.Max);
            const indexed = C.Indexed && (member.Flags & F.Indexed) !== 0;
            const proto = await EditTypeCollection.ItemType(member, null);
            const isPrimitive = !proto || E.IsPrimitive(proto);
            //  The items are read only when the collection is, or when the element type / element constraints say so
            const itemsReadOnly = readOnly || (!!proto && (proto.Flags & F.ReadOnly) !== 0);
            /** @type {object} A token that identifies this list while one of its items is dragged (move instead of copy). */
            const listToken = {};
            /** @type {boolean} Focus the drag handle (not the first input) of the next refreshed item (keyboard moves). */
            let focusGrip = false;
            /** @type {*} The key of the item to show expanded after the next re-render (added, inserted, moved). */
            let openKey = null;
            cell.classList.add("EditContainer");
            const data = editContext.CreateData();
            data.classList.add("DataRow");
            const row = editContext.Row || editContext.PrevRow;
            const info = Sw.el("span.sw-edit-info");
            const expBtn = new Sw.Button({ Kind: "ghost", Small: true, Icon: "right", Class: "sw-edit-expand" });
            let expanded = false;
            /** @type {Map<string, Sw.Edit>} Key text -> item editor. */
            const keyMap = new Map();
            const count = () => C.countVal(obj[mn]);
            const setInfo = () => {
                E.SetCollectionCount(info, obj[mn], C);
                expBtn.SetIcon(expanded ? "down" : "right");
                expBtn.E.setAttribute("aria-expanded", expanded ? "true" : "false");
                expBtn.SetTitle(expanded
                    ? _TF("Collapse collection", "Tool tip description of a button that when clicked will collapse (hide) the items in a collection")
                    : _TF("Expand collection", "Tool tip description of a button that when clicked will expand (show) the items in a collection"));
                const full = count() >= lim.Max;
                if (addBtn)
                    addBtn.SetDisabled(full, _T("The collection already has the maximum of {0} items", lim.Max, "Why the add or insert button of a collection editor is disabled.{0} is the maximum number of items allowed"));
                if (setValueBtn)
                    setValueBtn.E.hidden = !(obj[mn] === null || obj[mn] === undefined);
                const open = expanded && count() > 0;
                data.classList.toggle("EditorExpanded", open);
                row.classList.toggle("EditorExpanded", open);
                row.classList.toggle("sw-edit-expanded", open);
            };
            const changed = () => editor.Changed(member, obj[mn]);

            /** Create the default value of a new item. */
            const newItem = async () => {
                const t = await EditTypeCollection.ItemType(member, null);
                if (!t)
                    return null;
                if ((t.Flags & F.AcceptNull) !== 0 && !E.IsPrimitive(t))
                    return null;
                return await E.CreateDefault(t, options);
            };

            /**
             * Run an undoable structural change on this collection (found by path, so it works after re-rendering).
             * @param {function(object):Promise<*>} doFn - Change (gets the item, returns the key to focus).
             * @param {function(object):Promise<*>} undoFn - Revert (returns the key to focus).
             * @param {string} undoTitle - Title.
             */
            const structural = (doFn, undoFn, undoTitle) => editor.Invoke(
                async it => it && it.Refresh(await doFn(it)),
                async it => it && it.Refresh(await undoFn(it)),
                undoTitle, mn);

            //  The operations work on the item found by path (`it`), not on this closure, so they stay valid when the parent
            //  editor was re-rendered with another object (undo / redo after a paste of the parent, etc.).
            /** Remove an item. */
            const removeItem = key => {
                let old;
                return structural(async it => {
                    const c = it.GetValue();
                    old = E.CloneValue(C.getVal(c, key));
                    C.removeVal(c, key);
                    return C === E.Collections.Array ? Math.min(key, C.countVal(c) - 1) : null;
                }, async it => {
                    C.insertAt(it.GetValue(), E.CloneValue(old), key);
                    return key;
                }, _T("Remove item: {0}", C.getKeyName(key), "Command text stored in a log when an item in a collection is removed.{0} is replaced with the name of the key of item."));
            };
            /** Insert a new item at an index. */
            const insertItem = async (key, after) => {
                const v = await newItem();
                const at = after ? key + 1 : key;
                return structural(async it => {
                    C.insertAt(it.GetValue(), E.CloneValue(v), at);
                    return at;
                }, async it => {
                    C.removeVal(it.GetValue(), at);
                    return key;
                }, after ? _TF("Insert item after", "Command text stored in a log when an item is inserted after the selected item")
                    : _TF("Insert item before", "Command text stored in a log when an item is inserted before the selected item"));
            };
            /** Move an item. */
            const moveItem = (from, to) => structural(async it => {
                C.move(it.GetValue(), from, to);
                return to;
            }, async it => {
                C.move(it.GetValue(), to, from);
                return from;
            }, _T("Move item from {0} to {1}", from, to, "Command text stored in a log when an item in a collection is moved from one position to another.{0} is the name the position it was moved from.{1} is the name of the position it was moved to."));
            /** Insert a copy of an item after it (indexed collections). */
            const duplicateItem = key => insertValue(E.CloneValue(C.getVal(obj[mn], key)), key + 1,
                _T("Duplicate item: {0}", C.getKeyName(key), "Command text stored in a log when an item of a collection is duplicated.{0} is replaced with the name of the key of the item."));
            /**
             * Add an item at the end (dictionaries: asks for the key); creates the collection when null.
             * @param {*} [value] - The value of the new item (a pasted / dropped item); undefined = a new default item.
             * @param {string} [undoTitle] - Undo title (default "Add item to collection").
             * @returns {Promise<boolean>} True when added.
             */
            const addItem = async (value, undoTitle) => {
                if (count() >= lim.Max) {
                    Sw.Fail(_TF("Maximum number of items already added", "Tool tip description of a menu item that when clicked will add a new item to a collection, displayed when the maximum allowed number of items is already in the collection"));
                    return false;
                }
                let key;
                if (C === E.Collections.Map) {
                    key = await Sw.Dialog.Prompt(_TF("Add item", "Text on a menu item that when clicked will add a new item to a collection"), "", _TF("Key", "Placeholder of a text box where the key of a new dictionary item is entered"), {
                        Text: _TF("Enter the key of the new item", "Text in a dialog that asks for the key of a new dictionary item"),
                        Validate: k => {
                            if (!k)
                                return _TF("A key is required", "Validation error when the key of a new dictionary item is empty");
                            if (C.hasKey(obj[mn], k))
                                return _TF("The key already exists", "Validation error when the key of a new dictionary item is already used");
                            const kt = member.KeyTypeName;
                            if (kt && kt !== "System.String") {
                                const kh = E.GetTypeHandler(kt, Object.assign({ TypeName: kt, Flags: F.IsPrimitive }, member.KeyInst || {}));
                                if (!kh.IsOfType(k, { TypeName: kt, Flags: F.IsPrimitive }, options))
                                    return _TF("Invalid key", "Validation error when the key of a new dictionary item has the wrong type");
                            }
                            return null;
                        },
                    });
                    if (key === null || key === undefined)
                        return false;
                }
                const v = value === undefined ? await newItem() : value;
                let wasNull = false;
                await structural(async it => {
                    let c = it.GetValue();
                    wasNull = c === null || c === undefined;
                    if (wasNull) {
                        c = C.createVal();
                        it.SetRaw(c);
                    }
                    const k = C.addVal(c, E.CloneValue(v), key);
                    await it.Expand(true);
                    return k;
                }, async it => {
                    const c = it.GetValue();
                    if (wasNull)
                        it.SetRaw(null);
                    else
                        C.removeVal(c, C === E.Collections.Map ? key : C.countVal(c) - 1);
                    return null;
                }, undoTitle || _TF("Add item to collection", "Command text stored in a log when an new item is added to a collection"));
                return true;
            };
            /** Insert a value (a pasted / dropped item) at an index (indexed collections). */
            const insertValue = (v, at, undoTitle) => structural(async it => {
                C.insertAt(it.GetValue(), E.CloneValue(v), at);
                return at;
            }, async it => {
                C.removeVal(it.GetValue(), at);
                return Math.max(0, at - 1);
            }, undoTitle);
            /**
             * Interpret a pasted / dropped text as new items: one value of the element type, or (paste) an array of them.
             * @param {?string} text - The text.
             * @param {boolean} allowMany - Accept an array of items.
             * @returns {Promise<{Ok: boolean, Values: Array, Reason: ?string}>} The values.
             */
            const parseItems = async (text, allowMany) => {
                const bad = { Ok: false, Values: [], Reason: _TF("The clipboard holds no item of this collection", "Tool tip of a menu item that adds the clipboard content as new items of a collection, when the clipboard holds nothing usable") };
                const t = await EditTypeCollection.ItemType(member, null);
                if (!t || text === null || text === undefined || text === "")
                    return bad;
                let parsed;
                let json = true;
                try {
                    parsed = JSON.parse(text);
                }
                catch {
                    parsed = text;
                    json = false;
                }
                const list = allowMany && json && Array.isArray(parsed) ? parsed : [parsed];
                const out = [];
                if (E.IsPrimitive(t)) {
                    const pm = t.Members[0];
                    const h = E.GetTypeHandler(pm.TypeName, pm);
                    //  A raw text for a string element (a copied text that happens to be json stays that text unless it is a list)
                    if (pm.TypeName === "System.String" && !(allowMany && Array.isArray(parsed)))
                        list[0] = text;
                    for (const x of list) {
                        let ok = false;
                        try {
                            ok = h.IsOfType(x, pm, options) || (x === null && (pm.Flags & F.AcceptNull) !== 0);
                        }
                        catch {
                        }
                        if (!ok)
                            return bad;
                        out.push(x === null ? null : h.Condition(x, pm, options));
                    }
                }
                else {
                    for (const x of list) {
                        if (!E.IsObjectOf(x, t) && !(x === null && (t.Flags & F.AcceptNull) !== 0))
                            return bad;
                        out.push(x);
                    }
                }
                if (!out.length)
                    return bad;
                if (count() + out.length > lim.Max)
                    return { Ok: false, Values: [], Reason: _TF("Maximum number of items already added", "Tool tip description of a menu item that when clicked will add a new item to a collection, displayed when the maximum allowed number of items is already in the collection") };
                //  New items keep the read only parts of the element type (a read only element type gives default items) and lose the
                //  `$type` marker of copied objects (the element type is fixed); one toast names what was kept
                const kept = [];
                for (let i = 0; i < out.length; ++i)
                    out[i] = await E.KeepReadOnlyItem(t, E.CloneValue(out[i]), options, _T("{0} (new item)", member.DisplayName || mn, "Name of a new item of a collection in a toast about read only values.{0} is replaced with the name of the collection"), kept);
                E.ReportKept(kept);
                return { Ok: true, Values: out, Reason: null };
            };
            /**
             * Add parsed items at the end (each one undoable; dictionaries ask for every key).
             * @param {Array} values - The values.
             * @param {string} undoTitle - Undo title.
             * @returns {Promise<boolean>} True when at least one was added.
             */
            const addValues = async (values, undoTitle) => {
                let any = false;
                for (const v of values)
                    if (await addItem(E.CloneValue(v), undoTitle))
                        any = true;
                    else
                        break;
                return any;
            };
            const addBtn = fixed ? null : new Sw.Button({
                Small: true, Icon: "plus", Class: "sw-edit-add", Text: _TF("Add", "Text of the button that adds a new item to a collection"),
                Title: _TF("Add a new item to the collection", "Tool tip description of a menu item that when clicked will add a new item to a collection"),
                Label: _T("Add an item to {0}", member.DisplayName || mn, "Accessible name of the add button of a collection.{0} is replaced with the name of the collection"),
                OnClick: () => addItem(),
            });
            /** "Set a value" next to a null collection (Design v2 null token): makes it an empty collection (undoable). */
            const setValueBtn = readOnly || !fixed ? null : new Sw.Button({ // editable collections have "+ Add" (also creates a null collection)
                Kind: "ghost", Small: true, Class: "sw-edit-setvalue", Text: _TF("Set a value", "Text of a button next to a null value that creates a value (the default object)"),
                Title: _TF("Make an empty collection instead of null", "Tool tip description of a menu item that when clicked will make a collection empty istead of non-existent"),
                OnClick: () => editor.Invoke(it => it && it.SetValue(C.createVal()), it => it && it.SetValue(null),
                    _TF("Make empty collection", "Command text stored in a log when a collection is made empty instead of non-existent (null)"), mn),
            });
            /** The "Add item" button below the items of an expanded collection (the end of the list is where new items appear). */
            const addRowBtn = fixed ? null : new Sw.Button({
                Small: true, Kind: "ghost", Icon: "plus", Class: "sw-edit-addrow",
                Text: _TF("Add item", "Text on a menu item that when clicked will add a new item to a collection"),
                Title: _TF("Add a new item to the collection", "Tool tip description of a menu item that when clicked will add a new item to a collection"),
                OnClick: () => addItem(),
            });
            if (addRowBtn) {
                //  Dropping an item (copied / dragged from this or another list) on it adds it
                E.DropTarget(addRowBtn.E, async text => {
                    const r = await parseItems(text, true);
                    if (!r.Ok) {
                        Sw.Fail(r.Reason);
                        return;
                    }
                    await addValues(r.Values, _T("Dropped: {0}", text, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property."));
                });
            }
            /**
             * Move an item with the keyboard / the drag handle and keep the focus on its handle.
             * @param {number} from - Index.
             * @param {number} to - New index (clamped).
             */
            const moveTo = async (from, to) => {
                const n = count();
                to = Math.max(0, Math.min(n - 1, to));
                if (to === from || fixed || !indexed)
                    return;
                focusGrip = true;
                await moveItem(from, to);
            };
            /**
             * Drag handle of an item: pointer drag (mouse, touch, pen) re-orders the list, a line shows where the item goes; the
             * arrow keys (Up / Down, Home / End) move it too.
             * @param {Sw.Edit} ie - The item editor.
             * @returns {HTMLElement} The handle.
             */
            const grip = ie => {
                const g = new Sw.Button({
                    Kind: "ghost", Small: true, Icon: "drag", Class: "sw-edit-grip",
                    Title: _TF("Drag to move the item, or use the arrow keys (Alt+Up / Alt+Down in the item)", "Tool tip of the drag handle of a collection item"),
                    Label: _T("Move item {0}", C.getKeyName(ie.Key), "Accessible name of the drag handle of a collection item.{0} is replaced with the item index, ex: [3]"),
                });
                g.E.setAttribute("aria-keyshortcuts", "ArrowUp ArrowDown Home End");
                //  The title row around it is a native drag source (copy): while the handle is pressed the row is not draggable,
                //  so a drag on the handle re-orders with pointer events instead
                g.E.addEventListener("pointerdown", () => {
                    const rows = [];
                    for (let p = g.E.parentElement; p && p !== data; p = p.parentElement)
                        if (p.draggable) {
                            p.draggable = false;
                            rows.push(p);
                        }
                    const restore = () => {
                        rows.forEach(r => r.draggable = true);
                        window.removeEventListener("pointerup", restore, true);
                        window.removeEventListener("pointercancel", restore, true);
                    };
                    window.addEventListener("pointerup", restore, true);
                    window.addEventListener("pointercancel", restore, true);
                });
                g.E.addEventListener("keydown", ev => {
                    const k = ev.key;
                    const n = count();
                    const to = k === "ArrowUp" ? ie.Key - 1 : k === "ArrowDown" ? ie.Key + 1 : k === "Home" ? 0 : k === "End" ? n - 1 : null;
                    if (to === null || ev.ctrlKey || ev.metaKey)
                        return;
                    ev.preventDefault();
                    ev.stopPropagation();
                    moveTo(ie.Key, to);
                });
                let line = null;
                let target = -1;
                const others = () => Array.from(data.children).filter(x => x.classList.contains("sw-edit-entry") && x !== ie.ArrayEntry);
                Sw.Drag(g.E, {
                    Threshold: 0, // capture at once: a quick drag leaves the small handle before any threshold
                    OnStart: () => {
                        ie.ArrayEntry.classList.add("sw-edit-dragging");
                        line = Sw.el("div.sw-edit-dropline", { attrs: { "aria-hidden": "true" } });
                        data.append(line);
                        target = ie.Key;
                    },
                    OnMove: ev => {
                        const os = others();
                        let i = 0;
                        while (i < os.length) {
                            const r = os[i].getBoundingClientRect();
                            if (ev.clientY < r.top + r.height / 2)
                                break;
                            ++i;
                        }
                        target = i;
                        const ref = i < os.length ? os[i] : os[os.length - 1];
                        const top = !ref ? ie.ArrayEntry.offsetTop : (i < os.length ? ref.offsetTop - 3 : ref.offsetTop + ref.offsetHeight + 1);
                        line.style.top = top + "px";
                        //  Scroll the page / container while dragging near the top or bottom edge of the window
                        if (ev.clientY < 48)
                            window.scrollBy(0, -12);
                        else if (ev.clientY > window.innerHeight - 48)
                            window.scrollBy(0, 12);
                    },
                    OnEnd: (ev, st, cancelled) => {
                        ie.ArrayEntry.classList.remove("sw-edit-dragging");
                        if (line)
                            line.remove();
                        line = null;
                        if (!cancelled && target >= 0 && target !== ie.Key)
                            moveTo(ie.Key, target);
                    },
                });
                return g.E;
            };

            /**
             * Create the editor of one item.
             * @param {*} key - The key.
             * @returns {Promise<Sw.Edit>} The editor.
             */
            const addItemEditor = async key => {
                const kn = C.getKeyName(key);
                const t = await EditTypeCollection.ItemType(member, kn);
                const o = new E.Options();
                o.KeyName = kn;
                o.Name = mn;
                o.Title = !isPrimitive;
                o.Dev = options.Dev;
                o.ReadOnly = itemsReadOnly;
                o.ExpandAllCollections = options.ExpandAllCollections;
                o.MenuIcon = options.MenuIcon;
                o.TypeHints = options.TypeHints;
                //  Object items: a one line summary in the title row; long lists start collapsed (the item just added / moved is open)
                o.Summary = !isPrimitive;
                o.IsExpanded = count() <= 3 || (openKey !== null && openKey !== undefined && "" + openKey === "" + key);
                const headButtons = target => {
                    if (fixed)
                        return;
                    if (indexed)
                        target.append(grip(ie));
                    const rb = new Sw.Button({
                        Kind: "ghost", Small: true, Icon: "editClear", Class: "sw-edit-itembtn EditFloatRight",
                        Title: _TF("Remove the item from the collection", "Tool tip description for a menu item that when clicked will remove an item from a collection"),
                        OnClick: () => removeItem(ie.Key),
                    });
                    target.append(rb.E);
                    if (indexed) {
                        const ib = new Sw.Button({
                            Kind: "ghost", Small: true, Icon: "plus", Class: "sw-edit-itembtn",
                            Title: _TF("Insert a new item after this item", "Tool tip description on a menu item that when clicked will insert a new item after the selected item"),
                            OnClick: () => insertItem(ie.Key, true),
                        });
                        ib.SetDisabled(count() >= lim.Max, _T("The collection already has the maximum of {0} items", lim.Max, "Why the add or insert button of a collection editor is disabled.{0} is the maximum number of items allowed"));
                        target.append(ib.E);
                    }
                };
                if (!isPrimitive)
                    o.AddToHeader = headButtons;
                const ie = new E(t || { TypeName: member.ElementTypeName, Flags: F.IsPrimitive, Members: [{ Name: "Value", DisplayName: kn, TypeName: member.ElementTypeName, Flags: F.ReadOnly }] },
                    editor.ForcedStyle, o, editor, member.ElementInst || member, ev => {
                        const det = ev.detail;
                        if (!det.Next && (isPrimitive || !det.Member))
                            C.setVal(obj[mn], ie.Key, det.Value);
                    });
                ie.Key = key;
                ie.KeyName = kn;
                ie.AddMenuItems = items => EditTypeCollection.ItemMenu(items, ie, { C, fixed, readOnly, indexed, lim, count, removeItem, insertItem, moveItem, duplicateItem });
                await ie.SetObject(C.getVal(obj[mn], key));
                if (isPrimitive) {
                    const ic = ie.Body.querySelector(".sw-edit-icon");
                    if (ic)
                        headButtons(ic);
                }
                const entry = Sw.el("div.sw-edit-entry", { dataset: { key: "" + key } }, Sw.el("div.sw-edit-entryvalue.SysWeaver-EditEntryValue", null, ie.Element));
                entry.Edit = ie;
                ie.ArrayEntry = entry;
                if (indexed && !fixed) {
                    //  Alt+Up / Alt+Down (Alt+Home / Alt+End) anywhere in the item moves it (not inside a nested collection's item)
                    entry.addEventListener("keydown", ev => {
                        if (!ev.altKey || ev.ctrlKey || ev.metaKey || ev.shiftKey || ev.target.closest(".sw-edit-entry") !== entry)
                            return;
                        const k = ev.key;
                        const to = k === "ArrowUp" ? ie.Key - 1 : k === "ArrowDown" ? ie.Key + 1 : k === "Home" ? 0 : k === "End" ? count() - 1 : null;
                        if (to === null)
                            return;
                        ev.preventDefault();
                        ev.stopPropagation();
                        moveTo(ie.Key, to);
                    });
                }
                keyMap.set("" + key, ie);
                data.append(entry);
                return ie;
            };

            /** Render the items (expanded) or nothing (collapsed). */
            const render = async () => {
                keyMap.clear();
                data.replaceChildren();
                if (expanded) {
                    for (const k of C.keys(obj[mn]))
                        await addItemEditor(k);
                    if (addRowBtn)
                        data.append(Sw.el("div.sw-edit-addrowwrap", null, addRowBtn.E));
                }
                setInfo();
            };
            const toggle = async () => {
                expanded = !expanded;
                await render();
            };
            expBtn.O.OnClick = () => toggle();
            info.addEventListener("click", ev => {
                if (Sw.BadClick(ev)) return;
                if (!Sw.HasSelectionIn(info))
                    toggle();
            });
            cell.append(expBtn.E, info);
            if (addBtn)
                cell.append(addBtn.E);
            if (setValueBtn)
                cell.append(setValueBtn.E);
            if (options.ExpandAllCollections)
                expanded = true;
            await render();

            const item = E.Handler.Item(obj, editor, member, {
                Focus: expBtn.E, Title: tip, DefValue: def,
                DefValueText: def === null ? "null" : _TF("empty", "Text used to indicate that a list of items is empty"),
            });
            item.DataRow = data;
            /**
             * Re-render after a change and focus an item.
             * @param {*} [focusKey] - Key of the item to focus.
             */
            item.Refresh = async focusKey => {
                openKey = focusKey;
                await render();
                changed();
                if (focusKey !== null && focusKey !== undefined) {
                    const ie = keyMap.get("" + focusKey);
                    if (ie) {
                        //  The value input of the item (ready for typing after "+ Add"), else its first control; keyboard moves keep the handle
                        const f = (focusGrip && ie.ArrayEntry.querySelector(".sw-edit-grip"))
                            || ie.Element.querySelector(".sw-edit-body input:not([type=hidden]):not(:disabled):not([readonly]), .sw-edit-body textarea:not(:disabled):not([readonly]), .sw-edit-body select:not(:disabled)")
                            || ie.Element.querySelector("input,textarea,button,[tabindex='0']");
                        if (f)
                            f.focus({ preventScroll: false });
                    }
                }
                focusGrip = false;
            };
            /**
             * Take a pasted / dropped text that is not a whole collection: one item of the element type is added at the end.
             * @param {string} text - The text.
             * @param {string} undoTitle - Undo title.
             * @returns {Promise<boolean>} True when consumed.
             */
            item.AcceptText = async (text, undoTitle) => {
                if (fixed)
                    return false;
                const r = await parseItems(text, false);
                return r.Ok ? await addValues(r.Values, undoTitle) : false;
            };
            item.SetRaw = v => {
                obj[mn] = v;
            };
            item.SetValue = async v => {
                obj[mn] = v;
                await render();
                changed();
            };
            item.UpdateValue = async () => {
                setInfo();
            };
            item.FindItem = async name => {
                const first = name.split(".")[0];
                const b = first.indexOf("[");
                const key = b >= 0 ? first.substring(b + 1, first.lastIndexOf("]")) : "";
                if (!expanded) {
                    expanded = true;
                    await render();
                }
                const ie = keyMap.get(key);
                return ie ? await ie.FindItem(name) : null;
            };
            item.Expand = async on => {
                if (!!on !== expanded)
                    await toggle();
            };
            item.AddMenuItems = menu => {
                if (readOnly)
                    return;
                const cur = obj[mn];
                const isNull = cur === null || cur === undefined;
                const n = count();
                const canClear = isNull || n > 0;
                menu.Items.push({
                    Text: isNull ? _TF("Make empty", "Text on a menu item that when clicked will make a collection empty istead of non-existent (null)")
                        : _TF("Clear collection", "Text on a menu item that when clicked will clear a collection (remove all items)"),
                    Icon: "editClear", Disabled: !canClear || (fixed && !isNull),
                    Title: canClear ? (isNull ? _TF("Make an empty collection instead of null", "Tool tip description of a menu item that when clicked will make a collection empty istead of non-existent")
                        : _TF("Remove all items from the collection", "Tool tip description of a menu item that when clicked will clear a collection (remove all items)"))
                        : _TF("Collection is already empty", "Tool tip description of a menu item that when clicked will clear a collection, displayed when the collection is already empty"),
                    OnClick: () => {
                        const old = E.CloneValue(obj[mn]);
                        return editor.Invoke(it => it && it.SetValue(C.createVal()), it => it && it.SetValue(E.CloneValue(old)),
                            isNull ? _TF("Make empty collection", "Command text stored in a log when a collection is made empty instead of non-existent (null)")
                                : _TF("Clear collection", "Command text stored in a log when a collection is cleared (all items removed)"), mn);
                    },
                });
                const canAdd = !fixed && n < lim.Max;
                menu.Items.push({
                    Text: _TF("Add item", "Text on a menu item that when clicked will add a new item to a collection"), Icon: "plus", Disabled: !canAdd,
                    Title: canAdd ? _TF("Add a new item to the collection", "Tool tip description of a menu item that when clicked will add a new item to a collection")
                        : _TF("Maximum number of items already added", "Tool tip description of a menu item that when clicked will add a new item to a collection, displayed when the maximum allowed number of items is already in the collection"),
                    OnClick: () => addItem(),
                });
                menu.Items.push({
                    Text: _TF("Paste as new item", "Text on a menu item that adds the item (or the list of items) on the clipboard to the end of a collection"), Icon: "editPaste", Disabled: !canAdd,
                    Title: canAdd ? _TF("Add the item (or the list of items) on the clipboard to the end of the collection", "Tool tip of a menu item that adds the item (or the list of items) on the clipboard to the end of a collection")
                        : _TF("Maximum number of items already added", "Tool tip description of a menu item that when clicked will add a new item to a collection, displayed when the maximum allowed number of items is already in the collection"),
                    OnClick: async () => {
                        const [text, err] = await E.Util.ReadClipboard();
                        const r = err ? { Ok: false, Reason: err } : await parseItems(text, true);
                        if (!r.Ok) {
                            Sw.Fail(r.Reason);
                            return;
                        }
                        await addValues(r.Values, _T("Paste: {0}", text, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board."));
                    },
                });
            };
            return item;
        }

        /**
         * Add the item operations to an item menu.
         * @param {Array} items - Menu items (Sw.Menu items are pushed).
         * @param {Sw.Edit} ie - The item editor.
         * @param {object} s - State and operations of the collection.
         */
        static ItemMenu(items, ie, s) {
            const key = ie.Key;
            const n = s.count();
            const ro = _TF("Can't move item, collection is read only", "Tool tip description of a menu item that when clicked will move an item, displayed when the collection is read only");
            items.push({
                Text: _TF("Remove item", "Text of a menu item that when clicked will remove an item from a collection"), Icon: "editClear", Disabled: s.fixed,
                Title: s.fixed ? (s.readOnly ? _TF("Can't remove, collection is read only", "Tool tip description for a menu item that when clicked will remove an item from a collection when the collection is read only")
                    : _TF("Can't remove, collection is fixed size", "Tool tip description for a menu item that when clicked will remove an item from a collection when the collection is of a fixed length"))
                    : _TF("Remove the item from the collection", "Tool tip description for a menu item that when clicked will remove an item from a collection"),
                OnClick: () => s.removeItem(ie.Key),
            });
            if (!s.indexed)
                return;
            const isMax = n >= s.lim.Max;
            const isTop = key <= 0;
            const isBottom = key + 1 >= n;
            const insTitle = t => s.readOnly ? _TF("Can't insert item, collection is read only", "Tool tip description on a menu item that when pressed would insert an item into a collection, displayed when the collection is read only")
                : isMax ? _TF("Can't insert item, the collections is maxed out", "Tool tip description on a menu item that when pressed would insert an item into a collection, displayed when the collection is full (have reached the maximum number of allowed items)") : t;
            items.push({
                Text: _TF("Insert before", "Text of a menu item that when clicked will insert a new item before the selected item"), Icon: "editAddBefore", Disabled: s.fixed || isMax,
                Title: insTitle(_TF("Insert a new item before this item", "Tool tip description on a menu item that when clicked will insert a new item before the selected item")),
                OnClick: () => s.insertItem(ie.Key, false),
            });
            items.push({
                Text: _TF("Insert after", "Text of a menu item that when clicked will insert a new item after the selected item"), Icon: "editAddAfter", Disabled: s.fixed || isMax,
                Title: insTitle(_TF("Insert a new item after this item", "Tool tip description on a menu item that when clicked will insert a new item after the selected item")),
                OnClick: () => s.insertItem(ie.Key, true),
            });
            items.push({
                Text: _TF("Duplicate", "Text of a menu item that inserts a copy of the selected item after it"), Icon: "copy", Disabled: s.fixed || isMax,
                Title: insTitle(_TF("Insert a copy of this item after it", "Tool tip of a menu item that inserts a copy of the selected item after it")),
                OnClick: () => s.duplicateItem(ie.Key),
            });
            const top = _TF("Can't move item, already at the top", "Tool tip description of a menu item that when clicked will move an item to the top, displayed when the item is already at the top");
            const bottom = _TF("Can't move item, already at the bottom", "Tool tip description of a menu item that when clicked will move an item to the bottom, displayed when the item is already at the bottom");
            items.push({
                Text: _TF("Move to top", "Text for a menu item that when clicked will move the selected item to the top of a list"), Icon: "editTop", Disabled: s.fixed || isTop,
                Title: s.readOnly ? ro : isTop ? top : _TF("Move this item to the top", "Tool tip description on a menu item that when clicked will move the selected item to the top of a list"),
                OnClick: () => s.moveItem(key, 0),
            });
            items.push({
                Text: _TF("Move up", "Text for a menu item that when clicked will move the selected item up one position in the list"), Icon: "up", Disabled: s.fixed || isTop,
                Title: s.readOnly ? ro : isTop ? top : _TF("Move this item one step up", "Tool tip description of a menu item that when clicked will move the selected item up one position in the list"),
                OnClick: () => s.moveItem(key, key - 1),
            });
            items.push({
                Text: _TF("Move down", "Text for a menu item that when clicked will move the selected item down one position in the list"), Icon: "down", Disabled: s.fixed || isBottom,
                Title: s.readOnly ? ro : isBottom ? bottom : _TF("Move this item one step down", "Tool tip description of a menu item that when clicked will move the selected item down one position in the list"),
                OnClick: () => s.moveItem(key, key + 1),
            });
            items.push({
                Text: _TF("Move to bottom", "Text for a menu item that when clicked will move the selected item to the bottom of a list"), Icon: "editBottom", Disabled: s.fixed || isBottom,
                Title: s.readOnly ? ro : isBottom ? bottom : _TF("Move this item to the bottom", "Tool tip description of a menu item that when clicked will move the selected item to the bottom of a list"),
                OnClick: () => s.moveItem(key, n - 1),
            });
        }
    }

    E.Handlers.Collection = EditTypeCollection;
    if (!window.EditTypeCollection)
        window.EditTypeCollection = EditTypeCollection;
})();