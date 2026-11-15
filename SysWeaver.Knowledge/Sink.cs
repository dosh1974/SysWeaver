namespace SysWeaver.Knowledge
{
    /// <summary>
    /// A sink for values that must not be optimized away
    /// </summary>
    public static class Sink
    {
        /// <summary>
        /// The sink, write values here to keep them alive
        /// </summary>
        public static long Data;
    }


}
