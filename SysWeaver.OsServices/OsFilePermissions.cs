using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// An access control entry of a file or folder (Windows ACL or Linux POSIX ACL).
    /// </summary>
    public sealed class OsFileAccessEntry
    {
        /// <summary>
        /// The user or group, ex: "BUILTIN\Administrators" or "user:alice"
        /// </summary>
        public String Identity;

        /// <summary>
        /// True if access is allowed, false if denied
        /// </summary>
        public bool Allow;

        /// <summary>
        /// The rights, ex: "FullControl", "ReadAndExecute, Synchronize" or "rwx"
        /// </summary>
        public String Rights;

        /// <summary>
        /// True if the entry is inherited from a parent folder
        /// </summary>
        public bool Inherited;

        /// <summary>
        /// What the entry applies to, ex: "This folder, subfolders and files" (empty for files)
        /// </summary>
        public String AppliesTo;
    }


    /// <summary>
    /// The permissions of a file or folder.
    /// </summary>
    public sealed class OsFilePermissions
    {
        /// <summary>
        /// The full path
        /// </summary>
        public String Path;

        /// <summary>
        /// True if it's a folder
        /// </summary>
        public bool IsFolder;

        /// <summary>
        /// The owner
        /// </summary>
        public String Owner;

        /// <summary>
        /// The group (Linux, empty on Windows)
        /// </summary>
        public String Group;

        /// <summary>
        /// The permission bits (Linux, -1 on Windows)
        /// </summary>
        public int Mode = -1;

        /// <summary>
        /// True if the entries are inherited from the parent folder (Windows)
        /// </summary>
        public bool InheritanceEnabled;

        /// <summary>
        /// The access control entries
        /// </summary>
        public OsFileAccessEntry[] Entries;

        /// <summary>
        /// The ACL as text (ex: "getfacl" output on Linux, SDDL on Windows), empty if not available
        /// </summary>
        public String Text;
    }


    /// <summary>
    /// Reads and changes the permissions (owner, ACL, mode) of files and folders.
    /// Get an instance using <see cref="OsSystem.FilePermissions"/> (created by <see cref="IServiceHostFactory.CreateFilePermissions"/>).
    /// Failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface IFilePermissions
    {
        /// <summary>
        /// True if the permissions are Windows ACL's (entries can be added and removed, inheritance can be changed),
        /// false if they are Linux permission bits (mode, owner and group can be changed)
        /// </summary>
        bool UseAcl { get; }

        /// <summary>
        /// The rights that can be used when adding an entry (Windows), ex: "FullControl", "Modify", "ReadAndExecute"
        /// </summary>
        IReadOnlyList<String> Rights { get; }

        /// <summary>
        /// What a new entry can apply to (for folders, Windows), ex: "This folder, subfolders and files"
        /// </summary>
        IReadOnlyList<String> AppliesTo { get; }

        /// <summary>
        /// Get the permissions.
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <returns>The permissions</returns>
        OsFilePermissions GetPermissions(String path);

        /// <summary>
        /// Change the owner.
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <param name="owner">The new owner (user name)</param>
        /// <param name="recursive">True to also change everything in a folder</param>
        void SetOwner(String path, String owner, bool recursive);

        /// <summary>
        /// Change the group (Linux).
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <param name="group">The new group</param>
        /// <param name="recursive">True to also change everything in a folder</param>
        void SetGroup(String path, String group, bool recursive);

        /// <summary>
        /// Change the permission bits (Linux).
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <param name="mode">The mode (ex: 0x1ED = octal 755)</param>
        /// <param name="recursive">True to also change everything in a folder</param>
        void SetMode(String path, int mode, bool recursive);

        /// <summary>
        /// Add an access control entry (Windows).
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <param name="identity">The user or group</param>
        /// <param name="allow">True to allow, false to deny</param>
        /// <param name="rights">One of <see cref="Rights"/></param>
        /// <param name="appliesTo">One of <see cref="AppliesTo"/> (ignored for files)</param>
        void AddEntry(String path, String identity, bool allow, String rights, String appliesTo);

        /// <summary>
        /// Remove an (explicit, not inherited) access control entry (Windows).
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <param name="index">The index of the entry in <see cref="OsFilePermissions.Entries"/></param>
        void RemoveEntry(String path, int index);

        /// <summary>
        /// Enable or disable inheritance from the parent folder (Windows).
        /// </summary>
        /// <param name="path">The full path of a file or folder</param>
        /// <param name="enabled">True to inherit</param>
        /// <param name="keepCopies">When disabling: true to convert the inherited entries to explicit entries, false to remove them</param>
        void SetInheritance(String path, bool enabled, bool keepCopies);
    }
}
