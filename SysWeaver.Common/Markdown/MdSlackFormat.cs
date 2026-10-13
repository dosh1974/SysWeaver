using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SysWeaver.Markdown
{
    /// <summary>
    /// Renders markdown as Slack "mrkdwn" (the text format of Slack messages).
    /// Features that Slack doesn't have are emulated: headings are bold, lists use bullet chars, tables are rendered as aligned text in a code block,
    /// alerts are block quotes with a bold title, footnotes are listed at the end, nested block quotes are flattened.
    /// </summary>
    sealed class MdSlackFormat : MdFormat
    {
        public MdSlackFormat(MdDocument doc) : base(doc)
        {
        }

        /// <summary>
        /// Render the document
        /// </summary>
        public String Render()
        {
            var text = RenderBlocks(Doc.Blocks, 0, false);
            if (FootnoteOrder.Count > 0)
            {
                var sb = new StringBuilder(text);
                sb.Append("\n\n").Append(Rule);
                //  Footnotes may reference other footnotes (the list grows while rendering)
                for (int i = 0; i < FootnoteOrder.Count; ++i)
                    sb.Append('\n').Append('[').Append(i + 1).Append("] ").Append(RenderBlocks(Doc.Footnotes[FootnoteOrder[i]], 0, true));
                text = sb.ToString();
            }
            return text.Trim('\n');
        }

        const String Rule = "──────────────────────";

        static readonly String[] Bullets = ["•", "◦", "▪"];

        static readonly Dictionary<MdAlertKind, String> Alerts = new()
        {
            [MdAlertKind.Note] = "ℹ️ Note",
            [MdAlertKind.Tip] = "💡 Tip",
            [MdAlertKind.Important] = "❗ Important",
            [MdAlertKind.Warning] = "⚠️ Warning",
            [MdAlertKind.Caution] = "🛑 Caution",
        };

        #region Escaping

        /// <summary>
        /// Escape the characters that are control characters in Slack (&amp;, &lt; and &gt;)
        /// </summary>
        static String Esc(String s)
        {
            if (s.IndexOfAny(EscChars) < 0)
                return s;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        static readonly char[] EscChars = ['&', '<', '>'];

        /// <summary>
        /// Escape an url for use in a Slack link
        /// </summary>
        static String EscUrl(String url) => Esc(url).Replace("|", "%7C");

        /// <summary>
        /// Escape a link label (may not contain '|')
        /// </summary>
        static String EscLabel(String label) => Esc(label.Replace('|', '∣').Replace('\n', ' ').Trim());

        #endregion//Escaping

        #region Inline

        /// <summary>
        /// Slack has no way to escape formatting chars, literal chars that could be interpreted as formatting are replaced with look alikes
        /// </summary>
        public override String Text(String text)
        {
            if (text.IndexOfAny(FormatChars) < 0)
                return Esc(text);
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; ++i)
            {
                var c = text[i];
                switch (c)
                {
                    case '*':
                        c = '∗';
                        break;
                    case '~':
                        c = '∼';
                        break;
                    case '`':
                        c = 'ˋ';
                        break;
                    case '_':
                        //  Intraword underscores are safe (ex: snake_case)
                        if (!((i > 0) && Char.IsLetterOrDigit(text[i - 1]) && (i + 1 < text.Length) && Char.IsLetterOrDigit(text[i + 1])))
                            c = '＿';
                        break;
                }
                sb.Append(c);
            }
            return Esc(sb.ToString());
        }

        static readonly char[] FormatChars = ['*', '_', '~', '`'];

        public override String Code(String code) => "`" + Esc(code.Replace('`', 'ˋ')) + "`";

        public override String Link(String url, String title, String inner, String innerPlain)
        {
            //  Relative urls can't be used in Slack
            if (!MdInline.IsAbsolute(url))
                return inner;
            var label = innerPlain?.Trim() ?? "";
            if ((label.Length == 0) || (label == url))
                return "<" + EscUrl(url) + ">";
            return "<" + EscUrl(url) + "|" + EscLabel(label) + ">";
        }

        public override String Image(String url, String alt, String title)
        {
            if (!MdInline.IsAbsolute(url))
                return Esc(alt);
            var label = String.IsNullOrWhiteSpace(alt) ? title : alt;
            return String.IsNullOrWhiteSpace(label) ? "<" + EscUrl(url) + ">" : "<" + EscUrl(url) + "|" + EscLabel(label) + ">";
        }

        public override String HardBreak => "\n";

        /// <summary>
        /// Slack breaks lines on new lines, but a soft break in markdown is just a space
        /// </summary>
        public override String SoftBreak => " ";

        /// <summary>
        /// True for each open "a" tag that was rendered as a link
        /// </summary>
        readonly Stack<bool> OpenLinks = new();

        public override String Tag(String tag, bool closing, IReadOnlyList<MdAttribute> attributes)
        {
            switch (tag)
            {
                case "b":
                case "strong":
                    return "*";
                case "i":
                case "em":
                case "cite":
                case "dfn":
                case "var":
                    return "_";
                case "s":
                case "strike":
                case "del":
                    return "~";
                case "code":
                case "kbd":
                case "samp":
                case "tt":
                    return "`";
                case "br":
                    return "\n";
                case "hr":
                    return "\n" + Rule + "\n";
                case "summary":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    return closing ? "*\n" : "*";
                case "li":
                    return closing ? "\n" : "• ";
                case "td":
                case "th":
                    return closing ? " " : "";
                case "p":
                case "div":
                case "center":
                case "dt":
                case "dd":
                case "tr":
                case "blockquote":
                case "pre":
                case "figure":
                case "figcaption":
                case "caption":
                case "details":
                case "table":
                case "ul":
                case "ol":
                    return closing ? "\n" : "";
                case "img":
                    {
                        var src = attributes.FirstOrDefault(x => x.Name == "src").Value;
                        var alt = attributes.FirstOrDefault(x => x.Name == "alt").Value ?? attributes.FirstOrDefault(x => x.Name == "title").Value;
                        return src == null ? "" : Image(src, alt ?? "", null);
                    }
                case "a":
                    {
                        if (closing)
                            return OpenLinks.TryPop(out var wasLink) && wasLink ? ">" : "";
                        var href = attributes.FirstOrDefault(x => x.Name == "href").Value;
                        var isLink = (href != null) && MdInline.IsAbsolute(href);
                        OpenLinks.Push(isLink);
                        return isLink ? "<" + EscUrl(href) + "|" : "";
                    }
            }
            return "";
        }


        public override String EmphasisOpen(String tag) => tag switch
        {
            "strong" => "*",
            "del" => "~",
            _ => "_",
        };

        public override String EmphasisClose(String tag) => EmphasisOpen(tag);

        public override String Math(String tex, bool display) => "`" + Esc(tex.Replace('`', 'ˋ')) + "`";

        public override String Emoji(String name, String emoji) => emoji;

        public override String FootnoteRef(int number, String label) => "[" + number + "]";

        #endregion//Inline

        #region Blocks

        int QuoteDepth;

        String RenderBlocks(List<MdBlock> blocks, int listDepth, bool tight)
            => String.Join(tight ? "\n" : "\n\n", blocks.Select(x => RenderBlock(x, listDepth)).Where(x => x.Length > 0));

        String RenderBlock(MdBlock block, int listDepth)
        {
            switch (block)
            {
                case MdParagraph p:
                    return MdInline.Render(p.Text, this);
                case MdHeading h:
                    {
                        var text = Plain(h.Text).Trim();
                        return text.Length == 0 ? "" : "*" + Esc(text) + "*";
                    }
                case MdCode c:
                    return "```\n" + Esc(c.Text.TrimEnd('\n')) + "\n```";
                case MdMath m:
                    return "```\n" + Esc(m.Text) + "\n```";
                case MdRule:
                    return Rule;
                case MdHtml html:
                    return MdInline.Render(html.Text, this).Trim();
                case MdQuote q:
                    return Quote(() => RenderBlocks(q.Children, 0, false));
                case MdAlert a:
                    return Quote(() =>
                    {
                        var title = "*" + Alerts[a.Kind] + "*";
                        var content = RenderBlocks(a.Children, 0, false);
                        return content.Length == 0 ? title : title + "\n" + content;
                    });
                case MdList l:
                    return RenderList(l, listDepth);
                case MdTable t:
                    return RenderTable(t);
            }
            return "";
        }

        /// <summary>
        /// Slack only supports one level of block quotes, nested quotes are flattened
        /// </summary>
        String Quote(Func<String> render)
        {
            ++QuoteDepth;
            String text;
            try
            {
                text = render();
            }
            finally
            {
                --QuoteDepth;
            }
            if (QuoteDepth > 0)
                return text;
            return String.Join('\n', text.Split('\n').Select(x => x.Length == 0 ? ">" : "> " + x));
        }

        String RenderList(MdList l, int depth)
        {
            var lines = new List<String>();
            var number = l.Start;
            foreach (var item in l.Items)
            {
                var marker = l.Ordered ? (number++) + "." : Bullets[depth % Bullets.Length];
                if (item.Task != MdTask.None)
                {
                    var box = item.Task == MdTask.Done ? "☑" : "☐";
                    marker = l.Ordered ? marker + " " + box : box;
                }
                var content = RenderBlocks(item.Children, depth + 1, !l.Loose).Split('\n');
                lines.Add(marker + (content[0].Length > 0 ? " " + content[0] : ""));
                for (int i = 1; i < content.Length; ++i)
                    lines.Add(content[i].Length == 0 ? "" : "    " + content[i]);
            }
            return String.Join('\n', lines);
        }

        /// <summary>
        /// Tables are rendered as aligned (plain) text in a code block
        /// </summary>
        String RenderTable(MdTable t)
        {
            var rows = new List<String[]> { t.Header.Select(x => Plain(x ?? "").Trim()).ToArray() };
            rows.AddRange(t.Rows.Select(r => r.Select(x => Plain(x ?? "").Trim()).ToArray()));
            var cols = t.Header.Length;
            var widths = new int[cols];
            foreach (var r in rows)
                for (int i = 0; i < cols; ++i)
                    widths[i] = System.Math.Max(widths[i], r[i].Length);
            String pad(String s, int i)
            {
                var w = widths[i];
                return t.Align[i] switch
                {
                    MdAlign.Right => s.PadLeft(w),
                    MdAlign.Center => s.PadLeft(s.Length + ((w - s.Length) >> 1)).PadRight(w),
                    _ => s.PadRight(w),
                };
            }
            var sb = new StringBuilder("```\n");
            for (int ri = 0; ri < rows.Count; ++ri)
            {
                var r = rows[ri];
                sb.Append(Esc(String.Join(" | ", r.Select((x, i) => pad(x, i))).TrimEnd())).Append('\n');
                if (ri == 0)
                    sb.Append(String.Join("-+-", widths.Select(w => new String('-', System.Math.Max(1, w))))).Append('\n');
            }
            return sb.Append("```").ToString();
        }

        #endregion//Blocks
    }
}
