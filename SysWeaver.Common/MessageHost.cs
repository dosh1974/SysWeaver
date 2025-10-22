using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace SysWeaver
{

    public class MessageHost : IMessageHost
    {
        public MessageHost(int depth = 0)
        {
            InternalTabSpaces = Math.Max(0, depth);
        }

        public MessageHost(params MessageHandler[] handlers)
        {
            foreach (var h in handlers)
                MessageHandlers.TryAdd(h, 0);
        }

        /// <summary>
        /// Add a message handler to the message host
        /// </summary>
        /// <param name="h">Message handler instance</param>
        /// <returns>True if it was added, else false (it already existed)</returns>
        public bool AddMessageHandler(MessageHandler h) => h == null ? false : MessageHandlers.TryAdd(h, 0);

        /// <summary>
        /// Remove a message handler from the message host
        /// </summary>
        /// <param name="h">Message handler instance</param>
        /// <returns>True if it was removed, else false (it didn't exist)</returns>
        public bool RemoveMessageHandler(MessageHandler h) => h == null ? false : MessageHandlers.TryRemove(h, out var _);

        readonly ConcurrentDictionary<MessageHandler, int> MessageHandlers = new ();

        /// <summary>
        /// The default message filter level used
        /// </summary>
#if DEBUG
        public const MessageLevels DefaultMessageLevel = MessageLevels.All;
        #else//DEBUG
        public const MessageLevels DefaultMessageLevel = MessageLevels.Debug;
        #endif//DEBUG

        /// <summary>
        /// Only accept message with a message level above this value
        /// </summary>
        public MessageLevels AcceptMessageAbove { get; set; } = DefaultMessageLevel;

        /// <summary>
        /// Add a new message
        /// </summary>
        /// <param name="message">The text to add</param>
        /// <param name="level">Optional message level</param>
        public void AddMessage(String message, MessageLevels level = MessageLevels.Info)
        {
            if (level > AcceptMessageAbove)
                Process(new Message(message, null, level, Interlocked.Increment(ref CurrentId), InternalTabSpaces));
        }

        /// <summary>
        /// Add a new message
        /// </summary>
        /// <param name="message">The text to add</param>
        /// <param name="ex">An exception</param>
        /// <param name="level">Optional message level</param>
        public void AddMessage(String message, Exception ex, MessageLevels level = MessageLevels.Error)
        {
            if (level > AcceptMessageAbove)
                Process(new Message(message, ex, level, Interlocked.Increment(ref CurrentId), InternalTabSpaces));
        }

        void Process(Message m)
        {
            var ms = InternalMessages;
            ms.Enqueue(m);
            while (ms.Count > 1000)
                ms.TryDequeue(out var _);

            var mh = MessageHandlers;
            List<Task> syncTasks = new List<Task>(mh.Count);
            foreach (var h in mh)
            {
                var t = h.Key.StartProcessing(m);
                if (t != null)
                    syncTasks.Add(t);
            }
            if (syncTasks.Count > 0)
                Task.WaitAll(syncTasks.ToArray());
        }


        readonly ConcurrentQueue<Message> InternalMessages = new ConcurrentQueue<Message>();

        public IEnumerable<Message> Messages => InternalMessages;

        /// <summary>
        /// Number of spaces per tab
        /// </summary>
        public int TabSpaces { get; set; } = 4;

        int InternalTabSpaces;

        /// <summary>
        /// Tabulate in, dispose the returned value to "un tab"
        /// </summary>
        /// <param name="count">Number of tabs to apply</param>
        /// <returns>An object that should be disposed to "un tab"</returns>
        public IDisposable Tab(int count = 1)
        {
            if (count < 1)
                count = 1;
            count *= TabSpaces;
            Interlocked.Add(ref InternalTabSpaces, count);
            return new UnTab(this, count);
        }

        readonly struct UnTab : IDisposable
        {
            public readonly MessageHost Host;
            public readonly int Count;

            public UnTab(MessageHost host, int count)
            {
                Host = host;
                Count = count;
            }

            public void Dispose()
            {
                Interlocked.Add(ref Host.InternalTabSpaces, -Count);
            }
        }

        long CurrentId;

        /// <summary>
        /// Wait for all async handling to complete, and call flush on all message handlers
        /// </summary>
        public void Flush()
        {
            List<Task> tasks = new List<Task>();
            var mh = MessageHandlers;
            foreach (var c in mh)
            {
                var t = c.Key.CurrentTask;
                if (t != null)
                    tasks.Add(t);
            }
            Task.WaitAll(tasks.ToArray());
            foreach (var c in mh)
                c.Key.Flush();
        }
    


    }

}
