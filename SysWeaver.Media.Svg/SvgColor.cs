using System;
using System.Globalization;

namespace SysWeaver.Media
{
    /// <summary>
    /// An immutable RGBA color (components are clamped to [0, 255], alpha to [0, 1]) with a short html / css representation (<see cref="Html"/>)
    /// </summary>
    public sealed class SvgColor
    {

        /// <summary>
        /// Linear interpolation between two colors (the rgb components are rounded to the nearest integer)
        /// </summary>
        /// <param name="from">The color at distance 0</param>
        /// <param name="to">The color at distance 1</param>
        /// <param name="distance">The interpolation factor, values outside [0, 1] extrapolates (the result is clamped)</param>
        /// <returns>A new color</returns>
        /// <exception cref="ArgumentNullException"><paramref name="from"/> or <paramref name="to"/> is null (DEBUG builds only)</exception>
        public static SvgColor Lerp(SvgColor from, SvgColor to, double distance)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
#endif//DEBUG
            var r0 = from.R;
            var g0 = from.G;
            var b0 = from.B;
            var a0 = from.A;

            double dr = to.R - r0;
            double dg = to.G - g0;
            double db = to.B - b0;
            double da = to.A - a0;

            dr *= distance;
            dg *= distance;
            db *= distance;
            da *= distance;

            dr += r0;
            dg += g0;
            db += b0;
            da += a0;

            return new SvgColor(
                (int)Math.Round(dr),
                (int)Math.Round(dg),
                (int)Math.Round(db),
                da);
        }

        static int H(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            throw new FormatException("Invalid hex char '" + c + "'!");
        }
        static int H1(String s, int pos)
        {
            var c = H(s[pos]);
            c = (c << 4) | c;
            return c;
        }

        static int H2(String s, int pos)
        {
            var c = H(s[pos]);
            c <<= 4;
            c |= H(s[pos + 1]);
            return c;
        }

        /// <summary>
        /// Parse a color, supported formats: "#rgb", "#rrggbb", "rgb(r, g, b)" and "rgba(r, g, b, a)" (integer components, alpha as a decimal number, invariant culture).
        /// Color names, percentages and other css formats are not supported.
        /// </summary>
        /// <param name="s">The text to parse</param>
        /// <returns>The color</returns>
        /// <exception cref="FormatException">The color can't be parsed</exception>
        /// <exception cref="IndexOutOfRangeException">An rgb / rgba color has too few components</exception>
        /// <exception cref="ArgumentNullException"><paramref name="s"/> is null (DEBUG builds only)</exception>
        public static SvgColor Parse(String s)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(s);
#endif//DEBUG
            var ci = CultureInfo.InvariantCulture;
            if (s.StartsWith("rgba", StringComparison.Ordinal))
            {
                s = s.Substring(4).Trim("() \t".ToCharArray());
                var vals = s.Split(',');
                return new SvgColor(
                    int.Parse(vals[0].Trim(), ci),
                    int.Parse(vals[1].Trim(), ci),
                    int.Parse(vals[2].Trim(), ci),
                    double.Parse(vals[3].Trim(), ci)
                    );
            }
            if (s.StartsWith("rgb", StringComparison.Ordinal))
            {
                s = s.Substring(3).Trim("() \t".ToCharArray());
                var vals = s.Split(',');
                return new SvgColor(
                    int.Parse(vals[0].Trim(), ci),
                    int.Parse(vals[1].Trim(), ci),
                    int.Parse(vals[2].Trim(), ci)
                    );
            }
            if (s.StartsWith('#'))
            {
                var h = s.Substring(1).Trim();
                var l = h.Length;
                if (l == 3)
                    return new SvgColor(
                        H1(h, 0),
                        H1(h, 1),
                        H1(h, 2)
                        );
                if (l == 6)
                    return new SvgColor(
                        H2(h, 0),
                        H2(h, 2),
                        H2(h, 4)
                        );
            }
            throw new FormatException("Unsupported color \"" + s + "\" (names are not supported)!");
        }

        /// <summary>
        /// The red component [0, 255]
        /// </summary>
        public readonly int R;

        /// <summary>
        /// The green component [0, 255]
        /// </summary>
        public readonly int G;

        /// <summary>
        /// The blue component [0, 255]
        /// </summary>
        public readonly int B;

        /// <summary>
        /// The alpha (opacity) [0, 1]
        /// </summary>
        public readonly double A;


        /// <summary>
        /// The html / css representation: "transparent" if alpha is 0, "rgba(r,g,b,a)" (invariant culture) if alpha is less than 1,
        /// else "#rgb" when possible or "#rrggbb" (lower case)
        /// </summary>
        public readonly String Html;

        /// <summary>
        /// Same as <see cref="Html"/>
        /// </summary>
        /// <returns>The html / css representation</returns>
        public override string ToString() => Html;

        /// <summary>
        /// Create a color, out of range components are clamped
        /// </summary>
        /// <param name="r">The red component [0, 255]</param>
        /// <param name="g">The green component [0, 255]</param>
        /// <param name="b">The blue component [0, 255]</param>
        /// <param name="a">The alpha (opacity) [0, 1]</param>
        public SvgColor(int r, int g, int b, double a = 1.0)
        {
            if (r < 0)
                r = 0;
            if (r > 255)
                r = 255;

            if (g < 0)
                g = 0;
            if (g > 255)
                g = 255;

            if (b < 0)
                b = 0;
            if (b > 255)
                b = 255;

            if (a < 0)
                a = 0;
            if (a > 1)
                a = 1;

            R = r;
            G = g;
            B = b;
            A = a;
            if (a <= 0)
            {
                Html = "transparent";
                return;
            }
            if (a < 1)
            {
                Html = String.Create(CultureInfo.InvariantCulture, $"rgba({r},{g},{b},{a})");
                return;
            }
            //  The components are in [0, 255], "#rgb" if every component has two equal hex digits, else "#rrggbb" (lower case)
            var rgb = (r << 16) | (g << 8) | b;
            if (((rgb >> 4) & 0x0f0f0f) == (rgb & 0x0f0f0f))
            {
                Html = String.Create(4, rgb, static (dst, v) =>
                {
                    dst[0] = '#';
                    dst[1] = LowerHexChars[(v >> 16) & 15];
                    dst[2] = LowerHexChars[(v >> 8) & 15];
                    dst[3] = LowerHexChars[v & 15];
                });
                return;
            }
            Html = String.Create(7, rgb, static (dst, v) =>
            {
                dst[0] = '#';
                for (int i = 6; i > 0; --i)
                {
                    dst[i] = LowerHexChars[v & 15];
                    v >>= 4;
                }
            });
        }

        const String LowerHexChars = "0123456789abcdef";
    }


}