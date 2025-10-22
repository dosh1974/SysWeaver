using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SysWeaver.Docs
{

    public static class XmlDocExt
    {


        static readonly ConcurrentDictionary<Type, List<DocAssembly>> AsmsCache = new ConcurrentDictionary<Type, List<DocAssembly>>();
        static readonly ConcurrentDictionary<Type, IXmlDocInfo> TypeCache = new ConcurrentDictionary<Type, IXmlDocInfo>();
        static readonly ConcurrentDictionary<FieldInfo, IXmlDocInfo> FieldCache = new ConcurrentDictionary<FieldInfo, IXmlDocInfo>();
        static readonly ConcurrentDictionary<PropertyInfo, IXmlDocInfo> PropertyCache = new ConcurrentDictionary<PropertyInfo, IXmlDocInfo>();
        static readonly ConcurrentDictionary<MethodInfo, IXmlDocMethodInfo> MethodCache = new ConcurrentDictionary<MethodInfo, IXmlDocMethodInfo>();
        static readonly ConcurrentDictionary<ParameterInfo, IXmlDocParameterInfo> ParameterCache = new ConcurrentDictionary<ParameterInfo, IXmlDocParameterInfo>();
        static readonly ConcurrentDictionary<ConstructorInfo, IXmlDocMethodInfo> ConstructorCache = new ConcurrentDictionary<ConstructorInfo, IXmlDocMethodInfo>();
        static readonly ConcurrentDictionary<MethodBase, IXmlDocMethodInfo> MethodBaseCache = new ConcurrentDictionary<MethodBase, IXmlDocMethodInfo>();
        static readonly ConcurrentDictionary<MemberInfo, IXmlDocInfo> MemberCache = new ConcurrentDictionary<MemberInfo, IXmlDocInfo>();
        static readonly List<DocAssembly> Empty = new List<DocAssembly>(); 

        static IReadOnlyList<DocAssembly> GetAssemblies(Type type)
        {
            if (type == null)
                return Empty;
            var cache = AsmsCache;
            if (cache.TryGetValue(type, out var asms))
                return asms;
            asms = new List<DocAssembly>();
            HashSet<DocAssembly> seen = new HashSet<DocAssembly>();
            var a = GetAsm(type.Assembly);
            if (a != null)
                if (seen.Add(a))
                    asms.Add(a);
            foreach (var x in type.GetInterfaces())
            {
                a = GetAsm(x.Assembly);
                if (a != null)
                    if (seen.Add(a))
                        asms.Add(a);
            }
            var t = type;
            for (; ; )
            {
                t = t.BaseType;
                if (t == null)
                    break;
                if (t == typeof(Object))
                    break;
                a = GetAsm(t.Assembly);
                if (a != null)
                    if (seen.Add(a))
                        asms.Add(a);
            }
            cache.TryAdd(type, asms);
            return asms;
        }



        public static IXmlDocInfo XmlDoc(this Type type)
        {
            if (type == null)
                return null;
            var cache = TypeCache;
            if (cache.TryGetValue(type, out var docType))
                return docType;

            if (type.IsByRef)
                type = type.GetElementType() ?? throw new NullReferenceException();
            if (type.IsArray)
                type = type.GetElementType() ?? throw new NullReferenceException();
            if (type.IsGenericType)
                type = type.GetGenericTypeDefinition();
            var asms = GetAssemblies(type);
            foreach (var asm in asms)
            {
                if (asm.Types.TryGetValue(type, out var t))
                {
                    docType = t;
                    break;
                }
            }
            cache.TryAdd(type, docType);
            return docType;
        }


        public static IXmlDocInfo XmlDocEnum(this Type type, String enumValueName)
        {
            var fi = type.GetField(enumValueName, BindingFlags.Public | BindingFlags.Static);
            return fi?.XmlDoc();
        }



        public static String XmlSummary(this PropertyInfo mi) => XmlDoc(mi)?.Summary;
        public static String XmlSummary(this FieldInfo mi) => XmlDoc(mi)?.Summary;

        public static IXmlDocInfo XmlDoc(this FieldInfo mi)
        {
            if (mi == null)
                return null;
            var cache = FieldCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;

            foreach (var asm in GetAssemblies(mi.DeclaringType))
            {
                if (asm.Fields.TryGetValue(mi, out var t))
                {
                    docType = t;
                    break;
                }
            }
            cache.TryAdd(mi, docType);
            return docType;
        }

        public static IXmlDocInfo XmlDoc(this PropertyInfo mi)
        {
            if (mi == null)
                return null;
            var cache = PropertyCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;
            foreach (var asm in GetAssemblies(mi.DeclaringType))
            {
                if (asm.Properties.TryGetValue(mi, out var t))
                {
                    docType = t;
                    break;
                }
            }
            cache.TryAdd(mi, docType);
            return docType;
        }

        public static IXmlDocMethodInfo XmlDoc(this MethodInfo mi)
        {
            if (mi == null)
                return null;
            var cache = MethodCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;
            foreach (var asm in GetAssemblies(mi.DeclaringType))
            {
                if (asm.Methods.TryGetValue(mi, out var t))
                {
                    docType = t;
                    break;
                }
            }
            cache.TryAdd(mi, docType);
            return docType;
        }

        public static IXmlDocParameterInfo XmlDoc(this ParameterInfo mi)
        {
            if (mi == null)
                return null;
            var cache = ParameterCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;
            var method = mi.Member;
            var m = method.XmlDoc() as IXmlDocMethodInfo;
            if (m != null)
            {
                var pos = mi.Position;
                if (pos < 0)
                {
                    docType = new DocParameterInfo(m.Returns);
                }
                else
                {
                    var mp = m.Parameters;
                    if (mp != null)
                        docType = pos < mp.Length ? mp[pos] : null;
                }
            }
            cache.TryAdd(mi, docType);
            return docType;
        }

        public static IXmlDocMethodInfo XmlDoc(this ConstructorInfo mi)
        {
            if (mi == null)
                return null;
            var cache = ConstructorCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;
            foreach (var asm in GetAssemblies(mi.DeclaringType))
            {
                if (asm.Constructors.TryGetValue(mi, out var t ))
                {
                    docType = t;
                    break;
                }
            }
            cache.TryAdd(mi, docType);
            return docType;
        }

        public static IXmlDocMethodInfo XmlDoc(this MethodBase mi)
        {
            if (mi == null)
                return null;
            var cache = MethodBaseCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;
            if (mi.IsConstructor)
                docType = (mi as ConstructorInfo).XmlDoc();
            else
                docType = (mi as MethodInfo).XmlDoc();
            cache.TryAdd(mi, docType);
            return docType;
        }

        public static IXmlDocInfo XmlDoc(this MemberInfo mi)
        {
            if (mi == null)
                return null;
            var cache = MemberCache;
            if (cache.TryGetValue(mi, out var docType))
                return docType;
            switch (mi.MemberType)
            {
                case MemberTypes.Field:
                    docType = (mi as FieldInfo).XmlDoc();
                    break;
                case MemberTypes.Property:
                    docType = (mi as PropertyInfo).XmlDoc();
                    break;
                case MemberTypes.Method:
                    docType = (mi as MethodInfo).XmlDoc();
                    break;
                case MemberTypes.Constructor:
                    docType = (mi as ConstructorInfo).XmlDoc();
                    break;
            }
            cache.TryAdd(mi, docType);
            return docType;
        }


        public static String ToTitle(this IXmlDocInfo i)
        {
            if (i == null)
                return null;
            var s = i.Summary;
            var r = i.Remarks;
            if (s == null)
                return r;
            if (r == null)
                return s;
            return String.Concat(s, "\n-------\n\n", r);
        }


        class DocInfo : IXmlDocInfo
        {
            public override string ToString()
            {
                return String.Concat("Summary: ", Summary, Remarks == null ? "" : String.Concat(Environment.NewLine, "Remarks: ", Remarks));
            }

            public DocInfo(String summary, String remarks)
            {
                Summary = summary?.Trim();
                Remarks = remarks?.Trim();
            }
            public readonly String Summary;
            public readonly String Remarks;
            String IXmlDocInfo.Summary => Summary;
            String IXmlDocInfo.Remarks => Remarks;
        }


        class DocParameterInfo : IXmlDocParameterInfo
        {
            public override string ToString()
            {
                return String.Concat("Param: ", Summary);
            }
            public DocParameterInfo(String summary)
            {
                Summary = summary?.Trim();
            }
            public readonly String Summary;
            String IXmlDocParameterInfo.Param => Summary;
        }

        sealed class DocMethodInfo : DocInfo, IXmlDocMethodInfo
        {
            public override string ToString()
            {
                var p = Parameters;
                int pc = p?.Length ?? 0;
                return String.Concat("Summary: ", Summary, Returns == null ? "" : String.Concat(Environment.NewLine, "Returns: ", Returns), pc <= 0 ? "" : String.Concat(Environment.NewLine, String.Join(Environment.NewLine, Enumerable.Range(0, pc).Select(x => String.Concat("Param ", x + 1, ": ", (p ?? throw new NullReferenceException())[x]?.Param ?? "")))), Remarks == null ? "" : String.Concat(Environment.NewLine, "Remarks: ", Remarks));
            }
            public DocMethodInfo(String summary, String remarks, String returns, IXmlDocParameterInfo[] parameters) : base(summary, remarks)
            {
                Returns = returns?.Trim();
                Parameters = parameters;
            }
            public readonly String Returns;
            public readonly IXmlDocParameterInfo[] Parameters;
            String IXmlDocMethodInfo.Returns => Returns;
            IXmlDocParameterInfo[] IXmlDocMethodInfo.Parameters => Parameters;
        }


        sealed class MemberInfoComparer : IEqualityComparer<MemberInfo>
        {
            MemberInfoComparer()
            {
            }

            public static MemberInfoComparer Comparer = new MemberInfoComparer();


            bool IEqualityComparer<MemberInfo>.Equals(MemberInfo x, MemberInfo y)
            {
                if (x == null)
                    return y == null;
                if (y == null)
                    return false;
                return x.MetadataToken == y.MetadataToken;
            }

            int IEqualityComparer<MemberInfo>.GetHashCode(MemberInfo obj) => obj.MetadataToken;
        }




        sealed class FieldInfoComparer : IEqualityComparer<FieldInfo>
        {
            public bool Equals(FieldInfo x, FieldInfo y)
            {
                if (!String.Equals(x.Name, y.Name, StringComparison.Ordinal)) 
                    return false;
                if (!x.DeclaringType.IsAssignableFrom(y.DeclaringType))
                    if (!y.DeclaringType.IsAssignableFrom(x.DeclaringType))
                        return false;
                return true;
            }

            public int GetHashCode([DisallowNull] FieldInfo obj) => obj.Name.GetHashCode();
        }

        sealed class PropertyInfoComparer : IEqualityComparer<PropertyInfo>
        {
            public bool Equals(PropertyInfo x, PropertyInfo y)
            {
                if (!String.Equals(x.Name, y.Name, StringComparison.Ordinal))
                    return false;
                if (!x.DeclaringType.IsAssignableFrom(y.DeclaringType))
                    if (!y.DeclaringType.IsAssignableFrom(x.DeclaringType))
                        return false;
                return true;
            }

            public int GetHashCode([DisallowNull] PropertyInfo obj) => obj.Name.GetHashCode();
        }


        sealed class MethodInfoComparer : IEqualityComparer<MethodInfo>
        {
            public bool Equals(MethodInfo x, MethodInfo y)
            {
                if (!String.Equals(x.Name, y.Name, StringComparison.Ordinal))
                    return false;
                if (!x.DeclaringType.IsAssignableFrom(y.DeclaringType))
                    if (!y.DeclaringType.IsAssignableFrom(x.DeclaringType))
                        return false;
                if (x.ReturnType != y.ReturnType)
                    return false;
                if (x.IsStatic != y.IsStatic)
                    return false;
                var a = x.GetParameters();
                var b = y.GetParameters();
                if ((a == null) || (b == null))
                    if (a != b)
                        return false;
                var l = a.Length;
                if (l != b.Length)
                    return false;
                for (int i = 0; i< l; ++ i)
                {
                    var aa = a[i];
                    var bb = b[i];
                    if (!String.Equals(aa.Name, bb.Name, StringComparison.Ordinal))
                        return false;
                    if (aa.ParameterType != bb.ParameterType)
                        return false;
                    if (aa.IsIn != bb.IsIn) 
                        return false;
                    if (aa.IsOut != bb.IsOut)
                        return false;
                }
                return true;
            }

            public int GetHashCode([DisallowNull] MethodInfo obj) => obj.Name.GetHashCode();
        }


        sealed class ConstructorInfoComparer : IEqualityComparer<ConstructorInfo>
        {
            public bool Equals(ConstructorInfo x, ConstructorInfo y)
            {
                if (!String.Equals(x.Name, y.Name, StringComparison.Ordinal))
                    return false;
                if (!x.DeclaringType.IsAssignableFrom(y.DeclaringType))
                    if (!y.DeclaringType.IsAssignableFrom(x.DeclaringType))
                        return false;
                if (x.IsStatic != y.IsStatic)
                    return false;
                var a = x.GetParameters();
                var b = y.GetParameters();
                if ((a == null) || (b == null))
                    if (a != b)
                        return false;
                var l = a.Length;
                if (l != b.Length)
                    return false;
                for (int i = 0; i < l; ++i)
                {
                    var aa = a[i];
                    var bb = b[i];
                    if (!String.Equals(aa.Name, bb.Name, StringComparison.Ordinal))
                        return false;
                    if (aa.ParameterType != bb.ParameterType)
                        return false;
                    if (aa.IsIn != bb.IsIn)
                        return false;
                    if (aa.IsOut != bb.IsOut)
                        return false;
                }
                return true;
            }

            public int GetHashCode([DisallowNull] ConstructorInfo obj) => obj.Name.GetHashCode();
        }

        sealed class DocAssembly
        {
            public DocAssembly(String name)
            {
                Name = name;
            }
            readonly String Name;
            public override string ToString() => Name;

            public readonly ConcurrentDictionary<Type, DocInfo> Types = new();
            public readonly ConcurrentDictionary<MethodInfo, DocMethodInfo> Methods = new ConcurrentDictionary<MethodInfo, DocMethodInfo>(new MethodInfoComparer());
            public readonly ConcurrentDictionary<ConstructorInfo, DocMethodInfo> Constructors = new ConcurrentDictionary<ConstructorInfo, DocMethodInfo>(new ConstructorInfoComparer());
            public readonly ConcurrentDictionary<FieldInfo, DocInfo> Fields = new ConcurrentDictionary<FieldInfo, DocInfo>(new FieldInfoComparer());
            public readonly ConcurrentDictionary<PropertyInfo, DocInfo> Properties = new ConcurrentDictionary<PropertyInfo, DocInfo>(new PropertyInfoComparer());
        }

        static readonly ConcurrentDictionary<Assembly, DocAssembly> DocAssemblies = new ();

        static String Read(XElement x, String childName)
        {
            var c = x.Element(childName);
            if (c == null)
                return null;
            return c.Value;
        }

        const String AttrName = "name";
        const String ElementSummary = "summary";
        const String ElementRemarks = "remarks";
        const String ElementReturns = "returns";
        const String ElementParam = "param";

        const String GenericParameterReplaceFrom = "``";
        const String GenericParameter = "`";

        static String GetParamaterTypeSig(Type t)
        {
            if (t.IsByRef)
            {
                var tt = t.GetElementType();
                if (tt == null)
                    return null;
                return GetParamaterTypeSig(tt) + "@";
            }
            if (t.IsArray)
            {
                var tt = t.GetElementType();
                if (tt == null)
                    return null;
                return GetParamaterTypeSig(tt) + String.Concat('[', new String(',', t.GetArrayRank() - 1), ']');
            }
            if (t.IsGenericParameter)
                return GenericParameter + t.GenericParameterPosition;
            if (t.IsGenericType)
            {
                var r = String.Join('.', t.Namespace, t.Name).Split('`')[0];
                r += '{';
                r += String.Join(',', t.GenericTypeArguments.Select(x => GetParamaterTypeSig(x)));
                r += '}';
                return r;
            }
            return t.FullName;
        }

        static String GetParamaterSig(ParameterInfo p)
        {
            var r = GetParamaterTypeSig(p.ParameterType);
            return r;

        }

        static readonly Type[] EmptyTypes = [];



        static bool TryFindType(out Type t, out String typeName, String[] names, Assembly asm)
        {
            var nl = names.Length;
            typeName = String.Concat(String.Join('.', names, 0, nl - 1), ", ", asm.FullName);
            t = Type.GetType(typeName, false);
            if (t == null)
            {
                if (nl <= 2)
                    return false;
                typeName = String.Concat(String.Join('.', names, 0, nl - 2), '+', names[nl - 2], ", ", asm.FullName);
                t = Type.GetType(typeName, false);
                if (t == null)
                    return false;
            }
            return true;
        }

        static DocAssembly GetAsm(Assembly asm)
        {
            if (asm == null)
                return null;
            if (!DocAssemblies.TryGetValue(asm, out var docAsm))
            {
                lock (DocAssemblies)
                {
                    if (!DocAssemblies.TryGetValue(asm, out docAsm))
                    {
                        try
                        {
                            var loc = asm.Location;
                            if (!String.IsNullOrEmpty(loc))
                            {
                                var docFile = Path.Combine(Path.GetDirectoryName(loc), Path.GetFileNameWithoutExtension(loc) + ".xml");
                                if (File.Exists(docFile))
                                {
                                    docAsm = new DocAssembly(asm.FullName);
                                    var types = docAsm.Types;
                                    var xml = XDocument.Load(docFile);
                                    foreach (var m in xml.XPathSelectElements("doc/members/member"))
                                    {
                                        var name = m.Attribute(AttrName)?.Value;
                                        if (String.IsNullOrEmpty(name))
                                            continue;
                                        name = name.Replace(GenericParameterReplaceFrom, GenericParameter);
                                        var tn = name.Split(':');
                                        if (tn.Length < 2)
                                            continue;
                                        var mt = tn[0][0];
                                        switch (mt)
                                        {
                                            case 'T':
                                                {
                                                    var typeName = String.Join(", ", tn[1], asm.FullName);
                                                    var t = Type.GetType(typeName, false);
                                                    if (t == null)
                                                    {
                                                        typeName = tn[1];
                                                        var i = typeName.LastIndexOf('.');
                                                        if (i < 0)
                                                            continue;
                                                        typeName =  String.Join("+", typeName.Substring(0, i), typeName.Substring(i + 1));
                                                        typeName = String.Join(", ", typeName, asm.FullName);
                                                        t = Type.GetType(typeName, false);
                                                        if (t == null)
                                                            continue;
                                                    }
                                                    types.TryAdd(t, new DocInfo(Read(m, ElementSummary), Read(m, ElementRemarks)));
                                                }
                                                break;
                                            case 'F':
                                                {
                                                    var names = tn[1].Split('.');
                                                    if (!TryFindType(out var t, out var typeName, names, asm))
                                                        continue;
                                                    var mi = t.GetField(names[names.Length - 1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                                    if (mi == null)
                                                        continue;
                                                    if (!types.TryGetValue(t, out var dt))
                                                    {
                                                        dt = new DocInfo(null, null);
                                                        types.TryAdd(t, dt);
                                                    }
                                                    docAsm.Fields.TryAdd(mi, new DocInfo(Read(m, ElementSummary), Read(m, ElementRemarks)));
                                                }
                                                break;
                                            case 'P':
                                                {
                                                    var names = tn[1].Split('.');
                                                    if (!TryFindType(out var t, out var typeName, names, asm))
                                                        continue;
                                                    var mi = t.GetProperty(names[names.Length - 1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                                    if (mi == null)
                                                        continue;
                                                    if (!types.TryGetValue(t, out var dt))
                                                    {
                                                        dt = new DocInfo(null, null);
                                                        types.TryAdd(t, dt);
                                                    }
                                                    docAsm.Properties.TryAdd(mi, new DocInfo(Read(m, ElementSummary), Read(m, ElementRemarks)));
                                                }
                                                break;
                                            case 'M':
                                                {

                                                    var args = tn[1].Split('(');
                                                    var names = args[0].Split('.');
                                                    var argTypes = args.Length > 1 ? args[1].TrimEnd(')') : "";
                                                    if (!TryFindType(out var t, out var typeName, names, asm))
                                                        continue;
                                                    var mname = names[names.Length - 1].Replace('#', '.');
                                                    MethodInfo mi = null;
                                                    ConstructorInfo ci = null;
                                                    if (mname.StartsWith(".ctor", StringComparison.Ordinal))
                                                    {
                                                        try
                                                        {
                                                            ci = argTypes.Length > 0 ? null : t.GetConstructor(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, EmptyTypes, null);
                                                        }
                                                        catch
                                                        {
                                                        }
                                                        if (ci == null)
                                                        {
                                                            foreach (var mis in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                                                            {
                                                                String sig = mis.Name;
                                                                if (mis.IsGenericMethod)
                                                                    sig += GenericParameter + mis.GetGenericArguments().Length;
                                                                if (sig != mname)
                                                                    continue;
                                                                var pm = String.Join(',', mis.GetParameters().Select(x => GetParamaterSig(x)));
                                                                if (pm == argTypes)
                                                                {
                                                                    ci = mis;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                        if (ci == null)
                                                        {
#if DEBUG
                                                            foreach (var mis in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                                                            {
                                                                String sig = mis.Name;
                                                                if (mis.IsGenericMethod)
                                                                    sig += GenericParameter + mis.GetGenericArguments().Length;
                                                                if (sig != mname)
                                                                    continue;
                                                                var pm = String.Join(',', mis.GetParameters().Select(x => GetParamaterSig(x)));
                                                                if (pm == argTypes)
                                                                {
                                                                    ci = mis;
                                                                    break;
                                                                }
                                                            }
#endif//DEBUG
                                                            continue;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        try
                                                        {
                                                            mi = argTypes.Length > 0 ? null : t.GetMethod(mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, EmptyTypes, null);
                                                        }
                                                        catch
                                                        {
                                                        }
                                                        if (mi == null)
                                                        {
                                                            foreach (var mis in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                                                            {
                                                                if (mis == null)
                                                                    continue;
                                                                String sig = mis.Name;
                                                                if (mis.IsGenericMethod)
                                                                    sig += GenericParameter + mis.GetGenericArguments().Length;
                                                                if (sig != mname)
                                                                    continue;
                                                                var pm = String.Join(',', mis.GetParameters().Select(x => GetParamaterSig(x))).Replace('+', '.');
                                                                if (pm == argTypes)
                                                                {
                                                                    mi = mis;
                                                                    break;
                                                                }
                                                            }
                                                            if (mi == null)
                                                            {
#if DEBUG
                                                                foreach (var mis in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                                                                {
                                                                    String sig = mis.Name;
                                                                    if (mis.IsGenericMethod)
                                                                        sig += GenericParameter + mis.GetGenericArguments().Length;
                                                                    if (sig != mname)
                                                                        continue;
                                                                    var pm = String.Join(',', mis.GetParameters().Select(x => GetParamaterSig(x)));
                                                                    if (pm == argTypes)
                                                                    {
                                                                        mi = mis;
                                                                        break;
                                                                    }
                                                                }
#endif//DEBUG
                                                                continue;
                                                            }
                                                        }
                                                    }
                                                    if (!types.TryGetValue(t, out var dt))
                                                    {
                                                        dt = new DocInfo(null, null);
                                                        types.TryAdd(t, dt);
                                                    }
                                                    IXmlDocParameterInfo[] ps = null;
                                                    var psi = mi?.GetParameters() ?? ci?.GetParameters();
                                                    if (psi != null)
                                                    {
                                                        var psil = psi.Length;
                                                        if (psil > 0)
                                                        {
                                                            ps = new IXmlDocParameterInfo[psil];
                                                            Dictionary<String, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
                                                            for (int i = 0; i < psil; ++i)
                                                            {
                                                                var n = psi[i].Name;
                                                                if (!String.IsNullOrEmpty(n))
                                                                    indices[n] = i;
                                                            }
                                                            foreach (var pm in m.Elements(ElementParam))
                                                            {
                                                                var pname = pm.Attribute(AttrName)?.Value;
                                                                if (String.IsNullOrEmpty(pname))
                                                                    continue;
                                                                if (!indices.ContainsKey(pname))
                                                                    continue;
                                                                ps[indices[pname]] = new DocParameterInfo(pm.Value);
                                                            }
                                                        }
                                                    }
                                                    if (mi != null)
                                                        docAsm.Methods.TryAdd(mi, new DocMethodInfo(Read(m, ElementSummary), Read(m, ElementRemarks), Read(m, ElementReturns), ps));
                                                    else
                                                    {
                                                        if (ci != null)
                                                            docAsm.Constructors.TryAdd(ci, new DocMethodInfo(Read(m, ElementSummary), Read(m, ElementRemarks), Read(m, ElementReturns), ps));
                                                    }
                                                }
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                        if (!DocAssemblies.TryAdd(asm, docAsm))
                            throw new Exception("Internal error!");
                    }
                }
            }
            return docAsm;
        }



    }
}
