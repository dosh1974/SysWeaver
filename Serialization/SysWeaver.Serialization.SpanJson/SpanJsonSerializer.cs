using System;
using System.IO;
using System.Text;
using SpanJson;
using SpanJson.Resolvers;


namespace SysWeaver.Serialization
{
    public sealed class SpanJsonSerializer : ITextSerializerType
    {
        public string Name => "SpanJson";

        public string Extension => "json";

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;

        public int Prio => -5;

        SpanJsonSerializer()
        {
        }

        public static readonly ISerializerType Instance = new SpanJsonSerializer();
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


        public T Create<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T>(data);
        }

        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T>(data.Span);
        }

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.Generic.Utf8.Serialize<T, CustomResolver>(obj);
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Encoding.UTF8.GetString(JsonSerializer.Generic.Utf8.Serialize<T, CustomResolver>(obj));
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T>(Encoding.UTF8.GetBytes(new String(text)));
        }

        public T FromString<T>(string text)
        {
            return JsonSerializer.Generic.Utf8.Deserialize<T>(Encoding.UTF8.GetBytes(text));
        }
    }
}
