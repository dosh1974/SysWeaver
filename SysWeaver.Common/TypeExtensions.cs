using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
        /// Retrieves some reflection flags (properties) of a propery
        /// </summary>
        /// <param name="i">The property of interest</param>
        /// <returns>The reflection flags for the supplied property</returns>
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
        /// Retrieves some reflection flags (properties) of a method
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
        /// Retrieves some reflection flags (properties) of a field
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
    }

 
    public static class AssemblyExt
    {
        public static IReadOnlySet<String> GetEmbeddedResource(this Assembly assembly)
        {
            var c = EmbeddedResources;
            if (c.TryGetValue(assembly, out var r))
                return r;
            r = new HashSet<String>(assembly.GetManifestResourceNames(), StringComparer.Ordinal).Freeze();
            return c.TryAdd(assembly, r) ? r : c[assembly];
        }

        static readonly ConcurrentDictionary<Assembly, IReadOnlySet<String>> EmbeddedResources = new();

    }

    /// <summary>
    /// A collection of flags that describe some reflection properties
    /// </summary>
    [Flags]
    public enum ReflectionFlags
    {
        /// <summary>
        /// Nothing
        /// </summary>
        None = 0,
        /// <summary>
        /// The reflected item is static, else instance
        /// </summary>
        IsStatic = 1,
        /// <summary>
        /// The reflected item is public, else private / protected / internal etc
        /// </summary>
        IsPublic = 2,
        /// <summary>
        /// The reflected item is declared in the specified type, else it's declared in one of the inherited types
        /// </summary>
        IsDeclared = 4,
        /// <summary>
        /// All reflection types combined
        /// </summary>
        All = IsStatic | IsPublic | IsDeclared
    }

}
