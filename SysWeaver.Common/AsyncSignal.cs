using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// An object that let tasks wait async for a signal (that is manually triggered elsewhere).
    /// The signal can only be raised once (it can't be reset).
    /// </summary>
    /// <remarks>
    /// This is a struct that wraps a reference to the shared signal, copies of an instance shares the same signal.
    /// Always create instances using the constructor (new AsyncSignal()), a default(AsyncSignal) have no signal and all members will throw a <see cref="System.NullReferenceException"/>.
    /// Continuations of the waiting tasks are always run asynchronously (never inline on the thread that calls <see cref="Raise"/>).
    /// </remarks>
    public struct AsyncSignal
    {
        /// <summary>
        /// Create a new signal (that is not raised)
        /// </summary>
        public AsyncSignal()
        {
        }

        readonly TaskCompletionSource S = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// True if the async signal have been raised
        /// </summary>
        public bool IsRaised => S.Task.IsCompleted;

        /// <summary>
        /// Get a task that wait's until the signal is raised
        /// </summary>
        /// <returns>A task that completes when the signal is raised (the same task instance is returned on every call)</returns>
        public Task Wait() => S.Task;

        /// <summary>
        /// Raise the signal (allow waiter's to continue)
        /// </summary>
        /// <returns>True if the signal was raised by this call, false if it was already raised</returns>
        public bool Raise() => S.TrySetResult();

    }

}
