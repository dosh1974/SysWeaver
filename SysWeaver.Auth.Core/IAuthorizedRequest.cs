namespace SysWeaver.Auth
{
    /// <summary>
    /// The caller of a web api, for services that can't reference the http server (ex: <see cref="SimpleAuthorizer"/>, to log who created an API key).
    /// A web api method may take it as its trailing parameter instead of the http server's request type: the http server passes the request
    /// (which implements this interface), it's not part of the api's input.
    /// </summary>
    public interface IAuthorizedRequest
    {
        /// <summary>
        /// The authorization of the caller (the signed in user or API key of the request's session), null if no one is signed in.
        /// </summary>
        Authorization CallerAuth { get; }
    }
}
