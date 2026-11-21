using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// Flags of a chat message (ChatMessage.Flags), also read by the web client (Sw.Chat.Flags).
    /// </summary>
    [Flags]
    public enum ChatMessageFlags
    {
        /// <summary>
        /// The message is not complete yet (ex: an AI answer that is still being streamed), the client shows it as working.
        /// </summary>
        IsWorking = 1,

        /// <summary>
        /// The message is written by an AI (an answer of an AI chat session, or a message sent as its agent).
        /// The web client tags the sender with "AI", so people know that they talk to an AI (EU AI Act art. 50).
        /// </summary>
        FromAi = 2,

        /// <summary>
        /// The current user can remove the message without a confirmation.
        /// Set by the chat service on messages addressed to the user of the session, cleared on all others.
        /// </summary>
        CanRemove = 256,
    }

}
