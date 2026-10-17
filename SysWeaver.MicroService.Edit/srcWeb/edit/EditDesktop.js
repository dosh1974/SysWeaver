/**
 * @file Desktop layout strategy of the object editor.
 *
 * Loaded on demand by `Edit.Init()` (Edit.js) via `includeJs` relative to Edit.js; never included directly by a page.
 * Defines the global class `EditDesktop`. The `Edit` constructor instantiates it with `eval("new EditDesktop()")`
 * when the editor style is `"Desktop"` (the default for landscape windows / wide screens).
 *
 * Dependencies (globals from other files): `Edit` (Edit.js: `Edit.SetMember` and `editor.SetTypeInfo`).
 * The read-only member flag is tested with the literal `16` (= `TypeMemberFlags.ReadOnly`).
 *
 * Layout: a three-column `<table>` per editor: [member title | icon cell | editor cell]. Rows created here get the
 * CSS classes `TitleRow`, `SeparatorRow`, `ValueRow` and (toggled) `Hide`, styled by EditDesktop.css.
 */

/**
 * Desktop layout strategy: lays a form out as a three-column table (title, icon, editor).
 * Stateless; one instance is created per `Edit` instance (stored as `edit.Edit`) and called by
 * `Edit` (`AddTypeInfo` from the constructor, `AddMember` from `Edit.SetObject`).
 * Same duck-typed interface as `EditMobile`.
 */
class EditDesktop {
    /**
     * Creates the (stateless) layout strategy. Does nothing.
     */
    constructor() {
    }

    /**
     * Adds the title row of an editor: a `tr.TitleRow` with one cell spanning all three columns, which is filled by
     * `editor.SetTypeInfo(c, table, type, title, options)` (expand icon, menu icon, type title, drag/drop), followed by
     * an empty `tr.SeparatorRow`.
     * @param {Edit} editor - The editor the title row belongs to.
     * @param {HTMLTableElement} table - The editor's table (`editor.Table`); also passed as the element that gets
     *   class `EditorCollapsed` when the title row's expand icon collapses the editor.
     * @param {object} type - The `TypeDesc` being edited.
     * @param {string} title - Tooltip text for the title (type name, assembly, summary, remarks).
     * @param {EditOptions} options - The editor options.
     * @returns {void}
     */
    AddTypeInfo(editor, table, type, title, options) {
        const r = table.insertRow();
        r.classList.add("TitleRow");
        const c = r.insertCell();
        c.colSpan = "3";
        editor.SetTypeInfo(c, table, type, title, options);
        const s = table.insertRow();
        s.classList.add("SeparatorRow");
        s.insertCell().colSpan = "3";
    }

    /**
     * Adds the rows for one member: a `tr.ValueRow` with a title cell holding a `SysWeaver-MemberTitle`
     * (`member.DisplayName`), an icon cell and the editor cell, then calls `Edit.SetMember` which lets the member's
     * type handler build its editor in the editor cell.
     * The handler receives an `editContext` `{ PrevRow, Element, CreateData() }`: `PrevRow` is the value row,
     * `Element` the editor cell and `CreateData()` appends a new row with one cell spanning three columns (used by
     * object/collection handlers for their expandable content) and returns that cell.
     * Afterwards prepends `item.Title` (handler constraint text) to the tooltip, stores it as `BaseTitle` on the title
     * text, adds a `tr.SeparatorRow` unless `isLast`, attaches `item.Hide()` / `item.Show()` (toggle class `Hide` on the
     * value row and its separator), sets `item.Icon` to the icon cell and adds class `EditReadOnly` to the title cell
     * when the options or the member (flag 16 = ReadOnly) are read only.
     * @param {object} obj - The object being edited (member value is `obj[member.Name]`).
     * @param {Edit} editor - The owning editor.
     * @param {HTMLTableElement} table - The editor's table.
     * @param {object} member - The `TypeMemberDesc` of the member.
     * @param {string} title - Tooltip text for the member title.
     * @param {EditOptions} options - The editor options.
     * @param {boolean} isLast - True for the last visible member (no separator row is added).
     * @returns {Promise<object|null>} The item returned by the type handler's `AddEditor` (extended with
     *   `Hide`, `Show`, `Icon`, and by `Edit.SetMember` with `TypeHandler`, `TitleElement`), or null if the handler
     *   returned nothing (the value row is left in the table in that case).
     */
    async AddMember(obj, editor, table, member, title, options, isLast) {
        const r = table.insertRow();
        r.classList.add("ValueRow");
        const c = r.insertCell();
        const text = document.createElement("SysWeaver-MemberTitle");
        c.appendChild(text);
        text.innerText = member.DisplayName;
        const icon = r.insertCell();
        const editContext = {
            PrevRow: r,
            Element: r.insertCell(),
            CreateData: () => {
                const dataR = table.insertRow();
                const dataC = dataR.insertCell();
                dataC.colSpan = "3";
                return dataC;
            },
        };
        const ret = await Edit.SetMember(obj, editor, editContext, member, title, options, c);
        if (!ret)
            return null;
        if (ret.Title)
            title = ret.Title + "\n" + title;
        text.BaseTitle = title;
        c.title = title;
        if (!isLast) {
            const s = table.insertRow();
            s.classList.add("SeparatorRow");
            s.insertCell().colSpan = "3";
            /**
             * Hides the member: adds class `Hide` to the value row and its separator row.
             * @returns {void}
             */
            ret.Hide = () => {
                r.classList.add("Hide");
                s.classList.add("Hide");
            };
            /**
             * Shows the member again: removes class `Hide` from the value row and its separator row.
             * @returns {void}
             */
            ret.Show = () => {
                r.classList.remove("Hide");
                s.classList.remove("Hide");
            };
        } else {
            /**
             * Hides the (last) member: adds class `Hide` to the value row.
             * @returns {void}
             */
            ret.Hide = () => {
                r.classList.add("Hide");
            };
            /**
             * Shows the (last) member again: removes class `Hide` from the value row.
             * @returns {void}
             */
            ret.Show = () => {
                r.classList.remove("Hide");
            };
        }

        ret.Icon = icon;
        if (options.ReadOnly || ((member.Flags & 16) !== 0))
            c.classList.add("EditReadOnly");
        return ret;
    }

}
