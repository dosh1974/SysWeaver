using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace SysWeaver
{

    public sealed class MemoryFileAudit
    {
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
    /// Use to represent a "file".
    /// If mime = null and data = null, Name is a link to the "file".
    /// </summary>
    public sealed class MemoryFile
    {
        public MemoryFileAudit GetAudit()
            => new MemoryFileAudit
            {
                Name = Name,
                Mime = Mime,
                Length = Data.Length,
            };

        public override string ToString() => String.Concat(Name.ToQuoted(), " [", Data.Length, "] as ", Mime);

        /// <summary>
        /// Recomended filename
        /// </summary>
        public String Name;
        
        /// <summary>
        /// Mime type
        /// </summary>
        public String Mime;
        
        /// <summary>
        /// Data
        /// </summary>
        public Byte[] Data;


        public MemoryFile(string url)
        {
            Name = url;
        }

        public MemoryFile(string name, string mime, Byte[] data)
        {
            Name = name;
            Mime = mime;
            Data = data;
        }

        public MemoryFile(string name, string mime, ReadOnlySpan<Byte> data)
        {
            var l = data.Length;
            var d = GC.AllocateUninitializedArray<Byte>(l);
            data.CopyTo(d.AsSpan());
            Name = name;
            Mime = mime;
            Data = d;
        }

        public static MemoryFile FromDataUri(String dataUri, String filename = null)
        {
            var header = dataUri.SplitFirst(',', out var data);
            var schema = header.SplitFirst(':', out var mimeAndEncoding);
            if (!schema.FastEquals("data:"))
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
