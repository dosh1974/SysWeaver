using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// An object that let tasks wait async for a signal (that is manually triggered elsewhere).
    /// </summary>
    public struct AsyncSignal
    {
        public AsyncSignal()
        {
        }

        readonly TaskCompletionSource S = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Get a task that wait's until the signal is raised
        /// </summary>
        /// <returns></returns>
        public Task Wait() => S.Task;

        /// <summary>
        /// Raise the signal (allow waiter's to continue)
        /// </summary>
        /// <returns></returns>
        public bool Raise() => S.TrySetResult();
    
    } 

}
