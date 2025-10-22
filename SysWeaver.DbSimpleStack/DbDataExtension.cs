// https://github.com/SimpleStack/simplestack.orm

using SimpleStack.Orm;
using SimpleStack.Orm.Expressions.Statements.Typed;
using Dapper;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using SysWeaver.Data;
using System.Text;

namespace SysWeaver.Db
{

    public static class DbData
    {
        public static async Task<TableData> GetAsTableData<T>(this DbSimpleStack db, TableDataRequest request, long refreshRate = 30000, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null)
        {
            using var con = await db.GetAsync().ConfigureAwait(false);
            return await GetAsTableData<T>(con, request, refreshRate, tableName, maxAllowedRows, extraFn).ConfigureAwait(false);
        }

        public static async Task<TableData> GetAsTableData<T>(this OrmConnection con, TableDataRequest request, long refreshRate = 30000, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null)
        {
            var cols = DbDataType<T>.Cols;
            var res = await GetFiltered<T>(con, out var skip, out var limit, out var lookAhead, request, tableName, maxAllowedRows, extraFn).ConfigureAwait(false);
            var rows = TableDataTools.ExtractGet<T>(out var count, res, limit, lookAhead);
            count += skip;
            var cc = EnvInfo.Cc;
            return new TableData
            {
                Rows = rows,
                Cols = (request.Cc == cc) ? null : cols,
                RowCount = count,
                RefreshRate = refreshRate,
                Cc = cc,
            };
        }
        const int DefaultRows = 1000;

        public static Task<IEnumerable<T>> GetFiltered<T>(this OrmConnection con, TableDataRequest request, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null)
            => GetFiltered<T>(con, out var s, out var l, out var la, request, tableName, maxAllowedRows, extraFn);


        public static Task<IEnumerable<T>> GetFiltered<T>(this OrmConnection con, out long skip, out long limit, out long lookAhead, TableDataRequest request, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null)
        {
            long s = request.Row;
            if (s < 0)
                s = 0;
            if (s > Int32.MaxValue)
                s = Int32.MaxValue;
            long l = request.MaxRowCount;
            if (l <= 0)
                l = DefaultRows;
            if (l > maxAllowedRows)
                l = maxAllowedRows;
            long la = request.LookAheadCount;
            if (la <= 0)
                la = 0;
            if (la > DefaultRows)
                la = DefaultRows;

            skip = s;
            limit = l;
            lookAhead = la;
            var q = GetSelectStatement<T>(con, request);
            extraFn?.Invoke(q);
            q.Limit((int)s, (int)(l + la));
            var qs = q.Statement;
            if (!String.IsNullOrEmpty(tableName))
                qs.TableName = tableName;
            var dp = con.DialectProvider;
            var cmd = dp.ToSelectStatement(qs, CommandFlags.Buffered);
            return con.QueryAsync<T>(cmd);
        }

        public static TypedSelectStatement<T> GetSelectStatement<T>(this OrmConnection con, TableDataRequest request, bool allowNonIndexed = false)
        {
            var sort = request.Order;
            //            int sort = request.SortCol;
            //            var order = request.SortReverse ? DbDataType<T>.OrderDesc : DbDataType<T>.Order;
            var filterActions = allowNonIndexed ? DbDataType<T>.NoIndexColFilters : DbDataType<T>.ColFilters;
            //            var ol = order.Length;
            var filters = request.Filters;

            var fullTextSearch = request.SearchText?.Trim();

            var dp = con.DialectProvider;
            var q = new TypedSelectStatement<T>(dp);
            var qs = q.Statement;
            bool first = true;
            if (filters != null)
            {
                foreach (var f in filters)
                {
                    if (f == null)
                        continue;
                    var op = (int)f.Op;
                    if (op < 0)
                        continue;
                    var val = f.Value;
                    if (val == null)
                        continue;
                    if (!filterActions.TryGetValue(f.ColName, out var fs))
                        continue;
                    op += op;
                    if (f.Invert)
                        ++op;
                    if (op >= fs.Length)
                        continue;
                    var fa = fs[op];
                    if (fa == null)
                        continue;
                    fa(q, val);
                }
            }
            if (!String.IsNullOrEmpty(fullTextSearch))
            {
                var fullTextIndex = request.SearchIndex?.Trim();
                if (String.IsNullOrEmpty(fullTextIndex))
                    fullTextIndex = "FullText";
                if (DbDataType<T>.FullTextIndexes.TryGetValue(fullTextIndex, out var columns))
                {
                    var rname = dp.GetQuotedColumnName("FtRank");
                    qs.Parameters.Add(new SimpleStack.Orm.Expressions.Statements.StatementParameter(fullTextIndex, typeof(String), fullTextSearch));
                    var sb = new StringBuilder("MATCH(");
                    sb.Append(String.Join(',', columns.Select(x => dp.GetQuotedColumnName(x))));
                    sb.Append(") AGAINST (@");
                    sb.Append(fullTextIndex);
                    sb.Append(request.SearchNatural ? " IN NATURAL LANGUAGE MODE)" : " IN BOOLEAN MODE)");
                    var mn = sb.ToString();
                    sb.Append(" AS ");
                    sb.Append(rname);
                    qs.Columns.Add(sb.ToString());
                    q.OrderByDescendingColumn(rname);
                    q.Where(mn + ">0");
                    first = false;
                }
            }
            q.Sort(sort, first);
            return q;
        }


        internal static readonly Type[] StringArgs = [typeof(String)];
        internal static readonly MethodInfo ContainsMethod = typeof(String).GetMethod(nameof(String.Contains), BindingFlags.Instance | BindingFlags.Public, StringArgs);
        internal static readonly MethodInfo StartsWithMethod = typeof(String).GetMethod(nameof(String.StartsWith), BindingFlags.Instance | BindingFlags.Public, StringArgs);
        internal static readonly MethodInfo EndsWithMethod = typeof(String).GetMethod(nameof(String.EndsWith), BindingFlags.Instance | BindingFlags.Public, StringArgs);
        internal static readonly ParameterExpression ValueExp = Expression.Parameter(typeof(String), "value");
        internal static readonly ConstantExpression ZeroExp = Expression.Constant(0, typeof(Int32));

        internal static readonly IReadOnlySet<Type> CompareTypes = ReadOnlyData.Set(
            typeof(String),
            typeof(Boolean)
            );

    }



}
