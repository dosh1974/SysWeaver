using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;


namespace SysWeaver.Data
{
    /// <summary>
    /// Export table as html
    /// </summary>
    public sealed class HtmlTableDataExporter : ITableDataExporter
    {


        public static readonly HtmlTableDataExporter Simple = new HtmlTableDataExporter("Simple HTML", "A basic HTML table", "Simple");

        public override string ToString() => Name;




        public HtmlTableDataExporter(String name, String desc, String folder)
            : this(name, desc,
                  folder + "." + folder + "Body",
                  folder + "." + folder + "Row",
                  folder + "." + folder + "Header",
                  folder + "." + folder + "Cell"
                  )
        {
        }

        public HtmlTableDataExporter(String name, String desc, String body, String row, String header, String cell)
        {
            Body = Create(body);
            Row = Create(row);
            Header = Create(header);
            Cell = Create(cell);
            Name = name;
            Desc = desc;
        }



        static TextTemplate Create(String name)
        {
            var asm = typeof(HtmlTableDataExporter).Assembly;
            var mem = asm.GetUncompressedResourceData(name + ".html");
            var s = mem.Span;
            if (s.Length > 2)
                if ((s[0] == 0xef) && (s[1] == 0xbb) && (s[2] == 0xbf))
                    s = s[3..];
            var t = Encoding.UTF8.GetString(s);
            return new TextTemplate(t.TrimEnd() + "\n", "${", "}");
        }

        readonly TextTemplate Body;
        readonly TextTemplate Row;
        readonly TextTemplate Header;
        readonly TextTemplate Cell;

        
        public String Name { get; init; }
        public String Desc { get; init; }

        public String Icon => "IconFileHtml";

        public double Order { get; init; }

        public bool RequireUser => false;


        public static readonly IReadOnlyDictionary<String, String> Classes = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { typeof(Single).FullName, "m n" },
            { typeof(Double).FullName, "m n" },
            { typeof(Decimal).FullName, "m n" },

            { typeof(SByte).FullName, "m n" },
            { typeof(Int16).FullName, "m n" },
            { typeof(Int32).FullName, "m n" },
            { typeof(Int64).FullName, "m n" },

            { typeof(Byte).FullName, "m n" },
            { typeof(UInt16).FullName, "m n" },
            { typeof(UInt32).FullName, "m n" },
            { typeof(UInt64).FullName, "m n" },

            { typeof(DateTime).FullName, "m t" },
            { typeof(TimeSpan).FullName, "m t" },
            { typeof(DateOnly).FullName, "m t" },
            { typeof(DateTimeOffset).FullName, "m t" },
            { typeof(TimeOnly).FullName, "m t" },

            { typeof(Guid).FullName, "m" },
            { typeof(Boolean).FullName, "b" },
        }.Freeze();



        static String FormatUrl(TableDataExporterTools.Formatter f, Object value, Object nextValue, TableDataColumn col)
        {
            if (value == null)
                return "";
            // Url;{0};{1}/README.md;Click to open "{3}".
            var valueText = f(value, nextValue, col);
            var t = col.Format.Split(';');
            var text = String.Format(TableDataExporterTools.GetIndexed(t, 1, "{0}"), valueText, nextValue);
            var link = String.Format(TableDataExporterTools.GetIndexed(t, 2, "{2}"), value, nextValue, text);
            if (String.IsNullOrEmpty(link))
                return text;
            switch (link[0])
            {
                case '*':
                case '^':
                case '-':
                    if (link.IndexOf("://") < 0)
                        return text;
                    link = link.Substring(1);
                    break;
                case '+':
                    link = link.Substring(1);
                    break;
            }
            var title = String.Format(TableDataExporterTools.GetIndexed(t, 3, "Click to open \"{3}\"."), value, nextValue, text, link);
            if (!String.IsNullOrEmpty(title))
                return String.Concat((Char)1,
                    "<a href=\"",
                    HttpUtility.HtmlAttributeEncode(link),
                    "\" title=\"",
                    HttpUtility.HtmlAttributeEncode(title),
                    "\">",
                    HttpUtility.HtmlEncode(text),
                    "</a>"
                    );
            return String.Concat((Char)1,
                "<a href=\"",
                HttpUtility.HtmlAttributeEncode(link),
                "\">",
                HttpUtility.HtmlEncode(text),
                "</a>"
                );
        }

        static readonly IReadOnlyDictionary<String, TableDataExporterTools.SpecialFormatter> Formatters = new Dictionary<String, TableDataExporterTools.SpecialFormatter>(StringComparer.Ordinal)
        {
            { "Url", FormatUrl }

        }.Freeze();

        public Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TableDataExportOptions options = null)
        {
            options = options ?? new TableDataExportOptions();
            StringBuilder rowsBuilder = new StringBuilder();
            List<ValueTuple<TableDataExporterTools.Formatter, bool>> colToStrings = new();
            List<String> classes = new List<string>();

            HashSet<int> hide = new HashSet<int>();
            Dictionary<String, String> vals = new Dictionary<string, string>(StringComparer.Ordinal);
            var dc = Classes;
            var cols = tableData.Cols;
            var headers = !options.NoHeaders;
            if (cols != null)
            {
                var coll = cols.Length;
                var rowBuilder = new StringBuilder();
                for (int i = 0; i < coll; ++i)
                {
                    var col = cols[i];
                    var colFmt = TableDataExporterTools.Get(col.Type, col.Format, Formatters);
                    colToStrings.Add(colFmt);
                    dc.TryGetValue(col.Type, out var cl);
                    classes.Add(cl ?? "");
                    if ((col.Props & TableDataColumnProps.Hide) != 0)
                    {
                        hide.Add(i);
                        continue;
                    }
                    if (headers)
                    {
                        vals["Title"] = col.Desc;
                        vals["Text"] = col.Title.Replace(' ', (Char)0xa0);
                        vals["Class"] = (colFmt.Item2 ? "r " : "") + (cl ?? "").Replace("m", "").Trim();
                        rowBuilder.Append(Header.Get(vals));
                    }
                }
                if (headers)
                {
                    vals["Cells"] = rowBuilder.ToString().TrimEnd();
                    rowsBuilder.Append(Row.Get(vals));
                }
            }

            var colMax = colToStrings.Count;
            var rows = tableData.Rows;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var t = row.Values;
                    if (t != null)
                    {
                        var rowBuilder = new StringBuilder();
                        var tl = t.Length;
                        if (tl > 0)
                        {
                            for (int x = 0; x < tl; ++x)
                            {
                                if (hide.Contains(x))
                                    continue;
                                var ni = x + 1;
                                var value = t[x];
                                var nextValue = ni < tl ? t[ni] : null;
                                var (fmt, rightAlign) = x < colMax ? colToStrings[x] : (null, false);
                                if (fmt == null)
                                    (fmt, rightAlign) = TableDataExporterTools.GetDefault(value);
                                var cl = x < colMax ? classes[x] : "";
                                var valueText = fmt(value, nextValue, cols == null ? null : cols[x]);
                                var isFormatted = (!String.IsNullOrEmpty(valueText)) && (valueText[0] == 1);
                                vals["Title"] = value?.ToString() ?? "";
                                vals["Text"] = isFormatted ? "" : (valueText ?? "");
                                vals["TextFmt"] = isFormatted ? valueText.Substring(1) : "";
                                vals["Class"] = (rightAlign ? "r " : "") + cl ?? "";
                                rowBuilder.Append(Cell.Get(vals));
                            }
                            vals["Cells"] = rowBuilder.ToString().TrimEnd();
                            rowsBuilder.Append(Row.Get(vals));
                        }
                    }
                }
            }
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            vals["Title"] = tableData.Title ?? name;
            vals["Rows"] = rowsBuilder.ToString().TrimEnd();
            var text = Body.Get(vals);
            return Task.FromResult(new MemoryFile(name + ".html", Mimes.HtmlText, Encoding.UTF8.GetBytes(text)));
        }
    }

}
