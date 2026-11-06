/**
 * @file mediaEdit.js - the media editor of MediaEdit.html: `Sw.MediaEdit.Main()` (booted by mediaEditPage.js).
 *
 * One page to make a media ready for a web page: open any media (a link, a YouTube address, a file from this computer by drop or
 * "Open a file…", an effect from the library, a text), see it in the same player as MediaView.html (CreateChrome), change its
 * settings in a Sw.PropertyEditor (most changes show at once: volume, speed, crop, effect variables; the others rebuild the player
 * in place, keeping the position), pop the player out into a window of its own that follows every change (MediaView.html with
 * `ctrl`), and share it: a MediaView / MediaPreview link or an embed code with only the changed settings ("Share").
 *
 * Url (case insensitive keys): `link` | `d`, `type` | `t`, `props` like MediaView.html (the same link checks), or the host data mode
 * of media.js (`get`, `id`, `update`, `editmask`: see Sw.MediaView.EditMain; "Save" then posts the settings to the host). Without a
 * link the page starts empty (a drop zone and examples). Every change is kept in the address of this page (link mode), so a reload
 * or a bookmark opens the same media with the same settings.
 *
 * The settings are described here (Schema, the media params of the players in ../mediaPlayer: MediaPlayerParams*, mirrored by the
 * C# Media* types); an effect's own variables come from its `// var:` annotations (the player's ScriptTypeInfo).
 * Needs media.js (Sw.MediaView) and the SysWeaver core / ui. Styles: mediaEdit.css.
 */
(function () {
    "use strict";

    const Sw = window.Sw;
    const MV = Sw.MediaView;
    const el = Sw.el;

    /** Media type numbers (MediaTypes of media_player.js, loaded later by media.js). */
    const TImage = 0, TVideo = 1, TAudio = 2, TYouTube = 3, TEffect = 4, TText = 5;

    /** @type {number} A number for the names of the client types of the object editor (one per rendering). */
    let EditTypeSeq = 0;

    /** @type {number} Milliseconds after the last committed change before a rebuild of the player. */
    const RebuildMs = 350;

    /** @type {string} Storage key: "hidden" = the tip above the settings was dismissed. */
    const TipKey = "SysWeaver.MediaEdit.Tip";

    /** @type {string} Storage key of the width of the settings pane (Sw.Split). */
    const SplitKey = "SysWeaver.MediaEdit.Split";

    // ------------------------------------------------------------------ helpers

    /**
     * A deep copy of JSON data.
     * @param {*} o The value.
     * @returns {*} The copy.
     */
    const clone = o => (o === null || o === undefined) ? o : JSON.parse(JSON.stringify(o));

    /**
     * Read a dotted path ("Crop.MarginTop").
     * @param {object} o The object.
     * @param {string} key The path.
     * @returns {*} The value or undefined.
     */
    function getPath(o, key) {
        let v = o;
        for (const k of key.split(".")) {
            if (v === null || typeof v !== "object")
                return undefined;
            v = v[k];
        }
        return v;
    }

    /**
     * Write a dotted path, creating the objects on the way.
     * @param {object} o The object.
     * @param {string} key The path.
     * @param {*} val The value.
     */
    function setPath(o, key, val) {
        const ks = key.split(".");
        let v = o;
        for (let i = 0; i < ks.length - 1; ++i) {
            if (v[ks[i]] === null || typeof v[ks[i]] !== "object")
                v[ks[i]] = {};
            v = v[ks[i]];
        }
        v[ks[ks.length - 1]] = val;
    }

    /**
     * A css color as [r, g, b, a] (0..1) for the color picker.
     * @param {string} s The css color.
     * @returns {number[]} The color (transparent black when not a color).
     */
    function cssToColor(s) {
        const c = typeof s === "string" && s ? Sw.Color.ParseCss(s) : null;
        return c ? [c[0] / 255, c[1] / 255, c[2] / 255, c[3]] : [0, 0, 0, 0];
    }

    /**
     * [r, g, b(, a)] (0..1) as a css hex color (#rrggbb, #rrggbbaa with alpha).
     * @param {number[]} v The color.
     * @returns {string} The css color.
     */
    function colorToCss(v) {
        const h = x => Math.round(Math.max(0, Math.min(1, x || 0)) * 255).toString(16).padStart(2, "0");
        const a = v.length > 3 ? v[3] : 1;
        return "#" + h(v[0]) + h(v[1]) + h(v[2]) + (a < 1 ? h(a) : "");
    }

    /** css font shorthand of a text style ("bold 128px Verdana"). */
    const FontRx = /^\s*(italic\s+|oblique\s+)?(normal\s+|bold\s+|bolder\s+|lighter\s+|[1-9]00\s+)?(\d+(?:\.\d+)?)px\s+(.+?)\s*$/i;

    /**
     * Split a css font shorthand.
     * @param {string} f The font ("bold 128px Verdana").
     * @returns {{Italic: boolean, Bold: boolean, Size: number, Family: string}} The parts.
     */
    function parseFont(f) {
        const m = FontRx.exec(f || "");
        if (!m)
            return { Italic: false, Bold: true, Size: 128, Family: "Verdana" };
        const w = (m[2] || "").trim().toLowerCase();
        return { Italic: !!m[1], Bold: w === "bold" || w === "bolder" || (+w >= 600), Size: +m[3], Family: m[4] };
    }

    /**
     * Join font parts to a css font shorthand.
     * @param {{Italic: boolean, Bold: boolean, Size: number, Family: string}} o The parts.
     * @returns {string} The font.
     */
    const makeFont = o => (o.Italic ? "italic " : "") + (o.Bold ? "bold " : "") + (o.Size || 128) + "px " + (o.Family || "Verdana");

    /**
     * The font choices of a text: fonts every device has (web safe), the generic families, and the current one when it is another.
     * @param {string} cur The current family.
     * @returns {Array<{Value: string, Label: string, Hint: string}>} The choices.
     */
    function FontOptions(cur) {
        const safe = ["Arial", "Verdana", "Tahoma", "Trebuchet MS", "Georgia", "Times New Roman", "Courier New", "Impact", "Comic Sans MS"];
        const generic = [["sans-serif", _TF("Plain font", "A generic font family in the media editor (css sans-serif)"), _TF("the device's plain font", "Hint of a generic font family in the media editor")],
            ["serif", _TF("Book font", "A generic font family in the media editor (css serif)"), _TF("the device's book font", "Hint of a generic font family in the media editor")],
            ["monospace", _TF("Code font", "A generic font family in the media editor (css monospace)"), _TF("the device's code font", "Hint of a generic font family in the media editor")],
            ["system-ui", _TF("System font", "A generic font family in the media editor (css system-ui)"), _TF("the font of the device's menus", "Hint of a generic font family in the media editor")]];
        const out = safe.map(f => ({ Value: f, Label: f }));
        for (const [f, l, h] of generic)
            out.push({ Value: f, Label: l, Hint: h });
        if (cur && !out.some(o => o.Value === cur))
            out.unshift({ Value: cur, Label: cur, Hint: _TF("from the link or the font file", "Hint of the current font family in the media editor when it is not a listed one") });
        return out;
    }

    /**
     * The file name of a link (the last path part without the query), the YouTube code, or the start of a text.
     * @param {number} type The media type.
     * @param {string} data The link / code / text.
     * @returns {string} The name.
     */
    function MediaName(type, data) {
        if (type === TYouTube)
            return _T("YouTube {0}", data, "Title of the media viewer showing a YouTube video, {0} is the video code");
        if (type === TText) {
            const s = String(data || "").replace(/\s+/g, " ").trim();
            return s.length > 40 ? s.substring(0, 39) + "…" : s;
        }
        return Sw.Url.FileName(String(data || "").split(/[?#]/)[0]) || String(data || "");
    }

    /**
     * A code name as words: "OldMovie" -> "Old movie", "NoiseInt" -> "Noise int", "3D-flag" -> "3D flag", "BgColor" -> "Bg color".
     * @param {string} n The name.
     * @returns {string} The words.
     */
    function Humanize(n) {
        const s = String(n || "").replace(/[-_]+/g, " ").replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2").trim();
        //  Short names are letters (CRT, TV, VHS, RGB)
        //  Names spelled differently in the file name
        const fixed = { MetallicVoronotRoses: "Metallic Voronoi roses" };
        if (fixed[n])
            return fixed[n];
        const caps = /^(crt|tv|vhs|rgb|hdr|ui|3d|2d|uv)$/i;
        return s.split(" ").map((w, i) => caps.test(w) ? w.toUpperCase() : i && /^[A-Z][a-z]+$/.test(w) ? w.toLowerCase() : w).join(" ");
    }

    // ------------------------------------------------------------------ media types and their settings

    /**
     * The media types (the type select, the empty state).
     * @returns {Array<{Value: number, Text: string, Icon: string, Hint: string}>} The types.
     */
    function Kinds() {
        return [
            { Value: TImage, Text: _TF("Image", "Media type in the media editor"), Icon: "image", Hint: _TF("A picture file: png, jpg, svg, webp, gif", "Description of the image media type in the media editor") },
            { Value: TVideo, Text: _TF("Video", "Media type in the media editor"), Icon: "screen", Hint: _TF("A video file: mp4, webm, ogv", "Description of the video media type in the media editor") },
            { Value: TAudio, Text: _TF("Audio", "Media type in the media editor"), Icon: "mic", Hint: _TF("A sound file: mp3, wav, ogg, m4a", "Description of the audio media type in the media editor") },
            { Value: TYouTube, Text: _TF("YouTube", "Media type in the media editor"), Icon: "play", Hint: _TF("A YouTube video: its link or its video code", "Description of the YouTube media type in the media editor") },
            { Value: TEffect, Text: _TF("Effect", "Media type in the media editor (an animated GLSL shader)"), Icon: "sparkle", Hint: _TF("An animated effect (a .glsl file)", "Description of the effect media type in the media editor") },
            { Value: TText, Text: _TF("Text", "Media type in the media editor"), Icon: "text", Hint: _TF("A text drawn in a style, with an effect if you like", "Description of the text media type in the media editor") },
        ];
    }

    /** @returns {string} The label of a media type. @param {number} type The type. */
    const KindName = type => (Kinds().find(k => k.Value === type) || { Text: "?" }).Text;

    /** Defaults of the effect params (MediaPlayerParamsEffect). */
    const EffectDefaults = {
        Width: 1920, Height: 1080, Speed: 1, Offset: 0, LimitSize: false, DpiAdjust: false, DpiScale: 1, Static: false, Transparent: false,
        AdaptiveSize: true, WrapU: false, WrapY: false, Texture: null, WebGL2: false, MipMap: false, AntiAlias: false, FxProps: null,
    };

    /**
     * The settings of a media type as the player uses them when nothing is given (the MediaPlayerParams* defaults).
     * @param {number} type The media type.
     * @param {?string} [data] The media (link): an svg image fills its box by default, as the image player does (it has no pixel size).
     * @returns {object} A new object.
     */
    function Defaults(type, data) {
        switch (type) {
            case TImage:
                return { Crop: null, Duration: 10, Effect: null, EffectParams: null, EffectSizeFromImage: true, Transparent: false, AdaptiveSize: String(data || "").endsWith(".svg") };
            case TVideo:
                return { StartAt: 0, EndAt: 0, Volume: 100, Stream: 0, Transparent: false, Crop: null };
            case TAudio:
                return { StartAt: 0, EndAt: 0, Volume: 100 };
            case TYouTube:
                return {
                    Width: 1920, Height: 1920, StartAt: 0, EndAt: 0, Volume: 100, Stream: 0,
                    Crop: { MarginAsPercentage: true, MarginTopP: 21.875, MarginRightP: 0, MarginBottomP: 21.875, MarginLeftP: 0 },
                };
            case TEffect:
                return Object.assign({}, EffectDefaults);
            case TText:
                return {
                    Font: "bold 128px Verdana", FontUrl: null, LetterSpacing: 0, Fill: "#fff", Stroke: "#006", StrokeWidth: 2, ShadowFill: "#000c",
                    ShadowX: 3, ShadowY: 5, ShadowBlur: 12, CollageSeparator: null, CollageColumns: 2, Crop: null, Duration: 10,
                    Effect: null, EffectParams: null, EffectSizeFromImage: true, Transparent: false, AdaptiveSize: false,
                };
        }
        return {};
    }

    /** Group names of the settings. */
    const G = {
        Picture: () => _TF("Picture", "Group of settings in the media editor"),
        Playback: () => _TF("Playback", "Group of settings in the media editor"),
        Crop: () => _TF("Crop", "Group of settings in the media editor (cut off the edges)"),
        Effect: () => _TF("Effect", "Group of settings in the media editor (an animated effect over a picture or text)"),
        Size: () => _TF("Size", "Group of settings in the media editor"),
        PlayerBox: () => _TF("YouTube player box", "Group of settings in the media editor: the size of the YouTube player (the video fits into it)"),
        Time: () => _TF("Time", "Group of settings in the media editor"),
        Quality: () => _TF("Quality", "Group of settings in the media editor"),
        Texture: () => _TF("Texture", "Group of settings in the media editor (a picture used by an effect)"),
        Font: () => _TF("Font", "Group of settings in the media editor"),
        Colors: () => _TF("Colors", "Group of settings in the media editor"),
        Shadow: () => _TF("Shadow", "Group of settings in the media editor"),
        Collage: () => _TF("Collage", "Group of settings in the media editor (several texts in a grid)"),
        Variables: () => _TF("Effect variables", "Group of settings in the media editor: the variables of the effect's shader code"),
    };

    /**
     * The crop settings.
     * @param {object|null} def The default crop of the type (null = no crop).
     * @returns {Array<object>} Definitions.
     */
    function CropDefs(def) {
        const has = p => !!p.Crop;
        const pct = p => !!p.Crop && !!p.Crop.MarginAsPercentage;
        const sides = [
            ["Top", _TF("Top", "Crop margin in the media editor")], ["Right", _TF("Right", "Crop margin in the media editor")],
            ["Bottom", _TF("Bottom", "Crop margin in the media editor")], ["Left", _TF("Left", "Crop margin in the media editor")],
        ];
        const g = G.Crop();
        return [
            {
                Key: "$Crop", Label: _TF("Crop the edges", "Setting in the media editor"), Type: "bool", Default: !!def, Group: g, Live: "Crop",
                Desc: _TF("Cut off the edges of the media, ex: black bars", "Description of a setting in the media editor"),
                Get: p => !!p.Crop,
                Set: (p, v) => p.Crop = v ? Object.assign({ MarginAsPercentage: true, MarginTopP: 0, MarginRightP: 0, MarginBottomP: 0, MarginLeftP: 0 }, clone(def) || {}) : null,
            },
            {
                Key: "Crop.MarginAsPercentage", Label: _TF("Margins in", "Setting in the media editor: the unit of the crop margins"), Type: "enum", Group: g, Live: "Crop", Show: has,
                Default: def ? !!def.MarginAsPercentage : true,
                Options: [{ Value: true, Label: _TF("Percent of the size", "Unit of the crop margins") }, { Value: false, Label: _TF("Pixels", "Unit of the crop margins") }],
            },
            ...sides.map(([s, label]) => ({
                Key: "Crop.Margin" + s + "P", Label: label, Type: "float", Min: 0, Max: 50, Step: 0.5, Unit: "%", Group: g, Live: "Crop", Show: pct,
                Default: def ? (def["Margin" + s + "P"] || 0) : 0,
            })),
            ...sides.map(([s, label]) => ({
                Key: "Crop.Margin" + s, Label: label, Type: "int", Min: 0, Max: 2000, Step: 1, Unit: "px", Group: g, Live: "Crop", Show: p => has(p) && !pct(p),
                Default: def ? (def["Margin" + s] || 0) : 0,
            })),
            {
                Key: "Crop.VisualizeMargins", Label: _TF("Show what is cut off", "Setting in the media editor"), Type: "bool", Default: false, Group: g, Live: "Crop", Show: has, Local: true,
                Desc: _TF("Shows the cut off edges in red here instead of cutting them (only in the editor)", "Description of a setting in the media editor"),
            },
        ];
    }

    /**
     * The play window and volume settings (video, audio, YouTube).
     * @param {object} ctx The context: Duration (seconds, 0 = unknown).
     * @param {boolean} stream With the Stream choice.
     * @returns {Array<object>} Definitions.
     */
    function PlaybackDefs(ctx, stream) {
        const g = G.Playback();
        const max = ctx.Duration > 0 && isFinite(ctx.Duration) ? Math.ceil(ctx.Duration * 10) / 10 : 600;
        const defs = [
            {
                Key: "StartAt", Label: _TF("Start at", "Setting in the media editor: the play position to start at"), Type: "float", Min: 0, Max: max, Step: 0.1, Unit: "s", Default: 0, Group: g,
                Desc: _TF("Playback starts here, and returns here on Stop and when it loops", "Description of a setting in the media editor"),
                Menu: () => ctx.Pos ? [{ Text: _T("Use the play position ({0})", MV.FormatTime(ctx.Pos()), "Item of the menu of a time setting in the media editor"), Icon: "target", OnClick: () => ctx.SetFromPos("StartAt") }] : [],
                MenuTitle: _TF("More for Start at", "Tool tip of the menu button of the start setting in the media editor"),
            },
            {
                Key: "EndAt", Label: _TF("End at", "Setting in the media editor: the play position to stop at"), Type: "float", Min: 0, Max: max, Step: 0.1, Unit: "s", Default: 0, Group: g,
                Hint: _TF("0 = to the end", "Hint of a setting in the media editor"),
                Desc: _TF("Playback stops (or loops) here; 0 plays to the end", "Description of a setting in the media editor"),
                Menu: () => ctx.Pos ? [{ Text: _T("Use the play position ({0})", MV.FormatTime(ctx.Pos()), "Item of the menu of a time setting in the media editor"), Icon: "target", OnClick: () => ctx.SetFromPos("EndAt") }] : [],
                MenuTitle: _TF("More for End at", "Tool tip of the menu button of the end setting in the media editor"),
            },
            {
                Key: "Volume", Label: _TF("Media volume", "Setting in the media editor: the volume of this media"), Type: "float", Min: 0, Max: 100, Step: 1, Unit: "%", Default: 100, Group: g, Live: "Volume",
                Hint: _TF("times the player volume", "Hint of a setting in the media editor: the media volume is multiplied with the volume of the player"),
                Desc: _TF("The loudness of this media (ex: a loud clip at 50 %); the player's volume button is the visitor's own volume on top of it", "Description of a setting in the media editor"),
            },
        ];
        if (stream)
            defs.push({
                Key: "Stream", Label: _TF("Use", "Setting in the media editor: what streams of a video to use"), Type: "enum", Default: 0, Group: g,
                Options: [
                    { Value: 0, Label: _TF("Picture and sound", "Choice of the video streams in the media editor") },
                    { Value: 1, Label: _TF("Only the picture", "Choice of the video streams in the media editor") },
                    { Value: 2, Label: _TF("Only the sound", "Choice of the video streams in the media editor") },
                ],
            });
        return defs;
    }

    /**
     * The effect settings of an image or a text (an effect drawn over it).
     * @param {object} ctx The context: EffectOptions(kind, current).
     * @param {string} kind "image" or "collage" (the effect library to choose from).
     * @returns {Array<object>} Definitions.
     */
    function OverlayEffectDefs(ctx, kind) {
        const g = G.Effect();
        const on = p => !!p.Effect;
        return [
            {
                Key: "Effect", Label: _TF("Effect", "Setting in the media editor: an effect drawn over the picture"), Type: "enum", Default: "", Group: g,
                Options: ctx.EffectOptions(kind),
                Desc: _TF("An animated effect that draws the picture (ex: an old TV, rain, a waving flag); the Effects button at the top chooses it too", "Description of a setting in the media editor"),
                Get: p => p.Effect || "", Set: (p, v) => p.Effect = (v || "").trim() || null,
            },
            {
                Key: "EffectSizeFromImage", Label: _TF("Size of the picture", "Setting in the media editor"), Type: "bool", Default: true, Group: g, Show: on,
                Desc: _TF("On: the effect takes the size of the picture; off: the effect's own size", "Description of a setting in the media editor"),
            },
            {
                Key: "EffectParams.Speed", Label: _TF("Speed", "Setting in the media editor: the speed of an effect"), Type: "float", Min: -4, Max: 4, Step: 0.05, Unit: "×", Default: 1, Group: g, Show: on, Live: "Speed",
                Desc: _TF("How fast the effect plays; 0 stops it, below 0 plays it backwards", "Description of a setting in the media editor"),
            },
            {
                Key: "EffectParams.Static", Label: _TF("Still picture", "Setting in the media editor"), Type: "bool", Default: false, Group: g, Show: on,
                Desc: _TF("Draws the effect once (a still picture) instead of animating it", "Description of a setting in the media editor"),
            },
        ];
    }

    /**
     * The settings of a media type.
     * @param {number} type The media type.
     * @param {object} ctx The context (Data: the media link, Duration, Pos, SetFromPos, EffectOptions, TextureMenu).
     * @returns {Array<object>} PropertyEditor definitions with: Get / Set (virtual values), Show(props), Live (applied without a
     *   rebuild), Local (editor only, never shared).
     */
    function Schema(type, ctx) {
        const d = Defaults(type, ctx && ctx.Data);
        switch (type) {
            case TImage:
                return [
                    {
                        Key: "Duration", Label: _TF("Show for", "Setting in the media editor: how long an image is shown"), Type: "int", Min: 0, Max: 300, Step: 1, Unit: "s", Default: 10, Group: G.Picture(),
                        Hint: _TF("0 = no end", "Hint of a setting in the media editor"),
                        Desc: _TF("How long the picture is shown (its time line, ex: in a slideshow); 0 = no end", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "AdaptiveSize", Label: _TF("Fill the box", "Setting in the media editor"), Type: "bool", Default: d.AdaptiveSize, Group: G.Picture(), Live: "AdaptiveSize",
                        Desc: _TF("Scales the picture to the size of its box (keeps its shape)", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "Transparent", Label: _TF("Transparent", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Picture(),
                        Desc: _TF("The picture has see-through parts: no border is drawn around it", "Description of a setting in the media editor"),
                    },
                    ...CropDefs(null),
                    ...OverlayEffectDefs(ctx, "image"),
                ];
            case TVideo:
                return [
                    ...PlaybackDefs(ctx, true),
                    {
                        Key: "Transparent", Label: _TF("Transparent", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Picture(),
                        Desc: _TF("The video has see-through parts: no border is drawn around it", "Description of a setting in the media editor"),
                    },
                    ...CropDefs(null),
                ];
            case TAudio:
                return PlaybackDefs(ctx, false);
            case TYouTube:
                return [
                    ...PlaybackDefs(ctx, true),
                    {
                        Key: "Width", Label: _TF("Width", "Setting in the media editor"), Type: "int", Min: 144, Max: 3840, Step: 8, Unit: "px", Default: 1920, Group: G.PlayerBox(), Hint: _TF("square: see Crop", "Hint of the size of the YouTube player in the media editor: it is square by default, the crop makes it 16:9"),
                        Desc: _TF("The size of the YouTube player (the video fits into it)", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "Height", Label: _TF("Height", "Setting in the media editor"), Type: "int", Min: 144, Max: 3840, Step: 8, Unit: "px", Default: 1920, Group: G.PlayerBox(), Hint: _TF("square: see Crop", "Hint of the size of the YouTube player in the media editor: it is square by default, the crop makes it 16:9"),
                        Desc: _TF("The size of the YouTube player; the default square is cropped to 16:9", "Description of a setting in the media editor"),
                    },
                    ...CropDefs(d.Crop),
                ];
            case TEffect:
                return [
                    {
                        Key: "Speed", Label: _TF("Speed", "Setting in the media editor: the speed of an effect"), Type: "float", Min: -4, Max: 4, Step: 0.05, Unit: "×", Default: 1, Group: G.Time(), Live: "Speed",
                        Desc: _TF("How fast the effect plays; 0 stops it, below 0 plays it backwards", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "Offset", Label: _TF("Time offset", "Setting in the media editor"), Type: "float", Min: 0, Max: 600, Step: 0.1, Unit: "s", Default: 0, Group: G.Time(), Live: "Offset",
                        Desc: _TF("Starts the effect this many seconds in", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "Static", Label: _TF("Still picture", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Time(),
                        Desc: _TF("Draws the effect once (a still picture) instead of animating it", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "AdaptiveSize", Label: _TF("Fill the box", "Setting in the media editor"), Type: "bool", Default: true, Group: G.Size(),
                        Desc: _TF("On: the effect is drawn at the size of its box; off: at the width and height below, scaled to fit", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "LimitSize", Label: _TF("At most the size below", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Size(), Show: p => p.AdaptiveSize !== false,
                        Desc: _TF("Never draws more pixels than the width and height below (saves power on big screens)", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "Width", Label: _TF("Width", "Setting in the media editor"), Type: "int", Min: 144, Max: 7680, Step: 8, Unit: "px", Default: 1920, Group: G.Size(), Show: p => p.AdaptiveSize === false || !!p.LimitSize,
                    },
                    {
                        Key: "Height", Label: _TF("Height", "Setting in the media editor"), Type: "int", Min: 144, Max: 7680, Step: 8, Unit: "px", Default: 1080, Group: G.Size(), Show: p => p.AdaptiveSize === false || !!p.LimitSize,
                    },
                    {
                        Key: "Transparent", Label: _TF("Transparent", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Size(),
                        Desc: _TF("The effect draws see-through parts: the page shows through them", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "DpiScale", Label: _TF("Sharpness", "Setting in the media editor: the resolution of an effect"), Type: "float", Min: 0.1, Max: 1, Step: 0.05, Scale: 100, Unit: "%", Default: 1, Group: G.Quality(),
                        Desc: _TF("Draws fewer pixels below 100 % (faster on slow devices, a bit blurry)", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "DpiAdjust", Label: _TF("Adjust to the device", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Quality(),
                        Desc: _TF("Lowers the sharpness by itself when the device is too slow", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "AntiAlias", Label: _TF("Smooth edges", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Quality(),
                        Desc: _TF("Anti aliasing (MSAA): smoother edges, a bit slower", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "WebGL2", Label: _TF("WebGL 2", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Quality(),
                        Desc: _TF("Runs the effect with WebGL 2 (some effects need it)", "Description of a setting in the media editor"),
                    },
                    {
                        Key: "Texture", Label: _TF("Picture", "Setting in the media editor: the texture of an effect"), Type: "string", Default: "", Group: G.Texture(),
                        Hint: _TF("an image file · empty = none", "Hint of a setting in the media editor; the parts are separated by ' · '"),
                        Desc: _TF("A picture the effect can use (its texture)", "Description of a setting in the media editor"),
                        Get: p => p.Texture || "", Set: (p, v) => p.Texture = (v || "").trim() || null,
                        Menu: () => ctx.TextureMenu(), MenuTitle: _TF("Choose a picture", "Tool tip of the menu button of the texture setting in the media editor"),
                    },
                    {
                        Key: "WrapU", Label: _TF("Repeat across", "Setting in the media editor: repeat the texture horizontally"), Type: "bool", Default: false, Group: G.Texture(), Show: p => !!p.Texture,
                    },
                    {
                        Key: "WrapY", Label: _TF("Repeat down", "Setting in the media editor: repeat the texture vertically"), Type: "bool", Default: false, Group: G.Texture(), Show: p => !!p.Texture,
                    },
                    {
                        Key: "MipMap", Label: _TF("Smooth when small", "Setting in the media editor: mip maps of the texture"), Type: "bool", Default: false, Group: G.Texture(), Show: p => !!p.Texture,
                    },
                ];
            case TText: {
                const font = k => ({ Get: p => parseFont(p.Font)[k], Set: (p, v) => { const f = parseFont(p.Font); f[k] = v; p.Font = makeFont(f); } });
                const color = (key, label, def, group, desc) => ({
                    Key: key, Label: label, Type: "color4", Default: cssToColor(def), Group: group, Desc: desc,
                    Get: p => cssToColor(p[key] ?? def), Set: (p, v) => p[key] = colorToCss(v), Color: true,
                });
                return [
                    Object.assign({ Key: "$FontFamily", Label: _TF("Font", "Setting in the media editor: the font family"), Type: "enum", Default: "Verdana", Group: G.Font(), Options: FontOptions(parseFont(ctx.Props && ctx.Props.Font).Family), Help: _TF("A device without this font shows a similar one.", "Help under the font setting in the media editor"), Desc: _TF("The font family (a font every device has, or the one of the font file below)", "Description of a setting in the media editor") }, font("Family")),
                    Object.assign({ Key: "$FontSize", Label: _TF("Size", "Setting in the media editor: the font size"), Type: "int", Min: 8, Max: 400, Step: 1, Unit: "px", Default: 128, Group: G.Font() }, font("Size")),
                    Object.assign({ Key: "$Bold", Label: _TF("Bold", "Setting in the media editor"), Type: "bool", Default: true, Group: G.Font() }, font("Bold")),
                    Object.assign({ Key: "$Italic", Label: _TF("Italic", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Font() }, font("Italic")),
                    {
                        Key: "LetterSpacing", Label: _TF("Letter spacing", "Setting in the media editor"), Type: "float", Min: -20, Max: 100, Step: 1, Unit: "px", Default: 0, Group: G.Font(),
                    },
                    {
                        Key: "FontUrl", Label: _TF("Font file", "Setting in the media editor: a web font to load"), Type: "string", Default: "", Group: G.Font(),
                        Hint: _TF(".woff2 or .ttf · empty = installed font", "Hint of a setting in the media editor; the parts are separated by ' · '"),
                        Get: p => p.FontUrl || "", Set: (p, v) => p.FontUrl = (v || "").trim() || null,
                    },
                    color("Fill", _TF("Fill", "Setting in the media editor: the color of the letters"), d.Fill, G.Colors()),
                    color("Stroke", _TF("Outline", "Setting in the media editor: the color of the outline of the letters"), d.Stroke, G.Colors()),
                    {
                        Key: "StrokeWidth", Label: _TF("Outline width", "Setting in the media editor"), Type: "float", Min: 0, Max: 30, Step: 0.5, Unit: "px", Default: 2, Group: G.Colors(),
                    },
                    color("ShadowFill", _TF("Shadow", "Setting in the media editor: the color of the shadow"), d.ShadowFill, G.Shadow()),
                    { Key: "ShadowX", Label: _TF("Shadow right", "Setting in the media editor: the shadow offset"), Type: "float", Min: -50, Max: 50, Step: 1, Unit: "px", Default: 3, Group: G.Shadow() },
                    { Key: "ShadowY", Label: _TF("Shadow down", "Setting in the media editor: the shadow offset"), Type: "float", Min: -50, Max: 50, Step: 1, Unit: "px", Default: 5, Group: G.Shadow() },
                    { Key: "ShadowBlur", Label: _TF("Shadow blur", "Setting in the media editor"), Type: "float", Min: 0, Max: 60, Step: 1, Unit: "px", Default: 12, Group: G.Shadow() },
                    {
                        Key: "CollageSeparator", Label: _TF("Split the text at", "Setting in the media editor: the separator of a collage"), Type: "string", Default: "", Group: G.Collage(),
                        Hint: _TF("ex: a comma · empty = one text", "Hint of a setting in the media editor; the parts are separated by ' · '"),
                        Desc: _TF("Splits the text into several texts shown in a grid (a collage)", "Description of a setting in the media editor"),
                        Get: p => p.CollageSeparator || "", Set: (p, v) => p.CollageSeparator = v ? v : null,
                    },
                    {
                        Key: "CollageColumns", Label: _TF("Columns", "Setting in the media editor: the columns of a collage"), Type: "int", Min: 1, Max: 12, Step: 1, Default: 2, Group: G.Collage(), Show: p => !!p.CollageSeparator,
                    },
                    {
                        Key: "Duration", Label: _TF("Show for", "Setting in the media editor: how long an image is shown"), Type: "int", Min: 0, Max: 300, Step: 1, Unit: "s", Default: 10, Group: G.Picture(),
                        Hint: _TF("0 = no end", "Hint of a setting in the media editor"),
                    },
                    {
                        Key: "AdaptiveSize", Label: _TF("Fill the box", "Setting in the media editor"), Type: "bool", Default: false, Group: G.Picture(), Live: "AdaptiveSize",
                    },
                    ...OverlayEffectDefs(ctx, "collage"),
                ];
            }
        }
        return [];
    }

    /**
     * The settings of an effect's own variables (its `// var:` annotations, the player's ScriptTypeInfo members).
     * @param {Array<object>} members The members.
     * @returns {Array<object>} PropertyEditor definitions (Key "fx:<name>", Fx: the variable, Conv: "color" | "vecN" | null).
     */
    function FxDefs(members) {
        const out = [];
        for (const m of members || []) {
            const tn = m.TypeName || "";
            const base = tn.replace(/_.*$/, "");
            const col = /_(col|colhdr)$/.test(tn);
            const d = { Key: "fx:" + m.Name, Label: m.DisplayName && m.DisplayName !== m.Name ? m.DisplayName : Humanize(m.Name), Desc: m.Summary || undefined, Fx: m.Name, Group: G.Variables(), Conv: null };
            let def = null;
            try {
                def = m.Default ? JSON.parse(m.Default) : null;
            }
            catch {
                def = null;
            }
            const n = base === "glsl.vec2" ? 2 : base === "glsl.vec3" ? 3 : base === "glsl.vec4" ? 4 : 0;
            if (base === "System.Single" || base === "System.Double")
                d.Type = "float";
            else if (base === "System.Int32")
                d.Type = "int";
            else if (base === "System.Boolean")
                d.Type = "bool";
            else if (n && col) {
                d.Type = n === 4 ? "color4" : "color";
                d.Conv = "color";
            }
            else if (n) {
                d.Type = "vec" + n;
                d.Conv = "vec" + n;
            }
            else
                continue;
            if (d.Type !== "bool" && !d.Conv?.startsWith("color")) {
                const lo = parseFloat(m.Min), hi = parseFloat(m.Max);
                if (!isNaN(lo) && !isNaN(hi) && hi > lo) {
                    d.Min = lo;
                    d.Max = hi;
                    const st = parseFloat(m.EditParams);
                    if (st > 0)
                        d.Step = st;
                    else if (d.Type === "float")
                        d.Step = +((hi - lo) / 100).toPrecision(2);
                }
                else
                    d.NoSlider = true;
            }
            d.Default = FxToEd(d, def);
            out.push(d);
        }
        return out;
    }

    /**
     * An effect variable value as the property editor shows it.
     * @param {object} d The definition (FxDefs).
     * @param {*} v The value ({x, y, ..} for vectors, {Red, Green, Blue, Alpha} for colors).
     * @returns {*} The editor value.
     */
    function FxToEd(d, v) {
        if (d.Conv === "color") {
            v = v || {};
            const a = [v.Red ?? 1, v.Green ?? 1, v.Blue ?? 1];
            if (d.Type === "color4")
                a.push(v.Alpha ?? 1);
            return a;
        }
        if (d.Conv) {
            v = v || {};
            return ["x", "y", "z", "w"].slice(0, +d.Conv[3]).map(k => v[k] ?? 0);
        }
        return v;
    }

    /**
     * An editor value as the effect variable value.
     * @param {object} d The definition (FxDefs).
     * @param {*} v The editor value.
     * @returns {*} The value.
     */
    function FxFromEd(d, v) {
        if (d.Conv === "color") {
            const o = { Red: v[0], Green: v[1], Blue: v[2] };
            if (d.Type === "color4")
                o.Alpha = v.length > 3 ? v[3] : 1;
            return o;
        }
        if (d.Conv) {
            const o = {};
            ["x", "y", "z", "w"].slice(0, +d.Conv[3]).forEach((k, i) => o[k] = v[i] ?? 0);
            return o;
        }
        return v;
    }

    /**
     * Keep only what the property editor needs of a definition (the editor warns about nothing, but keeps the definitions small).
     * @param {object} d A definition.
     * @param {boolean} ro Read only.
     * @returns {object} The PropertyEditor definition.
     */
    function EdDef(d, ro) {
        const o = Object.assign({}, d);
        for (const k of ["Get", "Set", "Show", "Live", "Local", "Fx", "Conv", "Color"])
            delete o[k];
        if (ro) {
            o.ReadOnly = true;
            delete o.Menu;
        }
        return o;
    }

    // ------------------------------------------------------------------ the editor

    /**
     * The media editor of one page (see the file description).
     */
    class Editor {
        constructor() {
            const t = this;
            /** @type {number} Media type (-1 = none yet). */
            t.Type = -1;
            /** @type {string} The link, the YouTube code or the text. */
            t.Data = "";
            /** @type {object} The media params (the settings). */
            t.Props = {};
            /** @type {?object} The player. */
            t.Player = null;
            /** @type {?string} The name of a file of this computer (the link is then a blob: address of this page only). */
            t.LocalName = null;
            /** @type {?object} Host data mode (ReadEditSource). */
            t.Host = null;
            /** @type {boolean} The settings may be changed. */
            t.CanEdit = true;
            /** @type {?string} The settings as saved (host data mode). */
            t.Saved = null;
            /** @type {?{Win: Window, Timer: number}} The popped out player window. */
            t.Undocked = null;
            /** @type {Array<string>} Undo snapshots (JSON), and the redo ones. */
            t.UndoStack = [];
            t.RedoStack = [];
            /** @type {?object} The effect variable values as the effect defines them (for the "changed" count and sharing). */
            t.FxDefaults = null;
            t.RebuildTimer = 0;
            t.Building = Promise.resolve();
            t.Prefix = "";
        }

        // ---------------------------------------------------------------- layout

        /**
         * Create the page: the bar (type, source, actions), the stage (player) and the settings pane.
         */
        Layout() {
            const t = this;
            document.body.classList.add("me-page");
            //  The type
            t.TypeSel = new Sw.Select({
                Items: Kinds().map(k => ({ Value: k.Value, Text: k.Text, Icon: k.Icon, Hint: k.Hint })),
                Value: TImage, Title: _TF("The media type", "Tool tip of the type select of the media editor"),
                AriaLabel: _TF("Media type", "Accessible name of the type select of the media editor"), Dirty: false,
                OnChange: v => t.ChangeType(v),
            });
            //  The source: a link, a YouTube address or code, the text of a text media
            t.Source = new Sw.TextBox({
                Placeholder: _TF("Paste a link or a YouTube address (Enter opens it), or drop a file", "Placeholder of the source box of the media editor"),
                AriaLabel: _TF("Media address", "Accessible name of the source box of the media editor"),
                Icon: "link", Clear: true, SpellCheck: false,
                OnEnter: () => t.OpenTyped(),
            });
            t.Source.E.classList.add("me-source-box");
            //  A pasted media link opens at once (Enter opens a typed one)
            t.Source.Input.addEventListener("paste", () => setTimeout(() => {
                if (MV.ParseMediaLink(t.Source.Get() || ""))
                    t.OpenTyped();
            }, 0));
            t.OpenB = new Sw.Button({
                Text: _TF("Open link", "Button of the media editor: opens the typed address"), Icon: "open",
                Title: _TF("Show the media of the typed address", "Tool tip of a button of the media editor"),
                OnClick: () => t.OpenTyped(),
            });
            t.FileInput = el("input", { type: "file", hidden: true, attrs: { accept: "image/*,video/*,audio/*,.glsl,.txt", "aria-hidden": "true", tabindex: "-1" } });
            t.FileInput.addEventListener("change", () => {
                const f = t.FileInput.files && t.FileInput.files[0];
                t.FileInput.value = "";
                if (f)
                    t.OpenFile(f);
            });
            t.FileB = new Sw.Button({
                Text: _TF("Open file…", "Button of the media editor: opens a file of this computer"), Icon: "upload", Class: "me-act",
                Title: _TF("Open a media file of this computer (also by dropping it on the page)", "Tool tip of a button of the media editor"),
                OnClick: () => t.FileInput.click(),
            });
            t.LibB = new Sw.Button({
                Text: _TF("Effects", "Button of the media editor: the library of effects"), Icon: "sparkle", Class: "me-act",
                Title: _TF("Choose an animated effect of the library (for a picture or a text: the effect drawn over it)", "Tool tip of a button of the media editor"),
                OnClick: b => t.PickEffect(b.E),
            });
            //  The actions
            t.UndockB = new Sw.Button({
                Text: _TF("Pop out", "Button of the media editor: shows the player in a window of its own"), Icon: "window",
                Title: _TF("Show the player in a window of its own (ex: on another screen); it follows every change", "Tool tip of a button of the media editor"),
                Pressable: true, AutoToggle: false, Class: "me-popout",
                OnClick: () => t.Undocked ? t.Dock() : t.Undock(),
            });
            t.ShareB = new Sw.Button({
                Text: _TF("Share", "Button of the media editor: a link or embed code of the media"), Icon: "share", Kind: "primary",
                Title: _TF("A link or an embed code of this media with these settings", "Tool tip of a button of the media editor"),
                OnClick: () => t.Share(),
            });
            t.MoreB = new Sw.Button({
                Icon: "moreV", Kind: "ghost", Title: _TF("More", "Tool tip of the more button of the media editor"),
                Menu: () => t.MoreMenu(),
            });
            const bar = el("header.me-bar", null,
                el("div.me-source", null, t.TypeSel.E, t.Source.E, t.FileB.E, t.LibB.E, t.FileInput),
                el("div.me-actions", null, t.UndockB.E, t.ShareB.E, t.MoreB.E));
            //  The stage
            t.Stage = el("div.me-stage");
            //  The settings
            t.SideTitle = el("h1.me-side-title");
            t.SideSub = el("div.me-side-sub");
            t.UndoB = new Sw.Button({ Icon: "undo", Kind: "ghost", Small: true, Title: _TF("Undo", "Tool tip of a button of the media editor"), Shortcut: "Mod+Z", OnClick: () => t.Undo() });
            t.RedoB = new Sw.Button({ Icon: "redo", Kind: "ghost", Small: true, Title: _TF("Redo", "Tool tip of a button of the media editor"), Shortcut: "Mod+Y", OnClick: () => t.Redo() });
            t.ResetB = new Sw.Button({
                Text: _TF("Reset all", "Button of the media editor: all settings back to their defaults"), Icon: "reset", Kind: "ghost", Small: true,
                Title: _TF("All settings back to their defaults", "Tool tip of a button of the media editor"),
                OnClick: () => t.ResetAll(),
            });
            t.Props_ = el("div.me-props");
            t.Fx_ = el("div.me-fx");
            t.Foot = el("div.me-side-foot", { hidden: true });
            t.Side = el("aside.me-side", { attrs: { "aria-label": _TF("Media settings", "Accessible name of the settings pane of the media editor") } },
                el("div.me-side-head", null,
                    el("div.me-side-text", null, t.SideTitle, t.SideSub),
                    el("div.me-side-tools", null, t.UndoB.E, t.RedoB.E, t.ResetB.E)),
                el("div.me-side-scroll", null, t.Props_, t.Fx_),
                t.Foot);
            const narrow = window.matchMedia("(max-width: 760px)").matches;
            t.Split = new Sw.Split({ A: t.Stage, B: t.Side, Size: narrow ? 0.45 : 0.64, Vertical: narrow, Key: narrow ? SplitKey + ".V" : SplitKey, Min: 240, Label: _TF("Resize the player and the settings", "Accessible name of the bar between the player and the settings of the media editor") });
            t.Drop = el("div.me-drop", { attrs: { "aria-hidden": "true" } }, el("div.me-drop-text", { text: _TF("Drop to open", "Shown while a file or link is dragged over the media editor") }));
            t.App = el("div.me-app", null, bar, el("div.me-body", null, t.Split.E), t.Drop);
            document.body.appendChild(t.App);
            t.HookDrop();
            t.HookKeys();
            t.UpdateChrome();
        }

        /**
         * Enable / disable the bar and the pane tools for the current state.
         */
        UpdateChrome() {
            const t = this;
            const none = t.Type < 0;
            const host = !!t.Host;
            t.TypeSel.SetDisabled(host || !t.CanEdit, host ? _TF("The application decides the media type", "Why the type select of the media editor is disabled") : undefined);
            t.Source.SetDisabled(host, _TF("The application decides the media", "Why the source box of the media editor is disabled"));
            t.OpenB.SetDisabled(host, _TF("The application decides the media", "Why the source box of the media editor is disabled"));
            t.FileB.SetDisabled(host, _TF("The application decides the media", "Why the source box of the media editor is disabled"));
            t.LibB.E.hidden = host || !(none || t.Type === TEffect || t.Type === TImage || t.Type === TText);
            t.Source.Input.placeholder = t.Type === TText ? _TF("Type the text to show", "Placeholder of the source box of the media editor for a text media")
                : t.Type === TYouTube ? _TF("Paste a YouTube link or a video code", "Placeholder of the source box of the media editor for a YouTube media")
                    : window.matchMedia("(max-width: 760px)").matches ? _TF("Paste a link", "Short placeholder of the source box of the media editor on a phone")
                        : _TF("Paste a link or a YouTube address (Enter opens it), or drop a file", "Placeholder of the source box of the media editor");
            const local = !!t.LocalName;
            const why = none ? _TF("Open a media first", "Why a button of the media editor is disabled")
                : local ? _TF("A file of this computer can only be shown here: use its web address to share it or show it in another window", "Why a button of the media editor is disabled for a local file")
                    : null;
            t.ShareB.SetDisabled(!!why, why || undefined);
            t.UndockB.SetDisabled(!!why && !t.Undocked, why || undefined);
            t.UndockB.SetPressed(!!t.Undocked);
            t.UndockB.SetContent(t.Undocked ? _TF("Show here", "Button of the media editor: brings the popped out player back into the editor") : _TF("Pop out", "Button of the media editor: shows the player in a window of its own"), t.Undocked ? "dock" : "window");
            const es = t.EditStack();
            t.UndoB.SetDisabled(!t.UndoStack.length && !(es && es.CanUndo()), _TF("Nothing to undo", "Why the undo button of the media editor is disabled"));
            t.RedoB.SetDisabled(!t.RedoStack.length && !(es && es.CanRedo()), _TF("Nothing to redo", "Why the redo button of the media editor is disabled"));
            const n = none ? 0 : Object.keys(t.Changed()).length;
            t.ResetB.SetDisabled(!n || !t.CanEdit, _TF("Every setting has its default", "Why the reset button of the media editor is disabled"));
            //  The heading of the settings
            if (none) {
                t.SideTitle.textContent = _TF("No media yet", "Title of the settings pane of the media editor before a media is opened");
                t.SideSub.textContent = "";
            } else {
                t.SideTitle.textContent = t.LocalName || MediaName(t.Type, t.Data);
                t.SideTitle.title = t.LocalName ? t.LocalName : t.Type === TText ? t.Data : t.Data;
                t.SideSub.textContent = KindName(t.Type) + " · " + (n
                    ? _T("{0} changed", n, "Sub title of the settings pane of the media editor: how many settings differ from the defaults, {0} the count")
                    : _TF("default settings", "Sub title of the settings pane of the media editor when no setting was changed"));
                if (local)
                    t.SideSub.textContent += " · " + _TF("a file of this computer", "Sub title of the settings pane of the media editor for a local file");
            }
            if (t.SaveB) {
                const dirty = JSON.stringify(t.Props) !== t.Saved;
                t.SaveB.SetDisabled(!dirty, _TF("No changes to save yet", "Why the save button of the media editor is disabled"));
                //  Leaving with unsaved settings asks first (the shell, links, Back and the browser)
                Sw.Page.SetDirty("mediaEdit", dirty ? _TF("Your changes to the media settings are lost.", "Leave question of the media editor with unsaved settings") : false);
            }
        }

        // ---------------------------------------------------------------- open

        /**
         * Show the start state: a drop zone with examples (no media yet).
         */
        ShowEmpty() {
            const t = this;
            t.DisposePlayer();
            t.Type = -1;
            t.Stage.textContent = "";
            const ex = (text, icon, type, data, props) => new Sw.Button({ Text: text, Icon: icon, OnClick: () => t.Open(type, data, { Props: props, Undo: true }) }).E;
            const box = el("div.me-empty", null,
                el("div.me-empty-icon", { attrs: { "aria-hidden": "true" } }, Sw.Icon ? Sw.Icon("upload") : null),
                el("h2.me-empty-title", { text: _TF("Drop a media here", "Title of the start state of the media editor") }),
                el("p.me-empty-text", { text: _TF("A picture, a video, a sound, an effect (.glsl) or a link to one; or paste a YouTube link in the box above.", "Text of the start state of the media editor") }),
                el("div.me-empty-actions", null,
                    new Sw.Button({ Text: _TF("Open file…", "Button of the start state of the media editor"), Icon: "upload", Kind: "primary", OnClick: () => t.FileInput.click() }).E,
                    new Sw.Button({ Text: _TF("Paste a link", "Button of the start state of the media editor"), Icon: "link", OnClick: () => t.Source.Focus(true) }).E),
                el("p.me-empty-or", { text: _TF("Or start from an example:", "Text of the start state of the media editor before the example buttons") }),
                el("div.me-empty-examples", null,
                    ex(_TF("A picture", "Example of the media editor"), "image", TImage, "../logo.png"),
                    ex(_TF("An effect", "Example of the media editor"), "sparkle", TEffect, "backgroundEffects/SmoothBands.glsl"),
                    ex(_TF("A picture with an effect", "Example of the media editor"), "wand", TImage, "../logo.png", { Effect: "imageEffects/DistortedTv.glsl" }),
                    ex(_TF("A text", "Example of the media editor"), "text", TText, "Hello!", { Font: "bold 160px Tahoma" }),
                    ex(_TF("A text collage", "Example of the media editor"), "grid", TText, "Red,Green,Blue,Gold", { Font: "bold 80px Tahoma", CollageSeparator: ",", CollageColumns: 2, Effect: "collageEffects/Stripes.glsl" })));
            t.Stage.appendChild(box);
            t.Props_.textContent = "";
            t.Fx_.textContent = "";
            t.Props_.appendChild(el("p.me-props-note", { text: _TF("The settings of the media show here once it is open.", "Shown in the settings pane of the media editor before a media is opened") }));
            document.title = _TF("Media editor", "Title of the media editor page");
            t.UpdateChrome();
        }

        /**
         * Open the typed source: a media link (its type from the extension, a YouTube link), a text for a text media, else the
         * address as the chosen type.
         */
        OpenTyped() {
            const t = this;
            const s = (t.Source.Get() || "").trim();
            if (!s) {
                Sw.Toast(_TF("Type or paste an address first", "Warning of the media editor when Open is used with an empty box"), { Kind: "warning" });
                t.Source.Focus();
                return;
            }
            if (t.Type === TText) {
                t.Open(TText, s, { Props: t.Props, Undo: true });
                return;
            }
            const m = MV.ParseMediaLink(s);
            if (m) {
                t.Open(m.Type, m.Link, { Undo: true });
                return;
            }
            //  A YouTube code while the type is YouTube
            if (t.Type === TYouTube && /^[\w-]{6,20}$/.test(s)) {
                t.Open(TYouTube, s, { Undo: true });
                return;
            }
            //  An address without a known extension: as the chosen type (an image by default)
            if (/^(https?:\/\/|\/|\.\.?\/)/i.test(s) || /^[\w.-]+\/\S*$/.test(s)) {
                const type = t.Type >= 0 && t.Type !== TText && t.Type !== TYouTube ? t.Type : TImage;
                Sw.Toast(_T("The address has no known file type: opened as {0}. Choose the type at the left if it is something else.", KindName(type).toLowerCase(),
                    "Note of the media editor when an address without a known extension was opened, {0} the media type"), { Kind: "info" });
                t.Open(type, s, { Undo: true });
                return;
            }
            Sw.Toast(_TF("Not a media address: use a YouTube link or the address of a file (a picture, a video, a sound, .glsl), or choose the type Text for a text.", "Error of the media editor when the typed text is not a media address"), { Kind: "warning" });
        }

        /**
         * Open a file of this computer (a blob: address of this page: shown here only).
         * @param {File} f The file.
         */
        OpenFile(f) {
            const t = this;
            const name = f.name || "";
            const mt = f.type || "";
            let type = mt.startsWith("image/") ? TImage : mt.startsWith("video/") ? TVideo : mt.startsWith("audio/") ? TAudio : -1;
            if (type < 0)
                type = /\.glsl$/i.test(name) ? TEffect : MV.GetTypeFromUrl(name);
            if (type < 0 && /\.txt$/i.test(name)) {
                f.text().then(s => t.Open(TText, s.trim().substring(0, 500), { Undo: true }));
                return;
            }
            if (type < 0) {
                Sw.Toast(_T("{0} is not a media file this editor can show (a picture, a video, a sound or a .glsl effect).", name, "Error of the media editor when a dropped file has an unknown type, {0} the file name"), { Kind: "warning" });
                return;
            }
            if (t.Blob)
                URL.revokeObjectURL(t.Blob);
            t.Blob = URL.createObjectURL(f);
            t.Open(type, t.Blob, { Local: name, Undo: true });
        }

        /**
         * Open a media.
         * @param {number} type The media type.
         * @param {string} data The link / code / text.
         * @param {object} [o] Options: Props (settings on top of the defaults), Local (the file name of a blob: link), Undo (keep the
         *   current media for Undo), Keep (keep the settings as they are: host data, undo), Start (seconds).
         * @returns {Promise<void>}
         */
        async Open(type, data, o) {
            const t = this;
            o = o || {};
            if (o.Undo && t.Type >= 0)
                t.PushUndo();
            if (type === TYouTube) {
                const yt = MV.ParseYouTube(data);
                if (yt) {
                    data = yt.Id;
                    o.Start = o.Start || yt.Start;
                }
            }
            const sameMedia = type === t.Type && data === t.Data;
            t.Type = type;
            t.Data = data;
            t.LocalName = o.Local || (data && data === t.Blob ? t.LocalName : null);
            if (!o.Local && t.Blob && data !== t.Blob) {
                URL.revokeObjectURL(t.Blob);
                t.Blob = null;
            }
            t.Props = o.Keep ? (o.Props || {}) : Object.assign(Defaults(type, data), clone(o.Props) || {});
            //  A text collage is drawn by its effect at the size of the box (not of one text), as the collage pages do
            if (type === TText && t.Props.CollageSeparator && !o.Keep) {
                const given = o.Props || {};
                if (given.EffectSizeFromImage === undefined)
                    t.Props.EffectSizeFromImage = false;
                if (given.AdaptiveSize === undefined)
                    t.Props.AdaptiveSize = true;
            }
            delete t.Props.EffectProps;
            t.TypeSel.Set(type, true);
            t.Source.Set(t.LocalName ? t.LocalName : data, true);
            //  The end of a long address is its file name: show that part
            requestAnimationFrame(() => {
                if (document.activeElement !== t.Source.Input)
                    t.Source.Input.scrollLeft = t.Source.Input.scrollWidth;
            });
            document.title = (t.LocalName || MediaName(type, data)) + " - " + _TF("Media editor", "Title of the media editor page");
            if (t.Undocked && !sameMedia) {
                if (t.LocalName)
                    t.Dock();
                else
                    t.Post("Load", { Link: data, MediaType: type });
            }
            await t.CreateView(o.Start || 0);
            t.SyncUrl();
            t.UpdateChrome();
        }

        /**
         * The type select changed: the same address as another type (new defaults).
         * @param {number} type The type.
         */
        ChangeType(type) {
            const t = this;
            if (type === t.Type)
                return;
            if (t.Type < 0) {
                //  No media yet: the next opened address is of this type
                t.Type = -1;
                t.PendingType = type;
                t.UpdateChromeForType(type);
                return;
            }
            let data = t.Data;
            if (type === TText && t.Type !== TText)
                data = "Hello!";
            else if (t.Type === TText && type !== TText)
                data = "";
            if (!data) {
                t.PushUndo();
                t.DisposePlayer();
                t.Type = -1;
                t.PendingType = type;
                t.ShowEmpty();
                t.TypeSel.Set(type, true);
                t.UpdateChromeForType(type);
                t.Source.Focus();
                return;
            }
            t.Open(type, data, { Undo: true });
        }

        /**
         * Show the bar for a chosen type before a media is open (the placeholder, the effects button).
         * @param {number} type The type.
         */
        UpdateChromeForType(type) {
            const t = this;
            t.LibB.E.hidden = !(type === TEffect || type === TImage || type === TText);
            t.Source.Input.placeholder = type === TText ? _TF("Type the text to show", "Placeholder of the source box of the media editor for a text media")
                : type === TYouTube ? _TF("Paste a YouTube link or a video code", "Placeholder of the source box of the media editor for a YouTube media")
                    : _TF("Paste a link or a YouTube address (Enter opens it), or drop a file", "Placeholder of the source box of the media editor");
        }

        // ---------------------------------------------------------------- player

        /**
         * Remove the player and its view.
         */
        DisposePlayer() {
            const t = this;
            clearTimeout(t.RebuildTimer);
            const p = t.Player;
            t.Player = null;
            //  The view goes first (a YouTube iframe stops with it), then the player is stopped and disposed; not every player's
            //  OnDispose returns a promise, and a failing one must not keep the old media playing
            if (p && p.Pause)
                Promise.resolve().then(() => p.Pause()).catch(() => { });
            if (t.View)
                t.View.remove();
            t.View = null;
            if (p) {
                Promise.resolve().then(async () => {
                    if (p.OnDispose)
                        await p.OnDispose();
                }).catch(e => console.warn("Media editor: the previous player did not stop cleanly: " + (e && e.message ? e.message : e)));
            }
        }

        /**
         * Create the view and the player of the current media (a new media).
         * @param {number} start Seconds to start at.
         * @returns {Promise<void>}
         */
        async CreateView(start) {
            const t = this;
            t.DisposePlayer();
            t.Stage.textContent = "";
            const view = document.createElement("Media-View");
            t.View = view;
            //  A text gets some room around it (it is drawn edge to edge)
            view.classList.toggle("me-pad", t.Type === TText && !t.Props.CollageSeparator);
            t.Stage.appendChild(view);
            //  A media from the url: the same checks as MediaView.html (a link from a file or typed here is the user's own)
            if (t.FromUrl) {
                const problem = MV.MediaLinkProblem({ Type: t.Type, Data: t.Data, Props: t.Props });
                t.FromUrl = false;
                if (problem) {
                    MV.ShowLinkProblem(view, problem);
                    t.RenderProps();
                    return;
                }
            }
            const busy = el("div.me-loading", null, Sw.Spinner ? new Sw.Spinner().E : null, el("span", { text: _TF("Loading the media…", "Shown while the media editor loads a media") }));
            t.Stage.appendChild(busy);
            let player;
            try {
                player = (await t.NewPlayer(null))[0];
            }
            catch (e) {
                busy.remove();
                if (t.View !== view)
                    return;
                MV.ShowLoadError(view, e);
                t.RenderProps();
                t.UpdateChrome();
                return;
            }
            busy.remove();
            if (t.View !== view) {
                player.OnDispose && player.OnDispose();
                return;
            }
            t.Player = player;
            MV.CreateChrome(view, () => t.Player, {
                Stats: true,
                StartHidden: true,
                //  The name over the picture, as the viewer shows it (not the page title "... - Media editor")
                Title: () => t.LocalName || MediaName(t.Type, t.Data),
                Transparent: () => !!(t.Props && t.Props.Transparent),
                FullPage: () => Sw.Toast(_TF("This browser can't show the player in full screen here: use Pop out.", "Note of the media editor when full screen is not possible"), { Kind: "info" }),
                Info: () => ({
                    Type: t.Type,
                    Link: t.Type === TYouTube ? "https://www.youtube.com/watch?v=" + t.Data : t.Type === TText || t.LocalName ? null : new URL(t.Data, window.location.href).href,
                }),
                //  A link dropped / pasted on the player (CreateChrome); its paste listener stays with the page, so only the current view acts
                Load: m => {
                    if (t.View === view)
                        t.Open(m.Type, m.Link, { Undo: true });
                },
            });
            t.ReadFx();
            t.RenderProps();
            if (start > 0)
                await player.Seek(start);
            if (t.Undocked)
                player.Pause();
            else
                await t.StartPlayback(player);
            t.WatchDuration();
        }

        /**
         * The browser plays a media without sound until the page was clicked: a button on the player turns the sound on (the click it
         * needs), instead of a note far away from the player.
         */
        ShowSoundButton() {
            const t = this;
            const b = new Sw.Button({
                Text: _TF("Turn on the sound", "Button over the player of the media editor when the browser started the media without sound"), Icon: "IconMediaPreviewVolLoud", Small: true, Class: "me-sound",
                Title: _TF("The browser starts a media without sound until you click the page once", "Tool tip of a button of the media editor"),
                OnClick: async () => {
                    b.E.remove();
                    const p = t.Player;
                    if (p) {
                        await p.UnMute();
                        if (p.Paused)
                            await p.Play();
                    }
                },
            });
            t.Stage.appendChild(b.E);
        }

        /**
         * Video, audio and YouTube know their length only after a while (the first play): then the time settings get it as their range.
         */
        WatchDuration() {
            const t = this;
            if (t.Type !== TVideo && t.Type !== TAudio && t.Type !== TYouTube)
                return;
            clearInterval(t.DurTimer);
            let n = 0;
            t.DurTimer = setInterval(() => {
                const d = t.Player ? t.Player.Duration : 0;
                if (d > 0 && isFinite(d) && Math.abs(d - (t.RenderedDuration || 0)) > 0.05) {
                    clearInterval(t.DurTimer);
                    t.RenderProps();
                }
                else if (++n > 40)
                    clearInterval(t.DurTimer);
            }, 250);
        }

        /**
         * Start a new media with the viewer preferences (loop, volume, mute). Unlike the viewer (PlayWithPopup) the editor never covers
         * the page when the browser wants a click before sound: it plays muted and says how to get the sound.
         * @param {object} p The player.
         * @returns {Promise<void>}
         */
        async StartPlayback(p) {
            const P = MV.Prefs;
            if (P.Loop())
                await p.Loop();
            else
                await p.Once();
            p.SetVolume(P.Volume());
            if (p.HaveAudio && !P.Muted()) {
                if (await MediaPlayerTools.CanUnMute())
                    await p.UnMute();
                else
                    this.ShowSoundButton();
            }
            if (p.CanPlay)
                await p.Play();
            p.SetButtonState();
        }

        /**
         * Create a player of the current media and settings.
         * @param {?HTMLElement} before Build it before this element (a rebuild), or null.
         * @returns {Promise<Array>} The CreatePlayer result.
         */
        async NewPlayer(before) {
            const t = this;
            const pd = await MV.CreatePlayer(t.View, t.Type, t.Data, t.PlayerProps(), true, before, false, false, 0, 0, 0.5, 0.5, true);
            if (!pd)
                throw new Error(_TF("Failed to cache the media", "Error when a media file could not be loaded"));
            const p = pd[0];
            p.UseClip = !(t.Props.Crop && t.Props.Crop.VisualizeMargins);
            if (!p.UseClip) {
                p.ApplyClip(t.Props);
                p.UpdateSize();
            }
            return pd;
        }

        /**
         * The settings for a new player (a copy; the players keep their own).
         * @returns {object} The props.
         */
        PlayerProps() {
            const p = clone(this.Props);
            delete p.EffectProps;
            return p;
        }

        /**
         * Rebuild the player with the current settings, soon (changes that a player can't take on the fly).
         */
        ScheduleRebuild() {
            const t = this;
            clearTimeout(t.RebuildTimer);
            t.RebuildTimer = setTimeout(() => t.Rebuild(), RebuildMs);
        }

        /**
         * Rebuild the player in place (keeping the position, volume, loop and play state); a failure keeps the old player.
         * @returns {Promise<void>}
         */
        Rebuild() {
            const t = this;
            clearTimeout(t.RebuildTimer);
            t.Building = t.Building.then(async () => {
                const old = t.Player;
                if (!old || !t.View) {
                    if (t.Type >= 0)
                        await t.CreateView(0);
                    return;
                }
                const view = t.View;
                try {
                    const np = await MV.RebuildPlayer(old, before => t.NewPlayer(before));
                    if (t.View !== view) {
                        np.OnDispose && np.OnDispose();
                        return;
                    }
                    t.Player = np;
                    t.UpdateCropFrame();
                    if (t.Undocked)
                        np.Pause();
                    if (t.ReadFx())
                        t.RenderFx();
                }
                catch (e) {
                    //  RebuildPlayer leaves the old player when the new one fails
                    view.querySelectorAll("Media-Preview.Replacing").forEach(x => x.remove());
                    Sw.Toast(_T("This setting can't be shown: {0}", MV.FailReason(e), "Error of the media editor when the player could not be rebuilt with the changed settings, {0} the reason"), { Kind: "error" });
                }
            });
            return t.Building;
        }

        // ---------------------------------------------------------------- settings

        /**
         * The context of the settings (Schema).
         * @returns {object} The context.
         */
        Ctx() {
            const t = this;
            return {
                Duration: t.Player ? t.Player.Duration : 0,
                Data: t.Data,
                Props: t.Props,
                Pos: t.Player ? () => t.Player ? t.Player.GetPos() : 0 : null,
                SetFromPos: key => {
                    const v = Math.round((t.Player ? t.Player.GetPos() : 0) * 10) / 10;
                    t.RefreshValues();
                    t.Change(key, v, true, true);
                },
                EffectOptions: kind => t.EffectOptions(kind),
                TextureMenu: () => [{ Text: _TF("The site logo", "Item of the texture menu of the media editor"), Icon: "image", OnClick: () => t.Change("Texture", "../logo.png", true, true) },
                { Text: _TF("The demo collage", "Item of the texture menu of the media editor"), Icon: "grid", OnClick: () => t.Change("Texture", "DemoCollage.svg", true, true) },
                { Text: _TF("No picture", "Item of the texture menu of the media editor"), Icon: "close", Disabled: !t.Props.Texture, OnClick: () => t.Change("Texture", "", true, true) }],
            };
        }

        /**
         * The editor values of the settings (Get of a definition, else the path, else the default).
         * @returns {object} Values by key.
         */
        EdValues() {
            const t = this;
            const v = {};
            const fx = t.FxValues();
            for (const d of t.Defs || []) {
                const x = d.Fx ? FxToEd(d, fx[d.Fx]) : d.Get ? d.Get(t.Props) : getPath(t.Props, d.Key);
                v[d.Key] = x === undefined || x === null ? d.Default : x;
            }
            return v;
        }

        /**
         * Show the settings of the current media (a new media, a new type, another effect, undo): one property editor, so every
         * row lines up; an effect's own variables come first (they are what makes the effect), an overlay effect's at the end.
         */
        RenderProps() {
            const t = this;
            const seq = t.RenderSeq = (t.RenderSeq || 0) + 1;
            if (t.Type < 0) {
                t.Props_.textContent = "";
                t.Fx_.textContent = "";
                return;
            }
            const fx = FxDefs(t.FxMembers);
            const ctx = t.Ctx();
            t.RenderedDuration = ctx.Duration;
            const base = Schema(t.Type, ctx);
            t.Defs = t.Type === TEffect ? fx.concat(base) : base.concat(fx);
            t.DefMap = new Map(t.Defs.map(d => [d.Key, d]));
            const keep = t.Side.querySelector(".me-side-scroll");
            const top = keep ? keep.scrollTop : 0;
            const done = () => {
                t.ApplyVisibility();
                if (keep)
                    keep.scrollTop = top;
            };
            //  The object editor of the Edit service (the editor of the api pages: copy / paste, drag and drop, undo per member), else
            //  the plain property grid
            (t.HaveEdit === undefined ? MV.LoadEditor().then(ok => t.HaveEdit = ok) : Promise.resolve(t.HaveEdit)).then(async ok => {
                if (seq !== t.RenderSeq)
                    return;
                if (ok)
                    await t.RenderEdit();
                else
                    t.RenderGrid();
                if (seq === t.RenderSeq)
                    done();
            });
        }

        /**
         * The settings in a Sw.PropertyEditor (when the Edit service is missing).
         */
        RenderGrid() {
            const t = this;
            t.EdKind = "grid";
            const ro = !t.CanEdit;
            t.Ed = new Sw.PropertyEditor({
                Defs: t.Defs.map(d => EdDef(d, ro)),
                Values: t.EdValues(),
                OnInput: (k, v) => t.Change(k, v, false),
                OnChange: (k, v) => t.Change(k, v, true),
            });
            t.Props_.replaceChildren(t.Ed.E);
            t.Fx_.textContent = "";
        }

        /**
         * The settings in the object editor of the Edit service (Sw.Edit): the definitions become the members of a client type (labels,
         * groups, units, hints, sliders, enums, css / HDR colors, show rules, row menus), the editor edits a view object of their
         * values; every change (typed, a slider, a member's Paste / Set default / drop, Sw.Edit's undo) is read back (SyncEdit) and
         * applied like a change of the grid (Change: live or a rebuild).
         * @returns {Promise<void>}
         */
        async RenderEdit() {
            const t = this;
            t.EdKind = "edit";
            const E = Sw.Edit;
            const Fl = E.Flags;
            const members = [];
            const map = new Map();
            const vals = t.EdValues();
            const vm = {};
            const seq = ++EditTypeSeq;
            const name = k => "m_" + k.replace(/[^A-Za-z0-9]/g, "_");
            const ro = !t.CanEdit;
            for (const d of t.Defs) {
                const base = {
                    DisplayName: d.Label || d.Key, Summary: d.Desc || null, Group: d.Group || null, Flags: Fl.IsPrimitive | (ro ? Fl.ReadOnly : 0),
                };
                if (d.Hint !== undefined)
                    base.Hint = d.Hint;
                if (d.Show)
                    base.HideIf = () => !d.Show(t.Props);
                if (d.Menu)
                    base.MenuItems = () => typeof d.Menu === "function" ? d.Menu(d) : d.Menu;
                const add = (n, m, conv) => {
                    const mem = Object.assign({}, base, m, { Name: n });
                    if (m.Flags)
                        mem.Flags = base.Flags | m.Flags;
                    members.push(mem);
                    map.set(n, { Def: d, Conv: conv });
                    vm[n] = conv.To(vals[d.Key]);
                };
                const S = d.Scale && d.Scale !== 1 ? d.Scale : 1;
                const num = (m, comp) => {
                    const slider = !d.NoSlider && d.Min !== undefined && d.Max !== undefined;
                    const isInt = d.Type === "int" && S === 1;
                    const step = d.Step !== undefined ? d.Step * S : undefined;
                    return Object.assign({
                        TypeName: isInt ? "System.Int32" : "System.Double", Flags: slider ? Fl.Slider : 0,
                        Min: d.Min !== undefined ? String(+(d.Min * S).toFixed(6)) : null, Max: d.Max !== undefined ? String(+(d.Max * S).toFixed(6)) : null,
                        EditParams: step !== undefined ? String(+step.toFixed(6)) : null, Unit: d.Unit || null,
                        Default: String(+((comp === undefined ? d.Default : (Array.isArray(d.Default) ? d.Default[comp] : 0)) * S).toFixed(6)),
                    }, m);
                };
                switch (d.Type) {
                    case "float":
                    case "int":
                        add(name(d.Key), num({}), { To: v => typeof v === "number" ? +(v * S).toFixed(6) : v, From: v => typeof v === "number" ? v / S : v });
                        break;
                    case "bool":
                        add(name(d.Key), { TypeName: "System.Boolean", Default: d.Default ? "true" : "false" }, { To: v => !!v, From: v => !!v });
                        break;
                    case "enum": {
                        const opts = d.Options || [];
                        const clean = s => String(s ?? "").replace(/[<>|]/g, " ");
                        const di = Math.max(0, opts.findIndex(o => o.Value === d.Default));
                        add(name(d.Key), {
                            TypeName: "SysWeaver.MediaEdit.Enum" + seq + "_" + d.Key.replace(/[^A-Za-z0-9]/g, ""), Flags: Fl.IsEnum,
                            Default: di + ">" + opts.map((o, i) => i + "<" + clean(o.Label) + (o.Hint ? "<" + clean(o.Hint) : "")).join("|"),
                        }, { To: v => Math.max(0, opts.findIndex(o => o.Value === v)), From: i => (opts[i] || opts[0] || {}).Value });
                        break;
                    }
                    case "string":
                        add(name(d.Key), { TypeName: "System.String", Flags: d.Multiline ? Fl.Multiline : 0, Default: d.Default || "" }, { To: v => v ?? "", From: v => v ?? "" });
                        break;
                    case "color":
                    case "color4":
                        if (d.Color)    //  a css color of a text style: the editor keeps the css text (with alpha)
                            add(name(d.Key), { TypeName: "System.String", Type: "Color:Css:Alpha", Default: colorToCss(d.Default) }, { To: v => colorToCss(v || [0, 0, 0, 0]), From: v => cssToColor(v) });
                        else if ((d.Default || []).slice(0, 3).every(x => x <= 1)) //  an effect color in 0..1: as hex, like the text colors
                            add(name(d.Key), { TypeName: "System.String", Type: d.Type === "color4" ? "Color:Hex:Alpha" : "Color:Hex", Default: colorToCss(d.Default || [1, 1, 1]) }, {
                                To: v => colorToCss(v || [1, 1, 1]), From: v => { const c = cssToColor(v); return d.Type === "color4" ? c : c.slice(0, 3); },
                            });
                        else            //  an HDR effect color (above 1): 0..1 floats with an intensity
                            add(name(d.Key), { TypeName: "System.Single[]", Type: d.Type === "color4" ? "Color:HdrRgba::16" : "Color:HdrRgb::16", Default: JSON.stringify(d.Default || []) }, { To: v => (v || []).slice(), From: v => (v || []).slice() });
                        break;
                    case "vec2":
                    case "vec3":
                    case "vec4": {
                        //  A vector: one number per component ("Bg color x"), each with the vector's range
                        const n = +d.Type[3];
                        const names = d.Names || ["x", "y", "z", "w"];
                        for (let i = 0; i < n; ++i)
                            add(name(d.Key) + "_" + i, num({ DisplayName: (d.Label || d.Key) + " " + names[i] }, i), {
                                Comp: i, To: v => Array.isArray(v) ? v[i] ?? 0 : 0, From: v => v,
                            });
                        break;
                    }
                }
            }
            const type = { TypeName: "SysWeaver.MediaEdit.Settings" + seq, DisplayName: _TF("Media settings", "Name of the settings object of the media editor"), Flags: Fl.IsObject, Members: members, Asm: "mediaEdit.js" };
            E.AddType(type);
            const options = E.Options.CleanMenu.Clone();
            options.PlainHints = true;
            options.MarkChanged = true;
            options.TypeHints = true;
            options.ReadOnly = ro;
            options.UndoStack = new E.UndoStack();
            //  The side by side layout (name | value) in the settings pane, the stacked one on phones (the pane is narrower than the
            //  editor's own switch width)
            const ed = new E(type, window.matchMedia("(max-width: 760px)").matches ? "Mobile" : "Desktop", options);
            t.EditMap = map;
            t.VM = vm;
            t.VMLast = {};
            for (const k of Object.keys(vm))
                t.VMLast[k] = JSON.stringify(vm[k]);
            await ed.SetObject(vm);
            t.Ed = ed;
            ed.Element.classList.add("me-edit");
            ed.Element.addEventListener("EditChange", () => t.SyncEdit());
            options.UndoStack.OnChange(() => {
                t.SyncEdit();
                t.UpdateChrome();
            });
            //  How to reach the member actions (the framework editor's menu, copy / paste, drag and drop)
            const touch = window.matchMedia("(hover: none)").matches;
            /* SwWeb:allow ux-tip-7 (one tip above the whole settings editor, not a text per row) */
            const tipTouch = _TF("Long press a setting's name to reset, copy or paste it.", "Tip above the settings of the media editor on touch screens");
            /* SwWeb:allow ux-tip-7 (one tip above the whole settings editor, not a text per row) */
            const tipMouse = _TF("Right click a setting to reset, copy or paste it; drag its name onto another setting to copy the value.", "Tip above the settings of the media editor");
            //  Shown until dismissed (remembered)
            let tip = null;
            if (!ro && Sw.Storage.Get(TipKey) !== "hidden") {
                const close = new Sw.Button({
                    Icon: "close", Kind: "ghost", Small: true, Title: _TF("Hide this tip", "Tool tip of the button that hides the tip above the settings of the media editor"),
                    OnClick: () => {
                        Sw.Storage.Set(TipKey, "hidden");
                        tip.remove();
                    },
                });
                tip = el("div.me-edit-tip", null, el("span", { text: touch ? tipTouch : tipMouse }), close.E);
            }
            t.Props_.replaceChildren(...[tip, ed.Element].filter(x => x));
            t.Fx_.textContent = "";
        }

        /**
         * Read the view object of the object editor back: every member that changed is applied (Change), so a typed value, a slider, a
         * member's Paste / Set default / drop and Sw.Edit's own undo / redo all reach the player.
         */
        SyncEdit() {
            const t = this;
            if (t.EdKind !== "edit" || !t.VM)
                return;
            const vm = t.Ed.Object || t.VM;
            for (const [n, m] of t.EditMap) {
                const j = JSON.stringify(vm[n]);
                if (j === t.VMLast[n])
                    continue;
                t.VMLast[n] = j;
                const d = m.Def;
                let v = m.Conv.From(vm[n]);
                if (m.Conv.Comp !== undefined) {
                    const cur = (t.EdValues()[d.Key] || []).slice();
                    cur[m.Conv.Comp] = v;
                    v = cur;
                }
                t.Change(d.Key, v, true);
            }
        }

        /**
         * The settings were changed by the editor itself (a menu, undo, reset, paste): show the new values.
         */
        RefreshValues() {
            const t = this;
            if (t.EdKind === "grid" && t.Ed)
                t.Ed.SetValues(t.EdValues());
            else
                t.RenderProps();
        }

        /** The effect variables changed (another effect): the settings again. */
        RenderFx() {
            this.RenderProps();
        }

        /**
         * Show / hide the settings that depend on others (Show of the definitions), and the crop note of an effect.
         */
        ApplyVisibility() {
            const t = this;
            const cropNote = t.DefMap && t.DefMap.has("$Crop") && t.DefMap.has("Effect") && t.Props.Effect && t.Props.Crop
                ? _TF("Some effects draw the whole picture. The dashed frame on the player shows what the crop keeps.", "Help under the crop setting of the media editor when an effect is used") : null;
            if (t.EdKind === "grid" && t.Ed) {
                for (const d of t.Defs || [])
                    if (d.Show)
                        t.Ed.SetVisible(d.Key, !!d.Show(t.Props));
                if (t.DefMap && t.DefMap.has("$Crop"))
                    t.Ed.SetHelp("$Crop", cropNote);
            }
            else if (t.EdKind === "edit" && t.Ed && t.Ed.MemberMap) {
                for (const [n, item] of t.Ed.MemberMap) {
                    const m = t.EditMap.get(n);
                    if (m && m.Def.Show && item.Hide)
                        m.Def.Show(t.Props) ? item.Show() : item.Hide();
                }
                t.Ed.UpdateGroups && t.Ed.UpdateGroups();
                //  Help lines under rows (they wrap, unlike the hint line under a name): a definition's Help, the crop notes
                const note = (n, text) => {
                    const item = t.Ed.MemberMap.get(n);
                    if (!item || !item.Row)
                        return;
                    let h = item.Row.nextElementSibling && item.Row.nextElementSibling.classList.contains("me-edit-help") ? item.Row.nextElementSibling : null;
                    if (text && !h)
                        item.Row.after(h = el("div.me-edit-help", { attrs: { role: "note" } }));
                    if (h) {
                        h.textContent = text || "";
                        h.hidden = !text || item.Row.classList.contains("sw-hide");
                    }
                };
                for (const d of t.Defs || [])
                    if (d.Help)
                        note("m_" + d.Key.replace(/[^A-Za-z0-9]/g, "_"), d.Help);
                note("m__Crop", cropNote || (t.Type === TYouTube && t.Props.Crop ? _TF("The YouTube player is square: this crop (the default) shows its 16:9 picture.", "Help under the crop setting of the media editor for a YouTube video") : null));
            }
            t.UpdateCropFrame();
        }

        /**
         * Read the effect variables of the player (an effect, or an image / text with an effect).
         * @returns {boolean} True when they are another set than before (the variables editor must be built again).
         */
        ReadFx() {
            const t = this;
            const p = t.Player;
            const info = p && p.ScriptTypeInfo;
            const sig = info ? info.TypeName + "|" + info.Default : "";
            if (sig === t.FxSig)
                return false;
            t.FxSig = sig;
            t.FxMembers = info ? info.Members : null;
            try {
                t.FxDefaults = info && info.Default ? JSON.parse(info.Default) : null;
            }
            catch {
                t.FxDefaults = null;
            }
            return true;
        }

        /**
         * The object that holds the effect settings: the props of an effect, the EffectParams of an image or a text.
         * @param {boolean} [create] Create EffectParams when missing.
         * @returns {?object} The object.
         */
        FxTarget(create) {
            const t = this;
            if (t.Type === TEffect)
                return t.Props;
            if (!t.Props.EffectParams && create)
                t.Props.EffectParams = {};
            return t.Props.EffectParams || null;
        }

        /**
         * The current effect variable values (the saved FxProps over the effect's defaults).
         * @returns {object} Values by variable name.
         */
        FxValues() {
            const t = this;
            const tg = t.FxTarget(false);
            let cur = null;
            try {
                cur = tg && tg.FxProps ? (typeof tg.FxProps === "string" ? JSON.parse(tg.FxProps) : tg.FxProps) : null;
            }
            catch {
                cur = null;
            }
            return Object.assign({}, t.FxDefaults || {}, cur || {});
        }

        /**
         * An effect variable changed: recompile the effect (no rebuild) and keep the value in FxProps.
         * @param {object} d The definition.
         * @param {*} v The editor value.
         * @param {boolean} final Committed.
         */
        ChangeFx(d, v, final) {
            const t = this;
            if (!d)
                return;
            if (final && t.EdKind !== "edit")
                t.PushUndo(true, "fx:" + d.Fx);
            const vals = t.FxValues();
            vals[d.Fx] = FxFromEd(d, v);
            const tg = t.FxTarget(true);
            //  A collage writes its grid into an object (MediaPlayerTextTexture), the others keep a JSON text (the C# FxProps)
            tg.FxProps = t.Type === TText && t.Props.CollageSeparator ? vals : JSON.stringify(vals);
            const p = t.Player;
            if (p && p.Params) {
                p.Params.EffectProps = Object.assign(p.Params.EffectProps || {}, { [d.Fx]: vals[d.Fx] });
                try {
                    p.LazyCompile();
                }
                catch (e) {
                    console.warn("Media editor: the effect could not be compiled: " + e);
                }
                t.Post("Compile", { EffectProps: p.Params.EffectProps });
            }
            if (final)
                t.Committed();
        }

        /**
         * A setting changed.
         * @param {string} key The definition key.
         * @param {*} v The editor value.
         * @param {boolean} final Committed (else while dragging).
         * @param {boolean} [fromCode] Set by the editor itself (a menu): update the property editor too.
         */
        Change(key, v, final, fromCode) {
            const t = this;
            const d = t.DefMap && t.DefMap.get(key);
            if (!d)
                return;
            if (d.Fx) {
                t.ChangeFx(d, v, final);
                return;
            }
            //  The object editor has its own undo of the settings (the bar's Undo uses it first)
            if (final && t.EdKind !== "edit")
                t.PushUndo(true, key);
            if (d.Set)
                d.Set(t.Props, v);
            else
                setPath(t.Props, key, v);
            //  A collage is drawn at the size of the box, one text at its own size
            if (key === "CollageSeparator" && final) {
                const on = !!t.Props.CollageSeparator;
                t.Props.EffectSizeFromImage = !on;
                t.Props.AdaptiveSize = on;
            }
            if (fromCode)
                t.RefreshValues();
            t.ApplyVisibility();
            if (d.Live)
                t.ApplyLive(d.Live);
            else if (final) {
                t.ScheduleRebuild();
                t.Post("Reload", t.PlayerProps());
            }
            if (final) {
                //  A new effect (another file) has other variables
                if (key === "Effect" || key === "Texture")
                    t.FxSig = null;
                //  The effect choices of a collage differ (and the size settings changed above)
                if (key === "CollageSeparator")
                    t.RenderProps();
                t.Committed();
            }
        }

        /**
         * Apply a setting to the player without a rebuild (and to a popped out player).
         * @param {string} what Volume, Speed, Offset, Crop or AdaptiveSize.
         */
        ApplyLive(what) {
            const t = this;
            const p = t.Player;
            const pr = t.Props;
            switch (what) {
                case "Volume":
                    if (p) {
                        p.Params.Volume = pr.Volume;
                        p.ResetVolume();
                    }
                    t.Post("SetVolume", { Volume: pr.Volume });
                    break;
                case "Speed":
                case "Offset": {
                    const tg = t.FxTarget(false) || {};
                    const v = tg[what] ?? EffectDefaults[what];
                    if (p)
                        p.Params[what] = v;
                    t.Post("SetProps", { [what]: v });
                    break;
                }
                case "Crop":
                    if (p) {
                        p.UseClip = !(pr.Crop && pr.Crop.VisualizeMargins);
                        p.ApplyClip(pr);
                        p.UpdateSize();
                    }
                    t.Post("Crop", { Crop: pr.Crop });
                    break;
                case "AdaptiveSize":
                    if (p) {
                        p.Params.AdaptiveSize = pr.AdaptiveSize;
                        p.UpdateSize();
                    }
                    t.Post("UpdateSize", { AdaptiveSize: pr.AdaptiveSize });
                    break;
            }
        }

        /**
         * A picture / text with an effect and a crop: the effect draws the picture, and not every effect crops it, so a dashed frame
         * over the player shows what the crop keeps (the rest dimmed), whatever the effect does (editor only).
         */
        UpdateCropFrame() {
            const t = this;
            const p = t.Player;
            const c = t.Props.Crop;
            if (!(p && p.Element && c && t.Props.Effect && t.View && t.View.isConnected && (t.Type === TImage || t.Type === TText))) {
                if (t.CropFrame)
                    t.CropFrame.remove();
                t.CropFrame = null;
                return;
            }
            let f = t.CropFrame;
            if (!f) {
                f = t.CropFrame = el("div.me-cropframe", { attrs: { "aria-hidden": "true" } }, el("div.me-cropframe-keep"));
                if (!t.CropObserver) {
                    t.CropObserver = new ResizeObserver(() => t.UpdateCropFrame());
                    t.CropObserver.observe(t.Stage);
                }
            }
            if (f.parentElement !== t.View)
                t.View.appendChild(f);
            const r = p.Element.getBoundingClientRect();
            const s = t.View.getBoundingClientRect();
            //  Margins in pixels of the picture (MediaCrop): relative to the texture's size
            const tx = p.Texture;
            const tw = (tx && tx.Width) || r.width;
            const th = (tx && tx.Height) || r.height;
            const m = c.MarginAsPercentage
                ? [c.MarginTopP, c.MarginRightP, c.MarginBottomP, c.MarginLeftP].map(x => (x || 0) / 100)
                : [(c.MarginTop || 0) / th, (c.MarginRight || 0) / tw, (c.MarginBottom || 0) / th, (c.MarginLeft || 0) / tw];
            Object.assign(f.style, { left: (r.left - s.left) + "px", top: (r.top - s.top) + "px", width: r.width + "px", height: r.height + "px" });
            Object.assign(f.firstChild.style, {
                top: m[0] * r.height + "px", right: m[1] * r.width + "px", bottom: m[2] * r.height + "px", left: m[3] * r.width + "px",
            });
        }

        /**
         * After a committed change: the address, the counts and buttons.
         */
        Committed() {
            const t = this;
            t.SyncUrl();
            t.UpdateChrome();
        }

        /**
         * All settings back to their defaults.
         */
        ResetAll() {
            const t = this;
            t.PushUndo();
            const keep = t.Props.$type;
            t.Props = Defaults(t.Type, t.Data);
            if (keep)
                t.Props.$type = keep;
            t.FxSig = null;
            t.RefreshValues();
            t.ApplyVisibility();
            t.Rebuild().then(() => {
                t.ReadFx();
                t.RenderFx();
            });
            t.Post("Reload", t.PlayerProps());
            t.Committed();
            Sw.Toast(_TF("All settings are back to their defaults (Undo brings them back)", "Note of the media editor after Reset all"), { NoCopy: true, Duration: 3000 });
        }

        // ---------------------------------------------------------------- undo

        /**
         * Keep the current state for Undo.
         * @param {boolean} [coalesce] Join with the last snapshot when the same setting changes again within a second.
         * @param {string} [key] The setting.
         */
        PushUndo(coalesce, key) {
            const t = this;
            if (t.Type < 0)
                return;
            const now = Date.now();
            if (coalesce && key && t.LastUndoKey === key && now - t.LastUndoAt < 1000) {
                t.LastUndoAt = now;
                return;
            }
            t.LastUndoKey = key || null;
            t.LastUndoAt = now;
            t.UndoStack.push(JSON.stringify({ Type: t.Type, Data: t.Data, Props: t.Props, Local: t.LocalName }));
            if (t.UndoStack.length > 100)
                t.UndoStack.shift();
            t.RedoStack.length = 0;
        }

        /**
         * Go to a snapshot.
         * @param {string} json The snapshot.
         * @returns {Promise<void>}
         */
        async Restore(json) {
            const t = this;
            const s = JSON.parse(json);
            t.LastUndoKey = null;
            if (s.Type !== t.Type || s.Data !== t.Data) {
                if (s.Local && s.Data !== t.Blob) {
                    Sw.Toast(_TF("The file of this computer is no longer open: open it again", "Note of the media editor when undo would go back to a closed local file"), { Kind: "warning" });
                    return;
                }
                await t.Open(s.Type, s.Data, { Props: s.Props, Keep: true, Local: s.Local || undefined });
                return;
            }
            t.Props = s.Props;
            t.FxSig = null;
            t.RefreshValues();
            t.ApplyVisibility();
            t.Post("Reload", t.PlayerProps());
            await t.Rebuild();
            t.ReadFx();
            t.RenderFx();
            t.Committed();
        }

        /** @returns {?object} The undo stack of the object editor of the settings (null for the plain grid). */
        EditStack() {
            const t = this;
            return t.EdKind === "edit" && t.Ed && t.Ed.UndoStack ? t.Ed.UndoStack : null;
        }

        /** Undo the last change. @returns {Promise<void>} */
        async Undo() {
            const t = this;
            //  The settings: the object editor's own steps first, then the media steps (open, type, reset, paste)
            const es = t.EditStack();
            if (es && es.CanUndo()) {
                await es.Undo();
                t.UpdateChrome();
                return;
            }
            if (!t.UndoStack.length)
                return;
            t.RedoStack.push(JSON.stringify({ Type: t.Type, Data: t.Data, Props: t.Props, Local: t.LocalName }));
            await t.Restore(t.UndoStack.pop());
            t.UpdateChrome();
        }

        /** Redo the last undone change. @returns {Promise<void>} */
        async Redo() {
            const t = this;
            const es = t.EditStack();
            if (es && es.CanRedo()) {
                await es.Redo();
                t.UpdateChrome();
                return;
            }
            if (!t.RedoStack.length)
                return;
            t.UndoStack.push(JSON.stringify({ Type: t.Type, Data: t.Data, Props: t.Props, Local: t.LocalName }));
            await t.Restore(t.RedoStack.pop());
            t.UpdateChrome();
        }

        // ---------------------------------------------------------------- sharing

        /**
         * The settings that differ from the defaults (what a link needs), without the editor only ones.
         * @returns {object} The settings.
         */
        Changed() {
            const t = this;
            const d = Defaults(t.Type, t.Data);
            const out = {};
            const fxDiff = (fx, defs) => {
                let v = fx;
                try {
                    v = typeof fx === "string" ? JSON.parse(fx) : fx;
                }
                catch {
                    return fx;
                }
                if (!v || typeof v !== "object")
                    return null;
                const o = {};
                for (const k of Object.keys(v))
                    if (!defs || JSON.stringify(v[k]) !== JSON.stringify(defs[k]))
                        o[k] = v[k];
                return Object.keys(o).length ? (typeof fx === "string" ? JSON.stringify(o) : o) : null;
            };
            for (const k of Object.keys(t.Props)) {
                if (k === "$type" || k === "EffectProps")
                    continue;
                let v = clone(t.Props[k]);
                if (k === "Crop" && v)
                    delete v.VisualizeMargins;
                if (k === "FxProps")
                    v = fxDiff(v, t.FxDefaults);
                if (k === "EffectParams" && v) {
                    delete v.EffectProps;
                    if (v.FxProps)
                        v.FxProps = fxDiff(v.FxProps, t.FxDefaults);
                    for (const ek of Object.keys(v))
                        if (JSON.stringify(v[ek] ?? null) === JSON.stringify(EffectDefaults[ek] ?? null))
                            delete v[ek];
                    if (!Object.keys(v).length)
                        v = null;
                }
                if (JSON.stringify(v ?? null) !== JSON.stringify(d[k] ?? null))
                    out[k] = v;
            }
            return out;
        }

        /**
         * A page address of the current media.
         * @param {string} page "MediaView.html", "MediaPreview.html" or "MediaEdit.html".
         * @param {object} [o] Options: NoControls, NoStats, Muted, Loop, Pos (seconds), Ctrl (message prefix), Absolute, Fill (MediaPreview).
         * @returns {string} The address (relative to this page unless Absolute).
         */
        PageUrl(page, o) {
            const t = this;
            o = o || {};
            const q = new URLSearchParams();
            q.set("link", t.Data);
            q.set("type", String(t.Type));
            const ch = t.Changed();
            if (Object.keys(ch).length)
                q.set("props", JSON.stringify(ch));
            if (page === "MediaPreview.html") {
                q.set("pos", String(o.Pos || 0));
                if (o.Fill)
                    q.set("fill", "");
            }
            else if (o.Pos > 0)
                q.set("pos", String(Math.round(o.Pos * 10) / 10));
            if (o.NoControls)
                q.set("no_controls", "");
            if (o.NoStats)
                q.set("no_stats", "");
            if (o.Muted)
                q.set("muted", "");
            if (o.Loop)
                q.set("loop", "");
            if (o.Ctrl)
                q.set("ctrl", o.Ctrl);
            const s = page + "?" + q.toString().replace(/=(?=&|$)/g, "");
            return o.Absolute ? new URL(s, window.location.href).href : s;
        }

        /**
         * Keep the media and its settings in the address of this page (link mode), so a reload or a bookmark opens them again.
         */
        SyncUrl() {
            const t = this;
            if (t.Host || t.Type < 0 || t.LocalName)
                return;
            try {
                const u = new URL(t.PageUrl("MediaEdit.html"), window.location.href);
                history.replaceState(history.state, "", u.pathname + u.search);
            }
            catch {
            }
        }

        /**
         * The Share dialog: a link to the player page, to the bare media, or an embed code, with only the changed settings.
         * @returns {Promise<*>} The dialog.
         */
        Share() {
            const t = this;
            const pos = t.Player ? t.Player.GetPos() : 0;
            const visual = t.Type !== TAudio;
            const state = { Kind: "view", Controls: true, Stats: false, Muted: false, Loop: false, AtPos: false, Absolute: true, Width: "100%" };
            const out = new Sw.TextBox({ ReadOnly: true, Mono: true, Multiline: true, Rows: 4, AriaLabel: _TF("The link", "Accessible name of the result box of the share dialog of the media editor"), Copy: _TF("link", "What the copy button of the share dialog copies") });
            out.E.classList.add("me-share-out");
            const note = el("p.me-share-note");
            const kind = new Sw.Segmented({
                Items: [
                    { Value: "view", Text: _TF("Player page", "Choice of the share dialog of the media editor: a link to MediaView.html"), Icon: "play" },
                    { Value: "bare", Text: _TF("Media only", "Choice of the share dialog of the media editor: a link to MediaPreview.html (no controls)"), Icon: "image" },
                    { Value: "embed", Text: _TF("Embed code", "Choice of the share dialog of the media editor: an iframe html snippet"), Icon: "code" },
                ],
                Value: state.Kind, Label: _TF("What to share", "Accessible name of a choice of the share dialog of the media editor"),
                OnChange: v => {
                    state.Kind = v;
                    update();
                },
            });
            const toggles = {};
            const tg = (label, key, title) => {
                const x = new Sw.Toggle({ Label: label, Title: title, Value: state[key], OnChange: v => { state[key] = v; update(); } });
                toggles[key] = x;
                return x;
            };
            const muted = tg(_TF("Start muted", "Option of the share dialog of the media editor"), "Muted", _TF("Starts without sound (browsers play a muted media at once, ex: a background video)", "Tool tip of an option of the share dialog of the media editor"));
            const loop = tg(_TF("Loop", "Option of the share dialog of the media editor"), "Loop", _TF("Starts again at the end", "Tool tip of an option of the share dialog of the media editor"));
            //  A page background: no controls, no sound, looping (the options it sets are hidden while it is on)
            state.Background = false;
            const bg = new Sw.Toggle({
                Label: _TF("Page background (no controls, muted, looping)", "Option of the share dialog of the media editor"), Value: false,
                Title: _TF("For an animated or video background behind the content of a page: browsers play a muted media at once", "Tool tip of an option of the share dialog of the media editor"),
                OnChange: v => {
                    state.Background = v;
                    update();
                },
            });
            /** @returns {object} The choices as they go into the link (the background sets its options). */
            const eff = () => state.Background ? Object.assign({}, state, { Controls: false, Stats: false, Muted: true, Loop: true }) : state;
            const width = new Sw.TextBox({
                Label: _TF("Width (ex: 100% or 640px)", "Field of the share dialog of the media editor: the width of the embedded media; the height follows the media's shape"), Value: state.Width, Width: "10rem", Dirty: false,
                OnInput: v => { state.Width = v; update(); },
            });
            const controls = tg(_TF("Player controls", "Option of the share dialog of the media editor"), "Controls", _TF("Play, pause, the time line, volume and full screen on top of the media", "Tool tip of an option of the share dialog of the media editor"));
            const stats = tg(_TF("Statistics button", "Option of the share dialog of the media editor"), "Stats", _TF("A button that shows the size, time and speed of the media", "Tool tip of an option of the share dialog of the media editor"));
            const atPos = tg(_T("Start where it is now ({0})", MV.FormatTime(pos), "Option of the share dialog of the media editor, {0} the current play position"), "AtPos", _TF("Starts the media where it is now", "Tool tip of an option of the share dialog of the media editor"));
            const abs = tg(_TF("With the site address", "Option of the share dialog of the media editor"), "Absolute", _TF("On: a link that works anywhere (mail, chat, other sites); off: a short link for pages of this site", "Tool tip of an option of the share dialog of the media editor"));
            if (!(pos > 0.5))
                atPos.E.hidden = true;
            const openB = new Sw.Button({
                Text: _TF("Open in a new tab", "Button of the share dialog of the media editor"), Icon: "external",
                OnClick: () => window.open(t.ShareText(eff(), pos, true), "_blank", "noopener"),
            });
            const what = el("p.me-share-what");
            function update() {
                const embed = state.Kind === "embed";
                const player = state.Kind !== "bare";
                //  Only the options that apply are shown (an embed code always has the full address)
                for (const x of [controls, stats, muted, loop])
                    x.E.hidden = !player || state.Background;
                bg.E.hidden = !player || !visual;
                bgNote.hidden = bg.E.hidden || !state.Background;
                shows.hidden = !player || state.Background;
                play.hidden = (!player || state.Background) && atPos.E.hidden;
                abs.E.hidden = absNote.hidden = embed;
                absNote.textContent = state.Absolute ? _TF("Works anywhere: in mail, chat and on other sites.", "Note under an option of the share dialog of the media editor (on)")
                    : _TF("A short link: it only works on pages of this site.", "Note under an option of the share dialog of the media editor (off)");
                sizeG.hidden = !embed;
                out.Input.rows = embed ? 7 : 4;
                openB.E.hidden = embed;
                what.textContent = embed ? _TF("Paste this into the html of your page, where the media goes.", "Explanation of the embed code choice of the share dialog of the media editor")
                    : state.Kind === "bare" ? _TF("A page with only the media, without controls (ex: for a screen or a frame).", "Explanation of the media only choice of the share dialog of the media editor")
                        : _TF("A page with the player: open it, send it, or link to it.", "Explanation of the player page choice of the share dialog of the media editor");
                out.Set(t.ShareText(eff(), pos, false), true);
                const n = Object.keys(t.Changed()).length;
                const len = out.Get().length;
                note.textContent = (n === 1 ? _TF("1 changed setting is in it; the others are the defaults.", "Note of the share dialog of the media editor with one changed setting") : n ? _T("{0} changed settings are in it; the others are the defaults.", n, "Note of the share dialog of the media editor, {0} the number of changed settings")
                    : _TF("All settings are the defaults: the link has none.", "Note of the share dialog of the media editor"))
                    + (len > 2000 ? " " + _TF("It is long: some apps cut links above 2000 characters.", "Warning of the share dialog of the media editor about a long link") : "");
            }
            //  The choices are no user data: closing never asks to discard them
            const group = (title, ...items) => el("div.me-share-group", null, el("div.me-share-head", { text: title }), el("div.me-share-opts", null, ...items));
            const shows = group(_TF("Shows", "Heading of options of the share dialog of the media editor: what the player shows"), controls.E, stats.E);
            const play = group(_TF("Playback", "Heading of options of the share dialog of the media editor"), muted.E, loop.E, atPos.E);
            const bgNote = el("p.me-share-sub", { text: _TF("This switch sets: no controls, no statistics, muted, looping.", "Note under the page background option of the share dialog of the media editor") });
            const absNote = el("p.me-share-sub");
            const sizeG = group(_TF("Size", "Heading of options of the share dialog of the media editor"), width.E);
            const body = el("div.me-share", { attrs: { "data-sw-nodirty": "" } }, kind.E, what,
                el("div.me-share-mode", null, bg.E, bgNote), shows, play, el("div.me-share-mode", null, abs.E, absNote), sizeG, out.E, note);
            update();
            return Sw.Dialog.Show({
                Title: _TF("Share this media", "Title of the share dialog of the media editor"),
                Icon: "share", Content: body, Wide: false,
                Buttons: [
                    { Text: _TF("Close", "Button of a dialog"), Dismiss: true },
                    {
                        Text: _TF("Copy", "Button of the share dialog of the media editor"), Icon: "copy", Kind: "primary",
                        OnClick: () => {
                            Sw.Clipboard.Copy(out.Get());
                            return false;
                        },
                    },
                ],
                OnShow: (close, box) => {
                    const foot = box.querySelector(".sw-dialog-foot");
                    if (foot)
                        foot.prepend(openB.E);
                },
            });
        }

        /**
         * The shared text of the share dialog.
         * @param {object} s The choices.
         * @param {number} pos The play position.
         * @param {boolean} forOpen For "Open in a new tab" (always the full address).
         * @returns {string} The link or the embed code.
         */
        ShareText(s, pos, forOpen) {
            const t = this;
            const o = { Absolute: s.Absolute || forOpen || s.Kind === "embed", Pos: s.AtPos ? pos : 0 };
            if (s.Kind === "bare")
                return t.PageUrl("MediaPreview.html", Object.assign(o, { Fill: false }));
            const vo = { NoControls: !s.Controls, NoStats: !s.Stats, Muted: s.Muted, Loop: s.Loop };
            const url = new URL(t.PageUrl("MediaView.html", Object.assign(o, vo)), window.location.href);
            const rel = s.Absolute || forOpen || s.Kind === "embed" ? url.href : t.PageUrl("MediaView.html", Object.assign(o, vo, { Absolute: false }));
            if (s.Kind !== "embed")
                return rel;
            const esc = x => String(x).replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");
            //  The shape of the media (audio: a bar), the width as chosen (a plain number is pixels)
            const audio = t.Type === TAudio;
            const p = t.Player;
            const ratio = p && p.Width > 0 && p.Height > 0 && !audio ? Math.round(p.Width) + " / " + Math.round(p.Height) : "16 / 9";
            let w = String(s.Width || "").trim() || "100%";
            if (/^\d+(\.\d+)?$/.test(w))
                w += "px";
            if (!/^\d+(\.\d+)?(px|%|em|rem|vw)$/.test(w))
                w = "100%";
            const size = audio ? "width: " + w + "; height: 64px" : "width: " + w + "; aspect-ratio: " + ratio;
            //  One attribute per line: easy to read and to change in a page
            return "<iframe\n  src=\"" + esc(url.href) + "\"\n  title=\"" + esc(MediaName(t.Type, t.Data)) + "\"\n  style=\"" + size + "; border: 0\"\n  allow=\"autoplay; fullscreen; encrypted-media; picture-in-picture\" allowfullscreen loading=\"lazy\"></iframe>";
        }

        // ---------------------------------------------------------------- undock

        /**
         * Post a message to a popped out player (MediaView.html with `ctrl`).
         * @param {string} name The message name.
         * @param {object} [data] Data.
         */
        Post(name, data) {
            const t = this;
            if (t.Undocked && t.Prefix)
                Sw.InterOp.Post(t.Prefix + name, data);
        }

        /**
         * Show the player in a window of its own that follows every change.
         */
        Undock() {
            const t = this;
            if (t.Type < 0 || t.LocalName)
                return;
            const pos = t.Player ? t.Player.GetPos() : 0;
            const url = t.PageUrl("MediaView.html", { Ctrl: t.Prefix, Pos: pos });
            const r = t.Stage.getBoundingClientRect();
            const w = window.open(url, "SwMediaView" + t.Prefix, "popup=yes,width=" + Math.round(Math.max(480, r.width)) + ",height=" + Math.round(Math.max(320, r.height)));
            if (!w) {
                Sw.Toast(_TF("The browser blocked the new window: allow pop-ups for this site and try again.", "Error of the media editor when the pop out window was blocked"), { Kind: "warning" });
                return;
            }
            if (t.Player)
                t.Player.Pause();
            t.Undocked = { Win: w, Timer: setInterval(() => { if (w.closed) t.Dock(true); }, 700) };
            t.ShowUndockedNote();
            t.UpdateChrome();
        }

        /**
         * The note over the stage while the player is popped out.
         */
        ShowUndockedNote() {
            const t = this;
            t.UndockNote && t.UndockNote.remove();
            const st = new Sw.EmptyState({
                Icon: "window",
                Title: _TF("The player is in its own window", "Title shown in the media editor while the player is popped out"),
                Text: _TF("Every change here shows there at once. Close that window, or use Show here, to get the player back.", "Text shown in the media editor while the player is popped out"),
                Actions: [
                    new Sw.Button({ Text: _TF("Go to the window", "Button of the media editor while the player is popped out"), Icon: "external", Kind: "ghost", OnClick: () => t.Undocked && t.Undocked.Win.focus() }).E,
                    new Sw.Button({ Text: _TF("Show here", "Button of the media editor: brings the popped out player back into the editor"), Icon: "dock", Kind: "primary", OnClick: () => t.Dock() }).E,
                ],
            });
            t.UndockNote = el("div.me-undocked", null, st.E);
            t.Stage.appendChild(t.UndockNote);
        }

        /**
         * Show the player here again (closes the window unless it closed itself).
         * @param {boolean} [closed] The window was closed by the user.
         */
        Dock(closed) {
            const t = this;
            const u = t.Undocked;
            if (!u)
                return;
            clearInterval(u.Timer);
            t.Undocked = null;
            if (!closed)
                try {
                    u.Win.close();
                }
                catch {
                }
            t.UndockNote && t.UndockNote.remove();
            t.UndockNote = null;
            if (t.Player)
                t.Player.Play();
            if (closed)
                Sw.Toast(_TF("The player window was closed: the player is back here", "Note of the media editor when the popped out player window was closed"), { NoCopy: true, Duration: 3000 });
            t.UpdateChrome();
        }

        // ---------------------------------------------------------------- menus

        /**
         * Effect files of a library kind (cached).
         * @param {string} kind "Background", "Image" or "Collage".
         * @returns {Promise<string[]>} The names.
         */
        async EffectNames(kind) {
            const t = this;
            t.FxNames = t.FxNames || {};
            t.FxLists = t.FxLists || {};
            if (!t.FxNames[kind])
                t.FxNames[kind] = Sw.Net.Get("Api/Get" + kind + "Effects").catch(() => []).then(x => t.FxLists[kind] = Array.isArray(x) ? x : []);
            return (await t.FxNames[kind]) || [];
        }

        /**
         * The effects of the library as choices: an image's effects (a text collage: the collage effects first), or the background
         * effects (the Effect type). The current one is listed too when it is an own file.
         * @param {string} kind "image", "collage" or "background".
         * @returns {Array<{Value: string, Label: string, Hint: string}>} The choices ("" = no effect, not for "background").
         */
        EffectOptions(kind) {
            const t = this;
            const L = t.FxLists || {};
            const groups = kind === "background" ? [["Background", "backgroundEffects/", _TF("Effect", "Kind of an effect in the effect picker of the media editor")]]
                : kind === "collage" ? [["Collage", "collageEffects/", _TF("Collage effect", "Kind of an effect in the effect picker of the media editor")], ["Image", "imageEffects/", _TF("Picture effect", "Kind of an effect in the effect picker of the media editor")]]
                    : [["Image", "imageEffects/", _TF("Picture effect", "Kind of an effect in the effect picker of the media editor")]];
            const out = kind === "background" ? [] : [{ Value: "", Label: _TF("No effect", "Choice of the effect of a picture in the media editor"), Icon: "close" }];
            for (const [g, folder, hint] of groups)
                for (const n of L[g] || [])
                    if (n !== "None")
                        out.push({ Value: folder + n + ".glsl", Label: Humanize(n), Hint: groups.length > 1 ? hint : undefined, Icon: "sparkle" });
            const cur = kind === "background" ? (t.Type === TEffect ? t.Data : "") : (t.Props.Effect || "");
            if (cur && !out.some(o => o.Value === cur))
                out.push({ Value: cur, Label: MediaName(TEffect, cur), Hint: _TF("Own file", "Kind of an effect in the effect picker of the media editor"), Icon: "file" });
            return out;
        }

        /**
         * The Effects button: a searchable list of the library. An effect opens it; for a picture or a text it is the effect drawn over it.
         * @param {HTMLElement} anchor The button.
         * @returns {Promise<void>}
         */
        async PickEffect(anchor) {
            const t = this;
            const overlay = t.Type === TImage || t.Type === TText;
            const kind = !overlay ? "background" : t.Type === TText && t.Props.CollageSeparator ? "collage" : "image";
            await Promise.all(["Background", "Image", "Collage"].map(k => t.EffectNames(k)));
            const opts = t.EffectOptions(kind);
            if (!opts.length) {
                Sw.Toast(_TF("No effects found in the library", "Shown by the effects button of the media editor when the library is empty"), { Kind: "warning" });
                return;
            }
            const cur = overlay ? (t.Props.Effect || "") : (t.Type === TEffect ? t.Data : null);
            Sw.ListPicker(anchor, opts, cur, v => overlay ? t.Change("Effect", v, true, true) : t.Open(TEffect, v, { Undo: true }));
        }

        /**
         * The More menu.
         * @returns {Array} Menu items.
         */
        MoreMenu() {
            const t = this;
            const none = t.Type < 0;
            return [
                {
                    Text: _TF("Copy settings", "Item of the more menu of the media editor"), Icon: "copy", Disabled: none,
                    Desc: _TF("The changed settings, for your own code", "Description of an item of the more menu of the media editor"),
                    OnClick: () => Sw.Clipboard.Copy(JSON.stringify(t.Changed(), null, 2)),
                },
                {
                    Text: _TF("Paste settings…", "Item of the more menu of the media editor"), Icon: "braces", Disabled: none || !t.CanEdit,
                    Desc: _TF("Settings copied from another media or link", "Description of an item of the more menu of the media editor"),
                    OnClick: () => t.PasteSettings(),
                },
                {
                    Text: _TF("Open the player page", "Item of the more menu of the media editor"), Icon: "external", Disabled: none || !!t.LocalName,
                    OnClick: () => window.open(t.PageUrl("MediaView.html", { Absolute: true }), "_blank", "noopener"),
                },
                "-",
                {
                    Text: _TF("New media", "Item of the more menu of the media editor: start again without a media"), Icon: "newFile", Disabled: !!t.Host || none,
                    OnClick: () => {
                        t.PushUndo();
                        if (t.Undocked)
                            t.Dock();
                        t.ShowEmpty();
                        t.Source.Set("", true);
                        history.replaceState(history.state, "", window.location.pathname);
                    },
                },
                //  Theme and text size: only on a page of its own (inside the app shell or another page the host has them)
                ...(!Sw.Page.IsEmbedded && Sw.Menu.Appearance ? ["-", ...Sw.Menu.Appearance({ Density: false })] : []),
            ];
        }

        /**
         * Take settings from a JSON text (ex: copied from another link).
         * @returns {Promise<void>}
         */
        async PasteSettings() {
            const t = this;
            const s = await Sw.Dialog.Prompt(_TF("Paste settings", "Title of a dialog of the media editor"), "", "{ \"Volume\": 50 }", {
                Text: _TF("The settings as JSON (ex: the props of a MediaView link). They go on top of the current ones.", "Text of a dialog of the media editor"),
                Multiline: true, Mono: true, OkText: _TF("Use them", "Button of a dialog of the media editor"),
                Validate: v => {
                    try {
                        const o = JSON.parse(v);
                        return o && typeof o === "object" && !Array.isArray(o) ? null : _TF("Not a JSON object", "Error of a dialog of the media editor");
                    }
                    catch (e) {
                        return _TF("Not valid JSON: ", "Error of a dialog of the media editor") + e.message;
                    }
                },
            });
            if (!s)
                return;
            t.PushUndo();
            Object.assign(t.Props, JSON.parse(s));
            t.FxSig = null;
            t.RefreshValues();
            t.ApplyVisibility();
            t.Post("Reload", t.PlayerProps());
            await t.Rebuild();
            t.ReadFx();
            t.RenderFx();
            t.Committed();
        }

        // ---------------------------------------------------------------- drop, paste, keys

        /**
         * A file or link dropped anywhere on the page opens it (the player view handles its own link drops first).
         */
        HookDrop() {
            const t = this;
            let depth = 0;
            //  A drag that started on this page (a setting dragged onto another setting): no "drop a file or link" overlay
            let inPage = false;
            const can = ev => {
                const types = ev.dataTransfer ? [...ev.dataTransfer.types] : [];
                return !t.Host && !inPage && (types.includes("Files") || types.includes("text/uri-list") || types.includes("text/plain"));
            };
            const off = () => {
                depth = 0;
                document.body.classList.remove("me-dropping");
            };
            document.addEventListener("dragstart", () => inPage = true, true);
            document.addEventListener("dragend", () => {
                inPage = false;
                off();
            }, true);
            //  Capture: a drop target that stops the event (the settings rows) still ends the overlay
            document.addEventListener("drop", off, true);
            document.addEventListener("dragenter", ev => {
                if (!can(ev))
                    return;
                ++depth;
                document.body.classList.add("me-dropping");
            });
            document.addEventListener("dragleave", () => {
                if (--depth <= 0)
                    off();
            });
            document.addEventListener("dragover", ev => {
                if (!can(ev))
                    return;
                //  Text boxes take dropped text themselves
                if (ev.target.closest && ev.target.closest("input, textarea") && ![...ev.dataTransfer.types].includes("Files"))
                    return;
                ev.preventDefault();
                ev.dataTransfer.dropEffect = [...ev.dataTransfer.types].includes("Files") ? "copy" : "link";
            });
            document.addEventListener("drop", ev => {
                off();
                if (ev.defaultPrevented || t.Host || inPage)
                    return;
                const dt = ev.dataTransfer;
                if (!dt)
                    return;
                const f = dt.files && dt.files[0];
                if (f) {
                    ev.preventDefault();
                    t.OpenFile(f);
                    return;
                }
                if (ev.target.closest && ev.target.closest("input, textarea"))
                    return;
                const text = dt.getData("text/uri-list") || dt.getData("text/plain") || "";
                if (!text)
                    return;
                ev.preventDefault();
                t.Source.Set(text.trim().split(/\r?\n/)[0], true);
                t.OpenTyped();
            });
            //  Paste with nothing focused: a link opens (the player view does it while it exists)
            document.addEventListener("paste", ev => {
                const a = document.activeElement;
                if (t.View || t.Host || (a && (a.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(a.tagName))))
                    return;
                const text = ev.clipboardData ? ev.clipboardData.getData("text/plain") : "";
                if (!text)
                    return;
                ev.preventDefault();
                t.Source.Set(text.trim(), true);
                t.OpenTyped();
            });
        }

        /**
         * Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z) undo / redo outside text boxes, Ctrl+S saves (host data mode).
         */
        HookKeys() {
            const t = this;
            document.addEventListener("keydown", ev => {
                if (!(ev.ctrlKey || ev.metaKey) || ev.altKey)
                    return;
                const k = ev.key.toLowerCase();
                const a = document.activeElement;
                const inText = a && (a.isContentEditable || /^(INPUT|TEXTAREA)$/.test(a.tagName));
                if (k === "s" && t.SaveB) {
                    ev.preventDefault();
                    t.SaveB.Click();
                    return;
                }
                if (inText)
                    return;
                //  Steps of the object editor: its own keys handle them (in the editor and, for its last use, on the page)
                const es = t.EditStack();
                if (es && ((k === "z" && !ev.shiftKey) ? es.CanUndo() : (k === "y" || k === "z") ? es.CanRedo() : false))
                    return;
                if (k === "z" && !ev.shiftKey) {
                    ev.preventDefault();
                    t.Undo();
                }
                else if (k === "y" || (k === "z" && ev.shiftKey)) {
                    ev.preventDefault();
                    t.Redo();
                }
            });
        }

        // ---------------------------------------------------------------- host data mode

        /**
         * The Save / Revert row of the host data mode.
         */
        AddHostButtons() {
            const t = this;
            if (!t.Host || !t.CanEdit || !t.Host.Update)
                return;
            t.SaveB = new Sw.Button({
                Text: _TF("Save changes", "Button text in the media editor"), Icon: "IconSave", Kind: "primary", Shortcut: "Mod+S",
                Disabled: _TF("No changes to save yet", "Why the save button of the media editor is disabled"),
                OnClick: async () => {
                    const src = t.Host;
                    const id = src.Id;
                    const r = (id !== null && typeof id === "object" && !Array.isArray(id)) ? Object.assign({}, id) : (src.Legacy ? { AssignmentId: id } : { Id: id });
                    const data = clone(t.Props);
                    delete data.EffectProps;
                    if (data.Crop)
                        data.Crop.VisualizeMargins = false;
                    r.Data = data;
                    if (!(await Sw.Net.Send(src.Update, r)))
                        throw new Error(_TF("Failed to update!", "Error when the media settings could not be saved"));
                    t.Saved = JSON.stringify(t.Props);
                    t.UpdateChrome();
                },
            });
            const revert = new Sw.Button({
                Text: _TF("Revert", "Button of the media editor: back to the saved settings"), Icon: "undo", Kind: "ghost",
                OnClick: () => {
                    t.PushUndo();
                    return t.Restore(JSON.stringify({ Type: t.Type, Data: t.Data, Props: JSON.parse(t.Saved) }));
                },
            });
            t.Foot.append(revert.E, t.SaveB.E);
            t.Foot.hidden = false;
        }

        // ---------------------------------------------------------------- boot

        /**
         * Start the editor from the url.
         * @returns {Promise<void>}
         */
        async Start() {
            const t = this;
            await MV.Ready;
            t.Layout();
            //  The effect library (the effect choices of the settings need it at once)
            await Promise.all(["Background", "Image", "Collage"].map(k => t.EffectNames(k)));
            t.Prefix = await MV.MessagePrefix(location.href + "_" + Date.now() + "_" + Math.random());
            //  A popped out player asks for the settings
            Sw.InterOp.On("*", m => {
                if (!m || !m.Type || !t.Prefix)
                    return;
                if (m.Type === t.Prefix + "GetProps")
                    Sw.InterOp.Post(t.Prefix + "SetProps", t.PlayerProps());
                else if (m.Type === t.Prefix + "Closed" && t.Undocked)
                    setTimeout(() => t.Undocked && t.Undocked.Win.closed && t.Dock(true), 300);
            });
            window.addEventListener("pagehide", () => t.Undocked && t.Dock());
            const src = MV.ReadEditSource();
            if (src) {
                t.Host = src;
                const info = await Sw.Net.Send(src.Get, src.Id);
                if (!info)
                    throw new Error(_TF("The media was not found.", "Error when the media editor's get api returned nothing"));
                const e = info.Edit;
                t.CanEdit = src.EditMask ? ((typeof e === "number" ? e : 0) & src.EditMask) !== 0 : !!e;
                const props = info.Data || Defaults(info.Type, info.Link);
                if (!props.$type && MV.ParamTypeName(info.Type))
                    props.$type = MV.ParamTypeName(info.Type);
                if (props.Crop)
                    props.Crop.VisualizeMargins = false;
                t.Saved = JSON.stringify(props);
                t.AddHostButtons();
                await t.Open(info.Type, info.Link, { Props: props, Keep: true });
                t.UpdateChrome();
                return;
            }
            const U = Sw.Url;
            if (!(U.Get("link") ?? U.Get("d"))) {
                t.ShowEmpty();
                return;
            }
            const mp = MV.ReadMediaParams();
            t.FromUrl = true;
            await t.Open(mp.Type, mp.Data, { Props: mp.Props, Start: mp.Start });
        }
    }

    /**
     * Boot function of MediaEdit.html.
     * @returns {Promise<void>}
     */
    async function Main() {
        const remove = Sw.Page.Loading();
        const ed = new Editor();
        Sw.MediaEdit.Current = ed;
        try {
            await ed.Start();
        }
        catch (e) {
            if (!ed.App)
                MV.ShowLoadError(document.body, e);
            else {
                ed.Stage.textContent = "";
                MV.ShowLoadError(ed.Stage, e);
            }
        }
        finally {
            remove();
            Sw.Page.Loaded();
        }
    }

    Sw.MediaEdit = { Main, Editor, Schema, Defaults, Kinds, FxDefs, Current: null };
})();
