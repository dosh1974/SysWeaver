using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

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
            foreach (var asm in AppDomain.CurrentDomain?.GetAssemblies()?.Nullable())
            {
                if (asm == null)
                    continue;
                t = asm.GetType(typeName, false);
                if (t != null)
                {
                    types.TryAdd(typeName, t);
                    return t;
                }
            }

            var s = typeName.LastIndexOf(',');
            if (s >= 0)
            {
                var asmName = typeName.Substring(s + 1).TrimStart();
                if (PathExt.IsValidFilename(asmName))
                {
                    var dllName = Path.Combine(EnvInfo.ExecutableDir, asmName + ".dll");
                    var nasm = Assembly.LoadFile(dllName);
                    t = nasm.GetType(typeName.Substring(0, s).TrimEnd());
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
