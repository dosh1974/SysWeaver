using ExCSS;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SysWeaver.Media
{

    /// <summary>
    /// Helpers for svg XElements (all numbers are formatted using the invariant culture)
    /// </summary>
    public static class SvgCanvasTools
    {
        /// <summary>
        /// The culture used for all numbers (the invariant culture)
        /// </summary>
        public static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        /// <summary>
        /// The svg namespace ("http://www.w3.org/2000/svg")
        /// </summary>
        public static readonly XNamespace Namespace = XNamespace.Get("http://www.w3.org/2000/svg");

        /// <summary>
        /// Format a number using the invariant culture
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="coordFormat">The .NET number format, null for the general format</param>
        /// <returns>The formatted number</returns>
        public static String SvgValue(double value, String coordFormat = "0.###")
            => value.ToString(coordFormat, Ci);


        /// <summary>
        /// Set the opacity attribute if the opacity is less than 1 (negative values are set as 0)
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="opacity">The opacity</param>
        /// <returns>The element</returns>
        public static XElement SvgOpacity(this XElement e, double opacity)
        {
            if (opacity >= 1)
                return e;
            if (opacity < 0)
                opacity = 0;
            e.SetAttributeValue("opacity", SvgValue(opacity));
            return e;
        }

        /// <summary>
        /// Set the clip-path attribute (if <paramref name="clipId"/> isn't null or empty)
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="clipId">The clip path reference, ex: "url(#a)" (as returned by <see cref="SvgCanvas.ClipPathDef(string, string)"/>)</param>
        /// <returns>The element</returns>
        public static XElement SvgClipPath(this XElement e, String clipId)
        {
            if (String.IsNullOrEmpty(clipId))
                return e;
            e.SetAttributeValue("clip-path", clipId);
            return e;
        }

        /// <summary>
        /// Set the fill-opacity attribute if the opacity is less than 1 (negative values are set as 0)
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="opacity">The fill opacity</param>
        /// <returns>The element</returns>
        public static XElement SvgFillOpacity(this XElement e, double opacity)
        {
            if (opacity >= 1)
                return e;
            if (opacity < 0)
                opacity = 0;
            e.SetAttributeValue("fill-opacity", SvgValue(opacity));
            return e;
        }


        /// <summary>
        /// Set the filter attribute (if <paramref name="idRef"/> isn't null)
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="idRef">The filter reference, ex: "url(#a)" (as returned by <see cref="SvgCanvas.DropShadowDef"/>)</param>
        /// <returns>The element</returns>
        public static XElement SvgFilter(this XElement e, String idRef)
        {
            if (idRef == null)
                return e;
            e.SetAttributeValue("filter", idRef);
            return e;
        }

        /// <summary>
        /// Set the stroke attributes: nothing is set if the thickness or opacity is zero or less, or if the color is null or empty.
        /// The stroke-width is only set if it isn't 1 and the stroke-opacity only if it's less than 1.
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="color">The stroke color</param>
        /// <param name="thickness">The stroke width</param>
        /// <param name="opacity">The stroke opacity</param>
        /// <returns>The element</returns>
        public static XElement SvgStroke(this XElement e, String color, double thickness = 1, double opacity = 1)
        {
            if (thickness <= 0)
                return e;
            if (opacity <= 0)
                return e;
            if (String.IsNullOrEmpty(color))
                return e;
            if (thickness != 1)
                e.SetAttributeValue("stroke-width", SvgValue(thickness));
            e.SetAttributeValue("stroke", color);
            if (opacity < 1)
                e.SetAttributeValue("stroke-opacity", SvgValue(opacity));
            return e;
        }

    }

    /// <summary>
    /// An svg document (XDocument based) with helpers to add elements, definitions (gradients, filters, clip paths), images and other svg documents.
    /// The view box size is read when loading (and the width / height attributes are removed, so the document scales to its container).
    /// Numbers are formatted using <see cref="DefaultCoordFormat"/> and the invariant culture. Not thread safe.
    /// </summary>
    public sealed class SvgCanvas
    {
        double ToV(String v, double relSize)
        {
            v = v?.Trim();
            if (String.IsNullOrEmpty(v))
                return 0;
            var l = v.Length;
            if (l > 2)
            {
                if ((v[l - 2] == 'p') && (v[l - 1] == 'x'))
                {
                    v = v.Substring(0, l - 2).TrimEnd();
                    l = v.Length;
                }
            }
            if (l > 1)
            {
                --l;
                if (v[l] == '%')
                    return double.Parse(v.Substring(0, l).TrimEnd(), SvgCanvasTools.Ci) * relSize * 0.01;
            }
            return double.Parse(v, SvgCanvasTools.Ci);
        }

        double ToX(String x) => ToV(x, Width);
        double ToY(String y) => ToV(y, Height);


        /// <summary>
        /// Replace all svg text elements with paths (glyph outlines), so that the document can be rendered without the fonts.
        /// The font is found using <see cref="SvgFont.GetOrCreate(string, string, string, string[])"/> (font-family, font-style and font-weight of the style attribute,
        /// the folder of <see cref="Filename"/> is also searched), <see cref="SvgFont.MontserratBlack"/> is used if no font is found.
        /// Supported attributes: x, y (numbers, px or % of the canvas size), text-anchor (start, middle, end), the font-size of the style attribute (default 12),
        /// and the non standard "textLength" (max width), "max-height", "max-scale-up" (default 1) and "vertical-align" (baseline (default), top, middle, bottom).
        /// The other attributes (and the style without the font) are copied to the path element.
        /// </summary>
        /// <param name="maxDecimals">The maximum number of decimals of the path coordinates</param>
        /// <returns>Always true</returns>
        /// <exception cref="FormatException">A position or size attribute is not a valid number</exception>
        public bool RasterizeText(int maxDecimals = 2)
        {
            var parser = new StylesheetParser();
            var tas = TextAttributes;
            var tacs = TextAnchors;
            var vas = VerticalAlign;
            var fn = Filename;
            String[] extraFolders = String.IsNullOrEmpty(fn) ? null : [System.IO.Path.GetDirectoryName(fn)];
            foreach (var t in Svg.Descendants(SvgCanvasTools.Namespace + "text").ToList())
            {
                var value = t.Value;
                var ta = t.Attribute("text-anchor")?.Value ?? "start";
                var x = ToX(t.Attribute("x")?.Value);
                var y = ToY(t.Attribute("y")?.Value);
                var maxWidth = ToX(t.Attribute("textLength")?.Value);
                var maxHeight = ToY(t.Attribute("max-height")?.Value);
                var verticalAlign = t.Attribute("vertical-align")?.Value ?? "baseline";
                var maxScaleUp = ToV(t.Attribute("max-scale-up")?.Value ?? "1", 1.0);
                if (maxScaleUp <= 0)
                    maxScaleUp = 1.0;
                var r = parser.Parse(String.Concat("inline {", t.Attribute("style")?.Value ?? "", '}'));
                var style = (r.StyleRules.FirstOrDefault()).Style;
                var font = style.FontFamily;
                var fontWeight = style.FontWeight;
                var fontStyle = style.FontStyle;
                var fontSize = String.IsNullOrEmpty(style.FontSize) ? 12.0 : ToV(style.FontSize, 12.0);
                var svgFont = SvgFont.GetOrCreate(font, fontStyle, fontWeight, extraFolders) ?? SvgFont.MontserratBlack;
                var paths = SvgPath.GetSvgTextPaths(value, svgFont, fontSize, 10);
                var minMax = SvgPath.GetMinMaxForPaths(paths);
                double scaleX = 1.0;
                double scaleY = 1.0;
                if (maxWidth > 0)
                    scaleX = maxWidth / minMax.Width;
                if (maxHeight > 0)
                    scaleY = maxHeight / minMax.Height;
                double scale = Math.Min(maxScaleUp, Math.Min(scaleX, scaleY));


                x -= minMax.MinX * scale;
                x -= (minMax.Width * scale * (tacs.TryGetValue(ta, out var ws) ? ws : 0.0));

                if (verticalAlign.FastEquals("baseline"))
                {
                    y -= svgFont.TF.Baseline * fontSize * scale;
                }
                else {

                    y -= minMax.MinY * scale;
                    y -= (minMax.Height * scale * (vas.TryGetValue(verticalAlign, out var hs) ? hs : 0.0));
                }
                SvgPath.TransformPaths(paths, scale, scale, x, y, maxDecimals);
                var fontPath = SvgPath.JoinPaths(paths);
                var e = CreateElement("path");
                foreach (var attr in t.Attributes())
                {
                    var n = attr.Name.LocalName;
                    if (tas.Contains(n))
                        continue;
                    if (n.FastEquals("style"))
                    {
                        style.Font = null;
                        var css = style.CssText?.Trim();
                        if (!String.IsNullOrEmpty(css))
                            e.SetAttributeValue(n, css);
                        continue;
                    }
                    e.SetAttributeValue(n, attr.Value);
                }
                e.SetAttributeValue("d", fontPath);
                t.ReplaceWith(e);
            }
            return true;
        }

        static readonly IReadOnlyDictionary<String, double> TextAnchors = new Dictionary<String, double>(StringComparer.Ordinal)
        {
            { "middle", 0.5 },
            { "end", 1.0 },
        }.Freeze();

        static readonly IReadOnlyDictionary<String, double> VerticalAlign = new Dictionary<String, double>(StringComparer.Ordinal)
        {
            { "middle", 0.5 },
            { "bottom", 1.0 },
        }.Freeze();


        static readonly IReadOnlySet<String> TextAttributes = ReadOnlyData.Set(StringComparer.Ordinal,

            "x", "y",
            "text-anchor",
            "textLength",
            "max-height",
            "max-scale-up",
            "vertical-align",
            "lengthAdjust"
            );


        /// <summary>
        /// Create a canvas from svg text, the root must be an svg element in the svg namespace.
        /// The view box is normalized (rounded with <paramref name="defaultCoordFormat"/>), the width and height attributes are removed.
        /// </summary>
        /// <param name="svgText">The svg document</param>
        /// <param name="defaultCoordFormat">The .NET number format used for generated numbers</param>
        /// <returns>The canvas</returns>
        /// <exception cref="System.Xml.XmlException">The text is not valid xml</exception>
        /// <exception cref="InvalidDataException">The root element is not an svg element in the svg namespace (DEBUG builds only)</exception>
        /// <exception cref="FormatException">The view box has less than 4 values (DEBUG builds only)</exception>
        public static SvgCanvas Create(String svgText, String defaultCoordFormat = "0.##")
        {
            using var x = new StringReader(svgText);
            var doc = XDocument.Load(x);
            return new SvgCanvas(doc, defaultCoordFormat);
        }

        /// <summary>
        /// Load a canvas from an svg file, see <see cref="Create"/>.
        /// The folder of the file is used when searching for fonts in <see cref="RasterizeText"/>.
        /// </summary>
        /// <param name="filename">The svg file name</param>
        /// <param name="defaultCoordFormat">The .NET number format used for generated numbers</param>
        /// <returns>The canvas</returns>
        public static SvgCanvas Load(String filename, String defaultCoordFormat = "0.##")
        {
            var doc = XDocument.Load(filename);
            return new SvgCanvas(doc, defaultCoordFormat, filename);
        }

        /// <summary>
        /// Load a canvas from a stream with an svg document, see <see cref="Create"/>
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <param name="defaultCoordFormat">The .NET number format used for generated numbers</param>
        /// <param name="leaveOpen">If false the stream is disposed</param>
        /// <returns>The canvas</returns>
        public static SvgCanvas Load(Stream stream, String defaultCoordFormat = "0.##", bool leaveOpen = true)
        {
            using var x = leaveOpen ? null : stream;
            var doc = XDocument.Load(stream);
            return new SvgCanvas(doc, defaultCoordFormat);
        }

        /// <summary>
        /// Get the numerical value of an attribute (invariant culture, units are not supported)
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="attrName">The attribute name</param>
        /// <param name="def">The value to return if the attribute is missing or not a number</param>
        /// <returns>The value</returns>
        public static double GetAttributeNumber(XElement e, String attrName, double def = 0)
        {
            var v = e.Attribute(attrName);
            if (v == null)
                return def;
            return ParseNumber(v.Value, def);
        }

        /// <summary>
        /// Remove an attribute
        /// </summary>
        /// <param name="e">The element</param>
        /// <param name="attrName">The attribute name</param>
        /// <returns>True if the attribute was found (and removed)</returns>
        public static bool TryRemoveAttribute(XElement e, String attrName)
        {
            var v = e.Attribute(attrName);
            if (v == null)
                return false;
            v.Remove();
            return true;
        }

        /// <summary>
        /// Parse a number (invariant culture, units are not supported)
        /// </summary>
        /// <param name="vv">The text to parse</param>
        /// <param name="def">The value to return if the text is null, empty or not a number</param>
        /// <returns>The value</returns>
        public static double ParseNumber(String vv, double def = 0)
        {
            if (String.IsNullOrEmpty(vv))
                return def;
            if (!double.TryParse(vv, SvgCanvasTools.Ci, out var value))
                return def;
            return value;
        }

        /// <summary>
        /// The file name used to load the canvas (null if not loaded from a file name)
        /// </summary>
        public readonly String Filename;

        static readonly Char[] ViewBoxSeparators = [' ', ',', '\t', '\r', '\n'];

        SvgCanvas(XDocument doc, String defaultCoordFormat = "0.##", String filename = null)
        {
            Doc = doc;
            Filename = filename;
            DefaultCoordFormat = defaultCoordFormat;
            var ns = SvgCanvasTools.Namespace;
            var s = doc.Element(ns.GetName("svg"));
#if DEBUG
            if (s == null)
                throw new InvalidDataException("The root element must be an svg element in the \"" + ns.NamespaceName + "\" namespace!");
#endif//DEBUG
            Svg = s;
            var v = s.Attribute("viewBox");
            double width = 0;
            double height = 0;
            if (v == null)
            {
                width = GetAttributeNumber(s, "width");
                height = GetAttributeNumber(s, "height");
            }
            else
            {
                var t = v.Value.Split(ViewBoxSeparators, StringSplitOptions.RemoveEmptyEntries);
#if DEBUG
                if (t.Length < 4)
                    throw new FormatException("The viewBox \"" + v.Value + "\" must have 4 values!");
#endif//DEBUG
                var x = ParseNumber(t[0]);
                var y = ParseNumber(t[1]);
                OX = x;
                OY = y;
                width = ParseNumber(t[2]);
                height = ParseNumber(t[3]);
                TryRemoveAttribute(s, "viewBox");
            }
            TryRemoveAttribute(s, "width");
            TryRemoveAttribute(s, "height");
            s.SetAttributeValue("viewBox", String.Join(' ', Value(OX), Value(OY), Value(width), Value(height)));
            Width = width;
            Height = height;
        }


        /// <summary>
        /// Call a function for all descendants of the svg element that have a specific attribute (depth first, document order)
        /// </summary>
        /// <param name="attributeName">The attribute name (local name, no namespace)</param>
        /// <param name="onAttr">Called with the element and the attribute, return false to stop</param>
        /// <returns>False if stopped by <paramref name="onAttr"/>, else true</returns>
        public bool OnAttributes(String attributeName, Func<XElement, XAttribute, bool> onAttr)
            => OnAttributes(Svg, attributeName, onAttr);

        /// <summary>
        /// Call a function for all descendants of an element (not the element itself) that have a specific attribute (depth first, document order)
        /// </summary>
        /// <param name="e">The element to search below</param>
        /// <param name="attributeName">The attribute name (local name, no namespace)</param>
        /// <param name="onAttr">Called with the element and the attribute, return false to stop</param>
        /// <returns>False if stopped by <paramref name="onAttr"/>, else true</returns>
        public static bool OnAttributes(XElement e, String attributeName, Func<XElement, XAttribute, bool> onAttr)
        {
            foreach (var x in e.Elements())
            {
                var a = x.Attribute(attributeName);
                if (a != null)
                    if (!onAttr(x, a))
                        return false;
                if (!OnAttributes(x, attributeName, onAttr))
                    return false;
            }
            return true;
        }


        static readonly IReadOnlyDictionary<String, Func<SvgCanvas, String[], String>> ColorParsers = new Dictionary<String, Func<SvgCanvas, String[], String>>(StringComparer.Ordinal)
        {
            { "lineargradient", (svg, p) =>
                {
                    var c = (p.Length - 1) >> 1;
                    var stops = new SvgGradStop[c];
                    for (int i = 0; i < c; ++ i)
                        stops[i] = new SvgGradStop(100.0 * ParseNumber(p[i + i + 1]), p[i + i + 2]);
                    return svg.LinearGradientDef(stops);
                } 
            },
            { "lineargradientdir", (svg, p) =>
                {
                    var c = (p.Length - 1) >> 1;
                    var stops = new SvgGradStop[c - 2];
                    for (int i = 2; i < c; ++ i)
                        stops[i - 2] = new SvgGradStop(100.0 * ParseNumber(p[i + i + 1]), p[i + i + 2]);
                    return svg.LinearGradientDef(ParseNumber(p[1]), ParseNumber(p[2]), ParseNumber(p[3]), ParseNumber(p[4]), stops);
                }
            },
        };

        /// <summary>
        /// The lengths of the <see cref="ColorParsers"/> keys
        /// </summary>
        static readonly HashSet<int> ColorParserKeyLengths = ColorParsers.Keys.Select(x => x.Length).ToHashSet();

        /// <summary>
        /// Resolve a color specification (space separated):
        /// "lineargradient pos0 color0 pos1 color1 ..." (positions in [0, 1]) and "lineargradientdir x1 y1 x2 y2 pos0 color0 ..." add a gradient definition
        /// and returns its reference ("url(#id)"), any other value is returned as is.
        /// </summary>
        /// <param name="color">The color specification</param>
        /// <returns>The color to use, null if <paramref name="color"/> is null or empty</returns>
        public String Color(String color)
        {
            if (String.IsNullOrEmpty(color))
                return null;
            //  FastToLower keeps the length, so the first part can only be a parser key if it has the length of a key (no split for plain colors)
            var sp = color.IndexOf(' ');
            if (!ColorParserKeyLengths.Contains(sp < 0 ? color.Length : sp))
                return color;
            var cols = color.Split(' ');
            if (ColorParsers.TryGetValue(cols[0].FastToLower(), out var fn))
                return fn(this, cols);
            return color;
        }

        /// <summary>
        /// Create an empty canvas (an svg element with the view box "0 0 width height")
        /// </summary>
        /// <param name="width">The view box width</param>
        /// <param name="height">The view box height</param>
        /// <param name="defaultCoordFormat">The .NET number format used for generated numbers</param>
        public SvgCanvas(double width, double height, String defaultCoordFormat = "0.##")
        {
            DefaultCoordFormat = defaultCoordFormat;
            Width = width;
            Height = height;
            var d = new XDocument();
            var s = CreateElement("svg");
            s.SetAttributeValue("version", "1.1");
            d.Add(s);
            Doc = d;
            Svg = s;
            s.SetAttributeValue("viewBox", String.Concat("0 0 ", Value(width), ' ', Value(height)));
        }

        /// <summary>
        /// Get the svg document as text (indented, with an xml declaration that says utf-8, no byte order mark)
        /// </summary>
        /// <returns>The svg document</returns>
        public String ToSvgString()
        {
            using (var ms = new MemoryStream())
            {
                Doc.Save(ms);
                return Encoding.UTF8.GetStringWithoutBom(new ReadOnlySpan<Byte>(ms.GetBuffer(), 0, (int)ms.Length));
            }
        }


        /// <summary>
        /// The .NET number format used for generated numbers (see <see cref="Value"/>)
        /// </summary>
        public readonly String DefaultCoordFormat;
        /// <summary>
        /// The x origin of the view box (0 for new canvases)
        /// </summary>
        public readonly double OX;
        /// <summary>
        /// The y origin of the view box (0 for new canvases)
        /// </summary>
        public readonly double OY;


        /// <summary>
        /// The view box width (or the width attribute if there is no view box)
        /// </summary>
        public readonly double Width;
        /// <summary>
        /// The view box height (or the height attribute if there is no view box)
        /// </summary>
        public readonly double Height;

        /// <summary>
        /// Format a number (invariant culture)
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="coordFormat">The .NET number format, null to use <see cref="DefaultCoordFormat"/></param>
        /// <returns>The formatted number</returns>
        public String Value(double value, String coordFormat = null)
            => SvgCanvasTools.SvgValue(value, coordFormat ?? DefaultCoordFormat);


        /// <summary>
        /// Set the filter attribute of an element to "url(#filterId)"
        /// </summary>
        /// <param name="el">The element</param>
        /// <param name="filterId">The filter id (without '#')</param>
        public void AddFilter(XElement el, String filterId)
        {
            el.SetAttributeValue("filter", String.Concat("url(#", filterId, ')'));
        }

        /// <summary>
        /// Insert a drop shadow filter (feDropShadow) first in the svg element (not in the defs).
        /// Attributes with the svg default value are left out (black color, opacity 1, dx / dy 2, std deviation 2).
        /// </summary>
        /// <param name="id">The id of the filter</param>
        /// <param name="color">The shadow color (shortened using <see cref="HtmlColors.GetShortest"/>)</param>
        /// <param name="stdDeviation">The blur standard deviation</param>
        /// <param name="opacity">The shadow opacity</param>
        /// <param name="dx">The x offset</param>
        /// <param name="dy">The y offset</param>
        /// <returns>The filter element</returns>
        public XElement CreateDropShadowClass(String id, String color, double stdDeviation = 2, double opacity = 1, double dx = 2, double dy = 2)
        {
            color = HtmlColors.GetShortest(color);
            var b = CreateElement("filter");
            Svg.AddFirst(b);
            b.SetAttributeValue("id", id);
            var d = CreateElement("feDropShadow");
            b.Add(d);
            if (!color.FastEquals("#000"))
                d.SetAttributeValue("flood-color", color);
            if (stdDeviation != 2)
                d.SetAttributeValue("stdDeviation", Value(stdDeviation));
            if (opacity != 1)
                d.SetAttributeValue("flood-opacity", Value(opacity));
            if (dx != 2)
                d.SetAttributeValue("dx", Value(dx));
            if (dy != 2)
                d.SetAttributeValue("dy", Value(dy));
            return b;
        }

        /// <summary>
        /// Create an element in the svg namespace (not added to the document)
        /// </summary>
        /// <param name="localName">The element name, ex: "path"</param>
        /// <returns>The new element</returns>
        public XElement CreateElement(String localName)
            => new XElement(SvgCanvasTools.Namespace + localName);

        /// <summary>
        /// Set (or remove if null) an attribute (values are converted using the XML rules, i.e. culture invariant)
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="localName">The attribute name</param>
        /// <param name="value">The value, null to remove the attribute</param>
        public void SetAttribute(XElement element, String localName, Object value)
            => element.SetAttributeValue(localName, value);


        XElement Defs;

        /// <summary>
        /// Add an element to the defs element created by this canvas (a defs element is inserted first in the svg element on the first call)
        /// </summary>
        /// <param name="e">The element to add</param>
        /// <returns>The added element</returns>
        public XElement AddDef(XElement e)
        {
            var defs = Defs;
            if (defs == null)
            {
                defs = CreateElement("defs");
                Defs = defs;
                Svg.AddFirst(defs);
            }
            defs.Add(e);
            return e;
        }

        /// <summary>
        /// Add an element last in the svg element
        /// </summary>
        /// <param name="e">The element to add</param>
        /// <returns>The added element</returns>
        public XElement AddElement(XElement e)
        {
            Svg.Add(e);
            return e;
        }


        /// <summary>
        /// Set the fill and stroke of an element and add it last in the svg element
        /// </summary>
        /// <param name="e">The element to add</param>
        /// <param name="fill">The fill, null to not set it</param>
        /// <param name="stroke">The stroke, null to not set it (stroke-width is only set if there is a stroke)</param>
        /// <param name="strokeWidth">The stroke width</param>
        /// <returns>The added element</returns>
        public XElement AddElement(XElement e, String fill, String stroke, double strokeWidth)
        {
            if (fill != null)
                e.SetAttributeValue("fill", fill);
            if (stroke != null)
            {
                e.SetAttributeValue("stroke", stroke);
                e.SetAttributeValue("stroke-width", Value(strokeWidth));
            }
            Svg.Add(e);
            return e;
        }

        /// <summary>
        /// Add a rectangle (zero values are not written since that is the svg default)
        /// </summary>
        /// <param name="x">The left position</param>
        /// <param name="y">The top position</param>
        /// <param name="width">The width</param>
        /// <param name="height">The height</param>
        /// <param name="fill">The fill, null to not set it</param>
        /// <param name="stroke">The stroke, null to not set it</param>
        /// <param name="strokeWidth">The stroke width</param>
        /// <returns>The added element</returns>
        public XElement Rect(double x, double y, double width, double height, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("rect");
            if (x != 0)
                e.SetAttributeValue("x", Value(x));
            if (y != 0)
                e.SetAttributeValue("y", Value(y));
            if (width != 0)
                e.SetAttributeValue("width", Value(width));
            if (height != 0)
                e.SetAttributeValue("height", Value(height));
            return AddElement(e, fill, stroke, strokeWidth);
        }

        /// <summary>
        /// Add a circle (zero values are not written since that is the svg default)
        /// </summary>
        /// <param name="x">The center x</param>
        /// <param name="y">The center y</param>
        /// <param name="rad">The radius</param>
        /// <param name="fill">The fill, null to not set it</param>
        /// <param name="stroke">The stroke, null to not set it</param>
        /// <param name="strokeWidth">The stroke width</param>
        /// <returns>The added element</returns>
        public XElement Circle(double x, double y, double rad, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("circle");
            if (x != 0)
                e.SetAttributeValue("cx", Value(x));
            if (y != 0)
                e.SetAttributeValue("cy", Value(y));
            if (rad != 0)
                e.SetAttributeValue("r", Value(rad));
            return AddElement(e, fill, stroke, strokeWidth);
        }


        /// <summary>
        /// Generate a new short id that is unique for this canvas instance ("a", "b", .. "z", "ab", "bb", ..), it starts with a lower case letter.
        /// Ids already in a loaded document are not checked.
        /// </summary>
        /// <returns>The id</returns>
        public String GenId()
        {
            var c = CurrentId;
            CurrentId = c + 1;
            Span<Char> temp = stackalloc Char[16];
            int dest = 0;
            var ch = IdFirstChars;
            do
            {
                var l = ch.Length;
                var p = c % l;
                c /= l;
                temp[dest] = ch[p];
                ++dest;
                ch = IdSecondChars;
            } while (c != 0);
            return new String(temp[..dest]);
        }

        int CurrentId;

        const String IdFirstChars = "abcdefghijklmnopqrstuvwxyz";
        const String IdSecondChars = "abcdefghijklmnopqrstuvwxyz0123456789";


        /// <summary>
        /// Add a drop shadow filter definition (feDropShadow) to the defs
        /// </summary>
        /// <param name="dx">The x offset</param>
        /// <param name="dy">The y offset</param>
        /// <param name="stdDeviation">The blur standard deviation</param>
        /// <param name="color">The shadow color, null for black</param>
        /// <param name="opacity">The shadow opacity, zero or less adds nothing</param>
        /// <param name="id">The id of the filter, null to generate one (<see cref="GenId"/>)</param>
        /// <returns>The filter reference "url(#id)", or an empty string if the opacity is zero or less</returns>
        public String DropShadowDef(double dx = 2, double dy = 2, double stdDeviation = 2, String color = "#000", double opacity = 1, String id = null)
        {
            id = id ?? GenId();
            if (opacity <= 0)
                return "";
            var e = CreateElement("filter");
            e.SetAttributeValue("id", id);
            var ds = CreateElement("feDropShadow");
            e.Add(ds);
            if (dx != 2)
                ds.SetAttributeValue("dx", Value(dx));
            if (dy != 2)
                ds.SetAttributeValue("dy", Value(dy));
            if (stdDeviation != 2)
                ds.SetAttributeValue("stdDeviation", Value(stdDeviation));
            ds.SetAttributeValue("flood-color", color ?? "#000");
            if (opacity < 1)
                ds.SetAttributeValue("flood-opacity", Value(opacity));
            AddDef(e);
            return String.Concat("url(#", id, ')');
        }


        /// <summary>
        /// Add a clip path definition with a single path to the defs
        /// </summary>
        /// <param name="path">The path data, null adds nothing</param>
        /// <param name="id">The id of the clip path, null to generate one (<see cref="GenId"/>)</param>
        /// <returns>The clip path reference "url(#id)", null if <paramref name="path"/> is null</returns>
        public String ClipPathDef(String path, String id = null)
        {
            if (path == null)
                return null;
            var p = CreateElement("path");
            p.SetAttributeValue("d", path);
            return ClipPathDef(id, p);
        }

        /// <summary>
        /// Add a clip path definition with some elements to the defs
        /// </summary>
        /// <param name="id">The id of the clip path, null to generate one (<see cref="GenId"/>)</param>
        /// <param name="paths">The clip path elements, empty adds nothing</param>
        /// <returns>The clip path reference "url(#id)", null if there are no elements</returns>
        public String ClipPathDef(String id, params XElement[] paths)
        {
            if (paths.Length <= 0)
                return null;
            id = id ?? GenId();
            var e = CreateElement("clipPath");
            e.SetAttributeValue("id", id);
            foreach (var x in paths)
                e.Add(x);
            AddDef(e);
            return String.Concat("url(#", id, ')');
        }



        /// <summary>
        /// Add a (left to right) linear gradient definition with a generated id to the defs
        /// </summary>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The gradient reference "url(#id)"</returns>
        public String LinearGradientDef(params SvgGradStop[] stops)
            => LinearGradientDef(null, stops);

        /// <summary>
        /// Add a (left to right) linear gradient definition to the defs
        /// </summary>
        /// <param name="id">The id of the gradient, null to generate one (<see cref="GenId"/>)</param>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The gradient reference "url(#id)"</returns>
        public String LinearGradientDef(String id, params SvgGradStop[] stops)
        {
            id = id ?? GenId();
            var e = CreateElement("linearGradient");
            e.SetAttributeValue("id", id);
            foreach (var x in stops)
            {
                var s = CreateElement("stop");
                s.SetAttributeValue("offset", Value(x.Pos) + "%");
                s.SetAttributeValue("stop-color", x.Color);
                e.Add(s);
            }
            AddDef(e);
            return String.Concat("url(#", id, ')');
        }

        /// <summary>
        /// Add a linear gradient definition with a direction and a generated id to the defs
        /// </summary>
        /// <param name="x1">The start x (relative to the bounding box)</param>
        /// <param name="y1">The start y (relative to the bounding box)</param>
        /// <param name="x2">The end x (relative to the bounding box)</param>
        /// <param name="y2">The end y (relative to the bounding box)</param>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The gradient reference "url(#id)"</returns>
        public String LinearGradientDef(double x1, double y1, double x2, double y2, params SvgGradStop[] stops)
            => LinearGradientDef(null, x1, y1, x2, y2, stops);

        /// <summary>
        /// Add a linear gradient definition with a direction to the defs
        /// </summary>
        /// <param name="id">The id of the gradient, null to generate one (<see cref="GenId"/>)</param>
        /// <param name="x1">The start x (relative to the bounding box)</param>
        /// <param name="y1">The start y (relative to the bounding box)</param>
        /// <param name="x2">The end x (relative to the bounding box)</param>
        /// <param name="y2">The end y (relative to the bounding box)</param>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The gradient reference "url(#id)"</returns>
        public String LinearGradientDef(String id, double x1, double y1, double x2, double y2, params SvgGradStop[] stops)
        {
            id = id ?? GenId();
            var e = CreateElement("linearGradient");
            e.SetAttributeValue("id", id);
            e.SetAttributeValue("x1", Value(x1));
            e.SetAttributeValue("y1", Value(y1));
            e.SetAttributeValue("x2", Value(x2));
            e.SetAttributeValue("y2", Value(y2));
            foreach (var x in stops)
            {
                var s = CreateElement("stop");
                s.SetAttributeValue("offset", Value(x.Pos) + "%");
                s.SetAttributeValue("stop-color", x.Color);
                e.Add(s);
            }
            AddDef(e);
            return String.Concat("url(#", id, ')');
        }

        /// <summary>
        /// Add a path
        /// </summary>
        /// <param name="d">The path data</param>
        /// <param name="fill">The fill, null to not set it</param>
        /// <param name="stroke">The stroke, null to not set it</param>
        /// <param name="strokeWidth">The stroke width</param>
        /// <returns>The added element</returns>
        public XElement Path(String d, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("path");
            e.SetAttributeValue("d", d);
            return AddElement(e, fill, stroke, strokeWidth);
        }

        /// <summary>
        /// Add a group (g) element
        /// </summary>
        /// <param name="fill">The fill, null to not set it</param>
        /// <param name="stroke">The stroke, null to not set it</param>
        /// <param name="strokeWidth">The stroke width</param>
        /// <returns>The added element</returns>
        public XElement Graph(String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("g");
            return AddElement(e, fill, stroke, strokeWidth);
        }

        /// <summary>
        /// Add an image element with the data embedded as a base64 data uri
        /// </summary>
        /// <param name="data">The image file data</param>
        /// <param name="mime">The mime type of the data</param>
        /// <param name="x">The left position (not written if 0)</param>
        /// <param name="y">The top position (not written if 0)</param>
        /// <param name="width">The width (not written if zero or less)</param>
        /// <param name="height">The height (not written if zero or less)</param>
        /// <returns>The added element</returns>
        public XElement Image(ReadOnlySpan<Byte> data, String mime, double x, double y, double width, double height)
        {
            var e = CreateElement("image");
            if (x != 0)
                e.SetAttributeValue("x", x);
            if (y != 0)
                e.SetAttributeValue("y", y);
            if (width > 0)
                e.SetAttributeValue("width", width);
            if (height > 0)
                e.SetAttributeValue("height", height);
            e.SetAttributeValue("href", String.Concat("data:", mime, ";base64,", Convert.ToBase64String(data)));
            return AddElement(e, null, null, 0);
        }

        static bool FileExists(String url)
        {
            try
            {
                return File.Exists(url);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Get a transform that uniformly scales content with the bounds <paramref name="s"/> to fit inside a box (and optionally rotates it around the box center).
        /// The transform is "translate(box center) rotate(angle) scale(scale) translate(-content alignment point)", parts that are identity are left out.
        /// </summary>
        /// <param name="s">The bounds of the content</param>
        /// <param name="x">The left position of the box</param>
        /// <param name="y">The top position of the box</param>
        /// <param name="width">The width of the box</param>
        /// <param name="height">The height of the box</param>
        /// <param name="angle">Rotation in degrees (around the box center, applied after the fit, so a rotated content may not fit)</param>
        /// <param name="ax">The horizontal alignment in the unused space: 1 = left, 0 = center, -1 = right</param>
        /// <param name="ay">The vertical alignment in the unused space: 1 = top, 0 = center, -1 = bottom</param>
        /// <param name="coordFormat">The .NET number format, null to use <see cref="DefaultCoordFormat"/></param>
        /// <returns>The transform, null if no transform is needed</returns>
        public String GetTransformToFit(SvgMinMaxState s, double x, double y, double width, double height, double angle = 0, double ax = 0, double ay = 0, String coordFormat = null)
        {
            double scaleX = width / s.Width;
            double scaleY = height / s.Height;
            var scale = Math.Min(scaleX, scaleY);
            var ex = width / scale - s.Width;
            var ey = height / scale - s.Height;
            var dx = -s.MinX - (s.Width + ex * ax) * 0.5;
            var dy = -s.MinY - (s.Height + ey * ay) * 0.5;
            var cx = x + width * 0.5;
            var cy = y + height * 0.5;
            StringBuilder tr = new StringBuilder();
            if ((cx != 0) || (cy != 0))
                tr.Append("translate(").Append(Value(cx, coordFormat)).Append(' ').Append(Value(cy, coordFormat)).Append(") ");
            if (angle != 0)
                tr.Append("rotate(").Append(Value(angle, coordFormat)).Append(") ");
            if (scale != 1)
                tr.Append("scale(").Append(Value(scale, coordFormat)).Append(") ");
            if ((dx != 0) || (dy != 0))
                tr.Append("translate(").Append(Value(dx, coordFormat)).Append(' ').Append(Value(dy, coordFormat)).Append(") ");
            var l = tr.Length;
            if (l <= 1)
                return null;
            tr.Length = l - 1;
            return tr.ToString();
        }


        static readonly Char[] FindCssNameStart = ".}".ToCharArray();
        static readonly Char[] CssSelectorSeparators = " \t\r\n\f,>+~".ToCharArray();



        static readonly IReadOnlySet<String> LeafPrimitives = ReadOnlyData.Set(StringComparer.Ordinal,
            "circle",
            "ellipse",
            "image",
            "line",
            "path",
            "polygon",
            "polyline",
            "rect",
            "text",
            "textpath",
            "use"
            );

        static double ProcessAttr(XElement e, String name)
        {
            var a = e.Attribute(name);
            if (a == null)
                return 1.0;
            var val = Double.Parse(a.Value, CultureInfo.InvariantCulture);
            a.Remove();
            return val;
        }

        void InternalPushOpacityToLeafs(XElement e, double opacity = 1, double fillOpacity = 1, double strokeOpacity = 1) 
        {
            opacity *= ProcessAttr(e, "opacity");
            fillOpacity *= ProcessAttr(e, "fill-opacity");
            strokeOpacity *= ProcessAttr(e, "stroke-opacity");
            var en = e.Name.LocalName.FastToLower();
            if (LeafPrimitives.Contains(en))
            {
                /*if (en.FastEquals("image"))
                {
                    var g = CreateElement("g");
                    e.AddBeforeSelf(g);
                    e.Remove();
                    g.Add(e);
                    e = g;
                }*/
                if (opacity != 1)
                    e.SetAttributeValue("opacity", opacity);
                if (fillOpacity != 1)
                    e.SetAttributeValue("fill-opacity", fillOpacity);
                if (strokeOpacity != 1)
                    e.SetAttributeValue("stroke-opacity", strokeOpacity);
                return;
            }
            foreach (var x in e.Elements().ToList())
                InternalPushOpacityToLeafs(x, opacity, fillOpacity, strokeOpacity);
        }

        static double ReadAttr(out XAttribute a, XElement e, String name)
        {
            a = e.Attribute(name);
            if (a == null)
                return 1.0;
            var val = Double.Parse(a.Value, CultureInfo.InvariantCulture);
            return val;
        }

        void InternalApplyOpacityToImages(Func<String, double, String> func, XElement e, double opacity = 1)
        {
            opacity *= ReadAttr(out var attr, e, "opacity");
            var en = e.Name.LocalName.FastToLower();
            if (LeafPrimitives.Contains(en))
            {
                if (opacity >= 1)
                    return;
                if (!en.FastEquals("image"))
                    return;
                if (opacity <= 0)
                {
                    e.Remove();
                    return;
                }
                var u = e.Attribute("href");
                if (u == null)
                    return;
                var dataUri = u.Value;
                if (!dataUri.FastStartsWith("data:"))
                    return;
                var newUri = func(dataUri, opacity);
                if (newUri == null)
                    return;
                u.Value = newUri;
                attr?.Remove();
                return;
            }
            foreach (var x in e.Elements().ToList())
                InternalApplyOpacityToImages(func, x, opacity);
        }

        /// <summary>
        /// Remove the opacity, fill-opacity and stroke-opacity attributes of all container elements and multiply them into the leaf elements
        /// (shapes, text, image, use). Note that the result differs from group opacity when leaf elements overlap.
        /// </summary>
        /// <exception cref="FormatException">An opacity attribute is not a number (percentages are not supported)</exception>
        public void PushOpacityToLeafs() =>
            InternalPushOpacityToLeafs(Svg);

        /// <summary>
        /// Bake the opacity into image elements with a data uri (href, without a namespace): <paramref name="func"/> creates a new data uri.
        /// The effective opacity is the product of the opacity of the image and its ancestors, but only the image opacity attribute is removed,
        /// so call <see cref="PushOpacityToLeafs"/> first. Images with an effective opacity of zero or less are removed.
        /// </summary>
        /// <param name="func">Called with the data uri and the effective opacity (less than 1), returns the new data uri or null to keep the image as is</param>
        /// <exception cref="FormatException">An opacity attribute is not a number (percentages are not supported)</exception>
        public void ApplyOpacityToImages(Func<String, double, String> func) =>
            InternalApplyOpacityToImages(func, Svg);




        /// <summary>
        /// Convert simple css class rules in style elements to attributes on the elements with that class, and remove all style elements.
        /// Only the last class / element name of each selector is used (ex: ".a,.b" only applies to "b", ".a .b" to "b").
        /// White space around the selectors, braces, ':' and ';' is removed, white space inside values is kept (ex: "stroke-dasharray: 1 2", "font-family: Open Sans").
        /// Declarations without a ':' are ignored. If a selector is defined more than once (also in different style elements), the declarations are merged,
        /// later values of the same property override earlier ones (like css). Unknown classes are kept in the class attribute.
        /// Classes are applied in the order of the class attribute.
        /// </summary>
        public void StyleToInline()
        {
        //  Get all style elements
            var styleElements = Svg.DescendantNodes().Where(x => (x as XElement)?.Name?.LocalName?.FastEquals("style") ?? false).Select(x => (x as XElement)).ToList();
        //  Parse all css and created class setters
            var styles = String.Concat(styleElements.Select(x => x.Value));
            if (String.IsNullOrWhiteSpace(styles))
                return;
            var t = styles.Split('{');
            Dictionary<String, List<KeyValuePair<String, String>>> styleActions = new Dictionary<string, List<KeyValuePair<String, String>>>(StringComparer.Ordinal);
            var tl = t.Length;
            for (int i = 1; i < tl; ++ i)
            {
                var name = t[i - 1];
                name = name.Substring(name.LastIndexOfAny(FindCssNameStart) + 1).Trim();
                name = name.Substring(name.LastIndexOfAny(CssSelectorSeparators) + 1).FastToLower();
                var css = t[i];
                var csse = css.LastIndexOf('}');
                if (csse >= 0)
                    css = css.Substring(0, csse);
                var kv = css.Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (!styleActions.TryGetValue(name, out var actions))
                {
                    actions = new List<KeyValuePair<String, String>>(kv.Length);
                    styleActions.Add(name, actions);
                }
                foreach (var x in kv)
                {
                    var ci = x.IndexOf(':');
                    if (ci < 0)
                        continue;
                    var key = x.Substring(0, ci).Trim();
                    if (key.Length <= 0)
                        continue;
                    var v = x.Substring(ci + 1).Trim();
                    //  A later definition of the same property overrides (like css)
                    var al = actions.Count;
                    int ai = 0;
                    for (; ai < al; ++ai)
                    {
                        if (String.Equals(actions[ai].Key, key, StringComparison.Ordinal))
                            break;
                    }
                    var p = new KeyValuePair<String, String>(key, v);
                    if (ai < al)
                        actions[ai] = p;
                    else
                        actions.Add(p);
                }
            }
            //  Delete all style elements
            foreach (var x in styleElements)
                x.Remove();
            //  Parse all nodes
            foreach(var n in Svg.DescendantNodes())
            {
                var e = n as XElement;
                if (e == null)
                    continue;
                var a = e.Attribute("class");
                if (a == null)
                    continue;
                List<String> clLeft = new List<string>();
                foreach (var cl in a.Value.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (styleActions.TryGetValue(cl.FastToLower(), out var setters))
                    {
                        foreach (var x in setters)
                            e.SetAttributeValue(x.Key, x.Value);
                    }else
                    {
                        clLeft.Add(cl);
                    }
                }
                if (clLeft.Count > 0)
                {
                    a.Value = String.Join(' ', clLeft);
                }else
                {
                    a.Remove();
                }
            }
        }


        static readonly String[] GravImageTexts = ["Min", "Mid", "Max"];

        /// <summary>
        /// Add an image:
        /// if <paramref name="url"/> starts with '&lt;' it's treated as an svg document and embedded using <see cref="EmbeddSvg(string, double, double, double, double, bool, SvgCanvasSizing, SvgCanvasGravity, SvgCanvasGravity)"/>,
        /// if it's an existing local file (not containing "://" and not a data uri) the file is read and embedded as a data uri (the sizing and gravity are not applied),
        /// else an image element referencing the url is added (with preserveAspectRatio set from the sizing and gravity).
        /// Note: local files are read, so don't pass untrusted urls.
        /// </summary>
        /// <param name="url">An svg document, a local file name, a data uri or an url</param>
        /// <param name="x">The left position</param>
        /// <param name="y">The top position</param>
        /// <param name="width">The width</param>
        /// <param name="height">The height</param>
        /// <param name="moveDefsEtc">For embedded svg documents: move the defs, filters and styles to the top level</param>
        /// <param name="size">How to size the image in the box</param>
        /// <param name="gravityHorizontal">The horizontal alignment in the box</param>
        /// <param name="gravityVertical">The vertical alignment in the box</param>
        /// <returns>The added element</returns>
        public XElement Image(String url, double x, double y, double width, double height, bool moveDefsEtc = true, SvgCanvasSizing size = SvgCanvasSizing.Fit, SvgCanvasGravity gravityHorizontal = SvgCanvasGravity.Center, SvgCanvasGravity gravityVertical = SvgCanvasGravity.Center)
        {
            if (!String.IsNullOrEmpty(url))
            {
                if (url[0] == '<')
                    return EmbeddSvg(url, x, y, width, height, moveDefsEtc, size, gravityHorizontal, gravityVertical);
                if ((url.FastIndexOf("://") < 0) && (!url.FastStartsWith("data:")) && FileExists(url))
                {
                    var mime = MimeTypeMap.GetMimeType(url.Substring(url.LastIndexOf('.') + 1));
                    var data = File.ReadAllBytes(url);
                    return Image(data.AsSpan(), mime.Item1, x, y, width, height);
                }
            }
            var e = CreateElement("image");
            if (x != 0)
                e.SetAttributeValue("x", x);
            if (y != 0)
                e.SetAttributeValue("y", y);
            if (width > 0)
                e.SetAttributeValue("width", width);
            if (height > 0)
                e.SetAttributeValue("height", height);
            var g = GravImageTexts;
            switch (size)
            {
                case SvgCanvasSizing.Stretch:
                    e.SetAttributeValue("preserveAspectRatio", "none");
                    break;
                case SvgCanvasSizing.Fit:
                    if ((gravityHorizontal != SvgCanvasGravity.Center) || (gravityVertical != SvgCanvasGravity.Center))
                        e.SetAttributeValue("preserveAspectRatio", String.Concat('x', g[(int)gravityHorizontal], 'Y', g[(int)gravityVertical], " meet"));
                    break;
                case SvgCanvasSizing.Fill:
                    e.SetAttributeValue("preserveAspectRatio", String.Concat('x', g[(int)gravityHorizontal], 'Y', g[(int)gravityVertical], " slice"));
                    break;
            }
            if (!String.IsNullOrEmpty(url))
                e.SetAttributeValue("href", url);
            return AddElement(e, null, null, 0);
        }

        static readonly IReadOnlySet<String> IgnoreAttributes = ReadOnlyData.Set(StringComparer.Ordinal,
            "xmlns", "version", "viewBox", "xlink"
            );

        static readonly IReadOnlySet<String> MoveElements = ReadOnlyData.Set(StringComparer.Ordinal,
            "filter", "style"
        );

        /// <summary>
        /// Embed another svg document, see <see cref="EmbeddSvg(SvgCanvas, double, double, double, double, bool, SvgCanvasSizing, SvgCanvasGravity, SvgCanvasGravity)"/>
        /// </summary>
        /// <param name="svg">The svg document text</param>
        /// <param name="x">The left position of the box</param>
        /// <param name="y">The top position of the box</param>
        /// <param name="width">The width of the box</param>
        /// <param name="height">The height of the box</param>
        /// <param name="moveDefsEtc">Move the defs, filters and styles of the other document to the top level of this document</param>
        /// <param name="size">How to size the document in the box</param>
        /// <param name="gravityHorizontal">The horizontal alignment in the box</param>
        /// <param name="gravityVertical">The vertical alignment in the box</param>
        /// <returns>The added group element</returns>
        public XElement EmbeddSvg(String svg, double x, double y, double width, double height, bool moveDefsEtc = true, SvgCanvasSizing size = SvgCanvasSizing.Fit, SvgCanvasGravity gravityHorizontal = SvgCanvasGravity.Center, SvgCanvasGravity gravityVertical = SvgCanvasGravity.Center)
            => EmbeddSvg(SvgCanvas.Create(svg), x, y, width, height, moveDefsEtc, size, gravityHorizontal, gravityVertical);

        /// <summary>
        /// Embed another svg document scaled into a box: a group with the transform "translate(x,y) scale(sx,sy)" containing the content of the other svg element
        /// (and its attributes except xmlns, version, viewBox and xlink).
        /// The view box origin of the other document is taken into account (the view box is mapped to the box).
        /// When <paramref name="moveDefsEtc"/> is true, the other document is modified (its defs, filter and style elements are moved).
        /// </summary>
        /// <param name="other">The document to embed</param>
        /// <param name="x">The left position of the box</param>
        /// <param name="y">The top position of the box</param>
        /// <param name="width">The width of the box</param>
        /// <param name="height">The height of the box</param>
        /// <param name="moveDefsEtc">Move the defs, filters and styles of the other document to the top level of this document</param>
        /// <param name="size">How to size the document in the box</param>
        /// <param name="gravityHorizontal">The horizontal alignment in the box (Fit and Fill only)</param>
        /// <param name="gravityVertical">The vertical alignment in the box (Fit and Fill only)</param>
        /// <returns>The added group element</returns>
        public XElement EmbeddSvg(SvgCanvas other, double x, double y, double width, double height, bool moveDefsEtc = true, SvgCanvasSizing size = SvgCanvasSizing.Fit, SvgCanvasGravity gravityHorizontal = SvgCanvasGravity.Center, SvgCanvasGravity gravityVertical = SvgCanvasGravity.Center)
        {
            var scaleX = width / other.Width;
            var scaleY = height / other.Height;
            switch (size)
            {
                case SvgCanvasSizing.Fit:
                    if (scaleX < scaleY)
                    {
                        scaleY = scaleX;
                        y += (height - other.Height * scaleY) * ((int)gravityVertical * 0.5);
                    }
                    else
                    {
                        scaleX = scaleY;
                        x += (width - other.Width * scaleX) * ((int)gravityHorizontal * 0.5);
                    }
                    break;
                case SvgCanvasSizing.Fill:
                    if (scaleX > scaleY)
                    {
                        scaleY = scaleX;
                        y += (height - other.Height * scaleY) * ((int)gravityVertical * 0.5);
                    }
                    else
                    {
                        scaleX = scaleY;
                        x += (width - other.Width * scaleX) * ((int)gravityHorizontal * 0.5);
                    }
                    break;
            }
            //  The view box origin of the other document is placed at (x, y)
            x -= scaleX * other.OX;
            y -= scaleY * other.OY;
            var o = other.Svg;
            var ignore = IgnoreAttributes;
            if (moveDefsEtc)
            {
                var defs = o.Elements(SvgCanvasTools.Namespace + "defs").ToList();
                foreach (var od in defs)
                    foreach (var odc in od.Elements().ToList())
                    {
                        odc.Remove();
                        AddDef(odc);
                    }
                foreach (var od in defs)
                    od.Remove();

                var move = o.Elements().ToList();
                var insertAfter = Defs;
                var me = MoveElements;
                foreach (var od in move)
                {
                    if (!me.Contains(od.Name.LocalName))
                        continue;
                    od.Remove();
                    if (insertAfter == null)
                        Svg.AddFirst(od);
                    else
                        insertAfter.AddAfterSelf(od);
                    insertAfter = od;
                }
            }
            var ce = CreateElement("g");
            var e = new XElement(SvgCanvasTools.Namespace + "g", o.Attributes().Where(x => !ignore.Contains(x.Name.LocalName)), o.Elements());
            e.SetAttributeValue("transform", String.Concat("translate(", Value(x), ',', Value(y), ") scale(", Value(scaleX, "0.######"), ',', Value(scaleY, "0.######"), ')'));
            ce.Add(e);
            return AddElement(ce, null, null, 0);
        }

        /// <summary>
        /// Replace the color of all fill and stroke attributes of the descendants of an element (not the element itself) with a color of the given hue and saturation,
        /// keeping the (scaled) value and alpha of the original color.
        /// Values that are not colors (ex: "none", "url(#a)", "currentColor") are kept unchanged.
        /// </summary>
        /// <param name="doc">The element to process the descendants of</param>
        /// <param name="hue">The new hue</param>
        /// <param name="saturation">The new saturation</param>
        /// <param name="valueScale">A scale applied to the value of the original color (the result is clamped to 1)</param>
        public static void SetFillAndStrokeHue(XElement doc, double hue, double saturation, double valueScale = 1)
        {
            Func<XElement, XAttribute, bool> a = (el, attr) =>
            {
                //  Keep values that aren't colors ("none", "url(#a)", "currentColor" etc)
                if (!HtmlColors.ParseHtmlColor(attr.Value, out var r, out var g, out var b, out var a))
                    return true;
                ColorTools.RgbToHsv(out var h, out var s, out var v, (1.0 / 255.0) * r, (1.0 / 255.0) * g, (1.0 / 255.0) * b);
                var newCol = HashColors.GetWeb(hue, saturation, Math.Min(v * valueScale, 1), a);
                attr.Value = newCol;
                return true;
            };
            OnAttributes(doc, "fill", a);
            OnAttributes(doc, "stroke", a);
        }


        /// <summary>
        /// Add a copy of the content of another canvas in a group element (the other canvas is not modified).
        /// If both <paramref name="maxWidth"/> and <paramref name="maxHeight"/> are greater than zero, the content is uniformly scaled to fit and centered in that box
        /// (the view box origin of the other canvas is then taken into account), else it's only translated by (x, y).
        /// </summary>
        /// <param name="p">The canvas to copy the content from</param>
        /// <param name="x">The left position</param>
        /// <param name="y">The top position</param>
        /// <param name="maxWidth">The width of the box to fit into, zero or less to not scale</param>
        /// <param name="maxHeight">The height of the box to fit into, zero or less to not scale</param>
        /// <param name="onCopy">Optionally called with the svg element of the copy (before the content is moved), can be used to modify the copy</param>
        /// <returns>The added group element</returns>
        public XElement Add(SvgCanvas p, double x = 0, double y = 0, double maxWidth = 0, double maxHeight = 0, Action<XElement> onCopy = null)
        {
            var t = new XDocument(p.Doc);
            var svg = t.Element(SvgCanvasTools.Namespace.GetName("svg"));
            onCopy?.Invoke(svg);
            var e = CreateElement("g");
            double scale = 1;
            if ((maxWidth > 0) && (maxHeight > 0))
            {
                var scaleX = maxWidth / p.Width;
                var scaleY = maxHeight / p.Height;
                scale = Math.Min(scaleX, scaleY);
                var newW = scale * p.Width;
                var newH = scale * p.Height;

                x += 0.5 * (maxWidth - newW);
                y += 0.5 * (maxHeight - newH);

                x -= scale * p.OX;
                y -= scale * p.OY;
            }

            var haveTrans = (x != 0) || (y != 0);
            var haveScale = scale != 1;
            if (haveTrans || haveScale)
            {
                var ci = SvgCanvasTools.Ci;
                String s = haveTrans ? String.Concat("translate(", x.ToString(ci), ' ', y.ToString(ci), ") ") : "";
                if (haveScale)
                    s += String.Concat("scale(", scale.ToString(ci), ")");
                /*                String s = haveScale ? String.Concat("scale(", scale.ToString(Ci), ") ") : "";
                                if (haveTrans)
                                    s += String.Concat("translate(", x.ToString(Ci), ' ', y.ToString(Ci), ')');
                */
                SetAttribute(e, "transform", s.Trim());
            }
            var el = svg.Elements().ToList();
            foreach (var ee in el)
            {
                ee.Remove();
                e.Add(ee);
            }
            Svg.Add(e);
            return e;
        }


        readonly XDocument Doc;
        /// <summary>
        /// The root svg element
        /// </summary>
        public readonly XElement Svg;




    }

    /// <summary>
    /// How to size content in a box
    /// </summary>
    public enum SvgCanvasSizing
    {
        /// <summary>
        /// Uniformly scaled to fit inside the box (all content is visible)
        /// </summary>
        Fit,
        /// <summary>
        /// Uniformly scaled to fill the box (content may extend outside the box)
        /// </summary>
        Fill,
        /// <summary>
        /// Non-uniformly scaled to the box size
        /// </summary>
        Stretch,
    }

    /// <summary>
    /// Alignment of content in a box
    /// </summary>
    public enum SvgCanvasGravity
    {
        /// <summary>
        /// Left / top
        /// </summary>
        Start,
        /// <summary>
        /// Centered
        /// </summary>
        Center,
        /// <summary>
        /// Right / bottom
        /// </summary>
        End,
    }

}