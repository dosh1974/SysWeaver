using Jil;

using System;
using System.IO;
using System.Text;


namespace SysWeaver.Serialization
{
    public sealed class JilJsonSerializer : ITextSerializerType
    {
        public string Name => "JilJson";

        public string Extension => "json";

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;

        public int Prio => -5;

        JilJsonSerializer()
        {
        }

        public static readonly ISerializerType Instance = new JilJsonSerializer();
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        static readonly Options OptVerbose = new Options(true);
        static readonly Options OptCompact = new Options(false);

        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                return JSON.Deserialize<T>(r);
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                return JSON.Deserialize<T>(r);
            }
        }

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Encoding.UTF8.GetBytes(JSON.Serialize(obj, options == SerializerOptions.Verbose ? OptVerbose : OptCompact));
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JSON.Serialize(obj, options == SerializerOptions.Verbose ? OptVerbose : OptCompact);
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JSON.Deserialize<T>(new String(text));
        }

        public T FromString<T>(string text)
        {
            return JSON.Deserialize<T>(text);
        }
    }
}
