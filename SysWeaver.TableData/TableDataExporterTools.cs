using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;


namespace SysWeaver.Data
{
    /// <summary>
    /// Shared cell formatting helpers for the text based table exporters (<see cref="HtmlTableDataExporter"/> and <see cref="MarkDownTableDataExporter"/>).
    /// </summary>
    /// <remarks>
    /// Numbers (columns of the built-in numeric types and their nullable versions) are formatted using <c>ToValueString</c>
    /// (with the decimal count from a <see cref="TableDataFormats.Number"/> column format, default 3), other values using <see cref="Object.ToString"/>.
    /// Dates are formatted using the invariant culture as <see cref="DateTimeFormat"/> (UTC by convention, see <see cref="UtcSuffix"/> and <see cref="GetHeader"/>).
    /// Null values are formatted as empty texts.
    /// If the column format is <see cref="TableDataFormats.Default"/> or <see cref="TableDataFormats.Number"/> with a format string part,
    /// the text is formatted using <see cref="String.Format(string, object, object)"/> with {0} = the value text and {1} = the next column's value.
    /// All spaces in formatted text are replaced with non breaking spaces (U+00A0).
    /// A formatter may return a text starting with char 1 to signal that the rest is already formatted (and escaped) markup that should be emitted as is.
    /// </remarks>
    public static class TableDataExporterTools
    {
        /// <summary>
        /// Formats a cell value to text.
        /// </summary>
        /// <param name="value">The cell value, may be null.</param>
        /// <param name="nextValue">The value of the next column in the same row (used by some formats), may be null.</param>
        /// <param name="col">The column definition, may be null if the table has no column information.</param>
        /// <returns>The formatted text, a text starting with char 1 is pre-formatted markup.</returns>
        public delegate String Formatter(Object value, Object nextValue, TableDataColumn col);

        /// <summary>
        /// A formatter for a special column format (the first ';' separated part of <see cref="TableDataColumn.Format"/>, ex: "Url").
        /// </summary>
        /// <param name="fmt">The default formatter for the column type, used to format the raw value.</param>
        /// <param name="value">The cell value, may be null.</param>
        /// <param name="nextValue">The value of the next column in the same row, may be null.</param>
        /// <param name="col">The column definition.</param>
        /// <returns>The formatted text, a text starting with char 1 is pre-formatted markup.</returns>
        public delegate String SpecialFormatter(Formatter fmt, Object value, Object nextValue, TableDataColumn col);

        static readonly String NumberFmt = TableDataFormats.Number.ToString();

        static readonly IReadOnlyDictionary<String, int> DoFormats = new Dictionary<String, int>(StringComparer.Ordinal)
        {
            {  TableDataFormats.Default.ToString(), 1 },
            {  TableDataFormats.Number.ToString(), 2 },
        }.Freeze();

        /// <summary>
        /// Get an element from an array, falling back to a default value if the index is out of range or the element is null.
        /// </summary>
        /// <param name="vars">The array (typically the ';' separated parts of a column format).</param>
        /// <param name="index">The index of the element, must be non-negative.</param>
        /// <param name="def">The value to return if the element doesn't exist or is null.</param>
        /// <returns>The element or <paramref name="def"/>.</returns>
        public static String GetIndexed(String[] vars, int index, String def)
        {
            if (index >= vars.Length)
                return def;
            var v = vars[index];
            return v ?? def;
        }

        /// <summary>
        /// Check if a link is safe to put in an exported document, i.e. it's relative (no scheme), uses one of the schemes http, https, mailto or file,
        /// or starts with a drive letter ("C:\...").
        /// Prevents links like "javascript:..." or "data:..." (that would run code when clicked).
        /// </summary>
        /// <param name="link">The link to check (as it will be written, before any encoding).</param>
        /// <returns>True if the link is safe to use, false if it uses any other scheme (or is null).</returns>
        public static bool IsSafeLink(String link)
        {
            if (link == null)
                return false;
            //  Browsers ignore leading control chars / spaces and any tab or new line in an url
            //  Only schemes of length 1 (drive letters) or 4 - 6 (http, https, mailto, file) are safe, so only the first chars of the scheme are kept (on the stack)
            Span<Char> scheme = stackalloc Char[MaxSafeSchemeLength];
            int sl = 0;
            var l = link.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = link[i];
                if ((c == '\t') || (c == '\r') || (c == '\n'))
                    continue;
                if ((sl == 0) && (c <= ' '))
                    continue;
                if ((c == '/') || (c == '?') || (c == '#') || (c == '\\'))
                    return true;
                if (c == ':')
                {
                    if (sl > MaxSafeSchemeLength)
                        return false;
                    var s = scheme.Slice(0, sl);
                    if ((sl == 1) && Char.IsAsciiLetter(s[0]))
                        return true;
                    return
                        s.Equals("http", StringComparison.OrdinalIgnoreCase)
                        ||
                        s.Equals("https", StringComparison.OrdinalIgnoreCase)
                        ||
                        s.Equals("mailto", StringComparison.OrdinalIgnoreCase)
                        ||
                        s.Equals("file", StringComparison.OrdinalIgnoreCase);
                }
                if (sl < MaxSafeSchemeLength)
                    scheme[sl] = c;
                ++sl;
            }
            return true;
        }

        /// <summary>
        /// The length of the longest safe scheme ("mailto")
        /// </summary>
        const int MaxSafeSchemeLength = 6;

        static String ApplyTextFormat(String s, Object nextValue, TableDataColumn col)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            if (col == null)
                return s.Replace(' ', (Char)0xa0);
            var f = col.Format;
            if (f == null)
                return s.Replace(' ', (Char)0xa0);
            var p = f.Split(";");
            if (!DoFormats.TryGetValue(p[0], out var fi))
                return s.Replace(' ', (Char)0xa0);
            //  An amount in a currency (the fifth Number option, see TableDataCurrencyAttribute): the ISO 4217 code is appended ("1 234.50 USD")
            if ((fi == 2) && (p.Length > 5) && !String.IsNullOrEmpty(p[5]))
            {
                try
                {
                    var code = String.Format(p[5], s, nextValue, s);
                    if (!String.IsNullOrEmpty(code))
                        s = String.Concat(s, " ", code);
                }
                catch
                {
                }
            }
            var fmt = GetIndexed(p, fi, "{0}");
            if (fmt.FastEquals("{0}"))
                return s.Replace(' ', (Char)0xa0);
            return String.Format(fmt, s, nextValue).Replace(' ', (Char)0xa0);
        }

        static int GetDecimals(TableDataColumn col)
        {
            if (col == null)
                return 3;
            var f = col.Format;
            if (f == null)
                return 3;
            var p = f.Split(";");
            if (!p[0].FastEquals(NumberFmt))
                return 3;
            var countText = GetIndexed(p, 1, "3");
            //  A negative count means that many decimals for the floating point types (only those use the decimals), as in the web client
            return int.TryParse(countText, out var count) ? Math.Abs(count) : 3;
        }

        static readonly Formatter ObjToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            var s = ApplyTextFormat(data.ToString(), nextData, col);
            //  A leading (Char)1 is used by exporters to signal pre-formatted markup, so it may never come from a plain value
            if ((s != null) && (s.Length > 0) && (s[0] == (Char)1))
                s = s.TrimStart((Char)1);
            return s;
        };

        static readonly Formatter SingleToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToSingle(data).ToValueString(GetDecimals(col)), nextData, col);
        };

        static readonly Formatter DoubleToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToDouble(data).ToValueString(GetDecimals(col)), nextData, col);
        };

        static readonly Formatter DecimalToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToDecimal(data).ToValueString(GetDecimals(col)), nextData, col);
        };

        static readonly Formatter SByteToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(((Int32)Convert.ToSByte(data)).ToValueString(), nextData, col);
        };

        static readonly Formatter Int16ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(((Int32)Convert.ToInt16(data)).ToValueString(), nextData, col);
        };


        static readonly Formatter Int32ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToInt32(data).ToValueString(), nextData, col);
        };

        static readonly Formatter Int64ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToInt64(data).ToValueString(), nextData, col);
        };

        static readonly Formatter ByteToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(((UInt32)Convert.ToByte(data)).ToValueString(), nextData, col);
        };

        static readonly Formatter UInt16ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(((UInt32)Convert.ToUInt16(data)).ToValueString(), nextData, col);
        };


        static readonly Formatter UInt32ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToUInt32(data).ToValueString(), nextData, col);
        };

        static readonly Formatter UInt64ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(Convert.ToUInt64(data).ToValueString(), nextData, col);
        };



        /// <summary>
        /// The invariant date and time format used by the exporters for <see cref="DateTime"/> values ("2026-10-19 16:37:00").
        /// </summary>
        public const String DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// The invariant date format used by the exporters for <see cref="DateOnly"/> values ("2026-10-19").
        /// </summary>
        public const String DateOnlyFormat = "yyyy-MM-dd";

        /// <summary>
        /// The text appended to the header of date time columns (<see cref="IsUtcColumn"/>): table data date times are UTC by convention,
        /// and servers often run in another time zone than the reader, so the zone is always stated.
        /// </summary>
        public const String UtcSuffix = " (UTC)";

        /// <summary>
        /// Format a date time (UTC by convention) or a date time offset (converted to UTC) using the invariant <see cref="DateTimeFormat"/>,
        /// a <see cref="DateOnly"/> using <see cref="DateOnlyFormat"/>, an ISO 8601 date time text (json) as a UTC date time, null as an empty text
        /// and anything else using <see cref="Object.ToString"/>.
        /// </summary>
        /// <param name="data">The value, may be null.</param>
        /// <returns>The text.</returns>
        public static String DateToText(Object data)
        {
            switch (data)
            {
                case null:
                    return "";
                case DateTime dt:
                    return dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                case DateTimeOffset dto:
                    return dto.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                case DateOnly d:
                    return d.ToString(DateOnlyFormat, CultureInfo.InvariantCulture);
            }
            //  Exported table data is often deserialized from json (the web client's current view, an api), so a date is an ISO 8601 text
            var s = data.ToString();
            if ((s != null) && (s.Length >= 19) && (s[4] == '-') && (s[10] == 'T')
                && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var pdt))
                return pdt.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            return s;
        }

        static readonly Formatter DateToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return DateToText(data).Replace(' ', (Char)0xa0);
        };

        static readonly IReadOnlySet<String> DateTypes = ReadOnlyData.Set(StringComparer.Ordinal,
            typeof(DateTime).FullName, typeof(DateTime?).FullName, typeof(DateTimeOffset).FullName, typeof(DateTimeOffset?).FullName,
            typeof(DateOnly).FullName, typeof(DateOnly?).FullName);

        static readonly IReadOnlySet<String> UtcTypes = ReadOnlyData.Set(StringComparer.Ordinal,
            typeof(DateTime).FullName, typeof(DateTime?).FullName, typeof(DateTimeOffset).FullName, typeof(DateTimeOffset?).FullName);

        /// <summary>
        /// True if the column type is a date (<see cref="DateTime"/>, <see cref="DateTimeOffset"/> or <see cref="DateOnly"/>, nullable or not).
        /// </summary>
        /// <param name="typeName">The full type name of the column (<see cref="TableDataBaseColumn.Type"/>), may be null.</param>
        /// <returns>True for date columns.</returns>
        public static bool IsDateType(String typeName) => DateTypes.Contains(typeName ?? "");

        /// <summary>
        /// Get the enum type of a column (an enum or a nullable enum), the values of such a column are written as names, as the web client shows them
        /// (exported table data often comes through a serializer, so the values are numbers, ex: a log level "1" instead of "Debug").
        /// </summary>
        /// <param name="typeName">The full type name of the column (<see cref="TableDataBaseColumn.Type"/>), may be null.</param>
        /// <returns>The enum type, or null if the column isn't an enum (or the type isn't known or allowed here).</returns>
        public static Type GetEnumType(String typeName)
        {
            if (String.IsNullOrEmpty(typeName) || DefToStrings.ContainsKey(typeName) || DateTypes.Contains(typeName) || (typeName == StringTypeName))
                return null;
            return EnumTypes.GetOrAdd(typeName, static tn =>
            {
                try
                {
                    //  The name may come from a client (exported data), so it's resolved as data (type policy applies)
                    var t = TypeFinder.GetForData(tn);
                    if (t == null)
                        return null;
                    t = Nullable.GetUnderlyingType(t) ?? t;
                    return t.IsEnum ? t : null;
                }
                catch
                {
                    return null;
                }
            });
        }

        static readonly String StringTypeName = typeof(String).FullName;

        static readonly System.Collections.Concurrent.ConcurrentDictionary<String, Type> EnumTypes = new(StringComparer.Ordinal);

        /// <summary>
        /// Get the name of an enum value (the names of the flags for a flags enum, ex: "Read, Write").
        /// </summary>
        /// <param name="enumType">The enum type.</param>
        /// <param name="value">The value: an enum, a number (of any type, ex: from a serializer) or a text (returned as is), may be null.</param>
        /// <returns>The name, an empty text for null, the value text if it isn't a number.</returns>
        public static String EnumToText(Type enumType, Object value)
        {
            if (value == null)
                return "";
            if (value is Enum e)
                return e.ToString();
            if (value is String s)
                return s;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if ((enumType == null) || !enumType.IsEnum)
                return text;
            if (Int64.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                return Enum.ToObject(enumType, l).ToString();
            if (UInt64.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ul))
                return Enum.ToObject(enumType, ul).ToString();
            return text;
        }

        /// <summary>
        /// Get a value to text conversion for an enum column (see <see cref="GetEnumType"/> and <see cref="EnumToText"/>).
        /// </summary>
        /// <param name="typeName">The full type name of the column (<see cref="TableDataBaseColumn.Type"/>), may be null.</param>
        /// <returns>The conversion, or null if the column isn't an enum.</returns>
        public static Func<Object, String> GetEnumToText(String typeName)
        {
            var t = GetEnumType(typeName);
            return t == null ? null : (v => EnumToText(t, v));
        }

        /// <summary>
        /// True if the column holds date times that are exported as UTC: a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> column (nullable or not)
        /// without a special format (no format, "Default", or the "utc" / "local" display hints).
        /// </summary>
        /// <param name="col">The column, may be null.</param>
        /// <returns>True if the column header should be labeled with <see cref="UtcSuffix"/>.</returns>
        public static bool IsUtcColumn(TableDataColumn col)
        {
            if ((col == null) || !UtcTypes.Contains(col.Type ?? ""))
                return false;
            var f = col.Format;
            if (String.IsNullOrEmpty(f))
                return true;
            var k = f.SplitFirst(';');
            return k.FastEquals("Default") || k.FastEquals("utc") || k.FastEquals("local");
        }

        /// <summary>
        /// The header text of a column in an export: the title (or the name), with <see cref="UtcSuffix"/> appended for date time columns (<see cref="IsUtcColumn"/>).
        /// </summary>
        /// <param name="col">The column.</param>
        /// <returns>The header text.</returns>
        public static String GetHeader(TableDataColumn col)
        {
            var t = col.Title ?? col.Name ?? "";
            return IsUtcColumn(col) ? t + UtcSuffix : t;
        }

        static readonly IReadOnlyDictionary<String, Formatter> DefToStrings = new Dictionary<String, Formatter>(StringComparer.Ordinal)
        {
            { typeof(Single).FullName, SingleToString },
            { typeof(Double).FullName, DoubleToString },
            { typeof(Decimal).FullName, DecimalToString },
            { typeof(SByte).FullName, SByteToString },
            { typeof(Int16).FullName, Int16ToString },
            { typeof(Int32).FullName, Int32ToString },
            { typeof(Int64).FullName, Int64ToString },
            { typeof(Byte).FullName, ByteToString },
            { typeof(UInt16).FullName, UInt16ToString },
            { typeof(UInt32).FullName, UInt32ToString },
            { typeof(UInt64).FullName, UInt64ToString },
            { typeof(Single?).FullName, SingleToString },
            { typeof(Double?).FullName, DoubleToString },
            { typeof(Decimal?).FullName, DecimalToString },
            { typeof(SByte?).FullName, SByteToString },
            { typeof(Int16?).FullName, Int16ToString },
            { typeof(Int32?).FullName, Int32ToString },
            { typeof(Int64?).FullName, Int64ToString },
            { typeof(Byte?).FullName, ByteToString },
            { typeof(UInt16?).FullName, UInt16ToString },
            { typeof(UInt32?).FullName, UInt32ToString },
            { typeof(UInt64?).FullName, UInt64ToString },
        }.Freeze();


        /// <summary>
        /// Get the formatter to use for a column.
        /// </summary>
        /// <param name="typeName">The full type name of the column (<see cref="TableDataBaseColumn.Type"/>), may be null (formatted using <see cref="Object.ToString"/>).</param>
        /// <param name="headerFormat">The column format (<see cref="TableDataColumn.Format"/>), may be null.</param>
        /// <param name="specials">Optional special formatters keyed on the first ';' separated part of the format, wrapping the type formatter.</param>
        /// <returns>The formatter, and true if the column is numeric (should be right aligned).</returns>
        public static ValueTuple<Formatter, bool> Get(String typeName, String headerFormat, IReadOnlyDictionary<String, SpecialFormatter> specials = null)
        {
            var rightAlign = DefToStrings.TryGetValue(typeName ?? "", out var fmt);
            if ((fmt == null) && IsDateType(typeName))
                fmt = DateToString;
            if (fmt == null)
            {
                //  Enums: the name (exported data is often serialized, so the value is a number)
                var et = GetEnumToText(typeName);
                if (et != null)
                    fmt = (c, n, h) => ObjToString(c == null ? null : et(c), n, h);
            }
            if ((specials != null) && (!String.IsNullOrEmpty(headerFormat)))
            {
                var special = headerFormat.SplitFirst(';');
                if (specials.TryGetValue(special, out var sf))
                {
                    var f = fmt ?? ObjToString;
                    fmt = (c, n, h) => sf(f, c, n, h);
                }
            }
            return (fmt ?? ObjToString, rightAlign);
        }

        /// <summary>
        /// Get the formatter to use for a value when no column information is available, based on the runtime type of the value.
        /// </summary>
        /// <param name="value">The value, may be null.</param>
        /// <returns>The formatter, and true if the value is numeric (should be right aligned).</returns>
        public static ValueTuple<Formatter, bool> GetDefault(Object value)
        {
            if (value == null)
                return (ObjToString, false);
            var tn = value.GetType().FullName;
            var rightAlign = DefToStrings.TryGetValue(tn, out var fmt);
            if ((fmt == null) && IsDateType(tn))
                fmt = DateToString;
            return (fmt ?? ObjToString, rightAlign);
        }


        /// <summary>
        /// Get the UTF-8 bytes of the text in a string builder, the same bytes as Encoding.UTF8.GetBytes(sb.ToString()) (invalid surrogates are replaced with U+FFFD),
        /// without creating the string (the chunks are counted, then encoded into an exactly sized array).
        /// </summary>
        /// <param name="sb">The string builder</param>
        /// <returns>The UTF-8 bytes</returns>
        internal static Byte[] GetUtf8Bytes(StringBuilder sb)
        {
            var enc = Encoding.UTF8;
            //  Count, a surrogate pair split between two chunks is counted as 3 + 3 bytes (two replacement chars) instead of 4
            long count = 0;
            bool prevHigh = false;
            foreach (var c in sb.GetChunks())
            {
                var s = c.Span;
                var sl = s.Length;
                if (sl <= 0)
                    continue;
                count += enc.GetByteCount(s);
                if (prevHigh && Char.IsLowSurrogate(s[0]))
                    count -= 2;
                prevHigh = Char.IsHighSurrogate(s[sl - 1]);
            }
            if (count > Array.MaxLength)
                return enc.GetBytes(sb.ToString());
            var data = GC.AllocateUninitializedArray<Byte>((int)count);
            //  Encode, the encoder keeps a high surrogate at the end of a chunk until the next chunk
            var encoder = enc.GetEncoder();
            var d = data.AsSpan();
            foreach (var c in sb.GetChunks())
            {
                var w = encoder.GetBytes(c.Span, d, false);
                d = d.Slice(w);
            }
            var e = encoder.GetBytes(ReadOnlySpan<Char>.Empty, d, true);
            d = d.Slice(e);
            if (d.Length != 0)
                return enc.GetBytes(sb.ToString());
            return data;
        }


    }

}
