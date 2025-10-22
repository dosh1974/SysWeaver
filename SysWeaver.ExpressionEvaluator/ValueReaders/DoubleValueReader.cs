using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader

{
    public sealed class DoubleValueReader<T>
    {
        public DoubleValueReader(Func<int, int, Double, T> toValueCreator)
        {
            Format = CultureInfo.InvariantCulture;
            Style = NumberStyles.Float;
            ToValueToken = toValueCreator;
        }

        public DoubleValueReader(Func<int, int, Double, T> toValueCreator, IFormatProvider format, NumberStyles style)
        {
            Format = format;
            Style = style;
            ToValueToken = toValueCreator;
        }
        public readonly IFormatProvider Format;
        public readonly NumberStyles Style;
        public readonly Func<int, int, Double, T> ToValueToken;

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
            if (ValueReader.TryParse(out tempInt, valueStr, t, tokenStart, index))
                return ToValueToken(tokenStart, index, (Double)tempInt);
            try
            {
                var value = Double.Parse(valueStr.Replace(',', '.'), Style, Format);
                return ToValueToken(tokenStart, index, value);
            }
            catch (Exception ex)
            {
                throw new ExpressionParserException("Expected a decimal number but found \"" + valueStr + "\"", t, tokenStart, index, ex);
            }
        }
    }

}
