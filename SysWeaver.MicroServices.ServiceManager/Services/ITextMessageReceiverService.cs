using System;
using System.Threading.Tasks;
using SysWeaver.Remote;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Interface for services that can receive text messages
    /// </summary>
    public interface ITextMessageReceiverService : IDisposable
    {
        /// <summary>
        /// Poll for any new text messages, should block until new messages are available (or a time out).
        /// Messages are returned oldest first, use the <see cref="IncomingTextMessage.Id"/> of the last message as <see cref="GetIncomingTextMessageRequest.LastId"/> in the next call.
        /// If the last id is null or unknown (ex: the service restarted), only recent messages are returned.
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new messages or null if no new messages was recieved</returns>
        [RemoteEndPoint(HttpEndPointTypes.Post)]
        Task<IncomingTextMessage[]> GetIncoming(GetIncomingTextMessageRequest request);
    }
}
