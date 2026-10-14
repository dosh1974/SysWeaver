using System;
using System.Buffers;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Serializes objects to binary data.
    /// </summary>
    public interface ISerializer : ISerializerInfo
    {
        /// <summary>
        /// Serialize an object to binary data (UTF-8 encoded text for text based serializers).
        /// </summary>
        /// <typeparam name="T">The static type to serialize as. Some implementations add type information when the runtime type of <paramref name="obj"/> differs from <typeparamref name="T"/>, use <see cref="SerTools.SerializeWithoutType"/> to serialize as the runtime type.</typeparam>
        /// <param name="obj">The object to serialize, may be null.</param>
        /// <param name="options">The requested output style, implementations are free to ignore it.</param>
        /// <returns>The serialized data, owned by the caller. The memory may be a slice of a larger array.</returns>
        ReadOnlyMemory<byte> Serialize<T>(T obj, SerializerOptions options = SerializerOptions.Compact);

        /// <summary>
        /// Serialize an object to binary data (UTF-8 encoded text for text based serializers), written to a buffer writer
        /// (ex: a <see cref="SerBufferWriter"/>, so that no array is allocated for data that is only needed temporarily).
        /// The data written is the same as the data returned by <see cref="Serialize{T}(T, SerializerOptions)"/>, with the same exceptions.
        /// </summary>
        /// <remarks>
        /// The default implementation writes the data returned by <see cref="Serialize{T}(T, SerializerOptions)"/>,
        /// implementations that can serialize to a pooled buffer override it.
        /// </remarks>
        /// <typeparam name="T">The static type to serialize as, see <see cref="Serialize{T}(T, SerializerOptions)"/>.</typeparam>
        /// <param name="obj">The object to serialize, may be null.</param>
        /// <param name="output">The writer to write the serialized data to.</param>
        /// <param name="options">The requested output style, implementations are free to ignore it.</param>
        void Serialize<T>(T obj, IBufferWriter<Byte> output, SerializerOptions options = SerializerOptions.Compact)
        {
            var data = Serialize(obj, options).Span;
            data.CopyTo(output.GetSpan(data.Length));
            output.Advance(data.Length);
        }
    }


    /// <summary>
    /// Serializes objects to text.
    /// </summary>
    public interface ITextSerializer : ISerializer
    {
        /// <summary>
        /// Serialize an object to a string.
        /// </summary>
        /// <typeparam name="T">The static type to serialize as.</typeparam>
        /// <param name="obj">The object to serialize, may be null.</param>
        /// <param name="options">The requested output style, implementations are free to ignore it.</param>
        /// <returns>The serialized text.</returns>
        String ToString<T>(T obj, SerializerOptions options = SerializerOptions.Compact);
    }

}
