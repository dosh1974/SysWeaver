namespace SysWeaver.AI
{
    /// <summary>
    /// Size limits of the AI memory
    /// </summary>
    public static class AiMemoryLimits
    {
        /// <summary>
        /// The maximum number of memories per user
        /// </summary>
        public const int MaxRecords = 64;

        /// <summary>
        /// The maximum length of a key
        /// </summary>
        public const int MaxKeyLen = 32;

        /// <summary>
        /// The maximum length of a description
        /// </summary>
        public const int MaxDescLen = 128;

        /// <summary>
        /// The maximum length of a value
        /// </summary>
        public const int MaxContentLen = 4096;
    }

}
