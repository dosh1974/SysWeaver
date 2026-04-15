using System;
using System.Runtime.CompilerServices;
using SysWeaver.Serialization;


namespace SysWeaver
{
    public static class SerExtensions
    {
        static ITextSerializerType JsonSer;

        /// <summary>
        /// Create a json string from an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToJsonString<T>(this T value, SerializerOptions options = SerializerOptions.Verbose)
            => (JsonSer ??= SerManager.GetText("json")).ToString(value, options);

        /// <summary>
        /// Create a json data blob from an object
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<Byte> ToJsonData<T>(this T value, SerializerOptions options = SerializerOptions.Verbose)
            => (JsonSer ??= SerManager.GetText("json")).Serialize(value, options);

        /// <summary>
        /// Create an object from some json data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJsonData<T>(this ReadOnlyMemory<Byte> data)
            => (JsonSer ??= SerManager.GetText("json")).Create<T>(data);

        /// <summary>
        /// Create an object from some json data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJsonData<T>(this ReadOnlySpan<Byte> data)
            => (JsonSer ??= SerManager.GetText("json")).Create<T>(data);

        /// <summary>
        /// Create an object from some json data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromJsonData<T>(this Byte[] data)
            => (JsonSer ??= SerManager.GetText("json")).Create<T>(data);


    }

}

