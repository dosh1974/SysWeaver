using System;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Creates objects from binary data produced by a matching <see cref="ISerializer"/>.
    /// </summary>
    public interface IDeserializer : ISerializerInfo
    {
        /// <summary>
        /// Deserialize an object from binary data (UTF-8 encoded text for text based serializers).
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="data">The serialized data.</param>
        /// <returns>The deserialized object (null/default if the data represents a null value).</returns>
        T Create<T>(ReadOnlyMemory<Byte> data);

        /// <summary>
        /// Deserialize an object from binary data (UTF-8 encoded text for text based serializers).
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="data">The serialized data.</param>
        /// <returns>The deserialized object (null/default if the data represents a null value).</returns>
        T Create<T>(ReadOnlySpan<Byte> data);
    }

    /// <summary>
    /// Creates objects from text produced by a matching <see cref="ITextSerializer"/>.
    /// </summary>
    public interface ITextDeserializer : IDeserializer
    {
        /// <summary>
        /// Deserialize an object from text.
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="text">The serialized text.</param>
        /// <returns>The deserialized object (null/default if the text represents a null value).</returns>
        T FromString<T>(ReadOnlySpan<Char> text);

        /// <summary>
        /// Deserialize an object from text.
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="text">The serialized text.</param>
        /// <returns>The deserialized object (null/default if the text represents a null value).</returns>
        T FromString<T>(String text);

        /// <summary>
        /// Deserialize an object from text, with the same result and exceptions as <see cref="FromString{T}(String)"/> of the same text
        /// (unlike <see cref="FromString{T}(ReadOnlySpan{Char})"/> that may differ in edge cases for some implementations).
        /// The default implementation creates a string and calls <see cref="FromString{T}(String)"/>,
        /// implementations that can deserialize from a span with the same result override it (no string is created).
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="text">The serialized text.</param>
        /// <returns>The deserialized object (null/default if the text represents a null value).</returns>
        T FromSpan<T>(ReadOnlySpan<Char> text) => FromString<T>(new String(text));

    }

}
