using SimpleStack.Orm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Db;
using SysWeaver.MicroService.Db;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.MicroService
{

    [WebApiUrl("Audit")]
    public sealed class MySqlApiAuditService : IApiAuditService, IDisposable
    {
        public MySqlApiAuditService(ServiceManager manager, MySqlApiAuditParams p = null)
        {
            p = p ?? new MySqlApiAuditParams();
            Db = new MySqlDbSimpleStack(p);
            InitDb(Db).RunAsync();
            WriteTask = new PeriodicTask(WriteData, 100);
        }

        volatile bool IsDisposing;

        public void Dispose()
        {
            IsDisposing = true;
            Interlocked.Exchange(ref WriteTask, null)?.Dispose();

        }

        PeriodicTask WriteTask;


        async Task<long> GetClient(OrmConnection c, Temp t)
        {
            var ua = (t.UserAgent ?? "").LimitLength(768, "");
            var client = await c.FirstOrDefaultAsync<DbAuditApiClient>(x => 
            x.Ip == t.Ip &&
            x.Language == t.Language &&
            x.TimeZone == t.TimeZone &&
            x.UserAgent == ua &&
            x.DeviceId == t.DeviceId).ConfigureAwait(false);
            var nd = new DbAuditApiClient
            {
                Ip = t.Ip,
                Language = t.Language,
                TimeZone = t.TimeZone,
                UserAgent = ua,
                DeviceId = t.DeviceId,
                Count = 1,
                First = t.Time,
                FirstId = t.Id,
                Last = t.Time,
                LastId = t.Id,
            };
            if (client == null)
            {
                return await c.InsertAsync<long, DbAuditApiClient>(nd).ConfigureAwait(false);
            }else
            {
                client.Count += 1;
                client.Last = t.Time;
                client.LastId = t.Id;
                await c.UpdateAsync(client, x => new { x.Count, x.Last, x.LastId }).ConfigureAwait(false);
                return client.Id;
            }
        }

        readonly ITextSerializerType JsonSer = SerManager.GetText("json");

        async ValueTask<bool> WriteData()
        {
            var q = Calls;
            for (; ; )
            {
                List<Temp> write = null;
                while (q.TryDequeue(out var t))
                {
                    write = write ?? new List<Temp>();
                    write.Add(t);
                }
                if (write == null)
                    return true;

                var count = write.Count;
                Dictionary<long, DbAuditApiCall> calls = new Dictionary<long, DbAuditApiCall>(count);
                List<DbAuditApiCall> orderedCalls = new List<DbAuditApiCall>(count);
                var ser = JsonSer;
                using (var c = await Db.GetAsync().ConfigureAwait(false))
                {
                    using var tr = await c.BeginTransactionAsync().ConfigureAwait(false);
                    for (int i = 0; i < count; ++ i)
                    {
                        var temp = write[i];
                        DbAuditApiCall data;
                        var id = temp.Id;
                        var type = temp.Type;
                        var auth = temp.Auth;
                        var val = temp.Value;
                        if (type == 0)
                        {
                            var clientId = await GetClient(c, temp).ConfigureAwait(false);
                            var api = temp.Api;
                            data = new DbAuditApiCall
                            {
                                Api = api.Uri,
                                Begin = temp.Time,
                                ClientId = clientId,
                                Group = api.AuditGroup.LimitLength(32, ""),
                                Id = id,
                                Input = val == null ? null : ser.ToStringWithoutType(val).LimitLength(768, ""),
                                UserGuid = auth?.Guid,
                                Session = temp.SessionToken,
                                UserName = auth?.Username,
                            };
                            orderedCalls.Add(data);
                            calls[id] = data;
                        }
                        else
                        {
                            if (!calls.TryGetValue(id, out data))
                            {
                                data = new DbAuditApiCall
                                {
                                    Id = id,
                                };
                                orderedCalls.Add(data);
                                calls[id] = data;
                            }
                            data.State = type;
                            data.End = temp.Time;
                            data.UserGuid = auth?.Guid;
                            data.UserName = auth?.Username;
                            if (type == 1)
                                data.Output = val == null ? null : ser.ToStringWithoutType(val).LimitLength(768, "");
                            else
                                data.Output = (temp.Value as Exception).Message.LimitLength(768, "");
                        }
                    }
                    foreach (var oc in orderedCalls)
                    {
                        if (oc.Api == null)
                        {
                            await c.UpdateAsync(oc, x => new { x.State, x.End, x.UserGuid, x.UserName, x.Output }).ConfigureAwait(false);
                        }
                        else
                        {
                            await c.InsertAsync(oc).ConfigureAwait(false);
                        }
                    }
                    await tr.CommitAsync().ConfigureAwait(false);   
                }

            }
        }

        static async Task InitDb(MySqlDbSimpleStack db)
        {
            await db.Init().ConfigureAwait(false);
            using (var c = await db.GetAsync().ConfigureAwait(false))
            {
                await db.InitTable<DbAuditApiCall>(c).ConfigureAwait(false);
                await db.InitTable<DbAuditApiClient>(c).ConfigureAwait(false);
            }
        }

        sealed class Temp
        {

            public readonly Byte Type;
            public readonly long Id;
            public readonly Auth.Authorization Auth;
            public readonly String TimeZone;
            public readonly String UserAgent;
            public readonly String Language;
            public readonly String DeviceId;
            public readonly String SessionToken;
            public readonly String Ip;
            public readonly IHttpApiAudit Api;
            public readonly object Value;
            public readonly DateTime Time;

            public Temp(byte type, long id, object value, Auth.Authorization auth, string sessionToken, string deviceId, string timeZone, string userAgent, string language, string ip, IHttpApiAudit api)
            {
                Type = type;
                Id = id;
                DeviceId = deviceId;
                Value = value;
                Auth = auth;
                TimeZone = timeZone;
                UserAgent = userAgent;
                Language = language;
                Ip = ip;
                Api = api;
                SessionToken = sessionToken;
                Time = DateTime.UtcNow;
            }

            public Temp(byte type, long id, object value, Auth.Authorization auth)
            {
                Time = DateTime.UtcNow;
                Type = type;
                Id = id;
                Value = value;
                Auth = auth;
            }

        }


        readonly ConcurrentQueue<Temp> Calls = new ConcurrentQueue<Temp>();

        public void OnApiBegin(long id, HttpServerRequest r, IHttpApiAudit api, object value)
        {
            if (IsDisposing)
                return;
            var s = r.Session;
            Calls.Enqueue(new Temp(0, id, value, s.Auth, s.Token, s.DeviceId, s.ClientTimeZone, s.UserAgent, s.ClientLanguage, r.GetIpAddress(), api));
        }

        public void OnApiEnd(long id, HttpServerRequest r, IHttpApiAudit api, object value)
        {
            if (IsDisposing)
                return;
            Calls.Enqueue(new Temp(1, id, value, r.Session.Auth));
        }

        public void OnApiException(long id, HttpServerRequest r, IHttpApiAudit api, Exception ex)
        {
            if (IsDisposing)
                return;
            Calls.Enqueue(new Temp(2, id, ex, r.Session.Auth));
        }

        readonly MySqlDbSimpleStack Db;

        const String AuditAuth = Roles.Admin + ",Ops";


        static void RemoveAllButLast(ref TableDataFilter[] filters, String removeKey)
        {
            var c = filters.Length;
            List<TableDataFilter> n = new(c);
            --c;
            for (int i = 0; i < c - 1; ++i)
            {
                var f = filters[i];
                if (!f.ColName.FastEquals(removeKey))
                    n.Add(f);
            }
            if (n.Count == c)
                return;
            n.Add(filters[c]);
            filters = n.ToArray();
        }

        static readonly int Col_DbAuditApiCall_ClientId = TableDataTools.GetColumnIndex<DbAuditApiCall>(nameof(DbAuditApiCall.ClientId));
        static readonly int Col_DbAuditApiClient_Id = TableDataTools.GetColumnIndex<DbAuditApiClient>(nameof(DbAuditApiClient.Id));

        /// <summary>
        /// Show all auditable API calls made
        /// </summary>
        /// <param name="r">Table request</param>
        /// <returns>Auditable API calls made</returns>
        [WebApi]
        [WebApiAuth(AuditAuth)]
        [WebMenuTable(null, "Debug/Audit/Api calls", "Api calls", null, null, 0, AuditAuth)]
        [WebApiRequestCache(1)]
        public async Task<TableData> ApiCallTable(TableDataRequest r)
        {
            if (r.Param != null)
            {
                var id = long.Parse(r.Param);
                r.Filters = r.Filters.Push(new TableDataFilter
                {
                    ColName = nameof(DbAuditApiCall.ClientId),
                    Op = TableDataFilterOps.Equals,
                    Value = r.Param
                });
                RemoveAllButLast(ref r.Filters, nameof(DbAuditApiCall.ClientId));
            }
            var ret = await Db.GetAsTableData<DbAuditApiCall>(r, 2000).ConfigureAwait(false);
            if (r.Param != null)
                if (ret.ModifyColumns(out var cols))
                    cols[Col_DbAuditApiCall_ClientId].Props |= TableDataColumnProps.Hide;
            return ret;
        }


        /// <summary>
        /// Show all client's that made an auditable API call
        /// </summary>
        /// <param name="r">Table request</param>
        /// <returns>Clients</returns>
        [WebApi]
        [WebApiAuth(AuditAuth)]
        [WebMenuTable(null, "Debug/Audit/Clients", "Clients", null, null, 0, AuditAuth)]
        [WebApiRequestCache(1)]
        public async Task<TableData> ClientTable(TableDataRequest r)
        {
            if (r.Param != null)
            {
                var id = long.Parse(r.Param);
                r.Filters = r.Filters.Push(new TableDataFilter
                {
                    ColName = nameof(DbAuditApiClient.Id),
                    Op = TableDataFilterOps.Equals,
                    Value = r.Param
                });
                RemoveAllButLast(ref r.Filters, nameof(DbAuditApiClient.Id));
            }
            var ret = await Db.GetAsTableData<DbAuditApiClient>(r, 2000).ConfigureAwait(false);
            if (r.Param != null)
                if (ret.ModifyColumns(out var cols))
                    cols[Col_DbAuditApiClient_Id].Props |= TableDataColumnProps.Hide;
            return ret;
        }

    }
}
