using System;

namespace SysWeaver.MicroService
{
    public sealed class ServerManagerParams
    {
        /// <summary>
        /// If true, service versions are compressed, saving disc space and transfer size at the cost of slower switching
        /// </summary>
        public bool CompressServices = true;

        /// <summary>
        /// Where managed services are located
        /// </summary>
        public String ServiceFolder;

        /// <summary>
        /// The number of days to keep backup's
        /// </summary>
        public int RemoveServiceBackupsDays = 365;

        /// <summary>
        /// SysWeaver services that should be managed
        /// </summary>
        public ManagedService[] Services;

        /// <summary>
        /// Other folders that should be managed
        /// </summary>
        public FolderSyncFolder[] Folders;
    }

}
