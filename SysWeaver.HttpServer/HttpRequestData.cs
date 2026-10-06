using System;
using System.Collections.Generic;
using System.IO;

namespace SysWeaver.Net
{
    /// <summary>
    /// The response data produced by an <see cref="IHttpRequestHandler"/>, either a stream or a block of memory, plus the objects that must be disposed when the data has been sent.
    /// </summary>
    /// <remarks>
    /// The server may replace the data while processing (decompression, compression, templates), replaced streams are kept and disposed together with the original.
    /// Not thread safe, owned by a single request.
    /// </remarks>
    public sealed class HttpRequestData : IDisposable
    {

        /// <summary>
        /// Shared empty data (no stream, empty memory), disposing it does nothing.
        /// </summary>
        public static readonly HttpRequestData Empty = new ();

        /// <summary>
        /// Create empty data.
        /// </summary>
        public HttpRequestData()
        {
        }

        /// <summary>
        /// True if there is no stream and the memory is empty.
        /// </summary>
        public bool IsEmpty => Stream == null && Mem.IsEmpty;


        /// <summary>
        /// Create data from a stream, the stream is disposed together with this instance.
        /// </summary>
        /// <param name="stream">The stream to read the response from (from the current position)</param>
        public HttpRequestData(Stream stream)
        {
            Stream = stream;
            Mem = null;
            var d = new Stack<IDisposable>();
            d.Push(stream);
            Disp = d;
        }

        /// <summary>
        /// Create data from memory.
        /// The memory may be stored in the server request cache, so it must not be modified or reused after this call (unless <paramref name="disposable"/> owns it, in which case caching it is unsafe).
        /// </summary>
        /// <param name="mem">The response data</param>
        /// <param name="disposable">Optional object that is disposed together with this instance</param>
        public HttpRequestData(ReadOnlyMemory<Byte> mem, IDisposable disposable = null)
        {
            Stream = null;
            Mem = mem;
            if (disposable == null)
                return;
            var d = new Stack<IDisposable>();
            d.Push(disposable);
            Disp = d;
        }

        /// <summary>
        /// Create data from memory.
        /// </summary>
        /// <param name="mem">The response data</param>
        /// <param name="doNotCache">If true the memory is treated like mapped memory: it's never stored in the request cache (and copied if it must be kept), use for memory that is shared or reused</param>
        /// <param name="disposable">Optional object that is disposed together with this instance</param>
        public HttpRequestData(ReadOnlyMemory<Byte> mem, bool doNotCache, IDisposable disposable = null)
        {
            Stream = null;
            Mem = mem;
            IsMapped = doNotCache;
            if (disposable == null)
                return;
            var d = new Stack<IDisposable>();
            d.Push(disposable);
            Disp = d;
        }

        /// <summary>
        /// Create data from unmanaged memory, the memory is disposed together with this instance and is never stored in the request cache.
        /// </summary>
        /// <param name="mem">The response data</param>
        public HttpRequestData(IUnmanagedReadOnlyMemory<Byte> mem)
        {
            Stream = null;
            Mem = mem.Memory;
            var d = new Stack<IDisposable>();
            d.Push(mem);
            Disp = d;
            IsMapped = true;
        }

        /// <summary>
        /// Get the memory (empty if the data is a stream).
        /// </summary>
        /// <returns>The memory</returns>
        public ReadOnlyMemory<Byte> GetMemory() => Mem;


        /// <summary>
        /// The stream to read the data from, null if the data is in <see cref="Mem"/>.
        /// </summary>
        internal Stream Stream;
        /// <summary>
        /// The data, only valid if <see cref="Stream"/> is null.
        /// </summary>
        internal ReadOnlyMemory<Byte> Mem;
        /// <summary>
        /// True if <see cref="Mem"/> must not be stored as is (shared, pooled or unmanaged memory).
        /// </summary>
        internal bool IsMapped;

        Stack<IDisposable> Disp;

        /// <summary>
        /// Replace the data with a stream (the stream is disposed together with this instance, the previous stream is still disposed).
        /// </summary>
        /// <param name="stream">The new stream, null to clear</param>
        internal void ChangeStream(Stream stream)
        {
            var old = Stream;
            Stream = stream;
            Mem = null;
            IsMapped = false;
            if (stream == null)
                return;
            var d = Disp;
            if (d == null)
            {
                d = new Stack<IDisposable>();
                Disp = d;
            }
            d.Push(stream);
        }

        /// <summary>
        /// Replace the data with memory that the caller owns (not mapped, may be cached).
        /// </summary>
        /// <param name="mem">The new data</param>
        internal void ChangeMem(ReadOnlyMemory<Byte> mem)
        {
            Mem = mem;
            Stream = null;
            IsMapped = false;
        }

        /// <summary>
        /// Dispose all owned objects (streams and disposables), in reverse order of addition.
        /// </summary>
        public void Dispose()
        {
            var d = Disp;
            if (d == null)
                return;
            while (d.Count > 0)
                d.Pop().Dispose();
        }
    }
}
