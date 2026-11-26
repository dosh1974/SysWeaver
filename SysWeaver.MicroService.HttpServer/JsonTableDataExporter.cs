using SysWeaver.Data;
using System;
using SysWeaver.Net;
using SysWeaver.Serialization;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Export table as JSON
    /// </summary>
    public sealed class JsonTableDataExporter : ITableDataExporter
    {

        public override string ToString() => Name;

        /// <summary>
        /// Export as indented json
        /// </summary>
        public static readonly JsonTableDataExporter Verbose = new JsonTableDataExporter(false, "JSON (verbose)", 9000,
            "A json file that is formatted to be more human readable.");

        /// <summary>
        /// Export as json without white space
        /// </summary>
        public static readonly JsonTableDataExporter Compact = new JsonTableDataExporter(true, "JSON (compact)", 9001, 
            "A json file without unnecessary white spaces.");



        JsonTableDataExporter(bool compact, String name, double order, String desc)
        {
            C = compact;
            Name = name;
            Desc = desc;
            Order = order;
        }

        readonly bool C;

        public String Name { get; init; }
        public String Desc { get; init; }

        public String Icon => "IconFileJson";

        public double Order { get; init; }

        public bool RequireUser => false;

        public Task<MemoryFile> Export(BaseTableData tableData, Object context, TableDataExportOptions options = null)
        {
            options = options ?? new TableDataExportOptions();
            var sdata = new BaseTableData();
            sdata.CopyFrom(tableData);
            if (options.NoHeaders)
            {
                sdata.Cols = null;
                sdata.Title = null;
            }
            var s = SerManager.Get("json");
            if (s == null)
                throw new Exception("No json serializer registered!");
            //  The formatting is done here, since some serializers can't write formatted json (and some add type information when they do)
            var data = s.Serialize(sdata, SerializerOptions.Compact);
            //  Some serializers write NaN and infinities as is, that's not json so keep the data unformatted
            var content = (C ? null : JsonPretty.FormatUtf8(data.Span)) ?? data.ToArray();
            var name = String.IsNullOrEmpty(options.Filename) ? "Table" : options.Filename;
            return Task.FromResult(new MemoryFile(name + ".json", HttpServerTools.JsonMime, content));
        }
    }


}
