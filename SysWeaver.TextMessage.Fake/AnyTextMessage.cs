using System;
using System.Threading;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{

    public class AnyTextMessage : IncomingTextMessage
    {
        public bool Incoming;

        public override string ToString() => String.Concat('#', Id, Incoming ? " from " : " to ", PhoneNumber, ": ", Text);

        public AnyTextMessage(bool incoming, string phoneNumber, string text, String name, String isoCountry, String system)
        {
            Incoming = incoming;
            Id = "Fake" + Interlocked.Increment(ref Counter);
            Time = DateTime.UtcNow;
            PhoneNumber = phoneNumber;
            Name = name;
            Country = isoCountry;
            Text = text;
            System = system;
        }

        static long Counter = DateTime.UtcNow.Ticks - new DateTime(2025, 1, 1).Ticks;
    }





}
