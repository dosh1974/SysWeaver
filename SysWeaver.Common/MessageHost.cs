using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace SysWeaver
{

    /// <summary>
    /// The default <see cref="IMessageHost"/> implementation: filters messages by level, keeps the latest 1000 messages in memory
    /// and dispatches every accepted message to all registered <see cref="MessageHandler"/> instances.
    /// </summary>
    /// <remarks>
    /// Thread safe. Handlers can be added and removed at any time.
    /// Indentation applied by <see cref="Tab(int)"/> is shared by all threads using the host.
    /// </remarks>
    public class MessageHost : IMessageHost
    {
        /// <summary>
        /// Create a message host without any handlers.
        /// </summary>
        /// <param name="depth">Initial indentation in spaces (negative values are treated as 0).</param>
        public MessageHost(int depth = 0)
        {
            InternalTabSpaces = Math.Max(0, depth);
        }

        /// <summary>
        /// Create a message host with some initial handlers.
        /// </summary>
        /// <param name="handlers">The handlers to add (duplicates are ignored).</param>
        /// <exception cref="ArgumentNullException">One of the <paramref name="handlers"/> is null.</exception>
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
        /// The default value of <see cref="AcceptMessageAbove"/>: <see cref="MessageLevels.All"/> in debug builds (all messages accepted)
        /// and <see cref="MessageLevels.Debug"/> in release builds (debug messages are discarded).
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
        /// Add a new message, it's discarded if <paramref name="level"/> isn't above <see cref="AcceptMessageAbove"/>.
        /// </summary>
        /// <param name="message">The text to add (may not be null). A leading "[Prefix]" is treated as a prefix and aligned when rendered.</param>
        /// <param name="level">Optional message level</param>
        /// <remarks>
        /// Handlers in <see cref="MessageHandler.Modes.NativeSync"/> mode write on the calling thread,
        /// and the call blocks on handlers in <see cref="MessageHandler.Modes.ForceSync"/> mode.
        /// </remarks>
        /// <exception cref="NullReferenceException"><paramref name="message"/> is null (and the message is accepted).</exception>
        public void AddMessage(String message, MessageLevels level = MessageLevels.Info)
        {
            if (level > AcceptMessageAbove)
                Process(new Message(message, null, level, Interlocked.Increment(ref CurrentId), InternalTabSpaces));
        }

        /// <summary>
        /// Add a new message with an exception, it's discarded if <paramref name="level"/> isn't above <see cref="AcceptMessageAbove"/>.
        /// </summary>
        /// <param name="message">The text to add (may not be null). A leading "[Prefix]" is treated as a prefix and aligned when rendered.</param>
        /// <param name="ex">An exception (may be null), inner exceptions are included when rendered</param>
        /// <param name="level">Optional message level</param>
        /// <exception cref="NullReferenceException"><paramref name="message"/> is null (and the message is accepted).</exception>
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
                WaitAll(syncTasks);
        }

        /// <summary>
        /// Wait for all tasks to complete, failed tasks (a handler's Add threw) are ignored
        /// </summary>
        /// <param name="tasks">The tasks to wait for</param>
        static void WaitAll(List<Task> tasks)
        {
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException)
            {
            }
        }


        readonly ConcurrentQueue<Message> InternalMessages = new ConcurrentQueue<Message>();

        /// <summary>
        /// The most recent accepted messages (at most around 1000), oldest first.
        /// </summary>
        /// <remarks>
        /// This is a live view of a concurrent queue, enumeration is thread safe and returns a moment-in-time snapshot.
        /// </remarks>
        public IEnumerable<Message> Messages => InternalMessages;

        /// <summary>
        /// Number of spaces per tab, used by <see cref="Tab(int)"/>
        /// </summary>
        public int TabSpaces { get; set; } = 4;

        int InternalTabSpaces;

        /// <summary>
        /// Tabulate in, dispose the returned value to "un tab"
        /// </summary>
        /// <param name="count">Number of tabs to apply (values less than 1 are treated as 1)</param>
        /// <returns>An object that should be disposed (exactly once) to "un tab"</returns>
        /// <remarks>
        /// The indentation is global for the host (not per thread or async flow), so concurrent code using tabs will affect each other.
        /// The number of spaces is computed using <see cref="TabSpaces"/> at the time of the call.
        /// </remarks>
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
        /// <remarks>
        /// Blocks the calling thread (without a timeout for the background tasks).
        /// </remarks>
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
            WaitAll(tasks);
            foreach (var c in mh)
                c.Key.Flush();
        }
    


    }

}
