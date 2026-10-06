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
        /// Get the type for a given type name or null if it can't be found.
        /// The type is searched for using Type.GetType, then in all loaded assemblies and finally (if the name is assembly qualified) by loading the assembly from the executable folder.
        /// The result is cached (also if not found), except when an exception is thrown.
        /// </summary>
        /// <param name="typeName">The name of the type to find, a full type name, ex: "System.String" or an assembly qualified name, ex: "MyNamespace.MyType, MyAssembly"</param>
        /// <param name="noThrow">If true, always return null instead of throwing</param>
        /// <returns>The type or null if it can't be found (or if <paramref name="typeName"/> is null or empty)</returns>
        /// <exception cref="FileNotFoundException">If <paramref name="noThrow"/> is false, and the assembly file doesn't exist</exception>
        /// <exception cref="Exception">If <paramref name="noThrow"/> is false, any other exception thrown while finding the type or loading the assembly (ex: FileLoadException, BadImageFormatException)</exception>
        public static Type Get(String typeName, bool noThrow = true)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;
            var types = Types;
            if (types.TryGetValue(typeName, out var t))
                return t;
            try
            {
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

                if (TrySplitAssemblyQualifiedName(typeName, out var localName, out var asmName))
                {
                    if (PathExt.IsValidFilename(asmName))
                    {
                        var dllName = Path.Combine(EnvInfo.ExecutableDir, asmName + ".dll");
                        var nasm = Assembly.LoadFile(dllName);
                        t = nasm.GetType(localName);
                        types.TryAdd(typeName, t);
                        return t;
                    }
                }
                types.TryAdd(typeName, null);
                return null;
            }
            catch
            {
                if (noThrow)
                    return null;
                throw;
            }
        }

        /// <summary>
        /// Split an assembly qualified type name, "TypeName, AssemblyName[, Version=.., Culture=.., PublicKeyToken=..]" into the type name and the (simple) assembly name.
        /// Commas inside of brackets (generic type arguments) are ignored.
        /// </summary>
        /// <param name="typeName">The (possibly) assembly qualified type name</param>
        /// <param name="localName">The type name (without the assembly)</param>
        /// <param name="asmName">The simple name of the assembly</param>
        /// <returns>True if the name contains an assembly name</returns>
        static bool TrySplitAssemblyQualifiedName(String typeName, out String localName, out String asmName)
        {
            int depth = 0;
            var l = typeName.Length;
            for (int i = 0; i < l; ++i)
            {
                var c = typeName[i];
                if (c == '[')
                {
                    ++depth;
                    continue;
                }
                if (c == ']')
                {
                    --depth;
                    continue;
                }
                if ((c != ',') || (depth != 0))
                    continue;
                localName = typeName.Substring(0, i).TrimEnd();
                var rest = typeName.AsSpan(i + 1);
                var e = rest.IndexOf(',');
                if (e >= 0)
                    rest = rest.Slice(0, e);
                asmName = rest.Trim().ToString();
                return true;
            }
            localName = null;
            asmName = null;
            return false;
        }

        static readonly ConcurrentDictionary<String, Type> Types = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

    }

}
