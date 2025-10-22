using SysWeaver.Docs;
using System.Reflection;

namespace SysWeaver.MicroService.EditInternal
{
    sealed class GetParamaterInfo : GetBaseInfo
    {
        public GetParamaterInfo(ParameterInfo p) : base(p.HasDefaultValue, p.DefaultValue, p.ParameterType, p.XmlDoc()?.Param, null, false, p.Name)
        {
            P = p;
        }
        readonly ParameterInfo P;

        public override T GetAttribute<T>() => P.GetCustomAttribute<T>(true);
    }


}
