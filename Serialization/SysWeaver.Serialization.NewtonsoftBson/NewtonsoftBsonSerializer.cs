using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.NewtonsoftBson;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Binary serializer for the "bson" extension backed by Newtonsoft.Json.Bson.
    /// </summary>
    /// <remarks>
    /// Serialization uses <see cref="NewtonsoftBson.MemberResolver"/> (read-only members are not written) and writes "$type" information
    /// (only where needed for <see cref="SerializerOptions.Compact"/>, everywhere for <see cref="SerializerOptions.Verbose"/>, never for <see cref="SerializerOptions.Typeless"/>).
    /// Deserialization uses <see cref="JsonSerializer.CreateDefault()"/> (global default settings, no type name handling), so polymorphic members are not restored from the written type information.
    /// BSON requires an object or array at the root: primitive root values can not be serialized, and root arrays/collections can not be read back since the reader does not enable ReadRootValueAsArray.
    /// </remarks>
    public sealed class NewtonsoftBsonSerializer : ISerializerType
    {
        /// <inheritdoc/>
        public string Name => "Newtonsoft.Bson";

        /// <inheritdoc/>
        public string Extension => "bson";

        /// <inheritdoc/>
        public int Prio => 1;

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/bson";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = MimeType;

        /// <inheritdoc/>
        public Encoding Encoding => null;
        
        NewtonsoftBsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new NewtonsoftBsonSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
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

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = Formats[(int)options];
            var ser = JsonSerializer.Create(s);
            using var ms = new MemoryStream();
            using var wr = new BsonDataWriter(ms);
            ser.Serialize(wr, obj);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
