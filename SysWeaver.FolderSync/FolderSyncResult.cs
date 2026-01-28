using System;

namespace SysWeaver.FolderSync
{
    public sealed class FolderSyncResult
    {
        /// <summary>
        /// Number of source files
        /// </summary>
        public long SourceFiles;
        
        /// <summary>
        /// Number of bytes in source files
        /// </summary>
        public long SourceBytes;
        
        /// <summary>
        /// Number of files that have to be uploaded
        /// </summary>
        public long TransferredCount;

        /// <summary>
        /// Number of source bytes that have to be uploaded
        /// </summary>
        public long TransferredSourceBytes;

        /// <summary>
        /// Number of network bytes sent (excluding headers)
        /// </summary>
        public long TransferredNetworkSize;

        /// <summary>
        /// Number of file chunks sent (all chunks in all missing files)
        /// </summary>
        public long ChunkCount = 0;
        /// <summary>
        /// Number of new chunks that was sent (all missing chunks in all missing files)
        /// </summary>
        public long NewChunkCount = 0;
        /// <summary>
        /// Total number of compressed bytes that was sent (all missing chunk data in all missing files)
        /// </summary>
        public long NewChunkSize = 0;

        /// <summary>
        /// List of errors
        /// </summary>
        public Exception[] Errors;
    }
}
