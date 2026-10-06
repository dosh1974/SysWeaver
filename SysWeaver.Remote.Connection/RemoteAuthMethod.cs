namespace SysWeaver.Remote
{
    /// <summary>
    /// How the credentials of a <see cref="RemoteConnection"/> are used to authenticate.
    /// </summary>
    public enum RemoteAuthMethod
    {
        /// <summary>
        /// Use the Authorization http header for auth.
        /// Basic auth with user:password, Bearer if the user name is "bearer", or a custom header if the user name starts with '*' (ex: "*x-api-key").
        /// </summary>
        HttpAuth = 0,

        /// <summary>
        /// Use the SysWeaver login protocol (no plain text, no replay attacks), performed once when the connection is created, the session is kept using cookies.
        /// </summary>
        SysWeaverLogin,
    }
}


