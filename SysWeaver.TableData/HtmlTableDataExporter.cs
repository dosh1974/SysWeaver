using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;


namespace SysWeaver.Data
{
    /// <summary>
    /// Export table data as a stand alone UTF-8 HTML file, built from four embedded <see cref="TextTemplate"/> resources (body, row, header cell and cell).
    /// </summary>
    /// <remarks>
    /// Templates use <c>${Var}</c> variables (with the usual <see cref="TextTemplate"/> transforms, ex: <c>${#Text}</c> for HTML encoding).
    /// Variables: body: Title, Rows; row: Cells; header: Title (column description), Text, Class; cell: Title (raw value), Text (encoded by the template), TextFmt (pre-formatted markup), Class.
    /// Hidden columns (<see cref="TableDataColumnProps.Hide"/>) are omitted. Columns with a "Url" format are rendered as links.
    /// The exporter is immutable and thread safe.
    /// </remarks>
    public sealed class HtmlTableDataExporter : ITableDataExporter
    {


        /// <summary>
        /// A basic HTML table, using the embedded "Simple" templates.
        /// </summary>
        public static readonly HtmlTableDataExporter Simple = new HtmlTableDataExporter("Simple HTML", "A basic HTML table", "Simple");

        /// <summary>
        /// Returns the <see cref="Name"/>.
        /// </summary>
        /// <returns>The name of the exporter.</returns>
        public override string ToString() => Name;




        /// <summary>
        /// Create an HTML exporter using the templates "[folder].[folder]Body.html", "[folder].[folder]Row.html",
        /// "[folder].[folder]Header.html" and "[folder].[folder]Cell.html" embedded in this assembly.
        /// </summary>
        /// <param name="name">The display name of the exporter.</param>
        /// <param name="desc">A description of the exporter.</param>
        /// <param name="folder">The resource folder (and file name prefix) of the templates.</param>
        public HtmlTableDataExporter(String name, String desc, String folder)
            : this(name, desc,
                  folder + "." + folder + "Body",
                  folder + "." + folder + "Row",
                  folder + "." + folder + "Header",
                  folder + "." + folder + "Cell"
                  )
        {
        }

        /// <summary>
        /// Create an HTML exporter using explicitly named templates embedded in this assembly (resource names without the ".html" extension).
        /// </summary>
        /// <param name="name">The display name of the exporter.</param>
        /// <param name="desc">A description of the exporter.</param>
        /// <param name="body">The template for the whole document.</param>
        /// <param name="row">The template for a row.</param>
        /// <param name="header">The template for a header cell.</param>
        /// <param name="cell">The template for a data cell.</param>
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

        
        /// <inheritdoc/>
        public String Name { get; init; }

        /// <inheritdoc/>
        public String Desc { get; init; }

        /// <inheritdoc/>
        public String Icon => "IconFileHtml";

        /// <inheritdoc/>
        public double Order { get; init; }

        /// <summary>
        /// Always false, HTML export doesn't require a signed in user.
        /// </summary>
        public bool RequireUser => false;


        /// <summary>
        /// CSS classes to apply to cells, keyed on the full type name of the column
        /// ("m" = monospace, "n" = number, "t" = time, "b" = boolean).
        /// </summary>
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

        /// <summary>
        /// Export the table as an HTML file (completes synchronously).
        /// </summary>
        /// <param name="tableData">The table to export, columns are optional (without columns no header row is written).</param>
        /// <param name="context">Not used.</param>
        /// <param name="options">Export options, <see cref="TableDataExportOptions.NoHeaders"/> and <see cref="TableDataExportOptions.Filename"/> are used.</param>
        /// <returns>A file named "[Filename].html" (default "Table.html") with the <see cref="Mimes.HtmlText"/> mime type.
        /// The document title is the table title, or the file name if the table has no title.</returns>
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
                                var col = (cols != null) && (x < cols.Length) ? cols[x] : null;
                                var valueText = fmt(value, nextValue, col);
                                var isFormatted = (!String.IsNullOrEmpty(valueText)) && (valueText[0] == 1);
                                var wordWrap = col != null && ((col.Props & TableDataColumnProps.WordWrap) != 0);
                                vals["Title"] = value?.ToString() ?? "";
                                vals["Text"] = isFormatted ? "" : (valueText ?? "");
                                vals["TextFmt"] = isFormatted ? valueText.Substring(1) : "";
                                vals["Class"] = (rightAlign ? "r " : "") + (wordWrap ? "w " : "") + cl ?? "";
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
