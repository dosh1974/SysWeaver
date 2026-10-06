using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Helper methods used by serializer implementations and consumers.
    /// </summary>
    public static class SerTools
    {
        /// <summary>
        /// Encode a string as UTF-8.
        /// </summary>
        /// <param name="st">The string to encode, must not be null.</param>
        /// <returns>The UTF-8 bytes, as a slice of a newly allocated (possibly larger) array.</returns>
        /// <remarks>
        /// Starts with a buffer of 2 bytes per char (+64), if that is too small (text with many non-ASCII chars) the exact size is computed and a new buffer is allocated.
        /// Invalid surrogates are replaced with U+FFFD.
        /// </remarks>
        public static ReadOnlyMemory<Byte> ToUTF8(this String st)
        {
            var l = st.Length << 1;
            l += 64;
            var buf = GC.AllocateUninitializedArray<Byte>(l);
            if (!Encoding.UTF8.TryGetBytes(st, buf, out var bufUse))
            {
                buf = GC.AllocateUninitializedArray<Byte>(Encoding.UTF8.GetByteCount(st));
                bufUse = Encoding.UTF8.GetBytes(st, buf);
            }
            return new ReadOnlyMemory<byte>(buf, 0, bufUse);
        }


        /// <summary>
        /// Create a Content-Type header value by appending the encoding's char set to a MIME type (ex: "application/json; charset=utf-8").
        /// </summary>
        /// <param name="mime">The MIME type.</param>
        /// <param name="enc">The text encoding, null for binary formats.</param>
        /// <returns>The MIME type with a "; charset=" suffix, or <paramref name="mime"/> unchanged if <paramref name="enc"/> (or its header name) is null.</returns>
        public static String MakeHeader(String mime, Encoding enc)
        {
            var e = enc?.HeaderName;
            return e == null ? mime : String.Join("; charset=", mime, e);
        }





        /// <summary>
        /// Serialize an object as its runtime type (from <see cref="Object.GetType"/>) rather than as <see cref="Object"/>, so serializers that add type information for polymorphic values don't do so for the root.
        /// <code>
        ///     SomeType data = new SomeType();
        ///     Object obj = data;
        ///     var a = serializer.Serialize(data);
        ///     var b = serializer.Serialize(obj);
        ///     var c = serializer.SerializeWithoutType(obj);
        /// </code>
        /// For the above, a and c will be equal, b may contain type information (depending on the serializer).
        /// </summary>
        /// <param name="serializer">The serializer.</param>
        /// <param name="obj">The object to serialize, if null it is serialized as a null <see cref="Object"/>.</param>
        /// <param name="options">The requested output style.</param>
        /// <returns>The serialized data.</returns>
        /// <remarks>A compiled delegate is created and cached (thread safe) per runtime type on first use.</remarks>
        public static ReadOnlyMemory<Byte> SerializeWithoutType(this ISerializer serializer, Object obj, SerializerOptions options = SerializerOptions.Compact)
        {
            if (obj == null)
                return serializer.Serialize(obj, options);
            var type = obj.GetType();
            var cache = TypedSerializers;
            if (!cache.TryGetValue(type, out var fn))
            {
                fn = CreateTypeSerializer(type);
                cache.TryAdd(type, fn);
            }
            return fn(serializer, obj, options);
        }

        static Func<ISerializer, Object, SerializerOptions, ReadOnlyMemory<Byte>> CreateTypeSerializer(Type type)
        {
            
            var m = MethodSerialize.MakeGenericMethod(type);
            var ser = ExpInpSer;
            var obj = ExpInpObj;
            var opt = ExpInpOptions;
            var fn = Expression.Call(ser, m, Expression.Convert(obj, type), opt);
            return Expression.Lambda<Func<ISerializer, Object, SerializerOptions, ReadOnlyMemory<Byte>>>(fn, ser, obj, opt).Compile();
        }

        static readonly MethodInfo MethodSerialize = typeof(ISerializer).GetMethod(nameof(ISerializer.Serialize));
        static readonly ParameterExpression ExpInpSer = Expression.Parameter(typeof(ISerializer), "serializer");
        static readonly ParameterExpression ExpInpObj = Expression.Parameter(typeof(object), "obj");
        static readonly ParameterExpression ExpInpOptions= Expression.Parameter(typeof(SerializerOptions), "options");


        static readonly ConcurrentDictionary<Type, Func<ISerializer, Object, SerializerOptions, ReadOnlyMemory<Byte>>> TypedSerializers = new ();


    }



    /// <summary>
    /// Helper methods for text based serializers.
    /// </summary>
    public static class TextSerTools
    {

        /// <summary>
        /// Serialize an object to text as its runtime type (from <see cref="Object.GetType"/>) rather than as <see cref="Object"/>, so serializers that add type information for polymorphic values don't do so for the root.
        /// <code>
        ///     SomeType data = new SomeType();
        ///     Object obj = data;
        ///     var a = serializer.ToString(data);
        ///     var b = serializer.ToString(obj);
        ///     var c = serializer.ToStringWithoutType(obj);
        /// </code>
        /// For the above, a and c will be equal, b may contain type information (depending on the serializer).
        /// </summary>
        /// <param name="serializer">The text serializer.</param>
        /// <param name="obj">The object to serialize, if null it is serialized as a null <see cref="Object"/>.</param>
        /// <param name="options">The requested output style.</param>
        /// <returns>The serialized text.</returns>
        /// <remarks>A compiled delegate is created and cached (thread safe) per runtime type on first use.</remarks>
        public static String ToStringWithoutType(this ITextSerializer serializer, Object obj, SerializerOptions options = SerializerOptions.Compact)
        {
            if (obj == null)
                return serializer.ToString(obj, options);
            var type = obj.GetType();
            var cache = TypedSerializers;
            if (!cache.TryGetValue(type, out var fn))
            {
                fn = CreateTypeSerializer(type);
                cache.TryAdd(type, fn);
            }
            return fn(serializer, obj, options);
        }

        static Func<ITextSerializer, Object, SerializerOptions, String> CreateTypeSerializer(Type type)
        {

            var m = MethodSerialize.MakeGenericMethod(type);
            var ser = ExpInpSer;
            var obj = ExpInpObj;
            var opt = ExpInpOptions;
            var fn = Expression.Call(ser, m, Expression.Convert(obj, type), opt);
            return Expression.Lambda<Func<ITextSerializer, Object, SerializerOptions, String>>(fn, ser, obj, opt).Compile();
        }

        static readonly MethodInfo MethodSerialize = typeof(ITextSerializer).GetMethod(nameof(ITextSerializer.ToString));
        static readonly ParameterExpression ExpInpSer = Expression.Parameter(typeof(ITextSerializer), "serializer");
        static readonly ParameterExpression ExpInpObj = Expression.Parameter(typeof(object), "obj");
        static readonly ParameterExpression ExpInpOptions = Expression.Parameter(typeof(SerializerOptions), "options");


        static readonly ConcurrentDictionary<Type, Func<ITextSerializer, Object, SerializerOptions, String>> TypedSerializers = new();

    }


    /// <summary>
    /// Resolves type names used in serialized data (Newtonsoft "$type" names and SysWeaver.Json type names), see <see cref="TypeFinder"/>.
    /// </summary>
    public static class TypeNameResolver
    {
        /// <summary>
        /// Get the type for a trusted type name, see <see cref="TypeFinder.Get"/>.
        /// </summary>
        /// <param name="typeName">The name of the type to find (full name or assembly qualified name).</param>
        /// <returns>The type or null if it can't be found (or <paramref name="typeName"/> is null or empty).</returns>
        /// <remarks>Any type can be resolved, use <see cref="GetForData"/> for names from (untrusted) serialized data.</remarks>
        public static Type Get(String typeName) => TypeFinder.Get(typeName);

        /// <summary>
        /// Get the type for a type name from (untrusted) serialized data, see <see cref="TypeFinder.GetForData"/> and <see cref="DataTypePolicy"/>.
        /// </summary>
        /// <param name="typeName">The name of the type to find (full name or assembly qualified name).</param>
        /// <param name="expectedType">The type that the value must be assignable to (the declared type), null for any type</param>
        /// <returns>The type or null if no type with the name exists (or <paramref name="typeName"/> is null or empty).</returns>
        /// <exception cref="DataTypeNotAllowedException">The type isn't allowed by the <see cref="DataTypePolicy"/>, or it isn't assignable to <paramref name="expectedType"/></exception>
        public static Type GetForData(String typeName, Type expectedType = null) => TypeFinder.GetForData(typeName, expectedType);
    }

}
