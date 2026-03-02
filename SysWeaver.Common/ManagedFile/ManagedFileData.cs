using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// The current state of a managed file
    /// </summary>
    public sealed class ManagedFileData
    {
        public readonly String Location;
        public readonly Memory<Byte> Data;
        public readonly DateTime LastWriteTimeUtc;
        public readonly ManagedFile Manager;


        /// <summary>
        /// Get the data as a string
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String GetAsString(Encoding encoding = null)
            => (encoding ?? Encoding.UTF8).GetString(Data.Span);


        /// <summary>
        /// Get the data as an array of strings
        /// </summary>
        /// <param name="encoding">The text encoding, defaults to UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String[] GetAsStringArray(Encoding encoding = null, bool trim = false, bool removeEmpty = false)
            => Data.Span.ToStringArray(encoding, trim, removeEmpty);


        public override string ToString() => Location.ToQuoted();

        public ManagedFileData(string location, Memory<Byte> data, DateTime lastWriteTimeUtc, byte[] hash, ManagedFile manager)
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
