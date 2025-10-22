using System;
using System.Collections.Generic;

namespace SysWeaver.Compression
{
    public interface ICompInfo
    {
        /// <summary>
        /// The name of the compression implementation
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The http code as specified for the Accept-Encoding header (or custom), ex: "deflate", "gzip" or "br", "compress", must be all lowercase
        /// </summary>
        String HttpCode { get; }

        /// <summary>
        /// The priority (quality) of the compressor, if multiple compressors are available the one with the highest priority is returned by the compression manager
        /// </summary>
        int Prio { get; }

        /// <summary>
        /// File extensions that is associated with the compressor, must be all lowercase
        /// </summary>
        IReadOnlyCollection<String> FileExtensions { get; }

    }

}
