using System;
using System.Threading;

namespace SysWeaver
{

    /// <summary>
    /// A cancellation token source that periodically exexcutes a callback to determine if it's time to cancel
    /// </summary>
    public sealed class PeriodicCancellationTokenSource : CancellationTokenSource
    {
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

        protected override void Dispose(bool disposing)
        {
            Interlocked.Exchange(ref T, null)?.Dispose();
            base.Dispose(disposing);
        }
    }

}
