using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Cached, already completed tasks for a generic type
    /// </summary>
    /// <typeparam name="T">The result type of the tasks</typeparam>
    public static class TaskExt<T>
    {
        /// <summary>
        /// A completed task with the default value of <typeparamref name="T"/> (null for reference types) as the result
        /// </summary>
        public static readonly Task<T> NullTask = Task.FromResult(default(T));

        /// <summary>
        /// A completed value task with the default value of <typeparamref name="T"/> (null for reference types) as the result
        /// </summary>
        public static readonly ValueTask<T> NullValueTask = ValueTask.FromResult(default(T));

        /// <summary>
        /// A completed task with an empty array as the result
        /// </summary>
        public static readonly Task<T[]> EmptyArrayTask = Task.FromResult(Array.Empty<T>());

        /// <summary>
        /// A completed value task with an empty array as the result
        /// </summary>
        public static readonly ValueTask<T[]> EmptyArrayValueTask = ValueTask.FromResult(Array.Empty<T>());

    }

    /// <summary>
    /// Task related helpers: cached completed tasks, fire and forget, sync over async, delays, async wait handles, event raising and a ValueTask WhenAll
    /// </summary>
    public static class TaskExt
    {
        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        /// <remarks>The function is executed on the thread pool, the caller doesn't wait for it and any exception is unobserved</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChain(Func<ConfiguredTaskAwaitable> task) => Task.Run(task);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task</param>
        /// <remarks>The function is executed on the thread pool, the caller doesn't wait for it and any exception is unobserved</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChain(Func<Task> task) => Task.Run(task);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        /// <remarks>The function is executed on the thread pool, the caller doesn't wait for it and any exception is unobserved</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChain(this Func<ConfiguredValueTaskAwaitable> task) => Task.Run(task);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new value task</param>
        /// <remarks>The function is executed on the thread pool, the caller doesn't wait for it and any exception is unobserved</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChainValue(this Func<ValueTask> task) => Task.Run(task);

        /// <summary>
        /// Wait (blocking) for a task to complete and return it's value (sync over async)
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="t">The task to wait for</param>
        /// <returns>The return value of the task</returns>
        /// <remarks>The calling thread is blocked until the task completes.
        /// If the task faults, the original exception is re-thrown (not wrapped in an <see cref="AggregateException"/>), except when there are multiple exceptions.</remarks>
        /// <exception cref="NullReferenceException"><paramref name="t"/> is null</exception>
        /// <exception cref="TaskCanceledException">The task was canceled</exception>
        /// <exception cref="Exception">Any exception that the task faulted with</exception>
        public static T RunAsync<T>(this Task<T> t)
        {
            try
            {
                return t.GetAwaiter().GetResult();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Count > 1)
                    ExceptionDispatchInfo.Capture(ex).Throw();
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }


        /// <summary>
        /// Wait (blocking) for a value task to complete (sync over async)
        /// </summary>
        /// <param name="t">The value task to wait for</param>
        /// <remarks>The calling thread is blocked until the task completes.
        /// If the task faults, the original exception is re-thrown (not wrapped in an <see cref="AggregateException"/>), except when there are multiple exceptions.</remarks>
        /// <exception cref="TaskCanceledException">The task was canceled</exception>
        /// <exception cref="AggregateException">The task faulted with multiple exceptions</exception>
        /// <exception cref="Exception">Any exception that the task faulted with</exception>
        public static void RunAsync(this ValueTask t)
        {
            try
            {
                t.AsTask().Wait();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Count > 1)
                    ExceptionDispatchInfo.Capture(ex).Throw();
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }


        /// <summary>
        /// Wait (blocking) for a value task to complete and return it's value (sync over async)
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="t">The value task to wait for</param>
        /// <returns>The return value of the task</returns>
        /// <remarks>The calling thread is blocked until the task completes (also works for a pending value task that is backed by an <see cref="System.Threading.Tasks.Sources.IValueTaskSource{TResult}"/>).
        /// If the task faults, the original exception is re-thrown (not wrapped in an <see cref="AggregateException"/>).</remarks>
        /// <exception cref="TaskCanceledException">The task was canceled</exception>
        /// <exception cref="Exception">Any exception that the task faulted with</exception>
        public static T RunAsync<T>(this ValueTask<T> t)
        {
            try
            {
                // A pending value task backed by an IValueTaskSource can't be waited on using GetResult, it must be converted to a task
                return t.IsCompleted ? t.GetAwaiter().GetResult() : t.AsTask().GetAwaiter().GetResult();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Count > 1)
                    ExceptionDispatchInfo.Capture(ex).Throw();
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }

        /// <summary>
        /// Wait (blocking) for a task to complete (sync over async)
        /// </summary>
        /// <param name="t">The task to wait for</param>
        /// <remarks>The calling thread is blocked until the task completes.
        /// If the task faults, the original exception is re-thrown (not wrapped in an <see cref="AggregateException"/>), except when there are multiple exceptions.</remarks>
        /// <exception cref="NullReferenceException"><paramref name="t"/> is null</exception>
        /// <exception cref="TaskCanceledException">The task was canceled</exception>
        /// <exception cref="AggregateException">The task faulted with multiple exceptions</exception>
        /// <exception cref="Exception">Any exception that the task faulted with</exception>
        public static void RunAsync(this Task t)
        {
            try
            {
                t.Wait();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Count > 1)
                    ExceptionDispatchInfo.Capture(ex).Throw();
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }


        /// <summary>
        /// A complete task for a null string
        /// </summary>
        public static readonly Task<String> NullStringTask = Task.FromResult<String>(null);

        /// <summary>
        /// A complete task for an empty string
        /// </summary>
        public static readonly Task<String> EmptyStringTask = Task.FromResult("");

        /// <summary>
        /// A complete task for an empty string array
        /// </summary>
        public static readonly Task<String[]> EmptyStringArrayTask = Task.FromResult(Array.Empty<String>());

        /// <summary>
        /// A complete task for a True boolean
        /// </summary>
        public static readonly Task<Boolean> TrueTask = Task.FromResult(true);

        /// <summary>
        /// A complete task for a False boolean
        /// </summary>
        public static readonly Task<Boolean> FalseTask = Task.FromResult(false);

        /// <summary>
        /// A complete task for an empty read only memory buffer
        /// </summary>
        public static readonly Task<ReadOnlyMemory<Byte>> ReadonlyMemoryTask = Task.FromResult(ReadOnlyMemory<Byte>.Empty);



        /// <summary>
        /// A complete value task for a null string
        /// </summary>
        public static readonly ValueTask<String> NullStringValueTask = ValueTask.FromResult<String>(null);

        /// <summary>
        /// A complete value task for an empty string
        /// </summary>
        public static readonly ValueTask<String> EmptyStringValueTask = ValueTask.FromResult("");

        /// <summary>
        /// A complete value task for an empty string array
        /// </summary>
        public static readonly ValueTask<String[]> EmptyStringArrayValueTask = ValueTask.FromResult(Array.Empty<String>());

        /// <summary>
        /// A complete value task for a True boolean
        /// </summary>
        public static readonly ValueTask<Boolean> TrueValueTask = ValueTask.FromResult(true);

        /// <summary>
        /// A complete value task for a False boolean
        /// </summary>
        public static readonly ValueTask<Boolean> FalseValueTask = ValueTask.FromResult(false);

        /// <summary>
        /// A complete value task for an empty read only memory buffer
        /// </summary>
        public static readonly ValueTask<ReadOnlyMemory<Byte>> ReadonlyMemoryValueTask = ValueTask.FromResult(ReadOnlyMemory<Byte>.Empty);

        /// <summary>
        /// A complete value task for an empty memory buffer
        /// </summary>
        public static readonly ValueTask<Memory<Byte>> MemoryValueTask = ValueTask.FromResult(Memory<Byte>.Empty);

        /// <summary>
        /// Task that delays a small random amount (using a cryptographically secure random number, suitable to mitigate timing attacks)
        /// </summary>
        /// <param name="min">Minimum delay in ms, must be zero or greater</param>
        /// <param name="mask">Bitmask for the delay to add: delay = min + (RandomByte &amp; mask), so at most 255 ms is added</param>
        /// <returns>A task that completes after the delay</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is negative (or so large that the delay overflows)</exception>
        public static Task RandomDelay(int min = 1, int mask = 0xf)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(min);
            int delay = min;
            using (var rng = SecureRng.Get())
                delay += (rng.GetByte() & mask);
            return Task.Delay(delay);
        }


        /// <summary>
        /// Run a function after some fixed duration (fire and forget)
        /// </summary>
        /// <param name="func">The function to execute</param>
        /// <param name="delayInMs">The delay in milli seconds (<see cref="Timeout.Infinite"/> means that the function is never executed)</param>
        /// <remarks>The function is executed on the thread pool, any exception thrown by it is unobserved</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="delayInMs"/> is less than -1 (<see cref="Timeout.Infinite"/>)</exception>
        public static void RunDelayed(Action func, int delayInMs)
        {
            ArgumentNullException.ThrowIfNull(func);
            ArgumentOutOfRangeException.ThrowIfLessThan(delayInMs, Timeout.Infinite);
            _ = Task.Delay(delayInMs).ContinueWith(static (_, s) => ((Action)s)(), func, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }


        /// <summary>
        /// Execute a task after some fixed duration
        /// </summary>
        /// <param name="task">The task to execute</param>
        /// <param name="delayInMs">The delay in milli seconds</param>
        /// <remarks>A task is already running when it's passed to this method, so this method does nothing useful (the task is neither started nor observed).
        /// Use <see cref="RunDelayed(Action, int)"/> with a function that starts the task instead.</remarks>
        public static void RunDelayed(Task task, int delayInMs)
        {
            StartNewAsyncChain(() => Task.Delay(delayInMs).ContinueWith(x => task));
        }

        /// <summary>
        /// Execute a task after some fixed duration
        /// </summary>
        /// <param name="task">The task to execute</param>
        /// <param name="delayInMs">The delay in milli seconds</param>
        /// <remarks>A value task is already running when it's passed to this method, so this method does nothing useful (the task is neither started nor observed).
        /// Use <see cref="RunDelayed(Action, int)"/> with a function that starts the task instead.</remarks>
        public static void RunDelayed(ValueTask task, int delayInMs)
        {
            StartNewAsyncChain(() => Task.Delay(delayInMs).ContinueWith(x => task));
        }


        static readonly WaitOrTimerCallback OnWaitCompleted = static (s, timedOut) =>
        {
            var tcs = (TaskCompletionSource<bool>)s;
            if (timedOut)
                tcs.TrySetCanceled();
            else
                tcs.TrySetResult(true);
        };

        /// <summary>
        /// Wait asynchronously for a wait handle to be signaled
        /// </summary>
        /// <param name="waitHandle">The wait handle to wait for</param>
        /// <param name="timeoutMilliseconds">The maximum time to wait in milli seconds, <see cref="Timeout.Infinite"/> (-1) to wait forever</param>
        /// <returns>A task that completes when the wait handle is signaled, it's canceled if the time out expires first</returns>
        /// <remarks>The wait is performed using <see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle, WaitOrTimerCallback, object, int, bool)"/>, so no thread is blocked.
        /// Continuations of the returned task may run synchronously on the thread pool thread that observed the signal.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="waitHandle"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMilliseconds"/> is less than -1 (<see cref="Timeout.Infinite"/>)</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="waitHandle"/> is disposed</exception>
        public static Task WaitOneAsync(this WaitHandle waitHandle, int timeoutMilliseconds = Timeout.Infinite)
        {
            ArgumentNullException.ThrowIfNull(waitHandle);
            ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMilliseconds, Timeout.Infinite);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle, OnWaitCompleted, tcs, timeoutMilliseconds, true);
            Task<bool> task = tcs.Task;
            _ = task.ContinueWith(static (_, s) => ((RegisteredWaitHandle)s).Unregister(null), rwh, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return task;
        }

        sealed class WaitOneState
        {
            public RegisteredWaitHandle Rwh;
            public CancellationTokenRegistration Ctr;
        }

        /// <summary>
        /// Wait asynchronously for a wait handle to be signaled
        /// </summary>
        /// <param name="waitHandle">The wait handle to wait for</param>
        /// <param name="cancellationToken">A cancellation token that cancels the wait</param>
        /// <param name="timeoutMilliseconds">The maximum time to wait in milli seconds, <see cref="Timeout.Infinite"/> (-1) to wait forever</param>
        /// <returns>A task that completes when the wait handle is signaled, it's canceled if the time out expires or the <paramref name="cancellationToken"/> is canceled first</returns>
        /// <remarks>The wait is performed using <see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle, WaitOrTimerCallback, object, int, bool)"/>, so no thread is blocked.
        /// Continuations of the returned task may run synchronously on the thread that observed the signal or canceled the token.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="waitHandle"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMilliseconds"/> is less than -1 (<see cref="Timeout.Infinite"/>)</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="waitHandle"/> is disposed</exception>
        public static Task WaitOneAsync(this WaitHandle waitHandle, CancellationToken cancellationToken, int timeoutMilliseconds = Timeout.Infinite)
        {
            ArgumentNullException.ThrowIfNull(waitHandle);
            ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMilliseconds, Timeout.Infinite);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle, OnWaitCompleted, tcs, timeoutMilliseconds, true);
            Task<bool> task = tcs.Task;
            if (!cancellationToken.CanBeCanceled)
            {
                _ = task.ContinueWith(static (_, s) => ((RegisteredWaitHandle)s).Unregister(null), rwh, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                return task;
            }
            var state = new WaitOneState
            {
                Rwh = rwh,
                Ctr = cancellationToken.Register(static s => ((TaskCompletionSource<bool>)s).TrySetCanceled(), tcs),
            };
            _ = task.ContinueWith(static (_, s) =>
            {
                var st = (WaitOneState)s;
                st.Rwh.Unregister(null);
                st.Ctr.Unregister();
            }, state, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return task;
        }


        /// <summary>
        /// Like Task.WhenAll but running in serial (for debugging)
        /// </summary>
        /// <param name="tasks">The tasks to await, one at a time, in order (a lazy enumerable is only advanced after the previous task completed)</param>
        /// <returns>A task that completes when all tasks have completed, it faults with the exception of the first faulting task (the remaining tasks are not enumerated)</returns>
        /// <exception cref="NullReferenceException"><paramref name="tasks"/> is null (the returned task is faulted)</exception>
        public static async Task WhenAllDebug(IEnumerable<Task> tasks)
        {
            foreach (var t in tasks)
                await t.ConfigureAwait(false);
        }


        #region Async events

        /// <summary>
        /// Get statistics about the exceptions thrown by event handlers when using the RaiseEvents overloads that don't take an exception handler
        /// </summary>
        /// <returns>The statistics (count, time and last exception)</returns>
        public static IEnumerable<Stats> GetEventExceptionStats() => EventExceptions.GetStats(nameof(TaskExt), "EventExceptions.");

        static readonly ExceptionTracker EventExceptions = new ExceptionTracker();

        static readonly Action<Exception> OnEventException = ex => EventExceptions.OnException(ex);

        #region Async


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents(this Func<Task> eventHandlers)
            => RaiseEvents(eventHandlers, OnEventException);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0>(this Func<A0, Task> eventHandlers, A0 a0)
            => RaiseEvents(eventHandlers, OnEventException, a0);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, Task> eventHandlers, A0 a0, A1 a1)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, Task> eventHandlers, A0 a0, A1 a1, A2 a2)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <typeparam name="A3">The type of argument 3</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, Task> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2, a3);


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents(this Func<Task> eventHandlers, Action<Exception> onException)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler(Func<Task> eventHandler, Action<Exception> onException)
        {
            try
            {
                await eventHandler().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0>(this Func<A0, Task> eventHandlers, Action<Exception> onException, A0 a0)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0>(Func<A0, Task> eventHandler, Action<Exception> onException, A0 a0)
        {
            try
            {
                await eventHandler(a0).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, Task> eventHandlers, Action<Exception> onException, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0, a1);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0, a1);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0, A1>(Func<A0, A1, Task> eventHandler, Action<Exception> onException, A0 a0, A1 a1)
        {
            try
            {
                await eventHandler(a0, a1).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, Task> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0, a1, a2);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0, a1, a2);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0, A1, A2>(Func<A0, A1, A2, Task> eventHandler, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            try
            {
                await eventHandler(a0, a1, a2).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <typeparam name="A3">The type of argument 3</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, Task> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0, a1, a2, a3);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0, a1, a2, a3);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0, A1, A2, A3>(Func<A0, A1, A2, A3, Task> eventHandler, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            try
            {
                await eventHandler(a0, a1, a2, a3).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }

        #endregion//Async

        #region AsyncValue


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents(this Func<ValueTask> eventHandlers)
            => RaiseEvents(eventHandlers, OnEventException);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0>(this Func<A0, ValueTask> eventHandlers, A0 a0)
            => RaiseEvents(eventHandlers, OnEventException, a0);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, ValueTask> eventHandlers, A0 a0, A1 a1)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, ValueTask> eventHandlers, A0 a0, A1 a1, A2 a2)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2);


        /// <summary>
        /// Raise all async events in parallel without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <typeparam name="A3">The type of argument 3</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns>A task that completes when all event handlers have completed</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, ValueTask> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2, a3);


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents(this Func<ValueTask> eventHandlers, Action<Exception> onException)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler(Func<ValueTask> eventHandler, Action<Exception> onException)
        {
            try
            {
                await eventHandler().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0>(this Func<A0, ValueTask> eventHandlers, Action<Exception> onException, A0 a0)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0>(Func<A0, ValueTask> eventHandler, Action<Exception> onException, A0 a0)
        {
            try
            {
                await eventHandler(a0).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, ValueTask> eventHandlers, Action<Exception> onException, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0, a1);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0, a1);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0, A1>(Func<A0, A1, ValueTask> eventHandler, Action<Exception> onException, A0 a0, A1 a1)
        {
            try
            {
                await eventHandler(a0, a1).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, ValueTask> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0, a1, a2);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0, a1, a2);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0, A1, A2>(Func<A0, A1, A2, ValueTask> eventHandler, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            try
            {
                await eventHandler(a0, a1, a2).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }


        /// <summary>
        /// Raise all async events in parallel without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <typeparam name="A3">The type of argument 3</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception, must be thread safe! If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns>A task that completes when all event handlers have completed, it only faults if <paramref name="onException"/> throws</returns>
        /// <remarks>
        /// All handlers are started (on the calling thread, until their first await) before any of them is awaited, so they execute in parallel.
        /// Exceptions thrown by a handler (synchronously or asynchronously) are passed to the exception handler, they never stop the other handlers.
        /// </remarks>
        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, ValueTask> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            onException ??= OnEventException;
            if (eventHandlers.HasSingleTarget)
                return InvokeHandler(eventHandlers, onException, a0, a1, a2, a3);
            var count = 0;
            foreach (var _ in Delegate.EnumerateInvocationList(eventHandlers))
                ++count;
            var tasks = ArrayPool<Task>.Shared.Rent(count);
            try
            {
                var i = 0;
                var allCompleted = true;
                foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
                {
                    var t = InvokeHandler(h, onException, a0, a1, a2, a3);
                    allCompleted &= t.IsCompletedSuccessfully;
                    tasks[i++] = t;
                }
                if (allCompleted)
                    return Task.CompletedTask;
                // WhenAll doesn't keep a reference to the span, so the array can be returned immediately
                return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count));
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks, true);
            }
        }

        static async Task InvokeHandler<A0, A1, A2, A3>(Func<A0, A1, A2, A3, ValueTask> eventHandler, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            try
            {
                await eventHandler(a0, a1, a2, a3).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                onException(e);
            }
        }

        #endregion//AsyncValue

        #region Sync


        /// <summary>
        /// Raise all events without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents(this Action eventHandlers)
            => RaiseEvents(eventHandlers, OnEventException);


        /// <summary>
        /// Raise all events without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0>(this Action<A0> eventHandlers, A0 a0)
            => RaiseEvents(eventHandlers, OnEventException, a0);


        /// <summary>
        /// Raise all events without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0, A1>(this Action<A0, A1> eventHandlers, A0 a0, A1 a1)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1);


        /// <summary>
        /// Raise all events without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0, A1, A2>(this Action<A0, A1, A2> eventHandlers, A0 a0, A1 a1, A2 a2)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2);


        /// <summary>
        /// Raise all events without throwing, exceptions are tracked (see <see cref="GetEventExceptionStats"/>)
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <typeparam name="A3">The type of argument 3</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0, A1, A2, A3>(this Action<A0, A1, A2, A3> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2, a3);


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception. If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        /// <exception cref="Exception">Any exception thrown by <paramref name="onException"/></exception>
        public static void RaiseEvents(this Action eventHandlers, Action<Exception> onException)
        {
            if (eventHandlers == null)
                return;
            onException ??= OnEventException;
            foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
            {
                try
                {
                    h();
                }
                catch (Exception e)
                {
                    onException(e);
                }
            }
        }


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception. If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        /// <exception cref="Exception">Any exception thrown by <paramref name="onException"/></exception>
        public static void RaiseEvents<A0>(this Action<A0> eventHandlers, Action<Exception> onException, A0 a0)
        {
            if (eventHandlers == null)
                return;
            onException ??= OnEventException;
            foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
            {
                try
                {
                    h(a0);
                }
                catch (Exception e)
                {
                    onException(e);
                }
            }
        }


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception. If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        /// <exception cref="Exception">Any exception thrown by <paramref name="onException"/></exception>
        public static void RaiseEvents<A0, A1>(this Action<A0, A1> eventHandlers, Action<Exception> onException, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return;
            onException ??= OnEventException;
            foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
            {
                try
                {
                    h(a0, a1);
                }
                catch (Exception e)
                {
                    onException(e);
                }
            }
        }


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception. If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        /// <exception cref="Exception">Any exception thrown by <paramref name="onException"/></exception>
        public static void RaiseEvents<A0, A1, A2>(this Action<A0, A1, A2> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return;
            onException ??= OnEventException;
            foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
            {
                try
                {
                    h(a0, a1, a2);
                }
                catch (Exception e)
                {
                    onException(e);
                }
            }
        }


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <typeparam name="A0">The type of argument 0</typeparam>
        /// <typeparam name="A1">The type of argument 1</typeparam>
        /// <typeparam name="A2">The type of argument 2</typeparam>
        /// <typeparam name="A3">The type of argument 3</typeparam>
        /// <param name="eventHandlers">The event handlers to invoke (may be null)</param>
        /// <param name="onException">An action to perform on each exception. If null, the exceptions are tracked (see <see cref="GetEventExceptionStats"/>)</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <remarks>
        /// The handlers are invoked in order on the calling thread, an exception thrown by a handler doesn't stop the other handlers.
        /// </remarks>
        /// <exception cref="Exception">Any exception thrown by <paramref name="onException"/></exception>
        public static void RaiseEvents<A0, A1, A2, A3>(this Action<A0, A1, A2, A3> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return;
            onException ??= OnEventException;
            foreach (var h in Delegate.EnumerateInvocationList(eventHandlers))
            {
                try
                {
                    h(a0, a1, a2, a3);
                }
                catch (Exception e)
                {
                    onException(e);
                }
            }
        }

        #endregion Sync

        #endregion Async events


        /// <summary>
        /// Creates a task that will complete when all of the supplied tasks have completed.
        /// </summary>
        /// <typeparam name="T">The result type of the tasks</typeparam>
        /// <param name="tasks">The tasks to wait on for completion.</param>
        /// <returns>A task that represents the completion of all of the supplied tasks, the result contains the results of the tasks in the same order as the supplied tasks.</returns>
        /// <remarks>
        /// <para>
        /// If any of the supplied tasks completes in a faulted state, the returned task will also complete in a Faulted state,
        /// where its exceptions will contain the aggregation of the set of unwrapped exceptions from each of the supplied tasks.
        /// </para>
        /// <para>
        /// If none of the supplied tasks faulted but at least one of them was canceled, the returned task will end in the Canceled state.
        /// </para>
        /// <para>
        /// If none of the tasks faulted and none of the tasks were canceled, the resulting task will end in the RanToCompletion state.
        /// </para>
        /// <para>
        /// If the supplied list contains no tasks, or all tasks are already completed successfully, the returned task is completed synchronously (no task is allocated).
        /// </para>
        /// <para>
        /// Every value task is awaited exactly once, the list is read again (by index) after the returned task was created, so it must not be changed until the returned task completes.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        public static ValueTask<T[]> WhenAll<T>(
            IReadOnlyList<ValueTask<T>> tasks)
        {
            ArgumentNullException.ThrowIfNull(tasks);
            var tl = tasks.Count;
            if (tl <= 0)
                return TaskExt<T>.EmptyArrayValueTask;
            // We don't allocate the list if no task throws
            var results = GC.AllocateUninitializedArray<T>(tl);
            for (var i = 0; i < tl; i++)
            {
                var t = tasks[i];
                if (!t.IsCompletedSuccessfully)
                    return InternalWhenAll(tasks, results, i, tl);
                results[i] = t.GetAwaiter().GetResult();
            }
            return ValueTask.FromResult(results);
        }

        /// <summary>
        /// Check if a value task that threw an exception when awaited was canceled.
        /// A value task backed by an IValueTaskSource may not be queried after it's result have been consumed (the source can be reset and reused), use the exception type in that case.
        /// </summary>
        static bool WasCanceled<T>(in ValueTask<T> t, Exception ex)
        {
            try
            {
                return t.IsCanceled;
            }
            catch (InvalidOperationException)
            {
                return ex is OperationCanceledException;
            }
        }

        /// <summary>
        /// Check if a value task that threw an exception when awaited was canceled.
        /// A value task backed by an IValueTaskSource may not be queried after it's result have been consumed (the source can be reset and reused), use the exception type in that case.
        /// </summary>
        static bool WasCanceled(in ValueTask t, Exception ex)
        {
            try
            {
                return t.IsCanceled;
            }
            catch (InvalidOperationException)
            {
                return ex is OperationCanceledException;
            }
        }

        static async ValueTask<T[]> InternalWhenAll<T>(IReadOnlyList<ValueTask<T>> tasks, T[] results, int i, int tl)
        {
            List<Exception> exceptions = null;
            bool canceled = false;
            for (; i < tl; i++)
            {
                var t = tasks[i];
                try
                {
                    results[i] = await t.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (WasCanceled(t, ex))
                    {
                        canceled = true;
                        continue;
                    }
                    exceptions ??= new(tl);
                    exceptions.Add(ex);
                }
            }
            if (exceptions != null)
                throw new AggregateException(exceptions);
            if (canceled)
                throw new TaskCanceledException();
            return results;
        }



        /// <summary>
        /// Creates a task that will complete when all of the supplied tasks have completed.
        /// </summary>
        /// <typeparam name="T">The result type of the tasks</typeparam>
        /// <param name="tasks">The tasks to wait on for completion (enumerated once).</param>
        /// <returns>A task that represents the completion of all of the supplied tasks, the result contains the results of the tasks in the same order as the supplied tasks.</returns>
        /// <remarks>
        /// <para>
        /// If any of the supplied tasks completes in a faulted state, the returned task will also complete in a Faulted state,
        /// where its exceptions will contain the aggregation of the set of unwrapped exceptions from each of the supplied tasks.
        /// </para>
        /// <para>
        /// If none of the supplied tasks faulted but at least one of them was canceled, the returned task will end in the Canceled state.
        /// </para>
        /// <para>
        /// If none of the tasks faulted and none of the tasks were canceled, the resulting task will end in the RanToCompletion state.
        /// </para>
        /// <para>
        /// If the supplied enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> tasks)
            => WhenAll(tasks?.ToArray());

        /// <summary>
        /// Creates a task that will complete when all of the supplied tasks have completed.
        /// </summary>
        /// <typeparam name="T">The result type of the tasks</typeparam>
        /// <param name="tasks">The tasks to wait on for completion.</param>
        /// <returns>A task that represents the completion of all of the supplied tasks, the result contains the results of the tasks in the same order as the supplied tasks.</returns>
        /// <remarks>
        /// <para>
        /// If any of the supplied tasks completes in a faulted state, the returned task will also complete in a Faulted state,
        /// where its exceptions will contain the aggregation of the set of unwrapped exceptions from each of the supplied tasks.
        /// </para>
        /// <para>
        /// If none of the supplied tasks faulted but at least one of them was canceled, the returned task will end in the Canceled state.
        /// </para>
        /// <para>
        /// If none of the tasks faulted and none of the tasks were canceled, the resulting task will end in the RanToCompletion state.
        /// </para>
        /// <para>
        /// If the supplied array contains no tasks, or all tasks are already completed successfully, the returned task is completed synchronously.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T[]> WhenAll<T>(params ValueTask<T>[] tasks)
            => WhenAll(tasks as IReadOnlyList<ValueTask<T>>);





        /// <summary>
        /// Creates a task that will complete when all of the supplied tasks have completed.
        /// </summary>
        /// <param name="tasks">The tasks to wait on for completion.</param>
        /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
        /// <remarks>
        /// <para>
        /// If any of the supplied tasks completes in a faulted state, the returned task will also complete in a Faulted state,
        /// where its exceptions will contain the aggregation of the set of unwrapped exceptions from each of the supplied tasks.
        /// </para>
        /// <para>
        /// If none of the supplied tasks faulted but at least one of them was canceled, the returned task will end in the Canceled state.
        /// </para>
        /// <para>
        /// If none of the tasks faulted and none of the tasks were canceled, the resulting task will end in the RanToCompletion state.
        /// </para>
        /// <para>
        /// If the supplied list contains no tasks, or all tasks are already completed successfully, the returned task is completed synchronously.
        /// </para>
        /// <para>
        /// Every value task is awaited (it's result consumed) exactly once, the list is read again (by index) after the returned task was created, so it must not be changed until the returned task completes.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        public static ValueTask WhenAll(
            IReadOnlyList<ValueTask> tasks)
        {
            ArgumentNullException.ThrowIfNull(tasks);
            var tl = tasks.Count;
            if (tl <= 0)
                return ValueTask.CompletedTask;
            // We don't allocate the list if no task throws
            for (var i = 0; i < tl; i++)
            {
                var t = tasks[i];
                if (!t.IsCompletedSuccessfully)
                    return InternalWhenAll(tasks, i, tl);
                // Consume the result (a value task backed by an IValueTaskSource is only released / reset when the result is consumed)
                t.GetAwaiter().GetResult();
            }
            return ValueTask.CompletedTask;
        }

        static async ValueTask InternalWhenAll(IReadOnlyList<ValueTask> tasks, int i, int tl)
        {
            List<Exception> exceptions = null;
            bool canceled = false;
            for (; i < tl; i++)
            {
                var t = tasks[i];
                try
                {
                    await t.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (WasCanceled(t, ex))
                    {
                        canceled = true;
                        continue;
                    }
                    exceptions ??= new(tl);
                    exceptions.Add(ex);
                }
            }
            if (exceptions != null)
                throw new AggregateException(exceptions);
            if (canceled)
                throw new TaskCanceledException();
        }


        /// <summary>
        /// Creates a task that will complete when all of the supplied tasks have completed.
        /// </summary>
        /// <param name="tasks">The tasks to wait on for completion (enumerated once).</param>
        /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
        /// <remarks>
        /// <para>
        /// If any of the supplied tasks completes in a faulted state, the returned task will also complete in a Faulted state,
        /// where its exceptions will contain the aggregation of the set of unwrapped exceptions from each of the supplied tasks.
        /// </para>
        /// <para>
        /// If none of the supplied tasks faulted but at least one of them was canceled, the returned task will end in the Canceled state.
        /// </para>
        /// <para>
        /// If none of the tasks faulted and none of the tasks were canceled, the resulting task will end in the RanToCompletion state.
        /// </para>
        /// <para>
        /// If the supplied enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WhenAll(IEnumerable<ValueTask> tasks)
            => WhenAll(tasks?.ToArray());

        /// <summary>
        /// Creates a task that will complete when all of the supplied tasks have completed.
        /// </summary>
        /// <param name="tasks">The tasks to wait on for completion.</param>
        /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
        /// <remarks>
        /// <para>
        /// If any of the supplied tasks completes in a faulted state, the returned task will also complete in a Faulted state,
        /// where its exceptions will contain the aggregation of the set of unwrapped exceptions from each of the supplied tasks.
        /// </para>
        /// <para>
        /// If none of the supplied tasks faulted but at least one of them was canceled, the returned task will end in the Canceled state.
        /// </para>
        /// <para>
        /// If none of the tasks faulted and none of the tasks were canceled, the resulting task will end in the RanToCompletion state.
        /// </para>
        /// <para>
        /// If the supplied array contains no tasks, or all tasks are already completed successfully, the returned task is completed synchronously.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WhenAll(params ValueTask[] tasks)
            => WhenAll(tasks as IReadOnlyList<ValueTask>);


    }

}
