
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace SysWeaver.Collections
{

    /// <summary>
    /// Contains a set of strings with focus on being compact in memory.
    /// Searching for a string is log(n).
    /// Typically uses 3-4 times less memory than a byte array of the same strings along with a int array of string start indices.
    /// String must be added in sorted order (using <see cref="AsciiCompare"/>).
    /// All chars are compared ordinally.
    /// </summary>
    /// <remarks>
    /// Strings are stored in blocks of <see cref="BlockSize"/> strings, each string (except the first in a block) is front coded,
    /// i.e. stored as the length of the common prefix with the previous string (max 31) followed by the remaining chars, one byte per char.
    /// A binary search on the first string of each block is followed by a linear scan of the block.
    /// <para>
    /// Strings must be non-empty and only contain chars in the range [32, 255]; this (and the sort order) is only validated in DEBUG builds,
    /// in release builds violating it gives undefined results (chars above 255 are truncated to a byte).
    /// </para>
    /// <para>
    /// Not thread safe. The read methods (<see cref="IndexOf"/>, <see cref="Contains"/> and enumeration) build the last partial block if needed,
    /// so call <see cref="Fix"/> after the last <see cref="Add"/> before reading concurrently from multiple threads.
    /// </para>
    /// </remarks>
    public sealed class CompactAsciiStringSet : IEnumerable<String>
    {
#if DEBUG
        public override string ToString() => String.Concat(Count, " strings in ", ByteCount, " bytes @ ", (Compression * 100.0).ToString("0.00", CultureInfo.InvariantCulture), "% compression");
#endif//DEBBUG
        /// <summary>
        /// The ratio between <see cref="ByteCount"/> and <see cref="OriginalByteCount"/> (lower is better).
        /// </summary>
        public double Compression => (double)ByteCount / (double)OriginalByteCount;

        /// <summary>
        /// Approximate number of bytes used in memory.
        /// </summary>
        public long ByteCount => RawByteCount + Blocks.Count * 16 + 16;

        /// <summary>
        /// Approximate number of bytes used if stored as one Byte[] and an int[] with string start indices.
        /// </summary>
        public long OriginalByteCount { get; private set; } = 2 * 16;



        long RawByteCount;

        /// <summary>
        /// Number of strings in the set
        /// </summary>
        public long Count { get; private set; }


        readonly List<Byte[]> Blocks = new List<byte[]>();

        readonly List<String> Build;

        /// <summary>
        /// The number of strings per block.
        /// A larger block size is more compact but makes lookups slower (linear scan within a block).
        /// </summary>
        public readonly int BlockSize;
        /// <summary>
        /// Create an empty set.
        /// </summary>
        /// <param name="blockSize">The number of strings per block, values less than 8 are clamped to 8</param>
        public CompactAsciiStringSet(int blockSize = 64)
        {
            blockSize = Math.Max(8, blockSize);
            BlockSize = blockSize;
            Build = new List<string>(blockSize);
        }

        int MaxStringLength;

        bool HavePartial;
        bool LastIsPartial;

#if DEBUG
        String Old = "";
#endif//DEBUG


        /// <summary>
        /// The sort method that must be used, an ordinal char by char comparison where a shorter string sorts before a longer string with the same prefix.
        /// </summary>
        /// <param name="a">The first string (non-null)</param>
        /// <param name="b">The second string (non-null)</param>
        /// <returns>Less than zero if <paramref name="a"/> sorts before <paramref name="b"/>, zero if equal and greater than zero if <paramref name="a"/> sorts after <paramref name="b"/></returns>
        public static int AsciiCompare(String a, String b)
        {
            var al = a.Length;
            var bl = b.Length;
            //  Vectorized search for the first difference, the result is the same as before (the char difference or the length difference)
            var i = a.AsSpan().CommonPrefixLength(b);
            if (i < al && i < bl)
                return a[i] - b[i];
            return al - bl;
        }

        /// <summary>
        /// Check if a string is in the set
        /// </summary>
        /// <param name="s">The string to find (non-null)</param>
        /// <returns>True if the string is in the set</returns>
        public bool Contains(String s)
            => IndexOf(s) >= 0;

        /// <summary>
        /// Get the index of a string (in insert order, which is also the sort order)
        /// </summary>
        /// <param name="s">The string to find (non-null)</param>
        /// <returns>The zero based index of the string or -1 if it's not in the set (also if it contains chars outside [32, 255], those can't be added)</returns>
        /// <exception cref="Exception">DEBUG builds only: The string is empty</exception>
        public int IndexOf(String s)
        {
#if DEBUG
            if (s.Length <= 0)
                throw new Exception("String can't be empty!");
#endif//DEBUG
            if (HavePartial)
                FixPartial();
            var sl = s.Length;
            //  Chars outside [32, 255] can't be in the set (and must not be truncated to a byte)
            if (s.AsSpan().ContainsAnyExceptInRange((Char)32, (Char)255))
                return -1;
            var pool = ArrayPool<Byte>.Shared;
            Byte[] r = null;
            Byte[] t = null;
            try
            {
                Span<Byte> mem = sl <= MaxStackBytes ? stackalloc Byte[sl] : (r = pool.Rent(sl)).AsSpan(0, sl);
                for (int x = 0; x < sl; ++x)
                    mem[x] = (Byte)s[x];
                //  Find the first block with a first string that is greater or equal (same as BinarySearch.Lower with a lexicographic comparer)
                var blocks = Blocks;
                var blockSize = BlockSize;
                var count = blocks.Count;
                int min = 0;
                int max = count - 1;
                while (min <= max)
                {
                    var mid = min + ((max - min) >> 1);
                    if (FirstString(blocks[mid]).SequenceCompareTo(mem) < 0)
                        min = mid + 1;
                    else
                        max = mid - 1;
                }
                if ((min < count) && (FirstString(blocks[min]).SequenceCompareTo(mem) == 0))
                    return min * blockSize;
                var blockIndex = min;
                if (blockIndex == 0)
                    return -1;
                --blockIndex;
                var baseIndex = blockIndex * blockSize;
                var ml = MaxStringLength;
                Span<Byte> temp = ml <= MaxStackBytes ? stackalloc Byte[MaxStackBytes] : (t = pool.Rent(ml));
                var b = blocks[blockIndex];
                var l = b.Length;
                int common = 0;
                int index = 0;
                int i = 0;
                while (i < l)
                {
                    var c = b[i];
                    if (c < 32)
                    {
                        if (index > 0)
                        {
                            var res = ((ReadOnlySpan<Byte>)mem).SequenceCompareTo(temp.Slice(0, common));
                            if (res == 0)
                                return baseIndex + index;
                            if (res < 0)
                                return -1;
                        }
                        ++index;
                        ++i;
                        common = c;
                        continue;
                    }
                    temp[common] = c;
                    ++i;
                    ++common;
                }
                if (((ReadOnlySpan<Byte>)mem).SequenceEqual(temp.Slice(0, common)))
                    return baseIndex + index;
                return -1;
            }
            finally
            {
                if (t != null)
                    pool.Return(t);
                if (r != null)
                    pool.Return(r);
            }
        }

        /// <summary>
        /// The largest number of bytes to use on the stack (else a pooled array is used)
        /// </summary>
        const int MaxStackBytes = 256;

        /// <summary>
        /// Get the first (uncompressed) string of a block, the bytes before the first byte that is less than 32 (or the whole block)
        /// </summary>
        /// <param name="block">The block</param>
        /// <returns>The bytes of the first string</returns>
        static ReadOnlySpan<Byte> FirstString(Byte[] block)
        {
            var i = block.AsSpan().IndexOfAnyInRange((Byte)0, (Byte)31);
            return i < 0 ? block : block.AsSpan(0, i);
        }

        /// <summary>
        /// Add a string to the set, empty strings and null may not be present.
        /// The string to insert must compare greater than the previously added string.
        /// String must be ascii as in all chars are between: [32, 255].
        /// </summary>
        /// <param name="s">The string to insert</param>
        /// <exception cref="Exception">DEBUG builds only: The string is null, empty, contains invalid chars or isn't greater than the previously added string</exception>
        public void Add(String s)
        {
#if DEBUG
            if (String.IsNullOrEmpty(s))
                throw new Exception("Must be non-empty!");
            foreach (int c in s)
                if ((c < 32) || (c > 255))
                    throw new Exception("String may only contains ascci chars [32, 255], found: " + c);
            if (AsciiCompare(Old, s) >= 0)
                throw new Exception("Must insert strings in sorted order!");
            Old = s;
#endif//DEBUG
        //  If the last block is partial, remove it
            var blocks = Blocks;
            if (LastIsPartial)
            {
                var last = blocks.Count - 1;
                RawByteCount -= blocks[last].Length;
                blocks.RemoveAt(last);
                LastIsPartial = false;
            }
            MaxStringLength = Math.Max(MaxStringLength, s.Length);
            var b = Build;
            b.Add(s);
            OriginalByteCount += (s.Length + 4);
            ++Count;
            if (b.Count < BlockSize)
            {
                //  Block isn't full yet
                HavePartial = true;
                return;
            }
            //  Build and add block
            var data = BuildBlock(b);
            RawByteCount += data.Length;
            blocks.Add(data);
            b.Clear();
            HavePartial = false;
        }


        /// <summary>
        /// Call after last Add to get the correct statistics.
        /// Builds the last partial block (if any), this is otherwise done lazily by the first read.
        /// </summary>
        public void Fix()
        {
            if (HavePartial)
                FixPartial();
        }

        void FixPartial()
        {
            HavePartial = false;
            LastIsPartial = true;
            var data = BuildBlock(Build);
            RawByteCount += data.Length;
            Blocks.Add(data);
        }

        static int CommonPrefixLen(String prev, String current)
        {
            var len = prev.Length;
            var clen = current.Length;
            if (clen < len)
                len = clen;
            if (len >= 32)
                len = 31;
            for (int i = 0; i < len; ++ i)
            {
                int a = prev[i];
                int b = current[i];
                if (a != b)
                    return i;
            }
            return len;
        }

        Byte[] BuildBlock(List<String> s)
        {
            var sl = s.Count;
            int maxLen = 0;
            for (int i = 0; i < sl; ++i)
                maxLen += s[i].Length;
            maxLen += (BlockSize << 1);
            const int MaxStackAlloc = 1024;
            Byte[] rented = null;
            Span<Byte> temp = maxLen <= MaxStackAlloc ? stackalloc byte[maxLen] : (rented = ArrayPool<Byte>.Shared.Rent(maxLen));
            try
            {
                String prev = "";
                int dest = 0;
                for (int i = 0; i < sl; ++i)
                {
                    var c = s[i];
                    var plen = CommonPrefixLen(prev, c);
                    temp[dest] = (byte)plen;
                    ++dest;
                    var l = c.Length;
                    for (int j = plen; j < l; ++ j)
                    {
                        int a = c[j];
                        temp[dest] = (byte)a;
                        ++dest;
                    }
                    prev = c;
                }
                --dest;
                var bl = GC.AllocateUninitializedArray<Byte>(dest);
                temp.Slice(1, dest).CopyTo(bl.AsSpan());
                return bl;
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Enumerate all strings in the set (in insert order).
        /// </summary>
        /// <returns>An enumerator, a new string is allocated for every item</returns>
        public IEnumerator<string> GetEnumerator()
        {
            if (HavePartial)
                FixPartial();
            var t = GC.AllocateUninitializedArray<char>(MaxStringLength);
            foreach (var b in Blocks)
            {
                var l = b.Length;
                int common = 0;
                int i = 0;
                while (i < l)
                {
                    var c = b[i];
                    if (c < 32)
                    {
                        yield return new string(t, 0, common);
                        ++i;
                        common = c;
                        continue;
                    }
                    t[common] = (Char)c;
                    ++i;
                    ++common;
                }
                yield return new string(t, 0, common);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

}