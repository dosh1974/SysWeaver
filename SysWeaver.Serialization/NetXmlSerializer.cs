using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace SysWeaver.Serialization
{

    /// <summary>
    /// Serializer for the "xml" extension using <see cref="XmlSerializer"/>.
    /// Not registered by default, call <see cref="Register"/> to make it available.
    /// </summary>
    /// <remarks>
    /// The usual <see cref="XmlSerializer"/> restrictions apply: types must be public with a public parameterless constructor and only public read/write members are serialized.
    /// The <see cref="SerializerOptions"/> are ignored (output is always indented).
    /// The XML declaration of both <see cref="Serialize{T}(T, SerializerOptions)"/> and <see cref="ToString{T}(T, SerializerOptions)"/> states encoding="utf-8" (the <c>Encoding</c>), so the text encoded as UTF-8 can be read as data.
    /// Whitespace only strings are read as empty strings (an <see cref="XmlSerializer"/> limitation).
    /// </remarks>
    public sealed class NetXmlSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "System.Xml.Serialization";
        /// <inheritdoc/>
        public string Extension => "xml";

        /// <inheritdoc/>
        public int Prio => 0;

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/xml";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);


        /// <inheritdoc/>
        public Encoding Encoding => Encoding.UTF8;

        NetXmlSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ITextSerializerType Instance = new NetXmlSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = new XmlSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                s.Serialize(ms, obj);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// A string writer that reports UTF-8 as its encoding, so that the XML declaration states the <see cref="Encoding"/> of the serializer
        /// </summary>
        sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter() : base(System.Globalization.CultureInfo.InvariantCulture)
            {
            }

            public override Encoding Encoding => Encoding.UTF8;
        }

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = new XmlSerializer(typeof(T));
            using var sb = new Utf8StringWriter();
            s.Serialize(sb, obj);
            return sb.ToString();
        }

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            var t = new String(text);
            var s = new XmlSerializer(typeof(T));
            using var sb = new StringReader(t);
            return (T)s.Deserialize(sb);
        }

        /// <inheritdoc/>
        public T FromString<T>(string text)
        {
            var s = new XmlSerializer(typeof(T));
            using var sb = new StringReader(text);
            return (T)s.Deserialize(sb);
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            var s = new XmlSerializer(typeof(T));
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return (T)s.Deserialize(ms);
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            var s = new XmlSerializer(typeof(T));
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return (T)s.Deserialize(ms);
            }
        }


    }

}
