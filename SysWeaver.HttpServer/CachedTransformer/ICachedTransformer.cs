using System;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{


    /// <summary>
    /// A transformer that builds (and validates) disc cached variants of a file type, registered with <see cref="CachedTransformer"/>.
    /// </summary>
    public interface ICachedTransformer
    {
        /// <summary>
        /// Information string, displayed in the debug table.
        /// </summary>
        String Info { get; }

        /// <summary>
        /// Determines if missing variants are built in the background or while the request waits.
        /// </summary>
        CachedTransformerBuildStrategies BuildStrategy { get; }

        /// <summary>
        /// Check if all variants already exist on disc (named from <see cref="CachedTransformerFile.BaseName"/>).
        /// </summary>
        /// <param name="service">The owning transformer service.</param>
        /// <param name="info">Information about the file.</param>
        /// <returns>A completed entry if everything exists, else null (a build will be started).</returns>
        CachedTransformerEntry Validate(CachedTransformer service, CachedTransformerFile info);


        /// <summary>
        /// Build all variants and write them to disc.
        /// Calls are limited to <see cref="CachedTransformerParams.BuildThreads"/> concurrent builds.
        /// </summary>
        /// <param name="service">The owning transformer service.</param>
        /// <param name="info">Information about the web request.</param>
        /// <param name="data">The original data (possibly compressed, see <see cref="CachedTransformerFile.Decoder"/>).</param>
        /// <param name="entry">The entry being built (its <see cref="CachedTransformerEntry.OrgSize"/> is already set).</param>
        /// <returns>The variants in order of preference (see <see cref="CachedTransformerEntry.Files"/>), or null to leave the entry's files unchanged.
        /// Exceptions are caught and tracked by the service, the entry is then completed without files.</returns>
        Task<FileHttpRequestHandler[]> Build(CachedTransformer service, CachedTransformerFile info, ReadOnlyMemory<byte> data, CachedTransformerEntry entry);

    }


}
