using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Builds the SQL of the audit reports: the calls (c) joined with the clients (k), filtered by an <see cref="ApiAuditMatcher"/>.
    /// All values are passed as parameters (never as SQL text).
    /// </summary>
    sealed class MySqlAuditQuery
    {

        /// <summary>
        /// The SQL of a field
        /// </summary>
        sealed class Column
        {
            /// <summary>
            /// The value expression
            /// </summary>
            public readonly String Sql;

            /// <summary>
            /// The value as text (if it isn't a text)
            /// </summary>
            public readonly String TextSql;

            /// <summary>
            /// True if the column is in the client table
            /// </summary>
            public readonly bool IsClient;

            /// <summary>
            /// True if the column only can have ASCII chars
            /// </summary>
            public readonly bool IsAscii;

            public Column(String sql, String textSql = null, bool isAscii = false)
            {
                Sql = sql;
                TextSql = textSql ?? sql;
                IsClient = sql.Contains("k.`", StringComparison.Ordinal);
                IsAscii = isAscii;
            }
        }

        const String EndSql = "(CASE WHEN c.`State`=0 THEN NULL ELSE c.`End` END)";
        const String DurationSql = "(CASE WHEN c.`State`=0 THEN NULL ELSE TIMESTAMPDIFF(MICROSECOND,c.`Begin`,c.`End`)/1000.0 END)";
        const String DateFormat = ",'%Y-%m-%d %H:%i:%s')";

        static readonly String SessionSql = String.Concat("(CASE WHEN IFNULL(c.`Session`,'')='' THEN NULL WHEN CHAR_LENGTH(c.`Session`)>", ApiAuditTools.SessionChars,
            " THEN CONCAT(LEFT(c.`Session`,", ApiAuditTools.SessionChars, "),'" + ApiAuditTools.SessionSuffix + "') ELSE '" + ApiAuditTools.SessionSuffix + "' END)");

        static readonly String StateTextSql = String.Concat("(CASE c.`State` ", String.Join(' ', Enum.GetValues<ApiAuditStates>().Select(x => String.Concat("WHEN ", (int)x, " THEN '", x, '\''))), " ELSE CAST(c.`State` AS CHAR) END)");

        static readonly Dictionary<String, Column> Columns = new(StringComparer.Ordinal)
        {
            { nameof(ApiAuditEntry.Id), new("c.`Id`", "CAST(c.`Id` AS CHAR)") },
            { nameof(ApiAuditEntry.Begin), new("c.`Begin`", "DATE_FORMAT(c.`Begin`" + DateFormat) },
            { nameof(ApiAuditEntry.End), new(EndSql, "DATE_FORMAT(" + EndSql + DateFormat) },
            { nameof(ApiAuditEntry.Duration), new(DurationSql, "CAST(" + DurationSql + " AS CHAR)") },
            { nameof(ApiAuditEntry.State), new("c.`State`", StateTextSql) },
            { nameof(ApiAuditEntry.Api), new("c.`Api`", null, true) },
            { nameof(ApiAuditEntry.Group), new("c.`Group`", null, true) },
            { nameof(ApiAuditEntry.UserName), new("c.`UserName`") },
            { nameof(ApiAuditEntry.UserGuid), new("c.`UserGuid`", null, true) },
            { nameof(ApiAuditEntry.Input), new("c.`Input`") },
            { nameof(ApiAuditEntry.Output), new("c.`Output`") },
            { nameof(ApiAuditEntry.Session), new(SessionSql, null, true) },
            { nameof(ApiAuditEntry.ClientId), new("c.`ClientId`", "CAST(c.`ClientId` AS CHAR)") },
            { nameof(ApiAuditEntry.Ip), new("k.`Ip`", null, true) },
            { nameof(ApiAuditEntry.Language), new("k.`Language`", null, true) },
            { nameof(ApiAuditEntry.TimeZone), new("k.`TimeZone`", null, true) },
            { nameof(ApiAuditEntry.DeviceId), new("k.`DeviceId`", null, true) },
            { nameof(ApiAuditEntry.UserAgent), new("k.`UserAgent`") },
        };

        /// <summary>
        /// Create a query
        /// </summary>
        /// <param name="calls">Quoted name of the calls table</param>
        /// <param name="clients">Quoted name of the clients table</param>
        /// <param name="m">Optional filter</param>
        public MySqlAuditQuery(String calls, String clients, ApiAuditMatcher m = null)
        {
            Calls = calls;
            Clients = clients;
            if (m == null)
                return;
            var w = Where;
            if (m.From != null)
                w.Add("c.`Begin`>=" + Add(m.From.Value));
            if (m.To != null)
                w.Add("c.`Begin`<" + Add(m.To.Value));
            foreach (var x in m.Selections)
                w.Add(Get(x));
            foreach (var x in m.All)
                w.Add(Get(x));
            foreach (var x in m.Terms)
                w.Add(Get(x));
            if (m.Any.Count > 0)
                w.Add("(" + String.Join(" OR ", m.Any.Select(Get)) + ")");
        }

        readonly String Calls;
        readonly String Clients;
        readonly List<String> Where = new();
        bool NeedClient;

        /// <summary>
        /// The parameters
        /// </summary>
        public readonly DynamicParameters Params = new();

        int ParamCount;

        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The SQL of the parameter</returns>
        public String Add(Object value)
        {
            var name = "a" + ParamCount++;
            Params.Add(name, value);
            return "@" + name;
        }

        /// <summary>
        /// Add a condition
        /// </summary>
        /// <param name="sql">The SQL</param>
        public void AddWhere(String sql) => Where.Add(sql);

        Column Use(ApiAuditField f)
        {
            var c = Columns[f.Name];
            NeedClient |= c.IsClient;
            return c;
        }

        /// <summary>
        /// Get the SQL of the value of a field, as a text that never is null
        /// </summary>
        /// <param name="f">The field</param>
        /// <returns>The SQL</returns>
        public String GetTextSql(ApiAuditField f) => String.Concat("IFNULL(", Use(f).TextSql, ",'')");

        /// <summary>
        /// Get the SQL of the value of a field
        /// </summary>
        /// <param name="f">The field</param>
        /// <returns>The SQL</returns>
        public String GetSql(ApiAuditField f) => Use(f).Sql;

        /// <summary>
        /// Get the columns of a <see cref="MySqlAuditRow"/>
        /// </summary>
        public String GetRowColumns()
            => String.Join(',', ApiAuditField.All.Select(x => String.Concat(GetSql(x), " AS `", x.Name, '`')));

        /// <summary>
        /// Get the from and where part, must be used after all columns
        /// </summary>
        public String GetFrom()
        {
            var sb = new StringBuilder(" FROM ").Append(Calls).Append(" c");
            if (NeedClient)
                sb.Append(" LEFT JOIN ").Append(Clients).Append(" k ON k.`Id`=c.`ClientId`");
            if (Where.Count > 0)
                sb.Append(" WHERE ").Append(String.Join(" AND ", Where));
            return sb.ToString();
        }

        const String Never = "(0=1)";

        static String Invert(String sql) => String.Concat("(NOT IFNULL(", sql, ",0))");

        static String EscapeLike(String s) => s.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

        String Like(String sql, String before, String text, String after)
            => String.Concat("(", sql, " LIKE ", Add(String.Concat(before, EscapeLike(text), after)), " ESCAPE '!')");

        /// <summary>
        /// A text that can be compared with any parameter (an ASCII column can't be compared to a text with other chars), always using a binary compare.
        /// </summary>
        String ComparableText(Column c, bool caseSensitive)
        {
            var s = String.Concat("CONVERT(IFNULL(", c.TextSql, ",'') USING utf8mb4)");
            return (caseSensitive ? s : String.Concat("LOWER(", s, ")")) + " COLLATE utf8mb4_bin";
        }

        String Get(ApiAuditTerm x)
        {
            var t = x.Text.ToLowerInvariant();
            var fields = x.Field == null ? ApiAuditField.SearchFields : [x.Field];
            var s = "(" + String.Join(" OR ", fields.Select(f => Like(ComparableText(Use(f), false), "%", t, "%"))) + ")";
            return x.Exclude ? Invert(s) : s;
        }

        String Get(ApiAuditSelection x)
        {
            var f = x.Field;
            var c = Use(f);
            List<String> any = new();
            if (f.Type == ApiAuditFieldTypes.Text)
            {
                //  Exact (the columns use a binary collation), so that indices can be used
                var values = x.Values.Where(v => (v.Length > 0) && (!c.IsAscii || Ascii.IsValid(v))).ToArray();
                if (values.Length > 0)
                    any.Add(String.Concat(c.Sql, " IN ", Add(values)));
            }
            else if (f.Type == ApiAuditFieldTypes.Time)
            {
                any.Add(String.Concat(c.TextSql, " IN ", Add(x.Values.ToArray())));
            }
            else
            {
                List<double> values = new();
                foreach (var v in x.Values)
                    if (ApiAuditCondition.TryParseNumber(v, f.Type == ApiAuditFieldTypes.State, out var n))
                        values.Add(n);
                if (values.Count > 0)
                    any.Add(String.Concat(c.Sql, " IN ", Add(values.ToArray())));
            }
            if (x.Values.Contains(""))
                any.Add(f.Type == ApiAuditFieldTypes.Text ? String.Concat("IFNULL(", c.Sql, ",'')=''") : (c.Sql + " IS NULL"));
            var s = any.Count <= 0 ? Never : ("(" + String.Join(" OR ", any) + ")");
            return x.Exclude ? Invert(s) : s;
        }

        String Get(ApiAuditCondition x)
        {
            if (x.Never)
                return Never;
            var s = GetOp(x);
            return x.Invert ? Invert(s) : s;
        }

        /// <summary>
        /// Must have the same result as <see cref="ApiAuditCondition.Match(ApiAuditEntry)"/>
        /// </summary>
        String GetOp(ApiAuditCondition x)
        {
            var c = Use(x.Field);
            if (x.IsText)
            {
                var v = ComparableText(c, x.CaseSensitive);
                var t = x.CaseSensitive ? x.Texts : x.Texts.Select(y => y.ToLowerInvariant()).ToArray();
                switch (x.Op)
                {
                    case TableDataFilterOps.Equals:
                        return String.Concat("(", v, "=", Add(t[0]), ")");
                    case TableDataFilterOps.LessThan:
                        return String.Concat("(", v, "<", Add(t[0]), ")");
                    case TableDataFilterOps.GreaterThan:
                        return String.Concat("(", v, ">", Add(t[0]), ")");
                    case TableDataFilterOps.LessEqual:
                        return String.Concat("(", v, "<=", Add(t[0]), ")");
                    case TableDataFilterOps.GreaterEqual:
                        return String.Concat("(", v, ">=", Add(t[0]), ")");
                    case TableDataFilterOps.Contains:
                        return Like(v, "%", t[0], "%");
                    case TableDataFilterOps.StartsWith:
                        return Like(v, "", t[0], "%");
                    case TableDataFilterOps.EndsWith:
                        return Like(v, "%", t[0], "");
                    case TableDataFilterOps.AnyOf:
                        return String.Concat("(", v, " IN ", Add(t), ")");
                    case TableDataFilterOps.InRange:
                        return String.Concat("(", v, ">=", Add(t[0]), " AND ", v, "<", Add(t[1]), ")");
                    default:
                        return String.Concat("(", v, "<", Add(t[0]), " OR ", v, ">", Add(t[1]), ")");
                }
            }
            var sql = c.Sql;
            if (x.Field.Type == ApiAuditFieldTypes.Time)
            {
                var t = x.Times;
                String Equal(DateTime d) => String.Concat("(", sql, ">=", Add(d), " AND ", sql, "<", Add(ApiAuditCondition.EqualEnd(d)), ")");
                switch (x.Op)
                {
                    case TableDataFilterOps.Equals:
                    case TableDataFilterOps.AnyOf:
                        return String.Concat("(", String.Join(" OR ", t.Select(Equal)), ")");
                    case TableDataFilterOps.LessThan:
                        return String.Concat("(", sql, "<", Add(t[0]), ")");
                    case TableDataFilterOps.GreaterThan:
                        return String.Concat("(", sql, ">=", Add(ApiAuditCondition.EqualEnd(t[0])), ")");
                    case TableDataFilterOps.LessEqual:
                        return String.Concat("(", sql, "<", Add(ApiAuditCondition.EqualEnd(t[0])), ")");
                    case TableDataFilterOps.GreaterEqual:
                        return String.Concat("(", sql, ">=", Add(t[0]), ")");
                    case TableDataFilterOps.InRange:
                        return String.Concat("(", sql, ">=", Add(t[0]), " AND ", sql, "<", Add(t[1]), ")");
                    default:
                        return String.Concat("(", sql, "<", Add(t[0]), " OR ", sql, ">=", Add(ApiAuditCondition.EqualEnd(t[1])), ")");
                }
            }
            var n = x.Numbers;
            switch (x.Op)
            {
                case TableDataFilterOps.Equals:
                    return String.Concat("(", sql, "=", Add(n[0]), ")");
                case TableDataFilterOps.LessThan:
                    return String.Concat("(", sql, "<", Add(n[0]), ")");
                case TableDataFilterOps.GreaterThan:
                    return String.Concat("(", sql, ">", Add(n[0]), ")");
                case TableDataFilterOps.LessEqual:
                    return String.Concat("(", sql, "<=", Add(n[0]), ")");
                case TableDataFilterOps.GreaterEqual:
                    return String.Concat("(", sql, ">=", Add(n[0]), ")");
                case TableDataFilterOps.AnyOf:
                    return String.Concat("(", sql, " IN ", Add(n), ")");
                case TableDataFilterOps.InRange:
                    return String.Concat("(", sql, ">=", Add(n[0]), " AND ", sql, "<", Add(n[1]), ")");
                default:
                    return String.Concat("(", sql, "<", Add(n[0]), " OR ", sql, ">", Add(n[1]), ")");
            }
        }

    }


    /// <summary>
    /// A row read from the database, same as <see cref="ApiAuditEntry"/>
    /// </summary>
    sealed class MySqlAuditRow
    {
        public long Id { get; set; }
        public DateTime Begin { get; set; }
        public DateTime? End { get; set; }
        public double? Duration { get; set; }
        public byte State { get; set; }
        public String Api { get; set; }
        public String Group { get; set; }
        public String UserName { get; set; }
        public String UserGuid { get; set; }
        public String Input { get; set; }
        public String Output { get; set; }
        public String Session { get; set; }
        public long ClientId { get; set; }
        public String Ip { get; set; }
        public String Language { get; set; }
        public String TimeZone { get; set; }
        public String DeviceId { get; set; }
        public String UserAgent { get; set; }

        /// <summary>
        /// Get as an entry
        /// </summary>
        /// <returns>The entry</returns>
        public ApiAuditEntry ToEntry() => new ApiAuditEntry
        {
            Id = Id,
            Begin = DateTime.SpecifyKind(Begin, DateTimeKind.Utc),
            End = End == null ? null : DateTime.SpecifyKind(End.Value, DateTimeKind.Utc),
            Duration = Duration,
            State = (ApiAuditStates)State,
            Api = Api,
            Group = Group,
            UserName = UserName,
            UserGuid = UserGuid,
            Input = Input,
            Output = Output,
            Session = Session,
            ClientId = ClientId,
            Ip = Ip,
            Language = Language,
            TimeZone = TimeZone,
            DeviceId = DeviceId,
            UserAgent = UserAgent,
        };
    }

}
