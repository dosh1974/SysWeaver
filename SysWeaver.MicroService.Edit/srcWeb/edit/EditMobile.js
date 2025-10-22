class EditMobile {
    constructor() {
    }


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
        if (!isLast) {
            const s = table.insertRow();
            s.classList.add("SeparatorRow");
            s.insertCell();
            ret.Hide = () => {
                r.classList.add("Hide");
                r2.classList.add("Hide");
                s.classList.add("Hide");
            };
            ret.Show = () => {
                r.classList.remove("Hide");
                r2.classList.remove("Hide");
                s.classList.remove("Hide");
            };
        } else {
            ret.Hide = () => {
                r.classList.add("Hide");
                r2.classList.add("Hide");
            };
            ret.Show = () => {
                r.classList.remove("Hide");
                r2.classList.remove("Hide");
            };
        }
        if (options.ReadOnly || ((member.Flags & TypeMemberFlags.ReadOnly) != 0))
            c.classList.add("EditReadOnly");
        return ret;
    }
}
