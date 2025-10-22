using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class StreamExt
    {
        /// <summary>
        /// Read all lines of text in a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns></returns>
        public static IEnumerable<String> ReadAllLines(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            for (; ;)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                yield return line;
            }
        }


        /// <summary>
        /// Read all text of a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <returns></returns>
        public static String ReadAllText(this Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Read all lines of text in a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="leaveOpen">True will leave the stream opened, false will close it</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <returns></returns>
        public static IEnumerable<String> ReadAllLines(this Stream stream, bool leaveOpen = false, Encoding encoding = null)
        {
            using var x = leaveOpen ? null : stream;
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                yield return line;
            }
        }


        public static async ValueTask<Memory<Byte>> ReadAllMemoryAsync(this Stream stream, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return new Memory<Byte>(ms.GetBuffer(), 0, (int)ms.Position);
        }

        public static async ValueTask<Byte[]> ReadAllBytesAsync(this Stream stream, bool leaveOpen = false)
        {
            using var x = leaveOpen ? null : stream;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }



        /// <summary>
        /// Check if a word is found in some text, the glyph before a word may no be a letter, the glyph after a word may not be a letter.
        /// </summary>
        /// <param name="sentence"></param>
        /// <param name="word"></param>
        /// <param name="cmp"></param>
        /// <returns></returns>
        public static bool ContainsWord(this String sentence, String word, StringComparison cmp = StringComparison.OrdinalIgnoreCase)
        {
            var sl = sentence.Length;
            var wl = word.Length;
            int s = 0;
            for (; ;)
            {
                s = sentence.IndexOf(word, s, cmp);
                if (s < 0)
                    return false;
                var o = s - 1;
                s += wl;
                if (o >= 0)
                    if (Char.IsLetter(sentence[o]))
                        continue;
                if (s < sl)
                    if (Char.IsLetter(sentence[s]))
                        continue;
                return true;
            }
        }

        /// <summary>
        /// Remove all diacritics from a string (replaces them with base values)
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemoveDiacritics(this string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Remove one set of quotes from a a string (if they exist).
        /// Ex: "apa" => apa
        /// 'banana' => banana
        /// ""monkey"" => "monkey"
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemoveQuotes(this string text)
        {
            if (text == null)
                return text;
            var sp = text.AsSpan();
            var tl = sp.Length;
            if (tl < 2)
                return text;
            var c = sp[0];
            if (c != '"')
                if (c != '\'')
                    return text;
            if (sp[tl - 1] != c)
                return text;
            return new string(sp.Slice(1, tl - 2));
        }


        /// <summary>
        /// Count the number of occurances of a substring
        /// </summary>
        /// <param name="text"></param>
        /// <param name="subString"></param>
        /// <param name="com"></param>
        /// <returns></returns>
        public static int Count(this String text, String subString, StringComparison com = StringComparison.CurrentCulture)
        {
            int c = 0;
            var l = subString.Length;
            for (int p = 0; ; )
            {
                p = text.IndexOf(subString, p, com);
                if (p < 0)
                    return c;
                ++c;
                p += l;
            }
        }


        /// <summary>
        /// Remove all occurances of some chars from a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="removeChars">The chars to remove</param>
        /// <returns></returns>
        public static String RemoveChars(this String text, params Char[] removeChars)
        {
            if (removeChars == null)
                return text;
            if (removeChars.Length <= 0)
                return text;
            var remove = new HashSet<Char>(removeChars);
            return InternalRemoveChars(text, remove);
        }

        /// <summary>
        /// Remove all occurances of some chars from a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="removeChars">The chars to remove</param>
        /// <returns></returns>
        public static String RemoveChars(this String text, String removeChars)
        {
            if (removeChars == null)
                return text;
            if (removeChars.Length <= 0)
                return text;
            var remove = new HashSet<Char>(removeChars);
            return InternalRemoveChars(text, remove);
        }

        /// <summary>
        /// Remove all occurances of some chars from a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="removeChars">The chars to remove</param>
        /// <returns></returns>
        public static String RemoveChars(this String text, IReadOnlySet<Char> removeChars)
        {
            if (removeChars == null)
                return text;
            if (removeChars.Count <= 0)
                return text;
            return InternalRemoveChars(text, removeChars);
        }

        static String InternalRemoveChars(String text, IReadOnlySet<Char> remove)
        {
            if (text == null)
                return text;
            var l = text.Length;
            if (l <= 0)
                return text;
            Char[] o = null;
            int d = 0;
            for (int i = 0; i < l; ++i)
            {
                var c = text[i];
                bool haveO = o != null;
                if (remove.Contains(c))
                {
                    if (haveO)
                        continue;
                    o = text.ToCharArray();
                    d = i;
                    continue;
                }
                if (haveO)
                {
                    o[d] = c;
                    ++d;
                }
            }
            return o == null ? text : new String(o, 0, d);
        }


    }

}
