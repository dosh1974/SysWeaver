using System;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    public class CachedTransformerFile
    {
        internal readonly String CacheKey;
        internal readonly ICachedTransformer Handler;
        /// <summary>
        /// Mime of input
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// Filename base (no extension)
        /// </summary>
        public readonly String BaseName;
        /// <summary>
        /// Extension without leading dot of input
        /// </summary>
        public readonly String Ext;
        /// <summary>
        /// True if build strategy isn't AlwaysDirect
        /// </summary>
        public readonly bool IsSupported;
        /// <summary>
        /// The decoder to use to get the raw data
        /// </summary>
        public readonly ICompDecoder Decoder;

        public readonly HttpRequestTransformerState State;

        internal CachedTransformerFile(ICachedTransformer handler, String cacheKey, string baseName, HttpRequestTransformerState state)
        {
            State = state;
            CacheKey = cacheKey;
            Handler = handler;
            Mime = state.Mime;
            BaseName = baseName;
            Ext = state.Ext;
            Decoder = state.Handler.Decoder;
            IsSupported = handler.BuildStrategy != CachedTransformerBuildStrategies.AlwaysDirect;
        }
    }

    sealed class CachedTransformerJob
    {
        public readonly CachedTransformerFile File;

        public readonly CachedTransformerEntry Entry;
        /// <summary>
        /// Data of input
        /// </summary>
        public readonly ReadOnlyMemory<Byte> Data;
        internal CachedTransformerJob(CachedTransformerFile file, ReadOnlyMemory<byte> data, CachedTransformerEntry entry)
        {
            File = file;
            Entry = entry;
            Data = data;
        }
    }

}
