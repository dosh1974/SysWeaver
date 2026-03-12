using System;

namespace SysWeaver.HttpTransformer
{
    public class CachedTransformerParams
    {

#if DEBUG
        /// <summary>
        /// Maximum number of threads to use.
        /// Zero or negative to use relative to processior count
        /// </summary>
        public int BuildThreads = 1;
#else//DEBUG
        /// <summary>
        /// Maximum number of threads to use.
        /// Zero or negative to use relative to processior count
        /// </summary>
        public int BuildThreads = 4;
#endif//DEBUG

        /// <summary>
        /// Optionally specify where to store transformed data
        /// </summary>
        public String[] Folders;

        /// <summary>
        /// Number of days after last usage to remove a cache entry from disc
        /// </summary>
        public int RemoveAfterDays = 30;
    }

}
