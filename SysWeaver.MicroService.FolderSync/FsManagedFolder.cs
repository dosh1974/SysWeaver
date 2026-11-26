using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// A folder that can be updated remotely (pushed to), the previous versions are kept as backups
    /// </summary>
    public sealed class FsManagedFolder
    {
        /// <summary>
        /// Name of repository, used when synching.
        /// If null or empty, the name of the disc folder is used.
        /// </summary>
        public String Name;

        /// <summary>
        /// The folder to manage on disc (path templates are resolved)
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// The auth required to sync this folder.
        /// null is equal to <see cref="Roles.ServiceSync"/> ("service_sync", "debug" or "dev").
        /// The caller also needs <see cref="FolderSyncParams.ApiAuth"/>.
        /// </summary>
        public String Auth;

        /// <summary>
        /// The number of days to keep backup's (negative values are treated as 0).
        /// Defaults to 30.
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
        /// Optionally use this auth for pulling (only used if <see cref="AllowPull"/> is true), null to use <see cref="Auth"/>
        /// </summary>
        public String PullAuth;

        /// <summary>
        /// The signature of the handlers that run when a folder is activated, deactivated or a new folder is uploaded
        /// </summary>
        /// <param name="name">The name of the managed folder</param>
        /// <param name="folderDiscPath">The path of the folder on disc</param>
        /// <param name="commandRunner">Runs a command line (returns the exit code)</param>
        /// <returns>null on success, else the exception (for activate and deactivate the activation is stopped and the exception is returned, for a new folder it is only reported)</returns>
        public delegate Task<Exception> ActivationHandler(String name, String folderDiscPath, Func<String, Task<int>> commandRunner);

        /// <summary>
        /// Optional code to run when the folder is activated (after <see cref="OnActivate"/>)
        /// </summary>
        public ActivationHandler OnActivateAsync;

        /// <summary>
        /// Optional code to run before the folder is deactivated (after <see cref="OnDeactivate"/>)
        /// </summary>
        public ActivationHandler OnDeactivateAsync;

        /// <summary>
        /// Optional code to run when a new folder is uploaded (after <see cref="OnNewFolder"/>)
        /// </summary>
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
