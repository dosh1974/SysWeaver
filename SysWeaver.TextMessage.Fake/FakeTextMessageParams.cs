using System;

namespace SysWeaver.TextMessage
{
    public sealed class FakeTextMessageParams
    {
        /// <summary>
        /// Maximum size of the message queues (will try to keep them around this number)
        /// </summary>
        public int Size = 250;

        /// <summary>
        /// Time out in seconds when waiting for new messages
        /// </summary>
        public int TimeOut = 30;

        /// <summary>
        /// Maximum number of messages to return on a poll
        /// </summary>
        public int MaxPoll = 100;
    }



}
