using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using SysWeaver.Inspection.Implementation;
using SysWeaver.IO;


namespace SysWeaver.Inspection
{

    /// <summary>
    /// Reads (creates) objects from data written by a <see cref="BinaryWriterInspector"/>.
    /// Use <see cref="Read{T}()"/> with the same type that was written (or <see cref="Object"/> if the value was written as an object), or one of the static Read helpers.
    /// </summary>
    /// <remarks>
    /// Strings and objects that are written more than once are read as one instance (the writer writes a back reference).
    /// Types named by the data (polymorphic members, values written as an object) are resolved using <see cref="TypeFinder.GetForData"/>, so the <see cref="DataTypePolicy"/> applies and the type must be assignable to the declared type.
    /// Objects are always created (an existing value of a member is replaced, not updated).
    /// The look ups (strings, objects, types) are reused by the next reader on the same thread when the reader is disposed.
    /// Not thread safe.
    /// </remarks>
    public sealed class BinaryReaderInspector : IInspectorImplementation, IReadInspector, IDisposable
    {
        #region Life time

        /// <summary>
        /// Read from a stream.
        /// </summary>
        /// <param name="s">The stream to read from</param>
        /// <param name="endian">The byte order of the data</param>
        /// <param name="encoding">The encoding of strings (chars are always 16 bit code units), must be the same encoding that was used by the writer</param>
        /// <param name="leaveOpen">True to keep the stream open when the reader is disposed</param>
        public BinaryReaderInspector(Stream s, Endianess endian, Encoding encoding, bool leaveOpen = false)
            : this(EndianAwareBinaryReader.Open(s, endian, encoding, leaveOpen), false)
        {
            Encoding = encoding;
        }

        /// <summary>
        /// Read from a stream, strings are UTF-16 (<see cref="Encoding.Unicode"/>, the default of the <see cref="BinaryWriterInspector"/>).
        /// </summary>
        /// <param name="s">The stream to read from</param>
        /// <param name="endian">The byte order of the data</param>
        /// <param name="leaveOpen">True to keep the stream open when the reader is disposed</param>
        public BinaryReaderInspector(Stream s, Endianess endian, bool leaveOpen = false)
            : this(EndianAwareBinaryReader.Open(s, endian, Encoding.Unicode, leaveOpen), false)
        {
            Encoding = Encoding.Unicode;
        }

        /// <summary>
        /// Read from a stream (in the byte order of this machine).
        /// </summary>
        /// <param name="s">The stream to read from</param>
        /// <param name="encoding">The encoding of strings (chars are always 16 bit code units), must be the same encoding that was used by the writer</param>
        /// <param name="leaveOpen">True to keep the stream open when the reader is disposed</param>
        public BinaryReaderInspector(Stream s, Encoding encoding, bool leaveOpen = false)
            : this(EndianAwareBinaryReader.Open(s, Endianess.Current, encoding, leaveOpen), false)
        {
            Encoding = encoding;
        }

        /// <summary>
        /// Read from a stream (in the byte order of this machine), strings are UTF-16 (<see cref="Encoding.Unicode"/>, the default of the <see cref="BinaryWriterInspector"/>).
        /// </summary>
        /// <param name="s">The stream to read from</param>
        /// <param name="leaveOpen">True to keep the stream open when the reader is disposed</param>
        public BinaryReaderInspector(Stream s, bool leaveOpen = false)
            : this(s, Endianess.Current, Encoding.Unicode, leaveOpen)
        {
            Encoding = Encoding.Unicode;
        }

        /// <summary>
        /// Read using a binary reader.
        /// Strings are always decoded as UTF-16 (<see cref="Encoding.Unicode"/>).
        /// </summary>
        /// <param name="reader">The reader to use</param>
        /// <param name="leaveOpen">True to keep the reader open when this inspector is disposed</param>
        /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null</exception>
        public BinaryReaderInspector(BinaryReader reader, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Reader = reader;
            LeaveOpen = leaveOpen;
            Strings.Add(null);
            Objects.Add(null);
            Encoding = Encoding.Unicode;
        }

        readonly Encoding Encoding;

        /// <summary>
        /// User defined context values (case insensitive keys), created on first use
        /// </summary>
        public Dictionary<String, Object> Context
        {
            get
            {
                //  Created on first use (rarely used)
                return InternalContext ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }
        Dictionary<String, Object> InternalContext;
        readonly BinaryReader Reader;
        readonly bool LeaveOpen;

        /// <summary>
        /// Dispose the reader (unless leaveOpen was true) and return the look ups for reuse by the next reader on this thread.
        /// The inspector can't be used after this (may be called more than once).
        /// </summary>
        public void Dispose()
        {
            if (!LeaveOpen)
                Reader.Dispose();
            var s = State;
            if (s != null)
            {
                State = null;
                ReaderState.Return(s);
            }
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
            //  Chars are written as an UInt16 (any char, also a lone surrogate, independent of the encoding)
            value = (Char)Reader.ReadUInt16();
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
            value = ReadGuid();
        }

        /// <summary>
        /// Same as new Guid(Reader.ReadBytes(16)) without allocating (throws the same ArgumentException if the stream ends before 16 bytes are read)
        /// </summary>
        Guid ReadGuid()
        {
            Span<Byte> b = stackalloc Byte[16];
            var r = Reader;
            int o = 0;
            while (o < 16)
            {
                var n = r.Read(b.Slice(o));
                if (n <= 0)
                    break;
                o += n;
            }
            return new Guid(b.Slice(0, o));
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
                onSet?.Invoke(v);
        }

        public void Prop(Byte value, SetProp<Byte> onSet)
        {
            var v = Reader.ReadByte();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Char value, SetProp<Char> onSet)
        {
            var v = (Char)Reader.ReadUInt16();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Decimal value, SetProp<Decimal> onSet)
        {
            var v = Reader.ReadDecimal();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Double value, SetProp<Double> onSet)
        {
            var v = Reader.ReadDouble();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Int16 value, SetProp<Int16> onSet)
        {
            var v = Reader.ReadInt16();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Int32 value, SetProp<Int32> onSet)
        {
            var v = Reader.ReadInt32();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Int64 value, SetProp<Int64> onSet)
        {
            var v = Reader.ReadInt64();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(SByte value, SetProp<SByte> onSet)
        {
            var v = Reader.ReadSByte();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(Single value, SetProp<Single> onSet)
        {
            var v = Reader.ReadSingle();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(String value, SetProp<String> onSet)
        {
            String v = null;
            int l = Reader.ReadInt32();
            if (l < 0)
            {
                v = Strings[-l - 1];
                if (v != value)
                    onSet?.Invoke(v);
                return;
            }
            v = ReadNonNullString(l);
            Strings.Add(v);
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(UInt16 value, SetProp<UInt16> onSet)
        {
            var v = Reader.ReadUInt16();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(UInt32 value, SetProp<UInt32> onSet)
        {
            var v = Reader.ReadUInt32();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(UInt64 value, SetProp<UInt64> onSet)
        {
            var v = Reader.ReadUInt64();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(TimeSpan value, SetProp<TimeSpan> onSet)
        {
            var v = TimeSpan.FromTicks(Reader.ReadInt64());
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(DateTime value, SetProp<DateTime> onSet)
        {
            var ticks = Reader.ReadInt64();
            var kind = Reader.ReadByte();
            var v = new DateTime(ticks, (DateTimeKind)kind);
            //  != ignores the kind
            if ((v != value) || (v.Kind != value.Kind))
                onSet?.Invoke(v);
        }

        public void Prop(TimeOnly value, SetProp<TimeOnly> onSet)
        {
            var ticks = Reader.ReadInt64();
            var v = new TimeOnly(ticks);
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop(DateOnly value, SetProp<DateOnly> onSet)
        {
            var ticks = Reader.ReadInt64();
            var v = DateOnly.FromDateTime(new DateTime(ticks, DateTimeKind.Utc));
            if (v != value)
                onSet?.Invoke(v);

        }

        public void Prop(DateTimeOffset value, SetProp<DateTimeOffset> onSet)
        {
            //  Same as the field (ticks and offset), != ignores the offset
            DateTimeOffset v = default;
            Field(ref v);
            if (!v.EqualsExact(value))
                onSet?.Invoke(v);
        }

        public void Prop(Guid value, SetProp<Guid> onSet)
        {
            var v = ReadGuid();
            if (v != value)
                onSet?.Invoke(v);
        }

        public void Prop<T>(T value, SetProp<T> setValue)
        {
            var handler = TypeHandlerCache<T>.GetHandler(value);
            handler.Prop(this, ref value, setValue);
        }

        /// <summary>
        /// Called by the type handlers when an object (a class created with its parameterless constructor, or an array) has been created, before its members are read.
        /// Fills the object slot reserved by the object read that created it (so back references to it, also from its own members, resolve to it) and pushes it on the <see cref="Stack"/> (popped when the object read completes).
        /// Calls that don't belong to an object read (or a second call for the same object) are ignored, <paramref name="replaceLast"/> is not used.
        /// </summary>
        /// <typeparam name="T">The declared type</typeparam>
        /// <param name="value">The new instance</param>
        /// <param name="replaceLast">Not used</param>
        public void OnNew<T>(T value, bool replaceLast)
        {
            var pos = PendingObject;
            if (pos <= 0)
                return;
            PendingObject = 0;
            Object o = value;
            Objects[pos] = o;
            if (o is not ValueType)
            {
                InternalStack.Push(o);
                TopIsSelf = true;
            }
        }

        /// <summary>
        /// The index of the object slot reserved by the object read in progress, that isn't filled by <see cref="OnNew{T}(T, bool)"/> yet (0 = none)
        /// </summary>
        int PendingObject;

        /// <summary>
        /// True while the object on top of the <see cref="Stack"/> is the object whose own members are being read (it pushed itself in <see cref="OnNew{T}(T, bool)"/>).
        /// It's not a parent of itself, so <see cref="ParentField{T}(ref T)"/> / <see cref="ParentProp{T}(T, SetProp{T})"/> skip it.
        /// Cleared while a nested object or a struct is read (for them the top of the stack is a real parent), and restored after.
        /// </summary>
        bool TopIsSelf;

        /// <summary>
        /// Find the closest parent (an object higher up in the object graph) assignable to T: the objects on the stack, except the object being described itself
        /// </summary>
        T FindParent<T>() where T : class
        {
            var skip = TopIsSelf;
            foreach (var o in InternalStack)
            {
                if (skip)
                {
                    skip = false;
                    continue;
                }
                if (o is T p)
                    return p;
            }
            return null;
        }

        /// <summary>
        /// Assigns the closest parent assignable to T: the innermost object on the <see cref="Stack"/> that isn't the object being described itself (or null).
        /// A struct's parent is the object that contains it (if that object is on the stack).
        /// </summary>
        /// <typeparam name="T">The parent type</typeparam>
        /// <param name="value">Assigned the parent, or null</param>
        public void ParentField<T>(ref T value) where T : class
        {
            value = FindParent<T>();
        }

        /// <summary>
        /// Calls the setter with the closest parent assignable to T: the innermost object on the <see cref="Stack"/> that isn't the object being described itself (or null).
        /// A struct's parent is the object that contains it (if that object is on the stack).
        /// </summary>
        /// <typeparam name="T">The parent type</typeparam>
        /// <param name="value">Not used</param>
        /// <param name="setValue">Called with the parent, or null (may be null)</param>
        public void ParentProp<T>(T value, SetProp<T> setValue) where T : class
        {
            setValue?.Invoke(FindParent<T>());
        }


        public void UnmanagedMemory(ref IntPtr data, ref int length, ref Action disposeAction)
        {
            StaticTypeHandler.HandleUnmanagedMemory(this, ref data, ref length, ref disposeAction);
        }


        #endregion//IInspector

        #region IInspectorHandler

        /// <summary>
        /// The look ups of a reader, reused by the next reader on the same thread when the reader is disposed (cleared, and only if they aren't too big)
        /// </summary>
        sealed class ReaderState
        {
            public readonly List<String> Strings = new List<string>();
            public readonly List<Object> Objects = new List<Object>();
            public readonly List<Type> Types = new List<Type>();
            public readonly Stack<Object> Stack = new Stack<object>();
            public Byte[] StringBuf;

            /// <summary>
            /// Look ups with more entries than this are not reused (they hold a lot of memory)
            /// </summary>
            const int MaxReusedCount = 1 << 16;

            /// <summary>
            /// String buffers larger than this are not reused
            /// </summary>
            const int MaxReusedBuffer = 1 << 16;

            [ThreadStatic]
            static ReaderState Cached;

            /// <summary>
            /// Get the state cached by this thread (no allocation), or a new one
            /// </summary>
            public static ReaderState Rent()
            {
                var s = Cached;
                if (s == null)
                    return new ReaderState();
                Cached = null;
                return s;
            }

            /// <summary>
            /// Clear the state and cache it for the next reader on this thread (unless it's too big)
            /// </summary>
            public static void Return(ReaderState s)
            {
                if ((s.Strings.Count > MaxReusedCount) || (s.Objects.Count > MaxReusedCount) || (s.Types.Count > MaxReusedCount))
                    return;
                s.Strings.Clear();
                s.Objects.Clear();
                s.Types.Clear();
                s.Stack.Clear();
                var b = s.StringBuf;
                if ((b != null) && (b.Length > MaxReusedBuffer))
                    s.StringBuf = null;
                Cached = s;
            }
        }

        /// <summary>
        /// The look ups (null after the reader is disposed)
        /// </summary>
        ReaderState State = ReaderState.Rent();

        List<String> Strings => State.Strings;
        List<Object> Objects => State.Objects;
        List<Type> Types => State.Types;

        String ReadNonNullString()
        {
            int l = Reader.ReadInt32();
            return ReadNonNullString(l);
        }

        /// <summary>
        /// Strings with more bytes than this are validated before a buffer is allocated (the length may be corrupt)
        /// </summary>
        const int MaxUncheckedStringBytes = 1 << 16;

        /// <summary>
        /// Read the bytes of a string and decode them
        /// </summary>
        /// <param name="l">The number of bytes</param>
        /// <returns>The string</returns>
        /// <exception cref="InvalidDataException">The length is negative, or it's more than <see cref="MaxUncheckedStringBytes"/> and larger than the remaining data</exception>
        /// <exception cref="EndOfStreamException">The data ends before all bytes are read</exception>
        String ReadNonNullString(int l)
        {
            var s = State;
            var buf = s.StringBuf;
            //  Unsigned: also true for negative lengths
            if ((buf == null) || ((uint)buf.Length < (uint)l))
            {
                if ((uint)l > MaxUncheckedStringBytes)
                    return ReadLargeString(l);
                buf = GC.AllocateUninitializedArray<Byte>(l + 1024);
                s.StringBuf = buf;
            }
            ReadFully(buf, l);
            return Encoding.GetString(buf, 0, l);
        }

        /// <summary>
        /// Read a string with a length that may be corrupt (negative, or a large length), without allocating a buffer for (much) more bytes than the data has
        /// </summary>
        /// <param name="l">The number of bytes</param>
        /// <returns>The string</returns>
        /// <exception cref="InvalidDataException">The length is negative or larger than the remaining data</exception>
        String ReadLargeString(int l)
        {
            if (l < 0)
                throw new InvalidDataException("Invalid string length " + l + ", the data is corrupt");
            var s = State;
            var r = Reader;
            var st = r.BaseStream;
            if (st.CanSeek)
            {
                //  The binary reader doesn't read ahead, the stream position is the position of the string data
                var left = st.Length - st.Position;
                if (l > left)
                    throw new InvalidDataException("Invalid string length " + l + ", only " + left + " bytes left, the data is corrupt");
                var b = GC.AllocateUninitializedArray<Byte>(l + 1024);
                s.StringBuf = b;
                ReadFully(b, l);
                return Encoding.GetString(b, 0, l);
            }
            //  Unknown length: grow the buffer as the data arrives (the buffer is at most twice the bytes actually read)
            var buf = s.StringBuf;
            if ((buf == null) || (buf.Length < MaxUncheckedStringBytes))
                buf = GC.AllocateUninitializedArray<Byte>(MaxUncheckedStringBytes);
            int o = 0;
            while (o < l)
            {
                if (o == buf.Length)
                {
                    var nb = GC.AllocateUninitializedArray<Byte>((int)Math.Min(l + 1024L, buf.Length * 2L));
                    Buffer.BlockCopy(buf, 0, nb, 0, o);
                    buf = nb;
                }
                var n = r.Read(buf, o, Math.Min(l, buf.Length) - o);
                if (n <= 0)
                    throw new InvalidDataException("Invalid string length " + l + ", the data ended after " + o + " bytes, the data is corrupt");
                o += n;
            }
            s.StringBuf = buf;
            return Encoding.GetString(buf, 0, l);
        }

        /// <summary>
        /// Read exactly count bytes, a single read may return fewer bytes than requested (network streams etc)
        /// </summary>
        /// <param name="buf">The buffer to read to (starting at index 0)</param>
        /// <param name="count">The number of bytes to read</param>
        /// <exception cref="EndOfStreamException">The stream ends before count bytes are read</exception>
        void ReadFully(Byte[] buf, int count)
        {
            var r = Reader;
            int o = 0;
            while (o < count)
            {
                var n = r.Read(buf, o, count - o);
                if (n <= 0)
                    throw new EndOfStreamException("Unexpected end of stream, expected " + count + " bytes, got " + o);
                o += n;
            }
        }

        /// <summary>
        /// Read the type of a typed object (an index of a previously read type, or a type name)
        /// </summary>
        /// <typeparam name="T">The declared type, the named type must be assignable to it</typeparam>
        /// <returns>The type</returns>
        /// <exception cref="InvalidDataException">The named type doesn't exist</exception>
        /// <exception cref="DataTypeNotAllowedException">The named type isn't allowed by the <see cref="DataTypePolicy"/> or isn't assignable to <typeparamref name="T"/></exception>
        Type ReadType<T>()
        {
            int index = Reader.ReadInt32();
            if (index > 0)
                return Types[index - 1];
            String typeName = ReadNonNullString();
            //  The name comes from the data: the data type policy and the declared type applies
            var type = TypeFinder.GetForData(typeName, typeof(T)) ?? throw new InvalidDataException("Unknown type \"" + typeName + "\" (expected a " + typeof(T).FullName + ")");
            Types.Add(type);
            return type;
        }


        /// <summary>
        /// Create an object (not a back reference) and register it at the index the writer gave it.
        /// The slot is reserved before the object is created (the writer numbers an object before its members), and filled by <see cref="OnNew{T}(T, bool)"/> as soon as the object exists
        /// (so back references from its members resolve to it), else when it's created (constructor created objects, boxed values, strings written as objects).
        /// The object pushed by <see cref="OnNew{T}(T, bool)"/> is popped when the object is complete.
        /// <see cref="TopIsSelf"/> is cleared while the object is created (set again by <see cref="OnNew{T}(T, bool)"/>) and restored after.
        /// </summary>
        T CreateObject<T>(TypeHandler<T> handler, int version)
        {
            var o = Objects;
            var pos = o.Count;
            o.Add(null);
            var stack = InternalStack;
            var depth = stack.Count;
            var topIsSelf = TopIsSelf;
            TopIsSelf = false;
            PendingObject = pos;
            var value = handler.Create(this, version, version == handler.LatestVersion);
            PendingObject = 0;
            o[pos] = value;
            if (stack.Count > depth)
                stack.Pop();
            TopIsSelf = topIsSelf;
            return value;
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
#if REUSE_OBJECT
            var v = value;
            var o = Objects;
#endif//REUSE_OBJECT
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

                value = CreateObject(handler, version);
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
            value = CreateObject(context, version);
        }

        public void Prop_Object<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            int version = Reader.ReadInt32();
            if (version < 0)
            {
                value = (T)Objects[-1 - version];
                onSet?.Invoke(value);
                return;
            }
            bool typed = (version & 0x40000000) != 0;
            version &= ~0x40000000;
#if REUSE_OBJECT
            var v = value;
            var o = Objects;
#endif//REUSE_OBJECT
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
                    onSet?.Invoke(value);
                    InternalStack.Pop();
                    return;
                }
#endif//REUSE_OBJECT
                value = CreateObject(handler, version);
                onSet?.Invoke(value);
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
                onSet?.Invoke(value);
                InternalStack.Pop();
                return;
            }
#endif//REUSE_OBJECT
            value = CreateObject(context, version);
            onSet?.Invoke(value);
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
            //  A struct isn't on the stack: the object on top (if any) is its parent
            var topIsSelf = TopIsSelf;
            TopIsSelf = false;
            context.Describe(this, ref value, version);
            TopIsSelf = topIsSelf;
        }

        public void Prop_Value<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            int version = Reader.ReadInt32();
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
            //  A struct isn't on the stack: the object on top (if any) is its parent
            var topIsSelf = TopIsSelf;
            TopIsSelf = false;
            context.Describe(this, ref value, version);
            TopIsSelf = topIsSelf;
            onSet?.Invoke(value);
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
            //  A struct isn't on the stack: the object on top (if any) is its parent
            var topIsSelf = TopIsSelf;
            TopIsSelf = false;
            value = context.Create(this, version, version == context.LatestVersion);
            TopIsSelf = topIsSelf;
        }

        public void Prop_NullableValue<T>(TypeHandler<T> context, ref T value, SetProp<T> onSet)
        {
            int version = Reader.ReadInt32();
            if (version < 0)
            {
                value = default(T);
                onSet?.Invoke(value);
                return;
            }
            if (version > context.LatestVersion)
                StaticTypeHandler.ThrowInvalidVersion(version, context.LatestVersion, typeof(T));
#if REUSE_OBJECT
            if (value != null)
            {
                context.Describe(this, ref value, version);
                onSet?.Invoke(value);
                return;
            }
#endif//REUSE_OBJECT
            //  A struct isn't on the stack: the object on top (if any) is its parent
            var topIsSelf = TopIsSelf;
            TopIsSelf = false;
            value = context.Create(this, version, version == context.LatestVersion);
            TopIsSelf = topIsSelf;
            onSet?.Invoke(value);
        }

        /// <summary>
        /// The objects being read, the innermost on top (used by <see cref="ParentField{T}(ref T)"/> / <see cref="ParentProp{T}(T, SetProp{T})"/>).
        /// An object (a class created with its parameterless constructor, or an array) is pushed when it's created (before its members are read) and popped when it's complete,
        /// so while its own members are read it's on top of the stack (<see cref="ParentField{T}(ref T)"/> skips it: an object isn't its own parent).
        /// Objects created by a constructor (IInspector, int[, bool]) and structs are not on the stack while they are read (their parent is the object on top).
        /// </summary>
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
            for (int i = 0; i < dimensions.Length; ++i)
                dimensions[i] = Reader.ReadInt32();
        }

        public void Array_ByteArray(int length, ref Byte[] value)
        {
            ReadFully(value, length);
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
        /// Read a value written by <see cref="BinaryWriterInspector.Write{T}(T, bool)"/> with the same type (or with T = Object if it was written as an object).
        /// </summary>
        /// <typeparam name="T">The type that was written</typeparam>
        /// <returns>The value (null / default if a null was written)</returns>
        /// <exception cref="EndOfStreamException">The data is truncated</exception>
        /// <exception cref="InvalidDataException">The data names an unknown type, or was written by a newer version of a type</exception>
        /// <exception cref="DataTypeNotAllowedException">The data names a type that isn't allowed by the <see cref="DataTypePolicy"/> or isn't assignable to the declared type</exception>
        public T Read<T>()
        {
            T t = default(T);
            Field(ref t);
            return t;
        }

        /// <summary>
        /// Read a value of type T from a stream (see <see cref="Read{T}()"/>).
        /// </summary>
        /// <typeparam name="T">The type that was written</typeparam>
        /// <param name="s">The stream</param>
        /// <param name="encoding">The encoding of strings (must match the writer)</param>
        /// <param name="leaveOpen">True to keep the stream open</param>
        /// <param name="context">Context values added to the <see cref="Context"/> of the inspector (keys must be unique)</param>
        /// <returns>The value</returns>
        public static T Read<T>(Stream s, Encoding encoding, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, encoding, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<T>();
            }
        }

        /// <summary>
        /// Read a value of type T from a stream with UTF-16 strings (see <see cref="Read{T}()"/>).
        /// </summary>
        /// <typeparam name="T">The type that was written</typeparam>
        /// <param name="s">The stream</param>
        /// <param name="leaveOpen">True to keep the stream open</param>
        /// <param name="context">Context values added to the <see cref="Context"/> of the inspector (keys must be unique)</param>
        /// <returns>The value</returns>
        public static T Read<T>(Stream s, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<T>();
            }
        }

        /// <summary>
        /// Read a value of type T using a binary reader (see <see cref="Read{T}()"/>).
        /// </summary>
        /// <typeparam name="T">The type that was written</typeparam>
        /// <param name="reader">The reader (strings are UTF-16)</param>
        /// <param name="leaveOpen">True to keep the reader open</param>
        /// <param name="context">Context values added to the <see cref="Context"/> of the inspector (keys must be unique)</param>
        /// <returns>The value</returns>
        public static T Read<T>(BinaryReader reader, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(reader, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<T>();
            }
        }

        /// <summary>
        /// Read a value that was written as an object (see <see cref="Read{T}()"/>).
        /// </summary>
        /// <param name="s">The stream</param>
        /// <param name="encoding">The encoding of strings (must match the writer)</param>
        /// <param name="leaveOpen">True to keep the stream open</param>
        /// <param name="context">Context values added to the <see cref="Context"/> of the inspector (keys must be unique)</param>
        /// <returns>The value</returns>
        public static Object Read(Stream s, Encoding encoding, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, encoding, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<Object>();
            }
        }

        /// <summary>
        /// Read a value that was written as an object, with UTF-16 strings (see <see cref="Read{T}()"/>).
        /// </summary>
        /// <param name="s">The stream</param>
        /// <param name="leaveOpen">True to keep the stream open</param>
        /// <param name="context">Context values added to the <see cref="Context"/> of the inspector (keys must be unique)</param>
        /// <returns>The value</returns>
        public static Object Read(Stream s, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(s, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<Object>();
            }
        }

        /// <summary>
        /// Read a value that was written as an object, using a binary reader (see <see cref="Read{T}()"/>).
        /// </summary>
        /// <param name="reader">The reader (strings are UTF-16)</param>
        /// <param name="leaveOpen">True to keep the reader open</param>
        /// <param name="context">Context values added to the <see cref="Context"/> of the inspector (keys must be unique)</param>
        /// <returns>The value</returns>
        public static Object Read(BinaryReader reader, bool leaveOpen = false, params KeyValuePair<String, Object>[] context)
        {
            using (var insp = new BinaryReaderInspector(reader, leaveOpen))
            {
                StaticTypeHandler.AddContexts(insp, context);
                return insp.Read<Object>();
            }
        }

        #endregion//Helpers

    }

}

