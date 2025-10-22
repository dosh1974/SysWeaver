using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class TaskExt
    {
        /// <summary>
        /// A completed value task
        /// </summary>
        public static readonly ValueTask CompValTask = default;

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        public static void StartNewAsyncChain(Func<ConfiguredTaskAwaitable> task) => Task.Run(task).ConfigureAwait(false);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        public static void StartNewAsyncChain(Func<Task> task) => Task.Run(() => task().ConfigureAwait(false)).ConfigureAwait(false);


        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        public static void StartNewAsyncChain(Func<ConfiguredValueTaskAwaitable> task) => Task.Run(task).ConfigureAwait(false);

        /// <summary>
        /// Start a new async task (new thread / new chain)
        /// </summary>
        /// <param name="task">A function that creates the new task, and then returns the result of ConfigureAwait(false) on it</param>
        public static void StartNewAsyncChain(Func<ValueTask> task) => Task.Run(() => task().ConfigureAwait(false)).ConfigureAwait(false);





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
                return t.Result;
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
        public static T RunAsync<T>(this Task<T> t)
        {
            try
            {
                Task.Run(() => t.ConfigureAwait(false)).ConfigureAwait(false);
                return t.Result;
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
        /// A complete task for a True boolean
        /// </summary>
        public static readonly Task<Boolean> TrueTask = Task.FromResult(true);

        /// <summary>
        /// A complete task for a True boolean
        /// </summary>
        public static readonly Task<Boolean> FalseTask = Task.FromResult(false);




        /// <summary>
        /// A complete value task for a null string
        /// </summary>
        public static readonly ValueTask<String> NullStringValueTask = ValueTask.FromResult<String>(null);

        /// <summary>
        /// A complete value task for a null string
        /// </summary>
        public static readonly ValueTask<String> EmptyStringValueTask = ValueTask.FromResult("");

        /// <summary>
        /// A complete value task for a True boolean
        /// </summary>
        public static readonly ValueTask<Boolean> TrueValueTask = ValueTask.FromResult(true);

        /// <summary>
        /// A complete value task for a True boolean
        /// </summary>
        public static readonly ValueTask<Boolean> FalseValueTask = ValueTask.FromResult(false);



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
            Timer t = null;
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
            }, null, delayInMs, Timeout.Infinite);
        }


        /// <summary>
        /// Execute a task after some fixed duration
        /// </summary>
        /// <param name="task">The task to execute</param>
        /// <param name="delayInMs">The delay in milli seconds</param>
        public static void RunDelayed(Task task, int delayInMs)
        {
            Timer t = null;
            t = new Timer(state =>
            {
                task.RunAsync();
                t.Dispose();
            }, null, delayInMs, Timeout.Infinite);
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


        public static Task RaiseEvents(this Func<Task> eventHandlers)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            var tasks = new Task[lc];
            for (int i = 0; i < lc; i++)
                tasks[i] = ((Func<Task>)l[i])();
            return Task.WhenAll(tasks);
        }

        public static Task RaiseEvents<A0>(this Func<A0, Task> eventHandlers, A0 a0)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            var tasks = new Task[lc];
            for (int i = 0; i < lc; i++)
                tasks[i] = ((Func<A0, Task>)l[i])(a0);
            return Task.WhenAll(tasks);
        }

        public static Task RaiseEvents<A0, A1>(this Func<A0, A1, Task> eventHandlers, A0 a0, A1 a1)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            var tasks = new Task[lc];
            for (int i = 0; i < lc; i++)
                tasks[i] = ((Func<A0, A1, Task>)l[i])(a0, a1);
            return Task.WhenAll(tasks);
        }

        public static Task RaiseEvents<A0, A1, A2>(this Func<A0, A1, A2, Task> eventHandlers, A0 a0, A1 a1, A2 a2)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            var tasks = new Task[lc];
            for (int i = 0; i < lc; i++)
                tasks[i] = ((Func<A0, A1, A2, Task>)l[i])(a0, a1, a2);
            return Task.WhenAll(tasks);
        }

        public static Task RaiseEvents<A0, A1, A2, A3>(this Func<A0, A1, A2, A3, Task> eventHandlers, A0 a0, A1 a1, A2 a2, A3 a3)
        {
            if (eventHandlers == null)
                return Task.CompletedTask;
            Delegate[] l = eventHandlers.GetInvocationList();
            var lc = l.Length;
            var tasks = new Task[lc];
            for (int i = 0; i < lc; i++)
                tasks[i] = ((Func<A0, A1, A2, A3, Task>)l[i])(a0, a1, a2, a3);
            return Task.WhenAll(tasks);
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
        public static async ValueTask<T[]> WhenAll<T>(
            IReadOnlyList<ValueTask<T>> tasks)
        {
            ArgumentNullException.ThrowIfNull(tasks);
            var tl = tasks.Count;
            if (tl <= 0)
                return Array.Empty<T>();

            // We don't allocate the list if no task throws
            List<Exception> exceptions = null;
            var results = GC.AllocateUninitializedArray<T>(tl);
            for (var i = 0; i < tl; i++)
                try
                {
                    results[i] = await tasks[i].ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions ??= new(tl);
                    exceptions.Add(ex);
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
        public static ValueTask<T[]> WhenAll<T>(params ValueTask<T>[] tasks)
            => WhenAll(tasks as IReadOnlyList<ValueTask<T>>);



        /////////////////
        ///


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
        public static async ValueTask WhenAll(
            IReadOnlyList<ValueTask> tasks)
        {
            ArgumentNullException.ThrowIfNull(tasks);
            var tl = tasks.Count;
            if (tl <= 0)
                return;

            // We don't allocate the list if no task throws
            List<Exception> exceptions = null;
            for (var i = 0; i < tl; i++)
                try
                {
                    await tasks[i].ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions ??= new(tl);
                    exceptions.Add(ex);
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
        public static ValueTask WhenAll(params ValueTask[] tasks)
            => WhenAll(tasks as IReadOnlyList<ValueTask>);

    }
}
