using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SysWeaver
{
    /// <summary>
    /// An immutable lookup that finds the longest string (from a set of strings) that a text starts with, using ordinal (case sensitive) compares.
    /// Optimized for a few (up to ~32) short strings (8 - 16 chars), where most searches doesn't match, like the web page sub paths of special modules.
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// </summary>
    /// <remarks>
    /// If all strings have at least 8 chars (and there are at most 254 strings):
    /// - Texts that are shorter than the shortest string, or starts with an ASCII char that no string starts with, are rejected.
    /// - The first 8 chars of the text are loaded (as two integers) and hashed to a slot, the slot have a chain of entries (the strings that hash to the slot, longest first).
    /// - An entry is a match if the first 8 chars (as integers) and the last 8 chars (as a vector) of the string are equal to the text (and the chars in between for strings longer than 16 chars).
    /// - The hash multipliers are selected to minimize the collisions (typically there are none), so a miss is (typically) a hash and a compare.
    /// Else a FrozenStringTree is used.
    /// </remarks>
    public sealed class StringPrefixLookup : IStringTree
    {
        /// <summary>
        /// Create a lookup of some strings
        /// </summary>
        /// <param name="strings">The strings, may not contain null, empty strings or duplicates</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        public StringPrefixLookup(IEnumerable<String> strings)
        {
            var all = strings.ToArray();
            HashSet<String> seen = new(StringComparer.Ordinal);
            foreach (var s in all)
            {
                ArgumentNullException.ThrowIfNull(s, nameof(strings));
                StringTree.ValidateAdd(s);
                if (!seen.Add(s))
                    throw new Exception("The string \"" + s + "\" have already been added!");
            }
            Count = all.Length;
            if (all.Length == 0)
            {
                // Nothing can match
                MinLength = int.MaxValue;
                return;
            }
            MinLength = all.Min(x => x.Length);
            foreach (var s in all)
            {
                var c = s[0];
                if (c < 64)
                    FirstAscii0 |= 1UL << c;
                else if (c < 128)
                    FirstAscii1 |= 1UL << c;
            }
            if ((MinLength < VectorChars) || (all.Length > MaxStrings) || !Vector128.IsHardwareAccelerated)
            {
                Fallback = FrozenStringTree.Build(all);
                return;
            }
            // Find the hash multipliers with the least collisions
            var heads = all.Select(x => (Lo: Read(x, 0), Hi: Read(x, 4))).Distinct().ToArray();
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
            var entries = new Entry[all.Length + 1];
            entries[0].Length = int.MaxValue;
            int ei = 0;
            foreach (var slot in all.GroupBy(x => Hash(Read(x, 0), Read(x, 4), M0, M1)))
            {
                // Longest first, so that the first match is the longest match
                var first = ei + 1;
                foreach (var s in slot.OrderByDescending(x => x.Length))
                {
                    ++ei;
                    ref var e = ref entries[ei];
                    e.Head0 = Read(s, 0);
                    e.Head1 = Read(s, 4);
                    e.Tail = Load(s, s.Length - VectorChars);
                    e.Value = s;
                    e.Length = s.Length;
                    e.Next = ei < (first + slot.Count() - 1) ? ei + 1 : 0;
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
        /// Find the longest string, that the text starts with
        /// </summary>
        /// <param name="text">The text to match against the strings, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The longest found match or null if no match is found</returns>
        public String StartsWithAny(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            var len = text.Length;
            if ((uint)start >= (uint)len)
            {
                if (start < 0)
                    _ = text[start];
                return null;
            }
            // No string can match a text that is shorter than the shortest string (this also makes sure that the text have at least 8 chars to load)
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
                return f.StartsWithAny(text, start);
            ref var t = ref Unsafe.As<Char, UInt16>(ref Unsafe.Add(ref Unsafe.AsRef(in text.GetPinnableReference()), start));
            var h0 = Unsafe.ReadUnaligned<UInt64>(ref Unsafe.As<UInt16, Byte>(ref t));
            var h1 = Unsafe.ReadUnaligned<UInt64>(ref Unsafe.As<UInt16, Byte>(ref Unsafe.Add(ref t, 4)));
            var slot = Unsafe.Add(ref Unsafe.As<SlotBytes, Byte>(ref Unsafe.AsRef(in Slots)), Hash(h0, h1, M0, M1));
            ref var entries = ref MemoryMarshal.GetArrayDataReference(Entries);
            ref var e = ref Unsafe.Add(ref entries, slot);
            for (; ; )
            {
                if (((e.Head0 ^ h0) | (e.Head1 ^ h1)) == 0)
                {
                    var l = e.Length;
                    if ((l <= remaining) && (Vector128.LoadUnsafe(ref t, (nuint)(l - VectorChars)) == e.Tail) && ((l <= (VectorChars * 2)) || MatchMiddle(ref t, e.Value)))
                        return e.Value;
                }
                var n = e.Next;
                if (n == 0)
                    return null;
                e = ref Unsafe.Add(ref entries, n);
            }
        }

        #region Implementation

        /// <summary>
        /// Compare the chars between the first and last 8 chars (for strings with more than 16 chars)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool MatchMiddle(ref UInt16 text, String value)
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<UInt16, Char>(ref Unsafe.Add(ref text, VectorChars)), value.Length - VectorChars * 2)
                .SequenceEqual(value.AsSpan(VectorChars, value.Length - VectorChars * 2));

        /// <summary>
        /// The number of chars in a vector (and the number of chars that is hashed)
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
            /// The first 8 chars of the string (as two 4 char integers)
            /// </summary>
            public UInt64 Head0, Head1;
            /// <summary>
            /// The last 8 chars of the string
            /// </summary>
            public Vector128<UInt16> Tail;
            /// <summary>
            /// The string
            /// </summary>
            public String Value;
            /// <summary>
            /// The length of the string (int.MaxValue for the empty entry)
            /// </summary>
            public int Length;
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
        /// Used if the strings doesn't fit the lookup (a string is shorter than 8 chars, or there are too many strings)
        /// </summary>
        readonly FrozenStringTree Fallback;

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
