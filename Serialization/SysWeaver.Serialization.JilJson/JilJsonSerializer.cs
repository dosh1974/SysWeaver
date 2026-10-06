using Jil;

using System;
using System.IO;
using System.Text;


namespace SysWeaver.Serialization
{
    /// <summary>
    /// Serializer for the "json" extension backed by the Jil library.
    /// Has a low priority (-5) so it never displaces the other JSON serializers when they are registered.
    /// </summary>
    /// <remarks>
    /// <see cref="SerializerOptions.Verbose"/> produces pretty printed output, other options produce compact output.
    /// Uses Jil's default settings otherwise (Jil's own conventions for dates etc. apply).
    /// </remarks>
    public sealed class JilJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "JilJson";

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

        JilJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new JilJsonSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        static readonly Options OptVerbose = new Options(true);
        static readonly Options OptCompact = new Options(false);

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                return JSON.Deserialize<T>(r);
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                return JSON.Deserialize<T>(r);
            }
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Encoding.UTF8.GetBytes(JSON.Serialize(obj, options == SerializerOptions.Verbose ? OptVerbose : OptCompact));
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JSON.Serialize(obj, options == SerializerOptions.Verbose ? OptVerbose : OptCompact);
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JSON.Deserialize<T>(new String(text));
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            return JSON.Deserialize<T>(text);
        }
    }
}
