using System;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Db;
using SysWeaver.MicroService;
using SysWeaver.Serialization;

namespace SysWeaver.IpLocation.Caches
{


    public sealed class IpLocationMySqlCache : IIpLocationCache
    {
        public IpLocationMySqlCache(IpLocationMySqlCacheParams p = null)
        {
            p = p ?? new IpLocationMySqlCacheParams();
            MaxDays = Math.Max(1, p.DbCachedDays);
            var db = new MySqlDbSimpleStack(p);
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

        public Task<IpLocation> Get(string ip, Func<string, Task<IpLocation>> getFromSource)
            => Cache.GetOrUpdateAsync(ip, async s => await DbGet(s, getFromSource).ConfigureAwait(false));


        async Task<IpLocation> DbGet(string ip, Func<string, Task<IpLocation>> getFromSource)
        {
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                var res = await c.FirstOrDefaultAsync<DbIpLocation>(x => x.Ip == ip).ConfigureAwait(false);
                if (res != null)
                {
                    var old = DateTime.UtcNow.AddDays(-MaxDays);
                    if (res.Added >= old)
                    {
                        var d = res.Data;
                        return d == null ? null : Ser.FromString<IpLocation>(d);
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
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi("Debug/{0}")]
        [WebApiAuth(Roles.Debug)]
        [WebMenuTable(null, "Locations", "Locations", null, null)]
        public Task<TableData> LocationTable(TableDataRequest r)
            => 
            DbData.GetAsTableData<DbIpLocation>(Db, r, 2000);



    }
}
