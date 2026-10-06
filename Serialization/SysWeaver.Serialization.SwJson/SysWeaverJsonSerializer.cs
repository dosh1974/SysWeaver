using System;
using System.Buffers;
using System.Text;
using SysWeaver.Serialization.SwJson;
using SysWeaver.Serialization.SwJson.Reader;

namespace SysWeaver.Serialization
{

    /// <summary>
    /// Serializer for the "json" extension using SysWeaver's own high performance JSON writer (<see cref="JsonWriter"/>) and reader (<see cref="JsonReader"/>).
    /// Has priority 2, so it takes precedence over System.Text.Json and Newtonsoft when registered.
    /// </summary>
    /// <remarks>
    /// The <see cref="SerializerOptions"/> are ignored (output is always compact).
    /// <see cref="FromString{T}(ReadOnlySpan{char})"/> transcodes to UTF-8 in a stack buffer (up to 4 KiB) or a pooled array.
    /// </remarks>
    public sealed class SysWeaverJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "SysWeaver.Json";

        /// <inheritdoc/>
        public string Extension => "json";

        /// <inheritdoc/>
        public int Prio => 2;

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/json";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        /// <inheritdoc/>
        public Encoding Encoding => Encoding.UTF8;

        SysWeaverJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ITextSerializerType Instance = new SysWeaverJsonSerializer();

        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact) => JsonWriter.ToJsonBytes(obj);

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlySpan<byte> data) 
        {
            return JsonReader.Create<T>(data);
        }

        /// <inheritdoc/>
        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonReader.Create<T>(data.Span);
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonWriter.ToJsonString(obj);
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            var size = Utf8Parser.UTF8.GetMaxByteCount(text.Length);
            Byte[] rented = null;
            var data = size <= 4096 ? stackalloc Byte[size] : (rented = ArrayPoolStream.Rent(size)).AsSpan();
            try
            {
                var l = Utf8Parser.UTF8.GetBytes(text, data);
                return JsonReader.Create<T>(data.Slice(0, l));
            }
            finally
            {
                if (rented != null)
                    ArrayPoolStream.Return(rented);
            }
        }

        /// <inheritdoc/>
        public T FromString<T>(String text)
        {
            return JsonReader.Create<T>(text);
        }

    }



}
