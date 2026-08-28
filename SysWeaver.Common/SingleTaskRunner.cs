using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A class than let's you spawn an async task.
    /// Will wait for any previous task to complete before starting the new one.
    /// If a previous task throws an exception, the Start method will throw that excpetion.
    /// </summary>
    public sealed class SingleTaskRunner
    {
        readonly AsyncLock Lock = new AsyncLock();

        Exception Ex;


        /// <summary>
        /// Wait until any previous task is completed.
        /// If a previous task threw an exception, this method will throw that exception.
        /// </summary>
        /// <returns></returns>
        public async Task Wait()
        {
            using var d = await Lock.Lock().ConfigureAwait(false);
            var e = Interlocked.Exchange(ref Ex, null);
            if (e != null)
                throw e;
        }

        /// <summary>
        /// Wait until any previous task is completed.
        /// If a previous task threw an exception, this method will throw that exception.
        /// Start the supplied task ina new async chain, and return immediately.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public async Task Start(Func<Task> task)
        { 
            var d = await Lock.Lock().ConfigureAwait(false);
            var e = Interlocked.Exchange(ref Ex, null);
            if (e != null)
            {
                d.Dispose();
                throw e;
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
