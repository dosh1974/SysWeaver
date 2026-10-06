using System;

namespace SysWeaver.HttpTransformer
{
    /// <summary>
    /// Base parameters for <see cref="CachedTransformer"/> based transformer services.
    /// </summary>
    public class CachedTransformerParams
    {

#if DEBUG
        /// <summary>
        /// Maximum number of concurrent background builds.
        /// Zero or negative is relative to the processor count (processor count + value).
        /// The result is always clamped to [1, processor count / 2].
        /// </summary>
        public int BuildThreads = 1;
#else//DEBUG
        /// <summary>
        /// Maximum number of concurrent background builds.
        /// Zero or negative is relative to the processor count (processor count + value).
        /// The result is always clamped to [1, processor count / 2].
        /// </summary>
        public int BuildThreads = 4;
#endif//DEBUG

        /// <summary>
        /// Optionally specify the folders where transformed data is stored (files are distributed among them by hash).
        /// Null to use a "TransformerCache" sub folder in all application data folders. Must not be empty.
        /// </summary>
        public String[] Folders;

        /// <summary>
        /// Number of days after last access (file system last access time) to remove a cache entry from disc, clamped to [1, 36500].
        /// </summary>
        public int RemoveAfterDays = 30;
    }

}
