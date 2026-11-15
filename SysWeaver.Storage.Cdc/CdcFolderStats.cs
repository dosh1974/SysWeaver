using System;

namespace SysWeaver
{

    /// <summary>
    /// Statistics about a chunk storage folder
    /// </summary>
    public sealed class CdcFolderStats
    {
        /// <summary>
        /// The folder (or "SUMMARY" for merged statistics)
        /// </summary>
        public readonly String Folder;

        /// <summary>
        /// Estimated size on disc of all files in the folder (each file rounded up to 4096 byte clusters)
        /// </summary>
        public readonly long DiscSize;

        /// <summary>
        /// Number of chunk files
        /// </summary>
        public readonly long ChunkCount;

        /// <summary>
        /// Total size of the (compressed) chunk files in bytes
        /// </summary>
        public readonly long ChunkSize;

        /// <summary>
        /// Total size of the chunks when decompressed, only computed if requested (else 0)
        /// </summary>
        public readonly long ChunkUncompressedSize;

        /// <summary>
        /// Number of files that aren't chunks
        /// </summary>
        public readonly long OtherCount;

        /// <summary>
        /// Total size of files that aren't chunks in bytes
        /// </summary>
        public readonly long OtherSize;

        /// <summary>
        /// The UTC time used to classify files as old (chunks not used since this time, and all non-chunk files)
        /// </summary>
        public readonly DateTime Old;

        /// <summary>
        /// Number of old files
        /// </summary>
        public readonly long OldCount;

        /// <summary>
        /// Estimated size on disc of the old files
        /// </summary>
        public readonly long OldSize;


        /// <inheritdoc />
        public override string ToString() => String.Concat(Folder, " [", DiscSize, ']');

        /// <summary>
        /// Create folder statistics
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="discSize">Estimated size on disc of all files</param>
        /// <param name="chunkCount">Number of chunk files</param>
        /// <param name="chunkSize">Total size of the chunk files</param>
        /// <param name="chunkUncompressedSize">Total size of the decompressed chunks</param>
        /// <param name="otherCount">Number of files that aren't chunks</param>
        /// <param name="otherSize">Total size of files that aren't chunks</param>
        /// <param name="old">The UTC time used to classify files as old</param>
        /// <param name="oldDiscSize">Estimated size on disc of the old files</param>
        /// <param name="oldFileCount">Number of old files</param>
        public CdcFolderStats(string folder, 
            long discSize, long chunkCount, long chunkSize, 
            long chunkUncompressedSize, long otherCount, long otherSize,
            DateTime old, long oldDiscSize, long oldFileCount
            )
        {
            Folder = folder;
            DiscSize = discSize;
            ChunkCount = chunkCount;
            ChunkSize = chunkSize;
            ChunkUncompressedSize = chunkUncompressedSize;
            OtherCount = otherCount;
            OtherSize = otherSize;
            Old = old;
            OldSize = oldDiscSize;
            OldCount = oldFileCount;
        }


        /// <summary>
        /// Sum this and another set of statistics
        /// </summary>
        /// <param name="other">The statistics to add</param>
        /// <returns>New statistics named "SUMMARY", <see cref="Old"/> is kept if equal in both, else <see cref="DateTime.MinValue"/></returns>
        public CdcFolderStats Merge(CdcFolderStats other)
            => new CdcFolderStats(
                "SUMMARY",
                DiscSize + other.DiscSize,
                ChunkCount + other.ChunkCount,
                ChunkSize + other.ChunkSize,
                ChunkUncompressedSize + other.ChunkUncompressedSize,
                OtherCount + other.OtherCount,
                OtherSize + other.OtherSize,
                Old == other.Old ? Old : DateTime.MinValue,
                OldSize + other.OldSize,
                OldCount + other.OldCount
            );


    }


}
