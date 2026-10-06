using System;
using System.Runtime.CompilerServices;
using SysWeaver.Serialization;


namespace SysWeaver
{
    /// <summary>
    /// Convenience extension methods for converting objects to and from JSON using the "json" serializer registered in <see cref="SerManager"/>.
    /// </summary>
    /// <remarks>
    /// The serializer is resolved with <see cref="SerManager.GetText(string)"/> on first use and then cached for the lifetime of the process,
    /// so a higher priority JSON serializer registered after the first call is NOT picked up by these methods.
    /// Register all serializers at startup before using these.
    /// </remarks>
    public static class SerExtensions
    {
        static ITextSerializerType JsonSer;

        /// <summary>
        /// Create a json string from an object.
        /// </summary>
        /// <typeparam name="T">The static type to serialize as.</typeparam>
        /// <param name="value">The object to serialize, may be null.</param>
        /// <param name="options">The output style, defaults to <see cref="SerializerOptions.Verbose"/> (the effect depends on the active json serializer).</param>
        /// <returns>The json text.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToJsonString<T>(this T value, SerializerOptions options = SerializerOptions.Verbose)
            => (JsonSer ??= SerManager.GetText("json")).ToString(value, options);

        /// <summary>
        /// Create a UTF-8 encoded json data blob from an object.
        /// </summary>
        /// <typeparam name="T">The static type to serialize as.</typeparam>
        /// <param name="value">The object to serialize, may be null.</param>
        /// <param name="options">The output style, defaults to <see cref="SerializerOptions.Verbose"/> (the effect depends on the active json serializer).</param>
        /// <returns>The UTF-8 encoded json data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<Byte> ToJsonData<T>(this T value, SerializerOptions options = SerializerOptions.Verbose)
            => (JsonSer ??= SerManager.GetText("json")).Serialize(value, options);

        /// <summary>
        /// Create an object from some UTF-8 encoded json data.
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="data">The UTF-8 encoded json data.</param>
        /// <returns>The deserialized object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJsonData<T>(this ReadOnlyMemory<Byte> data)
            => (JsonSer ??= SerManager.GetText("json")).Create<T>(data);

        /// <summary>
        /// Create an object from some UTF-8 encoded json data.
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="data">The UTF-8 encoded json data.</param>
        /// <returns>The deserialized object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJsonData<T>(this ReadOnlySpan<Byte> data)
            => (JsonSer ??= SerManager.GetText("json")).Create<T>(data);

        /// <summary>
        /// Create an object from some UTF-8 encoded json data.
        /// </summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <param name="data">The UTF-8 encoded json data.</param>
        /// <returns>The deserialized object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJsonData<T>(this Byte[] data)
            => (JsonSer ??= SerManager.GetText("json")).Create<T>(data);


    }

}

