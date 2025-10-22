using System;

namespace SysWeaver
{
    /// <summary>
    /// Can be used to adjust member order
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class EditOrderAttribute : Attribute
    {
        /// <summary>
        /// Can be used to adjust member order
        /// </summary>
        /// <param name="order">The sort order (low to high)</param>
        public EditOrderAttribute(float order = 0)
        {
            Order = order;
        }

        public readonly float Order;
    }

}
