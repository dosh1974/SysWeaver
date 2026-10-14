namespace SimpleStack.Orm.Logging
{
    /// <summary>
    /// Optionally implemented by an <see cref="ILogger{T}"/>: tells if debug messages are logged,
    /// so that the callers can skip the message arguments (a params array, boxed values) of messages that are dropped anyway.
    /// A logger that doesn't implement it is treated as logging debug messages
    /// </summary>
    public interface IDebugLogFilter
    {
        /// <summary>
        /// True if <see cref="ILogger{T}.LogDebug"/> messages are logged (can change at any time)
        /// </summary>
        bool IsDebugEnabled { get; }
    }
}