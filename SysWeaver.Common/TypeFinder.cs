using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;

namespace SysWeaver
{
    /// <summary>
    /// Finds types by name, in the assemblies that are loaded and in the assemblies (dll files) in the executable folder.
    /// No other location is ever searched.
    /// </summary>
    /// <remarks>
    /// <see cref="Get"/> is for trusted names (manifests, configuration, code), <see cref="GetForData"/> is for names that comes from (untrusted) serialized data ("$type"),
    /// it resolves the same way but only returns types that are allowed by the <see cref="DataTypePolicy"/>.
    /// <para>
    /// Performance:
    /// <list type="bullet">
    /// <item>Successfully resolved names are cached (at most <see cref="MaxCachedNames"/> names per cache, since the same type can be named in many ways), failed lookups are never cached.</item>
    /// <item>The loaded assemblies are indexed once (by simple name), assemblies loaded later are added using the <see cref="AppDomain.AssemblyLoad"/> event, so they are never scanned again.</item>
    /// <item>The executable folder is listed once (the first time an assembly that isn't loaded is requested), see <see cref="RefreshDiskAssemblies"/>.</item>
    /// <item>A failed lookup costs a few hash lookups (and a <see cref="Assembly.GetType(string, bool)"/> per loaded assembly for names without an assembly), it never touches the disc.</item>
    /// </list>
    /// </para>
    /// Assemblies are loaded into the default load context (<see cref="AssemblyLoadContext.Default"/>), so the types are identical to types loaded by references.
    /// Thread safe.
    /// </remarks>
    public static class TypeFinder
    {
        /// <summary>
        /// Get the type for a given (trusted) type name.
        /// </summary>
        /// <param name="typeName">The name of the type to find, a full type name, ex: "System.String", an assembly qualified name, ex: "MyNamespace.MyType, MyAssembly",
        /// generic types are supported, ex: "System.Collections.Generic.List`1[[MyNamespace.MyType, MyAssembly]]"</param>
        /// <param name="noThrow">If true, always return null instead of throwing</param>
        /// <returns>The type or null if it can't be found (or if <paramref name="typeName"/> is null or empty)</returns>
        /// <remarks>
        /// Names without an assembly are searched for in all loaded assemblies (System.Private.CoreLib first).
        /// An assembly that isn't loaded is loaded from the executable folder ("AssemblyName.dll").
        /// The lookup is case sensitive.
        /// Don't use this for names from untrusted input, use <see cref="GetForData"/>.
        /// </remarks>
        /// <exception cref="FileNotFoundException">If <paramref name="noThrow"/> is false, and the assembly isn't loaded and doesn't exist in the executable folder</exception>
        /// <exception cref="Exception">If <paramref name="noThrow"/> is false, any exception thrown while loading the assembly (ex: FileLoadException, BadImageFormatException) or parsing the name</exception>
        public static Type Get(String typeName, bool noThrow = true)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;
            if (Types.TryGetValue(typeName, out var t))
                return t;
            var ctx = noThrow ? NoThrowContext : new ResolveContext(false);
            t = Resolve(typeName, ctx, false);
            if (t != null)
            {
                AddToCache(Types, typeName, t);
                return t;
            }
            ctx.Error?.Throw();
            return null;
        }

        /// <summary>
        /// Get the type for a type name that comes from (untrusted) serialized data, such as a "$type" member.
        /// </summary>
        /// <param name="typeName">The name of the type (see <see cref="Get"/> for the syntax)</param>
        /// <param name="expectedType">The type that the value must be assignable to (the declared type), null for any type</param>
        /// <returns>The type, or null if no type with that name exists (or if <paramref name="typeName"/> is null or empty)</returns>
        /// <remarks>
        /// Resolves exactly like <see cref="Get"/> (loaded assemblies and the executable folder), with a case insensitive fallback,
        /// and if the assembly doesn't exist, the type is searched for in all loaded assemblies (types that moved to another assembly).
        /// Never throws for unknown types or assemblies, assembly load errors are treated as not found.
        /// Names longer than <see cref="MaxDataTypeNameLength"/> chars or with generic arguments nested deeper than <see cref="MaxDataTypeNameDepth"/> levels are not resolved (returns null),
        /// since every distinct generic instantiation creates a new runtime type that is never released.
        /// </remarks>
        /// <exception cref="DataTypeNotAllowedException">The type exists, but it's not allowed by the <see cref="DataTypePolicy"/>, or it's not assignable to <paramref name="expectedType"/></exception>
        public static Type GetForData(String typeName, Type expectedType = null)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;
            if (!DataTypes.TryGetValue(typeName, out var t))
            {
                if ((typeName.Length > MaxDataTypeNameLength) || (GetGenericDepth(typeName) > MaxDataTypeNameDepth))
                    return null;
                t = Resolve(typeName, NoThrowContext, true);
                if (t == null)
                    return null;
                AddToCache(DataTypes, typeName, t);
            }
            if (!DataTypePolicy.IsAllowed(t))
                throw new DataTypeNotAllowedException(typeName, t, null);
            if ((expectedType != null) && !expectedType.IsAssignableFrom(t))
                throw new DataTypeNotAllowedException(typeName, t, expectedType);
            return t;
        }

        /// <summary>
        /// The maximum number of names in each name cache (the same type can be named in many ways, ex: different casing or version numbers,
        /// so the cache must be bounded when names comes from untrusted input), when full, names are resolved but not cached.
        /// </summary>
        public static int MaxCachedNames { get; set; } = 16384;

        /// <summary>
        /// The maximum length (in chars) of a type name from serialized data, see <see cref="GetForData"/>
        /// </summary>
        public const int MaxDataTypeNameLength = 2048;

        /// <summary>
        /// The maximum nesting depth of generic arguments in a type name from serialized data, see <see cref="GetForData"/>
        /// </summary>
        public const int MaxDataTypeNameDepth = 8;

        /// <summary>
        /// Get the generic nesting depth of a type name ("[[" counts as one level)
        /// </summary>
        static int GetGenericDepth(String typeName)
        {
            int depth = 0, max = 0;
            foreach (var c in typeName)
            {
                if (c == '[')
                {
                    if (++depth > max)
                        max = depth;
                }
                else if (c == ']')
                    --depth;
            }
            return (max + 1) >> 1;
        }

        static void AddToCache(LowAllocConcurrentDictionary<String, Type> cache, String typeName, Type t)
        {
            if (cache.Count < MaxCachedNames)
                cache.TryAdd(typeName, t);
        }

        /// <summary>
        /// List the dll files in the executable folder again.
        /// The folder is only listed once, call this if assemblies are added to the folder while the process is running (and must be found).
        /// </summary>
        public static void RefreshDiskAssemblies()
        {
            lock (LoadLock)
            {
                DiskAssemblies = null;
                FailedLoads.Clear();
            }
        }

        #region Resolve

        /// <summary>
        /// Per call state (a shared instance is used when errors aren't reported)
        /// </summary>
        sealed class ResolveContext
        {
            public ResolveContext(bool noThrow)
            {
                NoThrow = noThrow;
                AssemblyResolver = n => FindAssembly(n.Name, this);
            }

            /// <summary>
            /// If true, errors are not recorded
            /// </summary>
            public readonly bool NoThrow;

            /// <summary>
            /// Resolves assembly names using this context
            /// </summary>
            public readonly Func<AssemblyName, Assembly> AssemblyResolver;

            /// <summary>
            /// The first error (only recorded if <see cref="NoThrow"/> is false)
            /// </summary>
            public ExceptionDispatchInfo Error;

            public void SetError(ExceptionDispatchInfo error)
            {
                if (!NoThrow)
                    Error ??= error;
            }
        }

        static readonly ResolveContext NoThrowContext = new ResolveContext(true);

        static Type Resolve(String typeName, ResolveContext ctx, bool forData)
        {
            try
            {
                var asmResolver = ctx.AssemblyResolver;
                var t = Type.GetType(typeName, asmResolver, TypeResolver, false, false);
                if ((t == null) && forData)
                {
                    t = Type.GetType(typeName, asmResolver, TypeResolver, false, true);
                    //  The assembly doesn't exist, search all loaded assemblies for the type name (without the assembly)
                    if ((t == null) && TrySplitAssemblyQualifiedName(typeName, out var localName, out var asmName) && (FindAssembly(asmName, NoThrowContext) == null))
                        t = Type.GetType(localName, asmResolver, TypeResolver, false, false) ?? Type.GetType(localName, asmResolver, TypeResolver, false, true);
                }
                return t;
            }
            catch (Exception ex)
            {
                //  Malformed names etc
                ctx.SetError(ExceptionDispatchInfo.Capture(ex));
                return null;
            }
        }

        static readonly Func<Assembly, String, bool, Type> TypeResolver = (asm, name, ignoreCase) =>
        {
            if (asm != null)
                return asm.GetType(name, false, ignoreCase);
            //  No assembly, search all loaded assemblies (corelib first)
            var t = CoreLib.GetType(name, false, ignoreCase);
            if (t != null)
                return t;
            foreach (var a in AssemblyList)
            {
                t = a.GetType(name, false, ignoreCase);
                if (t != null)
                    return t;
            }
            return null;
        };

        static readonly Assembly CoreLib = typeof(Object).Assembly;

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
                return (localName.Length > 0) && (asmName.Length > 0);
            }
            localName = null;
            asmName = null;
            return false;
        }

        /// <summary>
        /// Successfully resolved trusted type names (exact match only), at most <see cref="MaxCachedNames"/> entries
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, Type> Types = new (StringComparer.Ordinal);

        /// <summary>
        /// Successfully resolved data type names (including the case insensitive and moved assembly fallbacks), at most <see cref="MaxCachedNames"/> entries.
        /// Separate from <see cref="Types"/>, so that the fallbacks never affects trusted lookups.
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, Type> DataTypes = new (StringComparer.Ordinal);

        #endregion//Resolve

        #region Assemblies

        static TypeFinder()
        {
            //  Subscribe first, so that no assembly is missed
            var d = AppDomain.CurrentDomain;
            d.AssemblyLoad += (_, e) => AddAssembly(e.LoadedAssembly);
            foreach (var a in d.GetAssemblies())
                AddAssembly(a);
        }

        /// <summary>
        /// Add an assembly to the index (the first assembly with a given simple name wins)
        /// </summary>
        static void AddAssembly(Assembly a)
        {
            if (a == null)
                return;
            var name = a.GetName().Name;
            if (String.IsNullOrEmpty(name))
                return;
            if (!Assemblies.TryAdd(name, a))
                return;
            if (a.IsDynamic)
                return;
            lock (AssemblyListLock)
                AssemblyList = [.. AssemblyList, a];
        }

        /// <summary>
        /// Find a loaded assembly, or load it from the executable folder
        /// </summary>
        static Assembly FindAssembly(String name, ResolveContext ctx)
        {
            if (String.IsNullOrEmpty(name))
                return null;
            if (Assemblies.TryGetValue(name, out var a))
                return a;
            return LoadFromExecutableFolder(name, ctx);
        }

        static Assembly LoadFromExecutableFolder(String name, ResolveContext ctx)
        {
            if (!PathExt.IsValidFilename(name))
                return null;
            var fileName = Path.Combine(EnvInfo.ExecutableDir, name + ".dll");
            var disk = DiskAssemblies ?? ListDiskAssemblies();
            if (!disk.Contains(name))
            {
                ctx.SetError(ExceptionDispatchInfo.Capture(new FileNotFoundException("Assembly \"" + name + "\" is not loaded and doesn't exist in the executable folder", fileName)));
                return null;
            }
            if (FailedLoads.TryGetValue(name, out var err))
            {
                ctx.SetError(err);
                return null;
            }
            lock (LoadLock)
            {
                if (Assemblies.TryGetValue(name, out var a))
                    return a;
                if (FailedLoads.TryGetValue(name, out err))
                {
                    ctx.SetError(err);
                    return null;
                }
                try
                {
                    a = AssemblyLoadContext.Default.LoadFromAssemblyPath(fileName);
                    AddAssembly(a);
                    return a;
                }
                catch (Exception ex)
                {
                    //  Not a managed assembly etc, not tried again (until RefreshDiskAssemblies is called)
                    err = ExceptionDispatchInfo.Capture(ex);
                    FailedLoads.TryAdd(name, err);
                    ctx.SetError(err);
                    return null;
                }
            }
        }

        static IReadOnlySet<String> ListDiskAssemblies()
        {
            lock (LoadLock)
            {
                var d = DiskAssemblies;
                if (d != null)
                    return d;
                List<String> names;
                try
                {
                    names = Directory.EnumerateFiles(EnvInfo.ExecutableDir, "*.dll").Select(Path.GetFileNameWithoutExtension).ToList();
                }
                catch
                {
                    names = [];
                }
                d = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase, names);
                DiskAssemblies = d;
                return d;
            }
        }

        /// <summary>
        /// Loaded assemblies by simple name
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, Assembly> Assemblies = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Loaded (non dynamic) assemblies, used to search for types without an assembly name (copy on write)
        /// </summary>
        static volatile Assembly[] AssemblyList = [];

        static readonly Object AssemblyListLock = new Object();

        /// <summary>
        /// The names (without extension) of the dll files in the executable folder, null until the folder is listed
        /// </summary>
        static volatile IReadOnlySet<String> DiskAssemblies;

        /// <summary>
        /// Assemblies in the executable folder that failed to load (bounded by the number of dll files)
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, ExceptionDispatchInfo> FailedLoads = new (StringComparer.OrdinalIgnoreCase);

        static readonly Object LoadLock = new Object();

        #endregion//Assemblies
    }

}
