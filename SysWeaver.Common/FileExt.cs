using System;
using System.IO;
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
        public static ValueTask WriteToFileAsync(this ReadOnlyMemory<Byte> memory, String filename)
            => WriteMemoryAsync(filename, memory);

        /// <summary>
        /// Save all memory to disc
        /// </summary>
        /// <param name="memory">The memory to save</param>
        /// <param name="filename">The file to write to (overwites existing)</param>
        public static ValueTask WriteToFileAsync(this Memory<Byte> memory, String filename)
            => WriteMemoryAsync(filename, memory);

        /// <summary>
        /// Save all span to disc
        /// </summary>
        /// <param name="filename">The file to write to (overwites existing)</param>
        /// <param name="span">The span to save</param>
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
        public static void WriteToFile(this ReadOnlySpan<Byte> span, String filename)
            => WriteSpan(filename, span);


        /// <summary>
        /// Read byte content of a file, allowing shared read/write
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <returns>Empty on error</returns>
        public static async ValueTask<ReadOnlyMemory<Byte>> ReadBytesAsync(String filename)
        {
            var fi = new FileInfo(filename);
            if (!fi.Exists)
                return null;
            var len = fi.Length + 8192;
            if (len > (1L << 31))
                len = (1L << 31);
            using var ms = new MemoryStream((int)len);
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                await fs.CopyToAsync(ms).ConfigureAwait(false);
            return ms.GetBuffer().AsMemory(0, (int)ms.Position);
        }

        /// <summary>
        /// Read byte content of a file, allowing shared read/write with retying
        /// </summary>
        /// <param name="filename">Name of the file to read</param>
        /// <param name="retryCount">Number of times to retry the operation</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries (on error)</param>
        /// <param name="delayInMsNoExisting">Number of milli seconds to wait between any retries (when file deosn't exit)</param>
        /// <returns>Empty on error</returns>
        public static async ValueTask<ReadOnlyMemory<Byte>> TryReadBytesAsync(String filename, int retryCount = 10, int delayInMs = 100, int delayInMsNoExisting = 1)
        {
            for (; ; )
            {
                try
                {
                    var d = await ReadBytesAsync(filename).ConfigureAwait(false);
                    if (!d.IsEmpty)
                        return d;
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
