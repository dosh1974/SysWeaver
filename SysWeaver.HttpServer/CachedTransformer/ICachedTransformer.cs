using System;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{


    public interface ICachedTransformer
    {

        CachedTransformerBuildStrategies BuildStrategy { get; }


        /// <summary>
        /// Check if all resources are available
        /// </summary>
        /// <param name="service"></param>
        /// <param name="baseName">Filename base (no extension)</param>
        /// <param name="isSupported">True if build strategy isn't AlwaysDirect</param>
        /// <returns></returns>
        CachedTransformerEntry Validate(CachedTransformer service, String baseName, bool isSupported);


        /// <summary>
        /// Create resources
        /// </summary>
        /// <param name="service"></param>
        /// <param name="baseName">Filename base (no extension)</param>
        /// <param name="inputMime">Mime of input</param>
        /// <param name="inputData">Data of input</param>
        /// <param name="inputExt">Extension without leading dot of input</param>
        /// <param name="isSupported">True if build strategy isn't AlwaysDirect</param>
        /// <param name="decoder">The decoder to use to get the raw data</param>
        /// <returns></returns>
        ValueTask<FileHttpRequestHandler[]> Build(CachedTransformer service, string baseName, string inputMime, ReadOnlyMemory<byte> inputData, String inputExt, bool isSupported, ICompDecoder decoder);

    }


}
