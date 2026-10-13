
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    public sealed class SingleThreadTaskScheduler : TaskScheduler
    {
        [ThreadStatic]
        static bool _isExecuting;

        readonly CancellationToken _cancellationToken;

        readonly BlockingCollection<Task> _taskQueue;

        readonly Lazy<Thread> _singleThread;
        public override int MaximumConcurrencyLevel => 1;

        public SingleThreadTaskScheduler(CancellationToken cancellationToken)
        {
            this._cancellationToken = cancellationToken;
            this._taskQueue = new BlockingCollection<Task>();
            _singleThread = new Lazy<Thread>(() =>
            {
                var thread = new Thread(RunOnCurrentThread) { Name = "STTS Thread", IsBackground = true };
                thread.Start();
                return thread;
            }
            );
        }

        
        void RunOnCurrentThread()
        {
            _isExecuting = true;

            try
            {
                foreach (var task in _taskQueue.GetConsumingEnumerable(_cancellationToken))
                {
                    TryExecuteTask(task);
                }
            }
            catch (OperationCanceledException)
            { }
            finally
            {
                _isExecuting = false;
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() => _taskQueue.ToList();

        protected override void QueueTask(Task task)
        {
            _ = _singleThread.Value;
            try
            {
                _taskQueue.Add(task, _cancellationToken);
            }
            catch (OperationCanceledException)
            { }
        }


        public void AddTask(Task task) => QueueTask(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // We'd need to remove the task from queue if it was already queued. 
            // That would be too hard.
            if (taskWasPreviouslyQueued) return false;

            return _isExecuting && TryExecuteTask(task);
        }


        public Task RunTask(Func<Task> task) => Task.Factory.StartNew(task, CancellationToken.None, Task.Factory.CreationOptions, this);

        public Task<R> RunTask<R>(Func<Task<R>> task) => Task.Factory.StartNew(task, CancellationToken.None, Task.Factory.CreationOptions, this).Unwrap();

        public Task Run(Action action) => Task.Factory.StartNew(action, CancellationToken.None, Task.Factory.CreationOptions, this);

        public Task<R> Run<R>(Func<R> task) => Task.Factory.StartNew(task, CancellationToken.None, Task.Factory.CreationOptions, this);

    }

}
