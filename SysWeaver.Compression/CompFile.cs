using System;
using System.IO;
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
            encoding ??= Encoding.UTF8;
            if (File.Exists(filename))
            {
                using var m = FileReadOnlyMemory.Read(Path.GetFullPath(filename));
                return encoding.GetString(m.Memory.Span);
            }
            foreach (var x in CompManager.ExtensionArray)
            {
                var ext = x.Key;
                //  The extensions are registered both with and without the '.' prefix, only use the ones with the prefix
                if ((ext.Length <= 0) || (ext[0] != '.'))
                    continue;
                var name = String.Concat(filename, ext);
                if (!File.Exists(name))
                    continue;
                using var m = FileReadOnlyMemory.Read(Path.GetFullPath(name));
                //  Decompress to a pooled buffer, only the string is allocated
                using var t = x.Value.GetUnmanagedDecompressed(m.Memory.Span);
                return encoding.GetString(t.Memory.Span);
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
            var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            //  Remove the comments in place
            int count = 0;
            for (int i = 0; i < lines.Length; ++i)
            {
                var x = lines[i];
                if (!FileExt.IsCommentOrBlank(ref x, trimComment))
                    lines[count++] = x;
            }
            if (count == lines.Length)
                return lines;
            Array.Resize(ref lines, count);
            return lines;
        }


    }
}
