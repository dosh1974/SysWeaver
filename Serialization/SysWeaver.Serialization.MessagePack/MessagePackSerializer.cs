using System;
using System.IO;
using System.Text;


namespace SysWeaver.Serialization
{
    public sealed class MessagePackSerializer : ISerializerType
    {
        public string Name => "MessagePack";

        public string Extension => "msgpack";

        public const String MimeType = "application/x-msgpack";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = MimeType;

        public Encoding Encoding => null;

        public int Prio => 0;

        MessagePackSerializer()
        {
        }

        sealed class Opt : MessagePack.MessagePackSerializerOptions
        {
            public Opt() : base(MessagePack.Resolvers.ContractlessStandardResolver.Instance)
            {
            }
        }

        static readonly Opt Options = new Opt();

        public static readonly ISerializerType Instance = new MessagePackSerializer();
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
                return MessagePack.MessagePackSerializer.Deserialize<T>(ms, Options);
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return MessagePack.MessagePackSerializer.Deserialize<T>(ms, Options);
            }
        }

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return MessagePack.MessagePackSerializer.Serialize<T>(obj, Options);
        }
    }
}
