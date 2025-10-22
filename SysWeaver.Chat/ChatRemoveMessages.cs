namespace SysWeaver.Chat
{
    /// <summary>
    /// Used to indicate what type of messages a user can remove 
    /// </summary>
    public enum ChatRemoveMessages
    {
        /// <summary>
        /// The user is not allowed to remove any messages
        /// </summary>
        None,
        /// <summary>
        /// The user is allowed to their own messages
        /// </summary>
        Own,
        /// <summary>
        /// The user can remove any message
        /// </summary>
        Any,
    }

}
