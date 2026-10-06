using SysWeaver.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SysWeaver.Net
{


    /// <summary>
    /// Common caching, compression, localization and auth options for request handlers (used by file handlers, generated data handlers etc).
    /// </summary>
    public class RequestOptions
    {

        /// <summary>
        /// Create request options.
        /// </summary>
        /// <param name="clientCacheDuration">Number of seconds to tell the client to cache the resource, negative values are clamped to 0</param>
        /// <param name="requestCacheDuration">Number of seconds to store the response in the server side request cache, negative values are clamped to 0</param>
        /// <param name="maxCacheSize">Maximum size (in bytes) of a response that can be stored in the cache</param>
        /// <param name="compression">Optional supported compression methods and levels, ex: "br:Fast, deflate:Balanced", null or empty for no compression</param>
        /// <param name="auth">An optional list of comma separated tokens (see <see cref="Authorization.GetRequiredTokens(String)"/>), null means no auth required</param>
        /// <param name="isLocalized">If this file will look different depending on the language, set this to true (the language becomes part of the cache key)</param>
        /// <param name="forceCache">If this is true, caching of otherwise uncompressable files will be enabled</param>
        public RequestOptions(int clientCacheDuration, int requestCacheDuration, long maxCacheSize, String compression, String auth, bool isLocalized = false, bool forceCache = false)
        {
            ClientCacheDuration = clientCacheDuration > 0 ? clientCacheDuration : 0;
            RequestCacheDuration = requestCacheDuration > 0 ? requestCacheDuration : 0;
            MaxCacheSize = maxCacheSize;
            Auth = auth == null ? null : Authorization.GetRequiredTokens(auth);
            Compression = HttpCompressionPriority.GetSupportedEncoders(compression);
            IsLocalized = isLocalized;
            ForceCache = forceCache;
        }
        /// <summary>
        /// Number of seconds the client should cache the resource (0 = no client caching).
        /// </summary>
        public readonly int ClientCacheDuration;
        /// <summary>
        /// Number of seconds the response is kept in the server side request cache (0 = not cached).
        /// </summary>
        public readonly int RequestCacheDuration;
        /// <summary>
        /// Maximum size (in bytes) of a response that can be stored in the cache.
        /// </summary>
        public readonly long MaxCacheSize;
        /// <summary>
        /// The supported compression methods (in order of preference), null if responses shouldn't be compressed.
        /// </summary>
        public readonly HttpCompressionPriority Compression;
        /// <summary>
        /// The (lower cased) tokens required to access the resource (any of them), null if no auth is required, empty if any logged in user is allowed.
        /// </summary>
        public readonly IReadOnlyList<String> Auth;
        /// <summary>
        /// True if the response depends on the session language.
        /// </summary>
        public readonly bool IsLocalized;
        /// <summary>
        /// True if caching should be enabled even for files that can't be compressed.
        /// </summary>
        public readonly bool ForceCache;
    }

    /// <summary>
    /// A folder on disc that is exposed through a web folder by the <see cref="FileHttpServerModule"/>, with its request options.
    /// </summary>
    public sealed class DiscFolder : RequestOptions
    {
        /// <inheritdoc/>
        public override string ToString() => Path;

        /// <summary>
        /// The absolute path of the folder on disc (without a trailing separator).
        /// </summary>
        public volatile String Path;
        /// <summary>
        /// If true, pre-compressed variants of the files (ex: "file.js.br") are looked for and used when smaller.
        /// </summary>
        public readonly bool AssumePreCompressed;
        /// <summary>
        /// If true, the file's last access time is updated whenever the file is read.
        /// </summary>
        public readonly bool UpdateAccessTime;
        /// <summary>
        /// If true, the files in this folder are treated as dynamic (may change at any time).
        /// </summary>
        public readonly bool IsDynamic;

        /// <summary>
        /// Create a disc folder.
        /// </summary>
        /// <param name="path">The absolute path of the folder on disc</param>
        /// <param name="folder">The folder configuration to copy the options from (localization and force cache are not copied, they are always false)</param>
        public DiscFolder(string path, FileHttpServerModuleFolder folder)
            : base(folder.ClientCacheDuration, folder.RequestCacheDuration, folder.MaxCacheSize, folder.Compression, folder.Auth)
        {
            Path = path;
            AssumePreCompressed = folder.AssumePreCompressed;
            UpdateAccessTime = folder.UpdateAccessTime;
            IsDynamic = folder.IsDynamic;
        }

    }


}
