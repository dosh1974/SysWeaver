using System;

namespace SimpleStack.Orm.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UpdateAggregateAttribute : Attribute
    {
        public UpdateAggregateAttribute(UpdateAggregations aggregate, params Object[] data)
        {
            Aggregate = aggregate;
            Data = data;
        }
        public readonly UpdateAggregations Aggregate;
        public readonly Object[] Data;
    }


}