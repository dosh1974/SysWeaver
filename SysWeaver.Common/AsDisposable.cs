using System;

namespace SysWeaver
{
    public readonly struct AsDisposable : IDisposable
    {
        public AsDisposable(Action onDispose)
        {
            A = onDispose;
        }

        readonly Action A;

        public void Dispose() => A?.Invoke();
    }

}
