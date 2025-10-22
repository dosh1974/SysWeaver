using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class ColorTools
    {

        public static String[] HtmlRandomGradient(int count, String text, double opacity = 1)
        {
            var rng = new Random(HashColors.SeedFromString(text));
            var range = rng.Next(90, 360);
            var hstart = rng.Next(0, 360);
            if ((rng.Next() & 1) != 0)
                range = -range;
            var sat = rng.NextDouble() * 0.3 + 0.6;
            var v = rng.NextDouble() * 0.2 + 0.5;
            return HtmlHueGradient(count, hstart, hstart + range, sat, v, opacity);
        }

        public static String[] HtmlHueGradient(int count, double h0, double h1, double s = 0.8, double v = 0.6, double opacity = 1, double s1 = -1, double v1 = -1, double opacity1 = -1)
        {
            h1 -= h0;
            var step = count > 1 ? (1.0 / (count - 1)) : 0;
            h1 *= step;
            s1 = s1 < 0 ? 0 : (s1 - s) * step;
            v1 = v1 < 0 ? 0 : (v1 - v) * step;
            opacity1 = opacity1 < 0 ? 0 : (opacity1 - opacity) * step;

            var t = new String[count];
            for (int i = 0; i < count; ++i)
            {
                var hh = h1 * i + h0;
                var ss = s1 * i + s;
                var vv = v1 * i + v;
                if (hh < 0)
                {
                    var add = Math.Floor(hh / -360) + 2;
                    add *= 360;
                    hh += add;
                }
                hh %= 360;

                var u = HsvToRgb(hh, ss, vv);
                var oo = opacity1 * i + opacity;
                if (oo >= 1)
                {
                    t[i] = "#" + u.ToString("x").PadLeft(6, '0');
                }
                else
                {
                    t[i] = String.Concat("rgba(", (u >> 16) & 0xff, ',', (u >> 8) & 0xff, ',', u & 0xff, ',', oo.ToString(CultureInfo.InvariantCulture), ')');
                }
            }
            return t;
        }





        /// <summary>
        /// Convert a HSV value to rgb uint (0xrrggbb)
        /// </summary>
        /// <param name="h">Hue in degrees [0, 360] </param>
        /// <param name="s">Saturation in [0, 1]</param>
        /// <param name="v">Value in [0, 1]</param>
        /// <returns>An rgb value as an uint (0xrrggbb)</returns>
        public static uint HsvToRgb(double h, double s, double v)
        {
            HsvToRgb(out var r, out var g, out var b, h, s, v);
            var ir = r <= 0 ? 0 : (uint)(r * 255);
            var ig = g <= 0 ? 0 : (uint)(g * 255);
            var ib = b <= 0 ? 0 : (uint)(b * 255);
            if (ir > 255)
                ir = 255;
            if (ig > 255)
                ig = 255;
            if (ib > 255)
                ib = 255;
            return (ir << 16) | (ig << 8) | ib;
        }

        /// <summary>
        /// Convert a HSV value to rgb
        /// </summary>
        /// <param name="r">Red result in [0, 1]</param>
        /// <param name="g">Green result in [0, 1]</param>
        /// <param name="b">Blue result in [0, 1]</param>
        /// <param name="h">Hue in degrees [0, 360] </param>
        /// <param name="s">Saturation in [0, 1]</param>
        /// <param name="v">Value in [0, 1]</param>
        public static void HsvToRgb(out double r, out double g, out double b, double h, double s, double v)
        {
            if (s == 0)
            {
                r = v;
                g = v;
                b = v;
            }
            else
            {
                int i;
                double f, p, q, t;

                if (h == 360)
                    h = 0;
                else
                    h = h / 60;

                i = (int)Math.Truncate(h);
                f = h - i;

                p = v * (1.0 - s);
                q = v * (1.0 - (s * f));
                t = v * (1.0 - (s * (1.0 - f)));

                switch (i)
                {
                    case 0:
                        r = v;
                        g = t;
                        b = p;
                        break;

                    case 1:
                        r = q;
                        g = v;
                        b = p;
                        break;

                    case 2:
                        r = p;
                        g = v;
                        b = t;
                        break;

                    case 3:
                        r = p;
                        g = q;
                        b = v;
                        break;

                    case 4:
                        r = t;
                        g = p;
                        b = v;
                        break;

                    default:
                        r = v;
                        g = p;
                        b = q;
                        break;
                }
            }
        }


        public static void RgbToHsv(out double h, out double s, out double v, double r, double g, double b)
        {
            double min, max, delta;

            min = r < g ? r : g;
            min = min < b ? min : b;

            max = r > g ? r : g;
            max = max > b ? max : b;

            v = max;                                // v
            delta = max - min;
            if (delta < 0.00001)
            {
                s = 0;
                h = 0; // undefined, maybe nan?
                return;
            }
            if (max > 0.0)
            {   // NOTE: if Max is == 0, this divide would cause a crash
                s = (delta / max);                  // s
            }
            else
            {
                // if max is 0, then r = g = b = 0              
                // s = 0, h is undefined
                s = 0.0;
                h = 0;
                return;
            }
            if (r >= max)                           // > is bogus, just keeps compilor happy
                h = (g - b) / delta;        // between yellow & magenta
            else
                if (g >= max)
                h = 2.0 + (b - r) / delta;  // between cyan & yellow
            else
                h = 4.0 + (r - g) / delta;  // between magenta & cyan
            h *= 60.0;                              // degrees
            if (h < 0.0)
                h += 360.0;
        }




        public static void SplitRgba(out double r, out double g, out double b, out double a, uint rgba)
        {
            r = (rgba >> 24);
            g = (rgba >> 16) & 0xff;
            b = (rgba >> 8) & 0xff;
            a = rgba & 0xff;
        }

        public static uint ToClampedInt(double v)
        {
            if (v <= 0)
                return 0;
            var t = (uint)Math.Round(v);
            return t > 255 ? 255 : t;
        }

        public static String GetInterpolatedAsHtml(uint fromRgba, uint toRgba, double fraction)
        {
            if (fraction < 0)
                fraction = 0;
            if (fraction > 1)
                fraction = 1;
            SplitRgba(out var r0, out var g0, out var b0, out var a0, fromRgba);
            SplitRgba(out var r1, out var g1, out var b1, out var a1, toRgba);
            r1 -= r0;
            g1 -= g0;
            b1 -= b0;
            a1 -= a0;
            r1 *= fraction;
            g1 *= fraction;
            b1 *= fraction;
            a1 *= fraction;
            r1 += r0;
            g1 += g0;
            b1 += b0;
            a1 += a0;
            var r = ToClampedInt(r1);
            var g = ToClampedInt(g1);
            var b = ToClampedInt(b1);
            if (a1 >= 255)
                return String.Concat('#', r.ToString("x").PadLeft(2, '0'), g.ToString("x").PadLeft(2, '0'), b.ToString("x").PadLeft(2, '0'));
            a1 *= (1.0 / 255.0);
            if (a1 < 0)
                a1 = 0;
            if (a1 > 1)
                a1 = 1;
            return String.Concat("rgba(", r, ',', g, ',', b, ',', a1.ToString("0.00000", CultureInfo.InvariantCulture), ')');
        }




    }

}
