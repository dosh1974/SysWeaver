namespace SysWeaver.Compression
{
    /// <summary>
    /// The compression effort, each implementation maps it to a format specific quality setting
    /// (ex: brotli quality 1 / 4 / 11, deflate / gzip <see cref="System.IO.Compression.CompressionLevel.Fastest"/> / <see cref="System.IO.Compression.CompressionLevel.Optimal"/> / <see cref="System.IO.Compression.CompressionLevel.SmallestSize"/>, zstd level 1 / 9 / 22).
    /// </summary>
    /// <remarks>
    /// The values are used as array indices by the implementations, only the defined values are valid.
    /// </remarks>
    public enum CompEncoderLevels
    {
        /// <summary>
        /// Fastest compression, use for real-time compression of API responses etc
        /// </summary>
        Fast = 0,
        /// <summary>
        /// A balance between speed and size, use for offline preview or for data that is cached for a longer time etc
        /// </summary>
        Balanced,
        /// <summary>
        /// Smallest output (can be very slow), use for offline builds and pre-compressed assets etc
        /// </summary>
        Best
    }

}
