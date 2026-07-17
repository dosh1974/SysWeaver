using GemBox.Spreadsheet;
using GemBox.Spreadsheet.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using SysWeaver.Data;


namespace SysWeaver.Excel
{

    public sealed class ExcelService : IHaveTableDataExporters
    {

        public override string ToString() => (ExcelTools.HaveLicense ? "Licensed, table margins (inches): " : "Free limited version, table margins (inches): ") + TableMarginInch.ToString("0.####", CultureInfo.InvariantCulture);

        public ExcelService(ExcelParams p)
        {
            p = p ?? new ExcelParams();
            ExcelTools.SetLicense(p);
            TableMarginInch = Math.Max(0.0, p.TableMarginInch);
        }

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
        static readonly Object InitLock = new object();

        /// <summary>
        /// True if SetLicense have been called
        /// </summary>
        public static bool DidInit { get; private set; }

        /// <summary>
        /// True if a true license was used, false if a free limited license is used
        /// </summary>
        public static bool HaveLicense { get; private set; }


        /// <summary>
        /// Set the license from a file or the actual api key
        /// </summary>
        /// <param name="fileOrApiKey">Filename or api key</param>
        /// <param name="isFile">True it the first parameter is a file, else false</param>
        public static void SetLicense(String fileOrApiKey = @"$(KeyFolder)\GemBox.Spreadsheet.txt", bool isFile = true)
            => SetLicense(new ApiKeyParams
            {
                CredFile = isFile ? fileOrApiKey : null,
                ApiKey = isFile ? null : fileOrApiKey,
            });


        /// <summary>
        /// Set the livense to the free limited license
        /// </summary>
        public static void SetFreeLicense()
            => SetLicense((ApiKeyParams)null);

        /// <summary>
        /// Set the license, have to be called once before using GemBox
        /// </summary>
        /// <param name="p">Paramaters, if null the free limited license</param>
        public static void SetLicense(ApiKeyParams p)
        {
            if (DidInit)
                return;
            lock (InitLock)
            {
                if (DidInit)
                    return;
                var apiKey = p?.GetApiKey(false);
                if (string.IsNullOrEmpty(apiKey))
                    apiKey = "FREE-LIMITED-KEY";
                HaveLicense = !apiKey.FastEquals("FREE-LIMITED-KEY");
                SpreadsheetInfo.SetLicense(apiKey);
                DidInit = true;
            }
        }


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
