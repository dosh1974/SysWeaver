using Dapper;
using SimpleStack.Orm;
using SimpleStack.Orm.Expressions.Statements.Typed;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Db
{
    public static class OrmConnectionExt
    {
        /// <summary>
        /// Insert an object in a specific table in the database 
        /// </summary>
        /// <param name="con">The connection object</param>
        /// <param name="obj">The object to insert</param>
        /// <param name="tableName">The name of the table</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="OrmException"></exception>
        public static ValueTask<int> InsertAsync<T>(this OrmConnection con, T obj, String tableName, CancellationToken cancellationToken = new CancellationToken())
            => InsertAsync<int, T>(con, obj, tableName, cancellationToken);

        /// <summary>
        /// Insert an object in a specific table in the database 
        /// </summary>
        /// <param name="con">The connection object</param>
        /// <param name="obj">The object to insert</param>
        /// <param name="tableName">The name of the table</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <returns></returns>
        /// <exception cref="OrmException"></exception>
        public async static ValueTask<TKey> InsertAsync<TKey, T>(this OrmConnection con, T obj, String tableName, CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                var dp = con.DialectProvider;
                var insertStatement = new TypedInsertStatement<T>(dp);
                insertStatement.Into(tableName);
                insertStatement.Values(obj, Array.Empty<string>());
                var commandDefinition = dp.ToInsertStatement(insertStatement.Statement, CommandFlags.None, cancellationToken);
                return await con.ExecuteScalarAsync<TKey>(commandDefinition).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new OrmException(e.Message, e);
            }
        }

    }

}