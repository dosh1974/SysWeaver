using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SysWeaver.IsoData;
using SysWeaver.Media;

namespace SysWeaver.Map
{

    /// <summary>
    /// Functions for getting SVG images from a map
    /// </summary>
    public static class MapGen
    {

        public static int CacheSize => MapCache.CacheSize;

        /// <summary>
        /// Generate a customized map
        /// </summary>
        /// <param name="p">Paramters to use for generating the map</param>
        /// <returns>The SVG representing the map</returns>
        public static String Generate(MapGenParams p)
        {
            var m = MapCache.Get(p);

            XDocument doc;
            using (var s = m.Svg.AsStream())
                doc = XDocument.Load(s);
            var svgElement = doc.Root;
            var ns = svgElement.Name.Namespace;
            StringBuilder css = new StringBuilder();
            Action<XAttribute> setDefault = attr => attr.Remove();
            var bs = p.Base;
            double baseEx = 0;
            if (bs != null)
            {
                bs.WriteCss(css, "X", null);
                setDefault = attr => attr.SetValue("X");
                svgElement.SetAttributeValue("style", "stroke-linejoin:round");
                baseEx += bs.Extrude;
            }

            Dictionary<String, MapRegion> formats = new Dictionary<string, MapRegion>(StringComparer.Ordinal);
            var r = p.Regions;
            if (r != null)
            {
                foreach (var x in r)
                    formats[x.Id] = x;
            }
            var paths = svgElement.Descendants(ns.GetName("path")).ToList();


            var pc = paths.Count;
            List<Tuple<double, double, MapRegionInfo, MapStyle, bool>> texts = new(pc);
            List<Tuple<MapRegionInfo, double, double, String, MapStyle, bool, bool>> extruded = new(pc);
            List<Tuple<MapRegionInfo, double, double, String, MapStyle>> extrudedSpecial = new(pc);
            var bex = p.MaxExtrudeX * baseEx;
            var bey = p.MaxExtrudeY * baseEx;


            var crop = p.CropToRegions;
            var cropState = new SvgMinMaxState();
            foreach (var path in paths)
            {
                var classAttr = path.Attribute("class");
                var id = classAttr.Value;
                var regInfo = m.Regions[id];
                MapStyle prop = p.Base;
                bool special = formats.TryGetValue(id, out var reg);
                if (special)
                {
                    prop = reg;
                    reg.WriteCss(css, id, bs);
                }
                else
                {
                    setDefault(classAttr);
                }
                var e = prop.Extrude;

                var pp = path.Attribute("d").Value;
                if (baseEx > 0)
                {
                    extruded.Add(Tuple.Create(regInfo, bex, bey, pp, p.Base as MapStyle, special, e > 0));
                    path.Remove();
                }
                var ex = p.MaxExtrudeX * e;
                var ey = p.MaxExtrudeY * e;
                if (special)
                {
                    if (baseEx > 0)
                    {
                        String[] d = [pp];
                        SvgPath.TransformPaths(d, 1.0, 1.0, bex, bey, 3);
                        pp = d[0];
                        path.Attribute("d").Value = pp;
                    }
                    if (e > 0)
                    {
                        extrudedSpecial.Add(Tuple.Create(regInfo, ex, ey, pp, prop));
                        if (path.Parent != null)
                            path.Remove();
                    }
                    if (crop != null)
                    {
                        var tex = p.MaxExtrudeX * (e + baseEx);
                        var tey = p.MaxExtrudeY * (e + baseEx);


                        var cx = regInfo.X;
                        var cy = regInfo.Y;
                        var minX = Math.Min(cx, cx + tex);
                        var minY = Math.Min(cy, cy + tey);
                        var maxX = cx + regInfo.W;
                        var maxY = cy + regInfo.H;
                        maxX = Math.Max(maxX, maxX + tex);
                        maxY = Math.Max(maxY, maxY + tey);
                        cropState.Update(minX, minY);
                        cropState.Update(maxX, maxY);
                    }
                }
                if (path.Parent != null)
                {
                    var title = prop.ToolTip;
                    if (!String.IsNullOrEmpty(title))
                    {
                        var ts = new XElement(ns.GetName("title"));
                        ts.Value = GetText(title, regInfo);
                        path.AddFirst(ts);
                    }
                }
                var text = prop.Text;
                if (!String.IsNullOrEmpty(text))
                    texts.Add(Tuple.Create(ex, ey, regInfo, prop, special));
            }

            HashSet<String> doneClasses = new HashSet<string>(StringComparer.Ordinal);

            //  Base extrusion
            if (extruded.Count > 0)
            {

                extruded.Sort((a, b) =>
                {
                    var ta = a.Item1;
                    var tb = b.Item1;
                    double aa = ta.CX * a.Item2 + ta.CY * a.Item3;
                    double bb = tb.CX * b.Item2 + tb.CY * b.Item3;
                    return aa.CompareTo(bb);
                });

                List<Tuple<String, String, double, double>> all = new List<Tuple<string, string, double, double>>();

                //  Add extruded shadows
                foreach (var x in extruded)
                {
                    var ri = x.Item1;
                    var dx = x.Item2;
                    var dy = x.Item3;
                    var path = x.Item4;
                    var isSpecial = x.Item6;
                    String[] d = [path];
                    SvgPath.MakeAbsolute(d, 8);
                    var exs = SvgPath.Extrude(d[0], x.Item2, x.Item3);
                    var cn = "X_";// isSpecial ? (ri.N + "_") : "X_";
                    if (doneClasses.Add(cn))
                        x.Item5.WriteShadowCss(css, cn, bs);
                    foreach (var ex in exs)
                        all.Add(Tuple.Create(cn, ex.Item1, ex.Item2, ex.Item3));
                }
                all.Sort((a, b) => a.Item4.CompareTo(b.Item4));
                foreach (var x in all)
                {
                    var s = new XElement(ns.GetName("path"));
                    s.SetAttributeValue("class", x.Item1);
                    s.SetAttributeValue("d", x.Item2);
                    svgElement.Add(s);
                }

                //  Add extruded faces
                extruded.Reverse();
                foreach (var x in extruded)
                {                                                                                                                                                                                                                                                                       
                    if (x.Item6 && x.Item7)
                        continue;
                    var ri = x.Item1;
                    var dx = x.Item2;
                    var dy = x.Item3;
                    var path = x.Item4;
                    String[] d = [path];
                    SvgPath.TransformPaths(d, 1.0, 1.0, x.Item2, x.Item3, 3);
                    var s = new XElement(ns.GetName("path"));
                    s.SetAttributeValue("class", x.Item6 ? ri.N : "X");
                    s.SetAttributeValue("d", d[0]);
                    var title = x.Item5.ToolTip;
                    if (!String.IsNullOrEmpty(title))
                    {
                        var ts = new XElement(ns.GetName("title"));
                        ts.Value = GetText(title, ri);
                        s.AddFirst(ts);
                    }
                    svgElement.Add(s);
                }


            }


            void AddTexts(bool mustBeSpecial)
            {
                foreach (var t in texts)
                {
                    var special = t.Item5;
                    if (special != mustBeSpecial)
                        continue;
                    var x = t.Item1;
                    var y = t.Item2;
                    var ri = t.Item3;
                    var rs = t.Item4;
                    x += ri.CX;
                    y += ri.CY;

                    var ts = new XElement(ns.GetName("text"));
                    var sx = x.ToString("0.###", CultureInfo.InvariantCulture);
                    ts.SetAttributeValue("x", sx);
                    ts.SetAttributeValue("y", y.ToString("0.###", CultureInfo.InvariantCulture));
                    var txt = GetText(rs.Text, ri);
                    var txts = txt.Split("\n");
                    var tl = txts.Length;
                    if (tl > 1)
                    {
                        for (int ti = 0; ti < tl; ++ ti)
                        {
                            var tx = txts[ti].Trim();
                            var sts = new XElement(ns.GetName("tspan"));
                            sts.SetAttributeValue("x", sx);
                            if (ti == 0)
                                sts.SetAttributeValue("dy", ((1.1 - (tl - 1)) * 0.5).ToString("0.###", CultureInfo.InvariantCulture) + "em");
                            else
                                sts.SetAttributeValue("dy", "1.1em");
                            sts.SetValue(tx);
                            ts.Add(sts);
                        }
                    }
                    else
                    {
                        ts.SetValue(txt);
                    }
                    var cn = special ? (ri.N + "-") : "X-";
                    if (doneClasses.Add(cn))
                        rs.WriteTextCss(css, cn, bs);
                    ts.SetAttributeValue("class", cn);

                    var title = rs.TextToolTip ?? rs.ToolTip ?? bs.TextToolTip ?? bs.ToolTip;
                    if (!String.IsNullOrEmpty(title))
                    {
                        var tts = new XElement(ns.GetName("title"));
                        tts.Value = GetText(title, ri);
                        ts.AddFirst(tts);
                    }

                    svgElement.Add(ts);
                }
            }

            AddTexts(false);

            //  Special extrusion
            if (extrudedSpecial.Count > 0)
            {

                extrudedSpecial.Sort((a, b) =>
                {
                    var ta = a.Item1;
                    var tb = b.Item1;
                    double aa = ta.CX * a.Item2 + ta.CY * a.Item3;
                    double bb = tb.CX * b.Item2 + tb.CY * b.Item3;
                    return aa.CompareTo(bb);
                });


                List<Tuple<String, String, double, double, bool, string>> all = new List<Tuple<string, string, double, double, bool, string>>();

                //  Add extruded shadows
                foreach (var x in extrudedSpecial)
                {
                    var ri = x.Item1;
                    var dx = x.Item2;
                    var dy = x.Item3;
                    var path = x.Item4;
                    String[] d = [path];
                    SvgPath.MakeAbsolute(d, 8);
                    var exs = SvgPath.Extrude(d[0], x.Item2, x.Item3);
                    var cn = ri.N + "_";
                    if (doneClasses.Add(cn))
                        x.Item5.WriteShadowCss(css, cn, bs);
                    foreach (var ex in exs)
                        all.Add(Tuple.Create(cn, ex.Item1, ex.Item2, ex.Item3, false, (String)null));
                }
                all.Sort((a, b) => a.Item4.CompareTo(b.Item4));
                foreach (var x in all)
                {
                    var s = new XElement(ns.GetName("path"));
                    s.SetAttributeValue("class", x.Item1);
                    s.SetAttributeValue("d", x.Item2);
                    svgElement.Add(s);
                }

                //  Add extruded faces
                extrudedSpecial.Reverse();
                foreach (var x in extrudedSpecial)
                {
                    var ri = x.Item1;
                    var dx = x.Item2;
                    var dy = x.Item3;
                    var path = x.Item4;
                    String[] d = [path];
                    SvgPath.TransformPaths(d, 1.0, 1.0, x.Item2, x.Item3, 3);
                    var s = new XElement(ns.GetName("path"));
                    s.SetAttributeValue("class", ri.N);
                    s.SetAttributeValue("d", d[0]);
                    var title = x.Item5.ToolTip;
                    if (!String.IsNullOrEmpty(title))
                    {
                        var ts = new XElement(ns.GetName("title"));
                        ts.Value = GetText(title, ri);
                        s.AddFirst(ts);
                    }
                    svgElement.Add(s);
                }

            }

            AddTexts(true);

            //  Fit into 1024x1024
            var allPaths = svgElement.Descendants(ns.GetName("path")).ToList();
            var allD = allPaths.Select(x => x.Attribute("d").Value).ToList();
            double margin = 4;
            SvgMinMaxState minMax = null;
            if ((crop != null) && (!cropState.IsEmpty))
            {
                var cc = crop ?? 0;
                minMax = cropState;
                if (cc >= 0)
                {
                    margin = cc;
                }else
                {
                    margin = Math.Max(cropState.Width, cropState.Height) * cc * -0.01;
                }
                if (margin > 128)
                    margin = 128;
            }
            else
            {
                minMax = SvgPath.GetMinMaxForPaths(allD);
            }
            var size = SvgPath.NormalizeInto(out var scale, out var px, out var py, allD, minMax, 1024 - margin * 2, 1024 - margin * 2, margin, margin, margin, margin, 1);
            var c = allD.Count;
            for (int i = 0; i < c; ++i)
                allPaths[i].Attribute("d").SetValue(allD[i]);
            var w = Math.Ceiling(size.Item1);
            var h = Math.Ceiling(size.Item2);
            foreach (var ts in svgElement.Descendants(ns.GetName("text")))
            {
                var x = double.Parse(ts.Attribute("x").Value, CultureInfo.InvariantCulture);
                var y = double.Parse(ts.Attribute("y").Value, CultureInfo.InvariantCulture);
                x *= scale;
                y *= scale;
                x += px;
                y += py;
                ts.SetAttributeValue("x", x.ToString("0.###", CultureInfo.InvariantCulture));
                ts.SetAttributeValue("y", y.ToString("0.###", CultureInfo.InvariantCulture));
            }
            if (minMax == cropState)
            {
                var s = new XElement(ns.GetName("g"));
                foreach (var pp in svgElement.Descendants().ToList())
                {
                    pp.Remove();
                    s.Add(pp);
                }
                s.SetAttributeValue("clip-path", "url(#c)");
                svgElement.AddFirst(s);
                var defs = new XElement(ns.GetName("defs"));
                var cp = new XElement(ns.GetName("clipPath"));
                cp.SetAttributeValue("id", "c");
                var cr = new XElement(ns.GetName("rect"));
                cr.SetAttributeValue("x", "0");
                cr.SetAttributeValue("y", "0");
                cr.SetAttributeValue("width", w);
                cr.SetAttributeValue("height", h);
                cp.Add(cr);
                defs.Add(cp);
                svgElement.AddFirst(defs);
            }

            svgElement.SetAttributeValue("viewBox", "0 0 " + w + " " + h);

            if (p.BackgroundColor != null)
            {
                var bg = new XElement(ns.GetName("rect"));
                bg.SetAttributeValue("x", "0");
                bg.SetAttributeValue("y", "0");
                bg.SetAttributeValue("width", w);
                bg.SetAttributeValue("height", h);
                bg.SetAttributeValue("fill", p.BackgroundColor);
                svgElement.AddFirst(bg);
            }
            if (css.Length > 0)
            {
                var s = new XElement(ns.GetName("style"));
                s.Value = css.ToString();
                svgElement.AddFirst(s);
            }

            return doc.ToString(SaveOptions.None);
        }

        static String GetText(String fmt, MapRegionInfo info)
        {
            var rn = info.N;
            fmt = fmt.Replace("{0}", rn);
            var name = IsoCountry.TryGet(rn)?.CommonName ?? rn;
            fmt = fmt.Replace("{1}", name);
            return fmt;
        }

    }


}
