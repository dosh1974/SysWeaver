using System;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public sealed partial class MediaTransformerService
    {
        sealed class BuildJob
        {
            public readonly String CacheKey;
            public readonly IMediaTransformHandler Handler;
            public readonly MediaTransformCacheEntry Entry;
            public readonly ReadOnlyMemory<Byte> Data;
            public readonly String Mime;
            public readonly String BaseName;
            public readonly String Ext;
            public readonly bool IsSupported;

            public BuildJob(IMediaTransformHandler handler, String cacheKey, MediaTransformCacheEntry entry, ReadOnlyMemory<byte> data, string baseName, HttpRequestTransformerState state)
            {
                CacheKey = cacheKey;
                Handler = handler;
                Entry = entry;
                Data = data;
                Mime = state.Mime;
                BaseName = baseName;
                Ext = state.Ext;
                IsSupported = handler.BuildStrategy != MediaTransformerBuilds.AlwaysDirect;
            }
        }



    }

}
