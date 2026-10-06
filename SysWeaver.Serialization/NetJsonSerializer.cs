using System;
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
