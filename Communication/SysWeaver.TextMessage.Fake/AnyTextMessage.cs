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

        public override string ToString() => String.Concat('#', Id, Incoming ? " from " : " to ", PhoneNumber, " [", Channel, Incoming ? "" : ", " + Status, Failure == TextMessageFailures.None ? "" : " " + Failure, "]: ", Text);

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
