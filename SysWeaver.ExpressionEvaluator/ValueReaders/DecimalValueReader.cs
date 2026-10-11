using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader
{
    /// <summary>
    /// Reads Decimal values from an expression
    /// </summary>
    /// <typeparam name="T">The token type</typeparam>
    public sealed class DecimalValueReader<T>
    {
        /// <summary>
        /// Create a reader using the invariant culture and NumberStyles.Float
        /// </summary>
        /// <param name="toValueCreator">Creates a token from the start position, end position and value</param>
        public DecimalValueReader(Func<int, int, Decimal, T> toValueCreator)
        {
            Format = CultureInfo.InvariantCulture;
            Style = NumberStyles.Float;
            ToValueToken = toValueCreator;
        }

        /// <summary>
        /// Create a reader
        /// </summary>
        /// <param name="toValueCreator">Creates a token from the start position, end position and value</param>
        /// <param name="format">The format provider used for parsing</param>
        /// <param name="style">The number styles used for parsing</param>
        public DecimalValueReader(Func<int, int, Decimal, T> toValueCreator, IFormatProvider format, NumberStyles style)
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
        public readonly Func<int, int, Decimal, T> ToValueToken;

        /// <summary>
        /// Read a value, it ends at a whitespace, an operator character or the end of the expression.
        /// Values starting with "0x" (hexadecimal), "0b" (binary) or "0o" (octal) are parsed as unsigned 64-bit integers (see <see cref="ValueReader.TryParse(out ulong, string, string, int, int)"/>). A ',' is read as the decimal separator (it's part of the value only if the value is the first token of the expression, else it's the argument separator).
        /// An exponent sign ('e+' / 'e-') is part of the value.
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
            bool first = true;
            for (; index < tlen; ++index)
            {
                var c = t[index];
                if (tokenizer.IsWhitespaceChar(c))
                    break;
                if (first && ((c == '-') || (c == '+')))
                {
                    first = false;
                    var p = t[index - 1];
                    if ((p == 'e') || (p == 'E'))
                        continue;
                }
                if (tokenizer.IsOperatorChar(c))
                {
                    if ((c != ',') || (context.TokenIndex > 0))
                        break;
                }
            }
            var valueLen = index - tokenStart;
            var valueStr = t.Substring(tokenStart, valueLen);
            ulong tempInt;
            if (ValueReader.TryParse(out tempInt, valueStr.Replace(',', '.'), t, tokenStart, index))
                return ToValueToken(tokenStart, index, (Decimal)tempInt);
            try
            {
                var value = Decimal.Parse(valueStr.Replace(',', '.'), Style, Format);
                return ToValueToken(tokenStart, index, value);
            }
            catch (Exception ex)
            {
                throw new ExpressionParserException("Expected a decimal number but found \"" + valueStr + "\"", t, tokenStart, index, ex);
            }
        }
    }

}

