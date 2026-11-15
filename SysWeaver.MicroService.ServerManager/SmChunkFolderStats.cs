using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Storage statistics of a folder with chunks (content defined chunking storage)
    /// </summary>
    [SysWeaver.Data.TableDataCard("storage@ServerManager/cards")]
    [TableDataPrimaryKey(nameof(Folder))]
    public sealed class SmChunkFolderStats
    {
        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
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
        /// Chunks that haven't been used since this time are considered old (as are zero size chunks and other files)
        /// </summary>
        public DateTime Old;

        /// <summary>
        /// Number of old files (will be removed soon)
        /// </summary>
        public long OldCount;

        /// <summary>
        /// The estimated number of true bytes used by old files (will be reclaimed soon)
        /// </summary>
        [TableDataByteSize]
        public long OldSize;


    }

}
