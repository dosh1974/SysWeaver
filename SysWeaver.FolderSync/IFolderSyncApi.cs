using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.FolderSync
{
    public interface IFolderSyncApi : IRemoteApi
    {
        /// <summary>
        /// Check if there are any differences in the managed folder
        /// </summary>
        /// <param name="r">Folder and local files</param>
        /// <returns>Changes required to sync the managed folder</returns>
        Task<ManagedFolderDiff> CheckManagedFolder(ManagedFolderSyncRequest r);

        /// <summary>
        /// Check if a new version of a shared folder is available
        /// </summary>
        /// <param name="r">Folder name and version (aka hash)</param>
        /// <returns>True if a new version is available</returns>
        Task<bool> SharedFolderHasChanged(SharedFolderSyncRequest r);

        /// <summary>
        /// Check for updates against a shared folder
        /// </summary>
        /// <param name="r">Folder and local files</param>
        /// <returns>Changes required to sync the local folder</returns>
        Task<SharedFolderDiff> CheckSharedFolder(LocalFolderInfo r);



    }


    /// <summary>
    /// End points that isn't called using a json API since it requires binary reads and/or writes (more optimal)
    /// </summary>
    public interface IFolderSyncEndPoints
    {

        /// <summary>
        /// Get the list of chunk hashes for a file
        /// </summary>
        /// <param name="r">Folder and file to get hash chunks for</param>
        /// <returns>Hashes for the chunks that make up the file</returns>
        ReadOnlyMemory<Byte> GetSharedFileChunks(SharedFileChunksRequest r);
        
        /// <summary>
        /// Get chunk content for the given hashes
        /// </summary>
        /// <param name="chunkHashes">Array of hashes that we want the data for</param>
        /// <returns>Data for the compressed chunks</returns>
        ReadOnlyMemory<Byte> GetChunks(ReadOnlyMemory<Byte> chunkHashes);
    }


}
