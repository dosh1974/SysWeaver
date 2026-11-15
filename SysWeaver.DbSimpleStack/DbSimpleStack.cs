using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SimpleStack.Orm;
using SysWeaver.Compression;
using SysWeaver.Serialization;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{

    /// <summary>
    /// Base class for database providers built on SimpleStack.Orm
    /// </summary>
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
        /// Create a db provider, resolves the server address and applies any forced system wide config
        /// </summary>
        /// <param name="p">The parameters, if null a default instance of paramType is created</param>
        /// <param name="dp">The SimpleStack dialect provider</param>
        /// <param name="paramType">The type of p</param>
        /// <param name="msg">Optional message host</param>
        protected DbSimpleStack(DbParams p, IDialectProvider dp, Type paramType, IMessageHost msg)
        {
            Msg = msg;
            paramType = paramType ?? typeof(DbParams);
            if (p == null)
                p = Activator.CreateInstance(paramType) as DbParams;
            if (p == null)
                throw new Exception("No paramaters!");

            var server = p.Server;
            if (!String.IsNullOrEmpty(server))
            {
                server = PathTemplate.Resolve(server);
                if (PathExt.IsValidPathToFile(server, true))
                {
                    var fn = server;
                    server = FileExt.ReadNonCommentString(server);
                    if (server == null)
                        throw new Exception("Server address file " + fn.ToFilename() + " must contain at least one line of text!");

                }
            }
            if (server.FastEquals("127.0.0.1") || String.IsNullOrEmpty(server))
                server = "localhost";
            p.Server = server;
            Config.ApplyConfig(paramType, p, 
                Path.Combine("DbConfigs", String.Concat(paramType.Name, '_', PathExt.SafeFilename(server), '_', p.Port, ".json")), 
                String.Concat("These settings are forced for all SysWeaver application in this system.\nFor all db connections to ", server, ':', p.Port)
                );
            LogPrefix = String.Concat('[', Name, '@', server, ':', p.Port, "] ");
            P = p;
            DP = dp;
            BlobSer = SerManager.Get(p.BlobSer);
            BlobComp = CompManager.GetFromHttp(p.BlobComp);
        }


        /// <summary>
        /// Prefix to use for log messages, identifies the provider and server
        /// </summary>
        public readonly String LogPrefix;

        /// <summary>
        /// Optional message host used for log messages
        /// </summary>
        public readonly IMessageHost Msg;

        #region Blob

        readonly ISerializerType BlobSer;
        readonly ICompType BlobComp;

        /// <summary>
        /// Convert an object to a database blob
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The object to blobbify</param>
        /// <param name="compLevel">The compression level to use.
        /// Null means use the default level based on the size of the data:
        /// levelSize bytes or less: Best.
        /// Otherwise: Balanced.
        /// </param>
        /// <param name="levelSize">The size threshold for determining the compression level (only applies if compLevel is null)</param>
        /// <returns>A binary blob</returns>
        public Byte[] ToBlob<T>(T data, CompEncoderLevels? compLevel = null, int levelSize = 1024)
        {
            var t = BlobSer.Serialize(data);
            var cl = compLevel ?? (t.Length <= levelSize ? CompEncoderLevels.Best : CompEncoderLevels.Balanced);
            var c = BlobComp.GetCompressed(t.Span, cl);
            //  GetCompressed returns a new array of the exact size (owned by the caller), avoid copying it again
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray<Byte>(c, out var seg) && (seg.Offset == 0) && (seg.Count == seg.Array.Length))
                return seg.Array;
            return c.ToArray();
        }

        /// <summary>
        /// Convert a database blob to an object
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The blob data</param>
        /// <returns>The object</returns>
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
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="data">The blob data, may be null</param>
        /// <returns>The object, or a new instance if data is null</returns>
        public T NewOrBlob<T>(Byte[] data) where T : new()
        {
            if (data == null)
                return new T();
            return FromBlob<T>(data);
        }




        #endregion//Blob

        /// <summary>
        /// The connection parameters
        /// </summary>
        public readonly DbParams P;

        /// <summary>
        /// The SimpleStack dialect provider
        /// </summary>
        public readonly IDialectProvider DP;

        /// <summary>
        /// Create a schema, errors (such as the schema already existing) are ignored
        /// </summary>
        /// <param name="schema">The name of the schema</param>
        /// <returns>A task that completes when done</returns>
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
        /// <returns>A task that completes when a connection could be made</returns>
        /// <exception cref="Exception">If no connection could be made within InitRetrySeconds</exception>
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
                        m?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Connection problems, will retry in a while: {ex.Message}");
                    if (i > 0)
                        if (DateTime.UtcNow > end)
                            throw;
                    await Task.Delay(1000).ConfigureAwait(false);
                    m?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Retrying connection");
                }
            }
            m?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Connected!");
        }

        /// <summary>
        /// The partition folders in use (set by derived classes that support partitioning)
        /// </summary>
        public String[] Partitions { get; protected set; }

        /// <summary>
        /// Creates a table if it doesn't exist and validates it
        /// </summary>
        /// <typeparam name="T">The type of the table</typeparam>
        /// <param name="con">An open connection to use for queries</param>
        /// <returns>A task that completes when done</returns>
        public virtual async Task InitTable<T>(OrmConnection con)
        {
            await con.CreateTableIfNotExistsAsync<T>().ConfigureAwait(false);
            await ValidateTable<T>(con).ConfigureAwait(false);
        }

        OrmConnectionFactory F;

        /// <summary>
        /// Open a connection (Init must have been called)
        /// </summary>
        /// <returns>An open connection, the caller must dispose it</returns>
        public OrmConnection Get() => F.OpenConnection();

        /// <summary>
        /// Open a connection (Init must have been called)
        /// </summary>
        /// <returns>An open connection, the caller must dispose it</returns>
        public Task<OrmConnection> GetAsync() => F.OpenConnectionAsync();



        /// <summary>
        /// Get all rows in a table
        /// </summary>
        /// <typeparam name="T">The row type</typeparam>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>All rows in the table</returns>
        public async Task<List<T>> GetAll<T>(String tableName = null)
        {
            using var c = await F.OpenConnectionAsync().ConfigureAwait(false);
            return (await (String.IsNullOrEmpty(tableName) ? c.SelectAsync<T>() : c.SelectAsync<T>(x => x.From(tableName))).ConfigureAwait(false)).ToList();
        }


        /// <summary>
        /// Get the column index for the given member
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <param name="fieldName">The name of the field</param>
        /// <returns>The column index, 0 if the field was not found</returns>
        public static int GetColumnIndex<T>(String fieldName) => DbSimpleStackCache<T>.Ordinal.TryGetValue(fieldName, out var o) ? o : 0;


        /// <summary>
        /// Partition a table (does nothing if the partition count is less than 2)
        /// </summary>
        /// <typeparam name="T">The type of the table</typeparam>
        /// <param name="con">An open connection to use for queries</param>
        /// <param name="partitionCount">Number of partitions (no explicit folders)</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>True if the table was partitioned</returns>
        public Task<bool> Partition<T>(OrmConnection con, int partitionCount, String tableName = null)
            => partitionCount < 2 ? TaskExt.FalseTask : Partition<T>(con, new String[partitionCount], tableName);

        /// <summary>
        /// Partition a table
        /// </summary>
        /// <typeparam name="T">The type of the table</typeparam>
        /// <param name="con">An open connection to use for queries</param>
        /// <param name="partitionFolders">The folder of each partition</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>True if the table was partitioned</returns>
        public Task<bool> Partition<T>(OrmConnection con, String[] partitionFolders, String tableName = null)
            => Partition(con, typeof(T), partitionFolders, tableName);

        /// <summary>
        /// Partition a table (the base implementation does nothing)
        /// </summary>
        /// <param name="con">An open connection to use for queries</param>
        /// <param name="t">The type of the table</param>
        /// <param name="partitionFolders">The folder of each partition</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>True if the table was partitioned</returns>
        public virtual Task<bool> Partition(OrmConnection con, Type t, String[] partitionFolders, String tableName = null)
            => TaskExt.FalseTask;

    }


}
