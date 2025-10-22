using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Net.IconModule;
using SysWeaver.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

[assembly: SysWeaver.ResourceOrder(-100)]

namespace SysWeaver.Net.ExploreModule
{


    [WebMenuPath(null, "Debug", "Debug", "Debug functionality, not available to most users", null, 100)]
    [WebMenuPath(null, "Debug/Data", "Data", "Misc static data", null, 100)]
    [WebMenuEmbedded(null, "Debug/Explore", "Explore", "explore", "Explore the HTTP server end-points", "IconExplore", -10, "Debug,Dev,Ops")]
    [WebMenuEmbedded(null, "Debug/LogoDesign", "Logo design", "explore/logo.html", "Open logo seed visualizer, the seed can be copied into an AppInfo to change the generated logo", "IconLogo", -3, "Debug")]
    [WebMenuEmbedded(null, "Debug/Icons", "Icons", "explore/icons.html", "Shows all available icons ('icons/*.svg' files)", "IconImage", -2, "Debug,Dev")]
    [WebApiUrl("debug/explore")]
    public sealed class ExploreHttpServerModule : IHttpServerModule, IHttpRequestHandler, IPerfMonitored
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(ExploreHttpServerModule));

        const String Name = "explore";

        public ExploreHttpServerModule(StaticDataHttpServerModule dataModule, IconHttpServerModule iconModule, ExploreHttpServerModuleParams p = null)
        {
            var pp = p ?? new ExploreHttpServerModuleParams();
            PerfMon.Enabled = p.PerMon;
            var auth = pp.Auth;
            var t = GetType();
            var asm = t.Assembly;
            var bn = t.Namespace + ".data.";
            var mem = asm.GetUncompressedResourceData(bn + "index.html");
            var text = Encoding.UTF8.GetString(mem.Span);

            Auth = SysWeaver.Auth.Authorization.GetRequiredTokens(auth);
            ClientCacheDuration = pp.ClientCacheDuration ?? dataModule.ClientCacheDuration;
            Compression = HttpCompressionPriority.GetSupportedEncoders(pp.Compression);

            FolderIcon = WebUtility.HtmlEncode(iconModule.FolderName);
            Main = new TextTemplate(text, "$(", ")");
            ToJson = (SerManager.Get("json") as ITextSerializer) ?? throw new NullReferenceException();
            Icons = iconModule;
        }

        public override string ToString() => "Provides functionality to explore a web server, add \"" + Name + "\" at any level to explore the content";

        readonly ITextSerializer ToJson;
        readonly TextTemplate Main;
        readonly String FolderIcon;
        readonly IconHttpServerModule Icons;


        #region IHttpServerModule

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var l = context.LocalUrl;
            var key = Name;
            if (!l.EndsWith(key, StringComparison.Ordinal))
                return null;
            var kl = key.Length;
            var ll = l.Length - kl - 1;
            if ((ll >= 0) && (l[ll] != '/'))
                return null;
            return this;
        }
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;

        #endregion//IHttpServerModule

        #region IHttpRequestHandler



        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            using (PerfMon.Track(nameof(GetData)))
            {
                var l = request.LocalUrl;
                l = l.Substring(0, l.Length - Name.Length);
                var ll = l.Length;
                List<ExploreItem> items = new List<ExploreItem>();
                foreach (var x in request.Server.EnumEndPoints(l))
                {
                    if (x == null)
                        continue;
                    var a = x.Auth;
                    var rcd = x.RequestCacheDuration;
                    items.Add(new ExploreItem
                    {
                        Name = x.Uri.Substring(ll),
                        Method = x.Method,
                        Type = x.Type,
                        ClientCacheDuration = x.ClientCacheDuration,
                        RequestCacheDuration = rcd < 0 ? -rcd : rcd,
                        PerSession = rcd < 0,
                        CompPreference = x.CompPreference,
                        PreCompressed = x.PreCompressed,
                        Auth = a == null ? null : String.Join(',', a),
                        Location = x.Location,
                        Size = x.Size,
                        LastModified = x.LastModified,
                        Mime = x.Mime,
                    });
                }
                var basePath = UrlHelper.ParentFolderRef(l.Count(x => x == '/'));
                var icons = Icons;
                var baseUrl = request.Url;
                var split = request.QueryStringStart;
                baseUrl = baseUrl.Substring(0, (split > 0 ? (split - 1) : baseUrl.Length) - Name.Length);
                var data = new ExploreData
                {
                    FolderSuffix = "/" + Name,
                    FolderIcon = basePath + icons.FolderName,
                    VirtualFolderIcon = basePath + icons.VirtualFolderName,
                    ApiIcon = basePath + icons.ApiFolderName,
                    IconBase = basePath + icons.BasePath,
                    ExtIconBase = basePath + icons.ExtPrefix,
                    MimeIconBase = basePath + icons.MimePrefix,
                    Items = items.ToArray(),
                    LocalUrl = l,
                    BaseUrl = baseUrl,
                };
                var s = ToJson.ToString(data);
                request.SetResMime("text/html;charset=UTF8-8");
                var bn = basePath + Name + "/" + Name;
                Dictionary<String, String> vars = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                { "FOLDERICON", basePath + FolderIcon },
                { "STYLECSSFILE", basePath + "common/theme.css" },
                { "CSSFILE", bn + ".css" },
                { "JSFILE", bn + ".js" },
                { "COMMONJSFILE", basePath + "common/common.js" },
                { "TITLE", WebUtility.HtmlEncode(l.Length <= 0 ? "<ROOT>" : l) },
                { "DATA", s },
            };
                var txt = Main.Get(vars);
                return Encoding.UTF8.GetBytes(txt);
            }
        }

        public int ClientCacheDuration { get; private set; } = 1;

        public int RequestCacheDuration { get; private set; } = 15;

        public bool UseStream => false;

        public HttpCompressionPriority Compression { get; private set; }

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth { get; private set; }


        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return null;
        }

        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        #endregion//IHttpRequestHandler


        #region Tables

        /// <summary>
        /// Get information about all serializer formats known to the service
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Serializers", null, "IconTableSerializer")]
        public TableData SerializerTable(TableDataRequest r) => TableDataTools.Get(r, 30000, SerManager.All.Select(x => new SerData(x)));

        /// <summary>
        /// Get information about all compression formats known to the service
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCache(30)]
        [WebApiRequestCache(30)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Data/{0}", "Compression formats", null, "IconTableCompression")]
        public TableData CompressionTable(TableDataRequest r) => TableDataTools.Get(r, 30000, CompManager.All.Select(x => new CompData(x)));


        #endregion//Tables

        /// <summary>
        /// Get the current application seed
        /// </summary>
        /// <returns>The current application seed</returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public int GetAppSeed() => EnvInfo.AppSeed;


        /// <summary>
        /// Get all icons (icons/*.svg), used to visualize what icons we can use.
        /// </summary>
        /// <param name="r">Parameters for sorting and filtering</param>
        /// <param name="req"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Dev)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic(WebApiCaches.Globally)]
        public IconInfo[] GetIcons(TableDataRequest r, HttpServerRequest req)
        {
            var path = String.IsNullOrEmpty(r?.Param) ? "icons/" : r.Param;
            if (!path.FastEndsWith("/"))
                path += "/";
            var server = req.Server;
            var icons = TableDataTools.SortAndFilter(r, server.EnumEndPoints(path)
                .Where(
                    x => x.Mime.FastStartsWith("image/"))
                .Select(
                    x => new IconInfo(x))
                ).ToArray();
            return icons;
        }

        #region Data References

        /// <summary>
        /// Get table data from a table data reference
        /// </summary>
        /// <param name="request">The request paramaters, must contain a table data reference in the Params</param>
        /// <param name="context"></param>
        /// <returns>The data contained is the table ref</returns>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCache(5)]
        public TableData DataRefTable(TableDataRequest request, HttpServerRequest context)
        {
            var r = request.Param;
            if (String.IsNullOrEmpty(r))
                throw new Exception("Must specify a data table reference as the Param!");
            var td = context.GetTableData(r);
            if (td == null)
                throw new Exception("Unknown data table reference (expired?)");
            var data = td.Get();
            data = data.Filter(request);
            var cc = EnvInfo.Cc;
            var isNew = request.Cc != cc;
            var ret = new TableData
            {
                Cols = isNew ? data.Cols : null,
                Title = isNew ? data.Title : null,
                Rows = data.Rows,
                RowCount = data.Rows.LongLength,
                RefreshRate = td.TimeToLive * 900,
                Cc = cc,
            };
            return ret;
        }

        /// <summary>
        /// Get a link to a page that displays an interactive table, showing the content of a table data reference
        /// </summary>
        /// <param name="dataRefId">The table data reference to explore</param>
        /// <returns>A link to a page</returns>
        [WebApi]
        [WebApiClientCacheStatic]
        public String GetTableDataRefUrl(String dataRefId)
            => String.Concat("../explore/table.html?q=../Api/debug/explore/", nameof(DataRefTable), "&p=", Uri.EscapeDataString(dataRefId));

        
        sealed class DataRefRow
        {
            public DataRefRow(DataReference r, KeyValuePair<String, HttpSession> s)
            {
                Scope = r.Scope;
                Id = r.Id;
                TimeToLive = r.TimeToLive;
                Created = r.Created;
                Expires = r.Expires;
                UseCounter = r.UseCounter;
                Type = r.GetType().Name;
                var t = r as TableDataReference;
                var ss = s.Value;
                if (t != null)
                {
                    Show = ss == null ? r.Id : String.Join("@", r.Id, s.Key);
                    Columns = t.Get().Cols.Length;
                    Rows = t.Rows;
                }
                if (ss != null)
                {
                    SessionId = s.Key;
                    SessionUser = ss.Auth?.Username;
                }
            }
            /// <summary>
            /// Scope of the data reference
            /// </summary>
            public DataScopes Scope;

            /// <summary>
            /// Id of the data reference
            /// </summary>
            public String Id;

            /// <summary>
            /// Data life time (every time the data is requested it will be kept alive for this many seconds)
            /// </summary>
            public int TimeToLive;

            /// <summary>
            /// When the data expires
            /// </summary>
            public DateTime Created;

            /// <summary>
            /// When the data expires
            /// </summary>
            public DateTime Expires;

            /// <summary>
            /// Number of times this data was been used (after creation)
            /// </summary>
            public long UseCounter;

            /// <summary>
            /// Name of the data type
            /// </summary>
            public String Type;

            /// <summary>
            /// Table data may be shown
            /// </summary>
            [TableDataUrl("Show", "table.html?q=../Api/debug/explore/DataRefTable&p={0}", "Click to show the table data")]
            public String Show;

            /// <summary>
            /// Number of columns (if this is table)
            /// </summary>
            public int Columns;

            /// <summary>
            /// Number of rows (if this is table)
            /// </summary>
            public long Rows;

            /// <summary>
            /// The session that owns this data (if it's session data)
            /// </summary>
            public String SessionId;

            /// <summary>
            /// User name of the user logged in to the session (if it's session data)
            /// </summary>
            public String SessionUser;

        }

        /// <summary>
        /// A table with all data references.
        /// Data references is a way of working with tabular data witout actuall sending the data to clients (until finally requested).
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebApiClientCache(4000)]
        [WebMenuTable(null, HttpServerBase.MenuPath)]
        public TableData DataTableReferencesTable(TableDataRequest request, HttpServerRequest context)
            => TableDataTools.Get(request, 5000, context.Server.AllDataReferences.Select(x => new DataRefRow(x.Item1, x.Item2)));


        #endregion//Data References
    }

    public sealed class IconInfo
    {
        internal IconInfo(IHttpServerEndPoint e)
        {
            var loc = e.Uri;
            Url = loc;
            var ll = loc.LastIndexOf('/') + 1;
            loc = ll > 0 ? loc.Substring(ll, loc.Length - ll) : loc;
            Name = loc;
            Location = e.Location;
            LastModified = e.LastModified;
            Size = e.Size ?? -1;
        }
        public IconInfo()
        {
        }
        public String Name;
        public String Url;
        public String Location;
        public long Size;
        public DateTime LastModified;
    }

}
