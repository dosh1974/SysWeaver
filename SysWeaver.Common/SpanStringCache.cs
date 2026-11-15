using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// A small, bounded and lock free cache of short strings, so that parsers that create strings from spans (json keys and values, binary data)
    /// allocate a value that repeats (keys, names, codes, status words) only once instead of once per read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two direct mapped tables (one for ASCII bytes, one for chars) of <see cref="Slots"/> strings each: the hash of the content picks one slot, a hit compares the whole content
    /// (a collision is only a miss, never a wrong string), a miss allocates the string. A string is only cached the second time its hash is the miss of its slot,
    /// so strings that never repeat (ids, tokens, guids) are never kept alive or promoted by the GC.
    /// </para>
    /// <para>
    /// Thread safe without locks (a reference or an int is read and written atomically, and a string never changes), the memory used is bounded
    /// (at most 2 x <see cref="Slots"/> strings of at most <see cref="MaxLength"/> chars).
    /// </para>
    /// <para>
    /// Why: allocations are slower on .NET 11 than on .NET 10 (6 - 15 % for small objects), a look up is cheaper than allocating the same short string.
    /// Measured on json reading: 20 - 35 % fewer allocated bytes for typical documents, about 4 % slower for a document of only unique strings (the worst case).
    /// </para>
    /// <para>
    /// A cached string stays reachable until its slot is reused, set <see cref="Enabled"/> to false to never keep a parsed string alive after the parse result is gone
    /// (ex: if parsed data contains secrets that must not stay in memory longer than the result).
    /// </para>
    /// </remarks>
    public static unsafe class SpanStringCache
    {
        /// <summary>
        /// True (the default) to use the cache, false to always allocate a new string (the users of the cache test this before calling it).
        /// Used by the SysWeaver json reader (<c>JsonReader.CacheStrings</c> is the same setting) and the binary reader (<c>BinaryReaderInspector</c>).
        /// </summary>
        public static bool Enabled = true;

        /// <summary>
        /// Strings longer than this (in chars) are never cached, callers should not call the cache for longer content (it's not checked)
        /// </summary>
        public const int MaxLength = 32;

        /// <summary>
        /// The number of slots per table (a power of two)
        /// </summary>
        public const int Slots = 4096;

        const int Mask = Slots - 1;

        static readonly String[] AsciiCache = new String[Slots];
        static readonly uint[] AsciiSeen = new uint[Slots];
        static readonly String[] CharCache = new String[Slots];
        static readonly uint[] CharSeen = new uint[Slots];

        /// <summary>
        /// Get a string with the content of some ASCII bytes (char codes less than 128), from the cache if possible.
        /// </summary>
        /// <param name="p">The first byte</param>
        /// <param name="len">The number of bytes, 1 to <see cref="MaxLength"/> (not checked)</param>
        /// <returns>The string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String GetAscii(Byte* p, int len)
        {
            var h = Hash(p, len);
            var s = AsciiCache[h & Mask];
            if ((s != null) && (s.Length == len) && Ascii.Equals(new ReadOnlySpan<Byte>(p, len), s))
                return s;
            return AsciiMiss(h, p, len);
        }

        /// <summary>
        /// Get a string with the content of some ASCII bytes (char codes less than 128), from the cache if possible.
        /// </summary>
        /// <param name="ascii">The bytes, 1 to <see cref="MaxLength"/> (not checked)</param>
        /// <returns>The string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String GetAscii(ReadOnlySpan<Byte> ascii)
        {
            fixed (Byte* p = ascii)
                return GetAscii(p, ascii.Length);
        }

        /// <summary>
        /// Get a string with the content of some chars, from the cache if possible.
        /// </summary>
        /// <param name="chars">The chars, 1 to <see cref="MaxLength"/> (not checked)</param>
        /// <returns>The string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String Get(ReadOnlySpan<Char> chars)
        {
            var len = chars.Length;
            fixed (Char* p = chars)
            {
                var h = Hash((Byte*)p, len << 1);
                var s = CharCache[h & Mask];
                if ((s != null) && chars.SequenceEqual(s))
                    return s;
                return CharMiss(h, p, len);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static String AsciiMiss(uint h, Byte* p, int len)
        {
            var s = String.Create(len, new IntPtr(p), WriteAscii);
            Admit(AsciiCache, AsciiSeen, h, s);
            return s;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static String CharMiss(uint h, Char* p, int len)
        {
            var s = new String(p, 0, len);
            Admit(CharCache, CharSeen, h, s);
            return s;
        }

        /// <summary>
        /// Cache the string if the same hash was the previous miss of the slot, else remember the hash
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Admit(String[] cache, uint[] seen, uint h, String s)
        {
            var i = (int)(h & Mask);
            ref var tag = ref seen[i];
            //  h | 1: a zero hash would match an unused slot
            var t = h | 1;
            if (tag == t)
                cache[i] = s;
            else
                tag = t;
        }

        static readonly System.Buffers.SpanAction<Char, IntPtr> WriteAscii = static (span, p) =>
            Ascii.ToUtf16(new ReadOnlySpan<Byte>((Byte*)p, span.Length), span, out _);

        /// <summary>
        /// A fast hash of 1 to 64 bytes: the first 8, the last 8 and (for longer data) the middle bytes, and the length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Hash(Byte* p, int len)
        {
            ulong a, b;
            if (len >= 8)
            {
                a = Unsafe.ReadUnaligned<ulong>(p);
                b = Unsafe.ReadUnaligned<ulong>(p + len - 8);
                if (len > 16)
                {
                    a ^= Unsafe.ReadUnaligned<ulong>(p + 8) * 0xC2B2AE3D27D4EB4FUL;
                    if (len > 24)
                    {
                        b ^= Unsafe.ReadUnaligned<ulong>(p + len - 16) * 0x165667B19E3779F9UL;
                        a ^= Unsafe.ReadUnaligned<ulong>(p + (len >> 1) - 4) * 0x27D4EB2F165667C5UL;
                    }
                }
            }
            else if (len >= 4)
            {
                a = Unsafe.ReadUnaligned<uint>(p);
                b = Unsafe.ReadUnaligned<uint>(p + len - 4);
            }
            else
            {
                a = p[0] | ((uint)p[len >> 1] << 8);
                b = p[len - 1];
            }
            var h = (a * 0x9E3779B97F4A7C15UL) ^ (b * 0xFF51AFD7ED558CCDUL) ^ (ulong)len;
            return (uint)(h >> 32) ^ (uint)h;
        }

        /// <summary>
        /// A fast sampling hash of more than 64 bytes: the first and last 16 bytes, 8 bytes at a quarter, half and three quarters of the data, and the length.
        /// Not every byte contributes, so it's only usable where the content is compared on a hit (different content with the same samples is a miss, never a wrong string).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint HashLong(Byte* p, int len)
        {
            var q = len >> 2;
            var a = Unsafe.ReadUnaligned<ulong>(p) ^ (Unsafe.ReadUnaligned<ulong>(p + 8) * 0xC2B2AE3D27D4EB4FUL);
            var b = Unsafe.ReadUnaligned<ulong>(p + len - 8) ^ (Unsafe.ReadUnaligned<ulong>(p + len - 16) * 0x165667B19E3779F9UL);
            var c = (Unsafe.ReadUnaligned<ulong>(p + q) * 0x27D4EB2F165667C5UL) ^ (Unsafe.ReadUnaligned<ulong>(p + (len >> 1) - 4) * 0x85EBCA77C2B2AE63UL) ^ Unsafe.ReadUnaligned<ulong>(p + len - q - 8);
            var h = (a * 0x9E3779B97F4A7C15UL) ^ (b * 0xFF51AFD7ED558CCDUL) ^ (c * 0xC4CEB9FE1A85EC53UL) ^ (ulong)len;
            return (uint)(h >> 32) ^ (uint)h;
        }
    }

    /// <summary>
    /// A bounded, lock free cache of strings created from chars, with its own table and length limit (ex: the local urls of http requests).
    /// The same design as <see cref="SpanStringCache"/> (direct mapped, content compared on every hit, a string is cached the second time it's seen),
    /// and it's also turned off by <see cref="SpanStringCache.Enabled"/>.
    /// </summary>
    /// <remarks>
    /// Content of at most 32 chars uses the fast hash of <see cref="SpanStringCache"/>, longer content a sampling hash (the first, last and a few inner bytes).
    /// The randomized <see cref="String.GetHashCode(ReadOnlySpan{char})"/> made a hit of 64 - 128 chars 4 - 5 times slower than <see cref="String.Substring(int, int)"/>.
    /// Content that only differs outside the samples shares a slot (misses, never a wrong string).
    /// Measured on .NET 10 and .NET 11: a hit is 10 - 35 % faster than <see cref="String.Substring(int, int)"/> of the same 8 - 32 chars, and doesn't allocate.
    /// </remarks>
    public sealed unsafe class SpanStringCacheTable
    {
        /// <summary>
        /// Create a cache.
        /// </summary>
        /// <param name="slots">The number of slots (rounded up to a power of two, at least 16), the max number of cached strings</param>
        /// <param name="maxLength">Content longer than this (in chars) is never cached</param>
        public SpanStringCacheTable(int slots = 4096, int maxLength = 256)
        {
            var s = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, slots));
            Cache = new String[s];
            Seen = new uint[s];
            Mask = (uint)(s - 1);
            MaxLength = maxLength;
        }

        /// <summary>
        /// Content longer than this (in chars) is never cached
        /// </summary>
        public readonly int MaxLength;

        readonly String[] Cache;
        readonly uint[] Seen;
        readonly uint Mask;

        /// <summary>
        /// Get a string with the content of some chars, from the cache if possible (an empty span gives <see cref="String.Empty"/>).
        /// </summary>
        /// <param name="chars">The chars</param>
        /// <returns>A string with the same content</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String Get(ReadOnlySpan<Char> chars)
        {
            var len = chars.Length;
            if ((len == 0) || (len > MaxLength) || (!SpanStringCache.Enabled))
                return len == 0 ? String.Empty : new String(chars);
            uint h;
            fixed (Char* p = chars)
                h = len <= SpanStringCache.MaxLength ? SpanStringCache.Hash((Byte*)p, len << 1) : SpanStringCache.HashLong((Byte*)p, len << 1);
            var s = Cache[h & Mask];
            if ((s != null) && chars.SequenceEqual(s))
                return s;
            return Miss(h, chars);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        String Miss(uint h, ReadOnlySpan<Char> chars)
        {
            var s = new String(chars);
            var i = (int)(h & Mask);
            ref var tag = ref Seen[i];
            //  h | 1: a zero hash would match an unused slot
            var t = h | 1;
            if (tag == t)
                Cache[i] = s;
            else
                tag = t;
            return s;
        }
    }
}
