using System;
using System.Threading;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    public class AnyTextMessage : IncomingTextMessage
    {
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

        public static String NewId() => "Fake" + Interlocked.Increment(ref Counter);

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;
    }

}
