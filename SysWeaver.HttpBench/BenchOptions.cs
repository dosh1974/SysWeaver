using System;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// A choice of a list (compression options).
    /// </summary>
    public sealed class BenchChoice
    {
        /// <summary>
        /// The value that is sent in the settings ("" = none).
        /// </summary>
        public String Value;

        /// <summary>
        /// The display text.
        /// </summary>
        public String Text;

        /// <summary>
        /// A description (tool tip).
        /// </summary>
        public String Title;
    }

    /// <summary>
    /// What the page needs to build the form.
    /// </summary>
    public sealed class BenchOptions
    {
        /// <summary>
        /// The modules and their end points.
        /// </summary>
        public BenchModule[] Modules;

        /// <summary>
        /// The POST body compressions.
        /// </summary>
        public BenchChoice[] RequestCompressions;

        /// <summary>
        /// The response compressions (Accept-Encoding).
        /// </summary>
        public BenchChoice[] ResponseCompressions;

        /// <summary>
        /// The shortest test, in seconds.
        /// </summary>
        public int MinDurationSeconds;

        /// <summary>
        /// The longest test, in seconds.
        /// </summary>
        public int MaxDurationSeconds;

        /// <summary>
        /// The most workers (concurrent requests) of a test.
        /// </summary>
        public int MaxConcurrency;

        /// <summary>
        /// The id of the running test, 0 if none.
        /// </summary>
        public int Running;

        /// <summary>
        /// The id of the latest test, 0 if none.
        /// </summary>
        public int Latest;

        /// <summary>
        /// The settings of the latest test (to fill in the form), null if none.
        /// </summary>
        public BenchStartRequest LatestSettings;

        /// <summary>
        /// The value of the "DataType" member of an exported parameter file (see <see cref="BenchSettingsFile"/>), the page checks it on import.
        /// </summary>
        public String SettingsDataType;
    }
}
