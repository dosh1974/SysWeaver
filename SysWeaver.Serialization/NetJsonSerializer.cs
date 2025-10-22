using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysWeaver.Serialization
{

    public sealed class NetJsonSerializer : ITextSerializerType
    {
        public string Name => "System.Text.Json";
        public string Extension => "json";

        public int Prio => 0;

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);


        public Encoding Encoding => Encoding.UTF8;

        NetJsonSerializer()
        {
        }

        public static readonly ITextSerializerType Instance = new NetJsonSerializer();
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
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            },
            new JsonSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                WriteIndented = true,
                AllowTrailingCommas = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            },
            new JsonSerializerOptions
            {
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                IncludeFields = true,
                WriteIndented = false,
                AllowTrailingCommas = true,
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
                ReadCommentHandling = JsonCommentHandling.Skip,
            };


        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var text = JsonSerializer.Serialize<T>(obj, Options[(int)options]);
            return Encoding.GetBytes(text);
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var text = JsonSerializer.Serialize<T>(obj, Options[(int)options]);
            return text;
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JsonSerializer.Deserialize<T>(text, DeSerOptions);
        }

        public T FromString<T>(string text)
        {
            return JsonSerializer.Deserialize<T>(text, DeSerOptions);
        }

        public T Create<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data, DeSerOptions);
        }

        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data.Span, DeSerOptions);
        }



    }

}
