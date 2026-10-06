using CommunityToolkit.HighPerformance;
using System;
using System.IO;
using SysWeaver.Compression;

namespace SysWeaver
{
    /// <summary>
    /// A read only stream of the uncompressed data of several compressed streams (chunks), concatenated.
    /// </summary>
    /// <remarks>
    /// Each chunk is opened when needed, fully decompressed into memory and then the chunk stream is disposed.
    /// Memory usage is therefore proportional to the uncompressed size of the largest chunk.
    /// </remarks>
    public sealed class CompressedChunkedStream : ChunkedStream
    {
        /// <summary>
        /// Create an uncompressed stream as the concatenation of several compressed streams.
        /// </summary>
        /// <param name="streamOpener">A function that opens one compressed stream chunk, the parameter starts at 0 and is incremented every time a new chunk is required, return null to signal end of data.
        /// The returned stream is disposed by this class.</param>
        /// <param name="comp">The decoder to use for all chunks.</param>
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
