using System;

using SysWeaver.Compression;

namespace SysWeaver
{
    public sealed class KeyValueStoreParams
    {
        /// <summary>
        /// Id / name of the store, must be unique within an application
        /// </summary>
        public String Id = "Default";

        /// <summary>
        /// The serializer to use
        /// </summary>
        public String Ser = "json";

        /// <summary>
        /// The compression method to use (can be null to disable compression, can be useful for small data or incompressible data)
        /// </summary>
        public String Comp = "br";

        /// <summary>
        /// The compression level to use
        /// </summary>
        public CompEncoderLevels Level = CompEncoderLevels.Balanced;

        /// <summary>
        /// The minimum number of file copies of each data.
        /// Minimum 3 is required.
        /// </summary>
        public int Redundance = 3;
        
        /// <summary>
        /// The location(s) of the files.
        /// Can be spread over multiple volumes to increase reliability, separtate using ;
        /// </summary>
        public String Folders;

        /// <summary>
        /// If folders isn't use, indicate if this store should be per user or not.
        /// </summary>
        public bool PerUser;

        /// <summary>
        /// If folders isn't use, indicate if this store should be per app or not.
        /// </summary>
        public bool PerApp = true;

    }

}
