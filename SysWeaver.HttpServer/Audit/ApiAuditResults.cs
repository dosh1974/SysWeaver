using System;

namespace SysWeaver.Net
{

    /// <summary>
    /// Information about an audit storage
    /// </summary>
    public sealed class ApiAuditInfo
    {
        /// <summary>
        /// Name of the storage
        /// </summary>
        public String Storage;

        /// <summary>
        /// Number of days that entries are kept, 0 = forever
        /// </summary>
        public int RetentionDays;

        /// <summary>
        /// Maximum number of chars stored of the input and output
        /// </summary>
        public int MaxDataLength;

        /// <summary>
        /// The resolution of the stored times, number of milliseconds (a duration is only meaningful if it's much longer than this)
        /// </summary>
        public int TimeResolution = 1;

        /// <summary>
        /// True if the service manager has an ip location service (<see cref="IIpLocation"/>), else <see cref="IApiAuditReportService.GetIpLocation(string)"/> always returns null
        /// </summary>
        public bool HaveIpLocation;
    }

    /// <summary>
    /// Some found API calls
    /// </summary>
    public sealed class ApiAuditPage
    {
        /// <summary>
        /// The found entries
        /// </summary>
        public ApiAuditEntry[] Entries;

        /// <summary>
        /// The first entry + the number of returned entries + the number of available look ahead entries (same as the row count of table data)
        /// </summary>
        public long RowCount;
    }

    /// <summary>
    /// A value of a field
    /// </summary>
    public sealed class ApiAuditFacetValue
    {
        /// <summary>
        /// The value, an empty text if it's missing
        /// </summary>
        public String Value;

        /// <summary>
        /// Number of calls with this value
        /// </summary>
        public long Count;

        /// <summary>
        /// Number of calls with this value that failed
        /// </summary>
        public long Failed;
    }

    /// <summary>
    /// The most common values of a field
    /// </summary>
    public sealed class ApiAuditFacet
    {
        /// <summary>
        /// The name of the field
        /// </summary>
        public String Field;

        /// <summary>
        /// The values, most common first
        /// </summary>
        public ApiAuditFacetValue[] Values;

        /// <summary>
        /// True if there are more values than returned
        /// </summary>
        public bool More;
    }

    /// <summary>
    /// One step of a time line
    /// </summary>
    public sealed class ApiAuditStep
    {
        /// <summary>
        /// When the step begins (UTC)
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Number of calls that completed
        /// </summary>
        public long Completed;

        /// <summary>
        /// Number of calls that failed
        /// </summary>
        public long Failed;

        /// <summary>
        /// Number of calls in progress
        /// </summary>
        public long InProgress;
    }

    /// <summary>
    /// Totals and a time line
    /// </summary>
    public sealed class ApiAuditSummary
    {
        /// <summary>
        /// Number of calls that completed
        /// </summary>
        public long Completed;

        /// <summary>
        /// Number of calls that failed
        /// </summary>
        public long Failed;

        /// <summary>
        /// Number of calls in progress
        /// </summary>
        public long InProgress;

        /// <summary>
        /// Number of unique users (calls without any user are not counted)
        /// </summary>
        public long Users;

        /// <summary>
        /// Number of unique clients
        /// </summary>
        public long Clients;

        /// <summary>
        /// Average number of milliseconds of the calls that ended, null if there are none
        /// </summary>
        public double? AverageDuration;

        /// <summary>
        /// Number of milliseconds of the slowest call that ended, null if there are none
        /// </summary>
        public double? MaxDuration;

        /// <summary>
        /// When the first call began (UTC), null if there are no calls
        /// </summary>
        public DateTime? First;

        /// <summary>
        /// When the last call began (UTC), null if there are no calls
        /// </summary>
        public DateTime? Last;

        /// <summary>
        /// Number of seconds between steps
        /// </summary>
        public long StepSeconds;

        /// <summary>
        /// The time line, without any gaps, empty if there are no calls
        /// </summary>
        public ApiAuditStep[] Steps;
    }

}
