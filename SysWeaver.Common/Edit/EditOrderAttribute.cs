using System;

namespace SysWeaver
{
    /// <summary>
    /// Can be used to adjust member order when editing, members are sorted by this value (low to high), default is 0
    /// </summary>
    /// <remarks>
    /// Read by the type service (SysWeaver.MicroService.Edit) when describing a type for the web based editor.
    /// If the member doesn't have the attribute, the attribute of the declaring type is used (if any).
    /// </remarks>
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

        /// <summary>
        /// The sort order (low to high)
        /// </summary>
        public readonly float Order;
    }

}
