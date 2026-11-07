using SysWeaver.Net;
using SysWeaver.Docs;

using System;
using System.Collections.Generic;
using System.Reflection;
using SysWeaver.Data;
using System.Linq;
using SysWeaver.MicroService.EditInternal;
using System.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using SysWeaver.Translation;
using SysWeaver.Auth;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.MicroService
{


    [IsMicroService]
    [WebApiUrl("../edit")]
    [OptionalDep<ApiHttpServerModule>]
    public sealed class TypeService : IPerfMonitored, IDisposable
    {
        public TypeService(ServiceManager manager, TypeParams p = null)
        {
            Manager = manager;
            p = p ?? new TypeParams();
            PerfMon.Enabled = p.PerMon;
            ApiMod = manager.TryGet<ApiHttpServerModule>();
            manager.OnServiceAdded += OnServiceAdded;
            manager.OnServiceRemoved += OnServiceRemoved;

            var te = TextEditor;
            foreach (var x in TextExtensions)
                manager.TryAddExtensionEditor(x, te);
        }
        
        const string TextEditor = "edit/text.html?r={0}&u={1}&d={2}";
        
        /// <summary>
        /// Should be synched with the extensions supported in text.js
        /// </summary>
        static readonly String[] TextExtensions = [
                //  Text and logs
                "txt", "log", "err",
                //  Windows scripts
                "bat", "cmd", "ps1", "psm1", "psd1", "reg",
                //  Other scripts
                "sh", "bash", "zsh", "py", "rb", "php", "pl", "pm", "lua", "r", "tcl",
                //  C and C++
                "c", "c++", "cc", "cpp", "cxx", "h", "hh", "hpp", "hxx", "inl", "ipp", "ino",
                //  .NET
                "cs", "csx", "cshtml", "razor", "vb", "vbs", "fs", "fsx", "fsi", "sln", "slnx", "csproj", "vbproj", "fsproj", "props", "targets", "nuspec", "resx", "xaml", "axaml", "config",
                //  Other languages
                "go", "rs", "java", "kt", "kts", "swift", "dart", "scala", "groovy", "gradle", "asm",
                //  Shaders
                "glsl", "vert", "frag", "geom", "comp", "hlsl", "hlsli", "fx",
                //  Web
                "htm", "html", "xhtml", "css", "less", "scss", "sass", "js", "mjs", "cjs", "jsx", "ts", "mts", "cts", "tsx", "vue", "svg",
                //  Data and configuration
                "csv", "tsv", "json", "jsonc", "json5", "xml", "xsd", "xsl", "xslt", "yaml", "yml", "toml", "ini", "inf", "cfg", "conf", "properties", "sql", "proto", "gql",
                //  Documents
                "md", "rst", "tex", "adoc", "nfo", "diff", "patch",
                //  Build and repository
                "cmake", "make", "mk", "dockerfile", "tf", "gitignore", "gitattributes", "editorconfig",
            ];


        public void Dispose()
        {
            var manager = Manager;
            manager.OnServiceRemoved -= OnServiceRemoved;
            manager.OnServiceAdded -= OnServiceAdded;
            foreach (var x in TextExtensions)
                manager.TryRemoveExtensionEditor(x, out var _);
        }

        void OnServiceRemoved(object arg1, ServiceInfo arg2)
        {
            var s = arg1 as ApiHttpServerModule;
            if (s == null)
                return;
            ApiMod = null;
        }

        void OnServiceAdded(object arg1, ServiceInfo arg2)
        {
            var s = arg1 as ApiHttpServerModule;
            if (s == null)
                return;
            ApiMod = s;
        }


        ApiHttpServerModule ApiMod;

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(TypeService));

        readonly ServiceManager Manager;

        public override string ToString()
        {
            var t = TypeInfoCache.Count + TypeInfoCacheDebug.Count;
            return t.ToString() + (t == 1 ? " type cached" : " types cached");
        }

        readonly ConcurrentDictionary<String, TypeDesc> TypeInfoCache = new ConcurrentDictionary<string, TypeDesc>(StringComparer.Ordinal);
        readonly ConcurrentDictionary<String, TypeDesc> TypeInfoCacheDebug = new ConcurrentDictionary<string, TypeDesc>(StringComparer.Ordinal);


        /// <summary>
        /// Get a default instance of a type.
        /// Public writable fields and properties with an <see cref="EditDefaultAttribute"/> are set to that default value
        /// (the same default that <see cref="GetTypeInfo(string, HttpServerRequest)"/> reports), so a new object starts valid.
        /// </summary>
        /// <param name="typeName">Name of the type to get an instance from</param>
        /// <returns>An instance or null if it failed</returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public Object GetDefaultInstance(String typeName)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;
            var type = FindType(typeName);
            if (type == null)
                return null;
            return ApplyEditDefaults(TryCreate(type));
        }

        /// <summary>
        /// Set the public writable fields and properties of an instance that have an <see cref="EditDefaultAttribute"/> to that default value.
        /// Only the members of the instance itself are changed (not nested objects); a default that can't be converted to the member type is ignored.
        /// </summary>
        /// <param name="inst">The instance (may be null)</param>
        /// <returns>The instance</returns>
        static Object ApplyEditDefaults(Object inst)
        {
            if (inst == null)
                return null;
            foreach (var m in inst.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var a = m.GetCustomAttribute<EditDefaultAttribute>(true);
                    if (a == null)
                        continue;
                    if (m is FieldInfo f)
                    {
                        if (f.IsInitOnly || f.IsLiteral)
                            continue;
                        if (TryConvertDefault(f.FieldType, a.Def, out var v))
                            f.SetValue(inst, v);
                        continue;
                    }
                    if (m is PropertyInfo p)
                    {
                        if (!p.CanWrite || p.SetMethod?.IsPublic != true || p.GetIndexParameters().Length > 0)
                            continue;
                        if (TryConvertDefault(p.PropertyType, a.Def, out var v))
                            p.SetValue(inst, v);
                    }
                }
                catch
                {
                }
            }
            return inst;
        }

        /// <summary>
        /// Convert an <see cref="EditDefaultAttribute"/> value to a member type (invariant culture, enums by name or value).
        /// </summary>
        /// <param name="type">The member type</param>
        /// <param name="value">The default value</param>
        /// <param name="result">The converted value</param>
        /// <returns>True if the value could be converted</returns>
        static bool TryConvertDefault(Type type, Object value, out Object result)
        {
            result = null;
            var u = Nullable.GetUnderlyingType(type);
            if (value == null)
                return (!type.IsValueType) || (u != null);
            u = u ?? type;
            try
            {
                if (u.IsInstanceOfType(value))
                {
                    result = value;
                    return true;
                }
                if (u.IsEnum)
                {
                    result = value is String es ? Enum.Parse(u, es, true) : Enum.ToObject(u, value);
                    return true;
                }
                if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(u))
                {
                    result = Convert.ChangeType(value, u, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Get a type from a (untrusted) client supplied type name
        /// </summary>
        /// <param name="typeName">The name of the type</param>
        /// <returns>The type or null if it doesn't exist or isn't allowed by the <see cref="DataTypePolicy"/></returns>
        static Type FindType(String typeName)
        {
            try
            {
                return TypeFinder.GetForData(typeName, typeof(Object));
            }
            catch (DataTypeNotAllowedException)
            {
                return null;
            }
        }

        /// <summary>
        /// Get information about a type
        /// </summary>
        /// <param name="typeName">Name of the type</param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public TypeDesc GetTypeInfo(String typeName, HttpServerRequest request)
        {
            if (String.IsNullOrEmpty(typeName))
                return null;

            var key = typeName;


            bool haveDebug = request?.Session?.Auth?.IsValid(Authorization.RoleDebug) ?? false;
            var cache = haveDebug ? TypeInfoCacheDebug : TypeInfoCache;
            if (cache.TryGetValue(key, out var v))
                return v;
            var type = FindType(typeName);
            if (type == null)
            {
                cache.TryAdd(key, null);
                return null;

            }
            if (!type.IsPublic)
            {
                cache.TryAdd(key, null);
                return null;
            }
            v = GetTypeInfo(type, typeName, haveDebug, GetInfo(type));
//            v = await Translate(v, request.Translator, lang).ConfigureAwait(false);
            cache.TryAdd(key, v);
            return v;
        }

        static Object TryCreate(Type t)
        {
            if (typeof(IDisposable).IsAssignableFrom(t))
                return null;
            if (t.IsAbstract || t.IsInterface)
                return null;
            var ci = t.GetConstructor([]);
            if (ci == null)
                return null;
            try
            {
                return Activator.CreateInstance(t);
            }
            catch
            {
                return null;
            }
        }
        
        static Object GetDefault(FieldInfo f, Object o)
        {
            if (o == null)
                return null;
            try
            {
                return f.GetValue(o);
            }
            catch
            {
                return null;
            }
        }

        static Object GetDefault(PropertyInfo f, Object o)
        {
            if (o == null)
                return null;
            try
            {
                return f.GetValue(o);
            }
            catch
            {
                return null;
            }
        }


        static readonly IReadOnlySet<Type> Primitives = ReadOnlyData.Set(
            typeof(String),
            typeof(Object),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateOnly),
            typeof(TimeOnly)
        );



        static GetBaseInfo GetInfo(ParameterInfo p) => p == null ? null : new GetParamaterInfo(p);
        static GetBaseInfo GetInfo(FieldInfo p) => p == null ? null : new GetFieldInfo(p);
        static GetBaseInfo GetInfo(PropertyInfo p) => p == null ? null : new GetPropertyInfo(p);

        static GetBaseInfo GetInfo(Type p) => p == null ? null : new GetTypeInfoX(p);

        static GetBaseInfo GetInfo(MemberInfo p)
        {
            if (p == null)
                return null;
            switch (p.MemberType)
            {
                case MemberTypes.Field:
                    return new GetFieldInfo(p as FieldInfo);
                case MemberTypes.Property:
                    return new GetPropertyInfo(p as PropertyInfo);
                case MemberTypes.TypeInfo:
                    return new GetTypeInfoX(p as Type);
                default:
                    throw new Exception("Invalid type!");
            }
        }


        static GetBaseInfo GetKeyInfo(GetBaseInfo b)
        {
            var a = b.GetAttribute<EditKeyAttributesAttribute>();
            if (a == null)
                return null;
            var m = a.T.GetMember(b.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault();
            return GetInfo(m);
        }
        static GetBaseInfo GetElementInfo(GetBaseInfo b)
        {
            var a = b.GetAttribute<EditElementAttributesAttribute>();
            if (a == null)
                return null;
            var m = a.T.GetMember(b.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault();
            return GetInfo(m);
        }

        static TypeInstanceDesc GetInstanceInfo(GetBaseInfo p, bool haveDebug)
        {
            if (p == null)
                return null;
            var t = new TypeInstanceDesc();
            SetInstanceInfo(out var elementType, out var keyType, t, p, haveDebug);
            return t;
        }

        struct FilterState
        {
            public readonly String S;
            public readonly IReadOnlySet<Char> I;
            public readonly Char R;

            public FilterState(string s, IReadOnlySet<char> i, char r)
            {
                S = s;
                I = i.Freeze();
                R = r;
            }

            public static void DoIt(Span<Char> to, FilterState state)
            {
                var inv = state.I;
                var s = state.S;
                var l = to.Length;
                var r = state.R;
                for (int i = 0; i < l; ++ i)
                {
                    var c = s[i];
                    if (inv.Contains(c))
                        c = r;
                    to[i] = c;
                }
            }
        }


        static String FilterChars(String s, IReadOnlySet<Char> invalid, Char replaceWith = '_')
        {
            if (String.IsNullOrEmpty(s))
                return s;
            return String.Create(s.Length, new FilterState(s, invalid, replaceWith), FilterState.DoIt);
        }

        static readonly IReadOnlySet<Char> InvalidComment = ReadOnlyData.Set("|<>".ToCharArray());

        static String GetExternalLink(Type type)
        {
            if (type.HasElementType)
                type = type.GetElementType();
            if (type.IsGenericType)
                type = type.GetGenericTypeDefinition();
            var asm = type.Assembly;
            if (asm == null)
                return null;
            var asmName = asm.FullName.Split(',')[0].Trim();
            if (asmName == "System.Private.CoreLib")
            {
                var name = type.FullName.Replace('`', '-').FastToLower();
                return String.Join(name, "https://learn.microsoft.com/en-us/dotnet/api/", "?view=net-8.0");
            }
            return null;
        }

        static void SetInstanceInfo(out Type elementType, out Type keyType, TypeInstanceDesc desc, GetBaseInfo p, bool haveDebug)
        {
            var displayName = p.GetAttribute<EditDisplayNameAttribute>()?.Name;
            if (String.IsNullOrEmpty(displayName))
                displayName = (p.Name ?? "Unknown").RemoveCamelCase().MakeFirstUppercase();
            desc.DisplayName = displayName;
            desc.Summary = p.Summary;
            desc.Remarks = p.Remarks;
            var type = p.Type;
            bool isPrim = type.IsPrimitive || Primitives.Contains(type) || type.IsEnum;
            if (isPrim)
                desc.Flags |= TypeMemberFlags.IsPrimitive;
            bool allowNull = p.GetAttribute<EditAllowNullAttribute>()?.AllowNull ?? false;


            var def = p.Def;
            var defAttr = p.GetAttribute<EditDefaultAttribute>();
            bool haveDef = p.HaveDefault || (defAttr != null);
            def = haveDef ? ObjectToValueString(type, defAttr != null ? defAttr.Def : def) : null;
            if (!haveDef)
            {
                if (type.IsValueType)
                {
                    if (type.IsGenericType)
                    {
                        if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            if (allowNull)
                                def = null;
                            else
                                def = "\t";
                            haveDef = true;
                        }
                    }
                    if (!haveDef)
                    {
                        try
                        {
                            def = Activator.CreateInstance(type).ToString();
                            haveDef = true;
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    if (allowNull)
                        def = null;
                    else
                        def = "\t";
                    haveDef = true;
                }
            }
            allowNull |= (haveDef && (def == null));

            if (p.GetAttribute<EditMultilineAttribute>()?.AllowMultiLine ?? false)
                desc.Flags |= TypeMemberFlags.Multiline;
            if (p.GetAttribute<EditPasswordAttribute>()?.IsPassword ?? false)
                desc.Flags |= TypeMemberFlags.Password;
            if (p.GetAttribute<EditHideAttribute>()?.Hide ?? false)
                desc.Flags |= TypeMemberFlags.Hide;
            if (p.GetAttribute<EditReadOnlyAttribute>()?.ReadOnly ?? false)
                desc.Flags |= TypeMemberFlags.ReadOnly;
            var sattr = p.GetAttribute<EditSliderAttribute>();
            if (sattr?.UseSlider ?? false)
            {
                desc.EditParams = sattr.Step > 0 ? sattr.Step.ToString(CultureInfo.InvariantCulture) : null;
                desc.Flags |= TypeMemberFlags.Slider;
            }
            if (allowNull)
                desc.Flags |= TypeMemberFlags.AcceptNull;
            desc.Type = p.GetAttribute<EditTypeAttribute>()?.Type;
            desc.Default = def?.ToString();
            desc.DisplayName = displayName;
            if (type.IsEnum)
            {
                if (!Enum.TryParse(type, def?.ToString(), true, out var res))
                    res = Activator.CreateInstance(type);
                desc.Flags |= TypeMemberFlags.IsEnum;
                if (type.GetCustomAttributes<FlagsAttribute>().Any())
                    desc.Flags |= TypeMemberFlags.IsFlags;
                var dd = Convert.ChangeType(res, type.GetEnumUnderlyingType()).ToString();
                var names = Enum.GetNames(type);
                var values = Enum.GetValuesAsUnderlyingType(type);
                var c = names.Length;
                var vals = GC.AllocateUninitializedArray<String>(c);
                var inv = InvalidComment;
                for (int i = 0; i < c; ++i)
                {
                    var name = names[i];
                    var di = type.XmlDocEnum(name);
                    vals[i] = String.Join('<', values.GetValue(i), name, FilterChars(di?.Summary, inv), FilterChars(di?.Remarks, inv));
                }
                desc.Default = String.Join('>', dd, String.Join('|', vals));
                elementType = type.GetEnumUnderlyingType();
            }


            var rangeType = type;
            if (GetElementType(out bool indexed, out elementType, out keyType, type))
            {
                desc.Flags |= TypeMemberFlags.Collection;
                if (indexed)
                    desc.Flags |= TypeMemberFlags.Indexed;
                if (keyType != null)
                {
                    var info = GetKeyInfo(p);
                    desc.KeyInst = info == null ? null : GetInstanceInfo(info, haveDebug);
                }
                if (elementType != null)
                {
                    var info = GetElementInfo(p);
                    desc.ElementInst = info == null ? null : GetInstanceInfo(info, haveDebug);
                }
                rangeType = typeof(int);
            }else
            {
                if (type.IsClass)
                    desc.Flags |= TypeMemberFlags.IsObject;
            }
            String min = null;
            String max = null;
            var range = p.GetAttribute<EditRangeAttribute>();
            if (range != null)
            {
                min = ObjectToValueString(rangeType, range.MinValue);
                max = ObjectToValueString(rangeType, range.MaxValue);
            }
            min = min ?? ObjectToValueString(rangeType, p.GetAttribute<EditMinAttribute>()?.MinValue);
            max = max ?? ObjectToValueString(rangeType, p.GetAttribute<EditMaxAttribute>()?.MaxValue);
            desc.Min = min;
            desc.Max = max;
            if (DateTypes.Contains(type))
                if (p.GetAttribute<EditDateUnspecifiedAttribute>() != null)
                    desc.Flags |= TypeMemberFlags.DateUnspecified;
        }

        static readonly IReadOnlySet<Type> DateTypes = ReadOnlyData.Set(typeof(DateTime), typeof(DateOnly));

        static String GetTypeNameNoAsm(Type type)
        {
            if (type == null)
                return null;
            if (type.IsArray)
                return String.Concat(GetTypeNameNoAsm(type.GetElementType()), '[', new String(',', type.GetArrayRank() - 1), ']');
            if (!type.IsGenericType)
                return type.FullName;
            var name = type.GetGenericTypeDefinition().FullName;
            return String.Concat(name, '[', String.Join(", ", type.GetGenericArguments().Select(x => String.Join(GetTypeNameNoAsm(x), '[', ']'))), ']');
        }



        static TypeDesc GetTypeInfo(Type type, String typeName, bool haveDebug, GetBaseInfo info)
        {
            if (type == null)
                return null;
            info = info ?? GetInfo(type);
            var inst = TryCreate(type);
            var desc = new TypeDesc();
            desc.TypeName = GetTypeNameNoAsm(type);
            desc.Asm = type.Assembly.GetName().Name;
            SetInstanceInfo(out var elementType, out var keyType, desc, info, haveDebug);
            desc.Ext = GetExternalLink(type);
            if ((desc.Flags & TypeMemberFlags.IsPrimitive) != 0)
            {
                desc.Members = [GetDesc(info, haveDebug).Item2];
                return desc;
            }
            if ((desc.Flags & TypeMemberFlags.Collection) != 0)
            {
                desc.Flags |= TypeMemberFlags.IsPrimitive;
                desc.Members = [GetDesc(info, haveDebug).Item2];
                if (keyType != null)
                {
                    var keyInfo = GetKeyInfo(info);
                    desc.KeyInst = keyInfo == null ? null : GetInstanceInfo(keyInfo, haveDebug);
                    desc.KeyTypeName = GetTypeNameNoAsm(keyType);
                }
                if (elementType != null)
                {
                    var keyInfo = GetElementInfo(info);
                    desc.ElementInst = keyInfo == null ? null : GetInstanceInfo(keyInfo, haveDebug);
                    desc.ElementTypeName = GetTypeNameNoAsm(elementType);
                }
                return desc;
            }
            var members = new List<Tuple<float, TypeMemberDesc>>();
            foreach (var x in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                switch (x.MemberType)
                {
                    case MemberTypes.Field:
                        if ((x as FieldInfo).IsInitOnly)
                            continue;
                        members.Add(GetDesc(GetInfo(x), haveDebug));
                        break;
                    case MemberTypes.Property:
                        if (!(x as PropertyInfo).CanWrite)
                            continue;
                        members.Add(GetDesc(GetInfo(x), haveDebug));
                        break;
                }
            }
            if (members.Count > 0)
                desc.Members = members.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
            return desc;
        }


        static String ObjectToValueString(Type type, Object value)
        {
            if (value == null)
                return null;
            var valType = value.GetType();
            if (valType == typeof(String))
                return value as String;
            try
            {
                value = Convert.ChangeType(value, type);
            }
            catch
            {
                if (value != null)
                {
                    try
                    {
                        var ci = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [valType]);
                        if (ci == null)
                            return null;
                        value = ci.Invoke(null, [value]);
                    }
                    catch
                    {
                    }
                }
            }
            if (value == null)
                return null;
            return value.ToString();
        }

        static bool GetElementType(out bool isIndexed, out Type elementType, out Type keyType, Type t)
        {
            isIndexed = false;
            elementType = null;
            keyType = null;
            if (t.IsArray)
            {
//                if (t.GetArrayRank() != 1)
//                    throw new Exception("Only one dimensional arrays are supported!");
                isIndexed = true;
                elementType = t.GetElementType();
                return true;
            }
            if (!t.IsGenericType)
                return false;
            var ga = t.GetGenericArguments();
            var gal = ga.Length;
            if (gal == 1)
            {
                if (typeof(IReadOnlyList<>).MakeGenericType(ga).IsAssignableFrom(t))
                {
                    elementType = ga[0];
                    isIndexed = true;
                    return true;
                }
                if (typeof(IReadOnlySet<>).MakeGenericType(ga).IsAssignableFrom(t))
                {
                    keyType = ga[0];
                    isIndexed = false;
                    return true;
                }
            }
            if (gal == 2)
            {
                if (typeof(IReadOnlyDictionary<,>).MakeGenericType(ga).IsAssignableFrom(t))
                {
                    keyType = ga[0];
                    elementType = ga[1];
                    isIndexed = false;
                    return true;
                }
            }
            return false;
        }




        static Tuple<float, TypeMemberDesc> GetDesc(GetBaseInfo info, bool haveDebug)
        {
            var desc = new TypeMemberDesc();
            desc.Name = info.Name;
            desc.TypeName = GetTypeNameNoAsm(info.Type);
            SetInstanceInfo(out var elementType, out var keyType, desc, info, haveDebug);
            desc.Ext = GetExternalLink(info.Type);
            desc.ElementTypeName = GetTypeNameNoAsm(elementType);
            desc.KeyTypeName = GetTypeNameNoAsm(keyType);
            var order = info.GetAttribute<EditOrderAttribute>()?.Order ?? 0;
            return Tuple.Create(order, desc);
        }

        /// <summary>
        /// Get information about a type's member as a data table
        /// </summary>
        /// <param name="r">Request params</param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        public TableData GetTypeInfoTable(TableDataRequest r, HttpServerRequest request)
        {
            var td = GetTypeInfo(r?.Param, request);
            if (td == null)
                return null;
            var m = td.Members;
            if (m == null)
                return null;
            return TableDataTools.Get(r, 30000, m);
        }


        readonly ConcurrentDictionary<String, ApiInfo> ApiInfoCache = new ConcurrentDictionary<string, ApiInfo>(StringComparer.Ordinal);
        readonly ConcurrentDictionary<String, ApiInfo> ApiInfoCacheDebug = new ConcurrentDictionary<string, ApiInfo>(StringComparer.Ordinal);

        /// <summary>
        /// Get detailed information about an API (signature, types, auth etc).
        /// Restricted to developers (Dev and Debug, the same as the list of all APIs), user interfaces that only need to know if the user may call an API use "Api/auth/CanCall".
        /// </summary>
        /// <param name="url">The api url (relative to the base url) to get information about</param>
        /// <param name="request"></param>
        /// <returns>Detailed information or null if not found</returns>
        [WebApi("../Api/debug/" + nameof(GetApiInfo))]
        [WebApiAuth(Roles.Dev)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        public ApiInfo GetApiInfo(String url, HttpServerRequest request)
        {
            var m = ApiMod;
            if (m == null)
                return null;
            if (String.IsNullOrEmpty(url))
                return null;
            var key = url;

            bool haveDebug = request?.Session?.Auth?.IsValid(Authorization.RoleDebug) ?? false;
            var cache = haveDebug ? ApiInfoCacheDebug : ApiInfoCache;
            if (cache.TryGetValue(key, out var v))
                return v;
            var i = m.GetApiInfo(out var arg, out var ret, out var mi, out var pi, out var ri, out var retMime, url);
            if (i == null)
            {
                cache.TryAdd(key, null);
                return null;
            }
            var r = new ApiInfo();
            i.CopyTo(r);
            bool isDebug = request?.Session?.Auth?.IsValid(Authorization.RoleDebug) ?? false;
            if (arg != null)
            {
                r.Arg = GetTypeInfo(arg, arg.FullName, isDebug, GetInfo(pi));
                if (pi != null)
                {
                    r.ArgName = pi.Name;
                    r.ArgSummary = pi.XmlDoc()?.Param;
                }
            }
            if (ret != null)
            {
                if (ret == typeof(ReadOnlyMemory<Byte>))
                    ret = typeof(Byte[]);
                if (ret == typeof(Memory<Byte>))
                    ret = typeof(Byte[]);
                var rti = GetTypeInfo(ret, ret.FullName, isDebug, GetInfo(ri));
                rti.DisplayName = "Value";
                rti.Flags |= TypeMemberFlags.ReadOnly;
                if (rti.Members != null)
                {
                    if (((rti.Flags & TypeMemberFlags.IsObject) == 0) || ((rti.Flags & TypeMemberFlags.IsPrimitive) != 0))
                        if (rti.Members.Length == 1)
                            rti.Members[0].DisplayName = ret.Name;
                    foreach (var mm in rti.Members)
                        mm.Flags |= TypeMemberFlags.ReadOnly;
                }
                r.Return = rti;
            }
            r.Mime = retMime;
            //r = await Translate(r, request.Translator, lang).ConfigureAwait(false);
            cache.TryAdd(key, r);
            return r;
        }

        /// <summary>
        /// How the APIs of this service can be called: the enabled input / output serializers, the compression methods of a request body
        /// and of a GET argument, and the request size limits (shown by explore/apihelp.html).
        /// Restricted like <see cref="GetApiInfo"/> (Dev and Debug).
        /// </summary>
        /// <returns>The information, null when the service has no API module</returns>
        [WebApi("../Api/debug/" + nameof(GetApiIoInfo))]
        [WebApiAuth(Roles.Dev)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        public ApiIoInfo GetApiIoInfo()
        {
            var io = ApiMod?.IoParams;
            if (io == null)
                return null;
            static ApiIoSerializer[] sers<T>(IEnumerable<T> l) where T : SysWeaver.Serialization.ISerializerInfo
                => l.Distinct().Select(x => new ApiIoSerializer
                {
                    Name = x.Name,
                    Mime = x.Mime,
                    Extension = x.Extension,
                    IsText = x.Encoding != null,
                }).OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            return new ApiIoInfo
            {
                Input = sers(io.InputSerializers.Values),
                Output = sers(io.OutputSerializers.Values),
                DefaultInput = io.DefaultInput?.Name,
                DefaultOutput = io.DefaultOutput?.Name,
                Compression = SysWeaver.Compression.CompManager.HttpCodes.Order(StringComparer.Ordinal).ToArray(),
                GetCompression = ApiIoParams.BinaryArgTypes.Select(x => new ApiIoGetCompression { Char = x.Key.ToString(), HttpCode = x.Value }).ToArray(),
                MaxRequestSize = io.MaxRequestSize,
                MaxDecompressedSize = io.MaxDecompressedSize,
            };
        }


    }

#if DEBUG


    static class EditTestAttributes
    {
#pragma warning disable CS0169
        [EditMax(512)]
        static String Files;
#pragma warning restore CS0169
    }




    public class EditTestObject
    {
        /// <summary>
        /// Name of something
        /// </summary>
        public String Name;

        /// <summary>
        /// Percentage weight
        /// </summary>
        [EditRange(0, 100)]
        [EditSlider]
        public float Weight;
    }


    public class EditNestedTestObject
    {
        /// <summary>
        /// Name of something
        /// </summary>
        [EditDefault(true)]
        public bool Enabled;

        /// <summary>
        /// Testing a single object that may be null
        /// </summary>
        [EditAllowNull]
        public EditTestObject Object;
    }



    /// <summary>
    /// A type just used for testing the edit properties and features
    /// </summary>
    public class EditTest
    {
        /// <summary>
        /// A regular string
        /// </summary>
        [EditDefault("Hello world!")]
        [EditMax(10)]
        public String Text;

        /// <summary>
        /// A multi line string
        /// </summary>
        [EditDisplayName("Multi line text")]
        [EditMultiline]
        public String Multiline;

        /// <summary>
        /// A string that may be null
        /// </summary>
        [EditDefault(null)]
        [EditMax(200)]
        [EditAllowNull]
        public String NullableText;

        /// <summary>
        /// Password
        /// </summary>
        [EditDefault("")]
        [EditMax(64)]
        [EditPassword]
        public String Password;


        /// <summary>
        /// A regular int
        /// </summary>
        public int Value;

        /// <summary>
        /// A range
        /// </summary>
        [EditRange(1, 10)]
        [EditDefault(5)]
        [EditSlider]
        public int Range;


        /// <summary>
        /// A range
        /// </summary>
        [EditRange(0, 32767)]
        [EditDefault(100)]
        [EditSlider]
        public int LargeRange;

        /// <summary>
        /// A double number
        /// </summary>
        [EditDefault(Math.PI)]
        public double NumberDouble;


        /// <summary>
        /// A single range
        /// </summary>
        [EditRange(0, 100)]
        [EditDefault(50)]
        [EditSlider]
        public float SingleRange;


        /// <summary>
        /// Testing array of primitive
        /// </summary>
        [EditMax(10)]
        [EditElementAttributes(typeof(EditTestAttributes))]
        public String[] Files;

        /// <summary>
        /// Nullable array of integers
        /// </summary>
        [EditMax(10)]
        [EditAllowNull]
        public int[] NullValues;

        /// <summary>
        /// Testing array of objects
        /// </summary>
        [EditMax(5)]
        public EditTestObject[] Values;


        /// <summary>
        /// A simple boolean
        /// </summary>
        public bool BoolTest;

        /// <summary>
        /// A simple boolean, with a default true value
        /// </summary>
        public bool BoolEnabled = true;


        /// <summary>
        /// Testing a single object
        /// </summary>
        public EditTestObject Object;

        /// <summary>
        /// Testing nested objects
        /// </summary>
        public EditNestedTestObject NestedObject;

        /// <summary>
        /// A css color string (#rrggbb)
        /// </summary>
        [EditColor]
        [EditDefault("#ff8800")]
        public String CssColor;

        /// <summary>
        /// A css color string with alpha (#rrggbbaa)
        /// </summary>
        [EditColor(EditColorFormat.Css, true)]
        [EditDefault("#3366ff80")]
        public String CssColorAlpha;

        /// <summary>
        /// A HDR color with alpha as 4 floats (r, g, b may exceed 1)
        /// </summary>
        [EditColor(EditColorFormat.HdrRgba, maxIntensity: 8)]
        public float[] HdrColor = [2, 1.5f, 0.5f, 1];

        /// <summary>
        /// A color as an integer 0xRRGGBBAA
        /// </summary>
        [EditColor(EditColorFormat.UInt32Rgba)]
        public uint PackedColor = 0xff8800ff;

        /// <summary>
        /// A color as an integer 0xAARRGGBB (System.Drawing.Color.ToArgb)
        /// </summary>
        [EditColor(EditColorFormat.UInt32Argb)]
        public int ArgbColor = unchecked((int)0xff336699);

        /// <summary>
        /// A duration (TimeSpan)
        /// </summary>
        public TimeSpan Timeout = TimeSpan.FromSeconds(90);

        /// <summary>
        /// An id (Guid)
        /// </summary>
        public Guid Id;

        /// <summary>
        /// An e-mail address
        /// </summary>
        [EditType(EditTypes.Email)]
        public String Email = "";

        /// <summary>
        /// An url
        /// </summary>
        [EditType(EditTypes.Url)]
        public String Url = "https://example.com/";

        /// <summary>
        /// A phone number
        /// </summary>
        [EditType(EditTypes.Phone)]
        public String Phone = "+1 555 0100";

        /// <summary>
        /// A css color that is always stored as #rrggbb (typed css colors are converted)
        /// </summary>
        [EditColor(EditColorFormat.Hex)]
        public String HexColor = "#336699";

        /// <summary>
        /// A duration in seconds (EditUnit: type 90, 90s, 1h 30m)
        /// </summary>
        [EditUnit(EditUnits.Seconds)]
        [EditRange(1, 86400)]
        public int TimeoutSeconds = 90;

        /// <summary>
        /// A size in bytes (EditUnit: type 2 MB, 512k)
        /// </summary>
        [EditUnit(EditUnits.Bytes)]
        public long MaxSize = 2 << 20;

        /// <summary>
        /// A quality in percent (EditUnit with a slider)
        /// </summary>
        [EditUnit(EditUnits.Percent)]
        [EditRange(10, 100)]
        [EditSlider]
        public int Quality = 80;

        /// <summary>
        /// An opacity as a ratio, 0 to 1 (EditUnit: type 0.25 or 25%)
        /// </summary>
        [EditUnit(EditUnits.Ratio)]
        [EditRange(0, 1)]
        public double Opacity = 0.25;

        /// <summary>
        /// A read only nested object (nothing inside may become writable)
        /// </summary>
        [EditReadOnly]
        public EditTestObject ReadOnlyObject = new EditTestObject { Name = "Fixed", Weight = 25 };

        /// <summary>
        /// A read only array of objects
        /// </summary>
        [EditReadOnly]
        public EditTestObject[] ReadOnlyValues = [new EditTestObject { Name = "A", Weight = 1 }, new EditTestObject { Name = "B", Weight = 2 }];
    }

#endif//DEBUG

}
