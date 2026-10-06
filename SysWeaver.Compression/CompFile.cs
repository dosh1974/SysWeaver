using System;
using System.IO;
using System.Text;

namespace SysWeaver.Compression
{
    /// <summary>
    /// Read text files that may be stored compressed on disc (ex: "words.txt" stored as "words.txt.br").
    /// </summary>
    public static class CompFile
    {
        /// <summary>
        /// Read the text from a file on disc, optionally compressed.
        /// If <paramref name="filename"/> exists it's read as is, else the first existing file named <paramref name="filename"/> + "." + a registered compression extension
        /// (in the order that the extensions were registered in the <see cref="CompManager"/>) is decompressed.
        /// </summary>
        /// <param name="filename">The filename without any compression extension</param>
        /// <param name="encoding">Optional text encoding, UTF8 assumed by default</param>
        /// <returns>The string content or null if neither the file nor a compressed version of it exists</returns>
        /// <exception cref="System.IO.InvalidDataException">The compressed file is invalid or truncated.</exception>
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
        /// Read the non-empty, non-comment lines of text from a file on disc, optionally compressed (see <see cref="TryGetAllText(string, Encoding)"/>).
        /// Comments are lines that start with a '#', lines are split on line feeds and trimmed.
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
