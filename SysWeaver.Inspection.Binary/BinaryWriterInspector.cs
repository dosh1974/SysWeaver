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
                //  Created on first use (rarely used)
                return InternalContext ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }
        Dictionary<String, Object> InternalContext;

        public TypenameQualifications TypenameQualification = TypenameQualifications.Assembly;

        public void Dispose()
        {
            if (!LeaveOpen)
                Writer.Dispose();
            var s = State;
            if (s != null)
            {
                State = null;
                WriterState.Return(s);
            }
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
            //  The same bytes as ToByteArray
            Span<Byte> b = stackalloc Byte[16];
            value.TryWriteBytes(b);
            Writer.Write(b);
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
            //  Same as the field (ticks and offset)
            Field(ref value);
        }

        public void Prop(Guid value, SetProp<Guid> onSet)
        {
            //  The same bytes as ToByteArray
            Span<Byte> b = stackalloc Byte[16];
            value.TryWriteBytes(b);
            Writer.Write(b);
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

        /// <summary>
        /// The look ups of a writer, reused by the next writer on the same thread when the writer is disposed (cleared, and only if they aren't too big)
        /// </summary>
        sealed class WriterState
        {
            public readonly Dictionary<Object, int> Objects = new Dictionary<Object, int>();
            public readonly Dictionary<Type, int> Types = new Dictionary<Type, int>();
            public readonly Dictionary<String, int> StringPool = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Stack<Object> Stack = new Stack<object>();
            public Byte[] StringBuffer;

            /// <summary>
            /// Look ups with more entries than this are not reused (clearing them is slow and they hold a lot of memory)
            /// </summary>
            const int MaxReusedCount = 4096;

            /// <summary>
            /// String buffers larger than this are not reused
            /// </summary>
            const int MaxReusedBuffer = 1 << 16;

            [ThreadStatic]
            static WriterState Cached;

            /// <summary>
            /// Get the state cached by this thread (no allocation), or a new one
            /// </summary>
            public static WriterState Rent()
            {
                var s = Cached;
                if (s == null)
                    return new WriterState();
                Cached = null;
                return s;
            }

            /// <summary>
            /// Clear the state and cache it for the next writer on this thread (unless it's too big)
            /// </summary>
            public static void Return(WriterState s)
            {
                if ((s.Objects.Count > MaxReusedCount) || (s.Types.Count > MaxReusedCount) || (s.StringPool.Count > MaxReusedCount))
                    return;
                s.Objects.Clear();
                s.Types.Clear();
                s.StringPool.Clear();
                s.Stack.Clear();
                var b = s.StringBuffer;
                if ((b != null) && (b.Length > MaxReusedBuffer))
                    s.StringBuffer = null;
                Cached = s;
            }
        }

        /// <summary>
        /// The look ups (null after the writer is disposed)
        /// </summary>
        WriterState State = WriterState.Rent();

        Dictionary<Object, int> Objects => State.Objects;
        Dictionary<Type, int> Types => State.Types;
        Dictionary<String, int> StringPool => State.StringPool;

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


        void WriteNonNullString(String v)
        {
            var l = v.Length;
            var s = State;
            var buf = s.StringBuffer;
            //  The worst case size for the encoding (any encoding can be passed to the constructor)
            var bl = Encoding.GetMaxByteCount(l);
            var w = Writer;
            if ((buf == null) || (buf.Length < bl))
            {
                //  Some slack, so that slightly longer strings don't allocate again
                buf = GC.AllocateUninitializedArray<Byte>(bl + 512);
                s.StringBuffer = buf;
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
        
        Stack<Object> InternalStack => State.Stack;

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

        /// <summary>
        /// Write a value, read it using <see cref="BinaryReaderInspector.Read{T}()"/> with the same T, or with T = Object if saved as an object.
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="obj">The value</param>
        /// <param name="saveAsObject">If true the value is written as an Object (with type information), the same as writing it with T = Object</param>
        public void Write<T>(T obj, bool saveAsObject = false)
        {
            //  Field, as BinaryReaderInspector.Read, the typed Prop and Field overloads doesn't write the same data for all types (ex: DateTimeOffset)
            if (saveAsObject)
            {
                Object o = obj;
                Field(ref o);
            }
            else
            {
                Field(ref obj);
            }
        }


        public static void Write<T>(T obj, Stream s, Encoding encoding, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryWriterInspector(s, encoding, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                insp.Write(obj);
            }
        }

        public static void Write<T>(T obj, Stream s, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryWriterInspector(s, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                insp.Write(obj);
            }
        }

        public static void Write<T>(T obj, BinaryWriter writer, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryWriterInspector(writer, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                insp.Write(obj);
            }
        }

        #endregion//Helpers


    }

}

