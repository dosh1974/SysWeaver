using System;

namespace SysWeaver.WebBrowser
{
    /// <summary>
    /// The result of running javascript in a browser window
    /// </summary>
    public sealed class BrowserJsResponse
    {
        /// <inheritdoc />
        public override string ToString() => Ok ? Value?.ToString() : ("Failed: " + Message);


        /// <summary>
        /// True if the script ran without errors
        /// </summary>
        public readonly bool Ok;
        /// <summary>
        /// The value that the script returned (null if it failed or returned nothing)
        /// </summary>
        public readonly Object Value;
        /// <summary>
        /// The error message if the script failed
        /// </summary>
        public readonly String Message;

        /// <summary>
        /// Create a response
        /// </summary>
        /// <param name="ok">True if the script ran without errors</param>
        /// <param name="value">The value that the script returned</param>
        /// <param name="message">The error message if the script failed</param>
        public BrowserJsResponse(bool ok, object value, string message)
        {
            Ok = ok;
            Value = value;
            Message = message;
        }
    }


}
