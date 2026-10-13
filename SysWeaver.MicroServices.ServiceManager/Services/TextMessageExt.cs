using System;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{
    public static class TextMessageExt
    {
        /// <summary>
        /// Send a text message
        /// </summary>
        /// <param name="sender">Service instance</param>
        /// <param name="phoneNumber">The phone number to send it to, should be country code prefixed using +</param>
        /// <param name="text">The message text</param>
        /// <param name="system">An optional system</param>
        /// <param name="purpose">The purpose of the message</param>
        /// <returns>The result</returns>
        public static Task<TextMessageSendResult> Send(this ITextMessageSender sender, String phoneNumber, String text, String system = null, TextMessagePurpose purpose = TextMessagePurpose.Transactional)
            => sender.Send(new OutgoingTextMessage { PhoneNumber = phoneNumber, Text = text, System = system, Purpose = purpose });

        /// <summary>
        /// Send a reply to a received message, using the same provider, channel and number (if possible)
        /// </summary>
        /// <param name="sender">Service instance (must be the same provider or router that received the message)</param>
        /// <param name="to">The received message</param>
        /// <param name="text">The message text</param>
        /// <returns>The result</returns>
        public static Task<TextMessageSendResult> Reply(this ITextMessageSender sender, IncomingTextMessage to, String text)
            => sender.Send(new OutgoingTextMessage { PhoneNumber = to.PhoneNumber, Text = text, System = to.System, ResponseToken = to.ResponseToken });

        /// <summary>
        /// Create a failed result
        /// </summary>
        /// <param name="msg">The message</param>
        /// <param name="channel">The channel</param>
        /// <param name="failure">The reason</param>
        /// <param name="error">A human readable error message</param>
        /// <param name="errorCode">An optional provider specific error code</param>
        /// <returns>The result</returns>
        public static TextMessageSendResult Failed(this OutgoingTextMessage msg, TextMessageChannels channel, TextMessageFailures failure, String error, String errorCode = null)
            => new TextMessageSendResult
            {
                Reference = msg?.Reference,
                Status = TextMessageStatuses.Failed,
                Channel = channel,
                Failure = failure,
                Error = error,
                ErrorCode = errorCode,
            };

        /// <summary>
        /// Check if a message may be sent using any of the channels
        /// </summary>
        /// <param name="msg">The message</param>
        /// <param name="channels">The channel(s) to check</param>
        /// <returns>True if allowed</returns>
        public static bool Allows(this OutgoingTextMessage msg, TextMessageChannels channels)
        {
            var c = msg.Channels;
            return (c == TextMessageChannels.Any) || ((c & channels) != 0);
        }

        /// <summary>
        /// True if the status is final (no more updates are expected)
        /// </summary>
        public static bool IsFinal(this TextMessageStatuses status)
            => (status == TextMessageStatuses.Read) || (status == TextMessageStatuses.Failed);
    }
}
