using System;

namespace SysWeaver.FolderSync
{


    public sealed class CdcFilePullRequest
    {
        /// <summary>
        /// The folder
        /// </summary>
        public String Folder;

        /// <summary>
        /// The file
        /// </summary>
        public String File;
    }

    public sealed class FolderPullRequest
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

    public sealed class FolderPullResponse
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
        /// If non-null and the CdcProps.Default.Key matches this string, prefer to use Cdc for upload
        /// </summary>
        public String Cdc;
    }


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
