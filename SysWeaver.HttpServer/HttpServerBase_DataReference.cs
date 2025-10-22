using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.AI;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Media;
using SysWeaver.MicroService;
using SysWeaver.Security;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {

        #region Data References

        /// <summary>
        /// Add a data table to some storage and get a reference to it
        /// </summary>
        /// <param name="context">The request context</param>
        /// <param name="scope">The scope of the availability of this data</param>
        /// <param name="data">The table data to add</param>
        /// <param name="lifeTimeInSeconds">The life time of this data in seconds (will be removed after this many seconds)</param>
        /// <returns>A reference to the table (meta data)</returns>
        public TableDataReference AddData(HttpServerRequest context, DataScopes scope, BaseTableData data, int lifeTimeInSeconds = 5 * 60)
            => GetDataStorage(context, scope).Add(data, lifeTimeInSeconds);

        /// <summary>
        /// Get the reference to a data table from a given id
        /// </summary>
        /// <param name="context">The request context</param>
        /// <param name="dataRefId">The id of the data</param>
        /// <returns></returns>
        public TableDataReference GetTableData(HttpServerRequest context, String dataRefId)
            => GetDataStorage(context, dataRefId).GetTable(dataRefId);



        static readonly IReadOnlyList<String> DebugAuth = ["Debug"];

        DataReferenceStorage GetDataStorage(HttpServerRequest context, String dataRefId)
        {
            var ss = dataRefId.Split('@');
            dataRefId = ss[0];
            switch (dataRefId[0])
            {
                case 'g':
                    return DataRefsGlobal;
                case 'a':
                    if (context.Session.Auth == null)
                        throw new Exception("Must be logged in!");
                    return DataRefsAnyUser;
                case 's':
                    var session = context.Session;
                    if (ss.Length > 1)
                    {
                        if (session.IsValid(DebugAuth))
                        {
                            if (Sessions.TryGetValue(ss[1], out var os))
                                return os.DataRefs;
                        }
                    }
                    return session.DataRefs;
            }
            throw new Exception("Storage is not supported yet!");
        }

        DataReferenceStorage GetDataStorage(HttpServerRequest context, DataScopes scope)
        {
            switch (scope)
            {
                case DataScopes.Global:
                    return DataRefsGlobal;
                case DataScopes.AnyUser:
                    if (context.Session.Auth == null)
                        throw new Exception("Must be logged in!");
                    return DataRefsAnyUser;
                case DataScopes.Session:
                    return context.Session.DataRefs;
            }
            throw new Exception("Storage is not supported yet!");
        }


        readonly DataReferenceStorage DataRefsGlobal = new DataReferenceStorage(DataScopes.Global);
        readonly DataReferenceStorage DataRefsAnyUser = new DataReferenceStorage(DataScopes.AnyUser);


        /// <summary>
        /// Manipulate some table data from a table data refernec and return a new table data reference.
        /// </summary>
        /// <param name="request">The table data reference to manipulate and operations to perform</param>
        /// <param name="context"></param>
        /// <returns>A "TableDataReference" to the modified table data.
        /// This data can't be used as is, must use GetTableData, continue working with it or display the data using some function.</returns>
        [WebApi("{0}")]
        [OpenAiTool("✂️")]
        public TableDataReference EditTableData(EditTableDataRequest request, HttpServerRequest context)
        {
            var bd = context.GetTableData(request.TableDataRef);
            if (bd == null)
                throw new Exception("Invalid or expired data reference!");
            var data = bd.Get();
            var ops = request.Ops;
            data = TableDataEdit.ApplyOps(data, context.ResolveTableData, ops);
            return context.AddData(bd.Scope, data, bd.TimeToLive).AsResponse(request.RequireColumns);
        }

        /// <summary>
        /// Get the table content from a table data reference
        /// </summary>
        /// <param name="request">The table data reference to manipulate and if column description is required</param>
        /// <param name="context"></param>
        /// <returns>The content of the table referenced</returns>
        [WebApi("{0}")]
        [OpenAiTool("🔎")]
        public BaseTableData GetTableData(GetTableDataRequest request, HttpServerRequest context)
        {
            var bd = context.GetTableData(request.TableDataRef);
            if (bd == null)
                throw new Exception("Invalid or expired data reference!");
            var data = bd.Get();
            if (!request.RequireColumns)
            {
                data = data.Clone();
                data.Cols = null;
                data.Title = null;
            }
            return data;
        }
        




        /// <summary>
        /// Enumerate over all data references
        /// </summary>
        public IEnumerable<Tuple<DataReference, KeyValuePair<String, HttpSession>>> AllDataReferences
        {
            get
            {
                KeyValuePair<String, HttpSession> none = new KeyValuePair<string, HttpSession>(null, null);
                foreach (var x in DataRefsGlobal.AllReferences)
                    yield return Tuple.Create(x, none);
                foreach (var x in DataRefsAnyUser.AllReferences)
                    yield return Tuple.Create(x, none);
                foreach (var y in Sessions)
                    foreach (var x in y.Value.DataRefs.AllReferences)
                        yield return Tuple.Create(x, y);
            }
        }

        #endregion//Data References


    }



}
