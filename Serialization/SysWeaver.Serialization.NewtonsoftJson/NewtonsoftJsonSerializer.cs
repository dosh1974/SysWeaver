using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.NewtonsoftJson;

namespace SysWeaver.Serialization
{
    public sealed class NewtonsoftJsonSerializer : ITextSerializerType
    {
        public string Name => "Newtonsoft.Json";

        public string Extension => "json";

        public int Prio => 1;

        public const String MimeType = "application/json";

        public string Mime => MimeType;

        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        public Encoding Encoding => Encoding.UTF8;
        
        NewtonsoftJsonSerializer()
        {
        }

        public static readonly ITextSerializerType Instance = new NewtonsoftJsonSerializer();
        public override string ToString() => Name;

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);


        static readonly JsonSerializerSettings[] Formats =
        [
            new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = MemberResolver.Instance,
            },
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.All,
                ContractResolver = MemberResolver.Instance,
            },
            new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.None,
                ContractResolver = MemberResolver.Instance,
            },
        ];

        static readonly JsonSerializerSettings DeserFormats = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = SerializationBinder.Instance,
        };

        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = Formats[(int)options];
            var st = JsonConvert.SerializeObject(obj, s);
            return st.ToUTF8();
        }

        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            var ser = new JsonSerializer();
            ser.ObjectCreationHandling = ObjectCreationHandling.Replace;
            ser.TypeNameHandling = TypeNameHandling.Auto;
            ser.SerializationBinder = SerializationBinder.Instance;
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                using var m = new JsonTextReader(r);
                return ser.Deserialize<T>(m);
            }
        }

        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            var ser = new JsonSerializer();
            ser.ObjectCreationHandling = ObjectCreationHandling.Replace;
            ser.TypeNameHandling = TypeNameHandling.Auto;
            ser.SerializationBinder = SerializationBinder.Instance;
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                using var m = new JsonTextReader(r);
                return ser.Deserialize<T>(m);
            }
        }


        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = Formats[(int)options];
            return JsonConvert.SerializeObject(obj, s);
        }

        public T FromString<T>(ReadOnlySpan<char> text)
        {
            return JsonConvert.DeserializeObject<T>(new String(text), DeserFormats);
        }

        public T FromString<T>(String text)
        {
            var t = JsonConvert.DeserializeObject<T>(text, DeserFormats);
            if (t != null)
                return t;
            if (text == null) 
                return t;
            if (text.AsSpan().SequenceEqual("null".AsSpan()))
                return t;
            throw new NullReferenceException();
        }

    }
}
