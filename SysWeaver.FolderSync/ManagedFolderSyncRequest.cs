using System;

namespace SysWeaver.FolderSync
{


    public sealed class ManagedFolderSyncRequest
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
        /// Populate with CdcProps.Default.Key if Cdc is requested
        /// </summary>
        public String Cdc;

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
