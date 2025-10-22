
using System;
using System.Collections;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Contains methods for getting and mixing hashcodes.
    /// These are generated with maximum performance and quality in mind
    /// </summary>
    public static class ObjectHash
    {
        public static int Get<T>(T value)
        {
            return value?.GetHashCode() ?? 0;
        }

        public static int Mix(int h1, int h2)
        {
            return (h1 * 17) ^ h2;
        }

        public static int Mix(int h1, int h2, int h3)
        {
            h1 = (h1 * 17) ^ h2;
            return (h3 * 524287) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            return (h1 * 31) ^ h2;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h1 = (h5 * 31) ^ h1;
            return (h2 * 131071) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h1 = (h1 * 131071) ^ h2;
            return (h3 * 127) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h1 = (h7 * 131071) ^ h1;
            h1 = (h1 * 127) ^ h2;
            return (h3 * 65537) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h1 = (h1 * 127) ^ h2;
            h2 = (h3 * 65537) ^ h4;
            return (h1 * 257) ^ h2;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h1 = (h9 * 127) ^ h1;
            h1 = (h1 * 65537) ^ h2;
            h2 = (h3 * 257) ^ h4;
            return (h1 * 8191) ^ h2;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h1 = (h1 * 65537) ^ h2;
            h2 = (h3 * 257) ^ h4;
            h1 = (h5 * 8191) ^ h1;
            return (h2 * 5) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h1 = (h11 * 65537) ^ h1;
            h1 = (h1 * 257) ^ h2;
            h2 = (h3 * 8191) ^ h4;
            h1 = (h5 * 5) ^ h1;
            return (h2 * 7) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h1 = (h1 * 257) ^ h2;
            h2 = (h3 * 8191) ^ h4;
            h3 = (h5 * 5) ^ h6;
            h1 = (h1 * 7) ^ h2;
            return (h3 * 17) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h1 = (h13 * 257) ^ h1;
            h1 = (h1 * 8191) ^ h2;
            h2 = (h3 * 5) ^ h4;
            h3 = (h5 * 7) ^ h6;
            h1 = (h1 * 17) ^ h2;
            return (h3 * 524287) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13, int h14)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h7 = (h13 * 257) ^ h14;
            h1 = (h1 * 8191) ^ h2;
            h2 = (h3 * 5) ^ h4;
            h3 = (h5 * 7) ^ h6;
            h1 = (h7 * 17) ^ h1;
            h1 = (h1 * 524287) ^ h2;
            return (h3 * 31) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13, int h14, int h15)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h7 = (h13 * 257) ^ h14;
            h1 = (h15 * 8191) ^ h1;
            h1 = (h1 * 5) ^ h2;
            h2 = (h3 * 7) ^ h4;
            h3 = (h5 * 17) ^ h6;
            h1 = (h7 * 524287) ^ h1;
            h1 = (h1 * 31) ^ h2;
            return (h3 * 131071) ^ h1;
        }

        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13, int h14, int h15, int h16)
        {
            h1 = (h1 * 17) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h7 = (h13 * 257) ^ h14;
            h8 = (h15 * 8191) ^ h16;
            h1 = (h1 * 5) ^ h2;
            h2 = (h3 * 7) ^ h4;
            h3 = (h5 * 17) ^ h6;
            h4 = (h7 * 524287) ^ h8;
            h1 = (h1 * 31) ^ h2;
            h2 = (h3 * 131071) ^ h4;
            return (h1 * 127) ^ h2;
        }

        public static int Mix(IReadOnlyList<int> p, int index = 0, int len = -1)
        {
            int hash = 42;
            len = len < 0 ? p.Count : len;
            while (len >= 15)
            {
                hash = Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11], p[index + 12], p[index + 13], p[index + 14]);
                index += 15;
                len -= 15;
            }
            switch (len)
            {
                case 1:
                    return Mix(hash, p[index + 0]);
                case 2:
                    return Mix(hash, p[index + 0], p[index + 1]);
                case 3:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2]);
                case 4:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3]);
                case 5:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4]);
                case 6:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5]);
                case 7:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6]);
                case 8:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7]);
                case 9:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8]);
                case 10:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9]);
                case 11:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10]);
                case 12:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11]);
                case 13:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11], p[index + 12]);
                case 14:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11], p[index + 12], p[index + 13]);
            }
            return hash;
        }

        public static int Mix(params Object[] p)
        {
            int hash = 42;
            int index = 0;
            int len = p.Length;
            while (len >= 15)
            {
                hash = Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]), Get(p[index + 12]), Get(p[index + 13]), Get(p[index + 14]));
                index += 15;
                len -= 15;
            }
            switch (len)
            {
                case 1:
                    return Mix(hash, Get(p[index + 0]));
                case 2:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]));
                case 3:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]));
                case 4:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]));
                case 5:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]));
                case 6:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]));
                case 7:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]));
                case 8:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]));
                case 9:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]));
                case 10:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]));
                case 11:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]));
                case 12:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]));
                case 13:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]), Get(p[index + 12]));
                case 14:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]), Get(p[index + 12]), Get(p[index + 13]));
            }
            return hash;
        }


        public static int Mix(IEnumerable obj)
        {
            int hash = 42;
            int[] p = GC.AllocateUninitializedArray<int>(15);
            var it = obj.GetEnumerator();
            for (; ; )
            {
                int len;
                for (len = 0; (len < 15) && it.MoveNext(); ++len)
                    p[len] = Get(it.Current);
                switch (len)
                {
                    case 0:
                        return hash;
                    case 1:
                        return Mix(hash, p[0]);
                    case 2:
                        return Mix(hash, p[0], p[1]);
                    case 3:
                        return Mix(hash, p[0], p[1], p[2]);
                    case 4:
                        return Mix(hash, p[0], p[1], p[2], p[3]);
                    case 5:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4]);
                    case 6:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5]);
                    case 7:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
                    case 8:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
                    case 9:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8]);
                    case 10:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]);
                    case 11:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10]);
                    case 12:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]);
                    case 13:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12]);
                    case 14:
                        return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13]);
                    case 15:
                        hash = Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]);
                        break;
                }
            }
        }

    }

}  //namespace SysWeaver
