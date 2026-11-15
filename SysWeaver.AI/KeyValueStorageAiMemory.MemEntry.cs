using System;

namespace SysWeaver.AI
{
    public sealed partial class KeyValueStorageAiMemory
    {
        /// <summary>
        /// A stored memory
        /// </summary>
        public sealed class MemEntry
        {
            /// <summary>
            /// Create an empty entry (for deserialization)
            /// </summary>
            public MemEntry()
            {
            }

            /// <summary>
            /// The header (key, last access and description)
            /// </summary>
            public AiMemoryEntry H;

            /// <summary>
            /// The value
            /// </summary>
            public String V;

            /// <summary>
            /// Create an entry, last access is set to now
            /// </summary>
            /// <param name="key">The unique key</param>
            /// <param name="desc">A short description</param>
            /// <param name="value">The value</param>
            public MemEntry(String key, String desc, String value)
            {
                H = new AiMemoryEntry
                {
                    Key = key,
                    Desc = desc,
                    Last = DateTime.UtcNow.Ticks,
                };
                V = value;
            }

        }
    }

}
