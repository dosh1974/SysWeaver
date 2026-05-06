using System;

namespace SysWeaver.MicroService
{
    public sealed class FsSharedFolder
    {
        /// <summary>
        /// Name of repository, used when synching
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
