using System;

namespace SysWeaver.FolderSync
{
    public sealed class LocalFolderInfo
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
        /// Populate with CdcProps.Default.Key if Cdc is requested
        /// </summary>
        public String Cdc;
    }

}
