using System;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysWeaver.Serialization
{

    /// <summary>
    /// Serializer for the "json" extension using System.Text.Json.
    /// Always registered by <see cref="SerManager"/> (priority 0), so it is the fallback JSON implementation.
    /// </summary>
    /// <remarks>
    /// Public fields are included, read-only fields and get-only properties are ignored.
    /// NaN and Infinity are written and accepted as named literals ("NaN", "Infinity").
    /// When reading, trailing commas are allowed and comments are skipped.
    /// <see cref="SerializerOptions.Verbose"/> produces indented output, <see cref="SerializerOptions.Typeless"/> is the same as <see cref="SerializerOptions.Compact"/>; no type information is ever written.
    /// Output uses <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>, so HTML sensitive characters (&lt; &gt; &amp; ') and non-ASCII characters are NOT escaped, the output must be escaped before it is embedded in HTML.
    /// </remarks>
    public sealed class NetJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "System.Text.Json";
        /// <inheritdoc/>
        public string Extension => "json";

        /// <inheritdoc/>
        public int Prio => 0;

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/json";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);


        /// <inheritdoc/>
        public Encoding Encoding => Encoding.UTF8;

        NetJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ITextSerializerType Instance = new NetJsonSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        static readonly JsonSerializerOptions[] Options =
        [
            new JsonSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                WriteIndented = false,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            },
            new JsonSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                WriteIndented = true,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            },
            new JsonSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                WriteIndented = false,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            },
        ];

        static readonly JsonSerializerOptions DeSerOptions =
            new JsonSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };


        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.SerializeToUtf8Bytes<T>(obj, Options[(int)options]);
        }

        /// <summary>
        /// The writer options that <see cref="JsonSerializer.SerializeToUtf8Bytes{TValue}(TValue, JsonSerializerOptions)"/> uses for each of the <see cref="Options"/> (same as the internal JsonSerializerOptions.GetWriterOptions)
        /// </summary>
        static readonly JsonWriterOptions[] WriterOptions = Array.ConvertAll(Options, o => new JsonWriterOptions
        {
            Encoder = o.Encoder,
            Indented = o.WriteIndented,
            IndentCharacter = o.IndentCharacter,
            IndentSize = o.IndentSize,
            MaxDepth = o.MaxDepth == 0 ? 64 : o.MaxDepth,
            NewLine = o.NewLine,
            SkipValidation = true,
        });

        /// <summary>
        /// A writer that the cached json writers are reset to when not in use (so that they don't keep a reference to the caller's writer), nothing is written to it
        /// </summary>
        static readonly ArrayBufferWriter<Byte> NoOutput = new ArrayBufferWriter<Byte>(1);

        /// <summary>
        /// The json writers of the thread (one per <see cref="SerializerOptions"/>), an entry is null while in use
        /// </summary>
        [ThreadStatic]
        static Utf8JsonWriter[] Writers;

        /// <inheritdoc/>
        /// <remarks>
        /// The json is written directly to <paramref name="output"/> using a cached json writer (no array is allocated).
        /// Implemented explicitly, so that code finding the Serialize method of the class by name (reflection) is unaffected.
        /// </remarks>
        void ISerializer.Serialize<T>(T obj, IBufferWriter<byte> output, SerializerOptions options)
        {
            var o = Options[(int)options];
            var ws = Writers ??= new Utf8JsonWriter[Options.Length];
            var i = (int)options;
            var w = ws[i];
            if (w == null)
            {
                w = new Utf8JsonWriter(output, WriterOptions[i]);
            }
            else
            {
                ws[i] = null;
                w.Reset(output);
            }
            try
            {
                JsonSerializer.Serialize<T>(w, obj, o);
            }
            finally
            {
                w.Reset(NoOutput);
                ws[i] = w;
            }
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var text = JsonSerializer.Serialize<T>(obj, Options[(int)options]);
            return text;
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JsonSerializer.Deserialize<T>(text, DeSerOptions);
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            return JsonSerializer.Deserialize<T>(text, DeSerOptions);
        }

        /// <inheritdoc/>
        /// <remarks>The string overload of <see cref="JsonSerializer"/> deserializes the span of the string, so the result is the same as <see cref="FromString{T}(String)"/>.</remarks>
        public T FromSpan<T>(ReadOnlySpan<char> text)
        {
            return JsonSerializer.Deserialize<T>(text, DeSerOptions);
        }

        /// <inheritdoc/>
        public T Create<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data, DeSerOptions);
        }

        /// <inheritdoc/>
        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data.Span, DeSerOptions);
        }



    }

}
