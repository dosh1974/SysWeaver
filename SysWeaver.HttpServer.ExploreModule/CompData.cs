using SysWeaver.Compression;
using SysWeaver.Data;
using System;

namespace SysWeaver.Net.ExploreModule
{

    [TableDataPrimaryKey(nameof(Name))]
    sealed class CompData
    {
        public CompData(ICompInfo i)
        {
            Name = i.Name;
            HttpCode = i.HttpCode;
            Prio = i.Prio;
            FileExtensions = String.Join("; ", i.FileExtensions);
        }

        /// <summary>
        /// The name of the compression implementation
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The http code as specified for the Accept-Encoding header (or custom), ex: "deflate", "gzip" or "br", "compress", must be all lowercase
        /// </summary>
        [TableDataGoogleSearch(null, "Details about the compression type \"{0}\"")]
        public readonly String HttpCode;

        /// <summary>
        /// The priority (quality) of the compressor, if multiple compressors are available the one with the highest priority is returned by the compression manager
        /// </summary>
        public readonly int Prio;

        /// <summary>
        /// File extensions that is associated with the compressor
        /// </summary>
        [TableDataGoogleSearch(null, "Information about files with extensions {0}")]
        public readonly String FileExtensions;
    }

}
