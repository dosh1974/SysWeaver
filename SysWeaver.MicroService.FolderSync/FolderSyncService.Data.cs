using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        sealed class Data
        {
 

            /// <summary>
            /// True if active
            /// </summary>
            public bool IsActive;

            /// <summary>
            /// Folder name on disc
            /// </summary>
            [TableDataUrl("{0}", "../FolderSync/GetSynchedFolderManifest?\"{1}/{0}\"", "Click to show the manifest file.")]
            public String DiscFolder;

            /// <summary>
            /// Name of the repo, use this when synchronizing a local folder.
            /// </summary>
            [TableDataUrl("{0}", "*../FolderSync/Folders/{0}/explore", "Click to explore \"{3}\".")]
            public String Name;


            /// <summary>
            /// Number of files in the folder
            /// </summary>
            public long Count;

            /// <summary>
            /// The number of bytes (sum of all file sizes)
            /// </summary>
            [TableDataByteSize]
            public long Size;

            /// <summary>
            /// Folder creation time
            /// </summary>
            [TableDataSortDesc]
            public DateTime Uploaded;

            /// <summary>
            /// True if compressed
            /// </summary>
            public bool Comp;

            /// <summary>
            /// The service user that uploaded this
            /// </summary>
            public String User;

            /// <summary>
            /// The name of the source machine (this can be anything)
            /// </summary>
            public String Machine;

            /// <summary>
            /// Optional comment supplied when uploading this folder
            /// </summary>
            [TableDataText]
            public String Comment;

            /// <summary>
            /// Actions that can be performed
            /// </summary>
            [TableDataActions(
                "Activate", 
                "Click to activate this folder (rename to base name)",
                "../FolderSync/" + nameof(Activate) + "?{0}",
                "IconOk",

                "Remove",
                "Click to remove this folder",
                "../FolderSync/" + nameof(Remove) + "?{0}",
                "IconCancel"
                )]
            public String Actions;

            /// <summary>
            /// When folder was last used (as active)
            /// </summary>
            [TableDataSortDesc]
            public DateTime LastUsed;

            /// <summary>
            /// Full path
            /// </summary>
            [TableDataText]
            public String FullPath;

            /// <summary>
            /// Required auth
            /// </summary>
            [TableDataTags]
            public String Auth;


            internal Folder Folder;
        }


    }

}
