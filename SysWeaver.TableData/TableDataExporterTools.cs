using System;
using System.Collections.Generic;


namespace SysWeaver.Data
{
    /// <summary>
    /// Shared cell formatting helpers for the text based table exporters (<see cref="HtmlTableDataExporter"/> and <see cref="MarkDownTableDataExporter"/>).
    /// </summary>
    /// <remarks>
    /// Numbers are formatted using <c>ToValueString</c> (with the decimal count from a <see cref="TableDataFormats.Number"/> column format, default 3).
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
            return int.TryParse(countText, out var count) ? count : 3;
        }

        static readonly Formatter ObjToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ApplyTextFormat(data.ToString(), nextData, col);
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
        }.Freeze();


        /// <summary>
        /// Get the formatter to use for a column.
        /// </summary>
        /// <param name="typeName">The full type name of the column (<see cref="TableDataBaseColumn.Type"/>).</param>
        /// <param name="headerFormat">The column format (<see cref="TableDataColumn.Format"/>), may be null.</param>
        /// <param name="specials">Optional special formatters keyed on the first ';' separated part of the format, wrapping the type formatter.</param>
        /// <returns>The formatter, and true if the column is numeric (should be right aligned).</returns>
        public static ValueTuple<Formatter, bool> Get(String typeName, String headerFormat, IReadOnlyDictionary<String, SpecialFormatter> specials = null)
        {
            var rightAlign = DefToStrings.TryGetValue(typeName, out var fmt);
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
            var rightAlign = DefToStrings.TryGetValue(value.GetType().FullName, out var fmt);
            return (fmt ?? ObjToString, rightAlign);
        }


    }

}
