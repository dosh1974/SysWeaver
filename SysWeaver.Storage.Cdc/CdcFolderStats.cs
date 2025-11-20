using System;

namespace SysWeaver
{

    public sealed class CdcFolderStats
    {
        public readonly String Folder;
        public readonly long DiscSize;

        public readonly long ChunkCount;
        public readonly long ChunkSize;
        public readonly long ChunkUncompressedSize;

        public readonly long OtherCount;
        public readonly long OtherSize;

        public readonly DateTime Old;
        public readonly long OldCount;
        public readonly long OldSize;


        public override string ToString() => String.Concat(Folder, " [", DiscSize, ']');

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
