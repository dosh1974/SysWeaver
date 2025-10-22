namespace SysWeaver.Chat
{
    /// <summary>
    /// The data required to set a new chat value
    /// </summary>
    public sealed class ChatValueRequest : ChatBaseRequest
    {
        /// <summary>
        /// The key to set a value for
        /// </summary>
        public string Key;

        /// <summary>
        /// The key to set a value for
        /// </summary>
        public string Value;

        /// <summary>
        /// Message Id (0 = Global command)
        /// </summary>
        public long Id;
    }
}
