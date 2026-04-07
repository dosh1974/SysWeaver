using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SysWeaver.Media
{
    public sealed class SvgBarcodeCode128
    {
        public static readonly SvgBarcodeCode128 Default = new();


        public SvgBarcodeCode128(double width = 1, double height = 32, int decimalCount = 1)
        {
            Width = Math.Max(0.001, width);
            Height = Math.Max(Width * 4, height);
            DecimalMask = decimalCount <= 0 ? "0" : ("0." + new string('#', decimalCount));
            BarWidths = [0, width, width * 2, width * 3, width * 4, width * 5];
        }

        readonly double[] BarWidths;

        readonly String DecimalMask;

        String S(double v)
            => v.ToString(DecimalMask, CultureInfo.InvariantCulture);

        public String GetPath(String data, double offsetX = 0, double offsetY = 0)
        {
            if (data.IsNumeric())
                return UnsafeGetCompactNumericPath(data, offsetX, offsetY);
            if (!data.IsAsciiOnly())
                throw new Exception("Only ASCII data can be encoded ATM!");
            //  TODO: Implement all
            throw new NotImplementedException();
        }

        public String GetGs1Path(String data, double offsetX = 0, double offsetY = 0)
        {
            if (!data.IsNumeric())
                throw new Exception("Only numeric data can be encoded");
            return UnsafeGetGs1Path(data, offsetX, offsetY);
        }

        public String GetNumericPath(String data, double offsetX = 0, double offsetY = 0)
        {
            if (!data.IsNumeric())
                throw new Exception("Only numeric data can be encoded");
            return UnsafeGetCompactNumericPath(data, offsetX, offsetY);
        }

        public String UnsafeGetCompactNumericPath(String data, double offsetX = 0, double offsetY = 0)
        {
            List<int> codes = new List<int>();
            codes.Add(105);
            var l = data.Length;
            var s2 = l >> 1;
            int o = 0;
            for (int i = 0; i < s2; ++ i)
            {
                int ca = data[o] - '0';
                ++o;
                ca *= 10;
                ca += (data[o] - '0');
                ++o;
                codes.Add(ca);
            }
            if (o < l)
            {
                codes.Add(101);
                while (o < l)
                {
                    int ca = data[o] - '0';
                    ++o;
                    ca += 16;
                    codes.Add(ca);
                }
            }
            AddChecksumAndEnd(codes);
            return Render(codes, offsetX, offsetY);
        }

        public String UnsafeGetGs1Path(String data, double offsetX = 0, double offsetY = 0)
        {
            List<int> codes = new List<int>();
            codes.Add(103);
            var l = data.Length;
            for (int i = 0; i < l; ++i)
            {
                int ca = data[i] - '0' + 16;
                codes.Add(ca);
            }
            AddChecksumAndEnd(codes);
            return Render(codes, offsetX, offsetY);
        }


        String Render(List<int> codes, double x, double y)
        {
            var sb = new StringBuilder();
            var cl = codes.Count;
            for (int i = 0; i < cl; ++i)
                AddChar(ref x, ref y, sb, codes[i]);
            return sb.ToString();
        }

        void AddChecksumAndEnd(List<int> codes)
        {
            var cl = codes.Count;
            //  Check sum
            int cs = 0;
            for (int i = 0; i < cl; ++i)
            {
                var w = i <= 0 ? 1 : i;
                w *= codes[i];
                cs += w;
            }
            codes.Add(cs % 103);
            codes.Add(108);
        }

        void AddChar(ref double x, ref double y, StringBuilder c, int code)
        {
            var w = Widths[code];
            var sw = BarWidths;
            var sbh = "v" + S(Height);
            var sy = " " + S(y);
            while (w > 0)
            {
                var width = sw[w & 0xf];
                w >>= 4;
                var sbw = S(width);
                c.Append('M').Append(S(x)).Append(sy);
                c.Append('h').Append(sbw);
                c.Append(sbh);
                c.Append("h-").Append(sbw);
                c.Append('z');
                x += width;
                width = sw[w & 0xf];
                w >>= 4;
                x += width;
            }
        }



        public readonly double Width;
        public readonly double Height;

        static readonly int[] Widths =
        [
            0x222212,
            0x221222,
            0x122222,
            0x322121,
            0x223121,
            0x222131,
            0x312221,
            0x213221,
            0x212231,
            0x312122,
            0x213122,
            0x212132,
            0x232211,
            0x231221,
            0x132221,
            0x222311,
            0x221321,
            0x122321,
            0x112322,
            0x231122,
            0x132122,
            0x212312,
            0x211322,
            0x131213,
            0x222113,
            0x221123,
            0x122123,
            0x212213,
            0x211223,
            0x112223,
            0x321212,
            0x123212,
            0x121232,
            0x323111,
            0x321131,
            0x123131,
            0x313211,
            0x311231,
            0x113231,
            0x313112,
            0x311132,
            0x113132,
            0x331211,
            0x133211,
            0x131231,
            0x321311,
            0x123311,
            0x121331,
            0x121313,
            0x133112,
            0x131132,
            0x311312,
            0x113312,
            0x131312,
            0x321113,
            0x123113,
            0x121133,
            0x311213,
            0x113213,
            0x111233,
            0x111413,
            0x114122,
            0x111134,
            0x422111,
            0x224111,
            0x421121,
            0x124121,
            0x221141,
            0x122141,
            0x412211,
            0x214211,
            0x411221,
            0x114221,
            0x211241,
            0x112241,
            0x112142,
            0x411122,
            0x111314,
            0x211142,
            0x111431,
            0x242111,
            0x241121,
            0x142121,
            0x212411,
            0x211421,
            0x112421,
            0x212114,
            0x211124,
            0x112124,
            0x141212,
            0x121412,
            0x121214,
            0x341111,
            0x143111,
            0x141131,
            0x311411,
            0x113411,
            0x311114,
            0x113114,
            0x141311,
            0x131411,
            0x141113,
            0x131114,
            0x214112,
            0x412112,
            0x232112,
            0x111332,
            0x331112,
            0x2111332,
        ];


    }
}
