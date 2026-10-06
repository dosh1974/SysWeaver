namespace SysWeaver
{
    /// <summary>
    /// The severity (log level) of a <see cref="Message"/>, in increasing order of severity.
    /// </summary>
    /// <remarks>
    /// Used both as the level of a message and as a filter threshold, see <see cref="IMessageHost.AcceptMessageAbove"/>.
    /// A host only accepts messages with a level strictly greater than the threshold, so a threshold of <see cref="All"/> accepts every level except <see cref="All"/> itself.
    /// </remarks>
    public enum MessageLevels
    {
        /// <summary>
        /// Lowest value, intended as a filter threshold that accepts all messages (not as the level of an actual message).
        /// </summary>
        All = 0,
        /// <summary>
        /// Very detailed diagnostic messages, typically only of interest during development.
        /// </summary>
        Debug,
        /// <summary>
        /// Informational messages about normal operation.
        /// </summary>
        Info,
        /// <summary>
        /// Something unexpected happened, but the operation could continue.
        /// </summary>
        Warning,
        /// <summary>
        /// An error occurred (often accompanied by an exception).
        /// </summary>
        Error,
    }

}
