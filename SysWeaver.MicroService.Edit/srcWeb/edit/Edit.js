/**
 * @file The object editor engine (`Sw.Edit`, legacy global `Edit`): renders an editable form for any type described by
 * the TypeService (`edit/GetTypeInfo`).
 *
 * Loading: a page includes this file only (`<script defer src="../edit/Edit.js">`, after `sw/core.js` + `sw/ui.js`, or
 * after the legacy `common/common.js`, which contains both). While it is evaluated it starts loading the rest of the
 * editor (type.js, UndoStack.js, EditDesktop.js, EditMobile.js, the EditType*.js handlers and the
 * EditCollection*.js strategies) and its style sheets (Edit.css, EditDesktop.css, EditMobile.css) relative to this
 * script. The dynamically inserted scripts delay the window load event, so `onload` code can use everything;
 * `await Sw.Edit.Ready` (or `Sw.Edit.Init()`) waits explicitly.
 *
 * Architecture:
 * - Handler registry: .NET type name -> type handler (`Sw.Edit.GetTypeHandler`, `Sw.Edit.Register`); unknown types fall
 *   back by member flags (collection, object, enum, default). A member `Type` spec `X[:args]` resolves to a plug-in factory
 *   registered with `Sw.Edit.RegisterSpec(X, factory)` or a global `EditTypeXFactory(typeName, specParts)` (old hook).
 * - Type-handler contract (duck-typed, see README): `CreateDefault`, `GetDefault`, `IsOfType`, `Condition`, `Validate`,
 *   `ToString`, `AddEditor(obj, editor, editContext, member, title, options)` -> item
 *   `{SetValue, UpdateValue, Focus, Title, DefValue, DefValueText, DataRow?, AddMenuItems?, FindItem?, GetValue?}`.
 *   `Sw.Edit.Handler` is an optional base class with shared helpers; handler classes are in `Sw.Edit.Handlers`.
 * - Undo: every change goes through `Invoke`, which records path based closures on the shared `UndoStack`
 *   (targets are re-found by dotted path, so undo works after sub editors were re-rendered).
 * - Change events: every change dispatches a non bubbling `EditChange` CustomEvent
 *   `{Next, Member, Value, Key, KeyName}` that each parent editor re-dispatches (chained by `Next`).
 * - Layouts: `Sw.Edit.Layouts.Desktop` / `Mobile` build the rows; automatic root editors switch by their width.
 *
 * Globals defined (legacy aliases of the new API): `Edit` (= Sw.Edit), `editTestMain` (edit/index.html).
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const el = Sw.el;

    /** @type {string} Url of this script (base for the other editor files). */
    const ScriptUrl = (document.currentScript && document.currentScript.src) || (Sw.Url.Root + "edit/Edit.js");

    /**
     * Bit flags of a type or member description (mirror of the C# TypeMemberFlags; every flag has its own bit,
     * DateUnspecified moved from 2048, shared with IsFlags, to 4096).
     * @enum {number}
     */
    const Flags = Object.freeze({
        Multiline: 1, Password: 2, AcceptNull: 4, Slider: 8, ReadOnly: 16, Collection: 32, Indexed: 64, IsEnum: 128,
        IsPrimitive: 256, IsObject: 512, Hide: 1024, IsFlags: 2048, DateUnspecified: 4096,
    });

    /**
     * Test if a metadata value is present. The server sends `null` for "not set" (ex: `Max: null` = no limit), so null and
     * undefined both mean "not set".
     * @param {*} v - The value.
     * @returns {boolean} True when neither undefined nor null.
     */
    const Has = v => v !== undefined && v !== null;

    /** Default option values (Sw.Edit.Options copies them; used directly when an editor is created without options). */
    const OptionDefaults = Object.freeze({
        Title: true, Dev: true, ReadOnly: false, CanExpand: true, IsExpanded: true, KeyName: null, Border: true,
        ExpandAllCollections: false, MenuIcon: false, MenuIconClass: "SysWeaverEditIconMenu", MenuColorClass: "IconColorThemeAcc2",
        AddToHeader: null, TypeHints: true, PlainHints: false, Summary: false, MarkChanged: false,
    });

    //  Inline icons used by the editor menus (stroked 24x24, follow the theme).
    const EditIcons = {
        editPaste: '<rect x="6" y="4" width="12" height="17" rx="2"/><path d="M9 4V3h6v1M9 10h6M9 14h6M9 18h3"/>',
        editAddBefore: '<path d="M12 3v8M8 7h8"/><rect x="4" y="14" width="16" height="6" rx="1.5"/>',
        editAddAfter: '<rect x="4" y="4" width="16" height="6" rx="1.5"/><path d="M12 13v8M8 17h8"/>',
        editTop: '<path d="M5 4h14M12 20V8M7 13l5-5 5 5"/>',
        editBottom: '<path d="M5 20h14M12 4v12M7 11l5 5 5-5"/>',
        editClear: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>',
    };
    for (const n in EditIcons)
        if (!Sw.Icons.Has(n))
            Sw.Icons.Add(n, EditIcons[n]);

    /**
     * Join a "Header: value" line onto a tooltip text (the old ValueFormat.AddNonNullLine).
     * @param {?string} text - Existing text.
     * @param {string} header - Line header.
     * @param {*} value - Value; nothing is added when not set or empty.
     * @returns {?string} The text.
     */
    function addLine(text, header, value) {
        if (!Has(value) || value === "")
            return text;
        const line = header + ": " + value;
        return text ? (text + "\n" + line) : line;
    }

    /**
     * Read the clipboard text.
     * @returns {Promise<Array>} `[text, error]`: the text (null when unavailable) and an error message.
     */
    async function readClipboard() {
        try {
            if (!navigator.clipboard || !navigator.clipboard.readText)
                return [null, _TF("The clipboard can't be read on this page", "Error text shown when the browser doesn't allow reading the clipboard")];
            return [await navigator.clipboard.readText(), null];
        }
        catch (e) {
            return [null, e && e.message ? e.message : String(e)];
        }
    }

    /**
     * Normalize a menu item: accepts Sw.Menu items and the old WebMenuItem shape ({Name, IconClass, Title, Data, Flags}).
     * @param {object|string} it - The item.
     * @returns {object|string} A Sw.Menu item.
     */
    function menuItem(it) {
        if (!it || typeof it !== "object" || it.Text !== undefined || it.Items || it.Name === undefined)
            return it;
        const data = it.Data;
        return {
            Text: it.Name, Icon: it.Icon || it.IconClass || null, Title: it.Title || null, Disabled: ((it.Flags || 0) & 1) !== 0,
            OnClick: typeof data === "function" ? () => data() : null,
        };
    }

    /**
     * The object editor: one instance edits one object (or one primitive value) of one type description.
     * Usage: `const e = new Sw.Edit(type, style, options)` (or `await Sw.Edit.FromServer(typeName)` / `Sw.Edit.Boolean(...)`),
     * `await e.SetObject(value)`, append `e.Element`. Listen for `EditChange` on `e.Element`, read `e.GetObject()`, check
     * `await e.Validate(true)`. Child editors (nested objects, collection items) get the parent editor and share its undo stack.
     */
    class Edit {
        /**
         * Create an editor (the members are rendered by SetObject).
         * @param {object|string} type - The TypeDesc to edit, or a type name: a type registered with `Sw.Edit.AddType`, else it is
         * fetched from the server by SetObject (an unknown name shows an error in the editor and logs a console error).
         * @param {?string} [style] - "Desktop", "Mobile", or falsy for automatic (switches with the available width).
         * @param {?object} [options] - Options (EditOptions or a plain object; missing values use the defaults).
         * @param {?Edit} [parentEditor] - Enclosing editor (child editors).
         * @param {?object} [member] - Member description used for the handler lookup (child editors).
         * @param {function(CustomEvent):void} [evListener] - Optional EditChange listener.
         */
        constructor(type, style, options, parentEditor, member, evListener) {
            /** @type {?string} A type name that SetObject still has to resolve (the server knows it, or it is an error). */
            this.PendingTypeName = null;
            if (typeof type === "string") {
                const known = Edit.Types.get(type);
                if (known)
                    type = Edit.CloneType(known);
                else {
                    this.PendingTypeName = type;
                    type = { TypeName: type, DisplayName: type.split(".").pop(), Flags: 0, Members: [] };
                }
            }
            else if (!type || typeof type !== "object") {
                const msg = "new Sw.Edit(type): type must be a TypeDesc object ({ TypeName, Flags, Members: [...] }, see edit/README.md \"TypeDesc\") or a type name, got " + (type === null ? "null" : typeof type) + ".";
                console.error(msg);
                throw new TypeError(msg);
            }
            if (!options)
                options = Sw.Edit.Options ? new Sw.Edit.Options() : Object.assign({}, OptionDefaults);
            for (const k in OptionDefaults)
                if (options[k] === undefined)
                    options[k] = OptionDefaults[k];
            /** @type {object} The type description (primitive handlers with IgnoreMembers get a one member copy). */
            this.Type = type;
            /** @type {?object} The member this (child) editor edits. */
            this.Member = member || null;
            /** @type {?Edit} The parent editor. */
            this.Parent = parentEditor || null;
            /** @type {object} The options. */
            this.Options = options;
            /** @type {?string} Forced style ("Desktop" / "Mobile") or null (automatic). */
            this.ForcedStyle = style === "Desktop" || style === "Mobile" ? style : null;
            /** @type {string} Name of this editor in undo paths (options.Name, else the last part of the type name, else "Edit"). */
            this.ObjName = options.Name || (type.TypeName ? type.TypeName.split(".").pop() : null) || "Edit";
            /** @type {*} Collection key of a collection item editor (set by the collection handler). */
            this.Key = null;
            /** @type {?string} Display form of the key ("[i]"). */
            this.KeyName = null;
            /** @type {?function(Array, function():void):void} Extra object-menu items (set by collections for their items). */
            this.AddMenuItems = null;
            /** @type {Map<string, object>} Member name -> item (filled by SetObject). */
            this.MemberMap = new Map();
            /** @type {*} The edited object (primitives are wrapped as {member: value}). */
            this.Object = undefined;
            /** @type {Array<function>} Hide-expression listeners (removed on the next SetObject). */
            this.RemoveListerners = [];
            /** @type {?UndoStack} The undo stack (see the UndoStack getter). */
            this.UndoStackValue = options.UndoStack || (parentEditor ? parentEditor.UndoStack : null);
            const e = el("div.sw-edit", { attrs: { role: "group", "aria-label": options.KeyName || type.DisplayName || null } });
            e.classList.add(parentEditor ? "sw-edit-child" : "sw-edit-root");
            if (!options.Border)
                e.classList.add("NoBorder", "sw-edit-noborder");
            if (options.ReadOnly)
                e.classList.add("sw-edit-readonly");
            /** @type {HTMLElement} The editor element. */
            this.Element = e;
            e.Editor = this;
            /** @type {HTMLElement} Member rows container. */
            this.Body = el("div.sw-edit-body");
            /** @type {?HTMLElement} The title row (when options.Title). */
            this.Head = null;
            if (evListener)
                e.addEventListener("EditChange", evListener);
            if (parentEditor) {
                const pe = parentEditor;
                e.addEventListener("EditChange", ev => pe.Element.dispatchEvent(Edit.CreateChangeEvent(member, this.GetObject(), ev, pe.Key, pe.KeyName)));
            }
            else
                this.InitStyle();
            e.addEventListener("keydown", ev => this.OnKey(ev));
            e.addEventListener("focusin", () => Edit.LastEdit = this);
            Edit.LastEdit = this;
            Edit.AddGlobalListener();
        }

        /** @type {UndoStack} The undo stack (own, from the options, or the parent's; created on first use). */
        get UndoStack() {
            if (!this.UndoStackValue)
                this.UndoStackValue = new Sw.Edit.UndoStack();
            return this.UndoStackValue;
        }

        set UndoStack(v) {
            this.UndoStackValue = v;
        }

        /** @type {string} The current layout style ("Desktop" or "Mobile"); children report their root's style. */
        get Style() {
            if (this.ForcedStyle)
                return this.ForcedStyle;
            let r = this;
            while (r.Parent)
                r = r.Parent;
            return r.Element.classList.contains("sw-edit-mobile") ? "Mobile" : "Desktop";
        }

        /** @type {object} The layout strategy (Sw.Edit.Layouts[Style]). */
        get Edit() {
            const L = Edit.Layouts;
            return L[this.Style] || L.Desktop;
        }

        /**
         * Set up the style classes of a root editor: a forced style, or automatic switching by the editor width
         * (narrow editors use the mobile layout: titles above values). Both layouts use the same rows, so switching is css only.
         */
        InitStyle() {
            const e = this.Element;
            if (window.ResizeObserver) {
                //  Shown for the first time: adopt a card it was placed in (AdoptCard)
                const ro = new ResizeObserver(() => {
                    if (e.isConnected) {
                        this.AdoptCard();
                        if (this.CardChecked && (!this.Options.Title || this.Head))
                            ro.disconnect();
                    }
                });
                ro.observe(e);
            }
            const set = mobile => {
                e.classList.toggle("sw-edit-mobile", mobile);
                e.classList.toggle("sw-edit-desktop", !mobile);
            };
            if (this.ForcedStyle) {
                set(this.ForcedStyle === "Mobile");
                return;
            }
            const w = window.innerWidth;
            set(w <= Edit.MobileWidth || !!(Sw.Layout && Sw.Layout.Mobile && Sw.Layout.Portrait && w <= 800));
            if (window.ResizeObserver) {
                new ResizeObserver(en => {
                    const cw = en[0].contentRect.width;
                    if (cw > 0)
                        set(cw < Edit.MobileWidth);
                }).observe(e);
            }
        }

        /**
         * A root editor placed directly in a card body (Sw.Card: section.sw-card > div.sw-card-body > the editor) uses the card as its
         * frame (Design v2, no card in a card): class sw-edit-incard drops the editor frame, and when the card has a title the editor's
         * title row (its menu, copy / paste / drag and drop of the whole value) moves into the card's tools without its own title and
         * collapse button. The class "sw-edit-framed" on the editor element keeps the old look. Runs when the editor is first shown
         * (and after its title row is built); does nothing for child editors, dialogs, tabs or other containers.
         */
        AdoptCard() {
            const e = this.Element;
            if (this.Parent || !e.isConnected || this.HeadInCard)
                return;
            const body = e.parentElement;
            if (!this.CardChecked) {
                this.CardChecked = true;
                this.InCard = !!(body && body.classList.contains("sw-card-body") && !e.classList.contains("sw-edit-framed"));
                if (this.InCard)
                    e.classList.add("sw-edit-incard");
            }
            if (!this.InCard || !this.Head || (this.Options.CanExpand && !this.Options.IsExpanded))
                return;
            const card = body.parentElement;
            const title = card && card.querySelector(":scope > .sw-card-head > .sw-card-title");
            const tools = card && card.querySelector(":scope > .sw-card-head > .sw-card-tools");
            if (!title || !tools || !title.textContent.trim())
                return;
            this.HeadInCard = true;
            this.Head.classList.add("sw-edit-head-incard");
            e.classList.add("sw-edit-cardtitled");
            tools.append(this.Head);
        }

        /**
         * Keyboard handling inside the editor: Ctrl+Z undo, Ctrl+Y / Ctrl+Shift+Z redo (Cmd on Apple); the native input
         * undo is suppressed.
         * @param {KeyboardEvent} ev - The event.
         */
        OnKey(ev) {
            Edit.LastEdit = this;
            if (Edit.HandleUndoKey(ev, this.UndoStack))
                ev.stopPropagation();
        }

        /**
         * Run undo / redo for a key event.
         * @param {KeyboardEvent} ev - The event.
         * @param {UndoStack} undoStack - The stack.
         * @returns {boolean} True when handled.
         */
        static HandleUndoKey(ev, undoStack) {
            const mod = ev.ctrlKey || ev.metaKey;
            if (!mod || ev.altKey || !undoStack || !ev.key)
                return false;
            const k = ev.key.toLowerCase();
            if (k === "z" && !ev.shiftKey) {
                ev.preventDefault();
                undoStack.Undo();
                return true;
            }
            if ((k === "y" && !ev.shiftKey) || (k === "z" && ev.shiftKey)) {
                ev.preventDefault();
                undoStack.Redo();
                return true;
            }
            return false;
        }

        /**
         * Install (once) the page wide undo keys: Ctrl+Z / Ctrl+Y outside any editor go to the last used editor
         * (not while typing in another text field of the page).
         */
        static AddGlobalListener() {
            if (Edit.HaveGlobalListener)
                return;
            Edit.HaveGlobalListener = true;
            document.addEventListener("keydown", ev => {
                const e = Edit.LastEdit;
                if (!e || !e.Element.isConnected)
                    return;
                const t = ev.target;
                if (t && t.closest && t.closest(".sw-edit"))
                    return;
                if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.isContentEditable))
                    return;
                Edit.HandleUndoKey(ev, e.UndoStack);
            });
        }

        // ------------------------------------------------------------------ loading

        /**
         * Load the editor files (called once while this script is evaluated; returns the shared promise).
         * @returns {Promise<void>} Resolves when every editor script and style sheet is loaded.
         */
        static Init() {
            if (Edit.Ready)
                return Edit.Ready;
            const js = ["type.js", "UndoStack.js", "EditDesktop.js", "EditMobile.js", "EditCollectionArray.js",
                "EditCollectionMap.js", "EditTypeBoolean.js", "EditTypeDefault.js", "EditTypeEnum.js", "EditTypeInteger.js",
                "EditTypeNumber.js", "EditTypeString.js", "EditTypeCollection.js", "EditTypeObject.js", "EditTypeDateTime.js",
                "EditTypeTimeSpan.js", "EditTypeColor.js", "EditTypeSpecs.js", "EditTypeUnit.js"];
            const css = ["Edit.css", "EditDesktop.css", "EditMobile.css"];
            Edit.Ready = Promise.all(js.map(f => Sw.Net.IncludeJs(ScriptUrl, f)).concat(css.map(f => Sw.Net.IncludeCss(ScriptUrl, f, true))))
                .then(() => undefined);
            return Edit.Ready;
        }

        /**
         * Make sure the plug-in handlers needed by a type are loaded (plug-ins in this folder, ex: Language, load on demand).
         * @param {object} type - The type description.
         * @returns {Promise<void>}
         */
        static async LoadPlugins(type) {
            const ms = type && type.Members;
            if (!ms)
                return;
            for (const m of ms) {
                const spec = m.Type;
                if (!spec)
                    continue;
                const name = spec.split(":")[0];
                if (Edit.Specs.has(name) || typeof window["EditType" + name + "Factory"] === "function")
                    continue;
                if (Edit.BuiltInPlugins.indexOf(name) >= 0) {
                    await Sw.Net.IncludeJs(ScriptUrl, "EditType" + name + ".js", true);
                    if (Edit.PluginReady[name])
                        await Edit.PluginReady[name];
                    Edit.TypeMapMember = null;
                }
            }
        }

        // ------------------------------------------------------------------ type info

        /**
         * Test if a type (or member) description is primitive.
         * @param {{Flags:number}} type - TypeDesc or TypeMemberDesc.
         * @returns {boolean} True when IsPrimitive is set.
         */
        static IsPrimitive(type) {
            return ((type && type.Flags) & Flags.IsPrimitive) !== 0;
        }

        /**
         * Test if a value is defined.
         * @param {*} value - The value.
         * @returns {boolean} True when not undefined.
         */
        static IsValid(value) {
            return typeof value !== "undefined";
        }

        /**
         * Interpret a value as boolean text ("1", "true", "True").
         * @param {*} val - The value.
         * @returns {boolean} The result.
         */
        static IsTrue(val) {
            val = "" + val;
            return val === "1" || val === "true" || val === "True";
        }

        /**
         * Parse an integer metadata parameter.
         * @param {*} val - The value (null / undefined = not set).
         * @param {number} def - Returned when not set or not a number.
         * @returns {number} The value.
         */
        static GetIntParam(val, def) {
            if (!Has(val))
                return def;
            const v = parseInt(val);
            return isNaN(v) ? def : v;
        }

        /**
         * Register a client side type description (takes precedence over the server; first registration wins).
         * @param {object} type - The TypeDesc.
         */
        static AddType(type) {
            if (!Edit.Types.has(type.TypeName))
                Edit.Types.set(type.TypeName, type);
        }

        /**
         * JSON deep clone with optional properties assigned on top.
         * @param {*} src - The value.
         * @param {?object} [props] - Properties to assign.
         * @returns {*} The clone (null for a falsy src).
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
         * Get a type description (a clone): client registered types first, else the server (cached, also unknown types).
         * @param {string} typeName - .NET type name.
         * @param {?object} [props] - Properties assigned on the clone (ex: member.ElementInst).
         * @param {boolean} [isJsType] - Only look in the client registry.
         * @returns {Promise<?object>} The TypeDesc or null.
         */
        static async GetType(typeName, props, isJsType) {
            const m = Edit.Types;
            if (m.has(typeName)) {
                const t = m.get(typeName);
                return t ? Edit.CloneType(t, props) : null;
            }
            if (isJsType || !typeName)
                return null;
            await Edit.Init();
            const r = await Sw.Edit.TypeInfo.GetType(typeName);
            m.set(typeName, r || null);
            return r ? Edit.CloneType(r, props) : null;
        }

        /**
         * Create the default value of a type: primitive -> the member handler default; object -> the server default instance
         * (`edit/GetDefaultInstance`), else every member's handler default.
         * @param {object} type - The TypeDesc.
         * @param {?object} [options] - Options.
         * @param {boolean} [setTypeName] - Add `$type`.
         * @returns {Promise<*>} The value.
         */
        static async CreateDefault(type, options, setTypeName) {
            await Edit.Init();
            await Edit.LoadPlugins(type);
            options = options || new Sw.Edit.Options();
            if (Edit.IsPrimitive(type)) {
                const m = type.Members[0];
                return Edit.GetTypeHandler(type.TypeName, m, options).CreateDefault(m, options);
            }
            const typeName = type.TypeName;
            try {
                let o = typeName ? await Sw.Edit.TypeInfo.GetInstance(typeName) : null;
                if (o !== null && o !== undefined) {
                    if (setTypeName && !o.$type)
                        o = Object.assign({ $type: typeName }, o);
                    return o;
                }
            }
            catch {
            }
            const o = {};
            if (setTypeName)
                o.$type = typeName;
            for (const m of type.Members || [])
                o[m.Name] = Edit.GetTypeHandler(m.TypeName, m, options).CreateDefault(m, options);
            return o;
        }

        /**
         * Remove (in place, recursively) properties that are not declared members (`$type` is kept).
         * @param {object} type - The TypeDesc.
         * @param {*} obj - The value.
         * @returns {Promise<*>} obj for primitive types.
         */
        static async CleanUp(type, obj) {
            if (!obj || !type)
                return;
            if (Edit.IsPrimitive(type))
                return obj;
            const m = type.Members || [];
            const valid = new Set(["$type"].concat(m.map(x => x.Name)));
            for (const p of Object.keys(obj))
                if (!valid.has(p))
                    delete obj[p];
            for (const mi of m) {
                if ((mi.Flags & Flags.IsPrimitive) !== 0)
                    continue;
                const val = obj[mi.Name];
                if (!val || typeof val !== "object")
                    continue;
                if (Array.isArray(val)) {
                    const et = await Edit.GetType(mi.ElementTypeName, mi.ElementInst);
                    for (const v of val)
                        await Edit.CleanUp(et, v);
                    continue;
                }
                if ((mi.Flags & Flags.Collection) !== 0)
                    continue;
                await Edit.CleanUp(await Edit.GetType(mi.TypeName, mi.ElementInst), val);
            }
        }

        /**
         * Create an editor for a server type and set its value.
         * @param {string} serverTypeName - .NET type name.
         * @param {?string} [style] - Layout style.
         * @param {?object} [options] - Options.
         * @param {?object} [props] - Properties assigned on the type description.
         * @param {*} [obj] - Initial value (undefined = default).
         * @returns {Promise<?Edit>} The editor, or null for an unknown type.
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
         * A stand alone editor for one boolean value (a toggle with a label).
         * @param {string} name - Member name and display name.
         * @param {boolean} value - Initial value.
         * @param {string} title - Tooltip (summary).
         * @param {function(boolean):(Promise<void>|void)} [onChange] - Called with each new value.
         * @param {boolean} [readOnly] - Read only.
         * @param {boolean} [showMenu] - Show the member menu icon.
         * @returns {Promise<Edit>} The editor.
         */
        static async Boolean(name, value, title, onChange, readOnly, showMenu) {
            await Edit.Init();
            const O = Sw.Edit.Options;
            const opt = (showMenu ? (readOnly ? O.CleanReadOnlyMenu : O.CleanMenu) : (readOnly ? O.CleanReadOnly : O.Clean)).Clone();
            const e = new Edit({
                Flags: Flags.IsPrimitive,
                Members: [{ Name: name, DisplayName: name, Flags: Flags.IsPrimitive, Summary: title, TypeName: "System.Boolean" }],
            }, "Desktop", opt);
            await e.SetObject(value);
            if (onChange)
                e.Element.addEventListener("EditChange", async ev => await onChange(ev.detail.Value));
            return e;
        }

        // ------------------------------------------------------------------ values

        /**
         * The current value (primitives unwrapped).
         * @returns {*} The value.
         */
        GetObject() {
            const obj = this.Object;
            if (!Edit.IsPrimitive(this.Type) || !obj)
                return obj;
            return obj[this.Type.Members[0].Name];
        }

        /**
         * A JSON copy of a value restricted to the declared members.
         * @param {*} src - The value.
         * @returns {*} The copy (src itself for primitives).
         */
        GetCleanCopy(src) {
            const t = this.Type;
            if (Edit.IsPrimitive(t))
                return src;
            const n = {};
            for (const m of t.Members || [])
                n[m.Name] = src[m.Name];
            return JSON.parse(JSON.stringify(n));
        }

        /**
         * Compare two values (JSON for objects).
         * @param {*} a - First.
         * @param {*} b - Second.
         * @returns {boolean} True when different.
         */
        static IsDifferent(a, b) {
            if (typeof a !== typeof b)
                return true;
            if (typeof a !== "object")
                return a !== b;
            return JSON.stringify(a) !== JSON.stringify(b);
        }

        /**
         * First time setup (after the editor files are loaded): resolve the type handler, apply the IgnoreMembers rule and
         * build the title row.
         */
        Prepare() {
            if (this.Prepared)
                return;
            this.Prepared = true;
            let type = this.Type;
            const m = this.Member ?? (((type.Flags & (Flags.Collection | Flags.IsPrimitive)) !== 0 && type.Members) ? type.Members[0] : null);
            /** @type {object} The handler of the whole type. */
            this.TypeHandler = Edit.GetTypeHandler(type.TypeName, m);
            if (this.TypeHandler.IgnoreMembers) {
                type = Object.assign({}, type);
                type.Flags |= Flags.IsPrimitive;
                const nm = Object.assign({}, type);
                nm.Name = nm.DisplayName;
                type.Members = [nm];
                this.Type = type;
            }
            const e = this.Element;
            if (this.Options.Title) {
                const t = this.Type;
                let title = null;
                if (this.Options.Dev) {
                    title = addLine(title, _TF("Type name", 'This is the header of a tool tip row that displays the name of a .NET data type, ex: "Type name: System.String"'), t.TypeName);
                    title = addLine(title, _TF("Assembly", 'This is the header of a tool tip row that displays the name of the assembly that a .NET data type was declared in, ex: "Assembly: SysWeaver.Network.Clien"'), t.Asm);
                }
                title = addLine(title, _TF("Summary", 'This is the header of a tool tip row that displays the summary text extracted from a code comment on a method, ex. "Summary: Returns a random prime"'), t.Summary);
                title = addLine(title, _TF("Remarks", 'This is the header of a tool tip row that displays the remarks text extracted from a code comment on a method, ex. "Remarks: This method is not thread safe"'), t.Remarks);
                this.Head = el("div.sw-edit-head");
                e.append(this.Head);
                this.Edit.AddTypeInfo(this, this.Head, t, title || "", this.Options);
            }
            e.append(this.Body);
            this.AdoptCard();
        }

        /**
         * Set the edited value and (re)build the member rows. `undefined` (or a falsy object) is replaced by the default.
         * The object is edited in place.
         * @param {*} [obj] - The value.
         * @returns {Promise<void>}
         */
        async SetObject(obj) {
            await Edit.Init();
            if (this.PendingTypeName && !await this.ResolveTypeName())
                return;
            await Edit.LoadPlugins(this.Type);
            this.Prepare();
            const options = this.Options;
            const type = this.Type;
            if (obj === undefined)
                obj = await Edit.CreateDefault(type, options);
            const members = type.Members || [];
            const e = this.Element;
            for (const l of this.RemoveListerners)
                e.removeEventListener("EditChange", l);
            this.RemoveListerners.length = 0;
            this.MemberMap.clear();
            const body = this.Body;
            body.replaceChildren();
            if (Edit.IsPrimitive(type))
                obj = { [members[0].Name]: obj };
            //  A null object is shown as its type's default (edit/GetDefaultInstance), as the original: an API that returned null shows
            //  the empty form, its JSON tab says "null" (P3-A-9)
            else if (!obj)
                obj = await Edit.CreateDefault(type, options);
            this.Object = obj;
            const visible = members.filter(m => (m.Flags & Flags.Hide) === 0);
            const layout = this.Edit;
            //  Units (member Unit) sit in a slot of one width on every number row, so the boxes and sliders line up
            const unitLen = Math.max(0, ...visible.map(m => m.Unit ? String(m.Unit).length : 0));
            e.style.setProperty("--sw-edit-suffix-w", unitLen ? (unitLen + 1) + "ch" : "0px");
            e.classList.toggle("sw-edit-units", unitLen > 0);
            let group = null;
            for (let i = 0; i < visible.length; ++i) {
                const member = visible[i];
                //  A heading before the first member of a group (member Group)
                if (member.Group && member.Group !== group) {
                    group = member.Group;
                    body.append(el("div.sw-edit-group", { text: group, attrs: { role: "heading", "aria-level": "3" } }));
                }
                const item = await layout.AddMember(obj, this, body, member, this.MemberTitle(member), options, i === visible.length - 1);
                if (!item)
                    continue;
                this.MemberMap.set(member.Name, item);
                this.WireHide(item, member, obj);
            }
            this.UpdateGroups();
            //  Options.MarkChanged: a member that is not at its default gets an accent name and a dot (as the settings grid)
            if (options.MarkChanged) {
                const mark = () => this.MarkChanged();
                mark();
                e.addEventListener("EditChange", mark);
                this.RemoveListerners.push(mark);
            }
            if (!visible.length)
                body.append(el("div.sw-edit-empty", { text: _TF("No members", "Shown in an object editor when the object has no editable members") }));
            if (this.UpdateSummary)
                this.UpdateSummary();
        }

        /**
         * Resolve the type name given to the constructor (client registered types, else the server). An unknown name shows an error
         * state in the editor (instead of "No members") and logs a console error.
         * @returns {Promise<boolean>} True when the type was found.
         */
        async ResolveTypeName() {
            const name = this.PendingTypeName;
            let type = null;
            try {
                type = await Edit.GetType(name);
            }
            catch (e) {
                console.error(e);
            }
            if (!type) {
                const msg = "Sw.Edit: unknown type \"" + name + "\": register it first with Sw.Edit.AddType({ TypeName: \"" + name + "\", ... }) or check the .NET type name (the server's edit/GetTypeInfo doesn't know it).";
                console.error(msg);
                this.Body.replaceChildren(el("div.sw-edit-typeerror", { attrs: { role: "alert" } },
                    Sw.Icon ? Sw.Icon("warning") : null,
                    el("span", { text: _T("Unknown type \"{0}\": the editor cannot show it.", name, "Shown in an object editor when the type name it was created with is not known.{0} is the type name") })));
                if (!this.Body.isConnected)
                    this.Element.append(this.Body);
                return false;
            }
            this.PendingTypeName = null;
            this.Type = type;
            this.ObjName = this.Options.Name || (type.TypeName ? type.TypeName.split(".").pop() : null) || "Edit";
            if (!this.Options.KeyName && type.DisplayName)
                this.Element.setAttribute("aria-label", type.DisplayName);
            return true;
        }

        /**
         * Test if the whole value is read only: the ReadOnly option, a read only type, or (primitive / collection editors) a read
         * only member. Used by the title row menu, the title row drop target and the keyboard paste of the whole value.
         * @returns {boolean} True when the value can't be replaced.
         */
        IsObjectReadOnly() {
            const type = this.Type;
            if (this.Options.ReadOnly || ((type.Flags & Flags.ReadOnly) !== 0))
                return true;
            const m = Edit.IsPrimitive(type) && type.Members ? type.Members[0] : null;
            return !!(m && (m.Flags & Flags.ReadOnly) !== 0);
        }

        /**
         * A short readable form of a .NET type name: no namespaces, `Nullable<T>` as `T?`, generic arguments as `<...>`
         * ("System.Collections.Generic.List`1[[System.String]]" -> "List<String>").
         * @param {?string} typeName - The type name.
         * @returns {string} The short name.
         */
        static ShortTypeName(typeName) {
            if (!typeName)
                return "";
            const n = /^System\.Nullable`1\[\[(.+)\]\]$/.exec(typeName);
            if (n)
                return Edit.ShortTypeName(n[1]) + "?";
            const g = /^([^`\[]+)`\d+\[(.*)\](\[,*\])?$/.exec(typeName);
            if (g) {
                //  Split the generic arguments "[[A],[B]]" at the top level
                const args = [];
                let depth = 0, cur = "";
                for (const ch of g[2]) {
                    if (ch === "[" && depth++ === 0)
                        continue;
                    if (ch === "]" && --depth === 0) {
                        args.push(cur);
                        cur = "";
                        continue;
                    }
                    if (depth > 0)
                        cur += ch;
                }
                return Edit.ShortTypeName(g[1]) + "<" + args.map(a => Edit.ShortTypeName(a.split(",")[0])).join(", ") + ">" + (g[3] || "");
            }
            return typeName.split(",")[0].split(".").pop().replace(/\+/g, ".");
        }

        /**
         * The hint line under a member name (Design v2, PORTING.md B14): the short type and the constraints, ex: "Int32 · 1 to 500",
         * "String · max 64 chars", "String[] · 1 to 5 items", "Color · Rgba".
         * A handler with a `Hint(member, options)` method (plug-ins) gives its own hint (a string, "" = none; null / undefined = the
         * automatic hint). With the option PlainHints (the CleanHints presets: forms for people) the hint has no type names, only the
         * format, range and unit in words (PlainHint).
         * @param {object} member - The member.
         * @param {object} [options] - The editor options.
         * @returns {string} The hint.
         */
        static MemberHint(member, options) {
            //  A client type's own hint (member Hint; "" = none) wins over the automatic one
            if (member.Hint !== undefined && member.Hint !== null)
                return String(member.Hint);
            const th = options ? Edit.GetTypeHandler(member.TypeName, member) : null;
            if (th && typeof th.Hint === "function") {
                const h = th.Hint(member, options);
                if (h !== undefined && h !== null)
                    return String(h);
            }
            if (options && options.PlainHints)
                return Edit.PlainHint(member);
            const f = member.Flags || 0;
            const spec = member.Type && !/^(Hide:|Allow\.)/.test(member.Type) ? member.Type.split(":") : null;
            let type = Edit.ShortTypeName(member.TypeName);
            //  A number with a unit (Unit:Seconds) keeps its type and range, the unit follows the range: "Int32 · 1 to 3600 s"
            const unitSpec = spec && spec[0] === "Unit" && Edit.Handlers.Unit && Edit.Handlers.Unit.Units ? Edit.Handlers.Unit.Units[spec[1]] : null;
            if (spec && !unitSpec)
                type = spec[0] + (spec[1] && spec[1] !== "Auto" ? " " + spec[1] : "");
            else if ((f & Flags.IsFlags) !== 0)
                type += " (flags)";
            const parts = [type];
            //  Color members that can't store alpha say so (a transparent color typed / picked there is stored opaque)
            const cf = spec && spec[0] === "Color" ? (spec[1] || "Auto") : null;
            const cssColor = cf === "Css" || cf === "Hex" || (cf === "Auto" && member.TypeName === "System.String");
            if (cf && (/^(Rgb|HdrRgb|Rgb8|UInt32Rgb)$/.test(cf) || (cssColor && spec.indexOf("Alpha") < 2)))
                parts.push(_TF("no alpha", "Part of the hint line of a color member that has no alpha (opacity) channel"));
            const min = Has(member.Min) && member.Min !== "" ? member.Min : null;
            const max = Has(member.Max) && member.Max !== "" ? member.Max : null;
            const unit = (f & Flags.Collection) !== 0 ? " " + _TF("items", "Unit in the hint line of a collection member, ex: 1 to 5 items")
                : member.TypeName === "System.String" ? " " + _TF("chars", "Unit in the hint line of a text member, ex: max 64 chars")
                    : unitSpec && unitSpec.Suffix ? " " + unitSpec.Suffix : "";
            if (unitSpec && min === null && max === null)
                parts.push(spec[1].toLowerCase());
            if ((f & Flags.IsEnum) === 0 && (!spec || unitSpec)) {
                if (min !== null && max !== null)
                    parts.push(_T("{0} to {1}", min, max, "Range in the hint line of a member, ex: 1 to 500.{0} is the minimum.{1} the maximum") + unit);
                else if (min !== null && min !== "0")
                    parts.push(_T("min {0}", min, "Minimum in the hint line of a member, ex: min 3 chars.{0} is the minimum") + unit);
                else if (max !== null)
                    parts.push(_T("max {0}", max, "Maximum in the hint line of a member, ex: max 64 chars.{0} is the maximum") + unit);
            }
            if ((f & Flags.AcceptNull) !== 0 && !type.endsWith("?"))
                parts.push(_TF("nullable", "Part of the hint line of a member that may be null"));
            return parts.filter(x => x).join(" · ");
        }

        /**
         * The hint line for people (option PlainHints, the CleanHints presets): no .NET type names or raw specs, only what helps when
         * typing: the format ("e-mail address"), the range in words ("7 to 365 days", "up to 20 characters", "up to 6 items"),
         * "no transparency" for colors without alpha, "one or more" for flags, "optional" for nullable members; "" when there is nothing
         * to say (plug-ins add theirs with a Hint method).
         * @param {object} member - The member.
         * @returns {string} The hint.
         */
        static PlainHint(member) {
            const f = member.Flags || 0;
            const spec = member.Type && !/^(Hide:|Allow\.)/.test(member.Type) ? member.Type.split(":") : null;
            const kind = spec ? spec[0] : null;
            const parts = [];
            const unitSpec = kind === "Unit" && Edit.Handlers.Unit && Edit.Handlers.Unit.Units ? Edit.Handlers.Unit.Units[spec[1]] : null;
            if (kind === "Color") {
                const cf = spec[1] || "Auto";
                const cssColor = cf === "Css" || cf === "Hex" || (cf === "Auto" && member.TypeName === "System.String");
                if (/^(Rgb|HdrRgb|Rgb8|UInt32Rgb)$/.test(cf) || (cssColor && spec.indexOf("Alpha") < 2))
                    parts.push(_TF("no transparency", "Part of the hint line of a color member (forms for end users) that can't store an alpha (opacity) channel"));
            }
            else if (kind === "Email")
                parts.push(_TF("e-mail address", "Hint line of a member that takes an e-mail address (forms for end users)"));
            else if (kind === "Url")
                parts.push(_TF("web address", "Hint line of a member that takes a web address / url (forms for end users)"));
            else if (kind === "Phone")
                parts.push(_TF("phone number", "Hint line of a member that takes a phone number (forms for end users)"));
            const min = Has(member.Min) && member.Min !== "" ? member.Min : null;
            const max = Has(member.Max) && member.Max !== "" ? member.Max : null;
            const ranged = (f & Flags.IsEnum) === 0 && (!spec || unitSpec || kind === "Email" || kind === "Url" || kind === "Phone");
            if (ranged && (min !== null || max !== null)) {
                const coll = (f & Flags.Collection) !== 0;
                const text = !coll && member.TypeName === "System.String";
                const ts = !coll && /^(System\.Nullable`1\[\[)?System\.TimeSpan\b/.test(member.TypeName || "") && Edit.Handlers.TimeSpan;
                const longUnit = unitSpec ? ({
                    Milliseconds: _TF("ms", "Unit in the hint line of a duration in milliseconds, ex: 10 to 500 ms"),
                    Seconds: _TF("seconds", "Unit in the hint line of a duration in seconds, ex: 1 to 60 seconds"),
                    Minutes: _TF("minutes", "Unit in the hint line of a duration in minutes, ex: 1 to 60 minutes"),
                    Hours: _TF("hours", "Unit in the hint line of a duration in hours, ex: 1 to 24 hours"),
                    Days: _TF("days", "Unit in the hint line of a duration in days, ex: 7 to 365 days"),
                })[spec[1]] ?? unitSpec.Suffix : "";
                const v = x => ts ? Edit.Handlers.TimeSpan.Human(Edit.Handlers.TimeSpan.Parse(x)) : x;
                const u = coll ? " " + _TF("items", "Unit in the hint line of a collection member, ex: 1 to 5 items")
                    : text ? " " + _TF("characters", "Unit in the hint line of a text member (forms for end users), ex: up to 20 characters")
                        : longUnit ? " " + longUnit : "";
                if (min !== null && max !== null)
                    parts.push(_T("{0} to {1}", v(min), v(max), "Range in the hint line of a member, ex: 1 to 500.{0} is the minimum.{1} the maximum") + u);
                else if (max !== null)
                    parts.push(_T("up to {0}", v(max), "Maximum in the hint line of a member (forms for end users), ex: up to 20 characters.{0} is the maximum") + u);
                else if (min !== "0" && !(ts && Edit.Handlers.TimeSpan.Parse(min) === 0))
                    parts.push(_T("at least {0}", v(min), "Minimum in the hint line of a member (forms for end users), ex: at least 3 characters.{0} is the minimum") + u);
            }
            if ((f & Flags.IsFlags) !== 0)
                parts.push(_TF("one or more", "Hint line of a member where several options can be chosen at once (flags; forms for end users)"));
            if ((f & Flags.AcceptNull) !== 0)
                parts.push(_TF("optional", "Hint line of a member that may be left without a value (nullable; forms for end users)"));
            return parts.filter(x => x).join(" · ");
        }

        /**
         * The hint line element (small.sw-edit-typehint): every " · " part is a span.sw-edit-hintpart that never wraps inside
         * ("2020-01-01 to 2030-12-31", "max 8 items"); the line wraps between the parts, a part wider than the label column ends
         * with an ellipsis and the element's tool tip has the whole hint. The textContent is the hint text.
         * @param {string} hint - The hint (MemberHint).
         * @returns {HTMLElement} The element.
         */
        /**
         * Size a number box to its value (Design v2): at least --sw-edit-number-w, wider for long values ("3.14159265358979"), at most
         * --sw-edit-number-max (then the tool tip shows the whole value). Sets --sw-edit-number-len on input / change / UpdateValue.
         * Plug-ins with their own number boxes (class sw-edit-number) can call it.
         * @param {HTMLInputElement} input - The box (from CreateTextInput).
         */
        static FitNumberBox(input) {
            if (!input || input.tagName !== "INPUT" || input.FitNumber)
                return;
            const fit = () => {
                input.style.setProperty("--sw-edit-number-len", ("" + input.value).length);
                //  Wider than --sw-edit-number-max: the tool tip shows the whole value
                requestAnimationFrame(() => {
                    const over = input.scrollWidth > input.clientWidth + 1;
                    if (over)
                        input.title = input.value;
                    else if (input.title === input.FitTitle)
                        input.removeAttribute("title");
                    input.FitTitle = over ? input.value : null;
                });
            };
            input.FitNumber = fit;
            input.addEventListener("input", fit);
            input.addEventListener("change", fit);
            input.addEventListener("blur", fit);
            const upd = input.UpdateValue;
            if (typeof upd === "function")
                input.UpdateValue = function () {
                    const r = upd.apply(this, arguments);
                    fit();
                    return r;
                };
            fit();
        }

        static HintElement(hint) {
            const e = el("small.sw-edit-typehint", { title: hint });
            hint.split(" · ").forEach((p, i) => {
                if (i)
                    e.append(" · ");
                e.append(el("span.sw-edit-hintpart", { text: p }));
            });
            return e;
        }

        /**
         * Tooltip of a member title (Dev mode: member name, type name, summary, remarks; else the summary).
         * @param {object} member - The member.
         * @returns {string} The text.
         */
        MemberTitle(member) {
            if (!this.Options.Dev)
                return member.Summary || "";
            let t = addLine(null, _TF("Member name", 'This is the header of a tool tip row that displays the name of a .NET property or field, ex: "Member name: InstanceId"'), member.Name);
            t = addLine(t, _TF("Type name", 'This is the header of a tool tip row that displays the name of a .NET data type, ex: "Type name: System.String"'), member.TypeName);
            t = addLine(t, _TF("Summary", 'This is the header of a tool tip row that displays the summary text extracted from a code comment on a method, ex. "Summary: Returns a random prime"'), member.Summary);
            t = addLine(t, _TF("Remarks", 'This is the header of a tool tip row that displays the remarks text extracted from a code comment on a method, ex. "Remarks: This method is not thread safe"'), member.Remarks);
            return t || "";
        }

        /**
         * Conditional hiding: a member `Type` of "Hide:<js expression>" (evaluated with `this` = the object) is re-evaluated
         * on every change.
         * @param {object} item - The member item.
         * @param {object} member - The member.
         * @param {object} obj - The object.
         */
        WireHide(item, member, obj) {
            const mt = member.Type;
            let fn;
            if (typeof member.HideIf === "function") {
                //  A client type's own rule (keeps the Type slot for the editor spec, ex: a unit or a color format)
                const hf = member.HideIf;
                fn = function () {
                    return hf(this);
                };
            }
            else {
                if (!mt || !mt.startsWith("Hide:"))
                    return;
                try {
                    fn = new Function("return " + mt.substring(5));
                }
                catch (x) {
                    console.warn("Invalid hide expression \"" + mt + "\": " + x);
                    return;
                }
            }
            const apply = () => {
                let hide = false;
                try {
                    hide = !!fn.call(obj);
                }
                catch {
                }
                if (hide)
                    item.Hide();
                else
                    item.Show();
                this.UpdateGroups();
            };
            apply();
            this.Element.addEventListener("EditChange", apply);
            this.RemoveListerners.push(apply);
        }

        /**
         * Mark the members that differ from their default (Options.MarkChanged): the row gets `sw-edit-changed` (an accent name and a
         * dot; the member menu's "Set default" resets it).
         */
        MarkChanged() {
            const obj = this.Object;
            if (!obj)
                return;
            const options = this.Options;
            for (const member of this.Type.Members || []) {
                const item = this.MemberMap.get(member.Name);
                if (!item || !item.Row)
                    continue;
                let def = item.DefValue;
                if (def === undefined && item.TypeHandler && typeof item.TypeHandler.GetDefault === "function")
                    try {
                        def = item.TypeHandler.GetDefault(member, options);
                    }
                    catch {
                        def = undefined;
                    }
                item.Row.classList.toggle("sw-edit-changed", def !== undefined && Edit.IsDifferent(obj[member.Name], def));
            }
        }

        /**
         * Hide the group headings (member Group) whose members are all hidden.
         */
        UpdateGroups() {
            const body = this.Body;
            if (!body)
                return;
            let head = null;
            let any = false;
            const done = () => {
                if (head)
                    head.classList.toggle("sw-hide", !any);
            };
            for (const c of body.children) {
                if (c.classList.contains("sw-edit-group")) {
                    done();
                    head = c;
                    any = false;
                }
                else if (c.classList.contains("sw-edit-row") && !c.classList.contains("sw-hide"))
                    any = true;
            }
            done();
        }

        /**
         * Re-render from the current value (used when an editor is the target of an undo path).
         * @returns {Promise<void>}
         */
        async UpdateValue() {
            await this.SetObject(this.GetObject());
        }

        /**
         * Change the displayed title (collection items after renumbering).
         * @param {string} name - The new title (ex: "[3]").
         */
        ChangeTitle(name) {
            this.Options.KeyName = name;
            this.Element.setAttribute("aria-label", name);
            const t = this.Head && this.Head.querySelector(".sw-edit-typetitle");
            if (t && t.textContent !== name)
                t.textContent = name;
            if (Edit.IsPrimitive(this.Type) && !this.Options.Title) {
                const n = this.Body.querySelector(".sw-edit-name");
                if (n && n.textContent !== name)
                    n.textContent = name;
                this.Type.Members[0].DisplayName = name;
            }
        }

        // ------------------------------------------------------------------ undo

        /**
         * Undo path of this editor: parent path + "." + ObjName + KeyName.
         * @returns {string} The path (ex: "Edit.Items[2]").
         */
        GetObjectName() {
            let n = this.ObjName;
            if (this.KeyName)
                n += this.KeyName;
            return this.Parent ? (this.Parent.GetObjectName() + "." + n) : n;
        }

        /**
         * Resolve a dotted undo path relative to this editor (the first segment is this editor).
         * @param {string} name - The path.
         * @returns {Promise<?object>} The item / editor.
         */
        async FindItem(name) {
            const p = name.split(".");
            const m = this.MemberMap;
            if (p.length <= 1) {
                if (Edit.IsPrimitive(this.Type))
                    return m.values().next().value || null;
                return this;
            }
            const sub = p[1].split("[");
            const item = m.get(sub[0]);
            if (!item)
                return null;
            if (p.length <= 2 && sub.length <= 1)
                return item;
            if (!item.FindItem)
                return item;
            return await item.FindItem(p.slice(1).join("."));
        }

        /**
         * Perform and record an undoable change. The target is addressed by path so it is found again after re-rendering;
         * on do / redo / undo the item is looked up from the root, changed, and refreshed (`UpdateValue(isFirst)`).
         * @param {function(object):Promise<void>} doChange - Applies the change to the found item.
         * @param {function(object):Promise<void>} doReverse - Reverts it.
         * @param {string} title - Description (undo log).
         * @param {string} [name] - Member name appended to the path.
         * @param {*} [tracking] - Coalescing token (consecutive changes with the same path and token merge).
         * @returns {Promise<void>}
         */
        async Invoke(doChange, doReverse, title, name, tracking) {
            let n = this.GetObjectName();
            if (!Edit.IsPrimitive(this.Type) && name)
                n += "." + name;
            let root = this;
            while (root.Parent)
                root = root.Parent;
            const onRedo = async isFirst => {
                const item = await root.FindItem(n);
                await doChange(item);
                if (item && item.UpdateValue)
                    await item.UpdateValue(isFirst !== false);
            };
            const onUndo = async () => {
                const item = await root.FindItem(n);
                await doReverse(item);
                if (item && item.UpdateValue)
                    await item.UpdateValue(false);
            };
            await this.UndoStack.Invoke(onRedo, onUndo, title, n, tracking);
        }

        /**
         * Record a member value change (the common case of the handlers).
         * @param {object} member - The member.
         * @param {*} newValue - New value.
         * @param {*} oldValue - Old value.
         * @param {string} title - Undo title.
         * @param {*} [tracking] - Coalescing token.
         * @returns {Promise<void>}
         */
        async SetMemberValue(member, newValue, oldValue, title, tracking) {
            await this.Invoke(async it => it && await it.SetValue(newValue), async it => it && await it.SetValue(oldValue), title, member.Name, tracking);
        }

        // ------------------------------------------------------------------ events

        /**
         * Create an EditChange event (values of Password members are reported as "***").
         * @param {?object} member - The changed member (null = whole object).
         * @param {*} value - The new value.
         * @param {?CustomEvent} [next] - The wrapped child event.
         * @param {*} [key] - Collection key.
         * @param {?string} [keyName] - Key display text.
         * @returns {CustomEvent} The event (detail {Next, Member, Value, Key, KeyName}).
         */
        static CreateChangeEvent(member, value, next, key, keyName) {
            if (member && (member.Flags & Flags.Password) !== 0 && value)
                value = "***";
            return new CustomEvent("EditChange", {
                detail: { Next: next || null, Member: member || null, Value: value, Key: Has(key) ? key : null, KeyName: keyName || null },
            });
        }

        /**
         * Dispatch an EditChange for a member of this editor.
         * @param {?object} member - The member (null = whole object).
         * @param {*} value - The value.
         */
        Changed(member, value) {
            this.Element.dispatchEvent(Edit.CreateChangeEvent(member, value, null, this.Key, this.KeyName));
        }

        /** Dispatch an EditChange for the whole value. */
        NotifyChange() {
            this.Changed(null, this.GetObject());
        }

        // ------------------------------------------------------------------ validation

        /**
         * Validate a value against a type.
         * @param {object} type - The TypeDesc.
         * @param {*} obj - The value (raw for primitives).
         * @param {object} options - Options.
         * @param {?string} name - Path prefix.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        static async ValidateType(type, obj, options, name) {
            const members = type && type.Members;
            if (!members)
                return null;
            const prim = Edit.IsPrimitive(type);
            if (!prim && (obj === null || obj === undefined))
                return null;
            for (const member of members) {
                if ((member.Flags & Flags.Hide) !== 0)
                    continue;
                const mn = member.Name;
                const path = name ? (prim ? name : (name + "." + mn)) : mn;
                const val = prim ? obj : obj[mn];
                if (val === undefined)
                    return [path, (member.DisplayName || mn) + ": " + _TF("No value found!", "Error message displayed when a value for a .NET type isn't found")];
                const r = await Edit.GetTypeHandler(member.TypeName, member, options).Validate(val, member, options, path);
                if (r)
                    return r;
            }
            return null;
        }

        /**
         * Validate the edited value.
         * With focusOnError the editor shows the error once: the message under the field (without the repeated member name), and a
         * toast only when that field can't be seen (a hidden / collapsed member, an editor that is not shown). The returned array then
         * has `Shown: true`: callers must not show it again.
         * @param {boolean} [focusOnError] - Focus (and flash) the failing input and show the message.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(focusOnError) {
            await Edit.Init();
            const prim = Edit.IsPrimitive(this.Type);
            const err = await Edit.ValidateType(this.Type, prim ? this.GetObject() : this.Object, this.Options, prim ? null : "Edit");
            if (!focusOnError)
                return err;
            //  The message of the last Validate(true) under its field (removed by the next Validate or when the value changes)
            if (this.FieldError) {
                this.FieldError();
                this.FieldError = null;
            }
            if (!err)
                return err;
            const fi = await this.FindItem(err[0]);
            const fe = fi && fi.Focus;
            let shown = false;
            if (fe) {
                fe.focus();
                if (fe.scrollIntoView)
                    fe.scrollIntoView({ block: "nearest" });
                const row = fe.closest && fe.closest(".sw-edit-row");
                if (row) {
                    row.classList.add("sw-edit-error");
                    setTimeout(() => row.classList.remove("sw-edit-error"), 2500);
                    const cell = row.querySelector(":scope > .sw-edit-value");
                    if (cell) {
                        const id = "sw-edit-err-" + (++Edit.ErrorN);
                        //  Under the field its name is not repeated ("Tags: Too few items" -> "Too few items"; also "Box › Serial: ...")
                        const nameE = row.querySelector(".sw-edit-name");
                        const name = nameE ? nameE.textContent.trim() : "";
                        let text = err[1];
                        if (name) {
                            const at = text.indexOf(name + ": ");
                            if (at === 0 || (at > 0 && text.lastIndexOf(" › ", at) === at - 3 && text.slice(0, at).indexOf(": ") < 0))
                                text = text.slice(at + name.length + 2);
                        }
                        const msg = el("div.sw-edit-fielderror", { id, text: text || err[1], attrs: { role: "alert" } });
                        cell.append(msg);
                        if (msg.scrollIntoView)
                            msg.scrollIntoView({ block: "nearest" });
                        shown = msg.getClientRects().length > 0;
                        row.classList.add("sw-edit-haserror");
                        const prev = fe.getAttribute("aria-describedby");
                        fe.setAttribute("aria-describedby", (prev ? prev + " " : "") + id);
                        fe.setAttribute("aria-invalid", "true");
                        const root = this.Element;
                        const clear = () => {
                            msg.remove();
                            row.classList.remove("sw-edit-haserror");
                            fe.removeAttribute("aria-invalid");
                            if (prev)
                                fe.setAttribute("aria-describedby", prev);
                            else
                                fe.removeAttribute("aria-describedby");
                            root.removeEventListener("EditChange", clear);
                            if (this.FieldError === clear)
                                this.FieldError = null;
                        };
                        root.addEventListener("EditChange", clear);
                        this.FieldError = clear;
                    }
                }
            }
            //  One error surface: the field message; a toast only when the field can't be seen
            if (!shown)
                Sw.Fail(err[1]);
            err.Shown = true;
            return err;
        }

        // ------------------------------------------------------------------ copy, paste, drag and drop

        /**
         * Text for copy / drag: "null", strings as is (unless forceJson), else JSON with `$type` for objects.
         * @param {*} obj - The value.
         * @param {boolean} [forceJson] - Always JSON.
         * @param {string} typeName - The .NET type name.
         * @returns {string} The text.
         */
        static GetTypedTextOrJson(obj, forceJson, typeName) {
            if (obj === null || obj === undefined)
                return "null";
            const ot = typeof obj;
            if (!forceJson && ot === "string")
                return obj;
            if (ot !== "object" || Array.isArray(obj))
                return JSON.stringify(obj);
            if (obj instanceof Map)
                return JSON.stringify(Object.fromEntries(obj));
            if (obj instanceof Date)
                return JSON.stringify(obj);
            if (!obj.$type && typeName)
                obj = Object.assign({ $type: typeName }, obj);
            return JSON.stringify(obj);
        }

        /**
         * Copy text of the whole value.
         * @param {boolean} [forceJson] - Always JSON.
         * @returns {string} The text.
         */
        GetCopyObject(forceJson) {
            return Edit.GetTypedTextOrJson(this.GetObject(), forceJson, this.Type.TypeName);
        }

        /**
         * Copy text of a member value.
         * @param {object} member - The member.
         * @param {boolean} [forceJson] - Always JSON.
         * @returns {string} The text.
         */
        GetCopyMember(member, forceJson) {
            let obj = this.GetObject();
            if (!Edit.IsPrimitive(this.Type))
                obj = obj[member.Name];
            return Edit.GetTypedTextOrJson(obj, forceJson, member.TypeName);
        }

        /**
         * Interpret a pasted / dropped text for a member (or a whole type).
         * Non string types are JSON parsed (the raw text is kept when it is no JSON); a value is accepted when its `$type`
         * matches, when it is null and null is accepted, or when the handler's IsOfType accepts it; it is then conditioned.
         * @param {?string} text - The text.
         * @param {object} member - Member (or type) description.
         * @param {object} handler - The type handler.
         * @param {object} options - Options.
         * @param {boolean} [fromJson] - The text is json (a drag of an editor value): a string member reads a json string too.
         * @returns {{Ok: boolean, Value: *, Json: boolean, Reason: ?string}} The result.
         */
        static ParseValue(text, member, handler, options, fromJson) {
            if (text === null || text === undefined || text === "")
                return { Ok: false, Reason: _TF("No known data on clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no known data found on the clipboard.") };
            let obj = text;
            let json = false;
            if (member.TypeName === "System.String") {
                //  A dragged string value is a json string ("\"#ffffff\""): the string, not the text with its quotes
                if (fromJson && text[0] === "\"") {
                    try {
                        const s = JSON.parse(text);
                        if (typeof s === "string")
                            obj = s;
                    }
                    catch {
                    }
                }
            }
            else {
                try {
                    obj = JSON.parse(text);
                    json = true;
                }
                catch {
                }
            }
            const okType = (obj !== null && typeof obj === "object" && ((obj.$type || "").split(",")[0] === member.TypeName))
                || (obj === null && (member.Flags & Flags.AcceptNull) !== 0);
            let ok = false;
            try {
                ok = okType || handler.IsOfType(obj, member, options);
            }
            catch {
            }
            if (!ok)
                return { Ok: false, Reason: _TF("Not a valid object on the clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no valid value on the clipboard.") };
            return { Ok: true, Value: obj === null ? null : handler.Condition(obj, member, options), Json: json && obj !== null && typeof obj === "object" };
        }

        /**
         * Test if a value can replace the whole edited value.
         * @param {*} obj - The value.
         * @returns {boolean} True if acceptable.
         */
        CanConsumeCopy(obj) {
            const type = this.Type;
            if (Edit.IsPrimitive(type)) {
                if (obj !== null && typeof obj === "object" && !Array.isArray(obj))
                    return false;
                const m = type.Members[0];
                return Edit.GetTypeHandler(m.TypeName, m).IsOfType(obj, m, this.Options);
            }
            return Edit.IsObjectOf(obj, type);
        }

        /**
         * Test if a json value is an object of a type: its `$type` matches, or (copied from a json text, ex: the JSON tab of
         * explore/api.html) it has no `$type` and every property is a declared member.
         * @param {*} obj - The value.
         * @param {object} type - The TypeDesc.
         * @returns {boolean} True when it is.
         */
        static IsObjectOf(obj, type) {
            if (obj === null || typeof obj !== "object" || Array.isArray(obj) || !type)
                return false;
            if (obj.$type)
                return obj.$type.split(",")[0] === type.TypeName;
            const names = new Set((type.Members || []).map(m => m.Name));
            const keys = Object.keys(obj);
            return names.size > 0 && keys.length > 0 && keys.every(k => names.has(k));
        }

        // ------------------------------------------------------------------ json type check

        /**
         * Check a json value against a type at every depth (explore/api.html's JSON tab, any page that lets the user type json for a
         * type): numbers (whole numbers and their range for the integer types), booleans, strings, enums (a defined name, the names of
         * a flags enum joined with ",", or a number), dates, TimeSpan ("c" format), Guid, collections (array / object for
         * dictionaries, every item), nested objects (unknown members are errors), null (only where the member accepts it).
         * Members left out are fine (they keep their defaults). Color, Url, ... specs are checked with their handler.
         * @param {object} type - The TypeDesc.
         * @param {*} value - The parsed json.
         * @param {?object} [options] - Editor options.
         * @returns {Promise<?{Path: string, Text: string}>} The first problem (Path = json path, ex: "Filters[0].Op"), or null.
         */
        static async CheckJson(type, value, options) {
            options = options || OptionDefaults;
            if (!type)
                return null;
            if (Edit.IsPrimitive(type)) {
                const m = type.Members && type.Members[0];
                return m ? await Edit.CheckJsonMember(m, value, "", options, 0) : null;
            }
            return await Edit.CheckJsonObject(type, value, "", options, 0);
        }

        /**
         * Check a json object against an object type (see CheckJson).
         * @param {object} type - The TypeDesc.
         * @param {*} value - The value.
         * @param {string} path - Json path of the value.
         * @param {object} options - Options.
         * @param {number} depth - Recursion depth (stops at 16).
         * @returns {Promise<?{Path: string, Text: string}>} The problem or null.
         */
        static async CheckJsonObject(type, value, path, options, depth) {
            const T = Edit.ShortTypeName(type.TypeName) || type.DisplayName || "";
            if (value === null)
                return null;
            if (typeof value !== "object" || Array.isArray(value))
                return { Path: path, Text: _T("Expected an object ({0}), got {1}", T, Edit.JsonKind(value), "Error of a json value that should be an object.{0} is the type name.{1} what the value is, ex: a text") };
            if (depth > 16)
                return null;
            const members = type.Members || [];
            const byName = new Map(members.map(m => [m.Name, m]));
            for (const k of Object.keys(value)) {
                if (k === "$type")
                    continue;
                const p = path ? path + "." + k : k;
                const m = byName.get(k);
                if (!m)
                    return { Path: p, Text: _T("Unknown member \"{0}\" of {1} (the members are: {2})", k, T, members.map(x => x.Name).join(", "), "Error of a json object with a property that is not a member of its type.{0} is the property name.{1} the type name.{2} the list of the member names") };
                const r = await Edit.CheckJsonMember(m, value[k], p, options, depth + 1);
                if (r)
                    return r;
            }
            return null;
        }

        /**
         * Describe what a json value is ("a text", "a number" ...), for error messages.
         * @param {*} v - The value.
         * @returns {string} The description.
         */
        static JsonKind(v) {
            if (v === null)
                return "null";
            if (Array.isArray(v))
                return _TF("a list", "Describes a json array in an error message, ex: Expected a number, got a list");
            switch (typeof v) {
                case "string": return _T("the text \"{0}\"", Edit.Short(v), "Describes a json string in an error message, ex: Expected a number, got the text \"abc\".{0} is the text");
                case "number": return _T("the number {0}", v, "Describes a json number in an error message.{0} is the number");
                case "boolean": return _T("the boolean {0}", v, "Describes a json true / false in an error message.{0} is true or false");
                default: return _TF("an object", "Describes a json object in an error message, ex: Expected a number, got an object");
            }
        }

        /**
         * Check the json value of one member (see CheckJson).
         * @param {object} member - The member (or element) description.
         * @param {*} v - The value.
         * @param {string} path - Json path.
         * @param {object} options - Options.
         * @param {number} depth - Recursion depth.
         * @returns {Promise<?{Path: string, Text: string}>} The problem or null.
         */
        static async CheckJsonMember(member, v, path, options, depth) {
            const f = member.Flags || 0;
            let tn = member.TypeName || "";
            const nul = /^System\.Nullable`1\[\[([^,\]]+)/.exec(tn);
            if (nul)
                tn = nul[1];
            const short = Edit.ShortTypeName(member.TypeName);
            const err = text => ({ Path: path, Text: text });
            const expected = what => err(_T("Expected {0} ({1}), got {2}", what, short, Edit.JsonKind(v), "Error of a json value of the wrong type.{0} describes the expected value, ex: a whole number.{1} is the type name.{2} what the value is"));
            if (v === null) {
                const prim = (f & Flags.IsPrimitive) !== 0 || /^System\.(Boolean|Byte|SByte|U?Int(16|32|64)|Single|Double|Decimal|Char|DateTime|DateOnly|TimeOnly|TimeSpan|Guid|DateTimeOffset)$/.test(tn);
                if (nul || (f & Flags.AcceptNull) !== 0 || !prim || tn === "System.String")
                    return null;
                return err(_T("null is not allowed ({0})", short, "Error of a json null for a member that needs a value.{0} is the type name"));
            }
            const spec = member.Type && !/^(Hide:|Allow\.)/.test(member.Type) ? member.Type : null;
            //  Editor specs (Color, Email, Url, ...): the handler decides
            if (spec) {
                const h = Edit.GetTypeHandler(member.TypeName, member);
                if (h && !h.ForceReadOnly && h.IsOfType && !(h instanceof (Edit.Handlers.Collection || function () { }))) {
                    let ok = false;
                    try {
                        ok = h.IsOfType(v, member, options);
                    }
                    catch {
                    }
                    return ok ? null : err(_T("Not a valid {0} value: {1}", spec.split(":")[0], Edit.JsonKind(v), "Error of a json value that the member's editor doesn't accept.{0} is the editor kind, ex: Color.{1} what the value is"));
                }
            }
            if ((f & Flags.Collection) !== 0 && !(spec && spec.startsWith("Color"))) {
                const CH = Edit.Handlers.Collection;
                const isMap = !!member.KeyTypeName;
                if (isMap ? (typeof v !== "object" || Array.isArray(v)) : !Array.isArray(v)) {
                    //  byte[] is sent as a base64 text
                    if (!isMap && typeof v === "string" && /Byte\[\]$/.test(member.TypeName) && /^[A-Za-z0-9+/]*={0,2}$/.test(v))
                        return null;
                    return expected(isMap ? _TF("an object (a dictionary)", "Describes the expected json value of a dictionary member in an error message") : _TF("a list", "Describes the expected json value of a list member in an error message"));
                }
                const et = CH && CH.ItemType ? await CH.ItemType(member, null) : null;
                if (!et)
                    return null;
                const keys = Object.keys(v);
                for (const k of keys) {
                    const p = path + (isMap ? "[" + JSON.stringify(k) + "]" : "[" + k + "]");
                    const r = Edit.IsPrimitive(et)
                        ? await Edit.CheckJsonMember(Object.assign({}, et.Members[0], { TypeName: member.ElementTypeName }), v[k], p, options, depth + 1)
                        : await Edit.CheckJsonObject(et, v[k], p, options, depth + 1);
                    if (r)
                        return r;
                }
                return null;
            }
            if ((f & Flags.IsEnum) !== 0) {
                const packed = "" + (member.Default ?? "");
                const gt = packed.indexOf(">");
                const names = (gt >= 0 ? packed.substring(gt + 1).split("|") : []).filter(x => x).map(x => x.split("<")[1]).filter(x => x);
                if (typeof v === "number")
                    return Number.isInteger(v) ? null : expected(_TF("a whole number or a name", "Describes the expected json value of an enum member in an error message"));
                if (typeof v !== "string")
                    return expected(_TF("a name or a number", "Describes the expected json value of an enum member in an error message"));
                const lower = new Set(names.map(x => x.toLowerCase()));
                const parts = (f & Flags.IsFlags) !== 0 ? v.split(",").map(x => x.trim()) : [v.trim()];
                const bad = parts.find(x => !lower.has(x.toLowerCase()) && !/^-?\d+$/.test(x));
                return bad === undefined ? null : err(_T("\"{0}\" is not a value of {1} (the values are: {2})", bad, short, names.join(", "), "Error of a json enum value that is not defined.{0} is the value.{1} the enum type.{2} the list of the names"));
            }
            const ints = {
                "System.Byte": [0, 255], "System.SByte": [-128, 127], "System.Int16": [-32768, 32767], "System.UInt16": [0, 65535],
                "System.Int32": [-2147483648, 2147483647], "System.UInt32": [0, 4294967295],
                "System.Int64": [Number.MIN_SAFE_INTEGER, Number.MAX_SAFE_INTEGER], "System.UInt64": [0, Number.MAX_SAFE_INTEGER],
            };
            const ir = ints[tn];
            if (ir) {
                if (typeof v !== "number" || !Number.isInteger(v))
                    return expected(_TF("a whole number", "Describes the expected json value of an integer member in an error message"));
                if (v < ir[0] || v > ir[1])
                    return err(_T("{0} is out of the range of {1} ({2} to {3})", v, short, ir[0], ir[1], "Error of a json number outside the range of its integer type.{0} is the number.{1} the type.{2} the minimum.{3} the maximum"));
                return null;
            }
            switch (tn) {
                case "System.Single": case "System.Double": case "System.Decimal":
                    return typeof v === "number" ? null : expected(_TF("a number", "Describes the expected json value of a number member in an error message"));
                case "System.Boolean":
                    return typeof v === "boolean" ? null : expected(_TF("true or false", "Describes the expected json value of a boolean member in an error message"));
                case "System.String":
                    return typeof v === "string" ? null : expected(_TF("a text", "Describes the expected json value of a text member in an error message"));
                case "System.Char":
                    return typeof v === "string" && v.length === 1 ? null : expected(_TF("one character", "Describes the expected json value of a character member in an error message"));
                case "System.DateTime": case "System.DateTimeOffset":
                    return typeof v === "string" && /^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?)?$/.test(v) && !isNaN(Date.parse(v)) ? null
                        : expected(_TF("a date and time, ex: 2026-10-19T08:30:00Z", "Describes the expected json value of a date and time member in an error message"));
                case "System.DateOnly":
                    return typeof v === "string" && /^\d{4}-\d{2}-\d{2}$/.test(v) && !isNaN(Date.parse(v)) ? null
                        : expected(_TF("a date, ex: 2026-10-19", "Describes the expected json value of a date member in an error message"));
                case "System.TimeOnly":
                    return typeof v === "string" && /^([01]?\d|2[0-3]):[0-5]\d(:[0-5]\d(\.\d{1,7})?)?$/.test(v) ? null
                        : expected(_TF("a time of day, ex: 08:30:00", "Describes the expected json value of a time of day member in an error message"));
                case "System.TimeSpan":
                    return typeof v === "string" && /^-?(\d+\.)?\d{1,2}:[0-5]\d(:[0-5]\d(\.\d{1,7})?)?$/.test(v) ? null
                        : expected(_TF("a duration, ex: 00:01:30 or 1.12:00:00", "Describes the expected json value of a TimeSpan member in an error message"));
                case "System.Guid":
                    return typeof v === "string" && /^\{?[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\}?$/i.test(v) ? null
                        : expected(_TF("a guid, ex: 3f2504e0-4f89-11d3-9a0c-0305e82c3301", "Describes the expected json value of a Guid member in an error message"));
                case "System.Object":
                    return null;
            }
            if ((f & Flags.IsPrimitive) !== 0)
                return null;
            //  Objects: the members, at every depth (types the server doesn't describe accept anything)
            const t = await Edit.GetType(member.TypeName, member.ElementInst);
            if (!t || !t.Members || !t.Members.length)
                return null;
            if (Edit.IsPrimitive(t))
                return await Edit.CheckJsonMember(Object.assign({}, t.Members[0], { TypeName: member.TypeName }), v, path, options, depth + 1);
            return await Edit.CheckJsonObject(t, v, path, options, depth + 1);
        }

        /**
         * Find the line of each value in a json text (for error markers): json path ("A.B[2].C", dictionary keys as ["key"]) -> 1 based
         * line of the value (of the key for object members). The text must be valid json.
         * @param {string} text - The json text.
         * @returns {Map<string, number>} The lines ("" = the root).
         */
        static JsonLines(text) {
            const lines = new Map();
            let i = 0, line = 1;
            const n = text.length;
            const ws = () => {
                for (; i < n; ++i) {
                    const c = text[i];
                    if (c === "\n")
                        ++line;
                    else if (c !== " " && c !== "\t" && c !== "\r")
                        break;
                }
            };
            const str = () => {
                const s = i;
                ++i;
                for (; i < n && text[i] !== "\""; ++i)
                    if (text[i] === "\\")
                        ++i;
                ++i;
                return JSON.parse(text.substring(s, i));
            };
            const val = (path, at) => {
                ws();
                if (!lines.has(path))
                    lines.set(path, at || line);
                const c = text[i];
                if (c === "{") {
                    ++i;
                    for (; ;) {
                        ws();
                        if (text[i] === "}") {
                            ++i;
                            return;
                        }
                        const kl = line;
                        const k = str();
                        ws();
                        ++i; // :
                        val(path ? path + "." + k : k, kl);
                        lines.set(path + "[" + JSON.stringify(k) + "]", lines.get(path ? path + "." + k : k));
                        ws();
                        if (text[i] === ",")
                            ++i;
                    }
                }
                if (c === "[") {
                    ++i;
                    let idx = 0;
                    for (; ;) {
                        ws();
                        if (text[i] === "]") {
                            ++i;
                            return;
                        }
                        val(path + "[" + idx++ + "]");
                        ws();
                        if (text[i] === ",")
                            ++i;
                    }
                }
                if (c === "\"") {
                    str();
                    return;
                }
                while (i < n && !/[\s,\]}]/.test(text[i]))
                    ++i;
            };
            try {
                val("", 1);
            }
            catch {
            }
            return lines;
        }

        /**
         * Make an element a drag source for a value (application/json + text/plain).
         * @param {HTMLElement} e - The element.
         * @param {function():string} getJson - Returns the json text.
         */
        static DragSource(e, getJson) {
            e.draggable = true;
            e.classList.add("EditDraggable", "sw-edit-draggable");
            e.addEventListener("dragstart", ev => {
                const t = ev.target;
                if (t !== e && t && t.closest && t.closest("input,textarea,select"))
                    return;
                //  The drag handle of a collection item re-orders with pointer events (touch too): no native drag of the title row
                if (t && t.closest && t.closest(".sw-edit-grip")) {
                    ev.preventDefault();
                    return;
                }
                const data = getJson();
                if (!data)
                    return;
                //  A string value is a json string in application/json and the plain string as text (text boxes, other apps)
                let plain = data;
                if (data[0] === "\"") {
                    try {
                        plain = JSON.parse(data);
                    }
                    catch {
                    }
                }
                ev.dataTransfer.setData("application/json", data);
                ev.dataTransfer.setData("text/plain", plain);
                ev.dataTransfer.effectAllowed = "copy";
                ev.stopPropagation();
            });
        }

        /**
         * Make an element a drop target: the dropped text is passed to `onDrop(text, json)`; the element gets class sw-edit-drop while
         * something is dragged over it.
         * @param {HTMLElement} e - The element.
         * @param {function(string, boolean):Promise<void>} onDrop - Called with the dropped json (json true) or plain text.
         */
        static DropTarget(e, onDrop) {
            e.addEventListener("dragover", ev => {
                const t = Array.from(ev.dataTransfer.types || []);
                if (t.indexOf("application/json") < 0 && t.indexOf("text/plain") < 0)
                    return;
                ev.preventDefault();
                ev.stopPropagation();
                ev.dataTransfer.dropEffect = "copy";
                e.classList.add("sw-edit-drop");
            });
            e.addEventListener("dragleave", () => e.classList.remove("sw-edit-drop"));
            e.addEventListener("drop", async ev => {
                e.classList.remove("sw-edit-drop");
                const json = ev.dataTransfer.getData("application/json");
                const text = json || ev.dataTransfer.getData("text/plain");
                if (!text)
                    return;
                ev.preventDefault();
                ev.stopPropagation();
                await onDrop(text, !!json);
            });
        }

        /**
         * Build the member row behaviors (called by the layouts): runs the handler's AddEditor, then wires the title as drag
         * source, drop target (unless read only), member menu (click, right click, long press, context menu key / Shift+F10
         * anywhere in the row) and the optional menu icon.
         * @param {object} obj - The object.
         * @param {Edit} editor - The editor.
         * @param {object} editContext - Layout context {PrevRow, Row, Element, Icon, CreateData()}.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @param {HTMLElement} c - The title element.
         * @returns {Promise<?object>} The item.
         */
        static async SetMember(obj, editor, editContext, member, title, options, c) {
            const th = Edit.GetTypeHandler(member.TypeName, member, options);
            const cell = editContext.Element;
            const had = cell ? new Set(cell.childNodes) : null;
            const res = await th.AddEditor(obj, editor, editContext, member, title, options);
            if (!res)
                return null;
            res.TypeHandler = th;
            res.TitleElement = c;
            const mn = member.Name;
            const readOnly = Edit.IsReadOnly(member, options) || !!th.ForceReadOnly;
            res.ReadOnly = readOnly;
            //  Plug-ins (handlers that are not the editor's own classes) get the framework's defaults: the default value from GetDefault,
            //  a re-render when the item can't refresh itself, and (every handler) the member name on the control
            const plugin = Edit.IsPlugin(th);
            if (plugin && res.DefValue === undefined && typeof th.GetDefault === "function") {
                try {
                    const def = th.GetDefault(member, options);
                    if (def !== undefined) {
                        res.DefValue = def;
                        if (res.DefValueText === undefined || res.DefValueText === null) {
                            let t = typeof th.ToString === "function" ? th.ToString(def, member) : "";
                            if (t === undefined || t === null || t === "")
                                t = def === null ? "null" : (typeof def === "object" ? JSON.stringify(def) : String(def));
                            res.DefValueText = String(t);
                        }
                    }
                }
                catch (x) {
                    console.warn("Edit plug-in \"" + member.Type + "\": GetDefault failed: " + x);
                }
            }
            Edit.NameControl(res.Focus, c, member);
            if (plugin && res.NeedsRender && cell) {
                let owned = Array.from(cell.childNodes).filter(n => !had.has(n));
                res.Rerender = async () => {
                    const tmp = el("div");
                    const r2 = await th.AddEditor(obj, editor, Object.assign({}, editContext, { Element: tmp }), member, title, options);
                    if (!r2)
                        return;
                    const nodes = Array.from(tmp.childNodes);
                    const hadFocus = owned.some(n => n.contains && n.contains(document.activeElement));
                    const at = owned.find(n => n.parentNode === cell);
                    for (const n of nodes)
                        cell.insertBefore(n, at || null);
                    owned.forEach(n => n.remove());
                    owned = nodes;
                    res.Focus = r2.Focus;
                    if (hadFocus && res.Focus && res.Focus.focus)
                        res.Focus.focus();
                    Edit.NameControl(res.Focus, c, member);
                };
            }
            if (readOnly && editContext.Row) {
                editContext.Row.classList.add("sw-edit-ro");
                c.classList.add("EditReadOnly");
            }
            Edit.DragSource(c, () => editor.GetCopyMember(member, true));
            if (!readOnly) {
                Edit.DropTarget(c, async (text, json) => {
                    const r = Edit.ParseValue(text, member, th, options, json);
                    if (!r.Ok) {
                        //  The handler may take other values (a collection appends a dropped item of its element type)
                        if (!res.AcceptText || !await res.AcceptText(text, _T("Dropped: {0}", text, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property.")))
                            Sw.Fail(r.Reason);
                        return;
                    }
                    const old = editor.Object[mn];
                    const v = await editor.KeepMemberReadOnly(member, r.Value);
                    if (!Edit.IsDifferent(old, v))
                        return;
                    await editor.SetMemberValue(member, v, old,
                        _T("Dropped: {0}", typeof r.Value === "object" ? JSON.stringify(r.Value) : r.Value, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property."));
                });
            }
            const open = ev => editor.ShowMemberMenu(res, member, ev);
            c.addEventListener("click", ev => {
                if (Sw.BadClick(ev) || Sw.HasSelectionIn(c))
                    return;
                open(ev);
            });
            Sw.LongPress(c, open);
            const row = editContext.Row || editContext.PrevRow;
            if (row) {
                row.addEventListener("keydown", ev => {
                    if (ev.target.closest(".sw-edit-row") !== row)
                        return;
                    //  Ctrl+C / Ctrl+V (Cmd on Apple) outside text fields (toggles, selects, buttons, the title): copy / paste the
                    //  member value like the menu items (text fields keep the native clipboard keys)
                    const clip = Edit.ClipboardKey(ev);
                    if (clip) {
                        ev.preventDefault();
                        ev.stopPropagation();
                        if (clip === "copy")
                            editor.CopyMember(member);
                        else if (!res.ReadOnly)
                            editor.PasteMember(res, member);
                        else
                            Sw.Fail(_TF("Value is read only", "Tool tip description on a property value that can't be edited"));
                        return;
                    }
                    if (ev.key !== "ContextMenu" && !(ev.key === "F10" && ev.shiftKey))
                        return;
                    ev.preventDefault();
                    ev.stopPropagation();
                    open(ev);
                });
            }
            const hint = _TF("Show options.", "Tool tip description for a button that when pressed will pop-up a menu");
            c.title = (c.title ? c.title + "\n\n" : "") + hint;
            const iconCell = res.Icon || editContext.Icon;
            if (options.MenuIcon && iconCell) {
                const b = new Sw.Button({
                    Icon: options.MenuIconClass === OptionDefaults.MenuIconClass ? "moreV" : options.MenuIconClass, Kind: "ghost", Small: true,
                    Title: hint, Class: "sw-edit-menubtn", OnClick: (x, ev) => open(ev),
                });
                b.E.setAttribute("aria-haspopup", "menu");
                iconCell.append(b.E);
            }
            return res;
        }

        /**
         * Test if a member is read only (options, member flag).
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {boolean} True when read only.
         */
        static IsReadOnly(member, options) {
            return !!(options.ReadOnly || ((member.Flags & Flags.ReadOnly) !== 0));
        }

        /**
         * Test if a handler is a plug-in: not an instance of one of the editor's own handler classes (`Sw.Edit.Handlers`; a subclass of
         * one is a plug-in).
         * @param {object} th - The handler.
         * @returns {boolean} True for a plug-in.
         */
        static IsPlugin(th) {
            if (!th)
                return false;
            const H = Edit.Handlers || {};
            for (const k in H)
                if (typeof H[k] === "function" && th.constructor === H[k])
                    return false;
            return true;
        }

        /**
         * Give a member's control an accessible name when it has none: aria-labelledby the member name (span.sw-edit-name of the
         * label), so plug-in controls are named like the built-in ones.
         * @param {?HTMLElement} f - The control (an item's Focus).
         * @param {?HTMLElement} label - The member label element.
         * @param {object} member - The member.
         */
        static NameControl(f, label, member) {
            if (!(f instanceof Element))
                return;
            if ((f.getAttribute("aria-label") || "").trim() || f.hasAttribute("aria-labelledby") || f.closest("label"))
                return;
            if (f.id && document.querySelector('label[for="' + CSS.escape(f.id) + '"]'))
                return;
            const n = label && (label.querySelector(".sw-edit-name") || label);
            if (n && (n.textContent || "").trim()) {
                if (!n.id)
                    n.id = "sw-edit-name-" + (++Edit.ErrorN);
                f.setAttribute("aria-labelledby", n.id);
            }
            else
                f.setAttribute("aria-label", member.DisplayName || member.Name);
        }

        /**
         * Show the member menu: Set default, Copy, Paste (with a preview of the clipboard), handler items, Copy name
         * (Dev: Copy member name, Copy type name).
         * @param {object} item - The member item.
         * @param {object} member - The member.
         * @param {Event} [ev] - The triggering event (positions the menu).
         * @returns {Promise<void>}
         */
        async ShowMemberMenu(item, member, ev) {
            const editor = this;
            const options = this.Options;
            const mn = member.Name;
            const obj = this.Object;
            const readOnly = item.ReadOnly ?? Edit.IsReadOnly(member, options);
            const isPassword = (member.Flags & Flags.Password) !== 0;
            const [clipText, clipErr] = readOnly ? [null, null] : await readClipboard();
            const items = [];
            //  Set default
            const def = item.DefValue;
            const defText = item.DefValueText ?? (def === undefined ? "" : (typeof def === "object" ? JSON.stringify(def) : String(def)));
            let enabled = def !== undefined && Edit.IsDifferent(obj[mn], def);
            let title = def !== undefined
                ? (enabled
                    ? _T("Reset the value to the default: {0}", defText, "Tool tip description for a menu item that if selected resets a property to the default value.{0} is replaced with the default value of the property.")
                    : _T("Default value is the same: {0}", defText, "Tool tip description for a menu item that if selected resets a property to the default value, displayed when it's disabled cause the property value is already at default.{0} is replaced with the default value of the property."))
                : _TF("This field has no default value.", "Tool tip description for a menu item that if selected resets a property to the default value, displayed when it is disabled because the property has no default value");
            if (readOnly) {
                enabled = false;
                title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
            }
            items.push({
                Text: _TF("Set default", "Text for a menu item that if selected resets a property to the default value"), Icon: "reset", Title: title, Disabled: !enabled,
                Hint: enabled ? Edit.Short(defText) : null,
                OnClick: async () => editor.SetMemberValue(member, await editor.KeepMemberReadOnly(member, def), obj[mn], _T("Set default: {0}", defText, "Command text stored in a log when a property value was reset to it's default.{0} is replaced with the default value of the property.")),
            });
            //  Copy
            const val = editor.GetCopyMember(member);
            items.push(isPassword ? {
                Text: _TF("Copy", "Text for a menu item that if selected copies the value of a property to the clip board"), Icon: "copy", Disabled: true,
                Title: _TF("Can't copy protected data.", "Text for a menu item that if selected copies the value of a property to the clip board, when the option is disabled because the property contains protected content"),
            } : {
                Text: _TF("Copy", "Text for a menu item that if selected copies the value of a property to the clip board"), Icon: "copy",
                Title: _T("Copy \"{0}\" to the clipboard.", val, "Text for a menu item that if selected copies the value of a property to the clip board.{0} is replaced with the value of the property."),
                OnClick: () => Sw.Clipboard.Copy(val),
            });
            //  Paste
            let paste = { Ok: false };
            if (readOnly)
                title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
            else if (clipErr)
                title = _T("Failed to read from clipboard: {0}", clipErr, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when the clip board data couldn't be read.{0} is replaced with the error message.");
            else {
                paste = Edit.ParseValue(clipText, member, item.TypeHandler, options);
                title = paste.Ok
                    ? (paste.Json
                        ? _T("Set the value to the clipboard object: {0}", clipText, "Tool tip description on a menu item that when clicked will set the property value to the complex object value on the clip board.{0} is replaced with a textual representation of the complex object value on the clip board.")
                        : _T("Set the value to the clipboard text: \"{0}\"", paste.Value, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board.{0} is replaced with a textual representation of the value on the clip board."))
                    : paste.Reason;
            }
            items.push({
                Text: _TF("Paste", "Text of a menu item that when clicked will set the property value to the value on the clip board"), Icon: "editPaste", Title: title,
                Hint: paste.Ok ? Edit.Short(paste.Json ? clipText : String(paste.Value)) : null, Disabled: !paste.Ok,
                OnClick: async () => editor.SetMemberValue(member, await editor.KeepMemberReadOnly(member, paste.Value), obj[mn], _T("Paste: {0}", paste.Json ? clipText : paste.Value, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board.")),
            });
            //  Handler and editor items
            const extra = { Items: [] };
            //  A client type's own actions of the member (member MenuItems(obj): Sw.Menu items, ex: "Use the play position")
            if (typeof member.MenuItems === "function" && !readOnly)
                for (const x of (await member.MenuItems(obj)) || [])
                    if (x)
                        extra.Items.push(x);
            if (item.AddMenuItems)
                item.AddMenuItems(extra, () => { });
            if (Edit.IsPrimitive(this.Type) && this.AddMenuItems)
                this.AddMenuItems(extra.Items, () => { });
            if (extra.Items.length) {
                items.push("-");
                extra.Items.forEach(x => items.push(menuItem(x)));
            }
            //  Names
            items.push("-");
            const dn = member.DisplayName ?? mn;
            items.push({
                Text: _TF("Copy name", "Text of a menu item that when clicked will copy the display name of a property to the clip board"), Icon: "copy",
                Title: _T("Copy the name \"{0}\" to the clipboard.", dn, "Tool tip description on a menu item that when clicked will copy the display name of a property to the clip board.{0} is replaced with the display name of the property"),
                OnClick: () => Sw.Clipboard.Copy(dn),
            });
            if (options.Dev) {
                items.push({
                    Text: _TF("Copy member name", "Text of a menu item that when clicked will copy the member name of a property to the clip board"), Icon: "copy",
                    Title: _T("Copy the member name \"{0}\" to the clipboard.", mn, "Tool tip description on a menu item that when clicked will copy the member name of a property to the clip board.{0} is replaced with the member name of the property"),
                    OnClick: () => Sw.Clipboard.Copy(mn),
                });
                items.push({
                    Text: _TF("Copy type name", "Text of a menu item that when clicked will copy the .NET type name of a property to the clip board"), Icon: "copy",
                    Title: _T("Copy the type name \"{0}\" to the clipboard.", member.TypeName, "Tool tip description on a menu item that when clicked will copy the .NET type name of a property to the clip board.{0} is replaced with a .NET type name"),
                    OnClick: () => Sw.Clipboard.Copy(member.TypeName),
                });
            }
            Edit.ShowMenu(ev, item.TitleElement, items, dn);
        }

        /**
         * Which clipboard shortcut a key event is: "copy" (Ctrl+C / Cmd+C) or "paste" (Ctrl+V / Cmd+V) when the focus is not in a
         * text field (inputs, textareas and editable content keep their native clipboard keys); else null.
         * @param {KeyboardEvent} ev - The event.
         * @returns {?string} "copy", "paste" or null.
         */
        static ClipboardKey(ev) {
            if (!(ev.ctrlKey || ev.metaKey) || ev.altKey || ev.shiftKey || !ev.key)
                return null;
            const k = ev.key.toLowerCase();
            if (k !== "c" && k !== "v")
                return null;
            const t = ev.target;
            if (t && (t.tagName === "TEXTAREA" || t.isContentEditable || (t.tagName === "INPUT" && !/^(button|checkbox|radio|range|color|submit|reset)$/i.test(t.type))))
                return null;
            const sel = window.getSelection && window.getSelection();
            if (k === "c" && sel && !sel.isCollapsed && String(sel).trim())
                return null;
            return k === "c" ? "copy" : "paste";
        }

        /**
         * Copy a member value to the clipboard (as the Copy menu item; protected values are not copied).
         * @param {object} member - The member.
         */
        CopyMember(member) {
            if ((member.Flags & Flags.Password) !== 0) {
                Sw.Fail(_TF("Can't copy protected data.", "Text for a menu item that if selected copies the value of a property to the clip board, when the option is disabled because the property contains protected content"));
                return;
            }
            Sw.Clipboard.Copy(this.GetCopyMember(member));
        }

        /**
         * Paste the clipboard into a member (as the Paste menu item, an undoable step); a toast tells why when it can't.
         * @param {object} item - The member item.
         * @param {object} member - The member.
         * @returns {Promise<boolean>} True when the value was changed.
         */
        async PasteMember(item, member) {
            if (item.ReadOnly ?? Edit.IsReadOnly(member, this.Options)) {
                Sw.Fail(_TF("Value is read only", "Tool tip description on a property value that can't be edited"));
                return false;
            }
            const [text, err] = await readClipboard();
            if (err) {
                Sw.Fail(_T("Failed to read from clipboard: {0}", err, "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when the clip board data couldn't be read.{0} is replaced with the error message."));
                return false;
            }
            const r = Edit.ParseValue(text, member, item.TypeHandler || Edit.GetTypeHandler(member.TypeName, member), this.Options);
            if (!r.Ok) {
                if (item.AcceptText && await item.AcceptText(text, _T("Paste: {0}", text, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board.")))
                    return true;
                Sw.Fail(r.Reason);
                return false;
            }
            const old = this.Object[member.Name];
            const v = await this.KeepMemberReadOnly(member, r.Value);
            if (!Edit.IsDifferent(old, v))
                return false;
            await this.SetMemberValue(member, v, old, _T("Paste: {0}", r.Json ? text : r.Value, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board."));
            return true;
        }

        /**
         * Replace the whole value (undoable; used by the title row Paste, drop and keyboard paste).
         * @param {*} newVal - The new value.
         * @param {string} title - Undo title.
         * @returns {Promise<void>}
         */
        async ReplaceObject(newVal, title) {
            const old = Edit.CloneValue(this.GetObject());
            //  Read only members (at any depth) keep their values: a pasted / dropped / typed whole value never changes them (one toast
            //  names what was kept)
            const kept = [];
            newVal = Edit.CloneValue(newVal);
            if (Edit.IsPrimitive(this.Type)) {
                const m = this.Type.Members[0];
                newVal = await Edit.KeepReadOnlyMember(m, newVal, old, this.Options, m.DisplayName || m.Name, kept);
            }
            else if (newVal !== null && typeof newVal === "object")
                newVal = await Edit.KeepReadOnly(this.Type, newVal, (old !== null && typeof old === "object") ? old : await Edit.CreateDefault(this.Type, this.Options), this.Options, "", kept);
            Edit.ReportKept(kept);
            if (!Edit.IsDifferent(old, newVal))
                return;
            if (Edit.IsPrimitive(this.Type)) {
                //  Primitive (and collection) editors: the path of the editor resolves to its only member item
                await this.SetMemberValue(this.Type.Members[0], Edit.CloneValue(newVal), old, title);
                return;
            }
            await this.Invoke(async x => {
                await x.SetObject(Edit.CloneValue(newVal));
                x.NotifyChange();
            }, async x => {
                await x.SetObject(Edit.CloneValue(old));
                x.NotifyChange();
            }, title);
        }

        /**
         * Test if a member can't be changed by the user (whatever the options): the ReadOnly flag, a handler that is always read only
         * (unknown types, Sw.Edit.Handlers.Default), an object member of a read only type or of a type the server doesn't describe.
         * @param {object} member - The member.
         * @returns {Promise<boolean>} True when read only.
         */
        static async IsMemberReadOnly(member) {
            const f = member.Flags || 0;
            if ((f & Flags.ReadOnly) !== 0)
                return true;
            if (Edit.GetTypeHandler(member.TypeName, member).ForceReadOnly)
                return true;
            if ((f & (Flags.IsObject | Flags.Collection | Flags.IsPrimitive)) === Flags.IsObject && !(member.Type && !/^(Hide:|Allow\.)/.test(member.Type))) {
                const t = await Edit.GetType(member.TypeName, member.ElementInst);
                return !t || (t.Flags & Flags.ReadOnly) !== 0;
            }
            return false;
        }

        /**
         * Keep the read only parts of an object value (in place): every read only member of the type gets the value of `oldVal` (at every
         * depth: nested objects, items of collections, read only element types). Used by every path that replaces a whole value (title row
         * Paste / Ctrl+V / drop, Set default, the form footer Reset, explore/api.html's JSON tab) and by the member paste / drop.
         * @param {object} type - The TypeDesc of the value.
         * @param {object} newVal - The new value (changed in place).
         * @param {?object} oldVal - The current value (the read only values come from it; null = the type's default).
         * @param {object} options - Editor options.
         * @param {string} path - Display path of the value ("" = the root).
         * @param {string[]} kept - Gets the display path of each read only member whose new value was not taken.
         * @param {number} [depth] - Recursion depth (limited to 12, self referencing types).
         * @returns {Promise<object>} newVal.
         */
        static async KeepReadOnly(type, newVal, oldVal, options, path, kept, depth) {
            depth = depth || 0;
            if (!type || depth > 12 || newVal === null || typeof newVal !== "object" || Array.isArray(newVal) || newVal instanceof Date)
                return newVal;
            const base = oldVal !== null && typeof oldVal === "object" ? oldVal : null;
            const own = Object.prototype.hasOwnProperty;
            for (const m of type.Members || []) {
                const n = m.Name;
                const p = (path ? path + "." : "") + (m.DisplayName || n);
                const has = own.call(newVal, n);
                const old = base ? base[n] : undefined;
                if (await Edit.IsMemberReadOnly(m)) {
                    const keep = base && own.call(base, n) ? Edit.CloneValue(old) : Edit.GetTypeHandler(m.TypeName, m).CreateDefault(m, options);
                    if (has && Edit.IsDifferent(newVal[n], keep))
                        kept.push(p);
                    newVal[n] = keep;
                    continue;
                }
                if (has)
                    newVal[n] = await Edit.KeepReadOnlyMember(m, newVal[n], old, options, p, kept, depth + 1);
            }
            return newVal;
        }

        /**
         * Keep the read only parts inside the new value of a writable member (nested objects, collection items); see KeepReadOnly.
         * Collections whose element type is read only accept only items that were already in the collection (re-ordered or removed) and
         * new default items; otherwise the old collection is kept.
         * @param {object} member - The member (writable).
         * @param {*} value - The new value (changed in place).
         * @param {*} oldVal - The current value.
         * @param {object} options - Editor options.
         * @param {string} path - Display path of the member.
         * @param {string[]} kept - Gets the paths of the kept read only values.
         * @param {number} [depth] - Recursion depth.
         * @returns {Promise<*>} The value to use.
         */
        static async KeepReadOnlyMember(member, value, oldVal, options, path, kept, depth) {
            depth = depth || 0;
            if (value === null || typeof value !== "object" || depth > 12 || value instanceof Date)
                return value;
            const f = member.Flags || 0;
            if ((f & Flags.Collection) !== 0) {
                const CH = Edit.Handlers.Collection;
                const et = CH && CH.ItemType ? await CH.ItemType(member, null) : null;
                if (!et)
                    return value;
                const prim = Edit.IsPrimitive(et);
                const isMap = !Array.isArray(value);
                const oldC = oldVal !== null && typeof oldVal === "object" ? oldVal : (isMap ? {} : []);
                const oldOf = k => oldC instanceof Map ? oldC.get(k) : oldC[k];
                const keys = value instanceof Map ? Array.from(value.keys()) : Object.keys(value);
                const get = k => value instanceof Map ? value.get(k) : value[k];
                const set = (k, v) => value instanceof Map ? value.set(k, v) : (value[k] = v);
                const pm = prim && et.Members ? et.Members[0] : null;
                const itemsRo = (et.Flags & Flags.ReadOnly) !== 0 || (pm && await Edit.IsMemberReadOnly(pm));
                if (itemsRo) {
                    //  Items may be removed, re-ordered or added as new default items, but not changed
                    const pool = (oldC instanceof Map ? Array.from(oldC.values()) : Object.values(oldC)).map(x => JSON.stringify(x));
                    let def;
                    let ok = true;
                    for (const k of keys) {
                        const j = JSON.stringify(get(k));
                        if (isMap && oldOf(k) !== undefined) {
                            if (JSON.stringify(oldOf(k)) !== j)
                                ok = false;
                            continue;
                        }
                        const i = isMap ? -1 : pool.indexOf(j);
                        if (i >= 0) {
                            pool.splice(i, 1);
                            continue;
                        }
                        if (def === undefined)
                            def = JSON.stringify(await Edit.CreateDefault(et, options));
                        if (j !== def)
                            ok = false;
                    }
                    if (ok)
                        return value;
                    kept.push(path);
                    return Edit.CloneValue(oldVal === undefined ? (isMap ? {} : []) : oldVal);
                }
                if (prim)
                    return value;
                for (const k of keys) {
                    const v = get(k);
                    if (v === null || typeof v !== "object")
                        continue;
                    let ov = oldOf(k);
                    if (ov === null || typeof ov !== "object")
                        ov = await Edit.CreateDefault(et, options);
                    set(k, await Edit.KeepReadOnly(et, Edit.StripType(v, et), ov, options, path + "[" + k + "]", kept, depth + 1));
                }
                return value;
            }
            if ((f & Flags.IsObject) !== 0 && !Edit.IsPrimitive(member) && !Array.isArray(value)) {
                const t = await Edit.GetType(member.TypeName, member.ElementInst);
                if (!t)
                    return value;
                const base = oldVal !== null && typeof oldVal === "object" ? oldVal : await Edit.CreateDefault(t, options);
                return await Edit.KeepReadOnly(t, Edit.StripType(value, t), base, options, path, kept, depth + 1);
            }
            return value;
        }

        /**
         * Make a new item (a pasted / dropped item of a collection) keep the read only parts of its type: a read only element type gives
         * the default item, an object type gets the default values of its read only members. The `$type` marker of copied objects is
         * removed (the element type is fixed).
         * @param {object} type - The element TypeDesc (Sw.Edit.Handlers.Collection.ItemType).
         * @param {*} value - The new item.
         * @param {object} options - Editor options.
         * @param {string} path - Display path (toast text).
         * @param {string[]} kept - Gets the paths of the kept read only values.
         * @returns {Promise<*>} The item to add.
         */
        static async KeepReadOnlyItem(type, value, options, path, kept) {
            if (!type)
                return value;
            const prim = Edit.IsPrimitive(type);
            const pm = prim && type.Members ? type.Members[0] : null;
            if ((type.Flags & Flags.ReadOnly) !== 0 || (pm && await Edit.IsMemberReadOnly(pm))) {
                const def = await Edit.CreateDefault(type, options);
                if (Edit.IsDifferent(value, def))
                    kept.push(path);
                return def;
            }
            if (prim || value === null || typeof value !== "object")
                return value;
            return await Edit.KeepReadOnly(type, Edit.StripType(value, type), await Edit.CreateDefault(type, options), options, path, kept);
        }

        /**
         * Remove the `$type` marker of a copied object when it names the expected type (it is implied there).
         * @param {*} value - The value (changed in place).
         * @param {object} type - The expected TypeDesc.
         * @returns {*} value.
         */
        static StripType(value, type) {
            if (value && typeof value === "object" && value.$type && type && value.$type.split(",")[0] === type.TypeName)
                delete value.$type;
            return value;
        }

        /**
         * Tell the user (one toast) which read only values were kept by a paste / drop / json edit.
         * @param {string[]} kept - The display paths (KeepReadOnly).
         */
        static ReportKept(kept) {
            if (!kept || !kept.length)
                return;
            const names = Array.from(new Set(kept));
            const list = names.slice(0, 5).join(", ") + (names.length > 5 ? ", ..." : "");
            Sw.Toast(names.length === 1
                ? _T("\"{0}\" is read only and kept its value.", list, "Toast after a paste, drop or json edit that tried to change a read only value; the rest of the value was taken.{0} is replaced with the name of the read only member")
                : _T("{0} read only values kept their values: {1}.", names.length, list, "Toast after a paste, drop or json edit that tried to change read only values; the rest of the value was taken.{0} is the number of values.{1} is the list of their names"),
            { Kind: "warning" });
        }

        /**
         * Keep the read only parts inside a new value of a member of this editor (member paste, drop, Set default); shows the toast.
         * @param {object} member - The member (writable).
         * @param {*} value - The new value.
         * @returns {Promise<*>} The value to use (a copy).
         */
        async KeepMemberReadOnly(member, value) {
            const kept = [];
            const cur = Edit.IsPrimitive(this.Type) ? this.GetObject() : (this.Object ? this.Object[member.Name] : undefined);
            const v = await Edit.KeepReadOnlyMember(member, Edit.CloneValue(value), cur, this.Options, member.DisplayName || member.Name, kept);
            Edit.ReportKept(kept);
            return v;
        }

        /**
         * Paste the clipboard as the whole value (title row Ctrl+V); a toast tells why when it can't. When the page can't read the
         * clipboard (no permission, not a secure context, a browser without clipboard reading) the value is pasted in a dialog instead
         * (PasteObjectDialog), so the whole value can always be replaced.
         * @returns {Promise<boolean>} True when replaced.
         */
        async PasteObject() {
            if (this.IsObjectReadOnly()) {
                Sw.Fail(_TF("Value is read only", "Tool tip description on a property value that can't be edited"));
                return false;
            }
            const [text, err] = await readClipboard();
            if (err)
                return this.PasteObjectDialog();
            const r = this.ParseObject(text);
            if (!r.Ok) {
                Sw.Fail(r.Reason);
                return false;
            }
            await this.ReplaceObject(r.Value, _T("Paste: {0}", text, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board."));
            return true;
        }

        /**
         * Replace the whole value with a text pasted (or typed) in a dialog: JSON for objects and collections, the value text for a primitive.
         * The dialog checks the text before it closes (the same check as Paste).
         * @param {string} [text] - The initial text (ex: a clipboard text that is not a valid value, to be fixed).
         * @returns {Promise<boolean>} True when replaced.
         */
        async PasteObjectDialog(text) {
            if (this.IsObjectReadOnly()) {
                Sw.Fail(_TF("Value is read only", "Tool tip description on a property value that can't be edited"));
                return false;
            }
            const prim = Edit.IsPrimitive(this.Type);
            const v = await Sw.Dialog.Prompt(
                _TF("Paste a value", "Title of a dialog where the user pastes a new value for a whole property (when the page can't read the clipboard)"),
                text || "", prim ? "" : "{ }", {
                    Text: prim
                        ? _TF("Paste (Ctrl+V) or type the new value.", "Text of the paste dialog of a property value (a single value)")
                        : _TF("Paste (Ctrl+V) or type the new value as JSON text. It replaces the whole value.", "Text of the paste dialog of a property value (an object or a list, as JSON)"),
                    Label: _TF("New value", "Accessible name of the text box of the paste dialog of a property value"),
                    Multiline: !prim, Rows: prim ? undefined : 10, Mono: true,
                    OkText: _TF("Replace", "Button of the paste dialog of a property value that replaces the value with the pasted text"),
                    Validate: x => {
                        const r = this.ParseObject(x);
                        return r.Ok ? null : r.Reason;
                    },
                });
            if (v === null || v === undefined)
                return false;
            const r = this.ParseObject(v);
            if (!r.Ok) {
                Sw.Fail(r.Reason);
                return false;
            }
            await this.ReplaceObject(r.Value, _T("Paste: {0}", v, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board."));
            return true;
        }

        /**
         * Show the object (title row) menu: Set default, Copy, Paste and editor supplied items (collection items).
         * @param {Event} [ev] - The triggering event.
         * @param {HTMLElement} anchor - Anchor element.
         * @returns {Promise<void>}
         */
        async ShowObjectMenu(ev, anchor) {
            const edit = this;
            const type = this.Type;
            const readOnly = this.IsObjectReadOnly();
            const [clipText, clipErr] = readOnly ? [null, null] : await readClipboard();
            const val = this.GetCopyObject();
            const replace = (newVal, title) => edit.ReplaceObject(newVal, title);
            const items = [{
                Text: _TF("Set default", "Text for a menu item that if selected resets a property to the default value"), Icon: "reset", Disabled: readOnly,
                Title: readOnly ? _TF("Value is read only", "Tool tip description on a property value that can't be edited") : _TF("Reset the value to the default", "Tool tip description for a menu item that if selected resets a property to the default value."),
                OnClick: async () => replace(await Edit.CreateDefault(type, edit.Options), _TF("Set default", "Text for a menu item that if selected resets a property to the default value")),
            }, {
                Text: _TF("Copy", "Text for a menu item that if selected copies the value of a property to the clip board"), Icon: "copy",
                Title: _T("Copy \"{0}\" to the clipboard.", val, "Text for a menu item that if selected copies the value of a property to the clip board.{0} is replaced with the value of the property."),
                OnClick: () => Sw.Clipboard.Copy(val),
            }];
            let paste = { Ok: false };
            let title;
            if (readOnly)
                title = _TF("Value is read only", "Tool tip description on a property value that can't be edited");
            else if (clipErr)
                //  The page can't read the clipboard (no permission yet, not a secure context ...): Paste tries again on the click (a user
                //  gesture) and else opens the paste dialog, so the whole value can always be replaced
                title = _TF("Paste a new value for the whole object (in a dialog when the clipboard can't be read here)", "Tool tip description on a menu item that replaces a whole property value with a pasted value, when the page can't read the clip board directly");
            else {
                paste = this.ParseObject(clipText);
                title = paste.Ok ? _T("Set the value to the clipboard object: {0}", clipText, "Tool tip description on a menu item that when clicked will set the property value to the complex object value on the clip board.{0} is replaced with a textual representation of the complex object value on the clip board.") : paste.Reason + "\n" + _TF("Click to paste the value in a dialog.", "Second line of the tool tip of the Paste menu item of a property value when the clip board has no valid value: a click opens the paste dialog");
            }
            items.push({
                Text: paste.Ok || readOnly
                    ? _TF("Paste", "Text of a menu item that when clicked will set the property value to the value on the clip board")
                    : _TF("Paste...", "Text of a menu item that opens a dialog where the user pastes a new value for a property (when the clip board can't be read directly or has no valid value)"),
                Icon: "editPaste", Title: title,
                Disabled: readOnly, Hint: paste.Ok ? Edit.Short(clipText) : null,
                OnClick: () => paste.Ok
                    ? replace(paste.Value, _T("Paste: {0}", clipText, "Command text stored in a log when a property value was replaced with the value on the clip board.{0} is replaced with the value on the clip board."))
                    : clipErr ? edit.PasteObject() : edit.PasteObjectDialog(clipText),
            });
            if (this.AddMenuItems) {
                const extra = [];
                this.AddMenuItems(extra, () => { });
                if (extra.length) {
                    items.push("-");
                    extra.forEach(x => items.push(menuItem(x)));
                }
            }
            Edit.ShowMenu(ev, anchor, items, this.Options.KeyName || type.DisplayName);
        }

        /**
         * Interpret a text as a replacement of the whole value.
         * @param {?string} text - The text.
         * @param {boolean} [fromJson] - The text is json (a drag of an editor value): a string value is a json string.
         * @returns {{Ok: boolean, Value: *, Reason: ?string}} The result.
         */
        ParseObject(text, fromJson) {
            const type = this.Type;
            if (Edit.IsPrimitive(type)) {
                const m = type.Members[0];
                return Edit.ParseValue(text, m, Edit.GetTypeHandler(m.TypeName, m), this.Options, fromJson);
            }
            const bad = _TF("Not a valid object on the clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no valid value on the clipboard.");
            if (!text)
                return { Ok: false, Reason: _TF("No known data on clipboard", "Tool tip description on a menu item that when clicked will set the property value to the value on the clip board, displayed when there is no known data found on the clipboard.") };
            let obj;
            try {
                obj = JSON.parse(text);
            }
            catch {
                return { Ok: false, Reason: bad };
            }
            if (!this.CanConsumeCopy(obj))
                return { Ok: false, Reason: bad };
            return { Ok: true, Value: obj };
        }

        /**
         * Show a menu next to an element (or at the pointer for right clicks / long presses).
         * @param {?Event} ev - The event.
         * @param {HTMLElement} anchor - Fallback anchor.
         * @param {Array} items - Sw.Menu items.
         * @param {string} [label] - Accessible label.
         */
        static ShowMenu(ev, anchor, items, label) {
            const pointer = ev && ev.clientX !== undefined && (ev.clientX || ev.clientY) && ev.type !== "click";
            Sw.Menu.Show(pointer ? { X: ev.clientX, Y: ev.clientY } : anchor, items, { Label: label || null, Class: "sw-edit-menu" });
        }

        /**
         * Shorten a text for a menu hint.
         * @param {string} s - The text.
         * @returns {string} At most 24 characters.
         */
        static Short(s) {
            s = String(s ?? "").replace(/\s+/g, " ");
            return s.length > 24 ? s.substring(0, 21) + "..." : s;
        }

        /**
         * Deep clone a json value (objects only; primitives returned as is).
         * @param {*} v - The value.
         * @returns {*} The clone.
         */
        static CloneValue(v) {
            if (v === null || typeof v !== "object")
                return v;
            if (v instanceof Date)
                return new Date(v.getTime());
            if (v instanceof Map)
                return new Map(JSON.parse(JSON.stringify(Array.from(v.entries()))));
            return JSON.parse(JSON.stringify(v));
        }

        /**
         * Fill the title row (called by the layout's AddTypeInfo): expand / collapse button, menu button, the title (copy on
         * click), header extras (options.AddToHeader); drag source and drop target for the whole value.
         * @param {HTMLElement} c - The title row element.
         * @param {HTMLElement} expandElement - Unused (the editor element gets class EditorCollapsed / sw-edit-collapsed).
         * @param {object} type - The type.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         */
        SetTypeInfo(c, expandElement, type, title, options) {
            const edit = this;
            const e = this.Element;
            const name = options.KeyName || type.DisplayName || type.TypeName || "";
            if (options.CanExpand && !Edit.IsPrimitive(type)) {
                let expanded = !!options.IsExpanded;
                const b = new Sw.Button({ Kind: "ghost", Small: true, Class: "sw-edit-expand", Icon: "down" });
                const set = () => {
                    e.classList.toggle("sw-edit-collapsed", !expanded);
                    e.classList.toggle("EditorCollapsed", !expanded);
                    b.SetIcon(expanded ? "down" : "right");
                    b.SetTitle(expanded
                        ? _TF("Hide object members", "Tool tip description on a button that when clicked will hide the list of object members")
                        : _TF("Show object members", "Tool tip description on a button that when clicked will display a list of object members"));
                    b.E.setAttribute("aria-expanded", expanded ? "true" : "false");
                };
                b.O.OnClick = () => {
                    expanded = !expanded;
                    set();
                };
                set();
                c.append(b.E);
            }
            const menu = new Sw.Button({
                Icon: "moreV", Kind: "ghost", Small: true, Class: "sw-edit-menubtn",
                Title: _TF("Show options.", "Tool tip description for a button that when pressed will pop-up a menu"),
                OnClick: (b, ev) => edit.ShowObjectMenu(ev, menu.E),
            });
            menu.E.setAttribute("aria-haspopup", "menu");
            c.append(menu.E);
            const text = el("span.sw-edit-typetitle", { text: name });
            text.BaseTitle = title;
            const hint = Sw.Clipboard.CopyOnClick(text, null);
            text.title = (title ? title + "\n\n" : "") + hint;
            c.append(text);
            if (options.Summary && !Edit.IsPrimitive(type)) {
                //  A one line summary after the title (collection items: "[3] Afar · aa · Afar"), live updated
                const sum = el("span.sw-edit-summary");
                /** Refresh the summary of the title row. */
                edit.UpdateSummary = () => {
                    const s = Edit.Summary(edit.GetObject(), edit.Type);
                    sum.textContent = s;
                    sum.title = s;
                };
                e.addEventListener("EditChange", () => edit.UpdateSummary());
                c.append(sum);
            }
            const extra = el("span.sw-edit-headextra");
            c.append(extra);
            if (options.AddToHeader)
                options.AddToHeader(extra);
            Sw.LongPress(c, ev => edit.ShowObjectMenu(ev, menu.E));
            //  Keyboard on the title row buttons: Ctrl+C / Ctrl+V copy / paste the whole value, the context menu key / Shift+F10 the menu
            c.addEventListener("keydown", ev => {
                const clip = Edit.ClipboardKey(ev);
                if (clip) {
                    ev.preventDefault();
                    ev.stopPropagation();
                    if (clip === "copy")
                        Sw.Clipboard.Copy(edit.GetCopyObject());
                    else
                        edit.PasteObject();
                    return;
                }
                if (ev.key === "ContextMenu" || (ev.key === "F10" && ev.shiftKey)) {
                    ev.preventDefault();
                    ev.stopPropagation();
                    edit.ShowObjectMenu(null, menu.E);
                }
            });
            Edit.DragSource(c, () => edit.GetCopyObject(true));
            if (!edit.IsObjectReadOnly()) {
                Edit.DropTarget(c, async (json, isJson) => {
                    const r = edit.ParseObject(json, isJson);
                    if (!r.Ok) {
                        Sw.Fail(r.Reason);
                        return;
                    }
                    await edit.ReplaceObject(r.Value, _T("Dropped: {0}", json, "Command text stored in a log when a property value was replaced by drag and drop.{0} is replaced with the value that was dragged onto the property."));
                });
            }
        }

        // ------------------------------------------------------------------ summaries

        /**
         * Show a short summary of a value (object members): "null", strings / numbers as is, objects as a one line summary (Sw.Edit.Summary, else JSON without $type; ellipsis by css)
         * (full json in the tooltip).
         * @param {HTMLElement} element - Target.
         * @param {*} obj - The value.
         * @param {?object} [type] - The TypeDesc of an object (member order of the summary).
         */
        static SetToString(element, obj, type) {
            element.classList.toggle("sw-edit-nulltoken", obj === null || obj === undefined);
            if (obj === null || obj === undefined) {
                element.textContent = "null";
                element.title = _TF("No object available", "Tool tip description of a property value input box when no object is available");
                return;
            }
            const ot = typeof obj;
            if (ot === "string") {
                element.textContent = obj;
                element.title = _T("The text is: \"{0}\"", obj, "Tool tip description of a property text value.{0} is replaced with the value text.");
                return;
            }
            if (ot === "number") {
                element.textContent = "" + obj;
                element.title = _T("The number is: {0}", obj, "Tool tip description of a property numerical value.{0} is replaced with the numerical value.");
                return;
            }
            if (ot !== "object") {
                element.textContent = "" + obj;
                element.title = _T("The value is: {0}", obj, "Tool tip description of a property value.{0} is replaced with the value.");
                return;
            }
            let text = null;
            try {
                if (typeof obj.ToString === "function")
                    text = obj.ToString();
            }
            catch {
            }
            let json = "";
            try {
                const c = Object.assign({}, obj);
                delete c.$type;
                json = JSON.stringify(c);
            }
            catch {
            }
            //  A one line summary (Design v2: "Fixed · 25"), else the json; the tool tip has the json
            if (!text)
                text = Edit.Summary(obj, type) || json;
            text = text || _TF("Value", "Shown in an object editor in place of a value that has no text or summary of its own");
            element.textContent = text;
            element.title = json && json !== text ? json : "";
        }

        /**
         * Show the item count of a collection ("null", "empty", "1 item", "{n} items").
         * @param {HTMLElement} element - Target.
         * @param {?(Array|Map|object)} collection - The collection.
         * @param {object} container - The collection strategy (countVal).
         */
        static SetCollectionCount(element, collection, container) {
            element.classList.toggle("sw-edit-nulltoken", collection === null || collection === undefined);
            if (collection === null || collection === undefined) {
                element.textContent = "null";
                element.title = _TF("The collection is non existent", "Tool tip description on a property value that is a .NET collection when there is no collection (it's null)");
                return;
            }
            const c = container.countVal(collection);
            if (c <= 0) {
                element.textContent = _TF("empty", "Text used to indicate that a list of items is empty");
                element.title = _TF("The collection is empty", "Tool tip description on a property value that is a .NET collection when the collection is empty");
                return;
            }
            if (c === 1) {
                element.textContent = _TF("1 item", "Text used to indicate that a list contains a single item");
                element.title = _TF("There is one item in the collection", "Tool tip description on a property value that is a .NET collection when the collection contains a single item");
                return;
            }
            element.textContent = _T("{0} items", c, "Text used to indicate that a list contains multiple items.{0} is replaced with the number of items.");
            element.title = _T("There are {0} items in the collection", c, "Tool tip description on a property value that is a .NET collection when the collection contains multiple items.{0} is replaced with the number of items in the collection.");
        }

        // ------------------------------------------------------------------ inputs (helpers for handlers)

        /**
         * Bind live / final change handling, Escape revert and coalescing tokens to an input. Typing applies temporary
         * changes on input; change (blur / Enter) finalizes and reformats; each focus session gets its own coalescing token,
         * so continuous typing is one undo step.
         * @param {HTMLInputElement|HTMLTextAreaElement} i - The input.
         * @param {function():*} getValue - Current value.
         * @param {?function(*, Event, *):Promise<void>} setValue - Store a value (third argument: the coalescing token).
         * @param {function(string):*} parseValue - Parse the text.
         * @param {function(*):string} toString - Format a value.
         * @param {?function(*):*} validateValue - Coerce / clamp.
         */
        static BindInput(i, getValue, setValue, parseValue, toString, validateValue) {
            let internal = false;
            let orgVal = getValue();
            let token = null;
            i.addEventListener("focus", () => {
                orgVal = getValue();
                token = {};
            });
            const onChange = async (ev, isTemp) => {
                if (internal || !setValue)
                    return;
                internal = true;
                try {
                    let v = parseValue(i.value);
                    if (validateValue)
                        v = validateValue(v);
                    if (!isTemp) {
                        const s = toString(v);
                        if (s != i.value)
                            i.value = s;
                    }
                    await setValue(v, ev, token || (token = {}));
                }
                finally {
                    internal = false;
                }
            };
            i.addEventListener("change", ev => onChange(ev, false));
            i.addEventListener("input", ev => onChange(ev, true));
            i.addEventListener("keydown", ev => {
                if (ev.key === "Escape" && Sw.IsPureEvent(ev)) {
                    const s = toString(orgVal);
                    if (i.value === s)
                        return;
                    ev.stopPropagation();
                    ev.preventDefault();
                    i.value = s;
                    onChange(ev, false);
                }
                else if (ev.key === "Enter" && i.tagName === "INPUT")
                    onChange(ev, false);
            });
            i.addEventListener("blur", () => token = null);
            /** Reload the text from the value (skipped while a change is processed). */
            i.UpdateValue = () => {
                if (internal)
                    return;
                internal = true;
                const s = toString(getValue());
                if (i.value !== s)
                    i.value = s;
                internal = false;
            };
        }

        /**
         * The display of a read only text value: plain selectable text (no input, so no caret, typing, paste or drop can change it),
         * focusable for keyboard users (copy with Ctrl+C / the member menu). Duck typed like an input for the handlers: `value`,
         * `readOnly` (true), `placeholder` (shown when empty), `type` ("password" masks it), `maxLength`, `UpdateValue()`.
         * @param {function():*} getValue - Current value.
         * @param {function(*):string} toString - Format.
         * @param {boolean} [multi] - Keep line breaks.
         * @returns {HTMLElement} The element (span.sw-edit-rotext).
         */
        static ReadOnlyText(getValue, toString, multi) {
            const s = el("span.sw-edit-rotext.sw-edit-input.EditReadOnly", { tabIndex: 0, attrs: { role: "textbox", "aria-readonly": "true" } });
            if (multi)
                s.classList.add("sw-edit-rotext-multi");
            let text = "";
            let ph = "";
            const show = () => {
                s.textContent = text;
                s.classList.toggle("sw-edit-rotext-empty", !text);
                //  An empty value shows a quiet "empty" token (Design v2: like the null token, never a blank)
                s.classList.toggle("sw-edit-rotext-blank", !text && !ph);
                if (!text)
                    s.dataset.placeholder = ph || Edit.EmptyText();
                else
                    delete s.dataset.placeholder;
            };
            Object.defineProperty(s, "value", { get: () => text, set: v => { text = v === null || v === undefined ? "" : String(v); show(); } });
            Object.defineProperty(s, "placeholder", { get: () => ph, set: v => { ph = v || ""; show(); } });
            Object.defineProperty(s, "readOnly", { get: () => true, set: () => { } });
            Object.defineProperty(s, "type", { get: () => s.classList.contains("sw-edit-masked") ? "password" : "text", set: v => s.classList.toggle("sw-edit-masked", v === "password") });
            s.maxLength = 0;
            s.UpdateValue = () => s.value = toString(getValue());
            s.UpdateValue();
            return s;
        }

        /**
         * Create an input of a given type bound to a value (date, time ...).
         * @param {string} inputType - Input type.
         * @param {function():*} getValue - Current value.
         * @param {?function(*, Event, *):Promise<void>} setValue - Store a value.
         * @param {function(string):*} parseValue - Parse.
         * @param {function(*):string} toString - Format.
         * @param {?function(*):*} validateValue - Coerce.
         * @param {boolean} readOnly - Read only.
         * @returns {HTMLInputElement} The input (with UpdateValue).
         */
        static CreateGenericInput(inputType, getValue, setValue, parseValue, toString, validateValue, readOnly) {
            if (readOnly)
                return Edit.ReadOnlyText(getValue, toString, false);
            const i = el("input.sw-input.sw-edit-input", { type: inputType });
            i.value = toString(getValue());
            if (readOnly) {
                i.readOnly = true;
                i.classList.add("EditReadOnly");
            }
            Edit.BindInput(i, getValue, readOnly ? null : setValue, parseValue, toString, validateValue);
            return i;
        }

        /**
         * Create a text input (or a textarea for more than one line) bound to a value, with optional character filtering
         * (disallowed characters are dropped on beforeinput).
         * @param {function():*} getValue - Current value.
         * @param {?function(*, Event, *):Promise<void>} setValue - Store a value.
         * @param {?function(string):*} parseValue - Parse.
         * @param {function(*):string} toString - Format.
         * @param {?string} placeHolder - Placeholder (first line used).
         * @param {?function(*):*} validateValue - Coerce.
         * @param {?(string|function(string, number, string):boolean)} validateKeyOrPattern - Allowed characters.
         * @param {?number} maxLength - Maximum length.
         * @param {*} def - Unused (kept for the signature).
         * @param {boolean} readOnly - Read only.
         * @param {?number} numberOfLines - More than 1 creates a textarea.
         * @returns {HTMLInputElement|HTMLTextAreaElement} The element (with UpdateValue).
         */
        static CreateTextInput(getValue, setValue, parseValue, toString, placeHolder, validateValue, validateKeyOrPattern, maxLength, def, readOnly, numberOfLines) {
            const multi = numberOfLines && numberOfLines > 1;
            if (readOnly)
                return Edit.ReadOnlyText(getValue, toString, multi);
            const i = multi ? el("textarea.sw-input.sw-edit-input.sw-edit-multiline", { rows: numberOfLines }) : el("input.sw-input.sw-edit-input", { type: "text" });
            i.spellcheck = false;
            if (maxLength)
                i.maxLength = maxLength;
            if (readOnly) {
                i.readOnly = true;
                i.classList.add("EditReadOnly");
            }
            else if (placeHolder)
                i.placeholder = placeHolder.split("\n")[0];
            let filter = validateKeyOrPattern;
            if (typeof filter === "string") {
                const valid = new Set(filter.split(""));
                filter = ch => valid.has(ch);
            }
            if (filter && !readOnly) {
                i.addEventListener("beforeinput", ev => {
                    if (!ev.data)
                        return;
                    const t = "" + ev.data;
                    const s = i.selectionStart, e = i.selectionEnd, val = i.value;
                    let o = "";
                    for (let k = 0; k < t.length; ++k)
                        if (filter(t.charAt(k), k + s, val))
                            o += t.charAt(k);
                    if (o === t)
                        return;
                    ev.preventDefault();
                    if (!o.length)
                        return;
                    let next = val.substring(0, s) + o + val.substring(e);
                    if (maxLength && next.length > maxLength)
                        next = next.substring(0, maxLength);
                    i.value = next;
                    i.setSelectionRange(s + o.length, s + o.length);
                    i.dispatchEvent(new Event("input"));
                });
            }
            i.value = toString(getValue());
            Edit.BindInput(i, getValue, readOnly ? null : setValue, parseValue || (v => v), toString, validateValue);
            return i;
        }

        /**
         * Create a slider (Sw.Slider) over the steps 0..stepCount (kept for plug-ins; the built in handlers use Sw.Slider).
         * @param {function():number} getStep - Current step.
         * @param {function(string):Promise<void>} setStep - Store a step (as string, like the old range input value).
         * @param {number} stepCount - Maximum step.
         * @param {boolean} readOnly - Disabled.
         * @returns {HTMLElement} The slider element (with UpdateValue, Slider).
         */
        static CreateSliderInput(getStep, setStep, stepCount, readOnly) {
            const s = new Sw.Slider({ Min: 0, Max: stepCount, Step: 1, Value: getStep(), Disabled: readOnly, OnInput: v => setStep("" + v), OnChange: v => setStep("" + v) });
            s.E.Slider = s;
            s.E.UpdateValue = () => s.Set(getStep(), true, true);
            return s.E;
        }

        /**
         * Create a selection bound to a value: a Sw.Select (single value), or toggle chips for bit flags.
         * @param {function():*} getValue - Current value.
         * @param {function(*):Promise<void>} setValue - Store a value.
         * @param {Array<Array>} values - `[value, text, title]` entries.
         * @param {*} def - Unused.
         * @param {boolean} readOnly - Read only.
         * @param {boolean} flags - Bit flags (chips, values are merged bitwise).
         * @param {?function(Array<{KeyValue: Array, disabled: boolean}>):void} onOpenFn - Called before the list opens (or when
         * the flags get the focus / pointer); may set `disabled` on the entries.
         * @returns {HTMLElement} The element (with UpdateValue).
         */
        static CreateSelectionInput(getValue, setValue, values, def, readOnly, flags, onOpenFn) {
            if (readOnly) {
                //  Read only (Design v2, PORTING.md B14): the value as plain text, no select box or chips that look interactive.
                //  Flags: the exact named value ("None", "All"), else the names of the set single bits joined by ", ".
                const text = v => {
                    const exact = values.find(x => x[0] == v);
                    if (exact || !flags)
                        return exact ? "" + exact[1] : (v === null || v === undefined ? "" : "" + v);
                    const n = v | 0;
                    const parts = values.filter(x => { const b = x[0] | 0; return b !== 0 && (b & (b - 1)) === 0 && (n & b) === b; }).map(x => x[1]);
                    return parts.length ? parts.join(", ") : "" + n;
                };
                const ro = Edit.ReadOnlyText(getValue, text, false);
                ro.classList.add("sw-edit-select", "sw-edit-roselect");
                const tip = () => {
                    const kv = values.find(x => x[0] == getValue());
                    ro.title = kv && kv[2] ? kv[2] : "";
                };
                const upd = ro.UpdateValue;
                ro.UpdateValue = () => {
                    upd();
                    tip();
                };
                tip();
                return ro;
            }
            const opts = values.map(kv => ({ KeyValue: kv, disabled: false }));
            const refresh = () => {
                opts.forEach(o => o.disabled = false);
                if (onOpenFn)
                    onOpenFn(opts);
            };
            if (!flags) {
                const toOpts = () => opts.map(o => ({ Value: o.KeyValue[0], Label: o.KeyValue[1], Title: o.KeyValue[2] || null, Disabled: o.disabled }));
                const sel = new Sw.Select({ Options: toOpts(), Value: getValue(), Disabled: readOnly, OnChange: v => setValue(v) });
                const orgOpen = sel.Open.bind(sel);
                sel.Open = () => {
                    refresh();
                    sel.SetOptions(toOpts());
                    orgOpen();
                };
                sel.E.classList.add("sw-edit-select");
                if (readOnly)
                    sel.E.classList.add("EditReadOnly");
                const sync = () => {
                    const v = getValue();
                    const kv = values.find(x => x[0] == v);
                    sel.Set(kv ? kv[0] : v, true);
                    sel.E.title = kv && kv[2] ? kv[2] : "";
                };
                sync();
                sel.E.UpdateValue = sync;
                sel.E.Select = sel;
                return sel.E;
            }
            const wrap = el("div.sw-edit-flags", { attrs: { role: "group" } });
            const chips = [];
            const sync = () => {
                const val = getValue() | 0;
                for (const c of chips) {
                    const ev = c.KeyValue[0] | 0;
                    c.Chip.Set(ev === 0 ? val === 0 : (val & ev) === ev, true);
                }
            };
            for (const o of opts) {
                const kv = o.KeyValue;
                const chip = new Sw.Chip({
                    Text: kv[1], Title: kv[2] || null, Pressable: !readOnly,
                    OnChange: on => {
                        let val = getValue() | 0;
                        const ev = kv[0] | 0;
                        if (ev === 0)
                            val = 0;
                        else if (on)
                            val |= ev;
                        else
                            val &= ~ev;
                        sync();
                        setValue(val);
                    },
                });
                if (readOnly) {
                    chip.Pressable = true;
                    chip.SetDisabled(true, _TF("This value is read only", "Reason shown when trying to toggle a flag chip of a read only flags (enum) value in the object editor"));
                }
                chips.push({ Chip: chip, KeyValue: kv, Opt: o });
                wrap.append(chip.E);
            }
            if (onOpenFn && !readOnly) {
                const apply = () => {
                    refresh();
                    chips.forEach(c => c.Chip.SetDisabled(c.Opt.disabled, _TF("This flag can't be combined with the current value", "Reason shown when trying to toggle a flag chip (of a flags enum value in the object editor) that the current value doesn't allow")));
                };
                wrap.addEventListener("focusin", apply);
                wrap.addEventListener("pointerenter", apply);
            }
            sync();
            wrap.UpdateValue = sync;
            wrap.focus = () => {
                const b = wrap.querySelector("button:not(:disabled, [aria-disabled=\"true\"])");
                if (b)
                    b.focus();
            };
            return wrap;
        }

        // ------------------------------------------------------------------ handler registry

        /**
         * Register a handler for a .NET type name (overrides the built in one).
         * @param {string} typeName - .NET type name.
         * @param {object} handler - The handler.
         */
        static Register(typeName, handler) {
            Edit.Registered.set(typeName, handler);
            Edit.TypeMap = null;
            Edit.TypeMapMember = null;
        }

        /**
         * Register a plug-in factory for a member `Type` spec (`Spec[:args]`), the new form of the global
         * `EditType<Spec>Factory` hook.
         * @param {string} spec - The spec name (ex: "Language").
         * @param {function(string, Array<string>):object} factory - (typeName, specParts) -> handler.
         */
        static RegisterSpec(spec, factory) {
            Edit.Specs.set(spec, factory);
            Edit.TypeMapMember = null;
        }

        /**
         * Build the map of the built in handlers by .NET type name (integers, floats, boolean, dates, strings, object and the
         * `System.Nullable` variants), plus the registered ones.
         * @returns {Map<string, object>} Type name -> handler.
         */
        static GetTypeMap() {
            const H = Edit.Handlers;
            const map = new Map();
            const N = n => "System.Nullable`1[[" + n + "]]";
            const both = (n, f) => {
                map.set(n, f(false));
                map.set(N(n), f(true));
            };
            both("System.SByte", n => new H.Integer(true, 1, n));
            both("System.Int16", n => new H.Integer(true, 2, n));
            both("System.Int32", n => new H.Integer(true, 4, n));
            both("System.Int64", n => new H.Integer(true, 8, n, Number.MIN_SAFE_INTEGER, Number.MAX_SAFE_INTEGER));
            both("System.Byte", n => new H.Integer(false, 1, n));
            both("System.UInt16", n => new H.Integer(false, 2, n));
            both("System.UInt32", n => new H.Integer(false, 4, n));
            both("System.UInt64", n => new H.Integer(false, 8, n, 0, Number.MAX_SAFE_INTEGER));
            both("System.Single", n => new H.Number(-3.40282347e+38, 3.40282347e+38, 11, n));
            both("System.Double", n => new H.Number(-1.7976931348623157e+308, 1.7976931348623157e+308, 19, n));
            both("System.Decimal", n => new H.Number(-7.9228162514264337593543950335e+28, 7.9228162514264337593543950335e+28, 30, n));
            both("System.Boolean", n => new H.Boolean(n));
            both("System.DateTime", n => new H.DateTime(false, false, false, n));
            both("System.DateOnly", n => new H.DateTime(false, true, false, n));
            both("System.TimeOnly", n => new H.DateTime(false, false, true, n));
            if (H.TimeSpan) {
                both("System.TimeSpan", n => new H.TimeSpan(n));
                both("System.Guid", n => new H.Guid(n));
            }
            map.set("System.String", new H.String());
            map.set("System.Object", new H.String(true));
            for (const [k, v] of Edit.Registered)
                map.set(k, v);
            return map;
        }

        /**
         * Resolve (and cache) the handler of a type / member: a spec plug-in, the type map, then by member flags (collection,
         * object, enum), else the default (read only text).
         * @param {string} typename - .NET type name.
         * @param {?object} [member] - The member (flags and Type spec drive the lookup).
         * @returns {object} The handler.
         */
        static GetTypeHandler(typename, member) {
            let tm = member ? Edit.TypeMapMember : Edit.TypeMap;
            if (!tm) {
                tm = Edit.GetTypeMap();
                if (member)
                    Edit.TypeMapMember = tm;
                else
                    Edit.TypeMap = tm;
            }
            const H = Edit.Handlers;
            const spec = member ? member.Type : null;
            const key = spec ? (typename + "|" + spec) : typename;
            let h = tm.get(key);
            if (h)
                return h;
            if (spec && !spec.startsWith("Hide:") && !spec.startsWith("Allow.")) {
                const parts = spec.split(":");
                const f = Edit.Specs.get(parts[0]) || window["EditType" + parts[0] + "Factory"];
                if (typeof f === "function") {
                    try {
                        h = f(typename, parts);
                    }
                    catch (x) {
                        console.warn("Edit type \"" + spec + "\" failed: " + x);
                    }
                }
            }
            if (!h && spec)
                h = tm.get(typename);
            if (!h && member) {
                const flags = member.Flags;
                if ((flags & Flags.Collection) !== 0)
                    h = Edit.Shared("Collection", () => new H.Collection());
                else if ((flags & Flags.IsObject) !== 0)
                    h = Edit.Shared("Object", () => new H.Object());
                else if ((flags & Flags.IsEnum) !== 0)
                    h = Edit.Shared("Enum", () => new H.Enum());
            }
            if (!h)
                h = Edit.Shared("Default", () => new H.Default());
            tm.set(key, h);
            return h;
        }

        /**
         * Get a shared handler instance.
         * @param {string} name - Name.
         * @param {function():object} create - Creates it.
         * @returns {object} The instance.
         */
        static Shared(name, create) {
            let h = Edit.SharedHandlers.get(name);
            if (!h) {
                h = create();
                Edit.SharedHandlers.set(name, h);
            }
            return h;
        }

        /**
         * The shared object handler (legacy accessor).
         * @returns {object} The handler.
         */
        static GetObjectTypeHandler() {
            return Edit.Shared("Object", () => new Edit.Handlers.Object());
        }
    }

    /** @type {?Edit} Editor that was created / used last (target of the page wide undo keys). */
    Edit.LastEdit = null;
    /** @type {number} Counter of the ids of field error messages (Validate(true)). */
    Edit.ErrorN = 0;
    /**
     * The text of the empty token of read only values ("empty"; a read only empty text never shows a blank).
     * @returns {string} The text.
     */
    Edit.EmptyText = () => _TF("empty", "Shown (muted) instead of a read only value that is an empty text");
    /** @type {boolean} True when the page wide key listener is installed. */
    Edit.HaveGlobalListener = false;
    /** @type {?Promise<void>} Resolves when every editor file is loaded. */
    Edit.Ready = null;
    /** @type {Map<string, ?object>} Type name -> TypeDesc (client registered or fetched; null = unknown). */
    Edit.Types = new Map();
    /** @type {?Map<string, object>} Handler cache for lookups without a member. */
    Edit.TypeMap = null;
    /** @type {?Map<string, object>} Handler cache for lookups with a member. */
    Edit.TypeMapMember = null;
    /** @type {Map<string, object>} Handlers registered with Register. */
    Edit.Registered = new Map();
    /** @type {Map<string, function>} Spec plug-in factories registered with RegisterSpec. */
    Edit.Specs = new Map();
    /** @type {Object<string, Promise>} Plug-in specific ready promises (ex: Language waits for its data). */
    Edit.PluginReady = {};
    /** @type {Map<string, object>} Shared handler instances (Collection, Object, Enum, Default). */
    Edit.SharedHandlers = new Map();
    /** @type {string[]} Plug-in specs whose handler file (EditType<Spec>.js) lives in this folder and is loaded on demand. */
    Edit.BuiltInPlugins = ["Language"];
    /** @type {number} Root editors narrower than this (px) use the mobile layout (automatic style). */
    Edit.MobileWidth = 520;
    /** @type {object} The type member flags. */
    Edit.Flags = Flags;
    /** @type {object} Default option values. */
    Edit.OptionDefaults = OptionDefaults;
    /** @type {string} Url of Edit.js. */
    Edit.ScriptUrl = ScriptUrl;
    /** @type {Object<string, function>} Handler classes by name (Integer, Number, Boolean, DateTime, String, Enum, Object, Collection, Default, ...), filled by the EditType*.js files. */
    Edit.Handlers = {};
    /** @type {Object<string, object>} Collection strategies (Array, Map), filled by the EditCollection*.js files. */
    Edit.Collections = {};
    /** @type {Object<string, function>} Layout strategies by style name, filled by EditDesktop.js / EditMobile.js. */
    Edit.Layouts = {};
    /** Helpers shared with the handler files. */
    Edit.Util = { Has, AddLine: addLine, MenuItem: menuItem, ReadClipboard: readClipboard };

    /**
     * Options controlling how an editor renders and behaves (`new Sw.Edit.Options()` = defaults, or clone a preset). Defined here
     * (not in EditOptions.js, which is now a compatibility stub) so pages can create options right after Edit.js ran, before
     * `await Sw.Edit.Ready`. The fields are documented in OptionDefaults and README.md ("Options").
     */
    class EditOptions {
        /** Create the default options. */
        constructor() {
            Object.assign(this, OptionDefaults);
        }

        /**
         * Copy the documented fields (not Name / UndoStack).
         * @returns {EditOptions} The copy.
         */
        Clone() {
            const e = new EditOptions();
            for (const k in OptionDefaults)
                e[k] = this[k];
            return e;
        }
    }
    {
        const preset = values => Object.assign(new EditOptions(), values);
        const clean = { Title: false, CanExpand: false, IsExpanded: false, Border: false, Dev: false, TypeHints: false };
        /** @type {EditOptions} Minimal embedded editor with menu icons. */
        EditOptions.CleanMenu = preset(Object.assign({ MenuIcon: true }, clean));
        /** @type {EditOptions} Read only CleanMenu. */
        EditOptions.CleanReadOnlyMenu = preset(Object.assign({ MenuIcon: true, ReadOnly: true }, clean));
        /** @type {EditOptions} Minimal embedded editor (no title, no frame, no dev info). */
        EditOptions.Clean = preset(clean);
        /** @type {EditOptions} Read only Clean. */
        EditOptions.CleanReadOnly = preset(Object.assign({ ReadOnly: true }, clean));
        /** @type {EditOptions} Clean with the hint lines for people under the names (format, range and unit in words, no type names: "up to 20 characters", "7 to 365 days"): a settings form in a card or dialog. */
        EditOptions.CleanHints = preset(Object.assign({}, clean, { TypeHints: true, PlainHints: true }));
        /** @type {EditOptions} Read only CleanHints. */
        EditOptions.CleanHintsReadOnly = preset(Object.assign({}, clean, { TypeHints: true, PlainHints: true, ReadOnly: true }));
    }
    Edit.Options = EditOptions;
    if (!window.EditOptions)
        window.EditOptions = EditOptions;

    //  Members defined by the files Init loads (type.js, UndoStack.js, EditDesktop.js, EditMobile.js): until then a read logs a clear
    //  console error (instead of the bare "Sw.Edit.UndoStack is not a constructor"); the files' assignments replace the guards.
    for (const [name, file] of [["TypeInfo", "type.js"], ["UndoStack", "UndoStack.js"], ["UndoEntry", "UndoStack.js"], ["LayoutDesktop", "EditDesktop.js"], ["LayoutMobile", "EditMobile.js"]]) {
        Object.defineProperty(Edit, name, {
            configurable: true, enumerable: false,
            get: () => {
                console.error("Sw.Edit." + name + " is not loaded yet (" + file + " is loaded by Sw.Edit.Init()): use \"await Sw.Edit.Ready\" before using it.");
                return undefined;
            },
            set: v => Object.defineProperty(Edit, name, { value: v, writable: true, configurable: true, enumerable: true }),
        });
    }

    /**
     * Optional base class of the type handlers (shared helpers; a handler only needs to implement the duck-typed contract).
     */
    class Handler {
        /**
         * The default for a new member.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {*} The value (GetDefault).
         */
        CreateDefault(member, options) {
            return this.GetDefault(member, options);
        }

        /**
         * Coerce an accepted pasted / dropped value (identity).
         * @param {*} obj - The value.
         * @returns {*} The value.
         */
        Condition(obj) {
            return obj;
        }

        /**
         * Display text.
         * @param {*} obj - The value.
         * @returns {string} The text.
         */
        ToString(obj) {
            return "" + obj;
        }

        /**
         * Validate null against AcceptNull.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {string} name - Path.
         * @returns {?Array<string>} Null or [path, message] (also null for a non null value).
         */
        static NullCheck(obj, member, name) {
            if (obj !== null && obj !== undefined)
                return null;
            if ((member.Flags & Flags.AcceptNull) !== 0)
                return null;
            return [name, (member.DisplayName || member.Name) + ": " + _TF("Value may not be null!", "Error message displayed when trying to set a value to null that may not be null")];
        }

        /**
         * Test if a member editor is read only (options, member flag, or a forced read only handler).
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {boolean} True when read only.
         */
        IsReadOnly(member, options) {
            return Edit.IsReadOnly(member, options) || !!this.ForceReadOnly;
        }

        /**
         * Create the standard item of a member editor.
         * @param {object} obj - The object.
         * @param {Edit} editor - The editor.
         * @param {object} member - The member.
         * @param {object} o - Item fields:
         *   Focus (an element or a Sw component: the control that gets the focus and is named after the member; a wrapper is resolved
         *   to its first control), Title, DefValue, DefValueText (default: the handler's GetDefault / ToString, see Sw.Edit.SetMember),
         *   Set(value, isFirst) (show a value that changed from outside: undo, redo, Set default, paste, drop, Reset; gets the current value),
         *   Update(isFirst) (refresh the UI, the older hook; both may be given).
         *   Without Set and Update the framework refreshes the member itself (Focus.UpdateValue() if the element has one, else it
         *   renders the member again), so a value shown by a plug-in is never stale.
         * @returns {object} The item.
         */
        static Item(obj, editor, member, o) {
            const mn = member.Name;
            const focus = Handler.FocusElement(o.Focus);
            const own = typeof o.Set === "function" || typeof o.Update === "function";
            const item = {
                Focus: focus,
                Title: o.Title || null,
                DefValue: o.DefValue,
                DefValueText: o.DefValueText,
                SetValue: v => {
                    obj[mn] = v;
                    editor.Changed(member, v);
                },
                UpdateValue: async isFirst => {
                    if (typeof o.Set === "function")
                        await o.Set(obj[mn], isFirst);
                    if (typeof o.Update === "function")
                        await o.Update(isFirst);
                    if (!own) {
                        if (focus && typeof focus.UpdateValue === "function")
                            focus.UpdateValue();
                        else if (typeof item.Rerender === "function")
                            await item.Rerender();
                    }
                    const f = item.Focus;
                    if (!isFirst && f && f.isConnected && document.activeElement !== f && !(f.contains && f.contains(document.activeElement)))
                        f.focus();
                },
                GetValue: () => obj[mn],
                //  Nothing refreshes the shown value: Sw.Edit.SetMember renders a plug-in member again after outside changes (Rerender)
                NeedsRender: !own && !(focus && typeof focus.UpdateValue === "function"),
            };
            return item;
        }

        /**
         * The element of an item's Focus: a Sw component (`.E`, `.Input`) is resolved to its input / element, an element that can't take
         * the focus (a wrapper) to its first control (input, select, textarea, button, an element with a tabindex or a widget role).
         * @param {*} f - The Focus value.
         * @returns {?HTMLElement} The element.
         */
        static FocusElement(f) {
            if (f && !(f instanceof Element) && (f.Input instanceof Element || f.E instanceof Element))
                f = f.Input instanceof Element ? f.Input : f.E;
            if (!(f instanceof Element))
                return null;
            if (f.matches("input,select,textarea,button,[tabindex],[contenteditable=true],[role]"))
                return f;
            return f.querySelector("input:not([type=hidden]),select,textarea,button,[tabindex]:not([tabindex='-1']),[role=radiogroup],[role=slider],[role=listbox],[role=combobox],[role=textbox]") || f;
        }
    }

    Edit.Handler = Handler;

    /**
     * Shared base of the numeric handlers (EditTypeInteger.js, EditTypeNumber.js): range / default parsing, paste conversion,
     * validation and the editor (a filtered text box plus a Sw.Slider for members with the Slider flag and both limits).
     */
    class NumberHandler extends Handler {
        /**
         * @param {boolean} isInteger - Integer type (values are truncated).
         * @param {number} typeMin - Smallest value of the type.
         * @param {number} typeMax - Largest value of the type.
         * @param {number} maxLen - Maximum text length.
         * @param {boolean} [nullable] - Nullable type (an empty text is null).
         */
        constructor(isInteger, typeMin, typeMax, maxLen, nullable) {
            super();
            /** @type {boolean} Integer type. */
            this.IsInteger = isInteger;
            /** @type {number} Type minimum. */
            this.TypeMin = typeMin;
            /** @type {number} Type maximum. */
            this.TypeMax = typeMax;
            /** @type {number} Maximum text length. */
            this.MaxLen = maxLen;
            /** @type {boolean} Nullable type. */
            this.Nullable = !!nullable;
        }

        /**
         * Parse a number (metadata may use "," as decimal separator).
         * @param {*} v - The value.
         * @returns {number} The number (NaN if invalid).
         */
        Parse(v) {
            if (typeof v === "number")
                return this.IsInteger ? Math.trunc(v) : v;
            const n = parseFloat(("" + v).trim().replace(",", "."));
            return this.IsInteger ? Math.trunc(n) : n;
        }

        /**
         * Test if null is a valid value.
         * @param {object} member - The member.
         * @returns {boolean} True for nullable types / AcceptNull.
         */
        AcceptsNull(member) {
            return this.Nullable || (member.Flags & Flags.AcceptNull) !== 0;
        }

        /**
         * Effective range and default of a member (null Min / Max = the type range; fixes the old "Max: null" NaN handling).
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {{Min: number, Max: number, Def: number, Title: ?string, HaveMin: boolean, HaveMax: boolean, ParsedDef: (number|undefined)}} The info.
         */
        Range(member, options) {
            const tmin = this.TypeMin, tmax = this.TypeMax;
            let title = null;
            const pmin = Has(member.Min) ? this.Parse(member.Min) : NaN;
            const pmax = Has(member.Max) ? this.Parse(member.Max) : NaN;
            const min = isNaN(pmin) ? tmin : Math.min(Math.max(pmin, tmin), tmax);
            let max = isNaN(pmax) ? tmax : Math.max(Math.min(pmax, tmax), tmin);
            if (max < min)
                max = min;
            const haveMin = !isNaN(pmin) && pmin === min;
            const haveMax = !isNaN(pmax) && pmax === max;
            const dev = options && options.Dev;
            if (haveMin || dev)
                title = addLine(title, _TF("Minimum value", "Header of a tool tip row with the smallest allowed value"), min);
            if (haveMax || dev)
                title = addLine(title, _TF("Maximum value", "Header of a tool tip row with the largest allowed value"), max);
            const pdef = Has(member.Default) ? this.Parse(member.Default) : NaN;
            let def = isNaN(pdef) ? 0 : pdef;
            def = Math.min(Math.max(def, min), max);
            if (!isNaN(pdef) || dev)
                title = addLine(title, _TF("Default value", "Header of a tool tip row with the default value of a property"), def);
            return { Min: min, Max: max, Def: def, Title: title, HaveMin: haveMin, HaveMax: haveMax, ParsedDef: isNaN(pdef) ? undefined : def };
        }

        /**
         * Accept numbers and numeric strings inside the type range.
         * @param {*} obj - Candidate.
         * @returns {boolean} True when usable.
         */
        IsOfType(obj) {
            if (typeof obj !== "number" && typeof obj !== "string")
                return false;
            const v = this.Parse(obj);
            return !isNaN(v) && v >= this.TypeMin && v <= this.TypeMax;
        }

        /**
         * Convert an accepted value: parsed, rounded (integers) and clamped to the member range.
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {number} The value.
         */
        Condition(obj, member, options) {
            const r = this.Range(member, options || {});
            let v = typeof obj === "string" ? parseFloat(obj.replace(",", ".")) : obj;
            if (this.IsInteger)
                v = Math.round(v);
            if (isNaN(v))
                v = r.Def;
            return Math.min(Math.max(v, r.Min), r.Max);
        }

        /**
         * Validate the range (null only for nullable types / AcceptNull).
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            const dn = member.DisplayName || member.Name;
            if (obj === null)
                return this.AcceptsNull(member) ? null : [name, dn + ": " + _TF("Value may not be null!", "Error message displayed when trying to set a value to null that may not be null")];
            const v = typeof obj === "number" ? obj : this.Parse(obj);
            if (isNaN(v))
                return [name, dn + ": " + _T("The value \"{0}\" is not a number", obj, "Validation error of a numerical value.{0} is replaced with the invalid value.")];
            const r = this.Range(member, options);
            if (v < r.Min)
                return [name, dn + ": " + _T("Value to small! Smallest allowed value is {0}, current value is {1}", r.Min, v, "Validation error of a numerical value that is too small.{0} is replaced with the minimum.{1} is replaced with the current value.")];
            if (v > r.Max)
                return [name, dn + ": " + _T("Value to large! Largest allowed value is {0}, current value is {1}", r.Max, v, "Validation error of a numerical value that is too large.{0} is replaced with the maximum.{1} is replaced with the current value.")];
            return null;
        }

        /**
         * The default value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @returns {number} The default.
         */
        GetDefault(member, options) {
            return this.Range(member, options || {}).Def;
        }

        /**
         * The slider step: EditParams, else a power of ten giving at most 100 steps (floats: 10 to 100 steps).
         * @param {object} member - The member.
         * @param {number} min - Minimum.
         * @param {number} max - Maximum.
         * @returns {number} The step.
         */
        SliderStep(member, min, max) {
            const p = Has(member.EditParams) ? parseFloat(("" + member.EditParams).replace(",", ".")) : NaN;
            if (p > 0)
                return this.IsInteger ? Math.max(1, Math.round(p)) : p;
            let step = 1;
            const range = max - min;
            if (range <= 0)
                return 1;
            for (let i = 0; i < 40; ++i) {
                const steps = range / step;
                if (steps <= 100 && (this.IsInteger || steps >= 10))
                    break;
                if (steps > 100)
                    step *= 10;
                else
                    step /= 10;
            }
            return this.IsInteger ? Math.max(1, Math.round(step)) : step;
        }

        /**
         * Build the editor: a text box (digits, a leading "-" when negative values are allowed, one "." for floats) and, for
         * Slider members with both limits, a slider beside it.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item.
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const t = this;
            const mn = member.Name;
            const cell = editContext.Element;
            const r = this.Range(member, options);
            const readOnly = this.IsReadOnly(member, options);
            const nullOk = this.AcceptsNull(member);
            if (obj[mn] === undefined || (obj[mn] === null && !nullOk) || (obj[mn] !== null && typeof obj[mn] === "object"))
                obj[mn] = r.Def;
            const signed = r.Min < 0;
            const useSlider = r.HaveMin && r.HaveMax && (member.Flags & Flags.Slider) !== 0;
            const step = useSlider ? this.SliderStep(member, r.Min, r.Max) : 0;
            let decimals = -1;
            if (useSlider && !this.IsInteger) {
                decimals = 0;
                for (let scale = 1; scale < 100000; scale *= 10, ++decimals) {
                    const ok = [step, r.Min, r.Max].every(x => Math.abs(Math.round(x * scale) - x * scale) < 1e-9);
                    if (ok)
                        break;
                }
            }
            const fmt = v => {
                if (v === null || v === undefined || (typeof v === "number" && isNaN(v)))
                    return "";
                if (decimals < 0)
                    return "" + v;
                const f = Number(v).toFixed(decimals);
                return Number(f) === Number(v) ? f : ("" + v);
            };
            const parse = s => {
                s = ("" + s).trim();
                if (!s || s === "-")
                    return nullOk && !s ? null : r.Def;
                let v = t.Parse(s);
                if (isNaN(v))
                    v = r.Def;
                return Math.min(Math.max(v, r.Min), r.Max);
            };
            const lenMax = this.IsInteger ? Math.max(("" + r.Min).length, ("" + r.Max).length) : this.MaxLen;
            const commit = async (v, token) => {
                const old = obj[mn];
                if (old === v)
                    return;
                await editor.SetMemberValue(member, v, old, _T("Set value: \"{0}\"", v, "Command text stored in a log when a property value was changed.{0} is replaced with the new value."), token);
            };
            const text = Edit.CreateTextInput(() => obj[mn], (v, ev, token) => commit(v, token), parse, fmt, member.Summary, null,
                (ch, pos, val) => {
                    if (ch === "-")
                        return pos === 0 && signed && val.indexOf("-") < 0;
                    if (ch === "." && !t.IsInteger)
                        return val.indexOf(".") < 0;
                    return ch >= "0" && ch <= "9";
                }, lenMax + (signed ? 1 : 0) + 1, r.Def, readOnly);
            text.inputMode = this.IsInteger ? "numeric" : "decimal";
            text.classList.add("sw-edit-number");
            text.style.setProperty("--sw-edit-chars", Math.min(lenMax + 2, 24));
            text.setAttribute("aria-label", member.DisplayName || mn);
            Edit.FitNumberBox(text);
            cell.append(text);
            //  The unit (member Unit, a plain text: "px", "%", "×") in a slot of one width on every number row of the editor
            //  (--sw-edit-suffix-w, set by SetObject), so the boxes and sliders line up with and without a unit
            cell.append(el("span.sw-edit-suffix", { text: member.Unit ? String(member.Unit) : "", attrs: { "aria-hidden": member.Unit ? null : "true" } }));
            let slider = null;
            if (useSlider && !readOnly) {
                let token = null;
                slider = new Sw.Slider({
                    Min: r.Min, Max: r.Max, Step: step, Value: obj[mn] ?? r.Def, Default: r.Def, Disabled: readOnly,
                    Title: member.DisplayName || mn,
                    OnStart: () => token = {},
                    OnInput: v => commit(fmtNum(v), token || (token = {})),
                    OnChange: v => {
                        const tk = token;
                        token = null;
                        return commit(fmtNum(v), tk);
                    },
                });
                const fmtNum = v => decimals >= 0 ? parseFloat(Number(v).toFixed(decimals)) : v;
                cell.classList.add("EditSlider", "sw-edit-hasslider");
                cell.append(slider.E);
            }
            return Handler.Item(obj, editor, member, {
                Focus: text, Title: r.Title, DefValue: r.ParsedDef, DefValueText: r.ParsedDef === undefined ? undefined : fmt(r.ParsedDef),
                Update: () => {
                    text.UpdateValue();
                    if (slider && obj[mn] !== null)
                        slider.Set(obj[mn], true, true);
                },
            });
        }
    }

    Edit.NumberHandler = NumberHandler;

    /**
     * The sticky footer of a form (Design v2, PORTING.md B14): Reset (ghost, left: undoes every change since the baseline, as undo steps that can be
     * redone), the change count ("2 changes", the undo steps since the baseline; 0 when the value equals the baseline again), and the
     * actions on the right (the primary one last: Save / Send ...). Usage:
     * `const f = new Sw.Edit.FormFooter(editor, { Actions: [button.E], OnReset })`, append `f.E` after the editor (or its tabs); call
     * `f.SetBaseline()` after a successful save. Changes made outside the editor (ex: the json tab of explore/api.html) count once they are
     * applied to the editor.
     */
    class FormFooter {
        /**
         * @param {Edit} editor - The (root) editor.
         * @param {object} [o] - Options: Actions (elements / components on the right), ResetText, OnReset(), ShowReset (default true),
         * Class, Badge (an element that also shows the count, ex: a tab bar slot), ShowReadOnly (when nothing is editable, see
         * NothingEditable, the footer is hidden; true keeps it with the actions only, ex: a Close button), Title (what is edited, for the
         * leave question "Your changes to {Title} are lost."), Dirty (default off, as the original editor never asked "Leave site?"; true: the page is dirty
         * while there are changes, SAF-10; a text = the leave sentence; inside a dialog the dialog asks instead).
         */
        constructor(editor, o) {
            const t = this;
            o = o || {};
            /** @type {object} The options. */
            t.O = o;
            /** @type {Edit} The editor. */
            t.Editor = editor;
            /** @type {HTMLElement} The count ("2 changes"). */
            t.Count = el("span.sw-badge.sw-edit-changes", { attrs: { "aria-live": "polite" } });
            t.Reset = new Sw.Button({
                Kind: "ghost", Icon: "undo", Class: "sw-edit-reset",
                Text: o.ResetText || _TF("Reset", "Text of the button in a form footer that undoes every change of the form"),
                Title: _TF("Undo every change since the form was opened (or last saved); Ctrl+Y redoes them", "Tool tip of the Reset button in a form footer"),
                OnClick: () => t.ResetAll(),
            });
            const push = el("div.sw-edit-formfoot-actions");
            for (const a of o.Actions || [])
                if (a)
                    push.append(a.E || a);
            /** @type {HTMLElement} The footer element. */
            t.E = el("div.sw-edit-formfoot", { class: o.Class || null, attrs: { role: "group", "aria-label": _TF("Form actions", "Accessible name of the footer of a form (reset, change count, save)") } },
                o.ShowReset === false ? null : t.Reset.E, t.Count, push);
            /** @type {HTMLElement} The actions container (append more buttons). */
            t.Actions = push;
            //  Nothing editable (the ReadOnly option, a read only type, or every visible member read only): no Save / Reset that look
            //  usable. The footer is hidden, or with ShowReadOnly only Reset and the count are (the actions stay, ex: Close).
            /** @type {boolean} The editor can't change anything. */
            t.ReadOnly = FormFooter.NothingEditable(editor);
            if (t.ReadOnly) {
                t.E.classList.add("sw-edit-formfoot-readonly");
                if (o.ShowReadOnly)
                    t.Reset.E.hidden = true;
                else
                    t.E.hidden = true;
            }
            t.SetBaseline();
            editor.UndoStack.OnChange(() => t.Update());
        }

        /**
         * Test if an editor can't change anything: the ReadOnly option, a read only type / value, or every visible member read only.
         * @param {Edit} editor - The editor.
         * @returns {boolean} True when nothing is editable.
         */
        static NothingEditable(editor) {
            if (editor.IsObjectReadOnly())
                return true;
            const type = editor.Type;
            if (!type || Edit.IsPrimitive(type) || (type.Flags & Flags.Collection) !== 0)
                return false;
            const visible = (type.Members || []).filter(m => (m.Flags & Flags.Hide) === 0);
            return visible.length > 0 && visible.every(m => (m.Flags & Flags.ReadOnly) !== 0);
        }

        /** Make the current value the baseline (after a save; the count is 0). */
        SetBaseline() {
            const s = this.Editor.UndoStack;
            this.BaseOp = s.Pos > 0 ? s.Ops[s.Pos - 1] : null;
            this.BaseJson = JSON.stringify(this.Editor.GetObject() ?? null);
            this.Update();
        }

        /**
         * The number of changes since the baseline (undo steps; 0 when the value equals the baseline).
         * @returns {number} The count.
         */
        Changes() {
            const t = this;
            if (JSON.stringify(t.Editor.GetObject() ?? null) === t.BaseJson)
                return 0;
            const s = t.Editor.UndoStack;
            const bi = t.BaseOp ? s.Ops.indexOf(t.BaseOp) + 1 : 0;
            if (t.BaseOp && bi === 0)
                return Math.max(1, s.Pos);
            return Math.max(1, Math.abs(s.Pos - bi));
        }

        /** Refresh the count and the Reset button. */
        Update() {
            const t = this;
            const n = t.Changes();
            const text = n === 0 ? "" : n === 1 ? _TF("1 change", "Change count in a form footer, one change") : _T("{0} changes", n, "Change count in a form footer.{0} is the number of changes");
            for (const b of [t.Count, t.O.Badge]) {
                if (!b)
                    continue;
                b.textContent = text;
                b.hidden = n === 0;
            }
            t.Reset.SetDisabled(n === 0, n === 0 ? _TF("There are no changes to undo", "Reason (tool tip) of the disabled Reset button of a form footer") : undefined);
            t.E.classList.toggle("sw-edit-dirty", n > 0);
            //  The page is dirty while the form has changes (UX guide SAF-10: leaving asks "Leave without saving?"); not inside a dialog
            //  (the dialog asks, POP-6), not with the option Dirty: false; SetBaseline (after a save) clears it
            //  Opt in (Dirty: true or the leave text): the original editor never asked "Leave site?" (P3-A-4)
            if ((t.O.Dirty === true || typeof t.O.Dirty === "string") && !t.ReadOnly && Sw.Page && Sw.Page.SetDirty) {
                t.DirtyKey = t.DirtyKey || "sw-formfoot:" + (++FormFooter.N);
                const inDialog = t.E.isConnected && !!t.E.closest("[aria-modal='true'], .sw-dialog");
                Sw.Page.SetDirty(t.DirtyKey, n > 0 && !inDialog ? (typeof t.O.Dirty === "string" ? t.O.Dirty : (t.O.Title
                    ? _T("Your changes to {0} are lost.", t.O.Title, "Text of the question 'Leave without saving?'. {0} names what was edited, ex: 'appsettings.json'")
                    : _TF("Your changes in the form are lost.", "Text of the question 'Leave without saving?' for a form without a name"))) : null);
            }
        }

        /**
         * Undo every change since the baseline (falls back to replacing the value with the baseline when the history doesn't reach it).
         * @returns {Promise<void>}
         */
        async ResetAll() {
            const t = this;
            const s = t.Editor.UndoStack;
            const bi = t.BaseOp ? s.Ops.indexOf(t.BaseOp) + 1 : 0;
            if (!t.BaseOp || bi > 0) {
                while (s.Pos > bi)
                    if (!await s.Undo())
                        break;
                while (s.Pos < bi)
                    if (!await s.Redo())
                        break;
            }
            if (JSON.stringify(t.Editor.GetObject() ?? null) !== t.BaseJson)
                await t.Editor.ReplaceObject(JSON.parse(t.BaseJson), _TF("Reset", "Text of the button in a form footer that undoes every change of the form"));
            t.Update();
            if (t.O.OnReset)
                t.O.OnReset();
        }
    }

    /** @type {number} Counter for the dirty keys of the footers. */
    FormFooter.N = 0;
    Edit.FormFooter = FormFooter;

    /**
     * A one line summary of an object for collapsed items and object members ("Afar · aa · Afar"): its first 3 non empty texts,
     * numbers and booleans (in member order when a type is given), collections as their count.
     * @param {*} obj - The value.
     * @param {?object} [type] - Its TypeDesc (member order, hidden and password members are skipped).
     * @returns {string} The summary ("" when there is nothing to show).
     */
    Edit.Summary = (obj, type) => {
        if (obj === null || obj === undefined || typeof obj !== "object")
            return obj === null || obj === undefined ? "" : String(obj);
        const names = type && type.Members ? type.Members.filter(m => (m.Flags & (Flags.Hide | Flags.Password)) === 0).map(m => m.Name) : Object.keys(obj).filter(k => k !== "$type");
        const parts = [];
        for (const k of names) {
            const v = obj[k];
            if (v === null || v === undefined || v === "")
                continue;
            const tv = typeof v;
            if (tv === "string" || tv === "number" || tv === "boolean")
                parts.push(tv === "string" && v.length > 40 ? v.substring(0, 37) + "..." : String(v));
            if (parts.length >= 3)
                break;
        }
        return parts.join(" · ");
    };

    Sw.Edit = Edit;
    if (!window.Edit)
        window.Edit = Edit;

    /**
     * Entry point of the edit test / demo page (edit/index.html): loads index.js (the demo) and runs it.
     * @returns {Promise<void>}
     */
    async function editTestMain() {
        await Edit.Init();
        await Sw.Net.IncludeJs(ScriptUrl, "index.js");
        await Sw.EditDemo.Main();
    }
    if (!window.editTestMain)
        window.editTestMain = editTestMain;

    Edit.Init();
})();