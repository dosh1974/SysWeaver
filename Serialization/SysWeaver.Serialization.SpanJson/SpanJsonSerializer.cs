using System;
using System.IO;
using System.Text;
using SpanJson;
using SpanJson.Resolvers;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Serializer for the "json" extension backed by the SpanJson library.
    /// Has a low priority (-5) so it never displaces the other JSON serializers when they are registered.
    /// </summary>
    /// <remarks>
    /// Serialization and deserialization use the same custom resolver (nulls included, byte arrays as number arrays, enums as integers, original member name casing).
    /// The <see cref="SerializerOptions"/> are ignored (output is always compact).
    /// </remarks>
    public sealed class SpanJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "SpanJson";

        /// <inheritdoc/>
        public string Extension => "json";

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

        /// <inheritdoc/>
        public int Prio => -5;

        SpanJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new SpanJsonSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        sealed class CustomResolver : ResolverBase<Byte, CustomResolver>
        {
            static readonly SpanJsonOptions Options = new SpanJsonOptions
            {
                NullOption = NullOptions.IncludeNulls,
                ByteArrayOption = ByteArrayOptions.Array,
                EnumOption = EnumOptions.Integer,
                NamingConvention = NamingConventions.OriginalCase
            };

            public CustomResolver() : base(Options)
            {
            }
        }


        /// <inheritdoc/>
        public T Create<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(data);
        }

        /// <inheritdoc/>
        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(data.Span);
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.Generic.Utf8.Serialize<T, CustomResolver>(obj);
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Encoding.UTF8.GetString(JsonSerializer.Generic.Utf8.Serialize<T, CustomResolver>(obj));
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return DeserializeChars<T>(text);
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            //  Null throws as before
            if (text == null)
                return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(Encoding.UTF8.GetBytes(text));
            return DeserializeChars<T>(text);
        }

        /// <summary>
        /// The maximum number of UTF-8 bytes to encode on the stack
        /// </summary>
        const int MaxStackBytes = 4096;

        /// <summary>
        /// Deserialize some json text, the text is UTF-8 encoded on the stack if it's small (else to a new array), same bytes as Encoding.UTF8.GetBytes(new String(text))
        /// </summary>
        /// <typeparam name="T">The type to deserialize</typeparam>
        /// <param name="text">The json text</param>
        /// <returns>The deserialized object</returns>
        static T DeserializeChars<T>(ReadOnlySpan<char> text)
        {
            var enc = Encoding.UTF8;
            var n = enc.GetByteCount(text);
            if (n > MaxStackBytes)
            {
                var a = new Byte[n];
                enc.GetBytes(text, a);
                return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(a);
            }
            Span<Byte> b = stackalloc Byte[n];
            enc.GetBytes(text, b);
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(b);
        }
    }
}
