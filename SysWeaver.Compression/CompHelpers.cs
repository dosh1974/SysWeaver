using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace SysWeaver.Compression
{
    /// <summary>
    /// Shared constants and helpers for compression implementations.
    /// </summary>
    public static class CompHelpers
    {
        /// <summary>
        /// A suggested size of temporary buffers (16 KB), currently not used by the built-in implementations.
        /// </summary>
        public const int TempSize = 16384;

        /// <summary>
        /// The .NET <see cref="CompressionLevel"/> to use for each <see cref="CompEncoderLevels"/> (indexed by the level), used by the stream based (Deflate / GZip / Brotli stream) implementations.
        /// </summary>
        public static readonly IReadOnlyList<CompressionLevel> StreamLevels =
        [
            CompressionLevel.Fastest,
            CompressionLevel.Optimal,
            CompressionLevel.SmallestSize,
        ];

        static ReadOnlySpan<CompressionLevel> StreamLevelValues =>
        [
            CompressionLevel.Fastest,
            CompressionLevel.Optimal,
            CompressionLevel.SmallestSize,
        ];

        /// <summary>
        /// Get the .NET compression level to use for a compression level (same as <see cref="StreamLevels"/>, without the interface call)
        /// </summary>
        /// <param name="level">The compression level</param>
        /// <returns>The .NET compression level</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompressionLevel GetStreamLevel(CompEncoderLevels level) => StreamLevelValues[(int)level];


        /// <summary>
        /// Exception message used (with an <see cref="ArgumentException"/>) when the compressed data doesn't fit in the destination memory.
        /// </summary>
        public static readonly String EncDestTooSmall = "Couldn't fit the compressed data into the destination!";

        /// <summary>
        /// Exception message used (with an <see cref="ArgumentException"/>) when the decompressed data doesn't fit in the destination memory.
        /// </summary>
        public static readonly String DecDestTooSmall = "Couldn't fit the decompressed data into the destination!";

        /// <summary>
        /// Exception message used (with an <see cref="InvalidOperationException"/>) when an encoder fails.
        /// </summary>
        public static readonly String EncFailed = "Failed to compress!";

        /// <summary>
        /// Exception message used (with a <see cref="System.IO.InvalidDataException"/>) when the compressed data is invalid.
        /// </summary>
        public static readonly String DecInvalid = "Failed to decompress, the data is invalid!";

        /// <summary>
        /// Exception message used (with a <see cref="System.IO.InvalidDataException"/>) when the compressed data ends before the end of the compressed stream.
        /// </summary>
        public static readonly String DecTruncated = "Failed to decompress, the data is truncated!";

        /// <summary>
        /// Check if the data starts with a gzip member (concatenated gzip members are valid gzip data)
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>True if the data starts with the gzip magic number</returns>
        public static bool IsGZipMember(ReadOnlySpan<Byte> data) => (data.Length >= 2) && (data[0] == 0x1f) && (data[1] == 0x8b);

        /// <summary>
        /// Check if the data starts with a zstd frame or a skippable frame (concatenated frames are valid zstd data)
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>True if the data starts with the magic number of a zstd frame or a skippable frame</returns>
        public static bool IsZstdFrame(ReadOnlySpan<Byte> data)
        {
            if (data.Length < 4)
                return false;
            if ((data[0] == 0x28) && (data[1] == 0xb5) && (data[2] == 0x2f) && (data[3] == 0xfd))
                return true;
            return ((data[0] & 0xf0) == 0x50) && (data[1] == 0x2a) && (data[2] == 0x4d) && (data[3] == 0x18);
        }

    }
}
