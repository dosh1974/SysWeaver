using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    /// <summary>
    /// The (possibly still building) set of transformed variants of a source file.
    /// </summary>
    public sealed class CachedTransformerEntry
    {
        /// <summary>
        /// True when the build is done (successfully or not).
        /// </summary>
        public volatile bool Completed;

        /// <summary>
        /// The size in bytes of the original data.
        /// </summary>
        public long OrgSize;

        /// <summary>
        /// Variants in order of preference (typically smallest first, see <see cref="CachedTransformer.GetValidSorted"/>).
        /// A null element represents the original, variants after it are never used.
        /// Null while building or if the build failed, in which case the original is served.
        /// </summary>
        public FileHttpRequestHandler[] Files;

    }


}
