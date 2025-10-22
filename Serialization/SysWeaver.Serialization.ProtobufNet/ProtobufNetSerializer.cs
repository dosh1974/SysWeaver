using ProtoBuf.Meta;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.ProtobufNet;


namespace SysWeaver.Serialization
{
    public sealed class ProtobufNetSerializer : ISerializerType
    {
        public string Name => "Protobuf-Net";

        public string Extension => "proto";

        public const String MimeType = "application/x-protobuf";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = MimeType;

        public Encoding Encoding => null;

        public int Prio => 0;

        ProtobufNetSerializer()
        {
        }

        public static readonly ISerializerType Instance = new ProtobufNetSerializer();
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


        public T Create<T>(ReadOnlySpan<byte> data)
        {
            var ser = InternalSerializer;
            SerializerBuilder.Build<T>(ser);
            return ser.Deserialize<T>(data);
        }

        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            var ser = InternalSerializer;
            SerializerBuilder.Build<T>(ser);
            return ser.Deserialize<T>(data);
        }


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
