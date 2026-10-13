using System;

namespace SysWeaver.TextMessage.WhatsAppApi
{
    //  The webhook payloads posted by Meta to the callback url.
    //  See: https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks/components

    /// <summary>
    /// The root of a webhook notification
    /// </summary>
    public sealed class WaWebhook
    {
        /// <summary>
        /// Always "whatsapp_business_account"
        /// </summary>
        public String @object;

        public WaEntry[] entry;
    }

    public sealed class WaEntry
    {
        /// <summary>
        /// The WhatsApp Business Account id
        /// </summary>
        public String id;

        public WaChange[] changes;
    }

    public sealed class WaChange
    {
        /// <summary>
        /// The subscribed field, "messages" for incoming messages and status updates
        /// </summary>
        public String field;

        public WaValue value;
    }

    public sealed class WaValue
    {
        public String messaging_product;

        /// <summary>
        /// The business phone number that received the message (or sent the message for statuses)
        /// </summary>
        public WaMetadata metadata;

        /// <summary>
        /// The senders of incoming messages
        /// </summary>
        public WaContact[] contacts;

        /// <summary>
        /// Incoming messages
        /// </summary>
        public WaIncomingMessage[] messages;

        /// <summary>
        /// Status updates for outgoing messages
        /// </summary>
        public WaStatus[] statuses;

        public WaError[] errors;
    }

    public sealed class WaMetadata
    {
        /// <summary>
        /// The business phone number, ex: "15550001234"
        /// </summary>
        public String display_phone_number;

        /// <summary>
        /// The business phone number id
        /// </summary>
        public String phone_number_id;
    }

    /// <summary>
    /// An incoming message
    /// </summary>
    public sealed class WaIncomingMessage
    {
        /// <summary>
        /// The sender WhatsApp id (phone number digits)
        /// </summary>
        public String from;

        /// <summary>
        /// The message id (wamid)
        /// </summary>
        public String id;

        /// <summary>
        /// Unix time stamp (seconds) as a string
        /// </summary>
        public String timestamp;

        /// <summary>
        /// The message type, ex: "text", "button", "interactive", "image", "audio", "video", "document", "sticker", "location", "contacts", "reaction", "unsupported"
        /// </summary>
        public String type;

        public WaText text;

        public WaButton button;

        public WaInteractive interactive;

        public WaMedia image;

        public WaMedia audio;

        public WaMedia video;

        public WaMedia document;

        public WaMedia sticker;

        /// <summary>
        /// Set if the message is a reply
        /// </summary>
        public WaContext context;

        public WaError[] errors;
    }

    /// <summary>
    /// A quick reply button (of a template) pressed by the user
    /// </summary>
    public sealed class WaButton
    {
        public String text;

        public String payload;
    }

    public sealed class WaInteractive
    {
        /// <summary>
        /// "button_reply" or "list_reply"
        /// </summary>
        public String type;

        public WaReply button_reply;

        public WaReply list_reply;
    }

    public sealed class WaReply
    {
        public String id;

        public String title;

        public String description;
    }

    public sealed class WaMedia
    {
        /// <summary>
        /// The media id, can be used to download the media
        /// </summary>
        public String id;

        public String mime_type;

        public String caption;

        public String filename;
    }

    /// <summary>
    /// A status update of an outgoing message
    /// </summary>
    public sealed class WaStatus
    {
        /// <summary>
        /// The message id (wamid)
        /// </summary>
        public String id;

        /// <summary>
        /// "sent", "delivered", "read" or "failed"
        /// </summary>
        public String status;

        /// <summary>
        /// Unix time stamp (seconds) as a string
        /// </summary>
        public String timestamp;

        /// <summary>
        /// The recipient WhatsApp id
        /// </summary>
        public String recipient_id;

        /// <summary>
        /// Set when status is "failed"
        /// </summary>
        public WaError[] errors;
    }

}
