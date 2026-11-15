using System;

namespace SysWeaver.FolderSync
{
    /// <summary>
    /// Information about a file of a folder that is synchronized
    /// </summary>
    public sealed class FolderSyncFile
    {
        /// <inheritdoc />
        public override string ToString() => Name;

        /// <summary>
        /// Name of the file (locally within the source folder)
        /// </summary>
        public String Name;

        /// <summary>
        /// The MD5 checksum of the file content, encoded using hex values, "abcd...", where a = is the high nibble of the first byte, b is the low nibble of the first byte and so on...
        /// </summary>
        public String Hash;

        /// <summary>
        /// When the file was last modified
        /// </summary>
        public DateTime LastModified;
    }
}
