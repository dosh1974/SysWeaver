using System;
using System.IO;
using System.Text;
using SysWeaver.Inspection;
using SysWeaver.IO;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Binary serializer for the "swbin" extension using SysWeaver's inspection based binary format (<see cref="BinaryWriterInspector"/> / <see cref="BinaryReaderInspector"/>).
    /// </summary>
    /// <remarks>
    /// Values are written as their static type <c>T</c> (no type information for the root), so they must be read back as the same type.
    /// The <see cref="SerializerOptions"/> are ignored.
    /// </remarks>
    public sealed class SysWeaverBinarySerializer : ISerializerType
    {
        /// <inheritdoc/>
        public string Name => "SysWeaver.Binary";

        /// <inheritdoc/>
        public string Extension => "swbin";

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/x-swbin";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = MimeType;

        /// <inheritdoc/>
        public Encoding Encoding => null;

        /// <inheritdoc/>
        public int Prio => 0;

        SysWeaverBinarySerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new SysWeaverBinarySerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            fixed (byte* bp = data)
                return Read<T>(bp, data.Length);
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
                return Read<T>(bp, data.Length);
        }

        /// <summary>
        /// Read a value from pinned data, using the stream and binary reader cached by this thread (only the inspector and the result are allocated).
        /// After an exception the session is dropped (a new one is created by the next read).
        /// </summary>
        static unsafe T Read<T>(byte* p, int length)
        {
            var s = ReadSession.Rent();
            s.Stream.Reset(p, length);
            T v;
            using (var r = new BinaryReaderInspector(s.Reader, true))
                v = r.Read<T>();
            //  Only reused after a successful read (an exception may leave the reader's decoder in any state), and never keeps the pointer
            s.Stream.Reset(null, 0);
            ReadSession.Return(s);
            return v;
        }

        /// <summary>
        /// A reusable stream (over pinned data) and binary reader (current byte order, UTF-16 strings, the same as a new <see cref="BinaryReaderInspector(Stream, bool)"/>),
        /// one is cached per thread (a nested read on the same thread creates another one).
        /// </summary>
        sealed class ReadSession
        {
            ReadSession()
            {
                Stream = new PointerReadStream();
                Reader = EndianAwareBinaryReader.Open(Stream, Endianess.Current, Encoding.Unicode, true);
            }

            public readonly PointerReadStream Stream;
            public readonly BinaryReader Reader;

            [ThreadStatic]
            static ReadSession Cached;

            public static ReadSession Rent()
            {
                var s = Cached;
                if (s == null)
                    return new ReadSession();
                Cached = null;
                return s;
            }

            public static void Return(ReadSession s) => Cached = s;
        }

        /// <summary>
        /// A read only, seekable stream over pinned memory that can be pointed at new data (an <see cref="UnmanagedMemoryStream"/> can't be reused)
        /// </summary>
        sealed unsafe class PointerReadStream : Stream
        {
            byte* P;
            int L;
            int Pos;

            /// <summary>
            /// Read from other data (from the start), null to forget the pointer
            /// </summary>
            public void Reset(byte* p, int length)
            {
                P = p;
                L = length;
                Pos = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => L;

            public override long Position
            {
                get => Pos;
                set
                {
                    ArgumentOutOfRangeException.ThrowIfNegative(value);
                    Pos = (int)Math.Min(value, L);
                }
            }

            public override int Read(Span<byte> buffer)
            {
                var n = Math.Min(buffer.Length, L - Pos);
                if (n <= 0)
                    return 0;
                new ReadOnlySpan<byte>(P + Pos, n).CopyTo(buffer);
                Pos += n;
                return n;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ValidateBufferArguments(buffer, offset, count);
                return Read(buffer.AsSpan(offset, count));
            }

            public override int ReadByte() => Pos < L ? P[Pos++] : -1;

            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Pos + offset,
                    SeekOrigin.End => L + offset,
                    _ => throw new ArgumentOutOfRangeException(nameof(origin)),
                };
                return Pos;
            }

            public override void Flush()
            {
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            //  Written to a pooled buffer (with a stream and binary writer cached by this thread), only the inspector and the result (of the exact size) are allocated
            var s = WriteSession.Rent();
            Byte[] r;
            try
            {
                using (var w = new BinaryWriterInspector(s.Writer, true))
                    w.Write(obj, false);
                s.Writer.Flush();
                r = s.Stream.ToArray();
            }
            catch
            {
                //  Not reused after an exception (the buffer goes back to the pool)
                s.Stream.Dispose();
                throw;
            }
            WriteSession.Return(s);
            return r;
        }

        /// <summary>
        /// A reusable pooled stream and binary writer (UTF-16 strings, the same as a new <see cref="BinaryWriterInspector(Stream, bool)"/>),
        /// one is cached per thread (a nested write on the same thread creates another one).
        /// </summary>
        sealed class WriteSession
        {
            WriteSession()
            {
                Stream = new ArrayPoolStream();
                Writer = new BinaryWriter(Stream, Encoding.Unicode, true);
            }

            public readonly ArrayPoolStream Stream;
            public readonly BinaryWriter Writer;

            /// <summary>
            /// A session that used a larger buffer than this isn't kept (the buffer goes back to the pool)
            /// </summary>
            const int MaxKeptBuffer = 1 << 16;

            [ThreadStatic]
            static WriteSession Cached;

            public static WriteSession Rent()
            {
                var s = Cached;
                if (s == null)
                    return new WriteSession();
                Cached = null;
                return s;
            }

            public static void Return(WriteSession s)
            {
                var st = s.Stream;
                if ((st.Data?.Length ?? 0) > MaxKeptBuffer)
                {
                    st.Dispose();
                    return;
                }
                st.SetLength(0);
                Cached = s;
            }
        }
    }


}
