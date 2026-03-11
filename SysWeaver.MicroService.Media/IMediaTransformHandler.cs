using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{


    interface IMediaTransformHandler
    {

        MediaTransformerBuilds BuildStrategy { get; }


        /// <summary>
        /// Check if all resources are available
        /// </summary>
        /// <param name="service"></param>
        /// <param name="baseName"></param>
        /// <returns></returns>
        MediaTransformCacheEntry Validate(MediaTransformerService service, String baseName);


        /// <summary>
        /// Create resources
        /// </summary>
        /// <param name="service"></param>
        /// <param name="baseName">Filename base (no extension)</param>
        /// <param name="inputMime">Mime of input</param>
        /// <param name="inputData">Data of input</param>
        /// <param name="inputExt">Extension without leading dot of input</param>
        /// <param name="isSupported">True if build strategy isn't AlwaysDirect</param>
        /// <returns></returns>
        ValueTask<FileHttpRequestHandler[]> Build(MediaTransformerService service, string baseName, string inputMime, ReadOnlyMemory<byte> inputData, String inputExt, bool isSupported);

    }


}
