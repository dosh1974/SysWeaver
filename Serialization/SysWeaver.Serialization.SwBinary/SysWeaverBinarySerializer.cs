using System;
using System.IO;
using System.Text;
using SysWeaver.Inspection;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Binary serializer for the "swbin" extension using SysWeaver's inspection based binary format (<see cref="BinaryWriterInspector"/> / <see cref="BinaryReaderInspector"/>).
    /// </summary>
    /// <remarks>
    /// Values are written as their static type <c>T</c> (no type information for the root), so they must be read back as the same type.
    /// The <see cref="SerializerOptions"/> are ignored.
    /// </remarks>
    public sealed class SysWeaverBinarySerializer : ISerializerType
    {
        /// <inheritdoc/>
        public string Name => "SysWeaver.Binary";

        /// <inheritdoc/>
        public string Extension => "swbin";

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/x-swbin";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = MimeType;

        /// <inheritdoc/>
        public Encoding Encoding => null;

        /// <inheritdoc/>
        public int Prio => 0;

        SysWeaverBinarySerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new SysWeaverBinarySerializer();
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
                using var r = new BinaryReaderInspector(ms, true);
                return r.Read<T>();
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new BinaryReaderInspector(ms, true);
                return r.Read<T>();
            }
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            //  Written to pooled buffers, only the result (of the exact size) is allocated
            using var ms = new ArrayPoolStream();
            using (var w = new BinaryWriterInspector(ms, true))
                w.Write(obj, false);
            return ms.ToArray();
        }
    }


}
