using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SysWeaver
{


    public interface ICodeGenerator
    {
        
        /// <summary>
        /// Length of string
        /// </summary>
        int StrLen { get; }

        /// <summary>
        /// Length of string with grouping
        /// </summary>
        int GroupStrLen { get; }

        /// <summary>
        /// The maximum allowed value
        /// </summary>
        long MaxValue { get; }

        /// <summary>
        /// Number of bits that can be used
        /// </summary>
        int MaxBits { get; }

        /// <summary>
        /// Max input value 
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
        String Encode(long value, bool upperCase = true, bool group = true);

        /// <summary>
        /// Decodes a value from a string
        /// </summary>
        /// <param name="value">A value encoded as a string</param>
        /// <returns>The value or -1 if the input string is invalid</returns>
        long Decode(String value);

    }

    static class CodeGeneratorHelper
    {


        public static long Pow(long v, long e)
        {
            long r = 1;
            while (e > 0)
            {
                r *= v;
                --e;
            }
            return r;
        }

        public static int Log2(long v)
        {
            int s = 0;
            while (v > 1)
            {
                ++s;
                v >>= 1;
            }
            return s;
        }


        public static Dictionary<Char, int> GetValueLookUp(String s)
        {
            var exp = new Dictionary<char, String>()
        {
            { 'l', "i1" },
            { 'o', "0" },
            { 'v', "w" },
        };
            var d = new Dictionary<Char, int>();
            var sc = s.Length;
            for (int i = 0; i < sc; ++i)
            {
                var c = s[i];
                d[c] = i;
                d[Char.ToLower(c)] = i;
                d[Char.ToUpper(c)] = i;
                if (!exp.TryGetValue(c, out var e))
                    continue;
                foreach (var x in e)
                {
                    d[x] = i;
                    d[Char.ToUpper(x)] = i;
                    d[Char.ToLower(x)] = i;
                }
            }
            return d;
        }

    }

    /// <summary>
    /// Bundles similar symbols to the same meaning, ex: 1il, o0, vw etc.
    /// A class that converts an integer range into a string of alphanumerics.
    /// The valid range starts at 0 and the max value depends on the string length.
    /// Length 2 = 10 bits = [0, 1023]
    /// Length 4 = 20 bits = [0, 1048575]
    /// Length 6 = 30 bits = [0, 1073741823]
    /// ..and so on.
    /// </summary>
    public sealed class AlphaNumericCodeGenerator : ICodeGenerator
    { 
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
        /// Get a code generator with the specified length
        /// </summary>
        /// <param name="strLen">The desired length, every char add 5 bits of possible data</param>
        /// <returns>A code generator</returns>
        /// <exception cref="Exception"></exception>
        public static ICodeGenerator Get(int strLen)
        {
            if (strLen <= 0)
                throw new Exception("Invalid length!");
            var gens = Gens;
            var gen = gens[strLen];
            if (gen != null)
                return gen;
            gen = new AlphaNumericCodeGenerator(strLen);
            gens[strLen] = gen;
            return gen;
        }

        AlphaNumericCodeGenerator(int strLen)
        {
            StrLen = strLen;
            MaxValue = CodeGeneratorHelper.Pow(SymbolCount, strLen);
            MaxBits = CodeGeneratorHelper.Log2(MaxValue);
            MaxInput = 1L << MaxBits;
            InputMask = MaxInput - 1;
            GroupStrLen = SepLens[strLen];
        }

        public override string ToString() => String.Concat(StrLen.ToString(), ": [0, ", MaxValue.ToString(), ')');

        /// <summary>
        /// Length of string
        /// </summary>
        public int StrLen { get; init; }

        /// <summary>
        /// Length of string with grouping
        /// </summary>
        public int GroupStrLen { get; init; }

        /// <summary>
        /// The maximum allowed value
        /// </summary>
        public long MaxValue { get; init; }

        /// <summary>
        /// Number of bits that can be used
        /// </summary>
        public int MaxBits { get; init; }

        /// <summary>
        /// Max input value 
        /// </summary>
        public long MaxInput { get; init; }

        /// <summary>
        /// Bit mask to apply to get within the supported interval
        /// </summary>
        public long InputMask { get; init; }

        static readonly String SymbolsU = Symbols.FastToUpper();


        static readonly Dictionary<Char, int> ValueLookUp = CodeGeneratorHelper.GetValueLookUp(Symbols);


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
        /// <param name="group">Add a hypen every 4th character to create groups</param>
        /// <returns>A string with the encoded value</returns>
        public String Encode(long value, bool upperCase = true, bool group = true)
        {
            if ((value >= MaxInput) || (value < 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid value!");
            if (group)
                return String.Create(GroupStrLen, value, upperCase ? StringEncodeSepActionU : StringEncodeSepAction);
            return String.Create(StrLen, value, upperCase ? StringEncodeActionU : StringEncodeAction);
        }

        /// <summary>
        /// Decodes a value from a string
        /// </summary>
        /// <param name="value">A value encoded as a string</param>
        /// <returns>The value or -1 if the input string is invalid</returns>
        public long Decode(String value)
        {
            var l = ValueLookUp;
            long v = 0;
            int taken = 0;
            var t = value.Length;
            var strLen = StrLen;
            while (t > 0)
            {
                --t;
                if (!l.TryGetValue(value[t], out var p))
                    continue;
                v <<= 5;
                v |= (long)p;
                ++taken;
                if (taken == strLen)
                {
                    if (t != 0)
                        break;
                    return v;
                }
            }
            return -1;
        }




    }

    /// <summary>
    /// Bundles similar symbols to the same meaning, ex: 1il, o0 etc.
    /// A class that converts an integer range into a string of alphanumerics.
    /// The valid range starts at 0 and the max value depends on the string length.
    /// Length 2 = 10 bits = [0, 1023]
    /// Length 4 = 20 bits = [0, 1048575]
    /// Length 6 = 30 bits = [0, 1073741823]
    /// ..and so on.
    /// </summary>
    public sealed class NumericCodeGenerator : ICodeGenerator
    {
        public const int BitsPerChar = 3;

        const String Symbols = "23456789";
        const int SymbolCount = 8;

        /// <summary>
        /// Cached instances
        /// </summary>
        static readonly ICodeGenerator[] Gens = new ICodeGenerator[16];
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
        /// Get a code generator with the specified length
        /// </summary>
        /// <param name="strLen">The desired length, every char add 5 bits of possible data</param>
        /// <returns>A code generator</returns>
        /// <exception cref="Exception"></exception>
        public static ICodeGenerator Get(int strLen)
        {
            if (strLen <= 0)
                throw new Exception("Invalid length!");
            var gens = Gens;
            var gen = gens[strLen];
            if (gen != null)
                return gen;
            gen = new NumericCodeGenerator(strLen);
            gens[strLen] = gen;
            return gen;
        }

        NumericCodeGenerator(int strLen)
        {
            StrLen = strLen;
            MaxValue = CodeGeneratorHelper.Pow(SymbolCount, strLen);
            MaxBits = CodeGeneratorHelper.Log2(MaxValue);
            MaxInput = 1L << MaxBits;
            InputMask = MaxInput - 1;
            GroupStrLen = SepLens[strLen];
        }

        public override string ToString() => String.Concat(StrLen.ToString(), ": [0, ", MaxValue.ToString(), ')');

        /// <summary>
        /// Length of string
        /// </summary>
        public int StrLen { get; init; }

        /// <summary>
        /// Length of string with grouping
        /// </summary>
        public int GroupStrLen { get; init; }

        /// <summary>
        /// The maximum allowed value
        /// </summary>
        public long MaxValue { get; init; }

        /// <summary>
        /// Number of bits that can be used
        /// </summary>
        public int MaxBits { get; init; }

        /// <summary>
        /// Max input value 
        /// </summary>
        public long MaxInput { get; init; }

        /// <summary>
        /// Bit mask to apply to get within the supported interval
        /// </summary>
        public long InputMask { get; init; }

        static readonly String SymbolsU = Symbols.FastToUpper();


        static readonly Dictionary<Char, int> ValueLookUp = CodeGeneratorHelper.GetValueLookUp(Symbols);


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
        /// <param name="upperCase">Output upper or lower case letters</param>
        /// <param name="group">Add a hypen every 4th character to create groups</param>
        /// <returns>A string with the encoded value</returns>
        public String Encode(long value, bool upperCase = true, bool group = true)
        {
            if ((value >= MaxInput) || (value < 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid value!");
            if (group)
                return String.Create(GroupStrLen, value, upperCase ? StringEncodeSepActionU : StringEncodeSepAction);
            return String.Create(StrLen, value, upperCase ? StringEncodeActionU : StringEncodeAction);
        }

        /// <summary>
        /// Decodes a value from a string
        /// </summary>
        /// <param name="value">A value encoded as a string</param>
        /// <returns>The value or -1 if the input string is invalid</returns>
        public long Decode(String value)
        {
            var l = ValueLookUp;
            long v = 0;
            int taken = 0;
            var t = value.Length;
            var strLen = StrLen;
            while (t > 0)
            {
                --t;
                if (!l.TryGetValue(value[t], out var p))
                    continue;
                v <<= 3;
                v |= (long)p;
                ++taken;
                if (taken == strLen)
                {
                    if (t != 0)
                        break;
                    return v;
                }
            }
            return -1;
        }




    }


}



