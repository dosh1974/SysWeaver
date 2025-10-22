using System;
using System.Threading;

namespace SysWeaver.Net
{

    /// <summary>
    /// Interface for audit services
    /// </summary>
    public interface IApiAuditService
    {
        /// <summary>
        /// Method invoked before an audited API is invoked
        /// </summary>
        /// <param name="id">A unique invoke id</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="value">The input value (can be used to inspect data), can be null for void API's</param>
        void OnApiBegin(long id, HttpServerRequest r, IHttpApiAudit api, Object value);

        /// <summary>
        /// Method invoked after an audited API is invoked (if no exception in thrown)
        /// </summary>
        /// <param name="id">A unique invoke id (same as for the begin)</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="value">The output value (can be used to inspect data), can be null for void API's</param>
        void OnApiEnd(long id, HttpServerRequest r, IHttpApiAudit api, Object value);

        /// <summary>
        /// Method invoked if an audited API throws an exception
        /// </summary>
        /// <param name="id">A unique invoke id (same as for the begin)</param>
        /// <param name="r">The server request (used to get session data, such as agent etc)</param>
        /// <param name="api">The api that is being invoked</param>
        /// <param name="ex">The exception object thrown</param>
        void OnApiException(long id, HttpServerRequest r, IHttpApiAudit api, Exception ex);


    }



    /// <summary>
    /// Utilities for adding audits
    /// </summary>
    public static class ApiAudit
    {

        static long Trackid = (DateTime.UtcNow - new DateTime(2024, 1, 1)).Ticks;

        /// <summary>
        /// Use this to get an audit ID
        /// </summary>
        /// <returns></returns>
        public static long GetId()
            => Interlocked.Increment(ref Trackid);
        

    }
}
