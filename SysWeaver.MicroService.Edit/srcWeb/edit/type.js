/**
 * @file type.js - shared type metadata module of the object editor and script of the type information page
 * edit/type.html (?q=<type name>).
 *
 * Loaded by:
 * - Edit.Init() (Edit.js) via includeJs(current, "type.js"), so every page using the object editor gets it
 *   (e.g. explore/api.html, which also uses AddTypeName / AddTextElement from explore/api.js);
 * - type.html as a deferred script after common/common.js and common/tab.js; the page calls typeMain() on load.
 *
 * Globals defined: TypeMemberFlags, TypeInfoCache, AddTextElement, BuildLink, BuildTypeNameRec, AddTypeName,
 * typeAddSection, typeAdd, typeMain.
 * Dependencies (common/common.js): sendRequest, ValueFormat (createLink, AddNonNullLine), _T, _TF, makeHtmlSafe,
 * ColorIcon, Open, AddLoading, getUrlParams, Fail.
 * Server endpoints (relative to this script): GetTypeInfo and GetDefaultInstance (TypeService).
 * Styled by type.css (SysWeaver-TypeSection, SysWeaver-TypeTitle, TypeMembers / TypeEnumValues tables, ...).
 */

/**
 * Bit flags of a type or member description; mirrors the C# TypeMemberFlags enum (TypeMemberFlags.cs).
 * Note: IsFlags and DateUnspecified share the value 2048 (as on the server; IsFlags applies to enums,
 * DateUnspecified to date members). Some files still use the literal values 16 / 2 instead of the names.
 * @type {Readonly<{Multiline:number, Password:number, AcceptNull:number, Slider:number, ReadOnly:number,
 *   Collection:number, Indexed:number, IsEnum:number, IsPrimitive:number, IsObject:number, Hide:number,
 *   IsFlags:number, DateUnspecified:number}>}
 */
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


/**
 * Static cache of type descriptions and default instances fetched from the server (TypeService).
 * Never instantiated; all members are static. URLs are resolved relative to this script when it is evaluated.
 */
class TypeInfoCache {

    /** @type {Map<string, ?object>} Type descriptions (TypeDesc) by type name (null for failed lookups, but those are re-requested). */
    static Types = new Map();
    /** @type {Map<string, Array>} Default instances by type name as [found:boolean, json:?string]. */
    static InstanceTypes = new Map();

    /** @type {string} Absolute url of the type information page (edit/type.html). */
    static TypeLink = new URL(document.currentScript.src + "/../type.html").href;
    /** @type {string} Absolute url of the GetTypeInfo endpoint (edit/GetTypeInfo). */
    static Req = new URL(document.currentScript.src + "/../GetTypeInfo").href;
    /** @type {string} Absolute url of the GetDefaultInstance endpoint (edit/GetDefaultInstance). */
    static ReqNew = new URL(document.currentScript.src + "/../GetDefaultInstance").href;


    /**
     * Gets the type description of a type, using the cache. Sends sendRequest(Req, typeName, false) on a miss.
     * A falsy response is stored as null, but since cached values are tested for truthiness, null results are
     * requested again next time.
     * @param {string} typeName - .NET type name.
     * @returns {Promise<?object>} The type description (TypeDesc: TypeName, Flags, Members, Summary, ...) or null.
     */
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

    /**
     * Gets a server-created default instance of a type, using the cache. Sends
     * sendRequest(ReqNew, typeName, false) on a miss and caches the JSON string (or "not found" for null),
     * so callers get a fresh copy (JSON.parse) on cached calls; the first call returns the response object itself.
     * @param {string} typeName - .NET type name.
     * @returns {Promise<?object>} A default instance, or null when the server returned null.
     */
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

/**
 * Appends a SysWeaver-Text element with the given text.
 * @param {HTMLElement} target - Parent element.
 * @param {string} text - Text (innerText).
 * @param {string} [title=""] - Tooltip.
 * @param {string} [className] - Optional class to add.
 * @returns {void}
 */
function AddTextElement(target, text, title, className) {
    const tt = document.createElement("SysWeaver-Text");
    tt.innerText = text;
    tt.title = title ?? "";
    if (className)
        tt.classList.add(className);
    target.appendChild(tt);
}

/**
 * Creates a link (ValueFormat.createLink, target "_self") to the type information page for a type.
 * @param {string} name - Display text.
 * @param {string} [link=name] - Type name used in the "?q=" query.
 * @returns {HTMLElement} The link element.
 */
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

/**
 * Recursively splits a .NET reflection type name into display parts, rendering generics as "Base<Arg,...>".
 * - Names not ending in "]" are returned as is (or as a link).
 * - Names ending in an array suffix like "[]" return the parts of the element type followed by the suffix.
 *   (The rank check indexes the suffix with absolute name positions, so in practice only "[]" is detected.)
 * - Names containing "`" are generics "Base`n[[Arg1],[Arg2]]": the base name (linked to the full generic name)
 *   is followed by "<", the recursively built argument parts (always linked) separated by "," and a closing ">".
 * - Anything else ending in "]" is returned whole.
 * @param {string} name - Type name.
 * @param {boolean} link - When true type names become links (BuildLink), otherwise plain strings.
 * @returns {Array<string|HTMLElement>} Parts in display order (strings and link elements).
 */
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

/**
 * Renders a type name into target using BuildTypeNameRec: string parts become SysWeaver-Text elements, links are
 * appended as is. When link is false and ext is given, the first part becomes an external link (target "_blank").
 * Also used by explore/api.js.
 * @param {HTMLElement} target - Parent element.
 * @param {string} name - .NET type name.
 * @param {boolean} link - Link type names to type.html.
 * @param {string} [ext] - External documentation url (used only when link is false).
 * @returns {void}
 */
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

/**
 * Appends a SysWeaver-TypeSection with a SysWeaver-TypeTitle heading and optional text.
 * @param {HTMLElement} target - Parent element.
 * @param {string} title - Section title.
 * @param {?string} [text] - Optional text node content appended after the title.
 * @param {string} [className] - Optional class for the section (TypeCode, TypeCol, TypeItalic).
 * @returns {HTMLElement} The section element.
 */
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


/**
 * Renders the documentation of a type into target:
 * a "Type name" section (class TypeCol for collections, TypeCode otherwise; with a SysWeaverTypeIconExternal
 * ColorIcon that Open()s data.Ext when present), then Summary / Remarks (TypeItalic) and Assembly sections.
 * Collections stop there. Enums get an "Enum values" / "Enum flags" table (table.TypeEnumValues; hex values for
 * flags) decoded from data.Default (same encoding as EditTypeEnum). Non-primitive types with members get a
 * "Members" table (table.TypeMembers) whose type cells link to type.html.
 * @param {HTMLElement} target - Parent element.
 * @param {object|string} data - Type description (TypeDesc) or a type name to fetch via TypeInfoCache.GetType.
 * @returns {Promise<void>}
 */
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

/**
 * Entry point of type.html (body onload). Shows AddLoading(), reads the "q" URL parameter (type name), sets
 * document.title to "TYPE: <name>", fetches the description with TypeInfoCache.GetType and renders it with
 * typeAdd into a new SysWeaver-Page.Wide element. Missing parameter, fetch failures and null results are reported
 * with Fail(...); the loading indicator is always removed.
 * @returns {Promise<void>}
 */
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