using System;
using System.Text.Json.Serialization;

namespace SysWeaver.TextMessage.WhatsAppApi
{
    //  Member names matches the json names of the WhatsApp Cloud API (snake case).
    //  Optional members are omitted when null, since the API rejects unexpected (null) objects.

    /// <summary>
    /// Request to send a message
    /// </summary>
    public sealed class WaSendRequest
    {
        /// <summary>
        /// Always "whatsapp"
        /// </summary>
        public String messaging_product = "whatsapp";

        /// <summary>
        /// Always "individual"
        /// </summary>
        public String recipient_type = "individual";

        /// <summary>
        /// The recipient phone number with country code (digits only), ex: "46701234567"
        /// </summary>
        public String to;

        /// <summary>
        /// The message type, "text" or "template" (others exists but are not used)
        /// </summary>
        public String type;

        /// <summary>
        /// The text, when type is "text"
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WaText text;

        /// <summary>
        /// The template, when type is "template"
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WaTemplate template;

        /// <summary>
        /// Optional, makes the message a reply to a previous message
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WaContext context;
    }

    /// <summary>
    /// A text message body
    /// </summary>
    public sealed class WaText
    {
        /// <summary>
        /// The text, max 4096 characters
        /// </summary>
        public String body;

        /// <summary>
        /// If true, the first url in the text is rendered as a link preview
        /// </summary>
        public bool preview_url;
    }

    /// <summary>
    /// A template message, templates must be created and approved in the WhatsApp Manager before use
    /// </summary>
    public sealed class WaTemplate
    {
        /// <summary>
        /// The template name
        /// </summary>
        public String name;

        /// <summary>
        /// The language of the template
        /// </summary>
        public WaLanguage language;

        /// <summary>
        /// Parameter values
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WaTemplateComponent[] components;
    }

    /// <summary>
    /// The language of a template
    /// </summary>
    public sealed class WaLanguage
    {
        /// <summary>
        /// The language (and locale) code, ex: "en", "en_US", "sv"
        /// </summary>
        public String code;
    }

    /// <summary>
    /// A component (body, header, button etc) of a template message
    /// </summary>
    public sealed class WaTemplateComponent
    {
        /// <summary>
        /// The component type, ex: "body", "header", "button"
        /// </summary>
        public String type;

        /// <summary>
        /// Values for the {{n}} placeholders, in order
        /// </summary>
        public WaTemplateParameter[] parameters;
    }

    /// <summary>
    /// A parameter value of a template component
    /// </summary>
    public sealed class WaTemplateParameter
    {
        /// <summary>
        /// The parameter type, ex: "text"
        /// </summary>
        public String type;

        /// <summary>
        /// The value for "text" parameters
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public String text;
    }

    /// <summary>
    /// The context of a message, used to send a message as a reply to another
    /// </summary>
    public sealed class WaContext
    {
        /// <summary>
        /// The id (wamid) of the message that this is a reply to
        /// </summary>
        public String message_id;
    }

    /// <summary>
    /// Response from sending a message
    /// </summary>
    public sealed class WaSendResponse
    {
        /// <summary>
        /// The messaging product, always "whatsapp"
        /// </summary>
        public String messaging_product;

        /// <summary>
        /// The recipients of the message
        /// </summary>
        public WaContact[] contacts;

        /// <summary>
        /// The ids of the sent messages
        /// </summary>
        public WaMessageId[] messages;
    }

    /// <summary>
    /// A WhatsApp user (a recipient in a send response, or the sender in a webhook)
    /// </summary>
    public sealed class WaContact
    {
        /// <summary>
        /// The phone number as sent in the request
        /// </summary>
        public String input;

        /// <summary>
        /// The WhatsApp id of the user (typically the phone number digits)
        /// </summary>
        public String wa_id;

        /// <summary>
        /// The user profile (only in webhooks)
        /// </summary>
        public WaProfile profile;
    }

    /// <summary>
    /// The profile of a WhatsApp user
    /// </summary>
    public sealed class WaProfile
    {
        /// <summary>
        /// The name that the user have set in WhatsApp
        /// </summary>
        public String name;
    }

    /// <summary>
    /// The id of a sent message
    /// </summary>
    public sealed class WaMessageId
    {
        /// <summary>
        /// The message id (wamid), used to correlate status webhooks
        /// </summary>
        public String id;

        /// <summary>
        /// Only for template messages, ex: "accepted"
        /// </summary>
        public String message_status;
    }

    /// <summary>
    /// Request to mark an incoming message as read
    /// </summary>
    public sealed class WaReadRequest
    {
        /// <summary>
        /// The messaging product, always "whatsapp"
        /// </summary>
        public String messaging_product = "whatsapp";

        /// <summary>
        /// The new status of the message, always "read"
        /// </summary>
        public String status = "read";

        /// <summary>
        /// The id (wamid) of the incoming message
        /// </summary>
        public String message_id;
    }

    /// <summary>
    /// Response from a request that only reports success
    /// </summary>
    public sealed class WaSuccessResponse
    {
        /// <summary>
        /// True if the request succeeded
        /// </summary>
        public bool success;
    }

    /// <summary>
    /// The body of a failed request
    /// </summary>
    public sealed class WaErrorResponse
    {
        /// <summary>
        /// The error
        /// </summary>
        public WaError error;
    }

    /// <summary>
    /// An error, used in failed requests and webhooks.
    /// See: https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes
    /// </summary>
    public sealed class WaError
    {
        /// <inheritdoc />
        public override string ToString() => String.Concat('(', code, ") ", title ?? message, String.IsNullOrEmpty(error_data?.details) ? "" : ": " + error_data.details);

        /// <summary>
        /// The error code, ex: 131026 (message undeliverable), 131047 (re-engagement message, more than 24 hours since the user replied)
        /// </summary>
        public int code;

        /// <summary>
        /// Error title (webhooks)
        /// </summary>
        public String title;

        /// <summary>
        /// Error message
        /// </summary>
        public String message;

        /// <summary>
        /// Error type (failed requests), ex: "OAuthException"
        /// </summary>
        public String type;

        /// <summary>
        /// The error sub code (failed requests)
        /// </summary>
        public int error_subcode;

        /// <summary>
        /// A trace id that can be given to Meta support
        /// </summary>
        public String fbtrace_id;

        /// <summary>
        /// Extra error data (webhooks)
        /// </summary>
        public WaErrorData error_data;
    }

    /// <summary>
    /// Extra data about an error
    /// </summary>
    public sealed class WaErrorData
    {
        /// <summary>
        /// The messaging product, always "whatsapp"
        /// </summary>
        public String messaging_product;

        /// <summary>
        /// Details about the error
        /// </summary>
        public String details;
    }

}
