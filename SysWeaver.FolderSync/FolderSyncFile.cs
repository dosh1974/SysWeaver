using System;

namespace SysWeaver.FolderSync
{
    public sealed class FolderSyncFile
    {
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
        /// Numer of milliseconds since the Unix epoch
        /// </summary>
        public DateTime LastModified;
    }
}
