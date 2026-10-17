/**
 * @file canvasTools.js - Pure 2D-canvas text rendering utilities used by the media player text support.
 *
 * Loading: included by MediaPlayer.Init() (media_player.js, via common's includeJs relative to
 * media_player.js) and directly by TextDemo.html with a script tag. Must be loaded before
 * media_player_text.js, because MediaPlayerParamsText extends TextStyle.
 *
 * Globals defined: TextGradientStop, TextLinearGradient, TextStyle (style descriptors that mirror the
 * C# text style types) and CanvasTools (static helpers that measure and render styled text into
 * canvases, text grids ("collages"), numeric glyph strips for counter effects, and convert canvases to
 * Blob / object URLs).
 *
 * Dependencies: only browser APIs (CanvasRenderingContext2D, HTMLCanvasElement.toBlob, URL.createObjectURL).
 * Conventions: no DOM attachment except the debug TextStyle.AttachTo; object URLs created here are never
 * revoked by this file (the caller owns them); the default output mime type is "image/webp".
 */

/**
 * One color stop of a TextLinearGradient. Plain data object (mirrors the C# gradient stop type).
 */
class TextGradientStop {
    /** {number} [0, 1] The position along the gradient */
    P = 0;
    /** {string} The css color at this position*/
    Color = "#000";
}

/**
 * Linear gradient definition used for text fill or stroke. Coordinates are relative [0, 1] to the
 * rectangle passed to CanvasTools.BuildCanvasGradient (the text area inside the margins).
 * Default is a top-to-bottom gradient with no stops. Create with `new` or CanvasTools.CreateLinearGradient.
 */
class TextLinearGradient {
    /** {number} [0, 1] Start horizontal position of the gradient */
    X1 = 0;
    /** {number} [0, 1] Start vertical position of the gradient */
    Y1 = 0;
    /** {number} [0, 1] End horizontal position of the gradient */
    X2 = 0;
    /** {number} [0, 1] End vertical position of the gradient */
    Y2 = 1;
    /** {array[TextGradientStop]} Array of gradient stops */
    Stops = [];
}

/**
 * Styling of rendered text (font, fill, stroke, gradients, shadows, margins). Mirrors the C# text style
 * type; MediaPlayerParamsText (media_player_text.js) extends it. Instances are plain data: create with
 * `new TextStyle()` and override fields. CanvasTools.ComputeMargins caches its result in an extra
 * `ComputedMargins` field ([Top, Right, Bottom, Left]) on the style object.
 */
class TextStyle {

    /** {string} The css font to use */
    Font = 'bold 128px Verdana';

    /** {string} Optional url to the font to use */
    FontUrl;

    /** {string} Name of the font loaded from the url, should match the facename in Font */
    FontUrlName;

    /** {number} Additional spacing between each glyph */
    LetterSpacing = 0;

    /** {string} The fill css color for the text */
    Fill = "#fff";

    /** {TextLinearGradient} The fill gradient, if non-null this is used instead of the Fill color */
    FillGradient = null;

    /** {string} The stroke css color for the text */
    Stroke = "#006";

    /** {TextLinearGradient} The stroke gradient, if non-null this is used instead of the Stroke color */
    StrokeGradient = null;

    /** {number} The width of the stroke */
    StrokeWidth = 2;

    /** {string} The stroke cap style: "butt", "round" or "square" */
    StrokeCap = "round";

    /** {string} The css color for the shadow from the fill (only rendered if Fill or FillGradient is present) */
    ShadowFill = "#000c";

    /** {string} The css color for the shadow from the stroke (only rendered if Stroke or StrokeGradient is present and StrokeWidth is greater than zero) */
    ShadowStroke = null;

    /** {number} The horizontal distance to throw the shadow */
    ShadowX = 3;

    /** {number} The vertical distance to throw the shadow */
    ShadowY = 5;

    /** {number} The blur radius */
    ShadowBlur = 12;

    /** {number} Left edge margin */
    MarginLeft = 1;

    /** {number} Top edge margin */
    MarginTop = 1;

    /** {number} Rught edge margin */
    MarginRight = 1;

    /** {number} Bottom edge margin */
    MarginBottom = 1;

    /** {boolean} If true, the stroke (if available) is rendered before the fill */
    StrokeFirst = true;

    /** {HTMLElement} attach the canvas to this element before rendering (mostly useful for debugging) */
    AttachTo = null;
}


/**
 * Static-only helper class for measuring and rendering styled text (TextStyle) to 2D canvases and
 * converting the canvases to image Blobs / object URLs. Never instantiated.
 */
class CanvasTools {


    /**
     * Create a gradient definition
     * @param {number} x1 [0, 1] Relative start X-position
     * @param {number} y1 [0, 1] Relative start Y-position
     * @param {number} x2 [0, 1] Relative end X-position
     * @param {number} y2 [0, 1] Relative end Y-position
     * @param {number,color} stops Tuples, of stop position [0, 1] and css color string: ex: 0.0, #fff, 1.0, #000
     * @returns {TextLinearGradient} A gradient definition
     */
    static CreateLinearGradient(x1, y1, x2, y2, stops) {
        const g = new TextLinearGradient();
        g.X1 = x1;
        g.Y1 = y1;
        g.X2 = x2;
        g.Y2 = y2;
        const s = [];
        const al = arguments.length;
        for (let i = 4; i < al; i += 2) {
            const ss = new TextGradientStop();
            ss.P = arguments[i];
            ss.Color = arguments[i + 1];
            s.push(ss);
        }
        g.Stops = s;
        return g;
    }

    /**
     * Build a Canvas 2D gradient object from a given gradient definition
     * @param {CanvasRenderingContext2D} cc Canvas context
     * @param {number} x1 The left side of the gradient rectangle
     * @param {number} y1 The top side of the gradient rectangle
     * @param {number} x2 The right side of the gradient rectangle
     * @param {number} y2 The bottom side of the gradient rectangle
     * @param {TextLinearGradient} gradient The gradient
     * @returns {CanvasGradient} A canvas gradient that can be used for fill or stroke
     */
    static BuildCanvasGradient(cc, x1, y1, x2, y2, gradient) {
        if (!gradient)
            return null;
        let g = null;
        const w = x2 - x1;
        const h = y2 - y1;
        if (!g)
            g = cc.createLinearGradient(x1 + w * gradient.X1, y1 + h * gradient.Y1, x1 + w * gradient.X2, y1 + h * gradient.Y2);
        if (!g)
            return null;
        const s = gradient.Stops;
        const sl = s.length;
        for (let i = 0; i < sl; ++i) {
            const ss = s[i];
            g.addColorStop(ss.P, ss.Color);
        }
        return g;
    }


    /**
     * Compute margins for a given style
     * @param {TextStyle} style The style
     * @param {boolean} useCache If true, any cached margins are used, the cached margins are stored in the style object
     * @returns {Array[number]} [Top, Right, Bottom, Left]
     */
    static ComputeMargins(style, useCache) {
        let m = style.ComputedMargins;
        if (m && useCache)
            return m;
        //  Compute margins
        let mt = style.MarginTop;
        let mr = style.MarginRight;
        let mb = style.MarginBottom;
        let ml = style.MarginLeft;

        const haveFill = (style.Fill || style.FillGradient) && (style.Fill != 'none');

        const sw = style.StrokeWidth;
        const haveStroke = (style.Stroke || style.StrokeGradient) && (sw > 0);

        const sf = style.ShadowFill;
        const ss = style.ShadowStroke;

        //  Shadowed extension
        if ((sf && haveFill) || (ss && haveStroke)) {
            let se = style.ShadowBlur;
            if (ss && haveStroke)
                se += sw;
            se *= 0.5;
            const scx = style.ShadowX;
            const scy = style.ShadowY;
            mt += Math.max(0, se - scy);
            mr += Math.max(0, se + scx);
            mb += Math.max(0, se + scy);
            ml += Math.max(0, se - scx);
        } else {
            if (haveStroke) {
                const em = sw * 0.5;
                mt += em;
                mr += em;
                mb += em;
                ml += em;
            }
        }
        m = [mt, mr, mb, ml];
        style.ComputedMargins = m;
        return m;
    }

    /**
     * "Apply" margins to some text measurement
     * @param {TextMetrics} m Some measured text
     * @param {Array[number]} margins Margins [Top, Right, Bottom, Left]
     * @returns [width:integer, height:integer, drawAtX, drawAtY, marginTop, marginRight, marginBottom, marginLeft]
     */
    static ApplyMargins(m, margins) {
        let mt = margins[0];
        let mr = margins[1];
        let mb = margins[2];
        let ml = margins[3];
        const w = m.actualBoundingBoxRight + m.actualBoundingBoxLeft + ml + mr;
        const h = m.actualBoundingBoxDescent + m.actualBoundingBoxAscent + mt + mb;
        const iw = Math.ceil(w) | 0;
        const ih = Math.ceil(h) | 0;
        const eh = (iw - w) * 0.5;
        const ev = (ih - h) * 0.5;
        mt += ev;
        mr += eh;
        mb += ev;
        ml += eh;
        const x = ml + m.actualBoundingBoxLeft;
        const y = mt + m.actualBoundingBoxAscent;
        return [iw, ih, x, y, mt, mr, mb, ml];

    }


    /**
     * Measure a text with the given style: sets cc.font and cc.letterSpacing from the style, measures,
     * and applies the (cached) style margins.
     * @param {CanvasRenderingContext2D} cc Canvas context (its font/letterSpacing are modified)
     * @param {string} text The text to measure
     * @param {TextStyle} style The styling of the text
     * @returns {Array<number>} See ApplyMargins: [width, height, drawAtX, drawAtY, marginTop, marginRight, marginBottom, marginLeft]
     */
    static MeasureText(cc, text, style) {
        const margins = CanvasTools.ComputeMargins(style);
        //  Measure and resize
        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";
        const m = cc.measureText(text);
        return CanvasTools.ApplyMargins(m, margins);
    }



    /**
     * Draw a text with the given style at a position. Renders a stroke pass and a fill pass (order given by
     * style.StrokeFirst), each with its own shadow (ShadowStroke / ShadowFill, sharing ShadowX/Y/Blur).
     * Gradients are built relative to the given gradient rectangle. Sets imageSmoothingQuality = "high"
     * (errors ignored). Note: the fill pass is skipped only if both Fill and FillGradient are falsy
     * (a Fill of 'none' is still passed to fillStyle, which the canvas ignores as invalid).
     * @param {CanvasRenderingContext2D} cc Canvas context to draw on
     * @param {string} text The text to draw
     * @param {TextStyle} style The styling of the text
     * @param {number} posX Text baseline-start x position (fillText/strokeText x)
     * @param {number} posY Text baseline y position
     * @param {number} gradX1 Left side of the gradient rectangle
     * @param {number} gradY1 Top side of the gradient rectangle
     * @param {number} gradX2 Right side of the gradient rectangle
     * @param {number} gradY2 Bottom side of the gradient rectangle
     * @returns {void}
     */
    static RenderText(cc, text, style, posX, posY, gradX1, gradY1, gradX2, gradY2) {
        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";

        try {
            cc.imageSmoothingQuality = "high";
        }
        catch {
        }

        const haveFill = (style.Fill || style.FillGradient) && (style.Fill != 'none');
        const sw = style.StrokeWidth;
        const haveStroke = (style.Stroke || style.StrokeGradient) && (sw > 0);
        const sf = style.ShadowFill;
        const ss = style.ShadowStroke;

        /**
         * Fill pass: draws the text with Fill or FillGradient and the ShadowFill shadow (if set), inside save/restore.
         * @returns {void}
         */
        function fill() {
            if (!style.Fill)
                if (!style.FillGradient)
                    return;
            cc.save();
            if (sf) {
                cc.shadowColor = sf;
                cc.shadowBlur = style.ShadowBlur;
                cc.shadowOffsetX = style.ShadowX;
                cc.shadowOffsetY = style.ShadowY;
            } else {
                cc.shadowColor = null;
            }
            cc.fillStyle = style.FillGradient ? CanvasTools.BuildCanvasGradient(cc, gradX1, gradY1, gradX2, gradY2, style.FillGradient) : style.Fill;
            cc.fillText(text, posX, posY);
            cc.restore();
        }

        /**
         * Stroke pass (only if a stroke color/gradient exists and StrokeWidth > 0): draws the outline with
         * StrokeWidth, StrokeCap, Stroke or StrokeGradient and the ShadowStroke shadow (if set), inside save/restore.
         * @returns {void}
         */
        function stroke() {
            if (!haveStroke)
                return;
            cc.save();
            if (ss) {
                cc.shadowColor = ss;
                cc.shadowBlur = style.ShadowBlur;
                cc.shadowOffsetX = style.ShadowX;
                cc.shadowOffsetY = style.ShadowY;
            } else {
                cc.shadowColor = null;
            }
            cc.lineWidth = style.StrokeWidth;
            cc.lineCap = style.StrokeCap;
            cc.strokeStyle = style.StrokeGradient ? CanvasTools.BuildCanvasGradient(cc, gradX1, gradY1, gradX2, gradY2, style.StrokeGradient) : style.Stroke;
            cc.strokeText(text, posX, posY);
            cc.restore();
        }


        if (style.StrokeFirst) {
            stroke();
            fill();
        } else {
            fill();
            stroke();
        }


    }


    /**
     * Create a canvas element with the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @returns {HTMLCanvasElement} The generated canvas element
     */
    static CreateTextImage(text, style) {
        if (!style)
            style = new TextStyle();
        const c = document.createElement("canvas");
        if (style.AttachTo)
            style.AttachTo.appendChild(c);
        const cc = c.getContext("2d");

        const stats = CanvasTools.MeasureText(cc, text, style);
        c.width = stats[0];
        c.height = stats[1];
        CanvasTools.RenderText(cc, text, style, stats[2], stats[3], stats[7], stats[4], stats[0] - stats[5], stats[1] - stats[6]);
        return c;
    }

    /**
     * Create an image Blob with the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<Blob>} A promise returning a Blob containing the image
     */
    static CreateTextImageBlob(text, style, mime = null, quality = 1) {

        const c = CanvasTools.CreateTextImage(text, style);
        return new Promise((resolve, reject) => {
            c.toBlob(data => {
                if (data)
                    resolve(data);
                else
                    reject();
            }, mime ?? "image/webp", quality);
        });
    }


    /**
     * Create an url to an image containing the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<string>} A promise returning an url to the image
     */
    static async CreateTextImageUrl(text, style, mime = null, quality = 1) {

        const b = await CanvasTools.CreateTextImageBlob(text, style, mime, quality);
        return URL.createObjectURL(b);
    }

    /**
     * Create a canvas with any number of texts rendered on a grid
     * @param {Array[string]} texts The texts to render
     * @param {TextStyle} style The styling of the text
     * @param {integer} columnCount The number of columns
     * @param {object} stats Optional object that will recieve some stats
     * @returns {HTMLCanvasElement} The generated canvas element
     */
    static CreateTextGrid(texts, style, columnCount = 2, stats = null) {
        const minMargin = 1;
        style = style ?? new TextStyle();
        if (style.MarginLeft < minMargin)
            style.MarginLeft = minMargin;
        if (style.MarginTop < minMargin)
            style.MarginTop = minMargin;
        if (style.MarginRight < minMargin)
            style.MarginRight = minMargin;
        if (style.MarginBottom < minMargin)
            style.MarginBottom = minMargin;

        const c = document.createElement("canvas");
        if (style.AttachTo)
            style.AttachTo.appendChild(c);
        const cc = c.getContext("2d");
        //  Measure and resize
        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";

        const textCount = texts.length;
        const rowCount = Math.ceil(textCount / columnCount) | 0;

        /**
         * Width of a measured text: the larger of the ink box width (right - left) and the advance width;
         * if the ink width is <= 0 (e.g. a space) half the advance width is used.
         * @param {TextMetrics} m Measured text
         * @returns {number} Width in pixels
         */
        function GetWidth(m) {
            const t = m.actualBoundingBoxRight - m.actualBoundingBoxLeft;
            return t <= 0 ? (m.width * 0.5) : Math.max(t, m.width);
        }

        const ms = [];
        let maxW = 0;
        let maxAsc = 0;
        let maxDesc = 0;
        for (let i = 0; i < textCount; ++i) {
            const m = cc.measureText(texts[i]);
            let t = GetWidth(m);
            if (t > maxW)
                maxW = t;
            t = m.actualBoundingBoxDescent;
            if (t > maxDesc)
                maxDesc = t;
            t = m.actualBoundingBoxAscent;
            if (t > maxAsc)
                maxAsc = t;
            ms.push(m);
        }

        const margins = CanvasTools.ComputeMargins(style);
        const mt = margins[0];
        const mr = margins[1];
        const mb = margins[2];
        const ml = margins[3];

        const iw = Math.ceil(maxW + ml + mr) | 0;
        const ih = Math.ceil(maxDesc + maxAsc + mt + mb) | 0;

        const pw = iw * columnCount;
        const ph = ih * rowCount;
        c.width = pw;
        c.height = ph;
        const y = mt + maxAsc;

        for (let i = 0; i < textCount; ++i) {
            const tx = (i % columnCount) * iw;
            const ty = ((i / columnCount) | 0) * ih;
            const m = ms[i];
            CanvasTools.RenderText(cc, texts[i], style,
                ml + tx + maxW * 0.5 - (m.actualBoundingBoxRight + m.actualBoundingBoxLeft) * 0.5 + m.actualBoundingBoxLeft, ty + y, 
                ml + tx, mt + ty, iw - mr + tx, ih - mb + ty
            );
        }
        if (stats) {

            stats.TileCountX = columnCount;
            stats.TileCountY = rowCount;
            stats.TileCount = textCount;
            stats.TileWidth = iw;
            stats.TileHeight = ih;
            stats.PageWidth = pw;
            stats.PageHeight = ph;
            stats.ImageWidth = iw - minMargin * 2;
            stats.ImageHeight = ih - minMargin * 2;
            stats.Border = minMargin
            stats.UniqueCount = textCount;
        }
        console.log("Text grid image size: " + pw + "x" + ph);
        return c;
    }



    /**
     * Create an image Blob with the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @param {integer} columnCount Number of columns in the genereated image
     * @param {object} stats Optional object that will recieve some stats
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<Blob>} A promise returning a Blob containing the image
     */
    static CreateTextGridBlob(texts, style, columnCount = 2, stats = null, mime = null, quality = 1) {

        const c = CanvasTools.CreateTextGrid(texts, style, columnCount, stats);
        return new Promise((resolve, reject) => {
            c.toBlob(data => {
                if (data)
                    resolve(data);
                else
                    reject();
            }, mime ?? "image/webp", quality);
        });
    }


    /**
     * Create an url to an image containing the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @param {integer} columnCount Number of columns in the genereated image
     * @param {object} stats Optional object that will recieve some stats
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<string>} A promise returning an url to the image
     */
    static async CreateTextGridUrl(texts, style, columnCount = 2, stats = null, mime = null, quality = 1) {

        const b = await CanvasTools.CreateTextGridBlob(texts, style, columnCount, stats, mime, quality);
        return URL.createObjectURL(b);
    }



    /**
     * Create a canvas element with a numeric font useable in counter effects.
     * Layout: two columns, 11 rows of equal height. The left (digit) column holds the digits
     * 1,2,...,9,0,1 (row i = (i + 1) % 10). The right ("other") column holds the thousands separator (row 1),
     * prefix (row 2), suffix (row 3) and decimal separator (row 4).
     * Extra fields are set on the returned canvas: NumberWidth, OtherWidth, TotalWidth, and the rounded widths
     * (incl. margins, 0 when empty) ThousandsSeparatorWidth, DecimalSeparatorWidth, PrefixWidth, SuffixWidth.
     * @param {TextStyle} [style=new TextStyle()] The styling of the text
     * @param {integer} [extraSpacing=0] Extra spacing between each glyph (in the height); currently normalized but not used
     * @param {string} [thousandSeparator=" "] A string to use as thousands separator (undefined gives " ", null gives "")
     * @param {string} [decimalSeparator="."] A string to use as decimal separator
     * @param {string} [prefix=""] A string to use as a numerical prefix
     * @param {string} [suffix=""] A string to use as a numerical suffix
     * @returns {HTMLCanvasElement} The generated canvas element
     */
    static CreateNumberImage(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix) {
        style = style ?? new TextStyle();
        extraSpacing = extraSpacing ?? 0;
        thousandSeparator = typeof thousandSeparator === "undefined" ? " " : (thousandSeparator ?? "");
        decimalSeparator = decimalSeparator ?? '.';
        prefix = prefix ?? "";
        suffix = suffix ?? "";

        const c = document.createElement("canvas");
        if (style.AttachTo)
            style.AttachTo.appendChild(c);
        const cc = c.getContext("2d");

        //  Measure and resize
        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";

        /**
         * Width of a measured text: the larger of the ink box width (right - left) and the advance width;
         * if the ink width is <= 0 (e.g. a space) half the advance width is used.
         * @param {TextMetrics} m Measured text
         * @returns {number} Width in pixels
         */
        function GetWidth(m) {
            const t = m.actualBoundingBoxRight - m.actualBoundingBoxLeft;
            return t <= 0 ? (m.width * 0.5) : Math.max(t, m.width);
        }

        const ms = [];
        let maxW = 0;
        let maxAsc = 0;
        let maxDesc = 0;
        for (let i = 0; i < 10; ++i)
        {
            const m = cc.measureText("" + i);
            let t = GetWidth(m);
            if (t > maxW)
                maxW = t;
            t = m.actualBoundingBoxDescent;
            if (t > maxDesc)
                maxDesc = t;
            t = m.actualBoundingBoxAscent;
            if (t > maxAsc)
                maxAsc = t;
            ms.push(m);
        }
        let maxOtherW = 0;
        /**
         * Measure a non-digit string (separator, prefix, suffix) and grow maxOtherW, maxAsc and maxDesc.
         * @param {string} text The text to measure
         * @returns {TextMetrics} The measurement
         */
        function measureOther(text) {
            const m = cc.measureText(text);
            let t = GetWidth(m);
            if (t > maxOtherW)
                maxOtherW = t;
            t = m.actualBoundingBoxDescent;
            if (t > maxDesc)
                maxDesc = t;
            t = m.actualBoundingBoxAscent;
            if (t > maxAsc)
                maxAsc = t;
            return m;
        }

        const tsM = measureOther(thousandSeparator);
        const dsM = measureOther(decimalSeparator);
        const prefixM = measureOther(prefix);
        const suffixM = measureOther(suffix);


        //  Compute margins

        const margins = CanvasTools.ComputeMargins(style);

        let mt = margins[0];
        let mr = margins[1];
        let mb = margins[2];
        let ml = margins[3];

        const wn = maxW + ml + mr;
        const wo = maxOtherW + ml + mr;
        const h = maxAsc + maxDesc + mt + mb;
        const iwn = Math.ceil(wn) | 0;
        const iwo = Math.ceil(wo) | 0;
        const ih = Math.ceil(h) | 0;
        const iw = iwn + iwo;
        c.width = iw;
        c.height = ih * 11;
        const ev = (ih - h) * 0.5;
        mt += ev;
        mb += ev;
        const ehn = (iwn - wn) * 0.5;
        const mrn = mr + ehn;
        const mln = ml + ehn;

        const eho = (iwo - wo) * 0.5;
        const mro = mr + eho;
        const mlo = ml + eho + iwn;
        const om = ml + mr;

        c.NumberWidth = iwn;
        c.OtherWidth = iwo;
        c.TotalWidth = iw;

        const cThousandsSeparatorWidth = GetWidth(tsM);
        const cDecimalSeparatorWidth = GetWidth(dsM);
        const cPrefixWidth = GetWidth(prefixM);
        const cSuffixWidth = GetWidth(suffixM);


        c.ThousandsSeparatorWidth = Math.ceil(cThousandsSeparatorWidth) + (cThousandsSeparatorWidth > 0 ? om : 0);
        c.DecimalSeparatorWidth = Math.ceil(cDecimalSeparatorWidth) + (cDecimalSeparatorWidth > 0 ? om : 0);
        c.PrefixWidth = Math.ceil(cPrefixWidth) + (cPrefixWidth > 0 ? om : 0);
        c.SuffixWidth = Math.ceil(cSuffixWidth) + (cSuffixWidth > 0 ? om : 0);

        //  Render
        const xn = mln + maxW * 0.5;
        const y = mt + maxAsc;


        for (let i = 0; i < 11; ++i) {
            const num = (i + 1) % 10;
            const m = ms[num];
            const oy = i * ih;
            CanvasTools.RenderText(cc, "" + num, style,
                xn - (m.actualBoundingBoxRight + m.actualBoundingBoxLeft) * 0.5 + m.actualBoundingBoxLeft, y + oy,
                mln, mt + oy, iw - mrn, ih - mb + oy);
        }

        /**
         * Render one non-digit string into the "other" column (right of the digit column) at row i.
         * @param {number} www Unrounded ink width of the string (used for the gradient rectangle)
         * @param {TextMetrics} m Measurement of the string
         * @param {number} i Row index (1 = thousands separator, 2 = prefix, 3 = suffix, 4 = decimal separator)
         * @param {string} text The string to render (shadows the outer function name inside)
         * @returns {void}
         */
        function text(www, m, i, text) {
            const x = mlo - m.actualBoundingBoxLeft;
            const oy = i * ih;
            CanvasTools.RenderText(cc, text, style,
                x, y + oy,
                mlo, mt + oy, mlo + www, ih - mb + oy);
        }
        text(cThousandsSeparatorWidth, tsM, 1, thousandSeparator);
        text(cPrefixWidth, prefixM, 2, prefix);
        text(cSuffixWidth, suffixM, 3, suffix);
        text(cDecimalSeparatorWidth, dsM, 4, decimalSeparator);
        return c;
    }


    /**
     * Create an image Blob with a numeric font useable in counter effects
     * @param {TextStyle} style The styling of the text
     * @param {integer} extraSpacing Extra spacing between each glyph (in the height)
     * @param {string} thousandSeparator A string to use as thousands separator
     * @param {string} decimalSeparator A string to use as decimal separator
     * @param {string} prefix A string to use as a numerical prefix
     * @param {string} suffix A string to use as a numerical suffix
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<[Blob,HTMLCanvasElement]>} A promise returning an array with [Blob containing the image, Canvas element]
     */
    static CreateNumberImageBlob(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix, mime = null, quality = 1) {

        const c = CanvasTools.CreateNumberImage(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix);
        return new Promise((resolve, reject) => {
            c.toBlob(data => {
                if (data) {
                    resolve([data, c]);
                } else
                    reject();
            }, mime ?? "image/webp", quality);
        });
    }


    /**
     * Create an url to an image containing a numeric font useable in counter effects
     * @param {TextStyle} style The styling of the text
     * @param {integer} extraSpacing Extra spacing between each glyph (in the height)
     * @param {string} thousandSeparator A string to use as thousands separator
     * @param {string} decimalSeparator A string to use as decimal separator
     * @param {string} prefix A string to use as a numerical prefix
     * @param {string} suffix A string to use as a numerical suffix
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<[string,Blob,HTMLCanvasElement]>} A promise returning an array with: [url to the image, Blob containing the image, Canvas element]
     */
    static async CreateNumberImageUrl(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix, mime = null, quality = 1) {
        const b = await CanvasTools.CreateNumberImageBlob(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix, mime, quality);
        return [URL.createObjectURL(b[0]), b[0], b[1]];
    }



    /**
     * Convert a canvas to an image blob
     * @param {HTMLCanvas} canvas The canvas to convert
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<Blob>} A promise returning an image blob containing the canvas
     */
    static CreateCanvasImageBlob(canvas, mime = null, quality = 1) {

        return new Promise((resolve, reject) => {
            canvas.toBlob(data => {
                if (data) {
                    resolve(data);
                } else
                    reject();
            }, mime ?? "image/webp", quality);
        });
    }


    /**
     * Create an image url from a canvas
     * @param {HTMLCanvas} canvas The canvas to convert
     * @param {string} mime Optionally the mime type to use, ex: "image/jpeg", "image/png", "image/webp"
     * @param {number} quality [0, 1] Optionally the encoding quality of the image for lossy formats.
     * @returns {Promise<[string,Blob]>} A promise returning an array with: [url to the image, Blob containing the image]
     */
    static async CreateCanvasImageUrl(canvas, mime = null, quality = 1) {
        const b = await CanvasTools.CreateCanvasImageBlob(canvas, mime, quality);
        return [URL.createObjectURL(b), b];
    }


}
