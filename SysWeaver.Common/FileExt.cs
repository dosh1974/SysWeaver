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
        public static Task WriteToFileAsync(this ReadOnlyMemory<Byte> memory, String filename)
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

    }

}
