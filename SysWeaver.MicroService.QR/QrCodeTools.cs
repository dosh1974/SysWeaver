using QRCoder;
using System;
using System.Text;



namespace SysWeaver.MicroService
{
    static class QrCodeTools
    {
        public static String GetWebColor(System.Drawing.Color color)
        {
            return System.Drawing.ColorTranslator.ToHtml(color);
        }

        public static String GetSvgPath(QRCodeData data, String bright = "#fff", String dark = "#000", bool safeArea = true)
        {

            var height = data.ModuleMatrix.Count;
            var width = data.ModuleMatrix[0].Count;
            int sub = safeArea ? 0 : 4;
            StringBuilder b = new StringBuilder(String.Format(""""<svg version="1.1" shape-rendering="crispEdges" viewBox="0 0 {0} {1}" xmlns="http://www.w3.org/2000/svg"><path fill="{2}" d="m0,0h{0}v{1}h-{0}" /><path fill="{3}" d="""", width - sub * 2, height - sub * 2, bright, dark));
            b.Append('"');
            int y = -1;
            int prevX = sub;
            int prevY = sub;
            foreach (var row in data.ModuleMatrix)
            {
                ++y;
                for (int x = 0; x < width; ++x)
                {
                    if (!row.Get(x))
                        continue;
                    int len = 1;
                    var maxLen = width - x - 1;
                    for (; len < maxLen; ++len)
                    {
                        if (!row.Get(x + len))
                            break;
                    }
                    b.Append('m').Append(x - prevX).Append(',').Append(y - prevY);
                    b.Append("v1h").Append(len).Append("v-1z");
                    prevX = x;
                    prevY = y;
                    x += len - 1;
                }
            }
            b.Append("\" /></svg>");
            return b.ToString();
        }


    }

}
