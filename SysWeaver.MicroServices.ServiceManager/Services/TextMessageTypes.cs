using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// The channels (transports) that a text message can be sent or received on
    /// </summary>
    [Flags]
    public enum TextMessageChannels
    {
        /// <summary>
        /// Any channel (when used as a filter), or unknown
        /// </summary>
        Any = 0,
        /// <summary>
        /// Plain SMS
        /// </summary>
        Sms = 1,
        /// <summary>
        /// MMS
        /// </summary>
        Mms = 2,
        /// <summary>
        /// WhatsApp
        /// </summary>
        WhatsApp = 4,
        /// <summary>
        /// RCS (Rich Communication Services)
        /// </summary>
        Rcs = 8,
        /// <summary>
        /// Any other channel
        /// </summary>
        Other = 0x40000000,
    }

    /// <summary>
    /// The purpose of a message, providers may use it to select the route, message type, template category etc
    /// </summary>
    public enum TextMessagePurpose
    {
        /// <summary>
        /// Transactional (notifications, replies etc)
        /// </summary>
        Transactional = 0,
        /// <summary>
        /// A one time password / verification code
        /// </summary>
        Otp,
        /// <summary>
        /// Marketing / promotional
        /// </summary>
        Marketing,
    }

    /// <summary>
    /// The delivery status of a message
    /// </summary>
    public enum TextMessageStatuses
    {
        Unknown = 0,
        /// <summary>
        /// Accepted (queued) by the provider
        /// </summary>
        Accepted,
        /// <summary>
        /// Sent to the carrier / network
        /// </summary>
        Sent,
        /// <summary>
        /// Delivered to the device
        /// </summary>
        Delivered,
        /// <summary>
        /// Read by the user (only some channels)
        /// </summary>
        Read,
        /// <summary>
        /// The message couldn't be delivered, see <see cref="TextMessageFailures"/>
        /// </summary>
        Failed,
    }

    /// <summary>
    /// Why a message failed, normalized between providers (used to decide if another channel / provider should be tried)
    /// </summary>
    public enum TextMessageFailures
    {
        /// <summary>
        /// Not failed
        /// </summary>
        None = 0,
        /// <summary>
        /// The recipient can't receive messages on this channel (not a WhatsApp user, a landline for SMS etc), another channel might work
        /// </summary>
        NotOnChannel,
        /// <summary>
        /// The phone number isn't valid
        /// </summary>
        InvalidNumber,
        /// <summary>
        /// The recipient have blocked the sender or opted out
        /// </summary>
        Blocked,
        /// <summary>
        /// Free form messages are not allowed (ex: outside the WhatsApp 24 hour customer service window), a template is required
        /// </summary>
        OutsideWindow,
        /// <summary>
        /// The recipient is temporarily unreachable (phone off, carrier issues)
        /// </summary>
        Unreachable,
        /// <summary>
        /// The message was rejected (content filtering, policy, unregistered sender etc)
        /// </summary>
        Rejected,
        /// <summary>
        /// Too many messages
        /// </summary>
        RateLimited,
        /// <summary>
        /// The message expired before it could be delivered (see <see cref="OutgoingTextMessage.TimeToLive"/>)
        /// </summary>
        Expired,
        /// <summary>
        /// Any other reason
        /// </summary>
        Other,
    }

    /// <summary>
    /// A template (pre-registered message), required by some channels and countries
    /// </summary>
    public sealed class TextMessageTemplate
    {
        public override string ToString() => String.Concat(Name, String.IsNullOrEmpty(Language) ? "" : " [" + Language + ']');

        /// <summary>
        /// The provider specific template name / id.
        /// Ex: the WhatsApp template name, a Twilio content SID, a registered template id (India DLT etc).
        /// </summary>
        public String Name;

        /// <summary>
        /// The language (locale) of the template, ex: "en", "en_US"
        /// </summary>
        public String Language;

        /// <summary>
        /// Values for the template variables, in order.
        /// If null, providers that support it use the message text as the single variable.
        /// </summary>
        public String[] Parameters;
    }

    /// <summary>
    /// The result of sending a message
    /// </summary>
    public sealed class TextMessageSendResult
    {
        public override string ToString() => String.Concat('#', Id, ' ', Channel, ' ', Status, Failure == TextMessageFailures.None ? "" : " " + Failure + ": " + Error);

        /// <summary>
        /// The provider message id, status updates for the message uses the same id (null if the message wasn't accepted)
        /// </summary>
        public String Id;

        /// <summary>
        /// The <see cref="OutgoingTextMessage.Reference"/> of the message
        /// </summary>
        public String Reference;

        /// <summary>
        /// The current status, typically <see cref="TextMessageStatuses.Accepted"/> (delivery is reported asynchronously using status updates) or <see cref="TextMessageStatuses.Failed"/>
        /// </summary>
        public TextMessageStatuses Status;

        /// <summary>
        /// The channel used
        /// </summary>
        public TextMessageChannels Channel;

        /// <summary>
        /// The sender (number, alphanumeric sender id etc) that was used
        /// </summary>
        public String From;

        /// <summary>
        /// The name of the provider (service instance) that handled the message, set by routers
        /// </summary>
        public String Provider;

        /// <summary>
        /// The reason, if the status is failed
        /// </summary>
        public TextMessageFailures Failure;

        /// <summary>
        /// The provider specific error code (if any), ex: "131026", "30006"
        /// </summary>
        public String ErrorCode;

        /// <summary>
        /// A human readable error message (if any)
        /// </summary>
        public String Error;
    }

    /// <summary>
    /// A change of the delivery status of a sent message
    /// </summary>
    public sealed class TextMessageStatusUpdate
    {
        public override string ToString() => String.Concat('#', MessageId, ' ', PhoneNumber, ' ', Channel, ' ', Status, Failure == TextMessageFailures.None ? "" : " " + Failure + ": " + Error);

        /// <summary>
        /// The id of this update (use the latest to poll for more)
        /// </summary>
        public String Id;

        /// <summary>
        /// The id of the message (same as <see cref="TextMessageSendResult.Id"/>)
        /// </summary>
        public String MessageId;

        /// <summary>
        /// The <see cref="OutgoingTextMessage.Reference"/> of the message (if known)
        /// </summary>
        public String Reference;

        /// <summary>
        /// The system of the message (if known)
        /// </summary>
        public String System;

        /// <summary>
        /// The recipient phone number (if known)
        /// </summary>
        public String PhoneNumber;

        /// <summary>
        /// The channel used
        /// </summary>
        public TextMessageChannels Channel;

        /// <summary>
        /// The new status
        /// </summary>
        public TextMessageStatuses Status;

        /// <summary>
        /// The reason, if the status is failed
        /// </summary>
        public TextMessageFailures Failure;

        /// <summary>
        /// The provider specific error code (if any)
        /// </summary>
        public String ErrorCode;

        /// <summary>
        /// A human readable error message (if any)
        /// </summary>
        public String Error;

        /// <summary>
        /// The name of the provider (service instance) that sent the message, set by routers
        /// </summary>
        public String Provider;

        /// <summary>
        /// The UTC time stamp of the status change
        /// </summary>
        public DateTime Time;

        public TextMessageStatusUpdate Clone() => (TextMessageStatusUpdate)MemberwiseClone();
    }

    /// <summary>
    /// Request for status updates
    /// </summary>
    public sealed class GetTextMessageStatusRequest
    {
        /// <summary>
        /// The id of the last received update (start with null).
        /// Will block until an update arrives that is newer than this.
        /// </summary>
        public String LastId;

        /// <summary>
        /// If non-empty, only updates of messages sent by this system are returned
        /// </summary>
        public String System;
    }

}
