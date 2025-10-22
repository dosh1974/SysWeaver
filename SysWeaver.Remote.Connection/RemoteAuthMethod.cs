namespace SysWeaver.Remote
{
    public enum RemoteAuthMethod
    {
        /// <summary>
        /// Use the Authorization http header for auth.
        /// Can use Bearer or Basic 
        /// </summary>
        HttpAuth = 0,

        /// <summary>
        /// Use the SysWeaver login protocol (no plain text, no replay attacks)
        /// </summary>
        SysWeaverLogin,
    }
}


