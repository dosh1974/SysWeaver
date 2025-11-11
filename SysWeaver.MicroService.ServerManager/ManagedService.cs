using System;

namespace SysWeaver.MicroService
{
    public sealed class ManagedService
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
        /// If true, a "master" configs are stored and copied into new folders
        /// </summary>
        public bool MasterConfig = true;

        /// <summary>
        /// The auth required to sync this folder
        /// null is equal to Roles.Debug
        /// </summary>
        public String SyncAuth;



    }
}
