using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Helpers that retry an operation that throws, with a fixed delay between attempts.
    /// Typically used for file system operations that can fail temporarily (files locked by another process, anti virus scanners etc).
    /// </summary>
    /// <remarks>
    /// Any exception triggers a retry (there is no filtering), when all attempts have failed the last exception is re-thrown.
    /// </remarks>
    public static class Retry
    {
        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <exception cref="Exception">The exception thrown by the last attempt</exception>
        /// <remarks>Blocks the calling thread between attempts (<see cref="Thread.Sleep(int)"/>)</remarks>
        public static void Op(Action op, int retryCount = 10, int delayInMs = 100)
        {
            for (; ; )
            {
                try
                {
                    op();
                    return;
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        throw;
                    Thread.Sleep(delayInMs);
                }
            }
        }

        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <typeparam name="R">The result type</typeparam>
        /// <returns>The result of the first successful attempt</returns>
        /// <exception cref="Exception">The exception thrown by the last attempt</exception>
        /// <remarks>Blocks the calling thread between attempts (<see cref="Thread.Sleep(int)"/>)</remarks>
        public static R Op<R>(Func<R> op, int retryCount = 10, int delayInMs = 100)
        {
            for (; ; )
            {
                try
                {
                    return op();
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        throw;
                    Thread.Sleep(delayInMs);
                }
            }
        }


        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <returns>A task that completes when an attempt succeeded, it faults with the exception of the last attempt if all attempts failed</returns>
        public static async Task OpAsync(Func<Task> op, int retryCount = 10, int delayInMs = 100)
        {
            for (; ; )
            {
                try
                {
                    await op().ConfigureAwait(false);
                    return;
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        throw;
                    await Task.Delay(delayInMs).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <typeparam name="R">The result type</typeparam>
        /// <returns>The result of the first successful attempt, the task faults with the exception of the last attempt if all attempts failed</returns>
        public static async Task<R> OpAsync<R>(Func<Task<R>> op, int retryCount = 10, int delayInMs = 100)
        {
            for (; ; )
            {
                try
                {
                    return await op().ConfigureAwait(false);
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        throw;
                    await Task.Delay(delayInMs).ConfigureAwait(false);
                }
            }
        }


        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <returns>A value task that completes when an attempt succeeded, it faults with the exception of the last attempt if all attempts failed</returns>
        /// <remarks>
        /// If the first attempt returns an already completed value task it's returned as is, a synchronously faulted value task is therefore NOT retried.
        /// If the first attempt throws synchronously, the second attempt is made immediately (without a delay).
        /// </remarks>
        public static ValueTask OpAsync(Func<ValueTask> op, int retryCount = 10, int delayInMs = 100)
        {
            bool needTask = true;
            ValueTask task = ValueTask.CompletedTask;
            try
            {
                task = op();
                if (task.IsCompleted)
                    return task;
                needTask = false;
            }
            catch
            {
                --retryCount;
                if (retryCount <= 0)
                    throw;
            }
            return doIt();

            async ValueTask doIt()
            { 
                for (; ; )
                {
                    try
                    {
                        if (needTask)
                            task = op();
                        if (!task.IsCompleted)
                            await task.ConfigureAwait(false);
                        return;
                    }
                    catch
                    {
                        --retryCount;
                        if (retryCount <= 0)
                            throw;
                        await Task.Delay(delayInMs).ConfigureAwait(false);
                    }
                    needTask = true;
                }
            }
        }

        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <typeparam name="R">The result type</typeparam>
        /// <returns>The result of the first successful attempt, the value task faults with the exception of the last attempt if all attempts failed</returns>
        /// <remarks>
        /// If the first attempt returns an already completed value task it's returned as is, a synchronously faulted value task is therefore NOT retried.
        /// If the first attempt throws synchronously, the second attempt is made immediately (without a delay).
        /// </remarks>
        public static ValueTask<R> OpAsync<R>(Func<ValueTask<R>> op, int retryCount = 10, int delayInMs = 100)
        {
            bool needTask = true;
            ValueTask<R> task = ValueTask.FromResult<R>(default);
            try
            {
                task = op();
                if (task.IsCompleted)
                    return task;
                needTask = false;
            }
            catch
            {
                --retryCount;
                if (retryCount <= 0)
                    throw;
            }
            return doIt();

            async ValueTask<R> doIt()
            {
                for (; ; )
                {
                    try
                    {
                        if (needTask)
                            task = op();
                        if (!task.IsCompleted)
                            return await task.ConfigureAwait(false);
                        return task.GetAwaiter().GetResult();
                    }
                    catch
                    {
                        --retryCount;
                        if (retryCount <= 0)
                            throw;
                        await Task.Delay(delayInMs).ConfigureAwait(false);
                    }
                    needTask = true;
                }
            }
        }


        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <returns>A task that completes when an attempt succeeded, it faults with the exception of the last attempt if all attempts failed</returns>
        /// <remarks>The operation is synchronous, but the delay between attempts is async (no thread is blocked while waiting)</remarks>
        public static async Task OpAsync(Action op, int retryCount = 10, int delayInMs = 100)
        {
            for (; ; )
            {
                try
                {
                    op();
                    return;
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        throw;
                    await Task.Delay(delayInMs).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">The maximum number of attempts in total (not the number of retries), values less than two means a single attempt</param>
        /// <param name="delayInMs">Number of milli seconds to wait between attempts</param>
        /// <typeparam name="R">The result type</typeparam>
        /// <returns>The result of the first successful attempt, the task faults with the exception of the last attempt if all attempts failed</returns>
        /// <remarks>The operation is synchronous, but the delay between attempts is async (no thread is blocked while waiting)</remarks>
        public static async Task<R> OpAsync<R>(Func<R> op, int retryCount = 10, int delayInMs = 100)
        {
            for (; ; )
            {
                try
                {
                    return op();
                }
                catch
                {
                    --retryCount;
                    if (retryCount <= 0)
                        throw;
                    await Task.Delay(delayInMs).ConfigureAwait(false);
                }
            }
        }


    }


}
