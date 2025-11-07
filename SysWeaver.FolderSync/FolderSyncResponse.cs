using System;

namespace SysWeaver.FolderSync
{
    public sealed class FolderSyncResponse
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
