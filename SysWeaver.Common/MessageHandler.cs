using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;


namespace SysWeaver
{
    /// <summary>
    /// Base class for message handlers
    /// </summary>
    public abstract class MessageHandler : IDisposable
    {
        protected abstract ValueTask Add(Message message);

        public enum Modes
        {
            /// <summary>
            /// Use when the add method isn't async
            /// </summary>
            NativeSync,
            Async,
            ForceSync,
        };

        protected MessageHandler(Modes mode)
        {
            Mode = mode;
        }
        protected readonly Modes Mode;

        public virtual void Dispose()
        {
            CurrentTask?.Wait(5000);
        }

        protected static readonly ValueTask CompletedTask = default;

        async ValueTask ProcessMessageQueue()
        {
            var messages = Messages;
            Message m;
            while (messages.TryDequeue(out m))
            {
                await Add(m).ConfigureAwait(false);
                Interlocked.Decrement(ref ProcessingCount);
            }
        }

        internal Task StartProcessing(Message m)
        {
            if (Mode == Modes.NativeSync)
            {
                var tt = Add(m);
                Debug.Assert(tt.IsCompleted);
                return null;
            }
            Messages.Enqueue(m);
            if (Interlocked.Increment(ref ProcessingCount) != 1)
                return null;
            var t = Task.Run(() => ProcessMessageQueue().ConfigureAwait(false));
            CurrentTask = t;
            return (Mode == Modes.Async) || t.IsCompleted ? null : t;
        }
        internal volatile Task CurrentTask;
        int ProcessingCount;
        readonly ConcurrentQueue<Message> Messages = new ConcurrentQueue<Message>();

        internal void Flush()
        {
            CurrentTask?.Wait(5000);
            OnFlush();
        }
        protected virtual void OnFlush()
        {
        }

    }

}
