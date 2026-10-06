using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace SysWeaver.Data
{
    /// <summary>
    /// Export table data as a UTF-8 CSV text file.
    /// </summary>
    /// <remarks>
    /// Values are never quoted: any occurrence of the separator char in a header or value is replaced by a replacement char instead.
    /// Line breaks inside values are not escaped.
    /// Hidden columns (<see cref="TableDataColumnProps.Hide"/>) are omitted.
    /// <see cref="Single"/>, <see cref="Double"/> and <see cref="Decimal"/> values are written using the invariant culture,
    /// other values using <see cref="Object.ToString"/> (current culture).
    /// The exporter is stateless and thread safe.
    /// </remarks>
    public sealed class CsvTableDataExporter : ITableDataExporter
    {

        /// <summary>
        /// Returns the <see cref="Name"/>.
        /// </summary>
        /// <returns>The name of the exporter.</returns>
        public override string ToString() => Name;

        /// <summary>
        /// Comma separated values, commas in values are replaced with '_'.
        /// </summary>
        public static readonly CsvTableDataExporter Comma = new CsvTableDataExporter("CSV (comma)", ',', '_', 10000, 
            "A text file where each line is a row.\nColumns are separated by a comma [,].");
        
        /// <summary>
        /// Tab separated values, tabs in values are replaced with '_'.
        /// </summary>
        public static readonly CsvTableDataExporter Tab = new CsvTableDataExporter("CSV (tab)", '\t', '_', 10001,
            "A text file where each line is a row.\nColumns are separated by a tab.");

        /// <summary>
        /// Semi colon separated values, semi colons in values are replaced with '_'.
        /// </summary>
        public static readonly CsvTableDataExporter SemiColon = new CsvTableDataExporter("CSV (semi colon)", ';', '_', 10002,
            "A text file where each line is a row.\nColumns are separated by a semia colon [;].");


        /// <summary>
        /// Create a CSV exporter.
        /// </summary>
        /// <param name="name">The display name of the exporter.</param>
        /// <param name="sep">The column separator char.</param>
        /// <param name="rep">The char that replaces any <paramref name="sep"/> found in headers and values.</param>
        /// <param name="order">Sort order of the exporter in UI lists.</param>
        /// <param name="desc">A description of the exporter.</param>
        public CsvTableDataExporter(String name, char sep, char rep, double order, String desc)
        {
            Name = name;
            Sep = sep;
            Rep = rep;
            Desc = desc;
            Order = order;
        }

        /// <inheritdoc/>
        public String Name { get; init; }

        /// <inheritdoc/>
        public String Desc { get; init; }

        /// <inheritdoc/>
        public String Icon => "IconFileCsv";

        /// <inheritdoc/>
        public double Order { get; init; }

        /// <summary>
        /// Always false, CSV export doesn't require a signed in user.
        /// </summary>
        public bool RequireUser => false;

        readonly Char Sep;
        readonly Char Rep;


        /// <summary>
        /// Default value to text conversion: empty for null, else <see cref="Object.ToString"/>.
        /// </summary>
        public static readonly Func<Object, String> DefToString = data =>
        {
            if (data == null)
                return "";
            return data.ToString();
        };

        /// <summary>
        /// Converts a value to a <see cref="Single"/> and formats it using the invariant culture (empty for null).
        /// </summary>
        public static readonly Func<Object, String> SingleToString = data =>
        {
            if (data == null)
                return "";

            return Convert.ToSingle(data).ToString(CultureInfo.InvariantCulture);
        };

        /// <summary>
        /// Converts a value to a <see cref="Double"/> and formats it using the invariant culture (empty for null).
        /// </summary>
        public static readonly Func<Object, String> DoubleToString = data =>
        {
            if (data == null)
                return "";
            return Convert.ToDouble(data).ToString(CultureInfo.InvariantCulture);
        };

        /// <summary>
        /// Converts a value to a <see cref="Decimal"/> and formats it using the invariant culture (empty for null).
        /// </summary>
        public static readonly Func<Object, String> DecimalToString = data =>
        {
            if (data == null)
                return "";
            return Convert.ToDecimal(data).ToString(CultureInfo.InvariantCulture);
        };


        /// <summary>
        /// Value to text conversions keyed on the full type name of the column, types not found use <see cref="DefToString"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<String, Func<Object, String>> DefToStrings = new Dictionary<String, Func<Object, String>>(StringComparer.Ordinal)
        {
            { typeof(Single).FullName, SingleToString },
            { typeof(Double).FullName, DoubleToString },
            { typeof(Decimal).FullName, DecimalToString },
        }.Freeze(); 


        /// <summary>
        /// Export the table as a CSV file (completes synchronously).
        /// </summary>
        /// <param name="tableData">The table to export, columns are optional (without columns no header line is written).</param>
        /// <param name="context">Not used.</param>
        /// <param name="options">Export options, <see cref="TableDataExportOptions.NoHeaders"/> and <see cref="TableDataExportOptions.Filename"/> are used.</param>
        /// <returns>A file named "[Filename].csv" (default "Table.csv") with the <see cref="Mimes.Utf8PlainText"/> mime type.</returns>
        public Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TableDataExportOptions options = null)
        {
            options = options ?? new TableDataExportOptions();
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
