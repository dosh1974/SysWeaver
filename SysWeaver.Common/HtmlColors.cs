using System;
using System.Collections.Generic;
using System.Globalization;

namespace SysWeaver
{
/// <summary>
/// Parse, format and manipulate HTML (CSS) color strings: named colors (case insensitive), "#rgb", "#rrggbb", "rgb(r,g,b)" and "rgba(r,g,b,a)".
/// Modern CSS syntax (space separated components, percentages, "#rrggbbaa", hsl(..)) is not supported.
/// </summary>
    public static class HtmlColors
    {

        /// <summary>
        /// Get an rgb value from a html color, 0xaarrggbb
        /// </summary>
        /// <param name="argColor">The color as 0xaarrggbb</param>
        /// <param name="htmlColor">A html color, can be a name [Red] (or "transparent", 0x00000000), a hex value [#f00] or [#ff0000], rgb [rgb(255,0,0)] or rgba [rgba(255,0,0,1.0)]</param>
        /// <returns>True if the input in understood and a hex colour value is returned, false if the format isn't recognized or is malformed (or <paramref name="htmlColor"/> is null)</returns>
        /// <remarks>
        /// Components in "rgb(..)" / "rgba(..)" are clamped to [0, 255] and the alpha to [0, 1].
        /// </remarks>
        public static bool TryGetArgb(out uint argColor, String htmlColor)
        {
            argColor = 0;
            if (htmlColor == null)
                return false;
            htmlColor = htmlColor.Trim();
            if (NameToHex.TryGetValue(htmlColor, out argColor))
            {
                argColor |= 0xff000000U;
                return true;
            }
            //  Produced by MakeHtmlColor / MakeTransparent
            if (String.Equals(htmlColor, "transparent", StringComparison.OrdinalIgnoreCase))
                return true;
            if (htmlColor.StartsWith('#'))
            {
                var cl = htmlColor.Length;
                if (cl == 4)
                {
                    if (!TryParseHex(htmlColor, 1, 1, out var r))
                        return false;
                    if (!TryParseHex(htmlColor, 2, 1, out var g))
                        return false;
                    if (!TryParseHex(htmlColor, 3, 1, out var b))
                        return false;
                    r |= (r << 4);
                    g |= (g << 4);
                    b |= (b << 4);
                    r <<= 16;
                    g <<= 8;
                    argColor = r | g | b | 0xff000000U;
                    return true;
                }
                if (cl == 7)
                {
                    if (!TryParseHex(htmlColor, 1, 2, out var r))
                        return false;
                    if (!TryParseHex(htmlColor, 3, 2, out var g))
                        return false;
                    if (!TryParseHex(htmlColor, 5, 2, out var b))
                        return false;
                    r <<= 16;
                    g <<= 8;
                    argColor = r | g | b | 0xff000000U;
                    return true;
                }
                return false;
            }
            if (htmlColor.StartsWith("rgba", StringComparison.OrdinalIgnoreCase))
            {
                var t = htmlColor.Substring(4).Trim(' ', '(', ')');
                var comp = t.Split(',');
                if (comp.Length != 4)
                    return false;
                if (!TryParseComponent(comp[0], out var r))
                    return false;
                if (!TryParseComponent(comp[1], out var g))
                    return false;
                if (!TryParseComponent(comp[2], out var b))
                    return false;
                if (!double.TryParse(comp[3].Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var da))
                    return false;
                if (!(da >= 0))
                    da = 0;
                if (da > 1)
                    da = 1;
                var a = (uint)Math.Round(da * 255.0);
                a <<= 24;
                r <<= 16;
                g <<= 8;
                argColor = r | g | b | a;
                return true;
            }
            if (htmlColor.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var t = htmlColor.Substring(3).Trim(' ', '(', ')');
                var comp = t.Split(',');
                if (comp.Length != 3)
                    return false;
                if (!TryParseComponent(comp[0], out var r))
                    return false;
                if (!TryParseComponent(comp[1], out var g))
                    return false;
                if (!TryParseComponent(comp[2], out var b))
                    return false;
                r <<= 16;
                g <<= 8;
                argColor = r | g | b | 0xff000000U;
                return true;
            }
            return false;
        }

        static bool TryParseHex(String s, int start, int length, out uint value)
            => uint.TryParse(s.AsSpan(start, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        static bool TryParseComponent(String s, out uint value)
        {
            if (!int.TryParse(s.AsSpan().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                value = 0;
                return false;
            }
            value = (uint)(v < 0 ? 0 : (v > 255 ? 255 : v));
            return true;
        }


        /// <summary>
        /// Get the red, green, blue and alpha component of a html color
        /// </summary>
        /// <param name="htmlColor">A html color, can be a name [Red], a hex value [#f00] or [#ff0000], rgb [rgb(255,0,0)] or rgba [rgba(255,0,0,1.0)]</param>
        /// <param name="r">[0, 255] Red component</param>
        /// <param name="g">[0, 255] Green component</param>
        /// <param name="b">[0, 255] Blue component</param>
        /// <param name="a">[0, 1] Alpha component</param>
        /// <returns>True if the input in understood, false if the format isn't recognized or is malformed</returns>
        public static bool ParseHtmlColor(String htmlColor, out int r, out int g, out int b, out double a)
        {
            if (!TryGetArgb(out var col, htmlColor))
            {
                r = 0;
                g = 0;
                b = 0;
                a = 0;
                return false;
            }
            r = (int)((col >> 16) & 0xff);
            g = (int)((col >> 8) & 0xff);
            b = (int)(col & 0xff);
            a = (col >> 24);
            a /= 255.0;
            return true;
        }

        /// <summary>
        /// Get the shortest version of a given color (using names, 3 component hex or 6 component hex).
        /// Semi-transparent and unrecognized colors are returned unchanged (with white space removed).
        /// </summary>
        /// <param name="htmlColor">A html color, can be a name [Red], a hex value [#f00] or [#ff0000], rgb [rgb(255,0,0)] or rgba [rgba(255,0,0,1.0)]</param>
        /// <returns>The shortest version of the color</returns>
        public static String GetShortest(String htmlColor)
        {
            htmlColor = htmlColor.Trim();
            htmlColor = htmlColor.Replace(" ", "");
            if (!TryGetArgb(out var argb, htmlColor))
                return htmlColor;
            //  Semi-transparent can't be optimized
            if ((argb & 0xff000000U) != 0xff000000U)
                return htmlColor;
            argb &= 0xffffff;
            if (HexToName.TryGetValue(argb, out var name))
            {
                if (name.Length < htmlColor.Length)
                    htmlColor = name;
            }
            var a0 = argb & 0x0f0f0f;
            var a1 = (argb >> 4) & 0x0f0f0f;
            var hex = Hex;
            if (a0 == a1)
            {
                var temp = "#"
                    + hex[(argb >> 16) & 0xf]
                    + hex[(argb >> 8) & 0xf]
                    + hex[argb & 0xf];
                if (temp.Length <= htmlColor.Length)
                    htmlColor = temp;
            }
            else
            {
                var temp = "#"
                    + hex[(argb >> 20) & 0xf]
                    + hex[(argb >> 16) & 0xf]
                    + hex[(argb >> 12) & 0xf]
                    + hex[(argb >> 8) & 0xf]
                    + hex[(argb >> 4) & 0xf]
                    + hex[argb & 0xf];
                if (temp.Length <= htmlColor.Length)
                    htmlColor = temp;
            }
            return htmlColor;
        }


        /// <summary>
        /// Make the shortest html color string for the given red, green, blue and alpha components.
        /// </summary>
        /// <param name="r">[0, 255] Red component</param>
        /// <param name="g">[0, 255] Green component</param>
        /// <param name="b">[0, 255] Blue component</param>
        /// <param name="a">[0, 1] Alpha component</param>
        /// <returns>A html color string</returns>
        public static String MakeHtmlColor(int r, int g, int b, double a = 1)
        {
            if (a <= 0)
                return "transparent";
            r = r < 0 ? 0 : (r > 255 ? 255 : r);
            g = g < 0 ? 0 : (g > 255 ? 255 : g);
            b = b < 0 ? 0 : (b > 255 ? 255 : b);
            if (a >= 1)
            {
                var key = (r << 16) | (g << 8) | b;
                HexToName.TryGetValue((uint)key, out var name);
                var h = Hex;
                var ru = r >> 4;
                var gu = g >> 4;
                var bu = b >> 4;
                var rl = r & 0xf;
                var gl = g & 0xf;
                var bl = b & 0xf;
                if ((rl == ru) && (gl == gu) && (bl == bu))
                    return SelShortest(name, String.Concat('#', h[ru], h[gu], h[bu]));
                return SelShortest(name, String.Concat('#', h[ru], h[rl], h[gu], h[gl], h[bu], h[bl]));
            }
            return String.Concat("rgba(", r, ',', g, ',', b, ',', a.ToString(CultureInfo.InvariantCulture), ')');
        }


        /// <summary>
        /// Make the shortest html color string for the given red, green, blue and alpha components.
        /// </summary>
        /// <param name="rgb">0xrrggbb</param>
        /// <param name="a">[0, 1] Alpha component</param>
        /// <returns>A html color string</returns>
        public static String MakeHtmlColor(uint rgb, double a = 1)
        {
            if (a <= 0)
                return "transparent";
            var r = (rgb >> 16) & 0xff;
            var g = (rgb >> 8) & 0xff;
            var b = rgb & 0xff;
            if (a >= 1) 
            {
                r = r < 0 ? 0 : (r > 255 ? 255 : r);
                g = g < 0 ? 0 : (g > 255 ? 255 : g);
                b = b < 0 ? 0 : (b > 255 ? 255 : b);
                var key = (r << 16) | (g << 8) | b;
                HexToName.TryGetValue((uint)key, out var name);
                var h = Hex;
                var ru = r >> 4;
                var gu = g >> 4;
                var bu = b >> 4;
                var rl = r & 0xf;
                var gl = g & 0xf;
                var bl = b & 0xf;
                if ((rl == ru) && (gl == gu) && (bl == bu))
                    return SelShortest(name, String.Concat('#', h[ru], h[gu], h[bu]));
                return SelShortest(name, String.Concat('#', h[ru], h[rl], h[gu], h[gl], h[bu], h[bl]));
            }
            return String.Concat("rgba(", r, ',', g, ',', b, ',', a.ToString(CultureInfo.InvariantCulture), ')');
        }

        /// <summary>
        /// Make the given color transparent
        /// </summary>
        /// <param name="htmlColor">A html color, can be a name [Red], a hex value [#f00] or [#ff0000], rgb [rgb(255,0,0)] or rgba [rgba(255,0,0,1.0)]</param>
        /// <param name="opacity">The opacity (multiplied with the html color opacity)</param>
        /// <returns>The transparent color</returns>
        public static String MakeTransparent(String htmlColor, double opacity = 0.5)
        {
            if (htmlColor == null)
                return htmlColor;
            if (opacity <= 0)
                return "transparent";
            if (opacity >= 1)
                return htmlColor;
            if (!ParseHtmlColor(htmlColor, out var r, out var g, out var b, out var a))
                return htmlColor;
            return MakeHtmlColor(r, g, b, a * opacity);
        }



        /// <summary>
        /// Lerp between 2 colors
        /// </summary>
        /// <param name="htmlColorA">A html color, can be a name [Red], a hex value [#f00] or [#ff0000], rgb [rgb(255,0,0)] or rgba [rgba(255,0,0,1.0)]</param>
        /// <param name="htmlColorB">A html color, can be a name [Red], a hex value [#f00] or [#ff0000], rgb [rgb(255,0,0)] or rgba [rgba(255,0,0,1.0)]</param>
        /// <param name="distance">The distance to move from A to B, 0 = A, 1 = B (not clamped)</param>
        /// <returns>The resulting color, or the unparsable input color (A is checked first). Null if either input is null.</returns>
        public static String MakeTransparentLerp(String htmlColorA, String htmlColorB, double distance = 0.5)
        {
            if (htmlColorA == null)
                return htmlColorA;
            if (htmlColorB == null)
                return htmlColorB;
            if (!ParseHtmlColor(htmlColorA, out var ar, out var ag, out var ab, out var aa))
                return htmlColorA;

            if (!ParseHtmlColor(htmlColorB, out var br, out var bg, out var bb, out var ba))
                return htmlColorB;

            br -= ar;
            bg -= ag;
            bb -= ab;
            ba -= aa;
           
            double dr = distance * br;
            double dg = distance * bg;
            double db = distance * bb;
            double da = distance * ba;

            dr += ar;
            dg += ag;
            db += ab;
            da += aa;

            dr = Math.Round(dr);
            dg = Math.Round(dg);
            db = Math.Round(db);

            return MakeHtmlColor((int)dr, (int)dg, (int)db, da);
        }



/// <summary>
/// Convert a HTML color to HSV.
/// </summary>
/// <param name="h">Hue in degrees [0, 360).</param>
/// <param name="s">Saturation [0, 1].</param>
/// <param name="v">Value [0, 1].</param>
/// <param name="a">Alpha [0, 1].</param>
/// <param name="htmlColor">A html color, unrecognized colors give black with zero alpha.</param>
        public static void HtmlToHsv(out double h, out double s, out double v, out double a, String htmlColor)
        {
            const double toN = (1.0 / 255.0);
            var x = HtmlColors.ParseHtmlColor(htmlColor, out var r, out var g, out var b, out a);
            ColorTools.RgbToHsv(out h, out s, out v, toN * r, toN * g, toN * b);
        }

/// <summary>
/// Convert a HSV color to the shortest HTML color string.
/// </summary>
/// <param name="h">Hue in degrees [0, 360].</param>
/// <param name="s">Saturation [0, 1].</param>
/// <param name="v">Value [0, 1].</param>
/// <param name="a">Alpha [0, 1].</param>
/// <returns>A html color string, see <see cref="MakeHtmlColor(int, int, int, double)"/>.</returns>
        public static String HsvToHtml(double h, double s, double v, double a = 1.0)
        {
            ColorTools.HsvToRgb(out var r, out var g, out var b, h, s, v);
            r *= 255.0;
            g *= 255.0;
            b *= 255.0;
            r += 0.5;
            g += 0.5;
            b += 0.5;
            return MakeHtmlColor((int)r, (int)g, (int)b, a);
        }

/// <summary>
/// Scale the HSV value (brightness) of a HTML color.
/// </summary>
/// <param name="color">A html color.</param>
/// <param name="adjust">The factor to multiply the value with, ex: 0.8 makes the color darker, 1.2 lighter (clamped when converted back).</param>
/// <returns>The adjusted html color string.</returns>
        public static String AdjustLightness(String color, double adjust = 1.0)
        {
            HtmlToHsv(out var h, out var s, out var v, out var a , color);
            return HsvToHtml(h, s, v * adjust, a);
        }


        static readonly char[] Hex = "0123456789abcdef".ToCharArray();


        static readonly Dictionary<String, uint> Names = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            {"AliceBlue",0xF0F8FF},
            {"AntiqueWhite",0xFAEBD7},
            {"Aqua",0x00FFFF},
            {"Aquamarine",0x7FFFD4},
            {"Azure",0xF0FFFF},
            {"Beige",0xF5F5DC},
            {"Bisque",0xFFE4C4},
            {"Black",0x000000},
            {"BlanchedAlmond",0xFFEBCD},
            {"Blue",0x0000FF},
            {"BlueViolet",0x8A2BE2},
            {"Brown",0xA52A2A},
            {"BurlyWood",0xDEB887},
            {"CadetBlue",0x5F9EA0},
            {"Chartreuse",0x7FFF00},
            {"Chocolate",0xD2691E},
            {"Coral",0xFF7F50},
            {"CornflowerBlue",0x6495ED},
            {"Cornsilk",0xFFF8DC},
            {"Crimson",0xDC143C},
            {"Cyan",0x00FFFF},
            {"DarkBlue",0x00008B},
            {"DarkCyan",0x008B8B},
            {"DarkGoldenRod",0xB8860B},
            {"DarkGrey",0xA9A9A9},
            {"DarkGreen",0x006400},
            {"DarkKhaki",0xBDB76B},
            {"DarkMagenta",0x8B008B},
            {"DarkOliveGreen",0x556B2F},
            {"Darkorange",0xFF8C00},
            {"DarkOrchid",0x9932CC},
            {"DarkRed",0x8B0000},
            {"DarkSalmon",0xE9967A},
            {"DarkSeaGreen",0x8FBC8F},
            {"DarkSlateBlue",0x483D8B},
            {"DarkSlateGrey",0x2F4F4F},
            {"DarkTurquoise",0x00CED1},
            {"DarkViolet",0x9400D3},
            {"DeepPink",0xFF1493},
            {"DeepSkyBlue",0x00BFFF},
            {"DimGray",0x696969},
            {"DodgerBlue",0x1E90FF},
            {"FireBrick",0xB22222},
            {"FloralWhite",0xFFFAF0},
            {"ForestGreen",0x228B22},
            {"Fuchsia",0xFF00FF},
            {"Gainsboro",0xDCDCDC},
            {"GhostWhite",0xF8F8FF},
            {"Gold",0xFFD700},
            {"GoldenRod",0xDAA520},
            {"Grey",0x808080},
            {"Green",0x008000},
            {"GreenYellow",0xADFF2F},
            {"HoneyDew",0xF0FFF0},
            {"HotPink",0xFF69B4},
            {"IndianRed",0xCD5C5C},
            {"Indigo",0x4B0082},
            {"Ivory",0xFFFFF0},
            {"Khaki",0xF0E68C},
            {"Lavender",0xE6E6FA},
            {"LavenderBlush",0xFFF0F5},
            {"LawnGreen",0x7CFC00},
            {"LemonChiffon",0xFFFACD},
            {"LightBlue",0xADD8E6},
            {"LightCoral",0xF08080},
            {"LightCyan",0xE0FFFF},
            {"LightGoldenRodYellow",0xFAFAD2},
            {"LightGrey",0xD3D3D3},
            {"LightGreen",0x90EE90},
            {"LightPink",0xFFB6C1},
            {"LightSalmon",0xFFA07A},
            {"LightSeaGreen",0x20B2AA},
            {"LightSkyBlue",0x87CEFA},
            {"LightSlateGrey",0x778899},
            {"LightSteelBlue",0xB0C4DE},
            {"LightYellow",0xFFFFE0},
            {"Lime",0x00FF00},
            {"LimeGreen",0x32CD32},
            {"Linen",0xFAF0E6},
            {"Magenta",0xFF00FF},
            {"Maroon",0x800000},
            {"MediumAquaMarine",0x66CDAA},
            {"MediumBlue",0x0000CD},
            {"MediumOrchid",0xBA55D3},
            {"MediumPurple",0x9370DB},
            {"MediumSeaGreen",0x3CB371},
            {"MediumSlateBlue",0x7B68EE},
            {"MediumSpringGreen",0x00FA9A},
            {"MediumTurquoise",0x48D1CC},
            {"MediumVioletRed",0xC71585},
            {"MidnightBlue",0x191970},
            {"MintCream",0xF5FFFA},
            {"MistyRose",0xFFE4E1},
            {"Moccasin",0xFFE4B5},
            {"NavajoWhite",0xFFDEAD},
            {"Navy",0x000080},
            {"OldLace",0xFDF5E6},
            {"Olive",0x808000},
            {"OliveDrab",0x6B8E23},
            {"Orange",0xFFA500},
            {"OrangeRed",0xFF4500},
            {"Orchid",0xDA70D6},
            {"PaleGoldenRod",0xEEE8AA},
            {"PaleGreen",0x98FB98},
            {"PaleTurquoise",0xAFEEEE},
            {"PaleVioletRed",0xD87093},
            {"PapayaWhip",0xFFEFD5},
            {"PeachPuff",0xFFDAB9},
            {"Peru",0xCD853F},
            {"Pink",0xFFC0CB},
            {"Plum",0xDDA0DD},
            {"PowderBlue",0xB0E0E6},
            {"Purple",0x800080},
            {"Red",0xFF0000},
            {"RosyBrown",0xBC8F8F},
            {"RoyalBlue",0x4169E1},
            {"SaddleBrown",0x8B4513},
            {"Salmon",0xFA8072},
            {"SandyBrown",0xF4A460},
            {"SeaGreen",0x2E8B57},
            {"SeaShell",0xFFF5EE},
            {"Sienna",0xA0522D},
            {"Silver",0xC0C0C0},
            {"SkyBlue",0x87CEEB},
            {"SlateBlue",0x6A5ACD},
            {"SlateGrey",0x708090},
            {"Snow",0xFFFAFA},
            {"SpringGreen",0x00FF7F},
            {"SteelBlue",0x4682B4},
            {"Tan",0xD2B48C},
            {"Teal",0x008080},
            {"Thistle",0xD8BFD8},
            {"Tomato",0xFF6347},
            {"Turquoise",0x40E0D0},
            {"Violet",0xEE82EE},
            {"Wheat",0xF5DEB3},
            {"White",0xFFFFFF},
            {"WhiteSmoke",0xF5F5F5},
            {"Yellow",0xFFFF00},
            {"YellowGreen",0x9ACD32},
            {"RebeccaPurple",0x663399},
            //  Alternative CSS spellings, after the names above so that those are used when making names (same length)
            {"DarkGray",0xA9A9A9},
            {"DarkSlateGray",0x2F4F4F},
            {"DimGrey",0x696969},
            {"Gray",0x808080},
            {"LightGray",0xD3D3D3},
            {"LightSlateGray",0x778899},
            {"SlateGray",0x708090},
        };

        static readonly IReadOnlyDictionary<String, uint> NameToHex = Names.Freeze();


        static IReadOnlyDictionary<uint, String> GetHexToName()
        {
            var d = new Dictionary<uint, String>();
            //  In declaration order, the first name wins on equal length
            foreach (var x in Names)
            {
                var newName = x.Key;
                var hex = x.Value;
                if (d.TryGetValue(hex, out var name))
                {
                    if (newName.Length >= name.Length)
                        continue;
                }
                d[hex] = newName;
            }
            return d.Freeze(); 
        }

        static readonly IReadOnlyDictionary<uint, String> HexToName = GetHexToName();

        static String SelShortest(String a, String b)
        {
            if (a == null)
                return b;
            if (b == null)
                return a;
            return a.Length < b.Length ? a : b;
        }




    }


}
