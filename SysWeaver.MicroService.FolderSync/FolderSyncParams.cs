namespace SysWeaver.MicroService
{
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
        public FolderPullFolder[] SharedFolders;

        /// <summary>
        /// A list of folders that in automatically synchronized from some source
        /// </summary>
        public RemoteCachedFolder[] RemoteFolders;

    }






}
