/**
 * @file Desktop layout strategy of the object editor (`Sw.Edit.Layouts.Desktop`, legacy global `EditDesktop`).
 *
 * Loaded by Edit.js. Both layouts create the same rows; EditDesktop.css lays them out as three columns
 * (title | icon / menu | value) and EditMobile.css as a title line above a full width value. Root editors with an
 * automatic style switch between the two by their width (class sw-edit-desktop / sw-edit-mobile), so a rotated phone or
 * a resized window re-flows without re-rendering.
 *
 * Row structure:
 * ```
 * div.sw-edit-row[data-member]          (one per member; class sw-edit-ro when read only, sw-hide when hidden)
 *   div.sw-edit-label > span.sw-edit-name   (title: click / long press / right click = member menu, drag source, drop target)
 *   div.sw-edit-icon                        (menu button when Options.MenuIcon, item buttons)
 *   div.sw-edit-value                       (the handler's editor)
 * div.sw-edit-data                       (optional full width row for nested editors: objects, collections)
 * ```
 */
(function () {
    "use strict";

    const el = Sw.el;

    /**
     * Desktop layout strategy (stateless). Called by Sw.Edit: AddTypeInfo (title row) and AddMember (one member).
     */
    class EditDesktop {
        /** @type {string} The style name. */
        get Name() {
            return "Desktop";
        }

        /**
         * Fill the title row (delegates to editor.SetTypeInfo).
         * @param {Sw.Edit} editor - The editor.
         * @param {HTMLElement} head - The title row element.
         * @param {object} type - The type.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         */
        AddTypeInfo(editor, head, type, title, options) {
            editor.SetTypeInfo(head, editor.Element, type, title, options);
        }

        /**
         * Add the row of one member and let its handler build the editor.
         * The handler gets `editContext` {PrevRow, Row (the member row), Element (value cell), Icon (icon cell),
         * CreateData() (creates the full width data row below the member and returns it)}.
         * @param {object} obj - The object.
         * @param {Sw.Edit} editor - The editor.
         * @param {HTMLElement} body - The rows container.
         * @param {object} member - The member.
         * @param {string} title - Tooltip.
         * @param {object} options - Options.
         * @param {boolean} isLast - Last visible member (unused, kept for the signature).
         * @returns {Promise<?object>} The handler's item extended with Hide(), Show(), Icon.
         */
        async AddMember(obj, editor, body, member, title, options, isLast) {
            const name = el("span.sw-edit-name", { text: member.DisplayName ?? member.Name });
            const label = el("div.sw-edit-label", null, name);
            if (options.TypeHints !== false) {
                //  The hint line: short type and constraints (Design v2: the type is no longer part of the label)
                const hint = Sw.Edit.MemberHint(member, options);
                if (hint && hint !== name.textContent)
                    label.append(Sw.Edit.HintElement(hint));
            }
            const icon = el("div.sw-edit-icon");
            const value = el("div.sw-edit-value");
            const row = el("div.sw-edit-row", { dataset: { member: member.Name } }, label, icon, value);
            const readOnly = Sw.Edit.IsReadOnly(member, options);
            if (readOnly) {
                row.classList.add("sw-edit-ro");
                label.classList.add("EditReadOnly");
            }
            body.append(row);
            let data = null;
            const ctx = {
                PrevRow: row,
                Row: row,
                Element: value,
                Icon: icon,
                ReadOnly: readOnly,
                CreateData: () => {
                    if (!data) {
                        data = el("div.sw-edit-data");
                        row.after(data);
                    }
                    return data;
                },
            };
            label.title = title || "";
            const ret = await Sw.Edit.SetMember(obj, editor, ctx, member, title, options, label);
            if (!ret) {
                row.remove();
                if (data)
                    data.remove();
                return null;
            }
            if (ret.Title)
                label.title = ret.Title + "\n" + label.title;
            name.BaseTitle = label.title;
            if (!ret.DataRow && data)
                ret.DataRow = data;
            ret.Icon = icon;
            ret.Row = row;
            /** Hide the member (row and data row). */
            ret.Hide = () => {
                row.classList.add("sw-hide");
                if (data)
                    data.classList.add("sw-hide");
            };
            /** Show the member again. */
            ret.Show = () => {
                row.classList.remove("sw-hide");
                if (data)
                    data.classList.remove("sw-hide");
            };
            return ret;
        }
    }

    Sw.Edit.Layouts.Desktop = new EditDesktop();
    Sw.Edit.LayoutDesktop = EditDesktop;
    if (!window.EditDesktop)
        window.EditDesktop = EditDesktop;
})();
