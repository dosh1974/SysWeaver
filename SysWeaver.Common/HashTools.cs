using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    public static unsafe class HashTools
    {
        /// <summary>
        /// Get a string of length 26 for a given text string
        /// </summary>
        /// <param name="text">The 16 byte hash</param>
        /// <returns>The text</returns>
        public static String GetHashString(String text)
        {
            Span<Byte> d = stackalloc Byte[16];
            MD5.HashData(MemoryMarshal.Cast<Char, Byte>(text.AsSpan()), d);
            return GetHashString16(d);
        }

        /// <summary>
        /// Get a string of length 26 for a given 16 byte hash
        /// </summary>
        /// <param name="hash">The 16 byte hash</param>
        /// <returns>A string representing the hash</returns>
        /// <exception cref="ArgumentException"></exception>
        public static String GetHashString16(ReadOnlySpan<Byte> hash)
        {
#if DEBUG
            if (hash.Length != 16)
                throw new ArgumentException("Hash length must be 16 bytes!", nameof(hash));
#endif//DEBUG
            fixed (void* ptr = hash)
            {
                return String.Create(26, (IntPtr)ptr, WriteByteArrayAction);
            }
        }

        /// <summary>
        /// Get the hash as 16 bytes from a string with the hash encoded in the 26 char string format
        /// </summary>
        /// <param name="hashStringh16">A string of length 26 with an encoded hash</param>
        /// <returns>16 bytes with the hash</returns>
        public static Byte[] GetHashFromString26(String hashStringh16)
        {
            var t = GC.AllocateUninitializedArray<Byte>(16);
            GetHashFromString26(t.AsSpan(), hashStringh16);
            return t;
        }

        /// <summary>
        /// Get the hash as 16 bytes from a string with the hash encoded in the 26 char string format
        /// </summary>
        /// <param name="hash">Destination for the hash, must be 16 bytes</param>
        /// <param name="hashStringh16">A string of length 26 with an encoded hash</param>
        public static void GetHashFromString26(Span<Byte> hash, String hashStringh16)
        {
#if DEBUG
            if (hash.Length != 16)
                throw new ArgumentException("Hash length must be 16 bytes!", nameof(hash));
            if (hashStringh16.Length != 26)
                throw new ArgumentException("Hash string must be 26 chars!", nameof(hashStringh16));
#endif//DEBUG
            var s = hashStringh16.AsSpan();
            var u0 = ReadUInt64(s);
            var u1 = ReadUInt64(s.Slice(13));
            BinaryPrimitives.WriteUInt64LittleEndian(hash, u0);
            BinaryPrimitives.WriteUInt64LittleEndian(hash.Slice(8), u1);
        }


        /// <summary>
        /// Get a string of length 13 for a given 64 bit integer
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <returns>A string representing the value</returns>
        public static String GetCompactString(UInt64 value) => String.Create(13, value, WriteUInt64Action);


        /// <summary>
        /// Get a string of length 13 for a given 64 bit integer
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <returns>A string representing the value</returns>
        public static String GetCompactString(Int64 value) => String.Create(13, (ulong)value, WriteUInt64Action);


        /// <summary>
        /// Read a value from a compact string
        /// </summary>
        /// <param name="compactString">The 13 char long compact string</param>
        /// <returns>The value encoded in the string</returns>
        public static UInt64 ParseCompactUInt64(ReadOnlySpan<Char> compactString)
        {
#if DEBUG
            if (compactString.Length != 13)
                throw new ArgumentException("String must be 13 chars!", nameof(compactString));
#endif//DEBUG
            return ReadUInt64(compactString);
        }

        /// <summary>
        /// Read a value from a compact string
        /// </summary>
        /// <param name="compactString">The 13 char long compact string</param>
        /// <returns>The value encoded in the string</returns>
        public static Int64 ParseCompactInt64(ReadOnlySpan<Char> compactString)
        {
#if DEBUG
            if (compactString.Length != 13)
                throw new ArgumentException("String must be 13 chars!", nameof(compactString));
#endif//DEBUG
            unchecked
            {
                return (Int64)ReadUInt64(compactString);
            }
        }


        static readonly Char[] Valid = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();


        static Byte[] GetValidMap()
        {
            var v = Valid;
            var len = 1 + 'z' - '0';
            var m = new Byte[len];
            var ll = v.Length;
            for (int i = 0; i < ll; ++i)
            {
                var c = v[i] - '0';
                m[c] = (Byte)i;
                c = Char.ToUpper(v[i]) - '0';
                m[c] = (Byte)i;
            }
            return m;
        }

        static readonly Byte[] ValidMap = GetValidMap();

        static void WriteUInt64(Span<Char> to, UInt64 ul)
        {
            var v = Valid;
            for (int i = 0; i < 13; ++i)
            {
                var nl = ul / 36;
                var dl = ul - (nl * 36);
                to[i] = v[dl];
                ul = nl;
            }
        }

        static UInt64 ReadUInt64(ReadOnlySpan<Char> from)
        {
            var m = ValidMap;
            uint ml = (uint)m.Length;
            ulong ul = 0;
            int i = 13;
            while (i > 0)
            {
                --i;
                ul *= 36;
                var c = (uint)(from[i] - '0');
                if (c >= ml)
                    continue;
                ul += m[c];
            }
            return ul;
        }

        static void WriteByteArray(Span<Char> to, IntPtr ptr)
        {
            Span<Byte> sp = new Span<byte>(ptr.ToPointer(), 16);
            var ul = BinaryPrimitives.ReadUInt64LittleEndian(sp);
            var v = Valid;
            for (int i = 0; i < 13; ++i)
            {
                var nl = ul / 36;
                var dl = ul - (nl * 36);
                to[i] = v[dl];
                ul = nl;
            }
            ul = BinaryPrimitives.ReadUInt64LittleEndian(sp.Slice(8));
            for (int i = 13; i < 26; ++i)
            {
                var nl = ul / 36;
                var dl = ul - (nl * 36);
                to[i] = v[dl];
                ul = nl;
            }
        }

        static readonly SpanAction<char, IntPtr> WriteByteArrayAction = WriteByteArray;
        static readonly SpanAction<char, UInt64> WriteUInt64Action = WriteUInt64;




    }

}
