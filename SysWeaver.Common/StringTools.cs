using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// General string helpers: quoting and formatting, casing of words, white space clean up, fuzzy matching (Levenstein), char classification and filtering,
    /// hex conversions, splitting, masking ("secure") of secrets, line splitting and markdown escaping.
    /// </summary>
    /// <remarks>
    /// Most methods avoid temporary allocations (stack or pooled buffers) and return the input instance if nothing changes.
    /// Unless stated otherwise, the string arguments may not be null. All methods are thread safe.
    /// </remarks>
    public static class StringTools
    {
        /// <summary>
        /// Temporary char buffers up to this length are allocated on the stack (larger buffers are rented from the shared array pool)
        /// </summary>
        const int MaxStackChars = 4096;

        /// <summary>
        /// Temporary int buffers up to this length are allocated on the stack (larger buffers are rented from the shared array pool)
        /// </summary>
        const int MaxStackInts = 2048;

        /// <summary>
        /// Temporary byte buffers up to this length are allocated on the stack (larger buffers are rented from the shared array pool)
        /// </summary>
        const int MaxStackBytes = 8192;

        /// <summary>
        /// Compute a deterministic hash of the string contents (stable across processes and runs, unlike <see cref="String.GetHashCode()"/>).
        /// Not a cryptographic hash.
        /// </summary>
        /// <param name="s">The string to compute a hash for, may not be null</param>
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
        /// Make sure that a string is quoted (starts and ends with the quotation char), quotes are added if it isn't
        /// </summary>
        /// <param name="s">The string, may be null</param>
        /// <param name="quotationChar">The quotation char</param>
        /// <returns>The input if it's already quoted, else a quoted copy, "null" (unquoted) if the input is null</returns>
        public static String EnsureQuoted(this String s, Char quotationChar = '"')
        {
            if (s == null)
                return "null";
            var l = s.Length;
            if (l > 1)
            {
                if ((s[0] == quotationChar) && (s[l - 1] == quotationChar))
                    return s;
            }
            return ToQuoted(s, quotationChar);
        }


        /// <summary>
        /// Add quotation chars around a string. Ex: Test => "Test"
        /// </summary>
        /// <param name="s">The string to add quotation chars around</param>
        /// <param name="quotationChar">The quotation char to use</param>
        /// <returns>A quoted string, "null" (unquoted) if the input is null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToQuoted(this String s, Char quotationChar = '"')
            => s == null ? "null" : String.Concat(new ReadOnlySpan<Char>(in quotationChar), s, new ReadOnlySpan<Char>(in quotationChar));

        /// <summary>
        /// Add quotation chars around a string. Ex: Test => "Test"
        /// </summary>
        /// <param name="s">The string to add quotation chars around</param>
        /// <param name="quotationChars">The quotation chars to use (added both before and after)</param>
        /// <returns>A quoted string, "null" (unquoted) if the input is null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToQuoted(this String s, String quotationChars)
            => s == null ? "null" : String.Concat(quotationChars, s, quotationChars);

        /// <summary>
        /// Format a string as a filename (for messages), ex: C:\a.txt => "file://C:\a.txt"
        /// </summary>
        /// <param name="s">The string to format as a filename, may be null</param>
        /// <returns>A filename formatted string, "null" if the input is null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToFilename(this String s) => s == null ? "null" : String.Concat("\"file://", s, "\"");

        /// <summary>
        /// Format a string as an email address (for messages), ex: a@b.com => "mailto:a@b.com"
        /// </summary>
        /// <param name="s">The string to format as an email address, may be null</param>
        /// <returns>An email formatted string, "null" if the input is null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToMail(this String s) => s == null ? "null" : String.Concat("\"mailto:", s, "\"");


        static readonly String FolderEnd = Path.DirectorySeparatorChar + "\"";

        /// <summary>
        /// Format a string as a folder name (for messages), any trailing directory separator is replaced by a single platform separator.
        /// Ex (windows): C:\Temp => file://"C:\Temp\" (note that the quote is placed after "file://", unlike <see cref="ToFilename(string)"/>)
        /// </summary>
        /// <param name="s">The string to format as a folder name, may be null</param>
        /// <returns>A folder name formatted string, "null" if the input is null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToFolder(this String s) => s == null ? "null" : String.Concat("file://\"", Path.TrimEndingDirectorySeparator(s.AsSpan()), FolderEnd);


        /// <summary>
        /// "Counts up" a string by incrementing the last number in it, or appending "_1" if there is no number (ASCII digits only),
        /// ex "apa_1.png" => "apa_2.png", "apa9.txt" => "apa10.txt", "apa_1_99" => "apa_1_100", "apa" => "apa_1".
        /// Typically used to create unique (file) names.
        /// </summary>
        /// <param name="str">The string to "count up", may not be null</param>
        /// <returns>A new string that has been "incremented"</returns>
        public static String CountUp(this String str)
        {
            var s = str.AsSpan();
            // Find the end of the last number
            var end = s.LastIndexOfAnyInRange('0', '9') + 1;
            if (end <= 0)
                return String.Concat(str, "_1");
            // Find the start of the last number
            var start = s[..end].LastIndexOfAnyExceptInRange('0', '9') + 1;
            // If all digits are 9's, the number gets one digit longer, ex: "99" => "100"
            var grow = s[start..end].IndexOfAnyExcept('9') < 0;
            return String.Create(grow ? s.Length + 1 : s.Length, (str, start, end), CountUpAction);
        }

        /// <summary>
        /// Writes the counted up string (the state is the string and the start and end of the last number)
        /// </summary>
        static readonly SpanAction<Char, (String Str, int Start, int End)> CountUpAction = (d, st) =>
        {
            var s = st.Str.AsSpan();
            var end = st.End;
            if (d.Length == s.Length)
            {
                // Increment, propagating the carry (there is at least one digit that isn't a 9)
                s.CopyTo(d);
                var i = end - 1;
                while (d[i] == '9')
                {
                    d[i] = '0';
                    --i;
                }
                ++d[i];
                return;
            }
            // All digits were 9's, ex: "x99y" => "x100y"
            var start = st.Start;
            s[..start].CopyTo(d);
            d[start] = '1';
            d.Slice(start + 1, end - start).Fill('0');
            s[end..].CopyTo(d[(end + 1)..]);
        };

        static readonly SpanAction<Char, String> CreateFirstUpperAction = (str, c) =>
        {
            str[0] = c[0].FastToUpper();
            c.AsSpan().Slice(1).CopyTo(str.Slice(1));
        };


        static readonly SpanAction<Char, String> CreateFirstLowerAction = (str, c) =>
        {
            str[0] = c[0].FastToLower();
            c.AsSpan().Slice(1).CopyTo(str.Slice(1));
        };

        /// <summary>
        /// Make sure that the first character is an uppercase letter (if it's a letter).
        /// Examples:
        /// "hello" becomes "Hello".
        /// "World" remains "World".
        /// "123" remains "123".
        /// </summary>
        /// <param name="str">The text to make the first letter uppercased, may be null</param>
        /// <returns>The original string (also if null or empty) or a new string with the first letter uppercased (invariant culture)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String MakeFirstUppercase(this String str)
            => (String.IsNullOrEmpty(str) || str[0].FastIsUpperOrNonLetter()) ? str : String.Create(str.Length, str, CreateFirstUpperAction);

        /// <summary>
        /// Make sure that the first character in each word is an uppercase letter (if it's a letter).
        /// Examples:
        /// "hello world" becomes "Hello World".
        /// "World is mine" becomes "World Is Mine".
        /// "123" remains "123".
        /// </summary>
        /// <param name="str">The text to make the first letter in each word uppercased, may be null.
        /// A word starts with a letter or digit that follows a non letter or digit (same as <see cref="OnWordStart(string, Func{int, bool}, int)"/>)</param>
        /// <returns>The original string (also if null, empty or nothing changes) or a new string with the first letter in each word uppercased (invariant culture)</returns>
        public static String MakeFirstCharInWordsUppercase(this String str)
        {
            if (String.IsNullOrEmpty(str))
                return str;
            // Find the first word start that changes (same word starts as OnWordStart)
            var s = str.AsSpan();
            var l = s.Length;
            bool prevIsLetter = false;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                var isP = Char.IsLetterOrDigit(c);
                if (isP && !prevIsLetter && (c.FastToUpper() != c))
                    return String.Create(l, (str, i), MakeFirstCharInWordsUppercaseAction);
                prevIsLetter = isP;
            }
            return str;
        }

        static readonly SpanAction<Char, (String Str, int First)> MakeFirstCharInWordsUppercaseAction = (d, st) =>
        {
            var s = st.Str.AsSpan();
            s.CopyTo(d);
            var l = s.Length;
            var first = st.First;
            d[first] = s[first].FastToUpper();
            bool prevIsLetter = true;
            for (int i = first + 1; i < l; ++i)
            {
                var c = s[i];
                var isP = Char.IsLetterOrDigit(c);
                if (isP && !prevIsLetter)
                    d[i] = c.FastToUpper();
                prevIsLetter = isP;
            }
        };

        /// <summary>
        /// Make sure that the first character is a lowercase letter (if it's a letter).
        /// Examples:
        /// "Hello" becomes "hello".
        /// "world" remains "world".
        /// "123" remains "123".
        /// </summary>
        /// <param name="str">The text to make the first letter lowercased, may be null</param>
        /// <returns>The original string (also if null or empty) or a new string with the first letter lowercased (invariant culture)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String MakeFirstLowercase(this String str)
            => (String.IsNullOrEmpty(str) || str[0].FastIsLowerOrNonLetter()) ? str : String.Create(str.Length, str, CreateFirstLowerAction);

        /// <summary>
        /// Take a camel cased string and convert it to a space separated string.
        /// A space is inserted before an upper case letter that follows a non upper case char (so acronyms aren't split, ex: "HTMLParser" is unchanged).
        /// Ex:
        /// "MyNameIsStupid" => "My name is stupid"
        /// </summary>
        /// <param name="str">The camel cased string. Ex: "MyNameIsStupid"</param>
        /// <param name="space">The character to use for space</param>
        /// <param name="keepFirstWordLetterCasing">If true, keep the casing of the first letter in each word (else it's lower cased, the first word is never changed)</param>
        /// <returns>The space separated string (the input instance if nothing changed). Ex: "My name is stupid"</returns>
        [SkipLocalsInit]
        public static String RemoveCamelCase(this String str, Char space = ' ', bool keepFirstWordLetterCasing = false)
        {
            var s = str.AsSpan();
            var l = str.Length;
            // The result is at most twice as long
            var bl = l + l;
            char[] rented = null;
            Span<Char> b = bl <= MaxStackChars ? stackalloc Char[bl] : (rented = ArrayPool<Char>.Shared.Rent(bl));
            try
            {
                int o = 0;
                bool prevIsUpper = true;
                foreach (var c in s)
                {
                    var isUpper = Char.IsUpper(c);
                    if (isUpper && (!prevIsUpper))
                    {
                        b[o] = space;
                        b[o + 1] = keepFirstWordLetterCasing ? c : CharExt.FastToLower(c);
                        o += 2;
                        prevIsUpper = true;
                        continue;
                    }
                    b[o] = c;
                    ++o;
                    prevIsUpper = isUpper;
                }
                return o == l ? str : new String(b[..o]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }


        /// <summary>
        /// The cost of mismatching a char (the cost of a mismatched pair is the max of the two chars cost)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int LevensteinCharCost(char c, int costLetter, int costNumber)
        {
            if (Char.IsNumber(c))
                return costNumber;
            return Char.IsLetter(c) ? costLetter : 1;
        }

        /// <summary>
        /// Levenstein (edit) distance, case sensitive (ordinal).
        /// Inserting or removing a char costs 1, replacing a char costs the max of the mismatch cost of the two chars (letters use <paramref name="costLetter"/>, numbers use <paramref name="costNumber"/>, other chars cost 1).
        /// </summary>
        /// <param name="source1">First string (null is the same as empty)</param>
        /// <param name="source2">Second string (null is the same as empty)</param>
        /// <param name="costLetter">Mismatched letter cost</param>
        /// <param name="costNumber">Mismatched number cost</param>
        /// <returns>The Levenstein distance between the two strings (the length of the other string if one is empty)</returns>
        [SkipLocalsInit]
        public static int Levenstein(string source1, string source2, int costLetter = 1, int costNumber = 1)
        {
            var source1Length = source1?.Length ?? 0;
            var source2Length = source2?.Length ?? 0;

            // First calculation, if one entry is empty return full length
            if (source1Length == 0)
                return source2Length;

            if (source2Length == 0)
                return source1Length;

            // Only a single row of the matrix is needed, the "diagonal" and "left" values are kept in locals.
            // The mismatch cost of every char in source2 is computed once (instead of for every mismatch)
            var w = source2Length + 1;
            var bl = w + source2Length;
            int[] rented = null;
            Span<int> buffer = bl <= MaxStackInts ? stackalloc int[bl] : (rented = ArrayPool<int>.Shared.Rent(bl));
            try
            {
                var row = buffer[..w];
                var costs = buffer.Slice(w, source2Length);
                for (var j = 0; j < w; ++j)
                    row[j] = j;
                var s1 = source1.AsSpan();
                var s2 = source2.AsSpan();
                for (var j = 0; j < s2.Length; ++j)
                    costs[j] = LevensteinCharCost(s2[j], costLetter, costNumber);
                var r = row[1..];
                // Calculate rows and collumns distances
                for (var i = 1; i <= source1Length; i++)
                {
                    var diag = i - 1;
                    var left = i;
                    var b = s1[i - 1];
                    var costB = LevensteinCharCost(b, costLetter, costNumber);
                    for (var j = 0; j < s2.Length; j++)
                    {
                        var up = r[j];
                        int cost = 0;
                        if (s2[j] != b)
                        {
                            cost = costs[j];
                            if (costB > cost)
                                cost = costB;
                        }
                        var v = Math.Min(Math.Min(up, left) + 1, diag + cost);
                        diag = up;
                        r[j] = v;
                        left = v;
                    }
                }
                return row[source2Length];
            }
            finally
            {
                if (rented != null)
                    ArrayPool<int>.Shared.Return(rented);
            }
        }


        /// <summary>
        /// Extract all words from some text, a word is a run of letters or digits (<see cref="Char.IsLetterOrDigit(char)"/>)
        /// </summary>
        /// <param name="text">The text, may not be null</param>
        /// <returns>The words (in order), an empty array if there are none</returns>
        [SkipLocalsInit]
        public static String[] ExtractWords(this String text)
        {
            var l = text.Length;
            var s = text.AsSpan();
            // Find the word boundaries in a single pass (same word starts as OnWordStart), so that only the result is allocated.
            // There are at most (l + 1) / 2 words, each needs a start and an end
            var bl = l + 2;
            int[] rented = null;
            Span<int> bounds = bl <= MaxStackInts ? stackalloc int[bl] : (rented = ArrayPool<int>.Shared.Rent(bl));
            try
            {
                int n = 0;
                bool prevIsLetter = false;
                for (int i = 0; i < l; ++i)
                {
                    var isP = Char.IsLetterOrDigit(s[i]);
                    if (isP != prevIsLetter)
                    {
                        // A word start or end
                        bounds[n] = i;
                        ++n;
                    }
                    prevIsLetter = isP;
                }
                if (prevIsLetter)
                {
                    bounds[n] = l;
                    ++n;
                }
                var count = n >> 1;
                if (count == 0)
                    return Array.Empty<String>();
                var words = new String[count];
                for (int w = 0, b = 0; w < count; ++w, b += 2)
                {
                    var start = bounds[b];
                    words[w] = text.Substring(start, bounds[b + 1] - start);
                }
                return words;
            }
            finally
            {
                if (rented != null)
                    ArrayPool<int>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Fuzzy match two texts by their words (see <see cref="ExtractWords(string)"/> and <see cref="FuzzyMatch(string[], string[])"/>), lower is a better match
        /// </summary>
        /// <param name="text">The text to search in</param>
        /// <param name="matchWith">The search text</param>
        /// <returns>The match error (a weighted sum of the best word matches), int.MaxValue if any of the texts have no words</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FuzzyMatch(string text, string matchWith)
            => FuzzyMatch(ExtractWords(text), ExtractWords(matchWith));


        /// <summary>
        /// The Levenstein cost of a mismatched letter used by the fuzzy matching
        /// </summary>
        public const int FuzzyLevensteinLetterCost = 3;

        /// <summary>
        /// The Levenstein cost of a mismatched number used by the fuzzy matching
        /// </summary>
        public const int FuzzyLevensteinNumberCost = 4;

        /// <summary>
        /// The Levenstein distance of a word pair is shifted left by this many bits (weighted), when used by the fuzzy matching
        /// </summary>
        public const int FuzzyLevensteinShiftWeight = 4;


        /// <summary>
        /// The length difference of a word that contains the search word is shifted left by this many bits (weighted)
        /// </summary>
        const int FuzzyPartOfShiftWeight = 1;

        /// <summary>
        /// The number of search words that couldn't be matched (when all text words are used) is shifted left by this many bits (weighted)
        /// </summary>
        const int FuzzyOrderShiftWeight = 0;

        /// <summary>
        /// The number of text words that wasn't matched is shifted left by this many bits (weighted)
        /// </summary>
        const int FuzzyMissingWordsShiftWeight = 0;

        const int FuzzyMaxShiftWeight = FuzzyLevensteinShiftWeight > FuzzyPartOfShiftWeight
                                        ?
                                        (FuzzyLevensteinShiftWeight > FuzzyOrderShiftWeight ? FuzzyLevensteinShiftWeight : FuzzyOrderShiftWeight)
                                        :
                                        (FuzzyPartOfShiftWeight > FuzzyOrderShiftWeight ? FuzzyPartOfShiftWeight : FuzzyOrderShiftWeight);


        /// <summary>
        /// The fuzzy match error threshold for a search text, results of <see cref="FuzzyMatch(string, string)"/> above this are typically considered non matches
        /// </summary>
        /// <param name="searchLength">The length of the search text</param>
        /// <param name="maxError">A tolerance divisor, a higher value gives a lower (stricter) threshold</param>
        /// <returns>The max error (at least 1)</returns>
        public static int FuzzyMaxErr(int searchLength, int maxError = 2) => Math.Max(1, (((searchLength + maxError - 1) * FuzzyLevensteinNumberCost) << FuzzyMaxShiftWeight) / maxError);

        /// <summary>
        /// Fuzzy match words, every match word (in order) is greedily paired with the best remaining text word, lower is a better match.
        /// A text word that contains the match word (ordinal) costs the length difference (weighted), else the Levenstein distance is used (weighted).
        /// </summary>
        /// <param name="textWords">The words to search in (not modified), may not contain null</param>
        /// <param name="matchWords">The search words, may not contain null</param>
        /// <returns>The match error (the sum of the best word matches), int.MaxValue if any of the arrays are empty</returns>
        public static int FuzzyMatch(string[] textWords, string[] matchWords)
        {
            var wlen = textWords.Length;
            var mlen = matchWords.Length;
            if ((wlen <= 0) || (mlen <= 0))
                return int.MaxValue;
            // The words are reordered, use a (pooled) copy
            var pool = ArrayPool<String>.Shared;
            var words = pool.Rent(wlen);
            try
            {
                textWords.CopyTo(words, 0);
                int levSum = 0;
                for (int x = 0; x < mlen; ++x)
                {
                    var a = matchWords[x];
                    int levBest = int.MaxValue;
                    int ibest = 0;
                    for (int i = 0; i < wlen; ++i)
                    {
                        var t = words[i];
                        var found = t.AsSpan().IndexOf(a.AsSpan());
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
                    var o = words[wlen];
                    words[wlen] = words[ibest];
                    words[ibest] = o;
                }
                levSum += (wlen << FuzzyMissingWordsShiftWeight);
                return levSum;
            }
            finally
            {
                pool.Return(words, true);
            }
        }

        /// <summary>
        /// Inspect each char and find the first match
        /// </summary>
        /// <param name="text">Text to search, null returns -1</param>
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
        /// Find the end of a word (the first char that isn't a letter or digit)
        /// </summary>
        /// <param name="text">Text to search, null returns -1</param>
        /// <param name="startIndex">Start position</param>
        /// <returns>Position of the first non letter or digit, or -1 if none is found (the word extends to the end of the text)</returns>
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
        /// <param name="maxLen">The maximum allowed length of the output string, must not be negative</param>
        /// <param name="elipses">If the string is cut short, end it with this string (only if max len is more than twice as long as this string, else the string is just cut)</param>
        /// <returns>A string that have at most max length chars (the input if it's short enough, null or empty)</returns>
        public static String LimitLength(this String s, int maxLen, String elipses = "...")
        {
            if (String.IsNullOrEmpty(s))
                return s;
            var l = s.Length;
            if (l <= maxLen)
                return s;
            if (String.IsNullOrEmpty(elipses))
                return s.Substring(0, maxLen);
            var el = elipses.Length;
            if ((el + el) < maxLen)
                return String.Concat(s.AsSpan(0, maxLen - el), elipses);
            return s.Substring(0, maxLen);
        }


        /// <summary>
        /// Find all word starts and execute a function on them
        /// </summary>
        /// <param name="text">The text to find word starts in</param>
        /// <param name="onNewWordStart">A function that is executed for every found word start (a letter or digit that follows a non letter or digit), the parameter is the start index, return false to abort further processing</param>
        /// <param name="start">The optional first position in the string to search, if it's inside a word that word is skipped</param>
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
        /// Get the length of the chars after applying a char map
        /// </summary>
        static int GetMappedLength(ReadOnlySpan<Char> source, IReadOnlyDictionary<Char, String> charMap)
        {
            int l = 0;
            foreach (var c in source)
                l += charMap.TryGetValue(c, out var cc) ? (cc?.Length ?? 0) : 1;
            return l;
        }

        /// <summary>
        /// Apply a char map
        /// </summary>
        static void MapChars(ReadOnlySpan<Char> source, Span<Char> destination, IReadOnlyDictionary<Char, String> charMap)
        {
            int o = 0;
            foreach (var c in source)
            {
                if (!charMap.TryGetValue(c, out var cc))
                {
                    destination[o] = c;
                    ++o;
                    continue;
                }
                if (cc == null)
                    continue;
                cc.AsSpan().CopyTo(destination[o..]);
                o += cc.Length;
            }
        }

        /// <summary>
        /// Clean up strings, removing duplicate white-spaces, turning all white spaces and control chars (below 31) to ' ' (tab's etc), and trimming the result.
        /// </summary>
        /// <param name="s">The string, may be null</param>
        /// <param name="charMap">An optional char remapper, applied (after trimming) before the white spaces are collapsed. A char maps to a string (null removes the char)</param>
        /// <returns>A sanitized string, the input instance if nothing changed (or if null or empty)</returns>
        [SkipLocalsInit]
        public static String Sanitize(this String s, IReadOnlyDictionary<Char, String> charMap = null)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            var src = s.AsSpan().Trim();
            if (src.IsEmpty)
                return String.Empty;
            var l = src.Length;
            if (charMap != null)
            {
                l = GetMappedLength(src, charMap);
                if (l <= 0)
                    return String.Empty;
            }
            char[] rented = null;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                t = t[..l];
                if (charMap != null)
                    MapChars(src, t, charMap);
                else
                    src.CopyTo(t);
                // Collapse white spaces (in place)
                bool isWhite = true;
                int o = 0;
                int lastNonWhite = 0;
                for (int i = 0; i < l; ++i)
                {
                    var c = t[i];
                    if (Char.IsWhiteSpace(c) || (c < 31))
                    {
                        if (isWhite)
                            continue;
                        t[o] = ' ';
                        ++o;
                        isWhite = true;
                        continue;
                    }
                    isWhite = false;
                    t[o] = c;
                    ++o;
                    lastNonWhite = o;
                }
                // Control chars and char map replacements can leave a trailing white space that the initial Trim didn't catch
                var res = t[..lastNonWhite];
                return res.SequenceEqual(s) ? s : new String(res);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }




        /// <summary>
        /// The default identifier char test of <see cref="CodeSanitize(string, Func{char, bool}, IReadOnlyDictionary{char, string})"/>: a letter, digit, '_' or '@'
        /// </summary>
        /// <param name="c">The char to test</param>
        /// <returns>True if the char can be part of an identifier</returns>
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
        /// A cached delegate of <see cref="IsCodeIdentifierChar(char)"/>
        /// </summary>
        static readonly Func<Char, bool> IsCodeIdentifierCharFn = IsCodeIdentifierChar;

        /// <summary>
        /// Clean up code strings, removing duplicate white-spaces, turning all white spaces (and control chars below 31) to ' ' (tab's etc).
        /// Removing redundant spaces: a white space is only kept if it separates two identifier chars, ex: "a  =  b + c" => "a=b+c", "int  x" => "int x".
        /// </summary>
        /// <param name="s">The string, may be null</param>
        /// <param name="isCodeIdentifier">An optional function that returns true if a char is a possible identifier char, default is <see cref="IsCodeIdentifierChar(char)"/></param>
        /// <param name="charMap">An optional char remapper, applied (after trimming) before the white spaces are processed. A char maps to a string (null removes the char)</param>
        /// <returns>A sanitized string, the input instance if nothing changed (or if null or empty)</returns>
        [SkipLocalsInit]
        public static String CodeSanitize(this String s, Func<Char, bool> isCodeIdentifier = null, IReadOnlyDictionary<Char, String> charMap = null)
        {
            if (String.IsNullOrEmpty(s))
                return s;
            var src = s.AsSpan().Trim();
            if (src.IsEmpty)
                return String.Empty;
            isCodeIdentifier ??= IsCodeIdentifierCharFn;
            var l = src.Length;
            if (charMap != null)
            {
                l = GetMappedLength(src, charMap);
                if (l <= 0)
                    return String.Empty;
            }
            char[] rented = null;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                t = t[..l];
                if (charMap != null)
                    MapChars(src, t, charMap);
                else
                    src.CopyTo(t);
                // Collapse white spaces (in place, the write position is never after the read position)
                bool isWhite = true;
                bool isIdentifier = false;
                int o = 0;
                for (int i = 0; i < l; ++i)
                {
                    var c = t[i];
                    if (Char.IsWhiteSpace(c) || (c < 31))
                    {
                        if (isWhite)
                            continue;
                        if (!isIdentifier)
                            continue;
                        var n = i + 1;
                        if (n < l)
                        {
                            if (!isCodeIdentifier(t[n]))
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
                    t[o] = c;
                    ++o;
                }
                var res = t[..o];
                return res.SequenceEqual(s) ? s : new String(res);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }



        /// <summary>
        /// Check if a string contains anything but ascii (7-bit).
        /// If any char in the string have a value greater or equal to 128 this method returns false.
        /// </summary>
        /// <param name="s">The string to check (null or empty returns true)</param>
        /// <returns>True if all chars in the string is less than 128</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiOnly(this String s)
            => Ascii.IsValid(s.AsSpan());

        /// <summary>
        /// Replace all non ascii chars in a string
        /// </summary>
        /// <param name="s">The string to replace chars in, may be null</param>
        /// <param name="replaceWith">The char to replace non-ascii chars to (every UTF-16 char is replaced, so a surrogate pair becomes two chars)</param>
        /// <returns>The original string if all ascii (or null or empty), or a string with replace values</returns>
        public static String ReplaceNonAscii(this String s, Char replaceWith = ' ')
        {
            if (String.IsNullOrEmpty(s))
                return s;
            var first = s.AsSpan().IndexOfAnyExceptInRange((Char)0, (Char)127);
            if (first < 0)
                return s;
            return String.Create(s.Length, (s, replaceWith, first), ReplaceNonAsciiAction);
        }

        static readonly SpanAction<Char, (String Str, Char With, int First)> ReplaceNonAsciiAction = (d, st) =>
        {
            st.Str.AsSpan().CopyTo(d);
            var w = st.With;
            var l = d.Length;
            for (int i = st.First; i < l; ++i)
            {
                if (d[i] >= 128)
                    d[i] = w;
            }
        };


        /// <summary>
        /// Make a string ascii, by removing diacritics (see <see cref="StringExt.RemoveDiacritics(string)"/>), using a (small) substitution table, or replacing the remaining non-ascii chars.
        /// </summary>
        /// <param name="s">The string to replace chars in, may be null</param>
        /// <param name="subsituteUnknownWith">Unknown non-ascii chars will be replaced by this</param>
        /// <returns>The original string if all ascii (or null or empty), or a string with replace values</returns>
        public static String MakeAscii(this String s, Char subsituteUnknownWith = ' ')
        {
            if (String.IsNullOrEmpty(s))
                return s;
            if (Ascii.IsValid(s.AsSpan()))
                return s;
            s = s.RemoveDiacritics();
            var first = s.AsSpan().IndexOfAnyExceptInRange((Char)0, (Char)127);
            if (first < 0)
                return s;
            return String.Create(s.Length, (s, subsituteUnknownWith, first), MakeAsciiAction);
        }

        static readonly SpanAction<Char, (String Str, Char With, int First)> MakeAsciiAction = (d, st) =>
        {
            st.Str.AsSpan().CopyTo(d);
            var w = st.With;
            var toA = ToAscii;
            var l = d.Length;
            for (int i = st.First; i < l; ++i)
            {
                var c = d[i];
                if (c >= 128)
                    d[i] = toA.TryGetValue(c, out var c2) ? c2 : w;
            }
        };

        /// <summary>
        /// Substitutions of non ascii chars that doesn't have a diacritic decomposition
        /// </summary>
        static readonly IReadOnlyDictionary<Char, Char> ToAscii = new Dictionary<Char, Char>()
        {
            {  '✕', 'x' },
            {  '×', 'x' },
        }.Freeze();


        static readonly SearchValues<Char> AsciiLetters = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
        static readonly SearchValues<Char> AsciiLettersOrSpace = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ");
        static readonly SearchValues<Char> AsciiLettersOrDigits = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");
        static readonly SearchValues<Char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");
        static readonly SearchValues<Char> HexDigitsOrSpace = SearchValues.Create("0123456789abcdefABCDEF ");

        /// <summary>
        /// Check if a string is a valid "identifier", only 'a'-'z', 'A'-'Z', and '0'-'9' is accepted (the first char must be a letter, '_' is not allowed)
        /// </summary>
        /// <param name="s">The string to check (null or empty returns false)</param>
        /// <returns>True if all chars in the string is valid</returns>
        public static bool IsIdentifier(this String s)
        {
            if (String.IsNullOrEmpty(s))
                return false;
            var c = s[0];
            if (((uint)((c | 0x20) - 'a')) > ('z' - 'a'))
                return false;
            return s.AsSpan(1).IndexOfAnyExcept(AsciiLettersOrDigits) < 0;
        }


        /// <summary>
        /// Check if a string is numeric (only contains '0' to '9', and optionally spaces, a leading '-' and a '.').
        /// At least one digit is required.
        /// </summary>
        /// <param name="s">The string to check (null or empty returns false)</param>
        /// <param name="allowSpace">true to allow spaces anywhere (should filter them out before converting to a number)</param>
        /// <param name="allowNeg">true to allow a single '-' as the first char</param>
        /// <param name="allowDecimal">true to allow a single '.' (anywhere)</param>
        /// <returns>True if all chars in the string is a number</returns>
        public static bool IsNumeric(this String s, bool allowSpace = true, bool allowNeg = false, bool allowDecimal = false)
        {
            if (s == null)
                return false;
            var l = s.Length;
            if (l <= 0)
                return false;
            // Vectorized: only digits
            if (s.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0)
                return true;
            bool haveDigit = false;
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
                haveDigit = true;
            }
            return haveDigit;
        }


        /// <summary>
        /// Check if a string is made up of only hexadecimal digits (only contains '0' to '9', 'a' to 'f' or 'A' to 'F').
        /// </summary>
        /// <param name="s">The string to check (null or empty returns false)</param>
        /// <param name="allowSpace">true to allow spaces (should filter them out before converting to a number), note that a string with only spaces returns true</param>
        /// <returns>True if all chars in the string is hexadecimal digits</returns>
        public static bool IsHex(this String s, bool allowSpace = true)
        {
            if (String.IsNullOrEmpty(s))
                return false;
            return s.AsSpan().IndexOfAnyExcept(allowSpace ? HexDigitsOrSpace : HexDigits) < 0;
        }


        /// <summary>
        /// Check if a string is letters only (<see cref="Char.IsLetter(char)"/>).
        /// </summary>
        /// <param name="s">The string to check (null returns false, empty returns true)</param>
        /// <param name="allowSpace">true to allow spaces</param>
        /// <returns>True if all chars in the string is a letter (or space if allowed)</returns>
        public static bool IsLetters(this String s, bool allowSpace = true)
        {
            if (s == null)
                return false;
            var v = s.AsSpan();
            // Vectorized: skip ASCII letters (and spaces)
            var first = v.IndexOfAnyExcept(allowSpace ? AsciiLettersOrSpace : AsciiLetters);
            if (first < 0)
                return true;
            var l = v.Length;
            for (int i = first; i < l; ++i)
            {
                var c = v[i];
                if (Char.IsLetter(c))
                    continue;
                if (allowSpace && (c == ' '))
                    continue;
                return false;
            }
            return true;
        }


        /// <summary>
        /// Replaces runs of white spaces with a single white space, and trims white spaces from the start and end (can be done in place).
        /// </summary>
        /// <param name="source">The source chars</param>
        /// <param name="destination">The destination (may be the same as the source)</param>
        /// <param name="useAsWhiteSpace">Replace white spaces with a single of this</param>
        /// <returns>The number of chars written to the destination</returns>
        static int RemoveMultiWhiteSpace(ReadOnlySpan<Char> source, Span<Char> destination, Char useAsWhiteSpace)
        {
            var l = source.Length;
            int o = 0;
            bool wasSpace = true;
            int lastNonSpace = 0;
            for (int i = 0; i < l; ++i)
            {
                var c = source[i];
                if (Char.IsWhiteSpace(c))
                {
                    if (!wasSpace)
                    {
                        destination[o] = useAsWhiteSpace;
                        ++o;
                    }
                    wasSpace = true;
                    continue;
                }
                wasSpace = false;
                destination[o] = c;
                ++o;
                lastNonSpace = o;
            }
            return lastNonSpace;
        }

        /// <summary>
        /// Replaces runs of white spaces (<see cref="Char.IsWhiteSpace(char)"/>) with a single white space (and trims white spaces from start and end).
        /// </summary>
        /// <param name="s">The string, may not be null</param>
        /// <param name="useAsWhiteSpace">Replace white spaces with a single of this</param>
        /// <returns>The cleaned string, the input instance if nothing changed</returns>
        [SkipLocalsInit]
        public static String RemoveMultiWhiteSpace(String s, Char useAsWhiteSpace = ' ')
        {
            var l = s.Length;
            char[] rented = null;
            Span<Char> b = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                var res = b[..RemoveMultiWhiteSpace(s, b, useAsWhiteSpace)];
                return res.SequenceEqual(s) ? s : new String(res);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Remove some parantheses etc from a string.
        /// Nested groups are removed as a whole, ex: "a (b (c) d) e" => "a e".
        /// An unclosed group is kept as is, and an end char without a matching start char is kept.
        /// </summary>
        /// <param name="s">The string, may not be null</param>
        /// <param name="groupStart">The char that starts a group</param>
        /// <param name="groupEnd">The char that ends a group (may be the same as the start char, ex: quotes)</param>
        /// <returns>The original string if no group was removed, else the string with all groups removed (and white spaces cleaned up, see <see cref="RemoveMultiWhiteSpace(string, char)"/>)</returns>
        [SkipLocalsInit]
        public static String RemoveGroup(String s, Char groupStart = '(', Char groupEnd = ')')
        {
            var src = s.AsSpan();
            var l = src.Length;
            // Most strings doesn't contain any group
            var firstStart = src.IndexOf(groupStart);
            if (firstStart < 0)
                return s;
            char[] rented = null;
            Span<Char> b = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                int o = 0;
                bool changed = false;
                int depth = 0;
                int groupPos = 0;
                int copyFrom = 0;
                for (int i = firstStart; i < l; ++i)
                {
                    var c = src[i];
                    // Check end first, so that groupStart == groupEnd (ex: quotes) works
                    if ((c == groupEnd) && (depth > 0))
                    {
                        --depth;
                        if (depth == 0)
                        {
                            changed = true;
                            var keep = src.Slice(copyFrom, groupPos - copyFrom);
                            keep.CopyTo(b[o..]);
                            o += keep.Length;
                            copyFrom = i + 1;
                        }
                        continue;
                    }
                    if (c != groupStart)
                        continue;
                    if (depth == 0)
                        groupPos = i;
                    ++depth;
                }
                if (!changed)
                    return s;
                // Keep the rest, including any unclosed group
                var rest = src[copyFrom..];
                rest.CopyTo(b[o..]);
                o += rest.Length;
                // Clean up the white spaces (in place)
                var n = RemoveMultiWhiteSpace(b[..o], b, ' ');
                return new String(b[..n]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join, may not be null (null elements are treated as empty, except that a single null element returns null)</param>
        /// <returns>The combined string, ex: ", " and " and " joins ["a", "b", "c"] to "a, b and c"</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String JoinWithSpecialLast(String first, String last, params String[] args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<String>)args);

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join</param>
        /// <returns>The combined string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String JoinWithSpecialLast(String first, String last, IEnumerable<String> args)
            => JoinWithSpecialLast(first, last, args as IReadOnlyList<String> ?? args?.ToList());

        /// <summary>
        /// Join strings using two separators, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The strings to join</param>
        /// <returns>The combined string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String JoinWithSpecialLast(String first, String last, List<String> args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<String>)args);

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
                    return String.Concat(args[0], last, args[1]);
                default:
                    return InternalJoinWithSpecialLast(first, last, args, counts);
            }
        }

        /// <summary>
        /// Join the first count strings (at least 3), only the result is allocated
        /// </summary>
        /// <exception cref="OverflowException">The result is too long</exception>
        static String InternalJoinWithSpecialLast(String first, String last, IReadOnlyList<String> args, int count)
        {
            long total = (long)(first?.Length ?? 0) * (count - 2) + (last?.Length ?? 0);
            for (int i = 0; i < count; ++i)
                total += args[i]?.Length ?? 0;
            return String.Create(checked((int)total), (first, last, args, count), JoinWithSpecialLastAction);
        }

        static readonly SpanAction<Char, (String First, String Last, IReadOnlyList<String> Args, int Count)> JoinWithSpecialLastAction = (d, st) =>
        {
            var args = st.Args;
            var count = st.Count;
            var lastIndex = count - 1;
            int o = 0;
            for (int i = 0; i < count; ++i)
            {
                if (i > 0)
                {
                    var sep = (i == lastIndex ? st.Last : st.First).AsSpan();
                    sep.CopyTo(d[o..]);
                    o += sep.Length;
                }
                var a = args[i].AsSpan();
                a.CopyTo(d[o..]);
                o += a.Length;
            }
        };

        /// <summary>
        /// Join strings using two separators into a string, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The objects to join</param>
        /// <returns>The combined string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String JoinWithSpecialLast<T>(String first, String last, params T[] args)
            => JoinWithSpecialLast(first, last, (IReadOnlyList<T>)args);

        /// <summary>
        /// Join strings using two separators into a string, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The objects to join</param>
        /// <returns>The combined string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String JoinWithSpecialLast<T>(String first, String last, IEnumerable<T> args)
            => JoinWithSpecialLast(first, last, args as IReadOnlyList<T> ?? args?.ToList());


        /// <summary>
        /// Join objects using two separators into a string, one only used for the last separation.
        /// </summary>
        /// <param name="first">The first separators, used for all but the last</param>
        /// <param name="last">The last separator</param>
        /// <param name="args">The objects to join (converted using ToString, null is treated as empty)</param>
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
                    return String.Concat(args[0]?.ToString(), last, args[1]?.ToString());
                default:
                    // Convert to strings in a pooled array
                    var pool = ArrayPool<String>.Shared;
                    var strings = pool.Rent(counts);
                    try
                    {
                        for (int i = 0; i < counts; ++i)
                            strings[i] = args[i]?.ToString();
                        return InternalJoinWithSpecialLast(first, last, strings, counts);
                    }
                    finally
                    {
                        pool.Return(strings, true);
                    }
            }
        }


        /// <summary>
        /// Convert a string to only hex characters (the lower case hex of the UTF-8 bytes), useful for turning any text into something that is safe for url's, file names etc
        /// </summary>
        /// <param name="value">The string</param>
        /// <returns>null if the input value was null. String.Empty is the input value was empty, else the hex encoded string only '0' to '9' and 'a' to 'f' is returned</returns>
        [SkipLocalsInit]
        public static String ToHex(this String value)
        {
            if (value == null)
                return null;
            var l = value.Length;
            if (l <= 0)
                return String.Empty;
            var enc = Encoding.UTF8;
            var len = enc.GetMaxByteCount(l);
            byte[] rented = null;
            Span<Byte> mem = len <= MaxStackBytes ? stackalloc Byte[len] : (rented = ArrayPool<Byte>.Shared.Rent(len));
            try
            {
                if (!enc.TryGetBytes(value.AsSpan(), mem, out len))
                    throw new Exception("Internal error!");
                return Convert.ToHexStringLower(mem[..len]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Decode hex chars to bytes, throws the same exception as HexValue on invalid chars
        /// </summary>
        /// <param name="value">The hex chars (an even number)</param>
        /// <param name="data">The destination (half the length of the value)</param>
        static void DecodeHex(ReadOnlySpan<Char> value, Span<Byte> data)
        {
            if (Convert.FromHexString(value, data, out var consumed, out _) == OperationStatus.Done)
                return;
            // Throw the same exception as before (on the first invalid char)
            value[consumed].HexValue();
            value[consumed + 1].HexValue();
            throw new Exception("Invalid hex string!");
        }

        /// <summary>
        /// Convert a hexadecimal string to it's original string (reverses the <see cref="ToHex(string)"/> operation, the bytes are decoded as UTF-8).
        /// If the number of chars are odd, the last char is ignored.
        /// </summary>
        /// <param name="value">The hex string ('0' - '9', 'a' - 'f' or 'A' - 'F')</param>
        /// <returns>null if the input value was null. String.Empty is the input value was empty, else the original string (reverse of the ToHex operation)</returns>
        /// <exception cref="Exception">The string contains a char that isn't a hex digit</exception>
        [SkipLocalsInit]
        public static String ToStringFromHex(this String value)
        {
            if (value == null)
                return null;
            var l = value.Length;
            if (l <= 0)
                return String.Empty;
            l >>= 1;
            byte[] rented = null;
            Span<Byte> data = l <= MaxStackBytes ? stackalloc Byte[l] : (rented = ArrayPool<Byte>.Shared.Rent(l));
            try
            {
                data = data[..l];
                DecodeHex(value.AsSpan(0, l + l), data);
                return Encoding.UTF8.GetString(data);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Byte>.Shared.Return(rented);
            }
        }


        /// <summary>
        /// Convert a hexadecimal string to it's data representation.
        /// If the number of chars are odd, the last char is ignored.
        /// </summary>
        /// <param name="value">A hexadecimal string, can only be null, empty or the characters '0' - '9', 'a' - 'f' or 'A' - 'F' (or it will throw)</param>
        /// <returns>null if input value is null, an empty array if input value is empty, else the binary data represented by the string</returns>
        /// <exception cref="Exception">The string contains a char that isn't a hex digit</exception>
        public static Byte[] ToDataFromHex(this String value)
        {
            if (value == null)
                return null;
            var l = value.Length;
            if (l <= 0)
                return Array.Empty<Byte>();
            l >>= 1;
            // The result (all bytes are written)
            var data = GC.AllocateUninitializedArray<Byte>(l);
            DecodeHex(value.AsSpan(0, l + l), data);
            return data;
        }

        /// <summary>
        /// Check if the string contains any letter (<see cref="Char.IsLetter(char)"/>)
        /// </summary>
        /// <param name="value">The string to check (null returns false)</param>
        /// <returns>True if any char is a letter</returns>
        public static bool AnyLetter(this String value)
        {
            if (value == null)
                return false;
            var v = value.AsSpan();
            // Vectorized: ASCII letters
            if (v.ContainsAny(AsciiLetters))
                return true;
            var first = v.IndexOfAnyExceptInRange((Char)0, (Char)127);
            if (first < 0)
                return false;
            var l = v.Length;
            for (int i = first; i < l; ++i)
            {
                if (Char.IsLetter(v[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Filter a string, just keeping the allowed chars
        /// </summary>
        /// <param name="value">The string to filter, may be null</param>
        /// <param name="keep">The chars to keep</param>
        /// <returns>The filtered string, the input instance if all chars are kept (or if null or empty)</returns>
        [SkipLocalsInit]
        public static String Filter(this String value, IReadOnlySet<Char> keep)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var v = value.AsSpan();
            var l = v.Length;
            int first = 0;
            while ((first < l) && keep.Contains(v[first]))
                ++first;
            if (first >= l)
                return value;
            char[] rented = null;
            Span<Char> data = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                v[..first].CopyTo(data);
                int o = first;
                for (int i = first + 1; i < l; ++i)
                {
                    var c = v[i];
                    if (!keep.Contains(c))
                        continue;
                    data[o] = c;
                    ++o;
                }
                return o == 0 ? String.Empty : new string(data[..o]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Filter a string, just keeping the allowed chars
        /// </summary>
        /// <param name="value">The string to filter, may be null</param>
        /// <param name="keepFn">A function that is called to determine if a char should be kept, return true to keep the char</param>
        /// <returns>The filtered string, the input instance if all chars are kept (or if null or empty)</returns>
        [SkipLocalsInit]
        public static String Filter(this String value, Func<Char, bool> keepFn)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var v = value.AsSpan();
            var l = v.Length;
            int first = 0;
            while ((first < l) && keepFn(v[first]))
                ++first;
            if (first >= l)
                return value;
            char[] rented = null;
            Span<Char> data = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                v[..first].CopyTo(data);
                int o = first;
                for (int i = first + 1; i < l; ++i)
                {
                    var c = v[i];
                    if (!keepFn(c))
                        continue;
                    data[o] = c;
                    ++o;
                }
                return o == 0 ? String.Empty : new string(data[..o]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Filter a string, just keeping the allowed chars
        /// </summary>
        /// <param name="value">The string to filter, may be null</param>
        /// <param name="minInclusive">The first char in the range to keep</param>
        /// <param name="maxInclusive">The last char in the range to keep</param>
        /// <returns>The filtered string, the input instance if all chars are kept (or if null or empty)</returns>
        [SkipLocalsInit]
        public static String Filter(this String value, Char minInclusive, Char maxInclusive)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var v = value.AsSpan();
            // Vectorized search for the first char to remove
            var first = v.IndexOfAnyExceptInRange(minInclusive, maxInclusive);
            if (first < 0)
                return value;
            var l = v.Length;
            char[] rented = null;
            Span<Char> data = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                v[..first].CopyTo(data);
                int o = first;
                v = v[(first + 1)..];
                for (; ; )
                {
                    // Skip chars to remove, then copy the run of chars to keep (vectorized)
                    var keepStart = v.IndexOfAnyInRange(minInclusive, maxInclusive);
                    if (keepStart < 0)
                        break;
                    v = v[keepStart..];
                    var keepEnd = v.IndexOfAnyExceptInRange(minInclusive, maxInclusive);
                    if (keepEnd < 0)
                        keepEnd = v.Length;
                    v[..keepEnd].CopyTo(data[o..]);
                    o += keepEnd;
                    v = v[keepEnd..];
                }
                return o == 0 ? String.Empty : new string(data[..o]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }



        /// <summary>
        /// Filter a string, just keeping numerical digits '0' - '9' (for parsing an unsigned integer)
        /// </summary>
        /// <param name="value">The string to filter, may be null</param>
        /// <returns>The filtered string, the input instance if all chars are kept (or if null or empty)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String FilterUInt(this String value)
            => Filter(value, '0', '9');

        /// <summary>
        /// Filter a string, just keeping numerical digits '0' - '9' and a leading '-' (for parsing a signed integer).
        /// A '-' is only kept if nothing has been kept before it, ex: "x-1-2" => "-12".
        /// </summary>
        /// <param name="value">The string to filter, may be null</param>
        /// <returns>The filtered string, the input instance if all chars are kept (or if null or empty)</returns>
        [SkipLocalsInit]
        public static String FilterInt(this String value)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var v = value.AsSpan();
            // Vectorized: only digits
            if (v.IndexOfAnyExceptInRange('0', '9') < 0)
                return value;
            var l = v.Length;
            char[] rented = null;
            Span<Char> data = l <= MaxStackChars ? stackalloc Char[l] : (rented = ArrayPool<Char>.Shared.Rent(l));
            try
            {
                int o = 0;
                for (int i = 0; i < l; ++i)
                {
                    var c = v[i];
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
                return new string(data[..o]);
            }
            finally
            {
                if (rented != null)
                    ArrayPool<Char>.Shared.Return(rented);
            }
        }


        /// <summary>
        /// Split a string into two parts (keeping the left part) on the first occurance of a char.
        /// Example:
        /// "name@example.com".SplitFirst('@') => "name"
        /// </summary>
        /// <param name="value">The value to split into two parts</param>
        /// <param name="split">The character to split</param>
        /// <param name="trimOuter">If true, the string is trimmed before splitting</param>
        /// <param name="trimInner">If true, the resulting value is trimmed on the end and the right string (if available) is trimmed on the start</param>
        /// <returns>null (or empty) if the value is null (or empty), else the left part (if the split char isn't found, the original string is returned, trimmed if trimOuter is true), semantically the same as value.Split(split)[0]</returns>
        /*public static String SplitFirst(this String value, Char split, bool trimOuter, bool trimInner = true)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            int start = 0;
            var end = value.Length;
            if (trimOuter)
            {
                while ((start < end) && Char.IsWhiteSpace(value[start]))
                    ++start;
                while ((end > start) && Char.IsWhiteSpace(value[end - 1]))
                    --end;
                if (start >= end)
                    return String.Empty;
            }
            var p = value.IndexOf(split, start);
            if (p < 0)
                return value;
            if (trimInner)
            {
                while ((p > start) && Char.IsWhiteSpace(value[p - 1]))
                    --p;
            }
            return value.Substring(start, p);
        }
        */

        public static unsafe string SplitFirst(
            this string value,
            char split,
            bool trimOuter,
            bool trimInner = true)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            fixed (char* p = value)
            {
                char* start = p;
                char* end = p + value.Length;

                if (trimOuter)
                {
                    while (start < end && char.IsWhiteSpace(*start))
                        ++start;

                    while (end > start && char.IsWhiteSpace(*(end - 1)))
                        --end;

                    if (start >= end)
                        return string.Empty;
                }

                int activeLength = (int)(end - start);

                int splitOffset = new ReadOnlySpan<char>((void*)start, activeLength).IndexOf(split);
                if (splitOffset < 0)
                    return activeLength == value.Length ? value : new string(start, 0, activeLength);

                char* splitPos = start + splitOffset;
                if (trimInner)
                {
                    while (splitPos > start && char.IsWhiteSpace(*(splitPos - 1)))
                        --splitPos;
                }
                int leftLength = (int)(splitPos - start);

                return leftLength <= 0
                    ? string.Empty
                    : new string(start, 0, leftLength);
            }
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
        /// <param name="right">The right part, null if the split char isn't found (or the value is null, empty or only white spaces when trimmed)</param>
        /// <param name="trimOuter">If true, the string is trimmed before splitting</param>
        /// <param name="trimInner">If true, the resulting value is trimmed on the end and the right string (if available) is trimmed on the start</param>
        /// <returns>null (or empty) if the value is null (or empty), else the left part (if the split char isn't found, the original string is returned, trimmed if trimOuter is true)</returns>
        /*
        public static String SplitFirst(this String value, Char split, out String right, bool trimOuter, bool trimInner = true)
        {
            if (String.IsNullOrEmpty(value))
            {
                right = null;
                return value;
            }
            int start = 0;
            var end = value.Length;
            if (trimOuter)
            {
                while ((start < end) && Char.IsWhiteSpace(value[start]))
                    ++start;
                while ((end > start) && Char.IsWhiteSpace(value[end - 1]))
                    --end;
                if (start >= end)
                {
                    right = null;
                    return String.Empty;
                }
            }
            var p = value.IndexOf(split, start);
            if (p < 0)
            {
                right = null;
                return value;
            }
            var rs = p + 1;
            if (trimInner)
            {
                while ((rs < end) && Char.IsWhiteSpace(value[rs]))
                    ++rs;
                while ((p > start) && Char.IsWhiteSpace(value[p - 1]))
                    --p;
            }
            right = value.Substring(rs, end - rs);
            return value.Substring(start, p);
        }
        */
        public static unsafe string SplitFirst(
            this string value,
            char split,
            out string right,
            bool trimOuter,
            bool trimInner = true)
        {
            right = null;
            if (string.IsNullOrEmpty(value))
                return value;
            fixed (char* p = value)
            {
                char* start = p;
                char* end = p + value.Length;

                if (trimOuter)
                {
                    while (start < end && char.IsWhiteSpace(*start))
                        ++start;

                    while (end > start && char.IsWhiteSpace(*(end - 1)))
                        --end;

                    if (start >= end)
                        return string.Empty;
                }

                int activeLength = (int)(end - start);

                int splitOffset = new ReadOnlySpan<char>((void*)start, activeLength).IndexOf(split);
                if (splitOffset < 0)
                    return activeLength == value.Length ? value : new string(start, 0, activeLength);

                char* splitPos = start + splitOffset;
                char* rightStart = splitPos + 1;

                if (trimInner)
                {
                    while (rightStart < end && char.IsWhiteSpace(*rightStart))
                        ++rightStart;

                    while (splitPos > start && char.IsWhiteSpace(*(splitPos - 1)))
                        --splitPos;
                }

                int leftLength = (int)(splitPos - start);
                int rightLength = (int)(end - rightStart);

                right = rightLength <= 0
                    ? string.Empty
                    : new string(rightStart, 0, rightLength);

                return leftLength <= 0
                    ? string.Empty
                    : new string(start, 0, leftLength);
            }
        }

        /// <summary>
        /// Split a string into two parts (keeping the left part) on the first occurance of a char.
        /// Example:
        /// "name@example.com".SplitFirst('@') => "name"
        /// </summary>
        /// <param name="value">The value to split into two parts</param>
        /// <param name="split">The character to split</param>
        /// <returns>null (or empty) if the value is null (or empty), else the left part (if the split char isn't found, the original string is returned), semantically the same as value.Split(split)[0]</returns>
        public static String SplitFirst(this String value, Char split)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var p = value.IndexOf(split);
            if (p < 0)
                return value;
            return value.Substring(0, p);
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
        /// <param name="right">The right part, null if the split char isn't found (or the value is null or empty)</param>
        /// <returns>null (or empty) if the value is null (or empty), else the left part (if the split char isn't found, the original string is returned)</returns>
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
        /// Split a string into two parts on the last occurance of a char.
        /// Example:
        /// var right = "name@example.com".SplitLast('.', out var left);
        /// left = "name@example";
        /// right = "com";
        /// </summary>
        /// <param name="value">The value to split into two parts</param>
        /// <param name="split">The character to split</param>
        /// <param name="left">The left part, null if the split char isn't found (or the value is null or empty)</param>
        /// <returns>null (or empty) if the value is null (or empty), else the right part (if the split char isn't found, the original string is returned)</returns>
        public static String SplitLast(this String value, Char split, out String left)
        {
            if (String.IsNullOrEmpty(value))
            {
                left = null;
                return value;
            }
            var p = value.LastIndexOf(split);
            if (p < 0)
            {
                left = null;
                return value;
            }
            left = value.Substring(0, p);
            return value.Substring(p + 1);
        }


        /// <summary>
        /// Split a string into two parts on the last occurance of a char.
        /// Example:
        /// var right = "name@example.com".SplitLast('.');
        /// right = "com";
        /// </summary>
        /// <param name="value">The value to split into two parts</param>
        /// <param name="split">The character to split</param>
        /// <returns>null (or empty) if the value is null (or empty), else the right part (if the split char isn't found, the original string is returned)</returns>
        public static String SplitLast(this String value, Char split)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var p = value.LastIndexOf(split);
            if (p < 0)
                return value;
            return value.Substring(p + 1);
        }


        /// <summary>
        /// Get the text before the last character.
        /// Example:
        /// var before = "www.example.com".BeforeLast('.');
        /// before = "www.example";
        /// </summary>
        /// <param name="value">The value to process</param>
        /// <param name="split">The character to split</param>
        /// <returns>null if the value is null, else the part before the last occurence of the split char (if the split char isn't found, the original string is returned)</returns>
        public static String BeforeLast(this String value, Char split)
        {
            if (String.IsNullOrEmpty(value))
                return value;
            var p = value.LastIndexOf(split);
            if (p < 0)
                return value;
            return value.Substring(0, p);
        }

        /// <summary>
        /// The state of the secure methods that fills with '*'
        /// </summary>
        struct SecureCount
        {
            public String Str;
            public int Keep;
        }

        /// <summary>
        /// The state of the secure methods that uses a prefix or suffix
        /// </summary>
        struct SecureStr
        {
            public String Str;
            public String Add;
        }

        static readonly SpanAction<Char, SecureCount> SecureEndWithCountAction = (str, c) =>
        {
            var k = c.Keep;
            c.Str.AsSpan(0, k).CopyTo(str);
            str[k..].Fill('*');
        };

        static readonly SpanAction<Char, SecureStr> SecureEndWithStrAction = (str, c) =>
        {
            var a = c.Add;
            var k = str.Length - a.Length;
            c.Str.AsSpan(0, k).CopyTo(str);
            a.AsSpan().CopyTo(str[k..]);
        };

        static readonly SpanAction<Char, SecureCount> SecureStartWithCountAction = (str, c) =>
        {
            var l = str.Length;
            var p = l - c.Keep;
            str[..p].Fill('*');
            c.Str.AsSpan(p, l - p).CopyTo(str[p..]);
        };

        static readonly SpanAction<Char, SecureStr> SecureStartWithStrAction = (str, c) =>
        {
            var l = str.Length;
            var s = c.Str;
            var a = c.Add;
            var p = a.Length;
            var k = l - p;
            a.AsSpan().CopyTo(str.Slice(0, p));
            s.AsSpan(s.Length - k).CopyTo(str.Slice(p, k));
        };



        /// <summary>
        /// Make a string "secure" by only keeping a few chars "visible".
        /// At most half the chars in the input can be kept, the rest will be replaced with *'s (or a custom suffix).
        /// Examples:
        /// "1234abcd5678".SecureEnd() => "1234********";
        /// "1234abcd5678".SecureEnd(4, "..") => "1234..";
        /// </summary>
        /// <param name="value">The value to "secure"</param>
        /// <param name="keep">The number of chars to keep (from the start), this is capped to at most half the number of chars in the input (rounded down), must not be negative</param>
        /// <param name="suffix">An optional suffix to use instead of filling with *'s (the length of the result is then keep + the suffix length)</param>
        /// <returns>The "secure" string, null if the value is null or empty</returns>
        public static String SecureEnd(this String value, int keep = 4, String suffix = null)
        {
            if (String.IsNullOrEmpty(value))
                return null;
            var l = value.Length;
            var maxKeep = l >> 1;
            if (keep > maxKeep)
                keep = maxKeep;
            if (suffix == null)
            {
                var sc = new SecureCount
                {
                    Str = value,
                    Keep = keep,
                };
                return String.Create(l, sc, SecureEndWithCountAction);
            }
            var sa = new SecureStr
            {
                Str = value,
                Add = suffix,
            };
            return String.Create(keep + suffix.Length, sa, SecureEndWithStrAction);



        }

        /// <summary>
        /// Make a string "secure" by only keeping a few chars "visible".
        /// At most half the chars in the input can be kept, the rest will be replaced with *'s (or a custom prefix).
        /// Examples:
        /// "1234abcd5678".SecureStart() => "********5678";
        /// "1234abcd5678".SecureStart(4, "..") => "..5678";
        /// </summary>
        /// <param name="value">The value to "secure"</param>
        /// <param name="keep">The number of chars to keep (from the end), this is capped to at most half the number of chars in the input (rounded down), must not be negative</param>
        /// <param name="prefix">An optional prefix to use instead of filling with *'s (the length of the result is then keep + the prefix length)</param>
        /// <returns>The "secure" string, null if the value is null or empty</returns>
        public static String SecureStart(this String value, int keep = 4, String prefix = null)
        {
            if (String.IsNullOrEmpty(value))
                return null;
            var l = value.Length;
            var maxKeep = l >> 1;
            if (keep > maxKeep)
                keep = maxKeep;
            if (prefix == null)
            {
                var sc = new SecureCount
                {
                    Str = value,
                    Keep = keep,
                };
                return String.Create(l, sc, SecureStartWithCountAction);
            }
            var sa = new SecureStr
            {
                Str = value,
                Add = prefix,
            };
            return String.Create(keep + prefix.Length, sa, SecureStartWithStrAction);
        }

        /// <summary>
        /// Decode text data and split it into lines (separated by '\n', any '\r' at the start or end of a line is removed)
        /// </summary>
        /// <param name="data">The encoded text, a byte order mark (of the encoding) is skipped</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines. Note: unless trim is true, empty lines are removed BEFORE the '\r' is removed (so a "\r\n" line is kept as an empty line)</param>
        /// <param name="encoding">optional string encoding to use, default is UTF-8</param>
        /// <returns>The lines</returns>
        public static String[] GetLines(ReadOnlySpan<Byte> data, bool trim = false, bool removeEmpty = false, Encoding encoding = null)
        {
            if (data.Length <= 0)
                return Array.Empty<String>();
            var s = encoding.GetStringWithoutBom(data);
            var opt = StringSplitOptions.None;
            if (trim)
                opt |= StringSplitOptions.TrimEntries;
            if (removeEmpty)
                opt |= StringSplitOptions.RemoveEmptyEntries;
            var lines = s.Split('\n', opt);
            if (trim)
                return lines;
            var lc = lines.Length;
            for (int i = 0; i < lc; ++i)
                lines[i] = lines[i].Trim('\r');
            return lines;
        }

        /// <summary>
        /// Split a string into lines (separated by '\n', any '\r' at the start or end of a line is removed)
        /// </summary>
        /// <param name="s">The text, may be null</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines (after removing '\r' and trimming)</param>
        /// <returns>The lines, an empty array if the text is null or empty</returns>
        public static String[] GetLines(this String s, bool trim = false, bool removeEmpty = false)
        {
            if (String.IsNullOrEmpty(s))
                return Array.Empty<String>();
            // Trimmed, or no '\r' to remove: String.Split (vectorized) gives the result
            if (trim)
                return s.Split('\n', removeEmpty ? (StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : StringSplitOptions.TrimEntries);
            if (!s.Contains('\r'))
                return s.Split('\n', removeEmpty ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
            // Remove any '\r' at the start or end of every line (one allocation per line)
            var span = s.AsSpan();
            var lines = new String[span.Count('\n') + 1];
            int n = 0;
            for (; ; )
            {
                var i = span.IndexOf('\n');
                var line = (i < 0 ? span : span.Slice(0, i)).Trim('\r');
                if ((line.Length > 0) || !removeEmpty)
                    lines[n++] = line.Length == s.Length ? s : new String(line);
                if (i < 0)
                    break;
                span = span.Slice(i + 1);
            }
            if (n < lines.Length)
                Array.Resize(ref lines, n);
            return lines;
        }


        /// <summary>
        /// Split chars into lines, the same result as <see cref="GetLines(String, bool, bool)"/> (only the lines and the returned array are allocated)
        /// </summary>
        /// <param name="s">The chars</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines (after removing '\r' and trimming)</param>
        /// <returns>The lines</returns>
        [SkipLocalsInit]
        internal static String[] GetLines(ReadOnlySpan<Char> s, bool trim, bool removeEmpty)
        {
            if (s.IsEmpty)
                return Array.Empty<String>();
            // The index of every '\n', found in one vectorized pass (cheaper than one IndexOf call per line for short lines)
            Span<int> stackEnds = stackalloc int[MaxStackLineEnds];
            int[] rentedEnds = null;
            var ends = stackEnds;
            int count = 0;
            var l = s.Length;
            ref var c = ref Unsafe.As<Char, ushort>(ref MemoryMarshal.GetReference(s));
            nuint i = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                var nlv = Vector128.Create((ushort)'\n');
                for (; i + 8 <= (nuint)l; i += 8)
                {
                    var m = Vector128.Equals(Vector128.LoadUnsafe(ref c, i), nlv).ExtractMostSignificantBits();
                    while (m != 0)
                    {
                        if (count == ends.Length)
                            ends = GrowLineEnds(ref rentedEnds, ends, count);
                        ends[count++] = (int)i + BitOperations.TrailingZeroCount(m);
                        m &= m - 1;
                    }
                }
            }
            for (; i < (nuint)l; ++i)
            {
                if (Unsafe.Add(ref c, i) == '\n')
                {
                    if (count == ends.Length)
                        ends = GrowLineEnds(ref rentedEnds, ends, count);
                    ends[count++] = (int)i;
                }
            }
            try
            {
                var total = count + 1;
                if (!removeEmpty)
                {
                    // Every line is kept, the number of lines is known
                    var all = new String[total];
                    int st = 0;
                    for (int k = 0; k < total; ++k)
                    {
                        var end = k < count ? ends[k] : l;
                        var seg = s[st..end];
                        // Trimming white space also removes '\r' (like String.Split with TrimEntries), else only '\r' is removed
                        all[k] = (trim ? seg.Trim() : seg.Trim('\r')).ToString();
                        st = end + 1;
                    }
                    return all;
                }
                // The lines are collected in a pooled array, so that the returned array has the exact size (one allocation, even if empty lines are removed)
                var pool = ArrayPool<String>.Shared;
                var lines = pool.Rent(total);
                try
                {
                    int n = 0;
                    int start = 0;
                    for (int k = 0; k < total; ++k)
                    {
                        var end = k < count ? ends[k] : l;
                        var seg = s[start..end];
                        var line = trim ? seg.Trim() : seg.Trim('\r');
                        if (line.Length > 0)
                            lines[n++] = line.ToString();
                        start = end + 1;
                    }
                    if (n <= 0)
                        return Array.Empty<String>();
                    var result = new String[n];
                    Array.Copy(lines, result, n);
                    return result;
                }
                finally
                {
                    pool.Return(lines, true);
                }
            }
            finally
            {
                if (rentedEnds != null)
                    ArrayPool<int>.Shared.Return(rentedEnds);
            }
        }

        /// <summary>
        /// Line ends up to this number are kept on the stack (1 KB)
        /// </summary>
        const int MaxStackLineEnds = 256;

        /// <summary>
        /// Double the capacity of the line end buffer (rented from the shared pool, the previously rented buffer is returned)
        /// </summary>
        static Span<int> GrowLineEnds(ref int[] rented, Span<int> ends, int count)
        {
            var n = ArrayPool<int>.Shared.Rent(count * 2);
            ends.Slice(0, count).CopyTo(n);
            if (rented != null)
                ArrayPool<int>.Shared.Return(rented);
            rented = n;
            return n;
        }

        /// <summary>
        /// Create a new string with a repeated string
        /// </summary>
        /// <param name="part">The string to repeat, ex: "Hello"</param>
        /// <param name="count">The number of times to repeat the string, ex: 3 (must not be negative)</param>
        /// <returns>A repeated string, ex: "HelloHelloHello"</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String Create(String part, int count)
            => String.Create(part.Length * count, part, CreateCountAction);

        static readonly SpanAction<Char, ReadOnlySpan<Char>> CreateCountAction = (str, c) =>
        {
            var ol = str.Length;
            if (ol <= 0)
                return;
            // Copy the part once, then double the written chars until the string is filled
            c.CopyTo(str);
            var filled = c.Length;
            while (filled < ol)
            {
                var n = Math.Min(filled, ol - filled);
                str[..n].CopyTo(str[filled..]);
                filled += n;
            }
        };




        /// <summary>
        /// The chars that are escaped (with a '\') by <see cref="EscapeMD(string, bool)"/>
        /// </summary>
        static readonly SearchValues<Char> EscapeChars = SearchValues.Create(
            [
                '\\',
                '`',
                '*',
                '_',
                '&',
                '{',
                '}',
                '[',
                ']',
                '<',
                '>',
                '(',
                ')',
                '#',
                '$',
                //                '+',
                //                '-',
                //                '.',
                '!',
                '|'
            ]);

        /// <summary>
        /// Escape some text to work inside mark down, by prefixing \ ` * _ &amp; { } [ ] &lt; &gt; ( ) # $ ! | with a '\'.
        /// Doesn't escape +, - and .
        /// A text that starts with the char 1 is considered to be markdown already, it's returned as is (without the first char).
        /// </summary>
        /// <param name="text">The text to escape</param>
        /// <param name="nbsp">If true, any spaces are converted to non breaking spaces to prevent word wrapping, if false any non breaking spaces are converted to spaces</param>
        /// <returns>Escaped text, an empty string if the text is null or empty, the input instance if nothing changed</returns>
        public static String EscapeMD(String text, bool nbsp = false)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            if (text[0] == (Char)1)
                return text.Substring(1);
            var s = text.AsSpan();
            // Find the first char to escape (vectorized search, most texts doesn't have any)
            var first = s.IndexOfAny(EscapeChars);
            var from = nbsp ? ' ' : (Char)0xa0;
            if (first < 0)
            {
                if (!s.Contains(from))
                    return text;
                first = s.Length;
            }
            // Count the chars to escape (escape chars are often frequent, a per char lookup is faster than restarting a vectorized search)
            var e = EscapeChars;
            int escapes = 0;
            var l = s.Length;
            for (int i = first; i < l; ++i)
            {
                if (e.Contains(s[i]))
                    ++escapes;
            }
            // Escape and replace in a single pass, only the result is allocated
            return String.Create(l + escapes, (text, nbsp, first), EscapeMDAction);
        }

        static readonly SpanAction<Char, (String Text, bool Nbsp, int First)> EscapeMDAction = (d, st) =>
        {
            var s = st.Text.AsSpan();
            var from = st.Nbsp ? ' ' : (Char)0xa0;
            var to = st.Nbsp ? (Char)0xa0 : ' ';
            var first = st.First;
            // Before the first escaped char, only replace
            s[..first].CopyTo(d);
            d[..first].Replace(from, to);
            var e = EscapeChars;
            var l = s.Length;
            int o = first;
            for (int i = first; i < l; ++i)
            {
                var c = s[i];
                if (e.Contains(c))
                {
                    d[o] = '\\';
                    ++o;
                }
                else if (c == from)
                {
                    c = to;
                }
                d[o] = c;
                ++o;
            }
        };


    }





}
