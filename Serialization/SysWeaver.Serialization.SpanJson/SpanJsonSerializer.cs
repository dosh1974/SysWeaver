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
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(Encoding.UTF8.GetBytes(new String(text)));
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T, CustomResolver>(Encoding.UTF8.GetBytes(text));
        }
    }
}
