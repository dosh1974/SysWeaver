using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader
{
    public static class ValueReader
    {
        public const String HexPrefix = "0x";
        public const String OctPrefix = "0&";
        public const String BinPrefix = "0b";

        public static bool IsHex(String valueStr)
        {
            return valueStr.StartsWith(HexPrefix, ExpressionEvaluator.DefaultStringComparison);
        }
        public static bool IsOct(String valueStr)
        {
            return valueStr.StartsWith(OctPrefix, ExpressionEvaluator.DefaultStringComparison);
        }
        public static bool IsBin(String valueStr)
        {
            return valueStr.StartsWith(BinPrefix, ExpressionEvaluator.DefaultStringComparison);
        }

        public static bool TryParse(out ulong value, String valueStr, String orgString, int tokenStart, int tokenEnd)
        {
            int valueLen = valueStr.Length;
            value = 0;
            if ((valueLen < 3) || (valueStr[0] != '0'))
                return false;
            if (IsHex(valueStr))
            {
                try
                {
                    value = UInt64.Parse(valueStr.Substring(2), NumberStyles.HexNumber);
                    return true;
                }
                catch (Exception ex2)
                {
                    throw new ExpressionParserException("Expected a hexadecimal number but found \"" + valueStr + "\"", orgString, tokenStart, tokenEnd, ex2);
                }
            }
            if (IsOct(valueStr))
            {
                UInt64 tempInt = 0;
                for (int i = 2; i < valueLen; ++i)
                {
                    char c = valueStr[i];
                    if ((c < '0') || (c > '7'))
                        throw new ExpressionParserException("Expected an octal number but found \"" + c + "\"", orgString, tokenStart + i, tokenEnd);
                    tempInt <<= 3;
                    tempInt |= (UInt64)((uint)(c - '0'));
                }
                value = tempInt;
                return true;
            }
            if (IsBin(valueStr))
            {
                UInt64 tempInt = 0;
                for (int i = 2; i < valueLen; ++i)
                {
                    char c = valueStr[i];
                    if ((c < '0') || (c > '1'))
                        throw new ExpressionParserException("Expected a binary number but found \"" + c + "\"", orgString, tokenStart + i, tokenEnd);
                    tempInt <<= 1;
                    tempInt |= (UInt64)((uint)(c - '0'));
                }
                value = tempInt;
                return true;
            }
            return false;
        }


    }

}
