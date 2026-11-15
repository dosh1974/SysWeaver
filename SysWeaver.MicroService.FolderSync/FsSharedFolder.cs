using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A folder that is shared, so that clients can sync it down (pull)
    /// </summary>
    public sealed class FsSharedFolder
    {
        /// <summary>
        /// Name of repository, used when synching.
        /// If null or empty, the name of the disc folder is used.
        /// </summary>
        public String Name;

        /// <summary>
        /// The folder to manage on disc
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// The auth required to sync this folder.
        /// null is equal to Roles.Debug
        /// </summary>
        public String Auth;
    }


}
