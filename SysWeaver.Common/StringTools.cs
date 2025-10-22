using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SysWeaver
{
    public static class StringTools
    {
        /// <summary>
        /// Compute a deterministic hash of the string contents
        /// </summary>
        /// <param name="s">The string to compute a hash for</param>
        /// <returns>A hash based on the string content</returns>
        public static int GetHashCode(String s)
        {
            int value = 1123456793;
            foreach (var c in s)
            {
                value *= 16411;
                value += (int)c;
            }
            return value;
        }



        /// <summary>
        /// Add quotation chars around a string. Ex: Test => "Test"
        /// </summary>
        /// <param name="s">The string to add quotation chars around</param>
        /// <param name="quotationChar">The quotation char to use</param>
        /// <returns>A quoted string</returns>
        public static String ToQuoted(this String s, Char quotationChar = '"') => s == null ? "null" : String.Join(s, quotationChar, quotationChar);





        /// <summary>
        /// Add quotation chars around a string. Ex: Test => "Test"
        /// </summary>
        /// <param name="s">The string to add quotation chars around</param>
        /// <param name="quotationChars">The quotation chars to use</param>
        /// <returns>A quoted string</returns>
        public static String ToQuoted(this String s, String quotationChars) => s == null ? "null" : String.Join(s, quotationChars, quotationChars);

        /// <summary>
        /// Format a string as a filename, typically add quotes
        /// </summary>
        /// <param name="s">The string to format as a filename</param>
        /// <returns>A filename formatted string</returns>
        public static String ToFilename(this String s) => s == null ? "null" : String.Join(s, "\"file://", '"');

        /// <summary>
        /// Format a string as a filename, typically add quotes
        /// </summary>
        /// <param name="s">The string to format as a filename</param>
        /// <returns>A filename formatted string</returns>
        public static String ToMail(this String s) => s == null ? "null" : String.Join(s, "\"mailto://", '"');


        /// <summary>
        /// Format a string as a folder name, typically add quotes
        /// </summary>
        /// <param name="s">The string to format as a folder name</param>
        /// <returns>A folder name formatted string</returns>
        public static String ToFolder(this String s) => s == null ? "null" : String.Join(Path.TrimEndingDirectorySeparator(s), "file://\"", Path.DirectorySeparatorChar + "\"");


        /// <summary>
        /// "Counts up" a string, ex "apa_1.png" => "apa_2.png", "apa9.txt" => "apa10.txt", "apa_1_99" => "apa_1_100", "apa" => "apa_1"
        /// </summary>
        /// <param name="str">The string to "count up"</param>
        /// <returns>A string that has been "incremented"</returns>
        public static String CountUp(this String str)
        {
            var l = str.Length;
            String insert = String.Empty;
            while (l > 0)
            {
                --l;
                var c = str[l];
                if ((c < '0') || (c > '9'))
                {
                    if (insert.Length > 0)
                        return String.Concat(str.AsSpan(0, l), insert, str.AsSpan(l + insert.Length));
                    continue;
                }
                insert = (char)((((c - '0') + 1) % 10) + '0') + insert;
                if (c != '9')
                    return String.Concat(str.AsSpan(0, l), insert, str.AsSpan(l + insert.Length));
            }
            return str;
        }

        static void CreateFirstUpper(Span<Char> str, String c)
        {
            str[0] = Char.ToUpper(c[0]);
            c.AsSpan().Slice(1).CopyTo(str.Slice(1));
        }

        static readonly SpanAction<Char, String> CreateFirstUpperAction = CreateFirstUpper;

        static void CreateFirstLower(Span<Char> str, String c)
        {
            str[0] = Char.ToLower(c[0]);
            c.AsSpan().Slice(1).CopyTo(str.Slice(1));
        }

        static readonly SpanAction<Char, String> CreateFirstLowerAction = CreateFirstLower;



        /// <summary>
        /// Make sure that the first character is an uppercase letter (if it's a letter).
        /// Examples:
        /// "hello" becomes "Hello".
        /// "World" remains "World".
        /// "123" remains "123".
        /// </summary>
        /// <param name="str">The text to make the first letter uppercased</param>
        /// <returns>The original string or a new string with the first letter uppercased</returns>
        public static String MakeFirstUppercase(this String str)
        {
            if (String.IsNullOrEmpty(str))
                return str;
            var first = str[0];
            if (!Char.IsLower(str[0]))
                return str;
            return String.Create(str.Length, str, CreateFirstUpperAction);
        }

        /// <summary>
        /// Make sure that the first character is a lowercase letter (if it's a letter).
        /// Examples:
        /// "Hello" becomes "hello".
        /// "world" remains "world".
        /// "123" remains "123".
        /// </summary>
        /// <param name="str">The text to make the first letter lowercased</param>
        /// <returns>The original string or a new string with the first letter lowercased</returns>
        public static String MakeFirstLowercase(this String str)
        {
            if (String.IsNullOrEmpty(str))
                return str;
            var first = str[0];
            if (!Char.IsUpper(str[0]))
                return str;
            return String.Create(str.Length, str, CreateFirstLowerAction);
        }

        /// <summary>
        /// Take a camel cased string and convert it to a space separated string. 
        /// Ex:
        /// "MyNameIsStupid" => "My name is stupid"
        /// </summary>
        /// <param name="str">The camel cased string. Ex: "MyNameIsStupid"</param>
        /// <param name="space">The character to use for space</param>
        /// <param name="keepFirstWordLetterCasing">If true, keep the casing of the first letter in each word</param>
        /// <returns>The space separated string. Ex: "My name is stupid"</returns>
        public static String RemoveCamelCase(this String str, Char space = ' ', bool keepFirstWordLetterCasing = false)
        {
            var sb = new StringBuilder(str.Length * 2);
            bool prevIsUpper = true;
            foreach (var c in str)
            {
                var isUpper = Char.IsUpper(c);
                if (isUpper && (!prevIsUpper))
                {
                    sb.Append(space);
                    sb.Append(keepFirstWordLetterCasing ? c : CharExt.FastToLower(c));
                    prevIsUpper = true;
                    continue;
                }
                sb.Append(c);
                prevIsUpper = isUpper;
            }
            return sb.ToString();
        }


        /// <summary>
        /// Levenstein distance
        /// </summary>
        /// <param name="source1">First string</param>
        /// <param name="source2">Second string</param>
        /// <param name="costLetter">Mismatched letter cost</param>
        /// <param name="costNumber">Mismatched number cost</param>
        /// <returns>The Levenstein distance between the two strings</returns>
        public static int Levenstein(string source1, string source2, int costLetter = 1, int costNumber = 1)
        {
            var source1Length = source1?.Length ?? 0;
            var source2Length = source2?.Length ?? 0;

            var matrix = new int[source1Length + 1, source2Length + 1];

            int Cost(char a, char b)
            {
                int aa;
                if (Char.IsNumber(a))
                    aa = costNumber;
                else
                    aa = Char.IsLetter(a) ? costLetter : 1;
                int bb;
                if (Char.IsNumber(b))
                    bb = costNumber;
                else
                    bb = Char.IsLetter(b) ? costLetter : 1;
                return aa > bb ? aa : bb;
            }


            // First calculation, if one entry is empty return full length
            if (source1Length == 0)
                return source2Length;

            if (source2Length == 0)
                return source1Length;

            // Initialization of matrix with row size source1Length and columns size source2Length
            for (var i = 0; i <= source1Length; matrix[i, 0] = i++) { }
            for (var j = 0; j <= source2Length; matrix[0, j] = j++) { }

            // Calculate rows and collumns distances
            for (var i = 1; i <= source1Length; i++)
            {
                for (var j = 1; j <= source2Length; j++)
                {
                    var a = source2[j - 1];
                    var b = source1[i - 1];
                    var cost = a == b ? 0 : Cost(a, b);
                    matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + cost);
                }
            }
            return matrix[source1Length, source2Length];
        }


        /// <summary>
        /// Extract all words from some text
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static String[] ExtractWords(this String text)
        {
            List<String> textWords = new List<string>();
            text.OnWordStart(pos =>
            {
                var e = text.EndOfWord(pos + 1);
                if (e < 0)
                    e = text.Length;
                textWords.Add(text.Substring(pos, e - pos));
                return true;
            });
            return textWords.ToArray();
        }

        /// <summary>
        /// Levenstein distance, by considering each possible word pair of the text and match, the sum of best matches is returned
        /// </summary>
        /// <param name="text">First string</param>
        /// <param name="matchWith">Second string</param>
        /// <returns>The sum of the best levenstein distance between each word pair</returns>
        public static int FuzzyMatch(string text, string matchWith)
            => FuzzyMatch(ExtractWords(text), ExtractWords(matchWith));


        public const int FuzzyLevensteinLetterCost = 3;
        public const int FuzzyLevensteinNumberCost = 4;
        public const int FuzzyLevensteinShiftWeight = 4;


        const int FuzzyPartOfShiftWeight = 1;
        const int FuzzyOrderShiftWeight = 0;
        const int FuzzyMissingWordsShiftWeight = 0;

        const int FuzzyMaxShiftWeight = FuzzyLevensteinShiftWeight > FuzzyPartOfShiftWeight
                                        ?
                                        (FuzzyLevensteinShiftWeight > FuzzyOrderShiftWeight ? FuzzyLevensteinShiftWeight : FuzzyOrderShiftWeight)
                                        :
                                        (FuzzyPartOfShiftWeight > FuzzyOrderShiftWeight ? FuzzyPartOfShiftWeight : FuzzyOrderShiftWeight);

        public static int FuzzyMaxErr(int searchLength, int maxError = 2) => Math.Max(1, (((searchLength + maxError - 1) * FuzzyLevensteinNumberCost) << FuzzyMaxShiftWeight) / maxError);

        public static int FuzzyMatch(string[] textWords, string[] matchWords)
        {
            textWords = textWords.ToArray();
            var wlen = textWords.Length;
            var mlen = matchWords.Length;
            if ((wlen <= 0) || (mlen <= 0))
                return int.MaxValue;
            int levSum = 0;
            for (int x = 0; x < mlen; ++x)
            {
                var a = matchWords[x];
                int levBest = int.MaxValue;
                int ibest = 0;
                for (int i = 0; i < wlen; ++i)
                {
                    var t = textWords[i];
                    var found = t.IndexOf(a, StringComparison.Ordinal);
                    var l = found < 0
                        ?
                        (Levenstein(t, a, FuzzyLevensteinLetterCost, FuzzyLevensteinNumberCost) << FuzzyLevensteinShiftWeight)
                        :
                        ((t.Length - a.Length) << FuzzyPartOfShiftWeight);
                    if (l < levBest)
                    {
                        levBest = l;
                        ibest = i;
                    }
                }
                levSum += levBest;
                --wlen;
                if (wlen == 0)
                {
                    levSum += ((mlen - x - 1) << FuzzyOrderShiftWeight);
                    break;
                }
                var o = textWords[wlen];
                textWords[wlen] = textWords[ibest];
                textWords[ibest] = o;
            }
            levSum += (wlen << FuzzyMissingWordsShiftWeight);
            return levSum;
        }

        /// <summary>
        /// Inspect each char and find the first match
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <param name="isMatch">Predicate that inspects a char, return true to return this position</param>
        /// <param name="startIndex">Start position</param>
        /// <returns>Position of the first match, or -1 if none is found</returns>
        public static int IndexOf(this string text, Func<Char, bool> isMatch, int startIndex = 0)
        {
            if (text == null)
                return -1;
            var l = text.Length;
            while (startIndex < l)
            {
                if (isMatch(text[startIndex]))
                    return startIndex;
                ++startIndex;
            }
            return -1;
        }


        /// <summary>
        /// Find the end of a word (first non letter or non digit)
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <param name="startIndex">Start position</param>
        /// <returns>Position of the first match, or -1 if none is found</returns>
        public static int EndOfWord(this string text, int startIndex)
        {
            if (text == null)
                return -1;
            var l = text.Length;
            while (startIndex < l)
            {
                if (!Char.IsLetterOrDigit(text[startIndex]))
                    return startIndex;
                ++startIndex;
            }
            return -1;
        }


        /// <summary>
        /// Limit (clamps) a string to be within a max length
        /// </summary>
        /// <param name="s">The string to limit</param>
        /// <param name="maxLen">The maximum allowed length of the output string</param>
        /// <param name="elipses">If the string is cut short, end it with this string (only if max len is twice as long as this string)</param>
        /// <returns>A string that have at most max length chars</returns>
        public static String LimitLength(this String s, int maxLen, String elipses = "...")
        {
            if (String.IsNullOrEmpty(s))
                return s;
            var l = s.Length;
            if (l <= maxLen)
                return s;
            var el = elipses?.Length ?? 0;
            if ((el + el) < maxLen)
                return s.Substring(0, maxLen - el) + elipses;
            return s.Substring(0, maxLen);
        }


        /// <summary>
        /// Find all word starts and execute a function on them
        /// </summary>
        /// <param name="text">The text to find word starts in</param>
        /// <param name="onNewWordStart">A function that is executed for every found word start, the paramater is the start index, return false to abort further processing</param>
        /// <param name="start">The optional first position in the string to search</param>
        public static void OnWordStart(this String text, Func<int, bool> onNewWordStart, int start = 0)
        {
            bool prevIsLetter = false;
            if (start > 0)
                prevIsLetter = Char.IsLetterOrDigit(text[start - 1]);
            var l = text.Length;
            for (int i = start; i < l; ++i)
            {
                var c = text[i];
                var isP = Char.IsLetterOrDigit(c);
                if (!prevIsLetter)
                {
                    if (isP)
                    {
                        if (!onNewWordStart(i))
                            return;
                    }
                }
                prevIsLetter = isP;
            }
        }



        /// <summary>
        /// Clean up strings, removing duplicate white-spaces, turning all white spaces to ' ' (tab's etc).
        /// </summary>
        /// <param name="s"></param>
        /// <param name="charMap">An optional char remapper</param>
        /// <returns>A sanitized string</returns>
        public static String Sanitize(this String s, IReadOnlyDictionary<Char, String> charMap = null)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            s = s.Trim();
            if (String.IsNullOrEmpty(s))
                return s;
            if (charMap != null)
            {
                StringBuilder b = new StringBuilder(s.Length);
                foreach (var c in s)
                {
                    if (!charMap.TryGetValue(c, out var cc))
                    {
                        b.Append(c);
                        continue;
                    }
                    b.Append(cc);
                }
                s = b.ToString();
                if (String.IsNullOrEmpty(s))
                    return s;
            }
            var l = s.Length;
            var t = new StringBuilder(l);
            bool isWhite = true;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (Char.IsWhiteSpace(c) || (c < 31))
                {
                    if (isWhite)
                        continue;
                    c = ' ';
                    isWhite = true;
                }
                else
                {
                    isWhite = false;
                }
                t.Append(c);
            }
            s = t.ToString();
            return s;
        }




        public static bool IsCodeIdentifierChar(char c)
        {
            if (Char.IsLetterOrDigit(c))
                return true;
            if (c == '_')
                return true;
            if (c == '@')
                return true;
            return false;
        }

        /// <summary>
        /// Clean up code strings, removing duplicate white-spaces, turning all white spaces to ' ' (tab's etc).
        /// Removing redunant spaces.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="isCodeIdentifier">An optional function that returns true if a char is a possible identifier</param>
        /// <param name="charMap">An optional char remapper</param>
        /// <returns>A sanitized string</returns>
        public static String CodeSanitize(this String s, Func<Char, bool> isCodeIdentifier = null, IReadOnlyDictionary<Char, String> charMap = null)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            s = s.Trim();
            if (String.IsNullOrEmpty(s))
                return s;
            if (isCodeIdentifier == null)
                isCodeIdentifier = IsCodeIdentifierChar;
            if (charMap != null)
            {
                StringBuilder b = new StringBuilder(s.Length);
                foreach (var c in s)
                {
                    if (!charMap.TryGetValue(c, out var cc))
                    {
                        b.Append(c);
                        continue;
                    }
                    b.Append(cc);
                }
                s = b.ToString();
                if (String.IsNullOrEmpty(s))
                    return s;
            }
            var l = s.Length;
            var t = new StringBuilder(l);
            bool isWhite = true;
            bool isIdentifier = false;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (Char.IsWhiteSpace(c) || (c < 31))
                {
                    if (isWhite)
                        continue;
                    if (!isIdentifier)
                        continue;
                    var n = i + 1;
                    if (n < l)
                    {
                        if (!isCodeIdentifier(s[n]))
                            continue;
                    }
                    c = ' ';
                    isWhite = true;
                    isIdentifier = false;
                }
                else
                {
                    isWhite = false;
                    isIdentifier = isCodeIdentifier(c);
                }
                t.Append(c);
            }
            s = t.ToString();
            return s;
        }



        /// <summary>
        /// Check if a string contains anything but ascii (7-bit).
        /// If any char in the string have a value greater or equal to 128 this method returns false.
        /// </summary>
        /// <param name="s">The string to check</param>
        /// <returns>True if all chars in the string is less than 128</returns>
        public static bool IsAsciiOnly(this String s)
        {
            if (s == null)
                return true;
            var l = s.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (c >= 128)
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Check if a string is a valid "identifier", only 'a'-'z' and numbers is accepeted (no number at the first position)
        /// </summary>
        /// <param name="s">The string to check</param>
        /// <returns>True if all chars in the string is valid</returns>
        public static bool IsIdentifier(this String s)
        {
            if (s == null)
                return false;
            var l = s.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if ((c >= 'a') && (c <= 'z'))
                    continue;
                if ((c >= 'A') && (c <= 'Z'))
                    continue;
                if (i == 0)
                    return false;
                if ((c < '0') || (c > '9'))
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Check if a string is numeric (only contains '0' to '9').
        /// </summary>
        /// <param name="s">The string to check</param>
        /// <param name="allowSpace">true to allow spaces (should filter them out before converting to a number)</param>
        /// <param name="allowNeg">true to allow a single '-' at the start</param>
        /// <param name="allowDecimal">true to allow a '.'</param>
        /// <returns>True if all chars in the string is a number</returns>
        public static bool IsNumeric(this String s, bool allowSpace = true, bool allowNeg = false, bool allowDecimal = false)
        {
            if (s == null)
                return false;
            var l = s.Length;
            if (l <= 0)
                return false;
            bool haveDecimal = false;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (c < '0')
                {
                    if (allowNeg && (i == 0) && (c == '-'))
                        continue;
                    if (allowSpace && (c == ' '))
                        continue;
                    if (allowDecimal && (c == '.') && (!haveDecimal))
                    {
                        haveDecimal = true;
                        continue;
                    }
                    return false;
                }
                if (c > '9')
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Check if a string is made up of only hexadecimal digits (only contains '0' to '9', 'a' to 'f' or 'A' to 'F').
        /// </summary>
        /// <param name="s">The string to check</param>
        /// <param name="allowSpace">true to allow spaces (should filter them out before converting to a number)</param>
        /// <returns>True if all chars in the string is hexadecimal digits</returns>
        public static bool IsHex(this String s, bool allowSpace = true)
        {
            if (s == null)
                return false;
            var l = s.Length;
            if (l <= 0)
                return false;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (c < '0')
                {
                    if (allowSpace && (c == ' '))
                        continue;
                    return false;
                }
                if (c <= '9')
                    continue;
                if ((c >= 'a') && (c <= 'f'))
                    continue;
                if ((c >= 'A') && (c <= 'F'))
                    continue;
                return false;
            }
            return true;
        }


        /// <summary>
        /// Check if a string is letters only.
        /// </summary>
        /// <param name="s">The string to check</param>
        /// <param name="allowSpace">true to allow spaces</param>
        /// <returns>True if all chars in the string is a letter (or space if allowed)</returns>
        public static bool IsLetters(this String s, bool allowSpace = true)
        {
            if (s == null)
                return false;
            var l = s.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (Char.IsLetter(c))
                    continue;
                if (allowSpace && (c == ' '))
                    continue;
                return false;
            }
            return true;
        }


        /// <summary>
        /// Removes duplicate white spaces, with a single white space (and trims white spaces from start and end).
        /// </summary>
        /// <param name="s">The string</param>
        /// <param name="useAsWhiteSpace">Replace white spaces with a single of this</param>
        /// <returns></returns>
        public static String RemoveMultiWhiteSpace(String s, Char useAsWhiteSpace = ' ')
        {
            var l = s.Length;
            StringBuilder b = new StringBuilder(l);
            bool wasSpace = true;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                bool isSpace = Char.IsWhiteSpace(c);
                if (isSpace)
                {
                    if (!wasSpace)
                        b.Append(useAsWhiteSpace);
                    wasSpace = true;
                    continue;
                }
                wasSpace = false;
                b.Append(c);
            }
            var nl = b.Length;
            if (nl > 0)
            {
                --nl;
                if (Char.IsWhiteSpace(b[nl]))
                    return b.ToString(0, nl);
            }
            return b.ToString();
        }

        /// <summary>
        /// Remove some parantheses etc from a string
        /// </summary>
        /// <param name="s"></param>
        /// <param name="groupStart"></param>
        /// <param name="groupEnd"></param>
        /// <returns></returns>
        public static String RemoveGroup(String s, Char groupStart = '(', Char groupEnd = ')')
        {
            bool changed = false;
            for (; ; )
            {
                var i = s.IndexOf(groupStart);
                if (i < 0)
                    return changed ? RemoveMultiWhiteSpace(s) : s;
                var e = s.IndexOf(groupEnd, i + 1);
                if (e < 0)
                    return changed ? RemoveMultiWhiteSpace(s) : s;
                s = s.Substring(0, i) + s.Substring(e + 1);
                changed = true;
            }
        }

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast(String first, String last, params String[] args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<String>)args);

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast(String first, String last, IEnumerable<String> args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<String>)args?.ToList());

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast(String first, String last, List<String> args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<String>)args?.ToList());

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast(String first, String last, IReadOnlyList<String> args)
        {
            var counts = args.Count;
            switch (counts)
            {
                case 0:
                    return String.Empty;
                case 1:
                    return args[0];
                case 2:
                    return String.Join(last, args);
                default:
                    --counts;
                    return String.Join(last, String.Join(first, args.ToArray(), 0, counts), args[counts]);
            }
        }

        /// <summary>
        /// Join strings using two separators into a string, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The objects to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast<T>(String first, String last, params T[] args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<T>)args);

        /// <summary>
        /// Join strings using two separators into a string, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The objects to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast<T>(String first, String last, IEnumerable<T> args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<T>)args?.ToList());


        /// <summary>
        /// Join objects using two separators into a string, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The objects to join</param>
        /// <returns>The combined string</returns>
        public static String JoinWithSpecialLast<T>(String first, String last, IReadOnlyList<T> args)
        {
            var counts = args.Count;
            switch (counts)
            {
                case 0:
                    return String.Empty;
                case 1:
                    return args[0]?.ToString();
                case 2:
                    return String.Join(last, args);
                default:
                    --counts;
                    return String.Join(last, String.Join(first, args.Select(x => x?.ToString()).ToArray(), 0, counts), args[counts]);
            }
        }


        /// <summary>
        /// Convert a string to only hex characters, useful for turning any text into something that is safe for url's, file names etc
        /// </summary>
        /// <param name="value">The string</param>
        /// <returns>null if the input value was null. String.Empty is the input value was empty, else the hex encoded string only '0' to '9' and 'a' to 'f' is returned</returns>
        public static String ToHex(this String value)
        {
            if (value == null)
                return null;
            var l = value.Length;
            if (l <= 0)
                return String.Empty;    
            var enc = Encoding.UTF8;
            var len = enc.GetMaxByteCount(l);
            Span<Byte> mem = stackalloc Byte[len];
            if (!enc.TryGetBytes(value.AsSpan(), mem, out len))
                throw new Exception("Internal error!");
            return mem.Slice(0, len).ToHexString();
        }

        /// <summary>
        /// Convert a hexadecimal string to it's original string (reverses the ToHex operation).
        /// </summary>
        /// <param name="value"></param>
        /// <returns>null if the input value was null. String.Empty is the input value was empty, else the original string (reverse of the ToHex operation)</returns>
        public static String ToStringFromHex(this String value)
        {
            if (value == null)
                return null;
            var l = value.Length;
            if (l <= 0)
                return String.Empty;
            l >>= 1;
            Span<Byte> data = stackalloc Byte[l];
            for (int i = 0, s = 0; i < l; ++i)
            {
                var u = value[s].HexValue();
                ++s;
                u <<= 4;
                u |= value[s].HexValue();
                ++s;
                data[i] = (Byte)u;
            }
            return Encoding.UTF8.GetString(data);
        }


        /// <summary>
        /// Convert a hexadecimal string to it's data representation
        /// </summary>
        /// <param name="value">A hexadecimal string, can only be null, empty or the characters '0' - '9', 'a' - 'f' or 'A' - 'F' (or it will throw)</param>
        /// <returns>null if input value is null, an empty array if input value is empty, else the binary data represented by the string</returns>
        public static Byte[] ToDataFromHex(this String value)
        {
            if (value == null)
                return null;
            var l = value.Length;
            if (l <= 0)
                return Array.Empty<Byte>();
            l >>= 1;
            var data = new Byte[l];
            for (int i = 0, s = 0; i < l; ++ i)
            {
                var u = value[s].HexValue();
                ++s;
                u <<= 4;
                u |= value[s].HexValue();
                ++s;
                data[i] = (Byte)u;
            }
            return data;


        }

        /// <summary>
        /// Check if the string contains any letter
        /// </summary>
        /// <param name="value">The string to check</param>
        /// <returns></returns>
        public static bool AnyLetter(this String value)
        {
            if (value == null)
                return false;
            var l = value.Length;
            for (int i = 0; i < l; ++i)
            {
                if (Char.IsLetter(value[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Filter a string, just keeping the allowed chars
        /// </summary>
        /// <param name="value">The string to filter</param>
        /// <param name="keep">The chars to keep</param>
        /// <returns>The filtered string</returns>
        public static String Filter(this String value, IReadOnlySet<Char> keep)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var l = value.Length;
            Span<Char> data = stackalloc char[l];
            int o = 0;
            for (int i = 0; i < l; ++i)
            {
                var c = value[i];
                if (!keep.Contains(c))
                    continue;
                data[o] = c;
                ++o;
            }
            if (o == l)
                return value;
            if (o == 0)
                return String.Empty;
            return new string(data.Slice(0, o));
        }

        /// <summary>
        /// Filter a string, just keeping the allowed chars
        /// </summary>
        /// <param name="value">The string to filter</param>
        /// <param name="keepFn">A function that is called to determine if a char should be kept, return true to keep the char</param>
        /// <returns>The filtered string</returns>
        public static String Filter(this String value, Func<Char, bool> keepFn)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var l = value.Length;
            Span<Char> data = stackalloc char[l];
            int o = 0;
            for (int i = 0; i < l; ++i)
            {
                var c = value[i];
                if (!keepFn(c))
                    continue;
                data[o] = c;
                ++o;
            }
            if (o == l)
                return value;
            if (o == 0)
                return String.Empty;
            return new string(data.Slice(0, o));
        }

        /// <summary>
        /// Filter a string, just keeping the allowed chars
        /// </summary>
        /// <param name="value">The string to filter</param>
        /// <param name="minInclusive">The first char in the range to keep</param>
        /// <param name="maxInclusive">The last char in the range to keep</param>
        /// <returns>The filtered string</returns>
        public static String Filter(this String value, Char minInclusive, Char maxInclusive)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var l = value.Length;
            Span<Char> data = stackalloc char[l];
            int o = 0;
            for (int i = 0; i < l; ++ i)
            {
                var c = value[i];
                if (c < minInclusive)
                    continue;
                if (c > maxInclusive)
                    continue;
                data[o] = c;
                ++o;
            }
            if (o == l)
                return value;
            if (o == 0)
                return String.Empty;
            return new string(data.Slice(0, o));
        }



        /// <summary>
        /// Filter a string, just keeping numerical digits (for parsing an unsigned integer)
        /// </summary>
        /// <param name="value">The string to filter</param>
        /// <returns>The filtered string</returns>
        public static String FilterUInt(this String value)
            => Filter(value, '0', '9');

        /// <summary>
        /// Filter a string, just keeping numerical digits and allowing a leading '-' (for parsing a signed integer)
        /// </summary>
        /// <param name="value">The string to filter</param>
        /// <returns>The filtered string</returns>
        public static String FilterInt(this String value)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var l = value.Length;
            Span<Char> data = stackalloc char[l];
            int o = 0;
            for (int i = 0; i < l; ++i)
            {
                var c = value[i];
                if (c < '0')
                {
                    if (c != '-')
                        continue;
                    if (o != 0)
                        continue;
                }
                if (c > '9')
                    continue;
                data[o] = c;
                ++o;
            }
            if (o == l)
                return value;
            if (o == 0)
                return String.Empty;
            return new string(data.Slice(0, o));
        }


        /// <summary>
        /// Split a string into two parts on the first occurance of a char.
        /// Example:
        /// var left = "name@example.com".SplitFirst('@', out var right);
        /// left = "name";
        /// right = "example.com";
        /// </summary>
        /// <param name="value">The value to split into two parts</param>
        /// <param name="split">The character to split</param>
        /// <param name="right">The right part, null if the split char isn't found</param>
        /// <returns>null if the value is null, else the left part (if the split char isn't found, the original string is returned)</returns>
        public static String SplitFirst(this String value, Char split, out String right)
        {
            if (String.IsNullOrEmpty(value))
            {
                right = null;
                return value;
            }
            var p = value.IndexOf(split);
            if (p < 0)
            {
                right = null;
                return value;
            }
            right = value.Substring(p + 1);
            return value.Substring(0, p);
        }


        /// <summary>
        /// Split a string into two parts (keeping the left part) on the first occurance of a char.
        /// Example:
        /// "name@example.com".SplitFirst('@') => "name"
        /// </summary>
        /// <param name="value">The value to split into two parts</param>
        /// <param name="split">The character to split</param>
        /// <returns>null if the value is null, else the left part (if the split char isn't found, the original string is returned), semantically the same as value.Split(split)[0]</returns>
        public static String SplitFirst(this String value, Char split)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var p = value.IndexOf(split);
            if (p < 0)
                return value;
            return value.Substring(0, p);
        }



        struct SecureCount
        {
            public String Str;
            public int Keep;
        }

        struct SecureStr
        {
            public String Str;
            public String Add;
        }

        static void SecureEndWithCount(Span<Char> str, SecureCount c)
        {
            var s = c.Str;
            var k = c.Keep;
            int i;
            for (i = 0; i < k; ++i)
                str[i] = s[i];
            var l = str.Length;
            for (; i < l; ++i)
                str[i] = '*';
        }

        static void SecureEndWithStr(Span<Char> str, SecureStr c)
        {
            var l = str.Length;
            var s = c.Str;
            var a = c.Add;
            var k = l - a.Length;
            int i;
            for (i = 0; i < k; ++i)
                str[i] = s[i];
            for (; i < l; ++i)
                str[i] = a[i - k];
        }

        static readonly SpanAction<Char, SecureCount> SecureEndWithCountAction = SecureEndWithCount;

        static readonly SpanAction<Char, SecureStr> SecureEndWithStrAction = SecureEndWithStr;



        static void SecureStartWithCount(Span<Char> str, SecureCount c)
        {
            var l = str.Length;
            var s = c.Str;
            var k = c.Keep;
            var p = l - k;
            int i;
            for (i = 0; i < p; ++i)
                str[i] = '*';
            for (; i < l; ++i)
                str[i] = s[i];
        }

        static void SecureStartWithStr(Span<Char> str, SecureStr c)
        {
            var l = str.Length;
            var s = c.Str;
            var a = c.Add;
            var p = a.Length;
            var k = l - p;
            int i;
            for (i = 0; i < p; ++i)
                str[i] = a[i];
            for (; i < l; ++i)
                str[i] = a[i + k];
        }

        static readonly SpanAction<Char, SecureCount> SecureStartWithCountAction = SecureStartWithCount;

        static readonly SpanAction<Char, SecureStr> SecureStartWithStrAction = SecureStartWithStr;


        /// <summary>
        /// Make a string "secure" by only keeping a few chars "visible".
        /// Examples:
        /// "1234abcd5678".SecureEnd() => "1234********";
        /// "1234abcd5678".SecureEnd(4, "..") => "1234..";
        /// </summary>
        /// <param name="value">The value to "secure"</param>
        /// <param name="keep">The number of chars to keep, this is capped to at most half the number of chars in the input</param>
        /// <param name="suffix">An optional suffix to use instead of filling with *'s</param>
        /// <returns>The "secure" string</returns>
        public static String SecureEnd(this String value, int keep = 4, String suffix = null)
        {
            if (String.IsNullOrEmpty(value))
                return null;
            var l = value.Length;
            var minKeep = l - (l >> 1);
            if (keep > minKeep)
                keep = minKeep;
            if (suffix == null)
            {
                var sc = new SecureCount
                {
                    Str = value,
                    Keep = l - keep,
                };
                return String.Create(l, sc, SecureEndWithCount);
            }
            var sa = new SecureStr
            {
                Str = value,
                Add = suffix,
            };
            return String.Create(keep + suffix.Length, sa, SecureEndWithStr);



        }

        /// <summary>
        /// Make a string "secure" by only keeping a few chars "visible".
        /// Examples:
        /// "1234abcd5678".SecureStart() => "********5678";
        /// "1234abcd5678".SecureStart(4, "..") => "..5678";
        /// </summary>
        /// <param name="value">The value to "secure"</param>
        /// <param name="keep">The number of chars to keep, this is capped to at most half the number of chars in the input</param>
        /// <param name="prefix">An optional prefix to use instead of filling with *'s</param>
        /// <returns>The "secure" string</returns>
        public static String SecureStart(this String value, int keep = 4, String prefix = null)
        {
            if (String.IsNullOrEmpty(value))
                return null;
            var l = value.Length;
            var minKeep = l - (l >> 1);
            if (keep > minKeep)
                keep = minKeep;
            if (prefix == null)
            {
                var sc = new SecureCount
                {
                    Str = value,
                    Keep = l - keep,
                };
                return String.Create(l, sc, SecureStartWithCount);
            }
            var sa = new SecureStr
            {
                Str = value,
                Add = prefix,
            };
            return String.Create(keep + prefix.Length, sa, SecureStartWithStr);
        }

    }





}
