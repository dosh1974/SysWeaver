using System;

namespace SysWeaver
{
    /// <summary>
    /// Statistics from pruning a chunk storage folder
    /// </summary>
    public sealed class CdcPruneStats
    {
        /// <summary>
        /// The folder (or "SUMMARY" for merged statistics)
        /// </summary>
        public readonly String Folder;

        /// <summary>
        /// Estimated size on disc of all files before pruning
        /// </summary>
        public readonly long BeforeDiscSize;

        /// <summary>
        /// Number of files before pruning
        /// </summary>
        public readonly long BeforeFileCount;

        /// <summary>
        /// Estimated size on disc of the files that were deleted
        /// </summary>
        public readonly long PruneDiscSize;

        /// <summary>
        /// Number of files that were deleted
        /// </summary>
        public readonly long PruneFileCount;

        /// <summary>
        /// Number of files that failed to delete
        /// </summary>
        public readonly long DeleteErrors;

        /// <summary>
        /// The first delete error message, null if no errors
        /// </summary>
        public readonly String DeleteErr;

        /// <summary>
        /// The UTC time used to classify chunks as old (chunks not used since this time, and all non-chunk files, were deleted)
        /// </summary>
        public readonly DateTime Old;

        /// <inheritdoc />
        public override string ToString() => String.Concat(Folder, " [", PruneDiscSize, ']');

        /// <summary>
        /// Create prune statistics
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="old">The UTC time used to classify chunks as old</param>
        /// <param name="beforeDiscSize">Estimated size on disc before pruning</param>
        /// <param name="beforeFileCount">Number of files before pruning</param>
        /// <param name="pruneDiscSize">Estimated size on disc of the deleted files</param>
        /// <param name="pruneFileCount">Number of deleted files</param>
        /// <param name="deleteErrors">Number of files that failed to delete</param>
        /// <param name="deleteErr">The first delete error message, if any</param>
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


        /// <summary>
        /// Sum this and another set of statistics
        /// </summary>
        /// <param name="other">The statistics to add</param>
        /// <returns>New statistics named "SUMMARY", <see cref="Old"/> is kept if equal in both, else <see cref="DateTime.MinValue"/></returns>
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
