using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// In-process notifications from a text message provider (push, instead of polling).
    /// Not available for remote instances (remote APIs can only have methods), use <see cref="ITextMessageReceiverService"/> and <see cref="ITextMessageStatusService"/> for those.
    /// Handlers are invoked synchronously by the provider, so they should be fast and must not throw.
    /// </summary>
    public interface ITextMessageEvents
    {
        /// <summary>
        /// Invoked when a message is received
        /// </summary>
        event Action<IncomingTextMessage> OnIncoming;

        /// <summary>
        /// Invoked when the status of a sent message changes
        /// </summary>
        event Action<TextMessageStatusUpdate> OnStatus;
    }
}
