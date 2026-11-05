using System;

namespace SysWeaver.Chat
{
    [Flags]
    public enum ChatMessageFlags
    {
        IsWorking = 1,

        /// <summary>
        /// The message is written by an AI (an answer of an AI chat session, or a message sent as its agent).
        /// The web client tags the sender with "AI", so people know that they talk to an AI (EU AI Act art. 50).
        /// </summary>
        FromAi = 2,

        CanRemove = 256,
    }

}
