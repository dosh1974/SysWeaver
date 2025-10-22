class EditDesktop {
    constructor() {
    }

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
            ret.Hide = () => {
                r.classList.add("Hide");
                s.classList.add("Hide");
            };
            ret.Show = () => {
                r.classList.remove("Hide");
                s.classList.remove("Hide");
            };
        } else {
            ret.Hide = () => {
                r.classList.add("Hide");
            };
            ret.Show = () => {
                r.classList.remove("Hide");
            };
        }

        ret.Icon = icon;
        if (options.ReadOnly || ((member.Flags & 16) != 0))
            c.classList.add("EditReadOnly");
        return ret;
    }

}
