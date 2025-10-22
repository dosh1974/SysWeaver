using System;
using System.Threading.Tasks;
using SysWeaver.Chart;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.MicroService
{
    sealed class UserStorageChartExporter : IChartExporter
    {
        public UserStorageChartExporter(IUserStorageService userStore, Func<ChartJsConfig, ReadOnlyMemory<Byte>> jsonSer, int scopeIndex)
        {
            UserStore = userStore;
            switch (scopeIndex)
            {
                case 0:
                    Name = "Private link";
                    Desc = "Save as a link that only you can view";
                    break;
                case 1:
                    Name = "Protected link";
                    Desc = "Save as a link that all logged in users can view";
                    break;
                case 2:
                    Name = "Public link";
                    Desc = "Save as a link that anyone can view";
                    break;
            }
            Icon = "IconShareLink" + scopeIndex;
            Order = -5 + scopeIndex;
            ScopeIndex = scopeIndex;
            Ser = jsonSer;
        }
        readonly Func<ChartJsConfig, ReadOnlyMemory<Byte>> Ser;
        readonly IUserStorageService UserStore;
        readonly int ScopeIndex;

        public string Name { get; init; }

        public string Desc { get; init; }

        public string Icon { get; init; }

        public double Order { get; init; }

        public bool RequireUser => true;

        public ChartExportInputTypes InputType => ChartExportInputTypes.Data;

        public async Task<MemoryFile> Export(object data, Object context, ChartExportOptions options = null)
        {
            HttpServerRequest c = context as HttpServerRequest;
            if (c == null)
                return null;
            options = options ?? new ChartExportOptions();
            var d = data as ChartJsConfig;
            if (d == null)
                throw new Exception("Expected data of the type " + typeof(ChartJsConfig).FullName.ToQuoted());
            d.RefreshRate = 5 * 60 * 1000;
            var name = (String.IsNullOrEmpty(options.Filename) ? "Chart" : options.Filename) + ".json";
            var scope = ScopeIndex;
            var us = UserStore;
            String url;
            var serData = Ser(d);
            if (scope == 0)
            {
                var file = await us.StorePrivateFile(c, name, serData, false).ConfigureAwait(false);
                url = "chart/chart.html?q=../" + file;
                url = await us.StorePrivateLink(c, url, [file]).ConfigureAwait(false);
            }
            else
            {
                String auth = scope == 1 ? "" : null;
                var file = await us.StorePublicFile(c, name, serData, auth, false).ConfigureAwait(false);
                url = "chart/chart.html?q=../" + file;
                url = await us.StorePublicLink(c, url, auth, [file]).ConfigureAwait(false);
            }
            return new MemoryFile(url);
        }
    }




}
