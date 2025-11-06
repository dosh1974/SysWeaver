using System;
using System.Collections.Generic;

namespace SysWeaver
{
    public sealed class CdcChunkStats
    {
        /// <summary>
        /// Number of files
        /// </summary>
        public readonly long FileCount;

        /// <summary>
        /// Total number of chunks
        /// </summary>
        public readonly long ChunkCount;

        /// <summary>
        /// Total number of unique chunks
        /// </summary>
        public readonly IReadOnlyCollection<String> UniqueChunks;

        /// <summary>
        /// Total number of missing chunks
        /// </summary>
        public readonly long TotalMissing;

        /// <summary>
        /// All missing chunks
        /// </summary>
        public readonly IReadOnlyCollection<String> MissingChunks;

        /// <summary>
        /// All broken files
        /// </summary>
        public readonly IReadOnlyCollection<String> BrokenFiles;


        public CdcChunkStats(long fileCount, long chunkCount, long totalMissing, IReadOnlyCollection<String> missingChunks, IReadOnlyCollection<String> brokenFiles, IReadOnlyCollection<String> uniqueChunks)
        {
            UniqueChunks = uniqueChunks;
            FileCount = fileCount;
            ChunkCount = chunkCount;
            TotalMissing = totalMissing;
            MissingChunks = missingChunks;
            BrokenFiles = brokenFiles;
        }

        static readonly IReadOnlyCollection<String> Empty = new List<string>();

        public static readonly CdcChunkStats Zero = new CdcChunkStats(0, 0, 0, Empty, Empty, Empty);

    }
}
