using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Executes a function or task periodically on the thread pool, with a fixed delay between the end of one execution and the start of the next.
    /// The function returns true to keep running or false to stop.
    /// </summary>
    /// <remarks>
    /// Executions never overlap. Exceptions are recorded in <see cref="Exceptions"/>.
    /// Once stopped (<see cref="TryStop"/> or <see cref="Dispose"/>) the internal cancellation token stays canceled, so the instance can't be restarted (a new start ends immediately without executing the function).
    /// <see cref="TryStop"/> and <see cref="Dispose"/> block the calling thread until the current execution has completed, so they must not be called from within the periodic function (that would dead lock).
    /// </remarks>
    public sealed class PeriodicTask : IDisposable
    {


        /// <summary>
        /// Runs a function periodically (on the thread pool) with the specified delay in between
        /// </summary>
        /// <param name="func">The function to execute at the given interval, return true to continue or false to stop the periodic execution</param>
        /// <param name="sleepMs">Number of milliseconds to pause between each execution (from the end of one execution to the start of the next)</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the function throws an exception, continue the periodic execution anyway</param>
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
        /// Runs a task periodically (on the thread pool) with the specified delay in between
        /// </summary>
        /// <param name="createTask">A function that creates the task to run at the given interval, the task returns true to continue or false to stop the periodic execution</param>
        /// <param name="sleepMs">Number of milliseconds to pause between each task execution (from the completion of one task to the start of the next)</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation throws an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<Task<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = createTask;
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }

        /*
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
        */

        /// <summary>
        /// Runs a task periodically (on the thread pool) with the specified delay in between, the task can be cancelled
        /// </summary>
        /// <param name="createTask">A function that creates the task to run at the given interval, the task returns true to continue or false to stop.
        /// It should observe the supplied cancellation token (<see cref="CancelToken"/>), that is canceled when the periodic task is stopped</param>
        /// <param name="sleepMs">Number of milliseconds to pause between each task execution (from the completion of one task to the start of the next)</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation throws an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<CancellationToken, Task<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = () => createTask(Cancel.Token);
            SleepMs = sleepMs;
            CancelToken = Cancel.Token;
            ContinueOnException = continueOnException;
            if (runNow)
                Start(initialDelay);
        }

        /*
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
        */


        /// <summary>
        /// Number of milliseconds to pause between each task execution
        /// </summary>
        public readonly int SleepMs;

        /// <summary>
        /// Start execution of the periodic task if it's not already running
        /// </summary>
        /// <param name="initialDelay">Delay the first execution using the specified sleepMs parameter</param>
        /// <returns>True if the periodic task was started or false if it's already running</returns>
        /// <remarks>
        /// The check is not atomic, concurrent calls may start two execution loops.
        /// After the periodic task have been stopped the cancellation token remains canceled, so a restarted loop ends without executing anything.
        /// </remarks>
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
        /// Start execution of the periodic task if it's not already running (same as <see cref="TryStart"/>, ignoring the result)
        /// </summary>
        /// <param name="initialDelay">Delay the first execution using the specified sleepMs parameter</param>
        public void Start(bool initialDelay = false) => TryStart(initialDelay);

        /// <summary>
        /// True if the periodic execution loop is active (it's false after the function returned false, threw with continueOnException false, or the task was stopped)
        /// </summary>
        public bool IsRunning => IsTaskRunning;

        /// <summary>
        /// Exceptions thrown by the function / task are tracked here
        /// </summary>
        public ExceptionTracker Exceptions { get; } = new ExceptionTracker();

        /// <summary>
        /// The token that is canceled when the periodic task is stopped (passed to the task creator when using the cancellable constructor)
        /// </summary>
        public readonly CancellationToken CancelToken;

        /// <summary>
        /// True if the periodic task is set to continue on exceptions
        /// </summary>
        public readonly bool ContinueOnException;

        /// <summary>
        /// Try to stop the periodic task, if a task is currently being invoked, it waits (blocking) for it to complete (cancellation is requested)
        /// </summary>
        /// <param name="onStopping">An optional callback to run when the task have been scheduled to stop (before waiting), exceptions thrown by it are propagated (and the method returns without waiting)</param>
        /// <returns>True if the periodic task was stopped or false if it wasn't running</returns>
        /// <remarks>Blocks the calling thread until the execution loop has ended, never call this from within the periodic function / task (dead lock)</remarks>
        public bool TryStop(Action onStopping = null)
        {
            if (!IsTaskRunning)
                return false;
            if (!CancelToken.IsCancellationRequested)
                Cancel.Cancel();
            onStopping?.Invoke();
            try
            {
                OnStopping?.Invoke();
            }
            catch
            {
            }
            IsDisposedCompleted.WaitOne();
            return true;
        }


        /// <summary>
        /// Raised by <see cref="TryStop"/> (and <see cref="Dispose"/>) after cancellation have been requested, but before waiting for the current execution to complete.
        /// Exceptions thrown by the handlers are ignored.
        /// </summary>
        public event Action OnStopping;

        /// <summary>
        /// Stop the periodic task, if a task is currently being invoked, it waits (blocking) for it to complete (cancellation is requested).
        /// </summary>
        /// <remarks>The internal cancellation token source and wait handle are not disposed. Never call this from within the periodic function / task (dead lock).</remarks>
        public void Dispose() => TryStop();

        /// <summary>
        /// Run the task once, and schedule the next execution (in a new async chain) if it should continue
        /// </summary>
        /// <param name="delay">True to wait <see cref="SleepMs"/> before executing</param>
        /// <returns>A task that completes when this execution is done</returns>
        async Task RunTask(bool delay)
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
                        Exceptions.OnException(ex);
                        if (ContinueOnException)
                            TaskExt.StartNewAsyncChain(() => RunTask(true).ConfigureAwait(false));
                    }
                }
            }
            IsDisposedCompleted.Set();
            IsTaskRunning = false;
        }

        readonly Func<Task<bool>> DoTask;
        readonly Func<bool> DoFunc;
       
        readonly CancellationTokenSource Cancel = new ();
        readonly ManualResetEvent IsDisposedCompleted = new (false);
        volatile bool IsTaskRunning;
    }

}
