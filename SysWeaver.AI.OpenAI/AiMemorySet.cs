using System;

namespace SysWeaver.AI
{
    public class AiMemorySet
    {
        /// <summary>
        /// The unqiue key used to manage this memory.
        /// This is listed in the system prompt.
        /// Max length is 32.
        /// </summary>
        public String Key;
        /// <summary>
        /// The value of this memory, typically MD text.
        /// Max length is 4096.
        /// </summary>
        public String Value;
    }

}
