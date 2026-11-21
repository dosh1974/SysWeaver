using GemBox.Spreadsheet;
using GemBox.Spreadsheet.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using SysWeaver.Data;


namespace SysWeaver.Excel
{

    /// <summary>
    /// Service that sets up the Excel (GemBox.Spreadsheet) license and provides table data exporters for Excel (xlsx) and PDF
    /// </summary>
    public sealed class ExcelService : IHaveTableDataExporters
    {

        /// <inheritdoc />
        public override string ToString() => (ExcelTools.HaveLicense ? "Licensed, table margins (inches): " : "Free limited version, table margins (inches): ") + TableMarginInch.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// Create the service, sets the license (see <see cref="ExcelTools.SetLicense(ApiKeyParams, bool)"/>) and the static <see cref="TableMarginInch"/>
        /// </summary>
        /// <param name="p">Parameters, null to use the defaults (no key: throws, a license key or "FREE-LIMITED-KEY" must be configured)</param>
        public ExcelService(ExcelParams p)
        {
            p = p ?? new ExcelParams();
            ExcelTools.SetLicense(p);
            TableMarginInch = Math.Max(0.0, p.TableMarginInch);
        }

        /// <inheritdoc />
        public IReadOnlyList<ITableDataExporter> TableDataExporters =>
            [
                ExcelTableDataExporter.Xlsx,
                ExcelTableDataExporter.Pdf,
            ];


        /// <summary>
        /// Margin for table pages in inches
        /// </summary>
        public static double TableMarginInch { get; private set; } = 0.2;


        /// <summary>
        /// Multiply inches by this value to get millimeters
        /// </summary>
        public const double Convert_inch_to_mm = 25.4;

        /// <summary>
        /// Multiply millimeters by this value to get inches
        /// </summary>
        public const double Convert_mm_to_inch = 1.0 / Convert_inch_to_mm;


        /// <summary>
        /// Set up a worksheet for printing on A4 paper: no gridlines, headings or cell notes, fit to one page wide, horizontally centered
        /// </summary>
        /// <param name="s">The worksheet</param>
        /// <param name="marginInch">The margin (top, right, bottom and left) in inches, header and footer margins are zero</param>
        /// <param name="portrait">True for portrait, false for landscape</param>
        /// <param name="singlePage">True to also fit the worksheet height to one page and center it vertically</param>
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


    /// <summary>
    /// Excel (GemBox.Spreadsheet) helpers, license handling and color conversion
    /// </summary>
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
        /// <param name="mustExist">If p is non-null and there isn't a valid license, throw an exception, else use the free</param>
        public static void SetLicense(String fileOrApiKey = @"$(KeyFolder)/GemBox.Spreadsheet.txt", bool isFile = true, bool mustExist = true)
            => SetLicense(new ApiKeyParams
            {
                CredFile = isFile ? fileOrApiKey : null,
                ApiKey = isFile ? null : fileOrApiKey,
            }, mustExist);


        /// <summary>
        /// Set the license to the free limited license
        /// </summary>
        public static void SetFreeLicense()
            => SetLicense((ApiKeyParams)null, false);

        /// <summary>
        /// Set the license, have to be called once before using GemBox
        /// </summary>
        /// <param name="p">Parameters, if null the free limited license</param>
        /// <param name="mustExist">If p is non-null and there isn't a valid license, throw an exception, else use the free</param>
        public static void SetLicense(ApiKeyParams p, bool mustExist = true)
        {
            if (DidInit)
                return;
            lock (InitLock)
            {
                if (DidInit)
                    return;
                var apiKey = p?.GetApiKey(mustExist);
                if (string.IsNullOrEmpty(apiKey))
                    apiKey = "FREE-LIMITED-KEY";
                HaveLicense = !apiKey.FastEquals("FREE-LIMITED-KEY");
                SpreadsheetInfo.SetLicense(apiKey);
                DidInit = true;
            }
        }


        /// <summary>
        /// Convert a web color (name or hex value, see <see cref="HtmlColors"/>) to a GemBox color, a color with alpha less than 255 is converted to a transparent color
        /// </summary>
        /// <param name="webColor">The web color, may be null</param>
        /// <param name="col">The color, red if the conversion failed</param>
        /// <returns>True if the color was converted, false if <paramref name="webColor"/> is null or isn't a valid color</returns>
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
