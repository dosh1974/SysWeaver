using System;

namespace SysWeaver
{
    /// <summary>
    /// Use to specify the order (priority) of embedded resources (when serving them as files)
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly,  AllowMultiple = false)]
    public sealed class ResourceOrderAttribute : Attribute
    {
        /// <summary>
        /// Use to specify the order (priority) of embedded resources (when serving them as files)
        /// </summary>
        /// <param name="order">A higher value gives it priority over the same reosurce in some other assembly with a lower order</param>
        public ResourceOrderAttribute(double order)
        {
            Order = order;
        }

        /// <summary>
        /// The order (priority) of resources in this assembly
        /// </summary>
        public readonly double Order;
    }

}
