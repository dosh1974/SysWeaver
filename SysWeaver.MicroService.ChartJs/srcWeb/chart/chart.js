/**
 * @file chart.js - the SysWeaver chart viewer (SysWeaver.MicroService.ChartJs), built on the Sw core / ui libraries.
 * Not to be confused with the vendored Chart.js library in external/chart.js.
 *
 * Page: chart.html?q=<data url>&... (q relative to chart/ or absolute; ~30 lower-cased url parameters, see chartMain).
 * Embedded by the app shell (menu type 6), explore tables ("show as chart"), ServerManager tiles, chat attachments and
 * links built by C# (ChartJsService, UserStorageChartExporter).
 *
 * Loading: classic deferred script after ../sw/core.js, ../sw/ui.js, external/chart.js (global Chart) and
 * external/chartjs_plugin_datalabels.js (global ChartDataLabels). When the script tag has a `data-main` attribute
 * (chart.html) the page entry chartMain runs on DOM ready. canvas2svg (global C2S) is loaded on demand for svg exports.
 *
 * Structure:
 *  - color helpers (seeded random, hsl, generated colors, gradient color strings),
 *  - data transforms (min / max filter, count limit, order, label modes, color schemes),
 *  - Chart.js setup (plugins, defaults, value formatter),
 *  - ChartView: one live, polling chart (view state, rebuilds, live merge, menu, keyboard, exports),
 *  - ChartOptions, the public API Sw.Chart and the legacy aliases CanvasChart / CanvasChartOptions,
 *  - chartMain: the page entry (url parameters).
 *
 * Server contract: the q url returns a Chart.js config extended with the C# ChartJsConfig fields (RefreshRate, ValidTypes,
 * Precision, Title, ValuePrefix, ValueSuffix, ValueLabel, DisableOrderMenu, DisableColorMenu). Exporters: GET Api/GetChartExporters,
 * POST Api/ExportChart. A click on a data point posts {Type: "ChartClick", Label, Value, DatasetIndex, DataIndex} to window.parent.
 *
 * Globals defined: Sw.Chart (new API), CanvasChart, CanvasChartOptions (legacy aliases), chartMain.
 * View state (type, order, colors, export scale) lives in memory only (no storage), as before.
 */
(function () {
    "use strict";

    const Sw = window.Sw;

    /** @type {HTMLScriptElement|null} This script element (base url of the lazy includes, data-main flag). */
    const Script = document.currentScript;

    /** @type {string} Url of this script (lazy includes resolve relative to it). */
    const ScriptUrl = Script ? Script.src : window.location.href;

    // ================================================================== constants

    /**
     * A4 landscape export sizes: 297x210 mm minus 10 mm margins (277x190 mm) at 150 dpi (WL x HL) and 300 dpi (WH x HH).
     * @type {{LowDpi: number, HighDpi: number, WL: number, HL: number, WH: number, HH: number}}
     */
    const A4 = (() => {
        const mmToInch = 0.0393700787;
        const w = 297 - 20, h = 210 - 20;
        return {
            LowDpi: 150, HighDpi: 300,
            WL: (w * 150 * mmToInch) | 0, HL: (h * 150 * mmToInch) | 0,
            WH: (w * 300 * mmToInch) | 0, HH: (h * 300 * mmToInch) | 0,
        };
    })();

    /** @type {Set<string>} Chart types without x / y axes. */
    const NoAxisTypes = new Set(["pie", "doughnut", "polarArea"]);

    /** Color mode names (also part of the icon class names "IconColor" + name). */
    const ColorMode = Object.freeze({
        RandomValue: "RandomValueGradient",
        RandomSeries: "RandomSeriesGradient",
        RandomMagnitude: "RandomMagnitudeGradient",
        LabelText: "LabelText",
        Original: "Original",
    });

    /**
     * Display names of the 12 hue steps (30 degrees apart) of the fixed gradients (FixedValue/FixedSeries/FixedMag 0-11).
     * @type {string[]}
     */
    const FixedColors = [
        _TF("Red", "Chart color name"),
        _TF("Orange", "Chart color name"),
        _TF("Yellow", "Chart color name"),
        _TF("Lime green", "Chart color name"),
        _TF("Green", "Chart color name"),
        _TF("Green", "Chart color name"),
        _TF("Cyan", "Chart color name"),
        _TF("Sky blue", "Chart color name"),
        _TF("Blue", "Chart color name"),
        _TF("Purple", "Chart color name"),
        _TF("Magenta", "Chart color name"),
        _TF("Pink", "Chart color name"),
    ];

    // ================================================================== color helpers

    /** Seeded pseudo random numbers (sfc32 style) for reproducible color schemes. */
    const Rng = {
        /**
         * A random seed.
         * @returns {number[]} Four int32 values.
         */
        Seed: () => [0, 0, 0, 0].map(() => (Math.random() * 4294967295) | 0),

        /**
         * A seed from a text (non secure rolling hashes); the same text gives the same seed.
         * @param {string} text Text.
         * @returns {number[]} Four int32 values.
         */
        FromString(text) {
            let a = 98764321234, b = 38764321237, c = 18764321309, d = 8764321243;
            text = String(text ?? "");
            for (let i = 0; i < text.length; ++i) {
                const e = text.charCodeAt(i);
                a = ((a << 5) - a + e) | 0;
                b = ((b << 7) - b + e) | 0;
                c = ((c << 8) + c + e) | 0;
                d = ((d << 13) - d + e) | 0;
            }
            return [a, b, c, d];
        },

        /**
         * Create a generator.
         * @param {string|number[]|number} seed A text, four int32 values or one integer.
         * @returns {function():number} Returns floats in [0, 1).
         */
        Create(seed) {
            if (typeof seed === "string")
                seed = Rng.FromString(seed);
            let [a, b, c, d] = Array.isArray(seed) ? seed : [seed | 0, 19, 74, 11];
            return () => {
                a |= 0; b |= 0; c |= 0; d |= 0;
                const t = (((a + b) | 0) + d) | 0;
                d = (d + 1) | 0;
                a = b ^ (b >>> 9);
                b = (c + (c << 3)) | 0;
                c = (c << 21) | (c >>> 11);
                c = (c + t) | 0;
                return (t >>> 0) / 4294967296;
            };
        },
    };

    /**
     * Convert RGB to HSL.
     * @param {number} r Red 0..255.
     * @param {number} g Green 0..255.
     * @param {number} b Blue 0..255.
     * @returns {number[]} [h, s, l] in 0..1.
     */
    function rgbToHsl(r, g, b) {
        r /= 255; g /= 255; b /= 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        const l = (max + min) / 2;
        if (max === min)
            return [0, 0, l];
        const d = max - min;
        const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        let h;
        if (max === r)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (max === g)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;
        return [h / 6, s, l];
    }

    /**
     * Convert HSL to RGB (the hue wraps).
     * @param {number} h Hue (1 = full circle).
     * @param {number} s Saturation 0..1.
     * @param {number} l Lightness 0..1.
     * @returns {number[]} [r, g, b] as floats 0..255.
     */
    function hslToRgb(h, s, l) {
        h = ((h % 1) + 1) % 1;
        if (s === 0)
            return [l * 255, l * 255, l * 255];
        /**
         * One channel.
         * @param {number} p Lower bound.
         * @param {number} q Upper bound.
         * @param {number} t Hue offset.
         * @returns {number} 0..1.
         */
        const hue = (p, q, t) => {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6) return p + (q - p) * 6 * t;
            if (t < 1 / 2) return q;
            if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
            return p;
        };
        const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        const p = 2 * l - q;
        return [hue(p, q, h + 1 / 3) * 255, hue(p, q, h) * 255, hue(p, q, h - 1 / 3) * 255];
    }

    /**
     * Format "rgb(r,g,b)".
     * @param {number[]} c [r, g, b] 0..255.
     * @returns {string} Css color.
     */
    const rgb = c => "rgb(" + (c[0] | 0) + "," + (c[1] | 0) + "," + (c[2] | 0) + ")";

    /**
     * Format "rgba(r,g,b,a)".
     * @param {number[]} c [r, g, b] 0..255.
     * @param {number} a Opacity 0..1.
     * @returns {string} Css color.
     */
    const rgba = (c, a) => "rgba(" + (c[0] | 0) + "," + (c[1] | 0) + "," + (c[2] | 0) + "," + a + ")";

    /** @type {CanvasRenderingContext2D|null} Off-screen context used to resolve css colors. */
    let colorCtx = null;

    /**
     * Parse the color functions that computed styles use for mixed and relative colors (a token like color-mix(in oklab, ...) is read
     * back as "oklab(L a b)", a relative oklch color as "oklch(L C h)", a srgb mix as "color(srgb r g b)"). A canvas that does not know
     * the syntax keeps its previous fill style, so without this every theme color read from a token could silently become black
     * (black axis text on a dark theme, black tooltip text on a black tooltip).
     * @param {string} css Css color.
     * @returns {?Uint8ClampedArray} [r, g, b, a] 0..255, or null for any other syntax (resolved by the canvas).
     */
    function parseModernColor(css) {
        const m = /^\s*(oklab|oklch|color)\(\s*([^()]*)\)\s*$/i.exec(String(css || ""));
        if (!m)
            return null;
        /**
         * A component: "none" is 0, a percentage is relative to full.
         * @param {string} s Text.
         * @param {number} full The value of 100 %.
         * @returns {number} The value.
         */
        const num = (s, full) => {
            if (!s || s === "none")
                return 0;
            const v = parseFloat(s);
            return isNaN(v) ? 0 : (s.endsWith("%") ? v / 100 * full : v);
        };
        let body = m[2].trim();
        let alpha = 1;
        const slash = body.indexOf("/");
        if (slash >= 0) {
            alpha = num(body.substring(slash + 1).trim(), 1);
            body = body.substring(0, slash).trim();
        }
        const p = body.split(/[\s,]+/);
        const fn = m[1].toLowerCase();
        /** @type {number[]} Gamma encoded sRGB 0..1. */
        let c;
        const encode = v => v <= 0.0031308 ? 12.92 * v : 1.055 * Math.pow(v, 1 / 2.4) - 0.055;
        if (fn === "color") {
            const space = (p.shift() || "").toLowerCase();
            const v = p.slice(0, 3).map(x => num(x, 1));
            if (space === "srgb")
                c = v;
            else if (space === "srgb-linear")
                c = v.map(encode);
            else
                return null;
        }
        else {
            const L = num(p[0], 1);
            let a, b;
            if (fn === "oklab") {
                a = num(p[1], 0.4);
                b = num(p[2], 0.4);
            }
            else {
                const C = num(p[1], 0.4);
                const h = (parseFloat(p[2]) || 0) * Math.PI / 180;
                a = C * Math.cos(h);
                b = C * Math.sin(h);
            }
            //  OKLab to linear sRGB (Björn Ottosson's matrices)
            const l = Math.pow(L + 0.3963377774 * a + 0.2158037573 * b, 3);
            const mm = Math.pow(L - 0.1055613458 * a - 0.0638541728 * b, 3);
            const s = Math.pow(L - 0.0894841775 * a - 1.2914855480 * b, 3);
            c = [
                4.0767416621 * l - 3.3077115913 * mm + 0.2309699292 * s,
                -1.2684380046 * l + 2.6097574011 * mm - 0.3413193965 * s,
                -0.0041960863 * l - 0.7034186147 * mm + 1.7076147010 * s,
            ].map(encode);
        }
        return Uint8ClampedArray.of(Math.round(c[0] * 255), Math.round(c[1] * 255), Math.round(c[2] * 255), Math.round(Math.min(1, Math.max(0, alpha)) * 255));
    }

    /**
     * Relative luminance (WCAG 2) of a color.
     * @param {ArrayLike<number>} c [r, g, b] 0..255.
     * @returns {number} 0..1.
     */
    function luminance(c) {
        const f = v => {
            v /= 255;
            return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
        };
        return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]);
    }

    /**
     * WCAG 2 contrast ratio of two opaque colors.
     * @param {ArrayLike<number>} a [r, g, b] 0..255.
     * @param {ArrayLike<number>} b [r, g, b] 0..255.
     * @returns {number} 1..21.
     */
    function contrastRatio(a, b) {
        const x = luminance(a), y = luminance(b);
        return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
    }

    /**
     * A css color drawn over an opaque background.
     * @param {string} css Css color.
     * @param {ArrayLike<number>} bg Opaque [r, g, b] 0..255.
     * @returns {number[]} Opaque [r, g, b] 0..255.
     */
    function overColor(css, bg) {
        const c = cssToRgba(css);
        const a = c[3] / 255;
        return [0, 1, 2].map(i => c[i] * a + bg[i] * (1 - a));
    }

    /**
     * A color with at least min contrast against every background (WCAG 2: 4.5 for text, 3 for lines and borders): the color itself
     * when it has it, else the color moved in 5 % steps towards white or black (the end that contrasts more with the backgrounds), so
     * the hue of the theme stays where it can.
     * @param {string} css The wanted color.
     * @param {Array<ArrayLike<number>>} bgs Opaque backgrounds [r, g, b] 0..255; the first one is also what a translucent color is drawn over.
     * @param {number} min Minimum contrast ratio.
     * @returns {string} An opaque "rgb(r,g,b)".
     */
    function readableOn(css, bgs, min) {
        const worst = c => Math.min(...bgs.map(b => contrastRatio(c, b)));
        const c = overColor(css, bgs[0]);
        if (worst(c) >= min)
            return rgb(c);
        const end = worst([255, 255, 255]) >= worst([0, 0, 0]) ? [255, 255, 255] : [0, 0, 0];
        for (let t = 0.05; t < 1; t += 0.05) {
            const x = c.map((v, i) => v + (end[i] - v) * t);
            if (worst(x) >= min)
                return rgb(x);
        }
        return rgb(end);
    }

    /**
     * Resolve any css color to RGBA bytes.
     * @param {string} css Css color.
     * @returns {Uint8ClampedArray} [r, g, b, a] 0..255.
     */
    function cssToRgba(css) {
        const modern = parseModernColor(css);
        if (modern)
            return modern;
        colorCtx = colorCtx || document.createElement("canvas").getContext("2d", { willReadFrequently: true });
        colorCtx.clearRect(0, 0, 4, 4);
        colorCtx.fillStyle = "#000";
        colorCtx.fillStyle = css;
        colorCtx.fillRect(0, 0, 4, 4);
        return colorCtx.getImageData(1, 1, 1, 1).data;
    }

    /**
     * Generate colors with the saturation / lightness / alpha of a base color and hues fanned around its hue.
     * @param {string} css Base css color.
     * @param {number} count Number of colors.
     * @param {number} [repeat=13] Fan size (negative: ceil(count / -repeat), min 5); capped at count.
     * @param {number} [spread=120] Half the fan in degrees.
     * @returns {string[]} Css colors.
     */
    function generateColorAround(css, count, repeat, spread) {
        repeat = repeat || 13;
        if (repeat < 0)
            repeat = Math.max(5, Math.ceil(count / -repeat));
        repeat = Math.min(repeat, count);
        spread = (spread || 120) / 360;
        const c = cssToRgba(css);
        const a = c[3] / 255;
        const hsl = rgbToHsl(c[0], c[1], c[2]);
        const step = repeat > 1 ? spread * 2 / (repeat - 1) : 0;
        const cols = [];
        for (let i = 0; i < count; ++i) {
            const n = hslToRgb(hsl[0] + 4 + (i % repeat) * step - spread, hsl[1], hsl[2]);
            cols.push(a >= 1 ? rgb(n) : rgba(n, a));
        }
        return cols;
    }

    /**
     * Gradient factories for gradient color strings "name(stop;color;stop;color...)": down, up, left, right (linear over the
     * canvas) and cone (conic from -90 degrees around the center). Called as (ctx, x0, y0, x1, y1).
     * @type {Map<string, function(CanvasRenderingContext2D, number, number, number, number):CanvasGradient>}
     */
    const GradientFns = new Map([
        ["down", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(0, y0, 0, y1)],
        ["up", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(0, y1, 0, y0)],
        ["left", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(x0, 0, x1, 0)],
        ["right", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(x1, 0, x0, 0)],
        ["cone", (ctx, x0, y0, x1, y1) => ctx.createConicGradient(-Math.PI * 0.5, Math.floor((x0 + x1) * 0.5), (y0 + y1) * 0.5)],
    ]);

    /**
     * Create (or get the cached) gradient for a gradient color string. The cache lives on the Chart instance and is cleared
     * when the canvas size changes. Non canvas contexts (canvas2svg) get the first color.
     * @param {object} sc Chart.js scriptable context.
     * @param {string} text The gradient string.
     * @param {Function} factory A GradientFns entry.
     * @param {number} open Index of "(" in text.
     * @returns {CanvasGradient|string} The gradient or a color.
     */
    function gradient(sc, text, factory, open) {
        const chart = sc.chart;
        if (!chart.chartArea)
            return "#000";
        const ctx = chart.ctx;
        const w = ctx.canvas.width, h = ctx.canvas.height;
        let cache = chart.SwGradients;
        if (!cache || cache.W !== w || cache.H !== h)
            chart.SwGradients = cache = { W: w, H: h, Map: new Map() };
        let g = cache.Map.get(text);
        if (g)
            return g;
        const values = text.substring(open + 1, text.length - 1).split(";");
        if (typeof CanvasRenderingContext2D !== "undefined" && ctx instanceof CanvasRenderingContext2D) {
            g = factory(ctx, 0, 0, w, h);
            for (let i = 0; i + 1 < values.length; i += 2)
                g.addColorStop(parseFloat(values[i].trim()), values[i + 1].trim());
        }
        else
            g = (values[1] || "#000").trim();
        cache.Map.set(text, g);
        return g;
    }

    /**
     * Turn gradient color strings into scriptable colors; arrays become per data index scriptables when any entry is a
     * gradient (an empty array becomes "red", one entry is used as is).
     * @param {*} v A color value.
     * @returns {*} The resolved value.
     */
    function resolveColor(v) {
        if (Array.isArray(v)) {
            if (!v.length)
                return "red";
            if (v.length === 1)
                return resolveColor(v[0]);
            let haveFn = false;
            for (let i = 0; i < v.length; ++i) {
                v[i] = resolveColor(v[i]);
                haveFn = haveFn || typeof v[i] === "function";
            }
            return haveFn ? (sc => {
                const c = v[sc.dataIndex];
                return typeof c === "function" ? c(sc) : c;
            }) : v;
        }
        if (typeof v !== "string")
            return v;
        const open = v.indexOf("(");
        if (open > 0) {
            const factory = GradientFns.get(v.substring(0, open).trim());
            if (factory)
                return sc => gradient(sc, v, factory, open);
        }
        return v;
    }

    /** Minimum fill alpha of pie / doughnut / polar area segments (the "unused" part of a gauge like donut must stay visible). */
    const MinArcAlpha = 0.45;

    /**
     * Raise the alpha of every rgba() color in a color value (string, gradient string or array; other values unchanged).
     * @param {*} v The color value.
     * @param {number} min The minimum alpha (0-1).
     * @returns {*} The new value.
     */
    function minAlpha(v, min) {
        if (Array.isArray(v))
            return v.map(x => minAlpha(x, min));
        if (typeof v !== "string")
            return v;
        return v.replace(/rgba\(([^()]*?),\s*([\d.]+)\s*\)/gi, (m, rgb, a) => parseFloat(a) < min ? "rgba(" + rgb + "," + min + ")" : m);
    }

    /** Alpha of the theme text color used for the "track" segment of a gauge like doughnut (see arcTrack). */
    const ArcTrackAlpha = 0.12;

    /**
     * True if a color value is a faded color: every rgba() alpha in it is below MinArcAlpha and it has no opaque color.
     * @param {*} v A color value (string or gradient string).
     * @returns {boolean}
     */
    function isFaded(v) {
        if (typeof v !== "string")
            return false;
        const alphas = Array.from(v.matchAll(/rgba\([^()]*?,\s*([\d.]+)\s*\)/gi), m => parseFloat(m[1]));
        if (!alphas.length || alphas.some(a => a >= MinArcAlpha))
            return false;
        return !/#[0-9a-f]{3,8}\b|rgb\(|hsl\(/i.test(v);
    }

    /**
     * Gauge like pie / doughnut colors: the servers draw the "rest" segment (ex: idle cpu, free memory) with a faded copy of
     * the used segment's colors. A faded conic gradient over the whole ring made a 0 % gauge look half red and half yellow,
     * so faded segments next to strong ones become a flat neutral track color. Other colors get at least MinArcAlpha.
     * @param {*} v The backgroundColor value of a dataset.
     * @param {string} track The track color.
     * @returns {*} The new value.
     */
    function arcTrack(v, track) {
        if (!Array.isArray(v) || v.length < 2)
            return minAlpha(v, MinArcAlpha);
        const faded = v.map(isFaded);
        if (!faded.some(f => f) || faded.every(f => f))
            return minAlpha(v, MinArcAlpha);
        return v.map((c, i) => faded[i] ? track : minAlpha(c, MinArcAlpha));
    }

    /**
     * Resolve the colors of a dataset or options.elements entry (border, background, fill above / below).
     * @param {?object} o The object (ignored if missing).
     */
    function resolveColors(o) {
        if (!o)
            return;
        o.borderColor = resolveColor(o.borderColor);
        o.backgroundColor = resolveColor(o.backgroundColor);
        if (o.fill && typeof o.fill === "object") {
            o.fill.above = resolveColor(o.fill.above);
            o.fill.below = resolveColor(o.fill.below);
        }
    }

    // ================================================================== data transforms

    /**
     * The per point color arrays of a config (dataset and options.elements point / line / bar / arc colors and fills).
     * @param {object} data The config.
     * @returns {Array[]} The arrays.
     */
    function colorArrays(data) {
        const r = [];
        /**
         * Collect the arrays of one object.
         * @param {?object} o Dataset or element options.
         */
        const add = o => {
            if (!o)
                return;
            r.push(o.backgroundColor, o.borderColor);
            if (o.fill && typeof o.fill === "object")
                r.push(o.fill.above, o.fill.below);
        };
        data.data.datasets.forEach(add);
        const els = data.options && data.options.elements;
        if (els)
            ["point", "line", "bar", "arc"].forEach(k => add(els[k]));
        return r.filter(Array.isArray);
    }

    /**
     * The number of x positions (the longest dataset).
     * @param {object} data The config.
     * @returns {number} The count.
     */
    function pointCount(data) {
        return data.data.datasets.reduce((m, ds) => Math.max(m, Array.isArray(ds.data) ? ds.data.length : 0), 0);
    }

    /**
     * Call fn for the labels, every dataset's data and every color array that has at least minLength entries.
     * @param {object} data The config.
     * @param {number} minLength Minimum length.
     * @param {function(Array):void} fn Mutator.
     */
    function forPointArrays(data, minLength, fn) {
        [data.data.labels, ...data.data.datasets.map(ds => ds.data), ...colorArrays(data)]
            .forEach(a => {
                if (Array.isArray(a) && a.length >= minLength)
                    fn(a);
            });
    }

    /**
     * Remove every x position where any dataset value is below min or above max (url minvalue / maxvalue; 0 counts).
     * @param {object} data The config (mutated).
     * @param {?number} min Minimum (null / NaN = none).
     * @param {?number} max Maximum (null / NaN = none).
     */
    function filterData(data, min, max) {
        const hasMin = typeof min === "number" && !isNaN(min);
        const hasMax = typeof max === "number" && !isNaN(max);
        if (!(hasMin || hasMax))
            return;
        const remove = new Set();
        data.data.datasets.forEach(ds => {
            (ds.data || []).forEach((v, i) => {
                if ((hasMax && v > max) || (hasMin && v < min))
                    remove.add(i);
            });
        });
        if (!remove.size)
            return;
        const order = Array.from(remove).sort((a, b) => b - a);
        forPointArrays(data, pointCount(data), a => order.forEach(i => a.splice(i, 1)));
    }

    /**
     * Keep only the first maxCount x positions (url maxcount).
     * @param {object} data The config (mutated).
     * @param {?number} maxCount Maximum count (falsy = no limit).
     */
    function limitData(data, maxCount) {
        if (!maxCount)
            return;
        const count = pointCount(data);
        if (count > maxCount)
            forPointArrays(data, count, a => a.splice(maxCount, count - maxCount));
    }

    /**
     * A sortable number of a data value (objects use y / r, missing values sort first).
     * @param {*} v The value.
     * @returns {number} The number.
     */
    function sortValue(v) {
        if (v && typeof v === "object")
            v = v.y ?? v.r ?? v.x;
        const n = typeof v === "number" ? v : parseFloat(v);
        return isNaN(n) ? -Infinity : n;
    }

    /**
     * Reorder the x positions: 0 original, +-1 by label, +-(n+2) by the values of dataset n; negative = descending.
     * Labels, data and per point color arrays of the same length are permuted together.
     * @param {object} data The config (mutated).
     * @param {number} order The order.
     */
    function orderData(data, order) {
        if (!order)
            return;
        const labels = data.data.labels || [];
        const count = labels.length;
        const kind = Math.abs(order);
        let cmp;
        if (kind === 1)
            cmp = (a, b) => String(labels[a] ?? "").localeCompare(String(labels[b] ?? ""));
        else {
            const ds = data.data.datasets[kind - 2];
            if (!ds)
                return;
            const sd = ds.data || [];
            cmp = (a, b) => sortValue(sd[a]) - sortValue(sd[b]);
        }
        const idx = Array.from({ length: count }, (_, i) => i).sort(cmp);
        if (order < 0)
            idx.reverse();
        [labels, ...data.data.datasets.map(ds => ds.data), ...colorArrays(data)].forEach(a => {
            if (Array.isArray(a) && a.length === count) {
                const org = a.slice(0);
                for (let i = 0; i < count; ++i)
                    a[i] = org[idx[i]];
            }
        });
    }

    /**
     * Get (or create) a nested object.
     * @param {object} o Parent.
     * @param {...string} keys Path.
     * @returns {object} The object at the path.
     */
    function ensure(o, ...keys) {
        for (const k of keys) {
            if (!o[k] || typeof o[k] !== "object")
                o[k] = {};
            o = o[k];
        }
        return o;
    }

    /**
     * Assign a value at a dot separated path ("options.scales.*x.ticks.font.size"). Missing segments are created, except
     * segments prefixed with "*" which must exist (else nothing is assigned).
     * @param {object} obj Root (mutated).
     * @param {string} path Path.
     * @param {*} v Value.
     */
    function setPath(obj, path, v) {
        const s = path.split(".");
        const last = s.pop();
        for (let seg of s) {
            const must = seg[0] === "*";
            if (must)
                seg = seg.substring(1);
            if (!obj[seg]) {
                if (must)
                    return;
                obj[seg] = {};
            }
            obj = obj[seg];
        }
        obj[last] = v;
    }

    // ================================================================== value formatting and label modes

    /**
     * @typedef {object} ValueFmt
     * @property {number} Precision Decimals (negative = up to that many decimals).
     * @property {string} Prefix Value prefix.
     * @property {string} Suffix Value suffix.
     */

    /** @type {ValueFmt} The formatter of the newest chart (used by charts without their own, ex: while constructing). */
    let lastFmt = { Precision: -2, Prefix: "", Suffix: "" };

    /**
     * The formatter of a Chart instance.
     * @param {?object} chart The Chart.js instance.
     * @returns {ValueFmt} The formatter.
     */
    const fmtOf = chart => (chart && chart.SwFmt) || lastFmt;

    /**
     * Format a value with prefix and suffix (objects use their label or r).
     * @param {*} value The value.
     * @param {ValueFmt} f Formatter.
     * @returns {string} Text.
     */
    function formatValue(value, f) {
        if (value && typeof value === "object")
            value = value.label ? value.label : (value.r ? value.r : value);
        if (typeof value === "number")
            return f.Prefix + Sw.Format.Number(value, f.Precision) + f.Suffix;
        return f.Prefix + value + f.Suffix;
    }

    /**
     * Format a value without prefix / suffix.
     * @param {*} value The value.
     * @param {ValueFmt} f Formatter.
     * @returns {string} Text.
     */
    const formatBare = (value, f) => typeof value === "number" ? Sw.Format.Number(value, f.Precision) : String(value);

    /**
     * The x label of a datalabels context.
     * @param {object} c The datalabels context.
     * @returns {string} The label.
     */
    const labelOf = c => String((c.chart.data.labels || [])[c.dataIndex] ?? "");

    /**
     * Data label modes 0-6 (url l / novalues / nolabels / valuelabel, ChartJsConfig.ValueLabel), applied to
     * options.plugins.datalabels: 0 formatted value, 1 label + value, 2 label, 3 first line of the label, 4 none,
     * 5 raw value, 6 formatted number without prefix / suffix.
     * @type {Array<function(object, boolean):void>} (datalabels options, existed in the server data)
     */
    const LabelModes = [
        (dl, existed) => {
            if (existed)
                dl.display = true;
        },
        dl => {
            dl.display = true;
            dl.formatter = (v, c) => labelOf(c) + "\n" + formatValue(v, fmtOf(c.chart));
        },
        dl => {
            dl.display = true;
            dl.formatter = (v, c) => labelOf(c);
        },
        dl => {
            dl.display = true;
            dl.formatter = (v, c) => labelOf(c).split("\n")[0];
        },
        dl => {
            dl.display = false;
        },
        dl => {
            dl.display = true;
            dl.formatter = null;
        },
        dl => {
            dl.display = true;
            dl.formatter = (v, c) => formatBare(v, fmtOf(c.chart));
        },
    ];

    /**
     * Apply a data label mode to a config.
     * @param {object} config The config (options created as needed).
     * @param {number} mode 0-6 (anything else = 0).
     */
    function applyLabelMode(config, mode) {
        const plugins = ensure(config, "options", "plugins");
        const existed = !!plugins.datalabels;
        (LabelModes[mode] || LabelModes[0])(ensure(plugins, "datalabels"), existed);
    }

    /** Minimum space per category (px along the index axis) to show the per point value labels of an axis chart. */
    const MinLabelSlot = 16;

    /** Minimum length of a category tick label when the index axis is crowded (longer ones end with "…", the tooltip has the full text). */
    const MinCrowdedTickLength = 14;

    /** Maximum length of a category tick label when the index axis is crowded. */
    const MaxCrowdedTickLength = 40;

    /** Space (px) along the index axis that is not available to category labels (value axis, padding, rounding). */
    const ValueAxisReserve = 90;

    /**
     * Pie / doughnut / polar area: no data label on a 0 segment (it has no area; a "Use 0.00 %" label at the top of an
     * empty gauge looked like a drawn segment). Other values keep the configured display.
     * @param {object} data The render config (options / plugins ensured).
     */
    function hideZeroArcLabels(data) {
        const dl = data.options.plugins.datalabels;
        const org = dl.display;
        if (org === false)
            return;
        /**
         * Data label display: false for 0 values, else the configured value ("auto" by default).
         * @param {object} c The datalabels context.
         * @returns {boolean|string} The display value.
         */
        dl.display = c => {
            if (sortValue((c.dataset.data || [])[c.dataIndex]) === 0)
                return false;
            return typeof org === "function" ? org(c) : (org ?? "auto");
        };
    }

    /**
     * Pick the categories of a crowded index axis that get a tick label: one label needs `step` categories; the categories
     * with the largest magnitude (over the visible datasets) are labeled first and their neighbours within step - 1 stay
     * unlabeled. So the tallest bars always carry their own label, and every label sits exactly under (next to) its own bar.
     * Scale like labels (at least 90 % times, dates or numbers, ex: history charts) get every step-th label instead.
     * @param {object} chart The Chart.js chart.
     * @param {number} count The category count.
     * @param {number} step Categories per label (>= 1).
     * @returns {Uint8Array} 1 = labeled, per category index.
     */
    function pickLabeledCategories(chart, count, step) {
        const keep = new Uint8Array(count);
        if (step <= 1)
            return keep.fill(1);
        //  A scale like axis (times, dates, numbers: ex: a history chart) keeps evenly spaced labels
        const labels = chart.data.labels || [];
        let scaleLike = 0;
        for (let i = 0; i < count; ++i)
            if (/^[\d\s:.,\-/+%]+$/.test(String(labels[i] ?? "").trim()))
                ++scaleLike;
        if (scaleLike >= count * 0.9) {
            for (let i = 0; i < count; i += step)
                keep[i] = 1;
            return keep;
        }
        const mag = new Float64Array(count);
        (chart.data.datasets || []).forEach((ds, di) => {
            if (!chart.isDatasetVisible(di))
                return;
            const d = ds.data || [];
            for (let i = 0; i < count; ++i) {
                const v = Math.abs(sortValue(d[i]));
                if (v !== Infinity && v > mag[i])
                    mag[i] = v;
            }
        });
        const idx = Array.from({ length: count }, (_, i) => i).sort((a, b) => (mag[b] - mag[a]) || (a - b));
        const blocked = new Uint8Array(count);
        for (const i of idx) {
            if (blocked[i])
                continue;
            keep[i] = 1;
            const lo = Math.max(0, i - step + 1), hi = Math.min(count - 1, i + step - 1);
            for (let j = lo; j <= hi; ++j)
                blocked[j] = 1;
        }
        return keep;
    }

    /**
     * Keep axis charts with many categories readable without dropping data (every point is still drawn and has its tooltip):
     * the per point value labels are only shown while each category gets at least MinLabelSlot px (evaluated on every draw,
     * so resizing brings them back), and a crowded category index axis gets vertical (x) tick labels on the categories with
     * the largest values only (pickLabeledCategories; Chart.js' auto skip labels every n-th category, which left tall bars
     * unlabeled right above a neighbour's label) and long labels shortened to what the chart size allows. Settings made by
     * the server config (rotation, auto skip, tick callback, display false) are kept (then Chart.js' auto skip is used).
     * @param {object} data The render config (options / plugins ensured).
     * @param {number} [labelAll] ChartOptions.LabelAll: the most categories shown (MaxCount, the data is limited after this; Infinity
     * = all); a horizontal chart showing at most 30 then names every category. 0 = off.
     */
    function applyDensity(data, labelAll) {
        const opt = data.options;
        const ia = opt.indexAxis === "y" ? "y" : "x";
        const dl = opt.plugins.datalabels;
        const org = dl.display;
        if (org !== false) {
            /**
             * Data label display: hidden while the categories are too narrow, else the configured value ("auto" by default).
             * @param {object} c The datalabels context.
             * @returns {boolean|string} The display value.
             */
            dl.display = c => {
                const ch = c.chart;
                const s = ch.scales && ch.scales[ia];
                const n = (ch.data.labels || []).length;
                if (s && n > 0 && (ia === "x" ? s.width : s.height) / n < MinLabelSlot)
                    return false;
                return typeof org === "function" ? org(c) : (org ?? "auto");
            };
        }
        const n = data.data.labels.length;
        const sc = ensure(opt, "scales", ia);
        if (n < 2 || (sc.type && sc.type !== "category"))
            return;
        //  Time series (Design v2): horizontal time labels, a few evenly spaced ticks (Chart.js auto skip), never rotated
        if (ia === "x" && isTimeLabels(data.data.labels)) {
            const ticks = ensure(sc, "ticks");
            ticks.autoSkip = ticks.autoSkip ?? true;
            ticks.autoSkipPadding = ticks.autoSkipPadding ?? 24;
            ticks.maxRotation = 0;
            ticks.minRotation = 0;
            ticks.maxTicksLimit = ticks.maxTicksLimit ?? 6;
            return;
        }
        const el = document.body;
        //  Every bar named (ChartOptions.LabelAll: the method graph's top N on phones): a horizontal chart with a few bars labels every
        //  category (shortened to the width; a smaller font only when the bars are too thin) and keeps the value ticks straight
        const shown = Math.min(n, labelAll || n);
        if (labelAll && ia === "y" && shown <= 30) {
            const ticks = ensure(sc, "ticks");
            if (ticks.callback !== undefined || ticks.autoSkip !== undefined)
                return;
            ticks.autoSkip = false;
            const fit = Math.floor((el.clientHeight || 400) * 0.5 / shown);
            if (fit < 13)
                ticks.font = Object.assign({}, ticks.font, { size: Math.max(9, fit) });
            /**
             * Category tick label: the name shortened to about 22 % of the chart width (the axis title and legend need the rest).
             * @param {*} v Tick value (index). @param {number} i Tick index. @param {Array<object>} t The ticks.
             * @returns {string} Text.
             */
            ticks.callback = function (v, i, t) {
                const font = Chart.helpers.toFont(this.options.ticks.font);
                const maxLen = Math.round(Math.min(MaxCrowdedTickLength, Math.max(MinCrowdedTickLength, this.chart.width * 0.22 / (font.size * 0.6))));
                return shortenLabel(String(Chart.defaults.scales.category.ticks.callback.call(this, v, i, t) ?? ""), maxLen, !!(this.chart.data && this.chart.data.SwGrouped));
            };
            const vt = ensure(opt, "scales", "x", "ticks");
            vt.maxRotation = 0;
            vt.minRotation = 0;
            return;
        }
        const len = Math.max(200, (ia === "x" ? el.clientWidth : el.clientHeight) || 800);
        if (len / n >= 40)
            return;
        const ticks = ensure(sc, "ticks");
        //  Server set auto skip / callback: keep Chart.js' auto skip (old behavior), else pick the labeled categories here
        const managed = ticks.autoSkip === undefined && ticks.callback === undefined;
        ticks.autoSkip = ticks.autoSkip ?? !managed;
        ticks.autoSkipPadding = ticks.autoSkipPadding ?? 8;
        if (ia === "x" && ticks.maxRotation === undefined && ticks.minRotation === undefined) {
            ticks.minRotation = 90;
            ticks.maxRotation = 90;
        }
        if (!managed)
            return;
        /**
         * Category tick label (the first line, see setupChartJs) of the picked categories (pickLabeledCategories), shortened
         * to what the chart size allows (about 30 % of the chart height for vertical labels, of the width for horizontal
         * ones); null (no label, no grid line) for the other categories. The pick is made once per tick generation (i = 0).
         * @param {*} v Tick value (index).
         * @param {number} i Tick index.
         * @param {Array<object>} t The ticks.
         * @returns {?string} Text.
         */
        ticks.callback = function (v, i, t) {
            const count = t.length;
            let pick = this.SwPick;
            if (i === 0 || !pick || pick.Keep.length !== count) {
                const to = this.options.ticks;
                const font = Chart.helpers.toFont(to.font);
                const horizontal = this.isHorizontal();
                const room = Math.max(1, (horizontal ? this.width : this.height) - ValueAxisReserve);
                const step = Math.max(1, Math.ceil((font.lineHeight + (to.autoSkipPadding ?? 8)) * count / room));
                const across = horizontal ? this.chart.height : this.chart.width;
                const maxLen = Math.round(Math.min(MaxCrowdedTickLength, Math.max(MinCrowdedTickLength, across * 0.3 / (font.size * 0.6))));
                this.SwPick = pick = { Keep: pickLabeledCategories(this.chart, count, step), MaxLen: maxLen };
            }
            if (!pick.Keep[i])
                return null;
            return shortenLabel(String(Chart.defaults.scales.category.ticks.callback.call(this, v, i, t) ?? ""), pick.MaxLen, !!(this.chart.data && this.chart.data.SwGrouped));
        };
    }

    /**
     * Shorten a category label to at most max characters, keeping the most telling part (the bar tooltip has the full text):
     * a leading group ("API / ..."; shown by the bar color and the group legend, see applyGroupLegend) is dropped first, then a
     * path keeps its last segment ("…/GetMessages [POST]"), else the end is cut ("Long name…").
     * @param {string} s The label.
     * @param {number} max Maximum length (>= 4).
     * @param {boolean} [grouped] The chart has a group legend: the group is always dropped (all labels look alike).
     * @returns {string} The label or a shorter text.
     */
    function shortenLabel(s, max, grouped) {
        if (s.length <= max && !grouped)
            return s;
        const g = s.indexOf(" / ");
        if (g > 0 && g + 3 < s.length)
            s = s.substring(g + 3).trim();
        if (s.length <= max)
            return s;
        const p = s.lastIndexOf("/");
        if (p >= 0 && p + 1 < s.length) {
            const tail = s.substring(p + 1).trim();
            if (tail.length + 2 <= max)
                return "…/" + tail;
            s = tail;
        }
        return s.length > max ? s.substring(0, max - 1).trimEnd() + "…" : s;
    }

    /** @type {RegExp} A http method suffix of a code name label ("Api/auth/Login [POST]"). */
    const MethodSuffix = /\s*\[([A-Z]+)\]$/;

    /**
     * Split a code name into lower case words ("GetMessages" -> "Get messages", "ApiHttpEntry" -> "Api http entry", acronyms
     * like "HTML" are kept).
     * @param {string} s The code name.
     * @returns {string} The words.
     */
    function codeWords(s) {
        const words = s.replace(/[_.]+/g, " ")
            .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
            .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2")
            .split(/\s+/).filter(x => x);
        return words.map((w, i) => (i > 0 && !/^[A-Z0-9]{2,}$/.test(w)) ? w.toLowerCase() : w).join(" ");
    }

    /**
     * True if most category labels are code names (api paths, method / member names: "API / Api/application/GetMessages [POST]",
     * "HttpServer / Handle.ApiHttpEntry"): one word (after an optional "Group / " and a "[METHOD]" suffix) with a path, a dot or
     * camel case. Such labels get readable tick texts (readableLabels); the tooltip keeps the full name.
     * @param {Array} labels The labels.
     * @returns {boolean} True for code names.
     */
    function isCodeLabels(labels) {
        const n = labels.length;
        if (n < 2)
            return false;
        let code = 0, strong = 0;
        for (const l of labels) {
            let s = String(l ?? "").split("\n")[0].trim();
            const g = s.indexOf(" / ");
            if (g > 0)
                s = s.substring(g + 3);
            s = s.replace(MethodSuffix, "");
            if (!s || /\s/.test(s))
                continue;
            ++code;
            if (/[a-z][A-Z]|\//.test(s))
                ++strong;
        }
        return code >= n * 0.8 && strong >= n * 0.5;
    }

    /**
     * Readable tick texts of code name labels (isCodeLabels), cached on the chart data per label set: the group (shown by the group
     * legend) and the path are dropped and the last name is split into words, the http method is kept ("Get messages (POST)",
     * "Handle api http entry"). Names that would be equal get their parent path segment ("Chat: get messages (POST)").
     * @param {object} chart The Chart.js chart.
     * @returns {?Map<string, string>} Full label -> readable text, null for other labels.
     */
    function readableLabels(chart) {
        const d = chart.data || {};
        const labels = d.labels || [];
        const sig = labels.length + "\u0001" + labels.join("\u0001");
        if (d.SwReadableSig === sig)
            return d.SwReadable;
        d.SwReadableSig = sig;
        d.SwReadable = null;
        if (!isCodeLabels(labels))
            return null;
        const parts = labels.map(l => {
            let s = String(l ?? "").split("\n")[0].trim();
            const g = s.indexOf(" / ");
            if (g > 0)
                s = s.substring(g + 3);
            const m = MethodSuffix.exec(s);
            const method = m ? m[1] : "";
            s = s.replace(MethodSuffix, "");
            const segs = s.split("/").filter(x => x);
            const name = codeWords(segs.pop() || s);
            return { Name: name, Parent: segs.length ? codeWords(segs[segs.length - 1]) : "", Method: method };
        });
        const count = new Map();
        parts.forEach(p => count.set(p.Name + p.Method, (count.get(p.Name + p.Method) || 0) + 1));
        const map = new Map();
        labels.forEach((l, i) => {
            const p = parts[i];
            let t = p.Name;
            if (count.get(p.Name + p.Method) > 1 && p.Parent)
                t = p.Parent + ": " + (/^[A-Z][a-z]/.test(t) ? t.charAt(0).toLowerCase() + t.substring(1) : t);
            if (p.Method)
                t += " (" + p.Method + ")";
            map.set(String(l ?? ""), t);
        });
        d.SwReadable = map;
        return map;
    }

    /**
     * True for the method graph data url (Api/PerformanceChart: calls in progress / max concurrency per monitored function).
     * @param {string} url The data url.
     * @returns {boolean}
     */
    function isPerformanceChart(url) {
        return /PerformanceChart(\?|$)/.test(String(url || "").split("#")[0]);
    }

    /**
     * True on touch screens (a touch device or a coarse pointer): texts say "tap" instead of "point at".
     * @returns {boolean}
     */
    function isTouchScreen() {
        return !!((Sw.Platform && Sw.Platform.IsTouch) || (window.matchMedia && window.matchMedia("(pointer: coarse)").matches));
    }

    /**
     * The spread of the values of axis chart datasets (numbers, or points with y / x): the largest value, the smallest positive one and
     * the median of the positive ones; null without two positive values.
     * @param {Array<object>} datasets The datasets.
     * @param {string} va The value axis ("x" / "y").
     * @returns {?{Max: number, Min: number, Median: number}} The spread.
     */
    function valueSpread(datasets, va) {
        const vs = [];
        datasets.forEach(ds => (ds && Array.isArray(ds.data) ? ds.data : []).forEach(p => {
            const v = typeof p === "number" ? p : (p && typeof p === "object" ? +p[va] : NaN);
            if (Number.isFinite(v) && v > 0)
                vs.push(v);
        }));
        if (vs.length < 2)
            return null;
        vs.sort((a, b) => a - b);
        return { Max: vs[vs.length - 1], Min: vs[0], Median: vs[vs.length >> 1] };
    }

    /**
     * Axis titles of an axis chart (ChartOptions.AxisTitles, the chart page) where the server config has none, so the numbers and
     * names say what they are: the value axis gets the series names (and the unit, ex: "Cpu use (%)"), the category axis "Time"
     * for time labels or "Name" for code names (point at a bar for the full name). The performance chart (Api/PerformanceChart:
     * calls in progress / max concurrency per monitored function) gets fixed titles; on low screens (phone landscape, below 500 px) its
     * vertical axis gets a short title, as the long one was cut ("Number of calls at the same tir").
     * @param {object} data The render config (options.scales ensured).
     * @param {string} url The data url.
     * @param {ValueFmt} f The value formatter (unit suffix).
     */
    function applyAxisTitles(data, url, f) {
        const opt = data.options;
        const ia = opt.indexAxis === "y" ? "y" : "x";
        const va = ia === "x" ? "y" : "x";
        const dd = data.data;
        const labels = dd.labels || [];
        let cat = null, val = null;
        //  Touch screens can't point: they say "tap" (a tap on a bar shows its tool tip with the full name)
        const touch = isTouchScreen();
        if (isPerformanceChart(url)) {
            const low = window.innerHeight < 500;
            cat = (low && ia === "y")
                ? (touch
                    ? _TF("Function (tap a bar)", "Short title of the category axis of the method graph (performance chart) on low touch screens (phones in landscape): a tap on a bar shows its full name")
                    : _TF("Monitored function", "Short title of the category axis of the method graph (performance chart) on low screens"))
                : touch
                    ? _TF("Monitored function (tap a bar for details)", "Title of the category axis of the method graph (performance chart) on touch screens: one bar per monitored function, a tap shows its details")
                    : _TF("Monitored function (hover over a bar for details)", "Title of the category axis of the method graph (performance chart) with a mouse: one bar per monitored function, its tool tip shows its details");
            val = (low && va === "y")
                ? _TF("Calls at once", "Short title of the value axis of the method graph (performance chart) on low screens: the number of calls running at the same time")
                : _TF("Number of calls at the same time", "Title of the value axis of the method graph (performance chart): calls in progress now and the most calls in progress at once");
        }
        else {
            if (labels.length > 1 && labels.every(l => TimeLabelPatterns.some(p => p.test(String(l ?? "").trim()))))
                cat = _TF("Time", "Title of the category axis of a chart whose labels are times");
            else if (isCodeLabels(labels))
                cat = touch
                    ? _TF("Name (tap a bar for the full name)", "Title of the category axis of a chart whose labels are code names (shown shortened), on touch screens")
                    : _TF("Name (point at a bar for the full name)", "Title of the category axis of a chart whose labels are code names (shown shortened)");
            const names = dd.datasets.map(ds => ds && ds.label).filter(x => x);
            if (names.length > 0 && names.length <= 3)
                val = names.join(" / ");
            const unit = String(f.Suffix || "").trim();
            if (val && unit)
                val += " (" + unit + ")";
        }
        /**
         * Set a title on a scale unless the server set one.
         * @param {string} axis Scale id.
         * @param {?string} text Title.
         */
        const set = (axis, text) => {
            const sc = ensure(opt, "scales", axis);
            if (!text || sc.display === false || (sc.title && sc.title.text))
                return;
            sc.title = Object.assign({}, sc.title, { display: true, text, font: Object.assign({ weight: "bold" }, sc.title && sc.title.font) });
        };
        set(ia, cat);
        set(va, val);
    }

    /**
     * Filled bars of the method graph (performance chart): the server draws the "Max concurrency" bars as outlines with a 10 % fill
     * ("rgba(r,g,b,0.1)"), so most bars looked hollow and unfinished. Their fill gets 35 % alpha (a light, visible bar in every theme;
     * 55 % on dark schemes (OWN4-Chart-3: a 35 % tint over the dark slate surface read as a muddy grey);
     * still translucent, so the opaque "In progress" part stays the dark one and the group legend keeps showing the series as light).
     * Idempotent (only fills below 30 % change).
     * @param {object} data The render config (mutated).
     */
    function fillPerformanceBars(data) {
        const tint = document.documentElement.getAttribute("data-sw-scheme") === "dark" ? "0.55" : "0.35";
        const lift = c => {
            const m = typeof c === "string" ? /^rgba\(([^,]+),([^,]+),([^,]+),\s*(0?\.\d+|0)\s*\)$/.exec(c) : null;
            return m && +m[4] < 0.3 ? "rgba(" + m[1] + "," + m[2] + "," + m[3] + "," + tint + ")" : c;
        };
        data.data.datasets.forEach(ds => {
            if (Array.isArray(ds.backgroundColor))
                ds.backgroundColor = ds.backgroundColor.map(lift);
            else if (ds.backgroundColor)
                ds.backgroundColor = lift(ds.backgroundColor);
        });
    }

    /**
     * Group legend of axis charts whose bars are colored by group: when most labels are "Group / Name" (ex: the performance chart,
     * where the server picks the hue by the system and the shade by the name) and the bars of different groups have different
     * colors, the legend lists the groups with their color (in addition to the series, which are then drawn in a neutral color:
     * filled or outlined like their bars), so every bar color is explained. Group items can not be clicked (series still toggle).
     * Nothing changes for other charts. Only the groups of the shown bars are listed (maxCount: the first n after ordering), and
     * groups that share a color (stableSlot) share one entry ("API · HttpServer").
     * @param {object} data The render config (after the colors are final).
     * @param {?number} [maxCount] The number of bars shown (ChartOptions.MaxCount), null for all.
     */
    function applyGroupLegend(data, maxCount) {
        const dd = data.data;
        const labels = dd.labels || [];
        const n = labels.length;
        const ds0 = dd.datasets[0];
        if (n < 2 || !ds0)
            return;
        //  The colors per bar: an opaque color array (border if the fill is translucent)
        const pickCols = ds => {
            for (const k of ["backgroundColor", "borderColor"]) {
                const c = ds[k];
                if (Array.isArray(c) && c.length === n && typeof c[0] === "string" && !/^rgba\([^)]*,\s*0?\.\d+\s*\)$/.test(c[0]))
                    return c;
            }
            return null;
        };
        let cols = null;
        for (const ds of dd.datasets)
            if ((cols = pickCols(ds)))
                break;
        if (!cols)
            return;
        const groups = new Map();
        let grouped = 0;
        labels.forEach((l, i) => {
            const s = String(l ?? "");
            const p = s.indexOf(" / ");
            if (p <= 0)
                return;
            ++grouped;
            const g = s.substring(0, p).trim();
            if (!groups.has(g) && !(maxCount > 0 && i >= maxCount))
                groups.set(g, cols[i]);
        });
        if (grouped < n * 0.8 || groups.size < 2 || groups.size > 16 || new Set(groups.values()).size < 2)
            return;
        //  Theme colors: list the groups in palette slot order (slot 1 first), not in the order of the sorted bars, so the legend
        //  shows --sw-chart-1, --sw-chart-2 ... in order and a color does not move when the bars are re-sorted
        const gs = dd.SwGroupSlots;
        if (gs && gs.size) {
            const sorted = Array.from(groups).sort((a, b) => (gs.has(a[0]) ? gs.get(a[0]) : 1e9) - (gs.has(b[0]) ? gs.get(b[0]) : 1e9));
            groups.clear();
            sorted.forEach(([g, c]) => groups.set(g, c));
        }
        const opt = data.options;
        const lg = ensure(opt, "plugins", "legend");
        //  The legend was turned off (url legend=false sets the default; a server config may hide it for several series)
        if (Chart.defaults.plugins.legend.display === false || (lg.display === false && dd.datasets.length > 1))
            return;
        lg.display = true;
        /** @type {boolean} Group legend shown: the tick labels drop the group (shortenLabel). */
        dd.SwGrouped = true;
        const neutral = "rgba(128,128,128,0.85)";
        /**
         * Legend items: the series, then the groups (their bar color). A series swatch is grey, drawn like that series' bars: solid for
         * a solid series ("In progress"), a light tint with an outline for a translucent one ("Max concurrency"). Every color in the
         * legend then means a group, and the grey swatches say which part of a bar is which series (S7a-D9: a swatch copied from the
         * first group's bar gave "In progress" the HttpServer color, so a color meant a state and a module at once).
         * @param {object} chart The chart.
         * @returns {Array<object>} Items.
         */
        ensure(lg, "labels").generateLabels = chart => {
            const first = groups.keys().next().value;
            const at = first === undefined ? -1 : (chart.data.labels || []).findIndex(l => String(l ?? "").startsWith(first + " / "));
            const pick = v => Array.isArray(v) ? v[at] : v;
            const tinted = c => typeof c === "string" && /^rgba\([^)]*,\s*0?\.\d+\s*\)$/.test(c);
            const items = dd.datasets.length > 1 ? Chart.defaults.plugins.legend.labels.generateLabels(chart).map(it => {
                const ds = chart.data.datasets[it.datasetIndex];
                const fill = ds && at >= 0 ? pick(ds.backgroundColor) : it.fillStyle;
                const bw = ds && typeof ds.borderWidth === "number" ? ds.borderWidth : 0;
                if (tinted(fill))
                    return Object.assign(it, { fillStyle: "rgba(128,128,128,0.3)", strokeStyle: neutral, lineWidth: Math.max(1, Math.min(bw, 2)) });
                return Object.assign(it, { fillStyle: neutral, strokeStyle: neutral, lineWidth: bw > 0 ? Math.min(bw, 2) : 0 });
            }) : [];
            //  groups with the same color (a shared stable slot) share one entry
            const byColor = new Map();
            groups.forEach((c, g) => byColor.has(c) ? byColor.get(c).push(g) : byColor.set(c, [g]));
            byColor.forEach((gl, c) => items.push({ text: gl.join(" · "), fillStyle: c, strokeStyle: c, lineWidth: 1, hidden: false, SwGroup: true }));
            return items;
        };
        /**
         * Legend click: series toggle as usual, group items do nothing.
         * @param {Event} e The event.
         * @param {object} item The item.
         * @param {object} legend The legend.
         */
        lg.onClick = (e, item, legend) => {
            if (item && !item.SwGroup)
                Chart.defaults.plugins.legend.onClick.call(legend, e, item, legend);
        };
    }

    // ================================================================== color schemes

    /**
     * Apply a forced color scheme to all datasets (border = opaque, background = translucent):
     * RandomValueGradient (one hue sweep over all values), RandomMagnitudeGradient (hue by value), RandomSeriesGradient (one color
     * per series), LabelText (color from the label text), FixedValue<i>, FixedSeries<i>, FixedMag<i> (hues i/12 .. (i+2)/12).
     * @param {object} data The config (mutated).
     * @param {string} mode The mode.
     * @param {number[]} seed Seed of the random modes.
     */
    function applyColorScheme(data, mode, seed) {
        const dss = data.data.datasets;
        const labels = data.data.labels || [];
        const sc = dss.length, vc = labels.length;
        /**
         * Set per point colors of every dataset.
         * @param {function(number, number):number[]} colorAt (series, value index) -> rgb.
         * @param {number} op Background opacity.
         */
        const perPoint = (colorAt, op) => dss.forEach((ds, si) => {
            const border = [], bg = [];
            for (let vi = 0; vi < vc; ++vi) {
                const c = colorAt(si, vi);
                border[vi] = rgb(c);
                bg[vi] = rgba(c, op);
            }
            ds.borderColor = border;
            ds.backgroundColor = bg;
        });
        /**
         * A value of a dataset (0 for missing values).
         * @param {number} si Series.
         * @param {number} vi Value index.
         * @returns {number} The value.
         */
        const val = (si, vi) => {
            const v = sortValue((dss[si].data || [])[vi]);
            return v === -Infinity ? 0 : v;
        };
        /**
         * Value range over all datasets.
         * @returns {{Min: number, Range: number}} Minimum and max - min.
         */
        const range = () => {
            let min = Infinity, max = -Infinity;
            for (let si = 0; si < sc; ++si)
                for (let vi = 0; vi < vc; ++vi) {
                    const v = val(si, vi);
                    min = Math.min(min, v);
                    max = Math.max(max, v);
                }
            return min > max ? { Min: 0, Range: 0 } : { Min: min, Range: max - min };
        };

        if (mode === ColorMode.RandomValue || mode === ColorMode.RandomMagnitude) {
            const r = Rng.Create(seed);
            const hueRange = (r() * (sc - 0.3) + 0.3) * (r() >= 0.5 ? 1 : -1);
            const hue = r(), sat = r() * 0.5 + 0.5, light = r() * 0.3 + 0.5;
            const op = r() * 0.45 + 0.5;
            if (mode === ColorMode.RandomValue) {
                const step = hueRange / Math.max(1, sc * vc);
                perPoint((si, vi) => hslToRgb(hue + (si * vc + vi) * step, sat, light), op);
            }
            else {
                const rg = range();
                const scale = rg.Range > 0 ? hueRange / rg.Range : 0;
                perPoint((si, vi) => hslToRgb(hue + (val(si, vi) - rg.Min) * scale, sat, light), op);
            }
            return;
        }
        if (mode === ColorMode.RandomSeries) {
            const r = Rng.Create(seed);
            const hueRange = r() * 0.7 + 0.3;
            const hue = r(), sat = r() * 0.5 + 0.5, light = r() * 0.3 + 0.5;
            let step = hueRange / Math.max(1, sc);
            if (r() >= 0.5)
                step = -step;
            const op = r() * 0.45 + 0.5;
            dss.forEach((ds, si) => {
                const c = hslToRgb(hue + si * step, sat, light);
                ds.borderColor = rgb(c);
                ds.backgroundColor = rgba(c, op);
            });
            return;
        }
        if (mode === ColorMode.LabelText) {
            perPoint((si, vi) => {
                const scale = 1 - (si * 0.4) / sc;
                const r = Rng.Create(String(labels[vi] ?? ""));
                const hue = r();
                const sat = (r() * 0.7 + 0.3) * scale;
                const light = (r() * 0.5 + 0.5) * scale;
                return hslToRgb(hue, sat, light);
            }, 0.7);
            return;
        }
        const m = /^(FixedValue|FixedSeries|FixedMag)(\d+)$/.exec(mode || "");
        if (!m)
            return;
        const start = parseInt(m[2]) / 12;
        const span = 2 / 12;
        switch (m[1]) {
            case "FixedValue": {
                const ss = 0.3 / (sc > 1 ? sc - 1 : 1);
                const vs = span / (vc > 1 ? vc - 1 : 1);
                perPoint((si, vi) => hslToRgb(start + vi * vs, 0.8, 0.7 - si * ss), 0.6);
                break;
            }
            case "FixedSeries": {
                const ss = span / (sc > 1 ? sc - 1 : 1);
                dss.forEach((ds, si) => {
                    const c = hslToRgb(start + si * ss, 0.8, 0.7);
                    ds.borderColor = rgb(c);
                    ds.backgroundColor = rgba(c, 0.6);
                });
                break;
            }
            default: {
                const rg = range();
                const scale = rg.Range > 0 ? span / rg.Range : 0;
                perPoint((si, vi) => hslToRgb(start + (val(si, vi) - rg.Min) * scale, 0.8, 0.7), 0.6);
                break;
            }
        }
    }

    // ================================================================== Chart.js setup

    /** @type {string} Theme background color (--ThemeBackground), painted behind exports by the background plugin. */
    let themeBackground = "#000";

    /**
     * Chart.js plugin "customCanvasBackgroundColor": applies the export font scale (canvas.Scale) and paints the background
     * (options.color or the theme background) unless the chart has the expando Transparent (the live chart always has it;
     * its container provides the background).
     */
    const BackgroundPlugin = {
        id: "customCanvasBackgroundColor",
        /**
         * Layout padding: 4, or enlarged right / bottom so the drawing scaled by canvas.Scale fills the canvas.
         * @param {object} chart The chart.
         */
        beforeLayout(chart) {
            const s = chart.ctx.canvas.Scale;
            const layout = ensure(chart.config.options, "layout");
            if (s && s !== 1) {
                const w = chart.width, h = chart.height;
                layout.padding = { top: 4, left: 4, right: w - ((w - 8) / s) + 4, bottom: h - ((h - 8) / s) + 4 };
            }
            else
                layout.padding = 4;
        },
        /**
         * Save + scale the context, fill the background (destination-over) unless transparent.
         * @param {object} chart The chart.
         * @param {object} args Hook arguments.
         * @param {object} options Plugin options (color).
         */
        beforeDraw(chart, args, options) {
            const ctx = chart.ctx;
            ctx.save();
            let s = ctx.canvas.Scale;
            if (s && s !== 1)
                ctx.scale(s, s);
            else
                s = 1;
            if (chart.Transparent)
                return;
            ctx.save();
            ctx.globalCompositeOperation = "destination-over";
            ctx.fillStyle = options.color || themeBackground;
            ctx.fillRect(0, 0, chart.width / s, chart.height / s);
            ctx.restore();
        },
        /**
         * Restore the context saved in beforeDraw.
         * @param {object} chart The chart.
         */
        afterDraw(chart) {
            chart.ctx.restore();
        },
    };

    /**
     * Chart.js plugin "dropShadow": options.plugins.shadow {dx, dy, rad, color (default rgba(0,0,0,0.5))} draws datasets with a
     * shadow and enlarges the layout padding so it fits.
     */
    const ShadowPlugin = {
        id: "dropShadow",
        /**
         * Raise the layout padding to max(|dx|, |dy|) + rad.
         * @param {object} chart The chart.
         */
        beforeLayout(chart) {
            const so = chart.config?.options?.plugins?.shadow;
            if (!so)
                return;
            const pd = Math.max(Math.abs(so.dx || 0), Math.abs(so.dy || 0)) + Math.max(0, so.rad || 0);
            const layout = ensure(chart.config.options, "layout");
            const p = layout.padding;
            if (!p || typeof p === "number")
                layout.padding = Math.max(p || 0, pd);
            else
                ["top", "right", "bottom", "left"].forEach(k => {
                    if (!(p[k] >= pd))
                        p[k] = pd;
                });
        },
        /**
         * Set the shadow before a dataset is drawn.
         * @param {object} chart The chart.
         */
        beforeDatasetDraw(chart) {
            const so = chart.config?.options?.plugins?.shadow;
            if (!so)
                return;
            const ctx = chart.ctx;
            ctx.save();
            ctx.shadowColor = so.color || "rgba(0,0,0,0.5)";
            ctx.shadowOffsetX = so.dx || 0;
            ctx.shadowOffsetY = so.dy || 0;
            ctx.shadowBlur = Math.max(0, so.rad || 0);
        },
        /**
         * Restore the context after a dataset was drawn.
         * @param {object} chart The chart.
         */
        afterDatasetDraw(chart) {
            if (chart.config?.options?.plugins?.shadow)
                chart.ctx.restore();
        },
    };

    /**
     * Chart.js plugin "swCrosshair" (Design v2): a 1 px hairline through the hovered index of line / time series charts
     * (options.plugins.swCrosshair {enabled, color}; vertical for a horizontal index axis, else horizontal).
     */
    const CrosshairPlugin = {
        id: "swCrosshair",
        defaults: { enabled: false, color: "rgba(128,128,128,0.5)" },
        /**
         * Draw the hairline after the datasets (under the tooltip).
         * @param {object} chart The chart.
         * @param {object} args Hook arguments.
         * @param {object} options Plugin options.
         */
        afterDatasetsDraw(chart, args, options) {
            if (!options || !options.enabled || !chart.tooltip || !chart.chartArea)
                return;
            const act = chart.tooltip.getActiveElements ? chart.tooltip.getActiveElements() : [];
            if (!act.length || !act[0].element)
                return;
            const a = chart.chartArea;
            const ctx = chart.ctx;
            const y = chart.options.indexAxis === "y";
            const p = Math.round(y ? act[0].element.y : act[0].element.x) + 0.5;
            ctx.save();
            ctx.strokeStyle = options.color;
            ctx.lineWidth = 1;
            ctx.beginPath();
            if (y) {
                ctx.moveTo(a.left, p);
                ctx.lineTo(a.right, p);
            }
            else {
                ctx.moveTo(p, a.top);
                ctx.lineTo(p, a.bottom);
            }
            ctx.stroke();
            ctx.restore();
        },
    };

    /** @type {?HTMLElement} Probe element used to resolve css tokens (color-mix values) to colors. */
    let tokenProbe = null;

    /**
     * Resolve a css color token (ex: --sw-chart-grid, a color-mix value) to an "rgba(r,g,b,a)" canvas color.
     * @param {string} name The custom property.
     * @param {string} fallback Css color used when the token is missing or can not be resolved.
     * @returns {string} The color.
     */
    function tokenColor(name, fallback) {
        if (!document.body)
            return fallback;
        if (!tokenProbe || !tokenProbe.isConnected) {
            tokenProbe = document.createElement("span");
            tokenProbe.setAttribute("aria-hidden", "true");
            //  No transition (important: wins over any style sheet rule): the color is read right after it is set, a transition would
            //  give the previous (inherited, grey) color for that read (S7a-B-1)
            tokenProbe.style.cssText = "position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;transition:none !important";
            document.body.append(tokenProbe);
        }
        tokenProbe.style.color = "";
        tokenProbe.style.color = "var(" + name + ", " + fallback + ")";
        const css = getComputedStyle(tokenProbe).color;
        if (!css)
            return fallback;
        const c = cssToRgba(css);
        return rgba(c, Math.round(c[3] / 255 * 1000) / 1000);
    }

    /** @type {{Grid: string, Axis: string, Text: string, Text2: string, Surface: string, Raised: string, Border: string}} Design v2 chart colors (applyThemeDefaults). */
    const DesignColors = { Grid: "rgba(128,128,128,0.15)", Axis: "#888", Text: "#000", Text2: "#444", Surface: "#fff", Raised: "#fff", Border: "#ccc" };

    /**
     * A plain color of a color value: a css color as is, the first stop of a gradient string ("up(0;#CC4343;1;#EDDD53)"), the first
     * entry of an array; null for anything else (scriptable functions, canvas gradients).
     * @param {*} v The color value.
     * @returns {?string} The color.
     */
    function solidOf(v) {
        if (Array.isArray(v))
            return v.length ? solidOf(v[0]) : null;
        if (typeof v !== "string")
            return null;
        const open = v.indexOf("(");
        if (open > 0 && GradientFns.get(v.substring(0, open).trim())) {
            const parts = v.substring(open + 1, v.length - 1).split(";").map(x => x.trim());
            return parts.find(x => x && isNaN(parseFloat(x)) || /^#|^rgb|^hsl/i.test(x)) || null;
        }
        return v;
    }

    /**
     * True when (at least 90 % of) the labels are times or dates ("HH:mm", "HH:mm:ss", "MM-dd HH:mm", "yyyy-MM-dd ..."): a time series
     * axis (horizontal labels, a few evenly spaced ticks, line rendering looks right).
     * @param {Array} labels The labels.
     * @returns {boolean}
     */
    function isTimeLabels(labels) {
        if (!labels || labels.length < 2)
            return false;
        let n = 0;
        for (const l of labels) {
            const s = String(Array.isArray(l) ? l[0] : (l ?? "")).trim();
            if (TimeLabelPatterns.some(r => r.test(s)) || /^\d{4}-\d{2}-\d{2}([ T]\d{1,2}:\d{2}(:\d{2})?)?$/.test(s) || /^\d{2}-\d{2}$/.test(s))
                ++n;
        }
        return n >= labels.length * 0.9;
    }

    /**
     * Design v2 look of a render config (Design v2, PORTING.md B11), applied after the colors were chosen and before they are resolved:
     *  - lines: 2 px, no points (a 4 px point with a 2 px surface ring on hover), tension 0.2, an area fill fading from 35 % to 12 % of the series color for a single series (a 3 px point while the line has fewer than two points);
     *  - bars: 4 px rounded data end (not for stacked bars);
     *  - doughnuts: a thinner flat ring (cutout 68 % unless the server sets one);
     *  - axis charts: horizontal hairline grid only (the index axis has no grid), no value axis line, at most 5 value ticks, a value
     *    axis from 0 for percentages, index tooltips with a crosshair for lines and time series.
     * The server drop shadow (options.plugins.shadow) is dropped unless keepShadow (url shadow=true).
     * @param {object} data The render config (options, plugins ensured).
     * @param {ValueFmt} f The value formatter.
     * @param {boolean} keepShadow Keep the server shadow.
     * @param {boolean} logScale The value axis is logarithmic.
     */
    function applyDesign(data, f, keepShadow, logScale) {
        const opt = data.options;
        const plugins = opt.plugins;
        if (!keepShadow)
            delete plugins.shadow;
        const type = data.type;
        const dss = data.data.datasets;
        if (type === "doughnut" && opt.cutout === undefined)
            opt.cutout = "68%";
        if (NoAxisTypes.has(type))
            return;
        const ia = opt.indexAxis === "y" ? "y" : "x";
        const va = ia === "x" ? "y" : "x";
        const scales = ensure(opt, "scales");
        const stacked = !!((scales.x && scales.x.stacked) || (scales.y && scales.y.stacked));
        let lines = 0;
        dss.forEach(ds => {
            if (!ds)
                return;
            const t = ds.type || type;
            if (t === "line") {
                ++lines;
                const c = solidOf(ds.borderColor) || solidOf(ds.backgroundColor);
                if (c)
                    ds.borderColor = c;
                ds.borderWidth = 2;
                ds.tension = ds.tension ?? 0.2;
                ds.pointRadius = 0;
                ds.pointHitRadius = 10;
                ds.pointHoverRadius = 4;
                ds.pointHoverBorderWidth = 2;
                ds.pointHoverBorderColor = DesignColors.Surface;
                if (c)
                    ds.pointHoverBackgroundColor = ds.pointBackgroundColor = c;
                delete ds.borderRadius;
                delete ds.borderSkipped;
                //  A line with fewer than two points draws no line: show its point (a fresh history has one sample)
                if (!Array.isArray(ds.data) || ds.data.length < 2)
                    ds.pointRadius = 3;
                if (dss.length === 1 && c) {
                    //  OWN4-Chart-2: the area is a vertical fade of the series color over the plot (35 % at the top of the plot, 12 % at
                    //  the axis); a flat 10 % tint read as a neutral grey on the dark themes (copper at 10 % over slate is grey)
                    const x = cssToRgba(c);
                    const a = f => Math.round(x[3] / 255 * f * 1000) / 1000;
                    const top = rgba(x, a(0.35)), bottom = rgba(x, a(0.12)), flat = rgba(x, a(0.18));
                    ds.backgroundColor = sc => {
                        const ch = sc.chart, area = ch.chartArea, ctx = ch.ctx;
                        if (!area || !(area.bottom > area.top) || typeof CanvasRenderingContext2D === "undefined" || !(ctx instanceof CanvasRenderingContext2D))
                            return flat;
                        const g = ctx.createLinearGradient(0, area.top, 0, area.bottom);
                        g.addColorStop(0, top);
                        g.addColorStop(1, bottom);
                        return g;
                    };
                    ds.fill = ds.fill ?? "origin";
                }
                else {
                    if (c)
                        ds.backgroundColor = c;
                    ds.fill = ds.fill ?? false;
                }
            }
            else if (t === "bar") {
                //  flat bars (no outline; the fill tells the series), 4 px rounded data end when not stacked
                ds.borderWidth = 0;
                if (!stacked) {
                    ds.borderRadius = 4;
                    ds.borderSkipped = "start";
                }
            }
        });
        //  the chart title and the legend are left aligned like a card head (Chart.js centers them)
        plugins.title = Object.assign({ align: "start" }, plugins.title);
        plugins.legend = Object.assign({ align: "start" }, plugins.legend);
        const timeAxis = isTimeLabels(data.data.labels);
        //  Grid: hairlines across the value axis only; no axis lines
        [ia, va].forEach(k => {
            const s = ensure(scales, k);
            s.grid = Object.assign(k === ia ? { display: false } : { color: DesignColors.Grid, drawTicks: false }, s.grid);
            s.border = Object.assign({ display: false }, s.border);
            const tk = ensure(s, "ticks");
            if (k === va) {
                tk.padding = tk.padding ?? 8;
                tk.maxTicksLimit = tk.maxTicksLimit ?? 5;
            }
            else
                tk.padding = tk.padding ?? 6;
        });
        //  Percentages: the value axis starts at 0
        if (!logScale && String(f.Suffix || "").trim() === "%") {
            const s = scales[va];
            s.beginAtZero = true;
            if (s.min === undefined)
                s.min = 0;
        }
        //  Lines and time series: the tooltip lists every series at the hovered index, with a crosshair
        if (lines || timeAxis) {
            opt.interaction = Object.assign({ mode: "index", intersect: false, axis: ia }, opt.interaction);
            plugins.swCrosshair = Object.assign({ enabled: true, color: DesignColors.Axis }, plugins.swCrosshair);
        }
    }

    /** @type {boolean} True once setupChartJs ran. */
    let chartJsReady = false;

    /**
     * Register the plugins and install the shared formatters in Chart.defaults (once per page).
     */
    function setupChartJs() {
        if (chartJsReady)
            return;
        chartJsReady = true;
        Chart.register(BackgroundPlugin, ShadowPlugin, CrosshairPlugin, ChartDataLabels);
        const def = Chart.defaults;
        def.plugins.datalabels = {
            display: "auto",
            align: "start",
            anchor: "end",
            font: { size: 11, weight: "bold" },
            borderRadius: 8,
            padding: 4,
            formatter: (v, c) => formatValue(v, fmtOf(c.chart)),
        };
        /**
         * Numeric tick label.
         * @param {*} v Tick value.
         * @returns {string} Text.
         */
        const tick = function (v) {
            return formatValue(v, fmtOf(this.chart));
        };
        def.scale.ticks.callback = tick;
        ["linear", "logarithmic", "radialLinear", "time", "timeseries"].forEach(k => {
            if (def.scales[k])
                def.scales[k].ticks.callback = tick;
        });
        /**
         * Category tick label: the first line of the label; code names (api paths, member names) as readable words
         * (readableLabels; the tooltip title keeps the full name).
         * @param {*} v Tick value (index).
         * @returns {string} Text.
         */
        def.scales.category.ticks.callback = function (v) {
            const t = String(this.getLabelForValue(v) ?? "");
            const map = readableLabels(this.chart);
            const r = map && map.get(t);
            if (r)
                return r;
            const i = t.indexOf("\n");
            return i >= 0 ? t.substring(0, i).trim() : t;
        };
        /**
         * Tooltip label: "<dataset label>: <formatted value>".
         * @param {object} c Tooltip item context.
         * @returns {string} Text.
         */
        def.plugins.tooltip.callbacks.label = c => {
            const l = c.dataset.label || "";
            return (l ? l + ": " : "") + formatValue(c.raw, fmtOf(c.chart));
        };
    }

    /** @type {number} The font scale in Chart.defaults (setFontScale; the --sw-scale step of the screen for live charts). */
    let fontScale = 1;

    /**
     * The large screen step of the ui size tokens (--sw-scale: 1, 1.2 at 2560 px, 1.45 at 3440 px and more, ui/tokens.css).
     * The step needs a wide window, so a chart in a small frame (ex: the server page tiles) uses the largest step of its same site
     * parent windows (the page around it).
     * @returns {number} The scale (1 if not set).
     */
    function screenScale() {
        const read = w => parseFloat(w.getComputedStyle(w.document.documentElement).getPropertyValue("--sw-scale")) || 1;
        let s = read(window);
        try {
            for (let w = window; w.parent && w.parent !== w;) {
                w = w.parent;
                s = Math.max(s, read(w));
            }
        }
        catch (e) {
            //  A cross site parent: the steps found so far
        }
        return s > 0 ? s : 1;
    }

    /**
     * Set the Chart.defaults text sizes for a scale: text (ticks, legend) 14 px, titles 16 px (semi bold), value labels and
     * tool tips 12 / 14 px, all times the scale (whole px). Live charts use the screen scale, so their text grows with the
     * rest of the page on large screens; image exports use 1 (their own "Export font size" applies on top).
     * @param {number} s The scale.
     */
    function setFontScale(s) {
        fontScale = s;
        const def = Chart.defaults;
        const px = n => Math.round(n * s);
        def.font.size = px(14);
        def.plugins.datalabels.font.size = px(12);
        def.plugins.title.font = Object.assign({}, def.plugins.title.font, { size: px(16), weight: "600" });
        def.plugins.tooltip.titleFont = Object.assign({}, def.plugins.tooltip.titleFont, { size: px(14) });
        def.plugins.tooltip.bodyFont = Object.assign({}, def.plugins.tooltip.bodyFont, { size: px(13) });
        //  Design v2: axis labels 12 px (times the scale)
        def.scale.ticks.font = Object.assign({}, def.scale.ticks.font, { size: px(12) });
    }

    /**
     * The theme state the chart colors depend on: the theme attributes of html and the computed theme tokens (base colors, roles and
     * the series palette) as one string; two equal signatures give the same colors (ChartView.Theme, OWN4-Chart-1).
     * @returns {string} The signature.
     */
    function themeSignature() {
        const de = document.documentElement;
        const cs = getComputedStyle(de);
        const bs = document.body ? getComputedStyle(document.body) : cs;
        const names = ["--ThemeBackground", "--ThemeMain", "--ThemeAcc1", "--ThemeAcc2", "--ThemeMainRGB", "--sw-bg", "--sw-fg", "--sw-accent1",
            "--sw-accent2", "--sw-text", "--sw-text-2", "--sw-surface", "--sw-surface-raised", "--sw-border", "--sw-chart-grid"];
        for (let i = 1; i <= 12; ++i)
            names.push("--sw-chart-" + i);
        return [de.getAttribute("data-theme"), de.getAttribute("data-sw-scheme")]
            .concat(names.map(n => bs.getPropertyValue(n).trim() + "|" + cs.getPropertyValue(n).trim())).join(";");
    }

    /**
     * Copy the theme colors into Chart.defaults (font, colors, datalabels background / color) and the text sizes for the
     * screen (setFontScale(screenScale())).
     */
    function applyThemeDefaults() {
        const st = getComputedStyle(document.body);
        const v = n => st.getPropertyValue(n).trim();
        const def = Chart.defaults;
        themeBackground = v("--ThemeBackground") || "#000";
        def.font.family = v("--ThemeFont") || v("--sw-sans") || def.font.family;
        //  Motion from the theme (ui/tokens.css: --sw-dur-* times the theme's --sw-motion scale; reduced motion 1 ms): value changes
        //  animate over --sw-dur-3, tool tips over --sw-dur-2 (Chart.js: 1 s and 400 ms), unless the server's chart config sets its own
        const ms = (n, d) => {
            const m = /^(-?[\d.]+)(ms|s)$/.exec(v(n));
            return m ? Math.max(0, Math.round(parseFloat(m[1]) * (m[2] === "s" ? 1000 : 1))) : d;
        };
        def.animation.duration = ms("--sw-dur-3", 150);
        def.plugins.tooltip.animation.duration = ms("--sw-dur-2", 100);
        setFontScale(screenScale());
        def.borderColor = "rgba(" + v("--ThemeMainRGB") + ",0.07)";
        def.elements.arc.borderColor = themeBackground;
        def.elements.arc.borderWidth = 1;
        def.plugins.datalabels.backgroundColor = "rgba(" + v("--ThemeBackgroundRGB") + ",0.65)";
        //  Design v2 (PORTING.md B11): hairline grid, axis text (text-2), tooltips on the raised surface with a 1 px border
        const main = v("--ThemeMainRGB") || "128,128,128";
        const dc = DesignColors;
        dc.Grid = tokenColor("--sw-chart-grid", "rgba(" + main + ",0.1)");
        dc.Text = tokenColor("--sw-text", "rgb(" + main + ")");
        dc.Text2 = tokenColor("--sw-text-2", "rgba(" + main + ",0.75)");
        dc.Surface = tokenColor("--sw-surface", themeBackground);
        dc.Raised = tokenColor("--sw-surface-raised", themeBackground);
        dc.Border = tokenColor("--sw-border", "rgba(" + main + ",0.15)");
        //  Text contrast (OWN3-Chart-2): every text color has at least 4.5:1 (WCAG AA) on what it is drawn on, in every theme and app
        //  color set: axis text, legend and titles on the page, the card surface and the raised surface (a chart is drawn on any of them),
        //  tooltip text on the tooltip; the tooltip border has 3:1 on the page so the tooltip stands out from the chart
        const page = overColor(themeBackground, [0, 0, 0]);
        const bgs = [page, overColor(dc.Surface, page), overColor(dc.Raised, page)];
        const tipBg = [overColor(dc.Raised, page)];
        dc.Axis = readableOn(dc.Text2, bgs, 4.5);
        def.color = readableOn(v("--ThemeMain") || dc.Text, bgs, 4.5);
        def.plugins.datalabels.color = readableOn(v("--ThemeAcc1") || def.color, [page], 4.5);
        def.borderColor = dc.Grid;
        def.scale.ticks.color = dc.Axis;
        def.scale.grid.color = dc.Grid;
        def.scale.border.color = dc.Grid;
        const tip = def.plugins.tooltip;
        tip.backgroundColor = rgb(tipBg[0]);
        tip.titleColor = readableOn(dc.Text, tipBg, 4.5);
        tip.bodyColor = tip.titleColor;
        tip.footerColor = readableOn(dc.Text2, tipBg, 4.5);
        tip.borderColor = readableOn(dc.Border, [page], 3);
        tip.borderWidth = 1;
        tip.cornerRadius = 6;
        tip.padding = 8;
        tip.boxPadding = 4;
        tip.caretSize = 5;
        tip.multiKeyBackground = dc.Raised;
        //  Doughnut / pie segments: separated by the surface color (flat, no 3D)
        def.elements.arc.borderColor = dc.Surface;
        def.elements.arc.borderWidth = 2;
    }

    // ================================================================== exporters (server) and image rendering

    /** @type {?Promise<Array<object>>} The server exporter list (WebMenuItem: Name, Title, IconClass, Data = input type). */
    let exportersPromise = null;

    /** @type {?Array<object>} The loaded exporter list. */
    let exportersLoaded = null;

    /**
     * Get the server chart exporters (cached; an error gives an empty list and is retried next time).
     * @returns {Promise<Array<object>>} The exporters.
     */
    function getExporters() {
        if (!exportersPromise)
            exportersPromise = Sw.Net.Get(Sw.Net.Api("GetChartExporters"))
                .then(r => (exportersLoaded = Array.isArray(r) ? r : []))
                .catch(e => {
                    console.warn("Chart: no exporters: " + e);
                    exportersPromise = null;
                    return [];
                });
        return exportersPromise;
    }

    /**
     * Parse a text as xml, let a callback change it and serialize it again.
     * @param {string} text Xml.
     * @param {function(XMLDocument):void} fn Mutator.
     * @returns {string} Xml.
     */
    function tweakXml(text, fn) {
        const doc = new DOMParser().parseFromString(text, "text/xml");
        fn(doc);
        return new XMLSerializer().serializeToString(doc);
    }

    /**
     * Render a config off-screen as an image.
     * @param {object} data Render config (mutated: not responsive, no animation, device pixel ratio 1).
     * @param {number} width Width (px).
     * @param {number} height Height (px).
     * @param {boolean} transparent No background.
     * @param {"Png"|"Svg"} format Image format.
     * @param {number} [scale=1] Font scale (png only).
     * @param {ValueFmt} [fmt] Value formatter.
     * @returns {Promise<string>} A data uri.
     */
    async function renderImage(data, width, height, transparent, format, scale, fmt) {
        let canvas = null, ctx;
        if (format === "Png") {
            canvas = Sw.el("canvas", { width, height, style: { display: "none" } });
            canvas.Scale = scale || 1;
            document.body.append(canvas);
            ctx = canvas.getContext("2d");
        }
        else if (format === "Svg") {
            if (typeof C2S === "undefined") {
                const r = await Sw.Net.IncludeJs(ScriptUrl, "external/canvas2svg.js");
                if (r && r.error)
                    throw new Error(r.error);
            }
            ctx = new C2S({ width, height });
        }
        else
            throw new Error("Invalid format " + format);
        //  Exports don't depend on the screen: the default text sizes (restored in finally)
        const liveScale = fontScale;
        setFontScale(1);
        try {
            const o = ensure(data, "options");
            o.responsive = false;
            o.maintainAspectRatio = false;
            o.devicePixelRatio = 1;
            ensure(o, "animation").duration = 0;
            const ch = new Chart(ctx, data);
            ch.Transparent = transparent;
            if (fmt)
                ch.SwFmt = fmt;
            return await new Promise((resolve, reject) => {
                ch.options.animation.onComplete = () => {
                    ch.options.animation.onComplete = null;
                    try {
                        if (format === "Png") {
                            resolve(ch.toBase64Image());
                            return;
                        }
                        let svg = ctx.getSerializedSvg(true);
                        const end = svg.indexOf(">");
                        const head = tweakXml(svg.substring(0, end) + "></svg>", doc => {
                            const r = doc.documentElement;
                            r.removeAttribute("width");
                            r.removeAttribute("height");
                            r.setAttribute("viewBox", "0 0 " + width + " " + height);
                        });
                        svg = head.substring(0, head.length - 2) + svg.substring(end);
                        resolve("data:image/svg+xml;charset=UTF-8;base64," + Sw.Util.Base64EncodeString(svg));
                    }
                    catch (e) {
                        reject(e);
                    }
                    finally {
                        //  (a canvas2svg context has no event listeners to unbind, destroy would throw)
                        if (format === "Png")
                            setTimeout(() => ch.destroy(), 0);
                    }
                };
                if (format === "Png")
                    ch.resize(width, height);
                ch.update();
            });
        }
        finally {
            setFontScale(liveScale);
            if (canvas)
                canvas.remove();
        }
    }

    // ================================================================== options

    /**
     * Options of a chart (Sw.Chart.Options, legacy CanvasChartOptions).
     */
    class ChartOptions {
        /** @type {boolean} No menu button, no long press menu and no keyboard shortcuts. */
        DisableMenu = false;
        /** @type {?function(object):(void|Promise<void>)} Called with each fetched server config before it is stored. */
        OnServerData = null;
        /** @type {?function(object):(void|Promise<void>)} Called with each polled config after the view state was applied. */
        OnFixedData = null;
        /** @type {?function(object):void} Called with Chart.defaults after the theme colors were applied. */
        OnDefaults = null;
        /** @type {?function(string, *, number, number):void} Data point click (label, value, datasetIndex, dataIndex). */
        OnClick = null;
        /** @type {boolean} No container background (class Transparent). */
        Transparent = false;
        /** @type {boolean} Add (addChart) waits for the first fetch. */
        WaitFirst = true;
        /** @type {?number} Data label mode 0-6, overriding the server ValueLabel. */
        ValueLabel = null;
        /** @type {boolean} No Order menu and no Space shortcut. */
        DisableOrderMenu = false;
        /** @type {boolean} No data coloring menu part and no o / l / g / s / m shortcuts. */
        DisableColorMenu = false;
        /** @type {?number} Remove x positions with any value below this. */
        MinValue = null;
        /** @type {?number} Remove x positions with any value above this. */
        MaxValue = null;
        /** @type {?number} Keep at most this many x positions. */
        MaxCount = null;
        /** @type {boolean} Name every bar of a horizontal chart with at most 30 bars (the method graph on phones; else Chart.js skips names). */
        LabelAll = false;
        /** @type {number} Initial order (url order): 0 original, +-1 labels, +-(n+2) dataset n; negative = descending. */
        Order = 0;
        /** @type {?string} Initial index axis ("y": horizontal bars / lines; null = the server's), changeable in the Type menu. */
        IndexAxis = null;
        /**
         * @type {boolean} Initial "Log scale" (url log=true): a logarithmic value axis on axis charts, so a few tall bars don't flatten
         * the rest (the axis starts at the power of ten at or below the smallest value, bars are at least 4 px long so values at the
         * start stay visible; zero values have no bar). Toggled in the chart menu.
         */
        LogScale = false;
        /**
         * @type {boolean} Automatic "Log scale" (chart.html: the method graph without a log url parameter): with the first data, the log
         * scale is turned on when the values span more than about two orders of magnitude (largest > 100 x smallest positive, or > 20 x
         * the median), so most bars are not flat lines. Decided once; the menu toggle still works (and wins).
         */
        AutoLogScale = false;
        /**
         * @type {?function(boolean, ?{Max: number, Min: number, Median: number}):void} Called once when AutoLogScale decided: (on, spread).
         */
        OnAutoLogScale = null;
        /** @type {?function(boolean):void} Called when the viewer toggles "Log scale" in the menu: (on). */
        OnLogScale = null;
        /**
         * @type {boolean} Solid theme colors (url solid=true): axis charts with the original colors draw each dataset in one
         * strong color (dataset 0: --ThemeAcc1, others: hues around it) instead of the server's per point colors.
         */
        Solid = false;
        /**
         * @type {?(number|string)} Time zone of UTC time labels (url timeoffset): minutes to add (ex: the server's offset, so the
         * labels match a "server time" clock), or "local" (the viewer's time zone, daylight saving aware). Null keeps the labels.
         * See shiftTimeLabels.
         */
        TimeOffset = null;
        /**
         * @type {boolean} Axis titles where the server config has none (applyAxisTitles; chart.html: on unless the menu is off
         * (m=false, small tiles) or axistitles=false).
         */
        AxisTitles = false;
        /**
         * @type {?string} Value history url (url vhist, same site): a gauge (pie / doughnut with two values, ex: "Use" / "Idle" in
         * percent) shows the mean of the last ValueHistoryCount values of dataset 0 of this (history) chart instead of its own
         * single sample, so it agrees with the history chart next to it (the current cpu sample of the server is very noisy).
         */
        ValueHistory = null;
        /** @type {number} Number of history values to average (url vhistn, default 3). */
        ValueHistoryCount = 3;
        /** @type {boolean} Keep the server's drop shadow (url shadow=true; Design v2 draws charts flat by default). */
        Shadow = false;
        /** @type {boolean} Start with the table view (url table=true): the data as an html table instead of the chart (toggled with d / the menu). */
        TableView = false;
    }

    /**
     * Patterns of UTC time labels that can be moved to another time zone (all labels of a chart must use the same one):
     * "HH:mm", "HH:mm:ss" (today) and "MM-dd HH:mm" (this year). Date only labels ("MM-dd") are kept.
     * @type {Array<RegExp>}
     */
    const TimeLabelPatterns = [/^(\d{1,2}):(\d{2})(?::(\d{2}))?$/, /^(\d{2})-(\d{2}) (\d{1,2}):(\d{2})$/];

    /**
     * Move UTC time labels (ex: the ServerManager history charts, "23:50:22") to another time zone, keeping their format
     * (ChartOptions.TimeOffset). Nothing changes unless every non empty label matches the same TimeLabelPatterns entry.
     * @param {Array} labels The labels (changed in place).
     * @param {number|string} offset Minutes to add, or "local".
     */
    function shiftTimeLabels(labels, offset) {
        const local = offset === "local";
        const add = local ? 0 : +offset;
        if (!labels || !labels.length || (!local && !(add >= -1440 && add <= 1440 && add !== 0)))
            return;
        const pi = TimeLabelPatterns.findIndex(p => p.test(String(labels[0] ?? "").trim()));
        if (pi < 0 || !labels.every(l => l === null || l === "" || TimeLabelPatterns[pi].test(String(l).trim())))
            return;
        const now = new Date();
        const p2 = v => String(v).padStart(2, "0");
        for (let i = 0; i < labels.length; ++i) {
            const s = labels[i];
            if (s === null || s === "")
                continue;
            const m = TimeLabelPatterns[pi].exec(String(s).trim());
            const utc = pi === 0
                ? Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), +m[1], +m[2], +(m[3] || 0))
                : Date.UTC(now.getUTCFullYear(), +m[1] - 1, +m[2], +m[3], +m[4]);
            const d = new Date(utc + (local ? -new Date(utc).getTimezoneOffset() : add) * 60000);
            //  SwWeb:allow ux-txt-20 (compact axis labels in the server's zone, ISO order like the exports; a viewer locale would mix zones)
            const hm = p2(d.getUTCHours()) + ":" + p2(d.getUTCMinutes());
            labels[i] = pi === 0
                ? (m[3] !== undefined ? hm + ":" + p2(d.getUTCSeconds()) : hm)
                : p2(d.getUTCMonth() + 1) + "-" + p2(d.getUTCDate()) + " " + hm;    //  SwWeb:allow ux-txt-20 (the same axis labels)
        }
    }

    /**
     * Solid theme colors of an axis chart (ChartOptions.Solid): one opaque color per dataset from the theme palette
     * (themePalette: dataset 0 in --ThemeAcc1; 85 % opaque fill, opaque border).
     * @param {object} data The render config (mutated).
     */
    function applySolidColors(data) {
        const dss = data.data.datasets;
        if (!dss.length)
            return;
        const pal = themePalette();
        dss.forEach((ds, i) => {
            const c = pal[i % pal.length];
            ds.borderColor = rgb(c);
            ds.backgroundColor = rgba(c, 0.85);
        });
    }

    /** Fallback series palette (the SysWeaver brand light palette, see ui/tokens.css --sw-chart-1..7). */
    const FallbackPalette = ["#A85724", "#2A78D6", "#138A60", "#6250D6", "#C4507E", "#4E6B80", "#A87400"];

    /**
     * True when the page shows an app's own theme ("name@css": a theme with the app's style sheet, the usetheme / settheme url flags, ex:
     * a chart framed by an app with its own UI): the app chose its look, so its charts keep the server's colors by default (as the
     * original chart); the theme palette is the default of the SysWeaver themes.
     * @returns {boolean} True for an app theme.
     */
    function appTheme() {
        const S = Sw.Session || {};
        const t = String(S.RequestedTheme || S.StoredTheme || "");
        return t.indexOf("@") > 0;
    }

    /**
     * The categorical series palette of the theme: the custom properties --sw-chart-1 .. --sw-chart-12 (ui/tokens.css: slot 1 is
     * --ThemeAcc1 and the steel slot --ThemeAcc2, so an app brand changes them; the hues between are validated for color vision
     * deficiencies against the brand backgrounds).
     * @returns {number[][]} [r, g, b] colors (at least one).
     */
    function themePalette() {
        const cs = getComputedStyle(document.documentElement);
        const out = [];
        for (let i = 1; i <= 12; ++i) {
            const v = cs.getPropertyValue("--sw-chart-" + i).trim();
            if (!v)
                break;
            //  resolved by the css engine (a theme slot may be a relative colour, ex: oklch(from var(--sw-accent1) ...))
            out.push(Array.from(cssToRgba(tokenColor("--sw-chart-" + i, v))).slice(0, 3));
        }
        return out.length ? out : FallbackPalette.map(c => Array.from(cssToRgba(c)).slice(0, 3));
    }

    /**
     * Parse one css color token (#rgb[a], #rrggbb[aa], rgb[a](), hsl[a](), or a name) into [r, g, b, a] (a 0..1). Parsed by hand
     * so faint colors keep their exact rgb (a canvas round trip loses it at a low alpha).
     * @param {string} t Token.
     * @returns {number[]} The color.
     */
    function parseCssColor(t) {
        const s = t.trim().toLowerCase();
        if (s[0] === "#") {
            const h = s.substring(1);
            const x = (h.length <= 4) ? h.split("").map(c => parseInt(c + c, 16)) : [0, 2, 4, 6].map(i => parseInt(h.substring(i, i + 2), 16));
            return [x[0], x[1], x[2], isNaN(x[3]) ? 1 : x[3] / 255];
        }
        const m = /^(rgba?|hsla?)\(([^()]*)\)$/.exec(s);
        if (m) {
            const p = m[2].split(/[\s,/]+/).filter(x => x).map(x => x.endsWith("%") ? parseFloat(x) / 100 : parseFloat(x));
            const a = p.length > 3 ? p[3] : 1;
            if (m[1][0] === "r")
                return [p[0], p[1], p[2], a];
            return hslToRgb(p[0] / 360, p[1], p[2]).concat([a]);
        }
        const c = cssToRgba(t);
        return [c[0], c[1], c[2], c[3] / 255];
    }

    /**
     * The palette slot of a named group ("Group / Name" labels, ex: the method graph's modules): a hash of the name (FNV-1a) modulo
     * the palette size, so the same name gets the same color in every app and every load, independent of the other groups present.
     * Two groups may share a color (the group legend then names both in one entry). The salt "sw949:" is chosen so the 7 framework
     * systems every app has get 7 different slots with the default 7 color palette: HttpServer 1 (the accent), IconHttpServerModule 2,
     * ServiceManager 3, ExploreHttpServerModule 4, FileHttpServerModule 5, API 6 (steel), CachedTransformer 7. Changing it recolors every app.
     * The slot is the same in every theme and the tokens keep one hue family per slot (ui/tokens.css), so a module keeps its color
     * family in light and dark.
     * @param {string} name The group name.
     * @param {number} size The palette size.
     * @returns {number} The slot (0 based, below size).
     */
    function stableSlot(name, size) {
        let h = 0x811c9dc5;
        const s = "sw949:" + name;
        for (let i = 0; i < s.length; ++i) {
            h ^= s.charCodeAt(i);
            h = Math.imul(h, 0x01000193) >>> 0;
        }
        return size > 0 ? h % size : 0;
    }

    /**
     * The next series slot (plain series: series n = slot n; group slots from stableSlot are not counted).
     * @param {Map<*, number>} slots Hue / group key to slot.
     * @returns {number} The slot.
     */
    function nextSeriesSlot(slots) {
        let n = 0;
        slots.forEach((v, k) => {
            if (typeof k === "number")
                ++n;
        });
        return n;
    }

    /**
     * Theme colors (the default color mode): every chromatic color of the config (datasets and options.elements: border,
     * background, fill, gradient color strings) is replaced by a color of the theme palette (themePalette), keeping its alpha.
     * Colors of the same hue get the same palette color, in the order of first appearance, so the structure the server
     * encoded stays: series colors, per label colors, groups by hue (ex: the performance chart's systems) and the faded
     * "rest" segment of gauge like doughnuts (alpha kept, see arcTrack). Grays stay as they are. Past the palette size the
     * colors repeat lighter, then darker (the legend and labels still tell the series apart).
     * Slot order (documented in chart/README.md and WebBranding.md): runs before the user's sort order, so the n-th distinct hue in
     * the server's order (datasets, then per label colors in label order) gets --sw-chart-n; that is the legend order of the default
     * view, and a color stays with its series when the bars are re-sorted. Grouped labels get the slot of their group name
     * (stableSlot: the same module has the same color in every app and load), and the group legend lists the groups in slot order
     * (data.data.SwGroupSlots, see applyGroupLegend).
     * @param {object} data The render config (mutated).
     */
    function applyThemePalette(data) {
        const pal = themePalette();
        const slots = new Map();
        const tokenRx = /#[0-9a-f]{3,8}\b|(?:rgba?|hsla?)\([^()]*\)/gi;
        //  Labels "Group / Name" (ex: the performance chart: hue by system, shade by name): per label colors get one slot per group
        const labels = data.data.labels || [];
        const n = labels.length;
        const groupOf = labels.map(l => {
            const s = String(l ?? "");
            const p = s.indexOf(" / ");
            return p > 0 ? s.substring(0, p).trim() : null;
        });
        const grouped = n > 1 && groupOf.filter(g => g).length >= n * 0.8;
        /**
         * Recolor one color value.
         * @param {*} v String, gradient string, array or other.
         * @param {number} [li] Label index of a per label color.
         * @returns {*} The new value.
         */
        const map = (v, li) => {
            if (Array.isArray(v))
                return v.map((x, i) => map(x, v.length === n ? i : undefined));
            if (typeof v !== "string")
                return v;
            const group = (grouped && li !== undefined) ? groupOf[li] : null;
            //  A gradient string ("cone(0;#c44;1;#ed5)") becomes one palette color, lighter towards its end (its first color picks the slot)
            const grad = v.indexOf(";") > 0;
            const count = grad ? (v.match(tokenRx) || []).length : 1;
            let slot = -1, k = 0;
            return v.replace(tokenRx, t => {
                const c = parseCssColor(t);
                const at = k++;
                if (c.some(x => isNaN(x)))
                    return t;
                if (slot < 0) {
                    const hsl = rgbToHsl(c[0], c[1], c[2]);
                    if (hsl[1] < 0.15 || hsl[2] < 0.06 || hsl[2] > 0.96)
                        return t;
                    //  Same hue within 8 degrees (shades of one hue, rounding of server generated colors) = same slot
                    const hue = hsl[0] * 360;
                    let i = group ? slots.get("g:" + group) : undefined;
                    if (!group)
                        for (const [h, s] of slots)
                            if (typeof h === "number" && Math.min(Math.abs(h - hue), 360 - Math.abs(h - hue)) <= 8) {
                                i = s;
                                break;
                            }
                    //  A group takes the slot of its name (stableSlot), so a module has the same color in every app, whatever other
                    //  modules the app loads; plain series take the next slot in the server's order (series n = slot n)
                    if (i === undefined)
                        slots.set(group ? "g:" + group : hue, i = group ? stableSlot(group, pal.length) : nextSeriesSlot(slots));
                    slot = i;
                }
                let p = pal[slot % pal.length];
                //  Past the palette: the same hues lighter, then darker (the legend still names every color)
                const round = Math.floor(slot / pal.length) % 3;
                if (round === 1)
                    p = p.map(x => x + (255 - x) * 0.45);
                else if (round === 2)
                    p = p.map(x => x * 0.65);
                if (grad && count > 1) {
                    const f = 0.4 * at / (count - 1);
                    p = p.map(x => x + (255 - x) * f);
                }
                else
                    slot = -1;
                return c[3] >= 1 ? rgb(p) : rgba(p, c[3]);
            });
        };
        /**
         * Recolor the color properties of a dataset or element options.
         * @param {object} o The object.
         */
        const obj = o => {
            if (!o)
                return;
            //  The fill first: it picks the slots (a border may be generated around another hue, see the pie fan in Apply)
            ["backgroundColor", "borderColor", "pointBackgroundColor", "pointBorderColor", "hoverBackgroundColor", "hoverBorderColor"].forEach(k => {
                if (k in o)
                    o[k] = map(o[k]);
            });
            if (o.fill && typeof o.fill === "object")
                ["above", "below", "target"].forEach(k => {
                    if (typeof o.fill[k] === "string" || Array.isArray(o.fill[k]))
                        o.fill[k] = map(o.fill[k]);
                });
        };
        data.data.datasets.forEach(obj);
        //  The slot of each group (applyGroupLegend lists the groups in slot order, so legend entry n has slot n)
        if (grouped) {
            /** @type {Map<string, number>} Group name to palette slot (set by applyThemePalette). */
            const gs = new Map();
            slots.forEach((s, k) => {
                if (typeof k === "string" && k.startsWith("g:"))
                    gs.set(k.substring(2), s);
            });
            Object.defineProperty(data.data, "SwGroupSlots", { value: gs, configurable: true, writable: true });
        }
        //  A server drop shadow without a color: a soft one instead of 50 % black (a light glow on dark themes would look like an effect)
        const so = data.options?.plugins?.shadow;
        if (so && typeof so === "object" && !so.color)
            so.color = Sw.Theme && Sw.Theme.IsDark ? "rgba(0,0,0,0.35)" : "rgba(29,42,51,0.16)";
        const els = data.options && data.options.elements;
        if (els)
            ["point", "line", "bar", "arc"].forEach(k => obj(els[k]));
    }

    // ================================================================== live merge

    /**
     * Merge new chart data into the live chart data so Chart.js animates the change: datasets are matched by label (removed /
     * inserted), x positions by label text (labels, data and same length color arrays spliced), then values and array colors
     * are copied.
     * @param {object} cd The live chart.data (mutated).
     * @param {object} nd The new data.
     */
    function mergeInto(cd, nd) {
        const cds = cd.datasets, nds = nd.datasets;
        //  Datasets by label
        const oldDs = new Map(cds.map((d, i) => [d.label, i]));
        const addDs = [];
        nds.forEach((d, i) => {
            if (oldDs.has(d.label))
                oldDs.delete(d.label);
            else
                addDs.push(i);
        });
        Array.from(oldDs.values()).sort((a, b) => b - a).forEach(i => cds.splice(i, 1));
        addDs.forEach(i => cds.splice(i, 0, null));
        //  X positions by label
        const cl = cd.labels, nl = nd.labels;
        const oldX = new Map(cl.map((l, i) => [l, i]));
        const addX = [];
        nl.forEach((l, i) => {
            if (oldX.has(l))
                oldX.delete(l);
            else
                addX.push(i);
        });
        let len = cl.length;
        /**
         * Splice one x position out of / into the live datasets.
         * @param {number} i Index.
         * @param {boolean} insert Insert (else remove).
         */
        const spliceX = (i, insert) => {
            /**
             * Splice an array.
             * @param {Array} a The array.
             */
            const sp = a => {
                if (insert)
                    a.splice(i, 0, null);
                else
                    a.splice(i, 1);
            };
            cds.forEach(ds => {
                if (!ds)
                    return;
                sp(ds.data);
                if (Array.isArray(ds.backgroundColor) && ds.backgroundColor.length === len)
                    sp(ds.backgroundColor);
                if (Array.isArray(ds.borderColor) && ds.borderColor.length === len)
                    sp(ds.borderColor);
            });
            len += insert ? 1 : -1;
        };
        Array.from(oldX.values()).sort((a, b) => b - a).forEach(i => {
            cl.splice(i, 1);
            spliceX(i, false);
        });
        addX.forEach(i => {
            cl.splice(i, 0, nl[i]);
            spliceX(i, true);
        });
        //  Values and colors
        nds.forEach((n, i) => {
            const c = cds[i];
            if (!c) {
                cds[i] = n;
                return;
            }
            const cdd = c.data, ndd = n.data || [];
            for (let j = 0; j < nl.length; ++j)
                if (cdd[j] !== ndd[j])
                    cdd[j] = ndd[j];
            ["backgroundColor", "borderColor"].forEach(k => {
                const a = c[k], b = n[k];
                if (Array.isArray(a) && b)
                    for (let j = 0; j < a.length; ++j)
                        a[j] = Array.isArray(b) ? b[j] : b;
            });
        });
    }

    // ================================================================== the live chart

    /**
     * One live, polling chart: fetches the config from an url, applies the view state (type, order, colors), renders it with
     * Chart.js, merges later polls into the live chart (animated) and provides the menu, keyboard shortcuts and exports.
     */
    class ChartView {
        /**
         * @param {string} url Data url.
         * @param {HTMLElement} parent Element to add the chart to.
         * @param {*} params Optional POST argument (url p).
         * @param {?function():void} onFirstLoad Called once after the first fetch (success or failure).
         * @param {ChartOptions} options Options.
         */
        constructor(url, parent, params, onFirstLoad, options) {
            /** @type {string} Data url. */
            this.Url = url;
            /** @type {*} POST argument (null = GET). */
            this.Params = params ?? null;
            /** @type {ChartOptions} Options. */
            this.Options = options;
            /** @type {?function():void} First load callback (cleared after the call). */
            this.OnFirstLoad = onFirstLoad || null;
            /** @type {ValueFmt} The value formatter (mutated by Apply). */
            this.Fmt = { Precision: -2, Prefix: "", Suffix: "" };
            /** @type {?string} Forced Chart.js type (null = server type). */
            this.Type = null;
            /** @type {?string} Forced index axis ("x" / "y"). */
            this.IndexAxis = options.IndexAxis || null;
            /** @type {boolean} Logarithmic value axis (axis charts, the "Log scale" menu toggle). */
            this.LogScale = !!options.LogScale;
            /** @type {boolean} The automatic log scale decision is still to be made (ChartOptions.AutoLogScale, Apply). */
            this.AutoLog = !!options.AutoLogScale && !options.LogScale;
            /** @type {?string} Forced color mode (null = the theme palette, "Original" = the server colors; the default with an app theme). */
            this.Color = appTheme() ? ColorMode.Original : null;
            /** @type {number[]} Seed of the random color modes. */
            this.ColorSeed = Rng.Seed();
            /** @type {boolean} Colored labels (legend / ticks). */
            this.ColorLabels = false;
            /** @type {boolean} Colored data labels. */
            this.ColorValues = false;
            /** @type {number} Order: 0 original, +-1 labels, +-(n+2) dataset n; negative = descending. */
            this.Order = options.Order || 0;
            /** @type {number} Export font scale (png exports). */
            this.Scale = 1;
            /** @type {?string} Json of the last server config (every rebuild starts from it). */
            this.OrgStr = null;
            /** @type {?string} Original type + "h" / "v" (Type menu filter). */
            this.OrgType = null;
            /** @type {?object} The live Chart instance (main.Data = last render config). */
            this.Main = null;
            /** @type {number} Poll interval (ms). */
            this.RefreshRate = 15000;
            /** @type {?HTMLButtonElement} The menu button. */
            this.MenuButton = null;
            /** @type {boolean} The table view is shown (ToggleTable: the data as an html table over the chart). */
            this.TableOn = !!options.TableView;
            /** @type {?HTMLElement} The table view container (div.ChartTable), while shown. */
            this.TableE = null;
            /** @type {HTMLElement} The container (SysWeaver-Chart). */
            this.E = Sw.el("SysWeaver-Chart", { class: options.Transparent ? "Transparent" : null });
            /** @type {HTMLCanvasElement} The canvas (replaced on rebuilds). */
            this.Canvas = ChartView.NewCanvas();
            this.E.append(this.Canvas);
            parent.append(this.E);
            lastFmt = this.Fmt;
            setupChartJs();
            if (!options.DisableMenu)
                this.AddMenu();
            /** @type {?string} The theme signature (themeSignature) the chart colors were last read with (Theme). */
            this.ThemeSig = null;
            this.Theme();
            Sw.Theme.OnChange(() => setTimeout(() => this.Theme(), 10));
            //  OWN4-Chart-1: the colors are right from the first paint and stay right. They are read from the computed theme tokens;
            //  whenever those differ from what the chart was drawn with (a theme or brand style sheet applied after the first read, a
            //  style sheet still loading, a frame that was hidden, a bfcache restore) or the text size step (--sw-scale) changed, the
            //  colors are read again and the chart is rebuilt. Checked on resize (once per frame), when the page has loaded or shows
            //  again and when the brand style sheet is applied; a check that finds nothing changed costs one style read and never
            //  redraws. No timed re-checks (S7a-B-1): the grey first read came from the reduced-motion rule of ui/tokens.css, which
            //  made every color a 1 ms transition (fixed there, S7a-A1); the token probe also opts out of transitions (tokenColor).
            let pending = 0;
            const check = () => {
                pending = 0;
                if (screenScale() !== fontScale || themeSignature() !== this.ThemeSig)
                    this.Theme();
            };
            const soon = () => {
                if (!pending)
                    pending = requestAnimationFrame(check);
            };
            window.addEventListener("resize", soon);
            window.addEventListener("load", soon);
            window.addEventListener("pageshow", soon);
            document.addEventListener("visibilitychange", () => {
                if (document.visibilityState === "visible")
                    soon();
            });
            if (Sw.Theme.BrandReady && typeof Sw.Theme.BrandReady.then === "function")
                Sw.Theme.BrandReady.then(soon, () => { });
        }

        /**
         * A new chart canvas element.
         * @returns {HTMLCanvasElement} The canvas.
         */
        static NewCanvas() {
            return Sw.el("canvas", { attrs: { role: "img", "aria-label": _TF("Chart") } });
        }

        // ------------------------------------------------------------------ state helpers

        /**
         * True if the order menu / shortcut is disabled (options or server).
         * @returns {boolean}
         */
        get OrderDisabled() {
            return !!(this.Options.DisableOrderMenu || this.Main?.Data?.DisableOrderMenu);
        }

        /**
         * True if the data coloring menu / shortcuts are disabled (options or server).
         * @returns {boolean}
         */
        get ColorDisabled() {
            return !!(this.Options.DisableColorMenu || this.Main?.Data?.DisableColorMenu);
        }

        /** Call the first load callback once. */
        FirstLoaded() {
            const f = this.OnFirstLoad;
            this.OnFirstLoad = null;
            f && f();
        }

        /**
         * A fresh render config: the last server config with the view state applied.
         * @returns {object} The config.
         */
        Fresh() {
            const d = JSON.parse(this.OrgStr);
            this.Apply(d);
            return d;
        }

        /**
         * The legend position of charts without axes: left, or bottom in a portrait container.
         * @returns {string} "left" or "bottom".
         */
        LegendSide() {
            const r = this.E.getBoundingClientRect();
            return r.height > r.width * 1.2 ? "bottom" : "left";
        }

        // ------------------------------------------------------------------ apply view state

        /**
         * Transform a server config into a render config (the old ApplyChanges): min / max filter, formatter, label mode,
         * forced type and axis, axis cleanup, legend, generated colors for pie types, order, color scheme or gradient
         * strings, colored labels / values, count limit and the click handler.
         * @param {object} data The config (mutated).
         */
        Apply(data) {
            const o = this.Options;
            const opt = ensure(data, "options");
            const dd = ensure(data, "data");
            if (!Array.isArray(dd.datasets))
                dd.datasets = [];
            if (!Array.isArray(dd.labels))
                dd.labels = [];
            if (o.TimeOffset !== null)
                shiftTimeLabels(dd.labels, o.TimeOffset);
            filterData(data, o.MinValue, o.MaxValue);
            opt.responsive = true;
            opt.maintainAspectRatio = false;

            const f = this.Fmt;
            f.Precision = typeof data.Precision === "number" ? data.Precision : -2;
            f.Prefix = data.ValuePrefix ?? "";
            f.Suffix = data.ValueSuffix ?? "";

            applyLabelMode(data, (o.ValueLabel || o.ValueLabel === 0) ? o.ValueLabel : data.ValueLabel);
            const plugins = ensure(opt, "plugins");

            if (this.Type === "line")
                dd.datasets.forEach(ds => {
                    if (!ds.borderColor)
                        ds.borderColor = ds.backgroundColor;
                    if (ds.borderWidth <= 0)
                        ds.borderWidth = 2;
                });
            data.type = this.Type ?? data.type;
            const orgAxis = opt.indexAxis;
            opt.indexAxis = this.IndexAxis ?? orgAxis;
            //  Turned sideways (horizontal / vertical): the server's x / y scale settings (ex: stacked) move with their axis
            if ((opt.indexAxis === "y") !== (orgAxis === "y") && opt.scales) {
                const sx = opt.scales.x, sy = opt.scales.y;
                delete opt.scales.x;
                delete opt.scales.y;
                if (sy)
                    opt.scales.x = sy;
                if (sx)
                    opt.scales.y = sx;
            }
            ensure(opt, "scales", opt.indexAxis ? opt.indexAxis : "y", "ticks").precision = Math.abs(f.Precision);

            const noAxis = NoAxisTypes.has(data.type);
            if (noAxis) {
                delete opt.scales.x;
                delete opt.scales.y;
                const lg = ensure(plugins, "legend");
                lg.position = this.LegendSide();
                lg.reverse = false;
            }
            else if (dd.datasets.length === 1)
                ensure(plugins, "legend").display = false;
            if (!noAxis && this.AutoLog && dd.datasets.length) {
                //  Automatic log scale (once, with the first data): values spanning more than about two orders of magnitude
                this.AutoLog = false;
                const sp = valueSpread(dd.datasets, opt.indexAxis === "y" ? "x" : "y");
                this.LogScale = !!sp && (sp.Max > 100 * sp.Min || sp.Max > 20 * sp.Median);
                o.OnAutoLogScale && o.OnAutoLogScale(this.LogScale, sp);
            }
            if (!noAxis && this.LogScale) {
                //  Log scale: from the power of ten at or below the smallest positive value (1 for counts; it started at 0.5, an odd
                //  first tick), and bars at least 4 px long, so values at the axis start (ex: 1) still get a bar (log of 0 has none)
                const va = opt.indexAxis === "y" ? "x" : "y";
                const vs = ensure(opt, "scales", va);
                vs.type = "logarithmic";
                const lo = valueSpread(dd.datasets, va);
                vs.min = Math.pow(10, Math.floor(Math.log10(lo ? lo.Min : 1)));
                dd.datasets.forEach(ds => {
                    if (ds && (ds.type || data.type) === "bar" && ds.minBarLength === undefined)
                        ds.minBarLength = 4;
                });
                delete vs.beginAtZero;
                vs.ticks = Object.assign({}, vs.ticks, {
                    /**
                     * Log axis tick: only 1, 2 and 5 times a power of ten get a text (0.5, 1, 2, 5, 10, 20, 50, 100...).
                     * @param {number} v Tick value.
                     * @returns {string} Text.
                     */
                    callback: function (v) {
                        const m = v / Math.pow(10, Math.floor(Math.log10(v)));
                        return [1, 2, 5].some(x => Math.abs(m - x) < 1e-6) ? formatValue(v, fmtOf(this.chart)) : "";
                    },
                });
                delete vs.ticks.beginAtZero;
            }

            if (data.type !== "polarArea")
                delete opt.scales.r;
            else {
                const t = ensure(opt, "scales", "r", "ticks");
                if (!t.backdropColor)
                    t.backdropColor = "rgba(" + getComputedStyle(document.body).getPropertyValue("--ThemeBackgroundRGB") + ",0.65)";
            }
            ensure(plugins, "datalabels").rotation = (!noAxis) && (opt.indexAxis !== "y") ? -80 : 0;
            if (!noAxis) {
                applyDensity(data, o.LabelAll ? (o.MaxCount || Infinity) : 0);
                if (o.AxisTitles)
                    applyAxisTitles(data, this.Url, f);
                //  A log value axis says so in its title (or gets the title "Log scale"), so the bar lengths are read right
                if (this.LogScale) {
                    const vt = ensure(opt, "scales", opt.indexAxis === "y" ? "x" : "y");
                    if (vt.display !== false) {
                        const ls = _TF("log scale", "Added in brackets to the value axis title of a chart with a logarithmic value axis");
                        const tt = vt.title && vt.title.text ? String(vt.title.text) : "";
                        if (tt.indexOf(ls) < 0)
                            vt.title = Object.assign({}, vt.title, { display: true, text: tt ? tt + " (" + ls + ")" : _TF("Log scale", "Value axis title of a chart with a logarithmic value axis and no other title") });
                    }
                }
            }
            else
                hideZeroArcLabels(data);

            //  Pie types: one color per label around dataset 0's color (later datasets copy dataset 0)
            const labelCount = dd.labels.length;
            if (noAxis && labelCount > 1)
                dd.datasets.forEach((ds, si) => {
                    ["backgroundColor", "borderColor"].forEach((k, ki) => {
                        const cols = ds[k];
                        const cl = Array.isArray(cols) ? cols.length : 0;
                        if (cl === labelCount)
                            return;
                        if (si > 0)
                            ds[k] = dd.datasets[0][k];
                        else {
                            const base = cl === 1 ? cols[0] : (typeof cols === "string" && cols.indexOf("(") < 0 || /^rgba?\(/.test(cols || "") ? cols : null);
                            ds[k] = generateColorAround(base || (ki ? "rgb(60,200,70)" : "rgba(60,200,70,0.8)"), labelCount);
                        }
                    });
                });

            //  Default colors: the theme palette ("Original" in the color menu keeps the server's colors; so does a chart that disables the color menu)
            if (!this.Color && !this.ColorDisabled && !o.Solid)
                applyThemePalette(data);

            orderData(data, this.Order);

            //  An app theme (the app's own look): the server's design is drawn as it is (bars, borders, shadow, grid), as the original chart
            const design = !appTheme();
            if (this.Color && this.Color !== ColorMode.Original) {
                applyColorScheme(data, this.Color, this.ColorSeed);
                if (design)
                    applyDesign(data, f, o.Shadow, this.LogScale);
            }
            else {
                if (noAxis) {
                    const track = "rgba(" + (getComputedStyle(document.body).getPropertyValue("--ThemeMainRGB").trim() || "128,128,128") + "," + ArcTrackAlpha + ")";
                    dd.datasets.forEach(ds => ds.backgroundColor = arcTrack(ds.backgroundColor, track));
                }
                else if (o.Solid)
                    applySolidColors(data);
                //  Design v2 look (line / bar / doughnut / axes), on the chosen colors before gradient strings are resolved
                if (design)
                    applyDesign(data, f, o.Shadow, this.LogScale);
                dd.datasets.forEach(resolveColors);
                const els = opt.elements;
                if (els)
                    ["point", "line", "bar", "arc"].forEach(k => resolveColors(els[k]));
                if (!noAxis && !o.Solid && isPerformanceChart(this.Url))
                    fillPerformanceBars(data);
                if (!noAxis && !o.Solid)
                    applyGroupLegend(data, o.MaxCount);
            }

            const ds0 = dd.datasets[0];
            const fc = ds0 && ds0.backgroundColor, bc = ds0 && ds0.borderColor;
            if (bc && fc) {
                if (this.ColorValues)
                    plugins.datalabels.color = bc;
                if (this.ColorLabels) {
                    if (noAxis) {
                        const at = i => Array.isArray(bc) ? bc[i] : bc;
                        const atF = i => Array.isArray(fc) ? fc[i] : fc;
                        ensure(plugins, "legend", "labels").generateLabels = chart =>
                            (chart.data.labels.length && chart.data.datasets.length) ? chart.data.labels.map((label, i) => ({
                                text: label, fontColor: at(i), fillStyle: atF(i), strokeStyle: at(i), lineWidth: 1, index: i,
                            })) : [];
                        if (opt.scales.r)
                            ensure(opt.scales.r, "pointLabels").color = p => at(p.index);
                    }
                    else
                        ensure(opt, "scales", opt.indexAxis !== "y" ? "x" : "y", "ticks").color = bc;
                }
            }

            limitData(data, o.MaxCount);

            //  Room for the menu button (top right) when there is no title line beside it
            const tl = plugins.title;
            if (!o.DisableMenu && !(tl && tl.display && tl.text && String(tl.text).length)) {
                const lay = ensure(opt, "layout");
                const pd = lay.padding;
                const room = 44;
                if (pd && typeof pd === "object")
                    pd.top = Math.max(+pd.top || 0, room);
                else
                    lay.padding = { top: Math.max(+pd || 0, room), left: +pd || 0, right: +pd || 0, bottom: +pd || 0 };
            }

            opt.onClick = (e, els) => {
                if (!els || !els.length)
                    return;
                const el = els[0];
                const ds = data.data.datasets[el.datasetIndex];
                const labels = data.data.labels;
                const vals = (ds && ds.data) || [];
                const label = el.index < labels.length ? labels[el.index] : null;
                const value = el.index < vals.length ? vals[el.index] : null;
                o.OnClick && o.OnClick(label, value, el.datasetIndex, el.index);
            };
            opt.onResize = chart => {
                if (!NoAxisTypes.has(chart.config.type))
                    return;
                const lg = chart.options.plugins && chart.options.plugins.legend;
                const side = this.LegendSide();
                if (lg && lg.position !== side && (lg.position === "left" || lg.position === "bottom"))
                    lg.position = side;
            };
        }

        // ------------------------------------------------------------------ render

        /**
         * Create the live Chart instance on the current canvas.
         * @param {object} data Render config.
         */
        Create(data) {
            this.Main = new Chart(this.Canvas, data);
            this.Main.Transparent = true;
            this.Main.SwFmt = this.Fmt;
            this.Main.Data = data;
            lastFmt = this.Fmt;
            this.Describe();
            this.RenderTable();
        }

        /**
         * Show or hide the table view (accessibility, Design v2, PORTING.md B11): the chart data as an html table (caption = the chart
         * title, a header row with the series, one row per label, formatted values) over the chart; the canvas is hidden from
         * assistive technology meanwhile. Escape (in the table) closes it. Key "d" and the menu toggle it.
         * @param {boolean} [on] Show (default: toggle).
         */
        ToggleTable(on) {
            this.TableOn = on === undefined ? !this.TableOn : !!on;
            this.RenderTable();
            if (this.TableOn && this.TableE)
                this.TableE.focus({ preventScroll: true });
            else if (this.MenuButton)
                this.MenuButton.focus({ preventScroll: true });
        }

        /** Build (or remove) the table view from the live chart data (called after every create / data update). */
        RenderTable() {
            if (!this.TableOn || !this.Main) {
                if (this.TableE) {
                    this.TableE.remove();
                    this.TableE = null;
                }
                this.Canvas && this.Canvas.removeAttribute("aria-hidden");
                this.E.classList.remove("ChartTableOn");
                return;
            }
            const d = this.Main.data || {};
            const labels = d.labels || [];
            const sets = (d.datasets || []).filter(x => x);
            const f = this.Fmt;
            const val = v => (v && typeof v === "object") ? (v.y ?? v.x ?? v.r ?? null) : v;
            const name = l => String(Array.isArray(l) ? l.join(" ") : (l ?? "")).split("\n")[0].trim();
            const title = document.title || _TF("Chart");
            const head = Sw.el("tr", null, Sw.el("th", { attrs: { scope: "col" }, text: _TF("Label", "Header of the first column of the chart table view (the category / time of each row)") }),
                ...sets.map((s, i) => Sw.el("th", { attrs: { scope: "col" }, text: s.label || (sets.length > 1 ? _T("Series {0}", i + 1) : _TF("Value", "Header of the value column of the chart table view")) })));
            const body = Sw.el("tbody");
            const count = Math.max(labels.length, ...sets.map(s => (s.data || []).length), 0);
            for (let r = 0; r < count; ++r)
                body.append(Sw.el("tr", null, Sw.el("th", { attrs: { scope: "row" }, text: name(labels[r]) }),
                    ...sets.map(s => {
                        const v = val((s.data || [])[r]);
                        return Sw.el("td", { text: (v === null || v === undefined || v === "") ? "" : formatValue(v, f) });
                    })));
            const table = Sw.el("table", null, Sw.el("caption", { text: title }), Sw.el("thead", null, head), body);
            if (!this.TableE) {
                this.TableE = Sw.el("div.ChartTable", { tabIndex: 0, attrs: { role: "region", "aria-label": _T("{0}, table view", title, "Accessible name of the table view of a chart, {0} is the chart title") } });
                this.TableE.addEventListener("keydown", ev => {
                    if (ev.key === "Escape") {
                        ev.preventDefault();
                        ev.stopPropagation();
                        this.ToggleTable(false);
                    }
                });
                this.E.append(this.TableE);
            }
            const top = this.TableE.scrollTop;
            this.TableE.replaceChildren(table);
            this.TableE.scrollTop = top;
            this.Canvas && this.Canvas.setAttribute("aria-hidden", "true");
            this.E.classList.add("ChartTableOn");
        }

        /**
         * Set the text alternative of the canvas (role img): the chart title (the page title) and, for pie / doughnut / polar charts
         * (gauges like the server page's cpu, memory and disk tiles), the label and value of each part of the first dataset
         * ("Cpu use: Used 12 %, Free 88 %"; at most 8 parts). Called after each create and data update.
         */
        Describe() {
            const ch = this.Main;
            if (!ch || !this.Canvas)
                return;
            const d = ch.data || {};
            let text = document.title || _TF("Chart");
            const ds = d.datasets && d.datasets[0];
            if (NoAxisTypes.has(ch.config.type) && ds && Array.isArray(ds.data)) {
                const labels = d.labels || [];
                const f = fmtOf(ch);
                const parts = ds.data.slice(0, 8).map((v, i) => {
                    const l = String(labels[i] ?? "").split("\n")[0].trim();
                    return (l ? l + " " : "") + formatValue(v, f);
                });
                if (parts.length)
                    text += ": " + parts.join(", ");
            }
            if (this.Canvas.getAttribute("aria-label") !== text)
                this.Canvas.setAttribute("aria-label", text);
        }

        /**
         * Rebuild the live chart from the last server config with the current view state (new canvas).
         * @param {boolean} [animate] Keep the configured animation (else no animation for this render).
         */
        Rebuild(animate) {
            if (!this.Main)
                return;
            const data = this.Fresh();
            let restore = null;
            if (!animate) {
                const an = ensure(data, "options", "animation");
                const had = Object.prototype.hasOwnProperty.call(an, "duration");
                const dur = an.duration;
                an.duration = 0;
                restore = () => {
                    if (had)
                        an.duration = dur;
                    else
                        delete an.duration;
                };
            }
            this.Main.destroy();
            const nc = ChartView.NewCanvas();
            this.Canvas.replaceWith(nc);
            this.Canvas = nc;
            this.Create(data);
            this.Main.update();
            if (restore)
                setTimeout(restore, 10);
        }

        /** Apply the theme colors (and the OnDefaults hook) to Chart.defaults and rebuild. */
        Theme() {
            this.ThemeSig = themeSignature();
            applyThemeDefaults();
            const fn = this.Options.OnDefaults;
            fn && fn(Chart.defaults);
            this.Rebuild();
        }

        // ------------------------------------------------------------------ polling

        /**
         * Fetch the server config (GET, or POST of Params).
         * @returns {Promise<?object>} The config.
         */
        Fetch() {
            return this.Params !== null ? Sw.Net.Send(this.Url, this.Params) : Sw.Net.Get(this.Url);
        }

        /**
         * ChartOptions.ValueHistory: replace the single sample of a two value gauge (pie / doughnut, ex: [use, idle] of 100 %) by the
         * mean of the last ValueHistoryCount values of dataset 0 of the history chart, keeping the total; the title line that shows
         * the sample (its value formatted with the config Precision) shows the mean. Any failure keeps the sample.
         * @param {object} data The fetched server config (changed in place).
         * @returns {Promise<void>}
         */
        async ApplyValueHistory(data) {
            const o = this.Options;
            const ds = data && data.data && data.data.datasets && data.data.datasets[0];
            const d = ds && ds.data;
            if (!Array.isArray(d) || d.length !== 2 || (data.type !== "pie" && data.type !== "doughnut"))
                return;
            try {
                const h = await Sw.Net.Get(o.ValueHistory, { NoNavigate: true });
                const hd = h && h.data && h.data.datasets && h.data.datasets[0] && h.data.datasets[0].data;
                if (!Array.isArray(hd))
                    return;
                const last = hd.map(v => (v && typeof v === "object") ? v.y : v).filter(v => typeof v === "number" && Number.isFinite(v)).slice(-Math.max(1, o.ValueHistoryCount));
                if (!last.length)
                    return;
                const old = +d[0];
                const total = old + (+d[1]);
                const mean = Math.min(total, Math.max(0, last.reduce((a, v) => a + v, 0) / last.length));
                d[0] = mean;
                d[1] = total - mean;
                //  The title line with the sample ("12.34%") shows the mean
                const prec = typeof data.Precision === "number" ? Math.abs(data.Precision) : 2;
                const tt = data.options && data.options.plugins && data.options.plugins.title;
                if (tt && Array.isArray(tt.text)) {
                    const from = old.toFixed(prec), to = mean.toFixed(prec);
                    tt.text = tt.text.map(line => typeof line === "string" && line.indexOf(from) >= 0 ? line.replace(from, to) : line);
                }
            }
            catch (e) {
                console.warn("Chart: value history failed: " + (e && e.message ? e.message : e));
            }
        }

        /**
         * One poll: fetch, render or merge, then schedule the next one (RefreshRate; also while the page is hidden, like the original chart).
         * Errors show a toast, wait 3 s and keep polling. A refused request (403, Sw.Access.IsDenied) shows the shared "not allowed"
         * state instead (ShowDenied, once) and keeps asking every 3 s like the original chart (an answer then reloads the page).
         * @returns {Promise<void>}
         */
        async Poll() {
            try {
                const data = await this.Fetch();
                if (this.Denied) {
                    location.reload();
                    return;
                }
                if (data)
                    await this.OnData(data);
            }
            catch (e) {
                if (Sw.Access && Sw.Access.IsDenied(e)) {
                    if (!this.Denied) {
                        /** @type {boolean} The chart was refused (403): the "not allowed" state is shown, the polls go on. */
                        this.Denied = true;
                        this.ShowDenied(e);
                    }
                    setTimeout(() => this.Poll(), 3000);
                    return;
                }
                Sw.Fail(_T("Failed to fetch data: {0}", e && e.message ? e.message : e));
                this.FirstLoaded();
                await Sw.Util.Delay(3000);
            }
            setTimeout(() => this.Poll(), this.RefreshRate);
        }

        /**
         * The user may not see this chart (403): the shared "not allowed" state (Sw.Page.ShowNotAllowed: who is signed in, the role
         * needed, a link Home, no retry) replaces the page (chart.html, the chart fills the body) or the chart element (Poll goes on asking every 3 s).
         * @param {*} e The refused request.
         */
        ShowDenied(e) {
            console.warn("Chart " + this.Url + ": not allowed (" + ((e && e.message) || e) + ")");
            const page = this.E.parentElement === document.body;
            if (page)
                document.body.style.overflow = "";
            Sw.Page.ShowNotAllowed(page ? { Url: this.Url, Error: e } : { Url: this.Url, Error: e, Element: this.E });
            this.FirstLoaded();
        }

        /**
         * Handle a fetched server config.
         * @param {object} data The config.
         * @returns {Promise<void>}
         */
        async OnData(data) {
            const o = this.Options;
            if (o.ValueHistory)
                await this.ApplyValueHistory(data);
            if (o.OnServerData)
                await o.OnServerData(data);
            ensure(data, "options", "plugins");
            ensure(data.options, "layout");
            this.OrgStr = JSON.stringify(data);
            this.OrgType = data.type + (data.options.indexAxis === "y" ? "h" : "v");
            this.Apply(data);
            if (o.OnFixedData)
                await o.OnFixedData(data);
            const tt = data.options.plugins.title && data.options.plugins.title.text;
            const title = Array.isArray(tt) ? tt[0] : tt;
            if (title && document.title !== title)
                document.title = title;
            if (!this.Main) {
                this.Create(data);
                this.FirstLoaded();
            }
            else {
                if (tt)
                    ensure(this.Main.options, "plugins", "title").text = tt;
                mergeInto(this.Main.data, data.data);
                this.Main.update();
                this.Describe();
                if (this.TableOn)
                    this.RenderTable();
            }
            const rate = data.RefreshRate;
            if (rate > 0)
                this.RefreshRate = rate;
            this.RefreshRate = Math.max(500, this.RefreshRate);
            this.Main.Data = data;
        }

        // ------------------------------------------------------------------ view state changes

        /**
         * Force a chart type.
         * @param {string} type Chart.js type.
         * @param {boolean} [horizontal] Index axis y.
         */
        SetType(type, horizontal) {
            this.Type = type;
            this.IndexAxis = horizontal ? "y" : "x";
            this.Rebuild();
        }

        /**
         * Set the color mode (a new random seed each time).
         * @param {?string} mode Mode (null = original colors).
         */
        SetColor(mode) {
            this.ColorSeed = Rng.Seed();
            this.Color = mode;
            this.Rebuild();
        }

        /** Toggle the colored labels. */
        ToggleColorLabels() {
            this.ColorLabels = !this.ColorLabels;
            this.Rebuild();
        }

        /** Toggle the colored values. */
        ToggleColorValues() {
            this.ColorValues = !this.ColorValues;
            this.Rebuild();
        }

        /**
         * Set the order (animated).
         * @param {number} order The order.
         */
        SetOrder(order) {
            this.Order = order;
            this.Rebuild(true);
        }

        /** Toggle the logarithmic value axis ("Log scale", axis charts). */
        ToggleLogScale() {
            this.AutoLog = false;
            this.LogScale = !this.LogScale;
            this.Options.OnLogScale && this.Options.OnLogScale(this.LogScale);
            this.Rebuild(true);
        }

        /**
         * Cycle the order 0 -> 1 -> -1 -> 2 -> -2 ... -> 0 and show a short info toast (Space key).
         */
        CycleOrder() {
            if (!this.Main)
                return;
            const sets = this.Main.data.datasets;
            let o = this.Order;
            if (o === 0)
                o = 1;
            else if (o > 0)
                o = -o;
            else {
                o = -o + 1;
                if (o > sets.length + 1)
                    o = 0;
            }
            const dur = 1500;
            const k = Math.abs(o);
            if (o === 0)
                Sw.Info(_TF("Using original order"), dur, true);
            else if (k === 1)
                Sw.Info(o < 0 ? _TF("Ordered by label name in reverse") : _TF("Ordered by label name"), dur, true);
            else {
                const name = sets[k - 2]?.label ?? _T("Series {0}", k - 1);
                Sw.Info(o < 0 ? _T("Ordered by {0} value from high to low", name) : _T("Ordered by {0} value from low to high", name), dur, true);
            }
            this.SetOrder(o);
        }

        // ------------------------------------------------------------------ menu

        /** Add the menu button, the long press / right click menu and the keyboard shortcuts. */
        AddMenu() {
            const title = _TF("Show options");
            const b = Sw.el("button.ChartMenuButton", { type: "button", title, attrs: { "aria-label": _TF("Chart options"), "aria-haspopup": "menu" } },
                Sw.Icon ? Sw.Icon("kebab") : Sw.IconClass("IconChartMenu"));
            b.addEventListener("click", ev => {
                ev.stopPropagation();
                this.ShowMenu(b);
            });
            Sw.Hint.Attach(b, title);
            this.E.append(b);
            this.MenuButton = b;
            Sw.LongPress(this.E, ev => {
                if (b.contains(ev.target))
                    return;
                this.ShowMenu(ev.clientX || ev.clientY ? { X: ev.clientX, Y: ev.clientY } : b);
            });
            document.addEventListener("keydown", ev => this.OnKey(ev));
        }

        /**
         * Open the chart menu.
         * @param {Element|{X: number, Y: number}} anchor Anchor.
         * @returns {Promise<void>}
         */
        async ShowMenu(anchor) {
            if (!this.OrgStr) {
                Sw.Fail(_TF("No data!"));
                return;
            }
            if (Sw.Popup.IsOpen())
                return;
            const exporters = await getExporters();
            if (Sw.Popup.IsOpen())
                return;
            const b = this.MenuButton;
            const isButton = anchor === b;
            b && b.setAttribute("aria-expanded", "true");
            Sw.Menu.Show(anchor, this.MenuItems(this.Fresh(), exporters), {
                Side: "below", Align: isButton ? "end" : "start", Class: "ChartMenu", Label: _TF("Chart options"),
                OnClose: () => b && b.setAttribute("aria-expanded", "false"),
            });
        }

        /**
         * Build the menu items (Type, Order, Color, Export).
         * @param {object} data The current render config.
         * @param {Array<object>} exporters Server exporters.
         * @returns {Array<object>} Sw.Menu items.
         */
        MenuItems(data, exporters) {
            const items = [];
            const types = this.TypeItems(data);
            if (types)
                items.push({ Text: _TF("Type"), Icon: "IconChartType", Title: _TF("Options for modifying the chart type"), Items: types });
            if (!(this.Options.DisableOrderMenu || data.DisableOrderMenu))
                items.push({ Text: _TF("Order"), Icon: "IconOrder", Title: _TF("Options for modifying the order of the data"), Items: this.OrderItems(data) });
            if (!NoAxisTypes.has(data.type))
                items.push({
                    Text: _TF("Log scale", "Chart menu toggle: a logarithmic value axis"), Icon: "IconChartSize", Checked: this.LogScale,
                    Title: this.LogScale
                        ? _TF("The value axis is logarithmic (each step is 10 times bigger). Select to use a normal axis.", "Tool tip of the checked Log scale toggle in the chart menu")
                        : _TF("Use a logarithmic value axis (each step 10 times bigger), so small values stay visible next to a few big ones.", "Tool tip of the Log scale toggle in the chart menu"),
                    Desc: _TF("Small values stay visible next to big ones", "One line description of the Log scale toggle in the chart menu"),
                    OnClick: () => this.ToggleLogScale(),
                });
            items.push({
                Text: _TF("Show as table", "Chart menu toggle: show the chart data as a table (accessible view)"), Icon: "table", Checked: this.TableOn,
                Title: _TF("Show the data of the chart as a table (key D)", "Tool tip of the Show as table toggle in the chart menu"),
                Desc: _TF("Every value as text, for screen readers too", "One line description of the Show as table toggle in the chart menu"),
                OnClick: () => this.ToggleTable(),
            });
            items.push({ Text: _TF("Color"), Icon: "IconColors", Title: _TF("Options for modifying the chart colors"), Items: this.ColorItems(data) });
            items.push({ Text: _TF("Export"), Icon: "IconChartExport", Title: _TF("Options for exporting the chart"), Items: this.ExportItems(exporters) });
            return items;
        }

        /**
         * The Type submenu (filtered by ValidTypes plus the original type; null when fewer than two types are valid or
         * ValidTypes is empty).
         * @param {object} data The render config.
         * @returns {?Array<object>} Items.
         */
        TypeItems(data) {
            let valid = null;
            if (Array.isArray(data.ValidTypes)) {
                if (!data.ValidTypes.length)
                    return null;
                valid = new Set(data.ValidTypes.map(t => String(t).toLowerCase()));
                valid.add(String(this.OrgType || "").toLowerCase());
                if (valid.size <= 1)
                    return null;
            }
            const ok = (...names) => !valid || names.some(n => valid.has(n));
            const type = data.type;
            const hor = data.options.indexAxis === "y";
            const defs = [
                [ok("bar", "barv"), "bar", false, _TF("Bar (vertical)"), _TF("Display the chart as a vertical bar chart"), "IconChartTypeBar"],
                [ok("bar", "barh"), "bar", true, _TF("Bar (horizontal)"), _TF("Display the chart as a horizontal bar chart"), "IconChartTypeBarHorizontal"],
                [ok("line", "linev"), "line", false, _TF("Line (vertical)"), _TF("Display the chart as a vertical line chart"), "IconChartTypeLine"],
                [ok("line", "lineh"), "line", true, _TF("Line (horizontal)"), _TF("Display the chart as a horizontal line chart"), "IconChartTypeLineHorizontal"],
                [ok("pie"), "pie", null, _TF("Pie"), _TF("Display the chart as a pie chart"), "IconChartTypePie"],
                [ok("doughnut"), "doughnut", null, _TF("Doughnut"), _TF("Display the chart as a doughnut chart"), "IconChartTypeDoughnut"],
                [ok("polararea"), "polarArea", null, _TF("Polar area"), _TF("Display the chart as a polar area chart"), "IconChartTypePolarArea"],
            ];
            return defs.filter(d => d[0]).map(([, t, h, text, title, icon]) => ({
                Text: text, Title: title, Icon: icon, Radio: true,
                Checked: type === t && (h === null || h === hor),
                OnClick: () => this.SetType(t, !!h),
            }));
        }

        /**
         * The Order submenu: Original, Labels and one item per dataset (a repeated pick flips ascending / descending).
         * @param {object} data The render config.
         * @returns {Array<object>} Items.
         */
        OrderItems(data) {
            const cur = this.Order;
            const desc = cur < 0;
            const kind = Math.abs(cur);
            const items = [{
                Text: _TF("Original"),
                Title: cur === 0 ? _TF("Ordered in the original order") : _TF("Use the original order"),
                Icon: "IconOrderOriginal" + (cur === 0 ? "Sel" : ""),
                Radio: true, Checked: cur === 0,
                OnClick: () => this.SetOrder(0),
            }];
            /**
             * One order item.
             * @param {number} n Order kind (1 labels, 2+ datasets).
             * @param {string} text Item text.
             * @param {string} title Tooltip.
             */
            const add = (n, text, title) => {
                const sel = kind === n;
                const isDesc = sel && desc;
                items.push({
                    Text: text, Title: title,
                    Icon: "IconOrder" + (isDesc ? "Descending" : "Ascending") + (sel ? "Sel" : ""),
                    Radio: true, Checked: sel,
                    OnClick: () => this.SetOrder(sel ? (isDesc ? n : -n) : n),
                });
            };
            const lSel = kind === 1, lDesc = lSel && desc;
            add(1, _TF("Labels"), lSel
                ? (lDesc ? _TF("Ordered by labels in descending order, select to order in ascending order") : _TF("Ordered by labels in ascending order, select to order in descending order"))
                : _TF("Order by labels in ascending order"));
            data.data.datasets.forEach((ds, i) => {
                const n = i + 2;
                const name = ds.label ?? _T("Series {0}", i + 1);
                const sel = kind === n, isDesc = sel && desc;
                add(n, name, sel
                    ? (isDesc ? _T("Ordered by values from {0} in descending order, select to order in ascending order", name) : _T("Ordered by values from {0} in ascending order, select to order in descending order", name))
                    : _T("Order by values from {0} in ascending order", name));
            });
            return items;
        }

        /**
         * The Color submenu: colored labels / values toggles, then (unless disabled) the data coloring modes and the fixed
         * gradient submenus.
         * @param {object} data The render config.
         * @returns {Array<object>} Items.
         */
        ColorItems(data) {
            const items = [
                {
                    Text: _TF("Colored labels"), Title: _TF("Colorize the labels"), Icon: "IconColoredLabels", Hint: "T",
                    Checked: this.ColorLabels, Keep: true, OnClick: () => this.ToggleColorLabels(),
                },
                {
                    Text: _TF("Colored values"), Title: _TF("Colorize the data point values"), Icon: "IconColoredValues", Hint: "V",
                    Checked: this.ColorValues, Keep: true, OnClick: () => this.ToggleColorValues(),
                },
            ];
            if (this.Options.DisableColorMenu || data.DisableColorMenu)
                return items;
            const c = this.Color;
            /**
             * A color mode item.
             * @param {?string} m Mode.
             * @param {string} text Text.
             * @param {string} title Tooltip.
             * @param {string} icon Icon class.
             * @param {string} [hint] Shortcut.
             * @returns {object} Item.
             */
            const mode = (m, text, title, icon, hint) => ({ Text: text, Title: title, Icon: icon, Hint: hint, Radio: true, Checked: c === m, OnClick: () => this.SetColor(m) });
            items.push("-", { Title: _TF("Data") },
                mode(null, _TF("Theme colors"), _TF("Use the colors of the theme (series, labels and groups keep their own color)"), "IconColors", "C"),
                mode(ColorMode.Original, _TF("Original"), _TF("Use the original colors"), "IconColorOriginal", "O"),
                mode(ColorMode.LabelText, _TF("Label hash"), _TF("Assign a color based on the text in the label, same text will always have the same color"), "IconColorLabelText", "L"),
                mode(ColorMode.RandomValue, _TF("Random value gradient"), _TF("The values will be colored according to a random hue gradient"), "IconColorRandomValueGradient", "G"),
                mode(ColorMode.RandomSeries, _TF("Random series gradient"), _TF("The series will be colored according to a random hue gradient"), "IconColorRandomSeriesGradient", "S"),
                mode(ColorMode.RandomMagnitude, _TF("Random magnitude gradient"), _TF("The values will be colored according to the magnitude of the value using a random hue gradient"), "IconColorRandomMagnitudeGradient", "M"));
            const values = [], series = [], mags = [];
            for (let i = 0; i < 12; ++i) {
                const c1 = FixedColors[i], c2 = FixedColors[(i + 2) % 12];
                const name = c1 + " - " + c2;
                const icon = "IconColorGradient" + i;
                values.push(mode("FixedValue" + i, name, _T("The values will be colored using a {0} to {1} gradient", c1, c2), icon));
                series.push(mode("FixedSeries" + i, name, _T("The series will be colored using a {0} to {1} gradient", c1, c2), icon));
                mags.push(mode("FixedMag" + i, name, _T("The values will be colored according to the magnitude of the value using a {0} to {1} gradient", c1, c2), icon));
            }
            items.push(
                { Text: _TF("Value gradients"), Icon: "IconColors", Title: _TF("Fixed gradients for labels (each value gets its own color)"), Items: values },
                { Text: _TF("Series gradients"), Icon: "IconColors", Title: _TF("Fixed gradients for series (each series gets its own color)"), Items: series },
                { Text: _TF("Magnitude gradients"), Icon: "IconColors", Title: _TF("Fixed gradients for value magnitudes (a gradient based on the value magnitude)"), Items: mags });
            return items;
        }

        /**
         * The Export submenu: server exporters, the export font size submenu and the client side svg / png downloads.
         * @param {Array<object>} exporters Server exporters.
         * @returns {Array<object>} Items.
         */
        ExportItems(exporters) {
            //  Every format has a one line description (Desc: what the file is good for; the server text's first line for server exporters)
            const items = exporters.map(x => ({ Text: x.Name, Title: x.Title, Desc: String(x.Title || "").split("\n")[0], Icon: x.IconClass, OnClick: () => this.Export(x) }));
            const scales = [
                [1, _TF("Display using the default size")],
                [1.25, _TF("Display using 125% of the chart size")],
                [1.5, _TF("Display using 150% of the chart size")],
                [2, _TF("Display using 200% of the chart size")],
                [2.5, _TF("Display using 250% of the chart size")],
            ];
            items.push({
                Text: _TF("Export font size"), Icon: "IconChartSize", Title: _TF("Options for modifying the font size of image exports"),
                Desc: _TF("Bigger text in the saved images", "One line description of the export font size submenu of a chart"),
                Items: scales.map(([s, title]) => ({
                    Text: _T("{0} %", Math.round(s * 100), "A font size of chart image exports as a percentage, {0} is the number"), Title: title, Icon: "IconChartSize" + Math.round(s * 100), Radio: true, Checked: this.Scale === s,
                    OnClick: () => {
                        this.Scale = s;
                    },
                })),
            });
            /**
             * A client side image export item.
             * @param {string} text Text.
             * @param {string} title Tooltip.
             * @param {string} desc One line description (what the file is good for).
             * @param {number} w Width.
             * @param {number} h Height.
             * @param {"Png"|"Svg"} fmt Format.
             * @returns {object} Item.
             */
            const img = (text, title, desc, w, h, fmt) => ({ Text: text, Title: title, Desc: desc, Icon: "IconFile" + fmt, OnClick: () => this.SaveImage(w, h, fmt) });
            const vector = _TF("Sharp at any size, for documents and slides", "One line description of the svg (vector image) chart export");
            items.push(
                img(_TF("Vector 16:9 (svg)"), _TF("Save the chart as a Scalable Vector Graphics file using the 16:9 aspect ratio"), vector, 1920, 1080, "Svg"),
                img(_TF("Vector A4 (svg)"), _TF("Save the chart as a Scalable Vector Graphics file suitable for printing on an A4 with 10 mm margins"), vector, A4.WL, A4.HL, "Svg"),
                img(_TF("Image HD (png)"), _TF("Save the chart as a 1920x1080 PNG image"), _TF("A picture for email, chat and web pages", "One line description of the HD png chart export"), 1920, 1080, "Png"),
                img(_TF("Image 1440p (png)"), _TF("Save the chart as a 2560x1440 PNG image"), _TF("A larger picture for big screens", "One line description of the 1440p png chart export"), 2560, 1440, "Png"),
                img(_TF("Image 4K UHD (png)"), _TF("Save the chart as a 3840x2160 PNG image"), _TF("A very large picture for 4K screens and posters", "One line description of the 4K png chart export"), 3840, 2160, "Png"),
                img(_TF("Image A4 low DPI (png)"), _T("Save the chart as a {0}x{1} PNG image, ideal for {2} DPI printing on an A4 with 10 mm margins", A4.WL, A4.HL, A4.LowDpi), _TF("A picture to print on an A4 page (smaller file)", "One line description of the A4 low dpi png chart export"), A4.WL, A4.HL, "Png"),
                img(_TF("Image A4 high DPI (png)"), _T("Save the chart as a {0}x{1} PNG image, ideal for {2} DPI printing on an A4 with 10 mm margins", A4.WH, A4.HH, A4.HighDpi), _TF("A picture to print on an A4 page in high quality", "One line description of the A4 high dpi png chart export"), A4.WH, A4.HH, "Png"));
            return items;
        }

        // ------------------------------------------------------------------ keyboard

        /**
         * Keyboard shortcuts (document keydown; not while a popup / menu is open, in inputs, with modifiers, or for Space on a
         * focused button): o / l / g / s / m color modes (unless the color menu is disabled), t / v toggles, d the table view, Space cycles the
         * order (unless the order menu is disabled).
         * @param {KeyboardEvent} ev The event.
         */
        OnKey(ev) {
            if (ev.defaultPrevented || ev.ctrlKey || ev.altKey || ev.metaKey || ev.shiftKey || ev.isComposing || !this.Main)
                return;
            if (Sw.Popup.IsOpen())
                return;
            const t = ev.target;
            if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.tagName === "SELECT" || t.isContentEditable))
                return;
            const colors = { c: null, o: ColorMode.Original, l: ColorMode.LabelText, g: ColorMode.RandomValue, s: ColorMode.RandomSeries, m: ColorMode.RandomMagnitude };
            const k = ev.key;
            if (Object.prototype.hasOwnProperty.call(colors, k)) {
                if (this.ColorDisabled)
                    return;
                this.SetColor(colors[k]);
            }
            else if (k === "t")
                this.ToggleColorLabels();
            else if (k === "v")
                this.ToggleColorValues();
            else if (k === "d")
                this.ToggleTable();
            else if (k === " ") {
                if (this.OrderDisabled || (t && (t.tagName === "BUTTON" || t.tagName === "A")))
                    return;
                this.CycleOrder();
            }
            else
                return;
            ev.preventDefault();
        }

        // ------------------------------------------------------------------ exports

        /**
         * The title of a config: options.plugins.title.text[0] or "Chart".
         * @param {object} data The config.
         * @returns {string} The title.
         */
        static Title(data) {
            const tt = data.options?.plugins?.title?.text;
            const t = Array.isArray(tt) ? tt[0] : tt;
            return t ? String(t) : _TF("Chart");
        }

        /**
         * Client side image export (download).
         * @param {number} width Width.
         * @param {number} height Height.
         * @param {"Png"|"Svg"} format Format.
         * @returns {Promise<void>}
         */
        async SaveImage(width, height, format) {
            const data = this.Fresh();
            const ctrl = new AbortController();
            const close = Sw.Dialog.Working({ Title: _TF("Exporting"), Text: width + "x" + height, Icon: "IconFile" + format, Cancel: () => ctrl.abort() });
            try {
                const img = await renderImage(data, width, height, false, format, format === "Png" ? this.Scale : 1, this.Fmt);
                if (ctrl.signal.aborted)
                    return;
                Sw.Net.Download(img, ChartView.Title(data) + "." + format.toLowerCase());
                await Sw.Util.Delay(250);
            }
            catch (e) {
                Sw.Fail(_T("Failed to export: {0}", e && e.message ? e.message : e));
            }
            finally {
                close();
            }
        }

        /**
         * Server side export: POST Api/ExportChart {Data, DataStr, ExportAs, Options: {Filename}} (input types 1 / 2 send an
         * A4 300 dpi png / svg as DataStr). A file reply is downloaded, a reply with only a Name is a link (copied and opened).
         * @param {object} x The exporter menu item (Name, Title, IconClass, Data = input type).
         * @returns {Promise<void>}
         */
        async Export(x) {
            let data = this.Fresh();
            const ctrl = new AbortController();
            const close = Sw.Dialog.Working({ Title: _TF("Exporting"), Text: x.Title, Icon: x.IconClass, Cancel: () => ctrl.abort() });
            try {
                const type = parseInt(x.Data);
                const title = ChartView.Title(data);
                let dataStr = null;
                if (type === 1 || type === 2) {
                    dataStr = await renderImage(data, A4.WH, A4.HH, false, type === 1 ? "Png" : "Svg", 1, this.Fmt);
                    data = null;
                }
                if (ctrl.signal.aborted)
                    return;
                const file = await Sw.Net.Send(Sw.Net.Api("ExportChart"), { Data: data, DataStr: dataStr, ExportAs: x.Name, Options: { Filename: title } }, { Signal: ctrl.signal });
                if (!file)
                    Sw.Fail(_TF("No data!"));
                else if (file.Mime && file.Data) {
                    Sw.Net.Download("data:" + file.Mime + ";base64," + file.Data, file.Name);
                    await Sw.Util.Delay(250);
                }
                else if (file.Name) {
                    const url = new URL(file.Name, Sw.Url.Root).href;
                    await Sw.Clipboard.Copy(url);
                    //  (in a new tab, like the original chart)
                    window.open(url, "_blank");
                }
                else
                    Sw.Fail(_TF("Empty data!"));
            }
            catch (e) {
                if (!ctrl.signal.aborted)
                    Sw.Fail(_T("Failed to export: {0}", e && e.message ? e.message : e));
            }
            finally {
                close();
            }
        }
    }

    // ================================================================== public api

    /**
     * Add a live chart that gets its config from the server.
     * @param {string} url Data url.
     * @param {HTMLElement} [parent=document.body] Element to add the chart to.
     * @param {*} [params] Optional POST argument.
     * @param {function():void} [onFirstLoad] Called once after the first fetch (success or failure).
     * @param {ChartOptions} [options] Options.
     * @returns {Promise<HTMLElement>} The SysWeaver-Chart element (after the first fetch when options.WaitFirst). The ChartView is
     * available as element.SwChart.
     */
    async function addChart(url, parent, params, onFirstLoad, options) {
        options = options || new ChartOptions();
        const v = new ChartView(url, parent || document.body, params, onFirstLoad, options);
        v.E.SwChart = v;
        if (options.WaitFirst)
            await v.Poll();
        else
            v.Poll();
        return v.E;
    }

    // ================================================================== page entry

    /**
     * Normalize a color url parameter: 3 or 6 characters not starting with "r" are hex without "#".
     * @param {string} t Parameter value.
     * @returns {string} Css color.
     */
    function parseCol(t) {
        return (t.length === 3 || t.length === 6) && t[0] !== "r" ? "#" + t : t;
    }

    /**
     * Page entry of chart.html. Url parameters (keys are case insensitive):
     * q (data url, required), p (json POST argument), m / mo / mc = false (no menu / order menu / color menu), type,
     * l / novalues / nolabels = true and valuelabel = 0-6 (label mode), minvalue, maxvalue, maxcount, bordersize, bordercol,
     * textcol, backgroundcol, font, font-size, font-style, font-weight, scale-font, scale-font-size, scale-font-style,
     * scale-font-weight, line-height, label-padding, label-radius, legend = false, aspect, transparent = true, center = true,
     * order (initial order: 0 original, +-1 labels, +-(n+2) by dataset n, negative = descending), solid = true (solid theme
     * colors on axis charts), timeoffset = minutes / local (UTC time labels shown in another time zone, see shiftTimeLabels),
     * axistitles = false (no automatic axis titles; also off with m=false), vhist (same site url of a history chart: a two value gauge
     * shows the mean of its last vhistn (default 3) values, see ChartView.ApplyValueHistory), log = true (logarithmic value axis);
     * the method graph (Api/PerformanceChart) gets an explanation line, busiest first and on phones the 10 busiest as horizontal bars
     * (fillscreen and the other core flags are handled by the core). Without q an in-page
     * "No chart selected" explanation is shown.
     * @returns {Promise<void>}
     */
    async function chartMain() {
        const removeLoader = Sw.Page.Loading();
        try {
            document.body.style.overflow = "hidden";
            const ps = Sw.Url.Params();
            const url = ps.get("q");
            if (!url) {
                //  Expected user error: explain in the page (no toast / console error)
                document.title = _TF("Chart", "Title of the chart page when it was opened without a chart");
                document.body.style.overflow = "";
                document.body.append(Sw.el("div.ChartMissing", { attrs: { role: "status" } },
                    Sw.Icon("info", "ChartMissingIcon"),
                    Sw.el("h1.ChartMissingTitle", { text: _TF("No chart selected", "Heading of the chart page when it was opened without a chart (the q url parameter is missing)") }),
                    Sw.el("p.ChartMissingText", { text: _TF("This page shows a live chart. Open a chart from the menu, use \"Show as chart\" on a table column, or add the chart data url as the q parameter, example: chart.html?q=../Api/PerformanceChart", "Explanation on the chart page when it was opened without a chart (the q url parameter is missing)") })));
                return;
            }
            //  Security: only chart data urls of this site (a relative url or the same origin, http(s)). A crafted link must not make the
            //  page fetch (and draw as ours) data from another site; nothing is requested.
            if (!Sw.Url.SafeSameOrigin(url)) {
                document.title = _TF("Chart", "Title of the chart page when it was opened without a chart");
                document.body.style.overflow = "";
                document.body.append(Sw.el("div.ChartMissing", { attrs: { role: "alert" } },
                    Sw.Icon("warning", "ChartMissingIcon"),
                    Sw.el("h1.ChartMissingTitle", { text: _TF("This chart cannot be shown", "Heading of the chart page when the q url parameter points to another web site (not allowed)") }),
                    Sw.el("p.ChartMissingText", { text: _TF("The chart data address (the q parameter) must be on this site. Open the chart from the menu instead.", "Explanation on the chart page when the q url parameter points to another web site (not allowed)") })));
                return;
            }
            //  Design v2 page anatomy (Design v2, PORTING.md B10): a chart page shown directly in the app shell's content frame (a menu item, ex:
            //  "Method calls chart", or a chart opened full page) gets a compact page header (the menu name, else the chart title) and the
            //  chart in a card that takes the rest of the height. Embedded charts (tiles in a page, chat attachments: not the shell's
            //  content frame) and charts without a menu (m=false) or a background (transparent=true) stay bare.
            let shellPage = false;
            try {
                shellPage = window.top !== window && window.parent === window.top && !!window.top.SysWeaverApp;
            }
            catch {
                shellPage = false;
            }
            /** @type {?object} The page header (Sw.PageHeader), null for embedded charts. */
            const pageHead = shellPage && ps.get("m") !== "false" && ps.get("transparent") !== "true" && Sw.PageHeader
                ? Sw.PageHeader({ Title: document.title, Compact: true, Class: "ChartHead" })
                : null;
            if (pageHead) {
                document.body.classList.add("ChartWithHead");
                document.body.append(pageHead.E);
            }
            const p = ps.get("p");
            const params = p ? JSON.parse(p) : null;
            /**
             * A float url parameter.
             * @param {string} n Name.
             * @returns {?number} The value (null if missing).
             */
            const num = n => {
                const v = ps.get(n);
                return v ? parseFloat(v) : null;
            };
            const o = new ChartOptions();
            o.DisableMenu = ps.get("m") === "false";
            o.DisableOrderMenu = ps.get("mo") === "false";
            o.DisableColorMenu = ps.get("mc") === "false";
            o.Transparent = ps.get("transparent") === "true";
            if (ps.get("l") === "true")
                o.ValueLabel = 1;
            if (ps.get("novalues") === "true")
                o.ValueLabel = 2;
            if (ps.get("nolabels") === "true")
                o.ValueLabel = 4;
            if (ps.get("valuelabel"))
                o.ValueLabel = parseInt(ps.get("valuelabel"));
            o.MinValue = num("minvalue");
            o.MaxValue = num("maxvalue");
            o.MaxCount = ps.get("maxcount") ? parseInt(ps.get("maxcount")) : null;
            o.Order = parseInt(ps.get("order") || "0") || 0;
            o.Solid = ps.get("solid") === "true";
            const tz = ps.get("timeoffset");
            if (tz)
                o.TimeOffset = tz === "local" ? "local" : (Number.isFinite(+tz) ? +tz : null);
            o.AxisTitles = ps.get("m") !== "false" && ps.get("axistitles") !== "false";
            const vh = ps.get("vhist");
            if (vh && Sw.Url.SafeSameOrigin(vh)) {
                o.ValueHistory = vh;
                o.ValueHistoryCount = parseInt(ps.get("vhistn") || "3") || 3;
            }
            o.LogScale = ps.get("log") === "true";
            o.Shadow = ps.get("shadow") === "true";
            o.TableView = ps.get("table") === "true";
            let compactLegend = false;
            /** @type {boolean} The legend goes to the right of the chart (method graph on low screens: the plot keeps the height). */
            let legendRight = false;
            /** @type {?ChartView} The live chart (set once added; the method graph "Show all" button rebuilds it). */
            let chartView = null;

            //  The method graph (performance chart): busiest functions first (unless the url sets an order), on phones only the
            //  busiest as horizontal bars (readable names), and one line saying what the bars mean
            if (isPerformanceChart(url) && ps.get("m") !== "false") {
                if (ps.get("order") === null)
                    o.Order = -3;
                //  Phones (portrait and landscape): the 10 busiest as horizontal bars (readable names; vertical bars were squashed into
                //  a strip about 50 px high in landscape)
                const phone = window.innerWidth < 600;
                const low = !phone && window.innerHeight < 500;
                //  horizontal bars on every screen (readable names, never rotated labels: Design v2, PORTING.md B11); every bar
                //  gets its (shortened) name (applyDensity; Chart.js' auto skip named only 1 of 10 bars in landscape)
                o.IndexAxis = "y";
                o.LabelAll = true;
                if (phone || low) {
                    //  A compact legend (nine colors took a third of the phone screen; OnDefaults below); beside the chart on low screens
                    compactLegend = true;
                    legendRight = low;
                }
                //  The busiest functions only by default (10 on phones, 25 else: about 65 of 99 bars were identical boxes at 1 on a
                //  large screen), with a "Show all" button in the explanation
                const top = (phone || low) ? 10 : 25;
                if (ps.get("maxcount") === null)
                    o.MaxCount = top;
                const limit = o.MaxCount;
                //  (solid / faint, not dark / light: on a dark theme the running part is the bright one, S8)
                const text = _TF("Each bar is one function: the faint bar is the most calls it ran at the same time, the solid part the calls running now. The busiest functions come first (the menu has other orders and a log scale).", "One line explanation above the method graph (performance chart)");
                const countNote = Sw.el("span.ChartNoteCount");
                const allB = limit ? Sw.el("button.ChartNoteMore.ChartNoteAll", { type: "button" }) : null;
                /** Update the "Showing the n busiest" text and the "Show all" / "Show the n busiest" button. */
                const setCount = () => {
                    countNote.textContent = " " + (o.MaxCount
                        ? _T("Showing the {0} busiest.", o.MaxCount, "Added to the explanation above the method graph, {0} is the number of functions shown")
                        : _TF("Showing all functions.", "Added to the explanation above the method graph after Show all"));
                    if (allB)
                        allB.textContent = o.MaxCount
                            ? _TF("Show all", "Button in the explanation above the method graph that shows all monitored functions instead of the busiest few")
                            : _T("Show the {0} busiest", limit, "Button in the explanation above the method graph that shows only the busiest functions again, {0} is their number");
                };
                setCount();
                if (allB)
                    allB.addEventListener("click", ev => {
                        if (Sw.BadClick(ev))
                            return;
                        o.MaxCount = o.MaxCount ? null : limit;
                        setCount();
                        if (chartView)
                            chartView.Rebuild(true);
                    });
                //  The explanation, the log scale note and (low screens, where the explanation is one line) a "More" / "Less" button
                const logNote = Sw.el("span.ChartNoteLog");
                const more = Sw.el("button.ChartNoteMore", { type: "button", attrs: { "aria-expanded": "false" } });
                /**
                 * Show the whole explanation or one line (low screens).
                 * @param {boolean} open Show all of it.
                 */
                const setOpen = open => {
                    document.body.classList.toggle("ChartNoteOpen", open);
                    more.textContent = open
                        ? _TF("Less", "Button after the explanation above the method graph on low screens that shows only its first line again")
                        : _TF("More", "Button after the explanation above the method graph on low screens (one line) that shows all of it");
                    more.setAttribute("aria-expanded", open ? "true" : "false");
                };
                more.addEventListener("click", ev => {
                    if (!Sw.BadClick(ev))
                        setOpen(!document.body.classList.contains("ChartNoteOpen"));
                });
                setOpen(false);
                const note = Sw.el("div.ChartNote", { attrs: { title: text } }, Sw.el("span.ChartNoteText", { text }), countNote, logNote, allB, more);
                document.body.classList.add("ChartWithNote");
                document.body.append(note);
                //  A stable value axis: linear unless the url sets log or the viewer chose "Log scale" in the menu (remembered per
                //  viewer in localStorage "SysWeaver.Chart.MethodLog"). No automatic switch: the same bars keep their length between
                //  loads. A log axis is named in the note and in the axis title (applyAxisTitles).
                const logKey = "SysWeaver.Chart.MethodLog";
                const st = Sw.Storage && Sw.Storage.Local;
                if (ps.get("log") === null && st)
                    o.LogScale = st.Get(logKey) === "1";
                /**
                 * The log scale note in the explanation.
                 * @param {boolean} on Log scale on.
                 */
                const setLogNote = on => logNote.textContent = on
                    ? " " + _TF("The value axis uses a log scale (each step is 10 times bigger; turn it off in the menu).", "Added to the explanation above the method graph when the value axis is logarithmic")
                    : "";
                setLogNote(o.LogScale);
                o.OnLogScale = on => {
                    setLogNote(on);
                    if (st)
                        st.Set(logKey, on ? "1" : "0");
                };
            }

            o.OnServerData = config => {
                const type = ps.get("type");
                if (type !== null)
                    config.type = type;
                //  title=false: no title inside the chart (the embedding card shows it, Design v2 cards: "CPU use · last 3 minutes")
                if (ps.get("title") === "false") {
                    const pl = config.options && config.options.plugins;
                    if (pl && pl.title)
                        pl.title.display = false;
                }
                //  The page header shows the menu name (Sw.MenuTitle) when the page is a menu item; else the chart title
                if (pageHead && pageHead.FromMenu)
                    return;
                if (config.Title != null && document.title !== config.Title)
                    document.title = config.Title;
                if (pageHead && config.Title != null)
                    pageHead.SetTitle(config.Title);
            };

            o.OnFixedData = config => {
                const ds0 = config.data.datasets[0];
                if (ds0) {
                    if (ps.get("bordersize") !== null)
                        ds0.borderWidth = parseFloat(ps.get("bordersize"));
                    if (ps.get("bordercol") !== null)
                        ds0.borderColor = parseCol(ps.get("bordercol"));
                }
                /**
                 * Set a tick font property on the existing x, x2, y and r scales.
                 * @param {string} prop Font property.
                 * @param {*} v Value.
                 */
                const tickFont = (prop, v) => ["x", "x2", "y", "r"].forEach(a => setPath(config, "options.scales.*" + a + ".ticks.font." + prop, v));
                let v = ps.get("scale-font-size") ?? ps.get("font-size");
                if (v !== null)
                    tickFont("size", parseFloat(v));
                v = ps.get("scale-font") ?? ps.get("font");
                if (v !== null)
                    tickFont("family", v);
                v = ps.get("scale-font-style") ?? ps.get("font-style");
                if (v !== null)
                    tickFont("style", v);
                v = ps.get("scale-font-weight") ?? ps.get("font-weight");
                if (v !== null)
                    tickFont("weight", parseFloat(v));
                v = ps.get("aspect");
                if (v !== null)
                    config.options.maintainAspectRatio = v === "true";
            };

            o.OnDefaults = def => {
                const dl = def.plugins.datalabels;
                if (compactLegend) {
                    const ll = def.plugins.legend.labels;
                    ll.boxWidth = 12;
                    ll.boxHeight = 12;
                    ll.padding = 6;
                    ll.font = Object.assign({}, ll.font, { size: 12 });
                    if (legendRight)
                        def.plugins.legend.position = "right";
                }
                if (ps.get("legend") === "false")
                    def.plugins.legend.display = false;
                let v = ps.get("textcol");
                if (v !== null)
                    def.color = dl.color = parseCol(v);
                v = ps.get("backgroundcol");
                if (v !== null)
                    def.backgroundColor = dl.backgroundColor = parseCol(v);
                v = ps.get("bordercol");
                if (v !== null)
                    def.borderColor = dl.borderColor = parseCol(v);
                v = ps.get("font");
                if (v !== null)
                    dl.font.family = def.font.family = v;
                v = ps.get("font-style");
                if (v !== null)
                    dl.font.style = def.font.style = v;
                v = ps.get("font-size");
                if (v !== null) {
                    v = parseFloat(v);
                    dl.font.size = def.font.size = v;
                    dl.borderRadius = 8 * v / 11;
                    dl.padding = 4 * v / 11;
                }
                v = ps.get("label-padding");
                if (v !== null)
                    dl.padding = parseFloat(v);
                v = ps.get("label-radius");
                if (v !== null)
                    dl.borderRadius = parseFloat(v);
                v = ps.get("font-weight");
                if (v !== null)
                    dl.font.weight = def.font.weight = v;
                v = ps.get("line-height");
                if (v !== null)
                    def.font.lineHeight = parseFloat(v);
            };

            //  ChartClick to the parent window (same origin, as before; at top level the window itself)
            o.OnClick = (label, value, datasetIndex, dataIndex) => {
                try {
                    window.parent.postMessage({ Type: "ChartClick", Label: label, Value: value, DatasetIndex: datasetIndex, DataIndex: dataIndex }, "/");
                }
                catch (e) {
                    console.warn("Chart: ChartClick failed: " + e);
                }
            };

            const el = await addChart(url, document.body, params, removeLoader, o);
            chartView = el.SwChart || null;
            if (ps.get("center") === "true")
                el.classList.add("ChartCenter");
        }
        finally {
            removeLoader();
        }
    }

    /**
     * @namespace Sw.Chart - the chart viewer api.
     */
    Sw.Chart = {
        /** Add a live chart (see addChart). */
        Add: addChart,
        /** The options class. */
        Options: ChartOptions,
        /** The live chart class. */
        View: ChartView,
        /** Get the server exporters (cached). */
        GetExporters: getExporters,
        /** Render a config as a png / svg data uri. */
        RenderImage: renderImage,
        /** Apply a data label mode (0-6) to a config. */
        ApplyLabelMode: applyLabelMode,
        /** Gradient factories (name -> fn). */
        GradientFns,
        /** Names of the fixed gradient hues. */
        FixedColors,
        /** A4 export sizes. */
        A4,
        /** The page entry. */
        Main: chartMain,
    };

    /**
     * Legacy alias of Sw.Chart.Options (the old CanvasChartOptions).
     * @deprecated Use Sw.Chart.Options.
     */
    window.CanvasChartOptions = ChartOptions;

    /**
     * Legacy alias object of the old static CanvasChart class (addChart, getChartExportMenu, tweakXmlText, the label mode
     * functions, FixedColors, GradientFns, CS, MenuExportItems).
     * @deprecated Use Sw.Chart.
     */
    window.CanvasChart = {
        addChart,
        getChartExportMenu: getExporters,
        tweakXmlText: tweakXml,
        FixedColors,
        GradientFns,
        CS: ScriptUrl,
        /**
         * The loaded server exporters.
         * @returns {?Array<object>} The exporters (null before the first load).
         */
        get MenuExportItems() {
            return exportersLoaded;
        },
        addDataLabels: c => applyLabelMode(c, 1),
        onlyDataLabels: c => applyLabelMode(c, 2),
        firstLineDataLabels: c => applyLabelMode(c, 3),
        noLabels: c => applyLabelMode(c, 4),
        valueLabels: c => applyLabelMode(c, 5),
        bareValueLabels: c => applyLabelMode(c, 6),
        formattedValueLabels: c => applyLabelMode(c, 0),
    };

    /** Legacy global page entry. */
    window.chartMain = chartMain;

    if (Script && Script.hasAttribute("data-main"))
        Sw.Page.Main(chartMain);
})();