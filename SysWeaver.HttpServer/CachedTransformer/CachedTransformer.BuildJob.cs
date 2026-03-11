using System;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    public sealed partial class CachedTransformer
    {
        sealed class BuildJob
        {
            public readonly String CacheKey;
            public readonly ICachedTransformer Handler;
            public readonly CachedTransformerEntry Entry;
            public readonly ReadOnlyMemory<Byte> Data;
            public readonly String Mime;
            public readonly String BaseName;
            public readonly String Ext;
            public readonly bool IsSupported;
            public readonly ICompDecoder Decoder;

            public BuildJob(ICachedTransformer handler, String cacheKey, CachedTransformerEntry entry, ReadOnlyMemory<byte> data, string baseName, HttpRequestTransformerState state)
            {
                CacheKey = cacheKey;
                Handler = handler;
                Entry = entry;
                Data = data;
                Mime = state.Mime;
                BaseName = baseName;
                Ext = state.Ext;
                Decoder = state.Handler.Decoder;
                IsSupported = handler.BuildStrategy != CachedTransformerBuildStrategies.AlwaysDirect;
            }
        }



    }

}
