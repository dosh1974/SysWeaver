using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{




    public sealed class FolderSyncFolder
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
        /// The auth required to sync this folder
        /// </summary>
        public String Auth = Roles.Debug;

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

        public delegate ValueTask<Exception> ActivationHandler(String name, String folderDiscPath, Func<String, ValueTask<int>> commandRunner);

        public ActivationHandler OnActivateAsync;
        public ActivationHandler OnDeactivateAsync;
        public ActivationHandler OnNewFolderAsync;
    }
}
