using SysWeaver.Data;
using SysWeaver.Docs;
using SysWeaver.MicroService;
using SysWeaver.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver.Net
{

    public sealed class ApiHttpServerModule : IHttpServerModule, IPerfMonitored
    {

        static IReadOnlyList<T> GetSers<T>(String ser, ISerializerType def) where T : ISerializerInfo
        {
            List<T> d = new();
            bool addDef = true;
            if (ser != null)
            {
                foreach (var x in ser.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var s = SerManager.Get(x);
                    if (s == null)
                        continue;
                    if (s == def)
                        addDef = false;
                    d.Add((T)s);
                }
            }
            if (addDef)
                d.Add((T)def);
            return d;
        }

        public ApiHttpServerModule(ApiHttpServerModuleParams p = null)
        {
            var pp = p ?? new ApiHttpServerModuleParams();
            var dn = pp.DefaultSerializer?.Trim();
            var def = SerManager.Get(String.IsNullOrEmpty(dn) ? "json" : dn) ?? SerManager.ExtensionHandlers?.FirstOrDefault().Value ?? throw new NullReferenceException("No serializers found!");
            PerfMon.Enabled = p.PerMon;
            Root = pp.Root;
            Auth = pp.Auth;
            CachedCompression = pp.CachedCompression;
            Compression = pp.Compression;
            IoParams = new ApiIoParams(
                GetSers<IDeserializer>(p.InputSerializers, def),
                GetSers<ISerializer>(p.OutputSerializers, def),
                def,
                def
                );
            DefaultSerializer = def;
        }
        public readonly ISerializerType DefaultSerializer;
        public override string ToString() => String.Concat(
            nameof(Root), ": ", Root.ToQuoted(), ", ",
            nameof(Entries), ": ", Entries.Count);

        readonly String Root;
        readonly String Auth;
        readonly String CachedCompression;
        readonly String Compression;


        public bool AddObject(Object o, String root = null)
        {
            var t = o.GetType();
            bool foundAny = false;
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                var a = m.GetCustomAttributeWithInterface<WebApiAttribute>(true);
                if (a == null)
                    continue;
                AddMethod(o, m, a.Url, root);
                foundAny = true;
            }
            return foundAny;
        }

        public bool RemoveObject(Object o, String root = null)
        {
            var t = o.GetType();
            bool foundAny = false;
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                var a = m.GetCustomAttributeWithInterface<WebApiAttribute>(true);
                if (a != null)
                {
                    RemoveMethod(o, m, (a as WebApiAttribute)?.Url, root);
                    foundAny = true;
                }
            }
            return foundAny;
        }

        public void AddMethod(Object o, MethodInfo method, String url = null, String root = null)
        {
            //  Handle the optional attribute (dynamically exclude some API's, depending on config etc)
            var check = method.GetCustomAttribute<WebApiOptionalAttribute>(true)?.MemberName;
            if (check != null)
            {
                var ot = o.GetType();
                var fi = ot.GetField(check, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    if (fi.FieldType != typeof(Boolean))
                        throw new Exception(check.ToQuoted() + " in " + ot.FullName.ToQuoted() + " must be a Boolean field for use with the " + nameof(WebApiOptionalAttribute).ToQuoted() + " attribute");
                    if (!((Boolean)fi.GetValue(o)))
                        return;
                } else
                {
                    var pi = ot.GetProperty(check, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pi != null)
                    {
                        if (pi.PropertyType != typeof(Boolean))
                            throw new Exception(check.ToQuoted() + " in " + ot.FullName.ToQuoted() + " must be a Boolean property for use with the " + nameof(WebApiOptionalAttribute).ToQuoted() + " attribute");
                        if (!((Boolean)pi.GetValue(o)))
                            return;
                    }
                    else
                    {
                        var mi = ot.GetMethod(check, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Array.Empty<Type>());
                        if (mi != null)
                        {
                            if (mi.ReturnType != typeof(Boolean))
                                throw new Exception(check.ToQuoted() + " in " + ot.FullName.ToQuoted() + " must be return Boolean for use with the " + nameof(WebApiOptionalAttribute).ToQuoted() + " attribute");
                            if (!((Boolean)mi.Invoke(o, null)))
                                return;
                        }
                        else
                        {
                            throw new Exception(check.ToQuoted() + " is not a valid member in " + ot.FullName.ToQuoted() + " for use with the " + nameof(WebApiOptionalAttribute).ToQuoted() + " attribute");
                        }
                    }
                }
            }
            var baseUrl = GetBaseUrl(method, url, root);
            InternalAddEndPoint(o, method, baseUrl);
        }

        public void RemoveMethod(Object o, MethodInfo method, String url = null, String root = null)
        {
            var baseUrl = GetBaseUrl(method, url, root);
            var entries = Entries;
            var or = OnApiRemoved;
            if (entries.TryRemove(baseUrl, out var ep))
            {
                try
                {
                    or?.Invoke(ep);
                }
                catch
                {
                }
            }
        }

        String GetBaseUrl(MethodInfo method, String url, String root)
        {
            String typeUrl = null;
            var t = method.DeclaringType;
            if (t != null)
            {
                var a = t.GetCustomAttribute<WebApiUrlAttribute>(true);
                if (a != null)
                {
                    typeUrl = a.Url;
                    if (typeUrl != null)
                        typeUrl = typeUrl.Replace("{0}", t.Name);
                }
            }
            var mn = method.Name;
            if (url != null)
                url = url.Replace("{0}", mn);
            var baseUrl = HttpServerTools.CleanupPaths(HttpServerTools.CombinePaths(Root, root, typeUrl, url ?? mn));
            return baseUrl;
        }

        /// <summary>
        /// Signature of method invoked before an audited API is invoked
        /// </summary>
        /// <param name="id">A unique invoke id</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="value">The input value (can be used to inspect data), can be null for void API's</param>
        public delegate void AuditBeginDel(long id, HttpServerRequest r, IApiHttpServerEndPoint api, Object value);

        /// <summary>
        /// Signature of method invoked after an audited API is invoked (if no exception in thrown)
        /// </summary>
        /// <param name="id">A unique invoke id (same as for the begin)</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="value">The output value (can be used to inspect data), can be null for void API's</param>
        public delegate void AuditEndDel(long id, HttpServerRequest r, IApiHttpServerEndPoint api, Object value);

        /// <summary>
        /// Signature of method invoked if an audited API throws an exception
        /// </summary>
        /// <param name="id">A unique invoke id (same as for the begin)</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="ex">The exception object thrown</param>
        public delegate void AuditExceptionDel(long id, HttpServerRequest r, IApiHttpServerEndPoint api, Exception ex);

        /// <summary>
        /// Invoked before an audited API is invoked
        /// </summary>
        public event AuditBeginDel OnAuditBegin;

        /// <summary>
        /// Invoked after an audited API is invoked (if no exception in thrown)
        /// </summary>
        public event AuditEndDel OnAuditEnd;

        /// <summary>
        /// Invoked if an audited API throws an exception
        /// </summary>
        public event AuditExceptionDel OnAuditException;

        void AuditBegin(long id, HttpServerRequest r, ApiHttpEntry api, Object value)
        {
            var fix = api.FilterAuditParams;
            if (fix != null)
            {
                try
                {
                    value = fix(id, r, value);
                }
                catch
                {
                }
            }
            OnAuditBegin?.Invoke(id, r, api, value);
        }

        void AuditEnd(long id, HttpServerRequest r, ApiHttpEntry api, Object value)
        {
            var fix = api.FilterAuditReturn;
            if (fix != null)
            {
                try
                {
                    value = fix(id, r, value);
                }
                catch
                {
                }
            }
            OnAuditEnd?.Invoke(id, r, api, value);
        }

        void AuditException(long id, HttpServerRequest r, ApiHttpEntry api, Exception ex)
        {
            OnAuditException?.Invoke(id, r, api, ex);
        }

        readonly ApiIoParams IoParams;

        void InternalAddEndPoint(Object o, MethodInfo method, String url)
        {
            var entries = Entries;
            if (entries.ContainsKey(url))
                return;
            lock (entries)
            {
                if (entries.ContainsKey(url))
                    return;
                var e = ApiHttpEntry.Create(IoParams, o, method, url, PerfMon, Auth, CachedCompression, Compression, LocationPrefix, AuditBegin, AuditEnd, AuditException);
                entries[url] = e;
                if ((e.RetType == typeof(TableData)) && (e.ArgType == typeof(TableDataRequest)))
                    Tables[e] = Interlocked.Increment(ref TableId);
                OnApiAdded?.Invoke(e);
            }
        }

        int TableId;

        readonly ConcurrentDictionary<ApiHttpEntry, int> Tables = new();


        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            if (!Entries.TryGetValue(context.LocalUrl, out var e))
                return null;
            return context.HttpMethod == HttpServerMethods.Other ? null : e;
        }

        const String LocationPrefix = "[Api] ";


        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            if (root == null)
            {
                foreach (var x in Entries)
                {
                    yield return (x.Value as IHttpServerEndPoint) ?? throw new NullReferenceException();
                }
            }
            else
            {
                root = HttpServerTools.FixEnumRoot(root);
                HashSet<String> folders = new HashSet<string>();
                var ul = root.Length;
                foreach (var x in Entries)
                {
                    var url = x.Key;
                    if (!url.FastStartsWith(root))
                        continue;
                    var f = url.IndexOf('/', ul);
                    if (f < 0)
                        yield return (x.Value as IHttpServerEndPoint) ?? throw new NullReferenceException();
                    else
                    {
                        var folderName = url.Substring(ul, f - ul);
                        if (!folders.Add(folderName))
                            continue;
                        yield return new HttpServerEndPoint(root + folderName, "[Implicit Folder] from " + LocationPrefix, HttpServerTools.StartedTime);
                    }
                }
            }
        }

        readonly SemiFrozenDictionary<String, ApiHttpEntry> Entries = new SemiFrozenDictionary<string, ApiHttpEntry>(StringComparer.Ordinal);

        /// <summary>
        /// Try to get information about an end point
        /// </summary>
        /// <param name="apiName"></param>
        /// <returns></returns>
        public IApiHttpServerEndPoint TryGet(String apiName)
            => Entries.TryGetValue(apiName, out var x) ? x : null;

        /// <summary>
        /// Enumerate all registered API's
        /// </summary>
        public IEnumerable<IApiHttpServerEndPoint> Apis => Entries.Values;

        public event Action<IApiHttpServerEndPoint> OnApiAdded;
        public event Action<IApiHttpServerEndPoint> OnApiRemoved;



        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor("API");

        #region Debug




        /// <summary>
        /// Get information about all registered API's
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Dev)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30, WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/{0}", "Api's", null, "IconTableApi")]
        public Task<TableData> ApiTable(TableDataRequest r, HttpServerRequest context)
            => TableDataTools.Get(context, r, 30000, Entries.Values.Select(x => SetApiInfo(x)).ToList());

        /// <summary>
        /// Get information about all registered data table's
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30, WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/{0}", "Data table", null, "IconTableTables")]
        public Task<TableData> ApiTableTable(TableDataRequest r, HttpServerRequest context) 
            => TableDataTools.Get(context, r, 30000, Tables.Keys.Select(x => new DataT(x)).ToList());




        /// <summary>
        /// Get information about an API
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="ret"></param>
        /// <param name="pi"></param>
        /// <param name="mi"></param>
        /// <param name="ri"></param>
        /// <param name="url"></param>
        /// <param name="retMime"></param>
        /// <returns></returns>
        public ApiInfoBase GetApiInfo(out Type arg, out Type ret, out MethodInfo mi, out ParameterInfo pi, out ParameterInfo ri, out String retMime, String url)
        {
            arg = null;
            ret = null;
            mi = null;
            pi = null;
            ri = null;
            retMime = null;
            if (!Entries.TryGetValue(url, out var e))
                return null;
            arg = e.ArgType;
            ret = e.RetType;
            mi = e.Mi;
            pi = e.Pi;
            ri = e.Ri;
            retMime = e.IsApi ? null : e.Mime;
            return SetApiInfo(e);
        }



        
        sealed class DataT
        {
            public DataT(ApiHttpEntry e)
            {
                Uri = e.Uri;
                var a = e.Auth;
                Auth = a == null ? null : String.Join(", ", a);
                Desc = e.Mi.XmlDoc().ToTitle();
            }

            /// <summary>
            /// The Uri of the end point
            /// </summary>
            [TableDataUrl(null, "*table.html?q=../{0}")]
            public String Uri;

            /// <summary>
            /// Auth information, null = open, empty = auth required or comma separted tokens that are required
            /// </summary>
            [TableDataTags("{^0}", null, "{0}", true)]
            public String Auth;

            /// <summary>
            /// API description (code comments)
            /// </summary>
            [AutoTranslate(false)]
            [AutoTranslateContext("This is the description an API endpoints that returns a data table")]
            public String Desc;

        }


        #endregion//Debug

        static ApiInfoBase SetApiInfo(ApiHttpEntry e, ApiInfoBase b = null)
        {
            if (b == null)
                b = new ApiInfoBase();
            b.Uri = e.Uri;
            var a = e.Auth;
            b.Auth = a == null ? null : String.Join(", ", a);
            b.Desc = e.Mi.XmlDoc().ToTitle();
            b.ClientCacheDuration = e.ClientCacheDuration;
            b.Mime = e.IsApi ? null : e.Mime.Split(';')[0];
            var rcd = e.RequestCacheDuration;
            b.RequestCacheDuration = rcd < 0 ? (-rcd) : rcd;
            b.PerSession = rcd < 0;
            b.CompPreference = e.CompPreference;
            b.Assembly = e.Mi.DeclaringType.Assembly.GetName().Name;
            b.Translated = e.NeedTranslation;
            return b;
        }

    }





}
