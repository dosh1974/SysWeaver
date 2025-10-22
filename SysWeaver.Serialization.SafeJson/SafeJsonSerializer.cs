using System;
using System.Text;

namespace SysWeaver.Serialization
{
    public sealed class SafeJsonSerializer : ITextSerializerType
    {
        SafeJsonSerializer()
        {
        }

        public static ITextSerializerType Instance = new SafeJsonSerializer();

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        public string Name => "Safe Json";

        public string Extension => "json";

        public int Prio => 10;

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;

        readonly ITextSerializerType DeSerializer = NewtonsoftJsonSerializer.Instance;
        readonly ITextSerializerType Serializer = SysWeaverJsonSerializer.Instance;

        public T Create<T>(ReadOnlyMemory<byte> data) => DeSerializer.Create<T>(data);
        public T Create<T>(ReadOnlySpan<byte> data) => DeSerializer.Create<T>(data);

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact) 
            => options == SerializerOptions.Verbose ? DeSerializer.Serialize(obj, options) : Serializer.Serialize(obj, options);

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
            => options == SerializerOptions.Verbose ? DeSerializer.ToString(obj, options) : Serializer.ToString(obj, options);

        public T FromString<T>(ReadOnlySpan<char> text) => DeSerializer.FromString<T>(text);

        public T FromString<T>(string text) => DeSerializer.FromString<T>(text);

    }

}
