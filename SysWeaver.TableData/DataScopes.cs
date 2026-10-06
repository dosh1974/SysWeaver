namespace SysWeaver.Data
{
    /// <summary>
    /// The visibility scope of a server side <see cref="DataReference"/>.
    /// Must be kept in sync with <see cref="DataScopeTools.ScopePrefixes"/>.
    /// </summary>
    public enum DataScopes
    {
        /// <summary>
        /// Anyone can access the data, even without logging in
        /// </summary>
        Global = 0,
        /// <summary>
        /// Logged in users can access the data
        /// </summary>
        AnyUser,
        /// <summary>
        /// Data is only available for this session (logged in or not)
        /// </summary>
        Session,
        /// <summary>
        /// Data should only be available for the user (across sessions).
        /// Not supported yet, the HTTP server throws if this scope is used.
        /// </summary>
        User,
    }

    /// <summary>
    /// Helpers for <see cref="DataScopes"/>.
    /// </summary>
    public static class DataScopeTools
    {
        /// <summary>
        /// The id prefix char for each <see cref="DataScopes"/> value, indexed by the enum value
        /// ('g' = Global, 'a' = AnyUser, 's' = Session, 'u' = User).
        /// Must be kept in sync with the enum.
        /// </summary>
        public const string ScopePrefixes = "gasu";
    }


}
