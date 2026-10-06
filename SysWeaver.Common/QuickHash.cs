using System;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    /// <summary>
    /// Tools for computing a 32-bit hash fast.
    /// This is NOT a secure hash, do not use for security!
    /// The hash is endian specific, meaning that a hash computed over some data on a little endian machine differs from the hash computed on a big endian machine (so do not use for data transport etc).
    /// </summary>
    public sealed class QuickHash
    {

        /// <summary>
        /// Compute a 32-bit hash for a string.
        /// This is NOT a secure hash, do not use for security!
        /// The hash is endian specific, meaning that a hash computed over some text on a little endian machine differs from the hash computed on a big endian machine (so do not use for data transport etc).
        /// </summary>
        /// <param name="text">The text to compute a hash over</param>
        /// <param name="seed">An optional seed (defaults to 0xc58f1a7b)</param>
        /// <returns>A 32-bit hash of the text, 0 if the text is null or empty</returns>
        /// <remarks>The hash is computed over the UTF-16 representation of the text, i.e. it's the same as hashing the bytes of the chars</remarks>
        public static unsafe UInt32 Hash(String text, UInt32 seed = 0xc58f1a7b)
        {
            if (text == null)
                return 0;
            return Hash(MemoryMarshal.Cast<Char, Byte>(text.AsSpan()), seed);
        }


        /// <summary>
        /// Compute a 32-bit hash over some data.
        /// This is NOT a secure hash, do not use for security!
        /// The hash is endian specific, meaning that a hash computed over some data on a little endian machine differs from the hash computed on a big endian machine (so do not use for data transport etc).
        /// </summary>
        /// <param name="data">The data to compute a hash over</param>
        /// <param name="seed">An optional seed (defaults to 0xc58f1a7b)</param>
        /// <returns>A 32-bit hash of the data, 0 if the data is empty</returns>
        /// <remarks>This is MurmurHash2 (on little endian machines), except that empty data always hash to 0 (regardless of the seed)</remarks>
        public static unsafe UInt32 Hash(ReadOnlySpan<Byte> data, UInt32 seed = 0xc58f1a7b)
        {
            Int32 length = data.Length;
            if (length == 0)
                return 0;
            UInt32 h = seed ^ (UInt32)length;
            Int32 remainingBytes = length & 3; // mod 4
            Int32 numberOfLoops = length >> 2; // div 4
            fixed (byte* firstByte = data)
            {
                UInt32* realData = (UInt32*)firstByte;
                while (numberOfLoops != 0)
                {
                    UInt32 k = *realData;
                    k *= m;
                    k ^= k >> r;
                    k *= m;
                    h *= m;
                    h ^= k;
                    numberOfLoops--;
                    realData++;
                }
                switch (remainingBytes)
                {
                    case 3:
                        // Only read the remaining bytes (never past the end of the data)
                        h ^= *((UInt16*)realData);
                        h ^= ((UInt32)(*(((Byte*)(realData)) + 2))) << 16;
                        h *= m;
                        break;
                    case 2:
                        h ^= *((UInt16*)realData);
                        h *= m;
                        break;
                    case 1:
                        h ^= *((Byte*)realData);
                        h *= m;
                        break;
                    default:
                        break;
                }
            }
            h ^= h >> 13;
            h *= m;
            h ^= h >> 15;
            return h;
        }

        const UInt32 m = 0x5bd1e995;
        const Int32 r = 24;
    }

}
