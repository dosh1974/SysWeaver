using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Finds types by name, in the assemblies that are loaded, in the assemblies (dll files) in the executable folder
    /// and in the trusted platform assemblies of the runtime (the framework and app assemblies the host lists in "TRUSTED_PLATFORM_ASSEMBLIES", the assemblies that default probing would load).
    /// No other location is ever searched.
    /// Assemblies loaded into a collectible <see cref="AssemblyLoadContext"/> (ex: scripts) are never searched, so that they can be unloaded.
    /// </summary>
    /// <remarks>
    /// <see cref="Get"/> is for trusted names (manifests, configuration, code), <see cref="GetForData"/> is for names that comes from (untrusted) serialized data ("$type"),
    /// it resolves the same way but only returns types that are allowed by the <see cref="DataTypePolicy"/>.
    /// <para>
    /// Performance:
    /// <list type="bullet">
    /// <item>Successfully resolved names are cached (at most <see cref="MaxCachedNames"/> names per cache, since the same type can be named in many ways).</item>
    /// <item>Failed lookups are cached in separate caches (at most <see cref="MaxCachedNames"/> names per cache), that are cleared whenever an assembly is loaded (or <see cref="RefreshDiskAssemblies"/> is called), since a name may resolve after that.</item>
    /// <item>The loaded assemblies are indexed once (by simple name), assemblies loaded later are added using the <see cref="AppDomain.AssemblyLoad"/> event, so they are never scanned again.</item>
    /// <item>The executable folder is listed once (the first time an assembly that isn't loaded is requested), see <see cref="RefreshDiskAssemblies"/>.</item>
    /// <item>The trusted platform assemblies list is parsed once (the first time an assembly that isn't loaded, nor in the executable folder, is requested).</item>
    /// <item>A failed lookup that isn't cached costs a few hash lookups (and a <see cref="Assembly.GetType(string, bool)"/> per loaded assembly for names without an assembly), it never touches the disc.</item>
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
        /// Names without an assembly are searched for in all loaded (non collectible) assemblies (System.Private.CoreLib first).
        /// An assembly that isn't loaded is loaded from the executable folder ("AssemblyName.dll"), or if it's not there, from the trusted platform assemblies (ex: framework assemblies that aren't loaded yet).
        /// The lookup is case sensitive.
        /// Failed lookups are cached (until an assembly is loaded), when <paramref name="noThrow"/> is false the name is always resolved again (so that the error can be thrown).
        /// Don't use this for names from untrusted input, use <see cref="GetForData"/>.
        /// </remarks>
        /// <exception cref="FileNotFoundException">If <paramref name="noThrow"/> is false, and the assembly isn't loaded, doesn't exist in the executable folder and isn't a trusted platform assembly</exception>
        /// <exception cref="Exception">If <paramref name="noThrow"/> is false, any exception thrown while loading the assembly (ex: FileLoadException, BadImageFormatException) or parsing the name</exception>
        public static Type Get(String typeName, bool noThrow = true)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;
            if (Types.TryGetValue(typeName, out var t))
                return t;
            if (noThrow && NotFoundTypes.ContainsKey(typeName))
                return null;
            var gen = Volatile.Read(ref NotFoundGeneration);
            var ctx = noThrow ? NoThrowContext : new ResolveContext(false);
            t = Resolve(typeName, ctx, false);
            if (t != null)
            {
                AddToCache(Types, typeName, t);
                return t;
            }
            AddNotFound(NotFoundTypes, typeName, gen);
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
        /// Resolves exactly like <see cref="Get"/> (loaded assemblies, the executable folder and the trusted platform assemblies), with a case insensitive fallback,
        /// and if the type isn't found in the named assembly (or the assembly doesn't exist), the type name (without the assembly) is searched for in all loaded assemblies (types that moved to another assembly).
        /// The <see cref="DataTypePolicy"/> and <paramref name="expectedType"/> checks always applies to the resolved type (including the fallbacks).
        /// Never throws for unknown types or assemblies, assembly load errors are treated as not found.
        /// Failed lookups are cached (until an assembly is loaded).
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
                if (NotFoundDataTypes.ContainsKey(typeName))
                    return null;
                var gen = Volatile.Read(ref NotFoundGeneration);
                t = Resolve(typeName, NoThrowContext, true);
                if (t == null)
                {
                    AddNotFound(NotFoundDataTypes, typeName, gen);
                    return null;
                }
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
        /// Cache a failed lookup, unless the failed lookup cache was cleared since the lookup started
        /// </summary>
        /// <param name="cache">The failed lookup cache</param>
        /// <param name="typeName">The name that failed to resolve</param>
        /// <param name="gen">The value of <see cref="NotFoundGeneration"/> before the lookup started</param>
        static void AddNotFound(LowAllocConcurrentDictionary<String, bool> cache, String typeName, int gen)
        {
            if (cache.Count >= MaxCachedNames)
                return;
            cache.TryAdd(typeName, true);
            //  An assembly was loaded (or the disk refreshed) after the lookup started, the name might resolve now.
            //  The generation is incremented before the caches are cleared, so either this sees the new generation, or the clear runs after the add.
            if (gen != Volatile.Read(ref NotFoundGeneration))
                cache.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Clear the failed lookup caches (called when an assembly is loaded or the executable folder is refreshed)
        /// </summary>
        static void ClearNotFound()
        {
            Interlocked.Increment(ref NotFoundGeneration);
            NotFoundTypes.Clear();
            NotFoundDataTypes.Clear();
        }

        /// <summary>
        /// List the dll files in the executable folder again.
        /// The folder is only listed once, call this if assemblies are added to the folder while the process is running (and must be found).
        /// Also clears the failed lookup caches and the failed assembly loads.
        /// </summary>
        public static void RefreshDiskAssemblies()
        {
            lock (LoadLock)
            {
                DiskAssemblies = null;
                FailedLoads.Clear();
            }
            ClearNotFound();
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
                    //  The type isn't in the named assembly (or the assembly doesn't exist), search all loaded assemblies for the type name (without the assembly), types that moved to another assembly
                    if ((t == null) && TrySplitAssemblyQualifiedName(typeName, out var localName, out _))
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

        /// <summary>
        /// Trusted type names that failed to resolve, at most <see cref="MaxCachedNames"/> entries, cleared when an assembly is loaded (see <see cref="ClearNotFound"/>).
        /// Separate from <see cref="Types"/>, so that failed lookups never prevents successful lookups from being cached.
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, bool> NotFoundTypes = new (StringComparer.Ordinal);

        /// <summary>
        /// Data type names that failed to resolve, at most <see cref="MaxCachedNames"/> entries, cleared when an assembly is loaded (see <see cref="ClearNotFound"/>).
        /// Separate from <see cref="DataTypes"/>, so that failed lookups (from untrusted input) never prevents successful lookups from being cached.
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, bool> NotFoundDataTypes = new (StringComparer.Ordinal);

        /// <summary>
        /// Incremented (before the failed lookup caches are cleared) every time the failed lookup caches are cleared
        /// </summary>
        static int NotFoundGeneration;

        #endregion//Resolve

        #region Assemblies

        static TypeFinder()
        {
            //  Subscribe first, so that no assembly is missed
            var d = AppDomain.CurrentDomain;
            d.AssemblyLoad += (_, e) => OnAssemblyLoad(e.LoadedAssembly);
            foreach (var a in d.GetAssemblies())
                AddAssembly(a);
        }

        /// <summary>
        /// Index a newly loaded assembly, and clear the failed lookup caches (names might resolve now)
        /// </summary>
        static void OnAssemblyLoad(Assembly a)
        {
            if ((a == null) || a.IsCollectible)
                return;
            AddAssembly(a);
            ClearNotFound();
        }

        /// <summary>
        /// Add an assembly to the index (the first assembly with a given simple name wins).
        /// Collectible assemblies are ignored, since the index would keep them (and their load context) alive forever.
        /// </summary>
        static void AddAssembly(Assembly a)
        {
            if ((a == null) || a.IsCollectible)
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
        /// Find a loaded (non collectible) assembly, or load it from the executable folder, or load it if it's a trusted platform assembly
        /// </summary>
        static Assembly FindAssembly(String name, ResolveContext ctx)
        {
            if (String.IsNullOrEmpty(name))
                return null;
            if (Assemblies.TryGetValue(name, out var a))
                return a;
            if (!PathExt.IsValidFilename(name))
                return null;
            var fileName = Path.Combine(EnvInfo.ExecutableDir, name + ".dll");
            var disk = DiskAssemblies ?? ListDiskAssemblies();
            if (disk.Contains(name))
                return LoadAssembly(name, fileName, ctx);
            var platform = PlatformAssemblies ?? ListPlatformAssemblies();
            if (platform.Contains(name))
                return LoadAssembly(name, null, ctx);
            ctx.SetError(ExceptionDispatchInfo.Capture(new FileNotFoundException("Assembly \"" + name + "\" is not loaded, doesn't exist in the executable folder and isn't a trusted platform assembly", fileName)));
            return null;
        }

        /// <summary>
        /// Load an assembly into the default load context (once, failures are remembered in <see cref="FailedLoads"/>)
        /// </summary>
        /// <param name="name">The simple name of the assembly</param>
        /// <param name="fileName">The dll file in the executable folder, or null to load a trusted platform assembly by name</param>
        /// <param name="ctx">The context that receives any error</param>
        static Assembly LoadAssembly(String name, String fileName, ResolveContext ctx)
        {
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
                    a = fileName != null
                        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(fileName)
                        : AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
                    AddAssembly(a);
                    return a;
                }
                catch (Exception ex)
                {
                    //  Not a managed assembly, missing file etc, not tried again (until RefreshDiskAssemblies is called)
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

        static IReadOnlySet<String> ListPlatformAssemblies()
        {
            lock (LoadLock)
            {
                var d = PlatformAssemblies;
                if (d != null)
                    return d;
                List<String> names;
                try
                {
                    names = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as String)?
                        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(x => !String.IsNullOrEmpty(x))
                        .ToList() ?? [];
                }
                catch
                {
                    names = [];
                }
                d = ReadOnlyData.Set(StringComparer.OrdinalIgnoreCase, names);
                PlatformAssemblies = d;
                return d;
            }
        }

        /// <summary>
        /// Loaded assemblies by simple name
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, Assembly> Assemblies = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Loaded (non dynamic, non collectible) assemblies, used to search for types without an assembly name (copy on write)
        /// </summary>
        static volatile Assembly[] AssemblyList = [];

        static readonly Object AssemblyListLock = new Object();

        /// <summary>
        /// The names (without extension) of the dll files in the executable folder, null until the folder is listed
        /// </summary>
        static volatile IReadOnlySet<String> DiskAssemblies;

        /// <summary>
        /// The simple names of the trusted platform assemblies (from "TRUSTED_PLATFORM_ASSEMBLIES"), null until first needed.
        /// The list is fixed for the lifetime of the process.
        /// </summary>
        static volatile IReadOnlySet<String> PlatformAssemblies;

        /// <summary>
        /// Assemblies in the executable folder or trusted platform assemblies that failed to load (bounded by the number of dll files and platform assemblies)
        /// </summary>
        static readonly LowAllocConcurrentDictionary<String, ExceptionDispatchInfo> FailedLoads = new (StringComparer.OrdinalIgnoreCase);

        static readonly Object LoadLock = new Object();

        #endregion//Assemblies
    }

}
