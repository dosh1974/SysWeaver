using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace SysWeaver.Compression
{
    public static class CompHelpers
    {
        public const int TempSize = 16384;

        public static readonly IReadOnlyList<CompressionLevel> StreamLevels =
        [
            CompressionLevel.Fastest,
            CompressionLevel.Optimal,
            CompressionLevel.SmallestSize,
        ];


        public static readonly String EncDestTooSmall = "Couldn't fit the compressed data into the destination!";

        public static readonly String DecDestTooSmall = "Couldn't fit the decompressed data into the destination!";

    }
}
