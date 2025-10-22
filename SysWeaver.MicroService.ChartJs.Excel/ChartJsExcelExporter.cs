
using GemBox.Spreadsheet;
using GemBox.Spreadsheet.Charts;
using GemBox.Spreadsheet.Drawing;
using GemBox.Spreadsheet.Tables;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Excel;
using SysWeaver.MicroService;

namespace SysWeaver.Chart
{
    public sealed class ChartJsExcelExporter : IChartExporter
    {
        public static readonly ChartJsExcelExporter Instance = new ChartJsExcelExporter();

        ChartJsExcelExporter()
        {
        }

        public string Name => "Excel Workbook";

        public string Desc => "Save the chart as an Excel workbook file";

        public string Icon => "IconFileXlsx";

        public double Order => -2;

        public ChartExportInputTypes InputType => ChartExportInputTypes.Data;

        public bool RequireUser => false;


        static String ColumnName(int index)
        {
            var range = 'Z' - 'A' + 1;
            String t = "";
            for (; ; )
            {
                t += (Char)('A' + (index % range));
                index /= range;
                if (index <= 0)
                    return t;
            }
        }

        delegate ExcelChart CreateChartDelegate(ref bool rotateDataLabels, ExcelWorksheet chartPage, ChartJsConfig data, String font, String textFormat);

        static void SetAxisTitle(ChartAxis axis, ChartJsScaleOptions d, String font, String textFormat)
    {
            axis.NumberFormat = textFormat;
            var t = d?.title;
            if (t == null)
                return;
            if (t.text == null)
                return;
            var tt = String.Join('\n', t.text);
            if (tt.Length <= 0)
                return;
            axis.Title.Text = tt;
            axis.Title.TextFormat.Font = font;
            axis.Title.TextFormat.Size = Length.From(10, LengthUnit.Point);
        }

        static ExcelChart BarChart(ref bool rotateDataLabels, ExcelWorksheet chartPage, ChartJsConfig data, String font, String textFormat)
        {
            if (data.options?.indexAxis == "y")
            {
                var c = chartPage.Charts.Add<BarChart>(ChartGrouping.Clustered, "A1", "A1");
                var a = c.Axes.Horizontal;
                SetAxisTitle(a, data.options?.scales?.x, font, textFormat);
                a.Title.Direction = ChartTitleDirection.Horizontal;
                a.LabelsPosition = AxisLabelsPosition.High;
                c.Axes.Vertical.ReverseOrder = true;
                c.SeriesGapWidth = 0.25;
                return c;
            }
            else
            {
                var c = chartPage.Charts.Add<ColumnChart>(ChartGrouping.Clustered, "A1", "A1");
                var a = c.Axes.Vertical;
                SetAxisTitle(a, data.options?.scales?.y, font, textFormat);
                c.SeriesGapWidth = 0.25;
                return c;
            }

        }

        static ExcelChart LineChart(ref bool rotateDataLabels, ExcelWorksheet chartPage, ChartJsConfig data, String font, String textFormat)
        {
            if (data.options?.indexAxis == "y")
            {
                var c = chartPage.Charts.Add<LineChart>(ChartGrouping.Clustered, "A1", "A1");
                var a = c.Axes.Vertical;
                SetAxisTitle(a, data.options?.scales?.y, font, textFormat);
                return c;
            }
            else
            {
                var c = chartPage.Charts.Add<LineChart>(ChartGrouping.Clustered, "A1", "A1");
                var a = c.Axes.Vertical;
                SetAxisTitle(a, data.options?.scales?.y, font, textFormat);
                return c;
            }
        }

        static ExcelChart PieChart(ref bool rotateDataLabels, ExcelWorksheet chartPage, ChartJsConfig data, String font, String textFormat)
        {
            return chartPage.Charts.Add<PieChart>(ChartGrouping.Clustered, "A1", "A1");
        }

        static ExcelChart DoughnutChart(ref bool rotateDataLabels, ExcelWorksheet chartPage, ChartJsConfig data, String font, String textFormat)
        {
            var c = chartPage.Charts.Add<DoughnutChart>(ChartGrouping.Clustered, "A1", "A1");
            //c.HoleSize = 25;
            return c;
        }

        static readonly IReadOnlyDictionary<String, CreateChartDelegate> ChartTypes = new Dictionary<string, CreateChartDelegate>(StringComparer.Ordinal)
        {
            { "bar",  BarChart },
            { "line",  LineChart },
            { "pie",  PieChart },
            { "doughnut",  DoughnutChart },
        }.Freeze();

        static readonly IReadOnlySet<String> NoAxisTypes = ReadOnlyData.Set(StringComparer.Ordinal,
           "pie",
           "doughnut",
           "polarArea"
        );



        public Task<MemoryFile> Export(object data, object context, ChartExportOptions options = null)
        {
            options = options ?? new ChartExportOptions();
            var d = data as ChartJsConfig;
            if (d == null)
               throw new Exception("Expected data of the type " + typeof(ChartJsConfig).FullName.ToQuoted());

            var portrait = options.SwapLandscapePortrait;
            var name = String.IsNullOrEmpty(options.Filename) ? "Chart" : options.Filename;

            var font = "Verdana";
            ExcelFile file = new ExcelFile();
            ExcelWorksheet chartPage = file.Worksheets.Add("Chart");
            ExcelWorksheet dataPage = file.Worksheets.Add("Data");
            var cols = d.data.datasets;
            var cc = cols.Length;
            int row = 0;
            {
                ExcelRow r = dataPage.Rows[row];
                ++row;
                var c = r.Cells[0];
                c.SetValue("Labels");
                c.Style.Font.Name = font;
                c.Style.Font.Weight = ExcelFont.BoldWeight;
                c.Style.Font.Size = 10 * 20;
                c.Style.Borders.SetBorders(MultipleBorders.All, SpreadsheetColor.FromName(ColorName.Accent1Lighter40Pct), LineStyle.Thin);
            }
            foreach (var x in d.data.labels)
            {
                ExcelRow r = dataPage.Rows[row];
                ++row;
                var c = r.Cells[0];
                c.SetValue(x);
                c.Style.Font.Name = font;
                c.Style.Font.Weight = ExcelFont.BoldWeight;
                c.Style.Font.Size = 10 * 20;
                c.Style.Borders.SetBorders(MultipleBorders.All, SpreadsheetColor.FromName(ColorName.Accent1Lighter40Pct), LineStyle.Thin);
            }
            var rowCount = row;
            String numberFormat = ExcelTableDataFormat.TypeInteger;
            int decCount = 0;
            for (int i = 0; i < cc; ++i)
            {
                row = 0;
                ExcelRow r = dataPage.Rows[row];
                var col = cols[i];
                var c = r.Cells[i + 1];
                c.SetValue(col.label);
                c.Style.Font.Name = font;
                c.Style.Font.Weight = ExcelFont.BoldWeight;
                c.Style.Font.Size = 8 * 20;
                c.Style.Borders.SetBorders(MultipleBorders.All, SpreadsheetColor.FromName(ColorName.Accent1Lighter40Pct), LineStyle.Thin);
                foreach (var x in col.data)
                {
                    var xs = x.ToString(CultureInfo.InvariantCulture);
                    var li = xs.IndexOf('.');
                    var dl = li < 0 ? 0 : (xs.Length - li - 1);
                    if (dl > decCount)
                        decCount = dl;
                    ++row;
                    c = dataPage.Rows[row].Cells[i + 1];
                    c.SetValue(x);
                    c.Style.NumberFormat = numberFormat;
                    c.Style.Font.Name = font;
                    c.Style.Borders.SetBorders(MultipleBorders.All, SpreadsheetColor.FromName(ColorName.Accent1Lighter60Pct), LineStyle.Thin);
                    c.Style.VerticalAlignment = VerticalAlignmentStyle.Top;
                }
            }
            if (decCount > 0)
            {
                if (decCount > 2)
                    decCount = 2;
                var t = "0." + new string('0', decCount);
                numberFormat = ExcelTableDataFormat.TypeDecimal.Replace("0.00", t);
                for (int i = 0; i < cc; ++i)
                {
                    row = 0;
                    ExcelRow r = dataPage.Rows[row];
                    var col = cols[i];
                    var c = r.Cells[i + 1];
                    row = 0;
                    foreach (var x in col.data)
                    {
                        ++row;
                        c = dataPage.Rows[row].Cells[i + 1];
                        c.Style.NumberFormat = t;
                    }
                }
            }
            var table = dataPage.Tables.Add("DataTable", dataPage.Cells.GetSubrangeAbsolute(0, 0, rowCount - 1, cc), true);
            table.BuiltInStyle = BuiltInTableStyleName.TableStyleMedium2;
            table.StyleOptions |= TableStyleOptions.BandedRows;
            
            var start = dataPage.Rows[0];
            var end = dataPage.Rows[rowCount];
            for (int i = 0; i <= cc; ++i)
            {
                var c = dataPage.Columns[i];
                c.AutoFit(1.1, start, end);
            }
            ExcelService.PageSetUp(chartPage, ExcelService.TableMarginInch, portrait, true);
            if (!ChartTypes.TryGetValue(d.type, out var createFn))
                createFn = BarChart;
            bool rotateDataLabels = false;
            var chart = createFn(ref rotateDataLabels , chartPage, d, font, numberFormat);
            chart.Title.Text = name;
            chart.Title.TextFormat.Font = font;
            chart.Title.TextFormat.Size = Length.From(12, LengthUnit.Point);
            if (NoAxisTypes.Contains(d.type))
            {
                chart.Legend.IsVisible = true;
                chart.Legend.Position = portrait ? ChartLegendPosition.Top : ChartLegendPosition.Left;
                chart.DataLabels.Show();
            }
            else
            {
                chart.Legend.Position = ChartLegendPosition.Top;
                chart.Legend.IsVisible = d.data.datasets.Length > 1;
                chart.DataLabels.Show(createFn == BarChart ? DataLabelPosition.InsideEnd : DataLabelPosition.Top);
            }
            chart.DataLabels.Fill.SetSolid(DrawingColor.FromRgb(255, 255, 255, 0.4));
            chart.DataLabels.TextFormat.Size = Length.From(8, LengthUnit.Point);
            chart.DataLabels.TextFormat.Font = font;
            chart.DataLabels.TextFormat.Fill.SetSolid(DrawingColor.FromRgb(0, 0, 0, 0.4));
            chart.DataLabels.NumberFormat = numberFormat;

            var width = portrait ? 210 : 297;
            var height = portrait ? 297 : 210;
            chartPage.Columns[0].SetWidth(width - ExcelService.TableMarginInch * ExcelService.Convert_inch_to_mm * 2 - 2, LengthUnit.Millimeter);
            chartPage.Rows[0].SetHeight(height - ExcelService.TableMarginInch * ExcelService.Convert_inch_to_mm * 2 - 2, LengthUnit.Millimeter);

            var range = dataPage.Cells.GetSubrangeAbsolute(0, 0, rowCount - 1, cc);
            chart.SelectData(range, true);
            const double pixelScale = 0.5;
            for (int i = 0; i < cc; ++i)
            {
                row = 0;
                var col = cols[i];
                var ds = chart.Series[i];
                
                var bw = col.borderWidth ?? -1;
                if (bw > 0)
                    ds.Outline.Width = Length.From(bw * pixelScale, LengthUnit.Pixel);
                var fillColors = col.backgroundColor;
                var outlineColors = col.borderColor;
                var fl = (fillColors?.Length ?? 0) - 1;
                var ol = (outlineColors?.Length ?? 0) -1;
                var dps = ds.DataPoints;
                var dpl = dps.Count;
                for (int di = 0; di < dpl; ++ di)
                {
                    var dp = dps[di];
                    if (ExcelTools.TryGetWebColor(fl > 0 ? fillColors[di <= fl ? di : fl] : null, out var fc))
                        dp.Fill.SetSolid(fc);
                    if (ExcelTools.TryGetWebColor(ol > 0 ? outlineColors[di <= ol ? di : ol] : null, out var oc))
                        dp.Outline.Fill.SetSolid(oc);
                }
            }

            ExcelService.PageSetUp(dataPage, ExcelService.TableMarginInch, portrait);

            var opt = new XlsxSaveOptions
            {
                Type = XlsxType.Xlsx,
            };
            using var ms = new MemoryStream();
            file.Save(ms, opt);
            return Task.FromResult(new MemoryFile(name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length)));
        }
    }


}
