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
    /// Headers and values containing the separator, a double quote or a line break (CR or LF) are wrapped in double quotes, with any embedded double quotes doubled (RFC 4180).
    /// Hidden columns (<see cref="TableDataColumnProps.Hide"/>) are omitted.
    /// <see cref="Single"/>, <see cref="Double"/> and <see cref="Decimal"/> values (also in nullable columns) are written using the invariant culture
    /// (rounded to the decimals of a <see cref="TableDataFormats.Number"/> column format that names them, see <see cref="RoundedToString"/>),
    /// dates using the invariant culture as "yyyy-MM-dd HH:mm:ss" (UTC by convention; the header of a date time column ends with " (UTC)", see <see cref="TableDataExporterTools.GetHeader"/>),
    /// enum values (also numbers from a serializer) as their names (ex: "Debug", see <see cref="TableDataExporterTools.GetEnumType"/>),
    /// other values using <see cref="Object.ToString"/>. Null values are written as empty texts.
    /// No protection against spreadsheet formula injection is done.
    /// Rows without values (null or empty) are skipped, values beyond the column definitions are written using <see cref="DefToString"/>.
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
        /// Comma separated values.
        /// </summary>
        public static readonly CsvTableDataExporter Comma = new CsvTableDataExporter("CSV (comma)", ',', '_', 10000, 
            "A text file where each line is a row.\nColumns are separated by a comma [,].");
        
        /// <summary>
        /// Tab separated values.
        /// </summary>
        public static readonly CsvTableDataExporter Tab = new CsvTableDataExporter("CSV (tab)", '\t', '_', 10001,
            "A text file where each line is a row.\nColumns are separated by a tab.");

        /// <summary>
        /// Semicolon separated values.
        /// </summary>
        public static readonly CsvTableDataExporter SemiColon = new CsvTableDataExporter("CSV (semi colon)", ';', '_', 10002,
            "A text file where each line is a row.\nColumns are separated by a semicolon [;].");


        /// <summary>
        /// Create a CSV exporter.
        /// </summary>
        /// <param name="name">The display name of the exporter.</param>
        /// <param name="sep">The column separator char.</param>
        /// <param name="rep">Not used (kept for compatibility): headers and values containing the separator are quoted instead.</param>
        /// <param name="order">Sort order of the exporter in UI lists.</param>
        /// <param name="desc">A description of the exporter.</param>
        public CsvTableDataExporter(String name, char sep, char rep, double order, String desc)
        {
            Name = name;
            Sep = sep;
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
        /// Converts a date (<see cref="DateTime"/>, <see cref="DateTimeOffset"/> converted to UTC, or <see cref="DateOnly"/>) to an invariant text (empty for null),
        /// see <see cref="TableDataExporterTools.DateToText"/>.
        /// </summary>
        public static readonly Func<Object, String> DateToString = TableDataExporterTools.DateToText;

        /// <summary>
        /// Value to text conversions keyed on the full type name of the column (<see cref="Single"/>, <see cref="Double"/>, <see cref="Decimal"/>, the date types and their nullable versions),
        /// types not found use <see cref="DefToString"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<String, Func<Object, String>> DefToStrings = new Dictionary<String, Func<Object, String>>(StringComparer.Ordinal)
        {
            { typeof(Single).FullName, SingleToString },
            { typeof(Double).FullName, DoubleToString },
            { typeof(Decimal).FullName, DecimalToString },
            { typeof(Single?).FullName, SingleToString },
            { typeof(Double?).FullName, DoubleToString },
            { typeof(Decimal?).FullName, DecimalToString },
            { typeof(DateTime).FullName, DateToString },
            { typeof(DateTime?).FullName, DateToString },
            { typeof(DateTimeOffset).FullName, DateToString },
            { typeof(DateTimeOffset?).FullName, DateToString },
            { typeof(DateOnly).FullName, DateToString },
            { typeof(DateOnly?).FullName, DateToString },
        }.Freeze();


        /// <summary>
        /// Wrap a value in double quotes (doubling embedded quotes) if it contains the separator, a double quote or a line break, else return it as is (RFC 4180).
        /// </summary>
        static String Escape(String v, Char sep)
        {
            if ((v.Length <= 0) || ((v.IndexOf(sep) < 0) && (v.IndexOfAny(QuoteChars) < 0)))
                return v;
            return String.Concat("\"", v.Replace("\"", "\"\""), "\"");
        }

        static readonly Char[] QuoteChars = ['"', '\r', '\n'];

        static readonly HashSet<String> RoundedTypes = new HashSet<String>(StringComparer.Ordinal)
        {
            typeof(Single).FullName, typeof(Double).FullName, typeof(Decimal).FullName,
            typeof(Single?).FullName, typeof(Double?).FullName, typeof(Decimal?).FullName,
        };

        static readonly String NumberFormatName = TableDataFormats.Number.ToString();

        /// <summary>
        /// A floating point column (<see cref="Single"/>, <see cref="Double"/>, <see cref="Decimal"/>, also nullable) with a <see cref="TableDataFormats.Number"/> format
        /// that names its decimals ("Number;4", a currency "Number;-2;..."; the absolute value is used, 0 = not named) is written rounded to those decimals,
        /// as the web client shows it (ex: 6865.67 instead of 6865.67164179104), using the invariant culture; a tiny non zero value that would round to 0 keeps
        /// two significant digits (as the web client). Other columns are written unchanged.
        /// </summary>
        /// <param name="col">The column.</param>
        /// <returns>The conversion, null if the column is not rounded.</returns>
        static Func<Object, String> RoundedToString(TableDataColumn col)
        {
            if ((col?.Format == null) || !RoundedTypes.Contains(col.Type ?? ""))
                return null;
            var p = col.Format.Split(';');
            if ((p.Length < 2) || !String.Equals(p[0], NumberFormatName, StringComparison.Ordinal) || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) || (dec == 0))
                return null;
            dec = Math.Min(15, Math.Abs(dec));
            return data =>
            {
                if (data == null)
                    return "";
                if (data is Decimal m)
                    return Math.Round(m, Math.Min(dec, 28), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
                var v = Convert.ToDouble(data, CultureInfo.InvariantCulture);
                if (Double.IsNaN(v) || Double.IsInfinity(v))
                    return v.ToString(CultureInfo.InvariantCulture);
                var d = dec;
                var a = Math.Abs(v);
                if ((a > 0) && (a < 0.5 * Math.Pow(10, -d)))
                    d = Math.Min(15, (int)Math.Ceiling(-Math.Log10(a)) + 1);
                return Math.Round(v, d, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            };
        }


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

            var cols = tableData.Cols;
            var def = DefToString;
            var d = DefToStrings;
            var headers = !options.NoHeaders;
            var colMax = cols?.Length ?? 0;
            var colToStrings = colMax > 0 ? new Func<Object, String>[colMax] : Array.Empty<Func<Object, String>>();
            var hide = colMax > 0 ? new bool[colMax] : Array.Empty<bool>();
            if (cols != null)
            {
                bool didFirst = false;
                var coll = cols.Length;
                for (int i = 0; i < coll; ++ i)
                {
                    var col = cols[i];
                    d.TryGetValue(col.Type ?? "", out var fn);
                    colToStrings[i] = RoundedToString(col) ?? fn ?? TableDataExporterTools.GetEnumToText(col.Type) ?? def;
                    if ((col.Props & TableDataColumnProps.Hide) != 0)
                    {
                        hide[i] = true;
                        continue;
                    }
                    if (headers)
                    {
                        if (didFirst)
                            sb.Append(sep);
                        didFirst = true;
                        sb.Append(Escape(TableDataExporterTools.GetHeader(col), sep));
                    }
                }
                if (headers)
                    sb.AppendLine();
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
                            bool didFirst = false;
                            for (int x = 0; x < tl; ++ x)
                            {
                                Func<Object, String> fn;
                                if (x < colMax)
                                {
                                    if (hide[x])
                                        continue;
                                    fn = colToStrings[x];
                                }
                                else
                                {
                                    fn = def;
                                }
                                if (didFirst)
                                    sb.Append(sep);
                                didFirst = true;
                                sb.Append(Escape(fn(t[x]), sep));
                            }
                            sb.AppendLine();
                        }
                    }
                }

            }
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            return Task.FromResult(new MemoryFile(name + ".csv", Mimes.Utf8PlainText, TableDataExporterTools.GetUtf8Bytes(sb)));
        }
    }


}
