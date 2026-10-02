using System;
using System.Collections.Generic;

namespace SysWeaver.AI
{
    public sealed partial class KeyValueStorageAiMemory
    {
        public sealed class Mem
        {
            public readonly AsyncLock Lock = new AsyncLock();
            public volatile Dictionary<String, MemEntry> M;
        }
    }

}
