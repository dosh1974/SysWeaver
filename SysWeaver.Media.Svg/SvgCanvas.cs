using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SysWeaver.Media
{
    public sealed class SvgCanvas
    {
        public static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        public static readonly XNamespace Namespace = XNamespace.Get("http://www.w3.org/2000/svg");

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
            if (!double.TryParse(vv, Ci, out var value))
                return def;
            return value;
        }

        SvgCanvas(XDocument doc, String defaultCoordFormat = "0.##")
        {
            Doc = doc;
            var ns = Namespace;
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
            s.SetAttributeValue("viewBox", String.Join(' ', Coord(OX), Coord(OY), Coord(width), Coord(height)));
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
            s.SetAttributeValue("viewBox", String.Concat("0 0 ", Coord(width), ' ', Coord(height)));
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

        public String Coord(double value, String coordFormat = null)
            => value.ToString(coordFormat ?? DefaultCoordFormat, Ci);


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
                d.SetAttributeValue("stdDeviation", Coord(stdDeviation));
            if (opacity != 1)
                d.SetAttributeValue("flood-opacity", Coord(opacity));
            if (dx != 2)
                d.SetAttributeValue("dx", Coord(dx));
            if (dy != 2)
                d.SetAttributeValue("dy", Coord(dy));
            return b;
        }

        public XElement CreateElement(String localName)
            => new XElement(Namespace + localName);

        public void SetAttribute(XElement element, String localName, Object value)
            => element.SetAttributeValue(localName, value);


        XElement AddElement(XElement e, String fill, String stroke, double strokeWidth)
        {
            if (fill != null)
                e.SetAttributeValue("fill", fill);
            if (stroke != null)
            {
                e.SetAttributeValue("stroke", stroke);
                e.SetAttributeValue("stroke-width", Coord(strokeWidth));
            }
            Svg.Add(e);
            return e;
        }

        public XElement Rect(double x, double y, double width, double height, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("rect");
            if (x != 0)
                e.SetAttributeValue("x", Coord(x));
            if (y != 0)
                e.SetAttributeValue("y", Coord(y));
            if (width != 0)
                e.SetAttributeValue("width", Coord(width));
            if (height != 0)
                e.SetAttributeValue("height", Coord(height));
            return AddElement(e, fill, stroke, strokeWidth);
        }

        public XElement Circle(double x, double y, double rad, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("circle");
            if (x != 0)
                e.SetAttributeValue("cx", Coord(x));
            if (y != 0)
                e.SetAttributeValue("cy", Coord(y));
            if (rad != 0)
                e.SetAttributeValue("r", Coord(rad));
            return AddElement(e, fill, stroke, strokeWidth);
        }


        public XElement Path(String d, String fill = null, String stroke = null, double strokeWidth = 1)
        {
            var e = CreateElement("path");
            e.SetAttributeValue("d", d);
            return AddElement(e, fill, stroke, strokeWidth);
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
            var svg = t.Element(Namespace.GetName("svg"));
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
                String s = haveTrans ? String.Concat("translate(", x.ToString(Ci), ' ', y.ToString(Ci), ") ") : "";
                if (haveScale)
                    s += String.Concat("scale(", scale.ToString(Ci), ")");
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