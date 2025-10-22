const TypeMemberFlags = Object.freeze({
    Multiline: 1,
    Password: 2,
    AcceptNull: 4,
    Slider: 8,
    ReadOnly: 16,
    Collection: 32,
    Indexed: 64,
    IsEnum: 128,
    IsPrimitive: 256,
    IsObject: 512,
    Hide: 1024,
    IsFlags: 2048,
    DateUnspecified: 2048,
});


class TypeInfoCache {

    static Types = new Map();
    static InstanceTypes = new Map();

    static TypeLink = new URL(document.currentScript.src + "/../type.html").href;
    static Req = new URL(document.currentScript.src + "/../GetTypeInfo").href;
    static ReqNew = new URL(document.currentScript.src + "/../GetDefaultInstance").href;
    

    static async GetType(typeName) {
        const m = TypeInfoCache.Types;
        const t = m.get(typeName);
        if (t)
            return t;
        const r = await sendRequest(TypeInfoCache.Req, typeName, false);
        if (!r) {
            m.set(typeName, null);
            return null;
        }
        m.set(typeName, r);
        return r;
    }

    static async GetInstance(typeName) {

        const m = TypeInfoCache.InstanceTypes;
        const t = m.get(typeName);
        if (typeof t !== "undefined")
            return t[0] ? JSON.parse(t[1]) : null;
        const d = await sendRequest(TypeInfoCache.ReqNew, typeName, false);
        if (d === null) {
            m.set(typeName, [false, null]);
            return null;
        }
        m.set(typeName, [true, JSON.stringify(d)]);
        return d;
    }

}

function AddTextElement(target, text, title, className) {
    const tt = document.createElement("SysWeaver-Text");
    tt.innerText = text;
    tt.title = title ?? "";
    if (className)
        tt.classList.add(className);
    target.appendChild(tt);
}

function BuildLink(name, link) {
    link = link ? link : name;
    //const a = document.createElement("a");
    //a.innerText = name;
    //a.href = TypeInfoCache.TypeLink + "?q=" + link;
    //a.title = 'Click to show information about the type "' + link + '"';
    //a.target = "_self";
    const a = ValueFormat.createLink(TypeInfoCache.TypeLink + "?q=" + link, name, "_self",
        _T('Click to show information about the type "{0}"', link, "A tool tip description of a link that when followed will show information about a C# type.{0} is replaced with the C# name of the type")
    );
    return a;
}

function BuildTypeNameRec(name, link) {
    //  Non array or generic
    const nl = name.length;
    if (name.charAt(nl - 1) != ']') {
        if (!link)
            return [name];
        return [BuildLink(name)];
    }
    //  Check for array
    const ai = name.lastIndexOf('[');
    const ap = name.substring(ai, nl);
    const apl = ap.length - 1;
    let count = 1;
    for (let i = ai + 1; i < apl; ++i) {
        const c = ap.charAt(i);
        if (c === ',')
            ++count;
        if (c === ' ')
            ++count;
    }
    if (count === apl) {
        //  It's an array
        const ret = BuildTypeNameRec(name.substring(0, ai), link);
        ret.push(ap);
        return ret;
    }
    //  Should be a generic..
    const gi = name.indexOf('`');
    if (gi < 0) {
        // ..but wasn't
        if (!link)
            return [name];
        return [BuildLink(name)];
    }
    //  Was a generic
    let pos = name.indexOf('[');
    const baseName = name.substring(0, gi);
    const parts = link ? [BuildLink(baseName, name)] : [baseName];
    let start = pos;
    count = 0;
    for (let i = pos + 1; i < nl; ++i) {
        const c = name.charAt(i);
        if (c === '[') {
            if (count === 0) {
                parts.push(name.substring(start, i + 1).replace(' ', '').replace('[[', '<').replace(']', '').replace('[', ''));
                start = i + 1;
            }
            ++count;
        }
        if (c === ']') {
            --count;
            if (count === 0) {
                const r = BuildTypeNameRec(name.substring(start, i), true);
                const rl = r.length;
                for (let j = 0; j < rl; ++j)
                    parts.push(r[j]);
                start = i;
            }
        }
    }
    parts.push(name.substring(start, nl).replace(' ', '').replace(']]', '>'));
    return parts;
}

function AddTypeName(target, name, link, ext) {
    const parts = BuildTypeNameRec(name, link);
    if (!link) {
        if (ext) {
            const name = parts[0];
            const a = ValueFormat.createLink(ext, name, "_blank",
                _T('Click to show external information about the type "{0}" at:', name, "A tool tip description of a link that when followed will show information about a C# type on the official external site.{0} is replaced with the C# name of the type.The text is followed by the url on a new line") + '\n' + ext
            );
            parts[0] = a;
        }
    }
    const pl = parts.length;
    for (let i = 0; i < pl; ++i) {
        const p = parts[i];
        if (typeof p === "string") {
            AddTextElement(target, p);
            continue;
        }
        target.appendChild(p);
    }
}

function typeAddSection(target, title, text, className) {
    const section = document.createElement("SysWeaver-TypeSection");
    const t = document.createElement("SysWeaver-TypeTitle");
    t.innerText = title;
    if (className)
        section.classList.add(className);
    section.appendChild(t);
    //section.appendChild(document.createElement("br"));
    target.appendChild(section);
    if (text)
        section.appendChild(document.createTextNode(text));
    return section;
}


async function typeAdd(target, data) {

    if (typeof data === "string")
        data = await TypeInfoCache.GetType(data);
    const flags = data.Flags;
    const isCollection = (flags & TypeMemberFlags.Collection) != 0;
    const ext = data.Ext;
    let section = typeAddSection(target, _TF("Type name", "The title text of a section that contains the C# type name"), null, isCollection ? "TypeCol" : "TypeCode");
    const tn = data.TypeName;
    AddTypeName(section, tn, false, ext);
    if (ext) {
        const extLink = new ColorIcon("SysWeaverTypeIconExternal", "IconColorThemeMain", 24, 24,
            _T('Click to show external information about the type "{0}" at:', tn, "A tool tip description of a link that when followed will show information about a C# type on the official external site.{0} is replaced with the C# name of the type.The text is followed by the url on a new line") + '\n' + ext,
            ev => {
                Open(ext);
            });
        section.appendChild(extLink.Element);
    }

    if (data.Summary)
        typeAddSection(target, _TF("Summary", "The title text of a section that contains the C# code summary for the type"), data.Summary, "TypeItalic");
    if (data.Remarks)
        typeAddSection(target, _TF("Remarks", "The title text of a section that contains the C# code remarks for the type"), data.Remarks, "TypeItalic");
    if (data.Asm)
        typeAddSection(target, _TF("Assembly", "The title text of a section that contains the C# assembly name that defined the type"), data.Asm);

    if (isCollection)
        return;

    if ((flags & TypeMemberFlags.IsEnum) != 0) {
        const v = data.Default;
        if (!v)
            return;
        const values = v.split('>')[1].split('|');
        const valLen = values.length;
        if (valLen <= 0)
            return;

        const isFlags = (flags & TypeMemberFlags.IsFlags) != 0;
        section = typeAddSection(target, isFlags ?
            _TF("Enum flags", "The title text of a section that contains a table with the C# enum values as flags")
            :
            _TF("Enum values", "The title text of a section that contains a table with the C# enum values")
        );
        const table = document.createElement("table");
        table.classList.add("TypeEnumValues");
        section.appendChild(table);

        table.innerHTML = "<tr><th>" +
            makeHtmlSafe(_TF("VALUE", "The header text of a table column that contains the decimal numerical value of an enum value")) +
            "</th><th>" +
            makeHtmlSafe(_TF("NAME", "The header text of a table column that contains the name of an enum value")) +
            "</th><th>" +
            makeHtmlSafe(_TF("SUMMARY", "The header text of a table column that contains the C# code summary of an enum value")) +
            "</th></tr>";
        for (let i = 0; i < valLen; ++i) {
            const vals = values[i].split('<');
            const vl = vals.length;
            const r = table.insertRow();
            let v = parseInt(vals[0]);
            const hexVal = "0x" + v.toString(16);
            v = "" + v;
            const sum = vl > 2 ? vals[2] : "";
            const remark = vl > 3 ? vals[3] : "";
            let title = ValueFormat.AddNonNullLine("", _TF("Name", "The prefix of a row in a tool tip that ends with the name of an enum value") + ": ", vals[1]);
            if (isFlags) {
                title = ValueFormat.AddNonNullLine(title, _TF("Hex value", "The prefix of a row in a tool tip that ends with the hexadecimal numerical value of an enum value") + ": ", hexVal);
                title = ValueFormat.AddNonNullLine(title, _TF("Decimal value", "The prefix of a row in a tool tip that ends with the decimal numerical value of an enum value") + ": ", v);
            } else {
                title = ValueFormat.AddNonNullLine(title, _TF("Decimal value", "The prefix of a row in a tool tip that ends with the decimal numerical value of an enum value") + ": ", v);
                title = ValueFormat.AddNonNullLine(title, _TF("Hex value", "The prefix of a row in a tool tip that ends with the hexadecimal numerical value of an enum value") + ": ", hexVal);
            }
            title = ValueFormat.AddNonNullLine(title, _TF("Summary", "The prefix of a row in a tool tip that ends with the C# code summary of an enum value") + ": ", sum);
            title = ValueFormat.AddNonNullLine(title, _TF("Remarks", "The prefix of a row in a tool tip that ends with the C# code remarks of an enum value") + ": ", remark);
            r.title = title;           
            r.insertCell().innerText = isFlags ? hexVal : v;
            r.insertCell().innerText = vals[1];
            r.insertCell().innerText = sum ?? "";
        }
        return;
    }
    if ((flags & TypeMemberFlags.IsPrimitive) != 0)
        return;
    const members = data.Members;
    if (!members)
        return;
    const ml = members.length;
    if (ml > 0) {
        section = typeAddSection(target, _TF("Members", "The title text of a section that contains a table with the C# members of a C# type"));
        const table = document.createElement("table");
        table.classList.add("TypeMembers");
        section.appendChild(table);
        table.innerHTML = "<tr><th>" +
            makeHtmlSafe(_TF("TYPE", "The header text of a table column that contains the C# type name of a C# type member")) +
            "</th><th>" +
            makeHtmlSafe(_TF("NAME", "The header text of a table column that contains the name of a C# type member")) +
            "</th><th>" +
            makeHtmlSafe(_TF("SUMMARY", "The header text of a table column that contains the C# code summary of a C# type member")) +
            "</th></tr>";
        for (let i = 0; i < ml; ++i) {
            const m = members[i];
            const r = table.insertRow();
            const mtn = m.TypeName;

            let title = ValueFormat.AddNonNullLine("", _TF("Type name", "The prefix of a row in a tool tip that ends with the C# type name of a C# type member") + ": ", mtn);
            title = ValueFormat.AddNonNullLine(title, _TF("Name", "The prefix of a row in a tool tip that ends with the name of a C# type member") + ": ", m.Name);
            title = ValueFormat.AddNonNullLine(title, _TF("Summary", "The prefix of a row in a tool tip that ends with the C# code summary of a C# type member") + ": ", m.Summary);
            title = ValueFormat.AddNonNullLine(title, _TF("Remarks", "The prefix of a row in a tool tip that ends with the C# code remarks of a C# type member") + ": ", m.Remarks);
            if (mtn.indexOf('[') < 0) {
                //const a = document.createElement("a");
                //a.innerText = mtn;
                //a.href = TypeInfoCache.TypeLink + "?q=" + mtn;
                //a.title = 'Click to show information about the type "' + mtn + '"';
                //a.target = "_self";
                const a = ValueFormat.createLink(TypeInfoCache.TypeLink + "?q=" + mtn, mtn, "_self",
                    _T('Click to show information about the type "{0}"', mtn, "A tool tip description of a button that when clicked will show information about a C# type.{0} is replaced with the C# type name")
                );
                r.insertCell().appendChild(a);
            } else {
                AddTypeName(r.insertCell(), mtn, true);
            }
            AddTextElement(r.insertCell(), m.Name, title);
            AddTextElement(r.insertCell(), m.Summary ?? "", title);
        }
    }
}

async function typeMain() {

    const removeLoading = AddLoading();
    try {

        const ps = getUrlParams();
        const typeName = ps.get('q');
        if (!typeName) {
            Fail(_TF("No query parameter specified!", "An error message that is shown when a required parameter isn't present"), true);
            return;
        }
        document.title = _TF("TYPE", "The page title prefix, is followed by the C# type name") + ": " + typeName;
        let data;
        try {
            data = await TypeInfoCache.GetType(typeName);
        }
        catch (e) {
            Fail(_T('Failed to get information about the type "{0}".\n{1}', typeName, e, "An error message that is shown when the server failed to get information about an C# type.{0} is replaced with the C# type name.{1} is replaced with the java script excetion text"), true);
            return;
        }
        if (!data) {
            Fail(_T('Failed to get information about the type "{0}"', typeName, "An error message that is shown when the server failed to get information about an C# type.{0} is replaced with the C# type name"), true);
            return;
        }

        const target = document.createElement("SysWeaver-Page");
        target.classList.add("Wide");
        document.body.appendChild(target);
        await typeAdd(target, data);
    }
    catch (e) {
        Fail(e, true);
    }
    finally {
        removeLoading();
    }





}