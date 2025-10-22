
using System;

namespace SysWeaver.WebBrowser
{
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
        /// Set to true to onlye log errors
        /// </summary>
        public bool SmallLog;

        /// <summary>
        /// Cache at most this many free (unused) windows
        /// </summary>
        public int MaxFreeWindows = 16;
    }

}
