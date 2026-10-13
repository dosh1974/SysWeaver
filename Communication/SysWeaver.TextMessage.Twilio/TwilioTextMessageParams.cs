using System;
using SysWeaver.MicroService;

namespace SysWeaver.TextMessage
{
    // User / Password (or CredFile) = the Account SID and Auth Token ("AC...:token"),
    // or an API key SID and secret ("SK...:secret", then AccountSid and AuthToken must be specified).

    public sealed class TwilioTextMessageParams : CredentialParams
    {
        public override string ToString() => String.Concat(Senders?.Length ?? 0, " sender(s), webhook: ", WebhookUrl.ToQuoted(), ", Auth: ", base.ToString());

        /// <summary>
        /// The Account SID ("AC..."), only required when the credentials are an API key.
        /// </summary>
        public String AccountSid;

        /// <summary>
        /// The Auth Token of the account, used to validate the signature of webhook requests from Twilio.
        /// Only required when the credentials are an API key (else the password is the auth token).
        /// </summary>
        public ApiKeyParams AuthToken;

        /// <summary>
        /// The senders to use (phone numbers, WhatsApp senders, alphanumeric sender ids, messaging services).
        /// </summary>
        public TwilioSenderParams[] Senders;

        /// <summary>
        /// The public base url of this server as seen by Twilio, ex: "https://my.server.com/".
        /// Required to receive status updates (the status callback url is built from it), and used to validate webhook signatures (the signature includes the url).
        /// If not specified, no status callback url is sent (a status callback configured on a messaging service is still used), and signatures are validated using the url of the request.
        /// </summary>
        public String PublicUrl;

        /// <summary>
        /// The local url of the webhooks:
        /// "[WebhookUrl]/incoming" must be configured as the incoming message webhook ("A message comes in", HTTP POST) of the numbers / messaging services / WhatsApp senders.
        /// "[WebhookUrl]/status" is sent as the status callback of all messages.
        /// Default: "twilio".
        /// </summary>
        public String WebhookUrl = "twilio";

        /// <summary>
        /// An optional template (Twilio Content API) to use for WhatsApp messages when the customer service window isn't known to be open.
        /// The template should have a single variable ({{1}}) that is set to the text.
        /// A template specified in the message (OutgoingTextMessage.Template) takes precedence.
        /// </summary>
        public TwilioTemplateParams WhatsAppTemplate;

        /// <summary>
        /// If true, the purpose of the message is sent as the MessageIntent (used by Twilio Traffic Shaping and the Compliance Toolkit).
        /// Default: false.
        /// </summary>
        public bool SendMessageIntent;

        /// <summary>
        /// When a message to a phone number fails with NotOnChannel (landline for SMS, not a WhatsApp user etc), messages to that number on that channel fails directly (without calling Twilio) for this number of hours.
        /// Default: 24.
        /// </summary>
        public int NotOnChannelHours = 24;

        /// <summary>
        /// Number of times to try sending a message (before returning a failure).
        /// Requests rejected by Twilio are not retried.
        /// Default: 3.
        /// </summary>
        public int RetryCount = 3;

        /// <summary>
        /// Maximum size of the message queues
        /// </summary>
        public int Size = 1000;

        /// <summary>
        /// Time out in seconds when waiting for new messages
        /// </summary>
        public int TimeOut = 30;

        /// <summary>
        /// Maximum number of messages to return on a poll
        /// </summary>
        public int MaxPoll = 100;
    }

    public sealed class TwilioSenderParams
    {
        public override string ToString() => String.Concat(From ?? MessagingServiceSid, " (", Channel, ')', String.IsNullOrEmpty(System) ? "" : " System: " + System);

        /// <summary>
        /// The sender: a Twilio phone number ("+15551234567"), a WhatsApp sender ("whatsapp:+15551234567"), an alphanumeric sender id ("MyBrand", send only) or a short code.
        /// Either this or MessagingServiceSid (or both) must be specified.
        /// </summary>
        public String From;

        /// <summary>
        /// Optional Messaging Service SID ("MG..."), Twilio selects the sender from the service's sender pool (if From isn't specified).
        /// </summary>
        public String MessagingServiceSid;

        /// <summary>
        /// The channel of this sender, Any (default) to derive it from From ("whatsapp:" = WhatsApp, "rcs:" = Rcs, else Sms).
        /// </summary>
        public TextMessageChannels Channel;

        /// <summary>
        /// Optional system that this sender belongs to.
        /// Incoming messages to this sender gets this system, and outgoing messages for this system are sent from this sender (if any).
        /// </summary>
        public String System;

        /// <summary>
        /// Optional list of two letter ISO-3166a2 country codes that this sender should be used for (in addition to the country of the number).
        /// </summary>
        public String[] Countries;
    }

    public sealed class TwilioTemplateParams
    {
        public override string ToString() => String.Concat(ContentSid, AlwaysUse ? " always" : "");

        /// <summary>
        /// The Content SID ("HX...") of an approved template with a single variable ({{1}}) that is set to the text.
        /// </summary>
        public String ContentSid;

        /// <summary>
        /// If true, the template is always used for WhatsApp messages.
        /// If false, the template is used when the customer service window isn't known to be open (no message from the user within 24 hours), or when a text message fails because the window is closed.
        /// Default: false.
        /// </summary>
        public bool AlwaysUse;
    }
}
