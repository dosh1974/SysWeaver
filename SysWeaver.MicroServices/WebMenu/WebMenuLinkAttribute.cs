using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on any type that is registered in the service manager to add it to a menu
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class WebMenuLinkAttribute : WebMenuAttribute
    {

        /// <summary>
        /// Put this attribute on any type that is registered in the service manager to add it to a menu
        /// </summary>
        /// <param name="menu">Menu name to add it to (null for default)</param>
        /// <param name="id">Id of this item, this can be path separated by /, ex: "Debug/Data/CompressionTable"</param>
        /// <param name="name">The display name of the item</param>
        /// <param name="url"></param>
        /// <param name="title">The title (tool tip) to display</param>
        /// <param name="iconClass">A class name (maybe url supported later)</param>
        /// <param name="order">Optional sort order (items with same order values will be sorted by Name)</param>
        /// <param name="auth">The auth required for this item to be available in thew menu</param>
        /// <param name="noUserRequired">If true, this menu item is only available if there is no user</param>
        /// <param name="dynamic">Dynamically modify item, this is a type name followed by a . and then the method name to invoke</param>
        public WebMenuLinkAttribute(String menu, String id, String name, String url, String title = null, String iconClass = null, float order = 0, String auth = null, bool noUserRequired = false, String dynamic = null)
            : base(menu, id, WebMenuItemTypes.Link, name, title, iconClass ?? "IconLink", order, auth, noUserRequired, url, dynamic)
        {
        }
    }


}
