/**
 * @file textEffect.js - text and animated number counter effects rendered through GLSL image effects.
 *
 * Loaded with a classic `<script defer>` (no module) as `../textEffect.js` by `counterEffects/index.html`
 * (after `../../common/common.js`, `../../common/anim.js`, `../canvasTools.js`), which boots `textEffectMain()`
 * from `<body onload>`.
 *
 * Globals defined: `CounterEffectParams` (class), `TextEffect` (static factory class), `textEffectMain` (function).
 *
 * Dependencies: common.js (`includeJs`, `getUrlParams`, `PageLoaded`), mediaPlayer/canvasTools.js
 * (`TextStyle`, `CanvasTools`), mediaPlayer/media_player.js (`MediaPlayer`, `MediaTypes`) and the parameter
 * classes `MediaPlayerParamsEffect` / `MediaPlayerParamsImage` (from the mediaPlayer folder), plus the
 * `imageEffects/*.glsl` and `counterEffects/*.glsl` shaders.
 *
 * Conventions: the factories return a media player; the caller appends `player.Element` and then calls
 * `Show()` and `Play()`. Both factories await the async `MediaPlayer.Create` and `player.Cache()`.
 */

/**
 * Top-level call executed at parse time: loads the media player framework (`MediaPlayer`, `MediaTypes`, ...)
 * relative to this script's URL. The load is asynchronous; the code expects it to be done when `onload` fires.
 */
includeJs(document.currentScript.src, "../mediaPlayer/media_player.js");


/**
 * Plain options object for `TextEffect.CreateCounterEffect`; created with `new` and modified by the caller.
 */
class CounterEffectParams {
    /** @type {number} Fade height in pixels at the top/bottom of the digits (clamped to a minimum of 1); also sent to the shader as `Fade`. */
    Fade = 16.0;
    /** @type {number} Extra vertical spacing (pixels, clamped to >= 0) between the glyph rows of the digit atlas image. */
    ExtraSpacing = 10.0;
    /** @type {number} Number of integer digit positions (clamped to 1..32, truncated to an integer). */
    MaxDigits = 10;
    /** @type {number} Speed factor of the digit flip animation (clamped to >= 0; multiplied by FlipFraction). Can be overridden per frame by GetValue returning [value, flipSpeed]. */
    FlipSpeed = 1;
    /** @type {number} Fraction (clamped 0..1) of each unit step of a digit that is spent rolling to the next digit. */
    FlipFraction = 0.1;
    /** @type {number} Number of decimal digits (0 = no decimal separator). */
    DecimalDigits = 2;
    /** @type {string} String drawn as thousands separator (every 3 integer digits); empty/zero width = none. */
    ThousandsSeparator = " ";
    /** @type {string} String drawn as decimal separator. */
    DecimalSeparator = ".";
    /** @type {string} Text drawn after the number (empty = none). */
    Suffix = "";
    /** @type {string} Text drawn before the number (empty = none); scrolls with the leading digit. */
    Prefix = "";
    /** @type {number} Extra horizontal pixels between digits (can be negative to tighten them). */
    DigitSpacing = 0;

}


/**
 * Factory class with static methods only (never instantiated); returns media players that render text or an
 * animated number counter through a GLSL image effect.
 */
class TextEffect {


    /**
     * Loads the player scripts the factories need before they create `MediaPlayerParamsImage` /
     * `MediaPlayerParamsEffect` objects (`MediaPlayer.Create` loads player classes lazily, which is too late
     * for that, and does not load media_player_image.js for an image with an effect). Loading is de-duplicated
     * by `includeJs`, so repeated calls are cheap.
     * @returns {Promise<void>}
     */
    static async LoadPlayers() {
        const current = MediaPlayer.CurrentUrl;
        await Promise.all([
            includeJs(current, "media_player_image.js"),
            includeJs(current, "media_player_effect.js"),
            includeJs(current, "media_effect_program.js"),
        ]);
    }


    /**
     * Create a text effect
     * Steps: if `textStyle.FontUrl` is set, loads it as a `FontFace` named `textStyle.FontUrlName` and adds it to
     * `document.fonts`; forces `effectParams.Transparent = true`; renders `text` to an image (blob) URL with
     * `CanvasTools.CreateTextImageUrl`; maps a name not ending in ".glsl" to `imageEffects/<name>.glsl`
     * (relative to the page); creates an Image player with `MediaPlayerParamsImage{Effect, EffectParams}`
     * and calls `player.Cache()`.
     * Only a commented-out call in textEffectMain uses this method.
     * @param {string} effectName Name of the effect (one of the predefined image effects, or the url to a .glsl image effect file), default = "Rain"
     * @param {string} text The text to display
     * @param {TextStyle} textStyle The styling of the text, default = new TextStyle()
     * @param {MediaPlayerParamsEffect} effectParams Additional effect params, default = new MediaPlayerParamsEffect() (modified: Transparent = true)
     * @returns {Promise<MediaPlayerEffect>} The effect, use .Element to get and attach the elemnt, then .Show() and .Play()
     */
    static async CreateTextEffect(effectName, text, textStyle, effectParams) {
        await TextEffect.LoadPlayers();
        effectName = effectName ?? "Rain";
        textStyle = textStyle ?? new TextStyle();
        if (textStyle.FontUrl) {
            const ff = new FontFace(textStyle.FontUrlName, "url('" + textStyle.FontUrl + "')");
            await ff.load();
            document.fonts.add(ff);
        }
        effectParams = effectParams ?? new MediaPlayerParamsEffect();
        effectParams.Transparent = true;
        const url = await CanvasTools.CreateTextImageUrl(text, textStyle);
        if (!effectName.endsWith(".glsl"))
            effectName = "imageEffects/" + effectName + ".glsl";
        const ip = new MediaPlayerParamsImage();
        ip.Effect = effectName;
        ip.EffectParams = effectParams;
        const player = await MediaPlayer.Create(MediaTypes.Image, url, ip);
        await player.Cache();
        return player;
    }


    /**
     * Create a counter effect
     * Steps:
     * 1. Loads `textStyle.FontUrl` as a FontFace when set; ensures `effectParams.FxProps` exists; clamps Fade (>= 1),
     *    ExtraSpacing (>= 0, then minus fade) and MaxDigits (1..32); sets `textStyle.MarginTop = 4` and
     *    `MarginBottom = 4 + waste` (the textStyle object is modified); forces `effectParams.Transparent = true`.
     * 2. Renders a digit atlas with `CanvasTools.CreateNumberImageUrl` (11 vertically stacked rows: digits,
     *    blank, separators, prefix, suffix); the returned canvas carries NumberWidth, TotalWidth,
     *    ThousandsSeparatorWidth, DecimalSeparatorWidth, PrefixWidth, SuffixWidth.
     * 3. Builds the part list from right to left with `DefPart`: suffix, decimal digits + decimal separator,
     *    integer digits with a thousands separator after every 3rd (not after the last), prefix, end marker (-1).
     * 4. Maps a name not ending in ".glsl" to `../counterEffects/<name>.glsl`; uses a fixed size
     *    (`EffectSizeFromImage = false`, `effectParams.Width = width`, `Height = atlasHeight/11 + fade - waste`,
     *    `AdaptiveSize = false`) and sets the shader vars `FxProps.MaxParts`, `UvScale`, `UvAdd`, `Fade`.
     * 5. Creates and caches the image player, looks up the uniforms `digits` (vec4 array) and `digitWidths`
     *    (float array) and installs `player.OnNewSize`, `player.GetValue` and `player.OnRender`.
     * @param {string} effectName Name of the effect (one of the predefined counter effects, or the url to a .glsl counter effect file), default = "ScrollUp"
     * @param {TextStyle} textStyle The styling of the text, default = new TextStyle() (modified: MarginTop/MarginBottom)
     * @param {CounterEffectParams} counterParams Additional counter effect params, default = new CounterEffectParams()
     * @param {MediaPlayerParamsEffect} effectParams Additional effect params, default = new MediaPlayerParamsEffect() (modified)
     * @returns {Promise<MediaPlayerEffect>} The effect, use .Element to get and attach the elemnt, then .Show() and .Play().
     * Set `player.GetValue` to drive the displayed value and optionally `player.OnNewSize` to react to width changes.
     */
    static async CreateCounterEffect(effectName, textStyle, counterParams, effectParams) {

        await TextEffect.LoadPlayers();
        effectName = effectName ?? "ScrollUp";
        counterParams = counterParams ?? new CounterEffectParams();
        textStyle = textStyle ?? new TextStyle();
        if (textStyle.FontUrl) {
            const ff = new FontFace(textStyle.FontUrlName, "url('" + textStyle.FontUrl + "')");
            await ff.load();
            document.fonts.add(ff);
        }
        effectParams = effectParams ?? new MediaPlayerParamsEffect();
        if (!effectParams.FxProps)
            effectParams.FxProps = {};

        const fade = Math.max(1, counterParams.Fade);
        const extraSpacing = Math.max(0, counterParams.ExtraSpacing) - fade;
        const maxDigits = Math.min(32, Math.max(1, counterParams.MaxDigits)) | 0;

        const waste = Math.max(0, fade + extraSpacing) + fade;
        textStyle.MarginTop = 4;
        textStyle.MarginBottom = 4 + waste;

        effectParams.Transparent = true;
        const digitSpacing = counterParams.DigitSpacing;

        const data = await CanvasTools.CreateNumberImageUrl(textStyle, extraSpacing, counterParams.ThousandsSeparator, counterParams.DecimalSeparator, counterParams.Prefix, counterParams.Suffix);
        const canvas = data[2];
        const url = data[0];
        //  Compute max width and get parts
        const partWidths = [];
        const partType = [];
        let width = 0;
        /**
         * Appends one part to the layout (parts are defined from right to left).
         * Pushes `partWidth` to `partWidths` and `type` to `partType`, and adds the width (plus `digitSpacing`
         * for digits) to the running total `width` (rounded).
         * @param {number} type - Part type: 0 digit, 17 thousands separator, 18 prefix, 19 suffix, 20 decimal separator, -1 end.
         * @param {number} partWidth - Width of the part in pixels.
         * @returns {void}
         */
        function DefPart(type, partWidth) {
            width = Math.round(partWidth + width + (type === 0 ? digitSpacing : 0));
            partWidths.push(partWidth);
            partType.push(type);
        }
        if (canvas.SuffixWidth > 0)
            DefPart(19, canvas.SuffixWidth);
        const dd = counterParams.DecimalDigits;
        let minDigits = 1;
        if (dd > 0) {
            minDigits += dd;
            for (let i = 0; i < dd; ++i)
                DefPart(0, canvas.NumberWidth);
            DefPart(20, canvas.DecimalSeparatorWidth);
        }
        const tsw = canvas.ThousandsSeparatorWidth;
        for (let i = 0; i < maxDigits; ++i) {
            DefPart(0, canvas.NumberWidth);
            if (((i % 3) === 2) && ((i + 1) < maxDigits) && (tsw > 0))
                DefPart(17, tsw);
        }
        const endPosition = partType.length - 1;
        if (canvas.PrefixWidth > 0)
            DefPart(18, canvas.PrefixWidth);
        DefPart(-1, 0);
        //  Create effect
        if (!effectName.endsWith(".glsl"))
            effectName = "../counterEffects/" + effectName + ".glsl";
        const ip = new MediaPlayerParamsImage();
        ip.Effect = effectName;
        ip.EffectParams = effectParams;
        ip.EffectSizeFromImage = false;
        effectParams.Width = width;
        effectParams.Height = canvas.height / 11.0 + fade - waste;
        effectParams.AdaptiveSize = false;

        const p = effectParams.FxProps;
        p.MaxParts = partType.length;
        p.UvScale = 1.0 / (effectParams.Height * 11.0);
        p.UvAdd = fade / canvas.height;
        p.Fade = fade;

        const player = await MediaPlayer.Create(MediaTypes.Image, url, ip);
        await player.Cache();

        const program = player.Program.Program;
        const digits = new Float32Array(partType.length * 4);
        const digitWidths = new Float32Array(partType.length);
        const digitsu = player.GL.getUniformLocation(program, "digits");
        const digitsWidthsu = player.GL.getUniformLocation(program, "digitWidths");
        const numberScaleU = (canvas.NumberWidth / canvas.TotalWidth);
        const otherOffsetU = numberScaleU;

        //  pos, type + fraction, uvScale, uvAdd
        //  type:
        //  0-11: 10 digits + space to one
        //  16: Keep blank
        //  17: Thousands separator
        //  18: Prefix
        //  19: Suffix
        //  20: Decimal separator
        // Less than zero = End

        const flipFraction = Math.max(0, Math.min(1, counterParams.FlipFraction));
        let flipSpeed = Math.max(0, counterParams.FlipSpeed) * flipFraction;
        /**
         * Called from OnRender when the visible counter width (in pixels, from the right edge to the leftmost
         * visible part) changes. Default is a no-op; callers may replace it (e.g. to resize/position the element).
         * @param {number} nw - New visible width.
         * @param {number} ow - Previous visible width (equal to nw on the first call).
         * @returns {void}
         */
        player.OnNewSize = (nw, ow) =>
        {
            //console.log(ow + " => " + nw);
        };
        /**
         * Returns the value to display for a playback position. Default: the position itself.
         * Callers replace it; returning an array `[value, flipSpeed]` also overrides the flip speed
         * (multiplied by the clamped FlipFraction) from then on.
         * @param {number} time - Playback position (`p.GetPos()` of the effect).
         * @returns {(number|Array<number>)} The value, or `[value, flipSpeed]`.
         */
        player.GetValue = time => time;



        const numberStep = canvas.NumberWidth + digitSpacing;
        let currentWidth = -1;

        let oldparts = "";

        /**
         * Per-frame render hook called by the effect player before drawing.
         * Reads the value with `player.GetValue(p.GetPos())`, then walks the parts right to left: each digit
         * shows `floor(t) % 10` and rolls (cosine eased) to the next digit during the last `FlipFraction` of a
         * unit step (the step interval shrinks by 10 per digit, so higher digits roll only when lower ones wrap);
         * `t` is divided by 10 per digit. Once `t < 1` and the minimum digits (1 + DecimalDigits) are drawn,
         * leading parts are dropped (or scrolled in while rolling) and the prefix is shifted by `scrollPrefix`.
         * Fills `digits` (per part: x position, atlas row / 11, UV scale, UV offset) and `digitWidths`, terminates
         * with zeroed entries at the end marker, logs the part list to the console when it changes, calls
         * `player.OnNewSize` when the visible width changes and uploads the uniforms `digitWidths` and `digits`.
         * @param {WebGLRenderingContext} gl - The GL context of the effect.
         * @param {object} p - The effect player (provides `GetPos()`).
         * @returns {void}
         */
        player.OnRender = (gl, p) => {
        //  Read value
            let t = player.GetValue(p.GetPos());
            if (Array.isArray(t)) {
                flipSpeed = Math.max(0, t[1]) * flipFraction;
                t = t[0];
            }


            let moveInterval = flipSpeed;
            let endPos = width;
            let partIndex = 0;
            let digs = minDigits;
            let cnt = true;
            let dest = 0;
            let startPos = 0;
            let scrollPrefix = 0;

            const parts = [];
            const partSpacing = [];

            for (; cnt; ++partIndex, dest += 4) {
                const partWidth = partWidths[partIndex];
                digitWidths[(dest >> 2)] = partWidth;
                startPos = Math.round(endPos - partWidth);
                const pt = partType[partIndex];
                let d = Math.floor(t);
                const mt = Math.min(flipFraction, moveInterval);
                const dt = t % 1;
                const cut = 1.0 - mt;
                let f = Math.max(0.0, dt - cut) / mt;
                const isEnd = (t < 1.0) && (digs <= 0);
                if (isEnd) {
                    switch (pt) {
                        case -1:
                        case 18:
                            break;
                        default:
                            if (f <= 0) {
                                dest -= 4;
                                partIndex = endPosition;
                                continue;
                            }
                            scrollPrefix = f <= 0 ? 0 : ((1.0 - f) % 1.0);
                            break;
                    }
                }
                switch (pt) {
                    case 0:
                        {
                            f = (1.0 - Math.cos(f * Math.PI)) * 0.5;
                            d %= 10;
                            const v = d;
                            d += f;
                            d = 11 - d;
                            d %= 10;
                            if (isEnd && (v == 0)) {
                                d += 10;
                                if (parts[parts.length - 1] === 17) {
                                    digits[dest - 4 + 1] = ((10 - (17 - 16) + 1) - f) / 11.0;
                                }
                            }
                            digits[dest + 0] = startPos - digitSpacing * 0.5;
                            digits[dest + 1] = d / 11.0;
                            digits[dest + 2] = numberScaleU / partWidth;
                            digits[dest + 3] = 0;
                            parts.push(pt);
                            partSpacing.push(partWidth + digitSpacing);
                            t /= 10.0;
                            moveInterval /= 10.0;
                            --digs;
                            startPos -= digitSpacing;
                        }
                        break;
                    case 16:
                    case 17:
                    case 18:
                    case 19:
                    case 20:
                        if (pt === 18) {
                            const pl = parts.length - 2;
                            let spacing = numberStep;
                            if (parts[pl] === 17)
                                spacing += partSpacing[pl];
                            digits[dest + 0] = startPos + scrollPrefix * spacing;
                        }
                        else
                            digits[dest + 0] = startPos;
                        digits[dest + 1] = (10 - (pt - 16)) / 11.0;
                        digits[dest + 2] = 1.0 / canvas.TotalWidth;
                        digits[dest + 3] = otherOffsetU;
                        parts.push(pt);
                        partSpacing.push(partWidth);
                        break;
                    default:
                        digits[dest + 0] = 0;
                        digits[dest + 1] = 0;
                        digits[dest + 2] = 0;
                        digits[dest + 3] = 0;
                        digits[dest + 4] = 0;
                        digits[dest + 5] = 0;
                        digits[dest + 6] = 0;
                        digits[dest + 7] = 0;
                        digitWidths[dest >> 2] = 0;
                        digitWidths[(dest >> 2) + 1] = 0;
                        cnt = false;
                        parts.push(pt);
                        partSpacing.push(partWidth);
                        break;
                }
                endPos = startPos;
            }



            const ps = parts.join(", ");
            if (ps !== oldparts) {
                oldparts = ps;
                console.log("Parts: " + ps);
            }


            const nw = width - startPos;
            if (nw !== currentWidth) {
                player.OnNewSize(nw, currentWidth < 0 ? nw : currentWidth);
                currentWidth = nw;
            }
            gl.uniform1fv(digitsWidthsu, digitWidths);
            gl.uniform4fv(digitsu, digits);
        };
        return player;
    }





    




}






/**
 * Demo boot function of `counterEffects/index.html` (called from `<body onload>`).
 * Builds a hard-coded gold `TextStyle` (bold 160px tahoma, fill/stroke gradients, drop shadow) and a
 * `CounterEffectParams` (prefix "SEK ", suffix ":-", "," thousands separator, DigitSpacing -25, FlipFraction 0.25,
 * Fade 40, 8 digits), creates `TextEffect.CreateCounterEffect("ScrollUp", ...)`, appends its element to the body,
 * sets `fx.GetValue = x => [(990 + x^1.6) * 100, 500]`, shows and plays it, then appends a
 * `CanvasTools.CreateTextGrid` of 16 greetings (3 columns) with the same style.
 * URL params are read (`getUrlParams()`) but not used.
 * On any error the body text is replaced with "Generic failure.\n" + error; otherwise `PageLoaded()` is called.
 * @returns {Promise<void>}
 */
async function textEffectMain() {
    const target = document.body;
    try {
        const ps = getUrlParams();


        const s = new TextStyle();

        Object.assign(s, {
            "Font": "bold 160px tahoma",
            "LetterSpacing": -5,
            "Fill": "#fff",
            "FillGradient": {
                "X1": 0,
                "Y1": 0,
                "X2": 0,
                "Y2": 1,
                "Stops": [
                    {
                        "P": 0,
                        "Color": "#AE7723"
                    },
                    {
                        "P": 0.0299,
                        "Color": "#C0923B"
                    },
                    {
                        "P": 0.0825,
                        "Color": "#DBBB60"
                    },
                    {
                        "P": 0.1302,
                        "Color": "#EFD97A"
                    },
                    {
                        "P": 0.1709,
                        "Color": "#FBEB8A"
                    },
                    {
                        "P": 0.2,
                        "Color": "#FFF290"
                    },
                    {
                        "P": 0.3619,
                        "Color": "#FFF08A"
                    },
                    {
                        "P": 0.5316,
                        "Color": "#EABD54"
                    },
                    {
                        "P": 0.6023,
                        "Color": "#CFA03C"
                    },
                    {
                        "P": 0.7129,
                        "Color": "#A9771C"
                    },
                    {
                        "P": 0.8006,
                        "Color": "#925E07"
                    },
                    {
                        "P": 0.8538,
                        "Color": "#895500"
                    },
                    {
                        "P": 0.8834,
                        "Color": "#8D5903"
                    },
                    {
                        "P": 0.9118,
                        "Color": "#97640C"
                    },
                    {
                        "P": 0.9399,
                        "Color": "#AA781C"
                    },
                    {
                        "P": 0.9677,
                        "Color": "#C39332"
                    },
                    {
                        "P": 0.995,
                        "Color": "#E3B64E"
                    },
                    {
                        "P": 1,
                        "Color": "#EABD54"
                    }
                ]
            },
            "Stroke": "#006",
            "StrokeGradient": {
                "X1": 0,
                "Y1": 0,
                "X2": 1,
                "Y2": 1,
                "Stops": [
                    {
                        "P": 0,
                        "Color": "#EDD685"
                    },
                    {
                        "P": 0.2812,
                        "Color": "#BB871D"
                    },
                    {
                        "P": 0.5174,
                        "Color": "#D9B253"
                    },
                    {
                        "P": 0.7922,
                        "Color": "#FFEFA2"
                    },
                    {
                        "P": 1,
                        "Color": "#C4942C"
                    }
                ]
            },
            "StrokeWidth": 8,
            "StrokeCap": "round",
            "ShadowFill": "#000c",
            "ShadowStroke": null,
            "ShadowX": 3,
            "ShadowY": 5,
            "ShadowBlur": 12,
            "MarginLeft": 4,
            "MarginTop": 0,
            "MarginRight": 4,
            "MarginBottom": 0,
            "StrokeFirst": true,
            "AttachTo": null,
            "ComputedMargins": [
                33,
                41,
                43,
                35
            ]
        });

        /*
        //      s.AttachTo = document.body;
        s.Font = 'bold 160px "Tahoma"';
        s.Stroke = null;
        s.StrokeWidth = 6;
        s.MarginLeft = 0;
        s.MarginRight = 0;
        s.ShadowBlur = 20;
        s.ShadowX = 6;
        s.ShadowY = 15;

        s.LetterSpacing = -5;

        s.FillGradient = CanvasTools.CreateLinearGradient(0, 0.1, 0, 0.9,
            0.0, "#fed",
            0.5, "#fff",
            1.0, "#fde"
        );

        s.StrokeGradient = CanvasTools.CreateLinearGradient(0, 0, 1, 1,
            0.0, "#652",
            0.5, "#661",
            1.0, "#541"
        );

        */

        const cp = new CounterEffectParams();
        cp.Prefix = "SEK ";
        cp.Suffix = ":-";
        cp.ThousandsSeparator = ",";
        cp.DecimalSeparator = ".";
        cp.DigitSpacing = -25.0;
        cp.FlipFraction = 0.25;
        cp.Fade = 40;
        cp.ExtraSpacing = 0;
        cp.MaxDigits = 8;

        const fx = await TextEffect.CreateCounterEffect("ScrollUp", s, cp);
        //const fx = await TextEffect.CreateTextEffect("Rain", "Hello world!", s);

        document.body.append(fx.Element);

        let value = 0;
        /**
         * Stores the mouse x offset in the local `value`; only the commented-out GetValue variants use it.
         * @param {MouseEvent} me - The mousemove event.
         * @returns {void}
         */
        document.body.onmousemove = me => {
            value = me.offsetX;
        };

        //fx.GetValue = x => [991, 100];
        //fx.GetValue = x => [990 + ((x % 3) | 0), 100];
        //fx.GetValue = x => [value, 100];
        fx.GetValue = x => [(990 + Math.pow(x, 1.6)) * 100.0, 500];
        //fx.GetValue = x => [99950 + value * 0.3, 100];



        await fx.Show();
        await fx.Play();



//Hi,Hello,Howdy,Hallo,Salam,مرحبًا,Hola,Guten tag,Ndewo,Ciao,Olá,Jambo,Hallå,Hej,Molo,Bonjour


        const texts =
            [
                "Hi",
                "Hello",
                "Howdy",
                "Hallo",
                "Salam",
                "مرحبًا",
                "Hola",
                "Guten tag",
                "Ndewo",
                "Ciao",
                "Olá",
                "Jambo",
                "Hallå",
                "Hej",
                "Molo",
                "Bonjour",
            ];

        const cc = CanvasTools.CreateTextGrid(texts, s, 3)
        document.body.append(cc);





    }
    catch (e) {
        target.innerText = "Generic failure.\n" + e;
        return;
    }
    PageLoaded();
}

