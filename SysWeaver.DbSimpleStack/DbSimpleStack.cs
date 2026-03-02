using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using SimpleStack.Orm;
using SysWeaver.Compression;
using SysWeaver.Serialization;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{

    public interface IBlob
    {
        /// <summary>
        /// Convert an object to a database blob
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        Byte[] ToBlob<T>(T data);

        /// <summary>
        /// Convert a database blob to an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        T FromBlob<T>(ReadOnlySpan<Byte> data);

        /// <summary>
        /// Convert a blob to an object, or a default instance if the blob is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        T NewOrBlob<T>(Byte[] data) where T : new();

    }

    public abstract class DbSimpleStack : IBlob
    {

        class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
        {
            public override void SetValue(IDbDataParameter parameter, DateTime value)
            {
                var k = value.Kind;
                if (k == DateTimeKind.Local)
                    value = value.ToUniversalTime();
                if (k == DateTimeKind.Unspecified)
                    value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
                parameter.Value = value;
            }

            public override DateTime Parse(object value)
            {
                var v = (DateTime)value;
                var k = v.Kind;
                if (k == DateTimeKind.Unspecified)
                    v = DateTime.SpecifyKind(v, DateTimeKind.Utc);
                return v;
            }
        }

        static DbSimpleStack()
        {
            SqlMapper.AddTypeHandler(new DateTimeHandler());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="dp"></param>
        /// <param name="paramType">The type of p</param>
        /// <param name="msg">Optional message host</param>
        public DbSimpleStack(DbParams p, IDialectProvider dp, Type paramType, IMessageHost msg)
        {
            Msg = msg;
            paramType = paramType ?? typeof(DbParams);
            if (p == null)
                p = Activator.CreateInstance(paramType) as DbParams;
            if (p == null)
                throw new Exception("No paramaters!");
            if (p.Server.FastEquals("127.0.0.1") || String.IsNullOrEmpty(p.Server))
                p.Server = "localhost";

            Config.ApplyConfig(paramType, p, 
                Path.Combine("DbConfigs", String.Concat(paramType.Name, '_', PathExt.SafeFilename(p.Server), '_', p.Port, ".json")), 
                String.Concat("These settings are forced for all SysWeaver application in this system.\nFor all db connections to ", p.Server, ':', p.Port)
                );
            LogPrefix = String.Concat('[', Name, '@', p.Server, ':', p.Port, "] ");
            P = p;
            DP = dp;
            BlobSer = SerManager.Get(p.BlobSer);
            BlobComp = CompManager.GetFromHttp(p.BlobComp);
        }


        public readonly String LogPrefix;

        public readonly IMessageHost Msg;

        #region Blob

        readonly ISerializerType BlobSer;
        readonly ICompType BlobComp;

        /// <summary>
        /// Convert an object to a database blob
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public Byte[] ToBlob<T>(T data)
        {
            var t = BlobSer.Serialize(data);
            var c = BlobComp.GetCompressed(t.Span, CompEncoderLevels.Best);
            return c.ToArray();
        }

        /// <summary>
        /// Convert a database blob to an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public T FromBlob<T>(ReadOnlySpan<Byte> data)
        {
            using var tMem = BlobComp.GetUnmanagedDecompressed(data);
            var t = tMem.Memory;
#if DEBUG
            if (BlobSer.Encoding != null)
            {
                var temp = BlobSer.Encoding.GetString(t.Span);
            }
#endif//DEBUG
            var o = BlobSer.Create<T>(t);
            return o;
        }

        /// <summary>
        /// Convert a blob to an object, or a default instance if the blob is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public T NewOrBlob<T>(Byte[] data) where T : new()
        {
            if (data == null)
                return new T();
            return FromBlob<T>(data);
        }




        #endregion//Blob

        public readonly DbParams P;

        public readonly IDialectProvider DP;

        public virtual async Task CreateSchemaIfNotExist(String schema)
        {
            var p = P;
            var f = new OrmConnectionFactory(DP, p.BuildConnectionString(false));
            f.DefaultCommandTimeout = Math.Max(1, p.TimeOut);
            using (var c = await f.OpenConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    await c.CreateSchemaAsync(schema).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Validates a table (after creation) to make sure that it matches the type
        /// </summary>
        /// <typeparam name="T">The type of the table</typeparam>
        /// <param name="con">An open connection to use for queries</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>True if table validation was made and the table is valid</returns>
        public Task<bool> ValidateTable<T>(OrmConnection con, String tableName = null) => ValidateTable(con, typeof(T), tableName);

        /// <summary>
        /// Validates a table (after creation) to make sure that it matches the type
        /// </summary>
        /// <param name="con">An open connection to use for queries</param>
        /// <param name="t">The type of the table</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>True if table validation was made and the table is valid</returns>
        public virtual Task<bool> ValidateTable(OrmConnection con, Type t, String tableName = null) => Task.FromResult(false);




        /// <summary>
        /// Remove all indices from a table (except for primary or unique constraints)
        /// </summary>
        /// <param name="con">An open connection to use for queries</param>
        /// <param name="t">The type of the table</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>True if indices was removed</returns>
        public virtual Task<bool> RemoveIndices(OrmConnection con, Type t, String tableName = null) => Task.FromResult(false);


        /// <summary>
        /// Name of the database provider
        /// </summary>
        public abstract String Name { get; }

        /// <summary>
        /// Call init once to init the db (creates the schema if it doesn't exist and creates the connection facotry etc)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public virtual async Task Init()
        {
            var f = F;
            if (f != null)
                return;
            var p = P;
            f = new OrmConnectionFactory(DP, p.BuildConnectionString());
            f.DefaultCommandTimeout = Math.Max(1, p.TimeOut);
            F = f;
            var s = p.InitRetrySeconds;
            var end = DateTime.UtcNow.AddSeconds(Math.Max(s, 0));
            var m = Msg;
            for (long i = 0; ;++i)
            {
                try
                {
                    using var c = await f.OpenConnectionAsync().ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    if (!p.ReadOnly)
                    {
                        try
                        {
                            await CreateSchemaIfNotExist(p.Schema).ConfigureAwait(false);
                            break;
                        }
                        catch
                        {
                        }
                    }
                    if (i == 0)
                        m?.AddMessage(LogPrefix + "Connection problems, will retry in a while: " + ex.Message, MessageLevels.Warning);
                    if (i > 0)
                        if (DateTime.UtcNow > end)
                            throw;
                    await Task.Delay(1000).ConfigureAwait(false);
                    m?.AddMessage(LogPrefix + "Retrying connection", MessageLevels.Debug);
                }
            }
            m?.AddMessage(LogPrefix + "Connected!", MessageLevels.Debug);
        }

        public String[] Partitions { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con">An open connection to use for queries</param>
        /// <returns></returns>
        public virtual async Task InitTable<T>(OrmConnection con)
        {
            await con.CreateTableIfNotExistsAsync<T>().ConfigureAwait(false);
            await ValidateTable<T>(con).ConfigureAwait(false);
        }

        OrmConnectionFactory F;

        public OrmConnection Get() => F.OpenConnection();
        public Task<OrmConnection> GetAsync() => F.OpenConnectionAsync();


        /// <summary>
        /// Get the column index for the given member
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static int GetColumnIndex<T>(String fieldName) => DbSimpleStackCache<T>.Ordinal.TryGetValue(fieldName, out var o) ? o : 0;


        public Task<bool> Partition<T>(OrmConnection con, int partitionCount, String tableName = null)
            => partitionCount < 2 ? TaskExt.FalseTask : Partition<T>(con, new String[partitionCount], tableName);

        public Task<bool> Partition<T>(OrmConnection con, String[] partitionFolders, String tableName = null)
            => Partition(con, typeof(T), partitionFolders, tableName);

        public virtual Task<bool> Partition(OrmConnection con, Type t, String[] partitionFolders, String tableName = null)
            => TaskExt.FalseTask;

    }


}
