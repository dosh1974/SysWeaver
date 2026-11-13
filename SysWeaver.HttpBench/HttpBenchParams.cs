using System;
using SysWeaver.MicroService;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// The parameters of the <see cref="HttpBenchService"/>.
    /// </summary>
    public sealed class HttpBenchParams
    {
        /// <summary>
        /// The roles that may run tests and read the results (comma separated tokens).
        /// null = anyone, also without signing in (a developer machine only: the service sends a lot of requests to any url it is given).
        /// "" = any signed in user.
        /// </summary>
        public String Auth = Roles.Dev;

        /// <summary>
        /// The maximum number of workers (concurrent requests) of a test.
        /// </summary>
        public int MaxConcurrency = 1024;

        /// <summary>
        /// The shortest test, in seconds.
        /// </summary>
        public int MinDurationSeconds = 10;

        /// <summary>
        /// The longest test, in seconds (default 120 minutes).
        /// </summary>
        public int MaxDurationSeconds = 120 * 60;

        /// <summary>
        /// The number of finished tests whose results are kept (in memory, the oldest is removed first).
        /// </summary>
        public int KeepRuns = 10;

        /// <summary>
        /// The maximum number of single requests that are kept for the request details of a test (a uniform random sample of all requests when there are more).
        /// </summary>
        public int MaxRequestSamples = 100_000;

        /// <summary>
        /// The time out of one request, in seconds (a request that takes longer is counted as an error).
        /// </summary>
        public int RequestTimeoutSeconds = 30;

        /// <summary>
        /// The time that requests that were started before the end of a test may take to complete, in seconds (they are cancelled after that and are not counted).
        /// </summary>
        public int DrainSeconds = 5;

        /// <summary>
        /// The limits and timing of the tests (for <see cref="BenchSetup"/> and <see cref="BenchRun"/>).
        /// </summary>
        internal BenchLimits ToLimits() => new BenchLimits
        {
            MaxConcurrency = Math.Max(1, MaxConcurrency),
            MinDurationSeconds = Math.Max(1, MinDurationSeconds),
            MaxDurationSeconds = Math.Max(Math.Max(1, MinDurationSeconds), MaxDurationSeconds),
            RequestTimeoutSeconds = RequestTimeoutSeconds,
            DrainSeconds = DrainSeconds,
            MaxRequestSamples = MaxRequestSamples,
        };
    }
}
