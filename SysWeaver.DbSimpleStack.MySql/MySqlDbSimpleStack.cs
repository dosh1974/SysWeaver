using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dapper;

using MySqlConnector;
using SimpleStack.Orm;
using SimpleStack.Orm.Attributes;
using SimpleStack.Orm.Expressions.Statements;
using SimpleStack.Orm.Expressions.Statements.Typed;
using SimpleStack.Orm.MySQLConnector;

// https://github.com/SimpleStack/simplestack.orm

namespace SysWeaver.Db
{


    public class MySqlDbSimpleStack : DbSimpleStack
    {
        static readonly ConcurrentDictionary<Tuple<ModelDefinition, String>, String[]> Upserts = new ConcurrentDictionary<Tuple<ModelDefinition, string>, string[]>();
        
        public static String[] GetUpsert(ModelDefinition def, string tableName)
        {
            var dp = MySqlConnectorDialectProvider.Instance;
            var ns = dp.NamingStrategy;
            if (tableName == null)
                tableName = ns.GetTableName(def.Alias ?? def.Name);
            var key = Tuple.Create(def, tableName);
            var cache = Upserts;
            if (cache.TryGetValue(key, out var c))
                return c;
            var t = new UpdateStatement();
            var tu = t.UpdateFields;
            String qt = dp.GetQuotedTableName(tableName);
            foreach (var f in def.FieldDefinitions)
            {
                if (f.IsPrimaryKey)
                    continue;
                var cname = dp.GetQuotedColumnName(ns.GetColumnName(f.FieldName));
                var n = String.Join('.', qt, cname);
                var vn = String.Join(cname, "VALUES(", ')');
                switch (f.UpdateAggregation)
                {
                    case UpdateAggregations.Add:
                        tu.Add(n, String.Join("+", n, vn));
                        break;
                    case UpdateAggregations.Sub:
                        tu.Add(n, String.Join('-', n, vn));
                        break;
                    case UpdateAggregations.Min:
                        tu.Add(n, String.Concat("LEAST(", n, ',', vn, ')'));
                        break;
                    case UpdateAggregations.Max:
                        tu.Add(n, String.Concat("GREATEST(", n, ',', vn, ')'));
                        break;
                    case UpdateAggregations.None:
                    case UpdateAggregations.Set:
                        tu.Add(n, vn);
                        break;
                    case UpdateAggregations.AddSameResetMax:
                        {
                            var cmpName = dp.GetQuotedColumnName(ns.GetColumnName(f.UpdateAggregationData[0] as String));
                            var cn = String.Join('.', qt, cmpName);
                            var cvn = String.Join(cmpName, "VALUES(", ')');
                            tu.Add(n, String.Concat("IF(", cvn, '>', cn, ',', cvn, ",IF(", cvn, '=', cn, ',', n, '+', vn, ',', vn, "))"));
                        }
                        break;
                    case UpdateAggregations.AddSameResetMin:
                        {
                            var cmpName = dp.GetQuotedColumnName(ns.GetColumnName(f.UpdateAggregationData[0] as String));
                            var cn = String.Join('.', qt, cmpName);
                            var cvn = String.Join(cmpName, "VALUES(", ')');
                            tu.Add(n, String.Concat("IF(", cvn, '<', cn, ',', cvn, ",IF(", cvn, '=', cn, ',', n, '+', vn, ',', vn, "))"));
                        }
                        break;
                    case UpdateAggregations.SetIfNewMax:
                        {
                            var cmpName = dp.GetQuotedColumnName(ns.GetColumnName(f.UpdateAggregationData[0] as String));
                            var cn = String.Join('.', qt, cmpName);
                            var cvn = String.Join(cmpName, "VALUES(", ')');
                            tu.Add(n, String.Concat("IF(", cvn, '>', cn, ',', vn, ',', n, ')'));
                        }
                        break;
                    case UpdateAggregations.SetIfNewMin:
                        {
                            var cmpName = dp.GetQuotedColumnName(ns.GetColumnName(f.UpdateAggregationData[0] as String));
                            var cn = String.Join('.', qt, cmpName);
                            var cvn = String.Join(cmpName, "VALUES(", ')');
                            tu.Add(n, String.Concat("IF(", cvn, '<', cn, ',', vn, ',', n, ')'));
                        }
                        break;

                    case UpdateAggregations.SetIfNewOrEqualMax:
                        {
                            var cmpName = dp.GetQuotedColumnName(ns.GetColumnName(f.UpdateAggregationData[0] as String));
                            var cn = String.Join('.', qt, cmpName);
                            var cvn = String.Join(cmpName, "VALUES(", ')');
                            tu.Add(n, String.Concat("IF(", cvn, ">=", cn, ',', vn, ',', n, ')'));
                        }
                        break;
                    case UpdateAggregations.SetIfNewOrEqualMin:
                        {
                            var cmpName = dp.GetQuotedColumnName(ns.GetColumnName(f.UpdateAggregationData[0] as String));
                            var cn = String.Join('.', qt, cmpName);
                            var cvn = String.Join(cmpName, "VALUES(", ')');
                            tu.Add(n, String.Concat("IF(", cvn, "<=", cn, ',', vn, ',', n, ')'));
                        }
                        break;


                }
            }
            String up;
            if (tu.Count > 0)
            {
                up = dp.ToUpdateStatement(t, CommandFlags.None, new CancellationToken()).CommandText;
                up = up.Replace("SET ", "");
            }else {
                var cname = ns.GetColumnName(def.FieldDefinitions.First().Name);
                var n = String.Join('.', qt, dp.GetQuotedColumnName(cname));
                up = String.Concat("UPDATE ", n, '=', n);
            }
            var insert = String.Concat(
                "INSERT INTO ",
                dp.GetQuotedTableName(tableName),
                " (",
                String.Join(',', def.FieldDefinitions.Select(x => dp.GetQuotedColumnName(ns.GetColumnName(x.Name)))),
                ") VALUES ");
            c = [
                insert,
                " ON DUPLICATE KEY " + up,
            ];
           
            cache.TryAdd(key, c);
            return c;
        }


        /// <summary>
        /// Total number of bulk upsert rows scheduled
        /// </summary>
        public static long RowsScheuled => Interlocked.Read(ref InternalRowsScheuled);
        /// <summary>
        /// Total number of bulk upsert rows completed
        /// </summary>
        public static long RowsCompleted => Interlocked.Read(ref InternalRowsCompleted);

        static long InternalRowsScheuled;
        static long InternalRowsCompleted;






        /// <summary>
        /// Copy a database table between databases (or tables)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Source database</param>
        /// <param name="dest">Dest database</param>
        /// <param name="sourceTableName">Optional name of the source table</param>
        /// <param name="destTableName">Optional name of the dest table</param>
        /// <param name="maxBatchSize">Number of rows per batch</param>
        /// <param name="writerCount">Number of paralell writers (can be one more when memory cache is full)</param>
        /// <param name="createDetTableIfNotExist">True to create the destination table if it doesn't exist</param>
        /// <returns>Number of rows copied</returns>
        public static async Task<long> CopyTableRows<T>(MySqlDbSimpleStack source, MySqlDbSimpleStack dest, String sourceTableName = null, String destTableName = null, int maxBatchSize = 4096, int writerCount = 8, bool createDetTableIfNotExist = true) where T : new()
        {
            var dp = MySqlConnectorDialectProvider.Instance;
            using var c = await dest.GetAsync().ConfigureAwait(false);
            sourceTableName = sourceTableName ?? ModelDefinition<T>.Definition.Name;
            destTableName = destTableName ?? sourceTableName;
            if (createDetTableIfNotExist)
                await c.CreateTableIfNotExistsAsync<T>(default, destTableName).ConfigureAwait(false);
            var src = new TypedSelectStatement<T>(dp);
            if (sourceTableName != null)
                src.From(sourceTableName);
            var selState = dp.ToSelectStatement(src.Statement, CommandFlags.Pipelined, default, 7 * 24 * 60 * 60);
            ConcurrentQueue<List<T>> queue = new ConcurrentQueue<List<T>>();
            long queueLen = 0;
            long quit = 0;
            async Task ProcessOne(List<T> data, OrmConnection cc)
            {
                await BulkUpsert(cc, data, destTableName, maxBatchSize).ConfigureAwait(false);
            }
            long rowCount = 0;
            long procCount = 0;
            async Task Processor(OrmConnection c = null)
            {
                Interlocked.Increment(ref procCount);
                using var cc = c == null ? (await dest.GetAsync().ConfigureAwait(false)) : null;
                c = c ?? cc;
                for (; ;)
                {
                    if (!queue.TryDequeue(out var batch))
                    {
                        if (Interlocked.Read(ref quit) != 0)
                            break;
                        await Task.Delay(1).ConfigureAwait(false);
                        continue;
                    }
                    Interlocked.Decrement(ref queueLen);
                    await ProcessOne(batch, c).ConfigureAwait(false);
                }
                Interlocked.Decrement(ref procCount);
            }
            int maxInMem = (writerCount + 1) << 3;
            for (int i = 0; i < writerCount; ++i)
                TaskExt.StartNewAsyncChain(() => Processor());
            using var con = await source.GetAsync().ConfigureAwait(false);
            using var reader = await con.ExecuteReaderAsync(selState).ConfigureAwait(false);
            List<T> batch = new List<T>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var row = new T();
                reader.CopyToObject(row);
                batch.Add(row);
                if (batch.Count >= maxBatchSize)
                {
                    rowCount += batch.Count;
                    if (Interlocked.Increment(ref queueLen) > maxInMem)
                    {
                        Interlocked.Decrement(ref queueLen);
                        await ProcessOne(batch, c).ConfigureAwait(false);
                    }else
                    {
                        queue.Enqueue(batch);
                    }
                    batch = new List<T>();
                }
            }
            if (batch.Count > 0)
            {
                queue.Enqueue(batch);
                rowCount += batch.Count;
            }
            Interlocked.Exchange(ref quit, 1);
            await Processor(c).ConfigureAwait(false);
            while (Interlocked.Read(ref queueLen) > 0)
                await Task.Delay(1).ConfigureAwait(false);
            return rowCount;
        }


        /// <summary>
        /// Upsert
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="con">The connection to use</param>
        /// <param name="data">The data</param>
        /// <param name="tableName">Optional table name</param>
        /// <returns></returns>
        public static async Task Upsert<T>(OrmConnection con, T data, String tableName = null)
        {
            var def = ModelDefinition<T>.Definition;
            var us = GetUpsert(def, tableName);
            var dp = MySqlConnectorDialectProvider.Instance;
            var fis = def.FieldDefinitions;
            var fl = fis.Count;
            var dyn = new DynamicParameters();
            var sb = new StringBuilder(us[0]);
            sb.Append(" (");
            int pi = 0;
            for (int j = 0; j < fl; ++j, ++pi)
            {
                var pn = "p" + pi;
                sb.Append(j == 0 ? "@" : ",@").Append(pn);
                var fi = fis[j];
                dyn.Add(pn, fi.GetValueFn(data));
            }
            sb.Append(')');
            sb.Append(us[1]);
            var cmd = sb.ToString();
            var c = new CommandDefinition(cmd, dyn, flags: CommandFlags.None);
            //                var c = new CommandDefinition(cmd, p.ToDynamicParameters(), flags: CommandFlags.Buffered);
            await con.ExecuteAsync(c).ConfigureAwait(false);
        }



        /// <summary>
        /// Bulk upsert
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="con">The connection to use</param>
        /// <param name="data">The data</param>
        /// <param name="tableName">Optional table name</param>
        /// <param name="maxBatchSize">Number of rows per query</param>
        /// <returns></returns>
        public static async Task BulkUpsert<T>(OrmConnection con, IReadOnlyList<T> data, String tableName = null, int maxBatchSize = 4096)
        {
            var def = ModelDefinition<T>.Definition;
            var us = GetUpsert(def, tableName);
            var dp = MySqlConnectorDialectProvider.Instance;
            var fis = def.FieldDefinitions;
            var fl = fis.Count;
            var l = data.Count;
            Interlocked.Add(ref InternalRowsScheuled, l);
            for (int i = 0; i < l; )
            {
                var count = l - i;
                if (count > maxBatchSize)
                    count = maxBatchSize;
                var cc = count;
//                var p = new StatementParameters();
                var dyn = new DynamicParameters();

                var sb = new StringBuilder(us[0]);
                int pi = 0;
                while (count > 0)
                {
                    var dd = data[i];
                    sb.Append(" (");
                    for (int j = 0; j < fl; ++ j, ++ pi)
                    {
                        var pn = "p" + pi;
                        sb.Append(j == 0 ? "@" :",@").Append(pn);
                        var fi = fis[j];
                        dyn.Add(pn, fi.GetValueFn(dd));
//                        p.Add(new StatementParameter(pn, fi.FieldType, fi.GetValueFn(dd)));
                    }
                    --count;
                    ++i;
                    sb.Append(count == 0 ? ")" : "),");
                }
                sb.Append(us[1]);
                var cmd = sb.ToString();
                var c = new CommandDefinition(cmd, dyn, flags: CommandFlags.None);
//                var c = new CommandDefinition(cmd, p.ToDynamicParameters(), flags: CommandFlags.Buffered);
                await con.ExecuteAsync(c).ConfigureAwait(false);
                Interlocked.Add(ref InternalRowsCompleted, cc);
            }
        }







        public MySqlDbSimpleStack(MySqlDbParams p) : base(p, MySqlConnectorDialectProvider.Instance, typeof(MySqlDbParams))
        {
            P = p;
            DP = base.DP as MySqlConnectorDialectProvider;
        }

        public new readonly MySqlDbParams P;
        public new readonly MySqlConnectorDialectProvider DP;

        public override async Task CreateSchemaIfNotExist(String schema)
        {
            //  Create db if it doesn't exist
            var p = P;
            if (p.ReadOnly)
                return;
            using (var db = new MySqlConnection(p.BuildConnectionString(false)))
            {
                await db.OpenAsync().ConfigureAwait(false);
                if (db.State != ConnectionState.Open)
                    throw new Exception("Failed to connect!");
                var cmd = db.CreateCommand();
                cmd.CommandText = "create schema if not exists " + DP.GetQuotedName(schema) + " character set=" + p.CharSet + " collate=" + p.CharSetCollate + ";";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        String GetKey(FieldDefinition f)
        {
            if (f.IsPrimaryKey)
                return " PRIMARY KEY";
            if (f.IsUnique)
                return " UNIQUE";
            return "";
        }

        String GetKey(DbDataReader r)
        {
            var x = r.GetString(3);
            if (String.IsNullOrEmpty(x))
                return "";
            if (String.Equals(x, "PRI", StringComparison.OrdinalIgnoreCase))
                return " PRIMARY KEY";
            if (String.Equals(x, "UNI", StringComparison.OrdinalIgnoreCase))
                return " UNIQUE";
            return "";
        }

        String GetTypeCreate(FieldDefinition f)
        {
            String suffix = "";
            if (!f.IsNullable)
                suffix += " NOT NULL";
            if (f.DefaultValue != null)
                suffix += " DEFAULT @p0";
            if (f.AutoIncrement)
                suffix += " AUTO_INCREMENT";
            if (f.FieldType == typeof(String))
            {
                var col = GetCollate(f);
                suffix += " COLLATE " + col;
            }
            suffix += GetKey(f);
            var dp = DP;
            var typeName = dp.GetColumnTypeDefinition(f.FieldType, f.FieldName, f.FieldLength);
            if (NameMap.TryGetValue(typeName, out var x))
                typeName = x;
            return typeName + suffix;
        }

        String GetCollate(FieldDefinition f)
        {
            if (f.FieldType != typeof(String))
                return null;
            var p = P;
            String col = null;
            if (f.PropertyInfo.GetCustomAttributes<AsciiAttribute>(true).FirstOrDefault() != null)
            {
                col = p.CharSetAsciiCollate;
                if (f.PropertyInfo.GetCustomAttributes<CaseSensitiveAttribute>(true).FirstOrDefault() != null)
                    col = p.CharSetAsciiCollate;
                if (f.PropertyInfo.GetCustomAttributes<CaseInsensitiveAttribute>(true).FirstOrDefault() != null)
                    col = p.CharSetAsciiCollateCI;
            }
            else
            {
                col = p.CharSetCollate;
                if (f.PropertyInfo.GetCustomAttributes<CaseSensitiveAttribute>(true).FirstOrDefault() != null)
                    col = p.CharSetCollateC;
                if (f.PropertyInfo.GetCustomAttributes<CaseInsensitiveAttribute>(true).FirstOrDefault() != null)
                    col = p.CharSetCollateCI;
            }
            return col;
        }

        static readonly Dictionary<String, String> NameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BOOLEAN", "tinyint" }
        };


        String GetTypeCreate(DbDataReader r, FieldDefinition def, String collate)
        {
            String suffix = "";
            if (String.Equals(r.GetString(2), "NO", StringComparison.OrdinalIgnoreCase))
                suffix += " NOT NULL";
            if (!r.IsDBNull(4))
                suffix += " DEFAULT @p0";
            var extra = new HashSet<String>(r.GetString(5).Split(',').Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
            if (extra.Contains("auto_increment"))
                suffix += " AUTO_INCREMENT";
            if (def.FieldType == typeof(String))
                suffix += " COLLATE " + collate;
            suffix += GetKey(r);
            var typeName = r.GetString(1);
            var tp = typeName.Split('(');
            var baseName = tp[0];
            if (tp.Length > 1)
                if (baseName.EndsWith("Int", StringComparison.OrdinalIgnoreCase))
                    typeName = baseName + typeName.Substring(typeName.IndexOf(')') + 1);
            return typeName + suffix;
        }


        static int FindMatching(String t, int start = 0, Char increment = '(', Char decrement = ')', int startCount = 0)
        {
            var l = t.Length;
            for (int i = start; i < l; ++ i)
            {
                var c = t[i];
                if (c == increment)
                    ++ startCount;
                if (c != decrement)
                    continue;
                --startCount;
                if (startCount == 0)
                    return i;
            }
            return -1;
        }


        sealed class MyProv : MySqlConnectorDialectProvider
        {
            public MyProv(ModelDefinition def, Func<FieldDefinition, String> getCol)
            {
                var f = Cols;
                foreach (var x in def.FieldDefinitions)
                {
                    if (x.FieldType != typeof(String))
                        continue;
                    var col = getCol(x);
                    if (col == null)
                        continue;
                    f[x.FieldName] = col;
                }
            }
            readonly Dictionary<String, String> Cols = new Dictionary<string, string>(StringComparer.Ordinal);

            public override string GetColumnDefinition(string fieldName, Type fieldType, bool isPrimaryKey, bool autoIncrement, bool isNullable, int? fieldLength, int? scale, object defaultValue)
            {
                var d = base.GetColumnDefinition(fieldName, fieldType, isPrimaryKey, autoIncrement, isNullable, fieldLength, scale, defaultValue);
                if (Cols.TryGetValue(fieldName, out var col))
                {
                    d += " COLLATE " + col;
                }
                return d;
            }
        }


        sealed class MyOrmConnection : OrmConnection
        {
            public MyOrmConnection(OrmConnection con, ModelDefinition def, Func<FieldDefinition, String> getCol) : base(con.DbConnection, new MyProv(def, getCol), con._loggerFactory)
            {
            }

            protected override void Dispose(bool disposing)
            {
            }
        }

        public override async Task InitTable<T>(OrmConnection con)
        {
            if (P.ReadOnly)
                return;
            var modelDef = ModelDefinition<T>.Definition;
            var m = new MyOrmConnection(con, modelDef, GetCollate);
            await m.CreateTableIfNotExistsAsync<T>().ConfigureAwait(false);
            /*
                        var cancellationToken = new CancellationToken();
                        var dialectProvider = DP;
                        var tableName = dialectProvider.NamingStrategy.GetTableName(modelDef.ModelName);
                        var tableExists = await con.TableExistsAsync(tableName, modelDef.Schema, cancellationToken);
                        if (!tableExists)
                        {
                            var myProv = new MyProv(modelDef);
                            await con.ExecuteAsync(myProv.ToCreateTableStatement(modelDef)).ConfigureAwait(false);

                            var sqlIndexes = dialectProvider.ToCreateIndexStatements(modelDef);
                            foreach (var sqlIndex in sqlIndexes)
                            {
                                await con.ExecuteAsync(sqlIndex, cancellationToken).ConfigureAwait(false);
                            }

                            var sequenceList = dialectProvider.SequenceList(modelDef);
                            if (sequenceList.Count > 0)
                            {
                                foreach (var seq in sequenceList)
                                {
                                    if (dialectProvider.DoesSequenceExist(con, seq) == false)
                                    {
                                        var seqSql = dialectProvider.ToCreateSequenceStatement(modelDef, seq);
                                        await con.ExecuteAsync(seqSql, cancellationToken).ConfigureAwait(false);
                                    }
                                }
                            }
                            else
                            {
                                var sequences = dialectProvider.ToCreateSequenceStatements(modelDef);
                                foreach (var seq in sequences)
                                {
                                    await con.ExecuteAsync(seq, cancellationToken).ConfigureAwait(false);
                                }
                            }
                        }
                        */
            await ValidateTable<T>(con).ConfigureAwait(false);
        }

        public override async Task Init()
        {
            await base.Init().ConfigureAwait(false);
            if (!P.ReadOnly)
            {
                using var c = await GetAsync().ConfigureAwait(false);
                Partitions = await CanPartition(c).ConfigureAwait(false) ? P.Partitions : null;
            }
        }

        static readonly long[] PrimeMod = new long[]
        {
            1200000041, 1200000059, 1200000071, 1200000073, 1200000079, 1200000101, 1200000121, 1200000133, 1200000163, 1200000203, 1200000227, 1200000281, 1200000299, 1200000311, 1200000313, 1200000337, 1200000343, 1200000349, 1200000379, 1200000383, 1200000419, 1200000433, 1200000443, 1200000463, 1200000491, 1200000517, 1200000559, 1200000589, 1200000611, 1200000623, 1200000631, 1200000691, 1200000701, 1200000707, 1200000797, 1200000799, 1200000817, 1200000821, 1200000839, 1200000847, 1200000877, 1200000881, 1200000899, 1200000911, 1200000961, 1200001001, 1200001009, 1200001013, 1200001051, 1200001067, 1200001087, 1200001091, 1200001093, 1200001097, 1200001123, 1200001141, 1200001157, 1200001163, 1200001171, 1200001189, 1200001193, 1200001247, 1200001267, 1200001277
        };

        static readonly long[] PrimeMul = new long[]
        {
            1500000001, 1500000041, 1500000043, 1500000059, 1500000077, 1500000079, 1500000101, 1500000107, 1500000113, 1500000167, 1500000233, 1500000283, 1500000301, 1500000373, 1500000377, 1500000409, 1500000419, 1500000427, 1500000449, 1500000473, 1500000511, 1500000527, 1500000529, 1500000563, 1500000577, 1500000587, 1500000613, 1500000617, 1500000701, 1500000707, 1500000713, 1500000721, 1500000739, 1500000743, 1500000767, 1500000773, 1500000779, 1500000787, 1500000823, 1500000829, 1500000839, 1500000851, 1500000857, 1500000871, 1500000877, 1500000893, 1500000917, 1500000973, 1500001003, 1500001033, 1500001043, 1500001049, 1500001117, 1500001213, 1500001231, 1500001241, 1500001271, 1500001291, 1500001319, 1500001333, 1500001337, 1500001343, 1500001367, 1500001369
        };

        static readonly long[] PrimeAdd = new long[]
        {
            100003, 100019, 100043, 100049, 100057, 100069, 100103, 100109, 100129, 100151, 100153, 100169, 100183, 100189, 100193, 100207, 100213, 100237, 100267, 100271, 100279, 100291, 100297, 100313, 100333, 100343, 100357, 100361, 100363, 100379, 100391, 100393, 100403, 100411, 100417, 100447, 100459, 100469, 100483, 100493, 100501, 100511, 100517, 100519, 100523, 100537, 100547, 100549, 100559, 100591, 100609, 100613, 100621, 100649, 100669, 100673, 100693, 100699, 100703, 100733, 100741, 100747, 100769, 100787
        };


        /// <summary>
        /// Check if a MySql instance supports partitioning
        /// </summary>
        /// <param name="con"></param>
        /// <returns></returns>
        static async Task<bool> CanPartition(OrmConnection con)
        {
            String version = "0.0.0.0";
            using (var r = await con.ReaderCommandAsync("SELECT VERSION()").ConfigureAwait(false))
            {
                if (await r.ReadAsync())
                    version = r.GetFieldValue<String>(0);
            }
            return int.Parse(version.Split('.')[0]) >= 8;
        }


        public override async Task<bool> Partition(OrmConnection con, Type t, String[] partitionFolders, String tableName = null)
        {
            var pc = partitionFolders?.Length ?? 0;
            if (pc < 2)
                return false;



            var dp = DP;
            var model = ModelDefinition.Get(t, tableName);
            tableName = dp.NamingStrategy.GetTableName(model.ModelName);
            var tableNameQ = dp.GetQuotedColumnName(tableName);
            String createSql;
            using (var r = await con.ReaderCommandAsync("SHOW CREATE TABLE " + tableNameQ))
            {
                if (!await r.ReadAsync().ConfigureAwait(false))
                    return false;
                createSql = r.GetString(1);
            }
            var pStart = createSql.IndexOf("/*");
            if (pStart > 0)
            {
                if (createSql.IndexOf(" PARTITION ", pStart) > 0)
                    return true;
            }
            StringBuilder sb = new StringBuilder("ALTER TABLE ");
            sb.Append(tableNameQ);
            bool havePart = false;
            if (!havePart)
            {
                var keys = t.GetCustomAttributes<PartitionByHashAttribute>(false).FirstOrDefault()?.ColumnNames ?? Array.Empty<string>();
                if (keys.Length > 0)
                {
                    havePart = true;
                    var hd = MD5.HashData(Encoding.UTF8.GetBytes(tableName));
                    var pmod = PrimeMod;
                    var pmodl = pmod.Length;
                    var pmods = (hd[1] & 3) + 1;
                    long pmodi = hd[2];
                    if (((hd[1] & 0x80)) != 0)
                        pmods = pmodl - pmods;

                    var pmul = PrimeMul;
                    var pmull = pmul.Length;
                    var pmuls = (hd[3] & 3) + 1;
                    long pmuli = hd[4];
                    if (((hd[3] & 0x80)) != 0)
                        pmuls = pmull - pmuls;

                    var padd = PrimeAdd;
                    sb.Append(" PARTITION BY HASH(").Append(padd[hd[0] % padd.Length]);
                    foreach (var x in keys)
                    {
                        pmodi %= pmodl;
                        pmuli %= pmull;
                        sb.Append("+(").Append(dp.GetQuotedColumnName(x)).Append('%').Append(pmod[pmodi]).Append(")*").Append(pmul[pmuli]);
                        pmodi += pmods;
                        pmuli += pmuls;
                    }
                }
            }
            if (!havePart)
            {
                var partA = t.GetCustomAttributes<PartitionByKeyAttribute>(false).FirstOrDefault();
                if (partA == null)
                    return true;
                sb.Append(" PARTITION BY KEY(");
                var keys = partA.ColumnNames ?? Array.Empty<string>();
                sb.Append(String.Join(',', keys.Select(x => dp.GetQuotedColumnName(x))));
            }
            sb.Append(") PARTITIONS ").Append(pc).Append(" (");
            int index = 0;
            foreach (var p in partitionFolders)
            {
                sb.Append("PARTITION p").Append(index);
                if (!String.IsNullOrEmpty(p))
                {
                    sb.Append(" DATA DIRECTORY='");
                    sb.Append(p);
                    sb.Append('\'');
                }
                ++index;
                if (index != pc)
                    sb.Append(',');
            }
            sb.Append(')');
            var cmd = sb.ToString();
            await con.ExecuteAsync(cmd).ConfigureAwait(false);
            return true;
        }

        
        public override async Task<bool> RemoveIndices(OrmConnection con, Type t, String tableName)
        {
            var dp = DP;
            var model = ModelDefinition.Get(t, tableName);
            tableName = dp.NamingStrategy.GetTableName(model.ModelName);
            var tableNameQ = dp.GetQuotedTableName(tableName);
            //  Full text index
            var remove = new HashSet<string>(StringComparer.Ordinal);
            await con.OnResultAsync<IndexInfo>(x =>
            {
                if ((x.NotUnique) && (x.KeyName != "PRIMARY"))
                    remove.Add(x.KeyName);
                return true;
            }, "SHOW INDEX FROM " + tableNameQ).ConfigureAwait(false);
            foreach (var x in remove)
            {
                var cmd = "DROP INDEX " + dp.GetQuotedColumnName(x) + " ON " + tableNameQ;
                await con.ExecuteAsync(cmd).ConfigureAwait(false);
            }
            return true;
        }

        public override async Task<bool> ValidateTable(OrmConnection con, Type t, String tableName)
        {
            if (P.ReadOnly)
                return true;

            var dp = DP;
            var model = ModelDefinition.Get(t, tableName);
            var ns = dp.NamingStrategy;
            tableName = ns.GetTableName(model.ModelName);
            var tableNameQ = dp.GetQuotedTableName(tableName);

            Dictionary<String, FieldDefinition> fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
            Dictionary<String, FieldDefinition> props = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in model.FieldDefinitions)
            {
                var n = ns.GetColumnName(x.Name);
                fields[n] = x;
                props[x.FieldName] = x;
            }
            char sep = tableNameQ[0];

            //  Get info
            Dictionary<String, String> collates = new Dictionary<string, String>(StringComparer.OrdinalIgnoreCase);
            String createSql;
            using (var r = await con.ReaderCommandAsync("SHOW CREATE TABLE " + tableNameQ))
            {
                if (!await r.ReadAsync().ConfigureAwait(false))
                    return false;
                createSql = r.GetString(1);
            }
            createSql = createSql.Replace('"', sep);
            var first = createSql.IndexOf('(');
            var end = FindMatching(createSql, first);
            createSql = createSql.Substring(first + 1, end - first - 2).Trim();
            var columns = createSql.Split(',');
            foreach (var col in columns)
            {
                var ccol = col.Trim();
                if (ccol[0] != sep)
                    continue;

                var icol = ccol.IndexOf(sep, 1);
                var colName = ccol.Substring(1, icol).Trim(sep);
                ccol = ccol.Substring(icol + 1).TrimStart();
                String readOne()
                {
                    icol = ccol.IndexOf(' ');
                    if (icol < 0)
                    {
                        var r = ccol;
                        ccol = "";
                        return r;
                    }
                    var ret = ccol.Substring(0, icol).Trim();
                    ccol = ccol.Substring(icol + 1).TrimStart();
                    return ret;
                }
                var type = readOne();
#pragma warning disable CS0219
                bool notNull = false;
                bool aa = false;
#pragma warning restore CS0219
                String collate = null;
                String def = null;
                while (ccol.Length > 0)
                {
                    var k = readOne().FastToLower();
                    if (k == "collate")
                    {
                        collate = readOne();
                        continue;
                    }
                    if (k == "default")
                    {
                        def = readOne();
                        continue;
                    }
                    if (k == "not")
                    {
                        if (readOne().FastToLower() == "null")
                            notNull = true;
                        continue;
                    }
                    if (k == "auto_increment")
                    {
                        aa = true;
                        continue;
                    }
                }
                if (collate != null)
                    collates[colName] = collate;
            }


            Dictionary<String, int> orders = new Dictionary<string, int>(StringComparer.Ordinal);
            int index = -1;
            foreach (var x in model.FieldDefinitions)
            {
                var n = ns.GetColumnName(x.Name);
                fields[n] = x;
                orders[n] = ++index;
            }
            HashSet<String> existingCols = new HashSet<string>(StringComparer.Ordinal);

            //  Drop columns
            List<String> drop = new List<string>();
            using (var r = await con.ReaderCommandAsync("DESC " + tableNameQ))
            {
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var name = r.GetString(0);
                    existingCols.Add(name);
                    if (!fields.ContainsKey(name))
                    {
                        var fieldName = dp.GetQuotedColumnName(name);
                        var cmd = "ALTER TABLE " + tableNameQ + " DROP COLUMN " + fieldName;
                        drop.Add(cmd);
                    }
                }
            }
            foreach (var cmd in drop)
                await con.CommandAsync(cmd).ConfigureAwait(false);

            //  Add columns
            foreach (var x in fields)
            {
                var f = x.Value;
                if (existingCols.Contains(f.FieldName))
                    continue;
                var typeName = GetTypeCreate(f);
                var fieldName = dp.GetQuotedColumnName(f.FieldName);
                var cmd = "ALTER TABLE " + tableNameQ + " ADD COLUMN " + fieldName + " " + typeName;
                await con.CommandAsync(cmd,
                    f.DefaultValue
                    ).ConfigureAwait(false);
            }

            //  Fix column order

            bool fixColIndex = false;
            using (var r = await con.ReaderCommandAsync("DESC " + tableNameQ))
            {
                index = 0;
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var name = r.GetString(0);
                    fixColIndex = orders[name] != index;
                    if (fixColIndex)
                        break;
                    ++index;
                }
            }
            if (fixColIndex)
            {
                Dictionary<String, String> create = new Dictionary<string, string>(StringComparer.Ordinal);
                using (var r = await con.ReaderCommandAsync("DESC " + tableNameQ))
                {
                    index = 0;
                    while (await r.ReadAsync().ConfigureAwait(false))
                    {
                        var name = r.GetString(0);
                        orders[name] = index;
                        ++index;
                        var f = fields[name];
                        String collate = null;
                        if (f.FieldType == typeof(String))
                        {
                            collates.TryGetValue(name, out collate);
                            if (collate == null)
                                collate = GetCollate(f);
                        }
                        var typeNameExisting = GetTypeCreate(r, f, collate);
                        create.Add(name, typeNameExisting);
                    }
                }
                String prev = null;
                index = 0;
                foreach (var fx in model.FieldDefinitions)
                {
                    var x = ns.GetColumnName(fx.Name);
                    if (orders[x] != index)
                    {
                        if (!fx.IsPrimaryKey)
                        {
                            var fieldName = dp.GetQuotedColumnName(x);
                            var cmd = "ALTER TABLE " + tableNameQ + " MODIFY COLUMN " + fieldName + " " + create[x] + (prev == null ? " FIRST" : " AFTER " + dp.GetQuotedColumnName(prev));
                            await con.CommandAsync(cmd, fx.DefaultValue).ConfigureAwait(false);
                        }
                    }
                    prev = x;
                    ++index;
                }
            }

            //  Get existing indices
            Dictionary<String, ExistingIndex> existingIndexes = new Dictionary<String, ExistingIndex>(StringComparer.Ordinal);
            var fullTexts = new List<String, IndexInfo>(StringComparer.Ordinal);
            await con.OnResultAsync<IndexInfo>(x =>
            {
                if (x.IndexType == "FULLTEXT")
                {
                    fullTexts.Add(x.KeyName, x);
                    return true;
                }
                if (!existingIndexes.TryGetValue(x.KeyName, out var ei))
                {
                    ei = new ExistingIndex
                    {
                        Name = x.KeyName,
                        Type = x.IndexType,
                        Unique = !x.NotUnique,
                    };
                    existingIndexes[x.KeyName] = ei;
                }
                Array.Resize(ref ei.Columns, x.SequenceIndex);
                ei.Columns[x.SequenceIndex - 1] = x.ColumnName;
                return true;
            }, "SHOW INDEX FROM " + tableNameQ).ConfigureAwait(false);
            Dictionary<String, ExistingIndex> existingSingle = new Dictionary<String, ExistingIndex>(StringComparer.Ordinal);
            foreach (var i in existingIndexes.ToList())
            {
                if (i.Value.Columns.Length == 1)
                {
                    existingSingle.TryAdd(i.Value.Columns[0], i.Value);
                    existingIndexes.TryRemove(i.Key, out var ei);
                }
            }


            //  Change columns

            List<Tuple<String, Object>> alts = new List<Tuple<string, object>>();
            using (var r = await con.ReaderCommandAsync("DESC " + tableNameQ))
            {
                while (await r.ReadAsync().ConfigureAwait(false))
                {
                    var name = r.GetString(0);
                    if (fields.TryGetValue(name, out var f))
                    {
                        fields.Remove(name);
                        String collate = null;
                        if (f.FieldType == typeof(String))
                            collates.TryGetValue(name, out collate);
                        var typeName = GetTypeCreate(f);
                        var typeNameExisting = GetTypeCreate(r, f, collate);
                        if (!String.Equals(typeNameExisting, typeName, StringComparison.OrdinalIgnoreCase))
                        {
                            var fieldName = dp.GetQuotedColumnName(f.FieldName);
                            var cmd = "ALTER TABLE " + tableNameQ + " MODIFY COLUMN " + fieldName + " " + typeName;
                            if (typeNameExisting.Contains(" PRIMARY KEY"))
                                cmd = cmd.Replace(" PRIMARY KEY", "");
                            if (typeNameExisting.Contains(" UNIQUE"))
                                cmd = cmd.Replace(" UNIQUE", "");
                            /*
                                if (typeName.Contains(" PRIMARY KEY"))
                                if (existingSingle.TryGetValue(name, out var ix))
                                    if (ix.Name != "PRIMARY")
                                        alts.Add(Tuple.Create("DROP INDEX " + dp.GetQuotedName(ix.Name) + " ON " + tableNameQ, (Object)null));
                            */
                            alts.Add(Tuple.Create(cmd, f.DefaultValue));
                        }
                    }
                }
            }
            //  Change columns
            foreach (var x in alts)
                await con.CommandAsync(x.Item1, x.Item2).ConfigureAwait(false);



            //  Full text index
            foreach (var fti in t.GetCustomAttributes<FullTextSearchIndexAttribute>(true))
            {
                var indexProps = fti.Props;
                if (indexProps == null)
                    throw new Exception(t.FullName + " attribute value " + nameof(FullTextSearchIndexAttribute) + "." + nameof(fti.Props) + " may not be null!");
                var pl = indexProps.Length;
                if (pl <= 0)
                    throw new Exception(t.FullName + " attribute value " + nameof(FullTextSearchIndexAttribute) + "." + nameof(fti.Props) + " may not be empty!");
                String col = null;
                for (int i = 0; i < pl; ++ i)
                {
                    var p = indexProps[i];
                    if (!props.TryGetValue(p, out var fi))
                        throw new Exception(t.FullName + " attribute value " + nameof(FullTextSearchIndexAttribute) + "." + nameof(fti.Props) + " have an unknown property name " + p.ToQuoted() + "!");
                    if (fi.FieldType != typeof(String))
                        throw new Exception(t.FullName + " attribute value " + nameof(FullTextSearchIndexAttribute) + "." + nameof(fti.Props) + " property " + p.ToQuoted() + " is not a string, only strings may be used for full text search!");
                    var name = ns.GetColumnName(fi.Name);
                    collates.TryGetValue(name, out var collate);
                    if (i == 0)
                    {
                        col = collate;
                    }else
                    {
                        if (collate != col)
                            throw new Exception(t.FullName + " attribute value " + nameof(FullTextSearchIndexAttribute) + "." + nameof(fti.Props) + " property " + p.ToQuoted() + " doesn't have the collation " + col.ToQuoted() + ", all properties must have the same collation!");
                    }
                    indexProps[i] = name;
                }
                var filterNameQ = dp.GetQuotedName(fti.Name);
                if (fullTexts.TryRemove(fti.Name, out var fullText))
                {
                    fullText.Sort((a, b) => a.SequenceIndex - b.SequenceIndex);
                    if (indexProps.SequenceEqual(fullText.Select(x => x.ColumnName)))
                        continue;
                    //  Remove changed index
                    await con.CommandAsync("ALTER TABLE " + tableNameQ + " DROP INDEX " + filterNameQ).ConfigureAwait(false);
                }
            //  Insert new index
                await con.CommandAsync("ALTER TABLE " + tableNameQ + " ADD FULLTEXT INDEX " + filterNameQ + " (" + String.Join(',', indexProps.Select(x => dp.GetQuotedColumnName(x))) + ")").ConfigureAwait(false);
            }
            //  Delete non existent columns
            foreach (var x in fullTexts.Keys)
                await con.CommandAsync("ALTER TABLE " + tableNameQ + " DROP INDEX " + dp.GetQuotedName(x)).ConfigureAwait(false);





            //  Add field indices
            foreach (var fd in model.FieldDefinitions)
            {
                if (!fd.IsIndexed)
                    continue;
                var colName = ns.GetColumnName(fd.Name);
                if (existingSingle.TryRemove(colName, out var ii))
                    continue;
                var inname = dp.GetQuotedColumnName(String.Join('_', "idx", tableName, colName));
                var cname = dp.GetQuotedColumnName(colName);
                var cmd = String.Concat(fd.IsUnique ? "CREATE UNIQUE INDEX " : "CREATE INDEX ", inname, " ON ", tableNameQ, " (", cname, ')');
                await con.CommandAsync(cmd).ConfigureAwait(false);
            }

            //  TODO: Remove field indices
            foreach (var x in existingSingle)
            {
                if (x.Value.Name == "PRIMARY")
                    continue;
                var y = x;
            }

            //  TODO: Add composite indices
            foreach (var x in model.CompositeIndexes)
            {
                var y = x;
            }

            //  TODO: Remove composite indices
            foreach (var x in existingIndexes)
            {
                if (x.Value.Name == "PRIMARY")
                    continue;
                var y = x;
            }
            
            return await Partition(con, t, Partitions, tableName).ConfigureAwait(false);
        }

        sealed class ExistingIndex
        {
            public String Name;
            public String Type;
            public bool Unique;
            public String[] Columns;
        }

        sealed class IndexInfo
        {
            public String Table { get; set; }
            public bool NotUnique { get; set; }
            public String KeyName { get; set; }
            public int SequenceIndex  { get; set; }
            public String ColumnName { get; set; }
            public String Collation{ get; set; }
            public int Cardinality { get; set; }
            public String SubPart{ get; set; }
            public String Packed { get; set; }
            public String Null { get; set; }
            public String IndexType { get; set; }
            public String Comment { get; set; }
            public String Index_Comment { get; set; }
        }


    }





}
