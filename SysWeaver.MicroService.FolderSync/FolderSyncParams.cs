namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="FolderSyncService"/>
    /// </summary>
    public sealed class FolderSyncParams
    {
        /// <summary>
        /// A list of managed folders.
        /// A managed folder can be update remotely using the SwSyncTool's push command or by using the SysWeaver.FolderSyncer class.
        /// </summary>
        public FsManagedFolder[] ManagedFolders;

        /// <summary>
        /// A list of shared folders.
        /// Shared folder can be synced down to clients using the SwSyncTool's pull command or by using the SysWeaver.FolderSyncer class.
        /// </summary>
        public FsSharedFolder[] SharedFolders;

        /// <summary>
        /// A list of folders that in automatically synchronized from some source
        /// </summary>
        public FsRemoteFolder[] RemoteFolders;

        /// <summary>
        /// The default value of <see cref="ApiAuth"/>: the "service_sync" token (API keys for sync clients), the "debug" and "dev" tokens, and admins and ops.
        /// </summary>
        public const System.String DefaultApiAuth = Roles.ServiceSync + "," + Roles.AdminOps;

        /// <summary>
        /// The auth needed to call the sync API's (check, push, pull, activate, remove) and the upload / chunk end points, checked before the
        /// auth of the folder (<see cref="FsManagedFolder.Auth"/>, <see cref="FsSharedFolder.Auth"/>), so a caller needs both.
        /// Must name tokens: "" (any signed in user) would refuse API keys, they only get access to what names one of their tokens.
        /// Defaults to <see cref="DefaultApiAuth"/>.
        /// </summary>
        public System.String ApiAuth = DefaultApiAuth;

    }






}
