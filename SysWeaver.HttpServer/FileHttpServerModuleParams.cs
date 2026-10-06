using System;
using System.Linq;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters for <see cref="FileHttpServerModule"/>.
    /// </summary>
    public class FileHttpServerModuleParams
    {
        public override string ToString() => String.Concat(
            nameof(Folders), ":\n    ", Folders == null ? "null" : String.Join("\n    ", Folders.Select(x => x == null ? "null" : String.Join(x.ToString(), '{', '}'))));

        /// <summary>
        /// The folders to serve files from (folders whose disc folder doesn't exist are ignored).
        /// </summary>
        public FileHttpServerModuleFolder[] Folders;

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// If positive, request handler lookups (url to file) are cached.
        /// Note: the value only enables or disables the cache, the cache duration is currently fixed at 5 seconds.
        /// </summary>
        public int CacheSeconds = 5;

    }


}
