using System;

namespace SysWeaver.Net
{
    public sealed class FileHttpServerModuleFolder
    {
        public override string ToString() => String.Concat(
            nameof(DiscFolder), ": ", DiscFolder.ToQuoted(), ", ",
            nameof(WebFolder), ": ", WebFolder.ToQuoted(), ", ",
            nameof(Auth), ": ", Auth.ToQuoted(), ", ",
            nameof(AssumePreCompressed), ": ", AssumePreCompressed);

        /// <summary>
        /// The folder on disc
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// The web folder to serve this folder at
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
        /// The preferred on the fly compression schemes
        /// </summary>
        public String Compression = "br: Balanced, deflate: Balanced, gzip: Balanced";

        /// <summary>
        /// If true, compressed files that have a compressed version may be served, i.e "Test.txt.gzip" may be served in place of "Test.txt" if Test.txt is older or non existent.
        /// </summary>
        public bool AssumePreCompressed = true;

        /// <summary>
        /// The required auth for these files (null = no auth required, "" = no special auth token is required, but user must be authenticated)
        /// </summary>
        public String Auth;

        /// <summary>
        /// If true, the file's access time is updated whenever the file is read
        /// </summary>
        public bool UpdateAccessTime;


    }


}
