using System;
using SysWeaver.Data;

namespace SysWeaver.Net
{

    /// <summary>
    /// The state of an audited API call
    /// </summary>
    public enum ApiAuditStates : byte
    {
        /// <summary>
        /// The call hasn't completed (or the process was terminated before it did)
        /// </summary>
        InProgress = 0,
        /// <summary>
        /// The call completed without any exception
        /// </summary>
        Completed = 1,
        /// <summary>
        /// The call threw an exception (the output is the message)
        /// </summary>
        Failed = 2,
    }

    /// <summary>
    /// A "fat" audit entry: one audited API call and the client that made it
    /// </summary>
    [TableDataPrimaryKey(nameof(Id))]
    [TableDataDefaultOrder("-" + nameof(Id))]
    public sealed class ApiAuditEntry
    {
        /// <summary>
        /// Unique id, one for each API call (a later call has a higher id)
        /// </summary>
        [TableDataHide]
        public long Id;

        /// <summary>
        /// Time when the execution began
        /// </summary>
        public DateTime Begin;

        /// <summary>
        /// The state of the call
        /// </summary>
        public ApiAuditStates State;

        /// <summary>
        /// Name of the API
        /// </summary>
        public String Api;

        /// <summary>
        /// The audit group of the API
        /// </summary>
        public String Group;

        /// <summary>
        /// The name of the user that performed this action (can be null if the API was called without any user logged in)
        /// </summary>
        public String UserName;

        /// <summary>
        /// Number of milliseconds that the call took, null if it hasn't completed
        /// </summary>
        [TableDataNumber(1, "{0} ms")]
        public double? Duration;

        /// <summary>
        /// The (capped) input data as json, null if the API doesn't have any input
        /// </summary>
        [TableDataJson(48)]
        public String Input;

        /// <summary>
        /// The (capped) output data as json, null if the API doesn't have any output, the exception message (typically plain text) if the call failed
        /// </summary>
        [TableDataJson(48)]
        public String Output;

        /// <summary>
        /// Ip address of the client
        /// </summary>
        [TableDataIp]
        public String Ip;

        /// <summary>
        /// User agent string of the client
        /// </summary>
        [TableDataUserAgent]
        public String UserAgent;

        /// <summary>
        /// Language code of the client
        /// </summary>
        [TableDataHide]
        [TableDataIsoLanguage]
        public String Language;

        /// <summary>
        /// Time zone used by the client
        /// </summary>
        [TableDataHide]
        public String TimeZone;

        /// <summary>
        /// The device id of the client
        /// </summary>
        [TableDataHide]
        public String DeviceId;

        /// <summary>
        /// The session that made the call (redacted, see <see cref="ApiAuditTools.RedactSession(string)"/>)
        /// </summary>
        [TableDataHide]
        public String Session;

        /// <summary>
        /// The id of the client (all calls with the same ip address, device, user agent, language and time zone have the same client id), 0 if unknown
        /// </summary>
        [TableDataHide]
        public long ClientId;

        /// <summary>
        /// The guid of the user that performed this action (can be null if the API was called without any user logged in)
        /// </summary>
        [TableDataHide]
        public String UserGuid;

        /// <summary>
        /// Time when the execution completed, null if it hasn't completed
        /// </summary>
        [TableDataHide]
        public DateTime? End;

    }

}
