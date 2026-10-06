using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Throw this from a http request handler to respond with a specific http status code and message (instead of a 500 error).
    /// Also thrown by http clients when the server responds with an unexpected status code.
    /// </summary>
    /// <remarks>
    /// The http server translates the message from <see cref="Translate"/> to the session language (if a translator is available).
    /// </remarks>
    public sealed class HttpResponseException : Exception
    {

        static String DefMsg(int code)
        {
            String text = "Http request error [";
            switch (code)
            {
                case 404:
                    text = "Not Found - The server cannot find the requested resource [";
                    break;
                case 429:
                    text = "Too Many Requests - The client has sent too many requests in a given amount of time [";
                    break;
            }
            return String.Concat(text, code, ']');
        }


        /// <summary>
        /// Create an exception that results in a specific http response.
        /// </summary>
        /// <param name="responseCode">The http status code, ex: 404</param>
        /// <param name="message">The message (response text), null to use a default message ending with the status code in brackets</param>
        /// <param name="translateFrom">The language of the message (used for translation), null to never translate the message</param>
        public HttpResponseException(int responseCode, String message = null, String translateFrom = "en") : base(message ?? DefMsg(responseCode))
        {
            ResponseCode = responseCode;
            Translate = translateFrom;
        }
        /// <summary>
        /// The http status code
        /// </summary>
        public readonly int ResponseCode;
        /// <summary>
        /// The language of the message (used for translation), null if the message shouldn't be translated
        /// </summary>
        public readonly String Translate;
    }

}