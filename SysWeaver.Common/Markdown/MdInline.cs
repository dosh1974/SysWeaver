using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SysWeaver.Markdown
{
    /// <summary>
    /// An allowed html attribute (the value is decoded and validated)
    /// </summary>
    readonly record struct MdAttribute(String Name, String Value);

    /// <summary>
    /// The output format, inline content is rendered by calling these methods
    /// </summary>
    abstract class MdFormat
    {
        protected MdFormat(MdDocument doc)
        {
            Doc = doc;
        }

        public readonly MdDocument Doc;

        /// <summary>
        /// Labels (normalized) of referenced footnotes, in reference order
        /// </summary>
        public readonly List<String> FootnoteOrder = new();

        /// <summary>
        /// Get the number of a footnote (the order that it was first referenced)
        /// </summary>
        public int FootnoteNumber(String label)
        {
            var i = FootnoteOrder.IndexOf(label);
            if (i >= 0)
                return i + 1;
            FootnoteOrder.Add(label);
            return FootnoteOrder.Count;
        }

        MdFormat PlainFormatInstance;

        /// <summary>
        /// The format used to render plain text
        /// </summary>
        public MdFormat PlainFormat => PlainFormatInstance ??= (this as MdPlainFormat) ?? (MdFormat)new MdPlainFormat(Doc);

        /// <summary>
        /// Render inline markdown as plain text
        /// </summary>
        public String Plain(String markdown) => MdInline.Render(markdown, PlainFormat);

        /// <summary>
        /// Plain text
        /// </summary>
        public abstract String Text(String text);
        /// <summary>
        /// A code span
        /// </summary>
        public abstract String Code(String code);
        /// <summary>
        /// A link
        /// </summary>
        /// <param name="url">The (safe) url</param>
        /// <param name="title">The title, can be null</param>
        /// <param name="inner">The rendered link text</param>
        /// <param name="innerPlain">The link text as plain text</param>
        public abstract String Link(String url, String title, String inner, String innerPlain);
        /// <summary>
        /// An image
        /// </summary>
        /// <param name="url">The (safe) url</param>
        /// <param name="alt">The alternative text (plain text)</param>
        /// <param name="title">The title, can be null</param>
        public abstract String Image(String url, String alt, String title);
        public abstract String HardBreak { get; }
        public abstract String SoftBreak { get; }
        /// <summary>
        /// An allowed html tag
        /// </summary>
        public abstract String Tag(String tag, bool closing, IReadOnlyList<MdAttribute> attributes);
        /// <summary>
        /// Start of emphasis
        /// </summary>
        /// <param name="tag">"em", "strong" or "del"</param>
        public abstract String EmphasisOpen(String tag);
        /// <summary>
        /// End of emphasis
        /// </summary>
        /// <param name="tag">"em", "strong" or "del"</param>
        public abstract String EmphasisClose(String tag);
        /// <summary>
        /// Math (TeX)
        /// </summary>
        public abstract String Math(String tex, bool display);
        /// <summary>
        /// An emoji short code
        /// </summary>
        /// <param name="name">The short code name, ex: "smile"</param>
        /// <param name="emoji">The emoji</param>
        public abstract String Emoji(String name, String emoji);
        /// <summary>
        /// A footnote reference
        /// </summary>
        public abstract String FootnoteRef(int number, String label);
    }

    /// <summary>
    /// Renders inline content as plain text
    /// </summary>
    sealed class MdPlainFormat : MdFormat
    {
        public MdPlainFormat(MdDocument doc) : base(doc)
        {
        }

        public override String Text(String text) => text;
        public override String Code(String code) => code;
        public override String Link(String url, String title, String inner, String innerPlain) => inner;
        public override String Image(String url, String alt, String title) => alt;
        public override String HardBreak => " ";
        public override String SoftBreak => " ";
        public override String Tag(String tag, bool closing, IReadOnlyList<MdAttribute> attributes) => tag == "br" ? " " : "";
        public override String EmphasisOpen(String tag) => "";
        public override String EmphasisClose(String tag) => "";
        public override String Math(String tex, bool display) => tex;
        public override String Emoji(String name, String emoji) => emoji;
        public override String FootnoteRef(int number, String label) => "";
    }

    /// <summary>
    /// Inline markdown parsing.
    /// Link texts are parsed as ranges of the same string, and the results are cached (a failed outer link renders the inner links, which are then parsed again),
    /// so that nested brackets doesn't take exponential (or quadratic) time.
    /// </summary>
    static class MdInline
    {
        /// <summary>
        /// Links and images are not parsed when nested deeper than this
        /// </summary>
        const int MaxLinkDepth = 16;

        /// <summary>
        /// The maximum length of a link label (CommonMark)
        /// </summary>
        const int MaxLabelLength = 999;

        /// <summary>
        /// Url schemes that are allowed in links and images (relative urls are always allowed)
        /// </summary>
        static readonly String[] SafeSchemes = ["http", "https", "mailto", "tel", "ftp", "cid"];

        /// <summary>
        /// Html tags that are allowed, key = tag, value = allowed attributes (in addition to title)
        /// </summary>
        static readonly Dictionary<String, String[]> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = [], ["strong"] = [], ["i"] = [], ["em"] = [], ["u"] = [], ["s"] = [], ["strike"] = [], ["del"] = [], ["ins"] = [], ["mark"] = [], ["small"] = [],
            ["sub"] = [], ["sup"] = [], ["q"] = [], ["cite"] = [], ["abbr"] = [], ["dfn"] = [], ["kbd"] = [], ["var"] = [], ["samp"] = [], ["code"] = [], ["tt"] = [],
            ["span"] = [], ["ruby"] = [], ["rt"] = [], ["rp"] = [], ["br"] = [], ["wbr"] = [], ["hr"] = [],
            ["a"] = ["href"],
            ["img"] = ["src", "alt", "width", "height", "align"],
            ["p"] = ["align"], ["div"] = ["align"], ["center"] = [],
            ["h1"] = ["align"], ["h2"] = ["align"], ["h3"] = ["align"], ["h4"] = ["align"], ["h5"] = ["align"], ["h6"] = ["align"],
            ["details"] = ["open"], ["summary"] = [],
            ["blockquote"] = [], ["pre"] = [], ["figure"] = [], ["figcaption"] = [],
            ["dl"] = [], ["dt"] = [], ["dd"] = [],
            ["ul"] = [], ["ol"] = ["start"], ["li"] = [],
            ["table"] = ["align"], ["caption"] = [], ["thead"] = [], ["tbody"] = [], ["tfoot"] = [], ["tr"] = ["align"],
            ["th"] = ["align", "colspan", "rowspan"], ["td"] = ["align", "colspan", "rowspan"],
        };

        /// <summary>
        /// Tags without content (closing tags are ignored)
        /// </summary>
        public static readonly HashSet<String> VoidTags = new(StringComparer.Ordinal) { "br", "wbr", "hr", "img" };

        static readonly Regex EntityRe = new(@"\G&(?:[A-Za-z][A-Za-z0-9]{1,31}|#[0-9]{1,7}|#[xX][0-9A-Fa-f]{1,6});", RegexOptions.Compiled);
        static readonly Regex AutoLinkRe = new(@"\G<([A-Za-z][A-Za-z0-9+.\-]{1,31}:[^\s<>]*)>", RegexOptions.Compiled);
        static readonly Regex EmailLinkRe = new(@"\G<([A-Za-z0-9.!#$%&'*+/=?^_`{|}~\-]+@[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*)>", RegexOptions.Compiled);
        static readonly Regex HtmlTagRe = new(@"\G<(/?)([A-Za-z][A-Za-z0-9]*)((?:\s+[A-Za-z_:][A-Za-z0-9_.:\-]*(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'=<>`]+))?)*)\s*(/?)>", RegexOptions.Compiled);
        static readonly Regex AttributeRe = new(@"([A-Za-z_:][A-Za-z0-9_.:\-]*)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'=<>`]+)))?", RegexOptions.Compiled);
        static readonly Regex BareUrlRe = new(@"\G(?:https?://|ftp://|www\.)[^\s<]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex BareEmailDomainRe = new(@"\G[A-Za-z0-9](?:[A-Za-z0-9\-_]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-_]*[A-Za-z0-9])?)+", RegexOptions.Compiled);
        static readonly Regex TrailingEntityRe = new(@"&[A-Za-z0-9]+;$", RegexOptions.Compiled);
        static readonly Regex EmojiRe = new(@"\G:([a-z0-9_+\-]{1,40}):", RegexOptions.Compiled);
        static readonly Regex NumberRe = new(@"^\d{1,6}%?$", RegexOptions.Compiled);
        static readonly Regex AlignRe = new(@"^(left|center|right|justify)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex SchemeRe = new(@"^([A-Za-z][A-Za-z0-9+.\-]{1,31}):", RegexOptions.Compiled);

        #region Urls

        /// <summary>
        /// Returns the url if it's safe to use in a link or image (percent encoded), else null
        /// </summary>
        public static String SafeUrl(String url)
        {
            url = url?.Trim();
            if (url == null)
                return null;
            //  Browsers ignore tabs and new lines in urls (ex: "java\nscript:")
            url = url.Replace("\t", "").Replace("\n", "").Replace("\r", "");
            var m = SchemeRe.Match(url);
            if (!m.Success)
                return EncodeUrl(url);
            var scheme = m.Groups[1].Value;
            return SafeSchemes.Any(x => String.Equals(x, scheme, StringComparison.OrdinalIgnoreCase)) ? EncodeUrl(url) : null;
        }

        /// <summary>
        /// True if the url has a scheme (ex: "https:", "mailto:")
        /// </summary>
        public static bool IsAbsolute(String url) => SchemeRe.IsMatch(url);

        const String UrlSafeChars = ";/?:@&=+$,-_.!~*'()#";

        /// <summary>
        /// Percent encode characters that aren't valid in an url (existing %XX escapes are kept), like CommonMark does
        /// </summary>
        static String EncodeUrl(String url)
        {
            bool ok = true;
            foreach (var c in url)
            {
                if (!((c < 128) && (Char.IsAsciiLetterOrDigit(c) || (c == '%') || UrlSafeChars.Contains(c))))
                {
                    ok = false;
                    break;
                }
            }
            if (ok && (url.IndexOf('%') < 0))
                return url;
            var sb = new StringBuilder(url.Length + 16);
            Span<byte> bytes = stackalloc byte[8];
            for (int i = 0; i < url.Length; ++i)
            {
                var c = url[i];
                if (c == '%')
                {
                    if ((i + 2 < url.Length) && Char.IsAsciiHexDigit(url[i + 1]) && Char.IsAsciiHexDigit(url[i + 2]))
                        sb.Append('%');
                    else
                        sb.Append("%25");
                    continue;
                }
                if ((c < 128) && (Char.IsAsciiLetterOrDigit(c) || UrlSafeChars.Contains(c)))
                {
                    sb.Append(c);
                    continue;
                }
                var len = (Char.IsHighSurrogate(c) && (i + 1 < url.Length) && Char.IsLowSurrogate(url[i + 1]))
                    ? Encoding.UTF8.GetBytes(url.AsSpan(i++, 2), bytes)
                    : Encoding.UTF8.GetBytes(Char.IsSurrogate(c) ? "�" : c.ToString(), bytes);
                for (int b = 0; b < len; ++b)
                    sb.Append('%').Append(bytes[b].ToString("X2"));
            }
            return sb.ToString();
        }

        #endregion//Urls

        #region Helpers

        /// <summary>
        /// Only ASCII white space separates link destinations and titles
        /// </summary>
        static bool IsAsciiWhiteSpace(char c) => (c == ' ') || (c == '\t') || (c == '\n') || (c == '\r') || (c == '\f') || (c == '\v');

        public static bool IsAsciiPunct(char c) => (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');

        static bool IsPunct(char c) => Char.IsPunctuation(c) || Char.IsSymbol(c);

        static bool IsEmailLocal(char c) => Char.IsAsciiLetterOrDigit(c) || (c == '.') || (c == '-') || (c == '_') || (c == '+');

        static int CountRun(String s, int i, int end, char c)
        {
            int e = i;
            while ((e < end) && (s[e] == c))
                ++e;
            return e - i;
        }

        /// <summary>
        /// The state of rendering a string (shared by all ranges and formats)
        /// </summary>
        sealed class Context
        {
            public Context(String s)
            {
                S = s;
            }

            public readonly String S;

            /// <summary>
            /// Rendered ranges (link texts), key = (format, start, end), value = the result and if it contains a link
            /// </summary>
            public readonly Dictionary<(MdFormat, int, int), (String Text, bool HasLink)> Ranges = new();

            /// <summary>
            /// Key = (run length, end), value = a position where a search for the end of a code span failed (searches from later positions will fail too)
            /// </summary>
            public readonly Dictionary<(int, int), int> CodeFailed = new();

            int[] BracketEnds;

            /// <summary>
            /// Get the position of the ']' that matches the '[' at a position (or -1)
            /// </summary>
            public int BracketEnd(int start) => (BracketEnds ??= FindBrackets(this))[start];

            /// <summary>
            /// Key = end, value = a position where a search for a closing $ failed (searches from later positions will fail too)
            /// </summary>
            public readonly Dictionary<int, int> MathFailed = new();

            /// <summary>
            /// The current link nesting depth
            /// </summary>
            public int LinkDepth;
        }

        /// <summary>
        /// Find the end of a code span (the start of a backtick run of the same length)
        /// </summary>
        static int FindCodeEnd(Context ctx, int from, int end, int run)
        {
            var key = (run, end);
            if (ctx.CodeFailed.TryGetValue(key, out var f) && (from >= f))
                return -1;
            var s = ctx.S;
            for (int i = from; i < end;)
            {
                if (s[i] != '`')
                {
                    ++i;
                    continue;
                }
                var r = CountRun(s, i, end, '`');
                if (r == run)
                    return i;
                i += r;
            }
            ctx.CodeFailed[key] = from;
            return -1;
        }

        /// <summary>
        /// Find the matching ']' of all '[' (escapes, code spans, autolinks and html tags are handled)
        /// </summary>
        /// <returns>For each position of a '[', the position of the matching ']' (or -1)</returns>
        static int[] FindBrackets(Context ctx)
        {
            var s = ctx.S;
            var n = s.Length;
            var ends = new int[n];
            Array.Fill(ends, -1);
            var stack = new Stack<int>();
            for (int i = 0; i < n; ++i)
            {
                switch (s[i])
                {
                    case '\\':
                        ++i;
                        break;
                    case '`':
                        {
                            var run = CountRun(s, i, n, '`');
                            var end = FindCodeEnd(ctx, i + run, n, run);
                            i = end < 0 ? i + run - 1 : end + run - 1;
                            break;
                        }
                    case '<':
                        {
                            //  Autolinks and html tags binds tighter than link brackets
                            var m = AutoLinkRe.Match(s, i);
                            if (!m.Success)
                                m = EmailLinkRe.Match(s, i);
                            if (!m.Success)
                                m = HtmlTagRe.Match(s, i);
                            if (m.Success)
                                i += m.Length - 1;
                            break;
                        }
                    case '[':
                        stack.Push(i);
                        break;
                    case ']':
                        if (stack.Count > 0)
                            ends[stack.Pop()] = i;
                        break;
                }
            }
            return ends;
        }

        #endregion//Helpers

        #region Rendering

        sealed class Node
        {
            /// <summary>
            /// 0 = text (escaped when rendered), 1 = rendered, 2 = a delimiter run (*, _ or ~)
            /// </summary>
            public int Kind;
            public String S;
            public char C;
            public int Count;
            public bool CanOpen;
            public bool CanClose;
            /// <summary>
            /// Opening emphasis, rendered after the (remaining) delimiter chars
            /// </summary>
            public String Opens = "";
            /// <summary>
            /// Closing emphasis, rendered before the (remaining) delimiter chars
            /// </summary>
            public String Closes = "";
        }

        /// <summary>
        /// Render inline markdown
        /// </summary>
        /// <param name="s">The inline markdown</param>
        /// <param name="f">The output format</param>
        public static String Render(String s, MdFormat f) => Render(new Context(s), 0, s.Length, f, false, out var _);

        /// <summary>
        /// Render the text of a link or image (cached)
        /// </summary>
        static (String Text, bool HasLink) RenderRange(Context ctx, int start, int end, MdFormat f)
        {
            var key = (f, start, end);
            if (ctx.Ranges.TryGetValue(key, out var r))
                return r;
            ++ctx.LinkDepth;
            try
            {
                var text = Render(ctx, start, end, f, true, out var hasLink);
                r = (text, hasLink);
            }
            finally
            {
                --ctx.LinkDepth;
            }
            ctx.Ranges[key] = r;
            return r;
        }

        /// <summary>
        /// Render a range of inline markdown
        /// </summary>
        /// <param name="ctx">The context</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <param name="f">The output format</param>
        /// <param name="inLink">True when rendering the text of a link (bare urls and email addresses are not converted to links)</param>
        /// <param name="hasLink">True if the range contains a link (at any level)</param>
        static String Render(Context ctx, int start, int end, MdFormat f, bool inLink, out bool hasLink)
        {
            var s = ctx.S;
            hasLink = false;
            var nodes = new List<Node>();
            var sb = new StringBuilder();
            void flush()
            {
                if (sb.Length <= 0)
                    return;
                nodes.Add(new Node { Kind = 0, S = sb.ToString() });
                sb.Clear();
            }
            void rendered(String text)
            {
                flush();
                nodes.Add(new Node { Kind = 1, S = text });
            }
            int n = end;
            int i = start;
            while (i < n)
            {
                var c = s[i];
                switch (c)
                {
                    case '\\':
                        if ((i + 1 < n) && IsAsciiPunct(s[i + 1]))
                        {
                            sb.Append(s[i + 1]);
                            i += 2;
                            continue;
                        }
                        if ((i + 1 < n) && (s[i + 1] == '\n'))
                        {
                            rendered(f.HardBreak);
                            i += 2;
                            while ((i < n) && (s[i] == ' '))
                                ++i;
                            continue;
                        }
                        break;
                    case '`':
                        {
                            var run = CountRun(s, i, n, '`');
                            var codeEnd = FindCodeEnd(ctx, i + run, n, run);
                            if (codeEnd < 0)
                            {
                                sb.Append('`', run);
                                i += run;
                                continue;
                            }
                            var code = s.Substring(i + run, codeEnd - i - run).Replace('\n', ' ');
                            if ((code.Length >= 2) && (code[0] == ' ') && (code[^1] == ' ') && (code.Trim().Length > 0))
                                code = code.Substring(1, code.Length - 2);
                            rendered(f.Code(code));
                            i = codeEnd + run;
                            continue;
                        }
                    case '$':
                        if (TryMath(ctx, i, n, f, out var mathText, out var mathNext))
                        {
                            rendered(mathText);
                            i = mathNext;
                            continue;
                        }
                        break;
                    case '!':
                        if ((i + 1 < n) && (s[i + 1] == '[') && TryLink(ctx, i + 1, n, f, true, out var imgText, out var imgNext, out var imgHasLink))
                        {
                            rendered(imgText);
                            hasLink |= imgHasLink;
                            i = imgNext;
                            continue;
                        }
                        break;
                    case '[':
                        if ((i + 1 < n) && (s[i + 1] == '^'))
                        {
                            var e = s.IndexOf(']', i + 2, n - i - 2);
                            if ((e > i + 2) && (e - i - 2 <= MaxLabelLength))
                            {
                                var label = MdDocument.NormalizeLabel(s.Substring(i + 2, e - i - 2));
                                if (f.Doc.Footnotes.ContainsKey(label))
                                {
                                    rendered(f.FootnoteRef(f.FootnoteNumber(label), label));
                                    i = e + 1;
                                    continue;
                                }
                            }
                        }
                        if (TryLink(ctx, i, n, f, false, out var linkText, out var linkNext, out var _))
                        {
                            rendered(linkText);
                            hasLink = true;
                            i = linkNext;
                            continue;
                        }
                        break;
                    case '<':
                        if (TryAngle(s, i, n, f, out var angleText, out var angleNext, out var isLink))
                        {
                            rendered(angleText);
                            hasLink |= isLink;
                            i = angleNext;
                            continue;
                        }
                        break;
                    case '&':
                        {
                            var m = EntityRe.Match(s, i, n - i);
                            if (m.Success)
                            {
                                //  Decoded (unknown entities are kept as text)
                                rendered(f.Text(WebUtility.HtmlDecode(m.Value).Replace('\0', '�')));
                                i += m.Length;
                                continue;
                            }
                            break;
                        }
                    case ':':
                        {
                            var m = EmojiRe.Match(s, i, n - i);
                            if (m.Success && MdEmoji.Codes.TryGetValue(m.Groups[1].Value, out var emoji))
                            {
                                rendered(f.Emoji(m.Groups[1].Value, emoji));
                                i += m.Length;
                                continue;
                            }
                            break;
                        }
                    case '@':
                        {
                            //  Bare email address, the local part is in the text buffer
                            int k = sb.Length;
                            while ((k > 0) && IsEmailLocal(sb[k - 1]))
                                --k;
                            if ((k < sb.Length) && !inLink)
                            {
                                var m = BareEmailDomainRe.Match(s, i + 1, n - i - 1);
                                var after = i + 1 + m.Length;
                                //  An address may not end with - or _
                                if (m.Success && !((after < n) && ((s[after] == '-') || (s[after] == '_'))))
                                {
                                    var address = sb.ToString(k, sb.Length - k) + "@" + m.Value;
                                    sb.Length = k;
                                    rendered(f.Link(EncodeUrl("mailto:" + address), null, f.Text(address), address));
                                    i = after;
                                    continue;
                                }
                            }
                            break;
                        }
                    case '*':
                    case '_':
                    case '~':
                        {
                            var run = CountRun(s, i, n, c);
                            //  Intraword underscores can't be emphasis (keep in the text, ex: email addresses)
                            if ((c == '_') && (i > start) && Char.IsLetterOrDigit(s[i - 1]) && (i + run < n) && Char.IsLetterOrDigit(s[i + run]))
                            {
                                sb.Append(c, run);
                                i += run;
                                continue;
                            }
                            flush();
                            nodes.Add(CreateDelimiter(s, i, run, c, start, n));
                            i += run;
                            continue;
                        }
                    case '\n':
                        {
                            //  Two or more trailing spaces is a hard line break
                            int k = sb.Length;
                            while ((k > 0) && (sb[k - 1] == ' '))
                                --k;
                            var spaces = sb.Length - k;
                            sb.Length = k;
                            rendered(spaces >= 2 ? f.HardBreak : f.SoftBreak);
                            ++i;
                            while ((i < n) && (s[i] == ' '))
                                ++i;
                            continue;
                        }
                    case 'h':
                    case 'H':
                    case 'f':
                    case 'F':
                    case 'w':
                    case 'W':
                        if (!inLink && TryBareUrl(s, i, start, n, f, out var urlText, out var urlNext))
                        {
                            rendered(urlText);
                            i = urlNext;
                            continue;
                        }
                        break;
                }
                sb.Append(c);
                ++i;
            }
            flush();
            ProcessEmphasis(nodes, f);
            var o = new StringBuilder(end - start + 32);
            foreach (var x in nodes)
            {
                switch (x.Kind)
                {
                    case 0:
                        o.Append(f.Text(x.S));
                        break;
                    case 1:
                        o.Append(x.S);
                        break;
                    default:
                        o.Append(x.Closes);
                        if (x.Count > 0)
                            o.Append(f.Text(new String(x.C, x.Count)));
                        o.Append(x.Opens);
                        break;
                }
            }
            return o.ToString();
        }

        #endregion//Rendering

        #region Emphasis

        static Node CreateDelimiter(String s, int i, int run, char c, int start, int end)
        {
            var prev = i > start ? s[i - 1] : ' ';
            var next = i + run < end ? s[i + run] : ' ';
            var prevWhite = Char.IsWhiteSpace(prev);
            var nextWhite = Char.IsWhiteSpace(next);
            var prevPunct = IsPunct(prev);
            var nextPunct = IsPunct(next);
            var left = !nextWhite && (!nextPunct || prevWhite || prevPunct);
            var right = !prevWhite && (!prevPunct || nextWhite || nextPunct);
            var d = new Node { Kind = 2, C = c, Count = run };
            switch (c)
            {
                case '_':
                    d.CanOpen = left && (!right || prevPunct);
                    d.CanClose = right && (!left || nextPunct);
                    break;
                case '~':
                    d.CanOpen = left && (run <= 2);
                    d.CanClose = right && (run <= 2);
                    break;
                default:
                    d.CanOpen = left;
                    d.CanClose = right;
                    break;
            }
            return d;
        }

        /// <summary>
        /// Match delimiter runs into em, strong and del (the CommonMark delimiter algorithm)
        /// </summary>
        static void ProcessEmphasis(List<Node> nodes, MdFormat f)
        {
            //  Key = (char, closer length % 3, closer can open), value = no opener at or below this index can match (avoids quadratic time for unmatched delimiters)
            var bottom = new Dictionary<(char, int, bool), int>();
            for (int ci = 0; ci < nodes.Count; ++ci)
            {
                var cl = nodes[ci];
                if (cl.Kind != 2)
                    continue;
                while (cl.CanClose && (cl.Count > 0))
                {
                    var key = (cl.C, cl.Count % 3, cl.CanOpen);
                    var lower = bottom.TryGetValue(key, out var b) ? b : -1;
                    int oi = -1;
                    for (int j = ci - 1; j > lower; --j)
                    {
                        var o = nodes[j];
                        if ((o.Kind != 2) || (o.C != cl.C) || !o.CanOpen || (o.Count <= 0))
                            continue;
                        if (cl.C == '~')
                        {
                            if (o.Count != cl.Count)
                                continue;
                        }
                        else if ((o.CanClose || cl.CanOpen) && ((o.Count + cl.Count) % 3 == 0) && !((o.Count % 3 == 0) && (cl.Count % 3 == 0)))
                        {
                            continue;
                        }
                        oi = j;
                        break;
                    }
                    if (oi < 0)
                    {
                        bottom[key] = ci - 1;
                        break;
                    }
                    var op = nodes[oi];
                    var use = cl.C == '~' ? cl.Count : ((op.Count >= 2) && (cl.Count >= 2) ? 2 : 1);
                    var tag = cl.C == '~' ? "del" : (use == 2 ? "strong" : "em");
                    op.Opens = f.EmphasisOpen(tag) + op.Opens;
                    cl.Closes += f.EmphasisClose(tag);
                    op.Count -= use;
                    cl.Count -= use;
                    //  Delimiters between can't be matched any more
                    for (int k = oi + 1; k < ci; ++k)
                    {
                        var x = nodes[k];
                        if (x.Kind == 2)
                        {
                            x.CanOpen = false;
                            x.CanClose = false;
                        }
                    }
                }
            }
        }

        #endregion//Emphasis

        #region Constructs

        /// <summary>
        /// Math: $$display$$, $`inline`$ or $inline$ (the opening $ may not be followed by a space, the closing $ may not be preceded by a space or followed by a digit)
        /// </summary>
        static bool TryMath(Context ctx, int i, int n, MdFormat f, out String text, out int next)
        {
            var s = ctx.S;
            text = null;
            next = 0;
            if ((i + 1 < n) && (s[i + 1] == '$'))
            {
                var e = i + 2 < n ? s.IndexOf("$$", i + 2, n - i - 2, StringComparison.Ordinal) : -1;
                if (e <= i + 2)
                    return false;
                text = f.Math(s.Substring(i + 2, e - i - 2).Trim(), true);
                next = e + 2;
                return true;
            }
            if ((i + 1 < n) && (s[i + 1] == '`'))
            {
                var e = i + 2 < n ? s.IndexOf("`$", i + 2, n - i - 2, StringComparison.Ordinal) : -1;
                if (e <= i + 2)
                    return false;
                text = f.Math(s.Substring(i + 2, e - i - 2), false);
                next = e + 2;
                return true;
            }
            if ((i + 1 >= n) || Char.IsWhiteSpace(s[i + 1]))
                return false;
            //  If can close doesn't depend on the opener, so if a search failed, searches from later positions will fail too
            if (ctx.MathFailed.TryGetValue(n, out var failed) && (i + 2 >= failed))
                return false;
            for (int e = i + 2; e < n; ++e)
            {
                var c = s[e];
                if (c == '\\')
                {
                    ++e;
                    continue;
                }
                if (c != '$')
                    continue;
                if (Char.IsWhiteSpace(s[e - 1]))
                    continue;
                if ((e + 1 < n) && Char.IsDigit(s[e + 1]))
                    continue;
                text = f.Math(s.Substring(i + 1, e - i - 1), false);
                next = e + 1;
                return true;
            }
            ctx.MathFailed[n] = i + 2;
            return false;
        }

        /// <summary>
        /// Parse an inline link destination and title: (url "title")
        /// </summary>
        static bool TryInlineDestination(String s, int p, int n, out String url, out String title, out int next)
        {
            url = null;
            title = null;
            next = 0;
            int k = p + 1;
            while ((k < n) && IsAsciiWhiteSpace(s[k]))
                ++k;
            if ((k < n) && (s[k] == '<'))
            {
                var e = k + 1;
                while ((e < n) && (s[e] != '>') && (s[e] != '<') && (s[e] != '\n'))
                    e += (s[e] == '\\') && (e + 1 < n) ? 2 : 1;
                if ((e >= n) || (s[e] != '>'))
                    return false;
                url = s.Substring(k + 1, e - k - 1);
                k = e + 1;
            }
            else
            {
                int depth = 0;
                int st = k;
                while (k < n)
                {
                    var c = s[k];
                    if ((c == '\\') && (k + 1 < n))
                    {
                        k += 2;
                        continue;
                    }
                    if (IsAsciiWhiteSpace(c) || Char.IsControl(c))
                        break;
                    if (c == '(')
                        ++depth;
                    else if (c == ')')
                    {
                        if (depth == 0)
                            break;
                        --depth;
                    }
                    ++k;
                }
                if (depth != 0)
                    return false;
                url = s.Substring(st, k - st);
            }
            int ws = k;
            while ((k < n) && IsAsciiWhiteSpace(s[k]))
                ++k;
            if ((k < n) && (k > ws) && ((s[k] == '"') || (s[k] == '\'') || (s[k] == '(')))
            {
                var close = s[k] == '(' ? ')' : s[k];
                var e = k + 1;
                while ((e < n) && (s[e] != close))
                    e += (s[e] == '\\') && (e + 1 < n) ? 2 : 1;
                if (e >= n)
                    return false;
                title = MdDocument.Unescape(s.Substring(k + 1, e - k - 1));
                k = e + 1;
                while ((k < n) && IsAsciiWhiteSpace(s[k]))
                    ++k;
            }
            if ((k >= n) || (s[k] != ')'))
                return false;
            url = MdDocument.Unescape(url);
            next = k + 1;
            return true;
        }

        /// <summary>
        /// Try to parse a link or an image starting at a '['
        /// </summary>
        /// <param name="ctx">The context</param>
        /// <param name="start">The position of the '['</param>
        /// <param name="n">The end of the current range</param>
        /// <param name="f">The output format</param>
        /// <param name="image">True if this is an image</param>
        /// <param name="text">The rendered link / image</param>
        /// <param name="next">The position after the link / image</param>
        /// <param name="hasLink">True if the image contains a link</param>
        static bool TryLink(Context ctx, int start, int n, MdFormat f, bool image, out String text, out int next, out bool hasLink)
        {
            text = null;
            next = 0;
            hasLink = false;
            if (ctx.LinkDepth >= MaxLinkDepth)
                return false;
            var close = ctx.BracketEnd(start);
            if ((close < 0) || (close >= n))
                return false;
            var s = ctx.S;
            var p = close + 1;
            String url;
            String title;
            if (!((p < n) && (s[p] == '(') && TryInlineDestination(s, p, n, out url, out title, out next)))
            {
                //  Reference links: [text][label], [text][] or [text]
                int labelStart = start + 1;
                int labelEnd = close;
                next = p;
                if ((p < n) && (s[p] == '['))
                {
                    //  Find the end of the label (it may not contain unescaped brackets)
                    int e = p + 1;
                    while ((e < n) && (s[e] != ']') && (s[e] != '['))
                        e += (s[e] == '\\') && (e + 1 < n) ? 2 : 1;
                    if ((e < n) && (s[e] == ']'))
                    {
                        if (s.AsSpan(p + 1, e - p - 1).Trim().Length > 0)
                        {
                            labelStart = p + 1;
                            labelEnd = e;
                        }
                        next = e + 1;
                    }
                }
                if (labelEnd - labelStart > MaxLabelLength)
                    return false;
                if (!f.Doc.Refs.TryGetValue(MdDocument.NormalizeLabel(s.Substring(labelStart, labelEnd - labelStart)), out var r))
                    return false;
                url = r.Url;
                title = r.Title;
            }
            var safe = SafeUrl(url);
            if (image)
            {
                var (alt, altHasLink) = RenderRange(ctx, start + 1, close, f.PlainFormat);
                hasLink = altHasLink;
                text = safe == null ? f.Text(alt) : f.Image(safe, alt, title);
                return true;
            }
            //  Links may not contain other links, the inner link wins
            var (inner, innerHasLink) = RenderRange(ctx, start + 1, close, f);
            if (innerHasLink)
                return false;
            if (safe == null)
            {
                text = inner;
                return true;
            }
            text = f.Link(safe, title, inner, RenderRange(ctx, start + 1, close, f.PlainFormat).Text);
            return true;
        }

        /// <summary>
        /// Try to parse an autolink, an allowed html tag or a comment starting at a '&lt;'
        /// </summary>
        static bool TryAngle(String s, int i, int n, MdFormat f, out String text, out int next, out bool isLink)
        {
            text = null;
            next = 0;
            isLink = false;
            if ((n - i >= 4) && (String.CompareOrdinal(s, i, "<!--", 0, 4) == 0))
            {
                var e = s.IndexOf("-->", i + 4, n - i - 4, StringComparison.Ordinal);
                if (e < 0)
                    return false;
                text = "";
                next = e + 3;
                return true;
            }
            var len = n - i;
            var m = AutoLinkRe.Match(s, i, len);
            if (m.Success)
            {
                var url = m.Groups[1].Value;
                var safe = SafeUrl(url);
                if (safe == null)
                    return false;
                text = f.Link(safe, null, f.Text(url), url);
                next = i + m.Length;
                isLink = true;
                return true;
            }
            m = EmailLinkRe.Match(s, i, len);
            if (m.Success)
            {
                var email = m.Groups[1].Value;
                text = f.Link(EncodeUrl("mailto:" + email), null, f.Text(email), email);
                next = i + m.Length;
                isLink = true;
                return true;
            }
            m = HtmlTagRe.Match(s, i, len);
            if (m.Success && AllowedTags.TryGetValue(m.Groups[2].Value, out var allowed))
            {
                var tag = m.Groups[2].Value.ToLowerInvariant();
                var closing = m.Groups[1].Length > 0;
                next = i + m.Length;
                if (closing)
                {
                    text = VoidTags.Contains(tag) ? "" : f.Tag(tag, true, Array.Empty<MdAttribute>());
                    return true;
                }
                var attributes = new List<MdAttribute>();
                foreach (Match a in AttributeRe.Matches(m.Groups[3].Value))
                {
                    var name = a.Groups[1].Value.ToLowerInvariant();
                    if ((name != "title") && !allowed.Contains(name))
                        continue;
                    var value = WebUtility.HtmlDecode(a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value);
                    switch (name)
                    {
                        case "href":
                        case "src":
                            value = SafeUrl(value);
                            break;
                        case "width":
                        case "height":
                        case "colspan":
                        case "rowspan":
                        case "start":
                            value = NumberRe.IsMatch(value.Trim()) ? value.Trim() : null;
                            break;
                        case "align":
                            value = AlignRe.IsMatch(value.Trim()) ? value.Trim().ToLowerInvariant() : null;
                            break;
                        case "open":
                            value = "";
                            break;
                    }
                    if (value == null)
                        continue;
                    if (!attributes.Any(x => x.Name == name))
                        attributes.Add(new MdAttribute(name, value));
                }
                //  An image without a source is useless
                if ((tag == "img") && !attributes.Any(x => x.Name == "src"))
                {
                    text = "";
                    return true;
                }
                text = f.Tag(tag, false, attributes);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to parse a bare url (http://, https://, ftp:// or www.)
        /// </summary>
        static bool TryBareUrl(String s, int i, int start, int n, MdFormat f, out String text, out int next)
        {
            text = null;
            next = 0;
            if (i > start)
            {
                var prev = s[i - 1];
                if (!(Char.IsWhiteSpace(prev) || (prev == '(') || (prev == '*') || (prev == '_') || (prev == '~')))
                    return false;
            }
            var m = BareUrlRe.Match(s, i, n - i);
            if (!m.Success)
                return false;
            var url = m.Value;
            for (; ; )
            {
                if (url.Length <= 0)
                    return false;
                var last = url[^1];
                if ("?!.,:*_~'\"".Contains(last))
                {
                    url = url.Substring(0, url.Length - 1);
                    continue;
                }
                if ((last == ')') && (url.Count(x => x == '(') < url.Count(x => x == ')')))
                {
                    url = url.Substring(0, url.Length - 1);
                    continue;
                }
                if (last == ';')
                {
                    var em = TrailingEntityRe.Match(url);
                    if (em.Success)
                    {
                        url = url.Substring(0, em.Index);
                        continue;
                    }
                }
                break;
            }
            //  Must have something after the prefix
            var prefixLength = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? 4 : url.IndexOf("//", StringComparison.Ordinal) + 2;
            if (url.Length <= prefixLength)
                return false;
            var href = prefixLength == 4 ? "http://" + url : url;
            text = f.Link(EncodeUrl(href), null, f.Text(url), url);
            next = i + url.Length;
            return true;
        }

        #endregion//Constructs
    }
}
