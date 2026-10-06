using System;
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


    }

}
