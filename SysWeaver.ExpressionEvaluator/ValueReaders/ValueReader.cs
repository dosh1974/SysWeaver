using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader
{
    /// <summary>
    /// Parsing of prefixed integer values (hexadecimal, octal and binary)
    /// </summary>
    public static class ValueReader
    {
        /// <summary>
        /// The prefix of hexadecimal values (case insensitive)
        /// </summary>
        public const String HexPrefix = "0x";
        /// <summary>
        /// The prefix of octal values (case insensitive)
        /// </summary>
        public const String OctPrefix = "0o";
        /// <summary>
        /// The prefix of binary values (case insensitive)
        /// </summary>
        public const String BinPrefix = "0b";

        /// <summary>
        /// Check if a value starts with the hexadecimal prefix (case insensitive)
        /// </summary>
        /// <param name="valueStr">The value text</param>
        /// <returns>True if it starts with the prefix</returns>
        public static bool IsHex(String valueStr)
        {
            return valueStr.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Check if a value starts with the octal prefix (case insensitive)
        /// </summary>
        /// <param name="valueStr">The value text</param>
        /// <returns>True if it starts with the prefix</returns>
        public static bool IsOct(String valueStr)
        {
            return valueStr.StartsWith(OctPrefix, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Check if a value starts with the binary prefix (case insensitive)
        /// </summary>
        /// <param name="valueStr">The value text</param>
        /// <returns>True if it starts with the prefix</returns>
        public static bool IsBin(String valueStr)
        {
            return valueStr.StartsWith(BinPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parse a prefixed (hexadecimal, octal or binary) value.
        /// Overflowing octal and binary values silently lose their high bits
        /// </summary>
        /// <param name="value">Receives the value</param>
        /// <param name="valueStr">The value text</param>
        /// <param name="orgString">The expression (used in exceptions)</param>
        /// <param name="tokenStart">The start position of the value in the expression (used in exceptions)</param>
        /// <param name="tokenEnd">The end position of the value in the expression (used in exceptions)</param>
        /// <returns>True if the value has a prefix and was parsed, false if it has no prefix (or is shorter than 3 characters)</returns>
        /// <exception cref="ExpressionParserException">The value has a prefix but the rest isn't a valid number</exception>
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
