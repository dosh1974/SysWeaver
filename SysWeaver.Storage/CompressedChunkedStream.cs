using CommunityToolkit.HighPerformance;
using System;
using System.IO;
using SysWeaver.Compression;

namespace SysWeaver
{
    /// <summary>
    /// Create an uncompressed stream as the contatenation of several compressed streams
    /// </summary>
    public sealed class CompressedChunkedStream : ChunkedStream
    {
        /// <summary>
        /// Create an uncompressed stream as the contatenation of several compressed streams
        /// </summary>
        /// <param name="streamOpener">A function that opens one stream chunk, the paramter start at 0 and is incremented every time a new chunk is required, return null to signal end of data</param>
        /// <param name="comp">The compression type of the streams</param>
        public CompressedChunkedStream(Func<int, Stream> streamOpener, ICompDecoder comp) 
            : 
            base(index =>
            {
                using var so = streamOpener(index);
                if (so == null)
                    return null;
                var mem = comp.GetDecompressed(so);
                return mem.AsStream();
            })
        {
        }
    }




}
