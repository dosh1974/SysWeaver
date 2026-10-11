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

        /// <summary>
        /// Default path (narrow width 2, height 64, no decimals)
        /// </summary>
        public static readonly SvgBarcodeItf14 Default = new (2, -32, 0);


        /// <summary>
        /// Compact path, the height of the bar code need to be scaled up by 64 to show correctly.
        /// </summary>
        public static readonly SvgBarcodeItf14 Compact = new(2, 1, 0);



        static void Throw1()
        {
            throw new ArgumentException("Only 13 digits are supported!", "digits");
        }
        static void Throw2()
        {
            throw new ArgumentException("Only digits can be supplied!", "digits");
        }

        static uint Digit(String text, int index)
        {
            var d = (uint)text[index] - '0';
            if (d >= 10)
                ThrowNotDigit(text, index);
            return d;
        }

        static void ThrowNotDigit(String text, int index)
        {
            throw new ArgumentException("Only digits are supported, found '" + text[index] + "' at index " + index.ToString(CultureInfo.InvariantCulture) + "!", nameof(text));
        }

        /// <summary>
        /// Compute the GTIN check digit of 13 digits (the 14th digit of an ITF-14 / GTIN-14)
        /// </summary>
        /// <param name="digits">Exactly 13 digits</param>
        /// <returns>The check digit ('0' - '9')</returns>
        /// <exception cref="ArgumentException"><paramref name="digits"/> is not exactly 13 digits</exception>
        public static Char ComputeCheckSum(String digits)
        {
            var ds = digits.AsSpan();
            if (ds.Length != 13)
                Throw1();
            int sum = 0;
            for (int i = 0; i < 13; i += 2)
            {
                var d = ds[i] - '0';
                if ((d < 0) || (d >= 10))
                    Throw2();
                sum += d;
            }
            sum *= 3;
            for (int i = 1; i < 13; i += 2)
            {
                var d = ds[i] - '0';
                if ((d < 0) || (d >= 10))
                    Throw2();
                sum += d;
            }
            sum %= 10;
            sum = sum == 0 ? 0 : (10 - sum);
            return (Char)('0' + sum);
        }

        /// <summary>
        /// Compute the GTIN style check digit of any number of digits without validation (weight 3 for even indices counted from the start,
        /// so it's only the GTIN check digit for an odd number of digits)
        /// </summary>
        /// <param name="digits">The digits</param>
        /// <returns>The check digit (garbage if the input contains other chars than digits)</returns>
        public static Char ComputeCheckSumUnsafe(String digits)
        {
            var ds = digits.AsSpan();
            var len = ds.Length;
            int sum = 0;
            for (int i = 0; i < len ; i += 2)
            {
                var d = ds[i] - '0';
                sum += d;
            }
            sum *= 3;
            for (int i = 1; i < len; i += 2)
            {
                var d = ds[i] - '0';
                sum += d;
            }
            sum %= 10;
            sum = sum == 0 ? 0 : (10 - sum);
            return (Char)('0' + sum);
        }


        /// <summary>
        /// Init an instance
        /// </summary>
        /// <param name="narrowPixelWidth">The width of the most narrow bar in the code, everything else is derviced using best practices</param>
        /// <param name="height">Height, less than zero is a multiplier of the narrow width, greater than zero sets the height to that, zero is 32 narrow widths</param>
        /// <param name="decimalCount">Number of decimals</param>
        /// <param name="wideMultiplier">The multiplier to apply to get a wide bar</param>
        /// <param name="barBleed">[0, 0.5] account for ink-bleed by reducing the black bar width by this amount (as a fraction)</param>
        public SvgBarcodeItf14(double narrowPixelWidth, double height = -32, int decimalCount = 1, double wideMultiplier = 2.5, double barBleed = 0)
        {
            Narrow = narrowPixelWidth;
            Wide = Narrow * wideMultiplier;
            Height = height == 0 ? (Narrow * 32) : (height < 0 ? (Narrow * -height) : height);
            DecimalMask = decimalCount <= 0 ? "0" : ("0." + new string('#', decimalCount));
            BarBleed = Math.Max(0.0, Math.Min(0.5, barBleed)) * narrowPixelWidth;
        }

        readonly double BarBleed;
        readonly String DecimalMask;

        String S(double v)
            => v.ToString(DecimalMask, CultureInfo.InvariantCulture);

        /// <summary>
        /// The height in pixels of the bar code
        /// </summary>
        public double Height { get; init; }

        /// <summary>
        /// Measures the width of a given text: from the left edge of the first bar to the right edge of the last bar of <see cref="GetPath"/>
        /// </summary>
        /// <param name="text">Must be even number of digits, only digits are supported</param>
        /// <returns>The width in pixels of the text, 0 if the text is empty or has an odd length</returns>
        /// <exception cref="ArgumentException"><paramref name="text"/> has an even length and contains a char that is not a digit ('0' - '9')</exception>
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
                var d0 = Digit(text, i);
                var d1 = Digit(text, i + 1);
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
            var h = Height;
            var bb = BarBleed;
            var sw = S(w - bb);
            var sn = S(n - bb);
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
        /// Get a path (interleaved 2 of 5 with start / stop pattern, one closed rectangle sub path per bar, no quiet zone, no bearer bars, no check digit is added)
        /// </summary>
        /// <param name="text">Must be even number of digits, only digits are supported</param>
        /// <param name="x">The left position</param>
        /// <param name="y">The top postion</param>
        /// <returns>An svg path with the barcode, null if the text is empty or has an odd length</returns>
        /// <exception cref="ArgumentException"><paramref name="text"/> has an even length and contains a char that is not a digit ('0' - '9')</exception>
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
                var d0 = Digit(text, i);
                var d1 = Digit(text, i + 1);
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

        /// <summary>
        /// The width of a wide bar / space (narrow width * wide multiplier)
        /// </summary>
        public readonly double Wide;
        /// <summary>
        /// The width of a narrow bar / space
        /// </summary>
        public readonly double Narrow;

        static readonly int[] Numbers = GetNumbers();
    }
}
