using System;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed class FsRemoteFolder : CredentialParams
    {
        /// <summary>
        /// If true, perform a sync when starting the service
        /// </summary>
        public bool SyncOnStart = true;

        /// <summary>
        /// The unique local name of this remote folder
        /// </summary>
        public String Name;

        /// <summary>
        /// The remote server address (where the folder to pull resides)
        /// </summary>
        public String RemoteAddress;

        /// <summary>
        /// The remote repository name (the folder to pull), if null or empty Name is used.
        /// </summary>
        public String RemoteName;

        /// <summary>
        /// Optional cache folder on disc (defaults to using the SysWaver folders)
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// Optional web folder paramaters, if null the folder won't be available
        /// </summary>
        public FileHttpServerModuleWebFolder WebFolder;

        /// <summary>
        /// The method to use for solving the problem with live swapping.
        /// When swapping a folder to a new version there is a risk that the data is currently being loaded, hence end up with files from mixed versions.
        /// This is especially true when caching is involved.
        /// The only way to be sure that this can't happen is to make sure that url's of the new files haven't been used before (aka located in a unique folder).
        /// There are two methods to mitigate this.
        /// </summary>
        public WebFolderSwapMethods SwapMethod = WebFolderSwapMethods.IFramePage;

        /// <summary>
        /// Optional list of html files to make a shim for when the swap method is iframe page.
        /// If null or empty, just "index.html" will be created.
        /// </summary>
        public String[] HtmlShims;

        /// <summary>
        /// If true and the swap method is an iframe page, version checking is available and auto reload may be activated. 
        /// Version checking/reloading can be done manually from the iframe page by sending a the message with the data { "Type": "CheckVersion" }.
        /// </summary>
        public bool VersionCheck = true;

        /// <summary>
        /// When the swap method is an iframe page, the web page can be auto reloaded if a new version is available, this value determines the version check interval in seconds.
        /// Set to zero to disable automatic version checking.
        /// Version checking/reloading can be done manually from the iframe page by sending a the message with the data { "Type": "CheckVersion" }.
        /// </summary>
        public int AutoReload = 15;

        /// <summary>
        /// Number of hours to keep old versions on disc
        /// </summary>
        public int DeleteAfterHours = 4;
    }


    /// <summary>
    /// The method to use for solving the problem with live swapping.
    /// When swapping a folder to a new version there is a risk that the data is currently being loaded, hence end up with files from mixed versions.
    /// This is especially true when caching is involved.
    /// The only way to be sure that this can't happen is to make sure that url's of the new files haven't been used before (aka located in a unique folder).
    /// There are two methods to mitigate this.
    /// </summary>
    public enum WebFolderSwapMethods
    {
        /// <summary>
        /// The index.html (and/or optional .html files) of the folder is "replaced" by a small shim page that iframe's the real page (at it's unqiue location).
        /// Pros:
        /// - The web brower url stays the same, automatic reloading when a new version happens may optionally be handled here.
        /// - Refreshing the page, loads the latest version.
        /// Cons:
        /// - Some behaviors such as meta tags etc can't be overridden,
        /// - Only works if the data is html pages, pure assets etc will not work.
        /// </summary>
        IFramePage = 0,
        /// <summary>
        /// Uses a 307 redirect for all requests to the versioned page.
        /// Pros:
        /// - Will redirect any request.
        /// Cons:
        /// - Will change the address bar url.
        /// - Page refresh will reload the same version (not a potentionally newer version).
        /// - Every request will have to roundtrip an extra time.
        /// - Two or more requests may end up with files from different versions (is a switch was made in between).
        /// </summary>
        HttpRedirect,
        /// <summary>
        /// Don't even try to solve it, just let files become mixed.
        /// </summary>
        None
    }


}
