using System;

namespace SysWeaver.TextMessage
{
    // ApiKey / CredFile = the access token (a system user token with the whatsapp_business_messaging permission),
    // used for all numbers that doesn't specify their own token.

    public sealed class WhatsAppTextMessageParams : ApiKeyParams
    {
        public override string ToString() => String.Concat(Numbers?.Length ?? 0, " number(s), webhook: ", WebhookUrl.ToQuoted(), ", Auth: ", base.ToString());

        /// <summary>
        /// The business phone numbers to send from and receive messages on.
        /// All numbers must belong to WhatsApp Business Accounts that are subscribed to the Meta app whose webhook points to this service.
        /// </summary>
        public WhatsAppNumberParams[] Numbers;

        /// <summary>
        /// The app secret of the Meta app (App settings, Basic), used to validate the signature of incoming webhook requests.
        /// Required to receive messages (webhook requests without a valid signature are rejected).
        /// </summary>
        public ApiKeyParams AppSecret;

        /// <summary>
        /// A secret string of your choice, Meta sends it when the webhook is configured in the Meta app (the "Verify token").
        /// Required to receive messages.
        /// </summary>
        public String VerifyToken;

        /// <summary>
        /// The local url of the webhook, the public url (https) must be entered as the "Callback URL" in the Meta app.
        /// Default: "whatsapp/webhook".
        /// </summary>
        public String WebhookUrl = "whatsapp/webhook";

        /// <summary>
        /// The Graph API version to use.
        /// Default: "v24.0".
        /// </summary>
        public String ApiVersion = "v24.0";

        /// <summary>
        /// The Graph API base url.
        /// Default: "https://graph.facebook.com/".
        /// </summary>
        public String GraphUrl = "https://graph.facebook.com/";

        /// <summary>
        /// An optional template to use when a free form text message can't be sent.
        /// WhatsApp only allows free form messages within 24 hours of the last message from the user (the customer service window), outside of it only approved templates can be sent.
        /// </summary>
        public WhatsAppTemplateParams Template;

        /// <summary>
        /// When a message to a phone number fails with error 131026 (undeliverable, typically not a WhatsApp user), messages to that number fails directly (without calling WhatsApp) with NotOnChannel for this number of hours.
        /// Use a router (TextMessageRouterService) to fall back to another channel (ex: SMS).
        /// Default: 24.
        /// </summary>
        public int NotOnWhatsAppHours = 24;

        /// <summary>
        /// If true, incoming messages are marked as read (blue check marks) when received.
        /// Default: false.
        /// </summary>
        public bool MarkAsRead;

        /// <summary>
        /// Number of times to try sending a message (before returning a failure).
        /// Requests rejected by WhatsApp are not retried.
        /// Default: 3.
        /// </summary>
        public int RetryCount = 3;

        /// <summary>
        /// Maximum size of the message queues (will try to keep them around this number)
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

    public sealed class WhatsAppNumberParams
    {
        public override string ToString() => String.Concat(PhoneNumber, " [", PhoneNumberId, ']', String.IsNullOrEmpty(System) ? "" : " System: " + System);

        /// <summary>
        /// The phone number id (from the WhatsApp Manager or "API Setup" in the Meta app), ex: "106540352242922".
        /// Required.
        /// </summary>
        public String PhoneNumberId;

        /// <summary>
        /// The phone number (with country code), ex: "+46 70 123 45 67".
        /// Used to select the sender for recipients in the same country, and for display.
        /// </summary>
        public String PhoneNumber;

        /// <summary>
        /// Optional system that this number belongs to.
        /// Incoming messages to this number gets this system, and outgoing messages for this system are sent from this number (if any).
        /// </summary>
        public String System;

        /// <summary>
        /// Optional list of two letter ISO-3166a2 country codes that this number should be used for (in addition to the country of the number).
        /// </summary>
        public String[] Countries;

        /// <summary>
        /// Optional access token for this number (if it belongs to another business portfolio), if not specified the token of the service is used.
        /// </summary>
        public ApiKeyParams Token;
    }

    public sealed class WhatsAppTemplateParams
    {
        public override string ToString() => String.Concat(Name, " [", Language, ']', AlwaysUse ? " always" : "");

        /// <summary>
        /// The name of an approved template that have a single body parameter ({{1}}) that is set to the text.
        /// A template specified in the message (OutgoingTextMessage.Template) takes precedence.
        /// Ex: a "utility" template with the body "{{1}}" and a footer, or "You have a new message: {{1}}".
        /// </summary>
        public String Name;

        /// <summary>
        /// The language code of the template.
        /// Default: "en".
        /// </summary>
        public String Language = "en";

        /// <summary>
        /// If true, the template is always used.
        /// If false, the template is used when the customer service window isn't known to be open (no message from the user within 24 hours), or a text message fails because the window is closed.
        /// Default: false.
        /// </summary>
        public bool AlwaysUse;
    }

}
