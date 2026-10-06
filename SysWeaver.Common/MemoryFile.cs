using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace SysWeaver
{

    /// <summary>
    /// A summary of a <see cref="MemoryFile"/> without the data (name, mime type and length), used for auditing / logging.
    /// </summary>
    public sealed class MemoryFileAudit
    {
        /// <summary>
        /// A short description, ex: "image.png" [1234] as image/png
        /// </summary>
        /// <returns>A short description of the file</returns>
        public override string ToString() => String.Concat(Name.ToQuoted(), " [", Length, "] as ", Mime);

        /// <summary>
        /// Recomended filename
        /// </summary>
        public String Name;

        /// <summary>
        /// Mime type
        /// </summary>
        public String Mime;

        /// <summary>
        /// File length
        /// </summary>
        public long Length;

    }

    /// <summary>
    /// Use to represent a "file" (a name, a mime type and the data in memory), ex: as an api argument or return value.
    /// If mime = null and data = null, Name is a link to the "file".
    /// </summary>
    public sealed class MemoryFile
    {
        /// <summary>
        /// Get a summary of this file (without the data)
        /// </summary>
        /// <returns>A new audit object (the length is 0 for a link)</returns>
        public MemoryFileAudit GetAudit()
            => new MemoryFileAudit
            {
                Name = Name,
                Mime = Mime,
                Length = Data?.Length ?? 0,
            };

        /// <summary>
        /// A short description, ex: "image.png" [1234] as image/png (the length is 0 for a link)
        /// </summary>
        /// <returns>A short description of the file</returns>
        public override string ToString() => String.Concat(Name.ToQuoted(), " [", Data?.Length ?? 0, "] as ", Mime);

        /// <summary>
        /// Recomended filename
        /// </summary>
        public String Name;
        
        /// <summary>
        /// Mime type
        /// </summary>
        public String Mime;
        
        /// <summary>
        /// The content of the file, null if this is a link
        /// </summary>
        public Byte[] Data;


        /// <summary>
        /// Create a link to a file (<see cref="Mime"/> and <see cref="Data"/> are null)
        /// </summary>
        /// <param name="url">The url of the file</param>
        public MemoryFile(string url)
        {
            Name = url;
        }

        /// <summary>
        /// Create a file (the data array is referenced, not copied)
        /// </summary>
        /// <param name="name">The recommended filename</param>
        /// <param name="mime">The mime type</param>
        /// <param name="data">The content of the file</param>
        public MemoryFile(string name, string mime, Byte[] data)
        {
            Name = name;
            Mime = mime;
            Data = data;
        }

        /// <summary>
        /// Create a file from a copy of some data
        /// </summary>
        /// <param name="name">The recommended filename</param>
        /// <param name="mime">The mime type</param>
        /// <param name="data">The content of the file (copied to a new array)</param>
        public MemoryFile(string name, string mime, ReadOnlySpan<Byte> data)
        {
            var l = data.Length;
            var d = GC.AllocateUninitializedArray<Byte>(l);
            data.CopyTo(d.AsSpan());
            Name = name;
            Mime = mime;
            Data = d;
        }

        /// <summary>
        /// Create a file from a data uri, ex: "data:image/png;base64,iVBORw0...".
        /// </summary>
        /// <param name="dataUri">The data uri, the data is either base64 encoded (";base64") or url encoded UTF-8 text</param>
        /// <param name="filename">The filename to use, if null or empty the name is "Data" + the first known extension of the mime type (or "Data.png" if the mime type is unknown)</param>
        /// <returns>A new file</returns>
        /// <exception cref="Exception">The text isn't a data uri</exception>
        /// <exception cref="FormatException">The base64 data is invalid</exception>
        /// <remarks>Only a single parameter after the mime type is supported, so ex: "data:text/plain;charset=utf-8;base64,..." is not decoded as base64</remarks>
        public static MemoryFile FromDataUri(String dataUri, String filename = null)
        {
            var header = dataUri.SplitFirst(',', out var data);
            var schema = header.SplitFirst(':', out var mimeAndEncoding);
            if (!schema.FastEquals("data"))
                throw new Exception("Invalid data uri");
            var mime = mimeAndEncoding.SplitFirst(';', out var encoding);
            if (String.IsNullOrEmpty(filename) && MimeTypeMap.TryGetExtensions(mime, out var exts))
            {
                var ext = exts.FirstOrDefault();
                if (ext != null)
                    filename = "Data" + ext;
            }
            if (String.IsNullOrEmpty(filename))
                filename = "Data.png";
            if (encoding.FastEquals("base64"))
                return new MemoryFile(filename, mime, Convert.FromBase64String(data));
            data = HttpUtility.UrlDecode(data);
            return new MemoryFile(filename, mime, Encoding.UTF8.GetBytes(data));
        }


    }


}
