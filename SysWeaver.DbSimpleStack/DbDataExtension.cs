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
using SysWeaver.AI;

namespace SysWeaver.Db
{


    public static class DbData
    {
        public static async Task<TableData> GetAsTableData<T>(this DbSimpleStack db, TableDataRequest request, long refreshRate = 30000, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null, TableData result = null)
        {
            using var con = await db.GetAsync().ConfigureAwait(false);
            return await GetAsTableData<T>(con, request, refreshRate, tableName, maxAllowedRows, extraFn, result).ConfigureAwait(false);
        }

        public static async Task<TableData> GetAsTableData<T>(this OrmConnection con, TableDataRequest request, long refreshRate = 30000, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null, TableData result = null)
        {
            var cols = DbDataType<T>.Cols;
            var res = await GetFiltered<T>(con, out var skip, out var limit, out var lookAhead, request, tableName, maxAllowedRows, extraFn).ConfigureAwait(false);
            var rows = TableDataTools.ExtractGet<T>(out var count, res, limit, lookAhead);
            count += skip;
            result = result ?? new();
            result.HandleCc(request.Cc, cols, tableName);
            result.Rows = rows;
            result.RowCount = count;
            result.RefreshRate = refreshRate;
            return result;
        }

        public static async Task<TypedTableData<T>> GetAsTypedTableData<T>(this DbSimpleStack db, TableDataRequest request, long refreshRate = 30000, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null, TypedTableData<T> result = null)
        {
            using var con = await db.GetAsync().ConfigureAwait(false);
            return await GetAsTypedTableData<T>(con, request, refreshRate, tableName, maxAllowedRows, extraFn, result).ConfigureAwait(false);
        }


        public static async Task<TypedTableData<T>> GetAsTypedTableData<T>(this OrmConnection con, TableDataRequest request, long refreshRate = 30000, String tableName = null, long maxAllowedRows = 100000, Action<TypedSelectStatement<T>> extraFn = null, TypedTableData<T> result = null)
        {
            var cols = DbDataType<T>.Cols;
            var res = await GetFiltered<T>(con, out var skip, out var limit, out var lookAhead, request, tableName, maxAllowedRows, extraFn).ConfigureAwait(false);
            var rows = TableDataTools.ExtractGetTyped<T>(out var count, res, limit, lookAhead);
            count += skip;
            result = result ?? new ();
            result.HandleCc(request.Cc, cols, tableName);
            result.Rows = rows;
            result.RowCount = count;
            result.RefreshRate = refreshRate;
            return result;
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
                    var colName = f.ColName;
                    if ((colName == null) || !filterActions.TryGetValue(colName, out var fs))
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
                else
                {
                    if (AddTextSearch<T>(q, dp, fullTextSearch))
                        first = false;
                }
            }
            q.Sort(sort, first);
            return q;
        }

        /// <summary>
        /// The most words of a search text that a plain text search uses (the rest are ignored).
        /// </summary>
        const int MaxSearchWords = 8;

        /// <summary>
        /// the plain text search of a <see cref="TableDataRequest.SearchText"/> when the row type has no matching full text index
        /// (before, the search text was silently ignored, ex: the Users table of the UserManager).
        /// The same semantics as the in-memory tables (<see cref="TableDataTools.Get{T}(TableDataRequest, IEnumerable{T}, string, long)"/>, SimpleTextSearch):
        /// the search text is split into words and numbers (<see cref="StringExt.ExtractWordsAndNumbers(string)"/>, at most <see cref="MaxSearchWords"/>),
        /// a row matches if every word is found (AND; case insensitive, as a sub string) in any of the <see cref="DbDataType{T}.SearchColumns"/>,
        /// and the rows are ordered by a rank (the sum over the found words of the weight of the first column it's found in, columns by descending
        /// weight, 0.95 per preceding column) before the requested sort order.
        /// The words are passed as parameters (never as SQL text), a word only has letters, digits and '.', so it has no LIKE wild cards.
        /// </summary>
        /// <typeparam name="T">The row type</typeparam>
        /// <param name="q">The query to add the condition and the rank order to</param>
        /// <param name="dialect">The dialect of the connection</param>
        /// <param name="searchText">The (trimmed, non empty) search text</param>
        /// <returns>True if a search was added (an order by was added), false if the type has no searchable column or the text has no words</returns>
        static bool AddTextSearch<T>(TypedSelectStatement<T> q, IDialectProvider dialect, String searchText)
        {
            var cols = DbDataType<T>.SearchColumns;
            var cl = cols.Length;
            if (cl <= 0)
                return false;
            var words = searchText.ExtractWordsAndNumbers().Select(x => x.FastToLower()).Distinct(StringComparer.Ordinal).Take(MaxSearchWords).ToArray();
            var wl = words.Length;
            if (wl <= 0)
                return false;
            var dp = q.Statement.Parameters;
            var lowerCols = cols.Select(x => "LOWER(" + dialect.GetQuotedColumnName(x) + ")").ToArray();
            var where = new StringBuilder("((");
            var rank = new StringBuilder("(");
            for (int w = 0; w < wl; ++w)
            {
                var pn = "SwSearch" + w;
                dp.Add(new SimpleStack.Orm.Expressions.Statements.StatementParameter(pn, typeof(String), String.Concat("%", words[w], "%")));
                if (w > 0)
                {
                    //  Every word must be found (AND), each in any column (OR)
                    where.Append(") AND (");
                    rank.Append(" + ");
                }
                rank.Append("(CASE");
                double weight = 1.0;
                for (int c = 0; c < cl; ++c)
                {
                    if (c > 0)
                        where.Append(" OR ");
                    where.Append(lowerCols[c]).Append(" LIKE @").Append(pn);
                    rank.Append(" WHEN ").Append(lowerCols[c]).Append(" LIKE @").Append(pn).Append(" THEN ")
                        .Append((Math.Round(weight * 10000.0) / 10000.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    weight *= 0.95;
                }
                rank.Append(" ELSE 0 END)");
            }
            where.Append("))");
            rank.Append(')');
            q.Where(where.ToString());
            var rname = dialect.GetQuotedColumnName("SwSearchRank");
            q.Statement.Columns.Add(rank.Append(" AS ").Append(rname).ToString());
            q.OrderByDescendingColumn(rname);
            return true;
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
