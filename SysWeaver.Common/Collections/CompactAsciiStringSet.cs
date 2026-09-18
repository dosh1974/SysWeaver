
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
    /// String must be added in sorted order (using ascii sort).
    /// All chars are compared ordinally.
    /// </summary>
    public sealed class CompactAsciiStringSet : IEnumerable<String>
    {
#if DEBUG
        public override string ToString() => String.Concat(Count, " strings in ", ByteCount, " bytes @ ", (Compression * 100.0).ToString("0.00", CultureInfo.InvariantCulture), "% compression");
#endif//DEBBUG
        public double Compression => (double)ByteCount / (double)OriginalByteCount;

        /// <summary>
        /// Approximate number of bytes used in memory.
        /// </summary>
        public long ByteCount => RawByteCount + Blocks.Count * 16 + 16;

        /// <summary>
        /// Approximate number of bytes used if sotred as one Byte[] and int int[] with string start indices.
        /// </summary>
        public long OriginalByteCount { get; private set; } = 2 * 16;



        long RawByteCount;

        /// <summary>
        /// Number of string in the set
        /// </summary>
        public long Count { get; private set; }


        readonly List<Byte[]> Blocks = new List<byte[]>();

        readonly List<String> Build;

        public readonly int BlockSize;
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
        /// The sort method that must be used 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int AsciiCompare(String a, String b)
        {
            var al = a.Length;
            var bl = b.Length;
            var l = al < bl ? al : bl;
            for (int i = 0; i < l; ++ i)
            {
                int aa = a[i];
                int bb = b[i];
                var d = aa - bb;
                if (d != 0)
                    return d;
            }
            return al - bl;
        }

        /// <summary>
        /// Check is a string is in the set
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public bool Contains(String s)
            => IndexOf(s) >= 0;

        /// <summary>
        /// Get the index of a string (in insert order)
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public int IndexOf(String s)
        {
#if DEBUG
            if (!s.IsAsciiOnly())
                throw new Exception("Only ascii chars [32, 255] is allowed in string!");
            if (s.Length <= 0)
                throw new Exception("String can't be empty!");
#endif//DEBUG
            if (HavePartial)
                FixPartial();
            var sl = s.Length;
            var pool = ArrayPool<Byte>.Shared;
            var r = pool.Rent(sl);
            try
            {
                int i;
                for (i = 0; i < sl; ++i)
                    r[i] = (Byte)(int)s[i];
                var mem = new ReadOnlyMemory<Byte>(r, 0, sl);
                var cmp = ReadOnlyMemoryComparer.GetComparer<Byte>();
                var blocks = Blocks;
                var blockSize = BlockSize;
                var blockIndex = BinarySearch.Lower(0, blocks.Count, mem, bi =>
                {
                    var block = blocks[(int)bi];
                    var bl = block.Length;
                    for (int i = 0; i < bl; ++i)
                    {
                        if (block[i] < 32)
                            return new ReadOnlyMemory<Byte>(block, 0, i);
                    }
                    return block;
                }, cmp);
                if (blockIndex >= 0)
                    return blockIndex * blockSize;
                blockIndex = ~blockIndex;
                if (blockIndex == 0)
                    return -1;
                --blockIndex;
                var baseIndex = blockIndex * blockSize;
                var t = pool.Rent(MaxStringLength);
                try
                {

                    var b = blocks[blockIndex];
                    var l = b.Length;
                    int common = 0;
                    int index = 0;
                    i = 0;
                    while (i < l)
                    {
                        var c = b[i];
                        if (c < 32)
                        {
                            if (index > 0)
                            {
                                var res = cmp.Compare(mem, new ReadOnlyMemory<byte>(t, 0, common));
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
                        t[common] = c;
                        ++i;
                        ++common;
                    }
                    if (cmp.Compare(mem, new ReadOnlyMemory<byte>(t, 0, common)) == 0)
                        return baseIndex + index;
                    return -1;
                }
                finally
                {
                    pool.Return(t);
                }
            }
            finally
            {
                pool.Return(r);
            }
        }

        /// <summary>
        /// Add a string to the set, empty strings and null may not be present.
        /// The string to insert must compare greater than the previously added string.
        /// String must be ascii as in all chars are between: [32, 255].
        /// </summary>
        /// <param name="s">The string to insert</param>
        /// <exception cref="Exception"></exception>
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
            RawByteCount += data.Length;
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
            Span<Byte> temp = stackalloc byte[maxLen];
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