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
    }

}
