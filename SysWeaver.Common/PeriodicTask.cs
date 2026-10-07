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
    /// A loop that was stopped (<see cref="TryStop"/>) or that ended by itself (the function returned false, or threw with continueOnException false) can be started again (<see cref="TryStart"/>),
    /// every start uses a new cancellation token (<see cref="CancelToken"/>). Once disposed (<see cref="Dispose"/>) the instance can't be started again.
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
            DoTask = _ => createTask();
            SleepMs = sleepMs;
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
        /// It should observe the supplied cancellation token (the <see cref="CancelToken"/> of the current execution loop), that is canceled when the periodic task is stopped</param>
        /// <param name="sleepMs">Number of milliseconds to pause between each task execution (from the completion of one task to the start of the next)</param>
        /// <param name="runNow">Set to true to start immediately</param>
        /// <param name="continueOnException">If set to true and the task or it's creation throws an exception, continue the periodic execution anyway</param>
        /// <param name="initialDelay">If runNow is true, delay the first execution using the sleepMs parameter</param>
        public PeriodicTask(Func<CancellationToken, Task<bool>> createTask, int sleepMs = 1000, bool runNow = true, bool continueOnException = true, bool initialDelay = false)
        {
            DoTask = createTask;
            SleepMs = sleepMs;
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
        /// <returns>True if the periodic task was started or false if it's already running (or have been disposed)</returns>
        /// <remarks>
        /// The check is atomic, concurrent calls will only start one execution loop.
        /// A stopped periodic task can be started again (with a new <see cref="CancelToken"/>), a disposed one can't (false is returned).
        /// </remarks>
        public bool TryStart(bool initialDelay = false)
        {
            RunState s;
            lock (Sync)
            {
                if (IsDisposed || (IsTaskRunning != 0))
                    return false;
                s = State;
                if (s.Used)
                    State = s = new RunState();
                s.Used = true;
                Volatile.Write(ref IsTaskRunning, 1);
            }
            TaskExt.StartNewAsyncChain(() => RunTask(s, initialDelay).ConfigureAwait(false));
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
        public bool IsRunning => Volatile.Read(ref IsTaskRunning) != 0;

        /// <summary>
        /// Exceptions thrown by the function / task are tracked here
        /// </summary>
        public ExceptionTracker Exceptions { get; } = new ExceptionTracker();

        /// <summary>
        /// The token that is canceled when the periodic task is stopped (passed to the task creator when using the cancellable constructor).
        /// Every start uses a new token, this is the token of the current execution loop (or of the last one, or of the next one if never started).
        /// </summary>
        public CancellationToken CancelToken => Volatile.Read(ref State).Token;

        /// <summary>
        /// True if the periodic task is set to continue on exceptions
        /// </summary>
        public readonly bool ContinueOnException;

        /// <summary>
        /// Try to stop the periodic task, if a task is currently being invoked, it waits (blocking) for it to complete (cancellation is requested)
        /// </summary>
        /// <param name="onStopping">An optional callback to run when the task have been scheduled to stop (before waiting), exceptions thrown by it are propagated (and the method returns without waiting)</param>
        /// <returns>True if the periodic task was stopped or false if it wasn't running</returns>
        /// <remarks>Blocks the calling thread until the execution loop has ended, never call this from within the periodic function / task (dead lock).
        /// Concurrent calls are safe (they all wait for the loop to end). When this returns the periodic task can be started again.</remarks>
        public bool TryStop(Action onStopping = null)
        {
            RunState s;
            lock (Sync)
            {
                if (IsTaskRunning == 0)
                    return false;
                s = State;
                ++s.Cancelling;
            }
            try
            {
                s.Cancel.Cancel();
            }
            finally
            {
                bool dispose;
                lock (Sync)
                    dispose = (--s.Cancelling == 0) && s.Ended;
                if (dispose)
                    s.Cancel.Dispose();
            }
            onStopping?.Invoke();
            try
            {
                OnStopping?.Invoke();
            }
            catch
            {
            }
            s.Completed.Task.GetAwaiter().GetResult();
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
        /// <remarks>Once disposed the periodic task can't be started again. The internal cancellation token source is disposed (when the execution loop ends).
        /// Safe to call more than once and concurrently with <see cref="TryStop"/>. Never call this from within the periodic function / task (dead lock).</remarks>
        public void Dispose()
        {
            lock (Sync)
                IsDisposed = true;
            TryStop();
            RunState s;
            lock (Sync)
            {
                s = State;
                if (s.Used)
                    return;
                //  Never started (and can't be started anymore), the source is owned here
                s.Used = true;
            }
            s.Cancel.Cancel();
            s.Cancel.Dispose();
        }

        /// <summary>
        /// Run the task once, and schedule the next execution (in a new async chain) if it should continue
        /// </summary>
        /// <param name="s">The state of this execution loop</param>
        /// <param name="delay">True to wait <see cref="SleepMs"/> before executing</param>
        /// <returns>A task that completes when this execution is done</returns>
        async Task RunTask(RunState s, bool delay)
        {
            var ct = s.Token;
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
                        var res = t == null ? DoFunc() : await t(ct).ConfigureAwait(false);
                        if (res)
                        {
                            TaskExt.StartNewAsyncChain(() => RunTask(s, true).ConfigureAwait(false));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Exceptions.OnException(ex);
                        if (ContinueOnException)
                        {
                            //  The loop continues, so it's still running (don't signal completion)
                            TaskExt.StartNewAsyncChain(() => RunTask(s, true).ConfigureAwait(false));
                            return;
                        }
                    }
                }
            }
            //  Clear the running flag before signaling completion, so that a TryStart right after TryStop succeeds.
            //  A new start uses a new state, so it can't race with the completion of this one.
            bool dispose;
            lock (Sync)
            {
                Volatile.Write(ref IsTaskRunning, 0);
                s.Ended = true;
                //  If a TryStop is canceling the source right now, it disposes the source when done
                dispose = s.Cancelling == 0;
            }
            s.Completed.TrySetResult();
            //  The token keeps it's state after the source is disposed
            if (dispose)
                s.Cancel.Dispose();
        }

        /// <summary>
        /// The state of one execution loop (every start uses a new one)
        /// </summary>
        sealed class RunState
        {
            public RunState()
            {
                Token = Cancel.Token;
            }

            /// <summary>
            /// Canceled when the loop should stop, disposed when the loop have ended (and no one is canceling it)
            /// </summary>
            public readonly CancellationTokenSource Cancel = new();

            /// <summary>
            /// The token of the source (cached, so that it can be read after the source have been disposed)
            /// </summary>
            public readonly CancellationToken Token;

            /// <summary>
            /// Completed when the loop have ended (not a wait handle, so there is nothing to dispose)
            /// </summary>
            public readonly TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// True once a loop (or dispose) have taken this state, protected by the Sync lock
            /// </summary>
            public bool Used;

            /// <summary>
            /// True once the loop have ended, protected by the Sync lock
            /// </summary>
            public bool Ended;

            /// <summary>
            /// Number of TryStop calls that are currently canceling the source (it's disposed when the loop have ended and this is zero), protected by the Sync lock
            /// </summary>
            public int Cancelling;
        }

        readonly Func<CancellationToken, Task<bool>> DoTask;
        readonly Func<bool> DoFunc;

        readonly Object Sync = new();
        RunState State = new();
        bool IsDisposed;
        int IsTaskRunning;
    }

}
