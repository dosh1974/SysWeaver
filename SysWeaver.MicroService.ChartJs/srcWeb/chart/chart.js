

class Random {


    // Create an array of 4 random integers that can be used as a seed
    static GetRandomSeed() {
        return [
            (Math.random() * 4294967295) | 0,
            (Math.random() * 4294967295) | 0,
            (Math.random() * 4294967295) | 0,
            (Math.random() * 4294967295) | 0
        ];
    }

    // Create an array of 4 integers for a given string (using a non-secure hash)
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

    // Seed can be: a string, an array of 4 integers, an integer.
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

class CanvasChartOptions {

    DisableMenu = false;
    OnServerData = null;
    OnFixedData = null;
    OnDefaults = null;
    OnClick = null;
}

class CanvasChart {


    static async getChartExportMenu() {
        let m = CanvasChart.MenuExportItems;
        if (m)
            return m;
        const current = CanvasChart.CS;
        await Promise.all([
            await includeJs(current, "external/canvas2svg.js"),
            await includeJs(current, "../app/application.js"),
            await includeCss(current, "../app/application.css")
        ]);
        m = await getRequest("../Api/GetChartExporters");
        CanvasChart.MenuExportItems = m;
        return m;
    }

    static CS = document.currentScript.src;
    static MenuExportItems = null;

    static tweakXmlText(text, onXmlDoc) {
        const xmlDoc = new DOMParser().parseFromString(text, "text/xml");
        onXmlDoc(xmlDoc);
        return new XMLSerializer().serializeToString(xmlDoc);
    }

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
     * Add a chart that get it's data from the server (as a achild to some element)
     * @param {string} url The url to the API that returns the chart options
     * @param {HTMLElement} toElement An optional element to add the canvas and button to (defaults to the body)
     * @param {any} tableParams An optional parameter (as an object) that is send to the url
     * @param {function()} onFirstLoad An optional function that is executed after the first successful request
     * @param {CanvasChartOptions} chartOptions Optional options
     */
    static async addChart(url, toElement, tableParams, onFirstLoad, chartOptions) {
        if (!toElement)
            toElement = document.body;
        if (!chartOptions)
            chartOptions = new CanvasChartOptions();
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

        const checked = 2;
        const readonly = 1;
        const checkedReadonly = checked | readonly;

        const chart = document.createElement("SysWeaver-Chart");
        toElement.appendChild(chart);
        let canvas = document.createElement("canvas");
        chart.appendChild(canvas);


        let forceChartType = null;
        let forcedIndexAxis = null;
        let forcedColor = null;
        let forcedColorSeed = null;


        let forceColorLabels = false;
        let forceColorValues = false;


        let forcedOrder = 0; // 0 = Orignal, 1 = Label name, 2+ = datasets

        let orgDataStr = null;
        let main = null;
        let chartScale = 1;

        const colorRandomValueGradient = "RandomValueGradient";
        const colorRandomSeriesGradient = "RandomSeriesGradient";
        const colorRandomMagnitudeGradient = "RandomMagnitudeGradient";
        const colorLabelText = "LabelText";

        const noAxisTypes = new Map();
        noAxisTypes.set("pie", 1);
        noAxisTypes.set("doughnut", 1);
        noAxisTypes.set("polarArea", 1);

        const keepRAxis = new Map();
        keepRAxis.set("polarArea", 1);


        let precision = -2;
        let valuePrefix = "";
        let valueSuffix = "";

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
        function hslToRgb(h, s, l) {
            var r, g, b;

            if (h < 0)
                h += 10;
            if (h >= 1)
                h = h % 1;

            if (s == 0) {
                r = g = b = l; // achromatic
            } else {
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

        function GetRgbaColorValues(cssColor) {
            const colorContext = GetRgbaColorValues.Context ?? document.createElement('canvas').getContext('2d');
            GetRgbaColorValues.Context = colorContext;
            colorContext.wi
            colorContext.reset();
            colorContext.fillStyle = cssColor;
            colorContext.fillRect(0, 0, 4, 4);
            return colorContext.getImageData(1, 1, 1, 1).data;
        }


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

        function ApplyChanges(data) {
            const style = getComputedStyle(document.body);

            precision = data.Precision;
            valuePrefix = data.ValuePrefix ?? "";
            valueSuffix = data.ValueSuffix ?? "";
            switch (data.ValueLabel) {
                case 1:
                    CanvasChart.addDataLabels(data);
                    break;
                case 2:
                    CanvasChart.onlyDataLabels(data);
                    break;
                case 3:
                    CanvasChart.firstLineDataLabels(data);
                    break;
                default:
                    CanvasChart.valueLabels(data);
                    break;
            }
            if (!precision)
                if (typeof precision !== "number")
                    precision = -2;

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
            if (!data.options.layout)
                data.options.layout = {};

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



            if (forcedColor) {

                function MakeRgb(rgb) {
                    return "rgb(" + (rgb[0] | 0) + "," + (rgb[1] | 0) + "," + (rgb[2] | 0) + ")";
                }

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

        function RebuildChart(allowAnimation) {
            if (!main)
                return;
            const data = JSON.parse(orgDataStr);
            ApplyChanges(data);
            let restore = () => { };
            if (!allowAnimation) {
                const options = data.options;
                if (options) {
                    const animation = options.animation;
                    if (animation) {
                        const duration = animation.duration;
                        if (typeof duration !== undefined)
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
            main.destroy();
            const nc = document.createElement("canvas");
            canvas.parentElement.replaceChild(nc, canvas);
            canvas = nc;
            //canvas.Scale = chartScale;
            main = new Chart(canvas, data);
            main.Transparent = true;
            main.update();
            if (!allowAnimation)
                setTimeout(restore, 10);
        }

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

        async function GetChartAsImage(data, width, height, transparent, format) {
            let canvas = null;
            let ctx = null;
            switch (format) {
                case "Png":
                    canvas = document.createElement("canvas");
                    canvas.setAttribute("width", "" + width);
                    canvas.setAttribute("height", "" + height);
                    canvas.width = width + "px";
                    canvas.height = height + "px";
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
                        dataStr = await GetChartAsImage(data, A4_wh, A4_hh, false, false);
                        data = null;
                        break;
                    case 2:
                        dataStr = await GetChartAsImage(data, A4_wh, A4_hh, false, true);
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

        function SetChartType(closeFn, newType, isHorizontal) {
            console.log("Setting chart type to " + newType);
            closeFn();
            const axis = isHorizontal ? "y" : "x";
            forceChartType = newType;
            forcedIndexAxis = axis;
            RebuildChart();
        }

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

        function SetChartScale(closeFn, newScale) {
            console.log("Setting chart scale to " + newScale);
            if (closeFn)
                closeFn();
            chartScale = newScale;
            //RebuildChart();
        }

        function SetOrder(closeFn, newOrder) {
            console.log("Setting new order to " + newOrder);
            if (closeFn)
                closeFn();
            forcedOrder = newOrder;
            RebuildChart(true);
        }


        if (!chartOptions.DisableMenu) {

            const menuIcon = new ColorIcon("IconChartMenu", "IconColorThemeAcc2", 32, 32, "Click to show options", async ev => {
                const menuItems = await CanvasChart.getChartExportMenu();
                if (!orgDataStr) {
                    Fail("No data!");
                    return;
                }
                const data = JSON.parse(orgDataStr);
                ApplyChanges(data);
                PopUpMenu(menuIcon.Element, (close, backEl) => {

                    const pp = backEl.parentElement;
                    pp.classList.add("Chart");
                    const menu = new WebMenu();
                    menu.Name = "Chart";
                    let menuDest;
                    //  Modify type menu
                    const typeMenu = WebMenuItem.From({
                        Name: "Type",
                        IconClass: "IconChartType",
                        Title: "Options for modifying the chart type",
                        Children: [],
                    });
                    menu.Items.push(typeMenu);

                    menuDest = typeMenu.Children;
                    const currentType = data.type;
                    const currentIsHorizontal = data.options.indexAxis === "y";

                    menuDest.push(WebMenuItem.From({
                        Name: "Bar (vertical)",
                        Title: "Display the chart as a vertical bar chart",
                        IconClass: "IconChartTypeBar",
                        Flags: ((currentType === "bar") && (!currentIsHorizontal)) ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "bar"),
                    }));

                    menuDest.push(WebMenuItem.From({
                        Name: "Bar (horizontal)",
                        Title: "Display the chart as a horizontal bar chart",
                        IconClass: "IconChartTypeBarHorizontal",
                        Flags: ((currentType === "bar") && (currentIsHorizontal)) ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "bar", true),
                    }));

                    menuDest.push(WebMenuItem.From({
                        Name: "Line (vertical)",
                        Title: "Display the chart as a vertical line chart",
                        IconClass: "IconChartTypeLine",
                        Flags: ((currentType === "line") && (!currentIsHorizontal)) ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "line"),
                    }));

                    menuDest.push(WebMenuItem.From({
                        Name: "Line (horizontal)",
                        Title: "Display the chart as a horizontal line chart",
                        IconClass: "IconChartTypeLineHorizontal",
                        Flags: ((currentType === "line") && (currentIsHorizontal)) ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "line", true),
                    }));

                    menuDest.push(WebMenuItem.From({
                        Name: "Pie",
                        Title: "Display the chart as a pie chart",
                        IconClass: "IconChartTypePie",
                        Flags: currentType === "pie" ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "pie"),
                    }));

                    menuDest.push(WebMenuItem.From({
                        Name: "Doughnut",
                        Title: "Display the chart as a doughnut chart",
                        IconClass: "IconChartTypeDoughnut",
                        Flags: currentType === "doughnut" ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "doughnut"),
                    }));

                    menuDest.push(WebMenuItem.From({
                        Name: "Polar area",
                        Title: "Display the chart as a polar area chart",
                        IconClass: "IconChartTypePolarArea",
                        Flags: currentType === "polarArea" ? checkedReadonly : 0,
                        Data: () => SetChartType(close, "polarArea"),
                    }));

                    //  Sort menu

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

                    //  Export menu
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



            toElement.onkeydown = async ev => {
                console.log("Key: " + ev.key);
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
                if (ev.key === "t") {
                    forceColorLabels ^= true;
                    RebuildChart();
                }
                if (ev.key === "v") {
                    forceColorValues ^= true;
                    RebuildChart();
                }
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
            };

        }

        let themeBackgroundColor;
        const renderBackgroundPlugin = {
            id: 'customCanvasBackgroundColor',
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
                    chart.config.options.layout.padding = 0;
                }
            },

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
            afterDraw: (chart, args, options) => {
                const { ctx } = chart;
                ctx.restore();
            }
        };

        Chart.register(renderBackgroundPlugin);
        Chart.register(ChartDataLabels);


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
        def.plugins.tooltip.callbacks.label = context => {
            let label = context.dataset.label || '';
            if (label)
                label += ": ";
            label += ValueFormatter(context.raw);
            return label;
        };
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


        InterOp.AddListener(ev => {
            if (InterOp.GetMessage(ev).Type === "Theme.Changed")
                setTimeout(UpdateColor, 10);
        });

        function PadArray(a, l) {
            if (!a)
                return;
            const al = a.length;
            if (al === l)
                return;
            if (al === 0)
                return;
            if (al > l) {
                a.splice(l, al - l);
                return;
            }
            const p = a[al - 1]
            while (l > al) {
                --l;
                a.push(p);
            }
        }

        function HaveArrayOffLen(a, l) {
            if (!a)
                return false;
            const al = a.length;
            return al === l;
        }


        let refreshRate = 15000;
        for (; ;) {
            try {
                let data = tableParams ? await sendRequest(url, tableParams) : await getRequest(url);
                if (data) {
                    let fn = chartOptions.OnServerData;
                    if (fn != null)
                        await fn(data);
                    orgDataStr = JSON.stringify(data);
                    ApplyChanges(data);
                    fn = chartOptions.OnFixedData;
                    if (fn != null)
                        await fn(data);

                    const nd = data.data;
                    const nds = nd.datasets;
                    const nl = nd.labels;
                    const nll = nl.length;
                    const tt = data.options?.plugins?.title?.text;
                    if (tt && (tt.length > 0)) {
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
                }
                await delay(refreshRate);
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
        }
    }


    static addDataLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        l.formatter = (value, context) => {

            const precision = config.Precision ?? 0;
            const valuePrefix = config.ValuePrefix ?? "";
            const valueSuffix = config.ValueSuffix ?? "";
            return config.data.labels[context.dataIndex] + "\n" + valuePrefix + ValueFormat.toString(value, precision) + valueSuffix;
           };
    }

    static onlyDataLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        l.formatter = (value, context) => {
            return config.data.labels[context.dataIndex];
        };
    }

    static firstLineDataLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l) {
            l = {};
            config.options.plugins.datalabels = l;
        }
        l.formatter = (value, context) => {
            return (config.data.labels[context.dataIndex] ?? "").split('\n')[0];
        };
    }



    static valueLabels(config) {
        let l = config.options.plugins.datalabels;
        if (!l)
            return;
        l.formatter = null;
    }

    

}


async function chartMain() {
    const removeLoader = AddLoading();
    try {

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


        const ps = getUrlParams();
        const url = ps.get('q');
        if (!url) {
            Fail("No query parameter specified!");
            return;
        }

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
        o.OnServerData = config => {
            let val = ps.get("type");
            if (val != null)
                config.type = val;
            var title = config.Title;
            if (title != null)
                if (document.title != title)
                    document.title = title;
        };
        o.OnFixedData = config => {
            if (ps.get("l") === "true")
                CanvasChart.addDataLabels(config);
            if (ps.get("novalues") === "true")
                CanvasChart.onlyDataLabels(config);
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
        };
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


        const par = window.parent;
        if (par) {
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

        await CanvasChart.addChart(url, null, tp, removeLoader, o);
    }
    finally {
        removeLoader();
    }


}