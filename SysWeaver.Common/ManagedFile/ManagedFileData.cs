using System;

namespace SysWeaver
{
    /// <summary>
    /// The current state of a managed file
    /// </summary>
    public sealed class ManagedFileData
    {
        public readonly String Location;
        public readonly Byte[] Data;
        public readonly DateTime LastWriteTimeUtc;
        public readonly ManagedFile Manager;

        public override string ToString() => Location.ToQuoted();

        public ManagedFileData(string location, byte[] data, DateTime lastWriteTimeUtc, byte[] hash, ManagedFile manager)
        {
            Location = location;
            Data = data;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Hash = hash;
            Manager = manager;
        }

        internal readonly Byte[] Hash;
    }

}
