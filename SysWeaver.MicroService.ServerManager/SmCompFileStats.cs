namespace SysWeaver.MicroService
{
    public sealed class SmCompFileStats
    {
        /// <summary>
        /// Number of files
        /// </summary>
        public long FileCount;

        /// <summary>
        /// Total number of chunks
        /// </summary>
        public long ChunkCount;

        /// <summary>
        /// Number of unique chunks
        /// </summary>
        public long UniqueChunks;

        /// <summary>
        /// Total number of missing chunks
        /// </summary>
        public long TotalMissing;

        /// <summary>
        /// Number of broken files
        /// </summary>
        public long BrokenFiles;

        /// <summary>
        /// The total size of the compressed chunks
        /// </summary>
        public long ChunkCompSize;

        /// <summary>
        /// The total size of the expanded chunks (only available if specified)
        /// </summary>
        public long ChunkExpSize;

        /// <summary>
        /// Size of the archive file
        /// </summary>
        public long FileSize;

        public SmCompFileStats()
        {
        }

        internal SmCompFileStats(CdcChunkStats s)
        {
            FileSize = s.FileSize;
            FileCount = s.FileCount;
            ChunkCount = s.ChunkCount;
            UniqueChunks = s.UniqueChunks.Count;
            TotalMissing = s.TotalMissing;
            BrokenFiles = s.BrokenFiles?.Count ?? 0;
            ChunkCompSize = s.ChunkCompSize;
            ChunkExpSize = s.ChunkExpSize;
        }

    }


}
