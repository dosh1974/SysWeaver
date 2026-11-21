using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Settings for uploading files of one scope (public, protected or private) to the user storage
    /// </summary>
    public sealed class UserStorageUpload
    {
        /// <summary>
        /// Set to a string with any required auth tokens (comma separated), null, "" or "-" only require a user to be logged in.
        /// </summary>
        public String Auth;

        /// <summary>
        /// Maximum file size for uploads in bytes (0 or less = Use the maximum in the retention plan)
        /// </summary>
        public long MaxFileSize;

        /// <summary>
        /// Extensions that are allowed, separated by a comma, case insensitive, with or without a leading '.' (null = no whitelist).
        /// There is no point having a whitelist and blacklist: if a whitelist is set, the blacklist is ignored.
        /// </summary>
        public String Whitelist;

        /// <summary>
        /// Extensions that aren't allowed, separated by a comma, case insensitive, with or without a leading '.' (null = no blacklist).
        /// Ignored if a <see cref="Whitelist"/> is set.
        /// </summary>
        public String Blacklist;

        /// <summary>
        /// Allow multiple files to be uploaded in parallel (default is true)
        /// </summary>
        public bool AllowMultiple = true;

        /// <summary>
        /// If true (the default), keep files that are sent compressed, compressed on disc
        /// </summary>
        public bool AllowPreCompressed = true;

    }

}
