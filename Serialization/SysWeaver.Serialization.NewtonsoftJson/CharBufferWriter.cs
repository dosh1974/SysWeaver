using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;

namespace SysWeaver.Serialization.NewtonsoftJson
{
    /// <summary>
    /// A <see cref="TextWriter"/> that writes into a buffer rented from <see cref="ArrayPool{T}.Shared"/> (returned when disposed), used instead of a <see cref="StringWriter"/> to serialize without a <see cref="StringBuilder"/> and an intermediate string.
    /// The formatting culture is <see cref="CultureInfo.InvariantCulture"/> (like the StringWriter of JsonConvert.SerializeObject).
    /// </summary>
    sealed class CharBufferWriter : TextWriter
    {
        public CharBufferWriter()
            : base(CultureInfo.InvariantCulture)
        {
            Buffer = ArrayPool<Char>.Shared.Rent(1024);
        }

        Char[] Buffer;
        int Pos;

        static readonly Encoding UnicodeEnc = new UnicodeEncoding(false, false);

        /// <summary>
        /// The same as a StringWriter (UTF-16)
        /// </summary>
        public override Encoding Encoding => UnicodeEnc;

        /// <summary>
        /// The text written so far (invalid after Dispose)
        /// </summary>
        public ReadOnlySpan<Char> Written => new ReadOnlySpan<Char>(Buffer, 0, Pos);

        public override void Write(Char value)
        {
            var b = Buffer;
            var p = Pos;
            if ((uint)p >= (uint)b.Length)
            {
                Grow(1);
                b = Buffer;
            }
            b[p] = value;
            Pos = p + 1;
        }

        public override void Write(Char[] buffer, int index, int count)
            => Write(new ReadOnlySpan<Char>(buffer, index, count));

        public override void Write(ReadOnlySpan<Char> buffer)
        {
            var l = buffer.Length;
            if (l > (Buffer.Length - Pos))
                Grow(l);
            buffer.CopyTo(new Span<Char>(Buffer, Pos, l));
            Pos += l;
        }

        public override void Write(String value)
        {
            if (value != null)
                Write(value.AsSpan());
        }

        void Grow(int extra)
        {
            var pool = ArrayPool<Char>.Shared;
            var old = Buffer;
            var n = pool.Rent(Math.Max(old.Length * 2, checked(Pos + extra)));
            new ReadOnlySpan<Char>(old, 0, Pos).CopyTo(n);
            Buffer = n;
            pool.Return(old);
        }

        protected override void Dispose(bool disposing)
        {
            var b = Buffer;
            if (b.Length > 0)
            {
                Buffer = [];
                Pos = 0;
                ArrayPool<Char>.Shared.Return(b);
            }
            base.Dispose(disposing);
        }
    }


    /// <summary>
    /// The Newtonsoft array pool interface on <see cref="ArrayPool{T}.Shared"/> (the internal buffers of a JsonTextWriter)
    /// </summary>
    sealed class SharedCharArrayPool : IArrayPool<Char>
    {
        SharedCharArrayPool()
        {
        }

        public static readonly SharedCharArrayPool Instance = new();

        public Char[] Rent(int minimumLength) => ArrayPool<Char>.Shared.Rent(minimumLength);

        public void Return(Char[] array) => ArrayPool<Char>.Shared.Return(array);
    }

}
