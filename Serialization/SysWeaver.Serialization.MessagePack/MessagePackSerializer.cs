using System;
using System.IO;
using System.Text;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Binary serializer for the "msgpack" extension backed by MessagePack-CSharp.
    /// </summary>
    /// <remarks>
    /// Uses the contractless standard resolver, so types do not need <c>[MessagePackObject]</c> attributes (public members are serialized by name).
    /// The <see cref="SerializerOptions"/> are ignored.
    /// </remarks>
    public sealed class MessagePackSerializer : ISerializerType
    {
        /// <inheritdoc/>
        public string Name => "MessagePack";

        /// <inheritdoc/>
        public string Extension => "msgpack";

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/x-msgpack";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = MimeType;

        /// <inheritdoc/>
        public Encoding Encoding => null;

        /// <inheritdoc/>
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

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new MessagePackSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return MessagePack.MessagePackSerializer.Deserialize<T>(ms, Options);
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return MessagePack.MessagePackSerializer.Deserialize<T>(ms, Options);
            }
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return MessagePack.MessagePackSerializer.Serialize<T>(obj, Options);
        }
    }
}
