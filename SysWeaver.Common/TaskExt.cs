using Microsoft.VisualBasic;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    public static class TaskExt<T> 
    {
        public static readonly Task<T> NullTask = Task.FromResult(default(T));
        public static readonly ValueTask<T> NullValueTask = ValueTask.FromResult(default(T));
        public static readonly Task<T[]> EmptyArrayTask = Task.FromResult(Array.Empty<T>());

        public static readonly ValueTask<T[]> EmptyArrayValueTask = ValueTask.FromResult(Array.Empty<T>());

    }

    public static class TaskExt
    {
          /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChain(Func<ConfiguredTaskAwaitable> task) => Task.Run(task);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChain(Func<Task> task) => Task.Run(task);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChain(this Func<ConfiguredValueTaskAwaitable> task) => Task.Run(task);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartNewAsyncChainValue(this Func<ValueTask> task) => Task.Run(task);

        /// <summary>
        /// Run a task in a new thread / chain, then wait for the task to complete and return it's value
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="t">The task to run</param>
        /// <returns>The return value of the task</returns>
        public static T RunAsync<T>(this Task<T> t)
        {
            try
            {
                Task.Run(() => t.ConfigureAwait(false)).ConfigureAwait(false);
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
        /// Run a task in a new thread / chain, then wait for the task to complete
        /// </summary>
        /// <param name="t">The task to run</param>
        public static void RunAsync(this ValueTask t)
        {
            try
            {
                Task.Run(() => t.ConfigureAwait(false)).ConfigureAwait(false);
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
        /// Run a task in a new thread / chain, then wait for the task to complete and return it's value
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="t">The task to run</param>
        /// <returns>The return value of the task</returns>
        public static T RunAsync<T>(this ValueTask<T> t)
        {
            try
            {
                Task.Run(() => t.ConfigureAwait(false)).ConfigureAwait(false);
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
        /// Run a task in a new thread / chain, then wait for the task to complete
        /// </summary>
        /// <param name="t">The task to run</param>
        public static void RunAsync(this Task t)
        {
            try
            {
                Task.Run(() => t.ConfigureAwait(false)).ConfigureAwait(false);
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
        /// A complete task for a null string
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
        /// A complete task for a True boolean
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
        /// A complete value task for a null string
        /// </summary>
        public static readonly ValueTask<String> EmptyStringValueTask = ValueTask.FromResult("");

        /// <summary>
        /// A complete task for an empty string array
        /// </summary>
        public static readonly ValueTask<String[]> EmptyStringArrayValueTask = ValueTask.FromResult(Array.Empty<String>());

        /// <summary>
        /// A complete value task for a True boolean
        /// </summary>
        public static readonly ValueTask<Boolean> TrueValueTask = ValueTask.FromResult(true);

        /// <summary>
        /// A complete value task for a True boolean
        /// </summary>
        public static readonly ValueTask<Boolean> FalseValueTask = ValueTask.FromResult(false);

        /// <summary>
        /// A complete task for an empty read only memory buffer
        /// </summary>
        public static readonly ValueTask<ReadOnlyMemory<Byte>> ReadonlyMemoryValueTask = ValueTask.FromResult(ReadOnlyMemory<Byte>.Empty);

        /// <summary>
        /// A complete task for an empty read only memory buffer
        /// </summary>
        public static readonly ValueTask<Memory<Byte>> MemoryValueTask = ValueTask.FromResult(Memory<Byte>.Empty);

        /// <summary>
        /// Task that delays a small random amount
        /// </summary>
        /// <param name="min">Minimum delay in ms</param>
        /// <param name="mask">Bitmask for the delay to add: delay = min + (RandomByte &amp; mask)</param>
        /// <returns></returns>
        public static Task RandomDelay(int min = 1, int mask = 0xf)
        {
            int delay = min;
            using (var rng = SecureRng.Get())
                delay += (rng.GetByte() & mask);
            return Task.Delay(delay);
        }


        /// <summary>
        /// Run a function after some fixed duration
        /// </summary>
        /// <param name="func">The function to execute</param>
        /// <param name="delayInMs">The delay in milli seconds</param>
        public static void RunDelayed(Action func, int delayInMs)
        {
            StartNewAsyncChain(() => Task.Delay(delayInMs).ContinueWith(x => func()).ConfigureAwait(false));
/*            Timer t = null;
            t = new Timer(state =>
            {
                try
                {
                    func();
                }
                catch
                {
                }
                t.Dispose();
            }, null, delayInMs, Timeout.Infinite);*/
        }


        /// <summary>
        /// Execute a task after some fixed duration
        /// </summary>
        /// <param name="task">The task to execute</param>
        /// <param name="delayInMs">The delay in milli seconds</param>
        public static void RunDelayed(Task task, int delayInMs)
        {
            StartNewAsyncChain(() => Task.Delay(delayInMs).ContinueWith(x => task));
/*            Timer t = null;
            t = new Timer(state =>
            {
                task.RunAsync();
                t.Dispose();
            }, null, delayInMs, Timeout.Infinite);*/
        }

        /// <summary>
        /// Execute a task after some fixed duration
        /// </summary>
        /// <param name="task">The task to execute</param>
        /// <param name="delayInMs">The delay in milli seconds</param>
        public static void RunDelayed(ValueTask task, int delayInMs)
        {
            StartNewAsyncChain(() => Task.Delay(delayInMs).ContinueWith(x => task));
        }



        public static Task WaitOneAsync(this WaitHandle waitHandle, int timeoutMilliseconds = Timeout.Infinite)
        {
            if (waitHandle == null)
                throw new ArgumentNullException(nameof(waitHandle));

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle,
                (_, timedOut) =>
                {
                    if (timedOut)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                },
                null, timeoutMilliseconds, true);

            Task<bool> task = tcs.Task;

            _ = task.ContinueWith(_ =>
            {
                rwh.Unregister(null);
            }, CancellationToken.None);

            return task;
        }

        public static Task WaitOneAsync(this WaitHandle waitHandle, CancellationToken cancellationToken, int timeoutMilliseconds = Timeout.Infinite)
        {
            if (waitHandle == null)
                throw new ArgumentNullException(nameof(waitHandle));

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            CancellationTokenRegistration ctr = cancellationToken.Register(() => tcs.TrySetCanceled());
            TimeSpan timeout = timeoutMilliseconds > Timeout.Infinite ? TimeSpan.FromMilliseconds(timeoutMilliseconds) : Timeout.InfiniteTimeSpan;

            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle,
                (_, timedOut) =>
                {
                    if (timedOut)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                },
                null, timeout, true);

            Task<bool> task = tcs.Task;

            _ = task.ContinueWith(_ =>
            {
                rwh.Unregister(null);
                return ctr.Unregister();
            }, CancellationToken.None);

            return task;
        }


        /// <summary>
        /// Like Task.WhenAll but running in serial (for debugging)
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public static async Task WhenAllDebug(IEnumerable<Task> tasks)
        {
            foreach (var t in tasks)
                await t.ConfigureAwait(false);
        }


        #region Async events

        public static IEnumerable<Stats> GetEventExceptionStats() => EventExceptions.GetStats(nameof(TaskExt), "EventExceptions.");

        static readonly ExceptionTracker EventExceptions = new ExceptionTracker();

        static readonly Action<Exception> OnEventException = ex => EventExceptions.OnException(ex);

        #region Async


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents(this Func<Task> eventHandlers)
            => RaiseEvents(eventHandlers, OnEventException);


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0>(this Func<A0, Task> eventHandlers, A0 a0)
            => RaiseEvents(eventHandlers, OnEventException, a0);


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, Task> eventHandlers, A0 a0, A1 a1)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1);


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, Task> eventHandlers, A0 a0, A1 a1, A2 a2)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2);

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, Task> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2, a3);



        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <returns></returns>
        public static async Task RaiseEvents(this Func<Task> eventHandlers, Action<Exception> onException)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<Task>)l[0])().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del().ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0>(this Func<A0, Task> eventHandlers, Action<Exception> onException, A0 a0)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, Task>)l[0])(a0).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0, A1>(this Func<A0, A1, Task> eventHandlers, Action<Exception> onException, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, A1, Task>)l[0])(a0, a1).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, A1, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0, a1).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, Task> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, A1, A2, Task>)l[0])(a0, a1, a2).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, A1, A2, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0, a1, a2).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, Task> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, A1, A2, A3, Task>)l[0])(a0, a1, a2, a3).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, A1, A2, A3, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0, a1, a2, a3).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }


        #endregion//Async

        #region AsyncValue


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents(this Func<ValueTask> eventHandlers)
            => RaiseEvents(eventHandlers, OnEventException);


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0>(this Func<A0, ValueTask> eventHandlers, A0 a0)
            => RaiseEvents(eventHandlers, OnEventException, a0);


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, ValueTask> eventHandlers, A0 a0, A1 a1)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1);


        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, ValueTask> eventHandlers, A0 a0, A1 a1, A2 a2)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2);

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, ValueTask> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2, a3);



        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <returns></returns>
        public static async Task RaiseEvents(this Func<ValueTask> eventHandlers, Action<Exception> onException)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<Task>)l[0])().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del().ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0>(this Func<A0, ValueTask> eventHandlers, Action<Exception> onException, A0 a0)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, Task>)l[0])(a0).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0, A1>(this Func<A0, A1, ValueTask> eventHandlers, Action<Exception> onException, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, A1, Task>)l[0])(a0, a1).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, A1, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0, a1).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, ValueTask> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, A1, A2, Task>)l[0])(a0, a1, a2).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, A1, A2, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0, a1, a2).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        /// <summary>
        /// Raise all async events in paralell without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception, must be thread safe!</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        /// <returns></returns>
        public static async Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, ValueTask> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            if (lc <= 0)
                return;
            if (lc == 1)
            {
                try
                {
                    await ((Func<A0, A1, A2, A3, Task>)l[0])(a0, a1, a2, a3).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    onException(e);
                }
                return;
            }
            onException = onException ?? OnEventException;
            var tasks = ArrayPool<Task>.Shared.Rent(lc);
            try
            {
                for (int i = 0; i < lc; i++)
                {
                    var del = (Func<A0, A1, A2, A3, Task>)l[i];
                    async Task Fn()
                    {
                        try
                        {
                            await del(a0, a1, a2, a3).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            onException(e);
                        }
                    }
                    tasks[i] = Fn();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        #endregion// AsyncValue

        #region Sync

        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents(this Action eventHandlers)
            => RaiseEvents(eventHandlers, OnEventException);


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0>(this Action<A0> eventHandlers, A0 a0)
            => RaiseEvents(eventHandlers, OnEventException, a0);


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0, A1>(this Action<A0, A1> eventHandlers, A0 a0, A1 a1)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1);


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0, A1, A2>(this Action<A0, A1, A2> eventHandlers, A0 a0, A1 a1, A2 a2)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2);


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RaiseEvents<A0, A1, A2, A3>(this Action<A0, A1, A2, A3> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
            => RaiseEvents(eventHandlers, OnEventException, a0, a1, a2, a3);


        /// <summary>
        /// Raise all events without throwing
        /// </summary>
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception</param>
        public static void RaiseEvents(this Action eventHandlers, Action<Exception> onException)
        {
            if (eventHandlers == null)
                return;
            onException = onException ?? OnEventException;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            for (int i = 0; i < lc; i++)
            {
                try
                {
                    ((Action)l[i])();
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
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception</param>
        /// <param name="a0">Action argument 0</param>
        public static void RaiseEvents<A0>(this Action<A0> eventHandlers, Action<Exception> onException, A0 a0)
        {
            if (eventHandlers == null)
                return;
            onException = onException ?? OnEventException;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            for (int i = 0; i < lc; i++)
            {
                try
                {
                    ((Action<A0>)l[i])(a0);
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
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        public static void RaiseEvents<A0, A1>(this Action<A0, A1> eventHandlers, Action<Exception> onException, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return;
            onException = onException ?? OnEventException;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            for (int i = 0; i < lc; i++)
            {
                try
                {
                    ((Action<A0, A1>)l[i])(a0, a1);
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
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        public static void RaiseEvents<A0, A1, A2>(this Action<A0, A1, A2> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return;
            onException = onException ?? OnEventException;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            for (int i = 0; i < lc; i++)
            {
                try
                {
                    ((Action<A0, A1, A2>)l[i])(a0, a1, a2);
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
        /// <param name="eventHandlers"></param>
        /// <param name="onException">An action to perform on each exception</param>
        /// <param name="a0">Action argument 0</param>
        /// <param name="a1">Action argument 1</param>
        /// <param name="a2">Action argument 2</param>
        /// <param name="a3">Action argument 3</param>
        public static void RaiseEvents<A0, A1, A2, A3>(this Action<A0, A1, A2, A3> eventHandlers, Action<Exception> onException, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return;
            onException = onException ?? OnEventException;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            for (int i = 0; i < lc; i++)
            {
                try
                {
                    ((Action<A0, A1, A2, A3>)l[i])(a0, a1, a2, a3);
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
        /// If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="tasks"/> array contained a null task.
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
                if (!t.IsCompleted)
                    return InternalWhenAll(tasks, results, i, tl);
                results[i] = t.GetAwaiter().GetResult();
            }
            return ValueTask.FromResult(results);
        }

        static async ValueTask<T[]> InternalWhenAll<T>(IReadOnlyList<ValueTask<T>> tasks, T[] results, int i, int tl)
        {
            List<Exception> exceptions = null;
            for (; i < tl; i++)
            {
                var t = tasks[i];
                if (t.IsCompleted)
                {
                    results[i] = t.GetAwaiter().GetResult();
                    continue;
                }
                try
                {
                    results[i] = await t.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions ??= new(tl);
                    exceptions.Add(ex);
                }
            }
            return exceptions is null
                ? results
                : throw new AggregateException(exceptions);
        }



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
        /// If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="tasks"/> array contained a null task.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> tasks)
            => WhenAll(tasks?.ToList());

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
        /// If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="tasks"/> array contained a null task.
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
        /// If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="tasks"/> array contained a null task.
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
                if (!t.IsCompleted)
                    return InternalWhenAll(tasks, i, tl);
            }
            return ValueTask.CompletedTask;
        }

        static async ValueTask InternalWhenAll(IReadOnlyList<ValueTask> tasks, int i, int tl)
        {
            List<Exception> exceptions = null;
            for (; i < tl; i++)
            {
                var t = tasks[i];
                if (t.IsCompleted)
                    continue;
                try
                {
                    await t.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions ??= new(tl);
                    exceptions.Add(ex);
                }
            }
            if (exceptions != null)
                throw new AggregateException(exceptions);
        }


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
        /// If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="tasks"/> array contained a null task.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WhenAll(IEnumerable<ValueTask> tasks)
            => WhenAll(tasks?.ToList());

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
        /// If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion
        /// state before it's returned to the caller.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="tasks"/> argument was null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="tasks"/> array contained a null task.
        /// </exception>        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WhenAll(params ValueTask[] tasks)
            => WhenAll(tasks as IReadOnlyList<ValueTask>);


    }

}
