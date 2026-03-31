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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToFile(this ReadOnlyMemory<Byte> memory, String filename)
            => WriteMemory(filename, memory);

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="memory">The memory to save</param>
        /// <param name="ensureWriteTo">If true, the function doesn't return until the data have been physically written to disc (or at least it tries to)</param>
        public static async ValueTask WriteMemoryAsync(String filename, ReadOnlyMemory<Byte> memory, bool ensureWriteTo = false)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WriteToFileAsync(this ReadOnlyMemory<Byte> memory, String filename)
            => WriteMemoryAsync(filename, memory);

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WriteToFileAsync(this Memory<Byte> memory, String filename)
            => WriteMemoryAsync(filename, memory);

        /// <summary>
        /// Save all span to disc
        /// </summary>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="span">The span to save</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSpan(String filename, ReadOnlySpan<Byte> span)
        {
            using var s = new FileStream(filename, FileMode.Create, FileAccess.Write);
            s.Write(span);
        }

        /// <summary>
        /// Save all span to disc
        /// </summary>
        /// <param name="span">The span to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToFile(this ReadOnlySpan<Byte> span, String filename)
            => WriteSpan(filename, span);


        /// <summary>
        /// Read byte content of a file, allowing shared read/write
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <returns>Empty on error</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<Byte[]> ReadBytesAsync(String filename)
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
        public static ValueTask<String> ReadTextAsync(String filename, Encoding encoding = null)
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
        public static ValueTask<String[]> ReadLinesAsync(String filename, Encoding encoding = null, bool trim = false, bool removeEmpty = false)
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
        /// Return the first non-empty, non-comment line of text (comments are lines that start with a '#').
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static String ReadNonCommentString(String filename)
        {
            var l = ReadLines(filename, null, true, true);
            var lc = l.Length;
            if (lc < 1)
                return null;
            for (int i = 0; i < lc; ++i)
            {
                var t = l[i];
                if (t[0] == '#')
                    continue;
                return t;
            }
            return null;
        }



        /// <summary>
        /// Read byte content of a file, allowing shared read/write with retying
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="retryCount">Number of times to retry the operation</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries (on error)</param>
        /// <param name="delayInMsNoExisting">Number of milli seconds to wait between any retries (when file deosn't exit)</param>
        /// <returns>Empty on error</returns>
        public static async ValueTask<Memory<Byte>> TryReadBytesAsync(String filename, int retryCount = 10, int delayInMs = 100, int delayInMsNoExisting = 1)
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
        public static async ValueTask<ReadOnlyMemory<Byte>> TryReadMemoryAsync(String filename, int retryCount = 10, int delayInMs = 100, int delayInMsNoExisting = 1)
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
