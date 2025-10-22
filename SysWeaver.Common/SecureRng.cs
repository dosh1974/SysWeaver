using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;


namespace SysWeaver
{


    public static class SecureRngExt
    {
        /// <summary>
        /// Get a random value below some max value [0, maxValue).
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask()</param>
        /// <returns>A random values in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"></exception>
        public static UInt32 GetUInt32Max(this SecureRng r, UInt32 maxValue, UInt32 mask = 0)
        {
#if DEBUG
            if (maxValue == 0)
                throw new Exception("Invalid max value!");
#endif//DEBUG
            if (mask == 0)
                mask = maxValue.MaxMask();
            for (; ; )
            {
                var v = r.GetUInt32() & mask;
                if (v < maxValue)
                    return v;
            }
        }

        /// <summary>
        /// Get a random value below some max value [0, maxValue).
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask()</param>
        /// <returns>A random values in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"></exception>
        public static UInt64 GetUInt64Max(this SecureRng r, UInt64 maxValue, UInt64 mask = 0)
        {
#if DEBUG
            if (maxValue == 0)
                throw new Exception("Invalid max value!");
#endif//DEBUG
            if (mask == 0)
                mask = maxValue.MaxMask();
            for (; ; )
            {
                var v = r.GetUInt64() & mask;
                if (v < maxValue)
                    return v;
            }
        }


        /// <summary>
        /// Get a random value below some max value [0, maxValue).
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask()</param>
        /// <returns>A random values in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"></exception>
        public static Int32 GetInt32Max(this SecureRng r, Int32 maxValue, Int32 mask = 0)
        {
#if DEBUG
            if (maxValue <= 0)
                throw new Exception("Invalid max value!");
#endif//DEBUG
            if (mask == 0)
                mask = maxValue.MaxMask();
            for (; ; )
            {
                var v = r.GetInt32() & mask;
                if (v < maxValue)
                    return v;
            }
        }

        /// <summary>
        /// Get a random value below some max value [0, maxValue).
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask()</param>
        /// <returns>A random values in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"></exception>
        public static Int64 GetInt64Max(this SecureRng r, Int64 maxValue, Int64 mask = 0)
        {
#if DEBUG
            if (maxValue <= 0)
                throw new Exception("Invalid max value!");
#endif//DEBUG
            if (mask == 0)
                mask = maxValue.MaxMask();
            for (; ; )
            {
                var v = r.GetInt64() & mask;
                if (v < maxValue)
                    return v;
            }
        }

        /// <summary>
        /// Get a random number with in an inclusive range [min, max]
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random values in the [min, max] interval</returns>
        public static Int32 InRangeInt32(this SecureRng r, Int32 min, Int32 max)
        {
            var range = max - min;
            if (range < 0)
            {
                range = -range;
                min = max;
            }
            if (range <= 0)
                return min;
            ++range;
            return r.GetInt32Max(range) + min;
        }

        /// <summary>
        /// Get a random number with in an inclusive range [min, max]
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random values in the [min, max] interval</returns>
        public static Int64 InRangeInt64(this SecureRng r, Int64 min, Int64 max)
        {
            var range = max - min;
            if (range < 0)
            {
                range = -range;
                min = max;
            }
            if (range <= 0)
                return min;
            ++range;
            return r.GetInt64Max(range) + min;
        }

        /// <summary>
        /// Get a random number with in an inclusive range [min, max]
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random values in the [min, max] interval</returns>
        public static UInt32 InRangeUInt32(this SecureRng r, UInt32 min, UInt32 max)
        {
            var range = max - min;
            if (min > max)
            {
                range = min - max;
                min = max;
            }
            if (range <= 0)
                return min;
            ++range;
            return r.GetUInt32Max(range) + min;
        }

        /// <summary>
        /// Get a random number with in an inclusive range [min, max]
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random values in the [min, max] interval</returns>
        public static UInt64 InRangeUInt64(this SecureRng r, UInt64 min, UInt64 max)
        {
            var range = max - min;
            if (min > max)
            {
                range = min - max;
                min = max;
            }
            if (range <= 0)
                return min;
            ++range;
            return r.GetUInt64Max(range) + min;
        }


    }

    public static class NumericCodeRngExt
    {
        /// <summary>
        /// Get a random numeric code of N-digits as a string
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="numDigits">Number of digits (min 1)</param>
        /// <param name="maxRepeat">Maximum number of repeated digits, Ex: if 2, "223311" is ok, "153444" is not ok</param>
        /// <param name="maxInc">Maximum number of digits in an increasing or decreasing series, Ex: if 3, "123890" is ok, "543299" is not ok</param>
        /// <param name="nonZeroFirst">If true, the first number may not be 0</param>
        /// <returns>A "random" numerical string obeying the above rules</returns>
        public static String GetNumericCode(this SecureRng r, int numDigits = 6, int maxRepeat = 2, int maxInc = 3, bool nonZeroFirst = true)
        {
            int dataMax = numDigits + numDigits;
            Span<Char> s = stackalloc Char[numDigits];
            Span<Byte> data = stackalloc Byte[dataMax];
            int dataPtr = 0;
            int digit;
            for (; ; )
            {
                if (dataPtr <= 0)
                {
                    r.GetBytes(data);
                    dataPtr = dataMax;
                }
                --dataPtr;
                digit = data[dataPtr] & 0xf;
                if (digit < 10)
                {
                    if ((digit == 0) && nonZeroFirst)
                        continue;
                    break;
                }
            }
            s[0] = (Char)('0' + digit);
            var prev = digit;
            var prevDy = 10;
            int repCount = 0;
            for (int i = 1; i < numDigits;)
            {
                for (; ; )
                {
                    if (dataPtr <= 0)
                    {
                        r.GetBytes(data);
                        dataPtr = dataMax;
                    }
                    --dataPtr;
                    digit = data[dataPtr] & 0xf;
                    if (digit < 10)
                        break;
                }
                var dy = digit - prev;
                if ((dy >= -1) && (dy <= 1))
                {
                    ++repCount;
                    if (prevDy == dy)
                    {
                        if ((dy == 0) && (repCount >= maxRepeat))
                            continue;
                        if (repCount >= maxInc)
                            continue;
                    }
                }
                else
                {
                    repCount = 0;
                }
                prevDy = dy;
                prev = digit;
                s[i] = (Char)('0' + digit);
                ++i;
            }
            return new string(s);
        }

    }

    public static class GuidRngExt
    {

        /// <summary>
        /// Create a 24 character long GUID (144 bits, 18 bytes of rng)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <returns>A GUID as a string</returns>
        public static String GetGuid24(this SecureRng r)
        {
            Span<Byte> span = stackalloc Byte[18];
            r.GetBytes(span);
            return Convert.ToBase64String(span);
        }


        /// <summary>
        /// Create a 48 character long GUID (288 bits, 36 bytes of rng)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <returns>A GUID as a string</returns>
        public static String GetGuid48(this SecureRng r)
        {
            Span<Byte> span = stackalloc Byte[36];
            r.GetBytes(span);
            return Convert.ToBase64String(span);
        }

        /// <summary>
        /// Create a 24 character long GUID (144 bits, 10 bytes of rng and 8 bytes of time stamp)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="lifeTimeTicks">Ticks that get added to the time stamp</param>
        /// <returns>A GUID as a string</returns>
        public static String GetTimeStampGuid24(this SecureRng r, long lifeTimeTicks = 0)
        {
            Span<Byte> span = stackalloc Byte[18];
            r.GetBytes(span);
            BitConverter.TryWriteBytes(span, DateTime.UtcNow.Ticks + lifeTimeTicks);
            return Convert.ToBase64String(span);
        }

        /// <summary>
        /// Create a 48 character long GUID (288 bits, 28 bytes of rng and 8 bytes of time stamp)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="lifeTimeTicks">Ticks that get added to the time stamp</param>
        /// <returns>A GUID as a string</returns>
        public static String GetTimeStampGuid48(this SecureRng r, long lifeTimeTicks = 0)
        {
            Span<Byte> span = stackalloc Byte[36];
            r.GetBytes(span);
            BitConverter.TryWriteBytes(span, DateTime.UtcNow.Ticks + lifeTimeTicks);
            return Convert.ToBase64String(span);
        }


    }


    public sealed class SecureRng : IDisposable
    {
        public static SecureRng Get()
        {
            var i = Instances;
            if (i.TryPop(out var r))
                return r;
            return new SecureRng();
        }


        public void GetBytes(Byte[] data) => Rng.GetBytes(data);
        public void GetBytes(Byte[] data, int offset, int count) => Rng.GetBytes(data, offset, count);
        public void GetBytes(Span<Byte> data) => Rng.GetBytes(data);
        public Byte[] GetBytes(int count)
        {
            var t = GC.AllocateUninitializedArray<Byte>(count);
            GetBytes(t);
            return t;
        }


        public void Dispose()
        {
            Instances.Push(this);
        }

        /// <summary>
        /// Create a 24 character long GUID (144 bits, 18 bytes of hash value)
        /// </summary>
        /// <param name="data">Some data (that will get hashed)</param>
        /// <returns>A GUID as a string</returns>
        public static String GetHashGuid24(ReadOnlySpan<Byte> data)
        {
            Span<Byte> span = stackalloc Byte[64];
            SHA512.HashData(data, span);
            return Convert.ToBase64String(span.Slice(0, 18));
        }

        /// <summary>
        /// Create a 48 character long GUID (288 bits, 36 bytes of hash value)
        /// </summary>
        /// <param name="data">Some data (that will get hashed)</param>
        /// <returns>A GUID as a string</returns>
        public static String GetGuid48(ReadOnlySpan<Byte> data)
        {
            Span<Byte> span = stackalloc Byte[64];
            SHA512.HashData(data, span);
            return Convert.ToBase64String(span.Slice(0, 36));
        }


        /// <summary>
        /// Extract the time stamp from a guid created with GetTimeStampGuid**
        /// </summary>
        /// <param name="guid">A guid created using any of the GetTimeStampGuid**</param>
        /// <returns></returns>
        public static long GetTimeStampFromGuid(String guid)
        {
            var data = Convert.FromBase64String(guid);
            return BitConverter.ToInt64(data, 0);
        }


        /// <summary>
        /// Get 8 random bits
        /// </summary>
        /// <returns></returns>
        public Byte GetByte()
        {
            Span<Byte> span = stackalloc Byte[1];
            Rng.GetBytes(span);
            return span[0];
        }


        /// <summary>
        /// Get 8 random bits
        /// </summary>
        /// <returns></returns>
        public SByte GetSByte()
        {
            Span<Byte> span = stackalloc Byte[1];
            Rng.GetBytes(span);
            return (SByte)span[0];
        }

        /// <summary>
        /// Get 16 random bits
        /// </summary>
        /// <returns></returns>
        public UInt16 GetUInt16()
        {
            Span<Byte> span = stackalloc Byte[2];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<UInt16>(span);
        }

        /// <summary>
        /// Get 32 random bits
        /// </summary>
        /// <returns></returns>
        public UInt32 GetUInt32()
        {
            Span<Byte> span = stackalloc Byte[4];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<UInt32>(span);
        }

        /// <summary>
        /// Get 64 random bits
        /// </summary>
        /// <returns></returns>
        public UInt64 GetUInt64()
        {
            Span<Byte> span = stackalloc Byte[8];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<UInt64>(span);
        }


        /// <summary>
        /// Get 16 random bits
        /// </summary>
        /// <returns></returns>
        public Int16 GetInt16()
        {
            Span<Byte> span = stackalloc Byte[2];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<Int16>(span);
        }


        /// <summary>
        /// Get 32 random bits
        /// </summary>
        /// <returns></returns>
        public Int32 GetInt32()
        {
            Span<Byte> span = stackalloc Byte[4];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<Int32>(span);
        }

        /// <summary>
        /// Get 64 random bits
        /// </summary>
        /// <returns></returns>
        public Int64 GetInt64()
        {
            Span<Byte> span = stackalloc Byte[8];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<Int64>(span);
        }


        readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        static readonly ConcurrentStack<SecureRng> Instances = new ConcurrentStack<SecureRng>();



    }

}
