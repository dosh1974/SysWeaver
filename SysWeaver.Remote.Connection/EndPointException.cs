using System;

namespace SysWeaver.Remote
{

    /// <summary>
    /// Exception thrown when the response status of a remote call isn't 200.
    /// </summary>
    public sealed class EndPointException : Exception
    {
        public EndPointException(String message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// The response code that genereated this exception
        /// </summary>
        public readonly int StatusCode;
    }

}
