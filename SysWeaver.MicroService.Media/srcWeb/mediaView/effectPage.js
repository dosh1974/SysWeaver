/**
 * @file effectPage.js - shared page logic of the effect folder pages (backgroundEffects/, imageEffects/, collageEffects/,
 * textEffects/ index.html). Before the rewrite each page had its own inline copy of this code.
 *
 * The pages are lean full-window, transparent, non-interactive hosts (used as backgrounds in iframes, by the effect
 * galleries and by external embeds), so they do NOT load the SysWeaver core: only parseTools.js, common/anim.js and (for
 * the pages that render directly) the effect player scripts. The page kind comes from `<body data-effect-page="...">`:
 * - "background": renders `Effect` (default SmoothBands) with MediaPlayerEffect directly.
 * - "image": renders `Image` (default ../../logo.png) through `Effect` (default DistortedTv) with an image texture.
 * - "collage": `Image` (default DemoCollage.svg) through a collage `Effect` (default Stripes), rendered by an iframe of
 *   `../MediaPreview.html?transparent=true&link=<Image>&type=0&props=<json>`.
 * - "text": `Text` (single, image effect, default DistortedTv) or `Texts` (collage, default Stripes) rendered by an iframe of
 *   `../MediaPreview.html?transparent=true&link=<text>&type=5&props=<json>`.
 *
 * Url parameters are case sensitive (PascalCase member names, see parseTools.js). Precedence: the effect's side `.json`,
 * then the image's side `.json`, then the url (url wins); keys go to the media (image / text) params first, then the effect
 * params, then become shader variables (FxProps). `ShaderDebug=true` is forwarded to MediaPreview.
 *
 * Page protocol: when the media shows, `{Type:"PageLoaded"}` is posted to the parent and `{Type:"LoaderRemoved"}` to the
 * top window (the app shell reveals the page at once instead of after its 1 s fallback). Direct players are remote
 * controllable with the Media* window messages (MediaPlayerTools.SetupMediaMessages); the iframe pages relay messages
 * between their parent and the MediaPreview frame (ParseTools.MessageProxy).
 *
 * Globals defined: `EffectPage` (also `Sw.Media.EffectPage`). Runs `EffectPage.Main()` at DOMContentLoaded.
 */

/**
 * Static page logic of the effect folder pages. Never instantiated.
 */
class EffectPage {

    /**
     * Default effect params (the MediaPlayerParamsEffect members a url may set; other keys become shader variables).
     * @param {boolean} transparent Transparent default.
     * @returns {object} New defaults.
     */
    static EffectDefaults(transparent) {
        return {
            Width: 1920,
            Height: 1080,
            Speed: 1,
            LimitSize: false,
            DpiAdjust: false,
            DpiScale: 1,
            Static: false,
            Transparent: transparent,
            AdaptiveSize: true,
            WrapU: false,
            WrapY: false,
            WebGL2: false,
            MipMap: false,
            AntiAlias: false,
            ScrollId: null,
            MouseId: null,
            MouseEnable: false,
            ImageSize: false,
            ShowVars: false,
            ForceMode: null,
            DarkModeSwitch: 200,
            CustomX: 0,
            CustomY: 0,
            CustomZ: 0,
            CustomAnimX: null,
            CustomAnimY: null,
            CustomAnimZ: null,
        };
    }

    /**
     * Default text params of the text page (MediaPlayerParamsText members a url may set).
     * @param {boolean} collage True for the collage (Texts) mode.
     * @returns {object} New defaults.
     */
    static TextDefaults(collage) {
        const common = {
            Crop: null,
            Duration: 10,
            Transparent: true,
            FontUrl: null,
            FontUrlName: null,
            LetterSpacing: 0,
            Fill: "#15b",
            FillGradient: null,
            Stroke: "#fd4",
            StrokeGradient: null,
            StrokeCap: "round",
            ShadowFill: "#000c",
            ShadowStroke: null,
            StrokeFirst: true,
            AttachTo: null,
            DebugImage: false,
        };
        if (collage)
            return Object.assign(common, {
                EffectSizeFromImage: false,
                AdaptiveSize: true,
                Font: "bold 80px Tahoma",
                StrokeWidth: 3,
                ShadowX: 0,
                ShadowY: 0,
                ShadowBlur: 6,
                MarginLeft: 1,
                MarginTop: 1,
                MarginRight: 1,
                MarginBottom: 1,
                Texts: "Hello,World,SysWeaver",
                CollageSeparator: ",",
                CollageColumns: 2,
            });
        return Object.assign(common, {
            EffectSizeFromImage: true,
            AdaptiveSize: false,
            Font: "bold 300px Tahoma",
            StrokeWidth: 8,
            ShadowX: 3,
            ShadowY: 5,
            ShadowBlur: 12,
            MarginLeft: 16,
            MarginTop: 8,
            MarginRight: 16,
            MarginBottom: 8,
            Text: "SysWeaver",
        });
    }

    /**
     * The side json url of a media / effect url ("x/y.glsl" -> prefix + "x/y.json").
     * @param {string} url The url.
     * @param {string} prefix Prefix for relative urls ("" or "../").
     * @returns {string} The json url.
     */
    static SideJson(url, prefix) {
        const p = url.indexOf("://") < 0 ? prefix : "";
        const dot = url.lastIndexOf(".");
        return p + (dot > 0 ? url.substring(0, dot) : url) + ".json";
    }

    /**
     * The folder the MediaPreview frame of the collage / text pages resolves relative urls against (mediaView/).
     * @returns {string} The absolute url of the mediaView folder.
     */
    static MediaViewBase() {
        return new URL("../", window.location.href).href;
    }

    /**
     * Load a script (or a style sheet) of the site on demand (the refusal state needs the SysWeaver core and ui).
     * @param {string} url The url (relative to this page).
     * @param {boolean} [css] True for a style sheet.
     * @returns {Promise<void>} Resolves when loaded or failed.
     */
    static Include(url, css) {
        return new Promise(r => {
            const e = document.createElement(css ? "link" : "script");
            e.onload = e.onerror = () => r();
            if (css) {
                e.rel = "stylesheet";
                e.href = url;
            }
            else
                e.src = url;
            document.head.appendChild(e);
        });
    }

    /**
     * Show a refused media parameter (ParseTools.UrlProblem) instead of the effect: the reason and a button that opens
     * this page in a new tab (outside the application a file of another site is allowed). Loads the SysWeaver core / ui
     * on demand (the page is lean), is announced to screen readers and signals loaded (the shell reveals the page).
     * @param {{Kind: string, Url: (string|null), Host: string}} problem The problem.
     * @returns {Promise<void>}
     */
    static async Refuse(problem) {
        console.warn("Effect page: refused a media parameter (an effect, image or font of another site or not a web address)");
        const root = "../../";
        await Promise.all([EffectPage.Include(root + "common/theme.css", true), EffectPage.Include(root + "sw/ui.css", true)]);
        await EffectPage.Include(root + "sw/core.js");
        await EffectPage.Include(root + "sw/ui.js");
        const site = problem.Kind === "Site";
        const text = site
            ? _T("This effect uses a file from another site ({0}), so it is not shown inside the application. Open it in a new tab to see it.", problem.Host,
                "Message on an effect page inside the application when one of its files (effect, image or font) is on another web site. {0} is the name of that site (host name)")
            : _TF("This effect cannot be shown: a file address in its parameters is not a web address.",
                "Message on an effect page when one of its file parameters (effect, image or font) is not a http(s) address (not allowed)");
        const b = document.body;
        b.classList.add("sw-root");
        const box = document.createElement("div");
        box.className = "EffectRefused";
        box.setAttribute("role", "status");
        box.style.cssText = "pointer-events:auto;box-sizing:border-box;max-width:40em;margin:2em auto;padding:1em 1.25em;border-radius:var(--sw-radius-lg,8px);"
            + "background:var(--sw-surface,#fff);color:var(--sw-fg,#222);border:1px solid var(--sw-border,#8886);font:inherit;display:flex;flex-direction:column;gap:0.75em;align-items:flex-start";
        const p = document.createElement("p");
        p.style.margin = "0";
        p.textContent = text;
        box.appendChild(p);
        if (site) {
            const label = _TF("Open in a new tab", "Button text on an effect page, opens the effect with a file from another site in a new browser tab");
            const open = () => window.open(window.location.href, "_blank", "noopener");
            const Sw = window.Sw;
            if (Sw && typeof Sw.Button === "function")
                box.appendChild(new Sw.Button({ Text: label, Title: problem.Url, Icon: "IconMediaNewWindow", OnClick: open }).E);
            else {
                const btn = document.createElement("button");
                btn.type = "button";
                btn.textContent = label;
                btn.title = problem.Url;
                btn.addEventListener("click", open);
                box.appendChild(btn);
            }
        }
        b.appendChild(box);
        if (window.Sw && Sw.Page && Sw.Page.Announce)
            Sw.Page.Announce(text);
        EffectPage.SignalLoaded();
    }

    /**
     * Check url-like parameters with ParseTools.UrlProblem and show the refusal state for the first refused one.
     * @param {Array<Array<string>>} checks [url, base] pairs (base: the folder the url is resolved against).
     * @returns {Promise<boolean>} True when one was refused (the page shows the refusal, stop rendering).
     */
    static async Refused(checks) {
        for (const c of checks) {
            const problem = ParseTools.UrlProblem(c[0], c[1]);
            if (problem) {
                await EffectPage.Refuse(problem);
                return true;
            }
        }
        return false;
    }

    /**
     * Apply side json files and the url to the params: media params first, then effect params, then shader variables.
     * @param {URLSearchParams} p Url parameters.
     * @param {object} props Media params (or the effect params on the background page).
     * @param {object|null} ep Effect params (null on the background page).
     * @param {object} fxParams Receives the shader variables.
     * @param {string[]} jsonUrls Side json files, in order (missing files are ignored).
     * @returns {Promise<void>}
     */
    static async Apply(p, props, ep, fxParams, jsonUrls) {
        /** @param {string} k Key. @param {string} v Value. */
        const toFx = (k, v) => fxParams[k] = ParseTools.DecodeObject(v);
        /** @param {string} k Key. @param {string} v Value. */
        const notFound = ep ? (k, v) => ParseTools.SetToObject(ep, k, v, toFx) : toFx;
        for (const j of jsonUrls)
            if (j)
                await ParseTools.FileToObject(j, props, notFound);
        ParseTools.ParamsToObject(p, props, notFound);
    }

    /**
     * Finish the media params with the effect params (Effect, EffectParams, FxProps, Transparent, AdaptiveSize).
     * @param {object} props Media params.
     * @param {string} effect Effect url.
     * @param {object} ep Effect params.
     * @param {object} fxParams Shader variables.
     */
    static Link(props, effect, ep, fxParams) {
        props.Effect = effect;
        props.EffectParams = ep;
        ep.FxProps = fxParams;
        ep.Transparent = props.Transparent;
        ep.AdaptiveSize = props.AdaptiveSize;
        if (props.EffectSizeFromImage)
            ep.AdaptiveSize = false;
    }

    /**
     * Post the loaded protocol: PageLoaded to the parent (or opener) and LoaderRemoved to the top window.
     */
    static SignalLoaded() {
        if (typeof MediaPlayerTools === "function") {
            MediaPlayerTools.PostPageLoaded();
            return;
        }
        const framed = window.parent !== window;
        try {
            const w = framed ? window.parent : window.opener;
            //  Same-origin hosts only ("/"): a foreign window framing the page receives nothing
            if (w)
                w.postMessage({ Type: "PageLoaded" }, "/");
            if (framed)
                window.top.postMessage({ Type: "LoaderRemoved" }, "/");
        }
        catch {
        }
    }

    /**
     * Render with an effect player in this page (background / image pages), sized to the body, remote controllable.
     * @param {object} player A MediaPlayerEffect.
     * @returns {Promise<void>}
     */
    static async Play(player) {
        const b = document.body;
        b.appendChild(player.Element);
        await player.Cache(true);
        const updateSize = () => MediaPlayerTools.UpdateSize(b, player);
        new ResizeObserver(updateSize).observe(b);
        await player.Play();
        await player.Show();
        MediaPlayerTools.SetupMediaMessages(player);
        EffectPage.SignalLoaded();
    }

    /**
     * Render through an iframe of MediaPreview.html (collage / text pages): relays messages and signals loaded when the
     * preview signals it.
     * @param {URLSearchParams} p Url parameters.
     * @param {string} link The media link (image url or text).
     * @param {number} type The media type (0 image, 5 text).
     * @param {object} props The media params.
     * @param {boolean} [caption] - A text from the url inside the application: the preview shows it captioned "Text from
     * the link", directly with the text and in a limited area (MediaPreview `caption` parameter, media.js).
     */
    static Frame(p, link, type, props, caption) {
        let url = "../MediaPreview.html?transparent=true&link=" + encodeURIComponent(link) + "&type=" + type + "&props=" + encodeURIComponent(JSON.stringify(props));
        if (caption)
            url += "&caption=true";
        if (p.get("ShaderDebug") === "true")
            url += "&ShaderDebug=true";
        console.log(url);
        const c = document.createElement("iframe");
        c.setAttribute("allowtransparency", "true");
        ParseTools.MessageProxy(c);
        //  The shell only accepts LoaderRemoved from its own frame (this page), so re-signal the preview's PageLoaded
        const onLoaded = ev => {
            if (ev.source !== c.contentWindow || ev.origin !== window.location.origin || !ev.data || ev.data.Type !== "PageLoaded")
                return;
            window.removeEventListener("message", onLoaded);
            if (window.parent !== window)
                try {
                    window.top.postMessage({ Type: "LoaderRemoved" }, "/");
                }
                catch {
                }
        };
        window.addEventListener("message", onLoaded);
        c.src = url;
        document.body.appendChild(c);
    }

    /**
     * Run the page (kind from `<body data-effect-page>`).
     * Url-like parameters (Effect, Image, FontUrl) follow the media link rule (ParseTools.UrlProblem): they are checked
     * before anything (also their side json) is fetched; a refused one shows the refusal state (Refuse) instead.
     * A text from the url is length limited inside the application and, when the page is the application's content
     * (ParseTools.IsAppContent), captioned "Text from the link" by the MediaPreview frame (Frame with caption).
     * @returns {Promise<void>}
     */
    static async Main() {
        const p = new URL(window.location.href).searchParams;
        const kind = document.body.getAttribute("data-effect-page");
        const fxParams = {};
        const here = window.location.href;
        const mv = EffectPage.MediaViewBase();
        switch (kind) {
            case "background": {
                const props = EffectPage.EffectDefaults(false);
                let effect = p.get("Effect") ?? "SmoothBands";
                if (!effect.endsWith(".glsl"))
                    effect += ".glsl";
                if (await EffectPage.Refused([[effect, here]]))
                    return;
                //  The side json is next to the .glsl (was "../" + name: the mediaView folder, a bug)
                await EffectPage.Apply(p, props, null, fxParams, [EffectPage.SideJson(effect, "")]);
                props.FxProps = fxParams;
                await EffectPage.Play(new MediaPlayerEffect(effect, props));
                return;
            }
            case "image": {
                const props = { Crop: null, Duration: 10, EffectSizeFromImage: true, Transparent: true, AdaptiveSize: false };
                const ep = EffectPage.EffectDefaults(false);
                const customImage = p.get("Image");
                const image = customImage ?? "../../logo.png";
                let effect = p.get("Effect") ?? "DistortedTv";
                if (!effect.endsWith(".glsl"))
                    effect += ".glsl";
                if (await EffectPage.Refused([[effect, here], [image, here]]))
                    return;
                //  The default image (the site logo, generated: no side json), also when the link names it (the gallery passes it,
                //  R1-Media-1)
                let logo = !customImage;
                try {
                    logo = logo || new URL(image, here).href === new URL("../../logo.png", here).href;
                }
                catch {
                }
                await EffectPage.Apply(p, props, ep, fxParams, [EffectPage.SideJson(effect, ""), logo ? null : EffectPage.SideJson(image, "")]);
                EffectPage.Link(props, effect, ep, fxParams);
                await EffectPage.Play(new MediaPlayerEffect(effect, ep, gl => new MediaPlayerImageTexture(gl, props, image)));
                return;
            }
            case "collage": {
                const props = { Crop: null, Duration: 10, Transparent: true, AdaptiveSize: true };
                const ep = EffectPage.EffectDefaults(false);
                const image = p.get("Image") ?? "DemoCollage.svg";
                let effect = p.get("Effect") ?? "Stripes";
                if (!effect.endsWith(".glsl"))
                    effect = "collageEffects/" + effect + ".glsl";
                //  Both are resolved by the MediaPreview frame in mediaView/
                if (await EffectPage.Refused([[effect, mv], [image, mv]]))
                    return;
                await EffectPage.Apply(p, props, ep, fxParams, [EffectPage.SideJson(effect, "../"), EffectPage.SideJson(image, "../")]);
                EffectPage.Link(props, effect, ep, fxParams);
                EffectPage.Frame(p, image, 0, props);
                return;
            }
            case "text": {
                const isCollage = p.has("Texts");
                const props = EffectPage.TextDefaults(isCollage);
                const ep = EffectPage.EffectDefaults(false);
                let effect = p.get("Effect") ?? (isCollage ? "Stripes" : "DistortedTv");
                if (!effect.endsWith(".glsl"))
                    effect = (isCollage ? "collageEffects/" : "imageEffects/") + effect + ".glsl";
                if (await EffectPage.Refused([[effect, mv], [p.get("FontUrl"), mv]]))
                    return;
                await EffectPage.Apply(p, props, ep, fxParams, [EffectPage.SideJson(effect, "../")]);
                if (await EffectPage.Refused([[props.FontUrl, mv]]))
                    return;
                const fromUrl = p.has(isCollage ? "Texts" : "Text");
                //  A single text is drawn at 300px: 40 characters stay inside the WebGL texture size limits
                const text = ParseTools.TextFromUrl((isCollage ? props.Texts : props.Text) ?? "SysWeaver", isCollage ? 200 : 40);
                delete props.Text;
                delete props.Texts;
                EffectPage.Link(props, effect, ep, fxParams);
                EffectPage.Frame(p, text, 5, props, fromUrl && ParseTools.IsAppContent());
                return;
            }
        }
        console.warn("Unknown effect page kind \"" + kind + "\"");
    }
}

(function () {
    "use strict";
    const Sw = window.Sw = window.Sw || {};
    (Sw.Media = Sw.Media || {}).EffectPage = EffectPage;
    /** Start the page, errors are logged (the page stays transparent). */
    const start = () => EffectPage.Main().catch(e => console.error("Effect page failed: " + (e && e.stack ? e.stack : e)));
    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", start, { once: true });
    else
        start();
})();
