using System;
using SysWeaver.Data;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represents an incoming text message
    /// </summary>
    public class IncomingTextMessage
    {
        /// <summary>
        /// Two letter ISO-3166a2 country code of the sender phone number, null means that this is not a country.
        /// </summary>
        [TableDataIsoCountryImage]
        public String Country;

        /// <summary>
        /// Name of the international phone prefix (country) of the sender phone number.
        /// </summary>
        public String Name;

        /// <summary>
        /// The phone number that the message was sent from
        /// </summary>
        public string PhoneNumber;

        /// <summary>
        /// The name of the sender (if known), ex: the WhatsApp profile name
        /// </summary>
        public String SenderName;

        /// <summary>
        /// The phone number (or other sender id) that the message was sent to
        /// </summary>
        public String To;

        /// <summary>
        /// The channel that the message was received on
        /// </summary>
        public TextMessageChannels Channel;

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
        /// The message id, use the id of the latest message to poll for more incoming messages
        /// </summary>
        public String Id;

        /// <summary>
        /// A token that can be used when sending a reply (see <see cref="OutgoingTextMessage.ResponseToken"/>), content depends on the provider (can't be used across providers).
        /// </summary>
        public String ResponseToken;


        public override string ToString() => String.Concat('#', Id, ' ', PhoneNumber, ": ", Text);

        public void CopyFrom(IncomingTextMessage from)
        {
            Id = from.Id;
            PhoneNumber = from.PhoneNumber;
            SenderName = from.SenderName;
            To = from.To;
            Channel = from.Channel;
            Text = from.Text;
            Time = from.Time;
            Name = from.Name;
            Country = from.Country;
            System = from.System;
            ResponseToken = from.ResponseToken;
        }

        public IncomingTextMessage Clone()
        {
            var t = new IncomingTextMessage();
            t.CopyFrom(this);
            return t;
        }

    }
}
