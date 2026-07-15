using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;


namespace SysWeaver.Data
{

    /// <summary>
    /// Export table as markdown
    /// </summary>
    public sealed class MarkDownTableDataExporter : ITableDataExporter
    {


        public static readonly MarkDownTableDataExporter Instance = new MarkDownTableDataExporter();

        public override string ToString() => Name;

        public MarkDownTableDataExporter()
        {
        }




        public String Name => "Mark down";
        public String Desc => "A mark down (MD) formatted text file";

        public String Icon => "IconFileMD";

        public double Order => 9000;

        public const String ColHeaderStyle = "**";
        public const String TitlePrefix = "### ";

        public bool RequireUser => false;


        static readonly IReadOnlySet<Char> EscapeChars = ReadOnlyData.Set(
                '\\',
                '`',
                '*',
                '_',
                '{',
                '}',
                '[',
                ']',
                '<',
                '>',
                '(',
                ')',
                '#',
//                '+',
//                '-',
//                '.',
                '!',
                '|'
            );

        public static String EscapeMD(String text)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            if (text[0] == (Char)1)
                return text.Substring(1);
            var l = text.Length;
            Span<Char> t = stackalloc Char[l * 2];
            int o = 0;
            var e = EscapeChars;
            for (int i = 0; i < l; ++ i)
            {
                var c = text[i];
                if (e.Contains(c))
                {
                    t[o] = '\\';
                    ++o;
                }
                t[o] = c;
                ++o;
            }
            if (o == l)
                return text;
            return new string(t.Slice(0, o));
        }

        static String GetIndexed(String[] vars, int index, String def)
        {
            if (index >= vars.Length)
                return def;
            var v = vars[index];
            return v ?? def;
        }


        static String FormatUrl(TableDataExporterTools.Formatter f, Object value, Object nextValue, TableDataColumn col)
        {
            if (value == null)
                return "";
            // Url;{0};{1}/README.md;Click to open "{3}".
            var valueText = f(value, nextValue, col);
            var t = col.Format.Split(';');
            var text = String.Format(GetIndexed(t, 1, "{0}"), valueText, nextValue);
            var link = String.Format(GetIndexed(t, 2, "{2}"), value, nextValue, text);
            var title = String.Format(GetIndexed(t, 3, "Click to open \"{3}\"."), value, nextValue, text, link);
            if (!String.IsNullOrEmpty(title))
                return String.Concat((Char)1, '[', EscapeMD(text), "](", EscapeMD(link), " \"", EscapeMD(title).Replace("\"", "\\\""), "\")");
            return String.Concat((Char)1, '[', EscapeMD(text), "](", EscapeMD(link), ')');
        }

        static readonly IReadOnlyDictionary<String, TableDataExporterTools.SpecialFormatter> Formatters = new Dictionary<String, TableDataExporterTools.SpecialFormatter>(StringComparer.Ordinal)
        {
            { "Url", FormatUrl }

        }.Freeze();

        public Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TableDataExportOptions options = null)
        {
        //  Measure and get data
            options = options ?? new TableDataExportOptions();
            String linePrefix = options?.Custom ?? "";
            List<ValueTuple<TableDataExporterTools.Formatter, bool>> colToStrings = new ();
            HashSet<int> hide = new HashSet<int>();
            var cols = tableData.Cols;
            var hs = options.NoHeaders ? "" : ColHeaderStyle;
            List<String> colTitles = new List<string>();
            int coll;
        //  Get column titles, functions and son
            if (cols != null)
            {
                //  With meta data
                coll = cols.Length;
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
                }
            }
            else
            {
                //  Without meta data
                coll = tableData.Rows.FirstOrDefault()?.Values?.Length ?? 0;
                for (int i = 0; i < coll; ++i)
                {
                    colToStrings.Add((null, false));
                    var title = (colTitles.Count + 1).ToString();
                    colTitles.Add(title);
                }
            }

            // Measure columns widths
            Span<int> colWidths = stackalloc int[coll];
            foreach (var row in tableData.Rows.Nullable())
            {
                for (int i = 0; i < coll; ++i)
                {
                    if (hide.Contains(i))
                        continue;

                    var ni = i + 1;
                    var value = row.Values[i];
                    var nextValue = ni < coll ? row.Values[ni] : null;
                    var (fmt, rightAlign) = colToStrings[i];
                    if (fmt == null)
                        (fmt, rightAlign) = TableDataExporterTools.GetDefault(value);
                    var valueText = EscapeMD(fmt(value, nextValue, cols == null ? null : cols[i]));
                    
                    var strLen = valueText.Length;
                    if (strLen > colWidths[i])
                        colWidths[i] = strLen;
                }
            }

            //  Build text

            StringBuilder sb = new StringBuilder();

            //  Title
            if (!String.IsNullOrEmpty(tableData.Title))
                sb.Append(linePrefix).Append(TitlePrefix).Append(EscapeMD(tableData.Title)).AppendLine("  ");

            //  Headers
            sb.Append(linePrefix);
            for (int i = 0, t = 0; i < coll; ++i)
            {
                if (hide.Contains(i))
                    continue;
                var title = String.Concat(hs, EscapeMD(colTitles[t]), hs);
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
                sb.Append(linePrefix);
                for (int i = 0; i < coll; ++i)
                {
                    if (hide.Contains(i))
                        continue;

                    var ni = i + 1;
                    var value = row.Values[i];
                    var nextValue = ni < coll ? row.Values[ni] : null;
                    var (fmt, rightAlign) = colToStrings[i];
                    if (fmt == null)
                        (fmt, rightAlign) = TableDataExporterTools.GetDefault(value);
                    var valueText = EscapeMD(fmt(value, nextValue, cols == null ? null : cols[i]));

                    var mxLen = colWidths[i];
                    sb.Append("| ").Append(rightAlign ? valueText.PadLeft(mxLen) : valueText.PadRight(mxLen)).Append(' ');
                }
                sb.AppendLine("|");
            }

            //  Save
            var text = sb.ToString();
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            return Task.FromResult(new MemoryFile(name + ".md", Mimes.MarkdownText, Encoding.UTF8.GetBytes(text)));
        }
    }

}
