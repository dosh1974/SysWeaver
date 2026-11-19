using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// A conversion target: an EcmaScript version and/or a list of browsers (with versions).
    /// A feature is transformed if the EcmaScript version doesn't include it or if any of the browsers doesn't support it.
    /// </summary>
    public sealed class JsTarget
    {
        /// <summary>
        /// The EcmaScript version (5, 2015, 2016 ...) the output must conform to, int.MaxValue if no limit
        /// </summary>
        public readonly int EsVersion;

        /// <summary>
        /// The browsers (normalized: chrome, firefox, safari, edge) and the lowest version that must be supported
        /// </summary>
        public readonly IReadOnlyDictionary<String, double> Browsers;

        /// <summary>
        /// The text this target was created from
        /// </summary>
        public readonly String Text;

        readonly HashSet<JsFeature> Needed;

        /// <summary>
        /// The explicit EcmaScript version limit (5, 2015, 2016 ...) of the target, int.MaxValue if only browsers are specified.
        /// Unlike EsVersion this isn't lowered because a browser lacks a single feature.
        /// </summary>
        public readonly int EsLimit;

        JsTarget(String text, int es, Dictionary<String, double> browsers)
        {
            Text = text;
            EsLimit = es;
            Browsers = browsers;
            var needed = new HashSet<JsFeature>();
            foreach (var f in JsFeatureInfo.All)
            {
                if (f.Es > es)
                {
                    needed.Add(f.Feature);
                    continue;
                }
                foreach (var b in browsers)
                {
                    if (f.Support.TryGetValue(b.Key, out var v) && b.Value < v)
                    {
                        needed.Add(f.Feature);
                        break;
                    }
                }
            }
            Needed = needed;
            //  The effective EcmaScript version is the latest version that has all features supported
            int eff = es;
            foreach (var f in JsFeatureInfo.All)
                if (needed.Contains(f.Feature) && f.Es <= eff)
                    eff = f.Es == 2015 ? 5 : f.Es - 1;
            EsVersion = eff;
        }

        /// <summary>
        /// Returns true if the feature should be transformed for this target
        /// </summary>
        public bool NeedsTransform(JsFeature f) => Needed.Contains(f);

        /// <summary>
        /// All features that should be transformed (in transform order)
        /// </summary>
        public IEnumerable<JsFeature> Features => JsFeatureInfo.All.Select(x => x.Feature).Where(Needed.Contains);

        /// <summary>
        /// True if the target supports ES modules (import / export)
        /// </summary>
        public bool SupportsModules
        {
            get
            {
                if (EsLimit < 2015)
                    return false;
                foreach (var b in Browsers)
                {
                    var min = b.Key switch
                    {
                        "chrome" => 61,
                        "firefox" => 60,
                        "safari" => 11,
                        "edge" => 16,
                        _ => 0,
                    };
                    if (b.Value < min)
                        return false;
                }
                return true;
            }
        }

        public override String ToString()
        {
            var sb = new StringBuilder();
            sb.Append(EsVersion == int.MaxValue ? "esnext" : EsVersion == 5 ? "es5" : ("es" + EsVersion));
            foreach (var b in Browsers.OrderBy(x => x.Key))
                sb.Append(", ").Append(b.Key).Append(' ').Append(b.Value.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static readonly Regex EsRegex = new Regex(@"^(?:es|ecmascript|ecma)\s*(\d+|next|latest)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex BrowserRegex = new Regex(@"^([a-z_ ]+?)\s*(?:>=)?\s*(\d+(?:\.\d+)?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static readonly (double Version, double Chrome)[] Samsung =
        [
            (4, 44), (5, 51), (6.2, 56), (7.2, 59), (8.2, 63), (9.2, 67), (10.1, 71), (11.1, 75), (12, 79), (13, 83), (14, 87), (15, 90),
            (16, 92), (17, 96), (18, 99), (19, 102), (20, 106), (21, 110), (22, 111), (23, 115), (24, 117), (25, 121), (26, 122), (27, 125),
        ];

        static readonly (double Version, double Chrome)[] Node =
        [
            (4, 45), (6, 51), (7, 54), (8, 58), (9, 61), (10, 66), (11, 70), (12, 74), (13, 78), (14, 83), (15, 86), (16, 93),
            (17, 95), (18, 101), (19, 107), (20, 113), (21, 118), (22, 124), (23, 129), (24, 134),
        ];

        static double Lookup((double Version, double Chrome)[] table, double v)
        {
            double r = 0;
            foreach (var x in table)
                if (v >= x.Version)
                    r = x.Chrome;
            return r;
        }

        static void AddBrowser(Dictionary<String, double> b, String name, double v)
        {
            if (b.TryGetValue(name, out var old) && old <= v)
                return;
            b[name] = v;
        }

        /// <summary>
        /// Parse a target, ex: "es5", "es2017", "chrome 69", "chrome 69, safari 12", "es2015, firefox 60".
        /// Supported browsers: chrome (also: android, webview, chromium), edge, firefox (ff), safari (ios), opera, samsung, node and ie (same as es5).
        /// A version can be written as "chrome69", "chrome 69" or "chrome >= 69".
        /// "esnext" or an empty string means no transforms.
        /// </summary>
        /// <param name="text">The target text, items separated by ',' or ';'</param>
        /// <returns>The target</returns>
        /// <exception cref="ArgumentException">Thrown if the target is invalid</exception>
        public static JsTarget Parse(String text)
        {
            text = text?.Trim() ?? "";
            int es = int.MaxValue;
            var browsers = new Dictionary<String, double>(StringComparer.Ordinal);
            foreach (var p in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var s = p.ToLowerInvariant();
                if (s == "esnext" || s == "latest" || s == "defaults")
                    continue;
                var m = EsRegex.Match(s);
                if (m.Success)
                {
                    var vs = m.Groups[1].Value;
                    if (vs == "next" || vs == "latest")
                        continue;
                    var v = int.Parse(vs, CultureInfo.InvariantCulture);
                    if (v == 3 || v == 5)
                        v = 5;
                    else if (v >= 6 && v < 100)
                        v = 2009 + v;
                    if (v != 5 && (v < 2015 || v > 2100))
                        throw new ArgumentException("Invalid EcmaScript version \"" + p + "\"", nameof(text));
                    es = Math.Min(es, v);
                    continue;
                }
                m = BrowserRegex.Match(s);
                if (!m.Success)
                    throw new ArgumentException("Invalid target \"" + p + "\", expected ex: \"es2017\" or \"chrome 69\"", nameof(text));
                var name = m.Groups[1].Value.Trim().Replace(" ", "_");
                var ver = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                switch (name)
                {
                    case "chrome":
                    case "chromium":
                    case "android":
                    case "webview":
                    case "and_chr":
                    case "chrome_android":
                    case "chromeandroid":
                    case "electron_chrome":
                        AddBrowser(browsers, "chrome", ver);
                        break;
                    case "edge":
                        if (ver >= 79)
                            AddBrowser(browsers, "chrome", ver);
                        else
                            AddBrowser(browsers, "edge", ver);
                        break;
                    case "firefox":
                    case "ff":
                    case "and_ff":
                    case "firefox_android":
                        AddBrowser(browsers, "firefox", ver);
                        break;
                    case "safari":
                    case "ios":
                    case "ios_saf":
                    case "ios_safari":
                    case "safari_ios":
                        AddBrowser(browsers, "safari", ver);
                        break;
                    case "opera":
                        //  Chromium based opera (15+) is roughly chrome - 13
                        AddBrowser(browsers, "chrome", ver >= 15 ? ver + 13 : 0);
                        break;
                    case "samsung":
                    case "samsunginternet":
                    case "samsung_internet":
                        AddBrowser(browsers, "chrome", Lookup(Samsung, ver));
                        break;
                    case "node":
                    case "nodejs":
                        AddBrowser(browsers, "chrome", Lookup(Node, ver));
                        break;
                    case "ie":
                    case "explorer":
                    case "ie_mob":
                        es = 5;
                        break;
                    default:
                        throw new ArgumentException("Unknown browser \"" + m.Groups[1].Value + "\" in target \"" + p + "\"", nameof(text));
                }
            }
            return new JsTarget(text, es, browsers);
        }
    }
}
