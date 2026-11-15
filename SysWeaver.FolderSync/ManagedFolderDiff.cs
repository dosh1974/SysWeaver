using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// The changes required to sync a managed folder
    /// </summary>
    public sealed class ManagedFolderDiff
    {
        /// <summary>
        /// The code to use when uploading files
        /// </summary>
        public String FolderCode;

        /// <summary>
        /// The files to upload
        /// </summary>
        public String[] Files;

        /// <summary>
        /// If non-null and the CdcProps.Default.Key matches this string, prefer to use Cdc for upload
        /// </summary>
        public String Cdc;
    }

}
