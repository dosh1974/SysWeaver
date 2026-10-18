/**
 * @file Mobile layout strategy of the object editor.
 *
 * Loaded on demand by `Edit.Init()` (Edit.js) via `includeJs` relative to Edit.js; never included directly by a page.
 * Defines the global class `EditMobile`. The `Edit` constructor instantiates it with `eval("new EditMobile()")`
 * when the editor style is `"Mobile"` (auto-selected for portrait windows that are narrow (<= 800 px) or when
 * `window.Mobile` is set).
 *
 * Dependencies (globals from other files): `Edit` (Edit.js: `Edit.SetMember` and `editor.SetTypeInfo`),
 * `TypeMemberFlags` (common.js / type.js).
 *
 * Layout: a two-column `<table>` per editor; each member uses a `HeaderRow` [member title | icon cell] followed by a
 * `ValueRow` whose single cell (colSpan 2) holds the editor. Row classes `TitleRow`, `SeparatorRow`, `HeaderRow`,
 * `ValueRow` and (toggled) `Hide` are styled by EditMobile.css.
 */

/**
 * Mobile layout strategy: lays a form out as a two-column table with each editor below its title.
 * Stateless; one instance is created per `Edit` instance (stored as `edit.Edit`) and called by
 * `Edit` (`AddTypeInfo` from the constructor, `AddMember` from `Edit.SetObject`).
 * Same duck-typed interface as `EditDesktop`.
 */
class EditMobile {
    /**
     * Creates the (stateless) layout strategy. Does nothing.
     */
    constructor() {
    }


    /**
     * Adds the title row of an editor: a `tr.TitleRow` with one cell spanning both columns, which is filled by
     * `editor.SetTypeInfo(c, table, type, title, options)`, followed by an empty `tr.SeparatorRow` (colSpan 2).
     * @param {Edit} editor - The editor the title row belongs to.
     * @param {HTMLTableElement} table - The editor's table; also the element that gets class `EditorCollapsed`
     *   when the title row's expand icon collapses the editor.
     * @param {object} type - The `TypeDesc` being edited.
     * @param {string} title - Tooltip text for the title.
     * @param {EditOptions} options - The editor options.
     * @returns {void}
     */
    AddTypeInfo(editor, table, type, title, options) {
        const r = table.insertRow();
        r.classList.add("TitleRow");
        const c = r.insertCell();
        c.colSpan = "2";
        editor.SetTypeInfo(c, table, type, title, options);
        const s = table.insertRow();
        s.classList.add("SeparatorRow");
        s.insertCell().colSpan = "2";
    }

    /**
     * Adds the rows for one member: a `tr.HeaderRow` (title cell with `SysWeaver-MemberTitle` = `member.DisplayName`,
     * icon cell) and a `tr.ValueRow` with one editor cell (colSpan 2), then calls `Edit.SetMember` which lets the
     * member's type handler build its editor in that cell.
     * The handler receives an `editContext` `{ PrevRow, Element, CreateData() }`: `PrevRow` is the value row,
     * `Element` the editor cell and `CreateData()` appends a new row with one colSpan-2 cell and returns that cell.
     * Afterwards prepends `item.Title` to the tooltip (stored as `BaseTitle` on the title text), sets `item.Icon`,
     * adds a `tr.SeparatorRow` unless `isLast`, attaches `item.Hide()` / `item.Show()` (toggle class `Hide` on the
     * header, value and separator rows) and adds class `EditReadOnly` to the title cell when read only.
     * A falsy item from `Edit.SetMember` is returned as null after the tooltip is set (no separator row is added).
     * @param {object} obj - The object being edited (member value is `obj[member.Name]`).
     * @param {Edit} editor - The owning editor.
     * @param {HTMLTableElement} table - The editor's table.
     * @param {object} member - The `TypeMemberDesc` of the member.
     * @param {string} title - Tooltip text for the member title.
     * @param {EditOptions} options - The editor options.
     * @param {boolean} isLast - True for the last visible member (no separator row is added).
     * @returns {Promise<object>} The item returned by the type handler's `AddEditor`, extended with `Hide`, `Show`
     *   and `Icon` (and by `Edit.SetMember` with `TypeHandler`, `TitleElement`).
     */
    async AddMember(obj, editor, table, member, title, options, isLast) {
        const r = table.insertRow();
        r.classList.add("HeaderRow");
        const c = r.insertCell();
        const text = document.createElement("SysWeaver-MemberTitle");
        c.appendChild(text);
        text.innerText = member.DisplayName;

        const icon = r.insertCell();

        const r2 = table.insertRow();
        r2.classList.add("ValueRow");

        const ec = r2.insertCell();
        ec.colSpan = "2";

        const editContext = {
            PrevRow: r2,
            Element: ec,
            CreateData: () => {
                const dataR = table.insertRow();
                const dataC = dataR.insertCell();
                dataC.colSpan = "2";
                return dataC;
            },
        };

        const ret = await Edit.SetMember(obj, editor, editContext, member, title, options, c);
        if (ret) {
            if (ret.Title)
                title = ret.Title + "\n" + title;
            ret.Icon = icon;
        }
        text.BaseTitle = title;
        c.title = title;
        if (!ret)
            return null;
        if (!isLast) {
            const s = table.insertRow();
            s.classList.add("SeparatorRow");
            s.insertCell();
            /**
             * Hides the member: adds class `Hide` to the header row, value row and separator row.
             * @returns {void}
             */
            ret.Hide = () => {
                r.classList.add("Hide");
                r2.classList.add("Hide");
                s.classList.add("Hide");
            };
            /**
             * Shows the member again: removes class `Hide` from the header row, value row and separator row.
             * @returns {void}
             */
            ret.Show = () => {
                r.classList.remove("Hide");
                r2.classList.remove("Hide");
                s.classList.remove("Hide");
            };
        } else {
            /**
             * Hides the (last) member: adds class `Hide` to the header row and value row.
             * @returns {void}
             */
            ret.Hide = () => {
                r.classList.add("Hide");
                r2.classList.add("Hide");
            };
            /**
             * Shows the (last) member again: removes class `Hide` from the header row and value row.
             * @returns {void}
             */
            ret.Show = () => {
                r.classList.remove("Hide");
                r2.classList.remove("Hide");
            };
        }
        if (options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) !== 0))
            c.classList.add("EditReadOnly");
        return ret;
    }
}
