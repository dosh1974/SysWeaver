using System;
using System.IO;
using System.Text;
using SysWeaver.Inspection;


namespace SysWeaver.Serialization
{
    public sealed class SysWeaverBinarySerializer : ISerializerType
    {
        public string Name => "SysWeaver.Binary";

        public string Extension => "swbin";

        public const String MimeType = "application/x-swbin";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = MimeType;

        public Encoding Encoding => null;

        public int Prio => 0;

        SysWeaverBinarySerializer()
        {
        }

        public static readonly ISerializerType Instance = new SysWeaverBinarySerializer();
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
                using var r = new BinaryReaderInspector(ms, true);
                return r.Read<T>();
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new BinaryReaderInspector(ms, true);
                return r.Read<T>();
            }
        }

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriterInspector(ms, true))
                w.Write(obj, false);
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }
    }


}
