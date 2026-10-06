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
        /// <summary>
        /// The resolved location of the file (file name or url)
        /// </summary>
        public readonly String Location;
        /// <summary>
        /// The content of the file (empty if the read failed)
        /// </summary>
        public readonly Memory<Byte> Data;
        /// <summary>
        /// The last write time of the file (for web files the Last-Modified header, or the time of the request), <see cref="DateTime.MinValue"/> if the read failed
        /// </summary>
        public readonly DateTime LastWriteTimeUtc;
        /// <summary>
        /// The managed file that this data belongs to
        /// </summary>
        public readonly ManagedFile Manager;


        /// <summary>
        /// Get the data as a string (a byte order mark is NOT removed)
        /// </summary>
        /// <param name="encoding">The text encoding, defaults to UTF8</param>
        /// <returns>The decoded text</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String GetAsString(Encoding encoding = null)
            => (encoding ?? Encoding.UTF8).GetString(Data.Span);


        /// <summary>
        /// Get the data as an array of strings
        /// </summary>
        /// <param name="encoding">The text encoding, defaults to UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>The lines of text (a byte order mark is removed)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String[] GetAsStringArray(Encoding encoding = null, bool trim = false, bool removeEmpty = false)
            => Data.Span.ToStringArray(encoding, trim, removeEmpty);


        /// <summary>
        /// The quoted location
        /// </summary>
        /// <returns>The quoted location</returns>
        public override string ToString() => Location.ToQuoted();

        /// <summary>
        /// The exception if the read failed, else null
        /// </summary>
        public readonly Exception Ex;

        /// <summary>
        /// Create the data of a managed file (used by <see cref="IManagedFileSource"/> implementations)
        /// </summary>
        /// <param name="location">The resolved location of the file</param>
        /// <param name="data">The content of the file</param>
        /// <param name="lastWriteTimeUtc">The last write time of the file</param>
        /// <param name="hash">The hash of the content (used to detect if the content changed), may be null if hashing is disabled</param>
        /// <param name="manager">The managed file that this data belongs to</param>
        /// <param name="ex">The exception if the read failed, else null</param>
        public ManagedFileData(string location, Memory<Byte> data, DateTime lastWriteTimeUtc, byte[] hash, ManagedFile manager, Exception ex)
        {
            Location = location;
            Data = data;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Hash = hash;
            Manager = manager;
            Ex = ex;
        }

        /// <summary>
        /// The hash of the content (MD5), null if hashing is disabled or the read failed
        /// </summary>
        internal readonly Byte[] Hash;
    }

}
