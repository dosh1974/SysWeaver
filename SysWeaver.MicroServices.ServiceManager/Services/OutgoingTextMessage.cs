using System;


namespace SysWeaver.MicroService
{
    public class OutgoingTextMessage
    {
        public override string ToString() => String.Concat(PhoneNumber, ": ", Text);

        /// <summary>
        /// The phone number to send the message to, should be country code prefixed using +
        /// </summary>
        [EditMin(1)]
        public string PhoneNumber;

        /// <summary>
        /// The message text
        /// </summary>
        [EditMin(1)]
        public string Text;

        /// <summary>
        /// The system that this message belongs to
        /// </summary>
        public String System;

        /// <summary>
        /// The channels that may be used, <see cref="TextMessageChannels.Any"/> (default) to let the provider decide.
        /// </summary>
        public TextMessageChannels Channels;

        /// <summary>
        /// Optional sender (phone number, alphanumeric sender id etc), if not specified the provider selects one.
        /// </summary>
        public String From;

        /// <summary>
        /// Optional <see cref="IncomingTextMessage.ResponseToken"/> of a received message that this is a reply to.
        /// The reply is sent using the same provider, channel and number (if possible).
        /// </summary>
        public String ResponseToken;

        /// <summary>
        /// Optional template, used by channels that requires one (ex: WhatsApp outside the customer service window, registered templates in some countries).
        /// </summary>
        public TextMessageTemplate Template;

        /// <summary>
        /// The purpose of the message
        /// </summary>
        public TextMessagePurpose Purpose;

        /// <summary>
        /// The number of seconds that the provider should try to deliver the message, 0 for the provider default.
        /// Use a short time for one time passwords.
        /// </summary>
        public int TimeToLive;

        /// <summary>
        /// An optional reference of your choice, returned in the result and in status updates
        /// </summary>
        public String Reference;

        public OutgoingTextMessage Clone() => (OutgoingTextMessage)MemberwiseClone();
    }
}
