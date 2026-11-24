using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService.Db;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    public sealed partial class MySqlApiAuditService
    {

        const int MaxDataLength = 768;

        static readonly String CallsTable = "`AuditApiCalls`";
        static readonly String ClientsTable = "`AuditApiClients`";

        static MySqlAuditQuery NewQuery(ApiAuditMatcher m = null) => new(CallsTable, ClientsTable, m);

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        public Task<ApiAuditInfo> GetInfo()
            => Task.FromResult(new ApiAuditInfo
            {
                Storage = "MySql",
                MaxDataLength = MaxDataLength,
                TimeResolution = 1000,
            });

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public async Task<ApiAuditPage> Search(ApiAuditSearch search)
        {
            ApiAuditTools.GetPaging(search, out var row, out var count, out var lookAhead);
            var q = NewQuery(new ApiAuditMatcher(search));
            var cols = q.GetRowColumns();
            var order = String.Join(',', ApiAuditTools.GetOrder(search?.Order).Select(x => q.GetSql(x.Field) + (x.Desc ? " DESC" : "")));
            var sql = String.Concat("SELECT ", cols, q.GetFrom(), " ORDER BY ", order, String.Concat(" LIMIT ", row, ",", count + lookAhead));
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var rows = (await c.QueryAsync<MySqlAuditRow>(new CommandDefinition(sql, q.Params)).ConfigureAwait(false)).AsList();
            return new ApiAuditPage
            {
                RowCount = row + rows.Count,
                Entries = rows.Take(count).Select(x => x.ToEntry()).ToArray(),
            };
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        public async Task<ApiAuditEntry> GetEntry(long id)
        {
            var q = NewQuery();
            q.AddWhere("c.`Id`=" + q.Add(id));
            var sql = String.Concat("SELECT ", q.GetRowColumns(), q.GetFrom());
            using var c = await Db.GetAsync().ConfigureAwait(false);
            return (await c.QueryAsync<MySqlAuditRow>(new CommandDefinition(sql, q.Params)).ConfigureAwait(false)).FirstOrDefault()?.ToEntry();
        }

        sealed class FacetRow
        {
            public String V { get; set; }
            public long N { get; set; }
            public long F { get; set; }
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public async Task<ApiAuditFacet[]> GetFacets(ApiAuditFacetRequest request)
        {
            var max = Math.Clamp(request?.MaxValues ?? 10, 1, ApiAuditTools.MaxFacetValues);
            List<ApiAuditFacet> res = new();
            using var c = await Db.GetAsync().ConfigureAwait(false);
            foreach (var name in (request?.Fields ?? []).Distinct(StringComparer.Ordinal))
            {
                var field = ApiAuditField.Get(name);
                if (field == null)
                    continue;
                var q = NewQuery(new ApiAuditMatcher(request, name));
                var value = q.GetTextSql(field);
                var sql = String.Concat("SELECT ", value, " AS V,COUNT(*) AS N,CAST(SUM(c.`State`=2) AS SIGNED) AS F", q.GetFrom(), String.Concat(" GROUP BY V ORDER BY N DESC,V LIMIT ", max + 1));
                var rows = (await c.QueryAsync<FacetRow>(new CommandDefinition(sql, q.Params)).ConfigureAwait(false)).AsList();
                res.Add(new ApiAuditFacet
                {
                    Field = field.Name,
                    Values = rows.Take(max).Select(x => new ApiAuditFacetValue
                    {
                        Value = x.V,
                        Count = x.N,
                        Failed = x.F,
                    }).ToArray(),
                    More = rows.Count > max,
                });
            }
            return res.ToArray();
        }

        sealed class TotalRow
        {
            public long Completed { get; set; }
            public long Failed { get; set; }
            public long InProgress { get; set; }
            public long Users { get; set; }
            public long Clients { get; set; }
            public double? AverageDuration { get; set; }
            public double? MaxDuration { get; set; }
            public DateTime? First { get; set; }
            public DateTime? Last { get; set; }
        }

        sealed class StepRow
        {
            public long I { get; set; }
            public byte S { get; set; }
            public long N { get; set; }
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public async Task<ApiAuditSummary> GetSummary(ApiAuditSummaryRequest request)
        {
            static DateTime? Utc(DateTime? t) => t == null ? null : DateTime.SpecifyKind(t.Value, DateTimeKind.Utc);
            var m = new ApiAuditMatcher(request);
            var q = NewQuery(m);
            var duration = q.GetSql(ApiAuditField.Get(nameof(ApiAuditEntry.Duration)));
            var sql = String.Concat(
                "SELECT IFNULL(CAST(SUM(c.`State`=1) AS SIGNED),0) AS Completed,IFNULL(CAST(SUM(c.`State`=2) AS SIGNED),0) AS Failed,IFNULL(CAST(SUM(c.`State`=0) AS SIGNED),0) AS InProgress," +
                "COUNT(DISTINCT NULLIF(c.`UserName`,'')) AS Users,COUNT(DISTINCT c.`ClientId`) AS Clients,MIN(c.`Begin`) AS `First`,MAX(c.`Begin`) AS `Last`,AVG(",
                duration, ") AS AverageDuration,MAX(", duration, ") AS MaxDuration", q.GetFrom());
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var t = (await c.QueryAsync<TotalRow>(new CommandDefinition(sql, q.Params)).ConfigureAwait(false)).First();
            var res = new ApiAuditSummary
            {
                Completed = t.Completed,
                Failed = t.Failed,
                InProgress = t.InProgress,
                Users = t.Users,
                Clients = t.Clients,
                AverageDuration = t.AverageDuration,
                MaxDuration = t.MaxDuration,
                First = Utc(t.First),
                Last = Utc(t.Last),
                Steps = [],
            };
            if ((res.First == null) || (res.Last == null))
                return res;
            ApiAuditTools.GetTimeLine(m, res, out var from, out var to);
            var stepSeconds = ApiAuditTools.GetStepSeconds(from, to, request?.Steps ?? 60);
            sql = String.Concat("SELECT CAST(FLOOR(TIMESTAMPDIFF(SECOND,'1970-01-01',c.`Begin`)/", stepSeconds, ") AS SIGNED) AS I,c.`State` AS S,COUNT(*) AS N" + q.GetFrom() + " GROUP BY I,S");
            Dictionary<long, ApiAuditStep> steps = new();
            foreach (var x in await c.QueryAsync<StepRow>(new CommandDefinition(sql, q.Params)).ConfigureAwait(false))
            {
                if (!steps.TryGetValue(x.I, out var s))
                    steps[x.I] = s = new ApiAuditStep();
                switch ((ApiAuditStates)x.S)
                {
                    case ApiAuditStates.Completed:
                        s.Completed = x.N;
                        break;
                    case ApiAuditStates.Failed:
                        s.Failed = x.N;
                        break;
                    default:
                        s.InProgress = x.N;
                        break;
                }
            }
            ApiAuditTools.SetSteps(res, m, stepSeconds, steps);
            return res;
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public async Task<TableData> AuditTable(TableDataRequest r)
        {
            var t = await ApiAuditTools.GetTable(this, r).ConfigureAwait(false);
            //  Times are stored with a resolution of a second, so the duration is of little use
            if (t.ModifyColumns(out _))
                t.HideColumn(nameof(ApiAuditEntry.Duration));
            return t;
        }

    }

}
