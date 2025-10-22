


class Edit {

    static async Boolean(name, value, title, onChange, readOnly, showMenu) {
        const e = new Edit({
            Flags: TypeMemberFlags.IsPrimitive,
            Members: [
                {
                    Name: name,
                    DisplayName: name,
                    Flags: TypeMemberFlags.IsPrimitive,
                    Summary: title,
                    TypeName: "System.Boolean",
                }
            ],
        }, "Desktop", showMenu ? (readOnly ? EditOptions.CleanReadOnlyMenu : EditOptions.CleanMenu) : (readOnly ? EditOptions.CleanReadOnly : EditOptions.Clean));
        await e.SetObject(value);
        if (onChange) {
            e.Element.addEventListener("EditChange", async ev => {
                const val = ev.detail.Value;
                await onChange(val);
            });
        }
        return e;
    }




    static async Init() {
        const current = document.currentScript.src;
        await Promise.all([
                includeJs(current, "type.js"),
                includeJs(current, "UndoStack.js"),
                includeJs(current, "EditOptions.js"),
                includeJs(current, "EditDesktop.js"),
                includeJs(current, "EditMobile.js"),
                includeJs(current, "EditTypeBoolean.js"),
                includeJs(current, "EditTypeDefault.js"),
                includeJs(current, "EditTypeEnum.js"),
                includeJs(current, "EditTypeInteger.js"),
                includeJs(current, "EditTypeNumber.js"),
                includeJs(current, "EditTypeString.js"),
                includeJs(current, "EditTypeCollection.js"),
                includeJs(current, "EditTypeObject.js"),
                includeJs(current, "EditTypeDateTime.js")
            ]
        );
    }

    static IsPrimitive(type) {
        return (type.Flags & TypeMemberFlags.IsPrimitive) !== 0;
    }

    static async CreateDefault(type, options, setTypeName) {
        if (!options)
            options = new EditOptions();
        if (Edit.IsPrimitive(type)) {
            const m = type.Members[0];
            const th = Edit.GetTypeHandler(type.TypeName, m, options);
            return th.CreateDefault(m, options);
        }
        const typeName = type.TypeName;// + "," + type.Asm;
        let o;
        try {
            o = await TypeInfoCache.GetInstance(typeName);
            if (o != null) {
                if (setTypeName && (!o["$type"]))
                    o = Object.assign({ $type: typeName }, o);
                return o;
            }
        }
        catch
        {
        }
        o = {};
        if (setTypeName)
            o["$type"] = typeName;
        if (type.Members)
            type.Members.forEach(m => {
                const th = Edit.GetTypeHandler(m.TypeName, m, options);
                const val = th.CreateDefault(m, options);
                o[m.Name] = val;
            });
        return o;
    }

    ChangeTitle(name) {
        const opt = this.Options;
        let texts = this.Element.getElementsByTagName("SysWeaver-TypeTitle");
        if (texts.length > 0) {
            const text = texts[0];
            if (name != text.textContent) {
                text.innerText = name;
                if (opt.MenuIcon) {
                    const c = text.parentElement;
                    c.title = text.BaseTitle + "\n\n" + ValueFormat.copyOnClick(c, name);
                }
            }
        }
        if (Edit.IsPrimitive(this.Type) && (!opt.Title))
        {
            texts = this.Element.getElementsByTagName("SysWeaver-MemberTitle");
            if (texts.length > 0) {
                const text = texts[0];
                if (name != text.textContent) {
                    text.innerText = name;
                    if (opt.MenuIcon) {
                        const c = text.parentElement;
                        c.title = text.BaseTitle + "\n\n" + ValueFormat.copyOnClick(c, name);
                    }
                }
            }
        }
    }

    async FindItem(name) {
        const p = name.split('.');
        const pl = p.length;
        const m = this.MemberMap;
        if (pl <= 1) {
            const t = this.Type;
            if (Edit.IsPrimitive(t))
                return m.entries().next().value[1];
            return this;// m.get(name);
        }
        const sub = p[1].split('[');
        const mn = sub[0];
        const item = m.get(mn);
        if (!item)
            return null;
        if ((pl <= 2) && (sub.length <= 1))
            return item;
        p.splice(0, 1);
        const part = p.join(".");
        return await item.FindItem(part);        
    }


    GetObjectName() {
        let n = this.ObjName;
        const kn = this.KeyName;
        if (kn)
            n += kn;
        const p = this.Parent;
        if (p)
            n = p.GetObjectName() + "." + n;
        return n;
    }

    async Invoke(doChange, doReverse, title, name, tracking) {
        let n = this.GetObjectName();
        const t = this.Type;
        if (!Edit.IsPrimitive(t))
            if (name)
                n += "." + name;

        let edit = this;
        while (edit.Parent)
            edit = edit.Parent;
        const root = edit;

        const onRedo = async isFirst => {
            const item = await root.FindItem(n);
            await doChange(item);
            if (item)
                await item.UpdateValue(isFirst);
        }; 

        const onUndo = async () => {
            const item = await root.FindItem(n);
            await doReverse(item);
            if (item)
                await item.UpdateValue();
        };


        await this.UndoStack.Invoke(onRedo, onUndo, title, n, tracking);
    }

    async UpdateValue()
    {
        this.SetObject(this.GetObject());
    }

    constructor(type, style, options, parentEditor, member, evListener) {

        if (!options)
            options = new EditOptions();
        //  Style
        const isMob = isMobile();
        const width = window.innerWidth;
        const height = window.innerHeight;
        const isPortrait = width < height;
        const zoomIn = isMob && isPortrait;
        const minScale = zoomIn ? 200 : 400;
        const maxScale = zoomIn ? 400 : 800;
        const editor = this;

        let undoStack = options.UndoStack;
        if (!undoStack)
            if (parentEditor)
                undoStack = parentEditor.UndoStack;
        if (!undoStack)
            undoStack = new UndoStack();
        this.UndoStack = undoStack;


        let objName = options.Name;
        if (!objName)
        {
            objName = type.TypeName;
            if (objName) {
                const os = objName.split('.');
                objName = os[os.length - 1];
            }
        }
        if (!objName)
            objName = "Edit";
//        if (parentEditor)
            //objName = parentEditor.ObjName + "." + objName;
        this.ObjName = objName;



        if (!style) {
            style = "Desktop";
            if (isPortrait) {
                if ((width <= maxScale) || isMob) {
                    style = "Mobile";
                }
            }
        }
        this.Style = style;
        //  Scaling
        let scale = 1;
        if (isMob) {
            if (width > maxScale)
                scale = width / maxScale;
            else
                if (width < minScale)
                    scale = width / minScale;
        }
        scale *= 100;
        scale = scale + "%";
        const e = document.createElement("SysWeaver-Edit" + style);
        if (evListener)
            e.addEventListener("EditChange", evListener);
        if (!options.Border)
            e.classList.add("NoBorder");
        e.style.zoom = scale;
        if (!parentEditor)
            parentEditor = null;
        if (parentEditor) {
            const pe = parentEditor.Element;
            e.addEventListener("EditChange", ev => {
                pe.dispatchEvent(Edit.CreateChangeEvent(member, editor.GetObject(), ev, parentEditor.Key, parentEditor.KeyName));
            });
        }

        const table = document.createElement("table");
        e.appendChild(table);
        const edit = eval("new Edit" + style + "()");
        this.Element = e;
        this.Member = member;
        this.Type = type;
        this.Parent = parentEditor;
        this.Options = options;
        this.Table = table;
        this.Edit = edit;
        this.AddMenuItems = null;
        this.Key = null;
        this.TypeHandler = Edit.GetTypeHandler(type.TypeName, member ?? (((type.Flags & (TypeMemberFlags.Collection | TypeMemberFlags.IsPrimitive)) !== 0) ? type.Members[0] : null));
        this.RemoveListerners = [];
        if (this.TypeHandler.IgnoreMembers) {

            type = Object.assign({}, type);
            type.Flags |= TypeMemberFlags.IsPrimitive;
            this.Type = type;
            const newMem = Object.assign({}, type);
            newMem.Name = newMem.DisplayName;
            type.Members = [newMem];
        }
        e["Editor"] = this;
        if (options.Title) {
            let title;
            if (options.Dev) {
                title = ValueFormat.AddNonNullLine("", _TF("Type name", 'This is the header of a tool tip row that displays the name of a .NET data type, ex: "Type name: System.String"'), type.TypeName);
                title = ValueFormat.AddNonNullLine(title, _TF("Assembly", 'This is the header of a tool tip row that displays the name of the assembly that a .NET data type was declared in, ex: "Assembly: SysWeaver.Network.Clien"'), type.Asm);
            }
            title = ValueFormat.AddNonNullLine(title, _TF("Summary", 'This is the header of a tool tip row that displays the summary text extracted from a code comment on a method, ex. "Summary: Returns a random prime"'), type.Summary);
            title = ValueFormat.AddNonNullLine(title, _TF("Remarks", 'This is the header of a tool tip row that displays the remarks text extracted from a code comment on a method, ex. "Remarks: This method is not thread safe"'), type.Remarks);
            //type.DisplayName += (" " + window.innerWidth + "x" + window.innerHeight + " " + scale);
            edit.AddTypeInfo(this, table, type, title, options);
        }

        Edit.LastEdit = editor;
        e.onkeydown =  async ev => {
            if (ev.ctrlKey && (!ev.shiftKey) && (!ev.altKey) && (!ev.metaKey)) {
                if (ev.key === 'z') {
                    ev.preventDefault();
                    ev.stopPropagation();
                    await undoStack.Undo();
                }
                if (ev.key === 'y') {
                    ev.preventDefault();
                    ev.stopPropagation();
                    await undoStack.Redo();
                }
            }
            Edit.LastEdit = editor;
        };
        Edit.AddGlobalListener();

    }

    static LastEdit = null;

    static AddGlobalListener() {
        if (Edit.HaveGlobalListener)
            return;
        Edit.HaveGlobalListener = true;
        document.body.addEventListener("keydown", async ev => {
            const e = Edit.LastEdit;
            if (!e)
                return;
            const undoStack = e.UndoStack;
            if (ev.ctrlKey && (!ev.shiftKey) && (!ev.altKey) && (!ev.metaKey)) {
                if (ev.key === 'z') {
                    ev.preventDefault();
                    ev.stopPropagation();
                    await undoStack.Undo();
                }
                if (ev.key === 'y') {
                    ev.preventDefault();
                    ev.stopPropagation();
                    await undoStack.Redo();
                }
            }
        });
    }

    static HaveGlobalListener = false;


    static CreateChangeEvent(member, value, next, key, keyName) {
        if (member)
            if ((member.Flags & 2) != 0)
                if (value)
                    value = "***";
        return new CustomEvent("EditChange", {
            detail:
            {
                Next: next ? next : null,
                Member: member,
                Value: value,
                Key: key ? key : null,
                KeyName: keyName ? keyName : null,
            }
        });
    }

    static IsDifferent(a, b) {
        const aa = typeof (a);
        const bb = typeof (b);
        if (aa !== bb)
            return true;
        if (aa !== "object")
            return a !== b;
        return JSON.stringify(a) !== JSON.stringify(b);
    }

    GetObject() {
        const t = this.Type;
        const obj = this.Object;
        if (!Edit.IsPrimitive(t))
            return obj;
        return obj[t.Members[0].Name];
    }

    GetCleanCopy(src) {
        const t = this.Type;
        if (Edit.IsPrimitive(t))
            return src;
        const n = {};
        const m = t.Members;
        const ml = m.length;
        for (let i = 0; i < ml; ++i) {
            const name = m[i].Name;
            n[name] = src[name];
        }
        const s = JSON.stringify(n);
        return JSON.parse(s);
    }

    static async CleanUp(type, obj) {
        if (!obj)
            return;
        if (Edit.IsPrimitive(type))
            return obj;
        var valid = new Map();
        valid.set("$type", true);
        const m = type.Members;
        const ml = m.length;
        for (let i = 0; i < ml; ++i) {
            const name = m[i].Name;
            valid.set(name, true);
        }
        for (let prop in obj) {
            if (Object.prototype.hasOwnProperty.call(obj, prop)) {
                if (!valid.get(prop))
                    delete obj[prop];
            }
        }
        for (let i = 0; i < ml; ++i) {
            const mi = m[i];
            if ((mi.Flags & TypeMemberFlags.IsPrimitive) !== 0)
                continue;
            const val = obj[mi.Name];
            if (!val)
                continue;
            if (Array.isArray(val)) {
                const elementType = await Edit.GetType(mi.ElementTypeName, mi.ElementInst);
                const al = val.length;
                for (let j = 0; j < al; ++j)
                    await Edit.CleanUp(elementType, val[j]);
                continue;
            }
            if (typeof val === 'object') {
                const objectType = await Edit.GetType(mi.TypeName, mi.ElementInst);
                await Edit.CleanUp(objectType, val);
                continue;
            }
            i = i;
        }
    }



    async SetObject(obj) {
        const editor = this;
        const options = this.Options;
        const type = this.Type;
        if (typeof obj === "undefined")
            obj = await Edit.CreateDefault(type, options);
        let members = type.Members;
        const edit = this.Edit;
        const table = this.Table;
        const e = this.Element;
        const isPrimitive = Edit.IsPrimitive(type);
        if (isPrimitive) {
            const x = {};
            x[members[0].Name] = obj;
            obj = x;
        } else {
            if (!obj)
                obj = await Edit.CreateDefault(type, options);
        }
        this.Object = obj;
        let rl = table.rows.length;
        const end = options.Title ? 1 : 0;
        while (rl > end) {
            --rl;
            table.deleteRow(rl);
        }
        const listeners = this.RemoveListerners;
        listeners.forEach(x => e.removeEventListener("EditChange", x));
        listeners.length = 0;

        if (members) {

            const ml = members.length;
            let lastMi = ml;
            while (lastMi > 0) {
                --lastMi;
                const member = members[lastMi];
                const flags = member.Flags;
                if ((flags & TypeMemberFlags.Hide) === 0)
                    break;
            }
            for (let i = 0; i < ml; ++i) {
                const member = members[i];
                const flags = member.Flags;
                if ((flags & TypeMemberFlags.Hide) !== 0)
                    continue;
                const mn = member.Name;
                let title;
                if (options.Dev) {
                    title = ValueFormat.AddNonNullLine("", _TF("Member name", 'This is the header of a tool tip row that displays the name of a .NET property or field, ex: "Member name: InstanceId"'), mn);
                    title = ValueFormat.AddNonNullLine(title, _TF("Type name", 'This is the header of a tool tip row that displays the name of a .NET data type, ex: "Type name: System.String"'), member.TypeName);
                    title = ValueFormat.AddNonNullLine(title, _TF("Summary", 'This is the header of a tool tip row that displays the summary text extracted from a code comment on a method, ex. "Summary: Returns a random prime"'), member.Summary);
                    title = ValueFormat.AddNonNullLine(title, _TF("Remarks", 'This is the header of a tool tip row that displays the remarks text extracted from a code comment on a method, ex. "Remarks: This method is not thread safe"'), member.Remarks);
                } else {
                    title = member.Summary;
                }
                const item = await edit.AddMember(obj, this, table, member, title, options, i === lastMi);
                if (!item)
                    continue;
                const mt = member.Type;
                if (mt && mt.startsWith("Hide:")) {
                    const cmp = mt.substring(5);
                    const fn = new Function("return " + cmp);
                    const dataRow = item.DataRow;
                    if (fn.call(obj)) {
                        item.Hide();
                        if (dataRow)
                            dataRow.classList.add("Hide");
                    }
                    const ls = () => {
                        if (fn.call(obj)) {
                            item.Hide();
                            if (dataRow)
                                dataRow.classList.add("Hide");
                        }
                        else {
                            item.Show();
                            if (dataRow)
                                dataRow.classList.remove("Hide");
                        }
                    };
                    e.addEventListener("EditChange", ls);
                    listeners.push(ls);
                }
                editor.MemberMap.set(mn, item);
                item.Icon.classList.add("EditButton");

                const popupMenu = async ev => {
                    if (badClick(ev))
                        return;

                    await PopUpElementMenu(item.TitleElement, async (popupMenuBackElement, close) => {

                        popupMenuBackElement.classList.add("DefaultMenu");
                        const menu = new WebMenu();
                        menu.Name = "EditObject";
                        const def = item.DefValue;
                        const defText = item.DefValueText;
                        const value = obj[mn];
                        let enabled = Edit.IsValid(def);
                        let title = enabled ?
                            _T("Default value is the same: {0}", defText, "Tool tip description for a menu item that if selected resets a property to the default value, displayed when it's disabled cause the property value is already at default.{0} is replaced with the default value of the property.")
                            :
                            _TF("No default value declared.", "Tool tip description for a menu item that if selected resets a property to the default value, displayed when it's disabled because no default value was declared")
                            ;
                        enabled &= Edit.IsDifferent(value, def);
                        if (options.ReadOnly || ((flags & TypeMemberFlags.ReadOnly) != 0)) {
                            title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
                            enabled = false;
                        }
                        function notifyChange() {
                            e.dispatchEvent(Edit.CreateChangeEvent(member, obj[mn], null, editor.Key, editor.KeyName));
                        }

                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Set default", "Text for a menu item that if selected resets a property to the default value"),
                            Flags: enabled ? 0 : 1,
                            IconClass: "SysWeaverEditIconDefault",
                            Title: enabled ? _T("Reset the value to the default: {0}", defText, "Tool tip description for a menu item that if selected resets a property to the default value.{0} is replaced with the default value of the property.") : title,
                            Data: async () => {
                                const oldVal = obj[mn];
                                await editor.Invoke(
                                    async edit => {
                                        await edit.SetValue(def);
                                    },
                                    async edit => {
                                        await edit.SetValue(oldVal);
                                    },
                                    _T("Set default: {0}", defText, "Command text stored in a log when a property value was reset to it's default.{0} is replaced with the default value of the property."),
                                    mn);
                                close();
                            },
                        }));
                        const clip = navigator.clipboard;
                        if (clip) {
                            const val = editor.GetCopyMember(member);
                            if ((flags & TypeMemberFlags.Password) != 0) {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy", "Text for a menu item that if selected copies the value of a property to the clip board"),
                                    IconClass: "SysWeaverEditIconCopy",
                                    Title: _TF("Can't copy protected data.", "Text for a menu item that if selected copies the value of a property to the clip board, when the option is disabled because the property contains protected content"),
                                    Flags: 1,
                                }));
                            } else {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy", "Text for a menu item that if selected copies the value of a property to the clip board"),
                                    IconClass: "SysWeaverEditIconCopy",
                                    Title: _T("Copy \"{0}\" to the clipboard.", val, "Text for a menu item that if selected copies the value of a property to the clip board.{0} is replaced with the value of the property."),
                                    Data: async () => {
                                        await ValueFormat.copyToClipboard(val);
                                        close();
                                    },
                                }));
                            }
                            enabled = false;
                            title = "";
                            let newVal = null;
                            let clipText = null;
                            if (options.ReadOnly || ((flags & TypeMemberFlags.ReadOnly) != 0)) {
                                title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
                            } else {
                                try {
                                    clipText = await ValueFormat.readFromClipboard();
                                    if (clipText) {
                                        const wasJson = member.TypeName !== "System.String";
                                        let obj = clipText;
                                        if (wasJson) {
                                            try {
                                                obj = JSON.parse(obj);
                                            }
                                            catch (err)
                                            {
                                                if (typeof obj !== "string")
                                                    throw err;
                                            }
                                        }
                                        const th = item.TypeHandler;
                                        const okType = (wasJson && (obj !== null) && (obj["$type"] === member.TypeName)) ||
                                            ((obj === null) && ((member.Flags & TypeMemberFlags.AcceptNull) !== 0));
                                        if (okType || th.IsOfType(obj, member, options)) {
                                            newVal = th.Condition(obj, member, options);
                                            enabled = true;
                                            if (wasJson)
                                                title = _T("Set the value to the clipboard object: {0}", clipText, "Tool tip description on a menu item that when clicked will set the property value to the complex object value on the clip board.{0} is replaced with a textual representation of the complex object value on the clip board.");
                                            else {
                                                clipText = "\n" + newVal + "\n";
                                                title = _T("Set the value to the clipboard text: \"{0}\"", newVal, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board.{0} is replaced with a textual representation of the value on the clip board.");
                                            }

                                        } else {
                                            title = _TF("Not a valid object on the clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no valid value on the clipboard.");
                                        }
                                    } else {
                                        title = _TF("No known data on clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no known data found on the clipboard.");
                                    }
                                }
                                catch (e) {
                                    title = _TF("Failed to read from clipboard: {0}", e, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when the clip board data couldn't be read.{0} is replaced with the error message.");
                                }
                            }
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Paste", "Text of a menu item that when clicked will set the property value to the value on the clip board"),
                                Flags: enabled ? 0 : 1,
                                IconClass: "SysWeaverEditIconPaste",
                                Title: title,
                                Data: async () => {

                                    const oldVal = obj[mn];
                                    await editor.Invoke(
                                        async edit => {
                                            await edit.SetValue(newVal);
                                        },
                                        async edit => {
                                            await edit.SetValue(oldVal);
                                        },
                                        _T("Paste: {0}", clipText, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board."),
                                        mn);
                                    close();
                                },
                            }));
                        }
                        const mi = item.AddMenuItems;
                        if (mi)
                            mi(menu, close);
                        if (Edit.IsPrimitive(this.Type)) {
                            const mit = this.AddMenuItems;
                            if (mit)
                                mit(menu.Items, close);
                        }
                        if (clip) {
                            const dn = member.DisplayName ?? mn;
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy name", "Text of a menu item that when clicked will copy the display name of a property to the clip board"),
                                IconClass: "SysWeaverEditIconCopy",
                                Title: _T("Copy the name \"{0}\" to the clipboard.", dn, "Tool tip description on a menu item that when clicked will copy the display name of a property to the clip board.{0} is replaced with the display name of the property"),
                                Data: async () => {
                                    await ValueFormat.copyToClipboard(dn);
                                    close();
                                },
                            }));
                            if (options.Dev) {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy member name", "Text of a menu item that when clicked will copy the member name of a property to the clip board"),
                                    IconClass: "SysWeaverEditIconCopy",
                                    Title: _T("Copy the member name \"{0}\" to the clipboard.", mn, "Tool tip description on a menu item that when clicked will copy the member name of a property to the clip board.{0} is replaced with the member name of the property"),
                                    Data: async () => {
                                        await ValueFormat.copyToClipboard(mn);
                                        close();
                                    },
                                }));
                                const tn = member.TypeName;
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy type name", "Text of a menu item that when clicked will copy the .NET type name of a property to the clip board"),
                                    IconClass: "SysWeaverEditIconCopy",
                                    Title: _T("Copy the type name \"{0}\" to the clipboard.", tn, "Tool tip description on a menu item that when clicked will copy the .NET type name of a property to the clip board.{0} is replaced with a .NET type name"),
                                    Data: async () => {
                                        await ValueFormat.copyToClipboard(tn);
                                        close();
                                    },
                                }));
                            }
                        }
                        const menuStyle = new MainMenuStyle();
                        menuStyle.HideFn = close;
                        const popupMenu = new MainMenu(menu, menuStyle, popupMenuBackElement);

                        TrackElement(ev instanceof MouseEvent ? ev : item.Icon, popupMenuBackElement);
                    });
                }

                const te = item.TitleElement;
                te.title = te.title + "\n\n" +
                    _TF("Click to show options.", "Tool tip description for a button that when pressed will pop-up a menu")
                    ;
                te.onclick = popupMenu;
                te.oncontextmenu = ev => {
                    if (badClick(ev, true))
                        return true;
                    popupMenu(ev);
                    return false;
                };

                if (options.MenuIcon) {
                    const icon = new ColorIcon(options.MenuIconClass, options.MenuColorClass, 24, 24,
                        _TF("Click to show options.", "Tool tip description for a button that when pressed will pop-up a menu")
                        , popupMenu, null, null, true);
                    item.Icon.appendChild(icon.Element);
                }
            }
        }
    }

    MemberMap = new Map();



 


    static Types = new Map();

    static AddType(type)
    {
        const tn = type.TypeName;
        const m = Edit.Types;
        if (m.has(tn))
            return;
        m.set(tn, type);
    }

    static CloneType(src, props) {
        if (!src)
            return null;
        src = JSON.parse(JSON.stringify(src));
        if (props)
            Object.assign(src, props);
        return src;
    }


    static async GetType(typeName, props, isJsType)
    {
        const m = Edit.Types;
        let t = m.get(typeName);
        if (t) {
            return Edit.CloneType(t, props);
        }
        if (isJsType)
            return null;

        const r = await TypeInfoCache.GetType(typeName);
        if (!r) {
            m.set(typeName, null);
            return null;
        }
        m.set(typeName, r);
        return Edit.CloneType(r, props);
    }

    static async FromServer(serverTypeName, style, options, props, obj) {

        const t = await Edit.GetType(serverTypeName, props);
        if (!t)
            return null;
        const e = new Edit(t, style, options);
        await e.SetObject(obj);
        return e;
    }

    static IsValid(value) {
        return typeof value !== "undefined";
    }

    static async SetMember(obj, editor, editContext, member, title, options, c) {
        const th = this.GetTypeHandler(member.TypeName, member, options);
        const res = await th.AddEditor(obj, editor, editContext, member, title, options);
        const mn = member.Name;
        if (res) {
            res.TypeHandler = th;
            res.TitleElement = c;
            c.draggable = true;
            c.classList.add("EditDraggable");
            c.addEventListener("dragstart", ev => {
                const data = editor.GetCopyMember(member, true);
                if (data) {
                    console.log("Dragging: " + data);
                    ev.dataTransfer.setData("application/json", data);
                    ev.dataTransfer.effectAllowed = "copy";
                }
            });
            if ((!options.ReadOnly) && ((member.Flags & 16) == 0)) {
                c.ondragover = ev => {
                    ev.preventDefault();
                    ev.dataTransfer.dropEffect = "copy";
                };
                c.ondrop = async ev => {
                    ev.preventDefault();
                    const json = ev.dataTransfer.getData("application/json");
                    if (json) {

                        try {
                            let obj = JSON.parse(json);
                            const okType = ((obj !== null) && (obj["$type"] === member.TypeName)) ||
                                ((obj === null) && ((member.Flags & TypeMemberFlags.AcceptNull) !== 0));
                            if (okType || th.IsOfType(obj, member, options)) {
                                obj = th.Condition(obj, member, options);
                                const current = editor.Object;
                                const oldVal = current[mn];
                                if (oldVal != obj) {
                                    await editor.Invoke(
                                        async edit => {
                                            await edit.SetValue(obj);
                                        },
                                        async edit => {
                                            await edit.SetValue(oldVal);
                                        },
                                        _T("Dropped: {0}", obj, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property."),
                                        mn, null, res.Focus);
                                }
                            } else {
                                console.warn("Data mimatch: " + json);
                            }
                        }
                        catch (e) {
                            console.warn("Invalid data: " + json);
                        }
                    } else {
                        let obj = ev.dataTransfer.getData("text/plain");
                        if (obj) {
                            if (th.IsOfType(obj, member)) {
                                obj = th.Condition(obj, member, options);
                                const current = editor.Object;
                                const oldVal = current[mn];
                                if (oldVal != obj) {
                                    await editor.Invoke(
                                        async edit => {
                                            await edit.SetValue(obj);
                                        },
                                        async edit => {
                                            await edit.SetValue(oldVal);
                                        },
                                        _T("Dropped: {0}", obj, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property."),
                                        mn, null, res.Focus);
                                }
                            } else {
                                console.warn("Data mimatch:  \"" + obj + "\"");
                            }
                        }
                    }
                };
            }
        }
        return res;

    }

    static IsTrue(val) {
        val = "" + val;
        if (val === "1")
            return true;
        if (val === "true")
            return true;
        if (val === "True")
            return true;
        return false;
    }


    static CreateGenericInput(inputType, getValue, setValue, parseValue, toString, validateValue, readOnly) {
        const i = document.createElement("input");
        i.type = inputType;
        let val = getValue();
        const orgVal = val;
        i.value = toString(val);
        if (readOnly) {
            i.readOnly = true;
            i.classList.add("EditReadOnly");
        }
        let isInternal = false;
        const onChange = async (ev, isTemp)  => {
            if (isInternal)
                return;
            isInternal = true;
            let newValue = parseValue(i.value);
            if (validateValue)
                newValue = validateValue(newValue);
            if (!isTemp) {
                const newStr = toString(newValue);
                if (newStr != i.value)
                    i.value = newStr;
            }
            await setValue(newValue, ev);
            isInternal = false;
        };
        i.addEventListener("change", async ev => await onChange(ev, false));
        i.addEventListener("input", async ev => await onChange(ev, true));
        i.onkeyup = async ev => {
            if (isPureClick(ev)) {
                if (ev.key === "Escape") {
                    ev.stopPropagation();
                    val = orgVal;
                    i.value = toString(val);
                    await onChange(ev);
                }
            }
        };
        i.UpdateValue = () => {
            if (isInternal)
                return;
            isInternal = true;
            i.value = toString(getValue());
            isInternal = false;
        };
        return i;
    }

    static CreateTextInput(getValue, setValue, parseValue, toString, placeHolder, validateValue, validateKeyOrPattern, maxLength, def, readOnly, numberOfLines) {
        const isMultiLine = numberOfLines && (numberOfLines > 1);
        let i;
        if (isMultiLine) {
            i = document.createElement("textarea");
        } else {
            i = document.createElement("input");
            i.type = "text";
        }
        if (maxLength)
            i.setAttribute("maxlength", "" + maxLength);
        let val = getValue();
        const orgVal = val;
        i.value = toString(val);
        if (readOnly) {
            i.readOnly = true;
            i.classList.add("EditReadOnly");
        } else {
            if (placeHolder)
                i.placeholder = placeHolder.split('\n')[0];
        }
        let isInternal = false;
        if (validateKeyOrPattern) {
            if (typeof validateKeyOrPattern == "string") {
                const valid = new Map();
                const sl = validateKeyOrPattern.length;
                for (let i = 0; i < sl; ++i)
                    valid.set(validateKeyOrPattern.charAt(i), true);
                validateKeyOrPattern = chr => {
                    return valid.get(chr);
                };
            }
            i.addEventListener("beforeinput", ev => {
                if (!ev.data)
                    return;
                const t = "" + ev.data;
                const tl = t.length;
                if (tl <= 0)
                    return;
                const ta = ev.target;
                const ts = ta.selectionStart;
                const te = ta.selectionEnd;
                const val = ta.value;
                let o = "";
                for (let i = 0; i < tl; ++i) {
                    const ch = t.charAt(i);
                    if (validateKeyOrPattern(ch, i + ts, val))
                        o += ch;
                }
                if (o.length <= 0) {
                    ev.preventDefault();
                    return;
                }
                if (o == t)
                    return;
                let nextVal = ta.value.substring(0, ts) + o + ta.value.substring(te);
                if (maxLength) {
                    if (nextVal.length > maxLength)
                        nextVal = nextVal.substring(0, maxLength);
                }
                ta.value = nextVal;
                const n = ts + o.length;
                ta.setSelectionRange(n, n);
                ev.preventDefault();
                ta.dispatchEvent(new Event("change"));
            });
        }
        const onChange = async (ev, isTemp)  => {
            if (isInternal)
                return;
            isInternal = true;
            let newValue = parseValue(i.value);
            if (validateValue)
                newValue = validateValue(newValue);
            if (!isTemp) {
                const newStr = toString(newValue);
                if (newStr != i.value)
                    i.value = newStr;
            }
            await setValue(newValue, ev);
            isInternal = false;
        };
        i.addEventListener("change", async ev => await onChange(ev, false));
        i.addEventListener("input", async ev => await onChange(ev, true));
        i.onkeyup = async ev => {
            if (isPureClick(ev)) {
                if (ev.key === "Escape") {
                    ev.stopPropagation();
                    val = orgVal;
                    i.value = toString(val);
                    await onChange(ev);
                }
            }
        };
        i.UpdateValue = () =>
        {
            if (isInternal)
                return;
            isInternal = true;
            i.value = toString(getValue());
            isInternal = false;
        };
        return i;
    }

    static CreateSliderInput(getStep, setStep, stepCount, readOnly) {
        const i = document.createElement("input");
        i.type = "range";
        i.setAttribute("min", "0");
        i.setAttribute("max", "" + stepCount);
        i.value = getStep();
        let isInternal = false;
        if (readOnly) {
            i.disabled = true;
        } else {
            const cf = async ev => {
                if (isInternal)
                    return;
                isInternal = true;
                await setStep(i.value);
                isInternal = false;
            };
            i.addEventListener("change", cf);
            i.addEventListener("input", cf);
        }
        i.UpdateValue = () => {
            isInternal = true;
            i.value = getStep();
            isInternal = false;
        };
        return i;
    }

    static CreateSelectionInput(getValue, setValue, values, def, readOnly, flags, onOpenFn) {
        const i = document.createElement("select");
        const vl = values.length;
        if (readOnly) {
            i.disabled = true;
            i.classList.add("EditReadOnly");
        }
        if (flags) {
            i.multiple = true;
            i.size = "" + (vl < 4 ? vl : 4);
        }
        const opts = [];
        let val = getValue();
        for (let j = 0; j < vl; ++j) {
            const kv = values[j];
            const o = document.createElement("option");
            const ev = kv[0];
            o.KeyValue = kv;
            o.innerText = kv[1];
            o.value = ev;
            o.selected = flags ? ((val & ev) == ev) && ((ev != 0) || (val == 0)) : (ev == val);
            o.PrevSel = o.selected;
            o.title = kv.length >= 2 ? kv[2] : "";
            opts[j] = o;
            i.appendChild(o);
        }
        if (onOpenFn) {
            i.addEventListener('focusin', e => {
                onOpenFn(opts);
            });
        }
        let isInternal = false;
        i.addEventListener("change", async ev => {
            if (isInternal)
                return;
            isInternal = true;
            if (flags) {
                val = getValue();
                for (let j = 0; j < vl; ++j) {

                    const o = opts[j];
                    if (o.selected == o.PrevSel)
                        continue;
                    if (o.selected)
                        val |= o.value;
                    else
                        val &= (~(o.value));
                }
                for (let j = 0; j < vl; ++j) {
                    const o = opts[j];
                    const ev = o.value;
                    o.selected = ((val & ev) == ev) && ((ev != 0) || (val == 0));
                    o.PrevSel = o.selected;
                }
                await setValue(val);
            } else {
                const sel = opts[i.selectedIndex];
                await setValue(sel.value);
            }
            isInternal = false;
        });
        i.UpdateValue = () => {
            isInternal = true;
            val = getValue();
            for (let j = 0; j < vl; ++j) {
                const o = opts[j];
                const ev = o.value;
                o.selected = flags ? ((val & ev) == ev) && ((ev != 0) || (val == 0)) : (ev == val);
                o.PrevSel = o.selected;
            }
            isInternal = false;
        };
        return i;
    }

    static async ValidateType(type, obj, options, name) {
        const members = type.Members;
        if (!members)
            return null;
        const isPrimitive = Edit.IsPrimitive(type);
        const ml = members.length;
        for (let i = 0; i < ml; ++i) {
            const member = members[i];
            const flags = member.Flags;
            if ((flags & TypeMemberFlags.Hide) !== 0)
                continue;
//            if ((flags & TypeMemberFlags.ReadOnly) !== 0)
//                continue;
            const mn = member.Name;
            const memName = name ? (isPrimitive ? name : (name + "." + mn)) : mn;
            const th = this.GetTypeHandler(member.TypeName, member, options);
            const val = isPrimitive ? obj : obj[mn];
            if (typeof val === "undefined")
                return [memName, member.DisplayName + ": " + _TF("No value found!", "Error message displayed when a value for a .NET type isn't found")];
            const r = await th.Validate(val, isPrimitive ? type : member, options, memName);
            if (r)
                return r;
        }
        return null;
    }

    async Validate(focusOnError)
    {
        const e = await Edit.ValidateType(this.Type, this.Object, this.Options, Edit.IsPrimitive(this.Type) ? null : "Edit");
        if (!e)
            return null;
        if (!focusOnError)
            return e;
        const fi = await this.FindItem(e[0]);
        if (!fi)
            return e;
        const fe = fi.Focus;
        if (!fe)
            return e;
        fe.focus();
        return e;
    }

    static GetIntParam(val, def) {
        try {
            if (Edit.IsValid(val))
                return parseInt(val ?? ("" + def));
        }
        catch
        {
        }
        return def;
    }

    static GetTypedTextOrJson(obj, forceJson, typeName) {
        if (obj === null)
            return "null";
        const ot = typeof (obj);
        if (!forceJson)
            if (ot === "string")
                return obj;
        if ((ot !== "object") || Array.isArray(obj))
            return JSON.stringify(obj);
        if (!obj["$type"])
            obj = Object.assign({ $type: typeName }, obj);
        return JSON.stringify(obj);

    }

    GetCopyObject(forceJson) {
        const t = this.Type;
        const tn = t.TypeName;
        return Edit.GetTypedTextOrJson(this.GetObject(), forceJson, tn);
    }

    GetCopyMember(member, forceJson) {
        let obj = this.GetObject();
        if (!Edit.IsPrimitive(this.Type))
            obj = obj[member.Name];
        return Edit.GetTypedTextOrJson(obj, forceJson, member.TypeName);
    }

    CanConsumeCopy(obj) {
        const isObject = typeof (obj) === "object";
        const type = this.Type;
        if (Edit.IsPrimitive(type)) {
            if (isObject)
                return false;
            const th = Edit.GetTypeHandler(type.TypeName);
            if (!th.IsOfType(obj, type))
                return false;
            return true;
        }
        if (obj["$type"] != type.TypeName)
            return false;
        //delete obj["$type"];
        return true;
    }

    NotifyChange() {
        this.Element.dispatchEvent(Edit.CreateChangeEvent(null, this.GetObject()), null, this.Key, this.KeyName);

    }

    SetTypeInfo(c, expandElement, type, title, options) {

        let name = options.KeyName;
        if (!name)
            name = type.DisplayName;
        const isReadOnly = options.ReadOnly || (Edit.IsPrimitive(type) && ((type.Members[0].Flags & 16) != 0));
        if (options.CanExpand && (!Edit.IsPrimitive(type))) {
            let isExpanded = options.IsExpanded;

            let expandIcon;

            function setExpand() {
                if (isExpanded) {
                    expandElement.classList.remove("EditorCollapsed");
                    expandIcon.ChangeImage("SysWeaverEditIconCollapse");
                    expandIcon.SetTitle(
                        _TF("Click to hide object members", "Tool tip description on a button that when clicked will hide the list of object members")
                    );
                } else {
                    expandElement.classList.add("EditorCollapsed");
                    expandIcon.ChangeImage("SysWeaverEditIconExpand");
                    expandIcon.SetTitle(
                        _TF("Click to show object members", "Tool tip description on a button that when clicked will display a list of object members")
                    );
                }
            }

            function toggleExpand() {
                isExpanded ^= true;
                setExpand();
            }

            expandIcon = new ColorIcon(
                isExpanded ? "SysWeaverEditIconCollapse" : "SysWeaverEditIconExpand",
                "IconColorThemeMain", 24, 24,
                isExpanded
                    ?
                    _TF("Click to hide object members", "Tool tip description on a button that when clicked will hide the list of object members")
                    :
                    _TF("Click to show object members", "Tool tip description on a button that when clicked will display a list of object members")
                ,
                toggleExpand
            );

            setExpand();

            c.appendChild(expandIcon.Element);
        }
        const text = document.createElement("SysWeaver-TypeTitle")
        text.innerText = name;
        text.BaseTitle = title;
        title = title + "\n" + ValueFormat.copyOnClick(c, name);
        c.title = title;

        const menuElement = document.createElement("SysWeaver-PopupMenuIcon");

        const edit = this;
        const e = this.Element;

        const menuIcon = new ColorIcon("SysWeaverEditIconMenu", "IconColorThemeAcc2", 24, 24,
            _TF("Click to show options.", "Tool tip description for a button that when pressed will pop-up a menu")
            , async ev => {
            if (badClick(ev))
                return;
            await PopUpElementMenu(menuIcon.Element, async (popupMenuBackElement, close) => {

                const menu = new WebMenu();
                menu.Name = "EditProp";
                menu.Items.push(WebMenuItem.From({
                    Name: _TF("Set default", "Text for a menu item that if selected resets a property to the default value"),
                    Flags: isReadOnly ? 1 : 0,
                    IconClass: "SysWeaverEditIconDefault",
                    Title: isReadOnly ?
                        _TF("Value is read only", "Tool tip description on a property value that can't be edited")
                        :
                        _TF("Reset the value to the default", "Tool tip description for a menu item that if selected resets a property to the default value.")
                    ,
                    Data: async () => {
                        const old = edit.GetObject();
                        await edit.Invoke(
                            async xedit => {
                                await xedit.SetObject();
                                xedit.NotifyChange();
                            },
                            async xedit => {
                                await xedit.SetObject(old);
                                xedit.NotifyChange();
                            },
                            _TF("Set default", "Text for a menu item that if selected resets a property to the default value"));
                        close();
                    },
                }));
                const clip = navigator.clipboard;
                if (clip) {
                    const val = edit.GetCopyObject();
                    menu.Items.push(WebMenuItem.From({
                        Name: _TF("Copy", "Text for a menu item that if selected copies the value of a property to the clip board"),
                        IconClass: "SysWeaverEditIconCopy",
                        Title: _T("Copy \"{0}\" to the clipboard.", val, "Text for a menu item that if selected copies the value of a property to the clip board.{0} is replaced with the value of the property."),
                        Data: async () => {
                            await ValueFormat.copyToClipboard(val);
                            close();
                        },
                    }));
                    let enabled = false;
                    title = "";
                    let newVal = null;
                    let newValueText = "";
                    if (isReadOnly) {
                        title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
                    } else {
                        try {
                            const clipText = await ValueFormat.readFromClipboard();
                            if (clipText) {
                                const typeName = edit.Type.TypeName;
                                const wasJson = typeName !== "System.String";
                                let obj = clipText;
                                if (wasJson)
                                    obj = JSON.parse(obj);
                                const th = edit.TypeHandler;

                                const okType = (wasJson && (obj !== null) && (obj["$type"] === typeName)) ||
                                    ((obj === null) && ((edit.Type.Flags & TypeMemberFlags.AcceptNull) !== 0));
                                if (okType || th.IsOfType(obj, edit.Type)) {
                                    newVal = th.Condition(obj, edit.Type);
                                    enabled = true;
                                    if (wasJson) {
                                        title = _T("Set the value to the clipboard object: {0}", clipText, "Tool tip description on a menu item that when clicked will set the property value to the complex object value on the clip board.{0} is replaced with a textual representation of the complex object value on the clip board.");
                                        newValueText = clipText;
                                    }
                                    else {
                                        title = _T("Set the value to the clipboard text: \"{0}\"", clipText, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board.{0} is replaced with a textual representation of the value on the clip board.");
                                        newValueText = "\"" + clipText + "\"";
                                    }
                                } else {
                                    title = _TF("Not a valid object on the clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no valid value on the clipboard.");
                                }
                            } else {
                                title = _TF("No known data on clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no known data found on the clipboard.");
                            }
                        }
                        catch (e) {
                            title = _TF("Failed to read from clipboard: {0}", e, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when the clip board data couldn't be read.{0} is replaced with the error message.");
                        }
                    }
                    menu.Items.push(WebMenuItem.From({
                        Name: _TF("Paste", "Text of a menu item that when clicked will set the property value to the value on the clip board"),
                        Flags: enabled ? 0 : 1,
                        IconClass: "SysWeaverEditIconPaste",
                        Title: title,
                        Data: async () => {
                            const old = edit.GetObject();
                            await edit.Invoke(
                                async xedit => {
                                    await xedit.SetObject(newVal);
                                    xedit.NotifyChange();
                                },
                                async xedit => {
                                    await xedit.SetObject(old);
                                    xedit.NotifyChange();
                                },
                                _T("Paste: {0}", newValueText, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board.")
                                );
                            close();
                        },
                    }));
                }

                const add = edit.AddMenuItems;
                if (add)
                    add(menu.Items, close);

                const menuStyle = new MainMenuStyle();
                menuStyle.HideFn = close;
                const popupMenu = new MainMenu(menu, menuStyle, popupMenuBackElement);

            });

        }, null, null, true);
        menuElement.appendChild(menuIcon.Element);
        c.appendChild(menuElement);
        c.appendChild(text);

        c.draggable = true;
        c.classList.add("EditDraggable");
        c.addEventListener("dragstart", ev =>
        {
            const data = edit.GetCopyObject(true);
            if (data) {
                console.log("Dragging: " + data);
                ev.dataTransfer.setData("application/json", data);
                ev.dataTransfer.effectAllowed = "copy";
            }
        });
        if (!options.ReadOnly) {
            c.ondragover = ev => {
                ev.preventDefault();
                ev.dataTransfer.dropEffect = "copy";
            };
            c.ondrop = async ev => {
                ev.preventDefault();
                const th = edit.TypeHandler;
                const json = ev.dataTransfer.getData("application/json");
                if (json) {
                    try {

                        let obj = JSON.parse(json);
                        if (edit.CanConsumeCopy(obj)) {
                            if (th)
                                obj = th.Condition(obj, edit.Type, options);
                            const oldObj = await edit.GetObject();
                            await edit.Invoke(
                                async xedit => {
                                    await xedit.SetObject(obj);
                                    xedit.NotifyChange();
                                },
                                async xedit => {
                                    await xedit.SetObject(oldObj);
                                    xedit.NotifyChange();
                                },
                                _T("Dropped: {0}", json, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property.")
                                );
                        } else {
                            console.warn("Data mimatch: " + json);
                        }
                    }
                    catch (e) {
                        console.warn("Invalid data: " + json);
                    }
                } else {
                    const obj = ev.dataTransfer.getData("text/plain");
                    if (obj) {
                        if (edit.CanConsumeCopy(obj)) {
                            console.log("Dropped: \"" + obj + "\"");
                            if (th)
                                obj = th.Condition(obj, edit.Type, options);
                            await edit.SetObject(obj);
                            edit.Element.dispatchEvent(Edit.CreateChangeEvent(null, obj, null, edit.Key, edit.KeyName));
                        } else {
                            console.warn("Data mimatch: \"" + obj + "\"");
                        }
                    }
                }
            };
        }
    }

    static SetToString(element, obj) {
        if (!obj) {
            element.innerText = "null";
            element.title = _TF("No object available", "Tool tip description of a property value input box when no object is available");
            return;
        }
        const ot = typeof (obj);
        if (ot === "string") {
            element.innerText = ot;
            element.title = _T("The text is: \"{0}\"", ot, "Tool tip description of a property text value.{0} is replaced with the value text.");
            return;
        }
        if (ot === "number") {
            element.innerText = "" + ot;
            element.title = _T("The number is: {0}", ot, "Tool tip description of a property numerical value.{0} is replaced with the numerical value.");
            return;
        }
        if (ot !== "object") {
            element.innerText = "" + ot;
            element.title = _T("The value is: {0}", ot, "Tool tip description of a property value.{0} is replaced with the value.");
            return;
        }
        let text = null;
        if (!text) {
            try {
                const fn = obj["toString"];
                if (fn) {
                    if (typeof (fn) == "function") {
                        const s = fn();
                        if (s)
                            if (s !== "[object Undefined]")
                                text = s;
                    }
                }
            }
            catch (e) {
            }
        }
        if (!text) {
            try {
                const fn = obj["ToString"];
                if (fn) {
                    if (typeof (fn) == "function") {
                        const s = fn();
                        if (s)
                            text = s;
                    }
                }
            }
            catch (e) {
            }
        }
        if (!text) {
            try {
                const s = JSON.stringify(obj);
                if (s)
                    text = s;
            }
            catch (e) {
            }
        }
        if (!text)
            text = "Value";

        if (text.length > 24) {
            element.innerText = text.substring(0, 21) + "...";
            element.title = text;

        } else {
            element.innerText = text;
            element.title = "";
        }
    }

    static SetCollectionCount(element, collection, container) {
        if (!collection) {
            element.innerText = "null";
            element.title = _TF("The collection is non existent", "Tool tip description on a property value that is a .NET collection when there is no collection (it's null)");
            return;
        }
        const c = container.countVal(collection);
        if (c <= 0) {
            element.innerText = _TF("empty", "Text used to indicate that a list of items is empty");
            element.title = _TF("The collection is empty", "Tool tip description on a property value that is a .NET collection when the collection is empty");
            return;
        }
        if (c == 1) {
            element.innerText = _TF("1 item", "Text used to indicate that a list contains a single item");
            element.title = _TF("There is one item in the collection", "Tool tip description on a property value that is a .NET collection when the collection contains a single item");
            return;
        }
        element.innerText = _T("{0} items", c, "Text used to indicate that a list contains multiple items.{0} is replaced with the number of items.");
        element.title = _T("There are {0} items in the collection", c, "Tool tip description on a property value that is a .NET collection when the collection contains multiple items.{0} is replaced with the number of items in the collection.");
    }

    static GetTypeMap() {
        const map = new Map();
        map.set("System.SByte", new EditTypeInteger(true, 1));
        map.set("System.Int16", new EditTypeInteger(true, 2));
        map.set("System.Int32", new EditTypeInteger(true, 4));
        map.set("System.Int64", new EditTypeInteger(true, 8));

        map.set("System.Byte", new EditTypeInteger(false, 1));
        map.set("System.UInt16", new EditTypeInteger(false, 2));
        map.set("System.UInt32", new EditTypeInteger(false, 4));
        map.set("System.UInt64", new EditTypeInteger(false, 8));

        map.set("System.Single", new EditTypeNumber(-3.40282347e+38, 3.40282347e+38, 11));
        map.set("System.Double", new EditTypeNumber(-1.7976931348623157e+308, 1.7976931348623157e+308, 19));
        map.set("System.Decimal", new EditTypeNumber(-1.7976931348623157e+308, 1.7976931348623157e+308, 19));

        map.set("System.Boolean", new EditTypeBoolean());
        map.set("System.DateTime", new EditTypeDateTime(false));
        map.set("System.DateOnly", new EditTypeDateTime(false, true));
        map.set("System.TimeOnly", new EditTypeDateTime(false, false, true));
        
        function N(n) {
            return "System.Nullable`1[[" + n + "]]";
        }

        map.set(N("System.SByte"), new EditTypeInteger(true, 1, true));
        map.set(N("System.Int16"), new EditTypeInteger(true, 2, true));
        map.set(N("System.Int32"), new EditTypeInteger(true, 4, true));
        map.set(N("System.Int64"), new EditTypeInteger(true, 8, true));

        map.set(N("System.Byte"), new EditTypeInteger(false, 1, true));
        map.set(N("System.UInt16"), new EditTypeInteger(false, 2, true));
        map.set(N("System.UInt32"), new EditTypeInteger(false, 4, true));
        map.set(N("System.UInt64"), new EditTypeInteger(false, 8, true));

        map.set(N("System.Single"), new EditTypeNumber(-3.40282347e+38, 3.40282347e+38, 11, true));
        map.set(N("System.Double"), new EditTypeNumber(-1.7976931348623157e+308, 1.7976931348623157e+308, 19, true));
        map.set(N("System.Decimal"), new EditTypeNumber(-1.7976931348623157e+308, 1.7976931348623157e+308, 19, true));

        map.set(N("System.Boolean"), new EditTypeBoolean(true));
        map.set(N("System.DateTime"), new EditTypeDateTime(false, false, false, true));
        map.set(N("System.DateOnly"), new EditTypeDateTime(false, true, false, true));
        map.set(N("System.TimeOnly"), new EditTypeDateTime(false, false, true, true));

        map.set("System.String", new EditTypeString());
        map.set("System.Object", new EditTypeString(true));

        return map;
    }

    static CollectionTypeHandler = null;
    static DefaultTypeHandler = null; 
    static TypeMap = null;

    static GetObjectTypeHandler()
    {
        let c = Edit.ObjectTypeHandler;
        if (!c) {
            c = new EditTypeObject();
            Edit.ObjectTypeHandler = c;
        }
        return c;
    }

    static GetTypeHandler(typename, member, options) {

        let tm = member ? Edit.TypeMapMember : Edit.TypeMap;
        if (!tm) {
            tm = Edit.GetTypeMap();
            if (member)
                Edit.TypeMapMember = tm;
            else
                Edit.TypeMap = tm;
        }
        const typeOrg = typename;
        if (member) {
            const spec = member.Type;
            if (spec) {
                typename += "|";
                typename += spec;
            }
        }
        const h = tm.get(typename); 
        if (h)
            return h;

        if (member) {
            const spec = member.Type;
            if (spec) {
                const specParts = spec.split(':');
                const editName = "EditType" + specParts[0] + "Factory";
                const cs = window[editName];
                if (typeof cs === 'function') {
                    const c = cs(typeOrg, specParts);
                    tm.set(typename, c);
                    return c;
                }
                const x = tm.get(typeOrg);
                if (x) {
                    tm.set(typename, x);
                    return x;
                }
            }
            const flags = member.Flags;
            if ((flags & TypeMemberFlags.Collection) !== 0) {
                let c = Edit.CollectionTypeHandler;
                if (!c) {
                    c = new EditTypeCollection();
                    Edit.CollectionTypeHandler = c;
                }
                tm.set(typename, c);
                return c;
            }
            if ((flags & TypeMemberFlags.IsObject) !== 0) {
                const c = Edit.GetObjectTypeHandler();
                tm.set(typename, c);
                return c;
            }
            if ((flags & TypeMemberFlags.IsEnum) !== 0) {
                let c = Edit.EnumTypeHandler;
                if (!c) {
                    c = new EditTypeEnum();
                    Edit.EnumTypeHandler = c;
                }
                tm.set(typename, c);
                return c;
            }
        }
        let d = Edit.DefaultTypeHandler;
        if (!d) {
            d = new EditTypeDefault();
            Edit.DefaultTypeHandler = d;
        }
        tm.set(typename, d);
        return d;
    }

}

Edit.Init();

async function editTestMain()
{
    const edit = await Edit.FromServer("SysWeaver.MicroService.EditTest");

    edit.Element.addEventListener("EditChange", ev => {
        let mp = ev.detail.Member;
        let p = mp ? mp.Name : "*";
        let val = ev.detail.Value;
        for (; ;) {
            ev = ev.detail.Next;
            if (!ev)
                break;
            mp = ev.detail.Member;
            const key = ev.detail.KeyName;
            if (key)
                p += key;
            p = p + "." + (mp ? mp.Name : "*");
            val = ev.detail.Value;
        }
        if (typeof val === "object")
            console.log(p + " = " + JSON.stringify(val));
        else
            console.log(p + " = " + val);
    });


    document.body.appendChild(edit.Element);

}