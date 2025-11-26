using System;

namespace SysWeaver.MicroService
{
    public sealed class ManagedService
    {
        /// <summary>
        /// Name of repository, used when synching
        /// </summary>
        [EditMin(1)]
        public String Name;

        /// <summary>
        /// If true, a "master" configs are stored and copied into new folders
        /// </summary>
        public bool MasterConfig = true;

        /// <summary>
        /// The folder to manage on disc, leave null or blank to use the default location (recommended)
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        public String DiscFolder;


        /// <summary>
        /// The auth required to sync this folder.
        /// null is equal to using the service default (recommended).
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        public String SyncAuth;



    }
}
