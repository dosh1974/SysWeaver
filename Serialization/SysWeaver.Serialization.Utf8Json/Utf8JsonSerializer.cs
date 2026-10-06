using System;
using System.IO;
using System.Text;
using Utf8Json;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Serializer for the "json" extension backed by the Utf8Json library.
    /// Has a low priority (-5) so it never displaces the other JSON serializers when they are registered.
    /// </summary>
    /// <remarks>
    /// Uses Utf8Json's default resolver, the <see cref="SerializerOptions"/> are ignored (output is always compact).
    /// </remarks>
    public sealed class Utf8JsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "Utf8Json";

        /// <inheritdoc/>
        public string Extension => "json";

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

        /// <inheritdoc/>
        public int Prio => -5;

        Utf8JsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new Utf8JsonSerializer();
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
                return JsonSerializer.Deserialize<T>(ms);
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return JsonSerializer.Deserialize<T>(ms);
            }
        }


        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.Serialize(obj);
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonSerializer.ToJsonString(obj);
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JsonSerializer.Deserialize<T>(new String(text));
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            return JsonSerializer.Deserialize<T>(text);
        }
    }
}
