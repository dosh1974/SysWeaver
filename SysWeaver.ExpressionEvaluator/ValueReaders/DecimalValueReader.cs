using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader
{
    public sealed class DecimalValueReader<T>
    {
        public DecimalValueReader(Func<int, int, Decimal, T> toValueCreator)
        {
            Format = CultureInfo.InvariantCulture;
            Style = NumberStyles.Float;
            ToValueToken = toValueCreator;
        }

        public DecimalValueReader(Func<int, int, Decimal, T> toValueCreator, IFormatProvider format, NumberStyles style)
        {
            Format = format;
            Style = style;
            ToValueToken = toValueCreator;
        }
        public readonly IFormatProvider Format;
        public readonly NumberStyles Style;
        public readonly Func<int, int, Decimal, T> ToValueToken;

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

