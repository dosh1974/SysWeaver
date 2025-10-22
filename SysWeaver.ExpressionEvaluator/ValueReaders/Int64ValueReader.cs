using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace SysWeaver.Parser.ValueReader
{
    public sealed class Int64ValueReader<T>
    {
        public Int64ValueReader(Func<int, int, Int64, T> toValueCreator)
        {
            Format = CultureInfo.InvariantCulture;
            Style = NumberStyles.Integer;
            ToValueToken = toValueCreator;
        }

        public Int64ValueReader(Func<int, int, Int64, T> toValueCreator, IFormatProvider format, NumberStyles style)
        {
            Format = format;
            Style = style;
            ToValueToken = toValueCreator;
        }
        public readonly IFormatProvider Format;
        public readonly NumberStyles Style;
        public readonly Func<int, int, Int64, T> ToValueToken;

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
                return ToValueToken(tokenStart, index, (Int64)tempInt);
            try
            {
                var value = Int64.Parse(valueStr, Style, Format);
                return ToValueToken(tokenStart, index, value);
            }
            catch (Exception ex)
            {
                throw new ExpressionParserException("Expected a integer number but found \"" + valueStr + "\"", t, tokenStart, index, ex);
            }
        }
    }

}
