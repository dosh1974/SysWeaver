using System;

namespace SysWeaver.Docs
{
    /// <summary>
    /// The XML documentation of a method or constructor.
    /// </summary>
    public interface IXmlDocMethodInfo : IXmlDocInfo
    {
        /// <summary>
        /// The text of the &lt;returns&gt; element, or null if missing.
        /// </summary>
        String Returns { get; }
        /// <summary>
        /// The parameter documentation, indexed by parameter position.
        /// Null if the method has no parameters; an element is null if that parameter has no &lt;param&gt; element.
        /// </summary>
        IXmlDocParameterInfo[] Parameters { get; }
    }
}
