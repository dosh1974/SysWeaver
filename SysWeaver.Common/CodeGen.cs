using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SysWeaver
{


    /// <summary>
    /// Converts an integer in the [0, MaxInput) interval to a short human friendly code (and back).
    /// Implemented by <see cref="AlphaNumericCodeGenerator"/> and <see cref="NumericCodeGenerator"/>.
    /// </summary>
    public interface ICodeGenerator
    {

        /// <summary>
        /// Length of string (number of symbols, excluding any group separators)
        /// </summary>
        int StrLen { get; }

        /// <summary>
        /// Length of string with grouping (number of symbols plus the number of hyphens)
        /// </summary>
        int GroupStrLen { get; }

        /// <summary>
        /// The exclusive upper bound of the values that can be encoded, the valid range is [0, MaxValue)
        /// </summary>
        long MaxValue { get; }

        /// <summary>
        /// Number of bits that can be used
        /// </summary>
        int MaxBits { get; }

        /// <summary>
        /// The exclusive upper bound of the values that can be encoded, the valid range is [0, MaxInput) (same as MaxValue)
        /// </summary>
        long MaxInput { get; }

        /// <summary>
        /// Bit mask to apply to get within the supported interval
        /// </summary>
        long InputMask { get; }

        /// <summary>
        /// Encode a value in the [0, MaxValue) interval.
        /// </summary>
        /// <param name="value">The value to encode in the [0, MaxValue)</param>
        /// <param name="upperCase">Output upper or lower case letters</param>
        /// <param name="group">Add a hypen to create groups</param>
        /// <returns>A string with the encoded value</returns>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or greater than or equal to <see cref="MaxInput"/></exception>
        String Encode(long value, bool upperCase = true, bool group = true);

        /// <summary>
        /// Decodes a value from a string.
        /// Decoding is case insensitive, similar looking symbols are treated as the same symbol and any char that isn't a symbol (such as hyphens and white space) is ignored.
        /// </summary>
        /// <param name="value">A value encoded as a string</param>
        /// <returns>The value or -1 if the input string is invalid (not exactly <see cref="StrLen"/> symbols, or a value that is outside of [0, MaxInput))</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        long Decode(String value);

    }

    /// <summary>
    /// Shared implementation of the code generators (symbol look up tables, decoding and instance caching)
    /// </summary>
    static class CodeGeneratorHelper
    {

        /// <summary>
        /// Create a look up table for all ASCII chars, the value is the symbol index or -1 if the char isn't a symbol.
        /// Symbols are case insensitive (invariant culture) and similar looking chars are mapped to the same symbol (l = i = 1, o = 0, v = w).
        /// </summary>
        /// <param name="s">The symbols (ASCII only)</param>
        /// <returns>A 128 entry look up table</returns>
        public static sbyte[] GetValueTable(String s)
        {
            var d = new sbyte[128];
            Array.Fill(d, (sbyte)-1);
            var sc = s.Length;
            for (int i = 0; i < sc; ++i)
            {
                var c = s[i];
                Set(d, c, i);
                var e = c switch
                {
                    'l' => "i1",
                    'o' => "0",
                    'v' => "w",
                    _ => null,
                };
                if (e == null)
                    continue;
                foreach (var x in e)
                    Set(d, x, i);
            }
            return d;
        }

        static void Set(sbyte[] d, Char c, int i)
        {
            d[c] = (sbyte)i;
            d[Char.ToLowerInvariant(c)] = (sbyte)i;
            d[Char.ToUpperInvariant(c)] = (sbyte)i;
        }

        /// <summary>
        /// Decode a string
        /// </summary>
        /// <param name="value">The string to decode</param>
        /// <param name="table">The look up table (from GetValueTable)</param>
        /// <param name="strLen">The required number of symbols</param>
        /// <param name="bitsPerChar">Number of bits per symbol</param>
        /// <param name="maxBits">Max number of bits in the result</param>
        /// <returns>The value or -1 if the string is invalid</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Decode(String value, sbyte[] table, int strLen, int bitsPerChar, int maxBits)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(value);
#endif//DEBUG
            // Before a shift the value must be below this limit, else the result would be outside of [0, 1 << maxBits)
            long limit = 1L << (maxBits - bitsPerChar);
            long v = 0;
            int taken = 0;
            ref var tr = ref MemoryMarshal.GetArrayDataReference(table);
            for (int t = value.Length - 1; t >= 0; --t)
            {
                uint c = value[t];
                if (c >= 128)
                    continue;
                int p = Unsafe.Add(ref tr, (nint)c);
                if (p < 0)
                    continue;
                if ((taken == strLen) || (v >= limit))
                    return -1;
                v = (v << bitsPerChar) | (long)p;
                ++taken;
            }
            return taken == strLen ? v : -1;
        }

        /// <summary>
        /// Thread safe get or create of a cached generator
        /// </summary>
        /// <param name="gens">The cache</param>
        /// <param name="strLen">The length</param>
        /// <param name="create">Creates a new generator</param>
        /// <returns>The cached generator</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="strLen"/> is less than 1 or not less than the length of <paramref name="gens"/></exception>
        public static ICodeGenerator GetOrCreate(ICodeGenerator[] gens, int strLen, Func<int, ICodeGenerator> create)
        {
            if ((strLen <= 0) || (strLen >= gens.Length))
                throw new ArgumentOutOfRangeException(nameof(strLen), strLen, "The length must be in the [1, " + (gens.Length - 1) + "] range!");
            var gen = Volatile.Read(ref gens[strLen]);
            if (gen != null)
                return gen;
            gen = create(strLen);
            return Interlocked.CompareExchange(ref gens[strLen], gen, null) ?? gen;
        }

    }

    /// <summary>
    /// Bundles similar symbols to the same meaning, ex: 1il, o0, vw etc.
    /// A class that converts an integer range into a string of alphanumerics.
    /// The valid range starts at 0 and the max value depends on the string length.
    /// Length 2 = 10 bits = [0, 1023]
    /// Length 4 = 20 bits = [0, 1048575]
    /// Length 6 = 30 bits = [0, 1073741823]
    /// ..and so on, lengths [1, 15] are supported (lengths of 13 or more are capped to 62 bits).
    /// Encoded symbols: abcdefghjklmnopqrstuvxyz23456789 (no i, w, 0 or 1), the least significant symbol is written first.
    /// </summary>
    public sealed class AlphaNumericCodeGenerator : ICodeGenerator
    {
        /// <summary>
        /// Number of bits encoded by every symbol
        /// </summary>
        public const int BitsPerChar = 5;

        const String Symbols = "abcdefghjklmnopqrstuvxyz23456789";
        const int SymbolCount = 32;

        /// <summary>
        /// Cached instances
        /// </summary>
        static readonly ICodeGenerator[] Gens = new ICodeGenerator[16];
        static readonly int[] SepMasks;
        static readonly int[] SepLens;

        static AlphaNumericCodeGenerator()
        {
            var s = new int[16];
            var sl = new int[16];
            SepLens = sl;
            s[5] = (1 << 3);                // 3-2
            s[6] = (1 << 3);                // 3-3
            s[7] = (1 << 4);                // 4-3
            s[8] = (1 << 4);                // 4-4
            s[9] = (1 << 3) | (1 << 6);     // 3-3-3
            s[10] = (1 << 4) | (1 << 7);    // 4-3-3
            s[11] = (1 << 4) | (1 << 8);    // 4-4-3
            s[12] = (1 << 4) | (1 << 8);    // 4-4-4
            s[13] = (1 << 4) | (1 << 7) | (1 << 10); // 4-3-3-3
            s[14] = (1 << 4) | (1 << 8) | (1 << 11); // 4-4-3-3
            s[15] = (1 << 4) | (1 << 8) | (1 << 12); // 4-4-4-3
            int max = 0;
            for (int i = 1; i < 16; ++ i)
            {
                int len = i;
                var bm = s[i];
                while (bm != 0)
                {
                    len += (bm & 1);
                    bm >>= 1;
                }
                sl[i] = len;
                if (len > max)
                    max = len;
            }
            var ssl = new int[max+ 1];
            SepMasks = ssl;
            for (int i = 1; i < 16; ++i)
            {
                var len = sl[i];
                ssl[len] = s[i];
            }
        }

        /// <summary>
        /// Get a code generator with the specified length (instances are cached, the same instance is always returned for a given length)
        /// </summary>
        /// <param name="strLen">The desired length [1, 15], every char add 5 bits of possible data (up to a max of 62 bits)</param>
        /// <returns>A code generator</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="strLen"/> is less than 1 or greater than 15</exception>
        public static ICodeGenerator Get(int strLen)
            => CodeGeneratorHelper.GetOrCreate(Gens, strLen, Create);

        static readonly Func<int, ICodeGenerator> Create = static len => new AlphaNumericCodeGenerator(len);

        AlphaNumericCodeGenerator(int strLen)
        {
            StrLen = strLen;
            // SymbolCount ^ strLen = 1 << (BitsPerChar * strLen), capped to 62 bits to stay a positive long
            var bits = Math.Min(BitsPerChar * strLen, 62);
            MaxBits = bits;
            MaxValue = 1L << bits;
            MaxInput = 1L << bits;
            InputMask = MaxInput - 1;
            GroupStrLen = SepLens[strLen];
        }

        /// <summary>
        /// Returns a description of the generator
        /// </summary>
        /// <returns>The string length and the valid range, ex: "4: [0, 1048576)"</returns>
        public override string ToString() => String.Concat(StrLen.ToString(), ": [0, ", MaxValue.ToString(), ')');

        /// <summary>
        /// Length of string (number of symbols, excluding any group separators)
        /// </summary>
        public int StrLen { get; init; }

        /// <summary>
        /// Length of string with grouping (number of symbols plus the number of hyphens)
        /// </summary>
        public int GroupStrLen { get; init; }

        /// <summary>
        /// The exclusive upper bound of the values that can be encoded, the valid range is [0, MaxValue)
        /// </summary>
        public long MaxValue { get; init; }

        /// <summary>
        /// Number of bits that can be used
        /// </summary>
        public int MaxBits { get; init; }

        /// <summary>
        /// The exclusive upper bound of the values that can be encoded, the valid range is [0, MaxInput) (same as MaxValue)
        /// </summary>
        public long MaxInput { get; init; }

        /// <summary>
        /// Bit mask to apply to get within the supported interval
        /// </summary>
        public long InputMask { get; init; }

        static readonly String SymbolsU = Symbols.FastToUpper();


        static readonly sbyte[] ValueTable = CodeGeneratorHelper.GetValueTable(Symbols);


        static void StringEncode(Span<char> to, long value)
        {
            var v = Symbols;
            var tol = to.Length;
            for (int i = 0; i < tol; ++i)
            {
                to[i] = v[(int)(value & 0x1f)];
                value >>= 5;
            }
        }

        static void StringEncodeSep(Span<char> to, long value)
        {
            var v = Symbols;
            var tol = to.Length;
            int sep = SepMasks[tol];
            for (int o = 0; ;)
            {
                to[o] = v[(int)(value & 0x1f)];
                ++o;
                sep >>= 1;
                if (o >= tol)
                    return;
                value >>= 5;
                if ((sep & 1) == 0)
                    continue;
                to[o] = '-';
                ++o;
            }
        }


        static void StringEncodeU(Span<char> to, long value)
        {
            var v = SymbolsU;
            var tol = to.Length;
            for (int i = 0; i < tol; ++i)
            {
                to[i] = v[(int)(value & 0x1f)];
                value >>= 5;
            }
        }

        static void StringEncodeSepU(Span<char> to, long value)
        {
            var v = SymbolsU;
            var tol = to.Length;
            int sep = SepMasks[tol];
            for (int o = 0; ;)
            {
                to[o] = v[(int)(value & 0x1f)];
                ++o;
                sep >>= 1;
                if (o >= tol)
                    return;
                value >>= 5;
                if ((sep & 1) == 0)
                    continue;
                to[o] = '-';
                ++o;
            }
        }




        static readonly SpanAction<char, long> StringEncodeAction = StringEncode;
        static readonly SpanAction<char, long> StringEncodeSepAction = StringEncodeSep;
        static readonly SpanAction<char, long> StringEncodeActionU = StringEncodeU;
        static readonly SpanAction<char, long> StringEncodeSepActionU = StringEncodeSepU;

        /// <summary>
        /// Encode a value in the [0, MaxInput) interval.
        /// </summary>
        /// <param name="value">The value to encode in the [0, MaxInput)</param>
        /// <param name="upperCase">Output upper or lower case letters</param>
        /// <param name="group">Add a hypen between groups of 3 or 4 characters</param>
        /// <returns>A string with the encoded value</returns>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or greater than or equal to <see cref="MaxInput"/></exception>
        public String Encode(long value, bool upperCase = true, bool group = true)
        {
            if ((value >= MaxInput) || (value < 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid value!");
            if (group)
                return String.Create(GroupStrLen, value, upperCase ? StringEncodeSepActionU : StringEncodeSepAction);
            return String.Create(StrLen, value, upperCase ? StringEncodeActionU : StringEncodeAction);
        }

        /// <summary>
        /// Decodes a value from a string.
        /// Decoding is case insensitive, similar looking symbols are treated as the same symbol and any char that isn't a symbol (such as hyphens and white space) is ignored.
        /// </summary>
        /// <param name="value">A value encoded as a string</param>
        /// <returns>The value or -1 if the input string is invalid (not exactly <see cref="StrLen"/> symbols, or a value that is outside of [0, MaxInput))</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public long Decode(String value)
            => CodeGeneratorHelper.Decode(value, ValueTable, StrLen, BitsPerChar, MaxBits);




    }

    /// <summary>
    /// A class that converts an integer range into a string of digits (only 2-9 are used, so that the codes can't be confused with letters).
    /// The valid range starts at 0 and the max value depends on the string length.
    /// Length 2 = 6 bits = [0, 63]
    /// Length 4 = 12 bits = [0, 4095]
    /// Length 6 = 18 bits = [0, 262143]
    /// ..and so on, lengths [1, 21] are supported (length 21 is capped to 62 bits).
    /// The least significant symbol is written first.
    /// </summary>
    /// <remarks>When decoding, the digits 0 and 1 (and any other non symbol char) are ignored, they are not rejected</remarks>
    public sealed class NumericCodeGenerator : ICodeGenerator
    {
        /// <summary>
        /// Number of bits encoded by every symbol
        /// </summary>
        public const int BitsPerChar = 3;

        const String Symbols = "23456789";
        const int SymbolCount = 8;

        /// <summary>
        /// Cached instances
        /// </summary>
        static readonly ICodeGenerator[] Gens = new ICodeGenerator[22];
        static readonly int[] SepMasks;
        static readonly int[] SepLens;

        static NumericCodeGenerator()
        {
            var s = new int[22];
            var sl = new int[22];
            SepLens = sl;
            s[5] = (1 << 3);                // 3-2
            s[6] = (1 << 3);                // 3-3
            s[7] = (1 << 4);                // 4-3
            s[8] = (1 << 4);                // 4-4
            s[9] = (1 << 3) | (1 << 6);     // 3-3-3
            s[10] = (1 << 4) | (1 << 7);    // 4-3-3
            s[11] = (1 << 4) | (1 << 8);    // 4-4-3
            s[12] = (1 << 4) | (1 << 8);    // 4-4-4
            s[13] = (1 << 4) | (1 << 7) | (1 << 10); // 4-3-3-3
            s[14] = (1 << 4) | (1 << 8) | (1 << 11); // 4-4-3-3
            s[15] = (1 << 4) | (1 << 8) | (1 << 12); // 4-4-4-3
            s[16] = (1 << 4) | (1 << 8) | (1 << 12); // 4-4-4-4

            s[17] = (1 << 4) | (1 << 8) | (1 << 11) | (1 << 14); // 4-4-3-3-3
            s[18] = (1 << 4) | (1 << 8) | (1 << 12) | (1 << 15); // 4-4-4-3-3

            s[19] = (1 << 4) | (1 << 8) | (1 << 12) | (1 << 16); // 4-4-4-4-3
            s[20] = (1 << 4) | (1 << 8) | (1 << 12) | (1 << 16); // 4-4-4-4-4

            s[21] = (1 << 4) | (1 << 8) | (1 << 12) | (1 << 15) | (1 << 18); // 4-4-4-3-3-3

            int max = 0;
            for (int i = 1; i < 22; ++i)
            {
                int len = i;
                var bm = s[i];
                while (bm != 0)
                {
                    bm >>= 1;
                    len += (bm & 1);
                }
                sl[i] = len;
                if (len > max)
                    max = len;
            }
            var ssl = new int[max + 1];
            SepMasks = ssl;
            for (int i = 1; i < 22; ++i)
            {
                var len = sl[i];
                ssl[len] = s[i];
            }
        }



        /// <summary>
        /// Get a code generator with the specified length (instances are cached, the same instance is always returned for a given length)
        /// </summary>
        /// <param name="strLen">The desired length [1, 21], every char add 3 bits of possible data (up to a max of 62 bits)</param>
        /// <returns>A code generator</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="strLen"/> is less than 1 or greater than 21</exception>
        public static ICodeGenerator Get(int strLen)
            => CodeGeneratorHelper.GetOrCreate(Gens, strLen, Create);

        static readonly Func<int, ICodeGenerator> Create = static len => new NumericCodeGenerator(len);

        NumericCodeGenerator(int strLen)
        {
            StrLen = strLen;
            // SymbolCount ^ strLen = 1 << (BitsPerChar * strLen), capped to 62 bits to stay a positive long
            var bits = Math.Min(BitsPerChar * strLen, 62);
            MaxBits = bits;
            MaxValue = 1L << bits;
            MaxInput = 1L << bits;
            InputMask = MaxInput - 1;
            GroupStrLen = SepLens[strLen];
        }

        /// <summary>
        /// Returns a description of the generator
        /// </summary>
        /// <returns>The string length and the valid range, ex: "4: [0, 4096)"</returns>
        public override string ToString() => String.Concat(StrLen.ToString(), ": [0, ", MaxValue.ToString(), ')');

        /// <summary>
        /// Length of string (number of symbols, excluding any group separators)
        /// </summary>
        public int StrLen { get; init; }

        /// <summary>
        /// Length of string with grouping (number of symbols plus the number of hyphens)
        /// </summary>
        public int GroupStrLen { get; init; }

        /// <summary>
        /// The exclusive upper bound of the values that can be encoded, the valid range is [0, MaxValue)
        /// </summary>
        public long MaxValue { get; init; }

        /// <summary>
        /// Number of bits that can be used
        /// </summary>
        public int MaxBits { get; init; }

        /// <summary>
        /// The exclusive upper bound of the values that can be encoded, the valid range is [0, MaxInput) (same as MaxValue)
        /// </summary>
        public long MaxInput { get; init; }

        /// <summary>
        /// Bit mask to apply to get within the supported interval
        /// </summary>
        public long InputMask { get; init; }

        static readonly String SymbolsU = Symbols.FastToUpper();


        static readonly sbyte[] ValueTable = CodeGeneratorHelper.GetValueTable(Symbols);


        static void StringEncode(Span<char> to, long value)
        {
            var v = Symbols;
            var tol = to.Length;
            for (int i = 0; i < tol; ++i)
            {
                to[i] = v[(int)(value & 0x7)];
                value >>= 3;
            }
        }

        static void StringEncodeSep(Span<char> to, long value)
        {
            var v = Symbols;
            var tol = to.Length;
            int sep = SepMasks[tol];
            for (int o = 0; ;)
            {
                to[o] = v[(int)(value & 0x7)];
                ++o;
                sep >>= 1;
                if (o >= tol)
                    return;
                value >>= 3;
                if ((sep & 1) == 0)
                    continue;
                to[o] = '-';
                ++o;
            }
        }


        static void StringEncodeU(Span<char> to, long value)
        {
            var v = SymbolsU;
            var tol = to.Length;
            for (int i = 0; i < tol; ++i)
            {
                to[i] = v[(int)(value & 0x7)];
                value >>= 3;
            }
        }

        static void StringEncodeSepU(Span<char> to, long value)
        {
            var v = SymbolsU;
            var tol = to.Length;
            int sep = SepMasks[tol];
            for (int o = 0; ;)
            {
                to[o] = v[(int)(value & 0x7)];
                ++o;
                sep >>= 1;
                if (o >= tol)
                    return;
                value >>= 3;
                if ((sep & 1) == 0)
                    continue;
                to[o] = '-';
                ++o;
            }
        }




        static readonly SpanAction<char, long> StringEncodeAction = StringEncode;
        static readonly SpanAction<char, long> StringEncodeSepAction = StringEncodeSep;
        static readonly SpanAction<char, long> StringEncodeActionU = StringEncodeU;
        static readonly SpanAction<char, long> StringEncodeSepActionU = StringEncodeSepU;

        /// <summary>
        /// Encode a value in the [0, MaxInput) interval.
        /// </summary>
        /// <param name="value">The value to encode in the [0, MaxInput)</param>
        /// <param name="upperCase">Not used (digits only)</param>
        /// <param name="group">Add a hypen between groups of 3 or 4 characters</param>
        /// <returns>A string with the encoded value</returns>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or greater than or equal to <see cref="MaxInput"/></exception>
        public String Encode(long value, bool upperCase = true, bool group = true)
        {
            if ((value >= MaxInput) || (value < 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid value!");
            if (group)
                return String.Create(GroupStrLen, value, upperCase ? StringEncodeSepActionU : StringEncodeSepAction);
            return String.Create(StrLen, value, upperCase ? StringEncodeActionU : StringEncodeAction);
        }

        /// <summary>
        /// Decodes a value from a string.
        /// Any char that isn't a symbol (such as hyphens, white space, 0 and 1) is ignored.
        /// </summary>
        /// <param name="value">A value encoded as a string</param>
        /// <returns>The value or -1 if the input string is invalid (not exactly <see cref="StrLen"/> symbols, or a value that is outside of [0, MaxInput))</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public long Decode(String value)
            => CodeGeneratorHelper.Decode(value, ValueTable, StrLen, BitsPerChar, MaxBits);




    }


}



