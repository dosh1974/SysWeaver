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

        public delegate Task<Exception> ActivationHandler(String name, String folderDiscPath, Func<String, Task<int>> commandRunner);

        public ActivationHandler OnActivateAsync;
        public ActivationHandler OnDeactivateAsync;
        public ActivationHandler OnNewFolderAsync;

        /// <summary>
        /// Takes over the activation of a folder (ex: when the process doing the swap is running from the active folder and must be stopped by it).
        /// </summary>
        /// <param name="name">The name of the managed folder</param>
        /// <param name="targetFolder">The active folder (no trailing separator)</param>
        /// <param name="backupFolder">The name that the active folder should get (no trailing separator)</param>
        /// <param name="newFolder">The folder that should replace the active folder (no trailing separator, already decompressed)</param>
        /// <returns>null to let the folder sync service do the activation (in process), else the task of the external activation: it's result is null on success (ex: the swap was started) or the exception.
        /// When a task is returned, <see cref="OnDeactivate"/>, <see cref="OnDeactivateAsync"/>, the folder swap, <see cref="OnActivate"/>, <see cref="OnActivateAsync"/> and the compression of the backup are skipped,
        /// the handler is responsible for all of it.</returns>
        public delegate Task<Exception> SwapHandler(String name, String targetFolder, String backupFolder, String newFolder);

        /// <summary>
        /// Optional, see <see cref="SwapHandler"/>.
        /// </summary>
        public SwapHandler OnSwapAsync;
    }


}
