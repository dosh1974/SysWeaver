using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Executes a task periodically (with a delay between executions)
    /// </summary>
    public sealed class PeriodicTask : IDisposable
    {


        /// <summary>
        /// Runs an action concurrently periodically with the specfied delay inbetween
        /// </summary>
        /// <param name="func">The function to execute at the given interval</param>
        /// <param name="sleepMs">Number of milliseconds to to pause between each task execution</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the action casts an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<bool> func, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoFunc = func;
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }

        /// <summary>
        /// Runs a task concurrently periodically with the specfied delay inbetween
        /// </summary>
        /// <param name="createTask">A function that creates the task to run at the given interval</param>
        /// <param name="sleepMs">Number of milliseconds to to pause between each task execution</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation casts an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<Task<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = async () => await createTask().ConfigureAwait(false);
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }

        /// <summary>
        /// Runs a task concurrently periodically with the specfied delay inbetween
        /// </summary>
        /// <param name="createTask">A function that creates the task to run at the given interval</param>
        /// <param name="sleepMs">Number of milliseconds to to pause between each task execution</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation casts an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<ValueTask<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = createTask;
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }

        /// <summary>
        /// Runs a task concurrently periodically with the specfied delay inbetween, this task can be cancelled
        /// </summary>
        /// <param name="createTask">A function that creates the task to run at the given interval, this task should be able to cancle using the canellation token provided</param>
        /// <param name="sleepMs">Number of milliseconds to to pause between each task execution</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation casts an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<CancellationToken, Task<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = async () => await createTask(Cancel.Token).ConfigureAwait(false);
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }


        /// <summary>
        /// Runs a task concurrently periodically with the specfied delay inbetween, this task can be cancelled
        /// </summary>
        /// <param name="createTask">A function that creates the task to run at the given interval, this task should be able to cancle using the canellation token provided</param>
        /// <param name="sleepMs">Number of milliseconds to to pause between each task execution</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation casts an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<CancellationToken, ValueTask<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = async () => await createTask(Cancel.Token).ConfigureAwait(false);
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }


        /// <summary>
        /// Number of milliseconds to to pause between each task execution
        /// </summary>
        public readonly int SleepMs;

        /// <summary>
        /// Start execution of the periodic task if it's not already running
        /// </summary>
        /// <param name="initialDelay">Delay the first execution using the specified sleepMs parameter</param>
        /// <returns>True if the periodic task was started or false if it's already running</returns>
        public bool TryStart(bool initialDelay = false)
        {
            if (IsTaskRunning)
                return false;
            IsDisposedCompleted.Reset();
            IsTaskRunning = true;
            TaskExt.StartNewAsyncChain(() => RunTask(initialDelay).ConfigureAwait(false));
            return true;
        }

        /// <summary>
        /// Start execution of the periodic task if it's not already running
        /// </summary>
        /// <param name="initialDelay">Delay the first execution using the specified sleepMs parameter</param>
        public void Start(bool initialDelay = false) => TryStart(initialDelay);

        /// <summary>
        /// True if the periodic task is running
        /// </summary>
        public bool IsRunning => IsTaskRunning;

        /// <summary>
        /// If the task throws an exception, the last one is stored here (the task continues to repeat even if there was an expcetion)
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// The token used to cancel a task
        /// </summary>
        public readonly CancellationToken CancelToken;

        /// <summary>
        /// True if the periodic task is set to continue on exceptions
        /// </summary>
        public readonly bool ContinueOnException;

        /// <summary>
        /// Try to stop the periodic task, if a task is currently being invoked, it waits for it to complete (cancellation is requested)
        /// </summary>
        /// <param name="onStopping">An optional callback to run when the task have been scheduled to stop</param>
        /// <returns>True if the periodic task was stopped or fasle if it wasn't running</returns>
        public bool TryStop(Action onStopping = null)
        {
            if (!IsTaskRunning)
                return false;
            if (!CancelToken.IsCancellationRequested)
                Cancel.Cancel();
            onStopping?.Invoke();
            IsDisposedCompleted.WaitOne();
            return true;
        }

        /// <summary>
        /// Stop and dispose, if a task is currently being invoked, it waits for it to complete (cancellation is requested)
        /// </summary>
        public void Dispose() => TryStop();

        /// <summary>
        /// Run the task once
        /// </summary>
        /// <param name="delay">True to wait</param>
        /// <returns></returns>
        async ValueTask RunTask(bool delay)
        {
            var ct = CancelToken;
            if (!ct.IsCancellationRequested)
            {
                if (delay)
                {
                    try
                    {
                        await Task.Delay(SleepMs, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var t = DoTask;
                        var res = t == null ? DoFunc() : await t().ConfigureAwait(false);
                        if (res)
                        {
                            TaskExt.StartNewAsyncChain(() => RunTask(true).ConfigureAwait(false));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Exception = ex;
                        if (ContinueOnException)
                            TaskExt.StartNewAsyncChain(() => RunTask(true).ConfigureAwait(false));
                    }
                }
            }
            IsDisposedCompleted.Set();
            IsTaskRunning = false;
        }

        readonly Func<ValueTask<bool>> DoTask;
        readonly Func<bool> DoFunc;
       
        readonly CancellationTokenSource Cancel = new CancellationTokenSource();
        readonly ManualResetEvent IsDisposedCompleted = new ManualResetEvent(false);
        volatile bool IsTaskRunning;
    }

}
