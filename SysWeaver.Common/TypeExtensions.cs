using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;

namespace SysWeaver
{
    
    /// <summary>
    /// Add some extension methods to the reflection types
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Enumerates all fields of this type, including inherited fields
        /// </summary>
        /// <param name="ti">The type of interest</param>
        /// <returns>All fields</returns>
        public static IEnumerable<FieldInfo> AllFields(this TypeInfo ti)
        {
            while (ti != null)
            {
                foreach (var x in ti.DeclaredFields)
                    yield return x;
                if (ti.AsType() == typeof(Object))
                    break;
                var pt = ti.BaseType;
                if (pt == null)
                    break;
                ti = pt.GetTypeInfo();
            }
        }

        /// <summary>
        /// Enumerates all methods of this type, including inherited methods
        /// </summary>
        /// <param name="ti">The type of interest</param>
        /// <returns>All methods</returns>
        public static IEnumerable<MethodInfo> AllMethods(this TypeInfo ti)
        {
            while (ti != null)
            {
                foreach (var x in ti.DeclaredMethods)
                    yield return x;
                if (ti.AsType() == typeof(Object))
                    break;
                var pt = ti.BaseType;
                if (pt == null)
                    break;
                ti = pt.GetTypeInfo();
            }
        }

        /// <summary>
        /// Enumerates all properties of this type, including inherited properties
        /// </summary>
        /// <param name="ti">The type of interest</param>
        /// <returns>All properties</returns>
        public static IEnumerable<PropertyInfo> AllProperties(this TypeInfo ti)
        {
            while (ti != null)
            {
                foreach (var x in ti.DeclaredProperties)
                    yield return x;
                if (ti.AsType() == typeof(Object))
                    break;
                var pt = ti.BaseType;
                if (pt == null)
                    break;
                ti = pt.GetTypeInfo();
            }
        }

        /// <summary>
        /// Retrieves some reflection flags (properties) of a property, based on its getter (or setter if it can't be read)
        /// </summary>
        /// <param name="i">The property of interest</param>
        /// <returns>The reflection flags for the supplied property</returns>
        /// <remarks>
        /// <see cref="ReflectionFlags.IsDeclared"/> compares the accessor's declaring type with the property's declaring type, which are normally always equal,
        /// so the flag is effectively always set.
        /// </remarks>
        /// <exception cref="NullReferenceException">The property has neither a getter nor a setter.</exception>
        public static ReflectionFlags Flags(this PropertyInfo i)
        {
            ReflectionFlags flags = 0;
            var gm = (i.CanRead ? i.GetMethod : i.SetMethod) ?? throw new NullReferenceException();
            if (gm.IsStatic)
                flags |= ReflectionFlags.IsStatic;
            if (gm.IsPublic)
                flags |= ReflectionFlags.IsPublic;
            if (gm.DeclaringType == i.DeclaringType)
                flags |= ReflectionFlags.IsDeclared;
            return flags;
        }

        /// <summary>
        /// Retrieves some reflection flags (properties) of a method (only <see cref="ReflectionFlags.IsStatic"/> and <see cref="ReflectionFlags.IsPublic"/> are set)
        /// </summary>
        /// <param name="gm">The method of interest</param>
        /// <returns>The reflection flags for the supplied method</returns>
        public static ReflectionFlags Flags(this MethodInfo gm)
        {
            ReflectionFlags flags = 0;
            if (gm.IsStatic)
                flags |= ReflectionFlags.IsStatic;
            if (gm.IsPublic)
                flags |= ReflectionFlags.IsPublic;
            return flags;
        }

        /// <summary>
        /// Retrieves some reflection flags (properties) of a field (only <see cref="ReflectionFlags.IsStatic"/> and <see cref="ReflectionFlags.IsPublic"/> are set)
        /// </summary>
        /// <param name="gm">The field of interest</param>
        /// <returns>The reflection flags for the supplied field</returns>
        public static ReflectionFlags Flags(this FieldInfo gm)
        {
            ReflectionFlags flags = 0;
            if (gm.IsStatic)
                flags |= ReflectionFlags.IsStatic;
            if (gm.IsPublic)
                flags |= ReflectionFlags.IsPublic;
            return flags;
        }

        /// <summary>
        /// Enumerates all properties of a type that have some specific flags (properties)
        /// </summary>
        /// <param name="ti">The type of interest</param>
        /// <param name="mustHave">The reflection flags (properties) that the property must have</param>
        /// <param name="mayNotHave">The reflection flags (properties) that the property may not have</param>
        /// <returns>All matching properties</returns>
        /// <remarks>
        /// If <paramref name="mustHave"/> contains <see cref="ReflectionFlags.IsDeclared"/> only properties declared on the type itself are enumerated, else inherited ones are included too.
        /// Passing <see cref="ReflectionFlags.IsDeclared"/> in <paramref name="mayNotHave"/> excludes all properties (see <see cref="Flags(PropertyInfo)"/>).
        /// </remarks>
        public static IEnumerable<PropertyInfo> FindProperties(this TypeInfo ti, ReflectionFlags mustHave = ReflectionFlags.None, ReflectionFlags mayNotHave = ReflectionFlags.None)
        {
            var mh = mustHave & ~ReflectionFlags.IsDeclared;
            var mnh = mayNotHave & ~ReflectionFlags.IsDeclared;
            foreach (var p in mustHave.HasFlag(ReflectionFlags.IsDeclared) ? ti.DeclaredProperties : ti.AllProperties())
            {
                var flags = p.Flags();
                if ((flags & mh) != mh)
                    continue;
                if ((flags & mayNotHave) != 0)
                    continue;
                yield return p;
            }
        }

        /// <summary>
        /// Enumerates all methods of a type that have some specific flags (properties)
        /// </summary>
        /// <param name="ti">The type of interest</param>
        /// <param name="mustHave">The reflection flags (properties) that the method must have</param>
        /// <param name="mayNotHave">The reflection flags (properties) that the method may not have</param>
        /// <returns>All matching methods</returns>
        /// <remarks>
        /// If <paramref name="mustHave"/> contains <see cref="ReflectionFlags.IsDeclared"/> only methods declared on the type itself are enumerated, else inherited ones are included too
        /// (overridden methods may then be returned more than once, once per declaring type).
        /// </remarks>
        public static IEnumerable<MethodInfo> FindMethods(this TypeInfo ti, ReflectionFlags mustHave = ReflectionFlags.None, ReflectionFlags mayNotHave = ReflectionFlags.None)
        {
            var mh = mustHave & ~ReflectionFlags.IsDeclared;
            var mnh = mayNotHave & ~ReflectionFlags.IsDeclared;
            foreach (var p in mustHave.HasFlag(ReflectionFlags.IsDeclared) ? ti.DeclaredMethods : ti.AllMethods())
            {
                var flags = p.Flags();
                if ((flags & mh) != mh)
                    continue;
                if ((flags & mayNotHave) != 0)
                    continue;
                yield return p;
            }
        }

        /// <summary>
        /// Enumerates all fields of a type that have some specific flags (properties)
        /// </summary>
        /// <param name="ti">The type of interest</param>
        /// <param name="mustHave">The reflection flags (properties) that the field must have</param>
        /// <param name="mayNotHave">The reflection flags (properties) that the field may not have</param>
        /// <returns>All matching fields</returns>
        /// <remarks>
        /// If <paramref name="mustHave"/> contains <see cref="ReflectionFlags.IsDeclared"/> only fields declared on the type itself are enumerated, else inherited ones are included too.
        /// </remarks>
        public static IEnumerable<FieldInfo> FindFields(this TypeInfo ti, ReflectionFlags mustHave = ReflectionFlags.None, ReflectionFlags mayNotHave = ReflectionFlags.None)
        {
            var mh = mustHave & ~ReflectionFlags.IsDeclared;
            var mnh = mayNotHave & ~ReflectionFlags.IsDeclared;
            foreach (var p in mustHave.HasFlag(ReflectionFlags.IsDeclared) ? ti.DeclaredFields : ti.AllFields())
            {
                var flags = p.Flags();
                if ((flags & mh) != mh)
                    continue;
                if ((flags & mayNotHave) != 0)
                    continue;
                yield return p;
            }
        }


        /// <summary>
        /// Get a custom attribute from a method, or if it's not found, from a public instance method with the same name and parameter types
        /// on any of the interfaces implemented by the declaring type.
        /// </summary>
        /// <typeparam name="T">The attribute type.</typeparam>
        /// <param name="m">The method of interest.</param>
        /// <param name="inherit">True to search the inheritance chain of <paramref name="m"/> (only applies to the method itself, not the interface methods).</param>
        /// <returns>The first attribute found, or null.</returns>
        /// <remarks>
        /// Interface methods are matched by name and signature only, not via the interface map, so explicit implementations are not detected.
        /// </remarks>
        /// <exception cref="AmbiguousMatchException">More than one attribute of type <typeparamref name="T"/> is found.</exception>
        public static T GetCustomAttributeWithInterface<T>(this MethodInfo m, bool inherit) where T : Attribute
        {
            var t = m.GetCustomAttribute<T>(inherit);
            if (t != null)
                return t;
            var pt = m.GetParameters().Select(x => x.ParameterType).ToArray();
            foreach (var i in m.DeclaringType.GetInterfaces())
            {
                var p = i.GetMethod(m.Name, BindingFlags.Public | BindingFlags.Instance, pt);
                if (p == null)
                    continue;
                t = p.GetCustomAttribute<T>();
                if (t != null)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Get a clean type name (no assembly qualified generic arguments), ex: "System.Collections.Generic.List`1[[System.String]]"
        /// </summary>
        /// <param name="type">The type of interest</param>
        /// <returns>The full name of the type, generic arguments are recursively cleaned and enclosed in brackets. Generic results are cached.</returns>
        /// <remarks>Returns null for types without a full name (ex: open generic parameters).</remarks>
        public static String CleanTypename(this Type type)
        {
            if (!type.IsGenericType)
                return type.FullName;
            var c = CachedCleanTypename;
            if (c.TryGetValue(type, out var v))
                return v;
            var test = type.FullName;
            var rt = type.GetGenericTypeDefinition();
            v = String.Concat(rt.FullName, '[', String.Join(", ", type.GetGenericArguments().Select(x => String.Concat('[', CleanTypename(x), ']'))), ']');
            c.TryAdd(type, v);
            return v;
        }

        static readonly SemiFrozenDictionary<Type, String> CachedCleanTypename = new SemiFrozenDictionary<Type, string>();



        /// <summary>
        /// Get a clean assembly qualified type name (no version, culture or public key token in assembly names), ex: "MyNamespace.MyType, MyAssembly".
        /// The result can be resolved using <see cref="TypeFinder.Get(string, bool)"/>.
        /// </summary>
        /// <param name="type">The type of interest</param>
        /// <returns>The clean assembly qualified name, generic arguments are recursively cleaned. The result is cached.</returns>
        public static String CleanAssemblyQualifiedTypename(this Type type)
        {
            var c = CachedCleanAssemblyQualifiedTypename;
            if (c.TryGetValue(type, out var v))
                return v;

            if (type.IsGenericType)
            {
                var test = type.FullName;
                var rt = type.GetGenericTypeDefinition();
                v = String.Concat(rt.FullName, '[', String.Join(", ", type.GetGenericArguments().Select(x => String.Concat('[', CleanAssemblyQualifiedTypename(x), ']'))), "], ", type.Assembly.FullName.SplitFirst(','));
            }
            else 
                v = String.Concat(type.FullName, ", ", type.Assembly.FullName.SplitFirst(','));
            c.TryAdd(type, v);
            return v;
        }

        static readonly SemiFrozenDictionary<Type, String> CachedCleanAssemblyQualifiedTypename = new SemiFrozenDictionary<Type, string>();




        /// <summary>
        /// Same as GetField but includes inherited non public and static fields, the type and then each base type is searched until a match is found
        /// </summary>
        /// <param name="type">The type to start searching from</param>
        /// <param name="name">The name of the field</param>
        /// <param name="flags">The binding flags used for each type</param>
        /// <returns>The first matching field, or null if not found</returns>
        /// <exception cref="NullReferenceException"><paramref name="type"/> is an interface (or another type whose base type chain doesn't end in <see cref="Object"/>) and the field isn't found.</exception>
        public static FieldInfo GetFieldWithBase(this Type type, String name, BindingFlags flags)
        {
            for(; ;)
            {
                var i = type.GetField(name, flags);
                if (i != null)
                    return i;
                if (type == typeof(Object))
                    return null;
                type = type.BaseType;
            }
        }

        /// <summary>
        /// Same as GetProperty but includes inherited non public and static properties, the type and then each base type is searched until a match is found
        /// </summary>
        /// <param name="type">The type to start searching from</param>
        /// <param name="name">The name of the property</param>
        /// <param name="flags">The binding flags used for each type</param>
        /// <returns>The first matching property, or null if not found</returns>
        /// <exception cref="AmbiguousMatchException">More than one property with the name is found on a type (ex: indexers).</exception>
        /// <exception cref="NullReferenceException"><paramref name="type"/> is an interface (or another type whose base type chain doesn't end in <see cref="Object"/>) and the property isn't found.</exception>
        public static PropertyInfo GetPropertyWithBase(this Type type, String name, BindingFlags flags)
        {
            for (; ; )
            {
                var i = type.GetProperty(name, flags);
                if (i != null)
                    return i;
                if (type == typeof(Object))
                    return null;
                type = type.BaseType;
            }
        }


    }

}
