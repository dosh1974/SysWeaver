using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SysWeaver.Serialization.SwJson.Writer
{
    public static class ByteBufferCache
    {
        /// <summary>
        /// Returns a byte buffer or null
        /// </summary>
        /// <returns>A buffer or null</returns>
        public static Byte[] GetTempBuffer()
        {
            if (!TempBuffers.TryPop(out var b))
                return null;
            Interlocked.Decrement(ref TempBufferCount);
            return b;
        }

        /// <summary>
        /// Return a buffer that isn't used anymore
        /// </summary>
        /// <param name="buffer">Buffer</param>
        public static void ReturnTempBuffer(Byte[] buffer)
        {
            if (buffer == null)
                return;
            if (buffer.Length > 65536)
                return;
            if (Interlocked.Read(ref TempBufferCount) > 1024)
                return;
            Interlocked.Increment(ref TempBufferCount);
            TempBuffers.Push(buffer);
        }

        static readonly ConcurrentStack<Byte[]> TempBuffers = new ConcurrentStack<byte[]>();
        static long TempBufferCount;
    }


}
