using System;

namespace SysWeaver.MicroService
{


    public sealed class ManagedService
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
    }


    public sealed class ServerManagerParams
    {
        public ManagedService[] Services;
        public FolderSyncFolder[] Folders;
    }


    [RequiredDep<FolderSyncService>()]
    public sealed class ServerManagerService
    {
        public ServerManagerService(ServiceManager manager, ServerManagerParams p)
        {
            p = p ?? new ServerManagerParams();
            var s = manager.Get<FolderSyncService>();
            Syncer = s;
            foreach (var f in p.Folders.Nullable())
                s.AddFolder(f);


        }

        readonly FolderSyncService Syncer;

    }
}
