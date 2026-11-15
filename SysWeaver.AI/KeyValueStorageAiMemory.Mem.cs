using System;
using System.Collections.Generic;

namespace SysWeaver.AI
{
    public sealed partial class KeyValueStorageAiMemory
    {
        /// <summary>
        /// The memories of a user
        /// </summary>
        public sealed class Mem
        {
            /// <summary>
            /// Lock that must be held when accessing <see cref="M"/>
            /// </summary>
            public readonly AsyncLock Lock = new AsyncLock();

            /// <summary>
            /// The memories by key, null until loaded
            /// </summary>
            public volatile Dictionary<String, MemEntry> M;
        }
    }

}
