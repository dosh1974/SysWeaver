using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// A range of UTF8 bytes used as a dictionary key (member names, see <see cref="MemberLookUp{T}"/>).
    /// Equality is a byte sequence compare, the hash code is the length and the first byte.
    /// </summary>
    /// <remarks>
    /// Instances created with <see cref="Create(string)"/> are interned (cached forever, thread safe) and immutable by convention.
    /// <see cref="JsonParserState.Range"/> is a reusable instance that is pointed at the json data for look ups, it's not thread safe.
    /// </remarks>
    sealed class Utf8Range : IEquatable<Utf8Range>
    {
#if DEBUG
        public override String ToString() => Utf8Parser.UTF8.GetString(Mem.Span);
#endif//DEBUG

        /// <summary>
        /// The UTF8 bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<Byte> AsSpan() => Mem.Span;


        /// <summary>
        /// Decode the bytes to a string.
        /// </summary>
        public String GetString() => Utf8Parser.UTF8.GetString(Mem.Span);

        /// <summary>
        /// Get the (cached) range for the UTF8 encoding of a string.
        /// </summary>
        /// <param name="s">The text, must not be null</param>
        /// <returns>A shared instance, must not be modified</returns>
        public static Utf8Range Create(String s)
        {
            var c = Cache;
            if (c.TryGetValue(s, out var r))
                return r;
            lock (c)
            {
                if (c.TryGetValue(s, out r))
                    return r;
                var d = Utf8Parser.UTF8.GetBytes(s);
                r = new Utf8Range();
                r.Mem = new ReadOnlyMemory<byte>(d, 0, d.Length);
                c[s] = r;
                return r;
            }
        }

        static readonly ConcurrentDictionary<String, Utf8Range> Cache = new ConcurrentDictionary<string, Utf8Range>(StringComparer.Ordinal);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => Equals(obj as Utf8Range);

        /// <summary>
        /// The length and the first byte (throws <see cref="IndexOutOfRangeException"/> for an empty range).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var m = Mem;
            return (m.Length << 8) | m.Span[0];
        }

        /// <summary>
        /// True if the bytes are equal (<paramref name="other"/> must not be null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Utf8Range other) => Mem.Span.SequenceEqual(other.Mem.Span);

        /// <summary>
        /// True if the bytes are equal to <paramref name="om"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<Byte> om) => Mem.Span.SequenceEqual(om);



        /// <summary>
        /// The UTF8 bytes.
        /// </summary>
        public ReadOnlyMemory<Byte> Mem;

        /// <summary>
        /// A new instance referencing the same memory (not a copy of the bytes).
        /// </summary>
        public Utf8Range Clone() => new Utf8Range
        {
            Mem = Mem
        };

    }

}
