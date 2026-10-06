using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// Interface for audit services.
    /// Registered services are automatically attached to the API module's audit events and receive calls for API methods marked with
    /// <see cref="SysWeaver.MicroService.WebApiAuditAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The callbacks are invoked synchronously on the request path; implementations should be fast, thread safe and should not throw
    /// (an exception from <see cref="OnApiBegin"/> or <see cref="OnApiEnd"/> fails the API call).
    /// </remarks>
    public interface IApiAuditService
    {
        /// <summary>
        /// Method invoked before an audited API is invoked
        /// </summary>
        /// <param name="id">A unique invoke id</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="value">The input value (after any audit params filter), can be null for API's without arguments. Must not be modified.</param>
        void OnApiBegin(long id, HttpServerRequest r, IHttpApiAudit api, Object value);

        /// <summary>
        /// Method invoked after an audited API is invoked (if no exception in thrown)
        /// </summary>
        /// <param name="id">A unique invoke id (same as for the begin)</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="value">The output value (after any audit return filter), can be null for void API's. Must not be modified.</param>
        void OnApiEnd(long id, HttpServerRequest r, IHttpApiAudit api, Object value);

        /// <summary>
        /// Method invoked if an audited API throws an exception
        /// </summary>
        /// <param name="id">A unique invoke id (same as for the begin)</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="ex">The exception object thrown</param>
        void OnApiException(long id, HttpServerRequest r, IHttpApiAudit api, Exception ex);

        /// <summary>
        /// Flush pending audit data.
        /// </summary>
        /// <returns>A task that completes when pending data has been written.</returns>
        Task Flush();

    }



    /// <summary>
    /// Utilities for adding audits.
    /// </summary>
    public static class ApiAudit
    {

        static long Trackid = (DateTime.UtcNow - new DateTime(2024, 1, 1)).Ticks;

        /// <summary>
        /// Get a new audit id (thread safe).
        /// </summary>
        /// <returns>An id that is unique within the process; ids are seeded from the time since 2024-01-01 at startup so they are very likely unique across restarts too.</returns>
        public static long GetId()
            => Interlocked.Increment(ref Trackid);
        

    }
}
