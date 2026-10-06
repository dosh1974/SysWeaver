using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SysWeaver.Docs;

namespace SysWeaver.Data
{


    /// <summary>
    /// Describes a single member (field / property) or enum value of a documented type, see <see cref="TypeTable"/>.
    /// </summary>
    public sealed class TypeTableMember
    {
        /// <summary>
        /// Name of member
        /// </summary>
        public String Name;
        /// <summary>
        /// Name of the member type (short name, collections are written as "Element[]").
        /// For enum values this is the value (hex for flags) followed by " : " and the underlying type name.
        /// </summary>
        public String Type;
        /// <summary>
        /// Optional documentation (the XML doc summary of the member), null if not available
        /// </summary>
        [TableDataWordWrap]
        public String Description;
    }


    /// <summary>
    /// Documentation of a single type, created by <see cref="TypeDocumentation.GetTypeTable(Type, Func{Type, bool}, Func{MemberInfo, bool})"/>.
    /// </summary>
    public sealed class TypeTable
    {
        /// <summary>
        /// The (short) name of the type, enums are prefixed with "enum " or "enum flags ".
        /// </summary>
        public String TypeName;
        /// <summary>
        /// The XML doc summary of the type, null if not available.
        /// </summary>
        public String Description;
        /// <summary>
        /// The members of a concrete type or the values of an enum, null for abstract types and interfaces.
        /// </summary>
        public TypeTableMember[] Members;
        /// <summary>
        /// For abstract types and interfaces: the documentation of all public, concrete implementations found in the loaded assemblies, else null.
        /// </summary>
        public TypeTable[] Implementations;
    }


    /// <summary>
    /// Creates human readable documentation (tables of members) for a type and all types that it references, using reflection and XML docs.
    /// </summary>
    /// <remarks>
    /// Currently not used within the framework.
    /// </remarks>
    public static class TypeDocumentation
    {
        /// <summary>
        /// The default filter for which types to document, excludes types in the "System" and "Microsoft" namespaces,
        /// types in assemblies signed with the .NET framework public keys and special name types.
        /// </summary>
        /// <param name="type">The type to test</param>
        /// <returns>True if a table should be created for the type</returns>
        /// <remarks>Throws a <see cref="NullReferenceException"/> for types without a namespace.</remarks>
        public static bool DefaultMakeFn(Type type)
        {
            var ns = type.Namespace;
            if (ns.StartsWith("System."))
                return false;
            if (ns.StartsWith("Microsoft."))
                return false;
            if (ns.FastEquals("System"))
                return false;
            if (ns.FastEquals("Microsoft"))
                return false;
            var fn = type.Assembly.FullName;
            if (fn.FastEndsWith("b77a5c561934e089"))
                return false;
            if (fn.FastEndsWith("7cec85d7bea7798e"))
                return false;
            if (type.IsSpecialName)
                return false;
            return true;
        }

        /// <summary>
        /// The default filter for which members to include: public, non-static, non-readonly fields and
        /// properties with a public getter and a public setter.
        /// </summary>
        /// <param name="mi">The member to test</param>
        /// <returns>True if the member should be included</returns>
        public static bool DefaultIncludeFn(MemberInfo mi)
        {
            if (mi.MemberType == MemberTypes.Field)
            {
                var fi = mi as FieldInfo;
                if (!fi.IsPublic)
                    return false;
                if (fi.IsInitOnly)
                    return false;
                if (fi.IsSpecialName)
                    return false;
                if (fi.IsStatic)
                    return false;
                return true;
            }
            if (mi.MemberType == MemberTypes.Property)
            {
                var pi = mi as PropertyInfo;
                if (!pi.CanWrite)
                    return false;
                if (!pi.CanRead)
                    return false;
                if (!pi.GetMethod.IsPublic)
                    return false;
                if (!pi.SetMethod.IsPublic)
                    return false;
                return true;
            }
            return false;
        }


        /// <summary>
        /// Get documentation tables for a type and (recursively) all types referenced by its members (element types of arrays and collections are used).
        /// </summary>
        /// <param name="type">The type to document</param>
        /// <param name="makeTableFn">Determines if a table should be created for a type, defaults to <see cref="DefaultMakeFn(Type)"/></param>
        /// <param name="includeMemberFn">Determines if a member should be included, defaults to <see cref="DefaultIncludeFn(MemberInfo)"/></param>
        /// <returns>One table per documented type, the requested type first (if it passes <paramref name="makeTableFn"/>).
        /// Implementations of abstract types / interfaces are nested in <see cref="TypeTable.Implementations"/></returns>
        /// <remarks>
        /// Documenting an abstract type or interface scans all types of all loaded assemblies, which can be slow.
        /// </remarks>
        public static TypeTable[] GetTypeTable(Type type, Func<Type, bool> makeTableFn = null, Func<MemberInfo, bool> includeMemberFn = null)
        {
            makeTableFn = makeTableFn ?? DefaultMakeFn;
            includeMemberFn = includeMemberFn ?? DefaultIncludeFn;
            var seen = new HashSet<Type>();
            var types = new List<TypeTable>();
            InternalMakeFn(types, seen, type, makeTableFn, includeMemberFn);
            return types.ToArray();
        }


        /// <summary>
        /// Append Mark Down documentation (a heading, the description and a member table per type) for a type and all types that it references.
        /// </summary>
        /// <param name="sb">The string builder to append the Mark Down to</param>
        /// <param name="type">The type to document</param>
        /// <param name="seenTypes">Optional set of types that have already been documented, types in the set are skipped and newly documented types are added to it.
        /// Use this to avoid duplicates when documenting multiple types into the same output</param>
        /// <param name="makeTableFn">Determines if a table should be created for a type, defaults to <see cref="DefaultMakeFn(Type)"/></param>
        /// <param name="includeMemberFn">Determines if a member should be included, defaults to <see cref="DefaultIncludeFn(MemberInfo)"/></param>
        /// <param name="linePrefix">Optional prefix for every line, implementations of abstract types / interfaces are nested using block quotes ("&gt; ")</param>
        public static void AddTypeTableToMD(StringBuilder sb, Type type, HashSet<Type> seenTypes = null, Func<Type, bool> makeTableFn = null, Func<MemberInfo, bool> includeMemberFn = null, String linePrefix = null)
        {
            if (seenTypes != null)
            {
                var make = makeTableFn ?? DefaultMakeFn;
                makeTableFn = type => seenTypes.Add(type) ? make(type) : false;
            }
            void addTables(TypeTable[] tables, String linePrefix)
            {
                foreach (var x in tables)
                {
                    if (x.Members != null)
                    {
                        sb.Append(linePrefix).Append("### ").Append(StringTools.EscapeMD(x.TypeName, true)).AppendLine("  ");
                        sb.Append(linePrefix).AppendLine("  ");
                        if (x.Description != null)
                        {
                            sb.Append(linePrefix).Append(StringTools.EscapeMD(x.Description).Replace("\r", "").Replace("\n", "<br>")).AppendLine("  ");
                            sb.Append(linePrefix).AppendLine("  ");
                        }
                        var text = MarkDownTableDataExporter.Instance.GetMarkDownText(TableDataTools.Get(new TableDataRequest
                        {
                            MaxRowCount = 1000000,
                        }, x.Members), null, new TableDataExportOptions
                        {
                            Custom = linePrefix
                        });
                        sb.Append(text);
                        sb.Append(linePrefix).AppendLine("  ");
                    }
                    if (x.Implementations != null)
                    {
                        sb.Append(linePrefix).Append("#### Implementations").AppendLine("  ");
                        sb.Append(linePrefix).AppendLine("  ");
                        addTables(x.Implementations, linePrefix.Length == 0 ? "> " : ">" + linePrefix);
                    }
                }
            }
            linePrefix = linePrefix ?? "";
            var tables = GetTypeTable(type, makeTableFn, includeMemberFn);
            addTables(tables, linePrefix);
        }


        static Type GetElementType(ref String suffix, Type t)
        {
            if (t.IsArray)
            {
                suffix += "[]";
                return GetElementType(ref suffix, t.GetElementType());
            }
            if (!t.IsGenericType)
                return t;
            var a = t.GetGenericArguments();
            if (a.Length != 1)
                return t;
            var et = a[0];
            if (typeof(IReadOnlyList<>).MakeGenericType(et).IsAssignableFrom(t))
            {
                suffix += "[]";
                return GetElementType(ref suffix, et);
            }
            if (typeof(IEnumerable<>).MakeGenericType(et).IsAssignableFrom(t))
            {
                suffix += "[]";
                return GetElementType(ref suffix, et);
            }
            return t;
        }

        static String GetTypeString(Action<Type> addType, MemberInfo i)
        {
            Type t = null;
            String suffix = "";
            switch (i.MemberType)
            {
                case MemberTypes.Field:
                    t = (i as FieldInfo).FieldType;
                    break;
                case MemberTypes.Property:
                    t = (i as PropertyInfo).PropertyType;
                    break;
                case MemberTypes.Method:
                    t = (i as MethodInfo).ReturnType;
                    break;
            }
            if (t == null)
                return "-";
            t = GetElementType(ref suffix, t);
            addType(t);
            return t.Name + suffix;
        }

        static Type[] FindInstanceOf(Type baseType)
        {
            List<Type> types = new List<Type>();
            void AddType(Type type)
            {
                try
                {
                    if (!type.IsPublic)
                        return;
                    if (type.IsInterface)
                        return;
                    if (!type.IsAbstract)
                    {
                        if (baseType.IsAssignableFrom(type))
                            types.Add(type);
                    }
                    foreach (var x in type.GetNestedTypes())
                        AddType(x);
                }
                catch
                {
                }
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                        AddType(type);
                }
                catch
                {
                }
            }
            return types.ToArray();
        }

        static IReadOnlySet<Type> IsSigned = ReadOnlyData.Set(
            typeof(SByte),
            typeof(Int16),
            typeof(Int32),
            typeof(Int64)
            );

        static void InternalMakeFn(List<TypeTable> types, HashSet<Type> seen, Type type, Func<Type, bool> makeTableFn, Func<MemberInfo, bool> includeMemberFn)
        {
            if (!seen.Add(type))
                return;
            if (!makeTableFn(type))
                return;

            var desc = type.XmlDoc()?.Summary;
            if (type.IsEnum)
            {
                bool isFlags = type.GetCustomAttribute<FlagsAttribute>() != null;
                var ut = type.GetEnumUnderlyingType();
                var isSigned = IsSigned.Contains(ut);
                var names = Enum.GetNames(type);
                var values = Enum.GetValues(type);
                var l = names.Length;
                String[] strVal = new string[l];
                int ml = 0;
                for (int i = 0; i < l; ++ i)
                {
                    String ss;
                    if (isSigned)
                    {
                        Int64 v = (Int64)Convert.ChangeType(values.GetValue(i), typeof(Int64));
                        ss = isFlags ? ((UInt64)v).ToString("x") : v.ToValueString();
                    }
                    else
                    {
                        UInt64 v = (UInt64)Convert.ChangeType(values.GetValue(i), typeof(UInt64));
                        ss = isFlags ? v.ToString("x") : v.ToValueString();
                    }
                    strVal[i] = ss;
                    ml = Math.Max(ml, ss.Length);
                }
                Char pad = isFlags ? '0' : ' ';
                for (int i = 0; i < l; ++i)
                    strVal[i] = (isFlags ? "0x" : "") + strVal[i].PadLeft(ml, pad);
                var typeSuf = " : " + ut.Name;

                TypeTableMember[] m = new TypeTableMember[l];
                for (int i = 0; i < l; ++ i)
                {
                    var name = names[i];
                    m[i] = new TypeTableMember
                    {
                        Name = name,
                        Type = strVal[i] + typeSuf,
                        Description = type.XmlDocEnum(name)?.Summary
                    };
                }
                types.Add(new TypeTable
                {
                    TypeName = (isFlags ? "enum flags " : "enum ") + type.Name,
                    Description = desc,
                    Members = m,
                });
                return;
            }


            List<Type> add = new List<Type>();
            HashSet<Type> added = new HashSet<Type>();

            Action<Type> addType = t =>
            {
                if (added.Add(t))
                    if (!seen.Contains(t))
                        add.Add(t);
            };

            if (type.IsAbstract || type.IsInterface)
            {
                List<TypeTable> impl = new List<TypeTable>();
                var impTypes = FindInstanceOf(type);
                foreach (var impType in impTypes)
                    InternalMakeFn(impl, seen, impType, makeTableFn, includeMemberFn);
                types.Add(new TypeTable
                {
                    TypeName = type.Name,
                    Description = desc,
                    Implementations = impl.ToArray(),
                });
            }
            else
            {
                List<TypeTableMember> members = new List<TypeTableMember>();
                foreach (var x in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (!includeMemberFn(x))
                        continue;
                    members.Add(new TypeTableMember
                    {
                        Name = x.Name,
                        Type = GetTypeString(addType, x),
                        Description = x.XmlDoc()?.Summary
                    });
                }

                types.Add(new TypeTable
                {
                    TypeName = type.Name,
                    Description = desc,
                    Members = members.ToArray(),
                });
            }

            foreach (var x in add)
                InternalMakeFn(types, seen, x, makeTableFn, includeMemberFn);
        }


    }


}
