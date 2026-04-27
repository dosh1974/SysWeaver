
class TextGradientStop {
    /** {number} [0, 1] The position along the gradient */
    P = 0;
    /** {string} The css color at this position*/
    Color = "#000";
}

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

class TextStyle {

    /** {string} The css font to use */
    Font = 'bold 128px Verdana';

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

        //  Measure and resize
        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";
        const m = cc.measureText(text);

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

        const w = m.actualBoundingBoxRight + m.actualBoundingBoxLeft + ml + mr;
        const h = m.actualBoundingBoxDescent + m.actualBoundingBoxAscent + mt + mb;
        const iw = Math.ceil(w) | 0;
        const ih = Math.ceil(h) | 0;
        c.width = iw;
        c.height = ih;
        const eh = (iw - w) * 0.5;
        const ev = (ih - h) * 0.5;
        mt += ev;
        mr += eh;
        mb += ev;
        ml += eh;


        //  Render

        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";

        try {
            cc.imageSmoothingQuality = "high";
        }
        catch {
        }


        const x = ml + m.actualBoundingBoxLeft;
        const y = mt + m.actualBoundingBoxAscent;

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
            cc.fillStyle = style.FillGradient ? CanvasTools.BuildCanvasGradient(cc, ml, mt, iw - mr, ih - mb, style.FillGradient) : style.Fill;
            cc.fillText(text, x, y);
            cc.restore();
        }

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
            cc.lineWidth = sw;
            cc.lineCap = style.StrokeCap;
            cc.strokeStyle = style.StrokeGradient ? CanvasTools.BuildCanvasGradient(cc, ml, mt, iw - mr, ih - mb, style.StrokeGradient) : style.Stroke;
            cc.strokeText(text, x, y);
            cc.restore();
        }


        if (style.StrokeFirst) {
            stroke();
            fill();
        } else {
            fill();
            stroke();
        }
        return c;
    }

    /**
     * Create an image Blob with the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @returns {Promise<Blob>} A promise returning a Blob containing the image
     */
    static CreateTextImageBlob(text, style) {

        const c = CanvasTools.CreateTextImage(text, style);
        return new Promise((resolve, reject) => {
            c.toBlob(data => {
                if (data)
                    resolve(data);
                else
                    reject();
            });
        });
    }


    /**
     * Create an url to an image containing the given text and styling
     * @param {string} text The text to render
     * @param {TextStyle} style The styling of the text
     * @returns {Promise<string>} A promise returning an url to the image
     */
    static async CreateTextImageUrl(text, style) {

        const b = await CanvasTools.CreateTextImageBlob(text, style);
        return URL.createObjectURL(b);
    }

    /**
     * Create a canvas element with a numeric font useable in counter effects
     * @param {TextStyle} style The styling of the text
     * @param {integer} extraSpacing Extra spacing between each glyph (in the height)
     * @param {thousandSeparator} string A string to use as thousands separator
     * @param {decimalSeparator} string A string to use as decimal separator
     * @param {prefix} string A string to use as a numerical prefix
     * @param {suffix} string A string to use as a numerical suffix
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
        mt = Math.ceil(mt);
        mr = Math.ceil(mr);
        mb = Math.ceil(mb);
        ml = Math.ceil(ml);


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
        cc.font = style.Font;
        cc.letterSpacing = style.LetterSpacing + "px";

        try {
            cc.imageSmoothingQuality = "high";
        }
        catch {
        }


        const xn = mln + maxW * 0.5;
        const y = mt + maxAsc;
        function fill() {
            if (!haveFill)
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
            for (let i = 0; i < 11; ++i) {
                const num = (i + 1) % 10;
                const m = ms[num]; 
                const oy = i * ih;
                cc.fillStyle = style.FillGradient ? CanvasTools.BuildCanvasGradient(cc, mln, mt + oy, iw - mrn, ih - mb + oy, style.FillGradient) : style.Fill;
                cc.fillText("" + num, xn - (m.actualBoundingBoxRight + m.actualBoundingBoxLeft) * 0.5 + m.actualBoundingBoxLeft, y + oy);
            }
            function text(www, m, i, text) {
                const x = mlo - m.actualBoundingBoxLeft;
                const oy = i * ih;
                cc.fillStyle = style.FillGradient ? CanvasTools.BuildCanvasGradient(cc, mlo, mt + oy, mlo + www, ih - mb + oy, style.FillGradient) : style.Fill;
                cc.fillText(text, x, y + oy);
            }
            text(cThousandsSeparatorWidth, tsM, 1, thousandSeparator);
            text(cPrefixWidth, prefixM, 2, prefix);
            text(cSuffixWidth, suffixM, 3, suffix);
            text(cDecimalSeparatorWidth, dsM, 4, decimalSeparator);
            cc.restore();
        }

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
            cc.lineWidth = sw;
            cc.lineCap = style.StrokeCap;
            for (let i = 0; i < 11; ++i) {
                const num = (i + 1) % 10;
                const m = ms[num];
                const oy = i * ih;
                cc.strokeStyle = style.StrokeGradient ? CanvasTools.BuildCanvasGradient(cc, mln, mt + oy, iw - mrn, ih - mb + oy, style.StrokeGradient) : style.Stroke;
                cc.strokeText("" + num, xn - (m.actualBoundingBoxRight + m.actualBoundingBoxLeft) * 0.5 + m.actualBoundingBoxLeft, y + oy);
            }
            function text(www, m, i, text) {
                const x = mlo - m.actualBoundingBoxLeft;
                const oy = i * ih;
                cc.strokeStyle = style.StrokeGradient ? CanvasTools.BuildCanvasGradient(cc, mlo, mt + oy, mlo + www, ih - mb + oy, style.StrokeGradient) : style.Stroke
                cc.strokeText(text, x, y + oy);

            }
            text(cThousandsSeparatorWidth, tsM, 1, thousandSeparator);
            text(cPrefixWidth, prefixM, 2, prefix);
            text(cSuffixWidth, suffixM, 3, suffix);
            text(cDecimalSeparatorWidth, dsM, 4, decimalSeparator);
            cc.restore();
        }


        if (style.StrokeFirst) {
            stroke();
            fill();
        } else {
            fill();
            stroke();
        }
        return c;

        


    }


    /**
     * Create an image Blob with a numeric font useable in counter effects
     * @param {TextStyle} style The styling of the text
     * @param {integer} extraSpacing Extra spacing between each glyph (in the height)
     * @param {thousandSeparator} string A string to use as thousands separator
     * @param {decimalSeparator} string A string to use as decimal separator
     * @param {prefix} string A string to use as a numerical prefix
     * @param {suffix} string A string to use as a numerical suffix
     * @returns {Promise<[Blob,HTMLCanvasElement]>} A promise returning an array with [Blob containing the image, Canvas element]
     */
    static CreateNumberImageBlob(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix) {

        const c = CanvasTools.CreateNumberImage(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix);
        return new Promise((resolve, reject) => {
            c.toBlob(data => {
                if (data) {
                    resolve([data, c]);
                } else
                    reject();
            });
        });
    }


    /**
     * Create an url to an image containing a numeric font useable in counter effects
     * @param {TextStyle} style The styling of the text
     * @param {integer} extraSpacing Extra spacing between each glyph (in the height)
     * @param {thousandSeparator} string A string to use as thousands separator
     * @param {decimalSeparator} string A string to use as decimal separator
     * @param {prefix} string A string to use as a numerical prefix
     * @param {suffix} string A string to use as a numerical suffix
     * @returns {Promise<[string,Blob,HTMLCanvasElement]>} A promise returning an array with: [url to the image, Blob containing the image, Canvas element]
     */
    static async CreateNumberImageUrl(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix) {

        const b = await CanvasTools.CreateNumberImageBlob(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix);
        return [URL.createObjectURL(b[0]), b[0], b[1]];
    }

}
