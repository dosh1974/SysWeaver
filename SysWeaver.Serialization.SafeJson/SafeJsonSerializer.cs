using System;
using System.Text;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Composite serializer for the "json" extension: writes compact output with <see cref="SysWeaverJsonSerializer"/> and reads (and writes verbose output) with <see cref="NewtonsoftJsonSerializer"/>.
    /// Has priority 10, the highest of all bundled JSON serializers, so registering it makes it the JSON implementation of the process.
    /// </summary>
    /// <remarks>
    /// <see cref="SerializerOptions.Verbose"/> output is produced by Newtonsoft (indented, with "$type" information on every object), all other options use SysWeaver.Json.
    /// All deserialization goes through Newtonsoft with type name handling enabled (see <see cref="NewtonsoftJsonSerializer"/>), so the security considerations for that serializer apply to untrusted input.
    /// Types must serialize compatibly with both engines.
    /// </remarks>
    public sealed class SafeJsonSerializer : ITextSerializerType
    {
        SafeJsonSerializer()
        {
        }

        /// <summary>
        /// The singleton instance of this serializer.
        /// </summary>
        public static ITextSerializerType Instance = new SafeJsonSerializer();

        /// <summary>
        /// Call once to register this serializer type to the serializer manager
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        /// <inheritdoc/>
        public string Name => "Safe Json";

        /// <inheritdoc/>
        public string Extension => "json";

        /// <inheritdoc/>
        public int Prio => 10;

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

        readonly ITextSerializerType DeSerializer = NewtonsoftJsonSerializer.Instance;
        readonly ITextSerializerType Serializer = SysWeaverJsonSerializer.Instance;

        /// <summary>
        /// Convert an object (using <see cref="NewtonsoftJsonSerializer.ToFormattedJson{T}(T)"/>) to indented json text (Newtonsoft, with "$type" information on every object) where byte arrays are written as compact,
        /// column aligned rows of 32 numbers per line instead of base64 strings.
        /// Intended for human readable diagnostics output.
        /// </summary>
        /// <typeparam name="T">The static type to serialize as.</typeparam>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>The formatted json text.</returns>
        public static String ToFormattedJson<T>(T obj)
            => NewtonsoftJsonSerializer.ToFormattedJson(obj);

        /// <inheritdoc/>
        public T Create<T>(ReadOnlyMemory<byte> data) => DeSerializer.Create<T>(data);
        /// <inheritdoc/>
        public T Create<T>(ReadOnlySpan<byte> data) => DeSerializer.Create<T>(data);

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact) 
            => options == SerializerOptions.Verbose ? DeSerializer.Serialize(obj, options) : Serializer.Serialize(obj, options);

        /// <inheritdoc/>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
            => options == SerializerOptions.Verbose ? DeSerializer.ToString(obj, options) : Serializer.ToString(obj, options);

        /// <inheritdoc/>
        public T FromString<T>(ReadOnlySpan<char> text) => DeSerializer.FromString<T>(text);

        /// <inheritdoc/>
        public T FromString<T>(string text) => DeSerializer.FromString<T>(text);

    }

}
