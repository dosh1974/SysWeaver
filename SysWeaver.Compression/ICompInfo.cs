using System;
using System.Collections.Generic;

namespace SysWeaver.Compression
{
    /// <summary>
    /// Describes a compression implementation (name, HTTP content-coding, priority and file extensions).
    /// </summary>
    public interface ICompInfo
    {
        /// <summary>
        /// A human readable name of the compression implementation (used for diagnostics, for example ".NET brotli").
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The HTTP content-coding name as used in the Accept-Encoding / Content-Encoding headers (or a custom name), ex: "deflate", "gzip", "br" or "zstd".
        /// Must be all lowercase, lookups in <see cref="CompManager.GetFromHttp(string)"/> are ordinal (case sensitive).
        /// </summary>
        String HttpCode { get; }

        /// <summary>
        /// The priority of the implementation. If multiple implementations share an HTTP code or file extension, the <see cref="CompManager"/> returns the one with the highest priority
        /// (on a tie, the one registered last).
        /// </summary>
        int Prio { get; }

        /// <summary>
        /// The file extensions (without a "." prefix) associated with the format, ex: "br", "gz".
        /// Must be all lowercase, the <see cref="CompManager"/> registers each extension both with and without a "." prefix.
        /// </summary>
        IReadOnlyCollection<String> FileExtensions { get; }

    }

}
