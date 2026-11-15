
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A task scheduler that runs all tasks, one at a time, on a single dedicated background thread (started on first use)
    /// </summary>
    public sealed class SingleThreadTaskScheduler : TaskScheduler
    {
        [ThreadStatic]
        static bool _isExecuting;

        readonly CancellationToken _cancellationToken;

        readonly BlockingCollection<Task> _taskQueue;

        readonly Lazy<Thread> _singleThread;
        /// <inheritdoc />
        public override int MaximumConcurrencyLevel => 1;

        /// <summary>
        /// Create the scheduler
        /// </summary>
        /// <param name="cancellationToken">When canceled, the thread stops running queued tasks</param>
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

        /// <inheritdoc />
        protected override IEnumerable<Task> GetScheduledTasks() => _taskQueue.ToList();

        /// <inheritdoc />
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


        /// <summary>
        /// Queue a task (that is not started yet) to be run by the scheduler thread
        /// </summary>
        /// <param name="task">The task</param>
        public void AddTask(Task task) => QueueTask(task);

        /// <inheritdoc />
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // We'd need to remove the task from queue if it was already queued. 
            // That would be too hard.
            if (taskWasPreviouslyQueued) return false;

            return _isExecuting && TryExecuteTask(task);
        }


        /// <summary>
        /// Run an asynchronous function on the scheduler thread
        /// </summary>
        /// <param name="task">The function</param>
        /// <returns>A task that completes when the function has completed</returns>
        public Task RunTask(Func<Task> task) => Task.Factory.StartNew(task, CancellationToken.None, Task.Factory.CreationOptions, this);

        /// <summary>
        /// Run an asynchronous function on the scheduler thread
        /// </summary>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="task">The function</param>
        /// <returns>A task that completes with the result of the function</returns>
        public Task<R> RunTask<R>(Func<Task<R>> task) => Task.Factory.StartNew(task, CancellationToken.None, Task.Factory.CreationOptions, this).Unwrap();

        /// <summary>
        /// Run an action on the scheduler thread
        /// </summary>
        /// <param name="action">The action</param>
        /// <returns>A task that completes when the action has completed</returns>
        public Task Run(Action action) => Task.Factory.StartNew(action, CancellationToken.None, Task.Factory.CreationOptions, this);

        /// <summary>
        /// Run a function on the scheduler thread
        /// </summary>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="task">The function</param>
        /// <returns>A task that completes with the result of the function</returns>
        public Task<R> Run<R>(Func<R> task) => Task.Factory.StartNew(task, CancellationToken.None, Task.Factory.CreationOptions, this);

    }

}
