using SysWeaver.Docs;
using System.Reflection;

namespace SysWeaver.MicroService.EditInternal
{
    sealed class GetFieldInfo : GetBaseInfo
    {
        public GetFieldInfo(FieldInfo p) : base(false, null, p.FieldType, p.XmlDoc()?.Summary, p.XmlDoc()?.Remarks, p.IsInitOnly, p.Name)
        {
            P = p;
        }
        readonly FieldInfo P;

        public override T GetAttribute<T>() => P.GetCustomAttribute<T>(true) ?? P.DeclaringType?.GetCustomAttribute<T>(false);
    }


}
