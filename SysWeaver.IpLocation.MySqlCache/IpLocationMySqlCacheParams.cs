using SysWeaver.Db;

namespace SysWeaver.IpLocation.Caches
{
    /// <summary>
    /// Parameters for the <see cref="IpLocationMySqlCache"/>
    /// </summary>
    public sealed class IpLocationMySqlCacheParams : MySqlDbParams
    {
        /// <summary>
        /// Create the parameters, the database schema defaults to "IpLocation"
        /// </summary>
        public IpLocationMySqlCacheParams()
        {
            Schema = "IpLocation";
        }

        /// <summary>
        /// The number of days that a location is kept in the database before it is looked up again (at least 1).
        /// Defaults to 90.
        /// </summary>
        public int DbCachedDays = 90;

        /// <summary>
        /// The number of minutes that a location is cached in memory (in front of the database), at least 1.
        /// Defaults to 60.
        /// </summary>
        public int MaxMemCachedMinutes = 60;

    }
}
