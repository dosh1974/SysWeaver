using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysWeaver.Media
{

    /// <summary>
    /// Builds an svg document from styles (css classes), gradients, masks and geometry (svg text snippets), see <see cref="ToSvg"/>.
    /// Generated names ("S0", "M1", "G2" etc) use a shared counter, so they are unique within a scene.
    /// All generated numbers are formatted using the invariant culture. Not thread safe.
    /// </summary>
    public sealed class SvgScene
    {
        int StyleIndex;

        /// <summary>
        /// Add a css class with the css of a style
        /// </summary>
        /// <param name="style">The style</param>
        /// <returns>The name of the css class ("S" + index), null if the css is empty (nothing is added)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="style"/> is null (DEBUG builds only)</exception>
        public String AddStyle(ISvgCss style)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(style);
#endif//DEBUG
            var s = style.GetCss();
            if (String.IsNullOrEmpty(s))
                return null;
            var name = "S" + StyleIndex;
            Styles.Add(String.Concat("\t\t.", name, "\n\t\t{\n", s, "\t\t}"));
            ++StyleIndex;
            return name;
        }

        /// <summary>
        /// Add a mask
        /// </summary>
        /// <param name="geometries">The svg elements (text) of the mask content</param>
        /// <param name="style">The css class name to apply to the mask content, null or empty to add a style with a white fill</param>
        /// <param name="transform">An optional transform of the mask content</param>
        /// <returns>The id of the mask ("M" + index), use with mask='url(#id)'</returns>
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

        /// <summary>
        /// Add a top to bottom linear gradient
        /// </summary>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The fill / stroke value to use the gradient: "url(#id)"</returns>
        public String AddVerticalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(0, 0, 0, 1, false, null, stops);

        /// <summary>
        /// Add a left to right linear gradient
        /// </summary>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The fill / stroke value to use the gradient: "url(#id)"</returns>
        public String AddHorizontalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(0, 0, 1, 0, false, null, stops);

        /// <summary>
        /// Add a top left to bottom right linear gradient
        /// </summary>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The fill / stroke value to use the gradient: "url(#id)"</returns>
        public String AddDiagonalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(0, 0, 1, 1, false, null, stops);

        /// <summary>
        /// Add a top right to bottom left linear gradient
        /// </summary>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The fill / stroke value to use the gradient: "url(#id)"</returns>
        public String AddOtherDiagonalGradient(params SvgGradStop[] stops)
            => AddLinearGradient(1, 0, 0, 1, false, null, stops);

        /// <summary>
        /// Add a linear gradient definition
        /// </summary>
        /// <param name="x0">The start x (x1)</param>
        /// <param name="y0">The start y (y1)</param>
        /// <param name="x1">The end x (x2)</param>
        /// <param name="y1">The end y (y2)</param>
        /// <param name="userSpace">If true the coordinates are in user space (gradientUnits='userSpaceOnUse'), else relative to the bounding box</param>
        /// <param name="transform">An optional gradientTransform</param>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The fill / stroke value to use the gradient: "url(#id)", the id is "G" + index</returns>
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

        /// <summary>
        /// Add a radial gradient definition
        /// </summary>
        /// <param name="cx">The center x</param>
        /// <param name="cy">The center y</param>
        /// <param name="r">The radius</param>
        /// <param name="fx">The focal point x</param>
        /// <param name="fy">The focal point y</param>
        /// <param name="userSpace">If true the coordinates are in user space (gradientUnits='userSpaceOnUse'), else relative to the bounding box</param>
        /// <param name="transform">An optional gradientTransform</param>
        /// <param name="stops">The gradient stops</param>
        /// <returns>The fill / stroke value to use the gradient: "url(#id)", the id is "G" + index</returns>
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

        /// <summary>
        /// Add some svg elements (text, added as is, one line in the output)
        /// </summary>
        /// <param name="geometries">The svg elements</param>
        public void AddGeometry(String geometries)
        {
            Geometry.Add(geometries);
        }

        /// <summary>
        /// Add a text (converted to paths and fitted into FitWidth x FitHeight at OffsetX, OffsetY) using <see cref="AddPath"/>
        /// </summary>
        /// <param name="text">The text</param>
        /// <param name="p">The parameters (the font must be set), null for defaults (fails since there is no default font)</param>
        /// <exception cref="ArgumentNullException">The font is null</exception>
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

        /// <summary>
        /// Add a regular n-gon (see <see cref="SvgPath.GetNGonPath"/>) using <see cref="AddPath"/>
        /// </summary>
        /// <param name="n">The number of vertices, must be at least 3</param>
        /// <param name="p">The parameters, null for defaults</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is less than 3</exception>
        public void AddNGon(int n, SvgNgonParams p)
        {
            p = p ?? new SvgNgonParams();
            var path = SvgPath.GetNGonPath(n, p.Size, p.MaxDecimals, p.AngleOffset, p.OffsetX, p.OffsetY);
            AddPath(path, p);
        }


        /// <summary>
        /// Add a rectangle (with a new css class for the style)
        /// </summary>
        /// <param name="x">The left position</param>
        /// <param name="y">The top position</param>
        /// <param name="w">The width</param>
        /// <param name="h">The height</param>
        /// <param name="color">The style</param>
        /// <param name="maxDecimals">The maximum number of decimals of the coordinates</param>
        public void AddRect(double x, double y, double w, double h, SvgColorStyle color, int maxDecimals = 4)
        {
            var style = AddStyle(color);
            var fmt = SvgTools.GetFormat(maxDecimals);
            var svg = new StringBuilder();
            svg.Append($"\t\t\t<rect width='{fmt(w)}' height='{fmt(h)}' ");
            if (x != 0)
                svg.Append($"x='{fmt(x)}' ");
            if (y != 0)
                svg.Append($"y='{fmt(y)}' ");
            svg.AppendLine($"class='{style}' />");
            AddGeometry(svg.ToString());
        }

        /// <summary>
        /// Add a path with an optional drop shadow, extrusion (3D sides, see <see cref="SvgPath.Extrude"/>) and face.
        /// The shadow is a rectangle covering the scene, masked by the (translated) face and extrusion.
        /// The extrusion only supports absolute M, L, H, V, Q and Z commands.
        /// If <see cref="SvgExtrudeParams.ExtrudeFillLight"/> is set, each side face gets a color interpolated between it and the extrusion fill color
        /// (both must be colors that <see cref="SvgColor.Parse"/> supports).
        /// </summary>
        /// <param name="facePath">The path data</param>
        /// <param name="p">The style parameters, null for defaults. Set Face, Extrude or Shadow (or their colors) to null to skip that part</param>
        /// <exception cref="NotSupportedException">The path contains a command that the extrusion doesn't support</exception>
        /// <exception cref="FormatException">The extrusion light and fill colors can't be parsed</exception>
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
                var s = ext ? 0.25 : (extrude?.StrokeWidth ?? 0);
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
                s = extrude?.StrokeWidth ?? 0;
                if (s != 0.25)
                {
                    svg.AppendLine($"\t\t\t<path stroke-width='{SvgTools.FullFormat(s)}' d='{facePath}' />");
                }
                else
                { 
                    svg.AppendLine($"\t\t\t<path d='{facePath}' />");
                }
                shadowMask = AddMask(svg.ToString(), maskStyle, String.Concat("translate(", fmt(shadow.ShadowX), ' ', fmt(shadow.ShadowY), ')'));
                var shadowStyle = AddStyle(shadow);
                AddGeometry($"\t<rect width='{SvgTools.FullFormat(Width)}' height='{SvgTools.FullFormat(Height)}' class='{shadowStyle}' mask='url(#{shadowMask})' />");
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


        /// <summary>
        /// The width of the scene (the view box width)
        /// </summary>
        public readonly double Width;
        /// <summary>
        /// The height of the scene (the view box height)
        /// </summary>
        public readonly double Height;

        /// <summary>
        /// Create an empty scene
        /// </summary>
        /// <param name="width">The width of the scene (the view box width)</param>
        /// <param name="height">The height of the scene (the view box height)</param>
        public SvgScene(double width, double height)
        {
            Width = width;
            Height = height;
        }


        /// <summary>
        /// Same as <see cref="ToSvg"/>
        /// </summary>
        /// <returns>The svg document</returns>
        public override string ToString() => ToSvg();

        /// <summary>
        /// Get the svg document: the svg element (view box "0 0 Width Height"), followed by the defs (gradients), the style (css classes),
        /// the masks and the geometries in the order they were added. Lines are separated by <see cref="Environment.NewLine"/>.
        /// </summary>
        /// <returns>The svg document</returns>
        public String ToSvg()
        {
            var fmt = SvgTools.FullFormat;
            var tw = fmt(Width);
            var th = fmt(Height);
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
            foreach (var mask in Masks)
            {
                svg.AppendLine($"\t<mask id='{mask.Item1}' maskUnits='objectBoundingBox' width='{tw}' height='{th}'>");
                svg.AppendLine($"\t\t<rect width='{tw}' height='{th}' fill='#000' />");
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

        /// <summary>
        /// Add a generated (deterministic) icon for a name: a background plate (n-gon or rounded square), the initials of the name (up to 3 upper case letters,
        /// else the first char) using one of the embedded fonts, and an optional sub title plate.
        /// The layout is made for a 256 x 256 scene without a sub title and a 512 x 384 scene with a sub title.
        /// The same name, sub title, colors and seed always gives the same icon.
        /// </summary>
        /// <param name="name">The name to make an icon for</param>
        /// <param name="subTitle">An optional sub title</param>
        /// <param name="color">The colors to use, null to derive them from the name and seed</param>
        /// <param name="seed">A seed used to derive the colors (if <paramref name="color"/> is null)</param>
        /// <param name="opaque">If true a background rectangle (covering the scene) is added</param>
        public void AddFavIcon(String name, String subTitle = null, HashColors color = null, int seed = 0, bool opaque = false)
        {
            color = color ?? new HashColors(name, seed);
            var t = new String(name.Where(x => Char.IsUpper(x)).Take(3).ToArray());
            if (t.Length <= 0)
                t = name.Length > 0 ? name.FastToUpper(0, 1) : "?";
            t = t.Substring(0, 1) + t.FastToLower(1);
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

            if (opaque)
                AddRect(0, 0, Width, Height, new SvgColorStyle
                {
                    FillColor = color.Background,
                });
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


        /// <summary>
        /// Render the scene to a png image, see <see cref="SvgBitmapRenderer.CreatePng(string, int, int)"/>
        /// </summary>
        /// <param name="width">The image width, zero or less to use the scene width (each axis is scaled independently)</param>
        /// <param name="height">The image height, zero or less to use the scene height (each axis is scaled independently)</param>
        /// <returns>The png file data</returns>
        public Byte[] ToPng(int width = 0, int height = 0)
            => SvgBitmapRenderer.CreatePng(ToSvg(), width, height);

    }



}