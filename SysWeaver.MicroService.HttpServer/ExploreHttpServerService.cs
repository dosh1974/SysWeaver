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
using System.Linq.Expressions;
using System.Reflection;

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
                HtmlTableDataExporter.Simple,
                MarkDownTableDataExporter.Instance,
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
            foreach (var x in CsvExtensions)
                if (manager.TryAddExtensionViewer(x, CsvViewer))
                    AddedViewers.Add(x);
        }

        /// <summary>
        /// The csv / tsv viewer (explore/csv.html, the file as a table in Sw.Table), {0} is the read url
        /// </summary>
        const String CsvViewer = "explore/csv.html?r={0}";

        /// <summary>
        /// The extensions shown by the <see cref="CsvViewer"/>
        /// </summary>
        static readonly String[] CsvExtensions = ["csv", "tsv"];

        /// <summary>
        /// The extension viewers this instance registered (removed again on dispose)
        /// </summary>
        readonly List<String> AddedViewers = new List<String>();

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
                throw UnknownTableApi(api);
            if (!context.Session.IsValid(ep.Auth))
                throw NotAllowedTableApi(api);
            if (s.RequireUser)
                if (context.Session.Auth == null)
                    throw NotAllowedTableApi(api);
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
            if (!TableExporters.TryGetValue(export.ExportAs ?? "", out var exporter))
                throw UnknownExporter(export.ExportAs);
            if (exporter.RequireUser)
                if (request.Session?.Auth == null)
                    throw ExporterNeedsUser(export.ExportAs);
            var opt = export.Options;
            if (opt != null)
            {
                var fn = opt.Filename;
                if (fn != null)
                    opt.Filename = PathExt.SafeFilename(fn);
            }
            return exporter.Export(export.Data, request, opt);
        }




        static readonly ConcurrentDictionary<Type, Func<IDeserializer, ReadOnlyMemory<Byte>, TableData>> TableCreators = new();
        static readonly ParameterExpression ExpParamDeser = Expression.Parameter(typeof(IDeserializer), "ser");
        static readonly ParameterExpression ExpParamData = Expression.Parameter(typeof(ReadOnlyMemory<Byte>), "data");
        static readonly MethodInfo MiSerCreate = typeof(IDeserializer).GetMethods().First(mi => mi.Name.FastEquals(nameof(IDeserializer.Create)) && mi.GetParameters().FirstOrDefault()?.ParameterType == typeof(ReadOnlyMemory<Byte>));
        static readonly MethodInfo MiToTableData = typeof(TableDataTools).GetMethod(nameof(TableDataTools.ToTableData));

        static Func<IDeserializer, ReadOnlyMemory<Byte>, TableData> GetTableCreator(Type type)
        {
            var tcs = TableCreators;
            if (tcs.TryGetValue(type, out var tc))
                return tc;
            var rt = type;
            if (rt.IsGenericType)
            {
                var bt = rt.GetGenericTypeDefinition();
                if ((bt == typeof(Task<>)) || (bt == typeof(ValueTask<>)))
                    rt = rt.GetGenericArguments()[0];
            }
            var serType = rt;
            var td = typeof(TableData);
            if (td.IsAssignableFrom(rt))
            {
                Expression prog = Expression.Call(ExpParamDeser, MiSerCreate.MakeGenericMethod(serType), ExpParamData);
                if (prog.Type != td)
                    prog = Expression.Convert(prog, td);
                tc = Expression.Lambda<Func<IDeserializer, ReadOnlyMemory<Byte>, TableData>>(prog, ExpParamDeser, ExpParamData).Compile();
                tcs[type] = tc;
                return tc;
            }
            else {
                for (; ; )
                {
                    if (rt.IsGenericType)
                    {
                        var ga = rt.GetGenericArguments();
                        if (ga.Length == 1)
                        {
                            if (typeof(TypedTableData<>).MakeGenericType(ga).IsAssignableFrom(serType))
                            {
                                Expression prog = Expression.Call(ExpParamDeser, MiSerCreate.MakeGenericMethod(serType), ExpParamData);
                                prog = Expression.Call(MiToTableData.MakeGenericMethod(ga[0]), prog);
                                tc = Expression.Lambda<Func<IDeserializer, ReadOnlyMemory<Byte>, TableData>>(prog, ExpParamDeser, ExpParamData).Compile();
                                tcs[type] = tc;
                                return tc;
                            }
                        }
                    }
                    rt = rt.BaseType;
                    if ((rt == null) || (rt == typeof(Object)))
                        throw new Exception("Don't know how to convert data of type \"" + serType.FullName + "\" to table data: \"" + typeof(TableData).FullName + "\"");
                }
            }
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
                throw UnknownTableApi(api);
            if (!request.Session.IsValid(ep.Auth))
                throw NotAllowedTableApi(api);
            if (!TableExporters.TryGetValue(export.ExportAs ?? "", out var exporter))
                throw UnknownExporter(export.ExportAs);
            if (exporter.RequireUser)
                if (request.Session.Auth == null)
                    throw ExporterNeedsUser(export.ExportAs);
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
            TableData tableData = GetTableCreator(ep.MethodInfo.ReturnType)(ser, output);
            return await exporter.Export(tableData, request, export.Options).ConfigureAwait(false);
        }


        #region Exports as background jobs

        /// <summary>
        /// Start an export of some table data as a background job (see <see cref="ExportTableData"/>): the result is kept on the server, so it survives a page reload.
        /// Follow the job with "Api/jobs/GetJob" / "Api/jobs/WaitJob" / the "sw.job" push message, get the file with "Api/jobs/GetJobResult" (the same file as <see cref="ExportTableData"/> returns),
        /// cancel it with "Api/jobs/CancelJob". The exporter and the access are checked before the job starts (an error is returned at once).
        /// </summary>
        /// <param name="export">The same parameters as <see cref="ExportTableData"/>, plus the optional job Key and Title</param>
        /// <param name="request">Automatically populated by the request handler, don't use</param>
        /// <returns>The job (IsNew false if a running export with the same Key was returned)</returns>
        /// <exception cref="HttpResponseException">400: unknown exporter, 403: not allowed (each with a reason for the user)</exception>
        /// <exception cref="HttpResponseException">429: too many running jobs</exception>
        [WebApi]
        public BackgroundJobInfo StartExportTableData(ExportTableDataRequest export, HttpServerRequest request)
        {
            if (!TableExporters.TryGetValue(export.ExportAs ?? "", out var exporter))
                throw UnknownExporter(export.ExportAs);
            if (exporter.RequireUser)
                if (request.Session?.Auth == null)
                    throw ExporterNeedsUser(export.ExportAs);
            var opt = SafeOptions(export.Options);
            var data = export.Data;
            return request.Server.StartJob(request, JobTitle(export.Title, opt), async ctx =>
            {
                //  The detached copy of the request (the original one ends when this api answered)
                return await exporter.Export(data, ctx.Request, opt).ConfigureAwait(false);
            }, export.Key, "export");
        }

        /// <summary>
        /// Start an export of a table data API as a background job (see <see cref="ExportTableApi"/>): the result is kept on the server, so it survives a page reload.
        /// Follow the job with "Api/jobs/GetJob" / "Api/jobs/WaitJob" / the "sw.job" push message, get the file with "Api/jobs/GetJobResult" (the same file as <see cref="ExportTableApi"/> returns),
        /// cancel it with "Api/jobs/CancelJob". The API, the exporter and the access are checked before the job starts (an error is returned at once).
        /// </summary>
        /// <param name="export">The same parameters as <see cref="ExportTableApi"/>, plus the optional job Key and Title</param>
        /// <param name="request">Automatically populated by the request handler, don't use</param>
        /// <returns>The job (IsNew false if a running export with the same Key was returned)</returns>
        /// <exception cref="HttpResponseException">404: unknown table api, 403: not allowed, 400: unknown exporter (each with a reason for the user)</exception>
        /// <exception cref="HttpResponseException">429: too many running jobs</exception>
        [WebApi]
        public BackgroundJobInfo StartExportTableApi(ExportTableApiRequest export, HttpServerRequest request)
        {
            var api = request.MakeAbsolute(export.Api).Substring(request.Prefix.Length);
            var am = ApiModule;
            var ep = am.TryGet(api);
            if (ep == null)
                throw UnknownTableApi(api);
            if (!request.Session.IsValid(ep.Auth))
                throw NotAllowedTableApi(api);
            if (!TableExporters.TryGetValue(export.ExportAs ?? "", out var exporter))
                throw UnknownExporter(export.ExportAs);
            if (exporter.RequireUser)
                if (request.Session.Auth == null)
                    throw ExporterNeedsUser(export.ExportAs);
            const int limit = 100000;
            var ser = am.DefaultSerializer;
            var tr = export.Req ?? new TableDataRequest();
            tr.Cc = 0;
            tr.LookAheadCount = 0;
            tr.MaxRowCount = limit;
            tr.Row = 0;
            var input = ser.Serialize(tr);
            var opt = SafeOptions(export.Options);
            return request.Server.StartJob(request, JobTitle(export.Title, opt), async ctx =>
            {
                //  The detached copy of the request (the original one ends when this api answered)
                var r = ctx.Request;
                r.Data["TableRowLimit"] = limit;
                ctx.Report(null, "Reading the rows");
                var output = await ep.InvokeAsync(r, input).ConfigureAwait(false);
                ctx.Cancel.ThrowIfCancellationRequested();
                TableData tableData = GetTableCreator(ep.MethodInfo.ReturnType)(ser, output);
                ctx.Report(0.5, "Making the file");
                return await exporter.Export(tableData, r, opt).ConfigureAwait(false);
            }, export.Key, "export");
        }

        /// <summary>
        /// The export options with a safe file name (no path).
        /// </summary>
        static TableDataExportOptions SafeOptions(TableDataExportOptions opt)
        {
            if (opt?.Filename != null)
                opt.Filename = PathExt.SafeFilename(opt.Filename);
            return opt;
        }

        /// <summary>
        /// The title of an export job.
        /// </summary>
        static String JobTitle(String title, TableDataExportOptions opt)
            => String.IsNullOrWhiteSpace(title) ? "Exporting " + (opt?.Filename ?? "Table") : title.Trim();

        /// <summary>
        /// 404 with a reason: the table api of an export doesn't exist (any more).
        /// </summary>
        static HttpResponseException UnknownTableApi(String api)
            => new HttpResponseException(404, "The table to export was not found in the service (" + (api ?? "").ToQuoted() + "): reload the page and try again");

        /// <summary>
        /// 403 with a reason: the caller may not read the table api of an export.
        /// </summary>
        static HttpResponseException NotAllowedTableApi(String api)
            => new HttpResponseException(403, "You are not allowed to read this table (" + (api ?? "").ToQuoted() + "), so it can't be exported");

        /// <summary>
        /// 403 with a reason: the exporter needs a signed in user.
        /// </summary>
        static HttpResponseException ExporterNeedsUser(String exporter)
            => new HttpResponseException(403, "Sign in to export to " + (exporter ?? "").ToQuoted());

        /// <summary>
        /// 400 with a reason: there is no exporter with that id (the reason names the valid ones).
        /// </summary>
        HttpResponseException UnknownExporter(String exporter)
            => new HttpResponseException(400, String.Concat((exporter ?? "").ToQuoted(), " is not an export format of this service, use one of: ", String.Join(", ", TableExporters.Keys.Order(StringComparer.Ordinal))));

        #endregion//Exports as background jobs


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
                using var bs = await UserStore.ReadFile(request, uri).ConfigureAwait(false);
                if (bs == null)
                    return null;
                var d = JsonSer.Create<BaseTableData>(bs.Memory.Span);
                return Tuple.Create(TableDataTools.GetStaticTableFn(d.Cols, d.Rows.Select(x => x.Values)), d.Cols, d.Title);
            }).ConfigureAwait(false);
            var data = filterFnCols.Item1(r);
            data.RefreshRate = 5 * 60 * 1000;
            data.HandleCc(r.Cc, filterFnCols.Item2, filterFnCols.Item3);
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
            foreach (var x in AddedViewers)
                manager.TryRemoveExtensionViewer(x, out var _);
            AddedViewers.Clear();
            manager.Unregister(Mod);
        }


        readonly ITableDataExporter[] Eporters; 
        

        public IReadOnlyList<ITableDataExporter> TableDataExporters => Eporters;


        #region Tables


        /// <summary>
        /// Get trace log
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.OpsDev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiCompression("br:Balanced, deflate:Balanced, gzip:Balanced")]
        [WebMenuTable(null, "Monitoring/{0}", "Recent log entries", null, "IconTableLog", 1)]
        public TableData LogTable(TableDataRequest r) => TableDataTools.Get(r, 1000, Manager.Messages);


        /// <summary>
        /// Read the content of one of the available text files
        /// </summary>
        /// <param name="name"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebApiRawText]
        [WebApiClientCache(2)]
        [WebApiRequestCache(1)]
        public Task<ReadOnlyMemory<Byte>> ReadTextFile(String name, HttpServerRequest context)
            => Manager.ReadTextFile(name, context);

        #endregion//Tables


    }




}
