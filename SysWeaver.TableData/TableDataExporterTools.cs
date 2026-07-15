using System;
using System.Collections.Generic;


namespace SysWeaver.Data
{
    public static class TableDataExporterTools
    {
        public delegate String Formatter(Object value, Object nextValue, TableDataColumn col);
        public delegate String SpecialFormatter(Formatter fmt, Object value, Object nextValue, TableDataColumn col);


        static readonly Formatter ObjToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return data.ToString();
        };
        /*
        static readonly Formatter DefToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            var type = data.GetType();
            if (DefToStrings.TryGetValue(type.FullName, out var fn))
                return fn(data, nextData, col);
            return data.ToString();
        };
        */
        static readonly Formatter SingleToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToSingle(data).ToValueString(3);
        };

        static readonly Formatter DoubleToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToDouble(data).ToValueString(3);
        };

        static readonly Formatter DecimalToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToDecimal(data).ToValueString(3);
        };

        static readonly Formatter SByteToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ((Int32)Convert.ToSByte(data)).ToValueString();
        };

        static readonly Formatter Int16ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ((Int32)Convert.ToInt16(data)).ToValueString();
        };


        static readonly Formatter Int32ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToInt32(data).ToValueString();
        };

        static readonly Formatter Int64ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToInt64(data).ToValueString();
        };

        static readonly Formatter ByteToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ((UInt32)Convert.ToByte(data)).ToValueString();
        };

        static readonly Formatter UInt16ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return ((UInt32)Convert.ToUInt16(data)).ToValueString();
        };


        static readonly Formatter UInt32ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToUInt32(data).ToValueString();
        };

        static readonly Formatter UInt64ToString = (data, nextData, col) =>
        {
            if (data == null)
                return "";
            return Convert.ToUInt64(data).ToValueString();
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
