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
    


    public sealed class DashboardParams
    {
        public KeyValueStoreParams Store;
    }




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
            var cacheKey = String.Join('_', id, user);
            var storeKey = StorePrefix + cacheKey;
            var ub = await UserBoards.GetOrUpdateAsync(cacheKey, cc => Store.TryGetAsync<UserBoard>(storeKey)).ConfigureAwait(false);
            return ub?.Board;
        }


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
