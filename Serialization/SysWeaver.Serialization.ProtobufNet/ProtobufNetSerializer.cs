using ProtoBuf.Meta;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.ProtobufNet;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Binary serializer for the "proto" extension backed by protobuf-net, without the need for attributes.
    /// </summary>
    /// <remarks>
    /// Types are added to a private <see cref="RuntimeTypeModel"/> on first use by <see cref="SerializerBuilder"/>: all instance fields (public and non-public, including auto property backing fields) are mapped
    /// to field numbers in reflection order, and sub types get numbers (500+) in the order they are discovered.
    /// Since the numbering depends on discovery order, data should only be read by the same build of the same types, and preferably in the same process.
    /// Constructors are not called when deserializing. The <see cref="SerializerOptions"/> are ignored.
    /// </remarks>
    public sealed class ProtobufNetSerializer : ISerializerType
    {
        /// <inheritdoc/>
        public string Name => "Protobuf-Net";

        /// <inheritdoc/>
        public string Extension => "proto";

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/x-protobuf";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = MimeType;

        /// <inheritdoc/>
        public Encoding Encoding => null;

        /// <inheritdoc/>
        public int Prio => 0;

        ProtobufNetSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new ProtobufNetSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        static RuntimeTypeModel CreateModel()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            model.IncludeDateTimeKind = true;
            model.AutoAddMissingTypes = true;
            model.SkipZeroLengthPackedArrays = false;
            model.UseImplicitZeroDefaults = false;
            return model;
        }

        static readonly RuntimeTypeModel InternalSerializer = CreateModel();


        /// <inheritdoc/>
        public T Create<T>(ReadOnlySpan<byte> data)
        {
            var ser = InternalSerializer;
            SerializerBuilder.Build<T>(ser);
            return ser.Deserialize<T>(data);
        }

        /// <inheritdoc/>
        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            var ser = InternalSerializer;
            SerializerBuilder.Build<T>(ser);
            return ser.Deserialize<T>(data);
        }


        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var ser = InternalSerializer;
            SerializerBuilder.Build<T>(ser);
            using var ms = new MemoryStream();
            ser.Serialize(ms, obj);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }
    }


}
