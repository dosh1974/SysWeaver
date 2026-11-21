namespace SysWeaver.Chat
{
    /// <summary>
    /// Who receives the push messages of a chat change (IChatController).
    /// </summary>
    public enum ChatScopes
    {
        /// <summary>
        /// A shared chat that can be accessed by anyone
        /// </summary>
        Global,
        
        /// <summary>
        /// A chat bound to the current session
        /// </summary>
        Session,
    }




}
