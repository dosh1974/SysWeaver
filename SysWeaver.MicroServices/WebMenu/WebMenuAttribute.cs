using System;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Put this attribute on a method to add to a menu
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class WebMenuAttribute : Attribute
    {
#if DEBUG
        public override string ToString() => String.Concat(Menu, "[\"", Id, "\"] \"", Name, "\"= ", Type, String.IsNullOrEmpty(IconClass) ? "" : String.Join(IconClass, " (Icon: ", ')'), String.IsNullOrEmpty(Title) ? "" : String.Join(Title, " (Title: ", ')'));
#endif//DEBUG

        /// <summary>
        /// Put this attribute on a method to add to a menu
        /// </summary>
        /// <param name="menu">Menu name to add it to (null for default)</param>
        /// <param name="id">Id of this item, this can be path separated by /, ex: "Debug/Data/CompressionTable"</param>
        /// <param name="type">The type of this item (action to perform)</param>
        /// <param name="name">The display name of the item</param>
        /// <param name="title">The title (tool tip) to display</param>
        /// <param name="iconClass">A class name (maybe url supported later)</param>
        /// <param name="order">Optional sort order (items with same order values will be sorted by Name)</param>
        /// <param name="auth">The auth required for this item to be available in the menu</param>
        /// <param name="noUserRequired">If true, this menu item is only available if there is no user</param>
        /// <param name="data">Menu data</param>
        /// <param name="dynamic">Dynamically modify item, this is a type name followed by a . and then the method name to invoke</param>
        public WebMenuAttribute(String menu, String id, WebMenuItemTypes type, String name, String title, String iconClass, float order, String auth, bool noUserRequired, String data, String dynamic)
        {
            Menu = menu;
            Type = type;
            Id = id;
            Name = name;
            Title = title;
            IconClass = iconClass;
            Order = order;
            Auth = auth;
            NoUser = noUserRequired;
            Data = data;
            Dynamic = dynamic;
        }
        /// <summary>Menu name to add it to (null for default)</summary>
        public readonly String Menu;
        /// <summary>The type of this item (action to perform)</summary>
        public readonly WebMenuItemTypes Type;
        /// <summary>Id of this item, this can be path separated by /</summary>
        public readonly String Id;
        /// <summary>The display name of the item</summary>
        public readonly String Name;
        /// <summary>The title (tool tip) to display</summary>
        public readonly String Title;
        /// <summary>A class name for the icon</summary>
        public readonly String IconClass;
        /// <summary>Sort order (items with same order values will be sorted by Name)</summary>
        public readonly float Order;
        /// <summary>The auth required for this item to be available in the menu</summary>
        public readonly String Auth;
        /// <summary>If true, this menu item is only available if there is no user</summary>
        public readonly bool NoUser;
        /// <summary>Menu data (type dependent, typically an url)</summary>
        public readonly String Data;
        /// <summary>Type name followed by a . and then the method name to invoke to dynamically modify the item</summary>
        public readonly String Dynamic;
    }


}
