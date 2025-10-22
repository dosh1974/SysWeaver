using System;

namespace SysWeaver.MicroService
{
    public sealed class UserStorageUpload
    {
        /// <summary>
        /// Set to a string with any required auth tokens, null will still require a user to be logged in (obviously).
        /// </summary>
        public String Auth;

        /// <summary>
        /// Maximum file size for uploads in bytes (0 or less = Use the maximum in the retention plan)
        /// </summary>
        public long MaxFileSize;

        /// <summary>
        /// Extensions that is allowed seprated by a comma.
        /// There is no point having a whitelist and blacklist.
        /// </summary>
        public String Whitelist;

        /// <summary>
        /// Extensions that isn't allowed seprated by a comma.
        /// There is no point having a whitelist and blacklist.
        /// </summary>
        public String Blacklist;

        /// <summary>
        /// Allow multiple files to be uploaded in paralell
        /// </summary>
        public bool AllowMultiple = true;

        /// <summary>
        /// If true, keep files that are sent compressed, compressed on disc
        /// </summary>
        public bool AllowPreCompressed = true;

    }

}
