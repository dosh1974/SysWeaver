using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// The speech synthesis voice to use for the messages of a sender (the web client speaks new messages with it).
    /// </summary>
    public sealed class ChatVoice
    {
        /// <summary>
        /// The user name that will be spoken with this voice
        /// </summary>
        public String Name;
        /// <summary>
        /// The preferred voice
        /// </summary>
        public String Voice;
        /// <summary>
        /// The BCP 47 code of the desired language.
        /// </summary>
        public String Language;
        /// <summary>
        /// True if a male voice is preferred over a female.
        /// </summary>
        public bool Male;
        /// <summary>
        /// [0.1, 10] Rate (speed) of the voice
        /// </summary>
        public float Rate = 1;
        /// <summary>
        /// [0, 2] Pitch of the voice
        /// </summary>
        public float Pitch = 1;
    }

}
