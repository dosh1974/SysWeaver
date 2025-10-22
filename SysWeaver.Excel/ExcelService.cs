using GemBox.Spreadsheet;
using GemBox.Spreadsheet.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using SysWeaver.Data;


namespace SysWeaver.Excel
{

    public sealed class ExcelService : IHaveTableDataExporters
    {

        public override string ToString() => (HaveLicense ? "Licensed, table margins (inches): " : "Free limited version, table margins (inches): ") + TableMarginInch.ToString("0.####", CultureInfo.InvariantCulture);

        public ExcelService(ExcelParams p)
        {
            p = p ?? new ExcelParams();
            var apiKey = p.GetApiKey(false);
            if (string.IsNullOrEmpty(apiKey))
                apiKey = "FREE-LIMITED-KEY";
            HaveLicense = !apiKey.FastEquals("FREE-LIMITED-KEY");
            SpreadsheetInfo.SetLicense(apiKey);
            TableMarginInch = Math.Max(0.0, p.TableMarginInch);
        }

        public readonly bool HaveLicense;

        public IReadOnlyList<ITableDataExporter> TableDataExporters =>
            [
                ExcelTableDataExporter.Xlsx,
                ExcelTableDataExporter.Pdf,
            ];


        /// <summary>
        /// Margin for table pages in inches
        /// </summary>
        public static double TableMarginInch { get; private set; } = 0.2;


        public const double Convert_inch_to_mm = 25.4;
        public const double Convert_mm_to_inch = 1.0 / Convert_inch_to_mm;


        public static void PageSetUp(ExcelWorksheet s, double marginInch = 0.2, bool portrait = false, bool singlePage = false)
        {
            var popt = s.PrintOptions;
            popt.HeaderMargin = 0;
            popt.FooterMargin = 0;
            popt.TopMargin = marginInch;
            popt.RightMargin = marginInch;
            popt.BottomMargin = marginInch;
            popt.LeftMargin = marginInch;
            popt.PrintCellNotes = false;
            popt.PrintGridlines = false;
            popt.PrintHeadings = false;
            popt.Portrait = portrait;
            popt.PaperType = PaperType.A4;
            popt.FitWorksheetWidthToPages = 1;
            if (singlePage)
                popt.FitWorksheetHeightToPages = 1;
            popt.HorizontalCentered = true;
            popt.VerticalCentered = singlePage;
        }


    }


    public static class ExcelTools
    {
        public static bool TryGetWebColor(String webColor, out DrawingColor col)
        {
            if (webColor == null)
            {
                col = DrawingColor.FromName(DrawingColorName.Red);
                return false;
            }
            if (!HtmlColors.TryGetArgb(out var c, webColor))
            {
                col = DrawingColor.FromName(DrawingColorName.Red);
                return false;
            }
            var a = c >> 24;
            var r = (int)((c >> 16) & 0xff);
            var g = (int)((c >> 8) & 0xff);
            var b = (int)(c & 0xff);
            col = a < 255 ? DrawingColor.FromRgb(r, g, b, Math.Max(0, 1.0 - (double)a / 255.0)) : DrawingColor.FromRgb(r, g, b);
            return true;
        }
    }

}
