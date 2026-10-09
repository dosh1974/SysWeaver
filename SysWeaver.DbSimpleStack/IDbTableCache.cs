using SimpleStack.Orm;
using System;
using System.Threading.Tasks;

namespace SysWeaver.Db
{
    public interface IDbTableCache : IDisposable, IPerfMonitored
    {
        /// <summary>
        /// Explicitly sync, call once after creation to populate the cache
        /// </summary>
        /// <returns></returns>
        Task SyncNow();

        /// <summary>
        /// Explicitly sync using an explicit db connection, call once after creation to populate the cache
        /// </summary>
        /// <param name="c">The db connection to use</param>
        /// <returns></returns>
        Task SyncNow(OrmConnection c);
    }

}