using System;

namespace SysWeaver.AI
{
    public enum AiContentPartKinds
    {
        /// <summary>
        /// Some text
        /// </summary>
        Text,
        /// <summary>
        /// An image (bytes or an uri)
        /// </summary>
        Image,
        /// <summary>
        /// A file (bytes or an uri)
        /// </summary>
        File,
    }

    /// <summary>
    /// An API agnostic message content part (typically used for attachments)
    /// </summary>
    public sealed class AiContentPart
    {
        public override string ToString() => String.Concat(Kind, ": ", Text ?? Filename ?? Uri?.ToString() ?? MimeType);

        /// <summary>
        /// The kind of content
        /// </summary>
        public AiContentPartKinds Kind { get; init; }

        /// <summary>
        /// The text (for text parts)
        /// </summary>
        public String Text { get; init; }

        /// <summary>
        /// The binary data (for image and file parts), null if an uri is used
        /// </summary>
        public BinaryData Data { get; init; }

        /// <summary>
        /// The mime type of the data (for image and file parts)
        /// </summary>
        public String MimeType { get; init; }

        /// <summary>
        /// An uri to the data (for image and file parts), null if the data is supplied
        /// </summary>
        public Uri Uri { get; init; }

        /// <summary>
        /// An optional filename (for file parts)
        /// </summary>
        public String Filename { get; init; }

        /// <summary>
        /// For images, true to request high detail processing (if supported by the API)
        /// </summary>
        public bool HighDetail { get; init; } = true;

        public static AiContentPart CreateTextPart(String text)
            => new AiContentPart { Kind = AiContentPartKinds.Text, Text = text };

        public static AiContentPart CreateImagePart(BinaryData data, String mimeType, bool highDetail = true)
            => new AiContentPart { Kind = AiContentPartKinds.Image, Data = data, MimeType = mimeType, HighDetail = highDetail };

        public static AiContentPart CreateImagePart(Uri uri, bool highDetail = true)
            => new AiContentPart { Kind = AiContentPartKinds.Image, Uri = uri, HighDetail = highDetail };

        public static AiContentPart CreateFilePart(BinaryData data, String mimeType, String filename)
            => new AiContentPart { Kind = AiContentPartKinds.File, Data = data, MimeType = mimeType, Filename = filename };

        public static AiContentPart CreateFilePart(Uri uri, String mimeType = null)
            => new AiContentPart { Kind = AiContentPartKinds.File, Uri = uri, MimeType = mimeType };
    }

}
