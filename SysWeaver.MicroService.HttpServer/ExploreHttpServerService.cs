using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.Net.ExploreModule;
using SysWeaver.Net.IconModule;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Serialization;
using System.Web;

namespace SysWeaver.MicroService
{


    [IsMicroService]
    [RequiredDep<IconHttpServerModule, StaticDataHttpServerModule>]
    public sealed class ExploreHttpServerService : IDisposable, IHaveTableDataExporters, IChatStoreLinkHandler
    {
        public override string ToString() => "[Service] " + Mod.ToString();

        public ExploreHttpServerService(ServiceManager manager, ExploreHttpServerServiceParams p = null)
        {
            Manager = manager;
            p = p ?? new ExploreHttpServerServiceParams();
            var icon = manager.Get<IconHttpServerModule>(ServiceInstanceTypes.LocalOnly);
            var staticData = manager.Get<StaticDataHttpServerModule>(ServiceInstanceTypes.LocalOnly);
            ApiModule = manager.TryGet<ApiHttpServerModule>(ServiceInstanceTypes.LocalOnly);
            var mod = new ExploreHttpServerModule(staticData, icon, p);
            Mod = mod;
            manager.Register(mod, p.InstanceName, false, typeof(ExploreHttpServerModuleParams));
            List<ITableDataExporter> exps = new List<ITableDataExporter>()
            {
                CsvTableDataExporter.Comma,
                CsvTableDataExporter.Tab,
                CsvTableDataExporter.SemiColon,
            };
            var ser = SerManager.Get("json");
            if (ser != null)
            {
                JsonSer = ser;
                exps.Add(JsonTableDataExporter.Verbose);
                exps.Add(JsonTableDataExporter.Compact);
                var us = manager.TryGet<IUserStorageService>();
                if (us != null)
                {
                    UserStore = us;
                    UserDataTableDataExporter[] uexps = new UserDataTableDataExporter[3];
                    StorageExporters = uexps;
                    for (int i = 0; i < 3; ++ i)
                    {
                        var ss = new UserDataTableDataExporter(us, ser, i);
                        uexps[i] = ss;
                        exps.Add(ss);
                    }
                }
            }
            Eporters = exps.ToArray();


            foreach (var x in manager.UniqueInstances)
                AddExporter(x as IHaveTableDataExporters);
            manager.OnServiceAdded += Manager_OnServiceAdded;
            manager.OnServiceRemoved += Manager_OnServiceRemoved;
        }

        #region Table exporters

        public async Task<string> HandleLink(IUserStorageService us, string url, UserStorageScopes scope, HttpServerRequest context)
        {
            var ss = StorageExporters;
            if (ss == null)
                return null;
            if (!url.FastStartsWith("explore/table.html"))
                return null;
            var s = ss[(int)scope];
            var pp = url.Substring(19);
            var qp = HttpServerTools.GetQueryParamsLowerKey(HttpUtility.ParseQueryString(pp));
            if (!qp.TryGetValue("q", out var api))
                return null;
            api = HttpServerTools.CleanupPaths("explore/" + api);
            var am = ApiModule;
            var ep = am.TryGet(api);
            if (ep == null)
                throw new Exception("Unknown API: " + api.ToQuoted());
            if (!context.Session.IsValid(ep.Auth))
                throw new Exception("Session is not authorized to acccess the API: " + api.ToQuoted());
            if (s.RequireUser)
                if (context.Session.Auth == null)
                    throw new Exception("Session is not authorized to acccess the API: " + api.ToQuoted());
            var ser = am.DefaultSerializer;
            const int limit = 100000;
            context.Data["TableRowLimit"] = limit;
            var r = new TableDataRequest
            {
                MaxRowCount = limit,
            };
            qp.TryGetValue("p", out r.Param);
            var input = ser.Serialize(r);
            var output = await ep.InvokeAsync(context, input).ConfigureAwait(false);
            var tableData = ser.Create<TableData>(output);
            var mem = await s.Export(tableData, context).ConfigureAwait(false);
            return mem.Name;
        }


        readonly IUserStorageService UserStore;
        readonly ISerializerType JsonSer;
        readonly UserDataTableDataExporter[] StorageExporters;

        ApiHttpServerModule ApiModule;

        WebMenuItem[] TableExporterMenu = [];
        WebMenuItem[] TableExporterMenuUser = [];


        /// <summary>
        /// Get the menu items for all registered table exporters
        /// </summary>
        /// <returns></returns>
        [WebApi]
        public WebMenuItem[] GetTableExporters(HttpServerRequest request) => request.Session.Auth == null ? TableExporterMenu : TableExporterMenuUser;


        /// <summary>
        /// Export some table data using a specified exporter
        /// </summary>
        /// <param name="export">Required paramterers</param>
        /// <param name="request"></param>
        /// <returns>A "file" with the exported data</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        public Task<MemoryFile> ExportTableData(ExportTableDataRequest export, HttpServerRequest request)
        {
            if (!TableExporters.TryGetValue(export.ExportAs, out var exporter))
                throw new Exception(export.ExportAs.ToQuoted() + " is not a reqistered data table exporter!");
            return exporter.Export(export.Data, request, export.Options);
        }

        /// <summary>
        /// Export some table data using a specified exporter
        /// </summary>
        /// <param name="export">Required paramterers</param>
        /// <param name="request"></param>
        /// <returns>A "file" with the exported data</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        public async Task<MemoryFile> ExportTableApi(ExportTableApiRequest export, HttpServerRequest request)
        {
            var api = request.MakeAbsolute(export.Api).Substring(request.Prefix.Length);
            var am = ApiModule;
            var ep = am.TryGet(api);
            if (ep == null)
                throw new Exception("Unknown API: " + api.ToQuoted());
            if (!request.Session.IsValid(ep.Auth))
                throw new Exception("Session is not authorized to acccess the API: " + api.ToQuoted());
            if (!TableExporters.TryGetValue(export.ExportAs, out var exporter))
                throw new Exception(export.ExportAs.ToQuoted() + " is not a reqistered data table exporter!");
            if (exporter.RequireUser)
                if (request.Session.Auth == null)
                    throw new Exception("Session is not authorized to acccess the API: " + api.ToQuoted());
            const int limit = 100000;
            request.Data["TableRowLimit"] = limit;
            var ser = am.DefaultSerializer;
            var r = export.Req;
            r.Cc = 0;
            r.LookAheadCount = 0;
            r.MaxRowCount = limit;
            r.Row = 0;
            var input = ser.Serialize(r);
            var output = await ep.InvokeAsync(request, input).ConfigureAwait(false);
            var tableData = ser.Create<TableData>(output);
            return await exporter.Export(tableData, request, export.Options).ConfigureAwait(false);
        }


        /// <summary>
        /// Used internally, make a table request against a stored file
        /// </summary>
        /// <param name="r"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(60)]
        public async Task<TableData> UserStoreTable(TableDataRequest r, HttpServerRequest request)
        {
            var uri = r.Param;
            var filterFnCols = await UserStoreTables.GetOrUpdateAsync(uri, async _uri =>
            {
                var bs = await UserStore.ReadFile(request, uri).ConfigureAwait(false);
                if (bs == null)
                    return null;
                var dd = bs ?? ReadOnlyMemory<byte>.Empty;
                var d = JsonSer.Create<BaseTableData>(dd.Span);
                return Tuple.Create(TableDataTools.GetStaticTableFn(d.Cols, d.Rows.Select(x => x.Values)), d.Cols, d.Title);
            }).ConfigureAwait(false);
            var cc = EnvInfo.Cc;
            var data = filterFnCols.Item1(r);
            data.RefreshRate = 5 * 60 * 1000;
            data.Cc = cc;
            var isNew = r.Cc != cc;
            data.Cols = isNew ? filterFnCols.Item2 : null;
            data.Title= isNew ? filterFnCols.Item3 : null;
            return data;
        }


        readonly FastMemCache<String, Tuple<Func<TableDataRequest, TableData>, TableDataColumn[], String>> UserStoreTables = new (TimeSpan.FromMinutes(1), StringComparer.Ordinal);





        void UpdateMenu()
        {
            List<Tuple<double, WebMenuItem>> items = new List<Tuple<double, WebMenuItem>>();
            List<Tuple<double, WebMenuItem>> userItems = new List<Tuple<double, WebMenuItem>>();
            var t = TableExporters;
            lock (t)
            {
                foreach (var x in t)
                {
                    var v = x.Value;
                    var item = Tuple.Create(v.Order, new WebMenuItem
                    {
                        Id = v.Name,
                        Name = v.Name,
                        IconClass = v.Icon,
                        Type = WebMenuItemTypes.Js,
                        Title = v.Desc,
                    });
                    userItems.Add(item);
                    if (!v.RequireUser)
                        items.Add(item);
                }
                TableExporterMenu = items.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
                TableExporterMenuUser = userItems.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
            }
        }


        void Manager_OnServiceAdded(object instance, ServiceInfo info)
        {
            var api = instance as ApiHttpServerModule;
            if (api != null)
                ApiModule = api;
            AddExporter(instance as IHaveTableDataExporters);
        }

        void Manager_OnServiceRemoved(object instance, ServiceInfo info)
        {
            RemoveExporter(instance as IHaveTableDataExporters);
        }

        public bool AddExporter(ITableDataExporter exp)
        {
            if (!TableExporters.TryAdd(exp.Name, exp))
                return false;
            UpdateMenu();
            return true;
        }

        public bool RemoveExporter(ITableDataExporter exp)
        { 
            if (!TableExporters.TryRemove(exp.Name, out var _))
                return false;
            UpdateMenu();
            return true;
        }


        public int AddExporter(IHaveTableDataExporters exp)
        {
            if (exp == null)
                return 0;
            var t = exp.TableDataExporters;
            if (t == null)
                return 0;
            int c = 0;
            foreach (var x in t)
                if (x != null)
                    if (AddExporter(x))
                        ++c;
            return c;
        }

        public int RemoveExporter(IHaveTableDataExporters exp)
        {
            if (exp == null)
                return 0;
            var t = exp.TableDataExporters;
            if (t == null)
                return 0;
            int c = 0;
            foreach (var x in t)
                if (x != null)
                    if (RemoveExporter(x))
                        ++c;
            return c;
        }

        readonly ConcurrentDictionary<String, ITableDataExporter> TableExporters = new ConcurrentDictionary<string, ITableDataExporter>(StringComparer.Ordinal);


        #endregion//Table exporters


        readonly ExploreHttpServerModule Mod;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            var manager = Manager;
            manager.OnServiceRemoved -= Manager_OnServiceRemoved;
            manager.OnServiceAdded -= Manager_OnServiceAdded;
            manager.Unregister(Mod);
        }


        readonly ITableDataExporter[] Eporters; 
        

        public IReadOnlyList<ITableDataExporter> TableDataExporters => Eporters;


        #region Tables


        /// <summary>
        /// Get trace log
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.OpsDev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Balanced, deflate:Balanced, gzip:Balanced")]
        [WebMenuTable(null, "Debug/{0}", "Log", null, "IconTableLog")]
        public TableData LogTable(TableDataRequest r) => TableDataTools.Get(r, 1000, Manager.Messages);

        #endregion//Tables

    }




}
