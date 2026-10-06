using Newtonsoft.Json;
using System;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// A Newtonsoft <see cref="JsonSerializer"/> that returns itself to its pool when disposed (instead of being disposed).
    /// </summary>
    sealed class PooledJsonSerializer : JsonSerializer, IDisposable
    {
        /// <summary>
        /// Create a pooled serializer.
        /// </summary>
        /// <param name="onDispose">Called with this instance when it is disposed (returns it to the pool).</param>
        public PooledJsonSerializer(Action<PooledJsonSerializer> onDispose)
        {
            OnDispose = onDispose;
        }

        /// <summary>
        /// Return this serializer to its pool, it must not be used after this call.
        /// </summary>
        public void Dispose()
            => OnDispose(this);

        readonly Action<PooledJsonSerializer> OnDispose;
    }


}
