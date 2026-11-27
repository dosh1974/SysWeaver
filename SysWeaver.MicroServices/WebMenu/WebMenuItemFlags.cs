using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// State flags of a menu item (<see cref="WebMenuItem.Flags"/>), read by the app shell (srcWeb/shell/menu.js, MenuFlags).
    /// Typically set by the dynamic function of a menu attribute (ex: check the active choice, disable an item that can't be used right now).
    /// </summary>
    [Flags]
    public enum WebMenuItemFlags
    {
        /// <summary>
        /// A normal item
        /// </summary>
        None = 0,
        /// <summary>
        /// The item is shown disabled (it can't be activated)
        /// </summary>
        Disabled = 1,
        /// <summary>
        /// The item is shown checked (ex: the active theme)
        /// </summary>
        Checked = 2,
    }
}
