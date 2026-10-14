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
    /// Header cells use the column title (the name if there is no title), cells beyond the column definitions are formatted using the runtime type of the value.
    /// Hidden columns (<see cref="TableDataColumnProps.Hide"/>) are omitted. Columns with a "Url" format are rendered as links (unless the link uses an unsafe scheme, see <see cref="TableDataExporterTools.IsSafeLink(string)"/>).
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
            //  Stream the rows / cells into the document when the result is the same (the templates only use the variables that are known at that point)
            if (OnlyUses(Body, "Title", "Rows"))
                (BodyPre, BodyPost) = Split(Body, "Rows");
            if (OnlyUses(Row, "Cells") && (!Uses(Header, "Cells")) && (!Uses(Cell, "Cells")) && (!Uses(Body, "Cells")))
                (RowPre, RowPost) = Split(Row, "Cells");
        }

        static bool Uses(TextTemplate t, String name)
        {
            foreach (var x in t.Vars)
                if (String.Equals(x, name, StringComparison.Ordinal))
                    return true;
            return false;
        }

        static bool OnlyUses(TextTemplate t, String a, String b = null)
        {
            foreach (var x in t.Vars)
                if ((!String.Equals(x, a, StringComparison.Ordinal)) && (!String.Equals(x, b, StringComparison.Ordinal)))
                    return false;
            return true;
        }

        /// <summary>
        /// Split a template at the only occurrence of an untransformed variable.
        /// </summary>
        /// <param name="t">The template</param>
        /// <param name="name">The name of the variable</param>
        /// <returns>The templates before and after the variable, (null, null) if the variable isn't used exactly once (untransformed) or if the parts don't render as the template</returns>
        static (TextTemplate, TextTemplate) Split(TextTemplate t, String name)
        {
            var text = t.Template;
            var token = String.Concat("${", name, "}");
            var i = text.IndexOf(token, StringComparison.Ordinal);
            if ((i < 0) || (text.IndexOf(token, i + 1, StringComparison.Ordinal) >= 0))
                return (null, null);
            var pre = new TextTemplate(text.Substring(0, i), "${", "}");
            var post = new TextTemplate(text.Substring(i + token.Length), "${", "}");
            if (Uses(pre, name) || Uses(post, name))
                return (null, null);
            //  Every variable rendered as a unique text: the parts must render the same text (the variable is a token of the template)
            static String Marker(String v) => String.Concat("\u0001", v, "\u0002");
            if (!String.Equals(t.Get(Marker), String.Concat(pre.Get(Marker), Marker(name), post.Get(Marker)), StringComparison.Ordinal))
                return (null, null);
            return (pre, post);
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

        /// <summary>
        /// The <see cref="Body"/> before / after the rows (null if the rows can't be streamed into the document)
        /// </summary>
        readonly TextTemplate BodyPre, BodyPost;

        /// <summary>
        /// The <see cref="Row"/> before / after the cells (null if the cells can't be streamed into the document)
        /// </summary>
        readonly TextTemplate RowPre, RowPost;

        /// <summary>
        /// The template variables (the values of a dictionary that is reused for all templates, default = not set).
        /// The values are memory: a pre-formatted cell is written without its leading (Char)1, a cell title can be formatted into <see cref="TitleBuffer"/> (no string per cell).
        /// </summary>
        sealed class Vars
        {
            public ReadOnlyMemory<Char> Title;
            public ReadOnlyMemory<Char> Text;
            public ReadOnlyMemory<Char> TextFmt;
            public ReadOnlyMemory<Char> Class;
            public ReadOnlyMemory<Char> Cells;
            public ReadOnlyMemory<Char> Rows;

            /// <summary>
            /// A reused buffer for the cell titles that are formatted without a string (see <see cref="SetTitle(object)"/>)
            /// </summary>
            Char[] TitleBuffer = new Char[64];

            /// <summary>
            /// The largest title buffer (a value that doesn't fit is converted with ToString)
            /// </summary>
            const int MaxTitleBuffer = 4096;

            /// <summary>
            /// Set the <see cref="Title"/> to the text of value?.ToString() ?? "".
            /// The CoreLib types whose ToString() is the same text as TryFormat without a format and provider (the current culture) are formatted into the reused buffer.
            /// </summary>
            /// <param name="value">The cell value</param>
            public void SetTitle(Object value)
            {
                if (value is String s)
                {
                    Title = s.AsMemory();
                    return;
                }
                if (value is SByte or Int16 or Int32 or Int64 or Byte or UInt16 or UInt32 or UInt64 or Single or Double or Decimal
                    or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid)
                {
                    var f = (ISpanFormattable)value;
                    var b = TitleBuffer;
                    while (b.Length <= MaxTitleBuffer)
                    {
                        if (f.TryFormat(b, out var written, default, null))
                        {
                            Title = new ReadOnlyMemory<Char>(b, 0, written);
                            return;
                        }
                        b = new Char[b.Length * 2];
                        TitleBuffer = b;
                    }
                }
                Title = (value?.ToString() ?? "").AsMemory();
            }

            public static readonly Func<Vars, String, ReadOnlyMemory<Char>> Get = static (v, name) => name switch
            {
                "Title" => v.Title,
                "Text" => v.Text,
                "TextFmt" => v.TextFmt,
                "Class" => v.Class,
                "Cells" => v.Cells,
                "Rows" => v.Rows,
                _ => default,
            };
        }

        /// <summary>
        /// Remove the trailing white space (as <see cref="String.TrimEnd()"/>) from the text appended after <paramref name="start"/>
        /// </summary>
        static void TrimEnd(StringBuilder sb, int start)
        {
            var l = sb.Length;
            while ((l > start) && Char.IsWhiteSpace(sb[l - 1]))
                --l;
            sb.Length = l;
        }

        /// <summary>
        /// Start a template that contains the text appended after this call (the cells of a row or the rows of the body)
        /// </summary>
        /// <returns>The start of the inner text</returns>
        static int Begin(StringBuilder sb, TextTemplate pre, Vars vars)
        {
            pre?.AppendMemoryValuesTo(sb, vars, Vars.Get);
            return sb.Length;
        }

        /// <summary>
        /// End a template that contains the text appended after <paramref name="start"/> (trimmed at the end)
        /// </summary>
        /// <param name="sb">The builder</param>
        /// <param name="start">The start of the inner text (returned by <see cref="Begin(StringBuilder, TextTemplate, Vars)"/>)</param>
        /// <param name="template">The whole template</param>
        /// <param name="post">The template after the inner text, null if the inner text isn't streamed (the whole template is rendered with the inner text as a variable)</param>
        /// <param name="vars">The variables</param>
        /// <param name="isRows">True for the rows of the body, false for the cells of a row</param>
        static void End(StringBuilder sb, int start, TextTemplate template, TextTemplate post, Vars vars, bool isRows)
        {
            TrimEnd(sb, start);
            if (post != null)
            {
                post.AppendMemoryValuesTo(sb, vars, Vars.Get);
                return;
            }
            var inner = sb.ToString(start, sb.Length - start).AsMemory();
            sb.Length = start;
            if (isRows)
                vars.Rows = inner;
            else
                vars.Cells = inner;
            template.AppendMemoryValuesTo(sb, vars, Vars.Get);
        }

        
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
        /// CSS classes to apply to cells, keyed on the full type name of the column (nullable versions of the types use the same classes)
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

            { typeof(Single?).FullName, "m n" },
            { typeof(Double?).FullName, "m n" },
            { typeof(Decimal?).FullName, "m n" },

            { typeof(SByte?).FullName, "m n" },
            { typeof(Int16?).FullName, "m n" },
            { typeof(Int32?).FullName, "m n" },
            { typeof(Int64?).FullName, "m n" },

            { typeof(Byte?).FullName, "m n" },
            { typeof(UInt16?).FullName, "m n" },
            { typeof(UInt32?).FullName, "m n" },
            { typeof(UInt64?).FullName, "m n" },

            { typeof(DateTime?).FullName, "m t" },
            { typeof(TimeSpan?).FullName, "m t" },
            { typeof(DateOnly?).FullName, "m t" },
            { typeof(DateTimeOffset?).FullName, "m t" },
            { typeof(TimeOnly?).FullName, "m t" },

            { typeof(Guid?).FullName, "m" },
            { typeof(Boolean?).FullName, "b" },
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
            if (!TableDataExporterTools.IsSafeLink(link))
                return text;
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
            //  The whole document is rendered into one builder: the rows and cells are appended between the parts of the body / row templates (when possible, else rendered as a variable)
            var sb = new StringBuilder(4096);
            var cols = tableData.Cols;
            var colMax = cols?.Length ?? 0;
            var colToStrings = new ValueTuple<TableDataExporterTools.Formatter, bool>[colMax];
            //  The cell classes of a column: [0] = left aligned, [1] = right aligned
            var classes = new String[colMax * 2];
            var hide = new bool[colMax];
            var vals = new Vars();
            var dc = Classes;
            var headers = !options.NoHeaders;
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            var bodyPost = BodyPost;
            var rowPre = RowPre;
            var rowPost = RowPost;
            Vars bodyVars = null;
            if (bodyPost != null)
                bodyVars = new Vars { Title = (tableData.Title ?? name).AsMemory() };
            var rowsStart = Begin(sb, BodyPre, bodyVars);
            if (cols != null)
            {
                var coll = cols.Length;
                var cellsStart = headers ? Begin(sb, rowPre, vals) : 0;
                for (int i = 0; i < coll; ++i)
                {
                    var col = cols[i];
                    var colFmt = TableDataExporterTools.Get(col.Type, col.Format, Formatters);
                    colToStrings[i] = colFmt;
                    dc.TryGetValue(col.Type ?? "", out var cl);
                    var wcl = ((col.Props & TableDataColumnProps.WordWrap) != 0) ? ("w " + cl) : (cl ?? "");
                    classes[i * 2] = wcl;
                    classes[i * 2 + 1] = "r " + wcl;
                    if ((col.Props & TableDataColumnProps.Hide) != 0)
                    {
                        hide[i] = true;
                        continue;
                    }
                    if (headers)
                    {
                        vals.Title = (col.Desc ?? "").AsMemory();
                        vals.Text = (col.Title ?? col.Name ?? "").Replace(' ', (Char)0xa0).AsMemory();
                        vals.Class = ((colFmt.Item2 ? "r " : "") + (cl ?? "").Replace("m", "").Trim()).AsMemory();
                        Header.AppendMemoryValuesTo(sb, vals, Vars.Get);
                    }
                }
                if (headers)
                    End(sb, cellsStart, Row, rowPost, vals, false);
            }

            var rows = tableData.Rows;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var t = row.Values;
                    if (t != null)
                    {
                        var tl = t.Length;
                        if (tl > 0)
                        {
                            var cellsStart = Begin(sb, rowPre, vals);
                            for (int x = 0; x < tl; ++x)
                            {
                                var ni = x + 1;
                                var value = t[x];
                                var nextValue = ni < tl ? t[ni] : null;
                                TableDataExporterTools.Formatter fmt;
                                bool rightAlign;
                                TableDataColumn col;
                                if (x < colMax)
                                {
                                    if (hide[x])
                                        continue;
                                    (fmt, rightAlign) = colToStrings[x];
                                    col = cols[x];
                                }
                                else
                                {
                                    fmt = null;
                                    rightAlign = false;
                                    col = null;
                                }
                                if (fmt == null)
                                    (fmt, rightAlign) = TableDataExporterTools.GetDefault(value);
                                var valueText = fmt(value, nextValue, col);
                                var isFormatted = (!String.IsNullOrEmpty(valueText)) && (valueText[0] == 1);
                                vals.SetTitle(value);
                                vals.Text = (isFormatted ? "" : (valueText ?? "")).AsMemory();
                                //  The markup without the leading (Char)1 (no substring)
                                vals.TextFmt = isFormatted ? valueText.AsMemory(1) : "".AsMemory();
                                vals.Class = (x < colMax ? classes[x * 2 + (rightAlign ? 1 : 0)] : (rightAlign ? "r " : "")).AsMemory();
                                Cell.AppendMemoryValuesTo(sb, vals, Vars.Get);
                            }
                            End(sb, cellsStart, Row, rowPost, vals, false);
                        }
                    }
                }
            }
            if (bodyVars == null)
            {
                bodyVars = vals;
                vals.Title = (tableData.Title ?? name).AsMemory();
            }
            End(sb, rowsStart, Body, bodyPost, bodyVars, true);
            return Task.FromResult(new MemoryFile(name + ".html", Mimes.HtmlText, TableDataExporterTools.GetUtf8Bytes(sb)));
        }
    }

}
