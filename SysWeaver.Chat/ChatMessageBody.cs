using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// The body of a chat message
    /// </summary>
    public class ChatMessageBody
    {
        public override string ToString() => Text;
        
        /// <summary>
        /// The text
        /// </summary>
        [EditMultiline]
        [EditMax(4096)]
        public String Text;
        
        /// <summary>
        /// A link to an url with some data (typically images), can use have multiple data entries using semi colon as a separator
        /// </summary>
        [EditMax(2048)]
        public String Data;
        
        /// <summary>
        /// The format of the supplied text
        /// </summary>
        public ChatMessageFormats Format;

        /// <summary>
        /// The language used in this message.
        /// A two letter ISO 639-1 language code or a three letter ISO 639-2 language code, optionally combined with a two letter ISO 3166-A2 country code using a hyphen.
        /// </summary>
        public String Lang;

    }
}
