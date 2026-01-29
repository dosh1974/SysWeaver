using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.FolderSync
{
    public interface IFolderSyncApi : IRemoteApi
    {
        Task<FolderSyncResponse> SyncFolder(FolderSyncRequest r);

        Task<FolderPullResponse> SyncPullFolder(FolderPullRequest r);



    }
}
