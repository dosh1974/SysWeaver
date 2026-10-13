using System;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Remote;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Interface used for services that can send text messages to a phone number
    /// </summary>
    public interface ITextMessageSender : IDisposable
    {
        /// <summary>
        /// Send a text message.
        /// Delivery failures that are known when sending (not on the channel, invalid number etc) are returned as a result with the status <see cref="TextMessageStatuses.Failed"/>,
        /// other errors (invalid parameters, configuration or connection problems etc) throws.
        /// Most providers report delivery asynchronously, see <see cref="ITextMessageStatusService"/> and <see cref="ITextMessageEvents"/>.
        /// </summary>
        /// <param name="msg">The message details</param>
        /// <returns>The result</returns>
        [RemoteEndPoint(HttpEndPointTypes.Post)]
        Task<TextMessageSendResult> Send(OutgoingTextMessage msg);
    }
}
