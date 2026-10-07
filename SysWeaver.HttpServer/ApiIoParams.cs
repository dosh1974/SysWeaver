using SysWeaver.Compression;
using SysWeaver.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Buffers;
using System.IO;
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


        struct FixBase64State
        {
            public readonly String Src;
            public readonly int Start;

            public FixBase64State(string src, int start)
            {
                Src = src;
                Start = start;
            }
        }


        static void WriteFixBase64(Span<Char> to, FixBase64State data)
        {
            var str = data.Src;
            var start = data.Start;
            var strLen = str.Length;
            int d = 0; 
            for (int i = start; i < strLen; ++ i)
            {
                var c = str[i];
                if (c == '-')
                    c = '+';
                if (c == '_')
                    c = '/';
                to[d] = c;
                ++d;
            }
            var len = to.Length;
            while (d < len)
            {
                to[d] = '=';
                ++d;
            }
        }

        static readonly SpanAction<Char, FixBase64State> WriteFixBase64Action = WriteFixBase64;




        /// <summary>
        /// Decode base64 or base64url (padding optional) starting at <paramref name="start"/>.
        /// </summary>
        /// <exception cref="Exception">The text isn't valid base64.</exception>
        static ReadOnlyMemory<Byte> FromText(String text, int start)
        {
            var ttl = text.Length;
            var tl = text.Length - start;
            bool needPadding = (tl & 3) != 0;
            bool needFix = false;
            if (!needPadding)
            {
                for (int i = start; i < ttl; ++i)
                {
                    var c = text[i];
                    needFix |= (c == '-');
                    needFix |= (c == '_');
                    if (needFix)
                        break;
                }
            }
            if (needPadding || needFix)
            {
                var nl = needPadding ? ((tl + 3) & ~3) : tl;
                text = String.Create(nl, new FixBase64State(text, start), WriteFixBase64Action);
                tl = nl;
                start = 0;
            }
            tl += (tl << 1);
            tl += 7;
            tl >>= 2;
            var temp = GC.AllocateUninitializedArray<Byte>(tl);
            if (!Convert.TryFromBase64Chars(text.AsSpan().Slice(start), temp.AsSpan(), out var b))
                throw new Exception("Invalid base64 data in binary API parameter");
            return new ReadOnlyMemory<Byte>(temp, 0, b);
        }

        /// <summary>
        /// Decode a binary argument: "_" + type char (see <see cref="GetDecomp"/>) + [serializer extension + ","] + base64url data.
        /// </summary>
        T GetBinary<T>(String text)
        {
#if DEBUG
            if (text.Length < 2)
                throw new Exception("Binary API parameter is missing the type char");
#endif//DEBUG
            if (!Decomp.TryGetValue(text[1], out var z))
                throw new Exception(String.Concat("Don't know how to handle binary data of type '", text[1], '\''));
            int i = 2;
            IDeserializer ser;
            if (z.Item2)
            {
                i = text.IndexOf(',', i);
                if (i < 0)
                    throw new Exception(String.Concat("Don't know how to handle binary data \"", text.LimitLength(20), '"'));
                var ext = text.FastToLower(2, i - 2);
                if (!InputSerializers.TryGetValue(ext, out ser))
                    throw new Exception(String.Concat("Can't find an enabled serializer named \"", ext, '"'));
                ++i;
            }else
            {
                ser = JsonDeSer;
                if (ser == null)
                    throw new Exception(String.Concat("Can't find an enabled json serializer"));
            }
            var data = FromText(text, i);
            var comp = z.Item1;
            if (comp != null)
            {
                using var dd = GetDecompressed(comp, data.Span);
                return ser.Create<T>(dd.Memory);

            }
            return ser.Create<T>(data);
        }

        T GetText<T>(String text, IDeserializer ser)
        {
            var ts = ser as ITextDeserializer;
#if DEBUG
            if (ts == null)
                throw new Exception(String.Concat("The \"", ser.Name, "\" isn't a ITextDeserializer which is required"));
#endif//DEBUG
            var v = ts.FromString<T>(text);
            return v;
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
            var text = Uri.UnescapeDataString(textInput);
            var c = text[0];
            if (c == '_')
                return GetBinary<T>(text);
            if (!SerMapper.TryGetValue(c, out var ser))
            {
                if (Consts.TryGetValue(text, out var val))
                {
                    ser = JsonDeSer;
                    if (ser == null)
                        throw new Exception("No enabled json deserializer");
                    text = val;
                }else
                {
                    if (!Char.IsLetter(c))
                        throw new Exception(String.Concat("Don't know how to handle API parameters \"", text, '"'));
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
            return GetText<T>(text, ser);
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
        public async Task<IUnmanagedReadOnlyMemory<Byte>> ReadRequestBodyAsync(HttpServerRequest request)
        {
            var max = MaxRequestSize;
            var len = request.ReqContentLength;
            if ((max > 0) && (len > max))
                ThrowTooLarge(max);
            using var ms = new ArrayPoolStream((int)Math.Clamp(len, 32, MaxInitialCapacity));
            var input = request.InputStream;
            if (input == null)
                return ms.GetMemory();
            var buf = ArrayPoolStream.Rent(16384);
            try
            {
                long total = 0;
                for (; ; )
                {
                    var r = await input.ReadAsync(buf.AsMemory()).ConfigureAwait(false);
                    if (r <= 0)
                        break;
                    total += r;
                    if ((max > 0) && (total > max))
                        ThrowTooLarge(max);
                    ms.Write(buf, 0, r);
                }
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
            return ms.GetMemory();
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
            var max = MaxDecompressedSize;
            using var ms = new ArrayPoolStream((int)Math.Clamp((long)data.Length << 2, 4096, MaxInitialCapacity));
            if (max <= 0)
            {
                decoder.Decompress(data, ms);
            }
            else
            {
                using var ls = new LimitedWriteStream(ms, max);
                decoder.Decompress(data, ls);
            }
            return ms.GetMemory();
        }

        /// <summary>
        /// A write only stream that forwards writes to another stream, throwing a 413 <see cref="HttpResponseException"/> if more than a given number of bytes are written.
        /// </summary>
        sealed class LimitedWriteStream : Stream
        {
            public LimitedWriteStream(Stream inner, long max)
            {
                Inner = inner;
                Max = max;
            }
            readonly Stream Inner;
            readonly long Max;
            long Written;

            void Add(int count)
            {
                var w = Written + count;
                if (w > Max)
                    ThrowTooLarge(Max);
                Written = w;
            }

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
                Add(count);
                Inner.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                Add(buffer.Length);
                Inner.Write(buffer);
            }

            public override void WriteByte(byte value)
            {
                Add(1);
                Inner.WriteByte(value);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Add(count);
                return Inner.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Add(buffer.Length);
                return Inner.WriteAsync(buffer, cancellationToken);
            }
        }

        #endregion//Size limits

    }





}
