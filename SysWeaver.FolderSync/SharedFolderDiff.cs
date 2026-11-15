using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// The changes required to sync a local folder with a shared folder
    /// </summary>
    public sealed class SharedFolderDiff
    {
        /// <summary>
        /// The files that need to be downloaded
        /// </summary>
        public FolderSyncFile[] Download;

        /// <summary>
        /// The files that need to be copied
        /// </summary>
        public String[] Keep;

        /// <summary>
        /// If non-null and the CdcProps.Default.Key matches this string, prefer to use Cdc for download
        /// </summary>
        public String Cdc;

        /// <summary>
        /// The version of the remote repository
        /// </summary>
        public String Version;
    }

}
