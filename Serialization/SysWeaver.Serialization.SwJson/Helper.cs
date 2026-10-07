using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SysWeaver.Serialization.SwJson
{
    /// <summary>
    /// Reflection helpers used when building the reader/writer expression trees.
    /// In DEBUG builds a missing (or ambiguous) method throws an exception naming the method and type, in release builds these are plain <see cref="Type.GetMethod(string)"/> calls (that return null for a missing method).
    /// </summary>
    static class Helper
    {
#if DEBUG

        /// <summary>
        /// Get a public method by name, see <see cref="Type.GetMethod(string)"/>.
        /// </summary>
        /// <exception cref="Exception">The method wasn't found or is ambiguous</exception>
        public static MethodInfo SafeGetMethod(Type t, String name)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\"", ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\"");
            return m;
        }

        /// <summary>
        /// Get a method by name and binding flags, see <see cref="Type.GetMethod(string, BindingFlags)"/>.
        /// </summary>
        /// <exception cref="Exception">The method wasn't found or is ambiguous</exception>
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\" with flags " + b, ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\" with flags " + b);
            return m;
        }

        /// <summary>
        /// Get a public method by name and parameter types, see <see cref="Type.GetMethod(string, Type[])"/>.
        /// </summary>
        /// <exception cref="Exception">The method wasn't found</exception>
        public static MethodInfo SafeGetMethod(Type t, String name, params Type[] b)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\" with argument types: " + String.Join(", ", b.Select(x => x.CleanTypename())), ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\" with argument types: " + String.Join(", ", b.Select(x => x.CleanTypename())));
            return m;
        }

        /// <summary>
        /// Get a method by name, binding flags and parameter types, see <see cref="Type.GetMethod(string, BindingFlags, Type[])"/>.
        /// </summary>
        /// <exception cref="Exception">The method wasn't found</exception>
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, params Type[] types)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b, types);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\" with flags " + b + ", argument types: " + String.Join(", ", types.Select(x => x.CleanTypename())), ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.CleanTypename() + "\" with flags " + b + ", argument types: " + String.Join(", ", types.Select(x => x.CleanTypename())));
            return m;
        }

        /// <summary>
        /// Get a method, see <see cref="Type.GetMethod(string, BindingFlags, Binder, Type[], ParameterModifier[])"/>.
        /// </summary>
        /// <exception cref="Exception">The method wasn't found</exception>
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, Binder binder, Type[] types, ParameterModifier[] mods)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b, binder, types, mods);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with flags " + b + ", argument types: " + String.Join(", ", types.Select(x => x.FullName)), ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with flags " + b + ", argument types: " + String.Join(", ", types.Select(x => x.FullName)));
            return m;
        }

#else //DEBUG

        /// <summary>
        /// Get a public method by name, see <see cref="Type.GetMethod(string)"/> (null if not found).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name)
        {
            return t.GetMethod(name);
        }

        /// <summary>
        /// Get a method by name and binding flags, see <see cref="Type.GetMethod(string, BindingFlags)"/> (null if not found).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b)
        {
            return t.GetMethod(name, b);
        }

        /// <summary>
        /// Get a public method by name and parameter types, see <see cref="Type.GetMethod(string, Type[])"/> (null if not found).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, params Type[] b)
        {
            return t.GetMethod(name, b);
        }

        /// <summary>
        /// Get a method by name, binding flags and parameter types, see <see cref="Type.GetMethod(string, BindingFlags, Type[])"/> (null if not found).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, params Type[] types)
        {
            return t.GetMethod(name, b, types);
        }

        /// <summary>
        /// Get a method, see <see cref="Type.GetMethod(string, BindingFlags, Binder, Type[], ParameterModifier[])"/> (null if not found).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, Binder binder, Type[] types, ParameterModifier[] mods)
        {
            return t.GetMethod(name, b, binder, types, mods);
        }

#endif //DEBUG

        static readonly ConcurrentDictionary<Type, Type> CreatedTypes = new();

        /// <summary>
        /// The type the reader creates for a value declared as <paramref name="declaredType"/> when the json has no <c>"$type"</c>, for collection and dictionary interfaces:
        /// a <see cref="List{T}"/> for interfaces it implements (<see cref="IList{T}"/>, <see cref="ICollection{T}"/>, <see cref="IEnumerable{T}"/>, <see cref="IReadOnlyList{T}"/>, <see cref="IReadOnlyCollection{T}"/>),
        /// a <see cref="HashSet{T}"/> for other collection interfaces it implements (<see cref="ISet{T}"/>)
        /// and a <see cref="Dictionary{TKey, TValue}"/> for <see cref="IDictionary{TKey, TValue}"/> and <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
        /// </summary>
        /// <remarks>
        /// The writer omits the <c>"$type"</c> of a value of exactly this type, so it must match what the reader creates (see JsonReader.CollectionFactory, JsonReader.EnumerableFactory and JsonReader.DictionaryInterface).
        /// </remarks>
        /// <param name="declaredType">The declared type</param>
        /// <returns>The type that is created, or null if the declared type isn't such an interface (the type is created as is, or needs a <c>"$type"</c>)</returns>
        public static Type GetCreatedType(Type declaredType)
        {
            //  typeof(void) is cached for "none" (a ConcurrentDictionary can't store null values)
            var c = CreatedTypes.GetOrAdd(declaredType, static t => FindCreatedType(t) ?? typeof(void));
            return c == typeof(void) ? null : c;
        }

        static Type FindCreatedType(Type t)
        {
            if (!(t.IsInterface && t.IsGenericType))
                return null;
            var args = t.GetGenericArguments();
            if (args.Length == 1)
            {
                var lt = typeof(List<>).MakeGenericType(args);
                if (t.IsAssignableFrom(lt))
                    return lt;
                var ht = typeof(HashSet<>).MakeGenericType(args);
                if (typeof(ICollection<>).MakeGenericType(args).IsAssignableFrom(t) && t.IsAssignableFrom(ht))
                    return ht;
                return null;
            }
            if (args.Length == 2)
            {
                var gt = t.GetGenericTypeDefinition();
                if ((gt == typeof(IDictionary<,>)) || (gt == typeof(IReadOnlyDictionary<,>)))
                    return typeof(Dictionary<,>).MakeGenericType(args);
            }
            return null;
        }

    }

}
