using SysWeaver.Data;
using System;
using SysWeaver.Net;
using SysWeaver.Serialization;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
    public sealed class UserDataTableDataExporter : ITableDataExporter
    {
        internal UserDataTableDataExporter(IUserStorageService userStore, ISerializerType jsonSer, int scopeIndex)
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
        readonly ISerializerType Ser;
        readonly IUserStorageService UserStore;
        readonly int ScopeIndex;

        public string Name { get; init; }

        public string Desc { get; init; }

        public string Icon { get; init; }

        public double Order { get; init; }

        public bool RequireUser => true;

        public async Task<MemoryFile> Export(BaseTableData tableData, Object context, TabelDataExportOptions options = null)
        {
            HttpServerRequest c = context as HttpServerRequest;
            if (c == null)
                return null;
            options = options ?? new TabelDataExportOptions();
            var name = (String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename) + ".json";
            var scope = ScopeIndex;
            var us = UserStore;
            String url;
            var serData = Ser.Serialize(tableData);
            if (scope == 0)
            {
                var file = await us.StorePrivateFile(c, name, serData, false).ConfigureAwait(false);
                url = "explore/table.html?q=../Api/" + nameof(ExploreHttpServerService.UserStoreTable) + "&p=" + file;
                url = await us.StorePrivateLink(c, url, [file]).ConfigureAwait(false);
            }
            else
            {
                String auth = scope == 1 ? "" : null;
                var file = await us.StorePublicFile(c, name, serData, auth, false).ConfigureAwait(false);
                url = "explore/table.html?q=../Api/" + nameof(ExploreHttpServerService.UserStoreTable) + "&p=" + file;
                url = await us.StorePublicLink(c, url, auth, [file]).ConfigureAwait(false);
            }
            return new MemoryFile(url);
        }
    }


}
