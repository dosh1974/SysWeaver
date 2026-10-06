using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.NewtonsoftJson;

namespace SysWeaver.Serialization
{


    /// <summary>
    /// Serializer for the "json" extension backed by Newtonsoft.Json (priority 1).
    /// </summary>
    /// <remarks>
    /// Serialization uses <see cref="NewtonsoftJson.MemberResolver"/>: fields are included and read-only fields/get-only properties are not written.
    /// "$type" information is written only where needed for <see cref="SerializerOptions.Compact"/>, on every object (and indented) for <see cref="SerializerOptions.Verbose"/> and never for <see cref="SerializerOptions.Typeless"/>.
    /// Deserialization honors "$type" (<see cref="TypeNameHandling.Auto"/>) and resolves names with <see cref="TypeNameResolver.GetForData"/>,
    /// so only types allowed by the <see cref="DataTypePolicy"/> (and assignable to the declared type) can be instantiated.
    /// Deserializers are pooled (<see cref="LimitedObjectPool{T}"/>) so concurrent calls are safe.
    /// </remarks>
    public sealed class NewtonsoftJsonSerializer : ITextSerializerType
    {
        /// <inheritdoc/>
        public string Name => "Newtonsoft.Json";

        /// <inheritdoc/>
        public string Extension => "json";

        /// <inheritdoc/>
        public int Prio => 1;

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
        
        NewtonsoftJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ITextSerializerType Instance = new NewtonsoftJsonSerializer();
        /// <summary>
        /// Returns the <see cref="Name"/> of this serializer.
        /// </summary>
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

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
            => ToString(obj, options).ToUTF8();


        /// <summary>
        /// Factory for pooled deserializers (type name handling enabled, resolving type names using <see cref="SerializationBinder"/>).
        /// </summary>
        static Func<Action<PooledJsonSerializer>, PooledJsonSerializer> DeserCreate = d =>
        {
            var ser = new PooledJsonSerializer(d);
            ser.ObjectCreationHandling = ObjectCreationHandling.Replace;
            ser.TypeNameHandling = TypeNameHandling.Auto;
            ser.SerializationBinder = SerializationBinder.Instance;
            return ser;
        };

        static readonly LimitedObjectPool<PooledJsonSerializer> DeSerPool = new(DeserCreate, 128);



        /// <summary>
        /// Factory for pooled serializers used by <see cref="ToFormattedJson{T}(T)"/>.
        /// </summary>
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



        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
        /// Convert an object to indented json text (Newtonsoft, with "$type" information on every object) where byte arrays are written as compact,
        /// column aligned rows of 32 numbers per line instead of base64 strings.
        /// Intended for human readable diagnostics output.
        /// </summary>
        /// <typeparam name="T">The static type to serialize as.</typeparam>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>The formatted json text.</returns>
        public static String ToFormattedJson<T>(T obj)
        {
            using var ser = FormattedSerPool.Get();
            using var sw = new StringWriter();
            using var t = new ExtendedJsonTextWriter(sw);
            ser.Serialize(t, obj);
            return sw.ToString();
        }


        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
            => JsonConvert.SerializeObject(obj, Formats[(int)options]);

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text)
            => JsonConvert.DeserializeObject<T>(new String(text), DeserFormats);

        /// <inheritdoc/>
        /// <exception cref="NullReferenceException">The text deserialized to null but is not null or exactly "null" (ex: an empty or white space string).</exception>
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
