using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.Media
{

    public sealed class SvgScene
    {
        int StyleIndex;
        
        public String AddStyle(ISvgCss style)
        {
            var s = style.GetCss();
            if (String.IsNullOrEmpty(s))
                return null;
            var name = "S" + StyleIndex;
            Styles.Add(String.Concat("\t\t.", name, "\n\t\t{\n", s, "\t\t}"));
            ++StyleIndex;
            return name;
        }

        public String AddMask(String geometries, String style = null, String transform = null)
        {
            if (String.IsNullOrEmpty(style))
            {
                style = AddStyle(new SvgColorStyle
                {
                    FillColor = "#fff",
                    StrokeColor = null,
                    StrokeWidth = 0.25,
                });
            }
            var name = "M" + StyleIndex;
            Masks.Add(Tuple.Create(name, geometries, style, transform));
            ++StyleIndex;
            return name;
        }

        public String AddVerticalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(0, 0, 0, 1, false, null, stops);

        public String AddHorizontalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(0, 0, 1, 0, false, null, stops);

        public String AddDiagonalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(0, 0, 1, 1, false, null, stops);

        public String AddOtherDiagonalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(1, 0, 0, 1, false, null, stops);

        public String AddLinearGradient(double x0, double y0, double x1, double y1, bool userSpace, String transform, params SvgGradStop[] stops)
        {
            var fmt = SvgTools.FullFormat;
            var name = "G" + StyleIndex;
            StringBuilder d = new StringBuilder();
            d.Append($"\t\t<linearGradient id='{name}' x1='{fmt(x0)}' y1='{fmt(y0)}' x2='{fmt(x1)}' y2='{fmt(y1)}'");
            if (userSpace)
                d.Append($" gradientUnits='userSpaceOnUse'");
            if (!String.IsNullOrEmpty(transform))
                d.Append($" gradientTransform='{transform}'");
            d.AppendLine(">");
            foreach (var x in stops)
                d.Append($"\t\t\t").AppendLine(x.ToSvg(fmt));
            d.AppendLine($"\t\t</linearGradient>");
            Defs.Add(d.ToString());
            ++StyleIndex;
            return String.Join(name, "url(#", ')');
        }

        public String AddRadialGradient(double cx, double cy, double r, double fx, double fy, bool userSpace, String transform, params SvgGradStop[] stops)
        {
            var fmt = SvgTools.FullFormat;
            var name = "G" + StyleIndex;
            StringBuilder d = new StringBuilder();
            d.Append($"\t\t<radialGradient id='{name}' cx='{fmt(cx)}' cy='{fmt(cy)}' r='{fmt(r)}' fx='{fmt(fx)}' fy='{fmt(fy)}'");
            if (userSpace)
                d.Append($" gradientUnits='userSpaceOnUse'");
            if (!String.IsNullOrEmpty(transform))
                d.Append($" gradientTransform='{transform}'");
            d.AppendLine(">");
            foreach (var x in stops)
                d.Append($"\t\t\t").AppendLine(x.ToSvg(fmt));
            d.AppendLine($"\t\t</radialGradient>");
            Defs.Add(d.ToString());
            ++StyleIndex;
            return String.Join(name, "url(#", ')');
        }

        public void AddGeometry(String geometries)
        {
            Geometry.Add(geometries);
        }

        public void AddText(String text, SvgTextParams p)
        {
            p = p ?? new SvgTextParams();
            //  Text
            var paths = SvgPath.GetSvgTextPaths(text, new SvgTextPathParams
            {
                Font = p.Font,
                FitWidth = p.FitWidth,
                FitHeight = p.FitHeight,
                MarginX = p.OffsetX,
                MarginY = p.OffsetY,
                MaxDecimals = p.MaxDecimals,
            });
            var path = SvgPath.JoinPaths(paths);
            AddPath(path, p);
        }

        public void AddNGon(int n, SvgNgonParams p)
        {
            p = p ?? new SvgNgonParams();
            var path = SvgPath.GetNGonPath(n, p.Size, p.MaxDecimals, p.AngleOffset, p.OffsetX, p.OffsetY);
            AddPath(path, p);
        }

        public void AddPath(String facePath, Svg3dParams p = null)
        {
            p = p ?? new Svg3dParams();
            var face = p.Face;
            var extrude = p.Extrude;
            var shadow = p.Shadow;
            bool doExtrude = (extrude != null) && ((extrude.FillColor != null) || ((extrude.StrokeColor != null) && (extrude.StrokeWidth > 0)));
            bool doFace = (face != null) && ((face.FillColor != null) || ((face.StrokeColor != null) && (face.StrokeWidth > 0)));
            bool doShadow = shadow?.ShadowColor != null;
            var fmt = SvgTools.GetFormat(p.MaxDecimals);
            //  Extrude extract
            Tuple<String, double, double>[] extPaths = null;
            if (doExtrude)
                extPaths = SvgPath.Extrude(facePath, extrude.ExtrudeX, extrude.ExtrudeY, p.MaxDecimals);
            //  Shadow
            String shadowMask = null;
            if (doShadow)
            {
                bool ext = p.IncludeExtrusionInShadow && doExtrude;
                var s = ext ? 0.25 : extrude.StrokeWidth;
                var maskStyle = AddStyle(new SvgColorStyle
                {
                    FillColor = "#fff",
                    StrokeColor = s > 0 ? "#fff" : null,
                    StrokeWidth = s > 0 ? s : 0,
                });
                var svg = new StringBuilder();
                if (ext)
                {
                    foreach (var x in extPaths)
                        svg.AppendLine($"\t\t\t<path d='{x.Item1}' />");
                }
                s = extrude.StrokeWidth;
                if (s != 0.25)
                {
                    svg.AppendLine($"\t\t\t<path stroke-width='{s}' d='{facePath}' />");
                }
                else
                { 
                    svg.AppendLine($"\t\t\t<path d='{facePath}' />");
                }
                shadowMask = AddMask(svg.ToString(), maskStyle, String.Concat("translate(", fmt(shadow.ShadowX), ' ', fmt(shadow.ShadowY), ')'));
                var shadowStyle = AddStyle(shadow);
                AddGeometry($"\t<rect width='{Width}' height='{Height}' class='{shadowStyle}' mask='url(#{shadowMask})' />");
            }
            //  Do extrude
            if (doExtrude)
            {
                var svg = new StringBuilder();
                if (String.IsNullOrEmpty(extrude.ExtrudeFillLight))
                {
                    var extrudeStyle = AddStyle(extrude);
                    svg.AppendLine($"\t<g class='{extrudeStyle}'>");
                    foreach (var x in extPaths)
                        svg.AppendLine($"\t\t<path d='{x.Item1}' />");
                    svg.AppendLine($"\t</g>");
                }
                else
                {
                    var extrudeStyle = AddStyle(new SvgStrokeStyle
                    {
                        LineJoin = extrude.LineJoin,
                        StrokeWidth = extrude.StrokeWidth,
                    });
                    if (extrudeStyle != null)
                        svg.AppendLine($"\t<g class='{extrudeStyle}'>");
                    else
                        svg.AppendLine($"\t<g>");
                    var col0 = SvgColor.Parse(extrude.ExtrudeFillLight);
                    var col1 = SvgColor.Parse(extrude.FillColor);
                    if (extrude.StrokeWidth > 0)
                    {
                        foreach (var x in extPaths)
                        {
                            var col = SvgColor.Lerp(col0, col1, x.Item3);
                            svg.AppendLine($"\t\t<path fill='{col}' stroke='{col}' d='{x.Item1}' />");
                        }
                    }
                    else
                    {
                        foreach (var x in extPaths)
                        {
                            var col = SvgColor.Lerp(col0, col1, x.Item3);
                            svg.AppendLine($"\t\t<path fill='{col}' d='{x.Item1}' />");
                        }
                    }
                    svg.AppendLine($"\t</g>");
                }
                AddGeometry(svg.ToString());
            }
            //  Main face
            if (doFace)
            {
                var faceStyle = AddStyle(face);
                AddGeometry($"\t<path class='{faceStyle}' d='{facePath}' />");
            }
        }



        readonly List<String> Styles = new List<string>();
        readonly List<String> Defs = new List<string>();
        readonly List<Tuple<String, String, String, String>> Masks = new List<Tuple<string, string, String, string>>();
        readonly List<String> Geometry = new List<string>();


        public readonly double Width;
        public readonly double Height;

        public SvgScene(double width, double height)
        {
            Width = width;
            Height = height;
        }


        public override string ToString() => ToSvg();

        public String ToSvg()
        {
            var tw = Width;
            var th = Height;
            var svg = new StringBuilder();
            svg.AppendLine($"<svg viewBox='0 0 {tw} {th}' xmlns='http://www.w3.org/2000/svg' version='1.1'>");
            var defs = Defs;
            if (defs.Count > 0)
            {
                svg.AppendLine($"\t<defs>");
                foreach (var def in defs)
                    svg.Append(def);
                svg.AppendLine($"\t</defs>");
            }
            var css = Styles;
            if (css.Count > 0)
            {
                svg.AppendLine($"\t<style>");
                foreach (var style in css)
                    svg.AppendLine(style);
                svg.AppendLine($"\t</style>");
            }
            var fmt = SvgTools.FullFormat;
            foreach (var mask in Masks)
            {
                svg.AppendLine($"\t<mask id='{mask.Item1}' maskUnits='objectBoundingBox' width='{tw}' height='{th}'>");
                svg.AppendLine($"\t\t<rect width='{tw}' height='{tw}' fill='#000' />");
                svg.Append($"\t\t<g class='{mask.Item3}'");
                if (!String.IsNullOrEmpty(mask.Item4))
                    svg.Append($" transform='{mask.Item4}'");
                svg.AppendLine(">");
                if (mask.Item2.EndsWith("\n"))
                    svg.Append(mask.Item2);
                else
                    svg.AppendLine(mask.Item2);
                svg.AppendLine($"\t\t</g>");
                svg.AppendLine($"\t</mask>");
            }
            foreach (var r in Geometry)
                svg.AppendLine(r);
            svg.AppendLine("</svg>");
            return svg.ToString();
        }





        static readonly double[] FavIconAngles = [0, 0.15, 0.5 - 0.15];
        static readonly double[] FavIconRads = [8, 16, 32, 64, 128];


        static readonly double[][] FavIconGradients = [
            [ 0.0, 47.9, 52.1, 0.0, 0.0, 1.0, 1.0],
            [ 1.0, 57.9, 62.1, 0.5, 0.5, 0.6, 0.5, 0.5],
            [ 1.0, 57.9, 62.1, 0.1, 0.1, 1.1, 0.2, 0.2],
            [ 1.0, 57.9, 62.1, 0.9, 0.9, 1.1, 0.8, 0.8],
        ];

        public void AddFavIcon(String name, String subTitle = null, HashColors color = null, int seed = 0)
        {
            color = color ?? new HashColors(name, seed);
            var t = new String(name.Where(x => Char.IsUpper(x)).Take(3).ToArray());
            if (t.Length <= 0)
                t = name.Length > 0 ? name.Substring(0, 1).FastToUpper() : "?";
            t = t.Substring(0, 1) + t.Substring(1).FastToLower();
            var rng = new Random(color.Seed);
            switch (rng.Next(4))
            {
                case 0:
                    t = t.FastToLower();
                    break;
                case 1:
                    t = t.FastToUpper();
                    break;
            }
            var acc1 = color.Acc1Dark1;
            var acc2 = color.Acc2Dark1;
            var darkAcc1 = color.Acc1Dark0;
            var darkAcc2 = color.Acc2Dark0;
            var main = color.Main;
            var mainBright = color.MainBright;
            var mainDark0 = color.MainDark0;
            var mainDark1 = color.MainDark1;
            var gradP = FavIconGradients[rng.Next(FavIconGradients.Length)];
            SvgFont GetFont(bool onlyFull = false)
            {
                switch (rng.Next(onlyFull ? 2 : 7))
                {
                    case 0:
                        return SvgFont.AtariClassic;
                    case 1:
                        return SvgFont.MontserratBlack;
                    case 2:
                        return SvgFont.AstroSpace;
                    case 3:
                        return SvgFont.AsianNinja;
                    case 4:
                        return SvgFont.RedemtionRegular;
                    case 5:
                        if (t.Length < 3)
                            return SvgFont.RedemtionRegular;
                        else
                            return SvgFont.RoTwimchRegular;
                    default:
                        return SvgFont.VegapunkFree;
                }
            }
            SvgFont iconFont = GetFont();

            String MakeGrad(string col0, string col1, String transform = null)
            {
                SvgGradStop[] stops =
                [
                    new SvgGradStop(gradP[1], col0),
                    new SvgGradStop(gradP[2], col1),
                ];
                if (gradP[0] > 0)
                    return AddRadialGradient(gradP[3], gradP[4], gradP[5], gradP[6], gradP[7], false, transform, stops);
                return AddLinearGradient(gradP[3], gradP[4], gradP[5], gradP[6], false, transform, stops);
            }
        //  Background plate
            var bp = new SvgNgonParams();
            bp.Face.FillColor = MakeGrad(acc1, acc2);
            bp.Face.StrokeColor = acc2;
            bp.Extrude.FillColor = darkAcc2;
            bp.Extrude.ExtrudeFillLight = darkAcc1;
            bp.Shadow.ShadowColor = "rgba(0,0,0,0.15)";
            bp.OffsetX += (subTitle != null ? 128 : 0);
            if (rng.Next(4) != 0)
            {
                var angles = FavIconAngles;
                bp.AngleOffset = angles[rng.Next(angles.Length)];
                AddNGon(5 + rng.Next(4), bp);
            }
            else
            {
                var rads = FavIconRads;
                var rad = rads[rng.Next(rads.Length)];
                var path = SvgPath.GetRoundedRect(bp.Size, bp.Size, rad, 3, bp.OffsetX, bp.OffsetY);
                AddPath(path, bp);
            }
            //  Subtitle
            if (subTitle != null)
            {
                var text = subTitle;
                switch (rng.Next(4))
                {
                    case 0:
                        text = text.FastToUpper();
                        break;
                    case 1:
                        text = text.FastToLower();
                        break;
                }
                var cols = rng.Next(4) != 0 ? color : color.RotateHue(180);
     
                var textPaths = SvgPath.GetSvgTextPaths(text, GetFont(true), 32, 15);
                var m = SvgPath.GetMinMaxForPaths(textPaths);
                var scale = Math.Min((512- 40) / m.Width, (128 - 40) / m.Height);
                var pwidth = Math.Ceiling((scale * m.Width + 16) / 2) * 2;
                var pheight = Math.Ceiling((scale * m.Height + 16) / 2) * 2;


                var offsetY = 240;

                var rads = FavIconRads;
                var rad = rads[rng.Next(rads.Length)];
                var offsetX = (512 - pwidth) / 2;
                var path = SvgPath.GetRoundedRect(pwidth, pheight, rad, 3, offsetX, offsetY);
                var pp = new Svg3dParams();
                pp.Face.FillColor = MakeGrad(cols.Acc1Dark1, cols.Acc2Dark1);
                pp.Face.StrokeWidth = 0.5;
                pp.Face.StrokeColor = cols.Acc2Dark1;
                //pp.Extrude.ExtrudeX = 1;
                //pp.Extrude.ExtrudeY = 2;
                pp.Extrude.FillColor = cols.Acc2Dark0;
                pp.Extrude.ExtrudeFillLight = cols.Acc1Dark0;
                pp.Shadow.ShadowColor = "rgba(0,0,0,0.15)";
                //pp.Shadow.ShadowX = 1;
                //pp.Shadow.ShadowY = 2;
                AddPath(path, pp);
                SvgPath.FitPaths(textPaths, m, pwidth - 16, pheight - 16, offsetX + 8, offsetY + 4, 3);
                pp.Face.FillColor = MakeGrad(cols.MainBright, cols.Main);
                pp.Face.StrokeWidth = 0.5;
                pp.Face.StrokeColor = cols.MainBright;
                //pp.Extrude.ExtrudeX = 1;
                //pp.Extrude.ExtrudeY = 2;
                pp.Extrude.FillColor = cols.MainDark0;
                pp.Extrude.ExtrudeFillLight = cols.MainDark1;
                pp.Shadow.ShadowColor = "rgba(0,0,0,0.15)";
                pp.Shadow.ShadowX = pp.Extrude.ExtrudeX;
                pp.Shadow.ShadowY = pp.Extrude.ExtrudeY;


                AddPath(SvgPath.JoinPaths(textPaths), pp);
            }
            //  Text
            var p = new SvgTextParams();
            p.Font = iconFont;
            p.OffsetX += (subTitle != null ? 128 : 0);
            var tfmt = SvgTools.GetFormat(3);
            String tr = $"translate({tfmt(-p.Extrude.ExtrudeX / Width)} {tfmt(-p.Extrude.ExtrudeY / Height)})";
            p.Face.FillColor = MakeGrad(mainBright, main, tr);
            p.Face.StrokeWidth = 0.5;
            p.Face.StrokeColor = mainBright;
            p.Shadow.ShadowX = p.Extrude.ExtrudeX;
            p.Shadow.ShadowY = p.Extrude.ExtrudeY;
            p.Shadow.ShadowColor = "rgba(0,0,0,0.15)";
            p.Extrude.FillColor = mainDark0;
            p.Extrude.ExtrudeFillLight = mainDark1;
            AddText(t, p);
        }


        public Byte[] ToPng(int width = 0, int height = 0)
            => SvgBitmapRenderer.CreatePng(ToSvg(), width, height);

    }



}