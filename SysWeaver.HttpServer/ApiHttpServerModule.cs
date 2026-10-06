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
using System.Diagnostics.CodeAnalysis;

namespace SysWeaver.Net
{

    /// <summary>
    /// The Web API engine: an <see cref="IHttpServerModule"/> that exposes public instance methods marked with
    /// <see cref="WebApiAttribute"/> as HTTP end points (one <see cref="ApiHttpEntry"/> per method).
    /// </summary>
    /// <remarks>
    /// URLs are composed as <c>[Root]/[root argument]/[type WebApiUrl]/[method url or name]</c> (see <see cref="AddMethod"/>).
    /// Typically created and populated by the API micro service, which calls <see cref="AddObject"/> for every local service
    /// instance and wires <see cref="IApiAuditService"/> implementations to the audit events.
    /// Lookups are lock-free; adding is serialized internally, so registration is thread safe.
    /// Only the exact URL is matched (no prefix matching) and requests using <see cref="HttpServerMethods.Other"/> are ignored.
    /// </remarks>
    public sealed class ApiHttpServerModule : IHttpServerModule, IPerfMonitored
    {

        /// <summary>
        /// Resolve a comma separated list of serializer names, always making sure that <paramref name="def"/> is included (last if not listed).
        /// Unknown names are silently skipped.
        /// </summary>
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

        /// <summary>
        /// Create a new, empty API module.
        /// </summary>
        /// <param name="p">Module configuration (root, default auth, compression, serializers, performance monitoring).</param>
        /// <exception cref="NullReferenceException">No serializers are registered at all, or <paramref name="p"/> is null (the parameters are dereferenced directly in a few places, so the default value does not currently work).</exception>
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
        /// <summary>
        /// The serializer used when the client doesn't specify an acceptable format (from <see cref="ApiHttpServerModuleParams.DefaultSerializer"/>, falls back to json).
        /// </summary>
        public readonly ISerializerType DefaultSerializer;

        /// <inheritdoc/>
        public override string ToString() => String.Concat(
            nameof(Root), ": ", Root.ToQuoted(), ", ",
            nameof(Entries), ": ", Entries.Count);

        readonly String Root;
        readonly String Auth;
        readonly String CachedCompression;
        readonly String Compression;


        /// <summary>
        /// Register all public instance methods (including inherited ones) of an object that have a <see cref="WebApiAttribute"/>
        /// (directly or through an implemented interface).
        /// </summary>
        /// <param name="o">The object instance that will be invoked, must not be null.</param>
        /// <param name="root">Optional extra path segment inserted after the module root.</param>
        /// <returns>True if at least one method had a <see cref="WebApiAttribute"/> (even if it was excluded by <see cref="WebApiOptionalAttribute"/> or its URL already existed).</returns>
        /// <exception cref="Exception">A <see cref="WebApiOptionalAttribute"/> refers to a missing or non-boolean member.</exception>
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

        /// <summary>
        /// Unregister all API methods of an object, the reverse of <see cref="AddObject"/>.
        /// </summary>
        /// <param name="o">The object that was previously added.</param>
        /// <param name="root">The same root that was used when adding.</param>
        /// <returns>True if the type has any <see cref="WebApiAttribute"/> methods (not whether anything was actually removed).</returns>
        /// <remarks>Entries are removed by URL only, so an entry registered at the same URL by another object would also be removed.</remarks>
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

        static String GetMethodSignature(MethodInfo m)
            => String.Join('\n', m.DeclaringType.FullName, m);

        /// <summary>
        /// Register a single method as an API end point.
        /// </summary>
        /// <param name="o">The instance to invoke the method on.</param>
        /// <param name="method">The method to expose.</param>
        /// <param name="url">The method part of the URL, "{0}" is replaced with the method name; null to use the method name.</param>
        /// <param name="root">Optional extra path segment inserted after the module root.</param>
        /// <remarks>
        /// If the method has a <see cref="WebApiOptionalAttribute"/>, the named boolean field, property or parameterless method on <paramref name="o"/>
        /// is evaluated once now; when false the method is not added.
        /// If the declaring type has a <see cref="WebApiUrlAttribute"/> its URL ("{0}" replaced with the type name) is inserted before the method part.
        /// If an end point with the resulting URL already exists, the call is silently ignored (first registration wins).
        /// </remarks>
        /// <exception cref="Exception">The <see cref="WebApiOptionalAttribute"/> member is missing or isn't boolean.</exception>
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

        /// <summary>
        /// Unregister a single API method (raises <see cref="OnApiRemoved"/> if it was registered).
        /// </summary>
        /// <param name="o">The instance (currently unused, the entry is identified by URL).</param>
        /// <param name="method">The method that was added.</param>
        /// <param name="url">The same url that was used when adding.</param>
        /// <param name="root">The same root that was used when adding.</param>
        public void RemoveMethod(Object o, MethodInfo method, String url = null, String root = null)
        {
            var baseUrl = GetBaseUrl(method, url, root);
            if (Entries.TryRemove(baseUrl, out var ep))
            {
                Methods.TryRemove(GetMethodSignature(method), out var _);
                try
                {
                    InternalRemoveEndPoint(ep);
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
                        typeUrl = typeUrl.TrimEnd('/').Replace("{0}", t.Name);
                }
            }
            var mn = method.Name;
            if (url != null)
                url = url.Replace("{0}", mn);
            var baseUrl = HttpServerTools.CleanupPaths(HttpServerTools.CombinePaths(Root, root, typeUrl, url ?? mn));
            return baseUrl;
        }

        /// <summary>
        /// Signature of method invoked before an audited API is invoked.
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
        /// Invoked before an audited API is invoked.
        /// The value has been passed through the API's <see cref="WebApiAuditFilterParamsAttribute"/> filter, if any
        /// (if the filter throws, the unfiltered value is passed).
        /// </summary>
        /// <remarks>Handlers run synchronously on the request path; an exception thrown by a handler fails the API call.</remarks>
        public event AuditBeginDel OnAuditBegin;

        /// <summary>
        /// Invoked after an audited API is invoked (if no exception in thrown).
        /// The value has been passed through the API's <see cref="WebApiAuditFilterReturnAttribute"/> filter, if any
        /// (if the filter throws, the unfiltered value is passed).
        /// </summary>
        /// <remarks>Handlers run synchronously on the request path; an exception thrown by a handler fails the API call.</remarks>
        public event AuditEndDel OnAuditEnd;

        /// <summary>
        /// Invoked if an audited API throws an exception.
        /// </summary>
        public event AuditExceptionDel OnAuditException;

        /// <summary>
        /// Applies the entry's audit parameter filter (exceptions in the filter are swallowed) and raises <see cref="OnAuditBegin"/>.
        /// </summary>
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

        /// <summary>
        /// Applies the entry's audit return filter (exceptions in the filter are swallowed) and raises <see cref="OnAuditEnd"/>.
        /// </summary>
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



        /// <summary>
        /// True if the API takes a <see cref="TableDataRequest"/> and returns a <see cref="TableData"/> or <see cref="TypedTableData{T}"/> (used to populate the data table list).
        /// </summary>
        static bool IsDataTable(Type arg, Type ret)
        {
            if (!typeof(TableDataRequest).IsAssignableFrom(arg))
                return false;
            while (ret != typeof(Object))
            {
                if (typeof(TableData).IsAssignableFrom(ret))
                    return true;
                if (ret.IsGenericType && (ret.GetGenericTypeDefinition() == typeof(TypedTableData<>)))
                    return true;
                ret = ret.BaseType;
            }
            return false;
        }

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
                Methods[GetMethodSignature(method)] = url;
                if (IsDataTable(e.ArgType, e.RetType))
                    Tables[e] = Interlocked.Increment(ref TableId);
                OnApiAdded?.Invoke(e);
            }
        }

        void InternalRemoveEndPoint(ApiHttpEntry e)
        {
            Tables.TryRemove(e, out var _);
            OnApiRemoved?.Invoke(e);
        }

        int TableId;

        readonly ConcurrentDictionary<ApiHttpEntry, int> Tables = new();


        /// <summary>
        /// Get the API entry whose URL exactly matches <see cref="HttpServerRequest.LocalUrl"/> (ordinal, case sensitive).
        /// </summary>
        /// <param name="context">The request.</param>
        /// <returns>The <see cref="ApiHttpEntry"/>, or null if no API matches or the HTTP method is <see cref="HttpServerMethods.Other"/>.</returns>
        /// <remarks>Authorization is not checked here, it's performed by the server pipeline using the entry's <see cref="IHttpServerEndPoint.Auth"/>.</remarks>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            if (!Entries.TryGetValue(context.LocalUrl, out var e))
                return null;
            return context.HttpMethod == HttpServerMethods.Other ? null : e;
        }

        const String LocationPrefix = "[Api] ";


        /// <inheritdoc/>
        /// <remarks>
        /// With a <paramref name="root"/>, only direct children are returned; deeper APIs are represented by an implicit folder end point
        /// (one per distinct sub folder).
        /// </remarks>
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
                var lwt = HttpServerTools.StartedTime;
                var etag = HttpServerTools.StartedETag;
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
                        yield return new HttpServerEndPoint(root + folderName, "[Implicit Folder] from " + LocationPrefix, lwt, etag);
                    }
                }
            }
        }

        /// <summary>
        /// URL -> entry.
        /// </summary>
        readonly SemiFrozenDictionary<String, ApiHttpEntry> Entries = new SemiFrozenDictionary<string, ApiHttpEntry>(StringComparer.Ordinal);
        /// <summary>
        /// Method signature (declaring type + method) -> URL.
        /// </summary>
        readonly SemiFrozenDictionary<String, String> Methods = new SemiFrozenDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Get the URL that a method was registered at.
        /// </summary>
        /// <param name="m">The method (matched by declaring type full name and method signature).</param>
        /// <param name="url">The local URL of the API (no leading slash) if found.</param>
        /// <returns>True if the method is registered as an API.</returns>
        public bool TryGetApi(MethodInfo m, out string url)
            => Methods.TryGetValue(GetMethodSignature(m), out url);

        /// <summary>
        /// Get a registered API end point by its local URL.
        /// </summary>
        /// <param name="apiName">The exact local URL of the API (ordinal, case sensitive, no leading slash).</param>
        /// <returns>The end point or null if not found.</returns>
        public IApiHttpServerEndPoint TryGet(String apiName)
            => Entries.TryGetValue(apiName, out var x) ? x : null;

        /// <summary>
        /// Enumerate all registered API's.
        /// </summary>
        public IEnumerable<IApiHttpServerEndPoint> Apis => Entries.Values;

        /// <summary>
        /// Raised (while holding the registration lock) when an API end point has been added.
        /// </summary>
        public event Action<IApiHttpServerEndPoint> OnApiAdded;

        /// <summary>
        /// Raised when an API end point has been removed.
        /// </summary>
        public event Action<IApiHttpServerEndPoint> OnApiRemoved;



        /// <summary>
        /// Performance monitor shared by all API entries of this module (enabled by <see cref="ApiHttpServerModuleParams.PerMon"/>).
        /// </summary>
        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor("API");

        #region Debug




        /// <summary>
        /// Get information about all registered API's.
        /// </summary>
        /// <param name="r">Table request parameters (paging, sorting, filtering).</param>
        /// <param name="context">The request.</param>
        /// <returns>A table with one <see cref="ApiInfoBase"/> row per API.</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Dev)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30, WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/{0}", "Api's", null, "IconTableApi")]
        public Task<TableData> ApiTable(TableDataRequest r, HttpServerRequest context)
            => TableDataTools.Get(context, r, 30000, Entries.Values.Select(x => SetApiInfo(x)).ToList());

        /// <summary>
        /// Get information about all registered data table API's (API's taking a <see cref="TableDataRequest"/> and returning table data).
        /// </summary>
        /// <param name="r">Table request parameters (paging, sorting, filtering).</param>
        /// <param name="context">The request.</param>
        /// <returns>A table with one row per data table API.</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30, WebApiCaches.Globally)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/{0}", "Data tables", "All registered data tables end points", "IconTableTables")]
        public Task<TableData> ApiTableTable(TableDataRequest r, HttpServerRequest context) 
            => TableDataTools.Get(context, r, 30000, Tables.Keys.Select(x => new DataT(x)).ToList());




        /// <summary>
        /// Get information about an API.
        /// </summary>
        /// <param name="arg">The argument type, null if the API has no argument.</param>
        /// <param name="ret">The return type, null if the API returns nothing.</param>
        /// <param name="mi">The method that implements the API.</param>
        /// <param name="pi">The argument parameter, null if the API has no argument.</param>
        /// <param name="ri">The return parameter.</param>
        /// <param name="retMime">The mime type of raw (non serialized) results, null for serialized results.</param>
        /// <param name="url">The exact local URL of the API.</param>
        /// <returns>A new <see cref="ApiInfoBase"/> describing the API, or null (and all out values null) if not found.</returns>
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



        
        /// <summary>
        /// Row type for <see cref="ApiTableTable"/>.
        /// </summary>
        sealed class DataT
        {
            public DataT(ApiHttpEntry e)
            {
                Uri = e.Uri;
                var a = e.Auth;
                Auth = a == null ? null : String.Join(", ", a);
                Desc = e.Mi.XmlDoc().ToTitle();
                var r = e.RetType;
                if (r.IsGenericType && (r.GetGenericTypeDefinition() == typeof(TypedTableData<>)))
                {
                    Type = r.GetGenericArguments()[0];
                }
            }

            /// <summary>
            /// The Uri of the end point
            /// </summary>
            [TableDataUrl(null, "*table.html?q=../{0}")]
            public String Uri;

            /// <summary>
            /// Auth information: null = open, empty = any logged in user, else comma separated tokens where at least one is required.
            /// </summary>
            [TableDataTags("{^0}", null, "{0}", true)]
            public String Auth;

            /// <summary>
            /// The type of the data (if typed)
            /// </summary>
            public Type Type;

            /// <summary>
            /// API description (code comments)
            /// </summary>
            [AutoTranslate(false)]
            [AutoTranslateContext("This is the description an API endpoints that returns a data table")]
            [TableDataText]
            public String Desc;


        }


        #endregion//Debug

        /// <summary>
        /// Populate (or create) an <see cref="ApiInfoBase"/> from an entry; negative request cache durations mean per session caching.
        /// </summary>
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
