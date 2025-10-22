using GemBox.Spreadsheet;
using GemBox.Spreadsheet.Tables;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;


namespace SysWeaver.Excel
{
    /// <summary>
    /// Export table to Excel
    /// </summary>
    public sealed class ExcelTableDataExporter : ITableDataExporter
    {

        public override string ToString() => Name;

        public static readonly ExcelTableDataExporter Xlsx = new ExcelTableDataExporter(false, "Excel Workbook", "An Excel workbook file", "IconFileXlsx", 0);
        public static readonly ExcelTableDataExporter Pdf = new ExcelTableDataExporter(true, "PDF document", "A Portable Document File", "IconFilePdf", 1);

        ExcelTableDataExporter(bool pdf, String name, String desc, String icon, double order)
        {
            IsPdf = pdf;
            Name = name;
            Desc = desc;
            Icon = icon;
            Order = order;
        }

        public readonly bool IsPdf;

        public String Name { get; init; }

        public String Desc { get; init; }

        public String Icon { get; init; }
        public double Order { get; init; }

        public bool RequireUser => false;


        public Task<MemoryFile> Export(BaseTableData tableData, Object context, TabelDataExportOptions options = null)
        {
            options = options ?? new TabelDataExportOptions();
            var name = String.IsNullOrEmpty(options.Filename) ? (String.IsNullOrEmpty(tableData.Title) ? "Table" : tableData.Title) : options.Filename;
            var isPdf = IsPdf;

            ExcelFile file = new ExcelFile();
            ExcelWorksheet s = file.Worksheets.Add(name);
            int rowOffset = isPdf ? 0 : 1;
            int colOffset = isPdf ? 0 : 1;
            int row = rowOffset;
            ExcelRow r = s.Rows[row];
            var cols = tableData.Cols;
            bool haveCols = (cols != null) && (cols.Length > 0);
            int colCount = cols?.Length ?? 0;
            var fmts = new ExcelTableDataFormat.FormatCellDelegate[colCount];
            bool headers = (!options.NoHeaders) && haveCols;
            if (haveCols)
            {
                var cc = cols.Length;
                int colIndex = colOffset;
                for (int i = 0; i < cc; ++i)
                {
                    var col = cols[i];
                    if ((col.Props & TableDataColumnProps.Hide) != 0)
                        continue;
                    var colType = TypeFinder.Get(col.Type);
                    var fmt = ExcelTableDataFormat.GetCellFormatter(colType, col.Format);
                    fmts[i] = fmt;
                    if (fmt == null)
                        continue;
                    if (headers)
                    {
                        var c = r.Cells[colIndex];
                        c.Style.Font.Name = "Verdana";
                        c.Style.Font.Weight = ExcelFont.BoldWeight;
                        c.Style.Font.Size = 10 * 20;
                        c.Style.Borders.SetBorders(MultipleBorders.All, SpreadsheetColor.FromName(ColorName.Accent1Lighter40Pct), LineStyle.Thin);
                        c.SetValue(col.Title);
                        ++colIndex;
                    }
                }
                if (headers)
                    ++row;
            }
            int rowCount = 0;
            var rows = tableData.Rows;
            int maxCol = 0;
            if (rows != null)
            {
                rowCount = rows.Length;
                foreach (var sr in rows)
                {
                    r = s.Rows[row];
                    var vals = sr.Values;
                    var vl = vals.Length;
                    int colIndex = colOffset;
                    for (int i = 0; i < vl; ++i)
                    {
                        var v = vals[i];
                        var c = r.Cells[colIndex];
                        c.Style.Font.Name = "Verdana";
                        c.Style.Borders.SetBorders(MultipleBorders.All, SpreadsheetColor.FromName(ColorName.Accent1Lighter60Pct), LineStyle.Thin);
                        c.Style.VerticalAlignment = VerticalAlignmentStyle.Top;
                        if (i < colCount)
                        {
                            var fmt = fmts[i];
                            if (fmt == null)
                                continue;
                            var ni = i + 1;
                            var next = ni < vl ? vals[ni] : null;
                            fmt(c, v, next);
                        }
                        else
                        {
                            c.Value = v;
                        }
                        if (isPdf)
                        {
                            ExcelTableDataFormat.NormalizeDuration(c);
                            if ((row & 1) != 0)
                                c.Style.FillPattern.SetSolid(SpreadsheetColor.FromName(ColorName.Accent1Lighter80Pct));
                        }
                        ++colIndex;
                    }
                    if (colIndex > maxCol)
                        maxCol = colIndex;
                    ++row;
                }
            }
            if (headers)
                rowCount += 1;
            var table = s.Tables.Add("DataTables", s.Cells.GetSubrangeAbsolute(rowOffset, colOffset, rowOffset + rowCount - 1, maxCol - 1), headers);
            table.BuiltInStyle = BuiltInTableStyleName.TableStyleMedium2;
            table.StyleOptions |= TableStyleOptions.BandedRows;

            var ctc = Thread.CurrentThread.CurrentCulture;
            var ctuc = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                var start = s.Rows[rowOffset];
                var end = s.Rows[rowOffset + rowCount + 1];
                var scale = isPdf ? 1.01 : 1.10;
                for (int i = colOffset; i < maxCol; ++i)
                {
                    var c = s.Columns[i];
                    c.AutoFit(scale, start, end);
                }

                ExcelService.PageSetUp(s, ExcelService.TableMarginInch, options.Portrait);

                using var ms = new MemoryStream();
                if (isPdf)
                {
                    var opt = new PdfSaveOptions
                    {
                    };
                    file.Save(ms, opt);
                    return Task.FromResult(new MemoryFile(name + ".pdf", "application/pdf", ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length)));
                }
                else
                {
                    var opt = new XlsxSaveOptions
                    {
                        Type = XlsxType.Xlsx,
                    };
                    file.Save(ms, opt);
                    return Task.FromResult(new MemoryFile(name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length)));
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = ctuc;
                Thread.CurrentThread.CurrentCulture = ctc;
            }
        }
    }

}
