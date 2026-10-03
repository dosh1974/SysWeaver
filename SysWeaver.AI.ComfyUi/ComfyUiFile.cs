using System;
using System.IO;

namespace SysWeaver.AI
{
    /// <summary>
    /// A file used as a workflow input (ex: the image of a "Load Image" node).
    /// Supply either Data or Url.
    /// Note: ComfyUI-Connect caches files by name, if a file with the same name already exist it's used (even if the content differs).
    /// If the name is omitted for data, a name is generated from a hash of the content.
    /// A byte[] property is the same as a ComfyUiFile with only Data set.
    /// </summary>
    public sealed class ComfyUiFile
    {
        public override string ToString() => Name ?? Url ?? (Data == null ? "null" : (Data.Length + " bytes"));

        /// <summary>
        /// The file name (required to be unique per content).
        /// If null, a name is generated from the content hash (for data) or the url.
        /// </summary>
        public String Name;

        /// <summary>
        /// The file content
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// An url that the ComfyUI-Connect server downloads the file from
        /// </summary>
        public String Url;

        /// <summary>
        /// Create from some data
        /// </summary>
        /// <param name="data">The file content</param>
        /// <param name="name">Optional name, if null a name is generated from the content hash</param>
        /// <returns></returns>
        public static ComfyUiFile FromData(byte[] data, String name = null) => new ComfyUiFile { Data = data, Name = name };

        /// <summary>
        /// Create from an url
        /// </summary>
        /// <param name="url">The url to download the file from</param>
        /// <param name="name">Optional name, if null the file name of the url is used</param>
        /// <returns></returns>
        public static ComfyUiFile FromUrl(String url, String name = null) => new ComfyUiFile { Url = url, Name = name };

        /// <summary>
        /// Create from a local file (the content is read now, the name is generated from the content hash)
        /// </summary>
        /// <param name="fileName">The local file name</param>
        /// <returns></returns>
        public static ComfyUiFile FromFile(String fileName) => new ComfyUiFile { Data = File.ReadAllBytes(fileName) };
    }

}
