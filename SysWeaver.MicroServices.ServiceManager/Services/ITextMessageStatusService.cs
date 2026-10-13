using System;
using System.Threading.Tasks;
using SysWeaver.Remote;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Interface for services that can report the delivery status of sent text messages
    /// </summary>
    public interface ITextMessageStatusService : IDisposable
    {
        /// <summary>
        /// Poll for any new status updates, should block until new updates are available (or a time out).
        /// Updates are returned oldest first, use the <see cref="TextMessageStatusUpdate.Id"/> of the last update as <see cref="GetTextMessageStatusRequest.LastId"/> in the next call.
        /// If the last id is null or unknown (ex: the service restarted), only recent updates are returned.
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new updates or null if no new updates was recieved</returns>
        [RemoteEndPoint(HttpEndPointTypes.Post)]
        Task<TextMessageStatusUpdate[]> GetStatusUpdates(GetTextMessageStatusRequest request);
    }
}
