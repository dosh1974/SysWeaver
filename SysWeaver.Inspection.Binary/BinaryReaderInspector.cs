using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using SysWeaver.Inspection.Implementation;


namespace SysWeaver.Inspection
{

    public sealed class BinaryReaderInspector : IInspectorImplementation, IReadInspector, IDisposable
    {
        #region Life time

        public BinaryReaderInspector(Stream s, Endianess endian, Encoding encoding, bool leaveOpen = false)
            : this(EndianAwareBinaryReader.Open(s, endian, encoding, leaveOpen), false)
        {
            Encoding = encoding;
        }

        public BinaryReaderInspector(Stream s, Endianess endian, bool leaveOpen = false)
            : this(EndianAwareBinaryReader.Open(s, endian, Encoding.Unicode, leaveOpen), false)
        {
            Encoding = Encoding.Unicode;
        }

        public BinaryReaderInspector(Stream s, Encoding encoding, bool leaveOpen = false)
            : this(EndianAwareBinaryReader.Open(s, Endianess.Current, encoding, leaveOpen), false)
        {
            Encoding = encoding;
        }

        public BinaryReaderInspector(Stream s, bool leaveOpen = false)
            : this(s, Endianess.Current, Encoding.Unicode, leaveOpen)
        {
            Encoding = Encoding.Unicode;
        }

        public BinaryReaderInspector(BinaryReader reader, bool leaveOpen = false)
        {
            Reader = reader;
            LeaveOpen = leaveOpen;
            Objects.Add(null);
            Encoding = Encoding.Unicode;
        }

        readonly Encoding Encoding;

        public Dictionary<String, Object> Context
        {
            get
            {
                return InternalContext;
            }
        }
        readonly Dictionary<String, Object> InternalContext = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly BinaryReader Reader;
        readonly bool LeaveOpen;

        public void Dispose()
        {
            if (!LeaveOpen)
                Reader.Dispose();
        }


        #endregion//Life time

        #region IInspector

        public void LazyRef<T>(T value, SetProp<T> setValue) where T : class
        {
        }

        public void Field(ref Boolean value)
        {
            value = Reader.ReadBoolean();
        }

        public void Field(ref Byte value)
        {
            value = Reader.ReadByte();
        }

        public void Field(ref Char value)
        {
            value = Reader.ReadChar();
        }

        public void Field(ref Decimal value)
        {
            value = Reader.ReadDecimal();
        }

        public void Field(ref Double value)
        {
            value = Reader.ReadDouble();
        }

        public void Field(ref Int16 value)
        {
            value = Reader.ReadInt16();
        }

        public void Field(ref Int32 value)
        {
            value = Reader.ReadInt32();
        }

        public void Field(ref Int64 value)
        {
            value = Reader.ReadInt64();
        }

        public void Field(ref SByte value)
        {
            value = Reader.ReadSByte();
        }

        public void Field(ref Single value)
        {
            value = Reader.ReadSingle();
        }

        public void Field(ref String value)
        {
            int l = Reader.ReadInt32();
            if (l < 0)
            {
                value = Strings[-l - 1];
                return;
            }
            value = ReadNonNullString(l);
            Strings.Add(value);
        }

        public void Field(ref UInt16 value)
        {
            value = Reader.ReadUInt16();
        }

        public void Field(ref UInt32 value)
        {
            value = Reader.ReadUInt32();
        }

        public void Field(ref UInt64 value)
        {
            value = Reader.ReadUInt64();
        }

        public void Field(ref TimeSpan value)
        {
            value = TimeSpan.FromTicks(Reader.ReadInt64());
        }

        public void Field(ref DateTime value)
        {
            var ticks = Reader.ReadInt64();
            var kind = Reader.ReadByte();
            value = new DateTime(ticks, (DateTimeKind)kind);
        }

        public void Field(ref TimeOnly value)
        {
            var ticks = Reader.ReadInt64();
            value = new TimeOnly(ticks);
        }

        public void Field(ref DateOnly value)
        {
            var ticks = Reader.ReadInt64();
            value = DateOnly.FromDateTime(new DateTime(ticks, DateTimeKind.Utc));
        }

        public void Field(ref DateTimeOffset value)
        {
            var ticks = Reader.ReadInt64();
            var ticksOffset = Reader.ReadInt32() * TimeSpan.TicksPerMinute;
            value = new DateTimeOffset(ticks, new TimeSpan(ticksOffset));
        }

        public void Field(ref Guid value)
        {
            value = new Guid(Reader.ReadBytes(16));
        }

        public void Field<T>(ref T value)
        {
            var handler = TypeHandlerCache<T>.GetHandler(ref value);
            handler.Field(this, ref value);
        }

        public void Prop(Boolean value, SetProp<Boolean> onSet)
        {
            var v = Reader.ReadBoolean();
            if (v != value)
                onSet(v);
        }

        public void Prop(Byte value, SetProp<Byte> onSet)
        {
            var v = Reader.ReadByte();
            if (v != value)
                onSet(v);
        }

        public void Prop(Char value, SetProp<Char> onSet)
        {
            var v = Reader.ReadChar();
            if (v != value)
                onSet(v);
        }

        public void Prop(Decimal value, SetProp<Decimal> onSet)
        {
            var v = Reader.ReadDecimal();
            if (v != value)
                onSet(v);
        }

        public void Prop(Double value, SetProp<Double> onSet)
        {
            var v = Reader.ReadDouble();
            if (v != value)
                onSet(v);
        }

        public void Prop(Int16 value, SetProp<Int16> onSet)
        {
            var v = Reader.ReadInt16();
            if (v != value)
                onSet(v);
        }

        public void Prop(Int32 value, SetProp<Int32> onSet)
        {
            var v = Reader.ReadInt32();
            if (v != value)
                onSet(v);
        }

        public void Prop(Int64 value, SetProp<Int64> onSet)
        {
            var v = Reader.ReadInt64();
            if (v != value)
                onSet(v);
        }

        public void Prop(SByte value, SetProp<SByte> onSet)
        {
            var v = Reader.ReadSByte();
            if (v != value)
                onSet(v);
        }

        public void Prop(Single value, SetProp<Single> onSet)
        {
            var v = Reader.ReadSingle();
            if (v != value)
                onSet(v);
        }

        public void Prop(String value, SetProp<String> onSet)
        {
            String v = null;
            int l = Reader.ReadInt32();
            if (l < 0)
            {
                v = Strings[-l - 1];
                if (v != value)
                    onSet(v);
                return;
            }
            v = ReadNonNullString(l);
            Strings.Add(v);
            if (v != value)
                onSet(v);
        }

        public void Prop(UInt16 value, SetProp<UInt16> onSet)
        {
            var v = Reader.ReadUInt16();
            if (v != value)
                onSet(v);
        }

        public void Prop(UInt32 value, SetProp<UInt32> onSet)
        {
            var v = Reader.ReadUInt32();
            if (v != value)
                onSet(v);
        }

        public void Prop(UInt64 value, SetProp<UInt64> onSet)
        {
            var v = Reader.ReadUInt64();
            if (v != value)
                onSet(v);
        }

        public void Prop(TimeSpan value, SetProp<TimeSpan> onSet)
        {
            var v = TimeSpan.FromTicks(Reader.ReadInt64());
            if (v != value)
                onSet(v);
        }

        public void Prop(DateTime value, SetProp<DateTime> onSet)
        {
            var ticks = Reader.ReadInt64();
            var kind = Reader.ReadByte();
            var v = new DateTime(ticks, (DateTimeKind)kind);
            if (v != value)
                onSet(v);
        }

        public void Prop(TimeOnly value, SetProp<TimeOnly> onSet)
        {
            var ticks = Reader.ReadInt64();
            var v = new TimeOnly(ticks);
            if (v != value)
                onSet(v);
        }

        public void Prop(DateOnly value, SetProp<DateOnly> onSet)
        {
            var ticks = Reader.ReadInt64();
            var v = DateOnly.FromDateTime(new DateTime(ticks, DateTimeKind.Utc));
            if (v != value)
                onSet(v);

        }

        public void Prop(DateTimeOffset value, SetProp<DateTimeOffset> onSet)
        {
            var ticks = Reader.ReadInt64();
            var v = new DateTimeOffset(ticks, TimeSpan.Zero);
            if (v != value)
                onSet(v);
        }

        public void Prop(Guid value, SetProp<Guid> onSet)
        {
            var v = new Guid(Reader.ReadBytes(16));
            if (v != value)
                onSet(v);
        }

        public void Prop<T>(T value, SetProp<T> setValue)
        {
            var handler = TypeHandlerCache<T>.GetHandler(value);
            handler.Prop(this, ref value, setValue);
        }

        public void OnNew<T>(T value, bool replaceLast)
        {
            if (replaceLast)
            {
                Objects[Objects.Count - 1] = (Object)value;
                if (value.GetType().GetTypeInfo().IsClass)
                    InternalStack.Push(value);
                return;
            }
            Objects.Add((Object)value);
            if (value.GetType().GetTypeInfo().IsClass)
                InternalStack.Push(value);
        }


        public void ParentField<T>(ref T value) where T : class
        {
            var t = typeof(T);
            foreach (var o in InternalStack)
            {
                if (t.GetTypeInfo().IsAssignableFrom(o.GetType().GetTypeInfo()))
                {
                    value = (T)o;
                    return;
                }
            }
            value = null;
        }

        public void ParentProp<T>(T value, SetProp<T> setValue) where T : class
        {
            var t = typeof(T);
            foreach (var o in InternalStack)
            {
                if (t.GetTypeInfo().IsAssignableFrom(o.GetType().GetTypeInfo()))
                {
                    setValue((T)o);
                    return;
                }
            }
            setValue(null);
        }


        public void UnmanagedMemory(ref IntPtr data, ref int length, ref Action disposeAction)
        {
            StaticTypeHandler.HandleUnmanagedMemory(this, ref data, ref length, ref disposeAction);
        }


        #endregion//IInspector

        #region IInspectorHandler

        readonly List<String> Strings = new List<string>(1024) { null };
        readonly List<Object> Objects = new List<Object>(1024); 
        readonly List<Type> Types = new List<Type>(1024);

        String ReadNonNullString()
        {
            int l = Reader.ReadInt32();
            return ReadNonNullString(l);
        }


        Byte[] StringBuf;

        String ReadNonNullString(int l)
        {
            var buf = StringBuf;
            if ((buf == null) || (buf.Length < l))
            {
                buf = new byte[l + 1024];
                StringBuf = buf;
            }
            l = Reader.Read(buf, 0, l);
            return Encoding.GetString(buf, 0, l);
        }

        Type ReadType<T>()
        {
            int index = Reader.ReadInt32();
            if (index > 0)
                return Types[index - 1];
            String typeName = ReadNonNullString();
            var type = TypeFinder.Get(typeName);
            Types.Add(type);
            return type;
        }


        public void Field_Object<T>(TypeHandler<T> context, ref T value)
        {
            int version = Reader.ReadInt32();
            if (version < 0)
            {
                value = (T)Objects[-1 - version];
                return;
            }
            bool typed = (version & 0x40000000) != 0;
            version &= ~0x40000000;
            var v = value;
            var o = Objects;
            if (typed)
            {
                var type = ReadType<T>();
                var handler = TypeHandlerCache<T>.GetHandler(type);
                if (version > handler.LatestVersion)
                    StaticTypeHandler.ThrowInvalidVersion(version, handler.LatestVersion, type);
#if REUSE_OBJECT
                if ((v != null) && (v.GetType() == type))
                {
                    o.Add(v);
                    InternalStack.Push(v);
                    handler.Describe(this, ref value, version);
                    InternalStack.Pop();
                    return;
                }
#endif//REUSE_OBJECT

                value = handler.Create(this, version, version == handler.LatestVersion);
                o.Add(value);
                //InternalStack.Pop();
                return;
            }
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
#if REUSE_OBJECT
            if (v != null)
            {
                o.Add(v);
                InternalStack.Push(v);
                context.Describe(this, ref value, version);
                InternalStack.Pop();
                return;
            }
#endif//REUSE_OBJECT
            value = context.Create(this, version, version == context.LatestVersion);
            o.Add(value);
            //InternalStack.Pop();
        }

        public void Prop_Object<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            int version = Reader.ReadInt32();
            if (version < 0)
            {
                value = (T)Objects[-1 - version];
                onSet(value);
                return;
            }
            bool typed = (version & 0x40000000) != 0;
            version &= ~0x40000000;
            var v = value;
            var o = Objects;
            if (typed)
            {
                var type = ReadType<T>();
                var handler = TypeHandlerCache<T>.GetHandler(type);
                if (version > handler.LatestVersion)
                    StaticTypeHandler.ThrowInvalidVersion(version, handler.LatestVersion, type);
#if REUSE_OBJECT
                if ((v != null) && (v.GetType() == type))
                {
                    o.Add(v);
                    InternalStack.Push(v);
                    handler.Describe(this, ref value, version);
                    onSet(value);
                    InternalStack.Pop();
                    return;
                }
#endif//REUSE_OBJECT
                value = handler.Create(this, version, version == handler.LatestVersion);
                onSet(value);
                o.Add(value);
                //InternalStack.Pop();
                return;
            }
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
#if REUSE_OBJECT
            if (v != null)
            {
                o.Add(v);
                InternalStack.Push(v);
                context.Describe(this, ref value, version);
                onSet(value);
                InternalStack.Pop();
                return;
            }
#endif//REUSE_OBJECT
            value = context.Create(this, version, version == context.LatestVersion);
            onSet(value);
            o.Add(value);
            //InternalStack.Pop();
        }

        public void Field_TypedObject<T, F>(TypeHandler<T> context, ref T value)
        {
            Field_Object(context, ref value);
        }

        public void Prop_TypedObject<T, F>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            Prop_Object(context, ref value, onSet);
        }

        public void Field_Value<T>(TypeHandler<T> context, ref T value)
        {
            int version = Reader.ReadInt32();
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
            context.Describe(this, ref value, version);
        }

        public void Prop_Value<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            int version = Reader.ReadInt32();
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
            context.Describe(this, ref value, version);
            onSet(value);
        }

        public void Field_NullableValue<T>(TypeHandler<T> context, ref T value)
        {
            int version = Reader.ReadInt32();
            if (version < 0)
            {
                value = default(T);
                return;
            }
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
#if REUSE_OBJECT
            if (value != null)
            {
                context.Describe(this, ref value, version);
                return;
            }
#endif//REUSE_OBJECT
            value = context.Create(this, version, version == context.LatestVersion);
        }

        public void Prop_NullableValue<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            int version = Reader.ReadInt32();
            if (version < 0)
            {
                value = default(T);
                onSet(value);
                return;
            }
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
#if REUSE_OBJECT
            if (value != null)
            {
                context.Describe(this, ref value, version);
                onSet(value);
                return;
            }
#endif//REUSE_OBJECT
            value = context.Create(this, version, version == context.LatestVersion);
            onSet(value);
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
            for (int i = 0; i < dimensions.Length; ++i)
                dimensions[i] = Reader.ReadInt32();
        }

        public void Array_ByteArray(int length, ref Byte[] value)
        {
            Reader.Read(value, 0, length);
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

        public T Read<T>()
        {
            T t = default(T);
            Field(ref t);
            return t;
        }

        public static T Read<T>(Stream s, Encoding encoding, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, encoding, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<T>();
            }
        }

        public static T Read<T>(Stream s, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<T>();
            }
        }

        public static T Read<T>(BinaryReader reader, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(reader, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<T>();
            }
        }

        public static Object Read(Stream s, Encoding encoding, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, encoding, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<Object>();
            }
        }

        public static Object Read(Stream s, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<Object>();
            }
        }

        public static Object Read(BinaryReader reader, bool disposeWhenDone = true, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(reader, disposeWhenDone))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<Object>();
            }
        }

        #endregion//Helpers

    }

}

