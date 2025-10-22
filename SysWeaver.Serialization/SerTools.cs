using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace SysWeaver.Serialization
{
    public static class SerTools
    {
        public static ReadOnlyMemory<Byte> ToUTF8(this String st)
        {
            var l = st.Length << 1;
            l += 64;
            Byte[] buf;
            int bufUse;
            for (; ; )
            {
                try
                {
                    buf = GC.AllocateUninitializedArray<Byte>(l);
                    bufUse = Encoding.UTF8.GetBytes(st, buf);
                    break;
                }
                catch (ArgumentException)
                {
                    l += (l >> 1);
                }
            }
            return new ReadOnlyMemory<byte>(buf, 0, bufUse);
        }


        /// <summary>
        /// Add char-set to a mime if it exist
        /// </summary>
        /// <param name="mime"></param>
        /// <param name="enc"></param>
        /// <returns></returns>
        public static String MakeHeader(String mime, Encoding enc)
        {
            var e = enc?.HeaderName;
            return e == null ? mime : String.Join("; charset=", mime, e);
        }





        /// <summary>
        /// Serialize an object as if if was of it's own type, thus without type information (uses GetType() to determine the type and then serialize as that.
        /// Example:
        ///     SomeType data = new SomeType();
        ///     Object obj = data;
        ///     var a = serializer.Serialize(data);
        ///     var b = serializer.Serialize(obj);
        ///     var c = serializer.SerializeWithoutType(obj);
        /// For the above, a and c will be equal, b will contain type information.
        /// </summary>
        /// <param name="serializer">The serializer</param>
        /// <param name="obj">The object to serialize</param>
        /// <param name="options">The object to serialize</param>
        /// <returns>The serialized data</returns>
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



    public static class TextSerTools
    {

        /// <summary>
        /// Serialize an object as if if was of it's own type, thus without type information (uses GetType() to determine the type and then serialize as that.
        /// Example:
        ///     SomeType data = new SomeType();
        ///     Object obj = data;
        ///     var a = serializer.Serialize(data);
        ///     var b = serializer.Serialize(obj);
        ///     var c = serializer.SerializeWithoutType(obj);
        /// For the above, a and c will be equal, b will contain type information.
        /// </summary>
        /// <param name="serializer">The serializer</param>
        /// <param name="obj">The object to serialize</param>
        /// <param name="options">The object to serialize</param>
        /// <returns>The serialized data</returns>
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
    /// Try (real hard) to find a type for the given type name
    /// </summary>
    public static class TypeNameResolver
    {

        static Assembly LastAsm;


        /// <summary>
        /// Get the type for a given type name or null if it can't be found
        /// </summary>
        /// <param name="typeName">The name of the type to find</param>
        /// <returns>The type or null if it can't be found</returns>
        public static Type Get(String typeName)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;
            var types = Types;
            if (types.TryGetValue(typeName, out var t))
                return t;
            t = Type.GetType(typeName, false);
            if (t != null)
            {
                types.TryAdd(typeName, t);
                return t;
            }
            t = Type.GetType(typeName, false, true);
            if (t != null)
            {
                types.TryAdd(typeName, t);
                return t;
            }
            var la = LastAsm;
            if (la != null)
            {
                t = la.GetType(typeName, false);
                if (t != null)
                {
                    types.TryAdd(typeName, t);
                    return t;
                }
                t = la.GetType(typeName, false, true);
                if (t != null)
                {
                    types.TryAdd(typeName, t);
                    return t;
                }
            }
            // TODO: Handle generics
            var s = typeName.LastIndexOf(',');
            var tt = s < 0 ? typeName : typeName.Substring(0, s).TrimEnd();
            var cdAsms = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in cdAsms)
            {
                t = asm.GetType(tt, false);
                if (t != null)
                {
                    types.TryAdd(typeName, t);
                    LastAsm = asm;
                    return t;
                }
            }
            foreach (var asm in cdAsms)
            {
                t = asm.GetType(tt, false, true);
                if (t != null)
                {
                    types.TryAdd(typeName, t);
                    LastAsm = asm;
                    return t;
                }
            }
            types.TryAdd(typeName, null);
            return null;
        }

        static readonly ConcurrentDictionary<String, Type> Types = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

    }

}
