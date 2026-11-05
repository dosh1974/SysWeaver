using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SysWeaver.AI
{
    /// <summary>
    /// Rule based minimizer of English LLM prompts, see <see cref="AiTools.MinimizePrompt(string)"/>.
    ///
    /// Works in three steps:
    /// 1. Everything that must survive verbatim (code, quotes, templates, tags, identifiers, urls, examples..) is swapped for an opaque slot.
    /// 2. The remaining prose is rewritten by a set of rules, that are all written to fail safe, when in doubt the text is left as is.
    /// 3. The slots are swapped back.
    ///
    /// Things that are deliberately never removed, since doing so can change what the LLM is told to do:
    /// - Negations and exceptions (not, never, no, without, except, unless..).
    /// - Modals and conditions (must, should, may, only, always, if, when..).
    /// - Words that restrict or are manner of a command (just, only, very, clearly, simply at the end of a sentence, literally, honestly..).
    /// - UPPERCASE words (emphasis).
    ///
    /// Rewrites target tokens rather than chars, so abbreviations (e.g. "for example" to "e.g.") and vowel dropping is not used, as they usually tokenize worse.
    /// This is a separate class so that the compiled regular expressions aren't created until they are needed.
    /// </summary>
    internal static class AiPromptMinimizer
    {
        /// <summary>
        /// Minimize a prompt, see <see cref="AiTools.MinimizePrompt(string)"/>
        /// </summary>
        public static String Minimize(String prompt)
        {
            if (String.IsNullOrEmpty(prompt))
                return prompt;
            //  The internal markers can't be told apart from the input if it contains them, extremely unlikely so just leave such a prompt as is
            if (prompt.AsSpan().IndexOfAnyInRange(SlotStart, CapNext) >= 0)
                return prompt;
            var text = Dedent(prompt.Replace("\r\n", "\n").Replace('\r', '\n'));
            var slots = new Slots();
            var prose = ProtectInline(ProtectBlocks(text, slots), slots);
            var res = slots.Restore(RewriteProse(prose));
            //  Everything was filler (ex: "Hello!"), an empty prompt helps no one
            return res.Length > 0 ? res : text.Trim();
        }

        #region Slots

        /// <summary>
        /// Start of a slot, followed by the decimal slot index and then SlotEnd.
        /// Uses the unicode private use area, the chars are not regarded as word chars by the regular expressions.
        /// </summary>
        const Char SlotStart = '';
        const Char SlotEnd = '';

        /// <summary>
        /// Inserted when a sentence start was removed, the next letter should be capitalized
        /// </summary>
        const Char CapNext = '';

        /// <summary>
        /// Stores protected text. Identical text share a slot, so that repeated sentences can be detected even if they contains protected text.
        /// </summary>
        sealed class Slots
        {
            readonly List<String> Texts = new();
            readonly Dictionary<String, int> Lookup = new(StringComparer.Ordinal);

            public String Add(String text)
            {
                if (!Lookup.TryGetValue(text, out var index))
                {
                    index = Texts.Count;
                    Texts.Add(text);
                    Lookup.Add(text, index);
                }
                return String.Concat(SlotStart.ToString(), index.ToString(), SlotEnd.ToString());
            }

            public String Restore(String text)
            {
                var sb = new StringBuilder(text.Length * 2);
                Restore(sb, text);
                return sb.ToString();
            }

            /// <summary>
            /// A slot can contain other slots (a token containing inline code), the nesting is finite since a slot can only reference previous slots
            /// </summary>
            void Restore(StringBuilder sb, String text)
            {
                int pos = 0;
                for (; ; )
                {
                    var start = text.IndexOf(SlotStart, pos);
                    if (start < 0)
                    {
                        sb.Append(text, pos, text.Length - pos);
                        return;
                    }
                    sb.Append(text, pos, start - pos);
                    var end = text.IndexOf(SlotEnd, start);
                    Restore(sb, Texts[int.Parse(text.AsSpan(start + 1, end - start - 1))]);
                    pos = end + 1;
                }
            }
        }

        #endregion

        #region Protect

        /// <summary>
        /// Removes the indentation shared by all lines, typically the result of a prompt in a multi line string in source code.
        /// Relative indentation is kept (it can define nesting).
        /// </summary>
        static String Dedent(String text)
        {
            String common = null;
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var ws = 0;
                while ((ws < line.Length) && ((line[ws] == ' ') || (line[ws] == '\t')))
                    ++ws;
                if (ws == line.Length)
                    continue;
                if (common == null)
                {
                    common = line.Substring(0, ws);
                    continue;
                }
                var shared = 0;
                while ((shared < common.Length) && (shared < ws) && (common[shared] == line[shared]))
                    ++shared;
                common = common.Substring(0, shared);
            }
            if (String.IsNullOrEmpty(common))
                return text;
            var cl = common.Length;
            return String.Join('\n', lines.Select(x => x.Length >= cl ? x.Substring(cl) : ""));
        }


        const RegexOptions RxOpt = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        const RegexOptions RxOptI = RxOpt | RegexOptions.IgnoreCase;

        /// <summary>
        /// Table rows and indented code (but not nested list items)
        /// </summary>
        static readonly Regex LiteralLine = new(@"\G(?:[ \t]*\|[^\n]*|(?:\t| {4,})(?![ \t]*(?:[-*+]|\d+[.)])[ \t])[ \t]*[^\s][^\n]*)", RxOpt);

        /// <summary>
        /// Lines starting with a label that (usually) is followed by literal data.
        /// If there is nothing after the label, the lines up until the next blank line are protected.
        /// </summary>
        static readonly Regex LiteralLabel = new(@"\G[ \t]*(?:[-*+][ \t]+)?\*{0,2}(?:examples?|samples?|expected(?:[ \t]+(?:output|result|response|answer))?|input|output|result|response|answer|question|q|a|user|assistant|human|ai)(?:[ \t]+\d+)?\*{0,2}:\*{0,2}[ \t]*(?<rest>[^\n]*)", RxOptI);

        /// <summary>
        /// A xml like tag, the attributes can't contain a tag start so scanning is bounded.
        /// </summary>
        static readonly Regex Tag = new(@"\G<(?<close>/?)(?<name>[A-Za-z][\w:.\-]*)(?:\s[^<>\n]*)?(?<self>/?)>", RxOpt);

        /// <summary>
        /// The content of these tags are data, not instructions, so they are protected along with the tags.
        /// The content of other tags (ex: instructions) is compressed, only the tags themselves are protected.
        /// </summary>
        static readonly IReadOnlySet<String> LiteralTags = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "example", "examples", "sample", "samples", "code", "pre", "template", "input", "output", "document", "documents",
            "context", "data", "quote", "script", "style", "json", "xml", "yaml", "csv", "text", "transcript", "article", "email",
        };

        /// <summary>
        /// Protect multi line spans and things that can contain any other syntax: Code fences, inline code, data tags, tables, labeled data.
        /// </summary>
        static String ProtectBlocks(String t, Slots slots)
        {
            var l = t.Length;
            var sb = new StringBuilder(l);
            HashSet<String> noCloseTag = null;
            var lineStart = true;
            int i = 0;
            while (i < l)
            {
                if (lineStart)
                {
                    var end = FindLineLiteral(t, i);
                    if (end > i)
                    {
                        sb.Append(slots.Add(t.Substring(i, end - i)));
                        i = end;
                        lineStart = false;
                        continue;
                    }
                }
                var c = t[i];
                lineStart = c == '\n';
                if (c == '`')
                {
                    var runEnd = i + 1;
                    while ((runEnd < l) && (t[runEnd] == '`'))
                        ++runEnd;
                    var end = FindInlineCodeEnd(t, runEnd, runEnd - i);
                    if (end < 0)
                        sb.Append(t, i, runEnd - i);
                    else
                        sb.Append(slots.Add(t.Substring(i, end - i)));
                    i = end < 0 ? runEnd : end;
                    continue;
                }
                if (c == '<')
                {
                    var m = Tag.Match(t, i);
                    if (m.Success && (m.Groups["close"].Length == 0) && (m.Groups["self"].Length == 0))
                    {
                        var name = m.Groups["name"].Value;
                        if (LiteralTags.Contains(name) && (!(noCloseTag?.Contains(name) ?? false)))
                        {
                            var close = t.IndexOf("</" + name, i + m.Length, StringComparison.OrdinalIgnoreCase);
                            var closeEnd = close < 0 ? -1 : t.IndexOf('>', close);
                            if (closeEnd >= 0)
                            {
                                sb.Append(slots.Add(t.Substring(i, closeEnd + 1 - i)));
                                i = closeEnd + 1;
                                continue;
                            }
                            (noCloseTag ??= new(StringComparer.OrdinalIgnoreCase)).Add(name);
                        }
                    }
                }
                sb.Append(c);
                ++i;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get the end of a literal starting at the start of a line, or -1 if there is none
        /// </summary>
        static int FindLineLiteral(String t, int i)
        {
            var fenceEnd = FindFenceEnd(t, i);
            if (fenceEnd >= 0)
                return fenceEnd;
            var m = LiteralLine.Match(t, i);
            if (m.Success)
                return i + m.Length;
            m = LiteralLabel.Match(t, i);
            if (!m.Success)
                return -1;
            var end = i + m.Length;
            if (m.Groups["rest"].Length > 0)
                return end;
            //  Label on it's own line, protect until a blank line
            var l = t.Length;
            while (end < l)
            {
                var next = LineEnd(t, end + 1);
                if (String.IsNullOrWhiteSpace(t.Substring(end + 1, next - end - 1)))
                    break;
                end = next;
            }
            return end;
        }

        /// <summary>
        /// Get the end of a code fence starting on this line, or -1 if this isn't a code fence. An unterminated fence continues to the end of the text.
        /// </summary>
        static int FindFenceEnd(String t, int i)
        {
            var l = t.Length;
            var p = i;
            while ((p < l) && ((t[p] == ' ') || (t[p] == '\t')))
                ++p;
            if (p >= l)
                return -1;
            var c = t[p];
            if ((c != '`') && (c != '~'))
                return -1;
            var runEnd = p;
            while ((runEnd < l) && (t[runEnd] == c))
                ++runEnd;
            var run = runEnd - p;
            if (run < 3)
                return -1;
            var line = LineEnd(t, runEnd);
            while (line < l)
            {
                var next = LineEnd(t, line + 1);
                var s = t.AsSpan(line + 1, next - line - 1).Trim();
                if ((s.Length >= run) && (s.IndexOfAnyExcept(c) < 0))
                    return next;
                line = next;
            }
            return l;
        }

        static int LineEnd(String t, int i)
        {
            if (i >= t.Length)
                return t.Length;
            var e = t.IndexOf('\n', i);
            return e < 0 ? t.Length : e;
        }

        /// <summary>
        /// Get the end of a inline code span, closed by a backtick run of the same length on the same line, or -1 if it isn't closed.
        /// </summary>
        static int FindInlineCodeEnd(String t, int i, int run)
        {
            var e = LineEnd(t, i);
            while (i < e)
            {
                if (t[i] != '`')
                {
                    ++i;
                    continue;
                }
                var r = i + 1;
                while ((r < e) && (t[r] == '`'))
                    ++r;
                if ((r - i) == run)
                    return r;
                i = r;
            }
            return -1;
        }


        /// <summary>
        /// Protect single tokens and delimited spans in what's left after <see cref="ProtectBlocks(string, Slots)"/>
        /// </summary>
        static String ProtectInline(String t, Slots slots)
        {
            var pairs = FindBracketPairs(t);
            var l = t.Length;
            var sb = new StringBuilder(l);
            //  A failed search for a closing quote is valid to the end of the line, so avoid doing it for every quote on a line
            int noCloseUntil = -1;
            int i = 0;
            while (i < l)
            {
                var c = t[i];
                if (Char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    ++i;
                    continue;
                }
                if (((c == '{') || (c == '[')) && pairs.TryGetValue(i, out var close))
                {
                    var end = close + 1;
                    //  Markdown link
                    if ((c == '[') && (end < l) && (t[end] == '('))
                    {
                        var le = LineEnd(t, end);
                        var pe = t.IndexOf(')', end, le - end);
                        if (pe > 0)
                            end = pe + 1;
                    }
                    sb.Append(slots.Add(t.Substring(i, end - i)));
                    i = end;
                    continue;
                }
                if (IsQuoteStart(t, i))
                {
                    var end = i >= noCloseUntil ? FindQuoteEnd(t, i) : -1;
                    if (end >= 0)
                    {
                        sb.Append(slots.Add(t.Substring(i, end - i)));
                        i = end;
                        continue;
                    }
                    noCloseUntil = LineEnd(t, i);
                    sb.Append(c);
                    ++i;
                    continue;
                }
                if (c == '<')
                {
                    var m = Tag.Match(t, i);
                    if (m.Success)
                    {
                        sb.Append(slots.Add(m.Value));
                        i += m.Length;
                        continue;
                    }
                }
                if ((c == '{') || (c == '[') || (c == '<'))
                {
                    sb.Append(c);
                    ++i;
                    continue;
                }
                i = ProtectToken(t, i, sb, slots);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Find matching curly and square brackets (independent of each other, since prose can be unbalanced).
        /// Done in one pass, so that many unbalanced brackets can't cause quadratic behaviour.
        /// Double quoted strings on one line inside of brackets are skipped, so that json with a bracket in a string works.
        /// </summary>
        /// <returns>Index of the start bracket => index of the end bracket</returns>
        static Dictionary<int, int> FindBracketPairs(String t)
        {
            var pairs = new Dictionary<int, int>();
            var curly = new Stack<int>();
            var square = new Stack<int>();
            var l = t.Length;
            for (int i = 0; i < l; ++i)
            {
                switch (t[i])
                {
                    case '"':
                        if ((curly.Count + square.Count) > 0)
                        {
                            var e = i + 1;
                            while ((e < l) && (t[e] != '"') && (t[e] != '\n'))
                                e += t[e] == '\\' ? 2 : 1;
                            if ((e < l) && (t[e] == '"'))
                                i = e;
                        }
                        break;
                    case '{':
                        curly.Push(i);
                        break;
                    case '[':
                        square.Push(i);
                        break;
                    case '}':
                        if (curly.Count > 0)
                            pairs[curly.Pop()] = i;
                        break;
                    case ']':
                        if (square.Count > 0)
                            pairs[square.Pop()] = i;
                        break;
                }
            }
            return pairs;
        }

        static bool IsQuoteStart(String t, int i)
        {
            var c = t[i];
            if ((c == '"') || (c == '“') || (c == '‘'))
                return true;
            //  Not an apostrophe
            if (c != '\'')
                return false;
            if ((i > 0) && Char.IsLetterOrDigit(t[i - 1]))
                return false;
            return ((i + 1) < t.Length) && (!Char.IsWhiteSpace(t[i + 1]));
        }

        /// <summary>
        /// Get the end of a quote that must be closed on the same line, or -1 if it isn't closed
        /// </summary>
        static int FindQuoteEnd(String t, int i)
        {
            var c = t[i];
            var close = c == '“' ? '”' : (c == '‘' ? '’' : c);
            var le = LineEnd(t, i);
            for (int p = i + 1; p < le; ++p)
            {
                var v = t[p];
                if ((v == '\\') && (c == '"'))
                {
                    ++p;
                    continue;
                }
                if (v != close)
                    continue;
                //  For single quotes, must not be an apostrophe or be preceded by a space
                if ((c == '\'') && (Char.IsWhiteSpace(t[p - 1]) || (((p + 1) < le) && Char.IsLetterOrDigit(t[p + 1]))))
                    continue;
                return p + 1;
            }
            return -1;
        }

        const String TokenLead = "([*_~";
        const String TokenTrail = ".,;:!?)]}*_~";

        /// <summary>
        /// Handle one whitespace delimited token, protecting it if it isn't a plain word.
        /// </summary>
        /// <returns>Index of the next char to process</returns>
        static int ProtectToken(String t, int i, StringBuilder sb, Slots slots)
        {
            var l = t.Length;
            var e = i + 1;
            for (; e < l; ++e)
            {
                var c = t[e];
                if (Char.IsWhiteSpace(c) || (c == '{') || (c == '[') || (c == '<') || IsQuoteStart(t, e))
                    break;
            }
            var s = i;
            while ((s < e) && TokenLead.Contains(t[s]))
                ++s;
            var ce = e;
            while ((ce > s) && TokenTrail.Contains(t[ce - 1]))
                --ce;
            if ((ce <= s) || (!IsTechnical(t.AsSpan(s, ce - s))))
            {
                sb.Append(t, i, e - i);
                return e;
            }
            sb.Append(t, i, s - i);
            sb.Append(slots.Add(t.Substring(s, ce - s)));
            sb.Append(t, ce, e - ce);
            return e;
        }

        /// <summary>
        /// Check if a token is anything else than a plain word, such as: identifiers (snake_case, camelCase, PascalCase), UPPERCASE, urls, paths, file names, versions, command line flags, numbers with units, and/or.
        /// </summary>
        static bool IsTechnical(ReadOnlySpan<Char> s)
        {
            //  Only a slot, nothing to do
            if ((s[0] == SlotStart) && (s.IndexOf(SlotEnd) == (s.Length - 1)))
                return false;
            if (s[0] == '-')
                return s.ContainsAnyInRange('a', 'z') || s.ContainsAnyInRange('A', 'Z');
            bool letter = false, digit = false, symbol = false;
            for (int i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if (Char.IsLetter(c))
                {
                    letter = true;
                    if ((i > 0) && Char.IsUpper(c))
                        return true;
                    continue;
                }
                if (Char.IsDigit(c))
                {
                    digit = true;
                    continue;
                }
                //  Plain words can contain these
                if ((c == '-') || (c == '\'') || (c == '’') || (c == ',') || (c == ';') || (c == '!') || (c == '?'))
                    continue;
                //  A symbol (or a slot) attached to something
                symbol = true;
            }
            return letter && (digit || symbol);
        }

        #endregion

        #region Prose rules

        //  Not inside of a larger word/token
        const String WordStart = @"(?<![\w'/\\\-])";
        const String WordEnd = @"(?![\w'/\\\-])";

        //  A rewrite of a negated phrase can invert the meaning (ex: "does not have the ability to")
        const String NotNegated = @"(?<!\b(?:not|never|cannot|without|no)[ \t]+)(?<!n't[ \t]+)";

        //  Start of a line (including any list marker or heading) or a sentence
        const String SentenceStart = @"(?<=(?:\A|\n|[.!?:;][ \t]+)[ \t]*(?:(?:[-*+>]|\d+[.)]|\#{1,6})[ \t]+)?*)";

        /// <summary>
        /// Verbose phrase => shorter phrase with the same meaning. An empty string removes the phrase.
        /// </summary>
        static readonly IReadOnlyDictionary<String, String> Phrases = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            //  Purpose
            { "in order to", "to" }, { "so as to", "to" }, { "for the purpose of", "for" }, { "with the aim of", "for" },
            //  Conditions (only exact synonyms of if, "if and only if", "provided that" etc are left as is)
            { "in the event that", "if" }, { "in the case that", "if" }, { "in case that", "if" }, { "on the condition that", "if" },
            //  Reasons
            { "due to the fact that", "because" }, { "owing to the fact that", "because" }, { "because of the fact that", "because" }, { "on account of the fact that", "because" },
            { "in light of the fact that", "because" }, { "for the reason that", "because" }, { "given the fact that", "since" },
            { "as a consequence of", "because of" }, { "as a result of", "because of" },
            //  Contrast
            { "despite the fact that", "although" }, { "in spite of the fact that", "although" }, { "regardless of the fact that", "although" },
            { "that being said", "however" }, { "having said that", "however" }, { "with that said", "however" },
            { "the fact that", "that" },
            //  Time
            { "at this point in time", "now" }, { "at the present time", "now" }, { "at this moment in time", "now" }, { "in the near future", "soon" },
            { "prior to", "before" }, { "in advance of", "before" }, { "subsequent to", "after" }, { "until such time as", "until" }, { "at such time as", "when" },
            { "during the course of", "during" }, { "in the course of", "during" }, { "is in the process of", "is" }, { "are in the process of", "are" },
            //  Frequency and manner
            { "on a daily basis", "daily" }, { "on a weekly basis", "weekly" }, { "on a monthly basis", "monthly" }, { "on a yearly basis", "yearly" }, { "on a regular basis", "regularly" },
            { "in a timely manner", "promptly" }, { "in a clear and concise manner", "clearly and concisely" }, { "in a concise manner", "concisely" }, { "in a clear manner", "clearly" },
            { "in a detailed manner", "in detail" }, { "in the following way", "as follows" }, { "in the following manner", "as follows" },
            //  Amounts
            { "a large number of", "many" }, { "a great number of", "many" }, { "a significant number of", "many" }, { "a wide range of", "many" }, { "a number of", "several" }, { "a variety of", "various" },
            { "the vast majority of", "most" }, { "the majority of", "most" }, { "a majority of", "most" }, { "in excess of", "over" },
            { "all of the", "all" }, { "both of the", "both" }, { "each of the", "each" }, { "any of the", "any" }, { "most of the", "most" }, { "some of the", "some" }, { "many of the", "many" },
            //  Redundant pairs
            { "whether or not", "whether" }, { "each and every", "every" }, { "any and all", "all" }, { "first and foremost", "first" }, { "first of all", "first" }, { "one and only", "only" }, { "and also", "and" },
            //  Relations
            { "with regard to", "regarding" }, { "with regards to", "regarding" }, { "in regard to", "regarding" }, { "with the exception of", "except" }, { "in the absence of", "without" },
            { "in accordance with", "per" }, { "by means of", "by" }, { "by virtue of", "by" }, { "in close proximity to", "near" }, { "in the vicinity of", "near" }, { "together with", "with" }, { "along with", "with" },
            //  Verbs
            { "make sure to", "" }, { "be sure to", "" }, { "make sure that", "ensure" }, { "be sure that", "ensure" }, { "ensure that", "ensure" }, { "make sure", "ensure" },
            { "take into account", "consider" }, { "take into consideration", "consider" }, { "taking into account", "considering" }, { "give consideration to", "consider" },
            { "make use of", "use" }, { "making use of", "using" }, { "utilize", "use" }, { "utilizes", "uses" }, { "utilizing", "using" }, { "utilise", "use" }, { "utilises", "uses" }, { "utilising", "using" },
            { "has the ability to", "can" }, { "is able to", "can" }, { "are able to", "can" }, { "will be able to", "can" },
            { "keep in mind that", "remember" },
            //  Notes
            { "it is important to note that", "note that" }, { "it should be noted that", "note that" }, { "it must be noted that", "note that" }, { "it is worth noting that", "note that" }, { "it is worth mentioning that", "note that" },
            { "it goes without saying that", "" },
        };

        /// <summary>
        /// Matches any phrase in <see cref="Phrases"/>, longest first
        /// </summary>
        static readonly Regex PhraseRule = new(
            NotNegated + WordStart + "(?:" + String.Join('|', Phrases.Keys.OrderByDescending(x => x.Length).Select(x => x.Replace(" ", @"[ \t\n]+"))) + ")" + WordEnd + @"(?<ws>[ \t]*)",
            RxOptI);

        /// <summary>
        /// "as well as" is the same as "and", except when comparing (ex: "do it as well as possible")
        /// </summary>
        static readonly Regex AsWellAsRule = new(WordStart + @"as[ \t]+well[ \t]+as(?=[ \t]+)(?![ \t]+(?:possible|you|they|we|it|he|she|i|can|could)\b)", RxOptI);

        /// <summary>
        /// A polite way to ask for something at the start of a sentence, the question becomes a command.
        /// A plain "Can you" is left as is, it can be a real question about capabilities.
        /// </summary>
        static readonly Regex RequestRule = new(
            SentenceStart + @"(?:(?:could|would)[ \t]+you[ \t]+(?!not\b|like\b|mind\b|be\b|have\b|rather\b)|(?:can|will)[ \t]+you[ \t]+(?=please\b|kindly\b)" +
            @"|i(?:[ \t]+would|'d)[ \t]+like[ \t]+you[ \t]+to[ \t]+|i[ \t]+(?:want|need)[ \t]+you[ \t]+to[ \t]+|we[ \t]+(?:want|need)[ \t]+you[ \t]+to[ \t]+|i[ \t]+would[ \t]+like[ \t]+to[ \t]+ask[ \t]+you[ \t]+to[ \t]+" +
            @"|i[ \t]+would[ \t]+appreciate[ \t]+it[ \t]+if[ \t]+you[ \t]+could[ \t]+|it[ \t]+would[ \t]+be[ \t]+(?:great|nice|helpful)[ \t]+if[ \t]+you[ \t]+could[ \t]+|your[ \t]+(?:task|job|goal)[ \t]+is[ \t]+to[ \t]+)" +
            @"(?:(?:please|kindly)[ \t]+)?(?<body>[^.?!\n]*)(?<end>[.?!]|$)",
            RxOptI | RegexOptions.Multiline);

        /// <summary>
        /// Different ways of saying must at the start of a sentence, not valid elsewhere (ex: "if you need to").
        /// </summary>
        static readonly Regex MustRule = new(
            SentenceStart + @"(?:you(?:[ \t]+will)?[ \t]+(?:need|have)[ \t]+to|you[ \t]+are[ \t]+(?:required|expected|obliged)[ \t]+to" +
            @"|it[ \t]+is[ \t]+(?:(?:very|extremely|absolutely|really|highly)[ \t]+)?(?:important|crucial|critical|essential|vital|imperative|necessary)[ \t]+(?:that[ \t]+you(?:[ \t]+must)?|for[ \t]+you[ \t]+to|to))" +
            @"(?<not>[ \t]+(?:do[ \t]+not|don't))?" + WordEnd,
            RxOptI);

        /// <summary>
        /// Words without meaning, anywhere. Words that can be a command (ex: "answer honestly", "take it literally") or restrict one (ex: "just the number") must not be here.
        /// </summary>
        static readonly Regex FillerRule = new(NotNegated + WordStart + @"(?:basically|essentially|actually|obviously)" + WordEnd + @",?[ \t]*", RxOptI);

        /// <summary>
        /// The word please, unless it is a verb (ex: "that will please customers")
        /// </summary>
        static readonly Regex PleaseRule = new(@"(?<!\b(?:to|will|would|may|can|must|should|might|could|not|that|who|which)[ \t]+)" + WordStart + "please" + WordEnd + @",?[ \t]*", RxOptI);

        /// <summary>
        /// Words without meaning at the start of a sentence, a new sentence is already an addition.
        /// </summary>
        static readonly Regex LeadRule = new(SentenceStart + @"(?:additionally|furthermore|moreover|in[ \t]+addition|also|kindly|simply(?![ \t]+put\b)|needless[ \t]+to[ \t]+say|as[ \t]+you[ \t]+know|as[ \t]+you[ \t]+can[ \t]+see|of[ \t]+course)" + WordEnd + @",?[ \t]*", RxOptI);

        /// <summary>
        /// A greeting at the very start of the prompt
        /// </summary>
        static readonly Regex GreetingRule = new(@"\A\s*(?:hi|hello|hey|greetings)(?:[ \t]+there)?[,!.]+\s*", RxOptI);

        /// <summary>
        /// A complete sentence just thanking
        /// </summary>
        static readonly Regex ThanksRule = new(SentenceStart + @"(?:thanks|thank[ \t]+you)(?:[ \t]+(?:so|very))?(?:[ \t]+much)?(?:[ \t]+in[ \t]+advance)?(?:[ \t]+for[ \t]+your[ \t]+(?:help|assistance|time))?[.!]*[ \t]*$", RxOptI | RegexOptions.Multiline);

        /// <summary>
        /// Articles can be removed before a lower case word, except:
        /// - Before a word that shows that it's not used as an article (ex: "a or b", "a is 5").
        /// - In idioms where the meaning changes (ex: "a few" vs "few", "a day", "in a row", "by the way").
        /// - When used as a name (ex: "option a").
        /// </summary>
        static readonly Regex ArticleRule = new(
            @"(?<!\b(?:option|variable|column|row|point|plan|grade|vitamin|letter|type|class|group|section|part|case)[ \t]+)" + WordStart + @"(?:[Tt]he|[Aa]n?)[ \t]+" +
            @"(?![a-z][\w'\-]*[ \t]+(?:or|and)\b[ \t]+[a-z]\b)" +
            @"(?!(?:or|and|to|is|are|was|were|be|been|vs|versus|of|in|on|at|for|with|by|from|then|than|if|but|nor|that|which|who|as|should|shall|can|could|will|would|may|might|has|have|had|does|do|did|not|equals|contains|few|little|lot|bit|couple|while|priori|posteriori|la|day|week|month|year|hour|minute|second|time|row|must|more|less|better|worse|way|end|great|good)\b)" +
            @"(?=[a-z])",
            RxOpt);

        static readonly Regex InvisibleChars = new(@"[​﻿­]", RxOpt);
        static readonly Regex RepeatedExclamation = new(@"!{2,}", RxOpt);
        static readonly Regex RepeatedQuestion = new(@"\?{2,}", RxOpt);
        static readonly Regex LongEllipsis = new(@"\.{4,}", RxOpt);
        static readonly Regex SeparatorLine = new(@"^[ \t]*([-=*_~])\1{3,}[ \t]*$", RxOpt | RegexOptions.Multiline);
        static readonly Regex InnerSpace = new(@"(?<=\S)[ \t]+(?=\S)", RxOpt);
        static readonly Regex SpaceBeforePunctuation = new(@"(?<=\S)[ \t]+(?=[,;:.!?](?:\s|$))", RxOpt | RegexOptions.Multiline);
        static readonly Regex DanglingComma = new(@",+(?=[.!?;:])", RxOpt);
        static readonly Regex TrailingSpace = new(@"[ \t]+$", RxOpt | RegexOptions.Multiline);
        static readonly Regex BlankLines = new(@"\n{3,}", RxOpt);
        static readonly Regex SentenceSplit = new(@"(?<=[.!?])[ \t]+", RxOpt);

        /// <summary>
        /// Rewrite the prose. Order matters, phrases that contain words that are removed by a later rule must be handled first.
        /// </summary>
        static String RewriteProse(String t)
        {
            t = t.Replace(' ', ' ').Replace('’', '\'');
            t = InvisibleChars.Replace(t, "");

            t = GreetingRule.Replace(t, static m => CapNext.ToString());
            t = RequestRule.Replace(t, static m =>
            {
                var end = m.Groups["end"].Value;
                return String.Concat(KeepCase(m, ""), m.Groups["body"].Value, end == "?" ? "." : end);
            });
            t = PhraseRule.Replace(t, static m =>
            {
                var to = Phrases[String.Join(' ', m.Value.Split((Char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant()];
                return to.Length == 0 ? KeepCase(m, "") : String.Concat(KeepCase(m, to), m.Groups["ws"].Value);
            });
            t = AsWellAsRule.Replace(t, static m => KeepCase(m, "and"));
            t = MustRule.Replace(t, static m => KeepCase(m, m.Groups["not"].Length > 0 ? "must not" : "must"));
            t = LeadRule.Replace(t, static m => KeepCase(m, ""));
            t = ThanksRule.Replace(t, "");
            t = PleaseRule.Replace(t, static m => KeepCase(m, ""));
            t = FillerRule.Replace(t, static m => KeepCase(m, ""));
            t = ArticleRule.Replace(t, static m => KeepCase(m, ""));

            t = RepeatedExclamation.Replace(t, "!");
            t = RepeatedQuestion.Replace(t, "?");
            t = LongEllipsis.Replace(t, "...");
            t = SeparatorLine.Replace(t, static m => new String(m.Groups[1].Value[0], 3));
            t = ApplyCapitalization(t);
            t = InnerSpace.Replace(t, " ");
            t = SpaceBeforePunctuation.Replace(t, "");
            t = DanglingComma.Replace(t, "");
            t = TrailingSpace.Replace(t, "");
            t = DropRepeats(t);
            t = BlankLines.Replace(t, "\n\n");
            return t.Trim();
        }

        /// <summary>
        /// Get the replacement of a match that started with a capital letter (the first letter of what ends up there is capitalized later)
        /// </summary>
        static String KeepCase(Match m, String to)
            => Char.IsUpper(m.Value[0]) ? String.Concat(CapNext.ToString(), to) : to;

        /// <summary>
        /// Process the markers inserted by <see cref="KeepCase(Match, string)"/>
        /// </summary>
        static String ApplyCapitalization(String t)
        {
            if (t.IndexOf(CapNext) < 0)
                return t;
            var sb = new StringBuilder(t.Length);
            var pending = false;
            foreach (var c in t)
            {
                if (c == CapNext)
                {
                    pending = true;
                    continue;
                }
                if (pending)
                {
                    //  Removed text at the start of a line must not leave any space
                    if ((c == ' ') || (c == '\t'))
                        continue;
                    pending = false;
                    sb.Append(Char.ToUpperInvariant(c));
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Minimum number of words in a sentence for it to be considered a repetition, repeating short texts can be intentional or data (ex: "Yes. Yes.")
        /// </summary>
        const int MinRepeatWords = 4;

        static bool IsRepeat(String a, String b)
            => a.Equals(b, StringComparison.Ordinal) && (a.AsSpan().Count(' ') >= (MinRepeatWords - 1));

        /// <summary>
        /// Drop lines and sentences that are exact copies of the one before it, this does not lose any instruction.
        /// </summary>
        static String DropRepeats(String t)
        {
            var lines = t.Split('\n');
            var res = new List<String>(lines.Length);
            String prevLine = null;
            foreach (var line in lines)
            {
                if ((prevLine != null) && IsRepeat(line, prevLine))
                    continue;
                var sentences = SentenceSplit.Split(line);
                var kept = sentences.Length <= 1
                    ? line
                    : String.Join(' ', sentences.Where((x, i) => (i == 0) || (!IsRepeat(x, sentences[i - 1]))));
                res.Add(kept);
                prevLine = line;
            }
            return String.Join('\n', res);
        }

        #endregion

    }
}
