/**
 * @file effectsLib.js - the effect galleries BackgroundEffectsLib.html, ImageEffectsLib.html and CollageEffectsLib.html
 * (developer pages of the Debug/Media menu). Before the rewrite each page had its own inline copy of this code.
 *
 * The page kind comes from `<body data-effects="Background|Image|Collage">`:
 * - names from `Api/GetBackgroundEffects` | `Api/GetImageEffects` | `Api/GetCollageEffects` (cached 30 s),
 * - a card per effect with the name, the compilation and draw time (no draw time when unknown) and a thumbnail rendered off-screen at 640x360, t = 5 s
 *   (MediaPlayerEffect.GetImage, cached in localStorage `SysWeaver.EffectInfo.<kind>.<hash(url)>` and re-validated by ETag),
 * - image effects use `../logo.png` as texture; collage effects `DemoCollage.svg` with the atlas vars of DemoCollage.json.
 * A card opens `<folder>/?Effect=<name>` (+ `&Image=...`: relative to the effect page for image effects, `../../logo.png`; the
 * collage page renders through mediaView/MediaPreview.html, so its `DemoCollage.svg` is relative to mediaView/) in the same frame (a click on the
 * thumbnail, or its name link `a.fx-name`: Enter, middle click / copy link); a plain click elsewhere on the card copies that url (as the
 * original card), so does its copy button. Effects that fail to render get an error text on their card (they were silently skipped before).
 *
 * Page protocol: the page reports itself loaded (`Sw.Page.Loaded`) as soon as the cards exist; the thumbnails are rendered
 * afterwards, Parallel (2 on phones, 3 else) at a time, cards in (or near) the view first, at most CardTimeoutMs each, so the
 * shell does not wait. The page never looks empty meanwhile: a card shows at once its last rendered thumbnail (localStorage,
 * re-validated when its turn comes) or a static placeholder (the effect name over a soft pattern, the image / collage
 * pages also the source picture dimmed) with its state (Waiting n of m, Rendering..., or "Failed to render: reason").
 *
 * Requires sw/core.js, sw/ui.js and the effect player scripts (media_player_tools.js, media_effect_program.js,
 * media_player_effect.js; static tags). Globals defined: none (`Sw.Media.EffectsLib`).
 */
(function () {
    "use strict";

    const Sw = window.Sw;

    /** Gallery definitions by kind. */
    const Kinds = {
        Background: {
            Api: "GetBackgroundEffects", Folder: "backgroundEffects", Image: null, Texture: null,
            Title: () => _TF("Background Effects Demo", "Title of the gallery page of the background effects (the original page title)"),
        },
        Image: {
            //  (`Image` is the url parameter of the effect page: relative to that page, imageEffects/, which resolves it itself (R1-Media-1:
            //  "../logo.png" was /mediaView/logo.png there, a 404 and a black effect); the texture of the thumbnails is relative to this page)
            Api: "GetImageEffects", Folder: "imageEffects", Image: "../../logo.png", Texture: { EffectSizeFromImage: true, Url: "../logo.png" },
            Title: () => _TF("Image Effects Demo", "Title of the gallery page of the image effects (the original page title)"),
        },
        Collage: {
            Api: "GetCollageEffects", Folder: "collageEffects", Image: "DemoCollage.svg", Texture: { EffectSizeFromImage: false, Url: "DemoCollage.svg" }, FxProps: "DemoCollage.json",
            Title: () => _TF("Image Effects Demo", "Title of the gallery page of the collage effects (the original page title, the same as the image effects gallery)"),
        },
    };

    /** @type {number} Longest time (ms) one thumbnail may take; then the card fails ("took too long") and the next one starts. */
    const CardTimeoutMs = 20000;

    /**
     * Number of thumbnails rendered at the same time (each is a WebGL context of its own; more would only share the same
     * GPU): 2 on phones / touch devices, 3 else.
     * @returns {number}
     */
    function Parallel() {
        const p = Sw.Platform;
        return p && (p.IsMobileDevice || p.IsTouch) ? 2 : 3;
    }

    /**
     * The localStorage key of a cached thumbnail (the key MediaPlayerEffect.GetImage uses).
     * @param {string} kindKey The gallery kind ("Background", "Image", "Collage").
     * @param {string} url The effect url (folder/name.glsl).
     * @returns {Promise<string>} The key.
     */
    async function CacheKey(kindKey, url) {
        return "SysWeaver.EffectInfo." + kindKey + "." + await MediaPlayerTools.HashString(url);
    }

    /**
     * Show the last rendered thumbnail of a card at once (from localStorage, without a request); GetImage re-validates it
     * when the card's turn comes.
     * @param {object} c The card (CreateCard).
     * @param {string} key The cache key (CacheKey).
     * @returns {boolean} True when a cached thumbnail is shown.
     */
    function ShowCached(c, key) {
        try {
            const s = localStorage.getItem(key);
            const d = s ? JSON.parse(s) : null;
            const r = d && d.R;
            if (!r || typeof r[0] !== "string" || !r[0].startsWith("data:image/"))
                return false;
            c.Img.src = r[0];
            c.E.classList.add("fx-has-thumb");
            return true;
        }
        catch {
            return false;
        }
    }

    /**
     * Create the card of one effect.
     * The card shows its render state (attribute data-state and the info text): "queued" (Waiting, n of m), "rendering",
     * "done" (the compilation and draw times; only the compilation time when the GPU did not report a draw time) or "failed" (with the reason).
     * Until its thumbnail exists the card shows a static placeholder (`div.fx-ph`: the effect name over a soft pattern, with
     * the gallery's source picture dimmed when it has one), so the gallery never looks empty.
     * @param {string} name Effect name.
     * @param {string} url The effect page url.
     * @param {string|null} [source] - The source picture of the gallery (image / collage effects), shown dimmed in the placeholder.
     * @returns {{E: HTMLElement, Info: HTMLElement, Img: HTMLImageElement, State: HTMLElement}} The card parts.
     */
    function CreateCard(name, url, source) {
        const img = Sw.el("img.fx-thumb", { attrs: { alt: "", draggable: "false" } });
        const ph = Sw.el("div.fx-ph", { attrs: { "aria-hidden": "true" } },
            source ? Sw.el("img.fx-ph-src", { attrs: { alt: "", draggable: "false", src: source, loading: "lazy" } }) : null,
            Sw.el("span.fx-ph-name", { text: name }));
        const info = Sw.el("div.fx-info.sw-selectable", { attrs: { "aria-live": "off" } });
        const state = Sw.el("div.fx-state", { attrs: { "aria-hidden": "true" } });
        const copy = new Sw.Button({
            Icon: "copy", Kind: "ghost", Small: true,
            Title: _TF("Copy the url of this effect page", "Tool tip of a button that copies the url of an effect page"),
            Label: _T("Copy the url of the {0} effect page", name, "Accessible name of the copy button on an effect gallery card, {0} is the effect name"),
            OnClick: (b, ev) => {
                if (ev)
                    ev.stopPropagation();
                return Sw.Clipboard.Copy(url);
            },
        });
        //  The name is a real link (keyboard focus, Enter, middle click / copy link work)
        const link = Sw.el("a.fx-name", { href: url, text: name, attrs: { "aria-label": _T("Open the {0} effect", name, "Accessible name of the link on an effect card, {0} is the effect name") } });
        link.addEventListener("click", ev => {
            //  (the card's copy action leaves a click on an inner link alone: a modifier / middle click keeps the browser's new tab)
            if (!Sw.IsPureEvent(ev))
                return;
            ev.preventDefault();
            Sw.Page.Navigate(url, "_self");
        });
        //  The picture opens the effect page, as the original card image (its img.onclick)
        const frame = Sw.el("div.fx-frame", { title: _TF("Open the effect page", "Tool tip of an effect card in the effect galleries") }, ph, img, state);
        frame.addEventListener("click", ev => {
            if (Sw.BadClick(ev))
                return;
            //  The page is left at once: the card's copy action does not run as well
            ev.stopPropagation();
            Sw.Page.Navigate(url, "_self");
        });
        const card = Sw.el("div.fx-card", { attrs: { "data-state": "queued" } },
            Sw.el("div.fx-title", null, link, copy.E),
            info,
            frame);
        //  A plain click elsewhere on the card copies the url of its effect page, as the original card (ValueFormat.copyOnClick,
        //  keyboard reachable); also Ctrl+C on the focused card and its menu (right click / long press). The name link and the copy
        //  button keep their own action (the copy action leaves inner controls alone), the picture stops its click.
        Sw.Clipboard.CopyOnClick(card, url, { Keyboard: true, AddTitle: true });
        return { E: card, Info: info, Img: img, State: state };
    }

    /**
     * Set the render state of a card.
     * @param {object} c The card (CreateCard).
     * @param {string} state "queued", "rendering", "done" or "failed".
     * @param {string} text The info text.
     */
    function SetState(c, state, text) {
        c.E.setAttribute("data-state", state);
        c.E.classList.toggle("fx-failed", state === "failed");
        if (state === "done")
            c.E.classList.add("fx-has-thumb");
        else if (state === "failed") {
            //  No stale thumbnail over a failure
            c.E.classList.remove("fx-has-thumb");
            c.Img.removeAttribute("src");
        }
        c.Info.textContent = text;
        //  The info line is two lines high (effectsLib.css): the whole text as its tool tip
        c.Info.title = text;
        c.State.textContent = state === "done" ? "" : text;
    }

    /**
     * Format a duration in ms for a card ("-" when unknown or too small to show).
     * @param {number} ms The duration.
     * @param {number} digits Fraction digits.
     * @returns {string} The text.
     */
    function Ms(ms, digits) {
        if (!(ms > 0))
            return "-";
        const t = ms.toFixed(digits);
        return parseFloat(t) > 0 ? t : "-";
    }

    /**
     * Render the thumbnails, Parallel() at a time, cards in (or near) the view first; every card shows its last thumbnail
     * (ShowCached) or its placeholder and its state while it waits. A card that takes longer than CardTimeoutMs fails with
     * "took too long" and the next one starts.
     * @param {object} kind The gallery definition.
     * @param {object} mp The effect params.
     * @param {Array<{Name: string, Card: object}>} items The cards.
     * @returns {Promise<void>}
     */
    async function RenderAll(kind, mp, items) {
        const tex = kind.Texture;
        const createTexture = tex ? gl => new MediaPlayerImageTexture(gl, { EffectSizeFromImage: tex.EffectSizeFromImage }, tex.Url) : undefined;
        //  The last thumbnails at once (no request), re-validated below
        await Promise.all(items.map(async it => ShowCached(it.Card, await CacheKey(kind.Key, kind.Folder + "/" + it.Name + ".glsl"))));
        const visible = new Set();
        const io = new IntersectionObserver(entries => {
            for (const e of entries) {
                if (e.isIntersecting)
                    visible.add(e.target);
                else
                    visible.delete(e.target);
            }
        }, { rootMargin: "200px" });
        items.forEach(it => io.observe(it.Card.E));
        //  Let the observer report the first layout
        await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
        const pending = items.slice();
        /** Show the queue position on the waiting cards. */
        const showQueue = () => pending.forEach((it, i) => SetState(it.Card, "queued",
            _T("Waiting ({0} of {1})", i + 1, pending.length, "Shown on an effect card while its preview waits for the cards before it. {0} is its place in the queue, {1} the number of waiting cards")));
        /**
         * One render lane: takes the next card (in view first) until none is left.
         * @returns {Promise<void>}
         */
        const lane = async () => {
            while (pending.length) {
                let i = pending.findIndex(it => visible.has(it.Card.E));
                if (i < 0)
                    i = 0;
                const it = pending.splice(i, 1)[0];
                const c = it.Card;
                if (!c.E.isConnected)
                    return;
                showQueue();
                SetState(c, "rendering", _TF("Rendering...", "Shown on an effect card while its preview image is rendered"));
                let timer = 0;
                const timeout = new Promise(r => timer = setTimeout(() => r([null, _TF("it took too long", "Reason on an effect card when its preview did not render within some seconds")]), CardTimeoutMs));
                let r;
                try {
                    r = await Promise.race([MediaPlayerEffect.GetImage(kind.Key, kind.Folder + "/" + it.Name + ".glsl", mp, createTexture), timeout]);
                }
                finally {
                    clearTimeout(timer);
                }
                if (!r || !r[0]) {
                    SetState(c, "failed", _T("Failed to render: {0}", (r && r[1]) || "?", "Shown on an effect card when the effect failed to render. {0} is the error"));
                    continue;
                }
                c.Img.src = r[0];
                //  The draw time only when the GPU reported it (no "Draw time -" line)
                const comp = _T("Compilation duration {0} ms", Ms(r[1], 1), "Text that displays the compilation duration in milli seconds. {0} is replaced with the duration in milli seconds");
                SetState(c, "done", Ms(r[2], 3) !== "-"
                    ? comp + " - " + _T("Draw time {0} ms", Ms(r[2], 3), "Text that displays the number of milli seconds it takes on average to render a graphics frame on the GPU. {0} is replaced with the duration in milli seconds")
                    : comp);
            }
        };
        try {
            showQueue();
            const lanes = [];
            for (let n = Math.min(Parallel(), pending.length); n > 0; --n)
                lanes.push(lane());
            await Promise.all(lanes);
        }
        finally {
            io.disconnect();
        }
    }

    /**
     * Page main: list the effects, create the cards, signal loaded, render the thumbnails.
     * @returns {Promise<void>}
     */
    async function Main() {
        const key = document.body.getAttribute("data-effects");
        const kind = Kinds[key];
        kind.Key = key;
        const list = Sw.el("div.fx-list");
        //  Design v2 dashboard page: a left aligned page header (title, what the gallery shows) above the card grid
        const head = Sw.PageHeader({
            Title: kind.Title(),
            Description: _TF("Open an effect to see it full size. The previews are rendered by your browser and kept for the next visit.", "One line description below the title of an effect gallery page"),
        });
        document.body.appendChild(Sw.el("main.fx-page.sw-page-dash", null, head.E, list));
        let items = [];
        const remove = Sw.Page.Loading();
        let mp;
        try {
            mp = new MediaPlayerParamsEffect();
            //  640x360 is sharp enough for a card (about 24em wide) and renders 2.25x faster than 960x540 on a slow GPU
            mp.Width = 640;
            mp.Height = 360;
            mp.AdaptiveSize = false;
            mp.Static = false;
            if (kind.FxProps)
                mp.FxProps = await Sw.Net.CachedGet(kind.FxProps, 30000);
            const effects = await Sw.Net.CachedGet("Api/" + kind.Api, 30000) || [];
            items = effects.map(name => {
                let url = kind.Folder + "/?Effect=" + encodeURIComponent(name);
                if (kind.Image)
                    url += "&Image=" + kind.Image;
                const card = CreateCard(name, url, kind.Texture ? kind.Texture.Url : null);
                list.appendChild(card.E);
                return { Name: name, Card: card };
            });
            if (!items.length)
                list.appendChild(Sw.StateView({ Title: _TF("No effects found.", "Shown in an effect gallery without effects"), Icon: "image" }).E);
        }
        catch (e) {
            const msg = e && e.message ? e.message : ("" + e);
            Sw.Fail(msg);
            list.replaceChildren(Sw.StateView({
                Kind: "error",
                Title: _TF("The effects could not be listed", "Title of the error state of an effect gallery page when the list of effects failed to load"),
                Text: _TF("Reload the page to try again.", "Text of the error state of an effect gallery page"),
                Details: msg,
            }).E);
        }
        finally {
            remove();
        }
        if (items.length)
            await RenderAll(kind, mp, items);
    }

    Sw.Media = Sw.Media || {};
    Sw.Media.EffectsLib = { Kinds, Main };
    Sw.Page.Main(Main);
})();
