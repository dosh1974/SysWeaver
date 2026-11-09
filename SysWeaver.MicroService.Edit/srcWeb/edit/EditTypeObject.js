/**
 * @file Type handler for nested object members (`Sw.Edit.Handlers.Object`, legacy global `EditTypeObject`).
 *
 * Loaded by Edit.js. The value cell shows an expand button and a short summary of the object (live updated); expanding
 * renders a child editor (no title row, sharing the undo stack) in the full width data row below. Expanding a null object
 * creates a default object as an undoable step (not possible when read only).
 */
(function () {
    "use strict";

    const E = Sw.Edit;
    const F = E.Flags;
    const Has = E.Util.Has;

    /**
     * Nested object handler.
     */
    class EditTypeObject extends E.Handler {
        /**
         * Accept objects with a matching $type, or without $type when every property is a member of the (loaded) type.
         * @param {*} obj - Candidate.
         * @param {object} member - The member.
         * @returns {boolean} True when acceptable.
         */
        IsOfType(obj, member) {
            if (E.IsPrimitive(member) || obj === null || typeof obj !== "object")
                return false;
            if (obj.$type)
                return obj.$type.split(",")[0] === member.TypeName;
            //  No $type (json copied from a text, ex: the JSON tab of explore/api.html): every property must be a member of the type
            const t = E.Types.get(member.TypeName);
            return !!t && E.IsObjectOf(obj, t);
        }

        /**
         * Validate: null only with AcceptNull; members recursively (path "member.sub", message "DisplayName/message").
         * @param {*} obj - The value.
         * @param {object} member - The member.
         * @param {object} options - Options.
         * @param {string} name - Path.
         * @returns {Promise<?Array<string>>} Null or [path, message].
         */
        async Validate(obj, member, options, name) {
            if (obj === null || obj === undefined)
                return E.Handler.NullCheck(obj, member, name);
            const type = await E.GetType(member.TypeName, member.ElementInst);
            if (!type)
                return null;
            const err = await E.ValidateType(type, obj, options, null);
            if (!err)
                return null;
            return [name + "." + err[0], (member.DisplayName || member.Name) + " › " + err[1]];
        }

        /**
         * Display text (JSON).
         * @param {*} obj - The value.
         * @returns {?string} The text.
         */
        ToString(obj) {
            return obj ? JSON.stringify(obj) : null;
        }

        /**
         * The metadata default: JSON, `{}` for "\t", null with AcceptNull, else `{}`.
         * @param {?object} member - The member.
         * @returns {?object} The default.
         */
        GetDefault(member) {
            if (member) {
                const def = member.Default;
                if (def === "\t")
                    return {};
                if (Has(def)) {
                    try {
                        return JSON.parse(def);
                    }
                    catch {
                    }
                }
                if ((member.Flags & F.AcceptNull) !== 0)
                    return null;
            }
            return {};
        }

        /**
         * JSON clone.
         * @param {*} def - Value.
         * @returns {*} Clone (null for falsy).
         */
        static CloneDef(def) {
            return def ? JSON.parse(JSON.stringify(def)) : null;
        }

        /**
         * Build the expand button, the summary and the data row.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {object} editContext - Layout context.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @returns {Promise<object>} The item (with FindItem for undo paths into the child editor).
         */
        async AddEditor(obj, editor, editContext, member, title, options) {
            const mn = member.Name;
            const cell = editContext.Element;
            const type = await E.GetType(member.TypeName, member.ElementInst);
            if (!type)
                return await E.Shared("Default", () => new E.Handlers.Default()).AddEditor(obj, editor, editContext, member, title, options);
            //  Read only: the options, the member, or the type (a read only type stays read only when nested)
            const readOnly = E.IsReadOnly(member, options) || (type.Flags & F.ReadOnly) !== 0;
            const childOpt = (options.Clone ? options.Clone() : Object.assign({}, options));
            childOpt.Title = false;
            childOpt.ReadOnly = readOnly;
            childOpt.Name = mn;
            //  Default: metadata, else the server default instance, else built from the member defaults.
            let def = this.GetDefault(member);
            let tip = null;
            if (def && Object.keys(def).length === 0) {
                let inst = null;
                try {
                    inst = await E.TypeInfo.GetInstance(member.TypeName);
                }
                catch {
                }
                def = inst || await E.CreateDefault(type, options);
            }
            tip = E.Util.AddLine(tip, _TF("Default value", "Header of a tool tip row with the default value of a property"), def ? JSON.stringify(def) : "null");
            if (obj[mn] === undefined)
                obj[mn] = EditTypeObject.CloneDef(def);
            cell.classList.add("EditObject");
            const data = editContext.CreateData();
            data.classList.add("DataRow");
            const row = editContext.Row || editContext.PrevRow;
            const info = Sw.el("span.sw-edit-info");
            E.SetToString(info, obj[mn], type);
            const btn = new Sw.Button({ Kind: "ghost", Small: true, Icon: "right", Class: "sw-edit-expand" });
            let expanded = false;
            let child = null;
            let internal = false;
            /** @type {?Sw.Button} "Set a value" next to a null object (editable only). */
            let setValueBtn = null;
            const setBtn = () => {
                const isNull = obj[mn] === null || obj[mn] === undefined;
                if (setValueBtn)
                    setValueBtn.E.hidden = !isNull;
                const can = !(readOnly && isNull);
                btn.SetDisabled(!can, _TF("The object is empty (null) and read only, so there is nothing to expand", "Why the expand button of a nested object in a read only editor is disabled"));
                btn.SetIcon(expanded ? "down" : "right");
                btn.E.setAttribute("aria-expanded", expanded ? "true" : "false");
                btn.SetTitle(!can ? _TF("Can't expand null object", "Tool tip of a disabled button that would expand a nested object that doesn't exist")
                    : expanded ? _TF("Collapse object", "Tool tip of a button that collapses (hides) a nested object")
                        : isNull ? _TF("Create a new object and expand it", "Tool tip of a button that creates a nested object and shows it")
                            : _TF("Expand object", "Tool tip of a button that expands (shows) a nested object"));
            };
            const toggle = async () => {
                expanded = !expanded;
                data.replaceChildren();
                if (expanded) {
                    internal = true;
                    try {
                        const needNew = obj[mn] === null || obj[mn] === undefined;
                        child = new E(type, editor.ForcedStyle, childOpt, editor, member);
                        await child.SetObject(needNew ? undefined : obj[mn]);
                        if (needNew) {
                            const n = child.GetObject();
                            await editor.Invoke(it => it && it.SetValue(n), it => it && it.SetValue(null), _TF("Create object", "Command text stored in a log when a nested object was created"), mn);
                        }
                        child.Element.addEventListener("EditChange", () => E.SetToString(info, obj[mn], type));
                        const entry = Sw.el("div.sw-edit-entry", null, Sw.el("div.sw-edit-entryvalue.SysWeaver-EditEntryValue", null, child.Element));
                        entry.Edit = child;
                        data.append(entry);
                    }
                    finally {
                        internal = false;
                    }
                }
                else
                    child = null;
                info.hidden = !!options.ExpandObjects && expanded && !!obj[mn];
                data.classList.toggle("EditorExpanded", expanded);
                row.classList.toggle("EditorExpanded", expanded);
                row.classList.toggle("sw-edit-expanded", expanded);
                setBtn();
                E.SetToString(info, obj[mn], type);
            };
            btn.O.OnClick = () => toggle();
            setBtn();
            info.addEventListener("click", ev => {
                if (Sw.BadClick(ev)) return;
                if (!btn.E.disabled && !Sw.HasSelectionIn(info))
                    toggle();
            });
            cell.append(btn.E, info);
            //  A null editable object: "Set a value" creates the default object (as the expand button does), Design v2 null token
            setValueBtn = readOnly ? null : new Sw.Button({
                Kind: "ghost", Small: true, Class: "sw-edit-setvalue", Text: _TF("Set a value", "Text of a button next to a null value that creates a value (the default object)"),
                Title: _TF("Create a new object and expand it", "Tool tip of a button that creates a nested object and shows it"),
                OnClick: () => expanded ? null : toggle(),
            });
            if (setValueBtn)
                cell.append(setValueBtn.E);
            setBtn();
            const item = E.Handler.Item(obj, editor, member, { Focus: btn.E, Title: tip, DefValue: def, DefValueText: def === null ? "null" : JSON.stringify(def) });
            item.DataRow = data;
            item.UpdateValue = async () => {
                if (internal)
                    return;
                E.SetToString(info, obj[mn], type);
                if (expanded)
                    await toggle();
                if (obj[mn] !== null && obj[mn] !== undefined)
                    await toggle();
                setBtn();
            };
            item.FindItem = async name => {
                if (!expanded)
                    await toggle();
                return child ? await child.FindItem(name) : null;
            };
            item.Expand = async on => {
                if (!!on !== expanded)
                    await toggle();
            };
            //  ExpandObjects: a nested object that exists starts open (forms for people)
            if (options.ExpandObjects && obj[mn] !== null && obj[mn] !== undefined)
                await toggle();
            return item;
        }
    }

    E.Handlers.Object = EditTypeObject;
    if (!window.EditTypeObject)
        window.EditTypeObject = EditTypeObject;
})();
