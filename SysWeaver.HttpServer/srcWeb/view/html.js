/**
 * @file The html viewer view/html.html (`Sw.HtmlView`): an html file shown as a page in a frame below the page header, like the other file
 * viewers (Sw.FileView, view.js: url parameters r / n / download, actions Text, Reload, Download, states).
 * Registered as the extension viewer of .html / .htm / .xhtml files by the http server service (C# HttpServerServiceBase,
 * `view/html.html?r={0}`). Without it the server's fallback viewer of html is the file itself, which Sw.Page.View opens in a new tab.
 *
 * The frame is sandboxed without allow-same-origin: the page's scripts, forms and pop-ups work, but in an origin of their own, so a file
 * can't reach this site's session, cookies or pages (the file may come from a user). Its own requests to this site go without the session,
 * so images or apis that need a sign in don't load: Open shows the page itself in a new tab, with the site's own rules (ex: user storage
 * serves active content with a CSP sandbox).
 * A plain file url (no query) is loaded by the frame itself (relative links, images and styles resolve as on the page); an api url (with a
 * query, ex: ReadTextFile?"x.html") serves the html as text, so it is fetched and shown through srcdoc.
 */
(function () {
    "use strict";

    const el = Sw.el;

    /** The sandbox of the frame: everything except the same origin (and top navigation). */
    const Sandbox = "allow-scripts allow-forms allow-popups allow-popups-to-escape-sandbox allow-modals allow-downloads";

    const HtmlView = {
        /**
         * Page main.
         * @returns {Promise<void>}
         */
        async Main() {
            const plain = !(Sw.Url.Get("r") || "").includes("?");
            await Sw.FileView.Start({
                Kind: "HTML", Type: "fill", Class: "html-main", HostClass: "html-card", Fetch: !plain, Empty: !plain,
                Actions: ctx => [new Sw.Button({
                    Icon: "external", Text: _TF("Open", "Text on a button of the html viewer that opens the html page itself in a new tab"),
                    Title: _TF("Open the page itself in a new tab (its scripts run with this site's session)", "Tool tip of the Open button of the html viewer"),
                    OnClick: () => Sw.Page.Navigate(Sw.Url.Absolute(ctx.Url), "_blank"),
                })],
                Render: ctx => this.Render(ctx, plain),
                Reload: ctx => this.Render(ctx, plain, true),
            });
        },

        /**
         * Show the page in the frame (again).
         * @param {object} ctx - The Sw.FileView context.
         * @param {boolean} plain - The read url is a plain file url (the frame loads it).
         * @param {boolean} [reload] - Load it again.
         * @returns {Promise<void>}
         */
        async Render(ctx, plain, reload) {
            let f = ctx.Frame;
            if (!f) {
                f = ctx.Frame = document.createElement("iframe");
                f.className = "html-frame";
                f.setAttribute("sandbox", Sandbox);
                f.setAttribute("referrerpolicy", "no-referrer");
                f.title = ctx.Name;
                ctx.Host.append(f);
            }
            if (plain) {
                //  (setting src navigates the frame, also to the same url: a reload)
                f.src = ctx.Url;
                return;
            }
            if (reload)
                ctx.Text = null;
            f.srcdoc = await ctx.Get();
        },
    };

    Sw.HtmlView = HtmlView;
    Sw.Page.Main(() => HtmlView.Main());
})();
