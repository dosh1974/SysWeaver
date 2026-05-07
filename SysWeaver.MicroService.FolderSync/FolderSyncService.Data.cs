using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{


    public partial class FolderSyncService
    {
        public sealed class ManagedFolderData
        {
 

            /// <summary>
            /// True if active
            /// </summary>
            public bool IsActive;

            /// <summary>
            /// Folder name on disc
            /// </summary>
            [TableDataUrl("{0}", "*../edit/text.html?r=../FolderSync/" + nameof(GetManagedFolderManifest) + "?\"{1}/{0}\"", "Click to show the manifest file.")]
            public String DiscFolder;

            /// <summary>
            /// Name of the repo, use this when synchronizing a local folder.
            /// </summary>
            [TableDataUrl("{0}", "*../FolderSync/" + nameof(FolderSyncParams.ManagedFolders) + "/{0}/explore", "Click to explore \"{3}\".")]
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
            [TableDataText(30, "{0}", "{0}", true)]
            public String FullPath;

            /// <summary>
            /// Required auth
            /// </summary>
            [TableDataTags]
            public String Auth;


            internal ManagedFolder Folder;
        }


        public sealed class SharedFolderData
        {
            /// <summary>
            /// Name of the repo, use this when synchronizing a local folder.
            /// </summary>
            [TableDataUrl("{0}", "*../FolderSync/" + nameof(FolderSyncParams.SharedFolders) + "/{0}/explore", "Click to explore \"{3}\".")]
            public String Name;

            /// <summary>
            /// The version (hash of files)
            /// </summary>
            public String Version;

            /// <summary>
            /// Folder name on disc
            /// </summary>
            [TableDataText(30, "{0}", "{0}", true)]
            public String DiscFolder;


            /// <summary>
            /// Required auth
            /// </summary>
            [TableDataTags]
            public String Auth;




            internal SharedFolder Folder;
        }



        public sealed class RemoteFolderData
        {
            /// <summary>
            /// Local unique name of this remote folder
            /// </summary>
            public String Name;

            /// <summary>
            /// The current version
            /// </summary>
            public String Version;


            /// <summary>
            /// The remote server that this folder in synchronized from
            /// </summary>
            [TableDataUrl]
            public String RemoteAddress;

            /// <summary>
            /// name of the remote repo that this folder is synchronized from
            /// </summary>
            public String RemoteName;

            /// <summary>
            /// Folder name on disc
            /// </summary>
            [TableDataText(30, "{0}", "{0}", true)]
            public String DiscFolder;

            /// <summary>
            /// The last exception registered
            /// </summary>
            [TableDataText(30)]
            public String LastException;

            /// <summary>
            /// Number of times an exception have been registered
            /// </summary>
            public long ExCount;

            /// <summary>
            /// The time stamp (in ticks) when the last fail happened, use new DateTime(ticks, DateTimeKind.Utc) to get a DateTime time
            /// </summary>
            public DateTime ExLastTime;

            /// <summary>
            /// Number of hours to keep old versions on disc
            /// </summary>
            [TableDataNumber(0, "{0} h")]
            public int DeleteAfterHours;


            /// <summary>
            /// True if this folders is served by the http service (aka available).
            /// Column to the right is only available if true.
            /// </summary>
            public bool IsServed;

            /// <summary>
            /// The web folder to serve this folder at
            /// </summary>
            [TableDataUrl("{0}", "*../{0}/explore", "Click to explore \"{3}\".")]
            public String WebFolder;


            /// <summary>
            /// The method to use for solving the problem with live swapping.
            /// When swapping a folder to a new version there is a risk that the data is currently being loaded by a clieny, hence end up with files from mixed versions, causing all kind of trouble.
            /// This is especially true when caching is involved.
            /// The only way to be sure that this can't happen is to make sure that url's of the new files haven't been used before (aka located in a unique folder).
            /// There are two methods to automatically mitigate this:
            /// 
            /// IFrame page:
            ///     The index.html (and/or optional .html files) of the folder is "replaced" by a small shim page that iframe's the real page (at it's unqiue location).
            ///     Pros:
            ///         - The web brower url stays the same, automatic reloading when a new version happens may optionally be handled here.
            ///         - Refreshing the page, loads the latest version.
            ///     Cons:
            ///         - Some behaviors such as meta tags etc can't be overridden,
            ///         - Only works if the data is html pages, pure assets etc will not work.
            ///         
            /// Http redirect:
            ///     Uses a 307 redirect for all requests to the versioned page.
            ///     Pros:
            ///         - Will redirect any request.
            ///     Cons:
            ///         - Will change the address bar url.
            ///         - Page refresh will reload the same version (not a potentionally newer version).
            ///         - Bookmarking will be version specific.
            ///         - Every request will have to roundtrip one extra time.
            ///         - Two or more requests may end up with files from different versions (if a switch was made in between).
            ///         
            /// </summary>
            public String SwapMethod;

            /// <summary>
            /// If true and the swap method is an iframe page, version checking is available and auto reload may be activated. 
            /// Version checking/reloading can be done manually from the iframe page by sending a the message with the data { "Type": "CheckVersion" }.
            /// </summary>
            public bool VersionCheck;

            /// <summary>
            /// When the swap method is an iframe page, the web page can be auto reloaded if a new version is available, this value determines the version check interval in seconds.
            /// Set to zero to disable automatic version checking.
            /// Version checking/reloading can be done manually from the iframe page by sending a the message with the data { "Type": "CheckVersion" }.
            /// </summary>
            [TableDataNumber(0, "{0} s")]
            public int AutoReload;


            /// <summary>
            /// Number of seconds to cache the file on a client
            /// </summary>
            [TableDataNumber(0, "{0} s")]
            public int ClientCacheDuration = 5;

            /// <summary>
            /// Number of seconds to cache any intermediate results (i.e small files that are compressed on the fly)
            /// </summary>
            [TableDataNumber(0, "{0} s")]
            public int RequestCacheDuration = 30;

            /// <summary>
            /// The maximum size of a file that can be cached
            /// </summary>
            [TableDataByteSize]
            public long MaxCacheSize = 32768;

            /// <summary>
            /// The preferred on the fly compression schemes
            /// </summary>
            public String Compression = "br: Balanced, deflate: Balanced, gzip: Balanced";

            /// <summary>
            /// If true, compressed files that have a compressed version may be served, i.e "Test.txt.gzip" may be served in place of "Test.txt" if Test.txt is older or non existent.
            /// </summary>
            public bool AssumePreCompressed = true;

            /// <summary>
            /// The required auth for these files (null = no auth required, "" = no special auth token is required, but user must be authenticated)
            /// </summary>
            [TableDataTags]
            public String Auth;

            /// <summary>
            /// If true, the file's access time is updated whenever the file is read
            /// </summary>
            public bool UpdateAccessTime;

            /// <summary>
            /// If true, files in this folder are marked as dynamic hence bypass any transformer chains
            /// </summary>
            public bool IsDynamic;


        }

    }

}
