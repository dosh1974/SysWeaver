using SysWeaver.Db;

namespace SysWeaver.IpLocation.Caches
{
    public sealed class IpLocationMySqlCacheParams : MySqlDbParams
    {
        public IpLocationMySqlCacheParams()
        {
            Schema = "IpLocation";
        }

        public int DbCachedDays = 90;

        public int MaxMemCachedMinutes = 60;

    }
}
