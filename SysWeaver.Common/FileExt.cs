using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace SysWeaver
{
    /// <summary>
    /// Read and write files (whole file operations).
    /// </summary>
    /// <remarks>
    /// Writes are not atomic, the file is truncated first, so a failed write leaves a partial file (write to a temp file and move it if atomicity is required).
    /// Reads uses <see cref="FileReadOnlyMemory"/> (memory mapped io when possible).
    /// </remarks>
    public static class FileExt
    {

        /// <summary>
        /// Save all memory to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="memory">The memory to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        public static void WriteMemory(String filename, ReadOnlyMemory<Byte> memory, bool ensureWriteTo = false)
            => WriteSpan(filename, memory.Span, ensureWriteTo);

        /// <summary>
        /// Open a file for writing (create or truncate), allowing others to read it while it's open
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static SafeFileHandle OpenWrite(String filename)
            => File.OpenHandle(filename, FileMode.Create, FileAccess.Write, FileShare.Read);

        /// <summary>
        /// Flush data to the physical disc (ignoring any errors)
        /// </summary>
        static void FlushToDisc(SafeFileHandle h)
        {
            try
            {
                RandomAccess.FlushToDisk(h);
                PlatformTools.Current.FlushToDisc(h);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Save all memory to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToFile(this ReadOnlyMemory<Byte> memory, String filename, bool ensureWriteTo = false)
            => WriteMemory(filename, memory, ensureWriteTo);

        /// <summary>
        /// Save all memory to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="memory">The memory to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        public static async Task WriteMemoryAsync(String filename, ReadOnlyMemory<Byte> memory, bool ensureWriteTo = false)
        {
            using var h = OpenWrite(filename);
            if (memory.Length > 0)
                await RandomAccess.WriteAsync(h, memory, 0).ConfigureAwait(false);
            if (ensureWriteTo)
                FlushToDisc(h);
        }

        /// <summary>
        /// Save all memory to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteToFileAsync(this ReadOnlyMemory<Byte> memory, String filename, bool ensureWriteTo = false)
            => WriteMemoryAsync(filename, memory, ensureWriteTo);

        /// <summary>
        /// Save all memory to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteToFileAsync(this Memory<Byte> memory, String filename, bool ensureWriteTo = false)
            => WriteMemoryAsync(filename, memory, ensureWriteTo);

        /// <summary>
        /// Save all span to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="span">The span to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        public static void WriteSpan(String filename, ReadOnlySpan<Byte> span, bool ensureWriteTo = false)
        {
            using var h = OpenWrite(filename);
            if (span.Length > 0)
                RandomAccess.Write(h, span, 0);
            if (ensureWriteTo)
                FlushToDisc(h);
        }

        /// <summary>
        /// Save all span to a file (any existing file is overwritten), others can read the file while it's written
        /// </summary>
        /// <param name="span">The span to save</param>
        /// <param name="filename">The file to write to (overwrites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: the file is read only, hidden or a directory).</exception>
        /// <exception cref="IOException">An I/O error occured (ex: the file is open by someone else).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToFile(this ReadOnlySpan<Byte> span, String filename, bool ensureWriteTo = false)
            => WriteSpan(filename, span, ensureWriteTo);


        /// <summary>
        /// Read byte content of a file.
        /// The file is opened with shared read only (FileShare.Read), so this fails while another process has the file open for writing.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <returns>The content of the file</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<Byte[]> ReadBytesAsync(String filename)
            => FileReadOnlyMemory.ReadAllBytesAsync(filename);

        /// <summary>
        /// Read byte content of a file.
        /// The file is opened with shared read only (FileShare.Read), so this fails while another process has the file open for writing.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <returns>The content of the file</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] ReadBytes(String filename)
            => FileReadOnlyMemory.ReadAllBytes(filename);

        /// <summary>
        /// Read all text from a file.
        /// The file is opened with FileShare.ReadWrite, so it can be read while someone else is writing to it.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <returns>The text content of the file (any byte order mark is removed)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<String> ReadTextAsync(String filename, Encoding encoding = null)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllTextAsync(encoding);

        /// <summary>
        /// Read all text from a file.
        /// The file is opened with FileShare.ReadWrite, so it can be read while someone else is writing to it.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <returns>The text content of the file (any byte order mark is removed)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ReadText(String filename, Encoding encoding = null)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllText(encoding);



        /// <summary>
        /// Read all lines of text from a file (lines are separated by "\n" or "\r\n").
        /// The file is opened with FileShare.ReadWrite, so it can be read while someone else is writing to it.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>The lines of the file (an empty array for an empty file)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<String[]> ReadLinesAsync(String filename, Encoding encoding = null, bool trim = false, bool removeEmpty = false)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllLinesAsync(encoding, false, trim, removeEmpty);

        /// <summary>
        /// Read all lines of text from a file (lines are separated by "\n" or "\r\n").
        /// The file is opened with FileShare.ReadWrite, so it can be read while someone else is writing to it.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>The lines of the file (an empty array for an empty file)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String[] ReadLines(String filename, Encoding encoding = null, bool trim = false, bool removeEmpty = false)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllLines(encoding, false, trim, removeEmpty);


        /// <summary>
        /// Check if a line is blank or starts with a comment (the first non-white space char is a '#')
        /// </summary>
        /// <param name="t">The line of text, if <paramref name="trimComment"/> is true and the line isn't a comment or blank, it's set to the text before the first '#' (with trailing white spaces removed), else it's unchanged</param>
        /// <param name="trimComment">If true, support # in the middle of a line to indicate a comment until end of line</param>
        /// <returns>True if the line is a comment or is empty (or white space only)</returns>
        /// <exception cref="NullReferenceException"><paramref name="t"/> is null.</exception>
        public static bool IsCommentOrBlank(ref String t, bool trimComment)
        {
            var tt = t.Trim();
            if (tt.Length <= 0)
                return true;
            var ci = tt.IndexOf('#');
            if (ci < 0)
                return false;
            if (ci <= 0)
                return true;
            if (trimComment)
                t = t.Substring(0, t.IndexOf('#')).TrimEnd();
            return false;
        }

        /// <summary>
        /// Return the first non-empty, non-comment line of text (comments are lines that start with a '#').
        /// </summary>
        /// <param name="filename">Name of the file to read (UTF-8 encoded)</param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns>The first line that isn't blank or a comment (white spaces are trimmed), null if no such line exists</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        public static String ReadNonCommentString(String filename, bool trimComment = false)
        {
            var l = ReadLines(filename, null, true, true);
            var lc = l.Length;
            if (lc < 1)
                return null;
            for (int i = 0; i < lc; ++i)
            {
                var t = l[i];
                if (IsCommentOrBlank(ref t, trimComment))
                    continue;
                return t;
            }
            return null;
        }

        /// <summary>
        /// Return the non-empty, non-comment lines of text (comments are lines that start with a '#').
        /// </summary>
        /// <param name="filename">Name of the file to read (UTF-8 encoded)</param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns>All lines that aren't blank or a comment (white spaces are trimmed), an empty array if no such line exists</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        public static String[] ReadNonCommentLines(String filename, bool trimComment = false)
        {
            var l = ReadLines(filename, null, true, true);
            var lc = l.Length;
            if (lc < 1)
                return Array.Empty<String>();
            int o = 0;
            for (int i = 0; i < lc; ++i)
            {
                var t = l[i];
                if (IsCommentOrBlank(ref t, trimComment))
                    continue;
                l[o] = t;
                ++o;
            }
            if (o <= 0)
                return Array.Empty<String>();
            Array.Resize(ref l, o);
            return l;
        }

        /// <summary>
        /// Return the first non-empty, non-comment line of text (comments are lines that start with a '#').
        /// </summary>
        /// <param name="filename">Name of the file to read (UTF-8 encoded)</param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns>The first line that isn't blank or a comment (white spaces are trimmed), null if no such line exists</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        public static async Task<String> ReadNonCommentStringAsync(String filename, bool trimComment = false)
        {
            var l = await ReadLinesAsync(filename, null, true, true).ConfigureAwait(false);
            var lc = l.Length;
            if (lc < 1)
                return null;
            for (int i = 0; i < lc; ++i)
            {
                var t = l[i];
                if (IsCommentOrBlank(ref t, trimComment))
                    continue;
                return t;
            }
            return null;
        }

        /// <summary>
        /// Return the non-empty, non-comment lines of text (comments are lines that start with a '#').
        /// </summary>
        /// <param name="filename">Name of the file to read (UTF-8 encoded)</param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns>All lines that aren't blank or a comment (white spaces are trimmed), an empty array if no such line exists</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="filename"/> is empty or invalid.</exception>
        /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
        /// <exception cref="DirectoryNotFoundException">The folder of the file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Access denied (ex: it's a directory).</exception>
        /// <exception cref="IOException">An I/O error occured.</exception>
        public static async Task<String[]> ReadNonCommentLinesAsync(String filename, bool trimComment = false)
        {
            var l = await ReadLinesAsync(filename, null, true, true).ConfigureAwait(false);
            var lc = l.Length;
            if (lc < 1)
                return Array.Empty<String>();
            int o = 0;
            for (int i = 0; i < lc; ++i)
            {
                var t = l[i];
                if (IsCommentOrBlank(ref t, trimComment))
                    continue;
                l[o] = t;
                ++o;
            }
            if (o <= 0)
                return Array.Empty<String>();
            Array.Resize(ref l, o);
            return l;
        }





        /// <summary>
        /// Read byte content of a file, with retrying.
        /// The file is opened with shared read only (FileShare.Read), so a read fails (and is retried) while another process has the file open for writing.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="retryCount">The max number of attempts (at least one attempt is always made)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries (on error)</param>
        /// <param name="delayInMsNoExisting">Number of milli seconds to wait between any retries (when the file doesn't exist)</param>
        /// <returns>The content of the file, empty if the file couldn't be read (never throws).
        /// Note that an empty file also returns empty</returns>
        /// <remarks>A missing file is also retried, so with the defaults a missing file takes approximately 10 short delays before returning</remarks>
        public static async Task<Memory<Byte>> TryReadBytesAsync(String filename, int retryCount = 10, int delayInMs = 100, int delayInMsNoExisting = 1)
        {
            for (; ; )
            {
                try
                {
                    if (File.Exists(filename))
                        return await ReadBytesAsync(filename).ConfigureAwait(false);
                    --retryCount;
                    if (retryCount <= 0)
                        return null;
                    await Task.Delay(delayInMsNoExisting).ConfigureAwait(false);
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        return null;
                    await Task.Delay(delayInMs).ConfigureAwait(false);
                }
            }

        }


        /// <summary>
        /// Read byte content of a file, with retrying.
        /// The file is opened with shared read only (FileShare.Read), so a read fails (and is retried) while another process has the file open for writing.
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="retryCount">The max number of attempts (at least one attempt is always made)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries (on error)</param>
        /// <param name="delayInMsNoExisting">Number of milli seconds to wait between any retries (when the file doesn't exist)</param>
        /// <returns>The content of the file, empty if the file couldn't be read (never throws).
        /// Note that an empty file also returns empty</returns>
        /// <remarks>A missing file is also retried, so with the defaults a missing file takes approximately 10 short delays before returning</remarks>
        public static async Task<ReadOnlyMemory<Byte>> TryReadMemoryAsync(String filename, int retryCount = 10, int delayInMs = 100, int delayInMsNoExisting = 1)
        {
            for (; ; )
            {
                try
                {
                    if (File.Exists(filename))
                        return await ReadBytesAsync(filename).ConfigureAwait(false);
                    --retryCount;
                    if (retryCount <= 0)
                        return null;
                    await Task.Delay(delayInMsNoExisting).ConfigureAwait(false);
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        return null;
                    await Task.Delay(delayInMs).ConfigureAwait(false);
                }
            }

        }

    }

}
