using Newtonsoft.Json;
using System;

namespace SysWeaver.Serialization
{
    sealed class PooledJsonSerializer : JsonSerializer, IDisposable
    {
        public PooledJsonSerializer(Action<PooledJsonSerializer> onDispose)
        {
            OnDispose = onDispose;
        }

        public void Dispose()
            => OnDispose(this);

        readonly Action<PooledJsonSerializer> OnDispose;
    }


}
