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
        /// If positive, request handler lookups (url to file) are cached for this number of seconds (0 or negative disables the cache).
        /// Note: the cache maps urls to files, file contents are not cached here, a file that is added / removed on disc may take this long to be noticed.
        /// </summary>
        public int CacheSeconds = 5;

    }


}
