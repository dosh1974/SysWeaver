using SysWeaver.Docs;
using SysWeaver.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Threading.Tasks;




namespace SysWeaver.MicroService
{
    


    /// <summary>
    /// Parameters for <see cref="DashboardService"/>.
    /// </summary>
    public sealed class DashboardParams
    {
        /// <summary>
        /// The key value store that holds the dashboards, null for the default store (id "Default")
        /// </summary>
        public KeyValueStoreParams Store;
    }




    /// <summary>
    /// Micro service that returns per user dashboards (Api/application/GetBoard), read from a key value store under the key "Dashboard_[id]_[user guid]".
    /// Nothing in the framework writes dashboards yet, and sessions without a user get none.
    /// </summary>
    [IsMicroService]
    [WebApiUrl("application")]
    public sealed class DashboardService
    {
        public DashboardService(DashboardParams p, ServiceManager manager)
        {
            p = p ?? new DashboardParams();
            Manager = manager;
            Store = KeyValueStore.Get(p.Store);
        }

        readonly KeyValueStore Store;
        readonly ServiceManager Manager;

        /// <summary>
        /// Get a dashboard
        /// </summary>
        /// <param name="id">Id of the dashboard to get</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>A dashboard or null if not found</returns>
        [WebApi]
        [WebApiClientCache(30)]
        [WebApiRequestCache(29)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        public async Task<WebDashboard> GetBoard(String id, HttpServerRequest context)
        {
            id = (id ?? "").FastToLower();
            var user = context.Session.Auth?.Guid;
            if (user == null)
            {
                if (!NoUserBoards.TryGetValue(id, out var nob))
                    return null;
                return nob?.Board;
            }
            var cacheKey = String.Concat(id, "_", user);
            var ub = await UserBoards.GetOrUpdateAsync(cacheKey, LoadUserBoard, Store).ConfigureAwait(false);
            return ub?.Board;
        }

        /// <summary>
        /// Read a user board from the store (only on a cache miss: the store key is only built here)
        /// </summary>
        /// <param name="cacheKey">The cache key ("[id]_[user guid]")</param>
        /// <param name="store">The store</param>
        static Task<UserBoard> LoadUserBoard(String cacheKey, KeyValueStore store)
            //  The store key must be a valid file name (the id is user input and user guids contain ':'), invalid chars are replaced with '_' (keys that were valid are unchanged)
            => store.TryGetAsync<UserBoard>(PathExt.SafeFilename(StorePrefix + cacheKey));


        readonly FastMemCache<String, UserBoard> UserBoards = new FastMemCache<string, UserBoard>(TimeSpan.FromSeconds(60), StringComparer.Ordinal);


        const String StorePrefix = "Dashboard_";

#pragma warning disable CS0649

        sealed class UserBoard
        {
            public WebDashboard Board;
        }

        sealed class NoUserBoard
        {
            public WebDashboard Board;
        }
#pragma warning restore CS0649

        readonly ConcurrentDictionary<String, NoUserBoard> NoUserBoards = new ConcurrentDictionary<string, NoUserBoard>(StringComparer.Ordinal);


    }



}
