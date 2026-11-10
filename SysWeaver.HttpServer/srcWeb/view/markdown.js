/**
 * @file The markdown viewer view/markdown.html (`Sw.MarkdownView`): a markdown file shown as a document, rendered with the shared
 * converter `Sw.Markdown` (common/marked.js; sanitized: allow listed tags and attributes, safe links, images from other sites behind a
 * "Show image" placeholder). The page frame (url parameters r / n / download, header actions Text, Reload, Download, states) is
 * Sw.FileView (view.js).
 * Registered as the extension viewer of .md / .markdown files by the http server service (C# HttpServerServiceBase, `view/markdown.html?r={0}`).
 * Relative links and images of the document resolve against the folder of the file when r is a plain file url (no query); an api url
 * (with a query) says nothing about where the file's images are, so they stay as written.
 * Width: a grip on the right edge of the document card (a vertical separator: drag, or Left / Right (Shift: larger steps), Home = the
 * narrowest, End = the full width, Enter or a double click = the default width) sets the width of the page; remembered in localStorage
 * `SysWeaver.MarkdownView.Width` (px, or "full"). Not on phones (the document is edge to edge).
 */
(function () {
    "use strict";

    const el = Sw.el;

    /** The local storage key of the chosen width. */
    const WidthKey = "SysWeaver.MarkdownView.Width";

    /** The narrowest document width (px). */
    const MinWidth = 320;

    /**
     * The base url of the files a document refers to: the folder of the read url when it is a plain file url, null for an api url with a
     * query (its folder says nothing about the file).
     * @param {string} url - The read url (r).
     * @returns {?string} The absolute base url ending with "/".
     */
    function DocBase(url) {
        if (!url || url.includes("?"))
            return null;
        try {
            return new URL(".", new URL(url, location.href)).href;
        }
        catch {
            return null;
        }
    }

    /**
     * Resolve the relative links and images of the rendered document against the file's folder (this page is view/markdown.html). Runs on
     * inert content (a template), so nothing loads from the wrong place first; images keep the markdown rule (Sw.Markdown.CanAutoLoadImage:
     * others get the "Show image" placeholder).
     * @param {DocumentFragment} root - The content.
     * @param {?string} base - The base url (DocBase), null = keep.
     */
    function Rebase(root, base) {
        if (!base)
            return;
        const rel = v => !!v && !/^\s*(?:[a-z][a-z0-9+.-]*:|\/|#|\?)/i.test(v);
        const abs = v => {
            try {
                return new URL(v, base).href;
            }
            catch {
                return v;
            }
        };
        for (const a of root.querySelectorAll("a[href]")) {
            const h = a.getAttribute("href");
            if (rel(h))
                a.setAttribute("href", abs(h));
        }
        const M = Sw.Markdown;
        for (const i of root.querySelectorAll("img[src], .sw-md-img-off[data-sw-src]")) {
            const img = i.localName === "img";
            const s = i.getAttribute(img ? "src" : "data-sw-src");
            if (!rel(s))
                continue;
            const u = abs(s);
            const alt = i.getAttribute(img ? "alt" : "data-sw-alt") || "";
            if (M.CanAutoLoadImage(u)) {
                const n = document.createElement("img");
                n.alt = alt;
                n.referrerPolicy = "no-referrer";
                n.setAttribute("src", u);
                i.replaceWith(n);
            }
            else
                i.replaceWith(M.ImagePlaceholder(u, alt));
        }
    }

    /**
     * The width grip of the document card: a vertical separator on its right edge that sets the width of the page (main), remembered.
     * @param {HTMLElement} main - The page container (main.sw-page-read).
     * @param {HTMLElement} card - The document card.
     */
    function WidthGrip(main, card) {
        const grip = el("div.md-grip", {
            attrs: {
                role: "separator", tabindex: "0", "aria-orientation": "vertical",
                "aria-label": _TF("Document width", "Accessible name of the grip on the right edge of the markdown viewer's document that changes its width"),
                title: _TF("Drag to change the width of the document (arrow keys: smaller / larger, End: the full width, double click or Enter: the default width)", "Tool tip of the grip on the right edge of the markdown viewer's document that changes its width"),
            },
        }, el("span.md-grip-bar"));
        card.append(grip);
        //  The default width: the page's own max width (45rem); the chosen one is set as max-width on main
        const max = () => document.documentElement.clientWidth;
        const pad = () => main.getBoundingClientRect().width - card.getBoundingClientRect().width;
        const apply = (w, save) => {
            if (w === "full")
                main.style.maxWidth = "none";
            else if (w > 0)
                main.style.maxWidth = Math.round(Math.max(MinWidth, Math.min(max(), w)) + pad()) + "px";
            else
                main.style.maxWidth = "";
            const cw = Math.round(card.getBoundingClientRect().width);
            grip.setAttribute("aria-valuenow", String(cw));
            grip.setAttribute("aria-valuemin", String(MinWidth));
            grip.setAttribute("aria-valuemax", String(max()));
            grip.setAttribute("aria-valuetext", w === "full"
                ? _TF("Full width", "Value of the width grip of the markdown viewer when the document uses the full width of the page")
                : _T("{0} pixels", cw, "Value of the width grip of the markdown viewer. {0} is the width of the document in pixels"));
            if (save)
                try {
                    Sw.Storage.Local.Set(WidthKey, w === "full" ? "full" : w > 0 ? String(Math.round(w)) : "");
                }
                catch {
                    //  (not remembered)
                }
        };
        let stored = null;
        try {
            stored = Sw.Storage.Local.Get(WidthKey);
        }
        catch {
            stored = null;
        }
        apply(stored === "full" ? "full" : parseInt(stored) || 0, false);
        //  Drag: the card grows by the pointer's move; a centered page grows on both sides, so by twice the move
        grip.addEventListener("pointerdown", e => {
            if (e.button !== 0)
                return;
            e.preventDefault();
            grip.setPointerCapture(e.pointerId);
            grip.classList.add("md-grip-active");
            const r = main.getBoundingClientRect();
            const centered = Math.abs((r.left + r.right) / 2 - max() / 2) < 2 && r.left > 1;
            const f = centered ? 2 : 1;
            const x0 = e.clientX, w0 = card.getBoundingClientRect().width;
            const move = ev => apply(w0 + f * (ev.clientX - x0), false);
            const up = ev => {
                grip.removeEventListener("pointermove", move);
                grip.removeEventListener("pointerup", up);
                grip.removeEventListener("pointercancel", up);
                grip.classList.remove("md-grip-active");
                const w = w0 + f * (ev.clientX - x0);
                apply(w >= max() - 4 ? "full" : w, true);
            };
            grip.addEventListener("pointermove", move);
            grip.addEventListener("pointerup", up);
            grip.addEventListener("pointercancel", up);
        });
        grip.addEventListener("dblclick", () => apply(0, true));
        grip.addEventListener("keydown", e => {
            const w = card.getBoundingClientRect().width;
            const step = e.shiftKey ? 160 : 40;
            let n = null;
            if (e.key === "ArrowLeft" || e.key === "ArrowDown")
                n = w - step;
            else if (e.key === "ArrowRight" || e.key === "ArrowUp")
                n = w + step;
            else if (e.key === "Home")
                n = MinWidth;
            else if (e.key === "End")
                n = "full";
            else if (e.key === "Enter")
                n = 0;
            else
                return;
            e.preventDefault();
            apply(n, true);
        });
        window.addEventListener("resize", () => grip.setAttribute("aria-valuemax", String(max())));
    }

    const MarkdownView = {
        /**
         * Page main.
         * @returns {Promise<void>}
         */
        async Main() {
            await Sw.FileView.Start({
                Kind: "Markdown", Type: "read", Class: "md-main", HostClass: "md-card",
                Render: ctx => this.Render(ctx),
            });
        },

        /**
         * Show the text as the document.
         * @param {object} ctx - The Sw.FileView context.
         * @returns {Promise<void>}
         */
        async Render(ctx) {
            if (!ctx.DocE) {
                ctx.DocE = el("div.sw-md.sw-selectable.md-doc", { attrs: { role: "document", "aria-label": ctx.Name } });
                ctx.Host.append(ctx.DocE);
                if (window.PointerEvent)
                    WidthGrip(ctx.Page.E, ctx.Host);
            }
            const tpl = document.createElement("template");
            tpl.innerHTML = await Sw.Markdown.ToHtml(ctx.Text);
            Rebase(tpl.content, DocBase(ctx.Url));
            ctx.DocE.replaceChildren(tpl.content);
        },
    };

    Sw.MarkdownView = MarkdownView;
    Sw.Page.Main(() => MarkdownView.Main());
})();
