namespace SysWeaver.Remote
{
    /// <summary>
    /// Corresponds to the html request verb to use
    /// </summary>
    public enum HttpEndPointTypes
    {
        /// <summary>
        /// Perform a GET request, no payload body
        /// </summary>
        Get,
        /// <summary>
        /// Perform a POST request, all data will go into the body
        /// </summary>
        Post,
        /// <summary>
        /// Perform a PUT request, all data will go into the body
        /// </summary>
        Put,
        /// <summary>
        /// Perform a DELETE request, no payload body
        /// </summary>
        Delete,
    }


}
