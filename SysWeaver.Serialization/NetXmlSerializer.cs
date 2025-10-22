using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace SysWeaver.Serialization
{

    public sealed class NetXmlSerializer : ITextSerializerType
    {
        public string Name => "System.Xml.Serialization";
        public string Extension => "xml";

        public int Prio => 0;

        public const String MimeType = "application/xml";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);


        public Encoding Encoding => Encoding.UTF8;

        NetXmlSerializer()
        {
        }

        public static readonly ITextSerializerType Instance = new NetXmlSerializer();
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var text = ToString(obj);
            var s = new XmlSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                s.Serialize(ms, obj);
                return new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = new XmlSerializer(typeof(T));
            using var sb = new StringWriter();
            s.Serialize(sb, obj);
            return sb.ToString();
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            var t = new String(text);
            var s = new XmlSerializer(typeof(T));
            using var sb = new StringReader(t);
            return (T)s.Deserialize(sb);
        }

        public T FromString<T>(string text)
        {
            var s = new XmlSerializer(typeof(T));
            using var sb = new StringReader(text);
            return (T)s.Deserialize(sb);
        }

        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            var s = new XmlSerializer(typeof(T));
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                return (T)s.Deserialize(ms);
            }
        }

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
