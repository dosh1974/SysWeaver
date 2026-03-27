using System;
using System.Text;

namespace SysWeaver.Media
{

    /// <summary>
    /// Class used to create itf-14 bar codes as a path
    /// </summary>
    public sealed class SvgBarcodeItf14
    {

        /// <summary>
        /// Init an instance
        /// </summary>
        /// <param name="narrowPixelWidth">The width of the most narrow bar in the code, everything else is derviced using best practices</param>
        public SvgBarcodeItf14(int narrowPixelWidth)
        {
            Narrow = narrowPixelWidth;
            Wide = (5 * Narrow + 1) / 2;
            Height = Narrow * 24;
        }

        /// <summary>
        /// The height in pixels of the bar code
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// Measures the width of a given text
        /// </summary>
        /// <param name="text">Must be even number of digits, only digits are supported</param>
        /// <returns>The width in pixels of the text</returns>
        public int Measure(String text)
        {
            var l = text.Length;
            if ((l <= 0) || ((l & 1) != 0))
                return 0;
            int x = Width(0, 4);
            for (int i = 0; i < l; i += 2)
            {
                uint d0 = ((uint)text[i] - '0') % 10;
                uint d1 = ((uint)text[i + 1] - '0') % 10;
                int value = (Numbers[d0] << 1);
                value |= Numbers[d1];
                x += Width(value, 10);
            }
            x += Width(4, 3);
            return x;
        }

        int Width(int value, int bitCount)
        {
            int mask = 1 << bitCount;
            var w = Wide;
            var n = Narrow;
            int x = 0;
            for (int i = 0; i < bitCount; ++i)
            {
                mask >>= 1;
                var dx = (value & mask) != 0 ? w : n;
                x += dx;
            }
            return x;
        }


        void Render(StringBuilder b, ref int value, ref int bitCount, ref int x, int y)
        {
            int c = bitCount & ~1;
            int mask = 1 << bitCount;
            bitCount -= c;
            var w = Wide;
            var n = Narrow;
            var h = Height - 4 * Narrow;
            for (int i = 0; i < c; i += 2)
            {
                mask >>= 1;
                var dx = (value & mask) != 0 ? w : n;

                b.Append('M').Append(x).Append(' ').Append(y);
                b.Append('h').Append(dx);
                b.Append('v').Append(h);
                b.Append("h-").Append(dx);
                b.Append('z');
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
        public String GetPath(String text, int x = 0, int y = 0)
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

        public readonly int Wide;
        public readonly int Narrow;

        static int[] Numbers = GetNumbers();
    }
}
