using System;
using System.Threading;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    /// <summary>
    /// A text message handled by the <see cref="FakeTextMessageService"/>, either sent by the system or a faked incoming message
    /// </summary>
    [SysWeaver.Data.TableDataCard("+message@fakeText/cards")]
    public class AnyTextMessage : IncomingTextMessage
    {
        /// <summary>
        /// True if this message was received (faked), false if it was sent by the system
        /// </summary>
        public bool Incoming;

        /// <summary>
        /// The delivery status (outgoing messages)
        /// </summary>
        public TextMessageStatuses Status;

        /// <summary>
        /// The failure reason (if failed)
        /// </summary>
        public TextMessageFailures Failure;

        /// <summary>
        /// The reference of an outgoing message
        /// </summary>
        public String Reference;

        /// <inheritdoc />
        public override string ToString()
        {
            //  Enums are formatted like ToString (current culture, the same as String.Concat of the boxed values)
            var incoming = Incoming;
            var failure = Failure;
            if (failure == TextMessageFailures.None)
                return incoming
                    ? $"#{Id} from {PhoneNumber} [{Channel}]: {Text}"
                    : $"#{Id} to {PhoneNumber} [{Channel}, {Status}]: {Text}";
            return incoming
                ? $"#{Id} from {PhoneNumber} [{Channel} {failure}]: {Text}"
                : $"#{Id} to {PhoneNumber} [{Channel}, {Status} {failure}]: {Text}";
        }

        /// <summary>
        /// Create a message with a new id and the current UTC time
        /// </summary>
        /// <param name="incoming">True if the message was received (faked), false if it was sent by the system</param>
        /// <param name="phoneNumber">The phone number of the other party</param>
        /// <param name="text">The message text</param>
        /// <param name="name">The name of the other party (may be null)</param>
        /// <param name="isoCountry">The two letter ISO country code of the phone number</param>
        /// <param name="system">The system that the message belongs to (may be null)</param>
        public AnyTextMessage(bool incoming, string phoneNumber, string text, String name, String isoCountry, String system)
        {
            Incoming = incoming;
            Id = NewId();
            Time = DateTime.UtcNow;
            PhoneNumber = phoneNumber;
            Name = name;
            Country = isoCountry;
            Text = text;
            System = system;
        }

        /// <summary>
        /// Create a new unique (incrementing) message id
        /// </summary>
        /// <returns>The id</returns>
        public static String NewId() => "Fake" + Interlocked.Increment(ref Counter);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;
    }

}
