/**
 * @file The shared frame of the file viewer pages (`Sw.FileView`): view/markdown.html, view/html.html, view/tree.html (SysWeaver.HttpServer)
 * and explore/csv.html (Explore module). These pages are registered as extension viewers (C# ServiceManager.TryAddExtensionViewer, format
 * "<page>?r={0}"), so Sw.Page.View, the explore listing and the View action of the text editor (edit/text.html) open them.
 *
 * `Sw.FileView.Start(o)` reads the url parameters, checks them, loads the file and builds the page:
 * - `r` read url (required, this site only: relative or the same origin), fetched with GET (unless `Fetch: false`).
 * - `n` display / download name (default: the last path segment of r, uri decoded, quotes dropped); sets the document title.
 * - `download=false` hides Download.
 * The page (Design v2): Sw.PageLayout (o.Type: "read" or "fill") = the page header (the file name, o.Kind as description; actions: the
 * viewer's own (o.Actions), Text (the file in its registered editor, Sw.Page.Edit), Reload, Download) and a card (ctx.Host, unless
 * `Host: false`) that o.Render fills. States: not allowed (403, Sw.Page.ShowNotAllowed), a failed load (Try again, the technical text under
 * Details), an empty file (unless `Empty: false`), a link without r or to another site.
 *
 * Options: Kind (description, ex: "Markdown"), Type ("read" default, "fill"), Class (on main), HostClass, Host (false: no card, Render adds
 * its own content to ctx.Page), Fetch (default true), Empty (default true: an empty text shows the empty state instead of Render),
 * Actions(ctx) -> Sw.Button[] (first in the header), Render(ctx) -> Promise (show ctx.Text), Reload(ctx) -> Promise (default: load the text
 * again and Render, keeping the scroll position).
 * Context: { Url, Name, Text, Page, Header, Host, Reload(), Get() (the text, loaded when not yet), State(o) }.
 */
(function () {
    "use strict";

    const el = Sw.el;

    /**
     * The display name of a read url: its last path segment, uri decoded, quotes dropped (ex: ReadTextFile?"x.md" has none).
     * @param {string} url - The read url.
     * @returns {string} The name.
     */
    function NameOf(url) {
        const path = url.split("?")[0];
        const seg = path.substring(path.lastIndexOf("/") + 1);
        try {
            return decodeURIComponent(seg).replace(/["']/g, "");
        }
        catch {
            return seg.replace(/["']/g, "");
        }
    }

    /**
     * Load the text of the file.
     * @param {string} url - The read url.
     * @returns {Promise<string>} The text ("" for none).
     */
    async function Load(url) {
        const t = await Sw.Net.Get(url, { Text: true, Reload: true });
        return t === null || t === undefined ? "" : String(t);
    }

    /**
     * A state view in a card that takes the place of the content.
     * @param {object} o - Sw.StateView options.
     * @param {boolean} fill - A fill page (the card takes the height).
     * @returns {HTMLElement} The card.
     */
    function StateCard(o, fill) {
        return el("section.sw-card.sw-fileview-state" + (fill ? ".sw-fill" : ""), null, Sw.StateView(o).E);
    }

    const FileView = {
        NameOf,

        /**
         * Build a viewer page (see the file description).
         * @param {object} o - Options.
         * @returns {Promise<object>} The context.
         */
        async Start(o) {
            const ctx = { O: o };
            const done = Sw.Page.Loading();
            try {
                await this.Init(ctx, o);
            }
            catch (e) {
                if (Sw.Access && Sw.Access.IsDenied(e)) {
                    document.body.replaceChildren();
                    Sw.Page.ShowNotAllowed({ Error: e, Url: e.Url || Sw.Url.Get("r") });
                }
                else
                    ctx.State({
                        Kind: "error",
                        Title: _TF("The file could not be loaded", "Title of the error state of a file viewer (markdown, html, json, xml, csv) when the file failed to load"),
                        Text: _T("Loading \"{0}\" failed. The server may be busy or restarting; try again in a moment.", ctx.Name || Sw.Url.Get("r") || "", "Text of the error state of a file viewer when the file failed to load. {0} is replaced by the file name"),
                        Details: e && e.message ? e.message : String(e),
                        Actions: [new Sw.Button({ Icon: "refresh", Text: _TF("Try again", "Text of a button that loads a page again after an error"), OnClick: () => location.reload() })],
                    });
            }
            finally {
                done();
                Sw.Page.Loaded();
            }
            return ctx;
        },

        /**
         * Read the parameters, load the text and build the page.
         * @param {object} ctx - The context.
         * @param {object} o - Options.
         * @returns {Promise<void>}
         */
        async Init(ctx, o) {
            const U = Sw.Url;
            const fill = o.Type === "fill";
            /** Show a state in place of the content (in the page when it is built, else in a new page). */
            ctx.State = s => {
                const card = StateCard(s, fill);
                if (ctx.Host) {
                    ctx.Host.replaceWith(card);
                    ctx.Host = card;
                    return;
                }
                if (!ctx.Page) {
                    document.body.replaceChildren();
                    ctx.Page = Sw.PageLayout({ Type: fill ? "fill" : "read", Title: ctx.Name || document.title, Compact: fill, Class: o.Class || null });
                }
                ctx.Page.Add(card);
            };
            const url = U.Get("r");
            if (!url) {
                ctx.State({
                    Kind: "error", Title: _TF("No file to show", "Title of the error state of a file viewer when the link names no file (no r url parameter)"),
                    Text: _TF("No read url supplied!", "Error message displayed when a required url parameter wasn't supplied") + " (r=..)",
                });
                return;
            }
            //  Security: the read url must be on this site (relative or the same origin, http(s)); a crafted link must not make the page show
            //  another site's content as ours. Nothing is requested.
            if (!U.SafeSameOrigin(url)) {
                ctx.State({
                    Kind: "error", Title: _TF("This file cannot be shown", "Title of the error state of a file viewer when the r url parameter points to another web site"),
                    Text: _TF("This file cannot be shown: its address (the r parameter) must be on this site.", "Error message of a file viewer when the r url parameter points to another web site (not allowed)"),
                });
                return;
            }
            const name = U.Get("n") || NameOf(url);
            ctx.Url = url;
            ctx.Name = name;
            document.title = name;
            ctx.Text = null;
            ctx.Get = async () => {
                if (ctx.Text === null)
                    ctx.Text = await Load(url);
                return ctx.Text;
            };
            //  The text first (one request; a failure / 403 is shown by Start), so the page is built once
            if (o.Fetch !== false)
                await ctx.Get();
            const show = async () => {
                if (o.Fetch !== false && o.Empty !== false && !ctx.Text.trim()) {
                    ctx.State({
                        Kind: "empty", Icon: "file",
                        Title: _TF("The file is empty", "Title of the empty state of a file viewer when the file has no text"),
                        Text: _TF("There is no text in this file.", "Text of the empty state of a file viewer when the file has no text"),
                    });
                    return;
                }
                await o.Render(ctx);
            };
            ctx.Reload = async () => {
                try {
                    if (o.Reload) {
                        await o.Reload(ctx);
                        return;
                    }
                    const x = window.scrollX, y = window.scrollY;
                    ctx.Text = null;
                    await ctx.Get();
                    if (ctx.Host && !ctx.Host.classList.contains("sw-fileview-state"))
                        await show();
                    else
                        location.reload();
                    window.scrollTo(x, y);
                }
                catch (e) {
                    Sw.Fail(_TF("Failed to reload the file", "Error message displayed when a file reload failed") + ":\n" + (e && e.message ? e.message : e));
                }
            };
            const actions = (o.Actions ? o.Actions(ctx) || [] : []).concat([
                new Sw.Button({
                    Icon: "code", Text: _TF("Text", "Text on a button of a file viewer (markdown, html, json, xml, csv) that opens the file's text in the text editor"),
                    Title: _TF("Show the text of the file in the text editor", "Tool tip of the Text button of a file viewer"),
                    OnClick: () => Sw.Page.Edit(url, "_blank"),
                }),
                new Sw.Button({
                    Icon: "IconReload", Text: _TF("Reload", "Text on a button that when clicked will reload a file"),
                    Title: _TF("Reload the file", "Tool tip description on a button that when clicked will reload the file"),
                    OnClick: () => ctx.Reload(),
                }),
            ]);
            if ((U.Get("download") || "").toLowerCase() !== "false")
                actions.push(new Sw.Button({
                    Icon: "download", Text: _TF("Download", "Text on a button that when clicked will download the file"),
                    Title: _TF("Download the file", "Tool tip description on a button that when clicked will download the file"),
                    OnClick: async () => Sw.Net.DownloadText(name, await ctx.Get()),
                }));
            const page = Sw.PageLayout({ Type: fill ? "fill" : "read", Title: name, Description: o.Kind || "", Compact: fill, Actions: actions, Class: o.Class || null });
            const header = page.Header;
            if (header.FromMenu)
                header.SetDescription(name);
            const nameE = header.FromMenu ? header.DescE : header.TitleE;
            if (nameE) {
                nameE.title = url;
                nameE.classList.add("sw-fileview-name");
            }
            ctx.Page = page;
            ctx.Header = header;
            if (o.Host !== false) {
                ctx.Host = el("section.sw-card" + (fill ? ".sw-fill" : ""), { class: o.HostClass || null });
                page.Add(ctx.Host);
            }
            await show();
        },
    };

    Sw.FileView = FileView;
})();
