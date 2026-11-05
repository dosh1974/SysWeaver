/**
 * @file textDemo.js - page logic of TextDemo.html (developer page of the Debug/Media menu): text style presets rendered
 * with CanvasTools, each with links to the text effect page (textEffects/).
 *
 * Url parameters (case sensitive): `Text` (sample text, default "Abc123"), `Texts` (comma separated collage texts,
 * default greetings).
 * Each preset card shows the rendered text (activating it opens `textEffects/?<style>&Text=<text>&Effect=None`), a button
 * that copies the style as JSON, 5 image effect buttons (single text) and 4 collage effect buttons (the style at half
 * scale, `Texts`), all opening `textEffects/` in the same frame. The effect buttons show their effect name next to the
 * icon, in two labelled groups ("Effect:", "Collage:"), with a tool tip and an accessible name that names the card.
 *
 * Requires sw/core.js, sw/ui.js, ../mediaPlayer/canvasTools.js (TextStyle, TextLinearGradient, CanvasTools) and
 * parseTools.js. Globals defined: none (`Sw.Media.TextDemo`).
 */
(function () {
    "use strict";

    const Sw = window.Sw;

    /** Image effects (single text) with their button icons. Each entry has its own icon (the old parallel arrays had a length bug). */
    const ImageEffects = [
        { Effect: "BadVHS", Icon: "../icons/preview_play.svg" },
        { Effect: "Rain", Icon: "../icons/download.svg" },
        { Effect: "SliceGlitch", Icon: "../icons/gradient.svg" },
        { Effect: "OldMovie", Icon: "../icons/movie.svg" },
        { Effect: "3D-flag", Icon: "../icons/os_windows.svg" },
    ];

    /** Collage effects (Texts) with their button icons. */
    const CollageEffects = [
        { Effect: "3d-tiles", Icon: "../icons/world.svg" },
        { Effect: "Stripes", Icon: "../icons/brick.svg" },
        { Effect: "Scroll", Icon: "../icons/credit_card.svg" },
        { Effect: "Wheel", Icon: "../icons/settings.svg" },
    ];

    /** Margin (px) around the demo renders. */
    const Margin = 32;

    /** Scale of the collage style compared to the demo style. */
    const CollageScale = 0.5;

    /**
     * The style at collage scale (font size, stroke, shadow offsets and letter spacing scaled, small margins).
     * @param {TextStyle} style The style.
     * @returns {object} The scaled copy.
     */
    function CollageStyle(style) {
        const c = Object.assign({}, style);
        const fontP = style.Font.split(" ");
        for (let i = 0; i < fontP.length; ++i) {
            const t = fontP[i];
            if (t.length > 2 && t.endsWith("px")) {
                fontP[i] = (parseFloat(t) * CollageScale) + "px";
                break;
            }
        }
        c.Font = fontP.join(" ");
        c.MarginTop = c.MarginRight = c.MarginBottom = c.MarginLeft = 2;
        c.StrokeWidth *= CollageScale;
        c.ShadowX *= CollageScale;
        c.ShadowY *= CollageScale;   // was ShadowX scaled twice
        c.LetterSpacing *= CollageScale;
        return c;
    }

    /**
     * Add a preset card.
     * @param {HTMLElement} list The card container.
     * @param {string} title Card title.
     * @param {TextStyle} style The style (margins are set to Margin).
     * @param {string} text Sample text.
     * @param {string} texts Collage texts.
     * @returns {Promise<void>} Resolves when the text is rendered.
     */
    async function Add(list, title, style, text, texts) {
        style.MarginTop = style.MarginRight = style.MarginBottom = style.MarginLeft = Margin;
        const url = "textEffects/?" + ParseTools.ObjectToParams(style) + "&Text=" + encodeURIComponent(text) + "&Effect=";
        const curl = "textEffects/?" + ParseTools.ObjectToParams(CollageStyle(style)) + "&Texts=" + encodeURIComponent(texts) + "&Effect=";
        /**
         * An effect link button.
         * @param {{Effect: string, Icon: string}} e The effect.
         * @param {string} base The url without the effect name.
         * @param {string} cls Extra class (color).
         * @returns {HTMLElement} The button.
         */
        const fxButton = (e, base, cls) => new Sw.Button({
            //  A short visible label (the effect name) next to the icon: the icons alone did not say what they do
            Icon: e.Icon, Text: e.Effect, Kind: "ghost", Small: true, Class: cls,
            Title: _T("Show full screen with the {0} effect", e.Effect, "Tool tip text on an icon that when clicked will show the content in full screen using a specific effect. {0} is replaced with the name of the effect"),
            //  The name says which card it is (the same effect buttons repeat on every card)
            Label: _T("Show {0} in full screen with the {1} effect", title, e.Effect, "Accessible name of an effect button on a text demo card, {0} is the name of the text style, {1} the name of the effect"),
            OnClick: () => Sw.Page.Navigate(base + encodeURIComponent(e.Effect), "_self"),
        }).E;
        const copy = new Sw.Button({
            Icon: "copy", Kind: "ghost", Small: true, Class: "td-copy",
            Title: _TF("Copy the text style as json to the clipboard", "Tool tip text on an icon that when clicked will copy the styling for some text to the clipboard in json format"),
            Label: _T("Copy the {0} text style as json", title, "Accessible name of the copy button on a text demo card, {0} is the name of the text style"),
            OnClick: () => Sw.Clipboard.Copy(JSON.stringify(style, null, "\t"), { Generic: _TF("Copied json to the clipboard", "Notification text shown when a user copied some json text to the clipboard") }),
        });
        const img = Sw.el("img.td-img", { attrs: { alt: "", draggable: "false" } });
        //  A real link to the plain text effect page (middle click / copy link work); a plain click stays in the frame
        const imgBox = Sw.el("a.td-imgbox", {
            href: url + "None",
            title: _TF("Show the text full screen", "Tool tip of a rendered text in the text demo"),
            attrs: { "aria-label": _T("Show {0} in full screen", title, "Accessible name of the rendered text on a text demo card (a link to the full screen page), {0} is the name of the text style") },
        }, img);
        imgBox.addEventListener("click", ev => {
            //  (the card's copy action leaves a click on an inner link alone: a modifier / middle click keeps the browser's new tab)
            if (!Sw.IsPureEvent(ev))
                return;
            ev.preventDefault();
            Sw.Page.Navigate(url + "None", "_self");
        });
        const card = Sw.el("section.td-card", null,
            Sw.el("div.td-title", null,
                Sw.el("h2.td-name", { text: title }),
                copy.E),
            Sw.el("div.td-effects", null,
                Sw.el("div.td-fxgroup", { attrs: { role: "group", "aria-label": _T("Full screen effects for {0}", title, "Accessible name of the group of effect buttons on a text demo card, {0} is the name of the text style") } },
                    Sw.el("span.td-fxlabel", { text: _TF("Effect:", "Label before the effect buttons on a text demo card (each shows the text full screen with that effect)") }),
                    ...ImageEffects.map(e => fxButton(e, url, "td-image-fx"))),
                Sw.el("div.td-fxgroup", { attrs: { role: "group", "aria-label": _T("Collage effects for {0}", title, "Accessible name of the group of collage effect buttons on a text demo card, {0} is the name of the text style") } },
                    Sw.el("span.td-fxlabel", { text: _TF("Collage:", "Label before the collage effect buttons on a text demo card (each shows several texts full screen with that effect)") }),
                    ...CollageEffects.map(e => fxButton(e, curl, "td-collage-fx")))),
            imgBox);
        //  The card copies the url of the plain text effect page like the original card (ValueFormat.copyOnClick, keyboard
        //  reachable), with the shared copy model: Ctrl+C on the focused card, its menu on a right click / long press
        Sw.Clipboard.CopyOnClick(card, url + "None", { Keyboard: true, AddTitle: true });
        list.appendChild(card);
        img.src = await CanvasTools.CreateTextImageUrl(text, style);
    }

    /**
     * The presets: [title, style values].
     * @returns {Array<Array>} The presets.
     */
    function Presets() {
        const lg = o => Object.assign(new TextLinearGradient(), o);
        return [
            [_TF("Fill and stroke color", "Name of a text style preset in the text demo"), { Font: "bold 256px serif", Fill: "#687", Stroke: "#020", StrokeWidth: 4, ShadowFill: null, StrokeFirst: false }],
            [_TF("Drop shadow", "Name of a text style preset in the text demo"), { Font: "bold 256px verdana", Fill: "#687", Stroke: "#020", StrokeWidth: 4, ShadowX: 4, ShadowY: 8, StrokeFirst: false }],
            [_TF("Letter spacing", "Name of a text style preset in the text demo (two presets have this name, letters closer together and further apart, as in the original demo)"), { Font: "bold 256px verdana", Fill: "#786", Stroke: null, StrokeWidth: 0, ShadowX: 4, ShadowY: 8, LetterSpacing: -30 }],
            [_TF("Letter spacing", "Name of a text style preset in the text demo (two presets have this name, letters closer together and further apart, as in the original demo)"), { Font: "bold 256px verdana", Fill: "#786", StrokeWidth: 0, ShadowX: 4, ShadowY: 8, LetterSpacing: 70 }],
            [_TF("Horizontal gradient", "Name of a text style preset in the text demo"), {
                Font: "bold 256px verdana",
                FillGradient: lg({ X1: 0, Y1: 0, X2: 0, Y2: 1, Stops: [{ P: 0.2, Color: "#000" }, { P: 0.35, Color: "#00f" }, { P: 0.40, Color: "#bbf" }, { P: 0.45, Color: "#00f" }, { P: 0.8, Color: "#000" }] }),
                StrokeFirst: false, Stroke: "#0ff", StrokeWidth: 4, ShadowX: 4, ShadowY: 8,
            }],
            [_TF("Diagonal gradient", "Name of a text style preset in the text demo"), {
                Font: "bold 256px verdana",
                FillGradient: lg({ X1: 0, Y1: 0.2, X2: 1, Y2: 0.8, Stops: [{ P: 0, Color: "#f00" }, { P: 0.2, Color: "#ff0" }, { P: 0.4, Color: "#0f0" }, { P: 0.6, Color: "#0ff" }, { P: 0.8, Color: "#00f" }, { P: 1, Color: "#f0f" }] }),
                Stroke: null, ShadowX: 4, ShadowY: 8,
            }],
            [_TF("Gold", "Name of a text style preset in the text demo"), {
                Font: "bold 256px tahoma",
                StrokeWidth: 8,
                FillGradient: lg({
                    X1: 0, Y1: 0, X2: 0, Y2: 1, Stops: [
                        { P: 0, Color: "#AE7723" }, { P: 0.0299, Color: "#C0923B" }, { P: 0.0825, Color: "#DBBB60" }, { P: 0.1302, Color: "#EFD97A" },
                        { P: 0.1709, Color: "#FBEB8A" }, { P: 0.2, Color: "#FFF290" }, { P: 0.3619, Color: "#FFF08A" }, { P: 0.5316, Color: "#EABD54" },
                        { P: 0.6023, Color: "#CFA03C" }, { P: 0.7129, Color: "#A9771C" }, { P: 0.8006, Color: "#925E07" }, { P: 0.8538, Color: "#895500" },
                        { P: 0.8834, Color: "#8D5903" }, { P: 0.9118, Color: "#97640C" }, { P: 0.9399, Color: "#AA781C" }, { P: 0.9677, Color: "#C39332" },
                        { P: 0.995, Color: "#E3B64E" }, { P: 1, Color: "#EABD54" },
                    ],
                }),
                StrokeGradient: lg({ X1: 0, Y1: 0, X2: 1, Y2: 1, Stops: [{ P: 0, Color: "#EDD685" }, { P: 0.2812, Color: "#BB871D" }, { P: 0.5174, Color: "#D9B253" }, { P: 0.7922, Color: "#FFEFA2" }, { P: 1, Color: "#C4942C" }] }),
            }],
            [_TF("Neon", "Name of a text style preset in the text demo"), { Font: "bold 256px verdana", Fill: null, LetterSpacing: -15, Stroke: "#f6f", StrokeWidth: 3, ShadowX: 0, ShadowY: 0, ShadowFill: null, ShadowBlur: 20, ShadowStroke: "#f0f", StrokeFirst: false }],
        ];
    }

    /**
     * Page main: build the cards (the page is revealed when the first renders are done).
     * @returns {Promise<void>}
     */
    async function Main() {
        const p = new URL(window.location.href).searchParams;
        const text = p.get("Text") ?? "Abc123";
        const texts = p.get("Texts") ?? "Hi,Hello,Howdy,Hallo,Salam,مرحبًا,Hola,Guten tag,Ndewo,Ciao,Olá,Jambo,Hallå,Hej,Molo,Bonjour";
        await MediaPlayerReady();
        const list = Sw.el("div.td-list");
        //  Design v2 page: a left aligned page header above the cards
        document.body.appendChild(Sw.el("main.sw-page-dash", null, Sw.PageHeader({
            Title: _TF("Text Demo", "Title of the text demo page (text style presets rendered with effects; the original page title)"),
            Description: _TF("Text style presets, each with links to the image and collage effects. Open a text to see it with no effect.", "One line description below the title of the text demo page"),
        }).E, list));
        try {
            await Promise.all(Presets().map(pr => Add(list, pr[0], Object.assign(new TextStyle(), pr[1]), text, texts)));
        }
        catch (e) {
            Sw.Fail(e && e.message ? e.message : ("" + e));
        }
    }

    /**
     * Resolves when canvasTools.js is loaded (static tag).
     * @returns {Promise<void>}
     */
    async function MediaPlayerReady() {
        if (typeof CanvasTools === "function")
            return;
        await new Promise(r => window.addEventListener("load", r, { once: true }));
    }

    Sw.Media = Sw.Media || {};
    Sw.Media.TextDemo = { Main, ImageEffects, CollageEffects };
    Sw.Page.Main(Main, { Loading: true });
})();
