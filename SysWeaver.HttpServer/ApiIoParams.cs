using SysWeaver.Compression;
using SysWeaver.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// The serializers available to API end points, and the logic for choosing an output serializer from an Accept header
    /// and for decoding API arguments passed in the query string.
    /// </summary>
    /// <remarks>
    /// One instance is shared by all <see cref="ApiHttpEntry"/> instances of an <see cref="ApiHttpServerModule"/>.
    /// Thread safe.
    /// </remarks>
    public sealed class ApiIoParams
    {

        const String JsonMime = "application/json";
        const String XmlMime = "application/xml";
        const string UrlMime = "application/x-www-form-urlencoded";

        /// <summary>
        /// The json serializer (used to deep copy API results before they are translated, so the original instance isn't modified), null if json isn't registered.
        /// </summary>
        public readonly ISerializerType CopySerializer = SerManager.Get("json");


        /// <summary>
        /// Valid output serializers, keyed by mime type, mime header value and file extension (as reported by the serializer).
        /// </summary>
        public readonly IReadOnlyDictionary<String, ISerializer> OutputSerializers;

        /// <summary>
        /// Valid input serializers, keyed by mime type, mime header value and file extension (as reported by the serializer).
        /// </summary>
        public readonly IReadOnlyDictionary<String, IDeserializer> InputSerializers;

        /// <summary>
        /// The serializer used for output when the Accept header is missing or doesn't match any output serializer.
        /// </summary>
        public readonly ISerializer DefaultOutput;

        /// <summary>
        /// The deserializer used for a request body that has no Content-Type header.
        /// </summary>
        public readonly IDeserializer DefaultInput;

        /// <summary>
        /// The enabled json deserializer, null if json input isn't enabled.
        /// </summary>
        public readonly IDeserializer JsonDeSer;

        /// <summary>
        /// The enabled "application/x-www-form-urlencoded" deserializer, null if not enabled.
        /// </summary>
        public readonly IDeserializer UriDeSer;

        /// <summary>
        /// Maps the first character of a query string argument to the deserializer to use (json for json-like starts, xml for '&lt;').
        /// A null value means the format is recognized but its deserializer isn't enabled.
        /// </summary>
        public readonly IReadOnlyDictionary<Char, IDeserializer> SerMapper;

        /// <summary>
        /// The default value of <see cref="MaxRequestSize"/> (64 MB).
        /// </summary>
        public const long DefaultMaxRequestSize = 64L << 20;

        /// <summary>
        /// The default value of <see cref="MaxDecompressedSize"/> (64 MB).
        /// </summary>
        public const long DefaultMaxDecompressedSize = 64L << 20;

        /// <summary>
        /// The maximum size in bytes of a request body (before any Content-Encoding decompression), larger bodies are rejected with a 413 response.
        /// Zero or negative means no limit.
        /// </summary>
        public long MaxRequestSize { get; init; } = DefaultMaxRequestSize;

        /// <summary>
        /// The maximum size in bytes of a request body after Content-Encoding decompression, larger bodies are rejected with a 413 response.
        /// Zero or negative means no limit.
        /// </summary>
        public long MaxDecompressedSize { get; init; } = DefaultMaxDecompressedSize;


        /// <summary>
        /// Cache of raw Accept header value -> serializer (keyed by client supplied header values, limited to <see cref="MaxCachedAccept"/> entries).
        /// </summary>
        readonly ConcurrentDictionary<String, ISerializer> Ac = new ConcurrentDictionary<string, ISerializer>(StringComparer.Ordinal);

        /// <summary>
        /// Get the output serializer to use for an Accept header value.
        /// </summary>
        /// <param name="accept">The raw Accept header value, can be null.</param>
        /// <returns>The first enabled serializer in the order listed by the header (quality values are ignored), or <see cref="DefaultOutput"/>.</returns>
        /// <remarks>Results are cached per distinct header value (ordinal), up to <see cref="MaxCachedAccept"/> distinct values (the cache is never pruned).</remarks>
        public ISerializer GetSerializer(String accept)
        {
            if (accept == null)
                return DefaultOutput;
            var c = Ac;
            if (c.TryGetValue(accept, out var s))
                return s;
            var a = accept.FastToLower();
            var o = OutputSerializers;
            foreach (var x in a.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var k = x.SplitFirst(';');
                if (o.TryGetValue(k, out s))
                    break;
            }
            s = s ?? DefaultOutput;
            //  The header values are client supplied, so limit the cache size
            if (c.Count < MaxCachedAccept)
                c.TryAdd(accept, s);
            return s;
        }

        /// <summary>
        /// The maximum number of distinct Accept header values to cache.
        /// </summary>
        const int MaxCachedAccept = 1024;

        /// <summary>
        /// Build a lookup keyed by mime, mime header and extension (the first serializer wins for an extension, the last for a mime).
        /// </summary>
        static IReadOnlyDictionary<String, T> Get<T>(IReadOnlyList<T> l) where T : ISerializerInfo
        {
            var t = new Dictionary<String, T>(StringComparer.Ordinal);
            foreach (var s in l)
            {
                var mime = s.Mime;
                t[mime] = s;
                t[s.MimeHeader] = s;
                t.TryAdd(s.Extension, s);
            }
            return t.Freeze();

        }

        /// <summary>
        /// Create the API IO parameters.
        /// </summary>
        /// <param name="inputSerializers">Enabled input deserializers.</param>
        /// <param name="outputSerializers">Enabled output serializers.</param>
        /// <param name="defaultInput">Default deserializer, null to use json if enabled, else the first input deserializer.</param>
        /// <param name="defaultOutput">Default serializer, null to use json if enabled, else the first output serializer.</param>
        public ApiIoParams(
            IReadOnlyList<IDeserializer> inputSerializers,
            IReadOnlyList<ISerializer> outputSerializers,
            IDeserializer defaultInput = null,
            ISerializer defaultOutput = null)
            
        {
            OutputSerializers = Get(outputSerializers);
            if (defaultOutput == null)
                if (!OutputSerializers.TryGetValue(JsonMime, out defaultOutput))
                    defaultOutput = outputSerializers.FirstOrDefault();
            DefaultOutput = defaultOutput;

            var t = Get(inputSerializers);
            InputSerializers = t;
            t.TryGetValue(JsonMime, out JsonDeSer);
            t.TryGetValue(UrlMime, out UriDeSer);
            var serMapper = new Dictionary<Char, IDeserializer>();
            foreach (var x in Formats)
            {
                var mime = x.Value;
                t.TryGetValue(mime, out var ser);
                serMapper.Add(x.Key, ser);
            }
            SerMapper = serMapper.Freeze();
            DefaultInput = defaultInput ?? JsonDeSer ?? t.FirstOrDefault().Value;
        }


        /// <summary>
        /// Binary argument type characters: lower case = json payload, upper case = payload serializer named explicitly;
        /// d = deflate, g = gzip, b = brotli, z = zstd (if available), u = uncompressed.
        /// </summary>
        static IReadOnlyDictionary<Char, ValueTuple<ICompType, bool>> GetDecomp()
        {
            Dictionary<Char, ValueTuple<ICompType, bool>> decomp = new Dictionary<char, ValueTuple<ICompType, bool>>();
            void Add(Char c, String http)
            {
                var s = CompManager.GetFromHttp(http);
                if (s == null)
                    return;
                decomp.Add(c, ValueTuple.Create(s, false));
                decomp.Add(Char.ToUpper(c), ValueTuple.Create(s, true));
            }
            Add('d', "deflate");
            Add('g', "gzip");
            Add('b', "br");
            Add('z', "zstd");
            decomp.Add('u', new ValueTuple<ICompType, bool>(null, false));
            decomp.Add('U', new ValueTuple<ICompType, bool>(null, true));
            return decomp.Freeze();
        }

        static readonly IReadOnlyDictionary<Char, ValueTuple<ICompType, bool>> Decomp = GetDecomp();

        static readonly IReadOnlyDictionary<Char, String> Formats = new Dictionary<Char, String>()
        {
            { '{', JsonMime },
            { '[', JsonMime },
            { '"', JsonMime },
            { '\'', JsonMime },
            { '-', JsonMime },
            { '+', JsonMime },
            { '.', JsonMime },
            { '0', JsonMime },
            { '1', JsonMime },
            { '2', JsonMime },
            { '3', JsonMime },
            { '4', JsonMime },
            { '5', JsonMime },
            { '6', JsonMime },
            { '7', JsonMime },
            { '8', JsonMime },
            { '9', JsonMime },
            { '<', XmlMime },
        }.Freeze();


        static readonly IReadOnlyDictionary<String, String> Consts = new Dictionary<String, String>(StringComparer.Ordinal)
        {
            { "null", "null" },
            { "nan", "0" },
            { "undefined", "null" },
            { "true", "true" },
            { "false", "false" },

        }.Freeze();


        /// <summary>
        /// The maximum number of base64 chars to fix up (base64url to base64 and padding) on the stack (4 KB), longer arguments use a pooled buffer.
        /// </summary>
        const int MaxStackBase64 = 2048;

        static void ThrowBinaryFormat(ReadOnlySpan<Char> text)
            => throw new Exception(String.Concat("Don't know how to handle binary data \"", new String(text).LimitLength(20), '"'));

        static void ThrowNoSerializer(ReadOnlySpan<Char> ext)
            => throw new Exception(String.Concat("Can't find an enabled serializer named \"", new String(ext).FastToLower(), '"'));

        static void ThrowInvalidBase64()
            => throw new Exception("Invalid base64 data in binary API parameter");

        /// <summary>
        /// Decode a binary argument: "_" + type char (see <see cref="GetDecomp"/>) + [serializer extension + ","] + base64url data.
        /// The data is decoded from the span into a pooled buffer (strings are only created for exception messages).
        /// </summary>
        [SkipLocalsInit]
        T GetBinary<T>(ReadOnlySpan<Char> text)
        {
#if DEBUG
            if (text.Length < 2)
                throw new Exception("Binary API parameter is missing the type char");
#endif//DEBUG
            var tc = text[1];
            if (!Decomp.TryGetValue(tc, out var z))
                throw new Exception(String.Concat("Don't know how to handle binary data of type '", tc, '\''));
            int i = 2;
            IDeserializer ser;
            if (z.Item2)
            {
                i = text.Slice(2).IndexOf(',');
                if (i < 0)
                    ThrowBinaryFormat(text);
                //  The same lookup as with the lower cased extension string
                if (!InputSerializers.TryGetValueLower(text.Slice(2, i), out ser))
                    ThrowNoSerializer(text.Slice(2, i));
                i += 3;
            }else
            {
                ser = JsonDeSer;
                if (ser == null)
                    throw new Exception(String.Concat("Can't find an enabled json serializer"));
            }
            //  Base64 or base64url (padding optional): '-' => '+', '_' => '/' and '=' padding to a multiple of 4 chars,
            //  the same chars to the same decoder as before (when nothing changes the text is decoded as is)
            scoped ReadOnlySpan<Char> b64 = text.Slice(i);
            var tl = b64.Length;
            var nl = (tl + 3) & ~3;
            Char[] pooledChars = null;
            if ((nl != tl) || b64.ContainsAny('-', '_'))
            {
                Span<Char> f = nl <= MaxStackBase64 ? stackalloc Char[nl] : (pooledChars = ArrayPool<Char>.Shared.Rent(nl)).AsSpan(0, nl);
                b64.Replace(f, '-', '+');
                f.Replace('_', '/');
                f.Slice(tl).Fill('=');
                b64 = f;
            }
            //  The same (large enough) size as before
            var temp = ArrayPoolStream.Rent(((nl + (nl << 1)) + 7) >> 2);
            var ok = Convert.TryFromBase64Chars(b64, temp, out var b);
            if (pooledChars != null)
                ArrayPool<Char>.Shared.Return(pooledChars);
            if (!ok)
            {
                ArrayPoolStream.Return(temp);
                ThrowInvalidBase64();
            }
            var comp = z.Item1;
            if (comp != null)
            {
                IUnmanagedReadOnlyMemory<Byte> dd;
                try
                {
                    dd = GetDecompressed(comp, new ReadOnlySpan<Byte>(temp, 0, b));
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
                using (dd)
                    return ser.Create<T>(dd.Memory);
            }
            //  Pooled like the decompressed data (and request bodies), deserializers don't keep a reference to the data
            try
            {
                return ser.Create<T>(new ReadOnlyMemory<Byte>(temp, 0, b));
            }
            finally
            {
                ArrayPoolStream.Return(temp);
            }
        }

        /// <summary>
        /// Decode an API argument passed as text (typically the query string of a GET request).
        /// </summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="textInput">The URL encoded argument text.</param>
        /// <returns>The deserialized value, default when <paramref name="textInput"/> is empty.</returns>
        /// <remarks>
        /// The text is URL unescaped and the format is chosen by its first character:
        /// <list type="bullet">
        /// <item>'_' : compressed/binary payload, "_" + d|g|b|z|u (upper case = followed by a serializer extension and ','), then base64url data.</item>
        /// <item>'{', '[', '"', ''', '-', '+', '.', digit : json.</item>
        /// <item>'&lt;' : xml.</item>
        /// <item>"null", "undefined", "true", "false", "nan" (exact, case sensitive) : json constants ("nan" becomes 0).</item>
        /// <item>Any other letter : "application/x-www-form-urlencoded" style data.</item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">The format can't be determined or the required serializer isn't enabled.</exception>
        public T Get<T>(ReadOnlySpan<Char> textInput)
        {
            var tl = textInput.Length;
            if (tl <= 0)
                return default(T);
            //  Uri.UnescapeDataString only decodes "%XX" sequences ('+' is kept), so text without a '%' is used as is (no string is created)
            String text = null;
            var span = textInput;
            if (textInput.Contains('%'))
            {
                text = Uri.UnescapeDataString(textInput);
                span = text;
            }
            var c = span[0];
            if (c == '_')
                return GetBinary<T>(span);
            if (!SerMapper.TryGetValue(c, out var ser))
            {
                if (Consts.TryGetValueSpan(span, out var val))
                {
                    ser = JsonDeSer;
                    if (ser == null)
                        throw new Exception("No enabled json deserializer");
                    text = val;
                }else
                {
                    if (!Char.IsLetter(c))
                        throw new Exception(String.Concat("Don't know how to handle API parameters \"", span, "\""));
                    ser = UriDeSer;
                    if (ser == null)
                        throw new Exception("No enabled uri deserializer");
                }
            }
            if (ser == null)
            {
                if (Formats.TryGetValue(c, out var mime))
                    throw new Exception(String.Concat("No enabled serializer for \"", mime, '"'));
                else
                    throw new Exception(String.Concat("No serializer defined for '", c, '\''));
            }
            var ts = ser as ITextDeserializer;
#if DEBUG
            if (ts == null)
                throw new Exception(String.Concat("The \"", ser.Name, "\" isn't a ITextDeserializer which is required"));
#endif//DEBUG
            //  An existing string (unescaped or a constant) is passed as is, else the deserializer reads the span (the default FromSpan creates the string)
            return text != null ? ts.FromString<T>(text) : ts.FromSpan<T>(span);
        }


        #region Size limits

        /// <summary>
        /// The maximum initial capacity to allocate based on a client supplied Content-Length (or compressed size), the buffer grows as data arrives.
        /// </summary>
        const int MaxInitialCapacity = 64 * 1024;

        static void ThrowTooLarge(long max)
            => throw new HttpResponseException(413, String.Concat("Content Too Large - The request body exceeds the limit of ", max, " bytes [413]"));

        /// <summary>
        /// Read the entire request body into pooled memory, enforcing <see cref="MaxRequestSize"/>.
        /// </summary>
        /// <param name="request">The request to read the body of.</param>
        /// <returns>The body (not decompressed), the caller must dispose it.</returns>
        /// <exception cref="HttpResponseException">A 413 response if the Content-Length or the actual body exceeds <see cref="MaxRequestSize"/>.</exception>
        public Task<IUnmanagedReadOnlyMemory<Byte>> ReadRequestBodyAsync(HttpServerRequest request)
        {
            var t = ReadBodyAsync(request);
            return t.IsCompletedSuccessfully ? Task.FromResult(t.Result) : t.AsTask();
        }

        /// <summary>
        /// The read size when the length of the request body is unknown (or zero, some servers reports 0 for a chunked body).
        /// </summary>
        const int UnknownLengthReadSize = 16384;

        /// <summary>
        /// Read the entire request body into pooled memory, enforcing <see cref="MaxRequestSize"/> (the same as <see cref="ReadRequestBodyAsync(HttpServerRequest)"/>).
        /// The body is read straight into a pooled buffer sized from the Content-Length (no stream object and no copy), the buffer grows if the body is larger.
        /// </summary>
        /// <param name="request">The request to read the body of.</param>
        /// <returns>The body (not decompressed), the caller must dispose it. Await once.</returns>
        /// <exception cref="HttpResponseException">A 413 response if the Content-Length or the actual body exceeds <see cref="MaxRequestSize"/>.</exception>
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        internal async ValueTask<IUnmanagedReadOnlyMemory<Byte>> ReadBodyAsync(HttpServerRequest request)
        {
            var max = MaxRequestSize;
            var len = request.ReqContentLength;
            if ((max > 0) && (len > max))
                ThrowTooLarge(max);
            var input = request.InputStream;
            if (input == null)
                return UnmanagedMemory.Empty<Byte>();
            //  Room for the Content-Length + 1 (the end is detected without growing), the client supplied length is capped (the buffer grows as data arrives)
            var buf = ArrayPoolStream.Rent(len > 0 ? (int)Math.Min(len + 1, MaxInitialCapacity) : UnknownLengthReadSize);
            int total = 0;
            try
            {
                for (; ; )
                {
                    if (total == buf.Length)
                        buf = Grow(buf, total);
                    var r = await input.ReadAsync(buf.AsMemory(total)).ConfigureAwait(false);
                    if (r <= 0)
                        break;
                    total += r;
                    if ((max > 0) && (total > max))
                        ThrowTooLarge(max);
                }
            }
            catch
            {
                ArrayPoolStream.Return(buf);
                throw;
            }
            return PooledMemory.Create(buf, total);
        }

        /// <summary>
        /// Replace a full pooled buffer with one twice the size (the same growth as <see cref="ArrayPoolStream"/>).
        /// </summary>
        /// <param name="buf">The full buffer, returned to the pool.</param>
        /// <param name="length">The number of valid bytes (the buffer length).</param>
        /// <param name="minLength">The minimum size of the new buffer.</param>
        /// <returns>The new buffer (rented using <see cref="ArrayPoolStream.Rent(int)"/>) with the valid bytes copied.</returns>
        /// <exception cref="IOException">The buffer would be larger than Array.MaxLength (the same exception as <see cref="ArrayPoolStream"/>).</exception>
        static Byte[] Grow(Byte[] buf, int length, long minLength = 0)
        {
            var size = Math.Min(Math.Max(minLength, (long)buf.Length << 1), Array.MaxLength);
            if ((size <= length) || (size < minLength))
                throw new IOException("Stream too long");
            var next = ArrayPoolStream.Rent((int)size);
            new ReadOnlySpan<Byte>(buf, 0, length).CopyTo(next.AsSpan());
            ArrayPoolStream.Return(buf);
            return next;
        }

        /// <summary>
        /// Pooled memory, the buffer is returned to the pool when disposed (only once).
        /// </summary>
        sealed class PooledMemory : IUnmanagedReadOnlyMemory<Byte>
        {
            PooledMemory(Byte[] buffer, int length)
            {
                Buffer = buffer;
                Memory = new ReadOnlyMemory<Byte>(buffer, 0, length);
            }

            /// <summary>
            /// Wrap a buffer rented using <see cref="ArrayPoolStream.Rent(int)"/> (the ownership is transferred).
            /// </summary>
            /// <returns>The memory, the shared empty instance if the length is 0 (the buffer is then returned).</returns>
            public static IUnmanagedReadOnlyMemory<Byte> Create(Byte[] buffer, int length)
            {
                if (length <= 0)
                {
                    ArrayPoolStream.Return(buffer);
                    return UnmanagedMemory.Empty<Byte>();
                }
                return new PooledMemory(buffer, length);
            }

            Byte[] Buffer;

            public ReadOnlyMemory<Byte> Memory { get; }

            public void Dispose()
            {
                var b = Interlocked.Exchange(ref Buffer, null);
                if (b != null)
                    ArrayPoolStream.Return(b);
            }
        }

        /// <summary>
        /// Decompress request data into pooled memory, enforcing <see cref="MaxDecompressedSize"/> (decompression stops as soon as the limit is exceeded).
        /// </summary>
        /// <param name="decoder">The decoder to use.</param>
        /// <param name="data">The compressed data.</param>
        /// <returns>The decompressed data, the caller must dispose it.</returns>
        /// <exception cref="HttpResponseException">A 413 response if the decompressed data exceeds <see cref="MaxDecompressedSize"/>.</exception>
        public IUnmanagedReadOnlyMemory<Byte> GetDecompressed(ICompDecoder decoder, ReadOnlySpan<Byte> data)
        {
            var s = new LimitedWriteStream(ArrayPoolStream.Rent((int)Math.Clamp((long)data.Length << 2, 4096, MaxInitialCapacity)), MaxDecompressedSize);
            try
            {
                decoder.Decompress(data, s);
            }
            catch
            {
                s.Release();
                throw;
            }
            return s.GetMemory();
        }

        /// <summary>
        /// A write only stream that writes to a pooled buffer, throwing a 413 <see cref="HttpResponseException"/> if more than a given number of bytes are written.
        /// The stream is also the memory handed out (one object instead of a stream, a limiting stream and a lease).
        /// </summary>
        sealed class LimitedWriteStream : Stream, IUnmanagedReadOnlyMemory<Byte>, IDisposable
        {
            /// <param name="buffer">A buffer rented using <see cref="ArrayPoolStream.Rent(int)"/> (the ownership is transferred).</param>
            /// <param name="max">The maximum number of bytes, zero or negative means no limit.</param>
            public LimitedWriteStream(Byte[] buffer, long max)
            {
                Buffer = buffer;
                Max = max;
            }
            Byte[] Buffer;
            readonly long Max;
            int Written;

            /// <summary>
            /// The written data (set by <see cref="GetMemory"/>).
            /// </summary>
            public ReadOnlyMemory<Byte> Memory { get; private set; }

            /// <summary>
            /// Get the written data (this instance), the shared empty instance if nothing is written (the buffer is then returned).
            /// </summary>
            public IUnmanagedReadOnlyMemory<Byte> GetMemory()
            {
                var w = Written;
                if (w <= 0)
                {
                    Release();
                    return UnmanagedMemory.Empty<Byte>();
                }
                Memory = new ReadOnlyMemory<Byte>(Buffer, 0, w);
                return this;
            }

            /// <summary>
            /// Return the buffer to the pool (only once).
            /// </summary>
            public void Release()
            {
                var b = Interlocked.Exchange(ref Buffer, null);
                if (b != null)
                    ArrayPoolStream.Return(b);
            }

            /// <summary>
            /// Disposing the memory returns the buffer (disposing the stream, ex: by a decoder, doesn't).
            /// </summary>
            void IDisposable.Dispose() => Release();

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => Written;
            public override long Position { get => Written; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                ValidateBufferArguments(buffer, offset, count);
                Write(new ReadOnlySpan<Byte>(buffer, offset, count));
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                var count = buffer.Length;
                if (count <= 0)
                    return;
                var pos = Written;
                var w = (long)pos + count;
                var max = Max;
                if ((max > 0) && (w > max))
                    ThrowTooLarge(max);
                var b = Buffer;
                if (w > b.Length)
                    Buffer = b = Grow(b, pos, w);
                buffer.CopyTo(new Span<Byte>(b, pos, count));
                Written = (int)w;
            }

            public override void WriteByte(byte value)
                => Write(new ReadOnlySpan<Byte>(in value));

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Write(buffer, offset, count);
                return Task.CompletedTask;
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
        }

        #endregion//Size limits

    }





}
