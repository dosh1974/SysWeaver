using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;


namespace SysWeaver
{
    /// <summary>
    /// Base class for message handlers, an output (console, debug output, file etc) that receives messages from a <see cref="MessageHost"/>.
    /// </summary>
    /// <remarks>
    /// Register an instance using <see cref="MessageHost.AddMessageHandler(MessageHandler)"/>.
    /// Depending on <see cref="Mode"/> messages are either written on the calling thread or queued and written by a background task.
    /// </remarks>
    public abstract class MessageHandler : IDisposable
    {
        /// <summary>
        /// Output a single message.
        /// </summary>
        /// <param name="message">The message to output, never null.</param>
        /// <returns>A task that completes when the message has been written. Must not return null.</returns>
        /// <remarks>
        /// In <see cref="Modes.NativeSync"/> mode this is called on the thread that added the message and must complete synchronously.
        /// In the other modes this is called from a background task, one message at a time in the normal case.
        /// </remarks>
        protected abstract Task Add(Message message);

        /// <summary>
        /// How messages are delivered to <see cref="Add(Message)"/>.
        /// </summary>
        public enum Modes
        {
            /// <summary>
            /// Use when the add method isn't async.
            /// <see cref="Add(Message)"/> is called directly on the thread that adds the message (the returned task must already be completed).
            /// </summary>
            NativeSync,
            /// <summary>
            /// Messages are queued and written by a background task, the thread adding the message never waits.
            /// </summary>
            Async,
            /// <summary>
            /// Messages are queued and written by a background task, but the thread adding the message waits for that task to complete
            /// (only if this call started the background task, if one is already running the call doesn't wait).
            /// </summary>
            ForceSync,
        };

        /// <summary>
        /// Create a message handler.
        /// </summary>
        /// <param name="mode">How messages are delivered to <see cref="Add(Message)"/>.</param>
        protected MessageHandler(Modes mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// How messages are delivered to <see cref="Add(Message)"/>.
        /// </summary>
        protected readonly Modes Mode;

        /// <summary>
        /// Wait (at most 5 seconds) for the current background processing task (if any) to complete.
        /// </summary>
        public virtual void Dispose()
        {
            CurrentTask?.Wait(5000);
        }

        /// <summary>
        /// A completed task (same as <see cref="Task.CompletedTask"/>), can be returned from <see cref="Add(Message)"/>.
        /// </summary>
        protected static readonly Task CompletedTask = Task.CompletedTask;

        async Task ProcessMessageQueue()
        {
            var messages = Messages;
            Message m;
            while (messages.TryDequeue(out m))
            {
                await Add(m).ConfigureAwait(false);
                Interlocked.Decrement(ref ProcessingCount);
            }
        }

        /// <summary>
        /// Called by the <see cref="MessageHost"/> to deliver a message according to <see cref="Mode"/>.
        /// </summary>
        /// <param name="m">The message to deliver.</param>
        /// <returns>A task that the caller must wait for (only in <see cref="Modes.ForceSync"/> mode), else null.</returns>
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
            var t = Task.Run(ProcessMessageQueue);
            CurrentTask = t;
            return (Mode == Modes.Async) || t.IsCompleted ? null : t;
        }
        internal volatile Task CurrentTask;
        int ProcessingCount;
        readonly ConcurrentQueue<Message> Messages = new ConcurrentQueue<Message>();

        /// <summary>
        /// Wait (at most 5 seconds) for the background processing task (if any) and then call <see cref="OnFlush"/>.
        /// </summary>
        internal void Flush()
        {
            CurrentTask?.Wait(5000);
            OnFlush();
        }

        /// <summary>
        /// Override to flush any buffered output, called by <see cref="MessageHost.Flush"/>.
        /// </summary>
        protected virtual void OnFlush()
        {
        }

    }

}
