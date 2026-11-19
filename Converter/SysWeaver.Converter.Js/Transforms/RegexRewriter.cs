using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Information about a regular expression pattern (from a light weight scan)
    /// </summary>
    sealed class RegexInfo
    {
        /// <summary>Named groups (name to group number), in order</summary>
        public readonly List<KeyValuePair<String, int>> Names = new List<KeyValuePair<String, int>>();
        public bool HasDuplicateNames;
        public bool HasLookbehind;
        public bool HasModifiers;
        public bool HasUnicodeProperty;
        public int GroupCount;

        public int IndexOf(String name)
        {
            foreach (var kv in Names)
                if (kv.Key == name)
                    return kv.Value;
            return -1;
        }
    }

    /// <summary>
    /// Regular expression pattern scanning and rewriting
    /// </summary>
    static class RegexRewriter
    {
        /// <summary>
        /// Scan a pattern
        /// </summary>
        public static RegexInfo Scan(String p, bool unicode)
        {
            var r = new RegexInfo();
            var seen = new HashSet<String>(StringComparer.Ordinal);
            int l = p.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = p[i];
                if (c == '\\')
                {
                    if (i + 1 < l && (p[i + 1] == 'p' || p[i + 1] == 'P') && unicode)
                        r.HasUnicodeProperty = true;
                    ++i;
                    continue;
                }
                if (c == '[')
                {
                    i = SkipClass(p, i, unicode, r);
                    continue;
                }
                if (c != '(')
                    continue;
                if (i + 1 < l && p[i + 1] == '?')
                {
                    if (i + 2 < l && p[i + 2] == '<' && i + 3 < l && (p[i + 3] == '=' || p[i + 3] == '!'))
                    {
                        r.HasLookbehind = true;
                        continue;
                    }
                    if (i + 2 < l && p[i + 2] == '<')
                    {
                        var e = p.IndexOf('>', i + 3);
                        if (e < 0)
                            continue;
                        var name = p.Substring(i + 3, e - i - 3);
                        ++r.GroupCount;
                        if (!seen.Add(name))
                            r.HasDuplicateNames = true;
                        r.Names.Add(new KeyValuePair<String, int>(name, r.GroupCount));
                        continue;
                    }
                    if (i + 2 < l && (p[i + 2] == 'i' || p[i + 2] == 'm' || p[i + 2] == 's' || p[i + 2] == '-'))
                        r.HasModifiers = true;
                    continue;
                }
                ++r.GroupCount;
            }
            return r;
        }

        /// <summary>
        /// Skip a character class, returns the index of the closing ]
        /// </summary>
        static int SkipClass(String p, int i, bool unicode, RegexInfo info)
        {
            int l = p.Length;
            ++i;
            for (; i < l; ++i)
            {
                var c = p[i];
                if (c == '\\')
                {
                    if (info != null && unicode && i + 1 < l && (p[i + 1] == 'p' || p[i + 1] == 'P'))
                        info.HasUnicodeProperty = true;
                    ++i;
                    continue;
                }
                if (c == ']')
                    return i;
            }
            return l;
        }

        /// <summary>
        /// Rewrite a pattern without lowering the u flag: dotAll and / or named groups
        /// </summary>
        public static String LowerSimple(String p, bool unicode, bool lowerDot, bool lowerNamed, RegexInfo info)
        {
            var sb = new StringBuilder(p.Length + 16);
            int l = p.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = p[i];
                if (c == '\\')
                {
                    if (lowerNamed && info.Names.Count > 0 && i + 2 < l && p[i + 1] == 'k' && p[i + 2] == '<')
                    {
                        var e = p.IndexOf('>', i + 3);
                        if (e > 0)
                        {
                            var idx = info.IndexOf(p.Substring(i + 3, e - i - 3));
                            if (idx > 0)
                            {
                                sb.Append("(?:\\").Append(idx.ToString(CultureInfo.InvariantCulture)).Append(')');
                                i = e;
                                continue;
                            }
                        }
                    }
                    sb.Append(c);
                    if (i + 1 < l)
                        sb.Append(p[++i]);
                    continue;
                }
                if (c == '[')
                {
                    var e = SkipClass(p, i, unicode, null);
                    sb.Append(p, i, Math.Min(e + 1, l) - i);
                    i = e;
                    continue;
                }
                if (c == '.' && lowerDot)
                {
                    sb.Append("[\\s\\S]");
                    continue;
                }
                if (c == '(' && lowerNamed && i + 2 < l && p[i + 1] == '?' && p[i + 2] == '<' && i + 3 < l && p[i + 3] != '=' && p[i + 3] != '!')
                {
                    var e = p.IndexOf('>', i + 3);
                    if (e > 0)
                    {
                        sb.Append('(');
                        i = e;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Replace $&lt;name&gt; in a replacement string with $N
        /// </summary>
        public static String RewriteReplacement(String s, RegexInfo info)
        {
            var sb = new StringBuilder(s.Length);
            int l = s.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (c == '$' && i + 1 < l)
                {
                    var n = s[i + 1];
                    if (n == '$')
                    {
                        sb.Append("$$");
                        ++i;
                        continue;
                    }
                    if (n == '<')
                    {
                        var e = s.IndexOf('>', i + 2);
                        if (e > 0)
                        {
                            var idx = info.IndexOf(s.Substring(i + 2, e - i - 2));
                            //  An unknown name is replaced with an empty string
                            if (idx > 0)
                                sb.Append('$').Append(idx.ToString(CultureInfo.InvariantCulture));
                            i = e;
                            continue;
                        }
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Lowers a pattern with the u flag to a pattern without it (like regexpu)
    /// </summary>
    sealed class UnicodeRegexLowering
    {
        const int MaxCp = 0x10FFFF;

        readonly String P;
        int I;
        readonly bool DotAll;
        readonly bool LowerNamed;
        readonly bool IgnoreCase;
        readonly RegexInfo Info;
        int Groups;

        /// <summary>True if case insensitive matching of non-ASCII characters might behave differently</summary>
        public bool CaseWarning;

        UnicodeRegexLowering(String p, bool dotAll, bool lowerNamed, bool ignoreCase, RegexInfo info)
        {
            P = p;
            DotAll = dotAll;
            LowerNamed = lowerNamed;
            IgnoreCase = ignoreCase;
            Info = info;
        }

        /// <summary>
        /// Lower a pattern, throws FormatException if the pattern can't be parsed
        /// </summary>
        public static String Lower(String pattern, bool dotAll, bool lowerNamed, bool ignoreCase, RegexInfo info, out bool caseWarning)
        {
            var x = new UnicodeRegexLowering(pattern, dotAll, lowerNamed, ignoreCase, info);
            var sb = new StringBuilder();
            x.Disjunction(sb);
            if (x.I < pattern.Length)
                throw new FormatException("Unexpected ')' at " + x.I);
            caseWarning = x.CaseWarning;
            return sb.ToString();
        }

        #region Sets

        /// <summary>A set of code points as sorted, non overlapping ranges</summary>
        sealed class CpSet
        {
            public readonly List<(int Lo, int Hi)> R = new List<(int, int)>();

            public void Add(int lo, int hi)
            {
                R.Add((lo, hi));
            }

            public void AddSet(CpSet s) => R.AddRange(s.R);

            public CpSet Normalize()
            {
                R.Sort((a, b) => a.Lo.CompareTo(b.Lo));
                var n = new List<(int, int)>();
                foreach (var x in R)
                {
                    if (n.Count > 0 && x.Lo <= n[n.Count - 1].Item2 + 1)
                    {
                        var last = n[n.Count - 1];
                        n[n.Count - 1] = (last.Item1, Math.Max(last.Item2, x.Hi));
                    }
                    else
                        n.Add(x);
                }
                R.Clear();
                R.AddRange(n);
                return this;
            }

            public CpSet Complement()
            {
                Normalize();
                var c = new CpSet();
                int next = 0;
                foreach (var x in R)
                {
                    if (x.Lo > next)
                        c.Add(next, x.Lo - 1);
                    next = x.Hi + 1;
                }
                if (next <= MaxCp)
                    c.Add(next, MaxCp);
                return c;
            }

            public CpSet Intersect(int lo, int hi)
            {
                var c = new CpSet();
                foreach (var x in R)
                {
                    var a = Math.Max(lo, x.Lo);
                    var b = Math.Min(hi, x.Hi);
                    if (a <= b)
                        c.Add(a, b);
                }
                return c;
            }
        }

        static CpSet Digits()
        {
            var s = new CpSet();
            s.Add('0', '9');
            return s;
        }

        static CpSet Word()
        {
            var s = new CpSet();
            s.Add('0', '9');
            s.Add('A', 'Z');
            s.Add('_', '_');
            s.Add('a', 'z');
            return s;
        }

        static CpSet Space()
        {
            var s = new CpSet();
            s.Add(9, 13);
            s.Add(0x20, 0x20);
            s.Add(0xA0, 0xA0);
            s.Add(0x1680, 0x1680);
            s.Add(0x2000, 0x200A);
            s.Add(0x2028, 0x2029);
            s.Add(0x202F, 0x202F);
            s.Add(0x205F, 0x205F);
            s.Add(0x3000, 0x3000);
            s.Add(0xFEFF, 0xFEFF);
            return s;
        }

        static CpSet EscapeSet(char c)
        {
            var s = Char.ToLowerInvariant(c) switch
            {
                'd' => Digits(),
                'w' => Word(),
                _ => Space(),
            };
            return Char.IsUpper(c) ? s.Complement() : s.Normalize();
        }

        #endregion//Sets

        #region Output

        static String Hex(int c) => "\\u" + c.ToString("X4", CultureInfo.InvariantCulture);

        static bool NeedsHex(int c) => c < 0x20 || c >= 0x7F;

        /// <summary>A BMP code point as an atom (outside a class)</summary>
        static String AtomChar(int c)
        {
            if (NeedsHex(c))
                return Hex(c);
            if ("^$\\.*+?()[]{}|/".IndexOf((char)c) >= 0)
                return "\\" + (char)c;
            return ((char)c).ToString();
        }

        /// <summary>A BMP code point inside a class</summary>
        static String ClassChar(int c)
        {
            if (NeedsHex(c))
                return Hex(c);
            if ("\\]^-[/".IndexOf((char)c) >= 0)
                return "\\" + (char)c;
            return ((char)c).ToString();
        }

        static String ClassText(CpSet s)
        {
            if (s.R.Count == 1 && s.R[0].Lo == s.R[0].Hi)
                return AtomChar(s.R[0].Lo);
            var sb = new StringBuilder("[");
            foreach (var (lo, hi) in s.R)
            {
                sb.Append(ClassChar(lo));
                if (hi == lo)
                    continue;
                if (hi > lo + 1)
                    sb.Append('-');
                sb.Append(ClassChar(hi));
            }
            return sb.Append(']').ToString();
        }

        static String RangeText(int lo, int hi) => lo == hi ? Hex(lo) : ("[" + Hex(lo) + "-" + Hex(hi) + "]");

        /// <summary>
        /// Emit a set, returns true if the output is a single atom (can be quantified)
        /// </summary>
        bool EmitSet(StringBuilder sb, CpSet s)
        {
            s.Normalize();
            CheckCase(s);
            var parts = new List<String>();
            var bmp = s.Intersect(0, 0xD7FF);
            bmp.AddSet(s.Intersect(0xE000, 0xFFFF));
            bmp.Normalize();
            if (bmp.R.Count > 0)
                parts.Add(ClassText(bmp));
            //  Astral ranges as surrogate pairs, grouped by high surrogate
            var astral = new List<(int HiLo, int HiHi, int LoLo, int LoHi)>();
            foreach (var (lo, hi) in s.Intersect(0x10000, MaxCp).R)
            {
                int h1 = ((lo - 0x10000) >> 10) + 0xD800, l1 = ((lo - 0x10000) & 0x3FF) + 0xDC00;
                int h2 = ((hi - 0x10000) >> 10) + 0xD800, l2 = ((hi - 0x10000) & 0x3FF) + 0xDC00;
                if (h1 == h2)
                    astral.Add((h1, h1, l1, l2));
                else
                {
                    astral.Add((h1, h1, l1, 0xDFFF));
                    if (h1 + 1 <= h2 - 1)
                        astral.Add((h1 + 1, h2 - 1, 0xDC00, 0xDFFF));
                    astral.Add((h2, h2, 0xDC00, l2));
                }
            }
            //  Merge adjacent high surrogates with the same low range
            for (int i = 1; i < astral.Count; ++i)
            {
                var a = astral[i - 1];
                var b = astral[i];
                if (a.LoLo == b.LoLo && a.LoHi == b.LoHi && a.HiHi + 1 == b.HiLo)
                {
                    astral[i - 1] = (a.HiLo, b.HiHi, a.LoLo, a.LoHi);
                    astral.RemoveAt(i);
                    --i;
                }
            }
            foreach (var a in astral)
                parts.Add(RangeText(a.HiLo, a.HiHi) + RangeText(a.LoLo, a.LoHi));
            //  Lone surrogates
            var hs = s.Intersect(0xD800, 0xDBFF);
            if (hs.R.Count > 0)
                parts.Add(ClassText(hs) + "(?![\\uDC00-\\uDFFF])");
            var ls = s.Intersect(0xDC00, 0xDFFF);
            if (ls.R.Count > 0)
                parts.Add("(?:[^\\uD800-\\uDBFF]|^)" + ClassText(ls));
            if (parts.Count == 0)
            {
                sb.Append("[]");
                return true;
            }
            if (parts.Count == 1 && bmp.R.Count > 0)
            {
                sb.Append(parts[0]);
                return true;
            }
            sb.Append("(?:").Append(String.Join("|", parts)).Append(')');
            return true;
        }

        void CheckCase(CpSet s)
        {
            if (!IgnoreCase || CaseWarning)
                return;
            foreach (var (lo, hi) in s.R)
            {
                //  Sets that cover (almost) everything don't matter
                if (hi - lo > 0x8000)
                    continue;
                if (hi > 0x7F)
                {
                    for (int c = Math.Max(lo, 0x80); c <= Math.Min(hi, 0xFFFF); ++c)
                    {
                        if (Char.IsLetter((char)c))
                        {
                            CaseWarning = true;
                            return;
                        }
                    }
                    if (hi > 0xFFFF)
                    {
                        CaseWarning = true;
                        return;
                    }
                }
                //  ASCII letters that have non-ASCII case variants in unicode mode (k: Kelvin sign, s: long s)
                if (lo <= 'k' && hi >= 'k' || lo <= 'K' && hi >= 'K' || lo <= 's' && hi >= 's' || lo <= 'S' && hi >= 'S')
                {
                    //  Only a problem if the input contains U+212A or U+017F, too rare to warn about
                }
            }
        }

        #endregion//Output

        #region Parser

        bool End => I >= P.Length;
        char Cur => P[I];

        FormatException Err(String m) => new FormatException(m + " at " + I);

        void Disjunction(StringBuilder sb)
        {
            Alternative(sb);
            while (!End && Cur == '|')
            {
                ++I;
                sb.Append('|');
                Alternative(sb);
            }
        }

        void Alternative(StringBuilder sb)
        {
            while (!End && Cur != '|' && Cur != ')')
                Term(sb);
        }

        int ReadCodePoint()
        {
            var c = P[I++];
            if (Char.IsHighSurrogate(c) && I < P.Length && Char.IsLowSurrogate(P[I]))
                return Char.ConvertToUtf32(c, P[I++]);
            return c;
        }

        static int HexVal(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        int ReadHex(int count)
        {
            int v = 0;
            for (int i = 0; i < count; ++i)
            {
                if (End)
                    throw Err("Invalid escape");
                var h = HexVal(Cur);
                if (h < 0)
                    throw Err("Invalid escape");
                v = v * 16 + h;
                ++I;
            }
            return v;
        }

        /// <summary>
        /// Parse \u escapes (after the u), combines surrogate pairs
        /// </summary>
        int ReadUnicodeEscape()
        {
            if (!End && Cur == '{')
            {
                ++I;
                int v = 0;
                int n = 0;
                while (!End && Cur != '}')
                {
                    var h = HexVal(Cur);
                    if (h < 0)
                        throw Err("Invalid escape");
                    v = v * 16 + h;
                    if (v > MaxCp)
                        throw Err("Invalid code point");
                    ++I;
                    ++n;
                }
                if (End || n == 0)
                    throw Err("Invalid escape");
                ++I;
                return v;
            }
            var c = ReadHex(4);
            if (c >= 0xD800 && c <= 0xDBFF && I + 5 < P.Length + 0 && P[I] == '\\' && P[I + 1] == 'u')
            {
                var save = I;
                I += 2;
                if (!End && Cur != '{')
                {
                    try
                    {
                        var lo = ReadHex(4);
                        if (lo >= 0xDC00 && lo <= 0xDFFF)
                            return Char.ConvertToUtf32((char)c, (char)lo);
                    }
                    catch (FormatException)
                    {
                    }
                }
                I = save;
            }
            return c;
        }

        /// <summary>
        /// Parse an escape (after the backslash) that is a character or a set (\d etc), returns -1 for a set
        /// </summary>
        int CharEscape(bool inClass, out CpSet set)
        {
            set = null;
            if (End)
                throw Err("Invalid escape");
            var c = P[I++];
            switch (c)
            {
                case 'd':
                case 'D':
                case 'w':
                case 'W':
                case 's':
                case 'S':
                    set = EscapeSet(c);
                    return -1;
                case 'p':
                case 'P':
                    throw Err("Unicode property escapes are not supported");
                case 't':
                    return 9;
                case 'n':
                    return 10;
                case 'v':
                    return 11;
                case 'f':
                    return 12;
                case 'r':
                    return 13;
                case 'b':
                    if (inClass)
                        return 8;
                    throw Err("Unexpected \\b");
                case 'c':
                    {
                        if (End || !((Cur >= 'a' && Cur <= 'z') || (Cur >= 'A' && Cur <= 'Z')))
                            throw Err("Invalid \\c escape");
                        return P[I++] % 32;
                    }
                case '0':
                    if (!End && Cur >= '0' && Cur <= '9')
                        throw Err("Invalid octal escape");
                    return 0;
                case 'x':
                    return ReadHex(2);
                case 'u':
                    return ReadUnicodeEscape();
                case '-':
                    return '-';
            }
            if ("^$\\.*+?()[]{}|/".IndexOf(c) >= 0)
                return c;
            throw Err("Invalid escape \\" + c);
        }

        void Term(StringBuilder sb)
        {
            var c = Cur;
            int start = sb.Length;
            bool quantifiable = true;
            bool multiUnit = false;
            switch (c)
            {
                case '^':
                case '$':
                    ++I;
                    sb.Append(c);
                    quantifiable = false;
                    break;
                case '(':
                    quantifiable = Group(sb);
                    break;
                case '.':
                    {
                        ++I;
                        CpSet s;
                        if (DotAll)
                        {
                            s = new CpSet();
                            s.Add(0, MaxCp);
                        }
                        else
                        {
                            var lt = new CpSet();
                            lt.Add(10, 10);
                            lt.Add(13, 13);
                            lt.Add(0x2028, 0x2029);
                            s = lt.Complement();
                        }
                        EmitSet(sb, s);
                        break;
                    }
                case '[':
                    EmitSet(sb, Class());
                    break;
                case '\\':
                    {
                        ++I;
                        if (End)
                            throw Err("Trailing backslash");
                        var e = Cur;
                        if (e == 'b' || e == 'B')
                        {
                            ++I;
                            sb.Append('\\').Append(e);
                            quantifiable = false;
                            break;
                        }
                        if (e >= '1' && e <= '9')
                        {
                            int n = 0;
                            while (!End && Cur >= '0' && Cur <= '9')
                                n = n * 10 + (P[I++] - '0');
                            sb.Append("(?:\\").Append(n.ToString(CultureInfo.InvariantCulture)).Append(')');
                            break;
                        }
                        if (e == 'k')
                        {
                            ++I;
                            if (End || Cur != '<')
                                throw Err("Invalid named reference");
                            var close = P.IndexOf('>', I);
                            if (close < 0)
                                throw Err("Invalid named reference");
                            var name = P.Substring(I + 1, close - I - 1);
                            I = close + 1;
                            if (LowerNamed)
                            {
                                var idx = Info.IndexOf(name);
                                if (idx < 0)
                                    throw Err("Unknown group name " + name);
                                sb.Append("(?:\\").Append(idx.ToString(CultureInfo.InvariantCulture)).Append(')');
                            }
                            else
                                sb.Append("\\k<").Append(name).Append('>');
                            break;
                        }
                        var cp = CharEscape(false, out var set);
                        if (set != null)
                            EmitSet(sb, set);
                        else
                            multiUnit = EmitChar(sb, cp);
                        break;
                    }
                case '*':
                case '+':
                case '?':
                case '{':
                    throw Err("Nothing to repeat");
                case ']':
                case '}':
                    throw Err("Lone quantifier bracket");
                default:
                    multiUnit = EmitChar(sb, ReadCodePoint());
                    break;
            }
            //  Quantifier
            if (End)
                return;
            int qs = I;
            var q = Cur;
            if (q == '*' || q == '+' || q == '?')
                ++I;
            else if (q == '{')
            {
                int j = I + 1;
                int digits = 0;
                while (j < P.Length && Char.IsAsciiDigit(P[j]))
                {
                    ++j;
                    ++digits;
                }
                if (digits == 0)
                    throw Err("Incomplete quantifier");
                if (j < P.Length && P[j] == ',')
                {
                    ++j;
                    while (j < P.Length && Char.IsAsciiDigit(P[j]))
                        ++j;
                }
                if (j >= P.Length || P[j] != '}')
                    throw Err("Incomplete quantifier");
                I = j + 1;
            }
            else
                return;
            if (!End && Cur == '?')
                ++I;
            if (!quantifiable)
                throw Err("Nothing to repeat");
            if (multiUnit)
            {
                sb.Insert(start, "(?:");
                sb.Append(')');
            }
            sb.Append(P, qs, I - qs);
        }

        /// <summary>
        /// Emit a code point, returns true if it was written as more than one atom (surrogate pair)
        /// </summary>
        bool EmitChar(StringBuilder sb, int cp)
        {
            if (cp > 0xFFFF)
            {
                if (IgnoreCase)
                    CaseWarning = true;
                var s = Char.ConvertFromUtf32(cp);
                sb.Append(Hex(s[0])).Append(Hex(s[1]));
                return true;
            }
            if (cp >= 0xD800 && cp <= 0xDFFF)
            {
                var set = new CpSet();
                set.Add(cp, cp);
                EmitSet(sb, set);
                return false;
            }
            if (IgnoreCase && cp > 0x7F && Char.IsLetter((char)cp))
                CaseWarning = true;
            sb.Append(AtomChar(cp));
            return false;
        }

        /// <summary>
        /// Parse a group, returns true if it can be quantified
        /// </summary>
        bool Group(StringBuilder sb)
        {
            ++I;
            bool quantifiable = true;
            if (!End && Cur == '?')
            {
                ++I;
                if (End)
                    throw Err("Invalid group");
                var k = Cur;
                if (k == ':' || k == '=' || k == '!')
                {
                    ++I;
                    sb.Append("(?").Append(k);
                    quantifiable = k == ':';
                }
                else if (k == '<' && I + 1 < P.Length && (P[I + 1] == '=' || P[I + 1] == '!'))
                {
                    sb.Append("(?<").Append(P[I + 1]);
                    I += 2;
                    quantifiable = false;
                }
                else if (k == '<')
                {
                    var close = P.IndexOf('>', I);
                    if (close < 0)
                        throw Err("Invalid group name");
                    var name = P.Substring(I + 1, close - I - 1);
                    I = close + 1;
                    ++Groups;
                    if (LowerNamed)
                        sb.Append('(');
                    else
                        sb.Append("(?<").Append(name).Append('>');
                }
                else
                {
                    //  Modifiers: (?ims-ims:
                    var close = P.IndexOf(':', I);
                    if (close < 0)
                        throw Err("Invalid group");
                    sb.Append("(?").Append(P, I, close - I + 1);
                    I = close + 1;
                }
            }
            else
            {
                ++Groups;
                sb.Append('(');
            }
            Disjunction(sb);
            if (End || Cur != ')')
                throw Err("Unterminated group");
            ++I;
            sb.Append(')');
            return quantifiable;
        }

        CpSet Class()
        {
            ++I;
            bool negate = false;
            if (!End && Cur == '^')
            {
                negate = true;
                ++I;
            }
            var s = new CpSet();
            while (true)
            {
                if (End)
                    throw Err("Unterminated character class");
                if (Cur == ']')
                {
                    ++I;
                    break;
                }
                var a = ClassAtom(out var aset);
                if (aset != null)
                {
                    s.AddSet(aset);
                    if (!End && Cur == '-' && I + 1 < P.Length && P[I + 1] != ']')
                        throw Err("Invalid character class range");
                    continue;
                }
                if (!End && Cur == '-' && I + 1 < P.Length && P[I + 1] != ']')
                {
                    ++I;
                    var b = ClassAtom(out var bset);
                    if (bset != null)
                        throw Err("Invalid character class range");
                    if (b < a)
                        throw Err("Range out of order in character class");
                    s.Add(a, b);
                    continue;
                }
                s.Add(a, a);
            }
            s.Normalize();
            return negate ? s.Complement() : s;
        }

        int ClassAtom(out CpSet set)
        {
            set = null;
            if (Cur == '\\')
            {
                ++I;
                return CharEscape(true, out set);
            }
            return ReadCodePoint();
        }

        #endregion//Parser
    }
}
