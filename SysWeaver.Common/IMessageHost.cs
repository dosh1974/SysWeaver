using System;

namespace SysWeaver
{
    /// <summary>
    /// A sink for log messages, used throughout the framework for logging (the default implementation is <see cref="MessageHost"/>).
    /// </summary>
    public interface IMessageHost
    {
        /// <summary>
        /// Filter threshold, only messages with a level strictly greater than this value are accepted (others are silently discarded).
        /// </summary>
        MessageLevels AcceptMessageAbove { get; set; }

        /// <summary>
        /// Add a log message.
        /// </summary>
        /// <param name="message">The message text. A leading "[Prefix]" (one or more bracketed tokens) is treated as a prefix and aligned when rendered.</param>
        /// <param name="level">The severity of the message.</param>
        void AddMessage(String message, MessageLevels level = MessageLevels.Info);

        /// <summary>
        /// Add a log message together with an exception.
        /// </summary>
        /// <param name="message">The message text. A leading "[Prefix]" (one or more bracketed tokens) is treated as a prefix and aligned when rendered.</param>
        /// <param name="ex">The exception to include (may be null). Inner exceptions are rendered as well.</param>
        /// <param name="level">The severity of the message.</param>
        void AddMessage(String message, Exception ex, MessageLevels level = MessageLevels.Error);

        /// <summary>
        /// Indent all subsequent messages until the returned object is disposed.
        /// </summary>
        /// <param name="count">Number of tab stops to indent.</param>
        /// <returns>An object that removes the indentation when disposed.</returns>
        IDisposable Tab(int count = 1);

        /// <summary>
        /// Block until all pending (asynchronously processed) messages have been written and flush all outputs.
        /// </summary>
        void Flush();
    }

}
