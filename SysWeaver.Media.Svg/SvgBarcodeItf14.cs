using System;
using System.Globalization;
using System.Text;

namespace SysWeaver.Media
{

    /// <summary>
    /// Class used to create itf-14 bar codes as a path
    /// </summary>
    public sealed class SvgBarcodeItf14
    {

        public static readonly SvgBarcodeItf14 Default = new (2, 0);

        /// <summary>
        /// Init an instance
        /// </summary>
        /// <param name="narrowPixelWidth">The width of the most narrow bar in the code, everything else is derviced using best practices</param>
        /// <param name="decimalCount">Number of decimals</param>
        public SvgBarcodeItf14(double narrowPixelWidth, int decimalCount = 1)
        {
            Narrow = narrowPixelWidth;
            Wide = Narrow * 2.5;
            Height = Narrow * 24;
            DecimalMask = decimalCount <= 0 ? "0" : ("0." + new string('#', decimalCount));
        }

        readonly String DecimalMask;

        String S(double v)
            => v.ToString(DecimalMask, CultureInfo.InvariantCulture);

        /// <summary>
        /// The height in pixels of the bar code
        /// </summary>
        public double Height { get; init; }

        /// <summary>
        /// Measures the width of a given text
        /// </summary>
        /// <param name="text">Must be even number of digits, only digits are supported</param>
        /// <returns>The width in pixels of the text</returns>
        public double Measure(String text)
            => InternalWidth(text, Wide, Narrow);

        static double InternalWidth(String text, double w, double n)
        {
            var l = text.Length;
            if ((l <= 0) || ((l & 1) != 0))
                return 0;
            double x = Width(0, 4, w, n);
            for (int i = 0; i < l; i += 2)
            {
                uint d0 = ((uint)text[i] - '0') % 10;
                uint d1 = ((uint)text[i + 1] - '0') % 10;
                int value = (Numbers[d0] << 1);
                value |= Numbers[d1];
                x += Width(value, 10, w, n);
            }
            x += Width(4, 3, w, n);
            return x;
        }


        static double Width(int value, int bitCount, double w, double n)
        {
            int mask = 1 << bitCount;
            double x = 0;
            for (int i = 0; i < bitCount; ++i)
            {
                mask >>= 1;
                var dx = (value & mask) != 0 ? w : n;
                x += dx;
            }
            return x;
        }


        void Render(StringBuilder b, ref int value, ref int bitCount, ref double x, double y)
        {
            int c = bitCount & ~1;
            int mask = 1 << bitCount;
            bitCount -= c;
            var w = Wide;
            var n = Narrow;
            var h = Height - 4 * Narrow;
            var sw = S(w);
            var sn = S(n);
            var sh = S(h);
            var sy = S(y);
            for (int i = 0; i < c; i += 2)
            {
                mask >>= 1;
                var iw = (value & mask) != 0;
                var sdx = iw ? sw : sn;
                b.Append('M').Append(S(x)).Append(' ').Append(sy);
                b.Append('h').Append(sdx);
                b.Append('v').Append(sh);
                b.Append("h-").Append(sdx);
                b.Append('z');
                var dx = iw ? w : n;
                x += dx;
                mask >>= 1;
                dx = (value & mask) != 0 ? w : n;
                x += dx;
            }
        }


        /// <summary>
        /// Get a path
        /// </summary>
        /// <param name="text">Must be even number of digits, only digits are supported</param>
        /// <param name="x">The left position</param>
        /// <param name="y">The top postion</param>
        /// <returns>An svg path with the barcode</returns>
        public String GetPath(String text, double x = 0, double y = 0)
        {
            var l = text.Length;
            if ((l <= 0) || ((l & 1) != 0))
                return null;
            var sb = new StringBuilder();
            int bitCount = 4;
            int value = 0;
            for (int i = 0; i < l; i += 2)
            {
                uint d0 = ((uint)text[i] - '0') % 10;
                uint d1 = ((uint)text[i + 1] - '0') % 10;
                value <<= 10;
                value |= (Numbers[d0] << 1);
                value |= Numbers[d1];
                bitCount += 10;
                if (bitCount > 20)
                    Render(sb, ref value, ref bitCount, ref x, y);
            }
            value <<= 4;
            value |= 8;
            bitCount += 4;
            Render(sb, ref value, ref bitCount, ref x, y);
            return sb.ToString();
        }

        static int[] GetNumbers()
        {
            String[] numbers = new string[]
            {
                    "00110", "10001", "01001", "11000", "00101", "10100", "01100", "00011", "10010", "01010",
            };
            int[] n = new int[10];
            for (int i = 0; i < 10; ++i)
            {
                var t = numbers[i];
                int v = 0;
                if (t[0] == '1')
                    v |= (1 << (2 * 4));
                if (t[1] == '1')
                    v |= (1 << (2 * 3));
                if (t[2] == '1')
                    v |= (1 << (2 * 2));
                if (t[3] == '1')
                    v |= (1 << (2 * 1));
                if (t[4] == '1')
                    v |= (1 << (2 * 0));
                n[i] = v;
            }
            return n;
        }

        public readonly double Wide;
        public readonly double Narrow;

        static readonly int[] Numbers = GetNumbers();
    }
}
