using System;

namespace SysWeaver.AI
{
    public sealed partial class KeyValueStorageAiMemory
    {
        public sealed class MemEntry
        {
            public MemEntry()
            {
            }
            public AiMemoryEntry H;
            public String V;

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
