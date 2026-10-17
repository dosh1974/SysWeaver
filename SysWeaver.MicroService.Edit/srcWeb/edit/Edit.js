


/**
 * @file The object editor engine: renders an editable form for any type described by `TypeService`.
 *
 * Loading: host pages include it with a static `<script defer src="../edit/Edit.js">` (e.g. explore/api.html of the
 * API explorer, ServerManager/services.html, mediaView, and the "Edit test" page edit/index.html which calls
 * `editTestMain`). On evaluation it runs `Edit.Init()` (not awaited), which loads type.js, UndoStack.js,
 * EditOptions.js, EditDesktop.js, EditMobile.js and the EditType*.js handlers with `includeJs` relative to this script.
 * Optional handlers such as EditTypeLanguage.js plug in by defining a global `EditType<Spec>Factory` function.
 *
 * Globals defined: class `Edit`, function `editTestMain`.
 *
 * Dependencies (globals from other files): `includeJs`, `ValueFormat`, `ColorIcon`, `PopUpElementMenu`,
 * `TrackElement`, `WebMenu`, `WebMenuItem`, `MainMenu`, `MainMenuStyle`, `badClick`, `isPureClick`, `_T`, `_TF`
 * (common.js / menu scripts of the host page); `TypeMemberFlags`, `TypeInfoCache` (type.js); `UndoStack`,
 * `EditOptions`, `EditDesktop`, `EditMobile`, `EditType*` (this folder).
 *
 * Conventions: type metadata are `TypeDesc` / `TypeMemberDesc` objects from `TypeService.GetTypeInfo` (fields
 * `TypeName`, `DisplayName`, `Flags` (TypeMemberFlags), `Members`, `Name`, `Default`, `Min`, `Max`, `Type` (special
 * type specification), `ElementTypeName`, `ElementInst`, `Summary`, `Remarks`, ...). A primitive type is edited as an
 * object with one member (`{ [Members[0].Name]: value }`). Every change dispatches a bubbling-free `EditChange`
 * `CustomEvent` on the editor element (see `Edit.CreateChangeEvent`); child editors re-dispatch to their parent with
 * `detail.Next` chaining. Undo paths are dotted names built from `ObjName` + `KeyName` segments (see `Invoke`).
 */

/**
 * The form engine. One instance edits one object (or one primitive value) of one type description.
 *
 * Usage: `const e = new Edit(type, style, options, parent, member, evListener)` (or `await Edit.FromServer(typeName)`
 * / `await Edit.Boolean(...)`), then `await e.SetObject(value)` and append `e.Element` to the page. Listen for
 * `EditChange` on `e.Element`, read the value with `e.GetObject()`, validate with `e.Validate(true)`.
 * Child editors (nested objects, collection items) are created by the type handlers with the parent editor as
 * `parentEditor`; they share the root's `UndoStack`.
 *
 * Layout strategy: `EditDesktop` / `EditMobile` (stored in `this.Edit`) create the table rows (`AddTypeInfo`,
 * `AddMember`) and call back into `Edit.SetMember` / `SetTypeInfo`.
 *
 * Type-handler contract (duck-typed). Each `EditType*` handler (and the object returned by a global
 * `EditType<Spec>Factory(typeName, specParts)`) is a plain object returned and cached by `Edit.GetTypeHandler`.
 * The engine calls:
 * - `CreateDefault(member, options)` -> value written into a new object for this member (`Edit.CreateDefault`,
 *   collection "add").
 * - `GetDefault(member, options)` -> default described by the metadata (shape varies per handler).
 * - `IsOfType(obj, member, options)` -> boolean: whether a pasted / dropped value is acceptable.
 * - `Condition(obj, member, options)` -> coerced / clamped value to store for an accepted pasted / dropped value.
 * - `Validate(obj, member, options, name)` (async) -> `null` or `[path, message]`.
 * - `ToString(obj, member, options)` -> display text.
 * - `AddEditor(obj, editor, editContext, member, title, options)` (async) -> item. Builds the editor DOM inside
 *   `editContext.Element` (optionally a data row via `editContext.CreateData()`; `editContext.PrevRow` is the value
 *   row), writes the default into `obj[member.Name]` when undefined and returns an item.
 * - `IgnoreMembers` (optional field): if truthy the type is treated as a single-member primitive (constructor,
 *   EditTypeCollection).
 *
 * The item returned by `AddEditor` has:
 * - `SetValue(v)`: store `v` into `obj[member.Name]` and dispatch `EditChange` (used by undo/redo, menu, drop).
 * - `UpdateValue(isFirst)`: refresh the UI from the object (called after each undo/redo step by `Invoke`).
 * - `Focus`: element focused when validation fails for this member.
 * - `Title`: constraint text prepended to the member tooltip.
 * - `DefValue` / `DefValueText`: default value and its text, for the member menu's "Set default".
 * - optional `DataRow` (row toggled by `Hide:` expressions), `AddMenuItems(menu, close)` (extra member-menu items),
 *   `FindItem(path)` (resolve deeper undo paths) and `GetValue()`.
 * The layout adds `Hide()`, `Show()` and `Icon` (icon cell); `Edit.SetMember` adds `TypeHandler` and `TitleElement`.
 */
class Edit {

    /**
     * Builds a stand-alone editor for a single boolean value (a one-member primitive `System.Boolean` type) using
     * the Desktop layout and one of the `EditOptions.Clean*` presets, sets `value` and forwards every change.
     * @param {string} name - Member name and display name.
     * @param {boolean} value - Initial value.
     * @param {string} title - Summary (tooltip) of the member.
     * @param {function(boolean):(Promise<void>|void)} [onChange] - Called with `ev.detail.Value` on every `EditChange`.
     * @param {boolean} [readOnly] - Use a read-only preset.
     * @param {boolean} [showMenu] - Use a `*Menu` preset (`CleanMenu` / `CleanReadOnlyMenu`).
     * @returns {Promise<Edit>} The editor; append `Element` to the page.
     */
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




    /**
     * Loads the dependent editor scripts in parallel with `includeJs(document.currentScript.src, ...)`: type.js,
     * UndoStack.js, EditOptions.js, EditDesktop.js, EditMobile.js, EditTypeBoolean/Default/Enum/Integer/Number/String/
     * Collection/Object/DateTime.js. Called once while this script is evaluated (so `currentScript` is Edit.js).
     * @returns {Promise<void>} Resolves when all scripts are loaded (the caller does not await it).
     */
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

    /**
     * Whether a type (or member) description is primitive.
     * @param {{Flags:number}} type - A `TypeDesc` or `TypeMemberDesc`.
     * @returns {boolean} True when `TypeMemberFlags.IsPrimitive` is set in `type.Flags`.
     */
    static IsPrimitive(type) {
        return (type.Flags & TypeMemberFlags.IsPrimitive) !== 0;
    }

    /**
     * Creates the default value of a type.
     * Primitive: the first member's handler `CreateDefault(member0, options)`.
     * Object: the server default instance from `TypeInfoCache.GetInstance(type.TypeName)` (errors ignored), else a
     * new object with every member set to its handler's `CreateDefault`. With `setTypeName` a `$type` property is
     * added (prepended to the server instance when missing).
     * @param {object} type - The `TypeDesc`.
     * @param {EditOptions} [options] - Options passed to the handlers (default `new EditOptions()`).
     * @param {boolean} [setTypeName] - Add `$type: type.TypeName`.
     * @returns {Promise<*>} The default value.
     */
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

    /**
     * Changes the displayed title of this editor (used when collection items are renumbered).
     * Replaces the text of the first `SysWeaver-TypeTitle` and, for a primitive editor without title row, of the first
     * `SysWeaver-MemberTitle`, when different; with `options.MenuIcon` also refreshes the parent cell's copy-on-click
     * tooltip (`BaseTitle` + `ValueFormat.copyOnClick`).
     * @param {string} name - New title, e.g. `"[3]"`.
     * @returns {void}
     */
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

    /**
     * Resolves a dotted undo path relative to this editor (the first segment is this editor's own name).
     * One segment: returns `this`, or for a primitive editor its single member item. Otherwise looks up `MemberMap`
     * by the second segment (stripped of any `[key]`); returns that item when the path ends there without a key,
     * else recurses with `item.FindItem(path without the first segment)`.
     * @param {string} name - Dotted path such as `"Edit.Items[2].Name"`.
     * @returns {Promise<object|Edit|null>} The item / editor, or null when the member is unknown.
     */
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


    /**
     * Path of this editor used in undo paths: the parent's `GetObjectName()` + `"."` + `ObjName` + `KeyName`.
     * @returns {string} E.g. `"Edit.Items[2]"`.
     */
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

    /**
     * Performs and records an undoable change on the shared `UndoStack`. The target is addressed by path, not by
     * reference, so it survives re-rendering: path = `GetObjectName()` (+ `"." + name` for non-primitive editors).
     * The callbacks are wrapped so that on do/redo/undo the target is looked up with `root.FindItem(path)`, passed to
     * `doChange(item)` / `doReverse(item)` and then refreshed with `item.UpdateValue(isFirst)`.
     * Callers sometimes pass extra arguments (e.g. a focus element); they are ignored.
     * @param {function(object):Promise<void>} doChange - Applies the change to the found item.
     * @param {function(object):Promise<void>} doReverse - Reverts the change on the found item.
     * @param {string} title - Description of the change.
     * @param {string} [name] - Member name appended to the path.
     * @param {*} [tracking] - Coalescing token (see `UndoStack.Invoke`).
     * @returns {Promise<void>}
     */
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

        /**
         * Do/redo closure registered on the undo stack: finds the target by path, applies `doChange` and refreshes it.
         * @param {boolean} [isFirst] - True on the first execution, false on redo.
         * @returns {Promise<void>}
         */
        const onRedo = async isFirst => {
            const item = await root.FindItem(n);
            await doChange(item);
            if (item)
                await item.UpdateValue(isFirst);
        }; 

        /**
         * Undo closure registered on the undo stack: finds the target by path, applies `doReverse` and refreshes it.
         * @returns {Promise<void>}
         */
        const onUndo = async () => {
            const item = await root.FindItem(n);
            await doReverse(item);
            if (item)
                await item.UpdateValue();
        };


        await this.UndoStack.Invoke(onRedo, onUndo, title, n, tracking);
    }

    /**
     * Re-renders the editor from its current value (`SetObject(GetObject())`); used when an `Edit` itself is the
     * target of an undo path. The `SetObject` promise is not awaited.
     * @returns {Promise<void>}
     */
    async UpdateValue()
    {
        this.SetObject(this.GetObject());
    }

    /**
     * Creates the editor element and title row (the value is set later with `SetObject`).
     * - Undo stack: `options.UndoStack`, else the parent's, else a new `UndoStack`.
     * - `ObjName`: `options.Name`, else the last dot segment of `type.TypeName`, else `"Edit"`.
     * - Style: when falsy, `"Mobile"` if the window is portrait and (width <= 800 (400 on mobile portrait) or
     *   `window.Mobile`), else `"Desktop"`. On `window.Mobile` the element gets a CSS `zoom` scaling it to fit.
     * - Creates `SysWeaver-Edit<style>` (class `NoBorder` when `!options.Border`) containing a `<table>`, and the
     *   layout strategy via `eval("new Edit" + style + "()")`.
     * - Resolves `TypeHandler` (with `member`, or the first member for collection/primitive types); when the handler
     *   has `IgnoreMembers` the type is converted to a one-member primitive.
     * - Sets `Element.Editor = this`; with `options.Title` adds the title row through the layout's `AddTypeInfo`
     *   (tooltip: type name/assembly in Dev mode, summary, remarks).
     * - A child editor re-dispatches each of its `EditChange` events on the parent element via
     *   `Edit.CreateChangeEvent(member, GetObject(), ev, parent.Key, parent.KeyName)`.
     * - Keyboard: Ctrl+Z / Ctrl+Y on the element call `Undo` / `Redo`; records `Edit.LastEdit` and installs the global
     *   listener (`Edit.AddGlobalListener`).
     * Instance fields set here: `Element`, `Table`, `Edit` (layout), `Type`, `Member`, `Parent`, `Options`, `Style`,
     * `ObjName`, `UndoStack`, `TypeHandler`, `AddMenuItems` (null; optional object-menu hook set by collections),
     * `Key` (null; collection key set by EditTypeCollection, which also sets `KeyName` and `ArrayEntry`) and
     * `RemoveListerners` (Hide-expression listeners). `Object` is set by `SetObject`.
     * @param {object} type - The `TypeDesc` to edit.
     * @param {?string} [style] - `"Desktop"`, `"Mobile"` or falsy for automatic.
     * @param {EditOptions} [options] - Options (default `new EditOptions()`).
     * @param {?Edit} [parentEditor] - Enclosing editor for child editors.
     * @param {?object} [member] - Member description used for the handler lookup (child editors).
     * @param {function(CustomEvent):void} [evListener] - Optional `EditChange` listener added to the element.
     */
    constructor(type, style, options, parentEditor, member, evListener) {

        if (!options)
            options = new EditOptions();
        //  Style
        const isMob = window.Mobile;
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

    /** @type {?Edit} Editor most recently created or that last received a keydown; target of the global Ctrl+Z / Ctrl+Y handler. */
    static LastEdit = null;

    /**
     * Installs (once, guarded by `HaveGlobalListener`) a `keydown` listener on `document.body` that sends Ctrl+Z /
     * Ctrl+Y (without Shift/Alt/Meta) to `Edit.LastEdit.UndoStack.Undo()` / `Redo()` and prevents the default.
     * @returns {void}
     */
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

    /** @type {boolean} True once `AddGlobalListener` has installed the body keydown listener. */
    static HaveGlobalListener = false;


    /**
     * Creates an `EditChange` event (not bubbling). A truthy value of a password member (`member.Flags & 2`) is
     * replaced by `"***"`.
     * @param {?object} member - The changed `TypeMemberDesc` (null for whole-object changes).
     * @param {*} value - The new value.
     * @param {?CustomEvent} [next] - The child event this one wraps (re-dispatch chain), or null.
     * @param {*} [key] - Collection key of the editor, or null.
     * @param {?string} [keyName] - Display form of the key (`"[i]"`), or null.
     * @returns {CustomEvent} Event with `detail { Next, Member, Value, Key, KeyName }`.
     */
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

    /**
     * Compares two values: different `typeof`, `!==` for non-objects, JSON text comparison for objects (and null).
     * @param {*} a - First value.
     * @param {*} b - Second value.
     * @returns {boolean} True when different.
     */
    static IsDifferent(a, b) {
        const aa = typeof (a);
        const bb = typeof (b);
        if (aa !== bb)
            return true;
        if (aa !== "object")
            return a !== b;
        return JSON.stringify(a) !== JSON.stringify(b);
    }

    /**
     * Current value: `Object`, or for primitive types the unwrapped member value.
     * @returns {*} The edited value.
     */
    GetObject() {
        const t = this.Type;
        const obj = this.Object;
        if (!Edit.IsPrimitive(t))
            return obj;
        return obj[t.Members[0].Name];
    }

    /**
     * Copy of a value containing only the declared members (not used within this folder).
     * @param {*} src - Source value.
     * @returns {*} `src` itself for primitive types, else a JSON deep copy with only the `Type.Members` names.
     */
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

    /**
     * Removes (in place) own properties of `obj` that are not declared in `type.Members` (`$type` is kept), recursing
     * into non-primitive members: arrays element-wise with the element type, objects with the member type (types
     * loaded with `Edit.GetType(..., mi.ElementInst)`).
     * @param {object} type - The `TypeDesc`.
     * @param {*} obj - The value to clean.
     * @returns {Promise<*>} `obj` for primitive types, otherwise undefined.
     */
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



    /**
     * Sets the edited value and rebuilds all member rows.
     * `undefined` (or a falsy value for non-primitive types) is replaced by `Edit.CreateDefault(type, options)`;
     * primitive values are wrapped as `{ [member0.Name]: value }` and stored in `Object`. Deletes all table rows except
     * the title row, removes previous `Hide:` listeners, then for each member without `TypeMemberFlags.Hide` builds the
     * tooltip (Dev: member name, type name, summary, remarks; else the summary) and calls the layout's `AddMember`.
     * For each returned item it:
     * - evaluates a `member.Type` of `"Hide:<expr>"` with `new Function("return " + expr).call(obj)` now and on every
     *   `EditChange` (inner `ls`), toggling `item.Hide()` / `item.Show()` and class `Hide` on `item.DataRow`;
     * - stores the item in `MemberMap` and adds class `EditButton` to `item.Icon`;
     * - wires the member menu (inner `popupMenu`) to the title cell's click / context menu and, with
     *   `options.MenuIcon`, to a `ColorIcon(options.MenuIconClass, options.MenuColorClass)` in the icon cell.
     * @param {*} [obj] - The new value.
     * @returns {Promise<void>}
     */
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
                    /**
                     * `EditChange` listener re-evaluating the `Hide:` expression against `obj` and hiding / showing the
                     * member (and its data row). Removed on the next `SetObject`.
                     * @returns {void}
                     */
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

                /**
                 * Opens the member menu (`PopUpElementMenu` on the title cell, class `DefaultMenu`, `WebMenu` named
                 * `EditObject`, positioned with `TrackElement`). Ignores bad clicks; reads the clipboard first.
                 * Items: "Set default" (`item.DefValue`; disabled when undefined, equal to the value or read only),
                 * "Copy" (disabled for Password members), "Paste" (clipboard JSON-parsed for non-string types, accepted
                 * on matching `$type`, null with `AcceptNull`, or the handler's `IsOfType`; then `Condition`ed),
                 * `item.AddMenuItems`, the editor's `AddMenuItems` (primitive editors), "Copy name" and, in Dev mode,
                 * "Copy member name" / "Copy type name". Copy items require `navigator.clipboard`.
                 * Changes go through `editor.Invoke(...SetValue...)`.
                 * For `System.String` members the paste check replaces `clipText` with the conditioned text (used in
                 * the undo log text).
                 * @param {MouseEvent|HTMLElement} ev - The click / context-menu event.
                 * @returns {Promise<void>}
                 */
                const popupMenu = async ev => {
                    if (badClick(ev))
                        return;
                    window.focus();
                    let clipText = await ValueFormat.readFromClipboard();
                    const clip = navigator.clipboard;
                    const val = clip ? editor.GetCopyMember(member) : null;

                    PopUpElementMenu(item.TitleElement, async (popupMenuBackElement, close) => {

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
                        /**
                         * Dispatches `EditChange` for the member's current value on the editor element.
                         * Declared but never called.
                         * @returns {void}
                         */
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

                        if (clip) {
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
                            if (options.ReadOnly || ((flags & TypeMemberFlags.ReadOnly) != 0)) {
                                title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
                            } else {
                                try {
                                    if (val || (val === "")) {
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
                                        const okType = (wasJson && (obj !== null) && ((obj["$type"] ?? "").split(',')[0] === member.TypeName)) ||
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

    /** @type {Map<string, object>} Member name -> item returned by the layout/handler; filled by `SetObject` (entries are overwritten, not cleared). */
    MemberMap = new Map();



 


    /** @type {Map<string, ?object>} Type name -> `TypeDesc` (client-registered via `AddType` or fetched by `GetType`; null cached for unknown server types). */
    static Types = new Map();

    /**
     * Registers a client-side `TypeDesc` in `Edit.Types` unless its `TypeName` is already present.
     * @param {object} type - The `TypeDesc`.
     * @returns {void}
     */
    static AddType(type)
    {
        const tn = type.TypeName;
        const m = Edit.Types;
        if (m.has(tn))
            return;
        m.set(tn, type);
    }

    /**
     * JSON deep clone of a value with optional properties assigned on top (also used to clone default values).
     * @param {*} src - Value to clone.
     * @param {?object} [props] - Properties to `Object.assign` onto the clone.
     * @returns {*} The clone, or null for a falsy `src`.
     */
    static CloneType(src, props) {
        if (!src)
            return null;
        src = JSON.parse(JSON.stringify(src));
        if (props)
            Object.assign(src, props);
        return src;
    }


    /**
     * Gets a type description: a clone (with `props` applied) of the cached entry in `Edit.Types`; when missing and
     * not `isJsType`, fetches it with `TypeInfoCache.GetType(typeName)` (server request through type.js) and caches
     * it (null for unknown types, which are then not fetched again).
     * @param {string} typeName - .NET type name.
     * @param {?object} [props] - Properties assigned on the clone (e.g. `member.ElementInst`).
     * @param {boolean} [isJsType] - Only look in the client registry.
     * @returns {Promise<?object>} The `TypeDesc` clone, or null.
     */
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

    /**
     * Creates an editor for a server type: `Edit.GetType` + `new Edit(t, style, options)` + `SetObject(obj)`.
     * @param {string} serverTypeName - .NET type name.
     * @param {?string} [style] - Layout style (falsy = automatic).
     * @param {EditOptions} [options] - Editor options.
     * @param {?object} [props] - Properties assigned on the type description.
     * @param {*} [obj] - Initial value (undefined = default).
     * @returns {Promise<?Edit>} The editor, or null when the type is unknown.
     */
    static async FromServer(serverTypeName, style, options, props, obj) {

        const t = await Edit.GetType(serverTypeName, props);
        if (!t)
            return null;
        const e = new Edit(t, style, options);
        await e.SetObject(obj);
        return e;
    }

    /**
     * Whether a value is defined.
     * @param {*} value - Value to test.
     * @returns {boolean} `typeof value !== "undefined"`.
     */
    static IsValid(value) {
        return typeof value !== "undefined";
    }

    /**
     * Builds the editor of one member (called by the layouts' `AddMember`): resolves the handler with
     * `GetTypeHandler(member.TypeName, member, options)`, calls `handler.AddEditor(...)` and, when an item is returned,
     * sets `item.TypeHandler` / `item.TitleElement = c` and makes the title cell draggable (class `EditDraggable`,
     * `application/json` from `editor.GetCopyMember(member, true)`). Unless read only (options or flag 16) the cell is
     * also a drop target: JSON with matching `$type` (or null with `AcceptNull`) or accepted by `IsOfType`, or
     * `text/plain` accepted by `IsOfType`, is `Condition`ed and, when different (`!=`), applied undoably with
     * `editor.Invoke(item.SetValue ...)`. Mismatches / parse errors are logged with `console.warn`.
     * @param {object} obj - The object being edited.
     * @param {Edit} editor - The owning editor.
     * @param {object} editContext - Layout context `{ PrevRow, Element, CreateData() }`.
     * @param {object} member - The `TypeMemberDesc`.
     * @param {string} title - Member tooltip.
     * @param {EditOptions} options - Editor options.
     * @param {HTMLTableCellElement} c - The member title cell.
     * @returns {Promise<?object>} The handler's item (see the contract on class `Edit`).
     */
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
            if (!(options.ReadOnly || ((member.Flags & 16) !== 0))) {
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
                            const okType = ((obj !== null) && ((obj["$type"] ?? "").split(',')[0] === member.TypeName)) ||
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

    /**
     * Interprets a value as boolean text.
     * @param {*} val - Value (converted to string).
     * @returns {boolean} True for `"1"`, `"true"` or `"True"`.
     */
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


    /**
     * Creates an `<input>` of the given type bound to a value (helper for type handlers).
     * `change` and `input` events run the inner `onChange`; Escape (pure key) restores the original value and
     * commits it. Read only sets `readOnly` and class `EditReadOnly`. Adds `i.UpdateValue()`.
     * @param {string} inputType - The input `type` attribute (e.g. `"number"`, `"date"`).
     * @param {function():*} getValue - Returns the current value.
     * @param {function(*, Event):Promise<void>} setValue - Stores a new value.
     * @param {function(string):*} parseValue - Parses the input text.
     * @param {function(*):string} toString - Formats a value for the input.
     * @param {?function(*):*} validateValue - Optional coercion / clamping of the parsed value.
     * @param {boolean} readOnly - Make the input read only.
     * @returns {HTMLInputElement} The input (with `UpdateValue`).
     */
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
        /**
         * Input -> value handler (guarded by `isInternal` against re-entrance): parses the text, applies
         * `validateValue`, on a final change (`isTemp` falsy) rewrites the input with the formatted value, then
         * awaits `setValue(newValue, ev)`.
         * @param {Event} ev - The `change` / `input` / keyup event.
         * @param {boolean} [isTemp] - True for `input` events (typing in progress; text not reformatted).
         * @returns {Promise<void>}
         */
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
        /**
         * Reloads the input text from `getValue()` (skipped while a change is being processed).
         * @returns {void}
         */
        i.UpdateValue = () => {
            if (isInternal)
                return;
            isInternal = true;
            i.value = toString(getValue());
            isInternal = false;
        };
        return i;
    }

    /**
     * Creates a text `<input>` (or a `<textarea>` when `numberOfLines > 1`) bound to a value; like
     * `CreateGenericInput` plus `maxlength`, a placeholder (first line of `placeHolder`, not when read only) and key
     * filtering: `validateKeyOrPattern` (a string of allowed characters, or `fn(ch, pos, value)`) filters the data of
     * `beforeinput` events; when some characters are dropped the filtered text is inserted manually (respecting
     * `maxLength`) and a `change` event is dispatched. Adds `i.UpdateValue()`.
     * @param {function():*} getValue - Returns the current value.
     * @param {function(*, Event):Promise<void>} setValue - Stores a new value.
     * @param {function(string):*} parseValue - Parses the text.
     * @param {function(*):string} toString - Formats a value.
     * @param {?string} placeHolder - Placeholder text.
     * @param {?function(*):*} validateValue - Optional coercion of the parsed value.
     * @param {?(string|function(string, number, string):boolean)} validateKeyOrPattern - Allowed characters filter.
     * @param {?number} maxLength - Maximum text length.
     * @param {*} def - Unused.
     * @param {boolean} readOnly - Make the input read only (class `EditReadOnly`).
     * @param {?number} numberOfLines - More than 1 creates a `<textarea>`.
     * @returns {HTMLInputElement|HTMLTextAreaElement} The element (with `UpdateValue`).
     */
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
        /**
         * Input -> value handler (same as in `CreateGenericInput`): parses, validates, reformats on final change and
         * awaits `setValue(newValue, ev)`; guarded by `isInternal`.
         * @param {Event} ev - The `change` / `input` / keyup event.
         * @param {boolean} [isTemp] - True for `input` events (text not reformatted).
         * @returns {Promise<void>}
         */
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
        /**
         * Reloads the text from `getValue()` (skipped while a change is being processed).
         * @returns {void}
         */
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

    /**
     * Creates an `<input type="range">` with range 0..`stepCount` bound to a step index; disabled when read only.
     * Adds `i.UpdateValue()`.
     * @param {function():number} getStep - Returns the current step.
     * @param {function(string):Promise<void>} setStep - Stores a new step (receives `i.value`, a string).
     * @param {number} stepCount - Maximum step.
     * @param {boolean} readOnly - Disable the slider.
     * @returns {HTMLInputElement} The slider (with `UpdateValue`).
     */
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
            /**
             * Slider `change` / `input` handler: awaits `setStep(i.value)`, guarded by `isInternal`.
             * @param {Event} ev - The event.
             * @returns {Promise<void>}
             */
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
        /**
         * Reloads the slider position from `getStep()` (suppressing the change handler meanwhile).
         * @returns {void}
         */
        i.UpdateValue = () => {
            isInternal = true;
            i.value = getStep();
            isInternal = false;
        };
        return i;
    }

    /**
     * Creates a `<select>` bound to a value (used by enum-like handlers). Each option stores its `[value, text, title]`
     * entry as `KeyValue` (title is `kv[2]`, which yields "undefined" for 2-element entries).
     * With `flags` it is a `multiple` select (size <= 4) for bit flags: changed options are OR-ed into / masked out
     * of the current value and all selections are then recomputed; otherwise single selection storing
     * `option.value` (a string). `onOpenFn(options)` runs on `focusin`. Disabled + `EditReadOnly` when read only.
     * Adds `i.UpdateValue()`.
     * @param {function():*} getValue - Returns the current value.
     * @param {function(*):Promise<void>} setValue - Stores a new value.
     * @param {Array<Array>} values - `[value, text, title]` entries.
     * @param {*} def - Unused.
     * @param {boolean} readOnly - Disable the select.
     * @param {boolean} flags - Bit-flag (multi select) mode.
     * @param {?function(HTMLOptionElement[]):void} onOpenFn - Called with the option elements on focus.
     * @returns {HTMLSelectElement} The select (with `UpdateValue`).
     */
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
        /**
         * Reloads the option selection from `getValue()`.
         * @returns {void}
         */
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

    /**
     * Validates a value against a type: for each member without `TypeMemberFlags.Hide`, an `undefined` value yields
     * "<DisplayName>: No value found!", otherwise the member handler's `Validate(value, member (or the type for
     * primitives), options, path)` runs; the first error is returned.
     * @param {object} type - The `TypeDesc`.
     * @param {*} obj - The value (the raw value for primitive types).
     * @param {EditOptions} options - Editor options.
     * @param {?string} name - Path prefix; member paths are `name + "." + member` (or `name` for primitives, or just
     *   the member name when `name` is falsy).
     * @returns {Promise<?Array<string>>} Null when valid, else `[path, message]`.
     */
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

    /**
     * Validates the edited value with `Edit.ValidateType(Type, Object, Options, primitive ? null : "Edit")`.
     * With `focusOnError` resolves the error path with `FindItem` and focuses the item's `Focus` element.
     * @param {boolean} [focusOnError] - Focus the failing member.
     * @returns {Promise<?Array<string>>} Null when valid, else `[path, message]`.
     */
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

    /**
     * Parses an integer metadata parameter (e.g. `Min` / `Max`).
     * @param {*} val - The value; when defined `parseInt(val)` is returned (may be NaN; parseInt does not throw).
     * @param {number} def - Returned when `val` is undefined.
     * @returns {number} The parsed value or `def`.
     */
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

    /**
     * Text used for copy / drag of a value: `"null"` for null, the string itself (unless `forceJson`), JSON for
     * non-objects and arrays, and for objects JSON with `$type: typeName` prepended when missing.
     * @param {*} obj - The value.
     * @param {boolean} [forceJson] - Always produce JSON (drag and drop).
     * @param {string} typeName - .NET type name for `$type`.
     * @returns {string} The text.
     */
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

    /**
     * Clipboard / drag text of the whole edited value (`Edit.GetTypedTextOrJson` with `Type.TypeName`).
     * @param {boolean} [forceJson] - Always produce JSON.
     * @returns {string} The text.
     */
    GetCopyObject(forceJson) {
        const t = this.Type;
        const tn = t.TypeName;
        return Edit.GetTypedTextOrJson(this.GetObject(), forceJson, tn);
    }

    /**
     * Clipboard / drag text of one member's value (`obj[member.Name]`, or the primitive value itself).
     * @param {object} member - The `TypeMemberDesc`.
     * @param {boolean} [forceJson] - Always produce JSON.
     * @returns {string} The text.
     */
    GetCopyMember(member, forceJson) {
        let obj = this.GetObject();
        if (!Edit.IsPrimitive(this.Type))
            obj = obj[member.Name];
        return Edit.GetTypedTextOrJson(obj, forceJson, member.TypeName);
    }

    /**
     * Whether a pasted / dropped value can replace the whole edited value. Primitive editor: `obj` must not be an
     * object and must pass the type handler's `IsOfType`. Object editor: `obj.$type` (before the first comma) must
     * equal `Type.TypeName` (a null `obj` throws a TypeError here).
     * @param {*} obj - Candidate value.
     * @returns {boolean} True if acceptable.
     */
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
        if ((obj["$type"] ?? "").split(',')[0] != type.TypeName)
            return false;
        //delete obj["$type"];
        return true;
    }

    /**
     * Dispatches an `EditChange` (no member, value `GetObject()`, the editor's `Key` / `KeyName`) on `Element`.
     * @returns {void}
     */
    NotifyChange() {
        this.Element.dispatchEvent(Edit.CreateChangeEvent(null, this.GetObject(), null, this.Key, this.KeyName));

    }

    /**
     * Fills the title cell `c` (called by the layout's `AddTypeInfo`):
     * - optional expand/collapse `ColorIcon` (when `options.CanExpand` and not primitive; inner `setExpand` /
     *   `toggleExpand` toggle class `EditorCollapsed` on `expandElement`, initial state `options.IsExpanded`);
     * - a `SysWeaver-PopupMenuIcon` with the object menu (inner `menuPop`), also opened by `c.oncontextmenu`;
     * - `options.AddToHeader(c)` hook;
     * - a `SysWeaver-TypeTitle` (`options.KeyName` or `type.DisplayName`; `BaseTitle` = tooltip) and a copy-on-click
     *   tooltip (`ValueFormat.copyOnClick`).
     * Makes `c` draggable (class `EditDraggable`, `application/json` from `GetCopyObject(true)`) and, unless
     * `options.ReadOnly`, a drop target: JSON accepted by `CanConsumeCopy` is `Condition`ed and applied undoably with
     * `SetObject` + `NotifyChange`; `text/plain` accepted by `CanConsumeCopy` is `Condition`ed and applied directly
     * with an `EditChange`.
     * @param {HTMLTableCellElement} c - The title cell.
     * @param {HTMLElement} expandElement - Element that gets class `EditorCollapsed` (the editor table).
     * @param {object} type - The `TypeDesc`.
     * @param {string} title - Tooltip text.
     * @param {EditOptions} options - Editor options.
     * @returns {void}
     */
    SetTypeInfo(c, expandElement, type, title, options) {

        let name = options.KeyName;
        if (!name)
            name = type.DisplayName;
        const edit = this;
        const e = edit.Element;
        const th = edit.TypeHandler;
        /**
         * Opens the object menu (`PopUpElementMenu` at the menu icon, `WebMenu` named `EditProp`): "Set default"
         * (`SetObject()` + `NotifyChange`, disabled when read only), and when `navigator.clipboard` exists "Copy"
         * (`GetCopyObject()`) and "Paste" (clipboard JSON-parsed unless the type is `System.String`, accepted on matching
         * `$type`, null with `AcceptNull`, or `IsOfType`; then `Condition`ed), followed by the editor's `AddMenuItems`.
         * Changes are undoable via `Invoke`. Uses `menuIcon` / `isReadOnly`, declared later in `SetTypeInfo`.
         * @returns {Promise<void>}
         */
        const menuPop = async () => {
            window.focus();
            const clip = navigator.clipboard;
            const val = edit.GetCopyObject();
            const clipText = await ValueFormat.readFromClipboard();

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
                if (clip) {
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
                            if (clipText) {
                                const typeName = edit.Type.TypeName;
                                const wasJson = typeName !== "System.String";
                                let obj = clipText;
                                if (wasJson)
                                    obj = JSON.parse(obj);
                                const okType = (wasJson && (obj !== null) && ((obj["$type"] ?? "").split(',')[0] === typeName)) ||
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
        };
        c.oncontextmenu = async ev => {
            if (badClick(ev, true)) 
                return false;
            ev.preventDefault();
            ev.stopPropagation();
            await menuPop();
            return true;
        };

        const isReadOnly = options.ReadOnly || (Edit.IsPrimitive(type) && ((type.Members[0].Flags & 16) !== 0));
        if (options.CanExpand && (!Edit.IsPrimitive(type))) {
            let isExpanded = options.IsExpanded;

            let expandIcon;

            /**
             * Applies `isExpanded`: removes / adds class `EditorCollapsed` on `expandElement` and switches the icon
             * (`SysWeaverEditIconCollapse` / `SysWeaverEditIconExpand`) and its tooltip.
             * @returns {void}
             */
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

            /**
             * Click handler of the expand icon: flips `isExpanded` and applies it with `setExpand`.
             * @returns {void}
             */
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

        const menuIcon = new ColorIcon("SysWeaverEditIconMenu", "IconColorThemeAcc2", 24, 24,
            _TF("Click to show options.", "Tool tip description for a button that when pressed will pop-up a menu")
            , async ev => {
                if (badClick(ev))
                    return;
                await menuPop();
            }, null, null, true);
        menuElement.appendChild(menuIcon.Element);
        c.appendChild(menuElement);
        const gops = options.AddToHeader;
        if (gops)
            gops(c);

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
                    let obj = ev.dataTransfer.getData("text/plain");
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

    /**
     * Writes a short summary of a value into `element.innerText` / `element.title` (object member summary).
     * Falsy -> `"null"`. Non-objects: the value itself is shown (not truncated).
     * Objects: the first non-empty of `toString()` (called unbound, so normally `"[object Undefined]"`, which is
     * skipped), `ToString()` (also unbound) and `JSON.stringify`, else `"Value"`; longer than 24 chars it is truncated
     * to 21 chars + `"..."` with the full text as tooltip.
     * @param {HTMLElement} element - Target element (`SysWeaver-EditInfo`).
     * @param {*} obj - The value.
     * @returns {void}
     */
    static SetToString(element, obj) {
        if (!obj) {
            element.innerText = "null";
            element.title = _TF("No object available", "Tool tip description of a property value input box when no object is available");
            return;
        }
        const ot = typeof (obj);
        if (ot === "string") {
            element.innerText = obj;
            element.title = _T("The text is: \"{0}\"", obj, "Tool tip description of a property text value.{0} is replaced with the value text.");
            return;
        }
        if (ot === "number") {
            element.innerText = "" + obj;
            element.title = _T("The number is: {0}", obj, "Tool tip description of a property numerical value.{0} is replaced with the numerical value.");
            return;
        }
        if (ot !== "object") {
            element.innerText = "" + obj;
            element.title = _T("The value is: {0}", obj, "Tool tip description of a property value.{0} is replaced with the value.");
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

    /**
     * Writes the item count of a collection into `element.innerText` (+ tooltip): `"null"`, "empty", "1 item" or
     * "{n} items" (localized).
     * @param {HTMLElement} element - Target element (`SysWeaver-EditInfo`).
     * @param {?(Array|Map)} collection - The collection.
     * @param {EditCollectionArray|EditCollectionMap} container - Strategy used for `countVal`.
     * @returns {void}
     */
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

    /**
     * Builds a new map of the built-in handlers by .NET type name: signed / unsigned integers -> `EditTypeInteger`
     * (64-bit limited to the JS safe integer range), `Single` / `Double` / `Decimal` -> `EditTypeNumber`, `Boolean` ->
     * `EditTypeBoolean`, `DateTime` / `DateOnly` / `TimeOnly` -> `EditTypeDateTime`, the same for the
     * ``System.Nullable`1[[...]]`` names (nullable variants), `String` -> `EditTypeString`, `Object` -> read-only
     * `EditTypeString`.
     * @returns {Map<string, object>} Type name -> handler.
     */
    static GetTypeMap() {
        const map = new Map();
        map.set("System.SByte", new EditTypeInteger(true, 1));
        map.set("System.Int16", new EditTypeInteger(true, 2));
        map.set("System.Int32", new EditTypeInteger(true, 4));
        map.set("System.Int64", new EditTypeInteger(true, 8, false, Number.MIN_SAFE_INTEGER, Number.MAX_SAFE_INTEGER));

        map.set("System.Byte", new EditTypeInteger(false, 1));
        map.set("System.UInt16", new EditTypeInteger(false, 2));
        map.set("System.UInt32", new EditTypeInteger(false, 4));
        map.set("System.UInt64", new EditTypeInteger(false, 8, false, 0, Number.MAX_SAFE_INTEGER));

        map.set("System.Single", new EditTypeNumber(-3.40282347e+38, 3.40282347e+38, 11));
        map.set("System.Double", new EditTypeNumber(-1.7976931348623157e+308, 1.7976931348623157e+308, 19));
        map.set("System.Decimal", new EditTypeNumber(-1.7976931348623157e+308, 1.7976931348623157e+308, 19));

        map.set("System.Boolean", new EditTypeBoolean());
        map.set("System.DateTime", new EditTypeDateTime(false));
        map.set("System.DateOnly", new EditTypeDateTime(false, true));
        map.set("System.TimeOnly", new EditTypeDateTime(false, false, true));
        
        /**
         * Builds the .NET nullable type name of a value type.
         * @param {string} n - Type name, e.g. `"System.Int32"`.
         * @returns {string} ``"System.Nullable`1[[" + n + "]]"``.
         */
        function N(n) {
            return "System.Nullable`1[[" + n + "]]";
        }

        map.set(N("System.SByte"), new EditTypeInteger(true, 1, true));
        map.set(N("System.Int16"), new EditTypeInteger(true, 2, true));
        map.set(N("System.Int32"), new EditTypeInteger(true, 4, true));
        map.set(N("System.Int64"), new EditTypeInteger(true, 8, true, Number.MIN_SAFE_INTEGER, Number.MAX_SAFE_INTEGER));

        map.set(N("System.Byte"), new EditTypeInteger(false, 1, true));
        map.set(N("System.UInt16"), new EditTypeInteger(false, 2, true));
        map.set(N("System.UInt32"), new EditTypeInteger(false, 4, true));
        map.set(N("System.UInt64"), new EditTypeInteger(false, 8, true, 0, Number.MAX_SAFE_INTEGER));

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

    /** @type {?EditTypeCollection} Shared collection handler, created on first use by `GetTypeHandler`. */
    static CollectionTypeHandler = null;
    /** @type {?EditTypeDefault} Shared fallback handler, created on first use by `GetTypeHandler`. */
    static DefaultTypeHandler = null;
    /**
     * Handler cache for lookups without a member (built by `GetTypeMap` on first use). `Edit.TypeMapMember` (lookups
     * with a member), `Edit.ObjectTypeHandler` and `Edit.EnumTypeHandler` are created on demand as undeclared static
     * properties the same way.
     * @type {?Map<string, object>}
     */
    static TypeMap = null;

    /**
     * Returns the shared `EditTypeObject` handler, creating it on first use (`Edit.ObjectTypeHandler`).
     * @returns {EditTypeObject} The handler.
     */
    static GetObjectTypeHandler()
    {
        let c = Edit.ObjectTypeHandler;
        if (!c) {
            c = new EditTypeObject();
            Edit.ObjectTypeHandler = c;
        }
        return c;
    }

    /**
     * Resolves (and caches) the type handler for a type / member. Uses `Edit.TypeMapMember` when a member is given,
     * else `Edit.TypeMap` (each initialized with `GetTypeMap()`). Cache key: `typename`, or `typename + "|" +
     * member.Type` when the member has a special type specification.
     * Unknown key, in order: a global `window["EditType" + spec.split(':')[0] + "Factory"](typename, specParts)` when
     * defined (e.g. EditTypeLanguage.js); the handler of the plain `typename`; by member flags `Collection` ->
     * `EditTypeCollection`, `IsObject` -> `EditTypeObject`, `IsEnum` -> `EditTypeEnum` (shared instances); otherwise
     * the shared `EditTypeDefault`. The result is cached under the key.
     * @param {string} typename - .NET type name.
     * @param {?object} [member] - The `TypeMemberDesc` (flags and `Type` spec drive the lookup).
     * @param {EditOptions} [options] - Unused.
     * @returns {object} The handler (see the contract on class `Edit`).
     */
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

//  Start loading the dependent editor scripts (not awaited).
Edit.Init();

/**
 * Entry point of the "Edit test" page (edit/index.html): creates an editor with
 * `Edit.FromServer("SysWeaver.MicroService.EditTest")`, appends it to `document.body` and logs every `EditChange`
 * to the console as `path = value` (path built from `detail.Member.Name` and the `KeyName` / member chain following
 * `detail.Next`, `*` for whole-object changes; object values JSON-stringified).
 * @returns {Promise<void>}
 */
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