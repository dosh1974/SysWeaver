using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SimpleStack.Orm;
using SimpleStack.Orm.Expressions.Statements.Typed;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{




    internal static class DbCacheReadReader
    {
        static readonly Type ReaderType = typeof(DbDataReader);
        static readonly Type[] GetParams = [typeof(int)];
        static readonly MethodInfo GetGeneric = ReaderType.GetMethod(nameof(DbDataReader.GetFieldValue), BindingFlags.Instance | BindingFlags.Public, GetParams);

        static DbCacheReadReader()
        {
            var rtype = ReaderType;
            var reader = Expression.Parameter(typeof(DbDataReader), "r");
            Reader = reader;
            var offset = Expression.Parameter(typeof(int), "o");
            Offset = offset;

            var getters = Getters;
            var getParams = GetParams;
            var IsDBNull = rtype.GetMethod(nameof(DbDataReader.IsDBNull), BindingFlags.Instance | BindingFlags.Public, getParams);
            IsDBNullMethod = IsDBNull;
            var GetBoolean = rtype.GetMethod(nameof(DbDataReader.GetBoolean), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetByte = rtype.GetMethod(nameof(DbDataReader.GetByte), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetChar = rtype.GetMethod(nameof(DbDataReader.GetChar), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetDateTime = rtype.GetMethod(nameof(DbDataReader.GetDateTime), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetDecimal = rtype.GetMethod(nameof(DbDataReader.GetDecimal), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetDouble = rtype.GetMethod(nameof(DbDataReader.GetDouble), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetFloat = rtype.GetMethod(nameof(DbDataReader.GetFloat), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetGuid = rtype.GetMethod(nameof(DbDataReader.GetGuid), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetInt16 = rtype.GetMethod(nameof(DbDataReader.GetInt16), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetInt32 = rtype.GetMethod(nameof(DbDataReader.GetInt32), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetInt64 = rtype.GetMethod(nameof(DbDataReader.GetInt64), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetString = rtype.GetMethod(nameof(DbDataReader.GetString), BindingFlags.Instance | BindingFlags.Public, getParams);
            var GetBlob = GetGeneric.MakeGenericMethod(typeof(Byte[]));

            getters.TryAdd(typeof(Boolean), index => Expression.Call(reader, GetBoolean, GetIndex(index)));
            getters.TryAdd(typeof(Byte), index => Expression.Call(reader, GetByte, GetIndex(index)));
            getters.TryAdd(typeof(Char), index => Expression.Call(reader, GetChar, GetIndex(index)));
            getters.TryAdd(typeof(DateTime), index => Expression.Call(reader, GetDateTime, GetIndex(index)));
            getters.TryAdd(typeof(Decimal), index => Expression.Call(reader, GetDecimal, GetIndex(index)));
            getters.TryAdd(typeof(Double), index => Expression.Call(reader, GetDouble, GetIndex(index)));
            getters.TryAdd(typeof(Single), index => Expression.Call(reader, GetFloat, GetIndex(index)));
            getters.TryAdd(typeof(Guid), index => Expression.Call(reader, GetGuid, GetIndex(index)));
            getters.TryAdd(typeof(Int16), index => Expression.Call(reader, GetInt16, GetIndex(index)));
            getters.TryAdd(typeof(Int32), index => Expression.Call(reader, GetInt32, GetIndex(index)));
            getters.TryAdd(typeof(Int64), index => Expression.Call(reader, GetInt64, GetIndex(index)));
            getters.TryAdd(typeof(SByte), index => Expression.Convert(Expression.Call(reader, GetByte, GetIndex(index)), typeof(SByte)));
            getters.TryAdd(typeof(UInt16), index => Expression.Convert(Expression.Call(reader, GetInt16, GetIndex(index)), typeof(UInt16)));
            getters.TryAdd(typeof(UInt32), index => Expression.Convert(Expression.Call(reader, GetInt32, GetIndex(index)), typeof(UInt32)));
            getters.TryAdd(typeof(UInt64), index => Expression.Convert(Expression.Call(reader, GetInt64, GetIndex(index)), typeof(UInt64)));

            getters.TryAdd(typeof(String), index => Expression.Call(reader, GetString, GetIndex(index)));
            getters.TryAdd(typeof(Byte[]), index => Expression.Call(reader, GetBlob, GetIndex(index)));

            var gettersOffset = GettersOffset;
            gettersOffset.TryAdd(typeof(Boolean), index => Expression.Call(reader, GetBoolean, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Byte), index => Expression.Call(reader, GetByte, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Char), index => Expression.Call(reader, GetChar, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(DateTime), index => Expression.Call(reader, GetDateTime, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Decimal), index => Expression.Call(reader, GetDecimal, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Double), index => Expression.Call(reader, GetDouble, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Single), index => Expression.Call(reader, GetFloat, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Guid), index => Expression.Call(reader, GetGuid, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Int16), index => Expression.Call(reader, GetInt16, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Int32), index => Expression.Call(reader, GetInt32, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(Int64), index => Expression.Call(reader, GetInt64, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(String), index => Expression.Call(reader, GetString, Expression.Add(offset, GetIndex(index))));
            gettersOffset.TryAdd(typeof(SByte), index => Expression.Convert(Expression.Call(reader, GetByte, Expression.Add(offset, GetIndex(index))), typeof(SByte)));
            gettersOffset.TryAdd(typeof(UInt16), index => Expression.Convert(Expression.Call(reader, GetInt16, Expression.Add(offset, GetIndex(index))), typeof(UInt16)));
            gettersOffset.TryAdd(typeof(UInt32), index => Expression.Convert(Expression.Call(reader, GetInt32, Expression.Add(offset, GetIndex(index))), typeof(UInt32)));
            gettersOffset.TryAdd(typeof(UInt64), index => Expression.Convert(Expression.Call(reader, GetInt64, Expression.Add(offset, GetIndex(index))), typeof(UInt64)));
            gettersOffset.TryAdd(typeof(Byte[]), index => Expression.Call(reader, GetBlob, Expression.Add(offset, GetIndex(index))));

            var indicies = Enumerable.Range(0, 32).Select(x => Expression.Constant(x)).ToArray();
            Indicies = indicies;
            IsNulls = Enumerable.Range(0, 32).Select(x => Expression.Call(reader, IsDBNull, indicies[x])).ToArray();
            IsNullOffsets = Enumerable.Range(0, 32).Select(x => Expression.Call(reader, IsDBNull, Expression.Add(offset, indicies[x]))).ToArray();
        }

        static readonly MethodInfo IsDBNullMethod;

        static Expression GetIndex(int index) => index < 32 ? Indicies[index] : Expression.Constant(index);

        public static Expression IsNull(int index) => index < 32 ? IsNulls[index] : Expression.Call(Reader, IsDBNullMethod, Expression.Constant(index));

        public static Expression IsNullOffset(int index) => index < 32 ? IsNullOffsets[index] : Expression.Call(Reader, IsDBNullMethod, Expression.Add(Offset, Expression.Constant(index)));

        static readonly Expression[] Indicies;

        static readonly Expression[] IsNulls;
        static readonly Expression[] IsNullOffsets;

        public static readonly ParameterExpression Reader;
        public static readonly ParameterExpression Offset;

        public static readonly Expression Null = Expression.Constant(null);

        static readonly ConcurrentDictionary<Type, Func<int, Expression>> Getters = new ConcurrentDictionary<Type, Func<int, Expression>>();

        static readonly ConcurrentDictionary<Type, Func<int, Expression>> GettersOffset = new ConcurrentDictionary<Type, Func<int, Expression>>();


        public static Expression Get(Type type, int index)
        {
            var g = Getters;
            if (g.TryGetValue(type, out var f))
                return f(index);
            var m = GetGeneric.MakeGenericMethod(type);
            f = i => Expression.Call(Reader, m, GetIndex(i));
            g.TryAdd(type, f);
            return f(index);
        }

        public static Expression GetOffset(Type type, int index)
        {
            var g = GettersOffset;
            if (g.TryGetValue(type, out var f))
                return f(index);
            var m = GetGeneric.MakeGenericMethod(type);
            f = i => Expression.Call(Reader, m, Expression.Add(Offset, GetIndex(i)));
            g.TryAdd(type, f);
            return f(index);
        }


        static DateTime FixDateTime(DateTime d)
        {
            var t = d.Ticks;
            return new DateTime(t, DateTimeKind.Utc);
        }

        public static MethodInfo FixDateTimeMethod = typeof(DbCacheReadReader).GetMethod(nameof(FixDateTime), BindingFlags.NonPublic | BindingFlags.Static);

    }



    public static class DbCacheRead<T>
    {
        static DbCacheRead()
        {
            List<Expression> prog = new List<Expression>();
            List<Expression> progOffset = new List<Expression>();
            var ttype = typeof(T);
            var obj = Expression.Parameter(ttype, "o");

            var def = ModelDefinition<T>.Definition;
            var fs = def.FieldDefinitions;
            var fl = fs.Count;
            var reader = DbCacheReadReader.Reader;
            var offset = DbCacheReadReader.Offset;

            var nullP = DbCacheReadReader.Null;
            var dt = typeof(DateTime);
            for (int i = 0; i < fl; ++ i)
            {
                var f = fs[i];
                var read = DbCacheReadReader.Get(f.FieldType, i);
                if (f.IsNullable)
                {
                    if (f.FieldType.IsValueType)
                    {
                        var nt = typeof(Nullable<>).MakeGenericType(f.FieldType);
                        if (f.FieldType == dt)
                            read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, nt), Expression.Convert(Expression.Call(DbCacheReadReader.FixDateTimeMethod, read), nt));
                        else
                            read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, nt), Expression.Convert(read, nt));
                    }
                    else
                        read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, read.Type), read);
                }
                else
                {
                    if (f.FieldType == dt)
                        read = Expression.Call(DbCacheReadReader.FixDateTimeMethod, read);
                    if (!f.FieldType.IsValueType)
                        read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, read.Type), read);
                }
                prog.Add(Expression.Assign(Expression.Property(obj, f.PropertyInfo), read));

                read = DbCacheReadReader.GetOffset(f.FieldType, i);
                if (f.IsNullable)
                {
                    if (f.FieldType.IsValueType)
                    {
                        var nt = typeof(Nullable<>).MakeGenericType(f.FieldType);
                        if (f.FieldType == dt)
                            read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, nt), Expression.Convert(Expression.Call(DbCacheReadReader.FixDateTimeMethod, read), nt));
                        else
                            read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, nt), Expression.Convert(read, nt));
                    }
                    else
                        read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, read.Type), read);
                }
                else
                {
                    if (f.FieldType == dt)
                        read = Expression.Call(DbCacheReadReader.FixDateTimeMethod, read);
                    if (!f.FieldType.IsValueType)
                        read = Expression.Condition(DbCacheReadReader.IsNull(i), Expression.Constant(null, read.Type), read);
                }
                progOffset.Add(Expression.Assign(Expression.Property(obj, f.PropertyInfo), read));

            }
            var pc = prog.Count;
            if (pc <= 0)
            {
                WriteToObject = (a, b) => { };
                return;
            }
            var exp = pc > 1 ? Expression.Block(prog) : prog[0];
            var fn = Expression.Lambda<Action<DbDataReader, T>>(exp, reader, obj);
            WriteToObject = fn.Compile();

            exp = pc > 1 ? Expression.Block(progOffset) : progOffset[0];
            var fnOffset = Expression.Lambda<Action<DbDataReader, int, T>>(exp, reader, offset, obj);
            WriteToObjectWithOffset = fnOffset.Compile();
        }

        public static readonly Action<DbDataReader, T> WriteToObject;
        public static readonly Action<DbDataReader, int, T> WriteToObjectWithOffset;
    }


    public static class DbExtensions
    {

        /// <summary>
        /// Execute a custom command with paramaters
        /// </summary>
        /// <param name="con"></param>
        /// <param name="command">The command to execute, first paramater can be inserted uisng @p0, second as @p1 and so on</param>
        /// <param name="args">The parameters, first is named @p0, then @p1 and so on</param>
        /// <returns>A reader</returns>
        public static Task<DbDataReader> ReaderCommandAsync(this OrmConnection con, String command, params object[] args)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = command;
            var al = args.Length;
            for (int index = 0; index < al; ++ index)
            {
                String name = "p" + index;
                var a = cmd.CreateParameter();
                a.ParameterName = name;
                a.Value = args[index];
                cmd.Parameters.Add(a);
            }
            return cmd.ExecuteReaderAsync();
        }

        /// <summary>
        /// Execute a custom command with paramaters
        /// </summary>
        /// <param name="con"></param>
        /// <param name="command">The command to execute, first paramater can be inserted uisng @p0, second as @p1 and so on</param>
        /// <param name="args">The parameters, first is named @p0, then @p1 and so on</param>
        public static Task CommandAsync(this OrmConnection con, String command, params object[] args)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = command;
            var al = args.Length;
            for (int index = 0; index < al; ++index)
            {
                String name = "p" + index;
                var a = cmd.CreateParameter();
                a.ParameterName = name;
                a.Value = args[index];
                cmd.Parameters.Add(a);
            }
            return cmd.ExecuteNonQueryAsync();
        }


        /// <summary>
        /// Execute a custom command with paramaters
        /// </summary>
        /// <param name="con"></param>
        /// <param name="command">The command to execute, first paramater can be inserted uisng @p0, second as @p1 and so on</param>
        /// <param name="args">The parameters, first is named @p0, then @p1 and so on</param>
        /// <returns>A scale value</returns>
        public static Task<Object> ScaleCommandAsync(this OrmConnection con, String command, params object[] args)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = command;
            var al = args.Length;
            for (int index = 0; index < al; ++index)
            {
                String name = "p" + index;
                var a = cmd.CreateParameter();
                a.ParameterName = name;
                a.Value = args[index];
                cmd.Parameters.Add(a);
            }
            return cmd.ExecuteScalarAsync();
        }


        /// <summary>
        /// Create an object from the reader, user must ensure that the read data is of the correct type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static T CreateObject<T>(this DbDataReader reader) where T : new()
        {
            var o = new T();
            DbCacheRead<T>.WriteToObject(reader, o);
            return o;
        }

        /// <summary>
        /// Copy the data from the reader to an object, user must ensure that the read data is of the correct type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="t"></param>
        public static void CopyToObject<T>(this DbDataReader reader, T t) => DbCacheRead<T>.WriteToObject(reader, t);


        /// <summary>
        /// Copy the data from the reader to an object, user must ensure that the read data is of the correct type.
        /// The first column containing object data can be specified, useful if returning computed properties etc.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="t"></param>
        /// <param name="offset">The index of the first colun that contains the object</param>
        public static void CopyToObject<T>(this DbDataReader reader, T t, int offset) => DbCacheRead<T>.WriteToObjectWithOffset(reader, offset, t);


        /// <summary>
        /// Enumerate over all objects in a reader, user must ensure that the read data is of the correct type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static IEnumerable<T> AllObjects<T>(this DbDataReader reader) where T : new()
        {
            while (reader.Read())
                yield return reader.CreateObject<T>();
        }

        /// <summary>
        /// Iterate over the results of a query, user must ensure that the read data is of the correct type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="onResult">The function to run for every read object, return false to stop enumeration</param>
        /// <param name="command">The command to execute, first paramater can be inserted uisng @p0, second as @p1 and so on</param>
        /// <param name="args">The parameters, first is named @p0, then @p1 and so on</param>
        /// <returns></returns>
        public static async Task OnResultAsync<T>(this OrmConnection con, Func<T, bool> onResult, String command, params object[] args) where T : new()
        {
            using var reader = await ReaderCommandAsync(con, command, args).ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                if (!onResult(reader.CreateObject<T>()))
                    break;
        }

        /// <summary>
        /// Iterate over the results of a query, user must ensure that the read data is of the correct type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="onResult">The task to perform on every read object, return false to stop enumeration</param>
        /// <param name="command">The command to execute, first paramater can be inserted uisng @p0, second as @p1 and so on</param>
        /// <param name="args">The parameters, first is named @p0, then @p1 and so on</param>
        /// <returns></returns>
        public static async Task OnResultAsync<T>(this OrmConnection con, Func<T, Task<bool>> onResult, String command, params object[] args) where T : new()
        {
            using var reader = await ReaderCommandAsync(con, command, args).ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                if (!(await onResult(reader.CreateObject<T>()).ConfigureAwait(false)))
                    break;
        }

        /// <summary>
        /// Get the whole result of a query as a list, user must ensure that the read data is of the correct type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="command">The command to execute, first paramater can be inserted uisng @p0, second as @p1 and so on</param>
        /// <param name="args">The parameters, first is named @p0, then @p1 and so on</param>
        /// <returns></returns>
        public static async Task<List<T>> GetResultAsync<T>(this OrmConnection con, String command, params object[] args) where T : new()
        {
            var t = new List<T>();
            using var reader = await ReaderCommandAsync(con, command, args).ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                t.Add(reader.CreateObject<T>());
            return t;
        }



        /*
        /// <summary>
        /// Create a sql select statement, that can be filtered etc and then executed
        /// </summary>
        /// <param name="con"></param>
        /// <returns>A sql select statement</returns>
        public static TypedSelectStatement<T> From<T>(this OrmConnection con) => new TypedSelectStatement<T>(con.DialectProvider);
        */


        /// <summary>
        /// Add some data if ity doesn't exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="defaultValues"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task InsertIgnore<T>(this OrmConnection con, T[] defaultValues, String tableName = null, CancellationToken cancellationToken = new CancellationToken())
        {
            var dp = con.DialectProvider;
            var e = Array.Empty<string>();
            var ht = !String.IsNullOrEmpty(tableName);
            foreach (var x in defaultValues)
            {
                try
                {
                    var insertStatement = new TypedInsertStatement<T>(dp);
                    insertStatement.Values(x, e);
                    if (ht)
                        insertStatement.Into(tableName);
                    insertStatement.IgnoreErrors();
                    var commandDefinition = dp.ToInsertStatement(insertStatement.Statement, CommandFlags.None, cancellationToken);
                    await con.ExecuteAsync(commandDefinition).ConfigureAwait(false);
                }
                catch
                {

                }
            }
        }


    }


}
