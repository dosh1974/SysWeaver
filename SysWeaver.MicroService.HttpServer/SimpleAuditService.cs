using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Stores audited API calls in files on disc (no database needed) and makes them searchable in the audit browser.
    /// Old data is pruned automatically, the files are distributed over multiple folders (load balancing).
    /// </summary>
    /// <remarks>
    /// Intended for a moderate number of audited calls (searches reads the files within the searched time range), use the MySqlApiAuditService if there are a lot of calls or many servers.
    /// The session tokens are stored redacted.
    /// The audit data is written in clear text, the default folders can be accessed by all users on the machine (see <see cref="Folders.AllAppFolders"/>), use other folders if that is a problem.
    /// </remarks>
    [IsMicroService]
    [WebApiUrl("Audit")]
    [WebMenuEmbedded(null, "Admin/AuditBrowser", "Audit browser", ApiAuditTools.BrowserUrl, "Search the audited API calls: who did what, when and from where", "icons/fingerprint.svg", 10, ApiAuditTools.ReadAuth)]
    public sealed class SimpleAuditService : IApiAuditService, IApiAuditReportService, IDisposable
    {
        public override string ToString() => String.Concat("[Service] Audit files: ", Storage.Count, ", size: ", Storage.Size >> 10, " kB, retention: ", RetentionDays, " days");

        /// <summary>
        /// Create the service
        /// </summary>
        /// <param name="msg">Optional message host</param>
        /// <param name="p">Parameters, null uses the defaults</param>
        public SimpleAuditService(IMessageHost msg = null, SimpleAuditParams p = null)
        {
            p = p ?? new SimpleAuditParams();
            Msg = msg;
            var folders = p.Folders;
            if ((folders?.Length ?? 0) <= 0)
                folders = Folders.Append(Folders.AllAppFolders, "Audit");
            else
                folders = Folders.Append(folders, null);
            RetentionDays = Math.Max(0, p.RetentionDays);
            MaxDataLength = Math.Max(16, p.MaxDataLength);
            Storage = new SimpleAuditStorage(msg, folders, Math.Max(1, p.MaxFileSize) * 1024L, Math.Max(0, p.CacheSize) * 1024L * 1024L);
            WriteTask = new PeriodicTask(WriteData, Math.Max(1, p.WriteInterval));
            if (RetentionDays > 0)
                PruneTask = new PeriodicTask(Prune, 60 * 60 * 1000);
        }

        readonly IMessageHost Msg;
        readonly SimpleAuditStorage Storage;
        readonly int RetentionDays;
        readonly int MaxDataLength;

        PeriodicTask WriteTask;
        PeriodicTask PruneTask;

        volatile bool IsDisposing;

        /// <summary>
        /// Stops accepting audit data and writes the pending data
        /// </summary>
        public void Dispose()
        {
            IsDisposing = true;
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            var w = Interlocked.Exchange(ref WriteTask, null);
            if (w == null)
                return;
            w.Dispose();
            WriteData();
        }

        #region IApiAuditService

        /// <summary>
        /// An audit event
        /// </summary>
        sealed class Event
        {
            public ApiAuditStates State;
            public long Id;
            public DateTime Time;
            public Object Value;
            public Auth.Authorization Auth;
            public IHttpApiAudit Api;
            public String Session;
            public String Ip;
            public String DeviceId;
            public String Language;
            public String TimeZone;
            public String UserAgent;
        }

        readonly ConcurrentQueue<Event> Events = new();

        /// <inheritdoc />
        public void OnApiBegin(long id, HttpServerRequest r, IHttpApiAudit api, object value)
        {
            if (IsDisposing)
                return;
            var s = r.Session;
            Events.Enqueue(new Event
            {
                State = ApiAuditStates.InProgress,
                Id = id,
                Time = DateTime.UtcNow,
                Value = value,
                Auth = s.Auth,
                Api = api,
                Session = ApiAuditTools.RedactSession(s.Token),
                Ip = r.GetIpAddress(),
                DeviceId = s.DeviceId,
                Language = s.ClientLanguage,
                TimeZone = s.ClientTimeZone,
                UserAgent = s.UserAgent,
            });
        }

        /// <inheritdoc />
        public void OnApiEnd(long id, HttpServerRequest r, IHttpApiAudit api, object value)
            => OnEnd(ApiAuditStates.Completed, id, r, value);

        /// <inheritdoc />
        public void OnApiException(long id, HttpServerRequest r, IHttpApiAudit api, Exception ex)
            => OnEnd(ApiAuditStates.Failed, id, r, ex);

        void OnEnd(ApiAuditStates state, long id, HttpServerRequest r, Object value)
        {
            if (IsDisposing)
                return;
            Events.Enqueue(new Event
            {
                State = state,
                Id = id,
                Time = DateTime.UtcNow,
                Value = value,
                Auth = r.Session.Auth,
            });
        }

        /// <inheritdoc />
        public Task Flush()
        {
            WriteData();
            return Task.CompletedTask;
        }

        #endregion//IApiAuditService

        #region Write

        /// <summary>
        /// Maximum number of records to keep when they can't be written
        /// </summary>
        const int MaxPending = 10000;

        readonly Object WriteLock = new();

        /// <summary>
        /// Records that haven't been written (only used within the <see cref="WriteLock"/>)
        /// </summary>
        List<SimpleAuditStorage.Record> Pending = new();

        bool WriteFailed;

        readonly ITextSerializerType JsonSer = SerManager.GetText("json");

        bool WriteData()
        {
            lock (WriteLock)
            {
                var pending = Pending;
                Dictionary<long, SimpleAuditStorage.Record> began = new();
                foreach (var x in pending)
                    if (x.T == "b")
                        began[x.Id.Value] = x;
                while (Events.TryDequeue(out var e))
                {
                    if (e.State == ApiAuditStates.InProgress)
                    {
                        var agent = e.UserAgent.LimitLength(1024, "");
                        var r = new SimpleAuditStorage.Record
                        {
                            T = "b",
                            Id = e.Id,
                            Begin = e.Time,
                            Api = e.Api.Uri,
                            Group = e.Api.AuditGroup,
                            User = e.Auth?.Username,
                            Guid = e.Auth?.Guid,
                            Session = e.Session,
                            In = GetText(e.Value),
                            Ip = e.Ip,
                            Device = e.DeviceId,
                            Lang = e.Language,
                            Tz = e.TimeZone,
                            Agent = agent,
                            Client = SimpleAuditStorage.GetClientId(e.Ip, e.DeviceId, e.Language, e.TimeZone, agent),
                        };
                        began[e.Id] = r;
                        pending.Add(r);
                        continue;
                    }
                    //  Began and ended before anything was written: a single record
                    if (began.TryGetValue(e.Id, out var x))
                        x.T = "a";
                    else
                    {
                        x = new SimpleAuditStorage.Record
                        {
                            T = "e",
                            Id = e.Id,
                        };
                        pending.Add(x);
                    }
                    x.End = e.Time;
                    x.State = (int)e.State;
                    x.User = e.Auth?.Username;
                    x.Guid = e.Auth?.Guid;
                    x.Out = GetText(e.Value);
                }
                if (pending.Count <= 0)
                    return true;
                try
                {
                    Storage.Write(pending);
                    Pending = new();
                    WriteFailed = false;
                }
                catch (Exception ex)
                {
                    //  Try again the next time
                    if (!WriteFailed)
                        Msg?.AddMessage("[SimpleAudit] Failed to write audit data!", ex);
                    WriteFailed = true;
                    if (pending.Count > MaxPending)
                        pending.RemoveRange(0, pending.Count - MaxPending);
                }
                return true;
            }
        }

        String GetText(Object value)
        {
            if (value == null)
                return null;
            try
            {
                var s = value is Exception ex ? ex.Message : JsonSer.ToStringWithoutType(value);
                return s.LimitLength(MaxDataLength, "");
            }
            catch (Exception ex)
            {
                //  Never lose an audit entry because of a value
                return ("Failed to serialize " + value.GetType().FullName + ": " + ex.Message).LimitLength(MaxDataLength, "");
            }
        }

        bool Prune()
        {
            var count = Storage.Prune(RetentionStart.Value);
            if (count > 0)
                Msg?.AddMessage(MessageLevels.Debug, $"[SimpleAudit] Deleted {count} old audit file(s)");
            return true;
        }

        #endregion//Write

        #region IApiAuditReportService

        /// <summary>
        /// Entries that began before this are pruned, null if entries are kept forever
        /// </summary>
        DateTime? RetentionStart => RetentionDays > 0 ? DateTime.UtcNow.AddDays(-RetentionDays) : null;

        /// <summary>
        /// Find all matching entries, newest first (very close to)
        /// </summary>
        IEnumerable<ApiAuditEntry> Find(ApiAuditMatcher m)
        {
            var start = RetentionStart;
            var from = (m.From == null) || ((start != null) && (start.Value > m.From.Value)) ? start : m.From;
            foreach (var e in Storage.Read(from, m.To))
                if (((start == null) || (e.Begin >= start.Value)) && m.Match(e))
                    yield return e;
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        public Task<ApiAuditInfo> GetInfo()
            => Task.FromResult(new ApiAuditInfo
            {
                Storage = "Files",
                RetentionDays = RetentionDays,
                MaxDataLength = MaxDataLength,
            });

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public Task<ApiAuditPage> Search(ApiAuditSearch search)
        {
            ApiAuditTools.GetPaging(search, out var row, out var count, out var lookAhead);
            var order = ApiAuditTools.GetOrder(search?.Order);
            var found = Find(new ApiAuditMatcher(search));
            //  Found newest first, so only a part must be read
            var (field, desc) = order[0];
            if (!desc || (field.Name != nameof(ApiAuditEntry.Id)))
            {
                var all = found.ToList();
                all.Sort(ApiAuditTools.GetComparison(order));
                found = all;
            }
            var res = found.Skip(row).Take(count + lookAhead).ToArray();
            return Task.FromResult(new ApiAuditPage
            {
                RowCount = row + res.Length,
                Entries = res.Length > count ? res[..count] : res,
            });
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        public Task<ApiAuditEntry> GetEntry(long id)
        {
            var start = RetentionStart;
            return Task.FromResult(Storage.Read(start, null).FirstOrDefault(x => (x.Id == id) && ((start == null) || (x.Begin >= start.Value))));
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public Task<ApiAuditFacet[]> GetFacets(ApiAuditFacetRequest request)
        {
            var max = Math.Clamp(request?.MaxValues ?? 10, 1, ApiAuditTools.MaxFacetValues);
            List<ApiAuditFacet> res = new();
            foreach (var name in (request?.Fields ?? []).Distinct(StringComparer.Ordinal))
            {
                var field = ApiAuditField.Get(name);
                if (field == null)
                    continue;
                Dictionary<String, ApiAuditFacetValue> values = new(StringComparer.Ordinal);
                foreach (var e in Find(new ApiAuditMatcher(request, name)))
                {
                    var t = field.GetText(e);
                    if (!values.TryGetValue(t, out var v))
                        values[t] = v = new ApiAuditFacetValue { Value = t };
                    ++v.Count;
                    if (e.State == ApiAuditStates.Failed)
                        ++v.Failed;
                }
                res.Add(new ApiAuditFacet
                {
                    Field = field.Name,
                    Values = values.Values.OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(max).ToArray(),
                    More = values.Count > max,
                });
            }
            return Task.FromResult(res.ToArray());
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public Task<ApiAuditSummary> GetSummary(ApiAuditSummaryRequest request)
        {
            var m = new ApiAuditMatcher(request);
            var res = new ApiAuditSummary
            {
                Steps = [],
            };
            HashSet<String> users = new(StringComparer.Ordinal);
            HashSet<long> clients = new();
            //  Only what is needed, there may be a lot of entries
            List<(DateTime Begin, ApiAuditStates State)> times = new();
            double sum = 0;
            long ended = 0;
            foreach (var e in Find(m))
            {
                times.Add((e.Begin, e.State));
                switch (e.State)
                {
                    case ApiAuditStates.Completed:
                        ++res.Completed;
                        break;
                    case ApiAuditStates.Failed:
                        ++res.Failed;
                        break;
                    default:
                        ++res.InProgress;
                        break;
                }
                if (!String.IsNullOrEmpty(e.UserName))
                    users.Add(e.UserName);
                clients.Add(e.ClientId);
                if ((res.First == null) || (e.Begin < res.First.Value))
                    res.First = e.Begin;
                if ((res.Last == null) || (e.Begin > res.Last.Value))
                    res.Last = e.Begin;
                if (e.Duration == null)
                    continue;
                var d = e.Duration.Value;
                sum += d;
                ++ended;
                res.MaxDuration = Math.Max(d, res.MaxDuration ?? 0);
            }
            res.Users = users.Count;
            res.Clients = clients.Count;
            if (ended > 0)
                res.AverageDuration = sum / ended;
            if (times.Count <= 0)
                return Task.FromResult(res);
            ApiAuditTools.GetTimeLine(m, res, out var from, out var to);
            var stepSeconds = ApiAuditTools.GetStepSeconds(from, to, request?.Steps ?? 60);
            Dictionary<long, ApiAuditStep> steps = new();
            foreach (var (begin, state) in times)
            {
                var i = ApiAuditTools.GetStepIndex(begin, stepSeconds);
                if (!steps.TryGetValue(i, out var s))
                    steps[i] = s = new ApiAuditStep();
                switch (state)
                {
                    case ApiAuditStates.Completed:
                        ++s.Completed;
                        break;
                    case ApiAuditStates.Failed:
                        ++s.Failed;
                        break;
                    default:
                        ++s.InProgress;
                        break;
                }
            }
            ApiAuditTools.SetSteps(res, m, stepSeconds, steps);
            return Task.FromResult(res);
        }

        /// <inheritdoc />
        [WebApi]
        [WebApiAuth(ApiAuditTools.ReadAuth)]
        [WebApiRequestCache(1)]
        public Task<TableData> AuditTable(TableDataRequest r)
            => ApiAuditTools.GetTable(this, r);

        #endregion//IApiAuditReportService

    }

}
