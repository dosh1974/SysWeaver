using System;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Db;
using SysWeaver.MicroService;
using SysWeaver.Serialization;

namespace SysWeaver.IpLocation.Caches
{


    /// <summary>
    /// An ip location cache that stores the locations in a MySql database (with a memory cache in front of it)
    /// </summary>
    public sealed class IpLocationMySqlCache : IIpLocationCache
    {
        /// <summary>
        /// Create the cache (the database tables are created in the background)
        /// </summary>
        /// <param name="msg">Optional message host, used to report database messages</param>
        /// <param name="p">Parameters, null to use the defaults</param>
        public IpLocationMySqlCache(IMessageHost msg = null, IpLocationMySqlCacheParams p = null)
        {
            p = p ?? new IpLocationMySqlCacheParams();
            MaxDays = Math.Max(1, p.DbCachedDays);
            var db = new MySqlDbSimpleStack(p, msg);
            Db = db;
            Init(db).RunAsync();
            Cache = new FastMemCache<string, IpLocation>(TimeSpan.FromMinutes(Math.Max(1, p.MaxMemCachedMinutes)), StringComparer.Ordinal);
            Ser = SerManager.GetText("json");
        }

        readonly ITextSerializerType Ser;
        readonly FastMemCache<String, IpLocation> Cache;
        readonly int MaxDays;

        MySqlDbSimpleStack Db;

        static async Task Init(MySqlDbSimpleStack db)
        {
            await db.Init().ConfigureAwait(false);
            using (var c = await db.GetAsync().ConfigureAwait(false))
            {
                await db.InitTable<DbIpLocation>(c).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public ValueTask<IpLocation> Get(string ip, Func<string, Task<IpLocation>> getFromSource)
            => Cache.GetOrUpdateAsync(ip, DbGet, getFromSource);

        static readonly DateTime CityAdded = new DateTime(2026, 09, 07, 09, 45, 00);

        async Task<IpLocation> DbGet(string ip, Func<string, Task<IpLocation>> getFromSource)
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var res = await c.FirstOrDefaultAsync<DbIpLocation>(x => x.Ip == ip).ConfigureAwait(false);
                if (res != null)
                {
                    var old = DateTime.UtcNow.AddDays(-MaxDays);
                    if (res.Added >= old)
                    {
                        var d = res.Data;
                        if (d == null)
                            return null;
                        var ipl = Ser.FromString<IpLocation>(d);
                        if (ipl.City != null) 
                            return ipl;
                        if (res.Added > CityAdded)
                            return ipl;
                    }
                    await c.DeleteAsync(res).ConfigureAwait(false);
                }
                var data = await getFromSource(ip).ConfigureAwait(false);
                await c.InsertAsync(new DbIpLocation
                {
                    Ip = ip,
                    Added = DateTime.UtcNow,
                    Data = data == null ? null : Ser.ToString(data),
                }).ConfigureAwait(false);
                await tr.CommitAsync().ConfigureAwait(false);
                return data;
            }
        }

        /// <summary>
        /// All locations cached in the DB
        /// </summary>
        /// <param name="r">The table request</param>
        /// <returns>The table data (at most 2000 rows)</returns>
        [WebApi("Debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebMenuTable(null, "Locations", "Locations", null, null)]
        public Task<TableData> LocationTable(TableDataRequest r)
            => 
            DbData.GetAsTableData<DbIpLocation>(Db, r, 2000);



    }
}
