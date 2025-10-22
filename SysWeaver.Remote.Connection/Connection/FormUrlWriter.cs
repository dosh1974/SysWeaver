using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Diagnostics;

namespace SysWeaver.Remote.Connection
{
    sealed class FormUrlWriter
    {

        public FormUrlWriter(Byte[] initData = null, int startOffset = 0)
        {
            initData ??= GC.AllocateUninitializedArray<Byte>(4096);
            Data = initData;
            S = initData.Length;
            Offset = startOffset;
        }

        int S;


        public Byte[] Data;
        public int Offset;

        [Conditional("DEBUG")]
        void Validate(int size)
        {
            var o = Offset;
            var end = o + size;
            if (end > S)
                throw new Exception("Not enough data enured before write!");
        }

        void Grow(int end)
        {
            end += (4096 + 4095);
            end &= ~4095;
            var b = GC.AllocateUninitializedArray<Byte>(end);
            var o = Offset;
            if (o > 0)
                Buffer.BlockCopy(Data, 0, b, 0, o);
            Data = b;
            S = end;
        }


        public void Ensure(int size)
        {
            var end = Offset + size;
            if (end > S)
                Grow(end);
        }

        public void WriteSmall(Byte[] data)
        {
            var size = data.Length;
            var o = Offset;
            Validate(size);
            var e = o + size;
            var d = Data;
            int i = 0;
            while (o < e)
            {
                d[o] = data[i];
                ++o;
                ++i;
            }
            Offset = o;
        }

        public void WriteAsciiString(String s)
        {
            Validate(s.Length);
            var d = Data;
            var o = Offset;
            var l = s.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
#if DEBUG
                if ((uint)c > 0x7f)
                    throw new Exception("Ascii string contains non ascii chars!");
#endif//DEBUG
                d[o] = (Byte)c;
                ++o;
            }
            Offset = o;
        }

        static void Swap(Byte[] d, int start, int end)
        {
            --end;
            while (end > start)
            {
                var t = d[start];
                d[start] = d[end];
                d[end] = t;
                --end;
                ++start;
            }
        }
        static Byte[] GetNumberBytes(int i) => Encoding.UTF8.GetBytes(i.ToString());


        const int NumberCacheSize = 100;

        static readonly Byte[][] NumberCache = Enumerable.Range(0, NumberCacheSize + 1).Select(x => GetNumberBytes(x)).ToArray();

        static void WriteUInt32(FormUrlWriter w, UInt32 value)
        {
            var d = w.Data;
            var o = w.Offset;
            if (value <= NumberCacheSize)
            {
                w.WriteSmall(NumberCache[value]);
                return;
            }
            int c = o;
            do
            {
                var next = value / 10;
                var v = value - (next * 10);
                value = next;
                v += 48;
                d[c] = (Byte)v;
                ++c;
            } while (value != 0);
            w.Offset = c;
            Swap(d, o, c);
        }

        static void WriteInt32(FormUrlWriter w, Int32 signedValue)
        {
            var d = w.Data;
            var o = w.Offset;
            UInt32 value = (UInt32)signedValue;
            if (signedValue < 0)
            {
                d[o] = (Byte)('-');
                ++o;
                value = (UInt32)(-signedValue);
            }
            if (value <= NumberCacheSize)
            {
                w.Offset = o;
                w.WriteSmall(NumberCache[value]);
                return;
            }
            int c = o;
            do
            {
                var next = value / 10;
                var v = value - (next * 10);
                value = next;
                v += 48;
                d[c] = (Byte)v;
                ++c;
            } while (value != 0);
            w.Offset = c;
            Swap(d, o, c);
        }

        static void WriteInt64(FormUrlWriter w, Int64 signedValue)
        {
            var d = w.Data;
            var o = w.Offset;
            UInt64 value = (UInt64)signedValue;
            if (signedValue < 0)
            {
                d[o] = (Byte)('-');
                ++o;
                value = (UInt64)(-signedValue);
            }
            if (value <= NumberCacheSize)
            {
                w.Offset = o;
                w.WriteSmall(NumberCache[value]);
                return;
            }
            int c = o;
            do
            {
                var next = value / 10;
                var v = value - (next * 10);
                value = next;
                v += 48;
                d[c] = (Byte)v;
                ++c;
            } while (value != 0);
            w.Offset = c;
            Swap(d, o, c);
        }

        static void WriteUInt64(FormUrlWriter w, UInt64 value)
        {
            var d = w.Data;
            var o = w.Offset;
            if (value <= NumberCacheSize)
            {
                w.WriteSmall(NumberCache[value]);
                return;
            }
            int c = o;
            do
            {
                var next = value / 10;
                var v = value - (next * 10);
                value = next;
                v += 48;
                d[c] = (Byte)v;
                ++c;
            } while (value != 0);
            w.Offset = c;
            Swap(d, o, c);
        }

        static void WriteSingle(FormUrlWriter w, Single value)
        {
            var t = value.ToString("r", CultureInfo.InvariantCulture);
            w.WriteAsciiString(t);
        }

        static void WriteDouble(FormUrlWriter w, Double value)
        {
            var t = value.ToString("r", CultureInfo.InvariantCulture);
            w.WriteAsciiString(t);
        }

        static void WriteDecimal(FormUrlWriter w, Decimal value)
        {
            var t = value.ToString(CultureInfo.InvariantCulture);
            w.WriteAsciiString(t);
        }

        static void WriteTimeSpan(FormUrlWriter w, TimeSpan value)
        {
            var t = value.ToString("c", CultureInfo.InvariantCulture);
            w.WriteAsciiString(t);
        }

        static void WriteDateTime(FormUrlWriter w, DateTime value)
        {
            var t = value.ToString("o", CultureInfo.InvariantCulture);
            w.WriteAsciiString(t);
        }

        static void WriteGuid(FormUrlWriter w, Guid value)
        {
            var t = value.ToString();
            w.WriteAsciiString(t);
        }

        static readonly Byte[][] Booleans = 
        [
            Encoding.UTF8.GetBytes("false"),
            Encoding.UTF8.GetBytes("true"),
        ];

        static void WriteBoolean(FormUrlWriter w, Boolean value)
        {
            var b = Booleans[value ? 1 : 0];
            var bl = b.Length;
            var o = w.Offset;
            Buffer.BlockCopy(b, 0, w.Data, o, bl);
            o += bl;
            w.Offset = o;
        }

        static void WriteString(FormUrlWriter w, String value)
        {
            if (String.IsNullOrEmpty(value))
                return;
            var b = Encoding.UTF8.GetBytes(Uri.EscapeDataString(value));
            var bl = b.Length;
            w.Ensure(bl);
            var o = w.Offset;
            Buffer.BlockCopy(b, 0, w.Data, o, bl);
            o += bl;
            w.Offset = o;
        }

        static void WriteChar(FormUrlWriter w, Char value)
        {
            var b = Encoding.UTF8.GetBytes(Uri.EscapeDataString("" + value));
            var bl = b.Length;
            w.Ensure(bl);
            var o = w.Offset;
            Buffer.BlockCopy(b, 0, w.Data, o, bl);
            o += bl;
            w.Offset = o;
        }

        static readonly ParameterExpression Buf = Expression.Parameter(typeof(FormUrlWriter), "bw");

        static readonly IReadOnlyDictionary<Type, Type> WriterMap = new Dictionary<Type, Type>()
        {
            {  typeof(Byte), typeof(UInt32) },
            {  typeof(UInt16), typeof(UInt32) },
            {  typeof(SByte), typeof(Int32) },
            {  typeof(Int16), typeof(Int32) },
        }.Freeze();

        static Dictionary<Type, MethodInfo> GetWriters()
        {
            var t = new Dictionary<Type, MethodInfo>();
            Type[] types = 
            [
                typeof(Byte),
                typeof(SByte),
                typeof(UInt16),
                typeof(Int16),

                typeof(UInt32),
                typeof(Int32),
                typeof(UInt64),
                typeof(Int64),

                typeof(Single),
                typeof(Double),
                typeof(Decimal),

                typeof(TimeSpan),
                typeof(DateTime),
                typeof(Guid),

                typeof(Boolean),
                typeof(Char),
                typeof(String),
            ];
            var wm = WriterMap;
            var ft = typeof(FormUrlWriter);
            foreach (var x in types)
            {
                if (!wm.TryGetValue(x, out var wt))
                    wt = x;
                var mi = ft.GetMethod("Write" + wt.Name, BindingFlags.Static | BindingFlags.NonPublic);
                if (mi == null)
                    throw new Exception("Internal error!");
                t.Add(x, mi);
            }
            return t;
        }



        static readonly IReadOnlyDictionary<Type, MethodInfo> Writers = GetWriters().Freeze();
        static readonly MethodInfo WriterEnsure = typeof(FormUrlWriter).GetMethod(nameof(FormUrlWriter.Ensure));

        static readonly MethodInfo BlockCopy = typeof(Buffer).GetMethod(nameof(Buffer.BlockCopy));
        static readonly Expression Int32Const0 = Expression.Constant(0);

        public sealed class Cache<T>
        {
            static Action<FormUrlWriter, T> Build(bool ignoreDefaults = true)
            {
                var t = typeof(T);
                var buf = Buf;
                var data = Expression.Parameter(t, "data");
                var byteBuffer = Expression.Field(buf, nameof(FormUrlWriter.Data));
                var byteOffset = Expression.Field(buf, nameof(FormUrlWriter.Offset));
                String prefix = "";
                List<Expression> program = new List<Expression>();
                var writers = Writers;
                var encoding = Encoding.UTF8;
                var zero = Int32Const0;
                foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    var pi = member as PropertyInfo;
                    if (pi != null)
                    {
                        var m = pi;
                        if (!writers.TryGetValue(m.PropertyType, out var mi))
                            continue;
                        List<Expression> writeTo = ignoreDefaults ? new List<Expression>() : program;
                        var name = encoding.GetBytes(prefix + Uri.EscapeDataString(m.Name) + "=");
                        prefix = "&";
                        var nl = name.Length;
                        var size = nl + 128;
                        writeTo.Add(Expression.Call(buf, WriterEnsure, Expression.Constant(size)));
                        writeTo.Add(Expression.Call(BlockCopy, Expression.Constant(name), zero, byteBuffer, byteOffset, Expression.Constant(nl)));
                        writeTo.Add(Expression.AddAssign(byteOffset, Expression.Constant(nl)));
                        Expression src = Expression.Property(data, m);
                        var mt = mi.GetParameters()[1].ParameterType;
                        if (src.Type != mt)
                            src = Expression.Convert(src, mt);
                        writeTo.Add(Expression.Call(mi, buf, src));
                        if (ignoreDefaults)
                            program.Add(Expression.IfThen(Expression.NotEqual(src, Expression.Default(src.Type)), Expression.Block(writeTo)));
                        continue;
                    }
                    var fi = member as FieldInfo;
                    if (fi != null)
                    {
                        var m = fi;
                        if (!writers.TryGetValue(m.FieldType, out var mi))
                            continue;
                        List<Expression> writeTo = ignoreDefaults ? new List<Expression>() : program;
                        var name = encoding.GetBytes(prefix + Uri.EscapeDataString(m.Name) + "=");
                        prefix = "&";
                        var nl = name.Length;
                        var size = nl + 128;
                        writeTo.Add(Expression.Call(buf, WriterEnsure, Expression.Constant(size)));
                        writeTo.Add(Expression.Call(BlockCopy, Expression.Constant(name), zero, byteBuffer, byteOffset, Expression.Constant(nl)));
                        writeTo.Add(Expression.AddAssign(byteOffset, Expression.Constant(nl)));
                        Expression src = Expression.Field(data, m);
                        var mt = mi.GetParameters()[1].ParameterType;
                        if (src.Type != mt)
                            src = Expression.Convert(src, mt);
                        writeTo.Add(Expression.Call(mi, buf, src));
                        if (ignoreDefaults)
                            program.Add(Expression.IfThen(Expression.NotEqual(src, Expression.Default(src.Type)), Expression.Block(writeTo)));
                        continue;
                    }
                }
                var b = Expression.Block(program.ToArray());
                var l = Expression.Lambda<Action<FormUrlWriter, T>>(b, buf, data);
                return l.Compile();
            }

            public static readonly Action<FormUrlWriter, T> Write = Build(false);
            public static readonly Action<FormUrlWriter, T> WriteIgnoreDefaults = Build(true);


        }
    }


}
