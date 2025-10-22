using System;

namespace SysWeaver.Docs
{
    public interface IXmlDocMethodInfo : IXmlDocInfo
    {
        String Returns { get; }
        IXmlDocParameterInfo[] Parameters { get; }
    }
}
