using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;


namespace SysWeaver.Data
{

    /// <summary>
    /// Export table data as a UTF-8 Markdown (GitHub flavored) table, with padded columns for readability in plain text.
    /// </summary>
    /// <remarks>
    /// Values are Markdown escaped, line breaks in values are replaced with &lt;br&gt;.
    /// Numeric columns are right aligned, columns without <see cref="TableDataColumnProps.WordWrap"/> use non breaking spaces.
    /// Hidden columns (<see cref="TableDataColumnProps.Hide"/>) are omitted. Columns with a "Url" format are rendered as links (unless the link uses an unsafe scheme, see <see cref="TableDataExporterTools.IsSafeLink(string)"/>).
    /// The exporter is stateless and thread safe.
    /// </remarks>
    public sealed class MarkDownTableDataExporter : ITableDataExporter
    {


        /// <summary>
        /// The shared instance.
        /// </summary>
        public static readonly MarkDownTableDataExporter Instance = new MarkDownTableDataExporter();

        /// <summary>
        /// Returns the <see cref="Name"/>.
        /// </summary>
        /// <returns>The name of the exporter.</returns>
        public override string ToString() => Name;

        /// <summary>
        /// Create a Markdown exporter, prefer using the shared <see cref="Instance"/>.
        /// </summary>
        public MarkDownTableDataExporter()
        {
        }




        /// <inheritdoc/>
        public String Name => "Mark down";

        /// <inheritdoc/>
        public String Desc => "A mark down (MD) formatted text file";

        /// <inheritdoc/>
        public String Icon => "IconFileMD";

        /// <inheritdoc/>
        public double Order => 1000;

        /// <summary>
        /// Markdown emphasis wrapped around each column header (bold italic), not used when <see cref="TableDataExportOptions.NoHeaders"/> is set.
        /// </summary>
        public const String ColHeaderStyle = "***";

        /// <summary>
        /// Prefix of the line containing the table title (a level 3 heading), only written if the table has a title.
        /// </summary>
        public const String TitlePrefix = "### ";

        /// <summary>
        /// Always false, Markdown export doesn't require a signed in user.
        /// </summary>
        public bool RequireUser => false;

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
                return String.Concat((Char)1, '[', StringTools.EscapeMD(text, true), "](", StringTools.EscapeMD(link), " \"", StringTools.EscapeMD(title).Replace("\"", "\\\""), "\")");
            return String.Concat((Char)1, '[', StringTools.EscapeMD(text, true), "](", StringTools.EscapeMD(link), ')');
        }

        static readonly IReadOnlyDictionary<String, TableDataExporterTools.SpecialFormatter> Formatters = new Dictionary<String, TableDataExporterTools.SpecialFormatter>(StringComparer.Ordinal)
        {
            { "Url", FormatUrl }

        }.Freeze();


        /// <summary>
        /// Get the Markdown text for a table.
        /// </summary>
        /// <param name="tableData">The table to convert. Without columns, the column count is taken from the first row and the headers are numbered.</param>
        /// <param name="context">Not used.</param>
        /// <param name="options">Export options.
        /// <see cref="TableDataExportOptions.Custom"/> is used as a prefix for every line (ex: for indentation or quoting),
        /// <see cref="TableDataExportOptions.NoHeaders"/> only removes the header emphasis, a header line is always written (required by Markdown).</param>
        /// <returns>The Markdown text.</returns>
        /// <remarks>Every row must have at least as many values as there are columns.</remarks>
        public String GetMarkDownText(BaseTableData tableData, Object context = null, TableDataExportOptions options = null)
        {
            //  Measure and get data
            options = options ?? new TableDataExportOptions();
            String linePrefix = options?.Custom ?? "";
            List<ValueTuple<TableDataExporterTools.Formatter, bool>> colToStrings = new();
            HashSet<int> hide = new HashSet<int>();
            var cols = tableData.Cols;
            var hs = options.NoHeaders ? "" : ColHeaderStyle;
            List<String> colTitles = new List<string>();
            bool[] colNbsps;
            int coll;
            //  Get column titles, functions and son
            if (cols != null)
            {
                //  With meta data
                coll = cols.Length;
                colNbsps = new bool[coll];
                for (int i = 0; i < coll; ++i)
                {
                    var col = cols[i];
                    colToStrings.Add(TableDataExporterTools.Get(col.Type, col.Format, Formatters));
                    if ((col.Props & TableDataColumnProps.Hide) != 0)
                    {
                        hide.Add(i);
                        continue;
                    }
                    var title = col.Title;
                    if (String.IsNullOrEmpty(title))
                        title = (colTitles.Count + 1).ToString();
                    colTitles.Add(title);
                    colNbsps[i] = (col.Props & TableDataColumnProps.WordWrap) == 0;
                }
            }
            else
            {
                //  Without meta data
                coll = tableData.Rows?.FirstOrDefault()?.Values?.Length ?? 0;
                colNbsps = new bool[coll];
                for (int i = 0; i < coll; ++i)
                {
                    colToStrings.Add((null, false));
                    var title = (colTitles.Count + 1).ToString();
                    colTitles.Add(title);
                    colNbsps[i] = true;
                }
            }

            // Measure columns widths
            Span<int> colWidths = stackalloc int[coll];
            foreach (var row in tableData.Rows.Nullable())
            {
                var rowValues = row.Values;
                var rowLen = rowValues?.Length ?? 0;
                for (int i = 0; i < coll; ++i)
                {
                    if (hide.Contains(i))
                        continue;

                    var ni = i + 1;
                    var value = i < rowLen ? rowValues[i] : null;
                    var nextValue = (ni < coll) && (ni < rowLen) ? rowValues[ni] : null;
                    var (fmt, rightAlign) = colToStrings[i];
                    if (fmt == null)
                        (fmt, rightAlign) = TableDataExporterTools.GetDefault(value);
                    var valueText = StringTools.EscapeMD(fmt(value, nextValue, cols == null ? null : cols[i]), colNbsps[i]).Replace("\r", "").Replace("\n", "<br>");

                    var strLen = valueText.Length;
                    if (strLen > colWidths[i])
                        colWidths[i] = strLen;
                }
            }

            //  Build text

            StringBuilder sb = new StringBuilder();

            //  Title
            if (!String.IsNullOrEmpty(tableData.Title))
                sb.Append(linePrefix).Append(TitlePrefix).Append(StringTools.EscapeMD(tableData.Title, true)).AppendLine("  ");

            //  Headers
            sb.Append(linePrefix);
            for (int i = 0, t = 0; i < coll; ++i)
            {
                if (hide.Contains(i))
                    continue;
                var title = String.Concat(hs, StringTools.EscapeMD(colTitles[t], true), hs);
                ++t;
                var mxLen = Math.Max(colWidths[i], title.Length);
                colWidths[i] = mxLen;
                var (fmt, rightAlign) = colToStrings[i];

                sb.Append("| ").Append(rightAlign ? title.PadLeft(mxLen) : title.PadRight(mxLen)).Append(' ');
            }
            sb.AppendLine("|");

            //  Header underline and alignment
            sb.Append(linePrefix);
            for (int i = 0; i < coll; ++i)
            {
                if (hide.Contains(i))
                    continue;
                var mxLen = colWidths[i] + 2;
                var (fmt, rightAlign) = colToStrings[i];

                sb.Append("|").Append(rightAlign ? ":".PadLeft(mxLen, '-') : ":".PadRight(mxLen, '-'));
            }
            sb.AppendLine("|");

            //  Rows
            foreach (var row in tableData.Rows.Nullable())
            {
                var rowValues = row.Values;
                var rowLen = rowValues?.Length ?? 0;
                sb.Append(linePrefix);
                for (int i = 0; i < coll; ++i)
                {
                    if (hide.Contains(i))
                        continue;

                    var ni = i + 1;
                    var value = i < rowLen ? rowValues[i] : null;
                    var nextValue = (ni < coll) && (ni < rowLen) ? rowValues[ni] : null;
                    var (fmt, rightAlign) = colToStrings[i];
                    if (fmt == null)
                        (fmt, rightAlign) = TableDataExporterTools.GetDefault(value);
                    var valueText = StringTools.EscapeMD(fmt(value, nextValue, cols == null ? null : cols[i]), colNbsps[i]).Replace("\r", "").Replace("\n", "<br>");

                    var mxLen = colWidths[i];
                    sb.Append("| ").Append(rightAlign ? valueText.PadLeft(mxLen) : valueText.PadRight(mxLen)).Append(' ');
                }
                sb.AppendLine("|");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Export the table as a Markdown file (completes synchronously), see <see cref="GetMarkDownText(BaseTableData, object, TableDataExportOptions)"/>.
        /// </summary>
        /// <param name="tableData">The table to export.</param>
        /// <param name="context">Not used.</param>
        /// <param name="options">Export options, see <see cref="GetMarkDownText(BaseTableData, object, TableDataExportOptions)"/>.</param>
        /// <returns>A file named "[Filename].md" (default "Table.md") with the <see cref="Mimes.MarkdownText"/> mime type.</returns>
        public Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TableDataExportOptions options = null)
        {
            options = options ?? new TableDataExportOptions();
            var text = GetMarkDownText(tableData, context, options);
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            return Task.FromResult(new MemoryFile(name + ".md", Mimes.MarkdownText, Encoding.UTF8.GetBytes(text)));
        }
    }

}
