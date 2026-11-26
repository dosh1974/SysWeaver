using System;
using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver.Net
{

    /// <summary>
    /// Interface for services that can search and report the API calls stored by an <see cref="IApiAuditService"/> (typically the same service implements both).
    /// All reports work on "fat" entries (<see cref="ApiAuditEntry"/>, the call and its client in one row), so any report can be filtered on both.
    /// The web page audit/audit.html (the audit browser) uses the methods below as web API's, so an implementation must:
    /// - Have a [WebApiUrl("Audit")] attribute on the class (the page calls Api/Audit/[Method]).
    /// - Have a [WebApi] and a [WebApiAuth] (with <see cref="ApiAuditTools.ReadAuth"/>) attribute on every method.
    ///   Note that a [WebApiAuth] attribute on an interface method isn't used, a missing attribute on the class makes the audit data public!
    /// </summary>
    /// <remarks>
    /// Implementations should use an <see cref="ApiAuditMatcher"/> for the interpretation of a query, so that all services give the same result.
    /// </remarks>
    public interface IApiAuditReportService
    {
        /// <summary>
        /// Get information about the audit storage
        /// </summary>
        /// <returns>Information about the audit storage</returns>
        Task<ApiAuditInfo> GetInfo();

        /// <summary>
        /// Find audited API calls
        /// </summary>
        /// <param name="search">What to find, the order and the page to return, null returns the newest calls</param>
        /// <returns>The found API calls (with client data)</returns>
        Task<ApiAuditPage> Search(ApiAuditSearch search);

        /// <summary>
        /// Get a single audited API call
        /// </summary>
        /// <param name="id">The id of the call</param>
        /// <returns>The API call (with client data), null if it doesn't exist (or if it's pruned)</returns>
        Task<ApiAuditEntry> GetEntry(long id);

        /// <summary>
        /// Get the estimated location of an ip address, using the ip location source of the service manager (if any).
        /// Implement it with <see cref="ApiAuditTools.GetIpLocation(IIpLocation, string)"/>.
        /// </summary>
        /// <param name="ip">The ip address</param>
        /// <returns>The location, null if there is no source, or if it isn't known</returns>
        Task<Location> GetIpLocation(String ip);

        /// <summary>
        /// Get the most common values of some fields (with the number of calls), among the calls that match a query
        /// </summary>
        /// <param name="request">The query and the fields to get values for</param>
        /// <returns>The values of every (known) requested field</returns>
        Task<ApiAuditFacet[]> GetFacets(ApiAuditFacetRequest request);

        /// <summary>
        /// Get totals and a time line of the calls that match a query
        /// </summary>
        /// <param name="request">The query and the number of time line steps</param>
        /// <returns>Totals and a time line</returns>
        Task<ApiAuditSummary> GetSummary(ApiAuditSummaryRequest request);

        /// <summary>
        /// Audited API calls (with client data) as a table.
        /// </summary>
        /// <param name="r">The table request, the param is an optional json serialized <see cref="ApiAuditQuery"/> (it's combined with the filters and the search text of the request)</param>
        /// <returns>The table</returns>
        Task<TableData> AuditTable(TableDataRequest r);
    }

}
