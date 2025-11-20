using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    public sealed class SmChunkFolderStats
    {
        public SmChunkFolderStats()
        {
        }
        internal SmChunkFolderStats(CdcFolderStats d)
        {
            Folder = d.Folder;
            DiscSize = d.DiscSize;
            ChunkCount = d.ChunkCount;
            ChunkSize = d.ChunkSize;
            Old = d.Old;
            OldCount = d.OldCount;
            OldSize = d.OldSize;
        }
        
        /// <summary>
        /// The folder where the chunks are stored or summary for all folders
        /// </summary>
        [TableDataText(30, "{0}", "{0}", true)]
        public String Folder;

        /// <summary>
        /// The estimated number of true bytes used
        /// </summary>
        [TableDataByteSize]
        public long DiscSize;

        /// <summary>
        /// Number of chunks (files)
        /// </summary>
        public long ChunkCount;

        /// <summary>
        /// The total size of the chunk content
        /// </summary>
        [TableDataByteSize]
        public long ChunkSize;

        /// <summary>
        /// Chunks that haven't been used before this time is considered old (as do zero size shunks and other files)
        /// </summary>
        public DateTime Old;

        /// <summary>
        /// Number of old files (will be removed soon)
        /// </summary>
        public long OldCount;

        /// <summary>
        /// The estimated number of true bytes used by old files (will be recalimed soon)
        /// </summary>
        [TableDataByteSize]
        public long OldSize;


    }

}
