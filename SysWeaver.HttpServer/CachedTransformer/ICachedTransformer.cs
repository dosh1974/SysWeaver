using System;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{


    public interface ICachedTransformer
    {
        /// <summary>
        /// Information string
        /// </summary>
        String Info { get; }

        /// <summary>
        /// Build strategy
        /// </summary>
        CachedTransformerBuildStrategies BuildStrategy { get; }

        /// <summary>
        /// Check if all resources are available
        /// </summary>
        /// <param name="service"></param>
        /// <param name="info">Information about the file</param>
        /// <returns></returns>
        CachedTransformerEntry Validate(CachedTransformer service, CachedTransformerFile info);


        /// <summary>
        /// Create resources
        /// </summary>
        /// <param name="service"></param>
        /// <param name="info">Information about the web request</param>
        /// <param name="data">Data in the request</param>
        /// <param name="entry">Existing data in the cache</param>
        /// <returns></returns>
        ValueTask<FileHttpRequestHandler[]> Build(CachedTransformer service, CachedTransformerFile info, ReadOnlyMemory<byte> data, CachedTransformerEntry entry);

    }


}
