using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SysWeaver.Docs;

namespace SysWeaver.Data
{


    public sealed class TypeTableMember
    {
        /// <summary>
        /// Name of member
        /// </summary>
        public String Name;
        /// <summary>
        /// Name of the member type
        /// </summary>
        public String Type;
        /// <summary>
        /// Optional documentation
        /// </summary>
        public String Description;
    }


    public sealed class TypeTable
    {
        public String TypeName;
        public String Description;
        public TypeTableMember[] Members;
        public TypeTable[] Implementations;
    }


    public static class TypeDocumentation
    {
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


        public static TypeTable[] GetTypeTable(Type type, Func<Type, bool> makeTableFn = null, Func<MemberInfo, bool> includeMemberFn = null)
        {
            makeTableFn = makeTableFn ?? DefaultMakeFn;
            includeMemberFn = includeMemberFn ?? DefaultIncludeFn;
            var seen = new HashSet<Type>();
            var types = new List<TypeTable>();
            InternalMakeFn(types, seen, type, makeTableFn, includeMemberFn);
            return types.ToArray();
        }


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

        static void InternalMakeFn(List<TypeTable> types, HashSet<Type> seen, Type type, Func<Type, bool> makeTableFn, Func<MemberInfo, bool> includeMemberFn)
        {
            if (!seen.Add(type))
                return;
            if (!makeTableFn(type))
                return;
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
                    Members = members.ToArray(),
                });
            }

            foreach (var x in add)
                InternalMakeFn(types, seen, x, makeTableFn, includeMemberFn);
        }


    }


}
