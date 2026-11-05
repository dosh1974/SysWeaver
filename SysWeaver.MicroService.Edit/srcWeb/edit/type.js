/**
 * @file Type information: the TypeService client (`Sw.TypeInfo`, legacy `TypeInfoCache`), the type flags
 * (`Sw.TypeFlags`, legacy `TypeMemberFlags`), .NET type name rendering (`Sw.TypeDoc.AddTypeName`, legacy `AddTypeName`,
 * `AddTextElement`, `BuildLink`, `BuildTypeNameRec`) and the type documentation page edit/type.html (`Sw.TypeDoc.Main`,
 * legacy `typeMain`, `typeAdd`, `typeAddSection`).
 *
 * Loaded by Edit.js (every page with the object editor, ex: explore/api.html uses AddTypeName / AddTextElement) and by
 * type.html. Server contract: POST `edit/GetTypeInfo` (type name -> TypeDesc or null), POST `edit/GetDefaultInstance`
 * (type name -> json or null); type.html?q=<TypeName>.
 */
(function () {
    "use strict";

    const el = Sw.el;
    const ScriptUrl = (document.currentScript && document.currentScript.src) || (Sw.Url.Root + "edit/type.js");

    /**
     * Type / member flags (mirror of the C# TypeMemberFlags; DateUnspecified has its own bit, 4096).
     * @enum {number}
     */
    const TypeFlags = (Sw.Edit && Sw.Edit.Flags) || Object.freeze({
        Multiline: 1, Password: 2, AcceptNull: 4, Slider: 8, ReadOnly: 16, Collection: 32, Indexed: 64, IsEnum: 128,
        IsPrimitive: 256, IsObject: 512, Hide: 1024, IsFlags: 2048, DateUnspecified: 4096,
    });

    /**
     * Client of the TypeService: type descriptions and default instances, cached per page (also null results).
     */
    const TypeInfo = {
        /** @type {Map<string, ?object>} Type name -> TypeDesc. */
        Types: new Map(),
        /** @type {Map<string, ?string>} Type name -> json of the default instance (null = none). */
        InstanceTypes: new Map(),
        /** @type {string} Url of the type documentation page. */
        TypeLink: new URL("type.html", ScriptUrl).href,
        /** @type {string} Url of the type info api. */
        Req: new URL("GetTypeInfo", ScriptUrl).href,
        /** @type {string} Url of the default instance api. */
        ReqNew: new URL("GetDefaultInstance", ScriptUrl).href,

        /**
         * Get a type description (cached).
         * @param {string} typeName - .NET type name.
         * @returns {Promise<?object>} The TypeDesc or null.
         */
        async GetType(typeName) {
            const m = TypeInfo.Types;
            if (m.has(typeName))
                return m.get(typeName);
            const r = await Sw.Net.Send(TypeInfo.Req, typeName, { Post: true });
            m.set(typeName, r || null);
            return r || null;
        },

        /**
         * Get the default instance of a type (a fresh copy on every call).
         * @param {string} typeName - .NET type name.
         * @returns {Promise<?object>} The instance or null.
         */
        async GetInstance(typeName) {
            const m = TypeInfo.InstanceTypes;
            if (!m.has(typeName)) {
                const d = await Sw.Net.Send(TypeInfo.ReqNew, typeName, { Post: true });
                m.set(typeName, d === null || d === undefined ? null : JSON.stringify(d));
            }
            const j = m.get(typeName);
            return j === null ? null : JSON.parse(j);
        },
    };

    /**
     * Add a text element (`SysWeaver-Text`, css hook used by explore/api.css).
     * @param {HTMLElement} target - Parent.
     * @param {string} text - Text.
     * @param {?string} [title] - Tooltip.
     * @param {?string} [className] - Class.
     * @returns {HTMLElement} The element.
     */
    function AddTextElement(target, text, title, className) {
        const t = document.createElement("SysWeaver-Text");
        t.textContent = text ?? "";
        t.title = title ?? "";
        if (className)
            t.classList.add(className);
        target.append(t);
        return t;
    }

    /**
     * A link to the documentation page of a type (same window).
     * @param {string} name - Display text.
     * @param {string} [link] - Type name (default name).
     * @returns {HTMLAnchorElement} The link.
     */
    function BuildLink(name, link) {
        link = link || name;
        return Sw.Cells.CreateLink(TypeInfo.TypeLink + "?q=" + encodeURIComponent(link), name, "_self",
            _T('Show information about the type "{0}"', link, "A tool tip description of a link that when followed will show information about a C# type.{0} is replaced with the C# name of the type"));
    }

    /**
     * Split a .NET type name into display parts: generics as `Base<Arg, ...>`, arrays as `Name[]`; each type part is a link
     * (when link) or a text.
     * @param {string} name - The type name.
     * @param {boolean} link - Create links.
     * @returns {Array<string|HTMLElement>} The parts.
     */
    function BuildTypeNameRec(name, link) {
        const nl = name.length;
        if (name.charAt(nl - 1) !== "]")
            return [link ? BuildLink(name) : name];
        //  Array: Name[] / Name[,]
        const ai = name.lastIndexOf("[");
        const ap = name.substring(ai);
        if (/^\[[, ]*\]$/.test(ap)) {
            const r = BuildTypeNameRec(name.substring(0, ai), link);
            r.push(ap);
            return r;
        }
        //  Generic: Base`1[[Arg1],[Arg2]]
        const gi = name.indexOf("`");
        if (gi < 0)
            return [link ? BuildLink(name) : name];
        const baseName = name.substring(0, gi);
        const parts = [link ? BuildLink(baseName, name) : baseName, "<"];
        let depth = 0, start = -1, first = true;
        for (let i = name.indexOf("["); i < nl; ++i) {
            const c = name.charAt(i);
            if (c === "[") {
                ++depth;
                if (depth === 2)
                    start = i + 1;
            }
            else if (c === "]") {
                if (depth === 2 && start >= 0) {
                    if (!first)
                        parts.push(", ");
                    first = false;
                    const arg = name.substring(start, i).split(",")[0].trim();
                    for (const p of BuildTypeNameRec(arg, true))
                        parts.push(p);
                    start = -1;
                }
                --depth;
            }
        }
        parts.push(">");
        return parts;
    }

    /**
     * Add a formatted type name (generic arguments linked to their own pages).
     * @param {HTMLElement} target - Parent.
     * @param {string} name - Type name.
     * @param {boolean} link - Link the base type too.
     * @param {?string} [ext] - External documentation url (the base name links there when not linked).
     */
    function AddTypeName(target, name, link, ext) {
        const parts = BuildTypeNameRec(name, link);
        if (!link && ext && typeof parts[0] === "string") {
            const n = parts[0];
            parts[0] = Sw.Cells.CreateLink(ext, n, "_blank",
                _T('Show external information about the type "{0}" at:', n, "A tool tip description of a link that when followed will show information about a C# type on the official external site.{0} is replaced with the C# name of the type.The text is followed by the url on a new line") + "\n" + ext);
        }
        for (const p of parts) {
            if (typeof p === "string")
                AddTextElement(target, p);
            else
                target.append(p);
        }
    }

    /**
     * Add a page section.
     * @param {HTMLElement} target - Parent.
     * @param {string} title - Heading.
     * @param {?string} [text] - Text.
     * @param {?string} [className] - Class (TypeCol, TypeCode, TypeItalic).
     * @returns {HTMLElement} The section.
     */
    function typeAddSection(target, title, text, className) {
        const s = el("section.sw-type-section", { class: className || null }, el("h2.sw-type-title", { text: title }));
        if (text)
            s.append(el("div.sw-type-text", { text: text }));
        target.append(s);
        return s;
    }

    /**
     * Create a table with a header row.
     * @param {string} cls - Class.
     * @param {string[]} heads - Header texts.
     * @returns {HTMLTableElement} The table.
     */
    function table(cls, heads) {
        //  Reference documentation of one type: its members / enum values in source order, cells with links to the member types; no data rows
        //  to sort, search or act on (a static "doc table" building block is requested from the framework; this moves to it then)
        /* SwWeb:allow ux-lst-3 (reference documentation of one type, not a list of data rows; see above) */
        const t = el("table.sw-type-table", { class: cls });
        const h = t.createTHead().insertRow();
        for (const x of heads)
            h.append(el("th", { text: x, attrs: { scope: "col" } }));
        t.createTBody();
        return t;
    }

    /**
     * Render the documentation of a type.
     * @param {HTMLElement} target - Parent.
     * @param {object|string} data - TypeDesc or type name.
     * @returns {Promise<void>}
     */
    async function typeAdd(target, data) {
        if (typeof data === "string")
            data = await TypeInfo.GetType(data);
        if (!data)
            return;
        const flags = data.Flags;
        const isCollection = (flags & TypeFlags.Collection) !== 0;
        const ext = data.Ext;
        const sec = typeAddSection(target, _TF("Type name", "The title text of a section that contains the C# type name"), null, isCollection ? "TypeCol" : "TypeCode");
        const nameRow = el("div.sw-type-name");
        sec.append(nameRow);
        AddTypeName(nameRow, data.TypeName, false, ext);
        nameRow.append(new Sw.Button({
            Icon: "copy", Kind: "ghost", Small: true, Title: _TF("Copy the type name", "Tool tip of a button that copies a C# type name"),
            OnClick: () => Sw.Clipboard.Copy(data.TypeName),
        }).E);
        if (ext) {
            nameRow.append(new Sw.Button({
                Icon: "external", Kind: "ghost", Small: true,
                Title: _T('Show external information about the type "{0}" at:', data.TypeName, "A tool tip description of a link that when followed will show information about a C# type on the official external site.{0} is replaced with the C# name of the type.The text is followed by the url on a new line") + "\n" + ext,
                OnClick: () => window.open(ext, "_blank", "noopener"),
            }).E);
        }
        if (data.Summary)
            typeAddSection(target, _TF("Summary", "The title text of a section that contains the C# code summary for the type"), data.Summary, "TypeItalic");
        if (data.Remarks)
            typeAddSection(target, _TF("Remarks", "The title text of a section that contains the C# code remarks for the type"), data.Remarks, "TypeItalic");
        if (data.Asm)
            typeAddSection(target, _TF("Assembly", "The title text of a section that contains the C# assembly name that defined the type"), data.Asm);
        if (isCollection)
            return;
        if ((flags & TypeFlags.IsEnum) !== 0) {
            const v = data.Default;
            const gt = v ? v.indexOf(">") : -1;
            const values = gt >= 0 ? v.substring(gt + 1).split("|").filter(x => x) : [];
            if (!values.length)
                return;
            const isFlags = (flags & TypeFlags.IsFlags) !== 0;
            const s = typeAddSection(target, isFlags
                ? _TF("Enum flags", "The title text of a section that contains a table with the C# enum values as flags")
                : _TF("Enum values", "The title text of a section that contains a table with the C# enum values"));
            const t = table("TypeEnumValues", [
                _TF("VALUE", "The header text of a table column that contains the decimal numerical value of an enum value"),
                _TF("NAME", "The header text of a table column that contains the name of an enum value"),
                _TF("SUMMARY", "The header text of a table column that contains the C# code summary of an enum value")]);
            const body = t.tBodies[0];
            for (const val of values) {
                const p = val.split("<");
                const n = parseInt(p[0]);
                const hex = "0x" + (n >>> 0).toString(16);
                const r = body.insertRow();
                let tip = _TF("Name", "The prefix of a row in a tool tip that ends with the name of an enum value") + ": " + p[1];
                tip += "\n" + _TF("Decimal value", "The prefix of a row in a tool tip that ends with the decimal numerical value of an enum value") + ": " + n;
                tip += "\n" + _TF("Hex value", "The prefix of a row in a tool tip that ends with the hexadecimal numerical value of an enum value") + ": " + hex;
                if (p[2])
                    tip += "\n" + _TF("Summary", "The prefix of a row in a tool tip that ends with the C# code summary of an enum value") + ": " + p[2];
                if (p[3])
                    tip += "\n" + _TF("Remarks", "The prefix of a row in a tool tip that ends with the C# code remarks of an enum value") + ": " + p[3];
                r.title = tip;
                r.insertCell().textContent = isFlags ? hex : ("" + n);
                r.insertCell().textContent = p[1] || "";
                r.insertCell().textContent = p[2] || "";
            }
            s.append(el("div.sw-type-scroll", null, t));
            return;
        }
        if ((flags & TypeFlags.IsPrimitive) !== 0)
            return;
        const members = data.Members || [];
        if (!members.length)
            return;
        const s = typeAddSection(target, _TF("Members", "The title text of a section that contains a table with the C# members of a C# type"));
        const t = table("TypeMembers", [
            _TF("TYPE", "The header text of a table column that contains the C# type name of a C# type member"),
            _TF("NAME", "The header text of a table column that contains the name of a C# type member"),
            _TF("SUMMARY", "The header text of a table column that contains the C# code summary of a C# type member")]);
        const body = t.tBodies[0];
        for (const m of members) {
            const r = body.insertRow();
            let tip = _TF("Type name", "The prefix of a row in a tool tip that ends with the C# type name of a C# type member") + ": " + m.TypeName;
            tip += "\n" + _TF("Name", "The prefix of a row in a tool tip that ends with the name of a C# type member") + ": " + m.Name;
            if (m.Summary)
                tip += "\n" + _TF("Summary", "The prefix of a row in a tool tip that ends with the C# code summary of a C# type member") + ": " + m.Summary;
            if (m.Remarks)
                tip += "\n" + _TF("Remarks", "The prefix of a row in a tool tip that ends with the C# code remarks of a C# type member") + ": " + m.Remarks;
            AddTypeName(r.insertCell(), m.TypeName, true);
            AddTextElement(r.insertCell(), m.Name, tip);
            AddTextElement(r.insertCell(), m.Summary ?? "", tip);
        }
        s.append(el("div.sw-type-scroll", null, t));
    }

    /**
     * Main of type.html: shows the documentation of the type in the url parameter q.
     * @returns {Promise<void>}
     */
    async function typeMain() {
        const done = Sw.Page.Loading();
        const page = el("main.sw-type-page");
        document.body.append(page);
        //  The Design v2 page anatomy: a form page with the type name as the page header (a page about one record: not the menu name)
        const layout = Sw.PageLayout({ Type: "form", Main: page, MenuTitle: false, Title: _TF("Type", "Title of the type documentation page while the type is loaded") });
        try {
            const typeName = Sw.Url.Get("q");
            if (!typeName) {
                page.append(el("p.sw-type-error", { text: _TF("No query parameter specified!", "An error message that is shown when a required parameter isn't present") + " (q=..)" }));
                Sw.Fail(_TF("No query parameter specified!", "An error message that is shown when a required parameter isn't present"), true);
                return;
            }
            //  `q` is text from the url: plain, limited text (Sw.Page.SetLinkTitle) until the server knows the type (then its own name)
            Sw.Page.SetLinkTitle(_TF("TYPE", "The page title prefix, is followed by the C# type name") + ": " + typeName);
            let data;
            try {
                data = await TypeInfo.GetType(typeName);
            }
            catch (e) {
                Sw.Fail(_T('Failed to get information about the type "{0}"', typeName, "An error message that is shown when the server failed to get information about an C# type.{0} is replaced with the C# type name") + "\n" + (e.message || e), true);
                return;
            }
            if (!data) {
                page.append(el("p.sw-type-error", { text: _T('Failed to get information about the type "{0}"', typeName, "An error message that is shown when the server failed to get information about an C# type.{0} is replaced with the C# type name") }));
                return;
            }
            if (typeof data.TypeName === "string" && data.TypeName) {
                //  The server knows the type: its own name is the title (no longer text from the link)
                Sw.Page.TitleFromLink = null;
                layout.Header?.SetTitle(Sw.Page.LinkText(data.TypeName, 200));
                //  (after the header, which sets the document title to its own: the original title "TYPE: <name>")
                document.title = _TF("TYPE", "The page title prefix, is followed by the C# type name") + ": " + Sw.Page.LinkText(data.TypeName, 200);
            }
            await typeAdd(page, data);
        }
        catch (e) {
            Sw.Fail(e && e.message ? e.message : String(e), true);
        }
        finally {
            done();
            Sw.Page.Loaded();
        }
    }

    /** The documentation helpers (new API). */
    Sw.TypeDoc = { AddTextElement, BuildLink, BuildTypeNameRec, AddTypeName, AddSection: typeAddSection, Add: typeAdd, Main: typeMain };
    Sw.TypeInfo = TypeInfo;
    Sw.TypeFlags = TypeFlags;
    if (Sw.Edit)
        Sw.Edit.TypeInfo = TypeInfo;
    //  Legacy globals.
    const legacy = {
        TypeMemberFlags: TypeFlags, TypeInfoCache: TypeInfo, AddTextElement, BuildLink, BuildTypeNameRec, AddTypeName,
        typeAddSection, typeAdd, typeMain,
    };
    for (const k in legacy)
        if (!window[k])
            window[k] = legacy[k];
})();