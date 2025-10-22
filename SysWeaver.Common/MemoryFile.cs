using System;

namespace SysWeaver
{
    /// <summary>
    /// Use to represent a "file".
    /// If mime = null and data = null, Name is a link to the "file".
    /// </summary>
    public sealed class MemoryFile
    {
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
            var d = new Byte[l];
            data.CopyTo(d.AsSpan());
            Name = name;
            Mime = mime;
            Data = d;
        }

    }


}
