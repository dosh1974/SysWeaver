using System;
using System.Text;
using SysWeaver.Remote.Connection;
using SysWeaver.Serialization;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Write only serializer that produces application/x-www-form-urlencoded data from the public fields and properties of an object (registered with the extension "formUrl").
    /// Registered automatically by <see cref="RemoteConnectionBase"/> when the corresponding <see cref="RemoteParam"/> serializer name is used.
    /// </summary>
    /// <remarks>
    /// Deserialization is not supported (all Create/FromString methods throw <see cref="NotImplementedException"/>). Thread safe.
    /// </remarks>
    public sealed class FormUrlSerializer : ITextSerializerType
    {
        /// <summary>
        /// The shared instance.
        /// </summary>
        public static readonly ITextSerializerType Instance = new FormUrlSerializer();

        /// <summary>
        /// Register <see cref="Instance"/> with <see cref="SerManager"/> (safe to call multiple times).
        /// </summary>
        public static void Register() => SerManager.AddType(Instance);

        /// <summary>
        /// Always UTF-8.
        /// </summary>
        public Encoding Encoding => Encoding.UTF8;

        /// <summary>
        /// The serializer name / extension, "formUrl".
        /// </summary>
        public string Extension => "formUrl";

        /// <summary>
        /// The mime type, "application/x-www-form-urlencoded".
        /// </summary>
        public const String MimeType = "application/x-www-form-urlencoded";

        /// <inheritdoc/>
        public string Mime => MimeType;

        /// <inheritdoc/>
        public string MimeHeader { get; private set; } = SerTools.MakeHeader(MimeType, Encoding.UTF8);

        /// <summary>
        /// Low priority (-1) so that it's never preferred over other serializers for the same extension.
        /// </summary>
        public int Prio => -1;

        /// <inheritdoc/>
        public string Name => "x-www-form-urlencoded (only writing available)";

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotImplementedException">Always.</exception>
        public T Create<T>(ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotImplementedException">Always.</exception>
        public T Create<T>(ReadOnlyMemory<byte> data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotImplementedException">Always.</exception>
        public T FromString<T>(ReadOnlySpan<char> text)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotImplementedException">Always.</exception>
        public T FromString<T>(string text)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Serializes the supported public members of <paramref name="obj"/> as UTF-8 form url encoded data.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="obj">The object, must not be null.</param>
        /// <param name="options">Ignored.</param>
        /// <returns>The encoded data (a slice of a newly allocated buffer).</returns>
        public ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var b = new FormUrlWriter();
            FormUrlWriter.Cache<T>.Write(b, obj);
            return new ReadOnlyMemory<byte>(b.Data, 0, b.Offset);
        }

        /// <summary>
        /// Serializes the supported public members of <paramref name="obj"/> as a form url encoded string.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="obj">The object, must not be null.</param>
        /// <param name="options">Ignored.</param>
        /// <returns>The encoded text.</returns>
        public string ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact)
        {
            var b = new FormUrlWriter();
            FormUrlWriter.Cache<T>.Write(b, obj);
            return Encoding.UTF8.GetString(b.Data, 0, b.Offset);
        }

    }


}
