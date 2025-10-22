using System;
using System.IO;
using System.Text;
using Utf8Json;


namespace SysWeaver.Serialization
{
    public sealed class Utf8JsonSerializer : ITextSerializerType
    {
        public string Name => "Utf8Json";

        public string Extension => "json";

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;

        public int Prio => -5;

        Utf8JsonSerializer()
        {
        }

        public static readonly ISerializerType Instance = new Utf8JsonSerializer();
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return JsonSerializer.Deserialize<T>(ms);
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return JsonSerializer.Deserialize<T>(ms);
            }
        }


        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.Serialize(obj);
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.ToJsonString(obj);
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JsonSerializer.Deserialize<T>(new String(text));
        }

        public T FromString<T>(string text)
        {
            return JsonSerializer.Deserialize<T>(text);
        }
    }
}
