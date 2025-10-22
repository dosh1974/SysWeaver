using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class Retry
    {
        /// <summary>
        /// Retry an operation
        /// </summary>
        /// <param name="op">The operation to perform</param>
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        public static async ValueTask OpAsync(Func<ValueTask> op, int retryCount = 10, int delayInMs = 100)
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
        public static async ValueTask<R> OpAsync<R>(Func<ValueTask<R>> op, int retryCount = 10, int delayInMs = 100)
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
        /// <param name="retryCount">Number of times to retry the operation (create folder)</param>
        /// <param name="delayInMs">Number of milli seconds to wait between any retries</param>
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
