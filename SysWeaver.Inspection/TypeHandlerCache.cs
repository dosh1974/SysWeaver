using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading;

namespace SysWeaver.Inspection.Implementation
{

    /// <summary>
    /// The cached type handlers for values declared as <typeparamref name="T"/>: one for <typeparamref name="T"/> itself, and one per runtime type (derived types, implementations of an interface, boxed values).
    /// Handlers are created on first use (compiled expression trees) and never released. Thread safe.
    /// </summary>
    /// <typeparam name="T">The declared type</typeparam>
    public static class TypeHandlerCache<T>
    {

        /// <summary>
        /// True for Nullable&lt;U&gt;, the run time type of a value is U (not a derived type), so the handler of T is always used
        /// </summary>
        static readonly bool IsNullableValueType = System.Nullable.GetUnderlyingType(typeof(T)) != null;

        /// <summary>
        /// Get the handler for a value: the handler of <typeparamref name="T"/> if the value is null, a Nullable&lt;U&gt; or of type <typeparamref name="T"/>, else the handler of the runtime type
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The handler</returns>
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

        /// <summary>
        /// Get the handler for values of a runtime type (that is assignable to <typeparamref name="T"/>), the handler is cached per type.
        /// Note: for <paramref name="type"/> == <typeparamref name="T"/> this returns another instance than <see cref="GetTypeHandler"/>.
        /// </summary>
        /// <param name="type">The runtime type</param>
        /// <returns>The handler</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null (DEBUG builds only)</exception>
        /// <exception cref="Exception">The handler can't be created (ex: the type isn't assignable to <typeparamref name="T"/>), the inner exception has the details</exception>
        public static TypeHandler<T> GetHandler(Type type)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(type);
#endif//DEBUG
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

        /// <summary>
        /// Get the handler for a value (same as <see cref="GetHandler(T)"/>)
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The handler</returns>
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

        /// <summary>
        /// Get the handler of <typeparamref name="T"/> itself (always the same instance)
        /// </summary>
        /// <returns>The handler</returns>
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

