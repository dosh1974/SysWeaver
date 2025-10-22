using System;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Remote;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Interface used for services that can send text messages to a phone number
    /// </summary>
    public interface ITextMessageSender : IDisposable
    {
        /// <summary>
        /// Send a text message
        /// </summary>
        /// <param name="msg">The message details</param>
        /// <returns></returns>
        [RemoteEndPoint(HttpEndPointTypes.Post)]
        Task Send(OutgoingTextMessage msg);
    }


    public static class TextMessageExt
    {
        /// <summary>
        /// Send a text message
        /// </summary>
        /// <param name="sender">Service instance</param>
        /// <param name="phoneNumber">The phone number to send it to, should be country code prefixed using +</param>
        /// <param name="text">The message text</param>
        /// <param name="system">An optional system</param>
        /// <returns></returns>
        public static Task Send(this ITextMessageSender sender, String phoneNumber, String text, String system = null)
            => sender.Send(new OutgoingTextMessage { PhoneNumber = phoneNumber, Text = text, System = system });

        
    }

    /// <summary>
    /// Interface for services that can receive text messages
    /// </summary>
    public interface ITextMessageReceiverService : IDisposable
    {
        /// <summary>
        /// Poll for any new text messages, should block until new messages are available
        /// </summary>
        /// <param name="request">Request parameters</param>
        /// <returns>The new messages or null if no new messages was recieved</returns>
        [RemoteEndPoint(HttpEndPointTypes.Post)]
        Task<IncomingTextMessage[]> GetIncoming(GetIncomingTextMessageRequest request);
    }



    public sealed class GetIncomingTextMessageRequest
    {
        /// <summary>
        /// The id of the last recieved message (start with null).
        /// Will block until a message arrives that have another id than this.
        /// </summary>
        public String LastId;

        /// <summary>
        /// If non-empty, only messages sent to this system is returned
        /// </summary>
        public String System;
    }



    public class OutgoingTextMessage
    {
        public override string ToString() => String.Concat(PhoneNumber, ": ", Text);

        /// <summary>
        /// The phone number that the message was sent from
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
    }


    /// <summary>
    /// Represents an incoming text message
    /// </summary>
    public class IncomingTextMessage
    {
        /// <summary>
        /// Two letter ISO-3166a2 country code, null means that this is not a country.
        /// </summary>
        [TableDataIsoCountryImage]
        public String Country;

        /// <summary>
        /// Name of the international phone prefix.
        /// </summary>
        public String Name;

        /// <summary>
        /// The phone number that the message was sent from
        /// </summary>
        public string PhoneNumber;
        
        /// <summary>
        /// The message text
        /// </summary>
        [TableDataText(160)]
        public string Text;

        /// <summary>
        /// The UTC time stamp of when this message was recieved
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// The system that this message belongs to
        /// </summary>
        public String System;

        /// <summary>
        /// The message id (sequence counter), use the latest to pool for more incoming messages
        /// </summary>
        public String Id;


        public override string ToString() => String.Concat('#', Id, ' ', PhoneNumber, ": ", Text);

        public void CopyFrom(IncomingTextMessage from)
        {
            Id = from.Id;
            PhoneNumber = from.PhoneNumber;
            Text = from.Text;
            Time = from.Time;
            Name = from.Name;
            Country = from.Country;
            System = from.System;
        }

        public IncomingTextMessage Clone()
        {
            var t = new IncomingTextMessage();
            t.CopyFrom(this);
            return t;
        }

    }
}
