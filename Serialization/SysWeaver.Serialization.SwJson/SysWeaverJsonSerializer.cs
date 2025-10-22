using System;
using System.Text;
using SysWeaver.Serialization.SwJson;
using SysWeaver.Serialization.SwJson.Reader;

namespace SysWeaver.Serialization
{

    public sealed class SysWeaverJsonSerializer : ITextSerializerType
    {
        public string Name => "SysWeaver.Json";

        public string Extension => "json";

        public int Prio => 2;

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;

        SysWeaverJsonSerializer()
        {
        }

        public static readonly ITextSerializerType Instance = new SysWeaverJsonSerializer();

        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact) => JsonWriter.ToJsonBytes(obj);

        public unsafe T Create<T>(ReadOnlySpan<byte> data) 
        {
            return JsonReader.Create<T>(data);
        }

        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            return JsonReader.Create<T>(data.Span);
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            return JsonWriter.ToJsonString(obj);
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            var size = Utf8Parser.UTF8.GetMaxByteCount(text.Length);
            var data = size <= 4096 ? stackalloc Byte[size] : GC.AllocateUninitializedArray<Byte>(size).AsSpan();
            var l = Utf8Parser.UTF8.GetBytes(text, data);
            return JsonReader.Create<T>(data.Slice(0, l));
        }

        public T FromString<T>(String text)
        {
            return JsonReader.Create<T>(text);
        }

    }



}
