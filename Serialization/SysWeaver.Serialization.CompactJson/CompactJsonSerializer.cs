using CompactJson;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace SysWeaver.Serialization
{
    public sealed class CompactJsonSerializer : ITextSerializerType
    {
        public string Name => "CompactJson";

        public string Extension => "json";

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;

        public int Prio => -5;

        CompactJsonSerializer()
        {
        }

        public static readonly ISerializerType Instance = new CompactJsonSerializer();
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
                using var r = new StreamReader(ms);
                return Serializer.Parse<T>(r);
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                return Serializer.Parse<T>(r);
            }
        }

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Encoding.UTF8.GetBytes(Serializer.ToString(obj, options == SerializerOptions.Verbose));
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return Serializer.ToString(obj, options == SerializerOptions.Verbose);
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return Serializer.Parse<T>(new String(text));
        }

        public T FromString<T>(string text)
        {
            return Serializer.Parse<T>(text);
        }
    }
}
