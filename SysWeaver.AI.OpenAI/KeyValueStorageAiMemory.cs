using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public sealed partial class KeyValueStorageAiMemory : IAiMemory
    {

        const String Key = "AiMemory";


        static String BuildKey(HttpSession session)
           => String.Join('_', Key, session.Auth.Guid.ToHex());
        

        async Task<Mem> GetMem(HttpSession session)
        {
            if (session == null)
                throw new Exception("No session found!");
            var a = session.Auth;
            if (a == null)
                throw new Exception("Must have a user!");
            if (!a.TryGet<Mem>(Key, out var mem))
            {
                mem = new Mem();
                if (!a.TryAdd(Key, mem))
                    if (!a.TryGet<Mem>(Key, out mem))
                        throw new Exception("Internal error!");
            }
            if (mem.M != null)
                return mem;
            using var l = await mem.Lock.Lock().ConfigureAwait(false);
            //  Load from disc
            var d = await KeyValueStore.AllApp.TryGetAsync<MemEntry[]>(BuildKey(session)).ConfigureAwait(false);
            mem.M = d == null ? new (StringComparer.Ordinal) : d.ToDictionary(x => x.H.Key, StringComparer.Ordinal);
            return mem;
        }

        public async Task<AiMemoryEntry[]> GetMemoryEntries(HttpSession session)
        {
            var mem = await GetMem(session).ConfigureAwait(false);
            return mem.M.Values.Select(x => x.H).ToArray();
        }

        public async Task<bool> AddMemory(HttpSession session, string key, string desc, string value)
        {
            if (String.IsNullOrEmpty(key))
                throw new Exception("You must supply a key!");
            if (key.Length > AiMemoryLimits.MaxKeyLen)
                throw new Exception("The key \"" + key + "\" is too long, maximum length is " + AiMemoryLimits.MaxKeyLen + " chars");
            if (String.IsNullOrEmpty(desc))
                throw new Exception("Please provide a short but meaningful description");
            if (desc.Length > AiMemoryLimits.MaxDescLen)
                throw new Exception("The description is too long (" + desc.Length + " chars), maximum length is " + AiMemoryLimits.MaxDescLen + " chars");
            if (String.IsNullOrEmpty(value))
                throw new Exception("Please provide some value");
            if (value.Length > AiMemoryLimits.MaxContentLen)
                throw new Exception("The value is too long (" + value.Length + " chars), maximum length is " + AiMemoryLimits.MaxContentLen + " chars");
            var mem = await GetMem(session).ConfigureAwait(false);
            using var l = await mem.Lock.Lock().ConfigureAwait(false);
            var me = new MemEntry(key, desc, value);
            if (!mem.M.TryAdd(key, me))
            {
                mem.M[key] = me;
                await KeyValueStore.AllApp.SetAsync(BuildKey(session), mem.M.Values.ToArray()).ConfigureAwait(false);
                return true;
            }
            if (mem.M.Count >= AiMemoryLimits.MaxRecords)
                throw new Exception("Too many memory records, the maximum number of memory records is " + AiMemoryLimits.MaxRecords + ", remove some old or update an existing");
            await KeyValueStore.AllApp.SetAsync(BuildKey(session), mem.M.Values.ToArray()).ConfigureAwait(false);
            return true;
        }

        public async Task<string> GetMemory(HttpSession session, string key)
        {
            var mem = await GetMem(session).ConfigureAwait(false);
            using var l = await mem.Lock.Lock().ConfigureAwait(false);
            if (!mem.M.TryGetValue(key, out var r))
                throw new Exception("The memory \"" + key + "\" do NOT exist");
            r.H.Last = DateTime.UtcNow.Ticks;
            await KeyValueStore.AllApp.SetAsync(BuildKey(session), mem.M.Values.ToArray()).ConfigureAwait(false);
            return r.V;
        }



        public async Task<bool> RemoveMemory(HttpSession session, string key)
        {
            var mem = await GetMem(session).ConfigureAwait(false);
            using var l = await mem.Lock.Lock().ConfigureAwait(false);
            if (!mem.M.TryRemove(key, out var r))
                throw new Exception("The memory \"" + key + "\" do NOT exist");
            await KeyValueStore.AllApp.SetAsync(BuildKey(session), mem.M.Values.ToArray()).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> SetMemory(HttpSession session, string key, string value)
        {
            var mem = await GetMem(session).ConfigureAwait(false);
            using var l = await mem.Lock.Lock().ConfigureAwait(false);
            if (!mem.M.TryGetValue(key, out var r))
                throw new Exception("The memory \"" + key + "\" do NOT exist, add a new memory using AddMemory instead?");
            r.V = value;
            r.H.Last = DateTime.UtcNow.Ticks;
            await KeyValueStore.AllApp.SetAsync(BuildKey(session), mem.M.Values.ToArray()).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Displays the memory that the AI have stored
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth("")]
        [WebMenuTable(null, "AiMemory", "AI Memory", null, "../icons/brain.svg", 8)]
        public async Task<TableData> GetAiMemoryTable(TableDataRequest r, HttpServerRequest context)
        {
            var mem = await GetMem(context.Session).ConfigureAwait(false);
            if (mem == null)
                return null;
            return TableDataTools.Get(r, mem.M.Values.Select(x => new MemDebug(x)), "AI memory");
        }

        [TableDataPrimaryKey(nameof(Key))]
        public sealed class MemDebug
        {
            public MemDebug()
            {
            }
            public MemDebug(MemEntry e)
            {
                var h = e.H;
                Key = h.Key;
                Last = new DateTime(Interlocked.Read(ref h.Last), DateTimeKind.Utc);
                Desc = h.Desc;
                Value = e.V;
            }
            /// <summary>
            /// The key used to access the memory
            /// </summary>
            public String Key;
            
            /// <summary>
            /// When the memory was last accessed
            /// </summary>
            public DateTime Last;

            /// <summary>
            /// A short description / hint of the memory contents
            /// </summary>
            [TableDataText(40)]
            public String Desc;
            
            /// <summary>
            /// The content of the memory
            /// </summary>
            [TableDataMd(80)]
            public String Value;
        }

    }

}
