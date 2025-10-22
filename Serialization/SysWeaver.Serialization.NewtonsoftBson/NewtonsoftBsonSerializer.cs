using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.NewtonsoftBson;

namespace SysWeaver.Serialization
{
    public sealed class NewtonsoftBsonSerializer : ISerializerType
    {
        public string Name => "Newtonsoft.Bson";

        public string Extension => "bson";

        public int Prio => 1;

        public const String MimeType = "application/bson";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = MimeType;

        public Encoding Encoding => null;
        
        NewtonsoftBsonSerializer()
        {
        }

        public static readonly ISerializerType Instance = new NewtonsoftBsonSerializer();
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        static readonly JsonSerializerSettings[] Formats =
        [
            new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = MemberResolver.Instance,
            },
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.All,
                ContractResolver = MemberResolver.Instance,
            },
            new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.None,
                ContractResolver = MemberResolver.Instance,
            },
        ];

        static readonly JsonSerializerSettings DeserFormats = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = Formats[(int)options];
            var ser = JsonSerializer.Create(s);
            using var ms = new MemoryStream();
            using var wr = new BsonDataWriter(ms);
            ser.Serialize(wr, obj);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            var ser = JsonSerializer.CreateDefault();
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new BsonDataReader(ms);
                return ser.Deserialize<T>(r);
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            var ser = JsonSerializer.CreateDefault();
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new BsonDataReader(ms);
                return ser.Deserialize<T>(r);
            }
        }


    }
}
