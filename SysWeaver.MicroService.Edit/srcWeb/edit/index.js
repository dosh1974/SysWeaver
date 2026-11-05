/**
 * @file The object editor test / demo page (edit/index.html, `Sw.EditDemo`; entry `editTestMain()` from Edit.js).
 *
 * Shows two editors in tabs: the server test type `SysWeaver.MicroService.EditTest` (TypeService, debug builds) and a client
 * registered demo type that uses every handler (enums, flags, Allow.<Member>, dates, nullable date, language plug-in,
 * dictionary (Map strategy), Hide: expression, password, read only, slider, Int64, unknown type). A toolbar offers undo /
 * redo (with the step titles), validate (focuses the failing member), show json, read only and layout switches, a dialog
 * editor (PopUpEdit style) and a reset. Every EditChange is logged with its member path (also to the console, like the old
 * test page).
 */
(function () {
    "use strict";

    const el = Sw.el;
    const T = Sw.Edit.Flags;

    /** The client registered demo type (exercises every handler). */
    const DemoType = {
        TypeName: "SysWeaver.Demo.EditDemo", DisplayName: "Edit demo", Flags: T.IsObject,
        Summary: "A client side type that uses every editor handler", Asm: "edit/index.js",
        Members: [
            { Name: "Mode", DisplayName: "Mode", TypeName: "SysWeaver.Demo.Mode", Flags: T.IsEnum | T.IsPrimitive, Summary: "A normal enum", Default: "2>0<Off<Turned off|1<On<Turned on|2<Auto<Automatic selection|3<SuperFast<As fast as possible" },
            { Name: "Access", DisplayName: "Access", TypeName: "SysWeaver.Demo.Access", Flags: T.IsEnum | T.IsFlags | T.IsPrimitive, Summary: "A flags enum", Default: "5>0<None<No access|1<Read|2<Write|4<Execute|7<All<Every access right" },
            { Name: "AllowedModes", DisplayName: "Allowed modes", TypeName: "SysWeaver.Demo.ModeMask", Flags: T.IsEnum | T.IsFlags | T.IsPrimitive, Summary: "Bit n allows mode n", Default: "7>1<AllowOff|2<AllowOn|4<AllowAuto|8<AllowSuperFast" },
            { Name: "LimitedMode", DisplayName: "Limited mode", TypeName: "SysWeaver.Demo.Mode", Flags: T.IsEnum | T.IsPrimitive, Type: "Allow.AllowedModes", Summary: "Only the modes allowed above can be picked", Default: "0>0<Off|1<On|2<Auto|3<SuperFast" },
            { Name: "When", DisplayName: "When", TypeName: "System.DateTime", Flags: T.IsPrimitive, Summary: "A date and time (local time, stored as UTC ISO text)" },
            { Name: "Day", DisplayName: "Day", TypeName: "System.DateOnly", Flags: T.IsPrimitive, Min: "2020-01-01", Max: "2030-12-31", Default: "2026-06-15", Summary: "A date between 2020 and 2030" },
            { Name: "At", DisplayName: "At", TypeName: "System.TimeOnly", Flags: T.IsPrimitive, Default: "08:30:00", Summary: "A time of day" },
            { Name: "Maybe", DisplayName: "Optional date", TypeName: "System.Nullable`1[[System.DateTime]]", Flags: T.IsPrimitive | T.AcceptNull, Summary: "A nullable date (clear the input for null)" },
            { Name: "Language", DisplayName: "Language", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Language", Default: "en", Summary: "The language plug-in" },
            { Name: "Scores", DisplayName: "Scores", TypeName: "System.Collections.Generic.Dictionary`2[[System.String],[System.Int32]]", KeyTypeName: "System.String", ElementTypeName: "System.Int32", Flags: T.Collection, Max: "8", Summary: "A dictionary (keys are asked for when adding)", ElementInst: { Flags: T.IsPrimitive, Min: "0", Max: "100", Default: "50" } },
            { Name: "Tags", DisplayName: "Tags", TypeName: "System.String[]", ElementTypeName: "System.String", Flags: T.Collection | T.Indexed, Min: "1", Max: "5", Summary: "1 to 5 texts", ElementInst: { Flags: T.IsPrimitive, Max: "16" } },
            { Name: "ShowSecret", DisplayName: "Show secret", TypeName: "System.Boolean", Flags: T.IsPrimitive, Default: "false", Summary: "Shows the password member (Hide: expression)" },
            { Name: "Secret", DisplayName: "Secret", TypeName: "System.String", Flags: T.IsPrimitive | T.Password, Type: "Hide:!this.ShowSecret", Max: "32", Summary: "Only shown when Show secret is on" },
            { Name: "Ratio", DisplayName: "Ratio", TypeName: "System.Double", Flags: T.IsPrimitive | T.Slider, Min: "0", Max: "1", Default: "0.25", Summary: "A float slider" },
            { Name: "Big", DisplayName: "Big number", TypeName: "System.Int64", Flags: T.IsPrimitive, Summary: "64 bit integers are limited to the JS safe range" },
            { Name: "Fixed", DisplayName: "Fixed text", TypeName: "System.String", Flags: T.IsPrimitive | T.ReadOnly, Default: "Read only", Summary: "A read only member" },
            { Name: "Notes", DisplayName: "Notes", TypeName: "System.String", Flags: T.IsPrimitive | T.Multiline, Summary: "Multi line text" },
            { Name: "Opaque", DisplayName: "Opaque", TypeName: "SysWeaver.Demo.Unknown", Flags: 0, Default: "unknown", Summary: "No handler: shown read only" },
            //  Colors (EditTypeColor.js, C# [EditColor(...)])
            { Name: "CssColor", DisplayName: "Css color", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Color:Css", Default: "#ff8800", Summary: "Color:Css (#rrggbb)" },
            { Name: "CssAlpha", DisplayName: "Css color with alpha", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Color:Css:Alpha", Default: "#3366ff80", Summary: "Color:Css:Alpha (#rrggbbaa)" },
            { Name: "Rgba", DisplayName: "Rgba floats", TypeName: "System.Single[]", ElementTypeName: "System.Single", Flags: T.Collection | T.Indexed, Type: "Color:Rgba", Default: "[0.2,0.6,1,1]", Summary: "Color:Rgba (float[4] 0..1)" },
            { Name: "Hdr", DisplayName: "HDR color", TypeName: "System.Single[]", ElementTypeName: "System.Single", Flags: T.Collection | T.Indexed, Type: "Color:HdrRgb::8", Default: "[2,1.5,0.5]", Summary: "Color:HdrRgb (float[3], intensity up to 8)" },
            { Name: "Argb", DisplayName: "Argb integer", TypeName: "System.Int32", Flags: T.IsPrimitive, Type: "Color:UInt32Argb", Default: "-16744448", Summary: "Color:UInt32Argb (0xAARRGGBB as a signed int, like System.Drawing.Color.ToArgb)" },
            { Name: "RgbaUInt", DisplayName: "Rgba uint", TypeName: "System.UInt32", Flags: T.IsPrimitive, Type: "Color:UInt32Rgba", Default: "4286611711", Summary: "Color:UInt32Rgba (0xRRGGBBAA)" },
            { Name: "Bytes", DisplayName: "Rgba bytes", TypeName: "System.Byte[]", ElementTypeName: "System.Byte", Flags: T.Collection | T.Indexed, Type: "Color:Rgba8", Default: "[255,0,128,255]", Summary: "Color:Rgba8 (byte[4])" },
            //  Durations, ids, texts with a format (EditTypeTimeSpan.js, EditTypeSpecs.js)
            { Name: "Timeout", DisplayName: "Timeout", TypeName: "System.TimeSpan", Flags: 0, Default: "00:01:30", Min: "00:00:01", Max: "1.00:00:00", Summary: "A TimeSpan (1 s to 1 day): type 00:01:30, 90s or 1h 30m" },
            { Name: "Id", DisplayName: "Id", TypeName: "System.Guid", Flags: T.IsPrimitive, Summary: "A Guid" },
            { Name: "Mail", DisplayName: "E-mail", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Email", Summary: "EditTypes.Email" },
            { Name: "Site", DisplayName: "Url", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Url", Summary: "EditTypes.Url" },
            { Name: "Phone", DisplayName: "Phone", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Phone", Summary: "EditTypes.Phone" },
            //  Numbers with a unit (EditTypeUnit.js, C# [EditUnit(EditUnits.X)])
            { Name: "WaitSeconds", DisplayName: "Wait", TypeName: "System.Int32", Flags: T.IsPrimitive, Type: "Unit:Seconds", Min: "1", Max: "86400", Default: "90", Summary: "Unit:Seconds (type 90, 90s, 1h 30m)" },
            { Name: "CacheSize", DisplayName: "Cache size", TypeName: "System.Int64", Flags: T.IsPrimitive, Type: "Unit:Bytes", Default: "2097152", Summary: "Unit:Bytes (type 2 MB, 512k)" },
            { Name: "Quality", DisplayName: "Quality", TypeName: "System.Int32", Flags: T.IsPrimitive | T.Slider, Type: "Unit:Percent", Min: "10", Max: "100", Default: "80", Summary: "Unit:Percent with a slider" },
            { Name: "Opacity", DisplayName: "Opacity", TypeName: "System.Double", Flags: T.IsPrimitive, Type: "Unit:Ratio", Min: "0", Max: "1", Default: "0.25", Summary: "Unit:Ratio (type 0.25 or 25%)" },
            { Name: "HexColor", DisplayName: "Hex color", TypeName: "System.String", Flags: T.IsPrimitive, Type: "Color:Hex", Default: "#336699", Summary: "Color:Hex (typed css colors are stored as #rrggbb)" },
            //  Read only members: nothing inside may become writable (nested objects, items, dictionary values, menus, paste, drop, keys)
            { Name: "RoPoint", DisplayName: "Read only object", TypeName: "SysWeaver.Demo.Point", Flags: T.IsObject | T.ReadOnly, Default: '{"X":1,"Y":2,"Tags":["a","b"]}', Summary: "A read only nested object" },
            { Name: "RoList", DisplayName: "Read only list", TypeName: "SysWeaver.Demo.Point[]", ElementTypeName: "SysWeaver.Demo.Point", Flags: T.Collection | T.Indexed | T.ReadOnly, Default: '[{"X":1,"Y":2,"Tags":["x"]},{"X":3,"Y":4,"Tags":[]}]', Summary: "A read only list of objects" },
            { Name: "RoMap", DisplayName: "Read only dictionary", TypeName: "System.Collections.Generic.Dictionary`2[[System.String],[System.Int32]]", KeyTypeName: "System.String", ElementTypeName: "System.Int32", Flags: T.Collection | T.ReadOnly, Default: '{"a":1,"b":2}', Summary: "A read only dictionary" },
            { Name: "Points", DisplayName: "Points", TypeName: "SysWeaver.Demo.Point[]", ElementTypeName: "SysWeaver.Demo.Point", Flags: T.Collection | T.Indexed, Max: "6", Default: '[{"X":1,"Y":2,"Tags":[]},{"X":5,"Y":6,"Tags":["z"]}]', Summary: "An editable list of objects (copy / paste / drag items between this and the read only list)" },
            //  Read only parts inside writable values: a paste / drop / json edit of the whole value keeps them (one toast names them)
            { Name: "Box", DisplayName: "Box", TypeName: "SysWeaver.Demo.Box", Flags: T.IsObject, Default: '{"Name":"Box","Serial":"SN-0001","Origin":{"X":0,"Y":0,"Tags":["o"]},"Size":{"X":10,"Y":20,"Tags":[]}}', Summary: "A writable object with read only members (Serial, Origin)" },
            { Name: "Stamps", DisplayName: "Stamps", TypeName: "System.String[]", ElementTypeName: "System.String", Flags: T.Collection | T.Indexed, Max: "5", Default: '["A-1","B-2"]', ElementInst: { Flags: T.IsPrimitive | T.ReadOnly }, Summary: "A list of read only items: items can be added (default), removed and moved, not changed" },
        ],
    };

    /** A small object type for the nested / list demos. */
    const PointType = {
        TypeName: "SysWeaver.Demo.Point", DisplayName: "Point", Flags: T.IsObject, Summary: "A point with tags", Asm: "edit/index.js",
        Members: [
            { Name: "X", DisplayName: "X", TypeName: "System.Int32", Flags: T.IsPrimitive },
            { Name: "Y", DisplayName: "Y", TypeName: "System.Int32", Flags: T.IsPrimitive },
            { Name: "Tags", DisplayName: "Tags", TypeName: "System.String[]", ElementTypeName: "System.String", Flags: T.Collection | T.Indexed, Default: "\t" },
        ],
    };

    /** An object type with read only members (the paste / drop / json demos keep them). */
    const BoxType = {
        TypeName: "SysWeaver.Demo.Box", DisplayName: "Box", Flags: T.IsObject, Summary: "A box with a read only serial number and origin", Asm: "edit/index.js",
        Members: [
            { Name: "Name", DisplayName: "Name", TypeName: "System.String", Flags: T.IsPrimitive, Default: "Box" },
            { Name: "Serial", DisplayName: "Serial", TypeName: "System.String", Flags: T.IsPrimitive | T.ReadOnly, Default: "SN-0001", Summary: "Read only" },
            { Name: "Origin", DisplayName: "Origin", TypeName: "SysWeaver.Demo.Point", Flags: T.IsObject | T.ReadOnly, Default: '{"X":0,"Y":0,"Tags":["o"]}', Summary: "A read only nested object" },
            { Name: "Size", DisplayName: "Size", TypeName: "SysWeaver.Demo.Point", Flags: T.IsObject, Default: '{"X":10,"Y":20,"Tags":[]}', Summary: "A writable nested object" },
        ],
    };

    /**
     * Build a readable path of a change event (member names and keys following detail.Next).
     * @param {CustomEvent} ev - The EditChange event.
     * @returns {Array} [path, value].
     */
    function changePath(ev) {
        let mp = ev.detail.Member;
        let p = mp ? mp.Name : "*";
        let val = ev.detail.Value;
        for (; ;) {
            ev = ev.detail.Next;
            if (!ev)
                break;
            mp = ev.detail.Member;
            if (ev.detail.KeyName)
                p += ev.detail.KeyName;
            p += "." + (mp ? mp.Name : "*");
            val = ev.detail.Value;
        }
        return [p, val];
    }

    /**
     * The demo page.
     */
    const EditDemo = {
        /** @type {Array<{Name: string, Edit: ?Sw.Edit, Host: HTMLElement, Create: function}>} The editors. */
        Pages: [],
        /** @type {?object} The active page. */
        Active: null,
        /** @type {?string} Forced style (null = automatic). */
        Style: null,
        /** @type {boolean} Read only mode. */
        ReadOnly: false,

        /**
         * Page main: builds the toolbar, tabs and log.
         * @returns {Promise<void>}
         */
        async Main() {
            const done = Sw.Page.Loading();
            try {
                await Sw.Edit.Ready;
                Sw.Edit.AddType(PointType);
                Sw.Edit.AddType(BoxType);
                Sw.Edit.AddType(DemoType);
                const page = el("main.edit-demo.sw-page-form");
                document.body.append(page);
                const undo = new Sw.Button({ Icon: "undo", Title: _TF("Undo (Ctrl+Z)", "Tool tip of an undo button"), OnClick: () => this.Undo() });
                const redo = new Sw.Button({ Icon: "redo", Title: _TF("Redo (Ctrl+Y)", "Tool tip of a redo button"), OnClick: () => this.Redo() });
                this.UndoBtn = undo;
                this.RedoBtn = redo;
                const style = new Sw.Select({
                    Title: _TF("Layout", "Tool tip of a select that picks the editor layout"), Small: true, Value: "",
                    Options: [{ Value: "", Label: _TF("Automatic", "Automatic layout option") }, { Value: "Desktop", Label: _TF("Desktop", "Desktop layout option") }, { Value: "Mobile", Label: _TF("Mobile", "Mobile layout option") }],
                    OnChange: v => {
                        this.Style = v || null;
                        this.Rebuild();
                    },
                });
                const ro = new Sw.Toggle({ Label: _TF("Read only", "Label of a switch that makes the editor read only"), OnChange: v => {
                    this.ReadOnly = v;
                    this.Rebuild();
                } });
                const bar = el("div.edit-demo-bar", null,
                    undo.E, redo.E,
                    new Sw.Button({ Text: _TF("Validate", "Button that validates the edited object"), Icon: "check", OnClick: () => this.Validate() }).E,
                    new Sw.Button({ Text: _TF("JSON", "Button that shows the edited object as json"), Icon: "code", OnClick: () => this.ShowJson() }).E,
                    new Sw.Button({ Text: _TF("Dialog", "Button that edits a copy of the object in a dialog"), Icon: "window", OnClick: () => this.EditInDialog() }).E,
                    new Sw.Button({ Text: _TF("Reset", "Button that recreates the editor with default values"), Icon: "reset", OnClick: () => this.Rebuild(true) }).E,
                    style.E, ro.E);
                this.Log = el("ol.edit-demo-log", { attrs: { "aria-live": "polite" } });
                const tabs = new Sw.Tabs({ Fill: false, OnSelect: tab => this.Select(tab.Data) });
                const add = (name, title, create) => {
                    const host = el("div.edit-demo-host");
                    const p = { Name: name, Host: host, Create: create, Edit: null };
                    this.Pages.push(p);
                    p.Tab = tabs.Add({ Title: title, Content: host, Data: p });
                };
                add("server", _TF("Server type", "Tab with an editor for a server side type"), o => Sw.Edit.FromServer("SysWeaver.MicroService.EditTest", this.Style, o));
                add("client", _TF("All handlers", "Tab with an editor for a client side demo type"), o => Sw.Edit.FromServer(DemoType.TypeName, this.Style, o));
                add("bool", _TF("Shortcuts", "Tab with the boolean shortcut editor"), async () => {
                    const b = await Sw.Edit.Boolean("Enabled", true, "A single boolean (Sw.Edit.Boolean)", v => this.AddLog("Boolean", v), this.ReadOnly, true);
                    return b;
                });
                page.append(Sw.PageHeader({ Title: _TF("Object editor", "Title of the object editor test page"), Description: _TF("Every editor handler on a server and a client type: edit, validate, undo, copy and paste.", "Description of the object editor test page") }).E, bar, tabs.E,
                    el("h2.edit-demo-sub", { text: _TF("Changes", "Heading of the list of edit changes") }), this.Log);
                for (const p of this.Pages)
                    await this.Build(p);
                tabs.Select(this.Pages[0].Tab.Id ?? this.Pages[0].Tab);
                this.Select(this.Pages[0]);
            }
            catch (e) {
                Sw.Fail(e && e.message ? e.message : String(e));
            }
            finally {
                done();
                Sw.Page.Loaded();
            }
        },

        /**
         * Create the editor of a page.
         * @param {object} p - The page.
         * @param {boolean} [reset] - Start from the default value instead of the current one.
         * @returns {Promise<void>}
         */
        async Build(p, reset) {
            const o = new Sw.Edit.Options();
            o.ReadOnly = this.ReadOnly;
            const old = !reset && p.Edit ? p.Edit.GetObject() : undefined;
            let e;
            try {
                e = await p.Create(o);
            }
            catch (x) {
                e = null;
                p.Host.replaceChildren(el("p.edit-demo-note", { text: "" + (x && x.message ? x.message : x) }));
                return;
            }
            if (!e) {
                p.Host.replaceChildren(el("p.edit-demo-note", { text: _TF("The type is not available on this service (the test type only exists in debug builds).", "Shown when a test type is missing") }));
                p.Edit = null;
                return;
            }
            if (old !== undefined && p.Name !== "bool")
                await e.SetObject(old);
            e.Element.addEventListener("EditChange", ev => {
                const [path, val] = changePath(ev);
                this.AddLog(path, val);
            });
            e.UndoStack.OnChange(() => this.UpdateUndo());
            p.Edit = e;
            //  The sticky form footer (Sw.Edit.FormFooter): Reset, the change count, "Save" (validates; a real page would post the value
            //  and then call SetBaseline)
            const foot = new Sw.Edit.FormFooter(e, {
                Actions: [new Sw.Button({
                    Text: _TF("Save", "Button of the demo form footer that validates the value and marks it saved"), Icon: "check", Kind: "primary",
                    OnClick: async () => {
                        if (await e.Validate(true))
                            return;
                        foot.SetBaseline();
                        Sw.Toast(_TF("Valid (the demo does not store the value)", "Message of the demo form footer after Save"), { Kind: "ok", Duration: 2000 });
                    },
                })],
            });
            p.Host.replaceChildren(e.Element, foot.E);
            this.UpdateUndo();
        },

        /**
         * Rebuild every editor (layout / read only changes, reset).
         * @param {boolean} [reset] - Use default values.
         * @returns {Promise<void>}
         */
        async Rebuild(reset) {
            for (const p of this.Pages)
                await this.Build(p, reset);
        },

        /**
         * Select a page.
         * @param {object} p - The page.
         */
        Select(p) {
            this.Active = p;
            if (p && p.Edit)
                Sw.Edit.LastEdit = p.Edit;
            this.UpdateUndo();
        },

        /** Refresh the undo / redo buttons. */
        UpdateUndo() {
            const e = this.Active && this.Active.Edit;
            const s = e ? e.UndoStack : null;
            if (!this.UndoBtn)
                return;
            this.UndoBtn.SetDisabled(!s || !s.CanUndo(), s ? _TF("Nothing to undo yet", "Why the undo button is disabled: no change was made in the active editor") : _TF("Select an editor first", "Why the undo or redo button is disabled: no editor is active"));
            this.RedoBtn.SetDisabled(!s || !s.CanRedo(), s ? _TF("Nothing to redo: undo a change first", "Why the redo button is disabled: no change was undone in the active editor") : _TF("Select an editor first", "Why the undo or redo button is disabled: no editor is active"));
            this.UndoBtn.SetTitle(s && s.CanUndo() ? _TF("Undo (Ctrl+Z)", "Tool tip of an undo button") + ": " + s.UndoTitle() : _TF("Undo (Ctrl+Z)", "Tool tip of an undo button"));
            this.RedoBtn.SetTitle(s && s.CanRedo() ? _TF("Redo (Ctrl+Y)", "Tool tip of a redo button") + ": " + s.RedoTitle() : _TF("Redo (Ctrl+Y)", "Tool tip of a redo button"));
        },

        /** Undo on the active editor. */
        async Undo() {
            const e = this.Active && this.Active.Edit;
            if (e)
                await e.UndoStack.Undo();
        },

        /** Redo on the active editor. */
        async Redo() {
            const e = this.Active && this.Active.Edit;
            if (e)
                await e.UndoStack.Redo();
        },

        /** Validate the active editor (focus the failing member). */
        async Validate() {
            const e = this.Active && this.Active.Edit;
            if (!e)
                return;
            //  Validate(true) shows an error itself (under the field, a toast only when the field can't be seen)
            const err = await e.Validate(true);
            if (!err)
                Sw.Info(_TF("The object is valid", "Message shown when the edited object passed the validation"));
        },

        /** Show the json of the active editor. */
        ShowJson() {
            const e = this.Active && this.Active.Edit;
            if (!e)
                return;
            const j = JSON.stringify(e.GetObject(), null, 2);
            Sw.Cells.ShowJson(j, j, e.Type.DisplayName || e.Type.TypeName);
        },

        /** Edit a copy of the active object in a dialog (like the old PopUpEdit) and apply it on OK. */
        async EditInDialog() {
            const p = this.Active;
            const e = p && p.Edit;
            if (!e || p.Name === "bool")
                return;
            const o = new Sw.Edit.Options();
            o.Title = false;
            o.Border = false;
            const de = new Sw.Edit(e.Type, null, o);
            await de.SetObject(Sw.Edit.CloneValue(e.GetObject()));
            const ok = await Sw.Dialog.Show({
                Title: e.Type.DisplayName, Wide: true, Content: de.Element,
                Buttons: [{ Text: _TF("Cancel", "Text of a button that closes a dialog without changes"), Value: false }, { Text: _TF("Apply", "Text on the primary button of the edit in a dialog pop-up that applies the edited copy to the object being edited"), Kind: "primary", Value: true }],
            });
            if (ok) {
                const old = Sw.Edit.CloneValue(e.GetObject());
                const v = Sw.Edit.CloneValue(de.GetObject());
                await e.Invoke(async x => {
                    await x.SetObject(Sw.Edit.CloneValue(v));
                    x.NotifyChange();
                }, async x => {
                    await x.SetObject(Sw.Edit.CloneValue(old));
                    x.NotifyChange();
                }, _TF("Edited in dialog", "Undo title of a change made in a dialog"));
            }
        },

        /**
         * Add a line to the change log (and the console).
         * @param {string} path - Member path.
         * @param {*} val - The value.
         */
        AddLog(path, val) {
            const text = path + " = " + (typeof val === "object" ? JSON.stringify(val) : val);
            console.log(text);
            this.Log.prepend(el("li", { text: text }));
            while (this.Log.children.length > 50)
                this.Log.lastChild.remove();
        },
    };

    Sw.EditDemo = EditDemo;
})();
