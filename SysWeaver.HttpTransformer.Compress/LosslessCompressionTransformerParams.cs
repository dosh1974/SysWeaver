using System;

namespace SysWeaver.HttpTransformer
{
    /// <summary>
    /// Parameters for <see cref="LosslessCompressionTransformer"/>.
    /// </summary>
    public sealed class LosslessCompressionTransformerParams : CachedTransformerParams
    {
        /// <summary>
        /// The compression methods to build variants for, in order of preference (http names like "br" or file extensions).
        /// </summary>
        public String[] Methods =
            [
                "br",
                "zstd",
                "deflate",
            ];


        /// <summary>
        /// If true, missing variants are built while the request waits, else they are built in the background (the original is served meanwhile).
        /// </summary>
        public bool BuildDirect;

        /// <summary>
        /// If true, an exception is thrown if a method in <see cref="Methods"/> can't be found, else the method is ignored.
        /// </summary>
        public bool ThrowOnMissing;
    }

}
