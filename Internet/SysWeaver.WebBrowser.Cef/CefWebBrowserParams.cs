
using System;

namespace SysWeaver.WebBrowser
{
    /// <summary>
    /// Parameters for the <see cref="CefWebBrowser"/>
    /// </summary>
    public sealed class CefWebBrowserParams
    {
        /// <summary>
        /// Cache path, use null to disable disc cache (only use memory).
        /// Empty string to use default location.
        /// </summary>
        public String CachePath = "";

        /// <summary>
        /// Perform the init during construction, else delay until first browser creation
        /// </summary>
        public bool Init = true;

        /// <summary>
        /// Set to true to only log fatal errors (the CEF log is verbose otherwise)
        /// </summary>
        public bool SmallLog;

        /// <summary>
        /// Cache at most this many free (unused) windows
        /// </summary>
        public int MaxFreeWindows = 16;
    }

}
