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



    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIncludeAttribute : Attribute
    {
        public TableDataIncludeAttribute(bool include = true)
        {
            Include = include;
        }

        public readonly bool Include;
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataSearchAttribute : Attribute
    {
        public TableDataSearchAttribute(bool enable, double weight = 1.0)
        {
            Weight = enable ? weight : 0;
        }

        public TableDataSearchAttribute(double weight = 1.0)
        {
            Weight = weight;
        }

        public readonly double Weight;
    }


}



