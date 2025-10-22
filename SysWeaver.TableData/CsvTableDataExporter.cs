using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace SysWeaver.Data
{
    /// <summary>
    /// Export table as CSV
    /// </summary>
    public sealed class CsvTableDataExporter : ITableDataExporter
    {

        public override string ToString() => Name;

        public static readonly CsvTableDataExporter Comma = new CsvTableDataExporter("CSV (comma)", ',', '_', 10000, 
            "A text file where each line is a row.\nColumns are separated by a comma [,].");
        
        public static readonly CsvTableDataExporter Tab = new CsvTableDataExporter("CSV (tab)", '\t', '_', 10001,
            "A text file where each line is a row.\nColumns are separated by a tab.");

        public static readonly CsvTableDataExporter SemiColon = new CsvTableDataExporter("CSV (semi colon)", ';', '_', 10002,
            "A text file where each line is a row.\nColumns are separated by a semia colon [;].");


        public CsvTableDataExporter(String name, char sep, char rep, double order, String desc)
        {
            Name = name;
            Sep = sep;
            Rep = rep;
            Desc = desc;
            Order = order;
        }

        public String Name { get; init; }
        public String Desc { get; init; }

        public String Icon => "IconFileCsv";

        public double Order { get; init; }

        public bool RequireUser => false;

        readonly Char Sep;
        readonly Char Rep;


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
            return ((Single)data).ToString(CultureInfo.InvariantCulture);
        };

        public static readonly Func<Object, String> DoubleToString = data =>
        {
            if (data == null)
                return "";
            return ((Double)data).ToString(CultureInfo.InvariantCulture);
        };

        public static readonly Func<Object, String> DecimalToString = data =>
        {
            if (data == null)
                return "";
            return ((Decimal)data).ToString(CultureInfo.InvariantCulture);
        };

        public static readonly IReadOnlyDictionary<String, Func<Object, String>> DefToStrings = new Dictionary<String, Func<Object, String>>(StringComparer.Ordinal)
        {
            { typeof(Single).Name, SingleToString },
            { typeof(Double).Name, DoubleToString },
            { typeof(Decimal).Name, DecimalToString },
        }.Freeze(); 


        public Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TabelDataExportOptions options = null)
        {
            options = options ?? new TabelDataExportOptions();
            StringBuilder sb = new StringBuilder();
            var sep = Sep;
            var rep = Rep;

            List<Func<Object, String>> colToStrings = new List<Func<object, string>>();
            HashSet<int> hide = new HashSet<int>();
            var cols = tableData.Cols;
            var def = DefToString;
            var d = DefToStrings;
            var headers = !options.NoHeaders;
            if (cols != null)
            {
                bool didFirst = false;
                var coll = cols.Length;
                for (int i = 0; i < coll; ++ i)
                {
                    var col = cols[i];
                    d.TryGetValue(col.Type, out var fn);
                    colToStrings.Add(fn ?? def);
                    if ((col.Props & TableDataColumnProps.Hide) != 0)
                    {
                        hide.Add(i);
                        continue;
                    }
                    if (headers)
                    {
                        if (didFirst)
                            sb.Append(sep);
                        didFirst = true;
                        sb.Append(col.Title.Replace(sep, rep));
                    }
                }
                if (headers)
                    sb.AppendLine();
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
                        var tl = t.Length;
                        if (tl > 0)
                        {
                            bool didFirst = false;
                            for (int x = 0; x < tl; ++ x)
                            {
                                if (hide.Contains(x))
                                    continue;
                                if (didFirst)
                                    sb.Append(sep);
                                didFirst = true;
                                var fn = x < colMax ? colToStrings[x] : def;
                                sb.Append(fn(t[x]).Replace(sep, rep));
                            }
                            sb.AppendLine();
                        }
                    }
                }

            }
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            return Task.FromResult(new MemoryFile(name + ".csv", Mimes.Utf8PlainText, Encoding.UTF8.GetBytes(sb.ToString())));
        }
    }





}
