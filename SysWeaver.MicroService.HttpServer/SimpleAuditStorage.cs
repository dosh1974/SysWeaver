using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// The file storage of the <see cref="SimpleAuditService"/>.
    /// Entries are appended to segments: text files with one json object per line (see <see cref="Record"/>), named "audit_[UTC time of the first entry]_[unique].jsonl".
    /// A new segment is started when the current is too large, every day and when the process is started.
    /// Every segment is stored in one of the folders, selected using the file name (<see cref="Folders.SelectFolder(IReadOnlyList{string}, string)"/>).
    /// The completion of a call is written to the segment that the call began in, so a segment can be read and deleted without any other segments.
    /// </summary>
    sealed class SimpleAuditStorage
    {

        /// <summary>
        /// A line in a segment, what members are used depends on the type
        /// </summary>
        internal sealed class Record
        {
            /// <summary>
            /// "c" = A client, must be written before any call that uses it.
            /// "b" = A call began.
            /// "e" = A call ended.
            /// "a" = A call began and ended.
            /// </summary>
            public String T;
            /// <summary>
            /// Id of the call (b, e, a)
            /// </summary>
            public long? Id;
            /// <summary>
            /// Id of the client (c, b, a)
            /// </summary>
            public long? Client;
            /// <summary>
            /// When the call began (b, a)
            /// </summary>
            public DateTime? Begin;
            /// <summary>
            /// When the call ended (e, a)
            /// </summary>
            public DateTime? End;
            /// <summary>
            /// <see cref="ApiAuditStates"/> (e, a)
            /// </summary>
            public int? State;
            public String Api;
            public String Group;
            public String User;
            public String Guid;
            public String Session;
            public String In;
            public String Out;
            public String Ip;
            public String Device;
            public String Lang;
            public String Tz;
            public String Agent;
        }

        sealed class Segment
        {
            public String Filename;
            /// <summary>
            /// When the first entry began
            /// </summary>
            public DateTime Start;
            /// <summary>
            /// No entry began after this
            /// </summary>
            public DateTime LastWrite;
            /// <summary>
            /// Number of (complete) bytes in the file
            /// </summary>
            public long Length;
            /// <summary>
            /// True if the last write failed (an incomplete line may have been written)
            /// </summary>
            public bool Failed;
            /// <summary>
            /// The clients that are written (only for segments created by this process)
            /// </summary>
            public HashSet<long> Clients;

            /// <summary>
            /// Cached entries (must not be modified), null if not cached
            /// </summary>
            public ApiAuditEntry[] Entries;
            /// <summary>
            /// The length of the file when the entries was cached
            /// </summary>
            public long EntriesLength;
            /// <summary>
            /// When the cached entries was used
            /// </summary>
            public long Used;
        }

        const String Prefix = "audit_";
        const String Ext = ".jsonl";
        const String NameFormat = "yyyyMMdd'T'HHmmssfff";

        /// <summary>
        /// Times are compared with this margin, an entry may be written slightly after a later entry
        /// </summary>
        static readonly TimeSpan Margin = TimeSpan.FromMinutes(1);

        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// Create the storage, finds existing segments
        /// </summary>
        /// <param name="msg">Optional message host</param>
        /// <param name="folders">The folders to use (must exist)</param>
        /// <param name="maxFileSize">Start a new segment when the current is larger than this</param>
        /// <param name="cacheSize">Maximum number of file bytes to keep parsed in memory</param>
        public SimpleAuditStorage(IMessageHost msg, IReadOnlyList<String> folders, long maxFileSize, long cacheSize)
        {
            Msg = msg;
            StorageFolders = folders;
            MaxFileSize = maxFileSize;
            CacheSize = cacheSize;
            foreach (var folder in folders.Distinct(StringComparer.Ordinal))
            {
                foreach (var f in new DirectoryInfo(folder).EnumerateFiles(Prefix + "*" + Ext))
                {
                    var p = f.Name.Substring(Prefix.Length).Split('_');
                    if ((p.Length != 2) || !DateTime.TryParseExact(p[0], NameFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start))
                        continue;
                    Segments.Add(new Segment
                    {
                        Filename = f.FullName,
                        Start = start,
                        LastWrite = f.LastWriteTimeUtc,
                        Length = f.Length,
                    });
                }
            }
        }

        readonly IMessageHost Msg;
        readonly IReadOnlyList<String> StorageFolders;
        readonly long MaxFileSize;
        readonly long CacheSize;

        /// <summary>
        /// All segments, also the lock for all members of the segments
        /// </summary>
        readonly List<Segment> Segments = new();

        /// <summary>
        /// The segment that new calls are written to, only used when writing
        /// </summary>
        Segment Current;

        /// <summary>
        /// The segments of the calls in progress, only used when writing (and pruning)
        /// </summary>
        readonly Dictionary<long, Segment> InProgress = new();

        readonly Object WriteLock = new();

        /// <summary>
        /// Number of segments
        /// </summary>
        public int Count
        {
            get
            {
                lock (Segments)
                    return Segments.Count;
            }
        }

        /// <summary>
        /// Number of bytes of all segments
        /// </summary>
        public long Size
        {
            get
            {
                lock (Segments)
                    return Segments.Sum(x => x.Length);
            }
        }

        /// <summary>
        /// Get the id of a client
        /// </summary>
        /// <param name="ip">Ip address</param>
        /// <param name="device">Device id</param>
        /// <param name="lang">Language</param>
        /// <param name="tz">Time zone</param>
        /// <param name="agent">User agent</param>
        /// <returns>The id, positive and exact as a double (java script)</returns>
        public static long GetClientId(String ip, String device, String lang, String tz, String agent)
        {
            var h = SHA256.HashData(Encoding.UTF8.GetBytes(String.Join('\n', ip, device, lang, tz, agent)));
            return (BitConverter.ToInt64(h, 0) & ((1L << 52) - 1)) | 1;
        }

        #region Write

        /// <summary>
        /// Write calls
        /// </summary>
        /// <param name="calls">The calls to write, in the order that they began ("b" or "a" records, all call and client members must be set) or ended ("e" records).</param>
        /// <exception cref="Exception">Nothing could be written to any of the folders</exception>
        public void Write(IReadOnlyList<Record> calls)
        {
            lock (WriteLock)
            {
                //  Ended calls, written to the segment that they began in
                foreach (var g in calls.Where(x => x.T == "e").GroupBy(x => InProgress.TryGetValue(x.Id.Value, out var s) ? s : null))
                {
                    //  Unknown or pruned
                    if (g.Key == null)
                        continue;
                    var t = new StringBuilder();
                    foreach (var x in g)
                        Add(t, x);
                    //  A file that can't be written (ex: the disc is gone) may not stop new calls from being written
                    for (int retry = 0; ; ++retry)
                    {
                        try
                        {
                            Append(g.Key, t);
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (retry < 3)
                            {
                                Thread.Sleep(50);
                                continue;
                            }
                            Msg?.AddMessage("[SimpleAudit] Failed to write the result of " + g.Count() + " calls to " + g.Key.Filename.ToQuoted(), ex);
                            break;
                        }
                    }
                    foreach (var x in g)
                        InProgress.Remove(x.Id.Value);
                }
                //  New calls
                var n = calls.Where(x => x.T != "e").ToArray();
                if (n.Length <= 0)
                    return;
                var start = n[0].Begin.Value;
                var cur = Current;
                if ((cur == null) || (cur.Length >= MaxFileSize) || (cur.Start.Date != start.Date))
                    cur = null;
                if (cur != null)
                {
                    try
                    {
                        WriteCalls(cur, n);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Msg?.AddMessage("[SimpleAudit] Failed to write to " + cur.Filename.ToQuoted() + ", trying a new file", ex, MessageLevels.Warning);
                    }
                }
                //  Start a new segment, if it fails (ex: a disc is full) try the other folders
                Current = null;
                var name = String.Concat(Prefix, start.ToString(NameFormat, CultureInfo.InvariantCulture), '_', Guid.NewGuid().ToString("N").Substring(0, 8), Ext);
                var first = Folders.SelectFolder(StorageFolders, name);
                Exception fail = null;
                foreach (var folder in StorageFolders.OrderBy(x => x == first ? 0 : 1))
                {
                    var s = new Segment
                    {
                        Filename = Path.Combine(folder, name),
                        Start = start,
                        Clients = new(),
                    };
                    try
                    {
                        PathExt.EnsureFolderExist(folder);
                        WriteCalls(s, n);
                        Current = s;
                        return;
                    }
                    catch (Exception ex)
                    {
                        fail = ex;
                    }
                }
                throw fail;
            }
        }

        void WriteCalls(Segment s, Record[] calls)
        {
            var t = new StringBuilder();
            var clients = new HashSet<long>();
            foreach (var x in calls)
            {
                var c = x.Client.Value;
                if (!s.Clients.Contains(c) && clients.Add(c))
                    Add(t, new Record
                    {
                        T = "c",
                        Client = c,
                        Ip = x.Ip,
                        Device = x.Device,
                        Lang = x.Lang,
                        Tz = x.Tz,
                        Agent = x.Agent,
                    });
                Add(t, new Record
                {
                    T = x.T,
                    Id = x.Id,
                    Client = c,
                    Begin = x.Begin,
                    End = x.End,
                    State = x.State,
                    Api = x.Api,
                    Group = x.Group,
                    User = x.User,
                    Guid = x.Guid,
                    Session = x.Session,
                    In = x.In,
                    Out = x.Out,
                });
            }
            Append(s, t);
            s.Clients.UnionWith(clients);
            foreach (var x in calls)
                if (x.T == "b")
                    InProgress[x.Id.Value] = s;
        }

        static void Add(StringBuilder t, Record r)
            => t.Append(JsonSerializer.Serialize(r, Options)).Append('\n');

        void Append(Segment s, StringBuilder text)
        {
            var isNew = s.Length == 0;
            if (s.Failed)
                text.Insert(0, '\n');
            var data = Encoding.UTF8.GetBytes(text.ToString());
            long length;
            try
            {
                using var f = new FileStream(s.Filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                f.Write(data);
                f.Flush();
                length = f.Position;
            }
            catch
            {
                s.Failed = true;
                throw;
            }
            lock (Segments)
            {
                s.Failed = false;
                s.Length = length;
                s.LastWrite = DateTime.UtcNow;
                if (isNew)
                    Segments.Add(s);
            }
        }

        #endregion//Write

        #region Read

        /// <summary>
        /// Get entries, newest segment first (and newest entry first in each segment), only the segments needed are read
        /// </summary>
        /// <param name="from">Optional start, some earlier entries may be returned</param>
        /// <param name="to">Optional end, some later entries may be returned</param>
        /// <returns>Entries, must not be modified</returns>
        public IEnumerable<ApiAuditEntry> Read(DateTime? from, DateTime? to)
        {
            Segment[] segs;
            lock (Segments)
                segs = Segments
                    .Where(x => ((from == null) || (x.LastWrite >= Sub(from.Value))) && ((to == null) || (x.Start < Add(to.Value))))
                    .OrderByDescending(x => x.Start)
                    .ToArray();
            foreach (var s in segs)
                foreach (var e in ReadSegment(s))
                    yield return e;
        }

        static DateTime Sub(DateTime t) => t.Ticks > Margin.Ticks ? t - Margin : t;

        static DateTime Add(DateTime t) => t < DateTime.MaxValue - Margin ? t + Margin : t;

        long UseCounter;

        ApiAuditEntry[] ReadSegment(Segment s)
        {
            long length;
            lock (Segments)
            {
                length = s.Length;
                if ((s.Entries != null) && (s.EntriesLength == length))
                {
                    s.Used = ++UseCounter;
                    return s.Entries;
                }
            }
            ApiAuditEntry[] entries;
            try
            {
                entries = Parse(s.Filename, length);
            }
            catch (FileNotFoundException)
            {
                //  Pruned
                return [];
            }
            catch (Exception ex)
            {
                Msg?.AddMessage("[SimpleAudit] Failed to read " + s.Filename.ToQuoted(), ex, MessageLevels.Warning);
                return [];
            }
            lock (Segments)
            {
                s.Entries = entries;
                s.EntriesLength = length;
                s.Used = ++UseCounter;
                //  Remove the least recently used entries from the cache
                var cached = Segments.Where(x => x.Entries != null).OrderByDescending(x => x.Used).ToArray();
                long total = 0;
                foreach (var x in cached)
                {
                    total += x.EntriesLength;
                    if (total > CacheSize)
                        x.Entries = null;
                }
            }
            return entries;
        }

        static ApiAuditEntry[] Parse(String filename, long length)
        {
            var data = new Byte[length];
            using (var f = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                data = data[..f.ReadAtLeast(data, data.Length, false)];
            Dictionary<long, Record> clients = new();
            Dictionary<long, ApiAuditEntry> entries = new();
            var left = data.AsSpan();
            while (left.Length > 0)
            {
                var e = left.IndexOf((Byte)'\n');
                var line = e < 0 ? left : left[..e];
                left = e < 0 ? default : left[(e + 1)..];
                if (line.Length <= 1)
                    continue;
                Record r;
                try
                {
                    r = JsonSerializer.Deserialize<Record>(line, Options);
                }
                catch (JsonException)
                {
                    //  Incomplete line, the process was terminated or a write failed
                    continue;
                }
                if (r.T == "c")
                {
                    clients[r.Client ?? 0] = r;
                    continue;
                }
                var id = r.Id ?? 0;
                ApiAuditEntry a;
                if (r.T == "e")
                {
                    if (!entries.TryGetValue(id, out a))
                        continue;
                }
                else
                {
                    clients.TryGetValue(r.Client ?? 0, out var c);
                    a = new ApiAuditEntry
                    {
                        Id = id,
                        Begin = r.Begin ?? default,
                        Api = r.Api,
                        Group = r.Group,
                        Input = r.In,
                        Session = r.Session,
                        ClientId = r.Client ?? 0,
                        Ip = c?.Ip,
                        DeviceId = c?.Device,
                        Language = c?.Lang,
                        TimeZone = c?.Tz,
                        UserAgent = c?.Agent,
                        UserName = r.User,
                        UserGuid = r.Guid,
                    };
                    entries[id] = a;
                }
                if (r.End != null)
                {
                    a.End = r.End;
                    a.Duration = Math.Max(0, (r.End.Value - a.Begin).TotalMilliseconds);
                    a.State = (ApiAuditStates)(r.State ?? 0);
                    a.Output = r.Out;
                    //  The user may change (ex: login)
                    a.UserName = r.User;
                    a.UserGuid = r.Guid;
                }
            }
            var res = entries.Values.ToArray();
            Array.Sort(res, (a, b) => b.Id.CompareTo(a.Id));
            return res;
        }

        #endregion//Read

        /// <summary>
        /// Delete segments that only have old entries
        /// </summary>
        /// <param name="before">Delete segments where all entries began before this</param>
        /// <returns>Number of segments deleted</returns>
        public int Prune(DateTime before)
        {
            lock (WriteLock)
            {
                Segment[] old;
                lock (Segments)
                    old = Segments.Where(x => (x != Current) && (x.LastWrite < before)).ToArray();
                int count = 0;
                foreach (var s in old)
                {
                    var ex = PathExt.TryDeleteFile(s.Filename);
                    if (ex != null)
                    {
                        Msg?.AddMessage("[SimpleAudit] Failed to delete " + s.Filename.ToQuoted(), ex, MessageLevels.Warning);
                        continue;
                    }
                    lock (Segments)
                        Segments.Remove(s);
                    foreach (var id in InProgress.Where(x => x.Value == s).Select(x => x.Key).ToArray())
                        InProgress.Remove(id);
                    ++count;
                }
                return count;
            }
        }

    }

}
