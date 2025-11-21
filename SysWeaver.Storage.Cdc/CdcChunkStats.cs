using System;
using System.Collections.Generic;

namespace SysWeaver
{

    public sealed class CdcChunkFileStats
    {
        /// <summary>
        /// File name (local within compressed file)
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// Number of chunks
        /// </summary>
        public readonly long ChunkCount;

        /// <summary>
        /// Sum of all chunk lengths
        /// </summary>
        public readonly long CompressedSize;

        /// <summary>
        /// Number of missing chunks
        /// </summary>
        public readonly long MissingChunkCount;

        /// <summary>
        /// Expanded size (optional)
        /// </summary>
        public readonly long ExpandedSize;

        public CdcChunkFileStats(string name, long chunkCount, long compressedSize, long missingChunkCount, long expandedSize)
        {
            Name = name;
            ChunkCount = chunkCount;
            CompressedSize = compressedSize;
            ExpandedSize = expandedSize;
            MissingChunkCount = missingChunkCount;
        }
    }


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
        /// The total size of the compressed chunks
        /// </summary>
        public readonly long ChunkCompSize;

        /// <summary>
        /// The total size of the expanded chunks (only available if specified)
        /// </summary>
        public readonly long ChunkExpSize;

        /// <summary>
        /// Total number of missing chunks
        /// </summary>
        public readonly long TotalMissing;

        /// <summary>
        /// Total number of unique chunks
        /// </summary>
        public readonly IReadOnlyCollection<String> UniqueChunks;

        /// <summary>
        /// All missing chunks
        /// </summary>
        public readonly IReadOnlyCollection<String> MissingChunks;

        /// <summary>
        /// All broken files
        /// </summary>
        public readonly IReadOnlyCollection<String> BrokenFiles;

        /// <summary>
        /// File information
        /// </summary>
        public readonly IReadOnlyCollection<CdcChunkFileStats> Files;

        /// <summary>
        /// Size of the archive file
        /// </summary>
        public readonly long FileSize;

        public CdcChunkStats(
            long fileSize,
            long fileCount, 
            long chunkCount, 
            long totalMissing,
            long chunkCompSize,
            long chunkExpSize,
            IReadOnlyCollection<String> missingChunks, 
            IReadOnlyCollection<String> brokenFiles, 
            IReadOnlyCollection<String> uniqueChunks,
            IReadOnlyCollection<CdcChunkFileStats> files)
        {
            FileSize = fileSize;
            FileCount = fileCount;
            ChunkCount = chunkCount;
            ChunkCompSize = chunkCompSize;
            ChunkExpSize = chunkExpSize;
            TotalMissing = totalMissing;
            MissingChunks = missingChunks ?? Empty;
            BrokenFiles = brokenFiles ?? Empty;
            UniqueChunks = uniqueChunks ?? Empty;
            Files = files ?? EmptyF;
        }

        static readonly IReadOnlyCollection<String> Empty = new List<string>();
        static readonly IReadOnlyCollection<CdcChunkFileStats> EmptyF = new List<CdcChunkFileStats>();

        public static readonly CdcChunkStats Zero = new CdcChunkStats(0, 0, 0, 0, 0, 0, Empty, Empty, Empty, EmptyF);

    }
}
