namespace SysWeaver.WebBrowser
{
    public sealed class WebViewBrowserParams
    {

        /// <summary>
        /// Cache at most this many free (unused) windows
        /// </summary>
        public int MaxFreeWindows = 16;

        /// <summary>
        /// Ignore any certificate errors, this is dangerous!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// If true, gpu is disabled, note that this is a global setting, multiple instances with different values can't be created within the same process.
        /// </summary>
        public bool DisableGPU;
    }

}
