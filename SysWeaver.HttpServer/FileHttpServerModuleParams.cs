using System;
using System.Linq;

namespace SysWeaver.Net
{
    public class FileHttpServerModuleParams
    {
        public override string ToString() => String.Concat(
            nameof(Folders), ":\n    ", Folders == null ? "null" : String.Join("\n    ", Folders.Select(x => x == null ? "null" : String.Join(x.ToString(), '{', '}'))));

        /// <summary>
        /// The folders to server files from
        /// </summary>
        public FileHttpServerModuleFolder[] Folders;

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// Number of seconds to use cached results
        /// </summary>
        public int CacheSeconds = 5;

    }


}
