using System;
using SysWeaver.Docs;
using System.Reflection;

namespace SysWeaver.MicroService.EditInternal
{
    sealed class GetTypeInfoX : GetBaseInfo
    {
        public GetTypeInfoX(Type p) : base(false, null, p, p.XmlDoc()?.Summary, p.XmlDoc()?.Remarks, false, p.Name)
        {
            P = p;
        }
        readonly Type P;

        public override T GetAttribute<T>() => P.GetCustomAttribute<T>(true) ?? P.DeclaringType?.GetCustomAttribute<T>(false);
    }
    

}
