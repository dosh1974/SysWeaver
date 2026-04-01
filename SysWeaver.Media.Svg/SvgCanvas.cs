using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SysWeaver.Media
{

    public static class SvgCanvasTools
    {
        public static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        public static readonly XNamespace Namespace = XNamespace.Get("http://www.w3.org/2000/svg");

        public static String SvgValue(double value, String coordFormat = "0.###")
            => value.ToString(coordFormat, Ci);


        public static XElement SvgOpacity(this XElement e, double opacity)
        {
            if (opacity >= 1)
                return e;
            if (opacity < 0)
                opacity = 0;
            e.SetAttributeValue("opacity", SvgValue(opacity));
            return e;
        }

        public static XElement SvgClipPath(this XElement e, String clipId)
        {
            if (String.IsNullOrEmpty(clipId))
                return e;
            e.SetAttributeValue("clip-path", clipId);
            return e;
        }

        public static XElement SvgFillOpacity(this XElement e, double opacity)
        {
            if (opacity >= 1)
                return e;
            if (opacity < 0)
                opacity = 0;
            e.SetAttributeValue("fill-opacity", SvgValue(opacity));
            return e;
        }


        public static XElement SvgFilter(this XElement e, String idRef)
        {
            if (idRef == null)
                return e;
            e.SetAttributeValue("filter", idRef);
            return e;
        }

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

    public sealed class SvgCanvas
    {

        public static SvgCanvas Create(String svgText, String defaultCoordFormat = "0.##")
        {
            using var x = new StringReader(svgText);
            var doc = XDocument.Load(x);
            return new SvgCanvas(doc, defaultCoordFormat);
        }

        public static SvgCanvas Load(String filename, String defaultCoordFormat = "0.##")
        {
            var doc = XDocument.Load(filename);
            return new SvgCanvas(doc, defaultCoordFormat);
        }

        public static SvgCanvas Load(Stream stream, String defaultCoordFormat = "0.##", bool leaveOpen = true)
        {
            using var x = leaveOpen ? null : stream;
            var doc = XDocument.Load(stream);
            return new SvgCanvas(doc, defaultCoordFormat);
        }

        public static double GetAttributeNumber(XElement e, String attrName, double def = 0)
        {
            var v = e.Attribute(attrName);
            if (v == null)
                return def;
            return ParseNumber(v.Value, def);
        }

        public static bool TryRemoveAttribute(XElement e, String attrName)
        {
            var v = e.Attribute(attrName);
            if (v == null)
                return false;
            v.Remove();
            return true;
        }

        public static double ParseNumber(String vv, double def = 0)
        {
            if (String.IsNullOrEmpty(vv))
                return def;
            if (!double.TryParse(vv, SvgCanvasTools.Ci, out var value))
                return def;
            return value;
        }

        SvgCanvas(XDocument doc, String defaultCoordFormat = "0.##")
        {
            Doc = doc;
            var ns = SvgCanvasTools.Namespace;
            var s = doc.Element(ns.GetName("svg"));
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
                var t = v.Value.Split(' ');
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


        public bool OnAttributes(String attributeName, Func<XElement, XAttribute, bool> onAttr)
            => OnAttributes(Svg, attributeName, onAttr);

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

        public String Color(String color)
        {
            if (String.IsNullOrEmpty(color))
                return null;
            var cols = color.Split(' ');
            if (ColorParsers.TryGetValue(cols[0].FastToLower(), out var fn))
                return fn(this, cols);
            return color;
        }

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

        public String ToSvgString()
        {
            using (var ms = new MemoryStream())
            {
                Doc.Save(ms);
                return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }


        public readonly String DefaultCoordFormat;
        public readonly double OX;
        public readonly double OY;


        public readonly double Width;
        public readonly double Height;

        public String Value(double value, String coordFormat = null)
            => SvgCanvasTools.SvgValue(value, coordFormat ?? DefaultCoordFormat);


        public void AddFilter(XElement el, String filterId)
        {
            el.SetAttributeValue("filter", String.Concat("url(#", filterId, ')'));
        }

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
            if (stdDeviation != 0)
                d.SetAttributeValue("stdDeviation", Value(stdDeviation));
            if (opacity != 1)
                d.SetAttributeValue("flood-opacity", Value(opacity));
            if (dx != 2)
                d.SetAttributeValue("dx", Value(dx));
            if (dy != 2)
                d.SetAttributeValue("dy", Value(dy));
            return b;
        }

        public XElement CreateElement(String localName)
            => new XElement(SvgCanvasTools.Namespace + localName);

        public void SetAttribute(XElement element, String localName, Object value)
            => element.SetAttributeValue(localName, value);


        XElement Defs;

        XElement AddDef(XElement e)
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

        XElement AddElement(XElement e, String fill, String stroke, double strokeWidth)
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
                ds.SetAttributeValue("stdDeviation", stdDeviation);
            ds.SetAttributeValue("flood-color", color ?? "#000");
            if (opacity < 1)
                ds.SetAttributeValue("flood-opacity", Value(opacity));
            AddDef(e);
            return String.Concat("url(#", id, ')');
        }


        public String ClipPathDef(String path, String id = null)
        {
            if (path == null)
                return null;
            var p = CreateElement("path");
            p.SetAttributeValue("d", path);
            return ClipPathDef(id, p);
        }
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



        public String LinearGradientDef(params SvgGradStop[] stops)
            => LinearGradientDef(null, stops);

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

        public String LinearGradientDef(double x1, double y1, double x2, double y2, params SvgGradStop[] stops)
            => LinearGradientDef(null, x1, y1, x2, y2, stops);

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

        public XElement Path(String d, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("path");
            e.SetAttributeValue("d", d);
            return AddElement(e, fill, stroke, strokeWidth);
        }

        public XElement Graph(String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("g");
            return AddElement(e, fill, stroke, strokeWidth);
        }

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
        
        public String GetTransformToFit(SvgMinMaxState s, double x, double y, double width, double height, double angle = 0, String coordFormat = null)
        {
            double scaleX = width / s.Width;
            double scaleY = height / s.Height;
            var scale = Math.Min(scaleX, scaleY);
            var dx = -s.MinX - s.Width * 0.5;
            var dy = -s.MinY - s.Height * 0.5;
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

        public XElement Image(String url, double x, double y, double width, double height)
        {
            if ((url.IndexOf("://") < 0) && (!url.FastStartsWith("data:")) && FileExists(url))
            {
                var mime = MimeTypeMap.GetMimeType(url.Substring(url.LastIndexOf('.') + 1));
                var data = File.ReadAllBytes(url);
                return Image(data.AsSpan(), mime.Item1, x, y, width, height);
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
            e.SetAttributeValue("href", url);
            return AddElement(e, null, null, 0);
        }



        public static void SetFillAndStrokeHue(XElement doc, double hue, double saturation, double valueScale = 1)
        {
            Func<XElement, XAttribute, bool> a = (el, attr) =>
            {
                HtmlColors.ParseHtmlColor(attr.Value, out var r, out var g, out var b, out var a);
                ColorTools.RgbToHsv(out var h, out var s, out var v, (1.0 / 255.0) * r, (1.0 / 255.0) * g, (1.0 / 255.0) * b);
                var newCol = HashColors.GetWeb(hue, saturation, Math.Min(v * valueScale, 1), a);
                attr.Value = newCol;
                return true;
            };
            OnAttributes(doc, "fill", a);
            OnAttributes(doc, "stroke", a);
        }


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
        public readonly XElement Svg;




    }


}