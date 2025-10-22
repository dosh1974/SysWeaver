using System;

namespace SysWeaver.Data
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataOrderAttribute : Attribute
    {
        public TableDataOrderAttribute(int order)
        {
            Order = order;
        }

        public readonly int Order;
    }

}



