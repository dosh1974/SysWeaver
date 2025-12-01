using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    public sealed class SmKeyFile
    {
        /// <summary>
        /// Name of the key file
        /// </summary>
        public String Name;

        /// <summary>
        /// Size in bytes of the key file
        /// </summary>
        [TableDataByteSize]
        public long Size;

        /// <summary>
        /// When the file was last modified
        /// </summary>
        public DateTime LastModified;

        /// <summary>
        /// True if this is a backup
        /// </summary>
        public bool Backup;
    }

}
