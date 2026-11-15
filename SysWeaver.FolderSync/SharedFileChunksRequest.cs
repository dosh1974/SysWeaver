using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// A request for the chunk hashes of a file in a shared folder
    /// </summary>
    public sealed class SharedFileChunksRequest
    {
        /// <summary>
        /// The folder
        /// </summary>
        public String Folder;

        /// <summary>
        /// The file
        /// </summary>
        public String File;

        /// <summary>
        /// The version of the shared folder
        /// </summary>
        public String Version;

    }

}
