using System;

using SysWeaver.Compression;

namespace SysWeaver
{
    /// <summary>
    /// Parameters for creating a custom <see cref="KeyValueStore"/> using <see cref="KeyValueStore.Get(KeyValueStoreParams)"/>.
    /// </summary>
    public sealed class KeyValueStoreParams
    {
        /// <summary>
        /// Id / name of the store, must be unique within an application.
        /// Stores are cached by id (case insensitive), so parameters supplied with an id that is already in use are ignored.
        /// Null or empty is treated as "Default". Also used as the folder name of the store.
        /// </summary>
        public String Id = "Default";

        /// <summary>
        /// The serializer to use (a serializer name known to the serialization manager, ex: "json").
        /// </summary>
        public String Ser = "json";

        /// <summary>
        /// The compression method to use as an HTTP encoding code, ex: "br", "gzip" (can be null to disable compression, can be useful for small data or incompressible data)
        /// </summary>
        public String Comp = "br";

        /// <summary>
        /// The compression level to use
        /// </summary>
        public CompEncoderLevels Level = CompEncoderLevels.Balanced;

        /// <summary>
        /// The minimum number of file copies of each value.
        /// Values below 2 are clamped to 2, the value is increased to a multiple of the number of folders.
        /// A write replaces all but one of the copies (the most recent valid copy is kept until the next write).
        /// </summary>
        public int Redundance = 3;
        
        /// <summary>
        /// The location(s) of the files.
        /// Can be spread over multiple volumes to increase reliability, separate using the platform path separator (';' on Windows, ':' on Unix).
        /// If null, the default folders selected by <see cref="PerUser"/> and <see cref="PerApp"/> are used.
        /// </summary>
        public String Folders;

        /// <summary>
        /// If <see cref="Folders"/> isn't used, indicate if this store should be per user or not.
        /// </summary>
        public bool PerUser;

        /// <summary>
        /// If <see cref="Folders"/> isn't used, indicate if this store should be per app or not.
        /// </summary>
        public bool PerApp = true;

    }

}
