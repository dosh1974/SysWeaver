using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SysWeaver.Markdown
{
    /// <summary>
    /// A parsed markdown document (GitHub flavored markdown: CommonMark + GFM extensions + some GitHub extras).
    /// The markdown is first parsed into blocks (so that reference links and footnotes can be defined anywhere), inline content is parsed when rendering (see <see cref="MdInline"/>).
    /// </summary>
    sealed class MdDocument
    {
        public List<MdBlock> Blocks;

        /// <summary>
        /// Link reference definitions, key = normalized label
        /// </summary>
        public readonly Dictionary<String, (String Url, String Title)> Refs = new(StringComparer.Ordinal);

        /// <summary>
        /// Footnote definitions, key = normalized label
        /// </summary>
        public readonly Dictionary<String, List<MdBlock>> Footnotes = new(StringComparer.Ordinal);

        public static MdDocument Parse(String markdown)
        {
            var doc = new MdDocument();
            var lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace('\0', '�').Split('\n').Select(ExpandLeadingTabs).ToList();
            doc.Blocks = doc.ParseBlocks(lines);
            return doc;
        }

        public static String NormalizeLabel(String label) => Regex.Replace(label.Trim(), @"\s+", " ").ToUpperInvariant();

        #region Helpers

        static readonly Regex AtxRe = new(@"^ {0,3}(#{1,6})(?:[ \t]+(.*?))?[ \t]*$", RegexOptions.Compiled);
        static readonly Regex AtxCloseRe = new(@"(?:^|[ \t]+)#+$", RegexOptions.Compiled);
        static readonly Regex RuleRe = new(@"^ {0,3}(?:(?:\*[ \t]*){3,}|(?:-[ \t]*){3,}|(?:_[ \t]*){3,})$", RegexOptions.Compiled);
        static readonly Regex SetextRe = new(@"^ {0,3}(=+|-+)[ \t]*$", RegexOptions.Compiled);
        static readonly Regex FenceRe = new(@"^( {0,3})(`{3,}|~{3,})(.*)$", RegexOptions.Compiled);
        static readonly Regex MathFenceRe = new(@"^ {0,3}\$\$[ \t]*$", RegexOptions.Compiled);
        static readonly Regex QuoteRe = new(@"^ {0,3}>", RegexOptions.Compiled);
        static readonly Regex BulletRe = new(@"^( {0,3})([-+*])([ \t]+|$)(.*)$", RegexOptions.Compiled);
        static readonly Regex OrderedRe = new(@"^( {0,3})(\d{1,9})([.)])([ \t]+|$)(.*)$", RegexOptions.Compiled);
        static readonly Regex TableDelimRe = new(@"^ {0,3}\|?[ \t]*:?-+:?[ \t]*(?:\|[ \t]*:?-+:?[ \t]*)*\|?[ \t]*$", RegexOptions.Compiled);
        /// <summary>
        /// A link reference definition (may span multiple lines), matched at a position in the paragraph text
        /// </summary>
        static readonly Regex RefDefRe = new(@"\G {0,3}\[((?:[^\[\]\\]|\\.){1,999})\]:[ \t]*(?:\n[ \t]*)?(<(?:[^<>\n\\]|\\.)*>|[^\s<]\S*)(?:(?:[ \t]*\n[ \t]*|[ \t]+)(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|\((?:[^()\\]|\\.)*\)))?[ \t]*(?:\n|$)", RegexOptions.Compiled);
        static readonly Regex FootnoteDefRe = new(@"^ {0,3}\[\^([^\]\s]+)\]:[ \t]?(.*)$", RegexOptions.Compiled);
        static readonly Regex TaskRe = new(@"^\[([ xX])\](?:[ \t]+|$)", RegexOptions.Compiled);
        static readonly Regex AlertRe = new(@"^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\][ \t]*(?:\n|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex HtmlBlockRe = new(@"^ {0,3}</?(?:details|summary|div|p|dl|dt|dd|table|caption|thead|tbody|tfoot|tr|th|td|ul|ol|li|h[1-6]|blockquote|pre|hr|center|figure|figcaption)(?=[\s/>]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex CommentStartRe = new(@"^ {0,3}<!--", RegexOptions.Compiled);

        static String ExpandLeadingTabs(String line)
        {
            int i = 0;
            while ((i < line.Length) && ((line[i] == ' ') || (line[i] == '\t')))
                ++i;
            if (line.IndexOf('\t', 0, i) < 0)
                return line;
            var sb = new StringBuilder(line.Length + 8);
            for (int j = 0; j < i; ++j)
            {
                if (line[j] == '\t')
                    sb.Append(' ', 4 - (sb.Length & 3));
                else
                    sb.Append(' ');
            }
            sb.Append(line, i, line.Length - i);
            return sb.ToString();
        }

        static bool IsBlank(String line)
        {
            foreach (var c in line)
                if ((c != ' ') && (c != '\t'))
                    return false;
            return true;
        }

        static int Indent(String line)
        {
            int i = 0;
            while ((i < line.Length) && (line[i] == ' '))
                ++i;
            return i;
        }

        static String RemoveIndent(String line, int count)
        {
            int i = 0;
            while ((i < count) && (i < line.Length) && (line[i] == ' '))
                ++i;
            return line.Substring(i);
        }

        static readonly Regex EntityRe = new(@"\G&(?:[A-Za-z][A-Za-z0-9]{1,31}|#[0-9]{1,7}|#[xX][0-9A-Fa-f]{1,6});", RegexOptions.Compiled);

        /// <summary>
        /// Process backslash escapes and html entities (used for urls, titles and info strings)
        /// </summary>
        public static String Unescape(String s)
        {
            if ((s.IndexOf('\\') < 0) && (s.IndexOf('&') < 0))
                return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if ((c == '\\') && (i + 1 < s.Length) && MdInline.IsAsciiPunct(s[i + 1]))
                {
                    sb.Append(s[++i]);
                    continue;
                }
                if (c == '&')
                {
                    var m = EntityRe.Match(s, i);
                    if (m.Success)
                    {
                        sb.Append(System.Net.WebUtility.HtmlDecode(m.Value).Replace('\0', '�'));
                        i += m.Length - 1;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// A lazy continuation line is always paragraph text, escape the first char if it would start a block
        /// </summary>
        static String LazyLine(String line)
        {
            var t = line.TrimStart(' ');
            if (!(IsBlockStart(t) || SetextRe.IsMatch(t) || TableDelimRe.IsMatch(t)))
                return t;
            int k = 0;
            while ((k < t.Length) && Char.IsAsciiDigit(t[k]))
                ++k;
            if ((k < t.Length) && MdInline.IsAsciiPunct(t[k]))
                return String.Concat(t.AsSpan(0, k), "\\", t.AsSpan(k));
            return t;
        }

        sealed class ListStart
        {
            public bool Ordered;
            /// <summary>
            /// The bullet char or the ordered delimiter ('.' or ')')
            /// </summary>
            public char Marker;
            public int Start;
            public int ContentIndent;
            public String Content;
            public bool Empty;
        }

        static ListStart GetListStart(String line)
        {
            var m = BulletRe.Match(line);
            bool ordered = false;
            if (!m.Success)
            {
                m = OrderedRe.Match(line);
                if (!m.Success)
                    return null;
                ordered = true;
            }
            var g = m.Groups;
            var indent = g[1].Length;
            var markerLen = ordered ? g[2].Length + 1 : 1;
            var spacing = g[ordered ? 4 : 3].Value.Replace("\t", "    ").Length;
            var rest = g[ordered ? 5 : 4].Value;
            var s = new ListStart
            {
                Ordered = ordered,
                Marker = ordered ? g[3].Value[0] : g[2].Value[0],
                Start = ordered ? int.Parse(g[2].Value) : 1,
            };
            if (rest.Length == 0)
            {
                s.Empty = true;
                s.ContentIndent = indent + markerLen + 1;
                s.Content = "";
            }
            else if (spacing > 4)
            {
                //  Starts with indented code
                s.ContentIndent = indent + markerLen + 1;
                s.Content = new String(' ', spacing - 1) + rest;
            }
            else
            {
                s.ContentIndent = indent + markerLen + spacing;
                s.Content = rest;
            }
            return s;
        }

        static bool IsFence(String line)
        {
            var m = FenceRe.Match(line);
            return m.Success && !((m.Groups[2].Value[0] == '`') && m.Groups[3].Value.Contains('`'));
        }

        /// <summary>
        /// True if the line starts a block that interrupts a paragraph
        /// </summary>
        static bool IsBlockStart(String line)
        {
            if (IsBlank(line) || RuleRe.IsMatch(line) || AtxRe.IsMatch(line) || QuoteRe.IsMatch(line) || IsFence(line) || MathFenceRe.IsMatch(line) || HtmlBlockRe.IsMatch(line) || CommentStartRe.IsMatch(line))
                return true;
            var l = GetListStart(line);
            return (l != null) && !l.Empty && ((!l.Ordered) || (l.Start == 1));
        }

        #endregion//Helpers

        #region Parsing

        /// <summary>
        /// Container blocks (quotes, lists etc) nested deeper than this are rendered as a paragraph (avoids stack overflows on malicious input)
        /// </summary>
        const int MaxDepth = 32;

        int Depth;

        List<MdBlock> ParseBlocks(List<String> lines)
        {
            if (Depth >= MaxDepth)
            {
                var text = String.Join('\n', lines.Where(x => !IsBlank(x)).Select(x => x.Trim())).Trim();
                return text.Length == 0 ? new List<MdBlock>() : new List<MdBlock> { new MdParagraph { Text = text } };
            }
            ++Depth;
            try
            {
                return ParseBlocksInternal(lines);
            }
            finally
            {
                --Depth;
            }
        }

        List<MdBlock> ParseBlocksInternal(List<String> lines)
        {
            var blocks = new List<MdBlock>();
            List<String> para = null;
            //  True if there was a blank line since the last block
            bool blank = false;
            void add(MdBlock b)
            {
                b.BlankBefore = blank && (blocks.Count > 0);
                blank = false;
                blocks.Add(b);
            }
            void closePara()
            {
                if (para == null)
                    return;
                var text = ExtractRefs(para);
                if (text != null)
                    add(new MdParagraph { Text = text });
                para = null;
            }
            int n = lines.Count;
            int i = 0;
            while (i < n)
            {
                var line = lines[i];
                if (IsBlank(line))
                {
                    closePara();
                    blank = true;
                    ++i;
                    continue;
                }
                var indent = Indent(line);
                //  Indented code
                if ((para == null) && (indent >= 4))
                {
                    var code = new List<String>();
                    while ((i < n) && (IsBlank(lines[i]) || (Indent(lines[i]) >= 4)))
                        code.Add(RemoveIndent(lines[i++], 4));
                    var trailingBlank = false;
                    while ((code.Count > 0) && IsBlank(code[^1]))
                    {
                        code.RemoveAt(code.Count - 1);
                        trailingBlank = true;
                    }
                    add(new MdCode { Text = String.Join('\n', code) + "\n" });
                    blank = trailingBlank;
                    continue;
                }
                //  Setext heading
                if (para != null)
                {
                    var sm = SetextRe.Match(line);
                    if (sm.Success)
                    {
                        var text = ExtractRefs(para);
                        para = null;
                        if (text != null)
                        {
                            add(new MdHeading { Level = sm.Groups[1].Value[0] == '=' ? 1 : 2, Text = text });
                            ++i;
                            continue;
                        }
                    }
                }
                //  Thematic break
                if (RuleRe.IsMatch(line))
                {
                    closePara();
                    add(new MdRule());
                    ++i;
                    continue;
                }
                //  ATX heading
                var am = AtxRe.Match(line);
                if (am.Success)
                {
                    closePara();
                    var text = AtxCloseRe.Replace(am.Groups[2].Value, "").Trim();
                    add(new MdHeading { Level = am.Groups[1].Length, Text = text });
                    ++i;
                    continue;
                }
                //  Fenced code (or math)
                var fm = FenceRe.Match(line);
                if (fm.Success && !((fm.Groups[2].Value[0] == '`') && fm.Groups[3].Value.Contains('`')))
                {
                    closePara();
                    var fenceIndent = fm.Groups[1].Length;
                    var fence = fm.Groups[2].Value;
                    var info = Unescape(fm.Groups[3].Value.Trim());
                    var close = new Regex("^ {0,3}" + Regex.Escape(fence[0].ToString()) + "{" + fence.Length + ",}[ \\t]*$");
                    var code = new List<String>();
                    ++i;
                    while (i < n)
                    {
                        var l = lines[i++];
                        if (close.IsMatch(l))
                            break;
                        code.Add(RemoveIndent(l, fenceIndent));
                    }
                    var sp = info.IndexOfAny([' ', '\t']);
                    var language = info.Length == 0 ? null : (sp < 0 ? info : info.Substring(0, sp));
                    var codeText = code.Count > 0 ? String.Join('\n', code) + "\n" : "";
                    if (String.Equals(language, "math", StringComparison.OrdinalIgnoreCase))
                        add(new MdMath { Text = codeText.TrimEnd('\n') });
                    else
                        add(new MdCode { Language = language, Text = codeText });
                    continue;
                }
                //  Math block: $$ ... $$
                if (MathFenceRe.IsMatch(line))
                {
                    closePara();
                    var math = new List<String>();
                    ++i;
                    while (i < n)
                    {
                        var l = lines[i++];
                        if (MathFenceRe.IsMatch(l))
                            break;
                        math.Add(l);
                    }
                    add(new MdMath { Text = String.Join('\n', math).Trim() });
                    continue;
                }
                //  Html comment (removed)
                if (CommentStartRe.IsMatch(line))
                {
                    closePara();
                    while (i < n)
                    {
                        if (lines[i++].Contains("-->"))
                            break;
                    }
                    continue;
                }
                //  Html block (sanitized when rendered)
                if (HtmlBlockRe.IsMatch(line))
                {
                    closePara();
                    var html = new List<String>();
                    while ((i < n) && !IsBlank(lines[i]))
                        html.Add(lines[i++]);
                    add(new MdHtml { Text = String.Join('\n', html).Trim() });
                    continue;
                }
                //  Block quote (or alert)
                if (QuoteRe.IsMatch(line))
                {
                    closePara();
                    var q = new List<String>();
                    while (i < n)
                    {
                        var l = lines[i];
                        if (QuoteRe.IsMatch(l))
                        {
                            var p = l.IndexOf('>') + 1;
                            if ((p < l.Length) && (l[p] == ' '))
                                ++p;
                            q.Add(ExpandLeadingTabs(l.Substring(p)));
                            ++i;
                            continue;
                        }
                        //  Lazy continuation of a paragraph
                        if ((q.Count > 0) && !IsBlank(q[^1]) && !IsBlockStart(l) && !IsFence(q[^1]) && (Indent(q[^1]) < 4))
                        {
                            q.Add(LazyLine(l));
                            ++i;
                            continue;
                        }
                        break;
                    }
                    var children = ParseBlocks(q);
                    if ((children.Count > 0) && (children[0] is MdParagraph fp))
                    {
                        var alert = AlertRe.Match(fp.Text);
                        if (alert.Success)
                        {
                            fp.Text = fp.Text.Substring(alert.Length).Trim();
                            if (fp.Text.Length == 0)
                                children.RemoveAt(0);
                            add(new MdAlert { Kind = Enum.Parse<MdAlertKind>(alert.Groups[1].Value, true), Children = children });
                            continue;
                        }
                    }
                    add(new MdQuote { Children = children });
                    continue;
                }
                //  Footnote definition
                if (para == null)
                {
                    var fn = FootnoteDefRe.Match(line);
                    if (fn.Success)
                    {
                        var content = new List<String> { fn.Groups[2].Value };
                        ++i;
                        bool prevBlank = false;
                        while (i < n)
                        {
                            var l = lines[i];
                            if (IsBlank(l))
                            {
                                content.Add("");
                                prevBlank = true;
                                ++i;
                                continue;
                            }
                            if (Indent(l) >= 4)
                            {
                                content.Add(RemoveIndent(l, 4));
                                prevBlank = false;
                                ++i;
                                continue;
                            }
                            if (!prevBlank && !IsBlockStart(l) && !FootnoteDefRe.IsMatch(l))
                            {
                                content.Add(l);
                                ++i;
                                continue;
                            }
                            break;
                        }
                        Footnotes.TryAdd(NormalizeLabel(fn.Groups[1].Value), ParseBlocks(content));
                        continue;
                    }
                }
                //  List
                var ls = GetListStart(line);
                if ((ls != null) && ((para == null) || (!ls.Empty && ((!ls.Ordered) || (ls.Start == 1)))))
                {
                    closePara();
                    var list = ParseList(lines, ref i, ls);
                    add(list);
                    blank = list.EndedWithBlank;
                    continue;
                }
                //  Table
                if ((para == null) && (i + 1 < n) && line.Contains('|') && lines[i + 1].Contains('|') && TableDelimRe.IsMatch(lines[i + 1]))
                {
                    var header = SplitRow(line);
                    var delim = SplitRow(lines[i + 1]);
                    if (header.Length == delim.Length)
                    {
                        var t = new MdTable
                        {
                            Header = header,
                            Align = delim.Select(x =>
                            {
                                var l = x.StartsWith(':');
                                var r = x.EndsWith(':');
                                return l ? (r ? MdAlign.Center : MdAlign.Left) : (r ? MdAlign.Right : MdAlign.None);
                            }).ToArray(),
                        };
                        i += 2;
                        while ((i < n) && !IsBlockStart(lines[i]))
                        {
                            var row = SplitRow(lines[i++]);
                            if (row.Length != header.Length)
                                Array.Resize(ref row, header.Length);
                            t.Rows.Add(row);
                        }
                        add(t);
                        continue;
                    }
                }
                //  Paragraph
                para ??= new List<String>();
                para.Add(line.TrimStart(' '));
                ++i;
            }
            closePara();
            return blocks;
        }

        MdList ParseList(List<String> lines, ref int i, ListStart first)
        {
            var list = new MdList { Ordered = first.Ordered, Start = first.Start };
            int n = lines.Count;
            List<String> cur = null;
            int contentIndent = 0;
            //  True if the previous line was blank
            bool prevBlank = false;
            //  True if the current item started with an empty line (only the marker)
            bool startedEmpty = false;
            bool firstItem = true;
            void finishItem()
            {
                if (cur == null)
                    return;
                while ((cur.Count > 0) && IsBlank(cur[^1]))
                    cur.RemoveAt(cur.Count - 1);
                var item = new MdListItem();
                if (cur.Count > 0)
                {
                    var tm = TaskRe.Match(cur[0]);
                    if (tm.Success)
                    {
                        item.Task = tm.Groups[1].Value == " " ? MdTask.Open : MdTask.Done;
                        cur[0] = cur[0].Substring(tm.Length);
                    }
                }
                item.Children = ParseBlocks(cur);
                //  A blank line between two (direct) child blocks makes the list loose
                if (item.Children.Skip(1).Any(x => x.BlankBefore))
                    list.Loose = true;
                list.Items.Add(item);
                cur = null;
            }
            while (i < n)
            {
                var line = lines[i];
                if (IsBlank(line))
                {
                    if (cur == null)
                    {
                        //  Blank lines after an (empty) item that was ended
                        if (firstItem)
                            break;
                        prevBlank = true;
                        ++i;
                        continue;
                    }
                    //  An item can begin with at most one blank line
                    if (startedEmpty && (cur.Count == 1) && (cur[0].Length == 0))
                        finishItem();
                    else
                        cur.Add("");
                    prevBlank = true;
                    ++i;
                    continue;
                }
                if ((cur != null) && (Indent(line) >= contentIndent))
                {
                    cur.Add(line.Substring(contentIndent));
                    prevBlank = false;
                    ++i;
                    continue;
                }
                var ls = firstItem ? first : GetListStart(line);
                var isRule = RuleRe.IsMatch(line);
                if ((ls != null) && (ls.Ordered == first.Ordered) && (ls.Marker == first.Marker) && !isRule)
                {
                    if (!firstItem)
                    {
                        finishItem();
                        if (prevBlank)
                            list.Loose = true;
                    }
                    firstItem = false;
                    cur = new List<String> { ls.Content };
                    contentIndent = ls.ContentIndent;
                    startedEmpty = ls.Empty;
                    prevBlank = false;
                    ++i;
                    continue;
                }
                //  Another kind of list item starts a new list
                if ((ls != null) && !isRule)
                    break;
                //  Lazy continuation of a paragraph
                if ((cur != null) && !prevBlank && !IsBlockStart(line) && (cur.Count > 0) && !IsBlank(cur[^1]))
                {
                    cur.Add(LazyLine(line));
                    ++i;
                    continue;
                }
                break;
            }
            finishItem();
            list.EndedWithBlank = prevBlank;
            return list;
        }

        static String[] SplitRow(String line)
        {
            var s = line.Trim();
            if (s.StartsWith('|'))
                s = s.Substring(1);
            if (s.EndsWith('|') && !s.EndsWith("\\|"))
                s = s.Substring(0, s.Length - 1);
            var cells = new List<String>();
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if ((c == '\\') && (i + 1 < s.Length) && (s[i + 1] == '|'))
                {
                    sb.Append('|');
                    ++i;
                    continue;
                }
                if (c == '|')
                {
                    cells.Add(sb.ToString().Trim());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            cells.Add(sb.ToString().Trim());
            return cells.ToArray();
        }

        /// <summary>
        /// Extract link reference definitions from the start of a paragraph
        /// </summary>
        /// <returns>The remaining paragraph text, or null if nothing remains</returns>
        String ExtractRefs(List<String> para)
        {
            var text = String.Join('\n', para);
            int pos = 0;
            //  Definitions may span multiple lines
            while (pos < text.Length)
            {
                var m = RefDefRe.Match(text, pos);
                if (!m.Success)
                    break;
                var label = m.Groups[1].Value;
                //  Footnotes are not link definitions
                if ((label.Trim().Length == 0) || label.StartsWith('^'))
                    break;
                var url = m.Groups[2].Value;
                if (url.StartsWith('<'))
                    url = url.Substring(1, url.Length - 2);
                var title = m.Groups[3].Success ? m.Groups[3].Value : null;
                if (title != null)
                    title = Unescape(title.Substring(1, title.Length - 2));
                Refs.TryAdd(NormalizeLabel(label), (Unescape(url), title));
                pos += m.Length;
            }
            var rest = text.Substring(pos).TrimEnd();
            return rest.Length == 0 ? null : rest;
        }

        #endregion//Parsing
    }

    abstract class MdBlock
    {
        /// <summary>
        /// True if the block is preceded by a blank line (and isn't the first block of its container), used to determine if a list is loose
        /// </summary>
        public bool BlankBefore;
    }

    sealed class MdHeading : MdBlock
    {
        public int Level;
        public String Text;
    }

    sealed class MdParagraph : MdBlock
    {
        public String Text;
    }

    sealed class MdCode : MdBlock
    {
        public String Language;
        public String Text;
    }

    sealed class MdMath : MdBlock
    {
        public String Text;
    }

    sealed class MdRule : MdBlock
    {
    }

    sealed class MdQuote : MdBlock
    {
        public List<MdBlock> Children;
    }

    enum MdAlertKind
    {
        Note,
        Tip,
        Important,
        Warning,
        Caution,
    }

    sealed class MdAlert : MdBlock
    {
        public MdAlertKind Kind;
        public List<MdBlock> Children;
    }

    /// <summary>
    /// Raw html (sanitized when rendered)
    /// </summary>
    sealed class MdHtml : MdBlock
    {
        public String Text;
    }

    sealed class MdList : MdBlock
    {
        public bool Ordered;
        public int Start;
        public bool Loose;
        /// <summary>
        /// True if the list was followed by blank lines (consumed by the list)
        /// </summary>
        public bool EndedWithBlank;
        public readonly List<MdListItem> Items = new();
    }

    enum MdTask
    {
        None,
        Open,
        Done,
    }

    sealed class MdListItem
    {
        public List<MdBlock> Children;
        public MdTask Task;
    }

    enum MdAlign
    {
        None,
        Left,
        Center,
        Right,
    }

    sealed class MdTable : MdBlock
    {
        public MdAlign[] Align;
        public String[] Header;
        public readonly List<String[]> Rows = new();
    }
}
