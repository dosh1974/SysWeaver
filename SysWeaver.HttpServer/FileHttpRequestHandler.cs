using Microsoft.Win32.SafeHandles;
using SysWeaver.Compression;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A request handler that serves a single file from disc (optionally a pre-compressed copy of it).
    /// Created by <see cref="FileHttpServerModule"/> and by transformers that serve cached files.
    /// </summary>
    /// <remarks>
    /// The etag is computed from the last write time and size of the file, using the (possibly cached) state of the supplied <see cref="FileInfo"/>.
    /// Files up to 1 MiB are read into pooled memory (a snapshot, the file isn't held open while the data is sent), larger files are streamed.
    /// Instances don't hold any open file handles and can be shared between concurrent requests.
    /// </remarks>
    public sealed class FileHttpRequestHandler : IHttpRequestHandler
    {
        /// <summary>
        /// Returns the full path of the file.
        /// </summary>
        /// <returns>The full path of the file</returns>
        public override string ToString() => Fi.FullName;

        /// <summary>
        /// True if text template variable substitution may be applied, set when the mime type ends with "UTF-8" (text files).
        /// </summary>
        public bool AllowTemplates { get; init;  }


        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// The same text as the default (GetType().Name, the class is sealed) without the type name lookup (read for every request by the perf tracking of the server).
        /// </summary>
        String IHttpRequestHandler.Name => nameof(FileHttpRequestHandler);

        /// <summary>
        /// If true, the file's access time is updated whenever the file is read
        /// </summary>
        public readonly bool UpdateAccessTime;

        /// <summary>
        /// Create a handler that serves a file.
        /// </summary>
        /// <param name="mime">Mime type of the file and a flag that is true if the type is compressible (null to use the file extension)</param>
        /// <param name="fi">File information, the file should exist</param>
        /// <param name="options">Options for caching, compression and auth</param>
        /// <param name="isAccepted">True if the pre-compressed format (if any) is accepted by the client. Server side caching is only enabled if this is true or the file isn't pre-compressed</param>
        /// <param name="decoder">If the file is precompressed, this should be the decoder to use</param>
        /// <param name="updateAccessTime">If true, the file's access time is updated whenever the file is read</param>
        /// <param name="isDynamic">If true, no transformers will be applied</param>
        /// <remarks>
        /// Runtime compression and server side caching are only enabled for compressible mime types (or if <see cref="RequestOptions.ForceCache"/> is set),
        /// and server side caching only for files smaller than <see cref="RequestOptions.MaxCacheSize"/>.
        /// </remarks>
        public FileHttpRequestHandler(Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder = null, bool updateAccessTime = false, bool isDynamic = false)
        {
            if (mime == null)
                mime = MimeTypeMap.GetMimeType(fi.Extension);
            IsDynamic = isDynamic;
            UpdateAccessTime = updateAccessTime;
            Fi = fi;
            Decoder = decoder;
            if (mime.Item2 || options.ForceCache)
            {
                E = Encoding.UTF8;
                Compression = options.Compression;
                bool cache = (fi.Length < options.MaxCacheSize) && (isAccepted || (decoder == null));
                if (cache)
                    RequestCacheDuration = options.RequestCacheDuration;
            }
            AllowTemplates = mime.Item1.FastEndsWith("UTF-8");
            ClientCacheDuration = options.ClientCacheDuration;
            Mime = mime.Item1;
            Auth = options.Auth;
            IsLocalized = options.IsLocalized;
        }

        /// <summary>
        /// The text encoding (UTF-8) for compressible (text) files, else null.
        /// </summary>
        public readonly Encoding E;
        /// <summary>
        /// The mime type of the response.
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// The file being served (may be a pre-compressed variant of the requested file).
        /// </summary>
        public readonly FileInfo Fi;

        /// <inheritdoc/>
        public int ClientCacheDuration { get; init; }
        /// <inheritdoc/>
        public int RequestCacheDuration { get; init; }

        /// <inheritdoc/>
        public bool IsLocalized { get; init; }
        /// <inheritdoc/>
        public bool IsDynamic { get; init; }

        /// <inheritdoc/>
        public HttpCompressionPriority Compression { get; init; }

        /// <inheritdoc/>
        public ICompDecoder Decoder { get; init; }

        /// <inheritdoc/>
        public IReadOnlyList<String> Auth { get; init; }

        /// <summary>
        /// Returns null, so the request url is used as the server cache key.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>A completed task with a null result</returns>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        /// <summary>
        /// Sets the response mime type and returns an etag based on the last write time and size of the file.
        /// </summary>
        /// <param name="useAsync">Always false (<see cref="Get(HttpServerRequest)"/> is used)</param>
        /// <param name="request">The request</param>
        /// <returns>The etag</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            request.SetResMime(Mime);
            var fi = Fi;
            //  Same as String.Concat(HttpServerTools.ToEtag(fi.LastWriteTimeUtc), ' ', HttpServerTools.ToEtag(fi.Length)) using a single allocation
            //  (LastWriteTimeUtc is always UTC, so ToEtag doesn't convert it)
            var time = (UInt64)fi.LastWriteTimeUtc.Ticks;
            var length = (UInt64)fi.Length;
            var e = CompactAsciiString.Secure;
            Span<Char> temp = stackalloc Char[CompactAsciiString.MaxEncodedLength + 1 + CompactAsciiString.MaxEncodedLength];
            e.TryEncode(time, temp, out var l);
            temp[l] = ' ';
            ++l;
            e.TryEncode(length, temp.Slice(l), out var l2);
            return new String(temp.Slice(0, l + l2));
        }



        /// <summary>
        /// Files up to this size are read into pooled memory (a handle and the buffer holder instead of a stream and a mapping: fewer objects and system calls, faster at all sizes up to 1 MiB, measured 2026-10-14).
        /// </summary>
        const int MaxReadSize = 1 << 20;

        /// <summary>
        /// Files up to this size are memory mapped (on Windows, read into pooled memory on other platforms) if they can't be read (byte range locks), larger files are streamed.
        /// </summary>
        const int MaxMapSize = 1 << 20;

        /// <summary>
        /// Open the file (updating the access time if <see cref="UpdateAccessTime"/> is true).
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The file data as memory if 1 MiB or smaller (read into pooled memory, memory mapped on Windows if a byte range lock prevents the read), else as a stream</returns>
        /// <exception cref="IOException">The file could not be opened (deleted, locked etc)</exception>
        public HttpRequestData Get(HttpServerRequest request)
        {
            var fi = Fi;
            if (UpdateAccessTime)
            {
                try
                {
                    fi.LastAccessTimeUtc = DateTime.UtcNow;
                }
                catch
                {
                }
            }
            //  Same mode, access, share and options (so the same exceptions) as fi.OpenRead()
            var h = File.OpenHandle(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                var l = RandomAccess.GetLength(h);
                if (l > MaxReadSize)
                {
                    //  Larger files are streamed using a light weight stream that owns the handle (one object instead of a FileStream and its strategies, faster reads and fewer allocations than a FileStream or a mapping, measured 2026-10-14)
                    var s = new ReadOnlyFileHandleStream(h, l, fi.FullName);
                    h = null;
                    return new HttpRequestData(s);
                }
                var data = TryRead(h, (int)l);
                if (data != null)
                    return data;
                //  The same stream as fi.OpenRead() (owns the handle), positioned at the start (a failed read may have moved the file pointer)
                var fs = new FileStream(h, FileAccess.Read, 4096);
                h = null;
                try
                {
                    fs.Position = 0;
                    if (FileReadOnlyMemory.TryMap(out var mem, fs, true, MaxMapSize))
                        return new HttpRequestData(mem);
                }
                catch
                {
                    //  TryMap doesn't dispose the stream (leaveOpen) when the mapping fails, don't keep the file open until the finalizer runs (modpass-3)
                    fs.Dispose();
                    throw;
                }
                return new HttpRequestData(fs);
            }
            finally
            {
                h?.Dispose();
            }
        }

        /// <summary>
        /// Read a small file into pooled memory (stops early at the end of the file if it shrunk).
        /// </summary>
        /// <param name="h">The file handle (not disposed)</param>
        /// <param name="size">The length of the file</param>
        /// <returns>The data (flagged as mapped: never cached, the buffer is returned to the pool when the data is disposed) or null if the file can't be read (ex: a byte range lock, the caller maps the file like before, a mapped view ignores byte range locks)</returns>
        static HttpRequestData TryRead(SafeFileHandle h, int size)
        {
            if (size <= 0)
                return new HttpRequestData(UnmanagedMemory.Empty<Byte>());
            var buf = ArrayPool<Byte>.Shared.Rent(size);
            try
            {
                var read = 0;
                do
                {
                    var r = RandomAccess.Read(h, buf.AsSpan(read, size - read), read);
                    if (r <= 0)
                        break;
                    read += r;
                }
                while (read < size);
                if (read <= 0)
                    return new HttpRequestData(UnmanagedMemory.Empty<Byte>());
                var ret = new HttpRequestData(new PooledFileMemory(buf, read));
                buf = null;
                return ret;
            }
            catch (IOException)
            {
                return null;
            }
            finally
            {
                if (buf != null)
                    ArrayPool<Byte>.Shared.Return(buf);
            }
        }

        /// <summary>
        /// A copy of a small file in a buffer rented from the shared ArrayPool (the same contract as the pooled memory of <see cref="FileReadOnlyMemory"/>).
        /// Disposing it returns the buffer to the pool (thread safe, only the first call has an effect).
        /// </summary>
        sealed class PooledFileMemory : IUnmanagedReadOnlyMemory<Byte>
        {
            public PooledFileMemory(Byte[] buffer, int length)
            {
                Buffer = buffer;
                Memory = new ReadOnlyMemory<Byte>(buffer, 0, length);
            }

            Byte[] Buffer;

            /// <summary>
            /// The data (must not be used after the object is disposed)
            /// </summary>
            public ReadOnlyMemory<Byte> Memory { get; }

            /// <summary>
            /// Return the buffer to the pool (only the first call has an effect)
            /// </summary>
            public void Dispose()
            {
                var b = Interlocked.Exchange(ref Buffer, null);
                if (b != null)
                    ArrayPool<Byte>.Shared.Return(b);
            }
        }

        /// <summary>
        /// A read only, seekable and unbuffered stream over a file handle that it owns (one object instead of a FileStream and its two strategy objects, no finalizer).
        /// Reads use RandomAccess at the stream position. Async reads complete synchronously: a FileStream over a handle that isn't opened for async IO does the same blocking read, on a thread pool thread.
        /// </summary>
        /// <remarks>
        /// The length is fixed: the file is opened with FileShare.Read, so it can't be written while the handle is open.
        /// Not thread safe (like a FileStream).
        /// </remarks>
        sealed class ReadOnlyFileHandleStream : Stream
        {
            public ReadOnlyFileHandleStream(SafeFileHandle handle, long length, String path)
            {
                H = handle;
                Len = length;
                FilePath = path;
            }

            SafeFileHandle H;
            readonly long Len;
            long Pos;

            /// <summary>
            /// The full path of the file (the same as the path of the handle, used in exception messages)
            /// </summary>
            readonly String FilePath;

            /// <summary>
            /// The last task returned by ReadAsync(Byte[], ..), reused if the next read returns the same count (like a FileStream does)
            /// </summary>
            Task<int> LastReadTask;

            /// <summary>
            /// The handle, throws the same exception as a disposed FileStream if disposed
            /// </summary>
            SafeFileHandle Handle => H ?? throw new ObjectDisposedException(null, "Cannot access a closed file.");

            /// <summary>
            /// The handle, throws the same exception as the members of a disposed FileStream that use the stream checks (Read(Span), ReadByte, CopyTo) if disposed
            /// </summary>
            SafeFileHandle StreamHandle => H ?? throw new ObjectDisposedException(null, "Cannot access a closed Stream.");

            public override bool CanRead => H != null;
            public override bool CanSeek => H != null;
            public override bool CanWrite => false;

            public override long Length
            {
                get
                {
                    _ = Handle;
                    return Len;
                }
            }

            public override long Position
            {
                get
                {
                    _ = Handle;
                    return Pos;
                }
                set
                {
                    if (value < 0)
                        throw new ArgumentOutOfRangeException(nameof(value), "Non-negative number required.");
                    _ = Handle;
                    Pos = value;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if ((origin < SeekOrigin.Begin) || (origin > SeekOrigin.End))
                    throw new ArgumentException("Invalid seek origin.", nameof(origin));
                _ = Handle;
                var p = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Pos + offset,
                    _ => Len + offset,
                };
                if (p < 0)
                    throw InvalidArgument();
                Pos = p;
                return p;
            }

            /// <summary>
            /// The exception that a FileStream throws for a seek before the start of the file (the system message of ERROR_INVALID_PARAMETER / EINVAL and the path)
            /// </summary>
            IOException InvalidArgument()
            {
                if (OperatingSystem.IsWindows())
                    return new IOException(String.Concat(Marshal.GetPInvokeErrorMessage(87), " : '", FilePath, "'."), unchecked((int)0x80070057));
                return new IOException(String.Concat(Marshal.GetPInvokeErrorMessage(22), " : '", FilePath, "'"), 22);
            }

            /// <summary>
            /// Read at the current position and advance it
            /// </summary>
            int ReadCore(SafeFileHandle h, Span<Byte> buffer)
            {
                if (buffer.Length <= 0)
                    return 0;
                var n = RandomAccess.Read(h, buffer, Pos);
                Pos += n;
                return n;
            }

            public override int Read(Span<Byte> buffer) => ReadCore(StreamHandle, buffer);

            public override int Read(Byte[] buffer, int offset, int count)
            {
                ValidateBufferArguments(buffer, offset, count);
                return ReadCore(Handle, new Span<Byte>(buffer, offset, count));
            }

            public override int ReadByte()
            {
                Span<Byte> b = stackalloc Byte[1];
                return ReadCore(StreamHandle, b) > 0 ? b[0] : -1;
            }

            public override ValueTask<int> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                    return ValueTask.FromCanceled<int>(cancellationToken);
                var h = Handle;
                try
                {
                    return new ValueTask<int>(ReadCore(h, buffer.Span));
                }
                catch (Exception ex)
                {
                    return ValueTask.FromException<int>(ex);
                }
            }

            public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ValidateBufferArguments(buffer, offset, count);
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled<int>(cancellationToken);
                var h = Handle;
                try
                {
                    var n = ReadCore(h, new Span<Byte>(buffer, offset, count));
                    var t = LastReadTask;
                    if ((t != null) && (t.Result == n))
                        return t;
                    t = Task.FromResult(n);
                    LastReadTask = t;
                    return t;
                }
                catch (Exception ex)
                {
                    return Task.FromException<int>(ex);
                }
            }

            public override void CopyTo(Stream destination, int bufferSize)
            {
                ValidateCopyToArguments(destination, bufferSize);
                var h = StreamHandle;
                var buf = ArrayPool<Byte>.Shared.Rent(bufferSize);
                try
                {
                    for (; ; )
                    {
                        var n = RandomAccess.Read(h, buf, Pos);
                        if (n <= 0)
                            break;
                        Pos += n;
                        destination.Write(buf, 0, n);
                    }
                }
                finally
                {
                    ArrayPool<Byte>.Shared.Return(buf);
                }
            }

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                ValidateCopyToArguments(destination, bufferSize);
                var h = StreamHandle;
                if (cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled(cancellationToken);
                return CopyToAsyncInternal(h, destination, bufferSize, cancellationToken);
            }

            async Task CopyToAsyncInternal(SafeFileHandle h, Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                var buf = ArrayPool<Byte>.Shared.Rent(bufferSize);
                try
                {
                    for (; ; )
                    {
                        //  Canceled like a FileStream read (a TaskCanceledException), allocates only when canceled
                        if (cancellationToken.IsCancellationRequested)
                            await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
                        var n = RandomAccess.Read(h, buf, Pos);
                        if (n <= 0)
                            break;
                        Pos += n;
                        await destination.WriteAsync(new ReadOnlyMemory<Byte>(buf, 0, n), cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    ArrayPool<Byte>.Shared.Return(buf);
                }
            }

            public override void Flush()
            {
                _ = Handle;
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                _ = Handle;
                return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
            }

            public override void SetLength(long value)
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Non-negative number required.");
                _ = Handle;
                throw new NotSupportedException("Stream does not support writing.");
            }

            public override void Write(Byte[] buffer, int offset, int count)
            {
                ValidateBufferArguments(buffer, offset, count);
                _ = Handle;
                throw new NotSupportedException("Stream does not support writing.");
            }

            public override void Write(ReadOnlySpan<Byte> buffer)
            {
                _ = StreamHandle;
                throw new NotSupportedException("Stream does not support writing.");
            }

            public override void WriteByte(Byte value)
            {
                _ = StreamHandle;
                throw new NotSupportedException("Stream does not support writing.");
            }

            protected override void Dispose(bool disposing)
            {
                Interlocked.Exchange(ref H, null)?.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Not supported, <see cref="Get(HttpServerRequest)"/> is always used.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always thrown</exception>
        public async ValueTask<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

    }



    /// <summary>
    /// A request handler that produces the response body asynchronously using a delegate (ex: generated images).
    /// </summary>
    /// <remarks>
    /// No etag is returned, so 304 responses, templates and transformers are never used.
    /// Server side caching (keyed on the request url) is used if <see cref="RequestOptions.RequestCacheDuration"/> is non-zero.
    /// </remarks>
    public sealed class DynamicDataHttpRequestHandler : IHttpRequestHandler
    {

        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// The same text as the default (GetType().Name, the class is sealed) without the type name lookup (read for every request by the perf tracking of the server).
        /// </summary>
        String IHttpRequestHandler.Name => nameof(DynamicDataHttpRequestHandler);


        /// <summary>
        /// Create a handler that gets the response body from a delegate.
        /// </summary>
        /// <param name="mime">The mime type of the response (only Item1 is used)</param>
        /// <param name="getBody">Called (possibly concurrently) to get the response body for a request</param>
        /// <param name="options">Options for caching, compression, auth and localization</param>
        public DynamicDataHttpRequestHandler(Tuple<String, bool> mime, Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> getBody, RequestOptions options)
        {
            RequestCacheDuration = options.RequestCacheDuration;
            ClientCacheDuration = options.ClientCacheDuration;
            Compression = options.Compression;
            Mime = mime.Item1;
            Auth = options.Auth;
            GetBody = getBody;
            IsLocalized = options.IsLocalized;
        }

        readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> GetBody;


        readonly String Mime;

        /// <inheritdoc/>
        public int ClientCacheDuration { get; init; }
        /// <inheritdoc/>
        public int RequestCacheDuration { get; init; }

        /// <inheritdoc/>
        public bool IsLocalized { get; init; }

        /// <inheritdoc/>
        public HttpCompressionPriority Compression { get; init; }

        /// <summary>
        /// The decoder of the data returned by the delegate if it's pre-compressed, null by default.
        /// </summary>
        public ICompDecoder Decoder { get; init; }

        /// <inheritdoc/>
        public IReadOnlyList<String> Auth { get; init; }

        /// <summary>
        /// Returns null, so the request url is used as the server cache key.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>A completed task with a null result</returns>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        /// <summary>
        /// Sets the response mime type, returns no etag and selects <see cref="GetAsync(HttpServerRequest)"/>.
        /// </summary>
        /// <param name="useAsync">Always true</param>
        /// <param name="request">The request</param>
        /// <returns>Always null</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            useAsync = true;
            return null;
        }

        /// <summary>
        /// Not supported, <see cref="GetAsync(HttpServerRequest)"/> is always used.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always thrown</exception>
        public HttpRequestData Get(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invokes the body delegate.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The response body</returns>
        /// <remarks>A body that is already available completes without an async state machine or a task, anything else (pending, faulted, canceled or a synchronous throw) wraps the same task as an async method.</remarks>
        public ValueTask<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            Task<ReadOnlyMemory<Byte>> t;
            try
            {
                t = GetBody(request);
            }
            catch (Exception ex)
            {
                return new ValueTask<HttpRequestData>(GetAsyncThrow(ExceptionDispatchInfo.Capture(ex)));
            }
            if ((t != null) && t.IsCompletedSuccessfully)
                return new ValueTask<HttpRequestData>(new HttpRequestData(t.Result));
            return new ValueTask<HttpRequestData>(GetAsyncAwait(t));
        }

        static async Task<HttpRequestData> GetAsyncAwait(Task<ReadOnlyMemory<Byte>> t)
            => new HttpRequestData(await t.ConfigureAwait(false));

#pragma warning disable CS1998 // Async method lacks 'await' operators (the async method builder creates the task)
        static async Task<HttpRequestData> GetAsyncThrow(ExceptionDispatchInfo ex)
        {
            ex.Throw();
            return null;
        }
#pragma warning restore CS1998

    }

}
