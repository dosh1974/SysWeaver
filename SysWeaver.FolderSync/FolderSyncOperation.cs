using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// Identifies a folder that an operation (ex: activating a version) is done on
    /// </summary>
    public sealed class FolderSyncOperation
    {
        /// <summary>
        /// The folder
        /// </summary>
        public String Folder;

        /// <summary>
        /// The name of the folder on disc
        /// </summary>
        public String DiscFolder;
    }

}
