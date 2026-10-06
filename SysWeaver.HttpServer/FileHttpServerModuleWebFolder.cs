using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// The web side settings of a folder served by <see cref="FileHttpServerModule"/> (see <see cref="FileHttpServerModuleFolder"/>).
    /// </summary>
    public class FileHttpServerModuleWebFolder
    {
        public override string ToString() => String.Concat(
            nameof(WebFolder), ": ", WebFolder.ToQuoted(), ", ",
            nameof(Auth), ": ", Auth.ToQuoted(), ", ",
            nameof(AssumePreCompressed), ": ", AssumePreCompressed);


        /// <summary>
        /// The web folder to serve this folder at, relative to the server root (ex: "site" or "site/images"), null or empty for the root.
        /// Leading and trailing '/' are ignored.
        /// </summary>
        public String WebFolder;

        /// <summary>
        /// Number of seconds to cache the file on a client
        /// </summary>
        public int ClientCacheDuration = 5;

        /// <summary>
        /// Number of seconds to cache any intermediate results (i.e small files that are compressed on the fly)
        /// </summary>
        public int RequestCacheDuration = 30;

        /// <summary>
        /// The maximum size of a file that can be cached
        /// </summary>
        public long MaxCacheSize = 32768;

        /// <summary>
        /// The preferred on the fly compression schemes (in order of preference), only applied to compressible mime types.
        /// </summary>
        public String Compression = "br: Balanced, deflate: Balanced, gzip: Balanced";

        /// <summary>
        /// If true, pre-compressed variants of files may be served, i.e "Test.txt.br" may be served in place of "Test.txt" if it's smaller and not older than "Test.txt", or if "Test.txt" doesn't exist.
        /// The compressed file extensions are the ones registered in the compression manager.
        /// </summary>
        public bool AssumePreCompressed = true;

        /// <summary>
        /// The required auth for these files (null = no auth required, "" = no special auth token is required, but user must be authenticated, else a comma separated list of required tokens)
        /// </summary>
        public String Auth;

        /// <summary>
        /// If true, the file's access time is updated whenever the file is read
        /// </summary>
        public bool UpdateAccessTime;

        /// <summary>
        /// If true, files in this folder are marked as dynamic hence bypass any transformer chains
        /// </summary>
        public bool IsDynamic;

        /// <summary>
        /// Copy all settings to another instance.
        /// </summary>
        /// <param name="t">The instance to copy to</param>
        public void CopyTo(FileHttpServerModuleWebFolder t)
        {
            t.WebFolder = WebFolder;
            t.ClientCacheDuration = ClientCacheDuration;
            t.RequestCacheDuration = RequestCacheDuration;
            t.MaxCacheSize = MaxCacheSize;
            t.Compression = Compression;
            t.AssumePreCompressed = AssumePreCompressed;
            t.Auth = Auth;
            t.UpdateAccessTime = UpdateAccessTime;
            t.IsDynamic = IsDynamic;
       }
    }


}
