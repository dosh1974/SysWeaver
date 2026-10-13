using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// An in-memory queue of incoming messages or status updates that supports long polling with a cursor (the id of the last item received).
    /// Used by text message providers to implement <see cref="ITextMessageReceiverService"/> and <see cref="ITextMessageStatusService"/>.
    /// Thread safe.
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    public sealed class TextMessageQueue<T> : IDisposable where T : class
    {
        /// <summary>
        /// Create a queue
        /// </summary>
        /// <param name="getId">Get the (unique) id of an item</param>
        /// <param name="getSystem">Get the system of an item</param>
        /// <param name="clone">Clone an item (items returned are cloned)</param>
        /// <param name="size">The maximum number of items to keep</param>
        /// <param name="timeOut">The maximum time in milli seconds to wait for new items</param>
        /// <param name="maxPoll">The maximum number of items to return per poll</param>
        /// <param name="recentMinutes">Items added within this time are returned to pollers without a (known) cursor (the time the item was added is used, not the time of the item, since events can be delivered late)</param>
        public TextMessageQueue(Func<T, String> getId, Func<T, String> getSystem, Func<T, T> clone, int size = 1000, int timeOut = 30000, int maxPoll = 100, int recentMinutes = 5)
        {
            GetId = getId;
            GetSystem = getSystem;
            CloneFn = clone;
            Size = Math.Max(10, size);
            TimeOut = Math.Max(0, timeOut);
            MaxPoll = Math.Max(1, maxPoll);
            Recent = TimeSpan.FromMinutes(Math.Max(0, recentMinutes));
        }

        public void Dispose() => Block.Dispose();

        readonly Func<T, String> GetId;
        readonly Func<T, String> GetSystem;
        readonly Func<T, T> CloneFn;
        readonly int Size;
        readonly int TimeOut;
        readonly int MaxPoll;
        readonly TimeSpan Recent;

        readonly BlockUntilChange Block = new BlockUntilChange();
        /// <summary>
        /// Oldest first
        /// </summary>
        readonly LinkedList<(T Value, DateTime Added)> Items = new();
        readonly Dictionary<String, LinkedListNode<(T Value, DateTime Added)>> ById = new(StringComparer.Ordinal);

        /// <summary>
        /// Number of items in the queue
        /// </summary>
        public int Count
        {
            get
            {
                lock (Items)
                    return Items.Count;
            }
        }

        /// <summary>
        /// Add an item (the id must be unique, items with an existing id are ignored)
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <returns>True if added</returns>
        public bool Add(T item)
        {
            var id = GetId(item);
            lock (Items)
            {
                if (ById.ContainsKey(id))
                    return false;
                ById[id] = Items.AddLast((item, DateTime.UtcNow));
                while (Items.Count > Size)
                {
                    ById.Remove(GetId(Items.First.Value.Value));
                    Items.RemoveFirst();
                }
            }
            Block.Change();
            return true;
        }

        /// <summary>
        /// Wait for items newer than the cursor
        /// </summary>
        /// <param name="lastId">The id of the last item received, null to start</param>
        /// <param name="system">If non-empty, only return items for this system</param>
        /// <returns>The items (oldest first), or null if no new items arrived before the time out</returns>
        public async Task<T[]> Poll(String lastId, String system)
        {
            bool anySystem = String.IsNullOrEmpty(system);
            var cc = Block.Cc;
            for (; ; )
            {
                lock (Items)
                {
                    LinkedListNode<(T Value, DateTime Added)> n;
                    if ((lastId != null) && ById.TryGetValue(lastId, out var last))
                    {
                        n = last.Next;
                    }
                    else
                    {
                        //  No (known) cursor, only recent items
                        var cutOff = DateTime.UtcNow - Recent;
                        n = Items.Last;
                        while ((n != null) && (n.Value.Added >= cutOff))
                            n = n.Previous;
                        n = n == null ? Items.First : n.Next;
                    }
                    if (n != null)
                    {
                        List<T> res = null;
                        var mp = MaxPoll;
                        for (; n != null; n = n.Next)
                        {
                            var v = n.Value.Value;
                            //  Everything up to here have been scanned (even if not returned due to the system filter)
                            lastId = GetId(v);
                            if (!(anySystem || system.FastEquals(GetSystem(v))))
                                continue;
                            res ??= new List<T>();
                            res.Add(CloneFn(v));
                            if (res.Count >= mp)
                                break;
                        }
                        if (res != null)
                            return res.ToArray();
                    }
                }
                if (TimeOut <= 0)
                    return null;
                var oldCC = cc;
                cc = await Block.WaitForChange(cc, TimeOut).ConfigureAwait(false);
                if (oldCC == cc)
                    return null;
            }
        }
    }
}
