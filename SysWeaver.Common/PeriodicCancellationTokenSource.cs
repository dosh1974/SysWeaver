using System;
using System.Threading;

namespace SysWeaver
{

    /// <summary>
    /// A cancellation token source that periodically executes a callback to determine if it's time to cancel
    /// </summary>
    /// <remarks>
    /// The callback is invoked on a thread pool timer thread, once it returns true (or throws an exception) the timer is stopped and the source is canceled.
    /// Dispose the instance to stop the timer.
    /// </remarks>
    public sealed class PeriodicCancellationTokenSource : CancellationTokenSource
    {
        /// <summary>
        /// Create a new cancellation token source that is canceled when <paramref name="cancel"/> returns true
        /// </summary>
        /// <param name="cancel">The function to call periodically, return true to cancel. An exception thrown by it also cancels the source</param>
        /// <param name="checkIntervallMs">Number of milliseconds between each check (and before the first check), values less than one are treated as one</param>
        public PeriodicCancellationTokenSource(Func<bool> cancel, int checkIntervallMs = 1000) : base()
        {
            DoCancel = cancel;
            var c = (uint)Math.Max(1, checkIntervallMs);
            var t = new Timer(OnEvent);
            T = t;
            t.Change(c, c);
        }

        readonly Func<bool> DoCancel;

        void OnEvent(Object state)
        {
            try
            {
                if (!DoCancel())
                    return;
            }
            catch
            {
            }
            try
            {
                Interlocked.Exchange(ref T, null)?.Dispose();
            }
            catch
            {
            }
            Cancel();
        }

        Timer T;

        /// <summary>
        /// Stops the timer and disposes the cancellation token source
        /// </summary>
        /// <param name="disposing">True if called from Dispose</param>
        protected override void Dispose(bool disposing)
        {
            Interlocked.Exchange(ref T, null)?.Dispose();
            base.Dispose(disposing);
        }
    }

}
