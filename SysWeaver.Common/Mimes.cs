// https://github.com/SimpleStack/simplestack.orm

namespace SysWeaver
{
    /// <summary>
    /// Commonly used MIME type (Content-Type) constants. See <see cref="MimeTypeMap"/> for file extension based lookups.
    /// </summary>
    public static class Mimes
    {
        /// <summary>
        /// Content-Type parameter suffix that declares UTF-8 encoding, append to a text MIME type.
        /// </summary>
        public const string Utf8Suffix = "; charset=UTF-8";
        /// <summary>
        /// Plain text, UTF-8 encoded ("text/plain; charset=UTF-8").
        /// </summary>
        public const string Utf8PlainText = "text/plain" + Utf8Suffix;
        /// <summary>
        /// HTML, UTF-8 encoded ("text/html; charset=UTF-8").
        /// </summary>
        public const string HtmlText = "text/html" + Utf8Suffix;
        /// <summary>
        /// Markdown, UTF-8 encoded ("text/markdown; charset=UTF-8").
        /// </summary>
        public const string MarkdownText = "text/markdown" + Utf8Suffix;

        

    }


}
