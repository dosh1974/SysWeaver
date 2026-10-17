


/**
 * @file chart.js - first-party chart viewer logic of SysWeaver.MicroService.ChartJs
 * (not to be confused with the vendored Chart.js library in external/chart.js).
 *
 * Loading: included by chart.html as a deferred classic script (not a module) after
 * ../common/common.js, external/chart.js (global `Chart`), external/hammerjs.js and
 * external/chartjs_plugin_datalabels.js (global `ChartDataLabels`). chart.html calls `chartMain()`
 * from `<body onload>`. Consumers never include this file directly; they open/embed
 * `chart/chart.html?q=<url>` in an iframe or link (explore/table.js, app/application.js menu type 6,
 * ServerManager server.js / serviceInfo.js, C# ChartJsService and UserStorageChartExporter links).
 *
 * Globals defined: classes `Random`, `CanvasChartOptions`, `CanvasChart`; functions `setValue`, `chartMain`.
 * The only code executed at load time is the evaluation of the static fields of `CanvasChart`
 * (`CS` captures `document.currentScript.src`).
 *
 * Dependencies (globals from other files):
 * - common/common.js: getUrlParams, getRequest, sendRequest, includeJs, includeCss, AddLoading, Fail, Info,
 *   delay, ColorIcon, ValueFormat (toString, copyToClipboardInfo), Base64EncodeString, GetAbsolutePath,
 *   Open, InterOp.
 * - app/application.js (loaded lazily by CanvasChart.getChartExportMenu): PopUpMenu, PopUpWorking,
 *   WebMenu, WebMenuItem.
 * - Vendored: Chart (Chart.js 4.x), ChartDataLabels, C2S (canvas2svg, loaded lazily for SVG export).
 * - common/theme.css variables: --ThemeBackground, --ThemeBackgroundRGB, --ThemeFont, --ThemeMain,
 *   --ThemeMainRGB, --ThemeAcc1.
 *
 * Server contract: the `q` URL returns a Chart.js config extended with the C# `ChartJsConfig` fields
 * (RefreshRate, ValidTypes, Precision, Title, ValuePrefix, ValueSuffix, ValueLabel, DisableOrderMenu,
 * DisableColorMenu). Exporters come from GET ../Api/GetChartExporters, exports are POSTed to
 * ../Api/ExportChart.
 *
 * Conventions: almost all logic lives in one closure inside `CanvasChart.addChart`; view state (type,
 * order, colours, export scale) lives only in that closure (no localStorage/sessionStorage). `addChart`
 * mutates the global `Chart.defaults` and calls `Chart.register` on every call, so one chart per page /
 * iframe is assumed. Labels act as identity keys in the live-update diff.
 */


/**
 * Static-only seeded pseudo random number generator (sfc32 style, 4 x 32 bit state).
 * Used for reproducible colour schemes (random gradients seeded per menu click, label-hash colours).
 * Never instantiated.
 */
class Random {


    /**
     * Create an array of 4 random integers that can be used as a seed (from Math.random()).
     * @returns {number[]} Four int32 values.
     */
    static GetRandomSeed() {
        return [
            (Math.random() * 4294967295) | 0,
            (Math.random() * 4294967295) | 0,
            (Math.random() * 4294967295) | 0,
            (Math.random() * 4294967295) | 0
        ];
    }

    /**
     * Create an array of 4 integers for a given string (using a non-secure hash: four shift/add rolling hashes).
     * The same text always gives the same seed.
     * @param {string} text - Text to hash.
     * @returns {number[]} Four int32 values.
     */
    static SeedFromString(text) {
        let a = 98764321234;
        let b = 38764321237;
        let c = 18764321309;
        let d = 8764321243;
        const l = text.length;
        for (let i = 0; i < l; ++i) {
            const e = text.charCodeAt(i);
            a = (a << 5) - a + e;
            b = (b << 7) - b + e;
            c = (c << 8) + c + e;
            d = (d << 13) - d + e;
            a |= 0;
            b |= 0;
            c |= 0;
            d |= 0;
        }
        return [a, b, c, d];
    }

    /**
     * Create a seeded generator function.
     * Seed can be: a string (hashed with SeedFromString), an array of 4 integers (the state words),
     * an integer (used as the first word, `seed | 0`, with the fixed constants 19, 74, 11 for the others).
     * @param {string|number[]|number} seed - The seed.
     * @returns {function():number} Generator returning floats in [0, 1).
     */
    static Create(seed) {
        let a, b, c, d;
        if (typeof seed === "string")
            seed = Random.SeedFromString(seed);
        if (Array.isArray(seed)) {
            a = seed[0];
            b = seed[1];
            c = seed[2];
            d = seed[3];
        } else {
            a = seed | 0;
            b = 19;
            c = 74;
            d = 11;
        }
        return function () {
            a |= 0; b |= 0; c |= 0; d |= 0;
            let t = (a + b | 0) + d | 0;
            d = d + 1 | 0;
            a = b ^ b >>> 9;
            b = c + (c << 3) | 0;
            c = (c << 21 | c >>> 11);
            c = c + t | 0;
            return (t >>> 0) / 4294967296;
        }
    }
}

/**
 * Plain option bag passed to `CanvasChart.addChart`; created with `new CanvasChartOptions()` by `chartMain`
 * (or defaulted inside `addChart`).
 * The undeclared optional fields `MinValue`, `MaxValue` and `MaxCount` (numbers) are also read when present
 * (by FilterData / Limit inside addChart; `chartMain` sets them from URL parameters).
 */
class CanvasChartOptions {

    /** @type {boolean} When true, no menu icon and no keyboard handler are created. */
    DisableMenu = false;
    /** @type {?function(object):(void|Promise<void>)} Awaited on each freshly fetched server config before it is stored as the original (orgDataStr). */
    OnServerData = null;
    /** @type {?function(object):(void|Promise<void>)} Awaited on the config after ApplyChanges, before it is rendered / diffed (only in DoUpdate). */
    OnFixedData = null;
    /** @type {?function(object):void} Called with `Chart.defaults` by UpdateColor after the theme has been copied into the defaults. */
    OnDefaults = null;
    /** @type {?function(string, any, number, number):void} Called from the chart options.onClick with (label, value, datasetIndex, dataIndex) of the first clicked element. */
    OnClick = null;
    /** @type {boolean} Adds class `Transparent` to the <SysWeaver-Chart> element (no background). */
    Transparent = false;
    /** @type {boolean} When true addChart awaits the first DoUpdate before returning; otherwise it returns immediately. */
    WaitFirst = true;
    /** @type {?number} Data label mode 0-6 overriding the config's ValueLabel (see ApplyChanges in addChart). */
    ValueLabel = null;
    /** @type {boolean} Hides the Order menu and disables the Space sort shortcut. */
    DisableOrderMenu = false;
    /** @type {boolean} Hides the data colouring part of the Color menu and disables the o/l/g/s/m shortcuts. */
    DisableColorMenu = false;
}

/**
 * Static-only class holding shared state, the `addChart` factory and the static data label strategies.
 * Never instantiated. Each `addChart` call creates one live, self-refreshing chart whose state lives in
 * the closure of that call.
 */
class CanvasChart {


    /**
     * Get the server exporter menu items, loading the lazy export/menu dependencies on first use.
     * Loads in parallel (relative to CanvasChart.CS) external/canvas2svg.js, ../app/application.js and
     * ../app/application.css, plus GET ../Api/GetChartExporters (relative to the page); caches the result
     * in CanvasChart.MenuExportItems.
     * @returns {Promise<WebMenuItem[]>} Exporter items (Name = exporter id, Title = description, IconClass,
     *   Data = ChartExportInputTypes as string: "0" Data, "1" Png, "2" Svg).
     */
    static async getChartExportMenu() {
        const m = CanvasChart.MenuExportItems;
        if (m)
            return m;
        const current = CanvasChart.CS;
        const [js1, js2, css, data] = await Promise.all([
            includeJs(current, "external/canvas2svg.js"),
            includeJs(current, "../app/application.js"),
            includeCss(current, "../app/application.css"),
            getRequest("../Api/GetChartExporters")
        ]);
        CanvasChart.MenuExportItems = data;
        return data;
    }

    /** @type {string} URL of this script (document.currentScript.src captured at load); base for the lazy includes. */
    static CS = document.currentScript.src;
    /** @type {?WebMenuItem[]} Cache of the server exporter list (set by getChartExportMenu). */
    static MenuExportItems = null;

    /**
     * Parse a text as XML, let a callback mutate the document and serialize it back.
     * Used to rewrite the SVG root element on SVG export.
     * @param {string} text - XML text (parsed as "text/xml").
     * @param {function(XMLDocument):void} onXmlDoc - Callback that mutates the parsed document.
     * @returns {string} The re-serialized XML.
     */
    static tweakXmlText(text, onXmlDoc) {
        const xmlDoc = new DOMParser().parseFromString(text, "text/xml");
        onXmlDoc(xmlDoc);
        return new XMLSerializer().serializeToString(xmlDoc);
    }

    /**
     * Display names of the 12 hue steps (30 degrees apart) used by the fixed gradient menu items
     * (FixedValue/FixedSeries/FixedMag 0-11). Index 4 and 5 are both "Green".
     * @type {string[]}
     */
    static FixedColors =
        [
            "Red",
            "Orange",
            "Yellow",
            "Lime green",
            "Green",
            "Green",
            "Cyan",
            "Sky blue",
            "Blue",
            "Purple",
            "Magenta",
            "Pink",
        ];

    /**
     * Gradient factories for gradient colour strings "name(stop;color;stop;color...)", keyed by name:
     * down (top to bottom), up (bottom to top), left (x0 to x1), right (x1 to x0) linear gradients and
     * cone (conic gradient starting at -90 degrees around the centre). Each is called as (ctx, x0, y0, x1, y1).
     * @type {Map<string, function(CanvasRenderingContext2D, number, number, number, number):CanvasGradient>}
     */
    static GradientFns = new Map([
        ["down", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(0, y0, 0, y1) ],
        ["up", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(0, y1, 0, y0) ],
        ["left", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(x0, 0, x1, 0) ],
        ["right", (ctx, x0, y0, x1, y1) => ctx.createLinearGradient(x1, 0, x0, 0) ],
        ["cone", (ctx, x0, y0, x1, y1) => ctx.createConicGradient(-Math.PI * 0.5, Math.floor((x0 + x1) * 0.5), (y0 + y1) * 0.5) ],
    ]);

    /**
     * Add a chart that get it's data from the server (as a achild to some element)
     *
     * Creates `<SysWeaver-Chart>` (class `Transparent` if requested) with a `<canvas>` child appended to
     * `toElement`, sets up the menu icon (unless DisableMenu), the keyboard handler (assigned to
     * `toElement.onkeydown`, overwriting any existing handler), registers the Chart.js plugins
     * (customCanvasBackgroundColor, dropShadow, ChartDataLabels) and replaces global Chart.defaults
     * (on every call), subscribes to InterOp "Theme.Changed", then starts the polling loop DoUpdate
     * (GET `url`, or POST `tableParams` to `url` when given; repeats every RefreshRate ms).
     *
     * Everything else is a closure: per-chart view state (forceChartType, forcedIndexAxis, forcedColor,
     * forcedColorSeed, forceColorLabels, forceColorValues, forcedOrder, chartScale), the original server
     * config as JSON (orgDataStr, every rebuild starts from it) and the live Chart instance (main).
     * @param {string} url The url to the API that returns the chart options
     * @param {HTMLElement} [toElement=document.body] An optional element to add the canvas and button to (defaults to the body)
     * @param {any} [tableParams] An optional parameter (as an object) that is send to the url
     * @param {function()} [onFirstLoad] An optional function that is executed after the first successful request
     *   (also called once after the first failure)
     * @param {CanvasChartOptions} [chartOptions] Optional options (a default instance is created when missing)
     * @returns {Promise<HTMLElement>} The `<SysWeaver-Chart>` element (resolved after the first fetch when chartOptions.WaitFirst).
     */
    static async addChart(url, toElement, tableParams, onFirstLoad, chartOptions) {
        if (!toElement)
            toElement = document.body;
        if (!chartOptions)
            chartOptions = new CanvasChartOptions();
        // A4 landscape export sizes: 297x210 mm minus a 10 mm margin on each side (277x190 mm printable),
        // converted to pixels at 150 DPI (A4_wl x A4_hl = 1635x1122) and 300 DPI (A4_wh x A4_hh = 3271x2244).
        const margin = 10;
        const ldpi = 150;
        const hdpi = 300;
        const dpiToMmPx = 0.0393700787;
        const A4_w = 297 - margin * 2;
        const A4_h = 210 - margin * 2;
        const A4_wl = (A4_w * ldpi * dpiToMmPx) | 0;
        const A4_hl = (A4_h * ldpi * dpiToMmPx) | 0;
        const A4_wh = (A4_w * hdpi * dpiToMmPx) | 0;
        const A4_hh = (A4_h * hdpi * dpiToMmPx) | 0;

        // WebMenuItem flag bits (application.js): checked = 2, readonly = 1.
        const checked = 2;
        const readonly = 1;
        const checkedReadonly = checked | readonly;

        // The <SysWeaver-Chart> container and its <canvas> (canvas is replaced by RebuildChart).
        const chart = document.createElement("SysWeaver-Chart");
        if (chartOptions.Transparent)
            chart.classList.add("Transparent");
        toElement.appendChild(chart);
        let canvas = document.createElement("canvas");

        chart.appendChild(canvas);


        // Per-chart view state chosen in the menu / with keys:
        // forceChartType - Chart.js type forced by the Type menu (null = server type);
        // forcedIndexAxis - "x" or "y" (horizontal) forced together with the type;
        // forcedColor - colour mode name (RandomValueGradient, RandomSeriesGradient, RandomMagnitudeGradient,
        //   LabelText, FixedValue<i>, FixedSeries<i>, FixedMag<i>) or null = original colours;
        // forcedColorSeed - Random seed picked by SetChartColor for the random modes.
        let forceChartType = null;
        let forcedIndexAxis = null;
        let forcedColor = null;
        let forcedColorSeed = null;


        // Coloured labels (legend / axis ticks) and coloured values (datalabels) toggles.
        let forceColorLabels = false;
        let forceColorValues = false;


        // Sort order: 0 = original, +-1 = by label, +-(n+2) = by values of dataset n; negative = descending.
        let forcedOrder = 0; // 0 = Orignal, 1 = Label name, 2+ = datasets

        // orgDataStr - JSON of the last server config (after OnServerData), every rebuild starts from it.
        // orgDataType - the original type plus "h"/"v" (indexAxis), used to filter the Type menu.
        // main - the live Chart instance. chartScale - export font scale (canvas.Scale, PNG export only).
        let orgDataStr = null;
        let orgDataType = null;
        let main = null;
        let chartScale = 1;

        // Colour mode names (also used in icon class names "IconColor" + name).
        const colorRandomValueGradient = "RandomValueGradient";
        const colorRandomSeriesGradient = "RandomSeriesGradient";
        const colorRandomMagnitudeGradient = "RandomMagnitudeGradient";
        const colorLabelText = "LabelText";

        // Chart types without x/y axes (pie, doughnut, polarArea) and the type that keeps the "r" scale (polarArea).
        const noAxisTypes = new Map();
        noAxisTypes.set("pie", 1);
        noAxisTypes.set("doughnut", 1);
        noAxisTypes.set("polarArea", 1);

        const keepRAxis = new Map();
        keepRAxis.set("polarArea", 1);


        // Formatter state, updated by ApplyChanges from Precision / ValuePrefix / ValueSuffix of the config.
        // Negative precision means "up to N decimals" (ValueFormat.toString).
        let precision = -2;
        let valuePrefix = "";
        let valueSuffix = "";

        /**
         * Shared value formatter for datalabels, tick labels and tooltips (installed in Chart.defaults).
         * Objects are replaced by their `label` or `r` property; numbers become
         * valuePrefix + ValueFormat.toString(value, precision) + valueSuffix, other values
         * valuePrefix + value + valueSuffix.
         * @param {any} value - The raw value.
         * @returns {string} Formatted text.
         */
        function ValueFormatter(value) {
            if (typeof value === "object") {
                if (value.label)
                    value = value.label;
                else {
                    if (value.r)
                        value = value.r;
                }
            }
            if (typeof value === "number") {
                return valuePrefix + ValueFormat.toString(value, precision) + valueSuffix;
            }
            return valuePrefix + value + valueSuffix;
        }


        /**
         * Convert RGB to HSL.
         * @param {number} r - Red 0..255.
         * @param {number} g - Green 0..255.
         * @param {number} b - Blue 0..255.
         * @returns {number[]} [h, s, l] with all components in [0, 1].
         */
        function rgbToHsl(r, g, b) {
            r /= 255, g /= 255, b /= 255;

            var max = Math.max(r, g, b), min = Math.min(r, g, b);
            var h, s, l = (max + min) / 2;

            if (max == min) {
                h = s = 0; // achromatic
            } else {
                var d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

                switch (max) {
                    case r: h = (g - b) / d + (g < b ? 6 : 0); break;
                    case g: h = (b - r) / d + 2; break;
                    case b: h = (r - g) / d + 4; break;
                }

                h /= 6;
            }
            return [h, s, l];
        }
        /**
         * Convert HSL to RGB. The hue is wrapped (h < 0 adds 10, then h >= 1 takes h % 1).
         * @param {number} h - Hue (1 = full circle).
         * @param {number} s - Saturation 0..1.
         * @param {number} l - Lightness 0..1.
         * @returns {number[]} [r, g, b] as floats 0..255.
         */
        function hslToRgb(h, s, l) {
            var r, g, b;

            if (h < 0)
                h += 10;
            if (h >= 1)
                h = h % 1;

            if (s == 0) {
                r = g = b = l; // achromatic
            } else {
                /**
                 * Standard HSL helper computing one colour channel.
                 * @param {number} p - Lower bound (2l - q).
                 * @param {number} q - Upper bound.
                 * @param {number} t - Hue offset for the channel (wrapped into 0..1).
                 * @returns {number} Channel value 0..1.
                 */
                function hue2rgb(p, q, t) {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1 / 6) return p + (q - p) * 6 * t;
                    if (t < 1 / 2) return q;
                    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
                    return p;
                }

                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;

                r = hue2rgb(p, q, h + 1 / 3);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1 / 3);
            }

            return [r * 255, g * 255, b * 255];
        }

        /**
         * Resolve any CSS colour to RGBA bytes by filling a 4x4 rect on a cached off-screen 2D context
         * (stored in GetRgbaColorValues.Context) and reading pixel (1,1) back.
         * Note: the line `colorContext.wi` is a no-op expression statement.
         * @param {string} cssColor - Any CSS colour.
         * @returns {Uint8ClampedArray} [r, g, b, a] in 0..255.
         */
        function GetRgbaColorValues(cssColor) {
            const colorContext = GetRgbaColorValues.Context ?? document.createElement('canvas').getContext('2d');
            GetRgbaColorValues.Context = colorContext;
            colorContext.wi
            colorContext.reset();
            colorContext.fillStyle = cssColor;
            colorContext.fillRect(0, 0, 4, 4);
            return colorContext.getImageData(1, 1, 1, 1).data;
        }


        /**
         * Generate `count` colours with the saturation/lightness/alpha of `cssColor` and hues fanned from
         * -spread to +spread degrees around its hue, repeating every `repeatCount` items.
         * @param {string} cssColor - Base CSS colour.
         * @param {number} count - Number of colours to generate.
         * @param {number} [repeatCount=13] - Fan size; a negative value means ceil(count / -repeatCount)
         *   (min 5). Capped at count.
         * @param {number} [spread=120] - Half the hue fan in degrees.
         * @returns {string[]} "rgb(r,g,b)" strings, or "rgba(r,g,b,a)" when the base colour is translucent.
         */
        function GenerateColorAround(cssColor, count, repeatCount, spread) {

            if (!repeatCount)
                repeatCount = 13;
            if (repeatCount < 0) {
                repeatCount = Math.ceil(count / -repeatCount);
                if (repeatCount < 5)
                    repeatCount = 5;
            }
            if (count < repeatCount)
                repeatCount = count;
            if (!spread)
                spread = 120;
            const rgba = GetRgbaColorValues(cssColor);
            const a = rgba[3];
            const op = "," + (a / 255.0) + ")";
            const set = (a >= 255) ?
                ((r, g, b) => "rgb(" + r + "," + g + "," + b + ")")
                :
                ((r, g, b) => "rgba(" + r + "," + g + "," + b + op);

            const hsl = rgbToHsl(rgba[0], rgba[1], rgba[2]);
            const cols = [];
            spread /= 360;
            const scale = spread * 2 / (repeatCount - 1);
            const newHsl = hsl.slice();
            for (let i = 0; i < count; ++i) {
                let h = i % repeatCount;
                h *= scale;
                h -= spread;
                newHsl[0] = (hsl[0] + 4 + h) % 1;
/*                let hi = ((i / repeatCount) | 0) & 1;
                hi ^= 1;
                hi *= 0.4;
                hi += 0.6;
                newHsl[2] = hsl[2] * hi;*/
                const newRgb = hslToRgb(newHsl[0], newHsl[1], newHsl[2]);
                cols.push(set(newRgb[0] | 0, newRgb[1] | 0, newRgb[2] | 0));
            }
            return cols;
        }

        /**
         * Gradient cache used by gradientCreator: the canvas box the gradients were created for and a
         * Map from gradient string to CanvasGradient (or fallback colour). Reset when the canvas size changes.
         * @type {{x0:number, y0:number, x1:number, y1:number, gradients:?Map<string, (CanvasGradient|string)>}}
         */
        const gradCache = {
            x0: 0,
            y0: 0,
            x1: 0,
            y1: 0,
            gradients: null,
        };

        /**
         * Scriptable-colour backend for gradient colour strings.
         * Returns "#000" until chart.chartArea exists. Uses the full canvas (0,0,width,height) as gradient
         * box; clears gradCache when the canvas size changed and caches per gradData string. Parses gradData
         * after the "(" at index i as "stop;color;stop;color..."; on a real CanvasRenderingContext2D creates
         * the gradient with `cf` and adds the stops, otherwise (e.g. the canvas2svg C2S context) returns the
         * first colour.
         * @param {object} chartContext - Chart.js scriptable context (uses .chart).
         * @param {string} gradData - The full gradient string, e.g. "down(0;red;1;blue)".
         * @param {function(CanvasRenderingContext2D, number, number, number, number):CanvasGradient} cf - Factory from CanvasChart.GradientFns.
         * @param {number} i - Index of "(" in gradData.
         * @returns {CanvasGradient|string} The gradient, or a plain colour.
         */
        function gradientCreator(chartContext, gradData, cf, i) {

            const chart = chartContext.chart;
            const ctx = chart.ctx;
            const chartArea = chart.chartArea;
            if (!chartArea)
                return "#000";
            const x0 = 0;
            const y0 = 0;
            const x1 = ctx.canvas.width;
            const y1 = ctx.canvas.height;
/*            const x0 = Math.floor(chartArea.left);
            const y0 = Math.floor(chartArea.top);
            const x1 = Math.ceil(chartArea.right);
            const y1 = Math.ceil(chartArea.bottom);*/
            if ((x0 != gradCache.x0) || (x1 != gradCache.x1) || (y0 != gradCache.y0) || (y1 != gradCache.y1)) {
                gradCache.x0 = x0;
                gradCache.x1 = x1;
                gradCache.y0 = y0;
                gradCache.y1 = y1;
                gradCache.gradients = new Map();
            }
            let g = gradCache.gradients.get(gradData);
            if (g)
                return g;
            const values = gradData.substring(i + 1, gradData.length - 1).split(';');
            if (ctx instanceof CanvasRenderingContext2D) {
                //console.log("Creating gradient: " + gradData + " (" + x0 + "," + y0 + ") - (" + x1 + ", " + y1 + ") (" + chartArea.left + "," + chartArea.top + ") - (" + chartArea.right + ", " + chartArea.bottom + ") ");
                g = cf(ctx, x0, y0, x1, y1);
                const vl = values.length;
                for (let i = 0; i < vl; i += 2)
                    g.addColorStop(parseFloat(values[i].trim()), values[i + 1].trim());
                gradCache.gradients.set(gradData, g);
                return g;
            }
            g = values[1].trim();
            gradCache.gradients.set(gradData, g);
            return g;
        }


        /**
         * Convert gradient colour strings "name(stops)" whose name is in CanvasChart.GradientFns into
         * scriptable functions `ctx => gradientCreator(...)`.
         * Arrays: empty gives "red", one element gives the resolved element, otherwise entries are resolved
         * in place and, if any entry became a function, the array is wrapped into a scriptable that picks
         * entry ctx.dataIndex. Other values are returned unchanged.
         * @param {any} v - Colour value (string, array or anything else).
         * @returns {any} The resolved colour value / scriptable function.
         */
        function resolveColor(v) {
            if (Array.isArray(v)) {
                const vl = v.length;
                if (vl <= 0)
                    return "red";
                if (vl === 1)
                    return resolveColor(v[0]);
                let haveFn = false;
                for (let i = 0; i < vl; ++i) {
                    const rc = resolveColor(v[i]);
                    v[i] = rc;
                    haveFn |= typeof rc !== "string";
                }
                if (!haveFn)
                    return v;
                return ctx1 => {
                    const cd = v[ctx1.dataIndex];
                    if (typeof cd === "function")
                        return cd(ctx1);
                    return cd;
                };
            }
            if (typeof v !== "string")
                return v;
            const i = v.indexOf('(');
            if (i > 0) {
                const name = v.substring(0, i).trim();
                const cf = CanvasChart.GradientFns.get(name);
                if (cf) {

                    return ct2 => gradientCreator(ct2, v, cf, i)
                }

            }
            return v;
        }


        /**
         * Remove out-of-range x positions. When chartOptions.MinValue and/or MaxValue is set (0 counts),
         * collects every x index where any dataset value is outside the range and removes those indices
         * (in descending order) from labels, each dataset's data, backgroundColor, borderColor,
         * fill.above / fill.below and the same colour arrays of options.elements.point/line/bar/arc.
         * Arrays shorter than the longest dataset are left alone.
         * @param {object} data - Chart config (mutated).
         */
        function FilterData(data) {
            const minValue = chartOptions.MinValue;
            const maxValue = chartOptions.MaxValue;
            const hasMin = minValue || (minValue === 0.0);
            const hasMax = maxValue || (maxValue === 0.0);
            if (hasMin || hasMax) {
                const dd = data.data;
                const remove = new Map();
                const datasets = dd.datasets;
                const dsl = datasets.length;
                let valCount = 0;
                for (let dsi = 0; dsi < dsl; ++dsi) {
                    const dsv = datasets[dsi].data;
                    if (dsv) {
                        const dsvl = dsv.length;
                        if (dsvl > valCount)
                            valCount = dsvl;
                        for (let vi = 0; vi < dsvl; ++vi) {
                            const val = dsv[vi];
                            if (hasMax && (val > maxValue))
                                remove.set(vi, true);
                            if (hasMin && (val < minValue))
                                remove.set(vi, true);
                        }
                    }
                }
                const removeO = [];
                remove.forEach((_, ri) => removeO.push(ri));
                const rl = removeO.length;
                if (rl > 0) {
                    if (rl > 1)
                        removeO.sort((a, b) => b - a);

                    /**
                     * Splice the collected indices (removeO, descending) out of an array if it exists and is
                     * at least as long as the longest dataset.
                     * @param {?any[]} dsv - Array to fix (mutated).
                     */
                    function fixArray(dsv) {
                        if (!dsv)
                            return;
                        if (dsv.length < valCount)
                            return;
                        for (let rc = 0; rc < rl; ++rc)
                            dsv.splice(removeO[rc], 1);
                    }

                    /**
                     * Apply fixArray to backgroundColor, borderColor and fill.above / fill.below.
                     * @param {object} ds - A dataset or options.elements.* object (must not be undefined).
                     */
                    function fixCommon(ds) {
                        fixArray(ds.backgroundColor);
                        fixArray(ds.borderColor);
                        const f = ds.fill;
                        if (f) {
                            fixArray(f.above);
                            fixArray(f.below);
                        }
                    }

                    fixArray(dd.labels);
                    for (let dsi = 0; dsi < dsl; ++dsi) {
                        const ds = datasets[dsi];
                        fixArray(ds.data);
                        fixCommon(ds);
                    }
                    if (data.options) {
                        const els = data.options.elements;
                        if (els) {
                            fixCommon(els.point);
                            fixCommon(els.line);
                            fixCommon(els.bar);
                            fixCommon(els.arc);
                        }
                    }

                }
            }
        }

        /**
         * Truncate to chartOptions.MaxCount x positions: when set and the longest dataset exceeds it,
         * truncates labels, data and the same colour arrays as FilterData to MaxCount entries.
         * @param {object} data - Chart config (mutated).
         */
        function Limit(data) {
            const maxCount = chartOptions.MaxCount;
            if (maxCount) {
                const dd = data.data;
                const datasets = dd.datasets;
                const dsl = datasets.length;
                let valCount = 0;
                for (let dsi = 0; dsi < dsl; ++dsi) {
                    const dsv = datasets[dsi].data;
                    if (dsv) {
                        const dsvl = dsv.length;
                        if (dsvl > valCount)
                            valCount = dsvl;
                    }
                }
                if (valCount > maxCount) {

                    const del = valCount - maxCount;
                    /**
                     * Remove everything after maxCount (splice(maxCount, valCount - maxCount)) from an array
                     * that exists and is at least as long as the longest dataset.
                     * @param {?any[]} dsv - Array to fix (mutated).
                     */
                    function fixArray(dsv) {
                        if (!dsv)
                            return;
                        if (dsv.length < valCount)
                            return;
                        dsv.splice(maxCount, del);
                    }

                    /**
                     * Apply Limit's fixArray to backgroundColor, borderColor and fill.above / fill.below
                     * (duplicate of the FilterData helper).
                     * @param {object} ds - A dataset or options.elements.* object (must not be undefined).
                     */
                    function fixCommon(ds) {
                        fixArray(ds.backgroundColor);
                        fixArray(ds.borderColor);
                        const f = ds.fill;
                        if (f) {
                            fixArray(f.above);
                            fixArray(f.below);
                        }
                    }

                    fixArray(dd.labels);
                    for (let dsi = 0; dsi < dsl; ++dsi) {
                        const ds = datasets[dsi];
                        fixArray(ds.data);
                        fixCommon(ds);
                    }
                    if (data.options) {
                        const els = data.options.elements;
                        if (els) {
                            fixCommon(els.point);
                            fixCommon(els.line);
                            fixCommon(els.bar);
                            fixCommon(els.arc);
                        }
                    }

                }
            }
        }


        /**
         * Transform a (freshly parsed) server config into the render config, applying the current view state.
         * Steps: FilterData; options.responsive = true, maintainAspectRatio = false; reads Precision
         * (default -2 when not a number), ValuePrefix, ValueSuffix into the formatter state; picks the data
         * label strategy from chartOptions.ValueLabel (or data.ValueLabel): 1 addDataLabels, 2 onlyDataLabels,
         * 3 firstLineDataLabels, 4 noLabels, 5 valueLabels, 6 bareValueLabels, else formattedValueLabels.
         * For a forced "line" type defaults borderColor to backgroundColor and borderWidth <= 0 to 2.
         * Applies forceChartType / forcedIndexAxis and sets ticks.precision = |precision| on the indexAxis
         * scale (or "y"). For pie/doughnut/polarArea removes the x/y scales, puts the legend left (not
         * reversed) and, with more than one label, generates per-label colours with GenerateColorAround where
         * the colour arrays don't match the label count (dataset 0 around its single colour or a default
         * green, later datasets copy dataset 0); for other types a single dataset hides the legend. Removes
         * the "r" scale except for polarArea (which gets a default tick backdropColor from
         * --ThemeBackgroundRGB). Sets datalabels rotation -80 on vertical axis charts. Reorders labels and
         * dataset data per forcedOrder. Applies the forcedColor scheme, otherwise resolves gradient colour
         * strings. If dataset 0 has both colours: forceColorValues colours the datalabels with its border
         * colours; forceColorLabels colours legend entries and polar point labels (no-axis types) or the
         * index axis ticks. Then Limit, and installs options.onClick forwarding to chartOptions.OnClick.
         * @param {object} data - Chart config with `options` present (mutated in place).
         */
        function ApplyChanges(data) {
            FilterData(data);

            const style = getComputedStyle(document.body);

            data.options.responsive = true;
            data.options.maintainAspectRatio = false;



            precision = data.Precision;
            valuePrefix = data.ValuePrefix ?? "";
            valueSuffix = data.ValueSuffix ?? "";

            let vl = chartOptions.ValueLabel;
            if ((!vl) && (vl !== 0))
                vl = data.ValueLabel;
            switch (vl) {
                case 1:
                    CanvasChart.addDataLabels(data);
                    break;
                case 2:
                    CanvasChart.onlyDataLabels(data);
                    break;
                case 3:
                    CanvasChart.firstLineDataLabels(data);
                    break;
                case 4:
                    CanvasChart.noLabels(data);
                    break;
                case 5:
                    CanvasChart.valueLabels(data);
                    break;
                case 6:
                    CanvasChart.bareValueLabels(data);
                    break;
                default:
                    CanvasChart.formattedValueLabels(data);
                    break;
            }
            if (!precision)
                if (typeof precision !== "number")
                    precision = -2;
            if (forceChartType === "line") {
                const dd = data.data;
                const ds = dd.datasets;
                const dsl = ds.length;
                for (let i = 0; i < dsl; ++i) {
                    const dss = ds[i];
                    if (!dss.borderColor)
                        dss.borderColor = dss.backgroundColor;
                    if (dss.borderWidth <= 0)
                        dss.borderWidth = 2;
                }
            }
            data.type = forceChartType ?? data.type;
            data.options.indexAxis = forcedIndexAxis ?? data.options.indexAxis;
            const axis = data.options.indexAxis ? data.options.indexAxis : "y";
            if (!data.options.scales)
                data.options.scales = {};
            if (!data.options.scales[axis])
                data.options.scales[axis] = {};
            const mainAxisOpt = data.options.scales[axis];
            if (!mainAxisOpt.ticks)
                mainAxisOpt.ticks = {};
            mainAxisOpt.ticks.precision = precision < 0 ? (-precision) : precision;

            const isNoAxis = noAxisTypes.get(data.type) === 1;
            if (isNoAxis) {
                if (data.options.scales?.x)
                    delete data.options.scales.x;
                if (data.options.scales?.y)
                    delete data.options.scales.y;
                if (!data.options.plugins?.legend)
                    data.options.plugins.legend = {};
                data.options.plugins.legend.position = "left";
                data.options.plugins.legend.reverse = false;
            } else {
                if (data.data.datasets.length === 1) {
                    if (!data.options.plugins?.legend)
                        data.options.plugins.legend = {};
                    data.options.plugins.legend.display = false;
                }
            }

            if (keepRAxis.get(data.type) !== 1) {
                if (data.options.scales?.r)
                    delete data.options.scales.r;
            } else {
                if (!data.options.scales)
                    data.options.scales = {};
                if (!data.options.scales.r)
                    data.options.scales.r = {};
                if (!data.options.scales.r.ticks)
                    data.options.scales.r.ticks = {};
                if ((!data.options.scales.r.ticks.backdropColor) || (data.options.scales.r.ticks.backdropColor === "")) {
                    data.options.scales.r.ticks.backdropColor = "rgba(" + style.getPropertyValue('--ThemeBackgroundRGB') + ",0.65)";
                }
            }

            if (!data.options.plugins)
                data.options.plugins = {};
            if (!data.options.plugins.datalabels)
                data.options.plugins.datalabels = {};
            data.options.plugins.datalabels.rotation = (!isNoAxis) && (data.options.indexAxis !== "y") ? -80 : 0;

            if (isNoAxis) {
                const dd = data.data;
                const ds = dd.datasets;
                const sl = ds.length;
                const labelCount = dd.labels.length;
                if (labelCount > 1) {
                    for (let si = 0; si < sl; ++si) {
                        const dss = ds[si];
                        let cols = dss.backgroundColor;
                        let cl = cols?.length;
                        if ((!cl) || (cl !== labelCount)) {
                            if (si > 0)
                                dss.backgroundColor = ds[0].backgroundColor;
                            else
                                dss.backgroundColor = GenerateColorAround(cl === 1 ? cols[0] : "rgba(60,200,70,0.8)", labelCount);
                        }
                        cols = dss.borderColor;
                        cl = cols?.length;
                        if ((!cl) || (cl !== labelCount)) {
                            if (si > 0)
                                dss.borderColor = ds[0].borderColor;
                            else
                                dss.borderColor = GenerateColorAround(cl === 1 ? cols[0] : "rgb(60,200,70)", labelCount);
                        }
                    }
                }
            }
            if (forcedOrder !== 0) {
                const newOrder = [];
                const dd = data.data;
                const ds = dd.datasets;
                const labels = dd.labels;
                const labelCount = labels.length;
                for (let i = 0; i < labelCount; ++i)
                    newOrder[i] = i;
                const isDesc = forcedOrder < 0;
                const otype = isDesc ? -forcedOrder : forcedOrder;
                let orderFn;
                if (otype === 1) {
                    orderFn = (a, b) => labels[a].localeCompare(labels[b]);
                } else {
                    const sd = ds[otype - 2].data;
                    orderFn = (a, b) => sd[a] - sd[b];
                }
                newOrder.sort(orderFn);
                if (isDesc)
                    newOrder.reverse();


                /**
                 * Permute an array in place according to newOrder (sorted index order: labels by
                 * localeCompare, or values of dataset otype - 2; reversed for descending).
                 * Colour arrays are not permuted.
                 * @param {any[]} a - Labels or dataset data (mutated).
                 */
                function reOrder(a) {
                    const org = a.slice(0);
                    for (let i = 0; i < labelCount; ++i)
                        a[i] = org[newOrder[i]];
                }
                reOrder(labels);
                const dsl = ds.length;
                for (let i = 0; i < dsl; ++i)
                    reOrder(ds[i].data);
            }



            // Forced colour schemes (all produce border colour = opaque rgb, background = rgba):
            // RandomValueGradient - one hue sweep over all values of all series (seeded by forcedColorSeed);
            // RandomMagnitudeGradient - hue proportional to the value between min and max (seeded);
            // RandomSeriesGradient - one colour per series stepping the hue (seeded);
            // LabelText - per label colour from Random.Create(label), darker/less saturated per series;
            // FixedValue<i> - hues i/12..(i+2)/12 over the values, lightness per series;
            // FixedSeries<i> - hues i/12..(i+2)/12 over the series;
            // FixedMag<i> - hues i/12..(i+2)/12 by value magnitude.
            if (forcedColor) {

                /**
                 * Format a colour as "rgb(r,g,b)" with integer-truncated channels.
                 * @param {number[]} rgb - [r, g, b] 0..255.
                 * @returns {string} CSS colour.
                 */
                function MakeRgb(rgb) {
                    return "rgb(" + (rgb[0] | 0) + "," + (rgb[1] | 0) + "," + (rgb[2] | 0) + ")";
                }

                /**
                 * Format a colour as "rgba(r,g,b,o)" with integer-truncated channels.
                 * @param {number[]} rgb - [r, g, b] 0..255.
                 * @param {number} o - Opacity 0..1.
                 * @returns {string} CSS colour.
                 */
                function MakeRgba(rgb, o) {
                    return "rgba(" + (rgb[0] | 0) + "," + (rgb[1] | 0) + "," + (rgb[2] | 0) + "," + o + ")";
                }


                const dd = data.data;
                const dss = dd.datasets;
                const dsl = dd.labels;
                const seriesCount = dss.length;
                const valueCount = dsl.length;
                const totalCount = seriesCount * valueCount;
                if (forcedColor === colorRandomValueGradient) {
                    const getRandom = Random.Create(forcedColorSeed);
                    const hueRange = (getRandom() * (seriesCount - 0.3) + 0.3) * (getRandom() >= 0.5 ? 1.0 : -1.0);
                    let hue = getRandom();
                    const sat = getRandom() * 0.5 + 0.5;
                    const light = getRandom() * 0.3 + 0.5;
                    const hueStep = hueRange / totalCount;

                    const op = getRandom() * 0.45 + 0.5;
                    for (let si = 0; si < seriesCount; ++si) {
                        const bgs = [];
                        const border = [];
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const rgb = hslToRgb(hue, sat, light);
                            hue += hueStep;
                            border[vi] = MakeRgb(rgb);
                            bgs[vi] = MakeRgba(rgb, op);
                        }
                        const ds = dss[si];
                        ds.borderColor = border;
                        ds.backgroundColor = bgs;
                    }
                }
                if (forcedColor === colorRandomMagnitudeGradient) {
                    let minVal = Number.MAX_VALUE;
                    let maxVal = Number.MIN_VALUE;
                    for (let si = 0; si < seriesCount; ++si) {
                        const sv = dss[si].data;
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const v = sv[vi];
                            if (v < minVal)
                                minVal = v;
                            if (v > maxVal)
                                maxVal = v;
                        }
                    }
                    const getRandom = Random.Create(forcedColorSeed);
                    const hueRange = (getRandom() * (seriesCount - 0.3) + 0.3) * (getRandom() >= 0.5 ? 1.0 : -1.0);
                    const hue = getRandom();
                    const sat = getRandom() * 0.5 + 0.5;
                    const light = getRandom() * 0.3 + 0.5;
                    const op = getRandom() * 0.45 + 0.5;
                    maxVal -= minVal;
                    const scaleVal = maxVal > 0 ? (hueRange / maxVal) : 0;
                    for (let si = 0; si < seriesCount; ++si) {
                        const sv = dss[si].data;
                        const bgs = [];
                        const border = [];
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const rgb = hslToRgb(hue + (sv[vi] - minVal) * scaleVal, sat, light);
                            border[vi] = MakeRgb(rgb);
                            bgs[vi] = MakeRgba(rgb, op);
                        }
                        const ds = dss[si];
                        ds.borderColor = border;
                        ds.backgroundColor = bgs;
                    }
                }
                if (forcedColor === colorRandomSeriesGradient) {
                    const getRandom = Random.Create(forcedColorSeed);
                    const hueRange = getRandom() * 0.7 + 0.3;
                    let hue = getRandom();
                    const sat = getRandom() * 0.5 + 0.5;
                    const light = getRandom() * 0.3 + 0.5;
                    let hueStep = hueRange / seriesCount;
                    if (getRandom() >= 0.5)
                        hueStep = -hueStep;
                    const op = getRandom() * 0.45 + 0.5;
                    for (let si = 0; si < seriesCount; ++si) {
                        const rgb = hslToRgb(hue, sat, light);
                        const ds = dss[si];
                        hue += hueStep;
                        ds.borderColor = MakeRgb(rgb);
                        ds.backgroundColor = MakeRgba(rgb, op);
                    }
                }

                if (forcedColor === colorLabelText) {
                    for (let si = 0; si < seriesCount; ++si) {
                        const satScale = 1.0 - (si * 0.4) / seriesCount;
                        const lumScale = 1.0 - (si * 0.4) / seriesCount;
                        const bgs = [];
                        const border = [];
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const getRandom = Random.Create(dsl[vi]);
                            const hue = getRandom();
                            const sat = (getRandom() * 0.7 + 0.3) * satScale;
                            const light = (getRandom() * 0.5 + 0.5) * lumScale;
                            const rgb = hslToRgb(hue, sat, light);
                            border[vi] = MakeRgb(rgb);
                            bgs[vi] = MakeRgba(rgb, 0.7);
                        }
                        const ds = dss[si];
                        ds.borderColor = border;
                        ds.backgroundColor = bgs;
                    }
                }

                if (forcedColor.startsWith("FixedValue")) {
                    const i = parseInt(forcedColor.substring(10));
                    const hueStart = i / 12.0;
                    const hueEnd = (i + 2) / 12.0;
                    const ss = 0.3 / (seriesCount > 1 ? (seriesCount - 1) : 1);
                    const vs = (hueEnd - hueStart) / (valueCount > 1 ? (valueCount - 1) : 1);
                    for (let si = 0; si < seriesCount; ++si) {
                        const light = 0.7 - si * ss;
                        const bgs = [];
                        const border = [];
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const rgb = hslToRgb(hueStart + vi * vs, 0.8, light);
                            border[vi] = MakeRgb(rgb);
                            bgs[vi] = MakeRgba(rgb, 0.6);
                        }
                        const ds = dss[si];
                        ds.borderColor = border;
                        ds.backgroundColor = bgs;
                    }
                }

                if (forcedColor.startsWith("FixedSeries")) {
                    const i = parseInt(forcedColor.substring(11));
                    const hueStart = i / 12.0;
                    const hueEnd = (i + 2) / 12.0;
                    const ss = (hueEnd - hueStart) / (seriesCount > 1 ? (seriesCount - 1) : 1);
                    for (let si = 0; si < seriesCount; ++si) {
                        const rgb = hslToRgb(hueStart + si * ss, 0.8, 0.7);
                        const ds = dss[si];
                        ds.borderColor = MakeRgb(rgb);
                        ds.backgroundColor = MakeRgba(rgb, 0.6);
                    }
                }

                if (forcedColor.startsWith("FixedMag")) {
                    const i = parseInt(forcedColor.substring(8));
                    const hueStart = i / 12.0;
                    const hueEnd = (i + 2) / 12.0;
                    let minVal = Number.MAX_VALUE;
                    let maxVal = Number.MIN_VALUE;
                    for (let si = 0; si < seriesCount; ++si) {
                        const sv = dss[si].data;
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const v = sv[vi];
                            if (v < minVal)
                                minVal = v;
                            if (v > maxVal)
                                maxVal = v;
                        }
                    }
                    maxVal -= minVal;
                    const scaleVal = maxVal > 0 ? ((hueEnd - hueStart) / maxVal) : 0;
                    for (let si = 0; si < seriesCount; ++si) {
                        const sv = dss[si].data;
                        const bgs = [];
                        const border = [];
                        for (let vi = 0; vi < valueCount; ++vi) {
                            const rgb = hslToRgb(hueStart + (sv[vi] - minVal) * scaleVal, 0.8, 0.7);
                            border[vi] = MakeRgb(rgb);
                            bgs[vi] = MakeRgba(rgb, 0.6);
                        }
                        const ds = dss[si];
                        ds.borderColor = border;
                        ds.backgroundColor = bgs;
                    }


                }


            } else {
                /**
                 * Run resolveColor on borderColor, backgroundColor and fill.above / fill.below
                 * (gradient strings become scriptable functions).
                 * @param {?object} ds - A dataset or options.elements.* object (ignored when missing).
                 */
                function resolve(ds) {
                    if (!ds)
                        return;
                    ds.borderColor = resolveColor(ds.borderColor);
                    ds.backgroundColor = resolveColor(ds.backgroundColor);
                    const f = ds.fill;
                    if (f) {
                        f.above = resolveColor(f.above);
                        f.below = resolveColor(f.below);
                    }
                }
                const dd = data.data;
                const dss = dd.datasets;
                const seriesCount = dss.length;
                for (let si = 0; si < seriesCount; ++si)
                    resolve(dss[si]);
                const els = data.options.elements;
                if (els) {
                    resolve(els.point);
                    resolve(els.line);
                    resolve(els.bar);
                    resolve(els.arc);
                }
            }


            const fc = data.data.datasets[0].backgroundColor;
            const bc = data.data.datasets[0].borderColor;
            if (bc && fc) {
                if (forceColorValues)
                    data.options.plugins.datalabels.color = bc;

                if (forceColorLabels)
                {
                    if (isNoAxis) {
                        if (!data.options)
                            data.options = {};
                        if (!data.options.plugins)
                            data.options.plugins = {};
                        if (!data.options.plugins.legend)
                            data.options.plugins.legend = {};
                        if (!data.options.plugins.legend.labels)
                            data.options.plugins.legend.labels = {};
                        /**
                         * Coloured-labels legend generator: one legend item per label with fontColor and
                         * strokeStyle = border colour i and fillStyle = background colour i of dataset 0.
                         * @param {Chart} chart - The Chart.js instance.
                         * @returns {object[]|undefined} Legend items (undefined when there are no labels/datasets).
                         */
                        data.options.plugins.legend.labels.generateLabels = chart => {
                            if (chart.data.labels.length && chart.data.datasets.length) {
                                return chart.data.labels.map((label, i) => ({
                                    text: label,
                                    fontColor: bc[i],
                                    fillStyle: fc[i],
                                    strokeStyle: bc[i],
                                    lineWidth: 1,

                                }));
                            }
                        };
                        if (!data.options)
                            data.options = {};
                        if (!data.options.scales)
                            data.options.scales = {};
                        if (data.options.scales.r) {

                            const ax = data.options.scales.r;
                            if (!ax.pointLabels)
                                ax.pointLabels = {};
                            /**
                             * Polar point label colour: border colour p.index of dataset 0.
                             * @param {object} p - Chart.js scriptable context.
                             * @returns {string} Colour.
                             */
                            ax.pointLabels.color = p => bc[p.index];
                        }
                    } else {

                        if (!data.options)
                            data.options = {};
                        if (!data.options.scales)
                            data.options.scales = {};
                        const axis = data.options.indexAxis !== "y" ? "x" : "y";
                        if (!data.options.scales[axis])
                            data.options.scales[axis] = {};
                        const ax = data.options.scales[axis];
                        if (!ax.ticks)
                            ax.ticks = {};
                        ax.ticks.color = bc;
                    }
                }
            }

            Limit(data);


            /**
             * Chart.js click handler: forwards the first clicked element as
             * (label, value, datasetIndex, index) to chartOptions.OnClick (if set).
             * @param {Event} e - Chart.js event.
             * @param {object[]} els - Active elements under the pointer.
             */
            data.options.onClick = (e, els) => {
                const dl = els.length;
                if (dl > 0) {
                    const el = els[0];
                    const dsi = el.datasetIndex;
                    const vi = el.index;
                    const ds = data.data.datasets[dsi];
                    const labels = data.data.labels;
                    const values = ds.data;
                    const label = vi < labels.length ? labels[vi] : null;
                    const value = vi < values.length ? values[vi] : null;

                    const onClick = chartOptions.OnClick;
                    if (onClick)
                        onClick(label, value, dsi, vi);

                }
            };
        }

        /**
         * Rebuild the live chart from the original server config with the current view state.
         * No-op until `main` exists. Re-parses orgDataStr, runs ApplyChanges; unless allowAnimation, forces
         * options.animation.duration = 0 (creating options / animation as needed). Destroys `main`, replaces
         * the <canvas> element with a new one, creates a new Chart (expandos Transparent = true, Data = data),
         * calls update() and, when animation was suppressed, runs `restore` after 10 ms.
         * restore re-assigns the old duration when it was defined, else deletes `animation.duration`.
         * @param {boolean} [allowAnimation] - Keep the configured animation.
         */
        function RebuildChart(allowAnimation) {
            if (!main)
                return;
            const data = JSON.parse(orgDataStr);
            ApplyChanges(data);
            /**
             * Undo function that puts back the original animation duration / removes the objects added
             * to disable animation.
             */
            let restore = () => { };
            if (!allowAnimation) {
                const options = data.options;
                if (options) {
                    const animation = options.animation;
                    if (animation) {
                        const duration = animation.duration;
                        if (typeof duration !== "undefined")
                            restore = () => animation.duration = duration;
                        else
                            restore = () => delete animation.duration;
                        animation.duration = 0;
                    } else {
                        restore = () => delete options.animation;
                        options.animation = {
                            duration: 0
                        };
                    }
                } else {
                    restore = () => delete data.options;
                    data.options = {
                        animation:
                        {
                            duration: 0
                        }
                    };
                }
            }
            //const ow = canvas.width;
            //const oh = canvas.height;
            main.destroy();
            const nc = document.createElement("canvas");
            canvas.parentElement.replaceChild(nc, canvas);
            canvas = nc;
            //canvas.width = ow;
            //canvas.height = oh;
            main = new Chart(canvas, data);
            main.Transparent = true;
            main.Data = data;
            main.update();
            if (!allowAnimation)
                setTimeout(restore, 10);
        }

        /**
         * Get the chart title.
         * @param {object} data - Chart config.
         * @returns {string} options.plugins.title.text[0], or "Chart".
         */
        function GetTitle(data) {
            let title = "Chart";
            const tt = data.options?.plugins?.title?.text;
            if (tt && (tt.length > 0)) {
                const t0 = tt[0];
                if (t0)
                    title = t0;
            }
            return title;
        }

        /**
         * Render a chart config off-screen and return it as a data URI.
         * "Png": a hidden <canvas> of the given size is appended to document.body with the expando
         * Scale = chartScale (font scaling via the background plugin) and removed in `finally`; the result is
         * ch.toBase64Image(). "Svg": a C2S (canvas2svg) mock context; the serialized SVG root element gets its
         * width/height replaced by viewBox="0 0 w h" (via CanvasChart.tweakXmlText) and the SVG is
         * base64-encoded (Base64EncodeString) into a data:image/svg+xml URI.
         * Sets responsive = false, maintainAspectRatio = false and options.animation.duration = 0 (throws if
         * options.animation is missing), sets the chart's Transparent expando and waits for
         * options.animation.onComplete (an assigned callback that captures the image and clears itself).
         * @param {object} data - Render config (output of ApplyChanges; mutated).
         * @param {number} width - Image width in pixels.
         * @param {number} height - Image height in pixels.
         * @param {boolean} transparent - When true the background plugin does not fill the background.
         * @param {string} format - "Png" or "Svg"; anything else throws Error("Invalid format ...").
         * @returns {Promise<string>} Data URI of the image.
         */
        async function GetChartAsImage(data, width, height, transparent, format) {
            let canvas = null;
            let ctx = null;
            switch (format) {
                case "Png":
                    canvas = document.createElement("canvas");
                    canvas.setAttribute("width", "" + width);
                    canvas.setAttribute("height", "" + height);
                    canvas.width = width;
                    canvas.height = height;
                    canvas.style.display = "none";
                    canvas.Scale = chartScale;
                    document.body.appendChild(canvas);
                    ctx = canvas.getContext("2d");
                    break;
                case "Svg":
                    ctx = new C2S({
                        width: width,
                        height: height,
                    });
                    break;
                default:
                    throw new Error("Invalid format " + format);
            }
            let img;
            try {
                let o = data.options;
                if (!o) {
                    o = {};
                    data.options = o;
                }
                o.responsive = false;
                o.maintainAspectRatio = false;
                o.animation.duration = 0;
                const ch = new Chart(ctx, data);
                ch.Transparent = transparent;
                await new Promise(resolve => {
                    /**
                     * One-shot animation completion callback: clears itself, captures the rendered image into
                     * `img` (serialized and patched SVG, or PNG data URI) and resolves the wait promise.
                     */
                    ch.options.animation.onComplete = () => {

                        ch.options.animation.onComplete = null;
                        switch (format) {
                            case "Svg":
                                let svg = ctx.getSerializedSvg(true);
                                const start = svg.indexOf('>');
                                const header = CanvasChart.tweakXmlText(svg.substring(0, start) + "></svg>", xmlDoc => {
                                    const xs = xmlDoc.documentElement;
                                    if (xs.hasAttribute("width"))
                                        xs.removeAttribute("width");
                                    if (xs.hasAttribute("height"))
                                        xs.removeAttribute("height");
                                    xs.setAttribute("viewBox", "0 0 " + width + " " + height);
                                });
                                svg = header.substring(0, header.length - 2) + svg.substring(start);
                                console.log("Len: " + svg.length);
                                img = "data:image/svg+xml; charset=UTF-8;base64," + Base64EncodeString(svg);
                                break;
                            case "Png":
                                img = ch.toBase64Image();
                                break;
                        }
                        resolve();
                    };
                    if (format === "Png")
                        ch.resize(width, height);
                    ch.update();
                });
            }
            finally {
                if (canvas)
                    canvas.remove();
            }
            return img;

        }

        /**
         * Menu action for a client-side image export (download).
         * Calls closeFn, builds a fresh config from orgDataStr + ApplyChanges, shows
         * PopUpWorking("Exporting", "WxH", "IconFile" + format), renders with GetChartAsImage and downloads the
         * result through a temporary hidden <a download="<chart title>">; errors are reported with Fail.
         * @param {function():void} closeFn - Closes the popup menu.
         * @param {number} width - Image width in pixels.
         * @param {number} height - Image height in pixels.
         * @param {boolean} transparent - Render without background.
         * @param {string|boolean} [format="Png"] - "Png" or "Svg"; `true` means "Svg".
         * @returns {Promise<void>}
         */
        async function SaveImage(closeFn, width, height, transparent, format) {

            if (!format)
                format = "Png";
            if (format === true)
                format = "Svg";

            console.log("Saving " + width + "x" + height);
            closeFn();
            const data = JSON.parse(orgDataStr);
            ApplyChanges(data);
            const close = await PopUpWorking("Exporting", width + "x" + height, "IconFile" + format);
            try {
                const img = await GetChartAsImage(data, width, height, transparent, format);
                const del = document.createElement('a');
                del.setAttribute("href", img);
                del.setAttribute('download', GetTitle(data));
                del.style.display = 'none';
                document.body.appendChild(del);
                del.click();
                document.body.removeChild(del);
                await delay(250);
            }
            catch (e) {
                Fail("Failed to export: " + e);
            }
            close();
        }

        /**
         * Menu action for a server-side exporter.
         * Calls closeFn, rebuilds the config (orgDataStr + ApplyChanges), shows PopUpWorking("Exporting", desc, icon).
         * For parseInt(format) 1 (Png) or 2 (Svg) (matching C# ChartExportInputTypes) renders an A4 300 DPI
         * image with GetChartAsImage and sends it as DataStr with Data null; otherwise sends the config as Data.
         * POSTs {Data, DataStr, ExportAs: name, Options: {Filename: title}} to ../Api/ExportChart (sendRequest).
         * A MemoryFile reply with Mime and Data is downloaded via a temporary <a download=Name>; a reply with
         * only Name becomes GetAbsolutePath("../" + Name), is copied to the clipboard
         * (ValueFormat.copyToClipboardInfo) and opened with Open. Empty replies or exceptions call Fail.
         * @param {function():void} closeFn - Closes the popup menu.
         * @param {string} name - Exporter id (WebMenuItem.Name).
         * @param {string} desc - Exporter description (WebMenuItem.Title), shown while working.
         * @param {string|number} format - Exporter input type ("0" Data, "1" Png, "2" Svg).
         * @param {string} icon - Icon class shown while working.
         * @returns {Promise<void>}
         */
        async function ExportChart(closeFn, name, desc, format, icon) {
            closeFn();
            let data = JSON.parse(orgDataStr);
            ApplyChanges(data);
            const close = await PopUpWorking("Exporting", desc, icon);
            try {
                format = parseInt(format);
                const title = GetTitle(data);
                let dataStr = null;
                switch (format) {
                    case 1:
                        dataStr = await GetChartAsImage(data, A4_wh, A4_hh, false, "Png");
                        data = null;
                        break;
                    case 2:
                        dataStr = await GetChartAsImage(data, A4_wh, A4_hh, false, "Svg");
                        data = null;
                        break;
                }
                const dataFile = await sendRequest("../Api/ExportChart", {
                    Data: data,
                    DataStr: dataStr,
                    ExportAs: name,
                    Options: {
                        Filename: title,
                    },
                });
                if (dataFile) {
                    if (dataFile.Mime && dataFile.Data) {
                        const del = document.createElement('a');
                        del.setAttribute("href", "data:" + dataFile.Mime + ";base64," + dataFile.Data);
                        del.setAttribute('download', dataFile.Name);
                        del.style.display = 'none';
                        document.body.appendChild(del);
                        del.click();
                        document.body.removeChild(del);
                        await delay(250);
                    } else {
                        if (dataFile.Name) {
                            const str = GetAbsolutePath("../" + dataFile.Name);
                            await ValueFormat.copyToClipboardInfo(str);
                            Open(str);
                        } else {
                            Fail("Empty data!");
                        }
                    }
                } else {
                    Fail("No data!");
                }
            }
            catch (e) {
                Fail("Failed to export: " + e);
            }
            close();
        }

        /**
         * Force a chart type: sets forceChartType and forcedIndexAxis ("y" when horizontal, else "x"),
         * closes the menu and rebuilds the chart without animation.
         * @param {function():void} closeFn - Closes the popup menu (required).
         * @param {string} newType - Chart.js type (bar, line, pie, doughnut, polarArea).
         * @param {boolean} [isHorizontal] - Use the y axis as index axis.
         */
        function SetChartType(closeFn, newType, isHorizontal) {
            console.log("Setting chart type to " + newType);
            closeFn();
            const axis = isHorizontal ? "y" : "x";
            forceChartType = newType;
            forcedIndexAxis = axis;
            RebuildChart();
        }

        /**
         * Select a colour mode: picks a new random forcedColorSeed, sets forcedColor, closes the menu (if
         * given) and rebuilds the chart.
         * @param {?function():void} closeFn - Closes the popup menu (null from the keyboard handler).
         * @param {?string} newColor - Colour mode name, or null for the original colours.
         */
        function SetChartColor(closeFn, newColor) {
            forcedColorSeed = Random.GetRandomSeed();
            if (newColor)
                console.log("Setting chart color to " + newColor + ", using seed: " + forcedColorSeed);
            else
                console.log("Restoring chart color to original");
            if (closeFn)
                closeFn();
            forcedColor = newColor;
            RebuildChart();
        }

        /**
         * Set the export font scale (chartScale, applied only to PNG exports via canvas.Scale); no rebuild.
         * @param {?function():void} closeFn - Closes the popup menu.
         * @param {number} newScale - Scale factor (1 - 2.5).
         */
        function SetChartScale(closeFn, newScale) {
            console.log("Setting chart scale to " + newScale);
            if (closeFn)
                closeFn();
            chartScale = newScale;
            //RebuildChart();
        }

        /**
         * Set the sort order (forcedOrder) and rebuild the chart with animation.
         * @param {?function():void} closeFn - Closes the popup menu.
         * @param {number} newOrder - 0 original, +-1 by label, +-(n+2) by dataset n; negative = descending.
         */
        function SetOrder(closeFn, newOrder) {
            console.log("Setting new order to " + newOrder);
            if (closeFn)
                closeFn();
            forcedOrder = newOrder;
            RebuildChart(true);
        }


        // Menu icon (unless DisableMenu): a 32x32 ColorIcon in the bottom-right corner of <SysWeaver-Chart>.
        // The async click handler fails with "No data!" before the first fetch, otherwise awaits
        // getChartExportMenu (lazy loads application.js etc.), rebuilds the config and opens PopUpMenu with a
        // builder that adds class "Chart" to the popup element and returns a WebMenu "Chart" with the
        // sub-menus Type, Order, Color and Export (WebMenuItem flags: checked = 2, readonly = 1).
        if (!chartOptions.DisableMenu) {

            const menuIcon = new ColorIcon("IconChartMenu", "IconColorThemeAcc2", 32, 32, "Click to show options", async ev => {
                if (!orgDataStr) {
                    Fail("No data!");
                    return;
                }
                const menuItems = await CanvasChart.getChartExportMenu();
                const data = JSON.parse(orgDataStr);
                ApplyChanges(data);
                PopUpMenu(menuIcon.Element, (close, backEl) => {

                    const pp = backEl.parentElement;
                    pp.classList.add("Chart");
                    const menu = new WebMenu();
                    menu.Name = "Chart";
                    let menuDest;
                    //  Modify type menu
                    const currentType = data.type;
                    const currentIsHorizontal = data.options.indexAxis === "y";
                    // Type menu filter: data.ValidTypes (lower-cased) plus the original type; shown only when more
                    // than one type is valid. An empty ValidTypes array hides the type menu.
                    const validTypes = data.ValidTypes;
                    let haveTypes = true;
                    let validMap = null;
                    if (validTypes) {
                        const l = validTypes.length;
                        if (l <= 0) {
                            haveTypes = false;
                        } else {
                            validMap = new Map();
                            for (let i = 0; i < l; ++i)
                                validMap.set(validTypes[i].toLowerCase(), 1);
                            validMap.set(orgDataType.toLowerCase(), 1);
                            haveTypes = validMap.size > 1;
                        }
                    }
                    if (haveTypes) {

                        const typeMenu = WebMenuItem.From({
                            Name: "Type",
                            IconClass: "IconChartType",
                            Title: "Options for modifying the chart type",
                            Children: [],
                        });
                        menu.Items.push(typeMenu);

                        menuDest = typeMenu.Children;
                        if ((!validMap) || validMap.get("bar") || validMap.get("barv")) {
                            menuDest.push(WebMenuItem.From({
                                Name: "Bar (vertical)",
                                Title: "Display the chart as a vertical bar chart",
                                IconClass: "IconChartTypeBar",
                                Flags: ((currentType === "bar") && (!currentIsHorizontal)) ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "bar"),
                            }));
                        }
                        if ((!validMap) || validMap.get("bar") || validMap.get("barh")) {
                            menuDest.push(WebMenuItem.From({
                                Name: "Bar (horizontal)",
                                Title: "Display the chart as a horizontal bar chart",
                                IconClass: "IconChartTypeBarHorizontal",
                                Flags: ((currentType === "bar") && (currentIsHorizontal)) ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "bar", true),
                            }));
                        }
                        if ((!validMap) || validMap.get("line") || validMap.get("linev")) {
                            menuDest.push(WebMenuItem.From({
                                Name: "Line (vertical)",
                                Title: "Display the chart as a vertical line chart",
                                IconClass: "IconChartTypeLine",
                                Flags: ((currentType === "line") && (!currentIsHorizontal)) ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "line"),
                            }));
                        }
                        if ((!validMap) || validMap.get("line") || validMap.get("lineh")) {
                            menuDest.push(WebMenuItem.From({
                                Name: "Line (horizontal)",
                                Title: "Display the chart as a horizontal line chart",
                                IconClass: "IconChartTypeLineHorizontal",
                                Flags: ((currentType === "line") && (currentIsHorizontal)) ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "line", true),
                            }));
                        }
                        if ((!validMap) || validMap.get("pie")) {
                            menuDest.push(WebMenuItem.From({
                                Name: "Pie",
                                Title: "Display the chart as a pie chart",
                                IconClass: "IconChartTypePie",
                                Flags: currentType === "pie" ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "pie"),
                            }));
                        }
                        if ((!validMap) || validMap.get("doughnut")) {
                            menuDest.push(WebMenuItem.From({
                                Name: "Doughnut",
                                Title: "Display the chart as a doughnut chart",
                                IconClass: "IconChartTypeDoughnut",
                                Flags: currentType === "doughnut" ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "doughnut"),
                            }));
                        }
                        if ((!validMap) || validMap.get("polararea")) {

                            menuDest.push(WebMenuItem.From({
                                Name: "Polar area",
                                Title: "Display the chart as a polar area chart",
                                IconClass: "IconChartTypePolarArea",
                                Flags: currentType === "polarArea" ? checkedReadonly : 0,
                                Data: () => SetChartType(close, "polarArea"),
                            }));
                        }
                    }
                    //  Sort menu
                    if (!(chartOptions.DisableOrderMenu || data.DisableOrderMenu)) {
                        const orderMenu = WebMenuItem.From({
                            Name: "Order",
                            IconClass: "IconOrder",
                            Title: "Options for modifying the order of the data",
                            Children: [],
                        });
                        menu.Items.push(orderMenu);
                        menuDest = orderMenu.Children;
                        {
                            const isSelected = (forcedOrder === 0)
                            menuDest.push(WebMenuItem.From({
                                Name: "Original",
                                Title: isSelected ? "Ordered in the original order" : "Use the original order",
                                IconClass: "IconOrderOriginal" + (isSelected ? "Sel" : ""),
                                Flags: isSelected ? checkedReadonly : 0,
                                Data: () => SetOrder(close, 0),
                            }));
                        }
                        const desc = forcedOrder < 0;
                        const oindex = desc ? -forcedOrder : forcedOrder;
                        {
                            const number = 1;
                            const isSelected = (oindex === number)
                            const isDesc = desc && isSelected;
                            const descText = isDesc ? "Descending" : "Ascending";
                            const idescText = isDesc ? "ascending" : "descending";
                            menuDest.push(WebMenuItem.From({
                                Name: "Labels",
                                Title: isSelected
                                    ?
                                    ("Ordered by labels in " + descText.toLowerCase() + " order.\nSelect to order by labels in " + idescText + " order")
                                    :
                                    ("Order by labels in " + descText.toLowerCase() + " order"),
                                IconClass: "IconOrder" + descText + (isSelected ? "Sel" : ""),
                                Flags: isSelected ? checked : 0,
                                Data: () => SetOrder(close, isSelected ? (isDesc ? number : -number) : number),
                            }));
                        }
                        const dss = data.data.datasets;
                        const dssl = dss.length;
                        for (let i = 0; i < dssl; ++i) {
                            const ds = dss[i];
                            const number = i + 2;
                            const isSelected = (oindex === number)
                            const isDesc = desc && isSelected;
                            const descText = isDesc ? "Descending" : "Ascending";
                            const idescText = isDesc ? "ascending" : "descending";
                            const name = ds.label ?? ("Series " + (i + 1));
                            menuDest.push(WebMenuItem.From({
                                Name: name,
                                Title: isSelected
                                    ?
                                    ("Ordered by values from \"" + name + "\" in " + descText.toLowerCase() + " order.\nSelect to order by values from \"" + name + "\" in " + idescText + " order")
                                    :
                                    ("Order by values from \"" + name + "\" in " + descText.toLowerCase() + " order"),
                                IconClass: "IconOrder" + descText + (isSelected ? "Sel" : ""),
                                Flags: isSelected ? checked : 0,
                                Data: () => SetOrder(close, isSelected ? (isDesc ? number : -number) : number),
                            }));
                        }
                    }
                    //  Color menu
                    const colorMenu = WebMenuItem.From({
                        Name: "Color",
                        IconClass: "IconColors",
                        Title: "Options for modifying the chart colors",
                        Children: [],
                    });
                    menu.Items.push(colorMenu);
                    menuDest = colorMenu.Children;

                    menuDest.push(WebMenuItem.From({
                        Name: "Colored labels",
                        Title: "Colorize the labels",
                        IconClass: "IconColoredLabels",
                        Flags: forceColorLabels ? checked : 0,
                        Data: () => {
                            forceColorLabels ^= true;
                            close();
                            RebuildChart();
                        },
                    }));


                    menuDest.push(WebMenuItem.From({
                        Name: "Colored values",
                        Title: "Colorize the data point values",
                        IconClass: "IconColoredValues",
                        Flags: forceColorValues ? checked : 0,
                        Data: () => {
                            forceColorValues ^= true;
                            close();
                            RebuildChart();
                        },
                    }));

                    if (!(chartOptions.DisableColorMenu || data.DisableColorMenu)) {

                        menuDest.push(WebMenuItem.From(
                            {
                                Id: "-",
                                Name: "Data",
                                Title: "Coloring of the data",
                            }));


                        menuDest.push(WebMenuItem.From({
                            Name: "Original",
                            Title: "Use the original colors  (key 'o')",
                            IconClass: "IconColorOriginal",
                            Flags: forcedColor === null ? checkedReadonly : 0,
                            Data: () => SetChartColor(close, null),
                        }));


                        menuDest.push(WebMenuItem.From({
                            Name: "Label hash",
                            Title: "Assign a color based on the text in the label, same text will always have the same color (key 'l')",
                            IconClass: "IconColor" + colorLabelText,
                            Flags: forcedColor === colorLabelText ? checkedReadonly : 0,
                            Data: () => SetChartColor(close, colorLabelText),
                        }));


                        menuDest.push(WebMenuItem.From({
                            Name: "Random value gradient",
                            Title: "The values will be colored according to a random hue gradient (key 'g')",
                            IconClass: "IconColor" + colorRandomValueGradient,
                            Flags: forcedColor === colorRandomValueGradient ? checked : 0,
                            Data: () => SetChartColor(close, colorRandomValueGradient),
                        }));
                        menuDest.push(WebMenuItem.From({
                            Name: "Random series gradient",
                            Title: "The series will be colored according to a random hue gradient (key 's')",
                            IconClass: "IconColor" + colorRandomSeriesGradient,
                            Flags: forcedColor === colorRandomSeriesGradient ? checked : 0,
                            Data: () => SetChartColor(close, colorRandomSeriesGradient),
                        }));

                        menuDest.push(WebMenuItem.From({
                            Name: "Random magnitude gradient",
                            Title: "The values will be colored according to the magnitude of the value using a random hue gradient (key 'm')",
                            IconClass: "IconColor" + colorRandomMagnitudeGradient,
                            Flags: forcedColor === colorRandomMagnitudeGradient ? checked : 0,
                            Data: () => SetChartColor(close, colorRandomMagnitudeGradient),
                        }));

                        const valueMenu = WebMenuItem.From({
                            Name: "Value gradients",
                            IconClass: "IconColors",
                            Title: "Fixed gradients for labels (each value get it's own color)",
                            Children: [],
                        });

                        menuDest.push(valueMenu);
                        const valueDest = valueMenu.Children;

                        const seriesMenu = WebMenuItem.From({
                            Name: "Series gradients",
                            IconClass: "IconColors",
                            Title: "Fixed gradients for series (each serie get it's own color)",
                            Children: [],
                        });
                        menuDest.push(seriesMenu);
                        const seriesDest = seriesMenu.Children;


                        const magnitudeMenu = WebMenuItem.From({
                            Name: "Magnitude gradients",
                            IconClass: "IconColors",
                            Title: "Fixed gradients for value magntidues (a gradient based on the value magnitude)",
                            Children: [],
                        });
                        menuDest.push(magnitudeMenu);
                        const magnitudeDest = magnitudeMenu.Children;
                        const colorNames = CanvasChart.FixedColors;
                        for (let i = 0; i < 12; ++i) {
                            const cname1 = colorNames[i];
                            const cname2 = colorNames[(i + 2) % 12];
                            const cname = cname1 + " - " + cname2;
                            const n1 = "FixedValue" + i;
                            const n2 = "FixedSeries" + i;
                            const n3 = "FixedMag" + i;
                            const ic = "IconColorGradient" + i;
                            valueDest.push(WebMenuItem.From({
                                Name: cname,
                                Title: "The values will be colored using a " + cname1 + " to " + cname2 + " gradient",
                                IconClass: ic,
                                Flags: forcedColor === n1 ? checkedReadonly : 0,
                                Data: () => SetChartColor(close, n1),
                            }));
                            seriesDest.push(WebMenuItem.From({
                                Name: cname,
                                Title: "The series will be colored using a " + cname1 + " to " + cname2 + " gradient",
                                IconClass: ic,
                                Flags: forcedColor === n2 ? checkedReadonly : 0,
                                Data: () => SetChartColor(close, n2),
                            }));
                            magnitudeDest.push(WebMenuItem.From({
                                Name: cname,
                                Title: "The values will be colored according to the magnitude of the value using a " + cname1 + " to " + cname2 + " gradient",
                                IconClass: ic,
                                Flags: forcedColor === n3 ? checkedReadonly : 0,
                                Data: () => SetChartColor(close, n3),
                            }));
                        }
                    }
                    //  Export menu
                    //  Server exporters (from getChartExportMenu), the "Export font size" sub-menu, then the
                    //  client-side SVG 16:9 / A4 and PNG HD / 1440p / 4K / A4 low / A4 high DPI downloads.
                    const exportMenu = WebMenuItem.From({
                        Name: "Export",
                        IconClass: "IconChartExport",
                        Title: "Options for exporting the chart",
                        Children: [],
                    });
                    menu.Items.push(exportMenu);
                    menuDest = exportMenu.Children;




                    menuItems.forEach(menuItem => {
                        const sm = WebMenuItem.From(menuItem);
                        const name = menuItem.Name;
                        const desc = menuItem.Title;
                        const icon = menuItem.IconClass;
                        const format = menuItem.Data;
                        // Replace the exporter's Data (ChartExportInputTypes code) with the click action.
                        sm.Data = () => ExportChart(close, name, desc, format, icon);
                        menuDest.push(sm);
                    });

                    const sizeMenu = WebMenuItem.From({
                        Name: "Export font size",
                        IconClass: "IconChartSize",
                        Title: "Options for modifying the font size of image exports",
                        Children: [],
                    });
                    menuDest.push(sizeMenu);

                    const transparent = false;

                    const fmtSvg = "Svg";
                    const fmtPng = "Png";

                    menuDest.push(WebMenuItem.From({
                        Name: "Vector 16:9 (svg)",
                        Title: "Save the chart as a Scalable Vector Graphics file using the 16:9 aspect ratio",
                        IconClass: "IconFileSvg",
                        Data: async () => await SaveImage(close, 1920, 1080, transparent, fmtSvg),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "Vector A4 (svg)",
                        Title: "Save the chart as a Scalable Vector Graphics file suitable for printing on an A4 with 10 mm margins",
                        IconClass: "IconFileSvg",
                        Data: async () => await SaveImage(close, A4_wl, A4_hl, transparent, fmtSvg),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "Image HD (png)",
                        Title: "Save the chart as a 1920x1080 PNG image",
                        IconClass: "IconFilePng",
                        Data: async () => await SaveImage(close, 1920, 1080, transparent, fmtPng),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "Image 1440p (png)",
                        Title: "Save the chart as a 2560x1440 PNG image",
                        IconClass: "IconFilePng",
                        Data: async () => await SaveImage(close, 2560, 1440, transparent, fmtPng),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "Image 4K UHD (png)",
                        Title: "Save the chart as a 3840x2160 PNG image",
                        IconClass: "IconFilePng",
                        Data: async () => await SaveImage(close, 3840, 2160, transparent, fmtPng),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "Image A4 low DPI (png)",
                        Title: "Save the chart as a " + A4_wl + "x" + A4_hl + " PNG image.\nThis is ideal for a " + ldpi + " DPI printing on an A4 with 10 mm margins.",
                        IconClass: "IconFilePng",
                        Data: async () => await SaveImage(close, A4_wl, A4_hl, transparent, fmtPng),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "Image A4 high DPI (png)",
                        Title: "Save the chart as a " + A4_wh + "x" + A4_hh + " PNG image.\nThis is ideal for a " + hdpi + " DPI printing on an A4 with 10 mm margins.",
                        IconClass: "IconFilePng",
                        Data: async () => await SaveImage(close, A4_wh, A4_hh, transparent, fmtPng),
                    }));
                    //  Size menu (sub items)
                    menuDest = sizeMenu.Children;

                    /*
                    Most increase canvas size for it to work, how?
                    menuDest.push(WebMenuItem.From({
                        Name: "50%",
                        Title: "Display using the default size",
                        IconClass: "IconChartSize50",
                        Flags: chartScale == 0.5 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 0.5),
                    }));
    
                    menuDest.push(WebMenuItem.From({
                        Name: "75%",
                        Title: "Display using the default size",
                        IconClass: "IconChartSize75",
                        Flags: chartScale == 0.75 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 0.75),
                    }));
                    */


                    menuDest.push(WebMenuItem.From({
                        Name: "100%",
                        Title: "Display using the default size",
                        IconClass: "IconChartSize100",
                        Flags: chartScale === 1 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 1),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "125%",
                        Title: "Display using 125% of the chart size",
                        IconClass: "IconChartSize125",
                        Flags: chartScale === 1.25 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 1.25),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "150%",
                        Title: "Display using 150% of the chart size",
                        IconClass: "IconChartSize150",
                        Flags: chartScale === 1.5 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 1.5),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "200%",
                        Title: "Display using 200% of the chart size",
                        IconClass: "IconChartSize200",
                        Flags: chartScale === 2 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 2),
                    }));
                    menuDest.push(WebMenuItem.From({
                        Name: "250%",
                        Title: "Display using 250% of the chart size",
                        IconClass: "IconChartSize250",
                        Flags: chartScale === 2.5 ? checkedReadonly : 0,
                        Data: async () => SetChartScale(close, 2.5),
                    }));


                    return menu;
                }, true, true);
            }, null, null, true);

            chart.appendChild(menuIcon.Element);



            /**
             * Keyboard shortcuts (assigned to toElement.onkeydown, overwriting any existing handler; logs every key).
             * Unless the colour menu is disabled (chartOptions or data.DisableColorMenu): o = original colours,
             * l = label hash, g = random value gradient, s = random series gradient, m = random magnitude gradient.
             * t / v toggle coloured labels / values and rebuild. Space (unless the order menu is disabled)
             * cycles forcedOrder 0 -> 1 -> -1 -> 2 -> -2 ... -> +-(datasets + 1) -> 0, shows an Info toast
             * (1500 ms) and calls SetOrder.
             * @param {KeyboardEvent} ev - The key event.
             * @returns {Promise<void>}
             */
            toElement.onkeydown = async ev => {
                console.log("Key: " + ev.key);
                if (!(chartOptions.DisableColorMenu || main?.Data?.DisableColorMenu)) {
                    if (ev.key === "o")
                        SetChartColor(null, null);
                    if (ev.key === "g")
                        SetChartColor(null, colorRandomValueGradient);
                    if (ev.key === "s")
                        SetChartColor(null, colorRandomSeriesGradient);
                    if (ev.key === "m")
                        SetChartColor(null, colorRandomMagnitudeGradient);
                    if (ev.key === "l")
                        SetChartColor(null, colorLabelText);
                }
                if (ev.key === "t") {
                    forceColorLabels ^= true;
                    RebuildChart();
                }
                if (ev.key === "v") {
                    forceColorValues ^= true;
                    RebuildChart();
                }
                if (!(chartOptions.DisableOrderMenu || main?.Data?.DisableOrderMenu)) {
                    if (ev.key === " ") {

                        if (forcedOrder === 0) {
                            forcedOrder = 1;
                        } else {
                            let desc = forcedOrder < 0;
                            if (desc)
                                forcedOrder = -forcedOrder;
                            if (desc)
                                ++forcedOrder;
                            desc ^= true;
                            if (forcedOrder > (main.data.datasets.length + 1)) {
                                forcedOrder = 0;
                            } else {
                                if (desc)
                                    forcedOrder = -forcedOrder;
                            }
                        }
                        const dur = 1500;
                        if (forcedOrder === 0)
                            Info("Using original order", dur, true);
                        else {
                            const dd = main.data;
                            const ds = main.data.datasets;
                            const isDesc = forcedOrder < 0;
                            const otype = isDesc ? -forcedOrder : forcedOrder;
                            if (otype === 1) {

                                Info(isDesc ? "Ordered by label name in reverse" : "Ordered by label name", dur, true);
                            } else {
                                const label = ds[otype - 2].label;
                                Info("Ordered by " + label + " value " + (isDesc ? " from high to low" : " from low to high"), dur, true);
                            }
                        }
                        SetOrder(null, forcedOrder);
                    }
                }
            };

        }

        /** @type {string} Theme background colour (--ThemeBackground), set by UpdateColor; used by the background plugin. */
        let themeBackgroundColor;
        /**
         * Chart.js plugin "customCanvasBackgroundColor": applies the export font scale (canvas.Scale) and paints
         * the background. Note: the live chart always has the expando Transparent = true (set in DoUpdate /
         * RebuildChart), so only exports rendered with transparent = false get a filled background.
         * @type {object}
         */
        const renderBackgroundPlugin = {
            id: 'customCanvasBackgroundColor',

            /**
             * When canvas.Scale is set and not 1, sets layout.padding so that the drawing scaled by Scale fills
             * the canvas (4 px top/left, enlarged right/bottom); otherwise padding 4.
             * @param {Chart} chart - The chart.
             * @param {object} args - Plugin hook arguments.
             * @param {object} options - Plugin options.
             */
            beforeLayout: (chart, args, options) => {
                const s = chart.ctx.canvas.Scale;
                if ((s) && (s !== 1)) {
                    const w = chart.width;
                    const h = chart.height;
                    chart.config.options.layout.padding =
                    {
                        top: 4,
                        left: 4,
                        right: w - ((w - 8) / s) + 4,
                        bottom: h - ((h - 8) / s) + 4,
                    };
                } else {
                    chart.config.options.layout.padding = 4;
                }
            },

            /**
             * Saves the context and scales it by canvas.Scale; unless chart.Transparent fills the canvas behind
             * the chart (globalCompositeOperation "destination-over") with options.color or themeBackgroundColor.
             * The matching ctx.restore() is in afterDraw.
             * @param {Chart} chart - The chart.
             * @param {object} args - Plugin hook arguments.
             * @param {object} options - Plugin options (color).
             */
            beforeDraw: (chart, args, options) => {
                const { ctx } = chart;
                ctx.save();
                let s = chart.ctx.canvas.Scale;
                if ((s) && (s !== 1))
                    ctx.scale(s, s);
                else
                    s = 1;
                if (chart.Transparent)
                    return;
                ctx.save();
                ctx.globalCompositeOperation = 'destination-over';
                ctx.fillStyle = options.color || themeBackgroundColor;
                ctx.fillRect(0, 0, chart.width / s, chart.height / s);
                ctx.restore();
            },
            /**
             * Restores the context saved (and scaled) in beforeDraw.
             * @param {Chart} chart - The chart.
             * @param {object} args - Plugin hook arguments.
             * @param {object} options - Plugin options.
             */
            afterDraw: (chart, args, options) => {
                const { ctx } = chart;
                ctx.restore();
            }
        };

        /**
         * Chart.js plugin "dropShadow": draws dataset elements with a canvas shadow when the config has
         * options.plugins.shadow = {dx, dy, rad, color} (color defaults to rgba(0,0,0,0.5)).
         * @type {object}
         */
        const dropShadowPlugin = {
            id: 'dropShadow',

            /**
             * Raises layout.padding (number or per side) to at least max(|dx|, |dy|) + rad so the shadow fits.
             * @param {Chart} chart - The chart.
             * @param {object} args - Plugin hook arguments.
             * @param {object} options - Plugin options.
             */
            beforeLayout: (chart, args, options) => {
                const so = chart.config?.options?.plugins?.shadow;
                if (!so)
                    return;
                const dx = so.dx;
                const dy = so.dy;
                let rad = so.rad;
                if (rad <= 0)
                    rad = 0;
                const pdist = Math.max(Math.abs(dx), Math.abs(dy)) + rad;
                const o = chart.config.options;
                const l = o.layout;
                if (l) {
                    const p = l.padding;
                    if (p) {
                        if (typeof p === "number") {
                            if (p < pdist)
                                l.padding = pdist;
                        } else {
                            if (p.top < pdist)
                                p.top = pdist;
                            if (p.right < pdist)
                                p.right = pdist;
                            if (p.bottom < pdist)
                                p.bottom = pdist;
                            if (p.left < pdist)
                                p.left = pdist;
                        }
                    } else {
                        l.padding = pdist;
                    }
                } else {
                    o.layout = {
                        padding: pdist,
                    };
                }
            },
            /**
             * Saves the context and sets shadowColor, shadowOffsetX/Y and shadowBlur before each dataset is drawn.
             * @param {Chart} chart - The chart.
             * @param {object} args - Plugin hook arguments.
             * @param {object} options - Plugin options.
             */
            beforeDatasetDraw: (chart, args, options) => {
                const so = chart.config?.options?.plugins?.shadow;
                if (!so)
                    return;
                const dx = so.dx;
                const dy = so.dy;
                let rad = so.rad;
                if (rad <= 0)
                    rad = 0;
                let col = so.color;
                if (!col)
                    col = "rgba(0,0,0,0.5)";

                const ctx = chart.ctx;
                ctx.save();
                ctx.shadowColor = col;
                ctx.shadowOffsetX = dx;
                ctx.shadowOffsetY = dy;
                ctx.shadowBlur = rad;
            },
            /**
             * Restores the context saved in beforeDatasetDraw (only when a shadow is configured).
             * @param {Chart} chart - The chart.
             * @param {object} args - Plugin hook arguments.
             * @param {object} options - Plugin options.
             */
            afterDatasetDraw: (chart, args, options) => {
                const so = chart.config?.options?.plugins?.shadow;
                if (!so)
                    return;
                chart.ctx.restore();
            }
        };



        // Global Chart.js setup (runs on every addChart call): register the plugins and replace the
        // datalabels defaults and tick / tooltip callbacks with ValueFormatter based ones.
        Chart.register(renderBackgroundPlugin);
        Chart.register(dropShadowPlugin);
        Chart.register(ChartDataLabels);

        /** @type {object} Chart.defaults (global, shared by all charts on the page). */
        const def = Chart.defaults;
        def.plugins.datalabels = {
            display: "auto",
            align: "start",
            anchor: "end",
            font:
            {
                size: 11,
                weight: "bold",
            },
            borderRadius: 8,
            padding: 4,
            formatter: ValueFormatter,
        };

 

        def.scale.ticks.callback = ValueFormatter;
        //def.scales.category.ticks.callback = ValueFormatter;
        def.scales.linear.ticks.callback = ValueFormatter;
        def.scales.logarithmic.ticks.callback = ValueFormatter;
        def.scales.radialLinear.ticks.callback = ValueFormatter;
        def.scales.time.ticks.callback = ValueFormatter;
        def.scales.timeseries.ticks.callback = ValueFormatter;

        def.scales.timeseries.ticks.callback = ValueFormatter;

        /**
         * Category tick label callback: shows only the first line of the label (up to "\n", trimmed).
         * @param {number} value - Tick value (category index).
         * @param {number} index - Tick index.
         * @param {object[]} ticks - All ticks.
         * @returns {string} Tick text.
         */
        def.scales.category.ticks.callback = function (value, index, ticks) {
            value = this.getLabelForValue(value);
            const i = value.indexOf('\n');
            if (i >= 0)
                value = value.substring(0, i).trim();
            return value;
        };

        /**
         * Tooltip label callback: "<dataset label>: " + ValueFormatter(context.raw) (no prefix without label).
         * @param {object} context - Chart.js tooltip item context.
         * @returns {string} Tooltip text.
         */
        def.plugins.tooltip.callbacks.label = context => {
            let label = context.dataset.label || '';
            if (label)
                label += ": ";
            label += ValueFormatter(context.raw);
            return label;
        };
        /**
         * Copy the theme CSS variables of document.body into Chart.defaults and rebuild the chart.
         * themeBackgroundColor = --ThemeBackground, font.family = --ThemeFont, font sizes 14 (datalabels 11),
         * color = --ThemeMain, borderColor = rgba(--ThemeMainRGB, 0.07), arc border = background colour with
         * width 1, datalabels background rgba(--ThemeBackgroundRGB, 0.65) and colour --ThemeAcc1; then calls
         * chartOptions.OnDefaults(def) and RebuildChart(). Runs once during addChart and 10 ms after each
         * InterOp "Theme.Changed" message.
         */
        function UpdateColor()
        {
            const style = getComputedStyle(document.body);
            themeBackgroundColor = style.getPropertyValue('--ThemeBackground');
            def.font.family = style.getPropertyValue('--ThemeFont');
            def.plugins.datalabels.font.size = 11;
            def.font.size = 14;
            const mainRgb = "rgba(" + style.getPropertyValue('--ThemeMainRGB');
            const mainC = style.getPropertyValue('--ThemeMain');
            def.color = mainC;
            //def.backgroundColor = mainRgb + ",0.1)";
            def.borderColor = mainRgb + ",0.07)";
            def.elements.arc.borderColor = themeBackgroundColor;// mainRgb + ",0.5)";
            def.elements.arc.borderWidth = 1;
            def.plugins.datalabels.backgroundColor = "rgba(" + style.getPropertyValue('--ThemeBackgroundRGB') + ",0.65)";
            def.plugins.datalabels.color = style.getPropertyValue('--ThemeAcc1');
            const onDefaults = chartOptions.OnDefaults;
            if (onDefaults)
                onDefaults(def);
            RebuildChart();
        }

        UpdateColor();


        // Re-apply the theme when another page / frame broadcasts "Theme.Changed" (common.js InterOp).
        InterOp.AddListener(ev => {
            if (InterOp.GetMessage(ev).Type === "Theme.Changed")
                setTimeout(UpdateColor, 10);
        });


        /**
         * Check that an array exists and has exactly the given length; used by the live diff to decide whether
         * per-point colour arrays must be spliced / copied along with the data.
         * @param {?any[]} a - Array (or anything falsy).
         * @param {number} l - Expected length.
         * @returns {boolean} True when `a` is truthy and a.length === l.
         */
        function HaveArrayOffLen(a, l) {
            if (!a)
                return false;
            const al = a.length;
            return al === l;
        }


        /*
        new ResizeObserver(() => {
            const nw = chart.parentElement.offsetWidth;
            const nh = chart.parentElement.offsetHeight;
            console.warn("Resize " + nw + "x" + nh);
            if (chart) {
                chart.style.width = nw + "px";
                chart.style.height = nh + "px";
            }
            if (main)
                main.update();
        }).observe(chart.parentElement);
        */

        /** @type {number} Polling interval in ms (data.RefreshRate, min 500; default 15000). */
        let refreshRate = 15000;

        /**
         * Polling loop: fetch the server config and render or diff it into the live chart, then reschedule.
         * Fetches with sendRequest(url, tableParams) (POST) when tableParams is set, else getRequest(url) (GET);
         * awaits chartOptions.OnServerData; ensures options, options.plugins, options.layout; stores
         * orgDataStr and orgDataType; runs ApplyChanges; awaits chartOptions.OnFixedData; updates
         * document.title from options.plugins.title.text[0].
         * First time: creates `main = new Chart(canvas, data)` (Transparent = true) and calls onFirstLoad once.
         * Later: copies the title and diffs into main.data so animations stay smooth: datasets matched by
         * `label` (removed, or inserted as placeholders then filled), x positions matched by label text (labels,
         * data and same-length colour arrays spliced), values and array colours copied per index, then
         * main.update(). Updates refreshRate from data.RefreshRate (min 500) and sets main.Data = data.
         * On exception: Fail("Failed to fetch data: ..."), calls onFirstLoad if still pending, waits 3 s.
         * Always reschedules itself with setTimeout(DoUpdate, refreshRate).
         * @returns {Promise<void>}
         */
        async function DoUpdate() {
            try {
                let data = tableParams ? await sendRequest(url, tableParams) : await getRequest(url);
                if (data) {
                    let fn = chartOptions.OnServerData;
                    if (fn != null)
                        await fn(data);
                    if (!data.options)
                        data.options = {};
                    if (!data.options.plugins)
                        data.options.plugins = {};
                    if (!data.options.layout)
                        data.options.layout = {};

                    orgDataStr = JSON.stringify(data);
                    const currentIsHorizontal = data.options.indexAxis === "y";
                    orgDataType = data.type + (currentIsHorizontal ? "h" : "v");
                    ApplyChanges(data);
                    fn = chartOptions.OnFixedData;
                    if (fn != null)
                        await fn(data);

                    const nd = data.data;
                    const nds = nd.datasets;
                    const nl = nd.labels;
                    const nll = nl.length;
                    const tt = data.options?.plugins?.title?.text;
                    const haveText = tt && (tt.length > 0);
                    if (haveText) {
                        const t0 = tt[0];
                        if (t0 && (document.title !== t0))
                            document.title = t0;
                    }
                    if (main == null) {
                        //                        canvas.Scale = chartScale;
                        main = new Chart(canvas, data);
                        main.Transparent = true;
                        if (onFirstLoad) {
                            onFirstLoad();
                            onFirstLoad = null;
                        }
                    } else {
                        if (haveText) {
                            if (!main.options)
                                main.options = {};
                            if (!main.options.plugins)
                                main.options.plugins = {};
                            if (!main.options.plugins.title)
                                main.options.plugins.title = {};
                            main.options.plugins.title.text = tt;
                        }
                        //  Determine deleted and added data sets (analyse current)
                        const cd = main.data;
                        const cds = cd.datasets;
                        const cdsl = cds.length;
                        const removeDsMap = new Map();
                        for (let i = 0; i < cdsl; ++i)
                            removeDsMap.set(cds[i].label, i);
                        //  Determine deleted and added data sets (analyse new)
                        const ndsl = nds.length;
                        const addDsMap = new Map();
                        for (let i = 0; i < ndsl; ++i) {
                            const l = nds[i].label;
                            if (typeof removeDsMap.get(l) === "undefined")
                                addDsMap.set(l, i);
                            else
                                removeDsMap.delete(l);
                        }
                        //  Delete data sets
                        const removeDs = Array.from(removeDsMap, ([l, i]) => i);
                        let removeDsl = removeDs.length;
                        if (removeDsl > 0) {
                            removeDs.sort(); // TODO: Any preformance gain?
                            while (removeDsl > 0) {
                                --removeDsl;
                                const removeIndex = removeDs[removeDsl];
                                cds.splice(removeIndex, 1);
                            }
                        }

                        //  Add data sets
                        const addDs = Array.from(addDsMap, ([l, i]) => i);
                        let addDsl = addDs.length;
                        if (addDsl > 0) {
                            addDs.sort();
                            for (let i = 0; i < addDsl; ++i) {
                                const addIndex = addDs[i];
                                cds.splice(addIndex, 0, null);
                            }
                        }

                        //  Determine deleted and added X (analyse current)
                        const cl = cd.labels;
                        const cll = cl.length;
                        const removeMap = new Map();
                        for (let i = 0; i < cll; ++i)
                            removeMap.set(cl[i], i);
                        //  Determine deleted and added X (analyse new)
                        const addMap = new Map();
                        for (let i = 0; i < nll; ++i) {
                            const l = nl[i];
                            if (typeof removeMap.get(l) === "undefined")
                                addMap.set(l, i);
                            else
                                removeMap.delete(l);
                        }
                        //  Delete data (X)
                        const remove = Array.from(removeMap, ([l, i]) => i);
                        let removel = remove.length;
                        let expectedLength = cll;
                        if (removel > 0) {
                            remove.sort(); // TODO: Any preformance gain?
                            while (removel > 0) {
                                --removel;
                                const removeIndex = remove[removel];
                                //  Labels
                                cl.splice(removeIndex, 1);
                                //  Data
                                cds.forEach(ds => {
                                    if (ds) {
                                        ds.data.splice(removeIndex, 1);
                                        if (HaveArrayOffLen(ds.backgroundColor, expectedLength))
                                            ds.backgroundColor.splice(removeIndex, 1);
                                        if (HaveArrayOffLen(ds.borderColor, expectedLength))
                                            ds.borderColor.splice(removeIndex, 1);
                                    }
                                });
                                --expectedLength;
                            }
                        }
                        //  Add data (X)
                        const add = Array.from(addMap, ([l, i]) => i);
                        let addl = add.length;
                        if (addl > 0) {
                            add.sort();
                            for (let i = 0; i < addl; ++i) {
                                const addIndex = add[i];
                                //  Labels
                                cl.splice(addIndex, 0, nl[addIndex]);
                                //  Data
                                cds.forEach(ds => {
                                    if (ds) {
                                        ds.data.splice(addIndex, 0, null);
                                        if (HaveArrayOffLen(ds.backgroundColor, expectedLength))
                                            ds.backgroundColor.splice(addIndex, 0, null);
                                        if (HaveArrayOffLen(ds.borderColor, expectedLength))
                                            ds.borderColor.splice(addIndex, 0, null);
                                    }
                                });
                                ++expectedLength;
                            }
                        }
                        //  Update values X / Y
                        for (let i = 0; i < ndsl; ++i) {
                            const cdss = cds[i];
                            const ndss = nds[i];
                            if (cdss === null) {
                                cds[i] = ndss;
                            } else {
                                const cdsd = cdss.data;
                                const ndsd = ndss.data;
                                for (let j = 0; j < nll; ++j) {
                                    const cv = cdsd[j];
                                    const nv = ndsd[j];
                                    //                                    if (cv === null)
                                    if (cv !== nv)
                                        cdsd[j] = nv;
                                    //                                    else
                                    //                                        cv.y = nv.y;
                                }
                                let cb = cdss.backgroundColor;
                                let nb = ndss.backgroundColor;
                                if (cb && nb) {
                                    if (Array.isArray(cb)) {
                                        const l = cb.length;
                                        for (let j = 0; j < l; ++j)
                                            cb[j] = nb[j];
                                    }
                                }
                                cb = cdss.borderColor;
                                nb = ndss.borderColor;
                                if (cb && nb) {
                                    if (Array.isArray(cb)) {
                                        const l = cb.length;
                                        for (let j = 0; j < l; ++j)
                                            cb[j] = nb[j];
                                    }
                                }
                            }
                        }
                        main.update();
                    }
                    const newRate = data.RefreshRate;
                    if (newRate && (newRate > 0))
                        refreshRate = newRate;
                    if (refreshRate < 500)
                        refreshRate = 500;
                    main.Data = data;
                }
            }
            catch (ex) {
                //  If we failed to fetch the data, retry in a second
                Fail("Failed to fetch data: " + ex);
                if (onFirstLoad) {
                    onFirstLoad();
                    onFirstLoad = null;
                }
                await delay(3000);
            }
            setTimeout(async ev => {
                await DoUpdate();
            }, refreshRate);
        }
        // Start polling; await the first fetch only when WaitFirst.
        if (chartOptions.WaitFirst)
            await DoUpdate();
        else
            DoUpdate();
        return chart;
    }


    /**
     * Data label mode 1 (label + value): ensures options.plugins.datalabels, sets display = true and a
     * formatter showing the x label, a newline and the formatted value.
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static addDataLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        /**
         * Returns labels[dataIndex] + "\n" + ValuePrefix + ValueFormat.toString(value, Precision ?? 0) + ValueSuffix.
         * @param {any} value - Data value.
         * @param {object} context - datalabels context (dataIndex).
         * @returns {string} Label text.
         */
        l.formatter = (value, context) => {

            const precision = config.Precision ?? 0;
            const valuePrefix = config.ValuePrefix ?? "";
            const valueSuffix = config.ValueSuffix ?? "";
            return config.data.labels[context.dataIndex] + "\n" + valuePrefix + ValueFormat.toString(value, precision) + valueSuffix;
           };
        l.display = true;
    }

    /**
     * Data label mode 2 (label only): ensures options.plugins.datalabels, sets display = true and a formatter
     * returning the x label.
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static onlyDataLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        /**
         * Returns labels[dataIndex].
         * @param {any} value - Data value (unused).
         * @param {object} context - datalabels context (dataIndex).
         * @returns {string} Label text.
         */
        l.formatter = (value, context) => {
            return config.data.labels[context.dataIndex];
        };
        l.display = true;
    }

    /**
     * Data label mode 4 (none): ensures options.plugins.datalabels and sets display = false.
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static noLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        l.display = false;
    }

    /**
     * Data label mode 3 (first line of the label): ensures options.plugins.datalabels, sets display = true and
     * a formatter returning the first line of the x label.
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static firstLineDataLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        /**
         * Returns the first line of labels[dataIndex] ("" when missing).
         * @param {any} value - Data value (unused).
         * @param {object} context - datalabels context (dataIndex).
         * @returns {string} Label text.
         */
        l.formatter = (value, context) => {
            return (config.data.labels[context.dataIndex] ?? "").split('\n')[0];
        };
        l.display = true;
    }

    /**
     * Default data label mode (0, formatted value): only if options.plugins.datalabels exists, sets
     * display = true and a formatter returning prefix + formatted value + suffix; otherwise does nothing
     * (the global ValueFormatter default applies).
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static formattedValueLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l)
            return;
        /**
         * Returns ValuePrefix + ValueFormat.toString(value, Precision ?? 0) + ValueSuffix.
         * @param {any} value - Data value.
         * @param {object} context - datalabels context (unused).
         * @returns {string} Label text.
         */
        l.formatter = (value, context) => {

            const precision = config.Precision ?? 0;
            const valuePrefix = config.ValuePrefix ?? "";
            const valueSuffix = config.ValueSuffix ?? "";
            return valuePrefix + ValueFormat.toString(value, precision) + valueSuffix;
        };
        l.display = true;
    }

    /**
     * Data label mode 6 (bare number): only if options.plugins.datalabels exists, sets display = true and a
     * formatter returning the formatted value without prefix/suffix.
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static bareValueLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l)
            return;
        /**
         * Returns ValueFormat.toString(value, Precision ?? 0).
         * @param {any} value - Data value.
         * @param {object} context - datalabels context (unused).
         * @returns {string} Label text.
         */
        l.formatter = (value, context) => {

            const precision = config.Precision ?? 0;
            return ValueFormat.toString(value, precision);
        };
        l.display = true;
    }

    /**
     * Data label mode 5 (raw value): only if options.plugins.datalabels exists, sets display = true and
     * formatter = null (the datalabels plugin renders the raw value).
     * @param {object} config - Chart config (options.plugins must exist; mutated).
     */
    static valueLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l)
            return;
        l.formatter = null;
        l.display = true;
    }

    

}

/**
 * Assign a value at a dot-separated path below an object, e.g. "options.scales.*x.ticks.font.size".
 * Intermediate segments are created as {} when missing, except segments prefixed with "*", which must
 * already exist (otherwise the function returns without assigning). The last segment is always assigned.
 * @param {object} obj - Root object (mutated).
 * @param {string} path - Dot-separated path.
 * @param {any} v - Value to assign.
 */
function setValue(obj, path, v) {
    const s = path.split('.');
    const sl = s.length - 1;
    for (let i = 0; i < sl; ++i) {
        let vv = s[i];
        const me = vv[0] === '*';
        if (me)
            vv = vv.substring(1);
        let v = obj[vv];
        if (!v) {
            if (me)
                return;
            v = {};
            obj[vv] = v;
        }
        obj = v;
    }
    obj[s[sl]] = v;
}

/**
 * Entry point of chart.html (called from <body onload>).
 * Shows a loader (AddLoading; removed in `finally` and also passed as onFirstLoad), hides the body overflow,
 * reads the URL parameters with getUrlParams() (lower-cased keys). Without `q` calls
 * Fail("No query parameter specified!") and returns. Parses `p` as JSON (POST body for the data request) and
 * builds a CanvasChartOptions: m / mo / mc = "false" disable the menu / order menu / colour menu;
 * l=true -> ValueLabel 1, novalues=true -> 2, nolabels=true -> 4, valuelabel=N overrides; minvalue, maxvalue,
 * maxcount -> MinValue, MaxValue, MaxCount; transparent=true -> Transparent; plus the callbacks OnServerData,
 * OnFixedData, OnDefaults and OnClick (see below). Awaits CanvasChart.addChart(q, null, p, removeLoader,
 * options) and adds class ChartCenter to the returned element when center=true.
 * @returns {Promise<void>}
 */
async function chartMain() {
    const removeLoader = AddLoading();
    try {
        document.body.style.overflow = "hidden";



        const ps = getUrlParams();
        const url = ps.get('q');
        if (!url) {
            Fail("No query parameter specified!");
            return;
        }

        /**
         * Normalise a colour URL parameter: a 3- or 6-character value not starting with "r" gets a "#"
         * prefix (hex without "#"); anything else is returned unchanged.
         * @param {string} text - Parameter value.
         * @returns {string} CSS colour.
         */
        function parseCol(text) {
            const tl = text.length;
            if ((tl === 3) || (tl === 6)) {
                if (text[0] !== 'r')
                    return '#' + text;
            }
            return text;
        }

        let tp = ps.get('p');
        if (tp)
            tp = JSON.parse(tp);
        const o = new CanvasChartOptions();
        if (ps.get("m") === "false")
            o.DisableMenu = true;
        if (ps.get("mo") === "false")
            o.DisableOrderMenu = true;
        if (ps.get("mc") === "false")
            o.DisableColorMenu = true;

        /**
         * Overrides config.type with the `type` URL parameter when present and sets document.title from
         * config.Title.
         * @param {object} config - Raw server config (mutated).
         */
        o.OnServerData = config => {
            let val = ps.get("type");
            if (val != null)
                config.type = val;
            var title = config.Title;
            if (title != null)
                if (document.title != title)
                    document.title = title;
        };
        if (ps.get("l") === "true")
            o.ValueLabel = 1;
        if (ps.get("novalues") === "true")
            o.ValueLabel = 2;
        if (ps.get("nolabels") === "true")
            o.ValueLabel = 4;
        const vl = ps.get("valuelabel");
        if (vl)
            o.ValueLabel = parseInt(vl);

        const miv = ps.get("minvalue");
        if (miv)
            o.MinValue = parseFloat(miv);

        const mav = ps.get("maxvalue");
        if (mav)
            o.MaxValue = parseFloat(mav);

        const mac = ps.get("maxcount");
        if (mac)
            o.MaxCount = parseInt(mac);

        /**
         * Applies display URL parameters after ApplyChanges: bordersize / bordercol on dataset 0 (borderWidth,
         * borderColor via parseCol); scale-font-size (or font-size), scale-font (or font), scale-font-style
         * (or font-style), scale-font-weight (or font-weight, parsed as float) into ticks.font.* of the existing
         * x, x2, y, r scales (setValue with "*" segments); aspect -> options.maintainAspectRatio (=== "true").
         * @param {object} config - Render config (mutated).
         */
        o.OnFixedData = config => {
            let val = ps.get("bordersize");
            if (val != null)
                config.data.datasets[0].borderWidth = parseFloat(val);
            val = ps.get("bordercol");
            if (val != null)
                config.data.datasets[0].borderColor = parseCol(val);

            val = ps.get("scale-font-size") ?? ps.get("font-size");
            if (val != null) {
                val = parseFloat(val);
                setValue(config, "options.scales.*x.ticks.font.size", val);
                setValue(config, "options.scales.*x2.ticks.font.size", val);
                setValue(config, "options.scales.*y.ticks.font.size", val);
                setValue(config, "options.scales.*r.ticks.font.size", val);
            }
            val = ps.get("scale-font") ?? ps.get("font");
            if (val != null) {
                setValue(config, "options.scales.*x.ticks.font.family", val);
                setValue(config, "options.scales.*x2.ticks.font.family", val);
                setValue(config, "options.scales.*y.ticks.font.family", val);
                setValue(config, "options.scales.*r.ticks.font.family", val);
            }
            val = ps.get("scale-font-style") ?? ps.get("font-style");
            if (val != null) {
                setValue(config, "options.scales.*x.ticks.font.style", val);
                setValue(config, "options.scales.*x2.ticks.font.style", val);
                setValue(config, "options.scales.*y.ticks.font.style", val);
                setValue(config, "options.scales.*r.ticks.font.style", val);
            }
            val = ps.get("scale-font-weight") ?? ps.get("font-weight");
            if (val != null) {
                val = parseFloat(val);
                setValue(config, "options.scales.*x.ticks.font.weight", val);
                setValue(config, "options.scales.*x2.ticks.font.weight", val);
                setValue(config, "options.scales.*y.ticks.font.weight", val);
                setValue(config, "options.scales.*r.ticks.font.weight", val);
            }
            val = ps.get("aspect");
            if (val != null)
                setValue(config, "options.maintainAspectRatio", val === "true");
        };
        /**
         * Applies URL parameters to the global Chart.js defaults on every theme refresh: legend=false hides the
         * legend; textcol, backgroundcol, bordercol (via parseCol) set def.color / backgroundColor / borderColor
         * and the matching datalabels colours; font, font-style, font-weight set def.font and the datalabels
         * font; font-size sets both sizes and scales datalabels borderRadius (8) and padding (4) by size / 11;
         * label-padding, label-radius override those; line-height sets def.font.lineHeight.
         * @param {object} def - Chart.defaults (mutated).
         */
        o.OnDefaults = def =>
        {
            if (ps.get("legend") === "false")
                def.plugins.legend.display = false;
            let val = ps.get("textcol");
            if (val != null) {
                val = parseCol(val);
                def.color = val;
                def.plugins.datalabels.color = val;
            }
            val = ps.get("backgroundcol");
            if (val != null) {
                val = parseCol(val);
                def.backgroundColor = val;
                def.plugins.datalabels.backgroundColor = val;
            }
            val = ps.get("bordercol");
            if (val != null) {
                val = parseCol(val);
                def.borderColor = val;
                def.plugins.datalabels.borderColor = val;
            }
            val = ps.get("font");
            if (val != null) {

                def.plugins.datalabels.font.family = val;
                def.font.family = val;
            } 
            val = ps.get("font-style");
            if (val != null) {
                def.plugins.datalabels.font.style = val;
                def.font.style = val;
            }
            val = ps.get("font-size");
            if (val != null) {
                val = parseFloat(val);
                def.plugins.datalabels.font.size = val;
                const scale = val / 11;
                def.plugins.datalabels.borderRadius = 8 * scale;
                def.plugins.datalabels.padding = 4 * scale;
                def.font.size = val;
            }
            val = ps.get("label-padding");
            if (val != null) {
                val = parseFloat(val);
                def.plugins.datalabels.padding = val;
            }
            val = ps.get("label-radius");
            if (val != null) {
                val = parseFloat(val);
                def.plugins.datalabels.borderRadius = val;
            }
            val = ps.get("font-weight");
            if (val != null) {
                def.plugins.datalabels.font.weight = val;
                def.font.weight = val;
            }

            val = ps.get("line-height");
            if (val != null)
                def.font.lineHeight = parseFloat(val);

        };
        o.Transparent = ps.get("transparent") === "true";
        const par = window.parent;
        if (par) {
            /**
             * Logs the label and posts {Type: "ChartClick", Label, Value, DatasetIndex, DataIndex} to window.parent
             * (postMessage without a targetOrigin argument; exceptions are swallowed). Installed whenever
             * window.parent is truthy, i.e. always (at top level it is the window itself).
             * @param {string} label - X label of the clicked element.
             * @param {any} value - Value of the clicked element.
             * @param {number} datasetIndex - Dataset index.
             * @param {number} dataIndex - Data index.
             */
            o.OnClick = (label, value, datasetIndex, dataIndex) => {

                console.log("Clicked on label: " + label);
                try {
                    par.postMessage(
                        {
                            Type: "ChartClick",
                            Label: label,
                            Value: value,
                            DatasetIndex: datasetIndex,
                            DataIndex: dataIndex
                        });
                }
                catch
                {
                }
            };
        }
        const el = await CanvasChart.addChart(url, null, tp, removeLoader, o);
        if (ps.get("center") === "true")
            el.classList.add("ChartCenter");

    }
    finally {
        removeLoader();
    }


}