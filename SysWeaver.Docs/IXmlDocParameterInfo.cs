using System;

namespace SysWeaver.Docs
{
    /// <summary>
    /// The XML documentation of a method parameter (or of the return value, see <see cref="XmlDocExt.XmlDoc(System.Reflection.ParameterInfo)"/>).
    /// </summary>
    public interface IXmlDocParameterInfo
    {
        /// <summary>
        /// The text of the &lt;param&gt; element (or &lt;returns&gt; for a return parameter), may be null.
        /// </summary>
        String Param { get; }
    }
}
