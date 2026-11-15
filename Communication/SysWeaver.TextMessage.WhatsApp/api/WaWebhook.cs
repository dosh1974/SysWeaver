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

        /// <summary>
        /// The entries (one per business account)
        /// </summary>
        public WaEntry[] entry;
    }

    /// <summary>
    /// A webhook entry, the changes for a business account
    /// </summary>
    public sealed class WaEntry
    {
        /// <summary>
        /// The WhatsApp Business Account id
        /// </summary>
        public String id;

        /// <summary>
        /// The changes
        /// </summary>
        public WaChange[] changes;
    }

    /// <summary>
    /// A change of a subscribed field
    /// </summary>
    public sealed class WaChange
    {
        /// <summary>
        /// The subscribed field, "messages" for incoming messages and status updates
        /// </summary>
        public String field;

        /// <summary>
        /// The change data
        /// </summary>
        public WaValue value;
    }

    /// <summary>
    /// The data of a "messages" change: incoming messages and status updates
    /// </summary>
    public sealed class WaValue
    {
        /// <summary>
        /// The messaging product, always "whatsapp"
        /// </summary>
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

        /// <summary>
        /// Errors that aren't related to a specific message
        /// </summary>
        public WaError[] errors;
    }

    /// <summary>
    /// The business phone number that a notification belongs to
    /// </summary>
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

        /// <summary>
        /// The text, set for the type "text"
        /// </summary>
        public WaText text;

        /// <summary>
        /// The pressed button, set for the type "button"
        /// </summary>
        public WaButton button;

        /// <summary>
        /// The interactive reply, set for the type "interactive"
        /// </summary>
        public WaInteractive interactive;

        /// <summary>
        /// The image, set for the type "image"
        /// </summary>
        public WaMedia image;

        /// <summary>
        /// The audio, set for the type "audio"
        /// </summary>
        public WaMedia audio;

        /// <summary>
        /// The video, set for the type "video"
        /// </summary>
        public WaMedia video;

        /// <summary>
        /// The document, set for the type "document"
        /// </summary>
        public WaMedia document;

        /// <summary>
        /// The sticker, set for the type "sticker"
        /// </summary>
        public WaMedia sticker;

        /// <summary>
        /// Set if the message is a reply
        /// </summary>
        public WaContext context;

        /// <summary>
        /// Set if the message could not be received (type "unsupported")
        /// </summary>
        public WaError[] errors;
    }

    /// <summary>
    /// A quick reply button (of a template) pressed by the user
    /// </summary>
    public sealed class WaButton
    {
        /// <summary>
        /// The text of the button
        /// </summary>
        public String text;

        /// <summary>
        /// The payload of the button
        /// </summary>
        public String payload;
    }

    /// <summary>
    /// A reply to an interactive message (reply buttons or a list)
    /// </summary>
    public sealed class WaInteractive
    {
        /// <summary>
        /// "button_reply" or "list_reply"
        /// </summary>
        public String type;

        /// <summary>
        /// The reply, set if the type is "button_reply"
        /// </summary>
        public WaReply button_reply;

        /// <summary>
        /// The reply, set if the type is "list_reply"
        /// </summary>
        public WaReply list_reply;
    }

    /// <summary>
    /// The button or list item that the user selected
    /// </summary>
    public sealed class WaReply
    {
        /// <summary>
        /// The id of the button or list item
        /// </summary>
        public String id;

        /// <summary>
        /// The title of the button or list item
        /// </summary>
        public String title;

        /// <summary>
        /// The description of the list item (list replies only)
        /// </summary>
        public String description;
    }

    /// <summary>
    /// A media attachment (image, audio, video, document or sticker) of an incoming message
    /// </summary>
    public sealed class WaMedia
    {
        /// <summary>
        /// The media id, can be used to download the media
        /// </summary>
        public String id;

        /// <summary>
        /// The mime type of the media
        /// </summary>
        public String mime_type;

        /// <summary>
        /// The caption (images, videos and documents)
        /// </summary>
        public String caption;

        /// <summary>
        /// The file name (documents)
        /// </summary>
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
