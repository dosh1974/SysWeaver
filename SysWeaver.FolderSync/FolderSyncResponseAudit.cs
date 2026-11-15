using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// The audit information of the response to a request to sync a managed folder
    /// </summary>
    public sealed class FolderSyncResponseAudit
    {
        /// <summary>
        /// The code to use when uploading files
        /// </summary>
        public String FolderCode;

        /// <summary>
        /// The number of files to upload
        /// </summary>
        public long FileCount;

        /// <summary>
        /// If non-null and the CdcProps.Default.Key matches this string, prefer to use Cdc for upload
        /// </summary>
        public String Cdc;

    }

}
