using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SysWeaver.Media
{
    public sealed class SvgBarcodeCode39
    {


        public SvgBarcodeCode39(double width = 1, double height = 32, int decimalCount = 1, double barRatio = 2)
        {
            Width = Math.Max(0.001, width);
            Height = Math.Max(Width * 4, height);
            DecimalMask = decimalCount <= 0 ? "0" : ("0." + new string('#', decimalCount));
            barRatio = Math.Max(2.0, Math.Min(3.0, barRatio));
            BarWidths = [0, width, width * barRatio];

/*            List<String> lines = new List<string>();
            foreach (var x in Widths)
                lines.Add("0x" + x.ToString("x") + ",");
            System.IO.File.WriteAllLines(@"D:\Temp\CodeWidths39.txt", lines);
*/

        }

        readonly double[] BarWidths;

        readonly String DecimalMask;

        String S(double v)
            => v.ToString(DecimalMask, CultureInfo.InvariantCulture);

        public String GetPath(String data, double offsetX = 0, double offsetY = 0)
        {
            var l = data.Length;
            List<int> codes = new(l + 4)
            {
                0
            };
            var m = CodeMap;
            for (int i = 0; i < l; ++i)
            {
                if (!m.TryGetValue(data[i], out var cl))
                    throw new Exception("Char '" + data[i] + "' is not valid for Code39!");
                codes.AddRange(cl);
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
/*            var cl = codes.Count;
            //  Check sum
            int cs = 0;
            for (int i = 1; i < cl; ++i)
                cs += (codes[i] - 1);
*/
            codes.Add(0);
        }

        void AddChar(ref double x, ref double y, StringBuilder c, int code)
        {
            var w = Widths[code];
            var sw = BarWidths;
            var sbh = "v" + S(Height);
            var sy = " " + S(y);
            while (w > 0)
            {
                var width = sw[w & 0x3];
                w >>= 2;
                var sbw = S(width);
                c.Append('M').Append(S(x)).Append(sy);
                c.Append('h').Append(sbw);
                c.Append(sbh);
                c.Append("h-").Append(sbw);
                c.Append('z');
                x += width;
                width = sw[w & 0x3];
                w >>= 2;
                x += width;
            }
        }



        public readonly double Width;
        public readonly double Height;


        static readonly IReadOnlyDictionary<Char, int[]> CodeMap = new Dictionary<Char, int[]>
        {
            { '0', [1] },
            { '1', [2] },
            { '2', [3] },
            { '3', [4] },
            { '4', [5] },
            { '5', [6] },
            { '6', [7] },
            { '7', [8] },
            { '8', [9] },
            { '9', [10] },
            { 'A', [11] },
            { 'B', [12] },
            { 'C', [13] },
            { 'D', [14] },
            { 'E', [15] },
            { 'F', [16] },
            { 'G', [17] },
            { 'H', [18] },
            { 'I', [19] },
            { 'J', [20] },
            { 'K', [21] },
            { 'L', [22] },
            { 'M', [23] },
            { 'N', [24] },
            { 'O', [25] },
            { 'P', [26] },
            { 'Q', [27] },
            { 'R', [28] },
            { 'S', [29] },
            { 'T', [30] },
            { 'U', [31] },
            { 'V', [32] },
            { 'W', [33] },
            { 'X', [34] },
            { 'Y', [35] },
            { 'Z', [36] },
            { '-', [37] },
            { '.', [38] },
            { '$', [39] },
            { '/', [40] },
            { '+', [41] },
            { '%', [42] },
        }.Freeze();

        static readonly int[] Widths =
        [
            0x56659,
            0x56695,
            0x65596,
            0x655a5,
            0x555a6,
            0x65695,
            0x55696,
            0x556a5,
            0x66595,
            0x56596,
            0x565a5,
            0x65956,
            0x65965,
            0x55966,
            0x65a55,
            0x55a56,
            0x55a65,
            0x66955,
            0x56956,
            0x56965,
            0x56a55,
            0x69556,
            0x69565,
            0x59566,
            0x69655,
            0x59656,
            0x59665,
            0x6a555,
            0x5a556,
            0x5a565,
            0x5a655,
            0x6555a,
            0x65569,
            0x5556a,
            0x65659,
            0x5565a,
            0x55669,
            0x66559,
            0x5655a,
            0x56569,
            0x55999,
            0x59599,
            0x59959,
            0x59995,
        ];

        public static readonly SvgBarcodeCode39 Default = new();

    }
}
