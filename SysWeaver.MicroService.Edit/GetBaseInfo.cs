using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService.EditInternal
{
    abstract class GetBaseInfo
    {
        protected GetBaseInfo(bool haveDefault, Object def, Type type, String summary, String remarks, bool isReadOnly, string name)
        {
            if (type.IsGenericType)
            {
                var gt = type.GetGenericTypeDefinition();
                if ((gt == typeof(Task<>)) || (gt == typeof(ValueTask<>)))
                    type = type.GetGenericArguments()[0];
            }
            Def = def;
            Type = type;
            Summary = summary;
            Remarks = remarks;
            IsReadOnly = isReadOnly;
            Name = name;
        }
        public readonly String Name;
#pragma warning disable CS0649
        public readonly bool HaveDefault;
#pragma warning restore CS0649
        public readonly Object Def;
        public readonly Type Type;
        public readonly String Summary;
        public readonly String Remarks;
        public readonly bool IsReadOnly;

        public abstract T GetAttribute<T>() where T : Attribute;
    }


}
