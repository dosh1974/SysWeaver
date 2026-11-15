namespace SysWeaver.IpLocation
{
    /// <summary>
    /// Parameters for the <see cref="IpLocationService"/>
    /// </summary>
    public sealed class IpLocationParams
    {
        /// <summary>
        /// The number of minutes to cache locations in memory, used if no ip location cache is defined.
        /// Defaults to 1440 (24 hours).
        /// </summary>
        public int MaxCachedMinutes = 60 * 24;

        /// <summary>
        /// The maximum number of rounds (every source is tried once per round, a source that timed out is not tried again for that ip), at least 1.
        /// Defaults to 10.
        /// </summary>
        public int MaxRetryCount = 10;

        /// <summary>
        /// The wait in milliseconds between the first and second round (the wait grows with each round up to <see cref="MaxWaitMs"/>), at least 10.
        /// Defaults to 500.
        /// </summary>
        public int MinWaitMs = 500;

        /// <summary>
        /// The maximum wait in milliseconds between two rounds (at least 5 times <see cref="MinWaitMs"/>).
        /// Defaults to 3000.
        /// </summary>
        public int MaxWaitMs = 3000;

    }
}
