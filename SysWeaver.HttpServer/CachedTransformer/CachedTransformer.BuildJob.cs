using System;
using SysWeaver.Compression;
using SysWeaver.Net;

namespace SysWeaver.HttpTransformer
{

    /// <summary>
    /// Describes a source file (request) that a <see cref="ICachedTransformer"/> should validate or build cached variants for.
    /// </summary>
    public class CachedTransformerFile
    {
        /// <summary>
        /// The cache key (local url + newline + etag of the source).
        /// </summary>
        internal readonly String CacheKey;

        /// <summary>
        /// The transformer that handles this file.
        /// </summary>
        internal readonly ICachedTransformer Handler;

        /// <summary>
        /// Mime of the input.
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// Full path and base file name (no extension) on disc where the transformed variants should be stored; append an extension to get a file name.
        /// The name is a hash of the cache key, so it changes when the source changes (new etag).
        /// </summary>
        public readonly String BaseName;
        /// <summary>
        /// File extension of the input, without leading dot.
        /// </summary>
        public readonly String Ext;
        /// <summary>
        /// True if the build strategy isn't <see cref="CachedTransformerBuildStrategies.AlwaysDirect"/>, i.e. the original data may
        /// also be served, so transformers should add a null entry (representing the original) and store the original size.
        /// </summary>
        public readonly bool IsSupported;
        /// <summary>
        /// The decoder needed to decompress the input data, null if the input isn't compressed.
        /// </summary>
        public readonly ICompDecoder Decoder;

        /// <summary>
        /// The transformer state of the request that triggered the build.
        /// </summary>
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

    /// <summary>
    /// A queued (or direct) build of a <see cref="CachedTransformerFile"/>.
    /// </summary>
    sealed class CachedTransformerJob
    {
        public readonly CachedTransformerFile File;

        public readonly CachedTransformerEntry Entry;
        /// <summary>
        /// Data of input (possibly compressed, see <see cref="CachedTransformerFile.Decoder"/>).
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
