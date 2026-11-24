using System;
using SysWeaver.Data;

namespace SysWeaver.Net
{

    /// <summary>
    /// A selection of field values, see <see cref="ApiAuditQuery.Values"/>
    /// </summary>
    public sealed class ApiAuditFieldValues
    {
        /// <summary>
        /// The name of the field (a member of <see cref="ApiAuditEntry"/>)
        /// </summary>
        public String Field;

        /// <summary>
        /// The values (exact), an empty text matches an empty or missing value
        /// </summary>
        public String[] Values;

        /// <summary>
        /// True to find the calls that don't have any of the values
        /// </summary>
        public bool Exclude;
    }

    /// <summary>
    /// Selects audited API calls, all parts must match (a part that is null or empty matches everything)
    /// </summary>
    public class ApiAuditQuery
    {
        /// <summary>
        /// Only calls that began at or after this time (UTC)
        /// </summary>
        [EditAllowNull]
        public DateTime? From;

        /// <summary>
        /// Only calls that began before this time (UTC)
        /// </summary>
        [EditAllowNull]
        public DateTime? To;

        /// <summary>
        /// Search terms separated by white space, all must match (case insensitive, as a part of any of the text fields).
        /// - "some text" is a term with white space.
        /// - -term only finds calls that don't match the term.
        /// - field:term only looks in one field, ex: user:anna, api:login, ip:10.0., input:"some text", state:failed.
        /// </summary>
        [EditAllowNull]
        public String Text;

        /// <summary>
        /// Selected field values, all must match
        /// </summary>
        [EditAllowNull]
        public ApiAuditFieldValues[] Values;

        /// <summary>
        /// Conditions on any field of <see cref="ApiAuditEntry"/>, all must match.
        /// </summary>
        [EditAllowNull]
        public TableDataFilter[] All;

        /// <summary>
        /// Conditions on any field of <see cref="ApiAuditEntry"/>, at least one must match.
        /// </summary>
        [EditAllowNull]
        public TableDataFilter[] Any;

        /// <summary>
        /// Copy all values from another query (shallow copy)
        /// </summary>
        /// <param name="src">The query to copy from, null is the same as an empty query</param>
        public void CopyFrom(ApiAuditQuery src)
        {
            From = src?.From;
            To = src?.To;
            Text = src?.Text;
            Values = src?.Values;
            All = src?.All;
            Any = src?.Any;
        }
    }

    /// <summary>
    /// Find audited API calls
    /// </summary>
    public sealed class ApiAuditSearch : ApiAuditQuery
    {
        /// <summary>
        /// Field names, the first is the primary order, prefix with a '-' for descending order, null or empty is the same as ["-Id"] (the newest call first).
        /// </summary>
        [EditAllowNull]
        public String[] Order;

        /// <summary>
        /// The first entry to return
        /// </summary>
        [EditMin(0)]
        public long Row;

        /// <summary>
        /// Maximum number of entries to return (at most <see cref="ApiAuditTools.MaxEntries"/>), 0 = 100
        /// </summary>
        [EditMin(0)]
        public long MaxRowCount;

        /// <summary>
        /// Number of entries after the returned that should be counted (see <see cref="TableDataOrderRequest.LookAheadCount"/>)
        /// </summary>
        [EditMin(0)]
        public long LookAheadCount;
    }

    /// <summary>
    /// Get the most common values of some fields
    /// </summary>
    public sealed class ApiAuditFacetRequest : ApiAuditQuery
    {
        /// <summary>
        /// The names of the fields to get values for.
        /// The selected values (<see cref="ApiAuditQuery.Values"/>) of a field aren't used for that field, so that other values can be selected too.
        /// </summary>
        public String[] Fields;

        /// <summary>
        /// Maximum number of values to return for each field (at most 1000)
        /// </summary>
        [EditRange(1, 1000)]
        public int MaxValues = 10;
    }

    /// <summary>
    /// Get totals and a time line
    /// </summary>
    public sealed class ApiAuditSummaryRequest : ApiAuditQuery
    {
        /// <summary>
        /// The wanted number of time line steps, the step duration is rounded to something nice (at most 500)
        /// </summary>
        [EditRange(1, 500)]
        public int Steps = 60;
    }

}
