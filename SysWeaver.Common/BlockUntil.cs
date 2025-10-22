using System;

using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{


    /// <summary>
    /// Provides a mechanism to async wait for a "change".
    /// Changes are tracked using a "change id" that is supplied internally.
    /// </summary>
    public sealed class BlockUntilChange : BlockUntil
    {
        /// <summary>
        /// Provides a mechanism to async wait for a "change".
        /// Changes are tracked using a "change id" that is supplied internally.
        /// </summary>
        /// <param name="startWithChange">If true, the supplied id starts at 1, else 0 (listeners should start with the change id 0, so if this is true, the first wait for change will return immediatelty)</param>
        public BlockUntilChange(bool startWithChange = true) : base(startWithChange ? 1 : 0)
        {
        }

        /// <summary>
        /// Triggers a change, any task waiting for a change on this instance will continue and return a new change id.
        /// </summary>
        /// <returns>The new change id</returns>
        public long Change()
        {
            using (AfterChange())
                return Interlocked.Increment(ref C);
        }
    }

    /// <summary>
    /// Provides a mechanism to async wait for a "change".
    /// Changes are tracked using a "change id" that is supplied by the calling code.
    /// </summary>
    public sealed class BlockUntilValueChange : BlockUntil
    {
        /// <summary>
        /// Provides a mechanism to async wait for a "change".
        /// Changes are tracked using a "change id" that is supplied by the calling code.
        /// </summary>
        /// <param name="startChangeId">The change id to start with</param>
        public BlockUntilValueChange(long startChangeId) : base(startChangeId)
        {
        }

        /// <summary>
        /// Triggers a change, any task waiting for a change on this instance will continue and return the new change id.
        /// </summary>
        /// <param name="newChangeId"></param>
        public void Change(long newChangeId)
        {
            using (AfterChange())
                Interlocked.Exchange(ref C, newChangeId);
        }
    }

    internal sealed class StateBlockUntil : IDisposable
    {
        public readonly SemaphoreSlim W = new SemaphoreSlim(0, 1);
        public readonly SemaphoreSlim C = new SemaphoreSlim(0, 1);
        public long Count = 1;

        public async Task WaitForChange(int msToWait, CancellationToken cancel)
        {
            if (Interlocked.Increment(ref Count) <= 1)
            {
                Interlocked.Decrement(ref Count);
                return;
            }
            try
            {
                var w = W;
                if (await w.WaitAsync(msToWait, cancel).ConfigureAwait(false))
                    w.Release();
            }
            catch
            {
            }
            if (Interlocked.Decrement(ref Count) == 0)
                C.Release();
        }

        public async Task WaitForChange(CancellationToken cancel)
        {
            if (Interlocked.Increment(ref Count) <= 1)
            {
                Interlocked.Decrement(ref Count);
                return;
            }
            try
            {
                var w = W;
                await w.WaitAsync(cancel).ConfigureAwait(false);
                w.Release();
            }
            catch
            {
            }
            if (Interlocked.Decrement(ref Count) == 0)
                C.Release();
        }


        public async Task WaitForChange(int msToWait)
        {
            if (Interlocked.Increment(ref Count) <= 1)
            {
                Interlocked.Decrement(ref Count);
                return;
            }
            try
            {
                var w = W;
                if (await w.WaitAsync(msToWait).ConfigureAwait(false))
                    w.Release();
            }
            catch
            {
            }
            if (Interlocked.Decrement(ref Count) == 0)
                C.Release();
        }


        public async Task WaitForChange()
        {
            if (Interlocked.Increment(ref Count) <= 1)
            {
                Interlocked.Decrement(ref Count);
                return;
            }
            try
            {
                var w = W;
                await w.WaitAsync().ConfigureAwait(false);
                w.Release();
            }
            catch
            {
            }
            if (Interlocked.Decrement(ref Count) == 0)
                C.Release();
        }

        async Task End()
        {
            var w = W;
            var c = C;
            w.Release();
            if (Interlocked.Decrement(ref Count) == 0)
                c.Release();
            await c.WaitAsync().ConfigureAwait(false);
            if (Interlocked.Read(ref AllocCount) < 100)
            {
                //  Reuse
                Count = 1;
                w.Wait();
                for (; ; )
                {
                    var f = AllocFirst;
                    Next = f;
                    if (Interlocked.CompareExchange(ref AllocFirst, this, f) == f)
                    {
                        Interlocked.Increment(ref AllocCount);
                        break;
                    }
                }
                return;
            }
            c.Release();
            w.Dispose();
            c.Dispose();
        }

        public void Dispose()
        {
            TaskExt.StartNewAsyncChain(End);
        }


        StateBlockUntil()
        {
        }

        public static StateBlockUntil Get()
        {
            for (; ; )
            {
                var f = AllocFirst;
                if (f == null)
                    break;
                var next = f.Next;
                if (Interlocked.CompareExchange(ref AllocFirst, next, f) == f)
                {
                    Interlocked.Decrement(ref AllocCount);
                    return f;
                }
            }
            Interlocked.Increment(ref TotalAllocCount);
            return new StateBlockUntil();
        }

        public static long TotalAllocCount;
        public static long AllocCount;
        static StateBlockUntil AllocFirst;

        StateBlockUntil Next;


    }


    public abstract class BlockUntil : IDisposable
    {
        protected BlockUntil(long current)
        {
            C = current;
            S = StateBlockUntil.Get();
        }
        public long Cc => Interlocked.Read(ref C);

        StateBlockUntil S;
        protected long C;

        bool IsDisposed;

        /// <summary>
        /// Any waiting tasks will continue returning the currentChangeId change id.
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
            Interlocked.Exchange(ref S, null).Dispose();
        }

        protected IDisposable AfterChange() => Interlocked.Exchange(ref S, IsDisposed ? null : StateBlockUntil.Get());

        /// <summary>
        /// Total number of wait objects allocated
        /// </summary>
        public static long TotalAllocCount => Interlocked.Read(ref StateBlockUntil.TotalAllocCount);

        /// <summary>
        /// Total number of wait objects that are unused, roughly 100 is allowed.
        /// </summary>
        public static long AllocatedUnused => Interlocked.Read(ref StateBlockUntil.AllocCount);


        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typiacally start at 0 and then update with the result of this method</param>
        /// <param name="msToWait">Number of ms to wait, when expired, the method will return with the same change id.</param>
        /// <param name="cancel">Custom cancellation, if triggered, the method will return with the same change id.</param>
        /// <returns>The new change id (if changed), or the currentChangeId change id if the wait is aborted</returns>
        public async Task<long> WaitForChange(long currentChangeId, int msToWait, CancellationToken cancel)
        {
            var s = S;
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange(msToWait, cancel).ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }

        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typiacally start at 0 and then update with the result of this method</param>
        /// <param name="cancel">Custom cancellation, if triggered, the method will return with the same change id.</param>
        /// <returns>The new change id (if changed), or the currentChangeId change id if the wait is aborted</returns>
        public async Task<long> WaitForChange(long currentChangeId, CancellationToken cancel)
        {
            var s = S;
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange(cancel).ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }


        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typiacally start at 0 and then update with the result of this method</param>
        /// <param name="msToWait">Number of ms to wait, when expired, the method will return with the same change id.</param>
        /// <returns>The new change id (if changed), or the currentChangeId change id if the wait is aborted</returns>
        public async Task<long> WaitForChange(long currentChangeId, int msToWait)
        {
            var s = S;
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange(msToWait).ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }

        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typiacally start at 0 and then update with the result of this method</param>
        /// <returns>The new change id (if changed), or the currentChangeId change id if the wait is aborted</returns>
        public async Task<long> WaitForChange(long currentChangeId)
        {
            var s = S;
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange().ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }

    }


}
