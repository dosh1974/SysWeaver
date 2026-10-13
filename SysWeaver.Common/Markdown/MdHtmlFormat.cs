using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SysWeaver.Markdown
{
    /// <summary>
    /// Renders markdown as html, either unstyled (styled using tags and classes) or with inline styles for emails
    /// </summary>
    sealed class MdHtmlFormat : MdFormat
    {
        public MdHtmlFormat(MdDocument doc, bool email) : base(doc)
        {
            Email = email;
        }

        readonly bool Email;

        /// <summary>
        /// Render the document
        /// </summary>
        public String Render()
        {
            var sb = new StringBuilder(1024);
            RenderBlocks(sb, Doc.Blocks, false);
            RenderFootnotes(sb);
            return sb.ToString();
        }

        /// <summary>
        /// The plain text of the first heading (null if none)
        /// </summary>
        public String FirstHeading()
        {
            static MdHeading find(List<MdBlock> blocks)
            {
                foreach (var b in blocks)
                {
                    if (b is MdHeading h)
                        return h;
                    var c = b switch
                    {
                        MdQuote q => find(q.Children),
                        MdAlert a => find(a.Children),
                        _ => null,
                    };
                    if (c != null)
                        return c;
                }
                return null;
            }
            var h = find(Doc.Blocks);
            return h == null ? null : Plain(h.Text).Trim();
        }

        #region Escaping

        static readonly char[] EscChars = ['&', '<', '>', '"'];

        /// <summary>
        /// Escape text for use in html content
        /// </summary>
        public static String Esc(String s)
        {
            if (s.IndexOfAny(EscChars) < 0)
                return s;
            var sb = new StringBuilder(s.Length + 16);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Escape text for use in a (double quoted) html attribute
        /// </summary>
        public static String EscAttr(String s) => Esc(s).Replace("'", "&#39;");

        #endregion//Escaping

        #region Styles

        const String MonoFont = "Consolas,Menlo,Monaco,'Courier New',monospace";

        /// <summary>
        /// Inline styles used for emails, key = tag (or a special key)
        /// </summary>
        static readonly Dictionary<String, String> EmailStyles = new(StringComparer.Ordinal)
        {
            ["p"] = "margin:0 0 16px 0;",
            ["h1"] = "margin:0 0 16px 0;font-size:28px;line-height:1.25;font-weight:bold;",
            ["h2"] = "margin:24px 0 16px 0;font-size:24px;line-height:1.25;font-weight:bold;",
            ["h3"] = "margin:24px 0 12px 0;font-size:20px;line-height:1.25;font-weight:bold;",
            ["h4"] = "margin:20px 0 12px 0;font-size:18px;line-height:1.25;font-weight:bold;",
            ["h5"] = "margin:16px 0 8px 0;font-size:16px;line-height:1.25;font-weight:bold;",
            ["h6"] = "margin:16px 0 8px 0;font-size:14px;line-height:1.25;font-weight:bold;color:#5b6375;",
            ["a"] = "color:#2f62e6;text-decoration:underline;",
            ["code"] = "font-family:" + MonoFont + ";font-size:90%;background-color:#eef0f3;padding:2px 4px;border-radius:4px;",
            ["kbd"] = "font-family:" + MonoFont + ";font-size:90%;background-color:#f6f8fa;padding:1px 5px;border:1px solid #d0d7de;border-radius:4px;",
            ["pre"] = "margin:0 0 16px 0;padding:12px 16px;background-color:#eef0f3;border-radius:6px;font-family:" + MonoFont + ";font-size:14px;line-height:1.45;white-space:pre-wrap;word-wrap:break-word;",
            ["pre code"] = "font-family:" + MonoFont + ";font-size:14px;",
            ["math"] = "font-family:" + MonoFont + ";font-size:90%;",
            ["math block"] = "margin:0 0 16px 0;padding:8px 0;font-family:" + MonoFont + ";font-size:14px;text-align:center;white-space:pre-wrap;",
            ["blockquote"] = "margin:0 0 16px 0;padding:0 0 0 16px;border-left:4px solid #d0d7de;color:#5b6375;",
            ["ul"] = "margin:0 0 16px 0;padding:0 0 0 28px;",
            ["ol"] = "margin:0 0 16px 0;padding:0 0 0 28px;",
            ["li"] = "margin:0 0 4px 0;",
            ["li task"] = "margin:0 0 4px 0;list-style-type:none;",
            ["hr"] = "border:0;border-top:1px solid #d0d7de;height:0;margin:24px 0;",
            ["table"] = "border-collapse:collapse;margin:0 0 16px 0;",
            ["th"] = "border:1px solid #d0d7de;padding:6px 12px;background-color:#f6f8fa;font-weight:bold;",
            ["td"] = "border:1px solid #d0d7de;padding:6px 12px;",
            ["img"] = "max-width:100%;height:auto;border:0;",
            ["mark"] = "background-color:#fff8c5;",
            ["details"] = "margin:0 0 16px 0;",
            ["summary"] = "font-weight:bold;",
            ["sup"] = "font-size:75%;line-height:0;",
            ["footnotes"] = "margin:24px 0 0 0;padding:16px 0 0 0;border-top:1px solid #d0d7de;font-size:14px;color:#5b6375;",
        };

        static readonly Dictionary<MdAlertKind, (String Title, String Color)> Alerts = new()
        {
            [MdAlertKind.Note] = ("Note", "#0969da"),
            [MdAlertKind.Tip] = ("Tip", "#1a7f37"),
            [MdAlertKind.Important] = ("Important", "#8250df"),
            [MdAlertKind.Warning] = ("Warning", "#9a6700"),
            [MdAlertKind.Caution] = ("Caution", "#cf222e"),
        };

        /// <summary>
        /// Append an opening tag (with an inline style for emails)
        /// </summary>
        /// <param name="sb">The output</param>
        /// <param name="tag">The tag name</param>
        /// <param name="attributes">Optional attributes (already escaped)</param>
        /// <param name="styleKey">The key of the email style (null to use the tag)</param>
        /// <param name="extraStyle">Optional extra email style</param>
        StringBuilder Open(StringBuilder sb, String tag, String attributes = null, String styleKey = null, String extraStyle = null)
        {
            sb.Append('<').Append(tag);
            if (attributes != null)
                sb.Append(' ').Append(attributes);
            if (Email)
            {
                EmailStyles.TryGetValue(styleKey ?? tag, out var style);
                style += extraStyle;
                if (!String.IsNullOrEmpty(style))
                    sb.Append(" style=\"").Append(style).Append('"');
            }
            return sb.Append(MdInline.VoidTags.Contains(tag) ? " />" : ">");
        }

        String Open(String tag, String attributes = null, String styleKey = null, String extraStyle = null)
            => Open(new StringBuilder(), tag, attributes, styleKey, extraStyle).ToString();

        #endregion//Styles

        #region Inline

        public override String Text(String text) => Esc(text);

        public override String Code(String code) => Open("code") + Esc(code) + "</code>";

        public override String Link(String url, String title, String inner, String innerPlain)
            => Open("a", "href=\"" + EscAttr(url) + '"' + (title == null ? "" : " title=\"" + EscAttr(title) + '"')) + inner + "</a>";

        public override String Image(String url, String alt, String title)
            => Open("img", "src=\"" + EscAttr(url) + "\" alt=\"" + EscAttr(alt) + '"' + (title == null ? "" : " title=\"" + EscAttr(title) + '"'));

        public override String HardBreak => Open("br") + "\n";

        public override String SoftBreak => "\n";

        public override String Tag(String tag, bool closing, IReadOnlyList<MdAttribute> attributes)
        {
            if (closing)
                return "</" + tag + ">";
            String attr = null;
            String extra = null;
            if (attributes.Count > 0)
            {
                attr = String.Join(' ', attributes.Select(x => x.Value.Length == 0 ? x.Name : x.Name + "=\"" + EscAttr(x.Value) + '"'));
                var align = attributes.FirstOrDefault(x => x.Name == "align").Value;
                if (Email && (align != null) && (tag != "img"))
                    extra = "text-align:" + align + ';';
            }
            return Open(tag, attr, null, extra);
        }


        public override String EmphasisOpen(String tag) => "<" + tag + ">";

        public override String EmphasisClose(String tag) => "</" + tag + ">";

        public override String Math(String tex, bool display)
            => Open("span", display ? "class=\"math math-display\"" : "class=\"math math-inline\"", "math") + Esc(tex) + "</span>";

        public override String Emoji(String name, String emoji) => emoji;

        /// <summary>
        /// Key = footnote label, Value = number of references
        /// </summary>
        readonly Dictionary<String, int> FootnoteRefCount = new(StringComparer.Ordinal);

        public override String FootnoteRef(int number, String label)
        {
            var id = FootnoteId(label);
            FootnoteRefCount.TryGetValue(label, out var count);
            FootnoteRefCount[label] = ++count;
            var refId = "fnref-" + id + (count > 1 ? "-" + count : "");
            return Open("sup", "class=\"footnote-ref\"") + Open("a", "href=\"#fn-" + id + "\" id=\"" + refId + '"') + number + "</a></sup>";
        }

        static String FootnoteId(String label)
        {
            var sb = new StringBuilder(label.Length);
            foreach (var c in label.ToLowerInvariant())
                sb.Append(Char.IsAsciiLetterOrDigit(c) || (c == '-') || (c == '_') ? c : '-');
            return sb.ToString();
        }

        #endregion//Inline

        #region Blocks

        /// <summary>
        /// Used heading ids, key = slug, value = number of times used
        /// </summary>
        readonly Dictionary<String, int> HeadingIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Get a GitHub style heading id: lower case, spaces to '-', punctuation removed, duplicates gets a suffix
        /// </summary>
        String HeadingId(String text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in Plain(text).Trim().ToLowerInvariant())
            {
                if (Char.IsLetterOrDigit(c) || (c == '-') || (c == '_'))
                    sb.Append(c);
                else if (c == ' ')
                    sb.Append('-');
            }
            var id = sb.ToString();
            if (HeadingIds.TryGetValue(id, out var count))
            {
                HeadingIds[id] = count + 1;
                id = id + '-' + count;
            }
            else
            {
                HeadingIds[id] = 1;
            }
            return id;
        }

        void RenderBlocks(StringBuilder sb, List<MdBlock> blocks, bool tight)
        {
            for (int bi = 0; bi < blocks.Count; ++bi)
            {
                switch (blocks[bi])
                {
                    case MdParagraph p:
                        if (tight)
                        {
                            sb.Append(MdInline.Render(p.Text, this));
                            if (bi + 1 < blocks.Count)
                                sb.Append('\n');
                        }
                        else
                        {
                            Open(sb, "p").Append(MdInline.Render(p.Text, this)).Append("</p>\n");
                        }
                        break;
                    case MdHeading h:
                        {
                            var tag = "h" + h.Level;
                            var id = HeadingId(h.Text);
                            Open(sb, tag, id.Length > 0 ? "id=\"" + EscAttr(id) + '"' : null).Append(MdInline.Render(h.Text, this)).Append("</").Append(tag).Append(">\n");
                            break;
                        }
                    case MdCode c:
                        Open(sb, "pre");
                        Open(sb, "code", c.Language == null ? null : "class=\"language-" + EscAttr(c.Language) + '"', "pre code");
                        sb.Append(Esc(c.Text)).Append("</code></pre>\n");
                        break;
                    case MdMath m:
                        Open(sb, "div", "class=\"math math-display\"", "math block").Append(Esc(m.Text)).Append("</div>\n");
                        break;
                    case MdRule:
                        Open(sb, "hr").Append('\n');
                        break;
                    case MdHtml html:
                        sb.Append(MdInline.Render(html.Text, this)).Append('\n');
                        break;
                    case MdQuote q:
                        Open(sb, "blockquote").Append('\n');
                        RenderBlocks(sb, q.Children, false);
                        sb.Append("</blockquote>\n");
                        break;
                    case MdAlert a:
                        {
                            var (title, color) = Alerts[a.Kind];
                            var kind = a.Kind.ToString().ToLowerInvariant();
                            Open(sb, "div", "class=\"markdown-alert markdown-alert-" + kind + '"', "", "margin:0 0 16px 0;padding:8px 16px;border-left:4px solid " + color + ';').Append('\n');
                            Open(sb, "p", "class=\"markdown-alert-title\"", "", "margin:0 0 8px 0;font-weight:bold;color:" + color + ';').Append(title).Append("</p>\n");
                            RenderBlocks(sb, a.Children, false);
                            sb.Append("</div>\n");
                            break;
                        }
                    case MdList l:
                        RenderList(sb, l);
                        break;
                    case MdTable t:
                        RenderTable(sb, t);
                        break;
                }
            }
        }

        void RenderList(StringBuilder sb, MdList l)
        {
            var tag = l.Ordered ? "ol" : "ul";
            var anyTask = l.Items.Any(x => x.Task != MdTask.None);
            var attr = anyTask ? "class=\"contains-task-list\"" : null;
            if (l.Ordered && (l.Start != 1))
                attr = (attr == null ? "" : attr + " ") + "start=\"" + l.Start + '"';
            Open(sb, tag, attr).Append('\n');
            foreach (var item in l.Items)
            {
                if (item.Task == MdTask.None)
                {
                    Open(sb, "li");
                }
                else
                {
                    Open(sb, "li", "class=\"task-list-item\"", "li task");
                    var done = item.Task == MdTask.Done;
                    //  Form elements are removed by many email clients
                    if (Email)
                        sb.Append("<span class=\"task-list-item-checkbox\">").Append(done ? "&#9745;" : "&#9744;").Append("</span> ");
                    else
                        sb.Append("<input type=\"checkbox\" class=\"task-list-item-checkbox\" disabled").Append(done ? " checked" : "").Append(" /> ");
                }
                var children = item.Children;
                if ((!l.Loose) && (children.Count > 0) && (children[0] is MdParagraph))
                {
                    RenderBlocks(sb, children, true);
                }
                else
                {
                    if (children.Count > 0)
                        sb.Append('\n');
                    RenderBlocks(sb, children, !l.Loose);
                }
                sb.Append("</li>\n");
            }
            sb.Append("</").Append(tag).Append(">\n");
        }

        void RenderTable(StringBuilder sb, MdTable t)
        {
            Open(sb, "table", Email ? "role=\"table\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"" : null).Append('\n');
            Open(sb, "thead").Append('\n');
            RenderRow(sb, t, t.Header, "th");
            sb.Append("</thead>\n");
            if (t.Rows.Count > 0)
            {
                Open(sb, "tbody").Append('\n');
                foreach (var r in t.Rows)
                    RenderRow(sb, t, r, "td");
                sb.Append("</tbody>\n");
            }
            sb.Append("</table>\n");
        }

        void RenderRow(StringBuilder sb, MdTable t, String[] cells, String tag)
        {
            Open(sb, "tr").Append('\n');
            for (int i = 0; i < cells.Length; ++i)
            {
                var align = t.Align[i];
                String attr = null;
                String extra = null;
                if (align != MdAlign.None)
                {
                    var a = align.ToString().ToLowerInvariant();
                    attr = "class=\"align-" + a + '"';
                    if (Email)
                    {
                        attr += " align=\"" + a + '"';
                        extra = "text-align:" + a + ';';
                    }
                }
                Open(sb, tag, attr, null, extra).Append(MdInline.Render(cells[i] ?? "", this)).Append("</").Append(tag).Append(">\n");
            }
            sb.Append("</tr>\n");
        }

        void RenderFootnotes(StringBuilder sb)
        {
            if (FootnoteOrder.Count <= 0)
                return;
            Open(sb, "section", "class=\"footnotes\"", "footnotes").Append('\n');
            Open(sb, "ol").Append('\n');
            //  Footnotes may reference other footnotes (the list grows while rendering)
            for (int i = 0; i < FootnoteOrder.Count; ++i)
            {
                var label = FootnoteOrder[i];
                var id = FootnoteId(label);
                Open(sb, "li", "id=\"fn-" + id + '"').Append('\n');
                var start = sb.Length;
                RenderBlocks(sb, Doc.Footnotes[label], false);
                var backRef = " " + Open("a", "href=\"#fnref-" + id + "\" class=\"footnote-backref\" aria-label=\"Back to reference " + (i + 1) + '"') + "&#8617;</a>";
                //  Put the back reference in the last paragraph
                var content = sb.ToString(start, sb.Length - start);
                if (content.EndsWith("</p>\n", StringComparison.Ordinal))
                    sb.Insert(sb.Length - 5, backRef);
                else
                    sb.Append(backRef.TrimStart()).Append('\n');
                sb.Append("</li>\n");
            }
            sb.Append("</ol>\n</section>\n");
        }

        #endregion//Blocks
    }
}
