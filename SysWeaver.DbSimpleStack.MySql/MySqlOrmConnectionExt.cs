using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimpleStack.Orm;

// https://github.com/SimpleStack/simplestack.orm

namespace SysWeaver.Db
{
    public static class MySqlOrmConnectionExt
    {
        /// <summary>
        /// Bulk upsert (or insert if no aggregation attributes are present).
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="con">The connection to use</param>
        /// <param name="data">The data</param>
        /// <param name="tableName">Optional table name</param>
        /// <param name="maxBatchSize">Number of rows per query</param>
        /// <returns></returns>
        public static Task BulkUpsert<T>(this OrmConnection con, IReadOnlyList<T> data, String tableName = null, int maxBatchSize = 4096)
            => MySqlDbSimpleStack.BulkUpsert(con, data, tableName, maxBatchSize);

        /// <summary>
        /// Upsert (or insert if no aggregation attributes are present).
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="con">The connection to use</param>
        /// <param name="data">The data</param>
        /// <param name="tableName">Optional table name</param>
        /// <returns></returns>
        public static Task Upsert<T>(this OrmConnection con, T data, String tableName = null)
            => MySqlDbSimpleStack.Upsert(con, data, tableName);




    }





}
