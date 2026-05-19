using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SysWeaver.Compression
{
    public static class CompFile
    {
        /// <summary>
        /// Read the text from a file on disc, optionally compressed
        /// </summary>
        /// <param name="filename">The filename without any compression extension</param>
        /// <param name="encoding">Optional text encoding, UTF8 assumed by default</param>
        /// <returns>The string content or null if file doesn't exist</returns>
        public static String TryGetAllText(String filename, Encoding encoding = null)
        {
            var fi = new FileInfo(filename);
            if (fi.Exists)
            {
                using var m = FileReadOnlyMemory.Read(fi.FullName);
                return (encoding ?? Encoding.UTF8).GetString(m.Memory.Span);
            }
            foreach (var x in CompManager.ExtensionHandlers)
            {
                fi = new FileInfo(filename + x.Key);
                if (fi.Exists)
                {
                    using var m = FileReadOnlyMemory.Read(fi.FullName);
                    var t = x.Value.GetDecompressed(m.Memory.Span);
                    return (encoding ?? Encoding.UTF8).GetString(t.Span);
                }
            }
            return null;
        }

        /// <summary>
        /// Read the non-empty, non-comment lines of text from a file on disc, optionally compressed.
        /// Ccomments are lines that start with a '#'.
        /// </summary>
        /// <param name="filename">The filename without any compression extension</param>
        /// <param name="encoding">Optional text encoding, UTF8 assumed by default</param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed (treated as a comment)</param>
        /// <returns>The non-empty, non-comment lines or null if file doesn't exist</returns>
        public static String[] TryGetNonCommentLines(String filename, Encoding encoding = null, bool trimComment = false)
        {
            var s = TryGetAllText(filename, encoding);
            if (s == null)
                return null;
            return s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => !FileExt.IsCommentOrBlank(ref x, trimComment)).ToArray();
        }


    }
}
