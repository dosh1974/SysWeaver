using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// Helpers for reading "managed" data, where a string can be a file name (monitored for changes), an embedded resource name or the data itself.
    /// </summary>
    public static class ManagedTools
    {

        /// <summary>
        /// Decode UTF8 data from a buffer, skipping any UTF8 byte order mark (preamble)
        /// </summary>
        /// <param name="data">The UTF8 encoded data</param>
        /// <returns>The decoded string</returns>
        public static String GetUtf8StringWithoutPreamble(ReadOnlySpan<Byte> data)
        {
            var encoding = Encoding.UTF8;
            var preamble = encoding.GetPreamble();
            var plen = preamble?.Length ?? 0;
            int offset = 0;
            int length = data.Length;
            if ((plen > 0) && (length >= plen) && data.Slice(offset, plen).SequenceEqual(preamble.AsSpan()))
            {
                offset += plen;
                length -= plen;
            }
            return encoding.GetString(data.Slice(offset, length));
        }


        /// <summary>
        /// Get some string data from a string.
        /// The input is evaluated in the following order:
        /// - If it points to a valid local file, it's read using UTF8 encoding.
        /// - If a type is supplied and an embedded resource exist (raw or compressed using any of the supported compressors), the embedded data is read as UTF8.
        /// - The string is the input string
        /// </summary>
        /// <param name="s">Input data, can be a filename, the actual data or if a type is supplied the embedded resource</param>
        /// <param name="embeddedResourceType">If supplied the data can be read from an embedded resource</param>
        /// <param name="onFileChange">Optionally called the first time the file changes (reload)</param>
        /// <returns>The string, null if <paramref name="s"/> is null or empty</returns>
        public static String GetString(String s, Type embeddedResourceType = null, Action onFileChange = null)
            => InternalGet<String>(s,
                d => FileExt.ReadText(d),
                d => GetUtf8StringWithoutPreamble(d.Span),
                d => d,
                embeddedResourceType,
                onFileChange);


        /// <summary>
        /// Get some data from a string.
        /// The input is evaluated in the following order:
        /// - If it points to a valid local file, it's read.
        /// - If a type is supplied and an embedded resource exist (raw or compressed using any of the supported compressors), the embedded data is read.
        /// - The data is the input string base64 encoded
        /// </summary>
        /// <param name="s">Input data, can be a filename, the actual data (as base64) or if a type is supplied the embedded resource</param>
        /// <param name="embeddedResourceType">If supplied the data can be read from an embedded resource</param>
        /// <param name="onFileChange">Optionally called the first time the file changes (reload)</param>
        /// <returns>The data, null if <paramref name="s"/> is null or empty, or not valid base64 (when used as the data)</returns>
        public static Byte[] GetByteArray(String s, Type embeddedResourceType = null, Action onFileChange = null)
            => InternalGet<Byte[]>(s,
                d => FileExt.ReadBytes(d),
                d => d.ToArray(),
                TryReadBase64,
                embeddedResourceType,
                onFileChange);



        /// <summary>
        /// Get some text lines from a string.
        /// The input is evaluated in the following order:
        /// - If it points to a valid local file, the lines are read from it using UTF8 encoding.
        /// - If a type is supplied and an embedded resource exist (raw or compressed using any of the supported compressors), the embedded data is read as UTF8 and split on new lines.
        /// - The string is the input string split on new lines
        /// </summary>
        /// <param name="s">Input data, can be a filename, the actual data or if a type is supplied the embedded resource</param>
        /// <param name="embeddedResourceType">If supplied the data can be read from an embedded resource</param>
        /// <param name="onFileChange">Optionally called the first time the file changes (reload)</param>
        /// <returns>The lines, null if <paramref name="s"/> is null or empty</returns>
        public static String[] GetLines(String s, Type embeddedResourceType = null, Action onFileChange = null)
            => InternalGet<String[]>(s,
                d => FileExt.ReadLines(d),
                d => GetLines(GetUtf8StringWithoutPreamble(d.Span)),
                d => d.GetLines(),
                embeddedResourceType,
                onFileChange);

        static Byte[] TryReadBase64(String s)
        {
            if (String.IsNullOrEmpty(s))
                return null;
            try
            {
                return Convert.FromBase64String(s);
            }
            catch
            {
                return null;
            }
        }



        /// <summary>
        /// Create a text template from a string, see <see cref="GetString(string, Type, Action)"/> for how the input is evaluated.
        /// </summary>
        /// <param name="s">Input data, can be a filename, the actual template text or if a type is supplied the embedded resource</param>
        /// <param name="vars">The variables that the template may use</param>
        /// <param name="embeddedResourceType">If supplied the data can be read from an embedded resource</param>
        /// <param name="onFileChange">Optionally called the first time the file changes (reload)</param>
        /// <returns>The template (an empty template if no data was found)</returns>
        public static TextTemplate GetTemplate(String s, IReadOnlySet<String> vars, Type embeddedResourceType = null, Action onFileChange = null) => new TextTemplate(GetString(s, embeddedResourceType, onFileChange) ?? "", vars, true);


        /// <summary>
        /// Get some data from a string using the supplied conversion functions.
        /// The input is evaluated in the following order:
        /// - If it is a valid path to an existing file (relative paths are relative to the executable), it's read using <paramref name="getFromFile"/>.
        /// - If a type is supplied and an embedded resource exist with the exact name, or the name prefixed with the namespace of the type (case insensitive), it's read using <paramref name="getFromMemory"/>.
        /// - Else <paramref name="getFromString"/> is called with the input string.
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="s">The input string</param>
        /// <param name="getFromFile">Read the data from a file name</param>
        /// <param name="getFromMemory">Read the data from (uncompressed) embedded resource data</param>
        /// <param name="getFromString">Get the data from the string itself</param>
        /// <param name="embeddedResourceType">If supplied the data can be read from an embedded resource</param>
        /// <param name="onFileChange">If non null and the input is a valid file path, a file monitor is created and this is called (once) the first time the file changes</param>
        /// <returns>The data, null if <paramref name="s"/> is null or empty</returns>
        /// <remarks>Errors reading files or resources are ignored (falls through to the next option).</remarks>
        public static T InternalGet<T>(String s, Func<String, T> getFromFile, Func<ReadOnlyMemory<Byte>, T> getFromMemory, Func<String, T> getFromString, Type embeddedResourceType, Action onFileChange) where T : class
        {
            if (String.IsNullOrEmpty(s))
                return default;
            T res = null;
            bool isFile = false;
            //  If it's a filename, load it from disc
            try
            {
                var fn = EnvInfo.MakeAbsoulte(s);
                if (PathExt.IsValidFilePath(fn))
                {
                    isFile = File.Exists(fn);
                    if (isFile)
                        res = getFromFile(fn);
                    if (onFileChange != null)
                    {
                        new ManagedFile(new ManagedFileParams
                        {
                            Location = fn,
                        }, d =>
                        {
                            try
                            {
                                onFileChange?.Invoke();
                            }
                            catch
                            {
                            }
                            d.Manager.Dispose();
                        });
                    }
                }
            }
            catch
            {
            }
            if (res == null)
            {
                bool isEmbedded = false;
                //  If it's an embedded resource, use that
                if ((!isFile) && (embeddedResourceType != null))
                {
                    try
                    {
                        var asm = embeddedResourceType.Assembly;
                        var rs = asm.GetManifestResourceNames();
                        var en = s;
                        foreach (var t in rs)
                        {
                            if (!t.StartsWith(en, StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (String.Equals(t, en, StringComparison.OrdinalIgnoreCase))
                            {
                                var mem = asm.GetUncompressedResourceData(t);
                                res = getFromMemory(mem);
                                isEmbedded = true;
                                break;
                            }
                        }
                        if (!isEmbedded)
                        {
                            en = embeddedResourceType.Namespace + "." + s;
                            foreach (var t in rs)
                            {
                                if (!t.StartsWith(en, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                if (String.Equals(t, en, StringComparison.OrdinalIgnoreCase))
                                {
                                    var mem = asm.GetUncompressedResourceData(t);
                                    res = getFromMemory(mem);
                                    isEmbedded = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return res ?? getFromString(s);
        }



    }


}
