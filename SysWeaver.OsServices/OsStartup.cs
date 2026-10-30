using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// A program that is started when the system starts or a user logs on.
    /// </summary>
    public sealed class OsStartupItem
    {
        /// <summary>
        /// The id (used by <see cref="IStartupItems"/>)
        /// </summary>
        public String Id;

        /// <summary>
        /// The name
        /// </summary>
        public String Name;

        /// <summary>
        /// The command that is run
        /// </summary>
        public String Command;

        /// <summary>
        /// Where it's defined, ex: "HKCU\Software\Microsoft\Windows\CurrentVersion\Run", a folder or a file
        /// </summary>
        public String Location;

        /// <summary>
        /// The type, ex: "Run key", "RunOnce key", "Startup folder", "Autostart", "Cron @reboot"
        /// </summary>
        public String Type;

        /// <summary>
        /// "User" (the current user) or "Machine" (all users / the system)
        /// </summary>
        public String Scope;

        /// <summary>
        /// True if it's enabled
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// True if it can be enabled / disabled
        /// </summary>
        public bool CanDisable;

        /// <summary>
        /// True if it can be removed
        /// </summary>
        public bool CanRemove;

        /// <summary>
        /// The file that defines it (shortcut, .desktop file, crontab), empty for registry values
        /// </summary>
        public String File;

        /// <summary>
        /// More details (platform specific)
        /// </summary>
        public KeyValuePair<String, String>[] Properties;
    }


    /// <summary>
    /// Lists, enables, disables, adds and removes programs that are started when the system starts or a user logs on
    /// (Windows: Run keys and startup folders, Linux: XDG autostart, cron @reboot and rc.local).
    /// Get an instance using <see cref="OsSystem.Startup"/> (created by <see cref="IServiceHostFactory.CreateStartupItems"/>).
    /// "User" items belong to the user running the process. Machine changes requires elevation (admin / root), failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface IStartupItems
    {
        /// <summary>
        /// The scopes that new items can be added to ("User" and / or "Machine")
        /// </summary>
        IReadOnlyList<String> AddScopes { get; }

        /// <summary>
        /// Get all startup items.
        /// </summary>
        /// <returns>The items</returns>
        IReadOnlyList<OsStartupItem> GetItems();

        /// <summary>
        /// Get an item.
        /// </summary>
        /// <param name="id">The id</param>
        /// <returns>The item or null if not found</returns>
        OsStartupItem GetItem(String id);

        /// <summary>
        /// Enable or disable an item (see <see cref="OsStartupItem.CanDisable"/>).
        /// </summary>
        /// <param name="id">The id</param>
        /// <param name="enabled">True to enable, false to disable</param>
        void SetEnabled(String id, bool enabled);

        /// <summary>
        /// Remove an item (see <see cref="OsStartupItem.CanRemove"/>).
        /// </summary>
        /// <param name="id">The id</param>
        void Remove(String id);

        /// <summary>
        /// Add a new item.
        /// </summary>
        /// <param name="scope">"User" or "Machine" (see <see cref="AddScopes"/>)</param>
        /// <param name="name">The name</param>
        /// <param name="command">The command to run</param>
        /// <returns>The id of the new item</returns>
        String Add(String scope, String name, String command);
    }
}
