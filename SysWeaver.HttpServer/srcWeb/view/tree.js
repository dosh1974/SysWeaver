/**
 * @file The tree viewer view/tree.html (`Sw.TreeView`): a json or xml file shown as a tree you can fold, filter and copy from. The page frame
 * (url parameters r / n / download, header actions Text, Reload, Download, states) is Sw.FileView (view.js).
 * Registered as the extension viewer of .json and .xml / .xsd / .xsl / .xslt files by the http server service (C# HttpServerServiceBase,
 * `view/tree.html?r={0}`). Url parameter `t=json|xml` sets the kind (default: the extension of the name, else the first character of the text).
 *
 * The tree (ARIA tree: ul[role=tree] > li[role=treeitem][aria-level][aria-expanded] > div.tv-row + ul[role=group]):
 * - Json: one item per value: the key (or index) and the value; objects and arrays fold and show their size ("{ 3 }", "[ 12 ]").
 * - Xml (DOMParser, a parse error is an error state with the parser's message): one item per element (tag with its attributes; an element with
 *   only text shows it inline: <name>text</name>), text, CDATA, comments and processing instructions.
 * - Children are drawn when their parent opens (large files stay fast); the first two levels start open (not a level with more than 200 items).
 * - Keys (one tab stop, the current item): Up / Down, Right (open / first child), Left (close / parent), Home / End, Enter or Space (fold),
 *   Ctrl+C (with nothing selected) copies the item: a json value as json (strings as they are), an xml element as xml.
 * - The tool bar: Filter (items whose key / tag / attribute / value contains the text, with their parents, open; matches marked; the count),
 *   Expand all (disabled with the reason above 20 000 items), Collapse all, and the status ("JSON · 1 234 items"). The path of an item
 *   ($.a[0].b, /root/item[2]) is its tool tip.
 */
(function () {
    "use strict";

    const el = Sw.el;

    /** Expand all draws every item: not above this many items. */
    const MaxExpandAll = 20000;

    /** Levels that start open (unless a level has more than OpenMaxKids items). */
    const OpenLevels = 2;

    /** A level with more items than this starts closed. */
    const OpenMaxKids = 200;

    /** At most this many matches are marked by the filter (the count says when there are more). */
    const MaxMatches = 5000;

    // ------------------------------------------------------------------ model

    /**
     * The model of a json value (recursive): { P parent, K key (string, index number, or null for the root), T type ("obj", "arr", "string",
     * "number", "boolean", "null"), V the value of a leaf, C children, J the value }.
     * @param {*} v - The value.
     * @param {?object} parent - The parent node.
     * @param {?(string|number)} key - The key / index.
     * @returns {object} The node.
     */
    function FromJson(v, parent, key) {
        const n = { P: parent, K: key, T: null, V: null, C: null, J: v };
        if (Array.isArray(v)) {
            n.T = "arr";
            n.C = v.map((x, i) => FromJson(x, n, i));
        }
        else if (v !== null && typeof v === "object") {
            n.T = "obj";
            n.C = Object.keys(v).map(k => FromJson(v[k], n, k));
        }
        else {
            n.T = v === null ? "null" : typeof v;
            n.V = v;
        }
        return n;
    }

    /**
     * The model of an xml node (recursive): { P, T ("el", "text", "cdata", "comment", "pi"), K tag / target, A attributes [[name, value]],
     * V inline text, C children, D the dom node }. White space only text is left out; an element with only text keeps it as V.
     * @param {Node} d - The dom node.
     * @param {?object} parent - The parent node.
     * @returns {?object} The node (null for white space).
     */
    function FromXml(d, parent) {
        switch (d.nodeType) {
            case 1: {
                const n = { P: parent, T: "el", K: d.nodeName, A: Array.from(d.attributes, a => [a.name, a.value]), V: null, C: null, D: d };
                const kids = [];
                for (const c of d.childNodes) {
                    const k = FromXml(c, n);
                    if (k)
                        kids.push(k);
                }
                if (kids.length === 1 && kids[0].T === "text")
                    n.V = kids[0].V;
                else if (kids.length)
                    n.C = kids;
                return n;
            }
            case 3: {
                const s = d.nodeValue.trim();
                return s ? { P: parent, T: "text", V: s, D: d } : null;
            }
            case 4:
                return { P: parent, T: "cdata", V: d.nodeValue, D: d };
            case 7:
                return { P: parent, T: "pi", K: d.target, V: d.data, D: d };
            case 8:
                return { P: parent, T: "comment", V: d.nodeValue.trim(), D: d };
            default:
                return null;
        }
    }

    /**
     * Parse a text into the root nodes of the tree.
     * @param {string} text - The text.
     * @param {string} kind - "json" or "xml".
     * @returns {object[]} The root nodes.
     */
    function Parse(text, kind) {
        if (kind === "json")
            return [FromJson(JSON.parse(text.charCodeAt(0) === 0xfeff ? text.substring(1) : text), null, null)];
        const doc = new DOMParser().parseFromString(text, "application/xml");
        const err = doc.getElementsByTagName("parsererror")[0];
        if (err)
            throw new Error((err.textContent || "").trim().replace(/\s+/g, " ") || "Parse error");
        const roots = [];
        for (const c of doc.childNodes) {
            const k = FromXml(c, null);
            if (k)
                roots.push(k);
        }
        return roots;
    }

    /**
     * The number of nodes of a tree.
     * @param {object[]} roots - The root nodes.
     * @returns {number} The count.
     */
    function CountNodes(roots) {
        let n = 0;
        const stack = roots.slice();
        while (stack.length) {
            const x = stack.pop();
            ++n;
            if (x.C)
                for (const c of x.C)
                    stack.push(c);
        }
        return n;
    }

    /**
     * The path of a node: json "$.a[0]['b c']", xml "/root/item[2]" (the position among the siblings of the same tag, from 1).
     * @param {object} n - The node.
     * @returns {string} The path.
     */
    function PathOf(n) {
        const parts = [];
        for (let x = n; x; x = x.P) {
            if (x.T === "el") {
                const same = x.P && x.P.C ? x.P.C.filter(s => s.T === "el" && s.K === x.K) : [];
                parts.unshift(x.K + (same.length > 1 ? "[" + (same.indexOf(x) + 1) + "]" : ""));
            }
            else if (x.K === null || x.K === undefined) {
                if (x.T !== "el" && x.P)
                    parts.unshift(x.T === "text" ? "text()" : x.T === "comment" ? "comment()" : "node()");
            }
            else if (typeof x.K === "number")
                parts.unshift("[" + x.K + "]");
            else
                parts.unshift(/^[A-Za-z_$][\w$]*$/.test(x.K) ? "." + x.K : "['" + x.K.replace(/\\/g, "\\\\").replace(/'/g, "\\'") + "']");
        }
        return n.T === "el" || (n.P && n.P.T === "el") || n.T === "text" || n.T === "comment" || n.T === "cdata" || n.T === "pi"
            ? "/" + parts.join("/")
            : "$" + parts.join("");
    }

    /**
     * The text searched by the filter for a node (key, tag, attributes, value).
     * @param {object} n - The node.
     * @returns {string} The text (lower case).
     */
    function SearchText(n) {
        let s = n.K === null || n.K === undefined ? "" : String(n.K);
        if (n.A)
            for (const [k, v] of n.A)
                s += "\n" + k + "\n" + v;
        if (n.V !== null && n.V !== undefined)
            s += "\n" + String(n.V);
        return s.toLowerCase();
    }

    /**
     * The text Ctrl+C copies for a node: json values as json (a string as it is), xml as xml.
     * @param {object} n - The node.
     * @returns {string} The text.
     */
    function CopyText(n) {
        if (n.D)
            return n.T === "el" ? new XMLSerializer().serializeToString(n.D) : String(n.V);
        if (n.T === "string")
            return n.V;
        return n.C ? JSON.stringify(n.J, null, 2) : String(n.V);
    }

    // ------------------------------------------------------------------ view

    /** The tree view of one file. */
    class TreeView {
        /**
         * @param {object[]} roots - The root nodes.
         * @param {string} label - The accessible name of the tree.
         */
        constructor(roots, label) {
            const t = this;
            t.Roots = roots;
            t.Q = "";
            t.Keep = null;
            t.Hits = null;
            t.E = el("ul.tv-tree", { attrs: { role: "tree", "aria-label": label } });
            t.E.addEventListener("keydown", e => t.Key(e));
            t.E.addEventListener("click", e => {
                const li = e.target.closest(".tv-item");
                if (!li || !t.E.contains(li))
                    return;
                t.Focus(li, false);
                //  (a click that ends a text selection does not fold)
                const sel = window.getSelection();
                if (sel && !sel.isCollapsed && String(sel).trim())
                    return;
                if (e.target.closest(".tv-row") && li.hasAttribute("aria-expanded"))
                    t.Toggle(li);
            });
            t.Render();
        }

        /** Draw the top level (closed or open as the defaults and the filter say). */
        Render() {
            const t = this;
            t.E.replaceChildren(...t.Kids(null).map(n => t.Item(n, 1)));
            const first = t.E.querySelector(".tv-item");
            if (first)
                first.tabIndex = 0;
        }

        /**
         * The shown children of a node (the roots for null): with a filter only those on the way to a match, unless none is (a match
         * opened by hand shows all its children).
         * @param {?object} n - The node.
         * @returns {object[]} The children.
         */
        Kids(n) {
            const all = n ? (n.C || []) : this.Roots;
            if (!this.Keep)
                return all;
            const k = all.filter(c => this.Keep.has(c));
            return k.length || !n ? k : all;
        }

        /**
         * Test if a node starts open.
         * @param {object} n - The node.
         * @param {number} level - Its level (1 = top).
         * @returns {boolean} Open.
         */
        StartsOpen(n, level) {
            if (!n.C || !n.C.length)
                return false;
            if (this.Keep)
                return n.C.some(c => this.Keep.has(c));
            return level <= OpenLevels && n.C.length <= OpenMaxKids;
        }

        /**
         * Create the item of a node (its children are drawn when it opens).
         * @param {object} n - The node.
         * @param {number} level - The level (1 = top).
         * @returns {HTMLElement} The li.
         */
        Item(n, level) {
            const li = el("li.tv-item", { attrs: { role: "treeitem", "aria-level": String(level), tabindex: "-1" } });
            li.Node = n;
            const row = el("div.tv-row", { title: PathOf(n) }, el("span.tv-tog", { attrs: { "aria-hidden": "true" } }), ...this.Label(n, false));
            li.append(row);
            if (n.C && n.C.length) {
                li.setAttribute("aria-expanded", "false");
                if (this.StartsOpen(n, level))
                    this.Open(li);
            }
            return li;
        }

        /**
         * Open an item (draws its children the first time).
         * @param {HTMLElement} li - The item.
         */
        Open(li) {
            if (!li.hasAttribute("aria-expanded"))
                return;
            if (!li.Group) {
                const level = parseInt(li.getAttribute("aria-level")) + 1;
                li.Group = el("ul.tv-group", { attrs: { role: "group" } }, ...this.Kids(li.Node).map(c => this.Item(c, level)));
                li.append(li.Group);
            }
            li.setAttribute("aria-expanded", "true");
            this.Summary(li);
        }

        /**
         * Close an item.
         * @param {HTMLElement} li - The item.
         */
        Close(li) {
            if (li.getAttribute("aria-expanded") !== "true")
                return;
            li.setAttribute("aria-expanded", "false");
            this.Summary(li);
        }

        /**
         * Open or close an item.
         * @param {HTMLElement} li - The item.
         */
        Toggle(li) {
            if (li.getAttribute("aria-expanded") === "true")
                this.Close(li);
            else
                this.Open(li);
        }

        /**
         * Redraw the row of an item after it opened / closed (xml shows the closing tag only when closed).
         * @param {HTMLElement} li - The item.
         */
        Summary(li) {
            const row = li.firstChild;
            const open = li.getAttribute("aria-expanded") === "true";
            row.replaceChildren(row.firstChild, ...this.Label(li.Node, open));
        }

        /**
         * Open every item (draws the whole tree).
         * @param {boolean} open - Open (else close).
         */
        All(open) {
            const t = this;
            if (!open) {
                for (const li of t.E.querySelectorAll(".tv-item[aria-expanded=true]"))
                    li.setAttribute("aria-expanded", "false");
                for (const li of t.E.querySelectorAll(".tv-item[aria-expanded]"))
                    t.Summary(li);
                return;
            }
            const stack = Array.from(t.E.children);
            while (stack.length) {
                const li = stack.pop();
                if (!li.hasAttribute("aria-expanded"))
                    continue;
                t.Open(li);
                for (const c of li.Group.children)
                    stack.push(c);
            }
        }

        /**
         * Filter the tree: items whose text contains q, with their parents, open; the matches marked.
         * @param {string} q - The text ("" = no filter).
         * @returns {{Count: number, More: boolean}} The number of matches.
         */
        Filter(q) {
            const t = this;
            q = (q || "").trim().toLowerCase();
            t.Q = q;
            if (!q) {
                t.Keep = t.Hits = null;
                t.Render();
                return { Count: 0, More: false };
            }
            const keep = new Set(), hits = new Set();
            let more = false;
            const stack = t.Roots.slice();
            while (stack.length) {
                const n = stack.pop();
                if (SearchText(n).includes(q)) {
                    if (hits.size >= MaxMatches) {
                        more = true;
                        break;
                    }
                    hits.add(n);
                    for (let x = n; x && !keep.has(x); x = x.P)
                        keep.add(x);
                }
                if (n.C)
                    for (let i = n.C.length - 1; i >= 0; --i)
                        stack.push(n.C[i]);
            }
            t.Keep = keep;
            t.Hits = hits;
            t.Render();
            return { Count: hits.size, More: more };
        }

        /**
         * A text with the filter's matches marked.
         * @param {string} s - The text.
         * @param {string} cls - The class of the span.
         * @returns {HTMLElement} The span.
         */
        Txt(s, cls) {
            const e = el("span." + cls);
            const q = this.Q;
            if (!q) {
                e.textContent = s;
                return e;
            }
            const low = s.toLowerCase();
            let i = 0;
            for (let j = low.indexOf(q); j >= 0; j = low.indexOf(q, i)) {
                if (j > i)
                    e.append(s.substring(i, j));
                e.append(el("mark.tv-hit", { text: s.substring(j, j + q.length) }));
                i = j + q.length;
            }
            if (i < s.length)
                e.append(s.substring(i));
            return e;
        }

        /**
         * The label of a node (after the fold mark).
         * @param {object} n - The node.
         * @param {boolean} open - The item is open.
         * @returns {Array<Node>} The parts.
         */
        Label(n, open) {
            const t = this;
            const out = [];
            const p = (s, cls) => el("span." + (cls || "tv-punct"), { text: s });
            switch (n.T) {
                case "el": {
                    out.push(p("<"), t.Txt(n.K, "tv-tag"));
                    for (const [k, v] of n.A)
                        out.push(" ", t.Txt(k, "tv-attr"), p("="), t.Txt('"' + v + '"', "tv-aval"));
                    if (n.V !== null)
                        out.push(p(">"), t.Txt(n.V, "tv-text"), p("</"), el("span.tv-tag", { text: n.K }), p(">"));
                    else if (!n.C)
                        out.push(p(" />"));
                    else if (open)
                        out.push(p(">"));
                    else
                        out.push(p(">"), el("span.tv-sum", {
                            text: " … " + (n.C.length === 1
                                ? _TF("1 item", "Size of a closed xml element in the tree viewer that has one child item")
                                : _T("{0} items", n.C.length, "Size of a closed xml element in the tree viewer. {0} is replaced by the number of child items")) + " … ",
                        }), p("</"), el("span.tv-tag", { text: n.K }), p(">"));
                    return out;
                }
                case "text":
                    return [t.Txt(n.V, "tv-text")];
                case "cdata":
                    return [p("<![CDATA["), t.Txt(n.V, "tv-text"), p("]]>")];
                case "comment":
                    return [t.Txt("<!-- " + n.V + " -->", "tv-comment")];
                case "pi":
                    return [p("<?"), t.Txt(n.K, "tv-tag"), " ", t.Txt(n.V, "tv-aval"), p("?>")];
            }
            //  Json: the key (an index in arrays), then the value or the size
            if (typeof n.K === "number")
                out.push(t.Txt(String(n.K), "tv-idx"), p(": "));
            else if (n.K !== null)
                out.push(t.Txt('"' + n.K + '"', "tv-key"), p(": "));
            switch (n.T) {
                case "obj":
                    out.push(el("span.tv-sum", { text: n.C.length ? "{ " + n.C.length + " }" : "{ }" }));
                    break;
                case "arr":
                    out.push(el("span.tv-sum", { text: n.C.length ? "[ " + n.C.length + " ]" : "[ ]" }));
                    break;
                case "string":
                    out.push(t.Txt(JSON.stringify(n.V), "tv-str"));
                    break;
                case "number":
                    out.push(t.Txt(String(n.V), "tv-num"));
                    break;
                case "boolean":
                    out.push(t.Txt(String(n.V), "tv-bool"));
                    break;
                default:
                    out.push(t.Txt("null", "tv-null"));
            }
            return out;
        }

        /**
         * The items that are shown (all parents open), in order.
         * @returns {HTMLElement[]} The items.
         */
        Shown() {
            const out = [];
            const walk = ul => {
                for (const li of ul.children) {
                    out.push(li);
                    if (li.Group && li.getAttribute("aria-expanded") === "true")
                        walk(li.Group);
                }
            };
            walk(this.E);
            return out;
        }

        /**
         * Make an item the current one (the tab stop) and focus it.
         * @param {HTMLElement} li - The item.
         * @param {boolean} [scroll] - Scroll it into view (default true).
         */
        Focus(li, scroll) {
            for (const x of this.E.querySelectorAll(".tv-item[tabindex='0']"))
                if (x !== li)
                    x.tabIndex = -1;
            li.tabIndex = 0;
            li.focus({ preventScroll: true });
            if (scroll !== false)
                li.firstChild.scrollIntoView({ block: "nearest" });
        }

        /**
         * The keys of the tree.
         * @param {KeyboardEvent} e - The event.
         */
        Key(e) {
            const t = this;
            const li = e.target.closest && e.target.closest(".tv-item");
            if (!li || e.altKey)
                return;
            if ((e.ctrlKey || e.metaKey) && (e.key === "c" || e.key === "C")) {
                const sel = window.getSelection();
                if (sel && !sel.isCollapsed && String(sel))
                    return;
                e.preventDefault();
                Sw.Clipboard.Copy(CopyText(li.Node), { Generic: true });
                return;
            }
            if (e.ctrlKey || e.metaKey)
                return;
            const open = li.getAttribute("aria-expanded") === "true";
            let go = null;
            switch (e.key) {
                case "ArrowDown":
                case "ArrowUp": {
                    const s = t.Shown();
                    go = s[s.indexOf(li) + (e.key === "ArrowDown" ? 1 : -1)] || null;
                    break;
                }
                case "ArrowRight":
                    if (li.hasAttribute("aria-expanded") && !open)
                        t.Open(li);
                    else if (open && li.Group.firstElementChild)
                        go = li.Group.firstElementChild;
                    break;
                case "ArrowLeft":
                    if (open)
                        t.Close(li);
                    else
                        go = li.parentElement.closest(".tv-item");
                    break;
                case "Home":
                    go = t.E.firstElementChild;
                    break;
                case "End": {
                    const s = t.Shown();
                    go = s[s.length - 1];
                    break;
                }
                case "Enter":
                case " ":
                    t.Toggle(li);
                    break;
                default:
                    return;
            }
            e.preventDefault();
            if (go)
                t.Focus(go);
        }
    }

    // ------------------------------------------------------------------ page

    const TreePage = {
        /**
         * Page main.
         * @returns {Promise<void>}
         */
        async Main() {
            const tp = (Sw.Url.Get("t") || "").toLowerCase();
            await Sw.FileView.Start({
                Kind: tp === "xml" ? "XML" : tp === "json" ? "JSON" : "", Type: "fill", Class: "tv-main", HostClass: "tv-card",
                Render: ctx => this.Render(ctx, tp),
            });
        },

        /**
         * Parse the text and show the tree (again).
         * @param {object} ctx - The Sw.FileView context.
         * @param {string} tp - The kind from the url ("json", "xml" or "").
         * @returns {Promise<void>}
         */
        async Render(ctx, tp) {
            const text = ctx.Text;
            const ext = (ctx.Name.split(".").pop() || "").toLowerCase();
            const kind = tp === "json" || tp === "xml" ? tp : ext === "json" ? "json" : /^(xml|xsd|xsl|xslt)$/.test(ext) ? "xml" : /^\s*</.test(text) ? "xml" : "json";
            const kindName = kind === "json" ? "JSON" : "XML";
            ctx.Header.SetDescription(ctx.Header.FromMenu ? ctx.Name : kindName);
            let roots;
            try {
                roots = Parse(text, kind);
            }
            catch (e) {
                ctx.Host.replaceChildren(Sw.StateView({
                    Kind: "error",
                    Title: kind === "json"
                        ? _TF("This is not valid JSON", "Title of the error state of the tree viewer when the json file can't be read")
                        : _TF("This is not valid XML", "Title of the error state of the tree viewer when the xml file can't be read"),
                    Text: _TF("The file can't be shown as a tree. Text shows the file as it is.", "Text of the error state of the tree viewer when the file can't be read"),
                    Details: e && e.message ? e.message : String(e),
                    Actions: [new Sw.Button({ Icon: "code", Text: _TF("Text", "Text on a button of a file viewer (markdown, html, json, xml, csv) that opens the file's text in the text editor"), OnClick: () => Sw.Page.Edit(ctx.Url, "_blank") })],
                }).E);
                ctx.Host.classList.add("tv-card-state");
                return;
            }
            ctx.Host.classList.remove("tv-card-state");
            const count = CountNodes(roots);
            const view = new TreeView(roots, ctx.Name);
            ctx.Tree = view;
            //  The tool bar: filter, expand / collapse all, the status
            const input = el("input.sw-input.tv-filter", {
                type: "search",
                placeholder: _TF("Filter", "Placeholder of the filter field of the tree viewer"),
                attrs: {
                    "aria-label": _TF("Filter the tree", "Accessible name of the filter field of the tree viewer"),
                    title: _TF("Show the items whose name or value contains the text (with their parents)", "Tool tip of the filter field of the tree viewer"),
                    spellcheck: "false", autocomplete: "off",
                },
            });
            const found = el("span.tv-found", { attrs: { "aria-live": "polite" } });
            let timer = 0;
            input.addEventListener("input", () => {
                clearTimeout(timer);
                timer = setTimeout(() => {
                    const r = view.Filter(input.value);
                    found.textContent = !input.value.trim() ? ""
                        : r.Count === 0 ? _TF("No matches", "Text next to the filter field of the tree viewer when nothing matches")
                            : r.More ? _T("{0}+ matches", r.Count, "Text next to the filter field of the tree viewer when there are more matches than are marked. {0} is the number marked")
                                : _T("{0} matches", r.Count, "Text next to the filter field of the tree viewer. {0} is the number of matching items");
                }, 200);
            });
            input.addEventListener("keydown", e => {
                if (e.key === "ArrowDown" || e.key === "Enter") {
                    const f = view.E.querySelector(".tv-hit");
                    const li = f ? f.closest(".tv-item") : view.E.querySelector(".tv-item");
                    if (li) {
                        e.preventDefault();
                        view.Focus(li);
                    }
                }
            });
            const tool = o => new Sw.Button(Object.assign({ Kind: "ghost", Class: "tv-tool" }, o));
            const expand = tool({
                Icon: "down", Text: _TF("Expand all", "Text of a button of the tree viewer that opens every item"),
                Title: _TF("Open every item", "Tool tip of the Expand all button of the tree viewer"),
                OnClick: () => view.All(true),
            });
            if (count > MaxExpandAll)
                expand.SetDisabled(true, _T("Too many items to open them all ({0}): open the parts you need, or use Filter", count, "Why the Expand all button of the tree viewer is disabled. {0} is the number of items"));
            const collapse = tool({
                Icon: "up", Text: _TF("Collapse all", "Text of a button of the tree viewer that closes every item"),
                Title: _TF("Close every item", "Tool tip of the Collapse all button of the tree viewer"),
                OnClick: () => view.All(false),
            });
            const status = el("span.tv-status", {
                text: kindName + " · " + _T("{0} items", count, "Status of the tree viewer: the number of items in the file. {0} is replaced by the number"),
                title: _TF("Keys: arrows move and open / close, Ctrl+C copies the item (json as json, xml as xml); the tool tip of an item is its path", "Tool tip of the status of the tree viewer"),
            });
            const bar = el("div.tv-bar", { attrs: { role: "toolbar", "aria-label": _TF("Tree tools", "Accessible name of the tool bar of the tree viewer") } },
                el("span.tv-filterbox", null, input, found), expand.E, collapse.E, el("span.tv-spacer"), status);
            ctx.Host.replaceChildren(bar, el("div.tv-scroll", null, view.E));
        },
    };

    Sw.TreeView = TreeView;
    Sw.TreeView.Parse = Parse;
    Sw.TreePage = TreePage;
    Sw.Page.Main(() => TreePage.Main());
})();
