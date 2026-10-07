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
        /// Add a data table to some storage and get a reference to it.
        /// </summary>
        /// <param name="context">The request context (used to find the session for session scoped data)</param>
        /// <param name="scope">The scope of the availability of this data, <see cref="DataScopes.Global"/>, <see cref="DataScopes.AnyUser"/> or <see cref="DataScopes.Session"/></param>
        /// <param name="data">The table data to add</param>
        /// <param name="lifeTimeInSeconds">The life time of this data in seconds (will be removed after this many seconds)</param>
        /// <returns>A reference to the table (meta data)</returns>
        /// <exception cref="Exception">Thrown if the scope is <see cref="DataScopes.AnyUser"/> and no user is logged in, or the scope isn't supported</exception>
        public TableDataReference AddData(HttpServerRequest context, DataScopes scope, BaseTableData data, int lifeTimeInSeconds = 5 * 60)
            => GetDataStorage(context, scope).Add(data, lifeTimeInSeconds);

        /// <summary>
        /// Get the reference to a data table from a given id.
        /// The first char of the id selects the storage: 'g' = global, 'a' = any logged in user, 's' = the current session.
        /// A session id may be suffixed with "@sessionToken" to read the data of another session (requires the "Debug" token).
        /// </summary>
        /// <param name="context">The request context</param>
        /// <param name="dataRefId">The id of the data, must not be null or empty</param>
        /// <returns>The reference, or null if not found / expired</returns>
        /// <exception cref="Exception">Thrown if the id refers to the any user storage and no user is logged in, or the storage isn't supported</exception>
        public TableDataReference GetTableData(HttpServerRequest context, String dataRefId)
            => GetDataStorage(context, dataRefId).GetTable(dataRefId);



        /// <summary>
        /// Tokens required for debug access (pre-processed, i.e lower cased, see <see cref="Authorization.RoleDebug"/>).
        /// </summary>
        static readonly IReadOnlyList<String> DebugAuth = Authorization.RoleDebug;

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
                            if (Sessions.TryGetValue(ss[1].AsMemory(), out var os))
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
        /// Manipulate some table data from a table data reference and return a new table data reference.
        /// </summary>
        /// <param name="request">The table data reference to manipulate and operations to perform</param>
        /// <param name="context">The request context</param>
        /// <returns>A "TableDataReference" to the modified table data (same scope and life time as the source).
        /// This data can't be used as is, must use GetTableData, continue working with it or display the data using some function.</returns>
        /// <exception cref="Exception">Thrown if the reference is invalid or expired</exception>
        [WebApi("{0}")]
        [AiTool("📅✂️")]
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
        /// Get the table content from a table data reference.
        /// </summary>
        /// <param name="request">The table data reference and if column description is required</param>
        /// <param name="context">The request context</param>
        /// <returns>The content of the table referenced (a clone without columns and title if columns aren't required)</returns>
        /// <exception cref="Exception">Thrown if the reference is invalid or expired</exception>
        [WebApi("{0}")]
        [AiTool("📅🔎")]
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
        /// Enumerate over all data references (global, any user and all active sessions).
        /// The key value pair is the session token and session for session data, else both are null.
        /// </summary>
        /// <remarks>Enumerating creates an (empty) data storage for every session that doesn't have one.</remarks>
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
                        yield return Tuple.Create(x, new KeyValuePair<String, HttpSession>(new String(y.Key.Span), y.Value));
            }
        }

        #endregion//Data References


    }



}
