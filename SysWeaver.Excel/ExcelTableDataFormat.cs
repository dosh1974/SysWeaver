using GemBox.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SysWeaver.Data;


namespace SysWeaver.Excel
{
    public static class ExcelTableDataFormat
    {

        public delegate void FormatCellDelegate(ExcelCell cell, Object value, Object nextValue);

        public static FormatCellDelegate GetCellFormatter(Type type, TableDataRawFormatAttribute attr)
            => GetCellFormatter(type, attr?.Value);


        public static FormatCellDelegate GetCellFormatter(Type type, String options = null)
        {
            TableDataFormats fmt = default;
            var o = options?.Split(';');
            var ol = o?.Length;
            if (ol > 0)
                fmt = Enum.Parse<TableDataFormats>(o[0]);
            if (FmtStyles.TryGetValue(fmt, out var format))
                return format(type, fmt, o);
            return null;
        }
        
        public static readonly FormatCellDelegate DefaultFormatter = (cell, value, next) => cell.Value = value;


        public static String Format(String fmt, params Object[] ps)
        {
            fmt = fmt.Replace("{^", "{");
            fmt = fmt.Replace("{_", "{");
            return String.Format(fmt, ps);
        }

        public static String EscapeNumberFormat(String text)
        {
            if (String.IsNullOrEmpty(text))
                return text;
            var tl = text.Length;
            var o = new Char[tl + tl];
            for (int i = 0, d = 0; i < tl; ++i)
            {
                o[d] = '\\';
                ++d;
                o[d] = text[i];
                ++d;
            }
            return new string(o);
        }

        delegate FormatCellDelegate FormatDelegate(Type dataType, TableDataFormats fmt, String[] options);

        #region Format


        static readonly FormatCellDelegate PerRowFormatter = (cell, value, next) =>
        {
            var valType = value?.GetType();
            var data = next as String;
            if (data != null)
            {
                var si = data.IndexOf('|');
                if (si < 0)
                {
                    valType = valType ?? TypeFinder.Get(data);
                    data = null;
                }else
                {
                    valType = valType ?? TypeFinder.Get(data.Substring(si));
                    data = data.Substring(si + 1);
                }
            }
            var fmt = GetCellFormatter(valType, data) ?? DefaultFormatter;
            fmt(cell, value, next);
        };

        static FormatCellDelegate GetDefaultFormatter(Type dataType, TableDataFormats fmt, String[] options)
        {
            var fmtStr = TypeGeneric;
            if (dataType != null)
            {
                if (TypeFormatters.TryGetValue(dataType, out var tf))
                    return tf.Item1(dataType, tf.Item2, tf.Item3);
                if (TypeStyle.TryGetValue(dataType, out var temp))
                    fmtStr = temp;
            }
            var ol = options?.Length ?? 0;
            String prefix = "";
            String suffix = "";
            if (ol > 2)
            {
                var textFmt = (options[2] ?? "{0}").Replace("{1}", "{0}");
                var sp = textFmt.IndexOf("{0}");
                if (sp >= 0)
                {
                    prefix = EscapeNumberFormat(textFmt.Substring(0, sp));
                    suffix = EscapeNumberFormat(textFmt.Substring(sp + 3));
                    var p = fmtStr.Split(';');
                    var pl = p.Length;
                    for (int i = 0; i < pl; ++i)
                    {
                        var text = p[i];
                        var li = text.LastIndexOf(']');
                        if (li < 0)
                        {
                            p[i] = String.Join(text, prefix, suffix);
                        }else
                        {
                            ++li;
                            p[i] = String.Concat(text.Substring(0, li), prefix, text.Substring(li), suffix);
                        }
                    }
                    fmtStr = String.Join(';', p);
                }
            }
            return (cell, value, next) =>
            {
                cell.Value = value;
                cell.Style.NumberFormat = fmtStr;
            };
        }

        const ulong MaxLongU = (1UL << 53);
        const long MaxLong = (1L << 53);

        static FormatCellDelegate GetUrlFormatter(Type dataType, TableDataFormats fmt, String[] options)
        {
            var ol = options?.Length;
            var fmtText = ol > 1 ? options[1] : "{0}";
            var urlFmt = ol > 2 ? options[2] : "{2}";
            var titleFmt = ol > 3 ? options[3] : "Click to open \"{3}\".";
            return (cell, value, next) =>
            {
                try
                {
                    next = next ?? value;
                    var text = Format(fmtText, value, next);
                    var url = Format(urlFmt, value, next, text);
                    var title = Format(titleFmt, value, next, text, url);
                    cell.Value = text;
                    if (!String.IsNullOrEmpty(url))
                    {
                        cell.Hyperlink.Location = url;
                        cell.Hyperlink.IsExternal = true;
                        if (!String.IsNullOrEmpty(title))
                            cell.Hyperlink.ToolTip = title;
                    }
                }
                catch
                {
                    cell.Value = value;
                }
            };
        }

        static FormatCellDelegate GetTagsFormatter(Type dataType, TableDataFormats fmt, String[] options)
        {
            var ol = options?.Length;
            var fmtText = ol > 1 ? options[1] : "{0}";
            return (cell, value, next) =>
            {
                try
                {
                    var vals = (value?.ToString() ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    var text = string.Join(", ", vals.Select(x =>
                    {
                        var k = x.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        return Format(fmtText, x, k[0], k.Length > 1 ? k[1] : fmtText);
                    }));
                    cell.Value = text;
                }
                catch
                {
                    cell.Value = value;
                }
            };
        }

        static FormatCellDelegate GetNumberFormatter(Type dataType, TableDataFormats fmt, params String[] options)
        {
            var ol = options?.Length;
            int decimals = 2;
            if (ol > 1 && int.TryParse(options[1], out var temp))
                decimals = temp < 0 ? (DecimalTypes.Contains(dataType) ? -temp : 0) : temp;
            var dec = decimals > 0 ? "." + new String('0', decimals) : "";
            String prefix = "";
            String suffix = "";
            if (ol > 2)
            {
                var textFmt = (options[2] ?? "{0}").Replace("{1}", "{0}");
                var sp = textFmt.IndexOf("{0}");
                if (sp >= 0)
                {
                    prefix = EscapeNumberFormat(textFmt.Substring(0, sp));
                    suffix = EscapeNumberFormat(textFmt.Substring(sp + 3));
                }
            }
            dec += suffix;
            var fmtStr = String.Concat(prefix, "#,##0", dec, ";[Red]", prefix, "-#,##0", dec, ";[Blue]", prefix, "0", dec);
            var fmtStrText = String.Join("@", prefix, suffix);
            if (dataType == typeof(long))
            {
                return (cell, value, next) =>
                {
                    try
                    {
                        var v = (long)value;
                        var max = MaxLong;
                        if ((v > max) || (v < -max))
                        {
                            cell.Value = value;
                            cell.Style.NumberFormat = fmtStrText;
                            return;
                        }
                        cell.Value = (double)v;
                        cell.Style.NumberFormat = fmtStr;
                    }
                    catch
                    {
                        cell.Value = value;
                        cell.Style.NumberFormat = fmtStrText;
                    }
                };
            }
            if (dataType == typeof(ulong))
            {
                return (cell, value, next) =>
                {
                    try
                    {
                        var v = (ulong)value;
                        if (v > MaxLong)
                        {
                            cell.Value = value;
                            cell.Style.NumberFormat = fmtStrText;
                            return;
                        }
                        cell.Value = (double)v;
                        cell.Style.NumberFormat = fmtStr;
                    }
                    catch
                    {
                        cell.Value = value;
                        cell.Style.NumberFormat = fmtStrText;
                    }
                };
            }

            return (cell, value, next) =>
            {
                cell.Value = value;
                cell.Style.NumberFormat = fmtStr;
            };
        }

        static FormatCellDelegate GetByteSizeFormatter(Type dataType, TableDataFormats fmt, String[] options)
            => GetNumberFormatter(dataType, TableDataFormats.Number, null, "0", "{0} bytes");

        static FormatCellDelegate GetByteSpeedFormatter(Type dataType, TableDataFormats fmt, String[] options)
            => GetNumberFormatter(dataType, TableDataFormats.Number, null, "0", "{0} bytes/s");


        static FormatCellDelegate GetPerRowFormatter(Type dataType, TableDataFormats fmt, String[] options)
            => PerRowFormatter;

        static readonly Dictionary<TableDataFormats, FormatDelegate> FmtStyles = new Dictionary<TableDataFormats, FormatDelegate>()
        {
            { TableDataFormats.Default, GetDefaultFormatter },
            { TableDataFormats.Url, GetUrlFormatter },
            { TableDataFormats.Tags, GetTagsFormatter },
            { TableDataFormats.Number, GetNumberFormatter },
            { TableDataFormats.ByteSize, GetByteSizeFormatter },
            { TableDataFormats.ByteSpeed, GetByteSpeedFormatter },
            { TableDataFormats.Duration, GetDuration },
            { TableDataFormats.PerRowFormat, GetPerRowFormatter },
        };


        static readonly IReadOnlySet<Type> DecimalTypes = ReadOnlyData.Set(
            typeof(Single),
            typeof(Double),
            typeof(Decimal)
        );




        const int SecondsPerDay = 86400;

        static double ToDays(Decimal v) => (Double)(v * SecondsPerDay);

        static readonly Dictionary<Type, Func<Object, double>> TypeToDays = new Dictionary<Type, Func<object, double>>()
        {
            { typeof(TimeSpan), value => value.GetType() == typeof(String) ? TimeSpan.Parse((String)value).TotalDays : ((TimeSpan)value).TotalDays },
            { typeof(Double), value => ToDays((Decimal)(Double)value) },
            { typeof(Decimal), value => ToDays((Decimal)value) },
            { typeof(Single), value => ToDays((Decimal)(Single)value) },
            { typeof(SByte), value => ToDays((Decimal)(SByte)value) },
            { typeof(Int16), value => ToDays((Decimal)(Int16)value) },
            { typeof(Int32), value => ToDays((Decimal)(Int32)value) },
            { typeof(Int64), value => ToDays((Decimal)(Int64)value) },
            { typeof(Byte), value => ToDays((Decimal)(Byte)value) },
            { typeof(UInt16), value => ToDays((Decimal)(UInt16)value) },
            { typeof(UInt32), value => ToDays((Decimal)(UInt32)value) },
            { typeof(UInt64), value => ToDays((Decimal)(UInt64)value) },
        };


        public static void NormalizeDuration(ExcelCell cell)
        {
            if (cell.Style.NumberFormat != FmtDuration)
                return;
            cell.Style.NumberFormat = "[hh]:mm:ss.0000";
        }

        static readonly String FmtDuration = String.Concat(
            "[<", (60.0 / SecondsPerDay).ToString(CultureInfo.InvariantCulture), @"]s.000\ \s;",
            "[>3", (10.0 / SecondsPerDay).ToString(CultureInfo.InvariantCulture), @"][Red]#,##0.00\ \d\a\y\s;",
            "[Blue][hh]:mm:ss");

        static FormatCellDelegate GetDuration(Type dataType, TableDataFormats fmt, String[] options)
        {
            if ((dataType != null) && TypeToDays.TryGetValue(dataType, out var td))
            {
                return (cell, value, next) =>
                {
                    try
                    {
                        double days = 0;
                        if (value != null)
                            days = td(value);
                        cell.Value = days;
                        cell.Style.NumberFormat = FmtDuration;
                    }
                    catch
                    {
                        cell.Value = value;
                    }
                };
            }
            return DefaultFormatter;
        }


        #endregion// Format

        #region Type 


        public const String TypeInteger = @"#,##0;[Red]-#,##0;[Blue]0";

        public const String TypeDecimal = @"#,##0.00;[Red]-#,##0.00;[Blue]0.00";

        public const String TypeDateTime = @"YYYY-MM-DD hh:mm:ss;[Red]\-";

        public const String TypeDateOnly = @"YYYY-MM-DD;[Red]\-";
        public const String TypeTimeOnly = @"hh:mm:ss;[Red]\-";

        public const String TypeGeneric = "@";


        static readonly Dictionary<Type, Tuple<FormatDelegate, TableDataFormats, String[]>> TypeFormatters = new Dictionary<Type, Tuple<FormatDelegate, TableDataFormats, String[]>>()
        {
            { typeof(Int64), Tuple.Create((FormatDelegate)GetNumberFormatter, TableDataFormats.Number, new String[] { null, "0" })},
            { typeof(UInt64), Tuple.Create((FormatDelegate)GetNumberFormatter, TableDataFormats.Number, new String[] { null, "0" })},
            { typeof(TimeSpan), Tuple.Create((FormatDelegate)GetDuration, TableDataFormats.Duration, Array.Empty<String>()) },
        };



        static readonly Dictionary<Type, String> TypeStyle = new Dictionary<Type, String>()
        {
            { typeof(SByte), TypeInteger },
            { typeof(Int16), TypeInteger },
            { typeof(Int32), TypeInteger },

            { typeof(Byte), TypeInteger },
            { typeof(UInt16), TypeInteger },
            { typeof(UInt32), TypeInteger },

            { typeof(Single), TypeDecimal },
            { typeof(Double), TypeDecimal },
            { typeof(Decimal), TypeDecimal },

            { typeof(DateTime), TypeDateTime },
            { typeof(DateOnly), TypeDateOnly },
            { typeof(TimeOnly), TypeTimeOnly },

        };

        #endregion//Type 

    }

}
