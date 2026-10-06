using CompactJson;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Serializer for the "json" extension backed by the CompactJson library.
    /// Has a low priority (-5) so it never displaces the other JSON serializers when they are registered.
    /// </summary>
    /// <remarks>
    /// <see cref="SerializerOptions.Verbose"/> produces indented output, other options produce compact output.
    /// Binary data is converted through intermediate strings (UTF-8 encode/decode), so this is not allocation free.
    /// </remarks>
    public sealed class CompactJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "CompactJson";

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

        CompactJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new CompactJsonSerializer();
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
                using var r = new StreamReader(ms);
                return Serializer.Parse<T>(r);
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                return Serializer.Parse<T>(r);
            }
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Encoding.UTF8.GetBytes(Serializer.ToString(obj, options == SerializerOptions.Verbose));
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Serializer.ToString(obj, options == SerializerOptions.Verbose);
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return Serializer.Parse<T>(new String(text));
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            return Serializer.Parse<T>(text);
        }
    }
}
