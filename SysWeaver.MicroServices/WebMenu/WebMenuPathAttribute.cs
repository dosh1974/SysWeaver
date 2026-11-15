using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on any type that is registered in the service manager to add a path (a folder that only groups other items) to a menu
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class WebMenuPathAttribute : WebMenuAttribute
    {
        /// <summary>
        /// Put this attribute on any type that is registered in the service manager to add a path (a folder that only groups other items) to a menu
        /// </summary>
        /// <param name="menu">Menu name to add it to (null for default)</param>
        /// <param name="id">Id of this item, this can be path separated by /, ex: "Debug/Data"</param>
        /// <param name="name">The display name of the item</param>
        /// <param name="title">The title (tool tip) to display</param>
        /// <param name="iconClass">A class name (maybe url supported later)</param>
        /// <param name="order">Optional sort order (items with same order values will be sorted by Name)</param>
        /// <param name="dynamic">Dynamically modify item, this is a type name followed by a . and then the method name to invoke</param>
        public WebMenuPathAttribute(String menu, String id, String name, String title = null, String iconClass = null, float order = 0, string dynamic = null)
            : base(menu, id, WebMenuItemTypes.Path, name, title, iconClass, order, null, false, null, dynamic)
        {
        }
    }


}
