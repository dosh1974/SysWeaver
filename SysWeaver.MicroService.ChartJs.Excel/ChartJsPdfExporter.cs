using GemBox.Spreadsheet;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using SysWeaver.Data;
using SysWeaver.Excel;
using SysWeaver.MicroService;

namespace SysWeaver.Chart
{
    public sealed class ChartJsPdfExporter : IChartExporter
    {
        public static readonly ChartJsPdfExporter Instance = new ChartJsPdfExporter();

        ChartJsPdfExporter()
        {
        }

        public string Name => "PDF document";

        public string Desc => "Save the chart as a PDF document file";

        public string Icon => "IconFilePdf";

        public double Order => -1;

        public bool RequireUser => false;

        public ChartExportInputTypes InputType => ChartExportInputTypes.Svg;


        static XElement FindFirst(XElement element, String name)
        {
            foreach (var e in element.DescendantNodesAndSelf())
            {
                var x = e as XElement;
                if (x == null)
                    continue;
                if (x.Name.LocalName == name)
                    return x;
            }
            return null;
        }

        static readonly Char[] SvgSplitChars = " ,".ToCharArray();
        static readonly Char[] SvgTrimChars = " \tpx".ToCharArray();

        public Task<MemoryFile> Export(object data, object context, ChartExportOptions options = null)
        {
            options = options ?? new ChartExportOptions();
            var d = data as String;
            if (d == null)
                throw new Exception("Expected data of the type " + typeof(String).FullName.ToQuoted());
            var bi = d.IndexOf(";base64,");
            if (bi < 0)
                throw new Exception("Expected base64 encoded data!");
            var dd = Convert.FromBase64String(d.Substring(bi + 8));
            double w = 100;
            double h = 100;
            String bgColor = null;
            using (var ss = new MemoryStream(dd, false))
            {
                XDocument xdoc = XDocument.Load(ss);
#pragma warning disable CS0219
                bool save = false;
#pragma warning restore CS0219
                var xSvg = FindFirst(xdoc.Root, "svg");
                if (xSvg != null)
                {
                    double x = 0, y = 0, width = 0, height = 0;
                    var widthA= xSvg.Attribute("width");
                    if (widthA != null)
                    {
                        var t = widthA.Value.Trim(SvgTrimChars);
                        if (double.TryParse(t, CultureInfo.InvariantCulture, out var temp))
                            width = temp;
                    }
                    var heightA = xSvg.Attribute("height");
                    if (heightA != null)
                    {
                        var t = heightA.Value.Trim(SvgTrimChars);
                        if (double.TryParse(t, CultureInfo.InvariantCulture, out var temp))
                            height = temp;
                    }
                    var va = xSvg.Attribute("viewBox");
                    if (va != null)
                    {
                        var coords = va.Value.Trim().Split(SvgSplitChars, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        if (coords.Length >= 4)
                        {
                            double.TryParse(coords[0], CultureInfo.InvariantCulture, out x);
                            double.TryParse(coords[1], CultureInfo.InvariantCulture, out y);
                            double.TryParse(coords[2], CultureInfo.InvariantCulture, out width);
                            double.TryParse(coords[3], CultureInfo.InvariantCulture, out height);
                            widthA = null;
                            heightA = null;
                        }
                    }
                    if (width > 0)
                        w = width;
                    if (height > 0)
                        h = height;
                    if (widthA == null)
                    {
                        save = true;
                        xSvg.SetAttributeValue("width", w.ToString(CultureInfo.InvariantCulture));
                    }
                    if (heightA == null)
                    {
                        save = true;
                        xSvg.SetAttributeValue("height", h.ToString(CultureInfo.InvariantCulture));
                    }
                }
                xSvg = FindFirst(xdoc.Root, "rect");
                if (xSvg != null)
                {
                    double width = 0, height = 0;
                    var va = xSvg.Attribute("width");
                    if (va != null)
                    {
                        var t = va.Value.Trim(SvgTrimChars);
                        if (double.TryParse(t, CultureInfo.InvariantCulture, out var temp))
                            width = temp;
                    }
                    va = xSvg.Attribute("height");
                    if (va != null)
                    {
                        var t = va.Value.Trim(SvgTrimChars);
                        if (double.TryParse(t, CultureInfo.InvariantCulture, out var temp))
                            height = temp;
                    }
                    if ((w == width) && (h == height))
                    {
                        va = xSvg.Attribute("fill");
                        if (va != null)
                        {
                            var t = va.Value.Trim();
                            bgColor = t;
                        }
                    }
                }
                /*
                if (save)
                {
                    using var os = new MemoryStream();
                    xdoc.Save(os);
                    dd = os.ToArray();
                    d = Encoding.UTF8.GetString(dd);
                }
                */
            }


            var name = String.IsNullOrEmpty(options.Filename) ? "Chart" : options.Filename;
            ExcelFile file = new ExcelFile();
            ExcelWorksheet s = file.Worksheets.Add(name);


            if (bgColor != null)
            {
                if (HtmlColors.TryGetArgb(out uint argb, bgColor))
                {
                    if ((argb & 0xffffff)!= 0xffffff)
                    {
                        var col = SpreadsheetColor.FromArgb((int)argb);
                        s.Cells.Style.FillPattern.SetSolid(col);
                    }
                }
            }

            s.Columns[0].SetWidth(w, LengthUnit.Pixel);
            s.Rows[0].SetHeight(h, LengthUnit.Pixel);
            ExcelPicture p;
            using var ps = new MemoryStream(dd, false);
            p = s.Pictures.Add(ps, ExcelPictureFormat.Svg, new AnchorCell(s.Columns[0], s.Rows[0], true), w, h, LengthUnit.Pixel);
            ExcelService.PageSetUp(s, ChartJsExcelService.ChartMarginInch, (h > w) ^ options.SwapLandscapePortrait, true);

                        
            /*
            var opt = new XlsxSaveOptions
            {
                Type = XlsxType.Xlsx,
            };
            var ms = new MemoryStream();
            file.Save(ms, opt);
            return new MemoryFile(name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length));
            */
            var opt = new PdfSaveOptions
            {
            };
            var ms = new MemoryStream();
            file.Save(ms, opt);
            return Task.FromResult(new MemoryFile(name + ".pdf", "application/pdf", ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length)));
            
        }
    }


}
