using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;


namespace SysWeaver
{


    /// <summary>
    /// Extension methods for getting uniformly distributed random numbers in a range from a <see cref="SecureRng"/> (using rejection sampling, so there is no modulo bias).
    /// </summary>
    public static class SecureRngExt
    {
        /// <summary>
        /// Get a random value below some max value [0, maxValue).
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask() (0 means that it's computed)</param>
        /// <returns>A random value in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"><paramref name="maxValue"/> is 0</exception>
        public static UInt32 GetUInt32Max(this SecureRng r, UInt32 maxValue, UInt32 mask = 0)
        {
            if (maxValue == 0)
                throw new Exception("Invalid max value!");
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
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask() (0 means that it's computed)</param>
        /// <returns>A random value in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"><paramref name="maxValue"/> is 0</exception>
        public static UInt64 GetUInt64Max(this SecureRng r, UInt64 maxValue, UInt64 mask = 0)
        {
            if (maxValue == 0)
                throw new Exception("Invalid max value!");
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
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask() (0 means that it's computed)</param>
        /// <returns>A random value in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"><paramref name="maxValue"/> is 0 or negative</exception>
        public static Int32 GetInt32Max(this SecureRng r, Int32 maxValue, Int32 mask = 0)
        {
            if (maxValue <= 0)
                throw new Exception("Invalid max value!");
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
        /// <param name="mask">An optional precomputed mask value for speed, use mask = maxValue.MaxMask() (0 means that it's computed)</param>
        /// <returns>A random value in the [0, maxValue) interval</returns>
        /// <exception cref="Exception"><paramref name="maxValue"/> is 0 or negative</exception>
        public static Int64 GetInt64Max(this SecureRng r, Int64 maxValue, Int64 mask = 0)
        {
            if (maxValue <= 0)
                throw new Exception("Invalid max value!");
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
        /// Get a random number within an inclusive range [min, max] (if min is greater than max they are swapped)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random value in the [min, max] interval</returns>
        /// <remarks>Any range is supported, including [Int32.MinValue, Int32.MaxValue], all values in the range are equally likely</remarks>
        public static Int32 InRangeInt32(this SecureRng r, Int32 min, Int32 max)
        {
            if (min > max)
                (min, max) = (max, min);
            // The difference always fits in an unsigned value (wraps correctly in unchecked arithmetic)
            var range = unchecked((UInt32)(max - min));
            if (range == 0)
                return min;
            if (range == UInt32.MaxValue)
                return r.GetInt32();
            return unchecked(min + (Int32)r.GetUInt32Max(range + 1));
        }

        /// <summary>
        /// Get a random number within an inclusive range [min, max] (if min is greater than max they are swapped)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random value in the [min, max] interval</returns>
        /// <remarks>Any range is supported, including [Int64.MinValue, Int64.MaxValue], all values in the range are equally likely</remarks>
        public static Int64 InRangeInt64(this SecureRng r, Int64 min, Int64 max)
        {
            if (min > max)
                (min, max) = (max, min);
            // The difference always fits in an unsigned value (wraps correctly in unchecked arithmetic)
            var range = unchecked((UInt64)(max - min));
            if (range == 0)
                return min;
            if (range == UInt64.MaxValue)
                return r.GetInt64();
            return unchecked(min + (Int64)r.GetUInt64Max(range + 1));
        }

        /// <summary>
        /// Get a random number within an inclusive range [min, max] (if min is greater than max they are swapped)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random value in the [min, max] interval</returns>
        /// <remarks>Any range is supported, including [0, UInt32.MaxValue], all values in the range are equally likely</remarks>
        public static UInt32 InRangeUInt32(this SecureRng r, UInt32 min, UInt32 max)
        {
            if (min > max)
                (min, max) = (max, min);
            var range = max - min;
            if (range == 0)
                return min;
            if (range == UInt32.MaxValue)
                return r.GetUInt32();
            return r.GetUInt32Max(range + 1) + min;
        }

        /// <summary>
        /// Get a random number within an inclusive range [min, max] (if min is greater than max they are swapped)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="min">The minimum inclusive value</param>
        /// <param name="max">The maximum inclusive value</param>
        /// <returns>A random value in the [min, max] interval</returns>
        /// <remarks>Any range is supported, including [0, UInt64.MaxValue], all values in the range are equally likely</remarks>
        public static UInt64 InRangeUInt64(this SecureRng r, UInt64 min, UInt64 max)
        {
            if (min > max)
                (min, max) = (max, min);
            var range = max - min;
            if (range == 0)
                return min;
            if (range == UInt64.MaxValue)
                return r.GetUInt64();
            return r.GetUInt64Max(range + 1) + min;
        }


    }

    /// <summary>
    /// Extension methods for creating random numeric codes (ex: one time codes sent by email / sms)
    /// </summary>
    public static class NumericCodeRngExt
    {
        /// <summary>
        /// Get a random numeric code of N-digits as a string, avoiding codes that are easy to guess (long repeats or series)
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="numDigits">Number of digits (min 1, the digits are stack allocated so keep it small)</param>
        /// <param name="maxRepeat">Maximum number of repeated digits, Ex: if 2, "223311" is ok, "153444" is not ok</param>
        /// <param name="maxInc">Maximum number of digits in an increasing or decreasing series, Ex: if 3, "123890" is ok, "543299" is not ok</param>
        /// <param name="nonZeroFirst">If true, the first number may not be 0</param>
        /// <returns>A "random" numerical string obeying the above rules</returns>
        /// <exception cref="IndexOutOfRangeException"><paramref name="numDigits"/> is less than 1</exception>
        /// <remarks>
        /// The repeat run (equal digits) and the series run (steps of +1 or -1 in the same direction) are tracked separately,
        /// ex: with the defaults "112234" is ok (repeats of 2 and a series of 3).
        /// </remarks>
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
            //  Number of equal digits ending with the previous digit
            int repRun = 1;
            //  Number of digits in the series ending with the previous digit (and the step direction of that series, 0 if no series)
            int seriesRun = 1;
            int seriesDy = 0;
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
                if (dy == 0)
                {
                    if ((repRun + 1) > maxRepeat)
                        continue;
                    ++repRun;
                    seriesRun = 1;
                    seriesDy = 0;
                }
                else if ((dy == -1) || (dy == 1))
                {
                    var newSeries = dy == seriesDy ? seriesRun + 1 : 2;
                    if (newSeries > maxInc)
                        continue;
                    seriesRun = newSeries;
                    seriesDy = dy;
                    repRun = 1;
                }
                else
                {
                    repRun = 1;
                    seriesRun = 1;
                    seriesDy = 0;
                }
                prev = digit;
                s[i] = (Char)('0' + digit);
                ++i;
            }
            return new string(s);
        }

    }

    /// <summary>
    /// Extension methods for creating random, base64 encoded identifiers (unguessable tokens)
    /// </summary>
    /// <remarks>The identifiers are standard base64 (may contain '+' and '/'), so they must be escaped if used in urls or file names</remarks>
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
        /// Create a 24 character long GUID (144 bits, 10 bytes of rng and 8 bytes of time stamp).
        /// The time stamp (UtcNow ticks + <paramref name="lifeTimeTicks"/>, machine endian) is the first 8 bytes and can be read using <see cref="SecureRng.GetTimeStampFromGuid(string)"/>.
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="lifeTimeTicks">Ticks that get added to the time stamp (ex: to encode an expiration time)</param>
        /// <returns>A GUID as a string</returns>
        public static String GetTimeStampGuid24(this SecureRng r, long lifeTimeTicks = 0)
        {
            Span<Byte> span = stackalloc Byte[18];
            r.GetBytes(span);
            BitConverter.TryWriteBytes(span, DateTime.UtcNow.Ticks + lifeTimeTicks);
            return Convert.ToBase64String(span);
        }

        /// <summary>
        /// Create a 48 character long GUID (288 bits, 28 bytes of rng and 8 bytes of time stamp).
        /// The time stamp (UtcNow ticks + <paramref name="lifeTimeTicks"/>, machine endian) is the first 8 bytes and can be read using <see cref="SecureRng.GetTimeStampFromGuid(string)"/>.
        /// </summary>
        /// <param name="r">The rng to use</param>
        /// <param name="lifeTimeTicks">Ticks that get added to the time stamp (ex: to encode an expiration time)</param>
        /// <returns>A GUID as a string</returns>
        public static String GetTimeStampGuid48(this SecureRng r, long lifeTimeTicks = 0)
        {
            Span<Byte> span = stackalloc Byte[36];
            r.GetBytes(span);
            BitConverter.TryWriteBytes(span, DateTime.UtcNow.Ticks + lifeTimeTicks);
            return Convert.ToBase64String(span);
        }


    }


    /// <summary>
    /// A cryptographically secure random number generator (wraps <see cref="RandomNumberGenerator"/>), instances are pooled.
    /// Use the pattern: using var rng = SecureRng.Get();
    /// </summary>
    /// <remarks>
    /// Dispose returns the instance to a shared pool, so it must not be used after it's disposed (and must not be disposed twice).
    /// Extension methods are found in <see cref="SecureRngExt"/>, <see cref="NumericCodeRngExt"/> and <see cref="GuidRngExt"/>.
    /// </remarks>
    public sealed class SecureRng : IDisposable
    {
        /// <summary>
        /// Get an instance from the pool (or create a new one if the pool is empty), dispose it when done to return it to the pool
        /// </summary>
        /// <returns>An instance that is owned by the caller until disposed</returns>
        public static SecureRng Get()
        {
            var i = Instances;
            if (i.TryPop(out var r))
                return r;
            return new SecureRng();
        }


        /// <summary>
        /// Fill an array with random bytes
        /// </summary>
        /// <param name="data">The array to fill</param>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null</exception>
        public void GetBytes(Byte[] data) => Rng.GetBytes(data);
        /// <summary>
        /// Fill a part of an array with random bytes
        /// </summary>
        /// <param name="data">The array to fill</param>
        /// <param name="offset">The index of the first byte to fill</param>
        /// <param name="count">The number of bytes to fill</param>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative</exception>
        /// <exception cref="ArgumentException">The range is outside of the array</exception>
        public void GetBytes(Byte[] data, int offset, int count) => Rng.GetBytes(data, offset, count);
        /// <summary>
        /// Fill a span with random bytes
        /// </summary>
        /// <param name="data">The span to fill</param>
        public void GetBytes(Span<Byte> data) => Rng.GetBytes(data);
        /// <summary>
        /// Get a new array with random bytes
        /// </summary>
        /// <param name="count">The number of bytes</param>
        /// <returns>A new array with <paramref name="count"/> random bytes</returns>
        /// <exception cref="OverflowException"><paramref name="count"/> is negative</exception>
        public Byte[] GetBytes(int count)
        {
            var t = GC.AllocateUninitializedArray<Byte>(count);
            GetBytes(t);
            return t;
        }


        /// <summary>
        /// Return this instance to the pool (it must not be used after this)
        /// </summary>
        public void Dispose()
        {
            Instances.Push(this);
        }

        /// <summary>
        /// Create a 16 character long GUID (96 bits, 12 bytes of randomness)
        /// </summary>
        /// <returns>A GUID as a string</returns>
        public static String GetHashGuid16()
        {
            using var r = Get();
            Span<Byte> span = stackalloc Byte[12];
            r.GetBytes(span);
            return Convert.ToBase64String(span);
        }


        /// <summary>
        /// Create a 24 character long GUID (144 bits, 18 bytes of randomness), base64 encoded.
        /// </summary>
        /// <returns>A GUID as a string</returns>
        public static String GetHashGuid24()
        {
            using var r = Get();
            Span<Byte> span = stackalloc Byte[18];
            r.GetBytes(span);
            return Convert.ToBase64String(span);
        }


        /// <summary>
        /// Create a 48 character long GUID (288 bits, 36 bytes of randomness)
        /// </summary>
        /// <returns>A GUID as a string</returns>
        public static String GetHashGuid48()
        {
            using var r = Get();
            Span<Byte> span = stackalloc Byte[36];
            r.GetBytes(span);
            return Convert.ToBase64String(span);
        }


        /// <summary>
        /// Create a deterministic 24 character long GUID (144 bits, the first 18 bytes of the SHA512 hash of the data)
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
        /// Create a deterministic 48 character long GUID (288 bits, the first 36 bytes of the SHA512 hash of the data)
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
        /// <returns>The time stamp in ticks (UTC ticks + the life time ticks used when the guid was created)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="guid"/> is null</exception>
        /// <exception cref="FormatException"><paramref name="guid"/> isn't valid base64</exception>
        /// <exception cref="ArgumentException">The decoded data is shorter than 8 bytes</exception>
        public static long GetTimeStampFromGuid(String guid)
        {
            var data = Convert.FromBase64String(guid);
            return BitConverter.ToInt64(data, 0);
        }


        /// <summary>
        /// Get 8 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public Byte GetByte()
        {
            Span<Byte> span = stackalloc Byte[1];
            Rng.GetBytes(span);
            return span[0];
        }


        /// <summary>
        /// Get 8 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public SByte GetSByte()
        {
            Span<Byte> span = stackalloc Byte[1];
            Rng.GetBytes(span);
            return (SByte)span[0];
        }

        /// <summary>
        /// Get 16 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public UInt16 GetUInt16()
        {
            Span<Byte> span = stackalloc Byte[2];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<UInt16>(span);
        }

        /// <summary>
        /// Get 32 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public UInt32 GetUInt32()
        {
            Span<Byte> span = stackalloc Byte[4];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<UInt32>(span);
        }

        /// <summary>
        /// Get 64 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public UInt64 GetUInt64()
        {
            Span<Byte> span = stackalloc Byte[8];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<UInt64>(span);
        }


        /// <summary>
        /// Get 16 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public Int16 GetInt16()
        {
            Span<Byte> span = stackalloc Byte[2];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<Int16>(span);
        }


        /// <summary>
        /// Get 32 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
        public Int32 GetInt32()
        {
            Span<Byte> span = stackalloc Byte[4];
            Rng.GetBytes(span);
            return MemoryMarshal.Read<Int32>(span);
        }

        /// <summary>
        /// Get 64 random bits
        /// </summary>
        /// <returns>A random value (all values are equally likely)</returns>
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
