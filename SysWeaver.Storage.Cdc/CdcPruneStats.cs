using System;

namespace SysWeaver
{
    public sealed class CdcPruneStats
    {
        public readonly String Folder;
        public readonly long BeforeDiscSize;
        public readonly long BeforeFileCount;

        public readonly long PruneDiscSize;
        public readonly long PruneFileCount;
        public readonly long DeleteErrors;

        public readonly String DeleteErr;

        public readonly DateTime Old;

        public override string ToString() => String.Concat(Folder, " [", PruneDiscSize, ']');

        public CdcPruneStats(string folder, DateTime old, long beforeDiscSize, long beforeFileCount, long pruneDiscSize, long pruneFileCount, long deleteErrors, String deleteErr)
        {
            Folder = folder;
            Old = old;
            BeforeDiscSize = beforeDiscSize;
            BeforeFileCount = beforeFileCount; 
            PruneDiscSize = pruneDiscSize;
            PruneFileCount = pruneFileCount;
            DeleteErrors = deleteErrors;
            DeleteErr = deleteErr;
        }


        public CdcPruneStats Merge(CdcPruneStats other)
            => new CdcPruneStats(
                "SUMMARY",
                Old == other.Old ? Old : DateTime.MinValue,
                BeforeDiscSize + other.BeforeDiscSize,
                BeforeFileCount + other.BeforeFileCount,
                PruneDiscSize + other.PruneDiscSize,
                PruneFileCount + other.PruneFileCount,
                DeleteErrors + other.DeleteErrors,
                DeleteErr ?? other.DeleteErr
            );

    }


}
