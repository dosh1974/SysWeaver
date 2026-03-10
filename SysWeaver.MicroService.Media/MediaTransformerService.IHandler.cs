using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public sealed partial class MediaTransformerService
    {
        interface IHandler
        {
            CacheEntry Validate(MediaTransformerService service, String baseName);

            ValueTask<FileHttpRequestHandler[]> Build(MediaTransformerService service, string baseName, string inputMime, ReadOnlyMemory<byte> inputData);

        }



    }

}
