using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// Identifies a version of a shared folder
    /// </summary>
    public sealed class SharedFolderSyncRequest
    {
        /// <summary>
        /// The folder
        /// </summary>
        public String Folder;

        /// <summary>
        /// The version of the remote repository
        /// </summary>
        public String Version;

    }

}
