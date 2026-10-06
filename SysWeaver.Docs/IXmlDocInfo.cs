using System;

namespace SysWeaver.Docs
{
    /// <summary>
    /// The XML documentation of a type or member, as read from the compiler generated documentation file.
    /// </summary>
    /// <remarks>
    /// Texts are the element's inner text, trimmed; nested elements contribute only their text, so self closing tags such as &lt;see cref="..."/&gt; disappear.
    /// &lt;inheritdoc/&gt; is not resolved.
    /// </remarks>
    public interface IXmlDocInfo
    {
        /// <summary>
        /// The text of the &lt;summary&gt; element, or null if missing.
        /// </summary>
        String Summary { get; }
        /// <summary>
        /// The text of the &lt;remarks&gt; element, or null if missing.
        /// </summary>
        String Remarks { get; }
    }

  


}
