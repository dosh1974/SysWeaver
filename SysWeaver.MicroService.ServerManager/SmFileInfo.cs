using System;

namespace SysWeaver.MicroService
{
    public sealed class SmFileInfo
    {
#if DEBUG
        public override string ToString() => String.Concat('"', Name, "\" (", Size, ") @ ", LastModified);
#endif//DEBUG

        /// <summary>
        /// Name of the file
        /// </summary>
        public String Name;

        /// <summary>
        /// Size in bytes of the file
        /// </summary>
        public long Size;

        /// <summary>
        /// WHen the file was last modified
        /// </summary>
        public DateTime LastModified;
    }
}
