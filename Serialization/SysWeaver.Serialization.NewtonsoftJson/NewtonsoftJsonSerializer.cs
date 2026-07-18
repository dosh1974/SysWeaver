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
            => ToString(obj).ToUTF8();


        static Func<Action<PooledJsonSerializer>, PooledJsonSerializer> DeserCreate = d =>
        {
            var ser = new PooledJsonSerializer(d);
            ser.ObjectCreationHandling = ObjectCreationHandling.Replace;
            ser.TypeNameHandling = TypeNameHandling.Auto;
            ser.SerializationBinder = SerializationBinder.Instance;
            return ser;
        };

        static readonly LimitedObjectPool<PooledJsonSerializer> DeSerPool = new(DeserCreate, 128);



        static Func<Action<PooledJsonSerializer>, PooledJsonSerializer> SerCreate = d =>
        {
            var ser = new PooledJsonSerializer(d);
            ser.Formatting = Formatting.Indented;
            ser.TypeNameHandling = TypeNameHandling.All;
            ser.ContractResolver = MemberResolver.Instance;
            ser.Converters.Add(JsonByteArrayConverter.Instance);
            return ser;
        };

        static readonly LimitedObjectPool<PooledJsonSerializer> FormattedSerPool = new(SerCreate, 16);

  

        static JsonSerializerSettings GetByteArraySer()
        {
            var s = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.All,
                ContractResolver = MemberResolver.Instance,
            };
            s.Converters.Add(new JsonByteArrayConverter());
            return s;
        }



        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            using var ser = DeSerPool.Get();
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
            using var ser = DeSerPool.Get();
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new StreamReader(ms);
                using var m = new JsonTextReader(r);
                return ser.Deserialize<T>(m);
            }
        }

        /// <summary>
        /// Convert an object to json text with nice formatting, specifically for byte array's.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static String ToFormattedJson<T>(T obj)
        {
            using var ser = FormattedSerPool.Get();
            using var sw = new StringWriter();
            using var t = new ExtendedJsonTextWriter(sw);
            ser.Serialize(t, obj);
            return sw.ToString();
        }


        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
            => JsonConvert.SerializeObject(obj, Formats[(int)options]);

        public T FromString<T>(ReadOnlySpan<char> text)
            => JsonConvert.DeserializeObject<T>(new String(text), DeserFormats);

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
