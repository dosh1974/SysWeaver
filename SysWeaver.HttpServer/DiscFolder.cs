using SysWeaver.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SysWeaver.Net
{


    public class RequestOptions
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientCacheDuration">Number of seconds to telll the client to cache the resource</param>
        /// <param name="requestCacheDuration">Numbner of seconds to store in the request cache</param>
        /// <param name="maxCacheSize">Maximum size that can be stored in the cache</param>
        /// <param name="compression">Optional supported compresson methods and level, ex: "br:Fast, deflate:Balanced"</param>
        /// <param name="auth">An optional list of comma separated tokens</param>
        /// <param name="isLocalized">If this file will look different depending on the language, set this to true</param>
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
        public readonly int ClientCacheDuration;
        public readonly int RequestCacheDuration;
        public readonly long MaxCacheSize;
        public readonly HttpCompressionPriority Compression;
        public readonly IReadOnlyList<String> Auth;
        public readonly bool IsLocalized;
        public readonly bool ForceCache;
    }

    public sealed class DiscFolder : RequestOptions
    {
        public override string ToString() => Path;

        public readonly String Path;
        public readonly bool AssumePreCompressed;
        /// <summary>
        /// If true, the file's access time is chnage whenever the file is read
        /// </summary>
        public readonly bool UpdateAccessTime;
        public DiscFolder(string path, FileHttpServerModuleFolder folder) 
            : base(folder.ClientCacheDuration, folder.RequestCacheDuration, folder.MaxCacheSize, folder.Compression, folder.Auth)
        {
            Path = path;
            AssumePreCompressed = folder.AssumePreCompressed;
            UpdateAccessTime = folder.UpdateAccessTime;
        }

    }


}
