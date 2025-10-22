namespace SysWeaver.IpLocation
{
    public sealed class IpLocationParams
    {
        /// <summary>
        /// Used if no ip location cache is defined
        /// </summary>
        public int MaxCachedMinutes = 60 * 24;

        /// <summary>
        /// Maximum number of retries
        /// </summary>
        public int MaxRetryCount = 10;

        /// <summary>
        /// Minimum wait
        /// </summary>
        public int MinWaitMs = 500;

        /// <summary>
        /// Maximum wait
        /// </summary>
        public int MaxWaitMs = 3000;

    }
}
