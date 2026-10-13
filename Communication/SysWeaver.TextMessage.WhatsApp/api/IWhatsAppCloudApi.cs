using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.TextMessage.WhatsAppApi
{
    /// <summary>
    /// The WhatsApp Cloud API (Meta Graph API) end points for a single business phone number.
    /// The base url must be the phone number node, ex: "https://graph.facebook.com/v24.0/{phone-number-id}/".
    /// Authentication is a bearer token (a system user access token with the whatsapp_business_messaging permission).
    /// See: https://developers.facebook.com/docs/whatsapp/cloud-api/reference/messages
    /// </summary>
    [RemoteSerializer(RemoteParam.JsonSerializer)]
    public interface IWhatsAppCloudApi : IRemoteApi
    {
        /// <summary>
        /// Send a message (text, template etc).
        /// Success only means that the message was accepted, delivery status is reported asynchronously using the "statuses" webhook.
        /// </summary>
        /// <param name="request">The message</param>
        /// <returns>The id (wamid) of the message</returns>
        [RemoteEndPoint(HttpEndPointTypes.Post, "messages")]
        Task<WaSendResponse> Send(WaSendRequest request);

        /// <summary>
        /// Mark an incoming message as read (displays the blue check marks to the user).
        /// </summary>
        /// <param name="request">The message to mark as read</param>
        /// <returns></returns>
        [RemoteEndPoint(HttpEndPointTypes.Post, "messages")]
        Task<WaSuccessResponse> MarkRead(WaReadRequest request);
    }
}
