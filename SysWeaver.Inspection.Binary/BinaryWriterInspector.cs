using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SysWeaver.Inspection.Implementation;

namespace SysWeaver.Inspection
{

    public sealed class BinaryWriterInspector : IInspectorImplementation, IWriteInspector, IDisposable
    {
        #region Life time

        public BinaryWriterInspector(Stream s, Encoding encoding, bool leaveOpen = false)
            : this(new BinaryWriter(s, encoding, leaveOpen), false)
        {
            Encoding = encoding;
        }

        public BinaryWriterInspector(Stream s, bool leaveOpen = false)
            : this(s, Encoding.Unicode, leaveOpen)
        {
            Encoding = Encoding.Unicode;
        }

        public BinaryWriterInspector(BinaryWriter writer, bool leaveOpen = false)
        {
            Writer = writer;
            LeaveOpen = leaveOpen;
            Encoding = Encoding.Unicode;
        }

        public readonly BinaryWriter Writer;

        readonly bool LeaveOpen;
        readonly Encoding Encoding;

        public Dictionary<String, Object> Context
        {
            get
            {
                return InternalContext;
            }
        }
        readonly Dictionary<String, Object> InternalContext = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public TypenameQualifications TypenameQualification = TypenameQualifications.Assembly;

        public void Dispose()
        {
            if (!LeaveOpen)
                Writer.Dispose();
        }

        #endregion//Life time

        #region IInspector

        public void LazyRef<T>(T value, SetProp<T> setValue) where T : class
        {
        }

        public void Field(ref Boolean value)
        {
            Writer.Write(value);
        }

        public void Field(ref Byte value)
        {
            Writer.Write(value);
        }

        public void Field(ref Char value)
        {
            Writer.Write(value);
        }

        public void Field(ref Decimal value)
        {
            Writer.Write(value);
        }

        public void Field(ref Double value)
        {
            Writer.Write(value);
        }

        public void Field(ref Int16 value)
        {
            Writer.Write(value);
        }

        public void Field(ref Int32 value)
        {
            Writer.Write(value);
        }

        public void Field(ref Int64 value)
        {
            Writer.Write(value);
        }

        public void Field(ref SByte value)
        {
            Writer.Write(value);
        }

        public void Field(ref Single value)
        {
            Writer.Write(value);
        }

        public void Field(ref String value)
        {
            var w = Writer;
            if (value == null)
            {
                w.Write((int)-1);
                return;
            }
            int index;
            if (StringPool.TryGetValue(value, out index))
            {
                w.Write(index);
                return;
            }
            StringPool.Add(value, -2 - StringPool.Count);
            WriteNonNullString(value);
        }

        public void Field(ref UInt16 value)
        {
            Writer.Write(value);
        }

        public void Field(ref UInt32 value)
        {
            Writer.Write(value);
        }

        public void Field(ref UInt64 value)
        {
            Writer.Write(value);
        }

        public void Field(ref TimeSpan value)
        {
            Writer.Write(value.Ticks);
        }

        public void Field(ref DateTime value)
        {
            Writer.Write(value.Ticks);
            Writer.Write((Byte)value.Kind);
        }

        public void Field(ref TimeOnly value)
        {
            Writer.Write(value.Ticks);
        }

        public void Field(ref DateOnly value)
        {
            Writer.Write(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks);
        }

        public void Field(ref DateTimeOffset value)
        {
            Writer.Write(value.Ticks);
            var o = value.Offset.Ticks / TimeSpan.TicksPerMinute;
#if DEBUG
            if ((o > Int32.MaxValue) || (o < Int32.MinValue))
                throw new Exception("Invalid tick!");
#endif//DEBUG
            Writer.Write((Int32)o);
        }

        public void Field(ref Guid value)
        {
            Writer.Write(value.ToByteArray());
        }

        public void Field<T>(ref T value)
        {
            var handler = TypeHandlerCache<T>.GetHandler(ref value);
            handler.Field(this, ref value);
        }

        public void Prop(Boolean value, SetProp<Boolean> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Byte value, SetProp<Byte> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Char value, SetProp<Char> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Decimal value, SetProp<Decimal> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Double value, SetProp<Double> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Int16 value, SetProp<Int16> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Int32 value, SetProp<Int32> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Int64 value, SetProp<Int64> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(SByte value, SetProp<SByte> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(Single value, SetProp<Single> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(String value, SetProp<String> onSet)
        {
            var w = Writer;
            if (value == null)
            {
                w.Write((int)-1);
                return;
            }
            int index;
            if (StringPool.TryGetValue(value, out index))
            {
                w.Write(index);
                return;
            }
            StringPool.Add(value, -2 - StringPool.Count);
            WriteNonNullString(value);
        }

        public void Prop(UInt16 value, SetProp<UInt16> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(UInt32 value, SetProp<UInt32> onSet)
        {
            Writer.Write(value);
        }

        public void Prop(UInt64 value, SetProp<UInt64> onSet)
        {
            Writer.Write(value);
        }


        public void Prop(TimeSpan value, SetProp<TimeSpan> onSet)
        {
            Writer.Write(value.Ticks);
        }

        public void Prop(DateTime value, SetProp<DateTime> onSet)
        {
            Writer.Write(value.Ticks);
            Writer.Write((Byte)value.Kind);
        }

        public void Prop(TimeOnly value, SetProp<TimeOnly> onSet)
        {
            Writer.Write(value.Ticks);
        }

        public void Prop(DateOnly value, SetProp<DateOnly> onSet)
        {
            Writer.Write(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks);
        }

        public void Prop(DateTimeOffset value, SetProp<DateTimeOffset> onSet)
        {
            Writer.Write(value.Ticks);
        }

        public void Prop(Guid value, SetProp<Guid> onSet)
        {
            Writer.Write(value.ToByteArray());
        }

        public void Prop<T>(T value, SetProp<T> setValue)
        {
            var handler = TypeHandlerCache<T>.GetHandler(value);
            handler.Prop(this, ref value, setValue);
        }

        public void OnNew<T>(T value, bool replaceLast)
        {
            throw new Exception("Internal error!");
        }

        public void ParentField<T>(ref T value) where T : class
        {
        }

        public void ParentProp<T>(T value, SetProp<T> setValue) where T : class
        {
        }

        public void UnmanagedMemory(ref IntPtr data, ref int length, ref Action disposeAction)
        {
            StaticTypeHandler.HandleUnmanagedMemory(this, ref data, ref length, ref disposeAction);
        }

#endregion//IInspector

        #region IInspectorHandler

        readonly Dictionary<Object, int> Objects = new Dictionary<Object, int>(1024);
        readonly Dictionary<Type, int> Types = new Dictionary<Type, int>(1024);
        readonly Dictionary<String, int> StringPool = new Dictionary<string, int>(StringComparer.Ordinal);

        public void Field_Object<T>(TypeHandler<T> context, ref T value)
        {
            var v = value;
            if (v == null)
            {
                Writer.Write((int)-1);
                return;
            }
            int index;
            if (Objects.TryGetValue((Object)v, out index))
            {
                Writer.Write(index);
                return;
            }
            Writer.Write(context.LatestVersion);
            Objects.Add((Object)v, -2 - Objects.Count);
            InternalStack.Push(v);
            context.Describe(this, ref value, context.LatestVersion);
            InternalStack.Pop();

        }

        public void Prop_Object<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            var v = value;
            if (v == null)
            {
                Writer.Write((int)-1);
                return;
            }
            int index;
            if (Objects.TryGetValue((Object)v, out index))
            {
                Writer.Write(index);
                return;
            }
            Writer.Write(context.LatestVersion);
            Objects.Add((Object)v, -2 - Objects.Count);
            InternalStack.Push(v);
            context.Describe(this, ref value, context.LatestVersion);
            InternalStack.Pop();
        }


        Byte[] StringBuffer;

        void WriteNonNullString(String v)
        {
            var l = v.Length;
            var buf = StringBuffer;
            var bl = (l + l) + 512;
            var w = Writer;
            if ((buf == null) || (buf.Length < bl))
            {
                buf = new byte[l + 1024];
                StringBuffer = buf;
            }
            int count = Encoding.GetBytes(v, 0, l, buf, 0);
            w.Write(count);
            w.Write(buf, 0, count);
        }

        void WriteType(Type t)
        {
            int index;
            if (Types.TryGetValue(t, out index))
            {
                Writer.Write(index);
                return;
            }
            Types.Add(t, Types.Count + 1);
            Writer.Write(0);
            WriteNonNullString(StaticTypeHandler.GetTypename(t, TypenameQualification));
        }
        public void Field_TypedObject<T, F>(TypeHandler<T> context, ref T value)
        {
            var v = value;
            if (v == null)
            {
                Writer.Write((int)-1);
                return;
            }
            var type = typeof(F);
            var o = Objects;
            int index;
            if (o.TryGetValue((Object)v, out index))
            {
                Writer.Write(index);
                return;
            }
            var handler = TypeHandlerCache<T>.GetHandler(type);
            Writer.Write(handler.LatestVersion | 0x40000000);
            WriteType(type);
            o.Add((Object)v, -2 - o.Count);
            InternalStack.Push(v);
            handler.Describe(this, ref value, handler.LatestVersion);
            InternalStack.Pop();
        }

        public void Prop_TypedObject<T, F>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            var v = value;
            if (v == null)
            {
                Writer.Write((int)-1);
                return;
            }
            var type = typeof(F);
            var o = Objects;
            int index;
            if (o.TryGetValue((Object)v, out index))
            {
                Writer.Write(index);
                return;
            }
            var handler = TypeHandlerCache<T>.GetHandler(type);
            Writer.Write(handler.LatestVersion | 0x40000000);
            WriteType(typeof(F));
            o.Add((Object)v, -2 - o.Count);
            InternalStack.Push(v);
            handler.Describe(this, ref value, handler.LatestVersion);
            InternalStack.Pop();
        }



        public void Field_Value<T>(TypeHandler<T> context, ref T value)
        {
            Writer.Write(context.LatestVersion);
            context.Describe(this, ref value, context.LatestVersion);
        }

        public void Prop_Value<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            Writer.Write(context.LatestVersion);
            context.Describe(this, ref value, context.LatestVersion);
        }

        public void Field_NullableValue<T>(TypeHandler<T> context, ref T value)
        {
            if (value == null)
            {
                Writer.Write((int)-1);
                return;
            }
            Writer.Write(context.LatestVersion);
            context.Describe(this, ref value, context.LatestVersion);
        }

        public void Prop_NullableValue<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            if (value == null)
            {
                Writer.Write((int)-1);
                return;
            }
            Writer.Write(context.LatestVersion);
            context.Describe(this, ref value, context.LatestVersion);
        }

        public Stack<Object> Stack
        {
            get
            {
                return InternalStack;
            }
        }
        
        readonly Stack<Object> InternalStack = new Stack<object>();

        #region Array

        public void Array_Begin(int[] dimensions)
        {
            foreach (var x in dimensions)
                Writer.Write(x);
        }

        public void Array_ByteArray(int length, ref Byte[] value)
        {
            Writer.Write(value);
        }

        public void Array_LevelUp(int rank)
        {
        }

        public void Array_LevelDown(int rank)
        {
        }


        #endregion//Array

        #endregion//IInspectorHandler

        #region Helpers

        public void Write<T>(T obj, bool saveAsObject = true)
        {
            if (saveAsObject)
                Prop((Object)obj, null);
            else
                Prop(obj, null);
        }


        public static void Write<T>(T obj, Stream s, Encoding encoding, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryWriterInspector(s, encoding, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                insp.Write(obj);
            }
        }

        public static void Write<T>(T obj, Stream s, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryWriterInspector(s, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                insp.Write(obj);
            }
        }

        public static void Write<T>(T obj, BinaryWriter writer, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryWriterInspector(writer, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                insp.Write(obj);
            }
        }

        #endregion//Helpers


    }

}

