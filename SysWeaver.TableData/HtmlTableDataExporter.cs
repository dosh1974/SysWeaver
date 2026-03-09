using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;


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


        public static readonly Func<Object, String> DefToString = data =>
        {
            if (data == null)
                return "";
            return data.ToString();
        };

        public static readonly Func<Object, String> SingleToString = data =>
        {
            if (data == null)
                return "";

            return Convert.ToSingle(data).ToString(CultureInfo.InvariantCulture);
        };

        public static readonly Func<Object, String> DoubleToString = data =>
        {
            if (data == null)
                return "";
            return Convert.ToDouble(data).ToString(CultureInfo.InvariantCulture);
        };

        public static readonly Func<Object, String> DecimalToString = data =>
        {
            if (data == null)
                return "";
            return Convert.ToDecimal(data).ToString(CultureInfo.InvariantCulture);
        };

        public static readonly IReadOnlyDictionary<String, Func<Object, String>> DefToStrings = new Dictionary<String, Func<Object, String>>(StringComparer.Ordinal)
        {
            { typeof(Single).FullName, SingleToString },
            { typeof(Double).FullName, DoubleToString },
            { typeof(Decimal).FullName, DecimalToString },
        }.Freeze();


        public static readonly IReadOnlyDictionary<String, String> Classes = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { typeof(Single).FullName, "r m n" },
            { typeof(Double).FullName, "r m n" },
            { typeof(Decimal).FullName, "r m n" },

            { typeof(SByte).FullName, "r m n" },
            { typeof(Int16).FullName, "r m n" },
            { typeof(Int32).FullName, "r m n" },
            { typeof(Int64).FullName, "r m n" },

            { typeof(Byte).FullName, "r m n" },
            { typeof(UInt16).FullName, "r m n" },
            { typeof(UInt32).FullName, "r m n" },
            { typeof(UInt64).FullName, "r m n" },

            { typeof(DateTime).FullName, "m t" },
            { typeof(TimeSpan).FullName, "m t" },
            { typeof(DateOnly).FullName, "m t" },
            { typeof(DateTimeOffset).FullName, "m t" },
            { typeof(TimeOnly).FullName, "m t" },

            { typeof(Guid).FullName, "m" },
            { typeof(Boolean).FullName, "b" },
        }.Freeze();


        public Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TableDataExportOptions options = null)
        {
            options = options ?? new TableDataExportOptions();
            StringBuilder rowsBuilder = new StringBuilder();
            List<Func<Object, String>> colToStrings = new List<Func<object, string>>();
            List<String> classes = new List<string>();

            HashSet<int> hide = new HashSet<int>();
            Dictionary<String, String> vals = new Dictionary<string, string>(StringComparer.Ordinal);
            var def = DefToString;
            var d = DefToStrings;
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
                    d.TryGetValue(col.Type, out var fn);
                    colToStrings.Add(fn ?? def);
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
                        vals["Text"] = col.Title;
                        vals["Class"] = (cl ?? "").Replace("m", "").Trim();
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
                                var raw = t[x];
                                var fn = x < colMax ? colToStrings[x] : def;
                                var cl = x < colMax ? classes[x] : "";
                                var fmt = fn(raw);
                                vals["Title"] = raw?.ToString() ?? "";
                                vals["Text"] = fmt ?? "";
                                vals["Class"] = cl ?? "";
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
