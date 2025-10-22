using System;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Information about a file that is about to be uploaded
    /// </summary>
    public sealed class FileUploadInfo
    {
        public override string ToString() => Name;

        /// <summary>
        /// Name of the file (only filename and extension, no path)
        /// </summary>
        public String Name;

        /// <summary>
        /// Length in bytes of the file data
        /// </summary>
        public long Length;

        /// <summary>
        /// The MD5 checksum of the file content, encoded using hex values, "abcd...", where a = is the high nibble of the first byte, b is the low nibble of the first byte and so on...
        /// </summary>
        public String Hash;

        /// <summary>
        /// Numer of milliseconds since the Unix epoch
        /// </summary>
        public double LastModified;

        /// <summary>
        /// Get the file extension (including the leading '.') or String.Empty if there is none
        /// </summary>
        /// <returns></returns>
        public String GetExtension()
        {
            var n = Name;
            var i = n.LastIndexOf('.');
            if (i < 0)
                return "";
            return n.Substring(i);
        }

        /// <summary>
        /// Set the LastModified field from a an UTC DateTime time.
        /// </summary>
        /// <param name="utcLastModified">The last modified time in UTC</param>
        public void SetLastModified(DateTime utcLastModified)
        {
            LastModified = new DateTimeOffset(utcLastModified).ToUnixTimeMilliseconds();
        }

    }
}
