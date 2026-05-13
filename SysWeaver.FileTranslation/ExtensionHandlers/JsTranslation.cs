using System;
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
            var sb = new StringBuilder(l);
            for (int pos = 0; ;)
            {
                if (!Find(out var isHtml, out var quote, out var start, out var end, out var srcText, out var srcDesc, out var args, pos, text))
                {
                    sb.Append(text.Substring(pos));
                    break;
                }
                sb.Append(text.Substring(pos, start - pos));
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
            if (!ch)
                return false;
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
                    var ch = _source[_index++];
                    code = code * 16 +
                           "0123456789abcdef".IndexOf(ch.ToString(),
                                                      StringComparison.OrdinalIgnoreCase);
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
        {
            var _length = _source.Length;
            var str = new StringBuilder();

            var quote = _source[_index];

            var start = _index;
            ++_index;

            while (_index < _length)
            {
                var ch = _source[_index++];

                if (ch == quote)
                {
                    quote = char.MinValue;
                    break;
                }

                if (ch == '\\')
                {
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
                                    var code = "01234567".IndexOf(ch);

                                    // \0 is not octal escape sequence
                                    if (_index < _length && IsOctalDigit(_source[_index]))
                                    {
                                        code = code * 8 + "01234567".IndexOf(_source[_index++]);

                                        // 3 digits are only allowed when string starts
                                        // with 0, 1, 2, 3
                                        if ("0123".IndexOf(ch) >= 0 &&
                                            _index < _length &&
                                            IsOctalDigit(_source[_index]))
                                        {
                                            code = code * 8 + "01234567".IndexOf(_source[_index++]);
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
                        if (ch == '\r' && _source[_index] == '\n')
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
            return str.ToString();
        }


        static bool ParseLiteral(out String literal, String text, ref int i)
        {
            var l = text.Length;
            var q = text[i];
            ++i;
            var s = i;
            while (i < l)
            {
                if (text[i] == q)
                    if (text[i - 1] != '\\')
                    {
                        text = text.Substring(s - 1, i - s + 2);
                        literal = UnescapeJavascriptString(text);
                        return true;
                    }
                ++i;
            }
            literal = null;
            return false;
        }

        static bool ParseLiteralReverse(out String literal, String text, ref int i)
        {
            var q = text[i];
            --i;
            var e = i;
            while (i > 0)
            {
                if (text[i] == q)
                    if (text[i - 1] != '\\')
                    {
                        text = text.Substring(i, e - i + 2);
                        literal = UnescapeJavascriptString(text);
                        return true;
                    }
                --i;
            }
            literal = null;
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
                    if (!ParseLiteral(out var _, text, ref i))
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
                start = i;
                if (text[i] != '_')
                    continue;
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
                        if (!ParseLiteralReverse(out srcDesc, text, ref j))
                            throw new Exception("Invalid JS!?");
                        --j;
                        while ((j > 0) && Char.IsWhiteSpace(text[j]))
                            --j;
                        if (text[j] != ',')
                            throw new Exception("Invalid JS!? (2)");
                        --j;
                        while ((j > 0) && Char.IsWhiteSpace(text[j]))
                            --j;
                        endArg = j + 1;
                    }
                }
                args = endArg > startArg ? text.Substring(startArg, endArg - startArg).Trim() : null;
                if (String.IsNullOrEmpty(args))
                    args = null;
                return true;
            }
            return false;
        }



    }
}
