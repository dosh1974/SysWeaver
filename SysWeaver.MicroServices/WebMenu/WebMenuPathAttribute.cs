using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Put this attribute on a method to make it available online
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class WebMenuPathAttribute : WebMenuAttribute
    {
        public WebMenuPathAttribute(String menu, String id, String name, String title = null, String iconClass = null, float order = 0, string dynamic = null)
            : base(menu, id, WebMenuItemTypes.Path, name, title, iconClass, order, null, false, null, dynamic)
        {
        }
    }


}
