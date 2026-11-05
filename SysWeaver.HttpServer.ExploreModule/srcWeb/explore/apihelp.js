/**
 * @file explore/apihelp.js - page script of explore/apihelp.html, "How to call APIs": how every API of a SysWeaver service is called with
 * GET or POST, how the argument is sent (json, other serializers, compression), how the result format is chosen (Accept, Accept-Encoding),
 * signing in and caching, with curl examples. Linked from the API inspector (explore/api.html, "How to call").
 *
 * Entry: `<body onload="Sw.ApiHelp.Main()">`. Url parameter `q` (optional): the API the user came from (the examples use it, a "Back to
 * the API" action opens explore/api.html for it).
 * The text is fixed; the lists of serializers and compression methods come from the service (`Api/debug/GetApiIoInfo`, ApiIoInfo
 * `{Input, Output, DefaultInput, DefaultOutput, Compression, GetCompression, MaxRequestSize, MaxDecompressedSize}`); without it (refused,
 * an older service) the page names the built in defaults.
 * Layout (Design v2, Sw.PageLayout "read"): the page header, then one card per topic. Styled by explore/apihelp.css.
 */
(function () {
    "use strict";

    const el = Sw.el;

    /**
     * A paragraph from a translated text with {0}, {1} ... replaced by elements (code samples, links).
     * @param {string} text - The text.
     * @param {...(Node|string)} parts - The elements for {0}, {1} ...
     * @returns {HTMLElement} The paragraph.
     */
    function para(text, ...parts) {
        const p = el("p.apihelp-p");
        let last = 0;
        text.replace(/\{(\d+)\}/g, (m, i, at) => {
            p.append(text.substring(last, at));
            const x = parts[parseInt(i, 10)];
            p.append(x instanceof Node ? x : code(String(x ?? "")));
            last = at + m.length;
            return m;
        });
        p.append(text.substring(last));
        return p;
    }

    /** @param {string} s - The text. @returns {HTMLElement} An inline code element. */
    function code(s) {
        return el("code.apihelp-code", { text: s });
    }

    /**
     * A code sample block with a Copy button.
     * @param {string} s - The text.
     * @returns {HTMLElement} The block.
     */
    function sample(s) {
        const b = new Sw.Button({
            Icon: "copy", Kind: "ghost", Small: true,
            Title: _TF("Copy to the clipboard", "Tool tip of the copy button of a code sample on the How to call APIs page"),
            OnClick: () => Sw.Clipboard.Copy(s, { Generic: true }),
        });
        return el("div.apihelp-sample", null, el("pre", { text: s }), b.E);
    }

    /**
     * A table.
     * @param {string[]} head - The column names.
     * @param {Array<Array<Node|string>>} rows - The cells.
     * @returns {HTMLElement} The table (in a scroll box).
     */
    function table(head, rows) {
        //  SwWeb:allow ux-lst-3 (a few fixed reference rows of a help text: nothing to sort, search or act on)
        const t = el("table.apihelp-table", null,
            el("thead", null, el("tr", null, ...head.map(h => el("th", { text: h, attrs: { scope: "col" } })))),
            el("tbody", null, ...rows.map(r => el("tr", null, ...r.map(c => el("td", null, c instanceof Node ? c : document.createTextNode(String(c ?? ""))))))));
        return el("div.apihelp-tablebox", null, t);
    }

    /**
     * A size in bytes as text ("64 MB"; "no limit" for zero or negative).
     * @param {number} n - Bytes.
     * @returns {string} The text.
     */
    function size(n) {
        if (!(n > 0))
            return _TF("no limit", "A request size limit that is not set (How to call APIs page)");
        return Sw.Format && Sw.Format.Bytes ? Sw.Format.Bytes(n) : n + " bytes";
    }

    /** The built in defaults (ApiHttpServerModuleParams) when the service doesn't answer GetApiIoInfo. */
    const Defaults = {
        Input: [{ Name: "json", Mime: "application/json", Extension: "json", IsText: true }],
        Output: [{ Name: "json", Mime: "application/json", Extension: "json", IsText: true }],
        DefaultInput: "json", DefaultOutput: "json",
        Compression: ["br", "deflate", "gzip"],
        GetCompression: [{ Char: "u", HttpCode: null }, { Char: "b", HttpCode: "br" }, { Char: "d", HttpCode: "deflate" }, { Char: "g", HttpCode: "gzip" }],
        MaxRequestSize: 64 << 20, MaxDecompressedSize: 64 << 20,
    };

    /**
     * Build the page.
     * @returns {Promise<void>}
     */
    async function Main() {
        const remove = Sw.Page.Loading();
        try {
            const q = Sw.Url.Get("q");
            const api = (q && Sw.Url.SitePath(q)) || "Api/debug/GetApiInfo";
            const full = Sw.Url.Root + api;
            let io = null, known = true;
            try {
                io = await Sw.Net.Get(Sw.Net.Api("debug/GetApiIoInfo"), { NoNavigate: true });
            }
            catch (e) {
                console.warn("Api/debug/GetApiIoInfo: " + (e && e.message ? e.message : e));
            }
            if (!io) {
                io = Defaults;
                known = false;
            }
            const actions = [];
            if (q)
                actions.push(new Sw.Button({
                    Text: _TF("Back to the API", "Text of a button on the How to call APIs page that opens the API inspector of the API the user came from"), Icon: "left",
                    OnClick: () => Sw.Page.Navigate("api.html?q=" + encodeURIComponent(q), "_self"),
                }));
            actions.push(new Sw.Button({
                Text: _TF("All APIs", "Text of a button on the API inspector page that opens the table of all APIs"), Icon: "table", Kind: "ghost",
                OnClick: () => Sw.Page.Navigate("table.html?q=" + encodeURIComponent("../Api/debug/ApiTable"), "_self"),
            }));
            const page = Sw.PageLayout({
                Type: "read", Class: "apihelp-page",
                Title: _TF("How to call APIs", "Title of the page that explains how to call the APIs of a service"),
                Description: _TF("Every API of this service can be called with GET or POST, from a browser, a script or any HTTP client.", "Description of the How to call APIs page"),
                Actions: actions,
            });
            const card = (title, ...body) => page.Add(Sw.Card({ Title: title, Body: body, Class: "apihelp-card" }));
            //  The short name (the file extension, ex: "json") of a serializer name
            const ext = (l, name) => (l.find(s => s.Name === name) || { Extension: name || "json" }).Extension;
            const sample1 = JSON.stringify({ Name: "value", Count: 3 });

            if (!known)
                page.Add(el("p.apihelp-note", { text: _TF("The service did not report its serializers and compression methods: the lists below are the defaults.", "Note on the How to call APIs page when the service doesn't answer Api/debug/GetApiIoInfo") }));

            card(_TF("The address", "Card title on the How to call APIs page: the url of an API"),
                para(_TF("An API is called at its url: the address of the service followed by the path of the API, ex: {0}. The API inspector shows the path of every API.", "Text on the How to call APIs page. {0} is an example url"), full),
                para(_TF("GET and POST work for every API. Use POST when the API takes an argument (the argument is then the body of the request) and GET for APIs without an argument or for a short argument in the url.", "Text on the How to call APIs page about GET and POST")));

            card(_TF("POST: the argument in the body", "Card title on the How to call APIs page"),
                para(_TF("The body is the argument, serialized with the format named by the {0} header. Without the header the {1} format is used.", "Text on the How to call APIs page. {0} is Content-Type, {1} the name of the default format"), "Content-Type", ext(io.Input, io.DefaultInput)),
                table([_TF("Format", "Column of a table of serializers on the How to call APIs page"), "Content-Type", _TF("Default", "Column of a table of serializers on the How to call APIs page: the format used without a header")],
                    io.Input.map(s => [s.Extension || s.Name, code(s.Mime), s.Name === io.DefaultInput ? "✓" : ""])),
                para(_TF("A body can be compressed: name the method in the {0} header. Accepted methods: {1}.", "Text on the How to call APIs page. {0} is Content-Encoding, {1} the list of compression methods"), "Content-Encoding", (io.Compression || []).join(", ")),
                para(_TF("Limits: a body may have {0} as sent and {1} after decompression; larger requests are refused with 413.", "Text on the How to call APIs page about the request size limits. {0} and {1} are sizes"),
                    el("strong", { text: size(io.MaxRequestSize) }), el("strong", { text: size(io.MaxDecompressedSize) })),
                sample("curl -X POST -H 'Content-Type: application/json' --data-binary '" + sample1 + "' '" + full + "'"));
            //  A deflated GET argument of the sample (the "_d" binary type), when the service and the browser have deflate
            const deflateChar = (io.GetCompression || []).find(x => x.HttpCode === "deflate");
            let deflated = null;
            if (deflateChar)
                try {
                    deflated = "?_" + deflateChar.Char + Sw.Util.Base64Encode(await Sw.Util.Deflate(new TextEncoder().encode(sample1)), true);
                }
                catch {
                    deflated = null;
                }

            const getRows = [
                [_TF("JSON", "Kind of a GET argument on the How to call APIs page"), code("?" + encodeURIComponent(sample1)), _TF("Url encoded json (a text starting with {, [, a quote, -, +, . or a digit).", "Explanation of the json GET argument on the How to call APIs page")],
                [_TF("Constant", "Kind of a GET argument on the How to call APIs page"), code("?true"), _TF("null, true, false (as json).", "Explanation of the constant GET argument on the How to call APIs page")],
            ];
            if (io.Input.some(s => s.Mime === "application/xml"))
                getRows.push([_TF("XML", "Kind of a GET argument on the How to call APIs page"), code("?<Arg>...</Arg>"), _TF("A text starting with <.", "Explanation of the xml GET argument on the How to call APIs page")]);
            if (io.Input.some(s => s.Mime === "application/x-www-form-urlencoded"))
                getRows.push([_TF("Form", "Kind of a GET argument on the How to call APIs page"), code("?Name=value&Count=3"), _TF("Members as name=value pairs (a text starting with a letter).", "Explanation of the form GET argument on the How to call APIs page")]);
            getRows.push([_TF("Binary", "Kind of a GET argument on the How to call APIs page"), code("?_g<base64url>"), _TF("\"_\", a type character (below), then the data as base64url (padding optional).", "Explanation of the binary GET argument on the How to call APIs page")]);
            card(_TF("GET: the argument in the url", "Card title on the How to call APIs page"),
                para(_TF("The argument follows the {0} directly (it is not a name=value parameter). The first character tells the format:", "Text on the How to call APIs page. {0} is ?"), "?"),
                table([_TF("Kind", "Column of a table on the How to call APIs page"), _TF("Example", "Column of a table on the How to call APIs page"), _TF("Notes", "Column of a table on the How to call APIs page")], getRows),
                para(_TF("Binary argument types: a lower case character means the data is json, an upper case character is followed by the name of a format and a comma, ex: {0}.", "Text on the How to call APIs page about binary GET arguments. {0} is an example"), "?_Gbson,<base64url>"),
                table([_TF("Character", "Column of a table on the How to call APIs page"), _TF("Compression", "Column of a table on the How to call APIs page")],
                    (io.GetCompression || []).map(x => [code(x.Char), x.HttpCode || _TF("none", "No compression (How to call APIs page)")])),
                para(_TF("With curl, add {0} so that {1} and {2} in the json are not read as url patterns.", "Text on the How to call APIs page. {0} is --globoff, {1} and {2} are brackets"), "--globoff", "{ }", "[ ]"),
                sample("curl --globoff '" + full + "?" + encodeURIComponent(sample1) + "'"),
                ...(deflated ? [para(_TF("The same argument deflated (the {0} type):", "Text on the How to call APIs page before a curl sample with a deflated GET argument. {0} is the type, ex: _d"), "_" + deflateChar.Char), sample("curl '" + full + deflated + "'")] : []));

            card(_TF("The result", "Card title on the How to call APIs page"),
                para(_TF("The result is serialized with the first format of the {0} header that the service has, else with {1}.", "Text on the How to call APIs page. {0} is Accept, {1} the default format"), "Accept", ext(io.Output, io.DefaultOutput)),
                table([_TF("Format", "Column of a table of serializers on the How to call APIs page"), "Accept", _TF("Type", "Column of a table of serializers on the How to call APIs page: text or binary")],
                    io.Output.map(s => [s.Extension || s.Name, code(s.Mime), s.IsText ? _TF("text", "A text format (How to call APIs page)") : _TF("binary", "A binary format (How to call APIs page)")])),
                para(_TF("Results are compressed when the {0} header allows it; the API inspector shows the methods of each API. With curl use {1}.", "Text on the How to call APIs page. {0} is Accept-Encoding, {1} is --compressed"), "Accept-Encoding", "--compressed"),
                para(_TF("Some APIs return a file (an image, a text, a download) instead of serialized data: the API inspector shows its type. Save it with {0}.", "Text on the How to call APIs page about raw results. {0} is a curl option"), "-o name"),
                sample("curl --compressed -H 'Accept: application/json' '" + full + "'"));

            card(_TF("Signing in", "Card title on the How to call APIs page"),
                para(_TF("Open APIs need no sign in. Other APIs need a signed in user (and some a role, shown on the API inspector): without one the service answers 401 (with the sign in page in the {0} header), without the role 403.", "Text on the How to call APIs page about signing in. {0} is X-Login-Url"), "X-Login-Url"),
                para(_TF("A browser sends the session cookie of the signed in user. From a script, send the same cookie ({0}) or, where the service has API keys, the key as {1}.", "Text on the How to call APIs page. {0} is a curl option, {1} a header"), "-b 'name=value'", "Authorization: Bearer <key>"));

            card(_TF("Caching", "Card title on the How to call APIs page"),
                para(_TF("\"Client cache\" on the API inspector: the result says how long a browser may keep it. \"Server cache\": the service keeps the result of the same call (the same argument) for that time, so a repeated call can return an older result.", "Text on the How to call APIs page about caching")));

            document.title = _TF("How to call APIs", "Title of the page that explains how to call the APIs of a service");
        }
        catch (e) {
            Sw.Fail(e && e.message ? e.message : String(e), true);
        }
        finally {
            remove();
        }
    }

    Sw.ApiHelp = { Main };
})();
