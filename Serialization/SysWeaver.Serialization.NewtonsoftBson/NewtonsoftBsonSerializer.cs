using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;
using System.Text;
using SysWeaver.Serialization.NewtonsoftBson;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Binary serializer for the "bson" extension backed by Newtonsoft.Json.Bson.
    /// </summary>
    /// <remarks>
    /// Serialization uses <see cref="NewtonsoftBson.MemberResolver"/> (read-only members are not written) and writes "$type" information
    /// (only where needed for <see cref="SerializerOptions.Compact"/>, everywhere for <see cref="SerializerOptions.Verbose"/>, never for <see cref="SerializerOptions.Typeless"/>).
    /// Deserialization honors "$type" (<see cref="TypeNameHandling.Auto"/>) and resolves names with <see cref="TypeNameResolver.GetForData"/>,
    /// so only types allowed by the <see cref="DataTypePolicy"/> (and assignable to the declared type) can be instantiated.
    /// Global <see cref="JsonConvert.DefaultSettings"/> are not applied.
    /// BSON requires an object or array at the root: root values that aren't (null, primitives, strings, enums, dates, Guid, byte[] etc) are written wrapped in a document with a single "$swRootValue" member
    /// (and unwrapped when read), objects and collections are written as is. Root arrays/collections are read back (as arrays, or as "$type" / "$values" objects when written with type information).
    /// </remarks>
    public sealed class NewtonsoftBsonSerializer : ISerializerType
    {
        /// <inheritdoc/>
        public string Name => "Newtonsoft.Bson";

        /// <inheritdoc/>
        public string Extension => "bson";

        /// <inheritdoc/>
        public int Prio => 1;

        /// <summary>
        /// The MIME type of the data produced by this serializer.
        /// </summary>
        public const String MimeType = "application/bson";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = MimeType;

        /// <inheritdoc/>
        public Encoding Encoding => null;
        
        NewtonsoftBsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static readonly ISerializerType Instance = new NewtonsoftBsonSerializer();
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

        /// <summary>
        /// Deserialization settings: type name handling enabled, resolving type names using <see cref="SerializationBinder"/> (<see cref="TypeNameResolver.GetForData"/>).
        /// </summary>
        static readonly JsonSerializerSettings DeserFormats = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = SerializationBinder.Instance,
        };

        /// <inheritdoc/>
        /// <remarks>
        /// Values that BSON can't hold at the root (null, primitives, strings, enums, dates, Guid, byte[] etc) are written wrapped as {"$swRootValue": value}, see <see cref="SerializeWrapped"/>.
        /// Objects and collections are written as before (unwrapped).
        /// </remarks>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var s = Formats[(int)options];
            var ser = JsonSerializer.Create(s);
            if (!IsValueRoot(ser, s, obj))
            {
                using var ms = new MemoryStream();
                using var wr = new BsonDataWriter(ms);
                try
                {
                    ser.Serialize(wr, obj);
                    return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
                }
                catch (JsonWriterException ex) when ((ms.Length == 0) && ex.Message.Contains(RootValueError, StringComparison.Ordinal))
                {
                    //  Written as a value by a custom converter, write it wrapped (below)
                }
            }
            return SerializeWrapped(ser, obj);
        }

        /// <summary>
        /// The name of the only member of the document that wraps a root value (BSON requires an object or array at the root).
        /// Newtonsoft never writes a "$" member name (other than "$type", "$values", "$value", "$id" and "$ref"), so it can't be the first member of a real object root (arrays start with "0").
        /// </summary>
        const String RootValueName = "$swRootValue";

        /// <summary>
        /// The BSON element name of <see cref="RootValueName"/> (zero terminated)
        /// </summary>
        static ReadOnlySpan<byte> RootValueNameZ => "$swRootValue\0"u8;

        /// <summary>
        /// Part of the message of the exception that the BsonDataWriter throws when the root isn't an object or array
        /// </summary>
        const String RootValueError = "BSON must start with an Object or Array";

        /// <summary>
        /// Check if a root value is written as a BSON value (not as an object or array), the BsonDataWriter can't write it.
        /// </summary>
        /// <param name="ser">The serializer used for writing</param>
        /// <param name="settings">The settings of the serializer</param>
        /// <param name="obj">The value</param>
        /// <returns>True for null, primitives and types written as strings (without a custom converter), byte[] is an object when all types are written ({"$type":...,"$value":...})</returns>
        static bool IsValueRoot(JsonSerializer ser, JsonSerializerSettings settings, Object obj)
        {
            if (obj == null)
                return true;
            var c = ser.ContractResolver.ResolveContract(obj.GetType());
            //  The output of a converter is unknown, a value root is detected by the writer exception
            if (c.Converter != null)
                return false;
            if (c is Newtonsoft.Json.Serialization.JsonStringContract)
                return true;
            if (c is Newtonsoft.Json.Serialization.JsonPrimitiveContract)
                return !((obj is Byte[]) && (settings.TypeNameHandling == TypeNameHandling.All));
            return false;
        }

        /// <summary>
        /// Write a root value wrapped in a document with a single <see cref="RootValueName"/> member.
        /// </summary>
        /// <param name="ser">The serializer used for writing</param>
        /// <param name="obj">The value</param>
        /// <returns>The BSON data</returns>
        static ReadOnlyMemory<byte> SerializeWrapped<T>(JsonSerializer ser, T obj)
        {
            using var ms = new MemoryStream();
            using var wr = new BsonDataWriter(ms);
            wr.WriteStartObject();
            wr.WritePropertyName(RootValueName);
            ser.Serialize(wr, obj);
            wr.WriteEndObject();
            wr.Flush();
            return new ReadOnlyMemory<Byte>(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// Check if the data is a wrapped root value (a document starting with a <see cref="RootValueName"/> member, see <see cref="SerializeWrapped"/>).
        /// </summary>
        /// <param name="data">The BSON data</param>
        /// <returns>True if the data is a wrapped root value</returns>
        static bool IsWrapped(ReadOnlySpan<byte> data)
        {
            //  BSON document: int32 size, then elements (type byte, zero terminated name, value)
            var name = RootValueNameZ;
            return (data.Length > (5 + name.Length)) && data.Slice(5, name.Length).SequenceEqual(name);
        }

        /// <summary>
        /// Read the data (a wrapped root value or an object / array).
        /// </summary>
        static T Read<T>(JsonSerializer ser, BsonDataReader r, ReadOnlySpan<byte> data)
        {
            if (IsWrapped(data))
            {
                //  The start of the document, the member name and the value
                r.Read();
                r.Read();
                r.Read();
                return ser.Deserialize<T>(r);
            }
            r.ReadRootValueAsArray = ReadRootAsArray(ser, typeof(T), data);
            return ser.Deserialize<T>(r);
        }

        /// <summary>
        /// Check if the root of the data should be read as an array.
        /// Root collections are written as BSON arrays (a document with "0", "1".. keys) unless they are written with type information (an object starting with a "$type" member, followed by "$values").
        /// </summary>
        /// <param name="ser">The serializer used for reading</param>
        /// <param name="type">The type to read</param>
        /// <param name="data">The BSON data</param>
        /// <returns>True if the type is read as a json array and the data doesn't start with a "$type" member</returns>
        static bool ReadRootAsArray(JsonSerializer ser, Type type, ReadOnlySpan<byte> data)
        {
            if (ser.ContractResolver.ResolveContract(type) is not Newtonsoft.Json.Serialization.JsonArrayContract)
                return false;
            //  BSON document: int32 size, then elements (type byte, zero terminated name, value), "$type" is a string (type 2)
            return !((data.Length >= 11) && (data[4] == 0x02) && data.Slice(5, 6).SequenceEqual("$type\0"u8));
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlySpan<byte> data)
        {
            var ser = JsonSerializer.Create(DeserFormats);
            fixed (byte* bp = data)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new BsonDataReader(ms);
                return Read<T>(ser, r, data);
            }
        }

        /// <inheritdoc/>
        public unsafe T Create<T>(ReadOnlyMemory<byte> data)
        {
            var ser = JsonSerializer.Create(DeserFormats);
            fixed (byte* bp = data.Span)
            {
                using var ms = new UnmanagedMemoryStream(bp, data.Length);
                using var r = new BsonDataReader(ms);
                return Read<T>(ser, r, data.Span);
            }
        }


    }
}
