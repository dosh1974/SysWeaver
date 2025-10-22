using System;

namespace SysWeaver.FolderSync
{
    public sealed class FolderSyncRequest
    {
        /// <summary>
        /// The folder
        /// </summary>
        public String Folder;

        /// <summary>
        /// The files
        /// </summary>
        public FolderSyncFile[] Files;

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
