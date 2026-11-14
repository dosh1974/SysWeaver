using System;
using System.Reflection;

namespace SysWeaver.MicroService.EditInternal
{
    /// <summary>
    /// Finds the edit attributes (EditMin, EditRange, EditDefault etc) of a field or property, including inherited and interface attributes
    /// </summary>
    static class EditAttributeLookup
    {
        const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Get an attribute of a field or property, in priority order:
        ///   1. The member of the declaring type (including the base definitions of an overridden virtual property).
        ///   2. A member with the same name on a base type (ex: a property hidden with "new" or a re-declared field).
        ///   3. The interface member that a property implements (also explicit implementations), or a property with the same name on an implemented interface.
        /// </summary>
        /// <typeparam name="T">The attribute type</typeparam>
        /// <param name="m">The field or property</param>
        /// <returns>The attribute or null if not found</returns>
        public static T Get<T>(MemberInfo m) where T : Attribute
        {
            if (m == null)
                return null;
            //  1. The member (inherit = true also finds the attribute on the base definition of an overridden property)
            var a = m.GetCustomAttribute<T>(true);
            if (a != null)
                return a;
            if ((m.MemberType != MemberTypes.Property) && (m.MemberType != MemberTypes.Field))
                return null;
            var name = m.Name;
            //  2. Base types
            for (var b = m.DeclaringType?.BaseType; b != null; b = b.BaseType)
            {
                foreach (var bm in b.GetMember(name, MemberTypes.Property | MemberTypes.Field, MemberFlags))
                {
                    a = bm.GetCustomAttribute<T>(false);
                    if (a != null)
                        return a;
                }
            }
            //  3. Interfaces
            if (m is not PropertyInfo p)
                return null;
            var t = p.ReflectedType ?? p.DeclaringType;
            if (t == null)
                return null;
            var interfaces = t.GetInterfaces();
            if (interfaces.Length == 0)
                return null;
            if (!t.IsInterface)
            {
                var get = p.GetMethod;
                var set = p.SetMethod;
                foreach (var i in interfaces)
                {
                    InterfaceMapping map;
                    try
                    {
                        map = t.GetInterfaceMap(i);
                    }
                    catch
                    {
                        continue;
                    }
                    var targets = map.TargetMethods;
                    for (int k = 0; k < targets.Length; ++k)
                    {
                        var tm = targets[k];
                        if ((tm == null) || ((tm != get) && (tm != set)))
                            continue;
                        var im = map.InterfaceMethods[k];
                        foreach (var ip in i.GetProperties())
                        {
                            if ((ip.GetMethod != im) && (ip.SetMethod != im))
                                continue;
                            a = ip.GetCustomAttribute<T>(false);
                            if (a != null)
                                return a;
                        }
                    }
                }
            }
            //  An interface type (or a property that isn't an implementation, ex: implemented by a base type): same name on the interfaces
            foreach (var i in interfaces)
            {
                a = i.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetCustomAttribute<T>(false);
                if (a != null)
                    return a;
            }
            return null;
        }
    }
}
