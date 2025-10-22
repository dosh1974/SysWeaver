using System;
using System.Collections.Concurrent;

namespace SysWeaver
{
    /// <summary>
    /// Try (real hard) to find a type for the given type name
    /// </summary>
    public static class TypeFinder
    {

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
            t = Type.GetType(typeName);
            if (t != null)
            {
                types.TryAdd(typeName, t);
                return t;
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName, false);
                if (t != null)
                {
                    types.TryAdd(typeName, t);
                    return t;
                }
            }
            types.TryAdd(typeName, null);
            return null;
        }

        static readonly ConcurrentDictionary<String, Type> Types = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

    }

}
