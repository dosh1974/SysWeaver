using System;
using System.IO;
using System.Text;
using SysWeaver.Remote.Connection;
using SysWeaver.Serialization;

namespace SysWeaver.Remote
{
    public sealed class IgnoreDefaultsFormUrlSerializer : ISerializerType
    {
        public static readonly ITextSerializerType Instance = new FormUrlSerializer();

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        public Encoding Encoding => Encoding.UTF8;

        public string Extension => "formUrlIgnoreDefaults";

        public const String MimeType = "application/x-www-form-urlencoded";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public int Prio => -1;

        public string Name => "x-www-form-urlencoded (only writing available)";


        public T Create<T>(ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }

        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            throw new NotImplementedException();
        }

        public T FromString<T>(string text)
        {
            throw new NotImplementedException();
        }

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var b = new FormUrlWriter();
            FormUrlWriter.Cache<T>.WriteIgnoreDefaults(b, obj);
            var len = b.Offset;
            var data = b.Data;
            var skip = ((len > 0) && (data[0] == (Byte)'&')) ? 1 : 0;
            return new ReadOnlyMemory<byte>(data, skip, len - skip);
        }

        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var b = new FormUrlWriter();
            FormUrlWriter.Cache<T>.WriteIgnoreDefaults(b, obj);
            var len = b.Offset;
            var data = b.Data;
            var skip = ((len > 0) && (data[0] == (Byte)'&')) ? 1 : 0;
            return Encoding.UTF8.GetString(data, skip, len - skip);
        }

    }


}
