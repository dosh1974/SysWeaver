using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader
{
    /// <summary>
    /// Reads UInt64 values from an expression
    /// </summary>
    /// <typeparam name="T">The token type</typeparam>
    public sealed class UInt64ValueReader<T>
    {
        /// <summary>
        /// Create a reader using the invariant culture and NumberStyles.Integer
        /// </summary>
        /// <param name="toValueCreator">Creates a token from the start position, end position and value</param>
        public UInt64ValueReader(Func<int, int, UInt64, T> toValueCreator)
        {
            Format = CultureInfo.InvariantCulture;
            Style = NumberStyles.Integer;
            ToValueToken = toValueCreator;
        }

        /// <summary>
        /// Create a reader
        /// </summary>
        /// <param name="toValueCreator">Creates a token from the start position, end position and value</param>
        /// <param name="format">The format provider used for parsing</param>
        /// <param name="style">The number styles used for parsing</param>
        public UInt64ValueReader(Func<int, int, UInt64, T> toValueCreator, IFormatProvider format, NumberStyles style)
        {
            Format = format;
            Style = style;
            ToValueToken = toValueCreator;
        }
        /// <summary>
        /// The format provider used for parsing
        /// </summary>
        public readonly IFormatProvider Format;
        /// <summary>
        /// The number styles used for parsing
        /// </summary>
        public readonly NumberStyles Style;
        /// <summary>
        /// Creates a token from the start position, end position and value
        /// </summary>
        public readonly Func<int, int, UInt64, T> ToValueToken;

        /// <summary>
        /// Read a value, it ends at a whitespace, an operator character or the end of the expression.
        /// Values starting with "0x" (hexadecimal), "0b" (binary) or "0o" (octal) are parsed as unsigned 64-bit integers (see <see cref="ValueReader.TryParse(out ulong, string, string, int, int)"/>).
        /// </summary>
        /// <param name="context">The parsing context</param>
        /// <param name="index">The start position of the value, receives the end position</param>
        /// <returns>The value token</returns>
        /// <exception cref="ExpressionParserException">The text isn't a valid number</exception>
        public T ReadValue(IStringTokenizerContext context, ref int index)
        {
            var tokenizer = context.Tokenizer;
            var t = context.Expression;
            var tokenStart = index;
            var tlen = t.Length;
            ++index;
            for (; index < tlen; ++index)
            {
                var c = t[index];
                if (tokenizer.IsWhitespaceChar(c))
                    break;
                if (tokenizer.IsOperatorChar(c))
                    break;
            }
            var valueLen = index - tokenStart;
            var valueStr = t.Substring(tokenStart, valueLen);
            ulong tempInt;
            if (ValueReader.TryParse(out tempInt, valueStr, t, tokenStart, index))
                return ToValueToken(tokenStart, index, tempInt);
            try
            {
                var value = UInt64.Parse(valueStr, Style, Format);
                return ToValueToken(tokenStart, index, value);
            }
            catch (Exception ex)
            {
                throw new ExpressionParserException("Expected a integer number but found \"" + valueStr + "\"", t, tokenStart, index, ex);
            }
        }
    }

}
