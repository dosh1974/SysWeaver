using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represents a single menu item
    /// </summary>
    public sealed class WebMenuItem
    {
#if DEBUG
        public override string ToString() => String.Concat("[\"", Id, "\"] \"", Name, "\" = ", Type, " \"", Data, '"', String.IsNullOrEmpty(IconClass) ? "" : String.Join(IconClass, " (Icon: ", ')'), String.IsNullOrEmpty(Title) ? "" : String.Join(Title, " (Title: ", ')'), (Children?.Length ?? 0) > 0 ? String.Join(Children.Length.ToString(), " (", " children)") : "");
#endif//DEBUG
        /// <summary>
        /// Id of the item
        /// </summary>
        public String Id;
        /// <summary>
        /// Name of the item
        /// </summary>
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the title text of a menu item, keep it short")]
        [AutoTranslateContext("The tool tip description for the title text is: \"{0}\"", nameof(Title))]
        public String Name;
        /// <summary>
        /// The type of the item
        /// </summary>
        public WebMenuItemTypes Type;
        /// <summary>
        /// Title (tool tip)
        /// </summary>
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the tool tip description of a menu item.")]
        [AutoTranslateContext("The title of the menu item is \"{0}\"", nameof(Name))]
        public String Title;
        /// <summary>
        /// Class name for an icon
        /// </summary>
        public String IconClass;
        /// <summary>
        /// Data (type dependent, typically an url).
        /// </summary>
        public String Data;
        /// <summary>
        /// Optional child items
        /// </summary>
        public WebMenuItem[] Children;
    }


}
