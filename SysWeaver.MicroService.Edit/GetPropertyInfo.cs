using SysWeaver.Docs;
using System.Reflection;

namespace SysWeaver.MicroService.EditInternal
{
    sealed class GetPropertyInfo : GetBaseInfo
    {
        public GetPropertyInfo(PropertyInfo p) : base(false, null, p.PropertyType, p.XmlDoc()?.Summary, p.XmlDoc()?.Remarks, !p.CanWrite, p.Name)
        {
            P = p;
        }
        readonly PropertyInfo P;

        public override T GetAttribute<T>() => EditAttributeLookup.Get<T>(P) ?? P.DeclaringType?.GetCustomAttribute<T>(false);
    }


}
