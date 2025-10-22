

using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.AI;
using SysWeaver.Chart;
using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.Serialization;
using SysWeaver.Serialization.NewtonsoftJson;

namespace SysWeaver.MicroService
{

    [OpenAiToolPrefix("")]
    public sealed class ChartJsService : IHaveOpenAiTools, IDisposable, IChatStoreLinkHandler
    {
        public ChartJsService(ServiceManager manager, ChartJsParams p = null)
        {
            p = p ?? new ChartJsParams();
            ApiModule = manager.TryGet<ApiHttpServerModule>(ServiceInstanceTypes.LocalOnly);
            Manager = manager;
            foreach (var x in manager.UniqueInstances)
                AddExporter(x as IHaveChartExporters);
            var us = manager.TryGet<IUserStorageService>();
            if (us != null)
            {
                var s = SerManager.Get("json");
                if (s != null)
                {
                    JsonSer = s;
                    var exp = new UserStorageChartExporter[3];
                    StorageExporters = exp;
                    for (int i = 0; i < 3; ++i)
                    {
                        var ss = new UserStorageChartExporter(us, ChartSerialize, i);
                        exp[i] = ss;
                        AddExporter(ss);
                    }
                }
            }
            manager.OnServiceAdded += Manager_OnServiceAdded;
            manager.OnServiceRemoved += Manager_OnServiceRemoved;
        }

        readonly ISerializerType JsonSer;
        readonly UserStorageChartExporter[] StorageExporters;


        public async Task<string> HandleLink(IUserStorageService us, string url, UserStorageScopes scope, HttpServerRequest context)
        {
            var ss = StorageExporters;
            if (ss == null)
                return null;
            if (!url.FastStartsWith("chart/chart.html"))
                return null;
            var s = ss[(int)scope];
            var pp = url.Substring(17);
            var qp = HttpServerTools.GetQueryParamsLowerKey(HttpUtility.ParseQueryString(pp));
            if (!qp.TryGetValue("q", out var q))
                return null;
            q = HttpServerTools.CleanupPaths(context.Prefix + "chart/" + q);
            var data = await context.Server.InternalRead(context, q).ConfigureAwait(false);
            var ds = JsonSer.Create<ChartJsConfig>(data.Item1);
            var mem = await s.Export(ds, context).ConfigureAwait(false);
            return mem.Name;
        }


        readonly ApiHttpServerModule ApiModule;

        public void Dispose()
        {
            var manager = Manager;
            manager.OnServiceRemoved -= Manager_OnServiceRemoved;
            manager.OnServiceAdded -= Manager_OnServiceAdded;
        }

        readonly ServiceManager Manager;

        static readonly JsonSerializerSettings SerOptions = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None,
            ContractResolver = MemberResolver.Instance,
            NullValueHandling = NullValueHandling.Ignore,
        };


        /// <summary>
        /// Used to serialize a chart
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static ReadOnlyMemory<Byte> ChartSerialize(ChartJsConfig c)
        {
            var st = JsonConvert.SerializeObject(c, SerOptions);
            return st.ToUTF8();
        }

        /// <summary>
        /// Get an url to an advanced chart.
        /// </summary>
        /// <param name="chart">The chart in the format of a Graph.js configuration</param>
        /// <param name="request"></param>
        /// <returns>An url to a html page containing the generated chart</returns>
        [OpenAiUse]
        [OpenAiTool("📊")]
        String BuildAdvancedChart(ChartJsConfig chart, HttpServerRequest request)
        {
            var c = ChartSerialize(chart);
            var uri = request.OpenAiAddMessageFile(HttpServerTools.JsonMime, c, chart.Title ?? "Chart");
            uri = "../chart/chart.html?q=../chat/" + uri;
            return uri;
        }

        /// <summary>
        /// Use this function to display an advanced chart.
        /// </summary>
        /// <param name="chart">The data required to generate the chart</param>
        /// <param name="request"></param>
        /// <returns>True if successful</returns>
        [OpenAiUse]
        [OpenAiTool("📊")]
        bool DisplayAdvancedChart(ChartJsConfig chart, HttpServerRequest request)
        {
            var url = BuildAdvancedChart(chart, request);
            if (url == null)
                return false;
            var c = request.Properties[OpenAiToolExt.RequestAiToolContext] as IOpenAiToolContext;
            if (c == null)
                return false;
            c.AddLink(url);
            return true;

        }

        /// <summary>
        /// Get an url (html) to a customizable chart, with the specified paramateres.
        /// </summary>
        /// <param name="chart">The data required to generate the chart</param>
        /// <param name="request"></param>
        /// <returns>An url to a html page containing the generated chart</returns>
        [OpenAiUse]
        [OpenAiTool("📊")]
        String BuildChart(Chart chart, HttpServerRequest request)
        {
            var labels = chart.Labels;
            var llength = labels.Length;
            var seenLabels = new HashSet<String>(StringComparer.Ordinal);
            for (int i = 0; i < llength; ++i)
            {
                var labelName = labels[i]?.Trim();
                if (String.IsNullOrEmpty(labelName))
                    throw new Exception("Label may not be null or empty at index " + i);
                if (!seenLabels.Add(labelName.FastToLower()))
                    throw new Exception("Label " + labelName.ToQuoted() + " is used more than one, second occurence was at index " + i);
            }
            var s = chart.Series;
            var l = s.Length;


            var seenDataSet = new HashSet<String>(StringComparer.Ordinal);
            var d = new ChartJsDataSet[l];
            for (int i = 0; i < l; ++i)
            {
                var ss = s[i];
                var setName = ss.Name?.Trim();
                if (String.IsNullOrEmpty(setName))
                    throw new Exception("Serie (data set) name may not be null or empty for series at index" + i);
                if (!seenDataSet.Add(setName.FastToLower()))
                    throw new Exception("Serie (data set) with name " + setName.ToQuoted() + " is used more than one, second occurence was for series at index " + i);

                var dd = new ChartJsDataSet
                {
                    label = setName,
                };
                d[i] = dd;
                var col = ss.Color?.Trim();
                if (String.IsNullOrEmpty(col))
                    col = "#99a";
                var cols = ss.Colors;
                var fillOpacity = ss.FillOpacity;
                if ((cols != null) && (cols.Length > 0))
                {
                    Array.Resize(ref cols, llength);
                    for (int j = 0; j < llength; ++j)
                    {
                        var fc = cols[j]?.Trim();
                        cols[j] = String.IsNullOrEmpty(fc) ? col : fc;
                    }
                    if (fillOpacity >= 1)
                        dd.backgroundColor = cols;
                    else
                        dd.backgroundColor = cols.Select(col => HtmlColors.MakeTransparent(col, fillOpacity)).ToArray();
                    dd.borderColor = cols;
                } else
                {
                    if (fillOpacity >= 1)
                        dd.backgroundColor = [col];
                    else
                        dd.backgroundColor = [HtmlColors.MakeTransparent(col, fillOpacity)];
                    dd.borderColor = [col];
                }
                dd.borderWidth = ss.BorderWidth;
                //var p = new ChartJsDataPoint[llength];
                var p = new double[llength];
                dd.data = p;
                var sd = ss.Values ?? Array.Empty<double>();
                var sdl = sd.Length;
                for (int j = 0; j < llength; ++j)
                {
                    p[j] = j < sdl ? sd[j] : 0;
                    /*
                    p[j] = new ChartJsDataPoint
                    {
                        x = labels[j],
                        y = j < sdl ? sd[j] : 0,
                    };*/
                }
            }
            var cc = new ChartJsConfig
            {
                type = Chart.TypeNames[(int)chart.Type],
                options = new ChartJsOptions
                {
                    maintainAspectRatio = false,
                    tension = chart.SmoothLines ? 1 : 0,
                },
                data = new ChartJsData
                {
                    labels = labels,
                    datasets = d,
                },
                RefreshRate = 24 * 60 * 60 * 1000,
            };
            if (chart.Stack)
            {
                cc.options.scales = cc.options.scales ?? new ChartJsScalesOptions();
                cc.options.scales.y = cc.options.scales.y ?? new ChartJsScaleOptions();
                cc.options.scales.y.stacked = true;
            }
            if (chart.Horizontal)
                cc.options.indexAxis = "y";

            var sort = chart.SortBySeries;
            if (!String.IsNullOrEmpty(sort))
            {
                bool desc = sort[0] == '-';
                if (desc)
                    sort = sort.Substring(1);
                ChartJsDataSet sortBy = null;
                var i = l;
                while (i > 0)
                {
                    --i;
                    sortBy = d[i];
                    if (sortBy.label == sort)
                        break;
                }
                int[] indices = new int[llength];
                for (i = 0; i < llength; ++i)
                    indices[i] = i;
                var vals = sortBy.data;
                if (desc)
                    Array.Sort(indices, (a, b) => vals[b].CompareTo(vals[a]));
                else
                    Array.Sort(indices, (a, b) => vals[a].CompareTo(vals[b]));
                /*
                                if (desc)
                                    Array.Sort(indices, (a, b) => vals[b].y.CompareTo(vals[a].y));
                                else
                                    Array.Sort(indices, (a, b) => vals[a].y.CompareTo(vals[b].y));
                */
                cc.data.labels = cc.data.labels.Reordered(indices);
                for (i = 0; i < l; ++i)
                {
                    var ds = cc.data.datasets[i];
                    ds.data = ds.data.Reordered(indices);
                    if (ds.backgroundColor?.Length == llength)
                        ds.backgroundColor = ds.backgroundColor.Reordered(indices);
                    if (ds.borderColor?.Length == llength)
                        ds.borderColor = ds.borderColor.Reordered(indices);
                }
            }
            var title = chart.Title;
            if (!String.IsNullOrEmpty(title))
            {
                cc.options.plugins = cc.options.plugins ?? new ChartJsPlugins();
                cc.options.plugins.title = new ChartJsTitle
                {
                    text = title.Split('\n', StringSplitOptions.TrimEntries),
                    display = true,
                    fullSize = true,
                    font = new ChartJsFontOptions
                    {
                        weight = "bold",
                    },
                };
            }

            if (cc.options.scales == null)
                cc.options.scales = new ChartJsScalesOptions();
            cc.options.scales.r = new ChartJsScaleOptions
            {
                display = true,
                pointLabels = new ChartJsPointLabel
                {
                    display = true,
                    centerPointLabels = true,
                },
                ticks = new ChartJsTickOptions
                {
                    display = true,
                    backdropColor = "",
                }
            };

            cc.options.plugins = cc.options.plugins ?? new ChartJsPlugins();
            cc.options.plugins.legend = new ChartJsLegend
            {
                display = true,
            };
            var vtitle = chart.ValueTitle;
            if (!String.IsNullOrEmpty(vtitle))
            {
                cc.options.scales = cc.options.scales ?? new ChartJsScalesOptions();

                if (chart.Horizontal)
                {
                    cc.options.scales.x = cc.options.scales.x ?? new ChartJsScaleOptions();
                    cc.options.scales.x.title = new ChartJsTitle
                    {
                        display = true,
                        text = vtitle.Split('\n', StringSplitOptions.TrimEntries),
                    };

                }
                else
                {
                    cc.options.scales.y = cc.options.scales.y ?? new ChartJsScaleOptions();
                    cc.options.scales.y.title = new ChartJsTitle
                    {
                        display = true,
                        text = vtitle.Split('\n', StringSplitOptions.TrimEntries),
                    };
                }
            }

            var c = ChartSerialize(cc);
            var uri = request.OpenAiAddMessageFile(HttpServerTools.JsonMime, c, chart.Title ?? "Chart");
            uri = "../chart/chart.html?q=../chat/" + uri;
            return uri;
        }


        /// <summary>
        /// Use this function to display a chart.
        /// </summary>
        /// <param name="chart">The data required to generate the chart</param>
        /// <param name="request"></param>
        /// <returns>True if successful</returns>
        [OpenAiUse]
        [OpenAiTool("📊")]
        bool DisplayChart(Chart chart, HttpServerRequest request)
        {
            var url = BuildChart(chart, request);
            if (url == null)
                return false;
            var c = request.Properties[OpenAiToolExt.RequestAiToolContext] as IOpenAiToolContext;
            if (c == null)
                return false;
            c.AddLink(url);
            return true;

        }


        /// <summary>
        /// Returns chart data for peformance mesaurement.
        /// Returns in progress and max concurrency data.
        /// </summary>
        /// <returns></returns>
        [WebApiAuth(Roles.Debug)]
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        [WebMenuChart(null, "Debug/MethodGraph", "Method graph", "A graph showing all active methods calls and peak", null, 0, "Debug")]
        public ReadOnlyMemory<Byte> PerformanceChart()
        {
            var p = Manager.GetPerformanceEntries().ToList();
            p.Sort((a, b) =>
            {
                var i = String.Compare(a.System, b.System);
                if (i != 0)
                    return i;
                return String.Compare(a.Name, b.Name);
            });
            var c = p.Count;
            String[] labels = new string[c];
            double[] dataInp = new double[c];
            double[] maxC = new double[c];
            //            ChartJsDataPoint[] dataInp = new ChartJsDataPoint[c];
            //            ChartJsDataPoint[] maxC = new ChartJsDataPoint[c];
            String[] backCols = new string[c];
            String[] edgeCols = new string[c];
            for (int i = 0; i < c; ++i)
            {
                var x = p[i];
                var l = String.Join(" / ", x.System, x.Name);
                HashColors.GetRandom(out var hue, out var _sat, HashColors.SeedFromString(x.System));
                HashColors.GetRandom(out var _hue, out var sat, HashColors.SeedFromString(x.Name));
                var back = HashColors.GetWeb(hue, sat, 0.8, 0.1);
                var edge = HashColors.GetWeb(hue, sat, 0.8);
                backCols[i] = back;
                edgeCols[i] = edge;

                labels[i] = l;
                dataInp[i] = x.InProgress;
                maxC[i] = x.MaxConcurrency;
                /*                dataInp[i] = new ChartJsDataPoint
                                {
                                    x = l,
                                    y = x.InProgress,
                                };
                                maxC[i] = new ChartJsDataPoint
                                {
                                    x = l,
                                    y = x.MaxConcurrency,
                                };
                */
            }
            return ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                data = new ChartJsData
                {
                    labels = labels,
                    datasets = [
                        new ChartJsDataSet
                        {
                            label = "In progress",
                            data = dataInp,
                            backgroundColor = edgeCols,
                        },
                        new ChartJsDataSet
                        {
                            label = "Max concurrency",
                            data = maxC,
                            backgroundColor = backCols,
                            borderColor = edgeCols,
                            xAxisID = "x2",
                            borderWidth = 2,
                        },
                    ]
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        title = new ChartJsTitle
                        {
                            text = ["Monitored functions"],
                            display = true,
                            fullSize = true,
                            font = new ChartJsFontOptions
                            {
                                weight = "bold",
                            }
                        }
                    },
                    maintainAspectRatio = false,
                    scales = new ChartJsScalesOptions
                    {
                        x2 = new ChartJsScaleOptions
                        {
                            display = false,
                        }
                    }
                }
            });
        }





        /// <summary>
        /// Returns true if the TableChart api is available.
        /// </summary>
        [WebApi]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public bool TableChartAvailable() => ApiModule != null;


        static String GetColumnName(TableDataColumn col)
        {
            return col.Title;
        }


        sealed class GetValState
        {
            public readonly DateTime BaseTime;

            public GetValState(DateTime baseTime)
            {
                BaseTime = baseTime;
            }
        }

        delegate double GetValDel(Object o, GetValState s);

        sealed class ValTypes
        {
            public readonly int Precision;
            public readonly GetValDel GetVal;
            public readonly String Unit;

            public ValTypes(GetValDel getVal, int precision = 0, string unit = null)
            {
                Precision = precision;
                GetVal = getVal;
                Unit = unit;
            }
        }




        static double DefVal(Object o, GetValState s)
            => (Double)Convert.ChangeType(o, typeof(Double));

        static IReadOnlyDictionary<Type, ValTypes> GetDataConverters()
        {
            var d = new Dictionary<Type, ValTypes>()
            {

                { typeof(SByte), new ValTypes(DefVal) },
                { typeof(Int16), new ValTypes(DefVal) },
                { typeof(Int32), new ValTypes(DefVal) },
                { typeof(Int64), new ValTypes(DefVal) },
                { typeof(Byte), new ValTypes(DefVal) },
                { typeof(UInt16), new ValTypes(DefVal) },
                { typeof(UInt32), new ValTypes(DefVal) },
                { typeof(UInt64), new ValTypes(DefVal) },

                { typeof(Single), new ValTypes(DefVal, -2) },
                { typeof(Double), new ValTypes(DefVal, -2) },
                { typeof(Decimal), new ValTypes(DefVal, -2) },

                { typeof(Boolean), new ValTypes((o, s) => (Boolean)o ? 0.0 : 1.0) },
                { typeof(TimeSpan), new ValTypes((o, s) =>
                {
                    var valStr = o as String;
                    if (String.IsNullOrEmpty(valStr))
                        return 0;
                    var age = TimeSpan.Parse(valStr);
                    return age.TotalSeconds;
                   }, -3, "seconds") },
                { typeof(DateTime), new ValTypes((o, s) =>
                {
                    var time = s.BaseTime;
                    if (o == null)
                        return 0;
                    var t = o.GetType();
                    DateTime val;
                    if (t == typeof(String))
                    {
                        var valStr = o as String;
                        if (String.IsNullOrEmpty(valStr))
                            return 0;
                        val = DateTime.Parse(valStr);
                    }else
                    {
                        val = (DateTime)o;
                    }
                    var age = time - val;
                    return age.TotalSeconds;
                } , -3, "seconds") },

            };
            return d.Freeze();
        }

        static readonly IReadOnlyDictionary<Type, ValTypes> DataConverters = GetDataConverters();


        /// <summary>
        /// Returns chart data for a table
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public async Task<ReadOnlyMemory<Byte>> TableChart(TableChartParams p, HttpServerRequest request)
        {
            var am = ApiModule;
            var api = am.TryGet(p.TableApi);
            if (api == null)
                throw new Exception("API " + p.TableApi.ToQuoted() + " is not found!");
            var opt = p.Options;
            if (opt != null)
                opt.Cc = 0;
            var ser = am.DefaultSerializer;
            var apiParams = ser.Serialize(opt);
            var res = await api.InvokeAsync(request, apiParams).ConfigureAwait(false);
            var data = ser.Create<TableData>(res);
            Dictionary<String, Tuple<TableDataColumn, int>> colMap = new Dictionary<string, Tuple<TableDataColumn, int>>(StringComparer.Ordinal);
            var cols = data.Cols;
            var cc = cols.Length;
            for (int i = 0; i < cc; ++ i)
            {
                var col = cols[i];
                colMap[col.Name] = Tuple.Create(col, i);
            }
            List<Tuple<TableDataColumn, int>> keys = new List<Tuple<TableDataColumn, int>>(cc);
            List<Tuple<TableDataColumn, int>> values = new List<Tuple<TableDataColumn, int>>(cc);
            var usedColumns = new HashSet<int>();
            foreach (var key in p.Keys)
            {
                var col = colMap[key];
                if (!usedColumns.Add(col.Item2))
                    throw new Exception("Key " + key.ToQuoted() + " is used more than once!");
                keys.Add(col);
            }
            foreach (var key in p.Values)
            {
                var col = colMap[key];
                if (!usedColumns.Add(col.Item2))
                    throw new Exception("Value" + key.ToQuoted() + " have already been used!");
                values.Add(col);
            }
            var rows = data.Rows;
            var rowCount = rows.Length;
            var keyCount = keys.Count;
            var setCount = values.Count;
            if (keyCount <= 0)
                throw new Exception("No keys!");
            if (setCount <= 0)
                throw new Exception("No values!");


            String[] labelTemp = new string[keyCount];
            String[] labels = new string[rowCount];
            ChartJsDataSet[] dataSets = new ChartJsDataSet[setCount];
            GetValDel[] getData = new GetValDel[setCount];

            var seed = String.Join('|', p.TableApi, p.Title, String.Join('|', p.Keys), String.Join('|', p.Values));
            var setGrad = setCount > 1;
            var gc = setGrad ? setCount : rowCount;
            var grad = ColorTools.HtmlRandomGradient(gc, seed, 0.4);
            var gradBorder = ColorTools.HtmlRandomGradient(gc, seed, 1.0);
            var dcs = DataConverters;
            int precision = -2;
            for (int i = 0; i < setCount; ++i)
            {
                var col = values[i].Item1;
                var t = Type.GetType(col.Type);
                if (t == null)
                    throw new Exception("Invalid type found for column: " + col.Name.ToQuoted());
                if (!dcs.TryGetValue(t, out var typeInfo))
                    throw new Exception("Unsupported type " + t.FullName.ToQuoted() + " found for column: " + col.Name.ToQuoted());
                getData[i] = typeInfo.GetVal;
                var newP = typeInfo.Precision;
                if (i == 0)
                {
                    precision = newP;
                }else
                {
                    bool pre = precision < 0;
                    if (pre)
                        precision -= precision;
                    bool newPre = newP < 0;
                    if (newPre)
                        newP -= newP;
                    if (newP > precision)
                        precision = newP;
                    if ((pre == newPre) && pre)
                        precision = -precision;
                }
                dataSets[i] = new ChartJsDataSet
                {
                    label = GetColumnName(col),
                    data = new double[rowCount],
                    backgroundColor = setGrad ? [grad[i]] : grad,
                    borderColor = setGrad ? [gradBorder[i]] : gradBorder,
                };
            }

            var state = new GetValState(DateTime.UtcNow);

            for (int ri = 0; ri < rowCount; ++ri)
            {
                var src = rows[ri].Values;
                for (int i = 0; i < keyCount; ++i)
                    labelTemp[i] = src[keys[i].Item2]?.ToString();
                labels[ri] = String.Join(p.KeySeparator, labelTemp);
                for (int i = 0; i < setCount; ++i)
                    dataSets[i].data[ri] = getData[i](src[values[i].Item2], state);
            }
            var title = p.Title;
            var haveTitle = !String.IsNullOrEmpty(title);

            var config = new ChartJsConfig
            {
                Precision = precision,
                RefreshRate = (int)data.RefreshRate,
                data = new ChartJsData
                {
                    labels = labels,
                    datasets = dataSets
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        title = new ChartJsTitle
                        {
                            text = haveTitle ? [title] : null,
                            display = haveTitle,
                            fullSize = true,
                            font = new ChartJsFontOptions
                            {
                                weight = "bold",
                            }
                        }
                    },
                    maintainAspectRatio = false,
                }
            };
            return ChartSerialize(config);
        }

        #region Chart exporters


        WebMenuItem[] ChartExporterUserMenu = [];
        WebMenuItem[] ChartExporterMenu = [];


        /// <summary>
        /// Get the menu items for all registered chart exporters
        /// </summary>
        /// <returns></returns>
        [WebApi]
        public WebMenuItem[] GetChartExporters(HttpServerRequest context) => context.Session?.Auth == null ? ChartExporterMenu : ChartExporterUserMenu;


        public override string ToString() => "Chart exporters: " + ChartExporters.Count;

        /// <summary>
        /// Export some chart using a specified exporter
        /// </summary>
        /// <param name="export">Required paramterers</param>
        /// <param name="context"></param>
        /// <returns>A "file" with the exported data</returns>
        [WebApi]
        public Task<MemoryFile> ExportChart(ExportChartRequest export, HttpServerRequest context)
        {
            if (!ChartExporters.TryGetValue(export.ExportAs, out var exporter))
                throw new Exception(export.ExportAs.ToQuoted() + " is not a reqistered chart exporter!");
            switch (exporter.InputType)
            {
                case ChartExportInputTypes.Data:
                    return exporter.Export(export.Data, context, export.Options);
            }
            return exporter.Export(export.DataStr, context, export.Options);
        }


        void UpdateMenu()
        {
            List<Tuple<double, WebMenuItem>> items = new List<Tuple<double, WebMenuItem>>();
            List<Tuple<double, WebMenuItem>> userItems = new List<Tuple<double, WebMenuItem>>();
            var t = ChartExporters;
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
                        Data = ((int)v.InputType).ToString(),
                    });
                    userItems.Add(item);
                    if (!v.RequireUser)
                        items.Add(item);
                }
                ChartExporterMenu = items.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
                ChartExporterUserMenu = userItems.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
            }
        }


        void Manager_OnServiceAdded(object instance, ServiceInfo info)
        {
            AddExporter(instance as IHaveChartExporters);
        }

        void Manager_OnServiceRemoved(object instance, ServiceInfo info)
        {
            RemoveExporter(instance as IHaveChartExporters);
        }

        public bool AddExporter(IChartExporter exp)
        {
            if (!ChartExporters.TryAdd(exp.Name, exp))
                return false;
            UpdateMenu();
            return true;
        }

        public bool RemoveExporter(IChartExporter exp)
        {
            if (!ChartExporters.TryRemove(exp.Name, out var _))
                return false;
            UpdateMenu();
            return true;
        }


        public int AddExporter(IHaveChartExporters exp)
        {
            if (exp == null)
                return 0;
            var t = exp.ChartExporters;
            if (t == null)
                return 0;
            int c = 0;
            foreach (var x in t)
                if (x != null)
                    if (AddExporter(x))
                        ++c;
            return c;
        }

        public int RemoveExporter(IHaveChartExporters exp)
        {
            if (exp == null)
                return 0;
            var t = exp.ChartExporters;
            if (t == null)
                return 0;
            int c = 0;
            foreach (var x in t)
                if (x != null)
                    if (RemoveExporter(x))
                        ++c;
            return c;
        }


        readonly ConcurrentDictionary<String, IChartExporter> ChartExporters = new ConcurrentDictionary<string, IChartExporter>(StringComparer.Ordinal);


        #endregion//Chart exporters



    }




}
