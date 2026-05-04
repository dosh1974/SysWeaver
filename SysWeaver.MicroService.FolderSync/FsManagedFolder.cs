using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{

    public sealed class FsManagedFolder
    {
        /// <summary>
        /// Name of repository, used when synching
        /// </summary>
        public String Name;

        /// <summary>
        /// The folder to manage on disc
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// The auth required to sync this folder.
        /// null is equal to Roles.Debug
        /// </summary>
        public String Auth;

        /// <summary>
        /// The number of days to keep backup's
        /// </summary>
        public int RemoveBackupsDays = 30;

        /// <summary>
        /// Optional commands to execute before deactivating (before old folder is renamed to back-up name)
        /// </summary>
        public String OnDeactivate;

        /// <summary>
        /// Optional commands to execute to activate (after the folder have been replaced with old content)
        /// </summary>
        public String OnActivate;

        /// <summary>
        /// Optional commands to execute when a new folder is uploaded
        /// </summary>
        public String OnNewFolder;

        /// <summary>
        /// If true, folder versions are compressed.
        /// Activating (swapping) is slower but disc usage is reduced a lot (especially for many versions).
        /// </summary>
        public bool Compress;

        /// <summary>
        /// If true, data pushed to this folder may be pulled down
        /// </summary>
        public bool AllowPull;

        /// <summary>
        /// Optionally use this auth for pulling
        /// </summary>
        public String PullAuth;

        public delegate ValueTask<Exception> ActivationHandler(String name, String folderDiscPath, Func<String, ValueTask<int>> commandRunner);

        public ActivationHandler OnActivateAsync;
        public ActivationHandler OnDeactivateAsync;
        public ActivationHandler OnNewFolderAsync;
    }


    public sealed class FolderPullFolder
    {
        /// <summary>
        /// Name of repository, used when synching
        /// </summary>
        public String Name;

        /// <summary>
        /// The folder to manage on disc
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// The auth required to sync this folder.
        /// null is equal to Roles.Debug
        /// </summary>
        public String Auth;
    }


    public sealed class RemoteCachedFolder : CredentialParams
    {
        /// <summary>
        /// If true, perform a sync when starting the service
        /// </summary>
        public bool SyncOnStart = true;

        /// <summary>
        /// The remote server address (where the folder to pull resides)
        /// </summary>
        public String RemoteAddress;

        /// <summary>
        /// The remote repository name (the folder to pull)
        /// </summary>
        public String Name;

        /// <summary>
        /// Optional cache folder on disc (defaults to using the SysWaver folders)
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// Optional web folder (if empty, the folder won't be available)
        /// </summary>
        public String WebFolder;



    }


}
