using System;
using System.Collections.Generic;

namespace SysWeaver
{

    /// <summary>
    /// Chunk statistics for a single file stored within a compacted (chunked) file
    /// </summary>
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

        /// <summary>
        /// Create chunk statistics for a file
        /// </summary>
        /// <param name="name">File name (local within compressed file)</param>
        /// <param name="chunkCount">Number of chunks</param>
        /// <param name="compressedSize">Sum of all chunk lengths</param>
        /// <param name="missingChunkCount">Number of missing chunks</param>
        /// <param name="expandedSize">Expanded size (optional)</param>
        public CdcChunkFileStats(string name, long chunkCount, long compressedSize, long missingChunkCount, long expandedSize)
        {
            Name = name;
            ChunkCount = chunkCount;
            CompressedSize = compressedSize;
            ExpandedSize = expandedSize;
            MissingChunkCount = missingChunkCount;
        }
    }


    /// <summary>
    /// Chunk statistics for a compacted (chunked) file, as returned by expand, recover and verify operations
    /// </summary>
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
        /// The unique chunks (names/hashes) referenced
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

        /// <summary>
        /// Create chunk statistics, null collections are replaced by empty ones
        /// </summary>
        /// <param name="fileSize">Size of the archive file</param>
        /// <param name="fileCount">Number of files</param>
        /// <param name="chunkCount">Total number of chunks</param>
        /// <param name="totalMissing">Total number of missing chunks</param>
        /// <param name="chunkCompSize">The total size of the compressed chunks</param>
        /// <param name="chunkExpSize">The total size of the expanded chunks (only available if specified)</param>
        /// <param name="missingChunks">All missing chunks</param>
        /// <param name="brokenFiles">All broken files</param>
        /// <param name="uniqueChunks">The unique chunks</param>
        /// <param name="files">File information</param>
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

        /// <summary>
        /// Statistics with all counts and sizes being zero and no chunks or files
        /// </summary>
        public static readonly CdcChunkStats Zero = new CdcChunkStats(0, 0, 0, 0, 0, 0, Empty, Empty, Empty, EmptyF);

    }
}
