using System;

using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{


    /// <summary>
    /// Provides a mechanism to async wait for a "change".
    /// Changes are tracked using a "change id" (a counter) that is incremented by every <see cref="Change"/> call.
    /// </summary>
    /// <remarks>
    /// Typical usage (long polling): a consumer starts with change id 0 and loops calling <see cref="BlockUntil.WaitForChange(long, int)"/>
    /// with the last returned id, a producer calls <see cref="Change"/> whenever new data is available.
    /// Thread safe, any number of producers and waiters can use the same instance.
    /// </remarks>
    public sealed class BlockUntilChange : BlockUntil
    {
        /// <summary>
        /// Create a new change tracker.
        /// </summary>
        /// <param name="startWithChange">If true, the change id starts at 1, else 0 (listeners should start with the change id 0, so if this is true, the first wait for change will return immediately)</param>
        public BlockUntilChange(bool startWithChange = true) : base(startWithChange ? 1 : 0)
        {
        }

        /// <summary>
        /// Triggers a change, any task waiting for a change on this instance will continue and return a new change id.
        /// </summary>
        /// <returns>The new change id (the previous id plus one)</returns>
        /// <remarks>Calling this after <see cref="BlockUntil.Dispose"/> still increments the change id, but no waiter is woken (waits return immediately after dispose).</remarks>
        public long Change()
        {
            //  Publish the new change id before swapping in a new wait state (a waiter that sees the new state must also see the new id)
            var c = Interlocked.Increment(ref C);
            AfterChange()?.Dispose();
            return c;
        }
    }

    /// <summary>
    /// Provides a mechanism to async wait for a "change".
    /// Changes are tracked using a "change id" that is supplied by the calling code (for example a version number or a time stamp).
    /// </summary>
    /// <remarks>Thread safe, any number of producers and waiters can use the same instance.</remarks>
    public sealed class BlockUntilValueChange : BlockUntil
    {
        /// <summary>
        /// Create a new change tracker.
        /// </summary>
        /// <param name="startChangeId">The change id to start with</param>
        public BlockUntilValueChange(long startChangeId) : base(startChangeId)
        {
        }

        /// <summary>
        /// Triggers a change, any task waiting for a change on this instance will continue and return the new change id.
        /// </summary>
        /// <param name="newChangeId">The new change id, waiters are woken even if it's equal to the current change id (they will then return the same id)</param>
        public void Change(long newChangeId)
        {
            //  Publish the new change id before swapping in a new wait state (a waiter that sees the new state must also see the new id)
            Interlocked.Exchange(ref C, newChangeId);
            AfterChange()?.Dispose();
        }
    }

    /// <summary>
    /// The wait state for one "generation" of a <see cref="BlockUntil"/> or <see cref="BlockUntilString"/> (one instance per change).
    /// Waiters block on <see cref="W"/>, disposing the state (done when a change happens) releases all current waiters.
    /// Instances are pooled (roughly 100 unused instances are kept in a global lock free free-list).
    /// </summary>
    internal sealed class StateBlockUntil : IDisposable
    {
        /// <summary>
        /// The wait semaphore, released once on dispose, every waiter that gets it releases it again (so that all waiters are woken)
        /// </summary>
        public readonly SemaphoreSlim W = new SemaphoreSlim(0, 1);
        /// <summary>
        /// Released when the last waiter (and the owner) have left, used to know when the instance can be reused
        /// </summary>
        public readonly SemaphoreSlim C = new SemaphoreSlim(0, 1);
        /// <summary>
        /// Number of active waiters plus one for the owner (the owner reference is removed on dispose)
        /// </summary>
        public long Count = 1;

        /// <summary>
        /// Wait until this state is disposed (a change happened), the time out expires or the token is canceled.
        /// Never throws (cancellation and time outs are swallowed), returns immediately if the state is already disposed.
        /// </summary>
        /// <param name="msToWait">Max time to wait in ms, -1 to wait forever</param>
        /// <param name="cancel">Cancels the wait</param>
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

        /// <summary>
        /// Wait until this state is disposed (a change happened) or the token is canceled.
        /// Never throws, returns immediately if the state is already disposed.
        /// </summary>
        /// <param name="cancel">Cancels the wait</param>
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


        /// <summary>
        /// Wait until this state is disposed (a change happened) or the time out expires.
        /// Never throws, returns immediately if the state is already disposed.
        /// </summary>
        /// <param name="msToWait">Max time to wait in ms, -1 to wait forever</param>
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


        /// <summary>
        /// Wait until this state is disposed (a change happened).
        /// Returns immediately if the state is already disposed.
        /// </summary>
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

        /// <summary>
        /// Release all waiters, the instance is returned to the pool (or disposed) on the thread pool once all waiters have left.
        /// Must only be called once (by the owner).
        /// </summary>
        public void Dispose()
        {
            TaskExt.StartNewAsyncChain(() => End().ConfigureAwait(false));
        }


        StateBlockUntil()
        {
        }

        /// <summary>
        /// Get a state from the pool, or allocate a new one
        /// </summary>
        /// <returns>A state with no waiters</returns>
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

        /// <summary>
        /// Total number of states allocated (process wide)
        /// </summary>
        public static long TotalAllocCount;
        /// <summary>
        /// Number of unused states in the pool (process wide)
        /// </summary>
        public static long AllocCount;
        static StateBlockUntil AllocFirst;

        StateBlockUntil Next;


    }


    /// <summary>
    /// Base class for async waiting on a change of a <see cref="long"/> change id, see <see cref="BlockUntilChange"/> and <see cref="BlockUntilValueChange"/>.
    /// </summary>
    /// <remarks>
    /// Waiters never throw, a time out or cancellation simply returns the current change id (so the caller should compare it with the id it passed in).
    /// Internally every change swaps in a new pooled wait state and releases the old one, so a change is cheap and doesn't allocate in steady state.
    /// </remarks>
    public abstract class BlockUntil : IDisposable
    {
        /// <summary>
        /// Initialize the change tracker
        /// </summary>
        /// <param name="current">The initial change id</param>
        protected BlockUntil(long current)
        {
            C = current;
            S = StateBlockUntil.Get();
        }

        /// <summary>
        /// The current change id
        /// </summary>
        public long Cc => Interlocked.Read(ref C);

        StateBlockUntil S;

        /// <summary>
        /// The current change id, derived classes must update it before calling <see cref="AfterChange"/> (so that a waiter that sees the new wait state also sees the new id)
        /// </summary>
        protected long C;

        bool IsDisposed;

        /// <summary>
        /// Any waiting tasks will continue returning the current change id, subsequent waits return immediately.
        /// </summary>
        /// <exception cref="NullReferenceException">The instance is already disposed (dispose must only be called once)</exception>
        public void Dispose()
        {
            IsDisposed = true;
            Interlocked.Exchange(ref S, null).Dispose();
        }

        /// <summary>
        /// Install a new wait state and return the previous one, dispose the returned value to wake all waiters. <see cref="C"/> must be updated before calling this.
        /// </summary>
        /// <returns>The previous wait state (null if disposed)</returns>
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
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <param name="msToWait">Number of ms to wait, -1 (<see cref="Timeout.Infinite"/>) to wait forever. When expired, the method will return with the same change id (an invalid value, less than -1, returns immediately)</param>
        /// <param name="cancel">Custom cancellation, if triggered, the method will return with the same change id (no exception is thrown)</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<long> WaitForChange(long currentChangeId, int msToWait, CancellationToken cancel)
        {
            var s = Volatile.Read(ref S);
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange(msToWait, cancel).ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }

        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <param name="cancel">Custom cancellation, if triggered, the method will return with the same change id (no exception is thrown)</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<long> WaitForChange(long currentChangeId, CancellationToken cancel)
        {
            var s = Volatile.Read(ref S);
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange(cancel).ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }


        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <param name="msToWait">Number of ms to wait, -1 (<see cref="Timeout.Infinite"/>) to wait forever. When expired, the method will return with the same change id (an invalid value, less than -1, returns immediately)</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<long> WaitForChange(long currentChangeId, int msToWait)
        {
            var s = Volatile.Read(ref S);
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange(msToWait).ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }

        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<long> WaitForChange(long currentChangeId)
        {
            var s = Volatile.Read(ref S);
            var t = Interlocked.Read(ref C);
            if ((s == null) || (t != currentChangeId))
                return t;
            await s.WaitForChange().ConfigureAwait(false);
            return Interlocked.Read(ref C);
        }

    }



    /// <summary>
    /// Provides a mechanism to async wait for a "change".
    /// Changes are tracked using a string "change id" that is supplied by the calling code (for example a hash or an etag).
    /// </summary>
    /// <remarks>Thread safe, any number of producers and waiters can use the same instance.</remarks>
    public sealed class BlockUntilStringValueChange : BlockUntilString
    {
        /// <summary>
        /// Create a new change tracker.
        /// </summary>
        /// <param name="startChangeId">The change id to start with (may be null)</param>
        public BlockUntilStringValueChange(String startChangeId = null) : base(startChangeId)
        {
        }

        /// <summary>
        /// Triggers a change, any task waiting for a change on this instance will continue and return the new change id.
        /// </summary>
        /// <param name="newChangeId">The new change id (may be null), waiters are woken even if it's equal to the current change id</param>
        public void Change(String newChangeId)
        {
            //  Publish the new change id before swapping in a new wait state (a waiter that sees the new state must also see the new id)
            Interlocked.Exchange(ref C, newChangeId);
            AfterChange()?.Dispose();
        }
    }

    /// <summary>
    /// Base class for async waiting on a change of a <see cref="String"/> change id, see <see cref="BlockUntilStringValueChange"/>.
    /// </summary>
    /// <remarks>
    /// Change ids are compared using an ordinal comparison, null is a valid change id.
    /// Waiters never throw, a time out or cancellation simply returns the current change id (so the caller should compare it with the id it passed in).
    /// </remarks>
    public abstract class BlockUntilString : IDisposable
    {
        /// <summary>
        /// Initialize the change tracker
        /// </summary>
        /// <param name="current">The initial change id (may be null)</param>
        protected BlockUntilString(String current)
        {
            C = current;
            S = StateBlockUntil.Get();
        }

        /// <summary>
        /// The current change id
        /// </summary>
        public String Cc => C;

        StateBlockUntil S;

        /// <summary>
        /// The current change id, derived classes must update it before calling <see cref="AfterChange"/> (so that a waiter that sees the new wait state also sees the new id)
        /// </summary>
        protected volatile String  C;

        bool IsDisposed;

        /// <summary>
        /// Any waiting tasks will continue returning the current change id, subsequent waits return immediately.
        /// </summary>
        /// <exception cref="NullReferenceException">The instance is already disposed (dispose must only be called once)</exception>
        public void Dispose()
        {
            IsDisposed = true;
            Interlocked.Exchange(ref S, null).Dispose();
        }

        /// <summary>
        /// Install a new wait state and return the previous one, dispose the returned value to wake all waiters. <see cref="C"/> must be updated before calling this.
        /// </summary>
        /// <returns>The previous wait state (null if disposed)</returns>
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
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <param name="msToWait">Number of ms to wait, -1 (<see cref="Timeout.Infinite"/>) to wait forever. When expired, the method will return with the same change id (an invalid value, less than -1, returns immediately)</param>
        /// <param name="cancel">Custom cancellation, if triggered, the method will return with the same change id (no exception is thrown)</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<String> WaitForChange(String currentChangeId, int msToWait, CancellationToken cancel)
        {
            var s = Volatile.Read(ref S);
            var t = C;
            if ((s == null) || (!t.FastEquals(currentChangeId)))
                return t;
            await s.WaitForChange(msToWait, cancel).ConfigureAwait(false);
            return C;
        }

        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <param name="cancel">Custom cancellation, if triggered, the method will return with the same change id (no exception is thrown)</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<String> WaitForChange(String currentChangeId, CancellationToken cancel)
        {
            var s = Volatile.Read(ref S);
            var t = C;
            if ((s == null) || (!t.FastEquals(currentChangeId)))
                return t;
            await s.WaitForChange(cancel).ConfigureAwait(false);
            return C;
        }


        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <param name="msToWait">Number of ms to wait, -1 (<see cref="Timeout.Infinite"/>) to wait forever. When expired, the method will return with the same change id (an invalid value, less than -1, returns immediately)</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<String> WaitForChange(String currentChangeId, int msToWait)
        {
            var s = Volatile.Read(ref S);
            var t = C;
            if ((s == null) || (!t.FastEquals(currentChangeId)))
                return t;
            await s.WaitForChange(msToWait).ConfigureAwait(false);
            return C;
        }

        /// <summary>
        /// Wait until a change is performed or the wait is aborted.
        /// </summary>
        /// <param name="currentChangeId">The last change id known to the caller, typically start with the initial id (0) and then update it with the result of this method. If it differs from the current change id, the current id is returned immediately</param>
        /// <returns>The new change id (if changed), or the current change id if the wait is aborted (or the instance is disposed)</returns>
        public async Task<String> WaitForChange(String currentChangeId)
        {
            var s = Volatile.Read(ref S);
            var t = C;
            if ((s == null) || (!t.FastEquals(currentChangeId)))
                return t;
            await s.WaitForChange().ConfigureAwait(false);
            return C;
        }

    }


}
