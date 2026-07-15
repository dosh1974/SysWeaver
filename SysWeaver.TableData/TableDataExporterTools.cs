using System;
using System.Collections.Generic;


namespace SysWeaver.Data
{
    public static class TableDataExporterTools
    {
        public delegate String Formatter(Object value, Object nextValue, TableDataColumn col);
        public delegate String SpecialFormatter(Formatter fmt, Object value, Object nextValue, TableDataColumn col);

        static readonly String NumberFmt = TableDataFormats.Number.ToString();

        static readonly IReadOnlyDictionary<String, int> DoFormats = new Dictionary<String, int>(StringComparer.Ordinal)
        {
            {  TableDataFormats.Default.ToString(), 1 },
            {  TableDataFormats.Number.ToString(), 2 },
        }.Freeze();

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

        public static ValueTuple<Formatter, bool> GetDefault(Object value)
        {
            if (value == null)
                return (ObjToString, false);
            var rightAlign = DefToStrings.TryGetValue(value.GetType().FullName, out var fmt);
            return (fmt ?? ObjToString, rightAlign);
        }


    }

}
