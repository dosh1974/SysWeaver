using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="SimpleAuditService"/>
    /// </summary>
    public sealed class SimpleAuditParams
    {
        public override string ToString() => String.Concat(
            nameof(RetentionDays), ": ", RetentionDays, ", ",
            nameof(Folders), ": ", Folders == null ? "null" : String.Join(", ", Folders));

        /// <summary>
        /// The folders to store the audit data in, the files are distributed over all folders.
        /// Can use <see cref="PathTemplate"/> variables.
        /// Null or empty to use the folder "Audit" in all <see cref="SysWeaver.Folders.AllAppFolders"/>.
        /// </summary>
        public String[] Folders;

        /// <summary>
        /// Number of days to keep audit data, zero or less to keep it forever.
        /// </summary>
        public int RetentionDays = 90;

        /// <summary>
        /// Maximum number of chars to store of the input and the output (or exception message) of a call.
        /// </summary>
        public int MaxDataLength = 2048;

        /// <summary>
        /// Start a new file when the current file is larger than this (a new file is also started every day), number of kB.
        /// Smaller files gives a better distribution over the folders.
        /// </summary>
        public int MaxFileSize = 1024;

        /// <summary>
        /// Maximum number of MB of files that are kept parsed in memory (to speed up searching).
        /// </summary>
        public int CacheSize = 32;

        /// <summary>
        /// Number of milliseconds between writes
        /// </summary>
        public int WriteInterval = 100;
    }
}
