using System;

namespace SysWeaver.MicroService
{

    public sealed partial class MediaTransformerService
    {
        sealed class BuildJob
        {
            public readonly IHandler Handler;
            public readonly CacheEntry Entry;
            public readonly ReadOnlyMemory<Byte> Data;
            public readonly String Mime;
            public readonly String BaseName;

            public BuildJob(IHandler handler, CacheEntry entry, ReadOnlyMemory<byte> data, string mime, string baseName)
            {
                Handler = handler;
                Entry = entry;
                Data = data;
                Mime = mime;
                BaseName = baseName;
            }
        }



    }

}
