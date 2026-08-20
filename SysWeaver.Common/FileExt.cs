using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Memory;

namespace SysWeaver
{
    public static class FileExt
    {

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="memory">The memory to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        public static void WriteMemory(String filename, ReadOnlyMemory<Byte> memory, bool ensureWriteTo = false)
        {
            using var s = new FileStream(filename, FileMode.Create, FileAccess.Write);
            s.Write(memory.Span);
            if (!ensureWriteTo)
                return;
            s.Flush(true);
            try
            {
                PlatformTools.Current.FlushToDisc(s.SafeFileHandle);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToFile(this ReadOnlyMemory<Byte> memory, String filename, bool ensureWriteTo = false)
            => WriteMemory(filename, memory, ensureWriteTo);

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="memory">The memory to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        public static async Task WriteMemoryAsync(String filename, ReadOnlyMemory<Byte> memory, bool ensureWriteTo = false)
        {
            using var s = new FileStream(filename, FileMode.Create, FileAccess.Write);
            var m = Mem.ToMemory(memory.Span);
            await s.WriteAsync(m).ConfigureAwait(false);
            if (!ensureWriteTo)
                return;
            await s.FlushAsync().ConfigureAwait(false);
            try
            {
                PlatformTools.Current.FlushToDisc(s.SafeFileHandle);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteToFileAsync(this ReadOnlyMemory<Byte> memory, String filename, bool ensureWriteTo = false)
            => WriteMemoryAsync(filename, memory, ensureWriteTo);

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteToFileAsync(this Memory<Byte> memory, String filename, bool ensureWriteTo = false)
            => WriteMemoryAsync(filename, memory, ensureWriteTo);

        /// <summary>
        /// Save all span to disc
        /// </summary>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="span">The span to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        public static void WriteSpan(String filename, ReadOnlySpan<Byte> span, bool ensureWriteTo = false)
        {
            using var s = new FileStream(filename, FileMode.Create, FileAccess.Write);
            s.Write(span);
            if (!ensureWriteTo)
                return;
            s.Flush();
            try
            {
                PlatformTools.Current.FlushToDisc(s.SafeFileHandle);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Save all span to disc
        /// </summary>
        /// <param name="span">The span to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToFile(this ReadOnlySpan<Byte> span, String filename, bool ensureWriteTo = false)
            => WriteSpan(filename, span, ensureWriteTo);


        /// <summary>
        /// Read byte content of a file, allowing shared read/write
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<Byte[]> ReadBytesAsync(String filename)
            => FileReadOnlyMemory.ReadAllBytesAsync(filename);

        /// <summary>
        /// Read byte content of a file, allowing shared read/write
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] ReadBytes(String filename)
            => FileReadOnlyMemory.ReadAllBytes(filename);

        /// <summary>
        /// Read all text from a file
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<String> ReadTextAsync(String filename, Encoding encoding = null)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllTextAsync(encoding);

        /// <summary>
        /// Read all text from a file
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ReadText(String filename, Encoding encoding = null)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllText(encoding);



        /// <summary>
        /// Read all text from a file
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<String[]> ReadLinesAsync(String filename, Encoding encoding = null, bool trim = false, bool removeEmpty = false)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllLinesAsync(encoding, false, trim, removeEmpty);

        /// <summary>
        /// Read all text from a file
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="encoding">The text encoding to use, default (null) is UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String[] ReadLines(String filename, Encoding encoding = null, bool trim = false, bool removeEmpty = false)
            => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).ReadAllLines(encoding, false, trim, removeEmpty);


        /// <summary>
        /// Check if a line is blang or starts with a comment
        /// </summary>
        /// <param name="t">The line of text (optionally with comments removed)</param>
        /// <param name="trimComment">If true, support # in the middle of a line to indictae a comment until end of line</param>
        /// <returns>True if the line is a comment or is empty</returns>
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
        /// <param name="filename"></param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns></returns>
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
        /// <param name="filename"></param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns></returns>
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
        /// <param name="filename"></param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns></returns>
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
        /// <param name="filename"></param>
        /// <param name="trimComment">If true, everything on a line after a '#' will be trimmed</param>
        /// <returns></returns>
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
        /// Read byte content of a file, allowing shared read/write with retying
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="retryCount">Number of times to retry the operation</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries (on error)</param>
        /// <param name="delayInMsNoExisting">Number of milli seconds to wait between any retries (when file deosn't exit)</param>
        /// <returns>Empty on error</returns>
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
        /// Read byte content of a file, allowing shared read/write with retying
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="retryCount">Number of times to retry the operation</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries (on error)</param>
        /// <param name="delayInMsNoExisting">Number of milli seconds to wait between any retries (when file deosn't exit)</param>
        /// <returns>Empty on error</returns>
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
