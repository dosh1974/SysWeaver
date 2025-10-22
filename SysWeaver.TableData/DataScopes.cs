namespace SysWeaver.Data
{
    /// <summary>
    /// Must be kept in sync with DataScopeTools.ScopePrefixes
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
        /// Unsupported as of now, data should be available for the user
        /// </summary>
        User,
    }

    public static class DataScopeTools
    {
        /// <summary>
        /// Must be kept in sync with enum
        /// </summary>
        public const string ScopePrefixes = "gasu";
    }


}
