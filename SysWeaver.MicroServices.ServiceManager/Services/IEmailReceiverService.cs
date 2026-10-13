using System;
using System.Threading.Tasks;
using SysWeaver.Remote;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Interface for services that can receive emails
    /// </summary>
    public interface IEmailReceiverService : IDisposable
    {
        /// <summary>
        /// Poll for any new emails, should block until new emails are available (or a time out).
        /// Emails are returned oldest first, use the <see cref="IncomingEmail.Id"/> of the last email as <see cref="GetIncomingEmailRequest.LastId"/> in the next call.
        /// If the last id is null or unknown (ex: the service restarted), only recent emails are returned.
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new emails or null if no new emails was recieved</returns>
        [RemoteEndPoint(HttpEndPointTypes.Post)]
        Task<IncomingEmail[]> GetIncoming(GetIncomingEmailRequest request);
    }
}
