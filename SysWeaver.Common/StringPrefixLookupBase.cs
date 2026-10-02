using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SysWeaver
{
    /// <summary>
    /// The implementation of the string prefix lookups (StringPrefixLookup and StringPrefixLookup&lt;T&gt;), finds the longest string (from a set of strings) that a text starts with, using ordinal (case sensitive) compares.
    /// A leaf (the string, or an entry with the value) is stored for every string (as an object, the derived classes have the typed public api, so that all code is compiled for the exact type).
    /// Optimized for a few (up to ~32) short strings (4 - 16 chars), where most searches doesn't match, like the web page sub paths of special modules.
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// </summary>
    /// <remarks>
    /// If all strings have at least 4 chars (and there are at most 254 strings):
    /// - Texts that are shorter than the shortest string, or starts with an ASCII char that no string starts with, are rejected.
    /// - The key is the first K chars of the text (K is the length of the shortest string, at most 8), loaded as two integers (the first 4 chars and the last 4 chars of the key, overlapping if K is less than 8), and hashed to a slot.
    ///   The slot have a chain of entries (the strings that hash to the slot, longest first).
    /// - An entry is a match if the key and the last 8 chars of the string (as a vector, or the last 4 chars as an integer for strings shorter than 8 chars) are equal to the text (and the chars in between, for long strings).
    /// - The hash multipliers are selected to minimize the collisions (typically there are none), so a miss is (typically) a hash and a compare.
    /// Else a FrozenStringTreeList is used.
    /// </remarks>
    public abstract class StringPrefixLookupBase
    {
        /// <summary>
        /// Create a lookup
        /// </summary>
        /// <param name="strings">The strings, may not contain null, empty strings or duplicates</param>
        /// <param name="leafs">The leaf of every string</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        private protected StringPrefixLookupBase(String[] strings, Object[] leafs)
        {
            HashSet<String> seen = new(StringComparer.Ordinal);
            foreach (var s in strings)
            {
                ArgumentNullException.ThrowIfNull(s, nameof(strings));
                StringTree.ValidateAdd(s);
                if (!seen.Add(s))
                    throw new Exception("The string \"" + s + "\" have already been added!");
            }
            Count = strings.Length;
            if (strings.Length == 0)
            {
                // Nothing can match
                MinLength = int.MaxValue;
                return;
            }
            MinLength = strings.Min(x => x.Length);
            foreach (var s in strings)
            {
                var c = s[0];
                if (c < 64)
                    FirstAscii0 |= 1UL << c;
                else if (c < 128)
                    FirstAscii1 |= 1UL << c;
            }
            if ((MinLength < 4) || (strings.Length > MaxStrings) || !Vector128.IsHardwareAccelerated)
            {
                Fallback = FrozenStringTreeList<Object>.Build(strings.Select((s, i) => Tuple.Create(s, leafs[i])));
                return;
            }
            var keyChars = Math.Min(MinLength, VectorChars);
            KeyChars = keyChars;
            var key1 = keyChars - 4;
            Key1Offset = key1 * 2;
            // Find the hash multipliers with the least collisions
            var heads = strings.Select(x => (Lo: Read(x, 0), Hi: Read(x, key1))).Distinct().ToArray();
            var rng = new Random(1);
            var used = new bool[SlotCount];
            int best = int.MaxValue;
            for (int attempt = 0; (attempt < 1000) && (best > 0); ++attempt)
            {
                var m0 = (UInt64)rng.NextInt64() | 1;
                var m1 = (UInt64)rng.NextInt64() | 1;
                Array.Clear(used);
                int collisions = 0;
                foreach (var (lo, hi) in heads)
                {
                    var h = Hash(lo, hi, m0, m1);
                    if (used[h])
                        ++collisions;
                    used[h] = true;
                }
                if (collisions >= best)
                    continue;
                best = collisions;
                M0 = m0;
                M1 = m1;
            }
            // Entry 0 is an empty entry (it never matches)
            var entries = new Entry[strings.Length + 1];
            entries[0].Length = int.MaxValue;
            int ei = 0;
            foreach (var slot in Enumerable.Range(0, strings.Length).GroupBy(i => Hash(Read(strings[i], 0), Read(strings[i], key1), M0, M1)))
            {
                // Longest first, so that the first match is the longest match
                var first = ei + 1;
                var count = slot.Count();
                foreach (var i in slot.OrderByDescending(i => strings[i].Length))
                {
                    var s = strings[i];
                    var l = s.Length;
                    ++ei;
                    ref var e = ref entries[ei];
                    e.Head0 = Read(s, 0);
                    e.Head1 = Read(s, key1);
                    if (l >= VectorChars)
                        e.Tail = Load(s, l - VectorChars);
                    else
                        e.Tail4 = Read(s, l - 4);
                    e.String = s;
                    e.Leaf = leafs[i];
                    e.Length = l;
                    e.MiddleLength = Math.Max(0, l - VectorChars - keyChars);
                    e.Next = ei < (first + count - 1) ? ei + 1 : 0;
                }
                Slots[slot.Key] = (Byte)first;
            }
            Entries = entries;
        }

        /// <summary>
        /// The number of strings
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Find the leaf of the longest string, that the text starts with
        /// </summary>
        /// <param name="text">The text to match against the strings, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The leaf of the longest found match or null if no match is found</returns>
        private protected Object StartsWithAnyLeaf(String text, int start)
        {
            StringTree.ValidateSearch(text, start);
            var len = text.Length;
            if ((uint)start >= (uint)len)
            {
                if (start < 0)
                    _ = text[start];
                return null;
            }
            // No string can match a text that is shorter than the shortest string (this also makes sure that the text have at least the key chars to load)
            var remaining = len - start;
            if (remaining < MinLength)
                return null;
            // Quick reject of ASCII chars that no string starts with
            var c = text[start];
            if (c < 128)
            {
                var m = c < 64 ? FirstAscii0 : FirstAscii1;
                if (((m >> c) & 1) == 0)
                    return null;
            }
            var f = Fallback;
            if (f != null)
                return f.StartsWithAny(text, start)?[0];
            ref var t = ref Unsafe.As<Char, UInt16>(ref Unsafe.Add(ref Unsafe.AsRef(in text.GetPinnableReference()), start));
            ref var tb = ref Unsafe.As<UInt16, Byte>(ref t);
            var h0 = Unsafe.ReadUnaligned<UInt64>(ref tb);
            var h1 = Unsafe.ReadUnaligned<UInt64>(ref Unsafe.Add(ref tb, Key1Offset));
            var slot = Unsafe.Add(ref Unsafe.As<SlotBytes, Byte>(ref Unsafe.AsRef(in Slots)), Hash(h0, h1, M0, M1));
            ref var entries = ref MemoryMarshal.GetArrayDataReference(Entries);
            ref var e = ref Unsafe.Add(ref entries, slot);
            for (; ; )
            {
                if (((e.Head0 ^ h0) | (e.Head1 ^ h1)) == 0)
                {
                    var l = e.Length;
                    if (l <= remaining)
                    {
                        if (l >= VectorChars)
                        {
                            if ((Vector128.LoadUnsafe(ref t, (nuint)(l - VectorChars)) == e.Tail) && ((e.MiddleLength == 0) || MatchMiddle(ref t, ref e)))
                                return e.Leaf;
                        }
                        else
                        {
                            if (Unsafe.ReadUnaligned<UInt64>(ref Unsafe.Add(ref tb, (l - 4) * 2)) == e.Tail4)
                                return e.Leaf;
                        }
                    }
                }
                var n = e.Next;
                if (n == 0)
                    return null;
                e = ref Unsafe.Add(ref entries, n);
            }
        }

        #region Implementation

        /// <summary>
        /// Compare the chars between the key and the last 8 chars (for strings with more than key + 8 chars)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        bool MatchMiddle(ref UInt16 text, ref Entry e)
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<UInt16, Char>(ref Unsafe.Add(ref text, KeyChars)), e.MiddleLength)
                .SequenceEqual(e.String.AsSpan(KeyChars, e.MiddleLength));

        /// <summary>
        /// The number of chars in a vector (and the max number of chars that is hashed)
        /// </summary>
        const int VectorChars = 8;

        /// <summary>
        /// The max number of strings (the entry index is a byte, entry 0 is empty)
        /// </summary>
        const int MaxStrings = 254;

        /// <summary>
        /// The number of hash slots (the top 8 bits of the hash)
        /// </summary>
        const int SlotCount = 256;

        struct Entry
        {
            /// <summary>
            /// The first 4 chars and the last 4 chars of the key of the string (as integers)
            /// </summary>
            public UInt64 Head0, Head1;
            /// <summary>
            /// The last 8 chars of the string (if the string have at least 8 chars)
            /// </summary>
            public Vector128<UInt16> Tail;
            /// <summary>
            /// The last 4 chars of the string (if the string have less than 8 chars)
            /// </summary>
            public UInt64 Tail4;
            /// <summary>
            /// The string
            /// </summary>
            public String String;
            /// <summary>
            /// The leaf of the string
            /// </summary>
            public Object Leaf;
            /// <summary>
            /// The length of the string (int.MaxValue for the empty entry)
            /// </summary>
            public int Length;
            /// <summary>
            /// The number of chars between the key and the last 8 chars
            /// </summary>
            public int MiddleLength;
            /// <summary>
            /// The index of the next entry in the same slot, 0 = no more entries
            /// </summary>
            public int Next;
        }

        [InlineArray(SlotCount)]
        struct SlotBytes
        {
            Byte Slot;
        }

        /// <summary>
        /// The length of the shortest string (int.MaxValue if there are no strings)
        /// </summary>
        readonly int MinLength;

        /// <summary>
        /// The number of chars in the key (the length of the shortest string, at most 8)
        /// </summary>
        readonly int KeyChars;

        /// <summary>
        /// The byte offset of the last 4 chars of the key
        /// </summary>
        readonly int Key1Offset;

        /// <summary>
        /// Used if the strings doesn't fit the lookup (a string is shorter than 4 chars, or there are too many strings)
        /// </summary>
        readonly FrozenStringTreeList<Object> Fallback;

        /// <summary>
        /// A bit for every ASCII char (0 - 63 and 64 - 127) that a string starts with
        /// </summary>
        readonly UInt64 FirstAscii0, FirstAscii1;

        /// <summary>
        /// The hash multipliers
        /// </summary>
        readonly UInt64 M0, M1;

        /// <summary>
        /// The index of the first entry for every hash value, 0 = the empty entry
        /// </summary>
        readonly SlotBytes Slots;

        /// <summary>
        /// The entries, ordered by slot and then length (longest first), entry 0 is empty
        /// </summary>
        readonly Entry[] Entries;

        /// <summary>
        /// Read 4 chars as an integer
        /// </summary>
        static UInt64 Read(String s, int offset) => MemoryMarshal.Read<UInt64>(MemoryMarshal.AsBytes(s.AsSpan(offset, 4)));

        /// <summary>
        /// Load 8 chars as a vector
        /// </summary>
        static Vector128<UInt16> Load(String s, int offset) => Vector128.Create<UInt16>(MemoryMarshal.Cast<Char, UInt16>(s.AsSpan(offset, VectorChars)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Hash(UInt64 lo, UInt64 hi, UInt64 m0, UInt64 m1) => (int)(((lo * m0) + (hi * m1)) >> 56);

        #endregion
    }
}
