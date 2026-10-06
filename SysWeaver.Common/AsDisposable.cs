using System;

namespace SysWeaver
{
    /// <summary>
    /// A lightweight <see cref="IDisposable"/> that invokes an action when disposed, useful for "using" scopes (ex: undo some state change).
    /// </summary>
    /// <remarks>
    /// This is a struct and does not track whether it has been disposed: every call to <see cref="Dispose"/> (including calls on copies) invokes the action again.
    /// Boxing occurs when it's returned as <see cref="IDisposable"/>.
    /// </remarks>
    public readonly struct AsDisposable : IDisposable
    {
        /// <summary>
        /// Create a disposable that invokes <paramref name="onDispose"/> when disposed.
        /// </summary>
        /// <param name="onDispose">The action to invoke on dispose, may be null (dispose is then a no-op).</param>
        public AsDisposable(Action onDispose)
        {
            A = onDispose;
        }

        readonly Action A;

        /// <summary>
        /// Invoke the action supplied to the constructor (if any).
        /// </summary>
        public void Dispose() => A?.Invoke();
    }

}
