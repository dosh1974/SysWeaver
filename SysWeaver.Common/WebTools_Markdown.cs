using System;
using System.Text;
using SysWeaver.Markdown;

namespace SysWeaver
{
    public static partial class WebTools
    {
        /// <summary>
        /// Convert markdown (GitHub flavored) to html, the result is html that can be used as the inner html of an element (no html, head or body tags etc).
        /// No styling is applied, style the result using css (tags, and the classes listed below).
        /// Supported (CommonMark + GitHub extensions):
        /// - Headings (# and underlined, with GitHub style id's), paragraphs, hard line breaks (two trailing spaces or a trailing backslash), thematic breaks.
        /// - Emphasis (*em*, **strong**), strikethrough (~~del~~), inline code, backslash escapes, html entities, emoji short codes (:smile:).
        /// - Links (inline, reference, autolinks and bare urls / email addresses) and images.
        /// - Block quotes, alerts (> [!NOTE], [!TIP], [!IMPORTANT], [!WARNING], [!CAUTION]), ordered and unordered (nested) lists, task lists, fenced and indented code blocks, tables, footnotes.
        /// - Math ($inline$, $`inline`$, $$display$$ and ```math blocks), the TeX is kept as is (render it on the client, ex: using KaTeX or MathJax).
        /// - Raw html (sanitized: only safe tags and attributes are kept, others are escaped), html comments are removed.
        /// Classes:
        /// - Code blocks: "language-[info]" on the code element (if a language is specified, ex: "language-mermaid").
        /// - Task lists: "contains-task-list" on the list, "task-list-item" on the item and "task-list-item-checkbox" on the (disabled) checkbox.
        /// - Tables: "align-left", "align-center" or "align-right" on th and td elements of aligned columns.
        /// - Alerts: a div with "markdown-alert" and "markdown-alert-[note|tip|important|warning|caution]", the title is a p with "markdown-alert-title".
        /// - Math: "math math-inline" on a span, or "math math-display" on a span (inline) or div (block).
        /// - Footnotes: "footnote-ref" on the sup of references, the definitions are in a section with "footnotes", with links back using "footnote-backref".
        /// Links and images may only use relative urls or the schemes http, https, mailto, tel, ftp and cid.
        /// </summary>
        /// <param name="markdown">The markdown text</param>
        /// <returns>The html</returns>
        public static String MarkdownToHtml(String markdown)
            => new MdHtmlFormat(MdDocument.Parse(markdown), false).Render();

        /// <summary>
        /// Convert markdown (GitHub flavored) to a stand alone html document that is suitable as the body of an email (compatible with most email clients).
        /// The same markdown is supported as for <see cref="MarkdownToHtml(string)"/>, the content is placed in a centered 600 pixel wide table and styled using inline styles (classes are kept).
        /// Task list check boxes are rendered as characters (form elements are removed by many email clients).
        /// </summary>
        /// <param name="markdown">The markdown text</param>
        /// <param name="title">The title of the document, null to use the text of the first heading (if any)</param>
        /// <param name="preheader">Optional preview text, shown by many email clients after the subject in the inbox (but not in the email)</param>
        /// <param name="language">The language of the content (the lang attribute)</param>
        /// <returns>A complete html document</returns>
        public static String MarkdownToHtmlDocument(String markdown, String title = null, String preheader = null, String language = "en")
        {
            var format = new MdHtmlFormat(MdDocument.Parse(markdown), true);
            var content = format.Render();
            title ??= format.FirstHeading() ?? "";
            const String font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";
            var sb = new StringBuilder(content.Length + 2048);
            sb.Append("<!DOCTYPE html>\n<html lang=\"").Append(MdHtmlFormat.EscAttr(String.IsNullOrWhiteSpace(language) ? "en" : language.Trim())).Append("\" xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\">\n");
            sb.Append("<head>\n<meta charset=\"utf-8\">\n<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n<meta name=\"x-apple-disable-message-reformatting\">\n<meta name=\"format-detection\" content=\"telephone=no, date=no, address=no, email=no\">\n<meta name=\"color-scheme\" content=\"light\">\n<meta name=\"supported-color-schemes\" content=\"light\">\n");
            sb.Append("<title>").Append(MdHtmlFormat.Esc(title)).Append("</title>\n");
            sb.Append("<!--[if mso]><noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript><style>body,table,td,p,a,li,h1,h2,h3,h4,h5,h6,blockquote{font-family:Arial,sans-serif !important;}</style><![endif]-->\n");
            sb.Append("</head>\n<body style=\"margin:0;padding:0;width:100%;background-color:#f4f5f7;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">\n");
            if (!String.IsNullOrWhiteSpace(preheader))
                sb.Append("<div style=\"display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;color:#f4f5f7;\">").Append(MdHtmlFormat.Esc(preheader.Trim())).Append("</div>\n");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;background-color:#f4f5f7;\">\n<tr>\n<td align=\"center\" style=\"padding:24px 12px;\">\n");
            sb.Append("<!--[if mso]><table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td><![endif]-->\n");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;max-width:600px;background-color:#ffffff;border-radius:8px;\">\n<tr>\n");
            sb.Append("<td class=\"content\" style=\"padding:24px 28px;font-family:").Append(font).Append(";font-size:16px;line-height:1.5;color:#1d2330;text-align:left;word-wrap:break-word;\">\n");
            sb.Append(content);
            sb.Append("</td>\n</tr>\n</table>\n");
            sb.Append("<!--[if mso]></td></tr></table><![endif]-->\n");
            sb.Append("</td>\n</tr>\n</table>\n</body>\n</html>\n");
            return sb.ToString();
        }

        /// <summary>
        /// Convert markdown (GitHub flavored) to Slack "mrkdwn" (the text format used in Slack messages), keeping as many features as possible.
        /// - Emphasis, strikethrough, inline code, code blocks, block quotes and links are converted to the Slack syntax (&amp;, &lt; and &gt; are escaped).
        /// - Headings are bold, lists use bullet characters (nested lists are indented), task lists use ☐ and ☑, thematic breaks are a line of ─.
        /// - Tables are rendered as aligned plain text in a code block, math is rendered as code.
        /// - Alerts are block quotes with a bold title, nested block quotes are flattened (Slack only supports one level).
        /// - Images are links, relative links are rendered as their text (they can't be used in Slack), footnotes are listed at the end.
        /// - Emoji short codes are converted to emojis, raw html is converted where possible (b, i, code, a, img, br etc), else removed.
        /// </summary>
        /// <param name="markdown">The markdown text</param>
        /// <returns>The Slack mrkdwn text</returns>
        public static String MarkdownToSlack(String markdown)
            => new MdSlackFormat(MdDocument.Parse(markdown)).Render();
    }
}
