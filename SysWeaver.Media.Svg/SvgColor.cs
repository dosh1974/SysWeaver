using System;

namespace SysWeaver.Media
{
    public sealed class SvgColor
    {

        public static SvgColor Lerp(SvgColor from, SvgColor to, double distance)
        {
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
            throw new Exception("Invalid char!");
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

        public static SvgColor Parse(String s)
        {
            if (s.StartsWith("rgb"))
            {
                s = s.Substring(3).Trim("() \t".ToCharArray());
                var vals = s.Split(',');
                return new SvgColor(
                    int.Parse(vals[0].Trim()),
                    int.Parse(vals[1].Trim()),
                    int.Parse(vals[2].Trim())
                    );
            }
            if (s.StartsWith("rgba"))
            {
                s = s.Substring(4).Trim("() \t".ToCharArray());
                var vals = s.Split(',');
                return new SvgColor(
                    int.Parse(vals[0].Trim()),
                    int.Parse(vals[1].Trim()),
                    int.Parse(vals[2].Trim()),
                    double.Parse(vals[3].Trim())
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
            throw new Exception("Names not supported!");
        }

        public readonly int R;

        public readonly int G;

        public readonly int B;

        public readonly double A;


        public readonly String Html;

        public override string ToString() => Html;

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
                Html = String.Concat("rgba(", r, ',', g, ',', b, ',', a, ')');
                return;
            }
            var rr = r.ToString("x").PadLeft(2, '0');
            var gg = g.ToString("x").PadLeft(2, '0');
            var bb = b.ToString("x").PadLeft(2, '0');
            if ((rr[0] == rr[1]) && (gg[0] == gg[1]) && (bb[0] == bb[1]))
            {
                Html = String.Concat('#', rr[0], gg[0], bb[0]);
                return;
            }
            Html = String.Concat('#', rr, gg, bb);
        }
    }


}