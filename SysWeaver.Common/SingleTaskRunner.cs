using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A class than let's you spawn an async task.
    /// Will wait for any previous task to complete before starting the new one.
    /// If a previous task throws an exception, the Start or Wait method will throw that exception.
    /// </summary>
    /// <remarks>
    /// Tasks are run one at a time, in the order that the Start calls acquire the internal lock.
    /// An exception thrown by a task is only reported once (by the next Start or Wait call), the original stack trace is preserved.
    /// </remarks>
    public sealed class SingleTaskRunner
    {
        readonly AsyncLock Lock = new AsyncLock();

        Exception Ex;


        /// <summary>
        /// Wait until any previous task is completed.
        /// If a previous task threw an exception, this method will throw that exception.
        /// </summary>
        /// <returns>A task that completes when all previously started tasks have completed</returns>
        /// <exception cref="Exception">Any exception thrown by the previous task (re-thrown once)</exception>
        public async Task Wait()
        {
            using var d = await Lock.Lock().ConfigureAwait(false);
            var e = Interlocked.Exchange(ref Ex, null);
            if (e != null)
                ExceptionDispatchInfo.Throw(e);
        }

        /// <summary>
        /// Wait until any previous task is completed.
        /// If a previous task threw an exception, this method will throw that exception.
        /// Start the supplied task in a new async chain, and return immediately.
        /// </summary>
        /// <param name="task">A function that creates the task to run, it's invoked on the thread pool</param>
        /// <returns>A task that completes when the previous task have completed and the new task have been started (not when the new task completes)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is null (the returned task is faulted)</exception>
        /// <exception cref="Exception">Any exception thrown by the previous task (re-thrown once), the new task is not started</exception>
        public async Task Start(Func<Task> task)
        {
            ArgumentNullException.ThrowIfNull(task);
            var d = await Lock.Lock().ConfigureAwait(false);
            var e = Interlocked.Exchange(ref Ex, null);
            if (e != null)
            {
                d.Dispose();
                ExceptionDispatchInfo.Throw(e);
            }
            TaskExt.StartNewAsyncChain(async () =>
            {
                try
                {
                    await task().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Interlocked.Exchange(ref Ex, ex);
                }
                finally
                {
                    d.Dispose();
                }
            });
        }





    }

}
