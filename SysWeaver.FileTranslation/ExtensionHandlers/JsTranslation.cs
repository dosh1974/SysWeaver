using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SysWeaver.MicroService.ExtensionHandlers
{
    public sealed class JsTranslation
    {


        public static LanguageTemplate CreateTemplate(String text, bool willTranslate, bool allowBrowserTranslation)
        {
            var vars = TranslationTools.GetVars();
            Process(vars, ref text);
            return new LanguageTemplate(text, vars.Values);
        }



        public static bool Process(Dictionary<String, LanguageTemplateVar> vars, ref String text)
        {
            bool ch = false;
            var l = text.Length;
            //  Created on the first match (no copy of a text without any _T(..))
            StringBuilder sb = null;
            for (int pos = 0; ;)
            {
                if (!Find(out var isHtml, out var quote, out var start, out var end, out var srcText, out var srcDesc, out var args, pos, text))
                {
                    if (!ch)
                        return false;
                    sb.Append(text, pos, l - pos);
                    break;
                }
                sb ??= new StringBuilder(l);
                sb.Append(text, pos, start - pos);
                if (!srcText.AnyLetter())
                {
                    if (args == null)
                    {
                        sb.Append(HttpUtility.JavaScriptStringEncode(srcText, true));
                    }
                    else
                    {
                        sb.Append("_T(").Append(HttpUtility.JavaScriptStringEncode(srcText, true)).Append(args).Append(')');
                    }
                    pos = end;
                    continue;
                }
                ch = true;
                var varName = TranslationTools.TryAddVar(vars, srcText, srcDesc);
                char fmt = isHtml ? '¤' : '£';
                if (args == null)
                {
                    sb.Append("${").Append(fmt).Append(varName).Append('}');
                }
                else
                {
                    sb.Append("_T(${").Append(fmt).Append(varName).Append("}").Append(args).Append(')');
                }
                pos = end;
            }
            text = sb.ToString();
            return true;
        }



        static bool IsLineTerminator(char ch)
        {
            return (ch == 10)
                || (ch == 13)
                || (ch == 0x2028) // line separator
                || (ch == 0x2029) // paragraph separator
                ;
        }

        static bool IsHexDigit(char ch)
        {
            return
                ch >= '0' && ch <= '9' ||
                ch >= 'a' && ch <= 'f' ||
                ch >= 'A' && ch <= 'F'
                ;
        }

        static bool IsOctalDigit(char ch)
        {
            return ch >= '0' && ch <= '7';
        }

        static bool ScanHexEscape(int _index, int _length, String _source, char prefix, out char result)
        {
            int code = char.MinValue;

            var len = (prefix == 'u') ? 4 : 2;
            for (var i = 0; i < len; ++i)
            {
                if (_index < _length && IsHexDigit(_source[_index]))
                {
                    int ch = _source[_index++];
                    //  ch is a hex digit: '0'..'9', 'a'..'f' or 'A'..'F' ('A' | 0x20 = 'a')
                    code = code * 16 + (ch <= '9' ? ch - '0' : (ch | 0x20) - ('a' - 10));
                }
                else
                {
                    result = char.MinValue;
                    return false;
                }
            }

            result = (char)code;
            return true;
        }

        public static String UnescapeJavascriptString(String _source, int _index = 0)
            => Unescape(_source, _index, _source.Length);

        //  The characters that end a run of plain characters (copied as is) in a " or ' quoted literal
        static readonly SearchValues<char> DoubleQuoteStops = SearchValues.Create("\"\\\n\r\u2028\u2029");
        static readonly SearchValues<char> SingleQuoteStops = SearchValues.Create("'\\\n\r\u2028\u2029");

        /// <summary>
        /// UnescapeJavascriptString on the text up to _length (exclusive), the same as calling it on _source.Substring(0, _length)
        /// </summary>
        static String Unescape(String _source, int _index, int _length)
        {
            var quote = _source[_index];
            ++_index;
            var stops = quote == '"' ? DoubleQuoteStops : (quote == '\'' ? SingleQuoteStops : null);
            //  Created on the first escape (a literal without escapes is a substring)
            StringBuilder str = null;
            while (_index < _length)
            {
                if (stops != null)
                {
                    var run = _source.AsSpan(_index, _length - _index).IndexOfAny(stops);
                    if (run < 0)
                        break; // No end quote
                    if (run > 0)
                    {
                        if ((str == null) && (_source[_index + run] == quote))
                            return _source.Substring(_index, run);
                        (str ??= new StringBuilder(run + 16)).Append(_source, _index, run);
                        _index += run;
                    }
                }
                var ch = _source[_index++];

                if (ch == quote)
                {
                    quote = char.MinValue;
                    break;
                }
                str ??= new StringBuilder();

                if (ch == '\\')
                {
                    if (_index >= _length)
                        break; // No end quote
                    ch = _source[_index++];
                    if (ch == char.MinValue || !IsLineTerminator(ch))
                    {
                        switch (ch)
                        {
                            case 'n':
                                str.Append('\n');
                                break;
                            case 'r':
                                str.Append('\r');
                                break;
                            case 't':
                                str.Append('\t');
                                break;
                            case 'u':
                            case 'x':
                                var restore = _index;
                                char unescaped;
                                if (ScanHexEscape(_index, _length, _source, ch, out unescaped))
                                {
                                    str.Append(unescaped);
                                }
                                else
                                {
                                    _index = restore;
                                    str.Append(ch);
                                }
                                break;
                            case 'b':
                                str.Append("\b");
                                break;
                            case 'f':
                                str.Append("\f");
                                break;
                            case 'v':
                                str.Append("\x0B");
                                break;

                            default:
                                if (IsOctalDigit(ch))
                                {
                                    var code = ch - '0';

                                    // \0 is not octal escape sequence
                                    if (_index < _length && IsOctalDigit(_source[_index]))
                                    {
                                        code = code * 8 + (_source[_index++] - '0');

                                        // 3 digits are only allowed when string starts
                                        // with 0, 1, 2, 3
                                        if (ch <= '3' &&
                                            _index < _length &&
                                            IsOctalDigit(_source[_index]))
                                        {
                                            code = code * 8 + (_source[_index++] - '0');
                                        }
                                    }
                                    str.Append((char)code);
                                }
                                else
                                {
                                    str.Append(ch);
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (ch == '\r' && _index < _length && _source[_index] == '\n')
                        {
                            ++_index;
                        }
                    }
                }
                else if (IsLineTerminator(ch))
                {
                    break;
                }
                else
                {
                    str.Append(ch);
                }
            }

            if (quote != 0)
                throw new Exception("No end quote!");
            return str?.ToString() ?? String.Empty;
        }


        static bool ParseLiteral(out String literal, String text, ref int i)
        {
            var l = text.Length;
            var q = text[i];
            ++i;
            var s = i;
            while (i < l)
            {
                var c = text[i];
                //  Skip escaped chars (a quote after an escaped backslash ends the literal)
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }
                if (c == q)
                {
                    literal = Unescape(text, s - 1, i + 1);
                    return true;
                }
                ++i;
            }
            literal = null;
            return false;
        }

        /// <summary>
        /// Find the start of a literal that ends at i (i is set to the start quote), the start must be at or after min
        /// </summary>
        static bool FindLiteralStartReverse(String text, ref int i, int min)
        {
            var q = text[i];
            --i;
            while (i >= min)
            {
                if (text[i] == q)
                {
                    //  The quote is escaped if it's preceded by an odd number of backslashes
                    var b = i;
                    while ((b > 0) && (text[b - 1] == '\\'))
                        --b;
                    if (((i - b) & 1) == 0)
                        return true;
                }
                --i;
            }
            return false;
        }


        /// <summary>
        /// The same as ParseLiteral, without unescaping the literal: the same end (i) and result, and the same exception as Unescape.
        /// Unescape throws "No end quote!" if the literal contains a line terminator that isn't escaped
        /// (an escaped "\r\n" is one line continuation, the other escapes only consume digits after the escaped char, so the escapes are the same as the ones skipped here).
        /// </summary>
        static bool SkipLiteral(String text, ref int i)
        {
            var l = text.Length;
            var q = text[i];
            ++i;
            bool lineBreak = false;
            while (i < l)
            {
                var c = text[i];
                if (c == '\\')
                {
                    //  "\\\r\n": the '\n' isn't a quote and Unescape consumes it with the escape
                    if (((i + 2) < l) && (text[i + 1] == '\r') && (text[i + 2] == '\n'))
                        ++i;
                    i += 2;
                    continue;
                }
                if (c == q)
                {
                    if (lineBreak)
                        throw new Exception("No end quote!");
                    return true;
                }
                if (IsLineTerminator(c))
                    lineBreak = true;
                ++i;
            }
            return false;
        }

        static bool FindEnd(String text, ref int i, int count = 1)
        {
            var l = text.Length;
            while (i < l)
            {
                var c = text[i];
                if (c == ')')
                {
                    --count;
                    if (count <= 0)
                        return true;
                }
                if (c == '(')
                {
                    ++count;
                    ++i;
                    continue;
                }
                if ((c == '"') || (c == '\''))
                {
                    if (!SkipLiteral(text, ref i))
                        return false;
                    ++i;
                    continue;
                }
                ++i;
            }
            return false;
        }

        static bool Find(out bool isHtml, out char quote, out int start, out int end, out string srcText, out string srcDesc, out String args, int pos, String text)
        {
            start = 0;
            end = 0;
            isHtml = false;
            quote = (Char)0;
            srcText = null;
            srcDesc = null;
            args = null;
            var l = text.Length;
            var e = l - 7; // Min length = 7
            for (int i = pos; i < e; ++i)
            {
                //  Skip to the next '_'
                var k = text.IndexOf('_', i, e - i);
                if (k < 0)
                    return false;
                i = k;
                start = i;
                ++i;
                if (text[i] != 'T')
                    continue;
                ++i;
                bool isFixed = text[i] == 'F';
                if (isFixed)
                    ++i;
                isHtml = text[i] == 'H';
                if (isHtml)
                    ++i;
                while ((i < l) && Char.IsWhiteSpace(text[i]))
                    ++i;
                if (i >= l)
                    return false;
                if (text[i] != '(')
                    continue;
                ++i;
                if (i >= l)
                    return false;
                while ((i < l) && Char.IsWhiteSpace(text[i]))
                    ++i;
                if (i >= l)
                    return false;
                quote = text[i];
                if (quote != '"')
                    if (quote != '\'')
                        continue;
                if (!ParseLiteral(out srcText, text, ref i))
                    return false;
                ++i;
                int startArg = i;
                if (!FindEnd(text, ref i))
                    return false;
                end = i + 1;
                int j = i - 1;
                while ((j > 0) && Char.IsWhiteSpace(text[j]))
                    --j;
                var endArg = j + 1;
                var c = text[j];
                if ((c == '"') || (c == '\''))
                {
                    if (j > startArg)
                    {
                        var literalEnd = j;
                        if (!FindLiteralStartReverse(text, ref j, startArg))
                            throw new Exception("Invalid JS!?");
                        var literalStart = j;
                        --j;
                        while ((j > 0) && Char.IsWhiteSpace(text[j]))
                            --j;
                        //  Only a literal after a ',' is the description, else it's a part of the last argument (ex: x + "s")
                        if (text[j] == ',')
                        {
                            srcDesc = Unescape(text, literalStart, literalEnd + 1);
                            --j;
                            while ((j > 0) && Char.IsWhiteSpace(text[j]))
                                --j;
                            endArg = j + 1;
                        }
                    }
                }
                if (endArg > startArg)
                {
                    var a = text.AsSpan(startArg, endArg - startArg).Trim();
                    if (a.Length > 0)
                        args = a.ToString();
                }
                return true;
            }
            return false;
        }



    }
}
