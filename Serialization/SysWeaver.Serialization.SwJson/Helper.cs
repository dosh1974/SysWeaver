using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SysWeaver.Serialization.SwJson
{
    static class Helper
    {
#if DEBUG

        public static MethodInfo SafeGetMethod(Type t, String name)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\"", ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\"");
            return m;
        }

        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with flags " + b, ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with flags " + b);
            return m;
        }

        public static MethodInfo SafeGetMethod(Type t, String name, params Type[] b)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with argument types: " + String.Join(", ", b.Select(x => x.FullName)), ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with argument types: " + String.Join(", ", b.Select(x => x.FullName)));
            return m;
        }

        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, params Type[] types)
        {
            MethodInfo m;
            try
            {
                m = t.GetMethod(name, b, types);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with flags " + b + ", argument types: " + String.Join(", ", types.Select(x => x.FullName)), ex);
            }
            if (m == null)
                throw new Exception("Failed to get method \"" + name + "\" for type \"" + t.FullName + "\" with flags " + b + ", argument types: " + String.Join(", ", types.Select(x => x.FullName)));
            return m;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name)
        {
            return t.GetMethod(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b)
        {
            return t.GetMethod(name, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, params Type[] b)
        {
            return t.GetMethod(name, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, params Type[] types)
        {
            return t.GetMethod(name, b, types);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo SafeGetMethod(Type t, String name, BindingFlags b, Binder binder, Type[] types, ParameterModifier[] mods)
        {
            return t.GetMethod(name, b, binder, types, mods);
        }

#endif //DEBUG


    }

}
