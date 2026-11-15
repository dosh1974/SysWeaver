using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// The audit information of a request to sync a managed folder
    /// </summary>
    public sealed class FolderSyncRequestAudit
    {
        /// <summary>
        /// The folder
        /// </summary>
        public string Folder;

        /// <summary>
        /// The number of source files
        /// </summary>
        public long FileCount;

        /// <summary>
        /// If true, the newly synched folder will be activated directly
        /// </summary>
        public bool UseFolder;

        /// <summary>
        /// The source machine name
        /// </summary>
        public String Machine;

        /// <summary>
        /// An optional comment
        /// </summary>
        public String Comment;

    }

}
