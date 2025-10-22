using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SysWeaver
{
    public static class ManagedTools
    {

        /// <summary>
        /// Read UTF8 data from a buffer and remove any preamble
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static String GetUtf8StringWithoutPreamble(Byte[] data, int offset, int length)
        {
            var encoding = Encoding.UTF8;
            var preamble = encoding.GetPreamble();
            var plen = preamble?.Length ?? 0;
            if ((plen > 0) && (length >= plen) && data.AsSpan().Slice(offset, plen).SequenceEqual(preamble.AsSpan()))
                return encoding.GetString(data, offset + plen, length - plen);
            return encoding.GetString(data, offset, length);
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
        /// <returns>The string</returns>
        public static String GetString(String s, Type embeddedResourceType = null, Action onFileChange = null)
            => InternalGet<String>(s,
                d => File.ReadAllText(d),
                d => GetUtf8StringWithoutPreamble(d.GetBuffer(), 0, (int)d.Position),
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
        /// <returns>The string</returns>
        public static Byte[] GetByteArray(String s, Type embeddedResourceType = null, Action onFileChange = null)
            => InternalGet<Byte[]>(s,
                d => File.ReadAllBytes(d),
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
        /// <returns>The string</returns>
        public static String[] GetLines(String s, Type embeddedResourceType = null, Action onFileChange = null)
            => InternalGet<String[]>(s,
                d => File.ReadAllLines(d),
                d => GetLines(GetUtf8StringWithoutPreamble(d.GetBuffer(), 0, (int)d.Position)),
                GetLines,
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

        static String[] GetLines(String s)
        {
            if (String.IsNullOrEmpty(s))
                return Array.Empty<String>();
            var lines = s.Split('\n');
            var lc = lines.Length;
            for (int i = 0; i < lc; ++i)
                lines[i] = lines[i].Trim('\r');
            return lines;
        }

        public static TextTemplate GetTemplate(String s, IReadOnlySet<String> vars, Type embeddedResourceType = null, Action onFileChange = null) => new TextTemplate(GetString(s, embeddedResourceType, onFileChange) ?? "", vars, true);


        public static T InternalGet<T>(String s, Func<String, T> getFromFile, Func<MemoryStream, T> getFromMemory, Func<String, T> getFromString, Type embeddedResourceType, Action onFileChange) where T : class
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
                    {
                        res = getFromFile(fn);
                        if (res is String)
                        {
                            var bb = Encoding.UTF8.GetBytes(res as String);
                            var t = bb;
                        }
                    }
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
                                using var ms = new MemoryStream();
                                using (var stream = asm.GetManifestResourceStream(t))
                                    stream.CopyTo(ms);
                                res = getFromMemory(ms);
                                isEmbedded = true;
                                break;
                            }
                            var exi = t.LastIndexOf('.');
                            if (exi < 0)
                                continue;
                            var comp = CompManager.GetFromExt(t.Substring(exi + 1));
                            if (comp == null)
                                continue;
                            {
                                using var ms = new MemoryStream();
                                using (var stream = asm.GetManifestResourceStream(t))
                                    comp.Decompress(stream, ms);
                                res = getFromMemory(ms);
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
                                    using var ms = new MemoryStream();
                                    using (var stream = asm.GetManifestResourceStream(t))
                                        stream.CopyTo(ms);
                                    res = getFromMemory(ms);
                                    isEmbedded = true;
                                    break;
                                }
                                var exi = t.LastIndexOf('.');
                                if (exi < 0)
                                    continue;
                                var comp = CompManager.GetFromExt(t.Substring(exi + 1));
                                if (comp == null)
                                    continue;
                                {
                                    using var ms = new MemoryStream();
                                    using (var stream = asm.GetManifestResourceStream(t))
                                        comp.Decompress(stream, ms);
                                    res = getFromMemory(ms);
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
