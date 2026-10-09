using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading;

namespace SysWeaver.Inspection.Implementation
{

    public static class TypeHandlerCache<T>
    {

        /// <summary>
        /// True for Nullable&lt;U&gt;, the run time type of a value is U (not a derived type), so the handler of T is always used
        /// </summary>
        static readonly bool IsNullableValueType = System.Nullable.GetUnderlyingType(typeof(T)) != null;

        public static TypeHandler<T> GetHandler(T value)
        {
            if (IsNullableValueType)
                return Handler;
            Type type = typeof(T);
            bool nullable = HelpersTypeHandler.IsNullable(type);
            if (nullable && (value != null))
                type = value.GetType();
            if (type == typeof(T))  
                return Handler;
            TypeHandler<T> handler;
            if (Handlers.TryGetValue(type, out handler))
                return handler;
            handler = new TypeHandler<T>(type, nullable, Interlocked.Increment(ref StaticTypeHandler.TypeCount));
            if (!Handlers.TryAdd(type, handler))
                handler = Handlers[type];
            return handler;
        }

        public static TypeHandler<T> GetHandler(Type type)
        {
            bool nullable = HelpersTypeHandler.IsNullable(type);
            //System.Diagnostics.Debug.Assert(type != typeof(T));
            TypeHandler<T> handler;
            if (Handlers.TryGetValue(type, out handler))
                return handler;
            handler = new TypeHandler<T>(type, nullable, Interlocked.Increment(ref StaticTypeHandler.TypeCount));
            if (!Handlers.TryAdd(type, handler))
                handler = Handlers[type];
            return handler;
        }

        public static TypeHandler<T> GetHandler(ref T value)
        {
            if (IsNullableValueType)
                return Handler;
            var tt = typeof(T);
            Type type = value?.GetType() ?? tt;
            if (type == tt)
                return Handler;
            TypeHandler<T> handler;
            if (Handlers.TryGetValue(type, out handler))
                return handler;
            bool nullable = HelpersTypeHandler.IsNullable(type);
            handler = new TypeHandler<T>(type, nullable, Interlocked.Increment(ref StaticTypeHandler.TypeCount));
            if (!Handlers.TryAdd(type, handler))
                handler = Handlers[type];
            return handler;
        }

        public static TypeHandler<T> GetTypeHandler()
        {
            return Handler;
        }

        static TypeHandlerCache()
        {
            Handlers = new ConcurrentDictionary<Type, TypeHandler<T>>();
            Handler = new TypeHandler<T>(typeof(T), HelpersTypeHandler.IsNullable(typeof(T)), Interlocked.Increment(ref StaticTypeHandler.TypeCount));
        }

        static readonly ConcurrentDictionary<Type, TypeHandler<T>> Handlers;
        static readonly TypeHandler<T> Handler;




    }

}

